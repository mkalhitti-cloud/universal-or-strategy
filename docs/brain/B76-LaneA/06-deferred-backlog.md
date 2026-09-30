# PTT Deferred Work Backlog

This file is maintained by ptt-plan-reviewer (Phase 5). Each block appends its own
section. Items carry forward until resolved or explicitly closed by Director.

---

## Block B76-LaneA — FlattenOneAccount Race+Guard + PositionState Dedup + ATM Class-Name Fix

**Block completed**: 2026-08-18 (PENDING Ph4a/Ph4b/Ph5)
**Pipeline verdict**: PENDING
**Files in scope**: CopyEngine.cs, TradeCopierPanel.cs, TradeCopierAddOn.cs, TradeCopierWindow.cs

### New Items from B76-LaneA

| ID | Item | Priority | Status |
|----|------|----------|--------|
| DW-B76-01 | NT8 popup "Cancellation rejected -- Order is complete" on ATM teardown. NT8-internal behavior; no code fix possible without hooking NT8 internals. Document as confirmed NT8 behavior. | P3 | OPEN (doc only) |
| DW-B76-02 | GetLeaderAtmTemplateName Fallback-1 reads wrong property. `AtmStrategySelector.SelectedAtmStrategy.Name` returns "AtmStrategy" (class name) -- same bug as `ct.AtmStrategy.Name`. The template name string lives on `sel.SelectedItem as string`, not on `.SelectedAtmStrategy.Name`. Fix: replace Fallback-1 body with `return sel?.SelectedItem as string ?? string.Empty`. Observed live 2026-08-19: SetCloneAtmCache still logs 'AtmStrategy' after B76 guard correctly rejects ct.AtmStrategy.Name -- all fallbacks fail. Clone mode cannot read ATM template name in any path until this is fixed. | P1 | OPEN |
| DW-B76-03 | QX self-cancellation race on 8-contract accounts. Observed live 2026-08-19: PA-APEX-422136-09 PTT-QX-Stop/Stop2/Stop3 submitted+accepted at 6:48:54, then Cancel submitted at 6:48:55 with Filled=0 -- before price reached stop level. Hypothesis: ATM teardown cancel-all sweep on -09 ran concurrently with PTT-QX order submission and swept the new QX stops as collateral. -07 (same 8 contracts, different ATM teardown timing) stopped out correctly. Needs sim reproduction test at 8 contracts across all accounts + investigation of cancel ordering in CancelQxBrackets / QX dispatch sequence. | P1 | OPEN |

### Carried Items from Prior Blocks (OPEN)

| ID | Source | Item | Priority | Status |
|----|--------|------|----------|--------|
| DW-B75-01 | B75-LaneA | Non-ASCII em-dash/arrow in CopyEngine.cs (lines 202, 203, 493, 697 approx). | P2 | OPEN |
| DW-B75-02 | B75-LaneA | [PTT-CLONE] diagnostic Output.Process lines. Remove after Clone live confirm. | P2 | OPEN |
| DW-B75-03 | B75-LaneA | 14 NT8-runtime-bound tests marked [Fact(Skip="NT8-runtime")]. | P2 | OPEN |
| DW-B75-04 | B75-LaneA | HasWorkingPttCopy no retry counter guard. | P2 | OPEN |
| DW-B66-BE-01 | B66/B74 | CancelQxBrackets cancels PTT-BE-Stop during QX. Director confirmation required. | P1 | OPEN |
| DW-B66-C-02 | B66/B74 | DispatchCopy Gate 5 dedup key = 0.0 for StopLimit. | P1 | OPEN |
| DW-B63-01 | B63/B74 | Spurious PTT-Copy bracket orders on Sim102. | P1 | OPEN |
| DW-B54-01 | B54 | ATM auto-inject blocked: AtmStrategyCreate() is StrategyBase-only. | P1 | OPEN (blocked) |
| DW-B72-01 | B72-LaneA | IsAtmBracketName("Stop10") returns true -- known edge. | P3 | OPEN |
| DW-B73-B-01 | B73-LaneB | RaiseBeAllDisarmed redundant broadcasts on flat. | P2 | OPEN |
| DW-B73-B-02 | B73-LaneB | UpdateBeAllVisuals unfrozen SolidColorBrush instances. | P2 | OPEN |
| DW-B58-01 | B58 | SnapshotTargetsPublic hardcoded order-name prefixes. | P2 | OPEN |
| DW-B58-02 | B58 | GlobalBe non-atomic lazy init. | P2 | OPEN |
| DW-B58-03 | B58 | RelayBe OcoGroup not forwarded. | P2 | OPEN |
| PRE-EXISTING-03 | pre-B72 | deploy-sync.ps1 archived; sync is manual. | P2 | OPEN |

---

*Append-only. New blocks add a new ## section above this footer.*
