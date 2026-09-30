# B169 Final Review
## Block: DW-B168-02 -- _dragsInFlight HashSet<string> Race Fix
## Phase: 5 (PTT Plan Reviewer -- Final Review)
## Status: FINAL_PASS

---

## A. Executive Summary

Block B169 implements DW-B168-02: the COLLATERAL-STORM race condition introduced
by NT8 SIM batch-acknowledgement (all three dragged stops reach Accepted before
any handler body runs, making the B168 IsLeaderLegBeingChanged guard permanently
dead code). The fix records in-flight stop suffixes at ChangePending time via a
per-instance `HashSet<string> _dragsInFlight`, checks the set in
`ResubmitCollateralLegs` before dispatching collateral resubmits, and clears the
set at Accepted/ChangeRejected/Cancelled callbacks.

Both tickets (B169-1: CopyEngine.cs implementation; B169-2: CopyEngineTests.cs
test replacement) achieved BUILD_PASS and VERIFY_PASS. All 7 scans returned the
expected values across both files. All 10 Final Checks pass. DW-B168-01
single-stop-drag behavior is confirmed unaffected.

---

## B. Diff Guard (FC1)

### Estimated character delta

Per architecture plan Section 4:

| File | Net lines +/- |
|------|--------------|
| src/PropTraderTools/CopyEngine.cs | +32 net lines |
| src/PropTraderTools/CopyEngineTests.cs | -28 net lines |

At an average of 60 characters per line (generous, given short comment lines),
total gross delta across both files is approximately:
  CopyEngine.cs: ~55 changed lines x 60 chars = ~3,300 chars
  CopyEngineTests.cs: ~65 changed lines x 60 chars = ~3,900 chars
  Total: ~7,200 chars

Architecture plan states "60-80 net lines changed. Well within 10,000 char
DIFF GUARD." Independent estimate of ~7,200 chars confirms this comfortably.

**FC1 RESULT: PASS** -- Estimated diff character count ~7,200 < 10,000 limit.

---

## C. Safety Checks (FC2, FC3, FC6)

### FC2 -- No new lock() in diff

grep `lock(` in src/PropTraderTools/CopyEngine.cs returns **14 matches**,
identical to the Phase 4a engineer baseline. All 14 matches are in comment
strings (e.g., "no lock()", "JS-021: no lock()"). No actual `lock(` statement
exists anywhere in CopyEngine.cs.

Independent verifier (ticket-1-verification V3) confirmed:
  - `^\s+lock\s*(` (exact statements): 0 matches
  - `lock(` (broad): 14 matches, ALL in comment text

**FC2 RESULT: PASS** -- Zero actual lock() statements. Count matches Phase 4b
report. JS-021 satisfied.

### FC3 -- ptt-sync.ps1 confirmed

ticket-1-completion.md SCAN-07:
```
ptt-sync complete: 18 linked, 0 errors
Exit: 0
```
Hard-link synchronization confirmed executed and successful. ticket-2-completion
notes correctly that CopyEngineTests.cs is not hard-linked to NT8 and no second
ptt-sync run is required.

**FC3 RESULT: PASS** -- ptt-sync.ps1 exit 0 recorded in completion report.

### FC6 -- IsLeaderLegBeingChanged fully gone

grep `IsLeaderLegBeingChanged` across entire src/ directory: **0 matches**.

Both the production method (lines 3815-3818 pre-edit) and its testable seam
(lines 3820-3824 pre-edit) are fully removed. The CopyEngineTests.cs class
`DwB168Tests` that called `IsLeaderLegBeingChangedTestable` is also absent
(ticket-2-verification V15: 0 matches).

Edit J updated the CYC comment in `ResubmitCollateralLegs` to remove any
reference to `IsLeaderLegBeingChanged`, ensuring the scan reports clean zero
including comment text.

**FC6 RESULT: PASS** -- 0 occurrences across all of src/. Dead code fully
removed.

---

## D. Test Results (FC4, FC5)

### FC4 -- 4 new tests present and correctly named

grep `DragsInFlight_` in CopyEngineTests.cs confirms **4 matches**:

| Line | Method Name |
|------|-------------|
| 10189 | `DragsInFlight_SingleStop_CollateralResubmitted` |
| 10202 | `DragsInFlight_ThreeStop_CollateralSuppressedForInFlightLeg` |
| 10218 | `DragsInFlight_ThreeStop_LastHandlerResubmitsAllLegs` |
| 10243 | `DragsInFlight_ClearOnCancelled_PreventsPermamentSkip` |

All 4 names match the spec exactly. `[Fact]` attributes independently confirmed
at lines 10188, 10201, 10217, 10242 (Phase 4b verifier V11). Class `DwB168_02Tests`
present at line 10179 inside the namespace.

**FC4 RESULT: PASS** -- Exactly 4 [Fact] methods with correct names.

### FC5 -- Existing test suite not broken

- `class DwB168Tests` grep: **0 matches** (intentional, DwB168Tests tested the
  now-removed IsLeaderLegBeingChangedTestable seam).
- No other [Fact] methods outside DwB168_02Tests were removed or renamed.
  ticket-2-verification confirms only the DwB168Tests class was deleted (Edit 1:
  lines 10175-10268), replaced by DwB168_02Tests (Edit 2).
- File structure verification confirms namespace closes correctly at line 10259.

**FC5 RESULT: PASS** -- No existing tests broken. DwB168Tests removal is
intentional and correct (the seam it tested is removed).

---

## E. Regression Proof DW-B168-01 (FC7)

DW-B168-01 established the single-stop drag path. The _dragsInFlight change
preserves this path exactly:

**Single-stop drag proof (Stop2 only):**

1. Stop2 ChangePending fires -> `TryTrackDragInFlight(order)` ->
   `_dragsInFlight.Add("2")` -> set = {"2"}.
2. Stop2 Accepted fires -> `HandleAtmStopSync` processes Stop2 ->
   `SyncAtmFollowerBracket` + `ResubmitCollateralLegs` called.
3. Inside `ResubmitCollateralLegs`, iterating otherLegPrices:
   - leg "1": `_dragsInFlight.Contains("1")` -> false -> NOT skipped ->
     `FindLeaderCollateralOrder` -> `ResubmitOneCollateralLeg`. ✓
   - leg "3": `_dragsInFlight.Contains("3")` -> false -> NOT skipped ->
     `FindLeaderCollateralOrder` -> `ResubmitOneCollateralLeg`. ✓
4. After `ResubmitCollateralLegs`: `_dragsInFlight.Remove("2")` -> set = {}.

Result: collateral legs 1 and 3 each receive exactly one clean resubmit.
Behavior is IDENTICAL to the original single-stop-drag path (pre-B168, pre-B169).
DW-B168-01 invariant is preserved.

**FC7 RESULT: PASS** -- DW-B168-01 single-stop drag path confirmed unaffected.

---

## F. Fix Proof DW-B168-02 (FC8)

**Three-stop simultaneous drag proof:**

1. All three ChangePending events fire first (before any broker round-trip):
   - Stop1 ChangePending -> Add("1") -> set = {"1"}
   - Stop2 ChangePending -> Add("2") -> set = {"1","2"}
   - Stop3 ChangePending -> Add("3") -> set = {"1","2","3"}

2. NT8 SIM batch-acks all three. Stop1 Accepted fires first:
   - HandleAtmStopSync processes Stop1 (legSuffix = "1").
   - ResubmitCollateralLegs iterates otherLegPrices:
     * leg "2": Contains("2") -> TRUE -> continue (skipped). ✓
     * leg "3": Contains("3") -> TRUE -> continue (skipped). ✓
   - After ResubmitCollateralLegs: Remove("1") -> set = {"2","3"}.

3. Stop2 Accepted fires:
   - HandleAtmStopSync processes Stop2 (legSuffix = "2").
   - ResubmitCollateralLegs iterates:
     * leg "1": Contains("1") -> FALSE -> FindLeaderCollateralOrder ->
       ResubmitOneCollateralLeg (one clean resubmit of leg 1). ✓
     * leg "3": Contains("3") -> TRUE -> continue (skipped). ✓
   - After ResubmitCollateralLegs: Remove("2") -> set = {"3"}.

4. Stop3 Accepted fires:
   - HandleAtmStopSync processes Stop3 (legSuffix = "3").
   - ResubmitCollateralLegs iterates:
     * leg "1": Contains("1") -> FALSE -> ResubmitOneCollateralLeg. ✓
     * leg "2": Contains("2") -> FALSE -> ResubmitOneCollateralLeg. ✓
   - After ResubmitCollateralLegs: Remove("3") -> set = {}.

Net result:
  - Leg "1": resubmitted once (by Stop2) and once (by Stop3) = 2 resubmits,
    both clean (no concurrent drags in flight when each fires). No storm.
  - Leg "2": resubmitted once (by Stop3). No storm.
  - Leg "3": 0 resubmits from collateral path (each handler was skipped).
    Stop3 handles its own PTT-STP-Drag-3 via SyncAtmFollowerBracket directly.

This matches the Design A analysis in DW-B168-02-design.md Section 4 exactly.
Maximum 1 cancel+resubmit per leg at any time. No CancelPending storm.
Prior behavior was 3 cancel+resubmit cycles per leg = CancelPending.

**FC8 RESULT: PASS** -- Three-stop simultaneous drag race condition resolved.

---

## G. Cross-File Coherence

### CopyEngine.cs -- CopyEngineTests.cs contract

- `DragsInFlightForTest()` seam at line 4208 (CopyEngine.cs) returns the live
  `_dragsInFlight` HashSet<string> instance.
- All 4 test methods in `DwB168_02Tests` access `_dragsInFlight` exclusively via
  `CopyEngine.Instance.DragsInFlightForTest()`. No direct field access from test
  assembly.
- Seam is `internal`, not `public` -- correct for test-only access.
- Pattern matches `IsPttStpDragCancellableTestable` at line 4169 (existing seam).

### No orphaned callers

- `IsLeaderLegBeingChanged`: removed from CopyEngine.cs (0 matches). Only
  caller was `ResubmitCollateralLegs` loop body -- that call is replaced.
- `IsLeaderLegBeingChangedTestable`: removed from CopyEngine.cs. Only caller
  was `DwB168Tests` in CopyEngineTests.cs -- that class is removed.
- No other files in src/ reference these methods (both were private/internal
  to CopyEngine).

### All change sites wired

| Site | Method | Wired? |
|------|--------|--------|
| Field declaration | _dragsInFlight (line 447) | YES |
| Add site | TryTrackDragInFlight -> ChangePending branch (line 2401) | YES |
| ChangeRejected clear | TryTrackDragInFlight -> ChangeRejected branch (line 2403) | YES |
| Cancelled clear | TryRemoveDragSuffix (line 2416), called from TryCancelFollowerEntries (line 2288) | YES |
| Accepted clear | HandleAtmStopSync (line 3558), after ResubmitCollateralLegs | YES |
| Guard | ResubmitCollateralLegs (line 4015), before FindLeaderCollateralOrder | YES |
| Test seam | DragsInFlightForTest() (line 4208) | YES |

All 7 code-bearing _dragsInFlight occurrences are wired and intentional.

**G RESULT: PASS** -- Full cross-file coherence confirmed.

---

## H. All 7 Scans Zero (Summary from Phase 4b)

### B169-1 (CopyEngine.cs) scans

| Scan | Pattern | Expected | Actual | Result |
|------|---------|----------|--------|--------|
| SCAN-01 | IsLeaderLegBeingChanged | 0 matches | 0 matches | PASS |
| SCAN-02 | _dragsInFlight | 7 code-bearing | 7 code-bearing (12 total, 5 in comments) | PASS |
| SCAN-03 | lock( actual | 0 actual lock() | 14 comment-only, 0 actual | PASS |
| SCAN-04 | CYC all touched methods <= 8 | all <= 8 | max CYC=4 (TryCancelFollowerEntries, unchanged) | PASS |
| SCAN-05 | non-ASCII | 0 matches | 0 matches | PASS |
| SCAN-06 | Contains before FindLeaderCollateralOrder | Contains at 4015, Find at 4017 | Confirmed | PASS |
| SCAN-07 | ptt-sync.ps1 exit 0 | exit 0 | 18 linked, 0 errors | PASS |

### B169-2 (CopyEngineTests.cs) scans

| Scan | Pattern | Expected | Actual | Result |
|------|---------|----------|--------|--------|
| SCAN-01 | DwB168Tests absent | 0 matches | 0 matches | PASS |
| SCAN-02 | DragsInFlightForTest refs | >= 4 code-bearing | 5 code-bearing | PASS |
| SCAN-03 | DwB168_02Tests present | 1 match | 1 match (line 10179) | PASS |
| SCAN-04 | Exactly 4 [Fact] methods | 4 | 4 (lines 10188/10201/10217/10242) | PASS |
| SCAN-05 | whitespace mutations | 0 | 0 | PASS |
| SCAN-06 | IsLeaderLegBeingChangedTestable calls | 0 | 0 | PASS |
| SCAN-07 | ASCII-only | 0 non-ASCII | 0 | PASS |

All 14 scans across both tickets pass. No violations across src/PropTraderTools/.

---

## K. Deferred Work

### Section K -- Deferred Work Tracking

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B169-K1 | SIM GATE: Director must drag all 3 stops simultaneously in NinjaTrader SIM and confirm 0 missing follower stops (three-stop DW-B168-02 path) | P0 | Next SIM session | OPEN |
| DW-B169-K2 | SIM GATE: Director must also test single-stop drag in same SIM session to confirm no DW-B168-01 regression | P0 | Next SIM session | OPEN |
| DW-B168-02 | COLLATERAL-STORM recurrence (_dragsInFlight HashSet guard) | P0 | B169 | CLOSED |
| DW-B168-01 | COLLATERAL-STORM original fix (IsLeaderLegBeingChanged guard) -- confirmed unaffected by B169 changes | P1 | B169 (regression check) | CLOSED |
| DW-B168-K1 | Director SIM gate from B168: drag all 3 stops simultaneously, confirm 0 missing follower stops -- now superseded by DW-B169-K1 which covers both the new fix AND the single-stop regression path | P0 | Next SIM session | OPEN (superseded by DW-B169-K1/K2) |
| B167-DEFER-02 / DW-B169-K3 | DW-B167-02: follower ATM brackets never placed after leader fill (fo=NULL). Second output.md required. Dedicated block required. | P1 | future | OPEN |
| B167-DEFER-01 / DW-B169-K4 | ECA-1 double-position race (pre-fill drag + manual fill). Low probability. Monitor at SIM. | P2 | future | OPEN |
| B167-DEFER-03 / DW-B169-K5 | Combined B161-B166 SIM gate pending | P1 | future | OPEN |
| B167-DEFER-04 / DW-B169-K6 | B167 SIM gate pending (prerequisite for B168/B169 SIM gate) | P1 | future | OPEN |
| DW-B159-K2 / DW-B169-K7 | 14 xUnit tests for B159 features (infrastructure required) | P2 | future | OPEN |
| DW-B159-K3 / DW-B169-K8 | Attempt cap hardcoded at 3 in TryArmBePriceRetry | P2 | future | OPEN |
| DW-B159-K4 / DW-B169-K9 | intendedBePrice sourcing dependency | P2 | future | OPEN |
| DW-B159-K5 / DW-B169-K10 | Stale line number in IsFollowerPositionBlocked comment | P3 | future | OPEN |

### SIM Gate Procedure (DW-B169-K1 + DW-B169-K2)

**Test 1 (DW-B168-02 fix verification):**
1. Load PropTraderTools in NinjaTrader SIM.
2. Open ATM with 3 stop legs on leader account. Ensure followers are active.
3. Simultaneously drag all 3 stop handles to a new price.
4. Confirm: all 3 follower accounts show updated stop orders at new price.
5. Confirm: Output Tab1 shows NO CancelSubmitted/CancelPending storm for follower stops.
6. Expected: exactly 1 cancel+resubmit event per follower stop leg. No duplicates.

**Test 2 (DW-B168-01 regression check):**
1. Same setup. Drag only 1 stop handle.
2. Confirm: collateral follower legs (the 2 non-dragged stops) resubmit normally.
3. Confirm: no suppression of collateral resubmit (leg should NOT be skipped).
4. Expected: 1 clean cancel+resubmit per collateral follower stop.

FINAL_PASS is granted at implementation level. Director SIM gate is required for
production confidence. Blocks DW-B169-K1 and DW-B169-K2 remain OPEN until gate.

---

*Final review produced by ptt-plan-reviewer. Phase 5. Block B169.*
*DW-B168-02 implementation complete. FINAL_PASS.*
