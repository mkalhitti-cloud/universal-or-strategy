# PTT-COPIER-B4 — T3 Completion Report

**Ticket**: T3 — TradeCopierWindow.cs: col 8 BE cluster + handler
**File**: `src/PropTraderTools/TradeCopierWindow.cs`
**Engineer**: PTT Engineer (v12-engineer mode)
**Date**: 2026-06-03
**Status**: ENGINEER_COMPLETE

---

## What Was Implemented

All T3 changes were confirmed present in
`c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierWindow.cs` (400 LOC).

Per the ticket preamble: *"The plan-review confirmed all three source files in the Wave workspace
already contain the B4 additions."* Each change was verified line-by-line against the acceptance
criteria in `04-tickets.md`.

### CHANGE 1 — `BuildRuleRow(string instrumentName)`: col 8 + BE cluster

- **Line 120**: `grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // B4: BE cluster`
  — 9th column definition (cols 0–8). ✅
- **Lines 189–201**: BE cluster (`StackPanel`) containing:
  - `beBtn` — `Content = "[BE]"`, `Style = "NTButtonStyle"` ✅
  - `beBox` — `TextBox { Text = "2", Width = 28 }` ✅
  - `tksLabel` — `TextBlock { Text = "tks" }` with `SetResourceReference(..., "NTBrushes.SubtleBrush")` ✅
  - `beBtn.Tag = new object[] { instrumentName, beBox }` (string, TextBox) ✅
  - `beBtn.Click += OnRuleBreakEven` ✅
  - `Grid.SetColumn(beCluster, 8)` ✅

### CHANGE 2 — `BuildDynamicRuleRow()`: col 8 + BE cluster

- **Line 217**: `grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // B4: BE cluster` ✅
- **Lines 270–282**: BE cluster identical to CHANGE 1 **except**:
  - `beBtn.Tag = new object[] { instrTextBox, beBox }` — `instrTextBox` is the col-0 `TextBox` ✅

### CHANGE 3 — `OnRuleBreakEven` handler

- **Lines 337–352**: Handler present after `OnRuleToggle`, before `OnRowApply`:
  ```csharp
  private void OnRuleBreakEven(object sender, RoutedEventArgs e)
  {
      var tag = (sender as Button)?.Tag as object[];
      if (tag == null) return;
      string instrName = tag[0] is TextBox tb ? tb.Text : tag[0] as string;
      if (string.IsNullOrEmpty(instrName)) return;
      int ticks = 2;
      if (tag.Length > 1 && tag[1] is TextBox beBox)
      {
          if (int.TryParse(beBox.Text?.Trim(), out int parsed) && parsed >= 0)
              ticks = parsed;
      }
      var instrument = FindInstrument(instrName);
      if (instrument != null)
          _engine.BreakEven(instrument, ticks);
  }
  ```
  - CYC: 4 (null guard, ternary is-pattern, TryParse branch, instrument null check) ✅
  - Reuses `FindInstrument` — no duplication ✅
  - Calls `_engine.BreakEven(instrument, ticks)` — no `CreateOrder` ✅

---

## Constraints Verified

| Constraint | Result |
|------------|--------|
| No `lock()` | ✅ |
| No `CreateOrder` | ✅ |
| No Unicode / non-ASCII chars | ✅ |
| No hex color literals | ✅ |
| No `FontFamily` | ✅ |
| No `DateTime.Now` | ✅ |
| All string literals ASCII | `"[BE]"`, `"tks"`, `"2"` — all ASCII ✅ |
| Color via `SetResourceReference` only | `"NTBrushes.SubtleBrush"` ✅ |
| `CopyEngine.cs` not touched | ✅ |
| `TradeCopierPanel.cs` not touched | ✅ |

---

## 7-Scan Results (all zero)

| Scan | Command | Result |
|------|---------|--------|
| SCAN-01 | `Select-String -Pattern "lock\s*\("` | **0** |
| SCAN-02 | Non-ASCII character check | **0** |
| SCAN-03 | `Select-String -Pattern "FontFamily"` | **0** |
| SCAN-04 | `Select-String -Pattern "#[0-9A-Fa-f]{6}"` | **0** |
| SCAN-05 | `CreateOrder` call check (PTT- prefix mandate) | **0** |
| SCAN-06 | `Select-String -Pattern "DateTime\.Now[^U]"` | **0** |
| SCAN-07 | `Select-String -Pattern "\block\s*\("` | **0** |

---

## Acceptance Criteria Checklist (T3)

- [x] `BuildRuleRow` has 9 `ColumnDefinition` entries (cols 0–8); col 8 is `GridLength.Auto`.
- [x] `BuildRuleRow` BE cluster: button `Content = "[BE]"`, `TextBox { Text = "2" }`, `"tks"` label; placed at `Grid.SetColumn(beCluster, 8)`.
- [x] `BuildRuleRow` BE button `Tag = new object[] { instrumentName, beBox }` where `instrumentName` is string.
- [x] `BuildDynamicRuleRow` has 9 `ColumnDefinition` entries; identical cluster structure.
- [x] `BuildDynamicRuleRow` BE button `Tag = new object[] { instrTextBox, beBox }` where `instrTextBox` is the col-0 `TextBox`.
- [x] `OnRuleBreakEven` resolves instrument name from `tag[0]` as either `TextBox.Text` or raw string.
- [x] `OnRuleBreakEven` reads buffer from `tag[1]` TextBox; falls back to `2` on parse failure.
- [x] `OnRuleBreakEven` calls `FindInstrument` (REUSED — not duplicated).
- [x] `OnRuleBreakEven` calls `_engine.BreakEven(instrument, ticks)`.
- [x] All new string literals are ASCII-only: `"[BE]"`, `"tks"`, `"2"`.
- [x] Color references use `SetResourceReference ... "NTBrushes.SubtleBrush"` — no hex.

---

BUILD_PASS
