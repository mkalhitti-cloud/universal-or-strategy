# B123 Deferred Backlog

**Block**: B123 (DW-B133 — QAll2t forced 2-target split)
**Date**: 2026-08-27
**Status**: FINAL_PASS — coding phases complete; Director-owned SIM gates deferred

---

## Items CLOSED This Block

| Item | Description | Closed By |
|------|-------------|-----------|
| DW-B133 | QAll2t button fires ATM snapshot target count instead of forced 2-target split | B123-T1 |

---

## New Deferred Items — B123 Block

### DW-B133-01 — DIAG for-loop extraction (optional CYC reduction)

**Priority**: P3 (cosmetic — implementation already achieved CYC=7)
**Context**: Architecture plan specified `Execute(forcedTargets)` at CYC=8 including a
`for (_d = 0; _d < forcedTargets.Count; _d++)` per-item DIAG log loop (Branch 6).
Engineer replaced the loop with a single `forcedTargetCount=N` summary log line, achieving
CYC=7 (better than specified). DW-B133-01 (extract to `LogLeaderDiag()` helper) was originally
filed to enable future CYC=7 — this is now already achieved. Item retained for documentation;
functionally pre-resolved by the B123 implementation.
**Unblocked by**: N/A — already resolved. No action required.
**Target block**: Documentation only; no engineering work needed.

---

### DW-B133-SIM-01 — Live SIM gate: QAll2t 2-target verification (Director-owned)

**Priority**: P1 — required before next live trading session using QAll2t button
**Context**: B123 fixes the QAll2t path to fire forced 2-target brackets. Full behavioral
validation requires a live NT8 SIM session.

**Test sequence**:
1. Enter a 7-contract MES position on Sim101 (leader) with Copier ON for 3 follower accounts.
2. Ensure a 3-target ATM template is loaded (to confirm the forced split wins over snapshot).
3. Press the QAll2t button.
4. Verify Output tab:
   - `[PTT-QX-2T-ALL] button: Sim101 ...MES... qty=7 T1=4 T2=3` appears.
   - `[PTT-QX-2T-ALL] GlobalQuickExit fired (forced 2-target)` appears for leader account.
   - `[PTT-QX-2T-ALL] leader: Sim101 ...MES... qty=7 forcedTargetCount=2` appears.
   - Exactly 2 OCO bracket pairs per account (PTT-QX-T1 + PTT-QX-T2 only; no T3).

**Pass criterion**: `forcedTargetCount=2` in Output, exactly 2 OCO pairs per account (T1=4, T2=3),
no T3 bracket.
**Fail criterion**: T3 bracket appears, or `forcedTargetCount=3`, or Output shows no `[PTT-QX-2T-ALL]` prefix.
**Prerequisite**: F5 NinjaTrader 8 compilation gate must pass first.
**Deferred to**: Director (next SIM session after B123 F5 gate).

---

### DW-B133-SIM-02 — Live SIM regression: QAll button still fires ATM-snapshot targets (Director-owned)

**Priority**: P1 — required before next live trading session
**Context**: B123 is additive only — the no-arg `Execute()` path (called by the normal QAll button)
is unchanged. T_B123_05 provides automated regression guard (reflection test). This SIM gate
provides runtime behavioral confirmation.

**Test sequence**:
1. Enter a position with a 3-target ATM loaded on leader + followers.
2. Press the normal QAll button (not QAll2t).
3. Verify Output tab:
   - `[PTT-QX-ALL] GlobalQuickExit fired` appears (no "2T" tag in prefix).
   - 3 OCO bracket pairs submitted per account (PTT-QX-T1, PTT-QX-T2, PTT-QX-T3).
   - No `[PTT-QX-2T-ALL]` prefix appears.

**Pass criterion**: 3 OCO pairs per account; `[PTT-QX-ALL]` prefix (not `[PTT-QX-2T-ALL]`).
**Fail criterion**: Only 2 OCO pairs, or `[PTT-QX-2T-ALL]` prefix on the QAll button path.
**Deferred to**: Director (same SIM session as DW-B133-SIM-01).

---

## Carry-Forward Items (from B107 — status unchanged)

Items below are copied from `docs/brain/B107/06-deferred-backlog.md`.
B123 changes (PttGlobalQuickExit.cs, TradeCopierPanel.cs, B123Tests.cs) do not affect any of these.

---

### DW-B107 — MoveStopToBreakEven Step A snapshots stale PTT-BE-Target-* on followers

**Priority**: P2 — correctness violation, functionally benign in observed test
**Context**: Sim102/103/104 each submitted 4 OCO bracket pairs on a 3-target ATM. `MoveStopToBreakEven`
Step A collects target orders with no native-vs-PTT discrimination and no count cap. A stale
`PTT-BE-Target-4` from a prior session was included and an extra OCO pair submitted.
**Full brief**: `docs/brain/DW-B107/00-defect-brief.md`
**Deferred to**: B108 (next pipeline block after current testing batch).

---

### B107-DEFER-01 — F5 NinjaTrader 8 Compilation Gate (B107 changes)

**Priority**: P0 — prerequisite for SIM gate and go-live
**Deferred to**: Director (immediate, prerequisite for B107-DEFER-02).

---

### B107-DEFER-02 — Combo C Live Re-Test (DW-B105 + DW-B106 behavioral validation)

**Priority**: P1 — required before next live trading session involving BE-ALL then QX-ALL
**Deferred to**: Director SIM gate session (after B107-DEFER-01 green).

---

### DW-B42-01 — T_BUG_QX_BE_01 does not assert PTT-QX-T3

**Priority**: Low
**Deferred to**: B43 or first block where T3 is confirmed in production use.

---

### DW-B42-02 — Live NT8 F5 verification required (B42 bugs)

**Priority**: High — required before next live trading session
**Deferred to**: Next live F5 session.

---

### DW-B42-03 — IsPttQxTarget range extension for future T4/T5 target slots

**Priority**: Conditional (low unless T4/T5 slots added)
**Deferred to**: Block that adds 4th+ target slot.

---

### DW-PTT-BE-FIX-01 — Option A: Lazy re-resolve for null followers

**Priority**: Medium
**Deferred to**: Next PTT productionisation block.

---

### DW-PTT-BE-FIX-02 — SIM gate: Path B 3-cycle runtime verification

**Priority**: High — required before next live trading session with QX-ALL then BE-ALL
**Deferred to**: DW-B89 SIM gate session (combined with DW-B89-DEFERRED-04).

---

### DW-PTT-BE-FIX-03 — Pre-existing test build errors (CopyEngineTests.cs + CS0433 Globals)

**Priority**: High — blocks full test suite build
**Deferred to**: Dedicated test infrastructure remediation block.

---

### DW-B89-DEFERRED-01 — Ctrl+F5 NT8 compilation gate (DW-B89 changes)

**Priority**: P0 — blocks DW-B89 SIM gate
**Deferred to**: Director (immediate, prerequisite for all SIM paths below).

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
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-05 — SIM gate DW-B87 timing race cycle

**Priority**: High
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-06 — Spec update: close DW-B89/B88/B87 in spec HTML

**Priority**: Medium
**Deferred to**: After all DW-B89 SIM paths green.

---

## Summary

| Category | Count | Items |
|----------|-------|-------|
| Closed this block | 1 | DW-B133 |
| New deferred (B123 pipeline) | 3 | DW-B133-01 (P3, pre-resolved), DW-B133-SIM-01 (P1 Director), DW-B133-SIM-02 (P1 Director) |
| Carry-forward from B107 (unchanged) | 14 | DW-B107, B107-DEFER-01/02, DW-B42-01/02/03, DW-PTT-BE-FIX-01/02/03, DW-B89-DEFERRED-01/02/03/04/05/06 |

**Total open items**: 17 (3 new + 14 carry-forward)
