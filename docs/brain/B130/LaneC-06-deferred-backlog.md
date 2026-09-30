# B130-LaneC Deferred Backlog

**Block**: B130-LaneC
**Defect**: DW-B107 (MoveStopToBreakEven Step A snapshots stale PTT-BE-Target-* orders)
**Date**: 2026-09-01
**Status**: FINAL_PASS

---

## Items CLOSED This Block

| Item | Description | Closed By |
|------|-------------|-----------|
| **DW-B107 (production fix)** | `SnapshotBeTargets` private helper implemented in `CopyEngine.cs` (two-pass native-first collect, CYC=7). `MoveStopToBreakEven` Step A calls it at L4019 instead of inline loop. Hard cap `while (targets.Count > 3)` at L4023-4024. | Prior block (pre-B130). Confirmed by direct code read in B130-LaneC plan review. |
| **DW-B107 (tests)** | 3 new `[Fact]` tests added to `src/PropTraderTools/Tests/B130Tests.cs`: `B130_DW107_SnapshotBeTargetsFiltersStaleOrders`, `B130_DW107_HardCapTrimsSnapshotToThreeTargets`, `B130_DW107_NonTargetOrdersProduceEmptySnapshot`. All 3 pass. 8/8 total B130 tests pass. | B130-LaneC T3 (this block). BUILD_PASS + VERIFY_PASS confirmed. |

---

## New Deferred Items From This Block

None introduced by B130-LaneC. This was a tests-only block with no new production code. No new
defects, regressions, or architectural gaps identified.

---

## Carry-Forward Open Items (unchanged from B107 deferred backlog)

### DW-B107-SIM — Director SIM Gate: BE-ALL stale order fix verification

**Priority**: P2
**Owner**: Director
**Context**: The production fix (`SnapshotBeTargets` + hard cap) and 3 behavioral tests are
complete and verified. Full end-to-end behavioral confirmation requires a live NT8 session:
Sim101 (master) + Sim102/103/104 (followers), BE-ALL with Copier ON, stale `PTT-BE-Target-4`
present in acc.Orders. Pass criterion: exactly 3 cancel lines per follower (`PTT-BE-Target-1..3`)
— no `PTT-BE-Target-4` cancel line.
**Deferred to**: Director SIM gate session.

---

### B107-DEFER-01 — F5 NinjaTrader 8 Compilation Gate

**Priority**: P0 (prerequisite for SIM gate)
**Owner**: Director
**Context**: `ptt-sync-and-verify.ps1` must complete with 0 MISMATCH. Director presses F5 in
NinjaTrader 8 to confirm "Compilation succeeded" after sync. Prerequisite for B107-DEFER-02.
**Deferred to**: Director (immediate, before next SIM test).

---

### B107-DEFER-02 — Combo C Live Re-Test

**Priority**: P1
**Owner**: Director
**Context**: Full behavioral validation of Combo C scenario (BE-ALL then QX-ALL with stale
partial-fill residue). Test sequence: Enter position (Sim101+102/103/104, Copier ON) -> BE-ALL
-> confirm 3 targets per follower -> QX-ALL -> confirm PTT-QX-T1/T2/T3 only, no T4.
Pass criterion: no extra OCO pair, no unprotected position.
**Deferred to**: Director SIM gate session (after B107-DEFER-01 green).

---

## Carry-Forward Items (from DW-B89 — status unchanged)

Items below are copied from `docs/brain/B107/06-deferred-backlog.md`.
B130-LaneC changes do not affect any of these items.

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

**Priority**: High
**Context**: The two bug directions can only be fully verified in a live NT8 session:
- Direction 1: Quick All -> BE All must place targets at BE price (not bare stop)
- Direction 2: BE All -> Quick All must start from clean slate
**Deferred to**: Next live F5 session (local compile + runtime confirm)

---

### DW-B42-03 — IsPttQxTarget range extension for future target slots

**Priority**: Conditional (low unless T4/T5 slots added)
**Context**: Current range `name[8] >= '1' && name[8] <= '3'` matches B41 two-OCO-group design.
If a future block adds PTT-QX-T4 or T5, `IsPttQxTarget` must be updated.
**Deferred to**: Block that adds 4th+ target slot.

---

### DW-PTT-BE-FIX-01 — Lazy re-resolve for null followers

**Priority**: Medium
**Context**: When a follower account is not in Account.All at LoadRules() time, the Option B
warning is emitted. Option A would re-attempt resolution lazily in AllAccounts() when the
account later appears in Account.All. Per spec, Option A is deferred.
**Deferred to**: Next PTT productionisation block.

---

### DW-PTT-BE-FIX-02 — SIM gate PATH B 3-cycle runtime verification

**Priority**: High
**Context**: T1 (DW-B86) fixes the stop name guard but full SIM verification of Path B
(QX-ALL then BE-ALL, 3 cycles) requires a live NT8 session.
**Deferred to**: DW-B89 SIM gate session (combined with DW-B89-DEFERRED-04).

---

### DW-PTT-BE-FIX-03 — Pre-existing test build errors

**Priority**: High (blocks full test suite build)
**Context**: Pre-existing errors in CopyEngineTests.cs stub infrastructure (83 errors) plus
CS0433 Globals ambiguity at CopyEngine.cs:L3350. Confirmed pre-existing, unrelated to B130-LaneC.
**Deferred to**: Dedicated test infrastructure remediation block.

---

### DW-B89-DEFERRED-01 — Ctrl+F5 NT8 compilation gate (DW-B89 changes)

**Priority**: P0
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
**Context**: Entry short -> BE-ALL buf=0t immediately. 1 cycle.
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-04 — SIM gate PATH B (QX-ALL then BE-ALL, 3 cycles)

**Priority**: High
**Note**: B107-DEFER-02 (Combo C re-test) covers the reverse direction (BE-ALL then QX-ALL).
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-05 — SIM gate DW-B87 timing race cycle

**Priority**: High
**Context**: Entry -> BE-ALL immediately (no wait). Must work (cancel sweep handles Submitted state).
**Deferred to**: Director after DW-B89-DEFERRED-01 green.

---

### DW-B89-DEFERRED-06 — Spec update: close DW-B89/B88/B87 in spec HTML

**Priority**: Medium
**Context**: `specs/002-trade-copier-spec.html` sections for DW-B89/B88/B87 must be updated to
CLOSED status after all DW-B89 SIM gate paths pass.
**Deferred to**: After all DW-B89 SIM paths green.

---

## Summary

| Category | Count | Items |
|----------|-------|-------|
| Closed this block | 2 | DW-B107 production fix (pre-implemented, confirmed), DW-B107 tests (3 new [Fact]) |
| New deferred | 0 | None — tests-only block introduced no new gaps |
| Carry-forward DW-B107 Director-owned | 3 | DW-B107-SIM, B107-DEFER-01, B107-DEFER-02 |
| Carry-forward from DW-B89/B89 (unchanged) | 12 | DW-B42-01/02/03, DW-PTT-BE-FIX-01/02/03, DW-B89-DEFERRED-01..06 |

**Total open items**: 15 (3 DW-B107 Director-owned + 12 carry-forward from prior blocks)
