# PTT-COPIER-B11 -- Architecture Plan
# Block: PTT-COPIER-B11
# Status: PLAN_COMPLETE (revised -- V1+V2 fixes applied 2026-07-11)
# Author: ptt-architect
# Date: 2026-07-11
# Phase: 1 (Architecture)

---

## 0. Block Summary

B11 delivers a **chart-level keyboard shortcut layer** (T1) and **ATM template selection
from the panel** (T2), while closing 4 carry-forward backlog items from B10.

B11 introduces a pre-implementation validation gate (SIM101 DW-B11-HK-01) that must PASS
before the keyboard shortcut layer is implemented. If SIM101 FAILS, T1+T2 defer to backlog
and B11 is still marked PIPELINE_COMPLETE with 0 tickets completed.

---

## 1. Block Scope

### T1 -- DW-B11-HK-01: PreviewKeyDown Keyboard Shortcut Layer
- Wire `chart.PreviewKeyDown` handler AFTER panel is attached in `DoInject()`.
- Unhook in `OnWindowDestroyed()` before `panel.Detach()`.
- Four shortcuts: Ctrl+Shift+T=Trim, Ctrl+Shift+F=FlattenAll,
  Ctrl+Shift+C=CancelPendingEntries, Ctrl+Shift+B=BreakEven.
- Calls EXISTING CopyEngine public methods -- no new CopyEngine code.
- CYC: `OnChartKeyDown`=3, `DispatchShortcut`=5.
- **Key.F shortcut** calls current `CopyEngine.Flatten(Instrument)` -- market order.
  DW-B12-BUFFERED-BUTTONS-01 will convert to `OrderType.Limit@bid+buffer` in B12.
  This is known spec debt, explicitly deferred.
- **Key.T shortcut** calls current `CopyEngine.Trim(Instrument)` -- market order.
  DW-B12-BUFFERED-BUTTONS-01 will convert to `OrderType.Limit@ask-buffer` in B12.
  This is known spec debt, explicitly deferred.

### T1 also closes:
- **DW-B10-01** (P2): Remove BuildDiagRow/OnDiagGap001d/OnDiagGap002 scaffolding from
  `TradeCopierPanel.cs` and `TradeCopierAddOn.cs` (RunGap001dTest, RunGap002Test).
- **DW-B10-04** (P1): Update `docs/standards/NT8_ADDON_KNOWLEDGE.md` with B10-T4
  confirmed chart attachment result (DispatcherTimer, CS1061).

### T2 -- DW-B11-HK-02: Focus-independence verification + ATM Template Writer
- Verify PreviewKeyDown fires after chart canvas click (panel does not need focus).
- Add ATM template writer: read .xml template filenames from NT8 install path;
  populate a new ComboBox in the panel for selecting the active ATM template.
- CYC: `BuildAtmTemplateRow`=1, `LoadAtmTemplates`=3, `OnAtmTemplateSelectionChanged`=2.

### T2 also closes:
- **DW-B10-02** (P1): Add 3 missing AtrSizingEngine xUnit tests.
- **DW-B10-03** (P2): Add Arm BE cluster to `TradeCopierWindow.cs` rule rows (Window surface).

### Shelved (do NOT include in B11):
- DW-B9-01 (ATR box on chart canvas)
- DW-B9-03 (Click trader Bid+1/Ask-1)
- DW-B10-01 remaining: Buy Ask / Sell Bid buttons, Full-panel mode

---

## 2. SIM101 Validation Protocol -- DW-B11-HK-01 (MANDATORY)

This gate MUST execute before writing the production shortcut layer.

### Step 1 -- Wire LOGGING-ONLY handler
In `DoInject()`, after `_panels[chart] = panel`, add:

```csharp
chart.PreviewKeyDown += _sim101KeyDiag;  // LOGGING-ONLY; remove after SIM101
```

Handler:
```csharp
private static void _sim101KeyDiag(object sender, KeyEventArgs e)
{
    // Writes key + modifier state to panel status text.
    // Uses Application.Current.Dispatcher.InvokeAsync for thread safety.
    string msg = "KB: " + e.Key + " M=" + Keyboard.Modifiers;
    System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
    {
        // Find the panel for this chart and update its status text.
        var chart = sender as Chart;
        if (chart == null) return;
        TradeCopierPanel p;
        if (_panels.TryGetValue(chart, out p))
            p.SetStatusText(msg);   // temporary public helper for SIM101 only
    });
}
```

### Step 2 -- Execute test
1. Open a chart in NT8 with the AddOn loaded.
2. Click anywhere on the chart canvas (to ensure focus is on the chart, NOT a panel TextBox).
3. Press Ctrl+Shift+T.
4. Observe TradeCopierPanel status text.

### Step 3 -- Evaluate result

| Outcome | Status Text Shows | Action |
|---------|-----------------|--------|
| PASS | "KB: T M=Control, Shift" | **Remove `_sim101KeyDiag` from `chart.PreviewKeyDown` first** (see V2 note below), then implement full shortcut layer as designed in §4. The production `HookKeyShortcut` call adds `panel.OnChartKeyDown` AFTER the diag handler is removed. |
| FAIL | Status text unchanged | **Remove `_sim101KeyDiag` from `chart.PreviewKeyDown` first** (same V2 note applies — the handler MUST be removed even on FAIL), then mark VERIFIED_NOT_FEASIBLE. Defer T1+T2 entire keyboard scope to B12 backlog. B11 is PIPELINE_COMPLETE with 0 shortcut tickets. |

> **V2 -- `_sim101KeyDiag` removal (leak guard)**
>
> `_sim101KeyDiag` is stored as a **class-level field** on `TradeCopierAddOn`:
> ```csharp
> private KeyEventHandler _sim101KeyDiag;  // set once in RunSim101(); cleared in RemoveSim101()
> ```
> A dedicated `RemoveSim101(Chart chart)` helper in `TradeCopierAddOn.cs` performs the unhook:
> ```csharp
> // Removes the SIM101 diag handler from chart.PreviewKeyDown.
> // Called unconditionally -- whether SIM101 passes OR fails.
> // MUST be called before HookKeyShortcut() adds the production handler (PASS path)
> // or before B11 is declared PIPELINE_COMPLETE (FAIL path).
> private static void RemoveSim101(Chart chart)
> {
>     if (_sim101KeyDiag != null)
>         chart.PreviewKeyDown -= _sim101KeyDiag;
>     _sim101KeyDiag = null;
> }
> ```
> `UnhookKeyShortcut` (§4.1) is NOT responsible for `_sim101KeyDiag` — it only unhooks
> `panel.OnChartKeyDown` (the production handler). `RemoveSim101` is a separate, explicit
> unhook call with a single responsibility.
>
> **Order of operations (PASS path)**:
> 1. `RemoveSim101(chart)` -- removes diag handler
> 2. `HookKeyShortcut(chart, panel)` -- adds production handler
>
> **Order of operations (FAIL path)**:
> 1. `RemoveSim101(chart)` -- removes diag handler
> 2. Mark VERIFIED_NOT_FEASIBLE; no production handler is added
>
> `_sim101KeyDiag` is ALWAYS null after leaving the SIM101 phase, regardless of outcome.

### Why PreviewKeyDown (not KeyBinding):
- `TradeCopierWindow.cs` comment (line 8) notes: "Shift+B KeyBinding removed --
  WPF KeyGesture rejects Shift+letter in NT8 host." Raw PreviewKeyDown is a
  tunneling RoutedEvent that fires on the Window root regardless of child focus.
  It bypasses the WPF InputBinding/CommandBinding system that fails in NT8's host.

---

## 3. Component List

| Component | File | Type | New/Modified |
|-----------|------|------|--------------|
| `TradeCopierAddOn._keyHandlers` | TradeCopierAddOn.cs | Field | NEW |
| `TradeCopierAddOn._sim101KeyDiag` | TradeCopierAddOn.cs | Field | NEW (SIM101 only; nulled by RemoveSim101) |
| `TradeCopierAddOn.HookKeyShortcut` | TradeCopierAddOn.cs | Method | NEW |
| `TradeCopierAddOn.UnhookKeyShortcut` | TradeCopierAddOn.cs | Method | NEW |
| `TradeCopierAddOn.RemoveSim101` | TradeCopierAddOn.cs | Method | NEW (SIM101 diag unhook -- called unconditionally) |
| `TradeCopierAddOn.DoInject` | TradeCopierAddOn.cs | Method | MODIFIED |
| `TradeCopierAddOn.OnWindowDestroyed` | TradeCopierAddOn.cs | Method | MODIFIED |
| `TradeCopierAddOn.RunGap001dTest` | TradeCopierAddOn.cs | Method | DELETE |
| `TradeCopierAddOn.RunGap002Test` | TradeCopierAddOn.cs | Method | DELETE |
| `TradeCopierAddOn.OnGap002AccountUpdate` | TradeCopierAddOn.cs | Method | DELETE |
| `TradeCopierAddOn._gap002TickCount` | TradeCopierAddOn.cs | Field | DELETE |
| `TradeCopierAddOn._gap002Account` | TradeCopierAddOn.cs | Field | DELETE |
| `TradeCopierPanel.OnChartKeyDown` | TradeCopierPanel.cs | Method | NEW |
| `TradeCopierPanel.DispatchShortcut` | TradeCopierPanel.cs | Method | NEW |
| `TradeCopierPanel.BuildDiagRow` | TradeCopierPanel.cs | Method | DELETE |
| `TradeCopierPanel.OnDiagGap001d` | TradeCopierPanel.cs | Method | DELETE |
| `TradeCopierPanel.OnDiagGap002` | TradeCopierPanel.cs | Method | DELETE |
| `TradeCopierPanel._atmTemplateCombo` | TradeCopierPanel.cs | Field | NEW |
| `TradeCopierPanel.BuildAtmTemplateRow` | TradeCopierPanel.cs | Method | NEW |
| `TradeCopierPanel.LoadAtmTemplates` | TradeCopierPanel.cs | Method | NEW |
| `TradeCopierPanel.OnAtmTemplateSelectionChanged` | TradeCopierPanel.cs | Method | NEW |
| `TradeCopierPanel.GetAtmTemplatesDirectory` | TradeCopierPanel.cs | Method | NEW |
| `TradeCopierWindow.BuildRuleRow` | TradeCopierWindow.cs | Method | MODIFIED |
| `TradeCopierWindow.BuildDynamicRuleRow` | TradeCopierWindow.cs | Method | MODIFIED |
| `TradeCopierWindow._armBeBtns` | TradeCopierWindow.cs | Field | NEW |
| `TradeCopierWindow.OnRuleArmBe` | TradeCopierWindow.cs | Method | NEW |
| xUnit tests (3) | CopyEngineTests.cs | Tests | NEW |
| NT8_ADDON_KNOWLEDGE.md | docs/standards/ | Doc | UPDATE |

---

## 4. Method Signatures

### 4.1 TradeCopierAddOn.cs -- New/Modified Methods

#### New field (mirrors _clickHandlers pattern):
```csharp
private static readonly ConcurrentDictionary<Chart, TradeCopierPanel> _keyHandlers
    = new ConcurrentDictionary<Chart, TradeCopierPanel>();
```

#### New: HookKeyShortcut
```csharp
// Wire chart.PreviewKeyDown to panel.OnChartKeyDown after successful DoInject.
// Mirrors HookClickTrader pattern: TryRemove-first to prevent duplicate handlers.
// Called on WPF UI thread (Dispatcher.InvokeAsync from DoInject).
// CYC=2: chart null guard (1) + TryRemove-first pattern (2)
private static void HookKeyShortcut(Chart chart, TradeCopierPanel panel)
```

#### New: UnhookKeyShortcut
```csharp
// Unwire chart.PreviewKeyDown (PRODUCTION handler only) before panel.Detach().
// Called from OnWindowDestroyed. Removes panel.OnChartKeyDown via _keyHandlers lookup.
// Does NOT remove _sim101KeyDiag -- that is RemoveSim101's responsibility.
// CYC=2: TryRemove guard (1) + unhook (2)
private static void UnhookKeyShortcut(Chart chart)
```

#### New: RemoveSim101
```csharp
// Removes the SIM101 logging-only diag handler from chart.PreviewKeyDown.
// Called UNCONDITIONALLY after SIM101 completes (PASS or FAIL).
// Must be called BEFORE HookKeyShortcut() on the PASS path.
// Nulls _sim101KeyDiag to prevent any accidental re-subscription.
// CYC=2: null guard (1) + unhook + null assignment (2)
private static void RemoveSim101(Chart chart)
```

#### Modified: DoInject (addition after _panels[chart] = panel)
```csharp
// Add after:  _panels[chart] = panel;
// Add before: return;
HookKeyShortcut(chart, panel);
```

#### Modified: OnWindowDestroyed (addition before panel.Detach)
```csharp
StopAtrEngine(chart);
UnregisterClickTrader(chart);
UnhookKeyShortcut(chart);    // NEW B11 -- leak guard
TradeCopierPanel panel;
if (_panels.TryRemove(chart, out panel))
    panel.Detach();
```

#### Deletions from TradeCopierAddOn.cs:
- `internal static void RunGap001dTest(NinjaTrader.Cbi.Account acc, NinjaTrader.Cbi.Instrument instr)`
- `private static void RunGap002Test(NinjaTrader.Cbi.Instrument cbiInstr)`
- `private static void OnGap002AccountUpdate(object sender, AccountItemEventArgs e)`
- `private static volatile int _gap002TickCount`
- `private static NinjaTrader.Cbi.Account _gap002Account`

---

### 4.2 TradeCopierPanel.cs -- New Methods

#### New: OnChartKeyDown
```csharp
// chart.PreviewKeyDown handler wired by TradeCopierAddOn.HookKeyShortcut().
// Fires on WPF UI thread -- no Dispatcher needed.
// CYC=3: instrument null guard (1), modifier guard (2), delegate to DispatchShortcut (3).
// Jane Street: guard-early, zero branches in the hot dispatch path.
internal void OnChartKeyDown(object sender, KeyEventArgs e)
```

#### New: DispatchShortcut
```csharp
// Jane Street switch preferred over if/else chain.
// Cases: T=Trim, F=FlattenAll, C=CancelPendingEntries, B=BreakEven.
// Calls EXISTING CopyEngine public methods -- no new CopyEngine code.
// CYC=5: switch entry (1) + 4 case arms (2,3,4,5).
// BE path reads _beBufferBox.Text for buffer ticks (UI-thread-safe here).
private void DispatchShortcut(Key key)
```

#### New: BuildAtmTemplateRow
```csharp
// Appends "ATM:" label + ComboBox row to root StackPanel.
// LoadAtmTemplates() populates ComboBox ItemsSource after construction.
// CYC=1: straight-line widget construction.
private void BuildAtmTemplateRow(StackPanel root)
```

#### New: LoadAtmTemplates
```csharp
// Reads .xml template filenames from NT8 ATM templates directory.
// Returns: populates _atmTemplateCombo.ItemsSource with filename-without-extension list.
// Path: Environment.GetFolderPath(SpecialFolder.MyDocuments) +
//       "\\NinjaTrader 8\\templates\\ATM\\"
// On DirectoryNotFoundException or IO error: sets ItemsSource to empty string[] (no throw).
// CYC=3: directory null guard (1), directory exists guard (2), foreach populate (3).
private void LoadAtmTemplates()
```

#### New: GetAtmTemplatesDirectory
```csharp
// Returns canonical NT8 ATM templates directory path.
// Pure string concatenation -- no IO, no branches.
// CYC=1: straight-line path build.
private static string GetAtmTemplatesDirectory()
```

#### New: OnAtmTemplateSelectionChanged
```csharp
// Stores selected ATM template name in _activeAtmTemplateName field.
// No engine call at selection time -- template applied when orders are submitted.
// CYC=2: null guard (1) + store selection (2).
private void OnAtmTemplateSelectionChanged(object sender, SelectionChangedEventArgs e)
```

#### New field:
```csharp
private ComboBox _atmTemplateCombo = null;
private string   _activeAtmTemplateName = string.Empty;
```

#### Deletions from TradeCopierPanel.cs (DW-B10-01):
- `private void BuildDiagRow(StackPanel root)`
- `private void OnDiagGap001d(object sender, RoutedEventArgs e)`
- `private void OnDiagGap002(object sender, RoutedEventArgs e)`
- The `BuildDiagRow(root);` call inside `BuildUI()` (near end of method)

---

### 4.3 TradeCopierWindow.cs -- New/Modified Methods (DW-B10-03)

#### New field:
```csharp
// Arm BE button tracking -- accessed exclusively on UI thread (JS-021)
private readonly List<Button> _armBeBtns = new List<Button>();
```

#### New: OnRuleArmBe
```csharp
// Arm BE click handler for rule rows in TradeCopierWindow.
// Tag layout: object[] { instrumentNameOrTextBox, leaderComboBox, bufferTextBox }
// Calls engine.ArmPendingBe(instr, leaderAcc, bufferTicks).
// CYC=4: tag null (1), name empty (2), instr null (3), leader null (4).
// JS-021: no lock. JS-002: no return null (uses guard-return pattern).
private void OnRuleArmBe(object sender, RoutedEventArgs e)
```

#### Modified: BuildRuleRow
- Add Col 11 ColumnDefinition (GridLength.Auto)
- Add Arm BE cluster: `[Arm BE]` Button (BrushInactive) + TextBox(width=30, "2") + "tks" label
- Tag = `new object[] { instrumentName, leaderCb, bufferTextBox }`
- Click += `OnRuleArmBe`
- `_armBeBtns.Add(armBeBtn)`
- Cluster goes in Col 11

#### Modified: BuildDynamicRuleRow
- Same addition as BuildRuleRow (tag[0] = instrTextBox, tag[1] = leaderCb, tag[2] = bufferTextBox)

---

### 4.4 xUnit Tests -- New [Fact] Methods (DW-B10-02)

Target file: `CopyEngineTests.cs` (existing test file)

```csharp
// Verifies AtrSizingEngine constructor + ManualOnBarUpdate tolerates a fresh instance
// with no SetParameters call. Must not throw.
[Fact]
public void StartAtrEngine_NullChart_DoesNotThrow()

// Verifies AtrSizingEngine SetParameters + ManualOnBarUpdate tolerates null
// instrument (pointValue falls back to default 5.0). Must not throw.
[Fact]
public void StartAtrEngine_NullInstrument_DoesNotThrow()

// Verifies AtrSizingEngine.AtrUpdated display string contains expected format tokens.
// Expected: "ATR=" prefix, "pts" substring, "stopTicks=" substring.
[Fact]
public void UpdateAtrOverlay_FormatsDisplayString_CorrectText()
```

---

## 5. Data Flow

### 5.1 Keyboard Shortcut Flow (T1)
```
User presses Ctrl+Shift+T (chart window has focus)
  -> WPF PreviewKeyDown tunnels from Chart window root downward
  -> chart.PreviewKeyDown fires (registered in DoInject)
  -> panel.OnChartKeyDown(sender, e)
     -> guard: _instrument == null -> return
     -> guard: !IsCtrlAndShift -> return
     -> DispatchShortcut(e.Key)
        -> switch(Key.T) -> _engine.Trim(_instrument)
           -> CopyEngine iterates AllAccounts, sends halved-qty orders via Account.CreateOrder
           -> NT8 fires OrderUpdate callbacks (async)
           -> CopyEngine.StatusUpdate event fires
        -> panel.OnStatusUpdate -> Dispatcher.InvokeAsync -> _statusText.Text update
```

### 5.2 BreakEven via Keyboard (Ctrl+Shift+B)
```
DispatchShortcut(Key.B)
  -> reads _beBufferBox.Text (UI thread -- safe; PreviewKeyDown is on UI thread)
  -> int.TryParse -> buffer ticks (default 2 on parse fail)
  -> _engine.BreakEven(_instrument, bufferTicks)
     -> CopyEngine.BreakEven -> foreach AllAccounts -> MoveStopToBreakEven
```

### 5.3 ATM Template Writer Flow (T2)
```
Panel BuildUI -> BuildAtmTemplateRow(root)
  -> _atmTemplateCombo = new ComboBox { ... }
  -> LoadAtmTemplates() called during BuildUI or OnLoaded

LoadAtmTemplates():
  -> GetAtmTemplatesDirectory() -> "...\NinjaTrader 8\templates\ATM\"
  -> Directory.Exists check
  -> Directory.GetFiles(dir, "*.xml")
  -> foreach: Path.GetFileNameWithoutExtension(f) -> names[]
  -> _atmTemplateCombo.ItemsSource = names

OnAtmTemplateSelectionChanged:
  -> _activeAtmTemplateName = selected name
  (no engine call at selection -- applies at order submission time in future block)
```

### 5.4 Window Arm BE Flow (DW-B10-03)
```
TradeCopierWindow rule row -> Arm BE button click
  -> OnRuleArmBe(sender, e)
     -> tag[] extract: instr name, leaderCb, bufferTextBox
     -> FindInstrument(name) -> Instrument
     -> leaderCb.SelectedItem as Account -> leaderAcc
     -> int.TryParse(bufferTextBox.Text) -> bufferTicks
     -> _engine.ArmPendingBe(instr, leaderAcc, bufferTicks)
        (CopyEngine.ArmPendingBe is already implemented from B10-T2)
```

---

## 6. Threading Model

| Location | Thread | Mechanism |
|----------|--------|-----------|
| `chart.PreviewKeyDown` fires | WPF UI thread | Inherent (WPF RoutedEvent dispatch) |
| `OnChartKeyDown` handler | WPF UI thread | No Dispatcher needed |
| `DispatchShortcut` | WPF UI thread | No Dispatcher needed |
| Engine calls (Trim, FlattenAll, Cancel, BreakEven) | WPF UI thread | Same as existing button handlers |
| `LoadAtmTemplates` (File.IO) | WPF UI thread | Called from BuildUI/OnLoaded (UI thread) |
| `OnAtmTemplateSelectionChanged` | WPF UI thread | WPF event |
| `HookKeyShortcut` / `UnhookKeyShortcut` | WPF UI thread | Called from `Dispatcher.InvokeAsync` path in DoInject/OnWindowDestroyed |
| `OnRuleArmBe` | WPF UI thread | WPF button event |
| xUnit tests | Test runner thread | No threading constraints |

**No lock() anywhere.** All new code runs on the WPF UI thread.
Engine uses ConcurrentDictionary and ConcurrentBag (existing -- not modified in B11).

---

## 7. NT8 API Usage

| API | Used By | Notes |
|-----|---------|-------|
| `chart.PreviewKeyDown` | HookKeyShortcut | Window : UIElement -- standard WPF tunneling event. Verified available via WPF inheritance. |
| `Keyboard.Modifiers` | OnChartKeyDown | System.Windows.Input.Keyboard -- already in using list (TradeCopierPanel.cs). |
| `KeyEventArgs.Key` | OnChartKeyDown | System.Windows.Input.KeyEventArgs -- standard WPF. |
| `Key.T`, `Key.F`, `Key.C`, `Key.B` | DispatchShortcut | System.Windows.Input.Key enum -- standard WPF. |
| `Environment.SpecialFolder.MyDocuments` | GetAtmTemplatesDirectory | System -- available. |
| `Directory.GetFiles(path, "*.xml")` | LoadAtmTemplates | System.IO -- needs `using System.IO;` added to TradeCopierPanel.cs. |
| `Path.GetFileNameWithoutExtension` | LoadAtmTemplates | System.IO -- same using. |
| `CopyEngine.ArmPendingBe` | OnRuleArmBe | Existing public method (B10-T2) -- no new engine code needed. |
| `CopyEngine.Trim/FlattenAll/CancelPendingEntries/BreakEven` | DispatchShortcut | All existing public methods -- no new engine code. Spec line 4750 names `FlattenAll(rule)` for Ctrl+Shift+F; plan uses this name throughout. |

**NT8 host constraint**: `KeyBinding`/`InputBinding` for Shift+letter combos is REJECTED
by the NT8 WPF host (documented in TradeCopierWindow.cs line 8 comment). Raw
`chart.PreviewKeyDown` bypasses this restriction -- confirmed design choice.
SIM101 validates this assumption before production code is written.

---

## 8. Jane Street Constraint Compliance

| Rule | Applies To | Status |
|------|-----------|--------|
| JS-021 (no lock) | All new code | CLEAN -- WPF UI thread only; ConcurrentDictionary for _keyHandlers |
| JS-001 (no throw in hot path) | OnChartKeyDown, DispatchShortcut | CLEAN -- no throw in handler |
| JS-002 (no return null) | LoadAtmTemplates, GetAtmTemplatesDirectory | CLEAN -- returns empty array on fail |
| JS-023 (volatile) | No new volatile fields needed | CLEAN -- _instrument is UI-thread-only |
| JS-033 (no async void except FlashBeFired) | All new handlers | CLEAN -- no async void |
| NT8-003 (no volatile double) | No doubles in B11 | N/A |
| Math.Clamp ban | BreakEven buffer parse | Use existing pattern: Math.Max(0, parsed) or no clamp |
| ASCII-only | All string literals | CLEAN -- "ATM:", "Arm BE", "tks" are all ASCII |
| No FontFamily | No UI font overrides | CLEAN |
| No hardcoded hex colors | No new color creation in B11 | CLEAN -- reuses existing frozen brushes |
| No abstract record / ImmutableDictionary / {get;init;} | No DU or record types in B11 | CLEAN |
| DateTime.UtcNow | Not used in B11 | N/A |
| CYC <= 8 per method | All new methods | See §9 CYC table |

---

## 9. CYC Verification Table

| Method | File | Decision Points | CYC |
|--------|------|----------------|-----|
| `OnChartKeyDown` | TradeCopierPanel.cs | 2 (null guard + modifier guard) | 3 |
| `DispatchShortcut` | TradeCopierPanel.cs | 4 (switch: T, F, C, B) | 5 |
| `HookKeyShortcut` | TradeCopierAddOn.cs | 1 (null guard) | 2 |
| `UnhookKeyShortcut` | TradeCopierAddOn.cs | 1 (TryRemove guard) | 2 |
| `RemoveSim101` | TradeCopierAddOn.cs | 1 (null guard on field) | 2 |
| `BuildAtmTemplateRow` | TradeCopierPanel.cs | 0 (straight-line) | 1 |
| `LoadAtmTemplates` | TradeCopierPanel.cs | 2 (exists guard + foreach) | 3 |
| `GetAtmTemplatesDirectory` | TradeCopierPanel.cs | 0 (straight-line) | 1 |
| `OnAtmTemplateSelectionChanged` | TradeCopierPanel.cs | 1 (null guard) | 2 |
| `OnRuleArmBe` | TradeCopierWindow.cs | 3 (tag null + name empty + instr null + leader null) | 4 |

All values <= 8. ✅

---

## 10. Backlog Disposition

| ID | Description | Priority | B11 Disposition | Ticket |
|----|-------------|----------|-----------------|--------|
| DW-B10-01 | Remove BuildDiagRow/OnDiagGap001d/OnDiagGap002 | P2 | CLOSED -- T1 removes all 3 methods from TradeCopierPanel.cs and the 2 RunGap diag methods + related fields from TradeCopierAddOn.cs | T1 |
| DW-B10-02 | Add 3 missing AtrSizingEngine xUnit tests | P1 | CLOSED -- T2 adds all 3 [Fact] methods to CopyEngineTests.cs | T2 |
| DW-B10-03 | TradeCopierWindow.cs Arm BE column | P2 | CLOSED -- T2 adds Arm BE cluster to BuildRuleRow + BuildDynamicRuleRow; adds OnRuleArmBe handler | T2 |
| DW-B10-04 | Update NT8_ADDON_KNOWLEDGE.md with T4 result | P1 | CLOSED -- T1 updates docs/standards/NT8_ADDON_KNOWLEDGE.md, records DispatcherTimer fallback as confirmed compile-safe path, marks DW-B9-02 RESOLVED | T1 |
| DW-B9-01 | ATR box on chart canvas | P2 | SHELVED (no change) -- carry to B12 | -- |
| DW-B9-03 | Click trader Bid+1/Ask-1 | P3 | SHELVED (no change) -- carry to B12 | -- |
| DW-B11-HK-01 | PreviewKeyDown shortcut layer | -- | PRIMARY T1 -- conditional on SIM101 PASS | T1 |
| DW-B11-HK-02 | Focus-independence + ATM template writer | -- | PRIMARY T2 -- conditional on T1 SIM101 PASS | T2 |

---

## 11. SIM101 Feasibility Gate -- Conditional Block

```
SIM101 DW-B11-HK-01 OUTCOME:
  PASS  -> Implement T1 and T2 as designed in this plan.
  FAIL  -> Mark both DW-B11-HK-01 and DW-B11-HK-02 as VERIFIED_NOT_FEASIBLE.
           Record failure in 06-deferred-backlog.md as:
             DW-B12-01: PreviewKeyDown VERIFIED_NOT_FEASIBLE in NT8 host.
                        Root cause: NT8 WPF host intercepts keyboard events at container level
                        before PreviewKeyDown reaches the Chart window. Alternative:
                        investigate NinjaTrader.Gui.Chart.ChartControl.KeyDown or
                        NinjaTrader.NinjaScript HotKey API if available.
           B11 PIPELINE_COMPLETE with T1(DEFERRED) + T2(DEFERRED).
           DW-B10-01 through DW-B10-04 still close in T1/T2 (not keyboard-dependent).
```

**Important**: DW-B10-01, DW-B10-02, DW-B10-03, DW-B10-04 are INDEPENDENT of SIM101
outcome. They must be completed regardless of whether keyboard shortcuts are feasible.
Only the PreviewKeyDown shortcut wiring itself is conditional.

---

## 12. File-Level Change Summary

### T1: TradeCopierAddOn.cs
```
ADD:    _keyHandlers ConcurrentDictionary<Chart, TradeCopierPanel>  (line ~50, after _clickHandlers)
ADD:    _sim101KeyDiag KeyEventHandler field                          (line ~52, SIM101 only; nulled by RemoveSim101)
ADD:    HookKeyShortcut(Chart, TradeCopierPanel)                     (near UnhookKeyShortcut)
ADD:    UnhookKeyShortcut(Chart)                                      (after HookKeyShortcut -- PRODUCTION handler only)
ADD:    RemoveSim101(Chart)                                           (after UnhookKeyShortcut -- SIM101 diag unhook)
MOD:    DoInject() -- add HookKeyShortcut call after _panels[chart] = panel
MOD:    OnWindowDestroyed() -- add UnhookKeyShortcut(chart) call
DELETE: RunGap001dTest, RunGap002Test, OnGap002AccountUpdate, _gap002TickCount, _gap002Account
        (approx. lines 460-620: the entire "-- DIAG" section)
```

### T1: TradeCopierPanel.cs
```
ADD:    using System.Windows.Input; (already present -- no action needed)
ADD:    OnChartKeyDown(object, KeyEventArgs)         (internal -- near Detach())
ADD:    DispatchShortcut(Key)                         (private -- after OnChartKeyDown)
DELETE: BuildDiagRow(StackPanel)                      (approx. lines 976-1017)
DELETE: OnDiagGap001d(object, RoutedEventArgs)        (approx. lines 1020-1041)
DELETE: OnDiagGap002(object, RoutedEventArgs)         (approx. lines 1044-1055)
MOD:    BuildUI() -- remove BuildDiagRow(root) call   (approx. line 404)
```

### T1: docs/standards/NT8_ADDON_KNOWLEDGE.md
```
UPDATE: Section "NT8 Chart Attachment API for Indicator -- UNRESOLVED" (lines 362-372):
  - Change section header to "NT8 Chart Attachment API -- RESOLVED 2026-07-09"
  - Add: "Confirmed result: NinjaScripts.Add and Indicators.Add produce CS1061 in AddOn context."
  - Add: "DispatcherTimer polling at DispatcherPriority.Background is the compile-safe fallback."
  - Add: "DW-B9-02 STATUS: RESOLVED 2026-07-09 (B10-EXEC T4)."
```

### T2: TradeCopierPanel.cs
```
ADD:    using System.IO;                              (new using if not present)
ADD:    _atmTemplateCombo ComboBox field              (near other private fields)
ADD:    _activeAtmTemplateName string field           (near _atmTemplateCombo)
ADD:    BuildAtmTemplateRow(StackPanel)               (private, CYC=1)
ADD:    LoadAtmTemplates()                            (private, CYC=3)
ADD:    GetAtmTemplatesDirectory()                    (private static, CYC=1)
ADD:    OnAtmTemplateSelectionChanged(object, SelectionChangedEventArgs) (private, CYC=2)
MOD:    BuildUI() -- call BuildAtmTemplateRow(root) at end (after existing rows)
MOD:    OnLoaded() -- call LoadAtmTemplates() at end (after follower items populated)
```

### T2: TradeCopierWindow.cs
```
ADD:    _armBeBtns List<Button> field                 (near other _xxxBtns fields)
ADD:    OnRuleArmBe(object, RoutedEventArgs)           (private, CYC=4)
MOD:    BuildRuleRow(string) -- add Col 11 Arm BE cluster
MOD:    BuildDynamicRuleRow() -- add Col 11 Arm BE cluster
```

### T2: CopyEngineTests.cs
```
ADD:    StartAtrEngine_NullChart_DoesNotThrow [Fact]
ADD:    StartAtrEngine_NullInstrument_DoesNotThrow [Fact]
ADD:    UpdateAtrOverlay_FormatsDisplayString_CorrectText [Fact]
```

---

## 13. Spec Requirements Satisfied

| Req | Ticket | Description |
|-----|--------|-------------|
| DW-B11-HK-01 | T1 | PreviewKeyDown layer with 4 shortcuts |
| DW-B11-HK-02 | T2 | Focus-independence verification + ATM template writer |
| DW-B10-01 | T1 | Diag scaffolding removal |
| DW-B10-02 | T2 | 3 AtrSizingEngine xUnit tests |
| DW-B10-03 | T2 | Window Arm BE column |
| DW-B10-04 | T1 | NT8_ADDON_KNOWLEDGE.md update |
| SIM101-gate | T1 | Logging-only handler first; full impl only on PASS |

---

## 14. Deferred Items for B12

| ID | Description | Priority | Reason Deferred |
|----|-------------|----------|-----------------|
| DW-B12-01 | PreviewKeyDown keyboard shortcuts (conditional) | TBD | Only deferred if SIM101 FAILS. If SIM101 PASSES in B11, this row is not created. |
| DW-B12-BUFFERED-BUTTONS-01 | Convert `Flatten(Instrument)` to `Flatten(Instrument, int exitBuffer)` emitting `OrderType.Limit@bid+buffer`; convert `Trim(Instrument)` to `Trim(Instrument, int exitBuffer)` emitting `OrderType.Limit@ask-buffer`. | P1 | B11 Key.F/Key.T shortcuts fire at market (current engine API). Buffered-limit exits are spec-required but depend on new CopyEngine method signatures -- deferred to B12. |
| DW-B9-01 | ATR box visualization on chart canvas | P2 | Carry from B10/B9 -- shelved |
| DW-B9-03 | Click trader Bid+1/Ask-1 auto-offset | P3 | Carry from B10/B9 -- shelved |

---

## 15. Pre-flight Summary

| Check | Result |
|-------|--------|
| SIM101 protocol described | PASS |
| All new methods CYC <= 8 | PASS |
| No lock() anywhere | PASS |
| No async void (except existing FlashBeFired) | PASS |
| No return null | PASS |
| ASCII-only string literals | PASS |
| No FontFamily overrides | PASS |
| No hardcoded hex colors | PASS |
| No new CopyEngine code for shortcuts | PASS |
| No volatile double / volatile bool | PASS |
| No Math.Clamp | PASS |
| No abstract record / ImmutableDictionary | PASS |
| PreviewKeyDown wired after panel attach | PASS |
| PreviewKeyDown unhooked in OnWindowDestroyed | PASS |
| DW-B10-01 through DW-B10-04 all assigned to tickets | PASS |
| File split validation (3 source files + 1 test file + 1 doc) | PASS |
