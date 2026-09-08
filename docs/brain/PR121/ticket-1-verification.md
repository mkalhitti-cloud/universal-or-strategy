# PR-121 Ticket 1 — Independent Verification Report

**Verifier**: PTT Verifier (Phase 4b)
**File**: `C:\WSGTA\ptt-host\src\PropTraderTools\TradeCopierAddOn.cs`
**Date**: 2026-08-20
**Scope**: Group G1 — 3 fixes (S1, S2/C6, S5)

---

## Verification Result

**VERIFY_PASS**

All 3 fixes correctly implemented. All 7 independent scans clean. Build: 0 errors.

---

## Fix 1 — S1: DoInject ContainsKey guard (TryAdd null crash)

| Check | Expected | Result | Line(s) |
|-------|----------|--------|---------|
| `TryAdd(chart, null)` gone | 0 hits | PASS (SCAN-07: 0) | — |
| `_panels.ContainsKey(chart)` guard in DoInject | present | PASS | 531 |
| guard returns on ContainsKey | `return;` after guard | PASS | 532 |
| `_panels[chart] = panel` in WireNewPanel | present, unchanged | PASS | 515 |

**Evidence (lines 527–532)**:
```csharp
// FIX 1 (PR-121): TryAdd with null throws ArgumentNullException on ConcurrentDictionary.
// Use ContainsKey guard instead -- _panels[chart] = panel is set later in WireNewPanel.
if (_panels.ContainsKey(chart))
    return;
```

**Evidence (line 515)**:
```csharp
_panels[chart] = panel;
```

Fix S1: CONFIRMED ✅

---

## Fix 2 — S2/C6: Per-chart _atrPollTimers ConcurrentDictionary

| Check | Expected | Result | Line(s) |
|-------|----------|--------|---------|
| Old `_atrPollTimer` field gone | 0 live references | PASS | comments only (66, 261) |
| `_atrPollTimers` static ConcurrentDictionary declared | present | PASS | 68–69 |
| `StartAtrEngine` creates per-chart timer in `_atrPollTimers[chart]` | present | PASS | 252–253 |
| `capturedEngine` local capture (not shared) | present | PASS | 241 |
| `StopAtrEngine` uses `_atrPollTimers.TryRemove(chart, out timer)` | present | PASS | 271 |

**Evidence (lines 65–69)**:
```csharp
// B10 T4: per-chart polling timers for ATR computation fallback.
// FIX 2 (PR-121): was single _atrPollTimer field -- only first chart got a timer.
// Now ConcurrentDictionary<Chart, DispatcherTimer> so every chart gets its own timer.
private static readonly ConcurrentDictionary<Chart, DispatcherTimer> _atrPollTimers =
    new ConcurrentDictionary<Chart, DispatcherTimer>();
```

**Evidence (lines 237–253)**:
```csharp
var timer = new DispatcherTimer(DispatcherPriority.Background)
{
    Interval = System.TimeSpan.FromSeconds(1),
};
var capturedEngine = engine; // capture engine local for this chart -- not shared
timer.Tick += (s, e2) => { ... capturedEngine.ManualOnBarUpdate(); ... };
_atrPollTimers[chart] = timer;
timer.Start();
```

**Evidence (lines 270–272)**:
```csharp
DispatcherTimer timer;
if (_atrPollTimers.TryRemove(chart, out timer)) // guard (2): stop per-chart timer
    timer.Stop();
```

**`_atrPollTimer` (singular) scan**:
`Select-String -Pattern "_atrPollTimer[^s]"` → 2 hits, BOTH are comment text only (lines 66, 261). Zero live code references to old field.

Fix S2/C6: CONFIRMED ✅

---

## Fix 3 — S5: Per-chart _sim101KeyDiags ConcurrentDictionary

| Check | Expected | Result | Line(s) |
|-------|----------|--------|---------|
| Old `_sim101KeyDiag` field gone | 0 hits | PASS (SCAN-06: 0) | — |
| `_sim101KeyDiags` static ConcurrentDictionary declared | present | PASS | 62–63 |
| `WireNewPanel` stores per-chart in `_sim101KeyDiags[chart]` | present | PASS | 505 |
| `WireNewPanel` wires `diagHandler` to `chart.PreviewKeyDown` | present | PASS | 506 |
| `RemoveSim101` uses `_sim101KeyDiags.TryRemove(chart, out h)` | present | PASS | 358 |
| `RemoveSim101` unhooks handler from `chart.PreviewKeyDown` | present | PASS | 359 |

**Evidence (lines 59–63)**:
```csharp
// B11 T1 SIM101: per-chart logging-only diag handlers -- mirrors _keyHandlers pattern.
// FIX 3 (PR-121): was single static field, now ConcurrentDictionary to prevent handler leak
// on multi-chart. RemoveSim101 now uses TryRemove per chart.
private static readonly ConcurrentDictionary<Chart, KeyEventHandler> _sim101KeyDiags =
    new ConcurrentDictionary<Chart, KeyEventHandler>();
```

**Evidence (lines 503–506, WireNewPanel)**:
```csharp
// FIX 3 (PR-121): store per-chart in _sim101KeyDiags instead of overwriting single field.
var diagHandler = new KeyEventHandler(OnChartKeyDiag);
_sim101KeyDiags[chart] = diagHandler;
chart.PreviewKeyDown += diagHandler;
```

**Evidence (lines 355–360, RemoveSim101)**:
```csharp
private static void RemoveSim101(Chart chart)
{
    KeyEventHandler h;
    if (_sim101KeyDiags.TryRemove(chart, out h) && h != null)
        chart.PreviewKeyDown -= h;
}
```

Fix S5: CONFIRMED ✅

---

## 7-Scan Results (Independent — Layer 3)

| # | Scan | Command | My Result | Engineer Reported | Cross-check |
|---|------|---------|-----------|-------------------|-------------|
| 1 | `lock(` | `Select-String -Pattern "lock\("` | **0 hits** | 0 hits | MATCH ✅ |
| 2 | `async void ` | `Select-String -Pattern "async void "` | **0 hits** | 0 hits | MATCH ✅ |
| 3 | `return null;` | `Select-String -Pattern "return null;"` | **8 hits — all pre-existing visual tree helpers** (FindVisualChild, FindAccountComboBox, FindVisualChildByIndexInternal at lines 598, 609, 621, 632, 657, 672, 679, 690) | 0 new (8 pre-existing) | MATCH ✅ |
| 4 | CCN > 8 | `lizard --CCN 8` | **0 violations** — all methods ≤ 8 CCN; avg CCN=3.5 for TradeCopierAddOn.cs; 0 Warning cnt | 0 violations | MATCH ✅ |
| 5 | Build | `dotnet build Linting.csproj` | **0 errors, 0 warnings, Build succeeded** | 0 errors | MATCH ✅ |
| 6 | `_sim101KeyDiag[^s]` | `Select-String -Pattern "_sim101KeyDiag[^s]"` | **0 hits** (old single field gone) | 0 hits | MATCH ✅ |
| 7 | `TryAdd(chart, null)` | `Select-String -Pattern "TryAdd\(chart, null\)"` | **0 hits** (old pattern gone) | Not in engineer's 7 (they ran ASCII check instead) | N/A — my scan added per task spec |

**Additional ASCII scan (task requirement)**:
`[Regex]::Matches(content, '[^\x00-\x7F]').Count` → **0 non-ASCII characters** ✅

---

## DNA Rule Check (Jane Street Rules Catalog)

| Rule | Check | Result |
|------|-------|--------|
| JS-021: `lock()` banned | SCAN-01: 0 hits | PASS ✅ |
| JS-023: `volatile bool` for menu guard | `_menuWired` is `volatile bool` (line 37) | PASS ✅ |
| JS-033: `async void` banned (non-event) | SCAN-02: 0 hits | PASS ✅ |
| JS-002: `return null` in hot paths | All 8 instances are in visual tree DFS helpers — acceptable pattern per NT8 visual tree API. None in dispatch, guard, or state methods. | PASS ✅ |
| JS-008: `new SolidColorBrush` must `.Freeze()` | No `SolidColorBrush` in TradeCopierAddOn.cs | N/A ✅ |
| JS-010: Non-private constructors on CopyEngine/signals | Not in this file | N/A ✅ |
| NT8: `FontFamily=` in WPF | SCAN-03 analog: 0 hits | PASS ✅ |
| NT8: `#RRGGBB` hex color string | 0 hits | PASS ✅ |
| NT8: `DateTime.Now` (not UtcNow) | Not present in file | PASS ✅ |
| NT8: `CreateOrder` without "PTT-" prefix | Not called in this file | N/A ✅ |
| NT8: `sealed` on TradeCopierWindow class | Not in this file | N/A ✅ |
| NT8: `async/await` in OnInitialize/OnDestroyed/OnWindowCreated | None present | PASS ✅ |

---

## Architecture Compliance

| Requirement | Status |
|-------------|--------|
| `ConcurrentDictionary` used for all per-chart state (not plain `Dictionary`) | PASS — `_panels`, `_atrEngines`, `_clickHandlers`, `_keyHandlers`, `_atrPollTimers`, `_sim101KeyDiags` all ConcurrentDictionary ✅ |
| Single `_atrPollTimer` replaced — first-chart-only capture bug eliminated | CONFIRMED ✅ |
| Single `_sim101KeyDiag` replaced — handler-leak-on-multi-chart eliminated | CONFIRMED ✅ |
| `TryAdd(chart, null)` crash eliminated — ContainsKey guard used instead | CONFIRMED ✅ |
| `_panels[chart] = panel` set in WireNewPanel after successful grid injection | CONFIRMED (line 515) ✅ |
| `StopAtrEngine` cleans up per-chart timer via `TryRemove` | CONFIRMED (line 271) ✅ |
| `RemoveSim101` cleans up per-chart handler via `TryRemove` | CONFIRMED (line 358) ✅ |
| Engine capture local (`capturedEngine`) prevents shared-state capture in lambda | CONFIRMED (line 241) ✅ |

---

## Cross-check vs Engineer's Completion Report

**Discrepancies**: NONE.

All engineer-reported fix descriptions match the actual source code exactly:
- Fix 1 before/after code blocks match lines 529–532 verbatim ✅
- Fix 2 before/after code blocks match lines 65–69, 237–253, 270–272 verbatim ✅
- Fix 3 before/after code blocks match lines 59–63, 503–506, 355–360 verbatim ✅
- Engineer scan results match independent Layer 3 results for all 6 overlapping scans ✅

**Note on scan numbering**: Engineer's 7-scan table used ASCII check as scan 6 (not TryAdd check).
Task spec required TryAdd(chart, null) as SCAN-07. Both were run independently — both return 0.

---

## Return Status

**VERIFY_PASS**

All 3 Group G1 fixes from PR-121 Ticket 1 are correctly implemented with no violations.