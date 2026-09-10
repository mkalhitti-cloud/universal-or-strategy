# PTT-REPAIRS-07-OBFUSCATION -- Implementation Tickets
# Phase: 3  Status: TICKETS_COMPLETE
# Author: ptt-architect  Date: 2026-08-10
# Source plan: 02-architecture-plan.md (REVIEW_PASS per 02-plan-review.md)

---

## Global Constraints (apply to ALL tickets)

- Touch ONLY: `src/PropTraderTools/CopyEngineTests.cs`
- No production code changes. No new helpers. No new fields. No new throw statements.
- No `lock()` anywhere.
- ASCII-only skip string (JS-042):
  `"obfuscation: AgileDotNetRT renames private members; cannot locate by string name"`
- Sequential execution required: T1 -> T2 -> T3 -> T4 (all edits target the same file).
- Run `powershell -File .\deploy-sync.ps1` after EACH ticket's implementation.

---

## Baseline / Target

```
Baseline: Total=501, Passed=19, Failed=450, Skipped=32
After T1:  Passed=19, Failed=391, Skipped=91
After T2:  Passed=19, Failed=356, Skipped=126
After T3:  Passed=19, Failed=331, Skipped=151
After T4:  Passed=19, Failed=307, Skipped=175
```

NOTE FOR VERIFIER: Run `dotnet test` fresh at session start to record the ACTUAL
baseline at that moment (the other lane may have already changed counts). Do not
treat the numbers above as immutable ground truth.

---

## Implementation Pattern (same for all tickets)

```csharp
// BEFORE:
[Fact]
public void SomeTestMethod_ShouldDoSomething()

// AFTER:
[Fact(Skip = "obfuscation: AgileDotNetRT renames private members; cannot locate by string name")]
public void SomeTestMethod_ShouldDoSomething()
```

No test BODY changes. Only the `[Fact]` attribute declaration is modified.

---

## Ticket 1 -- B79CancelRaceGuardTests

### Spec Requirement IDs

- PTT-REPAIRS-07-OBFUSCATION: Fix 59 Assert.NotNull failures in B79CancelRaceGuardTests
  caused by AgileDotNetRT obfuscating CopyEngine private/internal method and field names.

### Problem Statement

Every `[Fact]` test in `B79CancelRaceGuardTests` calls `GetMethod(string name)`,
`GetField(string name)`, or `type.GetMethod(string name, BindingFlags, ...)` on
`CopyEngine` or `PttBreakEven`. At runtime under AgileDotNetRT, private/internal member
names are renamed. The `GetMethod`/`GetField` calls return `null`, causing `Assert.NotNull`
to fail. This class has zero `TypeInitializationException` failures -- all failures are
pure obfuscation failures.

### Implementation

Strategy: Option B -- apply
`[Fact(Skip = "obfuscation: AgileDotNetRT renames private members; cannot locate by string name")]`
to every `[Fact]` test method in `B79CancelRaceGuardTests`.

**Pre-implementation step (mandatory):**
```powershell
dotnet test --filter "FullyQualifiedName~B79CancelRaceGuardTests" --no-build 2>&1
```
Confirm that ALL failures are `Assert.NotNull() Failure` (not `TypeInitializationException`).
If any test PASSES, do not add Skip to it. If any test shows `TypeInitializationException`,
do not add Skip to it (Lane B scope -- report to orchestrator instead).

### Method Signatures -- [Fact] names to apply Skip to

The following are all [Fact] methods in `B79CancelRaceGuardTests`
(file: `src/PropTraderTools/CopyEngineTests.cs`, class starts at line 5823).
Apply `Skip=` to every method confirmed as `Assert.NotNull() Failure` by the pre-run:

```
T_DW_B79_09_01_CancelQxBrackets2Param_HasRemoveAllGuard           (line 5850)
T_DW_B79_09_02_CancelQxBrackets3Param_HasRemoveAllGuard           (line 5882)
T_DW_B79_09_03_CancelStaleBracketsLocal_HasRemoveAllGuard         (line 5919)
B132_LaneB_DiagnosticMode_FieldExists                             (line 5946)
TryFireImmediateBeIfAlreadyAtLevel_ShouldReturnFalse_WhenTickSizeIsZero    (line 5963)
TryFireImmediateBeIfAlreadyAtLevel_ShouldReturnFalse_WhenPriceIsZero       (line 5971)
TryFireImmediateBeIfAlreadyAtLevel_ShouldReturnTrue_WhenLongAndBidAboveTarget   (line 5979)
TryFireImmediateBeIfAlreadyAtLevel_ShouldReturnTrue_WhenShortAndAskBelowTarget  (line 5987)
IsPendingBeTriggerMet_ShouldReturnFalse_WhenRefPriceIsZero                 (line 5997)
IsPendingBeTriggerMet_ShouldReturnFalse_WhenLongPositionPriceBelowTarget   (line 6004)
IsPendingBeTriggerMet_ShouldReturnTrue_WhenLongAndBidReachesTarget         (line 6012)
IsPendingBeTriggerMet_ShouldReturnTrue_WhenShortAndAskReachesTarget        (line 6020)
IsEligibleBeTargetOrder_ShouldReturnFalse_WhenOrderStateIsNotInSnapshot    (line 6031)
IsEligibleBeTargetOrder_ShouldReturnFalse_WhenInstrumentDoesNotMatch       (line 6037)
IsEligibleBeTargetOrder_ShouldReturnFalse_WhenOrderTypeIsNotLimit          (line 6044)
IsNativeAtmTargetOrder_ShouldReturnTrue_WhenNameIsTarget1                  (line 6053)
IsNativeAtmTargetOrder_ShouldReturnFalse_WhenNameIsTarget0                 (line 6060)
IsPttBeOrQxTargetOrder_ShouldReturnTrue_WhenNameStartsWithPttQxT1         (line 6069)
IsPttBeOrQxTargetOrder_ShouldReturnTrue_WhenNameStartsWithPttBeTarget      (line 6076)
LogDiagOrderCount_ShouldLogCorrectCount_WhenOrdersExistForInstrument       (line 6085)
RegisterBeRetryIfNoTargets_ShouldNotRegister_WhenIsRetryIsTrue             (line 6094)
RegisterBeRetryIfNoTargets_ShouldNotRegister_WhenPositionIsFlat            (line 6101)
RegisterBeRetryIfNoTargets_ShouldRegisterSlotAndQueueFallback_WhenConditionsMet (line 6108)
RegisterPartialTargetBeRetry_ShouldNotRegister_WhenTargetCountEqualsLeaderCount   (line 6117)
RegisterPartialTargetBeRetry_ShouldRegisterSlot_WhenFollowerHasFewerTargetsThanLeader (line 6124)
CancelExistingStpDragOrders_ShouldCancelMatchingLiveStpDragOrder           (line 6133)
CancelExistingTgtDragOrders_ShouldCancelMatchingLiveTgtDragOrder           (line 6142)
SubmitReplacementStopLeg_ShouldReturnEarly_WhenCreateOrderReturnsNull      (line 6151)
SubmitReplacementStopLeg_ShouldUseLeaderQuantity_WhenLeaderLegProvided     (line 6158)
SubmitReplacementTargetLeg_ShouldReturnEarly_WhenCreateOrderReturnsNull    (line 6167)
SubmitReplacementTargetLeg_ShouldUseLeaderQuantity_WhenLeaderLegProvided   (line 6174)
IsReArmedAtmBracketCleanupRequired_ShouldReturnFalse_WhenOrderStateIsNotWorkingOrAccepted (line 6184)
IsReArmedAtmBracketCleanupRequired_ShouldReturnFalse_WhenNameDoesNotStartWithPttQxT       (line 6194)
IsReArmedAtmBracketCleanupRequired_ShouldReturnFalse_WhenTtlHasExpired     (line 6204)
IsReArmedAtmBracketCleanupRequired_ShouldReturnTrue_WhenAllConditionsMet   (line 6214)
FindMatchingNativeAtmBracket_ShouldReturnNull_WhenNoMatchingOrderExists    (line 6225)
FindMatchingNativeAtmBracket_ShouldReturnOrder_WhenNameAndInstrumentMatch  (line 6232)
TryFindRuleAndFollowerIndex_ShouldReturnFalse_WhenInstrumentDoesNotMatch   (line 6241)
TryFindRuleAndFollowerIndex_ShouldReturnTrue_WhenFollowerAccountMatches    (line 6248)
TryFindRuleAndFollowerIndex_ShouldSetFollowerIndex_WhenMatchFound          (line 6255)
HasActiveQxOrdersForInstrument_ShouldReturnTrue_WhenPttQxOrderIsWorking    (line 6264)
HasActiveQxOrdersForInstrument_ShouldReturnFalse_WhenNoQxOrdersExist      (line 6271)
HasActiveQxOrdersForInstrument_ShouldReturnFalse_WhenQxOrderIsFilledNotWorking  (line 6278)
SyncAtmFollowerStopBracket_ShouldReturn_WhenStopPriceIsZero                (line 6287)
SyncAtmFollowerStopBracket_ShouldCallResubmitTarget_WhenCapturedPriceHasValue   (line 6294)
CancelStaleTgtDragOrders_ShouldCancelMatchingWorkingOrder                  (line 6303)
CancelStaleTgtDragOrders_ShouldSkipNonMatchingOrders                       (line 6311)
CreateAndSubmitReplacementTarget_ShouldReturnNull_WhenCreateOrderFails     (line 6319)
CreateAndSubmitReplacementTarget_ShouldUseLeaderQuantity_WhenLeaderOrderIsNotNull (line 6326)
HasInFlightFlattenOrder_ShouldReturnTrue_WhenPttFlattenOrderIsWorking      (line 6335)
HasInFlightFlattenOrder_ShouldReturnFalse_WhenNoFlattenOrderExists         (line 6343)
IsPositionFlatOrMissing_ShouldReturnTrue_WhenPositionIsNull                (line 6351)
IsPositionFlatOrMissing_ShouldReturnTrue_WhenPositionQuantityIsZero        (line 6361)
IsLeaderTargetOrder_ShouldReturnTrue_WhenOrderIsWorkingLimitWithValidTargetName  (line 6373)
IsLeaderTargetOrder_ShouldReturnFalse_WhenOrderStateIsNotWorking           (line 6380)
IsLeaderTargetOrder_ShouldReturnFalse_WhenNameDoesNotStartWithTarget       (line 6387)
IsLeaderTargetOrder_ShouldReturnFalse_WhenSixthCharIsNotDigit              (line 6394)
ResubmitFollowerEntry_ShouldSkip_WhenPriceChangeIsWithinTickSize           (line 6403)
ResubmitFollowerEntry_ShouldUseStopPrice_WhenOrderTypeIsStopLimit          (line 6410)
ResubmitFollowerEntry_ShouldPreloadDedupCache_WhenOrderIsCreated           (line 6417)
IsLeaderAccountForInstrument_ShouldReturnTrue_WhenAccountMatchesMasterAccount   (line 6426)
IsLeaderAccountForInstrument_ShouldReturnFalse_WhenAccountIsFollower       (line 6433)
CancelStaleCascadeTgtDrag_ShouldCancelMatchingWorkingOrder                 (line 6443)
CancelStaleCascadeTgtDrag_ShouldSkipNonWorkingOrders                       (line 6450)
```

Total enumerated: 64 [Fact] methods. The plan estimates ~59 will fail with
`Assert.NotNull`. The pre-run confirms the exact set. Skip all that fail with
`Assert.NotNull`. Skip none that pass or fail with a different exception.

### File to Modify

`src/PropTraderTools/CopyEngineTests.cs`
Full path: `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs`
Class body: lines 5823-6455

### 7-Scan Checklist

```
SCAN-1: grep -c "lock(" src/PropTraderTools/CopyEngineTests.cs -> 0
SCAN-2: grep -P "[^\x00-\x7F]" src/PropTraderTools/CopyEngineTests.cs -> 0 matches
SCAN-3: dotnet build 2>&1 | Select-String " error CS" | Measure-Object -Line -> 0
SCAN-4: dotnet build 2>&1 | Select-String "Error\(s\)" -> 0 Error(s)
SCAN-5: dotnet test --filter "FullyQualifiedName~B79CancelRaceGuardTests" --no-build 2>&1
         -> All previously-failing tests now show as Skipped
         -> passed count unchanged vs pre-T1 baseline
         -> failed count reduced by ~59 vs pre-T1 baseline
         -> Globally: Passed=19, Failed<=391, Skipped>=91
SCAN-6: powershell -File .\deploy-sync.ps1 -> SYNC COMPLETE (or equivalent success message)
SCAN-7: (Get-Item src/PropTraderTools/CopyEngineTests.cs).LinkType -eq "HardLink"
         and (Get-Item src/PropTraderTools/CopyEngineTests.cs).LinkCount -eq 1
         (or >= 2 if hard-linked to NinjaTrader AddOns directory -- SYNC COMPLETE implies valid)
```

### Acceptance Criteria

- All Assert.NotNull failures in `B79CancelRaceGuardTests` are now Skipped, not Failed.
- Zero tests that previously passed are now Failing (no regressions).
- Passed count remains 19 (unchanged).
- The exact ASCII skip string is used: `obfuscation: AgileDotNetRT renames private members; cannot locate by string name`
- No `lock()` added anywhere.
- No production code touched.
- `deploy-sync.ps1` executed and completed with success.

---

## Ticket 2 -- BwaveCycTaR3HelperTests

### Spec Requirement IDs

- PTT-REPAIRS-07-OBFUSCATION: Fix 35 Assert.NotNull failures in BwaveCycTaR3HelperTests
  caused by AgileDotNetRT obfuscating CopyEngine private method names.

### Problem Statement

Every `[Fact]` test in `BwaveCycTaR3HelperTests` calls `GetMethod(string name)` or
`typeof(CopyEngine).GetMethod(string name, BindingFlags)` on CopyEngine. Under
AgileDotNetRT, private method names are renamed at runtime, returning null and
causing `Assert.NotNull` to fail. This class has zero `TypeInitializationException`
failures -- all failures are pure obfuscation failures.

### Implementation

Strategy: Option B -- apply
`[Fact(Skip = "obfuscation: AgileDotNetRT renames private members; cannot locate by string name")]`
to every `[Fact]` test method in `BwaveCycTaR3HelperTests`.

**Pre-implementation step (mandatory):**
```powershell
dotnet test --filter "FullyQualifiedName~BwaveCycTaR3HelperTests" --no-build 2>&1
```
Confirm all failures are `Assert.NotNull() Failure`. Apply T1 first; T2 is applied after
T1's `deploy-sync.ps1` has completed.

### Method Signatures -- [Fact] names to apply Skip to

All [Fact] methods in `BwaveCycTaR3HelperTests`
(file: `src/PropTraderTools/CopyEngineTests.cs`, class starts at line 6812):

```
TrySyncAtmBrackets_ShouldExist_AsPrivateHelper                            (line 6820)
TrySkipTrailingStop_ShouldExist_AsPrivateHelper                           (line 6832)
SyncStandardBracket_ShouldExist_AsPrivateHelper                           (line 6840)
IsPttTgtDragOrder_ShouldExist_AsPrivateHelper                             (line 6849)
IsAtmTgtOrder_ShouldExist_AsPrivateHelper                                 (line 6858)
SyncAtmFollowerStopBracket_ShouldReturn_WhenStopPriceIsZero               (line 6867)
SyncAtmFollowerStopBracket_ShouldCallResubmitTarget_WhenCapturedPriceHasValue  (line 6874)
IsBePendingTargetOrder_ShouldReturnTrue_WhenOrderNameIsPttQxT1            (line 6882)
IsBePendingTargetOrder_ShouldReturnTrue_WhenOrderNameIsTarget1            (line 6889)
IsBePendingTargetOrder_ShouldReturnFalse_WhenOrderNameIsUnrelated         (line 6896)
IsPttBeStopRejected_ShouldReturnTrue_WhenOrderIsRejectedPttBeStop         (line 6904)
IsPttBeStopRejected_ShouldReturnFalse_WhenOrderNameIsNotPttBeStop         (line 6911)
IsPttBeStopRejected_ShouldReturnFalse_WhenOrderStateIsFilledNotRejected   (line 6918)
LogBeSlotEviction_ShouldExist_AsPrivateVoidMethod                         (line 6927)
LogBeSlotEviction_ShouldAccept_AccNameAndIsRejectedParameters             (line 6934)
IsPttDragOrderCancellable_ShouldReturnTrue_WhenWorkingPttTgtDragMatchesInstrument  (line 6942)
IsPttDragOrderCancellable_ShouldReturnTrue_WhenWorkingPttStpDragMatchesInstrument  (line 6949)
IsPttDragOrderCancellable_ShouldReturnFalse_WhenOrderStateIsNotWorking    (line 6957)
IsPttDragOrderCancellable_ShouldReturnFalse_WhenOrderNameIsUnknown        (line 6964)
IsPttQxTargetOrder_ShouldExist_AsPrivateHelper                            (line 6972)
IsNativeAtmBeRetryTarget_ShouldExist_AsPrivateHelper                      (line 6979)
IsBeRetryEligibleOrderState_ShouldExist_AsPrivateHelper                   (line 6986)
IsBeRetryOrderInvalid_ShouldExist_AsPrivateHelper                         (line 6993)
IsBeSlotNonTerminal_ShouldExist_AsPrivateHelper                           (line 7000)
IsBeFilledWithOpenPosition_ShouldExist_AsPrivateHelper                    (line 7007)
IsPttDragOrderName_ShouldExist_AsPrivateHelper                            (line 7014)
IsDragInstrumentMatch_ShouldExist_AsPrivateHelper                         (line 7021)
IsQxTOrderStateValid_ShouldExist_AsPrivateHelper                          (line 7030)
IsQxTBracketNameValid_ShouldExist_AsPrivateHelper                         (line 7039)
TryGetCleanupEntryForFollower_ShouldExist_AsPrivateHelper                 (line 7048)
IsCleanupEntryCurrentAndMatching_ShouldExist_AsPrivateHelper              (line 7060)
SendAtmCancelReplace_ShouldExist_AsPrivateHelper                          (line 7072)
TryMatchFollowerInRule_ShouldExist_AsPrivateHelper                        (line 7081)
IsBeReplaceTargetValid_ShouldReturnFalse_WhenOrderIsNull                  (line 7090)
TryIncrementBeReplaceAttempt_ShouldExist_AsPrivateHelper                  (line 7099)
```

Total: 35 [Fact] methods. Apply Skip to all confirmed as `Assert.NotNull() Failure`.

### File to Modify

`src/PropTraderTools/CopyEngineTests.cs`
Full path: `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs`
Class body: lines 6812-7105

### 7-Scan Checklist

```
SCAN-1: grep -c "lock(" src/PropTraderTools/CopyEngineTests.cs -> 0
SCAN-2: grep -P "[^\x00-\x7F]" src/PropTraderTools/CopyEngineTests.cs -> 0 matches
SCAN-3: dotnet build 2>&1 | Select-String " error CS" | Measure-Object -Line -> 0
SCAN-4: dotnet build 2>&1 | Select-String "Error\(s\)" -> 0 Error(s)
SCAN-5: dotnet test --filter "FullyQualifiedName~BwaveCycTaR3HelperTests" --no-build 2>&1
         -> All previously-failing tests now show as Skipped
         -> passed count unchanged vs pre-T2 baseline
         -> failed count reduced by ~35 vs pre-T2 baseline
         -> Globally: Passed=19, Failed<=356, Skipped>=126 (cumulative after T1+T2)
SCAN-6: powershell -File .\deploy-sync.ps1 -> SYNC COMPLETE (or equivalent success message)
SCAN-7: (Get-Item src/PropTraderTools/CopyEngineTests.cs).LinkType -eq "HardLink"
         and link count valid (SYNC COMPLETE implies valid)
```

### Acceptance Criteria

- All Assert.NotNull failures in `BwaveCycTaR3HelperTests` are now Skipped, not Failed.
- Zero tests that previously passed are now Failing (no regressions).
- Passed count remains 19 (unchanged).
- The exact ASCII skip string is used.
- No `lock()` added anywhere.
- No production code touched.
- `deploy-sync.ps1` executed and completed with success.

---

## Ticket 3 -- BwaveCycT1R1BeHelperTests

### Spec Requirement IDs

- PTT-REPAIRS-07-OBFUSCATION: Fix 25 Assert.NotNull failures in BwaveCycT1R1BeHelperTests
  caused by AgileDotNetRT obfuscating CopyEngine private method names.

### Problem Statement

Every `[Fact]` test in `BwaveCycT1R1BeHelperTests` calls `GetMethod(string name)` on
CopyEngine. Under AgileDotNetRT, private method names are renamed at runtime, returning
null and causing `Assert.NotNull` to fail. This class has zero `TypeInitializationException`
failures -- all failures are pure obfuscation failures.

### Implementation

Strategy: Option B -- apply
`[Fact(Skip = "obfuscation: AgileDotNetRT renames private members; cannot locate by string name")]`
to every `[Fact]` test method in `BwaveCycT1R1BeHelperTests`.

**Pre-implementation step (mandatory):**
```powershell
dotnet test --filter "FullyQualifiedName~BwaveCycT1R1BeHelperTests" --no-build 2>&1
```
Confirm all failures are `Assert.NotNull() Failure`. Apply T1 and T2 first; T3 is applied
after T2's `deploy-sync.ps1` has completed.

### Method Signatures -- [Fact] names to apply Skip to

All [Fact] methods in `BwaveCycT1R1BeHelperTests`
(file: `src/PropTraderTools/CopyEngineTests.cs`, class starts at line 6469):

```
GetMarketBidPrice_ShouldExist_AsPrivateHelper                             (line 6477)
GetMarketAskPrice_ShouldExist_AsPrivateHelper                             (line 6483)
GetBeTickSize_ShouldExist_AsPrivateHelper                                 (line 6490)
SelectBeRefPriceByDirection_ShouldReturnBid_WhenLongAndBidIsPositive      (line 6499)
SelectBeRefPriceByDirection_ShouldReturnAsk_WhenLongAndBidIsZero          (line 6509)
SelectBeRefPriceByDirection_ShouldReturnAsk_WhenShortAndAskIsPositive     (line 6519)
SelectBeRefPriceByDirection_ShouldReturnBid_WhenShortAndAskIsZero         (line 6529)
FireBeAndNotifyEvent_ShouldExist_AsPrivateHelper                          (line 6541)
ShouldFireBeImmediately_ShouldExist_AsPrivateHelper                       (line 6548)
CompleteBeArming_ShouldExist_AsPrivateHelper                              (line 6555)
GetSenderAccountName_ShouldReturnEmpty_WhenSenderIsNull                   (line 6564)
GetSenderAccountName_ShouldReturnEmpty_WhenSenderIsNotAccount             (line 6573)
TryClaimPendingBeSlot_ShouldExist_AsPrivateHelper                         (line 6584)
GetSlotInstrumentName_ShouldExist_AsPrivateHelper                         (line 6591)
GetSlotAccountName_ShouldExist_AsPrivateHelper                            (line 6598)
RaisePendingBeFiredEvent_ShouldExist_AsPrivateHelper                      (line 6605)
SettleAndFirePendingBe_ShouldExist_AsPrivateHelper                        (line 6612)
TryFireImmediateBeIfAlreadyAtLevel_ShouldReturnFalse_WhenTickSizeIsZero   (line 6622)
TryFireImmediateBeIfAlreadyAtLevel_ShouldReturnFalse_WhenPriceIsZero      (line 6629)
TryFireImmediateBeIfAlreadyAtLevel_ShouldReturnTrue_WhenLongAndBidAboveTarget    (line 6636)
TryFireImmediateBeIfAlreadyAtLevel_ShouldReturnTrue_WhenShortAndAskBelowTarget   (line 6643)
IsPendingBeTriggerMet_ShouldReturnFalse_WhenRefPriceIsZero                (line 6651)
IsPendingBeTriggerMet_ShouldReturnFalse_WhenLongPositionPriceBelowTarget  (line 6658)
IsPendingBeTriggerMet_ShouldReturnTrue_WhenLongAndBidReachesTarget        (line 6665)
IsPendingBeTriggerMet_ShouldReturnTrue_WhenShortAndAskReachesTarget       (line 6672)
```

Total: 25 [Fact] methods. Apply Skip to all confirmed as `Assert.NotNull() Failure`.

### File to Modify

`src/PropTraderTools/CopyEngineTests.cs`
Full path: `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs`
Class body: lines 6469-6678

### 7-Scan Checklist

```
SCAN-1: grep -c "lock(" src/PropTraderTools/CopyEngineTests.cs -> 0
SCAN-2: grep -P "[^\x00-\x7F]" src/PropTraderTools/CopyEngineTests.cs -> 0 matches
SCAN-3: dotnet build 2>&1 | Select-String " error CS" | Measure-Object -Line -> 0
SCAN-4: dotnet build 2>&1 | Select-String "Error\(s\)" -> 0 Error(s)
SCAN-5: dotnet test --filter "FullyQualifiedName~BwaveCycT1R1BeHelperTests" --no-build 2>&1
         -> All previously-failing tests now show as Skipped
         -> passed count unchanged vs pre-T3 baseline
         -> failed count reduced by ~25 vs pre-T3 baseline
         -> Globally: Passed=19, Failed<=331, Skipped>=151 (cumulative after T1+T2+T3)
SCAN-6: powershell -File .\deploy-sync.ps1 -> SYNC COMPLETE (or equivalent success message)
SCAN-7: (Get-Item src/PropTraderTools/CopyEngineTests.cs).LinkType -eq "HardLink"
         and link count valid (SYNC COMPLETE implies valid)
```

### Acceptance Criteria

- All Assert.NotNull failures in `BwaveCycT1R1BeHelperTests` are now Skipped, not Failed.
- Zero tests that previously passed are now Failing (no regressions).
- Passed count remains 19 (unchanged).
- The exact ASCII skip string is used.
- No `lock()` added anywhere.
- No production code touched.
- `deploy-sync.ps1` executed and completed with success.

---

## Ticket 4 -- BwaveCycTaR2HelperTests (13) + BwaveCycTaR6HelperTests (11)

### Spec Requirement IDs

- PTT-REPAIRS-07-OBFUSCATION: Fix 13 Assert.NotNull failures in BwaveCycTaR2HelperTests
  and 11 Assert.NotNull failures in BwaveCycTaR6HelperTests caused by AgileDotNetRT
  obfuscating CopyEngine private method names.
- CRITICAL: BwaveCycTaR6HelperTests also contains 6 TypeInitializationException failures
  that are Lane B scope -- DO NOT TOUCH those 6 tests.

### Problem Statement

**Part A -- BwaveCycTaR2HelperTests:**
Every `[Fact]` test calls `GetMethod(string name)` on CopyEngine. Under AgileDotNetRT,
private method names are renamed, returning null and causing `Assert.NotNull` to fail.
This class has zero `TypeInitializationException` failures.

**Part B -- BwaveCycTaR6HelperTests:**
Tests in this class use `GetStaticMethod(string name)` and `GetInstanceMethod(string name)`
on CopyEngine. The class has a mixed failure profile: 11 tests fail with
`Assert.NotNull() Failure` (obfuscation failures -- TO BE SKIPPED) and 6 tests fail with
`System.TypeInitializationException` (NT8 runtime dependency failures -- Lane B scope,
DO NOT TOUCH).

### Implementation

**MANDATORY DIAGNOSTIC STEP before ANY edits to BwaveCycTaR6HelperTests:**
```powershell
dotnet test --filter "FullyQualifiedName~BwaveCycTaR6HelperTests" --no-build 2>&1
```
For EACH failing test, inspect the exception type in the output:
- `Assert.NotNull() Failure` -> obfuscation failure -> ADD Skip attribute
- `System.TypeInitializationException` -> Lane B scope -> DO NOT ADD Skip, leave unchanged

Apply T1, T2, T3 first; T4 is applied after T3's `deploy-sync.ps1` has completed.

**Part A -- BwaveCycTaR2HelperTests:**
Apply `[Fact(Skip = "obfuscation: AgileDotNetRT renames private members; cannot locate by string name")]`
to every `[Fact]` test method in `BwaveCycTaR2HelperTests` (all 13 are obfuscation failures).

**Part B -- BwaveCycTaR6HelperTests:**
Apply Skip ONLY to the 11 tests confirmed as `Assert.NotNull() Failure` by the diagnostic run.
Leave the 6 `TypeInitializationException` tests UNCHANGED (they remain as Failed in the final count).

### Method Signatures -- [Fact] names (Part A: BwaveCycTaR2HelperTests)

All [Fact] methods in `BwaveCycTaR2HelperTests`
(file: `src/PropTraderTools/CopyEngineTests.cs`, class starts at line 6686):

```
HasValidTargetNameSuffix_ShouldExist_AsPrivateHelper                      (line 6693)
IsLeaderTargetOrder_ShouldReturnFalse_WhenOrderStateIsNotWorking           (line 6700)
IsLeaderTargetOrder_ShouldReturnFalse_WhenNameDoesNotStartWithTarget       (line 6707)
IsLeaderTargetOrder_ShouldReturnFalse_WhenSixthCharIsNotDigit              (line 6714)
IsLeaderTargetOrder_ShouldReturnTrue_WhenOrderIsWorkingLimitWithValidTargetName  (line 6721)
SelectBeTargetList_ShouldExist_AsPrivateHelper                            (line 6730)
IsBeTargetActiveState_ShouldExist_AsPrivateHelper                         (line 6739)
IsBeTargetPendingChangeState_ShouldExist_AsPrivateHelper                  (line 6748)
IsBeTargetSnapshotState_ShouldExist_AsPrivateHelper                       (line 6757)
IsEligibleBeTargetOrder_ShouldReturnFalse_WhenOrderStateIsNotInSnapshot   (line 6764)
IsEligibleBeTargetOrder_ShouldReturnFalse_WhenInstrumentDoesNotMatch      (line 6771)
IsEligibleBeTargetOrder_ShouldReturnFalse_WhenOrderTypeIsNotLimit         (line 6778)
OnTrailBeAccountUpdate_ShouldExist_AsPrivateMethod                        (line 6787)
GetSenderAccountName_ShouldBeReusedByOnTrailBeAccountUpdate               (line 6797)
```

NOTE: 14 [Fact] methods enumerated above. The plan estimates 13 will fail with
`Assert.NotNull`. The pre-run confirms the exact set. Apply Skip to each confirmed
`Assert.NotNull() Failure`. If `GetSenderAccountName_ShouldBeReusedByOnTrailBeAccountUpdate`
passes (it does not invoke CopyEngine.Instance), do NOT skip it.

### Method Signatures -- [Fact] names (Part B: BwaveCycTaR6HelperTests)

All [Fact] methods in `BwaveCycTaR6HelperTests`
(file: `src/PropTraderTools/CopyEngineTests.cs`, class starts at line 7110).
Engineer must triage ALL of these via the diagnostic run before editing:

```
IsBracketOrderLiveState_ShouldExist_AsPrivateStaticHelper                 (line 7121)
IsBracketOrderLiveState_ShouldReturnTrue_WhenOrderIsWorking               (line 7128)
ExtractLegSuffix_ShouldExist_AsPrivateStaticHelper                        (line 7138)
ExtractLegSuffix_ShouldReturnNull_WhenLeaderNameHasNoTrailingDigit        (line 7145)
ExtractLegSuffix_ShouldReturnDigit_WhenLeaderNameEndsWithDigit            (line 7155)
MatchesPttReplacementName_ShouldExist_AsPrivateStaticHelper               (line 7167)
MatchesPttReplacementName_ShouldAcceptThreeParameters                     (line 7174)
LogHbcDiag_ShouldExist_AsPrivateInstanceHelper                            (line 7184)
LogHbcDiag_ShouldAcceptFiveParameters                                     (line 7191)
ExecuteStopDragOrder_ShouldExist_AsPrivateInstanceHelper                  (line 7202)
ExecuteStopDragOrder_ShouldAcceptFiveParameters                           (line 7209)
IsPositionStateRelevant_ShouldExist_AsPrivateStaticHelper                 (line 7219)
IsPositionStateRelevant_ShouldReturnFalse_WhenStateIsWorking              (line 7226)
IsPositionStateRelevant_ShouldReturnTrue_WhenStateIsFilled                (line 7235)
IsPositionStateRelevant_ShouldReturnTrue_WhenStateIsPartFilled            (line 7244)
IsOrderEventProcessable_ShouldExist_AsPrivateStaticHelper                 (line 7255)
IsOrderEventProcessable_ShouldAcceptOneParameter                          (line 7262)
```

Total: 17 [Fact] methods. Apply Skip ONLY to the 11 confirmed as `Assert.NotNull() Failure`.
Leave the 6 `TypeInitializationException` tests unchanged (they remain Failed).

### File to Modify

`src/PropTraderTools/CopyEngineTests.cs`
Full path: `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs`
Class bodies:
- BwaveCycTaR2HelperTests: lines 6686-6805
- BwaveCycTaR6HelperTests: lines 7110-7268

### 7-Scan Checklist

```
SCAN-1: grep -c "lock(" src/PropTraderTools/CopyEngineTests.cs -> 0
SCAN-2: grep -P "[^\x00-\x7F]" src/PropTraderTools/CopyEngineTests.cs -> 0 matches
SCAN-3: dotnet build 2>&1 | Select-String " error CS" | Measure-Object -Line -> 0
SCAN-4: dotnet build 2>&1 | Select-String "Error\(s\)" -> 0 Error(s)
SCAN-5: dotnet test --no-build 2>&1
         -> Passed=19 (unchanged from baseline)
         -> Failed<=307 (reduced by ~24 from T4 changes + cumulative T1-T3 reductions)
         -> Skipped>=175 (cumulative after T1+T2+T3+T4)
         -> Total=501 (invariant)
         -> BwaveCycTaR6HelperTests MUST still show EXACTLY 6 Failed tests
            (the TypeInitializationException tests -- unchanged)
         -> Zero genuine regressions (no test moved from Passed to Failed)
SCAN-6: powershell -File .\deploy-sync.ps1 -> SYNC COMPLETE (or equivalent success message)
SCAN-7: (Get-Item src/PropTraderTools/CopyEngineTests.cs).LinkType -eq "HardLink"
         and link count valid (SYNC COMPLETE implies valid)
```

### Acceptance Criteria

- All 13 Assert.NotNull failures in `BwaveCycTaR2HelperTests` are now Skipped, not Failed.
- Exactly 11 Assert.NotNull failures in `BwaveCycTaR6HelperTests` are now Skipped, not Failed.
- The 6 TypeInitializationException failures in `BwaveCycTaR6HelperTests` remain Failed
  (unchanged -- Lane B scope).
- Zero tests that previously passed are now Failing (no regressions).
- Passed count remains 19 (unchanged).
- Final global counts: Total=501, Passed=19, Failed<=307, Skipped>=175.
- The exact ASCII skip string is used.
- No `lock()` added anywhere.
- No production code touched.
- `deploy-sync.ps1` executed and completed with success.

---

## Return Value

TICKETS_COMPLETE
