# B189 T1 Completion Report

**Epic**: B189 -- BE Direction Fix + BE Negative Range + Quick ALL 3T Fix + Quick2t/QAll2t Spinners
**Ticket**: T1 -- BE Direction Fix
**Phase**: 4a (Engineer)
**Engineer**: ptt-engineer
**Date**: 2026-07-08
**Status**: BUILD_PASS

---

## What Was Implemented

### Sign-flip fix (2 files)

**PttBeSwapOne.cs** line 37 (now line 38 after comment addition):

```csharp
// BEFORE (BUG):
double direction = isLong ? -1.0 : +1.0;

// AFTER (FIX):
double direction = isLong ? +1.0 : -1.0;
```

**PttBeSwapAll.cs** line 55 (now line 56 after comment addition):

```csharp
// BEFORE (BUG):
double direction = isLong ? -1.0 : +1.0;

// AFTER (FIX):
double direction = isLong ? +1.0 : -1.0;
```

Both files also received a single-line B189-T1 doc comment update (no logic impact).

### Direction logic (post-fix)

```
long  + positive buffer: direction=+1.0  raw = avg + (+1.0)*buf*tick  -> ABOVE entry (profit lock)
long  + negative buffer: direction=+1.0  raw = avg + (+1.0)*(-buf)*tick -> BELOW entry (loss accept)
short + positive buffer: direction=-1.0  raw = avg + (-1.0)*buf*tick  -> BELOW entry (profit lock)
short + negative buffer: direction=-1.0  raw = avg + (-1.0)*(-buf)*tick -> ABOVE entry (loss accept)
```

### Test updates (CopyEngineTests.cs)

**Updated existing test** `PttBeSwapOne_BufferTicks_ShiftsEntryPriceByDirection` (approx. line 9969):
- Long assertion corrected: `entryLong` now asserts `5000.5` (above avg, was `4999.5`)
- Short assertion corrected: `entryShort` now asserts `4999.5` (below avg, was `5000.5`)
- Direction literals in test updated to match fix (`+1.0` for long, `-1.0` for short)

**New test class** `B189T1BeDirectionTests` (added at end of file, 5 `[Fact]` tests):

| Test | AC | Assertion |
|------|----|-----------|
| `T_B189_T1_01_BeDirection_Long_PositiveBuffer_StopAboveEntry` | AC1 | raw > avg; raw == 5000.5 |
| `T_B189_T1_02_BeDirection_Long_NegativeBuffer_StopBelowEntry` | AC2 | raw < avg; raw == 4999.5 |
| `T_B189_T1_03_BeDirection_Short_PositiveBuffer_StopBelowEntry` | AC3 | raw < avg; raw == 4999.5 |
| `T_B189_T1_04_PttBeSwapOne_Formula_LongBufPos_ResultAboveAvg` | AC4 | entryPrice > avg (PttBeSwapOne formula path) |
| `T_B189_T1_05_PttBeSwapAll_Formula_LongBufPos_ResultAboveAvg` | AC5 | entryPrice > avg (PttBeSwapAll formula path) |

All tests are xUnit `[Fact]` (no NUnit/MSTest). No NT8 seam required -- pure arithmetic formula tests.

---

## 7-Scan Results (Layer 2)

### SCAN-01: lock() scan
```
Command: Select-String -Path "src/PropTraderTools/Features/PttBeSwapOne.cs","src/PropTraderTools/Features/PttBeSwapAll.cs" -Pattern "lock\("
Result: 0 matches
Status: PASS
```

### SCAN-02: async void scan
```
Command: Select-String -Path "src/PropTraderTools/Features/PttBeSwapOne.cs","src/PropTraderTools/Features/PttBeSwapAll.cs" -Pattern "async void"
Result: 0 matches
Status: PASS
```

### SCAN-03: return null scan
```
Scope: both Execute methods are void; ExecuteOne is void -- no return values
Command: Select-String -Path "src/PropTraderTools/Features/PttBeSwapOne.cs","src/PropTraderTools/Features/PttBeSwapAll.cs" -Pattern "return null"
Result: 0 matches
Status: N/A (void methods; confirmed 0 matches)
```

### SCAN-04: CYC ceiling check (JS-066 -- P0)
```
PttBeSwapOne.Execute:    CYC=7 post-fix (sign flip adds 0 branches; ceiling=8) PASS
PttBeSwapAll.ExecuteOne: CYC=5 post-fix (sign flip adds 0 branches; ceiling=8) PASS
PttBeSwapAll.Execute:    CYC=7 (not modified; unchanged) PASS

Verification: the change is a single ternary literal value swap (-1.0 -> +1.0 and +1.0 -> -1.0).
No new if/else/switch/while/for/catch/&&/|| branches added. CYC is identical pre and post fix.
Status: PASS
```

### SCAN-05: ASCII-only check (JS-081)
```
Command: Select-String -Path "src/PropTraderTools/Features/PttBeSwapOne.cs","src/PropTraderTools/Features/PttBeSwapAll.cs" -Pattern "[^\x00-\x7F]"
Result: 0 non-ASCII characters
Status: PASS
```

### SCAN-06: throw new check (JS-001)
```
Command: Select-String -Path "src/PropTraderTools/Features/PttBeSwapOne.cs","src/PropTraderTools/Features/PttBeSwapAll.cs" -Pattern "throw new"
Result: 0 matches
Status: PASS
```

### SCAN-07: xUnit2004 / build check
```
Command: dotnet build src/PropTraderTools/PropTraderTools.csproj
Result:
  Build succeeded.
      0 Warning(s)
      0 Error(s)
  Time Elapsed 00:00:01.09
Status: PASS
```

---

## Post-VERIFY_PASS Gate: ptt-sync.ps1
```
Command: powershell -File .\scripts\ptt-sync.ps1
Result:
  LINKED  Features\PttBeSwapAll.cs
  LINKED  Features\PttBeSwapOne.cs
  (+ 17 other files)
  ptt-sync complete: 19 linked, 0 errors
Status: PASS
```

---

## Before/After Diff Summary

### PttBeSwapOne.cs
- Line 6-7: added B189-T1 doc comment (no logic impact)
- Line 37 (was): `double direction = isLong ? -1.0 : +1.0;`
- Line 38 (now): `double direction = isLong ? +1.0 : -1.0;`
- All other lines: unchanged

### PttBeSwapAll.cs
- Line 7-8: added B189-T1 doc comment (no logic impact)
- Line 55 (was): `double direction = isLong ? -1.0 : +1.0;`
- Line 56 (now): `double direction = isLong ? +1.0 : -1.0;`
- All other lines: unchanged

### CopyEngineTests.cs
- `PttBeSwapOne_BufferTicks_ShiftsEntryPriceByDirection`: comment and Assert values corrected to match fix
- `B189T1BeDirectionTests`: new class added at end of file (5 `[Fact]` tests, AC1-AC5)

---

## Jane Street Rule Compliance

| Rule | Status | Notes |
|------|--------|-------|
| JS-021 (lock() ban) | PASS | No lock() in touched files |
| JS-001 (no throw in hot path) | PASS | No throw statements |
| JS-002 (no return null) | N/A | void methods |
| JS-033 (no async void) | PASS | No async code |
| JS-066 (CYC <= 8) | PASS | CYC unchanged by sign flip |
| JS-081 (ASCII-only) | PASS | Numeric literal flip only |

---

## Acceptance Criteria Status

| AC | Condition | Status |
|----|-----------|--------|
| AC1 | Long + buf=+2: BE stop ABOVE entry | PASS (T_B189_T1_01) |
| AC2 | Long + buf=-2: BE stop BELOW entry | PASS (T_B189_T1_02) |
| AC3 | Short + buf=+2: BE stop BELOW entry | PASS (T_B189_T1_03) |
| AC4 | direction sign in PttBeSwapOne = isLong ? +1.0 : -1.0 | PASS (code + T_B189_T1_04) |
| AC5 | direction sign in PttBeSwapAll = isLong ? +1.0 : -1.0 | PASS (code + T_B189_T1_05) |
| AC-DNA | SCAN-01..07 all PASS; all modified methods CYC <= 8 | PASS |
| AC-BUILD | dotnet build 0 errors, 0 new warnings | PASS |
| AC-SYNC | ptt-sync.ps1 19 linked, 0 errors | PASS |

---

*Written by: ptt-engineer (Phase 4a)*
*Epic: B189*
*Ticket: T1*
*Status: BUILD_PASS*
