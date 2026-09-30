# PTT-COPIER-B4 — Implementation Tickets

**Epic**: PTT-COPIER-B4  
**Phase**: 4 — Ticket Generation  
**Status**: TICKETS_COMPLETE  
**Date**: 2026-06-03  
**Plan source**: `02-architecture-plan.md` (REVIEW_PASS 30/30)  
**Wave workspace**: `c:\WSGTA\universal-or-strategy`

---

## Preamble — What "already in source" means

The plan-review confirmed all three source files in the Wave workspace
(`src/PropTraderTools/`) already contain the B4 additions. These tickets
describe those changes precisely so they can be:
1. **Verified** against source line-by-line, and
2. **Re-applied** to a clean baseline if a rebase or rollback is ever needed.

Each change list cites the **current line numbers** in the Wave workspace files
as read during ticket generation.

---

## T1 — CopyEngine.cs: BreakEven engine methods

**File**: `src/PropTraderTools/CopyEngine.cs`  
**Current LOC**: 419  
**Depends on**: Nothing — standalone engine change.

---

### Ordered Change List

#### Change 1: `IsStopLeg(Order)` private helper
**Insert location**: After `IsBracketLeg` method, before closing brace of `CopyEngine` class.  
**Current anchor**: Line 378–385 is `IsBracketLeg`. New method belongs at line ~369 (already present).

```csharp
// B4: a stop leg is specifically a working stop order -- NOT a target, NOT PTT-prefixed
private bool IsStopLeg(Order order)
{
    if (order.OrderType != OrderType.Stop) return false;
    if (order.OrderState != OrderState.Working) return false;
    if (order.Name != null && order.Name.StartsWith("PTT-")) return false;
    if (order.Name != null && order.Name.StartsWith("Target")) return false;
    return true;
}
```

**Current location in source**: Lines 368–376 (already present — verify).  
**CYC**: 5 (four `if` guards + base). ✅

---

#### Change 2: `BreakEven(Instrument, int)` public entry point
**Insert location**: After `CancelPendingEntries` method (~line 290–314), before `IsDedup`.

```csharp
// B4: move the working stop for every account in the rule to break-even + bufferTicks
internal void BreakEven(Instrument instrument, int bufferTicks)
{
    foreach (var acc in AllAccounts(instrument))
    {
        var pos = acc.Positions.FindByInstrument(instrument);
        if (pos == null || pos.Quantity == 0)
            continue;

        double tickSize = instrument.MasterInstrument.TickSize;
        double bePrice = pos.MarketPosition == MarketPosition.Long
            ? Math.Round((pos.AveragePrice + bufferTicks * tickSize) / tickSize) * tickSize
            : Math.Round((pos.AveragePrice - bufferTicks * tickSize) / tickSize) * tickSize;

        foreach (var order in acc.Orders)
        {
            if (order.Instrument != instrument) continue;
            if (!IsStopLeg(order)) continue;
            try
            {
                order.Change(0, bePrice, order.Quantity);
                StatusUpdate?.Invoke(acc.Name + ": BE stop -> " + bePrice.ToString("F2"));
            }
            catch (Exception ex)
            {
                StatusUpdate?.Invoke("PTT-BE error: " + ex.Message);
            }
        }
    }
}
```

**Current location in source**: Lines 387–416 (already present — verify).  
**CYC**: ≤ 8 (outer foreach, flat-guard `||`, ternary, inner foreach, `continue` ×2, catch). ✅  

**NT8 API notes**:
- `acc.Positions.FindByInstrument(instrument)` — live position lookup.
- `pos.AveragePrice` — break-even baseline.
- `pos.MarketPosition == MarketPosition.Long` — direction.
- `instrument.MasterInstrument.TickSize` — tick precision.
- `Math.Round(raw / tickSize) * tickSize` — mandatory price normalization.
- `order.Change(0, bePrice, order.Quantity)` — signature: `(limitPrice, stopPrice, quantity)`. `limitPrice = 0` for stop orders.
- **No `CreateOrder` call** — modifies existing stop in place. ✅

---

### Method Signatures (T1 complete set)

```csharp
// New private helper
private bool IsStopLeg(Order order)

// New public entry point
internal void BreakEven(Instrument instrument, int bufferTicks)
```

---

### xUnit Tests for T1

```csharp
// File: tests/PTT-COPIER-B4.Tests/CopyEngineBreakEvenTests.cs

[Fact]
public void BreakEven_FlatAccount_SkipsWithoutCallingChange()
{
    // Arrange: account with position.Quantity == 0
    // Assert: order.Change never called, no exception thrown
}

[Fact]
public void BreakEven_LongPosition_StopMovedToEntryPlusBuf()
{
    // Arrange: Long 1 MES at 5000.00, bufferTicks=2, tickSize=0.25
    // Expected bePrice = 5000.50
    // Assert: order.Change(0, 5000.50, qty) called exactly once
}

[Fact]
public void BreakEven_ShortPosition_StopMovedToEntryMinusBuf()
{
    // Arrange: Short 1 MES at 5000.00, bufferTicks=3, tickSize=0.25
    // Expected bePrice = 4999.25
    // Assert: order.Change(0, 4999.25, qty) called exactly once
}

[Fact]
public void BreakEven_ZeroBufferTicks_StopMovedToExactEntry()
{
    // Arrange: Long 2 MES at 4998.75, bufferTicks=0, tickSize=0.25
    // Expected bePrice = 4998.75 (no buffer)
    // Assert: order.Change(0, 4998.75, qty) called
}

[Fact]
public void IsStopLeg_PTTPrefixedOrder_ReturnsFalse()
{
    // Arrange: order.Name = "PTT-Copy", OrderType=Stop, OrderState=Working
    // Assert: IsStopLeg returns false
}

[Fact]
public void IsStopLeg_TargetPrefixedOrder_ReturnsFalse()
{
    // Arrange: order.Name = "Target 1", OrderType=Stop, OrderState=Working
    // Assert: IsStopLeg returns false
}

[Fact]
public void IsStopLeg_UnnamedWorkingStop_ReturnsTrue()
{
    // Arrange: order.Name = null, OrderType=Stop, OrderState=Working
    // Assert: IsStopLeg returns true
}

[Fact]
public void BreakEven_NullInstrument_DoesNotThrow()
{
    // Assert: BreakEven(null, 2) exits cleanly -- FindRule null guard path
}
```

---

### Acceptance Criteria (T1)

- [ ] `IsStopLeg` private helper exists at line ~369; returns `false` for `"PTT-"` or `"Target"` prefix, `false` for non-`Stop` type, `false` for non-`Working` state.
- [ ] `BreakEven` public method iterates `AllAccounts`; skips flat positions; computes `bePrice` with tick normalization; calls `order.Change(0, bePrice, order.Quantity)` on each matching stop; catches and routes exceptions to `StatusUpdate`.
- [ ] No `CreateOrder` call added anywhere in `CopyEngine.cs`.
- [ ] No `lock()` added anywhere in `CopyEngine.cs`.
- [ ] All new string literals are ASCII-only.
- [ ] `dotnet build` exits 0 after this change in isolation.

---

### 7-Scan Checklist (T1)

| Scan | Command | Expected Result |
|------|---------|-----------------|
| SCAN-01 lock | `grep -n "lock\s*(" src/PropTraderTools/CopyEngine.cs` | **0 matches** |
| SCAN-02 DateTime.Now | `grep -n "DateTime\.Now" src/PropTraderTools/CopyEngine.cs` | **0 matches** |
| SCAN-03 hex / FontFamily | `grep -nE "#[0-9A-Fa-f]{6}\|new FontFamily" src/PropTraderTools/CopyEngine.cs` | **0 matches** |
| SCAN-04 CYC | `IsStopLeg` CYC ≤ 8; `BreakEven` CYC ≤ 8 | **Both ≤ 8** |
| SCAN-05 ASCII | All new literals: `"PTT-"`, `"Target"`, `"BE stop -> "`, `"PTT-BE error: "` | **All ASCII** |
| SCAN-06 PTT-prefix | `grep -n "CreateOrder" src/PropTraderTools/CopyEngine.cs` | **Existing 3 calls only** — no new calls from `BreakEven` |
| SCAN-07 Dispatcher | `CopyEngine` has zero WPF usings. `StatusUpdate` delegates to `Dispatcher.InvokeAsync` in surfaces. | **No direct WPF in engine** |

---

## T2 — TradeCopierPanel.cs: BE cluster + Shift+B binding

**File**: `src/PropTraderTools/TradeCopierPanel.cs`  
**Current LOC**: 232  
**Depends on**: T1 (`CopyEngine.BreakEven` must be present).

---

### Ordered Change List

#### Change 1: `_beBtn` and `_beBufferBox` field declarations
**Insert location**: After `_cancelBtn` field declaration (line 23), before `_beBtn` (line 24).

```csharp
private Button _beBtn;
private TextBox _beBufferBox;
```

**Current location in source**: Lines 24–25 (already present — verify).

---

#### Change 2: `actionGrid` column count change
**Location**: `BuildUI()` method, `UniformGrid` construction.  
**Current line**: Line 95.

```csharp
// BEFORE (B3 baseline):
var actionGrid = new UniformGrid { Columns = 3 };

// AFTER (B4):
var actionGrid = new UniformGrid { Columns = 4 };
```

**Current state in source**: Line 95 reads `Columns = 4` (already present — verify).

---

#### Change 3: BE cluster added as 4th cell of `actionGrid`
**Insert location**: After `_cancelBtn` is added to `actionGrid` (after line 110), before `root.Children.Add(actionGrid)` (line 128).

```csharp
// B4: Break Even cluster -- button + inline buffer TextBox
var beCluster = new StackPanel { Orientation = Orientation.Horizontal };
_beBtn = new Button { Content = "BE  S+B", IsEnabled = true, Margin = new Thickness(0, 0, 2, 0) };
_beBtn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
_beBtn.Click += OnBreakEven;
beCluster.Children.Add(_beBtn);

_beBufferBox = new TextBox { Text = "2", Width = 28, VerticalContentAlignment = VerticalAlignment.Center };
beCluster.Children.Add(_beBufferBox);

var tkLabel = new TextBlock { Text = "tks", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 0, 0) };
tkLabel.SetResourceReference(TextBlock.ForegroundProperty, "NTBrushes.SubtleBrush");
beCluster.Children.Add(tkLabel);

actionGrid.Children.Add(beCluster);
```

**Current location in source**: Lines 112–126 (already present — verify).

---

#### Change 4: Shift+B keyboard binding
**Insert location**: After existing `Shift+C` binding (line 143), before `Content = root` (line 146).

```csharp
var beCmd = new RelayCommand(o => OnBreakEven(null, null)); // B4
InputBindings.Add(new KeyBinding(beCmd, Key.B, ModifierKeys.Shift)); // B4
```

**Current location in source**: Lines 139 and 144 (already present — verify).  
**Note**: `RelayCommand` inner class is REUSED — no new type added.

---

#### Change 5: `OnBreakEven` handler
**Insert location**: After `OnCancel` method (lines 168–172), before `OnApplyRule` (line 184).

```csharp
// B4: move stop to break-even for this chart's instrument
private void OnBreakEven(object sender, RoutedEventArgs e)
{
    if (_instrument == null) return;
    int ticks = 2;
    if (int.TryParse(_beBufferBox?.Text?.Trim(), out int parsed) && parsed >= 0)
        ticks = parsed;
    _engine.BreakEven(_instrument, ticks);
}
```

**Current location in source**: Lines 174–182 (already present — verify).  
**CYC**: 2 (null check + TryParse branch). ✅

---

### Method Signatures (T2 complete set)

```csharp
// New event handler
private void OnBreakEven(object sender, RoutedEventArgs e)
```

---

### xUnit Tests for T2

```csharp
// File: tests/PTT-COPIER-B4.Tests/TradeCopierPanelTests.cs

[Fact]
public void OnBreakEven_NullInstrument_EngineNotCalled()
{
    // Arrange: panel with _instrument = null
    // Assert: CopyEngine.BreakEven never called
}

[Fact]
public void OnBreakEven_ValidBuffer_PassesCorrectTicks()
{
    // Arrange: _beBufferBox.Text = "5"
    // Assert: engine.BreakEven(_instrument, 5) called
}

[Fact]
public void OnBreakEven_InvalidBuffer_DefaultsTo2()
{
    // Arrange: _beBufferBox.Text = "abc"
    // Assert: engine.BreakEven(_instrument, 2) called
}

[Fact]
public void OnBreakEven_NegativeBuffer_DefaultsTo2()
{
    // Arrange: _beBufferBox.Text = "-1"
    // Assert: engine.BreakEven(_instrument, 2) called (parsed >= 0 guard)
}

[Fact]
public void BuildUI_ActionGridHas4Columns()
{
    // Assert: actionGrid.Columns == 4
}

[Fact]
public void ShiftB_Binding_InvokesOnBreakEven()
{
    // Assert: InputBindings contains KeyBinding(Key.B, ModifierKeys.Shift)
}
```

---

### Acceptance Criteria (T2)

- [ ] `_beBtn` (Button) and `_beBufferBox` (TextBox) declared as private fields (lines ~24–25).
- [ ] `actionGrid` is `UniformGrid { Columns = 4 }` (line ~95).
- [ ] BE cluster (StackPanel with `_beBtn`, `_beBufferBox`, `"tks"` label) added as 4th child of `actionGrid`.
- [ ] Button content is `"BE  S+B"` (ASCII spaces — no Unicode).
- [ ] `_beBufferBox.Text` defaults to `"2"`.
- [ ] `"tks"` label color set via `SetResourceReference(TextBlock.ForegroundProperty, "NTBrushes.SubtleBrush")` — no hex.
- [ ] `Shift+B` `KeyBinding` added to `InputBindings`.
- [ ] `OnBreakEven` reads `_beBufferBox.Text`, falls back to `2` on parse failure or negative value.
- [ ] `OnBreakEven` calls `_engine.BreakEven(_instrument, ticks)` — no direct order operations.
- [ ] `dotnet build` exits 0 after this change in isolation.

---

### 7-Scan Checklist (T2)

| Scan | Command | Expected Result |
|------|---------|-----------------|
| SCAN-01 lock | `grep -n "lock\s*(" src/PropTraderTools/TradeCopierPanel.cs` | **0 matches** |
| SCAN-02 DateTime.Now | `grep -n "DateTime\.Now" src/PropTraderTools/TradeCopierPanel.cs` | **0 matches** |
| SCAN-03 hex / FontFamily | `grep -nE "#[0-9A-Fa-f]{6}\|new FontFamily" src/PropTraderTools/TradeCopierPanel.cs` | **0 matches** |
| SCAN-04 CYC | `OnBreakEven` CYC = 2 | **≤ 8** |
| SCAN-05 ASCII | All new literals: `"BE  S+B"`, `"tks"`, `"2"` | **All ASCII** |
| SCAN-06 PTT-prefix | `grep -n "CreateOrder" src/PropTraderTools/TradeCopierPanel.cs` | **0 matches** (Panel never calls CreateOrder) |
| SCAN-07 Dispatcher | `StatusUpdate` → `OnStatusUpdate` → `Dispatcher.InvokeAsync` — existing path unchanged. New `OnBreakEven` fires from UI thread. | **No off-thread WPF access** |

---

## T3 — TradeCopierWindow.cs: col 8 BE cluster + handler

**File**: `src/PropTraderTools/TradeCopierWindow.cs`  
**Current LOC**: 400  
**Depends on**: T1 (`CopyEngine.BreakEven` must be present).

---

### Ordered Change List

#### Change 1: `BuildRuleRow` — add col 8 `ColumnDefinition`
**Location**: `BuildRuleRow(string instrumentName)`, after existing 8 `ColumnDefinition` adds.  
**Current anchor**: Line 119 is the 8th `ColumnDefinition` (`GridLength.Auto`). Col 8 is added immediately after.

```csharp
grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // B4: BE cluster
```

**Current location in source**: Line 120 (already present — verify `// B4: BE cluster` comment).  
**Grid columns after change**: 9 (cols 0–8). Existing `applyBtn` remains at col 7.

---

#### Change 2: `BuildRuleRow` — insert BE cluster at col 8
**Insert location**: After `applyBtn` is added to `grid.Children` (line 185–186), before `return grid` (line ~203).

```csharp
// B4: Break Even cluster (col 8) -- [BE] + inline buffer TextBox + "tks"
var beCluster = new StackPanel { Orientation = Orientation.Horizontal };
var beBtn = new Button { Content = "[BE]", Margin = new Thickness(2) };
beBtn.SetResourceReference(Button.StyleProperty, "NTButtonStyle");
var beBox = new TextBox { Text = "2", Width = 28, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(2) };
var tksLabel = new TextBlock { Text = "tks", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(1, 0, 2, 0) };
tksLabel.SetResourceReference(TextBlock.ForegroundProperty, "NTBrushes.SubtleBrush");
beBtn.Tag = new object[] { instrumentName, beBox };
beBtn.Click += OnRuleBreakEven;
beCluster.Children.Add(beBtn);
beCluster.Children.Add(beBox);
beCluster.Children.Add(tksLabel);
Grid.SetColumn(beCluster, 8);
grid.Children.Add(beCluster);
```

**Current location in source**: Lines 188–201 (already present — verify).  
**Tag layout**: `tag[0]` = `instrumentName` (string), `tag[1]` = `beBox` (TextBox).

---

#### Change 3: `BuildDynamicRuleRow` — add col 8 `ColumnDefinition`
**Location**: `BuildDynamicRuleRow()`, after existing 8 `ColumnDefinition` adds.  
**Current anchor**: Line 217 is the 8th `ColumnDefinition`. Col 8 added immediately after.

```csharp
grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // B4: BE cluster
```

**Current location in source**: Line 218 (already present — verify comment).

---

#### Change 4: `BuildDynamicRuleRow` — insert BE cluster at col 8
**Insert location**: After `applyBtn` added to `grid.Children` (lines 262–267), before `return grid` (line ~284).

```csharp
// B4: Break Even cluster (col 8) -- Tag carries [TextBox instrRef, TextBox beBox]
var beCluster = new StackPanel { Orientation = Orientation.Horizontal };
var beBtn = new Button { Content = "[BE]", Margin = new Thickness(2) };
beBtn.SetResourceReference(Button.StyleProperty, "NTButtonStyle");
var beBox = new TextBox { Text = "2", Width = 28, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(2) };
var tksLabel = new TextBlock { Text = "tks", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(1, 0, 2, 0) };
tksLabel.SetResourceReference(TextBlock.ForegroundProperty, "NTBrushes.SubtleBrush");
beBtn.Tag = new object[] { instrTextBox, beBox }; // instrTextBox = col 0 TextBox
beBtn.Click += OnRuleBreakEven;
beCluster.Children.Add(beBtn);
beCluster.Children.Add(beBox);
beCluster.Children.Add(tksLabel);
Grid.SetColumn(beCluster, 8);
grid.Children.Add(beCluster);
```

**Current location in source**: Lines 269–282 (already present — verify).  
**Tag layout**: `tag[0]` = `instrTextBox` (TextBox, col 0), `tag[1]` = `beBox` (TextBox).  
**Key difference from `BuildRuleRow`**: `tag[0]` is a `TextBox` (instrument typed at runtime) instead of a string literal.

---

#### Change 5: `OnRuleBreakEven` handler
**Insert location**: After `OnRuleToggle` method (lines 326–334), before `OnRuleBreakEven` (line 337).

```csharp
// B4: move stop to break-even for the rule's instrument
private void OnRuleBreakEven(object sender, RoutedEventArgs e)
{
    var tag = (sender as Button)?.Tag as object[];
    if (tag == null) return;
    string instrName = tag[0] is TextBox tb ? tb.Text : tag[0] as string;
    if (string.IsNullOrEmpty(instrName)) return;
    int ticks = 2;
    if (tag.Length > 1 && tag[1] is TextBox beBox)
    {
        if (int.TryParse(beBox.Text?.Trim(), out int parsed) && parsed >= 0)
            ticks = parsed;
    }
    var instrument = FindInstrument(instrName);
    if (instrument != null)
        _engine.BreakEven(instrument, ticks);
}
```

**Current location in source**: Lines 337–352 (already present — verify).  
**CYC**: 4 (`tag == null` check; is-pattern ternary; `TryParse` fail branch; `instrument != null` check). ✅  
**`FindInstrument`** is REUSED from line 385 — no duplication.

---

### Method Signatures (T3 complete set)

```csharp
// New event handler
private void OnRuleBreakEven(object sender, RoutedEventArgs e)
```

---

### xUnit Tests for T3

```csharp
// File: tests/PTT-COPIER-B4.Tests/TradeCopierWindowTests.cs

[Fact]
public void OnRuleBreakEven_NullTag_DoesNothing()
{
    // Arrange: sender button with Tag = null
    // Assert: no exception, engine not called
}

[Fact]
public void OnRuleBreakEven_StringTag_PassesInstrumentName()
{
    // Arrange: tag[0] = "MES" (string), tag[1] = TextBox("3")
    // Assert: FindInstrument("MES") called; engine.BreakEven(..., 3) called
}

[Fact]
public void OnRuleBreakEven_TextBoxTag_ReadsDynamicInstrument()
{
    // Arrange: tag[0] = TextBox { Text = "NQ 09-26" }, tag[1] = TextBox("0")
    // Assert: engine.BreakEven called with buf=0
}

[Fact]
public void OnRuleBreakEven_EmptyInstrName_DoesNothing()
{
    // Arrange: tag[0] = "" (string)
    // Assert: engine.BreakEven never called
}

[Fact]
public void BuildRuleRow_Has9ColumnDefinitions()
{
    // Assert: grid.ColumnDefinitions.Count == 9 after BuildRuleRow("MES")
}

[Fact]
public void BuildDynamicRuleRow_Has9ColumnDefinitions()
{
    // Assert: grid.ColumnDefinitions.Count == 9 after BuildDynamicRuleRow()
}

[Fact]
public void BuildRuleRow_BeClusterAtColumn8()
{
    // Assert: BE StackPanel Grid.GetColumn == 8
}

[Fact]
public void BuildDynamicRuleRow_BeClusterTagUsesTextBoxRef()
{
    // Assert: beBtn.Tag[0] is TextBox (not string)
}
```

---

### Acceptance Criteria (T3)

- [ ] `BuildRuleRow` has 9 `ColumnDefinition` entries (cols 0–8); col 8 is `GridLength.Auto`.
- [ ] `BuildRuleRow` BE cluster: button `Content = "[BE]"`, `TextBox { Text = "2" }`, `"tks"` label; placed at `Grid.SetColumn(beCluster, 8)`.
- [ ] `BuildRuleRow` BE button `Tag = new object[] { instrumentName, beBox }` where `instrumentName` is string.
- [ ] `BuildDynamicRuleRow` has 9 `ColumnDefinition` entries; identical cluster structure.
- [ ] `BuildDynamicRuleRow` BE button `Tag = new object[] { instrTextBox, beBox }` where `instrTextBox` is the col-0 `TextBox`.
- [ ] `OnRuleBreakEven` resolves instrument name from `tag[0]` as either `TextBox.Text` or raw string.
- [ ] `OnRuleBreakEven` reads buffer from `tag[1]` TextBox; falls back to `2` on parse failure.
- [ ] `OnRuleBreakEven` calls `FindInstrument` (REUSED — not duplicated).
- [ ] `OnRuleBreakEven` calls `_engine.BreakEven(instrument, ticks)`.
- [ ] All new string literals are ASCII-only: `"[BE]"`, `"tks"`, `"2"`.
- [ ] Color references use `SetResourceReference ... "NTBrushes.SubtleBrush"` — no hex.
- [ ] `dotnet build` exits 0 after this change in isolation.

---

### 7-Scan Checklist (T3)

| Scan | Command | Expected Result |
|------|---------|-----------------|
| SCAN-01 lock | `grep -n "lock\s*(" src/PropTraderTools/TradeCopierWindow.cs` | **0 matches** |
| SCAN-02 DateTime.Now | `grep -n "DateTime\.Now" src/PropTraderTools/TradeCopierWindow.cs` | **0 matches** |
| SCAN-03 hex / FontFamily | `grep -nE "#[0-9A-Fa-f]{6}\|new FontFamily" src/PropTraderTools/TradeCopierWindow.cs` | **0 matches** |
| SCAN-04 CYC | `OnRuleBreakEven` CYC = 4 | **≤ 8** |
| SCAN-05 ASCII | All new literals: `"[BE]"`, `"tks"`, `"2"` | **All ASCII** |
| SCAN-06 PTT-prefix | `grep -n "CreateOrder" src/PropTraderTools/TradeCopierWindow.cs` | **0 matches** (Window never calls CreateOrder) |
| SCAN-07 Dispatcher | `OnStatusUpdate` (line 368–383) uses `Dispatcher.InvokeAsync` — unchanged. New `OnRuleBreakEven` fires from UI thread only. | **No off-thread WPF access** |

---

## Cross-Ticket Dependency Map

```
T1 (CopyEngine.cs)
  └─► T2 (TradeCopierPanel.cs) — calls CopyEngine.BreakEven
  └─► T3 (TradeCopierWindow.cs) — calls CopyEngine.BreakEven
```

T2 and T3 are independent of each other. Execution order: **T1 first, then T2 and T3 in any order**.

---

## Global Acceptance Gate (all three tickets)

Run after T1 + T2 + T3 are complete:

```powershell
# 1. Build gate
dotnet build src/PropTraderTools.csproj

# 2. Scan-01: zero lock()
grep -rn "lock\s*(" src/PropTraderTools/

# 3. Scan-02: zero DateTime.Now
grep -rn "DateTime\.Now" src/PropTraderTools/

# 4. Scan-06: no new CreateOrder in BE path
grep -n "CreateOrder" src/PropTraderTools/CopyEngine.cs
# Expected: exactly 3 lines (PTT-Copy, PTT-Trim, PTT-Flatten, PTT-Cancel)

# 5. Deploy-sync (hard-link re-sync)
powershell -File .\deploy-sync.ps1
```

All commands must exit 0 / return expected counts before wave PR is opened.

---

*End of PTT-COPIER-B4 Ticket File*
