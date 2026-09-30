# B173 -- AtrWidget Live ATR Fix
# Tickets -- Phase 3
# Author: ptt-architect
# Source plan: docs/brain/B173/02-architecture-plan.md (REVIEW_PASS)
# Status: TICKETS_COMPLETE

---

## Dependency Order

T1 --> T2 --> T3 (strictly sequential; no parallel execution)

- T1 creates AtrCalc.cs. T2 depends on it.
- T2 exposes SetAtrChartControl on TradeCopierPanel. T3 depends on it.
- T3 deletes AtrSizingEngine.cs + runs ptt-sync. Must be last.

---

## TICKET 1 -- Create AtrCalc.cs + Append B173AtrCalcTests to CopyEngineTests.cs

**Spec requirements satisfied:** B173-R1 (AtrCalc pure static helper), B173-R2 (tests for pure math overload)
**Depends on:** nothing (T1 is the root)
**Blocks:** T2 (OnTimerTick calls AtrCalc.Compute)

### Files Written

| File | Action |
|------|--------|
| `src/PropTraderTools/AtrCalc.cs` | NEW |
| `src/PropTraderTools/CopyEngineTests.cs` | APPEND (new class at end of file, before final `}` at line 10221) |

---

### 1A -- AtrCalc.cs (NEW FILE)

**Full file content to create:**

```
// AtrCalc.cs
// B173: Pure static ATR calculator. No NinjaScript pipeline dependency.
// Replaces AtrSizingEngine for the AtrWidget polling path.
// JS-001: both Compute overloads wrapped in try/catch, return 0.0 on exception.
// JS-021: no lock anywhere. Static class has no instance state.
// JS-023: no volatile fields (no fields at all).
// NT8-003: no volatile double.
// ASCII-only: no Unicode string literals.
using System;
using NinjaTrader.Gui.Chart;

namespace PropTraderTools
{
    // CYC=N/A -- static class, no instance branches
    internal static class AtrCalc
    {
        // --- Compute (pure math overload) ---
        // Primary testable overload. No NT8 runtime dependency.
        // Array ordering: index 0 = oldest bar, index length-1 = most recent bar (chronological).
        // Formula: TR[i] = Max(hi[i]-lo[i], Abs(hi[i]-pc[i-1]), Abs(lo[i]-pc[i-1]))
        //          ATR    = Sum(TR[1..period]) / period
        // Guards:
        //   - null arrays       -> return 0.0
        //   - period < 1        -> return 0.0
        //   - any array length < period+1 -> return 0.0 (need period TR values; each TR needs closes[i-1])
        // JS-001: try/catch -> return 0.0. CYC target: <= 6.
        internal static double Compute(double[] highs, double[] lows, double[] closes, int period)
        {
            try
            {
                if (highs == null || lows == null || closes == null)
                    return 0.0;
                if (period < 1)
                    return 0.0;
                if (highs.Length < period + 1 || lows.Length < period + 1 || closes.Length < period + 1)
                    return 0.0;
                double sum = 0.0;
                for (int i = 1; i <= period; i++)
                {
                    double pc = closes[i - 1];
                    double tr = Math.Max(
                        highs[i] - lows[i],
                        Math.Max(Math.Abs(highs[i] - pc), Math.Abs(lows[i] - pc))
                    );
                    sum += tr;
                }
                return sum / period;
            }
            catch (Exception)
            {
                return 0.0;
            }
        }

        // --- Compute (NT8 runtime overload) ---
        // Extracts OHLC arrays from ChartControl and delegates to the pure math overload.
        // ENGINEER MUST VERIFY NT8 API before writing (see note below).
        // Guards:
        //   - cc == null                              -> return 0.0
        //   - cc.BarsArray == null or Count == 0      -> return 0.0
        //   - bars.Count < period + 1                 -> return 0.0
        // Array ordering: k=0 -> barsAgo=period (oldest), k=period -> barsAgo=0 (newest).
        //   For k = 0 to period (inclusive):
        //       barsAgo  = period - k
        //       highs[k] = bars.GetHigh(barsAgo)
        //       lows[k]  = bars.GetLow(barsAgo)
        //       closes[k]= bars.GetClose(barsAgo)
        // JS-001: try/catch -> return 0.0. CYC target: <= 5.
        //
        // ENGINEER NOTE -- NT8 API VERIFICATION REQUIRED BEFORE WRITING THIS METHOD:
        //   1. Confirm ChartControl.BarsArray property type (expected: IList<ChartBars> or similar)
        //   2. Confirm ChartBars.Bars accessor (may be .Bars property or direct indexer)
        //   3. Confirm Bars.GetHigh(int barsAgo), GetLow(int barsAgo), GetClose(int barsAgo) exist
        //      with exactly this signature.
        //   4. If API differs, use correct form and update docs/standards/NT8_FULL_REFERENCE.md before
        //      marking this ticket complete.
        //   5. Fallback if GetHigh/GetLow/GetClose do not compile:
        //      use indexer form: bars[barsAgo].High / .Low / .Close
        //   6. If BarsArray accessor is different (e.g. cc.ChartBars or similar), update accordingly.
        //      SCAN-05 (dotnet build) must produce 0 errors -- this is the acceptance gate.
        internal static double Compute(NinjaTrader.Gui.Chart.ChartControl cc, int period)
        {
            try
            {
                if (cc == null)
                    return 0.0;
                if (cc.BarsArray == null || cc.BarsArray.Count == 0)
                    return 0.0;
                var bars = cc.BarsArray[0].Bars;
                if (bars == null || bars.Count < period + 1)
                    return 0.0;
                double[] highs  = new double[period + 1];
                double[] lows   = new double[period + 1];
                double[] closes = new double[period + 1];
                for (int k = 0; k <= period; k++)
                {
                    int barsAgo = period - k;
                    highs[k]  = bars.GetHigh(barsAgo);
                    lows[k]   = bars.GetLow(barsAgo);
                    closes[k] = bars.GetClose(barsAgo);
                }
                return Compute(highs, lows, closes, period);
            }
            catch (Exception)
            {
                return 0.0;
            }
        }
    }
}
```

**Method signatures (exact, engineer contract):**

```csharp
internal static double Compute(double[] highs, double[] lows, double[] closes, int period)
internal static double Compute(NinjaTrader.Gui.Chart.ChartControl cc, int period)
```

**CYC targets:**
- `Compute(double[], double[], double[], int)` -- CYC <= 6 (null guards x3, period guard, for loop, try/catch)
- `Compute(ChartControl, int)` -- CYC <= 5 (null guards x3, for loop, try/catch)

**JS rule constraints:**
- JS-001: both overloads: entire body in try/catch, return 0.0 on any exception. No throw, no rethrow.
- JS-021: no lock() anywhere. Static class has no shared mutable state.
- JS-023: no volatile fields. Static class has no instance fields.
- NT8-003: no volatile double. Not applicable (no fields).
- ASCII-only: all string literals (comments aside) must be ASCII-only. No Unicode characters.

---

### 1B -- CopyEngineTests.cs (APPEND)

**Location:** Append new class immediately before the final `}` on line 10221.
Insert after line 10219 (after the closing `}` of `B172AtrWidgetTests`), before line 10221 (the final namespace `}`).

**Exact insertion point:** after line 10219, before line 10221.

**Content to append:**

```csharp
    // =====================================================================
    // B173: AtrCalc static helper tests (T-B173-T1-01, T-B173-T1-02)
    // Pure arithmetic -- no NT8 runtime required. No [Skip].
    // Standalone class: no CopyEngine.Instance dependency.
    // =====================================================================
    public class B173AtrCalcTests
    {
        // T-B173-T1-01: Guard -- arrays shorter than period+1 return 0.0.
        // Input: arrays of length 3 with period=14. Need period+1=15 bars minimum. Expects 0.0.
        [Fact]
        public void AtrCalc_Compute_ReturnsZero_WhenInsufficientBars()
        {
            double result = AtrCalc.Compute(new double[3], new double[3], new double[3], 14);
            Assert.Equal(0.0, result);
        }

        // T-B173-T1-02: Known-value ATR(3) verification.
        // Array ordering: index 0 = oldest bar.
        // highs  = [2.0, 3.0, 4.0, 3.0]
        // lows   = [1.0, 2.0, 2.0, 1.0]
        // closes = [1.5, 2.5, 3.0, 2.0]
        // TR[1]: max(3-2=1.0, |3-1.5|=1.5, |2-1.5|=0.5) = 1.5
        // TR[2]: max(4-2=2.0, |4-2.5|=1.5, |2-2.5|=0.5) = 2.0
        // TR[3]: max(3-1=2.0, |3-3.0|=0.0, |1-3.0|=2.0) = 2.0
        // ATR(3) = (1.5 + 2.0 + 2.0) / 3 = 5.5 / 3
        [Fact]
        public void AtrCalc_Compute_CorrectValue_KnownBars()
        {
            double[] highs  = { 2.0, 3.0, 4.0, 3.0 };
            double[] lows   = { 1.0, 2.0, 2.0, 1.0 };
            double[] closes = { 1.5, 2.5, 3.0, 2.0 };
            double result = AtrCalc.Compute(highs, lows, closes, 3);
            Assert.Equal(5.5 / 3.0, result);
        }
    }
```

**xUnit [Fact] test names:**

| Test ID | Method Name | Asserts |
|---------|-------------|---------|
| T-B173-T1-01 | `AtrCalc_Compute_ReturnsZero_WhenInsufficientBars` | `Compute(new double[3], new double[3], new double[3], 14)` returns `0.0` (guard: length 3 < period+1=15) |
| T-B173-T1-02 | `AtrCalc_Compute_CorrectValue_KnownBars` | ATR(3) of hand-computed OHLC equals exactly `5.5 / 3.0` (formula verification) |

**No [Skip] attribute** -- pure arithmetic, no NT8 runtime required.

---

### T1 -- 7-SCAN CHECKLIST

Engineer must run all scans and confirm zero violations before marking T1 complete.

| # | Command | Expected Result | Notes |
|---|---------|-----------------|-------|
| SCAN-01 | `grep -r "lock(" src/PropTraderTools/` | **0 matches** | AtrCalc.cs has no lock |
| SCAN-02 | `grep -r "volatile double" src/PropTraderTools/` | **0 matches** | No volatile double in new file |
| SCAN-03 | `grep -rP "[^\x00-\x7F]" src/PropTraderTools/` | **0 matches** | ASCII-only in new file |
| SCAN-04 | `grep -rn "AtrSizingEngine" src/PropTraderTools/` | **> 0 matches expected** | AtrSizingEngine.cs still exists at T1; SCAN-04 passes at T3 only |
| SCAN-05 | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | **0 errors** | AtrCalc.cs must compile; NT8 API verification required for ChartControl overload |
| SCAN-06 | `dotnet test src/PropTraderTools/PropTraderTools.csproj` | **all pass** | T-B173-T1-01 and T-B173-T1-02 must pass; all prior tests must remain passing |
| SCAN-07 | N/A at T1 | **skip** | ptt-sync required at T3 only (file delete) |

**SCAN-04 note:** At T1, `AtrSizingEngine` references still exist in `TradeCopierPanel.cs` and `TradeCopierAddOn.cs`. This is expected at T1. SCAN-04 zero-match requirement is enforced at T3 (after all references are removed and the file is deleted).

---

## TICKET 2 -- TradeCopierPanel.cs Surgical Edit (AtrWidget + outer class)

**Spec requirements satisfied:** B173-R3 (replace _provider pattern with _chartControl + AtrCalc)
**Depends on:** T1 complete (AtrCalc.cs must exist for `AtrCalc.Compute` call in `OnTimerTick`)
**Blocks:** T3 (TradeCopierAddOn.cs calls `panel.SetAtrChartControl`)

### Files Written

| File | Action |
|------|--------|
| `src/PropTraderTools/TradeCopierPanel.cs` | SURGICAL EDIT |

---

### 2A -- AtrWidget Inner Class Changes

**Location in file:** lines ~3155-3291 (confirmed in source read).

#### Change 2A-1: Remove _provider field, add _chartControl field

**REMOVE** (line 3168 -- exact text):
```csharp
            private AtrSizingEngine _provider;         // stripped engine reference; null until wired
```

**ADD** in its place:
```csharp
            private ChartControl _chartControl;        // live chart; null until wired (B173)
```

`ChartControl` is already in scope via `using NinjaTrader.Gui.Chart;` at line 109 of the file. Use the unqualified name.

#### Change 2A-2: Remove SetProvider(), add SetChartControl()

**REMOVE** entire `SetProvider` method (lines ~3207-3226 -- exact text):
```csharp
            // --- SetProvider ---
            // Called from TradeCopierPanel.SetAtrProvider (which is called from TradeCopierAddOn.WireNewPanel).
            // Stores engine ref and starts/restarts the polling timer. CYC=2: null guard + timer create-once.
            // UI thread only (called from Dispatcher.InvokeAsync context).
            internal void SetProvider(AtrSizingEngine engine)
            {
                _provider = engine;
                if (_atrPollTimer == null)
                {
                    _atrPollTimer = new DispatcherTimer(DispatcherPriority.Background)
                    {
                        Interval = System.TimeSpan.FromSeconds(1),
                    };
                    _atrPollTimer.Tick += OnTimerTick;
                }
                if (engine != null)
                    _atrPollTimer.Start();
                else
                    _atrPollTimer.Stop();
            }
```

**ADD** replacement in its place:
```csharp
            // --- SetChartControl ---
            // Called from TradeCopierPanel.SetAtrChartControl (called from TradeCopierAddOn.WireNewPanel).
            // Stores ChartControl ref and starts/restarts the polling timer. CYC=2: null guard + timer create-once.
            // UI thread only (called from Dispatcher.InvokeAsync context).
            internal void SetChartControl(ChartControl cc)
            {
                _chartControl = cc;
                if (_atrPollTimer == null)
                {
                    _atrPollTimer = new DispatcherTimer(DispatcherPriority.Background)
                    {
                        Interval = System.TimeSpan.FromSeconds(1),
                    };
                    _atrPollTimer.Tick += OnTimerTick;
                }
                if (cc != null)
                    _atrPollTimer.Start();
                else
                    _atrPollTimer.Stop();
            }
```

**CYC=2.** UI thread only. No lock.

#### Change 2A-3: Update Detach() -- clear _chartControl

**In `Detach()` (lines ~3228-3239), CHANGE** the `_provider = null;` line only:

**REMOVE** (exact text within Detach):
```csharp
                _provider = null;
```

**REPLACE WITH:**
```csharp
                _chartControl = null;
```

**Full updated Detach() for reference (no other changes):**
```csharp
            // --- Detach ---
            // Called from TradeCopierPanel.Detach() on chart close.
            // Stops timer and clears ChartControl ref. CYC=2: null guards.
            internal void Detach()
            {
                if (_atrPollTimer != null)
                {
                    _atrPollTimer.Stop();
                    _atrPollTimer = null;
                }
                _chartControl = null;
            }
```

#### Change 2A-4: Replace OnTimerTick() entirely

**REMOVE** entire `OnTimerTick` method (lines ~3241-3261 -- exact text):
```csharp
            // --- OnTimerTick ---
            // DispatcherTimer fires on UI thread. CYC=6 (per WARN-T2-01: base + if + catch + if+&& + if).
            // JS-021: no lock. JS-023: _hasData volatile bool read. NT8-003 PASS: no volatile double.
            private void OnTimerTick(object sender, EventArgs e)
            {
                if (_provider == null)
                    return; // guard (1): provider not wired yet
                try
                {
                    _provider.ManualOnBarUpdate(); // may throw if NT8 context not ready; swallow
                }
                catch (System.Exception)
                { /* NT8 context not ready; next tick will retry */ }
                if (!_hasData && _provider.HasData)
                    _hasData = true; // latch: set once, never cleared during session
                if (_hasData) // guard (2): display only after first valid bar
                {
                    _lastAtr = _provider.GetLastAtr();
                    RecalcDisplay();
                }
            }
```

**ADD** replacement in its place:
```csharp
            // --- OnTimerTick ---
            // DispatcherTimer fires on UI thread. CYC=3 (base + if-null + if-atr>0 + if-hasData).
            // JS-021: no lock. JS-023: _hasData volatile bool read. NT8-003 PASS: no volatile double.
            // AtrCalc.Compute is JS-001 safe (never throws) -- no try/catch needed here.
            private void OnTimerTick(object sender, EventArgs e)
            {
                if (_chartControl == null)
                    return; // guard (1): ChartControl not wired yet
                double atr = AtrCalc.Compute(_chartControl, 14);
                if (atr > 0.0)
                {
                    _lastAtr = atr;   // non-volatile: sizing hint only (NT8-003 PASS)
                    _hasData = true;  // latch: set once, never cleared (volatile bool JS-023 PASS)
                }
                if (_hasData)         // guard (2): display only after first valid bar
                    RecalcDisplay();
            }
```

**CYC=3.** Improved from prior CYC=6. No try/catch (AtrCalc absorbs exceptions per JS-001). No lock.

---

### 2B -- Outer TradeCopierPanel Class Changes

**Location in file:** lines ~3077-3082 (confirmed in source read).

#### Change 2B-1: Remove SetAtrProvider(), add SetAtrChartControl()

**REMOVE** entire `SetAtrProvider` method (lines ~3077-3082 -- exact text):
```csharp
        // B172: SetAtrProvider -- called from TradeCopierAddOn.WireNewPanel after creating stripped engine.
        // CYC=1: delegation. JS-021: no lock. UI thread only (called from Dispatcher.InvokeAsync).
        internal void SetAtrProvider(AtrSizingEngine engine)
        {
            _widget?.SetProvider(engine);
        }
```

**ADD** replacement in its place:
```csharp
        // B173: SetAtrChartControl -- called from TradeCopierAddOn.WireNewPanel after obtaining ChartControl.
        // CYC=1: delegation. JS-021: no lock. UI thread only (called from Dispatcher.InvokeAsync).
        internal void SetAtrChartControl(ChartControl cc)
        {
            _widget?.SetChartControl(cc);
        }
```

**CYC=1.** No lock. UI thread only.

---

### T2 -- Post-Edit Verification

After all 4 changes above:

- **No `using` or reference to `AtrSizingEngine` should remain** in `TradeCopierPanel.cs`. Verify:
  `grep -n "AtrSizingEngine" src/PropTraderTools/TradeCopierPanel.cs` → 0 matches.
- **`_lastAtr` remains non-volatile double** (field at line ~3165 -- do NOT add volatile). NT8-003 PASS.
- **`_hasData` remains `volatile bool`** (field at line ~3167 -- do NOT change). JS-023 PASS.
- **Period `14` is hardcoded** in `OnTimerTick` call: `AtrCalc.Compute(_chartControl, 14)`. No Period property added.

**Method signatures delivered (exact, engineer contract):**

```csharp
// AtrWidget inner class:
internal void SetChartControl(ChartControl cc)
internal void Detach()                                        // updated (no signature change)
private void OnTimerTick(object sender, EventArgs e)          // updated (no signature change)

// TradeCopierPanel outer class:
internal void SetAtrChartControl(ChartControl cc)
```

**JS rule constraints:**
- JS-001: `OnTimerTick` has no try/catch -- permitted because `AtrCalc.Compute` is JS-001 safe (never throws). No new throw sites.
- JS-021: no lock() in any changed method.
- JS-023: `_hasData` volatile bool kept as-is. `_chartControl` non-volatile (UI thread only). `_lastAtr` non-volatile.
- NT8-003: `_lastAtr` remains non-volatile double. No new volatile double field introduced.
- ASCII-only: all new string literals in comments and code are ASCII-only.

---

### T2 -- 7-SCAN CHECKLIST

| # | Command | Expected Result | Notes |
|---|---------|-----------------|-------|
| SCAN-01 | `grep -r "lock(" src/PropTraderTools/` | **0 matches** | No lock in any changed method |
| SCAN-02 | `grep -r "volatile double" src/PropTraderTools/` | **0 matches** | _lastAtr remains non-volatile |
| SCAN-03 | `grep -rP "[^\x00-\x7F]" src/PropTraderTools/` | **0 matches** | ASCII-only in all changes |
| SCAN-04 | `grep -rn "AtrSizingEngine" src/PropTraderTools/` | **> 0 expected** | TradeCopierAddOn.cs still references it; zero only at T3 |
| SCAN-05 | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | **0 errors** | TradeCopierPanel.cs must compile with AtrCalc reference; depends on T1 |
| SCAN-06 | `dotnet test src/PropTraderTools/PropTraderTools.csproj` | **all pass** | All T1 tests + prior tests must remain passing |
| SCAN-07 | N/A at T2 | **skip** | ptt-sync required at T3 only |

**SCAN-04 note:** `AtrSizingEngine` still exists and is referenced in `TradeCopierAddOn.cs` at T2. This is expected. The file is deleted at T3.

---

## TICKET 3 -- TradeCopierAddOn.cs WireNewPanel + Delete AtrSizingEngine.cs + ptt-sync

**Spec requirements satisfied:** B173-R4 (WireNewPanel wires ChartControl), B173-R5 (AtrSizingEngine deleted), B173-R6 (DW-B172-01 root cause eliminated)
**Depends on:** T2 complete (`panel.SetAtrChartControl` must exist before calling it)
**Blocks:** nothing (T3 is the final ticket)

### Files Written

| File | Action |
|------|--------|
| `src/PropTraderTools/TradeCopierAddOn.cs` | SURGICAL EDIT -- WireNewPanel only |
| `src/PropTraderTools/AtrSizingEngine.cs` | DELETE |
| `scripts/ptt-sync.ps1` | RUN (not edited) |

---

### 3A -- TradeCopierAddOn.cs: WireNewPanel

**Location in file:** lines 396-399 (confirmed in source read).

**Current code (exact text, lines 396-399):**
```csharp
            var instr = TrySetPanelInstrument(chartTrader, panel);
            var engine = new AtrSizingEngine();
            panel.SetAtrProvider(engine);
            panel.SetChart(chart);
```

**New code (exact replacement):**
```csharp
            var instr = TrySetPanelInstrument(chartTrader, panel);
            var cc = FindVisualChild<ChartControl>(chart);
            panel.SetAtrChartControl(cc);
            panel.SetChart(chart);
```

**Change breakdown:**
- Line 396: `var instr = TrySetPanelInstrument(chartTrader, panel);` -- **UNCHANGED**
- Line 397: `var engine = new AtrSizingEngine();` -- **DELETE**
- Line 398: `panel.SetAtrProvider(engine);` -- **DELETE**
- Line 397 (new): `var cc = FindVisualChild<ChartControl>(chart);` -- **ADD**
- Line 398 (new): `panel.SetAtrChartControl(cc);` -- **ADD**
- Line 399: `panel.SetChart(chart);` -- **UNCHANGED** (do NOT remove)

**Constraints:**
- `FindVisualChild<ChartControl>` is an existing static method on `TradeCopierAddOn` (confirmed at lines 492-503 in source). No new helper needed.
- `ChartControl` is already in scope via `using NinjaTrader.Gui.Chart;` at line 28. No new using directive needed.
- `cc` may be null if the visual tree is not yet loaded. `AtrWidget.SetChartControl(null)` safely stops the timer. This is the correct null path -- no null guard needed in WireNewPanel.
- Do NOT double-call `FindVisualChild<ChartControl>` if it was already called earlier in `WireNewPanel`. Inspect the full method body and reuse the existing local if present. (Confirmed from source read: no prior `FindVisualChild<ChartControl>` call exists in WireNewPanel before line 396 -- the call on line 213 is in a different method. A new `var cc` assignment is correct here.)
- WireNewPanel CCN remains CCN=5 (no new branch added; `cc` assignment is straight-line).

---

### 3B -- Delete AtrSizingEngine.cs

**Action:** Delete the file `src/PropTraderTools/AtrSizingEngine.cs` entirely.

**Method:** Use `Remove-Item src/PropTraderTools/AtrSizingEngine.cs -Force` (PowerShell) or equivalent git-tracked delete.

**Post-delete verification:**
```
grep -rn "AtrSizingEngine" src/PropTraderTools/
```
Expected: **0 matches**. If any match remains, locate and remove it before proceeding.

**Context:** The file is 65 lines (`public class AtrSizingEngine : Indicator`). It is entirely replaced by `AtrCalc.cs`. No partial retention.

---

### 3C -- Run ptt-sync.ps1

**Command:**
```powershell
powershell -File .\scripts\ptt-sync.ps1
```

**Expected:** 0 errors. Hard link count reported (typically 18 linked files).

**Purpose:** File deletion of `AtrSizingEngine.cs` requires hard link resynchronization. ptt-sync must run after the delete and after the build confirms zero errors, not before.

**Order of operations for T3:**
1. Apply WireNewPanel edit (3A)
2. Delete `AtrSizingEngine.cs` (3B)
3. Run `dotnet build` (SCAN-05) -- must be 0 errors
4. Run `dotnet test` (SCAN-06) -- must be all pass
5. Verify `grep -rn "AtrSizingEngine" src/PropTraderTools/` -- 0 matches (SCAN-04)
6. Run `ptt-sync.ps1` (SCAN-07) -- 0 errors

---

### T3 -- Post-Edit Verification

After all changes:
- `grep -rn "AtrSizingEngine" src/PropTraderTools/` → **0 matches** (file deleted, all references removed)
- `panel.SetChart(chart)` at line 399 is **still present** (not removed)
- No `using PropTraderTools;` or `AtrSizingEngine`-specific import remains in `TradeCopierAddOn.cs` (there was no explicit using for AtrSizingEngine -- it is in the same namespace `PropTraderTools` -- but confirm no stale comment or reference remains)

**Method signatures delivered (exact, engineer contract):**

No new public/internal methods are added to `TradeCopierAddOn.cs` by T3. The only change is removal of 2 lines and addition of 2 lines within `WireNewPanel`.

**JS rule constraints:**
- JS-001: no new throw sites in WireNewPanel.
- JS-021: no lock() added. WireNewPanel already has no lock.
- ASCII-only: `var cc = FindVisualChild<ChartControl>(chart);` and `panel.SetAtrChartControl(cc);` are ASCII-only.

---

### T3 -- 7-SCAN CHECKLIST

All 7 scans must pass at T3 before marking complete.

| # | Command | Expected Result | Enforcement |
|---|---------|-----------------|-------------|
| SCAN-01 | `grep -r "lock(" src/PropTraderTools/` | **0 matches** | Zero tolerance |
| SCAN-02 | `grep -r "volatile double" src/PropTraderTools/` | **0 matches** | Zero tolerance |
| SCAN-03 | `grep -rP "[^\x00-\x7F]" src/PropTraderTools/` | **0 matches** | ASCII-only enforced |
| SCAN-04 | `grep -rn "AtrSizingEngine" src/PropTraderTools/` | **0 matches** | File deleted + all references removed |
| SCAN-05 | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | **0 errors** | Build gate |
| SCAN-06 | `dotnet test src/PropTraderTools/PropTraderTools.csproj` | **all pass** | Test gate (T1 tests + all prior) |
| SCAN-07 | `powershell -File .\scripts\ptt-sync.ps1` | **0 errors** | Hard link resync after file delete |

**SCAN-04 is the terminal gate.** Any AtrSizingEngine reference remaining after T3 = ticket incomplete.

---

## Deferred Backlog Update

| ID | Description | Prior Status | Status After B173 |
|----|-------------|--------------|-------------------|
| DW-B172-01 | SIM GATE: Director must verify AtrWidget displays live ATR data in NT8 | OPEN | **PENDING SIM GATE** -- root cause eliminated by B173. Pending Director SIM confirmation. |

All other open backlog items (DW-B170-SIM, DW-B169-K1, DW-B169-K2, B167-DEFER-02, etc.) are unaffected by B173.

---

## Summary

| Ticket | Files | Action | Dependency |
|--------|-------|--------|------------|
| T1 | `AtrCalc.cs` (NEW), `CopyEngineTests.cs` (APPEND) | Create static ATR helper + 2 xUnit tests | none |
| T2 | `TradeCopierPanel.cs` (SURGICAL) | Replace _provider/SetProvider with _chartControl/SetChartControl; update OnTimerTick | T1 |
| T3 | `TradeCopierAddOn.cs` (SURGICAL), `AtrSizingEngine.cs` (DELETE), ptt-sync (RUN) | Wire ChartControl in WireNewPanel; delete AtrSizingEngine | T2 |

**Period 14 is hardcoded** in `OnTimerTick` at `AtrCalc.Compute(_chartControl, 14)`. No Period field or property is added anywhere.

*Tickets written: B173 Phase 3. Ready for ptt-ticket-reviewer (Phase 3.5).*
