# B49-T1 Verification Report
**Block**: PTT-COPIER-B49
**Lane**: A
**Ticket**: T1 — TradeCopierPanel.cs layout reorder + CopyEngine.cs tag update
**Verifier**: ptt-verifier (Layer 3 independent)
**Date**: 2026-08-08
**Verdict**: VERIFY_PASS

---

## Layer 3 Scan Results (independently re-run — do NOT rely on engineer self-report)

### SCAN-01 — JS-021 No lock()

**Command**:
```powershell
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\*.cs" -Pattern "lock\s*\(" |
  Where-Object { $_.Line -notmatch "//.*lock" }
```
**Result**: 0 actual `lock(` calls. (All existing references in codebase are in comments only.)
**Status**: ✅ PASS
**Layer 2 match**: ✅ Engineer reported same result.

---

### SCAN-02 — JS-033 No async void

**Command**:
```powershell
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\*.cs" -Pattern "async void " |
  Where-Object { $_.Line -notmatch "//.*async" }
```
**Result**: 0 `async void` declarations.
**Status**: ✅ PASS
**Layer 2 match**: ✅ Engineer reported same result.

---

### SCAN-03 — JS-002 No new return null

**Command**:
```powershell
Select-String -Path "C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs" -Pattern "return null" |
  Select-Object LineNumber, Line
```
**Result**: 6 pre-existing `return null` occurrences:
- Line 435: `FindPriceCanvasPanel` (pre-existing B17 method)
- Line 494: `TryResolveLeaderAccount` (pre-existing B30-B method)
- Line 497: same method
- Line 501: same method
- Line 1525: (pre-existing helper)
- Line 1532: same helper

None are in B49 change regions (lines 759–764 BuildUI tail, lines 1682–1699 BuildCopierSection).
Zero new `return null` introduced by B49.
**Status**: ✅ PASS
**Layer 2 match**: ✅ Engineer reported 6 pre-existing occurrences, same lines.

---

### SCAN-04 — Hard-link integrity (no -Fix)

**Command**:
```powershell
powershell -File scripts\verify_links.ps1
```
(Run from `C:\WSGTA\universal-or-strategy\`)

**Result**:
```
OK      : 15
DESYNC  : 0
MISSING : 0
FIXED   : 0
SKIPPED : 7
PASS -- All deployable source files match NinjaTrader. No stale deploy risk.
```
**Status**: ✅ PASS — DESYNC=0, MISSING=0
**Layer 2 match**: ✅ Engineer reported identical output.

---

### SCAN-05 — Build gate

**Command**:
```powershell
dotnet build "C:\WSGTA\universal-or-strategy\src\PropTraderTools\PropTraderTools.csproj"
```

**Result**: Build FAILED — but **ALL errors are in `CopyEngineTests.cs` only** (pre-existing DW-B48-01):
- Multiple `CS0246: CopyRule not found`
- `CS0234: Immutable not found`
- `CS0234: NullabilityInfoContext not found`
- One `CS8632` warning in `CopyEngine.cs` (nullable annotation context) — pre-existing

**Zero errors in `TradeCopierPanel.cs` or `CopyEngine.cs`.**
DW-B48-01 exemption applies per ticket spec (V12.23 no scope creep rule).
**Status**: ✅ PASS (B49-introduced code: 0 errors)
**Layer 2 match**: ✅ Engineer reported same pattern (60 errors, all in CopyEngineTests.cs).

---

### SCAN-06 — CYC BuildCopierSection

**Method read** (lines 1682–1699 in TradeCopierPanel.cs):
```csharp
private void BuildCopierSection(StackPanel root)
{
    _copierCollapseBtn = new Button
    {
        Content = "\u25BC Copier",
        HorizontalContentAlignment = HorizontalAlignment.Left,
        Margin  = new Thickness(0, 4, 0, 1)
    };
    _copierCollapseBtn.SetResourceReference(Control.StyleProperty, "NTButtonStyle");
    _copierCollapseBtn.Click += OnCopierCollapseClick;
    root.Children.Add(_copierCollapseBtn);
    // B49: Mode row (Signal/Mirror/COPY OFF) moved inside Copier collapse box.
    // BuildModeRow appends directly to root -- it appears between the Copier header
    // and the follower scroll rows. Collapse click only hides _followerScrollViewer;
    // Mode row remains visible when Copier is collapsed (Director spec).
    BuildModeRow(root);
    root.Children.Add(_followerScrollViewer);  // sole visual tree insertion point for _followerScrollViewer
}
```

**Branch count**: `if`=0, `for`=0, `while`=0, `switch`=0, `?:`=0, `??`=0, `&&`=0, `||`=0.
`BuildModeRow(root)` is a method call — contributes 0 to CYC of caller.
**CYC = 1 (unchanged)**
**Status**: ✅ PASS (CYC = 1, well within <= 8)
**Layer 2 match**: ✅ Engineer reported CYC = 1.

---

### SCAN-07 — CYC BuildUI

**Method read** (tail verified at lines 759–769):
```csharp
            // B12 T3: Risk $ + ATR % spinner row (last row in _contentPanel)
            BuildRiskAtrRow(_contentPanel);

            // B49: Buttons first (BE/Quick rows), then Copier, then Position Tools.
            root.Children.Add(_beRowPanel);    // B49: moved from tail -- buttons first
            root.Children.Add(_quickRowPanel); // B49: moved from tail -- buttons first
            BuildCopierSection(root);          // B49: Copier second (Mode row now inside)
            root.Children.Add(_statusText);    // status below Copier
            BuildCollapsibleHeader(root);      // B49: Position Tools moved to bottom
            root.Children.Add(_contentPanel);  // B49: contentPanel follows its header
            Content = root;

            // V04: ensure consistent initial state
            UpdateButtonColors(false, false);
        }
```

B49 changes: removed 11 lines (no branches), replaced 5-line tail with 6-line tail (no branches).
**Branch count delta**: 0. No new `if`, `for`, `while`, `switch`, `?:`, `&&`, `||` added.
**CYC delta = 0. CYC unchanged, remains <= 8.**
**Status**: ✅ PASS
**Layer 2 match**: ✅ Engineer reported CYC unchanged.

---

## Acceptance Criteria Verification

Evidence sourced from independent `Select-String` scans and `ctx_read` of source files.
All line numbers are from the Wave workspace: `C:\WSGTA\universal-or-strategy\src\PropTraderTools\`.

| ID | Description | Evidence | Result |
|----|-------------|----------|--------|
| AC-01 | `_beRowPanel` is first child after applyBtn in BuildUI root StackPanel (no header above it) | `TradeCopierPanel.cs` line 759: `root.Children.Add(_beRowPanel)` — first child add after `applyBtn` (line 704) | ✅ PASS |
| AC-02 | `_quickRowPanel` appears immediately after `_beRowPanel` | `TradeCopierPanel.cs` line 760: `root.Children.Add(_quickRowPanel)` — immediately follows line 759 | ✅ PASS |
| AC-03 | `BuildCopierSection(root)` call appears after `_quickRowPanel` | `TradeCopierPanel.cs` line 761: `BuildCopierSection(root)` — follows lines 759–760 | ✅ PASS |
| AC-04 | `BuildModeRow(root)` INSIDE `BuildCopierSection`, between `_copierCollapseBtn.Add` and `_followerScrollViewer.Add` | `TradeCopierPanel.cs` line 1697: `BuildModeRow(root)` — between line 1692 (`root.Children.Add(_copierCollapseBtn)`) and line 1698 (`root.Children.Add(_followerScrollViewer)`) | ✅ PASS |
| AC-05 | `OnCopierCollapseClick` is UNCHANGED (still only toggles `_followerScrollViewer.Visibility`) | `TradeCopierPanel.cs` lines 1704–1711: method body reads `_followerScrollViewer.Visibility = _copierCollapsed ? Visibility.Collapsed : Visibility.Visible`. `_contentPanel` is untouched. No B49 modification. | ✅ PASS |
| AC-06 | `BuildCollapsibleHeader(root)` call appears AFTER `BuildCopierSection` in `BuildUI` tail | `TradeCopierPanel.cs` line 763: `BuildCollapsibleHeader(root)` — after line 761 (`BuildCopierSection`) and line 762 (`_statusText`) | ✅ PASS |
| AC-07 | `_contentPanel.Add` appears AFTER `BuildCollapsibleHeader` in `BuildUI` tail | `TradeCopierPanel.cs` line 764: `root.Children.Add(_contentPanel)` — last child add, after line 763 | ✅ PASS |
| AC-08 | No separator Border exists (lines 709-713 deleted) | `Select-String -Pattern "sep = new Border"` → **0 results** in TradeCopierPanel.cs. Separator entirely absent. | ✅ PASS |
| AC-09 | F5 readiness: 0 new build errors in B49 source | `dotnet build`: all errors in `CopyEngineTests.cs` (pre-existing DW-B48-01). `TradeCopierPanel.cs` + `CopyEngine.cs`: 0 errors. | ✅ PASS (DW-B48-01 exempt) |
| AC-10 | verify_links.ps1 DESYNC=0 MISSING=0 | Run independently: OK=15, DESYNC=0, MISSING=0. PASS. | ✅ PASS |
| AC-11 | Zero new fields, zero new event handlers, zero logic changes | Confirmed by inspection: BuildUI tail is pure child-order resequencing. BuildCopierSection adds one call site. CopyEngine.cs changes one string literal. No new fields, no new handlers, no logic branches added. | ✅ PASS |

---

## Cross-Check vs Engineer Layer 2 Report

| Check | Layer 2 (Engineer) | Layer 3 (Verifier) | Match? |
|-------|-------------------|-------------------|--------|
| SCAN-01 lock() | 0 actual lock() calls | 0 actual lock() calls | ✅ MATCH |
| SCAN-02 async void | 0 new declarations; 1 comment-only at line 1741 | 0 declarations | ✅ MATCH |
| SCAN-03 return null | 6 pre-existing, 0 new | 6 pre-existing at lines 435, 494, 497, 501, 1525, 1532. 0 new | ✅ MATCH |
| SCAN-04 verify_links.ps1 | OK=15, DESYNC=0, MISSING=0, PASS | OK=15, DESYNC=0, MISSING=0, PASS | ✅ MATCH |
| SCAN-05 dotnet build | 60 errors in CopyEngineTests.cs only (DW-B48-01 exempt) | Multiple errors all in CopyEngineTests.cs only. 0 in main source. | ✅ MATCH |
| SCAN-06 CYC BuildCopierSection | CYC = 1 | CYC = 1 (confirmed by method body inspection) | ✅ MATCH |
| SCAN-07 CYC BuildUI | CYC unchanged <= 8 | CYC delta = 0 (confirmed by tail inspection) | ✅ MATCH |
| AC-01 _beRowPanel position | Line 759 | Line 759 | ✅ MATCH |
| AC-04 BuildModeRow inside BuildCopierSection | Between lines 1692 and 1698 | Line 1697, between 1692 and 1698 | ✅ MATCH |
| AC-09 PttBuild.Tag | "PTT-COPIER B49 \| layout-reorder \| 2026-08-08" at CopyEngine.cs:41 | Confirmed CopyEngine.cs line 41 | ✅ MATCH |

**No discrepancies between Layer 2 (engineer) and Layer 3 (verifier) results.**

---

## DNA Rule Check

| Rule | Scan | Result |
|------|------|--------|
| JS-021 lock() banned | SCAN-01 | ✅ 0 actual lock() calls |
| JS-033 async void banned | SCAN-02 | ✅ 0 async void declarations |
| JS-002 no new return null | SCAN-03 | ✅ 0 new occurrences in B49 change areas |
| JS-008 SolidColorBrush.Freeze() | Inspect: no new brushes added | ✅ No new brushes |
| NT8-003 no volatile on value types | No new fields | ✅ No new fields |
| NT8 no FontFamily | No FontFamily= introduced | ✅ Absent |
| NT8 no #RRGGBB hex colors | No hex literals introduced | ✅ Absent |
| NT8 no DateTime.Now | No DateTime.Now introduced | ✅ Absent |
| CYC <= 8 | SCAN-06 + SCAN-07 | ✅ BuildCopierSection=1, BuildUI unchanged |

---

## Architecture Compliance

- **§2 Scope**: Exactly 2 files modified (`TradeCopierPanel.cs`, `CopyEngine.cs`). ✅
- **§3 Target panel order**: `[applyBtn] → [_beRowPanel] → [_quickRowPanel] → [BuildCopierSection] → [_statusText] → [BuildCollapsibleHeader] → [_contentPanel]` — confirmed by line-order scan. ✅
- **§4 Deletions**: B47 `BuildModeRow` at root (lines 706-707) deleted. Separator border (lines 709-713) deleted. Old `BuildCollapsibleHeader` (lines 715-716) deleted. Confirmed: no match for `sep = new Border` or displaced BuildModeRow in BuildUI. ✅
- **§5 BuildCopierSection**: `BuildModeRow(root)` inserted at line 1697, between `_copierCollapseBtn.Add` (1692) and `_followerScrollViewer.Add` (1698). ✅
- **§6 Tag**: `"PTT-COPIER B49 | layout-reorder | 2026-08-08"` at CopyEngine.cs line 41. ✅

---

## Deferred Items (carried forward)

| ID | Source Block | Description | Status |
|----|-------------|-------------|--------|
| DW-B48-01 | B48 | `CopyEngineTests.cs` — 60 compilation errors in test project | OPEN — out of scope for B49 |
| DW-B46-01 | B46 | Live F5 verification of full panel (requires running NinjaTrader instance) | OPEN — out of scope for B49 |
| DW-B42-02 | B42 | BE ALL / Quick ALL live verify (requires open position) | OPEN — out of scope for B49 |

---

## Verdict

**VERIFY_PASS**

All 7 scans independently clean. All 11 acceptance criteria satisfied with code-level evidence.
No Layer 2 / Layer 3 discrepancies found. No DNA violations introduced. Architecture matches §3 target panel order exactly.
