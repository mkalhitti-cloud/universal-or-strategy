# Implementation Plan: Phase 5 Session 5 - Signals/Trend Refactor

**MISSION**: Phase 5 - Signals/Trend Refactor
**BUILD_TAG**: 1111.006-v28.0-b984-complete
**REPO**: universal-or-strategy
**BRANCH**: main

## 1. STRATEGIC ANALYSIS & OBJECTIVE

The `src/V12_002.Entries.Trend.cs` file contains three god-functions in the
Signals/Trend subgraph. This session executes verbatim private extractions
to reduce cyclomatic complexity while preserving all rollback transactionality,
SIMA dispatch, and E1/E2 partnership logic exactly.

**Primary Objective**:
- T1: Extract `ExecuteTRENDEntry` (lines 58-289) into 6 private sub-handlers.
- T2: Extract `ExecuteTRENDManualEntry` (lines 361-445) into 4 private sub-handlers.
- T3: Extract `CreateTRENDPosition` (lines 291-344) into 2 private sub-handlers.

## 2. EXTRACTION PROTOCOL (V12 DNA)

- **Verbatim-Body**: Copy-paste exact logic bodies. Zero mutations.
- **Visibility**: All new sub-handlers MUST be `private`.
- **ASCII Compliance**: Zero non-ASCII characters in string literals.
- **Lock-Free**: `lock()` is BANNED. Actor/Enqueue only.
- **Atomic Sync**: Run `deploy-sync.ps1` after ALL tickets are done.
- **No public surface**: Zero new public methods anywhere.

## 3. TICKET BACKLOG

---

### T1: Extract ExecuteTRENDEntry

**File**: `src/V12_002.Entries.Trend.cs`
**Line range**: 58-289
**Handlers**:
- `ExecuteTREND_Preflight` -- IsOrderAllowed, isFlattenRunning, contracts<=0, BarsInProgress, !TRENDEnabled, EMA null checks
- `ExecuteTREND_ResolveDirection` -- currentPrice, ema9Value/ema15Value, direction determination
- `ExecuteTREND_CalculateLegs` -- ATR mults, stop distances, qty split (floor/remainder), timestamp/names, CreateTRENDPosition calls, ApplyTargetLadderGuard
- `ExecuteTREND_SubmitLeg1` -- AddExpectedPositionDelta E1, SubmitOrderUnmanaged E1, null-rollback, Enqueue activePositions+entryOrders
- `ExecuteTREND_SubmitLeg2` -- linkedTRENDEntries link, AddExpectedPositionDelta E2, SubmitOrderUnmanaged E2, null-rollback (TryRemove both + CancelOrderSafe(entryOrder1)), Enqueue activePositions+entryOrders
- `ExecuteTREND_DispatchSima` -- if(EnableSIMA) ExecuteSmartDispatchEntry("TREND",...,entry1Name,entry2Name), DeactivateTRENDMode()

**CRITICAL CONSTRAINTS**:
1. Preflight block lives ABOVE the try{...}catch in the original. ExecuteTREND_Preflight
   MUST be called from the parent BEFORE the try block begins.
2. The try-catch wrapper STAYS in the parent method.
3. ExecuteSmartDispatchEntry receives TWO entry-name args (entry1Name AND entry2Name).
   Both MUST be passed through to ExecuteTREND_DispatchSima.
4. E2-null rollback MUST call TryRemove on both entry1Name and entry2Name in
   linkedTRENDEntries, then CancelOrderSafe(entryOrder1).

**Acceptance criteria**:
- Compiles clean with zero build errors.
- ASCII-only gate passes.
- Zero new lock() calls introduced.
- Parent CC drops below 50.
- No new public API surface.

---

### T2: Extract ExecuteTRENDManualEntry

**File**: `src/V12_002.Entries.Trend.cs`
**Line range**: 361-445
**Handlers**:
- `ExecuteTRENDManual_Preflight` -- IsOrderAllowed, isFlattenRunning, contracts<=0, currentATR<=0
- `ExecuteTRENDManual_BuildPosition` -- entryPrice round, stopDistance/stopPrice, GetTargetDistribution, signalName/entryName, CreateTRENDPosition, ApplyTargetLadderGuard
- `ExecuteTRENDManual_SubmitEntry` -- AddExpectedPositionDelta, SubmitOrderUnmanaged, null-rollback, Enqueue activePositions+entryOrders
- `ExecuteTRENDManual_DispatchSima` -- if(EnableSIMA) ExecuteSmartDispatchEntry("TREND_MNL",...)

**Acceptance criteria**:
- Compiles clean. ASCII gate passes. Zero lock() calls.
- Parent CC drops below 50. No new public methods.

---

### T3: Extract CreateTRENDPosition

**File**: `src/V12_002.Entries.Trend.cs`
**Line range**: 291-344
**Handlers**:
- `CreateTRENDPosition_CalculateTargets` -- CalculateTargetPrice x5, GetTargetDistribution, Print
- `CreateTRENDPosition_BuildInfo` -- new PositionInfo { all fields }, return tPos

**Acceptance criteria**:
- Compiles clean. ASCII gate passes. Zero lock() calls.
- Parent CC drops below 50. No new public methods.

---

## 4. VERIFICATION SEQUENCE (FINAL STEP - after ALL tickets)

```text
1. Self-audit:
   - grep src/ for lock(  -- must be zero hits
   - check_ascii.py or ASCII scan -- must pass
2. powershell -File .\deploy-sync.ps1  -- must EXIT 0
3. dotnet build .\Linting.csproj       -- must succeed with zero new errors
4. dotnet test .\Testing.csproj        -- must pass all tests
5. Report all results before session ends.
```

## 5. DIRECTOR'S HANDOFF BLOCK (For P5 ENGINEER - Codex)

```text
@ENGINEER (Codex) - P5 Surgical Execution

Execute tickets T1, T2, T3 IN ORDER from src/V12_002.Entries.Trend.cs.
Complete and verify each ticket before starting the next.
After each ticket confirm: compiles? ASCII clean? Zero new lock() calls?
CC reduced? No new public methods?

See Section 3 for precise line ranges, handler names, and constraints.
After all three tickets: run full verification sequence from Section 4.
```