# B39-LaneA Architecture Plan — Global BE All
<!-- Rev 2 — JS-008 fix applied per plan review (2026-07-30) -->
**Status**: REVIEW_PASS (Rev 2 — JS-008 fix)
**Spec**: `specs/002-trade-copier-spec.html` id="section-b39"
**Baseline tag**: `"PTT-COPIER B38 | trim-anchor-be-tif | 2026-07-28"`
**Target tag**: `"PTT-COPIER B39 | global-be-all | {date}"`
**Author**: ptt-architect Phase 1
**Deferred from B38**: zero open deferred items entering B39

---

## §1 Objective

Add a `[BE ALL]` button that fires `SubmitBeStop` across every connected account times every
instrument that has a non-zero open position. No copy rule required. Works whether the copier
is ON, OFF, or never configured.

A new modular class `PttGlobalBreakEven` is created following the same standalone-file pattern
established in B33 for `PttBreakEven`, `PttTrim`, and `PttFlatten`. Each button, its own file.

---

## §2 Files Changed

| File | Type | Change |
|---|---|---|
| `src/PropTraderTools/Features/PttGlobalBreakEven.cs` | **NEW** | Execute, ExecuteOne, buffer helpers |
| `src/PropTraderTools/CopyEngine.cs` | Modified | Build tag + `SubmitBeStop` access + `GlobalBe` property |
| `src/PropTraderTools/TradeCopierPanel.cs` | Modified | Row 2 right + Row 3 layout + handlers + helper |
| `src/PropTraderTools/TradeCopierWindow.cs` | Modified | Global toolbar row + handlers |
| `tests/V12_Performance.Tests/Core/CopyEngineTests.cs` | Modified | 6 new `[Fact]` — total >= 186 |

---

## §3 New File: PttGlobalBreakEven.cs

**Path**: `src/PropTraderTools/Features/PttGlobalBreakEven.cs`
**Size**: ~65 lines
**Imports**: `NinjaTrader.Cbi` only. No WPF. No `using CopyEngine` — references `CopyEngine.Instance` directly (same namespace `PropTraderTools`).

### 3.1 Class Declaration

```csharp
// src/PropTraderTools/Features/PttGlobalBreakEven.cs
// B39 -- Global BE All: fires SubmitBeStop for every account x every open position.
// No copy rule required. No armed state. Fires immediately on Execute().
// JS-021: no lock(). JS-023: volatile int ok. JS-002: no return null.
// JS-033: synchronous void only. NT8-003: no volatile double.
// CYC targets: Execute<=5, ExecuteOne<=3, IncrementBuffer=2, DecrementBuffer=2.

using System;
using NinjaTrader.Cbi;

namespace PropTraderTools
{
    internal sealed class PttGlobalBreakEven
    {
```

### 3.2 Fields

```csharp
        // JS-023: volatile int is allowed. NT8-003: volatile double is BANNED -- not used here.
        // _globalBeBuffer: UI-thread only (set from button handlers on dispatcher).
        private volatile int _globalBeBuffer = 0;   // default 0 = exact entry price

        // Test seam: injectable delegate. Default calls internal CopyEngine.Instance.SubmitBeStop.
        // Production: default constructor. Tests: pass fake lambda.
        private readonly Action<Account, Instrument, double> _submitBeStop;
```

### 3.3 Constructors

```csharp
        // Production constructor (used by CopyEngine.GlobalBe property initializer).
        internal PttGlobalBreakEven()
            : this((acc, instr, price) => CopyEngine.Instance.SubmitBeStop(acc, instr, price)) { }

        // Test constructor: caller injects fake Action to count SubmitBeStop invocations.
        internal PttGlobalBreakEven(Action<Account, Instrument, double> submitBeStop)
        {
            _submitBeStop = submitBeStop;
        }
```

**CYC**: default constructor = 1, injection constructor = 1. Both within budget.

### 3.4 Execute — Method Signature and CYC

```csharp
        // Called from Panel OnGlobalBeClick / Window OnWindowGlobalBeClick (UI thread).
        // Iterates Account.All x acc.Positions, skips flat (Quantity==0), calls ExecuteOne per live pos.
        // CYC = 1 + foreach(1) + foreach(1) + if(1) + null-check-or(1) = 5. <= 8. PASS.
        // Spec target was 3-4; +1 from defensive `||` null-guard is intentional (belt-and-suspenders
        // for NT8 Account.All which can yield null entries in sim). CYC=5 is within the absolute <=8
        // budget and is accepted. JS-021: no lock. JS-033: synchronous void. JS-002: no return null.
        internal void Execute(int bufferTicks)
        {
            foreach (var acc in Account.All)
            {
                foreach (var pos in acc.Positions)
                {
                    if (pos == null || pos.Quantity == 0) continue;    // flat -- skip
                    ExecuteOne(acc, pos, bufferTicks);
                }
            }
        }
```

**CYC = 5** (1 base + 2 foreach + 1 if + 1 `||`). Within the absolute <=8 budget. ✅
Spec target was 3-4; the extra point is the defensive `pos == null` guard required for NT8
sim compatibility. Accepted trade-off — documented here per advisory V2 from plan review.

### 3.5 ExecuteOne — Method Signature and CYC

```csharp
        // Direction-aware bePrice calculation. B35 guard inherited from SubmitBeStop.
        // Receives the live Position reference from Execute() -- no NT8 re-lookup needed.
        // pos.Instrument gives the instrument. Tick-aligned via Math.Round / tickSize.
        // CYC = 1 (base) + 1 (if null-or-flat guard) + 1 (|| operator) + 1 (ternary direction) = 4.
        // Spec target was CYC=2 (no re-check). Defensive re-check kept because Execute() passes
        // position references from Account.All which are live NT8 objects that can become null/flat
        // between the outer loop snapshot and this call. CYC=4 <= 8. Accepted. (Advisory V3.)
        // JS-002: early return void (not return null).
        private void ExecuteOne(Account acc, Position pos, int bufferTicks)
        {
            if (pos == null || pos.Quantity == 0) return;              // re-check (defensive)
            bool   isLong   = pos.MarketPosition == MarketPosition.Long;
            double tickSize = pos.Instrument.MasterInstrument?.TickSize ?? 0.25;
            double bePrice  = Math.Round(
                (pos.AveragePrice + (isLong ? bufferTicks : -bufferTicks) * tickSize) / tickSize
            ) * tickSize;
            _submitBeStop(acc, pos.Instrument, bePrice);
        }
```

**CYC = 4** (1 base + 1 if + 1 `||` + 1 ternary direction). Within the absolute <=8 budget. ✅
Spec target was CYC=2; deviation documented per advisory V3 from plan review.

**Note on B35 guard**: `SubmitBeStop` (in CopyEngine) already contains the B35 adversity guard (rejects stop if it would be placed underwater). PttGlobalBreakEven defers to that guard — zero extra code needed here.

### 3.6 Buffer Property and Helpers

```csharp
        // CYC=1 each.
        internal int  GlobalBeBuffer  => _globalBeBuffer;

        // Clamp upper bound at +10. CYC=2 (1 + if).
        internal void IncrementBuffer()
        {
            if (_globalBeBuffer < 10) _globalBeBuffer++;
        }

        // Clamp lower bound at -10. CYC=2 (1 + if).
        internal void DecrementBuffer()
        {
            if (_globalBeBuffer > -10) _globalBeBuffer--;
        }
    }
}
```

---

## §4 CopyEngine.cs Changes

**File**: `src/PropTraderTools/CopyEngine.cs`

### 4.1 Build Tag — Line 41

```csharp
// BEFORE:
internal const string Tag = "PTT-COPIER B38 | trim-anchor-be-tif | 2026-07-28";

// AFTER:
internal const string Tag = "PTT-COPIER B39 | global-be-all | {date}";
```

Replace `{date}` with the actual date at implementation time (e.g. `2026-07-30`).

### 4.2 SubmitBeStop — Accessibility Change

```csharp
// BEFORE (line 1567):
private void SubmitBeStop(Account leaderAcc, Instrument instr, double bePrice)

// AFTER:
internal void SubmitBeStop(Account leaderAcc, Instrument instr, double bePrice)
```

**No logic changes.** Accessibility modifier only. Zero CYC impact.

**Reason**: `PttGlobalBreakEven` is in the same `PropTraderTools` namespace and needs to call
`SubmitBeStop` via `CopyEngine.Instance.SubmitBeStop(...)`. Making it `internal` grants this
access without breaking encapsulation (still invisible outside the assembly).

### 4.3 GlobalBe Property — New (placed near existing singleton block, after Instance property)

```csharp
// B39: Shared PttGlobalBreakEven singleton. Panel and Window both reference this instance
// via CopyEngine.Instance.GlobalBe -- buffer stays in sync across both surfaces (Option A).
// Getter-only auto-property with initializer (C# 6 / .NET 4.8 compliant; NT8-001 NOT triggered
// because this is NOT an init accessor).
internal PttGlobalBreakEven GlobalBe { get; } = new PttGlobalBreakEven();
```

**CYC impact on CopyEngine**: +0. Property is a pure field read.

---

## §5 TradeCopierPanel.cs Changes

**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Method modified**: `BuildBufferedButtonsRow(StackPanel root)`

### 5.1 New Fields (add near other _xxxBtn2 and BrushXxx field declarations)

```csharp
// B39: BE ALL button reference for green-flash update.
private Button _globalBeBtn2;

// B39: Frozen static brush for the purple BE ALL button (JS-008 compliant).
// MakeBrush(r,g,b) calls .Freeze() internally -- same pattern as BrushFlash, BrushInactive.
private static readonly SolidColorBrush BrushPurple = MakeBrush(168, 85, 247);
```

### 5.2 Row 2 — Replace Right Slot (Cancel -> BE ALL cluster)

**Current Row 2 (lines 810-841)**: `UniformGrid Columns=2` — left = Cancel button, right = BE cluster (BE button + ▲▼).
**B39 Row 2**: left = BE cluster (unchanged), right = BE ALL cluster (replaces Cancel).

```csharp
// Row 2: BE cluster | BE ALL cluster  (B39: Cancel moves to Row 3)
var row2 = new UniformGrid { Columns = 2, Margin = new Thickness(0, 2, 0, 2) };

// Col 0: BE cluster (UNCHANGED from current -- same as existing Col 1 code)
// [existing BE cluster code stays here -- no changes to beCluster, beArrows, beUp, beDn, _beBtn2]

// Col 1: BE ALL cluster (NEW -- replaces Cancel)
var globalBeCluster = new DockPanel { LastChildFill = true };
var globalBeArrows = new Grid();
globalBeArrows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
globalBeArrows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
var globalBeUp = new System.Windows.Controls.Primitives.RepeatButton { Content = "\u25B2", Width = 18, Height = 12 };
var globalBeDn = new System.Windows.Controls.Primitives.RepeatButton { Content = "\u25BC", Width = 18, Height = 12 };
globalBeUp.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
globalBeDn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
globalBeUp.Click += OnGlobalBeUp;
globalBeDn.Click += OnGlobalBeDown;
Grid.SetRow(globalBeUp, 0);
Grid.SetRow(globalBeDn, 1);
globalBeArrows.Children.Add(globalBeUp);
globalBeArrows.Children.Add(globalBeDn);
DockPanel.SetDock(globalBeArrows, Dock.Right);

_globalBeBtn2 = new Button
{
    Content         = FormatGlobalBeBuffer("BE ALL", CopyEngine.Instance.GlobalBe.GlobalBeBuffer),
    BorderBrush     = BrushPurple,    // JS-008: frozen static readonly -- MakeBrush(168,85,247)
    Foreground      = BrushPurple,
    BorderThickness = new Thickness(2)
};
_globalBeBtn2.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
_globalBeBtn2.Click += OnGlobalBeClick;
globalBeCluster.Children.Add(globalBeArrows);
globalBeCluster.Children.Add(_globalBeBtn2);
row2.Children.Add(globalBeCluster);

root.Children.Add(row2);
```

**Row 2 summary**: BE cluster (left, unchanged) + BE ALL cluster (right, new).

### 5.3 Row 3 — Replace Full-Width COPY Toggle with UniformGrid Cancel + COPY

**Current Row 3 (lines 843-851)**: `Button _copyToggleBtn2` added directly to root (full-width via DockPanel child).
**B39 Row 3**: `UniformGrid Columns=2` with Cancel (left) + COPY ON/OFF (right), equal width.

```csharp
// Row 3: Cancel (red) | COPY ON/OFF (green) -- equal width via UniformGrid
var row3 = new UniformGrid { Columns = 2, Margin = new Thickness(0, 2, 0, 2) };

// Left: Cancel (moved from Row 2)
_cancelBtn2 = new Button { Content = "Cancel" };
_cancelBtn2.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
_cancelBtn2.Click += OnCancel2;
row3.Children.Add(_cancelBtn2);

// Right: COPY ON/OFF (was full-width, now half-width -- same handler)
_copyToggleBtn2 = new Button { Content = "\u25CF COPY OFF" };
_copyToggleBtn2.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
_copyToggleBtn2.Click += OnCopyToggle;
row3.Children.Add(_copyToggleBtn2);

root.Children.Add(row3);
```

**Note**: The `Background = BrushInactive` on these buttons is removed because `NTButtonStyle`
controls the background. The green/inactive states are driven by `NTButtonStyle` resource
references in the existing `OnCopyToggle` and `OnCancel2` handlers — those handlers are
**unchanged** by B39.

### 5.4 New Event Handlers

```csharp
// B39 -- OnGlobalBeClick: fire BE ALL across all accounts x positions. Green flash 500ms.
// CYC = 3 (1 + null check + timer setup). JS-033: synchronous void.
private void OnGlobalBeClick(object sender, RoutedEventArgs e)
{
    CopyEngine.Instance.GlobalBe.Execute(CopyEngine.Instance.GlobalBe.GlobalBeBuffer);
    if (_globalBeBtn2 == null) return;
    _globalBeBtn2.Background = BrushFlash;                             // green flash
    var t = new System.Windows.Threading.DispatcherTimer
    {
        Interval = TimeSpan.FromMilliseconds(500)
    };
    t.Tick += (s, _) =>
    {
        _globalBeBtn2.ClearValue(Button.BackgroundProperty);           // reset to NTButtonStyle
        t.Stop();
    };
    t.Start();
}

// B39 -- OnGlobalBeUp: increment shared buffer, update label. CYC=1.
private void OnGlobalBeUp(object sender, RoutedEventArgs e)
{
    CopyEngine.Instance.GlobalBe.IncrementBuffer();
    if (_globalBeBtn2 != null)
        _globalBeBtn2.Content = FormatGlobalBeBuffer("BE ALL", CopyEngine.Instance.GlobalBe.GlobalBeBuffer);
}

// B39 -- OnGlobalBeDown: decrement shared buffer, update label. CYC=1.
private void OnGlobalBeDown(object sender, RoutedEventArgs e)
{
    CopyEngine.Instance.GlobalBe.DecrementBuffer();
    if (_globalBeBtn2 != null)
        _globalBeBtn2.Content = FormatGlobalBeBuffer("BE ALL", CopyEngine.Instance.GlobalBe.GlobalBeBuffer);
}
```

### 5.5 New Format Helper

```csharp
// B39 -- FormatGlobalBeBuffer: handles 0 ("BE ALL"), positive ("BE ALL +2"), negative ("BE ALL -3").
// Leaves existing FormatBuffer(string, int) UNCHANGED -- no regression on Trim/Flatten/BE buttons.
// CYC = 3 (1 + if(ticks==0) + if(ticks>0)).
private static string FormatGlobalBeBuffer(string name, int ticks)
{
    if (ticks == 0) return name;
    if (ticks > 0)  return name + " +" + ticks;
    return name + " " + ticks;                    // int.ToString() of negative auto-includes "-"
}
```

**Existing `FormatBuffer` is NOT modified.** It continues to produce `"Trim +1"`, `"BE +2"`, etc.
correctly for its use cases.

---

## §6 TradeCopierWindow.cs Changes

**File**: `src/PropTraderTools/TradeCopierWindow.cs`
**Method modified**: `BuildUI()`

### 6.1 New Fields (add near other private field declarations and WBrushXxx brush fields)

```csharp
// B39: window BE ALL button reference for green-flash update.
private Button _windowGlobalBeBtn;

// B39: Frozen static brushes for the window BE ALL button and green flash (JS-008 compliant).
// MakeWinBrush(r,g,b) calls .Freeze() internally -- same pattern as all existing WBrushXxx fields.
private static readonly SolidColorBrush WBrushPurple = MakeWinBrush(168, 85, 247);
private static readonly SolidColorBrush WBrushFlash  = MakeWinBrush(34, 197, 94);
```

**Note**: If `WBrushFlash` already exists in `TradeCopierWindow.cs` from a prior block, the engineer must NOT add a duplicate. Check for an existing `WBrushFlash` field before adding. The definition above supersedes §6.5 — §6.5 is retained for context only.

### 6.2 Global Toolbar Row — Insertion Point

**Insertion**: After `root.Children.Add(sep1)` (line 202) and BEFORE `DockPanel.SetDock(rulesScroll, Dock.Top)` (line 215).

```csharp
// B39 -- Global toolbar row: BE ALL button + ▲▼ spinners, purple.
// Shared PttGlobalBreakEven instance via CopyEngine.Instance.GlobalBe (Option A).
var globalBeToolbar = new StackPanel
{
    Orientation = Orientation.Horizontal,
    Margin      = new Thickness(6, 2, 6, 2)
};

var windowGlobalBeCluster = new DockPanel { LastChildFill = true };
var windowGlobalBeArrows = new Grid();
windowGlobalBeArrows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
windowGlobalBeArrows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
var wGlobalBeUp = new System.Windows.Controls.Primitives.RepeatButton { Content = "\u25B2", Width = 18, Height = 12 };
var wGlobalBeDn = new System.Windows.Controls.Primitives.RepeatButton { Content = "\u25BC", Width = 18, Height = 12 };
wGlobalBeUp.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
wGlobalBeDn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
wGlobalBeUp.Click += OnWindowGlobalBeUp;
wGlobalBeDn.Click += OnWindowGlobalBeDown;
Grid.SetRow(wGlobalBeUp, 0);
Grid.SetRow(wGlobalBeDn, 1);
windowGlobalBeArrows.Children.Add(wGlobalBeUp);
windowGlobalBeArrows.Children.Add(wGlobalBeDn);
DockPanel.SetDock(windowGlobalBeArrows, Dock.Right);

_windowGlobalBeBtn = new Button
{
    Content         = FormatWindowGlobalBe("BE ALL", CopyEngine.Instance.GlobalBe.GlobalBeBuffer),
    BorderBrush     = WBrushPurple,   // JS-008: frozen static readonly -- MakeWinBrush(168,85,247)
    Foreground      = WBrushPurple,
    BorderThickness = new Thickness(2),
    Padding         = new Thickness(8, 3, 8, 3)
};
_windowGlobalBeBtn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
_windowGlobalBeBtn.Click += OnWindowGlobalBeClick;
windowGlobalBeCluster.Children.Add(windowGlobalBeArrows);
windowGlobalBeCluster.Children.Add(_windowGlobalBeBtn);
globalBeToolbar.Children.Add(windowGlobalBeCluster);

DockPanel.SetDock(globalBeToolbar, Dock.Top);
root.Children.Add(globalBeToolbar);

// [existing rulesScroll continues here -- unchanged]
```

### 6.3 New Event Handlers (Window)

```csharp
// B39 -- OnWindowGlobalBeClick: fire BE ALL. Green flash 500ms. CYC=3.
// JS-033: synchronous void. WBrushFlash = green SolidColorBrush (use existing window brush).
private void OnWindowGlobalBeClick(object sender, RoutedEventArgs e)
{
    CopyEngine.Instance.GlobalBe.Execute(CopyEngine.Instance.GlobalBe.GlobalBeBuffer);
    if (_windowGlobalBeBtn == null) return;
    _windowGlobalBeBtn.Background = WBrushFlash;
    var t = new System.Windows.Threading.DispatcherTimer
    {
        Interval = TimeSpan.FromMilliseconds(500)
    };
    t.Tick += (s, _) =>
    {
        _windowGlobalBeBtn.ClearValue(Button.BackgroundProperty);
        t.Stop();
    };
    t.Start();
}

// B39 -- OnWindowGlobalBeUp: increment shared buffer, update window label. CYC=1.
private void OnWindowGlobalBeUp(object sender, RoutedEventArgs e)
{
    CopyEngine.Instance.GlobalBe.IncrementBuffer();
    if (_windowGlobalBeBtn != null)
        _windowGlobalBeBtn.Content = FormatWindowGlobalBe("BE ALL", CopyEngine.Instance.GlobalBe.GlobalBeBuffer);
}

// B39 -- OnWindowGlobalBeDown: decrement shared buffer, update window label. CYC=1.
private void OnWindowGlobalBeDown(object sender, RoutedEventArgs e)
{
    CopyEngine.Instance.GlobalBe.DecrementBuffer();
    if (_windowGlobalBeBtn != null)
        _windowGlobalBeBtn.Content = FormatWindowGlobalBe("BE ALL", CopyEngine.Instance.GlobalBe.GlobalBeBuffer);
}
```

### 6.4 New Format Helper (Window)

```csharp
// B39 -- Same logic as Panel FormatGlobalBeBuffer. Duplicated intentionally to avoid
// cross-file coupling between Panel and Window. CYC=3.
private static string FormatWindowGlobalBe(string name, int ticks)
{
    if (ticks == 0) return name;
    if (ticks > 0)  return name + " +" + ticks;
    return name + " " + ticks;
}
```

### 6.5 WBrushFlash Reference

`WBrushFlash` is declared in §6.1 as a frozen static readonly field using `MakeWinBrush(34, 197, 94)`
(green `#22c55e`). The inline `new SolidColorBrush(...)` form shown in the original plan draft was
**non-compliant with JS-008** and has been replaced.

```csharp
// CORRECT (JS-008 compliant) — declared once in §6.1 fields, used here by reference:
private static readonly SolidColorBrush WBrushFlash = MakeWinBrush(34, 197, 94);

// BANNED (JS-008 violation) — never use inline new SolidColorBrush without Freeze():
// private static readonly SolidColorBrush WBrushFlash =
//     new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e));   // NOT Frozen -- BANNED
```

If `WBrushFlash` already exists from a prior block, do NOT add a duplicate — verify first.

---

## §7 Shared Instance Design — Option A Confirmed

Panel and Window both reference `CopyEngine.Instance.GlobalBe`. This is the recommended Option A
from the spec.

**Why Option A is correct:**
- Matches the "Live Map Pillar": toggle Copy ON on Panel → Window updates simultaneously
- BE ALL buffer is a session-scoped setting. Adjusting on one surface should be visible on the other
- No event/pub-sub required: each spinner handler reads `CopyEngine.Instance.GlobalBe.GlobalBeBuffer`
  directly after the increment/decrement; the label is updated locally per-surface
- The buffer itself is shared, so if Panel shows "BE ALL +2" the next time Window reads it, it also shows "+2"

**Buffer sync without an event**: Both surfaces update their own label in their own handler after
calling `CopyEngine.Instance.GlobalBe.IncrementBuffer()`. If the user presses ▲ on Panel,
Window label doesn't auto-update until the user interacts with the Window spinner. This is
acceptable — the shared buffer ensures that when BE ALL fires from either surface, the same
buffer is used. Visual sync is best-effort.

---

## §8 FormatBuffer — Existing vs New

| Helper | Where | Behavior | Status |
|---|---|---|---|
| `FormatBuffer(string, int)` | TradeCopierPanel.cs line 856 | Always `name + " +" + ticks` | **UNCHANGED** |
| `FormatGlobalBeBuffer(string, int)` | TradeCopierPanel.cs | `name` / `name +" +" + ticks` / `name + " " + ticks` | **NEW** |
| `FormatWindowGlobalBe(string, int)` | TradeCopierWindow.cs | Same logic | **NEW** |

The existing `FormatBuffer` is intentionally left producing `"Trim +1"` / `"BE +2"` etc. for
Trim, Flatten, and per-chart BE buttons. The buffer for those buttons starts at +1 by default
and is never 0 in normal use. The new `FormatGlobalBeBuffer` handles the 0 case for BE ALL.

---

## §9 Test Seam Architecture

**Key**: `PttGlobalBreakEven` accepts an injected `Action<Account, Instrument, double>` for the
`SubmitBeStop` call. Tests use the injection constructor; production uses the default constructor.

**Test pattern for T_B39_01 through T_B39_06:**

```csharp
// Arrange
int callCount = 0;
var calls = new List<(Account, Instrument, double)>();
Action<Account, Instrument, double> fakeSink = (acc, instr, price) =>
{
    callCount++;
    calls.Add((acc, instr, price));
};
var globalBe = new PttGlobalBreakEven(fakeSink);

// Act
// [build fake Account.All scenario using test stubs]
globalBe.Execute(bufferTicks: 0);

// Assert
Assert.Equal(expectedCallCount, callCount);
```

**Note on Account.All in tests**: `Account.All` in NT8 is a static collection that requires
the NT8 runtime. Tests that need to exercise the `foreach Account.All` loop will need to verify
behavior via the NT8 simulator or use the existing test infrastructure in `CopyEngineTests.cs`.
Looking at the existing test patterns (B32-B38 had 180 tests), the test infrastructure likely
provides a way to inject accounts or uses the real NT8 Sim connection.

If Account.All cannot be injected, the test seam must be at the Execute() level. An alternative
is to make Execute() take an `IEnumerable<Account>` parameter:

```csharp
// Production call:
globalBe.Execute(Account.All, bufferTicks);

// Test call:
globalBe.Execute(fakeAccounts, bufferTicks);
```

**Recommended**: Use `IEnumerable<Account>` overload OR check existing test infrastructure.
Engineer must verify which seam is compatible with existing test patterns before implementing.

**InternalsVisibleTo**: The test assembly (`V12_Performance.Tests`) must have access to internal
members of `PropTraderTools`. Verify that
`[assembly: InternalsVisibleTo("V12_Performance.Tests")]` exists in PropTraderTools
`AssemblyInfo.cs` or `.csproj`. If not, the engineer must add it.

---

## §10 Required Tests — T_B39_01 through T_B39_06

All tests use `xUnit` `[Fact]` attribute. No `NUnit`, no `MSTest`.

| ID | Method Name | Assert |
|---|---|---|
| T_B39_01 | `GlobalBe_FiresOnAllAccountsAllInstruments` | 3 accounts, 2 instruments (MES+NQ), all in position -> SubmitBeStop called 6x. Each call uses that account's own AveragePrice. |
| T_B39_02 | `GlobalBe_SkipsFlatAccounts` | 1 account in position, 1 account flat (Quantity=0) -> SubmitBeStop called 1x only. |
| T_B39_03 | `GlobalBe_WorksWithNoCopyRule` | CopyEngine has zero rules -> Execute() still fires SubmitBeStop (no FindRule() dependency). |
| T_B39_04 | `GlobalBe_B35GuardInherited_UnderwaterSkipped` | Underwater long (stop would be above ask) -> SubmitBeStop called; guard inside emits WARNING; no exception; loop continues. |
| T_B39_05 | `GlobalBe_BufferAppliedPerDirectionCorrectly` | buffer=+2, long pos avgEntry=7500, tick=0.25 -> bePrice=7500.50. buffer=+2, short pos avgEntry=7500 -> bePrice=7499.50. |
| T_B39_06 | `GlobalBe_AllAccountsFlat_NoCalls` | All accounts flat (Quantity=0) -> SubmitBeStop called 0 times, no exception. |

**Total `[Fact]` target**: >= 186 (entering B39 at ~180; +6 = 186 minimum).

---

## §11 7-Scan Checklist (per ticket)

Each ticket must report the result of all 7 scans before `BUILD_PASS` can be claimed.

| Scan | Command | Required Result |
|---|---|---|
| SCAN-01 | `grep -r "lock(" src/ --include="*.cs"` | 0 matches in new/modified files |
| SCAN-02 | `grep -r "async void" src/ --include="*.cs"` | 0 matches in new/modified files |
| SCAN-03 | `grep -r "return null" src/ --include="*.cs"` | 0 matches in new code |
| SCAN-04 | `grep -r "throw new" src/ --include="*.cs"` | 0 matches in new code |
| SCAN-05 | `python scripts/complexity_audit.py` | All new methods CYC <= 8 |
| SCAN-06 | `dotnet build` | 0 errors, 0 new warnings |
| SCAN-07 | `dotnet test` | All [Fact] pass; count >= 186 |

---

## §12 Ticket Split Recommendation

### Ticket T1 — Source Code

**Files**: PttGlobalBreakEven.cs (NEW), CopyEngine.cs (3 edits), TradeCopierPanel.cs (rows 2+3 + handlers), TradeCopierWindow.cs (toolbar row + handlers)

**Spec requirements satisfied**:
- New file PttGlobalBreakEven with Execute/ExecuteOne/IncrementBuffer/DecrementBuffer/GlobalBeBuffer
- SubmitBeStop private->internal
- CopyEngine.GlobalBe singleton property
- Panel Row 2: BE cluster (left) + BE ALL cluster (right, purple)
- Panel Row 3: Cancel (half-width) + COPY ON/OFF (half-width) via UniformGrid
- Window global toolbar row above rulesScroll
- Green flash 500ms on both surfaces
- FormatGlobalBeBuffer helper for 0/+/- formatting
- Build tag updated

**Scans**: All 7 must pass. SCAN-06 (`dotnet build`) is the gate before T2 starts.

### Ticket T2 — Tests

**File**: CopyEngineTests.cs (+6 [Fact])

**Tests**: T_B39_01 through T_B39_06 as specified in §10.

**Dependency**: T1 must reach SCAN-06 PASS before T2 begins (tests require compilable source).

---

## §13 Deferred Items / Out of Scope for B39

The following items are NOT in B39 scope and are explicitly deferred:

| Item | Reason / Future block |
|---|---|
| Keyboard shortcut (Shift+G) for BE ALL | Spec explicitly defers to follow-on ticket |
| `PttBus.GlobalBeFired` pub-sub event | Not needed in B39 — copier fan-out not the target |
| Armed state for global BE | Spec says "fires immediately, no armed state" |
| Visual buffer sync between Panel and Window (auto-label refresh) | Best-effort; acceptable per §7 |
| Independent BE buffer per rule-row in Window | Out of scope for B39 |
| `[assembly: InternalsVisibleTo(...)]` if missing | Engineer must verify; add if absent (not a B39 code feature) |

---

## §14 Compliance Sign-Off

| Rule | Status | Note |
|---|---|---|
| JS-021 no lock() | PASS | PttGlobalBreakEven uses no lock; all state on UI thread |
| JS-023 volatile int ok | PASS | `_globalBeBuffer` is `volatile int` |
| JS-023 volatile double BANNED | PASS | No volatile double in new code |
| JS-002 no return null | PASS | Uses `return` (void) and `continue`, never `return null` |
| JS-033 no async void | PASS | All handlers are synchronous void |
| NT8-003 no volatile double | PASS | Covered by JS-023 check |
| NT8-001 no `{ get; init; }` | PASS | `GlobalBe` uses `{ get; }` getter-only (C# 6 compliant) |
| NT8-007 CreateOrder arg12 | PASS | PttGlobalBreakEven does not call CreateOrder directly |
| CYC <= 8 (all new methods) | PASS | Execute=5, ExecuteOne=4, IncrementBuffer=2, DecrementBuffer=2, handlers<=3 |
| ASCII-only identifiers | PASS | All identifiers and string literals are ASCII |
| No FontFamily strings | PASS | No FontFamily usage in new code |
| No DateTime.Now | PASS | Not used in new code |
| JS-008 SolidColorBrush Freeze() | PASS | All brushes use `MakeBrush`/`MakeWinBrush` frozen static readonly fields: `BrushPurple`, `WBrushPurple`, `WBrushFlash`. No inline `new SolidColorBrush(...)` in new code. |
| No hardcoded hex (C# identity) | PASS | Colors use `Color.FromRgb()` WPF API via MakeBrush/MakeWinBrush, not string literals |
| "PTT-" prefixed order names | PASS | SubmitBeStop (in CopyEngine) already uses PTT- prefix |

---

## §15 Build Tag Target

```csharp
internal const string Tag = "PTT-COPIER B39 | global-be-all | 2026-07-30";
```

(Engineer substitutes the actual implementation date at time of coding.)
