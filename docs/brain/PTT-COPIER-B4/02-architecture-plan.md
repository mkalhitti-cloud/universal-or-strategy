# PTT-COPIER-B4 Architecture Plan

**Epic**: PTT-COPIER-B4  
**Phase**: 2 — Architecture  
**Status**: REVIEW_PENDING  
**Date**: 2026-06-03  
**Architect**: PTT Architect (sequentialthinking, 8 thoughts)

---

## §1 Summary

B4 adds a single trading capability: **BreakEven** — move the active stop order for each account
in a rule to the position's average-entry price plus an editable buffer (default 2 ticks).

The feature is wired on both existing surfaces (TradeCopierPanel and TradeCopierWindow) with an
inline buffer TextBox so the trader can adjust the buffer live without any dialog. The engine
implementation follows the identical structural pattern as Trim/Flatten/CancelPendingEntries: a
public entry point that delegates per-account work to a private extracted helper, all wrapped in
try/catch with StatusUpdate routing, zero lock(), no CreateOrder().

---

## §2 Scope

| Ticket | File | New Public API | New Private API |
|--------|------|----------------|-----------------|
| T1 | `CopyEngine.cs` | `BreakEven(Instrument, int)` | `MoveStopToBreakEven`, `IsStopLeg`, `IsFlat` |
| T2 | `TradeCopierPanel.cs` | — | `OnBreakEven` + `_beBufferBox` field |
| T3 | `TradeCopierWindow.cs` | — | `OnRuleBreakEven` |

**No new files.** All changes are additive within existing files.  
**No changes** to: hot path (`OnOrderUpdate`), dedup cache, `_isCopyEnabled`, `_rules` collection type,
`IsBracketLeg`, `Trim`, `Flatten`, `CancelPendingEntries`, existing keyboard shortcuts.

---

## §3 T1 — CopyEngine.cs

### 3.1 New Methods

#### `internal void BreakEven(Instrument instrument, int bufferTicks)`

Public entry point. UI-triggered only — never called from `OnOrderUpdate`.  
Iterates `AllAccounts(instrument)` (identical to Trim/Flatten) and delegates per-account
work to `MoveStopToBreakEven`.

```csharp
internal void BreakEven(Instrument instrument, int bufferTicks)
{
    foreach (var acc in AllAccounts(instrument))
        MoveStopToBreakEven(acc, instrument, bufferTicks);
}
```

**CYC = 1** (one loop, no branches). ✅

---

#### `private void MoveStopToBreakEven(Account acc, Instrument instrument, int bufferTicks)`

Per-account implementation. Contains all stop-movement logic.

```csharp
private void MoveStopToBreakEven(Account acc, Instrument instrument, int bufferTicks)
{
    var pos = acc.Positions.FindByInstrument(instrument);
    if (IsFlat(pos))
    {
        StatusUpdate?.Invoke(acc.Name + ": flat skip");
        return;
    }

    double tickSize = instrument.MasterInstrument.TickSize;
    int direction = pos.MarketPosition == MarketPosition.Long ? 1 : -1;
    double raw = pos.AveragePrice + direction * bufferTicks * tickSize;
    double newStop = Math.Round(raw / tickSize) * tickSize;

    foreach (var order in acc.Orders)
    {
        if (order.Instrument != instrument) continue;
        if (order.OrderState != OrderState.Working) continue;
        if (order.OrderType != OrderType.Stop) continue;
        if (!IsStopLeg(order)) continue;

        try
        {
            order.Change(0, newStop, order.Quantity);
            StatusUpdate?.Invoke(acc.Name + ": BE moved to " + newStop);
        }
        catch (Exception ex)
        {
            StatusUpdate?.Invoke("PTT-BE error: " + ex.Message);
        }
        break; // move at most one stop per account per call
    }
}
```

**CYC = 8** (exactly at limit). Branch breakdown:
1. `if (IsFlat(pos))` — 1  
2. ternary `Long ? 1 : -1` — 1  
3. `foreach` loop — 1  
4. `if (order.Instrument != instrument)` — 1  
5. `if (order.OrderState != OrderState.Working)` — 1  
6. `if (order.OrderType != OrderType.Stop)` — 1  
7. `if (!IsStopLeg(order))` — 1  
8. `catch` block — 1  

---

#### `private bool IsFlat(Position pos)`

Guards the flat-account early return. Extracted to keep `MoveStopToBreakEven` at CYC 8.

```csharp
private bool IsFlat(Position pos) => pos == null || pos.Quantity == 0;
```

**CYC = 3**. This helper prevents the `||` from adding to `MoveStopToBreakEven`'s branch count. ✅

---

#### `private bool IsStopLeg(Order order)`

Identifies a stop order that belongs to an existing bracket. Intentionally tighter than
`IsBracketLeg` (which also matches `"Target"` and `"PTT-"` prefixed orders).

```csharp
private bool IsStopLeg(Order order)
    => order.FromEntrySignal != null
    || (order.Name != null && order.Name.StartsWith("Stop"));
```

**CYC = 4**. ✅

**Why not reuse `IsBracketLeg`?**  
`IsBracketLeg` returns `true` for names starting with `"Target"` or `"PTT-"`. Using it inside
`MoveStopToBreakEven` would cause the loop to skip Target orders (correct) but also to match
them first and then `continue` — the `!IsStopLeg` guard ensures only genuine stop legs are
selected. More importantly, an order named `"PTT-Copy"` would satisfy `IsBracketLeg` and
be incorrectly selected as a stop to move. `IsStopLeg` is the tighter, correct filter.

### 3.2 Unchanged Methods

`IsBracketLeg` — remains for use by `CancelPendingEntries`. Not modified.  
`AllAccounts`, `FindRule`, `Trim`, `Flatten`, `CancelPendingEntries`, `SendCopy`,
`OnOrderUpdate`, `IsDedup`, `PassesDailyCapCheck`, `SetEnabled`, `SetDailyCapFloor`,
`SetRuleEnabled`, `AddRule`, `Subscribe`, `Unsubscribe` — all unchanged.

### 3.3 NT8 API Usage

| API | Usage in B4 |
|-----|-------------|
| `acc.Positions.FindByInstrument(instrument)` | Retrieve live position |
| `pos.AveragePrice` | Break-even price baseline |
| `pos.MarketPosition` | Determine Long/Short direction |
| `pos.Quantity` | Flat check |
| `instrument.MasterInstrument.TickSize` | Price precision unit |
| `Math.Round(raw / tickSize) * tickSize` | Price normalization (mandatory) |
| `acc.Orders` | Iterate account orders |
| `order.Instrument`, `order.OrderState`, `order.OrderType` | Filter orders |
| `order.FromEntrySignal`, `order.Name` | `IsStopLeg` detection |
| `order.Change(0, newStop, order.Quantity)` | Move stop price |
| `order.Quantity` | Preserve existing quantity |

**`order.Change` signature**: `void Change(double limitPrice, double stopPrice, int quantity)`  
For a stop order: `limitPrice = 0`, `stopPrice = newStop`, `quantity = order.Quantity`.

---

## §4 T2 — TradeCopierPanel.cs

### 4.1 New Field

```csharp
private TextBox _beBufferBox;
```

Added alongside existing button fields (`_trimBtn`, `_flattenBtn`, `_cancelBtn`).

### 4.2 BuildUI Changes

**UniformGrid change**: `Columns = 3` → `Columns = 4`.

**BE cluster** added as the 4th cell of the `UniformGrid`. The cluster is a horizontal
`StackPanel` so the button and buffer box sit inline:

```csharp
var beCluster = new StackPanel { Orientation = Orientation.Horizontal };

_beBtn = new Button { Content = "BE  S+B", IsEnabled = true };
_beBtn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
_beBtn.Click += OnBreakEven;
beCluster.Children.Add(_beBtn);

_beBufferBox = new TextBox
{
    Text = "2",
    Width = 30,
    VerticalAlignment = VerticalAlignment.Center,
    Margin = new Thickness(2, 0, 0, 0)
};
beCluster.Children.Add(_beBufferBox);

beCluster.Children.Add(new TextBlock
{
    Text = "tks",
    VerticalAlignment = VerticalAlignment.Center,
    Margin = new Thickness(2, 0, 0, 0)
});

actionGrid.Children.Add(beCluster);
```

**Keyboard shortcut** added to `InputBindings` (after existing Shift+T/F/C bindings):

```csharp
var beCmd = new RelayCommand(o => OnBreakEven(null, null));
InputBindings.Add(new KeyBinding(beCmd, Key.B, ModifierKeys.Shift));
```

Note: `RelayCommand` is REUSED (inner sealed class already present). No new type added.

### 4.3 New Handler

```csharp
private void OnBreakEven(object sender, RoutedEventArgs e)
{
    if (_instrument == null) return;
    if (!int.TryParse(_beBufferBox?.Text, out int buf)) buf = 2;
    _engine.BreakEven(_instrument, buf);
}
```

**CYC = 2**. ✅

### 4.4 New Private Field Declaration

```csharp
private Button _beBtn;
```

Added alongside `_cancelBtn`.

---

## §5 T3 — TradeCopierWindow.cs

### 5.1 BuildRuleRow Changes

Add one `ColumnDefinition` (col 8, `GridLength.Auto`) and place the BE cluster:

```csharp
grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // col 8 -- BE

var beBufferBox = new TextBox
{
    Text = "2",
    Width = 30,
    VerticalAlignment = VerticalAlignment.Center,
    Margin = new Thickness(2, 0, 0, 0)
};

var beBtn = new Button
{
    Content = "BE",
    Tag = new object[] { instrumentName, beBufferBox },
    Margin = new Thickness(2)
};
beBtn.SetResourceReference(Button.StyleProperty, "NTButtonStyle");
beBtn.Click += OnRuleBreakEven;

var beCluster = new StackPanel { Orientation = Orientation.Horizontal };
beCluster.Children.Add(beBtn);
beCluster.Children.Add(beBufferBox);
beCluster.Children.Add(new TextBlock
{
    Text = "tks",
    VerticalAlignment = VerticalAlignment.Center,
    Margin = new Thickness(2, 0, 0, 0)
});

Grid.SetColumn(beCluster, 8);
grid.Children.Add(beCluster);
```

**Column count after change**: 9 (cols 0–8). Existing col 7 (`applyBtn`) is unchanged.

### 5.2 BuildDynamicRuleRow Changes

Identical structure. The only difference is the Tag — uses `instrTextBox` (local `TextBox`)
rather than a string literal for `instrumentName`:

```csharp
grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // col 8 -- BE

var beBufferBox = new TextBox
{
    Text = "2",
    Width = 30,
    VerticalAlignment = VerticalAlignment.Center,
    Margin = new Thickness(2, 0, 0, 0)
};

var beBtn = new Button
{
    Content = "BE",
    Tag = new object[] { instrTextBox, beBufferBox }, // instrTextBox = col 0 TextBox
    Margin = new Thickness(2)
};
beBtn.SetResourceReference(Button.StyleProperty, "NTButtonStyle");
beBtn.Click += OnRuleBreakEven;

var beCluster = new StackPanel { Orientation = Orientation.Horizontal };
beCluster.Children.Add(beBtn);
beCluster.Children.Add(beBufferBox);
beCluster.Children.Add(new TextBlock
{
    Text = "tks",
    VerticalAlignment = VerticalAlignment.Center,
    Margin = new Thickness(2, 0, 0, 0)
});

Grid.SetColumn(beCluster, 8);
grid.Children.Add(beCluster);
```

### 5.3 New Handler

```csharp
private void OnRuleBreakEven(object sender, RoutedEventArgs e)
{
    var btn = sender as Button;
    var tag = btn?.Tag as object[];
    if (tag == null) return;
    string instrName = (tag[0] is TextBox tbInstr)
        ? tbInstr.Text?.Trim()
        : tag[0] as string;
    var beBufferBox = tag[1] as TextBox;
    if (!int.TryParse(beBufferBox?.Text, out int buf)) buf = 2;
    var instrument = FindInstrument(instrName);
    if (instrument != null)
        _engine.BreakEven(instrument, buf);
}
```

**CYC = 4** (tag null check, is-pattern ternary, TryParse fail branch, instrument null check). ✅

**`FindInstrument`** is REUSED from the existing Window method. Not duplicated.

---

## §6 JS-Compliance Matrix

| Rule | Description | B4 Status |
|------|-------------|-----------|
| JS-001 | No throw in hot path | ✅ `BreakEven` is UI-triggered. `order.Change()` wrapped in `try/catch` → `StatusUpdate`. No rethrow. |
| JS-010 | Private constructor | ✅ `CopyEngine()` private constructor unchanged. |
| JS-021 | No `lock()` | ✅ Zero `lock()` calls in any new method. `AllAccounts` uses `ConcurrentBag` iteration (lock-free). |
| JS-023 | `volatile bool _isCopyEnabled` | ✅ `BreakEven` does NOT gate on `_isCopyEnabled`. BE is position management, not copy control. |
| JS-025 | `ConcurrentBag` maintained | ✅ `_rules` type unchanged. `AllAccounts` → `FindRule` uses lock-free enumeration. |
| JS-008 | Readonly structs | ✅ No new structs introduced in B4. |
| ASCII | ASCII-only strings | ✅ All literals: `"flat skip"`, `"BE moved to "`, `"PTT-BE error: "`, `"BE  S+B"`, `"BE"`, `"tks"`, `"2"` — all ASCII. |
| DateTime | No `DateTime.Now` | ✅ No new `DateTime` usage in B4. |
| FontFamily | No `FontFamily` | ✅ None used. |
| Hex colors | No hardcoded hex | ✅ All colors via `SetResourceReference` and NTBrushes. |
| PTT- prefix | Order names prefixed | ✅ `BreakEven` calls `order.Change()` (modifies existing order, no `CreateOrder`). No new order name needed. |
| async/await | No async in lifecycle | ✅ All new methods are synchronous `void`. |

---

## §7 SCAN Assertions (SCAN-01 through SCAN-07)

### SCAN-01: No `lock()` statement
**Assertion**: `grep -n "lock\s*(" src/PropTraderTools/CopyEngine.cs` → zero matches after B4.  
**Basis**: B4 adds `BreakEven`, `MoveStopToBreakEven`, `IsStopLeg`, `IsFlat` — none use `lock()`. ✅

### SCAN-02: No `DateTime.Now`
**Assertion**: `grep -n "DateTime\.Now" src/PropTraderTools/CopyEngine.cs` → zero matches.  
**Basis**: No new `DateTime` usage in B4. Existing `IsDedup` uses `DateTime.UtcNow`. ✅

### SCAN-03: No hardcoded hex colors or FontFamily
**Assertion**: No `#[0-9A-Fa-f]{6}` or `new FontFamily` in any B4 new code.  
**Basis**: All styling via `SetResourceReference`. ✅

### SCAN-04: CYC <= 8 per method
**Assertion**: All 6 new methods are within threshold.

| Method | File | CYC |
|--------|------|-----|
| `BreakEven` | CopyEngine.cs | 1 |
| `IsFlat` | CopyEngine.cs | 3 |
| `IsStopLeg` | CopyEngine.cs | 4 |
| `MoveStopToBreakEven` | CopyEngine.cs | 8 |
| `OnBreakEven` | TradeCopierPanel.cs | 2 |
| `OnRuleBreakEven` | TradeCopierWindow.cs | 4 |

✅ All at or below limit.

### SCAN-05: ASCII-only identifiers and string literals
**Assertion**: All new identifiers and string constants contain only ASCII characters (0x00–0x7F).  
**Basis**: Verified in §6. ✅

### SCAN-06: All `CreateOrder` calls use `"PTT-"` prefix
**Assertion**: No new `CreateOrder` calls added in B4.  
**Basis**: `BreakEven` uses `order.Change()` not `CreateOrder`. ✅

### SCAN-07: `Dispatcher.InvokeAsync` used for all UI mutations from non-UI threads
**Assertion**: `BreakEven` / `MoveStopToBreakEven` are called from UI thread (button click).
`StatusUpdate` delegates marshal to UI via existing `Dispatcher.InvokeAsync` in both surfaces.
No direct WPF element access inside `CopyEngine`.  
**Basis**: CopyEngine has zero WPF usings. Existing `OnStatusUpdate` pattern unchanged. ✅

---

## §8 Accepted Deviations

| Deviation | Justification |
|-----------|---------------|
| `MoveStopToBreakEven` CYC = 8 (at limit) | The 4 order-filter guards (`Instrument`, `OrderState`, `OrderType`, `IsStopLeg`) are each necessary for correctness. Collapsing them would sacrifice clarity or correctness. The `IsFlat` extraction already consumed one reduction opportunity. |
| `_beBtn` declared as class-level field in Panel | Required because `OnBreakEven` is a class method (not lambda); the button itself need not be accessed post-construction, but declaring it as a field is consistent with `_trimBtn`, `_flattenBtn`, `_cancelBtn` — style consistency maintained. |
| `beBufferBox` in Window methods is local (not class-level field) | Per-row buffer boxes must be independent per rule row. Making them fields would be incorrect for multiple rows. The `Tag = new object[]{ ..., beBufferBox }` pattern captures the reference correctly — same pattern as `leaderCb`/`followerCb` in `applyBtn.Tag`. |

---

## §9 Block-5 Backlog

The following items are OUT OF SCOPE for B4 but noted for future blocks:

| Item | Rationale for Deferral |
|------|------------------------|
| BE for target orders (move profit target symmetrically) | Not requested in B4 spec. Separate command flow needed. |
| Keyboard shortcut for BE in Window (TradeCopierWindow) | Window does not currently use keyboard shortcuts. Adding focus/InputBindings management is a separate concern. |
| Multi-follower stop detection (when multiple stops exist per account) | Current spec says "this is the stop to move" (singular). `break` after first match. If multiple stops exist, only first matching one is moved. Multi-stop handling is future work. |
| Buffer box persistence (save last-used value across sessions) | Not requested. Default=2 sufficient for B4. |
| BE status in position P&L display | Out of scope for B4. |

---

*End of PTT-COPIER-B4 Architecture Plan*
