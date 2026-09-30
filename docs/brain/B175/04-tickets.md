# Tickets — Block B175
# Defects: DW-B175-ATR-FORMULA + DW-B175-ATR-TEXTBOX-FOCUS
# Phase: 3 — Ticket Generation
# Author: ptt-architect
# Source plan: docs/brain/B175/02-architecture-plan.md (REVIEW_PASS — 0 violations)
# Date: 2025-01-27

---

## Ticket B175-1 — Wilder's ATR Formula (AtrCalc.cs)

**Scope lock**: `src/PropTraderTools/AtrCalc.cs` ONLY. No other file touched in this ticket.

**Spec requirement**: DW-B175-ATR-FORMULA

**File**: `src/PropTraderTools/AtrCalc.cs`

---

### Change A — Replace simple-average body with Wilder's smoothing

**Lines modified**: 26–52 (entire `Compute(double[], double[], double[], int)` method body)

**BEFORE** (lines 26–52, exact):
```csharp
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
```

**AFTER** (exact replacement, lines 26–52):
```csharp
        // B175: Wilder's ATR. Seed = simple avg of first period TRs; then smooth.
        // Requires length >= 2*period+1 for full Wilder's; falls back to simple avg
        // when length < 2*period+1 (short history: period+1 bars minimum unchanged).
        // CYC=8: null(1)+period<1(2)+length<period+1(3)+seed loop(4)+
        //        Wilder's length check(5)+smooth loop(6)+simple-fallback path(7)+catch(8).
        // JS-001 PASS. JS-021: no lock. ASCII-only.
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
                // Seed: simple average of first period TR values (indices 1..period)
                double atr = 0.0;
                for (int i = 1; i <= period; i++) // seed loop (4)
                {
                    double pc = closes[i - 1];
                    atr += Math.Max(
                        highs[i] - lows[i],
                        Math.Max(Math.Abs(highs[i] - pc), Math.Abs(lows[i] - pc))
                    );
                }
                atr /= period;
                // Wilder's smoothing: apply if array is long enough (2*period+1 values)
                if (highs.Length >= 2 * period + 1) // Wilder's length check (5)
                {
                    for (int i = period + 1; i <= 2 * period; i++) // smooth loop (6)
                    {
                        double pc = closes[i - 1];
                        double tr = Math.Max(
                            highs[i] - lows[i],
                            Math.Max(Math.Abs(highs[i] - pc), Math.Abs(lows[i] - pc))
                        );
                        atr = (atr * (period - 1) + tr) / period;
                    }
                }
                // else: simple average already computed -- return seed as-is (fallback)
                return atr;
            }
            catch (Exception)
            {
                return 0.0;
            }
        }
```

**apply_diff spec** — use `apply_diff` on `src/PropTraderTools/AtrCalc.cs`:
```
<<<<<<< SEARCH
:start_line:17
-------
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
=======
        // B175: Wilder's ATR. Seed = simple avg of first period TRs; then smooth.
        // Requires length >= 2*period+1 for full Wilder's; falls back to simple avg
        // when length < 2*period+1 (short history: period+1 bars minimum unchanged).
        // CYC=8: null(1)+period<1(2)+length<period+1(3)+seed loop(4)+
        //        Wilder's length check(5)+smooth loop(6)+simple-fallback path(7)+catch(8).
        // JS-001 PASS. JS-021: no lock. ASCII-only.
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
                // Seed: simple average of first period TR values (indices 1..period)
                double atr = 0.0;
                for (int i = 1; i <= period; i++) // seed loop (4)
                {
                    double pc = closes[i - 1];
                    atr += Math.Max(
                        highs[i] - lows[i],
                        Math.Max(Math.Abs(highs[i] - pc), Math.Abs(lows[i] - pc))
                    );
                }
                atr /= period;
                // Wilder's smoothing: apply if array is long enough (2*period+1 values)
                if (highs.Length >= 2 * period + 1) // Wilder's length check (5)
                {
                    for (int i = period + 1; i <= 2 * period; i++) // smooth loop (6)
                    {
                        double pc = closes[i - 1];
                        double tr = Math.Max(
                            highs[i] - lows[i],
                            Math.Max(Math.Abs(highs[i] - pc), Math.Abs(lows[i] - pc))
                        );
                        atr = (atr * (period - 1) + tr) / period;
                    }
                }
                // else: simple average already computed -- return seed as-is (fallback)
                return atr;
            }
            catch (Exception)
            {
                return 0.0;
            }
        }
>>>>>>> REPLACE
```

---

### Change B — Replace bar-read block in NT8 ChartControl overload

**Lines modified**: 94–106 (the allocation + loop block inside the NT8 overload, after the instrument
scan loop and before the closing brace of the try block)

**BEFORE** (lines 94–106, exact):
```csharp
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
```

**AFTER** (exact replacement):
```csharp
                // B175: read up to 2*period+1 bars for Wilder's warmup; fall back to
                // period+1 when chart has fewer bars loaded (Wilder's fallback to simple avg).
                if (bars == null || bars.Count < period + 1)
                    return 0.0;
                int readLen = bars.Count >= 2 * period + 1 ? 2 * period + 1 : period + 1;
                ReadBarsToArrays(bars, readLen, out double[] highs, out double[] lows, out double[] closes);
                return Compute(highs, lows, closes, period);
```

**apply_diff spec** — use `apply_diff` on `src/PropTraderTools/AtrCalc.cs`:
```
<<<<<<< SEARCH
:start_line:94
-------
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
=======
                // B175: read up to 2*period+1 bars for Wilder's warmup; fall back to
                // period+1 when chart has fewer bars loaded (Wilder's fallback to simple avg).
                if (bars == null || bars.Count < period + 1)
                    return 0.0;
                int readLen = bars.Count >= 2 * period + 1 ? 2 * period + 1 : period + 1;
                ReadBarsToArrays(bars, readLen, out double[] highs, out double[] lows, out double[] closes);
                return Compute(highs, lows, closes, period);
>>>>>>> REPLACE
```

---

### Change C — Add ReadBarsToArrays private static helper

**Insert location**: After the closing brace of the NT8 `Compute` overload (currently line 112),
before the closing brace of the `AtrCalc` class (currently line 113).

**BEFORE** (lines 112–114, exact):
```csharp
        }
    }
}
```

**AFTER** (insert new method before the class-closing brace):
```csharp
        }

        // B175: Extract bar OHLC into arrays. readLen bars, oldest-first (index 0 = oldest).
        // CYC=2 (base=1 + for=1). JS-001: called inside outer try/catch. ASCII-only.
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
    }
}
```

**apply_diff spec** — use `apply_diff` on `src/PropTraderTools/AtrCalc.cs`:
```
<<<<<<< SEARCH
:start_line:112
-------
        }
    }
}
=======
        }

        // B175: Extract bar OHLC into arrays. readLen bars, oldest-first (index 0 = oldest).
        // CYC=2 (base=1 + for=1). JS-001: called inside outer try/catch. ASCII-only.
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
    }
}
>>>>>>> REPLACE
```

---

### CYC Verification — B175-1

| Method | CYC Before | CYC After | Delta | Compliant (<=8) |
|---|---|---|---|---|
| `AtrCalc.Compute(double[],double[],double[],int)` | 6 | 8 | +2 | YES |
| `AtrCalc.Compute(ChartControl,int,string)` | 8 | 8 | 0 | YES |
| `AtrCalc.ReadBarsToArrays` (NEW) | — | 2 | +2 | YES |

CYC counting: `Compute(double[])` after: base(1)+null check(1)+period<1(1)+length guard(1)+seed loop(1)+Wilder's if(1)+smooth loop(1)+catch(1)=8. `Compute(ChartControl)`: the k-loop is extracted to helper; ternary `?:` adds +1; net delta=0 (loop removed, ternary added). Total stays 8. `ReadBarsToArrays`: base(1)+for(1)=2.

---

### 7-Scan Checklist — B175-1

- [ ] **JS-001**: try/catch present in all modified methods — `Compute(double[])` has try/catch lines 28/47. `Compute(ChartControl)` has try/catch. `ReadBarsToArrays` called inside outer try/catch (no own catch needed per JS-001; void helper, no fallible return). PASS.
- [ ] **JS-021**: no lock() introduced — zero `lock` statements in all three changes. PASS.
- [ ] **JS-023**: no volatile double — no new fields of any kind introduced. PASS.
- [ ] **ASCII-only**: no Unicode/curly quotes — all comment and string text is 7-bit ASCII printable. "Wilder's" uses straight apostrophe (0x27). PASS.
- [ ] **CYC ≤8 per method**: see CYC table above; all compliant. PASS.
- [ ] **apply_diff used**: engineer MUST use `apply_diff` (three SEARCH/REPLACE blocks above). `write_file` on `.cs` is BANNED by protocol.
- [ ] **ptt-sync.ps1**: NOT run after B175-1 alone — run ONCE after B175-2 (last `.cs` edit). See B175-3.

---

### Verification Greps — B175-1

After applying all three changes:
```powershell
# Must return 0 (simple average removed)
(Select-String -Path "src/PropTraderTools/AtrCalc.cs" -Pattern "sum / period").Count

# Must return >= 2 (helper defined + called)
(Select-String -Path "src/PropTraderTools/AtrCalc.cs" -Pattern "ReadBarsToArrays").Count

# Must return 0 (no lock introduced)
(Select-String -Path "src/PropTraderTools/AtrCalc.cs" -Pattern "lock\(").Count
```

Bash equivalents:
```bash
grep -c "sum / period" src/PropTraderTools/AtrCalc.cs     # expect 0
grep -c "ReadBarsToArrays" src/PropTraderTools/AtrCalc.cs # expect >= 2
grep -c "lock(" src/PropTraderTools/AtrCalc.cs            # expect 0
```

---

---

## Ticket B175-2 — ATR TextBox Focus Fix (TradeCopierPanel.cs)

**Scope lock**: `src/PropTraderTools/TradeCopierPanel.cs` ONLY. No other file touched in this ticket.

**Spec requirement**: DW-B175-ATR-TEXTBOX-FOCUS

**File**: `src/PropTraderTools/TradeCopierPanel.cs`

**Prerequisite**: B175-1 must be applied first (file dependency ordering; `TradeCopierPanel.cs` is independent but pipeline is sequential).

---

### Change A — Add PreviewKeyDown subscription in BuildAtrWidgetRows

**Line modified**: Line 3212 (insert new line after existing LostFocus subscription)

**BEFORE** (line 3212, exact):
```csharp
                _atrFractionBox.LostFocus += OnAtrFractionLostFocus;
```

**AFTER**:
```csharp
                _atrFractionBox.LostFocus += OnAtrFractionLostFocus;
                _atrFractionBox.PreviewKeyDown += OnAtrBoxPreviewKeyDown;
```

**apply_diff spec** — use `apply_diff` on `src/PropTraderTools/TradeCopierPanel.cs`:
```
<<<<<<< SEARCH
:start_line:3212
-------
                _atrFractionBox.LostFocus += OnAtrFractionLostFocus;
=======
                _atrFractionBox.LostFocus += OnAtrFractionLostFocus;
                _atrFractionBox.PreviewKeyDown += OnAtrBoxPreviewKeyDown;
>>>>>>> REPLACE
```

---

### Change B — Add OnAtrBoxPreviewKeyDown handler method in AtrWidget inner class

**Insert location**: After the closing brace of `OnAtrFractionLostFocus` (currently line 3306),
before the class-closing brace of `AtrWidget` (currently line 3307 `}`).

**BEFORE** (lines 3306–3309, exact):
```csharp
                if (_hasData) // guard (2)
                    RecalcDisplay();
            }
        }
    }
}
```

**AFTER** (insert new method before the `AtrWidget` inner class closing brace):
```csharp
                if (_hasData) // guard (2)
                    RecalcDisplay();
            }

            // B175: PreviewKeyDown on _atrFractionBox. Marks event handled when box has
            // keyboard focus, blocking NT8 chart-level search interceptor (tunnel phase).
            // CYC=2: base + IsKeyboardFocused check. JS-021: no lock. UI thread only. ASCII-only.
            private void OnAtrBoxPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
            {
                if (_atrFractionBox != null && _atrFractionBox.IsKeyboardFocused)
                    e.Handled = true;
            }
        }
    }
}
```

**apply_diff spec** — use `apply_diff` on `src/PropTraderTools/TradeCopierPanel.cs`:
```
<<<<<<< SEARCH
:start_line:3304
-------
                if (_hasData) // guard (2)
                    RecalcDisplay();
            }
        }
    }
}
=======
                if (_hasData) // guard (2)
                    RecalcDisplay();
            }

            // B175: PreviewKeyDown on _atrFractionBox. Marks event handled when box has
            // keyboard focus, blocking NT8 chart-level search interceptor (tunnel phase).
            // CYC=2: base + IsKeyboardFocused check. JS-021: no lock. UI thread only. ASCII-only.
            private void OnAtrBoxPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
            {
                if (_atrFractionBox != null && _atrFractionBox.IsKeyboardFocused)
                    e.Handled = true;
            }
        }
    }
}
>>>>>>> REPLACE
```

---

### CYC Verification — B175-2

| Method | CYC Before | CYC After | Delta | Compliant (<=8) |
|---|---|---|---|---|
| `AtrWidget.BuildAtrWidgetRows` | 1 | 1 | 0 | YES |
| `AtrWidget.OnAtrBoxPreviewKeyDown` (NEW) | — | 2 | +2 | YES |

CYC counting: `BuildAtrWidgetRows` — subscription line is straight-line, no branch. CYC unchanged=1. `OnAtrBoxPreviewKeyDown`: base(1)+if(&&=+1)=2. The `&&` compound condition counts as one branch (single `if`); both operands must be true. CYC=2.

---

### 7-Scan Checklist — B175-2

- [ ] **JS-001**: try/catch present in all modified methods — `OnAtrBoxPreviewKeyDown` has no throwable operations (null check + property read + bool set). No try/catch needed per JS-001 (handler does not return a fallible value). `BuildAtrWidgetRows` unchanged. PASS.
- [ ] **JS-021**: no lock() introduced — zero `lock` statements. PASS.
- [ ] **JS-023**: no volatile double — no new fields. PASS.
- [ ] **ASCII-only**: all comment text 7-bit ASCII printable. No Unicode. PASS.
- [ ] **CYC ≤8 per method**: see CYC table above; all compliant. PASS.
- [ ] **apply_diff used**: engineer MUST use `apply_diff` (two SEARCH/REPLACE blocks above). `write_file` on `.cs` is BANNED.
- [ ] **ptt-sync.ps1**: Run `powershell -File .\scripts\ptt-sync.ps1` after applying BOTH B175-1 and B175-2 (all `.cs` edits are complete). Confirm 0 link errors. This is the sync step for the entire pipeline. B175-3 (test file) does not require re-sync.

---

### Verification Greps — B175-2

After applying both changes:
```powershell
# Must return >= 2 (subscription + handler declaration)
(Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "OnAtrBoxPreviewKeyDown").Count

# Must return >= 1 (handler body)
(Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "IsKeyboardFocused").Count

# Must return 0 in changed region (no lock introduced)
(Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "lock\(").Count
```

Bash equivalents:
```bash
grep -c "OnAtrBoxPreviewKeyDown" src/PropTraderTools/TradeCopierPanel.cs  # expect >= 2
grep -c "IsKeyboardFocused" src/PropTraderTools/TradeCopierPanel.cs       # expect >= 1
grep -c "lock(" src/PropTraderTools/TradeCopierPanel.cs                   # expect 0
```

---

---

## Ticket B175-3 — New xUnit Tests (CopyEngineTests.cs)

**Scope lock**: `src/PropTraderTools/CopyEngineTests.cs` ONLY. No other file touched in this ticket.

**Spec requirements**: DW-B175-ATR-FORMULA (test coverage)

**File**: `src/PropTraderTools/CopyEngineTests.cs`

**Prerequisite**: B175-1 applied. `AtrCalc.Compute(double[], double[], double[], int)` must implement Wilder's smoothing for tests to pass.

**Insert location**: After the last line of the file (currently line 10313: `}`). Append the new test class after the closing `}` of the namespace.

**Current file end** (lines 10310–10313, exact):
```csharp
        }
    }

}
```

Note: line 10311 closes class `B174AtrCalcTests`, line 10312 is blank, line 10313 closes `namespace PropTraderTools`.

---

### Change — Append B175AtrCalcTests class

**BEFORE** (lines 10310–10313, exact):
```csharp
        }
    }

}
```

**AFTER** (replace closing namespace brace with new class + namespace brace):
```csharp
        }
    }

    // B175AtrCalcTests -- DW-B175-ATR-FORMULA formula validation.
    // Covers: Wilder's constant-TR identity, volatility spike comparison,
    //         fallback (short history), and B174 regression guard.
    // No NT8 runtime dependency. Uses AtrCalc.Compute(double[], double[], double[], int) only.
    // JS-021: no lock. ASCII-only. CYC: each test method = 1 (straight-line).
    public class B175AtrCalcTests
    {
        // T_B175_T1_01: constant TR=2.0 across 29 bars with period=14.
        // Wilder's seed = (2.0*14)/14 = 2.0; each step = (2.0*13+2.0)/14 = 2.0.
        // Constant TR => constant ATR at every smoothing step. Assert result == 2.0.
        [Fact]
        public void T_B175_T1_01_WildersAtr_ConstantTR_EqualsConstantTR()
        {
            const int len = 29;
            const int period = 14;
            double[] highs  = new double[len];
            double[] lows   = new double[len];
            double[] closes = new double[len];
            for (int i = 0; i < len; i++)
            {
                highs[i]  = i + 2;
                lows[i]   = i;
                closes[i] = i + 1;
            }
            double result = AtrCalc.Compute(highs, lows, closes, period);
            Assert.Equal(2.0, result, precision: 10);
        }

        // T_B175_T1_02: 29-bar spike array (TR=1 for bars 0..14, TR=3 for bars 15..28, period=14)
        // vs 15-bar flat fallback (TR=1 for bars 0..14, period=14).
        // Wilder's result (29-bar) > simple avg result (15-bar = 1.0).
        // Math: seed=1.0; 14 Wilder steps with TR=3.0 drive atr above 1.0. Assert holds.
        [Fact]
        public void T_B175_T1_02_WildersAtr_VolatilitySpike_ExceedsSimpleAvg()
        {
            const int period = 14;

            // 29-bar spike: indices 0..14 TR=1.0 (H=i+1, L=i, C=i+0.5)
            //               indices 15..28 TR=3.0 (H=i+3, L=i, C=i+1)
            double[] highs29  = new double[29];
            double[] lows29   = new double[29];
            double[] closes29 = new double[29];
            for (int i = 0; i <= 14; i++)
            {
                highs29[i]  = i + 1;
                lows29[i]   = i;
                closes29[i] = i + 0.5;
            }
            for (int i = 15; i <= 28; i++)
            {
                highs29[i]  = i + 3;
                lows29[i]   = i;
                closes29[i] = i + 1;
            }

            // 15-bar fallback: all TR=1.0 (H=i+1, L=i, C=i+0.5)
            double[] highs15  = new double[15];
            double[] lows15   = new double[15];
            double[] closes15 = new double[15];
            for (int i = 0; i <= 14; i++)
            {
                highs15[i]  = i + 1;
                lows15[i]   = i;
                closes15[i] = i + 0.5;
            }

            double wilders = AtrCalc.Compute(highs29, lows29, closes29, period);
            double simple  = AtrCalc.Compute(highs15, lows15, closes15, period);

            Assert.True(wilders > simple,
                $"Wilder's ({wilders}) must exceed simple avg ({simple}) when smoothing TR=3.0 over seed=1.0");
        }

        // T_B175_T1_03: 15-bar array (length=15, period=14): length=15 < 2*14+1=29.
        // Wilder's branch NOT entered. Returns simple average of 14 TRs all=2.0 = 2.0.
        // Verifies fallback path does not return 0.0 and returns correct simple avg.
        [Fact]
        public void T_B175_T1_03_ShortHistory_FallsBackToSimpleAvg()
        {
            const int len = 15;
            const int period = 14;
            double[] highs  = new double[len];
            double[] lows   = new double[len];
            double[] closes = new double[len];
            for (int i = 0; i < len; i++)
            {
                highs[i]  = i + 2;
                lows[i]   = i;
                closes[i] = i + 1;
            }
            double result = AtrCalc.Compute(highs, lows, closes, period);
            Assert.Equal(2.0, result, precision: 10);
        }

        // T_B175_T1_04: B174 regression guard. Exact H/L/C from B174 baseline.
        // period=3, length=4=period+1=4 < 2*3+1=7. Wilder's branch NOT entered.
        // TR[1]=max(12-9,|12-9|,|9-9|)=3; TR[2]=max(11-10,|11-10|,|10-10|)=1;
        // TR[3]=max(13-10,|13-11|,|10-11|)=3. Sum=7. ATR=7/3.
        // Assert result == 7.0/3.0 (within 1e-10). Label: B174 regression guard.
        [Fact]
        public void T_B175_T1_04_B174RegressionGuard_ShortHistory_SimpleAvg()
        {
            double[] highs  = new double[] { 10, 12, 11, 13 };
            double[] lows   = new double[] {  8,  9, 10, 10 };
            double[] closes = new double[] {  9, 10, 11, 12 };
            const int period = 3;
            double result = AtrCalc.Compute(highs, lows, closes, period);
            Assert.Equal(7.0 / 3.0, result, precision: 10);
        }
    }

}
```

**apply_diff spec** — use `apply_diff` on `src/PropTraderTools/CopyEngineTests.cs`:
```
<<<<<<< SEARCH
:start_line:10310
-------
        }
    }

}
=======
        }
    }

    // B175AtrCalcTests -- DW-B175-ATR-FORMULA formula validation.
    // Covers: Wilder's constant-TR identity, volatility spike comparison,
    //         fallback (short history), and B174 regression guard.
    // No NT8 runtime dependency. Uses AtrCalc.Compute(double[], double[], double[], int) only.
    // JS-021: no lock. ASCII-only. CYC: each test method = 1 (straight-line).
    public class B175AtrCalcTests
    {
        // T_B175_T1_01: constant TR=2.0 across 29 bars with period=14.
        // Wilder's seed = (2.0*14)/14 = 2.0; each step = (2.0*13+2.0)/14 = 2.0.
        // Constant TR => constant ATR at every smoothing step. Assert result == 2.0.
        [Fact]
        public void T_B175_T1_01_WildersAtr_ConstantTR_EqualsConstantTR()
        {
            const int len = 29;
            const int period = 14;
            double[] highs  = new double[len];
            double[] lows   = new double[len];
            double[] closes = new double[len];
            for (int i = 0; i < len; i++)
            {
                highs[i]  = i + 2;
                lows[i]   = i;
                closes[i] = i + 1;
            }
            double result = AtrCalc.Compute(highs, lows, closes, period);
            Assert.Equal(2.0, result, precision: 10);
        }

        // T_B175_T1_02: 29-bar spike array (TR=1 for bars 0..14, TR=3 for bars 15..28, period=14)
        // vs 15-bar flat fallback (TR=1 for bars 0..14, period=14).
        // Wilder's result (29-bar) > simple avg result (15-bar = 1.0).
        // Math: seed=1.0; 14 Wilder steps with TR=3.0 drive atr above 1.0. Assert holds.
        [Fact]
        public void T_B175_T1_02_WildersAtr_VolatilitySpike_ExceedsSimpleAvg()
        {
            const int period = 14;

            // 29-bar spike: indices 0..14 TR=1.0 (H=i+1, L=i, C=i+0.5)
            //               indices 15..28 TR=3.0 (H=i+3, L=i, C=i+1)
            double[] highs29  = new double[29];
            double[] lows29   = new double[29];
            double[] closes29 = new double[29];
            for (int i = 0; i <= 14; i++)
            {
                highs29[i]  = i + 1;
                lows29[i]   = i;
                closes29[i] = i + 0.5;
            }
            for (int i = 15; i <= 28; i++)
            {
                highs29[i]  = i + 3;
                lows29[i]   = i;
                closes29[i] = i + 1;
            }

            // 15-bar fallback: all TR=1.0 (H=i+1, L=i, C=i+0.5)
            double[] highs15  = new double[15];
            double[] lows15   = new double[15];
            double[] closes15 = new double[15];
            for (int i = 0; i <= 14; i++)
            {
                highs15[i]  = i + 1;
                lows15[i]   = i;
                closes15[i] = i + 0.5;
            }

            double wilders = AtrCalc.Compute(highs29, lows29, closes29, period);
            double simple  = AtrCalc.Compute(highs15, lows15, closes15, period);

            Assert.True(wilders > simple,
                $"Wilder's ({wilders}) must exceed simple avg ({simple}) when smoothing TR=3.0 over seed=1.0");
        }

        // T_B175_T1_03: 15-bar array (length=15, period=14): length=15 < 2*14+1=29.
        // Wilder's branch NOT entered. Returns simple average of 14 TRs all=2.0 = 2.0.
        // Verifies fallback path does not return 0.0 and returns correct simple avg.
        [Fact]
        public void T_B175_T1_03_ShortHistory_FallsBackToSimpleAvg()
        {
            const int len = 15;
            const int period = 14;
            double[] highs  = new double[len];
            double[] lows   = new double[len];
            double[] closes = new double[len];
            for (int i = 0; i < len; i++)
            {
                highs[i]  = i + 2;
                lows[i]   = i;
                closes[i] = i + 1;
            }
            double result = AtrCalc.Compute(highs, lows, closes, period);
            Assert.Equal(2.0, result, precision: 10);
        }

        // T_B175_T1_04: B174 regression guard. Exact H/L/C from B174 baseline.
        // period=3, length=4=period+1=4 < 2*3+1=7. Wilder's branch NOT entered.
        // TR[1]=max(12-9,|12-9|,|9-9|)=3; TR[2]=max(11-10,|11-10|,|10-10|)=1;
        // TR[3]=max(13-10,|13-11|,|10-11|)=3. Sum=7. ATR=7/3.
        // Assert result == 7.0/3.0 (within 1e-10). Label: B174 regression guard.
        [Fact]
        public void T_B175_T1_04_B174RegressionGuard_ShortHistory_SimpleAvg()
        {
            double[] highs  = new double[] { 10, 12, 11, 13 };
            double[] lows   = new double[] {  8,  9, 10, 10 };
            double[] closes = new double[] {  9, 10, 11, 12 };
            const int period = 3;
            double result = AtrCalc.Compute(highs, lows, closes, period);
            Assert.Equal(7.0 / 3.0, result, precision: 10);
        }
    }

}
>>>>>>> REPLACE
```

---

### CYC Verification — B175-3

| Method | CYC | Compliant (<=8) |
|---|---|---|
| `T_B175_T1_01_WildersAtr_ConstantTR_EqualsConstantTR` | 2 (base+for loop) | YES |
| `T_B175_T1_02_WildersAtr_VolatilitySpike_ExceedsSimpleAvg` | 3 (base+for+for) | YES |
| `T_B175_T1_03_ShortHistory_FallsBackToSimpleAvg` | 2 (base+for loop) | YES |
| `T_B175_T1_04_B174RegressionGuard_ShortHistory_SimpleAvg` | 1 (straight-line) | YES |

---

### 7-Scan Checklist — B175-3

- [ ] **JS-001**: try/catch present in all modified methods — test methods are assertions; no fallible return type. No try/catch needed (xUnit framework captures exceptions as test failures). PASS.
- [ ] **JS-021**: no lock() introduced — zero `lock` statements. PASS.
- [ ] **JS-023**: no volatile double — no fields declared in test class. PASS.
- [ ] **ASCII-only**: all identifiers and string literals are 7-bit ASCII. `"Wilder's"` uses straight apostrophe (0x27). PASS.
- [ ] **CYC ≤8 per method**: see CYC table above; all compliant. PASS.
- [ ] **apply_diff used**: engineer MUST use `apply_diff` (one SEARCH/REPLACE block above). `write_file` on `.cs` is BANNED.
- [ ] **ptt-sync.ps1**: NOT required for test-only file. B175-2 sync step covers all `.cs` production changes. Run `dotnet build` + `dotnet test` instead (see verification commands below).

---

### Verification Commands — B175-3

```powershell
# Build must show 0 errors
dotnet build src/PropTraderTools/PropTraderTools.Tests.csproj

# All 4 new tests must pass; no existing tests must fail
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --logger "console;verbosity=normal"

# New test names must be present
(Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "T_B175_T1_0[1-4]").Count
# Expect 4
```

---

---

## Pipeline Execution Order

```
B175-1 (AtrCalc.cs — 3 apply_diff blocks)
  -> B175-2 (TradeCopierPanel.cs — 2 apply_diff blocks)
     -> powershell -File .\scripts\ptt-sync.ps1   (RUN HERE — after all .cs edits)
        -> B175-3 (CopyEngineTests.cs — 1 apply_diff block)
           -> dotnet build
           -> dotnet test
```

`ptt-sync.ps1` runs ONCE after B175-2 (the last production `.cs` edit). B175-3 is test-only and does not require sync.

---

## Global Jane Street Compliance Summary

| Rule | B175-1 | B175-2 | B175-3 |
|---|---|---|---|
| JS-001 (try/catch / safe return) | PASS | PASS | PASS (N/A for tests) |
| JS-021 (no lock) | PASS | PASS | PASS |
| JS-023 (no volatile double) | PASS | PASS | PASS |
| ASCII-only | PASS | PASS | PASS |
| CYC ≤8 | PASS | PASS | PASS |
| No DateTime.Now | PASS | PASS | PASS |
| No hex colors | PASS | PASS | PASS |
| No FontFamily | PASS | PASS | PASS |
| No CreateOrder without PTT- prefix | N/A | N/A | N/A |
| No AtmStrategyCreate/ChangeStopTarget | N/A | N/A | N/A |

---

## Return Status

**TICKETS_COMPLETE**
