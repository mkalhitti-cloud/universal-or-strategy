using System;
using System.Runtime.InteropServices;
using System.Threading;
using NinjaTrader.Cbi;

// v29.0 Hybrid Arena Photon Kernel: sovereign slot + XorShadow integrity
// ADR-012 + ADR-016 + ADR-019: zero-allocation fleet dispatch, lock-free SPSC, MMIO-ready payload

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        private const int PhotonPoolCapacity = 64; // 5 signals x 12 accounts = 60 < 64
        private const int MaxOrdersPerSlot   = 7;  // 1 entry + 1 stop + 5 targets

        // FleetDispatchSlot (v29.0 Sovereign, blittable, 64 bytes, cache-line sized).
        //
        // Layout contract (ADR-016 + ADR-019 Hybrid Arena):
        //   - Explicit layout so Marshal.OffsetOf is deterministic across framework versions.
        //   - Size = 64 B = exactly one cache line.
        //   - Shadow is the LAST 8 bytes (FieldOffset 56). XorShadow computes over [0..56)
        //     with Shadow zeroed during compute and restored before publish.
        //   - AccountIndex (0..AccountCount-1) and PoolRecordId (0..PoolCapacity-1) are
        //     first-class fields. The hot consumer resolves the managed account via
        //     _membrane.AccountByIndex and orders via _photonPool.GetByIndex(PoolRecordId).
        //   - SignalHash is a stable 64-bit hash of the fleet entry name used only for
        //     diagnostics and dedup logging.
        //
        // Blittable verification: only int, long, double, ulong primitives.
        // MemoryMappedViewAccessor.Write<FleetDispatchSlot> accepts this struct.
        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct FleetDispatchSlot
        {
            [FieldOffset(0)]  public double EntryPrice;
            [FieldOffset(8)]  public double StopPrice;
            [FieldOffset(16)] public long   SignalTicks;
            [FieldOffset(24)] public int    AccountIndex;
            [FieldOffset(28)] public int    PoolRecordId;
            [FieldOffset(32)] public int    Quantity;
            [FieldOffset(36)] public int    TargetCount;
            [FieldOffset(40)] public int    Action;
            [FieldOffset(44)] public int    ReservedDelta;
            [FieldOffset(48)] public ulong  SignalHash;
            [FieldOffset(56)] public ulong  Shadow;
        }

        // Managed sideband retired in v29.0. The sovereign slot carries AccountIndex and
        // PoolRecordId, and managed references are resolved through the frozen membrane.
        private ulong _photonShadowSalt;

        // === Pool Claim Result ===
        // V14.2 FIX-D1: Returns both the Order[] and its SlotIndex so the producer
        // can store the index in FleetDispatchSlot for O(1) consumer retrieval.

        private struct PoolClaimResult
        {
            public Order[] Orders;    // null if pool exhausted
            public int SlotIndex;     // -1 if pool exhausted
        }

        // === PhotonOrderPool ===
        // THREADING: MUST be called from strategy thread only. Not safe for concurrent access.
        // Claim() and Release() are separated by TriggerCustomEvent cycles on the SAME thread.
        // Interlocked usage is defensive (guards against future refactoring), not required
        // for current single-threaded access pattern.

        private sealed class PhotonOrderPool
        {
            private readonly Order[][] _orderArrays;
            private readonly int[] _freeStack;
            private volatile int _freeTop;
            private readonly int _capacity;
            private long _claimCount;
            private long _releaseCount;
            private long _exhaustedCount;

            public PhotonOrderPool(int capacity)
            {
                _capacity = capacity;
                _orderArrays = new Order[capacity][];
                _freeStack = new int[capacity];
                for (int i = 0; i < capacity; i++)
                {
                    _orderArrays[i] = new Order[MaxOrdersPerSlot];
                    _freeStack[i] = capacity - 1 - i;
                }
                _freeTop = capacity;
            }

            /// <summary>
            /// Claim a pre-allocated Order[] with its slot index. Returns null Orders if exhausted.
            /// </summary>
            public PoolClaimResult Claim()
            {
                int top = Interlocked.Decrement(ref _freeTop);
                if (top < 0)
                {
                    Interlocked.Increment(ref _freeTop);
                    Interlocked.Increment(ref _exhaustedCount);
                    return new PoolClaimResult { Orders = null, SlotIndex = -1 };
                }
                Interlocked.Increment(ref _claimCount);
                int slotIndex = _freeStack[top];
                Order[] arr = _orderArrays[slotIndex];
                for (int i = 0; i < MaxOrdersPerSlot; i++)
                    arr[i] = null;
                return new PoolClaimResult { Orders = arr, SlotIndex = slotIndex };
            }

            /// <summary>
            /// V14.2 FIX-D1: O(1) index-based retrieval. Consumer uses this to get Order[] from ring slot.
            /// </summary>
            public Order[] GetByIndex(int slotIndex)
            {
                if (slotIndex < 0 || slotIndex >= _capacity) return null;
                return _orderArrays[slotIndex];
            }

            /// <summary>
            /// V14.2 FIX-D1: O(1) index-based release. Eliminates O(N) ReferenceEquals scan.
            /// </summary>
            public void ReleaseByIndex(int slotIndex)
            {
                if (slotIndex < 0 || slotIndex >= _capacity) return;
                Order[] arr = _orderArrays[slotIndex];
                for (int i = 0; i < MaxOrdersPerSlot; i++)
                    arr[i] = null;
                int top = Interlocked.Increment(ref _freeTop) - 1;
                if (top < _capacity)
                {
                    _freeStack[top] = slotIndex;
                    Interlocked.Increment(ref _releaseCount);
                }
            }

            public string GetDiagnostics()
            {
                return string.Format(
                    "PhotonPool: free={0}/{1} claims={2} releases={3} exhausted={4}",
                    Volatile.Read(ref _freeTop), _capacity,
                    Interlocked.Read(ref _claimCount),
                    Interlocked.Read(ref _releaseCount),
                    Interlocked.Read(ref _exhaustedCount));
            }
        }

        // === FNV-1a 64-bit Hash ===

        private static long FnvHash64(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            long hash = unchecked((long)0xcbf29ce484222325L);
            long prime = unchecked((long)0x100000001b3L);
            for (int i = 0; i < s.Length; i++)
            {
                hash ^= s[i];
                hash = unchecked(hash * prime);
            }
            return hash;
        }

        // === ExecutionIdRing (ADR-011) ===
        // Single-threaded access only (strategy thread actor context).
        // ProcessOnExecutionUpdate runs within DrainActor, serialized by _drainToken.

        private sealed class ExecutionIdRing
        {
            private readonly long[] _ringHashes;
            private readonly long[] _ringTimestamps;
            private int _ringHead;
            private int _ringCount;
            private readonly int _ringCapacity;

            private readonly long[] _tableKeys;
            private readonly int[] _tableValues;
            private readonly int _tableMask;
            private int _tableCount;

            public long HitCount;
            public long MissCount;
            public long EvictCount;
            public long CollisionCount;

            private const long EMPTY_KEY = 0L;

            public ExecutionIdRing(int ringCapacity, int tableCapacity)
            {
                _ringCapacity = ringCapacity;
                _ringHashes = new long[ringCapacity];
                _ringTimestamps = new long[ringCapacity];
                _ringHead = 0;
                _ringCount = 0;

                if ((tableCapacity & (tableCapacity - 1)) != 0)
                    throw new ArgumentException("Table capacity must be power of 2");
                _tableKeys = new long[tableCapacity];
                _tableValues = new int[tableCapacity];
                _tableMask = tableCapacity - 1;
                _tableCount = 0;

                for (int i = 0; i < tableCapacity; i++)
                {
                    _tableKeys[i] = EMPTY_KEY;
                    _tableValues[i] = -1;
                }
            }

            public bool ContainsOrAdd(long hash)
            {
                if (hash == EMPTY_KEY) hash = 1L;

                int bucket = (int)(hash & _tableMask);
                int probes = 0;
                while (probes < _tableKeys.Length)
                {
                    long key = _tableKeys[bucket];
                    if (key == EMPTY_KEY) break;
                    if (key == hash)
                    {
                        HitCount++;
                        return true;
                    }
                    if (probes > 0) CollisionCount++;
                    bucket = (bucket + 1) & _tableMask;
                    probes++;
                }

                MissCount++;

                if (_ringCount >= _ringCapacity)
                {
                    int evictIndex = (_ringHead - _ringCount + _ringCapacity) % _ringCapacity;
                    long evictHash = _ringHashes[evictIndex];
                    if (evictHash != EMPTY_KEY)
                        TableRemove(evictHash);
                    _ringCount--;
                    EvictCount++;
                }

                _ringHashes[_ringHead] = hash;
                _ringTimestamps[_ringHead] = DateTime.UtcNow.Ticks;
                _ringHead = (_ringHead + 1) % _ringCapacity;
                _ringCount++;

                TableInsert(hash, _ringHead - 1);
                return false;
            }

            private void TableInsert(long hash, int ringIndex)
            {
                int bucket = (int)(hash & _tableMask);
                while (_tableKeys[bucket] != EMPTY_KEY)
                    bucket = (bucket + 1) & _tableMask;
                _tableKeys[bucket] = hash;
                _tableValues[bucket] = ringIndex;
                _tableCount++;
            }

            private void TableRemove(long hash)
            {
                int bucket = (int)(hash & _tableMask);
                while (true)
                {
                    if (_tableKeys[bucket] == EMPTY_KEY) return;
                    if (_tableKeys[bucket] == hash)
                    {
                        _tableKeys[bucket] = EMPTY_KEY;
                        _tableValues[bucket] = -1;
                        _tableCount--;
                        // Robin Hood deletion: rehash subsequent displaced entries
                        int next = (bucket + 1) & _tableMask;
                        while (_tableKeys[next] != EMPTY_KEY)
                        {
                            long rehashKey = _tableKeys[next];
                            int rehashVal = _tableValues[next];
                            _tableKeys[next] = EMPTY_KEY;
                            _tableValues[next] = -1;
                            _tableCount--;
                            TableInsert(rehashKey, rehashVal);
                            next = (next + 1) & _tableMask;
                        }
                        return;
                    }
                    bucket = (bucket + 1) & _tableMask;
                }
            }

            public string GetDiagnostics()
            {
                return string.Format(
                    "ExecIdRing: count={0}/{1} hits={2} misses={3} evicts={4} probeCollisions={5}",
                    _ringCount, _ringCapacity, HitCount, MissCount, EvictCount, CollisionCount);
            }
        }

        // ComputeFleetDispatchShadow (ADR-016)
        //
        // v29.0 XorShadow: XOR over [0..56) with Shadow zeroed. Salt is mixed at the end.
        private static ulong ComputeFleetDispatchShadow(ref FleetDispatchSlot slot, ulong salt)
        {
            unchecked
            {
                ulong h = 0UL;
                h ^= (ulong)BitConverter.DoubleToInt64Bits(slot.EntryPrice);
                h ^= (ulong)BitConverter.DoubleToInt64Bits(slot.StopPrice);
                h ^= (ulong)slot.SignalTicks;
                h ^= ((ulong)(uint)slot.AccountIndex) | ((ulong)(uint)slot.PoolRecordId << 32);
                h ^= ((ulong)(uint)slot.Quantity) | ((ulong)(uint)slot.TargetCount << 32);
                h ^= ((ulong)(uint)slot.Action) | ((ulong)(uint)slot.ReservedDelta << 32);
                h ^= slot.SignalHash;
                h ^= salt * 0x9E3779B97F4A7C15UL;
                return h;
            }
        }
    }
}
