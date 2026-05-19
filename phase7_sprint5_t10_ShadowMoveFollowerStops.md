# [Phase7-S5-T10] ShadowMoveFollowerStops Extraction Plan

**BUILD_TAG**: `1111.007-phase7-t10`  
**Target**: `ShadowMoveFollowerStops` in `src/V12_002.SIMA.Shadow.cs`  
**Current Metrics**: CYC=23, LOC=74, Nesting=4, Params=2  
**Goal**: Residual CYC ≤19, Sub-helpers CYC ≤19

---

## 1. Forensic Analysis

### Current Structure (Lines 76-149)
```
ShadowMoveFollowerStops(leaderEntryKey, newStopPrice) -> bool
├─ [CYC+4] Validate dispatch context (lines 78-86)
│  └─ Early return if invalid leader/dispatch
├─ [CYC+8] Build follower entry list (lines 88-111)
│  ├─ Snapshot followers from context
│  ├─ Filter by dispatch ID match
│  └─ Scan symmetryFleetEntryToDispatch for additional entries
└─ [CYC+11] Process follower stop updates (lines 113-148)
   ├─ Iterate follower entries
   ├─ Validate FSM and position state
   ├─ Skip if stop already at target price
   └─ Call UpdateStopOrder + Print log

Total: 1 (base) + 4 + 8 + 11 = 24 decision points
```

### Verbatim Print/AppendLine Inventory
- Line 143-144: `Print(string.Format("[SHADOW] Propagating stop {0:F2} -> {1} on {2}", newStopPrice, followerEntryName, fsm.AccountName));`

**Count**: 1 Print statement

### Single Caller
- `ShadowPropagateStopMoves` at line 50: `if (ShadowMoveFollowerStops(kvp.Key, leaderStop.StopPrice))`
- Signature is **FREE** per D-D3 (single caller, returns bool)

---

## 2. Extraction Strategy

### Sub-Helper 1: `ShadowValidateDispatchContext`
**Purpose**: Validate leader entry and retrieve dispatch context  
**Signature**: `private bool ShadowValidateDispatchContext(string leaderEntryKey, out SymmetryDispatchContext ctx)`  
**Lines**: 78-86 (9 LOC)  
**CYC**: 4 (null checks + TryGetValue branches)  
**Returns**: `true` if valid context found, `false` otherwise

**DEVIATION-T10-A**: 9 LOC < 15 LOC threshold. Justified because:
- Cohesive validation block
- Clear single responsibility
- Reduces nesting in parent

### Sub-Helper 2: `ShadowBuildFollowerEntryList`
**Purpose**: Build complete list of follower entries linked to dispatch  
**Signature**: `private System.Collections.Generic.List<string> ShadowBuildFollowerEntryList(SymmetryDispatchContext ctx, string dispatchId)`  
**Lines**: 88-111 (24 LOC)  
**CYC**: 8 (snapshot loop + filter loop + scan loop)  
**Returns**: List of follower entry names

### Sub-Helper 3: `ShadowProcessFollowerStopUpdate`
**Purpose**: Process stop update for a single follower entry  
**Signature**: `private bool ShadowProcessFollowerStopUpdate(string followerEntryName, double newStopPrice, out bool waitingOnFollower)`  
**Lines**: 115-146 (32 LOC)  
**CYC**: 11 (FSM checks + position checks + price check)  
**Returns**: `true` if follower found, sets `waitingOnFollower` flag

### Residual Dispatcher
**Purpose**: Orchestrate validation → build list → process updates  
**CYC**: 1 (base) + 2 (validation check + loop) = 3  
**LOC**: ~15 lines

---

## 3. Implementation Plan

### Phase 1: Extract Sub-Helper 1 (Validation)
```csharp
/// <summary>
/// Validates leader entry key and retrieves associated dispatch context.
/// </summary>
private bool ShadowValidateDispatchContext(string leaderEntryKey, out SymmetryDispatchContext ctx)
{
    ctx = null;
    string dispatchId;
    if (string.IsNullOrEmpty(leaderEntryKey)
        || !symmetryMasterEntryToDispatch.TryGetValue(leaderEntryKey, out dispatchId)
        || !symmetryDispatchById.TryGetValue(dispatchId, out ctx)
        || ctx == null)
    {
        return false;
    }
    return true;
}
```

### Phase 2: Extract Sub-Helper 2 (Build List)
```csharp
/// <summary>
/// Builds complete list of follower entries linked to the dispatch context.
/// ADR-019: Uses Volatile.Read snapshot for lock-free access.
/// </summary>
private System.Collections.Generic.List<string> ShadowBuildFollowerEntryList(
    SymmetryDispatchContext ctx, string dispatchId)
{
    // ADR-019: snapshot via Volatile.Read on immutable string[] -- zero-alloc, lock-free.
    string[] followerSnapshot = ctx.Followers;
    var followerEntryNames = new System.Collections.Generic.List<string>(followerSnapshot.Length);
    
    foreach (string followerEntryName in followerSnapshot)
    {
        if (string.IsNullOrEmpty(followerEntryName))
            continue;
        if (!symmetryFleetEntryToDispatch.TryGetValue(followerEntryName, out var linkedDispatch))
            continue;
        if (!string.Equals(linkedDispatch, dispatchId, StringComparison.Ordinal))
            continue;
        followerEntryNames.Add(followerEntryName);
    }

    foreach (var kvp in symmetryFleetEntryToDispatch.ToArray())
    {
        if (!string.Equals(kvp.Value, dispatchId, StringComparison.Ordinal))
            continue;
        if (followerEntryNames.Contains(kvp.Key))
        {
            continue;
        }
        followerEntryNames.Add(kvp.Key);
    }
    
    return followerEntryNames;
}
```

### Phase 3: Extract Sub-Helper 3 (Process Update)
```csharp
/// <summary>
/// Processes stop update for a single follower entry.
/// Returns true if follower found, sets waitingOnFollower if not ready.
/// </summary>
private bool ShadowProcessFollowerStopUpdate(
    string followerEntryName, double newStopPrice, out bool waitingOnFollower)
{
    waitingOnFollower = false;
    
    FollowerBracketFSM fsm;
    bool hasFsm = _followerBrackets.TryGetValue(followerEntryName, out fsm) && fsm != null;
    PositionInfo followerPos;
    bool hasFollowerPos = activePositions.TryGetValue(followerEntryName, out followerPos) && followerPos != null;

    if (!hasFsm && !hasFollowerPos)
        return false;

    if (!hasFollowerPos || !followerPos.EntryFilled || !followerPos.BracketSubmitted)
    {
        waitingOnFollower = true;
        return true;
    }

    if (!hasFsm || fsm.State != FollowerBracketState.Active || fsm.StopOrder == null)
    {
        waitingOnFollower = true;
        return true;
    }

    // Skip if follower stop is already at the target price
    if (Math.Abs(fsm.StopOrder.StopPrice - newStopPrice) < tickSize * 0.5)
        return true;

    // Use existing stop update infrastructure (two-phase Replace FSM)
    Print(string.Format("[SHADOW] Propagating stop {0:F2} -> {1} on {2}",
        newStopPrice, followerEntryName, fsm.AccountName));
    UpdateStopOrder(followerEntryName, followerPos, newStopPrice, followerPos.CurrentTrailLevel);
    
    return true;
}
```

### Phase 4: Refactor Residual Dispatcher
```csharp
/// <summary>
/// Propagates a leader stop price to all followers tracking the same master entry.
/// Uses symmetry dispatch context to find the followers linked to this leader entry.
/// </summary>
private bool ShadowMoveFollowerStops(string leaderEntryKey, double newStopPrice)
{
    SymmetryDispatchContext ctx;
    if (!ShadowValidateDispatchContext(leaderEntryKey, out ctx))
        return false;

    string dispatchId;
    symmetryMasterEntryToDispatch.TryGetValue(leaderEntryKey, out dispatchId);
    
    var followerEntryNames = ShadowBuildFollowerEntryList(ctx, dispatchId);

    bool foundAnyFollower = false;
    bool waitingOnFollower = false;
    foreach (string followerEntryName in followerEntryNames)
    {
        bool waitingOnThis;
        if (ShadowProcessFollowerStopUpdate(followerEntryName, newStopPrice, out waitingOnThis))
        {
            foundAnyFollower = true;
            if (waitingOnThis)
                waitingOnFollower = true;
        }
    }

    return foundAnyFollower && !waitingOnFollower;
}
```

**Residual CYC**: 1 (base) + 1 (validation check) + 1 (loop) = 3 ✓

---

## 4. Invariant Verification

### INV-1.1: Lock-Free Atomic
- ✓ No `lock()` statements
- ✓ Uses ADR-019 Volatile.Read snapshot pattern
- ✓ All dictionary access via TryGetValue

### INV-1.2: ASCII-Only
- ✓ All string literals are ASCII
- ✓ No Unicode characters

### INV-1.3: Signature Stability
- ✓ Single caller at line 50
- ✓ Signature is FREE per D-D3
- ✓ Return type `bool` preserved

### INV-1.4: Verbatim Print Preservation
- ✓ 1 Print statement preserved in `ShadowProcessFollowerStopUpdate`
- ✓ Format string unchanged: `"[SHADOW] Propagating stop {0:F2} -> {1} on {2}"`

### INV-1.5: Zero Behavior Change
- ✓ All logic paths preserved
- ✓ Early returns maintained
- ✓ Loop iteration order unchanged
- ✓ UpdateStopOrder call preserved

---

## 5. Acceptance Criteria

### AC-1: Complexity Reduction
- [ ] Residual `ShadowMoveFollowerStops` CYC ≤19 (target: 3)
- [ ] `ShadowValidateDispatchContext` CYC ≤19 (target: 4)
- [ ] `ShadowBuildFollowerEntryList` CYC ≤19 (target: 8)
- [ ] `ShadowProcessFollowerStopUpdate` CYC ≤19 (target: 11)

### AC-2: Hotspot Removal
- [ ] `ShadowMoveFollowerStops` no longer in `CYC > 20` list

### AC-3: Caller Compatibility
- [ ] `ShadowPropagateStopMoves` line 50 compiles unchanged

### AC-4: Verbatim Print Preservation
- [ ] 1 Print statement preserved
- [ ] Format string unchanged

### AC-5: Build Verification
- [ ] BUILD_TAG = `1111.007-phase7-t10`
- [ ] `powershell -File .\scripts\build_readiness.ps1` passes
- [ ] Zero compiler errors

### AC-6: F5 Acceptance
**Test Scenario**: Trigger master-account stop move (manual stop drag in chart)  
**Expected**: All follower accounts' stops shadow-update to same price  
**Verification**: Check Output for shadow-propagation log lines, zero ERROR lines

---

## 6. Verification Steps

1. **Pre-Extract Baseline**
   ```powershell
   # Count Print statements
   Select-String -Path src/V12_002.SIMA.Shadow.cs -Pattern "Print\(" | Measure-Object
   # Expected: 1 match
   ```

2. **Extract Sub-Helpers**
   - Insert `ShadowValidateDispatchContext` after line 75
   - Insert `ShadowBuildFollowerEntryList` after validation helper
   - Insert `ShadowProcessFollowerStopUpdate` after build helper

3. **Refactor Residual**
   - Replace lines 76-149 with new dispatcher implementation

4. **Post-Extract Verification**
   ```powershell
   # Verify Print count unchanged
   Select-String -Path src/V12_002.SIMA.Shadow.cs -Pattern "Print\(" | Measure-Object
   # Expected: 1 match
   
   # Build verification
   powershell -File .\scripts\build_readiness.ps1
   
   # Deploy sync
   powershell -File .\deploy-sync.ps1
   ```

5. **F5 Manual Test**
   - Load strategy in NinjaTrader
   - Enter master position with stop
   - Manually drag stop to new price
   - Verify follower stops update in Output window
   - Check for `[SHADOW] Propagating stop` log lines

---

## 7. Risk Assessment

### Low Risk
- Single caller with FREE signature
- Pure refactor, zero logic change
- Well-defined extraction boundaries

### Mitigations
- DEVIATION-T10-A pre-flagged for 9 LOC helper
- Verbatim Print preservation verified
- Caller signature unchanged

---

## 8. DEVIATION-T10-A Documentation

**Issue**: `ShadowValidateDispatchContext` is 9 LOC, below 15 LOC threshold  
**Justification**:
- Cohesive validation block with single responsibility
- Reduces nesting depth in parent from 4 to 2
- Clear extraction boundary (lines 78-86)
- Improves readability and testability

**Approval**: Pre-flagged per D-S5 (LOC deviation for short targets)

---

**Status**: READY FOR IMPLEMENTATION  
**Next**: Execute extraction in sequence: Helper1 → Helper2 → Helper3 → Residual