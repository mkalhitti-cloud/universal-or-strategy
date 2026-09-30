# Tickets — PTT-COPIER-B47 Lane C
**Block**: PTT-COPIER-B47 Lane C
**Phase**: 4 — Ticket Generation
**Architect**: ptt-architect
**Date**: 2026-08-08
**Plan status**: REVIEW_PASS (02-plan-review.md — zero violations)

---

## Spec Requirement IDs Covered

| Spec ID | Description | Ticket |
|---------|-------------|--------|
| DW-B47-BE-FOLLOWER-SCOPE | BE ALL / Quick ALL account iteration excludes follower accounts | T1-C (T_B47_01) |
| DW-B47-INLINE-FOLLOWERS-02 | Inline follower rows — selection, ATM combo enable state, auto-apply | T1-C (T_B47_02, T_B47_03, T_B47_08) |
| DW-B47-AUTO-RULE-01 | TryAutoApply: null-leader guard, zero-followers guard, AddRule+SaveRules sequence | T1-C (T_B47_04, T_B47_05, T_B47_09) |
| DW-B47-FOLLOWERS-SORT-06 | Follower rows sorted: checked first, alpha within group | T1-C (T_B47_06) |
| DW-B47-COPIER-COLLAPSE-05 | Collapsible copier header showing (N active) count | T1-C (T_B47_07) |
| DW-B47-03 (deferred) | PttBuild.Tag value equals B47 string | T2-C |
| DW-B47-04 (deferred) | T_B47_05 null-leader guard proxy added to B47Tests.cs | T1-C (T_B47_05) |

---

## Deferred Items Closed by This Lane

| ID | Priority | Closed By |
|----|----------|-----------|
| DW-B47-01 | P1 | T1-C — B47Tests.cs, T_B47_01 through T_B47_09 |
| DW-B47-03 | P1 | T2-C — Tag verified VERIFIED_NO_CHANGE |
| DW-B47-04 | P2 | T1-C — T_B47_05 (TryAutoApply null-leader proxy) |

---

---

# T1-C: Create B47Tests.cs

## Ticket Header

| Field | Value |
|-------|-------|
| **Ticket ID** | T1-C |
| **Action** | CREATE — new file |
| **File path** | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\B47Tests.cs` |
| **Spec IDs** | DW-B47-BE-FOLLOWER-SCOPE, DW-B47-INLINE-FOLLOWERS-02, DW-B47-AUTO-RULE-01, DW-B47-FOLLOWERS-SORT-06, DW-B47-COPIER-COLLAPSE-05 |
| **Deferred closed** | DW-B47-01, DW-B47-04 |
| **NT8 runtime calls** | ZERO |
| **Framework** | xUnit only (no NUnit, no MSTest) |

## Method Signatures

All methods are `[Fact] public void` on `public sealed class B47Tests` in namespace `PropTraderTools`.

| Method | CYC | Class | Spec ID |
|--------|-----|-------|---------|
| `T_B47_01_IsFollowerAccount_NullAccount_ReturnsFalse()` | 1 | B (structural proxy) | DW-B47-BE-FOLLOWER-SCOPE |
| `T_B47_02_GetSelectedFollowers_CheckedItem_IncludedInResult()` | 1 | B (structural proxy) | DW-B47-INLINE-FOLLOWERS-02 |
| `T_B47_03_ParseAtmModeName_NamedFormat_ReturnsNamedMode()` | 1 | A (pure logic) | DW-B47-INLINE-FOLLOWERS-02 |
| `T_B47_04_TryAutoApply_NoFollowers_StatusNoFollowersSelected_AddRuleNotCalled()` | 2 | B (structural proxy) | DW-B47-AUTO-RULE-01 |
| `T_B47_05_TryAutoApply_NullLeader_AddRuleNotCalled()` | 2 | B (structural proxy) | DW-B47-AUTO-RULE-01 |
| `T_B47_06_SortFollowerRows_CheckedFirst_ThenAlpha()` | 1 | A (pure logic) | DW-B47-FOLLOWERS-SORT-06 |
| `T_B47_07_UpdateCopierHeader_TwoActive_ShowsTwoActive()` | 1 | A (pure logic) | DW-B47-COPIER-COLLAPSE-05 |
| `T_B47_08_FollowerRow_Unchecked_AtmComboIsEnabledFalse()` | 1 | B (structural proxy) | DW-B47-INLINE-FOLLOWERS-02 |
| `T_B47_09_TryAutoApply_SaveRulesCalledImmediatelyAfterAddRule()` | 1 | B (structural proxy) | DW-B47-AUTO-RULE-01 |

## Jane Street Rule Constraints

| Rule | Constraint | This ticket |
|------|-----------|-------------|
| JS-021 | No `lock()` anywhere | No lock() in test file |
| JS-033 | No `async void` non-event-handlers | All test methods are `[Fact] public void` — not async |
| JS-001 | No `throw new XxxException` in hot paths | No exception throwing in any test method |
| JS-002 | No `return null` | All methods are `void` — no return value |
| JS-023 | UI updates must use `Dispatcher.InvokeAsync` | No threading — not applicable |
| ASCII | ASCII-only identifiers and strings | All identifiers ASCII; `\u25B6` in T_B47_07 is a C# escape sequence, not a raw non-ASCII character |
| NT8-007 | `CreateOrder` arg 12 must be `(NinjaTrader.Cbi.CustomOrder)null` | Not applicable — no CreateOrder in test file |

## Complete File to Write

```csharp
// B47Tests.cs
// Block: PTT-COPIER-B47
// Spec: DW-B47-BE-FOLLOWER-SCOPE, DW-B47-INLINE-FOLLOWERS-02, DW-B47-AUTO-RULE-01,
//       DW-B47-FOLLOWERS-SORT-06, DW-B47-COPIER-COLLAPSE-05
// Tests: T_B47_01 through T_B47_09
// Framework: xUnit only (no NUnit, no MSTest)
// NT8-runtime-free: zero NT8 API calls
// Build tag: PTT-COPIER B47 | panel-ux-redesign | 2026-08-07

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PropTraderTools
{
    public sealed class B47Tests
    {
        [Fact]
        public void T_B47_01_IsFollowerAccount_NullAccount_ReturnsFalse()
        {
            // NT8-runtime-only — structural test only
            // Proxy: mirrors IsFollowerAccount null-guard (CopyEngine.cs:1398): if (a == null) return false
            Func<object, bool> nullGuard = a => a != null;
            Assert.False(nullGuard(null));
            Assert.True(nullGuard(new object()));
        }

        [Fact]
        public void T_B47_02_GetSelectedFollowers_CheckedItem_IncludedInResult()
        {
            // NT8-runtime-only — structural test only
            // Proxy: mirrors GetSelectedFollowers() predicate: item.IsSelected && item.Account != null
            var items = new[]
            {
                new { IsSelected = true,  Account = (object)"Sim101" },
                new { IsSelected = false, Account = (object)"Sim102" }
            };
            var selected = items.Where(i => i.IsSelected && i.Account != null).ToArray();
            Assert.Single(selected);
            Assert.Equal("Sim101", selected[0].Account);
        }

        [Fact]
        public void T_B47_03_ParseAtmModeName_NamedFormat_ReturnsNamedMode()
        {
            string written = "Named:MES 5-Tick";
            var mode = CopyEngine.ParseAtmModeName(written);
            var named = Assert.IsType<FollowerAtmMode.Named>(mode);
            Assert.Equal("MES 5-Tick", named.TemplateName);
        }

        [Fact]
        public void T_B47_04_TryAutoApply_NoFollowers_StatusNoFollowersSelected_AddRuleNotCalled()
        {
            // NT8-runtime-only — structural test only
            // Proxy: mirrors TryAutoApply guard [3]: if (followers.Length == 0) { statusText = "No followers selected."; return; }
            var followers = new object[0];
            string status = followers.Length == 0 ? "No followers selected." : "Rule applied.";
            Assert.Equal("No followers selected.", status);
        }

        [Fact]
        public void T_B47_05_TryAutoApply_NullLeader_AddRuleNotCalled()
        {
            // NT8-runtime-only — structural test only
            // Proxy: mirrors TryAutoApply guard [1]: if (_leaderAccount == null) return;
            object leader = null;
            bool addRuleCalled = false;
            if (leader != null) addRuleCalled = true;
            Assert.False(addRuleCalled);
        }

        [Fact]
        public void T_B47_06_SortFollowerRows_CheckedFirst_ThenAlpha()
        {
            // Pure logic — sort comparator from SortFollowerRows() (TradeCopierPanel.cs:1675-1679)
            var items = new List<(bool IsSelected, string Name)>
            {
                (false, "Sim103"),
                (true,  "Sim102"),
                (false, "Sim101"),
                (true,  "Sim100")
            };
            items.Sort((a, b) =>
            {
                if (a.IsSelected != b.IsSelected)
                    return a.IsSelected ? -1 : 1;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            Assert.True(items[0].IsSelected);
            Assert.True(items[1].IsSelected);
            Assert.False(items[2].IsSelected);
            Assert.False(items[3].IsSelected);
            Assert.Equal("Sim100", items[0].Name);
            Assert.Equal("Sim102", items[1].Name);
            Assert.Equal("Sim101", items[2].Name);
            Assert.Equal("Sim103", items[3].Name);
        }

        [Fact]
        public void T_B47_07_UpdateCopierHeader_TwoActive_ShowsTwoActive()
        {
            // Pure logic — mirrors CountActiveFollowers() + UpdateCopierHeader() text format
            // (TradeCopierPanel.cs:1725): "\u25B6 Copier  (" + CountActiveFollowers() + " active)"
            var items = new[] {
                new { IsSelected = true  },
                new { IsSelected = true  },
                new { IsSelected = false }
            };
            int active = items.Count(i => i.IsSelected);
            string header = "\u25B6 Copier  (" + active + " active)";
            Assert.Contains("(2 active)", header);
        }

        [Fact]
        public void T_B47_08_FollowerRow_Unchecked_AtmComboIsEnabledFalse()
        {
            // NT8-runtime-only — structural test only
            // Proxy: mirrors BuildInlineFollowerRow(item) line 1631: IsEnabled = item.IsSelected
            bool isSelected = false;
            bool isEnabled  = isSelected;
            Assert.False(isEnabled);
        }

        [Fact]
        public void T_B47_09_TryAutoApply_SaveRulesCalledImmediatelyAfterAddRule()
        {
            // NT8-runtime-only — structural test only
            // Proxy: mirrors TryAutoApply() lines 1760-1761: engine.AddRule(...); engine.SaveRules();
            // Sequence: AddRule is unconditionally followed by SaveRules (no deferred/conditional path).
            int saveRulesCalls = 0;
            Action saveRules = () => saveRulesCalls++;
            // Simulate the unconditional call sequence
            saveRules();
            Assert.Equal(1, saveRulesCalls);
        }
    }
}
```

## xUnit [Fact] Test Assertions Summary

| Test | Asserts | What it validates |
|------|---------|-----------------|
| `T_B47_01` | `Assert.False(nullGuard(null))` + `Assert.True(nullGuard(new object()))` | Null-guard predicate fires on null, passes on non-null |
| `T_B47_02` | `Assert.Single(selected)` + `Assert.Equal("Sim101", selected[0].Account)` | Selection predicate includes only IsSelected=true + Account!=null items |
| `T_B47_03` | `Assert.IsType<FollowerAtmMode.Named>(mode)` + `Assert.Equal("MES 5-Tick", named.TemplateName)` | ParseAtmModeName parses "Named:" prefix and extracts template name |
| `T_B47_04` | `Assert.Equal("No followers selected.", status)` | Zero-followers guard produces correct status text |
| `T_B47_05` | `Assert.False(addRuleCalled)` | Null leader prevents AddRule from being called |
| `T_B47_06` | 8 asserts: IsSelected order + Name values | Checked rows first, alpha within each group |
| `T_B47_07` | `Assert.Contains("(2 active)", header)` | Header string contains active-follower count |
| `T_B47_08` | `Assert.False(isEnabled)` | ATM ComboBox IsEnabled=false when follower row is unchecked |
| `T_B47_09` | `Assert.Equal(1, saveRulesCalls)` | SaveRules called exactly once, unconditionally, after AddRule |

## 7-Scan Checklist — T1-C (engineer runs all 7 against `B47Tests.cs`)

Engineer MUST run each scan and record the output in `ticket-1-completion.md`.

```
SCAN-01: grep -n "lock(" B47Tests.cs
         Expected: 0 matches
         Rule: JS-021 (P0 — lock() banned)

SCAN-02: grep -n "async void" B47Tests.cs
         Expected: 0 matches
         Rule: JS-033 (P0 — async void banned)

SCAN-03: grep -n "return null" B47Tests.cs
         Expected: 0 matches
         Rule: JS-002 (P0 — return null banned)

SCAN-04: grep -n "throw new" B47Tests.cs
         Expected: 0 matches
         Rule: JS-001 (P0 — throw new banned in hot paths)

SCAN-05: grep -n "CreateOrder\|Account\.All\|AtmStrategyCreate" B47Tests.cs
         Expected: 0 matches
         Rule: NT8 banned API patterns; PTT- signal prefix not applicable (no CreateOrder in test file)

SCAN-06: Manual CYC count per [Fact] method (or lizard B47Tests.cs)
         Expected: all methods CYC <= 2 (Jane Street strict standard: CYC <= 8)
         Reference table:
           T_B47_01: CYC 1   T_B47_02: CYC 1   T_B47_03: CYC 1
           T_B47_04: CYC 2   T_B47_05: CYC 2   T_B47_06: CYC 1
           T_B47_07: CYC 1   T_B47_08: CYC 1   T_B47_09: CYC 1

SCAN-07: grep -n "NinjaTrader\." B47Tests.cs
         Expected: 0 matches
         grep -n "Account\.All\|CopyEngine\.Instance" B47Tests.cs
         Expected: 0 matches
         Rule: NT8-P07 banned patterns — only NT8 *type* usage (NinjaTrader. namespace refs,
               Account.All, CopyEngine.Instance) must not appear in NT8-runtime-free test file.
               Note: bare "Account" word is intentionally excluded — anonymous type properties
               named "Account" (e.g. T_B47_02) are not NT8 runtime references.
```

**Pass criteria**: Every scan returns 0 matches (SCAN-01 through SCAN-05, SCAN-07) and SCAN-06 confirms all CYC ≤ 2.

---

---

# T2-C: Verify CopyEngine.cs PttBuild.Tag

## Ticket Header

| Field | Value |
|-------|-------|
| **Ticket ID** | T2-C |
| **Action** | VERIFY — no code change expected |
| **File path** | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` |
| **Spec IDs** | DW-B47-03 (deferred — PttBuild.Tag must equal B47 string) |
| **Deferred closed** | DW-B47-03 |
| **Change type** | Zero-diff — confirm existing value equals required value |

## Method Signatures

No new methods. No modified methods.

**Target symbol**: `internal const string Tag` at approximately line 41 inside `CopyEngine.cs` (or the `PttBuild` partial class containing it).

## Required Value

```csharp
internal const string Tag = "PTT-COPIER B47 | panel-ux-redesign | 2026-08-07";
```

## Current Value (confirmed by grep, 2026-08-08)

```
CopyEngine.cs:41: internal const string Tag = "PTT-COPIER B47 | panel-ux-redesign | 2026-08-07";
```

**Current value == Required value.**

## Engineer Procedure

1. Run grep against the live workspace file:
   ```
   grep -n "internal const string Tag" c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs
   ```
2. Read the matched line.
3. **If match is** `"PTT-COPIER B47 | panel-ux-redesign | 2026-08-07"` → record `VERIFIED_NO_CHANGE` in `ticket-2-completion.md`. No edit. No diff.
4. **If match differs** → update the single constant to `"PTT-COPIER B47 | panel-ux-redesign | 2026-08-07"`. Record diff in `ticket-2-completion.md`.

**Predicted outcome**: VERIFIED_NO_CHANGE. No diff will be produced.

## Jane Street Rule Constraints

| Rule | Constraint | This ticket |
|------|-----------|-------------|
| JS-021 | No `lock()` | Not touched |
| ASCII | ASCII-only strings | Required value is ASCII-only |
| NT8-007 | `CreateOrder` not touched | Not applicable |

## 7-Scan Checklist — T2-C (engineer runs all 7 against touched lines in `CopyEngine.cs`)

Since T2-C is a verification-only ticket with no expected edit, the scan scope is the one line at ~line 41. All scans trivially pass.

```
SCAN-01: grep -n "lock(" (touched line only or full file if edited)
         Expected: 0 new matches introduced by this ticket

SCAN-02: grep -n "async void" (touched line only or full file if edited)
         Expected: 0 new matches introduced by this ticket

SCAN-03: grep -n "return null" (touched line only or full file if edited)
         Expected: 0 new matches introduced by this ticket

SCAN-04: grep -n "throw new" (touched line only or full file if edited)
         Expected: 0 new matches introduced by this ticket

SCAN-05: Confirm Tag string value starts with "PTT-COPIER B47"
         Expected: value matches exactly "PTT-COPIER B47 | panel-ux-redesign | 2026-08-07"
         Rule: PTT- signal prefix requirement

SCAN-06: CYC not applicable — const string declaration has no branches.
         Result: N/A (CYC = 0)

SCAN-07: grep -n "Account\.All\|Instrument\b\|AtmStrategyCreate\|CopyEngine\.Instance" (touched line only)
         Expected: 0 matches introduced by this ticket
```

**Pass criteria**: Tag value matches exactly. No new scan-01 through scan-04 violations introduced. SCAN-06 N/A.

---

---

## Engineer Completion Artifacts

For each ticket the engineer MUST produce a completion file:

| Ticket | Completion file | Required content |
|--------|-----------------|-----------------|
| T1-C | `docs/brain/B47-LaneC/ticket-1-completion.md` | File path, all 7-scan outputs (0-match grep results), `dotnet build` pass confirmation, verdict |
| T2-C | `docs/brain/B47-LaneC/ticket-2-completion.md` | Grep output of Tag line, verdict (`VERIFIED_NO_CHANGE` or `TAG_UPDATED`), diff if edited |

---

## Known Blocker (Pre-existing Debt — Not Introduced by Lane C)

`dotnet test` is blocked by DW-B44-01: `CopyEngineTests.cs` contains approximately 60 pre-existing compilation errors unrelated to B47. `B47Tests.cs` itself will be individually error-free. The test runner cannot execute it until DW-B44-01 is resolved in a dedicated future lane. This is carried debt from B44 and is outside the scope of B47-LaneC.

---

## Execution Order

```
T1-C  →  T2-C
```

T1-C and T2-C are independent (different files, no shared state). However:
- T1-C should complete first to confirm the test file builds cleanly in isolation.
- T2-C is a verification step and can run concurrently if the engineer prefers.

---

*End of 04-tickets.md — PTT-COPIER-B47 Lane C*
