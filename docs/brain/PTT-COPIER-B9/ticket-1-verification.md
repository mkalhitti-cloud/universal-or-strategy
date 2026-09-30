# PTT-COPIER-B9 — Ticket T1 Verification Report
**Ticket**: T1 — ATR Dynamic Sizing Engine (DW-B7-02 / DW-B8-03)
**Verifier**: ptt-verifier (Phase 5.V) — independent check, no trust in engineer scan results
**Date**: 2026-07-09
**Wave workspace**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\`
**Director workspace**: `c:\WSGTA\universal-or-strategy-director\`

---

## Evidence Summary — Files Read

| File | Path | Lines |
|------|------|-------|
| `AtrSizingEngine.cs` | Wave `src/PropTraderTools/AtrSizingEngine.cs` | 99 |
| `CopyEngine.cs` | Wave `src/PropTraderTools/CopyEngine.cs` | 1061 |
| `TradeCopierAddOn.cs` | Wave `src/PropTraderTools/TradeCopierAddOn.cs` | 267 |
| `CopyEngineTests.cs` | Wave `src/PropTraderTools/CopyEngineTests.cs` | 972 |
| `04-tickets.md` | Director brain | T1 section lines 32–388 |
| `ticket-1-completion.md` | Director brain | Full |
| `04-ticket-review.md` | Director brain | T1 section lines 28–108 |

All files verified present with content. READ-ONLY — no modifications made.

---

## Check 1 — AtrSizingEngine.cs Existence and Correctness

### 1.1 File exists with content
**PASS** — File present, 99 lines of C# source.

### 1.2 Class declaration
**PASS** — Line 11: `public class AtrSizingEngine : Indicator`
Extends `Indicator` (NinjaTrader.NinjaScript.Indicators.Indicator) as required.
Not `AddOnBase`, not `NinjaScriptBase`. Using: `NinjaTrader.NinjaScript.Indicators`.

### 1.3 Namespace
**PASS** — Line 9: `namespace PropTraderTools`

### 1.4 CalcContracts is `internal static`
**PASS** — Line 89: `internal static int CalcContracts(double atrPoints, double maxRisk, double tickDollarValue)`
Fully unit-testable without NT8 context.

### 1.5 Volatile cross-thread fields (JS-023)
**PASS** — All three fields declared volatile:
- Line 25: `private volatile int    _lastContracts = 1;`
- Line 26: `private volatile double _lastAtr       = 0.0;`
- Line 27: `private volatile bool   _hasData       = false;`

### 1.6 OnBarUpdate `CurrentBar < Period` guard
**PASS** — Line 64: `if (CurrentBar < Period) return;`

### 1.7 CalcContracts: 3 guards
**PASS** — Lines 91–95:
- Guard (1): `if (atrPoints       <= 0) return 1;`   (zero/negative ATR)
- Guard (2): `if (tickDollarValue <= 0) return 1;`   (zero tick dollar value)
- Guard (3): `return contracts < 1 ? 1 : contracts;` (clamp minimum to 1)

### 1.8 GetSuggestedQty returns 1 if !_hasData
**PASS** — Lines 79–83:
```csharp
internal int GetSuggestedQty()
{
    if (!_hasData) return 1;
    return _lastContracts;
}
```

### 1.9 Test-seam constructor present
**PASS** — Lines 15–19:
```csharp
internal AtrSizingEngine(int testContracts)
{
    _lastContracts = testContracts;
    _hasData       = true;
}
```
Comment: `// Test-only seam. Do NOT use in production code.`

### 1.10 NO lock() calls in any method body
**PASS** — grep scan over `AtrSizingEngine.cs` for `lock\s*\(`: **0 matches** (zero).

### 1.11 NO async void methods
**PASS** — grep scan over `AtrSizingEngine.cs` for `async void`: **0 matches** (zero).

### 1.12 NO hex color literals
**PASS** — grep scan over `AtrSizingEngine.cs` for `#[0-9A-Fa-f]{6}`: **0 matches** (zero).

### 1.13 OnStateChange state handling
**PASS** — Lines 37–59 implement all 4 required states:
- `State.SetDefaults` (line 39): sets Description, Name, Period=14
- `State.Configure` (line 45): `AddDataSeries(NinjaTrader.Data.BarsPeriodType.Minute, 1)` ✅
- `State.DataLoaded` (line 49): `Add(ATR(Period))` ✅
- `State.Terminated` (line 53): resets `_hasData=false`, `_lastContracts=1`, `_lastAtr=0.0` ✅

**Deviation note (IMPL-NOTE-1):** Ticket spec shows `AddDataSeries(_instrument, BarsPeriodType.Minute, 1)` with a named instrument parameter. Actual implementation uses `AddDataSeries(NinjaTrader.Data.BarsPeriodType.Minute, 1)` (2-argument form without named instrument). The ticket explicitly defers `_instrument` parameter to DW-B9-02 "verify exact `AddDataSeries` / `Add(ATR(...))` API at T1 execution time per IMPL-NOTE-1". The completion report documents this as "IMPL-NOTE-1 acknowledged" and the engine's `GetSuggestedQty()` remains safe (returns 1 until `_hasData` is true). **This is a documented IMPL-NOTE-1 deferral, not a violation.**

**CYC — OnStateChange: 4 decision branches (4 `if`/`else if` chains). PASS (≤8)**
**CYC — OnBarUpdate: 2 (guard + base). PASS (≤8)**
**CYC — CalcContracts: 3 (two guards + ternary clamp). PASS (≤8)**
**CYC — GetSuggestedQty: 2 (guard + return). PASS (≤8)**
**CYC — SetParameters: 1 (straight-line). PASS (≤8)**
**CYC — GetLastAtr: 1 (expression body). PASS (≤8)**

### Check 1 Verdict: **PASS**

---

## Check 2 — CopyEngine.cs T1 Additions

### 2.1 `private volatile bool _atrEnabled = false;` (ADV-002 fix)
**PASS** — Line 52: `private volatile bool _atrEnabled = false;   // JS-023`
Comment on line 51 explicitly notes: `// B9 T1 -- ATR sizing engine integration (JS-023: volatile, ADV-002 fix)`

### 2.2 `_atrEngine` field exists and is volatile
**PASS** — Line 53: `private volatile AtrSizingEngine _atrEngine  = null;    // write/read on UI thread only`
Field is declared `volatile` as required by JS-023.

### 2.3 `SetAtrEngine(AtrSizingEngine engine, bool enabled)` method exists
**PASS** — Lines 177–181:
```csharp
internal void SetAtrEngine(AtrSizingEngine engine, bool enabled)
{
    _atrEngine  = engine;
    _atrEnabled = enabled;
}
```
CYC=1 (straight-line). No lock, no throw, no return null.

### 2.4 `GetSuggestedQty(Instrument instrument)` method exists and returns 1 when disabled
**PASS** — Lines 184–189:
```csharp
internal int GetSuggestedQty(NinjaTrader.Cbi.Instrument instrument)
{
    if (_atrEnabled && _atrEngine != null)
        return _atrEngine.GetSuggestedQty();
    return 1;
}
```
CYC=2. Returns 1 when `_atrEnabled=false` OR `_atrEngine=null`. Correct.

### 2.5 DispatchCopy uses ATR base qty
**PASS** — Line 346: `int baseQty = _atrEnabled ? GetSuggestedQty(order.Instrument) : baseSignal.Quantity;`
Line 358: `baseQty * mult` used in the scaled signal. ATR qty overrides signal qty when enabled; falls back to signal qty otherwise.

### 2.6 No new lock() calls
**PASS** — grep over all `.cs` files for `lock\s*\(` returns only **2 comment-only matches** in `CopyEngine.cs` (lines 226 and 610), both in `// no lock (JS-021)` comment strings. **ZERO executable lock() calls** anywhere in T1 new code.

### 2.7 No new return null in T1 methods
**PASS** — `SetAtrEngine` returns `void`. `GetSuggestedQty` returns `int`. Both return 1 as safe floor — never null.
grep over `AtrSizingEngine.cs` for `return null`: **0 matches** (zero).

### Check 2 Verdict: **PASS**

---

## Check 3 — TradeCopierAddOn.cs T1 Additions

### 3.1 `_atrEngines` field is `ConcurrentDictionary<Chart, AtrSizingEngine>` (not `Dictionary<`)
**PASS** — Lines 36–37:
```csharp
private static readonly ConcurrentDictionary<Chart, AtrSizingEngine> _atrEngines
    = new ConcurrentDictionary<Chart, AtrSizingEngine>();
```
grep for `new Dictionary<` over all `.cs` files: **0 matches** (zero). `ConcurrentDictionary` confirmed.

### 3.2 `StartAtrEngine(Chart, Instrument)` exists with CYC ≤ 8
**PASS** — Lines 146–158:
```csharp
private static void StartAtrEngine(Chart chart, NinjaTrader.Cbi.Instrument instr)
{
    if (chart == null) return;                        // guard (1)
    if (instr  == null) return;                       // guard (2)
    var engine = new AtrSizingEngine();
    double pointValue = instr.MasterInstrument?.PointValue ?? 5.0;
    engine.SetParameters(150.0, pointValue);
    _atrEngines[chart] = engine;
    CopyEngine.Instance.SetAtrEngine(engine, enabled: false); // disabled until user enables (3)
    // IMPL-NOTE-1 (DW-B9-02): NT8 Indicator attachment...
}
```
CYC=3 (2 null guards + base path). Well under limit of 8. IMPL-NOTE-1 acknowledged in comment.

### 3.3 `StopAtrEngine(Chart)` exists with CYC ≤ 8
**PASS** — Lines 161–166:
```csharp
private static void StopAtrEngine(Chart chart)
{
    AtrSizingEngine engine;
    if (!_atrEngines.TryRemove(chart, out engine)) return; // guard (1)
    CopyEngine.Instance.SetAtrEngine(null, enabled: false); // guard (2): clear reference
}
```
CYC=2. Note: Ticket shows `try { chart.NinjaScripts.Remove(engine) } catch { }` as guard (2), but actual implementation
omits the NinjaScripts.Remove call and instead only clears the CopyEngine reference. This is consistent with IMPL-NOTE-1
(chart attachment API deferred). The essential cleanup (clearing CopyEngine reference) is performed. No NT8 teardown crash
risk because NinjaScripts.Add was never called. **IMPL-NOTE-1 deferral — NOT a violation**.

### 3.4 `DoInject` calls `StartAtrEngine`
**PASS** — Line 200 in `DoInject`: `StartAtrEngine(chart, chartInstr);`
Called after `panel.SetInstrument(instr)` and instrument capture from `chartTrader.Instrument`.

### 3.5 Teardown path calls `StopAtrEngine`
**PASS** — Lines 70–77 in `OnWindowDestroyed`:
```csharp
var chart = window as Chart;
if (chart == null) return;
StopAtrEngine(chart);       // B9 T1 ATR teardown
TradeCopierPanel panel;
if (_panels.TryRemove(chart, out panel))
    panel.Detach();
```
`StopAtrEngine` is first call after the null guard — correct teardown ordering.

### 3.6 No new lock() calls
**PASS** — All T1 additions use `ConcurrentDictionary` and `volatile` fields. No `lock` keyword in AddOn.cs.
grep over entire directory: **ZERO executable lock calls** confirmed.

### Check 3 Verdict: **PASS**

---

## Check 4 — CopyEngineTests.cs T1 Additions

### 4.1 Total [Fact] count = 50
**PASS** — Independent count via:
```powershell
Select-String -Path CopyEngineTests.cs -Pattern '\[Fact\]' | Measure-Object -> 50
```
Also confirmed via grep over file: exactly **50 `[Fact]` attributes** (lines 23, 33, 43, 53, 63, 83, 104, 116, 131,
139, 149, 160, 171, 180, 188, 196, 211, 226, 239, 268, 295, 310, 347, 359, 371, 424, 440, 468, 500, 530, 560, 589,
608, 634, 673, 706, 742, 777, 816, 854, 896, 903, 910, 917, 924, 931, 938, 945, 952, 962).

### 4.2 T-B9-01 through T-B9-10 all present
**PASS** — grep for `T-B9` in `CopyEngineTests.cs` returns 11 matches covering comments for T-B9-01 through T-B9-10.

### T-B9-01: `CalcContracts(6.0, 150.0, 5.0) == 5`
**PASS** — Lines 896–899:
```csharp
[Fact]
public void CalcContracts_MES_ATR6_returns5()
{
    Assert.Equal(5, AtrSizingEngine.CalcContracts(atrPoints: 6.0, maxRisk: 150.0, tickDollarValue: 5.0));
}
```
Formula: floor(150 / (6 * 5)) = floor(150/30) = floor(5.0) = **5**. ✅

### T-B9-02: `CalcContracts(8.0, 150.0, 5.0) == 3`
**PASS** — Lines 903–907:
```csharp
Assert.Equal(3, AtrSizingEngine.CalcContracts(atrPoints: 8.0, maxRisk: 150.0, tickDollarValue: 5.0));
```
Formula: floor(150 / (8 * 5)) = floor(150/40) = floor(3.75) = **3**. ✅

### T-B9-03: `CalcContracts(12.0, 150.0, 5.0) == 2`
**PASS** — Lines 910–914:
```csharp
Assert.Equal(2, AtrSizingEngine.CalcContracts(atrPoints: 12.0, maxRisk: 150.0, tickDollarValue: 5.0));
```
Formula: floor(150 / (12 * 5)) = floor(150/60) = floor(2.5) = **2**. ✅

### T-B9-04: Zero ATR → 1
**PASS** — Lines 917–921:
```csharp
Assert.Equal(1, AtrSizingEngine.CalcContracts(atrPoints: 0.0, maxRisk: 150.0, tickDollarValue: 5.0));
```
atrPoints=0 → guard (1) fires → returns **1**. ✅

### T-B9-05: Negative ATR → 1
**PASS** — Lines 924–928:
```csharp
Assert.Equal(1, AtrSizingEngine.CalcContracts(atrPoints: -3.0, maxRisk: 150.0, tickDollarValue: 5.0));
```
atrPoints=-3 → guard (1) fires (`<= 0`) → returns **1**. ✅

### T-B9-06: Result below 1 → clamp to 1
**PASS** — Lines 931–935:
```csharp
Assert.Equal(1, AtrSizingEngine.CalcContracts(atrPoints: 1.0, maxRisk: 5.0, tickDollarValue: 10.0));
```
floor(5 / (1*10)) = floor(0.5) = 0 → guard (3) clamps to **1**. ✅

### T-B9-07: Zero tick value → 1
**PASS** — Lines 938–942:
```csharp
Assert.Equal(1, AtrSizingEngine.CalcContracts(atrPoints: 6.0, maxRisk: 150.0, tickDollarValue: 0.0));
```
tickDollarValue=0 → guard (2) fires → returns **1**. ✅

### T-B9-08: Large maxRisk → no overflow
**PASS** — Lines 945–949:
```csharp
Assert.Equal(2000, AtrSizingEngine.CalcContracts(atrPoints: 1.0, maxRisk: 10000.0, tickDollarValue: 5.0));
```
floor(10000 / (1*5)) = floor(2000.0) = **2000**. int range: 2000 << INT_MAX. No overflow. ✅

### T-B9-09: `GetSuggestedQty` returns 1 when ATR disabled
**PASS** — Lines 952–957:
```csharp
[Fact]
public void GetSuggestedQty_returns1_when_no_engine()
{
    CopyEngine.Instance.SetAtrEngine(null, enabled: false);
    int qty = CopyEngine.Instance.GetSuggestedQty(null);
    Assert.Equal(1, qty);
}
```
`_atrEnabled=false` → `GetSuggestedQty` returns 1. ✅

### T-B9-10: Test-seam constructor → returns 3
**PASS** — Lines 962–970:
```csharp
[Fact]
public void GetSuggestedQty_returns_engine_qty_when_set()
{
    var atrEngine = new AtrSizingEngine(testContracts: 3);
    CopyEngine.Instance.SetAtrEngine(atrEngine, enabled: true);
    int qty = CopyEngine.Instance.GetSuggestedQty(null);
    CopyEngine.Instance.SetAtrEngine(null, enabled: false); // teardown
    Assert.Equal(3, qty);
}
```
Test-seam ctor sets `_lastContracts=3`, `_hasData=true`. `GetSuggestedQty()` returns 3. ✅
Teardown present (restores null/disabled). ✅

### Check 4 Verdict: **PASS**

---

## Check 5 — Independent 7-Scan Results

All scans run independently via `grep` (built-in) and `execute_command` (PowerShell). No trust in engineer scan results.

### SCAN-01: `lock(` in executable code (AtrSizingEngine.cs)
**Command**: `grep pattern="lock\s*\(" path=AtrSizingEngine.cs`
**Result**: **ZERO matches** in AtrSizingEngine.cs
**Whole-directory**: grep over all `.cs` files returns 2 comment-only hits in `CopyEngine.cs`:
- Line 226: `// ConcurrentBag rebuild pattern -- no lock (JS-021)`
- Line 610: `// ConcurrentBag rebuild pattern -- no lock (JS-021).`
Both are comment strings. **ZERO executable `lock(` calls**. ✅ PASS

### SCAN-02: `throw new` in T1 code
**Command**: `grep pattern="throw new" path=AtrSizingEngine.cs`
**Result**: **ZERO matches**. ✅ PASS
**Whole-directory**: grep over all `.cs` files: **ZERO matches** for `throw new`. ✅

### SCAN-03: `return null` in T1 methods
**Command**: `grep pattern="return null" path=AtrSizingEngine.cs`
**Result**: **ZERO matches**. ✅ PASS
`CalcContracts` returns `int`, `GetSuggestedQty` returns `int`, `SetParameters` returns `void`.

### SCAN-04: `new Dictionary<` (should be ConcurrentDictionary)
**Command**: `grep pattern="new Dictionary<" path=TradeCopierAddOn.cs`
**Result**: **ZERO matches**. ✅ PASS
`_atrEngines` confirmed as `ConcurrentDictionary<Chart, AtrSizingEngine>` (line 36–37).
Whole-directory grep: **ZERO matches** for `new Dictionary<` anywhere.

### SCAN-05: `DateTime.Now` (non-UTC)
**Command**: `grep pattern="DateTime\.Now[^U]" include=*.cs path=PropTraderTools/`
**Result**: **ZERO matches** across all `.cs` files. ✅ PASS

### SCAN-06: `async void` in T1 code
**Command**: `grep pattern="async void" include=*.cs path=PropTraderTools/`
**Result**: **ZERO matches** across all `.cs` files. ✅ PASS

### SCAN-07: Hex color literals `#RRGGBB` in string literals
**Command**: `grep pattern="#[0-9A-Fa-f]{6}" include=*.cs path=PropTraderTools/`
**Result**: **8 matches** — ALL in comment annotations only:
- `TradeCopierWindow.cs` lines 51–54: `// green  #22c55e`, `// red  #ef4444`, `// amber  #f59e0b`, `// grey  #4b5563`
- `TradeCopierPanel.cs` lines 77–80: same color reference comments

**Assessment**: All 8 hits are in `// comment` text after the semicolons, documenting the decimal RGB values passed to `MakeBrush()`. The actual code uses `MakeBrush(34, 197, 94)` — decimal RGB, not hex strings. The DNA rule is "hex color STRING in WPF element" (e.g., `FontFamily="..."` or `Foreground="#22c55e"`). **None of these 8 are hex color strings in WPF markup or C# string literals.** ✅ PASS

Zero hits in `AtrSizingEngine.cs`. Zero hits in T1 code paths.

### 7-Scan Summary

| Scan | Pattern | Files Scanned | Matches | Verdict |
|------|---------|---------------|---------|---------|
| SCAN-01 | `lock\s*\(` executable | All `.cs` | 0 executable (2 comments) | ✅ PASS |
| SCAN-02 | `throw new` | AtrSizingEngine.cs + all | 0 | ✅ PASS |
| SCAN-03 | `return null` | AtrSizingEngine.cs | 0 | ✅ PASS |
| SCAN-04 | `new Dictionary<` | TradeCopierAddOn.cs + all | 0 | ✅ PASS |
| SCAN-05 | `DateTime\.Now[^U]` | All `.cs` | 0 | ✅ PASS |
| SCAN-06 | `async void` | All `.cs` | 0 | ✅ PASS |
| SCAN-07 | `#[0-9A-Fa-f]{6}` string | AtrSizingEngine.cs; others in comments only | 0 in strings | ✅ PASS |

### Check 5 Verdict: **PASS**

---

## Check 6 — Spec Alignment

### 6.1 CalcContracts formula matches spec example
**PASS** — Formula in code (line 94):
```csharp
int contracts = (int)Math.Floor(maxRisk / riskPerContract);
```
where `riskPerContract = atrPoints * tickDollarValue`.

Spec example: ATR=6pts, MES tick=$5, maxRisk=$150
- `riskPerContract = 6 * 5 = 30`
- `floor(150 / 30) = floor(5.0) = 5`
- Test T-B9-01 confirms: `Assert.Equal(5, CalcContracts(6.0, 150.0, 5.0))` ✅

### 6.2 ATR engine design matches spec note
**PASS** — `AtrSizingEngine` extends `Indicator` (not `AddOnBase`), managed by `TradeCopierAddOn`
(one instance per chart stored in `_atrEngines`). This is exactly the spec note
"AddOnBase has no MarketData — use detached Indicator for ATR data access". ✅

### 6.3 IMPL-NOTE-1 acknowledged in completion report
**PASS** — Completion report lines 49–51 explicitly documents:
> "NT8 Indicator attachment via `chart.NinjaScripts.Add(engine)` deferred pending runtime API verification.
> Engine object is stored in `_atrEngines` and `CopyEngine._atrEngine`;
> `GetSuggestedQty()` is callable immediately (returns safe default of 1 until `_hasData` is true)."

DW-B9-02 opened as follow-up. Design intent intact — engine is safe to call without chart attachment. ✅

### 6.4 Spec requirements closed by T1
**PASS**:
- DW-B7-02 → CLOSED T1 ✅
- DW-B8-03 → CLOSED T1 ✅

### Check 6 Verdict: **PASS**

---

## Architecture Compliance Summary

| Requirement | Source | Status |
|-------------|--------|--------|
| AtrSizingEngine extends Indicator | Ticket §T1, plan §8 | ✅ CONFIRMED line 11 |
| Namespace PropTraderTools | All files | ✅ CONFIRMED line 9 |
| CalcContracts internal static | Ticket §T1 testability | ✅ CONFIRMED line 89 |
| 3 volatile cross-thread fields | JS-023 | ✅ CONFIRMED lines 25–27 |
| Test-seam ctor present | Ticket §T1, T-B9-10 | ✅ CONFIRMED lines 15–19 |
| CopyEngine: _atrEnabled volatile | ADV-002 | ✅ CONFIRMED line 52 |
| CopyEngine: _atrEngine volatile | JS-023 | ✅ CONFIRMED line 53 |
| SetAtrEngine(engine, enabled) | Ticket §T1 | ✅ CONFIRMED lines 177–181 |
| GetSuggestedQty(Instrument) | Ticket §T1 | ✅ CONFIRMED lines 184–189 |
| DispatchCopy ATR integration | Ticket §T1 | ✅ CONFIRMED line 346 |
| _atrEngines ConcurrentDictionary | Ticket §T1, JS-025 | ✅ CONFIRMED lines 36–37 |
| StartAtrEngine(chart, instr) CYC≤8 | Ticket §T1 | ✅ CONFIRMED CYC=3 |
| StopAtrEngine(chart) CYC≤8 | Ticket §T1 | ✅ CONFIRMED CYC=2 |
| DoInject calls StartAtrEngine | Ticket §T1 | ✅ CONFIRMED line 200 |
| OnWindowDestroyed calls StopAtrEngine | Ticket §T1 | ✅ CONFIRMED line 73 |
| 50 [Fact] tests | Build gate T1 | ✅ CONFIRMED count=50 |
| T-B9-01..10 all present | Ticket §T1 | ✅ CONFIRMED all 10 |
| IMPL-NOTE-1 documented | DW-B9-02 | ✅ CONFIRMED in completion report |

---

## DNA Rule Check — Full Jane Street Compliance

| Rule | Pattern | Result | Evidence |
|------|---------|--------|---------|
| JS-021 no lock() | `lock\s*\(` executable | ✅ ZERO | grep: 0 executable hits |
| JS-023 volatile fields | cross-thread fields are volatile | ✅ CONFIRMED | Lines 25–27, 52–53 |
| JS-001 no throw in dispatch | `throw new` | ✅ ZERO | grep: 0 hits any .cs |
| JS-002 no return null | `return null` in new methods | ✅ ZERO | grep: 0 hits in AtrSizingEngine.cs |
| JS-003 readonly structs | CopySignal, TrimSignal, CopyRule constructors private | ✅ Pre-existing B8 compliance |
| JS-008 immutability | No mutable struct across threads; volatile primitives used | ✅ PASS |
| JS-009 ImmutableDictionary | FollowerAtmTemplates uses ImmutableDictionary | ✅ Pre-existing B8 compliance |
| JS-010 private ctor | CopyEngine() private (line 160); AtrSizingEngine() public (NT8 req.) | ✅ PASS — NT8 requires public default ctor |
| JS-025 ConcurrentDictionary | _atrEngines uses ConcurrentDictionary | ✅ CONFIRMED lines 36–37 |
| JS-033 no async void | `async void` | ✅ ZERO | grep: 0 hits any .cs |
| NT8: no lock in OnBarUpdate | `lock` in OnBarUpdate | ✅ ZERO | |
| NT8: no FontFamily | `FontFamily` | ✅ ZERO (pre-existing B8 PASS) |
| NT8: no hex color strings | `#RRGGBB` in string values | ✅ ZERO in strings | Comments only |
| NT8: PTT- prefix on CreateOrder | T1 does not add CreateOrder calls | ✅ N/A for T1 |
| NT8: DateTime.UtcNow | `DateTime.Now[^U]` | ✅ ZERO | grep: 0 hits |
| NT8: TradeCopierWindow not sealed | Not modified in T1 | ✅ N/A for T1 |
| CYC ≤ 8 all T1 methods | | ✅ ALL PASS | Max CYC=4 (OnStateChange) |

---

## Deviations From Ticket Spec (Non-Violations)

| # | Item | Ticket Spec | Actual | Classification |
|---|------|-------------|--------|----------------|
| D-1 | `AddDataSeries` signature | `AddDataSeries(_instrument, BarsPeriodType.Minute, 1)` 3-arg | `AddDataSeries(NinjaTrader.Data.BarsPeriodType.Minute, 1)` 2-arg | IMPL-NOTE-1 deferral. Engine safe; GetSuggestedQty returns 1 until _hasData. Not a violation. |
| D-2 | `StopAtrEngine` body | `try { chart.NinjaScripts.Remove(engine) } catch { }` present | `NinjaScripts.Remove` omitted; CopyEngine reference cleared | IMPL-NOTE-1 deferral. NinjaScripts.Add was never called, so Remove is a no-op. Essential cleanup (CopyEngine ref) present. Not a violation. |

Both deviations are documented IMPL-NOTE-1 deferrals. Neither produces a crash, data corruption, or DNA violation. The engine's safe-default path (returns 1 when `_hasData=false`) ensures correctness during the deferral window.

---

## Overall Verdict

| Check | Result |
|-------|--------|
| Check 1 — AtrSizingEngine.cs correctness | ✅ PASS |
| Check 2 — CopyEngine.cs T1 additions | ✅ PASS |
| Check 3 — TradeCopierAddOn.cs T1 additions | ✅ PASS |
| Check 4 — CopyEngineTests.cs T1 (50 tests, T-B9-01..10) | ✅ PASS |
| Check 5 — 7-Scan independent results | ✅ PASS (all ZERO violations) |
| Check 6 — Spec alignment | ✅ PASS |
| DNA rules (JS-021/023/001/002/008/010/025/033) | ✅ ALL PASS |
| NT8 constraints | ✅ ALL PASS |

**Zero violations. Zero missing methods. Zero DNA rule violations. Zero scan hits in executable code.**

## FINAL VERDICT: VERIFY_PASS
