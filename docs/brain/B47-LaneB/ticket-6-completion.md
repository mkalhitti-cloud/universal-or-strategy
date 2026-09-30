# B47-LaneB T6-B -- Completion Report

**Ticket**: T6-B: Panel Vertical Order Restructure
**Epic**: B47-LaneB
**File**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs`
**Status**: BUILD_PASS
**Date**: 2026-08-07

---

## What Was Implemented

Rewrote the structural order of `BuildUI()` in [`TradeCopierPanel.cs`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs) to achieve the canonical vertical panel order specified in DW-B47-PANEL-ORDER-04.

### Changes (3 surgical diffs via apply_diff)

**Change 1 -- BuildModeRow moved to root (above separator)**

Added `BuildModeRow(root)` call immediately after `root.Children.Add(applyBtn)` and before the separator `Border`. Removed the old `BuildModeRow(_contentPanel)` call from inside `_contentPanel` construction.

Before:
```csharp
// B9 T3: Copy mode row (Signal / Mirror radio buttons)
BuildModeRow(_contentPanel);
```

After (T6-B):
```csharp
// B47 T6-B: Mode row moved to root (above Position Tools header).
BuildModeRow(root);
```

**Change 2 -- _statusText removed from _contentPanel, comment added**

Removed `_contentPanel.Children.Add(_statusText)` while keeping the `TextBlock` construction. Added comment explaining it is added to root after `BuildCopierSection`.

Before:
```csharp
_contentPanel.Children.Add(_statusText);
```

After (T6-B):
```csharp
// B47 T6-B: do NOT add _statusText to _contentPanel here.
// It is added to root after BuildCopierSection (see tail of BuildUI).
```

**Change 3 -- Tail sequence added after root.Children.Add(_contentPanel)**

Added 4-line tail between `root.Children.Add(_contentPanel)` and `Content = root`:

```csharp
root.Children.Add(_contentPanel);
BuildCopierSection(root);        // B47 T3-B: "v Copier" header + _followerScrollViewer
root.Children.Add(_statusText);  // B47 T6-B: status below Copier
root.Children.Add(_beRowPanel);  // B47 T5-B/T6-B: BE | BE ALL
root.Children.Add(_quickRowPanel); // B47 T5-B/T6-B: Quick | Quick ALL
Content = root;
```

---

## Canonical Vertical Order Confirmed (lines 667-778)

```
1.  applyBtn (Visibility.Collapsed)                  -- line 696-704
2.  BuildModeRow(root)                               -- line 707  [T6-B]
3.  separator Border                                 -- line 710-713
4.  BuildCollapsibleHeader(root)                     -- line 716
5.  _contentPanel:
    a. BuildBufferedButtonsRow(_contentPanel)        -- line 722
    b. _statusText constructed (NOT added here)      -- line 725-728
    c. BuildClickTraderRow(_contentPanel)            -- line 731
    d. tightenRow (Visibility.Collapsed)             -- line 734-764
    e. BuildRiskAtrRow(_contentPanel)                -- line 767
6.  root.Children.Add(_contentPanel)                 -- line 769
7.  BuildCopierSection(root)                         -- line 770  [T6-B tail]
8.  root.Children.Add(_statusText)                   -- line 771  [T6-B tail]
9.  root.Children.Add(_beRowPanel)                   -- line 772  [T6-B tail]
10. root.Children.Add(_quickRowPanel)                -- line 773  [T6-B tail]
11. Content = root                                   -- line 774
12. UpdateButtonColors(false, false)                 -- line 777
```

---

## Mandatory 7 Scans

| Scan | Pattern | Result | Notes |
|------|---------|--------|-------|
| SCAN-01 | `lock(` (grep) | **0** | Line 1086 is a comment only: `// JS-021: no lock()` |
| SCAN-02 | non-ASCII chars | **0** | `Get-Content ... Where-Object {$_ -match '[^\x00-\x7F]'}` -- no output |
| SCAN-03 | `FontFamily` | **0** | No output |
| SCAN-04 | `#[0-9A-Fa-f]{6}` | **0 new** | 4 pre-existing comment annotations on `MakeBrush` lines (270-273); T6-B added none |
| SCAN-05 | `CreateOrder` prefix | **0 violations** | Only call: `"PTT-Click"` at line 2094 ✓ |
| SCAN-06 | `DateTime\.Now[^U]` | **0** | No output |
| SCAN-07 | `\block\s*\(` | **0** | Line 1086 is a comment only |

---

## Bonus Scan: BuildModeRow call count

```
Select-String -Pattern "BuildModeRow" → 2 hits:
  Line  707: BuildModeRow(root);          ← single call site in BuildUI, to root ✓
  Line 1386: private void BuildModeRow(StackPanel root)  ← method definition
```

Old `BuildModeRow(_contentPanel)` call: **removed**. Exactly one call exists, targeting `root`.

---

## CYC Analysis

| Method | CYC Before | CYC After | Limit |
|--------|-----------|-----------|-------|
| `BuildUI()` | 1 | 1 | <= 8 |

Reorder only -- no new conditional branches introduced.

---

## JS Rules Compliance

| Rule | Severity | Status |
|------|----------|--------|
| JS-021 (no lock()) | P0 | PASS |
| JS-001 (no throw in hot path) | P0 | PASS -- BuildUI() is void, no throw |
| JS-002 (no return null) | P0 | PASS -- BuildUI() is void |
| JS-033 (no async void) | P0 | PASS -- BuildUI() is synchronous void |

---

## NT8 Rules Compliance

| Rule | Severity | Status |
|------|----------|--------|
| NT8-001 (no init setter) | P0 | PASS -- no new properties |
| NT8-019 (no async void) | P0 | PASS -- signature unchanged |

---

## Double-Add Prevention Verified

- `_followerScrollViewer` is NOT added to root in `BuildUI()` directly.
- It enters the visual tree exclusively via `BuildCopierSection(root)` at line 770.
- Only one `BuildCopierSection` call exists in `BuildUI()`.
- WPF `InvalidOperationException ("Element is already the child of another element")` cannot occur.

---

**BUILD_PASS**
