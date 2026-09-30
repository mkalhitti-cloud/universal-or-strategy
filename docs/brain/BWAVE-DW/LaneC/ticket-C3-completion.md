# Ticket C-3 Completion Report

**Ticket**: C-3 — Test Name Inversions (5 Renames)
**Epic**: BWAVE-DW LaneC
**Branch**: `feature/bwave-dw-lane-c`
**Engineer**: ptt-engineer
**Date**: 2026-09-04
**Result**: BUILD_PASS

---

## Summary

5 test method names in `src/PropTraderTools/Tests/BwaveCycLaneBTests.cs` were renamed to match
their actual assertions. Pure rename — zero assertion changes, zero logic changes.

---

## Rename Mapping (old → new)

| DW Item | Line | Old Name (inverted) | New Name (correct) | Assert (unchanged) |
|---------|------|---------------------|--------------------|--------------------|
| DW-B37-02 | 433 | `IsBeRetryEligible_ReturnsFalse_WhenPositionIsFlat` | `IsPttBeRetryTriggerOrder_ReturnsTrue_WhenNameIsPttQxT` | `Assert.True(result)` |
| DW-B37-04 | 546 | `IsNativeExitName_ReturnsTrue_WhenNameIsTarget` | `IsNativeExitName_ReturnsFalse_WhenNameIsTarget` | `Assert.False(result)` |
| DW-B37-06 | 707 | `ResolveMultipliers_ReturnsAllOnes_WhenMultipliersNull` | `ResolveMultipliers_ReturnsNull_WhenMultipliersNull` | `Assert.Null(result)` |
| DW-B37-07 | 723 | `SelectRefPriceByDirection_ReturnsBid_WhenLongAndBidPositive` | `SelectRefPriceByDirection_ReturnsAsk_WhenLong` | `Assert.Equal(101.0, result)` |
| DW-B37-08 | 752 | `SelectRefPriceByDirection_ReturnsAsk_WhenShortAndAskPositive` | `SelectRefPriceByDirection_ReturnsBid_WhenShort` | `Assert.Equal(100.0, result)` |

---

## Assert Statement Verification

Each method body was read before and after rename to confirm byte-for-byte body identity:

- **DW-B37-02**: Body: `CopyEngine.IsPttBeRetryTriggerOrderTestable("PTT-QX-T1")` → `Assert.True(result)` — **unchanged**
- **DW-B37-04**: Body: `CopyEngine.IsNativeExitName("Target1")` → `Assert.False(result)` — **unchanged**
- **DW-B37-06**: Body: `CopyEngine.ResolveMultipliers(dto)` → `Assert.Null(result)` — **unchanged**
- **DW-B37-07**: Body: `CopyEngine.SelectRefPriceByDirection(isLong: true, bid: 100.0, ask: 101.0)` → `Assert.Equal(101.0, result)` — **unchanged**
- **DW-B37-08**: Body: `CopyEngine.SelectRefPriceByDirection(isLong: false, bid: 100.0, ask: 101.0)` → `Assert.Equal(100.0, result)` — **unchanged**

---

## 7-Scan Results

| Scan | Check | Result | Notes |
|------|-------|--------|-------|
| SCAN-01 | `lock()` usage | **0** | 4 matches are comment text only (`No lock().`), not code |
| SCAN-02 | `async void` | **0** | No matches |
| SCAN-03 | `return null` (code) | **0** | 2 matches are comment text only, not code statements |
| SCAN-04 | `throw new` | **0** | No matches |
| SCAN-05 | CYC unchanged | **PASS** | Pure rename — no branching logic modified |
| SCAN-06 | ASCII-only | **PASS** | 3 non-ASCII bytes = UTF-8 BOM only; zero content non-ASCII |
| SCAN-07 | xUnit only | **0** | No NUnit/MSTest imports or attributes |

---

## Old Names Absent Verification

```
Select-String -Pattern "IsBeRetryEligible_ReturnsFalse_WhenPositionIsFlat|IsNativeExitName_ReturnsTrue_WhenNameIsTarget|ResolveMultipliers_ReturnsAllOnes_WhenMultipliersNull|SelectRefPriceByDirection_ReturnsBid_WhenLongAndBidPositive|SelectRefPriceByDirection_ReturnsAsk_WhenShortAndAskPositive"
```
Result: **0 matches** — all 5 old (inverted) names absent from file.

---

## Build Result

```
dotnet build src/PropTraderTools/PropTraderTools.csproj --no-incremental
Build succeeded.
  1 Warning(s)  [pre-existing: xUnit2004 in B131Tests.cs — unrelated to this ticket]
  0 Error(s)
```

**BUILD_PASS**

---

## DW Items Closed

- [x] DW-B37-02 — `IsPttBeRetryTriggerOrder_ReturnsTrue_WhenNameIsPttQxT` (was ReturnsFalse)
- [x] DW-B37-04 — `IsNativeExitName_ReturnsFalse_WhenNameIsTarget` (was ReturnsTrue)
- [x] DW-B37-06 — `ResolveMultipliers_ReturnsNull_WhenMultipliersNull` (was ReturnsAllOnes)
- [x] DW-B37-07 — `SelectRefPriceByDirection_ReturnsAsk_WhenLong` (was ReturnsBid/Long)
- [x] DW-B37-08 — `SelectRefPriceByDirection_ReturnsBid_WhenShort` (was ReturnsAsk/Short)

---

*ptt-engineer | BWAVE-DW LaneC Ticket C-3 | BUILD_PASS*
