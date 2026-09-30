# 02 -- Architecture Plan: CreateSection3_Config Extraction

**Epic**: EPIC-UI-SECTION3-CONFIG
**File**: [`src/V12_002.UI.Panel.Construction.cs`](../../src/V12_002.UI.Panel.Construction.cs)
**Method**: `CreateSection3_Config` (lines 1214--1536)
**Violation**: LOC=276 (not CYC -- CYC=5 is clean, stays at 5 or lower after extraction)
**Date**: 2026-07-04
**Agent**: v12-phase2-architecture

---

## Class Context

```
public partial class V12_002   // src/V12_002.UI.Panel.Construction.cs
```

All helpers are `private` members of this same partial class.
All instance fields written by the extracted blocks (`modeOrbButton`, `cnt1`, `svT1Val`, etc.)
are already declared at lines 124--153.  No new fields are needed.

---

## OKF Constraints Applied

| Rule | Impact on this plan |
|------|---------------------|
| CYC <= 8 (complexity-reduction.md) | Each helper is pure sequential UI construction -- CYC = 1 each |
| No new public API | All helpers are `private` |
| No hot-path allocations (microsecond-eternity.md) | These are cold-path UI build methods -- not applicable |
| Behavior-preserving (why-testing-is-hard.md) | No logic change; field assignments stay in-place inside helpers |
| One method per epic, helpers in same class | All 5 helpers in `V12_002.UI.Panel.Construction.cs` |

---

## Field Assignment Ownership

A critical constraint: extracted helpers that assign instance fields
(`modeOrbButton = ...`, `cnt1 = ...`, etc.) must use the same assignment
semantics.  Because all target fields are `private` members of `V12_002`
(the containing partial class), the extracted helpers have direct access
-- no `out` parameters or return-and-assign wrappers are needed.

---

## Block-A: Mode/Count Chip Grid

### (1) Helper method name
`BuildModeCountGrid`

### (2) Exact signature
```csharp
private Grid BuildModeCountGrid(string currentMode)
```
- `currentMode`: already computed at line 1233 before the block starts.
- Returns `Grid` so the caller can call `stack.Children.Add(result)`.
- Side effects: assigns `modeOrbButton`, `modeRmaButton`, `modeRetestButton`,
  `modeMomoButton`, `modeFfmaButton`, `modeTrendButton`, `cnt1`--`cnt5`.

### (3) Lines to move (verbatim block)
Lines **1228--1291** inclusive:

```
Grid modeCountGrid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
...
modeCountGrid.Children.Add(countColumn);
// (last line before stack.Children.Add)
```

The `stack.Children.Add(modeCountGrid)` at line 1291 becomes the call-site
line in the caller (see below) and is **not** moved into the helper.

### (4) Call-site replacement in `CreateSection3_Config`
Replace lines 1228--1291 with:
```csharp
stack.Children.Add(BuildModeCountGrid(currentMode));
```

---

## Block-B: SV T1+T2 Row

### (1) Helper method name
`BuildSvT1T2Row`

### (2) Exact signature
```csharp
private StackPanel BuildSvT1T2Row(UIConfigSnapshot config)
```
- `config`: already available in caller at line 1217.
- Returns `StackPanel` (the `svRow1` panel).
- Side effects: assigns `svT1Val`, `svT1Type`, `svT2Val`, `svT2Type`.

### (3) Lines to move
Lines **1293--1351** inclusive:

```
StackPanel svRow1 = new StackPanel
{
    Orientation = Orientation.Horizontal,
    Margin = new Thickness(0, 0, 0, 2),
};
...
svRow1.Children.Add(svT2Type);
// (last line before stack.Children.Add)
```

The `stack.Children.Add(svRow1)` at line 1352 is **not** moved; it becomes
the call-site line below.

### (4) Call-site replacement
Replace lines 1293--1352 with:
```csharp
stack.Children.Add(BuildSvT1T2Row(config));
```

---

## Block-C: Collapsible Target Rows T3/T4/T5

The three target rows (T3, T4, T5) are structurally identical except for:
- label text ("       T3" / "       T4" / "       T5")
- foreground color (OrangeFg / RedFg / PinkFg)
- bottom margin (2 / 2 / 3)
- initial Visibility (Visible / Collapsed / Collapsed)
- config value/type (Target3Value+Target3Type / Target4... / Target5...)
- instance field pair assigned (svT3Val+svT3Type / svT4Val+svT4Type / svT5Val+svT5Type)
- the `tNRow` StackPanel field written (t3Row / t4Row / t5Row)

A single parameterized helper eliminates the repetition.

### (1) Helper method name
`BuildTargetRow`

### (2) Exact signature
```csharp
private StackPanel BuildTargetRow(
    string label,
    Brush labelColor,
    double bottomMargin,
    Visibility initialVisibility,
    double configValue,
    int configType,
    out TextBox valBox,
    out ComboBox typeCombo)
```
- `label`: e.g. `"       T3"`
- `labelColor`: `OrangeFg` / `RedFg` / `PinkFg`
- `bottomMargin`: `2` / `2` / `3`
- `initialVisibility`: `Visibility.Visible` / `Visibility.Collapsed` / `Visibility.Collapsed`
- `configValue`: `config.Target3Value` etc.
- `configType`: `config.Target3Type` etc.
- `out TextBox valBox`: receives the created `svT3Val` / `svT4Val` / `svT5Val`
- `out ComboBox typeCombo`: receives the created `svT3Type` / `svT4Type` / `svT5Type`
- Returns `StackPanel` (the row panel -- also assigned to `t3Row` / `t4Row` / `t5Row`)

> **Why `out` here?** Unlike Block-A/B/C where the fields are assigned inside
> a block that maps 1:1 to a single helper, here a single generic helper
> builds the row for any of three targets.  The caller must receive the
> two created controls to assign to the correct pair of instance fields.
> This avoids three near-duplicate helpers and keeps each call's intent clear.

### (3) Lines to move
The **body** of a synthesized helper method replaces the common structure
found in all three blocks.  The source lines consumed are:

- T3 row: lines **1354--1376** (up to but not including `stack.Children.Add(t3Row)`)
- T4 row: lines **1378--1405** (up to but not including `stack.Children.Add(t4Row)`)
- T5 row: lines **1407--1434** (up to but not including `stack.Children.Add(t5Row)`)

The helper body is the generalized form of this repeated pattern:
```
var row = new StackPanel { Orientation = Horizontal, Margin = new Thickness(0, 0, 0, bottomMargin), Visibility = initialVisibility };
row.Children.Add(new TextBlock { Text = label, Foreground = labelColor, ... });
valBox = CreateTextBox(FormatPanelDouble(configValue), 30);
valBox.Height = 20;
valBox.FontSize = 9;
row.Children.Add(valBox);
typeCombo = CreateCombo(42, "ATR", "Ticks", "Pts", "Runner");
typeCombo.Height = 20;
typeCombo.FontSize = 8;
typeCombo.Margin = new Thickness(2, 0, 0, 0);
SetComboSelection(typeCombo, GetPanelTargetModeText(configType));
row.Children.Add(typeCombo);
return row;
```

### (4) Call-site replacement
Replace lines 1354--1434 (all three blocks plus their `stack.Children.Add` calls) with:

```csharp
t3Row = BuildTargetRow(
    "       T3", OrangeFg, 2, Visibility.Visible,
    config.Target3Value, config.Target3Type,
    out svT3Val, out svT3Type);
stack.Children.Add(t3Row);

t4Row = BuildTargetRow(
    "       T4", RedFg, 2, Visibility.Collapsed,
    config.Target4Value, config.Target4Type,
    out svT4Val, out svT4Type);
stack.Children.Add(t4Row);

t5Row = BuildTargetRow(
    "       T5", PinkFg, 3, Visibility.Collapsed,
    config.Target5Value, config.Target5Type,
    out svT5Val, out svT5Type);
stack.Children.Add(t5Row);
```

---

## Block-D: Risk Row (STR + MAX)

### (1) Helper method name
`BuildRiskRow`

### (2) Exact signature
```csharp
private Grid BuildRiskRow(UIConfigSnapshot config, string currentMode)
```
- `config`: available in caller.
- `currentMode`: available in caller (computed at line 1233).
- Returns `Grid`.
- Side effects: assigns `strVal`, `svStrType`, `maxVal`.

### (3) Lines to move
Lines **1436--1492** inclusive (up to but not including `stack.Children.Add(riskRow)`):

```
Grid riskRow = new Grid { Margin = new Thickness(0, 0, 0, 3) };
...
riskRow.Children.Add(maxVal);
```

The `stack.Children.Add(riskRow)` at line 1492 becomes the call-site.

### (4) Call-site replacement
Replace lines 1436--1492 with:
```csharp
stack.Children.Add(BuildRiskRow(config, currentMode));
```

---

## Block-E: Chase Row

### (1) Helper method name
`BuildChaseRow`

### (2) Exact signature
```csharp
private Grid BuildChaseRow(UIConfigSnapshot config)
```
- `config`: available in caller.
- Returns `Grid`.
- Side effects: assigns `citVal`.

### (3) Lines to move
Lines **1494--1523** inclusive (up to but not including `stack.Children.Add(citRow)`):

```
Grid citRow = new Grid { Margin = new Thickness(0, 2, 0, 3) };
...
citRow.Children.Add(citVal);
```

The `stack.Children.Add(citRow)` at line 1523 becomes the call-site.

### (4) Call-site replacement
Replace lines 1494--1523 with:
```csharp
stack.Children.Add(BuildChaseRow(config));
```

---

## Resulting Shape of `CreateSection3_Config` After Extraction

```csharp
private Border CreateSection3_Config()
{
    UIStateSnapshot snapshot = GetUiSnapshot();
    UIConfigSnapshot config = snapshot.Config ?? new UIConfigSnapshot();
    Border section = CreateSectionBorder();
    section.BorderThickness = new Thickness(0);
    StackPanel stack = new StackPanel
    {
        Margin = new Thickness(2, 2, 2, 4),
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };

    string currentMode = string.IsNullOrEmpty(snapshot.Mode) ? "ORB" : snapshot.Mode;
    int currentCount = Math.Max(1, Math.Min(5, snapshot.TargetCount));

    stack.Children.Add(CreateSectionHeader("SECTION 3: CONFIG"));
    stack.Children.Add(BuildModeCountGrid(currentMode));
    stack.Children.Add(BuildSvT1T2Row(config));

    t3Row = BuildTargetRow("       T3", OrangeFg, 2, Visibility.Visible,
        config.Target3Value, config.Target3Type, out svT3Val, out svT3Type);
    stack.Children.Add(t3Row);

    t4Row = BuildTargetRow("       T4", RedFg, 2, Visibility.Collapsed,
        config.Target4Value, config.Target4Type, out svT4Val, out svT4Type);
    stack.Children.Add(t4Row);

    t5Row = BuildTargetRow("       T5", PinkFg, 3, Visibility.Collapsed,
        config.Target5Value, config.Target5Type, out svT5Val, out svT5Type);
    stack.Children.Add(t5Row);

    stack.Children.Add(BuildRiskRow(config, currentMode));
    stack.Children.Add(BuildChaseRow(config));

    syncAllButton = CreateButton("SYNC ALL", 0, CyanBg, CyanFg, CyanBorder);
    syncAllButton.Height = 24;
    syncAllButton.FontWeight = FontWeights.Bold;
    stack.Children.Add(syncAllButton);

    section.Child = stack;
    _panelLastSyncedMode = currentMode;
    _panelLastSyncedTargetCount = currentCount;
    _panelAppliedConfigRevision = snapshot.ConfigRevision;
    return section;
}
```

LOC: ~40 lines (from 276). CYC remains 5 (unchanged sequential flow, no new branches).

---

## CYC Budget Per Helper

| Helper | New CYC | Passes <= 8? |
|--------|---------|-------------|
| `BuildModeCountGrid` | 1 | YES |
| `BuildSvT1T2Row` | 1 | YES |
| `BuildTargetRow` | 1 | YES |
| `BuildRiskRow` | 2 (one `if` for ORB mode at line 1462) | YES |
| `BuildChaseRow` | 1 | YES |
| `CreateSection3_Config` (post) | 5 (unchanged) | YES |

---

## Test Requirement (OKF testing-strategies.md)

Each extracted helper requires at minimum 1 xUnit `[Fact]` happy-path test.
Because these are WPF controls built on the Dispatcher thread, tests must
run on an STA thread (use `[STAFact]` from xunit.sta or a custom xUnit
test runner configured for STA).

Minimum test per helper:

| Helper | Test name | Assert |
|--------|-----------|--------|
| `BuildModeCountGrid` | `BuildModeCountGrid_WhenModeIsORB_ActivatesOrbChip` | `modeOrbButton.Tag == true` (active state) |
| `BuildSvT1T2Row` | `BuildSvT1T2Row_WhenConfigHasT1Value_SetsTextBoxText` | `svT1Val.Text == expected` |
| `BuildTargetRow` | `BuildTargetRow_WhenVisibilityCollapsed_RowIsCollapsed` | `row.Visibility == Collapsed` |
| `BuildRiskRow` | `BuildRiskRow_WhenModeIsORB_SetsComboToOR` | `svStrType.SelectedItem == "OR"` |
| `BuildChaseRow` | `BuildChaseRow_WhenConfigHasChasePoints_SetsTextBox` | `citVal.Text == expected` |

---

## Placement of Helpers in File

Insert all 5 private helper methods immediately after the closing brace of
`CreateSection3_Config` (after line 1536), before `CreateSectionBorder`
(which starts at line 1538).

Insertion order:
1. `BuildModeCountGrid`
2. `BuildSvT1T2Row`
3. `BuildTargetRow`
4. `BuildRiskRow`
5. `BuildChaseRow`

This clusters all Section 3 construction helpers together for locality.

---

## Summary

| Block | Helper | Signature | Lines consumed | Call replacement |
|-------|--------|-----------|---------------|-----------------|
| A | `BuildModeCountGrid` | `(string currentMode) : Grid` | 1228--1291 | `stack.Children.Add(BuildModeCountGrid(currentMode));` |
| B | `BuildSvT1T2Row` | `(UIConfigSnapshot config) : StackPanel` | 1293--1351 | `stack.Children.Add(BuildSvT1T2Row(config));` |
| C | `BuildTargetRow` | `(string label, Brush labelColor, double bottomMargin, Visibility initialVisibility, double configValue, int configType, out TextBox valBox, out ComboBox typeCombo) : StackPanel` | 1354--1434 (x3) | 3 calls assigning t3Row/t4Row/t5Row + stack.Children.Add |
| D | `BuildRiskRow` | `(UIConfigSnapshot config, string currentMode) : Grid` | 1436--1491 | `stack.Children.Add(BuildRiskRow(config, currentMode));` |
| E | `BuildChaseRow` | `(UIConfigSnapshot config) : Grid` | 1494--1522 | `stack.Children.Add(BuildChaseRow(config));` |
