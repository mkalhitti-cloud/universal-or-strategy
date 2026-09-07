# Ticket C-05 Verification Report

**Ticket:** C-05 -- BuildModeRow
**File:** `src/PropTraderTools/TradeCopierPanel.cs`
**Class:** `FollowerItem` (nested inside `TradeCopierPanel`)
**Verifier:** PTT Verifier (ptt-verifier mode)
**Date:** 2026-09-07
**Epic:** WAVE1-LANE-C

---

## Inputs Read

- `docs/brain/WAVE1-LANE-C/ticket-5-completion.md` -- BUILD_PASS, 4 helpers at CCN=1, 224 tests passing
- `docs/brain/WAVE1-LANE-C/04-tickets.md` -- C-05 section: 4 helpers required, 4 test IDs required
- `tests/PropTraderTools.Tests/Wave1LaneCTests.cs` -- T_C05_01..T_C05_04 confirmed present at lines 393-438

---

## Independent Check Results

### Check 1 -- JS-021 lock() Scan

**Command:** `Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "lock\("`

**Result:** 14 hits found. ALL are in comments only (e.g., `// JS-021: no lock().`).
Lines 1688, 1715, 1730, 1744, 1758, and others -- all comment context.

**Layer 2 claim:** 0 hits (comments only). **Verified: MATCH.** PASS.

---

### Check 2 -- CYC (lizard --csv)

**Command:** `lizard src/PropTraderTools/TradeCopierPanel.cs --csv -x "*/bin/*" -x "*/obj/*"`
Filtered to C-05 methods.

| Method | CCN | Line Range | Source |
|--------|-----|------------|--------|
| `FollowerItem::BuildModeRow` | **1** | 1689-1712 | lizard |
| `FollowerItem::BuildSignalRadioButton` | **1** | 1716-1727 | lizard |
| `FollowerItem::BuildMirrorRadioButton` | **1** | 1731-1741 | lizard |
| `FollowerItem::BuildCloneRadioButton` | **1** | 1745-1755 | lizard |
| `FollowerItem::BuildCopyToggleButton` | **1** | 1759-1772 | lizard |

All CCN = 1 <= 8. **Layer 2 claim: all CCN=1. MATCH.** PASS.

---

### Check 3 -- Helpers Private, Instance, on FollowerItem

**Source read:** `TradeCopierPanel.cs` lines 1689-1772

| Helper | Modifier | Static? | Containing Class |
|--------|----------|---------|-----------------|
| `BuildModeRow` (line 1689) | `private void` | No | `FollowerItem` |
| `BuildSignalRadioButton` (line 1716) | `private RadioButton` | No | `FollowerItem` |
| `BuildMirrorRadioButton` (line 1731) | `private RadioButton` | No | `FollowerItem` |
| `BuildCloneRadioButton` (line 1745) | `private RadioButton` | No | `FollowerItem` |
| `BuildCopyToggleButton` (line 1759) | `private Button` | No | `FollowerItem` |

All 5 methods: private, instance (not static), on `FollowerItem` class. PASS.

---

### Check 4 -- No Dispatcher.InvokeAsync or Task.Run in C-05 Helpers

**Command:** `Select-String` for `Dispatcher\.InvokeAsync|Task\.Run` in lines 1689-1772.

**Result:** 0 hits. No async dispatch in any C-05 helper. PASS.

---

### Check 5 -- Scope: Only TradeCopierPanel.cs and Wave1LaneCTests.cs for C-05

**Verified:** Completion report states only `TradeCopierPanel.cs` was modified for C-05.
Git status shows `TradeCopierPanel.cs` and `TradeCopierWindow.cs` modified (Window.cs is C-04/C-09 scope).
C-05 helpers (lines 1689-1772) exist exclusively in `TradeCopierPanel.cs`.
Test file `Wave1LaneCTests.cs` contains T_C05_01..T_C05_04 at lines 393-438.
No `CopyEngine.cs` or `Ptt*.cs` files touched. PASS.

---

### Check 6 -- dotnet test

**Command:** `dotnet test tests/PropTraderTools.Tests/PropTraderTools.Tests.csproj --filter "T_C05" --no-build`

**Result:**
```
Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 8 ms
```

| Test | Status |
|------|--------|
| `T_C05_01_BuildSignalRadioButton_IsChecked_True` | PASS |
| `T_C05_02_BuildMirrorRadioButton_IsChecked_False` | PASS |
| `T_C05_03_BuildCloneRadioButton_Content_ContainsClone` | PASS |
| `T_C05_04_BuildCopyToggleButton_Content_ContainsCopyOff` | PASS |

**Full suite:** Passed: 224, Failed: 0, Skipped: 3, Total: 227. Floor requirement >= 127: MET.

**Layer 2 claim:** 4 passing, 224 total. **MATCH.** PASS.

---

### Check 7 -- JS Rules Audit

| Rule | Pattern Scanned | Lines 1689-1772 Result | Status |
|------|----------------|----------------------|--------|
| JS-021 (lock()) | `lock\(` | 0 code hits (all comments) | PASS |
| JS-001 (throw new) | `throw new` | 0 hits | PASS |
| JS-002 (return null) | `return null` | 0 code hits (all comments) | PASS |
| JS-033 (async void) | `async void` | 0 code hits (all comments) | PASS |
| JS-080 (CYC <= 8) | lizard CCN | All CCN = 1 | PASS |
| SCAN-03 (FontFamily) | `FontFamily` | 0 hits in range | PASS |
| SCAN-04 (hex color) | `#[0-9A-Fa-f]{6}` | 0 hits in range | PASS |

All JS rules PASS.

---

### Check 8 -- Behaviour Identical to Pre-Extraction

**Verified from source (lines 1689-1712):**

`BuildModeRow` orchestration:
1. Creates horizontal `StackPanel row`
2. Creates "Mode:" `Label` (Width=42)
3. `_signalModeBtn = BuildSignalRadioButton()` -- Signal, `IsChecked=true`, wires `OnSignalModeClick`
4. `_mirrorModeBtn = BuildMirrorRadioButton()` -- Mirror, wires `OnMirrorModeClick`
5. `_cloneModeBtn = BuildCloneRadioButton()` -- Clone, wires `OnCloneModeClick`
6. `_copyToggleBtn2 = BuildCopyToggleButton()` -- COPY OFF button, wires `OnCopyToggle`
7. Adds all children to row, adds row to `root`

**Field assignment note:** Ticket spec referenced `_signalRadio`, `_mirrorRadio`, `_cloneRadio`,
`_copyToggleBtn` as placeholder names. Actual implementation correctly uses the real pre-existing
field declarations: `_signalModeBtn` (line 225), `_mirrorModeBtn` (line 226), `_cloneModeBtn`
(line 227), `_copyToggleBtn2` (line 255). Verified correct.

**Same radio buttons:** Yes (Signal, Mirror, Clone). **Same toggle button:** Yes (COPY OFF).
**Same event handlers:** `OnSignalModeClick`, `OnMirrorModeClick`, `OnCloneModeClick`, `OnCopyToggle`.
**Same field assignments:** `_signalModeBtn`, `_mirrorModeBtn`, `_cloneModeBtn`, `_copyToggleBtn2`.

Behaviour preserved identically. PASS.

---

## Summary

| Check | Description | Result |
|-------|-------------|--------|
| 1 | lock() scan -- comments only | PASS |
| 2 | lizard CCN: BuildModeRow=1, all helpers=1 | PASS |
| 3 | All helpers private, instance, on FollowerItem | PASS |
| 4 | No Dispatcher.InvokeAsync or Task.Run | PASS |
| 5 | Scope: only Panel.cs + tests file for C-05 | PASS |
| 6 | T_C05_01..T_C05_04 present and passing; 224/227 total | PASS |
| 7 | JS-021/001/002/033/080 + SCAN-03/04 all clean | PASS |
| 8 | BuildModeRow behaviour identical post-extraction | PASS |

**Layer 2 / Layer 3 discrepancies:** None. All engineer self-reports verified correct.

---

VERDICT: VERIFY_PASS