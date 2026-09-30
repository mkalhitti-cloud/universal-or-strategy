# B47-LaneB Ticket 5 Completion Report

**Ticket**: T5-B — Restructure BuildBufferedButtonsRow  
**File**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs`  
**Status**: BUILD_PASS

---

## What Was Implemented

### 1. New Fields (after `_quickT2` field block, ~line 224)
- `private UniformGrid _beRowPanel = null;` — 2-col panel: BE cluster | BE ALL cluster
- `private UniformGrid _quickRowPanel = null;` — 2-col panel: Quick cluster | Quick ALL cluster
- `private int _quickAllT1 = 4;` — independent Quick ALL tick value, default 4

### 2. BuildBufferedButtonsRow Rewrite (~lines 862–1049)
Replaced the old 3-row + standalone button layout with new structure:

| Element | Change |
|---------|--------|
| **row1 (Trim\|Flatten)** | Kept; set `Visibility = Visibility.Collapsed`. Event handlers `OnTrimClick`, `OnFlattenClick`, `OnTrimUp`, `OnTrimDown`, `OnFlattenUp`, `OnFlattenDown` preserved. Both buttons get `SetResourceReference(NTButtonStyle)`. Added to `root.Children` (in tree but invisible). |
| **_beRowPanel** | New `UniformGrid { Columns=2 }`. Holds BE cluster (with `OnBeUp`/`OnBeDown`/`OnBeClick`) and BE ALL cluster (with `OnGlobalBeUp`/`OnGlobalBeDown`/`OnGlobalBeClick`, purple border). NOT added to root here — T6-B does that. |
| **_quickRowPanel** | New `UniformGrid { Columns=2 }`. Holds Quick cluster (with `OnQuickUp`/`OnQuickDown`/`OnQuickClick`, teal border) and Quick ALL cluster (new DockPanel with `OnQuickAllUp`/`OnQuickAllDown` spinners + `OnQuickAllClick`, teal border). NOT added to root here — T6-B does that. |
| **_quickT3Row** | Unchanged: `StackPanel` with `Visibility.Collapsed`. Added to `root.Children`. |

### 3. New Event Handlers (~lines 1476–1490)
Added after `OnQuickDown`:
- `OnQuickAllUp` — increments `_quickAllT1` (max 99), updates `_quickAllBtn.Content`
- `OnQuickAllDown` — decrements `_quickAllT1` (min 1), updates `_quickAllBtn.Content`

---

## Structural Invariants Verified

- `_beRowPanel` and `_quickRowPanel`: declared as fields, assigned in `BuildBufferedButtonsRow`, **not** added to `root.Children` inside that method
- `root.Children.Add` calls inside `BuildBufferedButtonsRow`: only `row1` (collapsed) and `_quickT3Row` (collapsed)
- `Visibility.Collapsed` on row1: confirmed at instantiation
- All old event handlers preserved: `OnTrimClick`, `OnFlattenClick`, `OnBeClick`, `OnGlobalBeClick`, `OnQuickClick`, `OnQuickAllClick`

---

## 7-Scan Results

| Scan | Pattern | Result |
|------|---------|--------|
| SCAN-01 | `lock(` actual code usage | **0** (12 regex hits are all in comments saying "no lock()") |
| SCAN-02 | Non-ASCII characters | **0** |
| SCAN-03 | `FontFamily` | **0** |
| SCAN-04 | `#[0-9A-Fa-f]{6}` | **0 new** (8 existing hits are in comments, e.g. `// green #22c55e`) |
| SCAN-05 | `CreateOrder` PTT- prefix | **0 violations** (all `CreateOrder` calls in CopyEngine.cs are pre-existing, none touched by T5-B) |
| SCAN-06 | `DateTime.Now[^U]` | **0** |
| SCAN-07 | `\block\s*\(` | **0 code** (all 12 hits are comments) |

---

## Jane Street DNA Compliance

- `CYC=1` in `BuildBufferedButtonsRow` — straight-line construction, no branches
- `CYC=1` in both new handlers — single expression, no branches
- No `lock()`, no `async void`, no `throw`, no `return null`
- All new brushes use `MakeBrush(r,g,b)` (frozen `SolidColorBrush`) — JS-008 compliant
- NT8 constraints: no `sealed` on `TradeCopierWindow`, no `FontFamily`, no UTF-16
- `DateTime.UtcNow` only (no `DateTime.Now`)
