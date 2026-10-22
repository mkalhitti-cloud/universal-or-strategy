# B211 Ticket-2 Completion Report
## UI -- TradeCopierPanel Button Rows (Ask / Ask-ALL / Bid / Bid-ALL)

**Phase**: 4a
**Engineer**: ptt-engineer
**Date**: 2026-07-08
**Prerequisite**: Ticket-1 VERIFY_PASS (PttMoveTargetOne.cs, PttMoveTargetAll.cs, PttContracts.cs, B211Tests.cs already present)

---

## Scope

Ticket 2 only. One file modified: `src/PropTraderTools/TradeCopierPanel.cs`.
No Feature files, PttContracts.cs, or B211Tests.cs were touched.

---

## Files Modified

| File | Lines Before | Lines After | Delta |
|------|-------------|-------------|-------|
| `src/PropTraderTools/TradeCopierPanel.cs` | 2971 | 3124 | +153 |

---

## Changes Implemented

### New Fields

**volatile int buffer fields** (after `_beBuffer`, line 241+):
```csharp
private volatile int _askBuffer = 0;  // B211: Ask/Ask-ALL buffer ticks. JS-023: volatile -- no lock.
private volatile int _bidBuffer = 0;  // B211: Bid/Bid-ALL buffer ticks. JS-023: volatile -- no lock.
```

**Button reference fields** (after `_copyToggleBtn2`):
```csharp
private Button _askBtn    = null;  // B211: Ask button ref for label updates
private Button _askAllBtn = null;  // B211: Ask-ALL button ref for label updates
private Button _bidBtn    = null;  // B211: Bid button ref for label updates
private Button _bidAllBtn = null;  // B211: Bid-ALL button ref for label updates
```

**UniformGrid panel fields** (after `_quickRowPanel`):
```csharp
private UniformGrid _askRowPanel = null; // B211: 2-col grid: [Ask cluster] | [Ask-ALL cluster]
private UniformGrid _bidRowPanel = null; // B211: 2-col grid: [Bid cluster] | [Bid-ALL cluster]
```

### BuildBufferedButtonsRow Modifications

Panel initializations added before `var specs = ...`:
```csharp
_askRowPanel = new UniformGrid { Columns = 2, Margin = new Thickness(0, 2, 0, 2) };
_bidRowPanel = new UniformGrid { Columns = 2, Margin = new Thickness(0, 2, 0, 2) };
```

4 new entries appended to the `specs` array (Ask, Ask-ALL, Bid, Bid-ALL), each referencing BrushTeal, the shared buffer, and the corresponding row panel.

### BuildUI Modifications

After `root.Children.Add(_beRowPanel)`:
```csharp
root.Children.Add(_askRowPanel);  // B211: Ask row directly below BE row
root.Children.Add(_bidRowPanel);  // B211: Bid row directly below Ask row
```

### 14 New Event Handlers

All added after `OnBeDown`. All synchronous void. No async. No Dispatcher.InvokeAsync.

---

## SCAN-01 -- CYC Results (Manual McCabe Count)

| Method | Branches | CYC | Limit | Status |
|--------|----------|-----|-------|--------|
| OnAskUp | 0 (Math.Max/Min are calls, no if/ternary) | 1 | 8 | PASS |
| OnAskDown | 0 | 1 | 8 | PASS |
| OnAskClick | _instrument null(1) + ??(1) + _leaderAccount null(1) | 4 | 8 | PASS |
| OnAskAllClick | _instrument null(1) | 2 | 8 | PASS |
| OnAskAllUp | 0 (expression body => delegate) | 1 | 8 | PASS |
| OnAskAllDown | 0 | 1 | 8 | PASS |
| UpdateAskLabels | if _askBtn!=null(1) + if _askAllBtn!=null(1) | 3 | 8 | PASS (RISK-C: stated 1, tool may report 3 -- within limit) |
| OnBidUp | 0 | 1 | 8 | PASS |
| OnBidDown | 0 | 1 | 8 | PASS |
| OnBidClick | _instrument null(1) + ??(1) + _leaderAccount null(1) | 4 | 8 | PASS |
| OnBidAllClick | _instrument null(1) | 2 | 8 | PASS |
| OnBidAllUp | 0 | 1 | 8 | PASS |
| OnBidAllDown | 0 | 1 | 8 | PASS |
| UpdateBidLabels | if _bidBtn!=null(1) + if _bidAllBtn!=null(1) | 3 | 8 | PASS (RISK-C: stated 1, tool may report 3 -- within limit) |

All 14 methods CYC <= 8. SCAN-01: **PASS**

Note on RISK-C: ticket-reviewer flagged that UpdateAsk/BidLabels have 2 null guards each. The plan stated CYC=1 (single line), but McCabe with `if` guards gives CYC=3. Both are well within the global limit of 8. Reported as directed.

---

## SCAN-02 -- lock() Scan

Command: `Select-String -Path "src\PropTraderTools\TradeCopierPanel.cs" -Pattern "lock\("`

Output: (no output -- zero matches)

SCAN-02: **PASS** (0 new lock() calls)

---

## SCAN-03 -- async void Scan

Command: `Select-String -Path "src\PropTraderTools\TradeCopierPanel.cs" -Pattern "^\s+async void "`

Output: (no output -- zero matches for actual declarations)

SCAN-03: **PASS** (0 new async void handlers -- all 14 are synchronous void)

---

## SCAN-04 -- null return Scan

Command: `Select-String -Path "src\PropTraderTools\TradeCopierPanel.cs" -Pattern "return null"`

Output: Pre-existing matches at lines 492, 568, 573, 577, 1983, 1993 (all pre-existing, none in B211 additions).
Zero new null returns introduced by Ticket-2 (all new handlers are void).

SCAN-04: **PASS** (0 new null returns)

---

## SCAN-05 -- throw Scan

Command: `Select-String -Path "src\PropTraderTools\TradeCopierPanel.cs" -Pattern "throw "`

Output: (no output -- zero matches)

SCAN-05: **PASS** (0 throw statements anywhere in file)

---

## SCAN-06 -- ASCII Scan

String literals in new code:
- `"Ask"` -- ASCII only
- `"Ask-ALL"` -- ASCII only
- `"Bid"` -- ASCII only
- `"Bid-ALL"` -- ASCII only
- Comments: ASCII-only, no Unicode, no curly quotes, no em-dashes

SCAN-06: **PASS** (all new string literals ASCII-only)

---

## SCAN-07 -- ptt-sync

Command: `powershell -File .\scripts\ptt-sync.ps1`

Output:
```
LINKED  CopyEngine.cs
LINKED  FeatureFlags.cs
LINKED  LicenseClient.cs
LINKED  PttTickMath.cs
LINKED  TradeCopierAddOn.cs
LINKED  TradeCopierPanel.cs
LINKED  TradeCopierWindow.cs
LINKED  Core\PttContracts.cs
LINKED  Features\PttBeSwapAll.cs
LINKED  Features\PttBeSwapOne.cs
LINKED  Features\PttBreakEvenSwap.cs
LINKED  Features\PttCancel.cs
LINKED  Features\PttCopier.cs
LINKED  Features\PttFlatten.cs
LINKED  Features\PttFollowerStrategy.cs
LINKED  Features\PttGlobalBreakEven.cs
LINKED  Features\PttGlobalQuickExit.cs
LINKED  Features\PttMoveTargetAll.cs
LINKED  Features\PttMoveTargetOne.cs
LINKED  Features\PttQuickExit.cs
LINKED  Features\PttTrim.cs

ptt-sync complete: 21 linked, 0 errors
```

SCAN-07: **PASS** (21 linked, 0 errors)

---

## Ph4b Deletion Audit

Command: `git diff HEAD~1 HEAD -- src/PropTraderTools/TradeCopierPanel.cs | Select-String "^-    (private|internal|public)"`

Output: (no output -- zero deleted symbols)

Deletion audit: **PASS** (0 symbols deleted, no DW-card required)

---

## Guardian Symbol Check

Command: `powershell -File .\scripts\verify_critical_symbols.ps1`

Output:
```
GUARDIAN FAIL -- missing symbols and/or gate violations:
  - TradeCopierPanel: _instr2tBtn.Background = BrushTeal (Quick2t visual fix)
```
Exit code: 1

**This is the DW-B209-02 pre-existing gap** (confirmed by running the script on the baseline commit before B211 changes -- same failure exists on commit `ba8ade4f`). The `_instr2tBtn.Background = BrushTeal` check was added to the Guardian script in a prior block but the code fix was deferred to the B209 deferred backlog. This is NOT a B211 regression. B211 architecture plan explicitly lists DW-B209-02 as "OPEN -- not B211 scope".

Guardian check: **PRE-EXISTING GAP (DW-B209-02) -- NOT a B211 regression**

---

## Threading Model Confirmation

All 14 handlers are synchronous void. No Dispatcher.InvokeAsync added (all handlers run on UI thread from WPF click events). `_askBuffer`/`_bidBuffer` are `volatile int` -- no inter-thread contention. Per ticket threading model section.

---

## JS Compliance Summary

| Rule | Status |
|------|--------|
| JS-021 (no lock) | PASS -- volatile int, no lock() |
| JS-001 (no throw) | PASS -- no throw anywhere |
| JS-002 (no null return) | PASS -- all new methods are void |
| JS-023 (volatile int for buffer) | PASS -- _askBuffer/_bidBuffer declared volatile |
| JS-033 (no async void) | PASS -- all 14 handlers are synchronous void |

---

## SIM Verification Note

For SIM verification after B211 merge:
- Ask button: press with position open, verify ask-side cancel+resubmit (PTT-MA-Ask-NNNNN) in NT8 SIM.
- Bid button: press with position open, verify bid-side cancel+resubmit (PTT-MA-Bid-NNNNN) in NT8 SIM.
- Ask-ALL and Bid-ALL buttons: verify that all accounts with position have Working Limit exit moved.

---

## BUILD_PASS
