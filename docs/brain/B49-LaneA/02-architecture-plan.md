# B49-LaneA Architecture Plan
**Block**: PTT-COPIER-B49  
**Lane**: A  
**Label**: layout-reorder  
**Date**: 2026-08-08  
**Status**: REVIEW_PENDING  
**Architect**: ptt-architect  

---

## §1 Block Summary

B49 is a **UI-only panel layout reorder** in `TradeCopierPanel.cs`.  
Zero logic changes. Zero new methods. Zero method signature changes.  
The only observable effects are visual ordering of widgets in the NinjaTrader add-on panel.

**Mission**: Move `_beRowPanel` and `_quickRowPanel` above the Copier section, move
the Mode row inside `BuildCopierSection`, and move `BuildCollapsibleHeader` / `_contentPanel`
to the bottom of the panel. Update `PttBuild.Tag` in `CopyEngine.cs` to reflect B49.

**Spec requirement IDs covered**: B49-UI-01 through B49-UI-04 (all layout-reorder requirements).

---

## §2 Scope

Exactly **2 files** are in scope. No other files may be touched.

| # | File | Path |
|---|------|------|
| 1 | `TradeCopierPanel.cs` | `src/PropTraderTools/TradeCopierPanel.cs` |
| 2 | `CopyEngine.cs` | `src/PropTraderTools/CopyEngine.cs` |

Wave workspace root: `C:\WSGTA\universal-or-strategy\src\PropTraderTools\`

---

## §3 Current vs Target Layout Diagram

### Current Panel Order (top → bottom)

```
[applyBtn (hidden)]          ← line 704
[BuildModeRow]               ← lines 706-707
[sep border]                 ← lines 709-713
[BuildCollapsibleHeader]     ← line 716  ("v Position Tools" header)
[_contentPanel]              ← line 769  (Risk$/ATR, ClickTrader, Tighten)
[BuildCopierSection]         ← line 770  (_copierCollapseBtn + _followerScrollViewer)
[_statusText]                ← line 771
[_beRowPanel]                ← line 772
[_quickRowPanel]             ← line 773
```

### Target Panel Order (top → bottom)

```
[applyBtn (hidden)]          ← unchanged, line 704
[_beRowPanel]                ← moved UP (was line 772)
[_quickRowPanel]             ← moved UP (was line 773)
[BuildCopierSection]         ← second slot (was line 770)
  └─ _copierCollapseBtn
  └─ BuildModeRow            ← NEW insertion inside BuildCopierSection
  └─ _followerScrollViewer
[_statusText]                ← unchanged relative position after Copier
[BuildCollapsibleHeader]     ← moved DOWN (was line 716)
[_contentPanel]              ← follows its header (was line 769)
```

**Visual delta**: BE/Quick buttons rise to top of panel. Mode row descends into the Copier
sub-section. Position Tools header + content move to the bottom of the panel.

---

## §4 Change 1 — BuildUI Tail Reorder

**File**: `TradeCopierPanel.cs`

### 4a. Lines to REMOVE

| Lines | Content | Action |
|-------|---------|--------|
| 706–707 | `// B47 T6-B: Mode row...` + `BuildModeRow(root);` | **Delete** |
| 709–713 | `// --- Separator ---` + `var sep = ...` + `sep.SetResourceReference(...)` + `sep.BorderThickness = ...` + `root.Children.Add(sep);` | **Delete** |
| 715–716 | `// B12 T2: Collapse header row...` + `BuildCollapsibleHeader(root);` | **Delete** |

### 4b. Tail replacement (lines 769–773)

**Current** (verbatim, lines 769–773):
```csharp
            root.Children.Add(_contentPanel);
            BuildCopierSection(root);        // B47 T3-B: "v Copier" header + _followerScrollViewer
            root.Children.Add(_statusText);  // B47 T6-B: status below Copier
            root.Children.Add(_beRowPanel);  // B47 T5-B/T6-B: BE | BE ALL
            root.Children.Add(_quickRowPanel); // B47 T5-B/T6-B: Quick | Quick ALL
```

**Target** (exact replacement):
```csharp
            root.Children.Add(_beRowPanel);    // B49: buttons first
            root.Children.Add(_quickRowPanel); // B49: buttons first
            BuildCopierSection(root);          // B49: Copier second (Mode row now inside)
            root.Children.Add(_statusText);    // B49: status below Copier
            BuildCollapsibleHeader(root);      // B49: Position Tools moved to bottom
            root.Children.Add(_contentPanel);  // B49: follows its header
```

**Net result**: 6 lines in, 5 lines out (the three deletions in §4a reduce total line count by 8;
the tail swap is a 5-for-5 replacement shifting the net file length down by ~8 lines).

---

## §5 Change 2 — BuildCopierSection Mode Row Insertion

**File**: `TradeCopierPanel.cs`  
**Method**: `BuildCopierSection(StackPanel root)` at approx. line 1691

### Current body (lines 1691–1703, verbatim):
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
            root.Children.Add(_followerScrollViewer);  // sole visual tree insertion point for _followerScrollViewer
        }
```

### Target body (exact replacement):
```csharp
        // B49: Mode row moved inside Copier section (between collapse btn and scroll viewer).
        // CYC=1: straight-line construction (unchanged from B47).
        // JS-021: no lock. NT8-019: no async void.
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
            BuildModeRow(root);                        // B49: Mode row between header and scroll viewer
            root.Children.Add(_followerScrollViewer);  // sole visual tree insertion point for _followerScrollViewer
        }
```

**Change**: Insert one `BuildModeRow(root);` call (+ comment) between the
`root.Children.Add(_copierCollapseBtn)` line and the `root.Children.Add(_followerScrollViewer)` line.
No other lines change. CYC remains 1.

---

## §6 Change 3 — PttBuild.Tag Update

**File**: `CopyEngine.cs`  
**Line**: 41  

**Current** (verbatim):
```csharp
        internal const string Tag = "PTT-COPIER B47 | panel-ux-redesign | 2026-08-07";
```

**Target** (exact replacement):
```csharp
        internal const string Tag = "PTT-COPIER B49 | layout-reorder | 2026-08-08";
```

**Scope**: Single string literal. No logic, no type, no dependency changes.

---

## §7 Acceptance Criteria

| ID | Description | Pass Condition |
|----|-------------|----------------|
| AC-01 | `_beRowPanel` appears above `BuildCopierSection` in panel | Visual: BE row is first child after applyBtn |
| AC-02 | `_quickRowPanel` appears above `BuildCopierSection` in panel | Visual: Quick row is second child after BE row |
| AC-03 | `BuildCopierSection` is called after `_beRowPanel` and `_quickRowPanel` are added | Code: lines 769-774 in target order |
| AC-04 | Mode row (`BuildModeRow`) is inside `BuildCopierSection`, between collapse btn and scroll viewer | Code: `BuildModeRow(root)` at correct position in method body |
| AC-05 | `BuildCollapsibleHeader` is called after `_statusText` in `BuildUI` tail | Code: `BuildCollapsibleHeader(root)` after `root.Children.Add(_statusText)` |
| AC-06 | `_contentPanel` is added after `BuildCollapsibleHeader` in `BuildUI` tail | Code: `root.Children.Add(_contentPanel)` is last child add |
| AC-07 | No `BuildModeRow(root)` call exists at root level in `BuildUI` (lines 706-707 deleted) | `grep "BuildModeRow" BuildUI body` → only result is inside `BuildCopierSection` |
| AC-08 | Separator border (lines 709-713) is deleted | `grep "sep = new Border"` → zero matches in this region |
| AC-09 | `PttBuild.Tag` reads `"PTT-COPIER B49 \| layout-reorder \| 2026-08-08"` | `grep "PTT-COPIER B49"` → one match in `CopyEngine.cs:41` |
| AC-10 | No logic changes in any method | `git diff src/PropTraderTools/` shows only line moves + Tag update |
| AC-11 | Build compiles without errors | `dotnet build` exits 0 |

---

## §8 Seven-Scan Checklist (Engineer Contract)

| Scan | Rule | Check | Expected Result |
|------|------|-------|-----------------|
| SCAN-01 | JS-021 — No lock() | `grep -n "lock(" TradeCopierPanel.cs CopyEngine.cs` | Zero matches in changed regions |
| SCAN-02 | JS-033 — No async void | `grep -n "async void" TradeCopierPanel.cs` | Zero new async void methods |
| SCAN-03 | JS-002 — No return null | `grep -n "return null" TradeCopierPanel.cs` | No new return null in changed methods |
| SCAN-04 | ASCII-only identifiers | `grep -Pn "[^\x00-\x7F]" TradeCopierPanel.cs` | Zero non-ASCII outside existing `\u25BC`/`\u25B6` literals |
| SCAN-05 | DateTime.UtcNow only | `grep -n "DateTime.Now[^U]" TradeCopierPanel.cs` | Zero matches |
| SCAN-06 | CYC ≤ 8 — BuildUI | Count branches in `BuildUI` after edit | CYC unchanged (no new branches added) |
| SCAN-07 | CYC ≤ 8 — BuildCopierSection | Count branches in `BuildCopierSection` after edit | CYC = 1 (straight-line, `BuildModeRow` is a call not a branch) |

---

## §9 Deferred Items Carried Forward

The following items are carried from B48 **unchanged**. B49 does not close, modify, or
reference any of them. They are reproduced here verbatim for continuity.

| ID | Source Block | Description | Status |
|----|-------------|-------------|--------|
| DW-B48-01 | B48 | `CopyEngineTests.cs` — 60 compilation errors in test project | OPEN — out of scope |
| DW-B46-01 | B46 | Live F5 verification of full panel (requires running NinjaTrader instance) | OPEN — out of scope |
| DW-B42-02 | B42 | BE ALL / Quick ALL live verify (requires open position) | OPEN — out of scope |

---

## §10 CYC Analysis

### BuildUI (TradeCopierPanel.cs)

`BuildUI` is a construction method; its CYC is driven by helper method calls, not conditional
branches in the tail being modified. The three deletions (lines 706-707, 709-713, 715-716) and
the tail reorder (lines 769-773) add **zero new conditional branches**. They remove two
non-branching statements and reorder five non-branching `root.Children.Add` calls.

**CYC delta**: 0. Method remains ≤ 8.

### BuildCopierSection (TradeCopierPanel.cs)

The current body (lines 1691–1703) is straight-line construction — **CYC = 1**.  
The B49 change inserts one additional straight-line call `BuildModeRow(root)` — no `if`, `for`,
`while`, `switch`, `?:`, `??`, `&&`, `||` added.

**CYC after B49**: 1 (unchanged). Trivially ≤ 8.

---

## Status

**REVIEW_PENDING** — ready for ptt-plan-reviewer.
