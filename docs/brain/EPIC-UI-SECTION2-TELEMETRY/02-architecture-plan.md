# 02 -- Architecture Plan: CreateSection2_Telemetry Extraction

**Epic**: EPIC-UI-SECTION2-TELEMETRY
**File**: [`src/V12_002.UI.Panel.Construction.cs`](../../src/V12_002.UI.Panel.Construction.cs)
**Method**: `CreateSection2_Telemetry` (lines 1098--1212)
**Violation**: LOC=94 (CYC=1 is clean; stays at 1 after extraction)
**Date**: 2026-07-04
**Agent**: v12-phase2-architecture

---

## Class Context

```
public partial class V12_002   // src/V12_002.UI.Panel.Construction.cs
```

All helpers are `private` members of this same partial class.
All instance fields written by the extracted blocks are already declared:
- `or5Text`, `or15Text` (line 111--112)
- `ema9Text`, `ema15Text`, `ema30Text`, `ema65Text`, `ema200Text` (line 113--117)
- `atrText` (line 118)
- `mktSyncButton` (line 119)
- `trendIndicator` (line 120)
- `trendText` (line 121)

No new fields are needed.

---

## OKF Constraints Applied

| Rule | Impact on this plan |
|------|---------------------|
| CYC <= 8 (complexity-reduction.md) | Each helper is pure sequential UI construction -- CYC = 1 each |
| No new public API | All helpers are `private` |
| No hot-path allocations (microsecond-eternity.md) | UI construction is cold-path -- not applicable |
| Behavior-preserving (why-testing-is-hard.md) | No logic change; field assignments remain in same instance fields |
| One method per epic, helpers in same class | All 3 helpers in `V12_002.UI.Panel.Construction.cs` |

---

## Field Assignment Ownership

Extracted helpers assign instance fields (`or5Text`, `ema9Text`, `mktSyncButton`, etc.)
directly -- no `out` parameters or return-and-assign wrappers needed -- because all
target fields are `private` members of the containing `V12_002` partial class.

The exception is `BuildOrTextBlock`, which builds a `TextBlock` for a field whose name
varies per call (`or5Text` vs `or15Text`). This helper returns the `TextBlock` and the
caller performs the field assignment. This avoids duplicating the field-assignment
responsibility inside a generic helper.

---

## Block-A + Block-B: OR TextBlock (Parameterized)

Blocks A and B are structurally identical:
- Same 5 `Inlines.Add` calls
- Same font/family settings
- Only differences: label prefix, top margin, bottom margin

They are merged into one parameterized helper.

### (1) Helper method name
`BuildOrTextBlock`

### (2) Exact signature
```csharp
private TextBlock BuildOrTextBlock(string labelText, double topMargin, double bottomMargin)
```
- `labelText` -- the bold label prefix string (`"OR5: "` or `"OR15: "`)
- `topMargin` -- top value of `Thickness(0, topMargin, 0, bottomMargin)`
- `bottomMargin` -- bottom value of the same `Thickness`
- Returns `TextBlock` (caller assigns to instance field)
- CYC = 1 (no branches)

### (3) Lines to move into helper
Lines **1109--1121** (Block-A, excluding `stack.Children.Add(or5Text)`) and
lines **1124--1136** (Block-B, excluding `stack.Children.Add(or15Text)`) are merged
into a single helper body:

```csharp
private TextBlock BuildOrTextBlock(string labelText, double topMargin, double bottomMargin)
{
    var tb = new TextBlock
    {
        FontSize = 10,
        FontFamily = ConsolasFont,
        Margin = new Thickness(0, topMargin, 0, bottomMargin),
    };
    tb.Inlines.Add(new System.Windows.Documents.Run(labelText) { Foreground = OrangeFg, FontWeight = FontWeights.Bold });
    tb.Inlines.Add(new System.Windows.Documents.Run("--")       { Foreground = OrangeFg });
    tb.Inlines.Add(new System.Windows.Documents.Run(" | ")      { Foreground = TextMuted });
    tb.Inlines.Add(new System.Windows.Documents.Run("--")       { Foreground = OrangeFg });
    tb.Inlines.Add(new System.Windows.Documents.Run(" (R: --)") { Foreground = TextMuted });
    return tb;
}
```

### (4) Call-site replacement in `CreateSection2_Telemetry`
Replace lines 1109--1137 with:

```csharp
or5Text = BuildOrTextBlock("OR5: ", 3, 1);
stack.Children.Add(or5Text);

or15Text = BuildOrTextBlock("OR15: ", 0, 2);
stack.Children.Add(or15Text);
```

---

## Block-C: EMA Rows

### (1) Helper method name
`BuildEmaRows`

### (2) Exact signature
```csharp
private void BuildEmaRows(StackPanel stack)
```
- `stack` -- the parent `StackPanel` to which both rows are added
- `void` -- all six instance field assignments (`ema9Text`, `ema15Text`, `ema30Text`,
  `ema65Text`, `ema200Text`, `atrText`) happen directly on instance fields inside the helper
- CYC = 1 (no branches)

### (3) Lines to move into helper
Lines **1139--1172** verbatim:

```csharp
private void BuildEmaRows(StackPanel stack)
{
    StackPanel emaRow1 = new StackPanel
    {
        Orientation = Orientation.Horizontal,
        Margin = new Thickness(0, 0, 0, 0),
    };
    ema9Text = CreateEmaLabel("9:", "--", TextPrimary);
    ema15Text = CreateEmaLabel("15:", "--", TextPrimary);
    ema30Text = CreateEmaLabel("30:", "--", GreenFg);
    emaRow1.Children.Add(ema9Text);
    emaRow1.Children.Add(ema15Text);
    emaRow1.Children.Add(ema30Text);
    stack.Children.Add(emaRow1);

    StackPanel emaRow2 = new StackPanel
    {
        Orientation = Orientation.Horizontal,
        Margin = new Thickness(0, 0, 0, 3),
    };
    ema65Text = CreateEmaLabel("65:", "--", TextPrimary);
    ema200Text = CreateEmaLabel("200:", "--", PurpleFg);
    atrText = new TextBlock
    {
        Text = "ATR: --",
        Foreground = TextMuted,
        FontSize = 10,
        FontFamily = ConsolasFont,
        FontStyle = FontStyles.Italic,
        Margin = new Thickness(8, 0, 0, 0),
        VerticalAlignment = VerticalAlignment.Center,
    };
    emaRow2.Children.Add(ema65Text);
    emaRow2.Children.Add(ema200Text);
    emaRow2.Children.Add(atrText);
    stack.Children.Add(emaRow2);
}
```

### (4) Call-site replacement in `CreateSection2_Telemetry`
Replace lines 1139--1172 with:

```csharp
BuildEmaRows(stack);
```

---

## Block-D: Sync Row Grid

### (1) Helper method name
`BuildSyncRow`

### (2) Exact signature
```csharp
private Grid BuildSyncRow()
```
- No parameters -- all data is from instance-level brush/font fields (`CyanBg`, `CyanFg`,
  `CyanBorder`, `GreenBg`, `GreenBorder`, `GreenFg`, `ConsolasFont`)
- Assigns instance fields `mktSyncButton`, `trendIndicator`, `trendText` directly
- Returns `Grid` (caller does `stack.Children.Add(BuildSyncRow())`)
- CYC = 1 (no branches)

### (3) Lines to move into helper
Lines **1174--1207** verbatim (Grid construction, both column definitions, mktSyncButton
build, trendIndicator/trendText build, all `Grid.SetColumn` and `syncRow.Children.Add`
calls, excluding the `stack.Children.Add(syncRow)` at line 1208):

```csharp
private Grid BuildSyncRow()
{
    Grid syncRow = new Grid();
    syncRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
    syncRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

    mktSyncButton = CreateButton("MKT SYNC", 70, CyanBg, CyanFg, CyanBorder);
    mktSyncButton.Height = 24;
    mktSyncButton.FontSize = 9;
    Grid.SetColumn(mktSyncButton, 0);
    syncRow.Children.Add(mktSyncButton);

    trendIndicator = new Border
    {
        Background = GreenBg,
        BorderBrush = GreenBorder,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(3),
        Padding = new Thickness(10, 2, 10, 2),
        Margin = new Thickness(8, 0, 0, 0),
        HorizontalAlignment = HorizontalAlignment.Right,
        Height = 24,
    };
    trendText = new TextBlock
    {
        Text = "BULLISH",
        Foreground = GreenFg,
        FontSize = 10,
        FontWeight = FontWeights.Bold,
        FontFamily = ConsolasFont,
        VerticalAlignment = VerticalAlignment.Center,
    };
    trendIndicator.Child = trendText;
    Grid.SetColumn(trendIndicator, 1);
    syncRow.Children.Add(trendIndicator);
    return syncRow;
}
```

### (4) Call-site replacement in `CreateSection2_Telemetry`
Replace lines 1174--1208 with:

```csharp
stack.Children.Add(BuildSyncRow());
```

---

## Resulting Shape of `CreateSection2_Telemetry` After Extraction

```csharp
private Border CreateSection2_Telemetry()
{
    Border section = CreateSectionBorder();
    StackPanel stack = new StackPanel
    {
        Margin = new Thickness(2, 2, 2, 1),
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };

    stack.Children.Add(CreateSectionHeader("SECTION 2: TELEMETRY"));

    or5Text = BuildOrTextBlock("OR5: ", 3, 1);
    stack.Children.Add(or5Text);

    or15Text = BuildOrTextBlock("OR15: ", 0, 2);
    stack.Children.Add(or15Text);

    BuildEmaRows(stack);

    stack.Children.Add(BuildSyncRow());

    section.Child = stack;
    return section;
}
```

LOC: ~17 lines (from 115). CYC remains 1 (unchanged -- no branches anywhere).

---

## CYC Budget Per Helper

| Helper | New CYC | Passes <= 8? |
|--------|---------|-------------|
| `BuildOrTextBlock` | 1 | YES |
| `BuildEmaRows` | 1 | YES |
| `BuildSyncRow` | 1 | YES |
| `CreateSection2_Telemetry` (post) | 1 (unchanged) | YES |

---

## Test Requirement (OKF testing-strategies.md)

Each extracted helper requires at minimum 1 xUnit `[Fact]` happy-path test.
WPF controls require an STA thread -- use `[STAFact]` from xunit.sta or a
custom xUnit runner configured for STA.

| Helper | Test name | Assert |
|--------|-----------|--------|
| `BuildOrTextBlock` | `BuildOrTextBlock_WhenCalledWithOR5Label_ReturnsTextBlockWithFiveInlines` | `tb.Inlines.Count == 5` |
| `BuildOrTextBlock` | `BuildOrTextBlock_WhenCalledWithTopMargin3_SetsThicknessTopTo3` | `tb.Margin.Top == 3` |
| `BuildEmaRows` | `BuildEmaRows_WhenCalled_AssignsAllSixEmaFields` | all 6 fields non-null after call |
| `BuildSyncRow` | `BuildSyncRow_WhenCalled_ReturnsTwoColumnGrid` | `grid.ColumnDefinitions.Count == 2` |
| `BuildSyncRow` | `BuildSyncRow_WhenCalled_AssignsMktSyncButton` | `mktSyncButton != null` |

---

## Placement of Helpers in File

Insert all 3 private helper methods immediately after the closing brace of
`CreateSection2_Telemetry` (after line 1212), before `CreateSection3_Config`
(which starts at line 1214).

Insertion order:
1. `BuildOrTextBlock`
2. `BuildEmaRows`
3. `BuildSyncRow`

This clusters all Section 2 construction helpers together for locality.

---

## Summary

| Block | Helper | Signature | Lines consumed | Call replacement |
|-------|--------|-----------|---------------|-----------------|
| A+B | `BuildOrTextBlock` | `(string labelText, double topMargin, double bottomMargin) : TextBlock` | 1109--1121 + 1124--1136 | 2 calls: `or5Text = BuildOrTextBlock(...)` + `or15Text = BuildOrTextBlock(...)` |
| C | `BuildEmaRows` | `(StackPanel stack) : void` | 1139--1172 | `BuildEmaRows(stack);` |
| D | `BuildSyncRow` | `() : Grid` | 1174--1207 | `stack.Children.Add(BuildSyncRow());` |
