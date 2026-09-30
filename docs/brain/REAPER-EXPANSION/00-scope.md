# REAPER-EXPANSION Epic — Phase 1: INTAKE
**Epic ID**: REAPER-EXPANSION  
**Protocol**: V12 Photon Kernel (Phase 6 Recursive)  
**Current BUILD_TAG**: `1111.007-reaper-t2`  
**Date**: 2026-05-21  
**Agent**: Plan Mode (v12-epic-planner)

---

## MISSION BRIEF

Expand the REAPER safety layer across the remaining strategy subgraphs (SIMA, IPC, Entries). Mandatory integration of Greptile MCP for semantic regression testing and Jane Street Atomic Unification for low-latency performance. Target: 100/100 PHS (Perfect Health Score) on every ticket.

---

## FORENSIC CONTEXT

### Current REAPER Architecture (Build 1111.007-reaper-t2)

The REAPER safety system was successfully extracted in the REAPER-EXTRACT epic into dedicated modules:

1. **[`V12_002.REAPER.cs`](../../src/V12_002.REAPER.cs)** (163 LOC)
   - Timer infrastructure (`StartReaperAudit`, `StopReaperAudit`, `OnReaperTimerElapsed`)
   - Shared state dictionaries (grace tracking, in-flight guards)
   - Helper methods (`StampAccountFillGrace`, `IsReaperFillGraceActive`, `TryGetRepairDistanceLimitPoints`)

2. **[`V12_002.REAPER.Audit.cs`](../../src/V12_002.REAPER.Audit.cs)** (979 LOC)
   - Main audit orchestrator (`AuditApexPositions`)
   - Fleet account audit (`AuditSingleFleetAccount` + 6 extracted helpers)
   - Master account audit (`AuditMasterAccountIfNeeded` + 3 extracted helpers)
   - Desync detection and flatten queue processing

3. **[`V12_002.REAPER.Repair.cs`](../../src/V12_002.REAPER.Repair.cs)** (269 LOC)
   - Ghost position repair engine (`ProcessReaperRepairQueue`, `ExecuteReaperRepair`)
   - Repair validation chain (4 extracted helpers)

4. **[`V12_002.REAPER.NakedStop.cs`](../../src/V12_002.REAPER.NakedStop.cs)** (84 LOC)
   - Emergency stop submission (`ProcessReaperNakedStopQueue`)
   - ATR-bounded stop price calculation

5. **[`V12_002.REAPER.NakedPosition.cs`](../../src/V12_002.REAPER.NakedPosition.cs)** (NEW - Build 1111.007-reaper-t1)
   - Naked position detection with 5s grace period
   - Stop-replace suppression logic
   - Emergency stop enqueue with in-flight guard
   - TOCTOU race fixed via atomic `GetOrAdd`

6. **[`V12_002.REAPER.OrphanSafety.cs`](../../src/V12_002.REAPER.OrphanSafety.cs)** (NEW - Build 1111.007-reaper-t2)
   - Orphan FSM detection with 10s grace period
   - 3-attempt self-heal threshold
   - Force-zero logic for sustained failures

### Current Coverage Gaps

**REAPER currently monitors**:
- ✅ Fleet account positions (broker vs. expected)
- ✅ Master account positions (broker vs. expected)
- ✅ Naked positions (missing stop orders)
- ✅ Orphan FSMs (broker flat but activePositions entry exists)
- ✅ Ghost positions (desync repair)

**REAPER does NOT monitor**:
- ❌ **SIMA Dispatch State** (fleet dispatch queue, in-flight guards, symmetry context)
- ❌ **IPC Command State** (command queue depth, stale commands, malformed payloads)
- ❌ **Entry Signal State** (entry mode transitions, signal staleness, duplicate signals)

---

## EXPANSION SCOPE

### Target Subgraphs

#### 1. SIMA (Single-Instance Multi-Account) Subgraph
**Files**: `V12_002.SIMA.*.cs` (7 files, ~2000 LOC)

**Safety Gaps Identified**:

1. **Fleet Dispatch Queue Overflow** (`V12_002.SIMA.Dispatch.cs`)
   - **Risk**: `_fleetDispatchQueue` (ConcurrentQueue) has no depth limit
   - **Scenario**: Rapid signal generation + slow broker submission → unbounded memory growth
   - **Detection**: Queue depth monitoring with configurable threshold (default 100)
   - **Response**: Log warning, reject new dispatches, trigger emergency flatten if threshold exceeded

2. **Stale Dispatch Detection** (`V12_002.SIMA.Dispatch.cs:72`)
   - **Risk**: `FleetDispatchRequest.SignalTicks` timestamp not validated before submission
   - **Scenario**: Deferred dispatch executes after market conditions change (e.g., 5+ seconds old)
   - **Detection**: Compare `DateTime.UtcNow.Ticks - SignalTicks` against configurable threshold (default 3s)
   - **Response**: Log warning, skip stale dispatch, clear in-flight guard

3. **Symmetry Context Corruption** (`V12_002.SIMA.cs:76-94`)
   - **Risk**: `expectedPositions` mutation via `AddOrUpdate` is atomic, but cross-account consistency not verified
   - **Scenario**: Partial fleet dispatch (3 of 5 accounts succeed) leaves asymmetric expected positions
   - **Detection**: Audit `expectedPositions` sum across fleet accounts vs. master account
   - **Response**: Log diagnostic, trigger repair if delta exceeds threshold

4. **SIMA Toggle Gate Leak** (`V12_002.SIMA.Dispatch.cs:49`)
   - **Risk**: `_simaToggleState` (Interlocked gate) not released if exception occurs before `finally` block
   - **Scenario**: Exception in `Dispatch_ValidatePreconditions` → gate remains locked → all future dispatches rejected
   - **Detection**: Monitor consecutive dispatch rejections (threshold: 5)
   - **Response**: Force-reset gate via `Interlocked.Exchange(ref _simaToggleState, 0)`

#### 2. IPC (Inter-Process Communication) Subgraph
**Files**: `V12_002.UI.IPC.*.cs` (6 files, ~1500 LOC)

**Safety Gaps Identified**:

1. **IPC Command Queue Depth** (`V12_002.UI.IPC.cs:46`)
   - **Risk**: `ipcQueuedCommandCount` tracks depth but no enforcement mechanism
   - **Scenario**: Malicious/buggy client floods TCP socket → unbounded queue growth
   - **Detection**: Monitor `ipcQueuedCommandCount` against `IpcMaxQueueDepth` (2000)
   - **Response**: Reject new commands, log warning, disconnect offending client

2. **Stale IPC Command Detection** (`V12_002.UI.IPC.Server.cs`)
   - **Risk**: Commands enqueued but not processed for extended period (e.g., strategy paused)
   - **Scenario**: Resume from pause → execute 100+ stale commands → unexpected state mutations
   - **Detection**: Timestamp each command at enqueue, check age before execution (threshold: 10s)
   - **Response**: Log warning, skip stale commands, send NACK to client

3. **Malformed Payload Handling** (`V12_002.UI.IPC.cs:48`)
   - **Risk**: `_ipcInvalidUtf8Count` tracks invalid UTF-8 but no circuit breaker
   - **Scenario**: Sustained invalid payloads → CPU waste on parsing failures
   - **Detection**: Monitor `_ipcInvalidUtf8Count` rate (threshold: 10/second)
   - **Response**: Disconnect client, log security event

4. **Allowlist Bypass Detection** (`V12_002.UI.IPC.cs:49`)
   - **Risk**: `_ipcAllowlistRejectCount` tracks rejections but no anomaly detection
   - **Scenario**: Attacker probes for undocumented commands
   - **Detection**: Monitor `_ipcAllowlistRejectCount` rate (threshold: 20/minute)
   - **Response**: Disconnect client, log security event, optional IP ban

#### 3. Entries Subgraph
**Files**: `V12_002.Entries.*.cs` (7 files, ~800 LOC)

**Safety Gaps Identified**:

1. **Entry Mode Transition Validation** (All `Entries.*.cs` files)
   - **Risk**: No validation that entry mode matches current strategy state
   - **Scenario**: RMA entry fires while strategy is in TREND mode → unexpected bracket configuration
   - **Detection**: Audit entry mode against `CurrentEntryMode` property before execution
   - **Response**: Log warning, reject entry, prevent order submission

2. **Duplicate Signal Suppression** (All `Entries.*.cs` files)
   - **Risk**: No deduplication of rapid-fire entry signals (e.g., OR_LONG called 3x in 100ms)
   - **Scenario**: Network latency + retry logic → duplicate bracket submissions
   - **Detection**: Track last signal timestamp per entry mode (grace period: 500ms)
   - **Response**: Log warning, suppress duplicate, preserve first signal only

3. **Signal Staleness Detection** (All `Entries.*.cs` files)
   - **Risk**: Entry signals generated on bar N-1 but executed on bar N+5 (e.g., during pause)
   - **Scenario**: Resume from pause → execute stale signals → enter at unfavorable price
   - **Detection**: Compare signal generation timestamp against current bar timestamp (threshold: 3 bars)
   - **Response**: Log warning, reject stale signal

4. **Entry Quantity Validation** (All `Entries.*.cs` files)
   - **Risk**: No validation that entry quantity matches configured position size
   - **Scenario**: Manual override + IPC command → submit 10ct entry when configured for 1ct
   - **Detection**: Audit entry quantity against `PositionSize` property before submission
   - **Response**: Log warning, clamp to configured size, prevent over-leverage

---

## EXTRACTION CONSTRAINTS

### V12 DNA Compliance (NON-NEGOTIABLE)

1. **Lock-Free Actor Pattern**: Zero `lock()` statements. All state mutations via `ConcurrentDictionary` atomic operations or `Enqueue` model.
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

**Application to REAPER-EXPANSION**:
- Queue depth checks use atomic reads (no blocking)
- Timestamp comparisons use `DateTime.UtcNow` (monotonic, no syscall blocking)
- In-flight guards use `TryAdd` (atomic CAS operation, wait-free)
- All audit logic marshalled via `TriggerCustomEvent` (bounded latency, strategy thread execution)

### Greptile MCP Integration (NEW REQUIREMENT)

**Reference**: `docs/brain/greptile_integration_manual.md`

**Mandatory Integration Points**:

1. **Pre-Implementation Scan** (Phase 2: PLAN)
   - Tool: `greptile query_repository`
   - Query: "What are the current safety gaps in [SIMA|IPC|Entries] subgraph?"
   - Output: Semantic analysis of existing code patterns, potential race conditions

2. **Post-Implementation Review** (Phase 5: VERIFICATION)
   - Tool: `greptile search_repository`
   - Query: "Find all usages of [new safety pattern] across codebase"
   - Output: Cross-module impact analysis, regression risk assessment

3. **PR Loop Automation** (Phase 6: SIGN-OFF)
   - Command: `/greploop <PR_NUMBER>`
   - Goal: Achieve 5/5 confidence score with zero critical findings
   - Loop: Iterate until 100/100 PHS achieved

**Greptile Configuration** (`greptile.json`):
```json
{
  "instructions": "Enforce V12 REAPER-EXPANSION standards. BANNED: lock(stateLock), Unicode in strings, non-atomic queue depth checks. MANDATORY: ASCII Gate, Atomic CAS operations, Bounded latency.",
  "rules": [
    {
      "id": "reaper-expansion-atomic",
      "rule": "All REAPER safety checks must use atomic operations (CAS, volatile, Interlocked).",
      "severity": "critical"
    },
    {
      "id": "reaper-expansion-bounded",
      "rule": "All REAPER audit cycles must complete in < 100ms per account.",
      "severity": "critical"
    },
    {
      "id": "reaper-expansion-ascii",
      "rule": "All diagnostic logging must use ASCII-only string literals.",
      "severity": "critical"
    }
  ]
}
```

---

## RISK ASSESSMENT

### CRITICAL RISKS

1. **SIMA Dispatch Queue Overflow**
   - **Location**: `V12_002.SIMA.Dispatch.cs` (unbounded `ConcurrentQueue`)
   - **Risk**: Rapid signal generation → OOM crash
   - **Mitigation**: Depth monitoring + rejection threshold + emergency flatten
   - **Severity**: P1 (production crash risk)

2. **IPC Command Queue Flood**
   - **Location**: `V12_002.UI.IPC.cs:46` (no enforcement of `IpcMaxQueueDepth`)
   - **Risk**: Malicious client → CPU saturation + memory exhaustion
   - **Mitigation**: Depth enforcement + client disconnect + rate limiting
   - **Severity**: P1 (security + stability risk)

3. **Stale Dispatch Execution**
   - **Location**: `V12_002.SIMA.Dispatch.cs:72` (no timestamp validation)
   - **Risk**: Execute 5-second-old dispatch → enter at stale price → immediate loss
   - **Mitigation**: Timestamp validation + configurable threshold + skip logic
   - **Severity**: P2 (financial risk, low probability)

### MEDIUM RISKS

4. **Symmetry Context Asymmetry**
   - **Location**: `V12_002.SIMA.cs:76-94` (partial fleet dispatch)
   - **Risk**: 3 of 5 accounts succeed → asymmetric expected positions → desync
   - **Mitigation**: Cross-account audit + repair trigger
   - **Severity**: P3 (self-healing logic present, rare trigger)

5. **Entry Mode Mismatch**
   - **Location**: All `Entries.*.cs` files (no mode validation)
   - **Risk**: RMA entry fires in TREND mode → unexpected bracket config
   - **Mitigation**: Mode validation + rejection logic
   - **Severity**: P3 (user error, non-fatal)

---

## EXTRACTION DEPENDENCIES

### Internal Dependencies (V12 Codebase)

1. **REAPER Infrastructure** (`V12_002.REAPER.cs`)
   - Used by: All new safety modules (timer, grace helpers, logging)
   - **Action**: Extend with new accessor methods (no breaking changes)

2. **SIMA State** (`expectedPositions`, `_fleetDispatchQueue`, `_simaToggleState`)
   - Used by: SIMA safety module
   - **Action**: Snapshot before iteration (defensive copy pattern)

3. **IPC State** (`ipcQueuedCommandCount`, `_ipcInvalidUtf8Count`, `_ipcAllowlistRejectCount`)
   - Used by: IPC safety module
   - **Action**: Atomic reads via `Volatile.Read` or `Interlocked.Read`

4. **Entry State** (`CurrentEntryMode`, last signal timestamps)
   - Used by: Entries safety module
   - **Action**: New state dictionaries (per-mode tracking)

### External Dependencies (NinjaTrader API)

1. **`TriggerCustomEvent`** (strategy thread marshalling)
   - Used by: All queue processors
   - **Action**: Preserve existing pattern (no changes)

2. **`DateTime.UtcNow`** (timestamp generation)
   - Used by: Staleness detection
   - **Action**: Preserve existing pattern (no changes)

### External Dependencies (Greptile MCP)

1. **`greptile query_repository`** (semantic analysis)
   - Used by: Phase 2 (PLAN)
   - **Action**: New tool integration (see Greptile section)

2. **`greptile search_repository`** (cross-module search)
   - Used by: Phase 5 (VERIFICATION)
   - **Action**: New tool integration (see Greptile section)

3. **`/greploop`** (autonomous PR loop)
   - Used by: Phase 6 (SIGN-OFF)
   - **Action**: New workflow integration (see Greptile section)

---

## SUCCESS CRITERIA

### Functional Requirements

1. **SIMA Safety**
   - [ ] Fleet dispatch queue depth monitoring (threshold: 100)
   - [ ] Stale dispatch detection (threshold: 3s)
   - [ ] Symmetry context audit (cross-account consistency)
   - [ ] SIMA toggle gate leak detection (threshold: 5 consecutive rejections)

2. **IPC Safety**
   - [ ] Command queue depth enforcement (max: 2000)
   - [ ] Stale command detection (threshold: 10s)
   - [ ] Malformed payload circuit breaker (threshold: 10/second)
   - [ ] Allowlist bypass detection (threshold: 20/minute)

3. **Entries Safety**
   - [ ] Entry mode validation (reject mismatched modes)
   - [ ] Duplicate signal suppression (grace: 500ms)
   - [ ] Signal staleness detection (threshold: 3 bars)
   - [ ] Entry quantity validation (clamp to configured size)

### Non-Functional Requirements

4. **Performance**
   - [ ] Zero new heap allocations on hot path
   - [ ] Audit cycle time unchanged (< 100ms per account)
   - [ ] Safety check latency < 10ms (P99)

5. **Maintainability**
   - [ ] New safety modules < 200 LOC each
   - [ ] Cyclomatic complexity < 20 per method
   - [ ] Zero code duplication between modules

6. **Safety**
   - [ ] Zero `lock()` statements in new modules
   - [ ] All state mutations atomic (CAS or queue-based)
   - [ ] ASCII-only compliance verified

7. **Greptile Integration**
   - [ ] Pre-implementation semantic scan completed
   - [ ] Post-implementation cross-module search completed
   - [ ] PR loop achieves 100/100 PHS (5/5 confidence, zero critical findings)

---

## OPEN QUESTIONS FOR DIRECTOR

1. **Module Naming Convention**
   - Proposed: `V12_002.REAPER.SIMA.cs`, `V12_002.REAPER.IPC.cs`, `V12_002.REAPER.Entries.cs`
   - Alternative: `V12_002.Safety.SIMA.cs`, `V12_002.Safety.IPC.cs`, `V12_002.Safety.Entries.cs` (new namespace)
   - **Recommendation**: Keep `REAPER.*` namespace for consistency with existing modules

2. **Threshold Configuration**
   - Current: All thresholds hardcoded (e.g., 100 dispatch queue depth, 3s staleness)
   - **Question**: Should thresholds be user-configurable via NinjaScript properties?
   - **Recommendation**: Start hardcoded, expose as properties in Phase 2 if Director requests

3. **Greptile API Key Management**
   - Current: API key stored in environment variable (`%GREPTILE_API_KEY%`)
   - **Question**: Should we use a more secure key management system (e.g., Azure Key Vault)?
   - **Recommendation**: Environment variable is acceptable for V12 (single-user, local dev)

4. **Emergency Flatten Trigger**
   - Current: REAPER can trigger flatten via `EnqueueReaperFlattenCandidate`
   - **Question**: Should SIMA/IPC/Entries safety modules also have flatten authority?
   - **Recommendation**: Yes, but only for P1 severity violations (queue overflow, security breach)

5. **Audit Cycle Frequency**
   - Current: REAPER audit runs every 1000ms (configurable via `ReaperIntervalMs`)
   - **Question**: Should SIMA/IPC/Entries safety checks run on same cycle or independent?
   - **Recommendation**: Same cycle (piggyback on existing timer) to minimize overhead

---

## NEXT STEPS

**[INTAKE-GATE]**

Director, this scope document defines the expansion boundaries for REAPER-EXPANSION. Key decisions:

1. **Three new modules**: `V12_002.REAPER.SIMA.cs` (fleet dispatch safety), `V12_002.REAPER.IPC.cs` (command queue safety), `V12_002.REAPER.Entries.cs` (entry signal safety)
2. **Preserve existing infrastructure**: Timer, audit orchestrators, existing REAPER modules remain unchanged
3. **Zero behavioral change**: All existing safety logic preserved exactly
4. **Jane Street alignment**: Atomic state transitions, wait-free progress, bounded latency verified
5. **Greptile integration**: Mandatory semantic analysis, cross-module search, autonomous PR loop

**Does this scope match your intent?**

Reply **YES** to proceed to Phase 2 (PLAN), or provide corrections.