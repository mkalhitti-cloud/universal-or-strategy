# B115 Deferred Backlog

Block: B115 (Formalize DW-B119 + DW-B121 + DW-B122 Hotfixes)
Date: 2026-08-27
Status: PIPELINE_COMPLETE (coding phases)
Final Review: FINAL_PASS (docs/brain/B115/05-final-review.md)

---

## Items CLOSED This Block

| Item | Description | Closed By |
|------|-------------|-----------|
| DW-B119 | TryAdd-before-Execute placement race — pre-existing fix from B114-T1 confirmed in source; T_B113_01 provides test coverage | B114-T1 (code) + B115-T1 (test confirmation) |
| DW-B121 | TTL 2s→10s — T_B113_01 constants updated to AddSeconds(10) / AddSeconds(11) to mirror production TTL | B115-T1 |
| DW-B122 | Accepted-state guard — new B115Tests.cs validates guard logic; CopyEngine.cs parentheses clarity edit confirms operator precedence | B115-T2 (test) + B115-T3 (clarity) |

---

## Carry-Forward Open Items (from B107 and prior blocks)

B115 changes do not affect any of these items. All carried forward unchanged from
`docs/brain/B107/06-deferred-backlog.md`.

### DW-B107 — MoveStopToBreakEven Step A stale PTT-BE-Target-*

**Priority**: P2 — correctness violation, functionally benign in observed test
**Context**: `MoveStopToBreakEven` Step A collects target orders with no native-vs-PTT
discrimination. A stale `PTT-BE-Target-4` from a prior session was included in the snapshot.
Same class as DW-B106 (which fixed the QX path in B107-T1 — BE path not in scope of B107 or B115).
**Full brief**: `docs/brain/DW-B107/00-defect-brief.md`
**Deferred to**: B108 or designated remediation block.

---

### B107-DEFER-01 — F5 NinjaTrader 8 Compilation Gate

**Priority**: P0 — Director-owned prerequisite for SIM gate
**Context**: `ptt-sync-and-verify.ps1` must complete with 0 MISMATCH, followed by F5 in
NinjaTrader 8 producing "Compilation succeeded" (zero errors).
**Action**: Director presses F5 in NinjaTrader 8 after confirming sync pass.
**Status**: OPEN (Director-owned)

---

### B107-DEFER-02 — Combo C Live Re-Test

**Priority**: P1 — required before next live trading session
**Context**: Full behavioral validation of Combo C scenario (QX-ALL followed by BE-ALL,
stale partial-fill residue case). DW-B105 + DW-B106 code changes verified by code inspection;
runtime behavioral validation requires a live NT8 session.
**Status**: OPEN (Director-owned)

---

### DW-B42-01 — T_BUG_QX_BE_01 does not assert PTT-QX-T3

**Priority**: Low
**Context**: T_BUG_QX_BE_01 asserts for PTT-QX-T1 and PTT-QX-T2 only. T3 path untested.
**Deferred to**: First block where T3 confirmed in production use.

---

### DW-B42-02 — Live NT8 F5 verification required

**Priority**: High — required before next live trading session
**Context**: Full behavioral verification of QX-ALL → BE-ALL and BE-ALL → QX-ALL sequences.
**Deferred to**: Next live F5 session.

---

### DW-B42-03 — IsPttQxTarget range extension for T4/T5 slots

**Priority**: Conditional (low unless T4/T5 slots added)
**Context**: Current range `name[8] >= '1' && name[8] <= '3'`. Future T4/T5 would require update.
**Deferred to**: Block that adds 4th+ target slot.

---

### DW-PTT-BE-FIX-01 — Lazy re-resolve for null followers (Option A)

**Priority**: Medium
**Context**: Option A lazy re-resolution in AllAccounts() for followers not in Account.All at LoadRules() time.
**Deferred to**: Next PTT productionisation block.

---

### DW-PTT-BE-FIX-02 — SIM gate Path B 3-cycle runtime verification

**Priority**: High — required before next live session with QX-ALL then BE-ALL
**Context**: T1 (DW-B86) stop name guard fix requires live SIM validation (3 cycles, Path B).
**Deferred to**: DW-B89 SIM gate session.

---

### DW-PTT-BE-FIX-03 — Pre-existing test build errors

**Priority**: High — blocks full test suite build
**Context**: Pre-existing errors in CopyEngineTests.cs stub infrastructure (83 errors) plus
CS0433 Globals ambiguity. Confirmed pre-existing, unrelated to B115.
**Deferred to**: Dedicated test infrastructure remediation block.

---

### DW-B89-DEFERRED-01 — Ctrl+F5 NT8 compilation gate (DW-B89 changes)

**Priority**: P0 — blocks DW-B89 SIM gate
**Deferred to**: Director (immediate, prerequisite for all SIM paths below).

---

### DW-B89-DEFERRED-02 — SIM gate PATH A nominal

**Priority**: High
**Context**: Entry → BE-ALL → verify zero [BE-ERR] lines, stops=N for all accounts. 3 cycles.
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-03 — SIM gate PATH A buf=0 edge case (short position)

**Priority**: High
**Context**: Entry short → BE-ALL buf=0t immediately. Verify no naked positions.
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-04 — SIM gate PATH B (QX-ALL → BE-ALL, 3 cycles)

**Priority**: High
**Context**: Entry → QX-ALL → BE-ALL arm → price trigger. Verify PTT-QX-Stop* cancelled,
PTT-BE-Stop-N placed. stops=N. 3 cycles.
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-05 — SIM gate DW-B87 timing race cycle

**Priority**: High
**Context**: Entry → BE-ALL immediately (no wait). Cancel sweep handles Submitted state.
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-06 — Spec update: close DW-B89/B88/B87 in spec HTML

**Priority**: Medium
**Context**: `specs/002-trade-copier-spec.html` sections #section-b89, #section-b88, #section-b87
must be updated to CLOSED after all DW-B89 SIM gate paths pass.
**Deferred to**: After all DW-B89 SIM paths green.

---

## New Deferred Items Added This Block

**None.** B115 is a formalization block (three hotfixes already in production source).
No new defects or scope items discovered during pipeline execution.

---

## Summary

| Category | Count | Items |
|----------|-------|-------|
| Closed this block | 3 | DW-B119, DW-B121, DW-B122 |
| New deferred (this block) | 0 | None |
| Carry-forward from B107 + DW-B89 (unchanged) | 15 | DW-B107, B107-DEFER-01/02, DW-B42-01/02/03, DW-PTT-BE-FIX-01/02/03, DW-B89-DEFERRED-01/02/03/04/05/06 |

**Total open items**: 15
