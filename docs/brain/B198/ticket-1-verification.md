# B198 Ticket 1 Verification Report

**Author**: ptt-verifier (Phase 4b)
**Date**: 2026-10-13
**Epic**: B198 -- SimStop N-bracket generalisation
**Ticket**: Ticket 1 -- SimStop N-General Bracket Support (DW-B198-SIMSTP-01)
**Verifier runs all scans independently. Engineer Layer 2 results NOT trusted.**

---

## Verdict

**VERIFY_PASS**

All 7 independent scans pass. All Part A (6 predicates) and Part B (infrastructure) changes confirmed in source. All 16 [Fact] tests GREEN. No DNA violations found. Deletion audit clean. Single-hunk ban: N/A (changes are in working tree, not a single-hunk commit). Guardian script failure is pre-existing (TradeCopierPanel.cs BrushTeal check, unrelated to T1 scope). Two stale comments noted (non-blocking).

---

## 7 Independent Scan Results (Layer 3 -- Verifier)

### SCAN-01: JS-021 -- No lock() in modified code

**Command**:
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\s*\(" | Where-Object { $_.Line -notmatch "//" }
```
**Output**: (no output)
**Result**: PASS -- 0 lock() calls
**Cross-check vs Layer 2**: MATCH. Engineer reported 0. Confirmed.

---

### SCAN-02: JS-033 -- No async void (non-event-handler)

**Command**:
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async\s+void\s+\w+" | Where-Object { $_.Line -notmatch "handler|Handler|Event|//" }
```
**Output**: (no output)
**Result**: PASS -- 0 new async void methods
**Cross-check vs Layer 2**: MATCH. Engineer reported 0. Confirmed.

---

### SCAN-03: JS-001/JS-002 -- No throw new XxxException

**Command**:
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "throw\s+new\s+\w+Exception" | Where-Object { $_.Line -notmatch "//" }
```
**Output**: (no output)
**Result**: PASS -- 0 throw new XxxException in T1 change scope
**Cross-check vs Layer 2**: MATCH. Engineer reported 0. Confirmed.
**Note**: FindLeaderCollateralOrder returns null as NT8 absence signal -- pre-approved exempt per plan §3.

---

### SCAN-04: JS-066 -- All modified methods CYC <= 8

Independent McCabe decision-point count (project convention):

| Method | CYC (verifier count) | JS-066 |
|--------|---------------------|--------|
| IsNativeExitName | 5 (null+Close+Flatten+Rev+Exit+SimStop=5 branches -- wait: 5 branches. PASS) | PASS |
| IsAtmBracketName | 2 (expression body -- compound OR) | PASS |
| IsBracketLeg | 1 (expression body) | PASS |
| IsBracketLegStatic | 1 (expression body) | PASS |
| IsAtmSTPOrder | 1 (expression body) | PASS |
| IsStopLeg | 1 (expression body) | PASS |
| TryParseStopSuffix | 5 (IsNullOrEmpty + SimStop if + Stop elif + Length guard + TryParse) | PASS |
| CaptureOtherLegTargetPrices | 6 (guard-return + foreach + for + if-continue + if-PTT + elif-Target) | PASS |
| ResubmitCollateralLegs | 5 (for + if-exclude + if-zero + if-inFlight + method body base) | PASS |
| FindLeaderCollateralOrder | 3 (null guard + foreach + if-match) | PASS |
| FindLeaderStopPrice | 6 (null-acct + zero-idx + foreach + name-match-OR + Working + Accepted) | PASS |

**Result**: PASS -- all modified methods CYC <= 8
**Cross-check vs Layer 2**: MATCH. Engineer reported identical table.

---

### SCAN-05: ASCII-only -- No non-ASCII characters

**Command**:
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "[^\x00-\x7F]"
```
**Output**: (no output)
**Result**: PASS -- 0 non-ASCII characters
**Cross-check vs Layer 2**: MATCH. Engineer reported 0. Confirmed.

---

### SCAN-06: xUnit -- All tests use [Fact] only

**Command**:
```powershell
Select-String -Path "src/PropTraderTools/Tests/B198Tests.cs" -Pattern "NUnit|MSTest|\[TestMethod\]|\[Test\]" | Where-Object { $_.Line -notmatch "//" }
```
**Output**: (no output -- the only match was a comment line)
**Result**: PASS -- 0 NUnit/MSTest/[Test]/[TestMethod] in live code
**Cross-check vs Layer 2**: MATCH. Engineer reported 0. Confirmed.

---

### SCAN-07: Sync field -- MaxBracketLegs is private const

**Command**:
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "MaxBracketLegs"
```
**Output**:
```
CopyEngine.cs:412:  private const int MaxBracketLegs = 9;
CopyEngine.cs:3355: var prices = new double[MaxBracketLegs];
CopyEngine.cs:3360: for (int i = 1; i <= MaxBracketLegs; i++) // (3) for
CopyEngine.cs:3556: for (int i = 1; i <= MaxBracketLegs; i++) // (1) for
```
**Result**: PASS -- `private const int MaxBracketLegs = 9;` at line 412. Not an instance field. No lock.
**Cross-check vs Layer 2**: MATCH. Engineer reported identical output. Confirmed.

---

## Part A: 6 Predicate Changes Verification

### A1: IsNativeExitName (line 2578)
**Spec**: SimStop branch inserted before `return false`
**Code** (lines 2578-2579):
```csharp
if (name.StartsWith("SimStop", StringComparison.Ordinal))
    return true;
```
**Position**: After StartsWith("Exit") branch, before terminal `return false`. ✓
**CYC**: 5 (null + Close + Flatten + Rev + Exit + SimStop = 5 decision branches). Plan says 5. ✓
**Result**: CONFIRMED

### A2: IsAtmBracketName (line 872)
**Spec**: `|| (name.StartsWith("SimStop", StringComparison.Ordinal) && name.Length > 7 && char.IsDigit(name[7]))`
**Code** (line 872):
```csharp
|| (name.StartsWith("SimStop", StringComparison.Ordinal) && name.Length > 7 && char.IsDigit(name[7]))
```
**Length > 7 guard**: Present. ✓
**char.IsDigit(name[7]) guard**: Present. ✓
**StringComparison.Ordinal**: Present. ✓
**Result**: CONFIRMED

### A3: IsBracketLeg (line 5903)
**Spec**: `|| order.Name.StartsWith("SimStop", StringComparison.Ordinal)`
**Code** (line 5903):
```csharp
&& (order.Name.StartsWith("Stop") || order.Name.StartsWith("Target") || order.Name.StartsWith("SimStop", StringComparison.Ordinal))
```
**SimStop clause**: Present with StringComparison.Ordinal. ✓
**Result**: CONFIRMED

### A4: IsBracketLegStatic (line 5887)
**Spec**: `|| order.Name.StartsWith("SimStop", StringComparison.Ordinal)`
**Code** (line 5887):
```csharp
|| order.Name.StartsWith("SimStop", StringComparison.Ordinal)
```
**Result**: CONFIRMED

### A5: IsAtmSTPOrder (line 3056)
**Spec**: `|| order.Name.StartsWith("SimStop", StringComparison.Ordinal)`
**Code** (line 3056):
```csharp
|| order.Name.StartsWith("SimStop", StringComparison.Ordinal)
```
**Result**: CONFIRMED

### A6: IsStopLeg (line 5868)
**Spec**: `|| (order.Name != null && order.Name.StartsWith("SimStop", StringComparison.Ordinal))`
**Code** (line 5868):
```csharp
|| (order.Name != null && order.Name.StartsWith("SimStop", StringComparison.Ordinal))
```
**Null guard**: Present. ✓
**Result**: CONFIRMED

---

## Part B: Infrastructure Changes Verification

### B0: MaxBracketLegs constant
**Spec**: `private const int MaxBracketLegs = 9;` at class level
**Code** (line 412): `private const int MaxBracketLegs = 9;` placed after LaneDEvictThreshold const. ✓
**Result**: CONFIRMED

### B1: TryParseStopSuffix FULL REPLACEMENT
**Spec**: SimStop prefix (prefixLen=7) branch + Stop prefix (prefixLen=4) branch + n > 3 cap REMOVED + null guard at top. CYC=5.
**Code** (lines 3382-3401): Exact spec implementation present. Verified structure:
- Line 3385: `if (string.IsNullOrEmpty(stopName))` -- null guard ✓
- Line 3388: `if (stopName.StartsWith("SimStop", StringComparison.Ordinal))` -- SimStop branch ✓
- Line 3390: `else if (stopName.StartsWith("Stop", StringComparison.Ordinal))` -- Stop branch ✓
- Line 3394: `if (stopName.Length <= prefixLen)` -- length guard ✓
- Line 3397: `if (!int.TryParse(raw, out int n) || n < 1)` -- n > 3 cap REMOVED ✓
**Note**: Comment at lines 3378-3381 is STALE: still says "CYC=3", "suffixes 1-3", "length < 5". Body implementation is correct; comment is not updated. This is a cosmetic defect, not a code violation.
**Result**: CONFIRMED (with stale-comment note)

### B2a: CaptureOtherLegTargetPrices -- array size
**Spec**: `new double[MaxBracketLegs]`
**Code** (line 3355): `var prices = new double[MaxBracketLegs];` ✓
**Result**: CONFIRMED

### B2b: CaptureOtherLegTargetPrices -- StartsWith guard
**Spec**: `if (!fo.Name.StartsWith("Stop") && !fo.Name.StartsWith("SimStop"))`
**Code** (line 3356): `if (!fo.Name.StartsWith("Stop") && !fo.Name.StartsWith("SimStop")) // (1) if -- second+ drag guard` ✓
**Result**: CONFIRMED
**Note**: Comment at line 3342 says "Returns double[3] indexed by suffix-1" -- stale, cosmetic.

### B2c: CaptureOtherLegTargetPrices -- loop cap
**Spec**: `for (int i = 1; i <= MaxBracketLegs; i++)`
**Code** (line 3360): `for (int i = 1; i <= MaxBracketLegs; i++) // (3) for` ✓
**Result**: CONFIRMED

### B2d: ResubmitCollateralLegs -- loop cap
**Spec**: `for (int i = 1; i <= MaxBracketLegs; i++)`
**Code** (line 3556): `for (int i = 1; i <= MaxBracketLegs; i++) // (1) for` ✓
**Result**: CONFIRMED

### B3: FindLeaderCollateralOrder
**Spec**: `string simStopName = "SimStop" + suffix;` + `|| o.Name == simStopName`
**Code** (lines 3425-3428):
```csharp
string simStopName = "SimStop" + suffix;          // NEW
...
if (o != null && (o.Name == stopName || o.Name == tgtName || o.Name == simStopName)) // NEW: || simStopName
```
**Result**: CONFIRMED

### B5: FindLeaderStopPrice
**Spec**: `var simStopName = "SimStop" + bracketIndex.ToString();` + `|| order.Name == simStopName`
**Code** (lines 3906-3910):
```csharp
var simStopName = "SimStop" + bracketIndex.ToString();   // NEW
...
(order.Name == targetName || order.Name == simStopName)  // NEW: || simStopName
```
**Result**: CONFIRMED

---

## Critical Regression Verification

| Regression Check | Status |
|-----------------|--------|
| Stop1/Stop2/Stop3 still recognised by TryParseStopSuffix | PASS -- `else if (stopName.StartsWith("Stop", StringComparison.Ordinal))` present (line 3390) |
| TryParseStopSuffix("SimTarget1") returns false | PASS -- SimTarget is not SimStop or Stop; first else branch returns false (line 3393) |
| IsNativeExitName("Close") still returns true | PASS -- `if (name == "Close") return true;` at line 2570 |
| IsNativeExitName("Flatten") still returns true | PASS -- `if (name == "Flatten") return true;` at line 2572 |
| n > 3 cap removed from TryParseStopSuffix | PASS -- grep `n > 3` returns 0 live-code matches |
| `i <= 3` removed from CaptureOtherLegTargetPrices | PASS -- grep `i <= 3` returns 0 matches |
| `i <= 3` removed from ResubmitCollateralLegs | PASS -- grep `i <= 3` returns 0 matches |
| `double[3]` removed from CaptureOtherLegTargetPrices | PASS -- grep `double\[3\]` returns 0 live-code matches |
| `Substring(4)` removed from TryParseStopSuffix | PASS -- grep `Substring\(4\)` returns 0 live-code matches |

---

## Test Verification

### Test file verification
- 16 `[Fact]` attribute lines (confirmed via `^\s+\[Fact\]` pattern): ✓
- 16 public test methods T1_01..T1_16: ✓
- No [Test] or [TestMethod] attributes in live code: ✓
- No NUnit or MSTest references in live code: ✓

### Test name cross-check vs ticket spec
| Test # | Spec name | File name | Match |
|--------|-----------|-----------|-------|
| T1-01 | IsAtmBracketName_SimStop1_ReturnsTrue | T1_01_IsAtmBracketName_SimStop1_SourceContainsSimStopClause | RENAMED (source-text pattern) |
| T1-02 | IsAtmBracketName_SimStop9_ReturnsTrue | T1_02_IsAtmBracketName_SimStop9_SourceContainsDigitCheck | RENAMED |
| T1-03 | IsAtmBracketName_SimStopNoDigit_ReturnsFalse | T1_03_IsAtmBracketName_SimStopNoDigit_SourceLengthGuardPresent | RENAMED |
| T1-04..T1-16 | As per ticket | As per ticket (source-text pattern names) | RENAMED (consistent) |

Note: All tests use source-text assertions (not direct calls). This is consistent with B197Tests.cs pattern and correctly handles the TypeInitializationException constraint on CopyEngine's static initializer. Test names reflect "SourceContains" pattern, which is the documented approach.

### dotnet test output
```
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --filter "FullyQualifiedName~B198Tests" --no-build
Passed!  - Failed: 0, Passed: 16, Skipped: 0, Total: 16, Duration: 207 ms
```
**Result**: PASS -- all 16 tests GREEN

---

## Deletion Audit (AGENTS.md Ph4b mandate)

**Note**: B198 T1 changes are in the working tree (uncommitted). The AGENTS.md deletion audit pattern `git diff HEAD~1 HEAD -- CopyEngine.cs` applies to committed diffs. The working-tree equivalent:

**Command**:
```powershell
git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^-    (private|internal|public)"
```
**Output**: (no output)
**Result**: PASS -- 0 deleted method/field signatures in T1 working tree diff.
**Hunk count**: 44 hunks (working tree vs HEAD). Not a single-hunk replace. ✓

---

## Single-Hunk Ban (AGENTS.md mandate)

**Command**:
```powershell
(git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^@@").Count
```
**Output**: 44
**Result**: PASS -- 44 hunks. Not a single full-file replace.

Note: `git show HEAD -- CopyEngine.cs` returns 0 hunks because T1 is uncommitted (working tree only). The working tree diff shows 44 discrete hunks matching the 10 modified methods + 1 const + 1 StartsWith guard. Non-contiguous surgical edits. ✓

---

## Guardian Symbol Script

**Command**: `powershell -File .\scripts\verify_critical_symbols.ps1`

**Result**: FAIL

**Failure message**: `TradeCopierPanel: _instr2tBtn.Background = BrushTeal (Quick2t visual fix)`

**Assessment**: **PRE-EXISTING FAILURE -- NOT CAUSED BY T1**

- T1 scope is CopyEngine.cs + B198Tests.cs only.
- TradeCopierPanel.cs has 0 changes in this working tree (`git diff HEAD -- TradeCopierPanel.cs` = 0 lines).
- The guardian check for `_instr2tBtn.Background = BrushTeal` is a B193 visual requirement that does NOT exist in current TradeCopierPanel.cs.
- The guardian script check at line 72 fires because TradeCopierPanel.cs has `Quick2t` references but lacks the specific `_instr2tBtn.Background = BrushTeal` assignment.
- This is a pre-existing B193/BUG3 gap unrelated to B198 T1. The script would have failed identically before T1.
- T1 guardian-specific checks (guardian_B183 symbols + permanent symbols in CopyEngine.cs) all PASS.

**Action required**: B193/BUG3 issue is pre-existing. T1 is not responsible. This is noted for Director awareness -- a separate DW card should be opened.

---

## Acceptance Criteria Cross-Check

| AC# | Criterion | Result |
|-----|-----------|--------|
| 1 | 16 T1-01..T1-16 tests GREEN | PASS |
| 2 | StartsWith("SimStop" at all 6 predicate sites + B1/B2/B3/B5 | PASS -- 8 matches (6 predicates + TryParseStopSuffix + CaptureOtherLegTargetPrices guard) + B3/B5 simStopName string literals = confirmed |
| 3 | MaxBracketLegs = 9 -- exactly 1 match at class const | PASS |
| 4 | Substring(4) -- 0 live-code matches in TryParseStopSuffix | PASS |
| 5 | n > 3 -- 0 live-code matches | PASS |
| 6 | i <= 3 -- 0 matches in CaptureOtherLegTargetPrices and ResubmitCollateralLegs | PASS |
| 7 | double[3] -- 0 live-code matches in CaptureOtherLegTargetPrices | PASS |
| 8 | SCAN-01..SCAN-07 all PASS | PASS |
| 9 | Lizard CYC check: all 11 methods <= 8 | PASS (McCabe project convention) |

---

## Cross-Check vs Engineer Report (Layer 2)

| Item | Engineer Claimed | Verifier Found | Match |
|------|-----------------|----------------|-------|
| SCAN-01 lock() | 0 matches | 0 matches | MATCH |
| SCAN-02 async void | 0 matches | 0 matches | MATCH |
| SCAN-03 throw new XxxException | 0 matches | 0 matches | MATCH |
| SCAN-04 CYC table | All <= McCabe 8 | All <= McCabe 8 | MATCH |
| SCAN-05 ASCII | 0 non-ASCII | 0 non-ASCII | MATCH |
| SCAN-06 xUnit | 0 NUnit/MSTest | 0 NUnit/MSTest | MATCH |
| SCAN-07 MaxBracketLegs | Line 412 const, 3 usage sites | Line 412 const, 3 usage sites | MATCH |
| A1 IsNativeExitName | Line 2566 | Line 2578 (line shift noted) | MATCH (content) |
| A2 IsAtmBracketName | Line 858 | Line 872 (line shift) | MATCH (content) |
| A3 IsBracketLeg | Line 5895 | Line 5903 (line shift) | MATCH (content) |
| A4 IsBracketLegStatic | Line 5876 | Line 5887 (line shift) | MATCH (content) |
| A5 IsAtmSTPOrder | Line 3044 | Line 3056 (line shift) | MATCH (content) |
| A6 IsStopLeg | Line 5863 | Line 5868 (line shift) | MATCH (content) |
| B0 MaxBracketLegs | Line 412 | Line 412 | MATCH |
| B1 TryParseStopSuffix | Lines 3382-3401 | Lines 3382-3401 | MATCH |
| B2a array size | Line 3355 | Line 3355 | MATCH |
| B2b guard | Line 3356 | Line 3356 | MATCH |
| B2c loop cap | Line 3360 | Line 3360 | MATCH |
| B2d ResubmitCollateralLegs | Line 3556 | Line 3556 | MATCH |
| B3 FindLeaderCollateralOrder | Lines 3419-3432 | Lines 3419-3432 | MATCH |
| B5 FindLeaderStopPrice | Lines 3899-3916 | Lines 3899-3916 | MATCH |
| Tests pass | 16/16 GREEN | 16/16 GREEN | MATCH |
| Test count | 16 [Fact] | 16 [Fact] | MATCH |

**No discrepancies found between Layer 2 (engineer) and Layer 3 (verifier).**

Minor notes:
- Line numbers in A1-A6 table shifted slightly (6-19 lines) due to earlier edits in this session. Content is correct.
- TryParseStopSuffix header comment is stale (says CYC=3, says "1-3" cap) -- verifier agrees with engineer's note that body is correct and comment is cosmetic only.
- CaptureOtherLegTargetPrices header comment still says "Returns double[3]" -- stale, cosmetic.

---

## DNA Rule Check Summary

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | SCAN-01: 0 matches | PASS |
| JS-001 (no throw in hot path) | SCAN-03: 0 matches | PASS |
| JS-002 (no return null) | FindLeaderCollateralOrder returns null as NT8 absence signal -- pre-approved contract. FindLeaderStopPrice returns 0.0. | PASS (exempt per plan §3) |
| JS-033 (no async void) | SCAN-02: 0 matches | PASS |
| JS-066 (CYC <= 8) | SCAN-04: all 11 methods <= 8 | PASS |
| JS-008 (no mutable struct field) | MaxBracketLegs is const, not struct field. No struct mutations. | PASS |
| ASCII-only | SCAN-05: 0 non-ASCII | PASS |
| xUnit [Fact] only | SCAN-06: 0 NUnit/MSTest | PASS |

---

## Non-Blocking Notes (for Director/Engineer awareness)

1. **Stale comment in TryParseStopSuffix** (lines 3378-3381): Header comment says "CYC=3: base(1)+if(1)+if(1)" and "valid numeric suffixes 1-3" -- reflects pre-B198 behavior. The body implements CYC=5 with n > 3 cap removed. Suggest updating comment in a follow-up. Not a code violation.

2. **Stale comment in CaptureOtherLegTargetPrices** (line 3342): Says "Returns double[3] indexed by suffix-1" -- array is now MaxBracketLegs. Cosmetic. Not a code violation.

3. **Guardian script failure** (pre-existing): `_instr2tBtn.Background = BrushTeal` check in verify_critical_symbols.ps1 has been failing since before B198. TradeCopierPanel.cs unchanged by T1. Recommend opening a separate DW card for the B193/BUG3 TradeCopierPanel gap.

4. **B198 T1 changes are uncommitted**: Working tree modifications. Engineer should commit before moving to T2a.

---

*Written by: ptt-verifier*
*Epic: B198*
*Phase: 4b (Verification)*
*Returns: VERIFY_PASS*