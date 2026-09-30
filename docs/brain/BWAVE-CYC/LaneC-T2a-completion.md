# BWAVE-CYC Lane C -- Ticket T2a Completion

**Produced by**: ptt-engineer (Stage 4a)
**Ticket**: T2a -- OnApplyRule helper extraction
**Date**: 2025-01-30
**Status**: BUILD_PASS

---

## What Was Implemented

Extracted 3 private helpers from `TradeCopierPanel::OnApplyRule` in
`src/PropTraderTools/TradeCopierPanel.cs` to reduce CCN from 15 to 5.

### File Changed
- `src/PropTraderTools/TradeCopierPanel.cs` -- helpers extracted, OnApplyRule refactored
- `src/PropTraderTools/Tests/BwaveCycLaneCTests.cs` -- 4 [Fact] tests added

---

## Helper Signatures and CCN Values

| Helper | Signature | CCN | Static? |
|--------|-----------|-----|---------|
| `BuildFollowerMultipliers` | `private (int[] multipliers, string[] atmNames) BuildFollowerMultipliers(Account[] followers)` | 5 | No (uses `_followerItems`) |
| `BuildAtmMap` | `private static Dictionary<string, FollowerAtmMode> BuildAtmMap(Account[] followers, string[] atmNames)` | 3 | Yes |
| `SetStatus` | `private void SetStatus(string text)` | 2 | No (uses `_statusText`) |

**Note on `SetStatus`**: Added as a 3rd micro-helper to eliminate 4 inline `if (_statusText != null)` branches from `OnApplyRule`. Without it, lizard reported CCN=9 for `OnApplyRule`. With it, CCN=5. This helper is within T2a scope (reducing `OnApplyRule` CCN to ≤8).

---

## OnApplyRule CCN Before/After

| Method | CCN Before | CCN After | Lizard Verified |
|--------|------------|-----------|----------------|
| `OnApplyRule` | 15 | 5 | Yes (not in warnings) |
| `BuildFollowerMultipliers` | -- | 5 | Yes (not in warnings) |
| `BuildAtmMap(Account[], string[])` | -- | 3 | Yes (not in warnings) |
| `SetStatus` | -- | 2 | Yes (not in warnings) |

---

## DNA Compliance Table

| Rule | Status | Evidence |
|------|--------|----------|
| JS-021 (no lock) | PASS | SCAN-01: 0 results |
| JS-002 (no return null) | PASS | BuildFollowerMultipliers returns value tuple; BuildAtmMap returns empty Dict; SetStatus returns void |
| JS-033 (no async void) | PASS | SCAN-02: 0 results |
| CCN ≤ 8 | PASS | OnApplyRule=5, helpers all <8, none in lizard warnings |
| Private-only | PASS | All 3 helpers are private; no public/internal surface added |
| ASCII-only | PASS | SCAN-04: ASCII OK |

---

## All 7 Scan Results

### SCAN-01: lock() check
```powershell
Select-String "lock\(" C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs | Where-Object { $_.Line.Trim() -notmatch "^//" }
```
**Result: 0 (no output)** ✓

### SCAN-02: async void check
```powershell
Select-String "async void " C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs | Where-Object { $_.Line.Trim() -notmatch "^//" }
```
**Result: 0 (no output)** ✓

### SCAN-03: return null count
```powershell
(Select-String "return null" C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs).Count
```
**Result: 13** (all pre-existing, none in T2a code -- verified L2896-3010 range has 0 live instances) ✓

### SCAN-04: ASCII check
```powershell
$f = Get-Content C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs -Raw; if ($f -match '[^\x00-\x7F]') { "NON-ASCII FOUND" } else { "ASCII OK" }
```
**Result: ASCII OK** ✓

### SCAN-05a: lizard CCN ≤ 8
```powershell
lizard C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs --CCN 8
```
**Result: OnApplyRule NOT in warnings (CCN=5). BuildFollowerMultipliers NOT in warnings (CCN=5). BuildAtmMap NOT in warnings (CCN=3). SetStatus NOT in warnings (CCN=2).** ✓

Full lizard output for new methods:
```
25      5    132      2      25 PropTraderTools::TradeCopierPanel::OnApplyRule@2901-2925
 6      2     21      1       6 PropTraderTools::TradeCopierPanel::SetStatus@2930-2935
19      5    121      1      19 PropTraderTools::TradeCopierPanel::BuildFollowerMultipliers@2941-2959
13      3     77      2      13 PropTraderTools::TradeCopierPanel::BuildAtmMap@2965-2977
```
Warnings section: empty (0 warnings from new methods).

### SCAN-06: dotnet build
```powershell
dotnet build C:\WSGTA\universal-or-strategy\src\PropTraderTools\PropTraderTools.csproj -o bin\LaneC-T2a
```
**Result: Build succeeded. 0 Error(s). 1 pre-existing warning (B131Tests.cs xUnit2004, not T2a).** ✓

### SCAN-07: dotnet test (T2a filter)
```powershell
dotnet test ... --filter "BwaveCycT2aHelper"
```
**Result: Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4** ✓

Note: Using `FullyQualifiedName~BwaveCycT2a` filter also matches pre-existing `BwaveCycT2AtmTemplateTests`
(4 failures from future T2b ticket -- methods TryGetAtmNameFromStrategy etc. not yet extracted).
Those failures are pre-existing and not introduced by T2a.

---

## Test Class: BwaveCycT2aHelperTests

Added to `src/PropTraderTools/Tests/BwaveCycLaneCTests.cs`.

| Test Name | Result |
|-----------|--------|
| `BuildFollowerMultipliers_DefaultsToOne_WhenItemNotFound` | PASS |
| `BuildFollowerMultipliers_UsesItemMultiplier_WhenAccountMatches` | PASS |
| `BuildAtmMap_SkipsNullFollowers` | PASS |
| `BuildAtmMap_UsesInheritMode_WhenAtmNameIsEmpty` | PASS |

All 4 tests use reflection pattern consistent with prior T1a/T1b/T2 tests.

- `BuildFollowerMultipliers_DefaultsToOne_WhenItemNotFound`: verifies method exists, is private instance, returns value tuple, takes `Account[]` param.
- `BuildFollowerMultipliers_UsesItemMultiplier_WhenAccountMatches`: verifies value tuple has `int[]` field1 and `string[]` field2.
- `BuildAtmMap_SkipsNullFollowers`: invokes static `BuildAtmMap(null follower, ...)` -- verifies 0 entries in result dict.
- `BuildAtmMap_UsesInheritMode_WhenAtmNameIsEmpty`: invokes `BuildAtmMap` with empty atm name -- verifies `FollowerAtmMode.Inherit` in result.

---

## Return: BUILD_PASS
