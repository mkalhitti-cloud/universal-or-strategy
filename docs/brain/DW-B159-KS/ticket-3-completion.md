# Ticket 3 Completion: DW-B159-K2 — 14 xUnit Tests
<!-- status: BUILD_PASS -->
<!-- epic: DW-B159-KS -->
<!-- ticket: T3 (DW-B159-K2) -->
<!-- phase: 4a -->
<!-- author: ptt-engineer -->
<!-- date: 2026-10-01 -->

---

## What Was Implemented

Added class `B159KSuiteTests` to `src/PropTraderTools/CopyEngineTests.cs` (appended before
the namespace closing brace at line 9830). Zero production code touched.

**Class:** `B159KSuiteTests` — 14 `[Fact]` methods, all `public void`, no `[Skip]`.

All tests use pure-BCL simulation (no `new CopyEngine()` or `CopyEngine.Instance`).
Structural tests use `typeof(CopyEngine).GetMethod(...)` for reflection-only contract proof.

---

## All 14 Method Names (verbatim)

| # | Method Name |
|---|-------------|
| 1 | `ShouldSkipFollower_FollowerHasOpenPosition_ReturnsTrue` |
| 2 | `ShouldSkipFollower_FollowerNoOpenPosition_ReturnsFalse` |
| 3 | `IsFollowerPositionBlocked_NullAccount_ReturnsFalse` |
| 4 | `IsFollowerPositionBlocked_NullInstrument_ReturnsFalse` |
| 5 | `IsFollowerPositionBlocked_AccountWithNoPosition_ReturnsFalse` |
| 6 | `IsFollowerPositionBlocked_AccountWithOpenPosition_ReturnsTrue` |
| 7 | `TryFireBePriceRetry_CancelledOrderWithRetryKey_HasOpenPosition_StillAtNonBePrice_CallsSubmitBeStop` |
| 8 | `TryFireBePriceRetry_CancelledOrderWithRetryKey_AlreadyAtBePrice_NoSubmitBeStop` |
| 9 | `TryArmBePriceRetry_AttemptsAtCap_NoNewEntry` |
| 10 | `TryFireBePriceRetry_WorkingOrder_RemovesFromDict` |
| 11 | `TryFireBePriceRetry_NonPttBeStopPrefix_ExitsEarly` |
| 12 | `TryArmBePriceRetry_ValidInputsFirstAttempt_AddsEntryToDict` |
| 13 | `TryFireBePriceRetry_CancelledPttBeStop_KeyNotInDict_NoSubmitBeStop` |
| 14 | `StillAtNonBePrice_WorkingStopAtOriginalPrice_ReturnsTrue` |

All 14 confirmed present. All have `[Fact]` attribute. None have `[Skip]`.

---

## dotnet build Result

```
dotnet build src/PropTraderTools/PropTraderTools.Tests.csproj
```

Result: **0 Error(s), 1181 Warning(s)** (all pre-existing xUnit1004 Skip warnings on
pre-existing test methods; zero warnings introduced by B159KSuiteTests).

**BUILD: PASS**

---

## dotnet test Result

```
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --filter "FullyQualifiedName~B159KSuiteTests"
```

Result: **Failed: 0, Passed: 14, Skipped: 0, Total: 14**

**TEST: PASS**

---

## 7-Scan Results

### SCAN-01: No `throw` statements (JS-001)
Command: `Select-String -Path src/PropTraderTools/CopyEngineTests.cs -Pattern "\bthrow\b" | Where-Object { $_.LineNumber -ge 9830 }`
Result: **0 matches**
Status: **PASS**

### SCAN-02: No `lock()` blocks (JS-021)
Command: `Select-String -Path src/PropTraderTools/CopyEngineTests.cs -Pattern "lock\(" | Where-Object { $_.LineNumber -ge 9830 }`
Result: **0 matches**
Status: **PASS**

### SCAN-03: ASCII-only string literals and comments (JS-042)
Command: Non-ASCII character scan over B159KSuiteTests region (lines >= 9830)
Result: **0 matches**
All string literals confirmed ASCII: `"PTT-BE-Stop-1"`, `"PTT-BE-Stop-"`, `"PTT-QX-Stop-1"`,
`"Sim102"`, `"ShouldSkipFollower"`, `"IsFollowerPositionBlocked"`, `"TryFireBePriceRetry"`,
`"TryArmBePriceRetry"`, `"StillAtNonBePrice"`. Class name and all comments are ASCII-only.
Status: **PASS**

### SCAN-04: CYC per test method <= 8 (JS-CYC)
All 14 test methods are straight-line BCL simulation with at most one conditional branch.
CYC = 1 or 2 per method. Maximum = 2. Budget = 8.
No nested loops. No deep branching. Manual review confirmed.
Status: **PASS**

### SCAN-05: No null returns from predicate methods (JS-002)
Command: `Select-String -Path src/PropTraderTools/CopyEngineTests.cs -Pattern "return null" | Where-Object { $_.LineNumber -ge 9830 }`
Result: **0 matches**
All 14 test methods return `void`. No predicate method introduced.
Status: **PASS**

### SCAN-06: No `[Skip]` on any test (ticket SCAN-06/SCAN-07)
Command: `Select-String -Path ... -Pattern "\[Skip\]|\[Fact\(Skip" | Where-Object { $_.LineNumber -ge 9830 }`
Result: **0 matches** (no `[Skip]` or `[Fact(Skip=...)]` attributes in B159KSuiteTests)
Note: 7 grep hits for plain `"Skip"` substring come exclusively from the canonical method
names `ShouldSkipFollower_*` (mandated verbatim by ticket). No test-skip attribute present.
Status: **PASS**

### SCAN-07: All 14 method names present (1 match each)
Each method name grepped individually:

| Method Name | Matches |
|-------------|---------|
| `ShouldSkipFollower_FollowerHasOpenPosition_ReturnsTrue` | 1 |
| `ShouldSkipFollower_FollowerNoOpenPosition_ReturnsFalse` | 1 |
| `IsFollowerPositionBlocked_NullAccount_ReturnsFalse` | 1 |
| `IsFollowerPositionBlocked_NullInstrument_ReturnsFalse` | 1 |
| `IsFollowerPositionBlocked_AccountWithNoPosition_ReturnsFalse` | 1 |
| `IsFollowerPositionBlocked_AccountWithOpenPosition_ReturnsTrue` | 1 |
| `TryFireBePriceRetry_CancelledOrderWithRetryKey_HasOpenPosition_StillAtNonBePrice_CallsSubmitBeStop` | 1 |
| `TryFireBePriceRetry_CancelledOrderWithRetryKey_AlreadyAtBePrice_NoSubmitBeStop` | 1 |
| `TryArmBePriceRetry_AttemptsAtCap_NoNewEntry` | 1 |
| `TryFireBePriceRetry_WorkingOrder_RemovesFromDict` | 1 |
| `TryFireBePriceRetry_NonPttBeStopPrefix_ExitsEarly` | 1 |
| `TryArmBePriceRetry_ValidInputsFirstAttempt_AddsEntryToDict` | 1 |
| `TryFireBePriceRetry_CancelledPttBeStop_KeyNotInDict_NoSubmitBeStop` | 1 |
| `StillAtNonBePrice_WorkingStopAtOriginalPrice_ReturnsTrue` | 1 |

All 14: exactly 1 match each.
Status: **PASS**

---

## Additional Acceptance Criteria

| Criterion | Result |
|-----------|--------|
| `grep "class B159KSuiteTests"` | 1 match |
| `grep "\[Fact\]"` in B159KSuiteTests region | 14 matches |
| No `[Skip]` | 0 attribute matches |
| No `new CopyEngine()` in B159KSuiteTests | 0 matches |
| No `CopyEngine.Instance` in B159KSuiteTests | 0 matches |
| `dotnet test Failed=0` | PASS — Failed:0, Passed:14 |
| Passed count increases by 14 | PASS |

---

## BUILD_PASS

*Completion report produced by ptt-engineer. Phase 4a. Wave DW-B159-KS. Date 2026-10-01.*
*All 7 scans zero. dotnet build 0 errors. dotnet test Failed=0 Passed=14.*
