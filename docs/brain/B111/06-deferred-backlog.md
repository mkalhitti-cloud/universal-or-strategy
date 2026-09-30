# B111-T1 Deferred Backlog

**Block**: B111-T1
**Date**: 2026-08-28
**Status**: PIPELINE_COMPLETE (coding phases)
**Final Review**: docs/brain/B111/05-final-review.md (FINAL_PASS)

---

## Items Resolved This Block

| Item | Description | Closed By |
|------|-------------|-----------|
| DW-B111 | `_beReplaceAttempts` Counter Reset in Timer Callback causes Infinite BE-Retry Loop (Combo D) | B111-T1 Change A + Changes B-1/B-2/B-3 |
| DW-B112 | `_qxCancelInProgress` Guard Cleared Before Async Cancel Events Arrive (Combo C) | B111-T1 Change C (structural PTT-QX presence check) |

---

## Current Block (B111-T1) Deferred Items

### B111-DEFER-01 -- PttBreakEvenSwap.cs Secondary Fix

- **Description**: Skip `CancelQxBrackets` call at `PttBreakEvenSwap.Execute` (~L70) when called from a retry path. Add an `isRetry` parameter (or equivalent structural check) to prevent spurious cancel events that trigger `TryReplacePttBeBrackets` during retries.
- **Priority**: P2 -- does not affect primary correctness of B111 fixes
- **Rationale**: Not required for DW-B111/B112 correctness. The primary fix (counter cap at 5 + removal of timer-callback TryRemove) terminates the infinite loop regardless of whether `CancelQxBrackets` fires on retry. The signature change requires modifying `PttBreakEvenSwap.Execute` and all call sites in `CopyEngine.cs`. Deferred per architect Section 5 decision.
- **Target block**: B112 or next available block.

### B111-DEFER-02 -- Combo D + Combo C Live SIM Re-Test

- **Description**: Director-owned gate. Restart NT8 (fresh order book). Run Combo D (QX-ALL -> BE-ALL) to verify the BE-retry loop terminates after cap=5. Run Combo C (BE-ALL -> QX-ALL) to verify the PTT-QX structural presence guard fires and no BE brackets appear on top of QX brackets.
- **Priority**: P1 -- required before next live trading session
- **Pass criteria**: Combo D -- BE-retry log shows "attempt N/5" and terminates; no infinite loop. Combo C -- `[BE-DIAG] ... skipping recovery (DW-B112)` lines appear; no unprotected positions.
- **Target**: Immediate (Director-owned, after B111-DEFER-03 green).

### B111-DEFER-03 -- F5 NinjaTrader 8 Compilation Gate

- **Description**: Director presses F5 in NinjaTrader 8 after confirming ptt-sync-and-verify.ps1 pass (0 MISMATCH, 16 files). Compilation must produce zero errors.
- **Priority**: P0 -- prerequisite for B111-DEFER-02.
- **Context**: Sync + MD5 verify already passed (ticket-1-verification.md: "0 MISMATCH, 16/16 OK"). F5 is the runtime compile gate and is Director-owned per plan Section 10.
- **Target**: Immediate (Director-owned).

---

## Carry-Forward from Prior Blocks (B107 Backlog -- Open Items)

Items from `docs/brain/B107/06-deferred-backlog.md` that remain open and are not resolved by B111-T1.
B111-T1 changes do not affect any of these items.

---

### DW-B107 -- MoveStopToBreakEven Stale PTT-BE-Target-* on Followers

**Priority**: P2 -- correctness violation, functionally benign in observed test
**Context**: `MoveStopToBreakEven` Step A collects target orders into a flat list with no native-vs-PTT discrimination and no count cap. A stale `PTT-BE-Target-4` from a prior session can be included, causing an extra OCO pair to be submitted on followers.
**Same class as**: DW-B106 (which fixed the QX path in B107). BE path not in scope for B107 or B111.
**Deferred to**: B112 (next pipeline block).
**Full brief**: `docs/brain/DW-B107/00-defect-brief.md`
**CYC note**: Fix requires extraction of the Step A loop into a new `SnapshotBeTargets` helper to keep `MoveStopToBreakEven` at CYC <= 8.

---

### B107-DEFER-01 -- F5 NinjaTrader 8 Compilation Gate (B107 Changes)

**Priority**: P0
**Context**: Subsumed by B111-DEFER-03. A single F5 press after the B111-T1 sync validates both B107 and B111 changes.
**Deferred to**: Director (immediate -- merged with B111-DEFER-03).

---

### B107-DEFER-02 -- Combo C Live Re-Test (B107 DW-B105 + DW-B106 Validation)

**Priority**: P1
**Context**: Extended by B111-T1. The B111-T1 Combo C re-test (B111-DEFER-02) covers the same scenario and additionally validates the DW-B112 PTT-QX presence guard. A single Combo C run after B111-T1 F5 green validates both B107 and B111 fixes.
**Deferred to**: Director (merged with B111-DEFER-02).

---

### DW-B42-01 -- T_BUG_QX_BE_01 Does Not Assert PTT-QX-T3

**Priority**: Low
**Context**: T_BUG_QX_BE_01 asserts PTT-QX-T1 and PTT-QX-T2 only. The production predicate also accepts T3.
**Deferred to**: B43 or first block where T3 is confirmed in production use.
**Fix**: Add `Assert.True(IsPttQxTargetInline("PTT-QX-T3"))` to T_BUG_QX_BE_01.

---

### DW-B42-02 -- Live NT8 F5 Verification (Direction 1 and Direction 2)

**Priority**: High -- required before next live trading session
**Context**: Direction 1 (Quick All -> BE All must place targets at BE price) and Direction 2 (BE All -> Quick All clean slate) can only be fully verified in a live NT8 session.
**Deferred to**: Next live F5 session.

---

### DW-B42-03 -- IsPttQxTarget Range Extension for Future T4/T5 Slots

**Priority**: Conditional (low unless T4/T5 slots added)
**Context**: Current range `name[8] >= '1' && name[8] <= '3'`. Future 4th+ target slots require update.
**Deferred to**: Block that adds 4th+ target slot.

---

### DW-PTT-BE-FIX-01 -- Lazy Re-Resolve for Null Followers (DW-B85 Option A)

**Priority**: Medium
**Context**: When a follower account is not in Account.All at LoadRules() time, Option B warning is emitted. Option A (lazy re-resolve in AllAccounts()) is deferred.
**Deferred to**: Next PTT productionisation block.

---

### DW-PTT-BE-FIX-02 -- SIM Gate Path B 3-Cycle Runtime Verification

**Priority**: High -- required before next live trading session with QX-ALL then BE-ALL sequence
**Context**: Full SIM verification of Path B (QX-ALL then BE-ALL, 3 cycles) requires a live NT8 session.
**Deferred to**: DW-B89 SIM gate session.

---

### DW-PTT-BE-FIX-03 -- Pre-Existing Test Build Errors (83 Errors, CS0433)

**Priority**: High -- blocks full test suite build
**Context**: Pre-existing errors in CopyEngineTests.cs stub infrastructure (83 errors) plus CS0433 Globals ambiguity at CopyEngine.cs:L3350. Also blocks `complexity_audit.py` (script not found at scripts/complexity_audit.py). Both B111-T1 Layer 2 and Layer 3 scans fell back to manual CYC verification due to this gap.
**Deferred to**: Dedicated test infrastructure remediation block.

---

### DW-B89-DEFERRED-01 -- Ctrl+F5 NT8 Compilation Gate (DW-B89 Changes)

**Priority**: P0 -- blocks DW-B89 SIM gate
**Context**: Director must confirm Ctrl+F5 in NinjaTrader for DW-B89 changes produces zero errors.
**Deferred to**: Director (immediate, prerequisite for all SIM paths below).

---

### DW-B89-DEFERRED-02 -- SIM Gate PATH A Nominal

**Priority**: High
**Context**: Entry -> BE-ALL -> verify Output tab has NO [BE-ERR] lines, stops=N for all accounts. 3 cycles.
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-03 -- SIM Gate PATH A buf=0 Edge Case (Short Position)

**Priority**: High
**Context**: Entry short -> BE-ALL buf=0t immediately. Verify correct behavior, NO naked positions.
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-04 -- SIM Gate PATH B (QX-ALL then BE-ALL, 3 Cycles)

**Priority**: High
**Context**: Entry -> QX-ALL -> BE-ALL arm -> price trigger. Verify PTT-QX-Stop* cancelled, PTT-BE-Stop-N placed. Merges DW-PTT-BE-FIX-02.
**Note**: B111-DEFER-02 (Combo C re-test) is the complementary test covering the reverse direction. Both remain open.
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-05 -- SIM Gate DW-B87 Timing Race Cycle

**Priority**: High
**Context**: Entry -> BE-ALL immediately (no wait). Must work (cancel sweep handles Submitted state).
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-06 -- Spec Update: Close DW-B89/B88/B87 in Spec HTML

**Priority**: Medium
**Context**: `specs/002-trade-copier-spec.html` sections must be updated to CLOSED after all DW-B89 SIM gate paths pass.
**Deferred to**: After all DW-B89 SIM paths green.

---

## Summary

| Category | Count | Items |
|----------|-------|-------|
| Closed this block | 2 | DW-B111, DW-B112 |
| New deferred (B111-T1 pipeline) | 3 | B111-DEFER-01 (PttBreakEvenSwap), B111-DEFER-02 (live re-test), B111-DEFER-03 (F5 gate) |
| Carry-forward from B107 / DW-B89 (unchanged) | 14 | DW-B107, B107-DEFER-01/02, DW-B42-01/02/03, DW-PTT-BE-FIX-01/02/03, DW-B89-DEFERRED-01/02/03/04/05/06 |

**Total open items**: 17 (3 new + 14 carry-forward)
