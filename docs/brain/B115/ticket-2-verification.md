# B115 Ticket T2 -- Verification Report

**Block**: B115
**Ticket**: T2
**DW Reference**: DW-B122
**Date**: 2026-08-27
**Verifier**: ptt-verifier (Phase 4b)
**Engineer Report**: docs/brain/B115/ticket-2-completion.md
**File Verified**: src/PropTraderTools/Tests/B115Tests.cs

---

## Layer 3 Scan Results (Independent — Run by Verifier)

### SCAN-01 -- lock() check
**Command**: Select-String -Path "src\PropTraderTools\Tests\B115Tests.cs" -Pattern "lock\("
**Result**: 0 matches
**Status**: PASS

### SCAN-02 -- async void check
**Command**: Select-String -Path "src\PropTraderTools\Tests\B115Tests.cs" -Pattern "async void"
**Result**: 1 hit on line 2 — comment text "JS-033: no async void." (documentation only, not executable code)
**Status**: PASS (no async void in method signatures or executable code)

### SCAN-03 -- throw new check
**Command**: Select-String -Path "src\PropTraderTools\Tests\B115Tests.cs" -Pattern "throw new"
**Result**: 0 matches
**Status**: PASS

### SCAN-04 -- return null check
**Command**: Select-String -Path "src\PropTraderTools\Tests\B115Tests.cs" -Pattern "return null"
**Result**: 0 matches
**Status**: PASS

### SCAN-05 -- new byte[] check
**Command**: Select-String -Path "src\PropTraderTools\Tests\B115Tests.cs" -Pattern "new byte\["
**Result**: 0 matches
**Status**: PASS

### SCAN-06 -- CYC check
**Command**: Select-String + manual branch count
**[Fact] methods found**: 4 (lines 27, 54, 75, 104)

| Method | Branch Count | CYC | <= 8? |
|--------|-------------|-----|-------|
| TryCleanupReArmedAtmBracket_GuardAcceptsAcceptedState (L27) | 0 | 1 | PASS |
| TryCleanupReArmedAtmBracket_GuardRejectsUnknownState (L54) | 0 | 1 | PASS |
| TryCleanupReArmedAtmBracket_DictSeam_T1Path_EntryRetained (L75) | 1 (if shouldRemove) | 2 | PASS |
| TryCleanupReArmedAtmBracket_DictSeam_T3Path_EntryRemoved (L104) | 1 (if shouldRemove) | 2 | PASS |

**Status**: PASS (all CYC <= 8; max CYC = 2)

### SCAN-07 -- ASCII-only check
**Command**: [System.IO.File]::ReadAllBytes byte scan > 127
**Result**: ZERO non-ASCII bytes
**Status**: PASS

---

## Cross-Check Layer 3 vs Layer 2

| Scan | Layer 2 (engineer self-report) | Layer 3 (verifier independent) | Agreement |
|------|-------------------------------|-------------------------------|-----------|
| SCAN-01 lock() | 0 matches | 0 matches | AGREE |
| SCAN-02 async void | 0 code hits (comment only) | 1 comment hit, 0 executable | AGREE |
| SCAN-03 throw new | 0 matches | 0 matches | AGREE |
| SCAN-04 return null | 0 matches | 0 matches | AGREE |
| SCAN-05 new byte[ | 0 matches | 0 matches | AGREE |
| SCAN-06 CYC | all <= 2 | all <= 2 (1,1,2,2) | AGREE |
| SCAN-07 non-ASCII | 0 bytes | 0 bytes | AGREE |

**Layer 2 vs Layer 3 discrepancies**: NONE

---

## Correctness Checks V1--V7

### V1 -- File exists
**Check**: Test-Path "src\PropTraderTools\Tests\B115Tests.cs"
**Result**: True
**Status**: PASS

### V2 -- Class name and namespace
**Check**: Class B115Tests in namespace PropTraderTools.Tests
**Result**: namespace PropTraderTools.Tests present; public class B115Tests present
**Status**: PASS

### V3 -- File header comment
**Check**: Header mentions "B115Tests.cs -- DW-B122"
**Result**: Line 1: // B115Tests.cs -- DW-B122 Accepted-state guard tests
**Status**: PASS

### V4 -- At least one [Fact] tests OrderState.Accepted specifically
**Check**: Method exercising OrderState.Accepted present
**Result**: TryCleanupReArmedAtmBracket_GuardAcceptsAcceptedState uses OrderState testState = OrderState.Accepted;
**Status**: PASS

### V5 -- Guard evaluates false for Accepted; Assert.False(guardEarly) present
**Check**: bool guardEarly = (testState != Working && testState != Accepted) for Accepted must be false
**Math verification**:
  - (Accepted != Working)   -> true
  - (Accepted != Accepted)  -> false
  - true && false            = false
**Result**: Assert.False(guardEarly, "DW-B122: Accepted state must NOT cause early return...") present at L48
**DW-B122 intent captured**: Accepted state correctly excluded from early-return (cleanup proceeds). PASS.
**Status**: PASS

### V6 -- xUnit framework only; no NUnit/MSTest
**Check**: using Xunit; present; no NUnit/MSTest/Microsoft.VisualStudio.TestTools
**Result**: using Xunit; at L11; Select-String for NUnit|MSTest: 0 matches
**Status**: PASS

### V7 -- No sealed NT8 types instantiated
**Check**: No new Order(, new OrderEventArgs(, new Account(, new Instrument(
**Result**: Select-String for all patterns: 0 matches
**Status**: PASS

---

## DNA Rule Check

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (lock ban) | No lock() in source | PASS |
| JS-033 (async void ban) | No async void in executable code | PASS |
| JS-001 (throw new ban) | No throw new XxxException | PASS |
| JS-002 (return null ban) | No return null | PASS |
| xUnit only | No NUnit/MSTest imported | PASS |
| NT8 sealed ban | No sealed NT8 types constructed | PASS |
| ASCII-only | Zero non-ASCII bytes | PASS |
| CYC <= 8 | Max CYC = 2 across all 4 [Fact] methods | PASS |

---

## Architecture Compliance

- File is a new test file (T2 contract: new file, new coverage for DW-B122). PASS.
- Seam used is _qxPendingFollowerCleanup (ConcurrentDictionary, accessible via InternalsVisibleTo). PASS.
- Direct NT8 invocation correctly avoided (OrderEventArgs is sealed, no public constructor). PASS.
- Dict-seam tests (T3 and T1 paths) included per architecture plan recommendation. PASS.
- CopyEngine.Instance._qxPendingFollowerCleanup.Clear() called at start of each dict-seam test. PASS.
- Production guard at CopyEngine.cs L2396-2398 confirmed: compound state check (state != Working && state != Accepted). PASS.

---

## Spec Coverage

| Acceptance Criterion | Status |
|----------------------|--------|
| File src/PropTraderTools/Tests/B115Tests.cs created | PASS |
| Namespace: PropTraderTools.Tests | PASS |
| Class: B115Tests | PASS |
| Framework: xUnit [Fact] only -- no [Theory], no NUnit, no MSTest | PASS |
| TryCleanupReArmedAtmBracket_GuardAcceptsAcceptedState present | PASS |
| Assert.False(guardEarly) for OrderState.Accepted | PASS |
| CopyEngine.Instance._qxPendingFollowerCleanup.Clear() in dict-seam tests | PASS |
| All 7 scans: zero violations | PASS |

---

## Overall Verdict

**VERIFY_PASS**

All 7 scans returned zero violations. All correctness checks V1--V7 passed. All DNA rules satisfied.
No discrepancy between Layer 2 (engineer self-report) and Layer 3 (verifier independent runs).
DW-B122 fix correctly tested: Assert.False(guardEarly) captures that OrderState.Accepted no longer
triggers early return in TryCleanupReArmedAtmBracket, matching the production guard at CopyEngine.cs L2397-2398.