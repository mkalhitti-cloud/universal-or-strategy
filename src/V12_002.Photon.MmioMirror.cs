using System;
using System.IO.MemoryMappedFiles;
using System.Threading;

// v29.0 Hybrid Arena MMIO Mirror v2.
//
// Header layout (192 bytes = 3 cache lines):
//   [0..8)     producer cursor
//   [8..64)    padding
//   [64..72)   consumer cursor
//   [72..128)  padding
//   [128..136) shadow salt
//   [136..144) capacity
//   [144..152) drop count
//   [152..160) high watermark
//   [160..192) reserved padding
//   [192..)    slot array

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        private sealed class MmioDispatchMirror : IDisposable
        {
            private const int HeaderBytes           = 192;
            private const long ProducerCursorOffset = 0;
            private const long ConsumerCursorOffset = 64;
            private const long ShadowSaltOffset     = 128;
            private const long CapacityOffset       = 136;
            private const long DropCountOffset      = 144;
            private const long HighWatermarkOffset  = 152;
            private const long SlotsBaseOffset      = 192;

            private readonly MemoryMappedFile         _mmf;
            private readonly MemoryMappedViewAccessor _accessor;
            private readonly int                      _capacity;
            private readonly int                      _mask;
            private readonly int                      _slotSize;
            private long                              _producerCursor;
            private long                              _highWatermark;
            private int                               _disposed;

            public string Name { get; private set; }

            public MmioDispatchMirror(string name, int capacity, int slotSize, ulong salt)
            {
                if (capacity < 2 || (capacity & (capacity - 1)) != 0)
                    throw new ArgumentException("Capacity must be power of 2", "capacity");
                if (slotSize <= 0 || (slotSize & 7) != 0)
                    throw new ArgumentException("Slot size must be a positive multiple of 8", "slotSize");

                _capacity = capacity;
                _mask     = capacity - 1;
                _slotSize = slotSize;
                Name      = name;

                long totalBytes = HeaderBytes + (long)slotSize * (long)capacity;

                _mmf      = MemoryMappedFile.CreateOrOpen(name, totalBytes, MemoryMappedFileAccess.ReadWrite);
                _accessor = _mmf.CreateViewAccessor(0, totalBytes, MemoryMappedFileAccess.ReadWrite);

                for (long i = 0; i < HeaderBytes; i++)
                    _accessor.Write(i, (byte)0);

                unchecked { _accessor.Write(ShadowSaltOffset, (long)salt); }
                _producerCursor = 0L;
                _highWatermark = 0L;
                _accessor.Write(ProducerCursorOffset, _producerCursor);
                _accessor.Write(ConsumerCursorOffset, 0L);
                _accessor.Write(CapacityOffset, (long)capacity);
                _accessor.Write(DropCountOffset, 0L);
                _accessor.Write(HighWatermarkOffset, 0L);
            }

            public bool TryPublish(ref FleetDispatchSlot slot)
            {
                if (Volatile.Read(ref _disposed) != 0)
                    return false;

                long prod = _producerCursor;
                long cons = 0L;
                _accessor.Read(ConsumerCursorOffset, out cons);
                long depth = prod - cons;
                if (depth >= _capacity)
                {
                    long drops = 0L;
                    _accessor.Read(DropCountOffset, out drops);
                    drops++;
                    _accessor.Write(DropCountOffset, drops);
                    return false;
                }

                int idx = (int)(prod & _mask);
                long slotOffset = SlotsBaseOffset + (long)idx * (long)_slotSize;
                _accessor.Write(slotOffset, ref slot);
                Thread.MemoryBarrier();

                _producerCursor = prod + 1;
                _accessor.Write(ProducerCursorOffset, _producerCursor);

                long newDepth = _producerCursor - cons;
                if (newDepth > _highWatermark)
                {
                    _highWatermark = newDepth;
                    _accessor.Write(HighWatermarkOffset, _highWatermark);
                }

                return true;
            }

            public void GetCursors(out long producerCursor, out long consumerCursor)
            {
                producerCursor = Volatile.Read(ref _producerCursor);
                _accessor.Read(ConsumerCursorOffset, out consumerCursor);
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0)
                    return;
                try { _accessor.Dispose(); } catch { }
                try { _mmf.Dispose();      } catch { }
            }

            public string GetDiagnostics()
            {
                long producerCursor;
                long consumerCursor;
                GetCursors(out producerCursor, out consumerCursor);

                return string.Format(
                    "MmioDispatchMirror v2: name={0} capacity={1} slotSize={2} prod={3} cons={4} high={5}",
                    Name, _capacity, _slotSize, producerCursor, consumerCursor, Volatile.Read(ref _highWatermark));
            }
        }
    }
}
