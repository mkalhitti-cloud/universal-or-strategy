# Zero-GC Integration Analysis (Bob IDE Conversation)
**Date**: 2026-05-29
**Context**: Discussion with Bob IDE about achieving zero-heap, zero-GC in V12 hot path
**Status**: Protocol documentation - requires `protocol/zero-gc-strategy` branch

---

## Executive Summary

**Verdict**: Zero-heap, zero-GC is **achievable for hot path** (order callbacks, bar updates) but **impossible system-wide** due to NinjaTrader 8 framework constraints.

**Target**: 99% GC-free hot path with deferred GC during idle periods.

---

## Current State: Photon Substrate (Already Zero-GC Aligned)

✅ **Existing Zero-Allocation Infrastructure**:
- `V12_002.Photon.Ring.cs` - Lock-free ring buffer (SPSC/MPMC)
- `V12_002.Photon.Pool.cs` - Object pooling for reuse
- `V12_002.Photon.MmioMirror.cs` - Memory-mapped IO (zero-copy)

These are **Jane Street-aligned HFT patterns** already in production.

---

## Where Zero-GC is Achievable

### Hot Path (Microsecond-Critical)
✅ Order execution callbacks (`OnExecutionUpdate`)
✅ Bar updates (`OnBarUpdate`)
✅ SIMA dispatch (FSM/Actor enqueue)
✅ Ring buffer operations (lock-free, pre-allocated)
✅ Memory pool allocations (reuse, no new allocations)

### Techniques Available
```csharp
// ✅ Zero-allocation patterns:
- Stackalloc for small buffers: Span<byte> buffer = stackalloc byte[256];
- ArrayPool<T>.Shared.Rent() for temporary arrays
- Object pooling (existing V12_002.Photon.Pool.cs)
- Struct-based state machines (no heap allocation)
- Pre-allocated ring buffers (existing)
- Memory-mapped files for IPC (existing MmioMirror)
```

---

## Where Zero-GC is Impossible

### NinjaTrader 8 Framework Constraints

❌ **UI Layer (S3)** - WPF allocates heavily:
- `V12_002.UI.*.cs` files use WPF controls (Button, TextBox, etc.)
- Event handlers allocate delegates
- Data binding creates temporary objects

❌ **NinjaTrader API Calls**:
- `SubmitOrderUnmanaged()` - NT8 internal allocations
- `Account.Orders` - Returns managed collections
- `Instrument` objects - Framework-managed

❌ **String Operations**:
- Logging (`Print()`, `V12_002.StructuredLog.cs`)
- IPC commands (string parsing in `V12_002.UI.IPC.cs`)
- Telemetry (`V12_002.Telemetry.cs`)

❌ **LINQ/Collections**:
- Any `.Where()`, `.Select()`, `.ToList()` creates allocations
- Dictionary/List resizing triggers GC

---

## Jane Street Alignment Strategy

### Jane Street's Approach (OCaml-based)
1. Separate hot/cold paths ✅ (V12 already does: Photon/Morpheus split)
2. Pre-allocate everything in cold path (startup)
3. Use stack allocation for hot path (`stackalloc` in C#)
4. Pool objects aggressively ✅ (V12 has `Photon.Pool.cs`)
5. Defer GC to idle periods (`GC.TryStartNoGCRegion()`)

---

## Realistic Target for V12

### Achievable Goal: 99% GC-free hot path

```csharp
// Example: Zero-allocation order callback
public void OnExecutionUpdate(Execution execution, string executionId, 
    double price, int quantity, MarketPosition marketPosition, 
    string orderId, DateTime time)
{
    // ✅ Use pre-allocated structs
    var evt = _eventPool.Rent(); // Object pool
    evt.Price = price;
    evt.Quantity = quantity;
    
    // ✅ Enqueue to lock-free ring (zero-allocation)
    _photonRing.TryEnqueue(evt);
    
    // ❌ AVOID: string.Format(), LINQ, new objects
}
```

---

## Measurement Strategy

### GC Pressure Monitoring
```csharp
// Add to V12_002.Telemetry.cs
private long _gen0Before = GC.CollectionCount(0);
private long _gen1Before = GC.CollectionCount(1);
private long _gen2Before = GC.CollectionCount(2);

public void MeasureGCPressure()
{
    var gen0 = GC.CollectionCount(0) - _gen0Before;
    var gen1 = GC.CollectionCount(1) - _gen1Before;
    var gen2 = GC.CollectionCount(2) - _gen2Before;
    
    Print($"GC: Gen0={gen0}, Gen1={gen1}, Gen2={gen2}");
}
```

### Profiling Tools
- `dotnet-trace` - Find allocations in hot path
- `PerfView` - GC pause analysis
- `BenchmarkDotNet` - Allocation benchmarks

---

## Recommended Action Plan

### Phase 1: Audit (P2 Forensics)
1. ✅ Run `dotnet-trace` on hot path to find allocations
2. ✅ Identify LINQ usage in `OnBarUpdate`/`OnExecutionUpdate`
3. ✅ Measure baseline GC pressure during live trading

### Phase 2: Hot Path Hardening (P4/P5 Engineering)
1. Replace LINQ with `for`-loops in hot path
2. Use `Span<T>` for temporary buffers (C# 7.2+)
3. Pool all event objects (expand `V12_002.Photon.Pool.cs`)
4. Convert string operations to `ReadOnlySpan<char>` where possible

### Phase 3: GC Deferral (P4 Engineering)
```csharp
// Defer GC during market hours
GC.TryStartNoGCRegion(100_000_000); // 100MB budget
```

### Phase 4: Verification (P6 Forensics)
1. Measure GC counters before/after
2. Verify <1% GC in hot path during live trading
3. Document results in `docs/performance/zero_gc_results.md`

---

## Critical Constraint

**You cannot eliminate GC entirely because**:
- NinjaTrader 8 is a managed framework (not unmanaged C++)
- UI thread must allocate (WPF requirement)
- String operations are unavoidable for logging/IPC

**Best achievable**: Zero GC in the 10-microsecond hot path (order callbacks, bar updates), with GC deferred to idle periods.

---

## Jane Street Knowledge Base References

Query the Jane Street KB for:
- "Zero-allocation patterns in OCaml" (translate to C# `stackalloc`)
- "GC pause mitigation strategies"
- "Memory pool design for HFT"

```powershell
python scripts/query_kb.py "zero allocation"
python scripts/query_kb.py "GC pause mitigation"
python scripts/query_kb.py "memory pool HFT"
```

---

## Branch Strategy

**This documentation belongs on**: `protocol/zero-gc-strategy` branch

**Rationale**: Per Three-Tier Branch Model (V12.18):
- ❌ NOT on `fix/pr3-clean-cs-only` (source code branch)
- ✅ YES on `protocol/` branch (architectural guidance)

**Next Steps**:
1. Create `protocol/zero-gc-strategy` branch
2. Move this document to `docs/performance/zero_gc_strategy.md`
3. Create Epic tickets for Phase 1-4 implementation
4. Link to Jane Street KB queries in AGENTS.md

---

## Impact on Phase 7

**Phase 7 Status**: COMPLETE (per task.md)
- ✅ ZERO methods with CYC >20 across all 817 methods
- ✅ M-Phase COMPLETE (MP-0, MP-1, MP-2)
- ✅ BUILD_TAG: 1111.007-mphase-mp0

**Zero-GC Integration**: This is a **NEW initiative**, not part of Phase 7.

**Verdict**: **Phase 7 can finish as-is**. Zero-GC work is a separate Epic that should be planned after Phase 7 closure.

---

## Recommended Epic Structure

### EPIC-ZGC: Zero-GC Hot Path Hardening

**Prerequisites**:
- Phase 7 complete ✅
- Jane Street KB queries complete
- Baseline GC measurements captured

**Tickets**:
- ZGC-1: Audit hot path allocations (`dotnet-trace`)
- ZGC-2: Replace LINQ in `OnBarUpdate`/`OnExecutionUpdate`
- ZGC-3: Expand `Photon.Pool.cs` for event pooling
- ZGC-4: Implement `GC.TryStartNoGCRegion()` during market hours
- ZGC-5: Add GC pressure telemetry to `V12_002.Telemetry.cs`
- ZGC-6: Verification audit (P6 Forensics)

**Estimated Effort**: 3-5 Bob CLI sessions (P4/P5 surgical work)

---

## Conclusion

Zero-GC is **achievable for the hot path** but requires:
1. Dedicated Epic (EPIC-ZGC)
2. Protocol branch for documentation
3. Baseline measurements before implementation
4. Jane Street KB alignment

**Phase 7 is unblocked** - this is future work.
