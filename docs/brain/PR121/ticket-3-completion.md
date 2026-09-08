# PR-121 Ticket 3 Completion Report

**Status**: BUILD_PASS  
**Files Modified**:
- `C:\WSGTA\ptt-host\src\PropTraderTools\TradeCopierAddOn.cs`
- `C:\WSGTA\ptt-host\src\PropTraderTools\TradeCopierWindow.cs`

---

## Rules Catalog Gate

SCAN: `docs/standards/jane-street/RULES_CATALOG.md` confirmed UTF-8 clean and readable.

P0 check on modified files:
- JS-021 (`lock(`): 0 hits
- JS-033 (`async void`): 0 hits
- JS-002 (`return null` hot path): 0 new instances

**GATE RESULT: PASS**

---

## 5 Fixes Applied

### FIX 1 — S4/C9 (TradeCopierAddOn.cs): Cleanup on WireNewPanel injection failure

**Before** (`WireNewPanel` failure path, lines ~519-523):
```csharp
if (InjectPanelIntoGrid(grid, panel))
{
    _panels[chart] = panel;
    return;
}
MessageBox.Show("PTT: ChartTrader.Content is not a Grid...", "PTT Info");
```

**After**:
```csharp
if (InjectPanelIntoGrid(grid, panel))
{
    _panels[chart] = panel;
    return;
}

// Cleanup: resources were wired but panel was never tracked -- release them.
StopAtrEngine(chart);
UnhookKeyShortcut(chart);
MessageBox.Show("PTT: ChartTrader.Content is not a Grid...", "PTT Info");
```

**Rationale**: ATR engine and keyboard shortcut were wired before `InjectPanelIntoGrid`. On failure, those resources had no cleanup path. The panel was never added to `_panels` so `OnWindowDestroyed` would never clean them up.

---

### FIX 2 — S6/C3 (TradeCopierWindow.cs): LicenseClient.Validate outside try/catch in OnActivateClick

**Before** (lines ~413-428):
```csharp
private void OnActivateClick(object sender, RoutedEventArgs e)
{
    string key = _licenseKeyBox?.Text?.Trim() ?? string.Empty;
    try
    {
        System.IO.Directory.CreateDirectory(...);
        System.IO.File.WriteAllText(LicenseTxtPath, key);
    }
    catch (Exception) { }
    var flags = LicenseClient.Validate(key);      // OUTSIDE try/catch
    CopyEngine.Instance.SetFlags(flags);
    ApplyFeatureFlags(flags);
    _licenseStatusText.Text = GetStatusText(flags);
}
```

**After**:
```csharp
private void OnActivateClick(object sender, RoutedEventArgs e)
{
    string key = _licenseKeyBox?.Text?.Trim() ?? string.Empty;
    try
    {
        System.IO.Directory.CreateDirectory(...);
        System.IO.File.WriteAllText(LicenseTxtPath, key);
    }
    catch (Exception) { }
    try
    {
        var flags = LicenseClient.Validate(key);
        CopyEngine.Instance.SetFlags(flags);
        ApplyFeatureFlags(flags);
        _licenseStatusText.Text = GetStatusText(flags);
    }
    catch (Exception ex)
    {
        if (_licenseStatusText != null)
            _licenseStatusText.Text = "Validation error";
        MessageBox.Show("PTT license error:\n\n" + ex.Message, "Trade Copier");
    }
}
```

**Rationale**: Validation exception would have escaped the WPF event handler entirely. Now contained.

---

### FIX 3 — C7 (TradeCopierAddOn.cs): UpdateAtrOverlay dispatches to wrong panel with multi-chart

**Before**:
```csharp
internal void UpdateAtrOverlay(string atrDisplay)
{
    var panel = _panels.Values.FirstOrDefault();
    if (panel == null) return;
    ...
}
private void OnAtrUpdated(string display) { UpdateAtrOverlay(display); }
// StartAtrEngine: engine.AtrUpdated += OnAtrUpdated;
// StopAtrEngine:  engine.AtrUpdated -= OnAtrUpdated;
```

**After**:
```csharp
internal void UpdateAtrOverlay(Chart chart, string atrDisplay)
{
    TradeCopierPanel panel;
    if (!_panels.TryGetValue(chart, out panel) || panel == null)
        return;
    System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        panel.SetAtrText(atrDisplay)
    );
}
// StartAtrEngine: captured lambda -- var capturedChart = chart;
//                engine.AtrUpdated += (display) => UpdateAtrOverlay(capturedChart, display);
// StopAtrEngine:  engine removed from _atrEngines; lambda becomes no-op (TryGetValue returns false).
//                OnAtrUpdated method removed entirely.
```

**Rationale**: `FirstOrDefault()` always dispatched to first panel in dictionary enumerate order. With multiple charts, wrong panels received ATR text updates. Lambda closure captures the correct `Chart` key for each engine.

---

### FIX 4 — C8 (TradeCopierAddOn.cs): RemoveStalePanelChild skips RowDefinition removal for row 0

**Before** (line ~419):
```csharp
if (staleRow > 0 && staleRow < grid.RowDefinitions.Count)
    grid.RowDefinitions.RemoveAt(staleRow);
```

**After**:
```csharp
if (staleRow >= 0 && staleRow < grid.RowDefinitions.Count)
    grid.RowDefinitions.RemoveAt(staleRow);
```

**Rationale**: Stale panel at row 0 had its `Children` entry removed but its empty `RowDefinition` was left in the grid. `>= 0` covers all valid row indices.

---

### FIX 5 — C10/R1 (TradeCopierAddOn.cs): Remove dev_mode.txt bypass entirely

**Before** (lines ~705-707 in `LoadAndValidateLicense`):
```csharp
var devMode = System.IO.Path.Combine(pttDir, "dev_mode.txt");
if (System.IO.File.Exists(devMode))
    return FeatureFlags.Elite();
```

**After**: Block deleted. `LoadAndValidateLicense` now reads `license.txt`, calls `LicenseClient.Validate(key)`, returns the result. No dev_mode.txt path, no early Elite() return. Comment updated to remove reference to bypass.

**Rationale**: Debug sentinel left in production. Any machine with a `dev_mode.txt` in the PTT data dir received unlimited Elite tier without a valid license.

---

## Build Output

```
dotnet build C:\WSGTA\ptt-host\Linting.csproj
0 Error(s)
Warnings: pre-existing StyleCop/CS warnings in V12_002.*.cs and SignalBroadcaster.cs
          (unrelated to this ticket -- not introduced by these changes)
```

**Build result: 0 errors**

---

## 7-Scan Results

| Scan | Command | Result |
|------|---------|--------|
| SCAN-01 | `Select-String "lock\s*\("` in TradeCopierAddOn.cs | **0 hits** |
| SCAN-02 | `Select-String "async void "` in TradeCopierAddOn.cs | **0 hits** |
| SCAN-03 | `Select-String "dev_mode"` in TradeCopierAddOn.cs | **0 hits** |
| SCAN-04 | CCN scan `lizard --CCN 8` on PropTraderTools/ | **0 methods > CCN 8** |
| SCAN-05 | Build | **0 errors** |
| SCAN-06 | `Select-String "FirstOrDefault"` in UpdateAtrOverlay | **0 hits in UpdateAtrOverlay** (2 remaining hits are in WireLeaderAccount — unrelated, pre-existing) |
| SCAN-07 | `Select-String "staleRow > 0"` in TradeCopierAddOn.cs | **0 hits** |

---

## CCN Scan Detail

```
lizard C:\WSGTA\ptt-host\src\PropTraderTools --CCN 8

No thresholds exceeded (cyclomatic_complexity > 8 or length > 1000 or nloc > 1000000 or parameter_count > 100)
Warning cnt: 0
```

All methods in both files pass CCN <= 8. Highest CCN in modified code:
- `OnActivateClick` (TradeCopierWindow.cs): CCN=7 (2 try/catch paths)
- `ApplyMirrorModeFlag` (TradeCopierWindow.cs): CCN=8 (pre-existing, at limit)

---

## Return Status: BUILD_PASS
