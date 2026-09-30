# B114 Deferred Backlog

**Block**: B114 — DW-B119 TryAdd Placement Race Fix
**Date**: 2026-08-27
**Pipeline status**: PIPELINE_COMPLETE (code phases) — pending F5 gate + SIM re-tests

---

## Items CLOSED This Block

| Item | Description | Closed By |
|------|-------------|-----------|
| DW-B119 | `_qxPendingFollowerCleanup` TryAdd placement race — cleanup never fired in NT8 Sim | B114-T1 |

---

## New Deferred Items — B114 Pipeline

### B114-DEFER-01 — Director F5 NT8 Compilation Gate

**Priority**: P0 — prerequisite for all SIM re-tests below
**Status**: PENDING — Director must press F5 in NinjaTrader 8 after sync
**Blocking**: YES — pipeline is PIPELINE_COMPLETE pending F5 green
**Context**: `ptt-sync-and-verify.ps1` completed with 16/16 OK, 0 MISMATCH (verified by both
engineer and verifier 2026-08-27). The F5 NinjaTrader 8 compilation step is the runtime compile
gate. It must produce "Compilation succeeded. 0 error(s), 0 warning(s)." after sync.
**Action**: Director presses F5 in NinjaTrader 8 after confirming sync pass.

---

### B114-DEFER-02 — Combo D SIM Re-Test with B114 Binary

**Priority**: P1 — required before QX-ALL is considered safe for live trading
**Status**: PENDING
**Scenario**: QX-ALL on 3-follower setup (Sim101/Sim102/Sim103 all in position with active ATM brackets).
**Pass criteria**:
- All followers show PTT-QX-T1/T2/T3 all Working after QX-ALL
- `[PTT-QX-CLEANUP]` log lines: 9 total (3 cleanups × 3 followers = one per target per follower)
- Native ATM Target1/2/3: ZERO remaining Working after cleanup fires
- `[PTT-QX-GUARD]` log lines: `follower submit (cancel-after):` present for each follower
- No unprotected position

**Fail criterion**: Any follower missing `[PTT-QX-CLEANUP]` lines, or any native Target* surviving Working.
**Deferred to**: Director SIM gate session (after B114-DEFER-01 green).

**Supersedes**: B113-DEFER-02 (same scenario, updated B114 binary).

---

### B114-DEFER-03 — Combo C SIM Re-Test + DW-B120 Re-Assessment

**Priority**: P1
**Status**: PENDING
**Part A — Combo C**: Confirm DW-B112 guard still intact after B114 change.
- BE-ALL then QX-ALL on 3-follower setup.
- Pass criterion: duplicate PTT-QX-T* cancel still blocked by `_qxCancelInProgress` guard.
- `[BE-DIAG] TryReplacePttBeBrackets: ... PTT-QX orders Working/Submitted, skipping recovery`
  log lines present (if BE fires after QX). No BE bracket conflicts.
**Part B — DW-B120 re-assessment**: If Combo D (B114-DEFER-02) shows any partial cleanup
(snapshot=3 residual behavior), DW-B120 escalates to P0 and requires a dedicated block.
If Combo D is fully clean (9/9 cleanup lines), DW-B120 is considered mitigated and closed.
**Deferred to**: Director SIM gate session (after B114-DEFER-01 green, after B114-DEFER-02 result).

**Supersedes**: B113-DEFER-03 (same scenario, updated B114 binary).

---

## Section K — Deferred Workbench (DW) Table

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| B114-DEFER-01 | Director F5 NT8 compilation gate after B114-T1 sync | P0 | B114 (immediate) | OPEN |
| B114-DEFER-02 | Combo D SIM re-test (QX-ALL, 3-follower, B114 binary) | P1 | B114 SIM gate | OPEN |
| B114-DEFER-03 | Combo C SIM re-test + DW-B120 re-assessment | P1 | B114 SIM gate | OPEN |
| DW-B120 | Partial ATM arm (snapshot=3); monitor after B114-DEFER-02 | P1 | B115 (conditional) | OPEN — MONITORED |

---

## Carry-Forward Items (from B113 — status unchanged unless noted)

Items below are copied from `docs/brain/B113/06-deferred-backlog.md`.
B114 changes do not affect any of these items unless explicitly noted.

B113-DEFER-01/02/03 are superseded by B114-DEFER-01/02/03 (same scenarios, B114 binary).
B107-DEFER-01 and B107-DEFER-02 are carry-forward from B107 — partially superseded by B114-DEFER-01/02 respectively.

---

### DW-B107 — MoveStopToBreakEven Step A Snapshots Stale PTT-BE-Target-* on Followers

**Priority**: P2
**Discovered**: 2026-08-25 live BE-ALL test
**Context**: `MoveStopToBreakEven` Step A collects target orders into a flat list with no
native-vs-PTT discrimination and no count cap. Stale `PTT-BE-Target-4` included in snapshot.
Same class as DW-B106 (fixed in B107-T1 for QX path — BE path not in scope of B107).
**Deferred to**: B108 (next pipeline block after current testing batch).
**Full brief**: `docs/brain/DW-B107/00-defect-brief.md`

---

### DW-B42-01 — T_BUG_QX_BE_01 Does Not Assert PTT-QX-T3

**Priority**: Low
**Deferred to**: B43 or first block where T3 confirmed in production use.

---

### DW-B42-02 — Live NT8 F5 Verification Required

**Priority**: High (superseded by B114-DEFER-01 — same gate)
**Deferred to**: Next live F5 session — superseded by B114-DEFER-01.

---

### DW-B42-03 — IsPttQxTarget Range Extension for Future Target Slots

**Priority**: Conditional (low unless T4/T5 slots added)
**Deferred to**: Block that adds 4th+ target slot.

---

### DW-PTT-BE-FIX-01 — DW-B85 Option A: Lazy Re-Resolve for Null Followers

**Priority**: Medium
**Deferred to**: Next PTT productionisation block.

---

### DW-PTT-BE-FIX-02 — SIM Gate: Path B 3-Cycle Runtime Verification

**Priority**: High
**Deferred to**: DW-B89 SIM gate session (combined with DW-B89-DEFERRED-04).

---

### DW-PTT-BE-FIX-03 — Pre-Existing Test Build Errors

**Priority**: High — blocks full test suite build
**Context**: Pre-existing errors in CopyEngineTests.cs stub infrastructure (83 errors) plus CS0433
Globals ambiguity. Confirmed pre-existing, unrelated to B114.
**Deferred to**: Dedicated test infrastructure remediation block.

---

### DW-B89-DEFERRED-01 — Ctrl+F5 NT8 Compilation Gate (DW-B89 Changes)

**Priority**: P0
**Deferred to**: Director (immediate prerequisite for all DW-B89 SIM paths).

---

### DW-B89-DEFERRED-02 — SIM Gate PATH A Nominal

**Priority**: High
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-03 — SIM Gate PATH A buf=0 Edge Case (Short Position)

**Priority**: High
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-04 — SIM Gate PATH B (QX-ALL then BE-ALL, 3 Cycles)

**Priority**: High
**Note**: B114-DEFER-03 is the complementary test covering the reverse direction. Both remain open.
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-05 — SIM Gate DW-B87 Timing Race Cycle

**Priority**: High
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-06 — Spec Update: Close DW-B89/B88/B87 in Spec HTML

**Priority**: Medium
**Context**: `specs/002-trade-copier-spec.html` sections #section-b89, #section-b88, #section-b87
must be updated to CLOSED status after all DW-B89 SIM gate paths pass.
**Deferred to**: After all DW-B89 SIM paths green.

---

## Summary

| Category | Count | Items |
|----------|-------|-------|
| Closed this block | 1 | DW-B119 |
| New deferred (B114 pipeline) | 3 | B114-DEFER-01 (F5), B114-DEFER-02 (Combo D), B114-DEFER-03 (Combo C + DW-B120 re-assess) |
| Monitored (not closed) | 1 | DW-B120 (pending B114-DEFER-02 SIM gate) |
| Superseded carry-forwards | 3 | B113-DEFER-01/02/03 (→ B114-DEFER-01/02/03) |
| Active carry-forward | 11 | DW-B107, DW-B42-01/02/03, DW-PTT-BE-FIX-01/02/03, DW-B89-DEFERRED-01..06 |

**Total open items**: 18 (4 new B114 + 1 monitored + 13 carry-forward)
