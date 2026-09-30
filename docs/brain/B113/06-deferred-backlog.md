# B113 Deferred Backlog

**Block**: B113 — DW-B117 Cancel-After Fix
**Date**: 2026-08-26
**Pipeline status**: PIPELINE_COMPLETE (code phases) — pending F5 gate + live re-tests

---

## Items CLOSED This Block

| Item | Description | Closed By |
|------|-------------|-----------|
| DW-B117 | QX-ALL PTT-QX-T2/T3 missing on followers due to NT8 ATM re-arm after pre-cancel | B113-T1 |
| DW-B117-DIAG | Diagnostic probe in OnOrderUpdate (L1230-1250) | B113-T1 (REMOVED-B113-T1) |

---

## New Deferred Items — B113 Pipeline

### B113-DEFER-01 — Director F5 NT8 Compilation Gate

**Priority**: P0 — prerequisite for all live re-tests below
**Status**: PENDING — Director must press F5 in NinjaTrader 8 after sync
**Blocking**: YES — pipeline is PIPELINE_COMPLETE pending F5 green
**Context**: `ptt-sync-and-verify.ps1` completed with 16/16 OK, 0 MISMATCH.
The F5 NinjaTrader 8 compilation step is the runtime compile gate. It must produce
"Compilation succeeded. 0 error(s), 0 warning(s)." after sync.
**Action**: Director presses F5 in NinjaTrader 8 after confirming sync pass.

---

### B113-DEFER-02 — Live Re-Test Combo D

**Priority**: P1 — required before QX-ALL is considered safe for live trading
**Status**: PENDING
**Scenario**: QX-ALL then BE-ALL on 3-follower setup (Sim101/Sim102/Sim103 all in position)
**Pass criterion**:
- All followers show PTT-QX-T1/T2/T3 all Working after QX-ALL
- No re-armed native ATM Target1/2/3 remaining Working after cleanup fires
- `[PTT-QX-CLEANUP]` log lines confirm one-for-one cancels (3 cleanups x N followers)
- `[PTT-QX-GUARD]` log lines show `follower submit (cancel-after):` (not `pre-cancel`)
- Zero `[DW-B117-DIAG]` log lines (probe removed — any hit = regression)
- No unprotected position

**Fail criterion**: Any follower missing PTT-QX-T2 or T3 Working after QX-ALL.
**Deferred to**: Director SIM gate session (after B113-DEFER-01 green).

---

### B113-DEFER-03 — Live Re-Test Combo C

**Priority**: P1 — required before QX-ALL is considered safe for live trading
**Status**: PENDING
**Scenario**: BE-ALL then QX-ALL on 3-follower setup
**Pass criterion**:
- DW-B112 guard fires correctly: `[BE-DIAG] TryReplacePttBeBrackets: ... PTT-QX orders Working/Submitted, skipping recovery` log lines present (if BE fires after QX)
- All followers show PTT-QX-T1/T2/T3 all Working after QX-ALL
- No PTT-BE brackets submitted on top of PTT-QX brackets (zero BE bracket conflicts)
- No unprotected position

**Fail criterion**: Any follower missing T2 or T3, or any DW-B112 guard bypass.
**Deferred to**: Director SIM gate session (after B113-DEFER-01 green).

---

## Section K — Deferred Workbench (DW) Table

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B113-01 | Director F5 NT8 compilation gate after B113-T1 sync | P0 | B113 (immediate) | OPEN |
| DW-B113-02 | Live re-test Combo D (QX-ALL then BE-ALL, 3-follower) | P1 | B113 SIM gate | OPEN |
| DW-B113-03 | Live re-test Combo C (BE-ALL then QX-ALL, 3-follower) | P1 | B113 SIM gate | OPEN |

---

## Carry-Forward Items (from B107 — status unchanged unless noted)

Items below are copied from `docs/brain/B107/06-deferred-backlog.md`.
B113 changes do not affect any of these items unless explicitly noted.

---

### DW-B107 — MoveStopToBreakEven Step A snapshots stale PTT-BE-Target-* on followers

**Priority**: P2
**Discovered**: 2026-08-25 live BE-ALL test
**Context**: `MoveStopToBreakEven` Step A collects target orders into a flat list with no
native-vs-PTT discrimination and no count cap. Stale `PTT-BE-Target-4` included in snapshot.
Same class as DW-B106 (fixed in B107-T1 for QX path — BE path not in scope of B107).
**Deferred to**: B108 (next pipeline block after current testing batch).
**Full brief**: `docs/brain/DW-B107/00-defect-brief.md`

---

### B107-DEFER-01 — F5 NinjaTrader 8 Compilation Gate (B107 changes)

**Priority**: P0
**Context**: `ptt-sync-and-verify.ps1` completed with 0 MISMATCH (16 files MD5-verified).
F5 NT8 compilation must produce "Compilation succeeded" (zero errors) after sync.
**Status**: Carry-forward — superseded by B113-DEFER-01 (same gate, later binary).

---

### B107-DEFER-02 — Combo C Live Re-Test (B107 context)

**Priority**: P1
**Context**: DW-B105 + DW-B106 code changes verified. Full behavioral validation of
Combo C (BE-ALL then QX-ALL) requires live NT8 session.
**Status**: Carry-forward — partially superseded by B113-DEFER-03 (same scenario, B113 binary).

---

### DW-B42-01 — T_BUG_QX_BE_01 does not assert PTT-QX-T3

**Priority**: Low
**Deferred to**: B43 or first block where T3 confirmed in production use.

---

### DW-B42-02 — Live NT8 F5 verification required

**Priority**: High
**Deferred to**: Next live F5 session (local compile + runtime confirm).

---

### DW-B42-03 — IsPttQxTarget range extension for future target slots

**Priority**: Conditional (low unless T4/T5 slots added)
**Deferred to**: Block that adds 4th+ target slot.

---

### DW-PTT-BE-FIX-01 — DW-B85 Option A: Lazy re-resolve for null followers

**Priority**: Medium
**Deferred to**: Next PTT productionisation block.

---

### DW-PTT-BE-FIX-02 — SIM gate: Path B 3-cycle runtime verification

**Priority**: High
**Deferred to**: DW-B89 SIM gate session (combined with DW-B89-DEFERRED-04).

---

### DW-PTT-BE-FIX-03 — Pre-existing test build errors

**Priority**: High — blocks full test suite build
**Context**: Pre-existing errors in CopyEngineTests.cs stub infrastructure (83 errors) plus CS0433
Globals ambiguity. Confirmed pre-existing, unrelated to B113.
**Deferred to**: Dedicated test infrastructure remediation block.

---

### DW-B89-DEFERRED-01 — Ctrl+F5 NT8 compilation gate (DW-B89 changes)

**Priority**: P0
**Deferred to**: Director (immediate prerequisite for all DW-B89 SIM paths).

---

### DW-B89-DEFERRED-02 — SIM gate PATH A nominal

**Priority**: High
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-03 — SIM gate PATH A buf=0 edge case (short position)

**Priority**: High
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-04 — SIM gate PATH B (QX-ALL then BE-ALL, 3 cycles)

**Priority**: High
**Note**: B113-DEFER-03 is the complementary test covering the reverse direction. Both remain open.
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-05 — SIM gate DW-B87 timing race cycle

**Priority**: High
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-06 — Spec update: close DW-B89/B88/B87 in spec HTML

**Priority**: Medium
**Context**: `specs/002-trade-copier-spec.html` sections #section-b89, #section-b88, #section-b87
must be updated to CLOSED status after all DW-B89 SIM gate paths pass.
**Deferred to**: After all DW-B89 SIM paths green.

---

## Summary

| Category | Count | Items |
|----------|-------|-------|
| Closed this block | 2 | DW-B117, DW-B117-DIAG |
| New deferred (B113 pipeline) | 3 | B113-DEFER-01 (F5), B113-DEFER-02 (Combo D), B113-DEFER-03 (Combo C) |
| Carry-forward from B107 | 2 | B107-DEFER-01, B107-DEFER-02 |
| Carry-forward from DW-B89/B42/earlier | 12 | DW-B42-01/02/03, DW-PTT-BE-FIX-01/02/03, DW-B107, DW-B89-DEFERRED-01/02/03/04/05/06 |

**Total open items**: 17 (3 new B113 + 14 carry-forward)
