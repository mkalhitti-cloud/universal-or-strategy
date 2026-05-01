using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Threading;

namespace SpscBench
{
    public unsafe struct CoreLane {
        public long Sequence;
        public double Value;
    }

    public static unsafe class XorShadow
    {
        public const ulong SHADOW_SALT = 0xDEADBEEFCAFEBABEUL;

        public static ulong Compute(byte* ptr, int length, ulong salt)
        {
            ulong accumulator = salt;
            int offset = 0;

            while (offset + sizeof(ulong) <= length)
            {
                accumulator ^= *(ulong*)(ptr + offset);
                offset += sizeof(ulong);
            }

            int shift = 0;
            while (offset < length)
            {
                accumulator ^= (ulong)ptr[offset] << shift;
                offset++;
                shift += 8;
            }

            return accumulator;
        }

        public static bool Validate(byte* ptr, int lengthExcludingShadow, ulong expected, ulong salt)
        {
            return Compute(ptr, lengthExcludingShadow, salt) == expected;
        }
    }

    public static unsafe class CoreLaneAllocator {
        public static unsafe void AllocAligned(int capacity, out CoreLane* ptr, out IntPtr handle) {
            int size = capacity * sizeof(CoreLane);
            handle = Marshal.AllocHGlobal(size + 128 + 63);
            long raw = (long)handle;
            long aligned = (raw + 63) & ~63;
            ptr = (CoreLane*)(aligned + 128);
            *(long*)aligned = 0;
            *(long*)(aligned + 64) = 0;
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public unsafe sealed class SpscRingV148 : IDisposable {
        [FieldOffset(64)] private int _producerIndex; 
        [FieldOffset(128)] private int _consumerIndex;
        [FieldOffset(136)] private long _cachedConsumer;
        [FieldOffset(160)] private int _mask;
        [FieldOffset(164)] private long _slotsRaw;
        [FieldOffset(172)] private long _handleRaw;
        [FieldOffset(180)] private int _capacity;
        [FieldOffset(184)] private int _disposed;
        [FieldOffset(192)] private long _shadowOffset;
        private CoreLane* Slots => (CoreLane*)_slotsRaw;

        // Aliases for Mmio submissions
        private byte* region => (byte*)_slotsRaw - 128; 
        private byte* _region => (byte*)_slotsRaw - 128; 
        private long mask => _mask;
        private long slotSize => sizeof(CoreLane);
        private long _slotSize => sizeof(CoreLane);
        private long shadowOffset => _shadowOffset;
        private long shadowLength => sizeof(CoreLane) - sizeof(ulong);
        private long _shadowLength => sizeof(CoreLane) - sizeof(ulong);
        private const ulong SHADOW_SALT = XorShadow.SHADOW_SALT;

        public SpscRingV148(int capacity) {
            _capacity = capacity; _mask = capacity - 1;
            CoreLane* ptr; IntPtr handle;
            CoreLaneAllocator.AllocAligned(capacity, out ptr, out handle);
            _slotsRaw = (long)ptr; _handleRaw = (long)handle;
            _shadowOffset = sizeof(CoreLane) - sizeof(ulong);
            for (int i = 0; i < capacity; i++) Slots[i].Sequence = i;
        }

        public unsafe bool TryEnqueue(double payload) {
            // Producer owns producerCursor -- plain (non-volatile) read.
            long prod = *(long*)_region;
            // consumerCursor is written by the other thread -- volatile read.
            long cons = Volatile.Read(ref *(long*)(_region + 64));

            if (prod - cons >= _capacity)
                return false;   // ring full

            byte* slot = _region + 128 + (prod & _mask) * _slotSize;

            // Copy the payload into the slot (overwrites whatever was there).
            Unsafe.CopyBlockUnaligned(
                destination: slot,
                source:      (byte*)Unsafe.AsPointer(ref Unsafe.AsRef(in payload)),
                byteCount:   (uint)_slotSize);

            // Compute and stamp the XorShadow over bytes [0, shadowLength).
            long shadow = XorShadow.Compute(slot, _shadowLength, SHADOW_SALT);
            *(ulong*)(slot + _shadowOffset) = shadow;

            // Publish: volatile write advances producerCursor.
            Volatile.Write(ref *(long*)_region, prod + 1);
            return true;
        }

        public unsafe bool TryDequeue(out double payload) {
            // Consumer owns consumerCursor -- plain (non-volatile) read.
            long cons = *(long*)(_region + 64);
            // producerCursor is written by the other thread -- volatile read.
            long prod = Volatile.Read(ref *(long*)_region);

            if (prod == cons)
            {
                payload = default;
                return false;   // ring empty
            }

            byte* slot = _region + 128 + (cons & _mask) * _slotSize;

            // Read the stamped shadow from the final 8 bytes of the slot.
            long stamped = *(ulong*)(slot + _shadowOffset);

            // Validate integrity before exposing data.
            if (!XorShadow.Validate(slot, _shadowLength, stamped, SHADOW_SALT))
            {
                payload = default;
                return false;
            }

            payload = Unsafe.ReadUnaligned<T>(slot);

            // Commit: volatile write advances consumerCursor.
            Volatile.Write(ref *(long*)(_region + 64), cons + 1);
            return true;
        }

        public void Dispose() {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0) {
                if (_handleRaw != 0) Marshal.FreeHGlobal((IntPtr)_handleRaw);
                _handleRaw = 0; _slotsRaw = 0;
            }
        }
    }

    class Program {
        static void Main() {
            try {
                var ring = new SpscRingV148(1024);
                const int warmUp = 100_000;
                const int iterations = 5_000_000;
                
                // Warm-up
                for (int i = 0; i < warmUp; i++) {
                    ring.TryEnqueue(i);
                    ring.TryDequeue(out _);
                }

                var sw = Stopwatch.StartNew();
                for (int i = 0; i < iterations; i++) {
                    if (!ring.TryEnqueue(i)) throw new Exception("Enqueue failed");
                    if (!ring.TryDequeue(out _)) throw new Exception("Dequeue failed");
                }
                sw.Stop();

                double mean = sw.Elapsed.TotalMilliseconds * 1000000.0 / iterations;
                Console.WriteLine($"| RoundTrip | {mean:F3} ns | 0 B |");
            } catch (Exception ex) {
                Console.WriteLine("ERROR: " + ex.Message);
            }
        }
    }
}
