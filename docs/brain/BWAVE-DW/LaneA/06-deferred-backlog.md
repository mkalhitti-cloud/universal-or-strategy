# BWAVE-DW LaneA -- Deferred Backlog

## Block: BWAVE-DW LaneA (2026-09-03)

---

### Completed This Block

| Item | Description | Status |
|------|-------------|--------|
| DW-C38-03 | Detach disarms sibling panels' BE slots -- `DisarmAllAccounts()` call removed from `Detach()` and method definition deleted entirely. Scoped `_engine.DisarmPendingBe(_leaderAccount)` at line 591 preserved. | FIXED |
| DW-C39-05 | License gate not re-applied after OnAddRule -- `ApplyFeatureFlags` expanded to gate `_armBeBtns` and `_tightenBtns` (Part A); `OnAddRule` now calls `ApplyFeatureFlags(CopyEngine.Instance.Flags)` immediately after adding dynamic row (Part B). | FIXED |

---

### Deferred to Future Blocks

#### Deferred from mission brief (not raised as blocking in this block)

| ID | Item | Priority | Target Block |
|----|------|----------|--------------|
| DW-C38-01 | Detach -- unsubscribe `OnPendingBeArmedDispatch` before clearing `_leaderAccount`; event fired on detached panel | P1 | B5/B6/future |
| DW-C38-02 | Detach -- `_modules.Teardown()` loop: verify all `IPttModule` impls call `Dispose` to avoid resource leaks | P2 | future |
| DW-C38-04 | Detach -- `_allAccounts.Clear()` does not unsubscribe follower `OrderUpdate`/`PositionUpdate` handlers added in B41; potential memory leak | P1 | B5/B6/future |
| DW-C39-06 | OnAddRule -- `BuildDynamicRuleRow()` initializes buttons but no `_rulesPanel.InvalidateMeasure()` call; may leave stale layout on some WPF hosts | P2 | future |
| DW-C39-07 | ApplyFeatureFlags -- `_trimBtns`, `_flattenBtns`, `_cancelBtns` have no null-guard before iteration (pre-existing risk); mirrored by `_armBeBtns`/`_tightenBtns` which are also unguarded | P2 | future |
| DW-C39-08 | OnAddRule -- no rule-count cap; unbounded rule row growth possible with rapid Add Rule clicks | P2 | future |
| DW-C39-09 | OnAddRule -- no `SaveRules()` call after row add; rule not persisted to disk across NT8 sessions | P1 | B5/B6/future |
| DW-B37-01..08 | B37 deferred items per mission brief -- not re-raised in this block | P1/P2 | future |
| DW-C39-10..15 | C39 deferred items per mission brief -- not re-raised in this block | P1/P2 | future |

#### New deferred items discovered this block

| ID | Item | Priority | Target Block |
|----|------|----------|--------------|
| DW-LaneA-01 | T1 test -- `DetachPanel_DoesNotDisarmSiblingPanelBeState` uses structural assertion (method deleted); consider integration-level SIM test to behaviorally verify sibling isolation | P2 | future |
| DW-LaneA-02 | T1 test -- `DetachPanel_DisarmsOwnLeaderAccount` uses structural assertion; no behavioral verification that line 591 call executes during Detach | P2 | future |
| DW-LaneA-03 | T2 tests use reflection to invoke private `ApplyButtonGroupFlag`; brittle if method signature changes | P2 | future |
| DW-LaneA-04 | `ApplyButtonGroupFlag` has no null-guard on list argument (pre-existing; no crash observed in production) | P2 | future |
| DW-LaneA-05 | `ptt-sync-and-verify.ps1` output (18/18 OK result) was not persisted in ticket completion artifacts; engineer must document this in the next session that touches PropTraderTools | P1 | next session |
| DW-LaneA-06 | F5 NinjaTrader 8 compile confirmation was not documented in any completion or verification artifact; mandatory per AGENTS.md §2 NT8 Sync Integrity V12.B95 | P1 | next session |

---

### Notes for Future Implementers

1. **DisarmAllAccounts extraction history**: The original B40 inline `Account.All` loop in `Detach()`
   was extracted into `DisarmAllAccounts()` during BWAVE-CYC LaneB (R10) to reduce CYC.
   This block (BWAVE-DW LaneA T1) deleted that extracted method entirely because the loop was
   logically wrong for multi-panel teardown. Future engineers should NOT re-add any global
   `Account.All` iteration to `Detach()`. The line 591 scoped disarm is intentional and sufficient.

2. **ApplyFeatureFlags call site discipline**: There are now 4 call sites for `ApplyFeatureFlags`:
   OnLoaded (line 153), OnActivateClick (line 403), OnFeatureFlagsChanged (line 464), and
   OnAddRule (line 904). Any future code path that adds buttons to `_armBeBtns`, `_tightenBtns`,
   or any of the other gated lists MUST also ensure `ApplyFeatureFlags` is called afterwards
   to gate the new buttons. Failure to do so re-introduces the DW-C39-05 class of bug.

3. **Test framework**: All tests in this block use xUnit only (namespace `PropTraderTools`,
   `using Xunit;`). NUnit and MSTest are banned per AGENTS.md Test Framework Mandate (V12.32).
   The `BwaveDwLaneATests.cs` file is in `src/PropTraderTools/Tests/` which is excluded from
   the `ptt-sync-and-verify.ps1` sync (Tests/ is in `$excludeDirs`). Tests build with the
   project but are not copied to NT8.

4. **NT8 sync gate documentation gap**: Both T1 and T2 completion reports confirm
   `dotnet build` succeeds (0 errors, 0 warnings). However, neither explicitly records the
   `ptt-sync-and-verify.ps1` output or F5 compilation result. This is a process gap
   (DW-LaneA-05/06 above). The next time `src/PropTraderTools/` is modified, the engineer
   MUST document the sync result (`18/18 OK, 0 MISMATCH`) and the F5 outcome in the
   completion artifact.
