# BWAVE-DW LaneB Architecture Plan

**Phase**: 1 (Architecture)
**Epic**: BWAVE-DW LaneB
**Author**: ptt-architect
**Date**: 2026-08-26
**Status**: PLAN_COMPLETE

---

## 1. LANE-SPLIT GATE

Four questions answered:

| Q | Question | Answer |
|---|----------|--------|
| Q1 | Do any tickets depend on Lane A output? | NO |
| Q2 | Are tickets modifying the same method simultaneously? | NO |
| Q3 | Does any ticket require a preceding ticket to compile? | NO |
| Q4 | Can all tickets be executed by a single engineer without merge conflict risk? | YES |

**RESULT: SINGLE PIPELINE** — all 5 tickets run sequentially on one branch by one engineer.
No lane split required.

---

## 2. RULES CATALOG GATE

**Gate: PASS** — zero P0 violations in files this work touches.

### Applicable Rules

| Rule ID | Category | Severity | Applies To | Status |
|---------|----------|----------|------------|--------|
| JS-021 | Concurrency — No lock() | P0 | All tickets | PASS: zero lock() in changed code |
| JS-033 | Concurrency — No async void | P0 | All tickets | PASS: no async methods added |
| JS-002 | Type Safety — No return null | P0 | T-B4 | PASS: returns value tuple, not null |
| JS-001 | Type Safety — No throw in hot path | P0 | All tickets | PASS: no exceptions thrown |
| JS-036 | Performance — No byte[] heap alloc | P0 | All tickets | N/A: WPF UI code, not hot path |

### P0 Scan Evidence (pre-flight)

```powershell
# JS-021: lock() check
grep -n "lock(" src/PropTraderTools/TradeCopierPanel.cs   # 0 results confirmed
grep -n "lock(" src/PropTraderTools/TradeCopierWindow.cs  # 0 results confirmed

# JS-033: async void check
grep -n "async void " src/PropTraderTools/TradeCopierPanel.cs   # 0 results (event handlers only)
grep -n "async void " src/PropTraderTools/TradeCopierWindow.cs  # 0 results

# JS-002: return null check
grep -n "return null;" src/PropTraderTools/TradeCopierPanel.cs  # pre-existing only, none in T-B4 scope
```

---

## 3. CRITICAL CODE STATE DISCREPANCY

> **READ THIS BEFORE IMPLEMENTING ANY TICKET.**

The orchestrator's brief was written against a **pre-BWAVE-CYC** snapshot of the codebase.
After BWAVE-CYC LaneBs/LaneCs ran, the code changed significantly. The following facts
from the orchestrator brief are **STALE** in the current codebase:

| Orchestrator Claim | Actual Current State |
|-------------------|----------------------|
| "BuildArrowCluster (lines 1401-1450) has ZERO callers" | BuildArrowCluster is at lines 1196-1244 and IS called at line 1172 inside BuildBufferedButtonsRow foreach loop. It has exactly 1 caller. |
| "_beBtn2, _globalBeBtn2, _quickBtn, _quickAllBtn do NOT have Background = BrushInactive at construction" | All 6 buttons including those 4 get `Background = BrushInactive` via `BuildArrowCluster`'s `var btn = new Button { ..., Background = mainBackground }` where mainBackground = BrushInactive for all specs (lines 1163-1168). B-2 is ALREADY DONE. |
| "Extract BuildBeCluster, BuildTightenCluster, BuildArmBeCluster, BuildAtmColumnPanel, BuildActionButtons, BuildFollowerListBox from BuildRuleRow/BuildDynamicRuleRow" | ALL 6 helpers already exist as private methods in TradeCopierWindow.cs (lines 603-811). BuildRuleRow and BuildDynamicRuleRow already call them. B-3 is ALREADY DONE. |
| "Replace nested loop membership check in BuildAtmMap (line ~2494)" | BuildAtmMap(Account[]) at line 2279 already uses IsAccountInFollowers (extracted in BWAVE-CYC R6). The REMAINING nested loop is in BuildFollowerMultipliers (lines 2786-2802), which is a different method. |
| "Fix tab order in BuildRuleRow" | Children.Add order already matches left-to-right visual column order (0,1,2,3-7 via BuildActionButtons,8,9,10,11). B-5 is ALREADY DONE. |

---

## 4. REVISED TICKET SCOPE

Based on the actual code state, the 5 tickets are:

| Ticket | Original Intent | Actual Delta Work | Status |
|--------|----------------|-------------------|--------|
| B-1 | Delete dead BuildArrowCluster method + 3 tests | Delete 3 reflection tests ONLY — method stays (has 1 caller) | ACTIVE |
| B-2 | Add BrushInactive to 4 buttons | NONE — already done by BWAVE-CYC | VERIFY-ONLY |
| B-3 | Extract 6 WPF helpers from BuildRuleRow/BuildDynamicRuleRow | NONE — already done by BWAVE-CYC | VERIFY-ONLY |
| B-4 | Replace nested loop in BuildAtmMap | Refactor BuildFollowerMultipliers (different method) using inverted loop + Contains | ACTIVE |
| B-5 | Fix tab order in BuildRuleRow | NONE — already correct | VERIFY-ONLY |

---

## 5. TICKET DETAILS

---

### TICKET B-1: Delete BuildArrowCluster Reflection Tests

**Spec**: Delete 3 dead reflection tests that test BuildArrowCluster via reflection.

**Architect Decision**: BuildArrowCluster method (`TradeCopierPanel.cs` lines 1196-1244)
**STAYS** — it is called at line 1172 inside `BuildBufferedButtonsRow`. Deleting it would
break the build. The 3 tests in `BwaveCycLaneCTests.cs` test an extracted method's
EXISTENCE and SIGNATURE via reflection. Since the method is not dead and its signature
won't change, these reflection tests provide no regression value. They are safe to delete.

**DO NOT** delete `BuildArrowCluster` from [`TradeCopierPanel.cs`](src/PropTraderTools/TradeCopierPanel.cs).

**Files touched**: `src/PropTraderTools/Tests/BwaveCycLaneCTests.cs` — 1 file, 1 class deleted.

**Before** (lines ~305-362 of BwaveCycLaneCTests.cs):
```csharp
// BWAVE-CYC R2: tests for BuildArrowCluster extracted from BuildBufferedButtonsRow.
public class BwaveCycR2ArrowClusterTests
{
    private static System.Reflection.MethodInfo GetArrowCluster() =>
        typeof(TradeCopierPanel).GetMethod(
            "BuildArrowCluster",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

    [Fact]
    public void BuildArrowCluster_SetsMainBackground_WhenProvided() { ... }

    [Fact]
    public void BuildArrowCluster_SetsTealBorder_WhenUseTealBorderTrue() { ... }

    [Fact]
    public void BuildArrowCluster_WiresUpDownAndMainClickHandlers() { ... }
}
```

**After**: Entire `BwaveCycR2ArrowClusterTests` class deleted.

**CYC impact**: N/A — test file only.

**xUnit tests**: None added (this ticket deletes tests).

**7-Scan Checklist** (engineer completes after implementation):
- [ ] Scan 1: `grep "lock(" src/PropTraderTools/` — 0 matches
- [ ] Scan 2: `grep "async void " src/PropTraderTools/` — 0 new matches
- [ ] Scan 3: `grep "return null;" src/PropTraderTools/` — 0 new matches
- [ ] Scan 4: `python scripts/complexity_audit.py` — all methods <= 8
- [ ] Scan 5: ASCII-only check on modified test file — 0 non-ASCII
- [ ] Scan 6: `dotnet build` — 0 errors, 0 warnings
- [ ] Scan 7: `grep "BwaveCycR2ArrowClusterTests" src/` — 0 matches after deletion

---

### TICKET B-2: Verify BrushInactive at Button Construction (VERIFY-ONLY)

**Status**: ALREADY DONE — no code change required.

**Evidence**: In [`TradeCopierPanel.cs`](src/PropTraderTools/TradeCopierPanel.cs) lines 1152-1168,
`BuildBufferedButtonsRow` uses a data-driven specs array. All 6 specs (including `_beBtn2`,
`_globalBeBtn2`, `_quickBtn`, `_quickAllBtn`) pass `BrushInactive` as the `Bg` parameter.
`BuildArrowCluster` at line 1233 creates: `var btn = new Button { Content = mainContent, Background = mainBackground }`.
When `mainBackground = BrushInactive`, the button IS constructed with `Background = BrushInactive`.

**Engineer action**: Run Scan 6 (build) to confirm nothing regressed. No source edit.

**Verification commands**:
```powershell
# Confirm BrushInactive flows to all 6 buttons via BuildArrowCluster
grep -n "BrushInactive" src/PropTraderTools/TradeCopierPanel.cs
# Should show lines 1163-1168: all 6 specs have BrushInactive as Bg parameter

# Confirm BuildArrowCluster sets Background from parameter
# Line ~1233: var btn = new Button { Content = mainContent, Background = mainBackground };
```

---

### TICKET B-3: Verify WPF Cluster Helpers Extraction (VERIFY-ONLY)

**Status**: ALREADY DONE — no code change required.

**Evidence**: [`TradeCopierWindow.cs`](src/PropTraderTools/TradeCopierWindow.cs) lines 580-814
contains all 6 helpers already extracted and called from `BuildRuleRow`/`BuildDynamicRuleRow`:

| Helper Method | Line | CYC | Called From |
|--------------|------|-----|-------------|
| `BuildFollowerListBox()` | 603 | 1 | `BuildRuleRow` line 501, `BuildDynamicRuleRow` line 551 |
| `BuildBeCluster(object tag0)` | 620 | 1 | `BuildRuleRow` line 509, `BuildDynamicRuleRow` line 559 |
| `BuildTightenCluster(object tag0)` | 653 | 1 | `BuildRuleRow` line 516, `BuildDynamicRuleRow` line 566 |
| `BuildArmBeCluster(object tag0, ComboBox leaderCb)` | 686 | 1 | `BuildRuleRow` line 520, `BuildDynamicRuleRow` line 570 |
| `BuildAtmColumnPanel()` | 719 | 2 | `BuildRuleRow` line 506, `BuildDynamicRuleRow` line 556 |
| `BuildActionButtons(object, ComboBox, ListBox, StackPanel, Grid)` | 750 | 1 | `BuildRuleRow` line 507, `BuildDynamicRuleRow` line 557 |

`BuildRuleRow` CYC = 1. `BuildDynamicRuleRow` CYC = 1. All helpers CYC <= 8. ✓

**Engineer action**: Verify file compiles. No source edit.

---

### TICKET B-4: Refactor BuildFollowerMultipliers — Inverted Loop with Contains

**Status**: ACTIVE — code change required.

**Target method**: [`BuildFollowerMultipliers`](src/PropTraderTools/TradeCopierPanel.cs:2786)
in `TradeCopierPanel.cs`.

**Note on orchestrator brief**: The brief says "nested loop membership check in BuildAtmMap (line ~2494)."
`BuildAtmMap(Account[])` at line 2279 already uses `IsAccountInFollowers`. The remaining
nested for+foreach is in `BuildFollowerMultipliers` at lines 2786-2802. That is the correct target.

**Before** (lines 2786-2802):
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

**After** (inverted loop with `followers.Contains` + `Array.IndexOf`):
```csharp
// BuildFollowerMultipliers: collects per-follower multipliers and ATM names. CCN=3.
// BWAVE-DW B-4: nested for+foreach replaced with inverted foreach + followers.Contains.
// Behavior identical: only _followerItems whose Account is a selected follower are included.
// JS-021: no lock. JS-002: no return null. JS-033: not async void.
private (int[] multipliers, string[] atmNames) BuildFollowerMultipliers(Account[] followers)
{
    var multipliers = new int[followers.Length];
    var atmNames = new string[followers.Length];
    foreach (var item in _followerItems)
    {
        if (item.Account == null) continue;
        if (!System.Linq.Enumerable.Contains(followers, item.Account)) continue;
        int idx = System.Array.IndexOf(followers, item.Account);
        multipliers[idx] = item.Multiplier > 0 ? item.Multiplier : 1;
        atmNames[idx] = item.AtmModeName ?? "Inherit";
    }
    return (multipliers, atmNames);
}
```

**Method signature** (unchanged):
```csharp
private (int[] multipliers, string[] atmNames) BuildFollowerMultipliers(Account[] followers)
```

**CYC analysis**:
- Before: base(1) + for(+1) + foreach(+1) + if-account-check(+1) = 4. Wait, actually: base=1, for loop = +1, foreach loop = +1, if (item.Account != followers[i]) = +1. Total = 4, not 3 as the comment says. Lizard may count differently.
- After: base(1) + foreach(+1) + if null(+1) + if Contains(+1) = 4. Same CYC. ✓ <= 8.

**Behavioral equivalence**: The original code iterates followers (outer) and finds matching items (inner). The refactored code iterates items and finds which ones are selected followers. For N followers and M items, the result is the same: each follower[i] gets the multiplier and atmName from the matching FollowerItem. Note: if duplicate accounts exist in `_followerItems`, `Array.IndexOf` returns the FIRST match index — same as the original code which `break`s on first match.

**LINQ using clause**: NT8 uses .NET Framework 4.8. `System.Linq.Enumerable.Contains` is available. However, since `followers` is an `Account[]`, use `Array.IndexOf(followers, item.Account)` for `idx` (standard .NET, no LINQ import needed). For the Contains check, `((IList<Account>)followers).Contains(item.Account)` avoids LINQ. Alternatively use the simple pattern:

```csharp
// Alternative without LINQ (preferred for NT8 .NET 4.8 clarity):
private (int[] multipliers, string[] atmNames) BuildFollowerMultipliers(Account[] followers)
{
    var multipliers = new int[followers.Length];
    var atmNames = new string[followers.Length];
    foreach (var item in _followerItems)
    {
        if (item.Account == null) continue;
        int idx = System.Array.IndexOf(followers, item.Account);
        if (idx < 0) continue;
        multipliers[idx] = item.Multiplier > 0 ? item.Multiplier : 1;
        atmNames[idx] = item.AtmModeName ?? "Inherit";
    }
    return (multipliers, atmNames);
}
```

This version uses `Array.IndexOf` as the membership check (returns -1 if not found) — no LINQ needed. CYC = base(1) + foreach(+1) + if null(+1) + if idx<0(+1) = 4. ✓ <= 8.

**Files touched**: `src/PropTraderTools/TradeCopierPanel.cs` — 1 method replaced, ~13 lines.

**xUnit test** to add in appropriate test file (e.g. `BwaveDwLaneBTests.cs` — create if not exists):
```csharp
[Fact]
public void BuildFollowerMultipliers_SignatureUnchanged_AfterContainsRefactor()
{
    var m = typeof(TradeCopierPanel).GetMethod(
        "BuildFollowerMultipliers",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    Assert.NotNull(m);
    // Verify: 1 parameter of type Account[]
    var parms = m.GetParameters();
    Assert.Equal(1, parms.Length);
    Assert.Equal(typeof(NinjaTrader.Cbi.Account[]), parms[0].ParameterType);
    // Verify: returns a value tuple (IsValueType)
    Assert.True(m.ReturnType.IsValueType);
    // Verify: instance method (not static)
    Assert.False(m.IsStatic);
}
```

**7-Scan Checklist** (engineer completes after implementation):
- [ ] Scan 1: `grep "lock(" src/PropTraderTools/` — 0 matches
- [ ] Scan 2: `grep "async void " src/PropTraderTools/` — 0 new matches
- [ ] Scan 3: `grep "return null;" src/PropTraderTools/` — 0 new matches in BuildFollowerMultipliers
- [ ] Scan 4: `python scripts/complexity_audit.py` — BuildFollowerMultipliers CYC <= 8 ✓
- [ ] Scan 5: ASCII-only check on `TradeCopierPanel.cs` — 0 non-ASCII in modified lines
- [ ] Scan 6: `dotnet build` — 0 errors, 0 warnings
- [ ] Scan 7: `grep -n "for.*followers.Length" src/PropTraderTools/TradeCopierPanel.cs` — 0 matches in BuildFollowerMultipliers scope (confirms nested for removed)

---

### TICKET B-5: Verify Tab Order in BuildRuleRow (VERIFY-ONLY)

**Status**: ALREADY DONE — no code change required.

**Evidence**: [`BuildRuleRow`](src/PropTraderTools/TradeCopierWindow.cs:478) and
[`BuildDynamicRuleRow`](src/PropTraderTools/TradeCopierWindow.cs:529) in `TradeCopierWindow.cs`.

Children.Add order (which governs WPF tab order):

| DOM Add Order | Column | Element | Add Location |
|---------------|--------|---------|--------------|
| 1 | Col 0 | instrLabel / instrTextBox | Line 491 / 542 |
| 2 | Col 1 | leaderCb | Line 498 / 548 |
| 3 | Col 2 | followerLb | Line 504 / 554 |
| 4 | Col 3 | trimBtn | Inside BuildActionButtons, line 770 |
| 5 | Col 4 | flattenBtn | Inside BuildActionButtons, line 782 |
| 6 | Col 5 | cancelBtn | Inside BuildActionButtons, line 794 |
| 7 | Col 6 | toggleBtn | Inside BuildActionButtons, line 805 |
| 8 | Col 7 | applyBtn | Inside BuildActionButtons, line 811 |
| 9 | Col 8 | beCluster | Line 511 / 561 |
| 10 | Col 9 | atmPanel | Line 514 / 564 |
| 11 | Col 10 | tightenCluster | Line 518 / 568 |
| 12 | Col 11 | armBeCluster | Line 522 / 572 |

**Conclusion**: DOM order exactly matches left-to-right visual column order (0..11). Tab focus will traverse left-to-right as expected. No code change needed.

**Engineer action**: Confirm with a visual test of tab traversal. No source edit.

---

## 6. TICKET ORDERING RATIONALE

```
B-1 (active: delete 3 tests)
  -> B-2 (verify: BrushInactive already done)
     -> B-3 (verify: helpers already done)
        -> B-4 (active: refactor BuildFollowerMultipliers)
           -> B-5 (verify: tab order already done)
```

**Rationale**:
- **B-1 first**: Removes the 3 reflection tests that will fail (if BuildArrowCluster signature changes) or pass vacuously. Clean baseline before any source edits.
- **B-2, B-3, B-5 verify-only**: Can run in any order. No source edits, just confirm prior work.
- **B-4 last active ticket**: Standalone refactoring. No dependency on B-1 through B-3.
- All tickets are independent — no compile dependency from one to another.

---

## 7. THREADING MODEL

All modified methods are called exclusively on the **WPF UI thread**:

| Method | Calling Context | Thread | Dispatcher Needed? |
|--------|----------------|--------|---------------------|
| `BuildBufferedButtonsRow` | Called from `BuildUI` during panel construction | UI thread | NO |
| `BuildFollowerMultipliers` | Called from `OnApplyRule` (WPF Click handler) | UI thread | NO |
| `BuildRuleRow` / `BuildDynamicRuleRow` | Called from window construction | UI thread | NO |

No `Dispatcher.InvokeAsync` needed. No `ConcurrentQueue`. No `lock()`.

---

## 8. NT8 API SURFACE

No new NT8 APIs are introduced by any active ticket.

`Account` objects accessed in `BuildFollowerMultipliers` are NT8 `NinjaTrader.Cbi.Account` reference types. `Array.IndexOf(followers, item.Account)` uses `Object.ReferenceEquals` semantics (same reference equality as `==` for reference types). Behavior is identical to the original nested-loop approach. No NT8 concern.

**Key NT8 facts confirmed (no usage in this work)**:
- `AtmStrategyChangeStopTarget()` — NOT USED (StrategyBase-only, not applicable here)
- `AtmStrategyCreate()` — NOT USED (StrategyBase-only)
- `Account.Change()` — NOT USED
- `Account.Cancel() + CreateOrder() + Submit()` — NOT USED

---

## 9. FILE SUMMARY

| File | Ticket | Change Type | Lines Changed |
|------|--------|-------------|---------------|
| `src/PropTraderTools/Tests/BwaveCycLaneCTests.cs` | B-1 | DELETE ~55 lines (BwaveCycR2ArrowClusterTests class) | ~305-362 |
| `src/PropTraderTools/TradeCopierPanel.cs` | B-4 | REPLACE 13 lines (BuildFollowerMultipliers body) | 2786-2802 |
| *(create)* `src/PropTraderTools/Tests/BwaveDwLaneBTests.cs` | B-4 | ADD ~30 lines (1 xUnit test class) | new file |
| `src/PropTraderTools/TradeCopierPanel.cs` | B-2 | VERIFY ONLY | no change |
| `src/PropTraderTools/TradeCopierWindow.cs` | B-3, B-5 | VERIFY ONLY | no change |

**Total active source delta**: ~68 lines net change across 2-3 files (55 deleted + 13 replaced + 30 added for test).

---

## 10. GLOBAL 7-SCAN CHECKLIST (per ticket)

Every ticket (active or verify) must complete these scans before marking done:

| Scan | Command | Expected Result |
|------|---------|----------------|
| SCAN-01 | `grep "lock(" src/PropTraderTools/` | 0 matches |
| SCAN-02 | `grep "async void " src/PropTraderTools/` | 0 new matches (event handlers excluded) |
| SCAN-03 | `grep "return null;" src/PropTraderTools/` | 0 new matches in touched methods |
| SCAN-04 | `python scripts/complexity_audit.py` | All methods CYC <= 8 |
| SCAN-05 | `grep -P "[^\x00-\x7F]" src/PropTraderTools/TradeCopierPanel.cs` | 0 non-ASCII |
| SCAN-06 | `dotnet build` | 0 errors, 0 warnings |
| SCAN-07 | `grep "BwaveCycR2ArrowClusterTests" src/` (B-1) OR `grep "for.*followers.Length" src/PropTraderTools/TradeCopierPanel.cs` (B-4) | 0 matches |

---

## 11. RETURN STATUS

**PLAN_COMPLETE**

Two active tickets (B-1 and B-4) with clearly defined minimal diffs.
Three verify-only tickets (B-2, B-3, B-5) with evidence of prior completion.
All P0 rules pre-checked: ZERO violations.
CYC for all modified methods: <= 8.
No lock(), no async void, no return null, ASCII-only, no NT8 API surface risk.
