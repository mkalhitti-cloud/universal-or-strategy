# EPIC-CCN-12: Code Style Cleanup

**Created**: 2026-05-30  
**Priority**: P2 (Low - cosmetic issues)  
**Estimated Effort**: 30 minutes  
**Status**: Backlog

## Objective

Fix 14 MINOR/HIGH code style issues identified by bots during PR #5 but deferred to maintain focus on critical P0/P1 fixes.

## Issues to Fix

### 1. Missing Curly Braces (2 instances)

**File**: `src/V12_002.SIMA.Flatten.cs`

**Line 337**:
```csharp
// BEFORE
if (drainedOps.Count == 0) return;

// AFTER
if (drainedOps.Count == 0)
{
    return;
}
```

**Line 355**:
```csharp
// BEFORE
if (!workItem.CancelOnly)
    ProcessFlattenWorkItem_ClosePositions(workItem, acct);

// AFTER
if (!workItem.CancelOnly)
{
    ProcessFlattenWorkItem_ClosePositions(workItem, acct);
}
```

### 2. Unnecessary ToString() (10 instances)

**Files**: `src/V12_002.Orders.Management.Flatten.cs`, `src/V12_002.SIMA.Flatten.cs`, `src/V12_002.Orders.Management.StopSync.cs`

**Pattern**:
```csharp
// BEFORE
Print("CRITICAL: Unexpected exception: " + ex.ToString());

// AFTER
Print("CRITICAL: Unexpected exception: " + ex);
```

**Locations**:
- `Orders.Management.Flatten.cs:225` - CancelMasterEntryOrders
- `Orders.Management.Flatten.cs:241` - DispatchFleetFlatten
- `Orders.Management.Flatten.cs:252` - ResetSyncStateAndPurgeFollowers
- `Orders.Management.Flatten.cs:262` - FlattenFilledMasterPositions
- `Orders.Management.Flatten.cs:272` - CancelUnfilledMasterEntries
- `SIMA.Flatten.cs:394` - ChainNextFlattenOp
- `SIMA.Flatten.cs:579` - ClosePositionsOnlyApexAccounts
- `Orders.Management.StopSync.cs:511` - UpdateStopQuantity
- `Orders.Management.StopSync.cs:667` - CreateNewStopOrder
- `Orders.Management.StopSync.cs:681` - Emergency flatten fallback

### 3. Generic Exception Catches (2 instances)

**File**: `src/V12_002.Orders.Management.StopSync.cs`

**Line 462** (bare catch):
```csharp
// BEFORE
catch
{
    // Emergency flatten fallback
}

// AFTER
catch (Exception ex) when (ex is InvalidOperationException || ex is NullReferenceException)
{
    // Emergency flatten fallback
}
```

**Line 482** (generic catch):
```csharp
// BEFORE
catch (Exception flatEx)
{
    Print("[FLATTEN] CRITICAL: Fallback flatten failed: " + flatEx);
}

// AFTER
catch (InvalidOperationException flatEx)
{
    Print("[FLATTEN] CRITICAL: Fallback flatten failed (InvalidOperation): " + flatEx);
}
catch (NullReferenceException flatEx)
{
    Print("[FLATTEN] CRITICAL: Fallback flatten failed (NullReference): " + flatEx);
}
catch (Exception flatEx)
{
    Print("[FLATTEN] CRITICAL: Fallback flatten failed (Unexpected): " + flatEx);
}
```

## Acceptance Criteria

- [ ] All 2 missing curly braces added
- [ ] All 10 unnecessary ToString() calls removed
- [ ] All 2 generic exception catches replaced with specific exception filters
- [ ] Pre-push validation passes (10/10 checks)
- [ ] F5 verification in NinjaTrader succeeds
- [ ] No new bot findings introduced
- [ ] PHS maintained at 100/100

## Implementation Notes

**Branch Strategy**: Create `fix/epic-ccn-12-style-cleanup` from main

**Testing**:
1. Run `powershell -File .\scripts\pre_push_validation.ps1`
2. Run `powershell -File .\deploy-sync.ps1`
3. Press F5 in NinjaTrader
4. Verify BUILD tag appears
5. Verify Logic Audit passes

**PR Requirements**:
- Title: "style: fix 14 code style issues (EPIC-CCN-12)"
- Description: Reference this EPIC document
- Files: 3 .cs files only (Three-Tier Branch Model)
- Target PHS: 100/100

## Related Work

- **PR #5**: Fixed critical P0/P1 issues, deferred style cleanup
- **EPIC-CCN-10**: Complexity reduction (separate effort)
- **EPIC-CCN-11**: StopSync.cs switch duplication (separate effort)

## Jane Street Alignment

While these are style issues, fixing them aligns with Jane Street principles:
- **Curly braces**: Reduces cognitive load (explicit structure)
- **Remove ToString()**: Simplicity (compiler handles it)
- **Exception filters**: Fail-fast (catch only expected exceptions)