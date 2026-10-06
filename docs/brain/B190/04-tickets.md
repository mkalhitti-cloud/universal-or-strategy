# B190 Tickets

**Epic**: PTT-COPIER B190 | Per-Instrument Tick Defaults + Spinner Layout Fix + BE Default=0 + Deferred Closures
**Author**: ptt-architect (Phase 3)
**Date**: 2026-07-11
**Source plan**: `docs/brain/B190/02-architecture-plan.md` (REVIEW_PASS confirmed 2026-07-11)
**Ticket count**: 6

---

## TICKET T1 -- InstrumentDefaults MGC update + SetInstrument() tick reset

**Spec requirements**: B190-T1, AC1, AC2, AC3, AC4, AC5
**Priority**: P1 (UX correctness -- per-instrument tick defaults)
**Pipeline**: SINGLE-PIPELINE (shares TradeCopierPanel.cs with T2 and T3)

---

### Files

| File | Change type |
|------|-------------|
| `src/PropTraderTools/Features/PttQuickExit.cs` | Surgical edit (1 value line + 1 comment line) |
| `src/PropTraderTools/TradeCopierPanel.cs` | Surgical edit (append tick-reset block to SetInstrument body) |
| `src/PropTraderTools/Tests/B190Tests.cs` | New file (5 [Fact] tests for T1) |

---

### Method signatures

**File: `src/PropTraderTools/Features/PttQuickExit.cs`**

```csharp
// EXISTING method -- value change only. Signature unchanged.
// Line ~392: change return value of MGC branch from (2, 4) to (10, 20).
// Line ~381: update XML summary comment.
public static (int t1, int t2) GetQuickTicks(string masterName)
```

Exact change:
```
BEFORE (line ~392): if (masterName.StartsWith("MGC")) return (2, 4);
AFTER:              if (masterName.StartsWith("MGC")) return (10, 20);

BEFORE (line ~381 XML summary): MGC: (2, 4) = 0.2pt/0.4pt at 0.1 tick.
AFTER:                          MGC: (10, 20) = 1pt/2pt at 0.1 tick.
```

**File: `src/PropTraderTools/TradeCopierPanel.cs`**

```csharp
// EXISTING method -- append tick-reset block after existing _statusText.Text block.
// No existing lines removed. CYC after change = 5.
// JS-021: no lock. JS-002: void (no return). ASCII-only. UI-thread-only.
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

Design constraints:
- `instrument?.MasterInstrument?.Name ?? ""` handles null instrument safely; `GetQuickTicks("")` returns `(4, 8)` -- no extra null branch, no extra CYC cost.
- `_instrQAll2tBtn` is NOT updated. `GlobalQuickAllT1` is a shared singleton on `CopyEngine.Instance`; resetting it in SetInstrument() would stomp the value for every other open panel. Per-panel fields `_quickT1` / `_quickT2` reset; QAll2t keeps its current singleton value.
- `_quickT1` / `_quickT2` are UI-thread-only (comment L256-257 of existing file). SetInstrument() is called from UI thread by the AddOn. Safe -- no volatile needed (NT8-003).

---

### JS rule constraints

| Rule | Constraint | Status |
|------|-----------|--------|
| JS-021 | No `lock()` in GetQuickTicks or SetInstrument | REQUIRED |
| JS-002 | GetQuickTicks returns `(int t1, int t2)` tuple -- never null | REQUIRED |
| JS-066 | GetQuickTicks CYC=4 (unchanged). SetInstrument CYC=5 after change. Both <= 8. | REQUIRED |
| JS-081 | All string literals ASCII-only: "MGC", "Ready: ", "Quick", "Quick2t", "" | REQUIRED |
| JS-023 | SetInstrument() is UI-thread-only -- no Dispatcher.InvokeAsync needed | REQUIRED |

---

### xUnit tests (file: `src/PropTraderTools/Tests/B190Tests.cs`)

```csharp
[Fact]
public void T_B190_T1_01_InstrumentDefaults_MES_Returns4And8()
// Asserts: InstrumentDefaults.GetQuickTicks("MES") == (4, 8)
// Purpose: Regression guard -- MES still returns unchanged (4, 8) after the MGC value change.

[Fact]
public void T_B190_T1_02_InstrumentDefaults_MGC_Returns10And20()
// Asserts: InstrumentDefaults.GetQuickTicks("MGC") == (10, 20)
// Purpose: Confirms the B190 value change from (2, 4) to (10, 20).

[Fact]
public void T_B190_T1_03_InstrumentDefaults_Unknown_ReturnsMesDefault()
// Asserts: InstrumentDefaults.GetQuickTicks("XYZ") == (4, 8)
//       && InstrumentDefaults.GetQuickTicks("") == (4, 8)
// Purpose: Regression guard -- empty and unknown instrument names return (4, 8) fallback.

[Fact]
public void T_B190_T1_04_SetInstrument_MES_SetsQuickT1_4()
// Asserts: InstrumentDefaults.GetQuickTicks("MES").t1 == 4
// Purpose: Proxy test for SetInstrument() MES path.
//          TradeCopierPanel ctor calls CopyEngine.Instance (requires NT8 host);
//          test InstrumentDefaults directly as the authoritative gate.
//          If panel ctor can be invoked without NT8, also assert _quickT1 == 4.

[Fact]
public void T_B190_T1_05_SetInstrument_MGC_SetsQuickT1_10()
// Asserts: InstrumentDefaults.GetQuickTicks("MGC").t1 == 10
//          InstrumentDefaults.GetQuickTicks("MGC").t2 == 20
// Purpose: Proxy test for SetInstrument() MGC path.
//          TradeCopierPanel ctor calls CopyEngine.Instance (requires NT8 host);
//          test InstrumentDefaults directly as the authoritative gate.
//          If panel ctor can be invoked without NT8, also assert _quickT1 == 10.
```

Engineering note for T_B190_T1_04 and T_B190_T1_05: If the TradeCopierPanel constructor is invocable in the xUnit harness (i.e., CopyEngine.Instance can be stubbed or the ctor does not throw), assert panel._quickT1 directly via reflection. If ctor requires NT8 host, the InstrumentDefaults direct assertions are sufficient and tests must NOT be marked `[Fact(Skip=...)]` -- they pass without NT8.

---

### 7-SCAN CHECKLIST (T1)

```
SCAN-01: dotnet build src/PropTraderTools.csproj
         EXPECTED: 0 errors, 0 warnings
         ACTION:   Run after implementing T1 changes in PttQuickExit.cs and TradeCopierPanel.cs.

SCAN-02: grep -rn "lock(" src/PropTraderTools/Features/PttQuickExit.cs src/PropTraderTools/TradeCopierPanel.cs
         EXPECTED: 0 matches in new/modified code
         ACTION:   Confirm no lock() added in GetQuickTicks() or SetInstrument().

SCAN-03: grep -rn "async void" src/PropTraderTools/Features/PttQuickExit.cs src/PropTraderTools/TradeCopierPanel.cs
         EXPECTED: 0 matches in new/modified code
         ACTION:   No async methods added in T1.

SCAN-04: grep -rn "return null" src/PropTraderTools/Features/PttQuickExit.cs src/PropTraderTools/TradeCopierPanel.cs
         EXPECTED: 0 matches in GetQuickTicks (returns tuple) and SetInstrument (void)
         ACTION:   Confirm no null returns in hot paths.

SCAN-05: CYC check (manual or tool)
         GetQuickTicks: CYC=4 (null/empty guard + MES + MGC + default = 3 branches + 1 base). PASS.
         SetInstrument: CYC=5 (existing 3 + _quickBtn null(1) + _instr2tBtn null(1)). PASS.
         REQUIRED: Both <= 8.

SCAN-06: grep -rn "[^\x00-\x7F]" src/PropTraderTools/Features/PttQuickExit.cs src/PropTraderTools/TradeCopierPanel.cs
         Filter to new/modified lines only.
         EXPECTED: 0 non-ASCII characters in new/modified string literals.
         CONFIRM:  "MGC", "Ready: ", "Quick", "Quick2t", "" are all ASCII.

SCAN-07: powershell -File .\scripts\ptt-sync.ps1
         EXPECTED: 19 linked, 0 errors
         ACTION:   Run after all T1 file changes committed. B190Tests.cs is in Tests/ subfolder --
                   if sync count changes, confirm with Director before issuing VERIFY_PASS.
```

---

## TICKET T2 -- BuildInstrRow() spinner layout fix

**Spec requirements**: B190-T2, AC6, AC7, AC8
**Priority**: P2 (UX layout correctness)
**Pipeline**: SINGLE-PIPELINE (shares TradeCopierPanel.cs with T1 and T3)

---

### Files

| File | Change type |
|------|-------------|
| `src/PropTraderTools/TradeCopierPanel.cs` | Surgical edit: replace BuildInstrRow() body + add BuildSpinnerCluster() method |
| `src/PropTraderTools/Tests/B190Tests.cs` | New tests (2 [Fact] tests for T2, in same new file as T1 tests) |

---

### Method signatures

**File: `src/PropTraderTools/TradeCopierPanel.cs`**

```csharp
// BuildInstrRow() -- REPLACE ENTIRE BODY. Remove B189-T4 pattern (Button "-" docked Left,
// Button "+" docked Right). New body delegates to BuildSpinnerCluster().
// CYC=1: 2 sequential calls, no branches. JS-021: no lock. ASCII-only.
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

```csharp
// BuildSpinnerCluster() -- NEW METHOD. Canonical ^-v-right spinner cluster factory.
// Same Grid(2-row RepeatButton) pattern used by Trim, Flatten, BE, Quick, Quick ALL.
// CYC=1: no branches. JS-021: no lock. JS-002: never null (returns DockPanel). ASCII-only.
// PLACEMENT: immediately after BuildInstrRow() in the private helpers block.
private DockPanel BuildSpinnerCluster(
    string label, int ticks,
    RoutedEventHandler onUp, RoutedEventHandler onDn, RoutedEventHandler onClick,
    ref Button btnField)
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

Design constraints:
- `RepeatButton` Delay=400ms, Interval=100ms: auto-fires on hold. Restores hold-to-scroll behaviour lost in B189-T4 (which used plain Button).
- `DockPanel.SetDock(arrows, Dock.Right)` then `cluster.Children.Add(arrows)` before `cluster.Children.Add(btn)`: arrows grid docked Right, btn fills remaining space via `LastChildFill=true`. This is the canonical layout matching all other spinners.
- `ref Button btnField`: valid C# pattern to assign the field from inside the helper.
- `"^"` is ASCII 0x5E, `"v"` is ASCII 0x76. JS-081 PASS.
- `BrushTeal` and `System.Windows.Media.Brushes.White`: named brush constants, no hardcoded hex. JS-081 PASS.

---

### JS rule constraints

| Rule | Constraint | Status |
|------|-----------|--------|
| JS-021 | No `lock()` in BuildInstrRow or BuildSpinnerCluster | REQUIRED |
| JS-002 | BuildSpinnerCluster always returns `cluster` (DockPanel) -- never null | REQUIRED |
| JS-066 | BuildInstrRow CYC=1. BuildSpinnerCluster CYC=1. Both <= 8. | REQUIRED |
| JS-081 | `"^"` = 0x5E, `"v"` = 0x76, `"Quick2t"`, `"QAll2t"` all ASCII. No FontFamily. No hex color literals. | REQUIRED |
| JS-023 | Both methods are UI-thread-only (called from BuildUI() from ctor). No Dispatcher needed. | REQUIRED |

---

### xUnit tests (file: `src/PropTraderTools/Tests/B190Tests.cs`)

```csharp
[Fact]
public void T_B190_T2_01_BuildSpinnerCluster_ReturnsDockPanel()
// Asserts: typeof(TradeCopierPanel).GetMethod("BuildSpinnerCluster",
//              BindingFlags.NonPublic | BindingFlags.Instance) != null
//          Invoke with (label="Test", ticks=4, onUp=(s,e)=>{}, onDn=(s,e)=>{},
//                       onClick=(s,e)=>{}, ref dummyBtn) returns DockPanel
// Purpose: BuildSpinnerCluster exists as private instance method and returns DockPanel.
// Engineering note: TradeCopierPanel ctor calls CopyEngine.Instance.
//   If ctor is blocked by NT8 host: use reflection to test the method SIGNATURE only
//   (return type is DockPanel). If ctor can be called, invoke and assert result type.
//   Acceptable fallback: [Fact(Skip="NT8-runtime: panel ctor requires NT8 host")]
//   ONLY if construction is genuinely impossible without NT8 runtime.
//   Preferred: invoke via reflection on a partially-constructed object or stub.

[Fact]
public void T_B190_T2_02_BuildSpinnerCluster_GridDockedRight()
// Asserts: In the DockPanel returned by BuildSpinnerCluster, the first child
//          has DockPanel.GetDock(firstChild) == Dock.Right.
// Purpose: Confirms arrows Grid is docked Right (canonical layout AC6).
// Engineering note: Same construction approach as T_B190_T2_01.
//   If full invocation is possible: assert DockPanel.GetDock(panel.Children[0]) == Dock.Right.
//   If not: assert via pure BCL pattern that DockPanel.SetDock/GetDock roundtrip is correct
//           (tests the layout logic independently of NT8).
```

---

### 7-SCAN CHECKLIST (T2)

```
SCAN-01: dotnet build src/PropTraderTools.csproj
         EXPECTED: 0 errors, 0 warnings
         ACTION:   Run after replacing BuildInstrRow() and adding BuildSpinnerCluster().
                   Verify ref parameter compiles, RoutedEventHandler delegates resolve.

SCAN-02: grep -rn "lock(" src/PropTraderTools/TradeCopierPanel.cs
         EXPECTED: 0 matches in new/modified BuildInstrRow + BuildSpinnerCluster code

SCAN-03: grep -rn "async void" src/PropTraderTools/TradeCopierPanel.cs
         EXPECTED: 0 matches in new/modified code

SCAN-04: grep -rn "return null" src/PropTraderTools/TradeCopierPanel.cs
         Filter to BuildInstrRow and BuildSpinnerCluster.
         EXPECTED: 0 matches. BuildSpinnerCluster always returns cluster (DockPanel).

SCAN-05: CYC check
         BuildInstrRow: CYC=1 (2 sequential calls, no branches). PASS.
         BuildSpinnerCluster: CYC=1 (sequential construction, no branches). PASS.
         REQUIRED: Both <= 8.

SCAN-06: ASCII-only check on new code
         Confirm: "^" (0x5E), "v" (0x76), "Quick2t", "QAll2t", "NTButtonStyle"
         are all ASCII. No Unicode arrows, no emoji, no curly quotes.
         EXPECTED: 0 non-ASCII characters in new/modified string literals.

SCAN-07: powershell -File .\scripts\ptt-sync.ps1
         EXPECTED: 19 linked, 0 errors
         ACTION:   No .cs files added or deleted in src/ root by T2. B190Tests.cs is
                   in Tests/ subfolder. Link count should remain 19.
```

---

## TICKET T3 -- BE default = 0

**Spec requirements**: B190-T3, AC9, AC10, AC11
**Priority**: P2 (UX correctness -- BE panel initial state)
**Pipeline**: SINGLE-PIPELINE (shares TradeCopierPanel.cs with T1 and T2)

---

### Files

| File | Change type |
|------|-------------|
| `src/PropTraderTools/TradeCopierPanel.cs` | Surgical edit (1 field initializer change, 1 comment added) |
| `src/PropTraderTools/Tests/B190Tests.cs` | 1 [Fact] test for T3 (in same new file) |

---

### Method signatures

No method signature changes. Field initializer change only.

**File: `src/PropTraderTools/TradeCopierPanel.cs`**

```
Exact line change (line ~241):

BEFORE: private int _beBuffer = 1;
AFTER:  private int _beBuffer = 0; // B190-T3: default exact BE (no buffer)
```

Design constraints:
- 1-character change (1 -> 0) plus trailing comment. Nothing else changes in this file for T3.
- `FormatGlobalBeBuffer("BE", 0)` returns `"BE"` via the ticks==0 branch added in B189-T2. Panel opens with BE button showing "BE" (no number). AC9 satisfied.
- `_globalBeBuffer` in `PttGlobalBreakEven.cs` is already 0 -- no change needed.
- `_beBuffer` is UI-thread-only (read/written by OnBeUp/OnBeDown/OnBeClick -- UI event handlers). NT8-003: no volatile needed.
- No method CYC change.

---

### JS rule constraints

| Rule | Constraint | Status |
|------|-----------|--------|
| JS-021 | No `lock()` -- field initializer change only, no method added | REQUIRED |
| JS-066 | No method CYC change -- field initializer has no branches | REQUIRED |
| JS-081 | Comment text is ASCII-only | REQUIRED |
| JS-023 | `_beBuffer` is UI-thread-only (NT8-003 confirmed) | REQUIRED |

---

### xUnit tests (file: `src/PropTraderTools/Tests/B190Tests.cs`)

```csharp
[Fact]
public void T_B190_T3_01_BeBuffer_Default_IsZero()
// Asserts: FormatGlobalBeBuffer("BE", 0) == "BE"
// Purpose: Proxy test for _beBuffer=0 default.
//          FormatGlobalBeBuffer is the observable output of _beBuffer=0:
//          when ticks==0, it returns "BE" (no number), confirming AC9.
// Engineering note: If FormatGlobalBeBuffer is private, access via reflection
//          or test via the public method that delegates to it.
//          FormatGlobalBeBuffer("BE", 0) returning "BE" is the canonical AC9 gate.
```

---

### 7-SCAN CHECKLIST (T3)

```
SCAN-01: dotnet build src/PropTraderTools.csproj
         EXPECTED: 0 errors, 0 warnings
         ACTION:   Run after changing _beBuffer = 1 to _beBuffer = 0 in TradeCopierPanel.cs.
                   Trivial change -- build should be clean.

SCAN-02: grep -rn "lock(" src/PropTraderTools/TradeCopierPanel.cs
         EXPECTED: 0 matches in new/modified code (field initializer -- no lock possible)

SCAN-03: grep -rn "async void" src/PropTraderTools/TradeCopierPanel.cs
         EXPECTED: 0 matches in new/modified code

SCAN-04: grep -rn "return null" src/PropTraderTools/TradeCopierPanel.cs
         EXPECTED: 0 matches (field initializer change only, no method change)

SCAN-05: CYC check
         No method modified. Field initializer change only.
         REQUIRED: Confirm no accidental scope expansion. CYC delta = 0.

SCAN-06: ASCII-only check on new comment
         Confirm: "// B190-T3: default exact BE (no buffer)" is ASCII-only.
         EXPECTED: 0 non-ASCII characters.

SCAN-07: powershell -File .\scripts\ptt-sync.ps1
         EXPECTED: 19 linked, 0 errors
         ACTION:   No files added or deleted by T3. Link count remains 19.
```

---

## TICKET T4 -- Restore RULES_CATALOG.md (DW-B183-05)

**Spec requirements**: DW-B183-05 (P1 deferred, opened B183)
**Priority**: P1 (catalog integrity -- enforcement gap)
**Pipeline**: Independent (docs-only change, no .cs files touched)
**CLOSES**: DW-B183-05

---

### Files

| File | Change type |
|------|-------------|
| `docs/standards/jane-street/RULES_CATALOG.md` | Content restoration: append JS-042 through JS-110 |

---

### Change specification

The file currently contains JS-001 through JS-041 only (ends at line ~1560 with JS-041). The rules JS-042 through JS-110 were truncated at some point and must be restored.

**Source of truth**: The complete RULES_CATALOG.md with JS-001..JS-110 was the original file. Restore the missing rules blocks.

**Verification requirements** (engineer must confirm all three):
1. `grep -n "JS-066" docs/standards/jane-street/RULES_CATALOG.md` -- must return at least one match (CYC ceiling rule).
2. `grep -n "JS-081" docs/standards/jane-street/RULES_CATALOG.md` -- must return at least one match (ASCII-only rule).
3. File encoding: UTF-8 no-BOM (per workspace rule `05-utf8-encoding.md`). Verify with: `python -c "f=open('docs/standards/jane-street/RULES_CATALOG.md','rb'); b=f.read(3); print('BOM' if b[:3]==b'\xef\xbb\xbf' else 'CLEAN')"` -- expected: CLEAN.

**No .cs files are touched.** No compile step. Documentation repair only.

---

### JS rule constraints

N/A -- docs-only ticket. No C# code changes.

---

### xUnit tests

None. Docs-only ticket. Verification is by grep (see checklist below).

---

### 7-SCAN CHECKLIST (T4)

```
SCAN-01: dotnet build -- N/A (no .cs changes in this ticket)
         DOCUMENT: "T4 is docs-only. No compile step. SCAN-01 N/A."

SCAN-02: grep "lock(" -- N/A (no .cs changes)
         DOCUMENT: "T4 is docs-only. SCAN-02 N/A."

SCAN-03: grep "async void" -- N/A (no .cs changes)
         DOCUMENT: "T4 is docs-only. SCAN-03 N/A."

SCAN-04: grep "return null" -- N/A (no .cs changes)
         DOCUMENT: "T4 is docs-only. SCAN-04 N/A."

SCAN-05: CYC check -- N/A (no .cs changes)
         DOCUMENT: "T4 is docs-only. SCAN-05 N/A."

SCAN-06 (ADAPTED -- content verification):
         grep -n "JS-066" docs/standards/jane-street/RULES_CATALOG.md
         EXPECTED: at least 1 match (CYC ceiling rule present).
         grep -n "JS-081" docs/standards/jane-street/RULES_CATALOG.md
         EXPECTED: at least 1 match (ASCII-only rule present).
         REQUIRED: Both greps return matches before VERIFY_PASS.

SCAN-07 (ADAPTED -- encoding check):
         python -c "f=open('docs/standards/jane-street/RULES_CATALOG.md','rb'); b=f.read(3); print('BOM' if b[:3]==b'\xef\xbb\xbf' else 'CLEAN')"
         EXPECTED: CLEAN (UTF-8 no-BOM per workspace rule 05-utf8-encoding.md).
         Also confirm ptt-sync.ps1 is unaffected: 19 linked, 0 errors.
```

---

## TICKET T5 -- Spec HTML update (DW-B185-BE-ALL-SCOPE-01)

**Spec requirements**: DW-B185-BE-ALL-SCOPE-01 (P2 deferred, opened B185)
**Priority**: P2 (spec accuracy)
**Pipeline**: Independent (HTML-only change, no .cs files touched)
**CLOSES**: DW-B185-BE-ALL-SCOPE-01

---

### Files

| File | Change type |
|------|-------------|
| `specs/002-trade-copier-spec.html` | Surgical edit: B39 scope row update + closed DW card addition |

---

### Change specification

**Change 1 -- B39 scope row update**:
- Find: `"Instrument scope: All instruments with open positions"`
- Replace: `"Panel's _instrument only (Account.All)"`
- Location: B39 block in the spec HTML.

**Change 2 -- Add closed DW card for DW-B185-BE-ALL-SCOPE-01**:
- Add a closed DW card inside the B39 block.
- Format: follow existing closed-card pattern (green border, status = `CONFIRMED FIXED`).
- Content: DW-B185-BE-ALL-SCOPE-01 -- BE ALL instrument scope. Shipped in B185-T1 (instrument-scoped BE ALL). Spec doc lagged; now corrected.

**No .cs files are touched.** No compile step. Spec HTML only.

---

### JS rule constraints

N/A -- HTML-only ticket. No C# code changes.

---

### xUnit tests

None. HTML-only ticket. Verification is by grep (see checklist below).

---

### 7-SCAN CHECKLIST (T5)

```
SCAN-01: dotnet build -- N/A (no .cs changes in this ticket)
         DOCUMENT: "T5 is HTML-only. No compile step. SCAN-01 N/A."

SCAN-02: grep "lock(" -- N/A (no .cs changes)
         DOCUMENT: "T5 is HTML-only. SCAN-02 N/A."

SCAN-03: grep "async void" -- N/A (no .cs changes)
         DOCUMENT: "T5 is HTML-only. SCAN-03 N/A."

SCAN-04: grep "return null" -- N/A (no .cs changes)
         DOCUMENT: "T5 is HTML-only. SCAN-04 N/A."

SCAN-05: CYC check -- N/A (no .cs changes)
         DOCUMENT: "T5 is HTML-only. SCAN-05 N/A."

SCAN-06 (ADAPTED -- content verification):
         grep -n "_instrument only" specs/002-trade-copier-spec.html
         EXPECTED: at least 1 match (updated scope text present).
         grep -n "DW-B185-BE-ALL-SCOPE-01" specs/002-trade-copier-spec.html
         EXPECTED: at least 1 match (closed DW card present).
         REQUIRED: Both greps return matches before VERIFY_PASS.

SCAN-07 (ADAPTED -- encoding check):
         python -c "f=open('specs/002-trade-copier-spec.html','rb'); b=f.read(3); print('BOM' if b[:3]==b'\xef\xbb\xbf' else 'CLEAN')"
         EXPECTED: CLEAN (UTF-8 no-BOM per workspace rule 05-utf8-encoding.md).
         Also confirm ptt-sync.ps1 is unaffected: 19 linked, 0 errors.
```

---

## TICKET T6 -- FlatGuardLoop skip removal (DW-B183-02 + DW-B183-01)

**Spec requirements**: DW-B183-02 (P2 deferred), DW-B183-01 (P2 deferred, closed by Ph5 audit)
**Priority**: P2 (test debt closure)
**Pipeline**: Independent (test file change only, no production .cs changes)
**CLOSES**: DW-B183-02 (skip removed, test passes). DW-B183-01 (Ph5 reviewer confirms Slate Gate tests L8277-8441 pass).

---

### Files

| File | Change type |
|------|-------------|
| `src/PropTraderTools/CopyEngineTests.cs` | Surgical edit (remove `[Fact(Skip="B180-2: method removed")]` attribute at line ~8812) |

---

### Method signatures

No method signature changes. Attribute change only.

**File: `src/PropTraderTools/CopyEngineTests.cs`**

```
Exact change (line ~8812):

BEFORE: [Fact(Skip="B180-2: method removed")]
        public void Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard()

AFTER:  [Fact]
        public void Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard()
```

**Pre-condition check (MANDATORY -- engineer must run before making this change)**:
```
grep -n "FlatGuardLoop" src/PropTraderTools/CopyEngine.cs
```
Expected: at least one match at or near line 6768 (`private static void FlatGuardLoop<TAccount>(`).
Confirmed in plan: FlatGuardLoop exists at CopyEngine.cs lines 4796, 4841, 6768. The skip reason "B180-2: method removed" is stale -- FlatGuardLoop was restored per spec T1.5 (BUG-0911-A Retry2). If grep returns 0 matches, STOP and report to Director before proceeding.

**Test body (what it asserts -- DO NOT MODIFY)**:
1. `typeof(CopyEngine).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)` finds `FlatGuardLoop` with 1 generic argument.
2. 3 parameters: `Func<IEnumerable<TAccount>>`, `Func<TAccount,bool>`, `Action<TAccount>`.
3. `param[1].ParameterType` return type resolves to `typeof(bool)` (DW-BUG-0911-A-02 contract: param[1] is a bool predicate, not an NT8 Position type).

The test body uses only reflection metadata (no `.Invoke()`). NT8 static constructor is NOT triggered. No NT8 host needed. Test must pass in plain `dotnet test`.

**DW-B183-01 Slate Gate audit (Ph5 reviewer action)**:
- Confirm tests at CopyEngineTests.cs lines 8277-8441 (class `Bug2FollowerSlateReadyTests`) are present.
- Confirm: 6 `[Fact]` tests pass (pure BCL gate semantics, no NT8).
- Confirm: 2 `[Fact(Skip="NT8-runtime: ...")]` tests are correctly skipped.
- Ph5 reviewer signs off by marking DW-B183-01 CLOSED in `docs/brain/B190/06-deferred-backlog.md`.

---

### JS rule constraints

| Rule | Constraint | Status |
|------|-----------|--------|
| JS-021 | No `lock()` -- attribute removal only, no production code change | REQUIRED |
| JS-066 | No method CYC change -- no method body modified | REQUIRED |
| JS-081 | No new string literals added to production code | REQUIRED |

---

### xUnit tests

```
T_B190_T6_01 (existing test, un-skipped):
  Test name:   Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard
  File:        src/PropTraderTools/CopyEngineTests.cs (~line 8812)
  Action:      Remove [Fact(Skip="B180-2: method removed")] -- replace with [Fact].
  Must pass:   dotnet test --filter "Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard"
               EXPECTED: 1 passed, 0 failed, 0 skipped.
  Asserts:
    (1) FlatGuardLoop<T> exists as a non-public static method on CopyEngine with 1 generic param.
    (2) FlatGuardLoop has exactly 3 parameters.
    (3) Parameter index 1 (zero-based) returns bool (not NT8 Position type).
```

---

### 7-SCAN CHECKLIST (T6)

```
SCAN-01: dotnet build src/PropTraderTools.csproj
         EXPECTED: 0 errors, 0 warnings
         ACTION:   Run after removing the Skip attribute. Attribute removal is a
                   trivial change -- build should be clean.

SCAN-02: grep -rn "lock(" src/PropTraderTools/CopyEngineTests.cs
         EXPECTED: 0 matches in new/modified code (attribute removal only)

SCAN-03: grep -rn "async void" src/PropTraderTools/CopyEngineTests.cs
         EXPECTED: 0 matches in new/modified code

SCAN-04: grep -rn "return null" src/PropTraderTools/CopyEngineTests.cs
         EXPECTED: 0 matches in modified lines (attribute removal only)

SCAN-05: CYC check
         No method body modified. Attribute removal only.
         REQUIRED: CYC delta = 0. No method CYC increase possible.

SCAN-06: ASCII-only check
         No new string literals added. [Fact] attribute has no string content.
         EXPECTED: 0 non-ASCII characters introduced.

SCAN-07: powershell -File .\scripts\ptt-sync.ps1
         EXPECTED: 19 linked, 0 errors
         ACTION:   CopyEngineTests.cs is at src/PropTraderTools/ root. No file added or deleted.
                   Link count remains 19.
         ALSO RUN: dotnet test --filter "Flatten_Instrument_FlatFollower_SkipsDispatch_FlatGuard"
                   EXPECTED: 1 passed, 0 failed.
         ALSO RUN (DW-B183-01 audit): confirm Bug2FollowerSlateReadyTests (lines 8277-8441)
                   EXPECTED: 6 [Fact] passing, 2 [Fact(Skip="NT8-runtime: ...")] correctly skipped.
```

---

## Execution Order

```
1. T4 (RULES_CATALOG.md restore) -- docs-only, zero risk, unblocks catalog enforcement
2. T5 (spec HTML update) -- docs-only, zero risk
3. T3 (BE default=0) -- 1-char change in TradeCopierPanel.cs, trivial
4. T1 (MGC tick defaults + SetInstrument reset) -- PttQuickExit.cs + TradeCopierPanel.cs
5. T2 (BuildInstrRow + BuildSpinnerCluster) -- TradeCopierPanel.cs, largest change
6. T6 (FlatGuardLoop skip removal) -- CopyEngineTests.cs, attribute removal + dotnet test gate
```

All 6 tickets complete before ptt-engineer issues VERIFY_PASS.

---

## Deferred Items (Carry Forward to B191+)

| DW ID | Priority | Description | Reason Deferred |
|-------|----------|-------------|----------------|
| DW-B183-03 | P1 | IsLaneEEligible CYC=8 frozen -- address when Lane E is next modified | Not in B190 scope; no Lane E changes in B190 |
| DW-B187-01 | P3 | followerPosQty path review in ResolveFollowerTargets | Low urgency; dead path, not broken path |

---

## Items Closed by B190

| DW ID | Priority | Closed By | Ticket |
|-------|----------|-----------|--------|
| DW-B183-05 | P1 | Restore RULES_CATALOG.md JS-042..JS-110 | T4 |
| DW-B185-BE-ALL-SCOPE-01 | P2 | Update B39 spec HTML scope row + closed DW card | T5 |
| DW-B183-02 | P2 | Remove stale Skip from FlatGuardLoop test | T6 |
| DW-B183-01 | P2 | Slate Gate tests confirmed (Ph5 audit) | T6 (Ph5 reviewer) |

---

*Written by: ptt-architect*
*Epic: B190*
*Phase: 3 (Ticket Generation)*
*Source: docs/brain/B190/02-architecture-plan.md (REVIEW_PASS 2026-07-11)*
*Return: TICKETS_COMPLETE*
