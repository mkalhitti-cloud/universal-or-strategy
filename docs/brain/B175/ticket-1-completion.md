# Ticket B175-1 Completion Report
# Phase 4a — Engineer
# File modified: src/PropTraderTools/AtrCalc.cs
# Date: 2025-01-27

---

## Status: BUILD_PASS

---

## Changes Applied

All three changes applied via `apply_diff` (no `write_file` used on .cs files).

---

### Change A — Replace Compute(double[]) body with Wilder's smoothing

**SEARCH** (lines 16–52 original):
```
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
        { ... double sum = 0.0; ... return sum / period; ... }
```

**REPLACE**: Wilder's seed+smoothing algorithm:
- Seed = simple average of first `period` TR values (indices 1..period)
- If `length >= 2*period+1`: apply Wilder's formula `atr = (atr*(period-1)+tr)/period` for indices `period+1..2*period`
- Else: return seed (simple avg fallback)

---

### Change B — Replace bar-read block in NT8 ChartControl overload

**SEARCH** (lines 94–106 original):
```
                if (bars == null || bars.Count < period + 1)
                    return 0.0;
                double[] highs  = new double[period + 1];
                double[] lows   = new double[period + 1];
                double[] closes = new double[period + 1];
                for (int k = 0; k <= period; k++) { ... }
                return Compute(highs, lows, closes, period);
```

**REPLACE**: Read up to `2*period+1` bars when available; delegate to new helper:
```csharp
                int readLen = bars.Count >= 2 * period + 1 ? 2 * period + 1 : period + 1;
                ReadBarsToArrays(bars, readLen, out double[] highs, out double[] lows, out double[] closes);
                return Compute(highs, lows, closes, period);
```

---

### Change C — Add ReadBarsToArrays private static helper

Inserted after the NT8 Compute overload closing brace, before the class closing brace.

```csharp
        private static void ReadBarsToArrays(
            NinjaTrader.Data.Bars bars,
            int readLen,
            out double[] highs,
            out double[] lows,
            out double[] closes)
        {
            highs  = new double[readLen];
            lows   = new double[readLen];
            closes = new double[readLen];
            for (int k = 0; k < readLen; k++)
            {
                int barsAgo = readLen - 1 - k;
                highs[k]  = bars.GetHigh(barsAgo);
                lows[k]   = bars.GetLow(barsAgo);
                closes[k] = bars.GetClose(barsAgo);
            }
        }
```

---

## CYC Verification Table

| Method | CYC Before | CYC After | Delta | Compliant (<=8) |
|---|---|---|---|---|
| `AtrCalc.Compute(double[],double[],double[],int)` | 6 | 8 | +2 | YES |
| `AtrCalc.Compute(ChartControl,int,string)` | 8 | 8 | 0 | YES |
| `AtrCalc.ReadBarsToArrays` (NEW) | — | 2 | +2 | YES |

**CYC accounting for `Compute(double[])`**:
base(1) + null check(1) + period<1(1) + length guard(1) + seed loop(1) + Wilder's if(1) + smooth loop(1) + catch(1) = 8

**CYC accounting for `Compute(ChartControl)`**:
Removed: k-loop (was +1). Added: ternary `?:` (+1). Net delta = 0. Total stays 8.

**CYC accounting for `ReadBarsToArrays`**:
base(1) + for loop(1) = 2

---

## 7-Scan Results

### SCAN-01 — JS-001: try/catch present in all modified methods
```powershell
Select-String -Path "src/PropTraderTools/AtrCalc.cs" -Pattern "\btry\b" | Select-Object LineNumber, Line
```
Output:
```
LineNumber  Line
----------  ----
         3  // JS-001: both Compute overloads wrapped in try/catch, return 0.0 on exception.
        25              try
        88              try
       121          // CYC=2 (base=1 + for=1). JS-001: called inside outer try/catch. ASCII-only.
```
**RESULT: PASS** — `Compute(double[])` has try at line 25; `Compute(ChartControl)` has try at line 88. `ReadBarsToArrays` is called inside outer try/catch (void helper, per JS-001 protocol no own catch needed).

---

### SCAN-02 — ASCII-only: no non-ASCII characters
```powershell
Select-String -Path "src/PropTraderTools/AtrCalc.cs" -Pattern "[^\x00-\x7F]"
```
Output: (no output)
**RESULT: PASS** — 0 hits

---

### SCAN-03 — No FontFamily
```powershell
Select-String -Path "src/PropTraderTools/AtrCalc.cs" -Pattern "FontFamily"
```
Output: (no output)
**RESULT: PASS** — 0 hits

---

### SCAN-04 — No hex color literals
```powershell
Select-String -Path "src/PropTraderTools/AtrCalc.cs" -Pattern "#[0-9A-Fa-f]{6}"
```
Output: (no output)
**RESULT: PASS** — 0 hits

---

### SCAN-05 — No CreateOrder without PTT- prefix (N/A — no CreateOrder calls)
```powershell
Select-String -Path "src/PropTraderTools/AtrCalc.cs" -Pattern "CreateOrder"
```
Output: (no output)
**RESULT: PASS** — 0 hits (N/A for this file)

---

### SCAN-06 — No DateTime.Now
```powershell
Select-String -Path "src/PropTraderTools/AtrCalc.cs" -Pattern "DateTime\.Now[^U]"
```
Output: (no output)
**RESULT: PASS** — 0 hits

---

### SCAN-07 — No lock()
```powershell
Select-String -Path "src/PropTraderTools/AtrCalc.cs" -Pattern "\block\s*\("
```
Output: (no output)
**RESULT: PASS** — 0 hits

---

## Verification Greps

### sum / period must equal 0
```powershell
(Select-String -Path "src/PropTraderTools/AtrCalc.cs" -Pattern "sum / period").Count
```
Output: `0`
**RESULT: PASS**

### ReadBarsToArrays must be >= 2
```powershell
(Select-String -Path "src/PropTraderTools/AtrCalc.cs" -Pattern "ReadBarsToArrays").Count
```
Output: `2`
**RESULT: PASS** — definition + call site = 2

---

## Jane Street Compliance Summary

| Rule | Result |
|---|---|
| JS-001 (try/catch / safe return) | PASS |
| JS-021 (no lock) | PASS |
| JS-023 (no volatile double) | PASS |
| ASCII-only | PASS |
| CYC <=8 per method | PASS |
| No DateTime.Now | PASS |
| No hex colors | PASS |
| No FontFamily | PASS |
| apply_diff used (no write_file on .cs) | PASS |
| ptt-sync.ps1 NOT run (runs after B175-2) | CONFIRMED |

---

## BUILD_PASS
