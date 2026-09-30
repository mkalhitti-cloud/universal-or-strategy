# B139 Deferred Backlog

**Block**: B139
**Block Header**: B139 Deferred Items
**Produced by**: ptt-plan-reviewer (Phase 5)
**Prior backlog**: `docs/brain/B137/06-deferred-backlog.md`
**Date**: 2026-09-01

---

## Status Changes From B137

| ID | B137 Status | B139 Status | Change |
|----|-------------|-------------|--------|
| DW-B152-B | OPEN (P1, identified B139 plan) | **CLOSED** | B139-T1 expanded CancelExistingPttStpDrag filter via IsPttStpDragCancellable predicate to include CancelPending and CancelSubmitted. B139-T2 added 7 xUnit tests covering all states. VERIFY_PASS issued for both tickets. |
| DW-B141 | OPEN (awaiting SIM Test A) | OPEN (awaiting SIM Test A) | No change. B139 does not touch Phase C / ExecutePhaseCStopReplacement path. |
| DW-B138 | OPEN (awaiting SIM Test B) | OPEN (awaiting SIM Test B) | No change. B139 does not touch FindFollowerBracketOrder path. |
| B135-DEFER-01 | OPEN (P1) | OPEN (P1) | No change. B139 does not touch entry-copy path. |
| B135-DEFER-02 | OPEN (P2) | OPEN (P2) | No change. B139 does not touch FindFollowerBracketOrder iteration scope. |
| DW-B134-OCO-OBS | OPEN (OBS-A/B/C/D) | OPEN (OBS-A/B/C/D) | No change. B139 does not touch partial-fill race code paths. |

---

## New Closure -- B139

---

### DW-B152-B -- Cancel-In-Flight Race (CancelPending/CancelSubmitted Gap)

| Field | Value |
|-------|-------|
| **ID** | DW-B152-B |
| **Title** | Cancel-in-flight race in SyncAtmFollowerBracket Block B -- CancelPending/CancelSubmitted gap |
| **Status** | **CLOSED** |
| **Priority** | P1 |
| **Closed in Block** | B139 |
| **Closed by** | T1: `IsPttStpDragCancellable` predicate + `CancelExistingPttStpDrag` refactor; T2: 7 xUnit tests |

**Root cause**: CancelExistingPttStpDrag filter (Submitted||Working||Accepted) did not match
CancelPending or CancelSubmitted. In a 3-stop ATM 3-event rapid burst, Event#2's cancel of
PTT-STP-Drag#1 puts it in CancelPending. Event#3's pre-sweep did not match CancelPending, allowing
Block B to place PTT-STP-Drag#3 alongside the already-cancelling PTT-STP-Drag#1.

**Fix**: `IsPttStpDragCancellable` (CopyEngine.cs L2395-2400, CYC=5) now returns true for
Submitted||Working||Accepted||CancelPending||CancelSubmitted. `CancelExistingPttStpDrag`
(CopyEngine.cs L2413-2433, CYC=6) delegates to `IsPttStpDragCancellable` at L2418.
acc.Cancel() on CancelPending/CancelSubmitted is idempotent; rejection absorbed by try/catch (L2423-2430).

**Closure evidence**:
- `IsPttStpDragCancellable(Order o)` added at CopyEngine.cs L2395-2400 (CYC=5).
- `IsPttStpDragCancellableTestable(Order o)` seam added at L2404-2405 (CYC=1).
- `CancelExistingPttStpDrag` refactored at L2407-2433; inline 3-state condition replaced with IsPttStpDragCancellable(o).
- OrderState.CancelPending at L2399 (NT8_FULL_REFERENCE.md L966, L3368 confirmed).
- OrderState.CancelSubmitted at L2400 (NT8_FULL_REFERENCE.md L971, L3369 confirmed).
- T1 BUILD_PASS. T1 VERIFY_PASS (7 scans zero; all implementation checks pass).
- T2: 7 xUnit [Fact] tests created in B139Tests.cs. dotnet test: Passed 7, Failed 0.
- T2 BUILD_PASS. T2 VERIFY_PASS (7 scans zero; all content checks pass).

**Note on DW-B152 predecessor**: DW-B152 (Submitted filter, commit 5250d8ee) remains valid as a prior
partial fix. DW-B152-B completes the closure by adding CancelPending and CancelSubmitted.

---

## Deferred Items (Carried Forward -- OPEN)

---

### DW-B141 -- Phase C Re-Confirmation (Pending SIM Test A)

| Field | Value |
|-------|-------|
| **ID** | DW-B141 |
| **Title** | SyncAtmFollowerTarget Phase C operable -- pending SIM Test A confirmation |
| **Status** | OPEN (awaiting SIM run) |
| **Priority** | P1 |
| **Target Block** | B135 SIM (carries forward until run) |
| **Root Block** | Pre-B134 (open since DW-B141 creation) |

**Description**: Phase C of `SyncAtmFollowerTarget` is the stop-replacement sub-path triggered when
a target drag moves the target far enough that the leader stop must be replaced. The B134 SIM PARTIAL
run confirmed Phase B (target drag -> follower sync) but did not conclusively exercise Phase C
(stop replacement leg). B137 T1 extracted Phase C to `ExecutePhaseCStopReplacement` (structural
refactor -- zero behavior change). The Phase C code path is unchanged.

**B139 impact**: None. B139 does not touch Phase C or SyncAtmFollowerTarget.

**SIM Test A Procedure** (unchanged from B137):
1. Open leader + 1 follower in SIM. Enter position via ATM.
2. Drag leader target order far enough past current stop that the stop must relocate (Phase C trigger).
3. Observe follower: PTT-TGT-Drag should move to new target price AND PTT-STP-Drag should appear
   (or move) to new stop price.
4. If both: DW-B141 CLOSED. If PTT-STP-Drag absent or mispositioned: remains INCONCLUSIVE.

**Resolution Condition**: Director or engineer runs SIM Test A with B139 fix deployed.

---

### DW-B138 -- Follower Stop Drag Confirmed (Pending SIM Test B)

| Field | Value |
|-------|-------|
| **ID** | DW-B138 |
| **Title** | Follower stop leg drag sync confirmed -- pending SIM Test B director confirmation |
| **Status** | OPEN (awaiting SIM run) |
| **Priority** | P1 |
| **Target Block** | B135 SIM (carries forward until run) |
| **Root Block** | B131 (DW-B138 created; leaderName param added to FindFollowerBracketOrder) |

**Description**: DW-B138 (B131) added the `leaderName` parameter to `FindFollowerBracketOrder`
to enable ATM Name-based stop bracket identification when `fromEntrySignalName` is null/empty.
B137 T3 fixes DW-B150 empty-string signalName routing; B137 T4 adds pre-sweep (DW-B151); B139
strengthens the pre-sweep to cover CancelPending/CancelSubmitted. SIM Test B (end-to-end stop drag
sync) has not been run.

**B139 impact**: B139 strengthens the pre-sweep guard (CancelPending/CancelSubmitted now caught),
further reducing the risk of PTT-STP-Drag accumulation during burst events. SIM Test B remains the
final end-to-end confirmation step.

**SIM Test B Procedure** (unchanged from B137):
1. Open leader + 1 follower in SIM. Enter position via ATM.
2. Drag leader stop bracket to a new price.
3. Observe follower: stop bracket should move to same price within 1 tick.
4. If sync occurs: DW-B138 CLOSED. If no sync: investigate guard chain.

**Resolution Condition**: Director runs SIM Test B with B139 fix deployed.

---

### B135-DEFER-01 -- Gap B Runtime (Two Simultaneous Entries)

| Field | Value |
|-------|-------|
| **ID** | B135-DEFER-01 |
| **Title** | Gap B -- two simultaneous leader entries, cancel first, verify 2nd copied |
| **Status** | OPEN |
| **Priority** | P1 |
| **Target Block** | B138+ |
| **Root Block** | B133 (originally B133-DEFER-01, carried through B134-DEFER-01, B135-DEFER-01) |

**Description**: If two leader entry signals fire in rapid succession (before the first follower copy
completes), the copy engine may cancel the first copy attempt and start a second. The second copy
may fail to find a valid follower entry if the first partial state was not fully cleaned up. This is
a runtime race condition that cannot be confirmed without SIM data showing two near-simultaneous
entry triggers.

**B139 impact**: None. B139 does not touch the entry-copy path.

**Resolution Condition**: Director or engineer demonstrates two-entry scenario in SIM;
`TryEvictFollowerBeSlot` and related gate logic confirmed to handle the second entry correctly. If
not handled: engineer implements a copy-queue or idempotency guard.

---

### B135-DEFER-02 -- Stale Orders Multi-Session

| Field | Value |
|-------|-------|
| **ID** | B135-DEFER-02 |
| **Title** | Stale orders from prior sessions may match FindFollowerBracketOrder |
| **Status** | OPEN |
| **Priority** | P2 |
| **Target Block** | future |
| **Root Block** | B133 (originally B133-DEFER-02, carried through B134-DEFER-02, B135-DEFER-02) |

**Description**: `FindFollowerBracketOrder` iterates `follower.Orders` which may, in certain NT8
reconnect scenarios, contain Working/Accepted orders from a prior trading session. These stale orders
could incorrectly match the filter and return as a valid `fo`, causing a cancel+resubmit sequence on
a prior-session bracket. Risk is LOW under normal trading hours (NT8 clears orders on disconnect).

**B139 impact**: None. B139 does not change `FindFollowerBracketOrder` iteration scope.

**Resolution Condition**: Director or engineer confirms whether `follower.Orders` is cleared on NT8
disconnect/reconnect. If not cleared: implement a session-epoch guard or timestamp filter.

---

### DW-B134-OCO (OBS-A/B/C/D) -- Partial-Fill Race Conditions

| Field | Value |
|-------|-------|
| **ID** | DW-B134-OCO-OBS |
| **Title** | OCO orphan partial-fill race conditions (OBS-A/B/C/D) -- carry-forward after T2 partial close |
| **Status** | OPEN (OBS-A/B/C/D) |
| **Priority** | P1 |
| **Target Block** | future |
| **Root Block** | B134 (DW-B134-OCO original; flat-position sweep closed by B135 T2) |

**Context**: B135 T2 closed the main DW-B134-OCO orphan-after-flat condition by adding
`TrySweptPttDragOrphans` + `CancelPttDragOrphansForAccount`. The four remaining sub-observations
require SIM data showing partial-fill race sequences.

**Remaining open sub-observations** (unchanged from B137):

| Obs ID | Description | Why Not Closed |
|--------|-------------|----------------|
| OBS-A | Cancel races partial fill -- acc.Cancel may be rejected with ErrorCode.UnableToCancelOrder after partial fill | T2 absorbs error via try/catch; does not prevent partial-fill race window |
| OBS-B | Replacement order duplicates partially-filled quantity -- follower over-positioned | T2 sweep is post-flat; pre-flat partial-fill state unaddressed |
| OBS-C | Stop side not cancelled before target replacement -- brief unhedged position window | T2 sweep is post-flat; pre-flat bracket ordering unaddressed |
| OBS-D | Net position drift on two-leg partial fill -- follower bracket position diverges from leader | Requires quantity-aware cancel guard in SyncAtmFollowerTarget; out of scope |

**B139 impact**: None. B139 fixes bracket sync pre-sweep but does not address partial-fill race conditions.

**Resolution Condition**: Each OBS requires SIM data showing the partial-fill race sequence.
OBS-B and OBS-D require quantity-aware guard in `SyncAtmFollowerTarget` Block A.
OBS-C may require coordinated bracket cancel before target resubmit.

---

## Closure Log

| ID | Block Closed | Reason |
|----|-------------|--------|
| DW-B134-OCO (main) | B135 | T2 implementation of TrySweptPttDragOrphans + CancelPttDragOrphansForAccount. |
| DW-B148 | B136 | B136-T1: OrderPassesBracketGate fused guard deployed. VERIFY_PASS issued. |
| DW-B146 | B136 | Consequential closure via DW-B148. MatchesLeaderName now reachable for all ATM-path PTT-prefix orders. |
| DW-B147 | B137 | T2: IsNoPriceChange guard in SyncAtmFollowerTarget (L2449) + SyncAtmFollowerBracket (L2341). |
| DW-B149 | B137 | T2: IsNoPriceChange guard (same fix as DW-B147). ChangeSubmitted race second TP3-HBC suppressed. |
| DW-B150 | B137 | T3: OrderPassesBracketGate condition `!string.IsNullOrEmpty(signalName)` at L2812. |
| DW-B151 | B137 | T4: CancelExistingPttStpDrag at L2396-2416; call in SyncAtmFollowerBracket at L2344. |
| DW-B152-B | B139 | T1: IsPttStpDragCancellable predicate (L2395-2400) + CancelExistingPttStpDrag refactor (L2407-2433). T2: 7 xUnit tests (B139Tests.cs). VERIFY_PASS both tickets. |

---

## Summary

| ID | Title | Priority | Target Block | Status |
|----|-------|----------|--------------|--------|
| DW-B147 | rawPrice==newPrice early-return guard | P2 | B137 | **CLOSED** |
| DW-B149 | ChangeSubmitted race second TP3-HBC | P1 | B137 | **CLOSED** |
| DW-B150 | OrderPassesBracketGate empty-string fo=NULL | P1 | B137 | **CLOSED** |
| DW-B151 | SyncAtmFollowerBracket missing Block A-Prime | P1 | B137 | **CLOSED** |
| DW-B152-B | Cancel-in-flight race -- CancelPending/CancelSubmitted gap | P1 | B139 | **CLOSED** |
| DW-B141 | Phase C re-confirmation -- pending SIM Test A | P1 | B135 SIM | OPEN |
| DW-B138 | Stop drag confirmed -- pending SIM Test B | P1 | B135 SIM | OPEN |
| B135-DEFER-01 | Gap B -- two simultaneous entries | P1 | B138+ | OPEN |
| B135-DEFER-02 | Stale orders multi-session | P2 | future | OPEN |
| DW-B134-OCO-OBS | OBS-A/B/C/D partial-fill race conditions | P1 | future | OPEN |

*1 item CLOSED this block (DW-B152-B).
5 items remain open/deferred. All require director SIM data or future block implementation.
DW-B134-OCO (main), DW-B148, DW-B146, DW-B147, DW-B149, DW-B150, DW-B151 closed in prior blocks (carried for history).*

---

*Produced by ptt-plan-reviewer, B139 Phase 5. Required gate artifact for FINAL_PASS.*
