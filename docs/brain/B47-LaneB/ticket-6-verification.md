# B47-LaneB Ticket 6 Verification
**Ticket**: T6-B -- Panel Vertical Order Restructure
**Verifier**: ptt-verifier (orchestrator direct verification)
**Date**: 2026-08-07
**Result**: VERIFICATION_PASS

---

## Acceptance Criteria

| AC | Criterion | Result | Evidence |
|----|-----------|--------|---------|
| AC-T6-1 | `BuildModeRow(root)` called with root StackPanel | PASS | Line 707: `BuildModeRow(root);` |
| AC-T6-2 | `BuildModeRow(root)` before separator and BuildCollapsibleHeader | PASS | Line 707 < sep line 710 < BuildCollapsibleHeader line 716 |
| AC-T6-3 | `_statusText` NOT added to `_contentPanel.Children` | PASS | Zero matches for `_contentPanel.Children.Add(_statusText)` -- replaced by comment at 727-728 |
| AC-T6-4 | `BuildCopierSection(root)` before `root.Children.Add(_statusText)` | PASS | Line 770 < line 771 |
| AC-T6-5 | `root.Children.Add(_beRowPanel)` after `root.Children.Add(_statusText)` | PASS | Line 772 > line 771 |
| AC-T6-6 | `root.Children.Add(_quickRowPanel)` after `root.Children.Add(_beRowPanel)` | PASS | Line 773 > line 772 |
| AC-T6-7 | `Content = root` is final assignment before UpdateButtonColors | PASS | Line 774: `Content = root;`; UpdateButtonColors at line 777 |
| AC-T6-8 | `_followerScrollViewer` added exactly once (inside BuildCopierSection) | PASS | No standalone `root.Children.Add(_followerScrollViewer)` in BuildUI; sole insertion at BuildCopierSection line 1700 |
| AC-T6-9 | `_contentPanel` still contains BuildRiskAtrRow output | PASS | `BuildRiskAtrRow(_contentPanel)` at line 767 is deepest entry in _contentPanel |

---

## Canonical Vertical Order Confirmed (BuildUI lines 667-778)

```
1.  applyBtn (Visibility.Collapsed, line 704)
2.  BuildModeRow(root) (line 707)
3.  separator Border (line 710-713)
4.  BuildCollapsibleHeader(root) -- "Position Tools" (line 716)
5.  _contentPanel:
    a. BuildBufferedButtonsRow (row1 Collapsed; _beRowPanel/_quickRowPanel NOT added here)
    b. _statusText TextBlock constructed (line 725) -- NOT added to _contentPanel
    c. BuildClickTraderRow (Collapsed)
    d. tightenRow (Collapsed)
    e. BuildRiskAtrRow
6.  root.Children.Add(_contentPanel) (line 769)
7.  BuildCopierSection(root) (line 770) -- sole insertion of _followerScrollViewer
8.  root.Children.Add(_statusText) (line 771)
9.  root.Children.Add(_beRowPanel) (line 772)
10. root.Children.Add(_quickRowPanel) (line 773)
11. Content = root (line 774)
12. UpdateButtonColors (line 777)
```

---

## Scans

| Scan | Pattern | Result |
|------|---------|--------|
| SCAN-01 | `lock(` | Zero code matches (comment-only) |
| SCAN-02 | `async void` | Zero code matches |
| SCAN-03 | `_contentPanel.Children.Add(_statusText)` | Zero matches |
| SCAN-04 | `BuildModeRow` call sites | Exactly 1 (line 707, param=root) |
| SCAN-05 | `_followerScrollViewer` in BuildUI | Zero direct root.Children.Add calls |

---

*T6-B verified by direct source read. VERIFICATION_PASS.*
