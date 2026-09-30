# B135 Deferred Backlog

**Block**: B135
**Block Header**: B135 Deferred Items
**Produced by**: ptt-plan-reviewer (Phase 5)
**Prior backlog**: `docs/brain/B134/06-deferred-backlog.md`
**Date**: 2026-09-07

---

## Status Changes From B134

| ID | B134 Status | B135 Status | Change |
|----|-------------|-------------|--------|
| DW-B134-OCO | DEFERRED -- awaiting SIM data | PARTIAL CLOSE | Flat-position sweep (main orphan cleanup) implemented by T2. OBS-A/B/C/D partial-fill race conditions remain OPEN. |
| B134-DEFER-01 | OPEN | OPEN (renamed B135-DEFER-01) | No change; carry-forward |
| B134-DEFER-02 | OPEN | OPEN (renamed B135-DEFER-02) | No change; carry-forward |
| DW-B141 | OPEN -- awaiting SIM Test A | OPEN | SIM Test A not yet run with B135 fix in place |
| DW-B138 | OPEN -- awaiting SIM Test B | OPEN | SIM Test B not yet run |

---

## Deferred Items

---

### DW-B147 -- rawPrice==newPrice Early-Return Guard

| Field | Value |
|-------|-------|
| **ID** | DW-B147 |
| **Title** | SyncAtmFollowerBracket/SyncAtmFollowerTarget rawPrice==newPrice early-return guard |
| **Status** | DEFERRED |
| **Priority** | P2 |
| **Target Block** | B136+ |
| **Root Block** | B135 (first evaluation) |

**Description**: `SyncAtmFollowerBracket` (CYC=4) and `SyncAtmFollowerTarget` (CYC=8, AT LIMIT) both
execute a cancel+resubmit sequence even when `rawPrice == newPrice` (i.e., no price change occurred
on the leader side). Adding a no-op early-return guard (`if (rawPrice == newPrice) return;`) would
eliminate redundant order cancel/resubmit cycles.

**Reason for Deferral**: `SyncAtmFollowerTarget` is already at CYC=8 (AT LIMIT per JS ceiling).
Adding the inline guard would push it to CYC=9 -- a P1 FAIL. To implement symmetrically across both
methods, a helper extraction (e.g., `IsNoPriceChange(double rawPrice, double newPrice, double tickSize)`)
would be required first. This is out of proportion for a P2 optimization with no confirmed
duplicate-sync symptom observed in SIM data.

**Resolution Condition**: Future block (B136+) extracts `IsNoPriceChange` helper from
`SyncAtmFollowerTarget` (reducing it from CYC=8), then applies the guard to both methods cleanly.

**Reference**: plan §E.

---

### DW-B141 -- Phase C Re-Confirmation (Pending SIM Test A)

| Field | Value |
|-------|-------|
| **ID** | DW-B141 |
| **Title** | SyncAtmFollowerTarget Phase C operable -- pending SIM Test A confirmation |
| **Status** | OPEN (awaiting SIM run) |
| **Priority** | P1 |
| **Target Block** | B135 SIM |
| **Root Block** | Pre-B134 (open since DW-B141 creation) |

**Description**: Phase C of `SyncAtmFollowerTarget` is the stop-replacement sub-path triggered when
a target drag moves the target far enough that the leader stop must be replaced. The B134 SIM PARTIAL
run confirmed Phase B (target drag → follower sync) but did not conclusively exercise Phase C
(stop replacement leg). With the B135 T1 fix in place, the second drag now correctly identifies
`PTT-TGT-Drag` as the follower target, which is a prerequisite for Phase C to reach the stop
replacement code.

**SIM Test A Procedure**:
1. Open leader + 1 follower in SIM. Enter position via ATM.
2. Drag leader target order far enough past current stop that the stop must relocate (Phase C trigger).
3. Observe follower: PTT-TGT-Drag should move to new target price AND PTT-STP-Drag should appear
   (or move) to new stop price.
4. If both: DW-B141 CLOSED. If PTT-STP-Drag absent or mispositioned: remains INCONCLUSIVE.

**Resolution Condition**: Director or engineer runs SIM Test A with B135 fix; reviewer marks CLOSED
upon confirmed Phase C stop sync.

---

### DW-B138 -- Follower Stop Drag Confirmed (Pending SIM Test B)

| Field | Value |
|-------|-------|
| **ID** | DW-B138 |
| **Title** | Follower stop leg drag sync confirmed -- pending SIM Test B director confirmation |
| **Status** | OPEN (awaiting SIM run) |
| **Priority** | P1 |
| **Target Block** | B135 SIM |
| **Root Block** | B131 (DW-B138 created; leaderName param added to FindFollowerBracketOrder) |

**Description**: DW-B138 (B131) added the `leaderName` parameter to `FindFollowerBracketOrder` to
enable ATM Name-based stop bracket identification when `fromEntrySignalName` is null/empty. The B134
changes (DW-B144 Submitted state + DW-B145 leaderName exact guard) depended on this fallback. The
B135 T1 fix extends it further (MatchesLeaderName handles PTT-STP-Drag as a valid stop bracket name).

SIM Test B would confirm that a stop drag on the leader side produces the correct `fo` (either the
ATM stop bracket or PTT-STP-Drag) via the updated guard chain, and successfully cancels+resubmits
the follower stop leg at the new price.

**SIM Test B Procedure**:
1. Open leader + 1 follower in SIM. Enter position via ATM.
2. Drag leader stop bracket to a new price.
3. Observe follower: stop bracket should move to same price within 1 tick.
4. If sync occurs: DW-B138 CLOSED. If no sync: investigate guard chain.

**Resolution Condition**: Director runs SIM Test B; confirms follower stop syncs correctly.

---

### B135-DEFER-01 -- Gap B Runtime (Two Simultaneous Entries)

| Field | Value |
|-------|-------|
| **ID** | B135-DEFER-01 |
| **Title** | Gap B -- two simultaneous leader entries, cancel first, verify 2nd copied |
| **Status** | OPEN |
| **Priority** | P1 |
| **Target Block** | B136+ |
| **Root Block** | B133 (originally B133-DEFER-01, carried through B134-DEFER-01) |

**Description**: If two leader entry signals fire in rapid succession (before the first follower copy
completes), the copy engine may cancel the first copy attempt and start a second. The second copy
may fail to find a valid follower entry if the first partial state was not fully cleaned up. This is
a runtime race condition that cannot be confirmed without SIM data showing two near-simultaneous
entry triggers.

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
| **Root Block** | B133 (originally B133-DEFER-02, carried through B134-DEFER-02) |

**Description**: `FindFollowerBracketOrder` iterates `follower.Orders` which may, in certain NT8
reconnect scenarios, contain Working/Accepted orders from a prior trading session. These stale orders
could incorrectly match the filter and return as a valid `fo`, causing a cancel+resubmit sequence on
a prior-session bracket.

Risk is LOW under normal trading hours (NT8 clears orders on disconnect), but has not been confirmed
empirically across a reconnect cycle. The B135 MatchesLeaderName fix does not change this exposure.

**Resolution Condition**: Director or engineer confirms whether `follower.Orders` is cleared on NT8
disconnect/reconnect. If not cleared: implement a session-epoch guard or timestamp filter in
`FindFollowerBracketOrder`.

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
`TrySweptPttDragOrphans` + `CancelPttDragOrphansForAccount`. This handles the case where a
follower's position goes flat (ATM natural fill or stop fire) and PTT-drag orders remain Working.

**Remaining open sub-observations** (NOT addressed by T2 sweep):

| Obs ID | Description | Why Not Closed By T2 |
|--------|-------------|----------------------|
| OBS-A | Cancel races partial fill -- acc.Cancel may be rejected with ErrorCode.UnableToCancelOrder after a partial fill already fired | T2 absorbs the error via try/catch, but does not prevent or detect the partial-fill race window itself |
| OBS-B | Replacement order duplicates partially-filled quantity -- follower over-positioned | T2 sweep fires only after flat; it does not address the pre-flat partial-fill state |
| OBS-C | Stop side not cancelled before target replacement -- brief unhedged position window | T2 sweep is post-flat; pre-flat bracket ordering is not addressed |
| OBS-D | Net position drift on two-leg partial fill -- follower bracket position diverges from leader | Requires quantity-aware cancel guard in SyncAtmFollowerTarget; out of T2 scope |

**Resolution Condition**: Each OBS requires SIM data showing the partial-fill race sequence. OBS-B
and OBS-D require quantity-aware guard in `SyncAtmFollowerTarget` Block A. OBS-C may require
coordinated bracket cancel before target resubmit.

---

## Closure Log

| ID | Block Closed | Reason |
|----|-------------|--------|
| DW-B134-OCO (main) | B135 | T2 implementation of TrySweptPttDragOrphans + CancelPttDragOrphansForAccount. Working PTT-TGT-Drag and PTT-STP-Drag swept on position flat. |

---

## Summary

| ID | Title | Priority | Target Block | Status |
|----|-------|----------|--------------|--------|
| DW-B147 | rawPrice==newPrice early-return guard | P2 | B136+ | DEFERRED |
| DW-B141 | Phase C re-confirmation -- pending SIM Test A | P1 | B135 SIM | OPEN |
| DW-B138 | Stop drag confirmed -- pending SIM Test B | P1 | B135 SIM | OPEN |
| B135-DEFER-01 | Gap B -- two simultaneous entries | P1 | B136+ | OPEN |
| B135-DEFER-02 | Stale orders multi-session | P2 | future | OPEN |
| DW-B134-OCO-OBS | OBS-A/B/C/D partial-fill race conditions | P1 | future | OPEN |

*6 open items. DW-B134-OCO (main) closed by B135 T2. All remaining items require director SIM data
or future block implementation.*

---

*Produced by ptt-plan-reviewer, B135 Phase 5. Required gate artifact for FINAL_PASS.*
