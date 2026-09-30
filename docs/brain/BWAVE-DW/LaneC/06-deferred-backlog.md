# BWAVE-DW LaneC Deferred Backlog

**Epic**: BWAVE-DW LaneC (Test Quality + StyleCop + ASCII Comments)
**Branch**: `feature/bwave-dw-lane-c`
**Date**: 2026-09-04
**Author**: ptt-plan-reviewer (Phase 5)

This file records:
1. All 18 DW items closed by Lane C tickets (with CLOSED status).
2. Items from prior BWAVE-DW lanes that remain OPEN and were NOT addressed by Lane C.
3. New items identified during Lane C verification (if any).

---

## DW Items Closed by This Lane

All 18 deferred work items targeted by the Lane C plan are confirmed CLOSED by independent
Layer 3 verification.

| DW Item | Description | Ticket | File | Status |
|---------|-------------|--------|------|--------|
| DW-LaneA-01 | SA1507 consecutive blank lines -- CopyEngineTests.cs ~6843 | C-1 | CopyEngineTests.cs | CLOSED |
| DW-LaneA-02 | SA1507 consecutive blank lines -- CopyEngineTests.cs ~6920 | C-1 | CopyEngineTests.cs | CLOSED |
| DW-LaneA-03 | SA1508 closing brace preceded by blank line -- CopyEngineTests.cs ~6921 | C-1 | CopyEngineTests.cs | CLOSED |
| DW-LaneA-04 | U+2500 box-drawing chars in comment separators | C-2 | CopyEngineTests.cs, B46Tests.cs, B47Tests.cs | CLOSED |
| DW-LaneA-05 | SA1507 consecutive blank lines -- BwaveCycLaneCTests.cs ~566 | C-1 | BwaveCycLaneCTests.cs | CLOSED |
| DW-B37-01 | TryRecordBeTargetFill Order-based path not exercised -- line 142 | C-4 | BwaveCycLaneBTests.cs | CLOSED |
| DW-B37-02 | Inverted test name: IsBeRetryEligible_ReturnsFalse contradicts Assert.True -- line 433 | C-3 | BwaveCycLaneBTests.cs | CLOSED |
| DW-B37-03 | TryFireFollowerBeRetry retry execution branch not invoked -- line 446 | C-4 | BwaveCycLaneBTests.cs | CLOSED |
| DW-B37-04 | Inverted test name: IsNativeExitName_ReturnsTrue contradicts Assert.False -- line 546 | C-3 | BwaveCycLaneBTests.cs | CLOSED |
| DW-B37-05 | CopyRule.Create never called; normalization path unverified -- line 697 | C-4 | BwaveCycLaneBTests.cs | CLOSED |
| DW-B37-06 | Inverted test name: ReturnsAllOnes contradicts Assert.Null -- line 707 | C-3 | BwaveCycLaneBTests.cs | CLOSED |
| DW-B37-07 | Inverted test name: ReturnsBid contradicts Assert.Equal(101.0=ask) -- line 723 | C-3 | BwaveCycLaneBTests.cs | CLOSED |
| DW-B37-08 | Inverted test name: ReturnsAsk contradicts Assert.Equal(100.0=bid) -- line 752 | C-3 | BwaveCycLaneBTests.cs | CLOSED |
| DW-C39-11 | MetadataToken cross-assembly instability in T_B76_08 | C-5 | B76Tests.cs | CLOSED |
| DW-C39-12 | Raw IL opcode-scanning loops in T_B76_02/03/04/05/06/11 | C-5 | B76Tests.cs | CLOSED |
| DW-C39-13 | Wrong opcode Ldstr (0x72) vs Ldsfld (0x7E) for string.Empty in T_B77_TPL_05 | C-6 | TradeCopierPanelB77Tests.cs | CLOSED |
| DW-C39-14 | Wrong scan target (GetLeaderAtmTemplateName vs TryGetAtmNameFromSelector) in T_B77_TPL_04 | C-6 | TradeCopierPanelB77Tests.cs | CLOSED |
| DW-C39-15 | Singleton mutation teardown missing in T_B66OBJ_P02 | C-7 | TradeCopierPanelB75Tests.cs | CLOSED |

**Closure evidence**: All items confirmed CLOSED via independent Layer 3 verification in
`ticket-C1-verification.md` through `ticket-C7-verification.md`.

---

## New Items Identified During Lane C

No new deferred items identified during Lane C verification.

The following observations were noted but do NOT become new DW items:

- **UTF-8 BOM on `BwaveCycLaneBTests.cs`**: Pre-existing 3-byte BOM (EF BB BF) at offset 0.
  Not introduced by Lane C. The `utf8_repair.py` hook (project rule 05-utf8-encoding.md) handles
  this automatically. No engineer action required.

- **`BwaveDwLaneBTests.cs` untracked**: Untracked file visible in git status during C-4
  verification. Belongs to Repair-LaneB epic. Not introduced by any Lane C ticket. Out of scope.

- **T_B76_08 at CYC=8**: The modified T_B76_08 method reaches the CYC limit exactly (CYC=8).
  This is compliant per JS rules. Not a finding. Noted for awareness only.

---

## Items From Prior Lanes Still OPEN

The following items were deferred by BWAVE-DW LaneA (`docs/brain/BWAVE-DW/LaneA/06-deferred-backlog.md`)
and were NOT in scope of Lane C (a test-only lane that touched no production files).

### Production Code Items (BWAVE-DW LaneA origin)

| ID | Priority | File | Description | Status | Reason Not Addressed by Lane C |
|----|----------|------|-------------|--------|--------------------------------|
| DW-LaneA-06 | P1 | TradeCopierPanel.cs:1233 | `BuildArrowCluster` unconditional `Background = mainBackground` overwrites teal-button background | OPEN | Production file -- Lane C is test-only |
| DW-C38-01 | P1 | TradeCopierPanel.cs | Detach: unsubscribe `OnPendingBeArmedDispatch` before clearing `_leaderAccount` | OPEN | Production file -- Lane C is test-only |
| DW-C38-02 | P2 | TradeCopierPanel.cs | Detach: `_modules.Teardown()` loop -- verify all `IPttModule` impls call `Dispose` | OPEN | Production file -- Lane C is test-only |
| DW-C38-04 | P1 | TradeCopierPanel.cs | Detach: `_allAccounts.Clear()` does not unsubscribe follower `OrderUpdate`/`PositionUpdate` handlers | OPEN | Production file -- Lane C is test-only |
| DW-C39-06 | P2 | TradeCopierWindow.cs | OnAddRule: no `_rulesPanel.InvalidateMeasure()` call after `BuildDynamicRuleRow()` | OPEN | Production file -- Lane C is test-only |
| DW-C39-07 | P2 | TradeCopierWindow.cs | ApplyFeatureFlags: `_trimBtns`/`_flattenBtns`/`_cancelBtns` no null-guard before iteration | OPEN | Production file -- Lane C is test-only |
| DW-C39-08 | P2 | TradeCopierWindow.cs | OnAddRule: no rule-count cap; unbounded rule row growth | OPEN | Production file -- Lane C is test-only |
| DW-C39-09 | P1 | TradeCopierWindow.cs | OnAddRule: no `SaveRules()` call after row add; rule not persisted across NT8 sessions | OPEN | Production file -- Lane C is test-only |

### Test Harness Structural Items (BWAVE-DW LaneA origin -- test quality debt)

These are from the BWAVE-DW LaneA `06-deferred-backlog.md` "New deferred items" section.
They are distinct from the BWAVE-CYC DW-LaneA-01..05 items (which are now CLOSED above).

| ID | Priority | File | Description | Status | Reason Not Addressed by Lane C |
|----|----------|------|-------------|--------|--------------------------------|
| DW-DWLA-01 | P2 | BwaveDwLaneATests.cs | `DetachPanel_DoesNotDisarmSiblingPanelBeState` uses structural assertion (method deleted); consider integration-level SIM test | OPEN | Different test file; out of scope |
| DW-DWLA-02 | P2 | BwaveDwLaneATests.cs | `DetachPanel_DisarmsOwnLeaderAccount` uses structural assertion; no behavioral verification of line 591 call | OPEN | Different test file; out of scope |
| DW-DWLA-03 | P2 | BwaveDwLaneATests.cs | T2 tests use reflection to invoke `ApplyButtonGroupFlag`; brittle if signature changes | OPEN | Different test file; out of scope |
| DW-DWLA-04 | P2 | TradeCopierWindow.cs | `ApplyButtonGroupFlag` null-guard on list arg absent (pre-existing) | OPEN | Production file -- Lane C is test-only |
| DW-DWLA-05 | P1 | (process) | ptt-sync-and-verify.ps1 output not persisted in BWAVE-DW LaneA completion artifacts | OPEN | Process gap from LaneA; not applicable to Lane C (no sync required) |
| DW-DWLA-06 | P1 | (process) | F5 NinjaTrader compile confirmation not documented in BWAVE-DW LaneA artifacts | OPEN | Process gap from LaneA; not applicable to Lane C (no F5 required) |

**Note**: These items use prefix `DW-DWLA-` to distinguish them from the BWAVE-CYC
`DW-LaneA-01..05` items (which have been CLOSED by this lane).

### BWAVE-CYC LaneB-PR37-repair Deferred Items (already shown as closed above)

For completeness: the BWAVE-CYC `06-deferred-backlog.md` (LaneB-PR37-repair) listed
`DW-LaneA-01..05` and `DW-B37-01..08` as OPEN. All 13 of those items are now CLOSED
by Lane C tickets C-1, C-2, C-3, C-4 as documented in the DW Item Closure Table above.

---

## Summary Table

| ID | Priority | File | Description | Closed By | Status |
|----|----------|------|-------------|-----------|--------|
| DW-LaneA-01 | P2 | CopyEngineTests.cs | SA1507 ~6843 | C-1 | CLOSED |
| DW-LaneA-02 | P2 | CopyEngineTests.cs | SA1507 ~6920 | C-1 | CLOSED |
| DW-LaneA-03 | P2 | CopyEngineTests.cs | SA1508 ~6921 | C-1 | CLOSED |
| DW-LaneA-04 | P1 | CopyEngineTests.cs, B46Tests.cs, B47Tests.cs | U+2500 box-drawing chars | C-2 | CLOSED |
| DW-LaneA-05 | P2 | BwaveCycLaneCTests.cs | SA1507 ~566 | C-1 | CLOSED |
| DW-B37-01 | P2 | BwaveCycLaneBTests.cs | Order-based path not exercised | C-4 | CLOSED |
| DW-B37-02 | P3 | BwaveCycLaneBTests.cs | Inverted test name ~433 | C-3 | CLOSED |
| DW-B37-03 | P2 | BwaveCycLaneBTests.cs | Retry execution branch not invoked | C-4 | CLOSED |
| DW-B37-04 | P3 | BwaveCycLaneBTests.cs | Inverted test name ~546 | C-3 | CLOSED |
| DW-B37-05 | P2 | BwaveCycLaneBTests.cs | CopyRule.Create never called | C-4 | CLOSED |
| DW-B37-06 | P3 | BwaveCycLaneBTests.cs | Inverted test name ~707 | C-3 | CLOSED |
| DW-B37-07 | P3 | BwaveCycLaneBTests.cs | Inverted test name ~723 | C-3 | CLOSED |
| DW-B37-08 | P3 | BwaveCycLaneBTests.cs | Inverted test name ~752 | C-3 | CLOSED |
| DW-C39-11 | P2 | B76Tests.cs | MetadataToken cross-assembly | C-5 | CLOSED |
| DW-C39-12 | P2 | B76Tests.cs | Raw IL opcode-scanning loops | C-5 | CLOSED |
| DW-C39-13 | P2 | TradeCopierPanelB77Tests.cs | Wrong opcode ldstr vs ldsfld | C-6 | CLOSED |
| DW-C39-14 | P2 | TradeCopierPanelB77Tests.cs | Wrong scan target method | C-6 | CLOSED |
| DW-C39-15 | P2 | TradeCopierPanelB75Tests.cs | Singleton mutation teardown missing | C-7 | CLOSED |
| DW-LaneA-06 | P1 | TradeCopierPanel.cs:1233 | BuildArrowCluster unconditional bg overwrite | N/A | OPEN |
| DW-C38-01 | P1 | TradeCopierPanel.cs | Detach: unsubscribe OnPendingBeArmedDispatch | N/A | OPEN |
| DW-C38-02 | P2 | TradeCopierPanel.cs | Detach: Teardown/Dispose verification | N/A | OPEN |
| DW-C38-04 | P1 | TradeCopierPanel.cs | Detach: allAccounts handler leak | N/A | OPEN |
| DW-C39-06 | P2 | TradeCopierWindow.cs | OnAddRule: no InvalidateMeasure | N/A | OPEN |
| DW-C39-07 | P2 | TradeCopierWindow.cs | ApplyFeatureFlags: null-guard absent | N/A | OPEN |
| DW-C39-08 | P2 | TradeCopierWindow.cs | OnAddRule: no rule-count cap | N/A | OPEN |
| DW-C39-09 | P1 | TradeCopierWindow.cs | OnAddRule: no SaveRules call | N/A | OPEN |
| DW-DWLA-01 | P2 | BwaveDwLaneATests.cs | Structural assertion (method deleted) | N/A | OPEN |
| DW-DWLA-02 | P2 | BwaveDwLaneATests.cs | Structural assertion (no behavioral verify) | N/A | OPEN |
| DW-DWLA-03 | P2 | BwaveDwLaneATests.cs | Reflection-based invocation fragility | N/A | OPEN |
| DW-DWLA-04 | P2 | TradeCopierWindow.cs | ApplyButtonGroupFlag null-guard absent | N/A | OPEN |
| DW-DWLA-05 | P1 | (process) | ptt-sync output not persisted (LaneA gap) | N/A | OPEN |
| DW-DWLA-06 | P1 | (process) | F5 compile confirmation not documented (LaneA gap) | N/A | OPEN |

**Closed by Lane C**: 18
**Still OPEN**: 14
**No new items**: 0

---

*ptt-plan-reviewer | BWAVE-DW LaneC | Phase 5 Deferred Backlog | 2026-09-04*
