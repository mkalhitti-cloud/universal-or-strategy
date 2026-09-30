# PTT-COPIER-B7 -- Ticket T2 Completion Report
# Phase 5 output. Written by v12-engineer after implementation.
# Ticket: T2 -- UI: Button Color Coding + ScrollViewer (P2)
# Status: COMPLETE

---

## What Was Implemented

### Files Modified

| File | Lines Before | Lines After | Change Type |
|------|-------------|------------|-------------|
| `TradeCopierPanel.cs` | 383 | 413 | +30 lines |
| `TradeCopierWindow.cs` | 444 | 490 | +46 lines |

---

### TradeCopierPanel.cs Changes

1. **Brush fields (V08 canonical RGB)**
   Added 4 `private static readonly SolidColorBrush` fields at class level:
   - `BrushActive   = MakeBrush( 34, 197,  94)` — green #22c55e (Copy ON, BE when live)
   - `BrushDanger   = MakeBrush(239,  68,  68)` — red #ef4444 (Flatten/Cancel when live)
   - `BrushCaution  = MakeBrush(245, 158,  11)` — amber #f59e0b (Trim when live)
   - `BrushInactive = MakeBrush( 55,  65,  81)` — grey #4b5563 (all buttons when no target)
   All frozen via existing `MakeBrush()` helper (JS-008). No duplicate `MakeBrush` added.

2. **Button reference fields**
   Added `_flattenBtn`, `_cancelBtn`, `_trimBtn`, `_beBtn` as class-level `Button` fields
   (previously local vars in `BuildUI`). Required for `UpdateButtonColors` to reference them.

3. **`UpdateButtonColors(bool hasPosition, bool hasEntries)` (NEW, CYC=5)**
   Sets all 5 button backgrounds via 5 ternary expressions. Called only on UI thread
   via `Dispatcher.InvokeAsync` from `OnPositionStateChanged`.

4. **`OnPositionStateChanged(string instr, PositionState state)` (NEW, CYC=1)**
   Filters by `_instrument.FullName == instr`, then marshals `UpdateButtonColors` via
   `Dispatcher.InvokeAsync`. `state` is a value type captured safely in closure (JS-003).

5. **`BuildUI()` modifications**
   - `_copyToggleBtn`: `Background = BrushInactive`, removed `SetResourceReference("NTButtonStyle")`
   - `_trimBtn`, `_flattenBtn`, `_cancelBtn`, `_beBtn`: `Background = BrushInactive`, removed NTButtonStyle
   - End of `BuildUI()`: `UpdateButtonColors(false, false)` (V04 initial state)

6. **`OnToggle()` modification**
   Added: `_copyToggleBtn.Background = _copyEnabled ? BrushActive : BrushInactive;`

7. **`OnLoaded()` modification**
   Added: `_engine.PositionStateChanged += OnPositionStateChanged;`

8. **`Detach()` modification**
   Added: `_engine.PositionStateChanged -= OnPositionStateChanged;`

---

### TradeCopierWindow.cs Changes

1. **`MakeWinBrush(byte r, byte g, byte b)` static helper (NEW, CYC=1)**
   Identical pattern to `MakeBrush` in Panel. "Win" prefix avoids base-class name collision.
   Calls `brush.Freeze()` before return (JS-008).

2. **Brush fields (V08 canonical RGB)**
   Added 4 `private static readonly SolidColorBrush` fields:
   - `WBrushActive   = MakeWinBrush( 34, 197,  94)`
   - `WBrushDanger   = MakeWinBrush(239,  68,  68)`
   - `WBrushCaution  = MakeWinBrush(245, 158,  11)`
   - `WBrushInactive = MakeWinBrush( 55,  65,  81)`

3. **Per-rule button tracking lists (Engineer Note #3)**
   Added 4 `private readonly List<Button>` class-level fields:
   `_flattenBtns`, `_cancelBtns`, `_trimBtns`, `_beBtns`

4. **`UpdateButtonColors(bool hasPosition, bool hasEntries)` (NEW, CYC=5)**
   Updates `_globalToggleBtn` and iterates all 4 button lists to set backgrounds.

5. **`OnPositionStateChanged(string instr, PositionState state)` (NEW, CYC=1)**
   Simple null guard on `instr`, then `Dispatcher.InvokeAsync(() => UpdateButtonColors(...))`.
   Window has no per-instrument filter (intentional asymmetry with Panel per plan).

6. **`OnWindowClosed(object sender, EventArgs e)` (NEW, CYC=1)**
   Unsubscribes `PositionStateChanged` on Window close to prevent memory leaks / ghost callbacks.

7. **Constructor modification**
   Added: `Closed += OnWindowClosed;`

8. **`OnLoaded()` modification**
   Added: `_engine.PositionStateChanged += OnPositionStateChanged;`

9. **`BuildRuleRow()` modifications**
   - `trimBtn`, `flattenBtn`, `cancelBtn`, `beBtn`: `Background = WBrushInactive` + appended to tracking lists
   - `toggleBtn`: `Background = WBrushActive` (starts [ON], active color)
   - Removed `SetResourceReference("NTButtonStyle")` from color-coded buttons

10. **`BuildDynamicRuleRow()` modifications**
    Identical changes as `BuildRuleRow()` -- same button initialization + list appends.

11. **`BuildUI()` modifications**
    - `_globalToggleBtn`: `Background = WBrushInactive`, removed `SetResourceReference("NTButtonStyle")`
    - B7-F5 ScrollViewer wrap:
      ```csharp
      var rulesScroll = new ScrollViewer
      {
          VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
          MaxHeight = 400,
          Content   = _rulesPanel
      };
      DockPanel.SetDock(rulesScroll, Dock.Top);
      root.Children.Add(rulesScroll);
      ```
      `DockPanel.SetDock` applied to `rulesScroll` (outer wrapper), not `_rulesPanel`.
      `_rulesPanel` field unchanged (StackPanel). `OnAddRule` appends to `_rulesPanel.Children` -- works correctly.
    - End of `BuildUI()`: `UpdateButtonColors(false, false)` (V04 initial state)

12. **`OnGlobalToggle()` modification**
    Added: `_globalToggleBtn.Background = _copyEnabled ? WBrushActive : WBrushInactive;`

13. **`OnRuleToggle()` modification**
    Added: `btn.Background = newState ? WBrushActive : WBrushInactive;`

---

## Deviations from Plan

None. Implementation follows plan exactly.

---

## 7-Scan Results (both files)

Scans executed against:
- `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs`
- `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierWindow.cs`

| Scan | Pattern | Result | Count |
|------|---------|--------|-------|
| SCAN-01 | `lock(` | **PASS** | 0 |
| SCAN-02 | Non-ASCII chars | **PASS** | 0 |
| SCAN-03 | `FontFamily` | **PASS** | 0 |
| SCAN-04 | `"#[0-9A-Fa-f]{6}"` hex string literals | **PASS** | 0 |
| SCAN-05 | `CreateOrder` without `PTT-` | **PASS** | 0 (no CreateOrder in UI files) |
| SCAN-06 | `DateTime.Now[^U]` | **PASS** | 0 |
| SCAN-07 | `class TradeCopierWindow` (declaration unchanged) | **PASS** | `public class TradeCopierWindow : Window` -- no `sealed` added |

All 7 scans: **0 violations**.

---

## CYC Summary (new methods)

| Method | File | CYC | Status |
|--------|------|-----|--------|
| `UpdateButtonColors` | TradeCopierPanel.cs | 5 | <= 8 ✅ |
| `OnPositionStateChanged` | TradeCopierPanel.cs | 1 | <= 8 ✅ |
| `MakeWinBrush` | TradeCopierWindow.cs | 1 | <= 8 ✅ |
| `UpdateButtonColors` | TradeCopierWindow.cs | 5 | <= 8 ✅ |
| `OnPositionStateChanged` | TradeCopierWindow.cs | 1 | <= 8 ✅ |
| `OnWindowClosed` | TradeCopierWindow.cs | 1 | <= 8 ✅ |

All new methods CYC <= 8. ✅

---

## Jane Street Rules Satisfied

| Rule | Satisfaction |
|------|-------------|
| JS-008 (brushes Frozen) | All brushes via `MakeBrush(r,g,b)` / `MakeWinBrush(r,g,b)` which call `brush.Freeze()`. `static readonly` = single allocation. |
| JS-021 (no lock) | No `lock` keyword. All button updates on UI thread only. `List<Button>` fields accessed exclusively on UI thread. |
| JS-023 (Dispatcher.InvokeAsync) | Both `OnPositionStateChanged` implementations marshal via `Dispatcher.InvokeAsync`. Never set Background directly on event thread. |
| JS-003 (readonly struct value capture) | `PositionState` is a `readonly struct` -- captured by value in lambda closure. No reference aliasing. |
| SCAN-04 (no hex literals) | All brush RGB values are integer triples via `MakeBrush`/`MakeWinBrush`. Hex values appear only in comments (not string literals). |
| NT8: no NTButtonStyle on color-coded buttons | Removed `SetResourceReference("NTButtonStyle")` from all color-coded buttons. NT8 ControlTemplate would override `Background`. |
| NT8: TradeCopierWindow not sealed | Class declaration `public class TradeCopierWindow : Window` unchanged. No `sealed` keyword added. |

---

## Verification Criteria Status

| Criterion | Status |
|-----------|--------|
| All 7 SCAN counts = 0 on both UI files | ✅ PASS |
| No `lock()` anywhere | ✅ PASS |
| No hex string literals | ✅ PASS |
| Canonical RGB: (34,197,94) / (239,68,68) / (245,158,11) / (55,65,81) | ✅ PASS |
| `_flattenBtns`, `_cancelBtns`, `_trimBtns`, `_beBtns` tracking lists in Window | ✅ PASS |
| Buttons appended to lists in both `BuildRuleRow` and `BuildDynamicRuleRow` | ✅ PASS |
| `UpdateButtonColors(false, false)` called at end of `BuildUI()` on both surfaces | ✅ PASS |
| `PositionStateChanged` subscribed in `OnLoaded` on both surfaces | ✅ PASS |
| `PositionStateChanged` unsubscribed in `Detach()` (Panel) and `OnWindowClosed` (Window) | ✅ PASS |
| ScrollViewer wrapping `_rulesPanel` with `MaxHeight=400` in Window | ✅ PASS |
| `DockPanel.SetDock` on ScrollViewer (outer wrapper), not StackPanel | ✅ PASS |
| `OnToggle()` / `OnGlobalToggle()` / `OnRuleToggle()` brush update after state flip | ✅ PASS |
| No `FontFamily` | ✅ PASS |
| NT8 F5 compilation: pending manual verification | MANUAL GATE |

---

## Pending

- Manual NT8 F5 compilation gate (requires NinjaTrader to load Add-On)
- Manual Layer 3 live state test (open position → buttons activate; close → buttons grey)
- `deploy-sync.ps1` execution after commit (hard-link sync)
