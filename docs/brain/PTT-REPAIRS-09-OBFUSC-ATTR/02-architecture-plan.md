# PTT-REPAIRS-09-OBFUSC-ATTR Architecture Plan
**Epic:** PTT-REPAIRS-09-OBFUSC-ATTR  
**Phase:** 1 — Architecture  
**Author:** ptt-architect  
**Date:** 2025-01  
**Status:** PLAN_COMPLETE

---

## CRITICAL ARCHITECTURAL FINDING

> **Source investigation revealed a material discrepancy between the spec and the codebase.**  
> The spec states: "Add ObfuscationAttribute to methods accessed by reflection in the test suite, re-enabling 146 currently-skipped tests."  
> **Finding: 70 of the 73 methods referenced in the 140 obfuscation-skipped tests do NOT exist in `CopyEngine.cs`. ObfuscationAttribute alone cannot re-enable those tests; the methods must first be implemented.**

This plan documents:
1. The 3 members that DO exist and can be decorated now.
2. The 70 missing members as new deferred items.
3. N = 0 safe skip-removals (skip removal deferred until implementations land).
4. Verification baseline: unchanged at 24/0/490/514.

---

## LANE-SPLIT GATE RESULT: SINGLE-PIPELINE

| Question | Answer | Rationale |
|----------|--------|-----------|
| Q1. Same method or within 50 lines? | No (3,000+ lines apart) | But all changes are pure attribute insertions with no cross-dependency |
| Q2. Fix B design depends on Fix A? | No | Each attribute insertion is independent |
| Q3. Each fix has standalone value if the other is blocked? | Yes | Each attribute independently protects one member |
| Q4. Each fix has independent SIM verification path? | Yes | dotnet test passes for each addition individually |

**Result: SINGLE-PIPELINE.** One ticket (T1) covers all 3 attribute insertions. No lane split warranted.

---

## STEP 1 — ENUMERATED REFLECTION TARGETS

### 1a. Complete method name list from obfuscation-skipped tests (140 total)

Sources scanned:
- `B79CancelRaceGuardTests` (~L5829–L6461)
- `BwaveCycT1R1BeHelperTests` (~L6475–L6683)
- `BwaveCycTaR2HelperTests` (~L6692–L6811)
- `BwaveCycTaR3HelperTests` (~L6818–L7111)
- `BwaveCycTaR6HelperTests` (~L7116–L7274)

**Deduplicated method name inventory (73 unique names):**

| Method Name | Kind | Test Classes Referencing It |
|-------------|------|-----------------------------|
| TryFireImmediateBeIfAlreadyAtLevel | method | B79Cancel, T1R1Be |
| IsPendingBeTriggerMet | method | B79Cancel, T1R1Be |
| IsEligibleBeTargetOrder | method | B79Cancel, TaR2 |
| IsNativeAtmTargetOrder | method | B79Cancel |
| IsPttBeOrQxTargetOrder | method | B79Cancel |
| LogDiagOrderCount | method | B79Cancel (**[Fact] not obfuscation-skip**) |
| RegisterBeRetryIfNoTargets | method | B79Cancel |
| RegisterPartialTargetBeRetry | method | B79Cancel |
| CancelExistingStpDragOrders | method | B79Cancel |
| CancelExistingTgtDragOrders | method | B79Cancel |
| SubmitReplacementStopLeg | method | B79Cancel |
| SubmitReplacementTargetLeg | method | B79Cancel |
| IsReArmedAtmBracketCleanupRequired | method | B79Cancel |
| FindMatchingNativeAtmBracket | method | B79Cancel |
| TryFindRuleAndFollowerIndex | method | B79Cancel |
| HasActiveQxOrdersForInstrument | method | B79Cancel |
| SyncAtmFollowerStopBracket | method | B79Cancel, TaR3 |
| CancelStaleTgtDragOrders | method | B79Cancel |
| CreateAndSubmitReplacementTarget | method | B79Cancel |
| HasInFlightFlattenOrder | method | B79Cancel |
| IsPositionFlatOrMissing | method | B79Cancel |
| IsLeaderTargetOrder | method | B79Cancel, TaR2 |
| ResubmitFollowerEntry | method | B79Cancel |
| IsLeaderAccountForInstrument | method | B79Cancel |
| CancelStaleCascadeTgtDrag | method | B79Cancel |
| GetMarketBidPrice | method | T1R1Be |
| GetMarketAskPrice | method | T1R1Be |
| GetBeTickSize | method | T1R1Be |
| SelectBeRefPriceByDirection | method | T1R1Be |
| FireBeAndNotifyEvent | method | T1R1Be |
| ShouldFireBeImmediately | method | T1R1Be |
| CompleteBeArming | method | T1R1Be |
| GetSenderAccountName | method | T1R1Be (**NT8-skip**), TaR2 (**obfuscation-skip**) |
| TryClaimPendingBeSlot | method | T1R1Be |
| GetSlotInstrumentName | method | T1R1Be |
| GetSlotAccountName | method | T1R1Be |
| RaisePendingBeFiredEvent | method | T1R1Be |
| SettleAndFirePendingBe | method | T1R1Be |
| HasValidTargetNameSuffix | method | TaR2 |
| SelectBeTargetList | method | TaR2 |
| IsBeTargetActiveState | method | TaR2 |
| IsBeTargetPendingChangeState | method | TaR2 |
| IsBeTargetSnapshotState | method | TaR2 |
| TrySyncAtmBrackets | method | TaR3 |
| TrySkipTrailingStop | method | TaR3 |
| SyncStandardBracket | method | TaR3 |
| IsPttTgtDragOrder | method | TaR3 |
| IsAtmTgtOrder | method | TaR3 |
| IsBePendingTargetOrder | method | TaR3 |
| IsPttBeStopRejected | method | TaR3 |
| LogBeSlotEviction | method | TaR3 (**obfuscation-skip**) |
| IsPttDragOrderCancellable | method | TaR3 |
| IsPttQxTargetOrder | method | TaR3 |
| IsNativeAtmBeRetryTarget | method | TaR3 |
| IsBeRetryEligibleOrderState | method | TaR3 |
| IsBeRetryOrderInvalid | method | TaR3 |
| IsBeSlotNonTerminal | method | TaR3 |
| IsBeFilledWithOpenPosition | method | TaR3 |
| IsPttDragOrderName | method | TaR3 |
| IsDragInstrumentMatch | method | TaR3 |
| IsQxTOrderStateValid | method | TaR3 |
| IsQxTBracketNameValid | method | TaR3 |
| TryGetCleanupEntryForFollower | method | TaR3 |
| IsCleanupEntryCurrentAndMatching | method | TaR3 |
| SendAtmCancelReplace | method | TaR3 |
| TryMatchFollowerInRule | method | TaR3 |
| IsBeReplaceTargetValid | method | TaR3 |
| TryIncrementBeReplaceAttempt | method | TaR3 |
| IsBracketOrderLiveState | method | TaR6 (GetStaticMethod) |
| MatchesPttReplacementName | method | TaR6 (GetStaticMethod) |
| LogHbcDiag | method | TaR6 (GetInstanceMethod) |
| ExecuteStopDragOrder | method | TaR6 (GetInstanceMethod) |
| IsOrderEventProcessable | method | TaR6 (GetStaticMethod) |

**No field names are referenced by any obfuscation-skipped test** (confirmed by automated scan).

---

## STEP 2 — MEMBER LOCATION IN CopyEngine.cs

### Members Found (3 of 73)

| Member Name | Kind | Line (pre-insertion) | Access Modifier | Already Decorated? | Static? |
|-------------|------|---------------------|-----------------|-------------------|---------|
| `LogBeSlotEviction` | method | L1778 | private | No | **static** |
| `LogDiagOrderCount` | method | L6422 | private | No | instance |
| `GetSenderAccountName` | method | L6967 | internal | No | **static** |

### Members NOT Found (70 of 73)

All 70 methods listed in the table above (excluding LogBeSlotEviction, LogDiagOrderCount, GetSenderAccountName) are **absent from `CopyEngine.cs`** and absent from all other .cs files in `src/PropTraderTools/`. These are unimplemented contract methods.

> **Root Cause:** The test classes (B79CancelRaceGuardTests, BwaveCycT1R1BeHelperTests, BwaveCycTaR2HelperTests, BwaveCycTaR3HelperTests, BwaveCycTaR6HelperTests) were authored contract-first, ahead of the implementation epics. The `obfuscation:` Skip is a dual guard: "not yet implemented AND would fail under obfuscation."

### Test Binding Flag Discrepancy (Additional Finding)

The two static methods found have a binding flag mismatch with their test helpers:

| Method | Actual | Test Helper Binding Flags | Will Find? |
|--------|--------|--------------------------|------------|
| `LogBeSlotEviction` | private **static** | `NonPublic \| Instance` (BwaveCycTaR3) | **NO** |
| `GetSenderAccountName` | internal **static** | `NonPublic \| Instance` (BwaveCycTaR2) | **NO** |
| `LogDiagOrderCount` | private instance | `NonPublic \| Instance` (B79Cancel) | **YES** |

`LogDiagOrderCount`'s test at L6094 uses correct binding flags and is already `[Fact]` (no skip) — currently **passing**.

---

## STEP 3 — ATTRIBUTE ADDITION PLAN

Three attribute insertions. All three are planned even though only one (LogDiagOrderCount) has matching test binding flags, because:
1. Protection against AgileDotNetRT rename is a compilation-time concern independent of test binding flags.
2. The skip-removal follow-on (DW-09-02/03) will fix binding flags to match.

### Insertion 1: `LogBeSlotEviction`

**File:** `src/PropTraderTools/CopyEngine.cs`  
**Before line:** L1778  
**Current L1778:**
```csharp
        private static void LogBeSlotEviction(string accName, bool isRejected)
```
**Insert immediately above:**
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
```
**Indentation:** 8 spaces (matches surrounding code).  
**CYC impact:** Zero (attribute is not a branch).

### Insertion 2: `LogDiagOrderCount`

**File:** `src/PropTraderTools/CopyEngine.cs`  
**Before line:** L6422 (shifts to L6423 after Insertion 1 adds 1 line)  
**Current L6422:**
```csharp
        private void LogDiagOrderCount(Account acc, Instrument instrument)
```
**Insert immediately above:**
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
```
**Indentation:** 8 spaces.  
**CYC impact:** Zero.  
**Note:** This protects the currently-passing `[Fact]` test (L6094) from a future obfuscated build regression.

### Insertion 3: `GetSenderAccountName`

**File:** `src/PropTraderTools/CopyEngine.cs`  
**Before line:** L6967 (shifts to L6970 after Insertions 1+2 add 2 lines)  
**Current L6967:**
```csharp
        internal static string GetSenderAccountName(object sender)
```
**Insert immediately above:**
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
```
**Indentation:** 8 spaces.  
**CYC impact:** Zero.

---

## STEP 4 — SKIP REMOVAL PLAN

### Scope Decision: ZERO skips removed in this epic.

**Rationale:**

| Category | Count | Safe to un-skip? | Reason |
|----------|-------|-----------------|--------|
| Tests referencing existing instance method (LogDiagOrderCount) | 0 obfuscation-skip tests | N/A | No obfuscation-skip test exists for this method |
| Tests referencing existing static method (LogBeSlotEviction) | 2 | **NO** | Test uses NonPublic\|Instance; method is static → GetMethod returns null → test FAILS |
| Tests referencing existing static internal method (GetSenderAccountName) | 1 | **NO** | Test uses NonPublic\|Instance; method is static → GetMethod returns null → test FAILS |
| Tests referencing non-existent methods (70 methods) | 137 | **NO** | Methods don't exist → GetMethod returns null → test FAILS |
| **Total** | **140** | **0** | |

**Risk Assessment:** Any skip removal from this plan would convert SKIPPED → FAILED. Zero skip removals is the correct safe scope.

### Deferred Skip-Removal Conditions

Skip removal becomes safe only after ALL of the following are complete:
1. **DW-09-01:** 70 missing methods implemented in CopyEngine.cs (large implementation epic).
2. **DW-09-02:** Fix binding flags in `BwaveCycTaR3HelperTests` for `LogBeSlotEviction` (NonPublic|Instance → NonPublic|Static).
3. **DW-09-03:** Fix binding flags in `BwaveCycTaR2HelperTests` for `GetSenderAccountName` (NonPublic|Instance → NonPublic|Static OR NonPublic|Instance if the method is changed to instance).
4. **DW-09-04:** For all 137 tests referencing non-existent methods — after DW-09-01 implements each method, verify the test helper binding flags match (static vs instance) and remove Skip individually.

---

## STEP 5 — VERIFICATION PLAN

### Command

```
dotnet test src/PropTraderTools/ --no-build --verbosity normal
```

### Expected Counts

| Scenario | Passed | Failed | Skipped | Total |
|----------|--------|--------|---------|-------|
| **Baseline (current)** | 24 | 0 | 490 | 514 |
| **After T1 (ObfuscationAttribute additions)** | **24** | **0** | **490** | **514** |

**N = 0 tests whose Skip is removed by this plan.**  
The pass/skip/total counts are unchanged. The value delivered is compile-time obfuscation protection for 3 existing members, not skip-to-pass conversion.

### Post-Edit Validation

After each `.cs` edit by the engineer:
```
powershell -File .\deploy-sync.ps1
dotnet test src/PropTraderTools/ --no-build
```
Both commands must succeed with output: `Failed: 0, Passed: 24, Skipped: 490, Total: 514`.

---

## STEP 6 — TICKET STRUCTURE

### Single Ticket: T1

| Field | Value |
|-------|-------|
| Ticket ID | T1 |
| Title | Add ObfuscationAttribute to 3 CopyEngine private/internal members |
| File | `src/PropTraderTools/CopyEngine.cs` |
| Spec Req IDs | PTT-REPAIRS-09-OBFUSC-ATTR STEP 3 |
| Blocked by | Nothing |
| Blocks | DW-09-02, DW-09-03, DW-09-04 (skip removal deferred) |

**Members to decorate:**

| # | Member | Line (pre-edit) | Insertion |
|---|--------|-----------------|-----------|
| 1 | `private static void LogBeSlotEviction` | L1778 | `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]` on line above |
| 2 | `private void LogDiagOrderCount` | L6422 | `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]` on line above |
| 3 | `internal static string GetSenderAccountName` | L6967 | `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]` on line above |

**Method signatures (exact, unchanged):**
```csharp
// No signature changes. Attribute line precedes declaration only.
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private static void LogBeSlotEviction(string accName, bool isRejected)

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void LogDiagOrderCount(Account acc, Instrument instrument)

[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
internal static string GetSenderAccountName(object sender)
```

**xUnit tests to run (unchanged — no new tests, no skip removals):**
```
dotnet test src/PropTraderTools/ --no-build
// Expected: Failed: 0, Passed: 24, Skipped: 490, Total: 514
```

---

## STEP 7 — 7-SCAN CHECKLIST (T1)

| Scan | Check | Result |
|------|-------|--------|
| SCAN-01 | No `lock()` added | PASS — attribute adds no code |
| SCAN-02 | No `DateTime.Now` added | PASS — attribute adds no code |
| SCAN-03 | ASCII-only strings in additions | PASS — `"rename"` and `"true"` are ASCII |
| SCAN-04 | No `FontFamily` added | PASS — N/A |
| SCAN-05 | No hardcoded hex colors | PASS — N/A |
| SCAN-06 | No new `throw` statements | PASS — attribute adds no code |
| SCAN-07 | Hard-link sync after src edit | **REQUIRED** — run `powershell -File .\deploy-sync.ps1` after CopyEngine.cs is modified |

---

## NEW DEFERRED ITEMS

The following items are CREATED by this epic's investigation (not previously tracked):

| ID | Title | Blocked Until |
|----|-------|---------------|
| DW-09-01 | Implement 70 unimplemented CopyEngine helper methods referenced by obfuscation-skipped tests (full BWAVE-CYC group) | New epic required — large scope |
| DW-09-02 | Fix binding flags in `BwaveCycTaR3HelperTests.GetMethod` for `LogBeSlotEviction` (NonPublic\|Instance → NonPublic\|Static) + remove obfuscation Skip from 2 tests | After DW-09-01 confirms LogBeSlotEviction exists as private static |
| DW-09-03 | Fix binding flags in `BwaveCycTaR2HelperTests.GetMethod` for `GetSenderAccountName` (NonPublic\|Instance → NonPublic\|Static) + remove obfuscation Skip from 1 test | After DW-09-01 scope for GetSenderAccountName addressed |
| DW-09-04 | Remove obfuscation Skip from all 137 remaining tests after DW-09-01 implementations, binding flag fixes, and ObfuscationAttribute additions are all complete | After DW-09-01 + DW-09-02 + DW-09-03 |

**DW-B7-01 note:** The pre-existing `B79CancelRaceGuardTests` 5th TypeInit test (from PTT-REPAIRS-07-NT8-BULK-SKIP) remains OUT OF SCOPE per orchestrator instruction.

---

## COMPONENT LIST

| Component | Class | File | Change |
|-----------|-------|------|--------|
| Copy Engine BE slot eviction helper | `CopyEngine` | `src/PropTraderTools/CopyEngine.cs` | +1 attribute at L1778 |
| Copy Engine diagnostic helper | `CopyEngine` | `src/PropTraderTools/CopyEngine.cs` | +1 attribute at L6422 |
| Copy Engine sender account name helper | `CopyEngine` | `src/PropTraderTools/CopyEngine.cs` | +1 attribute at L6967 |

---

## NT8 API USAGE

None. `System.Reflection.ObfuscationAttribute` is a pure .NET BCL type (mscorlib). No NinjaTrader 8 API is used or affected by this plan.

---

## THREADING MODEL

None. ObfuscationAttribute is compile-time metadata. No runtime threading impact. No `Dispatcher.InvokeAsync` required. No `ConcurrentQueue` involved.

---

## DATA FLOW

No data flow change. ObfuscationAttribute annotations do not affect method behavior, signatures, or call paths.

---

## PRIOR DEFERRED BACKLOG (READ-ONLY)

- **DW-B7-01:** B79CancelRaceGuardTests 5th TypeInit test unidentified (from PTT-REPAIRS-07-NT8-BULK-SKIP). **OUT OF SCOPE for this epic.**

---

## RETURN VALUE

**PLAN_COMPLETE**

The plan is internally consistent, grounded in source evidence, and delivers the maximum safe scope: 3 ObfuscationAttribute additions to existing CopyEngine members, zero skip removals, unchanged test counts. The 140-test obfuscation-skip enablement requires a separate implementation epic (DW-09-01) before skip removal is safe.
