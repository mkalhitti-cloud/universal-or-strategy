# B47-LaneB Ticket 4 Completion Report

**Ticket**: T4-B — Replace SortFollowerRows() stub with real implementation  
**File**: `src/PropTraderTools/TradeCopierPanel.cs`  
**Status**: BUILD_PASS

---

## What Was Implemented

Replaced the T1-B no-op stub at line 1609:

```csharp
// B47 T1-B stub -- filled by T4-B
private void SortFollowerRows() { }
```

with the real implementation (CYC=3):

```csharp
// B47 T4-B: SortFollowerRows -- sort _followerItems and rebuild ScrollViewer panel children.
// Sort order: checked items first; within each group, alpha by account Name.
// Rebuilds _followerScrollViewerPanel.Children to match sorted _followerItems order.
// CYC=3: null guard [1] + List.Sort call [2] + foreach rebuild [3].
// JS-021: no lock. UI-thread only (called from CheckBox event handlers and LoadFollowers).
private void SortFollowerRows()
{
    if (_followerScrollViewerPanel == null) return;  // guard [1]

    _followerItems.Sort((a, b) =>                    // [2]
    {
        if (a.IsSelected != b.IsSelected)
            return a.IsSelected ? -1 : 1;  // checked first
        string nameA = a.Account != null ? a.Account.Name : string.Empty;
        string nameB = b.Account != null ? b.Account.Name : string.Empty;
        return string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase);
    });

    _followerScrollViewerPanel.Children.Clear();
    foreach (var item in _followerItems)             // [3]
        BuildInlineFollowerRow(item);
}
```

**Key design decisions**:
- `a.Account != null ? a.Account.Name : string.Empty` used instead of `a.ToString()` — safe null guard, no reliance on `FollowerItem.ToString()` override
- `List<T>.Sort(Comparison<T>)` — in-place, no allocation beyond the lambda; `_followerItems` is `private readonly List<FollowerItem>`
- UI-thread only: called from CheckBox event handlers and `LoadFollowers` — no lock required (JS-021 compliant)
- `_followerScrollViewerPanel.Children.Clear()` + `BuildInlineFollowerRow` foreach rebuilds the WPF panel to match the new sorted order

---

## 7-Scan Results

| Scan | Pattern | Result |
|------|---------|--------|
| SCAN-01 | `lock\(` in source | **0** — only in comment at line 1045 |
| SCAN-02 | Non-ASCII chars | **0** |
| SCAN-03 | `FontFamily` | **0** |
| SCAN-04 | `#[0-9A-Fa-f]{6}` | **0 violations** — 4 hits are in `//` comments only (pre-existing JS-compliant MakeBrush pattern) |
| SCAN-05 | CreateOrder `"PTT-"` prefix | **0 violations** — `"PTT-Click"` at line 1929 (pre-existing) |
| SCAN-06 | `DateTime\.Now[^U]` | **0** |
| SCAN-07 | `\block\s*\(` | **0** — only in comment at line 1045 |

All 7 scans: **zero violations**.

---

## Jane Street Compliance

- **JS-021** (no lock): Compliant — UI-thread only, no synchronization primitive needed
- **JS-023** (volatile atomic toggle): N/A
- **CYC <= 8**: Compliant — CYC=3 (null guard + Sort + foreach)
- **NT8 constraints**: No `async void`, no `DateTime.Now`, no `FontFamily`, no `#RRGGBB` literals

---

**BUILD_PASS**
