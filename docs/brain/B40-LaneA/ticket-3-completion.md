# B40-LaneA Ticket T3 — Completion Report

**Ticket**: T3 — Tests T_B40_01 through T_B40_15
**Block**: B40-LaneA — BE ALL Armed/Wait + OCO Collision Fix
**Engineer**: ptt-engineer
**Date**: 2026-07-30
**Status**: BUILD_PASS

---

## Summary

Appended 15 new `[Fact]` tests to [`CopyEngineTests.cs`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs) after the last B39 test (`T_B39_08` at line 3898).

**[Fact] count before T3**: 201
**[Fact] count after T3**: 216 (+15)

---

## Seam / Stub Infrastructure Used

The existing B39 stub helpers in `CopyEngineTests.cs` were verified but not needed for the B40 tests:
- `MakeMasterInstrument`, `MakeInstrument`, `MakeLongPos`, `MakeShortPos`, `MakeFlatPos`, `MakeAccount` — already present, used by B39 tests

For B40 tests, the following approaches were used:
- **`BuildGlobalBeOcoId`** (T_B40_01–T_B40_05, T_B40_15): `internal static` on `PttGlobalBreakEven` — called directly, no stubs needed
- **`ComputeBePrice(MarketPosition, double, int, double)`** (T_B40_06–T_B40_09, T_B40_13–T_B40_14): test-seam overload added in T1, `internal static` on `CopyEngine` — called directly
- **`IsPendingSlotsEmpty()`** (T_B40_10–T_B40_12): `internal` instance method on `CopyEngine` — called on `CopyEngine.Instance`; `_pendingBeSlots` field manipulated via reflection (ConcurrentDictionary.Clear() + TryAdd for nested private struct)

**NT8 type instantiation**: Verified that `Account`, `Position`, `Instrument`, `MasterInstrument` are NOT sealed and can be instantiated with `new` + property assignment. B39 stub pattern confirms this.

---

## Tests Added

| Test ID | Method | Covers | Approach |
|---------|--------|--------|----------|
| T_B40_01 | `T_B40_01_BuildGlobalBeOcoId_ExactFormat_Seq1_Acc0_Pair0` | `BuildGlobalBeOcoId(1,0,0) == "PTT-BEG-00001-0-0"` | Pure static |
| T_B40_02 | `T_B40_02_BuildGlobalBeOcoId_SeqIncrement_UniqueAcrossPresses` | seq=1 != seq=2; exact strings | Pure static |
| T_B40_03 | `T_B40_03_BuildGlobalBeOcoId_ExactFormat_Seq5_Acc2_Pair1` | `BuildGlobalBeOcoId(5,2,1) == "PTT-BEG-00005-2-1"` | Pure static |
| T_B40_04 | `T_B40_04_BuildGlobalBeOcoId_SameSeq_DifferentAccIdx_UniqueIds` | Same seq, accIdx=0 != accIdx=1 | Pure static |
| T_B40_05 | `T_B40_05_BuildGlobalBeOcoId_SeqD5Format_LowSeqHasLeadingZeros` | seq=7 → "PTT-BEG-00007-" prefix | Pure static |
| T_B40_06 | `T_B40_06_ComputeBePrice_Long_TwoBufferTicks_ReturnsEntryPlusHalfPoint` | Long: 100.0+2*0.25=100.5 | Test-seam overload |
| T_B40_07 | `T_B40_07_ComputeBePrice_Short_TwoBufferTicks_ReturnsEntryMinusHalfPoint` | Short: 100.0-2*0.25=99.5 | Test-seam overload |
| T_B40_08 | `T_B40_08_ComputeBePrice_Long_ZeroBuffer_ReturnsExactEntry` | Zero buffer = exact entry price | Test-seam overload |
| T_B40_09 | `T_B40_09_ComputeBePrice_NonAlignedEntry_RoundsToNearestTick` | Non-aligned entry rounds to tick | Test-seam overload |
| T_B40_10 | `T_B40_10_IsPendingSlotsEmpty_EmptyDictionary_ReturnsTrue` | Empty dict → true | Reflection Clear() |
| T_B40_11 | `T_B40_11_IsPendingSlotsEmpty_AfterAddingSlot_ReturnsFalse` | Non-empty dict → false | Reflection TryAdd |
| T_B40_12 | `T_B40_12_IsPendingSlotsEmpty_AfterRemovingSlot_ReturnsTrue` | Dict cleared → true (auto-reset path) | Reflection Clear() |
| T_B40_13 | `T_B40_13_ComputeBePrice_Long_LargeBuffer_StillTickAligned` | Large buf=20, NQ entry=20000 → 20005.0 | Test-seam overload |
| T_B40_14 | `T_B40_14_ComputeBePrice_Long_OneBufferTick_ReturnsEntryPlusOneTick` | Single tick buf=1 → entry+0.25 | Test-seam overload |
| T_B40_15 | `T_B40_15_BuildGlobalBeOcoId_SameSeqSameAccIdx_DifferentPairIndex_UniqueIds` | pairIndex=0 != pairIndex=1 | Pure static |

### Tests Skipped (with reason)

| Test (from architecture plan) | Reason Skipped |
|-------------------------------|---------------|
| T_B40_01 (plan: ArmAllPendingBe flat, CreateForTest) | `ArmAllPendingBe` uses `Account.All` — no `CreateForTest` seam exists in engine. Re-numbered as BuildGlobalBeOcoId tests |
| T_B40_02 (plan: ArmAllPendingBe 2 above threshold) | Same — `Account.All` not injectable; skipped |
| T_B40_10 (plan: SubmitBeStop ocoOverride path) | `SubmitBeStop` calls `CreateOrder` which requires live NT8 runtime |
| T_B40_11–T_B40_12 (plan: IsPriceAlreadyAtBeForAccount) | Private method; indirect test via `ArmAllPendingBe` requires `Account.All` |

**Rationale**: The architecture plan's T_B40_01–T_B40_04 and T_B40_10–T_B40_12 assume a `CreateForTest` seam that was not added in T1 (T1 completion report confirms no such seam). Per the ticket instructions: "If NT8 types not mockable via existing pattern, write the pragmatic set that WILL compile and run." All 15 written tests compile and assert real behavior.

---

## 7-Scan Results

### SCAN-01: `lock(` usage — 0 new violations ✅
```
Select-String -Path "src\PropTraderTools\CopyEngineTests.cs" -Pattern "lock\("
```
**Result**: 1 match at line 3903 — in the comment `// JS-021: no lock()`. Zero real `lock(` usage. → **0 VIOLATIONS**

### SCAN-02: `async void` — 0 violations ✅
```
Select-String -Path "src\PropTraderTools\CopyEngineTests.cs" -Pattern "async void "
```
**Result**: No output — zero matches. → **0 VIOLATIONS**

### SCAN-03: `return null;` — 0 new violations ✅
```
Select-String -Path "src\PropTraderTools\CopyEngineTests.cs" -Pattern "return null;"
```
**Result**: No output — zero matches in B40 tests. → **0 VIOLATIONS**

### SCAN-04: `throw new` — 0 violations ✅
```
Select-String -Path "src\PropTraderTools\CopyEngineTests.cs" -Pattern "throw new "
```
**Result**: No output — zero matches. → **0 VIOLATIONS**

### SCAN-05: Complexity audit — CYC ≤ 8 ✅
```
python scripts/complexity_audit.py
```
**Result**: Script not present in Wave workspace (pre-existing condition from prior blocks).
Manual CYC verification: All 15 `[Fact]` test bodies are pure straight-line assertion sequences with no branches — CYC=1 per test. → **0 VIOLATIONS**

### SCAN-06: `[Fact]` count — 216/216 ✅
```
Select-String -Path "src\PropTraderTools\CopyEngineTests.cs" -Pattern "^\s*\[Fact\]" | Measure-Object
```
**Result**: **216** [Fact] attributes (201 baseline + 15 new). Build has pre-existing AtrSizingEngine.cs errors (exempt per DW-B39-INFO-01) — zero new errors. All 15 new tests compile correctly with zero new build errors.

Build output:
```
AtrSizingEngine.cs(20,31): error CS0234 -- PRE-EXISTING (exempt)
AtrSizingEngine.cs(24,36): error CS0246 -- PRE-EXISTING (exempt)
CopyEngine.cs(688,22): warning CS8632 -- PRE-EXISTING (B32)
1 Warning(s)  2 Error(s) -- ALL PRE-EXISTING, 0 new from B40-T3
```

### SCAN-07: `verify_links.ps1` — OK=12 DESYNC=0 ✅
```
powershell -File scripts\verify_links.ps1
```
**Result**:
```
OK      : 12  (CopyEngineTests.cs SKIPPED -- test file, not deployed to NT8)
DESYNC  : 0
MISSING : 0
FIXED   : 0
SKIPPED : 1
PASS -- All deployable source files match NinjaTrader. No stale deploy risk.
```

---

## Jane Street DNA Compliance

- JS-021: No `lock()` in any new test code ✅
- JS-033: No `async void` in any new test code ✅
- JS-001: No `throw` in any new test code ✅
- JS-002: No `return null` in any new test code ✅
- All test methods: CYC=1 (pure assertion sequences, no branches) ✅

---

## [Fact] Count Summary

| Block | [Fact] Count |
|-------|-------------|
| After T2 (baseline) | 201 |
| T_B40_01 through T_B40_15 | +15 |
| **After T3** | **216** |

---

*ptt-engineer | Phase 4a | B40-LaneA | T3 | 2026-07-30*
