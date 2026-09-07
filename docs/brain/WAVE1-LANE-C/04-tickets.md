# WAVE1-LANE-C Implementation Tickets
**Phase 3 -- PTT Pipeline**
**Output**: `docs/brain/WAVE1-LANE-C/04-tickets.md`
**Architect**: PTT Architect (Phase 3)
**Source plan**: `docs/brain/WAVE1-LANE-C/02-architecture-plan.md` (REVIEW_PASS)
**Source review**: `docs/brain/WAVE1-LANE-C/02-plan-review.md` (REVIEW_PASS)
**Date**: 2026-09-07

---

## SCOPE REMINDER (enforced in every ticket)

- **Files in scope**: `src/PropTraderTools/TradeCopierPanel.cs`, `src/PropTraderTools/TradeCopierWindow.cs`, `src/PropTraderTools/TradeCopierAddOn.cs`
- **Files NEVER touched**: `CopyEngine.cs`, any `Ptt*.cs` file
- **All helpers**: private instance methods, same class, same file -- NO static helpers, NO new classes, NO new files
- **Thread rule**: all helpers execute synchronously on the calling UI thread -- NO `Dispatcher.InvokeAsync`, NO `Task.Run`
- **CYC mandate**: lizard `--csv` must show CCN <= 8 for every parent method AND every extracted helper

---

## Ticket C-01: BuildBufferedButtonsRow

**File:** `src/PropTraderTools/TradeCopierPanel.cs`
**Class:** `FollowerItem` (nested inside `TradeCopierPanel`)
**Method:** `BuildBufferedButtonsRow`
**Current CCN:** 87
**Spec req IDs:** JS-021 (no lock()), JS-001 (no throw in helpers), JS-002 (no return null), JS-033 (no async void)

**Exact lines to extract:** 1131-1226

**Extracted helper signatures (ALL private instance methods):**
- `private void BuildSingleButtonCluster(string content, bool isTeal, RoutedEventHandler upHandler, RoutedEventHandler downHandler, RoutedEventHandler mainHandler, Action<Button> storeAction, Panel targetPanel)`  // CCN est: 3, dispatcher-safe: Y
- `private void BuildQuickT3HiddenRow(StackPanel root)`  // CCN est: 1, dispatcher-safe: Y

**Extraction logic:**
- `BuildSingleButtonCluster`: extract the foreach body that creates a DockPanel cluster (two arrow Buttons + one main Button), applies teal styling conditionally if `isTeal`, calls `storeAction(mainBtn)` to assign to field, adds cluster to `targetPanel`.
- `BuildQuickT3HiddenRow`: extract the collapsed `_quickT3Row` StackPanel construction with "T3 hidden" label; assigns `_quickT3Row` field directly; adds to `root`.
- Parent after extraction: initialise `row1` + `_beRowPanel` + `_quickRowPanel`, build specs tuple array, call `BuildSingleButtonCluster` in a foreach (CYC 1), call `BuildQuickT3HiddenRow(root)`, call `root.Children.Add(row1)`. Estimated parent CCN: 2.

**NT8 thread safety:** All helpers are private instance methods on `FollowerItem` (same class as `BuildBufferedButtonsRow`). Called synchronously on UI dispatcher thread. No Dispatcher.InvokeAsync introduced.

**7-scan checklist:**
- [ ] Scan 1 - JS-021 lock(): `grep -n "lock(" src/PropTraderTools/TradeCopierPanel.cs` -- zero new hits
- [ ] Scan 2 - JS-001 throw: `grep -n "throw new" src/PropTraderTools/TradeCopierPanel.cs` -- zero new hits in helpers
- [ ] Scan 3 - JS-002 return null: `grep -n "return null" src/PropTraderTools/TradeCopierPanel.cs` -- verify no new nulls in helpers
- [ ] Scan 4 - JS-033 async void: `grep -n "async void" src/PropTraderTools/TradeCopierPanel.cs` -- zero hits
- [ ] Scan 5 - CYC: `lizard src/PropTraderTools/TradeCopierPanel.cs --csv` -- `BuildBufferedButtonsRow` <= 8, `BuildSingleButtonCluster` <= 8, `BuildQuickT3HiddenRow` <= 8
- [ ] Scan 6 - JS-001 exceptions: no new `throw` statement in `BuildSingleButtonCluster` or `BuildQuickT3HiddenRow`
- [ ] Scan 7 - Build: `dotnet build src/PropTraderTools/PropTraderTools.csproj` -- zero errors

**Test requirement:**
- `[Fact] T_C01_01_BuildSingleButtonCluster_TealTrue_SetsBorderBrushToTeal` -- verify that when `isTeal=true` the main Button's BorderBrush equals NTBrushes.Teal equivalent
- `[Fact] T_C01_02_BuildSingleButtonCluster_TealFalse_DoesNotSetTealBorderBrush` -- verify that when `isTeal=false` no teal brush is applied to BorderBrush
- `[Fact] T_C01_03_BuildQuickT3HiddenRow_AddsCollapsedRowToRoot` -- verify the appended StackPanel Visibility is Collapsed
- `[Fact] T_C01_04_BuildSingleButtonCluster_StoreAction_AssignsButtonReference` -- verify the storeAction delegate receives a non-null Button and the captured field reference is set

**Acceptance criterion:** `lizard --csv` CCN <= 8 for `BuildBufferedButtonsRow` and all extracted helpers (`BuildSingleButtonCluster`, `BuildQuickT3HiddenRow`).

---

## Ticket C-02: BuildInlineFollowerRow

**File:** `src/PropTraderTools/TradeCopierPanel.cs`
**Class:** `FollowerItem` (nested inside `TradeCopierPanel`)
**Method:** `BuildInlineFollowerRow`
**Current CCN:** 64
**Spec req IDs:** JS-021 (no lock()), JS-001 (no throw in helpers), JS-002 (no return null), JS-033 (no async void)

**Exact lines to extract:** 2042-2132

**Extracted helper signatures (ALL private instance methods):**
- `private CheckBox BuildFollowerCheckBox(FollowerItem item)`  // CCN est: 1, dispatcher-safe: Y
- `private TextBlock BuildFollowerNameLabel(FollowerItem item)`  // CCN est: 1, dispatcher-safe: Y
- `private TextBlock BuildFollowerPnlLabel(FollowerItem item)`  // CCN est: 1, dispatcher-safe: Y
- `private ComboBox BuildFollowerAtmComboBox(FollowerItem item)`  // CCN est: 1, dispatcher-safe: Y
- `private void WireFollowerCheckBoxHandlers(FollowerItem item, CheckBox chk, ComboBox atmCombo)`  // CCN est: 4, dispatcher-safe: Y

**Extraction logic:**
- `BuildFollowerCheckBox`: create CheckBox bound to `item.IsSelected`, return it.
- `BuildFollowerNameLabel`: create 90px-wide TextBlock with account display name, return it.
- `BuildFollowerPnlLabel`: create 64px-wide TextBlock with DailyPnlText + DailyPnlColor foreground binding, return it.
- `BuildFollowerAtmComboBox`: create 120px ComboBox, wire `LoadedEvent` + `SelectionChanged`, set `DataContext = item`, return it.
- `WireFollowerCheckBoxHandlers`: wire `chk.Checked` lambda (sets `item.IsSelected=true`, enables `atmCombo`, calls `SortFollowerRows`, `UpdateCopierHeader`, `TryAutoApply`) and `chk.Unchecked` lambda (sets `item.IsSelected=false`, disables `atmCombo`, same calls). CYC = 4 (2 lambdas x 2 branches).
- Parent after extraction: create Grid row, call each builder, call `WireFollowerCheckBoxHandlers`, assemble. Estimated parent CCN: 1.

**NT8 thread safety:** All 5 helpers are private instance methods on the same class. Called synchronously on UI thread. No Dispatcher.InvokeAsync introduced.

**7-scan checklist:**
- [ ] Scan 1 - JS-021 lock(): `grep -n "lock(" src/PropTraderTools/TradeCopierPanel.cs` -- zero new hits
- [ ] Scan 2 - JS-001 throw: `grep -n "throw new" src/PropTraderTools/TradeCopierPanel.cs` -- zero new hits in helpers
- [ ] Scan 3 - JS-002 return null: `grep -n "return null" src/PropTraderTools/TradeCopierPanel.cs` -- zero new nulls in helpers
- [ ] Scan 4 - JS-033 async void: `grep -n "async void" src/PropTraderTools/TradeCopierPanel.cs` -- zero hits
- [ ] Scan 5 - CYC: `lizard src/PropTraderTools/TradeCopierPanel.cs --csv` -- `BuildInlineFollowerRow` <= 8, all 5 helpers <= 8
- [ ] Scan 6 - JS-001 exceptions: no new `throw` statement in any of the 5 helpers
- [ ] Scan 7 - Build: `dotnet build src/PropTraderTools/PropTraderTools.csproj` -- zero errors

**Test requirement:**
- `[Fact] T_C02_01_BuildFollowerCheckBox_IsChecked_ReflectsItemIsSelected` -- verify IsChecked equals item.IsSelected initial value
- `[Fact] T_C02_02_BuildFollowerPnlLabel_Foreground_EqualsDailyPnlColor` -- verify TextBlock Foreground binding source is DailyPnlColor property
- `[Fact] T_C02_03_BuildFollowerAtmComboBox_IsEnabled_ReflectsItemIsSelected` -- verify Width == 120
- `[Fact] T_C02_04_WireFollowerCheckBoxHandlers_Checked_SetsIsSelectedTrue` -- fire Checked event, verify item.IsSelected becomes true
- `[Fact] T_C02_05_WireFollowerCheckBoxHandlers_Unchecked_SetsIsSelectedFalse` -- fire Unchecked event, verify item.IsSelected becomes false

**Acceptance criterion:** `lizard --csv` CCN <= 8 for `BuildInlineFollowerRow` and all 5 extracted helpers.

---

## Ticket C-03: BuildCheckItemTemplate

**File:** `src/PropTraderTools/TradeCopierPanel.cs`
**Class:** `FollowerItem` (nested inside `TradeCopierPanel`)
**Method:** `BuildCheckItemTemplate`
**Current CCN:** 61
**Spec req IDs:** JS-021 (no lock()), JS-001 (no throw in helpers), JS-002 (no return null), JS-033 (no async void)

**Exact lines to extract:** 2297-2382

**Extracted helper signatures (ALL private instance methods):**
- `private FrameworkElementFactory BuildTemplateAccountNameColumn()`  // CCN est: 1, dispatcher-safe: Y
- `private FrameworkElementFactory BuildTemplatePnlColumn()`  // CCN est: 1, dispatcher-safe: Y
- `private FrameworkElementFactory BuildTemplateMultiplierColumn()`  // CCN est: 1, dispatcher-safe: Y
- `private FrameworkElementFactory BuildTemplateAtmComboColumn()`  // CCN est: 1, dispatcher-safe: Y
- `private FrameworkElementFactory BuildTemplateCheckBoxColumn()`  // CCN est: 1, dispatcher-safe: Y

**Extraction logic:**
- `BuildTemplateAccountNameColumn`: FrameworkElementFactory for col 0 TextBlock with Account.Name binding and CharacterEllipsis trimming.
- `BuildTemplatePnlColumn`: FrameworkElementFactory for col 1 TextBlock with DailyPnlText + DailyPnlColor bindings, right-aligned, 64px width.
- `BuildTemplateMultiplierColumn`: FrameworkElementFactory for col 2 TextBox (w=30, Text="1", Visibility=Collapsed), wires `OnFollowerMultiplierChanged`.
- `BuildTemplateAtmComboColumn`: FrameworkElementFactory for col 3 ComboBox (w=120), wires `LoadedEvent` + `SelectionChangedEvent`.
- `BuildTemplateCheckBoxColumn`: FrameworkElementFactory for col 4 CheckBox with TwoWay `IsSelected` binding, wires `OnFollowerChecked` click handler.
- Parent after extraction: create `DataTemplate` + `FrameworkElementFactory` for Grid, define 5 columns, call each `Build*Column()` and `stackFef.AppendChild(...)`, return template. Estimated parent CCN: 1.

**NT8 thread safety:** All 5 helpers are private instance methods on the same class. No Dispatcher.InvokeAsync. Called synchronously on UI thread during template construction.

**7-scan checklist:**
- [ ] Scan 1 - JS-021 lock(): `grep -n "lock(" src/PropTraderTools/TradeCopierPanel.cs` -- zero new hits
- [ ] Scan 2 - JS-001 throw: `grep -n "throw new" src/PropTraderTools/TradeCopierPanel.cs` -- zero new hits in helpers
- [ ] Scan 3 - JS-002 return null: `grep -n "return null" src/PropTraderTools/TradeCopierPanel.cs` -- zero new nulls; each helper returns a constructed FrameworkElementFactory
- [ ] Scan 4 - JS-033 async void: `grep -n "async void" src/PropTraderTools/TradeCopierPanel.cs` -- zero hits
- [ ] Scan 5 - CYC: `lizard src/PropTraderTools/TradeCopierPanel.cs --csv` -- `BuildCheckItemTemplate` <= 8, all 5 helpers <= 8
- [ ] Scan 6 - JS-001 exceptions: no new `throw` in any helper
- [ ] Scan 7 - Build: `dotnet build src/PropTraderTools/PropTraderTools.csproj` -- zero errors

**Test requirement:**
- `[Fact] T_C03_01_BuildTemplateAccountNameColumn_ColumnIndex_IsZero` -- verify returned factory Type == typeof(TextBlock)
- `[Fact] T_C03_02_BuildTemplatePnlColumn_TextAlignment_IsRight` -- verify TextAlignment property value is Right
- `[Fact] T_C03_03_BuildTemplateMultiplierColumn_Visibility_IsCollapsed` -- verify Visibility property value is Collapsed
- `[Fact] T_C03_04_BuildTemplateAtmComboColumn_Width_Is120` -- verify Width property value is 120
- `[Fact] T_C03_05_BuildTemplateCheckBoxColumn_ColumnIndex_IsFour` -- verify returned factory Type == typeof(CheckBox)

**Acceptance criterion:** `lizard --csv` CCN <= 8 for `BuildCheckItemTemplate` and all 5 extracted helpers.

---

## Ticket C-04: BuildActionButtons

**File:** `src/PropTraderTools/TradeCopierWindow.cs`
**Class:** `TradeCopierWindow`
**Method:** `BuildActionButtons`
**Current CCN:** 58
**Spec req IDs:** JS-021 (no lock()), JS-001 (no throw in helpers), JS-002 (no return null), JS-033 (no async void)

**Exact lines to extract:** 753-817

**Extracted helper signatures (ALL private instance methods):**
- `private void BuildTrimActionButton(object tag, Grid grid)`  // CCN est: 1, dispatcher-safe: Y
- `private void BuildFlattenActionButton(object tag, Grid grid)`  // CCN est: 1, dispatcher-safe: Y
- `private void BuildCancelActionButton(object tag, Grid grid)`  // CCN est: 1, dispatcher-safe: Y
- `private void BuildToggleActionButton(object tag, Grid grid)`  // CCN est: 1, dispatcher-safe: Y
- `private void BuildApplyActionButton(object tag, ComboBox leaderCb, ListBox followerLb, ComboBox atmCb, TextBox namedBox, Grid grid)`  // CCN est: 1, dispatcher-safe: Y

**Extraction logic:**
- `BuildTrimActionButton`: create [1/2] Trim Button, set Grid.Column=3, add to `_trimBtns`, wire `OnRuleTrim` click, add to `grid`.
- `BuildFlattenActionButton`: create [=] Flatten Button, set Grid.Column=4, add to `_flattenBtns`, wire `OnRuleFlatten` click, add to `grid`.
- `BuildCancelActionButton`: create [x] Cancel Button with `WBrushInactive` background, set Grid.Column=5, add to `_cancelBtns`, wire `OnRuleCancel` click, add to `grid`.
- `BuildToggleActionButton`: create [ON] Toggle Button, set Grid.Column=6, wire `OnRuleToggle` click, add to `grid`.
- `BuildApplyActionButton`: create Apply Button, build `tag` array `[tag, leaderCb, followerLb, atmCb, namedBox]`, set Grid.Column=7, wire `OnRowApply` click, add to `grid`.
- Parent after extraction: receive `tag`, `grid`, and UI control references; call each builder helper. Estimated parent CCN: 1.

**NT8 thread safety:** All 5 helpers are private instance methods on `TradeCopierWindow`. Called synchronously on UI thread. No Dispatcher.InvokeAsync.

**7-scan checklist:**
- [ ] Scan 1 - JS-021 lock(): `grep -n "lock(" src/PropTraderTools/TradeCopierWindow.cs` -- zero new hits
- [ ] Scan 2 - JS-001 throw: `grep -n "throw new" src/PropTraderTools/TradeCopierWindow.cs` -- zero new hits in helpers
- [ ] Scan 3 - JS-002 return null: `grep -n "return null" src/PropTraderTools/TradeCopierWindow.cs` -- zero new nulls
- [ ] Scan 4 - JS-033 async void: `grep -n "async void" src/PropTraderTools/TradeCopierWindow.cs` -- zero hits
- [ ] Scan 5 - CYC: `lizard src/PropTraderTools/TradeCopierWindow.cs --csv` -- `BuildActionButtons` <= 8, all 5 helpers <= 8
- [ ] Scan 6 - JS-001 exceptions: no new `throw` in any helper
- [ ] Scan 7 - Build: `dotnet build src/PropTraderTools/PropTraderTools.csproj` -- zero errors

**Test requirement:**
- `[Fact] T_C04_01_BuildTrimActionButton_GridColumn_IsThree` -- verify Grid.GetColumn on created button == 3
- `[Fact] T_C04_02_BuildFlattenActionButton_AddedTo_FlattenBtnsList` -- verify `_flattenBtns` count increases by 1
- `[Fact] T_C04_03_BuildCancelActionButton_Background_IsWBrushInactive` -- verify `_cancelBtns` count increases by 1
- `[Fact] T_C04_04_BuildToggleActionButton_GridColumn_IsSix` -- verify Grid.GetColumn == 6
- `[Fact] T_C04_05_BuildApplyActionButton_TagArray_ContainsFiveElements` -- verify Button.Tag is array of length 5

**Acceptance criterion:** `lizard --csv` CCN <= 8 for `BuildActionButtons` and all 5 extracted helpers.

---

## Ticket C-05: BuildModeRow

**File:** `src/PropTraderTools/TradeCopierPanel.cs`
**Class:** `FollowerItem` (nested inside `TradeCopierPanel`)
**Method:** `BuildModeRow`
**Current CCN:** 52
**Spec req IDs:** JS-021 (no lock()), JS-001 (no throw in helpers), JS-002 (no return null), JS-033 (no async void)

**Exact lines to extract:** 1668-1726

**Extracted helper signatures (ALL private instance methods):**
- `private RadioButton BuildSignalRadioButton()`  // CCN est: 1, dispatcher-safe: Y
- `private RadioButton BuildMirrorRadioButton()`  // CCN est: 1, dispatcher-safe: Y
- `private RadioButton BuildCloneRadioButton()`  // CCN est: 1, dispatcher-safe: Y
- `private Button BuildCopyToggleButton()`  // CCN est: 1, dispatcher-safe: Y

**Extraction logic:**
- `BuildSignalRadioButton`: create Signal RadioButton (`IsChecked=true`, appropriate margin/content), wire `OnSignalModeClick`, return.
- `BuildMirrorRadioButton`: create Mirror RadioButton, wire `OnMirrorModeClick`, return.
- `BuildCloneRadioButton`: create Clone RadioButton (B50 style), wire `OnCloneModeClick`, return.
- `BuildCopyToggleButton`: create COPY OFF Button (inactive border styling), wire `OnCopyToggle`, return.
- Parent after extraction: create row StackPanel, call each builder and assign to fields (`_signalRadio`, `_mirrorRadio`, `_cloneRadio`, `_copyToggleBtn`), add to row. Estimated parent CCN: 1.

**NT8 thread safety:** All 4 helpers are private instance methods on the same class. Called synchronously on UI thread. No Dispatcher.InvokeAsync.

**7-scan checklist:**
- [ ] Scan 1 - JS-021 lock(): `grep -n "lock(" src/PropTraderTools/TradeCopierPanel.cs` -- zero new hits
- [ ] Scan 2 - JS-001 throw: `grep -n "throw new" src/PropTraderTools/TradeCopierPanel.cs` -- zero new hits in helpers
- [ ] Scan 3 - JS-002 return null: `grep -n "return null" src/PropTraderTools/TradeCopierPanel.cs` -- zero new nulls; each helper returns a constructed control
- [ ] Scan 4 - JS-033 async void: `grep -n "async void" src/PropTraderTools/TradeCopierPanel.cs` -- zero hits
- [ ] Scan 5 - CYC: `lizard src/PropTraderTools/TradeCopierPanel.cs --csv` -- `BuildModeRow` <= 8, all 4 helpers <= 8
- [ ] Scan 6 - JS-001 exceptions: no new `throw` in any helper
- [ ] Scan 7 - Build: `dotnet build src/PropTraderTools/PropTraderTools.csproj` -- zero errors

**Test requirement:**
- `[Fact] T_C05_01_BuildSignalRadioButton_IsChecked_True` -- verify IsChecked == true on returned RadioButton
- `[Fact] T_C05_02_BuildMirrorRadioButton_IsChecked_False` -- verify IsChecked == false on returned RadioButton
- `[Fact] T_C05_03_BuildCloneRadioButton_Content_ContainsClone` -- verify Content string contains "Clone" (case-insensitive)
- `[Fact] T_C05_04_BuildCopyToggleButton_Content_ContainsCopyOff` -- verify Content string contains "OFF" (case-insensitive)

**Acceptance criterion:** `lizard --csv` CCN <= 8 for `BuildModeRow` and all 4 extracted helpers.

---

## Ticket C-06: BuildClickTraderRow

**File:** `src/PropTraderTools/TradeCopierPanel.cs`
**Class:** `FollowerItem` (nested inside `TradeCopierPanel`)
**Method:** `BuildClickTraderRow`
**Current CCN:** 52
**Spec req IDs:** JS-021 (no lock()), JS-001 (no throw in helpers), JS-002 (no return null), JS-033 (no async void)

**Exact lines to extract:** 986-1049

**Extracted helper signatures (ALL private instance methods):**
- `private ToggleButton BuildBuyToggleButton()`  // CCN est: 1, dispatcher-safe: Y
- `private ToggleButton BuildSellToggleButton()`  // CCN est: 1, dispatcher-safe: Y
- `private Button BuildArmButton()`  // CCN est: 1, dispatcher-safe: Y
- `private Button BuildClickTraderCancelButton()`  // CCN est: 1, dispatcher-safe: Y

**Extraction logic:**
- `BuildBuyToggleButton`: create Buy ToggleButton (`IsChecked=true`, W=45, H=22), wire `OnBuyToggleClick`, return.
- `BuildSellToggleButton`: create Sell ToggleButton (W=45, H=22), wire `OnSellToggleClick`, return.
- `BuildArmButton`: create Arm Button (W=48, H=22, dark background), wire `OnArmClick`, return.
- `BuildClickTraderCancelButton`: create Cancel Button with `BrushDanger` border styling, wire `OnCancel2`, return.
- Parent after extraction: create row StackPanel/DockPanel, call each builder, assign to fields, add to row. Estimated parent CCN: 1.

**NT8 thread safety:** All 4 helpers are private instance methods on the same class. Called synchronously on UI thread. No Dispatcher.InvokeAsync.

**7-scan checklist:**
- [ ] Scan 1 - JS-021 lock(): `grep -n "lock(" src/PropTraderTools/TradeCopierPanel.cs` -- zero new hits
- [ ] Scan 2 - JS-001 throw: `grep -n "throw new" src/PropTraderTools/TradeCopierPanel.cs` -- zero new hits in helpers
- [ ] Scan 3 - JS-002 return null: `grep -n "return null" src/PropTraderTools/TradeCopierPanel.cs` -- zero new nulls; each helper returns a constructed control
- [ ] Scan 4 - JS-033 async void: `grep -n "async void" src/PropTraderTools/TradeCopierPanel.cs` -- zero hits
- [ ] Scan 5 - CYC: `lizard src/PropTraderTools/TradeCopierPanel.cs --csv` -- `BuildClickTraderRow` <= 8, all 4 helpers <= 8
- [ ] Scan 6 - JS-001 exceptions: no new `throw` in any helper
- [ ] Scan 7 - Build: `dotnet build src/PropTraderTools/PropTraderTools.csproj` -- zero errors

**Test requirement:**
- `[Fact] T_C06_01_BuildBuyToggleButton_IsChecked_True` -- verify IsChecked == true
- `[Fact] T_C06_02_BuildSellToggleButton_Width_Is45` -- verify Width == 45
- `[Fact] T_C06_03_BuildArmButton_Width_Is48` -- verify Width == 48
- `[Fact] T_C06_04_BuildClickTraderCancelButton_BorderBrushIsSet` -- verify BorderBrush is non-null (BrushDanger applied)

**Acceptance criterion:** `lizard --csv` CCN <= 8 for `BuildClickTraderRow` and all 4 extracted helpers.

---

## Ticket C-07: DoInject

**File:** `src/PropTraderTools/TradeCopierAddOn.cs`
**Class:** `TradeCopierAddOn`
**Method:** `DoInject`
**Current CCN:** 43
**Spec req IDs:** JS-021 (no lock()), JS-001 (no throw in helpers), JS-002 (no return null), JS-033 (no async void)

**Exact lines to extract:** 477-540

**Extracted helper signatures (ALL private instance methods):**
- `private void PurgeStalePanel(System.Windows.Controls.Grid grid)`  // CCN est: 4, dispatcher-safe: Y
- `private void WireNewPanel(TradeCopierPanel panel, Chart chart, ChartTrader chartTrader, System.Windows.Controls.Grid grid)`  // CCN est: 5, dispatcher-safe: Y

**Extraction logic:**
- `PurgeStalePanel`: iterate `grid.Children` collecting elements whose `GetType().Name == "TradeCopierPanel"` into a local list; foreach stale child: remove from `grid.Children`, find its row index, remove the corresponding `RowDefinition` guard-gated by `index > 0` and `index < grid.RowDefinitions.Count`. CYC: 4 (1 foreach + 2 guards + 1 list iteration).
- `WireNewPanel`: wire instrument (null-guarded try/catch -- pre-existing, not new), start ATR engine, set chart, wire leader account via `WireLeaderAccount`, add `SIM101` diagnostic handler, remove `SIM101` handler, hook keyboard shortcut, add new `RowDefinition` to grid and set panel grid row. CYC: 5 (null guard + try/catch + 3 sequential guard checks).
- Parent `DoInject` after extraction: null-check `grid` parameter, call `PurgeStalePanel(grid)`, create new `TradeCopierPanel`, call `WireNewPanel(panel, chart, chartTrader, grid)`. Estimated parent CCN: 7 (retains outer null guards for `chart`, `chartTrader`, `grid`).

**IMPORTANT**: `WireNewPanel` wraps a pre-existing `try/catch` block for instrument wiring. This is NOT a new `throw`. The try/catch is preserved verbatim inside the helper -- do not remove it.

**NT8 thread safety:** Both helpers are private instance methods on `TradeCopierAddOn`. `DoInject` is called via `Dispatcher.InvokeAsync` in `TryInject` (confirmed in plan). Helpers execute synchronously within that dispatch. No additional `Dispatcher.InvokeAsync` calls added.

**7-scan checklist:**
- [ ] Scan 1 - JS-021 lock(): `grep -n "lock(" src/PropTraderTools/TradeCopierAddOn.cs` -- zero hits (pre-scan already clean)
- [ ] Scan 2 - JS-001 throw: `grep -n "throw new" src/PropTraderTools/TradeCopierAddOn.cs` -- zero new hits in `PurgeStalePanel` or `WireNewPanel`
- [ ] Scan 3 - JS-002 return null: `grep -n "return null" src/PropTraderTools/TradeCopierAddOn.cs` -- zero new nulls
- [ ] Scan 4 - JS-033 async void: `grep -n "async void" src/PropTraderTools/TradeCopierAddOn.cs` -- zero hits
- [ ] Scan 5 - CYC: `lizard src/PropTraderTools/TradeCopierAddOn.cs --csv` -- `DoInject` <= 8, `PurgeStalePanel` <= 8, `WireNewPanel` <= 8
- [ ] Scan 6 - JS-001 exceptions: no new `throw new` statement introduced in either helper (pre-existing try/catch preserved as-is)
- [ ] Scan 7 - Build: `dotnet build src/PropTraderTools/PropTraderTools.csproj` -- zero errors

**Test requirement:**
- `[Fact] T_C07_01_PurgeStalePanel_RemovesChildByTypeName_TradeCopierPanel` -- add a mock UIElement whose GetType().Name == "TradeCopierPanel" to a Grid, call PurgeStalePanel, verify it is removed
- `[Fact] T_C07_02_PurgeStalePanel_RemovesRowDefinitionAtStaleRowIndex` -- verify corresponding RowDefinition is removed (index > 0 guard)
- `[Fact] T_C07_03_PurgeStalePanel_DoesNotRemoveRow0` -- if stale panel is at row 0, RowDefinition NOT removed
- `[Fact] T_C07_04_WireNewPanel_NullInstrument_DoesNotThrow` -- when instrument is null, WireNewPanel completes without exception (try/catch path)
- `[Fact] T_C07_05_DoInject_DuplicateChart_ReturnsFalseOnTryAdd` -- when grid parameter is null, DoInject returns false without crash

**Acceptance criterion:** `lizard --csv` CCN <= 8 for `DoInject`, `PurgeStalePanel`, and `WireNewPanel`.

---

## Ticket C-08: OnBeClick

**File:** `src/PropTraderTools/TradeCopierPanel.cs`
**Class:** `FollowerItem` (nested inside `TradeCopierPanel`)
**Method:** `OnBeClick`
**Current CCN:** 43
**Spec req IDs:** JS-021 (no lock()), JS-001 (no throw in helpers), JS-002 (no return null), JS-033 (no async void)

**Exact lines to extract:** 1425-1474

**Extracted helper signatures (ALL private instance methods):**
- `private void ExecuteBeIdle(Account leader, NinjaTrader.Cbi.Instrument instrument)`  // CCN est: 3, dispatcher-safe: Y
- `private void ExecuteBeArmed(Account leader)`  // CCN est: 2, dispatcher-safe: Y

**Extraction logic:**
- `ExecuteBeIdle`: handles `BeState.Idle` arm: logs intent via `NinjaTrader.Code.Output.Process(...)`, tests `IsPriceAlreadyAtBe(...)` (1 conditional branch); if already at BE calls `DispatchModule("BE")` and stays Idle; otherwise calls `ArmPendingBe(...)`, sets `_beState = BeState.Armed`, calls `UpdateBeVisuals(BeState.Armed)`. CYC: 3 (1 null guard + 1 price check + 1 conditional dispatch).
- `ExecuteBeArmed`: handles `BeState.Armed` disarm: logs disarm via `NinjaTrader.Code.Output.Process(...)`, calls `DisarmPendingBe()`, sets `_beState = BeState.Idle`, calls `UpdateBeVisuals(BeState.Idle)`. CYC: 2 (1 null guard + sequential).
- Parent `OnBeClick` after extraction: resolves `leader` + `instrument` (null checks), resolves `_beState`, calls `ExecuteBeIdle` or `ExecuteBeArmed` based on state. Estimated parent CCN: 4 (outer null checks + switch/if on _beState).

**Note on string concatenation**: The existing `NinjaTrader.Code.Output.Process(...)` calls use `+` string concatenation. This is preserved verbatim in the helpers -- do NOT convert to interpolation or reduce the concatenation.

**NT8 thread safety:** Both helpers are private instance methods on the same class (`FollowerItem`). `OnBeClick` fires on WPF UI thread (RoutedEventHandler). Helpers execute synchronously. No Dispatcher.InvokeAsync added.

**7-scan checklist:**
- [ ] Scan 1 - JS-021 lock(): `grep -n "lock(" src/PropTraderTools/TradeCopierPanel.cs` -- zero new hits
- [ ] Scan 2 - JS-001 throw: `grep -n "throw new" src/PropTraderTools/TradeCopierPanel.cs` -- zero new hits in helpers
- [ ] Scan 3 - JS-002 return null: `grep -n "return null" src/PropTraderTools/TradeCopierPanel.cs` -- zero new nulls
- [ ] Scan 4 - JS-033 async void: `grep -n "async void" src/PropTraderTools/TradeCopierPanel.cs` -- zero hits
- [ ] Scan 5 - CYC: `lizard src/PropTraderTools/TradeCopierPanel.cs --csv` -- `OnBeClick` <= 8, `ExecuteBeIdle` <= 8, `ExecuteBeArmed` <= 8
- [ ] Scan 6 - JS-001 exceptions: no new `throw` statement in `ExecuteBeIdle` or `ExecuteBeArmed`
- [ ] Scan 7 - Build: `dotnet build src/PropTraderTools/PropTraderTools.csproj` -- zero errors

**Test requirement:**
- `[Fact] T_C08_01_ExecuteBeIdle_PriceAtBe_CallsDispatchModuleBe` -- when `IsPriceAlreadyAtBe` returns true, verify `DispatchModule("BE")` is invoked
- `[Fact] T_C08_02_ExecuteBeIdle_PriceNotAtBe_SetsBeStateArmed` -- when `IsPriceAlreadyAtBe` returns false, verify `_beState == BeState.Armed`
- `[Fact] T_C08_03_ExecuteBeIdle_PriceNotAtBe_CallsArmPendingBe` -- verify `ArmPendingBe` is called when price is not at BE
- `[Fact] T_C08_04_ExecuteBeArmed_SetsBeStateIdle` -- verify `_beState == BeState.Idle` after call
- `[Fact] T_C08_05_ExecuteBeArmed_CallsDisarmPendingBe` -- verify `DisarmPendingBe()` is called

**Acceptance criterion:** `lizard --csv` CCN <= 8 for `OnBeClick`, `ExecuteBeIdle`, and `ExecuteBeArmed`.

---

## Ticket C-09: BuildUI

**File:** `src/PropTraderTools/TradeCopierWindow.cs`
**Class:** `TradeCopierWindow`
**Method:** `BuildUI`
**Current CCN:** 47
**Spec req IDs:** JS-021 (no lock()), JS-001 (no throw in helpers), JS-002 (no return null), JS-033 (no async void)

**Exact lines to extract:** 227-291

**Extracted helper signatures (ALL private instance methods):**
- `private TextBlock BuildWindowTitleBlock()`  // CCN est: 1, dispatcher-safe: Y
- `private Button BuildGlobalToggleButton()`  // CCN est: 1, dispatcher-safe: Y
- `private StackPanel BuildCopyModeSection()`  // CCN est: 1, dispatcher-safe: Y
- `private ScrollViewer BuildRulesScrollSection()`  // CCN est: 1, dispatcher-safe: Y
- `private Button BuildAddRuleButton()`  // CCN est: 1, dispatcher-safe: Y
- `private ScrollViewer BuildLogScrollSection()`  // CCN est: 1, dispatcher-safe: Y

**Extraction logic:**
- `BuildWindowTitleBlock`: create bold "Prop Trader Tools -- Trade Copier" TextBlock, return it.
- `BuildGlobalToggleButton`: create "Copy All OFF" Button, assign to `_globalToggleBtn`, wire `OnGlobalToggle`, return.
- `BuildCopyModeSection`: create mode label + ComboBox with 3 items ("Signal", "Mirror", "Clone"), wire `OnCopyModeComboChanged`, wrap in StackPanel, return.
- `BuildRulesScrollSection`: create `_rulesPanel` (StackPanel), assign `_rulesPanel`, call `BuildRuleRow("MES")` (or equivalent initial row), wrap in ScrollViewer with MaxHeight=400, return ScrollViewer.
- `BuildAddRuleButton`: create "+ Add Rule" Button, wire `OnAddRule`, return.
- `BuildLogScrollSection`: create `_logPanel` (StackPanel), assign `_logPanel`, wrap in ScrollViewer, return ScrollViewer.
- Parent `BuildUI` after extraction: create root DockPanel, call each builder, set DockPanel.Dock anchors, add to root. Estimated parent CCN: 1.

**NT8 thread safety:** All 6 helpers are private instance methods on `TradeCopierWindow`. Called synchronously on UI thread during window construction. No Dispatcher.InvokeAsync.

**7-scan checklist:**
- [ ] Scan 1 - JS-021 lock(): `grep -n "lock(" src/PropTraderTools/TradeCopierWindow.cs` -- zero new hits
- [ ] Scan 2 - JS-001 throw: `grep -n "throw new" src/PropTraderTools/TradeCopierWindow.cs` -- zero new hits in helpers
- [ ] Scan 3 - JS-002 return null: `grep -n "return null" src/PropTraderTools/TradeCopierWindow.cs` -- zero new nulls; each helper returns a constructed object
- [ ] Scan 4 - JS-033 async void: `grep -n "async void" src/PropTraderTools/TradeCopierWindow.cs` -- zero hits
- [ ] Scan 5 - CYC: `lizard src/PropTraderTools/TradeCopierWindow.cs --csv` -- `BuildUI` <= 8, all 6 helpers <= 8
- [ ] Scan 6 - JS-001 exceptions: no new `throw` in any helper
- [ ] Scan 7 - Build: `dotnet build src/PropTraderTools/PropTraderTools.csproj` -- zero errors

**Test requirement:**
- `[Fact] T_C09_01_BuildWindowTitleBlock_Text_ContainsPropTraderTools` -- verify Text contains "Prop Trader Tools"
- `[Fact] T_C09_02_BuildGlobalToggleButton_Content_ContainsCopyAll` -- verify Content string contains "Copy All" or "OFF"
- `[Fact] T_C09_03_BuildCopyModeSection_ComboBox_HasThreeItems` -- verify the ComboBox inside returned StackPanel has exactly 3 items
- `[Fact] T_C09_04_BuildRulesScrollSection_MaxHeight_Is400` -- verify returned ScrollViewer MaxHeight == 400
- `[Fact] T_C09_05_BuildLogScrollSection_AssignsLogPanel` -- verify `_logPanel` field is non-null after call

**Acceptance criterion:** `lizard --csv` CCN <= 8 for `BuildUI` and all 6 extracted helpers.

---

## Ticket C-10: OnLoaded

**File:** `src/PropTraderTools/TradeCopierPanel.cs`
**Class:** `FollowerItem` (nested inside `TradeCopierPanel`)
**Method:** `OnLoaded`
**Current CCN:** 40
**Spec req IDs:** JS-021 (no lock()), JS-001 (no throw in helpers), JS-002 (no return null), JS-033 (no async void)

**Exact lines to extract:** 798-849

**Extracted helper signatures (ALL private instance methods):**
- `private void PopulateFollowerItems()`  // CCN est: 3, dispatcher-safe: Y
- `private void BuildAllAccountsList()`  // CCN est: 3, dispatcher-safe: Y
- `private void RegisterAndInitializeModules()`  // CCN est: 2, dispatcher-safe: Y
- `private void WireModuleLicenses()`  // CCN est: 6, dispatcher-safe: Y
- `private void WireLeaderOrderHandlers()`  // CCN est: 2, dispatcher-safe: Y

**Extraction logic:**
- `PopulateFollowerItems`: guard `Account.All` null; foreach acc in `Account.All`: create `FollowerItem`, add to `followerItems`, wire `acc.AccountItemUpdate` handler. CYC: 3 (null guard + foreach + add guard).
- `BuildAllAccountsList`: clear `_allAccounts`; null-guard add `_leaderAccount`; foreach `followerItems`: add accounts that are non-leader and non-null. CYC: 3 (null guard + foreach + null/equality check).
- `RegisterAndInitializeModules`: clear `_modules`; call `AddModule` for each of the 5 module IDs (BE/Trim/Flatten/Cancel/Copier); foreach `_modules`: call `m.Initialize(this)`. CYC: 2 (foreach + sequential).
- `WireModuleLicenses`: foreach `_modules`: switch on `m.ModuleId` with 5 cases ("BE", "TRIM", "FLAT", "CANCEL", "COPY") each calling `m.SetEnabled(licenseFlag)`. CYC: 6 (foreach=1 + 5 switch cases).
- `WireLeaderOrderHandlers`: guard `_leaderAccount` null; wire `OrderUpdate` + `PositionUpdate` handlers; call `RefreshQuickDisplay()`. CYC: 2 (null guard + sequential).
- Parent `OnLoaded` after extraction: call each helper in sequence. Estimated parent CCN: 2 (retains outer guard for early-exit if required + sequential calls).

**NT8 thread safety:** All 5 helpers are private instance methods on the same class. `OnLoaded` is a WPF `Loaded` event handler -- fires on UI thread. Helpers execute synchronously. No Dispatcher.InvokeAsync added.

**7-scan checklist:**
- [ ] Scan 1 - JS-021 lock(): `grep -n "lock(" src/PropTraderTools/TradeCopierPanel.cs` -- zero new hits
- [ ] Scan 2 - JS-001 throw: `grep -n "throw new" src/PropTraderTools/TradeCopierPanel.cs` -- zero new hits in helpers
- [ ] Scan 3 - JS-002 return null: `grep -n "return null" src/PropTraderTools/TradeCopierPanel.cs` -- zero new nulls
- [ ] Scan 4 - JS-033 async void: `grep -n "async void" src/PropTraderTools/TradeCopierPanel.cs` -- zero hits
- [ ] Scan 5 - CYC: `lizard src/PropTraderTools/TradeCopierPanel.cs --csv` -- `OnLoaded` <= 8, all 5 helpers <= 8
- [ ] Scan 6 - JS-001 exceptions: no new `throw` in any helper
- [ ] Scan 7 - Build: `dotnet build src/PropTraderTools/PropTraderTools.csproj` -- zero errors

**Test requirement:**
- `[Fact] T_C10_01_PopulateFollowerItems_NullAccountAll_DoesNotThrow` -- verify no exception when Account.All is null
- `[Fact] T_C10_02_PopulateFollowerItems_WithAccounts_AddsFollowerItems` -- stub Account.All with 2 accounts, verify 2 FollowerItems added
- `[Fact] T_C10_03_BuildAllAccountsList_LeaderNotNull_IsFirstEntry` -- verify `_allAccounts[0]` is `_leaderAccount` when leader is non-null
- `[Fact] T_C10_04_RegisterAndInitializeModules_AddsFiveModules` -- verify `_modules.Count == 5` after call
- `[Fact] T_C10_05_WireModuleLicenses_BeModule_SetEnabledCalledWithBeFlag` -- verify BE module's `SetEnabled` receives the BE license boolean

**Acceptance criterion:** `lizard --csv` CCN <= 8 for `OnLoaded` and all 5 extracted helpers (`PopulateFollowerItems`, `BuildAllAccountsList`, `RegisterAndInitializeModules`, `WireModuleLicenses`, `WireLeaderOrderHandlers`).

---

## CCN REDUCTION SUMMARY

| Ticket | Method | File | CCN Before | Parent CCN After | Helper CCN Range | All <= 8? |
|--------|--------|------|-----------|-----------------|-----------------|----------|
| C-01 | BuildBufferedButtonsRow | TradeCopierPanel.cs | 87 | 2 | 1-3 | YES |
| C-02 | BuildInlineFollowerRow | TradeCopierPanel.cs | 64 | 1 | 1-4 | YES |
| C-03 | BuildCheckItemTemplate | TradeCopierPanel.cs | 61 | 1 | 1 | YES |
| C-04 | BuildActionButtons | TradeCopierWindow.cs | 58 | 1 | 1 | YES |
| C-05 | BuildModeRow | TradeCopierPanel.cs | 52 | 1 | 1 | YES |
| C-06 | BuildClickTraderRow | TradeCopierPanel.cs | 52 | 1 | 1 | YES |
| C-07 | DoInject | TradeCopierAddOn.cs | 43 | 7 | 4-5 | YES |
| C-08 | OnBeClick | TradeCopierPanel.cs | 43 | 4 | 2-3 | YES |
| C-09 | BuildUI | TradeCopierWindow.cs | 47 | 1 | 1 | YES |
| C-10 | OnLoaded | TradeCopierPanel.cs | 40 | 2 | 2-6 | YES |

**Total helpers extracted**: 40 private instance methods across 10 tickets.
**All helpers**: private instance, same class, same file, no static helpers, no new classes.
**Execution model**: synchronous on calling UI thread, no Dispatcher.InvokeAsync.

---

## GLOBAL ENGINEERING CONSTRAINTS (for ALL tickets)

1. **JS-021 (P0)** -- `lock()` is BANNED. Zero new lock() in any file.
2. **JS-001 (P0)** -- No `throw new XxxException(...)` in any extracted helper.
3. **JS-002 (P0)** -- No `return null` in any helper that returns a reference type.
4. **JS-033 (P0)** -- No `async void` anywhere. Event handlers remain synchronous `void`.
5. **ASCII-only** -- No Unicode in helper names or string literals beyond pre-existing `\u` escapes.
6. **Private instance only** -- Every helper is `private` and instance (not `static`).
7. **Same file** -- Helpers added to the same `.cs` file as their parent method.
8. **No NT8 API additions** -- No new `Account.CreateOrder`, `AtmStrategyCreate`, or other NT8 calls. Structural extraction only.
9. **No CopyEngine.cs or Ptt*.cs edits** -- Hard boundary. Any edit to those files = ticket failure.
10. **Build must pass** -- `dotnet build src/PropTraderTools/PropTraderTools.csproj` zero errors after each ticket.

---

## RETURN STATUS: TICKETS_COMPLETE
