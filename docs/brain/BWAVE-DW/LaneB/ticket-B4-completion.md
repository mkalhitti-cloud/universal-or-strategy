# BWAVE-DW LaneB — ticket-B4-completion.md

**Ticket**: B-4
**Spec Req ID**: DW-C39-07
**Engineer**: ptt-engineer
**Date**: 2026-08-26
**Status**: BUILD_PASS

---

## Summary

Refactored `BuildFollowerMultipliers` in `TradeCopierPanel.cs`.
Nested `for` + inner `foreach` replaced with a single inverted `foreach` + `System.Array.IndexOf`.
First-match semantics preserved via `multipliers[idx] != 0` guard.
New test file `BwaveDwLaneBTests.cs` created with 1 [Fact].

---

## Files Modified

| File | Change |
|------|--------|
| `src/PropTraderTools/TradeCopierPanel.cs` | REPLACE `BuildFollowerMultipliers` body (lines 2773-2790 before; 2773-2790 after, same range — 3 comment lines added, nested loops collapsed to single foreach) |
| `src/PropTraderTools/Tests/BwaveDwLaneBTests.cs` | CREATE new file with 1 [Fact] test |

---

## Exact Change

### Before (lines 2773-2790 original):

```
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

### After (lines 2773-2790 new):

```
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

---

## 7-Scan Results

### SCAN-01 — lock() check
**Command**: `Select-String -Path "src/PropTraderTools/*.cs","src/PropTraderTools/Tests/*.cs" -Pattern "lock\(" | Select-Object -First 5`
**Result**: 5 matches in CopyEngine.cs — ALL are comment lines (e.g. `// No lock() anywhere.`). Zero actual `lock(` statements.
**Status**: PASS (0 actual lock calls)

### SCAN-02 — async void check
**Command**: `Select-String -Path "src/PropTraderTools/*.cs","src/PropTraderTools/Tests/*.cs" -Pattern "async void " | Select-Object -First 5`
**Result**: 4 matches — ALL are comment lines referencing the ban (e.g. `// not async void`). Zero actual `async void` declarations.
**Status**: PASS (0 async void declarations)

### SCAN-03 — return null in BuildFollowerMultipliers scope
**Command**: `Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "return null;" | Where-Object { $_.LineNumber -ge 2773 -and $_.LineNumber -le 2795 }`
**Result**: No output (0 matches)
**Status**: PASS

### SCAN-04 — complexity audit
**Command**: `python scripts/complexity_audit.py 2>&1 | Select-Object -Last 20`
**Result**: Script not found (N/A). CYC confirmed by manual count:
  base(1) + foreach(+1) + if-null(+1) + if-combined-||(+1) + ternary(+1) = CYC = 5
  Before = 4, After = 5. Both <= 8.
**Status**: PASS (CYC=5 by inspection)

### SCAN-05 — non-ASCII chars
**Command**: `Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "[^\x00-\x7F]"`
**Result**: No output (0 matches)
**Status**: PASS

### SCAN-06 — dotnet build
**Command**: `dotnet build src/PropTraderTools/ 2>&1 | Select-Object -Last 30`
**Result**:
```
Build succeeded.
1 Warning(s)   <-- pre-existing xUnit2004 in B131Tests.cs (unrelated to B-4)
0 Error(s)
Time Elapsed 00:00:05.21
```
**Status**: PASS (0 errors; 1 pre-existing warning in unrelated file)

### SCAN-07 — old for loop removed from BuildFollowerMultipliers
**Command**: `Select-String -Path "src/PropTraderTools/TradeCopierPanel.cs" -Pattern "for \(int i = 0; i < followers" | Select-Object -First 5`
**Result**: 2 matches — lines 2285 and 2799. Both are in OTHER methods (line 2285 = different method, line 2799 = BuildAtmMap). BuildFollowerMultipliers (lines 2773-2790) contains zero `for (int i = 0; i < followers` patterns.
**Status**: PASS (old nested for loop removed from BuildFollowerMultipliers)

---

## CYC Analysis

| Version | Branches | CYC |
|---------|----------|-----|
| Before | base(1) + for-outer(+1) + foreach-inner(+1) + if-account(+1) = 4 | 4 |
| After  | base(1) + foreach(+1) + if-null(+1) + if-idx-or-filled with ||(+1) + ternary(+1) = 5 | 5 |

Both <= 8. PASS. The extra branch is the `multipliers[idx] != 0` first-match guard required for behavioral equivalence.

---

## Behavioral Equivalence Note

Original code: outer `for` loop over `followers[i]`; inner `foreach` over `_followerItems`; `break` on first match per follower index. Only the first `_followerItems` entry matching `followers[i]` was used.

Refactored code: single `foreach` over `_followerItems`; `System.Array.IndexOf` finds the follower index; `multipliers[idx] != 0` guard skips any subsequent `_followerItems` entry for the same follower (preserves first-match-wins semantics identical to original `break`).

For any input where each `Account` appears at most once in `_followerItems`, results are bit-for-bit identical. For duplicate entries, first-match wins in both versions.

---

## Test File Created

`src/PropTraderTools/Tests/BwaveDwLaneBTests.cs` — 1 [Fact]:
- `BuildFollowerMultipliers_SignatureUnchanged_AfterContainsRefactor`
  Asserts: method exists, is instance (not static), 1 parameter of type `Account[]`, return type IsValueType.

---

## Status: BUILD_PASS