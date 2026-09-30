# BWAVE-DW Repair LaneC -- Deferred Backlog

**Block**: BWAVE-DW Repair LaneC (this pipeline run)
**Written by**: ptt-plan-reviewer (Phase 5 Final Review)
**Date**: 2026-08-20
**Pipeline status**: FINAL_PASS
**Branch**: feature/bwave-dw-lane-c

---

## Closed This Block

Items resolved by this Repair LaneC pipeline:

| ID | Description | Closed By |
|----|-------------|-----------|
| DW-C39-05b (B4) | ApplyFeatureFlags timing: flags applied before rows rebuilt on startup with saved rules -- ApplyFeatureFlags now called inside Dispatcher.InvokeAsync lambda in RefreshRuleRows() after row rebuild | R-LC-1 VERIFY_PASS |
| DW-C39-20 (B5) | Last-panel pending BE slot leak: orphan AccountItemUpdate handlers remain after last chart close -- IsPanelsEmpty() + ClearAllPendingBeSlots() guard added to Detach() | R-LC-2 VERIFY_PASS |
| DW-LaneA-05 | ptt-sync-and-verify.ps1 output (18/18 OK, 0 MISMATCH) documented in both completion artifacts | R-LC-1 and R-LC-2 completion reports |
| DW-LaneA-06 | F5 / dotnet build confirmation documented in both completion and verification artifacts | R-LC-1 and R-LC-2 completion + verification reports |

---

## BWAVE-DW-REPAIR-LANEC: Current Block Deferred Items

Items NOT fixed in this pipeline and requiring future attention:

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-DW-01 | DW-C38-01: Detach -- unsubscribe OnPendingBeArmedDispatch before clearing _leaderAccount; event could fire on detached panel (handler leak) | P1 | B5/B6/future | OPEN |
| DW-DW-02 | DW-C38-02: Detach -- _modules.Teardown() loop: verify all IPttModule impls call Dispose; potential resource leak if any module skips disposal | P2 | future | OPEN |
| DW-DW-03 | DW-C38-04: Detach -- _allAccounts.Clear() does not unsubscribe follower OrderUpdate/PositionUpdate handlers added in B41; potential memory leak on panel close | P1 | B5/B6/future | OPEN |
| DW-DW-04 | DW-C39-17 / DW-C39-06: OnAddRule -- BuildDynamicRuleRow() initializes buttons but no _rulesPanel.InvalidateMeasure() call; may leave stale layout on some WPF hosts | P2 | future | OPEN |
| DW-DW-05 | DW-C39-19 / DW-C39-09: OnAddRule -- no SaveRules() call after row add; dynamically added rule not persisted to disk across NT8 sessions | P1 | B5/B6/future | OPEN |
| DW-C39-08 | OnAddRule -- no rule-count cap; unbounded rule row growth possible with rapid consecutive Add Rule clicks | P2 | future | OPEN |
| DW-C39-07 | ApplyFeatureFlags -- _trimBtns/_flattenBtns/_cancelBtns have no null-guard before iteration (pre-existing risk; mirrored by _armBeBtns/_tightenBtns) | P2 | future | OPEN |
| DW-RepairLC-01 | R-LC-1 SIM gate AC-6 PENDING -- Starter license + persisted rules: verify Arm BE and Tighten buttons disabled on startup. Requires live NT8 host + F5 compile. | P1 | next SIM session | OPEN |
| DW-RepairLC-02 | R-LC-2 SIM gate AC-7 PENDING -- arm BE ALL on two accounts, close last chart, verify IsPendingSlotsEmpty()==true. Requires live NT8 host + F5 compile. | P1 | next SIM session | OPEN |
| DW-LaneA-01 | T1 test DetachPanel_DoesNotDisarmSiblingPanelBeState uses structural assertion (checks method absence); consider integration-level SIM test for behavioral verification of sibling isolation | P2 | future | OPEN |
| DW-LaneA-02 | T1 test DetachPanel_DisarmsOwnLeaderAccount uses structural assertion; no behavioral verification that line 591 DisarmPendingBe(_leaderAccount) executes during Detach | P2 | future | OPEN |
| DW-LaneA-03 | T2 tests use reflection to invoke private ApplyButtonGroupFlag; brittle if method signature or accessibility changes | P2 | future | OPEN |
| DW-LaneA-04 | ApplyButtonGroupFlag null-guard on list argument absent (pre-existing; no crash observed in production) | P2 | future | OPEN |
| DW-WARN-B131 | Pre-existing xUnit2004 warning at B131Tests.cs:165 -- Assert.Equal() used for boolean condition; should be Assert.True() per xUnit best practice | P2 | future | OPEN |
| DW-B37-01..08 | B37 deferred items per BWAVE-DW mandate -- not addressed in this pipeline | P1/P2 | future | OPEN (pre-existing) |
| DW-C39-10..15 | C39 deferred items per BWAVE-DW mandate -- not addressed in this pipeline | P1/P2 | future | OPEN (pre-existing) |

---

## Notes for Future Implementers

### IsPanelsEmpty/ClearAllPendingBeSlots call site discipline (NEW -- added this block)

The `ClearAllPendingBeSlots()` method introduced in R-LC-2 is called ONLY from `TradeCopierPanel.Detach()`
when `TradeCopierAddOn.IsPanelsEmpty()` is true. Any future code path that arms additional accounts
(e.g., a new multi-account BE feature) MUST ensure those accounts are disarmed before or within
`ClearAllPendingBeSlots()`. The current implementation iterates ALL `_pendingBeSlots` entries and
unsubscribes every `AccountItemUpdate` handler -- this is correct and exhaustive for the current design.

### ApplyFeatureFlags call site discipline (carried from LaneA)

There are now 5 call sites for `ApplyFeatureFlags` in `TradeCopierWindow.cs`:
- OnLoaded (line 154)
- OnActivateClick (line 403)
- OnFeatureFlagsChanged (line 464)
- OnAddRule (line 904)
- RefreshRuleRows Dispatcher.InvokeAsync lambda (line 173) -- NEW this block

Any future code path that adds buttons to `_armBeBtns`, `_tightenBtns`, or any other gated list
MUST ensure `ApplyFeatureFlags` is called afterwards. Failure re-introduces the DW-C39-05 class of bug.

### SIM gate documentation requirement

Both DW-RepairLC-01 and DW-RepairLC-02 are PENDING because they require a live NinjaTrader 8 host
for F5 compile and runtime verification. The next engineer session that performs an F5 compile on
this branch MUST document the outcome and close these items.
