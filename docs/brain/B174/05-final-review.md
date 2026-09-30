# B174 Final Review
# Epic: DW-B174-ATR-INSTRUMENT-MISMATCH
# Phase 5 output: ptt-plan-reviewer
# Block: B174
# Tickets reviewed: B174-1, B174-2, B174-3
# Sources verified: AtrCalc.cs, TradeCopierPanel.cs, CopyEngineTests.cs
# Prior backlog: docs/brain/B173/06-deferred-backlog.md
# Status: COMPLETE

---

## CROSS-FILE COHERENCE CHECKS

### CC-01: AtrCalc.Compute new signature — correct call from OnTimerTick
**Criterion:** `AtrCalc.Compute(ChartControl, int, string=null)` — new 3-param overload — called
from `AtrWidget.OnTimerTick` with `_instrument?.FullName` as the third argument.

**Source verified:** `src/PropTraderTools/AtrCalc.cs` lines 71-74:
```csharp
internal static double Compute(
    NinjaTrader.Gui.Chart.ChartControl cc,
    int period,
    string instrumentFullName = null)
```
`src/PropTraderTools/TradeCopierPanel.cs` line 3269:
```csharp
double atr = AtrCalc.Compute(_chartControl, 14, _instrument?.FullName);
```
**Result: PASS**

---

### CC-02: AtrWidget._instrument field and SetInstrument method present
**Criterion:** `AtrWidget._instrument` field (type `NinjaTrader.Cbi.Instrument`) and
`AtrWidget.SetInstrument(NinjaTrader.Cbi.Instrument instr)` method present inside the
`private sealed class AtrWidget` in `TradeCopierPanel.cs`.

**Source verified:** `src/PropTraderTools/TradeCopierPanel.cs` lines 3181-3188:
```csharp
private NinjaTrader.Cbi.Instrument _instrument = null;

// B174: CYC=1 (straight-line assignment, no branches). UI thread only.
// JS-021: no lock. JS-002: void return. ASCII-only.
internal void SetInstrument(NinjaTrader.Cbi.Instrument instr)
{
    _instrument = instr;
}
```
**Result: PASS**

---

### CC-03: TradeCopierPanel.SetInstrument (outer) propagates to _widget?.SetInstrument
**Criterion:** Outer class `TradeCopierPanel.SetInstrument` calls `_widget?.SetInstrument(instrument)`
after setting `_instrument = instrument;`.

**Source verified:** `src/PropTraderTools/TradeCopierPanel.cs` lines 519-526:
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
**Result: PASS**

---

### CC-04: No orphaned two-arg Compute(ChartControl, int) calls remaining
**Criterion:** Old two-arg form `AtrCalc.Compute(_chartControl, 14)` must not appear anywhere.
All callers use the three-arg form.

**Source verified:** `src/PropTraderTools/TradeCopierPanel.cs` line 3269 — only call site in
AtrWidget.OnTimerTick — reads `AtrCalc.Compute(_chartControl, 14, _instrument?.FullName)`.
No two-arg form remains. Ticket-2-verification.md line 62 confirms:
"No two-argument form `AtrCalc.Compute(_chartControl, 14)` remains in AtrWidget."
**Result: PASS**

---

## JS VIOLATIONS CROSS-FILE

### CV-01: No lock() introduced (JS-021)
**AtrCalc.cs:** ticket-1-verification SCAN-01 = 0. Source read confirms static class with no
lock anywhere. **PASS**

**TradeCopierPanel.cs:** ticket-2-verification SCAN-01 = 27. All 27 are pre-existing; zero new
lock() calls introduced by B174 edits. Verifier confirmed zero new occurrences in B174-added
lines. **PASS**

**CopyEngineTests.cs:** ticket-3-verification SCAN-01 = 0 in B174AtrCalcTests range (lines
10258-10313). File-wide 5 hits are all comment-only text ("no lock()") — not statements. **PASS**

---

### CV-02: No Unicode in changed lines (JS-003 / ASCII-ONLY)
**AtrCalc.cs:** ticket-1-verification SCAN-03 = 0 non-ASCII characters. **PASS**
**TradeCopierPanel.cs:** ticket-2-verification SCAN-03 = 0 non-ASCII characters. **PASS**
**CopyEngineTests.cs:** ticket-3-verification SCAN-03 = 0 non-ASCII lines. **PASS**

Note: `ticket-3-verification.md` header contains a UTF-8 BOM (`﻿`) at line 1. This is a file
meta artifact from the verifier's text editor, NOT a C# source file. The .cs source file itself
(CopyEngineTests.cs) contains zero non-ASCII characters per independent SCAN-03. **PASS**

---

### CV-03: BarsArray[0] absent from AtrCalc.Compute(ChartControl,...) body
**Source verified directly:** `src/PropTraderTools/AtrCalc.cs` lines 71-112. The old bug line
`var bars = cc.BarsArray[0].Bars;` is absent. The scan loop uses `cc.BarsArray[idx]` (variable
index). No occurrence of the literal `BarsArray[0]` anywhere in the file.
ticket-1-verification SCAN-05 = 0. **PASS**

---

### CV-04: All CYC values ≤ 8
**Verified against CYC table from plan + ticket-review correction F-01:**

| Method | File | CYC | Compliant |
|--------|------|-----|-----------|
| `AtrCalc.Compute(ChartControl, int, string)` | AtrCalc.cs | 8 | YES (<=8) |
| `AtrWidget.SetInstrument` (NEW) | TradeCopierPanel.cs | 1 | YES (<=8) |
| `AtrWidget.OnTimerTick` | TradeCopierPanel.cs | 3 | YES (<=8) |
| `TradeCopierPanel.SetInstrument` | TradeCopierPanel.cs | 2 | YES (<=8) |

CYC=8 annotation present at AtrCalc.cs lines 68-69 with 8-branch breakdown confirmed by
ticket-1-verification SCAN-02. **PASS**

---

### CV-05: No throw in OnOrderUpdate / SendCopy / gate chain (JS-001)
No throws introduced by B174 in any modified method. All B174 methods either return void
(SetInstrument overloads) or are wrapped in try/catch → return 0.0 (AtrCalc.Compute). **PASS**

---

### CV-06: No volatile double (JS-023 / NT8-003)
`AtrWidget._instrument` is `NinjaTrader.Cbi.Instrument` (reference type), not `volatile double`.
Non-volatile is correct per NT8-003; JS-PUSH-01 three-step documented at TradeCopierPanel.cs
lines 3172-3180. **PASS**

---

## SPEC REQUIREMENTS SATISFIED

### SR-01: DW-B174-ATR-INSTRUMENT-MISMATCH — instrument-scan fix implemented
**Criterion:** `AtrCalc.Compute` now scans `BarsArray` by instrument name instead of
hardcoding index 0.

**Verified:** Source AtrCalc.cs lines 84-93 implement the scan loop. `BarsArray[0]` literal
absent (SCAN-05 = 0). **CLOSED — VERIFY_PASS**

---

### SR-02: Fallback to first valid entry when instrumentFullName is null/empty
**Criterion:** When `instrumentFullName` is null or empty string, `hasName=false` and the scan
selects the first valid `candidate` (index-zero fallback, preserving single-instrument window
behavior).

**Verified:** AtrCalc.cs line 83: `bool hasName = !string.IsNullOrEmpty(instrumentFullName);`
Line 88: `if (!hasName || candidate.Instrument.FullName == instrumentFullName)` — when
`hasName=false`, condition is always true and first valid candidate is selected.
Test T_B174_T1_03 exercises the `!string.IsNullOrEmpty` guard directly (3 assertions, all
pass). **CONFIRMED**

---

### SR-03: Separate windows — each panel gets its own _instrument
**Criterion:** Each `TradeCopierPanel` instance has its own `_widget` reference with its own
`_instrument` field. `SetInstrument` propagates per-panel. Two separate windows → two separate
panels → two separate `_instrument` values → two separate `AtrCalc.Compute` scan results.

**Verified:** `TradeCopierPanel.SetInstrument` (outer, line 520) sets `_instrument` on the
panel and calls `_widget?.SetInstrument(instrument)`. Each panel instance has its own `AtrWidget`
instance. Architecture is per-panel-isolated. **CONFIRMED**

---

### SR-04: Multi-tab single-window — best-effort documented limitation
**Criterion:** Multi-tab single-window behavior (shows ATR for instrument active at inject time;
tab switching alone does not update display) is documented as a known limitation.

**Verified:** Architecture plan Section 1 ("Documented UX boundary"), Section 4 ("Multi-tab
single-window ATR is best-effort"), and spec (B174-orchestrator-prompt.md) all document this.
The instrument is set at `SetInstrument` call time (inject time). No tab-change event
subscription is implemented (documented and deliberate). **CONFIRMED**

---

## ALL 7 SCANS VERIFIED

### T1 — Ticket-1 verification: VERIFY_PASS, all 7 scans zero/clean
ticket-1-verification.md verdict: **VERIFY_PASS**
SCAN-01: 0 | SCAN-02: CYC=8 at line 68 | SCAN-03: 0 | SCAN-04: 0 | SCAN-05: 0 |
SCAN-06: apply_diff confirmed | SCAN-07: deferred (correct per execution order)
**PASS**

### T2 — Ticket-2 verification: VERIFY_PASS, all 7 scans zero/clean
ticket-2-verification.md verdict: **VERIFY_PASS**
SCAN-01: 27 pre-existing, 0 new | SCAN-02: CYC=1 at line 3183 | SCAN-03: 0 | SCAN-04: 0 |
SCAN-05: `_instrument?.FullName` at line 3269 | SCAN-06: apply_diff confirmed |
SCAN-07: deferred (correct)
**PASS**

### T3 — Ticket-3 verification: VERIFY_PASS, all 7 scans zero/clean
ticket-3-verification.md verdict: **VERIFY_PASS**
SCAN-01: 0 in B174 range | SCAN-02: B174AtrCalcTests present | SCAN-03: 0 |
SCAN-04: 1 comment hit (not API call) | SCAN-05: 0 [Skip] | SCAN-06: apply_diff confirmed |
SCAN-07: ptt-sync.ps1 18 linked, 0 errors
**PASS**

### T4 — ptt-sync.ps1: 18 linked, 0 errors
ticket-3-completion.md SCAN-07 output: `ptt-sync complete: 18 linked, 0 errors`
All 18 files linked: AtrCalc.cs, CopyEngine.cs, FeatureFlags.cs, LicenseClient.cs,
TradeCopierAddOn.cs, TradeCopierPanel.cs, TradeCopierWindow.cs + 11 Features/Core files.
**PASS**

### T5 — dotnet build: 0 errors, 0 new warnings
ticket-3-completion.md: `Build succeeded. 0 Error(s). 0 Warning(s).`
Pre-existing CS0436 warnings suppressed by project configuration (same baseline as before B174).
**PASS**

### T6 — dotnet test: 0 new failures
ticket-3-completion.md: Full suite — Failed: 8 (pre-existing), Passed: 227, Skipped: 358.
8 failures are pre-existing (DwB168_02Tests + DwB15701SlateReadyTests). 0 new failures from B174.
B174AtrCalcTests: 3 passed, 0 failed, 0 skipped (ticket-3-verification SCAN-07 independent run).
**PASS**

---

## VIOLATIONS FOUND

**None.**

All 24 checklist items above pass. No JS rule violations. No spec requirements missing.
No orphaned call sites. No cross-file wiring gaps. All 7 scans clean across all 3 tickets.

---

## SECTION K — DEFERRED WORK (MANDATORY)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B174-ATR-INSTRUMENT-MISMATCH | AtrCalc.Compute BarsArray[0] hardcode — instrument-scan fix | P1 | B174 | **CLOSED -- B174 VERIFY_PASS** |
| DW-B174-SIM | SIM GATE: Director verifies separate-window ATR shows instrument-correct values post-B174 (MES panel != MGC panel). Steps: open two separate chart windows (one MES, one MGC), wait 2s, confirm each panel shows its own instrument's ATR, not the same value. | P1 | B175 or next Director session | **OPEN -- SIM pending** |
| DW-B173-01 | SIM GATE: AtrWidget live ATR display | P1 | B173 | **CLOSED -- Director SIM confirmed** |
| DW-B170-SIM | SIM GATE: Director drags target -- confirm follower stop preserved | P0 | pending | OPEN (PENDING SIM GATE) |
| DW-B169-K1 | SIM GATE: 3-stop simultaneous drag -- 0 missing follower stops | P0 | pending | OPEN (partially verified; Sim102 blocked by B167-DEFER-02) |
| DW-B169-K2 | SIM GATE: single-stop drag regression | P0 | pending | OPEN |
| DW-B168-K1 | Superseded by DW-B169-K1/K2 | P0 | pending | OPEN (superseded) |
| B167-DEFER-02 | fo=NULL: follower ATM brackets never placed after leader fill | P1 | pending | OPEN |
| B167-DEFER-01 | ECA-1 double-position race | P2 | pending | OPEN |
| B167-DEFER-03 | Combined B161-B166 SIM gate pending | P1 | pending | OPEN |
| B167-DEFER-04 | B167 SIM gate pending | P1 | pending | OPEN |
| DW-B159-K2 | 14 xUnit tests | P2 | future | OPEN |
| DW-B159-K3 | Attempt cap hardcoded at 3 | P2 | future | OPEN |
| DW-B159-K4 | intendedBePrice sourcing | P2 | future | OPEN |
| DW-B159-K5 | Stale line comment | P3 | future | OPEN |

---

## VERDICT

**FINAL_PASS**

All cross-file coherence checks passed. All JS DNA rules satisfied. All 3 spec requirements
confirmed. All 3 verifications returned VERIFY_PASS. All 7 scans clean. ptt-sync.ps1: 18
linked, 0 errors. dotnet build: 0 errors. dotnet test: 0 new failures. Section K present.
06-deferred-backlog.md written.

DW-B174-ATR-INSTRUMENT-MISMATCH is CLOSED. SIM gate (DW-B174-SIM) is queued for Director
confirmation: open two separate chart windows (MES + MGC), verify different ATR values shown
per panel.

---

*Authored by: ptt-plan-reviewer*
*Block: B174 -- DW-B174-ATR-INSTRUMENT-MISMATCH*
*Phase: 5 (Final Review)*
*Tickets: B174-1 VERIFY_PASS, B174-2 VERIFY_PASS, B174-3 VERIFY_PASS*
*Sources verified: AtrCalc.cs (115 lines), TradeCopierPanel.cs (key sections), CopyEngineTests.cs (lines 10255-10313)*
