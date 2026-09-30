# B141 Deferred Backlog

**Block**: B141
**Block Header**: B141 Deferred Items — OCO Cascade Dual-Resubmit
**Produced by**: ptt-plan-reviewer (Phase 5)
**Prior backlog**: `docs/brain/B140-LaneA/06-deferred-backlog.md`
**Date**: 2026-09-01

---

## Status Changes From B140-LaneA

| ID | B140-LaneA Status | B141 Status | Change |
|----|-------------------|-------------|--------|
| DW-B153 | CLOSED (SIM Gate 1 PENDING — invalidated by SIM Gate 1 FAIL) | **CLOSED (re-closed)** | B141 dual-resubmit re-closes via correct mechanism: capture Target price, accept cascade, resubmit PTT-TGT-Drag. |
| DW-B140-01 | OPEN (P0, awaiting SIM run — blocking merge) | **CLOSED (superseded)** | SIM Gate 1 ran and FAILED. acc.Change IS a no-op on ATM Stop brackets. Question answered with negative result. B141 works around constraint via dual-resubmit. |
| DW-B140-02 | OPEN (P1, awaiting SIM run) | **CLOSED (superseded)** | acc.Change approach abandoned entirely. B141 uses dual-resubmit for ALL ATM stops uniformly. |
| DW-B140-03 | OPEN (P1, awaiting SIM run) | **CLOSED (superseded)** | B141 Gate 3 (consecutive stop drags, no PTT-TGT-Drag accumulation) replaces this gate. Block A-Prime in ResubmitTargetAfterCascade is idempotent. |
| DW-B64-01 | OPEN (P0) | OPEN (P0) | No change. B141 does not touch HandleEntryChange or entry drag sync path. Promoted to top P0 after SIM gates. |
| DW-B71-01..04 | OPEN (P1) | OPEN (P1) | No change. B141 does not touch follower bracket dispatch or QX guard. |
| DW-B63-01 | OPEN (P1) | OPEN (P1) | No change. B141 does not touch PTT-Flatten path. |
| DW-B141 | OPEN (P1, awaiting SIM Test A) | OPEN (P1) | No change. B141 does not touch Phase C or SyncAtmFollowerTarget. |
| DW-B138 | OPEN (P1, awaiting SIM Test B) | OPEN (P1) | No change. B141 changes stop bracket sync mechanism; SIM Test B must be re-run with B141 dual-resubmit behavior (PTT-STP-Drag for stop, PTT-TGT-Drag for target). |
| B135-DEFER-01 | OPEN (P1) | OPEN (P1) | No change. B141 does not touch entry-copy path. |
| B135-DEFER-02 | OPEN (P2) | OPEN (P2) | No change. B141 does not touch FindFollowerBracketOrder iteration scope. |
| DW-B134-OCO-OBS | OPEN (OBS-A/B/C/D, P1) | OPEN (OBS-A/B/C/D, P1) | No change. B141 does not address partial-fill race conditions. |

---

## New Closures — B141

---

### DW-B153 — CLOSED (Re-closed in B141 via Dual-Resubmit)

| Field | Value |
|-------|-------|
| **ID** | DW-B153 |
| **Title** | OCO cascade on Stop1/Stop2 drag — follower loses Target1/Target2 on every stop drag |
| **Status** | **CLOSED (re-closed)** |
| **Priority** | P0 |
| **Closed in Block** | B141 (original closure in B140-LaneA was invalidated by SIM Gate 1 FAIL) |
| **Closed by** | T1: `SyncFollowerBracket` branch (3) dual-resubmit: `CaptureLinkedTargetPrice` + `SyncAtmFollowerBracket` + `ResubmitTargetAfterCascade` (4 new methods, 1 modified method, 7 xUnit tests in B141Tests.cs) |

**Root cause** (confirmed B140 SIM Gate 1 FAIL): `acc.Cancel(fo)` on OCO-linked ATM Stop bracket from AddOnBase triggers NT8 OCO cascade atomically: Stop1 Cancelled → Target1 Cascade-Cancelled. `acc.Change()` is a silent no-op on ATM-owned brackets (DW-B154 — confirmed B140 SIM Gate 1 FAIL, fd4a439d). Neither approach prevents the cascade from AddOnBase context.

**Fix**: Accept the OCO cascade. Capture the linked target's limit price before the cancel fires. After `SyncAtmFollowerBracket` (which fires the cascade as before), check if a target price was captured and resubmit a standalone `PTT-TGT-Drag` limit order at that price. Block A-Prime in `ResubmitTargetAfterCascade` sweeps any stale PTT-TGT-Drag first (prevents accumulation on consecutive drags). The naked-position window is bounded to the cascade round-trip — the target is restored on every stop drag.

**Closure evidence**:
- `SyncFollowerBracket` branch (3) at CopyEngine.cs L2281-2288.
- `CaptureLinkedTargetPrice` at L2396-2407; `TryParseStopSuffix` at L2413-2423; `IsTargetOrderLive` at L2428-2429; `ResubmitTargetAfterCascade` at L2441-2499.
- 7 xUnit [Fact] tests T_B141_01 through T_B141_07 in B141Tests.cs. `dotnet test --filter "T_B141"`: Passed 7, Failed 0.
- BUILD_PASS (0 errors). VERIFY_PASS (all checks). ptt-sync-and-verify.ps1: 0 MISMATCH.
- **SIM Gate 1 (DW-B141-SIM-01) PENDING**: Dual-resubmit behavior requires director SIM confirmation. Gate 1 FAIL protocol: STOP, document as DW-B155, Director resolution.

---

### DW-B140-01 — CLOSED (Superseded)

| Field | Value |
|-------|-------|
| **ID** | DW-B140-01 |
| **Title** | SIM Gate 1 — acc.Change() non-no-op on Stop brackets |
| **Status** | **CLOSED (superseded)** |
| **Reason** | SIM Gate 1 ran and FAILED. `acc.Change()` IS a no-op on ATM Stop brackets from AddOnBase context. The question this gate was testing is now definitively answered: negative. B141 works around this NT8 API constraint via dual-resubmit. This gate will never be re-run on the acc.Change approach. |

---

### DW-B140-02 — CLOSED (Superseded)

| Field | Value |
|-------|-------|
| **ID** | DW-B140-02 |
| **Title** | SIM Gate 2 — Stop3 via acc.Change, Target3 not cancelled |
| **Status** | **CLOSED (superseded)** |
| **Reason** | acc.Change approach abandoned entirely (DW-B154 confirmed it is a no-op). B141 dual-resubmit handles Stop1/Stop2/Stop3 uniformly via the same `TryParseStopSuffix` suffix-1/2/3 logic. |

---

### DW-B140-03 — CLOSED (Superseded)

| Field | Value |
|-------|-------|
| **ID** | DW-B140-03 |
| **Title** | SIM Gate 3 — consecutive drags, no cascade |
| **Status** | **CLOSED (superseded)** |
| **Reason** | B141 Gate 3 (DW-B141-SIM-03) replaces this gate with the dual-resubmit behavior: after two consecutive stop drags, exactly one PTT-TGT-Drag should exist (Block A-Prime sweeps stale orders). The acc.Change consecutive-drag scenario is no longer relevant. |

---

## DW-B154 — DOCUMENTED (Informational, No Code Required)

| Field | Value |
|-------|-------|
| **ID** | DW-B154 |
| **Title** | `acc.Change()` is a confirmed silent no-op on ATM-owned Stop brackets from AddOnBase |
| **Status** | **DOCUMENTED** — confirmed architectural constraint, no fix required |
| **Priority** | N/A (architecture fact, not a bug) |
| **Block discovered** | B140 SIM Gate 1 FAIL; revert commit fd4a439d |

**Description**: `Account.Change()` called on ATM Strategy-owned Stop bracket orders from an AddOnBase-derived context does NOT update the stop price. The Order Grid shows no change. The state cycle does not advance to ChangeSubmitted. This is a confirmed NT8 API constraint: ATM-owned brackets are managed internally by the ATM Strategy engine; the AddOn `acc.Change()` call is accepted without error but produces zero effect on ATM-owned orders.

**Architecture implication**: ALL B141+ code for ATM Stop bracket price changes MUST use cancel+resubmit (never `acc.Change`). B141 dual-resubmit is the correct and confirmed pattern for AddOnBase ATM bracket management.

**Source reference**: CopyEngine.cs L2278 comment: `// DW-B154: acc.Change() confirmed no-op on ATM Stop brackets from AddOnBase (B140 SIM Gate 1 FAIL).`

---

## New Deferred Items — B141

---

### DW-B141-STP-CYC8-WALL — SyncFollowerBracket at CYC 8 Limit

| Field | Value |
|-------|-------|
| **ID** | DW-B141-STP-CYC8-WALL |
| **Title** | `SyncFollowerBracket` is at CYC 8 — no further branching may be added without prior extraction |
| **Status** | OPEN (architectural constraint) |
| **Priority** | P1 |
| **Target Block** | Next block that needs to modify `SyncFollowerBracket` |
| **Root Block** | B141 |

**Description**: After B141, `SyncFollowerBracket` reaches CYC 8 (the JS-041 project limit under the project counting convention: base=1, `&&`/`||`=0, `catch`=0). The 8 branches are: base(1) + fo-null(1) + price-delta(1) + ATM-STP-branch-3(1) + HasValue-B141(1) + ATM-TGT-branch-3b(1) + IsTrailingStop-branch-4(1) + isStop-inner-branch-5(1) = CYC 8.

**Constraint**: Any future requirement adding a branch to `SyncFollowerBracket` MUST first extract one or more existing branches to helper methods to create headroom. Engineer MUST perform a CYC audit before adding any conditional logic to `SyncFollowerBracket`.

**Source reference**: CopyEngine.cs L2285 comment: `// B141: +1 branch -> CYC 8 (at limit -- no further branching may be added)`.

---

### DW-B141-SIM-01 — SIM Gate 1 (P0 BLOCKING)

| Field | Value |
|-------|-------|
| **ID** | DW-B141-SIM-01 |
| **Title** | SIM Gate 1 — dual-resubmit: after Stop1 drag, Target1 NOT cancelled AND new PTT-TGT-Drag appears at captured price |
| **Status** | OPEN (P0 — BLOCKING merge) |
| **Priority** | P0 |
| **Target Block** | B141 SIM |
| **Root Block** | B141 |

**Description**: The B141 dual-resubmit fix relies on `ResubmitTargetAfterCascade` successfully creating and submitting a PTT-TGT-Drag order after the OCO cascade. SIM confirmation is required that:
1. The cascade fires as expected (Target1 cascade-cancelled — correct/expected behavior).
2. A new `PTT-TGT-Drag` limit order appears in the follower Order Grid at the ORIGINAL Target1 limit price.
3. The follower's naked-position window is bounded to the cascade round-trip.

**SIM Gate 1 Procedure**:
1. Open NT8 SIM environment with PTT leader + follower, ATM-entered position (Stop1/Target1 visible in follower Order Grid).
2. Drag leader Stop1 to a new price.
3. Observe follower Order Grid.

**Pass criteria (ALL must be true)**:
- Follower Stop1 price updates to new price (PTT-STP-Drag appears).
- Target1 is initially cascade-cancelled (expected — by design, NOT a failure).
- A new `PTT-TGT-Drag` limit order appears at the ORIGINAL Target1 price.
- StatusUpdate log shows: `"B141 TGT resubmit after cascade -> [price]"`.
- No naked-position window persists beyond the cascade round-trip.

**Gate 1 FAIL Protocol — NO EXCEPTIONS**:
- If `PTT-TGT-Drag` does NOT appear after cascade: STOP immediately.
- Document as **DW-B155**. Do NOT implement a further fallback.
- Merge is BLOCKED. Director resolution required.

---

### DW-B141-SIM-02 — SIM Gate 2 (P1)

| Field | Value |
|-------|-------|
| **ID** | DW-B141-SIM-02 |
| **Title** | SIM Gate 2 — Stop2 drag produces same result for Target2 |
| **Status** | OPEN (P1) |
| **Priority** | P1 |
| **Target Block** | B141 SIM |
| **Root Block** | B141 |

**Description**: Same procedure as DW-B141-SIM-01 applied to the Stop2/Target2 pair. Confirms that `TryParseStopSuffix` suffix="2" and `CaptureLinkedTargetPrice` lookup of "Target2" produce the correct result.

**Pass criteria**: Stop2 price updates, Target2 cascade-cancelled (expected), new PTT-TGT-Drag appears at original Target2 price.

---

### DW-B141-SIM-03 — SIM Gate 3 (P1)

| Field | Value |
|-------|-------|
| **ID** | DW-B141-SIM-03 |
| **Title** | SIM Gate 3 — two consecutive stop drags; both produce target resubmit; no accumulation of orphan PTT-TGT-Drag orders |
| **Status** | OPEN (P1) |
| **Priority** | P1 |
| **Target Block** | B141 SIM |
| **Root Block** | B141 |

**Description**: After two consecutive Stop1 drags, exactly ONE PTT-TGT-Drag should exist for the instrument. Block A-Prime in `ResubmitTargetAfterCascade` cancels any stale PTT-TGT-Drag before resubmitting. Confirms idempotency.

**Pass criteria**:
- After second drag, exactly ONE PTT-TGT-Drag exists for the instrument (not two).
- Second resubmit fires with latest captured target price.
- No accumulation of orphan Working orders.

---

## Deferred Items (Carried Forward — OPEN)

---

### DW-B64-01 — HandleEntryChange Not Firing (Next P0)

| Field | Value |
|-------|-------|
| **ID** | DW-B64-01 |
| **Title** | HandleEntryChange not firing — drag sync broken |
| **Status** | OPEN (P0) |
| **Priority** | P0 |
| **Target Block** | Next P0 after B141 SIM gates |
| **Root Block** | B64 |

**Description**: HandleEntryChange event handler is not being invoked on entry drag events. This breaks the entry sync path. The next P0 item after B141 SIM gates are resolved.

**B141 impact**: None. B141 does not touch HandleEntryChange or the entry drag sync path.

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

**B141 impact**: None.

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

**B141 impact**: None.

---

### DW-B141 — Phase C Re-Confirmation (Pending SIM Test A)

| Field | Value |
|-------|-------|
| **ID** | DW-B141 |
| **Title** | SyncAtmFollowerTarget Phase C operable — pending SIM Test A confirmation |
| **Status** | OPEN (P1, awaiting SIM run) |
| **Priority** | P1 |
| **Target Block** | B135 SIM (carries forward until run) |
| **Root Block** | Pre-B134 |

**B141 impact**: None. B141 does not touch Phase C or SyncAtmFollowerTarget.

---

### DW-B138 — Follower Stop Drag Confirmed (Pending SIM Test B)

| Field | Value |
|-------|-------|
| **ID** | DW-B138 |
| **Title** | Follower stop leg drag sync confirmed — pending SIM Test B director confirmation |
| **Status** | OPEN (P1, awaiting SIM run) |
| **Priority** | P1 |
| **Target Block** | B135 SIM (carries forward until run) |
| **Root Block** | B131 |

**B141 impact**: B141 changes the observable stop drag behavior — stop drag now produces both a PTT-STP-Drag (from `SyncAtmFollowerBracket`) AND a PTT-TGT-Drag resubmit (from `ResubmitTargetAfterCascade`). SIM Test B must be re-run with B141 behavior to confirm both legs appear correctly and Target1/Target2 are NOT permanently cancelled.

**Updated SIM Test B Pass Criteria**:
1. Leader stop drag → Follower Stop1 price updated via PTT-STP-Drag (cancel+resubmit).
2. Follower Target1 cascade-cancelled (expected, B141 design).
3. New PTT-TGT-Drag appears at original Target1 price (B141 resubmit).
4. No permanent naked-position window.

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

**B141 impact**: None.

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

**B141 impact**: None.

---

### DW-B134-OCO-OBS — Partial-Fill Race Conditions (OBS-A/B/C/D)

| Field | Value |
|-------|-------|
| **ID** | DW-B134-OCO-OBS |
| **Title** | OCO orphan partial-fill race conditions (OBS-A/B/C/D) — carry-forward after T2 partial close |
| **Status** | OPEN (OBS-A/B/C/D, P1) |
| **Priority** | P1 |
| **Target Block** | future |
| **Root Block** | B134 |

**B141 impact**: None. B141 fixes ATM bracket stop sync but does not address partial-fill race conditions.

| Obs ID | Description | Why Not Closed |
|--------|-------------|----------------|
| OBS-A | Cancel races partial fill | T2 absorbs error; partial-fill race window unaddressed |
| OBS-B | Replacement order duplicates partially-filled quantity | T2 sweep is post-flat; pre-flat partial-fill state unaddressed |
| OBS-C | Stop side not cancelled before target replacement | Pre-flat bracket ordering unaddressed |
| OBS-D | Net position drift on two-leg partial fill | Quantity-aware guard in SyncAtmFollowerTarget; out of scope |

---

## Closure Log (Cumulative — This Block)

| ID | Block Closed | Reason |
|----|-------------|--------|
| DW-B134-OCO (main) | B135 | T2: TrySweptPttDragOrphans + CancelPttDragOrphansForAccount. |
| DW-B148 | B136 | T1: OrderPassesBracketGate fused guard. |
| DW-B146 | B136 | Consequential via DW-B148. |
| DW-B147 | B137 | T2: IsNoPriceChange guard. |
| DW-B149 | B137 | T2: IsNoPriceChange guard (same fix). |
| DW-B150 | B137 | T3: OrderPassesBracketGate empty-string condition. |
| DW-B151 | B137 | T4: CancelExistingPttStpDrag pre-sweep. |
| DW-B152-B | B139 | T1+T2: IsPttStpDragCancellable + CancelExistingPttStpDrag refactor. |
| DW-B153 | B140-LaneA | CLOSED — but invalidated by SIM Gate 1 FAIL. |
| DW-B153 | **B141** | **RE-CLOSED via dual-resubmit (correct mechanism).** |
| DW-B140-01 | **B141** | **CLOSED (superseded) — SIM ran, FAIL, acc.Change confirmed no-op.** |
| DW-B140-02 | **B141** | **CLOSED (superseded) — acc.Change approach abandoned.** |
| DW-B140-03 | **B141** | **CLOSED (superseded) — B141 Gate 3 replaces.** |

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
| DW-B141-STP-CYC8-WALL | SyncFollowerBracket at CYC 8 limit | P1 | next SyncFollowerBracket mod | **OPEN** |
| DW-B141-SIM-01 | SIM Gate 1 — dual-resubmit: PTT-TGT-Drag appears after cascade (BLOCKING merge) | P0 | B141 SIM | **OPEN** |
| DW-B141-SIM-02 | SIM Gate 2 — Stop2 drag, Target2 resubmit | P1 | B141 SIM | **OPEN** |
| DW-B141-SIM-03 | SIM Gate 3 — consecutive drags, no accumulation | P1 | B141 SIM | **OPEN** |
| DW-B64-01 | HandleEntryChange not firing — drag sync broken | P0 | next P0 | **OPEN** |
| DW-B71-01..04 | Quick ALL follower bracket dispatch + QX guard | P1 | future | **OPEN** |
| DW-B63-01 | Double PTT-Flatten 11ms apart | P1 | future | **OPEN** |
| DW-B141 | Phase C re-confirmation — pending SIM Test A | P1 | B135 SIM | **OPEN** |
| DW-B138 | Stop drag confirmed — pending SIM Test B | P1 | B135 SIM | **OPEN** |
| B135-DEFER-01 | Gap B — two simultaneous entries | P1 | B138+ | **OPEN** |
| DW-B134-OCO-OBS | OBS-A/B/C/D partial-fill race conditions | P1 | future | **OPEN** |
| B135-DEFER-02 | Stale orders multi-session | P2 | future | **OPEN** |

*4 items CLOSED this block (DW-B153 re-closed, DW-B140-01/02/03 superseded).
1 item DOCUMENTED (DW-B154 — architectural constraint, no fix required).
4 new items added (DW-B141-STP-CYC8-WALL, DW-B141-SIM-01/02/03).
DW-B141-SIM-01 (P0) is BLOCKING for merge.
9 items carry forward open (non-SIM). DW-B64-01 (P0) is next non-SIM priority.*

---

*Produced by ptt-plan-reviewer, B141 Phase 5. Required gate artifact for FINAL_PASS.*

---

## B142 Direct-Fix No-Pipeline Repair Log

> **Protocol (V1 — hardened 2026-09-XX)**: B142 uses direct fixes (Director-approved, no pipeline)
> until SIM confirms the stop-drag sync is fully working. Once "it worked" is received, the loop
> terminates and the full PTT pipeline (ptt-orchestrator) is triggered for hardening.
> Every session ends by handing the Director a ready-to-paste `$continue` prompt containing
> the updated NO-PIPELINE LOG and STATUS line for the next session.

### $continue Loop Protocol

**Loop entry**: Director pastes `$continue` prompt into a new copier-spec session.
**Session start actions** (every session, in order):
1. `git log --oneline -5`
2. `dotnet test ... --no-build` (confirm baseline)
3. Read STATUS line in the pasted prompt.
4a. STATUS == "it worked" → declare confirmed, update spec HTML pill, produce full-pipeline prompt, STOP loop.
4b. STATUS describes a failure → read last commit diff in CopyEngine.cs (never speculate), diagnose, fix, build, test, sync, commit, log in spec HTML, produce next `$continue` prompt.

**Session end actions** (every "it didn't work" session):
- Commit tagged `B142-DIRECT-N` (increment N from last entry below).
- Log fix in spec HTML no-pipeline section (`file:///C:/WSGTA/universal-or-strategy-director/specs/002-trade-copier-spec.html`).
- Produce next `$continue` prompt with updated NO-PIPELINE LOG block and STATUS line.
- Tell Director: "Press F5 in NT8, then test. Paste this `$continue` prompt in a new session with the result."

**Loop termination**: Director says "it worked" → full pipeline (ptt-orchestrator).

---

### B142 Direct-Fix Commit Register

| Commit | Tag | Fix | CYC impact | Status |
|--------|-----|-----|------------|--------|
| `4cc50a24` | B142-DIRECT-1 | `IsTrailingStop` excludes `PTT-STP-Drag` — second+ drags silently skipped | 0 | Confirmed |
| `e8d529e2` | B142-DIRECT-2 | `fo.StopPrice < tickSize` guard — spurious ATM cancel on session start | 0 | Confirmed |
| `220bc152` | B142-DIRECT-3 | Per-leg PTT names `PTT-STP-Drag-N` / `PTT-TGT-Drag-N` — concurrent drag collision | 0 | Confirmed |
| `2b052b5d` | B142-DIRECT-4 | `IsAtmSTPOrder` recognises `PTT-STP-Drag-` prefix; suffix from `leaderOrder.Name` | 0 | Confirmed |
| `2b052b5d` | B142-DIRECT-5 | `SyncAtmFollowerTarget` `fo.LimitPrice <= 0` guard — OCO cascade via spurious target cancel | 0 | Confirmed |
| `fbf39d0e` | B142-DIRECT-6 | `ResubmitCollateralLegs` — ATM OCO full-group cascade kills Stop2/Stop3 before drag events fire | 0 | SIM received (partial — 2 bugs remain) |
| `77a02254` | B142-DIRECT-7 | `IsTargetOrderLive` + `Submitted`; `SyncAtmFollowerTarget` per-leg `PTT-TGT-Drag-N` | 0 | SIM pending |

---

### NT8 API Facts Confirmed by B142 SIM Gates (never re-investigate)

| Fact | Confirmed |
|------|-----------|
| `acc.Change()` is silent no-op on ATM-owned Stop brackets from AddOnBase | B140 SIM Gate 1 FAIL |
| `acc.Cancel(Stop1_ATM)` OCO-cascades ALL ATM group members (Stop2/Stop3/Target1/Target2/Target3) | B142-DIRECT-6 SIM |
| `acc.Change()` on `PTT-STP-Drag-N` (AddOn-created) DOES work | B142-DIRECT-4 SIM |
| `IsTargetOrderLive` must include `Submitted` — ATM engine places orders in Submitted briefly before Working | B142-DIRECT-7 (to confirm) |
| `SyncAtmFollowerTarget` Block B must use per-leg `PTT-TGT-Drag-N` suffix to avoid stale accumulation | B142-DIRECT-7 (to confirm) |

