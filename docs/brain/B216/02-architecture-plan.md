# B216 Architecture Plan
## Ask/Bid Pattern-B Conversion + Cancel/Cancel All Buttons + DW-B216-01

**Block**: B216  
**Phase**: 1 (Architecture)  
**Date**: 2026  
**Author**: ptt-architect  
**Status**: PLAN_COMPLETE (cycle 2 -- V-01 fixed)

---

## LANE-SPLIT GATE RESULT

### Gate Questions

**Q1. Same method or within 50 lines?**  
- Lane A (OnAskClick ~1488, OnAskAllClick ~1500) and Lane B (OnBidClick ~1545, OnBidAllClick ~1557) are in the same file. The gap between Lane A's last modified line (~1530) and Lane B's first modified line (~1545) is approximately 15 lines. WITHIN 50 LINES: **YES** for Lane A vs Lane B.  
- Lane C (field ~279, BuildUI ~906, BuildCancelRow new, handlers ~1670, PttCancelAll.cs new file) is in scattered regions not within 50 lines of Lanes A or B. WITHIN 50 LINES for C vs A/B: **NO**.

**Q2. Fix B design depends on Fix A final design?**  
Lane B (OnBidAllClick Pattern B) calls `HasNonFlatPosition(acc, _instrument)` -- a new private static helper added by Lane A. Lane B **depends** on Lane A having added this helper. **YES -- Lane B depends on Lane A.**  
Lane C is independent of both A and B. NO dependency.

**Q3. Each fix has standalone value if the other is blocked?**  
Lane A alone: delivers Ask null-leader log (AC-A1) + Ask-All Pattern B (AC-A2/A3). YES.  
Lane B alone: requires `HasNonFlatPosition` from Lane A. **NOT fully standalone** without Lane A.  
Lane C alone: fully independent. YES.

**Q4. Each fix has an independent SIM verification path?**  
Lane A: SIM Ask null-leader -> verify log. SIM Ask-All -> verify fires on position accounts. YES.  
Lane B: shares `HasNonFlatPosition` helper from A; not independently executable without A present. PARTIAL.  
Lane C: SIM Cancel and Cancel All buttons independently. YES.

### Gate Result

**LANE-SPLIT GATE RESULT: SINGLE-PIPELINE**

Rationale: Lane A must precede Lane B (B depends on `HasNonFlatPosition` helper added by A). Lanes A+B are within 50 lines of each other. Q2=YES (B->A dependency). Execute in sequence: Lane A -> Lane B -> Lane C. All three lanes are documented in this single plan as three sequential tickets.

---

## Rules Catalog Gate (Mandatory P0 Check)

| Rule | Description | Check Result |
|------|-------------|-------------|
| JS-021 | No `lock()` | PASS -- zero new `lock()` in TradeCopierPanel.cs or PttCancelAll.cs |
| JS-001 | No `throw new` in hot paths | PASS -- no throw in any new method |
| JS-002 | No `return null` | PASS -- all new methods return void or bool |
| JS-033 | No `async void` | PASS -- all handlers are synchronous `private void` |
| JS-036/037 | No heap alloc in hot path | PASS -- `new List<Order>()` in PttCancelAll.Execute is not a hot path |
| ASCII-only | No Unicode/emoji in string literals | PASS -- all log strings verified ASCII |
| DateTime.UtcNow | No DateTime.Now | PASS -- no DateTime usage |
| No FontFamily | No FontFamily references | PASS -- using BrushTeal field + `Brushes.White` |
| No hex colors | No `#RRGGBB` in code | PASS -- using named field `BrushTeal` throughout |

**GATE RESULT: PASS -- zero P0 violations. Work may proceed.**

---

## Deferred Item Status

### Items CLOSED by B216

| ID | Item | Closure |
|----|------|---------|
| DW-B216-01 Part A | Null-leader diagnostic log in OnAskClick / OnBidClick | CLOSED by Lane A (OnAskClick) and Lane B (OnBidClick) |
| DW-B216-01 Part B | OnAskAllClick + OnBidAllClick Pattern B conversion | CLOSED by Lane A (Ask-All) and Lane B (Bid-All) |

### Items Still Open (carry-forward from B215)

| ID | Item | Priority | Status |
|----|------|----------|--------|
| DW-B215-LA-04 | Fix DwB168_02Tests + B196Tests (gitignored) | P1 | OPEN -- manual |
| DW-B215-LA-07 | Fix B195Tests T1_SubmitDrained... (gitignored) | P1 | OPEN -- manual |
| DW-B214-LB-01/02/03 | gitignored test file updates | P1 | OPEN -- manual |
| DW-B209-01 | Guardian false positive (verify_critical_symbols.ps1) | P2 | OPEN |
| DW-B209-02 | BrushTeal gap (_instr2tBtn.Background) | P2 | OPEN |
| DW-B212-02 | PttMoveTargetAll.cs disposition decision | P2 | OPEN |
| DW-B211-01 | Direction-aware validation (future feature) | P2 | OPEN |
| DW-B211-02 | Target nudge buttons (depends DW-B211-01) | P2 | OPEN |

---

## Files Touched

| File | Lane | Change Type |
|------|------|-------------|
| `src/PropTraderTools/TradeCopierPanel.cs` | A, B, C | MODIFY (methods + field + BuildUI) |
| `src/PropTraderTools/Features/PttCancelAll.cs` | C | CREATE NEW |

**Files explicitly not touched:**
- `src/PropTraderTools/Features/PttCancel.cs` -- DO NOT CHANGE
- `src/PropTraderTools/Features/PttMoveTargetOne.cs` -- DO NOT CHANGE
- `src/PropTraderTools/CopyEngine.cs` -- NOT touched by B216
- `src/PropTraderTools/TradeCopierAddOn.cs` -- NOT touched by B216

---

## CYC Budget (Engineer Contract)

| Method | File | CYC | Status |
|--------|------|-----|--------|
| `OnAskClick` | TradeCopierPanel.cs | 4 | MODIFIED |
| `OnAskAllClick` (Pattern B) | TradeCopierPanel.cs | 4 | MODIFIED |
| `HasNonFlatPosition` (new) | TradeCopierPanel.cs | 5 | NEW |
| `OnBidClick` | TradeCopierPanel.cs | 4 | MODIFIED |
| `OnBidAllClick` (Pattern B) | TradeCopierPanel.cs | 4 | MODIFIED |
| `BuildCancelRow` (new) | TradeCopierPanel.cs | 1 | NEW |
| `OnCancelClick` (new) | TradeCopierPanel.cs | 1 | NEW |
| `OnCancelAllClick` (new) | TradeCopierPanel.cs | 2 | NEW |
| `PttCancelAll.Execute` | PttCancelAll.cs | 5 | NEW |
| `PttCancelAll.IsWorkingOrderForInstr` | PttCancelAll.cs | 4 | NEW (private) |

All methods <= 8. JS-066 ceiling: PASS.

---

## Lane A: Ask Pattern-B Conversion + Null-Leader Log

### Current State

[`OnAskClick`](src/PropTraderTools/TradeCopierPanel.cs:1488) (Pattern A):
```csharp
private void OnAskClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null) return;
    _leaderAccount = _leaderAccount ?? TryResolveLeaderAccount();
    if (_leaderAccount == null) return;   // silent return on null leader
    PttMoveTargetOne.Execute(_leaderAccount, _instrument, useAsk: true, _askBuffer);
}
```

[`OnAskAllClick`](src/PropTraderTools/TradeCopierPanel.cs:1500) (Pattern A):
```csharp
private void OnAskAllClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null) return;
    _leaderAccount = _leaderAccount ?? TryResolveLeaderAccount();
    if (_leaderAccount == null) return;   // requires leader
    PttMoveTargetOne.Execute(_leaderAccount, _instrument, useAsk: true, _askBuffer);
    foreach (var item in _followerItems)
        if (item.IsSelected && item.Account != null)
            PttMoveTargetOne.Execute(item.Account, _instrument, useAsk: true, _askBuffer);
}
```

Problem: OnAskClick silently does nothing when leader is null (DW-B216-01 Part A). OnAskAllClick requires leader account and follower list -- fails when copier is off or leader unresolved (DW-B216-01 Part B).

### Target State

**OnAskClick** (modified -- DW-B216-01 Part A):
```csharp
// B216: OnAskClick -- cancel Working target + resubmit Limit at Ask + buffer. CYC=4.
// Branches: (1) _instrument null, (2) ?? null-coalescing, (3) _leaderAccount null w/ diagnostic log.
// JS-033: synchronous void. ASCII-only. DW-B216-01 Part A: null-leader log.
private void OnAskClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null) return;
    _leaderAccount = _leaderAccount ?? TryResolveLeaderAccount();
    if (_leaderAccount == null)
    {
        NinjaTrader.Code.Output.Process(
            "[PTT-MA] Ask: leader null -- check account combo on chart trader",
            NinjaTrader.NinjaScript.PrintTo.OutputTab1);
        return;
    }
    PttMoveTargetOne.Execute(_leaderAccount, _instrument, useAsk: true, _askBuffer);
}
```

**OnAskAllClick** (rewritten -- Pattern B -- DW-B216-01 Part B):
```csharp
// B216: OnAskAllClick -- Pattern B: iterate Account.All, act on all accounts with non-flat position.
// Removes _leaderAccount + _followerItems dependency.
// Branches: (1) _instrument null, (2) foreach Account.All, (3) HasNonFlatPosition,
//           (4) acted==0 log path. CYC=4. JS-021: no lock. ASCII-only.
private void OnAskAllClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null) return;
    int acted = 0;
    foreach (Account acc in Account.All)
    {
        if (HasNonFlatPosition(acc, _instrument))
        {
            PttMoveTargetOne.Execute(acc, _instrument, useAsk: true, _askBuffer);
            acted++;
        }
    }
    if (acted > 0)
        NinjaTrader.Code.Output.Process(
            "[PTT-MA] Ask-All: instrument=" + _instrument.FullName + " accounts=" + acted,
            NinjaTrader.NinjaScript.PrintTo.OutputTab1);
    else
        NinjaTrader.Code.Output.Process(
            "[PTT-MA] Ask-All: no positions found for " + _instrument.FullName,
            NinjaTrader.NinjaScript.PrintTo.OutputTab1);
}
```

**HasNonFlatPosition** (new private static):
```csharp
// B216: HasNonFlatPosition -- returns true if acc holds a non-flat position for instr.
// Used by OnAskAllClick and OnBidAllClick (Pattern B) to filter accounts.
// CYC=5: (1) acc null, (2) instr null, (3) foreach, (4) pos null, (5) instrument+qty check.
// JS-002: returns bool. JS-021: no lock. ASCII-only.
private static bool HasNonFlatPosition(Account acc, Instrument instr)
{
    if (acc == null) return false;
    if (instr == null) return false;
    foreach (Position pos in acc.Positions)
    {
        if (pos == null) continue;
        if (pos.Instrument != null
            && pos.Instrument.FullName == instr.FullName
            && pos.Quantity != 0)
            return true;
    }
    return false;
}
```

### Data Flow (Lane A)

```
User clicks Ask button
  -> OnAskClick(UI thread)
  -> if _instrument null: return (silent)
  -> _leaderAccount lazy-resolve via TryResolveLeaderAccount()
  -> if _leaderAccount still null: log "[PTT-MA] Ask: leader null..." -> return (diagnostic)
  -> PttMoveTargetOne.Execute(leader, instr, useAsk:true, buffer)
     -> collect Working targets -> cancel+resubmit at Ask+buffer (B211 pattern)

User clicks Ask-All button
  -> OnAskAllClick(UI thread)
  -> if _instrument null: return
  -> for each acc in Account.All
       HasNonFlatPosition(acc, instr)?
         YES: PttMoveTargetOne.Execute(acc, instr, useAsk:true, buffer); acted++
  -> if acted>0: log accounts acted; else log no positions found
```

### Placement

- [`OnAskClick`](src/PropTraderTools/TradeCopierPanel.cs:1488): modify guard at line 1492.
- [`OnAskAllClick`](src/PropTraderTools/TradeCopierPanel.cs:1500): replace body entirely (lines 1501-1509).
- `HasNonFlatPosition`: insert as new private static method after [`OnAskAllDown`](src/PropTraderTools/TradeCopierPanel.cs:1515) (approximately line 1516).

---

## Lane B: Bid Pattern-B Conversion + Null-Leader Log

### Current State

[`OnBidClick`](src/PropTraderTools/TradeCopierPanel.cs:1545) (Pattern A): identical structure to OnAskClick, `useAsk: false`.  
[`OnBidAllClick`](src/PropTraderTools/TradeCopierPanel.cs:1557) (Pattern A): identical structure to pre-B216 OnAskAllClick, `useAsk: false`.

### Target State

**OnBidClick** (modified -- DW-B216-01 Part A mirror):
```csharp
// B216: OnBidClick -- cancel Working target + resubmit Limit at Bid + buffer. CYC=4.
// Branches: (1) _instrument null, (2) ?? null-coalescing, (3) _leaderAccount null w/ diagnostic log.
// JS-033: synchronous void. ASCII-only. DW-B216-01 Part A mirror (Bid).
private void OnBidClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null) return;
    _leaderAccount = _leaderAccount ?? TryResolveLeaderAccount();
    if (_leaderAccount == null)
    {
        NinjaTrader.Code.Output.Process(
            "[PTT-MA] Bid: leader null -- check account combo on chart trader",
            NinjaTrader.NinjaScript.PrintTo.OutputTab1);
        return;
    }
    PttMoveTargetOne.Execute(_leaderAccount, _instrument, useAsk: false, _bidBuffer);
}
```

**OnBidAllClick** (rewritten -- Pattern B -- DW-B216-01 Part B mirror):
```csharp
// B216: OnBidAllClick -- Pattern B: iterate Account.All, act on all accounts with non-flat position.
// Mirror of OnAskAllClick. Removes _leaderAccount + _followerItems dependency.
// Branches: (1) _instrument null, (2) foreach Account.All, (3) HasNonFlatPosition,
//           (4) acted==0 log path. CYC=4. JS-021: no lock. ASCII-only.
private void OnBidAllClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null) return;
    int acted = 0;
    foreach (Account acc in Account.All)
    {
        if (HasNonFlatPosition(acc, _instrument))
        {
            PttMoveTargetOne.Execute(acc, _instrument, useAsk: false, _bidBuffer);
            acted++;
        }
    }
    if (acted > 0)
        NinjaTrader.Code.Output.Process(
            "[PTT-MA] Bid-All: instrument=" + _instrument.FullName + " accounts=" + acted,
            NinjaTrader.NinjaScript.PrintTo.OutputTab1);
    else
        NinjaTrader.Code.Output.Process(
            "[PTT-MA] Bid-All: no positions found for " + _instrument.FullName,
            NinjaTrader.NinjaScript.PrintTo.OutputTab1);
}
```

Note: `HasNonFlatPosition` is already present after Lane A ticket. Lane B reuses it without redefinition.

### Data Flow (Lane B)

Mirror of Lane A with `useAsk: false` and "Bid" labels. Identical logic path.

### Placement

- [`OnBidClick`](src/PropTraderTools/TradeCopierPanel.cs:1545): modify guard at line 1549.
- [`OnBidAllClick`](src/PropTraderTools/TradeCopierPanel.cs:1557): replace body entirely (lines 1558-1566).

---

## Lane C: Cancel + Cancel All Button Row

### Current State

[`BuildUI`](src/PropTraderTools/TradeCopierPanel.cs:863): adds rows in this order:
```
root.Children.Add(_beRowPanel);
root.Children.Add(_askRowPanel);
root.Children.Add(_bidRowPanel);
root.Children.Add(_instrRowPanel);
root.Children.Add(_quickRowPanel);       // <-- no _cancelRowPanel after this
BuildCopierSection(root);
root.Children.Add(_statusText);
BuildCollapsibleHeader(root);
root.Children.Add(_contentPanel);
```

No Cancel or Cancel All buttons exist in the always-visible (non-collapsible) area. The only existing Cancel button ([`OnCancel2`](src/PropTraderTools/TradeCopierPanel.cs:1668)) is inside `_contentPanel` (collapsible Position Tools section) and dispatches through [`LogAndDispatchModule`](src/PropTraderTools/TradeCopierPanel.cs:1670).

### Target State

**Field addition** (after `_bidRowPanel` at line ~278):
```csharp
private UniformGrid _cancelRowPanel = null; // B216: 2-col: [Cancel] | [Cancel All]
```

**BuildUI change** (after `root.Children.Add(_quickRowPanel)` at line ~906):
```csharp
root.Children.Add(_cancelRowPanel);  // B216: Cancel row below Quick row
```

**BuildCancelRow** (new private method, called from end of `BuildBufferedButtonsRow`):
```csharp
// B216: BuildCancelRow -- constructs _cancelRowPanel with 2 plain buttons (NO spinner arrows).
// Cancel (left): LogAndDispatchModule -- identical to OnCancel2 (second Cancel in main area).
// Cancel All (right): PttCancelAll.Execute(_instrument) -- cancels all Working/Initialized for instr.
// Style: BrushTeal border + BrushTeal background + White foreground (matches 3x / 3x All row).
// CYC=1 (straight-line construction, no branches). JS-021: no lock. ASCII-only.
private void BuildCancelRow()
{
    _cancelRowPanel = new UniformGrid { Columns = 2, Margin = new Thickness(0, 2, 0, 2) };

    var cancelBtn = new Button { Content = "Cancel" };
    cancelBtn.BorderBrush = BrushTeal;
    cancelBtn.Background = BrushTeal;
    cancelBtn.Foreground = System.Windows.Media.Brushes.White;
    cancelBtn.BorderThickness = new Thickness(2);
    cancelBtn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
    cancelBtn.Background = BrushTeal; // explicit brush after style ref (matches existing pattern)
    cancelBtn.Click += OnCancelClick;
    _cancelRowPanel.Children.Add(cancelBtn);

    var cancelAllBtn = new Button { Content = "Cancel All" };
    cancelAllBtn.BorderBrush = BrushTeal;
    cancelAllBtn.Background = BrushTeal;
    cancelAllBtn.Foreground = System.Windows.Media.Brushes.White;
    cancelAllBtn.BorderThickness = new Thickness(2);
    cancelAllBtn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
    cancelAllBtn.Background = BrushTeal; // explicit brush after style ref
    cancelAllBtn.Click += OnCancelAllClick;
    _cancelRowPanel.Children.Add(cancelAllBtn);
}
```

**OnCancelClick** (new event handler):
```csharp
// B216: OnCancelClick -- second Cancel button in main (non-collapsible) area.
// Behaviour identical to OnCancel2 (both dispatch to PttCancel via LogAndDispatchModule).
// CYC=1. JS-021: no lock. JS-033: synchronous void. ASCII-only.
private void OnCancelClick(object sender, RoutedEventArgs e)
{
    LogAndDispatchModule("[CANCEL]", "CANCEL");
}
```

**OnCancelAllClick** (new event handler):
```csharp
// B216: OnCancelAllClick -- calls PttCancelAll.Execute for ALL accounts.
// Does NOT go through LogAndDispatchModule. Direct static call to PttCancelAll.
// CYC=2: (1) _instrument null guard, (2) Execute call.
// JS-021: no lock. JS-033: synchronous void. ASCII-only.
private void OnCancelAllClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null) return;
    PttCancelAll.Execute(_instrument);
}
```

**BuildBufferedButtonsRow** modification: add `BuildCancelRow();` call at the end of the method (after `root.Children.Add(_quickT3Row)`).

### Call site for BuildCancelRow

End of [`BuildBufferedButtonsRow`](src/PropTraderTools/TradeCopierPanel.cs:1073), after line 1255:
```csharp
root.Children.Add(_quickT3Row);
BuildCancelRow();  // B216: construct _cancelRowPanel (added to root by BuildUI)
```

### New File: PttCancelAll.cs

**Path**: `src/PropTraderTools/Features/PttCancelAll.cs`

```csharp
// src/PropTraderTools/Features/PttCancelAll.cs
// B216 -- PttCancelAll: cancel ALL Working/Initialized orders for ALL accounts + instrument.
// Static class. No IPttModule -- called directly from TradeCopierPanel.OnCancelAllClick.
// Does NOT go through CopyEngine dispatch or PttBus. No leader dependency.
// Dependencies: NinjaTrader.Cbi ONLY + NinjaTrader.Code.Output.
// JS-021: no lock. JS-033: synchronous void. NT8-006: no LINQ -- explicit foreach.

using System.Collections.Generic;
using NinjaTrader.Cbi;

namespace PropTraderTools
{
    /// <summary>
    /// Cancel-All module. Cancels all Working/Initialized orders for ALL accounts
    /// and the given instrument. No leader dependency. No copier state dependency.
    /// Called directly from TradeCopierPanel.OnCancelAllClick.
    /// </summary>
    internal static class PttCancelAll
    {
        /// <summary>
        /// Cancel all Working/Initialized orders for all accounts for instr.
        /// NT8-006: NO LINQ -- explicit foreach.
        /// CYC=5: (1) instr null, (2) foreach Account.All, (3) foreach acc.Orders,
        ///         (4) IsWorkingOrderForInstr, (5) toCancel.Count>0.
        /// JS-021: no lock.
        /// </summary>
        internal static void Execute(Instrument instr)
        {
            if (instr == null)
                return; // (1)

            var toCancel = new List<Order>();
            int totalOrders = 0;
            int accountsActed = 0;

            foreach (Account acc in Account.All) // (2)
            {
                toCancel.Clear();
                foreach (Order o in acc.Orders) // (3)
                {
                    if (IsWorkingOrderForInstr(o, instr)) // (4)
                        toCancel.Add(o);
                }
                if (toCancel.Count > 0) // (5)
                {
                    totalOrders += toCancel.Count;
                    try
                    {
                        acc.Cancel(toCancel.ToArray());
                        accountsActed++;
                    }
                    catch
                    { /* cancel on already-filled orders is non-fatal */ }
                }
            }

            NinjaTrader.Code.Output.Process(
                "[CANCEL-ALL] " + totalOrders + " orders cancelled across "
                    + accountsActed + " accounts for " + instr.FullName,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1);
        }

        /// <summary>
        /// Returns true if order o is Working or Initialized for instr.
        /// Unrestricted -- cancels entry orders AND target orders (unlike PttCancel which
        /// is semantically entry-focused).
        /// CYC=4: (1) o null, (2) stateOk (||), (3) o.Instrument null, (4) FullName match.
        /// JS-002: returns bool. ASCII-only.
        /// </summary>
        private static bool IsWorkingOrderForInstr(Order o, Instrument instr)
        {
            if (o == null)
                return false; // (1)
            bool stateOk =
                o.OrderState == OrderState.Working
                || o.OrderState == OrderState.Initialized; // (2)
            bool instrOk =
                o.Instrument != null                        // (3)
                && o.Instrument.FullName == instr.FullName; // (4)
            return stateOk && instrOk;
        }
    }
}
```

Note: `totalOrders` accumulates the count of matching orders from every account before `acc.Cancel(...)` is called; `accountsActed` counts only accounts where cancellation was attempted. The log line `[CANCEL-ALL] N orders cancelled across M accounts for X` is correct for all multi-account cases.

### Data Flow (Lane C)

```
User clicks Cancel button (new, main area)
  -> OnCancelClick(UI thread)
  -> LogAndDispatchModule("[CANCEL]", "CANCEL")
  -> CopyEngine module dispatch -> PttCancel.Execute -> CancelWorkingEntriesLocal(leader, instr)
  (Identical to existing OnCancel2 behaviour)

User clicks Cancel All button (new, main area)
  -> OnCancelAllClick(UI thread)
  -> if _instrument null: return (silent guard)
  -> PttCancelAll.Execute(_instrument)
     -> foreach acc in Account.All
          -> foreach o in acc.Orders
               -> IsWorkingOrderForInstr(o, instr)?
                    YES: toCancel.Add(o)
          -> if toCancel.Count > 0: acc.Cancel(toCancel.ToArray()); accts++
     -> Output.Process("[CANCEL-ALL] N orders cancelled across M accounts for X")

UI layout (BuildUI root.Children order after B216):
  _beRowPanel       (BE | BE All)
  _askRowPanel      (Ask | Ask-All)
  _bidRowPanel      (Bid | Bid-All)
  _instrRowPanel    (2x | QAll2t)
  _quickRowPanel    (3x | 3x All)
  _cancelRowPanel   (Cancel | Cancel All)  <-- NEW: B216
  BuildCopierSection...
  _statusText
  BuildCollapsibleHeader
  _contentPanel (Position Tools, collapsible)
```

### Visual Spec (Lane C)

Both Cancel and Cancel All buttons:
- `BorderBrush = BrushTeal`
- `Background = BrushTeal` (set AFTER SetResourceReference to ensure explicit brush wins)
- `Foreground = System.Windows.Media.Brushes.White`
- `BorderThickness = new Thickness(2)`
- Style: `"NTButtonStyle"` via SetResourceReference
- **NO** RepeatButton arrows (plain Button, not DockPanel+arrows cluster)
- Content: `"Cancel"` (left) and `"Cancel All"` (right)

---

## Threading Model

All new code executes on the WPF UI thread (all methods are synchronous event handlers or synchronous helpers called from event handlers). No Dispatcher.InvokeAsync required.

- `OnAskClick`, `OnAskAllClick`, `OnBidClick`, `OnBidAllClick`: UI thread event handlers. NT8 API calls (`Account.All`, `acc.Positions`, `PttMoveTargetOne.Execute`) are safe from UI thread. No threading concern.
- `OnCancelClick`: delegates to `LogAndDispatchModule` which handles its own internal threading. No change.
- `OnCancelAllClick` -> `PttCancelAll.Execute`: UI thread. `acc.Cancel(Order[])` is NT8 API safe from UI thread. `Output.Process` is NT8 API thread-safe.
- `HasNonFlatPosition`: pure read of `acc.Positions` on UI thread. Safe.

**No new `Dispatcher.InvokeAsync` calls required anywhere in B216.**

---

## NT8 API Surface (Verified From Existing Code)

| API | Confirmation Source | Usage |
|-----|---------------------|-------|
| `Account.All` | Standard NT8 API | Iterate all accounts in OnAskAllClick/OnBidAllClick/PttCancelAll |
| `acc.Orders` | PttCancel.cs:72 | Iterate orders per account in PttCancelAll |
| `acc.Cancel(Order[])` | PttCancel.cs:83 | Cancel order array in PttCancelAll.Execute |
| `acc.Positions` | NT8 Account member | Iterate positions in HasNonFlatPosition |
| `pos.Instrument.FullName` | PttCancel.cs:108 (order pattern) | Instrument match in HasNonFlatPosition |
| `pos.Quantity` | NT8 Position member | Non-flat check (`!= 0`) in HasNonFlatPosition |
| `OrderState.Working` | PttCancel.cs:107 | Order filter in PttCancelAll.IsWorkingOrderForInstr |
| `OrderState.Initialized` | PttCancel.cs:107 | Order filter in PttCancelAll.IsWorkingOrderForInstr |
| `Output.Process(string, PrintTo)` | PttCancel.cs:84-87 | Diagnostic logging |
| `PrintTo.OutputTab1` | PttCancel.cs:86 | Log destination |
| `BrushTeal` | TradeCopierPanel.cs:1225 | Button style (field, not new API) |
| `PttMoveTargetOne.Execute(acc, instr, useAsk, buf)` | TradeCopierPanel.cs:1505 | Pattern B target move |

---

## 7-Scan Checklist (Engineer Contract -- All 3 Tickets)

### SCAN-1: P0 Violations

```powershell
grep -n "lock(" src/PropTraderTools/TradeCopierPanel.cs
grep -n "lock(" src/PropTraderTools/Features/PttCancelAll.cs
grep -n "async void " src/PropTraderTools/TradeCopierPanel.cs
grep -n "return null;" src/PropTraderTools/Features/PttCancelAll.cs
```

Expected: **0 matches** in modified/new lines.

### SCAN-2: dotnet test -- Zero New Failures

```powershell
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj -v minimal
```

Expected: `Failed=9` (pre-existing), `Passed >= 213` (207 + 6 new), zero new failures.

New tests required:
| Test Name | Assertion |
|-----------|-----------|
| `T_B216_A1` | `OnAskClick` with null `_leaderAccount` logs `[PTT-MA] Ask: leader null` to OutputTab1 |
| `T_B216_A2` | `HasNonFlatPosition` returns false for flat position, true for non-flat |
| `T_B216_B1` | `OnBidClick` with null `_leaderAccount` logs `[PTT-MA] Bid: leader null` to OutputTab1 |
| `T_B216_B2` | `HasNonFlatPosition` returns false when Quantity==0, true when Quantity!=0 (duplicate guard/no-duplicate -- see ticket T-B216-B) |
| `T_B216_C1` | `OnCancelAllClick` with null `_instrument` returns without calling `PttCancelAll.Execute` |
| `T_B216_C2` | `PttCancelAll.IsWorkingOrderForInstr` returns false for null order, true for Working+matching instr |

Note: `T_B216_A2` and `T_B216_B2` test `HasNonFlatPosition` (private static). Test via `[InternalsVisibleTo]` or by testing the public behaviour of `OnAskAllClick`/`OnBidAllClick`. If the method cannot be made accessible, test via `PttCancelAll.IsWorkingOrderForInstr` pattern (private static test via reflection or via internal accessor).

### SCAN-3: CYC <= 8 (Verify by Inspection)

Verify all methods listed in CYC Budget table are within budget.
All target CYCs verified in architecture Thought 3. Engineer must confirm by inspection after writing.

### SCAN-4: ASCII-Only

```powershell
[System.IO.File]::ReadAllBytes("src/PropTraderTools/TradeCopierPanel.cs") `
    | Where-Object { $_ -gt 0x7E } | Measure-Object
[System.IO.File]::ReadAllBytes("src/PropTraderTools/Features/PttCancelAll.cs") `
    | Where-Object { $_ -gt 0x7E } | Measure-Object
```

Expected: **count 0** in new/changed regions.

### SCAN-5: Symbol Existence

```powershell
Select-String "OnAskAllClick" src/PropTraderTools/TradeCopierPanel.cs
Select-String "OnBidAllClick" src/PropTraderTools/TradeCopierPanel.cs
Select-String "OnCancelClick" src/PropTraderTools/TradeCopierPanel.cs
Select-String "OnCancelAllClick" src/PropTraderTools/TradeCopierPanel.cs
Select-String "HasNonFlatPosition" src/PropTraderTools/TradeCopierPanel.cs
Select-String "PttCancelAll" src/PropTraderTools/Features/PttCancelAll.cs
Select-String "_cancelRowPanel" src/PropTraderTools/TradeCopierPanel.cs
Select-String "BuildCancelRow" src/PropTraderTools/TradeCopierPanel.cs
```

Expected: all symbols found.

### SCAN-6: Functional Correctness (Acceptance Criteria)

Verify each acceptance criterion in the AC table (below) via NT8 SIM test or unit test as applicable.

### SCAN-7: Diff Hygiene

```powershell
git diff HEAD -- src/PropTraderTools/TradeCopierPanel.cs `
    | Select-String "^@@" | Measure-Object
powershell -File .\scripts\verify_pr_hygiene.ps1
powershell -File .\scripts\verify_critical_symbols.ps1
powershell -File .\scripts\ptt-sync.ps1
```

Expected: multiple hunks (NOT single-hunk), hygiene PASS, sync 21/0.

---

## Acceptance Criteria

| AC | Criterion | Ticket |
|----|-----------|--------|
| AC-A1 | `OnAskClick` with null `_leaderAccount` logs `[PTT-MA] Ask: leader null -- check account combo on chart trader` to OutputTab1 | T-B216-A |
| AC-A2 | `OnAskAllClick` no longer requires `_leaderAccount` -- fires `PttMoveTargetOne.Execute` for all accounts with non-flat position | T-B216-A |
| AC-A3 | `OnAskAllClick` logs `[PTT-MA] Ask-All: instrument=X accounts=N` when N>0 positions found | T-B216-A |
| AC-B1 | `OnBidClick` with null `_leaderAccount` logs `[PTT-MA] Bid: leader null -- check account combo on chart trader` to OutputTab1 | T-B216-B |
| AC-B2 | `OnBidAllClick` no longer requires `_leaderAccount` -- fires `PttMoveTargetOne.Execute` for all accounts with non-flat position | T-B216-B |
| AC-B3 | `OnBidAllClick` logs `[PTT-MA] Bid-All: instrument=X accounts=N` when N>0 positions found | T-B216-B |
| AC-C1 | `_cancelRowPanel` is visible in UI directly below `_quickRowPanel` | T-B216-C |
| AC-C2 | Cancel button (left) calls `LogAndDispatchModule("[CANCEL]", "CANCEL")` (identical to OnCancel2) | T-B216-C |
| AC-C3 | Cancel All button (right) calls `PttCancelAll.Execute(_instrument)` | T-B216-C |
| AC-C4 | `PttCancelAll.cs` exists at `src/PropTraderTools/Features/PttCancelAll.cs`, passes JS-DNA scan | T-B216-C |
| AC-C5 | No spinner arrows (no RepeatButton `^`/`v`) on Cancel or Cancel All buttons | T-B216-C |
| AC-C6 | Cancel and Cancel All buttons have BrushTeal border, BrushTeal background, white foreground | T-B216-C |
| AC-ALL | Zero new test failures (`dotnet test Failed` count does not exceed 9) | All tickets |
| AC-ALL | `ptt-sync 21/0` after every `.cs` edit | All tickets |
| AC-ALL | `DIFF GUARD PASS` (`verify_pr_hygiene.ps1`) | All tickets |

---

## Ticket Execution Order

1. **T-B216-A** (Lane A): Modify `OnAskClick` (null-leader log) + rewrite `OnAskAllClick` (Pattern B) + add `HasNonFlatPosition` private static helper.
2. **T-B216-B** (Lane B): Modify `OnBidClick` (null-leader log) + rewrite `OnBidAllClick` (Pattern B). Depends on `HasNonFlatPosition` from T-B216-A.
3. **T-B216-C** (Lane C): Add `_cancelRowPanel` field + `BuildCancelRow()` method + `OnCancelClick` + `OnCancelAllClick` in `TradeCopierPanel.cs` + create `PttCancelAll.cs`.

Each ticket is a single PR. ptt-sync 21/0 after every `.cs` edit. 7-scan checklist required per ticket before VERIFY_PASS.

---

## Risks and Open Questions

### Risk 1: HasNonFlatPosition test accessibility
`HasNonFlatPosition` is `private static`. Unit tests cannot access it directly. Mitigation: test via `OnAskAllClick`/`OnBidAllClick` behaviour (T_B216_A2 tests the guard -- flat account not acted upon, non-flat account acted upon). If NT8-bound `PttMoveTargetOne.Execute` is not mockable, test the predicate via `PttCancelAll.IsWorkingOrderForInstr` pattern (same structural shape, different semantics).

### Risk 2: Account.All position iteration on UI thread
`acc.Positions` is a live NT8 collection. Iterating on UI thread is safe per NT8 documentation. The `foreach` is lock-free (no snapshot required). No known risk.

### Risk 3: DW-B209-01 Guardian false positive
`verify_critical_symbols.ps1` may report false-positive GUARDIAN FAIL for `DW-B209-02` (`_instr2tBtn.Background`). This is pre-existing (open as DW-B209-01). B216 does not fix it. If Guardian FAIL is reported during SCAN-7, confirm it is the same pre-existing DW-B209-01 false positive and document in ticket completion notes.

### Non-issue: OnCancel2 unchanged
`OnCancel2` at line 1668 is NOT modified by B216. The new `OnCancelClick` is a SECOND Cancel button in the main (non-collapsible) area -- it does not replace `OnCancel2`. Both exist simultaneously after B216.

### Non-issue: PttMoveTargetAll.cs (DW-B212-02)
Pattern B conversion (OnAskAllClick/OnBidAllClick) no longer calls `PttMoveTargetAll.Execute`. This does not constitute an authorization to delete or deprecate `PttMoveTargetAll.cs`. DW-B212-02 remains open. Do NOT delete `PttMoveTargetAll.cs`.

---

*Plan produced by ptt-architect, Phase 1. All 9 sequential thoughts completed with zero violations found.*
