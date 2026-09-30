# B171 Ticket-3 Completion Report

**Ticket ID:** B171-3
**Block:** B171 — DW-B171-01 + DW-B171-02 test coverage
**File:** `src/PropTraderTools/CopyEngineTests.cs`
**Engineer:** ptt-engineer (Phase 4a)
**Status:** BUILD_PASS

---

## Scope

5 xUnit `[Fact]` tests added to `CopyEngineTests.cs` for B171 suffix fix coverage
(DW-B171-01 duplicate stop deduplication + DW-B171-02 fo=NULL on stop drag).

All tests use the established reflection-based test-seam pattern
(`GetMethod(..., BindingFlags.NonPublic | BindingFlags.Instance)`) consistent with
the existing test file. All tests carry
`[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]` matching the
existing skip pattern.

---

## Test Methods Added (exact names)

1. `ExecutePhaseCStopReplacement_SingleTargetDrag_ExactlyOneSuffixedStopPerFollower`
2. `ExecutePhaseCStopReplacement_TwoDragsOnSameBracket_DuplicateCancelled_StillOneStop`
3. `ExecutePhaseCStopReplacement_TwoDragsOnDifferentBrackets_TwoDistinctSuffixedStops`
4. `StopDragAfterTargetDrag_FindsFollowerOrder_FoNotNull`
5. `ExecutePhaseCStopReplacement_StpZeroOrNegative_NoCancelFired_ExistingStopPreserved`

Inserted before the closing `}` of class `CopyEngineTests` (lines 10316–10453 after patch).

---

## 7-Scan Results

### SCAN-01 — No `lock(` in new tests
```
Select-String -Path src/PropTraderTools/CopyEngineTests.cs -Pattern 'lock\(' |
  Where-Object { $_.LineNumber -gt 10314 }
```
**Result:** 0 matches in new tests.
(5 pre-existing matches are all in comment text `no lock()` within older tests — not
actual lock statements. None were introduced by this ticket.)

### SCAN-02 — All 5 [Fact] test names present
```
Select-String ... -Pattern "ExecutePhaseCStopReplacement_SingleTargetDrag|TwoDragsOnSameBracket|
  TwoDragsOnDifferentBrackets|StopDragAfterTargetDrag|StpZeroOrNegative"
```
**Result:** 5 matches at lines 10320, 10341, 10363, 10384, 10411.

### SCAN-03 — No `DateTime.Now` or `FontFamily` in new tests
```
Select-String ... -Pattern "DateTime\.Now[^U]|FontFamily" | Where-Object { $_.LineNumber -gt 10314 }
```
**Result:** 0 matches.

### SCAN-04 — `IsDragNameMatch` referenced in Test 4
```
Select-String -Path src/PropTraderTools/CopyEngineTests.cs -Pattern "IsDragNameMatch"
```
**Result:** 5 lines at 10386–10404 (Test 4 body). Confirmed — Test 4 calls
`IsDragNameMatch` with 3 parameters `("PTT-STP-Drag-3", true, "3")` (match)
and `("PTT-STP-Drag", true, "3")` (no match). Correctly reflects actual
`IsDragNameMatch(string orderName, bool isStop, string legSuffix)` signature.

### SCAN-05 — ASCII-only in all new test string literals
```
$content = Get-Content ... | Select-Object -Skip 10314
$content | Where-Object { $_ -match '[^\x00-\x7F]' } | Measure-Object
```
**Result:** 0 non-ASCII characters in new tests.

### SCAN-06 — `apply_diff` used (not `write_file`)
`apply_diff` was used to insert the 5 test methods before the class closing `}`.
`write_file` was NOT called on `CopyEngineTests.cs`.
**Result:** CONFIRMED.

### SCAN-07 — `ptt-sync.ps1` run confirmation
```
powershell -File .\scripts\ptt-sync.ps1
```
**Output:**
```
LINKED  AtrSizingEngine.cs
LINKED  CopyEngine.cs
LINKED  FeatureFlags.cs
LINKED  LicenseClient.cs
LINKED  TradeCopierAddOn.cs
LINKED  TradeCopierPanel.cs
LINKED  TradeCopierWindow.cs
LINKED  Core\PttContracts.cs
LINKED  Features\PttBreakEven.cs
LINKED  Features\PttBreakEvenSwap.cs
LINKED  Features\PttCancel.cs
LINKED  Features\PttCopier.cs
LINKED  Features\PttFlatten.cs
LINKED  Features\PttFollowerStrategy.cs
LINKED  Features\PttGlobalBreakEven.cs
LINKED  Features\PttGlobalQuickExit.cs
LINKED  Features\PttQuickExit.cs
LINKED  Features\PttTrim.cs

ptt-sync complete: 18 linked, 0 errors
```
**Result:** Exit code 0. Hard links re-synced.

---

## Jane Street Compliance

| Rule | Status | Notes |
|------|--------|-------|
| JS-021 — No `lock()` | PASS | No lock statement in any new test |
| JS-001 — No unhandled throw | PASS | Tests use `Assert.*`; no bare throw |
| JS-002 — Void return | PASS | All test methods are `public void` |
| ASCII-only literals | PASS | All string literals pure ASCII |
| No `DateTime.Now` | PASS | Not introduced |
| No `FontFamily` | PASS | Not introduced |
| CYC <= 8 | PASS | All test methods CYC <= 2 |

---

## Implementation Notes

- `IsDragNameMatch` has 3 parameters in the actual source: `(string orderName, bool isStop, string legSuffix)`.
  The ticket spec described 2 params but the actual method signature is 3. Test 4 was implemented
  with the correct 3-parameter call using `isStop=true` for stop-drag verification.
  This is correct: the test still verifies the fo != NULL fix path as specified.
- `apply_diff` was used exclusively. `write_file` was not called on existing `.cs` files.
- `ptt-sync.ps1` run once after all three B171 tickets complete.

---

**BUILD_PASS**
