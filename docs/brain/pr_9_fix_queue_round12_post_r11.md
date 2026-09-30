# PR #9 Round 12 Fix Queue - Post-Round 11 Bot Findings

**Date**: 2026-06-01  
**Branch**: `feature/src-phase7-new2-stopsync`  
**Commit**: `3bb861af` (Round 11 - 8 bot findings resolved)  
**Status**: NEW issues detected post-Round 11 push

## Bot Findings Summary

| Bot | Findings | Severity | Category |
|-----|----------|----------|----------|
| CodeFactor | 6 | P3 | Style (blank lines) |
| CodeScene | 1 | P0 | Complex Conditional (GATE FAILED) |
| Cubic AI | 1 | P0 | Jane Street Allocation Violation |
| **TOTAL** | **8** | **2 P0, 6 P3** | **2 BLOCKING** |

---

## P0 Issues (BLOCKING)

### P0-R12-1: Complex Conditional in HasActiveStopInAccountOrders
**Bot**: CodeScene  
**Location**: `src/V12_002.Orders.Management.StopSync.cs:487-490`  
**Issue**: Method has 3 branches, threshold = 2  
**Root Cause**: Round 11 fix V-3 added `|| o.Name.StartsWith("S_" + entryName + "_")` check

**Code**:
```csharp
if (
    IsOrderActiveOrPending(o)
    && IsProtectiveStopOrder(o)
    && (o.Name.EndsWith(suffix) || o.Name.StartsWith("S_" + entryName + "_"))
)
```

**Branch Count**:
1. `IsOrderActiveOrPending(o)` - branch 1
2. `IsProtectiveStopOrder(o)` - branch 2
3. `o.Name.EndsWith(suffix) || o.Name.StartsWith("S_" + entryName + "_")` - branch 3 (compound OR)

**Jane Street Impact**: Exceeds CodeScene threshold (2 branches)  
**Verdict**: **VALID** - Requires extraction  
**Priority**: P0 (blocks CodeScene gate)

**Fix Strategy**: Extract the compound name-matching logic into a helper method:
```csharp
private bool IsStopOrderForEntry(Order o, string entryName, string suffix)
{
    return o.Name.EndsWith(suffix) || o.Name.StartsWith("S_" + entryName + "_");
}
```

**Expected Outcome**: Reduces `HasActiveStopInAccountOrders` from 3→2 branches

---

### P0-R12-2: Per-Iteration String Allocation in Hot Path
**Bot**: Cubic AI (Jane Street Enforcer)  
**Location**: `src/V12_002.Orders.Management.StopSync.cs:490`  
**Issue**: `"S_" + entryName + "_"` allocates heap string on every foreach iteration  
**Root Cause**: Round 11 fix V-3 added prefix check without hoisting concatenation

**Code**:
```csharp
foreach (Order o in Account.Orders)
{
    if (
        IsOrderActiveOrPending(o)
        && IsProtectiveStopOrder(o)
        && (o.Name.EndsWith(suffix) || o.Name.StartsWith("S_" + entryName + "_")) // ❌ ALLOCATION
    )
    {
        return true;
    }
}
```

**Jane Street Impact**: **CRITICAL** - Violates Rule 1.1 (ALLOCATION IS A BUG)  
**Verdict**: **VALID** - Requires hoisting  
**Priority**: P0 (Jane Street zero-allocation mandate)

**Fix Strategy**: Hoist both suffix strings before the loop:
```csharp
private bool HasActiveStopInAccountOrders(string entryName)
{
    string suffix = "_" + entryName;
    string prefix = "S_" + entryName + "_"; // ✅ Hoisted - single allocation
    
    foreach (Order o in Account.Orders)
    {
        if (
            IsOrderActiveOrPending(o)
            && IsProtectiveStopOrder(o)
            && (o.Name.EndsWith(suffix) || o.Name.StartsWith(prefix)) // ✅ Zero-allocation
        )
        {
            return true;
        }
    }
    return false;
}
```

**Expected Outcome**: Zero per-iteration allocations (Jane Street compliant)

---

## P3 Issues (STYLE - NON-BLOCKING)

### P3-R12-1 through P3-R12-6: Missing Blank Lines in Struct
**Bot**: CodeFactor  
**Location**: `src/V12_002.PositionInfo.cs:409-418`  
**Issue**: Struct properties should be separated by blank lines (CodeFactor style rule)  
**Root Cause**: Round 11 fix V-9 converted class to readonly struct without adding blank lines

**Current Code**:
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
    public bool BracketRestorationNeeded { get; init; }
}
```

**Jane Street Impact**: None (style-only)  
**Verdict**: **VALID** - CodeFactor style enforcement  
**Priority**: P3 (non-blocking, but should fix for consistency)

**Fix Strategy**: Add blank lines between properties:
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

    public bool BracketRestorationNeeded { get; init; }
}
```

**Expected Outcome**: CodeFactor style compliance

---

## Fix Execution Plan

### Phase 1: P0 Fixes (BLOCKING)

**Fix 1**: Hoist string concatenations in `HasActiveStopInAccountOrders`
- Location: Line 466-494
- Action: Add `string prefix = "S_" + entryName + "_";` before foreach loop
- Impact: Eliminates per-iteration heap allocation

**Fix 2**: Extract compound name-matching logic
- Location: Line 487-490
- Action: Create `IsStopOrderForEntry(Order o, string entryName, string suffix, string prefix)` helper
- Impact: Reduces `HasActiveStopInAccountOrders` from 3→2 branches

### Phase 2: P3 Fixes (STYLE)

**Fix 3**: Add blank lines between struct properties
- Location: `src/V12_002.PositionInfo.cs:409-418`
- Action: Insert blank lines between all 8 properties
- Impact: CodeFactor style compliance

### Phase 3: Validation

1. Run `dotnet build` (verify zero errors)
2. Run `dotnet csharpier format src/`
3. Run `powershell -File .\scripts\pre_push_validation.ps1 -Fast`
4. Commit and push
5. Wait 5 minutes for bot re-analysis
6. Verify CodeScene gate passes (Complex Conditional resolved)
7. Verify Cubic AI Jane Street violation resolved

---

## Expected Outcomes

**After Round 12 Fixes**:
- ✅ CodeScene gate: PASS (Complex Conditional eliminated)
- ✅ Cubic AI: PASS (zero per-iteration allocations)
- ✅ CodeFactor: PASS (struct formatting compliant)
- ✅ Jane Street compliance: MAINTAINED (zero-allocation hot path)
- ✅ Cyclomatic Complexity: MAINTAINED (CYC ≤ 15)
- ✅ Cognitive Complexity: MAINTAINED (≤ 15)

**Then Proceed To**:
- PositionInfo.cs duplication refactoring (9 CodeScene warnings, lines 119-381)
- PR Loop V2 continuation (Round 13+)

---

## Commit Message Template

```
fix(StopSync): eliminate allocation + complex conditional (Round 12)

P0 Fixes:
- Hoist "S_" + entryName + "_" prefix before foreach loop (zero-allocation)
- Extract IsStopOrderForEntry helper (reduces branches 3→2)

P3 Fixes:
- Add blank lines between PendingStopReplacement struct properties

Jane Street Compliance:
✅ Zero per-iteration allocations in HasActiveStopInAccountOrders
✅ Complex Conditional eliminated (CodeScene gate unblocked)

Resolves: CodeScene Complex Conditional, Cubic AI Jane Street violation
```

---

## Risk Assessment

**Risk Level**: LOW
- **Scope**: 2 methods in StopSync.cs, 1 struct in PositionInfo.cs
- **Blast Radius**: Isolated to order-scanning logic
- **Rollback**: Simple revert if issues detected
- **Testing**: Existing unit tests cover order-scanning behavior

---

## Next Steps After Round 12

1. **Verify bot re-analysis** (5 minutes post-push)
2. **Check CodeScene gate status** (should be PASS)
3. **Proceed with PositionInfo.cs duplication refactoring** (9 warnings, lines 119-381)
4. **Continue PR Loop V2** (Round 13+)
5. **Target PHS 100/100** (currently unknown, need bot re-analysis)