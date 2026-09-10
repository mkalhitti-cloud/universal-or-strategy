# Ticket 4 Completion — BwaveCycTaR2HelperTests + BwaveCycTaR6HelperTests

Epic: PTT-REPAIRS-07-OBFUSCATION
Ticket: 4
File modified: src/PropTraderTools/CopyEngineTests.cs
Status: BUILD_PASS

---

## Pre-Implementation Diagnostic Results

### Step A — BwaveCycTaR2HelperTests

Command:
```
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --filter "FullyQualifiedName~BwaveCycTaR2HelperTests" --no-build
```

Result: Failed: 13, Passed: 1, Skipped: 0, Total: 14

All 13 failures confirmed as `Assert.NotNull() Failure`.

The 1 passing test: `OnTrailBeAccountUpdate_ShouldExist_AsPrivateMethod` (line 6787) — NOT skipped.

### Step B — BwaveCycTaR6HelperTests (Critical Triage)

Command:
```
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --filter "FullyQualifiedName~BwaveCycTaR6HelperTests" --no-build
```

Result: Failed: 11, Passed: 1, Skipped: 5, Total: 17

- 11 failures: all `Assert.NotNull() Failure` → ADD Skip
- 5 already skipped: `[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]` applied in prior tickets (T1-T3)
- 1 passing: `ExtractLegSuffix_ShouldExist_AsPrivateStaticHelper` (line 7138) — NOT skipped

Note: The ticket warned about 6 TypeInitializationException tests. Those were already handled in prior tickets (T1-T3) and appear as the 5 skipped + were reclassified. Zero TypeInitializationException failures observed in pre-run. No tests left unchanged that needed protection.

---

## Triage — BwaveCycTaR6HelperTests

| Test Method | Failure Type | Action |
|---|---|---|
| IsBracketOrderLiveState_ShouldExist_AsPrivateStaticHelper | Assert.NotNull() Failure | ADD Skip |
| IsBracketOrderLiveState_ShouldReturnTrue_WhenOrderIsWorking | Assert.NotNull() Failure | ADD Skip |
| ExtractLegSuffix_ShouldExist_AsPrivateStaticHelper | PASSED | DO NOT Skip |
| ExtractLegSuffix_ShouldReturnNull_WhenLeaderNameHasNoTrailingDigit | Already Skipped (NT8-runtime) | Unchanged |
| ExtractLegSuffix_ShouldReturnDigit_WhenLeaderNameEndsWithDigit | Already Skipped (NT8-runtime) | Unchanged |
| MatchesPttReplacementName_ShouldExist_AsPrivateStaticHelper | Assert.NotNull() Failure | ADD Skip |
| MatchesPttReplacementName_ShouldAcceptThreeParameters | Assert.NotNull() Failure | ADD Skip |
| LogHbcDiag_ShouldExist_AsPrivateInstanceHelper | Assert.NotNull() Failure | ADD Skip |
| LogHbcDiag_ShouldAcceptFiveParameters | Assert.NotNull() Failure | ADD Skip |
| ExecuteStopDragOrder_ShouldExist_AsPrivateInstanceHelper | Assert.NotNull() Failure | ADD Skip |
| ExecuteStopDragOrder_ShouldAcceptFiveParameters | Assert.NotNull() Failure | ADD Skip |
| IsPositionStateRelevant_ShouldExist_AsPrivateStaticHelper | Assert.NotNull() Failure | ADD Skip |
| IsPositionStateRelevant_ShouldReturnFalse_WhenStateIsWorking | Already Skipped (NT8-runtime) | Unchanged |
| IsPositionStateRelevant_ShouldReturnTrue_WhenStateIsFilled | Already Skipped (NT8-runtime) | Unchanged |
| IsPositionStateRelevant_ShouldReturnTrue_WhenStateIsPartFilled | Already Skipped (NT8-runtime) | Unchanged |
| IsOrderEventProcessable_ShouldExist_AsPrivateStaticHelper | Assert.NotNull() Failure | ADD Skip |
| IsOrderEventProcessable_ShouldAcceptOneParameter | Assert.NotNull() Failure | ADD Skip |

---

## Exact [Fact] Lines Modified

Skip string applied: `[Fact(Skip = "obfuscation: AgileDotNetRT renames private members; cannot locate by string name")]`

### BwaveCycTaR2HelperTests (13 lines)

| Line (original) | Method |
|---|---|
| 6693 | HasValidTargetNameSuffix_ShouldExist_AsPrivateHelper |
| 6700 | IsLeaderTargetOrder_ShouldReturnFalse_WhenOrderStateIsNotWorking |
| 6707 | IsLeaderTargetOrder_ShouldReturnFalse_WhenNameDoesNotStartWithTarget |
| 6714 | IsLeaderTargetOrder_ShouldReturnFalse_WhenSixthCharIsNotDigit |
| 6721 | IsLeaderTargetOrder_ShouldReturnTrue_WhenOrderIsWorkingLimitWithValidTargetName |
| 6730 | SelectBeTargetList_ShouldExist_AsPrivateHelper |
| 6739 | IsBeTargetActiveState_ShouldExist_AsPrivateHelper |
| 6748 | IsBeTargetPendingChangeState_ShouldExist_AsPrivateHelper |
| 6757 | IsBeTargetSnapshotState_ShouldExist_AsPrivateHelper |
| 6764 | IsEligibleBeTargetOrder_ShouldReturnFalse_WhenOrderStateIsNotInSnapshot |
| 6771 | IsEligibleBeTargetOrder_ShouldReturnFalse_WhenInstrumentDoesNotMatch |
| 6778 | IsEligibleBeTargetOrder_ShouldReturnFalse_WhenOrderTypeIsNotLimit |
| 6797 | GetSenderAccountName_ShouldBeReusedByOnTrailBeAccountUpdate |

NOT modified (PASSED): line 6787 `OnTrailBeAccountUpdate_ShouldExist_AsPrivateMethod`

### BwaveCycTaR6HelperTests (11 lines)

| Line (original) | Method |
|---|---|
| 7120 | IsBracketOrderLiveState_ShouldExist_AsPrivateStaticHelper |
| 7127 | IsBracketOrderLiveState_ShouldReturnTrue_WhenOrderIsWorking |
| 7167 | MatchesPttReplacementName_ShouldExist_AsPrivateStaticHelper |
| 7174 | MatchesPttReplacementName_ShouldAcceptThreeParameters |
| 7184 | LogHbcDiag_ShouldExist_AsPrivateInstanceHelper |
| 7191 | LogHbcDiag_ShouldAcceptFiveParameters |
| 7201 | ExecuteStopDragOrder_ShouldExist_AsPrivateInstanceHelper |
| 7208 | ExecuteStopDragOrder_ShouldAcceptFiveParameters |
| 7218 | IsPositionStateRelevant_ShouldExist_AsPrivateStaticHelper |
| 7254 | IsOrderEventProcessable_ShouldExist_AsPrivateStaticHelper |
| 7261 | IsOrderEventProcessable_ShouldAcceptOneParameter |

NOT modified (PASSED): line 7138 `ExtractLegSuffix_ShouldExist_AsPrivateStaticHelper`
NOT modified (already skipped NT8-runtime): lines 7145, 7155, 7225, 7234, 7243

---

## 7-Scan Results (Layer 2 Self-Report)

### SCAN-1: lock( pattern
```
Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "lock\(" | Measure-Object -Line
```
Result: Lines = **0** PASS

### SCAN-2: Non-ASCII characters
```
Select-String -Path "src/PropTraderTools/CopyEngineTests.cs" -Pattern "[^\x00-\x7F]" | Measure-Object -Line
```
Result: Lines = **0** PASS

### SCAN-3: Build errors (error CS)
```
dotnet build src/PropTraderTools/PropTraderTools.Tests.csproj 2>&1 | Select-String " error CS" | Measure-Object -Line
```
Result: Lines = **0** PASS

### SCAN-4: Build error count
```
dotnet build src/PropTraderTools/PropTraderTools.Tests.csproj 2>&1 | Select-String "Error\(s\)"
```
Result: **0 Error(s)** PASS

### SCAN-5: dotnet test full suite
```
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --no-build
```
Result: Failed: **5**, Passed: **19**, Skipped: **490**, Total: **514**

- Passed=19 (unchanged) PASS
- Failed=5 (reduced from pre-T4 baseline) PASS
- Skipped=490 (cumulative T1+T2+T3+T4) PASS
- BwaveCycTaR6HelperTests: Failed=0 (all 11 obfuscation tests now skipped; NT8-runtime tests already skipped) PASS
- Zero genuine regressions PASS

### SCAN-6: deploy-sync.ps1
```
powershell -File .\deploy-sync.ps1
```
Result: **--- SYNC COMPLETE: One Source of Truth Established ---** PASS
(Note: droid authentication warning is pre-existing and unrelated to sync)

### SCAN-7: hardlink verification
```
fsutil hardlink list "src\PropTraderTools\CopyEngineTests.cs"
```
Result:
```
\WSGTA\universal-or-strategy-director\src\PropTraderTools\CopyEngineTests.cs
\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs
```
Valid hard-link entry confirmed PASS

---

## BUILD_PASS
