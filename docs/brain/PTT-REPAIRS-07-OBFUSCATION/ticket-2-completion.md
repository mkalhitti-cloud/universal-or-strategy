# PTT-REPAIRS-07-OBFUSCATION -- Ticket 2 Completion
# Phase: 4a  Status: BUILD_PASS
# Engineer: ptt-engineer  Date: 2026-08-10
# Source: 04-tickets.md (Ticket 2), 04-ticket-review.md (TICKET_REVIEW_PASS)

---

## Ticket Scope

Ticket 2 -- BwaveCycTaR3HelperTests ONLY

File modified: `src/PropTraderTools/CopyEngineTests.cs`
Class modified: `BwaveCycTaR3HelperTests` (lines 6812-7105)
No production code touched.

---

## Pre-Edit Baseline

Command:
```
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --filter "FullyQualifiedName~BwaveCycTaR3HelperTests" --no-build
```

Result:
```
Failed: 35, Passed: 0, Skipped: 0, Total: 35, Duration: 222 ms
```

All 35 failures were `Assert.NotNull() Failure` (pure obfuscation failures, zero TypeInitializationException).

---

## [Fact] Methods with Skip= Added (35 total)

1.  TrySyncAtmBrackets_ShouldExist_AsPrivateHelper                           (line 6819)
2.  TrySkipTrailingStop_ShouldExist_AsPrivateHelper                          (line 6831)
3.  SyncStandardBracket_ShouldExist_AsPrivateHelper                          (line 6840)
4.  IsPttTgtDragOrder_ShouldExist_AsPrivateHelper                            (line 6849)
5.  IsAtmTgtOrder_ShouldExist_AsPrivateHelper                                (line 6858)
6.  SyncAtmFollowerStopBracket_ShouldReturn_WhenStopPriceIsZero              (line 6867)
7.  SyncAtmFollowerStopBracket_ShouldCallResubmitTarget_WhenCapturedPriceHasValue (line 6874)
8.  IsBePendingTargetOrder_ShouldReturnTrue_WhenOrderNameIsPttQxT1           (line 6882)
9.  IsBePendingTargetOrder_ShouldReturnTrue_WhenOrderNameIsTarget1           (line 6889)
10. IsBePendingTargetOrder_ShouldReturnFalse_WhenOrderNameIsUnrelated        (line 6896)
11. IsPttBeStopRejected_ShouldReturnTrue_WhenOrderIsRejectedPttBeStop        (line 6904)
12. IsPttBeStopRejected_ShouldReturnFalse_WhenOrderNameIsNotPttBeStop        (line 6911)
13. IsPttBeStopRejected_ShouldReturnFalse_WhenOrderStateIsFilledNotRejected  (line 6918)
14. LogBeSlotEviction_ShouldExist_AsPrivateVoidMethod                        (line 6926)
15. LogBeSlotEviction_ShouldAccept_AccNameAndIsRejectedParameters            (line 6933)
16. IsPttDragOrderCancellable_ShouldReturnTrue_WhenWorkingPttTgtDragMatchesInstrument (line 6941)
17. IsPttDragOrderCancellable_ShouldReturnTrue_WhenWorkingPttStpDragMatchesInstrument (line 6949)
18. IsPttDragOrderCancellable_ShouldReturnFalse_WhenOrderStateIsNotWorking   (line 6956)
19. IsPttDragOrderCancellable_ShouldReturnFalse_WhenOrderNameIsUnknown       (line 6963)
20. IsPttQxTargetOrder_ShouldExist_AsPrivateHelper                           (line 6972)
21. IsNativeAtmBeRetryTarget_ShouldExist_AsPrivateHelper                     (line 6979)
22. IsBeRetryEligibleOrderState_ShouldExist_AsPrivateHelper                  (line 6986)
23. IsBeRetryOrderInvalid_ShouldExist_AsPrivateHelper                        (line 6993)
24. IsBeSlotNonTerminal_ShouldExist_AsPrivateHelper                          (line 7000)
25. IsBeFilledWithOpenPosition_ShouldExist_AsPrivateHelper                   (line 7007)
26. IsPttDragOrderName_ShouldExist_AsPrivateHelper                           (line 7014)
27. IsDragInstrumentMatch_ShouldExist_AsPrivateHelper                        (line 7021)
28. IsQxTOrderStateValid_ShouldExist_AsPrivateHelper                         (line 7030)
29. IsQxTBracketNameValid_ShouldExist_AsPrivateHelper                        (line 7039)
30. TryGetCleanupEntryForFollower_ShouldExist_AsPrivateHelper                (line 7048)
31. IsCleanupEntryCurrentAndMatching_ShouldExist_AsPrivateHelper             (line 7060)
32. SendAtmCancelReplace_ShouldExist_AsPrivateHelper                         (line 7072)
33. TryMatchFollowerInRule_ShouldExist_AsPrivateHelper                       (line 7081)
34. IsBeReplaceTargetValid_ShouldReturnFalse_WhenOrderIsNull                 (line 7090)
35. TryIncrementBeReplaceAttempt_ShouldExist_AsPrivateHelper                 (line 7099)

Skip string used (exact):
`obfuscation: AgileDotNetRT renames private members; cannot locate by string name`

---

## 7-Scan Results

### SCAN-1: lock() check
Command: `Select-String -Path src/PropTraderTools/CopyEngineTests.cs -Pattern "lock\(" | Measure-Object -Line`
Result: **0** -- PASS

### SCAN-2: Non-ASCII check
Command: `Select-String -Path src/PropTraderTools/CopyEngineTests.cs -Pattern "[^\x00-\x7F]" | Measure-Object -Line`
Result: **0** -- PASS

### SCAN-3: Build error CS count
Command: `dotnet build src/PropTraderTools/PropTraderTools.Tests.csproj 2>&1 | Select-String " error CS" | Measure-Object -Line`
Result: **0** -- PASS

### SCAN-4: Build 0 Error(s)
Command: `dotnet build src/PropTraderTools/PropTraderTools.Tests.csproj 2>&1 | Select-String "Error\(s\)"`
Result: `0 Error(s)` -- PASS

### SCAN-5: dotnet test BwaveCycTaR3HelperTests
Command: `dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --filter "FullyQualifiedName~BwaveCycTaR3HelperTests" --no-build`
Result:
```
Skipped! - Failed: 0, Passed: 0, Skipped: 35, Total: 35, Duration: 137 ms
```
All 35 previously-failing tests now Skipped. 0 Failed. -- PASS

### SCAN-6: deploy-sync.ps1
Command: `powershell -File .\deploy-sync.ps1`
Result:
```
--- SYNC COMPLETE: One Source of Truth Established ---
```
PASS

### SCAN-7: HardLink check
Command: `fsutil hardlink list "src\PropTraderTools\CopyEngineTests.cs"`
Result: 1 entry (`\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs`)
SYNC COMPLETE confirms valid hard link state for production files. -- PASS

---

## Post-Edit Test Counts (BwaveCycTaR3HelperTests)

```
Failed: 0, Passed: 0, Skipped: 35, Total: 35
```

Delta from baseline: -35 Failed, +35 Skipped.

---

## BUILD_PASS
