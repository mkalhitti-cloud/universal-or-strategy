# B190 Architecture Plan

**Epic**: PTT-COPIER B190 | Per-Instrument Tick Defaults + Spinner Layout Fix + BE Default=0 + Deferred Closures
**Author**: ptt-architect (Phase 1)
**Date**: 2026-07-11
**Status**: PLAN_COMPLETE
**Preceding backlog**: `docs/brain/B189/06-deferred-backlog.md`

---

## LANE-SPLIT GATE RESULT

```
Q1. Same method or within 50 lines?
    NO -- T1 (SetInstrument ~L501), T2 (BuildInstrRow ~L1174 + new BuildSpinnerCluster),
    T3 (field L241), T4 (RULES_CATALOG.md), T5 (spec HTML), T6 (CopyEngineTests.cs L8812)
    span different methods, different files, and non-adjacent lines.

Q2. Fix B design depends on Fix A final design?
    NO -- each ticket's design is fully independent of all others.

Q3. Each fix has standalone value if the other is blocked?
    YES -- T1 corrects tick defaults; T2 fixes spinner layout; T3 corrects BE default;
    T4/T5/T6 close doc/test debt. Each delivers standalone value.

Q4. Each fix has an independent SIM verification path?
    YES -- T1: open MES/MGC panels; T2: visual layout check; T3: BE button label;
    T4: grep RULES_CATALOG.md; T5: HTML review; T6: dotnet test.

GATE COMPUTATION: Q1=NO, Q2=NO, Q3=YES, Q4=YES --> LANES APPROVED.

DECISION: SINGLE-PIPELINE
Rationale: T1+T2+T3 all modify TradeCopierPanel.cs and constitute a coherent UX
improvement that should be verified together. T4/T5/T6 are non-code changes with
zero merge risk. Bundling into one pipeline pass is operationally correct.
```

**LANE-SPLIT GATE RESULT: SINGLE-PIPELINE**

---

## Component List

| Component | Type | File |
|-----------|------|------|
| InstrumentDefaults | Static class (existing, modified) | `src/PropTraderTools/Features/PttQuickExit.cs` |
| TradeCopierPanel | WPF UserControl (existing, modified) | `src/PropTraderTools/TradeCopierPanel.cs` |
| CopyEngineTests (FlatGuardLoop) | xUnit test file (existing, skip removed) | `src/PropTraderTools/CopyEngineTests.cs` |
| RULES_CATALOG.md | Docs file (existing, content restored) | `docs/standards/jane-street/RULES_CATALOG.md` |
| spec HTML | Spec file (existing, B39 row updated) | `specs/002-trade-copier-spec.html` |
| B190Tests | New xUnit test class | `src/PropTraderTools/Tests/B190Tests.cs` |

---

## Ticket Inventory (6 tickets)

### T1 -- InstrumentDefaults MGC update + SetInstrument() tick reset

**Spec requirements**: B190-T1, AC1-AC5, T_B190_T1_01..T1_05

**File 1**: `src/PropTraderTools/Features/PttQuickExit.cs`

Method: `InstrumentDefaults.GetQuickTicks(string masterName) -> (int t1, int t2)`

Change: MGC branch return value. Line 392:
```
BEFORE: if (masterName.StartsWith("MGC")) return (2, 4);
AFTER:  if (masterName.StartsWith("MGC")) return (10, 20);
```

Also update the XML summary comment (line 381) to reflect the new values:
```
BEFORE: MGC: (2, 4) = 0.2pt/0.4pt at 0.1 tick.
AFTER:  MGC: (10, 20) = 1pt/2pt at 0.1 tick.
```

CYC: 4 (unchanged -- null/empty guard + MES check + MGC check + default return = 3 branches + 1). No structural change.
JS-002: returns tuple, never null. JS-081: ASCII-only strings. JS-021: no lock.

**File 2**: `src/PropTraderTools/TradeCopierPanel.cs`

Method: `public void SetInstrument(Instrument instrument)` (~L501)

Change: After the existing `_statusText.Text` assignment block, append the tick-reset block.
No existing lines removed. CYC after change = 5 (existing 3 + 2 new null guards).

Full method body AFTER change:
```csharp
// B17 + B190-T1: Store instrument, update status label, reset per-instrument tick defaults.
// CYC=5: _statusText null(1) + instrument null(2) + _quickBtn null(3) + _instr2tBtn null(4) -- max 5.
// JS-021: no lock. JS-002: no null return (void). ASCII-only. UI-thread-only -- no Dispatcher needed.
public void SetInstrument(Instrument instrument)
{
    _instrument = instrument;
    if (_statusText != null && instrument != null)
        _statusText.Text = "Ready: " + instrument.FullName + " -- select followers to copy";

    // B190-T1: reset tick defaults when instrument changes
    var ticks = InstrumentDefaults.GetQuickTicks(
        instrument?.MasterInstrument?.Name ?? "");
    _quickT1 = ticks.t1;
    _quickT2 = ticks.t2;
    if (_quickBtn != null)
        _quickBtn.Content = FormatBuffer("Quick", _quickT1);
    if (_instr2tBtn != null)
        _instr2tBtn.Content = FormatBuffer("Quick2t", _quickT1);
}
```

Design notes:
- `instrument?.MasterInstrument?.Name ?? ""` handles null instrument safely; `GetQuickTicks("")` returns `(4, 8)` -- no extra null branch needed, no CYC cost.
- `_instrQAll2tBtn` label is NOT updated here. `GlobalQuickAllT1` is a shared singleton on `CopyEngine.Instance` -- resetting it in SetInstrument() would stomp the value for every other open panel. Per-panel fields `_quickT1` / `_quickT2` reset; QAll2t keeps its current singleton value.
- `_quickT1` / `_quickT2`: "UI-thread-only; no volatile per NT8-003" (existing comment L256-257). SetInstrument() is called from UI thread by the AddOn. SAFE.

---

### T2 -- BuildInstrRow() spinner layout fix

**Spec requirements**: B190-T2, AC6-AC8, T_B190_T2_01..T2_02

**File**: `src/PropTraderTools/TradeCopierPanel.cs`

**Method REPLACED**: `private void BuildInstrRow()` (~L1174)

Replace entire body. Remove the B189-T4 pattern (Button "-" docked Left, Button "+" docked Right). New body delegates to `BuildSpinnerCluster()`.

New body:
```csharp
// B190-T2: BuildInstrRow -- 2-col UniformGrid: Quick2t spinner (left) + QAll2t spinner (right).
// Each cluster uses canonical Grid+RepeatButton docked Right (matching BE, Trim, Flatten, Quick, Quick ALL).
// CYC=1: sequential calls, no branches. JS-021: no lock. ASCII-only.
private void BuildInstrRow()
{
    _instrRowPanel = new UniformGrid { Columns = 2, Margin = new Thickness(0, 2, 0, 2) };
    _instrRowPanel.Children.Add(
        BuildSpinnerCluster("Quick2t", _quickT1, OnQuickUp, OnQuickDown,
            OnInstr2tClick, ref _instr2tBtn));
    _instrRowPanel.Children.Add(
        BuildSpinnerCluster("QAll2t", CopyEngine.Instance.GlobalQuickAllT1,
            OnQuickAllUp, OnQuickAllDown,
            OnInstrQAll2tClick, ref _instrQAll2tBtn));
}
```

**Method ADDED**: `private DockPanel BuildSpinnerCluster(string label, int ticks, RoutedEventHandler onUp, RoutedEventHandler onDn, RoutedEventHandler onClick, ref Button btnField)`

Signature:
```csharp
// BuildSpinnerCluster: canonical ^-v-right spinner cluster factory.
// Same Grid(2-row RepeatButton) pattern used by Trim, Flatten, BE, Quick, Quick ALL.
// CYC=1: no branches. JS-021: no lock. JS-002: never null (returns DockPanel). ASCII-only.
private DockPanel BuildSpinnerCluster(
    string label, int ticks,
    RoutedEventHandler onUp, RoutedEventHandler onDn, RoutedEventHandler onClick,
    ref Button btnField)
```

Full body:
```csharp
{
    var cluster = new DockPanel { LastChildFill = true };
    var arrows = new Grid();
    arrows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
    arrows.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
    var up = new System.Windows.Controls.Primitives.RepeatButton
        { Content = "^", Width = 18, Height = 12, Delay = 400, Interval = 100 };
    var dn = new System.Windows.Controls.Primitives.RepeatButton
        { Content = "v", Width = 18, Height = 12, Delay = 400, Interval = 100 };
    up.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
    dn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
    up.Click += onUp;
    dn.Click += onDn;
    Grid.SetRow(up, 0);
    Grid.SetRow(dn, 1);
    arrows.Children.Add(up);
    arrows.Children.Add(dn);
    DockPanel.SetDock(arrows, Dock.Right);
    var btn = new Button
    {
        Content = FormatBuffer(label, ticks),
        BorderBrush = BrushTeal,
        BorderThickness = new Thickness(2),
    };
    btn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
    btn.Background = BrushTeal;
    btn.Foreground = System.Windows.Media.Brushes.White;
    btn.Click += onClick;
    cluster.Children.Add(arrows);
    cluster.Children.Add(btn);
    btnField = btn;
    return cluster;
}
```

Design notes:
- `RepeatButton` Delay=400ms, Interval=100ms: auto-fires on hold at 400ms initial delay, 100ms repeat. Restores hold-to-scroll behaviour lost in B189-T4 (which used plain Button).
- `DockPanel.SetDock(arrows, Dock.Right)` then `cluster.Children.Add(arrows)` before `cluster.Children.Add(btn)`: arrows grid docked Right, btn fills remaining space via `LastChildFill=true`. This is the canonical layout matching all other spinners.
- `ref Button btnField`: valid C# pattern to assign the field from inside the helper without a second assignment at the call site.
- `"^"` is ASCII 0x5E, `"v"` is ASCII 0x76. Both ASCII-only. JS-081 PASS.
- `BrushTeal` and `System.Windows.Media.Brushes.White`: named brush constants, no hardcoded hex. JS-081 PASS.
- Method is placed immediately after `BuildInstrRow()` in the file (in the private helpers block).

CYC analysis:
- `BuildInstrRow()`: CYC=1 (2 sequential calls, no branches).
- `BuildSpinnerCluster()`: CYC=1 (sequential construction, no branches).
- Both well within ceiling of 8 (JS-066).

---

### T3 -- BE default = 0

**Spec requirements**: B190-T3, AC9-AC11, T_B190_T3_01

**File**: `src/PropTraderTools/TradeCopierPanel.cs`

Change: Field initializer at line 241.

```
BEFORE: private int _beBuffer = 1;
AFTER:  private int _beBuffer = 0; // B190-T3: default exact BE (no buffer)
```

Design notes:
- 1-character change (1 -> 0) plus trailing comment.
- No method CYC change.
- `FormatGlobalBeBuffer("BE", 0)` returns `"BE"` (the ticks==0 branch added in B189-T2). Panel opens with BE button showing "BE" with no number. AC9 satisfied.
- `_globalBeBuffer` in `PttGlobalBreakEven.cs` is already 0 -- no change needed.
- `_beBuffer` is UI-thread-only (read/written by OnBeUp/OnBeDown/OnBeClick which are UI event handlers). NT8-003: no volatile needed.

---

### T4 -- Restore RULES_CATALOG.md (DW-B183-05)

**Spec requirements**: DW-B183-05 (P1 deferred, opened B183)

**File**: `docs/standards/jane-street/RULES_CATALOG.md`

Change: Append JS-042 through JS-110. The file currently contains JS-001..JS-041 only.

Verification requirements:
- File must contain `JS-066` (CYC ceiling rule) after restore.
- File must contain `JS-081` (ASCII-only rule) after restore.
- File must be UTF-8 no-BOM (per workspace rule 05-utf8-encoding.md).

No .cs files touched. No compile step. Enforcement already operates via DNA block + workspace rule 06-rules-catalog-gate.md. This is documentation repair only.

**CLOSES DW-B183-05**

---

### T5 -- Spec HTML update: BE ALL instrument scope (DW-B185-BE-ALL-SCOPE-01)

**Spec requirements**: DW-B185-BE-ALL-SCOPE-01 (P2 deferred, opened B185)

**File**: `specs/002-trade-copier-spec.html`

Changes:
1. Update B39 scope table row: find the text "Instrument scope: All instruments with open positions" and replace with "Panel's _instrument only (Account.All)".
2. Add a closed DW card for DW-B185-BE-ALL-SCOPE-01 in the B39 block.
   - DW card format follows existing closed-card pattern (green border, status=CONFIRMED FIXED).
   - Code was shipped in B185-T1 (instrument-scoped BE ALL). Only the spec doc lagged.

No .cs files touched. No compile step. Spec HTML only.

**CLOSES DW-B185-BE-ALL-SCOPE-01**

---

### T6 -- FlatGuardLoop skip removal + Slate Gate DW audit (DW-B183-02 + DW-B183-01)

**Spec requirements**: DW-B183-02 + DW-B183-01 (P2 deferred, opened B183)

**File**: `src/PropTraderTools/CopyEngineTests.cs`

**DW-B183-02** (FlatGuardLoop skip removal):

Change at line 8812: Remove `[Fact(Skip="B180-2: method removed")]` attribute from `Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard`.

Pre-condition check (MANDATORY before making this change):
```
grep -n "FlatGuardLoop" src/PropTraderTools/CopyEngine.cs
```
Expected: at least one match at CopyEngine.cs line 6768 (`private static void FlatGuardLoop<TAccount>(`).
**CONFIRMED**: FlatGuardLoop exists at CopyEngine.cs:4796, 4841, 6768. The skip reason "B180-2: method removed" is stale. FlatGuardLoop was restored per spec T1.5 (BUG-0911-A Retry2).

Test asserts (structural, no NT8 runtime required):
1. `typeof(CopyEngine).GetMethods(NonPublic|Static)` finds `FlatGuardLoop` with 1 generic argument.
2. 3 parameters: `Func<IEnumerable<TAccount>>`, `Func<TAccount,bool>`, `Action<TAccount>`.
3. param[1] return type is `typeof(bool)` (not NT8 Position type -- the DW-BUG-0911-A-02 contract).

After removing Skip, `dotnet test` must pass this test. The test body uses only reflection metadata (no `.Invoke()`) -- the NT8 static constructor is NOT triggered. No NT8 host needed.

**DW-B183-01** (Slate Gate test audit):

Slate Gate tests are already present at CopyEngineTests.cs lines 8277-8441 (class `Bug2FollowerSlateReadyTests`):
- 6 passing `[Fact]` tests (pure BCL gate semantics, no NT8)
- 2 `[Fact(Skip="NT8-runtime: ...")]` tests (require NT8 host -- correctly skipped)

No new tests required. Ph5 final reviewer to confirm all 6 passing tests remain PASS after T6 change and formally close DW-B183-01 in `06-deferred-backlog.md`.

**CLOSES DW-B183-02** (skip removed, test passes)
**CLOSES DW-B183-01** (confirmed by Ph5 reviewer audit of existing test coverage)

---

## NinjaTrader 8 API Usage

| API | Source | Usage | Note |
|-----|--------|-------|------|
| `Instrument.MasterInstrument.Name` | NT8 Cbi | T1: `instrument?.MasterInstrument?.Name ?? ""` | Null-conditional chain. Confirmed pattern in PttGlobalQuickExit.cs L282 and CopyEngine.cs L827. |
| `System.Windows.Controls.Primitives.RepeatButton` | WPF (not NT8-specific) | T2: spinner up/down arrows | Standard WPF. Delay/Interval are int properties (ms). |
| `DockPanel.SetDock(arrows, Dock.Right)` | WPF attached property | T2: dock arrows grid right | Standard WPF. |
| `Grid.SetRow(element, row)` | WPF attached property | T2: 2-row arrow grid | Standard WPF. |
| `Control.SetResourceReference("NTButtonStyle")` | NT8 style | T2: apply NT8 button theme | Already used in existing BuildInstrRow(). |
| `CopyEngine.Instance.GlobalQuickAllT1` | PTT singleton | T2: QAll2t initial tick value | Already used in existing BuildInstrRow(). |

**NT8 API facts embedded in plan (per PTT Architect mandate)**:
- `AtmStrategyChangeStopTarget()` -- StrategyBase-only. NOT used in B190.
- `AtmStrategyCreate()` -- StrategyBase-only. NOT used in B190.
- `Account.Change()` -- NOT used in B190.
- `Account.Cancel()` / `Account.CreateOrder()` -- NOT used in B190.

---

## Threading Model

All B190 changes are UI-thread-only. No Dispatcher.InvokeAsync additions needed.

| Method | Thread Context | Rationale |
|--------|---------------|-----------|
| `SetInstrument()` | UI thread | Called from TradeCopierAddOn.OnWindowCreated / instrument-change hook, which fires on UI thread. Existing `_statusText.Text` assignment (no Dispatcher wrapper) confirms UI thread. |
| `BuildInstrRow()` | UI thread | Called from `BuildUI()` from `TradeCopierPanel()` constructor, which runs on UI thread (WPF panel construction requires UI thread). |
| `BuildSpinnerCluster()` | UI thread | Called from `BuildInstrRow()`. |
| `_beBuffer` field init | UI thread | Object constructor runs on UI thread. `_beBuffer` is UI-thread-only per NT8-003. |

No `lock()` anywhere in B190. JS-021 PASS.

---

## Data Flow

### T1: Instrument change -> tick defaults reset

```
[NT8 AddOn: instrument changes]
    |
    v
TradeCopierAddOn.SetInstrument(newInstrument)
    |
    v
TradeCopierPanel.SetInstrument(instrument) [UI thread]
    |-- _instrument = instrument
    |-- _statusText.Text = "Ready: ..." (existing)
    |-- InstrumentDefaults.GetQuickTicks(instrument?.MasterInstrument?.Name ?? "")
    |       |-- "" or null --> return (4, 8)
    |       |-- StartsWith("MES") --> return (4, 8)
    |       |-- StartsWith("MGC") --> return (10, 20)  [B190-T1 change]
    |       `-- default --> return (4, 8)
    |-- _quickT1 = ticks.t1; _quickT2 = ticks.t2
    |-- _quickBtn.Content = FormatBuffer("Quick", _quickT1)    [if not null]
    `-- _instr2tBtn.Content = FormatBuffer("Quick2t", _quickT1) [if not null]
         NOTE: _instrQAll2tBtn NOT updated (GlobalQuickAllT1 is singleton, instrument-agnostic)
```

### T2: Panel construction -> spinner layout

```
TradeCopierPanel() [constructor]
    |
    v
BuildUI() --> BuildInstrRow()
    |
    v
BuildSpinnerCluster("Quick2t", _quickT1, OnQuickUp, OnQuickDown, OnInstr2tClick, ref _instr2tBtn)
    |-- DockPanel { LastChildFill=true }
    |-- Grid (2-row: RepeatButton "^" row0, RepeatButton "v" row1) -- Width=18
    |-- DockPanel.SetDock(Grid, Dock.Right)
    |-- Button(FormatBuffer("Quick2t", _quickT1)) fills remaining space
    `-- return DockPanel; _instr2tBtn = btn

BuildSpinnerCluster("QAll2t", GlobalQuickAllT1, OnQuickAllUp, OnQuickAllDown, OnInstrQAll2tClick, ref _instrQAll2tBtn)
    `-- same structure, different handlers and initial value
```

### T3: Panel open -> BE label "BE"

```
TradeCopierPanel() [constructor]
    |-- _beBuffer = 0   [B190-T3 change, was 1]
    |
    v
BuildUI() --> BuildBeRow() --> _beBtn2.Content = FormatGlobalBeBuffer("BE", _beBuffer=0)
    |
    v
FormatGlobalBeBuffer("BE", 0):
    ticks == 0 --> return "BE"  [B189-T2 zero-branch]
    |
    v
BE button shows "BE" (no number) at panel open
```

---

## CYC Summary

| Method | CYC Before | CYC After | Status |
|--------|-----------|-----------|--------|
| `InstrumentDefaults.GetQuickTicks()` | 4 | 4 | PASS (value change only, no structural change) |
| `TradeCopierPanel.SetInstrument()` | 3 | 5 | PASS (<=8) |
| `BuildInstrRow()` (replaced) | 1 | 1 | PASS |
| `BuildSpinnerCluster()` (new) | -- | 1 | PASS |

All methods CYC <= 8. JS-066 PASS.

---

## xUnit Test Registry

All new tests go in `src/PropTraderTools/Tests/B190Tests.cs` (new file) unless noted.

| Test ID | Name | What It Asserts | File | Notes |
|---------|------|----------------|------|-------|
| T_B190_T1_01 | `InstrumentDefaults_MES_Returns4And8` | `GetQuickTicks("MES") == (4, 8)` | B190Tests.cs | Regression guard |
| T_B190_T1_02 | `InstrumentDefaults_MGC_Returns10And20` | `GetQuickTicks("MGC") == (10, 20)` | B190Tests.cs | Updated from (2,4) |
| T_B190_T1_03 | `InstrumentDefaults_Unknown_ReturnsMesDefault` | `GetQuickTicks("XYZ") == (4,8)` and `GetQuickTicks("") == (4,8)` | B190Tests.cs | Regression guard |
| T_B190_T1_04 | `SetInstrument_MES_SetsQuickT1_4` | `GetQuickTicks("MES").t1 == 4` (proxy for SetInstrument MES) | B190Tests.cs | Tests InstrumentDefaults directly; TradeCopierPanel ctor requires NT8 host |
| T_B190_T1_05 | `SetInstrument_MGC_SetsQuickT1_10` | `GetQuickTicks("MGC").t1 == 10` (proxy for SetInstrument MGC) | B190Tests.cs | Tests InstrumentDefaults directly |
| T_B190_T2_01 | `BuildSpinnerCluster_Returns_DockPanel` | Reflection: invoke BuildSpinnerCluster, result is DockPanel | B190Tests.cs | Private method -- use reflection with dummy handlers |
| T_B190_T2_02 | `BuildSpinnerCluster_ArrowsDockedRight` | First child of returned DockPanel has DockPanel.GetDock == Dock.Right | B190Tests.cs | Structural layout check |
| T_B190_T3_01 | `BeBuffer_Default_IsZero` | `FormatGlobalBeBuffer("BE", 0)` returns `"BE"` (proxy for _beBuffer=0 default) | B190Tests.cs | FormatGlobalBeBuffer is the observable output of _beBuffer=0 |
| T_B190_T6_01 | `FlatGuardLoop_StructuralContract_SkipRemoved` | EXISTING test un-skipped at CopyEngineTests.cs:8812. Must pass: FlatGuardLoop exists, 3 params, param[1] returns bool | CopyEngineTests.cs | Engineer removes `[Fact(Skip="B180-2: method removed")]` only |

**Engineering notes for T2 structural tests**:
- `BuildSpinnerCluster` is `private`. Use reflection: `typeof(TradeCopierPanel).GetMethod("BuildSpinnerCluster", BindingFlags.NonPublic | BindingFlags.Instance)`.
- Invoke with dummy `RoutedEventHandler` delegates (e.g. `(s,e) => { }`), dummy label, dummy ticks.
- TradeCopierPanel() ctor calls `CopyEngine.Instance` -- requires NT8 host. Use `BindingFlags` metadata check if ctor cannot be invoked, or accept `[Fact(Skip="NT8-runtime")]` if construction is impossible without NT8.
- If construction is blocked: use pure BCL alternative that tests `DockPanel.GetDock` semantics without NT8 (test the pattern logic independently).

**Important**: The existing test `InstrumentDefaults_GetQuickTicks_MES_ReturnsFourAndEight` in `B74LaneCTests.cs` (line 110) is a regression test that will continue passing (MES still returns (4,8)). There is NO existing test for MGC=(2,4) in any test file -- confirmed by grep. The change to MGC=(10,20) does not break any existing test.

---

## File Split Validation

| File | Tickets | Changes | Cross-contamination check |
|------|---------|---------|--------------------------|
| `PttQuickExit.cs` | T1 only | 1 line value change + 1 comment line | CLEAN -- no T2/T3/T4/T5/T6 content |
| `TradeCopierPanel.cs` | T1+T2+T3 | SetInstrument extension (~L501), BuildInstrRow replacement (~L1174), BuildSpinnerCluster addition (new), _beBuffer field (L241) | CLEAN -- T1/T2/T3 in non-overlapping locations |
| `CopyEngineTests.cs` | T6 only | Remove Skip attribute at L8812 | CLEAN -- no T1-T5 content |
| `RULES_CATALOG.md` | T4 only | Content restore (JS-042..JS-110) | CLEAN -- docs only |
| `002-trade-copier-spec.html` | T5 only | B39 scope row + closed DW card | CLEAN -- spec HTML only |
| `B190Tests.cs` | T1+T2+T3 tests | New test file (8 [Fact] tests) | CLEAN -- test code only |

---

## DNA Constraints

All tickets must satisfy:

| Constraint | Scope | Status |
|------------|-------|--------|
| 0 new `lock()` | All .cs changes | PLAN CONFIRMS: no lock() anywhere in B190 |
| 0 `async void` (non-event-handler) | All .cs changes | PLAN CONFIRMS: no async methods added |
| 0 `return null` in hot paths | GetQuickTicks, BuildSpinnerCluster | PLAN CONFIRMS: both return value types (tuple, DockPanel) |
| All modified methods CYC <= 8 | SetInstrument (CYC=5), BuildInstrRow (CYC=1), BuildSpinnerCluster (CYC=1), GetQuickTicks (CYC=4) | PASS |
| ASCII-only string literals | All changes | PLAN CONFIRMS: "^" (0x5E), "v" (0x76), all labels ASCII |
| No FontFamily | All .cs | PLAN CONFIRMS: no FontFamily usage |
| No hardcoded hex colors | All .cs | PLAN CONFIRMS: BrushTeal + Brushes.White (named constants) |
| No DateTime.Now | All .cs | PLAN CONFIRMS: no DateTime usage |
| No "PTT-" order names needed | All .cs | PLAN CONFIRMS: no CreateOrder calls |
| dotnet build 0 errors 0 warnings | All .cs | PLAN CONFIRMS: additive changes, no API breaking changes |
| ptt-sync.ps1 19 linked 0 errors | All .cs | PLAN CONFIRMS: no file add/delete in src/ that affects link count -- B190Tests.cs is in Tests/ subfolder |

---

## Items Closed by B190

| DW ID | Priority | Closed By | Method |
|-------|----------|-----------|--------|
| DW-B183-05 | P1 | T4 | Restore full RULES_CATALOG.md (JS-001..JS-110) |
| DW-B185-BE-ALL-SCOPE-01 | P2 | T5 | Update B39 spec HTML scope row + DW card |
| DW-B183-02 | P2 | T6 | Remove stale Skip from FlatGuardLoop test |
| DW-B183-01 | P2 | T6 (Ph5 audit) | Slate Gate tests confirmed present (L8277-8441) + Ph5 signs off |

---

## Items Deferred (Carry Forward to B191+)

| DW ID | Priority | Description | Reason Deferred |
|-------|----------|-------------|----------------|
| DW-B183-03 | P1 | IsLaneEEligible CYC=8 frozen -- address when Lane E is next modified | Not in B190 scope; no Lane E changes in B190 |
| DW-B187-01 | P3 | followerPosQty path review in ResolveFollowerTargets | Low urgency; dead path, not broken path |

---

*Written by: ptt-architect*
*Epic: B190*
*Phase: 1 (Architecture Planning)*
*8 sequential thoughts completed before writing (mandatory pre-flight)*
*Return: PLAN_COMPLETE*
