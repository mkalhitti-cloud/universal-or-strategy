---
type: KnowledgeIntel
title: The Cost of Concurrency Coordination (Jon Gjengset)
description: Cache coherency and false sharing patterns. Left-Right double-buffering for wait-free reads. Why reader-writer locks are worse than mutexes. Applied to V12 lock-free design.
tags: [concurrency, cache-coherency, false-sharing, left-right, wait-free, cache-line]
timestamp: 2026-06-25T00:00:00Z
---

# The Cost of Concurrency Coordination (Jon Gjengset)

**Source**: Jane Street engineering talk on the true cost of concurrency primitives

## Key Takeaways

- Locks are not slow; **hardware coordination of cache coherency is the true performance bottleneck**
- Reader-writer locks perform WORSE than mutexes under high contention (readers must write to a shared counter)
- Cache line ping-ponging across cores costs **~30-60ns per operation** vs 1ns L1 access
- **False sharing**: independent variables on the same 64-byte cache line cause unnecessary invalidations
- The **Left-Right pattern** uses double buffering + generation counters for wait-free, zero-coordination reads

## V12 C# Patterns

### `cache_alignment`
Align thread-local variables to separate 64-byte boundaries to prevent false sharing.
```csharp
[StructLayout(LayoutKind.Explicit, Size = 128)]
private struct PaddedCounter {
    [FieldOffset(0)]  public long Value;
    // 120 bytes padding = two full cache lines
    // Thread A and Thread B never share a cache line
}
```

### `lock_free_swmr`
Replace `ReaderWriterLockSlim` with Single-Writer Multi-Reader snapshot queues.
```csharp
// BANNED — reader-writer lock (readers write shared counter = cache ping-pong)
private ReaderWriterLockSlim _rwLock = new();

// CORRECT — immutable snapshot, zero coordination for readers
private volatile ImmutableArray<Order> _snapshot = ImmutableArray<Order>.Empty;
private void UpdateSnapshot(ImmutableArray<Order> next) {
    Volatile.Write(ref _snapshot, next);  // Single writer, atomic pointer swap
}
```

### `memory_barriers`
Use `Thread.MemoryBarrier` and `volatile` variables to prevent compiler/CPU memory reordering.
```csharp
// Ensure writes visible to all cores before signaling
_data = computedValue;
Volatile.Write(ref _ready, true);  // Full fence

// Reader
if (Volatile.Read(ref _ready)) {
    var data = _data;  // Guaranteed to see the write
}
```

## Why This Matters for V12

This is the deep technical reason WHY `lock()` is banned:
- A `lock()` call triggers a full cache line invalidation broadcast to all cores
- 30-60ns cost per lock acquisition = catastrophic for microsecond-level order management
- The Actor/Enqueue model (single-threaded FSM) eliminates ALL coordination overhead

## Cross-References
- [lock-free-patterns.md](lock-free-patterns.md) — the practical lock-free mandate
- [microsecond-eternity.md](microsecond-eternity.md) — cache_alignment hot path
- [ocaml-performance-engineering.md](ocaml-performance-engineering.md) — data race freedom
