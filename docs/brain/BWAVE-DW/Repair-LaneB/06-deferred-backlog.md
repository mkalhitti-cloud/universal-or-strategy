# BWAVE-DW-REPAIR-LANEB Deferred Backlog

**Block**: BWAVE-DW-REPAIR-LANEB (this pipeline run)
**Written by**: ptt-plan-reviewer (Phase 5 Final Review)
**Date**: 2026-09-03
**Pipeline status**: FINAL_PASS

---

## CLOSED THIS BLOCK

Items resolved within this pipeline run:

| ID | Description | Closed By |
|----|-------------|-----------|
| DW-C38-03 (repair) | Failing DisarmAllAccounts tests replaced with deletion-confirming Assert.Null test in BwaveCycR10HelperTests | R-LB-1 VERIFY_PASS |
| B3-csproj | BwaveDwLaneATests.cs and BwaveDwLaneBTests.cs missing Compile entries in PropTraderTools.csproj | R-LB-2 VERIFY_PASS |

---

## DEFERRED ITEMS

### Carried from BWAVE-DW LaneB (2026-08-26)

| ID | Description | Reason Deferred | Priority | Target Block |
|----|-------------|-----------------|----------|--------------|
| DW-C38-01 | TryAdd null-slot guard in CopyEngine or shared utility. Original mission brief explicitly excluded from LaneB scope. Requires its own dedicated ticket with correct scope, target method, and behavioral spec. | Intentionally excluded per BWAVE-DW LaneB mission brief. Dedicated ticket needed. | P1 | Future block (dedicated DW LaneX) |
| DW-WARN-B131 | Pre-existing xUnit2004 warning at `B131Tests.cs:165` -- `Assert.Equal()` for boolean condition; should be `Assert.True()` per xUnit best practice. May already be resolved by parallel lane (B-4 verifier saw 0 warnings). | Pre-existing technical debt, not introduced by this pipeline. Non-blocking. | P2 | Next available cleanup block |

### Carried from BWAVE-DW LaneA (2026-09-03)

| ID | Description | Priority | Target Block |
|----|-------------|----------|--------------|
| DW-C38-01 (variant) | Detach -- unsubscribe `OnPendingBeArmedDispatch` before clearing `_leaderAccount`; event fired on detached panel | P1 | B5/B6/future |
| DW-C38-02 | Detach -- `_modules.Teardown()` loop: verify all `IPttModule` impls call `Dispose` to avoid resource leaks | P2 | future |
| DW-C38-04 | Detach -- `_allAccounts.Clear()` does not unsubscribe follower `OrderUpdate`/`PositionUpdate` handlers; potential memory leak | P1 | B5/B6/future |
| DW-C39-06 | OnAddRule -- `BuildDynamicRuleRow()` initializes buttons but no `_rulesPanel.InvalidateMeasure()` call | P2 | future |
| DW-C39-07 | ApplyFeatureFlags -- `_trimBtns`/`_flattenBtns`/`_cancelBtns` null-check absent (pre-existing) | P2 | future |
| DW-C39-08 | OnAddRule -- no rule-count cap; unbounded rule row growth possible with rapid Add Rule clicks | P2 | future |
| DW-C39-09 | OnAddRule -- no `SaveRules()` call after row add; rule not persisted across NT8 sessions | P1 | B5/B6/future |
| DW-LaneA-01 | T1 test -- `DetachPanel_DoesNotDisarmSiblingPanelBeState` uses structural assertion; consider integration-level SIM test | P2 | future |
| DW-LaneA-02 | T1 test -- `DetachPanel_DisarmsOwnLeaderAccount` uses structural assertion; no behavioral verification of line 591 call | P2 | future |
| DW-LaneA-03 | T2 tests use reflection to invoke private `ApplyButtonGroupFlag`; brittle if method signature changes | P2 | future |
| DW-LaneA-04 | `ApplyButtonGroupFlag` has no null-guard on list argument (pre-existing; no crash observed) | P2 | future |
| DW-LaneA-05 | ptt-sync-and-verify.ps1 output not persisted in LaneA completion artifacts | P1 | Next engineer session touching PropTraderTools |
| DW-LaneA-06 | F5 NinjaTrader 8 compile confirmation not documented in any LaneA artifact | P1 | Next engineer session touching PropTraderTools |
| DW-B37-01..08 | B37 deferred items per mission brief | P1/P2 | future |
| DW-C39-10..15 | C39 deferred items per mission brief | P1/P2 | future |

### From original BWAVE-DW mandate (not addressed in this repair)

| ID | Description | Priority | Target Block |
|----|-------------|----------|--------------|
| DW-DW-01 | From original BWAVE-DW mandate deferred list -- not in scope of this repair pipeline | P1/P2 | future |
| DW-DW-02 | From original BWAVE-DW mandate deferred list -- not in scope of this repair pipeline | P1/P2 | future |
| DW-DW-03 | From original BWAVE-DW mandate deferred list -- not in scope of this repair pipeline | P1/P2 | future |
| DW-DW-04 | From original BWAVE-DW mandate deferred list -- not in scope of this repair pipeline | P1/P2 | future |
| DW-DW-05 | From original BWAVE-DW mandate deferred list -- not in scope of this repair pipeline | P1/P2 | future |
| DW-C39-17 | From original BWAVE-DW mandate deferred list -- not in scope of this repair pipeline | P1/P2 | future |
| DW-C39-19 | From original BWAVE-DW mandate deferred list -- not in scope of this repair pipeline | P1/P2 | future |
| B76Tests-naming | B76Tests.cs file naming convention (carried from original mandate) -- not in scope of this repair pipeline | P2 | future |

---

## NOTES

### What this repair fixed

This pipeline run was a targeted repair addressing two items that blocked BWAVE-DW wave completion:

1. **B2 (R-LB-1)**: Two [Fact] test methods in `BwaveCycR10HelperTests` asserted `Assert.NotNull`
   on the `DisarmAllAccounts` method that was deleted in BWAVE-DW LaneA. After deletion they failed
   with NullReferenceException. R-LB-1 replaced both with a single `DisarmAllAccounts_IsDeleted`
   test that correctly asserts `Assert.Null` to confirm method absence. DW-C38-03 is now CLOSED.

2. **B3 (R-LB-2)**: Two test files (`BwaveDwLaneATests.cs`, `BwaveDwLaneBTests.cs`) existed on
   disk but had no `<Compile Include>` entries in `PropTraderTools.csproj`. Without these entries
   the project could not compile the test files. R-LB-2 inserted both entries. B3 is now CLOSED.

### No production code was modified

Neither ticket touched any production `.cs` file. No NinjaTrader 8 API surface was affected.
NT8 sync (`ptt-sync-and-verify.ps1`) was correctly not run for either ticket.

### Test framework compliance

All test code in this repair uses xUnit only (`[Fact]`, `Assert.Null`). NUnit and MSTest remain
absent from the project per Test Framework Mandate (V12.32).
