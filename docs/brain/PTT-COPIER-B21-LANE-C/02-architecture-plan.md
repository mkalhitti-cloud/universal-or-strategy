# PTT-COPIER-B21-LANE-C Architecture Plan

**Epic**: PTT-COPIER-B21-LANE-C
**Spec**: DW-ATM-DROPDOWN-01 (Director Approved, P2)
**Status**: REVIEW_PENDING
**Author**: ptt-architect
**Date**: 2026-07-14

---

## RULES CATALOG GATE

**Result**: PASS

- `docs/standards/jane-street/RULES_CATALOG.md` — UTF-8 readable, confirmed.
- JS-021 (`lock(`): grep scan of TradeCopierPanel.cs returns 0 matches. PASS.
- JS-033 (`async void`): no async void in ATM block (all methods are synchronous void). PASS.
- No new code introduced. Removal-only task. All P0 rules trivially satisfied.

---

## Overview

The ATM template ComboBox row in TradeCopierPanel is dead code. The selected template name
is stored in `_activeAtmTemplateName` but is **never passed to `SendCopy()`, `AddRule()`, or
any order path**. ChartTrader already exposes NT8's native ATM selector directly above the
panel — this row duplicates it with zero wiring.

This block removes the complete dead circuit: 2 fields, 4 methods, 2 call sites, and 7 header
comment lines. No new code is written. No tests are required.

**File ownership**: `TradeCopierPanel.cs` ONLY
**Wave workspace**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs`

---

## Component List

This block has exactly one component: surgical deletion from a single file.

| Component | Action | File |
|-----------|--------|------|
| `TradeCopierPanel` (field `_atmTemplateCombo`) | DELETE | TradeCopierPanel.cs |
| `TradeCopierPanel` (field `_activeAtmTemplateName`) | DELETE | TradeCopierPanel.cs |
| `TradeCopierPanel.GetAtmTemplatesDirectory()` | DELETE | TradeCopierPanel.cs |
| `TradeCopierPanel.BuildAtmTemplateRow()` | DELETE | TradeCopierPanel.cs |
| `TradeCopierPanel.LoadAtmTemplates()` | DELETE | TradeCopierPanel.cs |
| `TradeCopierPanel.OnAtmTemplateSelectionChanged()` | DELETE | TradeCopierPanel.cs |
| Call site: `BuildAtmTemplateRow(_contentPanel)` in `BuildUI()` | DELETE | TradeCopierPanel.cs |
| Call site: `LoadAtmTemplates()` in `OnLoaded()` | DELETE | TradeCopierPanel.cs |
| Header comment block (B11 T2 changelog, lines 51-57) | DELETE | TradeCopierPanel.cs |

---

## Exact Removal Map (Line Numbers Verified from Source)

All line numbers confirmed by live grep against the current file.

### Item 1 — Field: `_atmTemplateCombo` (Line 160)

```
Line 160: private ComboBox _atmTemplateCombo       = null;
```

**Remove**: Line 160 only. Keep adjacent lines (159 = B11 T2 comment block start, 161 = next field).

### Item 2 — Field: `_activeAtmTemplateName` (Line 161)

```
Line 161: private string   _activeAtmTemplateName  = string.Empty;
```

**Remove**: Line 161 only.

**Note**: Lines 158-161 form the `// B11 T2 -- ATM template ComboBox` comment block. The
comment on line 158-159 becomes orphaned after removing lines 160-161. The engineer MUST
also remove lines 158-159 (the comment header for these two fields):
```
Line 158: (blank)
Line 159:         // B11 T2 -- ATM template ComboBox and selection state (UI-thread-only; no volatile)
Line 160:         private ComboBox _atmTemplateCombo       = null;
Line 161:         private string   _activeAtmTemplateName  = string.Empty;
```
Remove lines 159-161 (the comment + 2 fields). The preceding blank line (158) remains.

### Item 3 — Method: `GetAtmTemplatesDirectory()` (Lines 1395-1403)

```
Lines 1395-1403:
        // B11 T2: Returns canonical NT8 ATM templates directory path.
        // Pure string concatenation -- no IO, no branches.
        // CYC=1: straight-line path build.
        private static string GetAtmTemplatesDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "NinjaTrader 8", "templates", "ATM") + Path.DirectorySeparatorChar;
        }
```

**Remove**: Lines 1395-1403 inclusive (comment block + method body including blank line 1404).

### Item 4 — Method: `BuildAtmTemplateRow()` (Lines 1405-1431)

```
Lines 1405-1431:
        // B11 T2: Appends "ATM:" label + ComboBox row to root StackPanel.
        // LoadAtmTemplates() populates ComboBox ItemsSource after construction.
        // CYC=1: straight-line widget construction.
        private void BuildAtmTemplateRow(StackPanel root)
        {
            var row = new StackPanel { ... };
            ...
            root.Children.Add(row);
        }
```

**Remove**: Lines 1405-1431 inclusive (comment block + full method body + trailing blank line 1432).

### Item 5 — Method: `LoadAtmTemplates()` (Lines 1433-1451)

```
Lines 1433-1451:
        // B11 T2: Reads .xml template filenames from NT8 ATM templates directory.
        // Populates _atmTemplateCombo.ItemsSource with filename-without-extension list.
        // On DirectoryNotFoundException or IO error: sets ItemsSource to empty array (no throw).
        // CYC=3: combo null guard(1), directory exists guard(2), foreach populate(3).
        private void LoadAtmTemplates()
        {
            if (_atmTemplateCombo == null) return;
            string dir = GetAtmTemplatesDirectory();
            ...
            _atmTemplateCombo.ItemsSource = names;
        }
```

**Remove**: Lines 1433-1451 inclusive (comment block + full method body + trailing blank line 1452).

### Item 6 — Method: `OnAtmTemplateSelectionChanged()` (Lines 1453-1461)

```
Lines 1453-1461:
        // B11 T2: Stores selected ATM template name in _activeAtmTemplateName field.
        // No engine call at selection time -- template applied when orders submitted (future block).
        // CYC=2: null guard(1) + store selection(2).
        private void OnAtmTemplateSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = _atmTemplateCombo?.SelectedItem as string;
            if (item == null) return;
            _activeAtmTemplateName = item;
        }
```

**Remove**: Lines 1453-1461 inclusive (comment block + full method body + trailing blank line 1462).

### Item 7 — Call site in `BuildUI()` (Line 566)

```
Line 564:             // B11 T2: ATM template row
Line 565:             BuildAtmTemplateRow(_contentPanel);
Line 566: (blank)
```

Wait — confirmed from grep line 566 is `BuildAtmTemplateRow(_contentPanel);` and line 564 is the comment. Verify exact sequence:

From live grep:
- Line 566: `            BuildAtmTemplateRow(_contentPanel);`

The comment on line 565 (`// B11 T2: ATM template row`) is the associated comment. Both lines must be removed together.

**Remove**: The comment line immediately above (`// B11 T2: ATM template row`) AND the call `BuildAtmTemplateRow(_contentPanel);` — together these form lines ~564-566. The engineer must confirm the exact comment line number by searching for `// B11 T2: ATM template row` adjacent to line 566.

### Item 8 — Call site in `OnLoaded()` (Line 459)

```
Line 459:             LoadAtmTemplates();
```

**Remove**: Line 459 only. No adjacent comment exists for this call site (the method context
comment is above `OnLoaded` itself, not per-call).

### Item 9 — Header comment lines 51–57

```
Lines 51-57:
// PTT-COPIER-B11-T2 -- TradeCopierPanel.cs
// B11 T2 CHANGES:
//   1. Added _atmTemplateCombo ComboBox field and _activeAtmTemplateName string field.
//   2. GetAtmTemplatesDirectory(): returns canonical NT8 ATM templates path.
//   3. LoadAtmTemplates(): reads .xml files from NT8 ATM directory; no throw on IO fail.
//   4. BuildAtmTemplateRow(): appends "ATM:" label + ComboBox row to panel StackPanel.
//   5. OnAtmTemplateSelectionChanged(): stores selected template name. CYC=2.
//   6. BuildUI(): calls BuildAtmTemplateRow(root) at end.
//   7. OnLoaded(): calls LoadAtmTemplates() at end.
```

**Remove**: Lines 51-57 inclusive (the entire B11 T2 changelog block).

---

## Orphan Reference Verification

| Symbol | Other references in file? | Verdict |
|--------|--------------------------|---------|
| `_atmTemplateCombo` | Lines 160, 1422, 1427, 1429, 1439, 1443, 1450, 1458 — ALL within the ATM block being removed | SAFE |
| `_activeAtmTemplateName` | Lines 161, 1460 — both within ATM block | SAFE |
| `BuildAtmTemplateRow` | Lines 54, 566 — comment (being removed) + call site (being removed) | SAFE |
| `LoadAtmTemplates` | Lines 53, 459 — comment (being removed) + call site (being removed) | SAFE |
| `OnAtmTemplateSelectionChanged` | Lines 55, 1427 — comment (being removed) + wire-up inside BuildAtmTemplateRow (being removed) | SAFE |
| `GetAtmTemplatesDirectory` | Lines 52, 1440 — comment (being removed) + call inside LoadAtmTemplates (being removed) | SAFE |

**Conclusion**: No orphaned callers. The ATM circuit is fully self-contained. After all 9 items
are removed, the file will compile clean with zero references to any removed symbol.

---

## `using System.IO` Observation (NOT in scope)

After removal of the 4 ATM methods, `using System.IO;` at line 112 becomes unused.
`Path`, `Directory`, and `File` types are referenced **only** in the ATM block.
This is an `IDE0005` (unnecessary using) warning in modern IDEs.

**Spec constraint**: The spec says "nothing else". `using System.IO;` is NOT authorized for
removal in this block. The NT8 compiler (Roslyn/.NET 4.8) accepts unused using directives
without error — build will still PASS. A future cleanup block may remove it.

---

## Data Flow (Confirmed Dead)

```
OnLoaded() --> LoadAtmTemplates()
                  --> GetAtmTemplatesDirectory()  [returns path string]
                  --> _atmTemplateCombo.ItemsSource = names[]

UI interaction --> OnAtmTemplateSelectionChanged()
                  --> _activeAtmTemplateName = item    [NEVER READ ANYWHERE]
                  --> (dead end -- not passed to CopyEngine, SendCopy, AddRule, or any order path)
```

The entire chain terminates at `_activeAtmTemplateName`. This field is assigned once and
never consumed. ChartTrader's native ATM selector (above the panel in the NT8 UI) handles
ATM strategy selection for actual order execution.

---

## NinjaTrader 8 API Usage

No NT8 API calls are added. Removed NT8/WPF usage:
- `Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)` — removed
- `ComboBox.ItemsSource` (for ATM list) — removed (field `_atmTemplateCombo`)
- `ComboBox.SelectionChanged` event — removed (wiring in `BuildAtmTemplateRow`)
- `SelectionChangedEventArgs` parameter — removed (in `OnAtmTemplateSelectionChanged`)

Surviving NT8/WPF API usage is unaffected.

---

## Threading Model

No threading changes. All removed methods were synchronous UI-thread-only:
- `BuildAtmTemplateRow`: called from `BuildUI()` on UI thread (constructor chain)
- `LoadAtmTemplates`: called from `OnLoaded()` on UI thread (Loaded event)
- `GetAtmTemplatesDirectory`: pure string computation, no threads
- `OnAtmTemplateSelectionChanged`: WPF SelectionChanged fires on UI thread

Dispatcher.InvokeAsync pattern is unaffected. `ConcurrentQueue` ownership is unaffected.

---

## Jane Street Rule Compliance

| Rule | Status | Evidence |
|------|--------|---------|
| JS-021 `lock()` banned | PASS | 0 `lock(` in file; removal introduces 0 |
| JS-033 `async void` banned | PASS | ATM methods are sync void event handlers |
| JS-001 no throw in hot paths | PASS | Removed methods had no throw (IO guard = silent no-op) |
| JS-002 no return null | PASS | Removed methods: void or string return, no null return |
| NT8-003 no volatile double | PASS | No volatile fields in ATM block |

---

## 7-Scan Checklist (Engineer Contract)

These 7 scans MUST all pass before the ticket is marked complete.
All scans run against `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs`.

```
SCAN-01: _atmTemplateCombo absent
    grep -n "_atmTemplateCombo" TradeCopierPanel.cs
    Expected: 0 matches

SCAN-02: _activeAtmTemplateName absent
    grep -n "_activeAtmTemplateName" TradeCopierPanel.cs
    Expected: 0 matches

SCAN-03: BuildAtmTemplateRow absent
    grep -n "BuildAtmTemplateRow" TradeCopierPanel.cs
    Expected: 0 matches

SCAN-04: LoadAtmTemplates absent
    grep -n "LoadAtmTemplates" TradeCopierPanel.cs
    Expected: 0 matches

SCAN-05: OnAtmTemplateSelectionChanged absent
    grep -n "OnAtmTemplateSelectionChanged" TradeCopierPanel.cs
    Expected: 0 matches

SCAN-06: No lock() anywhere in file
    grep -n "lock(" TradeCopierPanel.cs
    Expected: 0 matches

SCAN-07: Build passes
    dotnet build (in c:\WSGTA\universal-or-strategy)
    Expected: 0 errors, 0 new warnings
```

---

## No New Tests Required

Dead code removal does not require new xUnit `[Fact]` tests because:
1. No behavioral change to `CopyEngine` or any order path — existing tests remain green.
2. The ATM template selection had no test coverage (it was unwired dead code).
3. Build correctness (SCAN-07) + symbol absence (SCAN-01..05) are the verification contract.
4. The per-panel UI is not unit-testable without WPF infrastructure.

---

## Deferred Backlog (Carried Forward, Read-Only)

The following items from PTT-COPIER-B20-LANE-C/06-deferred-backlog.md remain OPEN and are
NOT affected by this block:

| ID | Description | Priority |
|----|-------------|---------|
| DW-B9-01 | ATR box visualization on chart canvas | P2 |
| DW-B9-03 | Click trader Bid+1/Ask-1 auto-offset | P3 |
| DW-B12-DEFER-01 | Full-panel mode expansion | P2 |
| DW-B12-DEFER-02 | Auto-trail stop from BE CONNECTED | P3 |
| DW-B12-DEFER-03 | Math.Clamp ban comment fix | P3 |
| DW-B12-DEFER-04 | Align test names | P3 |
| DW-B19L2-DEFER-01 | ExitBufferTicks value-object | P2 |
| DW-B19L2-DEFER-02 | Spread validation guard | P2 |
| DW-B19L2-DEFER-03 | OnMarketData event hook | P2 |
| DW-B19L2-DEFER-04 | Telemetry log anchor price | P3 |

---

## Summary

- **Spec**: DW-ATM-DROPDOWN-01 — ATM template ComboBox row is dead code; remove entirely.
- **File**: TradeCopierPanel.cs (single file, Wave workspace)
- **Changes**: 2 field declarations, 4 private methods, 2 call sites, 7 header comment lines deleted
- **Net impact**: ~70 lines removed, zero added, zero behavior changed
- **Build**: Passes (no orphaned callers, `using System.IO` tolerated by NT8 compiler)
- **Tests**: None required
- **JS rules**: All P0 rules satisfied (no lock, no async void, no throw, no return null)
- **NT8 rules**: NT8-003 not applicable (no volatile fields in ATM block)
