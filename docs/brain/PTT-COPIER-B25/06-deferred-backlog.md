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

---

## Block B25 — Lane B (DW-B25-02: Per-Account BE State Isolation)

**Block Summary**: Replaced singleton `volatile int _pendingBeState` and `volatile int _trailBeState`
with `readonly ConcurrentDictionary<string, int> _pendingBeStates` and `_trailBeStates` in
CopyEngine.cs. Updated `DisarmPendingBe` and `DisarmTrailBe` signatures to accept `Account leader`
parameter. Extracted `IsPendingBeArmed(Account)` and `IsTrailBeArmed(Account)` private helper
methods. Updated all 5 TradeCopierPanel.cs call sites to pass `_leaderAccount`. Updated 3 existing
CopyEngineTests.cs tests. Test count preserved at 128. All 7 scans (SCAN-01 through SCAN-07) zero
across `src/PropTraderTools/`. DW-B25-02 CLOSED this block.

**Plan Review Cycles**: Cycle 1 FAIL (V1/V2/V3 CYC violations, V4 doc gap) → Cycle 2 PASS (all 4 resolved).

**Final Review**: FINAL_PASS (2026-07-07)

### B24 Items Carried Forward (Unchanged Status)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B24-01 | **NT8-043 formal rule entry**: Confirm null-conditional event unsubscription (`?.Event -=`) causes silent runtime crash under NT8 Roslyn. NT8-043 added as P0 compiler-error rule in B23; DW-B24-01 tracks runtime crash *confirmation* specifically. B25 SCAN-07 = 0 (no new ?.Event -= usage). Rule watch continues. | P2 | B26 or future | OPEN |
| DW-B24-02 | **Manual E2E runtime verification**: Press B on a solo account in a live NinjaTrader session. Confirm stop moves. B25 unit tests preserve 128 baseline; no new runtime coverage added. Must be done before production release. | P1 | B26 pre-release | OPEN |
| DW-B24-03 | **Skip-duplicate guard test**: Formal [Fact] for `if (acc == leader) continue` guard at CopyEngine.cs:~1195. Not addressed in B25 (out-of-scope for DW-B25-02 ticket). | P2 | B26 | OPEN |

### B25 Items

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B25-02 | **Per-account BE state isolation**: Replace singleton `volatile int _pendingBeState` / `_trailBeState` with per-account `ConcurrentDictionary<string,int>`. Update Disarm signatures and all 5 Panel call sites. | — | B25 (this block) | **CLOSED** |
| DW-B25-01 | **Companion field race**: `_pendingBeAccount`, `_pendingBeInstrument`, `_trailBeAccount`, `_trailBeInstrument` remain plain refs (single-writer UI thread). In a multi-panel topology, two panels could race on the same singleton companion ref. Per-account isolation was scoped to state slots only in B25. Full companion-field isolation requires a larger refactor. | P3 | B26 or future | OPEN |

---

*ptt-plan-reviewer · PTT-COPIER-B25 · 2026-07-07*
