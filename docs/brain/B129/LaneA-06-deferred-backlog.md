# B129 LaneA Deferred Backlog

**Block**: B129 (LaneA complete)
**Date**: 2026-08-31
**Deferred items carried forward to B130+**

Both B129 lanes are PIPELINE_COMPLETE:
- LaneA: DW-B135 (Reversal Guard False-Positive After Leader Flat) — CLOSED
- LaneB: DW-B134 (ATM Bracket Drag Not Synced to Followers, partial 3-layer fix) — CLOSED

---

## Items CLOSED This Block (LaneA)

| Item | Description | Closed By |
|------|-------------|-----------|
| DW-B135 | Reversal guard false-positive after leader flat — `_lastLeaderDirection` not cleared on position transition to flat. Fix: `TryFirePositionState` clears direction key on `hasPos=False` leader path. DW-B128 protection preserved. | B129 LaneA T1 — BUILD_PASS, VERIFY_PASS, FINAL_PASS |

**New LaneA deferred items**: None. No new deferred items were identified during LaneA
architecture planning, ticket execution, or independent verification.

---

## All Open Deferred Items (20 total)

Carry-forward from `LaneB-06-deferred-backlog.md` (19 items) plus LaneA new items (0).

---

### DW-B134-OCO — OCO Orphan Risk After ATM STP Cancel+Resubmit

**Priority**: P2
**Status**: OPEN
**Context**: When `SyncAtmFollowerBracket` (`CopyEngine.cs` L2100) cancels the follower's ATM STP
bracket via `acc.Cancel(new Order[] { fo })` (Block A) and resubmits as a standalone "PTT-STP-Drag"
`StopMarket` order via `acc.CreateOrder` + `acc.Submit` (Block B), the new stop is **not part of the
original ATM OCO pair**. NT8's ATM engine manages the STP and target brackets as an OCO group; cancelling
the original "Buy STP" (or "Sell STP") may also cancel the paired OCO target bracket depending on ATM OCO
cancellation behavior.

**Impact**: The follower's stop is correctly moved to the dragged price, but:
1. The OCO target bracket may have been auto-cancelled by the ATM engine when the original "Buy STP" was
   cancelled (ATM OCO behavior on partial Cancel is unverified).
2. If the target bracket was NOT auto-cancelled: after the target fills, the ATM engine's OCO cancellation
   applies to the old (already-cancelled) "Buy STP", not the new "PTT-STP-Drag" stop. The new stop remains
   working after target fill, creating an orphaned open stop order on the follower account.

**Fix**: Investigate NT8 ATM OCO behavior on `acc.Cancel` — determine whether the OCO partner (target
bracket) is auto-cancelled when the STP bracket is cancelled from AddOn context.
- If YES (auto-cancel): the target bracket must also be resubmitted in Block B of `SyncAtmFollowerBracket`.
  Add a paired `acc.CreateOrder` for the target bracket as a new Block C, mirroring the stop resubmit.
- If NO (target survives): add stop-cleanup logic in `OnOrderUpdate` — when "PTT-STP-Drag" is still working
  after the associated ATM target fills (detected by position going flat), cancel the orphaned stop.

**Prerequisite**: Director SIM gate session to observe actual NT8 OCO behavior when `acc.Cancel` is called
on an ATM bracket in an active ATM strategy (with both STP and target brackets working). This observation
gates the fix design direction.

**Deferred to**: B130 or first SIM gate session after DW-B134 sync.

---

### DW-B129-01 — Director SIM Gate: Quick2t + QAll2t Live Validation

**Priority**: P1 — required before using Quick2t / QAll2t in a live session
**Context**: The `_instr2tBtn` ("Quick2t") and `_instrQAll2tBtn` ("QAll2t") buttons added in
B129 require a Director SIM session to confirm runtime behavioral assertions. The implementation
is code-verified (BUILD_PASS, VERIFY_PASS) but NT8 UI handlers cannot be exercised by unit tests.

**Verification criteria (Director SIM gate)**:
- `_instrument` field resolves non-null when TradeCopierPanel is open on a chart with an
  instrument loaded.
- `TryResolveLeaderAccount()` returns a non-null leader when the copier is configured.
- Quick2t pressed with a 7-contract MES position:
  - Output tab shows: `[PTT-QX-2T] button: Sim101 MES qty=7 T1=4 T2=3`
  - PTT-QX-T1 bracket submitted (qty=4), PTT-QX-T2 bracket submitted (qty=3)
- Quick2t pressed with a 6-contract position:
  - Output tab shows: T1=3 T2=3
- Quick2t pressed with a 1-contract position:
  - Output tab shows: T1=1 T2=0
  - PTT-QX-T1 submitted (qty=1), PTT-QX-T2 **skipped** (tNQty=0 guard fires)
- QAll2t pressed:
  - Output tab shows: `[PTT-QX-ALL] GlobalQuickExit fired`
  - All accounts with non-flat positions receive PTT-QX-* brackets
- No naked positions result from either button action.

**Deferred to**: B130 or first SIM gate session after B129 sync.

---

### DW-B133 — 2-Target Forced Count for PttGlobalQuickExit ALL Path

**Priority**: P2 — enhancement to make QAll2t use exactly 2-target bracket on ALL accounts
**Context**: Option B was chosen in B129 — `OnInstrQAll2tClick` calls existing
`PttGlobalQuickExit.Execute()` (zero-arg), which uses `SnapshotTargetOrders()` count
internally (3-target path for standard MES/ES ATMs). The Director-preferred Option A
(forced 2-target count for ALL path via a new Execute overload) was deferred due to
CYC budget concerns.

**Architecture for future implementation**:
- New overload: `Execute(List<(double, int)> forcedTargets)` on PttGlobalQuickExit
- The outer account/position loop structure must not be duplicated — pass `forcedTargets`
  into `ExecuteOne()` as an additional parameter (currently CYC=2)
- When `forcedTargets != null && forcedTargets.Count > 0`, `ExecuteOne` uses `forcedTargets`
  for order qty calculation instead of the snapshotted target list
- `ExecuteFollowers` would need a parallel forced-targets passthrough
- Requires architect plan for ExecuteOne signature change + CYC budget analysis before
  destabilizing the existing SIM-validated PttGlobalQuickExit execution chain

**Prerequisite**: DW-B129-01 SIM gate pass confirms QAll2t current behavior is acceptable
for immediate use, making DW-B133 a non-blocking enhancement.
**Deferred to**: B133 or first block after DW-B129-01 SIM gate passes.

---

### DW-B124-01 — Behavioral Change: Second Click No Longer Disarms BE-ALL

**Priority**: P2
**Context**: The toggle-disarm path in `OnGlobalBeClick` (which called `CopyEngine.Instance.DisarmPendingBe(acc)` for each account in `Account.All` and then `UpdateBeAllVisuals(BeState.Idle)`) was replaced with a guard (log `[PTT-BE-ALL] already armed, ignoring double-press` + `return`). This is an intentional breaking change per the B124 spec. The previous disarm-on-second-click UX behavior is permanently removed from this button.
**Impact**: After arming BE-ALL, the user can no longer disarm by clicking the button again. Disarm must occur via BE resolution or a dedicated disarm control. If Director determines that disarm-on-second-click should be restored as a separate code path (e.g., a distinct disarm action distinct from double-press guard), a new block specification is required.
**Deferred to**: B125 or future block, pending Director product decision.

---

### DW-B124-02 — Test 2 Assertion Weakness: callCount == 0 Instead of 1

**Priority**: P2
**Context**: `FirstPressArmsWhenNotYetArmed` asserts `callCount == 0` rather than `callCount == 1` as specified in the architecture plan. The test passes an empty `List<Account>()` to the `Execute(IEnumerable<Account>, int)` test-seam overload, so the inner foreach loop is a no-op and the delegate never fires. The test confirms the first-press code path executes without exception and reaches `Execute()`, but does not assert that the delegate was called.
**Impact**: Test exercises reachability but not invocation. The actual execution of `Execute()` is confirmed by code inspection, not by test assertion.
**Fix**: Provide a non-empty account list stub (e.g., a fake `Account` instance or use `InternalsVisibleTo` to call `OnGlobalBeClick` directly), or restructure the test-seam to count calls at the `Execute` level rather than the delegate level, and assert `callCount == 1`.
**Deferred to**: B125 or first polish block that revisits B124 test quality.

---

### DW-B107 — MoveStopToBreakEven Step A snapshots stale PTT-BE-Target-* on followers

**Priority**: P2 — correctness violation, functionally benign in observed test
**Discovered**: 2026-08-25 live BE-ALL test (stopped out, Copier ON, 4 accounts)
**Context**: Sim102/103/104 each submitted 4 OCO bracket pairs on a 3-target ATM. Sim101
(leader) correct at 3. `MoveStopToBreakEven` Step A (`CopyEngine.cs` ~L3380) collects
target orders into a single flat list with no native-vs-PTT discrimination and no count cap.
A stale `PTT-BE-Target-4` from a prior session (still `Working` in `acc.Orders`) was
included in the snapshot and an extra OCO pair submitted.
**Same class as**: DW-B106 (which fixed the QX path in B107-T1 — BE path not in scope).
**Deferred to**: B108 (next pipeline block after current testing batch).
**Full brief**: `docs/brain/DW-B107/00-defect-brief.md`
**Spec section**: `specs/002-trade-copier-spec.html#section-b107`
**CYC note**: Fix requires extraction of the Step A loop into a new `SnapshotBeTargets`
helper to keep `MoveStopToBreakEven` at CYC <= 8. Architect must plan extraction first.

---

### B107-DEFER-01 — F5 NinjaTrader 8 Compilation Gate

**Priority**: P0 — prerequisite for SIM gate and go-live
**Context**: `ptt-sync-and-verify.ps1` completed with 0 MISMATCH (16 files MD5-verified).
The F5 NinjaTrader 8 compilation step is the runtime compile gate. It must produce
"Compilation succeeded" (zero errors) after sync. This is Director-owned (requires local NT8 session).
**Action**: Director presses F5 in NinjaTrader 8 after confirming sync pass.
**Deferred to**: Director (immediate, prerequisite for B107-DEFER-02).

---

### B107-DEFER-02 — Combo C Live Re-Test

**Priority**: P1 — required before next live trading session involving BE-ALL then QX-ALL
**Context**: DW-B105 + DW-B106 code changes have been implemented and verified by independent
code inspection (VERIFY_PASS). Full behavioral validation of the Combo C scenario
(QX-ALL followed by BE-ALL, stale partial-fill residue case) requires a live NT8 session
with leader + follower accounts.
**Test sequence**:
1. Enter position on leader (Sim101) + followers (Sim102/103/104) via copier
2. Fire BE-ALL — confirm BE brackets placed on all 4 accounts
3. Fire QX-ALL — confirm:
   - `[PTT-QX-GUARD] pre-cancel follower brackets: Sim10X` appears in Output tab for each follower
   - Zero `[BE-DIAG]` lines during QX sweep (guards firing correctly)
   - All 4 accounts covered by PTT-QX-* brackets (none unprotected)
   - Exactly 3 target brackets submitted (PTT-QX-T1, T2, T3) — no T4
4. Confirm no naked positions after sweep
**Pass criterion**: zero [BE-DIAG] lines during QX sweep; all 4 accounts covered;
exactly 3 PTT-QX-T* brackets; no unprotected position.
**Fail criterion**: any unprotected position, any T4 bracket, any [BE-DIAG] line that was
previously absent.
**Deferred to**: Director SIM gate session (after B107-DEFER-01 green).

---

### DW-B42-01 — T_BUG_QX_BE_01 does not assert PTT-QX-T3

**Priority**: Low
**Context**: T_BUG_QX_BE_01 asserts true for PTT-QX-T1 and PTT-QX-T2 only. The production
predicate `IsPttQxTarget` also accepts T3 (name[8]<='3'). Standard MES/ES setups use 2 targets
(T1+T2). T3 is the second half of even-quantity splits on rare configs.
**Deferred to**: B43 or first block where T3 is confirmed in production use.
**Fix**: Add `Assert.True(IsPttQxTargetInline("PTT-QX-T3"))` to T_BUG_QX_BE_01.

---

### DW-B42-02 — Live NT8 F5 verification required

**Priority**: High — required before next live trading session
**Context**: The two bug directions can only be fully verified in a live NT8 session:
- Direction 1: Quick All -> BE All must place targets at BE price (not bare stop)
- Direction 2: BE All -> Quick All must start from clean slate
**Deferred to**: Next live F5 session (local compile + runtime confirm)
**Action**: Press sequence in SIM account before go-live.

---

### DW-B42-03 — IsPttQxTarget range extension for future target slots

**Priority**: Conditional (low unless T4/T5 slots added)
**Context**: Current range `name[8] >= '1' && name[8] <= '3'` matches B41 two-OCO-group design
(PTT-QX-T1 in OCO-A, PTT-QX-T2 in OCO-B, T3 as potential 3rd slot). If a future block adds
PTT-QX-T4 or T5, `IsPttQxTarget` must be updated.
**Deferred to**: Block that adds 4th+ target slot.

---

### DW-PTT-BE-FIX-01 — DW-B85 Option A: Lazy re-resolve for null followers

**Priority**: Medium
**Context**: When a follower account is not in Account.All at LoadRules() time, the Option B
warning is emitted. Option A would re-attempt resolution lazily in AllAccounts() when the
account later appears in Account.All. Per spec, Option A is deferred.
**Deferred to**: Next PTT productionisation block.
**Fix**: In AllAccounts(), replace null-skip with a lazy re-resolve.

---

### DW-PTT-BE-FIX-02 — SIM gate: Path B 3-cycle runtime verification

**Priority**: High — required before next live trading session with QX-ALL then BE-ALL sequence
**Context**: T1 (DW-B86) fixes the stop name guard but full SIM verification of Path B
(QX-ALL then BE-ALL, 3 cycles, checking stops=N > 0 on each follower) requires a live NT8
session with leader + follower accounts and open positions.
**Deferred to**: DW-B89 SIM gate session (combined with DW-B89-DEFERRED-04).
**Action**: Run Path B test sequence (3 cycles) in SIM before go-live.

---

### DW-PTT-BE-FIX-03 (= DW-B102-DEFER-01 / DW-B102-DEFER-02) — Pre-existing test build errors

**Priority**: High — blocks full test suite build
**Context**: Pre-existing errors in CopyEngineTests.cs stub infrastructure (83 errors) plus
CS0433 Globals ambiguity at CopyEngine.cs:L3350. Confirmed pre-existing, unrelated to B129.
**Deferred to**: Dedicated test infrastructure remediation block.
**Action**: Separate remediation track.

---

### DW-B89-DEFERRED-01 — Ctrl+F5 NT8 compilation gate (DW-B89 changes)

**Priority**: P0 — blocks DW-B89 SIM gate
**Context**: Director must confirm Ctrl+F5 in NinjaTrader for DW-B89 changes produces
"Compilation succeeded" 0 errors.
**Deferred to**: Director (immediate, prerequisite for all SIM paths below).

---

### DW-B89-DEFERRED-02 — SIM gate PATH A nominal

**Priority**: High
**Context**: Entry -> BE-ALL -> verify Output tab has NO [BE-ERR] lines, stops=N for all accounts.
3 cycles. PASS criterion: zero error popups, zero naked positions.
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-03 — SIM gate PATH A buf=0 edge case (short position)

**Priority**: High
**Context**: Entry short -> BE-ALL buf=0t immediately. Verify [BE-ERR] lines appear if price
moved OR stops placed successfully if price still at entry. NO naked positions. 1 cycle.
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-04 — SIM gate PATH B (QX-ALL then BE-ALL, 3 cycles)

**Priority**: High
**Context**: Entry -> QX-ALL -> BE-ALL arm -> price trigger.
Verify PTT-QX-Stop* cancelled, PTT-BE-Stop-N placed. stops=N. 3 cycles.
**Merges**: DW-PTT-BE-FIX-02 (Path B 3-cycle verification).
**Note**: B107-DEFER-02 (Combo C re-test) is the complementary test covering the reverse
direction (BE-ALL then QX-ALL). Both remain open.
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-05 — SIM gate DW-B87 timing race cycle

**Priority**: High
**Context**: Entry -> BE-ALL immediately (no wait). Must work (cancel sweep handles Submitted state).
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-06 — Spec update: close DW-B89/B88/B87 in spec HTML

**Priority**: Medium
**Context**: `specs/002-trade-copier-spec.html` sections #section-b89, #section-b88, #section-b87
must be updated to CLOSED status after all DW-B89 SIM gate paths pass.
**Action**: Director updates spec after full SIM gate PASS.
**Deferred to**: After all DW-B89 SIM paths green.

---

## Summary

| Category | Count | Items |
|----------|-------|-------|
| Closed this block (LaneA) | 1 | DW-B135 (direction key clear on leader flat — PIPELINE_COMPLETE) |
| New deferred (LaneA) | 0 | None |
| Carry-forward from LaneB (unchanged) | 19 | DW-B134-OCO, DW-B129-01, DW-B133, DW-B124-01/02, DW-B107, B107-DEFER-01/02, DW-B42-01/02/03, DW-PTT-BE-FIX-01/02/03, DW-B89-DEFERRED-01/02/03/04/05/06 |

**Total open items**: 20 (0 new + 19 carry-forward + 1 pre-existing P0 B107-DEFER-01)

---

## Spec Update Note (Orchestrator Action Required)

After FINAL_PASS, the orchestrator/Director MUST apply these spec HTML updates to
`specs/002-trade-copier-spec.html`:

| # | Update | Action |
|---|--------|--------|
| 1 | DW-B135 | Mark CLOSED — B129 LaneA PIPELINE_COMPLETE |
| 2 | DW-B134 | Mark CLOSED — B129 LaneB PIPELINE_COMPLETE |
| 3 | DW-B134-OCO | Add as OPEN deferred → B130 |
| 4 | DW-B136 Gap A | Mark RESOLVED — root cause was DW-B135, now fixed |
| 5 | B129 | Mark fully PIPELINE_COMPLETE (LaneA + LaneB) |
