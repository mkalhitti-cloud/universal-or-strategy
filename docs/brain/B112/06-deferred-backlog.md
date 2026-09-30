# B112 Deferred Backlog

**Block**: B112
**Date**: 2026-08-26
**Status**: PIPELINE_COMPLETE (coding phases) | F5 GATE: PASS (Director confirmed 2026-08-26)

---

## Items CLOSED This Block

| Item | Priority | Description | Closed By |
|------|----------|-------------|-----------|
| DW-B116 | P1 | `CountLeaderTargets` returns 5 for a 3-target ATM when stale residue orders (PTT-QX-T*, PTT-BE-Target-*) present in Accepted/Submitted states | B112-T1 (Changes 1 + 2 + 3) |
| DW-B113 | P0 | Bracketless position after BE-retry cap exhaustion — triggered by DW-B116 overcount causing spurious mismatch branch in `MoveStopToBreakEven` | B112-T1 (DW-B116 side-effect; no additional code change required) |
| DW-B114 | P1 | `_beReplaceAttempts` double-increment — triggered by DW-B116 overcount causing mismatch branch on two consecutive `OnOrderUpdate` ticks | B112-T1 (track-only; resolves as DW-B116 side-effect; no change to increment site) |
| B112-DEFER-01 | P0 | Director F5 NT8 Compilation Gate | CLOSED — F5 passed, Director confirmed 2026-08-26 |
| B107-DEFER-01 | P0 | Director F5 NT8 Compilation Gate (B107 changes) | CLOSED — covered by same F5 session 2026-08-26 |

---

## Deferred Items Added This Block

### DW-B114-TRACK — Counter pattern 1→3→5 monitor

**Priority**: P1 (monitor only)
**Context**: DW-B114 (`_beReplaceAttempts` double-increment) was resolved as a side-effect of the
DW-B116 fix. The fix eliminates the spurious mismatch branch that caused double-increment. If a
clean-session live re-test (B112-DEFER-02) reveals the 1→3→5 counter pattern reappearing after
DW-B116 is confirmed fixed, the double-increment has a second root cause that was not triggered in
testing. In that case, open a new targeted ticket for the `_beReplaceAttempts` increment site.
**Action**: Monitor during Combo D live re-test (B112-DEFER-02). No code change deferred.
**Deferred to**: Director monitoring during B112-DEFER-02 re-test.

---

### DW-B115 — ATM T1 qty distribution mismatch

**Priority**: P1
**Context**: DW-B115 was referenced in the B112 architecture plan (§ Deferred Items) but is not
listed in the B107 deferred backlog and was not in scope for B112. Details: ATM T1 quantity
distribution mismatch on follower accounts (separate from DW-B116 overcount). This defect was
not reproduced or analysed during B112. B112 does not close, partially close, or modify any
artifact related to DW-B115.
**Action**: Director must triage — confirm whether DW-B115 is a live defect, assign priority,
and schedule a dedicated pipeline block.
**Deferred to**: Future block — Director triage required before scheduling.

---

### ~~B112-DEFER-01~~ — Director F5 NT8 Compilation Gate — **CLOSED**

**Priority**: P0 — CLOSED
**Resolved**: 2026-08-26 — Director confirmed F5 passed (0 errors).
**Unblocks**: B112-DEFER-02 (Combo D live re-test).

---

### B112-DEFER-02 — Live Re-Test: Combo D (BE-ALL then QX-ALL)

**Priority**: P1 — required before next live trading session involving BE-ALL followed by QX-ALL
**Context**: DW-B116 code change has been implemented and independently verified (VERIFY_PASS).
Full behavioral validation of the Combo D scenario (BE-ALL then QX-ALL, with stale residue
orders possible from a prior session) requires a live NT8 session with leader + follower accounts.

**Test sequence**:
1. Fresh NT8 session (restart) — clear stale residue from `acc.Orders`.
2. Enter position on leader (Sim101) + followers (Sim102/103/104) via copier ON.
3. Fire BE-ALL:
   - Verify Output tab: zero log lines matching `"partial targets=N leader=5"`.
   - Verify `orders-for-instr` on all followers < 20 (clean baseline).
   - Verify zero `[BE-RETRY]` loop fires on any follower.
4. Fire QX-ALL (Combo D):
   - Verify DW-B112 guard fires for each follower with open PTT-QX-* orders.
   - Verify exactly 3 PTT-QX-T* submitted per follower (T1, T2, T3 — no T4/T5).
5. Confirm position closes flat on all 4 accounts (no naked position).

**Pass criterion**: zero `leader=5` lines; zero [BE-RETRY] fires; DW-B112 guard fires;
exactly 3 PTT-QX-T* per follower; all accounts flat.
**Fail criterion**: any `leader=5`, any [BE-RETRY], missing guard, T4/T5 submitted, naked position.
**Deferred to**: Director SIM gate session (after B112-DEFER-01 green).

---

## Carry-Forward Items (from B107 — unchanged by B112)

B112 does not close any B107 deferred items. All items below are reproduced verbatim from
`docs/brain/B107/06-deferred-backlog.md`. Status is unchanged.

---

### ~~B107-DEFER-01~~ — F5 NinjaTrader 8 Compilation Gate (B107 changes) — **CLOSED**

**Priority**: P0 — CLOSED
**Resolved**: 2026-08-26 — Covered by B112 F5 session (same NT8 compile). Director confirmed 0 errors.
**Unblocks**: B107-DEFER-02 (Combo C live re-test).

---

### B107-DEFER-02 — Combo C Live Re-Test

**Priority**: P1 — required before next live trading session involving BE-ALL then QX-ALL sequence
**Context**: DW-B105 + DW-B106 code changes verified by independent inspection (B107 VERIFY_PASS).
Full behavioral validation of Combo C (QX-ALL followed by BE-ALL, stale partial-fill residue case)
requires a live NT8 session.

**Test sequence**:
1. Enter position on leader + followers via copier
2. Fire BE-ALL — confirm BE brackets placed on all 4 accounts
3. Fire QX-ALL — confirm:
   - `[PTT-QX-GUARD] pre-cancel follower brackets: Sim10X` in Output for each follower
   - Zero `[BE-DIAG]` lines during QX sweep
   - All 4 accounts covered by PTT-QX-* brackets (none unprotected)
   - Exactly 3 PTT-QX-T* brackets (T1, T2, T3 — no T4)
4. Confirm no naked positions after sweep

**Pass criterion**: zero [BE-DIAG] lines; all 4 accounts covered; exactly 3 PTT-QX-T*; no unprotected position.
**Deferred to**: Director SIM gate session (after B107-DEFER-01 green).

---

### DW-B107 — MoveStopToBreakEven Step A snapshots stale PTT-BE-Target-* on followers

**Priority**: P2 — correctness violation, functionally benign in observed test
**Context**: Discovered 2026-08-25 live BE-ALL test. `MoveStopToBreakEven` Step A collects target
orders into a flat list with no native-vs-PTT discrimination and no count cap. A stale
`PTT-BE-Target-4` from a prior session (still Working in `acc.Orders`) was included in the snapshot
and an extra OCO pair submitted. Same class as DW-B106 (which fixed the QX path in B107-T1 — BE
path not in scope for B107 or B112).
**Deferred to**: B108 (next pipeline block after current testing batch).
**Full brief**: `docs/brain/DW-B107/00-defect-brief.md`
**CYC note**: Fix requires extraction of the Step A loop into a new `SnapshotBeTargets` helper.

---

### DW-B42-01 — T_BUG_QX_BE_01 does not assert PTT-QX-T3

**Priority**: Low
**Context**: T_BUG_QX_BE_01 asserts true for PTT-QX-T1 and PTT-QX-T2 only. The production
predicate `IsPttQxTarget` also accepts T3. Standard MES/ES setups use 2 targets (T1+T2).
**Deferred to**: B43 or first block where T3 is confirmed in production use.
**Fix**: Add `Assert.True(IsPttQxTargetInline("PTT-QX-T3"))` to T_BUG_QX_BE_01.

---

### DW-B42-02 — Live NT8 F5 verification required (B42 changes)

**Priority**: High — required before next live trading session
**Context**: Full verification of B42 bug directions requires a live NT8 session:
- Direction 1: Quick All → BE All must place targets at BE price (not bare stop)
- Direction 2: BE All → Quick All must start from clean slate
**Deferred to**: Next live F5 session.

---

### DW-B42-03 — IsPttQxTarget range extension for future target slots

**Priority**: Conditional (low unless T4/T5 slots added)
**Context**: Current range `name[8] >= '1' && name[8] <= '3'` matches B41 two-OCO-group design.
If a future block adds PTT-QX-T4 or T5, `IsPttQxTarget` must be updated.
**Deferred to**: Block that adds 4th+ target slot.

---

### DW-PTT-BE-FIX-01 — DW-B85 Option A: Lazy re-resolve for null followers

**Priority**: Medium
**Context**: When a follower account is not in Account.All at LoadRules() time, Option A would
re-attempt resolution lazily in AllAccounts() when the account later appears. Per spec, Option A
is deferred.
**Deferred to**: Next PTT productionisation block.
**Fix**: In AllAccounts(), replace null-skip with a lazy re-resolve.

---

### DW-PTT-BE-FIX-02 — SIM gate: Path B 3-cycle runtime verification

**Priority**: High — required before next live trading session with QX-ALL then BE-ALL sequence
**Context**: T1 (DW-B86) fixes the stop name guard but full SIM verification of Path B
(QX-ALL then BE-ALL, 3 cycles, checking stops=N > 0 on each follower) requires a live NT8 session.
**Deferred to**: DW-B89 SIM gate session (combined with DW-B89-DEFERRED-04).

---

### DW-PTT-BE-FIX-03 — Pre-existing test build errors

**Priority**: High — blocks full test suite build
**Context**: Pre-existing errors in CopyEngineTests.cs stub infrastructure (83 errors) plus
CS0433 Globals ambiguity at CopyEngine.cs:L3350. Confirmed pre-existing; unrelated to B107 or B112.
**Deferred to**: Dedicated test infrastructure remediation block.

---

### DW-B89-DEFERRED-01 — Ctrl+F5 NT8 compilation gate (DW-B89 changes)

**Priority**: P0 — blocks DW-B89 SIM gate
**Context**: Director must confirm Ctrl+F5 in NinjaTrader for DW-B89 changes produces
"Compilation succeeded" 0 errors.
**Deferred to**: Director (immediate, prerequisite for all SIM paths below).

---

### DW-B89-DEFERRED-02 — SIM gate PATH A nominal

**Priority**: High
**Context**: Entry → BE-ALL → verify Output tab has NO [BE-ERR] lines, stops=N for all accounts.
3 cycles. PASS: zero error popups, zero naked positions.
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-03 — SIM gate PATH A buf=0 edge case (short position)

**Priority**: High
**Context**: Entry short → BE-ALL buf=0t immediately. Verify [BE-ERR] lines appear if price moved
OR stops placed successfully if price still at entry. No naked positions. 1 cycle.
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-04 — SIM gate PATH B (QX-ALL then BE-ALL, 3 cycles)

**Priority**: High
**Context**: Entry → QX-ALL → BE-ALL arm → price trigger.
Verify PTT-QX-Stop* cancelled, PTT-BE-Stop-N placed. stops=N. 3 cycles.
Merges: DW-PTT-BE-FIX-02. Note: B107-DEFER-02 (Combo C re-test) covers the reverse direction
(BE-ALL then QX-ALL). Both remain open.
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-05 — SIM gate DW-B87 timing race cycle

**Priority**: High
**Context**: Entry → BE-ALL immediately (no wait). Must work (cancel sweep handles Submitted state).
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-06 — Spec update: close DW-B89/B88/B87 in spec HTML

**Priority**: Medium
**Context**: `specs/002-trade-copier-spec.html` sections #section-b89, #section-b88, #section-b87
must be updated to CLOSED status after all DW-B89 SIM gate paths pass.
**Action**: Director updates spec after full SIM gate PASS.
**Deferred to**: After all DW-B89 SIM paths green.

---

## Summary Table

| Category | Count | Items |
|----------|-------|-------|
| Closed this block (pipeline) | 3 | DW-B116, DW-B113, DW-B114 |
| Closed this block (F5 gate) | 2 | B112-DEFER-01, B107-DEFER-01 |
| Open deferred this block | 2 | DW-B114-TRACK (monitor), DW-B115 (Director triage) |
| Open: Combo D live re-test | 1 | B112-DEFER-02 |
| Open: Combo C live re-test | 1 | B107-DEFER-02 |
| Carry-forward B107 post-pipeline | 1 | DW-B107 |
| Carry-forward DW-B89 (unchanged) | 11 | DW-B42-01/02/03, DW-PTT-BE-FIX-01/02/03, DW-B89-DEFERRED-01/02/03/04/05/06 |

**Total open items**: 16 (was 18 — B112-DEFER-01 + B107-DEFER-01 closed by F5 pass)
