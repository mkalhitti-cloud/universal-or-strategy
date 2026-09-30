# B172 Ticket 2 Completion — UI Layer + AtrWidget Build

**Epic:** B172  
**Ticket:** T2 — UI Layer + AtrWidget Build  
**Engineer:** ptt-engineer  
**Status:** BUILD_PASS  

---

## Changes Made

### TradeCopierAddOn.cs

**Fields DELETED:**
- `_atrEngines` — `private static readonly ConcurrentDictionary<Chart, AtrSizingEngine>` (lines 43–45 + comment)
- `_atrPollTimer` — `private DispatcherTimer` (lines 64–66 + comment)

**Methods DELETED:**
- `StartAtrEngine(Chart chart, NinjaTrader.Cbi.Instrument instr)` — entire method + comments (lines 207–249)
- `StopAtrEngine(Chart chart)` — entire method + comment (lines 251–263)
- `UpdateAtrOverlay(string atrDisplay)` — entire method + comment (lines 265–276)
- `OnAtrUpdated(string display)` — entire method + comment (lines 278–284)

**Call sites MODIFIED:**
- `OnWindowDestroyed()` line 108: removed `StopAtrEngine(chart);` call
- `OnWindowCreated()` line 100: cleaned stale comment "StartAtrEngine needs _atrOverlayLabel"
- `InjectIntoChart()` comment: updated from "chains to DoInject which calls StartAtrEngine (instance)" to "chains to DoInject which calls WireNewPanel"
- `WireNewPanel()` line ~484: replaced `StartAtrEngine(chart, instr);` with:
  ```csharp
  var engine = new AtrSizingEngine();
  panel.SetAtrProvider(engine);
  ```

---

### TradeCopierPanel.cs

**using directive ADDED:**
- `using System.Windows.Threading;` — required for `DispatcherTimer` and `DispatcherPriority` in `AtrWidget`

**Fields DELETED:**
- `_atrRow` — `private UniformGrid` (line 275 + comment)
- `_maxRiskDollars` — `private double` (line 296 + comment)
- `_atrFraction` (Panel-level) — `private double` (line 297)
- `_riskDollarsBox` — `private TextBox` (line 298)
- `_atrFractionBox` (Panel-level) — `private TextBox` (line 299)
- `_atrDisplayLabel` — `private TextBlock` (lines 301–302 + comment)

**Field ADDED:**
- `private AtrWidget _widget = null;` — B172 AtrWidget inner class instance

**Methods DELETED:**
- `BuildRiskAtrRow(StackPanel root)` — entire method + comments
- `BuildSpinnerColumn(string, TextBox, RoutedEventHandler, RoutedEventHandler)` — entire method + comments
- `BuildAtrDisplayRow()` — entire method + comments
- `SetAtrText(string display)` — entire method + comments
- `OnRiskUp(object, RoutedEventArgs)` — entire method + comment
- `OnRiskDown(object, RoutedEventArgs)` — entire method + comment
- `OnRiskTextLostFocus(object, RoutedEventArgs)` — entire method + comment
- `OnAtrFractionUp(object, RoutedEventArgs)` — entire method + comment
- `OnAtrFractionDown(object, RoutedEventArgs)` — entire method + comment
- `OnAtrFractionTextLostFocus(object, RoutedEventArgs)` — entire method + comment
- `NotifyRiskChanged()` — no-op stub deleted
- `NotifyAtrFractionChanged()` — no-op stub deleted

**`TryParseAndClamp` KEPT** (used by `AtrWidget.OnAtrFractionLostFocus`)

**`OnLoaded()` MODIFIED:** Removed `NotifyRiskChanged()` and `NotifyAtrFractionChanged()` calls (lines 803–804)

**`BuildUI()` MODIFIED:** Replaced `BuildRiskAtrRow(_contentPanel)` call with:
```csharp
_widget = new AtrWidget();
_widget.BuildAtrWidgetRows(_contentPanel); // B172: ATR fraction display widget
```

**`Detach()` MODIFIED:** Added `_widget?.Detach();` at start of method body

**`ApplyRowVisibilityFlags()` MODIFIED:** `_atrRow` visibility block deleted; method now contains only `_clickTraderRow` block. CYC reduced from 4 to 2.

**Method ADDED on TradeCopierPanel:**
```csharp
internal void SetAtrProvider(AtrSizingEngine engine)
{
    _widget?.SetProvider(engine);
}
```

**File header comment UPDATED:** B12-T3 header block replaced with B172-T2 summary

**`AtrWidget` INNER CLASS ADDED** — `private sealed class AtrWidget` with:
- Fields: `_atrFraction (double=0.75)`, `_atrFractionBox (TextBox)`, `_atrResultLabel (TextBlock)`, `_lastAtr (double)`, `_atrPollTimer (DispatcherTimer)`, `_hasData (volatile bool)`, `_provider (AtrSizingEngine)`
- Methods: `BuildAtrWidgetRows(StackPanel)`, `SetProvider(AtrSizingEngine)`, `Detach()`, `OnTimerTick(object, EventArgs)`, `RecalcDisplay()`, `OnAtrFractionLostFocus(object, RoutedEventArgs)`

---

## 7-Scan Results

### SCAN-01: lock() check
```
Command: Select-String -Path src/PropTraderTools/*.cs -Pattern "lock\("
Result: 0 actual lock() calls (all matches were comments containing "no lock()")
```
**PASS — 0 actual lock() calls**

### SCAN-02: Deleted symbol grep
```
Command: Select-String -Path src/PropTraderTools/*.cs -Pattern "StartAtrEngine|StopAtrEngine|UpdateAtrOverlay|OnAtrUpdated|_atrEngines|BuildRiskAtrRow|BuildAtrDisplayRow|SetAtrText|OnRiskUp|OnRiskDown|OnRiskTextLostFocus|NotifyRiskChanged|NotifyAtrFractionChanged|_maxRiskDollars|_riskDollarsBox|_atrDisplayLabel|\b_atrRow\b|BuildSpinnerColumn"
Result: (no output)
```
**PASS — 0 matches**

Note: `_atrPollTimer` in `AtrWidget` is the new widget-level field (plan Section 6); AddOn-level `_atrPollTimer` is confirmed deleted. AtrSizingEngine type name legitimately appears in new `SetAtrProvider` + `AtrWidget._provider` field per ticket spec.

### SCAN-03: volatile double check
```
Command: Select-String -Path src/PropTraderTools/*.cs -Pattern "volatile double"
Result: 2 matches — both in comments ("no volatile double"), zero field declarations
```
**PASS — 0 volatile double field declarations**

### SCAN-04: Non-ASCII check
```
Command: Get-ChildItem + per-line [^\x00-\x7F] check
Result: SCAN-04 PASS: 0 non-ASCII lines
```
**PASS — 0 non-ASCII characters**

AtrWidget strings verified ASCII: "ATR %" / "-- pts" / " pts" / "F2" / "0.75" — all pure ASCII.
Old BuildSpinnerColumn \u25B2 / \u25BC arrow characters confirmed deleted.

### SCAN-05: CYC check
```
Methods verified:
  AtrWidget.BuildAtrWidgetRows()           CYC=1  (straight-line)             PASS
  AtrWidget.SetProvider()                  CYC=2  (null guard + start/stop)   PASS
  AtrWidget.Detach()                       CYC=2  (null guard)                PASS
  AtrWidget.OnTimerTick()                  CYC=6  (WARN-T2-01: actual McCabe) PASS <=8
  AtrWidget.RecalcDisplay()               CYC=1  (straight-line)             PASS
  AtrWidget.OnAtrFractionLostFocus()      CYC=5  (WARN-T2-02: actual McCabe) PASS <=8
  TradeCopierPanel.SetAtrProvider()        CYC=1  (delegation)                PASS
  TradeCopierPanel.ApplyRowVisibilityFlags() CYC=2 (clickTrader only, post-T2) PASS
```
**PASS — all methods CYC ≤ 8 (JS-013)**

WARN-T2-01 and WARN-T2-02 addressed: CYC annotations in implementation use correct values (6 and 5 respectively) per reviewer guidance.

### SCAN-06: Raw brush constructor check
```
Command: Select-String -Path src/PropTraderTools/*.cs -Pattern "new Solid|new Linear|new Radial"
Result: 2 matches — both inside MakeBrush() which calls .Freeze() immediately
  TradeCopierPanel.cs:300 -- inside MakeBrush()
  TradeCopierWindow.cs:76 -- inside MakeBrush()
```
**PASS — all brush constructors are inside MakeBrush() and Freeze()d**

AtrWidget uses only `SetResourceReference("NTBrushes.SubtleBrush")` — no raw brush construction.

### SCAN-07: AtrSizing feature flag check
```
Command: Select-String -Path src/PropTraderTools/FeatureFlags.cs -Pattern "AtrSizing"
Result: (no output)

Command: Select-String -Path src/PropTraderTools/*.cs -Pattern "\.AtrSizing"
Result: (no output)
```
**PASS — 0 matches in FeatureFlags.cs; 0 .AtrSizing access anywhere**

---

## Build Result

```
dotnet build src/PropTraderTools/
Result: 0 Error(s), 187 Warning(s) — all pre-existing CS0436/CS8632 warnings from NT8 dual-compilation setup
```
**BUILD PASS**

---

## ptt-sync.ps1 Result

```
powershell -File .\scripts\ptt-sync.ps1
LINKED  AtrSizingEngine.cs
LINKED  CopyEngine.cs
LINKED  FeatureFlags.cs
LINKED  LicenseClient.cs
LINKED  TradeCopierAddOn.cs
LINKED  TradeCopierPanel.cs
LINKED  TradeCopierWindow.cs
LINKED  Core\PttContracts.cs
LINKED  Features\PttBreakEven.cs
LINKED  Features\PttBreakEvenSwap.cs
LINKED  Features\PttCancel.cs
LINKED  Features\PttCopier.cs
LINKED  Features\PttFlatten.cs
LINKED  Features\PttFollowerStrategy.cs
LINKED  Features\PttGlobalBreakEven.cs
LINKED  Features\PttGlobalQuickExit.cs
LINKED  Features\PttQuickExit.cs
LINKED  Features\PttTrim.cs

ptt-sync complete: 18 linked, 0 errors
```

---

## Status: BUILD_PASS
