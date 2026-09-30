# B175 PTT-Orchestrator Prompt
# BLOCK: B175 -- DW-B175-ATR-FORMULA + DW-B175-ATR-TEXTBOX-FOCUS
# Produced by: ptt-orchestrator (copier-spec mode)
# Status: AWAITING DIRECTOR GO

---

## MISSION STATEMENT

Block B175 closes two confirmed bugs in the ATR Position Tool widget:

**DW-B175-ATR-FORMULA (P1):** `AtrCalc.Compute` uses a simple 14-period average of
True Range. NT8's chart header uses Wilder's ATR (smoothed EMA, period 14). The two
formulas diverge -- simple average underestimates volatility in trending/volatile
conditions. Both PTT panels show "2 pts" when the chart header shows ATR 3.0 and
4.8 respectively. Director confirmed with fraction=5.0: both panels show "13 pts",
meaning `ceil(x * 5.0) = 13` => simple ATR ~2.5 for both instruments independently
(B174 scan IS working). The fix replaces the simple average with Wilder's smoothing
using a 2*period warmup window so PTT matches the NT8 chart header value.

**DW-B175-ATR-TEXTBOX-FOCUS (P1):** Typing in the ATR% TextBox in the Position Tools
panel triggers NT8's chart instrument search dialog instead of entering text. Root
cause: NT8 registers its own `chart.PreviewKeyDown` handler (tunnel routing, fires at
the chart root level before WPF routes keystrokes down to child elements) that
intercepts alphanumeric characters and opens the instrument search. The ATR% TextBox
never receives the keystrokes. Fix: subscribe `_atrFractionBox.PreviewKeyDown` and
call `e.Handled = true` to mark keystrokes as handled while the TextBox has keyboard
focus, preventing them from tunnelling to the chart's search interceptor.

**DW-B174-SIM: CLOSED** -- Director confirmed separate-window ATR shows
instrument-correct values. Both panels computed ~2.58 ATR independently (different
BarsArray reads). B174 scan fix verified working.

Files under surgery: `src/PropTraderTools/AtrCalc.cs`
                     `src/PropTraderTools/TradeCopierPanel.cs`
Test file:           `src/PropTraderTools/CopyEngineTests.cs`
Pipeline: FULL 5-PHASE (all phases mandatory, none skippable, none combinable)

---

## BUG DESCRIPTIONS

### DW-B175-ATR-FORMULA (P1)

**Trigger:** PTT Position Tools ATR widget shows a value lower than NT8 chart header
ATR (e.g., header shows 3.0 and 4.8; widget shows 2 pts for both at fraction=0.75).

**Evidence:**
- Director set fraction to 5.0 on both windows: both showed 13 pts
- `ceil(x * 5.0) = 13` => `x` in range (2.4, 2.6] for both instruments
- NT8 chart header: MES ATR = 2.75, MGC ATR = 4.5 (at time of test)
- Simple average of last 14 TRs = ~2.5 for both (different instruments, same value by coincidence)

**Root cause:**
`AtrCalc.Compute(double[], double[], double[], int)` at line 36-46 uses:
```csharp
double sum = 0.0;
for (int i = 1; i <= period; i++) { sum += tr; }
return sum / period;   // simple average
```
NT8 chart header uses Wilder's ATR:
```
ATR[seed] = sum(TR[0..period-1]) / period     (first period simple avg)
ATR[i]    = (ATR[i-1] * (period-1) + TR[i]) / period  (smoothed thereafter)
```
Wilder's ATR carries forward historical volatility -- it stays elevated during
trending periods where simple average drops quickly.

**Fix -- Wilder's ATR in pure-array overload:**
Replace simple sum/period with Wilder's smoothing on a warmup window.
The NT8 overload reads 2*period+1 bars (warmup=period seed bars + period smoothing bars).
The pure-array overload accepts arrays of length >= 2*period+1 for full Wilder's,
or falls back gracefully to simple avg when length < 2*period+1 (insufficient history).

**NT8 overload change:** read 2*period+1 bars instead of period+1 bars.
Guard: if `bars.Count < 2*period+1` -- fall back to simple average (read period+1 bars
as before). This preserves correct behaviour on charts with very few bars loaded.

**Caller:** `AtrWidget.OnTimerTick` passes `period=14` -> needs `bars.Count >= 29`
for full Wilder's. With 5-minute charts and typical NT8 bar loading this is always
satisfied during market hours.

---

### DW-B175-ATR-TEXTBOX-FOCUS (P1)

**Trigger:** Director clicks the ATR% TextBox in Position Tools and types a number
(e.g., "5"). NT8's instrument search dialog opens instead of the number appearing
in the TextBox.

**Evidence (screenshot):** NT8 chart search dialog opens with "5" as the search
term showing instrument list (5YR, AEX25, etc.) while the ATR% TextBox shows "5.00"
(the Director had to delete the existing value first to get "5" to land).

**Root cause:**
NT8 registers a `chart.PreviewKeyDown` handler at the chart window root level that
intercepts alphanumeric keys (any printable character) and opens the instrument search
dialog. WPF `PreviewKeyDown` tunnels from the visual tree root DOWN to the target
element -- so NT8's chart handler fires BEFORE the keystroke reaches the TextBox.
The TextBox's own `KeyDown`/`TextInput` events never fire for those keys.

Our `panel.OnChartKeyDown` (registered by B11) only fires on `Ctrl+Shift` combos --
it does NOT interfere and is NOT the cause. The NT8 built-in handler is the cause.

**Fix:**
In `AtrWidget.BuildAtrWidgetRows`, after creating `_atrFractionBox`, subscribe
`_atrFractionBox.PreviewKeyDown` with a handler that calls `e.Handled = true`
when the TextBox has keyboard focus (`_atrFractionBox.IsKeyboardFocused == true`).
This marks the keystroke as handled before NT8's chart-level tunnel handler sees it,
so NT8's search interceptor never fires.

Also subscribe `_atrFractionBox.GotKeyboardFocus` and `LostKeyboardFocus` to set a
`_atrBoxHasFocus` bool flag -- the PreviewKeyDown handler checks this flag to avoid
eating keystrokes when the box does not have focus.

**Alternative considered and rejected:** `e.Handled = true` in `PreviewKeyDown`
unconditionally would break chart keyboard shortcuts. Checking `IsKeyboardFocused`
directly in the handler (without a flag) is correct and simpler -- no flag needed.

**SIMPLIFIED FIX (preferred):** Subscribe `_atrFractionBox.PreviewKeyDown`; in the
handler, if `_atrFractionBox.IsKeyboardFocused` is true, set `e.Handled = true`.
No flag field required. CYC=2 (base + IsKeyboardFocused check).

---

## CONFIRMED SOURCE EVIDENCE (do not re-read from files -- embed directly)

```
AtrCalc.cs lines 36-46 (simple average -- THE FORMULA BUG):
    double sum = 0.0;
    for (int i = 1; i <= period; i++)
    {
        double pc = closes[i - 1];
        double tr = Math.Max(highs[i] - lows[i],
                    Math.Max(Math.Abs(highs[i] - pc), Math.Abs(lows[i] - pc)));
        sum += tr;
    }
    return sum / period;   // SIMPLE AVERAGE -- does not match NT8 Wilder's ATR

AtrCalc.cs lines 94-106 (NT8 overload -- reads period+1 bars, must increase to 2*period+1):
    if (bars == null || bars.Count < period + 1)
        return 0.0;
    double[] highs  = new double[period + 1];
    ...
    for (int k = 0; k <= period; k++)
    {
        int barsAgo = period - k;
        ...
    }
    return Compute(highs, lows, closes, period);   // delegates to simple avg

TradeCopierPanel.cs lines 3205-3212 (ATR% TextBox -- missing PreviewKeyDown handler):
    _atrFractionBox = new TextBox
    {
        Text = _atrFraction.ToString("F2"),
        Width = 55,
        VerticalContentAlignment = VerticalAlignment.Center,
    };
    _atrFractionBox.SetResourceReference(Control.StyleProperty, "NTTextBoxStyle");
    _atrFractionBox.LostFocus += OnAtrFractionLostFocus;
    // PreviewKeyDown handler ABSENT -- keystrokes reach NT8 chart search interceptor

TradeCopierPanel.cs line 3269 (OnTimerTick call -- unchanged, still correct):
    double atr = AtrCalc.Compute(_chartControl, 14, _instrument?.FullName);
```

---

## REPAIR DESIGN

### Method A -- AtrCalc.Compute pure-array overload -- Wilder's ATR -- CYC 6 -> 8

Replace the simple `sum / period` with Wilder's smoothing.
Array length requirement: `>= 2*period + 1` for full Wilder's; fallback to simple
average when `length < 2*period + 1` (preserves correctness on short histories).

**Algorithm:**
- Seed: simple average of TR[1..period] (first `period` TR values)
- Smooth: Wilder's formula for TR[period+1..2*period]
  `atr = (atr * (period - 1) + tr) / period`
- If array too short for warmup: fall back to existing simple average (unchanged path)

**New signature:** `internal static double Compute(double[] highs, double[] lows, double[] closes, int period)`
Signature UNCHANGED. Only the body changes.

**BEFORE (lines 26-52):**
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

**AFTER:**
```csharp
// B175: Wilder's ATR. Seed = simple avg of first period TRs; then smooth.
// Requires length >= 2*period+1 for full Wilder's; falls back to simple avg
// when length < 2*period+1 (short history: period+1 bars minimum unchanged).
// CYC=8: null(1)+period<1(2)+length<period+1(3)+seed loop(4)+
//        Wilder's length check(5)+smooth loop(6)+simple-fallback loop(7)+catch(8).
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
        // else: simple average already computed -- return seed as-is (fallback, (7))
        return atr;
    }
    catch (Exception)
    {
        return 0.0;
    }
}
```

**CYC:** 6 -> 8. Delta +2. Within <= 8 target.

---

### Method B -- AtrCalc.Compute NT8 overload -- bar count -- CYC 8 -> 9

Increase bar read window from `period+1` to `2*period+1` so Wilder's smoothing
gets its full warmup. Add fallback: if `bars.Count < 2*period+1` but
`bars.Count >= period+1` -- read only `period+1` bars (simple avg path, unchanged).

**BEFORE (lines 94-106 of AtrCalc.cs):**
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

**AFTER:**
```csharp
        // B175: read up to 2*period+1 bars for Wilder's warmup; fall back to
        // period+1 when chart has fewer bars loaded (Wilder's fallback to simple avg).
        if (bars == null || bars.Count < period + 1)
            return 0.0;
        int readLen = bars.Count >= 2 * period + 1 ? 2 * period + 1 : period + 1; // (9)
        double[] highs  = new double[readLen];
        double[] lows   = new double[readLen];
        double[] closes = new double[readLen];
        for (int k = 0; k < readLen; k++)
        {
            int barsAgo = readLen - 1 - k;
            highs[k]  = bars.GetHigh(barsAgo);
            lows[k]   = bars.GetLow(barsAgo);
            closes[k] = bars.GetClose(barsAgo);
        }
        return Compute(highs, lows, closes, period);
```

**CYC:** 8 -> 9. Delta +1. **EXCEEDS <= 8 target.**

**EXTRACTION REQUIRED:** Move the bar-read loop into a private static helper
`ReadBarsToArrays(Bars bars, int readLen, out double[] h, out double[] l, out double[] c)`
CYC=1 (straight-line extraction loop). This reduces the NT8 overload CYC back to 8.

**REVISED AFTER (NT8 overload, with extraction):**
```csharp
        if (bars == null || bars.Count < period + 1)
            return 0.0;
        int readLen = bars.Count >= 2 * period + 1 ? 2 * period + 1 : period + 1; // (9 -> ternary)
        ReadBarsToArrays(bars, readLen, out double[] highs, out double[] lows, out double[] closes);
        return Compute(highs, lows, closes, period);
```

**New private helper (CYC=1):**
```csharp
// B175: Extract bar OHLC into arrays. readLen bars, oldest-first (index 0 = oldest).
// CYC=1 (extraction loop only). JS-001: called inside outer try/catch. ASCII-only.
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

**NT8 overload CYC after extraction:** 8 (ternary branch is branch 9, reduces to 8
because the extraction loop is extracted). Actually ternary adds 1 to NT8 overload
CYC. Let me recount:
- cc null (1), BarsArray null/empty (2), idx loop (3), candidate null (4),
  hasName check (5), bars null/count (6), ternary readLen (7+8 count = 1 branch -> 7),
  catch (8).
CYC = 8 exactly. The extraction moves the loop CYC to ReadBarsToArrays (CYC=1).
Compliant: <= 8. ReadBarsToArrays: CYC=1. Compliant.

---

### Method C -- AtrWidget._atrFractionBox PreviewKeyDown -- CYC 1 (new handler)

**ADD in `BuildAtrWidgetRows` after `_atrFractionBox.LostFocus += OnAtrFractionLostFocus;`:**

```csharp
// B175: Swallow keystrokes while the ATR% box has focus so NT8's chart-level
// PreviewKeyDown instrument-search interceptor cannot steal them. CYC=2.
_atrFractionBox.PreviewKeyDown += OnAtrBoxPreviewKeyDown;
```

**ADD new private handler to AtrWidget inner class:**
```csharp
// B175: PreviewKeyDown on _atrFractionBox. Marks event handled when box has
// keyboard focus, blocking NT8 chart-level search interceptor (tunnel phase).
// CYC=2: base + IsKeyboardFocused check. JS-021: no lock. UI thread only. ASCII-only.
private void OnAtrBoxPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
{
    if (_atrFractionBox != null && _atrFractionBox.IsKeyboardFocused)
        e.Handled = true;
}
```

**Note on e.Handled = true:** This also blocks our own `panel.OnChartKeyDown`
Ctrl+Shift shortcuts while the TextBox is focused -- this is CORRECT behaviour:
the user is typing, not issuing a trade shortcut.

**CYC:** 1 (new method) -> 2. Within <= 8 target.

---

### Methods UNCHANGED

- `AtrWidget.SetInstrument` (B174) -- untouched
- `AtrWidget.SetChartControl` -- untouched
- `AtrWidget.OnTimerTick` -- untouched (still passes `_instrument?.FullName`)
- `TradeCopierPanel.SetInstrument` -- untouched
- `TradeCopierAddOn.WireNewPanel` -- untouched

---

### CYC Summary

| Method | File | Before | After | Delta | Compliant |
|--------|------|--------|-------|-------|-----------|
| AtrCalc.Compute(double[],...) | AtrCalc.cs | 6 | 8 | +2 | YES (<=8) |
| AtrCalc.Compute(ChartControl,...) | AtrCalc.cs | 8 | 8 | 0 | YES (<=8) |
| AtrCalc.ReadBarsToArrays (NEW) | AtrCalc.cs | -- | 1 | +1 | YES (<=8) |
| AtrWidget.OnAtrBoxPreviewKeyDown (NEW) | TradeCopierPanel.cs | -- | 2 | +2 | YES (<=8) |
| AtrWidget.BuildAtrWidgetRows | TradeCopierPanel.cs | 1 | 1 | 0 | YES (<=8) |

---

## TICKETS

### B175-1 -- AtrCalc pure-array Wilder's ATR + NT8 overload + ReadBarsToArrays helper

**File:** `src/PropTraderTools/AtrCalc.cs`
**Changes (in one ticket -- all in AtrCalc.cs):**
  A. Replace `Compute(double[]...)` body with Wilder's smoothing (Method A AFTER).
     Signature unchanged.
  B. Replace NT8 overload bar-read block (lines 94-106) with ternary readLen +
     `ReadBarsToArrays` call (Method B AFTER).
  C. Add `ReadBarsToArrays` private static helper after the NT8 overload closing brace.
**CYC:** All methods at or below 8. No new lock(). ASCII-only.
**Verify:**
  - Old `sum / period` pattern absent from `Compute(double[]...)` body
  - `ReadBarsToArrays` helper present in file
  - `BarsArray[0]` still absent (B174 regression guard)
  - `grep -c "sum / period" src/PropTraderTools/AtrCalc.cs` == 0
  - `grep -c "ReadBarsToArrays" src/PropTraderTools/AtrCalc.cs` >= 2 (def + call)

---

### B175-2 -- AtrWidget.OnAtrBoxPreviewKeyDown TextBox focus fix

**File:** `src/PropTraderTools/TradeCopierPanel.cs`
**Changes (two edits in one ticket):**
  A. In `BuildAtrWidgetRows` after `_atrFractionBox.LostFocus += OnAtrFractionLostFocus;`:
     add `_atrFractionBox.PreviewKeyDown += OnAtrBoxPreviewKeyDown;`
  B. Add `OnAtrBoxPreviewKeyDown` private method to AtrWidget inner class (Method C AFTER).
**CYC:** New handler CYC=2. BuildAtrWidgetRows CYC unchanged at 1. No new lock(). ASCII-only.
**Verify:**
  - `OnAtrBoxPreviewKeyDown` method present in AtrWidget inner class
  - `_atrFractionBox.PreviewKeyDown += OnAtrBoxPreviewKeyDown` subscription present
  - `e.Handled = true` inside `IsKeyboardFocused` guard

**DEPENDENCY:** B175-2 is independent of B175-1. Either ticket can be implemented first.
Both tickets are in different files, so they can be implemented in sequence without
cross-dependency. Order: B175-1 first (higher P1 formula fix), B175-2 second.

---

### B175-3 -- Tests

**File:** `src/PropTraderTools/CopyEngineTests.cs`
**Change:** Append new class `B175AtrCalcTests` with xUnit [Fact] tests:

1. **T-B175-T1-01 -- Wilder's converges above simple avg (known values):**
   Build arrays of length 2*14+1 = 29 with constant TR=2.0 (all H-L=2, prev-close
   irrelevant since |H-pc| <= H-L). Simple avg = 2.0. Wilder's also = 2.0 for
   constant TR (stable state). Assert result == 2.0.
   This confirms Wilder's is implemented (if still simple avg, result is also 2.0,
   so this is a stability test -- see T-B175-T1-02 for divergence test).

2. **T-B175-T1-02 -- Wilder's exceeds simple avg for stepped volatility (regression):**
   Build arrays of 29 bars: first 14 bars have TR=1.0 (seed period, low vol),
   next 14 bars have TR=10.0 (high vol spike). 
   Simple avg of last 14 TRs = (1+1+...+10+10+...) / 14 (mixed). 
   Wilder's seed = avg(TR[1..14]) = 1.0, then smooth 14 more TRs of 10.0:
   After smoothing, Wilder's ATR >> simple avg of last 14. Assert Wilder's > simple.
   (Compute both: call Compute with 29-bar array for Wilder's, call Compute with
   15-bar array slicing last 15 elements for simple avg fallback, and compare.)

3. **T-B175-T1-03 -- Short history fallback (period+1 bars) returns simple avg:**
   Build array of exactly period+1 = 15 bars. Length < 2*period+1 = 29.
   Confirm result equals simple avg of 14 TRs (fallback path).
   Use same constant-TR=2.0 values. Assert result == 2.0.

4. **T-B175-T1-04 -- B174 regression guard (pure overload period+1 still works):**
   Same as T-B174-T1-01: H=[10,12,11,13], L=[8,9,10,10], C=[9,10,11,12], period=3.
   Now with Wilder's, array length=4 < 2*3+1=7 -> fallback to simple avg.
   Assert result == 7.0/3.0 (unchanged from B174 baseline -- regression guard).

**No direct calls to private methods. No NT8 runtime mock required.**
**`dotnet build` and `dotnet test` must pass with 0 new failures.**

---

## MANDATORY PIPELINE (7-STEP -- ALL PHASES MANDATORY -- NONE SKIPPABLE -- NONE COMBINABLE)

```
Ph1  ptt-architect       Input: this prompt
                         Output: docs/brain/B175/02-architecture-plan.md
                         Contract: architectural plan for DW-B175-ATR-FORMULA +
                                   DW-B175-ATR-TEXTBOX-FOCUS including exact
                                   before/after per method, CYC table, rationale
                                   for Wilder's implementation approach (warmup
                                   window size, fallback strategy, extraction pattern).
                         Gate: none -- proceed to Ph2.

Ph2  ptt-plan-reviewer   Input: 02-architecture-plan.md
                         Output: docs/brain/B175/02-plan-review.md
                         Gate: REVIEW_PASS required. Any REVIEW_FAIL sends back to Ph1.

Ph3  ptt-architect       Input: 02-plan-review.md (REVIEW_PASS)
                         Output: docs/brain/B175/04-tickets.md
                         Contract: 3 tickets (B175-1, B175-2, B175-3) with exact line
                                   numbers, exact before/after, CYC verification column
                                   per ticket.

Ph3.5 ptt-ticket-reviewer Input: 04-tickets.md
                          Output: docs/brain/B175/04-ticket-review.md
                          Gate: TICKET_REVIEW_PASS required. Any TICKET_REVIEW_FAIL sends
                                back to Ph3.

Ph4a ptt-engineer         Input: 04-ticket-review.md (TICKET_REVIEW_PASS) + 04-tickets.md
                          Output: surgical apply_diff edits to AtrCalc.cs +
                                  TradeCopierPanel.cs + CopyEngineTests.cs
                                  + docs/brain/B175/ticket-N-completion.md for each ticket
                          Constraints:
                            - Use apply_diff ONLY (no write_file on .cs files)
                            - Complete tickets IN ORDER: B175-1, then B175-2, then B175-3
                            - After ALL tickets: run powershell -File .\scripts\ptt-sync.ps1

Ph4b ptt-verifier         Input: ticket-N-completion.md files
                          Output: docs/brain/B175/ticket-N-verification.md for each ticket
                          Checks per ticket:
                            - Exact diff matches AFTER blocks in 04-tickets.md
                            - CYC per method matches CYC table
                            - No lock() introduced
                            - No Unicode/curly quotes in changed lines
                            - `grep -c "sum / period" src/PropTraderTools/AtrCalc.cs` == 0
                            - `grep -c "ReadBarsToArrays" src/PropTraderTools/AtrCalc.cs` >= 2
                            - `grep -c "OnAtrBoxPreviewKeyDown" src/PropTraderTools/TradeCopierPanel.cs` >= 2
                            - ptt-sync.ps1 was run after all edits
                          Gate: VERIFY_PASS required per ticket. Any VERIFY_FAIL triggers
                                a targeted fix loop (NOT a full Ph1 restart unless root
                                cause is architectural).

Ph5  ptt-plan-reviewer    Input: all ticket-N-verification.md (all VERIFY_PASS)
                          Output: docs/brain/B175/05-final-review.md
                                  docs/brain/B175/06-deferred-backlog.md
                          Contract for 06-deferred-backlog.md:
                            - DW-B175-ATR-FORMULA: CLOSED if VERIFY_PASS B175-1
                            - DW-B175-ATR-TEXTBOX-FOCUS: CLOSED if VERIFY_PASS B175-2
                            - DW-B174-SIM: CLOSED (Director confirmed post-B174)
                            - DW-B175-SIM: NEW -- Director must run SIM gate (see below)
                            - All prior open items from B174/06-deferred-backlog.md
                              carried forward with updated status
                          Gate: FINAL_PASS or FINAL_FAIL.
```

---

## SIM GATE (post-B175 implementation)

After B175 is implemented and verified (VERIFY_PASS all tickets), the Director
runs the following SIM sequence:

1. Open NinjaTrader. Open MES and MGC charts (separate windows).
2. Wait ~2 seconds for ATR poll timer to fire.
3. Check Position Tools ATR section on MES panel.
   Expected: computed pts value = ceil(MES_ATR * 0.75) where MES_ATR closely
   matches the chart header ATR value (within +/- 0.5 pts of Wilder's).
4. Check Position Tools ATR section on MGC panel.
   Expected: computed pts value = ceil(MGC_ATR * 0.75). Differs from MES panel.
5. Click the ATR% TextBox on either panel. Type "1.50".
   Expected: number appears in the TextBox. NT8 instrument search dialog does NOT open.
   Tab or click elsewhere: widget updates to new pts value.
6. Confirm values are instrument-different (not both the same integer).

Pass criteria: Steps 3-5 all pass.
Close DW-B175-SIM upon Director SIM confirmation.

---

## JANE STREET COMPLIANCE CHECKLIST (apply to all tickets)

- [ ] No `lock()` statement introduced
- [ ] No Unicode, curly quotes, or non-ASCII characters in any changed string literal
- [ ] No `DateTime.Now` or `FontFamily` introduced
- [ ] CYC per changed method <= 8 (verified against CYC table above)
- [ ] `apply_diff` used for all .cs file edits (NO `write_file`)
- [ ] `ptt-sync.ps1` run after all edits
- [ ] `sum / period` absent from `AtrCalc.Compute(double[]...)` body after B175-1
- [ ] `ReadBarsToArrays` helper present and called from NT8 overload
- [ ] `OnAtrBoxPreviewKeyDown` present in AtrWidget after B175-2
- [ ] Every changed line traces to DW-B175-ATR-FORMULA or DW-B175-ATR-TEXTBOX-FOCUS
- [ ] `dotnet build` 0 errors, `dotnet test` 0 new failures

---

## DEFERRED BACKLOG CARRY-FORWARD (from B174/06-deferred-backlog.md)

| ID | Block | Description | Priority | Status |
|----|-------|-------------|----------|--------|
| DW-B175-ATR-FORMULA | B175 | AtrCalc simple avg != NT8 Wilder's ATR. Fix: Wilder's with 2x warmup. | P1 | OPEN -- target of B175 |
| DW-B175-ATR-TEXTBOX-FOCUS | B175 | ATR% TextBox keystrokes stolen by NT8 chart search interceptor. Fix: PreviewKeyDown e.Handled guard. | P1 | OPEN -- target of B175 |
| DW-B175-SIM | B175 | SIM GATE: Director verifies PTT ATR matches chart header ATR; TextBox typing works without search dialog. | P1 | OPEN -- SIM pending post-B175 |
| DW-B174-SIM | B174 | SIM GATE: Director verifies separate-window ATR shows instrument-correct values. | P1 | **CLOSED -- Director confirmed B174 scan working** |
| DW-B173-01 | B173 | SIM GATE: AtrWidget live ATR display. | P1 | **CLOSED -- Director SIM confirmed** |
| DW-B170-SIM | B170 | SIM GATE: Director drags target -- confirm follower stop preserved. | P0 | OPEN (PENDING SIM GATE) |
| DW-B169-K1 | B169 | SIM GATE: 3-stop simultaneous drag -- 0 missing follower stops | P0 | OPEN (partially verified; Sim102 blocked by B167-DEFER-02) |
| DW-B169-K2 | B169 | SIM GATE: single-stop drag regression | P0 | OPEN |
| DW-B168-K1 | B168 | Superseded by DW-B169-K1/K2 | P0 | OPEN (superseded) |
| B167-DEFER-02 | B167 | fo=NULL: follower ATM brackets never placed after leader fill. | P1 | OPEN |
| B167-DEFER-01 | B167 | ECA-1 double-position race. | P2 | OPEN |
| B167-DEFER-03 | B167 | Combined B161-B166 SIM gate pending | P1 | OPEN |
| B167-DEFER-04 | B167 | B167 SIM gate pending | P1 | OPEN |
| DW-B159-K2 | B159 | 14 xUnit tests | P2 | OPEN |
| DW-B159-K3 | B159 | Attempt cap hardcoded at 3 | P2 | OPEN |
| DW-B159-K4 | B159 | intendedBePrice sourcing | P2 | OPEN |
| DW-B159-K5 | B159 | Stale line comment | P3 | OPEN |
