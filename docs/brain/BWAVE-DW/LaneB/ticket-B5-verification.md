# Ticket B-5 Verification — BWAVE-DW LaneB

**Ticket**: B-5
**Type**: VERIFY-ONLY (no code change)
**Spec Req ID**: DW-C38-04
**Verifier**: ptt-verifier
**Date**: 2026-08-26
**Verdict**: VERIFY_PASS

---

## Scope

Independently verify that `BuildRuleRow` and `BuildDynamicRuleRow` in
`src/PropTraderTools/TradeCopierWindow.cs` add grid children in left-to-right visual
column order (cols 0 -> 1 -> 2 -> 3-7 via BuildActionButtons -> 8 -> 9 -> 10 -> 11).

No `.cs` files were modified by this ticket. All checks are read-only.

---

## Independent Check 1: Method Location

**Command run independently**:
```powershell
Select-String -Path "src/PropTraderTools/TradeCopierWindow.cs" -Pattern "private Grid BuildRuleRow|private Grid BuildDynamicRuleRow" | ForEach-Object { "Line $($_.LineNumber): $($_.Line.Trim())" }
```

**Result**:
```
Line 480: private Grid BuildRuleRow(string instrumentName)
Line 531: private Grid BuildDynamicRuleRow()
```

**Engineer reported**: BuildRuleRow at line 480, BuildDynamicRuleRow at line 531.
**Cross-check**: EXACT MATCH — no line-number drift.

---

## Independent Check 2: Children.Add Sequence in BuildRuleRow (lines 480-527)

Source read independently: `src/PropTraderTools/TradeCopierWindow.cs` lines 480-577.

### BuildRuleRow (lines 480-527)

| DOM Add Order | Line | Grid.SetColumn | Children.Add target | Column |
|---------------|------|---------------|---------------------|--------|
| 1 | 492-493 | `Grid.SetColumn(instrLabel, 0)` | `grid.Children.Add(instrLabel)` | Col 0 |
| 2 | 499-500 | `Grid.SetColumn(leaderCb, 1)` | `grid.Children.Add(leaderCb)` | Col 1 |
| 3 | 505-506 | `Grid.SetColumn(followerLb, 2)` | `grid.Children.Add(followerLb)` | Col 2 |
| 4-8 | 509 | (internal, via BuildActionButtons) | `BuildActionButtons(instrumentName, leaderCb, followerLb, atmPanel, grid)` | Cols 3-7 |
| 9 | 512-513 | `Grid.SetColumn(beCluster, 8)` | `grid.Children.Add(beCluster)` | Col 8 |
| 10 | 515-516 | `Grid.SetColumn(atmPanel, 9)` | `grid.Children.Add(atmPanel)` | Col 9 |
| 11 | 519-520 | `Grid.SetColumn(tightenCluster, 10)` | `grid.Children.Add(tightenCluster)` | Col 10 |
| 12 | 523-524 | `Grid.SetColumn(armBeCluster, 11)` | `grid.Children.Add(armBeCluster)` | Col 11 |

**Note on atmPanel construction order**: `atmPanel` is constructed at line 508 (before `beCluster`
at line 511), but its `Children.Add` call is at line 516 (after `beCluster` at line 513).
WPF tab traversal is governed by `Children.Add` order, not construction order. Tab order is
therefore Col 8 (beCluster) before Col 9 (atmPanel) — correct left-to-right sequence.

### BuildDynamicRuleRow (lines 531-577)

| DOM Add Order | Line | Grid.SetColumn | Children.Add target | Column |
|---------------|------|---------------|---------------------|--------|
| 1 | 543-544 | `Grid.SetColumn(instrTextBox, 0)` | `grid.Children.Add(instrTextBox)` | Col 0 |
| 2 | 549-550 | `Grid.SetColumn(leaderCb, 1)` | `grid.Children.Add(leaderCb)` | Col 1 |
| 3 | 555-556 | `Grid.SetColumn(followerLb, 2)` | `grid.Children.Add(followerLb)` | Col 2 |
| 4-8 | 559 | (internal, via BuildActionButtons) | `BuildActionButtons(instrTextBox, leaderCb, followerLb, atmPanel, grid)` | Cols 3-7 |
| 9 | 562-563 | `Grid.SetColumn(beCluster, 8)` | `grid.Children.Add(beCluster)` | Col 8 |
| 10 | 565-566 | `Grid.SetColumn(atmPanel, 9)` | `grid.Children.Add(atmPanel)` | Col 9 |
| 11 | 569-570 | `Grid.SetColumn(tightenCluster, 10)` | `grid.Children.Add(tightenCluster)` | Col 10 |
| 12 | 573-574 | `Grid.SetColumn(armBeCluster, 11)` | `grid.Children.Add(armBeCluster)` | Col 11 |

**Children.Add sequence confirmed**: Both methods follow column order 0 -> 1 -> 2 -> (3-7 via
BuildActionButtons) -> 8 -> 9 -> 10 -> 11. Left-to-right visual column order is correct.
WPF tab traversal will traverse left-to-right as required by DW-C38-04.

---

## Cross-Check vs Engineer (Layer 2)

| Item | Engineer (Layer 2) | Verifier (Layer 3) | Match? |
|------|--------------------|--------------------|--------|
| BuildRuleRow start line | 480 | 480 | YES |
| BuildDynamicRuleRow start line | 531 | 531 | YES |
| Col 0 add line (BuildRuleRow) | 493 | 493 | YES |
| Col 1 add line (BuildRuleRow) | 500 | 500 | YES |
| Col 2 add line (BuildRuleRow) | 506 | 506 | YES |
| Col 8 add line (BuildRuleRow) | 513 | 513 | YES |
| Col 9 add line (BuildRuleRow) | 516 | 516 | YES |
| Col 10 add line (BuildRuleRow) | 520 | 520 | YES |
| Col 11 add line (BuildRuleRow) | 524 | 524 | YES |
| Col 0 add line (BuildDynamicRuleRow) | 544 | 544 | YES |
| Col 1 add line (BuildDynamicRuleRow) | 550 | 550 | YES |
| Col 2 add line (BuildDynamicRuleRow) | 556 | 556 | YES |
| Col 8 add line (BuildDynamicRuleRow) | 563 | 563 | YES |
| Col 9 add line (BuildDynamicRuleRow) | 566 | 566 | YES |
| Col 10 add line (BuildDynamicRuleRow) | 570 | 570 | YES |
| Col 11 add line (BuildDynamicRuleRow) | 574 | 574 | YES |

All Layer 2 line numbers confirmed correct by independent Layer 3 read. Zero discrepancies.

---

## SCAN-06: Build Verification

**Command run independently**:
```powershell
dotnet build src/PropTraderTools/ 2>&1 | Select-Object -Last 15
```

**Result**:
```
Build succeeded.
    1 Warning(s)
    0 Error(s)
Time Elapsed 00:00:02.66
```

**Warning**: `B131Tests.cs(165,13): warning xUnit2004` — pre-existing, unrelated to B-5.
Not introduced by this ticket (VERIFY-ONLY, no code change). Present in engineer report too.

**SCAN-06: PASS — 0 errors, build succeeded**

---

## DNA Rule Check (Jane Street Platinum Standard)

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock()) | VERIFY-ONLY — no new code; engineer SCAN-01 confirmed 0 actual lock() calls | PASS |
| JS-033 (no async void) | VERIFY-ONLY — no new code; engineer SCAN-02 confirmed 0 actual async void | PASS |
| JS-002 (no return null) | VERIFY-ONLY — no code changed in BuildRuleRow/BuildDynamicRuleRow | N/A |
| JS-001 (no throw exception) | VERIFY-ONLY — no code changed | N/A |
| NT8: no sealed on window | Not applicable — no class-level change | N/A |
| NT8: no FontFamily= | VERIFY-ONLY — no WPF markup added | N/A |
| NT8: no #RRGGBB hex | VERIFY-ONLY — no code changed | N/A |
| NT8: no DateTime.Now | VERIFY-ONLY — no code changed | N/A |

No DNA violations found.

---

## Spec Coverage

**DW-C38-04**: Tab order in rule rows must follow left-to-right visual column order.

Confirmed: `Children.Add` sequence in both `BuildRuleRow` and `BuildDynamicRuleRow` follows
left-to-right column order 0->1->2->3-7->8->9->10->11. WPF tab traversal follows DOM insertion
order, so tab focus will traverse left-to-right. Spec requirement satisfied — no code change needed.

---

## Summary

| Check | Result |
|-------|--------|
| Check 1: Method line numbers (independent grep) | PASS — exact match with engineer report |
| Check 2: BuildRuleRow Children.Add sequence (independent read) | PASS — col 0->1->2->3-7->8->9->10->11 |
| Check 2: BuildDynamicRuleRow Children.Add sequence (independent read) | PASS — col 0->1->2->3-7->8->9->10->11 |
| SCAN-06: dotnet build | PASS — 0 errors, 1 pre-existing warning (xUnit2004) |
| Layer 2 vs Layer 3 cross-check | PASS — all 16 line numbers match exactly |
| DNA rule violations | NONE |
| Spec DW-C38-04 coverage | CONFIRMED |
| Files modified by ticket | NONE (VERIFY-ONLY) |

---

## VERDICT: VERIFY_PASS