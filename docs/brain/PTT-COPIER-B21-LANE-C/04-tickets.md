# PTT-COPIER-B21-LANE-C — Tickets

**Epic**: PTT-COPIER-B21-LANE-C
**Spec**: DW-ATM-DROPDOWN-01
**Phase**: 3 (Ticket Generation)
**Author**: ptt-architect
**Date**: 2026-07-14
**Plan status**: REVIEW_PASS (confirmed by ptt-plan-reviewer)

---

## T1 — Remove ATM Template Dead Code from TradeCopierPanel.cs

### Spec Requirements Satisfied

- **DW-ATM-DROPDOWN-01** — Remove the ATM template ComboBox row in its entirety:
  the `_atmTemplateCombo` field, `_activeAtmTemplateName` field,
  `BuildAtmTemplateRow()`, `LoadAtmTemplates()`, `OnAtmTemplateSelectionChanged()`,
  and `GetAtmTemplatesDirectory()` methods, both call sites, and the B11 T2
  header comment block (lines 51–57).

### File

```
c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs
```

No other file is modified. Scope is strictly `TradeCopierPanel.cs`.

---

### Deletions (Exact Line Numbers from REVIEW_PASS Plan)

The engineer MUST delete every item below. Items are ordered bottom-to-top within
the file so that line numbers for earlier items remain stable during editing.
Alternatively, use the symbol names as stable anchors for a top-down pass.

#### Item 9 — Header comment block (lines 51–57)

Remove the B11 T2 changelog block at the top of the file:

```
Lines 51–57:
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

Action: Delete lines 51–57 inclusive.

---

#### Item 8 — Call site in `OnLoaded()` (line 459)

```
Line 459:             LoadAtmTemplates();
```

Action: Delete line 459 only. No adjacent comment exists for this call site.

---

#### Item 7 — Call site in `BuildUI()` (line 566 + adjacent comment)

The call at line 566 and its immediately preceding comment line must be removed together.

```
~Line 565:             // B11 T2: ATM template row
 Line 566:             BuildAtmTemplateRow(_contentPanel);
```

Action: Search for `// B11 T2: ATM template row` adjacent to `BuildAtmTemplateRow(_contentPanel);`
and delete both lines. (Exact comment line number may be 564 or 565 depending on earlier edits —
use the text pattern, not the line number alone.)

---

#### Items 1–2 — Field declarations (lines 158–161)

Remove the comment header and both field declarations:

```
Line 158: (blank — retain)
Line 159:         // B11 T2 -- ATM template ComboBox and selection state (UI-thread-only; no volatile)
Line 160:         private ComboBox _atmTemplateCombo       = null;
Line 161:         private string   _activeAtmTemplateName  = string.Empty;
```

Action: Delete lines 159–161 inclusive (comment + 2 fields). The preceding blank line 158 remains.

---

#### Item 3 — Method `GetAtmTemplatesDirectory()` (lines 1395–1403)

```
Lines 1395–1403:
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

Action: Delete lines 1395–1403 inclusive (comment block + method body).
Also delete the blank line immediately following (line 1404).

---

#### Item 4 — Method `BuildAtmTemplateRow()` (lines 1405–1431)

```
Lines 1405–1431:
        // B11 T2: Appends "ATM:" label + ComboBox row to root StackPanel.
        // LoadAtmTemplates() populates ComboBox ItemsSource after construction.
        // CYC=1: straight-line widget construction.
        private void BuildAtmTemplateRow(StackPanel root)
        { ... }
```

Action: Delete lines 1405–1431 inclusive (comment block + full method body).
Also delete the blank line immediately following (line 1432).

---

#### Item 5 — Method `LoadAtmTemplates()` (lines 1433–1451)

```
Lines 1433–1451:
        // B11 T2: Reads .xml template filenames from NT8 ATM templates directory.
        // Populates _atmTemplateCombo.ItemsSource with filename-without-extension list.
        // On DirectoryNotFoundException or IO error: sets ItemsSource to empty array (no throw).
        // CYC=3: combo null guard(1), directory exists guard(2), foreach populate(3).
        private void LoadAtmTemplates()
        { ... }
```

Action: Delete lines 1433–1451 inclusive (comment block + full method body).
Also delete the blank line immediately following (line 1452).

---

#### Item 6 — Method `OnAtmTemplateSelectionChanged()` (lines 1453–1461)

```
Lines 1453–1461:
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

Action: Delete lines 1453–1461 inclusive (comment block + full method body).
Also delete the blank line immediately following (line 1462).

---

### Method Signatures Deleted

| Signature | Lines | CYC |
|-----------|-------|-----|
| `private static string GetAtmTemplatesDirectory()` | 1395–1403 | 1 |
| `private void BuildAtmTemplateRow(StackPanel root)` | 1405–1431 | 1 |
| `private void LoadAtmTemplates()` | 1433–1451 | 3 |
| `private void OnAtmTemplateSelectionChanged(object sender, SelectionChangedEventArgs e)` | 1453–1461 | 2 |

### Fields Deleted

| Declaration | Line |
|-------------|------|
| `private ComboBox _atmTemplateCombo = null;` | 160 |
| `private string _activeAtmTemplateName = string.Empty;` | 161 |

### `using System.IO` — NOT IN SCOPE

After ATM method removal, `using System.IO;` (line 112) becomes unused.
**Do not remove it.** The NT8 compiler (Roslyn / .NET Framework 4.8) accepts unused
using directives without error. A future block will address it. Removing it here violates
the single-concern scope of DW-ATM-DROPDOWN-01.

---

### Jane Street Rule Constraints

| Rule ID | Description | Constraint Applied |
|---------|-------------|-------------------|
| JS-021 | `lock()` banned | This deletion adds zero lock() calls. Post-edit file must contain 0 lock() — verified by SCAN-06. |
| JS-033 | `async void` banned (non-event-handler) | All removed methods are synchronous void. No async void introduced. |
| JS-001 | No throw in hot paths | No new code written. No throw introduced. |
| JS-002 | No `return null` for missing values | No new code written. No null return introduced. |
| CYC ≤ 8 | All methods ≤ 8 branches | All removed methods annotated CYC ≤ 3. Net change: 0 new methods. |

---

### NT8 Compiler Rule Constraints

| Rule ID | Description | Constraint Applied |
|---------|-------------|-------------------|
| NT8-003 | No `volatile double` | No volatile fields exist in the ATM block. Removal leaves no volatile fields added. |

No other NT8 compiler rules are triggered by this removal-only task.
The NT8 / .NET 4.8 build gate is the authoritative check (SCAN-07).

---

### [Fact] Section

**No new `[Fact]` tests required — dead code removal.**

Justification (per REVIEW_PASS plan):
1. No behavioral change to `CopyEngine` or any order path — existing `[Fact]` tests remain green.
2. ATM template selection had zero test coverage (the feature was unwired dead code).
3. Symbol-absence scans (SCAN-01..SCAN-05) + build gate (SCAN-07) constitute the full verification contract.
4. The WPF panel is not unit-testable without a live WPF infrastructure; no xUnit harness exists for it.

The engineer MUST confirm existing `CopyEngineTests.cs` tests pass unchanged after this edit.

---

### Scope Constraint

- **Modify**: `TradeCopierPanel.cs` ONLY.
- **Do not touch**: `CopyEngine.cs`, `TradeCopierWindow.cs`, `TradeCopierAddOn.cs`,
  `CopyEngineTests.cs`, any `.csproj`, any other file.
- **Do not refactor** any code adjacent to the removed blocks.
- **Do not remove** `using System.IO;` — explicitly out of scope.
- Every changed line must trace directly to DW-ATM-DROPDOWN-01.
  Any change not traceable to the spec is a No Scope Creep (V12.23) violation.

---

### 7-Scan Checklist (MANDATORY — Engineer Contract)

All 7 scans MUST pass before this ticket is marked complete.
Run all scans against `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs`
(and solution for SCAN-07).

```
SCAN-01: _atmTemplateCombo absent
    Command : grep -n "_atmTemplateCombo" TradeCopierPanel.cs
    Expected: 0 matches
    Fail if : any match appears

SCAN-02: _activeAtmTemplateName absent
    Command : grep -n "_activeAtmTemplateName" TradeCopierPanel.cs
    Expected: 0 matches
    Fail if : any match appears

SCAN-03: BuildAtmTemplateRow absent
    Command : grep -n "BuildAtmTemplateRow" TradeCopierPanel.cs
    Expected: 0 matches
    Fail if : any match appears

SCAN-04: LoadAtmTemplates absent
    Command : grep -n "LoadAtmTemplates" TradeCopierPanel.cs
    Expected: 0 matches
    Fail if : any match appears

SCAN-05: OnAtmTemplateSelectionChanged absent
    Command : grep -n "OnAtmTemplateSelectionChanged" TradeCopierPanel.cs
    Expected: 0 matches
    Fail if : any match appears

SCAN-06: No lock() anywhere in file (JS-021 gate)
    Command : grep -n "lock(" TradeCopierPanel.cs
    Expected: 0 matches
    Fail if : any match appears

SCAN-07: Build passes (NT8 gate)
    Command : dotnet build  (from c:\WSGTA\universal-or-strategy)
    Expected: 0 errors, 0 new warnings introduced by this edit
    Fail if : any error; new warnings are investigated but do not auto-block
```

All 7 scans must show the expected result before the engineer marks T1 complete.
A single failing scan = ticket remains open.

---

### Completion Criteria

T1 is complete when ALL of the following are true:

- [ ] Lines 51–57 (B11 T2 changelog header) deleted
- [ ] Line 459 (`LoadAtmTemplates()` call in `OnLoaded`) deleted
- [ ] Lines ~565-566 (`// B11 T2: ATM template row` comment + `BuildAtmTemplateRow(_contentPanel)` call in `BuildUI`) deleted
- [ ] Lines 159–161 (`// B11 T2` comment + 2 field declarations) deleted
- [ ] Lines 1395–1404 (`GetAtmTemplatesDirectory` comment + method + trailing blank) deleted
- [ ] Lines 1405–1432 (`BuildAtmTemplateRow` comment + method + trailing blank) deleted
- [ ] Lines 1433–1452 (`LoadAtmTemplates` comment + method + trailing blank) deleted
- [ ] Lines 1453–1462 (`OnAtmTemplateSelectionChanged` comment + method + trailing blank) deleted
- [ ] SCAN-01 → 0 matches
- [ ] SCAN-02 → 0 matches
- [ ] SCAN-03 → 0 matches
- [ ] SCAN-04 → 0 matches
- [ ] SCAN-05 → 0 matches
- [ ] SCAN-06 → 0 matches
- [ ] SCAN-07 → 0 errors
- [ ] Existing `CopyEngineTests.cs` [Fact] tests pass unchanged

---

*End of T1. This is the only ticket for PTT-COPIER-B21-LANE-C.*
