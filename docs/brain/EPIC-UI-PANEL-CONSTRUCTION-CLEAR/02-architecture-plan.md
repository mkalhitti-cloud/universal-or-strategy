# EPIC-UI-PANEL-CONSTRUCTION-CLEAR -- Phase 2 Architecture Plan
# ClearPanelWidgetRefs Extraction

**File**: `src/V12_002.UI.Panel.Construction.cs`
**Class**: `V12_002` (partial)
**Target method**: `ClearPanelWidgetRefs()` (lines 441--552)
**Violation type**: LOC (103 non-blank/non-brace-only lines), NOT cyclomatic complexity
**Current CYC**: 1 (pure sequential null-assignment chain -- no branches)
**Goal**: Reduce method body to 4 delegation calls; CYC stays 1 throughout.

---

## Jane Street OKF Alignment

| Rule | Application |
|------|-------------|
| CYC <= 8 per method | Each helper is pure sequential assignment; CYC = 1. Original stays CYC = 1. |
| ONE method per epic -- helpers stay private | All 4 helpers are `private`, no public API added. |
| Extract helpers: single concern, CYC <= 8 each | Each helper owns one logical section of the panel. |
| Behavior-preserving extraction | Zero semantic change -- null-assignments and scalar resets only. |
| Every extracted helper gets >= 1 xUnit [Fact] | Covered in Phase 4 ticket requirements (see below). |

---

## Extraction Plan

### Overview

`ClearPanelWidgetRefs` is split into 4 private helpers plus a thin coordinator.
Every helper is zero-parameter, `void`, `private`.
No instance fields are promoted, captured, or reordered.
The only change to the original method body is replacement of the 4 field-assignment
blocks with 4 calls in the same top-to-bottom order.

```
ClearPanelWidgetRefs()          CYC=1   (4 call sites)
  ClearIdentitySectionRefs()    CYC=1   Block-A (lines 443-452)
  ClearExecButtonRefs()         CYC=1   Block-B (lines 454-466)
  ClearTargetAndLiveManagementRefs()  CYC=1   Block-C (lines 468-499)
  ClearTelemetryAndScorecardRefs()    CYC=1   Block-D (lines 501-551)
```

---

### Extraction 1 -- Block-A

**(1) Helper name**: `ClearIdentitySectionRefs`

**(2) Exact signature**:
```csharp
private void ClearIdentitySectionRefs()
```
- Return type: `void`
- Parameters: none (all fields are instance members of `V12_002`)

**(3) Lines to move into the helper** (source lines 443--452):
```csharp
hubStatusLed = null;
leaderAccountCombo = null;
fleetSelectButton = null;
fleetPopup = null;
fleetCheckboxPanel = null;
selectedFleetAccounts = new List<string>();
directionCombo = null;
priceInput = null;
submitButton = null;
manualEntryRow = null;
```
Note: `selectedFleetAccounts = new List<string>()` is the single non-null assignment
in the entire method. It is a structural reset (clears the collection reference),
not a behavioral branch. CYC contribution: 0.

**(4) Call site replacement** in `ClearPanelWidgetRefs` (replaces lines 443--452):
```csharp
ClearIdentitySectionRefs();
```

---

### Extraction 2 -- Block-B

**(1) Helper name**: `ClearExecButtonRefs`

**(2) Exact signature**:
```csharp
private void ClearExecButtonRefs()
```
- Return type: `void`
- Parameters: none

**(3) Lines to move into the helper** (source lines 454--466):
```csharp
orLongButton = null;
orShortButton = null;
retestButton = null;
rmaButton = null;
momoButton = null;
ffmaButton = null;
ffmaManualButton = null;
mButton = null;
trendButton = null;
trendRmaToggle = null;
retestRmaToggle = null;
execRetestRow = null;
execTrendRow = null;
```

**(4) Call site replacement** in `ClearPanelWidgetRefs` (replaces lines 454--466):
```csharp
ClearExecButtonRefs();
```

---

### Extraction 3 -- Block-C

**(1) Helper name**: `ClearTargetAndLiveManagementRefs`

**(2) Exact signature**:
```csharp
private void ClearTargetAndLiveManagementRefs()
```
- Return type: `void`
- Parameters: none

**(3) Lines to move into the helper** (source lines 468--499):
```csharp
t1Button = null;
t2Button = null;
t3Button = null;
t4Button = null;
t5Button = null;
// Build 1107: Live target row cleanup
liveT1Row = null;
liveT2Row = null;
liveT3Row = null;
liveT4Row = null;
liveT5Row = null;
liveT1Price = null;
liveT2Price = null;
liveT3Price = null;
liveT4Price = null;
liveT5Price = null;
liveT1Cts = null;
liveT2Cts = null;
liveT3Cts = null;
liveT4Cts = null;
liveT5Cts = null;
liveStopRow = null;
liveStopPrice = null;
_currentLiveEntryName = null;
trim50Button = null;
beButton = null;
trailButton = null;
trailDistInput = null;
beOffsetInput = null;
flattenButton = null;
cancelButton = null;
lastPriceText = null;
```
Note: The inline comment `// Build 1107: Live target row cleanup` moves with the block.

**(4) Call site replacement** in `ClearPanelWidgetRefs` (replaces lines 468--499):
```csharp
ClearTargetAndLiveManagementRefs();
```

---

### Extraction 4 -- Block-D

**(1) Helper name**: `ClearTelemetryAndScorecardRefs`

**(2) Exact signature**:
```csharp
private void ClearTelemetryAndScorecardRefs()
```
- Return type: `void`
- Parameters: none

**(3) Lines to move into the helper** (source lines 501--551):
```csharp
complianceSummaryText = null;
complianceConsistencyText = null;
compliancePayoutText = null;
complianceDrawdownText = null;

or5Text = null;
or15Text = null;
ema9Text = null;
ema15Text = null;
ema30Text = null;
ema65Text = null;
ema200Text = null;
atrText = null;
mktSyncButton = null;
trendIndicator = null;
trendText = null;

modeOrbButton = null;
modeRmaButton = null;
modeRetestButton = null;
modeMomoButton = null;
modeFfmaButton = null;
modeTrendButton = null;
cnt1 = null;
cnt2 = null;
cnt3 = null;
cnt4 = null;
cnt5 = null;
svT1Val = null;
svT2Val = null;
svT3Val = null;
svT4Val = null;
svT5Val = null;
svT1Type = null;
svT2Type = null;
svT3Type = null;
svT4Type = null;
svT5Type = null;
svStrType = null;
strVal = null;
maxVal = null;
citVal = null;
t2Row = null;
t3Row = null;
t4Row = null;
t5Row = null;
syncAllButton = null;

_panelLastSyncedMode = null;
_panelLastSyncedTargetCount = 0;
_panelAppliedConfigRevision = 0;
```

**(4) Call site replacement** in `ClearPanelWidgetRefs` (replaces lines 501--551):
```csharp
ClearTelemetryAndScorecardRefs();
```

---

## Resulting ClearPanelWidgetRefs (post-extraction)

```csharp
private void ClearPanelWidgetRefs()
{
    ClearIdentitySectionRefs();
    ClearExecButtonRefs();
    ClearTargetAndLiveManagementRefs();
    ClearTelemetryAndScorecardRefs();
}
```

CYC = 1. LOC = 4 statements. No behavioral change.

---

## Helper Placement

All 4 helpers are placed in `src/V12_002.UI.Panel.Construction.cs`, inside the
`V12_002` partial class, immediately after the closing brace of `ClearPanelWidgetRefs`.
No new files. No partial class split. Private scope only.

---

## Test Requirements (Phase 4 tickets)

Per OKF rule: every extracted helper gets at minimum 1 xUnit [Fact] happy-path test.

| Helper | Test name | Assertion |
|--------|-----------|-----------|
| `ClearIdentitySectionRefs` | `ClearIdentitySectionRefs_WhenCalled_NullsAllIdentityRefs` | Verify `hubStatusLed`, `leaderAccountCombo`, `fleetSelectButton`, `fleetPopup`, `fleetCheckboxPanel` are null and `selectedFleetAccounts` is empty. |
| `ClearExecButtonRefs` | `ClearExecButtonRefs_WhenCalled_NullsAllExecButtons` | Verify `orLongButton`, `orShortButton`, and at least one toggle ref are null. |
| `ClearTargetAndLiveManagementRefs` | `ClearTargetAndLiveManagementRefs_WhenCalled_NullsAllLiveTargetRefs` | Verify `t1Button`..`t5Button`, all `liveT*` refs, management buttons all null. |
| `ClearTelemetryAndScorecardRefs` | `ClearTelemetryAndScorecardRefs_WhenCalled_NullsAllTelemetryAndResetsScalars` | Verify compliance texts, EMA/OR texts, scorecard vals null; `_panelLastSyncedTargetCount == 0`, `_panelAppliedConfigRevision == 0`. |

Test project: `tests/V12_Performance.Tests/`
Framework: xUnit `[Fact]` + `Assert.Null` / `Assert.Equal`

---

## Agent Tracking

| Field | Value |
|-------|-------|
| Phase | 2 -- Architecture Planning |
| Epic ID | EPIC-UI-PANEL-CONSTRUCTION-CLEAR |
| Target file | `src/V12_002.UI.Panel.Construction.cs` |
| Target method | `ClearPanelWidgetRefs()` lines 441--552 |
| Helpers produced | 4 |
| CYC pre/post (coordinator) | 1 / 1 |
| CYC per helper | 1 / 1 / 1 / 1 |
| New public API | none |
| Behavior change | none |
| OKF docs applied | `complexity-reduction.md`, `how-to-build-an-exchange.md` |
| Status | READY FOR PHASE 4 (ticket generation) |
