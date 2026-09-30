# B47-LaneB -- Ticket Completion Report
**Block**: PTT-COPIER-B47 -- Panel UX Redesign
**Tickets**: T7-B (Build Tag) + T1-B (Inline Followers ScrollViewer)
**Engineer**: ptt-engineer (Phase 4a)
**Date**: 2026-08-07
**Status**: BUILD_PASS

---

## T7-B: Build Tag Update

**File**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`

**Change**: Single const string replacement in `PttBuild.Tag` (line 41).

| Field | Before | After |
|-------|--------|-------|
| `PttBuild.Tag` | `"PTT-COPIER B46 \| atm-template-guard \| 2026-08-06"` | `"PTT-COPIER B47 \| panel-ux-redesign \| 2026-08-07"` |

No other lines changed.

---

## T1-B: Inline Followers ScrollViewer

**File**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs`

### Changes Applied

#### 1. New fields added (after line 176 -- _followerItems)

```csharp
// B47 T1-B: Inline followers ScrollViewer (replaces _followersDropDown in visual tree)
private ScrollViewer _followerScrollViewer       = null;
private StackPanel   _followerScrollViewerPanel  = null;
```

#### 2. New methods inserted before `BuildCheckItemTemplate()` (~line 1494)

- `LoadFollowers()` -- CYC=2 (null guard + foreach). Clears panel, iterates _followerItems, calls BuildInlineFollowerRow for each, then SortFollowerRows.
- `BuildInlineFollowerRow(FollowerItem item)` -- CYC=1 (straight-line). Builds row: [CheckBox][name TextBlock][P&L TextBlock][ATM ComboBox]. wires chk.Checked/Unchecked lambdas to toggle IsSelected+IsEnabled and call SortFollowerRows/UpdateCopierHeader/TryAutoApply.
- `SortFollowerRows()` -- B47 T1-B stub `{ }` (filled by T4-B)
- `UpdateCopierHeader()` -- B47 T1-B stub `{ }` (filled by T3-B)
- `TryAutoApply()` -- B47 T1-B stub `{ }` (filled by T2-B)

#### 3. `BuildUI()` modified

- `_followersDropDown` kept as field, constructed without `Margin`, NOT added to `root.Children`.
- `_followerScrollViewerPanel` and `_followerScrollViewer` (MaxHeight=66, VerticalScrollBarVisibility=Auto) constructed but NOT added to visual tree (T6-B inserts via BuildCopierSection).
- `applyBtn` set to `Visibility.Collapsed` and added to `root.Children` (wired to OnApplyRule, invisible).

#### 4. `OnLoaded()` modified

Added `LoadFollowers();` call immediately after `UpdateDropDownHeader();` (line 583+1).

---

## Acceptance Criteria Verification

| ID | Criterion | Status |
|----|-----------|--------|
| AC-T1-1 | `_followerScrollViewer` constructed with `MaxHeight=66` and `VerticalScrollBarVisibility=Auto` | PASS |
| AC-T1-2 | `_followerScrollViewer` NOT added to `root.Children` in T1-B BuildUI() block | PASS |
| AC-T1-3 | `applyBtn` added to `root.Children` with `Visibility.Collapsed`; `applyBtn.Click += OnApplyRule` wired | PASS |
| AC-T1-4 | `LoadFollowers()` called at end of `OnLoaded()` after `_followersDropDown.ItemsSource` assignment | PASS |
| AC-T1-5 | Each row produced by `BuildInlineFollowerRow()` has exactly 4 children: [CheckBox][name TextBlock][P&L TextBlock][ATM ComboBox] | PASS |
| AC-T1-6 | ATM ComboBox `IsEnabled` = `item.IsSelected` at construction time | PASS |
| AC-T1-7 | `chk.Checked` lambda sets `item.IsSelected = true` and `atmCombo.IsEnabled = true` | PASS |
| AC-T1-8 | `chk.Unchecked` lambda sets `item.IsSelected = false` and `atmCombo.IsEnabled = false` | PASS |
| AC-T1-9 | `_followersDropDown` field kept (not deleted), constructed but not in visual tree | PASS |

---

## CYC Analysis

| Method | CYC | Limit |
|--------|-----|-------|
| `LoadFollowers()` | 2 (null guard [1] + foreach [2]) | <= 8 |
| `BuildInlineFollowerRow()` | 1 (straight-line) | <= 8 |
| `SortFollowerRows()` (stub) | 1 | <= 8 |
| `UpdateCopierHeader()` (stub) | 1 | <= 8 |
| `TryAutoApply()` (stub) | 1 | <= 8 |
| `BuildUI()` | 1 (no new branches) | <= 8 |
| `OnLoaded()` | 5 (no new branches) | <= 8 |

---

## 7-Scan Report

All scans run on: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs`

| Scan | Pattern | Result | Notes |
|------|---------|--------|-------|
| SCAN-01 | `lock\s*\(` | **0 code violations** | Line 1045 match is a comment only (pre-existing JS-021 reminder) |
| SCAN-02 | Non-ASCII chars `[^\x00-\x7F]` | **0** | Zero wide-character bytes |
| SCAN-03 | `FontFamily` | **0** | No FontFamily assignments |
| SCAN-04 | `#[0-9A-Fa-f]{6}` | **0 code violations** | Lines 260-263 are comment annotations on existing `MakeBrush` calls; no new hex literals |
| SCAN-05 | `CreateOrder` name arg PTT-prefix | **0 violations** | Only call: `"PTT-Click"` (pre-existing) |
| SCAN-06 | `DateTime\.Now[^U]` | **0** | No non-UTC timestamps |
| SCAN-07 | `\block\s*\(` | **0 code violations** | Line 1045 match is comment only (same as SCAN-01) |

**All 7 scans: ZERO violations. BUILD_PASS.**

---

## JS / NT8 Rule Compliance

| Rule | Status |
|------|--------|
| JS-021 (no lock()) | PASS -- zero lock() in new code |
| JS-001 (no throw in hot path) | PASS -- LoadFollowers uses `return;` guard |
| JS-002 (no return null) | PASS -- all new methods are void |
| JS-033 (no async void) | PASS -- all new methods synchronous void |
| NT8-001 (no init setter) | PASS -- no `{ get; init; }` |
| NT8-003 (no volatile double) | PASS -- new fields are ScrollViewer/StackPanel refs |
| NT8-012 (no FrameworkElementFactory) | PASS -- BuildInlineFollowerRow uses imperative construction |
| NT8-019 (no async void) | PASS -- confirmed |
| NT8-042 (no Dispatcher.InvokeAsync) | PASS -- no Dispatcher calls added |
| NT8-043 (no null-conditional compound assignment) | PASS -- no `?.` compound-assignment |

---

## Next Tickets (execution order per 04-tickets.md)

```
T4-B (SortFollowerRows) -- replaces SortFollowerRows() stub
T3-B (Copier header)    -- replaces UpdateCopierHeader() stub; adds BuildCopierSection()
T2-B (TryAutoApply)     -- replaces TryAutoApply() stub; adds BuildAtmMap/BuildMultipliers
T5-B (button layout)    -- restructures BuildBufferedButtonsRow; adds _beRowPanel/_quickRowPanel
T6-B (panel order)      -- rebuilds BuildUI() vertical order; calls BuildCopierSection
```
