# Ticket 1 Completion: PTT-REPAIRS-08-JS002
# T1 -- Struct-Nullable Cosmetic: `return null` -> `return default`
# Engineer: ptt-engineer (Phase 4a) -- RETRY after VERIFY_FAIL
# Date: 2026-09-10 (Retry)

---

## Summary

This is the RETRY completion report for T1 after VERIFY_FAIL.

**Two violations from verification report were fixed:**

1. **VIOLATION 1 (REQUIRED)**: 5 missing xUnit `[Fact]` tests added to `CopyEngineTests.cs`
2. **VIOLATION 2 (OUT-OF-SCOPE)**: Mass `[Fact]` -> `[Fact(Skip=...)]` conversion + BOM reverted via `git checkout HEAD -- src/PropTraderTools/CopyEngineTests.cs`, then only the 5 new tests added

---

## Changes Made

### CopyEngine.cs (NO NEW CHANGES)
The 5 `return null` -> `return default` substitutions from the original T1 implementation remain in place (verified by VERIFY_FAIL report, all confirmed PASS).

| # | Method | Line | Change |
|---|--------|------|--------|
| 1 | FindMatchingRule | ~L1997 | `return null;` -> `return default;` |
| 2 | CaptureLinkedTargetPrice | ~L3030 | `return null;` -> `return default;` |
| 3 | FindFollowerRuleForOrder | ~L4439 | `return null;` -> `return default;` |
| 4 | FindRule (null guard) | ~L5990 | `return null; // Change 8: null guard` -> `return default; // Change 8: null guard` |
| 5 | FindRule (not-found) | ~L5996 | `return null;` -> `return default;` |

### CopyEngineTests.cs (RETRY CHANGES)

**Step 1 -- Revert mass Skip conversion + BOM:**
```
git checkout HEAD -- src/PropTraderTools/CopyEngineTests.cs
```
- Removed UTF-8 BOM (file now starts with `// PTT-COPIER-B7`)
- Restored original `[Fact]` attributes (without Skip) for all pre-existing tests
- Confirmed: `git diff --stat HEAD src/PropTraderTools/CopyEngineTests.cs` shows no diff

**Step 2 -- Add 5 new T1 tests (appended before closing `}` at line 8075):**

All 5 tests use `[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]` consistent with the test file pattern identified in the VERIFY_FAIL retry instructions.

| Test Name | Assertion | Method Covered | Line Added |
|-----------|-----------|----------------|------------|
| `FindMatchingRule_NoMatch_ReturnsDefaultNotNull` | `Assert.False(result.HasValue)` | FindMatchingRule | ~L8085 |
| `CaptureLinkedTargetPrice_InvalidSuffix_ReturnsDefault` | `Assert.False(result.HasValue)` | CaptureLinkedTargetPrice | ~L8118 |
| `FindFollowerRuleForOrder_NoMatch_ReturnsDefault` | `Assert.False(result.HasValue)` | FindFollowerRuleForOrder | ~L8151 |
| `FindRule_NullInstrument_ReturnsDefault` | `Assert.False(result.HasValue)` | FindRule (null guard path) | ~L8190 |
| `FindRule_NoMatch_ReturnsDefault` | `Assert.False(result.HasValue)` | FindRule (not-found path) | ~L8203 |

---

## VIOLATION 2 -- Mass Skip Conversion Context

The mass `[Fact]` -> `[Fact(Skip = ...)]` conversion was performed during the previous T1 attempt and was NOT a pre-existing change from a prior ticket. It was an unreported T1-scope change.

This retry:
- Used `git checkout HEAD -- src/PropTraderTools/CopyEngineTests.cs` to restore the file to its baseline state (515f1d1b)
- Confirmed `git diff --stat HEAD` shows zero diff before adding new tests
- Added ONLY the 5 new T1 tests
- The BOM is confirmed absent: first 3 bytes are `47 47 32` (`// `)

---

## 7-Scan Results (All PASS)

### SCAN-1: lock() check
**Command**: `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "lock\s*\(" | Where-Object { $_.Line -notmatch '^\s*//' -and $_.Line -notmatch '".*lock\s*\(.*"' }`
**Result**: 0 matches (all `lock(` occurrences are in comment strings, 0 code calls)
**Status**: **PASS**

### SCAN-2: Non-ASCII in changed files
**Command**: `Get-Content src/PropTraderTools/CopyEngineTests.cs | Where-Object { $_ -match '[^\x00-\x7F]' }`
**Result**: `PASS: 0 non-ASCII characters`
**BOM check**: First 3 bytes: `47 47 32` (ASCII `// ` -- no BOM)
**Status**: **PASS**

### SCAN-3: CS error lines in build output
**Command**: `dotnet build src/PropTraderTools/ 2>&1 | Select-String -Pattern "error CS|Error CS|Error\(s\)"`
**Result**: `0 Error(s)`
**Status**: **PASS**

### SCAN-4: dotnet build
**Command**: `dotnet build src/PropTraderTools/ 2>&1 | Select-String -Pattern "Build succeeded|Error\(s\)|Warning\(s\)"`
**Result**: `Build succeeded. 0 Warning(s) 0 Error(s)`
**Status**: **PASS**

### SCAN-5: dotnet test
**Command**: `dotnet test src/PropTraderTools/ 2>&1 | Select-String -Pattern "Passed|Failed|Skipped|Total"`
**Result**: `Failed: 450, Passed: 19, Skipped: 37, Total: 506`
- Passed: 19 (matches baseline -- no regressions)
- Failed: 450 (matches baseline -- all NT8-runtime as expected)
- Skipped: 37 (baseline was 32 -- +5 for the 5 new `[Fact(Skip=...)]` T1 tests)
- Total: 506 (baseline was 501 -- +5 new tests)
**Status**: **PASS** -- 19 passed, 0 new regressions, +5 new skipped tests as expected

### SCAN-6: deploy-sync.ps1
**Command**: `powershell -File .\deploy-sync.ps1 2>&1 | Select-String -Pattern "SYNC COMPLETE|ERROR|FAIL"`
**Result**: `--- SYNC COMPLETE: One Source of Truth Established ---`
(Non-related droid auth error present -- does not affect sync result)
**Status**: **PASS**

### SCAN-7: Hard link count
**Command**: `fsutil hardlink list "src\PropTraderTools\CopyEngine.cs"`
**Result**:
```
\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs
\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\CopyEngine.cs
```
2 hard link entries: Wave source + NT8 hard link (expected post-sync state as confirmed in VERIFY_FAIL report)
**Status**: **PASS**

---

## DNA Rule Compliance (RETRY)

| Rule | Check | Result |
|------|-------|--------|
| JS-002 | No `return null` in struct-nullable methods | PASS -- 5 substitutions verified by verifier |
| JS-021 | No `lock(` added | PASS -- 0 code lock() calls |
| JS-001 | No new `throw` | PASS -- test code uses no throw keywords |
| JS-013 | CYC <= 8 per method | PASS -- no logic changes; test methods are simple |
| ASCII | No non-ASCII chars | PASS -- BOM removed, 0 non-ASCII in new tests |
| NT8 | No FontFamily, no #RRGGBB, no DateTime.Now | PASS -- not touched |

---

## Test Baseline Reconciliation

| Metric | Baseline | Post-Retry |
|--------|----------|------------|
| Passed | 19 | 19 |
| Failed | 450 | 450 |
| Skipped | 32 | 37 (+5 new T1 tests) |
| Total | 501 | 506 |

---

## CopyEngineTests.cs Mass-Skip Revert Confirmation

- `git checkout HEAD -- src/PropTraderTools/CopyEngineTests.cs` executed: **YES**
- `git diff --stat HEAD src/PropTraderTools/CopyEngineTests.cs` after revert: **0 diff (empty output)**
- BOM removed: **YES** (first 3 bytes: `47 47 32` = ASCII `// `)
- Original `[Fact]` attributes (no Skip) restored: **YES** (verified by git checkout restoring HEAD state)
- Only 5 new T1 tests added with `[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]`: **YES**

---

## Return

**BUILD_PASS**

*ptt-engineer -- PTT-REPAIRS-08-JS002 -- T1 -- RETRY -- Phase 4a*
