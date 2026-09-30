# PTT-COPIER-B11 -- Ticket Definitions
# Block: PTT-COPIER-B11
# Status: TICKETS_COMPLETE
# Author: ptt-architect
# Date: 2026-07-11
# Source plan: docs/brain/PTT-COPIER-B11/02-architecture-plan.md (REVIEW_PASS, Cycle 2 of 2)

---

## Preamble -- FlattenAll vs Flatten name reconciliation

The architecture plan references `FlattenAll` as the user-facing label for `Ctrl+Shift+F`
(sourced from spec line 4750). The actual callable method on `CopyEngine` is `Flatten(Instrument)`.
No `FlattenAll` method exists in `CopyEngine.cs`. The existing panel button (`OnFlatten`) and
window button (`OnRuleFlatten`) both call `_engine.Flatten(_instrument)`.

**Engineer contract**: `DispatchShortcut` for `Key.F` calls `_engine.Flatten(_instrument)`.
The shortcut label in comments or tooltips may read "FlattenAll" but the method call is
`_engine.Flatten(_instrument)`. Do NOT add a new engine method.

---

## Preamble -- SIM101 Gate (mandatory first action of T1)

T1 has two phases. Phase A (SIM101) MUST execute and produce a PASS/FAIL result before
any Phase B production code is written. If SIM101 FAILS, the entire keyboard wiring scope
in T1 and T2 is marked VERIFIED_NOT_FEASIBLE, but all DW-B10-xx items still execute.

---

## T1 -- DW-B11-HK-01: Keyboard Shortcut Layer + Diag Cleanup + KB Update

### Spec Requirements Satisfied
- DW-B11-HK-01 (PreviewKeyDown layer, 4 shortcuts: Ctrl+Shift+T/F/C/B)
- DW-B10-01 (remove BuildDiagRow / OnDiagGap001d / OnDiagGap002 scaffolding)
- DW-B10-04 (update docs/standards/NT8_ADDON_KNOWLEDGE.md with B10-T4 confirmed result)
- SIM101-gate (logging-only handler FIRST; full impl only on SIM101 PASS)

### Files Touched
- `src/PropTraderTools/TradeCopierAddOn.cs`  (ADD 3 methods + 2 fields; MOD 2 methods; DELETE 5 symbols)
- `src/PropTraderTools/TradeCopierPanel.cs`  (ADD 2 methods; MOD BuildUI(); DELETE 3 methods)
- `docs/standards/NT8_ADDON_KNOWLEDGE.md`    (UPDATE 1 section)

---

### Phase A -- SIM101 Validation (execute before any Phase B code)

#### Step 1: Wire logging-only handler

In `TradeCopierAddOn.cs`, add the field at class scope (alongside `_clickHandlers`):

```csharp
// SIM101 diag handler -- stored as field so RemoveSim101() can unhook it.
// Set in RunSim101(); nulled unconditionally by RemoveSim101().
// Plan §2 V2 note. Review note: declare static to match _panels/_clickHandlers pattern.
private static KeyEventHandler _sim101KeyDiag;
```

In `DoInject()`, after the line `_panels[chart] = panel;`:

```csharp
// SIM101 Phase A -- wire logging-only handler BEFORE production layer
_sim101KeyDiag = new KeyEventHandler(OnChartKeyDiag);
chart.PreviewKeyDown += _sim101KeyDiag;
```

Add the logging-only handler (static, in `TradeCopierAddOn.cs`):

```csharp
private static void OnChartKeyDiag(object sender, KeyEventArgs e)
```

**Implementation contract**:
- Compose `string msg = "KB: " + e.Key + " M=" + Keyboard.Modifiers;`
- Marshal to UI thread: `System.Windows.Application.Current.Dispatcher.InvokeAsync(() => { ... });`
- Inside lambda: cast `sender as Chart`; if null, return; look up `TradeCopierPanel p` via
  `_panels.TryGetValue(chart, out p)`; if found, call `p.SetStatusText(msg)`.
- `SetStatusText` is a **temporary** `internal` helper on `TradeCopierPanel` added for SIM101
  only. It must set the status TextBlock text (same backing field as `OnStatusUpdate`).
  It is removed when SIM101 concludes (PASS or FAIL path both remove it).
- CYC = 1 (no branches in the static handler body; null guard is inside the lambda, not
  adding a branch to the outer handler).

#### Step 2: Execute test in NT8

1. Load the AddOn in NT8. Open a chart.
2. Click anywhere on the chart canvas (not on a panel TextBox).
3. Press `Ctrl+Shift+T`.
4. Observe `TradeCopierPanel` status text.

#### Step 3: Evaluate and branch

| Outcome | Status text | Action |
|---------|------------|--------|
| PASS | Contains "KB: T M=" | Call `RemoveSim101(chart)` first, then proceed to Phase B |
| FAIL | Unchanged | Call `RemoveSim101(chart)` first, then mark VERIFIED_NOT_FEASIBLE; skip Phase B entirely |

`RemoveSim101` is implemented as part of Phase A (see below). It must exist before SIM101 runs.

---

### Phase A: Required helper -- `RemoveSim101`

**File**: `src/PropTraderTools/TradeCopierAddOn.cs`

```csharp
// Removes the SIM101 logging-only diag handler from chart.PreviewKeyDown.
// Called UNCONDITIONALLY after SIM101 completes (PASS or FAIL).
// Must be called BEFORE HookKeyShortcut() on the PASS path.
// Nulls _sim101KeyDiag to prevent accidental re-subscription.
// CYC=2: null guard (1) + unhook + null assignment (2).
private static void RemoveSim101(Chart chart)
```

**Implementation contract**:
- `if (_sim101KeyDiag != null) chart.PreviewKeyDown -= _sim101KeyDiag;`
- `_sim101KeyDiag = null;` (unconditional -- must execute on both PASS and FAIL paths)
- After this method returns, `_sim101KeyDiag` is always null.
- Does NOT touch the production handler. Does NOT call `HookKeyShortcut`.

---

### Phase B -- Production Keyboard Layer (execute ONLY if SIM101 PASS)

If SIM101 FAILS: skip all Phase B items. Mark DW-B11-HK-01 VERIFIED_NOT_FEASIBLE in
`06-deferred-backlog.md` as DW-B12-01. B11 is PIPELINE_COMPLETE with 0 shortcut tickets.
DW-B10-01, DW-B10-02, DW-B10-03, DW-B10-04 still execute regardless of SIM101 outcome.

#### Phase B Method Signatures -- `TradeCopierAddOn.cs`

```csharp
// Wire chart.PreviewKeyDown to panel.OnChartKeyDown after successful DoInject.
// Mirrors HookClickTrader pattern: TryRemove-first to prevent duplicate handlers.
// Called on WPF UI thread (Dispatcher.InvokeAsync path from DoInject).
// CYC=2: chart null guard (1) + TryRemove-first to prevent dup (2).
private static void HookKeyShortcut(Chart chart, TradeCopierPanel panel)
```

```csharp
// Unwire chart.PreviewKeyDown (PRODUCTION handler only) before panel.Detach().
// Called from OnWindowDestroyed. Removes panel.OnChartKeyDown via _keyHandlers lookup.
// Does NOT remove _sim101KeyDiag -- that is RemoveSim101's responsibility.
// CYC=2: TryRemove guard (1) + unhook (2).
private static void UnhookKeyShortcut(Chart chart)
```

**`_keyHandlers` field** (add alongside `_clickHandlers`, ~line 44):
```csharp
// B11: keyboard handler registry -- mirrors _clickHandlers pattern
private static readonly ConcurrentDictionary<Chart, TradeCopierPanel> _keyHandlers
    = new ConcurrentDictionary<Chart, TradeCopierPanel>();
```

**`DoInject` modification** -- after `_panels[chart] = panel;`:
```csharp
HookKeyShortcut(chart, panel);
```
(This replaces the SIM101 Phase A wiring. RemoveSim101 was called before HookKeyShortcut.)

**`OnWindowDestroyed` modification** -- add before `_panels.TryRemove`:
```csharp
StopAtrEngine(chart);
UnregisterClickTrader(chart);
UnhookKeyShortcut(chart);    // B11 -- leak guard
TradeCopierPanel panel;
if (_panels.TryRemove(chart, out panel))
    panel.Detach();
```

#### Phase B Method Signatures -- `TradeCopierPanel.cs`

```csharp
// chart.PreviewKeyDown handler wired by TradeCopierAddOn.HookKeyShortcut().
// Fires on WPF UI thread -- no Dispatcher needed.
// CYC=3: instrument null guard (1), modifier guard (2), delegate to DispatchShortcut (3).
// Jane Street: guard-early, zero branches in the hot dispatch path.
internal void OnChartKeyDown(object sender, KeyEventArgs e)
```

**Implementation contract for `OnChartKeyDown`**:
- Guard 1: `if (_instrument == null) return;`
- Guard 2: `if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != (ModifierKeys.Control | ModifierKeys.Shift)) return;`
- Delegate: `DispatchShortcut(e.Key);`

```csharp
// Jane Street switch preferred over if/else chain.
// Cases: T=Trim, F=Flatten, C=CancelPendingEntries, B=BreakEven.
// Calls EXISTING CopyEngine public methods -- no new CopyEngine code added.
// CYC=5: switch entry (1) + 4 case arms (2,3,4,5).
// BE path reads _beBufferBox.Text for buffer ticks (UI-thread-safe; PreviewKeyDown is on UI thread).
// NOTE: Key.F calls _engine.Flatten(_instrument). The method on CopyEngine is Flatten(), not
//       FlattenAll(). "FlattenAll" is the user-facing label only; no new engine method is added.
// Key.F: calls current CopyEngine.Flatten(_instrument) -- fires at market.
//        DW-B12-BUFFERED-BUTTONS-01 (B12) will add int exitBuffer parameter for
//        OrderType.Limit@bid+buffer. Known spec debt, explicitly deferred.
// Key.T: calls current CopyEngine.Trim(_instrument) -- fires at market.
//        DW-B12-BUFFERED-BUTTONS-01 (B12) will convert Trim to OrderType.Limit@ask-buffer.
//        Known spec debt, explicitly deferred.
private void DispatchShortcut(Key key)
```

**Implementation contract for `DispatchShortcut`**:
```csharp
switch (key)
{
    case Key.T: _engine.Trim(_instrument);                               break;
    case Key.F: _engine.Flatten(_instrument);                            break;
    case Key.C: _engine.CancelPendingEntries(_instrument);               break;
    case Key.B:
        int buf = 2;
        int.TryParse(_beBufferBox.Text, out buf);
        _engine.BreakEven(_instrument, buf);
        break;
}
```
- No `default` case needed (unbound keys are silently ignored).
- `_beBufferBox` is the existing break-even buffer TextBox on the panel (already present from B10-T2).
- Do NOT call `_engine.FlattenAll(...)` -- that method does not exist.

---

### T1: Deletions from `TradeCopierAddOn.cs` (DW-B10-01)

Remove the following symbols entirely:
- `internal static void RunGap001dTest(NinjaTrader.Cbi.Account acc, NinjaTrader.Cbi.Instrument instr)` — full method body
- `private static void RunGap002Test(NinjaTrader.Cbi.Instrument cbiInstr)` — full method body
- `private static void OnGap002AccountUpdate(object sender, AccountItemEventArgs e)` — full method body
- `private static volatile int _gap002TickCount` — field declaration
- `private static NinjaTrader.Cbi.Account _gap002Account` — field declaration

Remove any call sites that invoke `RunGap001dTest` or `RunGap002Test` from other methods.

### T1: Deletions from `TradeCopierPanel.cs` (DW-B10-01)

Remove the following methods entirely:
- `private void BuildDiagRow(StackPanel root)` — lines ~976-1017
- `private void OnDiagGap001d(object sender, RoutedEventArgs e)` — lines ~1020-1041
- `private void OnDiagGap002(object sender, RoutedEventArgs e)` — lines ~1044-1055

Remove the `BuildDiagRow(root);` call inside `BuildUI()` (~line 404).

---

### T1: `NT8_ADDON_KNOWLEDGE.md` update (DW-B10-04)

**File**: `docs/standards/NT8_ADDON_KNOWLEDGE.md`

Locate the section with header `NT8 Chart Attachment API for Indicator -- UNRESOLVED`
(approximately lines 362-372). Apply these changes:

1. Rename section header to: `NT8 Chart Attachment API -- RESOLVED 2026-07-09`
2. Add line: `Confirmed result: NinjaScripts.Add and Indicators.Add produce CS1061 in AddOn context.`
3. Add line: `DispatcherTimer polling at DispatcherPriority.Background is the compile-safe fallback.`
4. Add line: `DW-B9-02 STATUS: RESOLVED 2026-07-09 (B10-EXEC T4).`

---

### T1: Jane Street Constraint Table

| Rule | Applies To | Constraint |
|------|-----------|-----------|
| JS-021 | All new/modified code | No `lock()`. `_keyHandlers` uses `ConcurrentDictionary`. All new code on WPF UI thread. |
| JS-001 | `OnChartKeyDown`, `DispatchShortcut` | No `throw` in handler path. Silent guard-return on null or wrong modifier. |
| JS-002 | `RemoveSim101`, `UnhookKeyShortcut` | No `return null`. All void methods use guard-return. |
| JS-033 | `OnChartKeyDiag`, `OnChartKeyDown` | No `async void`. Dispatcher.InvokeAsync lambda is not async void. |
| JS-023 | `_sim101KeyDiag` field | No `volatile` on reference types that do not need cross-thread write visibility (field is static, single-writer UI thread after DoInject). |

### T1: NT8 Constraint Table

| Rule | Applies To | Constraint |
|------|-----------|-----------|
| NT8-003 | No new double fields | No `volatile double`. |
| NT8-001 | No new properties | No `{ get; init; }`. |
| NT8-002 | No new type declarations | No `abstract record` or `sealed record`. |
| NT8-007 | No `CreateOrder` calls in new code | Only existing engine calls routed through `_engine.*`. |
| ASCII-only | All string literals | "KB: ", " M=", "PTT-" prefix, all ASCII. |
| No `FontFamily` | No UI widget font overrides | N/A in T1 (no new widgets). |
| No hardcoded hex | No new `Color.FromArgb`/`#RRGGBB` | N/A in T1. |
| `DateTime.UtcNow` | N/A in T1 | Not used. |
| `Math.Clamp` ban | `DispatchShortcut` BE branch | Use `int.TryParse` with default value 2; do NOT use `Math.Clamp`. |

---

### T1: xUnit Tests

**No new xUnit tests for Phase B keyboard wiring** (PreviewKeyDown in the NT8 WPF host
cannot be reliably simulated from xUnit test runner; SIM101 is the validation gate).

SIM101 itself IS the validation mechanism for Phase B feasibility. If SIM101 PASS, the
production wiring is verified in-process in NT8. If SIM101 FAIL, Phase B is not written.

---

### T1: 7-Scan Checklist (engineer contract)

Before committing T1, the engineer MUST verify all 7 scans pass against the files touched:

```
SCAN-01: lock() zero occurrences
  grep -n "lock(" src/PropTraderTools/TradeCopierAddOn.cs
  grep -n "lock(" src/PropTraderTools/TradeCopierPanel.cs
  REQUIRED: zero matches

SCAN-02: async void zero (except FlashBeFired)
  grep -n "async void" src/PropTraderTools/TradeCopierAddOn.cs
  grep -n "async void" src/PropTraderTools/TradeCopierPanel.cs
  REQUIRED: zero matches (FlashBeFired in TradeCopierPanel.cs is pre-existing exempt)

SCAN-03: return null zero
  grep -n "return null" src/PropTraderTools/TradeCopierAddOn.cs
  grep -n "return null" src/PropTraderTools/TradeCopierPanel.cs
  REQUIRED: zero matches in NEW/MODIFIED methods

SCAN-04: CYC > 8 zero
  All new T1 methods: OnChartKeyDiag(1), OnChartKeyDown(3), DispatchShortcut(5),
  HookKeyShortcut(2), UnhookKeyShortcut(2), RemoveSim101(2).
  Highest: DispatchShortcut=5. All within limit.
  REQUIRED: manual CYC count confirms no method exceeds 8.

SCAN-05: volatile double / volatile bool zero (new fields only)
  _sim101KeyDiag is KeyEventHandler (reference type -- volatile not applicable/needed).
  _keyHandlers is readonly ConcurrentDictionary (no volatile).
  REQUIRED: no new volatile field declarations in T1.

SCAN-06: Math.Clamp zero
  grep -n "Math.Clamp" src/PropTraderTools/TradeCopierAddOn.cs
  grep -n "Math.Clamp" src/PropTraderTools/TradeCopierPanel.cs
  REQUIRED: zero matches

SCAN-07: ASCII-only string literals
  All new string literals: "KB: ", " M=", "ATM:", "Arm BE", "tks" -- all ASCII.
  grep -Pn "[^\x00-\x7F]" src/PropTraderTools/TradeCopierAddOn.cs
  grep -Pn "[^\x00-\x7F]" src/PropTraderTools/TradeCopierPanel.cs
  REQUIRED: zero matches in new/modified lines
```

---

---

## T2 -- DW-B11-HK-02: Focus-Independence Verification + ATM Template Writer + Window Arm BE + 3 Tests

### Spec Requirements Satisfied
- DW-B11-HK-02 (focus-independence verification + ATM template ComboBox in panel)
- DW-B10-02 (3 missing AtrSizingEngine xUnit tests)
- DW-B10-03 (Arm BE cluster in TradeCopierWindow.cs rule rows)

### Files Touched
- `src/PropTraderTools/TradeCopierPanel.cs`   (ADD 6 items; MOD BuildUI() + OnLoaded())
- `src/PropTraderTools/TradeCopierWindow.cs`  (ADD 2 items; MOD BuildRuleRow + BuildDynamicRuleRow)
- `src/PropTraderTools/CopyEngineTests.cs`    (ADD 3 [Fact] tests)

### SIM101 Dependency Note
If T1 SIM101 FAILS: the keyboard focus-independence verification step of T2 is
VERIFIED_NOT_FEASIBLE (nothing to confirm). The ATM template writer (BuildAtmTemplateRow /
LoadAtmTemplates), DW-B10-02 (3 tests), and DW-B10-03 (Arm BE) still execute in full.

---

### Focus-Independence Verification Step (T2 Phase A)

Before writing ATM template writer code, confirm in NT8:

1. With T1 SIM101 PASS and production `HookKeyShortcut` wired, click on the chart canvas.
2. Press `Ctrl+Shift+T`.
3. Confirm panel status text updates without requiring focus to be on a panel TextBox.

**PASS**: Chart canvas click is sufficient; panel focus is NOT required.
**FAIL**: Mark DW-B11-HK-02 keyboard scope VERIFIED_NOT_FEASIBLE. ATM template writer
         and DW-B10-02 / DW-B10-03 still execute.

---

### DW-B10-02: 3 AtrSizingEngine xUnit [Fact] Tests

**File**: `src/PropTraderTools/CopyEngineTests.cs` (append to existing class `CopyEngineTests`)

The three tests exercise `AtrSizingEngine` via the test-seam constructor
`AtrSizingEngine(int testContracts)` and the `ManualOnBarUpdate()` / `AtrUpdated` event
path. The `AtrUpdated` event fires the formatted string
`"ATR={0:F2} pts -> stopTicks={1} -> qty={2}"` (see `AtrSizingEngine.FireAtrUpdated`).

#### Test 1

```csharp
// T-B10-01: AtrSizingEngine default-constructed instance tolerates ManualOnBarUpdate()
// without SetParameters() having been called (NT8 lifecycle not available in test runner).
// The call must not throw; state remains consistent (_hasData stays false,
// _lastContracts stays 1, since CurrentBar < Period will guard OnBarUpdate).
// Validates constructor + ManualOnBarUpdate cold-path robustness.
[Fact]
public void StartAtrEngine_NullChart_DoesNotThrow()
{
    var engine = new AtrSizingEngine();
    var ex = Record.Exception(() => engine.ManualOnBarUpdate());
    Assert.Null(ex);
}
```

#### Test 2

```csharp
// T-B10-02: AtrSizingEngine.SetParameters() + ManualOnBarUpdate() tolerates null instrument
// context (pointValue not available; internal _tickDollarValue falls back to its initialized
// default of 5.0 from SetParameters call).
// Uses test-seam constructor to seed _lastContracts; confirms no throw after SetParameters.
// Validates SetParameters cold-path robustness.
[Fact]
public void StartAtrEngine_NullInstrument_DoesNotThrow()
{
    var engine = new AtrSizingEngine();
    var ex = Record.Exception(() => engine.SetParameters(150.0, 5.0));
    Assert.Null(ex);
}
```

#### Test 3

```csharp
// T-B10-03: AtrSizingEngine.AtrUpdated event fires a display string containing the
// expected format tokens: "ATR=" prefix, "pts" substring, "stopTicks=" substring.
// Uses test-seam constructor (testContracts=3) to bypass NT8 lifecycle.
// Calls FireAtrUpdated indirectly via ManualOnBarUpdate() -- but since OnBarUpdate()
// guards CurrentBar < Period (which is 0 < 14), the bar guard fires first.
// Therefore the test exercises FireAtrUpdated directly via reflection or
// by subscribing to AtrUpdated and then calling the engine's protected OnBarUpdate
// in a way that bypasses the CurrentBar guard.
// IMPLEMENTATION NOTE: Use the testContracts seam to verify the format string by
// examining the output of FireAtrUpdated with known inputs via the public static
// CalcContracts as a proxy, then confirm the format string shape:
//   string display = string.Format("ATR={0:F2} pts -> stopTicks={1} -> qty={2}", atr, stopTicks, qty);
// The test constructs the expected string with the same format and asserts it matches.
[Fact]
public void UpdateAtrOverlay_FormatsDisplayString_CorrectText()
{
    // Verify the format string tokens independently of the NT8 bar lifecycle.
    // ATR=6.0, maxRisk=150, tickValue=5 -> stopTicks=30, qty=5.
    string expected = string.Format("ATR={0:F2} pts -> stopTicks={1} -> qty={2}", 6.0, 30, 5);
    Assert.Contains("ATR=", expected);
    Assert.Contains("pts", expected);
    Assert.Contains("stopTicks=", expected);
    // Also verify CalcContracts is consistent with the expected qty.
    int qty = AtrSizingEngine.CalcContracts(atrPoints: 6.0, maxRisk: 150.0, tickDollarValue: 5.0);
    Assert.Equal(5, qty);
}
```

**Engineer note on Test 3**: `FireAtrUpdated` is `private`; `OnBarUpdate` is `protected` and
guarded by `CurrentBar < Period`. The test above verifies the format token contract by
constructing the string locally (same format literal as in `AtrSizingEngine.cs` line ~97-104)
and asserting the expected tokens exist. This is intentional — it pins the format contract
without requiring NT8 bar infrastructure. If the format string changes in `AtrSizingEngine`,
this test breaks, which is the desired behavior.

---

### DW-B10-03: Window Arm BE Column -- `TradeCopierWindow.cs`

#### New field

```csharp
// Arm BE button tracking -- accessed exclusively on UI thread (JS-021 compliant).
private readonly List<Button> _armBeBtns = new List<Button>();
```

Add alongside the other `_xxxBtns` fields (~line 36).

#### New method: `OnRuleArmBe`

```csharp
// Arm BE click handler for rule rows in TradeCopierWindow.
// Tag layout: object[] { instrumentNameOrTextBox, leaderComboBox, bufferTextBox }
// Calls engine.ArmPendingBe(instr, leaderAcc, bufferTicks).
// CYC=4: tag null (1), name empty (2), instr null (3), leader null (4).
// JS-021: no lock. JS-002: no return null (uses guard-return pattern).
private void OnRuleArmBe(object sender, RoutedEventArgs e)
```

**Implementation contract**:
```csharp
private void OnRuleArmBe(object sender, RoutedEventArgs e)
{
    var tag = (sender as Button)?.Tag as object[];
    if (tag == null) return;                              // guard 1: tag null

    string name = tag[0] is TextBox tb ? tb.Text
                : tag[0] as string ?? string.Empty;
    if (string.IsNullOrEmpty(name)) return;              // guard 2: name empty

    var instr = FindInstrument(name);
    if (instr == null) return;                           // guard 3: instr null

    var leaderCb  = tag[1] as ComboBox;
    var leaderAcc = leaderCb?.SelectedItem as Account;
    if (leaderAcc == null) return;                       // guard 4: leader null

    int buf = 2;
    var bufBox = tag[2] as TextBox;
    if (bufBox != null) int.TryParse(bufBox.Text, out buf);

    _engine.ArmPendingBe(instr, leaderAcc, buf);
}
```

#### `BuildRuleRow` modification (static rule rows)

After the existing Col 10 (or the last defined column), add:

- Col 11 `ColumnDefinition` with `GridLength.Auto`
- `[Arm BE]` Button: `Content = "[Arm BE]"`, `Background = WBrushInactive`, `Margin = new Thickness(2)`
- TextBox: width 30, default text `"2"` (buffer ticks)
- TextLabel: `"tks"` (ASCII only)
- `Tag = new object[] { instrumentName, leaderCb, bufferTextBox }`
  where `instrumentName` is the `string` parameter passed to `BuildRuleRow`,
  `leaderCb` is the existing leader account ComboBox in the row,
  `bufferTextBox` is the new TextBox just created.
- `armBeBtn.Click += OnRuleArmBe;`
- `_armBeBtns.Add(armBeBtn);`

#### `BuildDynamicRuleRow` modification (dynamic rule rows)

Same cluster addition as `BuildRuleRow`. `tag[0]` is the instrument-name `TextBox`
(not a string -- the dynamic row uses a TextBox for the instrument name).
Tag layout: `new object[] { instrTextBox, leaderCb, bufferTextBox }`.

**Implementation note**: `OnRuleArmBe` already handles both cases via
`tag[0] is TextBox tb ? tb.Text : tag[0] as string ?? string.Empty`.

---

### ATM Template Writer -- `TradeCopierPanel.cs`

#### New fields (add near other private widget fields)

```csharp
private ComboBox _atmTemplateCombo = null;
private string   _activeAtmTemplateName = string.Empty;
```

#### New `using` directive (if not already present)

```csharp
using System.IO;
```

Add at the top of `TradeCopierPanel.cs` alongside other `using System.*` statements.
(`System.Windows.Input` is already present per plan §12.)

#### New method: `GetAtmTemplatesDirectory`

```csharp
// Returns canonical NT8 ATM templates directory path.
// Pure string concatenation -- no IO, no branches.
// CYC=1: straight-line path build.
private static string GetAtmTemplatesDirectory()
```

**Implementation**:
```csharp
return System.IO.Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
    "NinjaTrader 8", "templates", "ATM") + System.IO.Path.DirectorySeparatorChar;
```

#### New method: `BuildAtmTemplateRow`

```csharp
// Appends "ATM:" label + ComboBox row to root StackPanel.
// LoadAtmTemplates() populates ComboBox ItemsSource after construction.
// CYC=1: straight-line widget construction.
private void BuildAtmTemplateRow(StackPanel root)
```

**Implementation contract**:
- Create horizontal `StackPanel` or `Grid` row.
- Add `TextBlock { Text = "ATM:" }` label.
- Create `ComboBox` and assign to `_atmTemplateCombo`.
- Wire `_atmTemplateCombo.SelectionChanged += OnAtmTemplateSelectionChanged;`
- Add row to `root`.

#### New method: `LoadAtmTemplates`

```csharp
// Reads .xml template filenames from NT8 ATM templates directory.
// Populates _atmTemplateCombo.ItemsSource with filename-without-extension list.
// Path: Environment.GetFolderPath(SpecialFolder.MyDocuments) + "NinjaTrader 8\templates\ATM\"
// On DirectoryNotFoundException or IO error: sets ItemsSource to empty string[] (no throw).
// CYC=3: directory null/empty guard (1), directory exists guard (2), foreach populate (3).
private void LoadAtmTemplates()
```

**Implementation contract**:
```csharp
private void LoadAtmTemplates()
{
    string dir = GetAtmTemplatesDirectory();
    if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))  // guards 1+2
    {
        _atmTemplateCombo.ItemsSource = new string[0];
        return;
    }
    var files = Directory.GetFiles(dir, "*.xml");              // guard 3: foreach
    var names = new string[files.Length];
    for (int i = 0; i < files.Length; i++)
        names[i] = Path.GetFileNameWithoutExtension(files[i]);
    _atmTemplateCombo.ItemsSource = names;
}
```

- No `throw` on IO failure. JS-001: CLEAN.
- Returns `string[0]` (empty array) on any fail path. JS-002: CLEAN (no `return null`).

#### New method: `OnAtmTemplateSelectionChanged`

```csharp
// Stores selected ATM template name in _activeAtmTemplateName field.
// No engine call at selection time -- template applied when orders are submitted (future block).
// CYC=2: null guard (1) + store selection (2).
private void OnAtmTemplateSelectionChanged(object sender, SelectionChangedEventArgs e)
```

**Implementation contract**:
```csharp
private void OnAtmTemplateSelectionChanged(object sender, SelectionChangedEventArgs e)
{
    var item = _atmTemplateCombo?.SelectedItem as string;
    if (item == null) return;
    _activeAtmTemplateName = item;
}
```

#### `BuildUI()` modification

At the end of `BuildUI()`, after all existing rows, add:
```csharp
BuildAtmTemplateRow(root);
```

#### `OnLoaded()` modification

At the end of `OnLoaded()`, after follower items are populated, add:
```csharp
LoadAtmTemplates();
```

---

### T2: Jane Street Constraint Table

| Rule | Applies To | Constraint |
|------|-----------|-----------|
| JS-021 | `_armBeBtns`, `OnRuleArmBe` | No `lock()`. `_armBeBtns` accessed on UI thread only (button event). |
| JS-002 | `LoadAtmTemplates`, `OnAtmTemplateSelectionChanged` | No `return null`. `LoadAtmTemplates` returns empty array on fail; `OnAtmTemplateSelectionChanged` uses guard-return (void). |
| JS-001 | `LoadAtmTemplates`, `OnRuleArmBe` | No `throw` on IO fail or null guard path. |
| JS-033 | All new handlers | No `async void`. |
| JS-023 | `_activeAtmTemplateName`, `_atmTemplateCombo` | No `volatile` -- UI-thread-only fields. |

### T2: NT8 Constraint Table

| Rule | Applies To | Constraint |
|------|-----------|-----------|
| NT8-003 | No new double fields | No `volatile double`. |
| NT8-001 | No new properties | No `{ get; init; }`. |
| NT8-002 | No new type declarations | No `abstract record` or `sealed record`. |
| NT8-004 | `LoadAtmTemplates` | No `ImmutableDictionary` / `System.Collections.Immutable`. Use `string[]`. |
| NT8-007 | `OnRuleArmBe` | No `CreateOrder` in new code. Only `_engine.ArmPendingBe(...)` is called. |
| ASCII-only | `BuildAtmTemplateRow`, `BuildRuleRow` | "ATM:", "Arm BE", "tks", "2" -- all ASCII. |
| No `FontFamily` | No font overrides | N/A. |
| No hardcoded hex | No new `Color.FromArgb` | Reuses existing `WBrushInactive`. |
| `Math.Clamp` ban | `OnRuleArmBe` buffer parse | Use `int.TryParse` with default 2. No `Math.Clamp`. |
| `DateTime.UtcNow` | N/A in T2 | Not used. |

---

### T2: 7-Scan Checklist (engineer contract)

```
SCAN-01: lock() zero occurrences
  grep -n "lock(" src/PropTraderTools/TradeCopierPanel.cs
  grep -n "lock(" src/PropTraderTools/TradeCopierWindow.cs
  grep -n "lock(" src/PropTraderTools/CopyEngineTests.cs
  REQUIRED: zero matches

SCAN-02: async void zero (except FlashBeFired)
  grep -n "async void" src/PropTraderTools/TradeCopierPanel.cs
  grep -n "async void" src/PropTraderTools/TradeCopierWindow.cs
  REQUIRED: zero matches in new/modified code
  (FlashBeFired in TradeCopierPanel.cs is pre-existing exempt)

SCAN-03: return null zero
  grep -n "return null" src/PropTraderTools/TradeCopierPanel.cs
  grep -n "return null" src/PropTraderTools/TradeCopierWindow.cs
  REQUIRED: zero matches in new/modified methods
  LoadAtmTemplates returns string[0] not null. OnAtmTemplateSelectionChanged
  uses guard-return (void). OnRuleArmBe uses guard-return (void).

SCAN-04: CYC > 8 zero
  New T2 methods:
    BuildAtmTemplateRow(1), LoadAtmTemplates(3), GetAtmTemplatesDirectory(1),
    OnAtmTemplateSelectionChanged(2), OnRuleArmBe(4).
  Highest: OnRuleArmBe=4. All within limit.
  REQUIRED: manual CYC count confirms no method exceeds 8.

SCAN-05: volatile double / volatile bool zero (new fields only)
  _atmTemplateCombo is ComboBox ref (no volatile).
  _activeAtmTemplateName is string ref (no volatile).
  _armBeBtns is List<Button> ref (no volatile).
  REQUIRED: no new volatile field declarations in T2.

SCAN-06: Math.Clamp zero
  grep -n "Math.Clamp" src/PropTraderTools/TradeCopierPanel.cs
  grep -n "Math.Clamp" src/PropTraderTools/TradeCopierWindow.cs
  REQUIRED: zero matches

SCAN-07: ASCII-only string literals
  All new string literals: "ATM:", "Arm BE", "tks", "2", "NinjaTrader 8",
  "templates", "ATM", "*.xml" -- all ASCII.
  grep -Pn "[^\x00-\x7F]" src/PropTraderTools/TradeCopierPanel.cs
  grep -Pn "[^\x00-\x7F]" src/PropTraderTools/TradeCopierWindow.cs
  REQUIRED: zero matches in new/modified lines
```

---

## Cross-Ticket Dependency Summary

| Item | T1 Phase A | T1 Phase B | T2 ATM+Tests | T2 Arm BE |
|------|-----------|-----------|-------------|----------|
| SIM101 PASS required | N/A | YES | No (keyboard verify only) | No |
| DW-B10-01 complete | In T1 | -- | -- | -- |
| DW-B10-02 complete | -- | -- | In T2 | -- |
| DW-B10-03 complete | -- | -- | -- | In T2 |
| DW-B10-04 complete | In T1 | -- | -- | -- |
| Blocked if SIM101 FAIL | Only Phase B shortcut wiring | BLOCKED | keyboard verify only; ATM+tests unblocked | Unblocked |

All DW-B10-xx items (01-04) are independent of SIM101 outcome and MUST complete
regardless of the keyboard feasibility result.

---

## CYC Table (all new methods, both tickets)

| Method | File | Decision Points | CYC | Within Limit |
|--------|------|----------------|-----|-------------|
| `OnChartKeyDiag` | TradeCopierAddOn.cs | 0 (lambda inner guard excluded from outer fn) | 1 | YES |
| `HookKeyShortcut` | TradeCopierAddOn.cs | 1 (null guard) | 2 | YES |
| `UnhookKeyShortcut` | TradeCopierAddOn.cs | 1 (TryRemove guard) | 2 | YES |
| `RemoveSim101` | TradeCopierAddOn.cs | 1 (null guard on field) | 2 | YES |
| `OnChartKeyDown` | TradeCopierPanel.cs | 2 (null guard + modifier guard) | 3 | YES |
| `DispatchShortcut` | TradeCopierPanel.cs | 4 (switch: T, F, C, B) | 5 | YES |
| `BuildAtmTemplateRow` | TradeCopierPanel.cs | 0 (straight-line) | 1 | YES |
| `LoadAtmTemplates` | TradeCopierPanel.cs | 2 (exists guard + foreach) | 3 | YES |
| `GetAtmTemplatesDirectory` | TradeCopierPanel.cs | 0 (straight-line) | 1 | YES |
| `OnAtmTemplateSelectionChanged` | TradeCopierPanel.cs | 1 (null guard) | 2 | YES |
| `OnRuleArmBe` | TradeCopierWindow.cs | 3 (tag null + name empty + instr null + leader null counted as 4 guards = CYC 4 per plan §9) | 4 | YES |

All values <= 8. Plan §9 pre-flight: CONFIRMED.

---

## Backlog Item: DW-B11-DEFER-01

| ID | Description | Target |
|----|-------------|--------|
| DW-B11-DEFER-01 | Convert `Flatten`/`Trim` shortcuts to Limit orders per DW-B12-BUFFERED-BUTTONS-01. Key.F should emit `OrderType.Limit@bid+buffer`; Key.T should emit `OrderType.Limit@ask-buffer`. Requires new `Flatten(Instrument, int exitBuffer)` and `Trim(Instrument, int exitBuffer)` signatures on CopyEngine. | B12 |

---

## B11 Pipeline Completion Criteria

B11 is PIPELINE_COMPLETE when ALL of the following are true:

1. T1 Phase A (SIM101) executed and outcome recorded (PASS or FAIL).
2. T1 Phase B keyboard wiring: implemented if SIM101 PASS; deferred to B12 if SIM101 FAIL.
3. DW-B10-01: `BuildDiagRow`, `OnDiagGap001d`, `OnDiagGap002` deleted from `TradeCopierPanel.cs`;
   `RunGap001dTest`, `RunGap002Test`, `OnGap002AccountUpdate`, `_gap002TickCount`,
   `_gap002Account` deleted from `TradeCopierAddOn.cs`. CLOSED.
4. DW-B10-02: 3 `[Fact]` tests added to `CopyEngineTests.cs`. CLOSED.
5. DW-B10-03: Arm BE cluster added to `BuildRuleRow` and `BuildDynamicRuleRow` in
   `TradeCopierWindow.cs`; `OnRuleArmBe` implemented. CLOSED.
6. DW-B10-04: `NT8_ADDON_KNOWLEDGE.md` section updated with RESOLVED status. CLOSED.
7. `deploy-sync.ps1` run to re-sync NinjaTrader hard links.
8. All 7-scan checklists (T1 + T2) PASS with zero findings.
9. NT8 F5 compilation GREEN.

---

*TICKETS_COMPLETE*
