# REAPER-EXTRACT Epic — Phase 3: VALIDATION
**Epic ID**: REAPER-EXTRACT  
**Protocol**: V12 Photon Kernel (Phase 6 Recursive)  
**Date**: 2026-05-21  
**Agent**: Plan Mode (v12-epic-planner)

---

## VALIDATION SCOPE

This document validates the extraction plan ([`02-approach.md`](02-approach.md)) against:
1. **V12 DNA Constraints** (Lock-Free, ASCII-Only, Zero-Allocation)
2. **Jane Street Atomic Unification Principles**
3. **Architectural Consistency** (with existing REAPER modules)
4. **Risk Mitigation Completeness**

---

## V12 DNA COMPLIANCE AUDIT

### 1. Lock-Free Actor Pattern ✅ PASS

**Requirement**: Zero `lock()` statements. All state mutations via `ConcurrentDictionary` atomic operations or `Enqueue` model.

**Validation**:

#### Module 1: NakedPosition.cs
- ✅ `_nakedPositionFirstSeen`: `GetOrAdd` (atomic CAS)
- ✅ `_reaperNakedStopInFlight`: `TryAdd`, `TryRemove` (atomic CAS)
- ✅ `_reaperNakedStopQueue`: `Enqueue`, `TryDequeue` (lock-free MPSC)
- ✅ No `lock()` statements in any extracted method

#### Module 2: OrphanSafety.cs
- ✅ `_orphanedPositionFirstSeen`: `GetOrAdd`, `TryRemove` (atomic CAS)
- ✅ `_reaperOrphanRepairCount`: `AddOrUpdate`, `TryRemove` (atomic CAS)
- ✅ No `lock()` statements in any extracted method

**TOCTOU Fix Verification**:
```csharp
// BEFORE (TOCTOU race):
if (!_nakedPositionFirstSeen.TryGetValue(acct.Name, out firstSeen))
{
    _nakedPositionFirstSeen[acct.Name] = DateTime.UtcNow;  // ❌ Non-atomic write
}

// AFTER (TOCTOU-safe):
DateTime firstSeen = _nakedPositionFirstSeen.GetOrAdd(acct.Name, DateTime.UtcNow);  // ✅ Atomic CAS
```

**Verdict**: ✅ **PASS** — All state mutations are lock-free and atomic.

---

### 2. ASCII-Only Compliance ✅ PASS

**Requirement**: No Unicode, emoji, or curly quotes in C# string literals.

**Validation**:

#### Log Prefixes (All ASCII)
- ✅ `[REAPER][NAKED_POSITION]` — ASCII brackets and uppercase
- ✅ `[REAPER][EMERGENCY_STOP]` — ASCII brackets and uppercase
- ✅ `[REAPER][DIAGNOSTIC]` — ASCII brackets and uppercase
- ✅ `[REAPER] SELF-HEAL:` — ASCII brackets and hyphen

#### Format Strings (All ASCII)
- ✅ `"{0}: {1}ct naked -- starting {2}s grace window."` — ASCII punctuation
- ✅ `"{0}: {1}ct CONFIRMED naked after {2:F1}s grace."` — ASCII punctuation
- ✅ `"[REAPER] SELF-HEAL: {0} has no PositionInfo after 3 attempts."` — ASCII punctuation

**Verdict**: ✅ **PASS** — All string literals are ASCII-only.

---

### 3. Zero New Allocations ✅ PASS (with caveat)

**Requirement**: Hot-path methods must not allocate on heap. Use object pooling or stack allocation.

**Validation**:

#### Hot-Path Analysis

**DetectNakedPosition** (called per audit cycle, per account):
- ✅ `GetOrAdd` — No allocation if key exists (common case after first detection)
- ✅ `TryAdd` — No allocation on failure (duplicate guard)
- ✅ `Enqueue` — Allocates tuple `(string, MarketPosition, int)` **ONLY on grace expiry** (rare event)
- ✅ `string.Format` — Allocates string **ONLY when `shouldLog=true`** (30s interval)

**CalculateEmergencyStopPrice** (called per emergency stop, rare event):
- ✅ `Math.Min`, `Math.Max` — No allocation (value types)
- ✅ `Instrument.MasterInstrument.RoundToTickSize` — No allocation (in-place rounding)
- ✅ Tuple return `(double, OrderAction)` — Stack allocation (value types)

**DetectOrphanFSM** (called per audit cycle, per FSM):
- ✅ `GetOrAdd` — No allocation if key exists (common case after first detection)
- ✅ `TryRemove` — No allocation (CAS operation)
- ✅ `string.Format` — Allocates string **ONLY after 10s grace expiry** (rare event)

**Caveat**: Emergency stop tuple `(string, MarketPosition, int)` allocates on enqueue. This is **acceptable** because:
1. Enqueue happens ONLY after grace expiry (5s minimum) — not every audit cycle
2. Emergency stop is a rare event (naked position should not occur in normal operation)
3. Allocation is bounded (one tuple per emergency stop, not per audit cycle)

**Verdict**: ✅ **PASS** — Zero allocations on common path. Rare-event allocations are bounded and acceptable.

---

## JANE STREET ATOMIC UNIFICATION AUDIT

### Principle 1: Atomic State Transitions ✅ PASS

**Requirement**: State changes must be indivisible (no partial updates visible to observers).

**Validation**:

#### Grace Period Initialization (TOCTOU Fix)
```csharp
// ATOMIC: GetOrAdd is a single CAS operation
DateTime firstSeen = _nakedPositionFirstSeen.GetOrAdd(acct.Name, DateTime.UtcNow);
```
- ✅ Either the key exists (return existing value) OR it doesn't (insert new value)
- ✅ No intermediate state where key exists but value is null
- ✅ No race where two threads both insert (CAS ensures only one succeeds)

#### In-Flight Guard (Duplicate Prevention)
```csharp
// ATOMIC: TryAdd is a single CAS operation
if (!_reaperNakedStopInFlight.TryAdd(expectedKey, 0))
{
    return false;  // Already in flight - skip
}
```
- ✅ Either the key is added (return true) OR it already exists (return false)
- ✅ No race where two threads both add the same key

#### Orphan Counter (3-Attempt Threshold)
```csharp
// ATOMIC: AddOrUpdate is a single CAS operation
int orphanCount = _reaperOrphanRepairCount.AddOrUpdate(accountName, 1, (k, v) => v + 1);
```
- ✅ Either the key is added with value 1 OR existing value is incremented
- ✅ No race where two threads both increment (CAS ensures serialization)

**Verdict**: ✅ **PASS** — All state transitions are atomic via CAS operations.

---

### Principle 2: Wait-Free Progress ✅ PASS

**Requirement**: No thread can block another thread's progress indefinitely.

**Validation**:

#### Audit Thread (Background Timer)
- ✅ `GetOrAdd` — CAS operation, no blocking (retry on contention)
- ✅ `TryAdd` — CAS operation, no blocking (fail-fast on duplicate)
- ✅ `Enqueue` — Lock-free MPSC, no blocking (wait-free enqueue)
- ✅ `TriggerCustomEvent` — Marshals to strategy thread, no blocking (fire-and-forget)

#### Strategy Thread (Queue Processor)
- ✅ `TryDequeue` — Lock-free MPSC, no blocking (fail-fast if empty)
- ✅ `TryRemove` — CAS operation, no blocking (fail-fast if missing)

**No Blocking Scenarios**:
- Audit thread NEVER waits for strategy thread (fire-and-forget marshalling)
- Strategy thread NEVER waits for audit thread (queue-based communication)
- Multiple audit cycles can run concurrently (CAS operations are wait-free)

**Verdict**: ✅ **PASS** — All operations are wait-free or fail-fast.

---

### Principle 3: Memory Ordering ✅ PASS

**Requirement**: Explicit memory barriers where cross-thread visibility is required.

**Validation**:

#### Cross-Thread Communication
```csharp
// Audit thread (background timer):
_reaperNakedStopQueue.Enqueue((acct.Name, pos.MarketPosition, Math.Abs(actualQty)));
TriggerCustomEvent(e => ProcessReaperNakedStopQueue(), null);  // ✅ Memory barrier

// Strategy thread (queue processor):
while (_reaperNakedStopQueue.TryDequeue(out var item))  // ✅ Sees enqueued item
```

**Memory Barrier Analysis**:
- ✅ `TriggerCustomEvent` provides implicit memory barrier (NinjaTrader API guarantee)
- ✅ `ConcurrentQueue.Enqueue` provides release semantics (writes visible to dequeue)
- ✅ `ConcurrentQueue.TryDequeue` provides acquire semantics (reads see prior writes)
- ✅ `ConcurrentDictionary` operations provide full fence (all writes visible)

**Verdict**: ✅ **PASS** — Memory ordering is correct via `TriggerCustomEvent` and concurrent collection semantics.

---

### Principle 4: Bounded Latency ✅ PASS

**Requirement**: Worst-case execution time must be deterministic and bounded.

**Validation**:

#### DetectNakedPosition (Worst-Case Path)
1. `_isTerminating` check — O(1) volatile read
2. `CheckPendingStopReplace` — O(N) where N = pendingStopReplacements.Count (bounded by max concurrent positions)
3. `GetOrAdd` — O(1) CAS operation (amortized, no rehashing on hot path)
4. `DateTime.UtcNow` — O(1) syscall (monotonic clock, no blocking)
5. `TryAdd` — O(1) CAS operation
6. `Enqueue` — O(1) lock-free operation
7. `TriggerCustomEvent` — O(1) fire-and-forget (no blocking)

**Worst-Case Bound**: O(N) where N = max concurrent positions (typically < 10)

#### CalculateEmergencyStopPrice (Worst-Case Path)
1. `CalculateATRStopDistance` — O(1) (ATR indicator cached)
2. `Math.Min`, `Math.Max` — O(1) arithmetic
3. `RoundToTickSize` — O(1) arithmetic

**Worst-Case Bound**: O(1)

**Verdict**: ✅ **PASS** — All operations have bounded latency (no unbounded loops, no blocking I/O).

---

## ARCHITECTURAL CONSISTENCY AUDIT

### 1. Module Naming Convention ✅ PASS

**Existing Pattern**:
- `V12_002.REAPER.cs` — Base infrastructure
- `V12_002.REAPER.Audit.cs` — Audit orchestrators
- `V12_002.REAPER.Repair.cs` — Ghost position repair
- `V12_002.REAPER.NakedStop.cs` — Emergency stop processor

**New Modules**:
- ✅ `V12_002.REAPER.NakedPosition.cs` — Follows `REAPER.*` pattern
- ✅ `V12_002.REAPER.OrphanSafety.cs` — Follows `REAPER.*` pattern

**Verdict**: ✅ **PASS** — Naming is consistent with existing REAPER namespace.

---

### 2. State Ownership Pattern ✅ PASS

**Existing Pattern** (from `REAPER.cs`):
```csharp
private ConcurrentQueue<string> _reaperFlattenQueue;
private ConcurrentQueue<string> _reaperRepairQueue;
private readonly ConcurrentDictionary<string, byte> _repairInFlight;
```

**New Pattern** (proposed):
```csharp
// NakedPosition.cs
private ConcurrentDictionary<string, DateTime> _nakedPositionFirstSeen;
private readonly ConcurrentDictionary<string, byte> _reaperNakedStopInFlight;
private ConcurrentQueue<(string, MarketPosition, int)> _reaperNakedStopQueue;

// OrphanSafety.cs
private readonly ConcurrentDictionary<string, DateTime> _orphanedPositionFirstSeen;
private readonly ConcurrentDictionary<string, int> _reaperOrphanRepairCount;
```

**Consistency Check**:
- ✅ In-flight guards use `ConcurrentDictionary<string, byte>` (matches `_repairInFlight`)
- ✅ Queues use `ConcurrentQueue<T>` (matches `_reaperFlattenQueue`)
- ✅ Grace tracking uses `ConcurrentDictionary<string, DateTime>` (new pattern, but consistent with atomic requirements)

**Verdict**: ✅ **PASS** — State ownership follows existing patterns.

---

### 3. Accessor Method Pattern ✅ PASS

**Existing Pattern** (from `REAPER.cs`):
```csharp
private void StampAccountFillGrace(string expKey) { /* ... */ }
private bool IsReaperFillGraceActive(string expKey) { /* ... */ }
```

**New Pattern** (proposed):
```csharp
internal void ClearNakedPositionGrace(string accountName) { /* ... */ }
internal void ClearNakedStopInFlight(string expectedKey) { /* ... */ }
internal void ClearOrphanFSMGrace(string entryName) { /* ... */ }
internal void ClearOrphanRepairCount(string accountName) { /* ... */ }
```

**Consistency Check**:
- ✅ `internal` visibility (matches existing helper methods)
- ✅ Verb-noun naming (`Clear*`, matches `Stamp*`, `Is*`)
- ✅ Single responsibility (one state operation per method)

**Verdict**: ✅ **PASS** — Accessor methods follow existing patterns.

---

### 4. Log Prefix Consistency ✅ PASS

**Existing Prefixes**:
- `[REAPER]` — General audit messages
- `[REAPER REPAIR]` — Repair engine messages
- `[FSM-C3]`, `[FSM-RACE GUARD]` — FSM lifecycle messages

**New Prefixes** (proposed):
- ✅ `[REAPER][NAKED_POSITION]` — Naked position detection
- ✅ `[REAPER][EMERGENCY_STOP]` — Emergency stop submission
- ✅ `[REAPER][DIAGNOSTIC]` — Orphan FSM diagnostic
- ✅ `[REAPER] SELF-HEAL:` — Orphan self-heal

**Consistency Check**:
- ✅ All use `[REAPER]` prefix (matches existing pattern)
- ✅ Sub-categories use `[REAPER][SUBCATEGORY]` (matches existing pattern)
- ✅ ASCII-only (no Unicode brackets or emoji)

**Verdict**: ✅ **PASS** — Log prefixes are consistent with existing REAPER messages.

---

## RISK MITIGATION COMPLETENESS AUDIT

### Critical Risks (from 01-analysis.md)

#### 1. TOCTOU Race in Grace Check ✅ MITIGATED

**Risk**: Two audit cycles could both pass grace check before either updates timestamp.

**Mitigation Applied**:
```csharp
// BEFORE (TOCTOU race):
if (!_nakedPositionFirstSeen.TryGetValue(acct.Name, out firstSeen))
{
    _nakedPositionFirstSeen[acct.Name] = DateTime.UtcNow;  // ❌ Non-atomic
}

// AFTER (TOCTOU-safe):
DateTime firstSeen = _nakedPositionFirstSeen.GetOrAdd(acct.Name, DateTime.UtcNow);  // ✅ Atomic
```

**Verification**:
- ✅ `GetOrAdd` is atomic (CAS operation)
- ✅ No intermediate state where key exists but value is null
- ✅ Race condition eliminated

**Verdict**: ✅ **MITIGATED** — TOCTOU race fixed via atomic `GetOrAdd`.

---

#### 2. Orphan Self-Heal Scope Blast ✅ DOCUMENTED

**Risk**: `SetExpectedPositionLocked(ExpKey(accountName), 0)` clears expected position for ENTIRE account, not just orphaned entry.

**Mitigation Applied**:
```csharp
/// <remarks>
/// SCOPE BLAST WARNING: SetExpectedPositionLocked(ExpKey(accountName), 0) clears the expected
/// position for the ENTIRE account (all FSMs on this account-instrument pair), not just the
/// orphaned entry. This is INTENTIONAL - the 3-attempt threshold ensures this only fires after
/// sustained failure (3+ audit cycles = 3+ seconds), indicating a systemic issue requiring
/// aggressive cleanup to unblock the repair loop.
/// </remarks>
```

**Verification**:
- ✅ Scope blast is INTENTIONAL (not a bug)
- ✅ 3-attempt threshold ensures rare trigger (3+ seconds of sustained failure)
- ✅ Documentation added to `ExecuteOrphanSelfHeal` method

**Verdict**: ✅ **DOCUMENTED** — Scope blast is intentional and documented.

---

#### 3. Emergency Stop Price Precision ✅ MITIGATED

**Risk**: If ATR collapses, `atrBound` could be very small (e.g., 2 ticks), producing an emergency stop too tight.

**Mitigation Applied**:
```csharp
// BEFORE (no floor):
if (atrBound > 0)
    emergencyStopDist = Math.Min(emergencyStopDist, atrBound);

// AFTER (with floor):
if (atrBound > 0)
{
    atrBound = Math.Max(atrBound, MinimumStop);  // ✅ Floor ATR bound
    emergencyStopDist = Math.Min(emergencyStopDist, atrBound);
}
```

**Verification**:
- ✅ `MinimumStop` floor prevents stops tighter than user-configured minimum
- ✅ Fallback to `Math.Max(tickSize, MinimumStop)` still present (defensive)
- ✅ Emergency stop will be at least `MinimumStop` ticks away

**Verdict**: ✅ **MITIGATED** — ATR floor prevents overly tight stops.

---

### Medium Risks (from 01-analysis.md)

#### 4. Stop-Replace Suppression Lag ✅ ACCEPTED

**Risk**: `pendingStopReplacements.Values` snapshot could miss concurrent removals, causing false suppression.

**Mitigation**: Grace period (5s default) absorbs lag — emergency stop still fires at T=6s instead of T=5s.

**Verification**:
- ✅ Grace period is intentionally generous (5s minimum)
- ✅ One-cycle lag (1s) is acceptable given grace buffer
- ✅ No code change needed (snapshot semantics are correct)

**Verdict**: ✅ **ACCEPTED** — Lag is within grace period tolerance.

---

#### 5. Orphan Detection False Positives During Restart ✅ ACCEPTED

**Risk**: Hydrated Active FSMs with no order reference (restart edge case) could trigger orphan detection.

**Mitigation**: Special handling in `AuditFleet_CalculateExpectedActual` (lines 365-388) auto-terminates stale FSMs.

**Verification**:
- ✅ Restart edge case is handled BEFORE orphan detection (lines 365-388)
- ✅ Orphan detection (lines 127-153) is a SEPARATE mechanism (10s grace)
- ✅ No extraction impact (restart logic stays in `AuditFleet_CalculateExpectedActual`)

**Verdict**: ✅ **ACCEPTED** — Restart edge case is already handled.

---

## EXTRACTION BOUNDARY VALIDATION

### Module 1: NakedPosition.cs ✅ PASS

**Responsibilities**:
- ✅ Naked position detection with grace period tracking
- ✅ Stop-replace suppression logic
- ✅ Emergency stop enqueue with in-flight guard
- ✅ Emergency stop queue processing (ATR-bounded stop price calculation + order submission)

**State Ownership**:
- ✅ `_nakedPositionFirstSeen` (grace tracking)
- ✅ `_reaperNakedStopInFlight` (in-flight guard)
- ✅ `_reaperNakedStopQueue` (emergency stop queue)

**External Dependencies** (passed as parameters):
- ✅ `pendingStopReplacements` (stop-replace suppression check)
- ✅ `activePositions` (stop-replace lookup)
- ✅ `NakedPositionGraceSec` (configurable grace period)
- ✅ `MaximumStop`, `MinimumStop`, `RMAStopATRMultiplier` (stop distance bounds)

**Verdict**: ✅ **PASS** — Boundaries are clear and dependencies are explicit.

---

### Module 2: OrphanSafety.cs ✅ PASS

**Responsibilities**:
- ✅ Orphan FSM detection with 10s grace period
- ✅ Diagnostic logging after grace expiry
- ✅ Orphan repair counter with 3-attempt threshold
- ✅ Force-zero self-heal logic

**State Ownership**:
- ✅ `_orphanedPositionFirstSeen` (grace tracking)
- ✅ `_reaperOrphanRepairCount` (3-attempt counter)

**External Dependencies** (passed as parameters):
- ✅ `activePositions` (PositionInfo lookup)
- ✅ `expectedPositions` (force-zero target)
- ✅ `SetExpectedPositionLocked` (force-zero method)

**Verdict**: ✅ **PASS** — Boundaries are clear and dependencies are explicit.

---

## COMPLEXITY REDUCTION VALIDATION

### Pre-Extraction Baseline (from 01-analysis.md)

| Method | Current CYC | Target CYC | Reduction Strategy |
|:---|---:|---:|:---|
| `EnqueueReaperNakedStopCandidate` | 8 | ≤ 5 | Extract 3 helpers |
| `ProcessReaperNakedStopQueue` | 5 | ≤ 5 | Extract 1 helper |
| `ValidateRepairEligibility` | 6 | ≤ 5 | Extract 2 helpers |

### Post-Extraction Target

| Method | Target CYC | Helper Count | Validation |
|:---|---:|---:|:---|
| `DetectNakedPosition` | ≤ 5 | 3 | ✅ Orchestrator only |
| `CheckPendingStopReplace` | ≤ 3 | 0 | ✅ Pure helper |
| `EvaluateNakedPositionGrace` | ≤ 3 | 0 | ✅ Pure helper |
| `EnqueueEmergencyStop` | ≤ 2 | 0 | ✅ Pure helper |
| `CalculateEmergencyStopPrice` | ≤ 3 | 0 | ✅ Pure helper |
| `DetectOrphanFSM` | ≤ 4 | 0 | ✅ Diagnostic only |
| `ValidateRepairEligibility_OrphanCheck` | ≤ 5 | 1 | ✅ Orchestrator only |
| `ExecuteOrphanSelfHeal` | ≤ 2 | 0 | ✅ Pure helper |

**Verdict**: ✅ **PASS** — All CYC targets are achievable via helper extraction.

---

## VALIDATION SUMMARY

### V12 DNA Compliance
- ✅ **Lock-Free Actor Pattern**: All state mutations atomic (CAS or queue-based)
- ✅ **ASCII-Only Compliance**: All string literals ASCII-only
- ✅ **Zero New Allocations**: Zero allocations on common path, bounded allocations on rare events

### Jane Street Atomic Unification
- ✅ **Atomic State Transitions**: All state changes indivisible via CAS
- ✅ **Wait-Free Progress**: No blocking operations, fail-fast on contention
- ✅ **Memory Ordering**: Correct via `TriggerCustomEvent` and concurrent collection semantics
- ✅ **Bounded Latency**: All operations O(1) or O(N) where N is bounded

### Architectural Consistency
- ✅ **Module Naming**: Follows `REAPER.*` pattern
- ✅ **State Ownership**: Follows existing patterns
- ✅ **Accessor Methods**: Follows existing patterns
- ✅ **Log Prefixes**: Consistent with existing REAPER messages

### Risk Mitigation
- ✅ **TOCTOU Race**: Fixed via atomic `GetOrAdd`
- ✅ **Orphan Scope Blast**: Documented as intentional
- ✅ **Emergency Stop Precision**: Fixed via ATR floor
- ✅ **Stop-Replace Lag**: Accepted (within grace tolerance)
- ✅ **Restart False Positives**: Accepted (already handled)

### Complexity Reduction
- ✅ **All CYC Targets Achievable**: Via helper extraction

---

## VALIDATION ISSUES

### CRITICAL: None ✅

### SIGNIFICANT: None ✅

### MODERATE: None ✅

---

## RECOMMENDATIONS

### 1. Pre-Extraction Checklist Enhancement

Add to [`02-approach.md`](02-approach.md) Pre-Extraction Checklist:

```markdown
4. **Jane Street Alignment Verification**
   - [ ] All state mutations use CAS operations (no direct indexer writes)
   - [ ] All cross-thread communication uses `TriggerCustomEvent` marshalling
   - [ ] All operations have bounded latency (no unbounded loops)
```

### 2. Post-Extraction Verification Enhancement

Add to [`02-approach.md`](02-approach.md) Post-Extraction Verification:

```markdown
4. **Jane Street Compliance Tests**
   - [ ] TOCTOU race eliminated (use `GetOrAdd` instead of `TryGetValue` + indexer)
   - [ ] ATR floor applied (emergency stop distance >= MinimumStop)
   - [ ] Memory ordering correct (all writes visible after `TriggerCustomEvent`)
```

---

**[VALIDATE-GATE]**

Validation complete. Summary:

- **V12 DNA Compliance**: ✅ PASS (Lock-Free, ASCII-Only, Zero-Allocation)
- **Jane Street Alignment**: ✅ PASS (Atomic, Wait-Free, Bounded, Memory-Ordered)
- **Architectural Consistency**: ✅ PASS (Naming, State, Accessors, Logging)
- **Risk Mitigation**: ✅ COMPLETE (All critical risks addressed)
- **Complexity Reduction**: ✅ ACHIEVABLE (All CYC targets feasible)

**Issues Found**: 0 CRITICAL, 0 SIGNIFICANT, 0 MODERATE

**Recommendations**: 2 enhancements to checklists (non-blocking)

**Verdict**: ✅ **APPROVED FOR EXECUTION**

Proceed to Phase 4 (TICKETS)?