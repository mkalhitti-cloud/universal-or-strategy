# B137 Deferred Backlog

**Block**: B137
**Block Header**: B137 Deferred Items
**Produced by**: ptt-plan-reviewer (Phase 5)
**Prior backlog**: `docs/brain/B136/06-deferred-backlog.md`
**Date**: 2026-09-08

---

## Status Changes From B136

| ID | B136 Status | B137 Status | Change |
|----|-------------|-------------|--------|
| DW-B147 | DEFERRED (P2) | **CLOSED** | B137-T2 implemented IsNoPriceChange guard in both SyncAtmFollowerTarget (L2449) and SyncAtmFollowerBracket (L2341). Guard fires when `fo.LimitPrice == newPrice` (target) or `fo.StopPrice == newPrice` (stop). ARM event spurious cancel+resubmit suppressed. VERIFY_PASS issued. |
| DW-B149 | (implicit, not listed separately in B136) | **CLOSED** | Same IsNoPriceChange fix (T2) suppresses the ChangeSubmitted race second TP3-HBC at same rawPrice. Both DW-B147 and DW-B149 share the same root class (rawPrice==newPrice scenario) and are closed by the same guard. |
| DW-B150 | NEW (identified B137 plan) | **CLOSED** | B137-T3 implemented condition fix: `OrderPassesBracketGate` branch (1) changed from `if (signalName != null)` to `if (!string.IsNullOrEmpty(signalName))` at L2812. Empty string signalName now routes to ATM path (MatchesLeaderName), enabling Stop3 identification for Sim103/Sim104 on first stop drag. VERIFY_PASS issued. |
| DW-B151 | NEW (identified B137 plan) | **CLOSED** | B137-T4 extracted `CancelExistingPttStpDrag(Account acc, Order fo)` helper and called it from `SyncAtmFollowerBracket` at L2344 (before Block A). Prevents accumulation of Working/Accepted PTT-STP-Drag orders on repeated stop drag events. Mirrors SyncAtmFollowerTarget A-Prime pattern. VERIFY_PASS issued. |
| DW-B141 | OPEN (awaiting SIM Test A) | OPEN (awaiting SIM Test A) | No change. SIM Test A not yet run. B137 T1 extracted Phase C to `ExecutePhaseCStopReplacement` (structural refactor, zero behavior change) -- Phase C code path is structurally unchanged. |
| DW-B138 | OPEN (awaiting SIM Test B) | OPEN (awaiting SIM Test B) | No change. SIM Test B not yet run. |
| B135-DEFER-01 | OPEN (P1) | OPEN (P1) | No change. |
| B135-DEFER-02 | OPEN (P2) | OPEN (P2) | No change. |
| DW-B134-OCO-OBS | OPEN (OBS-A/B/C/D) | OPEN (OBS-A/B/C/D) | No change. |

---

## New Closures -- B137

---

### DW-B147 -- rawPrice==newPrice Early-Return Guard

| Field | Value |
|-------|-------|
| **ID** | DW-B147 |
| **Title** | SyncAtmFollowerBracket/SyncAtmFollowerTarget rawPrice==newPrice early-return guard |
| **Status** | **CLOSED** |
| **Priority** | P2 (elevated to P1 context by DW-B149 co-fix) |
| **Closed in Block** | B137 |
| **Closed by** | T2: `IsNoPriceChange` helper + guards in both sync methods |

**Closure evidence**:
- `IsNoPriceChange(double currentPrice, double newPrice)` added at CopyEngine.cs L2783-2784 (CYC=1).
- Guard in `SyncAtmFollowerTarget` at L2449: `if (IsNoPriceChange(fo.LimitPrice, newPrice)) return;`
- Guard in `SyncAtmFollowerBracket` at L2341: `if (IsNoPriceChange(fo.StopPrice, newPrice)) return;`
- T2 VERIFY_PASS issued (ticket-2-verification.md).
- `IsNoPriceChangeTestable` seam at L2787-2788. T_B137_01/02 PASS.

---

### DW-B149 -- ChangeSubmitted Race Second TP3-HBC

| Field | Value |
|-------|-------|
| **ID** | DW-B149 |
| **Title** | ChangeSubmitted race -- second TP3-HBC at same rawPrice triggers spurious cancel+resubmit |
| **Status** | **CLOSED** |
| **Priority** | P1 |
| **Closed in Block** | B137 |
| **Closed by** | T2: `IsNoPriceChange` guard (same fix as DW-B147) |

**Closure evidence**: Same `IsNoPriceChange` guard at L2341 (SyncAtmFollowerBracket) and L2449
(SyncAtmFollowerTarget) suppresses the second TP3-HBC event when `Accepted→Working` transition fires
with the same rawPrice as the first event. T2 VERIFY_PASS issued.

---

### DW-B150 -- OrderPassesBracketGate Empty-String signalName (Sim103/Sim104 fo=NULL)

| Field | Value |
|-------|-------|
| **ID** | DW-B150 |
| **Title** | OrderPassesBracketGate empty-string signalName takes signal path -- fo=NULL on first stop drag for accounts with no prior PTT-STP-Drag |
| **Status** | **CLOSED** |
| **Priority** | P1 |
| **Closed in Block** | B137 |
| **Closed by** | T3: condition change in `OrderPassesBracketGate` branch (1) |

**Closure evidence**:
- CopyEngine.cs L2812: `if (!string.IsNullOrEmpty(signalName))` (was: `if (signalName != null)`)
- Empty string signalName now routes to ATM path → `MatchesLeaderName` → "Stop3" found.
- T3 VERIFY_PASS issued (ticket-3-verification.md).
- T_B137_06: PASS (`signalName=""` → ATM path → `SignalPathTaken("") = false`).
- T_B137_09: PASS (`signalName=null` → ATM path regression confirmed).

**Root cause**: NT8 sets `leaderOrder.FromEntrySignal = ""` (empty string, non-null) on ATM bracket
state-transition events. Pre-B137 condition `signalName != null` evaluated TRUE for `""`, taking the
signal-exclusive path. Follower's original ATM "Stop3" bracket has `FromEntrySignal = null`. Comparison
`null == ""` returns FALSE → order filtered out → fo=NULL → SyncFollowerBracket returns early.

---

### DW-B151 -- SyncAtmFollowerBracket Missing Block A-Prime (Duplicate PTT-STP-Drag)

| Field | Value |
|-------|-------|
| **ID** | DW-B151 |
| **Title** | SyncAtmFollowerBracket missing Block A-Prime pre-sweep -- PTT-STP-Drag accumulates on repeated stop drags |
| **Status** | **CLOSED** |
| **Priority** | P1 |
| **Closed in Block** | B137 |
| **Closed by** | T4: `CancelExistingPttStpDrag` extraction + call in `SyncAtmFollowerBracket` |

**Closure evidence**:
- `CancelExistingPttStpDrag(Account acc, Order fo)` added at L2396-2416 (CYC=6-7).
- Call at L2344 in SyncAtmFollowerBracket (BEFORE Block A cancel of `fo`).
- Cancels any Working or Accepted PTT-STP-Drag for the same instrument before placing a new one.
- T4 VERIFY_PASS issued (ticket-4-verification.md).
- T_B137_07: PASS (Working filter validated).
- T_B137_08: PASS (Accepted filter validated).

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
run confirmed Phase B (target drag → follower sync) but did not conclusively exercise Phase C
(stop replacement leg). B137 T1 extracted Phase C to `ExecutePhaseCStopReplacement` (structural
refactor -- zero behavior change). The Phase C code path is unchanged; T1 only moves it to a named
method for CYC headroom. The SIM Test A obligation is unchanged.

**B137 impact**: T1 extraction is a pure structural refactor with ZERO behavior change. Phase C
still fires unconditionally at the end of SyncAtmFollowerTarget (L2514: `ExecutePhaseCStopReplacement`
call). SIM Test A procedure is unchanged.

**SIM Test A Procedure** (unchanged from B136):
1. Open leader + 1 follower in SIM. Enter position via ATM.
2. Drag leader target order far enough past current stop that the stop must relocate (Phase C trigger).
3. Observe follower: PTT-TGT-Drag should move to new target price AND PTT-STP-Drag should appear
   (or move) to new stop price.
4. If both: DW-B141 CLOSED. If PTT-STP-Drag absent or mispositioned: remains INCONCLUSIVE.

**Resolution Condition**: Director or engineer runs SIM Test A with B137 fix deployed.

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
B137 T3 fixes the DW-B150 empty-string signalName routing, which directly strengthens the
prerequisite chain for stop drag sync on Sim103/Sim104 first-drag scenarios. However, SIM Test B
(confirmed end-to-end stop drag sync) has not been run.

**B137 impact**: DW-B150 fix (T3) removes the fo=NULL barrier for first-drag accounts. DW-B151 fix
(T4) prevents PTT-STP-Drag accumulation on repeated drags. Both fixes strengthen the stop drag sync
path. SIM Test B is the final confirmation step.

**SIM Test B Procedure** (unchanged from B136):
1. Open leader + 1 follower in SIM. Enter position via ATM.
2. Drag leader stop bracket to a new price.
3. Observe follower: stop bracket should move to same price within 1 tick.
4. If sync occurs: DW-B138 CLOSED. If no sync: investigate guard chain.

**Resolution Condition**: Director runs SIM Test B with B137 fix deployed.

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

**B137 impact**: None. B137 does not touch the entry-copy path.

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

**B137 impact**: None. B137 fixes routing in `OrderPassesBracketGate` and adds pre-sweep in
`SyncAtmFollowerBracket`, but does not change `FindFollowerBracketOrder` iteration scope.

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

**Remaining open sub-observations** (unchanged from B136):

| Obs ID | Description | Why Not Closed |
|--------|-------------|----------------|
| OBS-A | Cancel races partial fill -- acc.Cancel may be rejected with ErrorCode.UnableToCancelOrder after partial fill | T2 absorbs error via try/catch; does not prevent partial-fill race window |
| OBS-B | Replacement order duplicates partially-filled quantity -- follower over-positioned | T2 sweep is post-flat; pre-flat partial-fill state unaddressed |
| OBS-C | Stop side not cancelled before target replacement -- brief unhedged position window | T2 sweep is post-flat; pre-flat bracket ordering unaddressed |
| OBS-D | Net position drift on two-leg partial fill -- follower bracket position diverges from leader | Requires quantity-aware cancel guard in SyncAtmFollowerTarget; out of scope |

**B137 impact**: None. B137 fixes bracket sync routing (DW-B150) and pre-sweep (DW-B151) but does
not address partial-fill race conditions.

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

---

## Summary

| ID | Title | Priority | Target Block | Status |
|----|-------|----------|--------------|--------|
| DW-B147 | rawPrice==newPrice early-return guard | P2 | B137 | **CLOSED** |
| DW-B149 | ChangeSubmitted race second TP3-HBC | P1 | B137 | **CLOSED** |
| DW-B150 | OrderPassesBracketGate empty-string fo=NULL | P1 | B137 | **CLOSED** |
| DW-B151 | SyncAtmFollowerBracket missing Block A-Prime | P1 | B137 | **CLOSED** |
| DW-B141 | Phase C re-confirmation -- pending SIM Test A | P1 | B135 SIM | OPEN |
| DW-B138 | Stop drag confirmed -- pending SIM Test B | P1 | B135 SIM | OPEN |
| B135-DEFER-01 | Gap B -- two simultaneous entries | P1 | B138+ | OPEN |
| B135-DEFER-02 | Stale orders multi-session | P2 | future | OPEN |
| DW-B134-OCO-OBS | OBS-A/B/C/D partial-fill race conditions | P1 | future | OPEN |

*4 items CLOSED this block (DW-B147, DW-B149, DW-B150, DW-B151).
5 items remain open/deferred. All require director SIM data or future block implementation.
DW-B134-OCO (main), DW-B148, DW-B146 closed in prior blocks (carried for history).*

---

*Produced by ptt-plan-reviewer, B137 Phase 5. Required gate artifact for FINAL_PASS.*
