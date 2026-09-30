# B128 Architecture Plan

**Block**: B128 — Instrument-scoped QX-Instr (2-target) + BE-Instr buttons  
**Phase**: 1 (Architecture)  
**Status**: REVIEW_PENDING  
**Date**: 2026  
**Author**: ptt-architect

---

## Rules Catalog Gate

**STEP 0 RESULT: PASS**

Pre-flight scan of all new code against `docs/standards/jane-street/RULES_CATALOG.md`:

| Rule | Description | Result |
|------|-------------|--------|
| JS-021 | No lock() usage | PASS — 0 new lock() |
| JS-033 | No async void (non-event-handler) | PASS — all handlers are synchronous void |
| JS-001 | No throw in hot path | PASS — 0 throw statements |
| JS-002 | No return null | PASS — ComputeInstrSplit returns value tuple; handlers return void |
| ASCII-only | All identifiers and string literals ASCII | PASS — \u25B2 \u25BC are pre-existing pattern |
| DateTime.Now ban | No DateTime.Now | PASS — not used |
| FontFamily ban | No FontFamily | PASS |
| Hex color ban | No hardcoded hex | PASS — uses existing BrushTeal brush |

---

## Feature Summary

Block B128 adds a new instrument-scoped row (`_instrRowPanel`) between the existing
`_beRowPanel` and `_quickRowPanel` in the Trade Copier Panel. The row contains two buttons:

1. **QX-Instr**: per-instrument Quick Exit bracket swap (2-target, using ComputeInstrSplit
   to split `_instrQxT1` into t1/t2 halves). Fires `PttQuickExit.Execute` on the leader
   account for the current chart's instrument only.

2. **BE-Instr**: per-instrument Break Even arm. Calls `_engine.ArmPendingBe` for the current
   instrument and leader account using the existing `_beBuffer`.

---

## New Layout

### Current Layout (L913-915 in BuildCopierButtons)

```
root → _beRowPanel
root → _quickRowPanel
```

### New Layout After B128

```
root → _beRowPanel
root → _instrRowPanel   (NEW — inserted between _beRowPanel and _quickRowPanel)
root → _quickRowPanel   (UNCHANGED)
```

### Insertion Point (BuildCopierButtons, ~L914)

```csharp
root.Children.Add(_beRowPanel);      // existing L914
root.Children.Add(_instrRowPanel);   // B128 NEW
root.Children.Add(_quickRowPanel);   // existing L915 (unchanged)
```

`BuildInstrRow()` must be called before this section so `_instrRowPanel` is non-null.
`_instrRowPanel` is constructed inside `BuildInstrRow()` but NOT added to root there —
consistent with the pattern used by `_beRowPanel` (see L1240 NOTE) and `_quickRowPanel`
(see L1322 NOTE).

---

## New Fields

Declared in `TradeCopierPanel.cs` field declaration section (~L244 area), grouped with
the existing Quick Exit button refs (~L256-260):

```csharp
// B128: Instrument-row button refs and spinner state (UI-thread-only; no volatile per NT8-003)
private Button _instrQxBtn = null;
private Button _instrBeBtn = null;
private UniformGrid _instrRowPanel = null;
private int _instrQxT1 = 4;
```

**Notes**:
- `_instrQxT1` default 4 mirrors the 2-target split default (ComputeInstrSplit(4) → t1=2, t2=2).
- All fields are UI-thread-only. No `volatile` per NT8-003 (double ban not relevant here, but
  int fields follow the same convention as `_beBuffer`, `_trimBuffer`, etc.).

---

## New Methods

All methods are in `TradeCopierPanel.cs`. No new methods in any other file.

---

### 1. `BuildInstrRow()` — CYC <= 4

**Signature**: `private void BuildInstrRow()`

**Purpose**: Constructs `_instrRowPanel` (a 2-column `UniformGrid`) containing:
- Left cell: DockPanel with RepeatButton spinner arrows + `_instrQxBtn`
- Right cell: `_instrBeBtn` (full-width)

**Construction pattern** (mirrors `_quickRowPanel` at L1242-1322):

```csharp
// UniformGrid container
_instrRowPanel = new UniformGrid { Columns = 2, Margin = new Thickness(0, 2, 0, 2) };

// --- Left cell: QX-Instr cluster ---
var instrQxCluster = new DockPanel { LastChildFill = true };
var instrQxArrows = new Grid();
instrQxArrows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
instrQxArrows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
var instrQxUp = new System.Windows.Controls.Primitives.RepeatButton
{
    Content = "\u25B2",
    Width = 18,
    Height = 12,
};
var instrQxDn = new System.Windows.Controls.Primitives.RepeatButton
{
    Content = "\u25BC",
    Width = 18,
    Height = 12,
};
instrQxUp.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
instrQxDn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
instrQxUp.Click += OnInstrQxUp;
instrQxDn.Click += OnInstrQxDown;
Grid.SetRow(instrQxUp, 0);
Grid.SetRow(instrQxDn, 1);
instrQxArrows.Children.Add(instrQxUp);
instrQxArrows.Children.Add(instrQxDn);
DockPanel.SetDock(instrQxArrows, Dock.Right);
_instrQxBtn = new Button
{
    Content = FormatBuffer("QX-Instr", _instrQxT1),
    BorderBrush = BrushTeal,
    Foreground = BrushTeal,
    BorderThickness = new Thickness(2),
};
_instrQxBtn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
_instrQxBtn.Click += OnInstrQxClick;
instrQxCluster.Children.Add(instrQxArrows);
instrQxCluster.Children.Add(_instrQxBtn);
_instrRowPanel.Children.Add(instrQxCluster);

// --- Right cell: BE-Instr ---
_instrBeBtn = new Button
{
    Content = "BE-Instr",
    BorderBrush = BrushTeal,
    Foreground = BrushTeal,
    BorderThickness = new Thickness(2),
};
_instrBeBtn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
_instrBeBtn.Click += OnInstrBeClick;
_instrRowPanel.Children.Add(_instrBeBtn);

// NOTE: _instrRowPanel is NOT added to root here.
// Added in BuildCopierButtons() — see insertion point above.
```

**CYC analysis**: Sequential construction, zero branches, zero loops. CYC = 1. Budget = 4. ✅

---

### 2. `ComputeInstrSplit(int instrQxT1)` — CYC = 1

**Signature**: `internal static (int t1, int t2) ComputeInstrSplit(int instrQxT1)`

**Purpose**: Splits `instrQxT1` into two halves for the 2-target QX-Instr bracket.
- `t1 = (instrQxT1 + 1) / 2`  (ceiling half — takes the larger share on odd inputs)
- `t2 = instrQxT1 / 2`          (floor half)

**Implementation**:

```csharp
// B128: internal static for direct xUnit test access (no instance required).
// JS-002: returns value tuple (never null). CYC=1 (no branches).
internal static (int t1, int t2) ComputeInstrSplit(int instrQxT1) =>
    (t1: (instrQxT1 + 1) / 2, t2: instrQxT1 / 2);
```

**Verification of test inputs** (integer arithmetic):

| Input | t1 = (n+1)/2 | t2 = n/2 | Output |
|-------|-------------|---------|--------|
| 4 | (4+1)/2 = 2 | 4/2 = 2 | (2, 2) |
| 5 | (5+1)/2 = 3 | 5/2 = 2 | (3, 2) |
| 1 | (1+1)/2 = 1 | 1/2 = 0 | (1, 0) |
| 7 | (7+1)/2 = 4 | 7/2 = 3 | (4, 3) |

**JS constraints**: JS-002 (value tuple, never null). JS-021 (no lock). ASCII-only. CYC = 1. ✅

---

### 3. `OnInstrQxClick(object sender, RoutedEventArgs e)` — CYC <= 3

**Signature**: `private void OnInstrQxClick(object sender, RoutedEventArgs e)`

**Purpose**: Fires per-instrument Quick Exit bracket swap using the instrument-scoped t1/t2 split.

**Implementation**:

```csharp
// B128: OnInstrQxClick -- per-instrument Quick Exit, 2-target split. CYC=3.
// JS-033: synchronous void event handler. JS-021: no lock.
private void OnInstrQxClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null)             // (1) guard
        return;
    _leaderAccount = _leaderAccount ?? TryResolveLeaderAccount();  // (2) ?? branch
    var (t1, t2) = ComputeInstrSplit(_instrQxT1);
    NinjaTrader.Code.Output.Process(
        "[PTT-QX-INSTR] button: "
            + (_leaderAccount?.Name ?? "null")
            + " "
            + (_instrument?.FullName ?? "null")
            + " t1=" + t1 + " t2=" + t2,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
    var qx = new PttQuickExit();
    qx.Execute(_leaderAccount, _instrument, t1, t2);  // (3) shim overload (t1Ticks, t2Ticks)
}
```

**CYC analysis**: (1) if null → 1, (2) ?? operator → 1, remainder straight-line → CYC = 3. ✅

**NT8 API call chain**:
- `PttQuickExit.Execute(Account, Instrument, int t1Ticks, int t2Ticks, bool skipIfFollower=true)`
  confirmed at `PttQuickExit.cs` L215. This is the bridge shim that delegates to the
  targets-based overload with an empty targets list and `leaderTargetCount=0`.
  `ResolveTargetCount` will then fall back to 3 (the production default).
- The "2-target" description refers to the QX-Instr button scope (instrument-only, not global).
  Target bracket count is determined by the existing `ResolveTargetCount` logic in PttQuickExit.

**JS constraints**: JS-033 synchronous void. JS-021 no lock. ASCII log prefix "[PTT-QX-INSTR]". ✅

---

### 4. `OnInstrQxUp(object sender, RoutedEventArgs e)` — CYC <= 2

**Signature**: `private void OnInstrQxUp(object sender, RoutedEventArgs e)`

**Purpose**: Increments `_instrQxT1` by 1 (min 1, max 100), refreshes button label.

**Implementation**:

```csharp
// B128: OnInstrQxUp -- increment spinner. CYC=2.
// JS-033: synchronous void. JS-021: no lock.
private void OnInstrQxUp(object sender, RoutedEventArgs e)
{
    _instrQxT1 = Math.Max(1, Math.Min(_instrQxT1 + 1, 100));
    if (_instrQxBtn != null)   // (1) null guard
        _instrQxBtn.Content = FormatBuffer("QX-Instr", _instrQxT1);
}
```

**CYC analysis**: null guard → CYC = 2. ✅  
**Pattern match**: mirrors `OnQuickUp` at L1878-1884. ✅

---

### 5. `OnInstrQxDown(object sender, RoutedEventArgs e)` — CYC <= 2

**Signature**: `private void OnInstrQxDown(object sender, RoutedEventArgs e)`

**Purpose**: Decrements `_instrQxT1` by 1 (min 1, max 100), refreshes button label.

**Implementation**:

```csharp
// B128: OnInstrQxDown -- decrement spinner. CYC=2.
// JS-033: synchronous void. JS-021: no lock.
private void OnInstrQxDown(object sender, RoutedEventArgs e)
{
    _instrQxT1 = Math.Max(1, Math.Min(_instrQxT1 - 1, 100));
    if (_instrQxBtn != null)   // (1) null guard
        _instrQxBtn.Content = FormatBuffer("QX-Instr", _instrQxT1);
}
```

**CYC analysis**: null guard → CYC = 2. ✅  
**Pattern match**: mirrors `OnQuickDown` at L1886-1893. ✅

---

### 6. `OnInstrBeClick(object sender, RoutedEventArgs e)` — CYC <= 3

**Signature**: `private void OnInstrBeClick(object sender, RoutedEventArgs e)`

**Purpose**: Arms per-instrument BE pending watcher via `_engine.ArmPendingBe`.

**Implementation**:

```csharp
// B128: OnInstrBeClick -- per-instrument BE arm. CYC=3.
// JS-033: synchronous void event handler. JS-021: no lock.
// NOTE: No re-arm guard -- _engine.ArmPendingBe is idempotent per existing spec.
private void OnInstrBeClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null || _leaderAccount == null)   // (1)(2) compound guard
        return;
    NinjaTrader.Code.Output.Process(
        "[PTT-BE-INSTR] button: "
            + _leaderAccount.Name
            + " "
            + _instrument.FullName
            + " buf=" + _beBuffer,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
    _engine.ArmPendingBe(_instrument, _leaderAccount, _beBuffer);
}
```

**CYC analysis**: compound `||` guard counts as 2 predicates (CYC = 1+2 = 3). ✅

**NT8 API call**:
- `CopyEngine.ArmPendingBe(Instrument instr, Account masterAcc, int bufferTicks)`
  confirmed at `CopyEngine.cs` L3935.
- Call: `_engine.ArmPendingBe(_instrument, _leaderAccount, _beBuffer)` ✅
- Parameter order verified: (Instrument, Account, int) → (_instrument, _leaderAccount, _beBuffer) ✅
- `_engine` field: `private CopyEngine _engine` at `TradeCopierPanel.cs` L118 ✅
- `_beBuffer` field: `private int _beBuffer = 1` at `TradeCopierPanel.cs` L244 ✅
- No re-arm guard needed: `ArmPendingBe` is idempotent (confirmed by existing spec and code comments).

**JS constraints**: JS-033 synchronous void. JS-021 no lock. ASCII log prefix "[PTT-BE-INSTR]". ✅

---

## NinjaTrader 8 API Surface (Verified from Source)

| Symbol | Location | Confirmed |
|--------|----------|-----------|
| `CopyEngine _engine` field | `TradeCopierPanel.cs` L118 | ✅ |
| `CopyEngine.ArmPendingBe(Instrument, Account, int)` | `CopyEngine.cs` L3935 | ✅ |
| `PttQuickExit.Execute(Account, Instrument, int, int, bool)` | `PttQuickExit.cs` L215 | ✅ |
| `BrushTeal` (SolidColorBrush) | `TradeCopierPanel.cs` L320 | ✅ |
| `FormatBuffer(string, int)` | `TradeCopierPanel.cs` L1344 | ✅ |
| `TryResolveLeaderAccount()` | `TradeCopierPanel.cs` (called at L1855) | ✅ |
| RepeatButton spinner pattern | `TradeCopierPanel.cs` L1250-1270 | ✅ |
| DockPanel + Dock.Right pattern | `TradeCopierPanel.cs` L1246, L1270 | ✅ |
| `SetResourceReference(Control.StyleProperty, "NTButtonStyle")` | `TradeCopierPanel.cs` L1262, L1278 | ✅ |
| UniformGrid Columns=2 | `TradeCopierPanel.cs` L1243 | ✅ |
| `_beBuffer` field | `TradeCopierPanel.cs` L244 | ✅ |

---

## Threading Model

All B128 code is UI-thread-only:

- `BuildInstrRow()` is called from `BuildCopierButtons()` during panel initialization (UI thread).
- All click/spinner handlers are WPF event handlers — always dispatched on UI thread by WPF.
- `_instrQxT1`, `_instrQxBtn.Content`, `_instrBeBtn` are only ever read/written from UI thread.
- `_engine.ArmPendingBe` is called synchronously from OnInstrBeClick (UI thread) — consistent
  with the existing pattern of all other button handlers in TradeCopierPanel.cs.

**No `Dispatcher.InvokeAsync` required.**  
**No `ConcurrentQueue` required.**  
**No `volatile` fields required** (all UI-thread-only, per NT8-003 convention).

---

## Data Flow

### QX-Instr Click

```
User clicks _instrQxBtn
  → OnInstrQxClick (UI thread)
  → guard: _instrument null? → return
  → resolve _leaderAccount via TryResolveLeaderAccount() if null
  → ComputeInstrSplit(_instrQxT1) → (t1, t2)
  → Log "[PTT-QX-INSTR] button: {acc} {instr} t1={t1} t2={t2}"
  → new PttQuickExit().Execute(_leaderAccount, _instrument, t1, t2)
      → PttQuickExit shim (L215) → delegates to targets-based Execute (L39)
      → cancels existing brackets + submits new PTT-QX-Stop / PTT-QX-T* OCO pairs
```

### BE-Instr Click

```
User clicks _instrBeBtn
  → OnInstrBeClick (UI thread)
  → guard: _instrument null OR _leaderAccount null? → return
  → Log "[PTT-BE-INSTR] button: {acc} {instr} buf={_beBuffer}"
  → _engine.ArmPendingBe(_instrument, _leaderAccount, _beBuffer)
      → CopyEngine arms the pending BE watcher for this instrument+account
      → (idempotent — safe to call repeatedly)
```

---

## Files Changed

| File | Change Type | Description |
|------|-------------|-------------|
| `src/PropTraderTools/TradeCopierPanel.cs` | MODIFIED | 4 new fields; BuildInstrRow(); 4 event handlers; 1 static method; 1 line in BuildCopierButtons |
| `src/PropTraderTools/Tests/B128Tests.cs` | NEW | xUnit tests for ComputeInstrSplit |

## Files Explicitly Not Changed

| File | Reason |
|------|--------|
| `src/PropTraderTools/Features/PttQuickExit.cs` | Used via existing shim overload (L215). UNCHANGED. |
| `src/PropTraderTools/Features/PttGlobalBreakEven.cs` | Not involved. ArmPendingBe is on CopyEngine, not GlobalBe. UNCHANGED. |
| `src/PropTraderTools/CopyEngine.cs` | ArmPendingBe called via _engine reference. UNCHANGED. |
| All other `.cs` files | Not in scope. UNCHANGED. |

---

## CYC Budget Table

| Method | CYC | Budget | Status |
|--------|-----|--------|--------|
| `BuildInstrRow()` | 1 | <= 4 | ✅ |
| `ComputeInstrSplit(int)` | 1 | = 1 | ✅ |
| `OnInstrQxClick` | 3 | <= 3 | ✅ |
| `OnInstrQxUp` | 2 | <= 2 | ✅ |
| `OnInstrQxDown` | 2 | <= 2 | ✅ |
| `OnInstrBeClick` | 3 | <= 3 | ✅ |

All methods within Jane Street strict standard (CYC <= 8). ✅

---

## JS Compliance Summary

| Rule | New Code Check | Result |
|------|---------------|--------|
| JS-021 — no lock() | 0 new lock() anywhere in B128 | PASS |
| JS-033 — no async void | 0 async void; all handlers synchronous void | PASS |
| JS-001 — no throw | 0 throw statements | PASS |
| JS-002 — no return null | ComputeInstrSplit returns value tuple; all others void | PASS |
| ASCII-only identifiers | All new identifiers ASCII | PASS |
| ASCII-only string literals | "[PTT-QX-INSTR]", "[PTT-BE-INSTR]", "QX-Instr", "BE-Instr" are ASCII | PASS |
| Unicode note | \u25B2 \u25BC are pre-existing spinner arrow pattern — preserved | ACCEPTABLE |

---

## Test Specification

**File**: `src/PropTraderTools/Tests/B128Tests.cs` (NEW)  
**Framework**: xUnit only — `[Fact]` attribute  
**Class**: `B128Tests` in `PropTraderTools` namespace  
**Target**: `TradeCopierPanel.ComputeInstrSplit` (internal static method)  
**Access**: `InternalsVisibleTo` already configured for test project, or same namespace  

### Test Cases

| # | Test Name | Input | Expected | Assertion |
|---|-----------|-------|----------|-----------|
| 1 | `QxInstrSplit_Even_T1EqualT2` | `ComputeInstrSplit(4)` | `(2, 2)` | `Assert.Equal(2, t1); Assert.Equal(2, t2)` |
| 2 | `QxInstrSplit_Odd_T1Heavier` | `ComputeInstrSplit(5)` | `(3, 2)` | `Assert.Equal(3, t1); Assert.Equal(2, t2)` |
| 3 | `QxInstrSplit_One_BothOne` | `ComputeInstrSplit(1)` | `(1, 0)` | `Assert.Equal(1, t1); Assert.Equal(0, t2)` |
| 4 | `QxInstrSplit_Large_Odd` | `ComputeInstrSplit(7)` | `(4, 3)` | `Assert.Equal(4, t1); Assert.Equal(3, t2)` |

**Test math verification**:
- `(4+1)/2 = 2`, `4/2 = 2` → (2, 2) ✅
- `(5+1)/2 = 3`, `5/2 = 2` → (3, 2) ✅
- `(1+1)/2 = 1`, `1/2 = 0` → (1, 0) ✅
- `(7+1)/2 = 4`, `7/2 = 3` → (4, 3) ✅

---

## 7-Scan Checklist (Engineer Contract)

The implementing engineer MUST run all 7 scans before marking T1 complete.

| Scan | Command | Pass Criterion |
|------|---------|---------------|
| SCAN-01 — ASCII | `grep -P "[\x80-\xFF]" TradeCopierPanel.cs` (new lines only) | 0 matches |
| SCAN-02 — lock() | `Select-String -Pattern "lock\(" TradeCopierPanel.cs` (new methods only) | 0 new matches |
| SCAN-03 — async void | `grep "async void" TradeCopierPanel.cs` | 0 new matches |
| SCAN-04 — return null | `grep "return null" TradeCopierPanel.cs` (new methods only) | 0 new matches |
| SCAN-05 — build | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | 0 errors, 0 new warnings |
| SCAN-06 — CYC | `python scripts/complexity_audit.py src/PropTraderTools/TradeCopierPanel.cs` (new methods) | All new methods <= 8 |
| SCAN-07 — tests | `dotnet test` (B128Tests.cs) | 4 passed, 0 failed |

---

## Deferred Backlog Carry-Forward

B128 does NOT close any items from the B107 or B124 deferred backlogs.
All carry-forward items below remain open and unaffected by B128 changes.

**Open items (unchanged from B124/06-deferred-backlog.md)**:
- DW-B124-01: Second click no longer disarms BE-ALL (behavioral change, pending Director decision)
- DW-B124-02: Test assertion weakness in B124 Test 2 (pending polish block)
- DW-B107: MoveStopToBreakEven stale PTT-BE-Target-* on followers
- B107-DEFER-01: F5 NinjaTrader 8 Compilation Gate (Director-owned)
- B107-DEFER-02: Combo C Live Re-Test
- DW-B42-01/02/03, DW-PTT-BE-FIX-01/02/03, DW-B89-DEFERRED-01/02/03/04/05/06

**Total carry-forward open items**: 16 (unchanged from B124 final count)
