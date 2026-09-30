# Ticket 3 Verification: DW-B159-K2 — 14 xUnit Tests
<!-- status: VERIFY_PASS -->
<!-- epic: DW-B159-KS -->
<!-- ticket: T3 (DW-B159-K2) -->
<!-- phase: 4b -->
<!-- author: ptt-verifier -->
<!-- date: 2026-10-01 -->

---

## Verdict: VERIFY_PASS

All 14 method names present. All 7 independent scans: 0 violations. dotnet test: Failed=0, Passed=14, Skipped=0.

---

## File Verified

`src/PropTraderTools/CopyEngineTests.cs` — total lines: 10,117.
`B159KSuiteTests` class appended at lines 9836–10115 (closing brace at 10115, namespace closes at 10117).

---

## Check 1: Class Presence

```
Select-String -Path src/PropTraderTools/CopyEngineTests.cs -Pattern "class B159KSuiteTests"
```

Result: **1 match** — line 9836: `public class B159KSuiteTests`
Status: **PASS**

---

## Check 2: All 14 Canonical Method Names (1 match each)

Independently grepped. All confirmed present at exactly 1 occurrence each.

| # | Method Name | Line | Matches |
|---|-------------|------|---------|
| 1 | `ShouldSkipFollower_FollowerHasOpenPosition_ReturnsTrue` | 9846 | 1 |
| 2 | `ShouldSkipFollower_FollowerNoOpenPosition_ReturnsFalse` | 9864 | 1 |
| 3 | `IsFollowerPositionBlocked_NullAccount_ReturnsFalse` | 9875 | 1 |
| 4 | `IsFollowerPositionBlocked_NullInstrument_ReturnsFalse` | 9894 | 1 |
| 5 | `IsFollowerPositionBlocked_AccountWithNoPosition_ReturnsFalse` | 9908 | 1 |
| 6 | `IsFollowerPositionBlocked_AccountWithOpenPosition_ReturnsTrue` | 9922 | 1 |
| 7 | `TryFireBePriceRetry_CancelledOrderWithRetryKey_HasOpenPosition_StillAtNonBePrice_CallsSubmitBeStop` | 9942 | 1 |
| 8 | `TryFireBePriceRetry_CancelledOrderWithRetryKey_AlreadyAtBePrice_NoSubmitBeStop` | 9973 | 1 |
| 9 | `TryArmBePriceRetry_AttemptsAtCap_NoNewEntry` | 9988 | 1 |
| 10 | `TryFireBePriceRetry_WorkingOrder_RemovesFromDict` | 10005 | 1 |
| 11 | `TryFireBePriceRetry_NonPttBeStopPrefix_ExitsEarly` | 10029 | 1 |
| 12 | `TryArmBePriceRetry_ValidInputsFirstAttempt_AddsEntryToDict` | 10045 | 1 |
| 13 | `TryFireBePriceRetry_CancelledPttBeStop_KeyNotInDict_NoSubmitBeStop` | 10073 | 1 |
| 14 | `StillAtNonBePrice_WorkingStopAtOriginalPrice_ReturnsTrue` | 10096 | 1 |

Status: **PASS** — all 14 canonical names present verbatim.

---

## Check 3: [Fact] Attribute — 14 Count

```powershell
$region = (Get-Content CopyEngineTests.cs)[9835..10116]
$region | Select-String -Pattern "^\s*\[Fact\]$"
```

Result: **14 matches** — at lines 9845, 9863, 9874, 9893, 9907, 9921, 9941, 9972, 9987, 10004, 10028, 10044, 10072, 10095.
Each `[Fact]` is the line immediately preceding its method declaration.
No `[Fact(Skip=...)]` found.
Status: **PASS**

---

## Check 4: No [Skip] Attribute

```powershell
$region | Select-String -Pattern "\[Skip\]|\[Fact\(Skip"
```

Result: **0 matches**
Note: 7 lines contain the substring "Skip" as part of method names (`ShouldSkipFollower_*`), but NO test-skip attribute is present.
Status: **PASS**

---

## Check 5: No CopyEngine Instantiation

```powershell
$region | Select-String -Pattern "new CopyEngine\(|CopyEngine\.Instance"
```

Result: **0 matches**
All 14 tests use `typeof(CopyEngine).GetMethod(...)` (type metadata only) or pure BCL simulation.
No `CopyEngine.Instance` accessed; no NT8 cctor triggered.
Status: **PASS**

---

## dotnet test Result (independently run by verifier)

```
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --filter "FullyQualifiedName~B159KSuiteTests"
```

Result: **Failed: 0, Passed: 14, Skipped: 0, Total: 14, Duration: 179 ms**
Build: 0 errors, pre-existing xUnit1004 Skip warnings only (none in B159KSuiteTests).
Status: **PASS**

---

## 7-Scan Independent Results (Layer 3 — Verifier)

### SCAN-01: No `throw` in B159KSuiteTests region (JS-001)

```powershell
$region | Select-String -Pattern "\bthrow\b"
```

Result: **0 matches**
Status: **PASS**

Cross-check vs engineer Layer 2: Engineer reported 0. **Layer 2 matches Layer 3.**

---

### SCAN-02: No `lock(` in B159KSuiteTests region (JS-021)

```powershell
$region | Select-String -Pattern "lock\("
```

Result: **0 matches**
All concurrency state uses `ConcurrentDictionary<string, T>` (BCL lock-free). No `lock(...)` statement.
Status: **PASS**

Cross-check vs engineer Layer 2: Engineer reported 0. **Layer 2 matches Layer 3.**

---

### SCAN-03: ASCII-only string literals and comments (JS-042)

```powershell
$region | Where-Object { $_ -match '[^\x00-\x7F]' }
```

Result: **0 matches**
All string literals (`"PTT-BE-Stop-1"`, `"PTT-BE-Stop-"`, `"PTT-QX-Stop-1"`, `"Sim102"`, reflection method name strings) are ASCII-only.
Status: **PASS**

Cross-check vs engineer Layer 2: Engineer reported 0. **Layer 2 matches Layer 3.**

---

### SCAN-04: CYC per test method <= 8 (JS-CYC)

Manual review of all branch-keyword lines in the region (27 total across 14 methods).

Most complex method: `TryArmBePriceRetry_ValidInputsFirstAttempt_AddsEntryToDict` (lines 10045–10068):
- 1 base + `if (acc == null || instr == null || bePrice <= 0.0)` [3 conditions] + `if (a < BeRetryMaxAttempts)` = CYC ≈ 5

All other methods: CYC 1–3. Maximum CYC = 5. Budget = 8.
Result: **0 violations**
Status: **PASS**

Cross-check vs engineer Layer 2: Engineer reported max CYC = 2. **Verifier confirms max = 5 for most complex method — still well within budget of 8. No VERIFY_FAIL. Note: engineer underestimated slightly due to || compound conditions, but budget is not exceeded.**

---

### SCAN-05: No null returns from test methods (JS-002)

```powershell
$region | Select-String -Pattern "return null"
```

Result: **0 matches**
All 14 test methods return `void`.
Status: **PASS**

Cross-check vs engineer Layer 2: Engineer reported 0. **Layer 2 matches Layer 3.**

---

### SCAN-06: No [Skip] attribute on any test

```powershell
$region | Select-String -Pattern "\[Skip\]|\[Fact\(Skip"
```

Result: **0 matches**
Status: **PASS**

Cross-check vs engineer Layer 2: Engineer reported 0. **Layer 2 matches Layer 3.**

---

### SCAN-07: All 14 method names confirmed 1 match each

Independently grepped each of the 14 method names using `Select-String`.
All returned exactly 1 match. See Check 2 table above.
Status: **PASS**

Cross-check vs engineer Layer 2: Engineer reported 1 match each. **Layer 2 matches Layer 3.**

---

## Architecture and Spec Compliance

| Criterion | Expected | Actual | Status |
|-----------|----------|--------|--------|
| File modified | `CopyEngineTests.cs` only | `CopyEngineTests.cs` only | PASS |
| Production code changed | None | None (`CopyEngine.cs` not in this ticket) | PASS |
| Class name | `B159KSuiteTests` | `B159KSuiteTests` at line 9836 | PASS |
| Method count | 14 | 14 | PASS |
| All `[Fact]` (no `[Skip]`) | Yes | Yes — 14 `[Fact]`, 0 `[Skip]` | PASS |
| No CopyEngine instantiation | None | 0 hits for `new CopyEngine(` or `CopyEngine.Instance` | PASS |
| Pattern: BCL simulation | Per ticket T3 spec | All methods use pure BCL or `typeof(CopyEngine).GetMethod` | PASS |
| Namespace placement | Inside existing `PropTraderTools` namespace | Lines 9836–10115, inside namespace closing `}` at 10117 | PASS |

---

## Engineer Layer 2 vs Verifier Layer 3 Discrepancy Report

| Scan | Layer 2 (Engineer) | Layer 3 (Verifier) | Discrepancy? |
|------|--------------------|--------------------|--------------|
| SCAN-01 throw | 0 | 0 | NONE |
| SCAN-02 lock( | 0 | 0 | NONE |
| SCAN-03 non-ASCII | 0 | 0 | NONE |
| SCAN-04 CYC max | "1 or 2" | 5 (TryArmBePriceRetry_ValidInputs) | MINOR — engineer underestimated; budget (8) not exceeded; NO VERIFY_FAIL |
| SCAN-05 return null | 0 | 0 | NONE |
| SCAN-06 [Skip] | 0 | 0 | NONE |
| SCAN-07 method names | 14 × 1 match | 14 × 1 match | NONE |

No discrepancy that triggers VERIFY_FAIL.

---

## Final Verdict

**VERIFY_PASS**

All 14 canonical method names present at exactly 1 match each. `[Fact]` on all 14. `[Skip]` on none. `B159KSuiteTests` class present at line 9836. No `new CopyEngine()` or `CopyEngine.Instance`. All 7 scans: 0 violations. `dotnet test` independently run: Failed=0, Passed=14, Skipped=0.

*Verification produced by ptt-verifier. Phase 4b. Wave DW-B159-KS. Date 2026-10-01.*
