# B75-LaneB Tickets

**Status**: TICKETS_COMPLETE  
**Phase**: 3 (Ticket Generation)  
**Lane**: B (Panel-side: TradeCopierPanel.cs + CopyEngine.cs)  
**Date**: 2026-08-17  
**Plan source**: `docs/brain/B75-LaneB/02-architecture-plan.md` (REVIEW_PASS)  
**Test file to create**: `tests/PropTraderTools.Tests/TradeCopierPanelB75Tests.cs`  
**Test class**: `TradeCopierPanelB75Tests`  
**Framework**: xUnit `[Fact]` ONLY — no NUnit, no MSTest (JS-051..065)

---

## Ticket Index

| ID | Hotfix | Method Under Test | File Under Test | Test Name |
|----|--------|-------------------|-----------------|-----------|
| T_B66TPL_01 | HOTFIX-B66-ATM-TPL | `GetLeaderAtmTemplateName` | TradeCopierPanel.cs | `T_B66TPL_01_NullChart_ReturnsEmpty` |
| T_B66TPL_02 | HOTFIX-B66-ATM-TPL | `GetLeaderAtmTemplateName` | TradeCopierPanel.cs | `T_B66TPL_02_NullChart_NoChartTrader_ReturnsEmpty` |
| T_B66TPL_03 | HOTFIX-B66-ATM-TPL | `GetLeaderAtmTemplateName` | TradeCopierPanel.cs | `T_B66TPL_03_PrimaryPath_AtmStrategyNonNull_ReturnsName` |
| T_B66TPL_04 | HOTFIX-B66-ATM-TPL | `GetLeaderAtmTemplateName` | TradeCopierPanel.cs | `T_B66TPL_04_Fallback1_AtmStrategySelectorFound_ReturnsName` |
| T_B66TPL_05 | HOTFIX-B66-ATM-TPL | `GetLeaderAtmTemplateName` | TradeCopierPanel.cs | `T_B66TPL_05_AllPathsNull_ReturnsEmpty` |
| T_B66OBJ_P01 | HOTFIX-B66-ATM-OBJ | `SetCloneAtmObjectCache` / `GetCloneAtmMode` | CopyEngine.cs | `T_B66OBJ_P01_SetNonNull_GetCloneAtmMode_ReturnsNamedWithObject` |
| T_B66OBJ_P02 | HOTFIX-B66-ATM-OBJ | `SetCloneAtmObjectCache` / `GetCloneAtmMode` | CopyEngine.cs | `T_B66OBJ_P02_SetNull_GetCloneAtmMode_ReturnsInherit` |
| T_B67_01 | HOTFIX-B67-CHECKBOX-RESTORE | `GetSavedFollowerNames` | CopyEngine.cs | `T_B67_01_MatchingRule_ReturnsBothFollowerNames` |
| T_B67_02 | HOTFIX-B67-CHECKBOX-RESTORE | `GetSavedFollowerNames` | CopyEngine.cs | `T_B67_02_NoMatchingRule_ReturnsEmptyHashSet` |
| T_B67_03 | HOTFIX-B67-CHECKBOX-RESTORE | `GetSavedFollowerNames` + `_followerItems` IsSelected logic | CopyEngine.cs + TradeCopierPanel.cs | `T_B67_03_RestoreBlock_OnlyMatchingItemsChecked` |

---

## Ticket T_B66TPL_01

**Ticket ID**: T_B66TPL_01  
**Hotfix ref**: HOTFIX-B66-ATM-TPL  
**Spec requirement**: `GetLeaderAtmTemplateName` MUST return `string.Empty` (not null, not throw) when the `currentChart` argument is null — the Guard-1 null-check path.  
**Method under test**: `GetLeaderAtmTemplateName`  
**Signature**: `internal static string GetLeaderAtmTemplateName(Chart currentChart)` — TradeCopierPanel.cs line 2218  
**File under test**: `src/PropTraderTools/TradeCopierPanel.cs`  
**Test type**: xUnit `[Fact]`  
**Test class**: `TradeCopierPanelB75Tests`  
**Test name**: `T_B66TPL_01_NullChart_ReturnsEmpty`

**Arrange**:
- No setup required. The null-guard branch fires before any visual-tree access, so no WPF host is needed.
- `GetLeaderAtmTemplateName` is `internal static` — accessible from the test assembly via `[assembly: InternalsVisibleTo("PropTraderTools.Tests")]` in `PropTraderTools.csproj` (must be confirmed present before implementation).

**Act**:
```
string result = TradeCopierPanel.GetLeaderAtmTemplateName(null);
```

**Assert**:
- `Assert.Equal(string.Empty, result)` — returns `string.Empty`, not `null`, not any other value.
- (Optional defensive) `Assert.NotNull(result)` — documents that null is never returned.

**NT8 constraint note**: NT8-HOST-NOT-REQUIRED. The null guard at line 2220 (`if (currentChart == null) return string.Empty`) fires before any NT8 visual-tree call. This test exercises purely managed C# logic. No WPF dispatcher, no Chart object, no ChartTrader lookup is needed.

**Source line reference**: TradeCopierPanel.cs line 2220 — `if (currentChart == null) return string.Empty;`

**7-scan checklist**:
- [ ] `lock()` scan: no new `lock(` in test code
- [ ] `throw new` scan: no `throw new` in test code
- [ ] `return null` scan: no `return null` in test method
- [ ] `async void` scan: no `async void` test method
- [ ] CYC<=8: test method is straight-line Arrange/Act/Assert — zero branches
- [ ] ASCII: all string literals in test are ASCII-only (`string.Empty`, no Unicode)
- [ ] NT8 constraint: `GetLeaderAtmTemplateName(null)` executes without NT8 host — Guard-1 returns before any NT8 API call

---

## Ticket T_B66TPL_02

**Ticket ID**: T_B66TPL_02  
**Hotfix ref**: HOTFIX-B66-ATM-TPL  
**Spec requirement**: `GetLeaderAtmTemplateName` MUST return `string.Empty` without throwing when called with a `null` argument — the Guard-1 null-guard covers the "chart with no ChartTrader child" scenario for unit-test purposes; the Guard-2 branch (ChartTrader not found in visual tree) requires an NT8 host and is documented as a skip-annotated integration test skeleton.  
**Method under test**: `GetLeaderAtmTemplateName`  
**Signature**: `internal static string GetLeaderAtmTemplateName(Chart currentChart)` — TradeCopierPanel.cs line 2218  
**File under test**: `src/PropTraderTools/TradeCopierPanel.cs`  
**Test type**: xUnit `[Fact]` (unit portion) + `[Fact(Skip="NT8-HOST-REQUIRED")]` (integration skeleton)  
**Test class**: `TradeCopierPanelB75Tests`  
**Test name**: `T_B66TPL_02_NullChart_NoChartTrader_ReturnsEmpty`

**Arrange**:
- Unit portion: pass `null` as `currentChart`. Guard-1 fires, Guard-2 is not reached. No NT8 host needed.
- Integration skeleton (skip-annotated): arrange a real `Chart` object with no `ChartTrader` child in its visual tree. This requires NT8 host — mark `[Fact(Skip="NT8-HOST-REQUIRED")]`.

**Act**:
- Unit portion: `string result = TradeCopierPanel.GetLeaderAtmTemplateName(null);`
- Integration skeleton (skip body, documents intent): `string result = TradeCopierPanel.GetLeaderAtmTemplateName(realChartWithNoChartTrader);`

**Assert**:
- Unit portion: `Assert.Equal(string.Empty, result)` — Guard-1 path returns empty.
- Integration skeleton assert (documents intent): `Assert.Equal(string.Empty, result)` — Guard-2 path (ChartTrader null) returns empty.

**NT8 constraint note**: NT8-HOST-REQUIRED for the Guard-2 branch (FindVisualChild<ChartTrader> returns null). The unit-test `[Fact]` covers the Guard-1 path via `null` input. The Guard-2 path is documented as `[Fact(Skip="NT8-HOST-REQUIRED")]` with a body that states the arrange/assert intent for future integration coverage.

**Source line reference**: TradeCopierPanel.cs line 2220 (Guard-1), line 2224 (Guard-2).

**7-scan checklist**:
- [ ] `lock()` scan: no new `lock(` in test code
- [ ] `throw new` scan: no `throw new` in test code
- [ ] `return null` scan: no `return null` in test method
- [ ] `async void` scan: no `async void` test method
- [ ] CYC<=8: test method is straight-line — zero branches; skip-annotated skeleton is also straight-line
- [ ] ASCII: all string literals in test are ASCII-only
- [ ] NT8 constraint: unit `[Fact]` uses `null` input — no NT8 host needed; Guard-2 integration skeleton annotated `Skip="NT8-HOST-REQUIRED"`

---

## Ticket T_B66TPL_03

**Ticket ID**: T_B66TPL_03  
**Hotfix ref**: HOTFIX-B66-ATM-TPL  
**Spec requirement**: `GetLeaderAtmTemplateName` MUST return the `AtmStrategy.Name` string (e.g., `"MES $200 SL6"`) when the primary path fires — i.e., `ChartTrader.AtmStrategy != null`. This is the core post-B66 fix: bypasses the PTT-index-shifted ComboBox and reads the direct property instead.  
**Method under test**: `GetLeaderAtmTemplateName`  
**Signature**: `internal static string GetLeaderAtmTemplateName(Chart currentChart)` — TradeCopierPanel.cs line 2218  
**File under test**: `src/PropTraderTools/TradeCopierPanel.cs`  
**Test type**: `[Fact(Skip="NT8-HOST-REQUIRED")]` — integration test skeleton  
**Test class**: `TradeCopierPanelB75Tests`  
**Test name**: `T_B66TPL_03_PrimaryPath_AtmStrategyNonNull_ReturnsName`

**Arrange** (integration skeleton body — documents intent):
- Obtain a real `Chart` instance with a `ChartTrader` child in its WPF visual tree.
- Set `ChartTrader.AtmStrategy` to a real `NinjaTrader.NinjaScript.AtmStrategy` object whose `.Name` property returns `"MES $200 SL6"`.
- This requires a running NT8 host with an active chart — mark `[Fact(Skip="NT8-HOST-REQUIRED")]`.

**Act**:
```
string result = TradeCopierPanel.GetLeaderAtmTemplateName(chartWithAtmStrategy);
```

**Assert**:
```
Assert.Equal("MES $200 SL6", result);
```

**NT8 constraint note**: NT8-HOST-REQUIRED. `FindVisualChild<ChartTrader>(currentChart)` traverses the WPF visual tree; `ct.AtmStrategy` is an NT8 API property that requires a live chart instance. No mock or stub can replace the NT8 host for this branch. The test is documented as a skip skeleton — it defines the exact arrange/assert contract for future integration test infrastructure.

**Source line reference**: TradeCopierPanel.cs lines 2227-2228 — primary path `if (ct.AtmStrategy != null) return ct.AtmStrategy.Name ?? string.Empty`.

**7-scan checklist**:
- [ ] `lock()` scan: no new `lock(` in test code
- [ ] `throw new` scan: no `throw new` in test code
- [ ] `return null` scan: no `return null` in test method
- [ ] `async void` scan: no `async void` test method
- [ ] CYC<=8: test skeleton is straight-line Arrange/Act/Assert — zero branches
- [ ] ASCII: `"MES $200 SL6"` is ASCII-only (digits, spaces, dollar sign — all ASCII)
- [ ] NT8 constraint: annotated `[Fact(Skip="NT8-HOST-REQUIRED")]`; body documents integration test intent only

---

## Ticket T_B66TPL_04

**Ticket ID**: T_B66TPL_04  
**Hotfix ref**: HOTFIX-B66-ATM-TPL  
**Spec requirement**: `GetLeaderAtmTemplateName` MUST return the `AtmStrategySelector.SelectedAtmStrategy.Name` string when the primary path is null and Fallback-1 fires — i.e., `ct.AtmStrategy == null` but an `AtmStrategySelector` control is found in the `ChartTrader` visual tree.  
**Method under test**: `GetLeaderAtmTemplateName`  
**Signature**: `internal static string GetLeaderAtmTemplateName(Chart currentChart)` — TradeCopierPanel.cs line 2218  
**File under test**: `src/PropTraderTools/TradeCopierPanel.cs`  
**Test type**: `[Fact(Skip="NT8-HOST-REQUIRED")]` — integration test skeleton  
**Test class**: `TradeCopierPanelB75Tests`  
**Test name**: `T_B66TPL_04_Fallback1_AtmStrategySelectorFound_ReturnsName`

**Arrange** (integration skeleton body — documents intent):
- Obtain a real `Chart` with `ChartTrader` in its visual tree.
- Ensure `ct.AtmStrategy` is `null` (user has "None" selected or template not loaded).
- Ensure an `AtmStrategySelector` control is present in `ct`'s visual tree, with `SelectedAtmStrategy.Name` equal to `"ATM1"`.
- Requires running NT8 host.

**Act**:
```
string result = TradeCopierPanel.GetLeaderAtmTemplateName(chartWithSelectorOnly);
```

**Assert**:
```
Assert.Equal("ATM1", result);
```

**NT8 constraint note**: NT8-HOST-REQUIRED. `FindVisualChild<AtmStrategySelector>(ct)` requires a real WPF visual tree. `SelectedAtmStrategy` is an NT8 API object. Mark `[Fact(Skip="NT8-HOST-REQUIRED")]`.

**Source line reference**: TradeCopierPanel.cs lines 2230-2232 — `FindVisualChild<NinjaTrader.Gui.NinjaScript.AtmStrategy.AtmStrategySelector>(ct)`, then `sel?.SelectedAtmStrategy != null`.

**7-scan checklist**:
- [ ] `lock()` scan: no new `lock(` in test code
- [ ] `throw new` scan: no `throw new` in test code
- [ ] `return null` scan: no `return null` in test method
- [ ] `async void` scan: no `async void` test method
- [ ] CYC<=8: test skeleton is straight-line — zero branches
- [ ] ASCII: `"ATM1"` is ASCII-only
- [ ] NT8 constraint: annotated `[Fact(Skip="NT8-HOST-REQUIRED")]`; body documents integration test intent only

---

## Ticket T_B66TPL_05

**Ticket ID**: T_B66TPL_05  
**Hotfix ref**: HOTFIX-B66-ATM-TPL  
**Spec requirement**: `GetLeaderAtmTemplateName` MUST return `string.Empty` (not null, not throw) when all fallback paths are null — no `AtmStrategy`, no `AtmStrategySelector`, no index-2 `ComboBox`. The method's return contract is "never null, never throw".  
**Method under test**: `GetLeaderAtmTemplateName`  
**Signature**: `internal static string GetLeaderAtmTemplateName(Chart currentChart)` — TradeCopierPanel.cs line 2218  
**File under test**: `src/PropTraderTools/TradeCopierPanel.cs`  
**Test type**: `[Fact(Skip="NT8-HOST-REQUIRED")]` for the fully-null visual tree path; `[Fact]` for the null-input path (already covered by T_B66TPL_01, documented here as cross-reference)  
**Test class**: `TradeCopierPanelB75Tests`  
**Test name**: `T_B66TPL_05_AllPathsNull_ReturnsEmpty`

**Arrange** (integration skeleton body — documents intent):
- Obtain a real `Chart` with a `ChartTrader` child.
- Set `ct.AtmStrategy` to null (no template active).
- Ensure no `AtmStrategySelector` is present in `ct`'s visual tree (or `SelectedAtmStrategy` is null).
- Ensure `FindVisualChildByIndex<ComboBox>(ct, 2)` returns null or has no `SelectedItem`.
- All three paths exhausted — method must reach `return atmCb?.SelectedItem as string ?? string.Empty` with a null result.
- Requires NT8 host.

**Act**:
```
string result = TradeCopierPanel.GetLeaderAtmTemplateName(chartAllPathsNull);
```

**Assert**:
```
Assert.Equal(string.Empty, result);
Assert.NotNull(result);  // contracts: never null
```

**NT8 constraint note**: NT8-HOST-REQUIRED for the full visual tree path. Mark `[Fact(Skip="NT8-HOST-REQUIRED")]`. The null-input shortcut (Guard-1 path) that also returns `string.Empty` is already verified by T_B66TPL_01 as a full `[Fact]`.

**Source line reference**: TradeCopierPanel.cs lines 2233-2237 — Fallback-2 (`FindVisualChildByIndex<ComboBox>`) and `catch` clause both return `string.Empty`.

**7-scan checklist**:
- [ ] `lock()` scan: no new `lock(` in test code
- [ ] `throw new` scan: no `throw new` in test code
- [ ] `return null` scan: no `return null` in test method
- [ ] `async void` scan: no `async void` test method
- [ ] CYC<=8: test skeleton is straight-line — zero branches
- [ ] ASCII: `string.Empty` is ASCII-only; no Unicode in test strings
- [ ] NT8 constraint: annotated `[Fact(Skip="NT8-HOST-REQUIRED")]`; null-input path covered without NT8 by T_B66TPL_01

---

## Ticket T_B66OBJ_P01

**Ticket ID**: T_B66OBJ_P01  
**Hotfix ref**: HOTFIX-B66-ATM-OBJ (panel-side)  
**Spec requirement**: After `SetCloneAtmObjectCache(nonNullAtmObject)` is called, `GetCloneAtmMode()` MUST return a `FollowerAtmMode.Named` instance where `AtmObject` is not null. This confirms the volatile field write is visible to the reader and the object-overload dispatch path is armed.  
**Method under test**: `SetCloneAtmObjectCache` + `GetCloneAtmMode`  
**Signatures**:
- `internal void SetCloneAtmObjectCache(NinjaTrader.NinjaScript.AtmStrategy atmObj)` — CopyEngine.cs line 443
- `internal FollowerAtmMode GetCloneAtmMode()` — CopyEngine.cs line 453  
**File under test**: `src/PropTraderTools/CopyEngine.cs`  
**Test type**: xUnit `[Fact]`  
**Test class**: `TradeCopierPanelB75Tests`  
**Test name**: `T_B66OBJ_P01_SetNonNull_GetCloneAtmMode_ReturnsNamedWithObject`

**Arrange**:
- Obtain `CopyEngine.Instance`.
- Reset state: call `CopyEngine.Instance.SetCloneAtmObjectCache(null)` and `CopyEngine.Instance.SetCloneAtmCache(string.Empty)` to ensure a clean baseline before this test.
- Create a stub/fake `NinjaTrader.NinjaScript.AtmStrategy` object. Because `NinjaTrader.NinjaScript.AtmStrategy` is a class in the NT8 assembly, use one of the following approaches (engineer chooses the one that compiles cleanly with the NT8 reference):
  - **Option A**: Subclass `NinjaTrader.NinjaScript.AtmStrategy` in the test assembly (if the class is not sealed) and instantiate the subclass.
  - **Option B**: Use reflection to create an instance without invoking the constructor: `System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(NinjaTrader.NinjaScript.AtmStrategy))` cast to `NinjaTrader.NinjaScript.AtmStrategy`.
  - **Option C**: If both options fail due to NT8 sealing/native wiring: mark test `[Fact(Skip="NT8-HOST-REQUIRED")]` and document the constraint. The volatile write/read is still verified by T_B66OBJ_P02 (null path).
- Call `CopyEngine.Instance.SetCloneAtmObjectCache(stubAtmObj)`.

**Act**:
```
FollowerAtmMode mode = CopyEngine.Instance.GetCloneAtmMode();
```

**Assert**:
```
Assert.IsType<FollowerAtmMode.Named>(mode);
var named = (FollowerAtmMode.Named)mode;
Assert.NotNull(named.AtmObject);
```

**NT8 constraint note**: NT8-HOST-NOT-REQUIRED for the volatile field mechanics. `SetCloneAtmObjectCache` is a single volatile-write assignment (CopyEngine.cs line 445: `_cloneAtmObject = atmObj`). `GetCloneAtmMode` reads `_cloneAtmObject` (line 455) and branches on non-null. Both are pure C# volatile field operations with no NT8 host dependency. The only potential NT8 host requirement is constructing the `AtmStrategy` stub — see Option A/B/C above. If stub creation requires NT8 host, annotate `[Fact(Skip="NT8-HOST-REQUIRED")]` and document.

**Source line references**:
- CopyEngine.cs line 445: `_cloneAtmObject = atmObj;`
- CopyEngine.cs line 455-457: `var atmObj = _cloneAtmObject; if (atmObj != null) return new FollowerAtmMode.Named(_cloneAtmCache, atmObj);`

**7-scan checklist**:
- [ ] `lock()` scan: no new `lock(` in test code
- [ ] `throw new` scan: no `throw new` in test code
- [ ] `return null` scan: no `return null` in test method
- [ ] `async void` scan: no `async void` test method
- [ ] CYC<=8: test is straight-line Arrange/Act/Assert; the `AtmStrategy` stub option selection is an engineer decision, not a branch in the test method itself
- [ ] ASCII: all test string literals are ASCII-only
- [ ] NT8 constraint: volatile field mechanics are NT8-host-independent; stub construction path documented with Options A/B/C; if host required, test is annotated `[Fact(Skip="NT8-HOST-REQUIRED")]`

---

## Ticket T_B66OBJ_P02

**Ticket ID**: T_B66OBJ_P02  
**Hotfix ref**: HOTFIX-B66-ATM-OBJ (panel-side)  
**Spec requirement**: After `SetCloneAtmObjectCache(null)` and `SetCloneAtmCache(string.Empty)` are called, `GetCloneAtmMode()` MUST return `FollowerAtmMode.Inherit` — no ATM dispatch, no object reference. Calling `SetCloneAtmObjectCache(null)` MUST NOT throw.  
**Method under test**: `SetCloneAtmObjectCache` + `GetCloneAtmMode`  
**Signatures**:
- `internal void SetCloneAtmObjectCache(NinjaTrader.NinjaScript.AtmStrategy atmObj)` — CopyEngine.cs line 443
- `internal FollowerAtmMode GetCloneAtmMode()` — CopyEngine.cs line 453  
**File under test**: `src/PropTraderTools/CopyEngine.cs`  
**Test type**: xUnit `[Fact]`  
**Test class**: `TradeCopierPanelB75Tests`  
**Test name**: `T_B66OBJ_P02_SetNull_GetCloneAtmMode_ReturnsInherit`

**Arrange**:
- Obtain `CopyEngine.Instance`.
- Call `CopyEngine.Instance.SetCloneAtmObjectCache(null)` — must not throw.
- Call `CopyEngine.Instance.SetCloneAtmCache(string.Empty)` — ensures string cache is also empty, driving the Inherit fallback.

**Act**:
```
FollowerAtmMode mode = CopyEngine.Instance.GetCloneAtmMode();
```

**Assert**:
```
Assert.IsType<FollowerAtmMode.Inherit>(mode);
```

**NT8 constraint note**: NT8-HOST-NOT-REQUIRED. `SetCloneAtmObjectCache(null)` assigns `null` to a volatile reference field (line 445) — pure C# volatile write. `GetCloneAtmMode` reads `null` from `_cloneAtmObject` (branch 1 fails), then reads `string.Empty` from `_cloneAtmCache` (branch 2 fails: `cache.Length > 0` is false), then returns `new FollowerAtmMode.Inherit()`. No NT8 API is called. This test is fully unit-testable.

**Source line references**:
- CopyEngine.cs line 445: `_cloneAtmObject = atmObj;` (null write)
- CopyEngine.cs lines 455-462: both branches fail, `return new FollowerAtmMode.Inherit()`

**7-scan checklist**:
- [ ] `lock()` scan: no new `lock(` in test code
- [ ] `throw new` scan: no `throw new` in test code
- [ ] `return null` scan: no `return null` in test method
- [ ] `async void` scan: no `async void` test method
- [ ] CYC<=8: test is straight-line — zero branches
- [ ] ASCII: all string literals are ASCII-only (`string.Empty`)
- [ ] NT8 constraint: both `SetCloneAtmObjectCache(null)` and `GetCloneAtmMode()` Inherit path are NT8-host-independent — confirmed by source inspection of CopyEngine.cs lines 443-462

---

## Ticket T_B67_01

**Ticket ID**: T_B67_01  
**Hotfix ref**: HOTFIX-B67-CHECKBOX-RESTORE  
**Spec requirement**: `GetSavedFollowerNames(instrument, masterName)` MUST return a `HashSet<string>` containing all follower account names from a matching rule when the `_rules` collection has one rule whose `Instrument` and `MasterAccount.Name` match the query arguments.  
**Method under test**: `GetSavedFollowerNames`  
**Signature**: `internal HashSet<string> GetSavedFollowerNames(string instrument, string masterName)` — CopyEngine.cs line 479  
**File under test**: `src/PropTraderTools/CopyEngine.cs`  
**Test type**: xUnit `[Fact]`  
**Test class**: `TradeCopierPanelB75Tests`  
**Test name**: `T_B67_01_MatchingRule_ReturnsBothFollowerNames`

**Arrange**:
- Obtain or construct a `CopyEngine` instance with a clean `_rules` collection.
- Add one `CopyRule` via the public or internal API with:
  - `Instrument = "MES SEP26"`
  - `MasterAccount.Name = "Sim101"`
  - `FollowerAccounts` = list containing two accounts: `Name = "Sim102"` and `Name = "Sim103"`
- Note: if `CopyEngine` has no public constructor (singleton only), use `CopyEngine.Instance` and call the internal rule-add method (confirm method name by grepping CopyEngine.cs for `AddRule` or `SaveRule`). If only `CopyEngine.Instance` is available and its `_rules` are shared state, the test MUST call `ClearRules()` (or equivalent) as teardown to avoid polluting other tests.

**Act**:
```
HashSet<string> result = CopyEngine.Instance.GetSavedFollowerNames("MES SEP26", "Sim101");
```

**Assert**:
```
Assert.NotNull(result);
Assert.Contains("Sim102", result);
Assert.Contains("Sim103", result);
```

**NT8 constraint note**: NT8-HOST-NOT-REQUIRED. `GetSavedFollowerNames` (CopyEngine.cs lines 479-489) iterates `_rules` (a `ConcurrentBag<CopyRule>`) and builds a local `HashSet<string>`. No NT8 API is called. `CopyRule` and `Account` (via `rule.MasterAccount`, `rule.FollowerAccounts`) are data model classes — constructable in test without NT8 host, provided they have accessible constructors or factory methods. If `Account` requires NT8 host to construct, annotate `[Fact(Skip="NT8-HOST-REQUIRED")]`.

**Source line references**:
- CopyEngine.cs line 479: `internal HashSet<string> GetSavedFollowerNames(string instrument, string masterName)`
- CopyEngine.cs line 484: `if (rule.Instrument != instrument || rule.MasterAccount?.Name != masterName) continue;`
- CopyEngine.cs line 485-486: inner loop adds `f.Name` to result

**7-scan checklist**:
- [ ] `lock()` scan: no new `lock(` in test code
- [ ] `throw new` scan: no `throw new` in test code
- [ ] `return null` scan: no `return null` in test method
- [ ] `async void` scan: no `async void` test method
- [ ] CYC<=8: test is straight-line — the rule population is a sequence of assignments, no branches in the test method itself
- [ ] ASCII: `"MES SEP26"`, `"Sim101"`, `"Sim102"`, `"Sim103"` are all ASCII-only
- [ ] NT8 constraint: `GetSavedFollowerNames` is NT8-host-independent; stub construction for `Account` must be confirmed; fallback annotation documented

---

## Ticket T_B67_02

**Ticket ID**: T_B67_02  
**Hotfix ref**: HOTFIX-B67-CHECKBOX-RESTORE  
**Spec requirement**: `GetSavedFollowerNames(instrument, masterName)` MUST return an empty `HashSet<string>` (not null, no throw) when `_rules` contains no rule matching the given `instrument` and `masterName`. An empty result prevents the restore block from applying incorrect selections.  
**Method under test**: `GetSavedFollowerNames`  
**Signature**: `internal HashSet<string> GetSavedFollowerNames(string instrument, string masterName)` — CopyEngine.cs line 479  
**File under test**: `src/PropTraderTools/CopyEngine.cs`  
**Test type**: xUnit `[Fact]`  
**Test class**: `TradeCopierPanelB75Tests`  
**Test name**: `T_B67_02_NoMatchingRule_ReturnsEmptyHashSet`

**Arrange**:
- Obtain `CopyEngine.Instance`.
- Ensure no rule for `"MES SEP26"` / `"Sim101"` exists in `_rules`. Either use a freshly cleared engine state or query a key that has never been used (e.g., `"INSTRUMENT_THAT_DOES_NOT_EXIST"`).
- If engine state cannot be fully cleared between tests, use an instrument key guaranteed unique to this test (e.g., `"T_B67_02_PHANTOM_INSTRUMENT"`).

**Act**:
```
HashSet<string> result = CopyEngine.Instance.GetSavedFollowerNames("T_B67_02_PHANTOM_INSTRUMENT", "Sim101");
```

**Assert**:
```
Assert.NotNull(result);
Assert.Equal(0, result.Count);
```

**NT8 constraint note**: NT8-HOST-NOT-REQUIRED. The no-match path in `GetSavedFollowerNames` iterates `_rules`, finds no matching rule (all `continue` conditions fire), and returns `result` — which is initialized as `new HashSet<string>()` (line 481). This is pure C# iteration over a `ConcurrentBag`. No NT8 API called.

**Source line references**:
- CopyEngine.cs line 481: `var result = new HashSet<string>();`
- CopyEngine.cs line 484: `if (rule.Instrument != instrument || ...) continue;` — all rules skip
- CopyEngine.cs line 488: `return result;` — returns the empty HashSet

**7-scan checklist**:
- [ ] `lock()` scan: no new `lock(` in test code
- [ ] `throw new` scan: no `throw new` in test code
- [ ] `return null` scan: no `return null` in test method
- [ ] `async void` scan: no `async void` test method
- [ ] CYC<=8: test is straight-line — zero branches
- [ ] ASCII: `"T_B67_02_PHANTOM_INSTRUMENT"`, `"Sim101"` are ASCII-only
- [ ] NT8 constraint: no-match path is fully NT8-host-independent — confirmed by CopyEngine.cs line 481-488

---

## Ticket T_B67_03

**Ticket ID**: T_B67_03
**Hotfix ref**: HOTFIX-B67-CHECKBOX-RESTORE
**Spec requirement**: HOTFIX-B67-CHECKBOX-RESTORE — the restore block in `OnLoaded` MUST correctly pre-check only the follower items that appear in the saved rule, leaving all others unchecked. After the restore sequence fires: every `_followerItems` entry whose `Account.Name` is in the saved set has `IsSelected = true`; every entry whose name is NOT in the saved set retains `IsSelected = false`.
**Method under test**: `CopyEngine.GetSavedFollowerNames` + `_followerItems` `IsSelected` state logic (the `foreach` predicate at TradeCopierPanel.cs lines 648-650)
**Signatures**:
- `internal HashSet<string> GetSavedFollowerNames(string instrument, string masterName)` — CopyEngine.cs line 479
- Restore predicate: `if (item.Account != null && saved.Contains(item.Account.Name)) item.IsSelected = true;` — TradeCopierPanel.cs line 649-650
**Files under test**: `src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/TradeCopierPanel.cs`
**Test type**: xUnit `[Fact]`
**Test class**: `TradeCopierPanelB75Tests`
**Test name**: `T_B67_03_RestoreBlock_OnlyMatchingItemsChecked`

**Arrange**:
- Obtain `CopyEngine.Instance`.
- Reset rule state: call `CopyEngine.Instance.ClearRules()` (or equivalent teardown method — confirm name by grepping CopyEngine.cs for `ClearRules` or the public rule-clear API). If no clear method exists, use a unique instrument key `"T_B67_03_INSTRUMENT"` that is guaranteed to have no prior rules.
- Add one `CopyRule` via the internal rule-add API with:
  - `Instrument = "MES SEP26"`
  - `MasterAccount.Name = "Sim101"`
  - `FollowerAccounts` containing one account: `Name = "Sim102"` (NOT "Sim103")
- Call `GetSavedFollowerNames("MES SEP26", "Sim101")` to obtain the saved set — this mirrors exactly what `OnLoaded` does at TradeCopierPanel.cs line 644.
- Create a local stand-in collection that mirrors the `_followerItems` state after `LoadFollowers()` — because `FollowerItem` is `private sealed class` inside `TradeCopierPanel`, the test cannot instantiate it directly. Instead, use two plain C# objects that expose `AccountName` (string) and `IsSelected` (bool), e.g.:
  ```
  var item102 = new { AccountName = "Sim102", IsSelected = false };
  var item103 = new { AccountName = "Sim103", IsSelected = false };
  ```
  Then simulate the restore predicate inline (mirrors TradeCopierPanel.cs lines 648-650):
  ```
  bool sim102Selected = saved.Contains(item102.AccountName);
  bool sim103Selected = saved.Contains(item103.AccountName);
  ```
  This isolates the predicate logic (`saved.Contains`) from the WPF/NT8 host dependency, matching the plan's intent that the test verifies "the same predicate in isolation".

**Act**:
```
HashSet<string> saved = CopyEngine.Instance.GetSavedFollowerNames("MES SEP26", "Sim101");
bool sim102Selected = saved.Contains("Sim102");
bool sim103Selected = saved.Contains("Sim103");
```

**Assert**:
```
Assert.True(sim102Selected,  "Sim102 is in the saved rule -- must be selected after restore");
Assert.False(sim103Selected, "Sim103 is NOT in the saved rule -- must remain unselected");
```

**NT8 constraint note**: NT8-HOST-NOT-REQUIRED. `GetSavedFollowerNames` (CopyEngine.cs lines 479-489) is pure C# iteration over `_rules` — no NT8 API called. The restore predicate (`saved.Contains(name)`) is a pure `HashSet<string>.Contains` call. The test uses a local simulation of the predicate rather than instantiating the private `FollowerItem` class, making the test fully host-independent.

**Source line references**:
- CopyEngine.cs line 479: `internal HashSet<string> GetSavedFollowerNames(string instrument, string masterName)`
- CopyEngine.cs line 484: `if (rule.Instrument != instrument || rule.MasterAccount?.Name != masterName) continue;`
- CopyEngine.cs lines 485-486: inner loop adds `f.Name` to result
- TradeCopierPanel.cs lines 642-654: the full restore block that the predicate belongs to
- TradeCopierPanel.cs line 649: `if (item.Account != null && saved.Contains(item.Account.Name))`

**7-scan checklist**:
- [ ] `lock()` scan: no new `lock(` in test code
- [ ] `throw new` scan: no `throw new` in test code
- [ ] `return null` scan: no `return null` in test method
- [ ] `async void` scan: no `async void` test method
- [ ] CYC<=8: test is straight-line Arrange/Act/Assert — zero branches; `saved.Contains` calls are expressions, not control-flow branches
- [ ] ASCII: `"MES SEP26"`, `"Sim101"`, `"Sim102"`, `"Sim103"` are all ASCII-only; all assertion message strings are ASCII-only
- [ ] NT8 constraint: `GetSavedFollowerNames` is NT8-host-independent (pure ConcurrentBag iteration); predicate simulation uses `HashSet<string>.Contains` — no NT8 API called; `[Fact]` annotation (no skip) is correct

---

## Engineer Notes (Cross-Ticket)

### InternalsVisibleTo
`GetLeaderAtmTemplateName` is `internal static` (TradeCopierPanel.cs line 2218). The test assembly `PropTraderTools.Tests` must be granted access via:
```csharp
[assembly: InternalsVisibleTo("PropTraderTools.Tests")]
```
Add this to `src/PropTraderTools/Properties/AssemblyInfo.cs` (or an existing `GlobalAssemblyInfo.cs`) if not already present. Verify before implementing T_B66TPL_01.

### CopyEngine Singleton State
`CopyEngine.Instance` is a singleton. Tests T_B66OBJ_P01, T_B66OBJ_P02, T_B67_01, T_B67_02, T_B67_03 all access the same instance. Engineers MUST ensure test isolation by resetting volatile fields and rule state between tests. Recommended pattern: call state-reset methods in `[Fact]` arrange step, or use `IDisposable` teardown in the test class constructor/dispose.

### Source Comment Correction (non-blocking, noted by reviewer)
- TradeCopierPanel.cs line 640: comment reads `// CYC cost: +0 (straight-line, no branch beyond the foreach)`. Plan and reviewer both confirm this is incorrect — actual additive CYC is +4. Engineer should correct this comment while implementing the restore block changes.
- CopyEngine.cs line 478: comment reads `// CYC=2: foreach rules(1) + foreach followers(2)`. Actual CYC is 5. Engineer should correct this comment.

### FollowerAtmMode Type Check
Tests T_B66OBJ_P01 and T_B66OBJ_P02 use `Assert.IsType<FollowerAtmMode.Named>(mode)` and `Assert.IsType<FollowerAtmMode.Inherit>(mode)`. These require that `FollowerAtmMode` is a sealed record hierarchy visible to the test assembly. Confirm `FollowerAtmMode` is `internal` or `public` and accessible from `PropTraderTools.Tests`. If `internal`, add to `InternalsVisibleTo` list.

### xUnit Framework Only
All tests MUST use xUnit `[Fact]` — no `[Test]`, no `[TestMethod]`, no `[Theory]` unless explicitly reviewing parameterized cases. Import: `using Xunit;`. No NUnit or MSTest references permitted (JS-051..065).
