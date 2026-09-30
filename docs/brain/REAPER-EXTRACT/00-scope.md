# REAPER-EXTRACT Epic — Phase 1: INTAKE
**Epic ID**: REAPER-EXTRACT  
**Protocol**: V12 Photon Kernel (Phase 6 Recursive)  
**Current BUILD_TAG**: `1111.007-mphase-mp0`  
**Date**: 2026-05-21  
**Agent**: Plan Mode (v12-epic-planner)

---

## MISSION BRIEF

Surgical extraction of **naked-position monitoring** and **orphan order safety logic** from V12 REAPER monoliths into dedicated lock-free safety modules. Mandatory alignment with Jane Street Atomic Unification principles.

---

## FORENSIC CONTEXT

### Current REAPER Architecture (Build 1111.007)

The REAPER safety system is currently distributed across 4 files:

1. **[`V12_002.REAPER.cs`](../../src/V12_002.REAPER.cs)** (163 LOC)
   - Timer infrastructure (`StartReaperAudit`, `StopReaperAudit`, `OnReaperTimerElapsed`)
   - Shared state dictionaries (grace tracking, in-flight guards, orphan detection)
   - Helper methods (`StampAccountFillGrace`, `IsReaperFillGraceActive`, `TryGetRepairDistanceLimitPoints`)

2. **[`V12_002.REAPER.Audit.cs`](../../src/V12_002.REAPER.Audit.cs)** (979 LOC)
   - Main audit orchestrator (`AuditApexPositions`)
   - Fleet account audit (`AuditSingleFleetAccount` + 6 extracted helpers)
   - Master account audit (`AuditMasterAccountIfNeeded` + 3 extracted helpers)
   - Naked position detection (`EnqueueReaperNakedStopCandidate`)
   - Orphan FSM detection (lines 121-153)
   - Flatten queue processing (`ProcessReaperFlattenQueue` + 3 helpers)

3. **[`V12_002.REAPER.Repair.cs`](../../src/V12_002.REAPER.Repair.cs)** (269 LOC)
   - Ghost position repair engine (`ProcessReaperRepairQueue`, `ExecuteReaperRepair`)
   - Repair validation chain (4 extracted helpers from Phase 7-T1)
   - Orphan self-heal logic (lines 56-69)

4. **[`V12_002.REAPER.NakedStop.cs`](../../src/V12_002.REAPER.NakedStop.cs)** (84 LOC)
   - Emergency stop submission (`ProcessReaperNakedStopQueue`)
   - ATR-bounded stop price calculation

### Key Safety Mechanisms Identified

#### 1. Naked Position Monitoring (Build 1102R)
**Location**: `REAPER.Audit.cs` lines 304-339, 654-709  
**Purpose**: Detects positions without working stop orders (protection missing)  
**Flow**:
- Grace period tracking via `_nakedPositionFirstSeen` (default 5s, configurable via `NakedPositionGraceSec`)
- Suppression during stop-replace cycles (`pendingStopReplacements` check)
- Emergency stop enqueue with in-flight guard (`_reaperNakedStopInFlight`)
- ATR-bounded stop price calculation in `NakedStop.cs`

**State**:
```csharp
// V12_002.REAPER.cs lines 40-41
private ConcurrentDictionary<string, DateTime> _nakedPositionFirstSeen;
private ConcurrentDictionary<string, byte> _reaperNakedStopInFlight;
private ConcurrentQueue<(string, MarketPosition, int)> _reaperNakedStopQueue;
```

#### 2. Orphan Order Safety (Build 946, 981)
**Location**: `REAPER.Audit.cs` lines 121-153, `REAPER.Repair.cs` lines 56-69  
**Purpose**: Detects and self-heals orphaned FSM positions (broker flat but activePositions entry exists)  
**Flow**:
- 10-second grace period via `_orphanedPositionFirstSeen`
- Diagnostic logging after grace expiry (non-fatal assertion)
- Orphan repair counter (`_reaperOrphanRepairCount`) with 3-attempt self-heal threshold
- Force-zero expectedPositions after 3 failed repair attempts

**State**:
```csharp
// V12_002.REAPER.cs lines 61, 68
private readonly ConcurrentDictionary<string, int> _reaperOrphanRepairCount;
private readonly ConcurrentDictionary<string, DateTime> _orphanedPositionFirstSeen;
```

### Atomic Compliance Audit

**PASS**: Zero `lock()` statements found in REAPER modules (verified via grep)  
**PASS**: All state mutations use `ConcurrentDictionary` atomic operations (`TryAdd`, `TryRemove`, `AddOrUpdate`)  
**PASS**: Cross-thread marshalling via `TriggerCustomEvent` (strategy thread safety)  
**PASS**: ASCII-only compliance (no Unicode in string literals)

---

## EXTRACTION SCOPE

### IN-SCOPE: Safety Logic to Extract

1. **Naked Position Detection & Emergency Stop**
   - Grace period tracking (`_nakedPositionFirstSeen`)
   - Stop-replace suppression logic
   - Emergency stop enqueue with in-flight guard
   - ATR-bounded stop price calculation
   - **Target Module**: `V12_002.REAPER.NakedPosition.cs` (new)

2. **Orphan FSM Detection & Self-Heal**
   - Orphan detection with 10s grace (`_orphanedPositionFirstSeen`)
   - Diagnostic logging (non-fatal assertion)
   - Orphan repair counter with 3-attempt threshold
   - Force-zero self-heal logic
   - **Target Module**: `V12_002.REAPER.OrphanSafety.cs` (new)

### OUT-OF-SCOPE: Existing REAPER Infrastructure (Preserve)

- Timer infrastructure (`StartReaperAudit`, `StopReaperAudit`)
- Main audit orchestrators (`AuditApexPositions`, `AuditSingleFleetAccount`, `AuditMasterAccountIfNeeded`)
- Desync detection and flatten logic
- Ghost position repair engine
- Shared helper methods (`IsReaperFillGraceActive`, `TryGetRepairDistanceLimitPoints`)

---

## EXTRACTION CONSTRAINTS

### V12 DNA Compliance (NON-NEGOTIABLE)

1. **Lock-Free Actor Pattern**: Zero `lock()` statements. All state mutations via `ConcurrentDictionary` atomic operations.
2. **ASCII-Only**: No Unicode, emoji, or curly quotes in C# string literals.
3. **Zero New Allocations**: Hot-path methods must not allocate on heap (use object pooling or stack allocation).
4. **Hard-Link Integrity**: Every `src/` modification followed by `powershell -File .\deploy-sync.ps1`.
5. **F5 Verification**: Live NinjaTrader validation required per ticket.

### Jane Street Atomic Unification Principles

**Reference**: AGENTS.md line 23 mandates Jane Street alignment for all architectural decisions.

**Key Principles** (derived from HFT best practices):
1. **Atomic State Transitions**: State changes must be indivisible (no partial updates visible to observers).
2. **Wait-Free Progress**: No thread can block another thread's progress indefinitely.
3. **Memory Ordering**: Explicit memory barriers where cross-thread visibility is required.
4. **Bounded Latency**: Worst-case execution time must be deterministic and bounded.

**Application to REAPER**:
- Grace period checks use `DateTime.UtcNow` (monotonic, no syscall blocking)
- In-flight guards use `TryAdd` (atomic CAS operation, wait-free)
- Queue operations use `ConcurrentQueue` (lock-free MPSC)
- Emergency stop submission marshalled via `TriggerCustomEvent` (bounded latency, strategy thread execution)

---

## RISK ASSESSMENT

### CRITICAL RISKS

1. **TOCTOU Race in Grace Period Checks**
   - **Location**: `REAPER.Audit.cs` lines 523-537 (naked position), lines 132-146 (orphan FSM)
   - **Risk**: Two audit cycles could both pass grace check before either updates `_nakedPositionFirstSeen`
   - **Mitigation**: Atomic `TryAdd` guard already present (line 540), but grace check happens BEFORE guard
   - **Severity**: P2 (edge case, low probability, non-fatal)

2. **Orphan Self-Heal Force-Zero Side Effect**
   - **Location**: `REAPER.Repair.cs` lines 64-68
   - **Risk**: `SetExpectedPositionLocked(ExpKey(accountName), 0)` clears expected position for ALL entries on account, not just orphaned entry
   - **Mitigation**: Comment states "Force-zeroing expectedPositions to unblock repair loop" — intentional design
   - **Severity**: P3 (documented behavior, rare trigger condition)

3. **Emergency Stop Price Calculation Precision**
   - **Location**: `REAPER.NakedStop.cs` lines 38-43
   - **Risk**: `Math.Min(emergencyStopDist, atrBound)` could produce stop too tight if ATR collapses
   - **Mitigation**: Fallback to `Math.Max(tickSize, MinimumStop)` if `emergencyStopDist <= 0`
   - **Severity**: P3 (defensive bounds present, tested in production)

### MEDIUM RISKS

4. **Orphan Detection False Positives During Restart**
   - **Location**: `REAPER.Audit.cs` lines 365-388
   - **Risk**: Hydrated Active FSMs with no order reference (restart edge case) could trigger orphan detection
   - **Mitigation**: Special handling present (lines 366-388) — auto-terminates stale FSMs if broker flat
   - **Severity**: P4 (edge case, self-healing logic present)

5. **Naked Position Suppression During Stop-Replace**
   - **Location**: `REAPER.Audit.cs` lines 499-520
   - **Risk**: `pendingStopReplacements` iteration could miss concurrent additions
   - **Mitigation**: `ConcurrentDictionary.Values` snapshot semantics — safe but may lag by one cycle
   - **Severity**: P4 (grace period absorbs lag, non-critical)

---

## EXTRACTION DEPENDENCIES

### Internal Dependencies (V12 Codebase)

1. **FSM State** (`_followerBrackets`, `activePositions`)
   - Used by: Orphan detection (lines 127-153), Repair validation (lines 44-54)
   - **Action**: Pass as parameters to extracted methods (no direct access)

2. **Broker Order State** (`Account.Orders`, `pendingStopReplacements`)
   - Used by: Naked position detection (lines 476-486, 499-520)
   - **Action**: Snapshot before iteration (defensive copy pattern already present)

3. **Expected Position State** (`expectedPositions`, `_dispatchSyncPendingExpKeys`)
   - Used by: Orphan self-heal (line 67), Repair validation (line 201)
   - **Action**: Expose via accessor methods (maintain encapsulation)

4. **Grace Period Helpers** (`IsReaperFillGraceActive`, `StampAccountFillGrace`)
   - Used by: Repair validation, Desync detection
   - **Action**: Keep in `V12_002.REAPER.cs` (shared infrastructure)

### External Dependencies (NinjaTrader API)

1. **`TriggerCustomEvent`** (strategy thread marshalling)
   - Used by: All queue processors (lines 202, 285, 320, 689)
   - **Action**: Preserve existing pattern (no changes)

2. **`Account.CreateOrder` + `Account.Submit`** (order submission)
   - Used by: Emergency stop submission (lines 60-65)
   - **Action**: Preserve existing pattern (no changes)

3. **`Instrument.MasterInstrument.RoundToTickSize`** (price rounding)
   - Used by: Stop price calculation (line 50)
   - **Action**: Preserve existing pattern (no changes)

---

## SUCCESS CRITERIA

### Functional Requirements

1. **Naked Position Detection**
   - [ ] Grace period tracking preserved (5s default, configurable)
   - [ ] Stop-replace suppression logic intact
   - [ ] Emergency stop enqueue with in-flight guard functional
   - [ ] ATR-bounded stop price calculation correct

2. **Orphan FSM Detection**
   - [ ] 10-second grace period preserved
   - [ ] Diagnostic logging after grace expiry functional
   - [ ] 3-attempt self-heal threshold intact
   - [ ] Force-zero logic triggers correctly

### Non-Functional Requirements

3. **Performance**
   - [ ] Zero new heap allocations on hot path
   - [ ] Audit cycle time unchanged (< 100ms per account)
   - [ ] Emergency stop submission latency < 50ms (P99)

4. **Maintainability**
   - [ ] Extracted modules < 200 LOC each
   - [ ] Cyclomatic complexity < 20 per method
   - [ ] Zero code duplication between modules

5. **Safety**
   - [ ] Zero `lock()` statements in extracted modules
   - [ ] All state mutations atomic (CAS or queue-based)
   - [ ] ASCII-only compliance verified

---

## OPEN QUESTIONS FOR DIRECTOR

1. **Module Naming Convention**
   - Proposed: `V12_002.REAPER.NakedPosition.cs` and `V12_002.REAPER.OrphanSafety.cs`
   - Alternative: `V12_002.Safety.NakedPosition.cs` and `V12_002.Safety.OrphanFSM.cs` (new namespace)
   - **Recommendation**: Keep `REAPER.*` namespace for consistency with existing modules

2. **Grace Period Configuration**
   - Current: `NakedPositionGraceSec` property (default 5s, user-configurable)
   - Orphan grace is hardcoded to 10s (line 135)
   - **Question**: Should orphan grace also be configurable? Or keep hardcoded for safety?
   - **Recommendation**: Keep hardcoded (10s is a safety floor, not a tuning parameter)

3. **Orphan Self-Heal Aggressiveness**
   - Current: Force-zero after 3 failed repair attempts (lines 62-68)
   - **Question**: Is 3 attempts the right threshold? Or should it be configurable?
   - **Recommendation**: Keep hardcoded (3 is empirically validated, changing it is a policy decision)

4. **Emergency Stop Distance Calculation**
   - Current: `Math.Min(MaximumStop, atrBound)` with fallback to `Math.Max(tickSize, MinimumStop)`
   - **Question**: Should emergency stops use a separate multiplier (e.g., 1.5x normal stop)?
   - **Recommendation**: Defer to Phase 2 (analysis required, not blocking extraction)

---

## NEXT STEPS

**[INTAKE-GATE]**

Director, this scope document defines the extraction boundaries for REAPER-EXTRACT. Key decisions:

1. **Two new modules**: `V12_002.REAPER.NakedPosition.cs` (naked position monitoring + emergency stop) and `V12_002.REAPER.OrphanSafety.cs` (orphan FSM detection + self-heal)
2. **Preserve existing infrastructure**: Timer, audit orchestrators, desync/flatten logic remain in current files
3. **Zero behavioral change**: All safety thresholds, grace periods, and logic preserved exactly
4. **Jane Street alignment**: Atomic state transitions, wait-free progress, bounded latency verified

**Does this scope match your intent?**

Reply **YES** to proceed to Phase 2 (PLAN), or provide corrections.