# B143 Deferred Backlog

**Block**: B143
**Block Header**: B143 Deferred Items — MGC Entry Guard
**Produced by**: ptt-plan-reviewer (Phase 5)
**Prior backlog**: `docs/brain/B142/06-deferred-backlog.md`
**Date**: 2026-09-07

---

## Status Changes From B142

| ID | B142 Status | B143 Status | Change |
|----|-------------|-------------|--------|
| DW-B142-MGC-02 | N/A (new in B143) | **CLOSED** | Instrument-level entry guard functional; commit `3f709a91`; T_B143_01 + T_B143_02 verify first-pass allow + duplicate block. |
| DW-B142-MGC-01 | N/A (new in B143) | **CLOSED** | Root cause confirmed resolved by MGC-02 guard. The MGC cancel+resubmit produces a new orderId; `IsLiveEntryBlocked` Branch 1 (`ContainsKey(instrKey)`) blocks it before any orderId check. |
| DW-B141-STP-CYC8-WALL | OPEN (P1) — 3 methods at CYC=8 | **OPEN (P1, SCOPE EXPANDED)** | B143 adds `TryFirePositionState` as a fourth method at CYC=8 AT LIMIT. The three original bracket-sync methods (`SyncFollowerBracket`, `SyncAtmFollowerTarget`, `FindFollowerBracketOrder` list overload) are unchanged. |
| DW-B141-SIM-03 | OPEN (P1) | **OPEN (P1)** | No change. B143 does not touch the drag-sync path. |
| DW-B64-01 | OPEN (P0) | **OPEN (P0)** | No change. B143 is pure test instrumentation; entry drag sync path untouched. Remains next P0 priority. |
| DW-B71-01..04 | OPEN (P1) | **OPEN (P1)** | No change. |
| DW-B63-01 | OPEN (P1) | **OPEN (P1)** | No change. |
| DW-B141 | OPEN (P1) | **OPEN (P1)** | No change. |
| DW-B138 | OPEN (P1) | **OPEN (P1)** | No change. |
| B135-DEFER-01 | OPEN (P1) | **OPEN (P1)** | No change. |
| B135-DEFER-02 | OPEN (P2) | **OPEN (P2)** | No change. |
| DW-B134-OCO-OBS | OPEN (OBS-A/B/C/D, P1) | **OPEN (OBS-A/B/C/D, P1)** | No change. |
| SHA-DOC-01 | OPEN (P2) | **OPEN (P2)** | No change. |
| DW-B141-SIM-01 | EFFECTIVELY CONFIRMED | **EFFECTIVELY CONFIRMED** | No change. |
| DW-B141-SIM-02 | EFFECTIVELY CONFIRMED | **EFFECTIVELY CONFIRMED** | No change. |

---

## New Closures — B143

---

### DW-B142-MGC-02 — CLOSED (Commit 3f709a91 + T_B143_01/02 Verification)

| Field | Value |
|-------|-------|
| **ID** | DW-B142-MGC-02 |
| **Title** | Instrument-level entry guard blocks duplicate dispatches for MGC cancel+resubmit pattern |
| **Status** | **CLOSED** |
| **Priority** | P1 |
| **Closed in Block** | B143 (code committed in `3f709a91`; tests verify in B143) |
| **Closure mechanism** | `_liveEntryInstruments` key set on first Gate 5 pass via `IsLiveEntryBlocked` TryAdd. Any second dispatch for the same `instrKey` (instrument + direction) is rejected by `ContainsKey` check at Branch 1 of `IsLiveEntryBlocked`, regardless of orderId. The MGC cancel+resubmit produces a new orderId but the same `instrKey`; Branch 1 blocks it. |
| **Test evidence** | T_B143_01: first call for new instrKey returns false (dispatch allowed). T_B143_02: second call for same instrKey (different orderId) returns true (blocked). T_B143_07: bracket-cancel orderId not in companion map — live entry guard survives (scoped-removal contract). |

---

### DW-B142-MGC-01 — CLOSED (Root Cause Resolved by MGC-02)

| Field | Value |
|-------|-------|
| **ID** | DW-B142-MGC-01 |
| **Title** | Root cause: MGC cancel+resubmit produces duplicate entry dispatch |
| **Status** | **CLOSED** |
| **Priority** | P1 |
| **Closed in Block** | B143 |
| **Closure mechanism** | The root cause was the absence of an instrument-level guard at Gate 5. `DispatchCopy` previously checked only orderId-level dedup (`IsDedup` + `IsEntryDispatched`). A fresh orderId on the same instrument+direction bypassed both checks. `IsLiveEntryBlocked` Branch 1 (`ContainsKey(instrKey)`) is the broadest check and runs first; it closes this structural gap. |
| **Test evidence** | T_B143_02 directly validates: `ORD-B143-02B` is a fresh orderId that would have bypassed orderId dedup; it is now correctly blocked at the instrument-level guard. |

---

## New Deferred Items — B143

---

### DW-B143-POSSTATE-CYC8 — TryFirePositionState at CYC=8 AT LIMIT

| Field | Value |
|-------|-------|
| **ID** | DW-B143-POSSTATE-CYC8 |
| **Title** | `TryFirePositionState` reached CYC=8 AT LIMIT after B143 — no further branching without extraction |
| **Status** | OPEN (architectural constraint) |
| **Priority** | P1 |
| **Target Block** | Next block touching `TryFirePositionState` |
| **Root Block** | B143 |

**Description**: B143 added a straight-line call to `ClearLiveEntryForInstrument(instr)` inside the existing `if (isLeaderAcct)` block in `TryFirePositionState`. This addition consumed the final CYC headroom (from CYC=7 to CYC=8). The branch count is now 1 (base) + 7 decision points = CYC=8 AT LIMIT:
- `if (state != Filled && state != PartFilled)` — 1
- `if (e.Order?.Instrument?.FullName == null)` — 1
- `if (prior == newVal)` — 1
- `if (!hasPos)` — 1
- `foreach (var r in _rules)` — 1
- `if (e.Order.Account.Name == r.MasterAccount?.Name)` — 1
- `if (isLeaderAcct)` — 1

**Constraint**: Any future modification adding a conditional branch to `TryFirePositionState` requires prior extraction of one existing branch to a helper method. This is consistent with the DW-B141-STP-CYC8-WALL pattern for the three bracket-sync methods.

**Four methods now at CYC=8 AT LIMIT**:
1. `SyncFollowerBracket` (L2266-2345) — root: B141
2. `SyncAtmFollowerTarget` (L2856-2940) — root: B142
3. `FindFollowerBracketOrder` list overload (L3138-3171) — root: B142
4. `TryFirePositionState` (L3451-3499) — root: B143

---

## NT8 API Facts Confirmed in B143

**None.** B143 is pure C# unit tests using only `ConcurrentDictionary` BCL operations and the `NinjaTrader.Cbi.OrderState` enum (a value type; no NT8 runtime dependency). No new NT8 API behavior was exercised or confirmed in B143.

---

## Carried Forward — Open Items (Unchanged by B143)

---

### DW-B141-STP-CYC8-WALL — Four Methods Now at CYC=8 (Scope Expanded by B143)

| Field | Value |
|-------|-------|
| **ID** | DW-B141-STP-CYC8-WALL |
| **Title** | Four methods at CYC=8 limit — no further branching without prior extraction |
| **Status** | OPEN (architectural constraint, scope expanded by B143) |
| **Priority** | P1 |
| **Target Block** | Next block touching any of these four methods |
| **Root Block** | B141 (SyncFollowerBracket); B142 (SyncAtmFollowerTarget, FindFollowerBracketOrder); B143 (TryFirePositionState) |

**Current AT LIMIT methods** (4 total):
1. `SyncFollowerBracket` (L2266-2345) — CYC=8
2. `SyncAtmFollowerTarget` (L2856-2940) — CYC=8
3. `FindFollowerBracketOrder` list overload (L3138-3171) — CYC=8
4. `TryFirePositionState` (L3451-3499) — CYC=8 (added by B143)

---

### DW-B141-SIM-01 — SIM Gate 1 (Effectively Confirmed)

| Field | Value |
|-------|-------|
| **ID** | DW-B141-SIM-01 |
| **Title** | SIM Gate 1 — dual-resubmit: after Stop1 drag, PTT-TGT-Drag appears at captured price |
| **Status** | **EFFECTIVELY CONFIRMED** by B142 SIM chain (2026-09-02). P0 merge blocker resolved. Formal standalone documentation pending. |
| **Priority** | P0 → de-escalated |

---

### DW-B141-SIM-02 — Stop2 Drag (Effectively Confirmed)

| Field | Value |
|-------|-------|
| **ID** | DW-B141-SIM-02 |
| **Title** | SIM Gate 2 — Stop2 drag produces same result for Target2 |
| **Status** | **EFFECTIVELY CONFIRMED** — same mechanism as SIM-01; explicit formal test still pending |
| **Priority** | P1 |

---

### DW-B141-SIM-03 — Consecutive Drags, No Accumulation (SIM Pending)

| Field | Value |
|-------|-------|
| **ID** | DW-B141-SIM-03 |
| **Title** | SIM Gate 3 — two consecutive stop drags; both produce target resubmit; no accumulation of orphan PTT-TGT-Drag orders |
| **Status** | OPEN (P1) — Block A-Prime sweeps in place; explicit SIM documentation not yet produced |
| **Priority** | P1 |
| **Target Block** | B142 SIM follow-up |

---

### DW-B64-01 — HandleEntryChange Not Firing (Next P0)

| Field | Value |
|-------|-------|
| **ID** | DW-B64-01 |
| **Title** | HandleEntryChange not firing — drag sync broken |
| **Status** | OPEN (P0) |
| **Priority** | P0 |
| **Target Block** | Next P0 block after B143 complete |

**B143 impact**: None. B143 is pure test instrumentation; HandleEntryChange and the entry drag sync path are untouched.

---

### DW-B71-01..04 — Quick ALL Follower Bracket Dispatch + QX Guard

| Field | Value |
|-------|-------|
| **ID** | DW-B71-01..04 |
| **Title** | Quick ALL follower bracket dispatch + QX guard |
| **Status** | OPEN (P1) |
| **Priority** | P1 |
| **Target Block** | future |

---

### DW-B63-01 — Double PTT-Flatten 11ms Apart

| Field | Value |
|-------|-------|
| **ID** | DW-B63-01 |
| **Title** | Double PTT-Flatten 11ms apart |
| **Status** | OPEN (P1) |
| **Priority** | P1 |
| **Target Block** | future |

---

### DW-B141 — Phase C Re-Confirmation (Pending SIM Test A)

| Field | Value |
|-------|-------|
| **ID** | DW-B141 |
| **Title** | SyncAtmFollowerTarget Phase C operable — pending SIM Test A confirmation |
| **Status** | OPEN (P1, awaiting SIM run) |
| **Priority** | P1 |
| **Target Block** | B135 SIM |

---

### DW-B138 — Follower Stop Drag (Pending SIM Test B — Must Re-Run with B142 Behavior)

| Field | Value |
|-------|-------|
| **ID** | DW-B138 |
| **Title** | Follower stop leg drag sync confirmed — pending SIM Test B director confirmation (must re-run with B142 3-leg behavior) |
| **Status** | OPEN (P1, awaiting SIM run) |
| **Priority** | P1 |
| **Target Block** | B135 SIM |

---

### B135-DEFER-01 — Gap B Runtime (Two Simultaneous Entries)

| Field | Value |
|-------|-------|
| **ID** | B135-DEFER-01 |
| **Title** | Gap B — two simultaneous leader entries, cancel first, verify 2nd copied |
| **Status** | OPEN (P1) |
| **Priority** | P1 |
| **Target Block** | B138+ |

---

### B135-DEFER-02 — Stale Orders Multi-Session

| Field | Value |
|-------|-------|
| **ID** | B135-DEFER-02 |
| **Title** | Stale orders from prior sessions may match FindFollowerBracketOrder |
| **Status** | OPEN (P2) |
| **Priority** | P2 |
| **Target Block** | future |

---

### DW-B134-OCO-OBS — Partial-Fill Race Conditions (OBS-A/B/C/D)

| Field | Value |
|-------|-------|
| **ID** | DW-B134-OCO-OBS |
| **Title** | OCO orphan partial-fill race conditions (OBS-A/B/C/D) |
| **Status** | OPEN (OBS-A/B/C/D, P1) |
| **Priority** | P1 |
| **Target Block** | future |

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
| **Title** | SHA discrepancy: `02-architecture-plan.md` uses `a702bcbd`; `04-tickets.md` uses `a702ccbd` for DW-B142-DRAG commit |
| **Status** | OPEN (P2) |
| **Priority** | P2 |
| **Target Block** | future docs sweep |

---

## Closure Log (Cumulative — Append B143 Closures)

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
| DW-B142-DRAG | B142 | CLOSED (SIM CONFIRMED 2026-09-02) — IsAtmSTPOrder PTT-TGT-Drag- clause |
| DW-B142-QTY-DESYNC-01 | B142 | CLOSED (code committed; SIM pending) — FindLeaderCollateralOrder + leaderOrder.Quantity in all resubmit helpers |
| **DW-B142-MGC-02** | **B143** | **CLOSED** — instrument-level entry guard functional; commit `3f709a91`; T_B143_01 + T_B143_02 verify |
| **DW-B142-MGC-01** | **B143** | **CLOSED** — root cause confirmed resolved by MGC-02 guard; T_B143_02 verifies fresh orderId blocked by instrument-level ContainsKey |

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
| **DW-B142-MGC-02** | Instrument-level entry guard blocks duplicate MGC dispatches | P1 | **B143** | **CLOSED** |
| **DW-B142-MGC-01** | Root cause: MGC cancel+resubmit produces duplicate entry dispatch | P1 | **B143** | **CLOSED** |
| DW-B143-POSSTATE-CYC8 | TryFirePositionState at CYC=8 AT LIMIT — no further branching without extraction | P1 | Next block touching TryFirePositionState | **OPEN** |
| DW-B141-STP-CYC8-WALL | Four methods at CYC=8 limit (scope expanded by B143) | P1 | next mod to affected methods | **OPEN** |
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

## Carry-Forward Note for B144

**Next P0 item**: `DW-B64-01` — HandleEntryChange not firing (drag sync broken). This is the next P0 priority after B143.

**Open SIM gates** (unchanged from B142):
1. `DW-B141-SIM-03` (P1) — consecutive-drag idempotency SIM test (Block A-Prime sweeps in place; SIM run not documented)
2. `DW-B142-QTY-DESYNC-01` (P1) — formal SIM confirmation of per-leg quantity correctness (code committed; SIM date TBD)
3. `DW-B138` (P1) — SIM Test B must be re-run under full B142 3-leg drag behavior
4. `DW-B141` (P1) — Phase C SIM Test A still pending

**Methods at CYC=8 AT LIMIT** (cannot add branches without prior extraction — 4 total after B143):
- `SyncFollowerBracket` (L2266-2345)
- `SyncAtmFollowerTarget` (L2856-2940)
- `FindFollowerBracketOrder` list overload (L3138-3171)
- `TryFirePositionState` (L3451-3499) — **added by B143**

*2 items CLOSED this block (DW-B142-MGC-02, DW-B142-MGC-01 — both confirmed by T_B143_01 through T_B143_07).
1 new deferred item added (DW-B143-POSSTATE-CYC8, P1 architectural constraint).
DW-B141-STP-CYC8-WALL scope expanded: 3 methods → 4 methods at CYC=8 AT LIMIT.
DW-B64-01 (P0) is next priority for B144.*

---

*Produced by ptt-plan-reviewer, B143 Phase 5. Required gate artifact for FINAL_PASS.*
