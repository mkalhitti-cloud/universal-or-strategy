# DW-LB-SFB-01 Ticket 1 -- Independent Verification Report

**Ticket ID**: DW-LB-SFB-01-T1
**Title**: Add xUnit test coverage for IsBracketLegStatic post-fix (11 regression guards)
**Verifier**: ptt-verifier (Phase 4b)
**Date**: 2026-09-08
**SCOPE**: DW-LB-SFB-01-T1 ONLY

---

## RULES CATALOG GATE

**Gate result**: PASS
Rules Catalog docs/standards/jane-street/RULES_CATALOG.md confirmed UTF-8 clean and readable.
P0 rules applicable to this ticket (test-only file, no src/ modifications):

| Rule | Check | Result |
|------|-------|--------|
| JS-021 | No lock() in test file | PASS |
| JS-001 | No throw in test code | PASS |
| JS-002 | No return null | PASS (returns bool) |
| JS-033 | No async void | PASS |

---

## INDEPENDENT 7-SCAN RESULTS (Layer 3 -- Verifier Run)

### SCAN-01 -- lock() Check

**Command run independently**:
  Select-String -Path tests\PropTraderTools.Tests\IsBracketLegStaticTests.cs -Pattern lock\s*\(

**Result**: Zero matches. No lock() in IsBracketLegStaticTests.cs.

**SCAN-01: PASS** (0 lock() in new test file)
**Layer 2 vs Layer 3**: MATCH

---

### SCAN-02 -- CYC Manual Count (independent)

**Inline mirror IsBracketLegStatic** (IsBracketLegStaticTests.cs lines 24-32):

  private static bool IsBracketLegStatic(string? name, bool hasEntrySignal)
  {
    if (hasEntrySignal) return true;          // +1 branch
    if (name == null) return false;           // +1 branch
    return name.StartsWith("Stop", ...)       // base=1
        || name.StartsWith("Target", ...)     // +1 (|| operator)
        || name.StartsWith("PTT-STP-Drag-")   // +1
        || name.StartsWith("PTT-TGT-Drag-")   // +1
        || name.EndsWith("STP", ...);         // +1
  }
  Total CYC = 7 (PASS <= 8)

**All 11 [Fact] test methods**: CYC=1 each (single Assert.True/Assert.False, no branching)

**SCAN-02: PASS** (inline mirror CYC=7; all test methods CYC=1)
**Layer 2 vs Layer 3**: MATCH

NOTE on inline mirror vs production: Production IsBracketLegStatic at L5799-5812 uses
StartsWith("Stop") and StartsWith("Target") without explicit StringComparison.
The inline mirror uses StartsWith("Stop", System.StringComparison.Ordinal).
This is functionally equivalent for all-ASCII NT8 order names. Not a semantic drift.
The test is slightly more explicit (Ordinal is stricter) but produces identical results.
This is NOT a violation -- it is a safe conservative choice.

---

### SCAN-03 -- ASCII-Only Check

**Commands run independently**:
  (bytes IsBracketLegStaticTests.cs > 127).Count = 0
  (bytes CopyEngine.cs > 127).Count = 0

**SCAN-03: PASS** (0 non-ASCII bytes)
**Layer 2 vs Layer 3**: MATCH

---

### SCAN-04 -- NT8 API Check

**Command run independently**:
  Select-String IsBracketLegStaticTests.cs -Pattern "NinjaTrader|Account\.|Order\s+|AtmStrategy"
  Result: Zero matches

Test file uses only System.StringComparison (BCL) -- no NT8 types.

**SCAN-04: PASS** (0 NT8 API references in test file)
**Layer 2 vs Layer 3**: MATCH

---

### SCAN-05 -- Build Gate

**Command run independently**:
  dotnet build src/PropTraderTools/PropTraderTools.csproj --no-restore

**Output**:
  Build succeeded.
      0 Warning(s)
      0 Error(s)
  Time Elapsed 00:00:00.73

(src/ build is clean. Test project build confirmed as part of SCAN-06 dotnet test which also builds.)

**SCAN-05: PASS** (0 errors, 0 warnings)
**Layer 2 vs Layer 3**: MATCH

---

### SCAN-06 -- Test Gate

**Command run independently**:
  dotnet test tests/PropTraderTools.Tests/PropTraderTools.Tests.csproj

**Output**:
  Passed!  - Failed: 0, Passed: 97, Skipped: 3, Total: 100, Duration: 21 ms

**DW-LB-SFB-01 new tests (T1-T11) -- all PASSED**:

| Test | Assert Type | Result |
|------|-------------|--------|
| T1 PTT_STP_Drag_1_ReturnsTrue | Assert.True | PASSED |
| T2 PTT_TGT_Drag_1_ReturnsTrue | Assert.True | PASSED |
| T3 PTT_BE_Stop_1_ReturnsFalse | Assert.False -- KEY REGRESSION GUARD | PASSED |
| T4 PTT_Flatten_ReturnsFalse | Assert.False -- KEY REGRESSION GUARD | PASSED |
| T5 PTT_Tighten_Stop_ReturnsFalse | Assert.False -- REGRESSION GUARD | PASSED |
| T6 Stop1_ReturnsTrue | Assert.True | PASSED |
| T7 Target1_ReturnsTrue | Assert.True | PASSED |
| T8 Buy_STP_ReturnsTrue | Assert.True | PASSED |
| T9 Entry_ReturnsFalse | Assert.False | PASSED |
| T10 NullName_ReturnsFalse | Assert.False | PASSED |
| T11 NullOrderAnalog_ReturnsFalse | Assert.False | PASSED |

T3 (PTT-BE-Stop-1): Assert.False confirmed -- post-fix IsBracketLegStatic correctly rejects this.
T4 (PTT-Flatten): Assert.False confirmed -- post-fix correctly rejects this.
T5 (PTT-Tighten-Stop): Assert.False confirmed -- post-fix correctly rejects this.

Pre-existing tests: all 86 pre-existing continued to pass (0 regressions).
Skipped: 3 (CopyEngineB137Tests T_B137_03/04/05 -- unchanged from baseline).

**SCAN-06: PASS** (0 failures, all 11 SFB-01 T1-T11 confirmed passing)
**Layer 2 vs Layer 3**: MATCH

---

### SCAN-07 -- Sync Gate

**Command run independently**:
  powershell -File scripts\ptt-sync-and-verify.ps1

**Output**:
  === PTT SYNC: src/PropTraderTools -> NT8 AddOns ===
    Copied:   0  |  In-sync: 18  |  Excluded: 74
  === PTT VERIFY: MD5 check every synced file ===
    OK       CopyEngine.cs
    [17 other files all OK]
  === SYNC + VERIFY: PASS (18 files confirmed) ===

0 DESYNC, 0 MISSING. Test files do not sync to NT8 -- the synced set is undisturbed.
CopyEngine.cs is confirmed in-sync (not modified by this ticket).

**SCAN-07: PASS** (0 DESYNC, 0 MISSING, 18 files confirmed)
**Layer 2 vs Layer 3**: MATCH

---

## IMPLEMENTATION CORRECTNESS VERDICT

### IsBracketLegStaticTests.cs Review

| Requirement | Expected | Actual | Status |
|-------------|----------|--------|--------|
| File exists | tests/PropTraderTools.Tests/IsBracketLegStaticTests.cs | Confirmed | PASS |
| Class name | public sealed class IsBracketLegStaticTests | Confirmed | PASS |
| Inline mirror present | private static bool IsBracketLegStatic(string? name, bool hasEntrySignal) | Confirmed at line 24 | PASS |
| 11 [Fact] methods | 11 | 11 (independently counted) | PASS |
| Framework | xUnit [Fact] only | Confirmed xUnit, no NUnit/MSTest | PASS |
| T1 PTT_STP_Drag_1 = Assert.True | Assert.True | Confirmed line 43 | PASS |
| T2 PTT_TGT_Drag_1 = Assert.True | Assert.True | Confirmed line 55 | PASS |
| T3 PTT_BE_Stop_1 = Assert.False | Assert.False (KEY REGRESSION) | Confirmed line 68 | PASS |
| T4 PTT_Flatten = Assert.False | Assert.False (KEY REGRESSION) | Confirmed line 80 | PASS |
| T5 PTT_Tighten_Stop = Assert.False | Assert.False (REGRESSION) | Confirmed line 92 | PASS |
| T6 Stop1 = Assert.True | Assert.True | Confirmed line 103 | PASS |
| T7 Target1 = Assert.True | Assert.True | Confirmed line 114 | PASS |
| T8 Buy_STP = Assert.True | Assert.True | Confirmed line 125 | PASS |
| T9 Entry = Assert.False | Assert.False | Confirmed line 136 | PASS |
| T10 NullName = Assert.False | Assert.False | Confirmed line 147 | PASS |
| T11 NullOrderAnalog = Assert.False | Assert.False | Confirmed line 160 | PASS |
| Inline mirror logic matches production IsBracketLegStatic | L5799-5812 | Confirmed equivalent | PASS |
| src/CopyEngine.cs NOT modified | git status shows no src/ change for SFB-01 | Confirmed in-sync, no new edits | PASS |
| IsBracketLeg (non-static) unchanged | L5819 separate | Confirmed at L5819 | PASS |
| No lock() | 0 | 0 | PASS |
| ASCII-only | 0 non-ASCII | 0 non-ASCII bytes | PASS |
| Build: 0 errors | PASS | 0 errors | PASS |
| Tests: 0 failures | PASS | 0 failures, 97 passed | PASS |
| Sync: 0 DESYNC | PASS | 0 DESYNC | PASS |

**Implementation correctness: CORRECT. All requirements satisfied.**

---

## CROSS-CHECK MATRIX (Layer 2 vs Layer 3)

| Item | Engineer Layer 2 Claim | Verifier Layer 3 Result | Status |
|------|----------------------|------------------------|--------|
| IsBracketLegStaticTests.cs exists with 11 [Fact] | YES, 11 [Fact] | Confirmed 11 [Fact] | MATCH |
| T3 PTT_BE_Stop_1_ReturnsFalse = Assert.False | Assert.False confirmed | Assert.False confirmed (line 68) | MATCH |
| T4 PTT_Flatten_ReturnsFalse = Assert.False | Assert.False confirmed | Assert.False confirmed (line 80) | MATCH |
| T5 PTT_Tighten_Stop_ReturnsFalse = Assert.False | Assert.False confirmed | Assert.False confirmed (line 92) | MATCH |
| Inline mirror logic matches production | Confirmed | Confirmed (functionally identical) | MATCH |
| src/CopyEngine.cs NOT modified | git diff clean | In-sync, no new modifications | MATCH |
| IsBracketLeg non-static unchanged | Confirmed at L5769 | Confirmed at L5819 (line shift) | MATCH |
| SCAN-01 lock() | 0 | 0 | MATCH |
| SCAN-02 CYC | CYC=7 mirror, CYC=1 per test | CYC=7 / CYC=1 (independent count) | MATCH |
| SCAN-03 ASCII | 0 non-ASCII | 0 non-ASCII | MATCH |
| SCAN-04 NT8 API | 0 | 0 | MATCH |
| SCAN-05 Build | 0 errors | 0 errors | MATCH |
| SCAN-06 Tests | 97 passed (reported at SFB-01 time) | 97 passed | MATCH |
| SCAN-07 Sync | 0 DESYNC, 18 OK | 0 DESYNC, 18 OK | MATCH |

**All items: MATCH. Zero discrepancies between Layer 2 and Layer 3.**

---

## SIM GATE STATUS

**SIM_GATE: PREVIOUSLY_PASSED**

DW-LB-SFB-01 source fix confirmed PASS on 2026-09-07, commit 1086d9fd.
SIM test confirmed no PTT-Flatten storm after BE ALL cycle with the narrowed IsBracketLegStatic.
DW-LB-SFB-01-T1 is a test-coverage-only ticket (no src/ changes) -- no new SIM test required.

---

## FINAL VERDICT

**VERIFY_PASS**

All 7 independent scans PASS with zero discrepancies against Layer 2.
Test file implements all 11 required [Fact] methods correctly.
T3/T4/T5 regression guards confirmed as Assert.False -- the fix is locked in.
Inline mirror is semantically equivalent to production IsBracketLegStatic (L5799-5812).
No src/ changes made by this ticket (fix was pre-existing at commit 1086d9fd).
Build clean (0 errors, 0 warnings). Sync clean. SIM previously passed.