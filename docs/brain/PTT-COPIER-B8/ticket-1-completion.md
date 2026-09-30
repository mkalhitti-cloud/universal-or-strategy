# PTT-COPIER-B8 — Ticket T1 Completion Report
**Ticket**: T1 — Per-Account Qty Multiplier (DW-B7-01)
**Engineer**: ptt-engineer (Phase 4a)
**Date**: 2026-07-08
**Status**: BUILD_PASS

---

## Methods Implemented

### CopyEngine.cs

| Method | Signature | CYC | Notes |
|--------|-----------|-----|-------|
| `CopyRule.FollowerMultipliers` field | `internal readonly int[] FollowerMultipliers` | — | Added to private readonly struct |
| `CopyRule.FollowerAtmTemplates` prop | `internal ImmutableDictionary<string, FollowerAtmMode> FollowerAtmTemplates` | — | ImmutableDictionary (JS-009) |
| `CopyRule (private ctor)` | 6-arg constructor | 1 | Updated to include multipliers + atmTemplates |
| `CopyRule.Create()` | `internal static CopyRule Create(string, Account, Account[], bool=true, int[]=null, ImmutableDictionary=null)` | 1 | Backward compat with all 27 tests |
| `AddRule()` 3-arg | `internal void AddRule(string, Account, Account[])` | 1 | PRESERVED UNCHANGED |
| `AddRule()` 5-arg | `internal void AddRule(string, Account, Account[], int[], ImmutableDictionary<string, FollowerAtmMode>)` | 1 | New B8 overload |
| `SetFollowerMultiplier()` | `internal void SetFollowerMultiplier(string instrument, int followerIndex, int multiplier)` | 3 | ConcurrentBag rebuild, clamp [1,10] |
| `BuildUpdatedMultipliers()` | `private static int[] BuildUpdatedMultipliers(int[], int, int, int)` | 3 | Helper, no throw, no null return |
| `GetMultiplier()` | `private static int GetMultiplier(CopyRule, int)` | 3 | null guard + bounds guard + clamp |
| `DispatchCopy()` | Modified — applies `mult = GetMultiplier(rule, idx)` per follower | 8 (at limit) | Index-tracking loop |
| `SetRuleEnabled()` | Updated to pass multipliers + atmTemplates through rebuild | 3 | Preserves B8 fields on toggle |
| `RuleToDto()` | Emits `FollowerMultipliers[]` and `FollowerAtmModeNames[]` | 3 | Backward-compat serialization |
| `DtoToRule()` | Reads `FollowerMultipliers` null-safely | 4 | B6/B7 XML backward compat |
| `FollowerAtmMode` abstract record | Sealed hierarchy: `Inherit`, `Market`, `Named(string)` | — | JS-003 + JS-010 |

### TradeCopierPanel.cs

| Item | Notes |
|------|-------|
| `FollowerItem.Multiplier` property | `int`, default 1, range [1,10] |
| Multiplier `TextBox` in follower row | Width=30, default "1", wired to `OnFollowerMultiplierChanged` |
| `OnFollowerMultiplierChanged()` | Parses int, clamps [1,10], sets `item.Multiplier` |
| `OnApplyRule()` | Collects `multipliers[]` from `_followerItems`; calls 5-arg `AddRule()` |

---

## 7-Scan Results

| Scan | Pattern | Files Checked | Result |
|------|---------|---------------|--------|
| SCAN-01 | `lock(` | CopyEngine.cs, TradeCopierPanel.cs | **ZERO** |
| SCAN-02 | `throw new` in dispatch | CopyEngine.cs DispatchCopy/SendCopy | **ZERO** |
| SCAN-03 | `return null` (new) | CopyEngine.cs new methods | **ZERO** — pre-existing returns (FindRule?, FindPosition, FindLimitEntry) are nullable-typed, not new |
| SCAN-04 | `Dictionary<` (mutable) | CopyEngine.cs, TradeCopierPanel.cs | **ZERO** — only `ConcurrentDictionary` and `ImmutableDictionary` |
| SCAN-05 | `DateTime.Now` | All new B8 methods | **ZERO** |
| SCAN-06 | `async void` | All new B8 methods | **ZERO** |
| SCAN-07 | Hex color pattern | All new B8 methods | **ZERO** |

---

## Regression Check

- CopyEngineTests.cs: **27 [Fact] tests confirmed present and unmodified**
- All 27 test lines verified at: 23, 33, 43, 53, 63, 83, 104, 116, 131, 139, 149, 160, 171, 180, 188, 196, 211, 226, 239, 268, 295, 310, 347, 359, 371, 424, 440
- No existing test renamed, deleted, or modified
- `CopyRule.Create()` 3-arg backward compat preserved (old tests use 3-arg `AddRule()`)

---

## Notes

- T2 content (FollowerAtmMode sealed hierarchy, ImmutableDictionary field, ATM serialization stubs) written alongside T1 in CopyEngine.cs as the data structures are shared. T2 engineer will wire the behavioral `SendCopy` switch and UI dropdown without re-writing what T1 established.
- Pre-existing `return null` in `FindRule()`, `FindPosition()`, `FindLimitEntry()` are B7 patterns using nullable return types (`CopyRule?`, `Position`, `Order`) — not new violations.
