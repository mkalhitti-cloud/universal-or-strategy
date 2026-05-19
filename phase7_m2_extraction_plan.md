# Phase 7 MEDIUM Cluster Sub-Epic 2: Extraction Plans

**BUILD_TAG_BASELINE**: `1111.007-phase7-m1`  
**PROTOCOL**: PLAN-THEN-EXECUTE (one ticket per method)  
**CONSTRAINT**: Zero new heap allocs, ASCII-only, zero logic change

---

## M2-A: MoveStopsToBreakevenWithOffset

**File**: [`src/V12_002.Trailing.Breakeven.cs`](src/V12_002.Trailing.Breakeven.cs:43)  
**Current Metrics**: CYC 25, Nesting 7, Lines 87  
**Target Metrics**: Residual CYC ≤5, Helpers CYC ≤15

### Complexity Analysis

**Branching Structure** (25 decision points):
1. `if (activePositions.Count == 0)` - early exit guard
2. `foreach` loop over positions (implicit branch)
3. `if (!pos.EntryFilled || pos.RemainingContracts <= 0)` - position validation
4. `if (pos.Direction == MarketPosition.Long)` - direction-based stop calculation
5. `if (pos.IsFollower)` - follower fast-path branch
6. `if (isBetterF)` - follower improvement check
7. `if (lastKnownPrice <= 0)` - price staleness guard
8. `if (!priceCleared)` - ARM GUARD logic (V12.12)
9. `if (!isBetter)` - master improvement check
10. Multiple nested conditions within follower/master branches

**Hot-Path Characteristics**:
- Called per tick when breakeven is armed
- Iterates over ALL active positions
- Zero heap allocation requirement is CRITICAL
- Must preserve exact Master/Follower routing logic

### Extraction Strategy

**Approach**: Extract per-position processing into focused helper that handles single position logic.

**Helper 1: `MoveStop_SinglePosition`**
```csharp
private void MoveStop_SinglePosition(
    string entryName,
    PositionInfo pos,
    double offsetPoints,
    double lastKnownPrice)
```

**Responsibility**:
- Calculate new stop price for single position
- Handle direction-based offset application
- Execute follower fast-path logic
- Execute master ARM GUARD + improvement check
- Call `UpdateStopOrder` when conditions met
- Update `pos.ManualBreakevenTriggered` and `pos.ManualBreakevenArmed` flags

**Parameters** (zero new allocations):
- `entryName`: string (already allocated, passed by reference)
- `pos`: PositionInfo (already allocated, passed by reference)
- `offsetPoints`: double (value type, stack-allocated)
- `lastKnownPrice`: double (value type, stack-allocated)

**Extracted Logic** (reduces parent CYC by ~20):
- Lines 62-84: Follower branch (CYC ~5)
  - Direction-based stop calculation
  - `isBetterF` check
  - `UpdateStopOrder` call
  - Flag updates
- Lines 86-110: Master branch (CYC ~10)
  - Price staleness check
  - ARM GUARD logic (priceCleared calculation)
  - `isBetter` check
  - `UpdateStopOrder` call
  - Flag updates

**Residual Logic** (CYC ≤5):
- Early exit guard (activePositions.Count == 0)
- Foreach loop over positions
- Position validation filter (!EntryFilled || RemainingContracts <= 0)
- Call to `MoveStop_SinglePosition` for each valid position
- Exception handling wrapper

### Implementation Plan

**Step 1**: Create `MoveStop_SinglePosition` helper
- Extract lines 62-110 (single position processing)
- Preserve exact branching logic
- Maintain all Print statements for diagnostics
- Keep `MarkStickyDirty()` calls in place

**Step 2**: Refactor parent method
- Keep early exit guard
- Keep foreach loop structure
- Keep position validation filter
- Replace lines 62-110 with single call: `MoveStop_SinglePosition(entryName, pos, offsetPoints, lastKnownPrice);`
- Keep exception handler wrapper

**Step 3**: Verification Criteria
- [ ] Residual CYC ≤5 (verified via complexity_audit.py)
- [ ] Helper CYC ≤15 (verified via complexity_audit.py)
- [ ] Zero new heap allocations (verified via benchmark comparison)
- [ ] All Print statements preserved (verified via text search)
- [ ] Exact logic preservation (verified via diff review)
- [ ] Build succeeds with zero warnings
- [ ] `deploy-sync.ps1` completes successfully

### Risk Mitigation

**Critical Preservation Points**:
1. **Master/Follower Routing**: Lines 72-82 (follower fast-path) vs lines 86-110 (master ARM GUARD) must remain functionally identical
2. **ARM GUARD Logic**: Lines 95-103 - the `priceCleared` calculation and `ManualBreakevenArmed` flag logic is V12.12 critical behavior
3. **Flag State Management**: `pos.ManualBreakevenTriggered` and `pos.ManualBreakevenArmed` must be set at exact same points
4. **UpdateStopOrder Calls**: Must preserve exact parameters (entryName, pos, newStopPrice, 1)

**Zero-Allocation Verification**:
- All parameters are value types or existing references
- No string concatenation in hot path (all Print statements use string.Format)
- No LINQ operations
- No collection allocations

---

## M2-B: ManageTrail_RunFleetSymmetrySync

**File**: [`src/V12_002.Trailing.cs`](src/V12_002.Trailing.cs:91)  
**Current Metrics**: CYC 24, Nesting 6, Lines 59  
**Target Metrics**: Residual CYC ≤5, Helpers CYC ≤15

### Complexity Analysis

**Branching Structure** (24 decision points):
1. `foreach` loop over positions (Phase 1 - leader scan)
2. `if (ldr.IsFollower || !ldr.EntryFilled || !ldr.BracketSubmitted)` - leader filter
3. `if (ldr.Direction == MarketPosition.Long)` - direction-based max level tracking
4. `else if (ldr.Direction == MarketPosition.Short)` - short direction tracking
5. `if (leaderLongMaxLevel > 0 || leaderShortMaxLevel > 0)` - sync gate
6. `foreach` loop over positions (Phase 2 - follower sync)
7. `if (!fol.IsFollower)` - follower filter
8. `if (!fol.EntryFilled || !fol.BracketSubmitted)` - follower validation
9. `if (!activePositions.ContainsKey(entryName2))` - active position check
10. Ternary operator for `targetLevel` calculation
11. `if (targetLevel == 0)` - guard for missing leader
12. `if (fol.CurrentTrailLevel >= targetLevel)` - regression guard
13. Ternary operator for `isBetter` calculation
14. `if (isBetter)` - sync execution gate

**Fleet Symmetry Criticality**:
- This method enforces fleet-wide stop level synchronization
- ANY deviation produces wrong stop levels across accounts
- Leader max level calculation MUST be exact
- Follower sync-up logic MUST preserve "never regress" invariant

### Extraction Strategy

**Approach**: Extract Phase 1 (leader scan) and Phase 2 (follower sync) into separate focused helpers.

**Helper 1: `FleetSync_FindLeaderMaxLevels`**
```csharp
private void FleetSync_FindLeaderMaxLevels(
    KeyValuePair<string, PositionInfo>[] positionSnapshot,
    out int leaderLongMaxLevel,
    out int leaderShortMaxLevel)
```

**Responsibility**:
- Scan all positions for leader entries
- Track highest trail level per direction
- Return max levels via out parameters (zero heap allocation)

**Parameters** (zero new allocations):
- `positionSnapshot`: KeyValuePair[] (already allocated, passed by reference)
- `leaderLongMaxLevel`: out int (value type, stack-allocated)
- `leaderShortMaxLevel`: out int (value type, stack-allocated)

**Extracted Logic** (reduces parent CYC by ~8):
- Lines 93-107: Leader scan loop
  - IsFollower filter
  - EntryFilled/BracketSubmitted validation
  - Direction-based max level tracking

**Helper 2: `FleetSync_SyncFollowersToLevel`**
```csharp
private void FleetSync_SyncFollowersToLevel(
    KeyValuePair<string, PositionInfo>[] positionSnapshot,
    int leaderLongMaxLevel,
    int leaderShortMaxLevel)
```

**Responsibility**:
- Iterate over followers
- Calculate target level per direction
- Execute sync-up logic (never regress)
- Call `UpdateStopOrder` when improvement detected

**Parameters** (zero new allocations):
- `positionSnapshot`: KeyValuePair[] (already allocated, passed by reference)
- `leaderLongMaxLevel`: int (value type, stack-allocated)
- `leaderShortMaxLevel`: int (value type, stack-allocated)

**Extracted Logic** (reduces parent CYC by ~14):
- Lines 113-147: Follower sync loop
  - Follower filter
  - EntryFilled/BracketSubmitted validation
  - activePositions containment check
  - Target level calculation (direction-based)
  - Zero-leader guard
  - Regression guard
  - `CalculateStopForLevel` call
  - `isBetter` check
  - `UpdateStopOrder` call

**Residual Logic** (CYC ≤5):
- Call `FleetSync_FindLeaderMaxLevels` (out params)
- Diagnostic Print statement (lines 109-110)
- Gate check: `if (leaderLongMaxLevel > 0 || leaderShortMaxLevel > 0)`
- Call `FleetSync_SyncFollowersToLevel`

### Implementation Plan

**Step 1**: Create `FleetSync_FindLeaderMaxLevels` helper
- Extract lines 93-107 (leader scan)
- Initialize out params to 0
- Preserve exact filter logic (IsFollower, EntryFilled, BracketSubmitted)
- Preserve direction-based Math.Max logic

**Step 2**: Create `FleetSync_SyncFollowersToLevel` helper
- Extract lines 113-147 (follower sync)
- Preserve all validation filters
- Preserve exact target level calculation (ternary operator)
- Preserve zero-leader guard (line 121)
- Preserve regression guard (line 124)
- Preserve `CalculateStopForLevel` call
- Preserve `isBetter` calculation (ternary operator)
- Preserve `UpdateStopOrder` call with exact parameters
- Preserve Print statement format

**Step 3**: Refactor parent method
- Call `FleetSync_FindLeaderMaxLevels(positionSnapshot, out int leaderLongMaxLevel, out int leaderShortMaxLevel);`
- Keep diagnostic Print (lines 109-110)
- Keep gate check (line 112)
- Call `FleetSync_SyncFollowersToLevel(positionSnapshot, leaderLongMaxLevel, leaderShortMaxLevel);`

**Step 4**: Verification Criteria
- [ ] Residual CYC ≤5 (verified via complexity_audit.py)
- [ ] Helper 1 CYC ≤8 (verified via complexity_audit.py)
- [ ] Helper 2 CYC ≤15 (verified via complexity_audit.py)
- [ ] Zero new heap allocations (verified via benchmark comparison)
- [ ] Fleet symmetry logic preserved (verified via diff review)
- [ ] All Print statements preserved (verified via text search)
- [ ] Build succeeds with zero warnings
- [ ] `deploy-sync.ps1` completes successfully

### Risk Mitigation

**Critical Preservation Points**:
1. **Leader Max Level Calculation**: Lines 103-105 - Math.Max logic per direction must be exact
2. **Target Level Selection**: Line 126 - ternary operator must preserve direction-based routing
3. **Zero-Leader Guard**: Line 121 - `if (targetLevel == 0) continue;` prevents sync when no leader exists
4. **Regression Guard**: Line 124 - `if (fol.CurrentTrailLevel >= targetLevel) continue;` enforces "never regress" invariant
5. **isBetter Calculation**: Lines 129-131 - ternary operator for direction-based improvement check

**Fleet Symmetry Invariants**:
- Leaders drive followers (never reverse)
- Followers only sync UP (never down)
- Direction-specific max levels (Long vs Short independent)
- Zero-leader case handled gracefully (skip sync)

---

## M2-C: UpdateExistingPendingReplacement

**File**: [`src/V12_002.Trailing.StopUpdate.cs`](src/V12_002.Trailing.StopUpdate.cs:132)  
**Current Metrics**: CYC 24, Nesting 6, Lines 63  
**Target Metrics**: Residual CYC ≤5, Helpers CYC ≤15

### Complexity Analysis

**Branching Structure** (24 decision points):
1. `for` loop over targets 1-5 (Build 955 snapshot - Phase 1)
2. `if (_tDA != null && _tDA.TryGetValue(...) && _tOA != null && (...))` - target validation (5 iterations)
3. `if (pendingStopReplacements.TryAdd(entryName, newPending))` - new pending branch
4. `if (currentCount >= CIRCUIT_BREAKER_THRESHOLD && !circuitBreakerActive)` - circuit breaker activation
5. `else if (pendingStopReplacements.TryGetValue(entryName, out var pending))` - existing pending branch
6. `if (!pending.BracketRestorationNeeded)` - Build 950 refresh gate
7. `for` loop over targets 1-5 (Build 950 refresh - Phase 2)
8. `if (_tD2 != null && _tD2.TryGetValue(...) && _tO2 != null && (...))` - target validation (5 iterations)

**FSM Pattern Criticality**:
- This method is part of the Move-Sync/Follower Order Replace Pattern
- Two-phase FSM: PendingCancel → Submitting → SubmitFollowerReplacement
- `_followerReplaceSpecs` dict tracks FSM state
- Target snapshot capture (Build 955) MUST happen BEFORE TryAdd
- Bracket restoration logic (Build 950) MUST preserve OCO cascade detection

### Extraction Strategy

**Approach**: Extract target snapshot logic into focused helpers that handle the two snapshot phases.

**Helper 1: `CaptureTargetSnapshot`**
```csharp
private TargetSnapshot[] CaptureTargetSnapshot(string entryName)
```

**Responsibility**:
- Iterate over targets 1-5
- Validate target order state (Working or Accepted)
- Build TargetSnapshot array
- Return null if no targets found (zero allocation for empty case)

**Parameters** (minimal allocations):
- `entryName`: string (already allocated, passed by reference)
- **Return**: TargetSnapshot[] (allocated only when targets exist - unavoidable for snapshot pattern)

**Extracted Logic** (reduces parent CYC by ~10):
- Lines 134-143: Build 955 target snapshot loop
  - GetTargetOrdersDictionary call
  - TryGetValue validation
  - OrderState check (Working || Accepted)
  - TargetSnapshot construction
  - List.Add operation

**Helper 2: `RefreshTargetSnapshot`**
```csharp
private TargetSnapshot[] RefreshTargetSnapshot(string entryName)
```

**Responsibility**:
- Iterate over targets 1-5 (Build 950 refresh)
- Validate target order state (Working or Accepted)
- Build refreshed TargetSnapshot array
- Return null if no targets found

**Parameters** (minimal allocations):
- `entryName`: string (already allocated, passed by reference)
- **Return**: TargetSnapshot[] (allocated only when targets exist)

**Extracted Logic** (reduces parent CYC by ~10):
- Lines 167-176: Build 950 refresh loop
  - GetTargetOrdersDictionary call
  - TryGetValue validation
  - OrderState check (Working || Accepted)
  - TargetSnapshot construction
  - List.Add operation

**Residual Logic** (CYC ≤5):
- Call `CaptureTargetSnapshot(entryName)` → `_b955TargetsA`
- Create `PendingStopReplacement` struct (lines 145-154)
- `if (pendingStopReplacements.TryAdd(...))` branch
  - Circuit breaker logic (lines 158-164)
- `else if (pendingStopReplacements.TryGetValue(...))` branch
  - Update `pending.StopPrice`
  - `if (!pending.BracketRestorationNeeded)` gate
    - Call `RefreshTargetSnapshot(entryName)` → refresh array
    - Update `pending.CapturedTargets` and `pending.BracketRestorationNeeded`
- Update `pos.CurrentStopPrice` and `pos.CurrentTrailLevel`
- Call `MarkStickyDirty()`
- Print diagnostic

### Implementation Plan

**Step 1**: Create `CaptureTargetSnapshot` helper
- Extract lines 134-143 (Build 955 snapshot)
- Create local `List<TargetSnapshot>` (unavoidable allocation)
- Iterate targets 1-5
- Call `GetTargetOrdersDictionary(_tA)`
- Validate: `_tDA != null && _tDA.TryGetValue(entryName, out _tOA) && _tOA != null && (_tOA.OrderState == OrderState.Working || _tOA.OrderState == OrderState.Accepted)`
- Add to list: `new TargetSnapshot { TargetNum = _tA, Price = _tOA.LimitPrice, Qty = _tOA.Quantity, CapturedOrder = _tOA }`
- Return: `_list.Count > 0 ? _list.ToArray() : null`

**Step 2**: Create `RefreshTargetSnapshot` helper
- Extract lines 167-176 (Build 950 refresh)
- Identical logic to Helper 1 (different variable names)
- Return: `_list.Count > 0 ? _list.ToArray() : null`

**Step 3**: Refactor parent method
- Replace lines 134-143 with: `var _b955TargetsA = CaptureTargetSnapshot(entryName);`
- Update line 153: `CapturedTargets = _b955TargetsA`
- Update line 154: `BracketRestorationNeeded = _b955TargetsA != null && _b955TargetsA.Length > 0`
- Replace lines 167-176 with: `var _b950Refresh = RefreshTargetSnapshot(entryName);`
- Update line 177: `pending.CapturedTargets = _b950Refresh;`
- Update line 178: `pending.BracketRestorationNeeded = _b950Refresh != null && _b950Refresh.Length > 0;`

**Step 4**: Verification Criteria
- [ ] Residual CYC ≤5 (verified via complexity_audit.py)
- [ ] Helper 1 CYC ≤10 (verified via complexity_audit.py)
- [ ] Helper 2 CYC ≤10 (verified via complexity_audit.py)
- [ ] Minimal heap allocations (TargetSnapshot[] only when targets exist)
- [ ] FSM pattern preserved (verified via diff review)
- [ ] Build 955/950 snapshot logic preserved (verified via diff review)
- [ ] Circuit breaker logic preserved (verified via diff review)
- [ ] Build succeeds with zero warnings
- [ ] `deploy-sync.ps1` completes successfully

### Risk Mitigation

**Critical Preservation Points**:
1. **Snapshot Timing**: Lines 134-143 MUST execute BEFORE `TryAdd` (line 156) - Build 955 requirement
2. **Target Validation**: `_tDA != null && _tDA.TryGetValue(...) && _tOA != null && (OrderState check)` - exact logic required
3. **Circuit Breaker**: Lines 158-164 - threshold check and activation logic must be exact
4. **Refresh Gate**: Line 166 - `if (!pending.BracketRestorationNeeded)` guards Build 950 refresh
5. **FSM State Updates**: Lines 179-180 - `pos.CurrentStopPrice` and `pos.CurrentTrailLevel` must be set after pending update

**FSM Invariants**:
- Snapshot captured BEFORE TryAdd (prevents race condition)
- Circuit breaker activates at threshold (prevents runaway pending queue)
- Refresh only when BracketRestorationNeeded is false (prevents duplicate work)
- MarkStickyDirty called after all state updates (ensures persistence)

**Allocation Analysis**:
- `List<TargetSnapshot>` allocation: unavoidable (snapshot pattern requires collection)
- `TargetSnapshot[]` allocation: only when targets exist (null for empty case)
- `PendingStopReplacement` struct: stack-allocated (value type)
- No string allocations (all strings passed by reference)

---

## Cross-Cutting Verification Protocol

### Pre-Extraction Checklist
- [ ] Confirm BUILD_TAG_BASELINE: `1111.007-phase7-m1`
- [ ] Run `complexity_audit.py` to establish baseline metrics
- [ ] Run `powershell -File .\scripts\build_readiness.ps1` to confirm clean build
- [ ] Verify zero `lock(` statements in target files: `grep -r "lock(" src/V12_002.Trailing*.cs`
- [ ] Document current benchmark baseline (if available)

### Post-Extraction Checklist (Per Method)
- [ ] Run `complexity_audit.py` to verify CYC reduction
- [ ] Run `powershell -File .\scripts\lint.ps1` to verify zero new warnings
- [ ] Run `powershell -File .\deploy-sync.ps1` to sync NinjaTrader hard links
- [ ] Run `powershell -File .\scripts\build_readiness.ps1` to verify clean build
- [ ] Verify zero new heap allocations (benchmark comparison if available)
- [ ] Verify ASCII-only compliance: `python check_ascii.py src/V12_002.Trailing*.cs`
- [ ] Verify zero logic change (diff review against baseline)
- [ ] Verify all Print statements preserved (text search)
- [ ] Verify FSM patterns preserved (manual review)

### Integration Testing
- [ ] F5 in NinjaTrader (manual smoke test)
- [ ] Verify BUILD_TAG incremented correctly
- [ ] Run stress test: `powershell -File .\scripts\test_stress.ps1` (if available)
- [ ] Verify no regression in fleet symmetry behavior (manual observation)
- [ ] Verify breakeven ARM GUARD behavior preserved (manual observation)
- [ ] Verify stop replacement FSM behavior preserved (manual observation)

---

## Execution Sequence

**Recommended Order** (lowest risk to highest risk):

1. **M2-B: ManageTrail_RunFleetSymmetrySync** (CYC 24)
   - Cleanest extraction (two independent phases)
   - Zero allocation requirement easiest to verify
   - Fleet symmetry logic well-isolated

2. **M2-A: MoveStopsToBreakevenWithOffset** (CYC 25)
   - Single helper extraction (simpler than M2-C)
   - Hot-path method (requires careful benchmark verification)
   - ARM GUARD logic requires careful preservation

3. **M2-C: UpdateExistingPendingReplacement** (CYC 24)
   - Most complex (two helpers + FSM pattern)
   - Unavoidable allocations (snapshot arrays)
   - Circuit breaker logic requires careful preservation

**Per-Method Protocol**:
1. Create ticket in tracking system
2. Create feature branch: `phase7-m2-{A|B|C}-extract`
3. Execute extraction plan
4. Run post-extraction checklist
5. Create PR with forensic diff review
6. Merge after approval
7. Increment BUILD_TAG
8. Run integration testing
9. Mark ticket complete

---

## V12 DNA Compliance Matrix

| Constraint | M2-A | M2-B | M2-C | Notes |
|------------|------|------|------|-------|
| Zero new heap allocs | ✓ | ✓ | ⚠️ | M2-C: TargetSnapshot[] allocation unavoidable (snapshot pattern) |
| ASCII-only | ✓ | ✓ | ✓ | All string literals verified |
| Zero logic change | ✓ | ✓ | ✓ | Surgical extraction only |
| Lock-free | ✓ | ✓ | ✓ | No lock statements in any target file |
| Actor pattern | ✓ | ✓ | ✓ | FSM patterns preserved |
| Correctness by construction | ✓ | ✓ | ✓ | Type safety maintained |

**Legend**:
- ✓ = Full compliance
- ⚠️ = Partial compliance (documented exception)
- ✗ = Non-compliance (blocker)

---

## Appendix: Helper Method Signatures Summary

### M2-A Helpers
```csharp
// Helper 1: Single position breakeven logic
private void MoveStop_SinglePosition(
    string entryName,
    PositionInfo pos,
    double offsetPoints,
    double lastKnownPrice)
```

### M2-B Helpers
```csharp
// Helper 1: Find leader max trail levels
private void FleetSync_FindLeaderMaxLevels(
    KeyValuePair<string, PositionInfo>[] positionSnapshot,
    out int leaderLongMaxLevel,
    out int leaderShortMaxLevel)

// Helper 2: Sync followers to leader levels
private void FleetSync_SyncFollowersToLevel(
    KeyValuePair<string, PositionInfo>[] positionSnapshot,
    int leaderLongMaxLevel,
    int leaderShortMaxLevel)
```

### M2-C Helpers
```csharp
// Helper 1: Capture target snapshot (Build 955)
private TargetSnapshot[] CaptureTargetSnapshot(string entryName)

// Helper 2: Refresh target snapshot (Build 950)
private TargetSnapshot[] RefreshTargetSnapshot(string entryName)
```

---

**END OF EXTRACTION PLAN**

**Next Steps**: 
1. Review this plan with Director
2. Create tickets for M2-A, M2-B, M2-C
3. Execute in recommended order (M2-B → M2-A → M2-C)
4. Switch to Code mode for implementation after approval