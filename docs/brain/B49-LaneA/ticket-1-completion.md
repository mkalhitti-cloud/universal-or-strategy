# B49-T1 Completion Report
**Block**: PTT-COPIER-B49  
**Lane**: A  
**Ticket**: T1 — TradeCopierPanel.cs layout reorder + CopyEngine.cs tag update  
**Engineer**: ptt-engineer  
**Date**: 2026-08-08  
**Status**: BUILD_PASS

---

## Summary

Implemented three surgical changes to reorder the panel layout so BE/Quick buttons appear first,
Copier (with Mode row embedded) appears second, and Position Tools / contentPanel appear at the bottom.

---

## Changes Applied

### Change 1 — TradeCopierPanel.cs BuildUI tail reorder

**File**: `C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs`

#### 1a — Deleted lines 706-707 (BuildModeRow at root level)
```csharp
// B47 T6-B: Mode row moved to root (above Position Tools header).
BuildModeRow(root);
```

#### 1b — Deleted lines 709-713 (separator border)
```csharp
// --- Separator ---
var sep = new Border { Height = 1, Margin = new Thickness(0, 2, 0, 2) };
sep.SetResourceReference(Border.BorderBrushProperty, "NTBrushes.BorderBrush");
sep.BorderThickness = new Thickness(0, 1, 0, 0);
root.Children.Add(sep);
```

#### 1c — Deleted lines 715-716 (BuildCollapsibleHeader at old position)
```csharp
// B12 T2: Collapse header row (above _contentPanel; always visible)
BuildCollapsibleHeader(root);
```

#### 1d — Replaced tail lines 769-773 with new 6-line tail (now lines 758-764)
```csharp
// B49: Buttons first (BE/Quick rows), then Copier, then Position Tools.
root.Children.Add(_beRowPanel);    // B49: moved from tail -- buttons first
root.Children.Add(_quickRowPanel); // B49: moved from tail -- buttons first
BuildCopierSection(root);          // B49: Copier second (Mode row now inside)
root.Children.Add(_statusText);    // status below Copier
BuildCollapsibleHeader(root);      // B49: Position Tools moved to bottom
root.Children.Add(_contentPanel);  // B49: contentPanel follows its header
```

---

### Change 2 — TradeCopierPanel.cs BuildCopierSection Mode row insertion

**File**: `C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs`  
**Method**: `BuildCopierSection` (lines 1682-1699 after Change 1)

Inserted `BuildModeRow(root)` call between `_copierCollapseBtn` add and `_followerScrollViewer` add:

```csharp
root.Children.Add(_copierCollapseBtn);
// B49: Mode row (Signal/Mirror/COPY OFF) moved inside Copier collapse box.
// BuildModeRow appends directly to root -- it appears between the Copier header
// and the follower scroll rows. Collapse click only hides _followerScrollViewer;
// Mode row remains visible when Copier is collapsed (Director spec).
BuildModeRow(root);
root.Children.Add(_followerScrollViewer);  // sole visual tree insertion point for _followerScrollViewer
```

---

### Change 3 — CopyEngine.cs PttBuild.Tag update

**File**: `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`  
**Line**: 41

```csharp
// FROM:
internal const string Tag = "PTT-COPIER B47 | panel-ux-redesign | 2026-08-07";
// TO:
internal const string Tag = "PTT-COPIER B49 | layout-reorder | 2026-08-08";
```

---

## Seven-Scan Results

### SCAN-01 — JS-021 No lock()
**Command**: `Select-String -Path TradeCopierPanel.cs,CopyEngine.cs -Pattern "lock\s*\("`  
**Result**: Zero actual `lock(` calls. All 12 matches are comment references (`// no lock`, `// JS-021: no lock()`).  
**Status**: ✅ PASS

### SCAN-02 — JS-033 No async void
**Command**: `Select-String -Path TradeCopierPanel.cs -Pattern "async void "`  
**Result**: Zero `async void` declarations. One comment-only reference at line 1741.  
**Status**: ✅ PASS

### SCAN-03 — JS-002 No new return null
**Command**: `Select-String -Path TradeCopierPanel.cs -Pattern "return null"`  
**Result**: 6 pre-existing `return null` occurrences in unchanged methods (`FindPriceCanvasPanel`, `TryResolveLeaderAccount`, `GetAsk`/`GetBid` helpers). Zero new occurrences introduced by B49 changes. Pre-existing instances exempt.  
**Status**: ✅ PASS

### SCAN-04 — Hard-link integrity (no -Fix)
**Command**: `powershell -File scripts\verify_links.ps1` (from Wave workspace root)  
**Result**:
```
OK      : 15
DESYNC  : 0
MISSING : 0
PASS -- All deployable src files match NinjaTrader. No stale deploy risk.
```
**Status**: ✅ PASS

### SCAN-05 — Build gate
**Command**: `dotnet build PropTraderTools.csproj`  
**Result**: Build FAILED — but ALL 60 errors are in `CopyEngineTests.cs` (pre-existing DW-B48-01 compilation errors: missing NinjaTrader type references in test project). One CS0433 `Globals` conflict is also test-project-only (assembly ambiguity). Zero errors in `TradeCopierPanel.cs` or `CopyEngine.cs` main code. DW-B48-01 exemption applies per ticket specification and V12.23 (no scope creep).  
**Status**: ✅ PASS (B49-introduced code: 0 errors)

### SCAN-06 — CYC BuildCopierSection
**Method body** (lines 1682-1699): Pure straight-line construction.  
Decision points: `if`=0, `for`=0, `while`=0, `switch`=0, `?:`=0, `??`=0, `&&`=0, `||`=0.  
`BuildModeRow(root)` is a method call — contributes 0 to CYC of caller.  
**CYC = 1 (unchanged)**  
**Status**: ✅ PASS

### SCAN-07 — CYC BuildUI
**Method body** (lines 667-769): Straight-line widget construction with delegated calls.  
No `if`, `for`, `while`, `switch`, `?:`, `??`, `&&`, or `||` in `BuildUI` body itself.  
B49 changes: removed 11 lines (no branches) and replaced 5-line tail with 6-line tail (no branches).  
CYC delta = 0.  
**CYC = 1 (unchanged, remains ≤ 8)**  
**Status**: ✅ PASS

---

## Hard-Link Sync Result

**Command**: `powershell -File scripts\verify_links.ps1 -Fix` (from Wave workspace root)  
**Result**:
```
OK      : 15
DESYNC  : 0
MISSING : 0
FIXED   : 0
PASS -- All deployable src files match NinjaTrader. No stale deploy risk.
```

---

## Acceptance Criteria Verification

| ID | Description | Result |
|----|-------------|--------|
| AC-01 | `_beRowPanel` appears above `BuildCopierSection` in `BuildUI` | ✅ Line 759 precedes line 761 |
| AC-02 | `_quickRowPanel` appears above `BuildCopierSection` in `BuildUI` | ✅ Line 760 precedes line 761 |
| AC-03 | `BuildCopierSection` called after both button rows | ✅ Line 761 follows lines 759-760 |
| AC-04 | `BuildModeRow(root)` inside `BuildCopierSection`, between `_copierCollapseBtn` and `_followerScrollViewer` | ✅ Line 1697 between lines 1692 and 1698 |
| AC-05 | `BuildCollapsibleHeader(root)` after `_statusText` add in `BuildUI` tail | ✅ Line 763 follows line 762 |
| AC-06 | `root.Children.Add(_contentPanel)` is last child add in `BuildUI` tail | ✅ Line 764 |
| AC-07 | No `BuildModeRow` call at root level in `BuildUI` (deleted) | ✅ Verified: no `BuildModeRow` in `BuildUI` body; single match only inside `BuildCopierSection` |
| AC-08 | Separator border deleted | ✅ `var sep = new Border` no longer present in `BuildUI` |
| AC-09 | `PttBuild.Tag` reads `"PTT-COPIER B49 \| layout-reorder \| 2026-08-08"` | ✅ CopyEngine.cs line 41 |
| AC-10 | No logic changes in any method | ✅ `git diff` shows only line moves + Tag update |
| AC-11 | Build compiles without errors (DW-B48-01 exempt) | ✅ 0 errors in B49 source files |

---

## Deferred Items (carried forward unchanged)

| ID | Source Block | Description | Status |
|----|-------------|-------------|--------|
| DW-B48-01 | B48 | `CopyEngineTests.cs` — 60 compilation errors in test project | OPEN — out of scope for B49 |
| DW-B46-01 | B46 | Live F5 verification of full panel (requires running NinjaTrader instance) | OPEN — out of scope for B49 |
| DW-B42-02 | B42 | BE ALL / Quick ALL live verify (requires open position) | OPEN — out of scope for B49 |

---

## BUILD_PASS
