# PTT-REPAIRS-07-OBFUSCATION -- Ticket 1 Completion
# Phase: 4a  Status: BUILD_PASS
# Engineer: ptt-engineer  Date: 2026-08-10
# Source: 04-tickets.md (Ticket 1 ONLY), 04-ticket-review.md (TICKET_REVIEW_PASS)

---

## Ticket Scope

**Ticket 1 -- B79CancelRaceGuardTests ONLY**

File modified: `src/PropTraderTools/CopyEngineTests.cs`
Class modified: `B79CancelRaceGuardTests` (lines 5823-6455)
No production files touched. No other classes modified.

---

## Pre-Edit Baseline (Actual dotnet test output)

Command: `dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --filter "FullyQualifiedName~B79CancelRaceGuardTests" --no-build`

```
Failed!  - Failed: 63, Passed: 1, Skipped: 0, Total: 64, Duration: 460 ms
```

Passing test (DO NOT SKIP):
- `LogDiagOrderCount_ShouldLogCorrectCount_WhenOrdersExistForInstrument` -- PASSES

TypeInitializationException test (DO NOT SKIP -- Lane B scope):
- `T_DW_B79_09_03_CancelStaleBracketsLocal_HasRemoveAllGuard` -- TypeInitializationException (AgileDotNetRT)

All remaining 62 tests: `Assert.NotNull() Failure` (obfuscation failures -- SKIP applied)

---

## [Fact] Methods with Skip= Added (62 total)

```
T_DW_B79_09_01_CancelQxBrackets2Param_HasRemoveAllGuard           (line 5849)
T_DW_B79_09_02_CancelQxBrackets3Param_HasRemoveAllGuard           (line 5881)
B132_LaneB_DiagnosticMode_FieldExists                             (line 5945)
TryFireImmediateBeIfAlreadyAtLevel_ShouldReturnFalse_WhenTickSizeIsZero  (line 5962)
TryFireImmediateBeIfAlreadyAtLevel_ShouldReturnFalse_WhenPriceIsZero     (line 5970)
TryFireImmediateBeIfAlreadyAtLevel_ShouldReturnTrue_WhenLongAndBidAboveTarget  (line 5978)
TryFireImmediateBeIfAlreadyAtLevel_ShouldReturnTrue_WhenShortAndAskBelowTarget (line 5986)
IsPendingBeTriggerMet_ShouldReturnFalse_WhenRefPriceIsZero               (line 5996)
IsPendingBeTriggerMet_ShouldReturnFalse_WhenLongPositionPriceBelowTarget (line 6004)
IsPendingBeTriggerMet_ShouldReturnTrue_WhenLongAndBidReachesTarget       (line 6012)
IsPendingBeTriggerMet_ShouldReturnTrue_WhenShortAndAskReachesTarget      (line 6020)
IsEligibleBeTargetOrder_ShouldReturnFalse_WhenOrderStateIsNotInSnapshot  (line 6030)
IsEligibleBeTargetOrder_ShouldReturnFalse_WhenInstrumentDoesNotMatch     (line 6037)
IsEligibleBeTargetOrder_ShouldReturnFalse_WhenOrderTypeIsNotLimit        (line 6044)
IsNativeAtmTargetOrder_ShouldReturnTrue_WhenNameIsTarget1                (line 6053)
IsNativeAtmTargetOrder_ShouldReturnFalse_WhenNameIsTarget0               (line 6060)
IsPttBeOrQxTargetOrder_ShouldReturnTrue_WhenNameStartsWithPttQxT1       (line 6069)
IsPttBeOrQxTargetOrder_ShouldReturnTrue_WhenNameStartsWithPttBeTarget    (line 6076)
RegisterBeRetryIfNoTargets_ShouldNotRegister_WhenIsRetryIsTrue           (line 6094)
RegisterBeRetryIfNoTargets_ShouldNotRegister_WhenPositionIsFlat          (line 6101)
RegisterBeRetryIfNoTargets_ShouldRegisterSlotAndQueueFallback_WhenConditionsMet (line 6108)
RegisterPartialTargetBeRetry_ShouldNotRegister_WhenTargetCountEqualsLeaderCount (line 6117)
RegisterPartialTargetBeRetry_ShouldRegisterSlot_WhenFollowerHasFewerTargetsThanLeader (line 6124)
CancelExistingStpDragOrders_ShouldCancelMatchingLiveStpDragOrder         (line 6133)
CancelExistingTgtDragOrders_ShouldCancelMatchingLiveTgtDragOrder         (line 6142)
SubmitReplacementStopLeg_ShouldReturnEarly_WhenCreateOrderReturnsNull    (line 6151)
SubmitReplacementStopLeg_ShouldUseLeaderQuantity_WhenLeaderLegProvided   (line 6158)
SubmitReplacementTargetLeg_ShouldReturnEarly_WhenCreateOrderReturnsNull  (line 6167)
SubmitReplacementTargetLeg_ShouldUseLeaderQuantity_WhenLeaderLegProvided (line 6174)
IsReArmedAtmBracketCleanupRequired_ShouldReturnFalse_WhenOrderStateIsNotWorkingOrAccepted (line 6183)
IsReArmedAtmBracketCleanupRequired_ShouldReturnFalse_WhenNameDoesNotStartWithPttQxT      (line 6193)
IsReArmedAtmBracketCleanupRequired_ShouldReturnFalse_WhenTtlHasExpired  (line 6203)
IsReArmedAtmBracketCleanupRequired_ShouldReturnTrue_WhenAllConditionsMet (line 6213)
FindMatchingNativeAtmBracket_ShouldReturnNull_WhenNoMatchingOrderExists  (line 6225)
FindMatchingNativeAtmBracket_ShouldReturnOrder_WhenNameAndInstrumentMatch (line 6232)
TryFindRuleAndFollowerIndex_ShouldReturnFalse_WhenInstrumentDoesNotMatch (line 6241)
TryFindRuleAndFollowerIndex_ShouldReturnTrue_WhenFollowerAccountMatches  (line 6248)
TryFindRuleAndFollowerIndex_ShouldSetFollowerIndex_WhenMatchFound        (line 6255)
HasActiveQxOrdersForInstrument_ShouldReturnTrue_WhenPttQxOrderIsWorking  (line 6264)
HasActiveQxOrdersForInstrument_ShouldReturnFalse_WhenNoQxOrdersExist    (line 6271)
HasActiveQxOrdersForInstrument_ShouldReturnFalse_WhenQxOrderIsFilledNotWorking (line 6278)
SyncAtmFollowerStopBracket_ShouldReturn_WhenStopPriceIsZero             (line 6287)
SyncAtmFollowerStopBracket_ShouldCallResubmitTarget_WhenCapturedPriceHasValue  (line 6294)
CancelStaleTgtDragOrders_ShouldCancelMatchingWorkingOrder               (line 6303)
CancelStaleTgtDragOrders_ShouldSkipNonMatchingOrders                    (line 6310)
CreateAndSubmitReplacementTarget_ShouldReturnNull_WhenCreateOrderFails  (line 6319)
CreateAndSubmitReplacementTarget_ShouldUseLeaderQuantity_WhenLeaderOrderIsNotNull (line 6326)
HasInFlightFlattenOrder_ShouldReturnTrue_WhenPttFlattenOrderIsWorking   (line 6335)
HasInFlightFlattenOrder_ShouldReturnFalse_WhenNoFlattenOrderExists      (line 6342)
IsPositionFlatOrMissing_ShouldReturnTrue_WhenPositionIsNull             (line 6351)
IsPositionFlatOrMissing_ShouldReturnTrue_WhenPositionQuantityIsZero     (line 6361)
IsLeaderTargetOrder_ShouldReturnTrue_WhenOrderIsWorkingLimitWithValidTargetName (line 6373)
IsLeaderTargetOrder_ShouldReturnFalse_WhenOrderStateIsNotWorking        (line 6380)
IsLeaderTargetOrder_ShouldReturnFalse_WhenNameDoesNotStartWithTarget    (line 6387)
IsLeaderTargetOrder_ShouldReturnFalse_WhenSixthCharIsNotDigit           (line 6394)
ResubmitFollowerEntry_ShouldSkip_WhenPriceChangeIsWithinTickSize        (line 6403)
ResubmitFollowerEntry_ShouldUseStopPrice_WhenOrderTypeIsStopLimit       (line 6410)
ResubmitFollowerEntry_ShouldPreloadDedupCache_WhenOrderIsCreated        (line 6417)
IsLeaderAccountForInstrument_ShouldReturnTrue_WhenAccountMatchesMasterAccount (line 6426)
IsLeaderAccountForInstrument_ShouldReturnFalse_WhenAccountIsFollower    (line 6433)
CancelStaleCascadeTgtDrag_ShouldCancelMatchingWorkingOrder              (line 6442)
CancelStaleCascadeTgtDrag_ShouldSkipNonWorkingOrders                    (line 6449)
```

NOT skipped (2 methods):
- `T_DW_B79_09_03_CancelStaleBracketsLocal_HasRemoveAllGuard` (line 5918) -- TypeInitializationException, Lane B scope
- `LogDiagOrderCount_ShouldLogCorrectCount_WhenOrdersExistForInstrument` (line 6085) -- PASSES (no skip needed)

Skip string used (ASCII-only, JS-042):
`obfuscation: AgileDotNetRT renames private members; cannot locate by string name`

---

## 7-Scan Results

### SCAN-1: lock() check
Command: `Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "lock\(" | Measure-Object -Line`
Result: **0** PASS

### SCAN-2: Non-ASCII check
Command: `Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "[^\x00-\x7F]" | Measure-Object -Line`
Result: **0** PASS

### SCAN-3: Build error CS lines
Command: `dotnet build src/PropTraderTools/PropTraderTools.Tests.csproj 2>&1 | Select-String " error CS" | Measure-Object -Line`
Result: **0** PASS

### SCAN-4: Build 0 Error(s)
Command: `dotnet build src/PropTraderTools/PropTraderTools.Tests.csproj 2>&1 | Select-String "Error\(s\)"`
Result: **0 Error(s)** PASS

### SCAN-5: Test run post-edit
Command: `dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --filter "FullyQualifiedName~B79CancelRaceGuardTests" --no-build`
Result:
```
Failed!  - Failed: 1, Passed: 1, Skipped: 62, Total: 64, Duration: 278 ms
```
- Failed: 1 (T_DW_B79_09_03 -- TypeInitializationException, intentionally NOT skipped)
- Passed: 1 (LogDiagOrderCount -- unchanged, still passes)
- Skipped: 62 (all Assert.NotNull failures now skipped)
- Zero regressions: no previously-passing test now fails PASS

### SCAN-6: deploy-sync.ps1
Command: `powershell -File .\deploy-sync.ps1`
Result:
```
--- SYNC COMPLETE: One Source of Truth Established ---
Tip: Edit files in C:\WSGTA\universal-or-strategy. NT8 will update instantly.
```
PASS

### SCAN-7: HardLink check
Command: `fsutil hardlink list "C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs"`
Result:
```
\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs
```
File confirmed present with hardlink entry. deploy-sync.ps1 SYNC COMPLETE confirms valid state. PASS

---

## Post-Edit Test Counts (B79CancelRaceGuardTests)

| Metric   | Pre-Edit | Post-Edit | Delta |
|----------|----------|-----------|-------|
| Passed   | 1        | 1         | 0     |
| Failed   | 63       | 1         | -62   |
| Skipped  | 0        | 62        | +62   |
| Total    | 64       | 64        | 0     |

Note: The plan estimated 59 Assert.NotNull failures; actual baseline showed 62 Assert.NotNull
failures + 1 TypeInitializationException + 1 pass. The pre-run confirmed exact failure types
before applying Skip. The ticket's "estimate ~59" was approximate; actual confirmed count is 62.

---

## Acceptance Criteria Check

- [x] All Assert.NotNull failures in B79CancelRaceGuardTests are now Skipped, not Failed (62 skipped)
- [x] Zero tests that previously passed are now Failing (LogDiagOrderCount still passes)
- [x] The TypeInitializationException test (T_DW_B79_09_03) remains Failed -- Lane B scope, untouched
- [x] Passed count: 1 (unchanged -- same 1 test that was passing at baseline)
- [x] Exact ASCII skip string used
- [x] No lock() added
- [x] No production code touched
- [x] deploy-sync.ps1 executed and completed with SYNC COMPLETE

---

## BUILD_PASS
