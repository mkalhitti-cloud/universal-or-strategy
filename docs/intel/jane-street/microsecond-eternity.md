---
type: KnowledgeIntel
title: When a Microsecond Is an Eternity (Carl Cook)
description: Hot path zero-alloc patterns from Jane Street. JIT warmup, cache alignment, aggressive inlining, zero allocation. Applied to V12 hot-path execution.
tags: [microsecond, hot-path, zero-alloc, jit, cache, inlining, performance]
timestamp: 2026-06-25T00:00:00Z
---

# When a Microsecond Is an Eternity (Carl Cook)

**Source**: Jane Street talk by Carl Cook on microsecond-level HFT performance engineering

## Key Takeaways

- Hot path accounts for **1-5% of code**, must execute zero-alloc and with zero-jitter
- Branching and virtual functions evict caches; favor generics and compile-time specialization
- I-cache should be protected by extracting cold path logging out-of-line
- Warming up systems with dummy data trains hardware branch predictors and JIT compilers
- Pin background engine threads to isolated CPU cores while maintaining lock-free architecture

## V12 C# Patterns

### `jit_warmup`
Pre-compile execution loops before market open via simulated event cycles.
```csharp
private void WarmupJit() {
    // Feed dummy events to force JIT compilation of hot paths
    for (int i = 0; i < 1000; i++) {
        ProcessBar(dummyBar);   // triggers JIT for hot path
        EvaluateSignal(0.0, 0.0);
    }
}
```

### `cache_alignment`
`StructLayout Explicit` with `FieldOffset` to pad fields to 64-byte cache lines.
```csharp
[StructLayout(LayoutKind.Explicit, Size = 64)]
private struct HotState {
    [FieldOffset(0)]  public double LastPrice;
    [FieldOffset(8)]  public double Position;
    [FieldOffset(16)] public int    OrderId;
    // Remaining bytes = padding to fill 64-byte cache line
}
```

### `inlining`
`AggressiveInlining` on hot path, `NoInlining` on cold loggers.
```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private double ComputeSignal(double bid, double ask) => (bid + ask) * 0.5;

[MethodImpl(MethodImplOptions.NoInlining)]
private void LogDiagnostics(string message) { Print(message); }
```

### `zero_alloc`
Use struct passed by ref/in/out, avoid LINQ, preallocate pools.
```csharp
// BANNED on hot path
var signals = orders.Where(o => o.IsActive).Select(o => o.Price).ToList();

// CORRECT — zero alloc, preallocated buffer
private readonly double[] _signalBuffer = new double[MaxOrders];
private int ComputeSignals(ref Span<double> output) { /* fill output, return count */ }
```

## Complexity Impact

Hot-path extraction reduces CYC AND improves perf:
- `NoInlining` loggers extracted as cold methods → parent method CYC drops
- `AggressiveInlining` helpers are CYC=1-3 (simple, fast)
- `zero_alloc` patterns avoid LINQ chains that inflate CYC

## Cross-References
- [ocaml-performance-engineering.md](ocaml-performance-engineering.md) — struct_cache_locality
- [advanced-skylake-deep-dive.md](advanced-skylake-deep-dive.md) — CPU front-end, DSB cache
- [lock-free-patterns.md](lock-free-patterns.md) — zero-coordination hot path
