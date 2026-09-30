# PTT-COPIER Deferred Backlog
# Last updated: B55 LaneA block close (2026-08-09)
# Maintained by: ptt-plan-reviewer (Phase 5 gate)

---

## B55-LaneA Block Entry (DW-B43-02 P1 — ATM Template Read Fix)

### Closed This Block

| ID | Description | Closed By |
|----|-------------|-----------|
| DW-B43-02 P1 | GetLeaderAtmTemplateName reads SelectedValue (null) instead of SelectedItem | Production fix confirmed in TradeCopierPanel.cs line 2088 (`return atmCb.SelectedItem as string ?? string.Empty;`); test T_B55A_01 in B55Tests.cs documents the SelectedItem read path and locks the fix |

### Carried Forward (Deferred)

| ID | Description | Priority | Blocked By | Target |
|----|-------------|----------|------------|--------|
| DW-B54-01 | AtmStrategyCreate AddOn API path — Director research required before implementation | P1 | Director investigation | B56+ |
| DW-B54-02 | F5-GATE-02 live ATM bracket test — requires live ATM bracket to fire; blocked pending DW-B54-01 resolution | P1 | DW-B54-01 | B56+ (after DW-B54-01) |
| PRE-EXISTING-01 | 24 test failures in CopyEngineTests.cs (T_B54_02_LoadRules, T_B54_03, T_B33_AllAccounts_BeLoop, T_B37, ArmTrailBe, T_B25_03_IsStopLeg, and others) — none introduced by B55; predate this block; Director investigation required to triage root causes | P1 | Director investigation | Director-assigned block |
| PRE-EXISTING-02 | return null instances in PttBreakEven.cs, PttFlatten.cs, TradeCopierWindow.cs — JS-002 violations not introduced by B55; separate cleanup block required | P2 | — | Future block |
| PRE-EXISTING-03 | throw new in B42Tests.cs line 63 (InvalidOperationException) and TradeCopierWindow.cs line 684 (NotImplementedException) — JS-001 violations not introduced by B55; separate cleanup block required | P2 | — | Future block |

### Notes

- DW-B54-01 and DW-B54-02 were first identified during the B54 LaneA execution and are carried forward unchanged; no B54 06-deferred-backlog.md file was written (B54 did not produce one), so this B55 entry serves as the first formal deferred-backlog record for both items.
- PRE-EXISTING-01 through PRE-EXISTING-03 are reported per No Scope Creep Protocol. They were visible during B55 execution but not introduced by B55. The engineer and verifier both flagged them. Director should assign a dedicated cleanup block.
- Test count baseline: 279 total (255 pass, 24 fail) after B55 LaneA. The 24 failures are all in PRE-EXISTING-01.

---

## Running Open Items (all blocks)

| ID | Description | Priority | Status |
|----|-------------|----------|--------|
| DW-B54-01 | AtmStrategyCreate AddOn API path | P1 | OPEN |
| DW-B54-02 | F5-GATE-02 live ATM bracket test | P1 | OPEN — blocked by DW-B54-01 |
| PRE-EXISTING-01 | 24 CopyEngineTests.cs failures | P1 | OPEN |
| PRE-EXISTING-02 | return null in PttBreakEven/PttFlatten/TradeCopierWindow | P2 | OPEN |
| PRE-EXISTING-03 | throw new in B42Tests/TradeCopierWindow | P2 | OPEN |
