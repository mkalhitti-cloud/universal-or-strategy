# PR #5 Iteration 6 Repair Design
Generated: 2026-05-30 00:02 UTC

## JANE STREET PRE-VALIDATION (MANDATORY)

**Issue**: Code duplication - fallback flatten logic repeated 6 times across catch blocks

**Jane Street Alignment Check**:
- ✅ **DRY Principle**: Violates "Don't Repeat Yourself" - maintenance burden
- ✅ **Cognitive Simplicity**: 6 identical blocks increase cognitive load
- ✅ **Single Source of Truth**: Changes must be applied 6 times (error-prone)
- ✅ **Testability**: Cannot unit test fallback logic in isolation
- ✅ **YAGNI Compliance**: Extraction is justified (already used 6 times)

**Verdict**: ✅ **VALID-FIX** - Aligns with Jane Street principles

---

## REPAIR DESIGN

### Objective
Extract duplicated fallback flatten logic into a single helper method to eliminate 6x code duplication.

### Target File
`src/V12_002.SIMA.Flatten.cs`

### Duplicated Code Locations (6 catch blocks)
1. Lines 99-113: `FlattenAllApexAccounts` - InvalidOperationException catch
2. Lines 147-161: `FlattenAllApexAccounts` - Exception catch
3. Lines 405-419: `ChainNextFlattenOp` - InvalidOperationException catch
4. Lines 455-469: `ChainNextFlattenOp` - Exception catch
5. Lines 677-691: `ClosePositionsOnlyApexAccounts` - InvalidOperationException catch
6. Lines 724-738: `ClosePositionsOnlyApexAccounts` - Exception catch

### Proposed Helper Method

```csharp
private void PerformFallbackFlatten(string callerContext)
{
    var drainedOps = new List<FlattenWorkItem>();
    FlattenWorkItem item;
    while (_pendingFlattenOps.TryDequeue(out item))
    {
        drainedOps.Add(item);
    }

    if (drainedOps.Count == 0) return;

    Print("[FLATTEN] Attempting fallback flatten for " + drainedOps.Count + " accounts (" + callerContext + ")...");

    foreach (var workItem in drainedOps)
    {
        try
        {
            Account acct = workItem.Account;
            if (acct == null)
            {
                Print("[FLATTEN] WARNING: NULL account in fallback flatten queue");
                continue;
            }
            ProcessFlattenWorkItem_CancelOrders(workItem, acct);
            if (!workItem.CancelOnly)
                ProcessFlattenWorkItem_ClosePositions(workItem, acct);
            SetExpectedPositionLocked(ExpKey(acct.Name), 0);
            Print("[FLATTEN] Fallback flatten succeeded for " + acct.Name);
        }
        catch (Exception flatEx)
        {
            Print("[FLATTEN] CRITICAL: Fallback flatten failed for " + (workItem.Account != null ? workItem.Account.Name : "NULL") + ": " + flatEx);
        }
    }
}
```

### Replacement Pattern (6 locations)

**BEFORE** (each catch block):
```csharp
catch (InvalidOperationException ex) when (ex.Message.Contains("TriggerCustomEvent"))
{
    Print("[FLATTEN] WARNING: Known NT8 quirk - TriggerCustomEvent failed: " + ex.Message);
    
    var drainedOps = new List<FlattenWorkItem>();
    FlattenWorkItem item;
    while (_pendingFlattenOps.TryDequeue(out item))
    {
        drainedOps.Add(item);
    }
    // ... 15 more lines of duplicated logic
}
```

**AFTER** (each catch block):
```csharp
catch (InvalidOperationException ex) when (ex.Message.Contains("TriggerCustomEvent"))
{
    Print("[FLATTEN] WARNING: Known NT8 quirk - TriggerCustomEvent failed: " + ex.Message);
    PerformFallbackFlatten("FlattenAllApexAccounts-KnownQuirk");
}
```

### Jane Street Validation

**Zero-Allocation Hot Path**: ✅ PASS
- Helper method uses existing `List<FlattenWorkItem>` (already allocated in original code)
- No new heap allocations introduced
- String concatenation already present (not adding new allocations)

**Lock-Free Concurrency**: ✅ PASS
- No locks introduced
- Uses existing `_pendingFlattenOps.TryDequeue()` (lock-free queue)
- No shared state mutations outside existing patterns

**Fail-Fast**: ✅ PASS
- Preserves existing null-check and exception handling
- No new error paths introduced
- Maintains existing CRITICAL logging on failures

**ASCII-Only**: ✅ PASS
- No Unicode characters in helper method
- All strings are ASCII-compliant

**Complexity Target**: ✅ PASS
- Helper method CYC: ~5 (simple loop + conditional)
- Reduces overall file complexity by eliminating duplication
- Each caller site CYC reduced by ~10 (replaced 15-line block with 1-line call)

### Expected Impact

**Code Reduction**:
- **Before**: 6 blocks × 15 lines = 90 lines of duplicated logic
- **After**: 1 helper method (30 lines) + 6 call sites (1 line each) = 36 lines
- **Net Reduction**: 54 lines eliminated

**Maintainability**:
- Future changes to fallback logic: 1 location instead of 6
- Testability: Can unit test `PerformFallbackFlatten()` in isolation
- Readability: Catch blocks now show intent (call helper) vs implementation (15 lines)

**PHS Impact**:
- **Current**: 5 resolved / 6 findings (~83/100)
- **After Fix**: 6 resolved / 6 findings (100/100)

### Verification Plan

1. **Build**: `dotnet build` must pass
2. **ASCII Gate**: `deploy-sync.ps1` must pass
3. **Complexity**: `complexity_audit.py` - verify CYC reduction
4. **Pre-Push**: `pre_push_validation.ps1` - all 13 checks must pass
5. **F5 Test**: NinjaTrader compile + BUILD_TAG verification

---

## JANE STREET FINAL VERDICT

✅ **APPROVED FOR IMPLEMENTATION**

**Rationale**:
- Eliminates code duplication (DRY principle)
- Reduces cognitive load (simpler catch blocks)
- Improves testability (isolated helper)
- No new allocations, locks, or complexity
- Maintains fail-fast semantics
- ASCII-compliant

**Risk Level**: LOW
- Pure refactoring (no logic changes)
- Existing tests cover fallback behavior
- Easy to revert if issues arise

---

**Status**: Ready for implementation in Iteration 6