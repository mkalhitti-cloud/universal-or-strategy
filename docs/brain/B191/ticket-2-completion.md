# B191 Ticket 2 — Completion Note

**Epic**: B191
**Ticket**: T2 — B191Tests.cs (new xUnit test file)
**Engineer**: ptt-engineer
**Phase**: 4a (Implementation)
**Status**: BUILD_PASS

---

## Files Written

```
src/PropTraderTools/Tests/B191Tests.cs   (NEW — primary deliverable)
```

Also updated (csproj Compile entries):
```
src/PropTraderTools/PropTraderTools.csproj          (added Tests\B191Tests.cs)
src/PropTraderTools/PropTraderTools.Tests.csproj    (added Tests\B191Tests.cs)
```

No other files modified.

---

## Implementation Summary

Created `src/PropTraderTools/Tests/B191Tests.cs` with exactly 5 `[Fact]` test methods
covering `Build2TargetList` arithmetic and structural reflection for the `Execute` overload.

### Test class: `B191Tests` in `namespace PropTraderTools.Tests`

| Method | Description |
|--------|-------------|
| `Build2TargetList_Qty5_ReturnsT1_3_T2_2` | Ceiling-split oracle: (5+1)/2=3, 5-3=2 |
| `Build2TargetList_Qty20_ReturnsT1_10_T2_10` | Even-split oracle: (20+1)/2=10, 20-10=10 |
| `Build2TargetList_Qty5_SumEqualsInput` | Sum invariant: t1+t2==5 |
| `Execute_ListInstrument_Overload_ExistsAsInternal` | Reflection check: Execute(2-param) exists on PttGlobalQuickExit |
| `Build2TargetList_Qty1_ReturnsT1_1_T2_0` | Boundary: (1+1)/2=1, 1-1=0 |

T2-4 uses the fallback reflection form (name + parameter count, no NT8 type references),
ensuring SCAN-02 is unconditionally 0.

---

## 7-Scan Results

### SCAN-01: [Fact] count = 5 AND Skip = 0

```powershell
(Get-Content src\PropTraderTools\Tests\B191Tests.cs | Select-String "\[Fact\]").Count
# Output: 5

Select-String -Pattern "Skip" src\PropTraderTools\Tests\B191Tests.cs
# Output: (no output -- 0 results)
```

**RESULT: PASS** — 5 [Fact] attributes, 0 Skip attributes.

---

### SCAN-02: No NinjaTrader.* references

```powershell
Select-String -Pattern "NinjaTrader\." src\PropTraderTools\Tests\B191Tests.cs
# Output: (no output -- 0 results)
```

**RESULT: PASS** — 0 NT8 namespace references. T2-4 uses fallback reflection form.

---

### SCAN-03: dotnet test --filter "FullyQualifiedName~B191"

```powershell
dotnet test src\PropTraderTools\PropTraderTools.Tests.csproj --filter "FullyQualifiedName~B191" --no-build
```

Output:
```
Test run for ...\PropTraderTools.Tests.dll (.NETFramework,Version=v4.8)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:     5, Skipped:     0, Total:     5, Duration: 731 ms - PropTraderTools.Tests.dll (net48)
```

**RESULT: PASS** — 5 passed, 0 failed, 0 skipped.

---

### SCAN-04: Build2TargetList_Qty5_ReturnsT1_3_T2_2 — PASS

Confirmed by dotnet test output above. Oracle: first.Qty=3, second.Qty=2. PASS.

---

### SCAN-05: Build2TargetList_Qty1_ReturnsT1_1_T2_0 — PASS

Confirmed by dotnet test output above. Boundary case: first.Qty=1, second.Qty=0. PASS.

---

### SCAN-06: Build2TargetList_Qty5_SumEqualsInput — PASS

Confirmed by dotnet test output above. Sum invariant: Build2TargetList(5).Sum(t=>t.Qty)==5. PASS.

---

### SCAN-07: No lock() in B191Tests.cs

```powershell
Select-String -Pattern "lock\(" src\PropTraderTools\Tests\B191Tests.cs
# Output: (no output -- 0 results)
```

**RESULT: PASS** — 0 lock() occurrences.

---

## Scan Summary

| Scan | Description | Result |
|------|-------------|--------|
| SCAN-01 | [Fact] count = 5, Skip = 0 | PASS |
| SCAN-02 | No NinjaTrader.* references | PASS |
| SCAN-03 | dotnet test: 5 passed / 0 failed / 0 skipped | PASS |
| SCAN-04 | Build2TargetList_Qty5 oracle verified ({3,2}) | PASS |
| SCAN-05 | Build2TargetList_Qty1 boundary oracle verified ({1,0}) | PASS |
| SCAN-06 | Sum invariant verified (sum==5) | PASS |
| SCAN-07 | lock() = 0 | PASS |

All 7 scans: **PASS**

---

## JS Rule Compliance

| Rule | Check | Result |
|------|-------|--------|
| JS-021 | lock() ban — SCAN-07: 0 occurrences | PASS |
| JS-033 | async void ban — 0 async void methods | PASS |
| JS-066 | CYC <= 8 — all 5 methods are straight-line [Fact], CYC=1 each | PASS |
| JS-081 | ASCII-only identifiers and strings — confirmed | PASS |
| xUnit mandate | [Fact] only, no [Theory], no [Fact(Skip=...)] | PASS |

---

## BUILD_PASS
