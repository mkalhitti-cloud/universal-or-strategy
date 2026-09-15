# Architecture Plan -- Block B157
# ptt-architect Phase 1 output
# Produced: 2026-08-10
# Status: REVIEW_PASS

---

## LANE-SPLIT GATE RESULT (mandatory header)

**Gate answer:** SINGLE LANE -- 3 tickets.

| Question | Answer |
|---|---|
| 1. Do any changes touch more than one source file? | YES -- CopyEngine.cs and CopyEngineTests.cs |
| 2. Can all changes be done in one ticket (one PR)? | YES -- single engineer, single PR |
| 3. Are there dependency ordering constraints? | T1 (code fix + tests must travel together). T2 and T3 are fully independent. |
| 4. One lane or multiple parallel lanes? | ONE LANE |

**Justification:** All 6 defect cards target the same two files (CopyEngine.cs and
CopyEngineTests.cs) within the same PTT CopyEngine component. No parallel work streams
exist. Three tickets group changes by priority and file scope, minimizing PR complexity.

---

## Section 0: Deferred Items Closed by B157

| ID | Prior Status | B157 Disposition |
|---|---|---|
| DW-B157-RG-01 | New defect | CLOSED by B157-T1 |
| DW-B156-DEFER-1 | OPEN (B156) | CLOSED by B157-T2 |
| DW-B156-DEFER-2 | OPEN (B156) | CLOSED by B157-T2 |
| DW-B156-DEFER-3 | OPEN (B156) | CLOSED by B157-T3 |
| DW-B153-05 | OPEN (B153/155/156) | CLOSED by B157-T2 |
| DW-B153-06 | OPEN (B153/155/156) | CLOSED by B157-T2 |

Items NOT addressed (carry forward to B158+):

| ID | Item | Priority |
|---|---|---|
| DW-B154-05 | IsPttCopyEntryFilled delegation refactor | P3 |
| DW-B154-06 | TryTriggerBeRecovery post-deployment monitor | P2 |
| DW-B155-01 | B155 SIM gate pending | P2 |
| DW-BUG-0911-A-01 | Mid-close race condition in OnOrderUpdate | P2 |

---

## Section 1: DW-B157-RG-01 (P0) -- IsReversalToFlatFollower Logic Inversion

### 1.1 Root Cause

`IsReversalToFlatFollower` returns `true` (skip=yes) when:
  `currentAction != lastAction && followerIsFlat`
This fires when the follower is FLAT and direction changed -- exactly the safe-to-copy case.
A flat follower has no live brackets; any direction is safe.

### 1.2 Exact Change

File: `src/PropTraderTools/CopyEngine.cs`
Line: 6527

Before:
```
return currentAction != lastAction && followerIsFlat;
```
After:
```
return currentAction != lastAction && !followerIsFlat;
```

Change size: 1 character insertion (`!`). No other changes to this method.

### 1.3 Method Signature (unchanged)

```csharp
// File: src/PropTraderTools/CopyEngine.cs ~line 6521
internal static bool IsReversalToFlatFollower(
    OrderAction currentAction,
    OrderAction lastAction,
    bool followerIsFlat
)
```

Access: `internal static` -- accessible via InternalsVisibleTo for tests.
No NT8 runtime required for direct test calls.

### 1.4 Corrected Semantics

| followerIsFlat | currentAction != lastAction | Returns (fixed) | Meaning |
|---|---|---|---|
| true | true (reversal) | false | Flat follower + reversal = ALLOW copy |
| false | true (reversal) | true | Open follower + reversal = SKIP (protection) |
| true | false (same dir) | false | Flat + same direction = ALLOW copy |
| false | false (same dir) | false | Open + same direction = ALLOW copy |

### 1.5 CYC Verification

Method `IsReversalToFlatFollower` after fix:
- base = 1
- `&&` operator = +1
- Total: CYC = 2. PASS (<=8).

### 1.6 Log Marker Behavior After Fix

Log marker `[PTT-COPY-GUARD] skip reversal entry` is emitted by the caller when
`IsReversalToFlatFollower` returns `true`.

After fix:
- MUST NOT fire when follower is flat + direction changed (was firing = bug)
- MUST still fire when follower has open position + direction changed (protection path)

No change to the log marker string itself. No new markers needed.

---

## Section 2: DW-B156-DEFER-1 (P1) -- TryEvictFollowerBeSlot Decomposition

### 2.1 Goal

Extract two private helpers from `TryEvictFollowerBeSlot` to reduce parent CYC from 14 to <=5.
No logic change. All existing log markers preserved. All existing tests must still pass.

### 2.2 Pre-Extraction State

File: `src/PropTraderTools/CopyEngine.cs`
Method: `TryEvictFollowerBeSlot(OrderEventArgs e)` at lines 1926-1973
Current CYC: 14 (strict McCabe, confirmed T1 verifier B156)
Current structure:
- Lines 1926-1932: null guard + IsEvictTriggerState gate + Clear
- Lines 1934-1957: leader-flat drain abort block
- Lines 1958-1972: follower eviction block

### 2.3 Extraction Design

#### CRITICAL CYC DESIGN DECISION

The `IsLeaderFlatDrainAbort` helper takes `(Order o, string instrFull)` rather than
`(OrderEventArgs e)`. The `instrFull` string is pre-computed by the parent using
`o.Instrument?.FullName ?? string.Empty` BEFORE calling the helper.

**Rationale:** The inner loop condition `kv.Value.Instrument?.FullName == instrFull`
contains one `?.` operator (+1 CYC). If `instrFull` were computed inside the helper
using `o.Instrument?.FullName ?? string.Empty`, that would add `?.`(+1) and `??`(+1)
inside the helper, pushing CYC to 10 (over limit). Pre-computing in the parent keeps
the parent at CYC=7 and the helper at CYC=8 (exactly at limit).

This is a JS-013-mandated design choice, not an arbitrary parameter preference.

#### Parent: TryEvictFollowerBeSlot (thin router after extraction)

```csharp
// CYC=7: e?.Order(1)+o==null(1)+IsEvictTriggerState(1)+o.Instrument?.FullName(1)+??(1)+IsFollowerAccount(1)=7
// (base=1 + 6 decision points)
// JS-021: no lock. JS-001: no throw. JS-002: void. ASCII-only.
private void TryEvictFollowerBeSlot(OrderEventArgs e)
{
    var o = e?.Order;                            // ?. (+1)
    if (o == null)                               // (+1)
        return;
    if (!IsEvictTriggerState(o))                 // (+1)
        return;
    _entryDispatchedOrders.Clear();
    string instrFull = o.Instrument?.FullName ?? string.Empty;  // ?. (+1), ?? (+1)
    IsLeaderFlatDrainAbort(o, instrFull);
    if (!IsFollowerAccount(o.Account))           // (+1)
        return;
    RunFollowerEvictPath(o);
}
```
CYC: base(1) + 6 = 7. PASS.

#### Helper A: IsLeaderFlatDrainAbort

```csharp
// DW-B156-01: leader-flat drain abort path extracted from TryEvictFollowerBeSlot.
// When leader's PTT-BE-Stop fills and leader is flat, aborts all pending follower drains
// whose Instrument matches. Runs before follower-only eviction path.
// CYC=8: IsFollowerAccount(1)+OrderState(1)+IsFlat(1)+foreach(1)+Instrument?.(1)+abortCount>0(1)
//        = base(1) + 7 = 8 (at limit).
// instrFull pre-computed by caller to avoid ?./??  cost inside this helper (JS-013).
// JS-021: ConcurrentDictionary foreach is lock-free snapshot. JS-001: no throw. ASCII-only.
private void IsLeaderFlatDrainAbort(Order o, string instrFull)
{
    if (IsFollowerAccount(o.Account))                           // guard: skip follower accounts (+1)
        return;
    if (o.OrderState != OrderState.Filled)                      // (+1)
        return;
    if (!IsFlat(FindPosition(o.Account, o.Instrument)))         // (+1)
        return;
    int abortCount = 0;
    foreach (var kv in _pendingDispatchDrains)                  // (+1)
    {
        if (kv.Value.Instrument?.FullName == instrFull)         // if (+1) + ?. (+1) = +2
        {
            AbortDrainOnFill(kv.Key);
            abortCount++;
        }
    }
    if (abortCount > 0)                                         // (+1)
        NinjaTrader.Code.Output.Process(
            "[DRAIN-ABORT-LEADER-FLAT] leader=" + o.Account.Name
            + " instr=" + instrFull
            + " aborted=" + abortCount,
            NinjaTrader.NinjaScript.PrintTo.OutputTab1
        );
}
```
CYC: base(1) + IsFollowerAccount(1) + OrderState(1) + IsFlat(1) + foreach(1) + if?(1) + ?.(1) + abortCount(1) = 8. PASS (at limit).

Note: `instrFull` replaces `o.Instrument?.FullName ?? "?"` in the log string --
same value, no `?.`/`??` cost inside this method.

#### Helper B: RunFollowerEvictPath

```csharp
// DW-B156-01: follower eviction path extracted from TryEvictFollowerBeSlot.
// Handles lines 1960-1972: flat-guard, slot eviction, retry reset, drain abort, log.
// CYC=4: isFilled&&IsFlat(1)+&&(1)+slotEvicted(1) = base(1)+3 = 4.
// JS-021: ConcurrentDictionary TryRemove is lock-free. JS-001: no throw. ASCII-only.
private void RunFollowerEvictPath(Order o)
{
    bool isFilled = o.OrderState == OrderState.Filled;
    if (isFilled && !IsFlat(FindPosition(o.Account, o.Instrument)))  // if(+1) + &&(+1)
        return;
    string accName = o.Account.Name;
    bool slotEvicted = _pendingFollowerBeSlots.TryRemove(accName, out _);
    _beReplaceAttempts.TryRemove(accName, out _);
    _filledBeTargetCount.TryRemove(accName, out _);
    AbortDrainOnFill(accName);
    if (slotEvicted)                                                 // (+1)
        LogBeSlotEviction(accName, o.OrderState == OrderState.Rejected);
}
```
CYC: base(1) + if(1) + &&(1) + if(1) = 4. PASS.

### 2.4 CYC Summary Table

| Method | Before | After | Limit | Pass? |
|---|---|---|---|---|
| TryEvictFollowerBeSlot | 14 | 7 | 8 | YES |
| IsLeaderFlatDrainAbort | new | 8 | 8 | YES |
| RunFollowerEvictPath | new | 4 | 8 | YES |

### 2.5 Placement

Both new helpers must be placed IMMEDIATELY after `TryEvictFollowerBeSlot` (after line 1973),
before `LogBeSlotEviction` at line 1975. Insert order: IsLeaderFlatDrainAbort, then
RunFollowerEvictPath.

---

## Section 3: DW-B153-05 (P2) -- Stale CYC Annotation for CountLeaderTargets

### 3.1 Exact Changes

File: `src/PropTraderTools/CopyEngine.cs`

**Change A (line 6639):**
Before: `// CountLeaderTargets: CYC=4. Returns the number of Working native target limit orders`
After:  `// CountLeaderTargets: CYC=6. Returns the number of Working native target limit orders`

**Change B (line 6646):**
Before: `// CYC<=5: rule null(1) + leader null(2) + foreach(3) + o null continue(4) + IsNativeLeaderTarget(5).`
After:  `// CYC<=6: rule null(1) + leader null(2) + foreach(3) + o null continue(4) + IsNativeLeaderTarget(5) + base(1).`

**Rationale:** Strict McCabe count for `CountLeaderTargets`:
base(1) + if rule==null(1) + if leader==null(1) + foreach(1) + if o==null(1) + if IsNativeLeaderTarget(1) = 6.
Lizard CCN=6 confirmed per B153/B156 verifier. Both annotations were stale.

Zero logic change. Zero behavior change.

---

## Section 4: DW-B153-06 (P2) -- Remove Dead Code UpdateLegTargetPrice

### 4.1 Verification (required before removal)

Pre-removal grep confirms:
- `src/PropTraderTools/CopyEngine.cs`: exactly 1 match for `UpdateLegTargetPrice` (line 3519 = definition only, zero callers).
- `src/PropTraderTools/CopyEngineTests.cs`: zero matches.
- Confirmed zero callers. Safe to remove.

### 4.2 Exact Lines to Remove

File: `src/PropTraderTools/CopyEngine.cs`
Remove lines 3514-3532 inclusive (comment block + method body):
```
// BWAVE-REFACTOR-LaneB-T5: extracted from CaptureOtherLegTargetPrices for-i body.
// [... 3 comment lines ...]
private void UpdateLegTargetPrice(double[] prices, int i, Order o, string excludeSuffix)
{
    string s = i.ToString();
    if (s == excludeSuffix)
        return;
    if (IsTargetOrderLive(o) && o.Name == "PTT-TGT-Drag-" + s)
        prices[i - 1] = o.LimitPrice;
    else if (
        IsTargetOrderLive(o)
        && o.Name == "Target" + s
        && prices[i - 1] == 0
    )
        prices[i - 1] = o.LimitPrice;
}
```

Do NOT remove or modify any surrounding methods. Do NOT reformat.

### 4.3 Test Impact

None. Zero test references confirmed.

---

## Section 5: DW-B156-DEFER-2 (P3) -- Stale CYC Annotation for TrySetSlateReadyOnDrainFill

### 5.1 Exact Change

File: `src/PropTraderTools/CopyEngine.cs`
Line: 991

Before: `// CYC<=5. JS-021: ConcurrentDictionary indexer is lock-free. JS-001: no throw. JS-002: void. ASCII-only.`
After:  `// CYC<=8. JS-021: ConcurrentDictionary indexer is lock-free. JS-001: no throw. JS-002: void. ASCII-only.`

**Rationale:** T2 verifier (B156) counted strict McCabe including `?.` operators and yielded CYC=8.
The annotation `CYC<=5` was under-counted. Non-blocking discrepancy (8<=8 = still compliant).

Zero logic change.

---

## Section 6 (DW-B156-DEFER-3): Described in Ticket Section Below

See B157-T3 test plan.

---

## Section 7: Ticket Structure

### B157-T1 (P0) -- Logic fix + tests

**Defect cards closed:** DW-B157-RG-01

**Files:**
- `src/PropTraderTools/CopyEngine.cs` (1-character change at line 6527)
- `src/PropTraderTools/CopyEngineTests.cs` (1 test modified + 3 tests added)

**Method signatures:**
```csharp
// CopyEngine.cs line 6521 -- signature UNCHANGED
internal static bool IsReversalToFlatFollower(
    OrderAction currentAction,
    OrderAction lastAction,
    bool followerIsFlat
)
// Body change ONLY: line 6527  followerIsFlat --> !followerIsFlat
```

**Test methods (CopyEngineTests.cs):**
```csharp
// MODIFY existing (line 8110): rename + flip assertion
[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]
public void ShouldBlockReversalCopy_WhenFollowerHasOpenPosition()

// ADD -- regression proof: bug was returning true here
[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]
public void IsReversalToFlatFollower_FlatFollower_DirectionChanged_ReturnsFalse()

// ADD -- protection path confirmation
[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]
public void IsReversalToFlatFollower_OpenFollower_DirectionChanged_ReturnsTrue()

// ADD -- same-direction never skips
[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]
public void IsReversalToFlatFollower_SameDirection_ReturnsFalse()
```

**JS rules checked:**
- JS-013: CYC=2 after fix. PASS.
- JS-021: No lock. Static method. PASS.
- JS-001: No throw. PASS.
- ASCII-001: All changes ASCII-only. PASS.

---

### B157-T2 (P1/P2/P3) -- Decomposition + annotations + dead code

**Defect cards closed:** DW-B156-DEFER-1, DW-B153-05, DW-B153-06, DW-B156-DEFER-2

**File:** `src/PropTraderTools/CopyEngine.cs` ONLY

**Method signatures:**

```csharp
// MODIFIED: TryEvictFollowerBeSlot (~line 1926) -- becomes thin router
// CYC=7. JS-021: no lock. JS-001: no throw. JS-002: void.
private void TryEvictFollowerBeSlot(OrderEventArgs e)

// NEW: extracted from lines 1938-1957
// CYC=8. instrFull = o.Instrument?.FullName ?? string.Empty (pre-computed by caller).
// JS-021: no lock. JS-001: no throw. JS-002: void.
private void IsLeaderFlatDrainAbort(Order o, string instrFull)

// NEW: extracted from lines 1960-1972
// CYC=4. JS-021: no lock. JS-001: no throw. JS-002: void.
private void RunFollowerEvictPath(Order o)
```

**Annotation changes (comment-only, zero logic):**
- Line 6639: `CYC=4` -> `CYC=6`
- Line 6646: `CYC<=5:` -> `CYC<=6:`
- Line 991: `CYC<=5.` -> `CYC<=8.`

**Dead code removal:**
- Lines 3514-3532: `UpdateLegTargetPrice` method + comment block (confirmed 0 callers)

**JS rules checked:**
- JS-013: All new/modified methods CYC<=8. See Section 2.4 table. PASS.
- JS-021: All state access via ConcurrentDictionary (lock-free). PASS.
- JS-001: No throw in any new method. PASS.
- ASCII-001: All identifiers and strings ASCII-only. PASS.
- JS-PUSH-01: No new shared mutable fields introduced. PASS.

**Tests:** None required. Pure structural extraction. All existing tests must still pass.

---

### B157-T3 (P3) -- Test assertion upgrade

**Defect card closed:** DW-B156-DEFER-3

**File:** `src/PropTraderTools/CopyEngineTests.cs` ONLY

**New test method:**
```csharp
// ADD: exact-string assertion for DRAIN-ABORT-LEADER-FLAT log marker.
// Stronger than existing test at ~line 9364 which checks "DRAIN-ABORT-FLAT" (substring).
// This test checks the exact marker "[DRAIN-ABORT-LEADER-FLAT]" including brackets and LEADER.
[Fact]
public void TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString()
```

**Implementation pattern:** Same file-read approach as existing test at line 9357-9364.
Assert: `Assert.Contains("[DRAIN-ABORT-LEADER-FLAT]", src)`

The existing test at line 9364 (`Assert.Contains("DRAIN-ABORT-FLAT", src)`) is NOT modified.
The new test is an ADDITION that provides stronger coverage.

---

## Section 8: Full Test Plan

### B157-T1 Test Specifications

| # | Test Name | [Fact] Attrs | Inputs | Assert |
|---|---|---|---|---|
| T1-1 | `ShouldBlockReversalCopy_WhenFollowerHasOpenPosition` | `[Fact(Skip="NT8-runtime...")]` | currentAction=Buy, lastAction=Sell, followerIsFlat=false | `Assert.True(wouldSkip)` |
| T1-2 | `IsReversalToFlatFollower_FlatFollower_DirectionChanged_ReturnsFalse` | `[Fact(Skip="NT8-runtime...")]` | currentAction=Buy, lastAction=Sell, followerIsFlat=true | `Assert.False(result)` |
| T1-3 | `IsReversalToFlatFollower_OpenFollower_DirectionChanged_ReturnsTrue` | `[Fact(Skip="NT8-runtime...")]` | currentAction=Sell, lastAction=Buy, followerIsFlat=false | `Assert.True(result)` |
| T1-4 | `IsReversalToFlatFollower_SameDirection_ReturnsFalse` | `[Fact(Skip="NT8-runtime...")]` | currentAction=Buy, lastAction=Buy, followerIsFlat=false | `Assert.False(result)` |

**T1-1 additional context:** Test was previously named
`ShouldSkipForReversalGuard_AllowsEntryWhenFollowerHasWorkingOrders`. The name is being
updated to reflect the CORRECTED semantics. The comment block at lines 8103-8108 must also
be updated to describe the protection path (skip when open follower + reversal) rather than
the allow path. No Skip attribute change required.

**T1-2 is the REGRESSION PROOF test.** Prior to fix, this input returned `true` (bug).
After fix it must return `false`. If this test fails post-engineer-implementation,
the bug fix was not applied correctly.

### B157-T2 Test Specifications

No new tests. Existing tests for `TryEvictFollowerBeSlot` region are the regression gate.
The following existing test names should be confirmed passing after T2 implementation:
- Any test containing `TryEvictFollowerBeSlot` in name
- Any test containing `DRAIN-ABORT` in name
- Any test containing `BeSlot` in name

Engineer must run `dotnet test` and confirm zero regressions (SCAN-06).

### B157-T3 Test Specifications

| # | Test Name | [Fact] Attrs | Assert |
|---|---|---|---|
| T3-1 | `TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString` | `[Fact]` | `Assert.Contains("[DRAIN-ABORT-LEADER-FLAT]", src)` using same file-read pattern as existing test at ~line 9357 |

---

## Section 9: 7-Scan Checklist Template (Per Ticket)

Engineer must self-report all 7 scans. Verifier re-runs independently.

```
## SCAN RESULTS -- B157-T[N]

SCAN-01: lock() scan
Command: Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern '^\s+lock\s*\('
Required: 0 executable lock() statements
Result: [PASS/FAIL -- count]

SCAN-02: CYC manual McCabe
All new/modified methods (B157-T1: IsReversalToFlatFollower;
B157-T2: TryEvictFollowerBeSlot, IsLeaderFlatDrainAbort, RunFollowerEvictPath):
Required: All <= 8
Results:
  IsReversalToFlatFollower (T1): CYC=2 [PASS]
  TryEvictFollowerBeSlot (T2): CYC=7 [PASS]
  IsLeaderFlatDrainAbort (T2): CYC=8 [PASS]
  RunFollowerEvictPath (T2): CYC=4 [PASS]

SCAN-03: async/await scan
Command: Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern '\basync\b|\bawait\b'
Required: 0 in executable code (comment hits acceptable)
Result: [PASS/FAIL -- count in executable]

SCAN-04: non-ASCII byte scan
Command: [System.IO.File]::ReadAllBytes('src\PropTraderTools\CopyEngine.cs') | Where-Object { $_ -gt 127 } | Measure-Object | Select-Object -ExpandProperty Count
Required: 0
Result: [PASS/FAIL]

SCAN-05: banned NT8 API scan
Command: Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'AtmStrategyCreate|AtmStrategyChangeStopTarget'
Required: 0 actual calls (comments only acceptable)
Result: [PASS/FAIL]

SCAN-06: build and test
Command: dotnet build; dotnet test
Required: 0 errors, 0 test failures, 0 regressions
Result: [PASS/FAIL -- error/failure counts]

SCAN-07: structural verification
Required: Verify all items at reported line numbers exist and match spec:
  T1: Line 6527 contains !followerIsFlat
  T1: Test file contains all 4 test methods
  T2: TryEvictFollowerBeSlot body is thin router (no inline abort logic)
  T2: IsLeaderFlatDrainAbort exists after TryEvictFollowerBeSlot
  T2: RunFollowerEvictPath exists after IsLeaderFlatDrainAbort
  T2: Line 6639 reads CYC=6
  T2: Line 6646 reads CYC<=6
  T2: UpdateLegTargetPrice is absent from file
  T2: Line 991 reads CYC<=8
  T3: Test method TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString exists
Result: [PASS/FAIL -- list any items not confirmed]
```

---

## Section 10: SIM Validation Steps

### SIM Plan for DW-B157-RG-01 (P0 -- must be SIM-confirmed before merge)

1. Configure: 1 leader account + 1 follower account in CopyEngine. Copy mode enabled.
2. Execute Long entry on leader.
   - Verify: follower receives Long copy entry. No `[PTT-COPY-GUARD]` in output.
3. Close Long on leader. Confirm both accounts flat.
4. Execute Short entry on leader (reversal scenario: prior=Long, current=Short).
   - **POST-FIX PASS criteria:** follower receives Short copy entry. `[PTT-COPY-GUARD]` does NOT appear in output.
   - **FAIL criteria (bug present):** `[PTT-COPY-GUARD] skip reversal entry` appears and follower does not receive Short copy.
5. Confirm follower holds Short position.
6. Close Short on leader. Confirm both accounts flat.
7. Execute another Short entry on leader (same direction: prior=Short, current=Short).
   - Verify: follower receives Short copy. No guard block.
8. PROTECTION PATH TEST: With follower holding open Short position, execute a Long entry on leader.
   - **POST-FIX PASS criteria:** `[PTT-COPY-GUARD] skip reversal entry` APPEARS in output. Follower does NOT receive Long copy (correctly blocked). Open Short position preserved.

### SIM Plan for DW-B156-DEFER-1 (P1 -- regression verification only)

1. Execute BE (Break-Even) workflow to trigger TryEvictFollowerBeSlot via OnOrderUpdate.
2. Confirm `[DRAIN-ABORT-LEADER-FLAT]` log fires when leader PTT-BE-Stop fills (leader flat).
3. Confirm `[BE-SLOT-EVICT]` or equivalent log fires when follower BE slot is evicted.
4. No new behavior expected. SIM is regression-only for this change.

---

## Section 11: Constraint Summary

| Constraint | Source | B157 Status |
|---|---|---|
| ASCII-only string literals and comments | ASCII-001 (P0) | All changes use straight ASCII. PASS. |
| No lock() anywhere | JS-021 (P0) | No lock() added. ConcurrentDictionary used throughout. PASS. |
| No async/await | NT8-001 (P0) | No async in any new/modified method. PASS. |
| No throw in hot paths | JS-001 (P0) | No throw statements in any change. PASS. |
| No DateTime.Now | NT8-006 (P1) | Not introduced. PASS. |
| CYC<=8 per method | JS-013 (P1) | All methods verified: max CYC=8 (IsLeaderFlatDrainAbort at limit). PASS. |
| Minimal change | Project rule | T1: 1 char + 4 test changes. T2: decompose + 2 annotation fixes + dead code removal. T3: 1 new test. PASS. |
| No reformatting | Project rule | Engineer must NOT reformat surrounding code or adjust whitespace outside changed lines. |
| No FontFamily | NT8-004 (P1) | Not applicable (no WPF changes). |
| No hex color strings | NT8-005 (P1) | Not applicable. |
| No AtmStrategyCreate | NT8-008 (P0) | Not introduced. |
| No AtmStrategyChangeStopTarget | NT8-009 (P0) | Not introduced. |
| PTT- prefix on CreateOrder | NT8-003 (P0) | No CreateOrder calls. |
| Dispatcher.InvokeAsync for WPF | JS-023 (P0) | No WPF mutations in any change. Not required. |

---

## Section 12: Carry-Forward for B157 Deferred Backlog (Pre-written)

The following items are expected to be OPEN at the end of B157 and must be carried forward
in B157/06-deferred-backlog.md by ptt-plan-reviewer (Ph5):

| ID | Item | Priority | Target |
|---|---|---|---|
| DW-B154-05 | IsPttCopyEntryFilled delegation refactor | P3 | Future CYC block |
| DW-B154-06 | TryTriggerBeRecovery post-deployment monitor | P2 | Post-deployment |
| DW-B155-01 | B155 SIM gate pending | P2 | Post-deployment |
| DW-BUG-0911-A-01 | Mid-close race condition in OnOrderUpdate | P2 | Future drain-path block |

---

_Architecture plan authored by ptt-architect, Block B157, Phase 1._
_Sequential thinking: 10 thoughts, all mandatory Thoughts 1-8 completed._
_Source lines verified: CopyEngine.cs lines 6521-6528, 1926-1973, 6639-6665, 3514-3532, 985-1010._
_Test lines verified: CopyEngineTests.cs lines 8100-8125, 9355-9379._
_B156 deferred backlog read at: docs/brain/B156/06-deferred-backlog.md._
_Rules catalog read at: docs/standards/RULES_CATALOG.md._
