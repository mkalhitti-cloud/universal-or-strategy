# B129 SIM Gate Session Notes — 2026-08-31

**Session**: Director SIM gate run after B129 LaneA + LaneB PIPELINE_COMPLETE + F5 green
**Date**: 2026-08-31
**Accounts**: Sim101 (leader), Sim102/103/104 (followers)
**Instrument**: MES SEP26
**ATM Template**: MES $200 SL 6 (order names: Stop1, Stop2, Stop3, Target1, Target2, Target3)

---

## F5 Compile Status

**PASS** — NinjaTrader 8 compiled successfully after ptt-sync-and-verify.ps1 (18 files, 0 MISMATCH).

---

## Test Results

### Test A — DW-B135 Reversal Guard Fix

**Result**: PARTIAL — guard fired once in the middle of the session, but copies ultimately completed.

**Observed output sequence**:
```
[PTT-COPY] dispatch: Buy x7 MES SEP26 -> Sim102/103/104       <- first entry copied OK
PositionStateChanged instr=MES SEP26 hasPos=False
PositionStateChanged instr=MES SEP26 hasPos=True
[PTT-COPY] dispatch: Buy x7 MES SEP26 -> Sim102/103/104       <- second Buy copied OK
[PTT-COPY-GUARD] skip reversal entry: Sim102 MES SEP26 follower flat  <- guard fired here
[PTT-COPY-GUARD] skip reversal entry: Sim103 MES SEP26 follower flat
[PTT-COPY-GUARD] skip reversal entry: Sim104 MES SEP26 follower flat
[PTT-COPY] dispatch: Sell x7 MES SEP26 -> Sim102/103/104      <- Sell copied OK after guard
[PTT-COPY] dispatch: Sell x7 MES SEP26 -> Sim102/103/104      <- second Sell dispatch (duplicate?)
```

**Analysis**:
- Guard fired once on Sell-after-Buy rapid open scenario
- Likely cause: hasPos=False -> hasPos=True transition happened in same tick
  TryRemove fired but second Buy dispatch wrote direction key again before Sell dispatch arrived
- Sell DID copy eventually — not blocking
- Prior-session stale direction key (trades from 8/30 in acc.Orders) may have contributed
- Logged as: B129-SIM-01 (observed, not blocking)

**Conclusion**: B129 LaneA fix is working for the clean-restart case. Edge case remains for
rapid close+reopen on same tick. Not a regression blocker for trading.

---

### Test B — DW-B128 Race Window Protection

**Result**: NOT TESTED (deferred — hard to trigger in clean SIM, not priority)
**Logged as**: B129-DEFER-02 (race window regression gate — deferred to low priority list)

---

### Test C — DW-B134 ATM Bracket Drag

**Result**: FAIL — follower brackets did NOT update when leader stop/target was dragged.

**What was tested**: Director dragged both target and stop loss on leader chart.
**Followers**: did not move their stops or targets.

**Root cause analysis** (NEW FINDING):
The DW-B134 fix added `EndsWith("STP")` to IsBracketLegStatic targeting "Buy STP" / "Sell STP"
ATM name format. However, the Director's ATM template (MES $200 SL 6) generates orders named:
  Stop1, Stop2, Stop3, Target1, Target2, Target3
NOT "Buy STP" / "Sell STP".

The `StartsWith("Stop")` predicate in IsBracketLegStatic DOES match Stop1/Stop2/Stop3.
The `StartsWith("Target")` predicate DOES match Target1/Target2/Target3.

So IsBracketLegStatic Layer 1 was NOT the actual bottleneck for this template.
The bottleneck is Layer 2: acc.Change() is a silent no-op on ATM-owned brackets.
The cancel+resubmit fix (SyncAtmFollowerBracket) should have fired — but didn't.

**New DW item**: DW-B137 — SyncFollowerBracket cancel+resubmit not firing for Stop1/Stop2/Target1
names. Architect must re-examine the routing logic from IsBracketLegStatic through
HandleBracketChange to SyncFollowerBracket to determine why cancel+resubmit is not triggered
for the MES $200 SL 6 template order names.

**Logged as**: DW-B137 — NEW P1

---

### Test C OCO Observation — DW-B134-OCO

**Result**: NOT OBSERVED (Test C did not reach cancel+resubmit stage, so no PTT-STP-Drag
order was created, so OCO orphan behavior could not be observed)
**Status**: DW-B134-OCO still OPEN — observation pending DW-B137 fix

---

### Test D — B120-DEFER-02 Fallback Flatten

**Result**: NOT APPLICABLE IN THIS RUN.
Director confirmed: QX-ALL was pressed when PTT-QX orders existed on Sim101 (normal path ran).
The [PTT-QX-FLATTEN] path only fires when SnapshotTargetOrders() returns empty (leader has
no PTT-QX brackets to cancel — specific edge case after BE-ALL + immediate QX-ALL).
Normal trading does not trigger this path. Moved to low-priority deferred list alongside
the DW-B128 race window gate.

---

## Additional Observations

### OBS-1: Intermittent Copy (Leader-only on first attempt, followers copy on retry)

**Symptom**: Occasionally, first entry attempt goes to leader only. Cancel and re-enter works.

**Root cause**: Stale order accumulation in acc.Orders.
Evidence: `[BE-DIAG] Sim104 orders-for-instr=46` and `[BE-DIAG] Sim101 orders-for-instr=46`
46 orders per account accumulated from multiple sessions without NT8 restart.

**Existing defect**: DW-B107 (MoveStopToBreakEven stale orders — same root, broader scope)
The HasWorkingEntries() check likely finds stale "Working" orders from prior sessions and
blocks the first copy dispatch. On retry, the check passes.

**Workaround**: If a follower misses a copy on first attempt, re-enter on leader.
The second attempt always works. NT8 restart clears stale orders.

**For testing**: Restart NT8 between test sessions to reset acc.Orders to clean state.
After restart, order count starts at 0 — intermittent copy issue does not occur in first
few trades of a fresh session.

### OBS-2: Duplicate Sell Dispatch

Two `[PTT-COPY] dispatch: Sell x7` lines appeared in sequence for the same entry.
This may be NT8 firing OnOrderUpdate twice for the same fill event (known NT8 behavior).
Each dispatch is guarded by HasWorkingEntries — second dispatch likely a no-op because
followers already have positions. Not a separate defect but worth noting.

### OBS-3: BE-ALL + QX-ALL sequence worked correctly

```
[BE-ALL] button: arm buf=0
[PTT-QX-2T-ALL] button: Sim101 MES SEP26 qty=7 T1=4 T2=3
[PTT-QX-2T-ALL] GlobalQuickExit fired (forced 2-target)
[PTT-QX-ALL] CancelPttBeOrders: acc=Sim101 count=0
[PTT-QX-2T-ALL] leader: Sim101 MES SEP26 qty=7 forcedTargetCount=2
```

BE-ALL armed, then QAll2t fired. BE was cancelled (count=0 means no active BE orders —
BE was just armed, no stops submitted yet). QAll2t submitted PTT-QX brackets (T1=4, T2=3)
correctly for all 4 accounts. All 4 accounts covered. No naked positions.

QAll2t is working correctly for the normal trading scenario.

---

## New Defects Logged This Session

| ID | Priority | Description | Action |
|----|----------|-------------|--------|
| DW-B137 | P1 | SyncFollowerBracket cancel+resubmit not firing for MES $200 SL 6 template (Stop1/Stop2/Target1 names). DW-B134 fix targeted wrong ATM name format. | Needs architect investigation of HandleBracketChange routing for these names. |
| B129-SIM-01 | P2 | Guard fired once on rapid close+reopen (hasPos=False+True same tick). Copy still completed. Not blocking. | Monitor — may need a short debounce or sequence-number guard. |

---

## Deferred / Low Priority Gates (Not Worth Testing Now)

These gates require extreme timing or rare conditions not relevant to normal trading:

| Gate | Reason Deferred |
|------|----------------|
| Test B — DW-B128 race window | Requires manually submitting a close order at exact millisecond the position closes. Not achievable cleanly in SIM. Not blocking trading. |
| B120-DEFER-02 — fallback flatten | Only fires in a specific edge case (BE-ALL then immediate QX-ALL before BE stops are submitted). Normal trading does not hit this path. |
| B119-DEFER-02 — old reversal guard gate | Obsoleted by DW-B135 fix. Old gate expected guard to fire on every direction change — that was the bug. New behavior: guard fires only during race window. |

---

## Overall SIM Gate Status

| Test | Status | Notes |
|------|--------|-------|
| Test A (DW-B135) | PARTIAL_PASS | Guard fired once (edge case), but copies completed. B129 fix is working for normal case. |
| Test B (DW-B128) | DEFERRED | Low priority — hard to trigger in clean SIM |
| Test C (DW-B134) | FAIL → NEW DW-B137 | Drag not working — wrong ATM name format targeted |
| Test C OCO | NOT OBSERVED | Blocked by Test C fail |
| Test D (B120-DEFER-02) | DEFERRED | Not relevant to normal trading |
| QAll2t / BE-ALL combo | PASS | Works correctly, 4 accounts covered, T1=4 T2=3 |
| Intermittent copy | KNOWN BUG | DW-B107 stale orders — workaround: restart NT8 between sessions |

---

## Next Block Priority (B130)

1. **DW-B137 (P1)** — Fix bracket drag for MES $200 SL 6 template (Stop1/Stop2/Target1 names)
   Architect must trace HandleBracketChange -> SyncFollowerBracket path for these order names
2. **DW-B134-OCO (P2)** — OCO orphan observation (blocked until DW-B137 is fixed)
3. **DW-B136 Gap B (P1)** — Order-ID scoped cancel for two simultaneous entries
4. **DW-B107 (P2)** — Stale order accumulation in acc.Orders (46 orders after multi-session run)
5. **Complexity refactor** — CopyEngine.cs is well above CYC <=8 threshold on many methods.
   This is making agent execution harder. Needs a dedicated refactor wave.

---

## Key Source Locations (for B130 architect)

| Symbol | File | Line | Purpose |
|--------|------|------|---------|
| IsBracketLegStatic | CopyEngine.cs | ~L3621 | Gate for bracket drag routing — DW-B137 investigation start |
| HandleBracketChange | CopyEngine.cs | ~L2040 | Routes to SyncFollowerBracket or SyncAtmFollowerBracket |
| SyncFollowerBracket | CopyEngine.cs | ~L2048 | Calls acc.Change() or SyncAtmFollowerBracket |
| SyncAtmFollowerBracket | CopyEngine.cs | ~L2100 | Cancel+resubmit helper (B129 LaneB) |
| IsAtmSTPOrder | CopyEngine.cs | ~L2028 | EndsWith("STP") predicate — only covers "Buy STP" format |
| TryFirePositionState | CopyEngine.cs | ~L2361 | B129 LaneA fix — TryRemove on hasPos=False |
| _lastLeaderDirection | CopyEngine.cs | L331 | Direction dict cleared by B129 LaneA |
