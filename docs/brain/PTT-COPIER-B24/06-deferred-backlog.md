# PTT-COPIER Deferred Work Backlog

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
| DW-B24-01 | **NT8-043 formal rule entry**: Confirm null-conditional event unsubscription (`?.Event -=`) causes silent runtime crash under NT8 Roslyn. Add to `docs/standards/NT8_COMPILER_RULES.md` as NT8-043 (P1). B24 code has zero null-conditional unsubscriptions (SCAN-07 = 0); rule is WATCH-only per plan Section 9. Needs explicit confirmation in a future block before promoting to P1 CONFIRMED. | P2 | B25 or future | OPEN |
| DW-B24-02 | **Manual E2E runtime verification**: Press B on a solo account (no copy rule registered) in a live NinjaTrader session. Confirm stop moves. Unit tests cover null-leader path and no-throw paths but cannot substitute for in-process NT8 runtime validation. Must be done before releasing B24 changes to production users. | P1 | B25 pre-release | OPEN |
| DW-B24-03 | **Skip-duplicate guard test**: The `if (acc == leader) continue` guard (CopyEngine.cs:1195) prevents double-firing when the leader account appears in the `AllAccounts` fan-out. A formal [Fact] test for this scenario is absent. Add a test that wires a rule where master == leader account and verifies `MoveStopToBreakEven` is called exactly once for that account. | P2 | B25 | OPEN |

---

*ptt-plan-reviewer · PTT-COPIER-B24 · 2026-07-07*
