# PR-121 Ticket 1 — Completion Report

**File**: `C:\WSGTA\ptt-host\src\PropTraderTools\TradeCopierAddOn.cs`
**Date**: 2026-08-20
**Status**: BUILD_PASS

---

## Rules Catalog Gate

PASS — confirmed no P0 violations in TradeCopierAddOn.cs:
- JS-021 `lock()`: 0 hits
- JS-033 `async void`: 0 hits
- JS-002 `return null`: all pre-existing instances in visual tree helpers (not introduced by this PR)

---

## Fix 1 — S1: TryAdd null value crash (DoInject guard)

**Problem**: `ConcurrentDictionary<Chart, TradeCopierPanel>` does not permit null values.
`_panels.TryAdd(chart, null)` always throws `ArgumentNullException`, preventing DoInject from
ever reaching its try block. No panel was ever injected.

**Before** (line 526–527):
```csharp
if (!_panels.TryAdd(chart, null))
    return;
```

**After**:
```csharp
// FIX 1 (PR-121): TryAdd with null throws ArgumentNullException on ConcurrentDictionary.
// Use ContainsKey guard instead -- _panels[chart] = panel is set later in WireNewPanel.
if (_panels.ContainsKey(chart))
    return;
```

`_panels[chart] = panel` assignment in `WireNewPanel` (line 512) is preserved — unchanged.

---

## Fix 2 — S2/C6: Single shared _atrPollTimer captures only first chart's engine

**Problem**: `private DispatcherTimer _atrPollTimer = null` (line 66) was a single instance field.
The `if (_atrPollTimer == null)` guard meant only the first chart's engine received a timer.
All subsequent charts never received `ManualOnBarUpdate`. Closing the first chart stopped the
timer for ALL charts.

**Before** — field:
```csharp
private DispatcherTimer _atrPollTimer = null;
```

**Before** — StartAtrEngine timer block:
```csharp
if (_atrPollTimer == null) // guard (3): create timer once
{
    _atrPollTimer = new DispatcherTimer(DispatcherPriority.Background)
    {
        Interval = System.TimeSpan.FromSeconds(1),
    };
    _atrPollTimer.Tick += (s, e2) =>
    {
        try { engine.ManualOnBarUpdate(); }
        catch (System.Exception) { /* NT8 context not ready; next tick will retry */ }
    };
    _atrPollTimer.Start();
}
```

**Before** — StopAtrEngine timer cleanup:
```csharp
if (_atrPollTimer != null) // guard (2): stop poll timer
{
    _atrPollTimer.Stop();
    _atrPollTimer = null;
}
```

**After** — field replaced with ConcurrentDictionary:
```csharp
// B10 T4: per-chart polling timers for ATR computation fallback.
// FIX 2 (PR-121): was single _atrPollTimer field -- only first chart got a timer.
// Now ConcurrentDictionary<Chart, DispatcherTimer> so every chart gets its own timer.
private static readonly ConcurrentDictionary<Chart, DispatcherTimer> _atrPollTimers =
    new ConcurrentDictionary<Chart, DispatcherTimer>();
```

**After** — StartAtrEngine timer block (always creates, no guard):
```csharp
var timer = new DispatcherTimer(DispatcherPriority.Background)
{
    Interval = System.TimeSpan.FromSeconds(1),
};
var capturedEngine = engine; // capture engine local for this chart -- not shared
timer.Tick += (s, e2) =>
{
    try { capturedEngine.ManualOnBarUpdate(); }
    catch (System.Exception) { /* NT8 context not ready; next tick will retry */ }
};
_atrPollTimers[chart] = timer;
timer.Start();
```

**After** — StopAtrEngine timer cleanup (per-chart TryRemove):
```csharp
DispatcherTimer timer;
if (_atrPollTimers.TryRemove(chart, out timer)) // guard (2): stop per-chart timer
    timer.Stop();
```

---

## Fix 3 — S5: _sim101KeyDiag single static field leaks handlers on multi-chart

**Problem**: `private static KeyEventHandler _sim101KeyDiag` (line 62) was a single field.
On second chart injection it was overwritten. `RemoveSim101` could only unhook the LAST chart's
handler. Earlier charts' diagnostic handlers were never removed — they leaked.

**Before** — field:
```csharp
private static KeyEventHandler _sim101KeyDiag;
```

**Before** — WireNewPanel:
```csharp
_sim101KeyDiag = new KeyEventHandler(OnChartKeyDiag);
chart.PreviewKeyDown += _sim101KeyDiag;
```

**Before** — RemoveSim101:
```csharp
private static void RemoveSim101(Chart chart)
{
    if (_sim101KeyDiag != null)
        chart.PreviewKeyDown -= _sim101KeyDiag;
    _sim101KeyDiag = null;
}
```

**After** — field replaced with ConcurrentDictionary:
```csharp
// B11 T1 SIM101: per-chart logging-only diag handlers -- mirrors _keyHandlers pattern.
// FIX 3 (PR-121): was single static field, now ConcurrentDictionary to prevent handler leak
// on multi-chart. RemoveSim101 now uses TryRemove per chart.
private static readonly ConcurrentDictionary<Chart, KeyEventHandler> _sim101KeyDiags =
    new ConcurrentDictionary<Chart, KeyEventHandler>();
```

**After** — WireNewPanel:
```csharp
// FIX 3 (PR-121): store per-chart in _sim101KeyDiags instead of overwriting single field.
var diagHandler = new KeyEventHandler(OnChartKeyDiag);
_sim101KeyDiags[chart] = diagHandler;
chart.PreviewKeyDown += diagHandler;
```

**After** — RemoveSim101 (per-chart TryRemove):
```csharp
private static void RemoveSim101(Chart chart)
{
    KeyEventHandler h;
    if (_sim101KeyDiags.TryRemove(chart, out h) && h != null)
        chart.PreviewKeyDown -= h;
}
```

---

## Build Output

```
dotnet build C:\WSGTA\ptt-host\Linting.csproj
Build succeeded.
    0 Error(s)
```

All warnings are pre-existing (SA StyleCop, CS0612 obsolete Account.CreateOrder, etc.)
Zero errors introduced by this PR.

---

## CCN Scan

```
lizard C:\WSGTA\ptt-host\src\PropTraderTools -x "*/bin/*" -x "*/obj/*" --CCN 8
No thresholds exceeded (cyclomatic_complexity > 8 or length > 1000 or nloc > 1000000 or parameter_count > 100)
```

**Result: 0 methods exceed CCN 8.**

---

## 7-Scan Checklist

| # | Scan | Command | Result |
|---|------|---------|--------|
| 1 | lock() usage | `Select-String -Pattern "lock\("` | **0 hits** |
| 2 | async void | `Select-String -Pattern "async void "` | **0 hits** |
| 3 | return null (new) | `Select-String -Pattern "return null;"` | **0 new** — all 8 hits are pre-existing visual tree helpers (FindVisualChild etc.) |
| 4 | CCN > 8 | `lizard --CCN 8` | **0 violations** |
| 5 | Build | `dotnet build Linting.csproj` | **0 errors** |
| 6 | ASCII check | `[Regex]::Matches(..., '[^\x00-\x7F]')` | **0 non-ASCII chars** |
| 7 | _sim101KeyDiag field gone | `Select-String -Pattern "_sim101KeyDiag[^s]"` | **0 hits** (old field removed; stale comment in UnhookKeyShortcut updated) |

---

## Return Status

**BUILD_PASS**
