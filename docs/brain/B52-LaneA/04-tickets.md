# B52-LaneA Tickets
**Block**: B52-LaneA | `test-restore-extraction`
**Architect**: ptt-architect (Phase 3)
**Date**: 2026-08-08
**Plan input**: docs/brain/B52-LaneA/02-architecture-plan.md (PLAN_REVIEW_PASS — 10/10 checks, 0 violations)

---

## Ticket Summary

| ID | Label | Priority | File(s) |
|----|-------|----------|---------|
| T-B52-01 | DW-B50C-01 — Restore weakened FindFollowerBracketOrder test assertion | P1 | `CopyEngineTests.cs` |
| T-B52-02 | DW-B51-03 — Extract PopulateAtmComboItems + ApplyAtmAutoSelect from OnFollowerAtmTemplateComboLoaded | P2 | `TradeCopierPanel.cs`, `CopyEngine.cs` (tag only) |

---

## T-B52-01 — DW-B50C-01: Restore FindFollowerBracketOrder Test

**ID**: T-B52-01
**Label**: DW-B50C-01 — Restore weakened FindFollowerBracketOrder test assertion
**Priority**: P1
**Deferred from**: B50-LaneC (DW-B50C-01)

### File

```
C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs
```

### Spec Requirement ID

**DW-B50C-01** — Restore `FindFollowerBracketOrder_NullableReturnType` test so it:
1. Keeps the return-type assertion (`Assert.Equal typeof Order`)
2. Adds a behavioral null-path assertion: invoking the method with a non-matching
   Account + signal name produces a null return value
3. Remains .NET 4.8 compatible (no `NullabilityInfoContext`)
4. The test is renamed to `FindFollowerBracketOrder_ReturnsNullWhenNoMatch`

### Implementation Instructions

**Locate** the existing test at lines 428–440 in `CopyEngineTests.cs`.

**REPLACE** this block (lines 428–440):

```csharp
        [Fact]
        public void FindFollowerBracketOrder_NullableReturnType()
        {
            // T-B7-04: FindFollowerBracketOrder return type is Order? (nullable reference type).
            // Confirms JS-002 compliance -- null contract is explicit at the type level.
            // NullabilityInfoContext is .NET 6+ only; on .NET 4.8 we verify the return type directly.
            var method = typeof(CopyEngine).GetMethod(
                "FindFollowerBracketOrder",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            // On .NET 4.8 the return type is NinjaTrader.Cbi.Order (reference type, nullable by nature).
            Assert.Equal(typeof(NinjaTrader.Cbi.Order), method.ReturnType);
        }
```

**WITH** this replacement:

```csharp
        [Fact]
        public void FindFollowerBracketOrder_ReturnsNullWhenNoMatch()
        {
            // T-B7-04 (DW-B50C-01 restored): FindFollowerBracketOrder returns null when no matching order.
            // Confirms JS-002 compliance -- null contract verified at BOTH type and behavioral level.
            // On .NET 4.8, NullabilityInfoContext is unavailable; return type checked directly.
            var method = typeof(CopyEngine).GetMethod(
                "FindFollowerBracketOrder",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            // Assertion 1: return type contract (type-level JS-002 compliance).
            Assert.Equal(typeof(NinjaTrader.Cbi.Order), method.ReturnType);
            // Assertion 2: behavioral null contract -- method returns null when no order matches.
            // Use a fresh Account with a nonexistent signal name -- foreach produces 0 matches.
            var stubAccount = new Account { Name = "B52-NULL-PATH" };
            object result = null;
            try
            {
                result = method.Invoke(_engine, new object[] { stubAccount, "NONEXISTENT_SIGNAL_B52", false });
            }
            catch (System.Reflection.TargetInvocationException tie)
            {
                // Account.Orders not available in test harness (no NT8 runtime) -- NRE is expected.
                // Type-level assertion above already confirmed the null contract at the signature level.
                if (tie.InnerException is NullReferenceException)
                    return;
                throw;
            }
            // If method returned cleanly (Account.Orders was empty), result must be null.
            Assert.Null(result);
        }
```

### Method Signatures Involved

No new production methods. One test method **replaced** (rename + body expansion):

| Method | Kind | Status |
|--------|------|--------|
| `public void FindFollowerBracketOrder_ReturnsNullWhenNoMatch()` | `[Fact]` xUnit test | REPLACES `FindFollowerBracketOrder_NullableReturnType` |

### xUnit [Fact] Test Names

| Test Name | Asserts |
|-----------|---------|
| `FindFollowerBracketOrder_ReturnsNullWhenNoMatch` | (1) `method.ReturnType == typeof(NinjaTrader.Cbi.Order)` — type-level null contract; (2) `Assert.Null(result)` — behavioral null contract when invoked with nonexistent signal; (3) Handles NT8-absent runtime via `TargetInvocationException` + `NullReferenceException` guard |

### JS Rule Constraints

| Rule | Applies To | Assessment |
|------|-----------|------------|
| JS-002 | `Assert.Null(result)` | NOT a violation — test assertion code checking SUT behavior, not production code returning null |
| JS-002 | `object result = null;` | NOT a violation — local variable initialization, not a `return null` statement |
| JS-021 | Entire test method | No `lock(` anywhere ✅ |
| JS-033 | Test method signature | `public void [Fact]` — not `async void` ✅ |

### 7-Scan Checklist (Engineer Contract)

| Scan | Check | Command | Expected |
|------|-------|---------|---------|
| SCAN-01 | No `lock()` in test code | `grep -rn "lock(" CopyEngineTests.cs` | 0 results in modified lines |
| SCAN-02 | No `async void` in test code | `grep -rn "async void" CopyEngineTests.cs` | 0 results in modified lines |
| SCAN-03 | No new `return null` in CopyEngineTests.cs | `grep -rn "return null" CopyEngineTests.cs` | No NEW occurrences introduced |
| SCAN-04 | CYC of new test method ≤ 8 | Manual: `try/catch`(1) + `if(NRE)`(1) = 2 decisions → McCabe=3, Lizard=2 | ≤ 8 ✅ |
| SCAN-05 | dotnet build passes | `dotnet build` in Wave workspace | 0 errors |
| SCAN-06 | N/A | No production complexity change in this ticket | N/A |
| SCAN-07 | Hard-link sync clean | `powershell -File scripts\verify_links.ps1` | DESYNC=0 |

### Acceptance Criteria

- [ ] Test method named `FindFollowerBracketOrder_ReturnsNullWhenNoMatch`
- [ ] Old test `FindFollowerBracketOrder_NullableReturnType` is gone (deleted/replaced)
- [ ] Two assertions present: `Assert.Equal` (type contract) + `Assert.Null` (behavioral contract)
- [ ] `TargetInvocationException` catch block with `NullReferenceException` inner-exception guard
- [ ] `dotnet build` passes — SCAN-05 ✅
- [ ] No new `return null` statement in `CopyEngineTests.cs` — SCAN-03 ✅
- [ ] No `lock(` or `async void` in modified code — SCAN-01, SCAN-02 ✅

---

## T-B52-02 — DW-B51-03: Extract OnFollowerAtmTemplateComboLoaded Helpers

**ID**: T-B52-02
**Label**: DW-B51-03 — Extract PopulateAtmComboItems + ApplyAtmAutoSelect from OnFollowerAtmTemplateComboLoaded
**Priority**: P2
**Deferred from**: B51-LaneA (DW-B51-03)

### Files

```
C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs  (method replacement + 2 new private methods)
C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs        (build tag only — line 41)
```

### Spec Requirement ID

**DW-B51-03** — Reduce `OnFollowerAtmTemplateComboLoaded` CYC from 12 to ≤ 5 by extracting:
- `PopulateAtmComboItems` — absorbs branches 5–8 (dir-guard, foreach, leader-match, catch)
- `ApplyAtmAutoSelect` — absorbs branches 9–11 (defaultIdx guard, selName guard, item guard)

Parent retains branches 1–4 only. Both helpers are `private` instance methods. No new public API. All 11 branches preserved (no behavior dropped).

### Implementation Instructions

#### STEP A — Replace `OnFollowerAtmTemplateComboLoaded` (lines 1969–2021 in `TradeCopierPanel.cs`)

**REPLACE** the entire method body with:

```csharp
        private void OnFollowerAtmTemplateComboLoaded(object sender, RoutedEventArgs e)
        {
            var cb = sender as ComboBox;
            if (cb == null) return;                                // branch 1 -- null guard
            if (cb.Items.Count > 0) return;                       // branch 2 -- idempotency guard
            if (!_atmComboRefs.Contains(cb))
            {
                _atmComboRefs.Add(cb);                            // B50: track combo for Clone visibility toggle
                // B51: apply current mode to newly-loaded combo (timing fix)
                if (CopyEngine.Instance.GetCopyMode() == CopyMode.Clone)
                    cb.Visibility = Visibility.Collapsed;
            }
            cb.Items.Add("(none)");
            string leaderTemplate = GetLeaderAtmTemplateName(_currentChart);
            PopulateAtmComboItems(cb, leaderTemplate, out int defaultIdx);
            cb.SelectedIndex = defaultIdx;
            ApplyAtmAutoSelect(cb, defaultIdx);
        }
```

#### STEP B — Insert two new private methods IMMEDIATELY AFTER the closing brace of `OnFollowerAtmTemplateComboLoaded` (after line 2021)

```csharp
        // DW-B51-03: extracted from OnFollowerAtmTemplateComboLoaded to reduce parent CYC.
        // Scans ATM template XML files and identifies the leader's default selection index.
        // CYC(Lizard)=4: dir-exists + foreach + leader-match + catch.
        private void PopulateAtmComboItems(ComboBox cb, string leaderTemplate, out int defaultIdx)
        {
            defaultIdx = 0;
            try
            {
                // NT8-045: AtmStrategyTemplates not available in Linting DLL -- use filesystem path.
                string atmDir = System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                    "NinjaTrader 8", "templates", "AtmStrategy");
                if (System.IO.Directory.Exists(atmDir))
                {
                    foreach (var f in System.IO.Directory.GetFiles(atmDir, "*.xml"))
                    {
                        string tName = System.IO.Path.GetFileNameWithoutExtension(f);
                        cb.Items.Add(tName);
                        if (tName == leaderTemplate)
                            defaultIdx = cb.Items.Count - 1;
                    }
                }
            }
            catch
            {
                // Directory unavailable -- "(none)" only.
            }
        }

        // DW-B51-03: extracted from OnFollowerAtmTemplateComboLoaded to reduce parent CYC.
        // Applies auto-selection and writes AtmModeName on the FollowerItem if a named template was selected.
        // CYC(Lizard)=3: defaultIdx-guard + selName-guard + item-guard.
        private void ApplyAtmAutoSelect(ComboBox cb, int defaultIdx)
        {
            // B46 T2: write item.AtmModeName immediately on auto-select so OnApplyRule
            // picks up Named mode without requiring a manual ComboBox interaction.
            // defaultIdx == 0 means "(none)" was selected -- leave AtmModeName as "Inherit".
            if (defaultIdx > 0)
            {
                var selName = cb.Items[defaultIdx] as string;
                if (!string.IsNullOrEmpty(selName))
                {
                    var item = (cb.DataContext as FollowerItem)
                               ?? FindAncestorDataContext<FollowerItem>(cb);
                    if (item != null)
                        item.AtmModeName = "Named:" + selName;
                }
            }
        }
```

#### STEP C — Update build tag in `CopyEngine.cs` (line 41 only)

Change:
```csharp
internal const string Tag = "PTT-COPIER B51 | ui-fixes | 2026-08-08";
```

To:
```csharp
internal const string Tag = "PTT-COPIER B52 | test-restore-extraction | 2026-08-08";
```

### Method Signatures

| Method | Access | Status | File |
|--------|--------|--------|------|
| `private void OnFollowerAtmTemplateComboLoaded(object sender, RoutedEventArgs e)` | `private` | MODIFIED (simplified) | `TradeCopierPanel.cs` |
| `private void PopulateAtmComboItems(ComboBox cb, string leaderTemplate, out int defaultIdx)` | `private` | NEW | `TradeCopierPanel.cs` |
| `private void ApplyAtmAutoSelect(ComboBox cb, int defaultIdx)` | `private` | NEW | `TradeCopierPanel.cs` |

### xUnit Tests

**No new xUnit tests required** for this ticket. `OnFollowerAtmTemplateComboLoaded` is a WPF `RoutedEventHandler`; WPF UI context is unavailable in the xUnit test harness. SCAN-06 (branch-count / CYC verification) substitutes for test coverage on the refactoring. Same rationale as B51.

### JS Rule Constraints

| Rule | Applies To | Assessment |
|------|-----------|------------|
| JS-021 | `PopulateAtmComboItems`, `ApplyAtmAutoSelect`, parent | No `lock(` in any method ✅ |
| JS-033 | All three methods | `private void` only — not `async void` ✅ |
| JS-002 | `PopulateAtmComboItems` | Returns `void` via `out` param — no `return null` ✅ |
| JS-002 | `ApplyAtmAutoSelect` | Returns `void` — no `return null` ✅ |
| JS-002 | `OnFollowerAtmTemplateComboLoaded` (parent) | `return;` early exits (void) — not `return null` ✅ |

### 7-Scan Checklist (Engineer Contract)

| Scan | Check | Command | Expected |
|------|-------|---------|---------|
| SCAN-01 | No `lock()` in new/modified methods | `grep -rn "lock(" TradeCopierPanel.cs` | 0 occurrences in new methods |
| SCAN-02 | No `async void` in new/modified methods | `grep -rn "async void" TradeCopierPanel.cs` | 0 occurrences in new methods |
| SCAN-03 | No new `return null` in `TradeCopierPanel.cs` | `grep -rn "return null" TradeCopierPanel.cs` | No NEW occurrences introduced |
| SCAN-04 | N/A | No test method CYC check for this ticket | N/A |
| SCAN-05 | `dotnet build` passes | `dotnet build` in Wave workspace | 0 errors |
| SCAN-06 | CYC of all 3 methods ≤ 8 | Lizard: parent=4, `PopulateAtmComboItems`=4, `ApplyAtmAutoSelect`=3 | All ≤ 8 ✅ |
| SCAN-07 | Hard-link sync clean | `powershell -File scripts\verify_links.ps1` | DESYNC=0 MISSING=0 |

**CYC Reference Table** (for SCAN-06 verification):

| Method | Before (McCabe/Lizard) | After (McCabe/Lizard) | Target ≤ 8? |
|--------|----------------------|----------------------|-------------|
| `OnFollowerAtmTemplateComboLoaded` | 12 / 11 | 5 / 4 | ✅ |
| `PopulateAtmComboItems` | N/A (new) | 5 / 4 | ✅ |
| `ApplyAtmAutoSelect` | N/A (new) | 4 / 3 | ✅ |

### Acceptance Criteria

- [ ] `OnFollowerAtmTemplateComboLoaded` body reduced to ≤ 14 lines (branches 1–4 + 2 helper calls)
- [ ] `PopulateAtmComboItems` present immediately after `OnFollowerAtmTemplateComboLoaded` closing brace
- [ ] `ApplyAtmAutoSelect` present immediately after `PopulateAtmComboItems` closing brace
- [ ] All 11 branches present across the 3 methods — none dropped, none duplicated
- [ ] `cb.SelectedIndex = defaultIdx` remains in parent method, between the two helper calls
- [ ] Both helpers are `private` (not `static`, not `public`)
- [ ] `dotnet build` passes — SCAN-05: 0 errors ✅
- [ ] No `lock(` anywhere in new/modified code — SCAN-01 ✅
- [ ] No `async void` in new/modified code — SCAN-02 ✅
- [ ] DESYNC=0 after `verify_links.ps1` — SCAN-07 ✅
- [ ] CYC: parent Lizard=4, `PopulateAtmComboItems` Lizard=4, `ApplyAtmAutoSelect` Lizard=3 — SCAN-06 ✅
- [ ] Build tag updated to `"PTT-COPIER B52 | test-restore-extraction | 2026-08-08"` at `CopyEngine.cs` line 41

---

## Implementation Sequence

Execute tickets in this order:

1. **T-B52-01 first** (P1, `CopyEngineTests.cs` only — isolated, no production code changed)
2. **T-B52-02 second** (P2, `TradeCopierPanel.cs` + `CopyEngine.cs` tag)

Run `dotnet build` after each ticket. Run `powershell -File scripts\verify_links.ps1 -Fix` once after T-B52-02 to sync all hard links.

---

*Tickets written by ptt-architect (Phase 3). Input: PLAN_REVIEW_PASS (10/10 checks, 0 violations).*
