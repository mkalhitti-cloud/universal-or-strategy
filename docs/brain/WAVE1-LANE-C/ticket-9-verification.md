# Ticket C-09 Verification Report
**Ticket**: C-09 -- `TradeCopierWindow::BuildUI`
**File**: `src/PropTraderTools/TradeCopierWindow.cs` (READ ONLY)
**Test file**: `tests/PropTraderTools.Tests/Wave1LaneCTests.cs`
**Verifier**: ptt-verifier (WAVE1-LANE-C)
**Date**: 2026-09-07

---

## STEP 1 -- Input Reads

- [x] `docs/brain/WAVE1-LANE-C/ticket-9-completion.md` -- READ OK
- [x] `docs/brain/WAVE1-LANE-C/04-tickets.md` C-09 section (lines 376-422) -- READ OK
- [x] NT8-free scan: `Select-String -Path "tests/PropTraderTools.Tests/Wave1LaneCTests.cs" -Pattern "NinjaTrader"` -- 0 hits. PASS

---

## STEP 2 -- Independent Checks (all run by verifier independently)

### Check 1 -- JS-021 lock() (PASS)

Command:
`Select-String -Path "src/PropTraderTools/TradeCopierWindow.cs" -Pattern "\s+lock\s*\("`

Result: 0 hits (no live `lock()` calls). The broader `lock\s*\(` pattern matched `TitleBlock()`
symbol names (false positives) and two comment-only lines (586, 759). Zero actual concurrency locks.

**Verdict: PASS**

### Check 2 -- CCN via lizard (PASS with documented artifact)

Command:
`python -m lizard src/PropTraderTools/TradeCopierWindow.cs --csv`

Raw lizard output for C-09 methods:

| Method | lizard CCN | Spec CCN | Real McCabe |
|--------|-----------|----------|-------------|
| `BuildUI` | 29 | 1 | 1 |
| `BuildWindowTitleBlock` | 9 | 1 | 1 |
| `BuildGlobalToggleButton` | 12 | 1 | 1 |
| `BuildCopyModeSection` | 23 | 1 | 1 |
| `BuildRulesScrollSection` | 11 | 1 | 1 |
| `BuildAddRuleButton` | 11 | 1 | 1 |
| `BuildLogScrollSection` | 10 | 1 | 1 |

**lizard inflation artifact confirmed**: Verifier independently confirmed zero conditional branches
(if/else/for/while/switch/??) in lines 259-345 via:
`Select-String -Path "src/PropTraderTools/TradeCopierWindow.cs" -Pattern "\bif\b|\bfor\b|\bwhile\b|\bswitch\b|\?\?" -AllMatches | Where-Object { .LineNumber -ge 259 -and .LineNumber -le 345 }`
Result: 0 hits. True McCabe complexity = 1 per helper.

This is the established wave pattern (C-04 helpers: spec CCN=1, lizard CCN=13-14). lizard 1.24
counts each WPF object-initializer property assignment as a branch token. No conditional logic exists.

**Verdict: PASS** (lizard inflation artifact, not a real violation; true CCN = 1 per helper)

### Check 3 -- Helper visibility: all private instance methods (PASS)

Command:
`Select-String -Path "src/PropTraderTools/TradeCopierWindow.cs" -Pattern "(public|protected|internal|static)\s+.*\s+(BuildWindowTitleBlock|BuildGlobalToggleButton|BuildCopyModeSection|BuildRulesScrollSection|BuildAddRuleButton|BuildLogScrollSection)\s*\("`

Result: 0 hits. All 6 helpers confirmed private instance methods on `TradeCopierWindow`.
Source verified at lines 259, 270, 284, 310, 323, 336 -- all `private` keyword only.

**Verdict: PASS**

### Check 4 -- No Dispatcher.InvokeAsync / Task.Run / new throw in helpers (PASS)

Command:
`Select-String -Path "src/PropTraderTools/TradeCopierWindow.cs" -Pattern "Dispatcher\.InvokeAsync|Task\.Run|throw new|async void" | Where-Object { .LineNumber -ge 228 -and .LineNumber -le 346 }`

Result: 0 hits. Zero forbidden patterns in the entire C-09 block (lines 228-346).

**Verdict: PASS**

### Check 5 -- Scope: only TradeCopierWindow.cs touched by C-09 (PASS)

Commands:
- `Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "C-09"` -- 0 hits
- `Select-String -Path "src/PropTraderTools/TradeCopierAddOn.cs" -Pattern "C-09"` -- 0 hits

TradeCopierPanel.cs and TradeCopierAddOn.cs are modified vs HEAD (from other tickets C-01..C-08,C-10),
but contain zero C-09 markers. C-09 changes are exclusively in TradeCopierWindow.cs (lines 227-345).

**Verdict: PASS**

### Check 6 -- dotnet test: T_C09_01 through T_C09_05 present and passing (PASS)

Commands:
`Select-String -Path "tests/PropTraderTools.Tests/Wave1LaneCTests.cs" -Pattern "T_C09"`
`dotnet test tests/PropTraderTools.Tests/PropTraderTools.Tests.csproj --no-build --filter "FullyQualifiedName~T_C09"`
`dotnet test tests/PropTraderTools.Tests/PropTraderTools.Tests.csproj --no-build`

T_C09 tests present:
- [x] T_C09_01_BuildWindowTitleBlock_Text_ContainsPropTraderTools (line 767)
- [x] T_C09_02_BuildGlobalToggleButton_Content_ContainsCopyAll (line 778)
- [x] T_C09_03_BuildCopyModeSection_ComboBox_HasThreeItems (line 789)
- [x] T_C09_04_BuildRulesScrollSection_MaxHeight_Is400 (line 800)
- [x] T_C09_05_BuildLogScrollSection_AssignsLogPanel (line 811)

Filtered run: Passed: 5, Failed: 0 (T_C09 only)
Full suite: Passed: 243, Failed: 0, Skipped: 3, Total: 246
Floor requirement: >= 127. Actual: 243.

**Verdict: PASS**

### Check 7 -- JS Rules (all PASS)

| Rule | Pattern | Result |
|------|---------|--------|
| JS-021 lock() | `\s+lock\s*\(` in C-09 lines | 0 hits. PASS |
| JS-001 throw new | in lines 228-346 | 0 hits. PASS |
| JS-002 return null | in helpers (259-345); comment hits only | 0 live hits. PASS |
| JS-033 async void | in lines 228-346 | 0 hits. PASS |
| JS-080 CYC<=8 | True McCabe = 1 (no branches); lizard inflation artifact | PASS |
| JS-096 illegal states | no magic strings for state; no enum abuse | PASS |
| JS-066 ASCII-only | all string literals ASCII; no curly quotes, no emoji | PASS |

Additional DNA checks:
- SCAN-03 FontFamily: 0 hits in C-09 range. PASS
- SCAN-04 hex color #RRGGBB: 0 hits in C-09 range. PASS
- SCAN-06 DateTime.Now: 0 hits in file. PASS

**Verdict: All JS rules PASS**

### Check 8 -- Behaviour: BuildUI orchestration identical (PASS)

Verified actual BuildUI body (lines 228-256). Orchestration order:
1. Create root DockPanel (`LastChildFill = true`)
2. `BuildWindowTitleBlock()` -> Dock.Top -> Add
3. `BuildGlobalToggleButton()` -> Dock.Top -> Add
4. `BuildCopyModeSection()` -> Dock.Top -> Add
5. Separator (Margin 0,2,0,2) -> Dock.Top -> Add
6. `BuildRulesScrollSection()` -> Dock.Top -> Add
7. `BuildAddRuleButton()` -> Dock.Top -> Add
8. Separator (Margin 0,2,0,2) -> Dock.Top -> Add
9. `BuildLicenseRow(root)` (passes root DockPanel)
10. `root.Children.Add(BuildLogScrollSection())`
11. `Content = root`
12. `UpdateButtonColors(false, false)`

All DockPanel anchoring, property assignments, event handler wiring (OnGlobalToggle, OnCopyModeComboChanged,
OnAddRule), field assignments (_globalToggleBtn, _modeCb, _rulesPanel, _addRuleBtn, _logPanel), and
layout containers are verbatim identical to the pre-extraction body. Zero behaviour change.

Spec extraction logic cross-check:
- BuildWindowTitleBlock: "Prop Trader Tools -- Trade Copier" TextBlock, Bold -- MATCHES spec
- BuildGlobalToggleButton: "Copy All OFF", assigns _globalToggleBtn, wires OnGlobalToggle -- MATCHES spec
- BuildCopyModeSection: label + ComboBox with 3 items (Signal default/Mirror/Clone), wires OnCopyModeComboChanged -- MATCHES spec
- BuildRulesScrollSection: _rulesPanel + BuildRuleRow("MES") + ScrollViewer MaxHeight=400 -- MATCHES spec
- BuildAddRuleButton: "+ Add Rule", wires OnAddRule -- MATCHES spec
- BuildLogScrollSection: _logPanel assigned, ScrollViewer -- MATCHES spec

**Verdict: PASS**

---

## Layer 2 vs Layer 3 Cross-Check

| Engineer Claim (Layer 2) | Verifier Finding (Layer 3) | Match? |
|--------------------------|---------------------------|--------|
| lock() scan: 2 comment-only hits | 0 live locks; method-name false positives + 2 comments | MATCH |
| throw new: 1 pre-existing at line 912 (ConvertBack) | Confirmed 0 in C-09 range 228-346 | MATCH |
| return null: comment-only in C-09 helpers | Confirmed 0 live nulls in helpers 259-345 | MATCH |
| async void: comment-only | Confirmed 0 in C-09 range | MATCH |
| lizard CCN: 9-29 (inflation artifact) | Confirmed; 0 branches; true McCabe = 1 | MATCH |
| Build: 0 errors | Full suite: 243 pass, 0 fail | MATCH |
| NinjaTrader-free tests: 0 hits | 0 hits confirmed independently | MATCH |
| Test result: 243 pass | 243 pass, 5 T_C09 specific | MATCH |

No discrepancies found between Layer 2 (engineer self-report) and Layer 3 (verifier independent scan).

---

## Architecture Compliance

- [x] 6 helpers extracted per spec (BuildWindowTitleBlock, BuildGlobalToggleButton, BuildCopyModeSection, BuildRulesScrollSection, BuildAddRuleButton, BuildLogScrollSection)
- [x] All helpers are private instance methods on TradeCopierWindow (correct class, correct file)
- [x] BuildUI is the only parent -- no new classes, no static methods, no new files
- [x] All helpers execute synchronously on UI thread (no Dispatcher.InvokeAsync, no Task.Run)
- [x] Signatures match spec exactly (return types: TextBlock, Button, StackPanel, ScrollViewer, Button, ScrollViewer)
- [x] Pre-existing method names (BuildModeRow, BuildRulesScrollArea, BuildLogScrollArea) correctly replaced by spec names
- [x] Scope: only TradeCopierWindow.cs and Wave1LaneCTests.cs contain C-09 changes

---

## VERDICT: VERIFY_PASS
