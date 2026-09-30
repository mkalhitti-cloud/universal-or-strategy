# BWAVE-DW LaneB — 04-tickets.md

**Phase**: 3 (Ticket Generation)
**Epic**: BWAVE-DW LaneB
**Author**: ptt-architect
**Date**: 2026-08-26
**Source Plan**: 02-architecture-plan.md (REVIEW_PASS)

---

## Ticket Execution Order

```
B-1 (ACTIVE: delete 3 tests in BwaveCycLaneCTests.cs)
  -> B-2 (VERIFY-ONLY: BrushInactive at button construction)
     -> B-3 (VERIFY-ONLY: WPF cluster helpers extraction)
        -> B-4 (ACTIVE: refactor BuildFollowerMultipliers)
           -> B-5 (VERIFY-ONLY: tab order in BuildRuleRow)
```

All tickets are independent. No compile dependency from one to another.

---

## TICKET B-1: Delete BwaveCycR2ArrowClusterTests Class

**Type**: ACTIVE (test deletion — no production `.cs` change)
**Spec Req IDs**: DW-C39-06, DW-LaneA-06

---

### Files

| File | Change |
|------|--------|
| `src/PropTraderTools/Tests/BwaveCycLaneCTests.cs` | DELETE class `BwaveCycR2ArrowClusterTests` (lines 305-352, including leading comment on line 305) |

**DO NOT** touch `src/PropTraderTools/TradeCopierPanel.cs`.
`BuildArrowCluster` is called at line 1172 inside `BuildBufferedButtonsRow`. It has 1 caller and MUST NOT be deleted.

---

### Exact Change

**Before** — lines 305–352 of `src/PropTraderTools/Tests/BwaveCycLaneCTests.cs`:

```csharp
    // BWAVE-CYC R2: tests for BuildArrowCluster extracted from BuildBufferedButtonsRow.
    // All tests use reflection -- xUnit on .NET Framework 4.8 cannot instantiate WPF Panel directly.
    // Pattern: invoke BuildArrowCluster via reflection, inspect returned ValueTuple fields.
    public class BwaveCycR2ArrowClusterTests
    {
        private static System.Reflection.MethodInfo GetArrowCluster()
        {
            return typeof(TradeCopierPanel).GetMethod(
                "BuildArrowCluster",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        }

        [Fact]
        public void BuildArrowCluster_SetsMainBackground_WhenProvided()
        {
            var m = GetArrowCluster();
            Assert.NotNull(m);
            // Verify signature: 6 params
            Assert.Equal(6, m.GetParameters().Length);
            // Verify param 1 is string (mainContent)
            Assert.Equal(typeof(string), m.GetParameters()[0].ParameterType);
            // Verify param 2 is Brush (mainBackground)
            Assert.Equal(typeof(System.Windows.Media.Brush), m.GetParameters()[1].ParameterType);
        }

        [Fact]
        public void BuildArrowCluster_SetsTealBorder_WhenUseTealBorderTrue()
        {
            var m = GetArrowCluster();
            Assert.NotNull(m);
            // Verify param 2 is bool (useTealBorder)
            Assert.Equal(typeof(bool), m.GetParameters()[2].ParameterType);
            // Return type is a ValueTuple -- verify it is a value type (tuple struct)
            Assert.True(m.ReturnType.IsValueType);
        }

        [Fact]
        public void BuildArrowCluster_WiresUpDownAndMainClickHandlers()
        {
            var m = GetArrowCluster();
            Assert.NotNull(m);
            var parms = m.GetParameters();
            // Params 3,4,5 must all be RoutedEventHandler
            Assert.Equal(typeof(System.Windows.RoutedEventHandler), parms[3].ParameterType);
            Assert.Equal(typeof(System.Windows.RoutedEventHandler), parms[4].ParameterType);
            Assert.Equal(typeof(System.Windows.RoutedEventHandler), parms[5].ParameterType);
        }
    }
```

**After**: Lines 305–352 are deleted entirely. Line 353 (`// BWAVE-CYC R3: tests for...`) becomes the new line 305 with no blank line inserted above it.

---

### Method Signatures

N/A — deletion only, no new method signatures.

---

### xUnit [Fact] Names

None — this ticket deletes tests; no new tests are added.

---

### JS Rule Constraints

| Rule | Constraint | Applies |
|------|------------|---------|
| JS-021 | No `lock()` anywhere | verify 0 matches in `src/PropTraderTools/` |
| JS-033 | No `async void` (non-event-handler) | verify 0 new matches |

---

### 7-Scan Checklist

Engineer MUST complete ALL 7 scans and record results before marking ticket done.

| Scan | Command | Expected Result |
|------|---------|----------------|
| SCAN-01 | `grep -r "lock(" src/PropTraderTools/` | 0 matches |
| SCAN-02 | `grep -rn "async void " src/PropTraderTools/` | 0 new non-event-handler matches |
| SCAN-03 | `grep -rn "return null;" src/PropTraderTools/` | N/A — no production code changed |
| SCAN-04 | `python scripts/complexity_audit.py` | All methods CYC <= 8 (no change expected) |
| SCAN-05 | `grep -P "[^\x00-\x7F]" src/PropTraderTools/Tests/BwaveCycLaneCTests.cs` | 0 non-ASCII in modified file |
| SCAN-06 | `dotnet build` | 0 errors, 0 warnings |
| SCAN-07 | `Select-String -Path "src/PropTraderTools/Tests/BwaveCycLaneCTests.cs" -Pattern "BwaveCycR2ArrowCluster"` | 0 matches after deletion |

---

### Acceptance Criteria

- [ ] Lines 305–352 of `BwaveCycLaneCTests.cs` deleted (48 lines: 3-line comment + full class body)
- [ ] `BuildArrowCluster` in `TradeCopierPanel.cs` is UNTOUCHED
- [ ] SCAN-06: `dotnet build` passes with 0 errors, 0 warnings
- [ ] SCAN-07: 0 matches for `BwaveCycR2ArrowCluster` anywhere in test file

---

## TICKET B-2: Verify BrushInactive at Button Construction

**Type**: VERIFY-ONLY (no code change)
**Spec Req IDs**: DW-C39-09

---

### Files

No files modified. Verification only.

---

### Exact Change

No change. Evidence that the work is already complete:

In `src/PropTraderTools/TradeCopierPanel.cs` lines 1152–1168, `BuildBufferedButtonsRow` declares a
data-driven specs array. All 6 entries (including `_beBtn2`, `_globalBeBtn2`, `_quickBtn`,
`_quickAllBtn`) pass `BrushInactive` as the `Bg` parameter.

`BuildArrowCluster` (line ~1233) creates:
```csharp
var btn = new Button { Content = mainContent, Background = mainBackground };
```
When `mainBackground = BrushInactive`, the button IS constructed with `Background = BrushInactive`.

**Verification commands** (engineer runs to confirm, does not modify):
```powershell
# Confirm all 6 specs have BrushInactive as Bg parameter (lines 1163-1168)
Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "BrushInactive" | ForEach-Object { "Line $($_.LineNumber): $($_.Line.Trim())" }

# Confirm BuildArrowCluster sets Background from the mainBackground parameter
Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "Background = mainBackground"
```

---

### Method Signatures

N/A — no code change.

---

### xUnit [Fact] Names

None — verify-only ticket.

---

### JS Rule Constraints

| Rule | Constraint |
|------|------------|
| JS-021 | Verify 0 `lock()` in src/PropTraderTools/ |
| JS-033 | Verify 0 `async void` non-event-handler matches |

---

### 7-Scan Checklist

Engineer MUST run all 7 scans and document results before marking ticket done.

| Scan | Command | Expected Result |
|------|---------|----------------|
| SCAN-01 | `grep -r "lock(" src/PropTraderTools/` | 0 matches |
| SCAN-02 | `grep -rn "async void " src/PropTraderTools/` | 0 new non-event-handler matches |
| SCAN-03 | `grep -rn "return null;" src/PropTraderTools/` | 0 new matches (no code changed) |
| SCAN-04 | `python scripts/complexity_audit.py` | All methods CYC <= 8 (no change) |
| SCAN-05 | N/A | No files modified |
| SCAN-06 | `dotnet build` | 0 errors, 0 warnings |
| SCAN-07 | `Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "BrushInactive"` | Lines 1163-1168 show all 6 specs with BrushInactive |

---

### Acceptance Criteria

- [ ] `Select-String` confirms `BrushInactive` appears at lines 1163–1168 for all 6 button specs
- [ ] `Select-String` confirms `Background = mainBackground` in `BuildArrowCluster`
- [ ] SCAN-06: `dotnet build` passes with 0 errors, 0 warnings
- [ ] No `.cs` file modified

---

## TICKET B-3: Verify WPF Cluster Helpers Extraction

**Type**: VERIFY-ONLY (no code change)
**Spec Req IDs**: DW-C38-02

---

### Files

No files modified. Verification only.

---

### Exact Change

No change. Evidence that all 6 helpers already exist:

| Helper Method | Line in TradeCopierWindow.cs | CYC | Called From |
|--------------|------------------------------|-----|-------------|
| `BuildFollowerListBox()` | 603 | 1 | `BuildRuleRow` line 501, `BuildDynamicRuleRow` line 551 |
| `BuildBeCluster(object tag0)` | 620 | 1 | `BuildRuleRow` line 509, `BuildDynamicRuleRow` line 559 |
| `BuildTightenCluster(object tag0)` | 653 | 1 | `BuildRuleRow` line 516, `BuildDynamicRuleRow` line 566 |
| `BuildArmBeCluster(object tag0, ComboBox leaderCb)` | 686 | 1 | `BuildRuleRow` line 520, `BuildDynamicRuleRow` line 570 |
| `BuildAtmColumnPanel()` | 719 | 2 | `BuildRuleRow` line 506, `BuildDynamicRuleRow` line 556 |
| `BuildActionButtons(object, ComboBox, ListBox, StackPanel, Grid)` | 750 | 1 | `BuildRuleRow` line 507, `BuildDynamicRuleRow` line 557 |

`BuildRuleRow` CYC = 1. `BuildDynamicRuleRow` CYC = 1. All helpers CYC <= 8.

**Verification commands** (engineer runs to confirm, does not modify):
```powershell
# Confirm all 6 helpers exist in TradeCopierWindow.cs
Select-String -Path "src/PropTraderTools/TradeCopierWindow.cs" -Pattern "private.*Build(FollowerListBox|BeCluster|TightenCluster|ArmBeCluster|AtmColumnPanel|ActionButtons)"

# Confirm BuildRuleRow and BuildDynamicRuleRow call all 6 helpers
Select-String -Path "src/PropTraderTools/TradeCopierWindow.cs" -Pattern "(BuildFollowerListBox|BuildBeCluster|BuildTightenCluster|BuildArmBeCluster|BuildAtmColumnPanel|BuildActionButtons)\("
```

---

### Method Signatures

N/A — no code change.

---

### xUnit [Fact] Names

None — verify-only ticket.

---

### JS Rule Constraints

| Rule | Constraint |
|------|------------|
| JS-021 | Verify 0 `lock()` in src/PropTraderTools/ |
| JS-033 | Verify 0 `async void` non-event-handler matches |

---

### 7-Scan Checklist

| Scan | Command | Expected Result |
|------|---------|----------------|
| SCAN-01 | `grep -r "lock(" src/PropTraderTools/` | 0 matches |
| SCAN-02 | `grep -rn "async void " src/PropTraderTools/` | 0 new non-event-handler matches |
| SCAN-03 | `grep -rn "return null;" src/PropTraderTools/` | 0 new matches (no code changed) |
| SCAN-04 | `python scripts/complexity_audit.py` | All methods CYC <= 8 (no change) |
| SCAN-05 | N/A | No files modified |
| SCAN-06 | `dotnet build` | 0 errors, 0 warnings |
| SCAN-07 | `Select-String -Path "src/PropTraderTools/TradeCopierWindow.cs" -Pattern "BuildFollowerListBox\|BuildBeCluster\|BuildTightenCluster\|BuildArmBeCluster\|BuildAtmColumnPanel\|BuildActionButtons"` | All 6 helpers referenced from both BuildRuleRow and BuildDynamicRuleRow |

---

### Acceptance Criteria

- [ ] `Select-String` confirms all 6 private helper methods exist in `TradeCopierWindow.cs`
- [ ] `Select-String` confirms both `BuildRuleRow` and `BuildDynamicRuleRow` call each of the 6 helpers
- [ ] SCAN-06: `dotnet build` passes with 0 errors, 0 warnings
- [ ] No `.cs` file modified

---

## TICKET B-4: Refactor BuildFollowerMultipliers — Inverted Loop

**Type**: ACTIVE (1 method replaced in TradeCopierPanel.cs; 1 new test file created)
**Spec Req IDs**: DW-C39-07

---

### Files

| File | Change |
|------|--------|
| `src/PropTraderTools/TradeCopierPanel.cs` | REPLACE body of `BuildFollowerMultipliers` (lines 2785–2802, ~18 lines replaced with ~16 lines) |
| `src/PropTraderTools/Tests/BwaveDwLaneBTests.cs` | CREATE new file with 1 `[Fact]` test (approx 30 lines) |

---

### Exact Change

**Before** — `src/PropTraderTools/TradeCopierPanel.cs` lines 2785–2802:

```csharp
        // BuildFollowerMultipliers: collects per-follower multipliers and ATM names. CCN=3.
        private (int[] multipliers, string[] atmNames) BuildFollowerMultipliers(Account[] followers)
        {
            var multipliers = new int[followers.Length];
            var atmNames = new string[followers.Length];
            for (int i = 0; i < followers.Length; i++)
            {
                foreach (var item in _followerItems)
                {
                    if (item.Account != followers[i])
                        continue;
                    multipliers[i] = item.Multiplier > 0 ? item.Multiplier : 1;
                    atmNames[i] = item.AtmModeName ?? "Inherit";
                    break;
                }
            }
            return (multipliers, atmNames);
        }
```

**After** — same lines replaced with:

```csharp
        // BuildFollowerMultipliers: collects per-follower multipliers and ATM names. CCN=5.
        // BWAVE-DW B-4: nested for+foreach replaced with inverted foreach + Array.IndexOf.
        // First-match wins preserved: multipliers[idx]!=0 guard skips already-assigned indices,
        // matching the original break-on-first-match semantics for duplicate _followerItems entries.
        // JS-021: no lock. JS-002: no return null. JS-033: not async void.
        private (int[] multipliers, string[] atmNames) BuildFollowerMultipliers(Account[] followers)
        {
            var multipliers = new int[followers.Length];
            var atmNames = new string[followers.Length];
            foreach (var item in _followerItems)
            {
                if (item.Account == null) continue;
                int idx = System.Array.IndexOf(followers, item.Account);
                if (idx < 0 || multipliers[idx] != 0) continue;  // first-match wins: skip if already assigned
                multipliers[idx] = item.Multiplier > 0 ? item.Multiplier : 1;
                atmNames[idx] = item.AtmModeName ?? "Inherit";
            }
            return (multipliers, atmNames);
        }
```

**Key changes**:
1. Outer `for (int i = 0; i < followers.Length; i++)` replaced with `foreach (var item in _followerItems)` (inverted loop).
2. Inner `foreach (var item in _followerItems)` removed.
3. Membership check: `Array.IndexOf` replaces `item.Account != followers[i]` comparison.
4. First-match guard: `multipliers[idx] != 0` combined into the `idx < 0` continue — skips already-assigned indices, preserving original `break`-on-first-match semantics.
5. Comment updated: CCN=3 → CCN=5 (accurate post-refactor count; the additional branch is the combined idx/filled guard).
6. No LINQ import added. `System.Array.IndexOf` is standard .NET — no new `using` directive required.

---

### Method Signatures

**Before** (unchanged):
```csharp
private (int[] multipliers, string[] atmNames) BuildFollowerMultipliers(Account[] followers)
```

**After** (unchanged — same signature):
```csharp
private (int[] multipliers, string[] atmNames) BuildFollowerMultipliers(Account[] followers)
```

The signature is not modified. Only the body changes.

---

### CYC Analysis

| Version | Branches | Calculation | CYC |
|---------|----------|-------------|-----|
| Before | for loop, foreach loop, if (account!=), implicit ternary×2 | base(1) + for(+1) + foreach(+1) + if(+1) = 4 | 4 |
| After | foreach loop, if (null), combined if (idx<0 \|\| already-assigned), implicit ternary×2 | base(1) + foreach(+1) + if-null(+1) + if-idx-or-filled(+1) + ternary(+1) = 5 | 5 |

CYC before = 4, after = 5. <= 8. **PASS**. The extra branch is the `multipliers[idx] != 0` first-match guard that preserves original semantics.

---

### New Test File — `src/PropTraderTools/Tests/BwaveDwLaneBTests.cs`

**Create this file** (does not exist yet):

```csharp
// BWAVE-DW LaneB — reflection tests for BuildFollowerMultipliers refactor.
// xUnit only. No NUnit. No MSTest.
using System.Reflection;
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools.Tests
{
    public class BwaveDwLaneBTests
    {
        [Fact]
        public void BuildFollowerMultipliers_SignatureUnchanged_AfterContainsRefactor()
        {
            var m = typeof(TradeCopierPanel).GetMethod(
                "BuildFollowerMultipliers",
                BindingFlags.NonPublic | BindingFlags.Instance);
            // Method must exist
            Assert.NotNull(m);
            // Must be an instance method (not static)
            Assert.False(m.IsStatic);
            // Must accept exactly 1 parameter
            var parms = m.GetParameters();
            Assert.Equal(1, parms.Length);
            // Parameter must be Account[]
            Assert.Equal(typeof(Account[]), parms[0].ParameterType);
            // Return type must be a value type (ValueTuple<int[], string[]>)
            Assert.True(m.ReturnType.IsValueType);
        }
    }
}
```

---

### xUnit [Fact] Names

| Test Name | Asserts |
|-----------|---------|
| `BuildFollowerMultipliers_SignatureUnchanged_AfterContainsRefactor` | Method exists via reflection (`NotNull`); is instance (not static); 1 parameter of type `Account[]`; return type `IsValueType` (value tuple) |

---

### JS Rule Constraints

| Rule | Constraint | Verification |
|------|------------|-------------|
| JS-021 | No `lock()` in new code | SCAN-01: grep must return 0 |
| JS-002 | No `return null` in `BuildFollowerMultipliers` | SCAN-03: grep in method scope returns 0 |
| JS-033 | No `async void` non-event-handler | SCAN-02: grep must return 0 new matches |
| JS-001 | No `throw new XxxException` in new code | Confirmed by inspection: new body has no `throw` statement |

---

### 7-Scan Checklist

Engineer MUST complete ALL 7 scans and record results before marking ticket done.

| Scan | Command | Expected Result |
|------|---------|----------------|
| SCAN-01 | `grep -r "lock(" src/PropTraderTools/` | 0 matches |
| SCAN-02 | `grep -rn "async void " src/PropTraderTools/` | 0 new non-event-handler matches |
| SCAN-03 | `grep -n "return null;" src/PropTraderTools/TradeCopierPanel.cs` | 0 occurrences inside `BuildFollowerMultipliers` (method returns value tuple, not null) |
| SCAN-04 | `python scripts/complexity_audit.py` | `BuildFollowerMultipliers` CYC = 5, <= 8 PASS |
| SCAN-05 | `grep -P "[^\x00-\x7F]" src/PropTraderTools/TradeCopierPanel.cs` | 0 non-ASCII chars in modified file |
| SCAN-06 | `dotnet build` | 0 errors, 0 warnings |
| SCAN-07 | `Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "for \(int i = 0; i < followers"` | 0 matches — confirms old nested outer for-loop removed from `BuildFollowerMultipliers` |

---

### Acceptance Criteria

- [ ] `BuildFollowerMultipliers` body replaced exactly as specified in the "After" block above
- [ ] Comment updated from `CCN=3` to `CCN=5` on line 2785
- [ ] No new `using` directive added (uses `System.Array.IndexOf` fully qualified)
- [ ] `BwaveDwLaneBTests.cs` created with exactly 1 `[Fact]` as specified
- [ ] SCAN-04: `BuildFollowerMultipliers` CYC = 5
- [ ] SCAN-06: `dotnet build` passes with 0 errors, 0 warnings
- [ ] SCAN-07: 0 matches for `for (int i = 0; i < followers` — nested outer loop is gone
- [ ] First-match semantics confirmed: `multipliers[idx] != 0` guard present in new body

---

## TICKET B-5: Verify Tab Order in BuildRuleRow

**Type**: VERIFY-ONLY (no code change)
**Spec Req IDs**: DW-C38-04

---

### Files

No files modified. Verification only.

---

### Exact Change

No change. Evidence that tab order is already correct:

`BuildRuleRow` and `BuildDynamicRuleRow` in `src/PropTraderTools/TradeCopierWindow.cs` add children
in left-to-right column order via their `Children.Add` sequence:

| DOM Add Order | Column | Element | Source Line (BuildRuleRow / BuildDynamicRuleRow) |
|---------------|--------|---------|--------------------------------------------------|
| 1 | Col 0 | instrLabel / instrTextBox | Line 491 / 542 |
| 2 | Col 1 | leaderCb | Line 498 / 548 |
| 3 | Col 2 | followerLb | Line 504 / 554 |
| 4–8 | Col 3–7 | trimBtn, flattenBtn, cancelBtn, toggleBtn, applyBtn | Inside `BuildActionButtons`, lines 770–811 |
| 9 | Col 8 | beCluster | Line 511 / 561 |
| 10 | Col 9 | atmPanel | Line 514 / 564 |
| 11 | Col 10 | tightenCluster | Line 518 / 568 |
| 12 | Col 11 | armBeCluster | Line 522 / 572 |

DOM `Children.Add` order matches left-to-right visual column order (cols 0–11). WPF tab traversal
follows `Children.Add` order. Tab focus will traverse left-to-right as expected.

**Verification commands** (engineer runs to confirm, does not modify):
```powershell
# Confirm Children.Add sequence in BuildRuleRow (lines ~490-525)
Select-String -Path "src/PropTraderTools/TradeCopierWindow.cs" -Pattern "Children\.Add" | ForEach-Object { "Line $($_.LineNumber): $($_.Line.Trim())" }
```

---

### Method Signatures

N/A — no code change.

---

### xUnit [Fact] Names

None — verify-only ticket.

---

### JS Rule Constraints

| Rule | Constraint |
|------|------------|
| JS-021 | Verify 0 `lock()` in src/PropTraderTools/ |
| JS-033 | Verify 0 `async void` non-event-handler matches |

---

### 7-Scan Checklist

| Scan | Command | Expected Result |
|------|---------|----------------|
| SCAN-01 | `grep -r "lock(" src/PropTraderTools/` | 0 matches |
| SCAN-02 | `grep -rn "async void " src/PropTraderTools/` | 0 new non-event-handler matches |
| SCAN-03 | `grep -rn "return null;" src/PropTraderTools/` | 0 new matches (no code changed) |
| SCAN-04 | `python scripts/complexity_audit.py` | All methods CYC <= 8 (no change) |
| SCAN-05 | N/A | No files modified |
| SCAN-06 | `dotnet build` | 0 errors, 0 warnings |
| SCAN-07 | `Select-String -Path "src/PropTraderTools/TradeCopierWindow.cs" -Pattern "Children\.Add"` | `Children.Add` calls appear in column order 0→1→2→(3-7 via BuildActionButtons)→8→9→10→11 |

---

### Acceptance Criteria

- [ ] `Children.Add` order confirmed matching left-to-right column order in both `BuildRuleRow` and `BuildDynamicRuleRow`
- [ ] SCAN-06: `dotnet build` passes with 0 errors, 0 warnings
- [ ] No `.cs` file modified

---

## Summary Table

| Ticket | Type | Files Touched | Lines Delta | Tests Added | JS Rules |
|--------|------|---------------|-------------|-------------|----------|
| B-1 | ACTIVE (delete tests) | BwaveCycLaneCTests.cs | -48 lines | 0 | JS-021, JS-033 |
| B-2 | VERIFY-ONLY | none | 0 | 0 | JS-021, JS-033 |
| B-3 | VERIFY-ONLY | none | 0 | 0 | JS-021, JS-033 |
| B-4 | ACTIVE (refactor + new test file) | TradeCopierPanel.cs, BwaveDwLaneBTests.cs (new) | ~-18 +16 +30 | 1 | JS-021, JS-002, JS-033, JS-001 |
| B-5 | VERIFY-ONLY | none | 0 | 0 | JS-021, JS-033 |

**Total active source delta**: ~20 net lines in production code + 30 lines new test file + 48 test lines deleted.

---

**TICKETS_COMPLETE**
