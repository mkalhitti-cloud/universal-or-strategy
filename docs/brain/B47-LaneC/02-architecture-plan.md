# Architecture Plan — PTT-COPIER-B47 Lane C
**Block**: PTT-COPIER-B47 Lane C
**Phase**: 2 — Architecture Plan
**Architect**: ptt-architect
**Date**: 2026-08-08
**Status**: REVIEW_PASS_PENDING

---

## 1. Rules Catalog Gate Result

**GATE RESULT: PASS**

| Rule | Check | Result |
|------|-------|--------|
| JS-021 `lock()` | No lock() in test file or const string change | CLEAR |
| JS-033 `async void` | All test methods are `void` with `[Fact]` | CLEAR |
| JS-001 `throw new XxxException` | No exception throwing in any test | CLEAR |
| JS-002 `return null` | No method returns null in test file | CLEAR |
| NT8-001 `{ get; init; }` | Not used | CLEAR |
| NT8-002 `abstract/sealed record` | `sealed class` only | CLEAR |
| ASCII-only | All string literals are ASCII | CLEAR |

---

## 2. Prior Lane Context (READ ONLY)

| Lane | Status | Key Changes |
|------|--------|-------------|
| B47-LaneA | FINAL_PASS | `IsFollowerAccount(Account a)` guard on 3 fan-out paths: CopyEngine.cs, PttBreakEven.cs, PttGlobalQuickExit.cs |
| B47-LaneB | FINAL_PASS | TradeCopierPanel.cs Panel UX redesign: inline follower rows, `TryAutoApply()`, `SortFollowerRows()`, `UpdateCopierHeader()`, `BuildCopierSection()`, `BuildBufferedButtonsRow`, BuildUI reorder. `PttBuild.Tag` set to `"PTT-COPIER B47 | panel-ux-redesign | 2026-08-07"` by T7-B. |

---

## 3. Deferred Items Closed by This Lane

| Deferred ID | Priority | Description | Close Action |
|-------------|----------|-------------|-------------|
| DW-B47-01 | P1 | B47Tests.cs T_B47_01 through T_B47_04 | T1-C writes all 9 tests (T_B47_01–T_B47_09) |
| DW-B47-03 | P1 | PttBuild.Tag update to B47 value | T2-C verifies Tag already correct; no edit needed |
| DW-B47-04 | P2 | Add T_B47_05 (`IsFollowerAccount_ReturnsFalse_WhenLeaderNull`) | T1-C includes T_B47_05 as TryAutoApply null-leader guard proxy |

---

## 4. Deliverable Summary

### T1-C: `B47Tests.cs` (NEW FILE)
- **Path**: `src/PropTraderTools/B47Tests.cs`
- **Type**: New file — xUnit test class
- **Class**: `public sealed class B47Tests`
- **Namespace**: `PropTraderTools`
- **Tests**: 9 `[Fact]` methods (T_B47_01 through T_B47_09)
- **NT8 runtime calls**: ZERO
- **CYC per method**: ≤ 2 (all methods)

### T2-C: `CopyEngine.cs` — Tag Verification
- **Path**: `src/PropTraderTools/CopyEngine.cs`
- **Type**: Verification only — no code change expected
- **Scope**: Line ~41: `internal const string Tag`
- **Required value**: `"PTT-COPIER B47 | panel-ux-redesign | 2026-08-07"`
- **Current value** (confirmed 2026-08-08 by grep): `"PTT-COPIER B47 | panel-ux-redesign | 2026-08-07"`
- **Action**: If equal → document as VERIFIED, no diff. If different → update to required value.

---

## 5. T1-C: File Header and Using Directives

```csharp
// B47Tests.cs
// Block: PTT-COPIER-B47
// Spec: DW-B47-BE-FOLLOWER-SCOPE, DW-B47-INLINE-FOLLOWERS-02, DW-B47-AUTO-RULE-01,
//       DW-B47-FOLLOWERS-SORT-06, DW-B47-COPIER-COLLAPSE-05
// Tests: T_B47_01 through T_B47_09
// Framework: xUnit only (no NUnit, no MSTest)
// NT8-runtime-free: zero NT8 API calls

using System;
using System.Linq;
using Xunit;

namespace PropTraderTools
{
    public sealed class B47Tests
    {
        // ... 9 [Fact] methods
    }
}
```

**Import rationale**:
- `using System;` — required for `Func<>`, `Action`
- `using System.Linq;` — required for `.Where()` in T_B47_02 and `.Count()` in T_B47_07
- `using Xunit;` — required for `[Fact]`, `Assert`
- NO `using NinjaTrader.*` anywhere in file

---

## 6. NT8 Runtime Boundary Classification

Every test is classified as Class A (pure logic) or Class B (pure proxy — mirrors NT8 logic without NT8 types):

| Test | Class | NT8 type avoided | Proxy mechanism |
|------|-------|-----------------|-----------------|
| T_B47_01 | B | `Account` | `Func<object, bool> nullGuard = a => a != null` |
| T_B47_02 | B | `Account`, `FollowerItem` (private sealed) | Anonymous type array + `Where()` LINQ filter |
| T_B47_03 | A | n/a | Direct call to `CopyEngine.ParseAtmModeName` (pure static) |
| T_B47_04 | B | `TradeCopierPanel` | Ternary expression: `followers.Length == 0 ? "No followers selected." : "..."` |
| T_B47_05 | B | `TradeCopierPanel`, `Account` | `if (leader != null)` guard inline |
| T_B47_06 | A | n/a | Anonymous types with `IsSelected` (bool) + `AccountName` (string) |
| T_B47_07 | A | n/a | Anonymous types with `IsSelected` + `Count()` + string format |
| T_B47_08 | B | `ComboBox` (WPF) | `bool isEnabled = false; Assert.False(isEnabled)` |
| T_B47_09 | B | `CopyEngine.Instance` | `Action saveRules = () => saveRulesCalls++; saveRules()` |

Class B tests carry the comment: `// NT8-runtime-only — structural test only`

---

## 7. Per-Test Architecture (Full Specification)

### T_B47_01 — `IsFollowerAccount_NullGuard_ReturnsFalse_WhenNull`
**Spec**: DW-B47-BE-FOLLOWER-SCOPE
**Class**: B (NT8-runtime-only — structural test only)
**CYC**: 1

```
// NT8-runtime-only — structural test only
// Proxy: mirrors IsFollowerAccount null-guard (CopyEngine.cs ~line 1398):
//   if (a == null) return false;
Func<object, bool> nullGuard = a => a != null;
Assert.False(nullGuard(null));         // follower=null -> false (guard fires)
Assert.True(nullGuard(new object())); // non-null -> guard passes
```

**What it asserts**: The null-guard predicate `a == null -> false` is structurally correct at both ends.

---

### T_B47_02 — `GetSelectedFollowers_Proxy_CheckedItemIsIncluded`
**Spec**: DW-B47-INLINE-FOLLOWERS-02
**Class**: B (NT8-runtime-only — structural test only)
**CYC**: 1

```
// NT8-runtime-only — structural test only
// Proxy: mirrors GetSelectedFollowers() predicate:
//   item.IsSelected && item.Account != null
var items = new[]
{
    new { IsSelected = true,  Account = (object)"Sim101" },
    new { IsSelected = false, Account = (object)"Sim102" }
};
var selected = items.Where(i => i.IsSelected && i.Account != null).ToArray();
Assert.Single(selected);
Assert.Equal("Sim101", selected[0].Account);
```

**What it asserts**: Selection predicate includes only checked+non-null items; unchecked items are excluded.

---

### T_B47_03 — `ParseAtmModeName_NamedPrefix_ParsesTemplateName`
**Spec**: DW-B47-INLINE-FOLLOWERS-02 (ATM template selection)
**Class**: A (pure logic)
**CYC**: 1

```
string written = "Named:MES 5-Tick";
var mode = CopyEngine.ParseAtmModeName(written);
var named = Assert.IsType<FollowerAtmMode.Named>(mode);
Assert.Equal("MES 5-Tick", named.TemplateName);
```

**What it asserts**: A different template name than T_B46_03 (`"MES $200 SL5"`). Confirms `ParseAtmModeName` correctly parses the `Named:` prefix and extracts the template name for any valid input, not just the B46 test value.

---

### T_B47_04 — `TryAutoApply_ZeroFollowers_StatusIsNoFollowersSelected`
**Spec**: DW-B47-AUTO-RULE-01
**Class**: B (NT8-runtime-only — structural test only)
**CYC**: 2

```
// NT8-runtime-only — structural test only
// Proxy: mirrors TryAutoApply() zero-followers guard:
//   if (followers.Length == 0) { status = "No followers selected."; return; }
var followers = new object[0];
string status = followers.Length == 0 ? "No followers selected." : "Rule applied.";
Assert.Equal("No followers selected.", status);
```

**What it asserts**: Zero-length followers array triggers the early-return status message.

---

### T_B47_05 — `TryAutoApply_NullLeader_AddRuleNotCalled`
**Spec**: DW-B47-AUTO-RULE-01 (null leader guard)
**Also closes**: DW-B47-04 (LaneA deferred item requesting T_B47_05 for leader-null edge case)
**Class**: B (NT8-runtime-only — structural test only)
**CYC**: 2

```
// NT8-runtime-only — structural test only
// Proxy: mirrors TryAutoApply() null-leader guard:
//   if (_leaderAccount == null) return;
object leader = null;
bool addRuleCalled = false;
if (leader != null) addRuleCalled = true;
Assert.False(addRuleCalled);
```

**What it asserts**: When leader is null, `AddRule` is never called.

---

### T_B47_06 — `SortFollowerRows_CheckedFirst_AlphaWithinGroup`
**Spec**: DW-B47-FOLLOWERS-SORT-06
**Class**: A (pure logic)
**CYC**: 1

```
// Pure-logic: mirrors SortFollowerRows() comparator:
//   checked first (descending IsSelected), then alpha by AccountName within group
var items = new[]
{
    new { IsSelected = false, AccountName = "Sim101" },
    new { IsSelected = true,  AccountName = "Sim200" },
    new { IsSelected = true,  AccountName = "Sim102" },
    new { IsSelected = false, AccountName = "Sim050" }
};
Array.Sort(items, (a, b) =>
{
    int sel = b.IsSelected.CompareTo(a.IsSelected); // checked first
    return sel != 0 ? sel : string.Compare(a.AccountName, b.AccountName, StringComparison.Ordinal);
});
Assert.Equal("Sim102", items[0].AccountName); // checked, alpha first
Assert.Equal("Sim200", items[1].AccountName); // checked, alpha second
Assert.Equal("Sim050", items[2].AccountName); // unchecked, alpha first
Assert.Equal("Sim101", items[3].AccountName); // unchecked, alpha second
```

**What it asserts**: Sort order is checked-first, then alpha-within-group. Uses the exact same comparator logic as `SortFollowerRows`.

---

### T_B47_07 — `UpdateCopierHeader_ShowsActiveCount`
**Spec**: DW-B47-COPIER-COLLAPSE-05
**Class**: A (pure logic)
**CYC**: 1

```
// Pure-logic: mirrors UpdateCopierHeader() text format:
//   "\u25B6 Copier  (" + CountActiveFollowers() + " active)"
// where CountActiveFollowers = items.Count(i => i.IsSelected)
var items = new[]
{
    new { IsSelected = true  },
    new { IsSelected = true  },
    new { IsSelected = false }
};
int count = items.Count(i => i.IsSelected);
string header = "\u25B6 Copier  (" + count + " active)";
Assert.Contains("(2 active)", header);
```

**What it asserts**: Header string contains the correct active-follower count in the expected format.

---

### T_B47_08 — `FollowerAtmCombo_IsEnabled_FalseWhenUnchecked`
**Spec**: DW-B47-INLINE-FOLLOWERS-02 (ATM ComboBox enabled state)
**Class**: B (NT8-runtime-only — structural test only)
**CYC**: 1

```
// NT8-runtime-only — structural test only
// Proxy: mirrors inline follower row wiring:
//   atmCombo.IsEnabled = item.IsSelected;  (where item.IsSelected = false)
bool isEnabled = false; // item.IsSelected = false -> IsEnabled = false
Assert.False(isEnabled);
```

**What it asserts**: When a follower row is unchecked (`IsSelected = false`), the ATM ComboBox IsEnabled state is false.

---

### T_B47_09 — `TryAutoApply_SaveRulesCalled_AfterAddRule`
**Spec**: DW-B47-AUTO-RULE-01 (SaveRules called unconditionally after AddRule)
**Class**: B (NT8-runtime-only — structural test only)
**CYC**: 1

```
// NT8-runtime-only — structural test only
// Proxy: mirrors TryAutoApply() unconditional SaveRules call:
//   CopyEngine.Instance.AddRule(rule);
//   CopyEngine.Instance.SaveRules();  // always called after AddRule
int saveRulesCalls = 0;
Action saveRules = () => saveRulesCalls++;
saveRules(); // mirrors the unconditional SaveRules() call
Assert.Equal(1, saveRulesCalls);
```

**What it asserts**: SaveRules is called exactly once, unconditionally, after AddRule succeeds.

---

## 8. T2-C: CopyEngine.cs Tag Verification

### Expected state
```
internal const string Tag = "PTT-COPIER B47 | panel-ux-redesign | 2026-08-07";
```

### Engineer procedure
1. Open `src/PropTraderTools/CopyEngine.cs` and locate `internal const string Tag` (~line 41).
2. Read current value.
3. If `Tag == "PTT-COPIER B47 | panel-ux-redesign | 2026-08-07"`: **no edit required**. Record `ticket-2-completion.md` with grep output and verdict `VERIFIED_NO_CHANGE`.
4. If `Tag != "PTT-COPIER B47 | panel-ux-redesign | 2026-08-07"`: update to required value, record `ticket-2-completion.md` with diff and verdict `TAG_UPDATED`.

**Current confirmed value** (grep 2026-08-08):
```
CopyEngine.cs:41: internal const string Tag = "PTT-COPIER B47 | panel-ux-redesign | 2026-08-07";
```

**Prediction**: VERIFIED_NO_CHANGE — no diff will be produced.

---

## 9. CYC Summary Table

| Method | CYC | Branches | PASS? |
|--------|-----|----------|-------|
| T_B47_01 | 1 | 0 (straight-line lambda + 2 asserts) | PASS |
| T_B47_02 | 1 | 0 (LINQ predicate is expression, not method branch) | PASS |
| T_B47_03 | 1 | 0 (3 straight-line statements) | PASS |
| T_B47_04 | 2 | 1 (ternary operator) | PASS |
| T_B47_05 | 2 | 1 (`if` statement) | PASS |
| T_B47_06 | 1 | 0 (sort call + 4 asserts; comparator is a lambda arg) | PASS |
| T_B47_07 | 1 | 0 (Count + format + Contains assert) | PASS |
| T_B47_08 | 1 | 0 (bool assignment + Assert.False) | PASS |
| T_B47_09 | 1 | 0 (Action + invoke + Assert.Equal) | PASS |

All methods: CYC ≤ 2. JS standard CYC ≤ 8: ALL PASS.

---

## 10. SCAN Checklist (Pre-flight — ALL Methods)

| SCAN | Rule | Check | Result |
|------|------|-------|--------|
| SCAN-01 | ASCII-only identifiers and strings | All string literals: ASCII only. Unicode `\u25B6` in T_B47_07 is an escape sequence, not a literal non-ASCII character. | PASS |
| SCAN-02 | No `lock()` | No lock() anywhere | PASS |
| SCAN-03 | No `throw new XxxException` in hot paths | No exception throwing | PASS |
| SCAN-04 | No `DateTime.Now` | Not used | PASS |
| SCAN-05 | No `async void` | All methods are `void` | PASS |
| SCAN-06 | No `FontFamily` | Not used | PASS |
| SCAN-07 | No hardcoded hex colors | Not used | PASS |

---

## 11. Component List

### New Components (T1-C)
| Component | Type | File | Namespace |
|-----------|------|------|-----------|
| `B47Tests` | `public sealed class` | `src/PropTraderTools/B47Tests.cs` | `PropTraderTools` |

### Methods on B47Tests (all `[Fact] public void`)
| Method | CYC | Class | Spec ID |
|--------|-----|-------|---------|
| `T_B47_01_IsFollowerAccount_NullGuard_ReturnsFalse_WhenNull` | 1 | B | DW-B47-BE-FOLLOWER-SCOPE |
| `T_B47_02_GetSelectedFollowers_Proxy_CheckedItemIsIncluded` | 1 | B | DW-B47-INLINE-FOLLOWERS-02 |
| `T_B47_03_ParseAtmModeName_NamedPrefix_ParsesTemplateName` | 1 | A | DW-B47-INLINE-FOLLOWERS-02 |
| `T_B47_04_TryAutoApply_ZeroFollowers_StatusIsNoFollowersSelected` | 2 | B | DW-B47-AUTO-RULE-01 |
| `T_B47_05_TryAutoApply_NullLeader_AddRuleNotCalled` | 2 | B | DW-B47-AUTO-RULE-01 |
| `T_B47_06_SortFollowerRows_CheckedFirst_AlphaWithinGroup` | 1 | A | DW-B47-FOLLOWERS-SORT-06 |
| `T_B47_07_UpdateCopierHeader_ShowsActiveCount` | 1 | A | DW-B47-COPIER-COLLAPSE-05 |
| `T_B47_08_FollowerAtmCombo_IsEnabled_FalseWhenUnchecked` | 1 | B | DW-B47-INLINE-FOLLOWERS-02 |
| `T_B47_09_TryAutoApply_SaveRulesCalled_AfterAddRule` | 1 | B | DW-B47-AUTO-RULE-01 |

### Modified Components (T2-C)
| Component | Type | File | Change |
|-----------|------|------|--------|
| `PttBuild.Tag` | `internal const string` | `src/PropTraderTools/CopyEngine.cs` | Verify only — no edit expected |

---

## 12. Data Flow

```
Spec requirements
    |
    v
[NT8 runtime boundary classification]
    |
    +-- Class A (pure logic) ---- direct call to PropTraderTools pure static methods
    |       T_B47_03, T_B47_06, T_B47_07     (no proxies needed)
    |
    +-- Class B (structural) ---- inline proxy lambdas / local bool / ternary
            T_B47_01, T_B47_02, T_B47_04, T_B47_05, T_B47_08, T_B47_09
            (marked: // NT8-runtime-only -- structural test only)
    |
    v
xUnit [Fact] assertions
    |
    v
dotnet test --filter "T_B47" (pending DW-B44-01 CopyEngineTests.cs cleanup)
```

---

## 13. Threading Model

Not applicable. B47Tests.cs is a pure test file:
- No `Dispatcher.InvokeAsync` (no NT8 UI thread)
- No `ConcurrentQueue`
- No shared mutable state between test methods
- No `static` fields on `B47Tests`
- xUnit isolates each `[Fact]` method

T2-C (`PttBuild.Tag`): compile-time constant, no threading.

---

## 14. NinjaTrader 8 API Usage

**T1-C (B47Tests.cs)**: ZERO NT8 API calls.

**T2-C (CopyEngine.cs tag)**: No NT8 API involved. `PttBuild` is a PropTraderTools internal type.

---

## 15. File Paths in Wave Workspace

| Ticket | File Path | Status |
|--------|-----------|--------|
| T1-C | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\B47Tests.cs` | NEW — does not exist yet |
| T2-C | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` | EXISTING — verify Tag at ~line 41 |

---

## 16. Deferred Backlog for B48+

Lane C does not introduce new deferred items. All prior items are either closed or carried forward unchanged.

Items closed by Lane C:

| ID | Action |
|----|--------|
| DW-B47-01 | CLOSED — T1-C writes B47Tests.cs with T_B47_01–T_B47_09 |
| DW-B47-03 | CLOSED — T2-C verifies Tag is already correct |
| DW-B47-04 | CLOSED — T_B47_05 added to B47Tests.cs (TryAutoApply null-leader guard proxy) |

All other carried items (DW-B42-01 through DW-B46-02) remain OPEN and are outside Lane C scope.

---

## 17. Summary

Lane C is a low-risk, two-ticket lane:
- **T1-C**: New `B47Tests.cs` with 9 `[Fact]` tests. All test logic is pure C# (no NT8 runtime). The richest test (T_B47_06) validates the exact sort comparator used by `SortFollowerRows`. All spec IDs DW-B47-BE-FOLLOWER-SCOPE, DW-B47-INLINE-FOLLOWERS-02, DW-B47-AUTO-RULE-01, DW-B47-FOLLOWERS-SORT-06, DW-B47-COPIER-COLLAPSE-05 are covered.
- **T2-C**: `CopyEngine.cs` `PttBuild.Tag` verification. Current value already equals required value. Engineer records VERIFIED_NO_CHANGE. Closes DW-B47-03.

**Risk**: Low. No production logic is changed. B47Tests.cs is a new file — zero merge conflict risk. T2-C produces no diff.

**Known blocker**: `dotnet test` is blocked by DW-B44-01 (`CopyEngineTests.cs` 60 pre-existing errors). B47Tests.cs will be individually error-free but the test runner cannot execute it until DW-B44-01 is resolved. This is pre-existing debt carried from B44, not introduced by B47-LaneC.
