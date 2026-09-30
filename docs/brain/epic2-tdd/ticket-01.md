# Ticket 01: SIMA Fleet Health Check Diagnostic Extraction

**Epic**: EPIC2-TDD
**Priority**: P1 (HIGH)
**Estimated Effort**: 3-4 hours
**Risk Level**: MODERATE

## Target Method

**File**: `src/V12_002.SIMA.Fleet.cs`
**Method**: `ShouldSkipFleet_RunHealthCheck`
- **Current CYC**: 29 (ACTUAL, verified via jcodemunch)
- **Target CYC**: ≤8
- **LOC**: 53 (lines 407-459)
- **Returns**: `void` (diagnostic-only, no decision path)
- **Purpose**: H-13 stale state reconciliation (diagnostic logging only)

## Problem Statement

`ShouldSkipFleet_RunHealthCheck` is a **diagnostic-only** method (CYC 29) that logs broker position vs FSM/activePositions/dispatch state for stale state reconciliation. It does NOT return a boolean decision - it only logs diagnostic output.

**Current Behavior**:
- Snapshots broker position for the account
- Checks for active FSM entries for the account
- Checks for active positions for the account
- Checks for pending dispatch operations
- Logs diagnostic output based on state combinations
- Returns `void` (no decision path)

**Why This Matters**:
- High complexity (CYC 29) for diagnostic-only code
- Multiple nested loops and conditionals
- Affects fleet health monitoring visibility
- Must be stable before touching order callbacks or dispatch logic

## Refactoring Strategy

### Extraction Plan

Split `ShouldSkipFleet_RunHealthCheck` into 5 focused diagnostic sub-methods:

#### 1. `SnapshotBrokerPosition()`
**Purpose**: Get broker position for instrument
**Responsibility**: Snapshot positions array and find matching instrument
**Estimated CYC**: 3-4
**Returns**: `Position` (or null if flat)

**Logic to Extract**:
```csharp
private Position SnapshotBrokerPosition(Account acct)
{
    Position[] _posSnapshot = acct.Positions.ToArray();
    for (int _pi = 0; _pi < _posSnapshot.Length; _pi++)
    {
        if (_posSnapshot[_pi] != null && _posSnapshot[_pi].Instrument.FullName == Instrument.FullName)
            return _posSnapshot[_pi];
    }
    return null;
}
```

#### 2. `CheckActiveFSM()`
**Purpose**: Check if account has active FSM entries
**Responsibility**: Iterate _followerBrackets for account
**Estimated CYC**: 3-4
**Returns**: `bool` (true if active FSM found)

**Logic to Extract**:
```csharp
private bool CheckActiveFSM(Account acct)
{
    foreach (var _fkvp in _followerBrackets)
    {
        var f = _fkvp.Value;
        if (f != null && f.AccountName == acct.Name
            && (f.State == FollowerBracketState.Active
                || f.State == FollowerBracketState.Accepted
                || f.State == FollowerBracketState.Submitted
                || f.State == FollowerBracketState.Replacing))
            return true;
    }
    return false;
}
```

#### 3. `CheckActivePositions()`
**Purpose**: Check if account has active positions
**Responsibility**: Iterate activePositions for account
**Estimated CYC**: 2-3
**Returns**: `bool` (true if active position found)

**Logic to Extract**:
```csharp
private bool CheckActivePositions(Account acct)
{
    foreach (var _pkvp in activePositions)
    {
        var p = _pkvp.Value;
        if (p != null && p.IsFollower && p.ExecutingAccount != null && p.ExecutingAccount.Name == acct.Name)
            return true;
    }
    return false;
}
```

#### 4. `CheckDispatchPending()`
**Purpose**: Check if dispatch operation is pending
**Responsibility**: Check _dispatchSyncPendingExpKeys for account
**Estimated CYC**: 1-2
**Returns**: `bool` (true if dispatch pending)

**Logic to Extract**:
```csharp
private bool CheckDispatchPending(Account acct)
{
    return _dispatchSyncPendingExpKeys.ContainsKey(ExpKey(acct.Name));
}
```

#### 5. `LogHealthCheckResult()`
**Purpose**: Log diagnostic output based on state
**Responsibility**: Format and append diagnostic message
**Estimated CYC**: 2-3
**Returns**: `void`

**Logic to Extract**:
```csharp
private void LogHealthCheckResult(Account acct, bool brokerFlat, bool hasActiveFsm, bool hasActivePos, bool hasDispatch, StringBuilder dispatchLog)
{
    if (brokerFlat && !hasActiveFsm && !hasActivePos && !hasDispatch)
    {
        dispatchLog.AppendLine(string.Format("[DISPATCH] H-13: {0} broker flat, no FSM/position/dispatch -- no action", acct.Name));
    }
    else if (brokerFlat && (hasActiveFsm || hasActivePos || hasDispatch))
    {
        dispatchLog.AppendLine(string.Format("[DISPATCH] H-13 SKIP: {0} Flat but {1} -- not resetting",
            acct.Name, hasActiveFsm ? "FSM active" : (hasDispatch ? "dispatch pending" : "activePos present")));
    }
}
```

#### 6. `ShouldSkipFleet_RunHealthCheck()` (Refactored)
**Purpose**: Orchestrate diagnostic checks and log results
**Responsibility**: Call sub-methods and handle exceptions
**Target CYC**: ≤8
**Returns**: `void` (unchanged)

**Refactored Logic**:
```csharp
private void ShouldSkipFleet_RunHealthCheck(Account acct, StringBuilder dispatchLog)
{
    try
    {
        Position brokerPos = SnapshotBrokerPosition(acct);
        bool brokerFlat = (brokerPos == null || brokerPos.MarketPosition == MarketPosition.Flat);
        
        bool hasActiveFsmForAcct = CheckActiveFSM(acct);
        bool hasActivePositionForAcct = CheckActivePositions(acct);
        bool hasDispatchPending = CheckDispatchPending(acct);
        
        LogHealthCheckResult(acct, brokerFlat, hasActiveFsmForAcct, hasActivePositionForAcct, hasDispatchPending, dispatchLog);
    }
    catch (Exception ex)
    {
        if (_diagFleet)
            Print("[FLEET_CATCH] ProcessFleetSlot account iteration failed: " + ex.Message);
    }
}
```

### Complexity Reduction

**Before**:
- `ShouldSkipFleet_RunHealthCheck`: CYC 29

**After**:
- `SnapshotBrokerPosition`: CYC 3-4
- `CheckActiveFSM`: CYC 3-4
- `CheckActivePositions`: CYC 2-3
- `CheckDispatchPending`: CYC 1-2
- `LogHealthCheckResult`: CYC 2-3
- `ShouldSkipFleet_RunHealthCheck` (refactored): CYC ≤8

**Total CYC**: ~11-16 (distributed across 6 methods, all ≤8)
**Reduction**: 29 → 8 (main method), 72% reduction

## Jane Street Alignment

### Pre-Refactoring Audit
- [ ] Verify zero locks in current implementation: `grep -r "lock(" src/V12_002.SIMA.Fleet.cs`
- [ ] Verify ASCII-only: `python check_ascii.py src/V12_002.SIMA.Fleet.cs`
- [ ] Document current FSM/Actor usage (if any)

### Post-Refactoring Requirements
- ✅ **Zero locks**: All sub-methods must use FSM/Actor Enqueue model (no `lock()` statements)
- ✅ **Deterministic behavior**: No `DateTime.Now` (use `DateTime.UtcNow` or injected time)
- ✅ **Explicit error handling**: No silent catches, all exceptions logged
- ✅ **Cognitive simplicity**: Each sub-method CYC ≤8, main method CYC ≤8
- ✅ **ASCII-only**: No Unicode characters in strings or comments
- ✅ **Preserve void return**: Method remains diagnostic-only (no decision path)

### Jane Street Principles Applied
1. **Make illegal states unrepresentable**: Use explicit boolean returns for state checks
2. **Single responsibility**: Each sub-method checks one aspect of state
3. **Explicit over implicit**: Each check has a named method
4. **Composability**: Sub-methods can be tested and reused independently

## Implementation Steps

### Step 1: Read Current Implementation
```powershell
# Already verified via jcodemunch
# CYC: 29, LOC: 53, Lines: 407-459
```

### Step 2: Extract Sub-Methods (TDD Order)
1. Extract `SnapshotBrokerPosition()` first (simplest, no dependencies)
2. Extract `CheckActiveFSM()` second
3. Extract `CheckActivePositions()` third
4. Extract `CheckDispatchPending()` fourth
5. Extract `LogHealthCheckResult()` fifth
6. Refactor main method last (orchestration only)

### Step 3: Write Tests (Per Sub-Method)
For each sub-method, write tests covering:
- Happy path (check succeeds)
- Failure cases (check fails with correct state)
- Edge cases (null inputs, empty collections)

**Test File**: `tests/V12_Performance.Tests/SIMA/FleetHealthTests.cs`

### Step 4: Local Verification
```powershell
# Run complexity audit
python scripts/complexity_audit.py | Select-String "ShouldSkipFleet_RunHealthCheck"

# Run full validation
powershell -File .\scripts\pre_push_validation.ps1

# Verify zero locks
grep -r "lock(" src/V12_002.SIMA.Fleet.cs
```

### Step 5: F5 Verification
1. Run `powershell -File .\deploy-sync.ps1`
2. Press F5 in NinjaTrader IDE
3. Verify BUILD_TAG banner appears
4. Test fleet operations manually (multi-account trade)
5. Verify diagnostic logs still appear correctly

## Success Criteria

- [ ] `ShouldSkipFleet_RunHealthCheck`: CYC ≤8 (verified by `complexity_audit.py`)
- [ ] `SnapshotBrokerPosition`: CYC ≤4
- [ ] `CheckActiveFSM`: CYC ≤4
- [ ] `CheckActivePositions`: CYC ≤3
- [ ] `CheckDispatchPending`: CYC ≤2
- [ ] `LogHealthCheckResult`: CYC ≤3
- [ ] All sub-methods have unit tests (minimum 2 tests each)
- [ ] Zero locks introduced (verified by `grep -r "lock(" src/`)
- [ ] ASCII-only maintained (verified by `python check_ascii.py`)
- [ ] FSM/Actor pattern preserved (no new stateful classes without Enqueue)
- [ ] All tests pass (`powershell -File .\scripts\pre_push_validation.ps1`)
- [ ] F5 verification passed (BUILD_TAG visible in NinjaTrader)
- [ ] Manual fleet operation test passed (diagnostic logs still appear)
- [ ] Method signature unchanged (still returns `void`)

## Blast Radius Analysis

**Files Affected**:
- `src/V12_002.SIMA.Fleet.cs` (primary)
- `tests/V12_Performance.Tests/SIMA/FleetHealthTests.cs` (new test file)

**Callers** (methods that call `ShouldSkipFleet_RunHealthCheck`):
- Use jcodemunch `find_references` to identify all callers
- Verify no caller logic needs adjustment after refactoring
- Callers should not be affected (method signature unchanged)

**Dependencies**:
- `_followerBrackets` (ConcurrentDictionary)
- `activePositions` (ConcurrentDictionary)
- `_dispatchSyncPendingExpKeys` (ConcurrentDictionary)
- `ExpKey()` helper method

## Rollback Plan

If F5 verification fails or fleet operations break:
1. Revert commit: `git revert HEAD`
2. Run `powershell -File .\deploy-sync.ps1`
3. Press F5 to verify rollback
4. Analyze failure in `docs/brain/epic2-tdd/ticket-01-failure-analysis.md`
5. Fix and retry

## Implementation Notes

[Engineer fills this section during execution]

### Actual CYC Scores After Refactoring
- `ShouldSkipFleet_RunHealthCheck`: [ACTUAL]
- `SnapshotBrokerPosition`: [ACTUAL]
- `CheckActiveFSM`: [ACTUAL]
- `CheckActivePositions`: [ACTUAL]
- `CheckDispatchPending`: [ACTUAL]
- `LogHealthCheckResult`: [ACTUAL]

### Unexpected Challenges
[Document any surprises or deviations from plan]

### Test Coverage
[Document test count and coverage percentage]

### F5 Verification Results
[Document BUILD_TAG and manual test results]