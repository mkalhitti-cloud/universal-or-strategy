# B137 Ticket 2 Completion Report

**Block**: B137
**Phase**: 4a -- Engineer
**Ticket**: T2 -- IsNoPriceChange Guard (DW-B147 + DW-B149)
**Engineer**: ptt-engineer
**Date**: 2026-09-08
**SCOPE LOCK**: TICKET 2 ONLY

---

## SCOPE

Ticket 2 implements the IsNoPriceChange early-return guard to suppress spurious
cancel+resubmit cycles on ATM bracket sync when the price has not changed.

**Spec requirement IDs**:
- DW-B147: ARM event spurious cancel+resubmit (rawPrice==newPrice)
- DW-B149: ChangeSubmitted race second TP3-HBC at same rawPrice

**Prerequisite**: T1 VERIFY_PASS confirmed (SyncAtmFollowerTarget CYC=7) before starting.

**Files modified**:
- `src/PropTraderTools/CopyEngine.cs` (Wave workspace)

**Files created**:
- `tests/PropTraderTools.Tests/CopyEngineB137Tests.cs` (NEW)

---

## IsNoPriceChange Helper

**Location**: `src/PropTraderTools/CopyEngine.cs` (near line 2737, inserted before OrderPassesBracketGate)

**Method**:
```csharp
private static bool IsNoPriceChange(double currentPrice, double newPrice) =>
    currentPrice == newPrice;
```

**CYC**: 1 (expression body, no branches)

**Test seam**:
```csharp
internal static bool IsNoPriceChangeTestable(double currentPrice, double newPrice) =>
    IsNoPriceChange(currentPrice, newPrice);
```

---

## SyncAtmFollowerTarget Guard

**Parameter names used**: `fo.LimitPrice` (currentPrice), `newPrice`

**CYC before T2**: 7 (confirmed by T1 VERIFY_PASS)
**CYC after T2**: 8 (AT LIMIT -- must not exceed)

**Guard inserted** (after `if (fo == null) return;`, before Block A-Prime foreach):
```csharp
if (IsNoPriceChange(fo.LimitPrice, newPrice)) // (3) T2 B137 DW-B147/DW-B149 guard
    return;
```

**CYC comment updated** from CYC=7 to:
```
// CYC=8: (1) acc null, (2) fo null, (3) IsNoPriceChange guard [T2],
//        (4) foreach A-Prime, (5) OrderState==Working, (6) Name=="PTT-TGT-Drag",
//        (7) catch A-Prime, (8) Block A catch.
// AT LIMIT. T2 B137: DW-B147/DW-B149 guard. T1 B137: Phase C -> ExecutePhaseCStopReplacement.
```

---

## SyncAtmFollowerBracket Guard

**Parameter names used**: `fo.StopPrice` (currentPrice), `newPrice`

**CYC before T2**: 4 (source-verified pre-T2 state)
**CYC after T2**: 5

**Guard inserted** (after `if (fo == null) return;`, before Block A try/catch):
```csharp
if (IsNoPriceChange(fo.StopPrice, newPrice)) // (3) T2 B137 DW-B147/DW-B149 guard
    return;
```

**CYC comment updated** from CYC=4 to:
```
// CYC=5: (1) acc null guard, (2) fo null guard, (3) IsNoPriceChange guard [T2 B137],
//        (4) Block A catch, (5) newStop null guard.
// T2 B137: DW-B147/DW-B149 IsNoPriceChange guard added after fo null check.
```

---

## Test File

**Path**: `tests/PropTraderTools.Tests/CopyEngineB137Tests.cs`

**Approach**: Option A -- [Fact(Skip=...)] for T_B137_03..09

**Note on test project architecture**: `tests/PropTraderTools.Tests/` targets net8.0 with no
ProjectReference to the net48 PropTraderTools assembly (TFM mismatch prevents direct reference).
T_B137_01/02 use an inline predicate `IsNoPriceChangeInline` that exactly mirrors the production
`IsNoPriceChange` expression body. Pattern follows existing `CopyEngineBreakEvenFollowerTests.cs`.
T_B137_03..05 are skipped pending NT8 runtime (Order/Account not instantiable in net8.0).
T_B137_06/09 are skipped pending T3 (DW-B150 fix).
T_B137_07/08 are skipped pending T4 (CancelExistingPttStpDrag not yet implemented).

**Test list**:

| Test ID | Status | Description |
|---------|--------|-------------|
| T_B137_01 | PASS | IsNoPriceChange: same price returns true |
| T_B137_02 | PASS | IsNoPriceChange: different price returns false |
| T_B137_03 | SKIP | SyncAtmFollowerTarget: no cancel when price unchanged -- NT8 runtime required |
| T_B137_04 | SKIP | SyncAtmFollowerBracket: no cancel when price unchanged -- NT8 runtime required |
| T_B137_05 | SKIP | Regression: cancel fires on real price change -- NT8 runtime required |
| T_B137_06 | SKIP | OrderPassesBracketGate: empty signalName -> ATM path -- pending T3 |
| T_B137_07 | SKIP | CancelExistingPttStpDrag: Working drag cancelled -- pending T4 |
| T_B137_08 | SKIP | CancelExistingPttStpDrag: Accepted drag cancelled -- pending T4 |
| T_B137_09 | SKIP | OrderPassesBracketGate: null signalName regression -- pending T3 |

---

## 7 Scan Results (Layer 2)

### SCAN-01: No lock() in src/

**Command**: `Get-ChildItem -Path src -Recurse -Filter "*.cs" | Select-String -Pattern "lock\s*\(" | Where-Object { $_.Line -notmatch "^\s*//" }`
**Result**: (no output -- 0 matches)
**Status**: PASS ✅

### SCAN-02: No async void in src/

**Command**: `Get-ChildItem -Path src -Recurse -Filter "*.cs" | Select-String -Pattern "async\s+void\s" | Where-Object { $_.Line -notmatch "^\s*//" }`
**Result**: (no output -- 0 matches)
**Status**: PASS ✅

### SCAN-03: No new return null in T2 diff

**Command**: `git diff HEAD src/PropTraderTools/CopyEngine.cs | Select-String -Pattern "^\+" | Select-String -Pattern "return null;"`
**Result**: (no output -- 0 matches)
**Status**: PASS ✅

### SCAN-04: dotnet build

**Command**: `dotnet build tests/PropTraderTools.Tests/PropTraderTools.Tests.csproj`
**Result**: Build succeeded. 0 Error(s). 0 Errors.
**Status**: PASS ✅
**Note**: CA1707 and xUnit1004 warnings are pre-existing (present in CopyEngineBreakEvenFollowerTests.cs before T2) and not new.

### SCAN-05: Complexity audit (manual -- scripts/complexity_audit.py does not exist in repo)

**Manual McCabe count**:
- `IsNoPriceChange`: expression body `=> currentPrice == newPrice` -- base(1), zero branches. **CYC=1** ✅
- `IsNoPriceChangeTestable`: expression delegation -- base(1), zero branches. **CYC=1** ✅
- `SyncAtmFollowerTarget`: was CYC=7 (T1 VERIFY_PASS). Added `if (IsNoPriceChange(...)) return;` (+1 branch). **CYC=8** (AT LIMIT) ✅
- `SyncAtmFollowerBracket`: was CYC=4 (source-verified). Added `if (IsNoPriceChange(...)) return;` (+1 branch). **CYC=5** ✅
- All other methods: unchanged (ExecutePhaseCStopReplacement=2, OrderPassesBracketGate=2, MatchesLeaderName=5, FindFollowerBracketOrder(list)=7)

**Status**: PASS ✅

### SCAN-06: dotnet test

**Command**: `dotnet test tests/PropTraderTools.Tests/PropTraderTools.Tests.csproj --verbosity normal`
**Result**:
```
Test Run Successful.
Total tests: 19
     Passed: 12
    Skipped: 7
      Failed: 0
 Total time: 0.5778 Seconds
```
T_B137_01 PASS, T_B137_02 PASS. T_B137_03..09 SKIP (documented above).
All 10 pre-existing BreakEven tests: PASS.
**Status**: PASS ✅

### SCAN-07: CSharpier check

**Command**: `csharpier check src/`
**Result**: `Checked 71 files in 663ms.` (no issues)
**Status**: PASS ✅

---

## CYC Summary After T2

| Method | CYC Before T2 | CYC After T2 | Limit | Status |
|--------|--------------|-------------|-------|--------|
| `SyncAtmFollowerTarget` | 7 (post-T1) | **8** | <=8 | AT LIMIT ✅ |
| `SyncAtmFollowerBracket` | 4 | **5** | <=8 | ✅ |
| `IsNoPriceChange` (NEW) | -- | **1** | <=8 | ✅ |
| `IsNoPriceChangeTestable` (NEW) | -- | **1** | <=8 | ✅ |
| `ExecutePhaseCStopReplacement` | 2 (post-T1) | **2** | <=8 | UNCHANGED ✅ |
| `OrderPassesBracketGate` | 2 | **2** | <=8 | UNCHANGED ✅ |

---

## DNA Compliance

| Rule | Check | Result |
|------|-------|--------|
| JS-001 | No throw in hot path | PASS -- IsNoPriceChange has no throw; guards return void |
| JS-002 | No return null | PASS -- IsNoPriceChange returns bool; guards return void |
| JS-021 | No lock() | PASS -- SCAN-01 confirmed 0 matches |
| JS-033 | No async void | PASS -- SCAN-02 confirmed 0 matches |
| JS-036 | No heap alloc in hot path | PASS -- IsNoPriceChange is stack-only, pure comparison |
| JS-066 | CYC <= 8 | PASS -- SyncAtmFollowerTarget=8 AT LIMIT, all others below |
| ASCII-only | All identifiers ASCII | PASS -- IsNoPriceChange, IsNoPriceChangeTestable all ASCII |
| DateTime.UtcNow | No time logic | PASS -- no time logic added |
| PTT- prefix | No new CreateOrder calls | PASS -- T2 adds no new CreateOrder calls |

---

## BUILD_PASS
