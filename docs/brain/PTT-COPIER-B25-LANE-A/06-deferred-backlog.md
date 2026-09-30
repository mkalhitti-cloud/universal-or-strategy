# PTT-COPIER Deferred Work Backlog — B25 Lane A Append

This file is appended by ptt-plan-reviewer at the end of each block's Phase 5 final review.
Items are added when work is identified but out-of-scope for the current block. Items are
closed when addressed in a later block.

---

## Block B24 — Lane B (DW-B23-BE-ALLACCOUNTS-01 fix)

**Block Summary**: New `BreakEven(Account, Instrument, int)` overload in CopyEngine.cs. All 6 call
sites updated (1 in CopyEngine.cs + 5 in TradeCopierPanel.cs). Two new [Fact] tests. Test count
126 → 128. Defect DW-B23-BE-ALLACCOUNTS-01 structurally closed.

**Final Review**: FINAL_PASS (2026-07-07)

### Deferred Items from B24

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B24-01 | **NT8-043 formal rule entry**: Confirm null-conditional event unsubscription (`?.Event -=`) causes silent runtime crash under NT8 Roslyn. Add to `docs/standards/NT8_COMPILER_RULES.md` as NT8-043 (P1). B24 and B25 Lane A code have zero null-conditional unsubscriptions; rule is WATCH-only. Needs explicit confirmation in a future block before promoting to P1 CONFIRMED. | P2 | B26 or future | OPEN |
| DW-B24-02 | **Manual E2E runtime verification (B24 scope)**: Press B on a solo account (no copy rule registered) in a live NinjaTrader session. Confirm stop moves without crashing. Unit tests cover null-leader path but cannot substitute for in-process NT8 runtime validation. Must be done before releasing B24 changes to production users. | P1 | B26 pre-release | OPEN |
| DW-B24-03 | **Skip-duplicate guard test**: The `if (acc == leader) continue` guard (CopyEngine.cs ~L1195) prevents double-firing when the leader account appears in the `AllAccounts` fan-out. A formal [Fact] test for this scenario is absent. Architecture plan §9 deferred this to Lane B or future block. Not addressed in B25 Lane A. | P2 | B26 or Lane B | OPEN |

---

## Block B25 — Lane A (DW-B25-01: ATM bracket stop fix)

**Block Summary**: Gate 4 in `MoveStopToBreakEven` extended to accept `StopLimit` (ATM bracket)
in addition to `StopMarket` (direct stop). `IsStopLeg` hardened with STP suffix arm for ATM bracket
order names (`"12s Buy STP"` pattern). Diagnostic log added for `StopLimit` path. Three new [Fact]
tests. Test count 128 → 131. Defect DW-B25-01 closed. Pre-existing NT8-013 violation at L766
(`DateTime.Now.AddDays(1)`) fixed to `DateTime.MaxValue` as mandatory SCAN-06 clearance.

**Final Review**: FINAL_PASS (2026-07-07)

### Deferred Items from B25 Lane A

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B25-LA-01 | **`DateTime.UtcNow` audit in `CreateOrder` calls**: SCAN-06 pattern `DateTime\.Now[^U]` does NOT catch `DateTime.UtcNow` usages in `CreateOrder` calls. NT8-013 bans both `DateTime.Now` and `DateTime.UtcNow` — only `DateTime.MaxValue` is correct for GTC orders. The B25 Lane A bonus fix cleared the one `DateTime.Now.AddDays(1)` instance at L766. A dedicated scan `DateTime\.UtcNow` inside `CreateOrder` call sites should be added to B26's 7-scan checklist to confirm no silent NT8-013 violations remain. | P2 | B26 | OPEN |
| DW-B25-LA-02 | **Manual E2E runtime verification (B25 scope — ATM bracket stop path)**: T_B25_01 and T_B25_02 validate no-throw behaviour via null-account harness only. They cannot exercise `acc.Change()` on a real `StopLimit` ATM bracket stop in a unit test context (requires live NT8 runtime with an active ATM strategy). Manual verification in NinjaTrader sim with an ATM bracket in place is required before releasing B25 changes to production users. | P1 | B26 pre-release | OPEN |

---

*ptt-plan-reviewer · PTT-COPIER-B25 Lane A · 2026-07-07*
