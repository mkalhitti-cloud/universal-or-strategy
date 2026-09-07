# Ticket C-04 Verification Report
**Ticket**: C-04 -- `TradeCopierWindow::BuildActionButtons`
**File**: `src/PropTraderTools/TradeCopierWindow.cs` (READ-ONLY)
**Test file**: `tests/PropTraderTools.Tests/Wave1LaneCTests.cs`
**Verifier**: ptt-verifier (WAVE1-LANE-C, Phase 4b)
**Date**: 2026-09-07

---

## Layer 2 vs Layer 3 Cross-Check

Engineer self-reported (Layer 2): BUILD_PASS, 186 tests passing, all 7 scans clean.
Verifier independently ran (Layer 3): all scans confirmed. Results below.

---

## Check 1 — JS-021 lock() Scan (SCAN-01)

Command: `Select-String -Path src/PropTraderTools/TradeCopierWindow.cs -Pattern "lock\("`

Results:
- Line 581: comment-only (`// ... no lock() ...`)
- Line 754: comment-only (`// ... no lock() ...`)

**Zero live lock() calls. PASS.**

---

## Check 2 — CCN via lizard (SCAN-05)

Command: `lizard src/PropTraderTools/TradeCopierWindow.cs --csv -x "*/bin/*" -x "*/obj/*"`

| Method | Lines | CCN | <= 8? |
|--------|-------|-----|-------|
| `BuildActionButtons` | 755-769 | **1** | YES |
| `BuildTrimActionButton` | 772-785 | **1** | YES |
| `BuildFlattenActionButton` | 788-801 | **1** | YES |
| `BuildCancelActionButton` | 804-817 | **1** | YES |
| `BuildToggleActionButton` | 820-832 | **1** | YES |
| `BuildApplyActionButton` | 835-848 | **1** | YES |

All CCN = 1. Ticket spec CCN-before was 58, now 1. **PASS.**

---

## Check 3 — Helpers Are Private Instance Methods (Not Static)

Command: `Select-String ... -Pattern "private (static )?(void) Build(Trim|Flatten|Cancel|Toggle|Apply)ActionButton"`

Results (lines 772, 788, 804, 820, 835): All 5 helpers declared as `private void` with no `static` modifier.

**PASS -- all 5 helpers are private instance methods on TradeCopierWindow.**

---

## Check 4 — No Dispatcher.InvokeAsync or Task.Run in Helpers

Command: Content of lines 772-848 scanned for `Dispatcher\.InvokeAsync|Task\.Run`

Result: Zero matches.

**PASS -- helpers execute synchronously on calling UI thread.**

---

## Check 5 — Scope Boundary

Command: `git diff --name-only HEAD` + `git status --short`

Modified tracked files:
- `src/PropTraderTools/TradeCopierWindow.cs` (C-04 target) -- expected
- `src/PropTraderTools/TradeCopierPanel.cs` (earlier tickets C-01..C-03) -- not C-04

CopyEngine.cs: NOT modified. PASS.
Ptt*.cs files: NOT modified. PASS.
Wave1LaneCTests.cs: untracked new file (test additions). PASS.

**PASS -- no out-of-scope file modifications for C-04.**

---

## Check 6 — dotnet test (T_C04 Present and Passing)

Command: `dotnet test tests/PropTraderTools.Tests/PropTraderTools.Tests.csproj --no-build`

Full suite result: **Failed: 0, Passed: 203, Skipped: 3, Total: 206**
Floor requirement: >= 127. Actual: 203. PASS.

T_C04-specific filter result (5/5):
- T_C04_01_BuildTrimActionButton_GridColumn_IsThree -- **PASSED**
- T_C04_02_BuildFlattenActionButton_AddedTo_FlattenBtnsList -- **PASSED**
- T_C04_03_BuildCancelActionButton_Background_IsWBrushInactive -- **PASSED**
- T_C04_04_BuildToggleActionButton_GridColumn_IsSix -- **PASSED**
- T_C04_05_BuildApplyActionButton_TagArray_ContainsFiveElements -- **PASSED**

**PASS -- all 5 T_C04 tests present and green.**

---

## Check 7 — JS DNA Rules

| Rule | Pattern | Scan Result | Status |
|------|---------|-------------|--------|
| JS-021 (lock) | `lock\(` in TradeCopierWindow.cs | 2 comment-only hits | PASS |
| JS-001 (throw new) | `throw new` in C-04 helpers (lines 772-848) | 0 hits | PASS |
| JS-001 (throw new) | `throw new` elsewhere in file | Line 907 (pre-existing ConvertBack) | PASS (not C-04) |
| JS-002 (return null) | `return null` in C-04 helpers (lines 772-848) | 0 hits | PASS |
| JS-002 (return null) | `return null` elsewhere | Lines 1165, 1172 (pre-existing FindInstrument) | PASS (not C-04) |
| JS-033 (async void) | `async void` in TradeCopierWindow.cs | 3 comment-only hits | PASS |
| JS-080 (CYC<=8) | lizard CCN for all 6 methods | All CCN=1 | PASS |
| JS-096 (private instance) | No static on helpers | Confirmed | PASS |
| JS-066 (ASCII-only) | No Unicode in helper bodies | Confirmed | PASS |

**All JS rules: PASS.**

---

## Check 8 — Behaviour Fidelity (BuildActionButtons Orchestration)

Source verified at lines 755-848:

**Parent method** (`BuildActionButtons`, lines 755-769):
- Extracts `atmCb = (ComboBox)atmPanel.Children[0]` and `namedBox = (TextBox)atmPanel.Children[1]`
- Calls all 5 helpers in sequence: Trim, Flatten, Cancel, Toggle, Apply
- Identical orchestration to pre-extraction (zero behaviour change)

**Helper fidelity confirmed:**
| Helper | Grid.Column | List Add | Event Handler | Background |
|--------|-------------|----------|---------------|------------|
| BuildTrimActionButton | 3 | `_trimBtns.Add(btn)` | `OnRuleTrim` | WBrushInactive |
| BuildFlattenActionButton | 4 | `_flattenBtns.Add(btn)` | `OnRuleFlatten` | WBrushInactive |
| BuildCancelActionButton | 5 | `_cancelBtns.Add(btn)` | `OnRuleCancel` | WBrushInactive |
| BuildToggleActionButton | 6 | (none -- spec correct) | `OnRuleToggle` | WBrushActive |
| BuildApplyActionButton | 7 | (none -- tag-based) | `OnRowApply` | Tag=5-elem array |

BuildApplyActionButton tag array: `new object[] { tag, leaderCb, followerLb, atmCb, namedBox }` = 5 elements as specified.

**PASS -- behaviour identical to pre-extraction. Purely structural extraction.**

---

## Summary

| Check | Result |
|-------|--------|
| 1. lock() scan | PASS |
| 2. CCN (lizard) -- all 6 methods CCN=1 | PASS |
| 3. Private instance methods (no static) | PASS |
| 4. No Dispatcher.InvokeAsync / Task.Run | PASS |
| 5. Scope boundary | PASS |
| 6. dotnet test (203 pass, 5 T_C04 pass) | PASS |
| 7. JS DNA rules (JS-021/001/002/033/080/096/066) | PASS |
| 8. Behaviour fidelity | PASS |

---

VERDICT: VERIFY_PASS