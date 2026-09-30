# Ticket DW-B10-REPAIR-01 Verification Report

**Epic**: PTT-COPIER-B10-UI-01
**Ticket**: DW-B10-REPAIR-01
**Verifier**: ptt-verifier
**Date**: 2026-07-09
**Wave workspace**: C:\WSGTA\universal-or-strategy\src\PropTraderTools\

---

## 7-SCAN RESULTS (independent, run by verifier)

| # | Pattern | Files | Matches | Result |
|---|---------|-------|---------|--------|
| SCAN-01 | lock\s*\( | TradeCopierAddOn.cs, TradeCopierPanel.cs | 0 | PASS |
| SCAN-02 | sync void  | TradeCopierAddOn.cs, TradeCopierPanel.cs | 0 | PASS |
| SCAN-03 | eturn null; | TradeCopierAddOn.cs, TradeCopierPanel.cs | 4 (all in FindVisualChild helpers — generic T return, not business logic) | PASS |
| SCAN-04 | olatile double | TradeCopierAddOn.cs, TradeCopierPanel.cs | 0 | PASS |
| SCAN-05 | { get; init; } | TradeCopierAddOn.cs, TradeCopierPanel.cs | 0 | PASS |
| SCAN-06 | ContainsKey in InjectIntoChart executable code | TradeCopierAddOn.cs | 0 (line 199 is a COMMENT only) | PASS |
| SCAN-07 | FontFamily | TradeCopierAddOn.cs, TradeCopierPanel.cs | 0 | PASS |

> SCAN-03 clarification: The 4 eturn null; hits are exclusively in the private generic helpers
> FindVisualChild<T> (lines 304, 313) and FindVisualChildByName<T> (lines 319, 328).
> These return T (a DependencyObject subtype), which is nullable by reference-type contract.
> They are NOT business-logic gate methods (OnOrderUpdate, SendCopy, DoInject).
> JS-002 is NOT violated.

> Hex colour comment note: TradeCopierPanel.cs lines 101-104 contain #RRGGBB strings in
> code COMMENTS only (e.g. // green #22c55e). The actual brush construction uses decimal RGB
> via MakeBrush(r,g,b). No hex string literals exist in executable code. SCAN-04 (hex
> colour string) passes.

---

## CHANGE 1 — InjectIntoChart (TradeCopierAddOn.cs)

**Spec requirement**: The if (_panels.ContainsKey(chart)) return; guard MUST be absent.
Method body must only contain the IsLoaded branch and OnChartLoaded hook.

**Finding**:
- ContainsKey does NOT appear in any executable line of InjectIntoChart. The word appears
  only in a comment at line 199 inside DoInject, which describes what the old guard did.
- InjectIntoChart (lines ~145-154) contains exactly:
  1. if (chart.IsLoaded) branch → Dispatcher.InvokeAsync(() => DoInject(chart))
  2. else → chart.Loaded += OnChartLoaded;
  No other statements.

**Result**: PASS

---

## CHANGE 2 — DoInject (TradeCopierAddOn.cs)

**Spec requirements**:

| Requirement | Finding | Result |
|-------------|---------|--------|
| First line: if (!_panels.TryAdd(chart, null)) return; | Line ~198: if (!_panels.TryAdd(chart, null)) return; — CONFIRMED | PASS |
| Visual tree scan loop: oreach (UIElement child in grid.Children) | Present: oreach (UIElement child in grid.Children) with if (child is TradeCopierPanel p) { existing = p; break; } | PASS |
| Adopt path calls WireLeaderAccount(chartTrader, existing) | Present in adopt branch | PASS |
| Adopt path calls StartAtrEngine | Present: StartAtrEngine(chart, chartInstr) in adopt branch | PASS |
| Fresh inject path calls WireLeaderAccount(chartTrader, panel) | Present in fresh-inject branch | PASS |
| On exception: _panels.TryRemove(chart, out _) | Present in both the chartTrader == null early return AND the catch block | PASS |
| WireLeaderAccount exists as separate private static method | Present at lines ~275-290: private static void WireLeaderAccount(ChartTrader chartTrader, TradeCopierPanel panel) | PASS |
| WireLeaderAccount has // CYC=3 comment | Present: // CYC=3: null guard (1) + SelectedItem cast (2) + SelectionChanged subscription (3) | PASS |
| Lambda in WireLeaderAccount must NOT reference chartTrader or chart | Lambda body: ar acc = accountCombo.SelectedItem as NinjaTrader.Cbi.Account; panel.SetLeaderAccount(acc); — captures only ccountCombo and panel. Neither chartTrader nor chart appear. NT8-023 COMPLIANT. | PASS |

**Result**: PASS

---

## CHANGE 3 — OnDiagGap001d (TradeCopierPanel.cs)

**Spec requirements**:

| Requirement | Finding | Result |
|-------------|---------|--------|
| Must NOT contain _leaderAccount anywhere in the method body | Searched full method body of OnDiagGap001d — _leaderAccount does not appear. | PASS |
| Must contain NinjaTrader.Cbi.Account.All loop with IndexOf("Sim"...) | Present: oreach (var a in NinjaTrader.Cbi.Account.All) with .Name.IndexOf("Sim", System.StringComparison.OrdinalIgnoreCase) >= 0 | PASS |
| Must use diagAcc as the account variable passed to RunGap001dTest | diagAcc declared, populated from the loop, null-checked, then TradeCopierAddOn.RunGap001dTest(diagAcc, _instrument) | PASS |
| CYC comment must be // CYC=3 | Present: // CYC=3: instrument guard (1) + Account.All loop (2) + null diagAcc guard (3) | PASS |

**Result**: PASS

---

## CHANGE 4 — T-B10-REPAIR-01 Test (CopyEngineTests.cs)

**Spec requirements**:

| Requirement | Finding | Result |
|-------------|---------|--------|
| Method name: DoInjectGuard_TryAdd_SameKey_ReturnsFalseOnSecondCall | Present exactly | PASS |
| [Fact] attribute | [Fact] present, xUnit | PASS |
| Assert.True(first) | Assert.True(first, "First TryAdd must succeed (slot claimed)") | PASS |
| Assert.False(second) | Assert.False(second, "Second TryAdd with same key must fail (adopt path -- no duplicate panel)") | PASS |
| Assert.Equal(1, dict.Count) | Assert.Equal(1, dict.Count) | PASS |
| xUnit only (no NUnit/MSTest) | File uses only using Xunit; — no NUnit/MSTest imports anywhere | PASS |

**Result**: PASS

---

## DNA RULE CHECK

| Rule | Check | Result |
|------|-------|--------|
| JS-021 — no lock() | 0 matches in both files | PASS |
| JS-023 — volatile bool for menu guard | _menuWired, _clickArmed, _clickBuy all declared olatile bool | PASS |
| JS-008 — SolidColorBrush.Freeze() | All brushes created via MakeBrush() which calls .Freeze() | PASS |
| JS-001 — no throw in gate methods | No 	hrow new in DoInject, OnOrderUpdate, or dispatch paths | PASS |
| JS-002 — no return null in business logic | eturn null only in generic visual-tree helpers (FindVisualChild<T>) | PASS |
| NT8-023 — lambda closure safety | WireLeaderAccount lambda captures only ccountCombo + panel | PASS |
| NT8 — no sealed on TradeCopierWindow | Not present in these files | PASS |
| NT8 — no FontFamily= | 0 matches | PASS |
| NT8 — no hex color strings in code | Hex values in comments only; executable code uses MakeBrush(r,g,b) | PASS |
| NT8 — no DateTime.Now (non-UTC) | 0 matches | PASS |
| NT8 — { get; init; } banned | 0 matches | PASS |
| NT8 — olatile double banned | 0 matches | PASS |
| CYC <= 8 | WireLeaderAccount=3, DoInject scanned (multiple branches, well under 8), OnDiagGap001d=3 | PASS |

---

## ARCHITECTURE COMPLIANCE

- ConcurrentDictionary used for _panels, _atrEngines, _clickHandlers — no plain Dictionary on shared state.
- No sync/await in OnWindowCreated, OnWindowDestroyed, or OnInitialize.
- All UI mutations go through Dispatcher.InvokeAsync.
- WireLeaderAccount correctly extracted as a separate private static method with its own CYC annotation.
- Test uses ConcurrentDictionary<string, object> as a key-type-independent proxy for the NT8-unavailable Chart type — correct test isolation strategy.

---

## VERDICT

**VERIFY_PASS**

All 4 changes implemented correctly. All 7 scans returned 0 violations. All DNA rules satisfied.
No NT8 constraints violated. Test is xUnit [Fact] with correct assertions.
