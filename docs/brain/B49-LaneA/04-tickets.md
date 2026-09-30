# B49-LaneA Tickets
**Block**: PTT-COPIER-B49  
**Lane**: A  
**Label**: layout-reorder  
**Date**: 2026-08-08  
**Architect**: ptt-architect  
**Plan Status**: REVIEW_PASS  

---

## Spec Requirements Covered

| ID | Description |
|----|-------------|
| B49-UI-01 | `_beRowPanel` and `_quickRowPanel` appear above `BuildCopierSection` in panel |
| B49-UI-02 | Mode row (`BuildModeRow`) is inside `BuildCopierSection`, between collapse btn and scroll viewer |
| B49-UI-03 | `BuildCollapsibleHeader` and `_contentPanel` are at the bottom of the panel |
| B49-UI-04 | `PttBuild.Tag` reflects B49 block label and date |

---

## T1 — B49-T1: TradeCopierPanel.cs layout reorder + CopyEngine.cs tag update

### Files

| Action | File Path (Wave workspace) |
|--------|---------------------------|
| MODIFY | `C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs` |
| MODIFY | `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` |

**No other files may be touched.**

---

### Change 1 — BuildUI tail: remove displaced lines, reorder tail

**File**: `TradeCopierPanel.cs`

#### 1a. Lines to DELETE

Remove **lines 706–707** (Mode row at root level — B47 placement):
```csharp
            // B47 T6-B: Mode row moved to root (above Position Tools header).
            BuildModeRow(root);
```

Remove **lines 709–713** (separator border):
```csharp
            // --- Separator ---
            var sep = new Border { Height = 1, Margin = new Thickness(0, 2, 0, 2) };
            sep.SetResourceReference(Border.BorderBrushProperty, "NTBrushes.BorderBrush");
            sep.BorderThickness = new Thickness(0, 1, 0, 0);
            root.Children.Add(sep);
```

Remove **lines 715–716** (CollapsibleHeader at old position):
```csharp
            // B12 T2: Collapse header row (above _contentPanel; always visible)
            BuildCollapsibleHeader(root);
```

#### 1b. Tail replacement

**CURRENT** (lines 769–773 after the deletions above shift line numbers — match these five lines verbatim):
```csharp
            root.Children.Add(_contentPanel);
            BuildCopierSection(root);        // B47 T3-B: "v Copier" header + _followerScrollViewer
            root.Children.Add(_statusText);  // B47 T6-B: status below Copier
            root.Children.Add(_beRowPanel);  // B47 T5-B/T6-B: BE | BE ALL
            root.Children.Add(_quickRowPanel); // B47 T5-B/T6-B: Quick | Quick ALL
```

**TARGET** (exact replacement — 6 lines):
```csharp
            root.Children.Add(_beRowPanel);    // B49: moved from tail -- buttons first
            root.Children.Add(_quickRowPanel); // B49: moved from tail -- buttons first
            BuildCopierSection(root);          // B49: Copier second (Mode row now inside)
            root.Children.Add(_statusText);    // status below Copier
            BuildCollapsibleHeader(root);      // B49: Position Tools moved to bottom
            root.Children.Add(_contentPanel);  // B49: contentPanel follows its header
```

**Constraint**: All strings are ASCII-only. No `lock()`. No `async void`. No `DateTime.Now`.  
**CYC delta**: 0 — no new conditional branches introduced. `BuildUI` CYC remains <= 8.

---

### Change 2 — BuildCopierSection: insert Mode row between header and scroll viewer

**File**: `TradeCopierPanel.cs`  
**Method**: `BuildCopierSection(StackPanel root)` (approx. line 1691 after Change 1 shifts)

**CURRENT** (last two lines of `BuildCopierSection` body):
```csharp
            root.Children.Add(_copierCollapseBtn);
            root.Children.Add(_followerScrollViewer);  // sole visual tree insertion point for _followerScrollViewer
        }
```

**TARGET** (exact replacement — insert one call + comment):
```csharp
            root.Children.Add(_copierCollapseBtn);
            // B49: Mode row (Signal/Mirror/COPY OFF) moved inside Copier collapse box.
            // BuildModeRow appends directly to root -- it appears between the Copier header
            // and the follower scroll rows. Collapse click only hides _followerScrollViewer;
            // Mode row remains visible when Copier is collapsed (Director spec).
            BuildModeRow(root);
            root.Children.Add(_followerScrollViewer);  // sole visual tree insertion point for _followerScrollViewer
        }
```

**Constraint**: Single straight-line call insertion. No new fields. No new methods.  
**CYC**: `BuildCopierSection` remains CYC = 1 (no conditional branches). `BuildModeRow` is
a method call, not a branch. Trivially <= 8.  
**JS-021**: no `lock()`. **JS-033**: no `async void`. **JS-002**: no `return null`.

---

### Change 3 — PttBuild.Tag: update to B49

**File**: `CopyEngine.cs`  
**Line**: 41 (exact)

**CURRENT**:
```csharp
        internal const string Tag = "PTT-COPIER B47 | panel-ux-redesign | 2026-08-07";
```

**TARGET**:
```csharp
        internal const string Tag = "PTT-COPIER B49 | layout-reorder | 2026-08-08";
```

**Scope**: Single string literal. ASCII-only. No logic, no type, no dependency changes.

---

### Method Signatures (unchanged — no new methods)

No new methods are introduced. The following existing method signatures must not change:

| Method | Signature | File |
|--------|-----------|------|
| `BuildUI` | `private void BuildUI()` | `TradeCopierPanel.cs` |
| `BuildCopierSection` | `private void BuildCopierSection(StackPanel root)` | `TradeCopierPanel.cs` |
| `BuildModeRow` | `private void BuildModeRow(StackPanel root)` (called but not modified) | `TradeCopierPanel.cs` |
| `BuildCollapsibleHeader` | `private void BuildCollapsibleHeader(StackPanel root)` (called but not modified) | `TradeCopierPanel.cs` |

---

### xUnit Tests

No new xUnit tests are required for this ticket. The changes are **pure UI widget-order changes**
(StackPanel child insertion order) with no observable logic branching. The following existing tests
must remain GREEN (no regressions):

| Test | File | Assertion |
|------|------|-----------|
| All pre-existing tests in `CopyEngineTests.cs` | `tests/` | Must pass; DW-B48-01 exemption applies if test project fails to compile |

**DW-B48-01 exemption**: If `CopyEngineTests.cs` has pre-existing compilation errors, those errors
are out of scope for B49. The engineer must document them in the deferred items list but must NOT
fix them as part of this ticket (no scope creep per V12.23).

---

### Acceptance Criteria

| ID | Description | Pass Condition |
|----|-------------|----------------|
| AC-01 | `_beRowPanel` appears above `BuildCopierSection` in `BuildUI` | Code: `root.Children.Add(_beRowPanel)` precedes `BuildCopierSection(root)` call |
| AC-02 | `_quickRowPanel` appears above `BuildCopierSection` in `BuildUI` | Code: `root.Children.Add(_quickRowPanel)` precedes `BuildCopierSection(root)` call |
| AC-03 | `BuildCopierSection` is called after both button rows are added | Code: confirmed by line order in `BuildUI` tail |
| AC-04 | `BuildModeRow(root)` is called inside `BuildCopierSection`, between `_copierCollapseBtn` add and `_followerScrollViewer` add | Code: `BuildModeRow(root)` present at correct position in `BuildCopierSection` body |
| AC-05 | `BuildCollapsibleHeader(root)` is called after `root.Children.Add(_statusText)` in `BuildUI` tail | Code: confirmed by line order |
| AC-06 | `root.Children.Add(_contentPanel)` is the last child add in `BuildUI` tail | Code: confirmed by line order |
| AC-07 | No `BuildModeRow(root)` call exists at root level in `BuildUI` (lines 706-707 deleted) | `grep "BuildModeRow"` in `BuildUI` body → zero matches; single match only inside `BuildCopierSection` |
| AC-08 | Separator border (lines 709-713) is deleted | `grep "sep = new Border"` in deleted region → zero matches |
| AC-09 | `PttBuild.Tag` reads `"PTT-COPIER B49 | layout-reorder | 2026-08-08"` | `grep "PTT-COPIER B49"` → one match in `CopyEngine.cs:41` |
| AC-10 | No logic changes in any method | `git diff src/PropTraderTools/` shows only line moves + Tag update |
| AC-11 | Build compiles without errors | `dotnet build` exits 0 (DW-B48-01 exempt if pre-existing) |

---

### Seven-Scan Checklist (SCAN-01 through SCAN-07) — Engineer Contract

Every scan must be run and its result recorded before marking T1 complete.

| Scan | Rule | Command | Expected Result |
|------|------|---------|-----------------|
| SCAN-01 | JS-021 — No `lock()` | `grep -n "lock(" TradeCopierPanel.cs CopyEngine.cs` | Zero matches in any changed or new region |
| SCAN-02 | JS-033 — No `async void` | `grep -n "async void" TradeCopierPanel.cs` | Zero new `async void` declarations introduced by B49 |
| SCAN-03 | JS-002 — No `return null` | `grep -n "return null" TradeCopierPanel.cs` | Zero new `return null` occurrences in changed methods; pre-existing occurrences elsewhere are exempt |
| SCAN-04 | Hard-link integrity | `powershell -File scripts\verify_links.ps1` (no `-Fix`) | `DESYNC=0 MISSING=0` |
| SCAN-05 | Build gate | `dotnet build` from Wave workspace root | 0 errors (DW-B48-01 test project compile failures are exempt if pre-existing) |
| SCAN-06 | CYC — `BuildCopierSection` | Count branches: `if`, `for`, `while`, `switch`, `?:`, `??`, `&&`, `\|\|` in method body | CYC = 1 (unchanged; `BuildModeRow` call is not a branch) |
| SCAN-07 | CYC — `BuildUI` | Count branches in `BuildUI` body after all deletions and tail reorder | CYC unchanged, remains <= 8 (no new conditional branches added) |

**SCAN-06/07 pass criterion**: CYC is measured by counting decision points (each `if`, `else if`,
`for`, `foreach`, `while`, `case`, `?:`, `??`, `&&`, `||` adds 1). A method call such as
`BuildModeRow(root)` contributes 0 to CYC of the caller. Both methods must remain <= 8.

---

### Deferred Items Carried Forward (unchanged from B48)

| ID | Source Block | Description | Status |
|----|-------------|-------------|--------|
| DW-B48-01 | B48 | `CopyEngineTests.cs` — 60 compilation errors in test project | OPEN — out of scope for B49 |
| DW-B46-01 | B46 | Live F5 verification of full panel (requires running NinjaTrader instance) | OPEN — out of scope for B49 |
| DW-B42-02 | B42 | BE ALL / Quick ALL live verify (requires open position) | OPEN — out of scope for B49 |

---

### Hard-Link Sync (mandatory after any `.cs` edit)

After all `.cs` changes are complete and `dotnet build` passes:

```powershell
powershell -File scripts\verify_links.ps1 -Fix
```

This syncs the Wave workspace hard-linked files. Must report `DESYNC=0 MISSING=0` after `-Fix`.

---

*End of T1 — B49-T1*
