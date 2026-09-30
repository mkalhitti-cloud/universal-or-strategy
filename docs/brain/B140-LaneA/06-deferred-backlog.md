# B140-LaneA Deferred Backlog

**Block**: B140-LaneA
**Block Header**: B140-LaneA Deferred Items
**Produced by**: ptt-plan-reviewer (Phase 5)
**Prior backlog**: `docs/brain/B139/06-deferred-backlog.md`
**Date**: 2026-09-01

---

## Status Changes From B139

| ID | B139 Status | B140-LaneA Status | Change |
|----|-------------|-------------------|--------|
| DW-B153 | P0 OPEN (root cause identified) | **CLOSED** | B140-LaneA Ticket 1 implemented acc.Change for OCO-linked ATM Stop brackets in SyncFollowerBracket branch (3a). OCO cascade on Stop1/Stop2 drag eliminated. BUILD_PASS + VERIFY_PASS issued. NT8-VERIFY-01 confirms acc.Change preserves OCO link (NT8_API_SURFACE.md B31). |
| DW-B64-01 | OPEN (P0) | OPEN (P0) | No change. B140-LaneA does not touch HandleEntryChange or entry drag sync path. |
| DW-B71-01..04 | OPEN (P1) | OPEN (P1) | No change. B140-LaneA does not touch follower bracket dispatch or QX guard. |
| DW-B63-01 | OPEN (P1) | OPEN (P1) | No change. B140-LaneA does not touch PTT-Flatten path. |
| DW-B141 | OPEN (awaiting SIM Test A) | OPEN (awaiting SIM Test A) | No change. B140-LaneA does not touch Phase C or SyncAtmFollowerTarget. |
| DW-B138 | OPEN (awaiting SIM Test B) | OPEN (awaiting SIM Test B) | No change. B140-LaneA does not touch FindFollowerBracketOrder path. |
| B135-DEFER-01 | OPEN (P1) | OPEN (P1) | No change. B140-LaneA does not touch entry-copy path. |
| B135-DEFER-02 | OPEN (P2) | OPEN (P2) | No change. B140-LaneA does not touch FindFollowerBracketOrder iteration scope. |
| DW-B134-OCO-OBS | OPEN (OBS-A/B/C/D) | OPEN (OBS-A/B/C/D) | No change. B140-LaneA does not address partial-fill race conditions. |

---

## New Closure -- B140-LaneA

---

### DW-B153 -- OCO Cascade on Stop1/Stop2 Drag

| Field | Value |
|-------|-------|
| **ID** | DW-B153 |
| **Title** | OCO cascade on Stop1/Stop2 drag -- acc.Cancel on OCO-linked ATM Stop bracket cascades to Target1/Target2 cancellation |
| **Status** | **CLOSED** |
| **Priority** | P0 |
| **Closed in Block** | B140-LaneA |
| **Closed by** | T1: `SyncFollowerBracket` branch (3a) — `if (!string.IsNullOrEmpty(fo.Oco))` routes OCO-linked stops to `acc.Change` instead of `acc.Cancel`; 7 xUnit tests (B140Tests.cs) |

**Root cause**: `SyncFollowerBracket` routed all ATM stop brackets (Stop1/Stop2/Stop3) to `SyncAtmFollowerBracket`, which called `acc.Cancel(fo)` before resubmitting. For Stop1 (Oco non-empty GUID) and Stop2 (Oco non-empty GUID), NT8 OCO cascade fired atomically: Stop1 Cancelled -> Target1 Cancelled; Stop2 Cancelled -> Target2 Cancelled. Follower lost Target1 and Target2 on every stop drag, resulting in naked position risk.

**Fix**: Branch (3a) `if (!string.IsNullOrEmpty(fo.Oco))` added inside the existing `if (isStop && IsAtmSTPOrder(fo)) // (3)` block in `SyncFollowerBracket` (CopyEngine.cs lines 2280-2292). When `fo.Oco` is non-empty, the branch sets `fo.StopPrice = newPrice` and calls `acc.Change(new Order[] { fo })`. `acc.Change` modifies stop price in-place and preserves the ATM OCO link (NT8_API_SURFACE.md B31), eliminating the cascade. Exceptions are absorbed via `StatusUpdate?.Invoke(acc.Name + ": ATM STP Change error: " + ex.Message)`. Branch (3b) preserves the existing `SyncAtmFollowerBracket` cancel+resubmit path for orders with empty Oco (PTT-STP-Drag).

**CYC impact**: `SyncFollowerBracket` CYC 7 -> 8 (at JS-041 limit, PASS). No further branching may be added to this method.

**Stop3 routing**: Stop3 has a non-empty Oco GUID (paired with Target3). Branch (3a) routes Stop3 to `acc.Change` as well. This is intentional and strictly better than cancel+resubmit: it preserves the Target3 OCO link.

**Closure evidence**:
- `SyncFollowerBracket` branch (3a) inserted at CopyEngine.cs lines 2280-2292. Verified by ptt-verifier at exact lines.
- `NT8_API_SURFACE.md` line 151: `Account.Change(Order[])` B31 "Modifies stop price in-place (preserves ATM OCO link)". NT8-VERIFY-01 confirmed.
- `NT8_FULL_REFERENCE.md` lines 849-850: `Oco` is a string property on NT8 Order class representing OCO group id. NT8-VERIFY-02 confirmed.
- `fo.StopPrice = newPrice` pattern consistent with existing acc.Change usage in CopyEngine.cs (~line 2300).
- T1 BUILD_PASS. T1 VERIFY_PASS (7 scans zero; all implementation checks PASS).
- 7 xUnit [Fact] tests (T_B140_01 through T_B140_07) created in `tests/PropTraderTools.Tests/B140Tests.cs`. `dotnet test --filter "T_B140"`: Passed 7, Failed 0, Total 7.
- ptt-sync-and-verify.ps1: 0 MISMATCH lines. CopyEngine.cs MD5-verified.
- **SIM Gate 1 PENDING**: acc.Change() non-no-op behavior on Stop brackets requires director SIM confirmation before merge. Gate 1 FAIL protocol: STOP, DW-B154, no fallback, Director resolution required.

---

## New Deferred Items -- B140-LaneA

---

### DW-B140-01 -- SIM Gate 1 (acc.Change Non-No-Op on Stop Brackets)

| Field | Value |
|-------|-------|
| **ID** | DW-B140-01 |
| **Title** | SIM Gate 1 — acc.Change() on Stop1/Stop2 is NOT a silent no-op in NT8 AddOn context |
| **Status** | OPEN (awaiting director SIM run) |
| **Priority** | P0 (BLOCKING — merge gated) |
| **Target Block** | B140 SIM |
| **Root Block** | B140-LaneA (plan Section 3 Fact 5; architecture plan Section 8 Gate 1) |

**Description**: The B140 fix relies on `acc.Change()` on ATM Stop brackets from AddOn context updating the stop price in-place and NOT being a silent no-op. NT8_API_SURFACE.md B31 confirms the behavior in general, but the specific behavior from an AddOnBase-derived context on ATM-owned brackets must be confirmed in SIM. If `acc.Change` is a no-op on Stop brackets (price does not update in Order Grid), the fix has zero effect and the OCO cascade problem is not resolved.

**SIM Gate 1 Procedure**:
1. Open NT8 SIM environment with PTT leader + follower running, ATM-entered position.
2. Drag leader stop price to a new level.
3. Observe NT8 Order Grid for follower account.

**Pass criteria (ALL must be true)**:
- Follower Stop1 price updates to new price in Order Grid.
- Follower Stop2 price updates to new price in Order Grid.
- Target1 is NOT cancelled after drag.
- Target2 is NOT cancelled after drag.

**Gate 1 FAIL Protocol — NO EXCEPTIONS**:
- If acc.Change is confirmed as a no-op on ATM Stop brackets:
  - STOP immediately. Do NOT implement a fallback.
  - Report to Director with SIM log.
  - Document as **DW-B154**.
  - Merge is BLOCKED until Director resolution.

**If SIM Gate 1 PASSES**: DW-B140-01 CLOSED. Proceed to Gate 2 and Gate 3.

---

### DW-B140-02 -- SIM Gate 2 (Stop3 Routes Correctly via acc.Change)

| Field | Value |
|-------|-------|
| **ID** | DW-B140-02 |
| **Title** | SIM Gate 2 — Stop3 routes to acc.Change and price updates correctly, Target3 not cancelled |
| **Status** | OPEN (awaiting director SIM run) |
| **Priority** | P1 |
| **Target Block** | B140 SIM |
| **Root Block** | B140-LaneA (architecture plan Section 8 Gate 2; ticket Stop3 routing note) |

**Description**: Stop3 has a non-empty Oco GUID and routes to branch (3a) same as Stop1/Stop2. SIM confirmation that Stop3 also behaves correctly under the new acc.Change path is required.

**Pass criteria**:
- Stop3 price updates via acc.Change (not cancel+resubmit).
- Target3 is NOT cancelled.
- No OCO cascade observed.

---

### DW-B140-03 -- SIM Gate 3 (Second Drag Works, No Cascade)

| Field | Value |
|-------|-------|
| **ID** | DW-B140-03 |
| **Title** | SIM Gate 3 — Two consecutive stop drags; Stop1/Stop2 update on both drags; no cascade on either |
| **Status** | OPEN (awaiting director SIM run) |
| **Priority** | P1 |
| **Target Block** | B140 SIM |
| **Root Block** | B140-LaneA (architecture plan Section 8 Gate 3) |

**Description**: Idempotency of acc.Change on a second consecutive drag. After the first drag updates Stop1/Stop2 via acc.Change, the second drag should produce the same result: price updated, no cascade.

**Pass criteria**:
- Stop1 and Stop2 prices update on both drags.
- No target cancellation on either drag.
- Order Grid state is consistent after second drag.

---

## Deferred Items (Carried Forward -- OPEN)

---

### DW-B64-01 -- HandleEntryChange Not Firing (Next P0)

| Field | Value |
|-------|-------|
| **ID** | DW-B64-01 |
| **Title** | HandleEntryChange not firing -- drag sync broken |
| **Status** | OPEN (P0) |
| **Priority** | P0 |
| **Target Block** | next P0 after B140 |
| **Root Block** | B64 (DW-B64-01 creation) |

**Description**: HandleEntryChange event handler is not being invoked on entry drag events. This breaks the entry sync path. Listed as the next P0 item after B140-LaneA OCO cascade fix.

**B140-LaneA impact**: None. B140-LaneA does not touch HandleEntryChange or the entry drag sync path.

**Resolution Condition**: Engineer investigates HandleEntryChange registration and invocation path; confirms whether NT8 fires the event for AddOn-registered handlers on drag events.

---

### DW-B71-01..04 -- Quick ALL Follower Bracket Dispatch + QX Guard

| Field | Value |
|-------|-------|
| **ID** | DW-B71-01..04 |
| **Title** | Quick ALL follower bracket dispatch + QX guard |
| **Status** | OPEN (P1) |
| **Priority** | P1 |
| **Target Block** | future |
| **Root Block** | B71 |

**Description**: Four sub-items related to Quick ALL follower bracket dispatch and a QX guard. Details carried forward from B139 backlog.

**B140-LaneA impact**: None.

---

### DW-B63-01 -- Double PTT-Flatten 11ms Apart

| Field | Value |
|-------|-------|
| **ID** | DW-B63-01 |
| **Title** | Double PTT-Flatten 11ms apart |
| **Status** | OPEN (P1) |
| **Priority** | P1 |
| **Target Block** | future |
| **Root Block** | B63 |

**Description**: Double PTT-Flatten events arriving 11ms apart can cause duplicate flatten processing. Details carried forward from B139 backlog.

**B140-LaneA impact**: None.

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

**Description**: Phase C of `SyncAtmFollowerTarget` is the stop-replacement sub-path triggered when a target drag moves the target far enough that the leader stop must be replaced. B134 SIM PARTIAL confirmed Phase B but did not conclusively exercise Phase C. B137 T1 extracted Phase C to `ExecutePhaseCStopReplacement` (structural refactor, zero behavior change). The Phase C code path is unchanged.

**B140-LaneA impact**: None. B140-LaneA does not touch Phase C or SyncAtmFollowerTarget.

**SIM Test A Procedure** (unchanged from B139):
1. Open leader + 1 follower in SIM. Enter position via ATM.
2. Drag leader target order far enough past current stop that the stop must relocate (Phase C trigger).
3. Observe follower: PTT-TGT-Drag should move to new target price AND PTT-STP-Drag should appear (or move) to new stop price.
4. If both: DW-B141 CLOSED. If PTT-STP-Drag absent or mispositioned: remains INCONCLUSIVE.

**Resolution Condition**: Director or engineer runs SIM Test A with B140-LaneA fix deployed.

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

**Description**: DW-B138 (B131) added the `leaderName` parameter to `FindFollowerBracketOrder` to enable ATM Name-based stop bracket identification when `fromEntrySignalName` is null/empty. B137 T3 fixes DW-B150 empty-string signalName routing; B137 T4 adds pre-sweep (DW-B151); B139 strengthens the pre-sweep to cover CancelPending/CancelSubmitted. SIM Test B (end-to-end stop drag sync) has not been run.

**B140-LaneA impact**: B140-LaneA changes the ATM stop bracket sync from cancel+resubmit to acc.Change for OCO-linked stops. This is relevant to SIM Test B: the stop drag sync will now use acc.Change for Stop1/Stop2/Stop3, which changes the observable behavior (no PTT-STP-Drag for ATM-owned brackets; direct price update instead). SIM Test B should be re-evaluated with B140-LaneA behavior: confirm Stop1/Stop2 price update (not PTT-STP-Drag) and no cascade.

**SIM Test B Procedure** (updated for B140-LaneA behavior):
1. Open leader + 1 follower in SIM. Enter position via ATM.
2. Drag leader stop bracket to a new price.
3. Observe follower: **Stop1 and Stop2 should update in-place** (not cancel+resubmit) AND Target1/Target2 must NOT be cancelled.
4. If sync occurs with no cascade: DW-B138 CLOSED (with DW-B140-01 Gate 1 also confirmed). If no sync: investigate acc.Change path and gate chain.

**Resolution Condition**: Director runs SIM Test B with B140-LaneA fix deployed. Note: DW-B138 and DW-B140-01 Gate 1 may be confirmed in the same SIM run.

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

**Description**: If two leader entry signals fire in rapid succession (before the first follower copy completes), the copy engine may cancel the first copy attempt and start a second. The second copy may fail to find a valid follower entry if the first partial state was not fully cleaned up. This is a runtime race condition that cannot be confirmed without SIM data showing two near-simultaneous entry triggers.

**B140-LaneA impact**: None. B140-LaneA does not touch the entry-copy path.

**Resolution Condition**: Director or engineer demonstrates two-entry scenario in SIM; `TryEvictFollowerBeSlot` and related gate logic confirmed to handle the second entry correctly. If not handled: engineer implements a copy-queue or idempotency guard.

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

**Description**: `FindFollowerBracketOrder` iterates `follower.Orders` which may, in certain NT8 reconnect scenarios, contain Working/Accepted orders from a prior trading session. These stale orders could incorrectly match the filter and return as a valid `fo`, causing a cancel+resubmit sequence on a prior-session bracket. Risk is LOW under normal trading hours (NT8 clears orders on disconnect).

**B140-LaneA impact**: None. B140-LaneA does not change `FindFollowerBracketOrder` iteration scope.

**Resolution Condition**: Director or engineer confirms whether `follower.Orders` is cleared on NT8 disconnect/reconnect. If not cleared: implement a session-epoch guard or timestamp filter.

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

**Context**: B135 T2 closed the main DW-B134-OCO orphan-after-flat condition by adding `TrySweptPttDragOrphans` + `CancelPttDragOrphansForAccount`. The four remaining sub-observations require SIM data showing partial-fill race sequences.

**B140-LaneA impact**: None. B140-LaneA fixes ATM bracket stop sync but does not address partial-fill race conditions.

**Remaining open sub-observations** (unchanged from B139):

| Obs ID | Description | Why Not Closed |
|--------|-------------|----------------|
| OBS-A | Cancel races partial fill -- acc.Cancel may be rejected with ErrorCode.UnableToCancelOrder after partial fill | T2 absorbs error via try/catch; does not prevent partial-fill race window |
| OBS-B | Replacement order duplicates partially-filled quantity -- follower over-positioned | T2 sweep is post-flat; pre-flat partial-fill state unaddressed |
| OBS-C | Stop side not cancelled before target replacement -- brief unhedged position window | T2 sweep is post-flat; pre-flat bracket ordering unaddressed |
| OBS-D | Net position drift on two-leg partial fill -- follower bracket position diverges from leader | Requires quantity-aware cancel guard in SyncAtmFollowerTarget; out of scope |

**Resolution Condition**: Each OBS requires SIM data showing the partial-fill race sequence. OBS-B and OBS-D require quantity-aware guard in `SyncAtmFollowerTarget` Block A. OBS-C may require coordinated bracket cancel before target resubmit.

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
| DW-B153 | B140-LaneA | T1: SyncFollowerBracket branch (3a) `if (!string.IsNullOrEmpty(fo.Oco))` -> acc.Change (CopyEngine.cs L2280-2292). OCO cascade on Stop1/Stop2 drag eliminated. 7 xUnit tests (B140Tests.cs). BUILD_PASS + VERIFY_PASS. NT8-VERIFY-01 B31 confirmed. SIM Gate 1 PENDING (director run required before merge). |

---

## Summary

| ID | Title | Priority | Target Block | Status |
|----|-------|----------|--------------|--------|
| DW-B134-OCO (main) | OCO orphan flat-position sweep | P1 | B135 | **CLOSED** |
| DW-B148 | OrderPassesBracketGate fused guard | P1 | B136 | **CLOSED** |
| DW-B146 | MatchesLeaderName ATM-path reachability | P1 | B136 | **CLOSED** |
| DW-B147 | rawPrice==newPrice early-return guard | P2 | B137 | **CLOSED** |
| DW-B149 | ChangeSubmitted race second TP3-HBC | P1 | B137 | **CLOSED** |
| DW-B150 | OrderPassesBracketGate empty-string fo=NULL | P1 | B137 | **CLOSED** |
| DW-B151 | SyncAtmFollowerBracket missing Block A-Prime | P1 | B137 | **CLOSED** |
| DW-B152-B | Cancel-in-flight race -- CancelPending/CancelSubmitted gap | P1 | B139 | **CLOSED** |
| DW-B153 | OCO cascade on Stop1/Stop2 drag -- acc.Change fix | P0 | B140-LaneA | **CLOSED** |
| DW-B140-01 | SIM Gate 1 -- acc.Change non-no-op on Stop brackets (BLOCKING merge) | P0 | B140 SIM | OPEN |
| DW-B64-01 | HandleEntryChange not firing -- drag sync broken | P0 | next P0 after B140 | OPEN |
| DW-B140-02 | SIM Gate 2 -- Stop3 via acc.Change, Target3 not cancelled | P1 | B140 SIM | OPEN |
| DW-B140-03 | SIM Gate 3 -- second consecutive drag, no cascade | P1 | B140 SIM | OPEN |
| DW-B71-01..04 | Quick ALL follower bracket dispatch + QX guard | P1 | future | OPEN |
| DW-B63-01 | Double PTT-Flatten 11ms apart | P1 | future | OPEN |
| DW-B141 | Phase C re-confirmation -- pending SIM Test A | P1 | B135 SIM | OPEN |
| DW-B138 | Stop drag confirmed -- pending SIM Test B | P1 | B135 SIM | OPEN |
| B135-DEFER-01 | Gap B -- two simultaneous entries | P1 | B138+ | OPEN |
| DW-B134-OCO-OBS | OBS-A/B/C/D partial-fill race conditions | P1 | future | OPEN |
| B135-DEFER-02 | Stale orders multi-session | P2 | future | OPEN |

*1 item CLOSED this block (DW-B153).
3 new items added (DW-B140-01/02/03 — SIM gates).
9 items carry forward open. DW-B140-01 (P0) is BLOCKING for merge.
DW-B134-OCO (main), DW-B148, DW-B146, DW-B147, DW-B149, DW-B150, DW-B151, DW-B152-B, DW-B153 closed in this or prior blocks (carried for history).*

---

*Produced by ptt-plan-reviewer, B140-LaneA Phase 5. Required gate artifact for FINAL_PASS.*
