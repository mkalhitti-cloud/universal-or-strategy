# REAPER-EXTRACT Epic — Phase 2: ANALYSIS
**Epic ID**: REAPER-EXTRACT  
**Protocol**: V12 Photon Kernel (Phase 6 Recursive)  
**Date**: 2026-05-21  
**Agent**: Plan Mode (v12-epic-planner)

---

## COMPLEXITY METRICS

### Current State (Pre-Extraction)

| Method | File | LOC | CYC | Max Nesting | Status |
|:---|:---|---:|---:|---:|:---|
| `EnqueueReaperNakedStopCandidate` | REAPER.Audit.cs:488 | 72 | 8 | 4 | **EXTRACT** |
| `AuditFleet_HandleNakedPosition` | REAPER.Audit.cs:304 | 36 | 4 | 3 | Keep (orchestrator) |
| `ProcessReaperNakedStopQueue` | REAPER.NakedStop.cs:21 | 60 | 5 | 2 | **EXTRACT** |
| `ValidateRepairEligibility` | REAPER.Repair.cs:31 | 46 | 6 | 3 | **EXTRACT** (orphan logic) |
| `AuditSingleFleetAccount` | REAPER.Audit.cs:62 | 100 | 7 | 2 | Keep (orchestrator) |

**Extraction Targets**:
1. **Naked Position Detection** (72 LOC, CYC 8) — Grace tracking + stop-replace suppression + enqueue logic
2. **Emergency Stop Submission** (60 LOC, CYC 5) — ATR-bounded stop price calculation + order submission
3. **Orphan Self-Heal** (46 LOC, CYC 6) — 3-attempt counter + force-zero logic

**Post-Extraction Target**:
- All extracted methods: CYC ≤ 10 (well below 20 threshold)
- Orchestrator methods: CYC ≤ 5 (pure coordinators)

---

## RISK HOTSPOTS

### CRITICAL RISKS

#### 1. TOCTOU Race in Naked Position Grace Check
**Location**: `REAPER.Audit.cs:525-537`  
**Severity**: P2 (Low probability, non-fatal)

**Current Flow**:
```csharp
// Line 525: Read grace timestamp (non-atomic)
if (!_nakedPositionFirstSeen.TryGetValue(acct.Name, out firstSeen))
{
    _nakedPositionFirstSeen[acct.Name] = DateTime.UtcNow;  // Line 527: Write timestamp
}
else if ((DateTime.UtcNow - firstSeen).TotalSeconds >= graceSeconds)  // Line 537: Check elapsed
{
    if (!_reaperNakedStopInFlight.TryAdd(expectedKey, 0))  // Line 540: Atomic guard
```

**Race Condition**:
1. Audit Cycle A reads `_nakedPositionFirstSeen` at T=0 (line 525) → miss (no entry)
2. Audit Cycle B reads `_nakedPositionFirstSeen` at T=0.1ms (line 525) → miss (no entry)
3. Cycle A writes timestamp at T=0.2ms (line 527)
4. Cycle B writes timestamp at T=0.3ms (line 527) → **overwrites A's timestamp**
5. Both cycles proceed to grace check on next iteration

**Impact**: Grace period could be extended by up to one audit interval (default 1000ms). Non-fatal — position remains naked for extra 1s, but emergency stop still fires eventually.

**Mitigation Strategy**:
- Use `TryAdd` instead of direct indexer write (line 527)
- If `TryAdd` fails, read existing timestamp and proceed to grace check
- Atomic pattern: `_nakedPositionFirstSeen.GetOrAdd(acct.Name, DateTime.UtcNow)`

**Extraction Impact**: This fix should be applied DURING extraction to the new module.

---

#### 2. Orphan Self-Heal Scope Blast
**Location**: `REAPER.Repair.cs:67`  
**Severity**: P3 (Documented behavior, rare trigger)

**Current Code**:
```csharp
if (orphanCount >= 3)
{
    Print(string.Format("[REAPER] SELF-HEAL: {0} has no PositionInfo after 3 attempts. Force-zeroing expectedPositions to unblock repair loop.",
        accountName));
    SetExpectedPositionLocked(ExpKey(accountName), 0);  // Line 67: Clears ALL entries for account
    _reaperOrphanRepairCount.TryRemove(accountName, out _);
}
```

**Issue**: `SetExpectedPositionLocked(ExpKey(accountName), 0)` clears the expected position for the ENTIRE account, not just the orphaned entry. If the account has multiple active positions (e.g., two separate entries), this force-zero will affect all of them.

**Forensic Analysis**:
- `ExpKey(accountName)` returns composite key: `accountName + "_" + Instrument.FullName`
- `expectedPositions` is keyed by this composite key (one entry per account-instrument pair)
- In V12 SIMA architecture, each account typically has ONE active position per instrument
- Multiple positions on same account = multiple FSMs with different entry names, but same ExpKey

**Impact**: If account has 2 active FSMs (rare but possible during transition), force-zero will clear expected position for BOTH, potentially triggering desync detection on the healthy FSM.

**Mitigation Strategy**:
- Document this as INTENTIONAL behavior (comment already states "to unblock repair loop")
- The 3-attempt threshold ensures this only fires after sustained failure (3 audit cycles = 3+ seconds)
- Alternative: Track orphan count per FSM entry name instead of per account (more granular)

**Extraction Decision**: Keep current behavior (account-level force-zero) but add explicit comment about scope. Defer per-FSM tracking to future enhancement.

---

### MEDIUM RISKS

#### 3. Stop-Replace Suppression Lag
**Location**: `REAPER.Audit.cs:499-513`  
**Severity**: P4 (Grace period absorbs lag)

**Current Code**:
```csharp
bool hasPendingStopReplace = false;
foreach (var psr in pendingStopReplacements.Values)  // Line 500: Snapshot iteration
{
    PositionInfo psrPos;
    if (activePositions.TryGetValue(psr.EntryName, out psrPos)
        && psrPos != null
        && psrPos.ExecutingAccount != null
        && psrPos.ExecutingAccount.Name == acct.Name)
    {
        hasPendingStopReplace = true;
        break;
    }
}
```

**Issue**: `pendingStopReplacements.Values` creates a snapshot at iteration start. If a stop-replace completes DURING iteration (concurrent removal), the snapshot will still contain the stale entry, causing false suppression.

**Impact**: Naked position detection suppressed for one extra audit cycle (1s). Grace period (5s default) absorbs this lag — emergency stop still fires at T=6s instead of T=5s.

**Mitigation**: Already present — grace period is intentionally generous to handle broker latency and concurrent state updates.

**Extraction Decision**: No code change needed. Document this as expected behavior (snapshot semantics).

---

#### 4. Emergency Stop Price Precision
**Location**: `REAPER.NakedStop.cs:38-43`  
**Severity**: P3 (Defensive bounds present)

**Current Code**:
```csharp
double emergencyStopDist = MaximumStop;
double atrBound = CalculateATRStopDistance(RMAStopATRMultiplier);
if (atrBound > 0)
    emergencyStopDist = Math.Min(emergencyStopDist, atrBound);  // Line 41: Use tighter bound
if (emergencyStopDist <= 0)
    emergencyStopDist = Math.Max(tickSize, MinimumStop);  // Line 43: Fallback
```

**Issue**: If ATR collapses (e.g., during low-volatility overnight session), `atrBound` could be very small (e.g., 2 ticks), producing an emergency stop too tight to be useful.

**Analysis**:
- `MaximumStop` is user-configured (typically 50-100 ticks)
- `atrBound` is dynamic (ATR × multiplier)
- `Math.Min` chooses the TIGHTER bound (more conservative)
- Fallback to `MinimumStop` only if result is ≤ 0 (defensive)

**Scenario**: ATR = 0.5 ticks (extreme low volatility) → `atrBound` = 0.5 × 2.0 = 1.0 tick → `emergencyStopDist` = 1.0 tick → Stop placed 1 tick away from current price.

**Impact**: Emergency stop could be too tight, triggering immediately on next tick. However, this is SAFER than no stop at all (naked position protection is the priority).

**Mitigation**: Consider adding a FLOOR to `atrBound` (e.g., `Math.Max(atrBound, MinimumStop)`) before the `Math.Min` comparison.

**Extraction Decision**: Apply floor during extraction:
```csharp
if (atrBound > 0)
    atrBound = Math.Max(atrBound, MinimumStop);  // NEW: Floor ATR bound
emergencyStopDist = Math.Min(emergencyStopDist, atrBound);
```

---

#### 5. Orphan Detection False Positives During Restart
**Location**: `REAPER.Audit.cs:365-388`  
**Severity**: P4 (Self-healing logic present)

**Current Code**:
```csharp
// Handle hydrated Active FSMs with no order reference (restart edge case)
foreach (var f in accountFsms)
{
    if (f.State == FollowerBracketState.Active && f.EntryOrder == null)  // Line 368
    {
        if (actualQty != 0)
        {
            fsmExpectedQty += actualQty;  // Line 372: Hydrate expected position
        }
        else
        {
            FollowerBracketFSM staleFsm;
            if (TryTerminateFollowerBracket(f.EntryName, out staleFsm))  // Line 377: Auto-terminate
            {
                Print(string.Format("[REAPER-C7] Stale Active FSM for {0} on {1} (broker flat) -- auto-terminating",
                    f.EntryName, acct.Name));
            }
        }
    }
}
```

**Issue**: After strategy restart, FSMs are hydrated from `activePositions` but `EntryOrder` references are null (orders are not persisted). If broker position is flat, the FSM is considered "stale" and auto-terminated.

**Analysis**:
- This is INTENTIONAL cleanup logic (comment: "restart edge case")
- If `actualQty != 0`, FSM is kept and expected position is hydrated (line 372)
- If `actualQty == 0`, FSM is terminated (line 377) — assumes position was closed before restart

**False Positive Scenario**: Position closed DURING restart (e.g., stop hit while strategy was offline). FSM is hydrated as Active, but broker is flat. Auto-termination is CORRECT behavior.

**Impact**: None — this is working as designed. Orphan detection (lines 127-153) is a SEPARATE mechanism for detecting FSMs that persist AFTER the 10s grace period.

**Extraction Decision**: No change needed. This logic stays in `AuditFleet_CalculateExpectedActual` (not part of orphan safety extraction).

---

## DEPENDENCY ANALYSIS

### Shared State Dependencies

#### 1. Grace Period Tracking
**State**:
```csharp
private ConcurrentDictionary<string, DateTime> _nakedPositionFirstSeen;
private ConcurrentDictionary<string, DateTime> _orphanedPositionFirstSeen;
```

**Access Pattern**:
- **Write**: Audit thread (via `TriggerCustomEvent` marshalling)
- **Read**: Audit thread (same thread, no cross-thread visibility issues)
- **Cleanup**: Audit thread (`TryRemove` on grace expiry or suppression)

**Extraction Strategy**: Move both dictionaries to new modules. Expose via accessor methods if needed by orchestrators.

---

#### 2. In-Flight Guards
**State**:
```csharp
private readonly ConcurrentDictionary<string, byte> _reaperNakedStopInFlight;
private readonly ConcurrentDictionary<string, int> _reaperOrphanRepairCount;
```

**Access Pattern**:
- **Write**: Audit thread (enqueue) + Strategy thread (queue processor cleanup)
- **Read**: Audit thread (duplicate check)
- **Atomic Operation**: `TryAdd` (CAS), `TryRemove` (CAS)

**Extraction Strategy**: Move to new modules. Cleanup in queue processors must call back to clear guards.

---

#### 3. Queue State
**State**:
```csharp
private ConcurrentQueue<(string, MarketPosition, int)> _reaperNakedStopQueue;
```

**Access Pattern**:
- **Write**: Audit thread (enqueue via `TriggerCustomEvent`)
- **Read**: Strategy thread (queue processor via `TriggerCustomEvent`)
- **Atomic Operation**: `Enqueue` (lock-free), `TryDequeue` (lock-free)

**Extraction Strategy**: Move to new module. Queue processor stays in new module.

---

#### 4. External Dependencies (Read-Only)
**State**:
```csharp
pendingStopReplacements  // Stop-replace suppression check
activePositions          // Orphan PositionInfo lookup
_followerBrackets        // FSM state check
expectedPositions        // Orphan self-heal force-zero
```

**Access Pattern**:
- **Read**: Audit thread (snapshot via `.ToArray()` or `.Values`)
- **Write**: NOT by REAPER (external ownership)

**Extraction Strategy**: Pass as parameters to extracted methods. No direct field access from new modules.

---

## EXTRACTION BOUNDARIES

### Module 1: `V12_002.REAPER.NakedPosition.cs`

**Responsibilities**:
1. Naked position detection with grace period tracking
2. Stop-replace suppression logic
3. Emergency stop enqueue with in-flight guard
4. Emergency stop queue processing (ATR-bounded stop price calculation + order submission)

**Extracted Methods**:
- `DetectNakedPosition(Account, Position, int actualQty, string expectedKey, bool shouldLog)` → bool (enqueued)
- `ProcessNakedStopQueue()` → void (strategy thread processor)
- `CalculateEmergencyStopPrice(MarketPosition, int qty)` → (double stopPrice, OrderAction action)

**State Ownership**:
- `_nakedPositionFirstSeen` (grace tracking)
- `_reaperNakedStopInFlight` (in-flight guard)
- `_reaperNakedStopQueue` (emergency stop queue)

**External Dependencies** (passed as parameters):
- `pendingStopReplacements` (stop-replace suppression check)
- `NakedPositionGraceSec` (configurable grace period)
- `MaximumStop`, `MinimumStop`, `RMAStopATRMultiplier` (stop distance bounds)

---

### Module 2: `V12_002.REAPER.OrphanSafety.cs`

**Responsibilities**:
1. Orphan FSM detection with 10s grace period
2. Diagnostic logging after grace expiry
3. Orphan repair counter with 3-attempt threshold
4. Force-zero self-heal logic

**Extracted Methods**:
- `DetectOrphanFSM(string entryName, string accountName, int actualQty)` → void (diagnostic only)
- `ValidateRepairEligibility_OrphanCheck(string accountName)` → (bool hasOrphan, int attemptCount)
- `ExecuteOrphanSelfHeal(string accountName)` → void (force-zero + counter reset)

**State Ownership**:
- `_orphanedPositionFirstSeen` (grace tracking)
- `_reaperOrphanRepairCount` (3-attempt counter)

**External Dependencies** (passed as parameters):
- `activePositions` (PositionInfo lookup)
- `expectedPositions` (force-zero target)
- `SetExpectedPositionLocked` (force-zero method)

---

## EXTRACTION RISKS

### HIGH RISK

1. **State Ownership Transfer**
   - **Risk**: Moving dictionaries to new modules breaks existing references
   - **Mitigation**: Use accessor methods in base class, delegate to new modules
   - **Verification**: Grep for all references to moved state before extraction

2. **Queue Processor Thread Safety**
   - **Risk**: `ProcessNakedStopQueue` must run on strategy thread (NinjaTrader API requirement)
   - **Mitigation**: Keep `TriggerCustomEvent` marshalling in orchestrator, call into new module
   - **Verification**: F5 test with live naked position scenario

### MEDIUM RISK

3. **Parameter Explosion**
   - **Risk**: Extracted methods need 5-8 parameters (external dependencies)
   - **Mitigation**: Group related parameters into context objects (e.g., `NakedPositionContext`)
   - **Verification**: Code review for readability

4. **Diagnostic Logging Consistency**
   - **Risk**: Log prefixes must remain consistent (`[REAPER][NAKED_POSITION]`, `[REAPER][DIAGNOSTIC]`)
   - **Mitigation**: Define log prefix constants in new modules
   - **Verification**: Grep for all `Print` calls in extracted code

---

## COMPLEXITY REDUCTION TARGETS

### Pre-Extraction Baseline

| Metric | Current | Target | Delta |
|:---|---:|---:|---:|
| Total REAPER LOC | 1,495 | 1,495 | 0 (no deletion) |
| Naked Position CYC | 8 | ≤ 5 | -3 |
| Emergency Stop CYC | 5 | ≤ 5 | 0 (already compliant) |
| Orphan Self-Heal CYC | 6 | ≤ 5 | -1 |
| Max Method LOC | 100 | ≤ 80 | -20 |

**Note**: LOC count unchanged because extraction MOVES code, not deletes it. CYC reduction comes from splitting complex conditionals into helper methods.

---

## ACCEPTANCE CRITERIA

### Functional

1. **Naked Position Detection**
   - [ ] Grace period tracking preserved (5s default, configurable)
   - [ ] Stop-replace suppression logic intact
   - [ ] Emergency stop enqueue with in-flight guard functional
   - [ ] ATR-bounded stop price calculation correct (with floor applied)

2. **Orphan FSM Detection**
   - [ ] 10-second grace period preserved
   - [ ] Diagnostic logging after grace expiry functional
   - [ ] 3-attempt self-heal threshold intact
   - [ ] Force-zero logic triggers correctly (account-level scope documented)

### Non-Functional

3. **Performance**
   - [ ] Zero new heap allocations on hot path
   - [ ] Audit cycle time unchanged (< 100ms per account)
   - [ ] Emergency stop submission latency < 50ms (P99)

4. **Maintainability**
   - [ ] Extracted modules < 200 LOC each
   - [ ] All methods CYC ≤ 10 (target ≤ 5 for orchestrators)
   - [ ] Zero code duplication between modules

5. **Safety**
   - [ ] Zero `lock()` statements in extracted modules
   - [ ] All state mutations atomic (CAS or queue-based)
   - [ ] ASCII-only compliance verified
   - [ ] TOCTOU race in grace check FIXED (use `GetOrAdd`)

---

**[PLAN-GATE]**

Analysis complete. Key risk hotspots identified:
1. TOCTOU race in naked position grace check (P2) — FIX during extraction
2. Orphan self-heal scope blast (P3) — DOCUMENT, no code change
3. Emergency stop price precision (P3) — ADD floor to ATR bound

Proceed to Phase 2B (APPROACH) to define extraction strategy and sub-method signatures.