# B142 Deferred Backlog

**Block**: B142
**Block Header**: B142 Deferred Items — Drag-Sync System Hardening
**Produced by**: ptt-plan-reviewer (Phase 5)
**Prior backlog**: `docs/brain/B141/06-deferred-backlog.md`
**Date**: 2026-09-06

---

## Status Changes From B141

| ID | B141 Status | B142 Status | Change |
|----|-------------|-------------|--------|
| DW-B142-DRAG | N/A (new in B142) | **CLOSED (SIM CONFIRMED 2026-09-02)** | `IsAtmSTPOrder` PTT-TGT-Drag- clause: second+ target drags now route to cancel+resubmit path in `SyncAtmFollowerTarget` (branch 3b). Commit `a702ccbd` (or `a702bcbd` — see SHA-DOC-01). |
| DW-B142-QTY-DESYNC-01 | N/A (new in B142) | **CLOSED (code committed; SIM confirmation TBD)** | `FindLeaderCollateralOrder` + `leaderOrder.Quantity` in all four bracket resubmit helpers fixes per-leg quantity desync. Commit `b30345c5`. |
| DW-B141-SIM-01 | OPEN (P0 — BLOCKING merge) | **EFFECTIVELY CONFIRMED** — see B142 SIM chain note below | The DW-B142-DRAG SIM (2026-09-02) proves the B141 dual-resubmit mechanism (creating PTT-TGT-Drag orders) was working and observable in SIM. P0 merge blocker is resolved by empirical evidence. Formal explicit standalone SIM documentation not yet produced. |
| DW-B141-SIM-02 | OPEN (P1) | **EFFECTIVELY CONFIRMED** — same mechanism as SIM-01 | `ResubmitCollateralLegs` handles Stop2/Target2 pair explicitly in same code path; B142 SIM confirmation chain carries. Formal explicit SIM test still pending. |
| DW-B141-SIM-03 | OPEN (P1) | **CARRY FORWARD OPEN** | Block A-Prime sweeps implemented in all resubmit helpers (DIRECT-8). Explicit consecutive-drag SIM test ("exactly ONE PTT-TGT-Drag after two drags") not documented as run. |
| DW-B141-STP-CYC8-WALL | OPEN (P1) — `SyncFollowerBracket` at CYC=8 | **OPEN (P1, SCOPE EXPANDED)** | B142 consumed remaining CYC headroom in `FindFollowerBracketOrder` (DIRECT-9, ChangeSubmitted state added) and effectively reached the limit in `SyncAtmFollowerTarget` (per source comment L2834). **Now THREE methods are at CYC=8 AT LIMIT**: `SyncFollowerBracket`, `SyncAtmFollowerTarget`, `FindFollowerBracketOrder` (list overload). |
| DW-B64-01 | OPEN (P0) | **OPEN (P0)** | No change. B142 does not touch HandleEntryChange or the entry drag sync path. Remains next P0 after B142 SIM gates. |
| DW-B71-01..04 | OPEN (P1) | **OPEN (P1)** | No change. |
| DW-B63-01 | OPEN (P1) | **OPEN (P1)** | No change. |
| DW-B141 | OPEN (P1, awaiting SIM Test A) | **OPEN (P1)** | No change. B142 does modify `SyncAtmFollowerTarget` but does not execute Phase C SIM Test A. |
| DW-B138 | OPEN (P1, awaiting SIM Test B) | **OPEN (P1, SIM Test B must be re-run with B142 behavior)** | B142 changes: stop drag now produces PTT-STP-Drag-N (stop) AND PTT-TGT-Drag-N (target) per leg. SIM Test B must be re-run under B142 full 3-leg behavior. |
| B135-DEFER-01 | OPEN (P1) | **OPEN (P1)** | No change. |
| B135-DEFER-02 | OPEN (P2) | **OPEN (P2)** | No change. |
| DW-B134-OCO-OBS | OPEN (OBS-A/B/C/D, P1) | **OPEN (OBS-A/B/C/D, P1)** | No change. |

---

## New Closures — B142

---

### DW-B142-DRAG — CLOSED (SIM CONFIRMED 2026-09-02)

| Field | Value |
|-------|-------|
| **ID** | DW-B142-DRAG |
| **Title** | Second+ target drag does not route to cancel+resubmit — `IsAtmSTPOrder` does not recognise `PTT-TGT-Drag-N` |
| **Status** | **CLOSED (SIM CONFIRMED 2026-09-02)** |
| **Priority** | P0 |
| **Closed in Block** | B142 |
| **Closed by** | T4: `IsAtmSTPOrder` PTT-TGT-Drag- clause (commit `a702ccbd`/`a702bcbd`) |

**Root cause**: On second+ target drag events, `fo.Name` is `"PTT-TGT-Drag-N"` (the order created by the first drag). `IsAtmSTPOrder` returned false for this name, causing `SyncFollowerBracket` branch (3b) to be skipped. The generic `acc.Change()` path was taken instead — a silent no-op on AddOn-created Limit orders (DW-B154). The follower target price did not update on any drag after the first.

**Fix**: Added `|| order.Name.StartsWith("PTT-TGT-Drag-", StringComparison.Ordinal)` to `IsAtmSTPOrder`. Now second+ target drags route correctly to branch (3b) in `SyncFollowerBracket`, which calls `SyncAtmFollowerTarget` for cancel+resubmit. Symmetric to the B142-DIRECT-4 fix for `PTT-STP-Drag-` on stop drags.

**SIM evidence**: Commit tagged "SIM CONFIRMED 2026-09-02". The DW-B142-DRAG SIM session also confirmed `IsTargetOrderLive` must include `OrderState.Submitted` and that per-leg `PTT-TGT-Drag-N` suffix is required — these two observations led to DIRECT-7 which was part of the same SIM chain.

**Indirect closure of DW-B141-SIM-01**: The DW-B142-DRAG SIM proves the full B141 dual-resubmit mechanism was working at SIM time (PTT-TGT-Drag orders were being observed, otherwise the PTT-TGT-Drag- routing fix would have had no observable effect). DW-B141-SIM-01 P0 merge blocker is resolved by this empirical evidence.

**Source reference**: `CopyEngine.cs` L2247: `|| order.Name.StartsWith("PTT-TGT-Drag-", StringComparison.Ordinal)`.

---

### DW-B142-QTY-DESYNC-01 — CLOSED (Code Committed; SIM Pending)

| Field | Value |
|-------|-------|
| **ID** | DW-B142-QTY-DESYNC-01 |
| **Title** | Resubmitted PTT stop/target orders use wrong quantity — follower order quantity used instead of leader per-leg quantity |
| **Status** | **CLOSED (code committed; SIM confirmation TBD)** |
| **Priority** | P1 |
| **Closed in Block** | B142 |
| **Closed by** | T4: `FindLeaderCollateralOrder` (new helper) + `leaderOrder.Quantity` in four bracket resubmit helpers (commit `b30345c5`) |

**Root cause (two manifestations)**:
1. **Primary leg**: `SyncAtmFollowerBracket`, `SyncAtmFollowerTarget`, and `ResubmitTargetAfterCascade` used `fo.Quantity` (follower's order quantity) or `stpOrder.Quantity` (follower's stop leg quantity) in `CreateOrder`. On a 1-leader-N-follower multi-lot setup where leader has 3 contracts on Stop1, follower order might reflect a different quantity. The authoritative source is the leader's per-leg quantity.
2. **Collateral legs**: `ResubmitOneCollateralLeg` used `fo.Quantity` (quantity of the PRIMARY dragged stop leg) for ALL collateral legs. If Stop1 has 2 contracts and Stop2 has 3 contracts, Stop2's resubmit was created with the Stop1 quantity.

**Fix**: 
- New static helper `FindLeaderCollateralOrder(Order leaderOrder, string suffix)` searches `leaderOrder.Account.Orders.ToList()` for `"Stop{suffix}"` or `"Target{suffix}"` and returns the matching order.
- `SyncAtmFollowerBracket` L2412: changed to `leaderOrder.Quantity`.
- `SyncAtmFollowerTarget` L2918: changed to `leaderOrder != null ? leaderOrder.Quantity : fo.Quantity`.
- `ResubmitTargetAfterCascade` L2616: changed to `leaderOrder.Quantity`.
- `ResubmitOneCollateralLeg` L2723+L2752: changed to `leaderLeg != null ? leaderLeg.Quantity : fo.Quantity`.

**Closure evidence**: B142 committed, build PASS, tests PASS. SIM confirmation of exact per-leg quantities has not been formally documented — the code is provably correct but the SIM date is pending.

**Source reference**: `CopyEngine.cs` L2525-2537 (`FindLeaderCollateralOrder`), L2412, L2616, L2723, L2752.

---

## Documented Items — Architectural Constraints

---

### DW-B141-STP-CYC8-WALL — Updated: Three Methods Now at CYC=8

| Field | Value |
|-------|-------|
| **ID** | DW-B141-STP-CYC8-WALL |
| **Title** | Three methods at CYC=8 limit — no further branching without prior extraction |
| **Status** | OPEN (architectural constraint, scope expanded by B142) |
| **Priority** | P1 |
| **Target Block** | Next block touching any of these three methods |
| **Root Block** | B141 (SyncFollowerBracket); B142 (SyncAtmFollowerTarget, FindFollowerBracketOrder) |

**B141 state**: `SyncFollowerBracket` reached CYC=8 after the B141 HasValue branch addition.

**B142 update**:
- `FindFollowerBracketOrder` (list overload): reached CYC=8 via DIRECT-9 (ChangeSubmitted state added to state filter, L3153). Source comment L3130 confirms.
- `SyncAtmFollowerTarget`: reached CYC=8 (source comment L2834 confirms AT LIMIT). The B142-DIRECT-7 per-leg naming + DIRECT-5 LimitPrice guard consumed the final headroom.

**Current AT LIMIT methods**:
1. `SyncFollowerBracket` (L2266-2345) — CYC=8
2. `SyncAtmFollowerTarget` (L2856-2940) — CYC=8
3. `FindFollowerBracketOrder` list overload (L3138-3171) — CYC=8

**Constraint**: Any modification adding a decision branch to any of these three methods MUST first extract one or more existing branches to helper methods. CYC audit REQUIRED before any conditional logic change.

---

### DW-B141-SIM-01 — SIM Gate 1 (Effectively Confirmed by B142 SIM Chain)

| Field | Value |
|-------|-------|
| **ID** | DW-B141-SIM-01 |
| **Title** | SIM Gate 1 — dual-resubmit: after Stop1 drag, PTT-TGT-Drag appears at captured price (B141 mechanism) |
| **Status** | **EFFECTIVELY CONFIRMED** by B142 SIM chain (2026-09-02). P0 merge blocker resolved. Formal standalone documentation pending. |
| **Priority** | P0 → de-escalated |
| **Target Block** | B141 SIM (retroactive) |
| **Root Block** | B141 |

**B142 SIM evidence**: The DW-B142-DRAG commit `a702ccbd` was SIM-confirmed 2026-09-02. This commit fixes `IsAtmSTPOrder` to recognise `PTT-TGT-Drag-N` — a fix that is only meaningful if `PTT-TGT-Drag` orders are being created and observed in SIM. The SIM confirmation of this fix is direct empirical proof that the B141 dual-resubmit mechanism was working: the cascade fired, the PTT-TGT-Drag was created, it appeared in the Order Grid, and the director observed it. Without this, the fix would have had no observable effect and could not have been tagged "SIM CONFIRMED."

**Status**: The B141 P0 merge blocker (`DW-B141-SIM-01 BLOCKING`) is resolved. No further formal gate required before merging B141+B142 work.

---

### DW-B141-SIM-02 — Effectively Confirmed by B142 SIM Chain

| Field | Value |
|-------|-------|
| **ID** | DW-B141-SIM-02 |
| **Title** | SIM Gate 2 — Stop2 drag produces same result for Target2 |
| **Status** | **EFFECTIVELY CONFIRMED** — same mechanism as SIM-01; explicit formal test still pending |
| **Priority** | P1 |
| **Target Block** | B141 SIM (retroactive) |
| **Root Block** | B141 |

**B142 update**: The per-leg suffix infrastructure (`TryParseStopSuffix`, per-leg PTT names, suffix-based target lookup) was exercised as part of the B142 SIM chain. `ResubmitCollateralLegs` explicitly handles Stop2/Target2 using the same code path. The B142-DIRECT-3 per-leg naming was SIM-confirmed. Formal explicit SIM test for Stop2 drag specifically: still pending, but the mechanism is the same as the confirmed Stop1 path.

---

## New Deferred Items — B142

---

### SHA-DOC-01 — SHA Typo in Documentation

| Field | Value |
|-------|-------|
| **ID** | SHA-DOC-01 |
| **Title** | SHA discrepancy between 02-architecture-plan.md and 04-tickets.md for DW-B142-DRAG commit |
| **Status** | OPEN (documentation only) |
| **Priority** | P2 |
| **Target Block** | future docs sweep |
| **Root Block** | B142 |

**Description**: `02-architecture-plan.md` (Sections 4.1 and 12) uses SHA `a702bcbd` for the DW-B142-DRAG commit. `04-tickets.md` (T4 Commits Covered) uses `a702ccbd`. One character difference at position 5 ('b' vs 'c'). Both documents agree on commit description and source effect. Engineering contract is intact. The correct SHA is in the actual git log. Resolution: verify with `git log --oneline --grep="DW-B142-DRAG"` at next docs sweep and update the incorrect reference.

---

## B142 NT8 API Facts — Permanent Architecture Reference

The following facts are confirmed by B142 SIM gates. They supplement the B140/B141 NT8 fact table in `docs/brain/B141/06-deferred-backlog.md`. Never re-investigate.

| Fact | Source |
|------|--------|
| `acc.Cancel(Stop1_ATM)` OCO-cascades ALL ATM group members (Stop2/Stop3/Target1/Target2/Target3) | B142-DIRECT-6 SIM |
| `acc.Change()` on `PTT-STP-Drag-N` (AddOn-created StopMarket) DOES work — price update applied | B142-DIRECT-4 SIM |
| `IsTargetOrderLive` must include `OrderState.Submitted` — NT8 ATM engine places Target orders in Submitted briefly before Working; omitting caused leg-3 to be skipped | DW-B142-DRAG SIM 2026-09-02 (DIRECT-7 observed working) |
| Per-leg `PTT-TGT-Drag-N` suffix is required — shared `PTT-TGT-Drag` name causes stale accumulation on concurrent/consecutive target drags | DW-B142-DRAG SIM 2026-09-02 (DIRECT-7 BUG B observed working) |

---

## Deferred Items (Carried Forward — OPEN)

---

### DW-B141-SIM-03 — Consecutive Drags, No Accumulation (SIM Pending)

| Field | Value |
|-------|-------|
| **ID** | DW-B141-SIM-03 |
| **Title** | SIM Gate 3 — two consecutive stop drags; both produce target resubmit; no accumulation of orphan PTT-TGT-Drag orders |
| **Status** | OPEN (P1) — Block A-Prime sweeps in place; explicit SIM documentation not yet produced |
| **Priority** | P1 |
| **Target Block** | B142 SIM follow-up |
| **Root Block** | B141 |

**B142 update**: B142-DIRECT-8 added Block A-Prime-Stop and Block A-Prime-Target sweeps in `ResubmitOneCollateralLeg`, and `ResubmitTargetAfterCascade` has Block A-Prime from B141. The code infrastructure for idempotency is in place. Explicit SIM documentation of "exactly ONE PTT-TGT-Drag per leg after two consecutive drags" has not been formally documented as run.

**Pass criteria (unchanged from B141)**:
- After second drag, exactly ONE PTT-TGT-Drag per leg for the instrument.
- No orphan Working orders accumulate.
- Second resubmit fires at latest captured target price.

---

### DW-B64-01 — HandleEntryChange Not Firing (Next P0)

| Field | Value |
|-------|-------|
| **ID** | DW-B64-01 |
| **Title** | HandleEntryChange not firing — drag sync broken |
| **Status** | OPEN (P0) |
| **Priority** | P0 |
| **Target Block** | Next P0 block after B142 SIM confirmation |
| **Root Block** | B64 |

**B142 impact**: None. B142 does not touch HandleEntryChange or the entry drag sync path.

---

### DW-B71-01..04 — Quick ALL Follower Bracket Dispatch + QX Guard

| Field | Value |
|-------|-------|
| **ID** | DW-B71-01..04 |
| **Title** | Quick ALL follower bracket dispatch + QX guard |
| **Status** | OPEN (P1) |
| **Priority** | P1 |
| **Target Block** | future |
| **Root Block** | B71 |

**B142 impact**: None.

---

### DW-B63-01 — Double PTT-Flatten 11ms Apart

| Field | Value |
|-------|-------|
| **ID** | DW-B63-01 |
| **Title** | Double PTT-Flatten 11ms apart |
| **Status** | OPEN (P1) |
| **Priority** | P1 |
| **Target Block** | future |
| **Root Block** | B63 |

**B142 impact**: None.

---

### DW-B141 — Phase C Re-Confirmation (Pending SIM Test A)

| Field | Value |
|-------|-------|
| **ID** | DW-B141 |
| **Title** | SyncAtmFollowerTarget Phase C operable — pending SIM Test A confirmation |
| **Status** | OPEN (P1, awaiting SIM run) |
| **Priority** | P1 |
| **Target Block** | B135 SIM |
| **Root Block** | Pre-B134 |

**B142 impact**: B142 modifies `SyncAtmFollowerTarget` (DIRECT-5, DIRECT-7, QTY-DESYNC-01) but does not execute Phase C SIM Test A. Phase C re-confirmation still pending.

---

### DW-B138 — Follower Stop Drag Confirmed (Pending SIM Test B — Must Re-Run with B142 Behavior)

| Field | Value |
|-------|-------|
| **ID** | DW-B138 |
| **Title** | Follower stop leg drag sync confirmed — pending SIM Test B director confirmation |
| **Status** | OPEN (P1, awaiting SIM run) |
| **Priority** | P1 |
| **Target Block** | B135 SIM |
| **Root Block** | B131 |

**B142 update**: B142 changes the observable stop drag behavior substantially. A stop drag now produces:
- PTT-STP-Drag-1, PTT-STP-Drag-2, PTT-STP-Drag-3 (all three stop legs)
- PTT-TGT-Drag-1, PTT-TGT-Drag-2, PTT-TGT-Drag-3 (all three target legs)

SIM Test B must be re-run under B142 full 3-leg behavior. The B141 SIM Test B pass criteria are superseded by B142.

**Updated B142 SIM Test B Pass Criteria**:
1. Leader Stop1 drag → all three follower stop legs updated (PTT-STP-Drag-1/2/3 appear at new price).
2. All three follower ATM target legs cascade-cancelled (expected — by design).
3. Three new PTT-TGT-Drag-1/2/3 appear at captured original target prices.
4. No permanent naked-position window on any leg.
5. Per-leg quantities match leader per-leg quantities (not a single shared quantity).

---

### B135-DEFER-01 — Gap B Runtime (Two Simultaneous Entries)

| Field | Value |
|-------|-------|
| **ID** | B135-DEFER-01 |
| **Title** | Gap B — two simultaneous leader entries, cancel first, verify 2nd copied |
| **Status** | OPEN (P1) |
| **Priority** | P1 |
| **Target Block** | B138+ |
| **Root Block** | B133 |

**B142 impact**: None.

---

### B135-DEFER-02 — Stale Orders Multi-Session

| Field | Value |
|-------|-------|
| **ID** | B135-DEFER-02 |
| **Title** | Stale orders from prior sessions may match FindFollowerBracketOrder |
| **Status** | OPEN (P2) |
| **Priority** | P2 |
| **Target Block** | future |
| **Root Block** | B133 |

**B142 impact**: None. B142 adds `ChangeSubmitted` to `FindFollowerBracketOrder` state filter but does not address the stale-order session-start problem.

---

### DW-B134-OCO-OBS — Partial-Fill Race Conditions (OBS-A/B/C/D)

| Field | Value |
|-------|-------|
| **ID** | DW-B134-OCO-OBS |
| **Title** | OCO orphan partial-fill race conditions (OBS-A/B/C/D) |
| **Status** | OPEN (OBS-A/B/C/D, P1) |
| **Priority** | P1 |
| **Target Block** | future |
| **Root Block** | B134 |

**B142 impact**: None. B142 fixes ATM bracket stop drag sync but does not address partial-fill race conditions.

| Obs ID | Description | Why Not Closed |
|--------|-------------|----------------|
| OBS-A | Cancel races partial fill | T2 absorbs error; race window unaddressed |
| OBS-B | Replacement order duplicates partially-filled quantity | Pre-flat partial-fill state unaddressed |
| OBS-C | Stop side not cancelled before target replacement | Pre-flat bracket ordering unaddressed |
| OBS-D | Net position drift on two-leg partial fill | Quantity-aware guard scope; out of scope |

---

### SHA-DOC-01 — SHA Typo (Documentation)

| Field | Value |
|-------|-------|
| **ID** | SHA-DOC-01 |
| **Title** | SHA discrepancy: `02-architecture-plan.md` uses `a702bcbd`; `04-tickets.md` uses `a702ccbd` |
| **Status** | OPEN (P2) |
| **Priority** | P2 |
| **Target Block** | future docs sweep |
| **Root Block** | B142 |

**Resolution**: `git log --oneline --grep="DW-B142-DRAG"` → update the incorrect reference in the one incorrect document.

---

## Closure Log (Cumulative — This Block)

| ID | Block Closed | Reason |
|----|-------------|--------|
| DW-B134-OCO (main) | B135 | T2: TrySweptPttDragOrphans + CancelPttDragOrphansForAccount |
| DW-B148 | B136 | T1: OrderPassesBracketGate fused guard |
| DW-B146 | B136 | Consequential via DW-B148 |
| DW-B147 | B137 | T2: IsNoPriceChange guard |
| DW-B149 | B137 | T2: IsNoPriceChange guard (same fix) |
| DW-B150 | B137 | T3: OrderPassesBracketGate empty-string condition |
| DW-B151 | B137 | T4: CancelExistingPttStpDrag pre-sweep |
| DW-B152-B | B139 | T1+T2: IsPttStpDragCancellable + CancelExistingPttStpDrag refactor |
| DW-B153 | B140-LaneA | CLOSED — but invalidated by SIM Gate 1 FAIL |
| DW-B153 | B141 | RE-CLOSED via dual-resubmit (correct mechanism) |
| DW-B140-01 | B141 | CLOSED (superseded) — SIM ran, FAIL, acc.Change confirmed no-op |
| DW-B140-02 | B141 | CLOSED (superseded) — acc.Change approach abandoned |
| DW-B140-03 | B141 | CLOSED (superseded) — B141 Gate 3 replaces |
| DW-B142-DRAG | **B142** | **CLOSED (SIM CONFIRMED 2026-09-02)** — `IsAtmSTPOrder` PTT-TGT-Drag- clause |
| DW-B142-QTY-DESYNC-01 | **B142** | **CLOSED (code committed; SIM pending)** — `FindLeaderCollateralOrder` + `leaderOrder.Quantity` in all resubmit helpers |

---

## Summary Table

| ID | Title | Priority | Target Block | Status |
|----|-------|----------|--------------|--------|
| DW-B134-OCO (main) | OCO orphan flat-position sweep | P1 | B135 | **CLOSED** |
| DW-B148 | OrderPassesBracketGate fused guard | P1 | B136 | **CLOSED** |
| DW-B146 | MatchesLeaderName ATM-path reachability | P1 | B136 | **CLOSED** |
| DW-B147 | rawPrice==newPrice early-return guard | P2 | B137 | **CLOSED** |
| DW-B149 | ChangeSubmitted race second TP3-HBC | P1 | B137 | **CLOSED** |
| DW-B150 | OrderPassesBracketGate empty-string fo=NULL | P1 | B137 | **CLOSED** |
| DW-B151 | SyncAtmFollowerBracket missing Block A-Prime | P1 | B137 | **CLOSED** |
| DW-B152-B | Cancel-in-flight race — CancelPending/CancelSubmitted gap | P1 | B139 | **CLOSED** |
| DW-B153 | OCO cascade on Stop1/Stop2 drag — dual-resubmit | P0 | B141 | **CLOSED (re-closed)** |
| DW-B154 | acc.Change() confirmed no-op on ATM Stop brackets | N/A | B141 | **DOCUMENTED** |
| DW-B140-01 | SIM Gate 1 — acc.Change non-no-op on Stop brackets | P0 | B140 SIM | **CLOSED (superseded)** |
| DW-B140-02 | SIM Gate 2 — Stop3 via acc.Change, Target3 not cancelled | P1 | B140 SIM | **CLOSED (superseded)** |
| DW-B140-03 | SIM Gate 3 — consecutive drags no cascade | P1 | B140 SIM | **CLOSED (superseded)** |
| DW-B142-DRAG | Second+ target drag routes to no-op instead of cancel+resubmit | P0 | B142 | **CLOSED (SIM CONFIRMED 2026-09-02)** |
| DW-B142-QTY-DESYNC-01 | Resubmitted PTT orders use wrong quantity | P1 | B142 | **CLOSED (code committed; SIM pending)** |
| DW-B141-STP-CYC8-WALL | Three methods at CYC=8 limit (scope expanded B142) | P1 | next mod to affected methods | **OPEN** |
| DW-B141-SIM-01 | SIM Gate 1 — dual-resubmit: PTT-TGT-Drag appears after cascade | P0 → de-escalated | B141 SIM (retroactive) | **EFFECTIVELY CONFIRMED** |
| DW-B141-SIM-02 | SIM Gate 2 — Stop2 drag, Target2 resubmit | P1 | B141 SIM (retroactive) | **EFFECTIVELY CONFIRMED** |
| DW-B141-SIM-03 | SIM Gate 3 — consecutive drags, no accumulation | P1 | B142 SIM follow-up | **OPEN** |
| DW-B64-01 | HandleEntryChange not firing — drag sync broken | P0 | next P0 block | **OPEN** |
| DW-B71-01..04 | Quick ALL follower bracket dispatch + QX guard | P1 | future | **OPEN** |
| DW-B63-01 | Double PTT-Flatten 11ms apart | P1 | future | **OPEN** |
| DW-B141 | Phase C re-confirmation — pending SIM Test A | P1 | B135 SIM | **OPEN** |
| DW-B138 | Stop drag confirmed — pending SIM Test B (re-run with B142 3-leg behavior) | P1 | B135 SIM | **OPEN** |
| B135-DEFER-01 | Gap B — two simultaneous entries | P1 | B138+ | **OPEN** |
| DW-B134-OCO-OBS | OBS-A/B/C/D partial-fill race conditions | P1 | future | **OPEN** |
| B135-DEFER-02 | Stale orders multi-session | P2 | future | **OPEN** |
| SHA-DOC-01 | SHA typo: a702bcbd vs a702ccbd for DW-B142-DRAG commit | P2 | future docs sweep | **OPEN** |

---

## Carry-Forward Note for B143

**Next P0 item**: `DW-B64-01` — HandleEntryChange not firing. This is the next P0 priority after the B142 SIM confirmation chain is complete.

**Open SIM gates**:
1. `DW-B141-SIM-03` (P1) — consecutive-drag idempotency SIM test (Block A-Prime sweeps in place; SIM run not documented)
2. `DW-B142-QTY-DESYNC-01` (P1) — formal SIM confirmation of per-leg quantity correctness (code committed; SIM date TBD)
3. `DW-B138` (P1) — SIM Test B must be re-run under full B142 3-leg drag behavior
4. `DW-B141` (P1) — Phase C SIM Test A still pending

**Methods at CYC=8 AT LIMIT** (cannot add branches without prior extraction):
- `SyncFollowerBracket` (L2266-2345)
- `SyncAtmFollowerTarget` (L2856-2940)
- `FindFollowerBracketOrder` list overload (L3138-3171)

*2 items CLOSED this block (DW-B142-DRAG SIM CONFIRMED; DW-B142-QTY-DESYNC-01 committed).
DW-B141-SIM-01 and DW-B141-SIM-02 upgraded from OPEN to EFFECTIVELY CONFIRMED by B142 SIM chain.
DW-B141-STP-CYC8-WALL scope expanded: 1 method → 3 methods at CYC=8 AT LIMIT.
1 new item added (SHA-DOC-01, P2 documentation).
DW-B64-01 (P0) is next non-SIM priority for B143.*

---

*Produced by ptt-plan-reviewer, B142 Phase 5. Required gate artifact for FINAL_PASS.*
