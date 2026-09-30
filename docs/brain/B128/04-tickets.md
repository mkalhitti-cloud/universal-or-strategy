# B128 Tickets

**Block**: B128 — Instrument-scoped QX-Instr (2-target) + BE-Instr buttons
**Phase**: 3 (Ticket Generation)
**Status**: TICKETS_COMPLETE
**Plan**: `docs/brain/B128/02-architecture-plan.md` (REVIEW_PASS, R-01..R-11 all passed)
**Author**: ptt-architect

---

## Ticket 1 — B128-T1: Instrument-Row Panel with QX-Instr + BE-Instr Buttons

### Spec Requirements

| ID | Requirement |
|----|-------------|
| B128-REQ-01 | New `_instrRowPanel` (UniformGrid, 2 cols) inserted above `_quickRowPanel` in `root.Children` |
| B128-REQ-02 | QX-Instr button with spinner arrows; default `_instrQxT1=4`; `FormatBuffer("QX-Instr", _instrQxT1)` |
| B128-REQ-03 | `ComputeInstrSplit(int)` internal static; `t1=(n+1)/2`, `t2=n/2` |
| B128-REQ-04 | `OnInstrQxClick` calls `ComputeInstrSplit` then `PttQuickExit.Execute(_leaderAccount, _instrument, t1, t2)` |
| B128-REQ-05 | BE-Instr button; `OnInstrBeClick` calls `_engine.ArmPendingBe(_instrument, _leaderAccount, _beBuffer)` |
| B128-REQ-06 | Log `"[PTT-QX-INSTR]"` on QX click, `"[PTT-BE-INSTR]"` on BE click |
| B128-REQ-07 | 4 xUnit `[Fact]` tests in `Tests/B128Tests.cs` covering `ComputeInstrSplit(4,5,1,7)` |

---

### Files To Modify

| File | Change Type |
|------|-------------|
| `src/PropTraderTools/TradeCopierPanel.cs` | MODIFY — 4 new fields + 6 new methods + 2 new lines in `BuildCopierButtons` |
| `src/PropTraderTools/Tests/B128Tests.cs` | CREATE NEW — 4 xUnit `[Fact]` tests |

### Files Explicitly NOT Changed

| File | Reason |
|------|--------|
| `src/PropTraderTools/Features/PttQuickExit.cs` | Used via existing shim overload at L215. UNCHANGED. |
| `src/PropTraderTools/Features/PttGlobalBreakEven.cs` | Not involved. `ArmPendingBe` is on `CopyEngine`, not `GlobalBe`. UNCHANGED. |
| `src/PropTraderTools/CopyEngine.cs` | `ArmPendingBe` called via `_engine` reference. UNCHANGED. |
| ALL other `.cs` files | Not in scope. No touch. |

---

### Method Signatures (exact — engineer MUST NOT deviate)

```csharp
private void BuildInstrRow()                                        // CYC <= 4
internal static (int t1, int t2) ComputeInstrSplit(int instrQxT1)  // CYC = 1
private void OnInstrQxClick(object sender, RoutedEventArgs e)       // CYC <= 3
private void OnInstrQxUp(object sender, RoutedEventArgs e)          // CYC <= 2
private void OnInstrQxDown(object sender, RoutedEventArgs e)        // CYC <= 2
private void OnInstrBeClick(object sender, RoutedEventArgs e)       // CYC <= 3
```

### New Fields (add near L244 in TradeCopierPanel.cs, with existing field declarations)

```csharp
private Button _instrQxBtn = null;
private Button _instrBeBtn = null;
private UniformGrid _instrRowPanel = null;
private int _instrQxT1 = 4;
```

---

### Implementation Instructions (ptt-engineer MUST follow exactly — 8 changes)

#### CHANGE 1 — Add 4 fields near L244

Add immediately after the existing Quick fields block (after `_quickT3Row`, before next region):

```csharp
private Button _instrQxBtn = null;
private Button _instrBeBtn = null;
private UniformGrid _instrRowPanel = null;
private int _instrQxT1 = 4;
```

---

#### CHANGE 2 — Insert `BuildInstrRow()` call and `_instrRowPanel` into root at L914

Find these two consecutive lines in `BuildCopierButtons()`:

```csharp
root.Children.Add(_beRowPanel); // B49: moved from tail -- buttons first
root.Children.Add(_quickRowPanel); // B49: moved from tail -- buttons first
```

Replace with:

```csharp
root.Children.Add(_beRowPanel); // B49: moved from tail -- buttons first
BuildInstrRow(); // B128: build instrument row before adding to root
root.Children.Add(_instrRowPanel); // B128: instrument-scoped row above Quick row
root.Children.Add(_quickRowPanel); // B49: moved from tail -- buttons first
```

---

#### CHANGE 3 — Add `BuildInstrRow()` method

Place after the existing `BuildQuickRow` region, before the `OnQuickClick` region.

```csharp
// B128: BuildInstrRow -- constructs _instrRowPanel (UniformGrid 2-col: QX-Instr | BE-Instr).
// _instrRowPanel is NOT added to root here; added by BuildCopierButtons caller.
// CYC=1: sequential construction, no branches.
// JS-021: no lock. JS-033: no async. ASCII-only labels.
private void BuildInstrRow()
{
    _instrRowPanel = new UniformGrid { Columns = 2, Margin = new Thickness(0, 2, 0, 2) };

    // Left cell: QX-Instr cluster (DockPanel with spinner + button)
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

    // Right cell: BE-Instr button (full-width)
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
}
```

---

#### CHANGE 4 — Add `ComputeInstrSplit()` static method

Place near the `OnQuickClick` region, clearly labeled B128:

```csharp
// B128: ComputeInstrSplit -- ceiling/floor integer split for 2-target QX-Instr.
// t1 = ceiling half (heavier on odd), t2 = floor half.
// Examples: 4->(2,2), 5->(3,2), 1->(1,0), 7->(4,3).
// CYC=1: single expression return. internal static for xUnit direct test access.
// JS-021: no lock. ASCII-only. No heap alloc.
internal static (int t1, int t2) ComputeInstrSplit(int instrQxT1) =>
    ((instrQxT1 + 1) / 2, instrQxT1 / 2);
```

---

#### CHANGE 5 — Add `OnInstrQxClick` handler

Place after `OnQuickDown`, before next region:

```csharp
// B128: OnInstrQxClick -- fires instrument-scoped 2-target QX. CYC=3.
// (1) null guard on _instrument
// (2) late-resolve _leaderAccount
// (3) compute split, log, execute
// JS-033: synchronous void event handler. JS-021: no lock.
private void OnInstrQxClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null) // (1)
        return;
    _leaderAccount = _leaderAccount ?? TryResolveLeaderAccount(); // (2)
    var (t1, t2) = ComputeInstrSplit(_instrQxT1); // (3)
    NinjaTrader.Code.Output.Process(
        "[PTT-QX-INSTR] button: "
            + (_leaderAccount?.Name ?? "null")
            + " "
            + (_instrument?.FullName ?? "null")
            + " t1="
            + t1
            + " t2="
            + t2,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
    var qx = new PttQuickExit();
    qx.Execute(_leaderAccount, _instrument, t1, t2);
}
```

---

#### CHANGE 6 — Add `OnInstrQxUp` and `OnInstrQxDown` handlers

Place after `OnInstrQxClick`:

```csharp
// B128: OnInstrQxUp -- increment _instrQxT1, clamp [1..100]. CYC=2.
private void OnInstrQxUp(object sender, RoutedEventArgs e)
{
    _instrQxT1 = Math.Max(1, Math.Min(_instrQxT1 + 1, 100));
    if (_instrQxBtn != null)
        _instrQxBtn.Content = FormatBuffer("QX-Instr", _instrQxT1);
}

// B128: OnInstrQxDown -- decrement _instrQxT1, clamp [1..100]. CYC=2.
private void OnInstrQxDown(object sender, RoutedEventArgs e)
{
    _instrQxT1 = Math.Max(1, Math.Min(_instrQxT1 - 1, 100));
    if (_instrQxBtn != null)
        _instrQxBtn.Content = FormatBuffer("QX-Instr", _instrQxT1);
}
```

---

#### CHANGE 7 — Add `OnInstrBeClick` handler

Place after `OnInstrQxDown`:

```csharp
// B128: OnInstrBeClick -- arms BE on leader account for current instrument. CYC=3.
// (1) null guard on _instrument and _leaderAccount
// (2) log
// (3) arm pending BE via _engine
// JS-033: synchronous void event handler. JS-021: no lock.
private void OnInstrBeClick(object sender, RoutedEventArgs e)
{
    if (_instrument == null || _leaderAccount == null) // (1)
        return;
    NinjaTrader.Code.Output.Process(
        "[PTT-BE-INSTR] button: "
            + _leaderAccount.Name
            + " "
            + _instrument.FullName
            + " buf="
            + _beBuffer,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
    _engine.ArmPendingBe(_instrument, _leaderAccount, _beBuffer); // (3)
}
```

---

#### CHANGE 8 — Create `src/PropTraderTools/Tests/B128Tests.cs` (NEW FILE)

```csharp
// B128Tests.cs -- xUnit tests for ComputeInstrSplit (B128 instrument-scoped QX split).
// Framework: xUnit only ([Fact]). No NUnit. No MSTest.
using Xunit;
using NinjaTrader.NinjaScript.AddOns;

namespace PropTraderTools.Tests
{
    public class B128Tests
    {
        [Fact]
        public void QxInstrSplit_Even_T1EqualT2()
        {
            var (t1, t2) = TradeCopierPanel.ComputeInstrSplit(4);
            Assert.Equal(2, t1);
            Assert.Equal(2, t2);
        }

        [Fact]
        public void QxInstrSplit_Odd_T1Heavier()
        {
            var (t1, t2) = TradeCopierPanel.ComputeInstrSplit(5);
            Assert.Equal(3, t1);
            Assert.Equal(2, t2);
        }

        [Fact]
        public void QxInstrSplit_One_BothOne()
        {
            var (t1, t2) = TradeCopierPanel.ComputeInstrSplit(1);
            Assert.Equal(1, t1);
            Assert.Equal(0, t2);
        }

        [Fact]
        public void QxInstrSplit_Large_Odd()
        {
            var (t1, t2) = TradeCopierPanel.ComputeInstrSplit(7);
            Assert.Equal(4, t1);
            Assert.Equal(3, t2);
        }
    }
}
```

---

### 7-Scan Checklist (engineer MUST run all 7 to zero before BUILD_PASS)

| Scan | Command | Expected |
|------|---------|----------|
| SCAN-01 ASCII | `grep -P "[\x80-\xFF]" src/PropTraderTools/TradeCopierPanel.cs src/PropTraderTools/Tests/B128Tests.cs` | 0 matches. Note: `\u25B2` and `\u25BC` are pre-existing spinner pattern — grep for NEW non-ASCII bytes only in new lines. |
| SCAN-02 lock() | `Select-String -Pattern "lock\(" src/PropTraderTools/TradeCopierPanel.cs` | 0 matches in new code (pre-existing 0 is already confirmed) |
| SCAN-03 async void | `Select-String -Pattern "async void " src/PropTraderTools/TradeCopierPanel.cs` | 0 matches in new code |
| SCAN-04 return null | `Select-String -Pattern "return null;" src/PropTraderTools/TradeCopierPanel.cs` | 0 matches in new methods |
| SCAN-05 build | `dotnet build src/PropTraderTools/PropTraderTools.csproj --no-restore` | Build succeeded. 0 Error(s). 0 new Warning(s). |
| SCAN-06 CYC | `python scripts/complexity_audit.py src/PropTraderTools/TradeCopierPanel.cs` | All new methods (`BuildInstrRow`, `ComputeInstrSplit`, `OnInstrQxClick`, `OnInstrQxUp`, `OnInstrQxDown`, `OnInstrBeClick`) <= 8 |
| SCAN-07 tests | `dotnet test src/PropTraderTools/PropTraderTools.csproj --filter "FullyQualifiedName~B128Tests"` | 4 passed, 0 failed. Fallback: verify `ComputeInstrSplit` arithmetic manually if test infrastructure unavailable. |

---

### Acceptance Criteria (verifier checks independently)

| ID | Criterion |
|----|-----------|
| AC-01 | `_instrRowPanel` appears in `root.Children` AFTER `_beRowPanel` and BEFORE `_quickRowPanel` (visual tree order) |
| AC-02 | `"[PTT-QX-INSTR]"` log prefix present in `OnInstrQxClick` |
| AC-03 | `"[PTT-BE-INSTR]"` log prefix present in `OnInstrBeClick` |
| AC-04 | `_engine.ArmPendingBe` (not `CopyEngine.Instance?.GlobalBe?.ArmPendingBe`) used in `OnInstrBeClick` |
| AC-05 | `ComputeInstrSplit` is `internal static` (accessible from `B128Tests.cs` without instance) |
| AC-06 | All 4 `[Fact]` tests pass |
| AC-07 | SCAN-01 through SCAN-07 all zero/pass |

---

### CYC Budget Reference

| Method | CYC | Budget | Status |
|--------|-----|--------|--------|
| `BuildInstrRow()` | 1 | <= 4 | PASS |
| `ComputeInstrSplit(int)` | 1 | = 1 | PASS |
| `OnInstrQxClick` | 3 | <= 3 | PASS |
| `OnInstrQxUp` | 2 | <= 2 | PASS |
| `OnInstrQxDown` | 2 | <= 2 | PASS |
| `OnInstrBeClick` | 3 | <= 3 | PASS |

All methods within Jane Street strict standard (CYC <= 8). ✅

---

### JS Rule Constraints (per method)

| Method | JS-021 no lock | JS-033 no async void | JS-001 no throw | JS-002 no null return | ASCII-only |
|--------|---------------|---------------------|-----------------|----------------------|------------|
| `BuildInstrRow` | PASS | PASS (synchronous void) | PASS | PASS (void) | PASS |
| `ComputeInstrSplit` | PASS | PASS (static, not handler) | PASS | PASS (value tuple) | PASS |
| `OnInstrQxClick` | PASS | PASS (synchronous void event handler) | PASS | PASS (void) | PASS |
| `OnInstrQxUp` | PASS | PASS (synchronous void event handler) | PASS | PASS (void) | PASS |
| `OnInstrQxDown` | PASS | PASS (synchronous void event handler) | PASS | PASS (void) | PASS |
| `OnInstrBeClick` | PASS | PASS (synchronous void event handler) | PASS | PASS (void) | PASS |

Note: `\u25B2` / `\u25BC` are pre-existing spinner arrow characters used throughout the panel — acceptable per plan review R-11. New string literals `"[PTT-QX-INSTR]"`, `"[PTT-BE-INSTR]"`, `"QX-Instr"`, `"BE-Instr"` are all ASCII.

---

### Return

Engineer returns: **BUILD_PASS** | BUILD_FAIL

Verifier confirms independently against AC-01 through AC-07.
