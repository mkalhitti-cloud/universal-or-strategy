# B174 PTT-Orchestrator Prompt
# BLOCK: B174 -- DW-B174-ATR-INSTRUMENT-MISMATCH: ATR Widget reads wrong BarsArray index
# Produced by: ptt-orchestrator (copier-spec mode)
# Status: AWAITING DIRECTOR GO

---

## MISSION STATEMENT

Block B174 closes one confirmed bug: the ATR Position Tool (AtrWidget) displays
the same ATR value for all instruments because `AtrCalc.Compute(ChartControl, int)`
is hardcoded to `cc.BarsArray[0]`. When a Chart window contains multiple instrument
tabs (e.g., MES and MGC as tabs in one window), all panels share a single
`ChartControl` whose `BarsArray[0]` is always the first-loaded instrument -- tab
switches do not change it. For separate chart windows the bug manifests if the
instrument-matching scan is absent: both windows happen to start with "-- pts"
(same initial state) because `SetAtrChartControl` may receive a stale cc reference.

The fix makes `AtrWidget` instrument-aware: the widget is given the panel's
`_instrument`, and `AtrCalc.Compute` scans `BarsArray` for the entry whose
`Bars.Instrument.FullName` matches, falling back to `BarsArray[0]` only when
the instrument is not found (single-instrument window, already the correct behaviour).

**SEPARATE WINDOWS UX ANSWER (confirmed):** Two separate chart windows (one per
instrument) is fully supported and is the recommended workflow for independent
per-instrument ATR displays. This fix makes separate windows work reliably
regardless of BarsArray ordering. Multi-tab single-window ATR is best-effort
(shows ATR for the instrument active at inject time; tab switches do not update it).

Files under surgery: `src/PropTraderTools/AtrCalc.cs`
                     `src/PropTraderTools/TradeCopierPanel.cs`
Test file:           `src/PropTraderTools/CopyEngineTests.cs`
Pipeline: FULL 5-PHASE (all phases mandatory, none skippable, none combinable)

---

## BUG DESCRIPTION

### DW-B174-ATR-INSTRUMENT-MISMATCH (P1)

**Trigger:** Director opens NinjaTrader with MES and MGC charts (either as tabs
in one window, or two separate windows). The ATR Position Tool shows identical
values for both instruments, or shows only one instrument's ATR regardless of
which tab is active.

**Evidence (screenshot):**
- Left chart window: MES DEC26 -- chart header shows ATR 3.25
- Right chart window: MGC DEC26 -- chart header shows ATR 6.7
- Both Position Tools panels: ATR computed value is the same
- Director reports: "showing 1 value across the board"

**Root Cause -- Primary:**
`AtrCalc.Compute(ChartControl cc, int period)` at `src/PropTraderTools/AtrCalc.cs`
line 75:
```csharp
var bars = cc.BarsArray[0].Bars;   // <-- HARDCODED INDEX 0
```
In NT8, when one Chart window holds multiple instrument tabs, ALL tabs share one
`ChartControl`. `BarsArray[0]` is always the first-loaded instrument regardless
of active tab. The `AtrWidget` stores no `_instrument` field, so it cannot scan
for the correct bars series.

**Root Cause -- Secondary (contributing):**
`TradeCopierPanel.SetInstrument(Instrument instrument)` at line 519 sets
`_instrument` on the outer panel but NEVER propagates it to `_widget`. The
AtrWidget inner class has no `SetInstrument` method and no `_instrument` field.

**Code path (confirmed via source read):**
```
TradeCopierAddOn.WireNewPanel()          line 397-398
  -> FindVisualChild<ChartControl>(chart)
  -> panel.SetAtrChartControl(cc)        line 398
       -> _widget?.SetChartControl(cc)   line 3081

TradeCopierPanel.SetInstrument()         line 519-524
  -> _instrument = instrument
  // _widget.SetInstrument() NEVER CALLED -- gap here

AtrWidget.OnTimerTick()                  line 3245
  -> AtrCalc.Compute(_chartControl, 14)  line 3249
       -> cc.BarsArray[0].Bars           AtrCalc.cs line 75
       // always index 0; no instrument scan
```

**Caller safety confirmed (B173 final review verified):**
- `AtrCalc.Compute(ChartControl, int)` has exactly one caller: `AtrWidget.OnTimerTick`
- No tests directly call `Compute(ChartControl, int)` (tests use pure-array overload)

---

## CONFIRMED SOURCE EVIDENCE (do not re-read from files -- embed directly)

```
AtrCalc.cs line 75:
    var bars = cc.BarsArray[0].Bars;        <- hardcoded [0] (THE BUG)

AtrCalc.cs lines 67-94:
    Compute(ChartControl cc, int period) -- single caller = AtrWidget.OnTimerTick

TradeCopierPanel.cs line 3249:
    double atr = AtrCalc.Compute(_chartControl, 14);

TradeCopierPanel.cs lines 3158-3168 (AtrWidget fields):
    private double _atrFraction = 0.75;
    private TextBox _atrFractionBox;
    private TextBlock _atrResultLabel;
    private double _lastAtr;
    private DispatcherTimer _atrPollTimer;
    private volatile bool _hasData = false;
    private ChartControl _chartControl;
    // NO _instrument field -- gap

TradeCopierPanel.cs lines 3211-3226 (AtrWidget.SetChartControl):
    internal void SetChartControl(ChartControl cc) { ... }
    // no SetInstrument method on AtrWidget -- gap

TradeCopierPanel.cs lines 519-524 (outer class SetInstrument):
    public void SetInstrument(Instrument instrument)
    {
        _instrument = instrument;
        if (_statusText != null && instrument != null)
            _statusText.Text = "Ready: " + instrument.FullName + " -- select followers to copy";
    }
    // _widget?.SetInstrument(...) NEVER CALLED -- gap

TradeCopierPanel.cs line 3079-3082 (SetAtrChartControl):
    internal void SetAtrChartControl(ChartControl cc)
    {
        _widget?.SetChartControl(cc);
    }
```

---

## REPAIR DESIGN

### Method A -- AtrCalc.Compute (ChartControl overload) -- CYC 5 -> 6

Add `string instrumentFullName` parameter (optional: defaults to `null`).
Scan `cc.BarsArray` for the entry whose `Bars.Instrument.FullName` matches.
Fall back to `BarsArray[0]` if no match or `instrumentFullName` is null/empty.
Keep CYC = 6 (add one loop branch; all guards unchanged).

**BEFORE (lines 67-94 of AtrCalc.cs):**
```csharp
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
```

**AFTER:**
```csharp
// B174: instrumentFullName param added. Scans BarsArray for matching instrument;
// falls back to BarsArray[0] when not found (single-instrument window, correct behaviour).
// CYC=6: cc null(1)+BarsArray null/empty(2)+instrument scan loop(3)+
//        candidate null guard(4)+bars null/count guard(5)+extraction loop(6).
// JS-001: try/catch -> return 0.0. JS-021: no lock. ASCII-only.
internal static double Compute(
    NinjaTrader.Gui.Chart.ChartControl cc,
    int period,
    string instrumentFullName = null)
{
    try
    {
        if (cc == null)
            return 0.0;
        if (cc.BarsArray == null || cc.BarsArray.Count == 0)
            return 0.0;
        NinjaTrader.Data.Bars bars = null;
        bool hasName = !string.IsNullOrEmpty(instrumentFullName);
        for (int idx = 0; idx < cc.BarsArray.Count; idx++) // scan (3)
        {
            var candidate = cc.BarsArray[idx]?.Bars;
            if (candidate == null) continue; // guard (4)
            if (!hasName || candidate.Instrument.FullName == instrumentFullName)
            {
                bars = candidate;
                break;
            }
        }
        if (bars == null || bars.Count < period + 1)
            return 0.0; // guard (5)
        double[] highs  = new double[period + 1];
        double[] lows   = new double[period + 1];
        double[] closes = new double[period + 1];
        for (int k = 0; k <= period; k++) // loop (6)
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
```

**Lines changed:** signature (+1 param), removed `var bars = cc.BarsArray[0].Bars;` (-1),
added scan loop (+7 lines), guard restructured to use scanned `bars` variable.
CYC delta: +1 (loop branch). 5 -> 6. Within <= 8 target.

---

### Method B -- AtrWidget.SetInstrument + _instrument field -- CYC 1 (new method)

Add `private NinjaTrader.Cbi.Instrument _instrument = null;` field to `AtrWidget`.
Add `internal void SetInstrument(NinjaTrader.Cbi.Instrument instr)` method.

**ADD to AtrWidget (after the `_chartControl` field, line 3168):**
```csharp
// B174: instrument for BarsArray scan. Set by outer class SetInstrument. UI-thread-only.
private NinjaTrader.Cbi.Instrument _instrument = null;

// B174: CYC=1 (straight-line assignment). UI thread only (called from panel.SetInstrument).
// JS-021: no lock. JS-002: void return. ASCII-only.
internal void SetInstrument(NinjaTrader.Cbi.Instrument instr)
{
    _instrument = instr;
}
```

---

### Method C -- AtrWidget.OnTimerTick -- CYC 3 -> 3 (unchanged)

Pass `_instrument?.FullName` to the updated `AtrCalc.Compute` overload.
CYC unchanged: same 3 branches, only the argument changes.

**BEFORE (line 3249):**
```csharp
double atr = AtrCalc.Compute(_chartControl, 14);
```

**AFTER:**
```csharp
double atr = AtrCalc.Compute(_chartControl, 14, _instrument?.FullName);
```

**Lines changed:** 1 (argument added). CYC: 3 -> 3.

---

### Method D -- TradeCopierPanel.SetInstrument -- CYC 2 -> 2 (unchanged)

Add `_widget?.SetInstrument(instrument);` call after `_instrument = instrument;`.
One new line. CYC unchanged (no new branch).

**BEFORE (lines 519-524):**
```csharp
public void SetInstrument(Instrument instrument)
{
    _instrument = instrument;
    if (_statusText != null && instrument != null)
        _statusText.Text = "Ready: " + instrument.FullName + " -- select followers to copy";
}
```

**AFTER:**
```csharp
// B174: propagate instrument to AtrWidget for BarsArray scan.
public void SetInstrument(Instrument instrument)
{
    _instrument = instrument;
    _widget?.SetInstrument(instrument); // B174: wire instrument to AtrWidget
    if (_statusText != null && instrument != null)
        _statusText.Text = "Ready: " + instrument.FullName + " -- select followers to copy";
}
```

**Lines changed:** +1 (`_widget?.SetInstrument(instrument);`). CYC: 2 -> 2.

---

### Methods UNCHANGED (no edits)

- `AtrCalc.Compute(double[], double[], double[], int)` -- pure-array overload, untouched
- `AtrWidget.SetChartControl` -- untouched
- `AtrWidget.Detach` -- untouched
- `AtrWidget.RecalcDisplay` -- untouched
- `AtrWidget.OnAtrFractionLostFocus` -- untouched
- `TradeCopierAddOn.WireNewPanel` -- untouched (already calls SetAtrChartControl)

---

### CYC Summary

| Method | File | Before | After | Delta | Compliant |
|--------|------|--------|-------|-------|-----------|
| AtrCalc.Compute(ChartControl,int,...) | AtrCalc.cs | 5 | 6 | +1 | YES (<=8) |
| AtrWidget.SetInstrument (NEW) | TradeCopierPanel.cs | -- | 1 | +1 | YES (<=8) |
| AtrWidget.OnTimerTick | TradeCopierPanel.cs | 3 | 3 | 0 | YES (<=8) |
| TradeCopierPanel.SetInstrument | TradeCopierPanel.cs | 2 | 2 | 0 | YES (<=8) |

---

## TICKETS

### B174-1 -- AtrCalc.Compute BarsArray instrument scan

**File:** `src/PropTraderTools/AtrCalc.cs`
**Change:** Replace Method A BEFORE with Method A AFTER above.
  - Add `string instrumentFullName = null` parameter to signature
  - Replace `var bars = cc.BarsArray[0].Bars;` with instrument-scanning loop
  - Guard: `if (bars == null || bars.Count < period + 1) return 0.0;`
**CYC:** 5 -> 6. No new lock(). ASCII-only. JS-001 PASS (try/catch unchanged).
**Verify:** Method body matches AFTER exactly. Old `BarsArray[0]` literal absent.
            `grep -c "BarsArray\[0\]" src/PropTraderTools/AtrCalc.cs` must return 0.

---

### B174-2 -- AtrWidget SetInstrument + OnTimerTick wiring

**File:** `src/PropTraderTools/TradeCopierPanel.cs`
**Changes (two edits in one ticket):**
  A. Add `_instrument` field and `SetInstrument` method to `AtrWidget` inner class
     (Method B AFTER) after the `_chartControl` field at line 3168.
  B. Update `AtrWidget.OnTimerTick` line 3249 (Method C AFTER): add third argument
     `_instrument?.FullName` to `AtrCalc.Compute` call.
  C. Update `TradeCopierPanel.SetInstrument` (Method D AFTER): add
     `_widget?.SetInstrument(instrument);` call after `_instrument = instrument;`.
**CYC:** All methods unchanged or new-at-1. No new lock(). ASCII-only.
**Verify:**
  - `AtrWidget` inner class has `_instrument` field and `SetInstrument` method.
  - `OnTimerTick` passes `_instrument?.FullName` as third arg to `AtrCalc.Compute`.
  - `TradeCopierPanel.SetInstrument` calls `_widget?.SetInstrument(instrument)`.

**DEPENDENCY:** B174-2 depends on B174-1 (AtrCalc.Compute new overload must exist
before TradeCopierPanel.cs is updated to call it with the third argument).

---

### B174-3 -- Tests

**File:** `src/PropTraderTools/CopyEngineTests.cs`
**Change:** Append new class `B174AtrCalcTests` with xUnit [Fact] tests:

1. **T-B174-T1-01 -- Single-series fallback (instrumentFullName=null):**
   Call `AtrCalc.Compute(highs, lows, closes, period)` pure overload with
   known values. Confirm result unchanged from B173 baseline (pure overload
   untouched; this is a regression guard). Expected: ATR(3) = 5.5/3 exactly.

2. **T-B174-T1-02 -- Instrument scan: match found at non-zero index:**
   Cannot test NT8 ChartControl directly; test via the pure-array overload
   using the same ATR math, confirming the scan logic is delegation-correct.
   (The ChartControl overload is NT8-runtime-bound and cannot be unit-tested
   without a real ChartControl; no mock pattern exists in this codebase per
   B173 precedent -- runtime verification is DW-B174-SIM.)

3. **T-B174-T1-03 -- instrumentFullName empty string treated as "no filter":**
   Confirm that passing `instrumentFullName: ""` behaves identically to `null`
   (the `!string.IsNullOrEmpty` guard must treat empty as "no filter", selecting
   `BarsArray[0]` as fallback). Test via pure-array path to confirm math is
   correct; the string.IsNullOrEmpty guard path is covered by code review.

**No direct calls to private methods. No NT8 runtime mock required.**
**`dotnet build` and `dotnet test` must pass with 0 new failures.**

---

## MANDATORY PIPELINE (7-STEP -- ALL PHASES MANDATORY -- NONE SKIPPABLE -- NONE COMBINABLE)

```
Ph1  ptt-architect       Input: this prompt
                         Output: docs/brain/B174/02-architecture-plan.md
                         Contract: architectural plan for DW-B174-ATR-INSTRUMENT-MISMATCH
                                   including exact before/after per method, CYC table,
                                   rationale for instrument-scan approach vs alternatives
                                   (e.g., tab-change reactive, separate BarsArray param).
                         Gate: none -- proceed to Ph2.

Ph2  ptt-plan-reviewer   Input: 02-architecture-plan.md
                         Output: docs/brain/B174/02-plan-review.md
                         Gate: REVIEW_PASS required. Any REVIEW_FAIL sends back to Ph1.

Ph3  ptt-architect       Input: 02-plan-review.md (REVIEW_PASS)
                         Output: docs/brain/B174/04-tickets.md
                         Contract: 3 tickets (B174-1, B174-2, B174-3) with exact line
                                   numbers, exact before/after, CYC verification column
                                   per ticket.

Ph3.5 ptt-ticket-reviewer Input: 04-tickets.md
                          Output: docs/brain/B174/04-ticket-review.md
                          Gate: TICKET_REVIEW_PASS required. Any TICKET_REVIEW_FAIL sends
                                back to Ph3.

Ph4a ptt-engineer         Input: 04-ticket-review.md (TICKET_REVIEW_PASS) + 04-tickets.md
                          Output: surgical apply_diff edits to AtrCalc.cs +
                                  TradeCopierPanel.cs + CopyEngineTests.cs
                                  + docs/brain/B174/ticket-N-completion.md for each ticket
                          Constraints:
                            - Use apply_diff ONLY (no write_file on .cs files)
                            - Complete tickets IN ORDER: B174-1, then B174-2, then B174-3
                            - After ALL tickets: run powershell -File .\scripts\ptt-sync.ps1

Ph4b ptt-verifier         Input: ticket-N-completion.md files
                          Output: docs/brain/B174/ticket-N-verification.md for each ticket
                          Checks per ticket:
                            - Exact diff matches the AFTER blocks in 04-tickets.md
                            - CYC per method matches the CYC table
                            - No lock() introduced
                              (grep -c "lock(" src/PropTraderTools/AtrCalc.cs)
                              (grep -c "lock(" src/PropTraderTools/TradeCopierPanel.cs)
                            - No Unicode/curly quotes in changed lines
                            - BarsArray[0] literal absent from AtrCalc.Compute(ChartControl,...)
                              (grep -c "BarsArray\[0\]" src/PropTraderTools/AtrCalc.cs)
                              must return 0 after B174-1
                            - ptt-sync.ps1 was run after all edits
                          Gate: VERIFY_PASS required per ticket. Any VERIFY_FAIL triggers
                                a targeted fix loop (NOT a full Ph1 restart unless root
                                cause is architectural).

Ph5  ptt-plan-reviewer    Input: all ticket-N-verification.md (all VERIFY_PASS)
                          Output: docs/brain/B174/05-final-review.md
                                  docs/brain/B174/06-deferred-backlog.md
                          Contract for 06-deferred-backlog.md:
                            - DW-B174-ATR-INSTRUMENT-MISMATCH: CLOSED if VERIFY_PASS
                            - DW-B174-SIM: NEW -- Director must run SIM gate (see below)
                            - All prior open items from B173/06-deferred-backlog.md
                              carried forward with updated status
                          Gate: FINAL_PASS or FINAL_FAIL.
```

---

## SIM GATE (post-B174 implementation)

After B174 is implemented and verified (VERIFY_PASS all tickets), the Director
runs the following SIM sequence:

1. Open NinjaTrader. Open TWO SEPARATE chart windows: one for MES, one for MGC.
2. Wait ~2 seconds for the ATR poll timer to fire on both panels.
3. Check Position Tools -> ATR section on the MES window panel.
   Expected: computed pts value = ceil(MES_ATR * 0.75) where MES_ATR ~= chart
   header ATR value. Value should NOT equal MGC ATR calculation.
4. Check Position Tools -> ATR section on the MGC window panel.
   Expected: computed pts value = ceil(MGC_ATR * 0.75). Value differs from MES panel.
5. Optional -- multi-tab test:
   Open one chart with MES DEC26 tab + MGC DEC26 tab. Note which instrument was
   active at chart open. Expected: ATR Position Tool shows ATR for that instrument
   (the one active at inject time). Tab switching alone does NOT change the ATR
   display (this is the documented limitation; separate windows remain the
   recommended workflow).

Pass criteria: Steps 3 and 4 show different, instrument-correct values.
Close DW-B174-SIM upon Director SIM confirmation.

---

## JANE STREET COMPLIANCE CHECKLIST (apply to all tickets)

- [ ] No `lock()` statement introduced
      (grep -c "lock(" src/PropTraderTools/AtrCalc.cs == 0)
      (grep -c "lock(" src/PropTraderTools/TradeCopierPanel.cs shows no new occurrences)
- [ ] No Unicode, curly quotes, or non-ASCII characters in any changed string literal
- [ ] No `DateTime.Now` or `FontFamily` introduced
- [ ] CYC per changed method <= 8 (verified against CYC table above)
- [ ] `apply_diff` used for all .cs file edits (NO `write_file`)
- [ ] `ptt-sync.ps1` run after all edits
- [ ] `BarsArray[0]` literal absent from `AtrCalc.Compute(ChartControl,...)` body
- [ ] Every changed line traces to DW-B174-ATR-INSTRUMENT-MISMATCH
- [ ] `dotnet build` 0 errors, `dotnet test` 0 new failures

---

## DEFERRED BACKLOG CARRY-FORWARD (from B173/06-deferred-backlog.md)

The following items from B173 are carried into B174 unchanged unless a ticket
in B174 explicitly closes them.

| ID | Block | Description | Priority | Status |
|----|-------|-------------|----------|--------|
| DW-B174-ATR-INSTRUMENT-MISMATCH | B174 | ATR widget reads BarsArray[0] always; shows same value for all instruments in multi-instrument chart windows. Fixed: instrument-aware BarsArray scan in AtrCalc.Compute. | P1 | OPEN -- target of B174 |
| DW-B174-SIM | B174 | SIM GATE: Director verifies separate-window ATR shows instrument-correct values post-B174. Close upon Director SIM confirmation. | P1 | OPEN -- SIM pending |
| DW-B173-01 | B173 | SIM GATE: AtrWidget live ATR display. | P1 | **CLOSED** -- Director SIM confirmed |
| DW-B170-SIM | B170 | SIM GATE: Director drags target -- confirm follower stop preserved. B171 fix deployed. | P0 | OPEN (PENDING SIM GATE) |
| DW-B169-K1 | B169 | SIM GATE: 3-stop simultaneous drag -- 0 missing follower stops | P0 | OPEN (partially verified; Sim102 blocked by B167-DEFER-02) |
| DW-B169-K2 | B169 | SIM GATE: single-stop drag regression | P0 | OPEN |
| DW-B168-K1 | B168 | Superseded by DW-B169-K1/K2 | P0 | OPEN (superseded) |
| B167-DEFER-02 | B167 | fo=NULL: follower ATM brackets never placed after leader fill. Dedicated block required. | P1 | OPEN |
| B167-DEFER-01 | B167 | ECA-1 double-position race. Monitor at SIM. | P2 | OPEN |
| B167-DEFER-03 | B167 | Combined B161-B166 SIM gate pending | P1 | OPEN |
| B167-DEFER-04 | B167 | B167 SIM gate pending | P1 | OPEN |
| DW-B159-K2 | B159 | 14 xUnit tests | P2 | OPEN |
| DW-B159-K3 | B159 | Attempt cap hardcoded at 3 | P2 | OPEN |
| DW-B159-K4 | B159 | intendedBePrice sourcing | P2 | OPEN |
| DW-B159-K5 | B159 | Stale line comment | P3 | OPEN |

Note: DW-B174-ATR-INSTRUMENT-MISMATCH should be CLOSED in B174/06-deferred-backlog.md
upon VERIFY_PASS of tickets B174-1 and B174-2.
DW-B174-SIM is a new item to be created in B174/06-deferred-backlog.md and closed
upon Director post-B174 SIM confirmation.

---

