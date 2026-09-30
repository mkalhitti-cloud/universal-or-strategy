# PTT-COPIER B41 — Quick Exit: Per-Instrument Bracket Swap
## Architecture Plan — B41-LaneA

**Status**: PLAN_COMPLETE  
**Spec**: `specs/002-trade-copier-spec.html` id="section-b41" (lines 18595–19566)  
**Baseline build tag**: `"PTT-COPIER B40 | be-all-armed-oco-fix | 2026-07-30"`  
**Baseline [Fact] count**: 217  
**Target [Fact] count**: >= 234 (217 + 17 new tests; orchestrator floor: >= 231)  
**Brain dir**: `docs/brain/B41-LaneA/`

---

## 1. Purpose and Scope Summary

B41 adds two new ChartTrader panel buttons:

| Button | Scope | Works without CopyRule? |
|--------|-------|------------------------|
| `[Quick +4t ▲▼]` | This chart's `_instrument` — leader + all followers for that rule | Yes — InstrumentDefaults fallback |
| `[Quick ALL]` | `Account.All × positions` — every account, every instrument with a non-flat position | Yes — always |

Each press is a **complete idempotent bracket swap**: cancel existing PTT-QX orders + cancel ATM bracket, resubmit PTT-QX-Stop + PTT-QX-T1 + PTT-QX-T2 at prices computed from `pos.AveragePrice ± ticks * tickSize`. Press again at any time to swap to a new configuration.

T1 and T2 are linked: T2 = T1 × 2 by default. Pressing `▲` adds +1t to T1, +2t to T2. `▼` subtracts symmetrically.

**T3 row**: hidden by default (`Visibility.Collapsed`). Auto-shows when `SnapshotTargets()` returns 3+ Working targets. Driven by `UpdateT3Visibility()`. `CopyRule.QuickT3Ticks = 0` means hidden.

**Card A (Live Sync)**: After a `[Quick]` press, `RefreshQuickDisplay()` back-calculates actual tick distances from live `PTT-QX-T1` order price to spinner. Spinners reflect live bracket, not just the configured default.

**Card B (Window Sync)**: `TradeCopierWindow` subscribes to `PttBus.QuickExitFired` and independently back-calculates from event payload. `QuickExitEventArgs.TickSize` field enables this without polling.

---

## 2. Full File List

### 2.1 New Files (2)

| File | Purpose | ~Lines |
|------|---------|--------|
| `src/PropTraderTools/Features/PttQuickExit.cs` | Per-chart Quick Exit logic: 9-step Execute() + SnapshotStopPrice() + InstrumentDefaults inner class | ~85 |
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | All-accounts Quick Exit: Execute() loop + ResolveQuickTicks() | ~60 |

### 2.2 Modified Files (5 source + 1 test)

| File | Scope of Change |
|------|----------------|
| `src/PropTraderTools/Core/PttContracts.cs` | Add `QuickExitEventArgs` (7 fields) + `PttBus.QuickExitFired` event + `PttBus.RaiseQuickExit()` |
| `src/PropTraderTools/CopyEngine.cs` | (A) `CopyRule` struct: 3 new int fields + ctor update + `Create()` factory update; (B) `CopyRuleDto`: 3 new serialized int properties; (C) `RuleToDto`/`DtoToRule`: round-trip new fields; (D) `CancelStaleBrackets`: add `cancelPttQx` bool param; (E) new `SetQuickTicks()` engine method; (F) build tag update to B41 |
| `src/PropTraderTools/TradeCopierPanel.cs` | (A) `BuildBufferedButtonsRow`: Row 2 → 3-col + Quick cluster, Row 3 → full-width Quick ALL, add `_quickT3Row`; (B) `BuildClickTraderRow`: StackPanel → UniformGrid 4-col + `_cancelBtn`; (C) `BuildModeRow`: move `_copyToggleBtn2` here as ToggleButton; (D) new `RefreshQuickDisplay()`; (E) new `UpdateT3Visibility()`; (F) new `FindWorkingOrder()` helper; (G) new event handlers; (H) wire call sites |
| `src/PropTraderTools/TradeCopierWindow.cs` | Subscribe `PttBus.QuickExitFired`; add `OnWindowQuickExitFired()` handler; on-open call `RefreshQuickDisplay()` |
| `src/PropTraderTools/tests/CopyEngineTests.cs` | Add T_B41_01 through T_B41_17 (17 [Fact] tests) |

> **Note on "CopyRule.cs"**: There is no separate `CopyRule.cs` file. `CopyRule` is a `private readonly struct` nested inside `CopyEngine` at L172–225 of `CopyEngine.cs`. All "CopyRule.cs" spec references map to edits in `CopyEngine.cs`.

---

## 3. Method Signatures — All New Methods with CYC Budget

### 3.1 PttQuickExit.cs — `internal sealed class PttQuickExit`

```csharp
// CYC=7 (null/flat guard x2 + snapshotStop guard + isLong branch x3 + T1-null + T2-null guards)
internal void Execute(Account leader, Instrument instr, int t1Ticks, int t2Ticks)
// Returns void. No return null. No lock(). Uses Interlocked.Increment(ref _qxSeq).
// CreateOrder arg12: (CustomOrder)null. TimeInForce.Gtc + DateTime.MaxValue for GTC.

// CYC=3 (instrument match loop + stop-type check + state Working/Accepted check)
private static double SnapshotStopPrice(Account acc, Instrument instr)
// Returns 0.0 if no stop found (never returns null — returns double).

// volatile int field (NT8-003: not volatile double)
private volatile int _qxSeq = 0;
```

### 3.2 PttQuickExit.cs — `internal static class InstrumentDefaults` (nested in PttQuickExit.cs)

```csharp
// CYC=3 (null/empty guard + MES prefix check + MGC prefix check)
internal static (int t1, int t2) GetQuickTicks(string masterName)
// "MES*" -> (4, 8); "MGC*" -> (2, 4); * -> (4, 8). No return null. ASCII-only strings.
```

### 3.3 PttGlobalQuickExit.cs — `internal sealed class PttGlobalQuickExit`

```csharp
// CYC=3 (acc loop + pos loop + null/flat continue)
internal void Execute()
// foreach Account.All × Positions; skip null/flat; ResolveQuickTicks → ExecuteOne

// CYC=2 (rule null check branch)
private (int t1, int t2) ResolveQuickTicks(Instrument instr)
// FindRule → rule ticks if found; else PttQuickExit.InstrumentDefaults.GetQuickTicks(masterName)

// private — delegates to PttQuickExit per-account execution
private void ExecuteOne(Account acc, Instrument instr, int t1Ticks, int t2Ticks)
// Instantiates PttQuickExit, calls Execute(). CYC=1.

// volatile int field (NT8-003)
private volatile int _qxSeq = 0;
```

### 3.4 PttContracts.cs additions

```csharp
// PttBus additions:
internal static event EventHandler<QuickExitEventArgs> QuickExitFired;
internal static void RaiseQuickExit(object sender, QuickExitEventArgs e)
// CYC=1 (straight null-conditional invoke)

// New EventArgs class:
public sealed class QuickExitEventArgs : EventArgs
{
    public Instrument Instrument  { get; private set; }  // NT8-001: private set
    public double     EntryPrice  { get; private set; }  // pos.AveragePrice at press time
    public double     T1Price     { get; private set; }  // absolute price of PTT-QX-T1
    public double     T2Price     { get; private set; }  // absolute price of PTT-QX-T2
    public bool       IsLong      { get; private set; }
    public string     OcoId       { get; private set; }
    public double     TickSize    { get; private set; }  // Card B: enables back-calc in Window
    // All-field ctor. CYC=1.
    public QuickExitEventArgs(Instrument instr, double entryPrice, double t1Price, double t2Price,
                               bool isLong, string ocoId, double tickSize)
}
```

### 3.5 CopyEngine.cs — CopyRule struct additions

```csharp
// 3 new readonly int fields inside private readonly struct CopyRule:
internal readonly int QuickT1Ticks;   // default: from GetDefaultQuickTicks()
internal readonly int QuickT2Ticks;   // default: QuickT1Ticks * 2
internal readonly int QuickT3Ticks;   // 0 = no T3 (Visibility.Collapsed)

// Updated private CopyRule constructor (add 3 params):
private CopyRule(string instrument, Account master, Account[] followers, bool enabled,
                 int[] multipliers, Dictionary<string, FollowerAtmMode> atmTemplates,
                 int tightenTicks, int quickT1Ticks, int quickT2Ticks, int quickT3Ticks)

// Updated factory (3 new optional params with hardcoded defaults — safe at construction):
internal static CopyRule Create(string instrument, Account master, Account[] followers,
    bool enabled = true, int[] multipliers = null,
    Dictionary<string, FollowerAtmMode> atmTemplates = null, int tightenTicks = 5,
    int quickT1Ticks = 0, int quickT2Ticks = 0, int quickT3Ticks = 0)
// When quickT1Ticks == 0 at construction, pre-populate from GetDefaultQuickTicks(instrument).
// CYC=2 (+1 for the 0-check).

// New private static helper in CopyEngine.cs (mirrors InstrumentDefaults logic, no cross-file dep):
private static (int t1, int t2) GetDefaultQuickTicks(string instrName)
// CYC=3 (null/empty + MES + MGC). Same logic as InstrumentDefaults — avoids compile ordering issue.

// New engine-level method for panel spinner → rule update (mirrors SetFollowerMultiplier pattern):
internal void SetQuickTicks(string instrument, int t1, int t2)
// Finds rule by instrument, creates new CopyRule with updated Quick ticks, replaces in _rules.
// CYC=2 (find loop + found check). No lock() — called on NT8 main thread only.
```

### 3.6 CopyEngine.cs — CancelStaleBrackets updated signature

```csharp
// B41: add cancelPttQx bool param alongside existing cancelPttBe (B33 U2)
// CYC stays <= 5 (null guards + filter clauses)
private void CancelStaleBrackets(Account leaderAcc, Instrument instr,
    bool cancelPttBe = false, bool cancelPttQx = false)
// All existing call sites pass no new args — defaults preserve current behaviour.
// Quick Exit submit path: cancelPttQx:true (wipe previous QX bracket).
// Flat event path: cancelPttBe:true, cancelPttQx:true (full cleanup).
```

### 3.7 TradeCopierPanel.cs — New Methods

```csharp
// Card A — CYC=4 (t1Ord null + pos null + isLong branch + clamp branch)
private void RefreshQuickDisplay(Account acc, Instrument instr)
// Finds PTT-QX-T1 Working order; back-calcs liveT1 from (LimitPrice - AveragePrice) / tick.
// Clamps liveT1 >= 1. Dispatches Dispatcher.InvokeAsync(() => { _quickT1ValueBox.Text = ...; })
// Updates display ONLY — does NOT call SetQuickTicks on CopyRule.

// CYC=2 (targets null check + count >= 3 check)
private void UpdateT3Visibility(Account acc, Instrument instr)
// Calls CopyEngine.Instance.SnapshotTargets(acc, instr); shows/hides _quickT3Row.

// CYC=2 (instrument match + name match + Working state)
private static Order FindWorkingOrder(Account acc, Instrument instr, string orderName)
// Returns first matching Working order; returns null if none (used in RefreshQuickDisplay null guard).

// Event handlers (all CYC=1 — straight dispatch):
private void OnQuickClick(object sender, RoutedEventArgs e)
private void OnQuickAllClick(object sender, RoutedEventArgs e)
private void OnQuickUp(object sender, RoutedEventArgs e)
private void OnQuickDown(object sender, RoutedEventArgs e)
```

### 3.8 TradeCopierWindow.cs — New Method

```csharp
// Card B — CYC=2 (isLong branch + null guard)
private void OnWindowQuickExitFired(object sender, QuickExitEventArgs e)
// Back-calcs liveT1 = Math.Round((isLong ? T1-Entry : Entry-T1) / TickSize)
// Dispatches Dispatcher.InvokeAsync(() => { /* update window spinner displays */ })
```

---

## 4. Panel Layout — B41 Final (supersedes B40)

```
Row 1 (exits):   [Trim +0   [▲][▼]]  [Flatten +0  [▲][▼]]     <- unchanged
Row 2 (mgmt):    [BE +1 [▲][▼]]  [BE ALL]  [Quick +4t [▲][▼]]  <- 3-col UniformGrid (was 2-col)
Row 3 (quick):   [Quick ALL                                   ]  <- full-width teal
[T3 row]:        [Quick T3 +12t [▲][▼]]  (Visibility.Collapsed) <- new hidden row
[status bar]:    unchanged
Click row:       [Buy]  [Sell]  [Arm]  [Cancel]                 <- 4-col UniformGrid (was StackPanel)
Mode row:        ○Signal  ○Mirror  [● COPY ON/OFF]              <- ToggleButton appended (green border)
Tighten row:     [Tighten] [5] [tks]                            <- unchanged
Row 4 (risk):    [Risk $  200 [▲][▼]]  [ATR %  0.75 [▲][▼]]   <- unchanged
```

### WPF Change Details (TradeCopierPanel.cs)

| Element | B40 State | B41 Change |
|---------|-----------|------------|
| Row 2 (`BuildBufferedButtonsRow`) | `UniformGrid Columns=2` (BE + BE ALL) | `Columns=3` + Quick cluster (DockPanel: `_quickBtn` main button + `_quickUp`/`_quickDown` RepeatButtons) |
| Row 3 (`BuildBufferedButtonsRow`) | `UniformGrid Columns=2` (Cancel + COPY toggle) | **Replace entirely** with full-width `_quickAllBtn` Button (teal `BorderBrush`, teal `Foreground`, `BorderThickness=2`) |
| T3 row (new) | Does not exist | New `_quickT3Row` StackPanel/UniformGrid with `Visibility.Collapsed`. `UpdateT3Visibility()` toggles it |
| Click trader row (`BuildClickTraderRow`) | Horizontal `StackPanel` (Buy + Sell + Arm) | Replace with `UniformGrid Columns=4`. Add `_cancelBtn2` as 4th child (red `BorderBrush`) |
| Mode row (`BuildModeRow`) | Horizontal StackPanel (Signal + Mirror radio buttons) | Append `_copyToggleBtn2` `ToggleButton` as last child (green `BorderBrush`) |

**Note for engineer**: `_copyToggleBtn2` and `_cancelBtn2` already exist as field variables in the B40 codebase (constructed in `BuildBufferedButtonsRow`). In B41 they **relocate** to new construction sites. Remove the old construction from `BuildBufferedButtonsRow` Row 3 and add new construction in the new locations. The field variables and event handlers (`OnCopyToggle`, `OnCancel2`) remain unchanged.

---

## 5. Implementation Order (Build-Safe at Each Step)

```
STEP 1  PttContracts.cs
        Add QuickExitEventArgs + PttBus.QuickExitFired + RaiseQuickExit()
        BUILD: dotnet build -> 0 errors (new class only, no deps)

STEP 2  CopyEngine.cs (4 edit areas + 2 new methods)
        A: CopyRule struct — add 3 int fields, update ctor, update Create() factory
        B: CopyRuleDto — add 3 int properties with { get; set; } for XmlSerializer
        C: RuleToDto — emit QuickT1Ticks, QuickT2Ticks, QuickT3Ticks
        D: DtoToRule — read new fields with backward-compat defaults (0 -> GetDefaultQuickTicks)
        E: CancelStaleBrackets — add cancelPttQx bool param
        F: Add private static GetDefaultQuickTicks(string instrName) helper
        G: Add internal void SetQuickTicks(string instrument, int t1, int t2) engine method
        H: Update build tag: "PTT-COPIER B41 | quick-exit | {date}"
        BUILD: dotnet build -> 0 errors

STEP 3  CopyEngineTests.cs — CancelStaleBrackets tests (can run in parallel with STEP 4)
        Add T_B41_09, T_B41_10, T_B41_11
        RUN: dotnet test -> T_B41_09..11 pass

STEP 4  Create src/PropTraderTools/Features/PttQuickExit.cs
        Implement PttQuickExit.Execute() + SnapshotStopPrice() + InstrumentDefaults
        HARD LINK: powershell -File scripts\verify_links.ps1 -Fix  ← MANDATORY #1
        Confirm PttQuickExit.cs present in NT AddOns Features/ folder
        BUILD: dotnet build -> 0 errors
        Add T_B41_01..08, T_B41_12..14 to tests
        RUN: dotnet test -> 11 new tests pass

STEP 5  Create src/PropTraderTools/Features/PttGlobalQuickExit.cs
        Implement PttGlobalQuickExit.Execute() + ResolveQuickTicks() + ExecuteOne()
        HARD LINK: powershell -File scripts\verify_links.ps1 -Fix  ← MANDATORY #2
        Confirm PttGlobalQuickExit.cs present in NT AddOns Features/ folder
        BUILD: dotnet build -> 0 errors

STEP 6  TradeCopierPanel.cs (7 edit areas)
        A: BuildBufferedButtonsRow — Row 2 expand + Row 3 replace + _quickT3Row
        B: BuildClickTraderRow — convert to UniformGrid 4-col + _cancelBtn2
        C: BuildModeRow — append _copyToggleBtn2 ToggleButton
        D: Add RefreshQuickDisplay() + FindWorkingOrder() helper
        E: Add UpdateT3Visibility()
        F: Add OnQuickClick, OnQuickAllClick, OnQuickUp, OnQuickDown
        G: Wire call sites in OnOrderUpdate + OnPositionUpdate + OnLoaded (panel attach)
        H: Wire OnCopyToggle + OnCancel2 to new button instances
        Add T_B41_15, T_B41_16
        BUILD: dotnet build -> 0 errors

STEP 7  TradeCopierWindow.cs
        Subscribe PttBus.QuickExitFired in OnLoaded (or Subscribe method)
        Add OnWindowQuickExitFired handler (back-calc liveT1/liveT2, Dispatcher.InvokeAsync)
        Add on-open: panel.RefreshQuickDisplay(_leaderAccount, _instrument)
        Add T_B41_17
        BUILD: dotnet build -> 0 errors

STEP 8  Final verification
        Run all 7 SCAN checks (see Section 6)
        RUN: dotnet test -> 234/234 (or >= 231) [Fact] pass
        F5 NinjaTrader -> 0 errors, 0 warnings
```

---

## 6. 7-Scan Checklist Template (Engineer Contract)

Every item must be PASS before `BUILD_PASS` is declared.

```
SCAN-01: lock() check
  Select-String -Path "src\PropTraderTools\" -Recurse -Pattern "lock\(" -Include "*.cs"
  EXPECTED: 0 matches in any new or modified file
  STATUS: [ ] PASS / [ ] FAIL

SCAN-02: async void check
  Select-String -Path "src\PropTraderTools\" -Recurse -Pattern "async void " -Include "*.cs"
  EXPECTED: 0 matches in any new or modified file
  STATUS: [ ] PASS / [ ] FAIL

SCAN-03: return null check (new code only)
  Review PttQuickExit.cs, PttGlobalQuickExit.cs, TradeCopierPanel.cs new methods
  EXPECTED: no "return null;" in new methods (use early return void, 0.0, or tuple)
  EXCEPTION: FindWorkingOrder() may return null Order — this is a reference type Query, not a
             "missing value" pattern. Guard with null check at call site in RefreshQuickDisplay.
  STATUS: [ ] PASS / [ ] FAIL

SCAN-04: throw new check (new code only)
  Select-String -Path "src\PropTraderTools\Features\" -Pattern "throw new" -Include "*.cs"
  EXPECTED: 0 matches
  STATUS: [ ] PASS / [ ] FAIL

SCAN-05: CYC <= 8 on all new methods
  python scripts/complexity_audit.py
  EXPECTED: 0 violations in PttQuickExit.cs, PttGlobalQuickExit.cs, new panel methods
  Highest expected: PttQuickExit.Execute() = CYC 7
  STATUS: [ ] PASS / [ ] FAIL

SCAN-06: dotnet build
  dotnet build src/PropTraderTools/PropTraderTools.csproj
  EXPECTED: 0 errors, 0 warnings
  STATUS: [ ] PASS / [ ] FAIL

SCAN-07: dotnet test
  dotnet test src/PropTraderTools/
  EXPECTED: >= 231 [Fact] pass (actual: 234 if baseline 217 + 17 new)
  STATUS: [ ] PASS / [ ] FAIL
```

---

## 7. Test Coverage Map — T_B41_01 through T_B41_17

| Test ID | Description | Implementation Under Test |
|---------|-------------|--------------------------|
| T_B41_01 | `QuickExit_LimitPriceComputed_Long_T1` — entry=5000, t1=4t, tick=0.25 → T1 @ 5001.00 | `PttQuickExit.Execute()` price math (long) |
| T_B41_02 | `QuickExit_LimitPriceComputed_Long_T2` — entry=5000, t2=8t → T2 @ 5002.00 | `PttQuickExit.Execute()` price math (long T2) |
| T_B41_03 | `QuickExit_LimitPriceComputed_Short_T1` — short, entry=5000, t1=4t → T1 @ 4999.00 | `PttQuickExit.Execute()` price math (short) |
| T_B41_04 | `QuickExit_LimitPriceComputed_Short_T2` — short, t2=8t → T2 @ 4998.00 | `PttQuickExit.Execute()` price math (short T2) |
| T_B41_05 | `QuickExit_QtySplit_EvenPosition` — qty=4 → t1Qty=2, t2Qty=2 | `Math.Ceiling(qty/2.0)` ceil rule |
| T_B41_06 | `QuickExit_QtySplit_OddPosition` — qty=3 → t1Qty=2, t2Qty=1 | ceil rule on odd qty |
| T_B41_07 | `QuickExit_FlatPosition_NoOrders` — qty=0 → 0 `CreateOrder` calls | null/flat guard step 1 |
| T_B41_08 | `QuickExit_NoPosition_NoOrders` — pos==null → 0 calls | null position guard |
| T_B41_09 | `CancelStaleBrackets_PttQxExcluded_WhenFlagFalse` — `cancelPttQx=false` → PTT-QX-* NOT cancelled | `CancelStaleBrackets` new param |
| T_B41_10 | `CancelStaleBrackets_PttQxIncluded_WhenFlagTrue` — `cancelPttQx=true` → PTT-QX-* cancelled | `CancelStaleBrackets` new param |
| T_B41_11 | `CancelStaleBrackets_PttBeUnaffected_ByQxFlag` — `cancelPttQx=true, cancelPttBe=false` → PTT-BE-* still excluded | flag independence |
| T_B41_12 | `InstrumentDefaults_MES_Returns4And8` — `GetQuickTicks("MES SEP26")` → `(4, 8)` | `InstrumentDefaults.GetQuickTicks` |
| T_B41_13 | `InstrumentDefaults_MGC_Returns2And4` — `GetQuickTicks("MGC SEP26")` → `(2, 4)` | `InstrumentDefaults.GetQuickTicks` |
| T_B41_14 | `InstrumentDefaults_Unknown_ReturnsMesDefault` — `GetQuickTicks("XYZ")` → `(4, 8)` | fallback branch |
| T_B41_15 | `RefreshQuickDisplay_NoLiveOrders_SpinnerUnchanged` — no Working PTT-QX-T1 → spinner value unchanged (Card A) | `RefreshQuickDisplay` null-order guard |
| T_B41_16 | `RefreshQuickDisplay_LiveT1At5004_Long5000_SpinnerEquals4` — T1 Working @ 5004, entry=5000, tick=0.25 → T1=4, T2=8 (Card A) | `RefreshQuickDisplay` back-calc |
| T_B41_17 | `QuickExitEventArgs_TickSizeCarried` — `new QuickExitEventArgs(..., 0.25)` → `args.TickSize == 0.25`, back-calc T1=4, T2=8 (Card B) | `QuickExitEventArgs.TickSize` + back-calc formula |

### Test Baseline Clarification

```
Baseline reported by orchestrator:  217 [Fact]
New tests added in B41:              17 [Fact]  (T_B41_01 through T_B41_17)
Expected total:                     234 [Fact]
Orchestrator floor:               >= 231 [Fact]

Note: spec HTML (section-b41) states "Baseline: 214, Target: >= 228" but
      orchestrator confirmed baseline is 217 (B40 completed). Use 217 as authoritative.
      Test target: 234 (> floor of 231). If pre-existing tests were added between
      B40 completion and B41 start, actual total may differ — dotnet test is the gate.
```

---

## 8. NT8 Compliance Section

All 6 NT8 rules cited per applicable method/file:

| Rule | Description | Applies to | Required pattern |
|------|-------------|-----------|-----------------|
| **NT8-001** | `{ get; init; }` is BANNED | `QuickExitEventArgs` all 7 properties; `CopyRuleDto` new properties | Must use `{ get; private set; }` for EventArgs; `{ get; set; }` for DTO serialization |
| **NT8-002** | `abstract record` / `sealed record` BANNED | `QuickExitEventArgs` | Must be `public sealed class QuickExitEventArgs : EventArgs` |
| **NT8-003** | `volatile double` BANNED | `PttQuickExit._qxSeq`, `PttGlobalQuickExit._qxSeq` | Must be `private volatile int _qxSeq = 0;` (int, not double) |
| **NT8-007** | `CreateOrder` arg12 must be `(CustomOrder)null` | `PttQuickExit.Execute()` — Stop, T1, T2 CreateOrder calls (3x) | `(CustomOrder)null` NOT `null` and NOT `(NinjaTrader.Cbi.CustomOrder)null` |
| **NT8-013** | `DateTime.MaxValue` for GTC | All 3 `CreateOrder` calls in `PttQuickExit.Execute()` | 11th argument = `DateTime.MaxValue` |
| **NT8-049** | Limit/StopMarket arg layout | T1 and T2 CreateOrder (Limit): `arg6=limitPrice, arg7=0`; Stop CreateOrder (StopMarket): `arg6=0, arg7=stopPrice` | Confirmed from spec code at lines 18803–18815 |

### NT8 CreateOrder Reference (per NT8-049 + NT8-007 + NT8-013)

```csharp
// PTT-QX-Stop (StopMarket, GTC): arg6=0, arg7=stopPrice, arg12=(CustomOrder)null
leader.CreateOrder(instr,
    isLong ? OrderAction.Sell : OrderAction.BuyToCover,
    OrderType.StopMarket, OrderEntry.Manual, TimeInForce.Gtc,
    pos.Quantity,          // arg5: qty
    0,                     // arg6: limitPrice = 0 for StopMarket (NT8-049)
    snapshotStop,          // arg7: stopPrice (NT8-049)
    ocoId,                 // arg8: OCO group
    "PTT-QX-Stop",         // arg9: signal name (NT8-014)
    DateTime.MaxValue,     // arg10: expiry = MaxValue for GTC (NT8-013)
    (CustomOrder)null)     // arg11: (NT8-007) NOT null, NOT (Cbi.CustomOrder)null

// PTT-QX-T1 (Limit, GTC): arg6=limitPrice, arg7=0, arg12=(CustomOrder)null
leader.CreateOrder(instr,
    isLong ? OrderAction.Sell : OrderAction.BuyToCover,
    OrderType.Limit, OrderEntry.Manual, TimeInForce.Gtc,
    t1Qty,                 // arg5
    t1Price,               // arg6: limitPrice (NT8-049)
    0,                     // arg7: stopPrice = 0 for Limit (NT8-049)
    ocoId,
    "PTT-QX-T1",           // NT8-014: signal name starts "PTT-QX-"
    DateTime.MaxValue,     // NT8-013
    (CustomOrder)null)     // NT8-007
```

### ASCII-Only Compliance
All new string literals in new files must be ASCII-only. Button labels: "Quick ALL", "Cancel", "COPY OFF" — no Unicode arrows (use RepeatButton content `"\u25B2"` / `"\u25BC"` which is already the pattern in the codebase for Trim/BE arrows).

---

## 9. Hard-Link Gate Callout

> **BLOCKING — CS0246 guaranteed without this step**

Two new `.cs` files in `Features/` require a hard-link sync to the NinjaTrader AddOns folder. F5 will fail with `CS0246 (type not found)` if the hard links are missing.

```powershell
# MANDATORY: run after creating PttQuickExit.cs (STEP 4)
powershell -File scripts\verify_links.ps1 -Fix

# MANDATORY: run after creating PttGlobalQuickExit.cs (STEP 5)
powershell -File scripts\verify_links.ps1 -Fix

# Verify both files are present in NT AddOns folder:
Test-Path "$env:USERPROFILE\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\Features\PttQuickExit.cs"
Test-Path "$env:USERPROFILE\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\Features\PttGlobalQuickExit.cs"
# Both must return: True
```

Both paths must appear before F5. Run `verify_links.ps1 -Fix` twice — once per new file.

---

## 10. Data Flow Summary

```
[User presses Quick ▲▼]
  -> TradeCopierPanel.OnQuickClick()
  -> PttQuickExit.Execute(leader, instr, t1Ticks, t2Ticks)
       |-- Step 1: null/flat guard → void return
       |-- Step 2: SnapshotStopPrice() → snapshotStop double (0 = no stop)
       |-- Step 3: CancelStaleBrackets(cancelPttQx:true) → ATM + old QX cancelled
       |-- Step 4: ocoId = "PTT-QX-" + Interlocked.Increment(_qxSeq).ToString("D5")
       |-- Step 5: isLong, entryPx, tick, t1Price, t2Price, t1Qty, t2Qty
       |-- Step 6: Submit PTT-QX-Stop (if snapshotStop > 0)
       |-- Step 7: Submit PTT-QX-T1 (Limit GTC t1Qty t1Price)
       |-- Step 8: Submit PTT-QX-T2 (Limit GTC t2Qty t2Price)
       |-- Step 9: PttBus.RaiseQuickExit(this, new QuickExitEventArgs(..., TickSize))
              |
              +--> TradeCopierWindow.OnWindowQuickExitFired()
              |       back-calc liveT1/liveT2 from event payload
              |       Dispatcher.InvokeAsync(() => update window spinners)
              |
              +--> PttCopier [DEFERRED — B41 raises event only; fan-out to followers
                              is future block scope — see note below]

[NT8 confirms PTT-QX-T1 → Working]
  -> TradeCopierPanel.OnOrderUpdate()
  -> if (order.Name == "PTT-QX-T1" && order.OrderState == Working)
  -> RefreshQuickDisplay(acc, instr)
       |-- FindWorkingOrder(acc, instr, "PTT-QX-T1") → t1Ord
       |-- if t1Ord null → return (keep default)
       |-- back-calc liveT1 = Math.Round((isLong? T1-Entry : Entry-T1) / tick)
       |-- clamp liveT1 >= 1
       |-- Dispatcher.InvokeAsync(() => { _quickT1ValueBox.Text = liveT1; _quickT2ValueBox.Text = liveT1*2; })

[User presses Quick ALL]
  -> TradeCopierPanel.OnQuickAllClick()
  -> PttGlobalQuickExit.Execute()
       |-- foreach acc in Account.All
       |-- foreach pos in acc.Positions (skip null/flat)
       |-- ResolveQuickTicks(pos.Instrument) → (t1, t2)
       |-- ExecuteOne(acc, instr, t1, t2) [delegates to PttQuickExit per slot]
```

> **PttCopier Fan-Out Note**: The spec states `PttCopier` subscribes to `QuickExitFired` and fans out to followers. However, `PttCopier.cs` is **NOT listed in B41 modified files**. B41 raises the bus event correctly; the fan-out implementation is deferred to a future block. The engineer must NOT add `PttCopier.cs` fan-out wiring in B41 — that is out of scope.

---

## 11. CopyRule Struct — Readonly Constraint Note

`CopyRule` is a `private readonly struct` nested inside `CopyEngine`. It has no mutable instance methods. The spec's `"internal void SetQuickTicks(int t1, int t2)"` **cannot** be a method on `CopyRule` itself.

**Correct implementation**: `CopyEngine.SetQuickTicks(string instrument, int t1, int t2)` — engine-level method following the exact same pattern as `SetFollowerMultiplier()` at `CopyEngine.cs:L375-392`:

```csharp
// Pattern from SetFollowerMultiplier — replicate for SetQuickTicks
internal void SetQuickTicks(string instrument, int t1, int t2)
{
    for (int i = 0; i < _rules.Count; i++)
    {
        if (_rules[i].Instrument == instrument)
        {
            var r = _rules[i];
            _rules[i] = CopyRule.Create(r.Instrument, r.MasterAccount, r.FollowerAccounts,
                r.Enabled, r.FollowerMultipliers, r.FollowerAtmTemplates, r.TightenTicks,
                t1, t2 * 2, r.QuickT3Ticks);
            return;
        }
    }
}
// CYC=2 (loop + found return). No lock() — called on NT8 main thread.
```

The panel calls `CopyEngine.Instance.SetQuickTicks(instrument, t1, t2)` from `OnQuickUp`/`OnQuickDown` handlers if a persistent rule update is desired. However, per spec Card A: spinner adjustments are session-only display changes — `SetQuickTicks` is only called when the user explicitly wants to persist (e.g. `OnApplyRule`). For the spinner increment/decrement, only the TextBox display value changes; `SetQuickTicks` is NOT called on every `▲▼` press.

---

## 12. Deferred Items (Out of B41 Scope)

| Item | Spec Reference | Deferred To |
|------|---------------|-------------|
| PttCopier.cs fan-out subscription to QuickExitFired | spec L18817 | B41b or future block |
| T3 spinner UI (full implementation) | spec B41 T3 row section | T3 row Visibility toggle is in scope; T3 submit logic is deferred |
| B42 portable stop strategy (trail steps on QX stop) | spec Card C (L19436–19517) | B42 — blocked on ARCH-BRACKET-03 probe |

---

*Plan generated by ptt-architect (B41-LaneA Phase 1). All 9 sequential-thinking thoughts complete. Baseline source files read and verified. Ready for ptt-plan-reviewer review.*
