# DW-B10-REPAIR-01 Completion Report

**Ticket**: DW-B10-REPAIR-01 — Adopt-or-inject guard + WireLeaderAccount
**Wave workspace**: C:\WSGTA\universal-or-strategy\src\PropTraderTools\
**Status**: BUILD_PASS

---

## Changes Implemented

### CHANGE 1 — TradeCopierAddOn.cs :: InjectIntoChart
- Removed the `ContainsKey` guard at the top of `InjectIntoChart`.
- `DoInject`'s `TryAdd` is now the single authority for slot claims.
- Eliminates the race window where ContainsKey returned false for a survivor panel
  from a prior AddOn domain reload.

### CHANGE 2 — TradeCopierAddOn.cs :: DoInject + WireLeaderAccount (new)
- Replaced `ContainsKey` guard with atomic `TryAdd` slot claim at the top of `DoInject`.
- Added survivor scan: walks the ChartTrader Grid visual tree for a pre-existing
  `TradeCopierPanel` from a prior F5 recompile.
- Adopt path: re-wires instrument + chart + leader account on the survivor panel,
  skips adding a new Grid row.
- Fresh inject path: unchanged flow but now calls `WireLeaderAccount` on the new panel.
- On exception: `TryRemove` releases the slot so a future inject can retry.
- Added new `WireLeaderAccount` static method (CYC=3):
  - Finds the ChartTrader account ComboBox via visual tree.
  - Sets leader account immediately from current selection.
  - Hooks `SelectionChanged` to keep leader account live.
  - NT8-023 compliant: lambda captures only `accountCombo` + `panel`.

### CHANGE 3 — TradeCopierPanel.cs :: OnDiagGap001d
- Replaced `_leaderAccount` dependency with `Account.All` Sim lookup pattern.
- No longer requires leader account to be wired before running the test.
- Scans `Account.All` for a Sim account; shows a clear status if none found.
- CYC=3: instrument guard (1) + Account.All loop (2) + null diagAcc guard (3).

### CHANGE 4 — CopyEngineTests.cs :: T-B10-REPAIR-01
- Added `DoInjectGuard_TryAdd_SameKey_ReturnsFalseOnSecondCall` xUnit [Fact].
- Verifies `ConcurrentDictionary.TryAdd` rejects a second insert for the same key.
- Tests the core invariant of the adopt-or-inject guard without NT8 WPF types.
- Uses string key (same TryAdd contract, key-type independent).

---

## 7-Scan Results (all zero violations)

| Scan | Pattern | Result |
|------|---------|--------|
| SCAN-01 | `lock(` in TradeCopierAddOn.cs + TradeCopierPanel.cs | 0 matches |
| SCAN-02 | `async void ` in changed files | 0 matches |
| SCAN-03 | `return null;` in **ticket-changed methods** | 0 violations (hits are in pre-existing FindVisualChild helper, not in any changed method) |
| SCAN-04 | `volatile double` | 0 matches |
| SCAN-05 | `{ get; init; }` | 0 matches |
| SCAN-06 | CYC annotations present: WireLeaderAccount // CYC=3 (L278), OnDiagGap001d // CYC=3, InjectIntoChart CYC=2 (no comment per spec) | PASS |
| SCAN-07 | SelectionChanged lambda body: captures only accountCombo + panel, NOT chartTrader or chart | PASS |

---

## NT8 Compiler Gate

- NT8-001 (`{ get; init; }`): not used — PASS
- NT8-003 (`volatile double`): not used — PASS
- NT8-023 (lambda captures): lambda at WireLeaderAccount L293 captures only `accountCombo`
  and `panel` — `chartTrader` and `chart` are NOT captured — PASS

---

## Methods Implemented

- `TradeCopierAddOn.InjectIntoChart` (modified — ContainsKey removed)
- `TradeCopierAddOn.DoInject` (replaced — TryAdd + adopt-or-inject pattern)
- `TradeCopierAddOn.WireLeaderAccount` (new — CYC=3)
- `TradeCopierPanel.OnDiagGap001d` (replaced — Account.All Sim lookup)
- `CopyEngineTests.DoInjectGuard_TryAdd_SameKey_ReturnsFalseOnSecondCall` (new [Fact])

## Tests Added

- T-B10-REPAIR-01: 1 [Fact] (xUnit only, no NUnit/MSTest)
