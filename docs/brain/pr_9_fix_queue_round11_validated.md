# PR #9 Round 11 - Validated Fix Queue

**Source**: `docs/brain/pr_9_forensics_round11_jane_street_audit.md`
**Validation Time**: 2026-05-31 17:34 PST (Updated: H-1 reclassified as V-9)
**Total Issues**: 8 VALID, 1 HALLUCINATION

## VALID Fixes (8 issues)

### V-1: CodeScene Complex Conditional (P0 - BLOCKING)
**Status**: VALID ✅
**Severity**: P0 (blocks merge)
**Location**: `HasActiveStopInAccountOrders` line 465-468
**Issue**: 3-branch condition exceeds CodeScene threshold (2)
**Jane Street Violation**: Cognitive simplicity principle
**Fix**: Extract `IsProtectiveStopOrder(Order o)` helper
```csharp
private bool IsProtectiveStopOrder(Order o)
{
    return (o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit);
}
```
**Impact**: Reduces branches from 3→2, unblocks CodeScene gate

### V-2: CodeRabbit ContainsKey Race (P1 - BUG)
**Status**: VALID ✅
**Severity**: P1 (correctness)
**Location**: `UpdateStopQuantity` lines 536-550
**Issue**: Check-then-read race on `stopOrders` dictionary
**Current Code**:
```csharp
if (!stopOrders.ContainsKey(entryName))
{
    return;
}
// ... later ...
Order currentStop = stopOrders[entryName];
```
**Problem**: Key can be removed between `ContainsKey` and indexer access
**Impact**: Indexer throws, exception flows to `catch`, clears `pendingStopReplacements`, calls emergency flatten
**Fix**: Replace with atomic `TryGetValue`
```csharp
if (!stopOrders.TryGetValue(entryName, out Order currentStop))
{
    return;
}
// currentStop is now safely available
```
**Jane Street Alignment**: Fail-fast principle (don't escalate benign races)

### V-3: CodeRabbit Missing "S_" Prefix (P1 - BUG)
**Status**: VALID ✅
**Severity**: P1 (correctness)
**Location**: `HasActiveStopInAccountOrders` lines 465-476
**Issue**: Fallback protection scan only checks `EndsWith("_" + entryName)`, misses local stops with `"S_" + entryName + "_"` prefix
**Current Code**:
```csharp
&& o.Name.EndsWith(suffix)  // suffix = "_" + entryName
```
**Problem**: Local protective stops use `"S_" + entryName + "_" + suffix` format (from `SubmitStopOrderToBroker`)
**Impact**: If `stopOrders` is stale/missing, can flatten a protected local position
**Fix**: Check both formats
```csharp
string followerSignalName = "S_" + entryName;
string localSignalPrefix = followerSignalName + "_";
// ... in condition ...
&& (o.Name == followerSignalName || o.Name.StartsWith(localSignalPrefix))
```
**Jane Street Alignment**: Correctness by construction (make illegal states unrepresentable)

### V-4: Gitar XML Doc Mis-Assignment (P1 - BUG)
**Status**: VALID ✅
**Severity**: P1 (documentation integrity)
**Location**: `IsOrderActiveOrPending` insertion point (line 432)
**Issue**: Inserted directly after `HandleEmergencyFlatten`'s `</summary>` without blank line
**Impact**: C# treats contiguous `///` lines as single block:
- `HandleEmergencyFlatten` loses its documentation
- `IsOrderActiveOrPending` gets malformed XML (nested `<summary>` tags)
**Fix**: Add blank line after `HandleEmergencyFlatten`'s `</summary>` tag
```csharp
/// </summary>
private void UpdateStopQuantity_HandleEmergencyFlatten(...)

/// <summary>
/// [Phase 7 NEW-2 Round 7] Helper: Check if order is in active/pending state
/// </summary>
private bool IsOrderActiveOrPending(Order order)
```
**Jane Street Alignment**: Fail-fast principle (malformed metadata breaks tooling)

### V-5: Cubic XML Doc Mis-Assignment (P2 - BUG)
**Status**: VALID ✅
**Severity**: P2 (documentation)
**Location**: `ShouldSkipStopQuantityUpdate` insertion point (line 443)
**Issue**: Helper inserted before `UpdateStopQuantity`, stealing its `<summary>` and `<remarks>`
**Impact**: `UpdateStopQuantity` loses critical V12.Audit [C-08] thread-safety documentation
**Fix**: Move `UpdateStopQuantity` doc-comment block to directly above `private void UpdateStopQuantity(...)`
```csharp
/// <summary>
/// [Phase 7 NEW-2 Round 10] Helper: Validate preconditions for stop quantity update
/// Reduces cognitive complexity (early return pattern extraction).
/// </summary>
private bool ShouldSkipStopQuantityUpdate(string entryName, PositionInfo pos)
{
    // ...
}

/// <summary>
/// Updates the stop order quantity after a partial target fill.
/// </summary>
/// <remarks>
/// V12.Audit [C-08]: Callers MUST ensure the <paramref name="pos"/> reference is
/// read under <c>stateLock</c> or from within a callback that is already serialized
/// by the NinjaTrader dispatch thread. Passing a stale <paramref name="pos"/> can
/// result in the stop being undersized relative to actual remaining contracts.
/// </remarks>
private void UpdateStopQuantity(string entryName, PositionInfo pos)
```
**Jane Street Alignment**: Correctness by construction (missing safety contract)

### V-6: CodeRabbit stateLock in Remarks (P2 - REFACTOR)
**Status**: VALID ✅
**Severity**: P2 (protocol compliance)
**Location**: Lines 523-531 (XML remarks for `UpdateStopQuantity`)
**Issue**: Remarks reference banned `lock(stateLock)` pattern
**Current Text**: "read under `stateLock` or from within a callback..."
**Problem**: `lock(stateLock)` is BANNED in V12 DNA (Lock-free Actor pattern mandate)
**Fix**: Update remarks to describe actor/dispatch-thread serialization only
```csharp
/// <remarks>
/// V12.Audit [C-08]: Callers MUST ensure the <paramref name="pos"/> reference is
/// obtained from the NinjaTrader dispatch thread or from within a callback that is
/// already serialized by that actor. Passing a stale <paramref name="pos"/> can
/// result in the stop being undersized relative to actual remaining contracts.
/// DO NOT use lock(stateLock) for internal logic - this pattern is BANNED.
/// </remarks>
```
**Jane Street Alignment**: Lock-free Actor pattern (no internal locks allowed)

### V-8: CodeFactor Missing Periods (P3 - STYLE)
**Status**: VALID ✅
**Severity**: P3 (style)
**Locations**: Lines 445, 453, 520
**Issue**: XML doc summaries missing terminal periods
**Fix**: Add periods to 3 summary lines
1. Line 445: `/// Reduces cognitive complexity (nested condition extraction).`
2. Line 453: `/// Reduces cognitive complexity (nested loop extraction).`
3. Line 520: `/// Reduces cognitive complexity (early return pattern extraction).`
**Jane Street Alignment**: None (style only)

### V-9: Greptile Heap Allocation (P1 - PERF) **[RECLASSIFIED FROM H-1]**
**Status**: VALID ✅ (was HALLUCINATION, now VALID after investigation)
**Severity**: P1 (performance - Jane Street zero-allocation violation)
**Location**: `src/V12_002.PositionInfo.cs:406` (class definition)
**Hot Path**: Line 393 in `UpdateStopQuantity` (`new PendingStopReplacement { ... }`)
**Issue**: `PendingStopReplacement` is a **class** (reference type), causing heap allocation on every stop replacement
**Evidence**:
```csharp
// Line 406: src/V12_002.PositionInfo.cs
private class PendingStopReplacement  // ← CLASS = HEAP ALLOCATION
{
    public string EntryName;
    public int Quantity;
    public double StopPrice;
    public MarketPosition Direction;
    public Order OldOrder;
    public DateTime CreatedTime;
    public TargetSnapshot[] CapturedTargets;
}

// Line 393: UpdateStopQuantity hot path
var newPending = new PendingStopReplacement { ... };  // ← HEAP ALLOCATION
```
**Jane Street Violation**: Zero-allocation principle (heap allocation in microsecond-latency hot path)
**Impact**:
- Heap allocation on every stop replacement (GC pressure)
- Reference semantics make mutation unsafe if struct conversion happens later
- Greptile correctly identified forward compatibility hazard

**Fix Part 1 - Convert to struct** (`src/V12_002.PositionInfo.cs:406`):
```csharp
private readonly struct PendingStopReplacement
{
    public string EntryName { get; init; }
    public int Quantity { get; init; }
    public double StopPrice { get; init; }
    public MarketPosition Direction { get; init; }
    public Order OldOrder { get; init; }
    public DateTime CreatedTime { get; init; }
    public TargetSnapshot[] CapturedTargets { get; init; }
}
```

**Fix Part 2 - Fix mutation pattern** (`src/V12_002.Orders.Management.StopSync.cs`):
```csharp
// BEFORE (UpdateStopQuantity_CreateReplacement):
if (pendingStopReplacements.TryGetValue(entryName, out var existingPendingQty))
{
    existingPendingQty.Quantity = remainingContracts;  // ← BREAKS WITH STRUCT (value copy)
    return;
}

// AFTER:
if (pendingStopReplacements.TryGetValue(entryName, out var existingPendingQty))
{
    // Struct is immutable - must create new instance with updated Quantity
    var updatedPending = new PendingStopReplacement
    {
        EntryName = existingPendingQty.EntryName,
        Quantity = remainingContracts,  // ← UPDATED
        StopPrice = existingPendingQty.StopPrice,
        Direction = existingPendingQty.Direction,
        OldOrder = existingPendingQty.OldOrder,
        CreatedTime = existingPendingQty.CreatedTime,
        CapturedTargets = existingPendingQty.CapturedTargets
    };
    pendingStopReplacements[entryName] = updatedPending;  // ← REASSIGN
    return;
}
```
**Verification**: 
- Eliminates heap allocation (stack allocation for struct)
- Fixes mutation pattern (immutable struct with reassignment)
- Greptile's forward compatibility warning resolved
**Jane Street Alignment**: Zero-allocation hot path (stack allocation only)

## HALLUCINATION (Suppress)

### H-2: Cubic UTC Timestamp Skew (FALSE POSITIVE) **[RECLASSIFIED FROM V-7]**
**Status**: HALLUCINATION ❌ (was VALID, now HALLUCINATION after verification)
**Claimed Severity**: P2
**Location**: Line 351 (claimed)
**Claim**: `PendingStopReplacement` timestamps on different time basis
**Reality**:
- Line 400: `CreatedTime = DateTime.UtcNow` (UTC)
- All other timestamps in V12 use `DateTime.UtcNow` (UTC)
- No timestamp skew exists
**Evidence**: Both use UTC basis, no inconsistency
**Action**: SUPPRESS - false positive

## Fix Execution Order

### Phase 1: Critical Correctness (P1 bugs)
1. **V-2**: Fix ContainsKey race (TryGetValue pattern)
2. **V-3**: Add "S_" prefix check (both stop formats)
3. **V-4**: Fix IsOrderActiveOrPending doc mis-assignment (blank line)
4. **V-9**: Convert PendingStopReplacement to struct + fix mutation pattern

### Phase 2: Blocking Gate (P0)
5. **V-1**: Extract IsProtectiveStopOrder helper (CodeScene unblock)

### Phase 3: Documentation & Protocol (P2)
6. **V-5**: Fix ShouldSkipStopQuantityUpdate doc mis-assignment (move doc block)
7. **V-6**: Remove stateLock from remarks (actor/dispatch-thread only)

### Phase 4: Style (P3)
8. **V-8**: Add periods to 3 XML doc summaries

## Validation Summary

- **Total Issues Analyzed**: 9
- **VALID**: 8 (88.9%)
- **HALLUCINATION**: 1 (11.1%)
- **Blocking**: 1 (CodeScene)
- **Critical**: 3 (ContainsKey race, missing "S_" prefix, heap allocation)
- **High**: 1 (XML doc mis-assignment for IsOrderActiveOrPending)
- **Medium**: 2 (XML doc mis-assignment for ShouldSkipStopQuantityUpdate, stateLock remarks)
- **Style**: 1 (missing periods)

## Risk Assessment

**Correctness Risk**: HIGH
- 3 critical bugs (race condition, incomplete protection check, heap allocation)
- Struct conversion requires careful mutation pattern fixes
- All require careful testing after fix

**Complexity Risk**: HIGH
- Struct conversion is non-trivial (requires mutation pattern changes)
- Must verify all `PendingStopReplacement` usage sites
- Clear fix patterns for other issues

**Jane Street Alignment**: CRITICAL
- V-9 is a direct violation of zero-allocation principle
- Must fix to maintain microsecond-latency performance
- Greptile correctly identified this as a real issue