# B77-LaneA Deferred Backlog

This file is maintained by ptt-plan-reviewer (Phase 5). Each block appends its own
section. Items carry forward until resolved or explicitly closed by Director.

---

## Block: B77-LaneA (GetLeaderAtmTemplateName fallback-1 repair + tests)

**Pipeline completed**: 2026-08-19
**Status**: PIPELINE_COMPLETE
**Files in scope**: TradeCopierPanelB77Tests.cs (new, read-only on TradeCopierPanel.cs)

### New DW- items from B77-LaneA review

None. No new deferred work items identified in this lane.

### T_B77_TPL_02 / T_B77_TPL_03 integration gap

These two tests are skip skeletons (`[Fact(Skip="NT8-HOST-REQUIRED: ...")]`). They document
the intended integration test scenarios but cannot run in the MSBuild environment. These are
intentional gaps per the NT8 WPF visual-tree constraint — not new DW- items. They are
satisfied by the architecture plan's documented rationale (02-architecture-plan.md §3 and §4).

---

## Carry-forward items from prior blocks (not re-opened here)

| ID | Source | Item | Priority | Status |
|----|--------|------|----------|--------|
| DW-B76-03 | B76-LaneA | QX self-cancellation race on 8-contract accounts. PA-APEX-422136-09 PTT-QX-Stop/Stop2/Stop3 submitted at 6:48:54, cancelled at 6:48:55 before fill. Hypothesis: ATM teardown cancel-all sweep concurrent with QX stop submission. Target: B77-LaneB investigation. | P1 | OPEN, B77-LaneB |
| DW-B76-02 | B76-LaneA | GetLeaderAtmTemplateName Fallback-1 used SelectedAtmStrategy.Name (class-name trap). | P1 | CLOSED — fixed in commit ff5944ee, confirmed by B77-LaneA REPAIR-01 |
| DW-B76-01 | B76-LaneA | NT8 popup "Cancellation rejected -- Order is complete" on ATM teardown. NT8-internal; no code fix possible. | P3 | OPEN (doc only) |
| DW-B75-02 | B75-LaneA | [PTT-CLONE] diagnostic Output.Process lines. Remove after Clone live confirm. | P2 | OPEN, Director gate pending |
| DW-B75-01 | B75-LaneA | Non-ASCII em-dash/arrow in CopyEngine.cs (lines 502, 717 addressed in HOTFIX-B77-02; lines 202, 203 still open). | P2 | PARTIAL -- HOTFIX-B77-02 applied (B77 cosmetic carry-in) |
| DW-B75-03 | B75-LaneA | 14 NT8-runtime-bound tests marked [Fact(Skip="NT8-runtime")]. | P2 | OPEN |
| DW-B75-04 | B75-LaneA | HasWorkingPttCopy no retry counter guard. | P2 | OPEN |
| DW-B66-BE-01 | B66/B74 | CancelQxBrackets cancels PTT-BE-Stop during QX. Director confirmation required. | P1 | OPEN |
| DW-B66-C-02 | B66/B74 | DispatchCopy Gate 5 dedup key = 0.0 for StopLimit entries. | P1 | OPEN |
| DW-B63-01 | B63/B74 | Spurious PTT-Copy bracket orders on Sim102 after ATM fill. | P1 | OPEN |
| DW-B54-01 | B54 | ATM auto-inject blocked: AtmStrategyCreate() is StrategyBase-only, not available on AddOnBase. | P1 | OPEN (blocked) |
| DW-B72-01 | B72-LaneA | IsAtmBracketName("Stop10") returns true -- digit-at-[4] edge case. Acceptable known edge. | P3 | OPEN |
| DW-B73-B-01 | B73-LaneB | RaiseBeAllDisarmed fires on every flat regardless of per-account slot ownership -- redundant broadcasts, no correctness impact. | P2 | OPEN |
| DW-B73-B-02 | B73-LaneB | UpdateBeAllVisuals creates unfrozen SolidColorBrush instances on every call -- allocation on WPF UI thread, not a hot path. | P2 | OPEN |
| DW-B58-01 | B58 | SnapshotTargetsPublic hardcoded order-name prefixes. | P2 | OPEN |
| DW-B58-02 | B58 | GlobalBe non-atomic lazy init. | P2 | OPEN |
| DW-B58-03 | B58 | RelayBe OcoGroup not forwarded. | P2 | OPEN |
| PRE-EXISTING-03 | pre-B72 | deploy-sync.ps1 archived; PropTraderTools sync is manual. | P2 | OPEN |

---

*Append-only. New blocks add a new ## section above the carry-forward table.*
