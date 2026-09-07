# WAVE1-LANE-C Architecture Plan
**Phase 1 -- PTT Pipeline**
**Output**: `docs/brain/WAVE1-LANE-C/02-architecture-plan.md`
**Architect**: PTT Architect (Phase 1)
**Date**: 2026-09-07

---

## RULES CATALOG GATE RESULT: PASS

Sources read:
- `docs/standards/jane-street/RULES_CATALOG.md` (JS-001..JS-110)
- `docs/intel/jane-street/complexity-reduction.md` (CYC <= 8 patterns)
- `docs/intel/jane-street/building-tools-for-traders.md` (exhaustive pattern matching, keyboard-first UI)

No P0 violations found in target code. See P0 scan results below.

---

## LANE-SPLIT GATE RESULT: SINGLE-PIPELINE

Gate evaluation:

| Question | Answer | Rationale |
|----------|--------|-----------|
| Q1. All 10 methods within the same 3 files or within 50 lines? | YES | All 10 methods are within the 3 files defined in Lane C scope |
| Q2. Does fix B design depend on fix A final design? | NO | Each method is independently extractable |
| Q3. Does each fix have standalone value if others are blocked? | YES | Any reduction independently delivers CCN improvement |
| Q4. Independent SIM/lizard verification path? | YES | lizard per-method CSV output verifies each independently |

**Decision**: Q1=YES triggers default SINGLE-PIPELINE. All 10 tickets execute in sequence within one pipeline.

---

## P0 SCAN RESULTS

### TradeCopierPanel.cs
```
Select-String -Pattern "lock\(" -Path src/PropTraderTools/TradeCopierPanel.cs
Line 1297: // JS-021: no lock(). JS-033: synchronous void event handler -- not async void.
```
**Result**: Comment only -- NOT a live lock() call. PASS.

### TradeCopierWindow.cs
```
Select-String -Pattern "lock\(" -Path src/PropTraderTools/TradeCopierWindow.cs
Line 581: // All helpers: private instance, UI-thread only, CYC <= 2, no lock(), no async void, no return null...
```
**Result**: Comment only -- NOT a live lock() call. PASS.

### TradeCopierAddOn.cs
```
Select-String -Pattern "lock\(" -Path src/PropTraderTools/TradeCopierAddOn.cs
(no output)
```
**Result**: Zero hits. PASS.

**P0 GATE: ALL 3 FILES PASS -- zero live lock() calls.**

---

## SCOPE

Files (ONLY these 3):
- `src/PropTraderTools/TradeCopierPanel.cs`
- `src/PropTraderTools/TradeCopierWindow.cs`
- `src/PropTraderTools/TradeCopierAddOn.cs`

Files NOT touched (hard boundary):
- `src/PropTraderTools/CopyEngine.cs`
- `src/PropTraderTools/PttBreakEven.cs`
- Any `src/PropTraderTools/Ptt*.cs`

---

## NT8 THREAD SAFETY GLOBAL NOTE

All 10 target methods execute on the WPF UI (dispatcher) thread:
- Build* methods: called from BuildUI() invoked in panel/window constructors (UI thread)
- OnLoaded: WPF Loaded event fires on UI thread
- OnBeClick: WPF RoutedEventHandler fires on UI thread
- DoInject: called via Dispatcher.InvokeAsync in TryInject (confirmed UI thread)

**All extracted helpers MUST be private instance methods on the SAME class.**
**NO Task.Run, no background threads, no Dispatcher.InvokeAsync wrapping in any helper.**
**All helpers execute synchronously on the calling UI thread.**

---

## PER-METHOD EXTRACTION PLANS

---

### C-01: TradeCopierPanel.BuildBufferedButtonsRow
**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Lizard CCN**: 87 (line 1131-1222, live version)
**Ticket**: C-01

**Root Cause of High CCN**: The specs array initializer with 6 entries each containing Action<Button> lambdas (b => _beBtn2 = b etc.) plus the s.Teal conditional inside the foreach loop. Lizard counts each lambda expression and each conditional branch.

**Extraction Strategy**: Extract the foreach loop body into a named helper. Move the _quickT3Row construction into its own helper.

**Helpers** (all private void on TradeCopierPanel):

| Helper Name | Signature | CYC Est | Dispatcher-Safe | Purpose |
|-------------|-----------|---------|-----------------|---------|
| `BuildSingleButtonCluster` | `private void BuildSingleButtonCluster(string content, bool isTeal, RoutedEventHandler upHandler, RoutedEventHandler downHandler, RoutedEventHandler mainHandler, Action<Button> storeAction, Panel targetPanel)` | 3 | Y -- UI thread, no Dispatcher needed | Builds one DockPanel cluster (arrow spinners + main button), applies teal styling if isTeal, stores button to field via storeAction, adds cluster to targetPanel |
| `BuildQuickT3HiddenRow` | `private void BuildQuickT3HiddenRow(StackPanel root)` | 1 | Y -- UI thread | Builds collapsed _quickT3Row StackPanel with "T3 hidden" label, assigns _quickT3Row, adds to root |

**Parent BuildBufferedButtonsRow after extraction**:
- Init row1 + _beRowPanel + _quickRowPanel (sequential, no branch = CYC 0)
- Build specs array (value tuple array init = CYC 0)
- foreach s in specs -> BuildSingleButtonCluster(s...) (foreach = CYC 1)
- root.Children.Add(row1) (CYC 0)
- BuildQuickT3HiddenRow(root) (CYC 0)

**Estimated parent post-extraction CCN**: 2

**Acceptance criterion:** lizard --csv CCN <= 8 for parent method and all extracted helpers.

**Test names**:
- T_C01_01_BuildSingleButtonCluster_TealTrue_SetsBorderBrushToTeal
- T_C01_02_BuildSingleButtonCluster_TealFalse_DoesNotSetBorderBrush
- T_C01_03_BuildQuickT3HiddenRow_AddsCollapsedRowToRoot
- T_C01_04_BuildSingleButtonCluster_StoreAction_AssignsButtonToField

---

### C-02: TradeCopierPanel.BuildInlineFollowerRow
**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Lizard CCN**: 64 (line 2042-2126)
**Ticket**: C-02

**Root Cause of High CCN**: Two inline lambda event handlers (Checked/Unchecked) with multi-statement bodies, each capturing item and atmCombo. Lizard counts each lambda and each statement inside as branches.

**Extraction Strategy**: Extract each column element into its own builder helper. Extract lambda wiring into a dedicated handler-wiring method.

**Helpers** (all private on TradeCopierPanel):

| Helper Name | Signature | CYC Est | Dispatcher-Safe | Purpose |
|-------------|-----------|---------|-----------------|---------|
| `BuildFollowerCheckBox` | `private CheckBox BuildFollowerCheckBox(FollowerItem item)` | 1 | Y | Creates CheckBox bound to item.IsSelected |
| `BuildFollowerNameLabel` | `private TextBlock BuildFollowerNameLabel(FollowerItem item)` | 1 | Y | Creates 90px TextBlock with account name |
| `BuildFollowerPnlLabel` | `private TextBlock BuildFollowerPnlLabel(FollowerItem item)` | 1 | Y | Creates 64px P&L TextBlock with DailyPnlColor foreground |
| `BuildFollowerAtmComboBox` | `private ComboBox BuildFollowerAtmComboBox(FollowerItem item)` | 1 | Y | Creates 120px ATM ComboBox, wires LoadedEvent + SelectionChanged + DataContext |
| `WireFollowerCheckBoxHandlers` | `private void WireFollowerCheckBoxHandlers(FollowerItem item, CheckBox chk, ComboBox atmCombo)` | 4 | Y | Wires Checked/Unchecked lambdas; each lambda sets IsSelected, sets atmCombo.IsEnabled, calls SortFollowerRows + UpdateCopierHeader + TryAutoApply |

**Estimated parent post-extraction CCN**: 1

**Acceptance criterion:** lizard --csv CCN <= 8 for parent method and all extracted helpers.

**Test names**:
- T_C02_01_BuildFollowerCheckBox_IsChecked_ReflectsItemIsSelected
- T_C02_02_BuildFollowerPnlLabel_Foreground_EqualsDailyPnlColor
- T_C02_03_BuildFollowerAtmComboBox_IsEnabled_ReflectsItemIsSelected
- T_C02_04_WireFollowerCheckBoxHandlers_Checked_SetsIsSelectedTrue
- T_C02_05_WireFollowerCheckBoxHandlers_Unchecked_SetsIsSelectedFalse

---

### C-03: TradeCopierPanel.BuildCheckItemTemplate
**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Lizard CCN**: 61 (line 2297-2372)
**Ticket**: C-03

**Root Cause of High CCN**: FrameworkElementFactory.SetBinding, AddHandler, SetValue chains. Each AddHandler call creates a delegate branch in lizard model. 5 columns with multiple operations each.

**Extraction Strategy**: Extract each column factory into its own named builder. Each returns a FrameworkElementFactory.

**Helpers** (all private on TradeCopierPanel, each returns FrameworkElementFactory):

| Helper Name | Signature | CYC Est | Dispatcher-Safe | Purpose |
|-------------|-----------|---------|-----------------|---------|
| `BuildTemplateAccountNameColumn` | `private FrameworkElementFactory BuildTemplateAccountNameColumn()` | 1 | Y | Col 0: TextBlock with Account.Name binding, CharacterEllipsis |
| `BuildTemplatePnlColumn` | `private FrameworkElementFactory BuildTemplatePnlColumn()` | 1 | Y | Col 1: TextBlock with DailyPnlText + DailyPnlColor bindings, right-aligned |
| `BuildTemplateMultiplierColumn` | `private FrameworkElementFactory BuildTemplateMultiplierColumn()` | 1 | Y | Col 2: TextBox w=30, Text="1", Collapsed, wires OnFollowerMultiplierChanged |
| `BuildTemplateAtmComboColumn` | `private FrameworkElementFactory BuildTemplateAtmComboColumn()` | 1 | Y | Col 3: ComboBox w=120, wires LoadedEvent + SelectionChangedEvent handlers |
| `BuildTemplateCheckBoxColumn` | `private FrameworkElementFactory BuildTemplateCheckBoxColumn()` | 1 | Y | Col 4: CheckBox with TwoWay IsSelected binding, wires OnFollowerChecked click |

**Estimated parent post-extraction CCN**: 1

**Acceptance criterion:** lizard --csv CCN <= 8 for parent method and all extracted helpers.

**Test names**:
- T_C03_01_BuildTemplateAccountNameColumn_ColumnIndex_IsZero
- T_C03_02_BuildTemplatePnlColumn_TextAlignment_IsRight
- T_C03_03_BuildTemplateMultiplierColumn_Visibility_IsCollapsed
- T_C03_04_BuildTemplateAtmComboColumn_Width_Is120
- T_C03_05_BuildTemplateCheckBoxColumn_ColumnIndex_IsFour

---

### C-04: TradeCopierWindow.BuildActionButtons
**File**: `src/PropTraderTools/TradeCopierWindow.cs`
**Lizard CCN**: 58 (line 753-815)
**Ticket**: C-04

**Root Cause of High CCN**: Five buttons each with Tag assignment, list registration (_trimBtns.Add()), Grid column placement, and Click handler wiring. Lizard counts each event subscription as a branch.

**Extraction Strategy**: Extract each button build+register+place into its own helper.

**Helpers** (all private void on TradeCopierWindow):

| Helper Name | Signature | CYC Est | Dispatcher-Safe | Purpose |
|-------------|-----------|---------|-----------------|---------|
| `BuildTrimActionButton` | `private void BuildTrimActionButton(object tag, Grid grid)` | 1 | Y | Creates [1/2] Trim button, sets col 3, registers to _trimBtns, wires OnRuleTrim |
| `BuildFlattenActionButton` | `private void BuildFlattenActionButton(object tag, Grid grid)` | 1 | Y | Creates [=] Flatten button, sets col 4, registers to _flattenBtns, wires OnRuleFlatten |
| `BuildCancelActionButton` | `private void BuildCancelActionButton(object tag, Grid grid)` | 1 | Y | Creates [x] Cancel button, sets col 5, registers to _cancelBtns, wires OnRuleCancel |
| `BuildToggleActionButton` | `private void BuildToggleActionButton(object tag, Grid grid)` | 1 | Y | Creates [ON] Toggle button, sets col 6, wires OnRuleToggle |
| `BuildApplyActionButton` | `private void BuildApplyActionButton(object tag, ComboBox leaderCb, ListBox followerLb, ComboBox atmCb, TextBox namedBox, Grid grid)` | 1 | Y | Creates Apply button, constructs tag array, sets col 7, wires OnRowApply |

**Estimated parent post-extraction CCN**: 1

**Acceptance criterion:** lizard --csv CCN <= 8 for parent method and all extracted helpers.

**Test names**:
- T_C04_01_BuildTrimActionButton_GridColumn_IsThree
- T_C04_02_BuildFlattenActionButton_AddedTo_FlattenBtnsList
- T_C04_03_BuildCancelActionButton_Background_IsWBrushInactive
- T_C04_04_BuildToggleActionButton_GridColumn_IsSix
- T_C04_05_BuildApplyActionButton_TagArray_ContainsFiveElements

---

### C-05: TradeCopierPanel.BuildModeRow
**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Lizard CCN**: 52 (line 1668-1724)
**Ticket**: C-05

**Root Cause of High CCN**: RadioButton and Button creation with inline Click wiring counted by lizard as branches. Multiple controls with event handlers compound the CYC count.

**Extraction Strategy**: Extract each control into its own helper that creates, configures, and returns the control. Parent assigns to instance field.

**Helpers** (all private on TradeCopierPanel):

| Helper Name | Signature | CYC Est | Dispatcher-Safe | Purpose |
|-------------|-----------|---------|-----------------|---------|
| `BuildSignalRadioButton` | `private RadioButton BuildSignalRadioButton()` | 1 | Y | Creates Signal RadioButton (IsChecked=true, margin), wires OnSignalModeClick |
| `BuildMirrorRadioButton` | `private RadioButton BuildMirrorRadioButton()` | 1 | Y | Creates Mirror RadioButton, wires OnMirrorModeClick |
| `BuildCloneRadioButton` | `private RadioButton BuildCloneRadioButton()` | 1 | Y | Creates Clone RadioButton (B50), wires OnCloneModeClick |
| `BuildCopyToggleButton` | `private Button BuildCopyToggleButton()` | 1 | Y | Creates COPY OFF Button with inactive border styling, wires OnCopyToggle |

**Estimated parent post-extraction CCN**: 1

**Acceptance criterion:** lizard --csv CCN <= 8 for parent method and all extracted helpers.

**Test names**:
- T_C05_01_BuildSignalRadioButton_IsChecked_True
- T_C05_02_BuildMirrorRadioButton_IsChecked_False
- T_C05_03_BuildCloneRadioButton_Content_IsClone
- T_C05_04_BuildCopyToggleButton_Content_IsCopyOff

---

### C-06: TradeCopierPanel.BuildClickTraderRow
**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Lizard CCN**: 52 (line 986-1044)
**Ticket**: C-06

**Root Cause of High CCN**: Four controls (ToggleButton x2, Button x2) each with inline Click wiring, style reference, and property assignments. Each event handler registration counted as a branch by lizard.

**Extraction Strategy**: Extract each control into its own helper that creates, configures, and returns the control.

**Helpers** (all private on TradeCopierPanel):

| Helper Name | Signature | CYC Est | Dispatcher-Safe | Purpose |
|-------------|-----------|---------|-----------------|---------|
| `BuildBuyToggleButton` | `private ToggleButton BuildBuyToggleButton()` | 1 | Y | Creates Buy ToggleButton (IsChecked=true, W=45, H=22), wires OnBuyToggleClick |
| `BuildSellToggleButton` | `private ToggleButton BuildSellToggleButton()` | 1 | Y | Creates Sell ToggleButton, wires OnSellToggleClick |
| `BuildArmButton` | `private Button BuildArmButton()` | 1 | Y | Creates Arm Button (W=48, H=22, dark background), wires OnArmClick |
| `BuildClickTraderCancelButton` | `private Button BuildClickTraderCancelButton()` | 1 | Y | Creates Cancel Button with BrushDanger border styling, wires OnCancel2 |

**Estimated parent post-extraction CCN**: 1

**Acceptance criterion:** lizard --csv CCN <= 8 for parent method and all extracted helpers.

**Test names**:
- T_C06_01_BuildBuyToggleButton_IsChecked_True
- T_C06_02_BuildSellToggleButton_Width_Is45
- T_C06_03_BuildArmButton_Width_Is48
- T_C06_04_BuildClickTraderCancelButton_BorderBrush_IsBrushDanger

---

### C-07: TradeCopierAddOn.DoInject
**File**: `src/PropTraderTools/TradeCopierAddOn.cs`
**Lizard CCN**: 43 (line 477-532)
**Ticket**: C-07

**Root Cause of High CCN**: Multiple nested null guards, a stale-panel purge loop with two inner if-guards, error handling try/catch, and multi-step injection sequence. Each conditional and loop body adds CYC.

**Extraction Strategy**: Extract the stale panel purge loop into its own method. Extract the new panel wiring sequence into its own method. Parent becomes a sequential coordinator.

**Helpers** (all private on TradeCopierAddOn):

| Helper Name | Signature | CYC Est | Dispatcher-Safe | Purpose |
|-------------|-----------|---------|-----------------|---------|
| `PurgeStalePanel` | `private void PurgeStalePanel(System.Windows.Controls.Grid grid)` | 4 | Y -- called from UI-thread DoInject | Collects UIElements with GetType().Name == "TradeCopierPanel"; removes each stale child + its RowDefinition guard-gated by index > 0 and index < count |
| `WireNewPanel` | `private void WireNewPanel(TradeCopierPanel panel, Chart chart, ChartTrader chartTrader, System.Windows.Controls.Grid grid)` | 5 | Y -- UI thread | Wires instrument (null-guarded try/catch), starts ATR engine, sets chart, wires leader account, adds SIM101 diagnostic handler, removes SIM101, hooks keyboard shortcut, adds grid RowDefinition + grid row |

**Estimated parent post-extraction CCN**: 7

**Acceptance criterion:** lizard --csv CCN <= 8 for parent method and all extracted helpers.

**Test names**:
- T_C07_01_PurgeStalePanel_RemovesChildByTypeName_TradeCopierPanel
- T_C07_02_PurgeStalePanel_RemovesRowDefinitionAtStaleRowIndex
- T_C07_03_PurgeStalePanel_DoesNotRemoveRow0
- T_C07_04_WireNewPanel_NullInstrument_DoesNotThrow
- T_C07_05_DoInject_DuplicateChart_ReturnsFalseOnTryAdd

---

### C-08: TradeCopierPanel.OnBeClick
**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Lizard CCN**: 43 (line 1425-1470)
**Ticket**: C-08

**Root Cause of High CCN**: Three NinjaTrader.Code.Output.Process(...) calls with multi-line string concatenation (+). Lizard counts each + operator as an additional branch. The FSM logic itself is CYC=5 (confirmed in docstring). String concatenation is the primary inflator.

**Extraction Strategy**: Extract each FSM arm (Idle and Armed) into its own handler method. Each extracted method contains the logging + state mutation for one FSM state.

**Helpers** (all private on TradeCopierPanel):

| Helper Name | Signature | CYC Est | Dispatcher-Safe | Purpose |
|-------------|-----------|---------|-----------------|---------|
| `ExecuteBeIdle` | `private void ExecuteBeIdle(Account leader, NinjaTrader.Cbi.Instrument instrument)` | 3 | Y -- UI thread | Handles BeState.Idle: logs intent, tests IsPriceAlreadyAtBe (1 branch), either calls DispatchModule("BE") staying Idle OR calls ArmPendingBe + sets _beState=Armed + UpdateBeVisuals |
| `ExecuteBeArmed` | `private void ExecuteBeArmed(Account leader)` | 2 | Y -- UI thread | Handles BeState.Armed: logs disarm, calls DisarmPendingBe, sets _beState=Idle, calls UpdateBeVisuals(BeState.Idle) |

**Estimated parent post-extraction CCN**: 4

**Acceptance criterion:** lizard --csv CCN <= 8 for parent method and all extracted helpers.

**Test names**:
- T_C08_01_ExecuteBeIdle_PriceAtBe_CallsDispatchModuleBe
- T_C08_02_ExecuteBeIdle_PriceNotAtBe_SetsBeStateArmed
- T_C08_03_ExecuteBeIdle_PriceNotAtBe_CallsArmPendingBe
- T_C08_04_ExecuteBeArmed_SetsBeStateIdle
- T_C08_05_ExecuteBeArmed_CallsDisarmPendingBe

---

### C-09: TradeCopierWindow.BuildUI
**File**: `src/PropTraderTools/TradeCopierWindow.cs`
**Lizard CCN**: 47 (line 227-289)
**Ticket**: C-09

**Root Cause of High CCN**: Long sequential UI construction with many DockPanel.SetDock + root.Children.Add pairs, inline ComboBox population (3 Items.Add calls), event handler wiring. Lizard counts each Items.Add and event subscription as branches.

**Extraction Strategy**: Extract each logical UI section into its own helper. Helpers that set _rulesPanel/_logPanel assign the instance field directly.

**Helpers** (all private on TradeCopierWindow):

| Helper Name | Signature | CYC Est | Dispatcher-Safe | Purpose |
|-------------|-----------|---------|-----------------|---------|
| `BuildWindowTitleBlock` | `private TextBlock BuildWindowTitleBlock()` | 1 | Y | Creates bold "Prop Trader Tools -- Trade Copier" TextBlock |
| `BuildGlobalToggleButton` | `private Button BuildGlobalToggleButton()` | 1 | Y | Creates "Copy All OFF" Button, assigns _globalToggleBtn, wires OnGlobalToggle |
| `BuildCopyModeSection` | `private StackPanel BuildCopyModeSection()` | 1 | Y | Creates mode label + ComboBox with 3 items (Signal/Mirror/Clone), wires OnCopyModeComboChanged |
| `BuildRulesScrollSection` | `private ScrollViewer BuildRulesScrollSection()` | 1 | Y | Creates _rulesPanel StackPanel + BuildRuleRow("MES") + ScrollViewer MaxHeight=400, assigns _rulesPanel |
| `BuildAddRuleButton` | `private Button BuildAddRuleButton()` | 1 | Y | Creates "+ Add Rule" Button, wires OnAddRule |
| `BuildLogScrollSection` | `private ScrollViewer BuildLogScrollSection()` | 1 | Y | Creates _logPanel StackPanel, assigns _logPanel, wraps in ScrollViewer |

**Estimated parent post-extraction CCN**: 1

**Acceptance criterion:** lizard --csv CCN <= 8 for parent method and all extracted helpers.

**Test names**:
- T_C09_01_BuildWindowTitleBlock_Text_ContainsPropTraderTools
- T_C09_02_BuildGlobalToggleButton_Content_IsCopyAllOff
- T_C09_03_BuildCopyModeSection_ComboBox_HasThreeItems
- T_C09_04_BuildRulesScrollSection_MaxHeight_Is400
- T_C09_05_BuildLogScrollSection_AssignsLogPanel

---

### C-10: TradeCopierPanel.OnLoaded
**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Lizard CCN**: 40 (line 798-846)
**Ticket**: C-10

**Root Cause of High CCN**: Multiple foreach loops, a switch with 5 cases (module license wiring), null guards, and sequential event wiring. The switch alone contributes +5 CYC; each foreach adds +1; null guard adds +1.

**Extraction Strategy**: Extract each logical phase of Loaded initialization into its own helper.

**Helpers** (all private void on TradeCopierPanel):

| Helper Name | Signature | CYC Est | Dispatcher-Safe | Purpose |
|-------------|-----------|---------|-----------------|---------|
| `PopulateFollowerItems` | `private void PopulateFollowerItems()` | 3 | Y | Guards Account.All null; foreach acc: adds FollowerItem, wires acc.AccountItemUpdate |
| `BuildAllAccountsList` | `private void BuildAllAccountsList()` | 3 | Y | Clears _allAccounts; adds _leaderAccount (null guard); foreach followerItems adds non-leader non-null accounts |
| `RegisterAndInitializeModules` | `private void RegisterAndInitializeModules()` | 2 | Y | Clears _modules; AddModule x5 (BE/Trim/Flatten/Cancel/Copier); foreach _modules: m.Initialize(this) |
| `WireModuleLicenses` | `private void WireModuleLicenses()` | 6 | Y | foreach _modules: switch m.ModuleId { "BE","TRIM","FLAT","CANCEL","COPY" -> m.SetEnabled(licenseFlag) } |
| `WireLeaderOrderHandlers` | `private void WireLeaderOrderHandlers()` | 2 | Y | Guards _leaderAccount null; wires OrderUpdate + PositionUpdate; calls RefreshQuickDisplay |

**Estimated parent post-extraction CCN**: 2

**Acceptance criterion:** lizard --csv CCN <= 8 for parent method and all extracted helpers.

**Test names**:
- T_C10_01_PopulateFollowerItems_NullAccountAll_DoesNotThrow
- T_C10_02_PopulateFollowerItems_WithAccounts_AddsFollowerItems
- T_C10_03_BuildAllAccountsList_LeaderNotNull_IsFirstEntry
- T_C10_04_RegisterAndInitializeModules_AddsFiveModules
- T_C10_05_WireModuleLicenses_BeModule_SetEnabledCalledWithBeFlag

---

## CCN SUMMARY TABLE

| Ticket | Method | File | CCN Before | CCN Parent After | Helper CCN Range | All <= 8? |
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

---

## TICKET ASSIGNMENTS

| Ticket | Method | Class | File |
|--------|--------|-------|------|
| C-01 | BuildBufferedButtonsRow | TradeCopierPanel | TradeCopierPanel.cs |
| C-02 | BuildInlineFollowerRow | TradeCopierPanel | TradeCopierPanel.cs |
| C-03 | BuildCheckItemTemplate | TradeCopierPanel | TradeCopierPanel.cs |
| C-04 | BuildActionButtons | TradeCopierWindow | TradeCopierWindow.cs |
| C-05 | BuildModeRow | TradeCopierPanel | TradeCopierPanel.cs |
| C-06 | BuildClickTraderRow | TradeCopierPanel | TradeCopierPanel.cs |
| C-07 | DoInject | TradeCopierAddOn | TradeCopierAddOn.cs |
| C-08 | OnBeClick | TradeCopierPanel | TradeCopierPanel.cs |
| C-09 | BuildUI | TradeCopierWindow | TradeCopierWindow.cs |
| C-10 | OnLoaded | TradeCopierPanel | TradeCopierPanel.cs |

---

## ARCHITECTURE CONSTRAINTS (embedded per-ticket)

All engineers implementing these tickets MUST adhere:
1. **No lock()** -- JS-021. Zero lock() in any new or modified code.
2. **No async void** -- JS-033. Event handlers are synchronous void. Never add async.
3. **No return null** -- JS-002. Helpers returning reference types return a constructed object or void.
4. **ASCII-only identifiers** -- No Unicode in helper names or string literals beyond existing \u escapes.
5. **Private instance methods only** -- All helpers are private on the same class. No static helpers. No new classes.
6. **UI thread -- no Dispatcher.InvokeAsync** -- All helpers called synchronously on calling UI thread.
7. **Same file** -- Helpers added in the same .cs file as the parent method.
8. **CYC <= 8 for ALL methods post-extraction** -- Parent + each helper must pass lizard --csv with CCN <= 8.
9. **Field assignment in parent** -- When a helper returns a value for an instance field, assignment _fieldName = BuildHelper() is in the parent unless helper signature documents direct field assignment.
10. **No NT8 API changes** -- No new calls to NT8 APIs (Account.CreateOrder, AtmStrategyCreate, etc.). Pure structural extractions only.

---

## RETURN STATUS: PLAN_COMPLETE