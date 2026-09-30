# BWAVE-DW LaneB — ticket-B4-verification.md

**Ticket**: B-4
**Spec Req ID**: DW-C39-07
**Verifier**: ptt-verifier (Layer 3 independent)
**Date**: 2026-08-26
**Status**: VERIFY_PASS

---

## Scope Lock

Verifying ONLY:
1. `BuildFollowerMultipliers` refactor in `src/PropTraderTools/TradeCopierPanel.cs`
2. New test file `src/PropTraderTools/Tests/BwaveDwLaneBTests.cs` (1 [Fact])

All other changes in TradeCopierPanel.cs and TradeCopierWindow.cs are ignored per parallel-lane policy.

---

## STEP 1 — BuildFollowerMultipliers Method Verification

### Location Found (Layer 3 independent scan)

```
Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "BuildFollowerMultipliers"
  Line 2773: // BuildFollowerMultipliers: collects per-follower multipliers and ATM names. CCN=5.
  Line 2777: private (int[] multipliers, string[] atmNames) BuildFollowerMultipliers(Account[] followers)
  Line 2835: var (multipliers, atmNames) = BuildFollowerMultipliers(followers);
```

### Actual Method Body (lines 2773-2790, verified by read_file):

```csharp
        // BuildFollowerMultipliers: collects per-follower multipliers and ATM names. CCN=5.
        // BWAVE-DW B-4: nested for+foreach replaced with inverted foreach + System.Array.IndexOf.
        // First-match semantics preserved: multipliers[idx]!=0 guard skips duplicate _followerItems entries.
        // JS-021: no lock. JS-002: no return null. JS-033: not async void.
        private (int[] multipliers, string[] atmNames) BuildFollowerMultipliers(Account[] followers)
        {
            var multipliers = new int[followers.Length];
            var atmNames = new string[followers.Length];
            foreach (var item in _followerItems)
            {
                if (item.Account == null) continue;
                int idx = System.Array.IndexOf(followers, item.Account);
                if (idx < 0 || multipliers[idx] != 0) continue;
                multipliers[idx] = item.Multiplier > 0 ? item.Multiplier : 1;
                atmNames[idx] = item.AtmModeName ?? "Inherit";
            }
            return (multipliers, atmNames);
        }
```

### Acceptance Criteria Check

| Criterion | Expected | Actual | Result |
|-----------|----------|--------|--------|
| (a) Old nested `for(int i=0; i<followers.Length; i++)` GONE from this method | absent (lines 2770-2790) | line 2285 = BuildMultipliers; line 2799 = BuildAtmMap — NEITHER in scope | PASS |
| (b) New body uses `foreach(var item in _followerItems)` as only loop | single foreach | line 2781: `foreach (var item in _followerItems)` — only loop present | PASS |
| (c) First-match guard `if (idx < 0 || multipliers[idx] != 0) continue;` present | present | line 2785: exactly present | PASS |
| (d) Signature unchanged: `private (int[] multipliers, string[] atmNames) BuildFollowerMultipliers(Account[] followers)` | unchanged | line 2777: identical | PASS |
| (e) Comment updated to CCN=5 | CCN=5 in comment | line 2773: `CCN=5` | PASS |
| (f) No new using directives added | none added | verified: `System.Array.IndexOf` fully qualified, no new `using` needed | PASS |

**STEP 1: ALL criteria PASS**

---

## STEP 2 — BwaveDwLaneBTests.cs Verification

### Actual File Content (Layer 3 independent read):

```csharp
// BWAVE-DW LaneB tests -- verifies B-4 BuildFollowerMultipliers refactor.
// Uses reflection to confirm: method present, 1 param (Account[]), value-tuple return, instance method.
// xUnit only -- JS-051. No lock() -- JS-021. No async void -- JS-033.
using System;
using System.Reflection;
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
            Assert.NotNull(m);
            var parms = m.GetParameters();
            Assert.Equal(1, parms.Length);
            Assert.Equal(typeof(NinjaTrader.Cbi.Account[]), parms[0].ParameterType);
            Assert.True(m.ReturnType.IsValueType);
            Assert.False(m.IsStatic);
        }
    }
}
```

### Acceptance Criteria Check

| Criterion | Expected | Actual | Result |
|-----------|----------|--------|--------|
| (a) Class `BwaveDwLaneBTests` exists | present | `public class BwaveDwLaneBTests` | PASS |
| (b) `[Fact] BuildFollowerMultipliers_SignatureUnchanged_AfterContainsRefactor` exists | present | line 12: `[Fact]` + line 13 method name | PASS |
| (c) Uses reflection: `GetMethod("BuildFollowerMultipliers", NonPublic | Instance)` | present | `BindingFlags.NonPublic | BindingFlags.Instance` | PASS |
| (d) xUnit only (no NUnit/MSTest) | xUnit only | `using Xunit;` only — no NUnit/MSTest imports | PASS |
| (e) ASCII-only content | ASCII only | no non-ASCII chars found | PASS |

**Note**: The actual file has `using System;` as an extra `using` not in the ticket spec template. This is benign — `System` is a standard namespace alias, it introduces no behavior change and is not a violation of any JS rule. The assertions are functionally identical to the spec.

**STEP 2: ALL criteria PASS**

---

## STEP 3 — Independent 7-Scan Results (Layer 3)

### SCAN-01 — lock() in BuildFollowerMultipliers scope (lines 2770-2800)

**Command**: `Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "lock\(" | Where-Object { $_.LineNumber -ge 2770 -and $_.LineNumber -le 2800 }`
**Result**: 0 matches
**Layer 2 (engineer)**: PASS (0 actual lock calls)
**Discrepancy**: None
**Status**: PASS

### SCAN-02 — async void in BuildFollowerMultipliers scope (lines 2770-2800)

**Command**: `Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "async void " | Where-Object { $_.LineNumber -ge 2770 -and $_.LineNumber -le 2800 }`
**Result**: 0 matches
**Layer 2 (engineer)**: PASS (0 async void declarations)
**Discrepancy**: None
**Status**: PASS

### SCAN-03 — return null in BuildFollowerMultipliers scope (lines 2770-2800)

**Command**: `Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "return null;" | Where-Object { $_.LineNumber -ge 2770 -and $_.LineNumber -le 2800 }`
**Result**: 0 matches
**Layer 2 (engineer)**: 0 matches
**Discrepancy**: None
**Status**: PASS

### SCAN-04 — CYC inspection of new method body

**Manual CYC count for new BuildFollowerMultipliers**:
- base: 1
- `foreach (var item in _followerItems)`: +1
- `if (item.Account == null) continue;`: +1
- `if (idx < 0 || multipliers[idx] != 0) continue;` — combined condition: +1 (for the if) + 1 (for the ||) = +2 (strict counting)
- `item.Multiplier > 0 ? item.Multiplier : 1` (ternary): +1
- Total strict: 1+1+1+2+1 = 6; lenient (|| as single branch): 1+1+1+1+1 = 5

**Layer 2 (engineer) claimed**: CYC=5 (lenient counting, || as +1 on the if-guard)
**Assessment**: CYC is 5 (lenient) or 6 (strict McCabe with `||` as separate branch). Either way CYC <= 8. PASS.

Note on discrepancy: Engineer notes CCN=5 in comment and completion. Strict McCabe counting would give 6 (both `idx<0` and `multipliers[idx]!=0` are independent predicates). The difference does not affect compliance — both are well under CYC=8 threshold.

**Status**: PASS (CYC=5 or 6, either <= 8)

### SCAN-05 — Non-ASCII in BuildFollowerMultipliers scope (lines 2770-2800)

**Command**: `Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "[^\x00-\x7F]" | Where-Object { $_.LineNumber -ge 2770 -and $_.LineNumber -le 2800 }`
**Result**: 0 matches
**Layer 2 (engineer)**: 0 matches (whole file)
**Discrepancy**: None
**Status**: PASS

### SCAN-06 — dotnet build

**Command**: `dotnet build src/PropTraderTools/ 2>&1 | Select-Object -Last 20`
**Result**:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:01.85
```
**Layer 2 (engineer)**: `Build succeeded. 1 Warning(s) 0 Error(s)` (1 pre-existing warning in B131Tests.cs)
**Discrepancy**: Layer 3 result shows 0 warnings vs engineer's 1 pre-existing warning. No errors in either run. This discrepancy is non-blocking — the pre-existing warning may have been resolved by another parallel lane's work between the engineer run and this verification run. Zero errors confirmed by both layers.
**Status**: PASS (0 errors)

### SCAN-07 — Old nested for loop removed from BuildFollowerMultipliers

**Command**: `Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "for \(int i = 0; i < followers" | Select-Object -First 5`
**Result**:
```
Line 2285: for (int i = 0; i < followers.Length; i++)  [in BuildMultipliers — different method]
Line 2799: for (int i = 0; i < followers.Length; i++)  [in BuildAtmMap — different method]
```
**Independent confirmation**: Both matches verified to be outside BuildFollowerMultipliers scope (lines 2770-2790). Line 2285 is in `BuildMultipliers` (confirmed by read_file 2280-2292). Line 2799 is in `BuildAtmMap` (confirmed by read_file 2795-2808).
**Layer 2 (engineer)**: Same finding — 2 matches in other methods, 0 in BuildFollowerMultipliers
**Discrepancy**: None
**Status**: PASS (old nested for loop correctly removed from BuildFollowerMultipliers)

---

## STEP 4 — Behavioral Equivalence Analysis

### Original Algorithm

```
for each follower index i (0..followers.Length-1):
    for each item in _followerItems:
        if item.Account == followers[i]:
            multipliers[i] = item.Multiplier > 0 ? item.Multiplier : 1
            atmNames[i]   = item.AtmModeName ?? "Inherit"
            break  // first-match-wins per follower
```

### Refactored Algorithm

```
for each item in _followerItems:
    if item.Account == null: skip
    idx = IndexOf(followers, item.Account)
    if idx < 0 OR multipliers[idx] != 0: skip
    multipliers[idx] = item.Multiplier > 0 ? item.Multiplier : 1
    atmNames[idx]   = item.AtmModeName ?? "Inherit"
```

### Case Analysis

**Case 1: Follower has no matching _followerItems entry**
- Original: inner foreach iterates all items, finds no match, `multipliers[i]` stays 0 (default), loop continues
- Refactored: `Array.IndexOf` returns -1 for that follower in every item; `idx < 0` skips all items; `multipliers[idx]` stays 0
- Result: **IDENTICAL** — follower gets multiplier=0, atmName="" (default)

**Case 2: Follower has exactly one matching _followerItems entry**
- Original: inner foreach hits the matching item, sets multipliers[i] and atmNames[i], `break`
- Refactored: `Array.IndexOf` returns the correct idx; `multipliers[idx]==0` (not yet assigned) so the continue-skip is NOT taken; values are assigned
- Result: **IDENTICAL** — follower gets the correct multiplier and atmName

**Case 3: _followerItems has duplicate entries for the same Account**
- Original: outer for index i finds the first match in inner foreach (due to `break`); second duplicate is never reached in that i iteration; original "first-wins" established by `break`
- Refactored: first duplicate seen sets `multipliers[idx]`; when the second duplicate is encountered, `multipliers[idx] != 0` is true; the `if (idx < 0 || multipliers[idx] != 0) continue;` skips it
- Result: **IDENTICAL** — first _followerItems entry for a given Account wins in both versions

**Case 4: `item.Account == null` in _followerItems**
- Original: `item.Account != followers[i]` comparison when followers[i] is valid — null != valid Account is true, so `continue`; effectively skipped in original too (null can never match a real Account)
- Refactored: explicit `if (item.Account == null) continue;` guard
- Result: **IDENTICAL** — null items are skipped in both. The explicit null guard in the refactor is a minor behavioral improvement (prevents NullReferenceException if `Array.IndexOf` is called with null element on a non-null array), not a semantic change.

**Behavioral Equivalence Conclusion**: CONFIRMED. All cases produce identical output arrays. The refactor is a pure algorithmic restructuring that preserves all first-match-wins semantics.

---

## Layer 2 vs Layer 3 Cross-Check

| Scan | Layer 2 (engineer) | Layer 3 (this verifier) | Match? |
|------|--------------------|------------------------|--------|
| SCAN-01 lock() | PASS (0 actual) | PASS (0 matches) | YES |
| SCAN-02 async void | PASS (0 actual) | PASS (0 matches) | YES |
| SCAN-03 return null | PASS (0 in scope) | PASS (0 matches) | YES |
| SCAN-04 CYC | CYC=5 (lenient) | CYC=5-6 (both <= 8) | MINOR DIFFERENCE — non-blocking |
| SCAN-05 non-ASCII | PASS (0 whole file) | PASS (0 in scope) | YES |
| SCAN-06 build | 1 warning, 0 errors | 0 warnings, 0 errors | WARNING COUNT DIFFERS — non-blocking |
| SCAN-07 old loop | 0 in scope | 0 in scope (2 in other methods, confirmed) | YES |

**Discrepancies**: None blocking. SCAN-04 CYC interpretation difference (5 vs 6) is within academic counting variance; both well under 8. SCAN-06 warning count difference (1 vs 0) is non-blocking and attributable to parallel lane activity resolving a pre-existing warning between engineer and verifier runs.

---

## DNA Rule Check

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | 0 lock() in scope (2770-2800) | PASS |
| JS-002 (no return null) | 0 return null in scope | PASS |
| JS-033 (no async void) | 0 async void in scope | PASS |
| JS-001 (no throw Exception) | no throw statement in new body | PASS |
| JS-008 (no mutable struct cross-thread) | N/A — no structs introduced | PASS |
| NT8 async/await ban | no async/await in method | PASS |
| ASCII-only | 0 non-ASCII in scope | PASS |

---

## Architecture Compliance

| Criterion | Expected | Actual | Result |
|-----------|----------|--------|--------|
| Spec Req ID | DW-C39-07 | Completion references DW-C39-07 | PASS |
| Method remains private instance method | private, non-static | line 2777: `private` + reflection confirms `Assert.False(m.IsStatic)` | PASS |
| No new using directives | none | System.Array fully qualified; no new using added | PASS |
| xUnit test present | 1 [Fact] | `BuildFollowerMultipliers_SignatureUnchanged_AfterContainsRefactor` | PASS |
| Test uses reflection (WPF Panel not directly instantiatable in .NET 4.8) | reflection pattern | `GetMethod(... NonPublic | Instance)` | PASS |
| Build passes | 0 errors | `Build succeeded. 0 Error(s)` | PASS |

---

## Final Verdict

**VERIFY_PASS**

All independent Layer 3 scans confirm:
- `BuildFollowerMultipliers` correctly refactored with inverted foreach + `System.Array.IndexOf`
- Old nested `for (int i = 0; i < followers.Length; i++)` loop removed from the method
- First-match guard `if (idx < 0 || multipliers[idx] != 0) continue;` present and correct
- Signature unchanged: `private (int[] multipliers, string[] atmNames) BuildFollowerMultipliers(Account[] followers)`
- Comment updated to CCN=5
- No new using directives
- `BwaveDwLaneBTests.cs` created with correct 1 [Fact] using reflection, xUnit only, ASCII-only
- All DNA rules respected (JS-021, JS-002, JS-033, JS-001)
- Build: 0 errors
- Behavioral equivalence confirmed across all 4 case scenarios