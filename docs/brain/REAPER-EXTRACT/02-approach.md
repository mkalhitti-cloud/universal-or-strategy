# REAPER-EXTRACT Epic — Phase 2: APPROACH
**Epic ID**: REAPER-EXTRACT  
**Protocol**: V12 Photon Kernel (Phase 6 Recursive)  
**Date**: 2026-05-21  
**Agent**: Plan Mode (v12-epic-planner)

---

## EXTRACTION STRATEGY

### Design Philosophy

**Principle**: "Surgical Extraction with Zero Behavioral Change"

1. **Move, Don't Rewrite**: Extract existing logic verbatim, then refactor for clarity
2. **Atomic State Transitions**: Preserve all `ConcurrentDictionary` atomic operations
3. **Thread Safety First**: Maintain `TriggerCustomEvent` marshalling pattern
4. **Fail-Safe Defaults**: Keep all defensive bounds and grace periods
5. **Observability**: Preserve all diagnostic logging with consistent prefixes

---

## TARGET STATE ARCHITECTURE

### Module Structure

```
src/
├── V12_002.REAPER.cs                    [EXISTING - Timer + Shared Helpers]
├── V12_002.REAPER.Audit.cs              [EXISTING - Orchestrators]
├── V12_002.REAPER.Repair.cs             [EXISTING - Ghost Position Repair]
├── V12_002.REAPER.NakedStop.cs          [EXISTING - Emergency Stop Processor]
├── V12_002.REAPER.NakedPosition.cs      [NEW - Naked Position Detection]
└── V12_002.REAPER.OrphanSafety.cs       [NEW - Orphan FSM Detection]
```

### Responsibility Matrix

| Module | Responsibilities | LOC | CYC Target |
|:---|:---|---:|---:|
| **REAPER.cs** | Timer infrastructure, shared helpers | 163 | ≤ 5 |
| **REAPER.Audit.cs** | Audit orchestrators, desync detection | ~800 | ≤ 7 |
| **REAPER.Repair.cs** | Ghost position repair engine | ~200 | ≤ 10 |
| **REAPER.NakedStop.cs** | Emergency stop queue processor | 84 | ≤ 5 |
| **REAPER.NakedPosition.cs** | Naked position detection + enqueue | ~150 | ≤ 5 |
| **REAPER.OrphanSafety.cs** | Orphan FSM detection + self-heal | ~100 | ≤ 5 |

---

## MODULE 1: V12_002.REAPER.NakedPosition.cs

### Extracted Methods

#### 1. `DetectNakedPosition` (Primary Entry Point)
**Signature**:
```csharp
/// <summary>
/// Detects naked positions (position without working stop) and enqueues emergency stop after grace period.
/// Thread-safe: Called from audit thread via TriggerCustomEvent marshalling.
/// </summary>
/// <param name="acct">Account to check</param>
/// <param name="pos">Broker position (non-null, non-flat)</param>
/// <param name="actualQty">Signed quantity (positive=long, negative=short)</param>
/// <param name="expectedKey">Composite key for in-flight guard (accountName_instrumentName)</param>
/// <param name="shouldLog">Enable diagnostic logging</param>
/// <param name="pendingStopReplacements">Stop-replace queue for suppression check</param>
/// <param name="activePositions">Position info dictionary for stop-replace lookup</param>
/// <returns>True if emergency stop was enqueued, false otherwise</returns>
private bool DetectNakedPosition(
    Account acct,
    Position pos,
    int actualQty,
    string expectedKey,
    bool shouldLog,
    ConcurrentDictionary<string, PendingStopReplacement> pendingStopReplacements,
    ConcurrentDictionary<string, PositionInfo> activePositions)
```

**Logic Flow**:
1. Check `_isTerminating` guard (H17-GUARD)
2. Check for pending stop-replace (suppression logic)
3. If suppressed: Clear grace timestamp, log, return false
4. If not suppressed: Check grace period
   - First detection: Record timestamp via `GetOrAdd` (TOCTOU fix)
   - Grace active: Return false (wait)
   - Grace expired: Enqueue emergency stop via `TryAdd` guard

**CYC Target**: ≤ 5 (currently 8, reduce via helper extraction)

---

#### 2. `CheckPendingStopReplace` (Helper)
**Signature**:
```csharp
/// <summary>
/// Checks if account has a pending stop-replace operation (suppresses naked position detection).
/// </summary>
/// <param name="acct">Account to check</param>
/// <param name="pendingStopReplacements">Stop-replace queue</param>
/// <param name="activePositions">Position info dictionary</param>
/// <returns>True if stop-replace is in flight for this account</returns>
private bool CheckPendingStopReplace(
    Account acct,
    ConcurrentDictionary<string, PendingStopReplacement> pendingStopReplacements,
    ConcurrentDictionary<string, PositionInfo> activePositions)
```

**Logic**: Extract lines 499-513 from `EnqueueReaperNakedStopCandidate`

**CYC Target**: ≤ 3

---

#### 3. `EvaluateNakedPositionGrace` (Helper)
**Signature**:
```csharp
/// <summary>
/// Evaluates naked position grace period. Records first detection or checks elapsed time.
/// TOCTOU-safe: Uses GetOrAdd for atomic timestamp initialization.
/// </summary>
/// <param name="accountName">Account name</param>
/// <param name="actualQty">Signed quantity</param>
/// <param name="graceSeconds">Configurable grace period (default 5s, min 5s)</param>
/// <param name="shouldLog">Enable diagnostic logging</param>
/// <returns>True if grace period has expired, false if still active or just started</returns>
private bool EvaluateNakedPositionGrace(
    string accountName,
    int actualQty,
    int graceSeconds,
    bool shouldLog)
```

**Logic**: Extract lines 523-537 from `EnqueueReaperNakedStopCandidate`

**TOCTOU Fix**:
```csharp
// BEFORE (TOCTOU race):
if (!_nakedPositionFirstSeen.TryGetValue(acct.Name, out firstSeen))
{
    _nakedPositionFirstSeen[acct.Name] = DateTime.UtcNow;  // Non-atomic write
}

// AFTER (TOCTOU-safe):
DateTime firstSeen = _nakedPositionFirstSeen.GetOrAdd(acct.Name, DateTime.UtcNow);
if ((DateTime.UtcNow - firstSeen).TotalSeconds < graceSeconds)
{
    // Grace active - log on first detection only
    if ((DateTime.UtcNow - firstSeen).TotalSeconds < 0.5)  // Within first 500ms
    {
        Print(string.Format("[REAPER][NAKED_POSITION] {0}: {1}ct naked -- starting {2}s grace window.",
            acct.Name, actualQty, graceSeconds));
    }
    return false;  // Grace active
}
return true;  // Grace expired
```

**CYC Target**: ≤ 3

---

#### 4. `EnqueueEmergencyStop` (Helper)
**Signature**:
```csharp
/// <summary>
/// Enqueues emergency stop request with in-flight guard (prevents duplicates).
/// </summary>
/// <param name="accountName">Account name</param>
/// <param name="pos">Broker position</param>
/// <param name="actualQty">Signed quantity</param>
/// <param name="expectedKey">Composite key for in-flight guard</param>
/// <param name="graceElapsed">Elapsed grace time (for logging)</param>
/// <returns>True if enqueued, false if already in-flight</returns>
private bool EnqueueEmergencyStop(
    string accountName,
    Position pos,
    int actualQty,
    string expectedKey,
    double graceElapsed)
```

**Logic**: Extract lines 539-554 from `EnqueueReaperNakedStopCandidate`

**CYC Target**: ≤ 2

---

#### 5. `CalculateEmergencyStopPrice` (Extracted from NakedStop.cs)
**Signature**:
```csharp
/// <summary>
/// Calculates ATR-bounded emergency stop price with defensive floor.
/// Jane Street Alignment: Bounded latency via deterministic calculation.
/// </summary>
/// <param name="direction">Position direction (Long/Short)</param>
/// <param name="currentClose">Current bar close price</param>
/// <param name="qty">Position quantity (unsigned)</param>
/// <returns>Tuple of (stopPrice, closeAction)</returns>
private (double stopPrice, OrderAction closeAction) CalculateEmergencyStopPrice(
    MarketPosition direction,
    double currentClose,
    int qty)
```

**Logic**: Extract lines 35-57 from `REAPER.NakedStop.cs:ProcessReaperNakedStopQueue`

**ATR Floor Fix**:
```csharp
// BEFORE (no floor):
double emergencyStopDist = MaximumStop;
double atrBound = CalculateATRStopDistance(RMAStopATRMultiplier);
if (atrBound > 0)
    emergencyStopDist = Math.Min(emergencyStopDist, atrBound);

// AFTER (with floor):
double emergencyStopDist = MaximumStop;
double atrBound = CalculateATRStopDistance(RMAStopATRMultiplier);
if (atrBound > 0)
{
    atrBound = Math.Max(atrBound, MinimumStop);  // NEW: Floor ATR bound
    emergencyStopDist = Math.Min(emergencyStopDist, atrBound);
}
if (emergencyStopDist <= 0)
    emergencyStopDist = Math.Max(tickSize, MinimumStop);  // Fallback unchanged
```

**CYC Target**: ≤ 3

---

### State Ownership

**Moved to NakedPosition.cs**:
```csharp
// Grace period tracking (key = account name)
private ConcurrentDictionary<string, DateTime> _nakedPositionFirstSeen
    = new ConcurrentDictionary<string, DateTime>();

// In-flight guard (key = expectedKey = accountName_instrumentName)
private readonly ConcurrentDictionary<string, byte> _reaperNakedStopInFlight
    = new ConcurrentDictionary<string, byte>();

// Emergency stop queue (marshalled to strategy thread)
private ConcurrentQueue<(string AccountName, MarketPosition Direction, int Qty)> _reaperNakedStopQueue
    = new ConcurrentQueue<(string, MarketPosition, int)>();
```

**Accessor Methods** (in base class `V12_002.REAPER.cs`):
```csharp
// Expose for orchestrator cleanup
internal void ClearNakedPositionGrace(string accountName)
{
    _nakedPositionFirstSeen.TryRemove(accountName, out _);
}

// Expose for queue processor cleanup
internal void ClearNakedStopInFlight(string expectedKey)
{
    _reaperNakedStopInFlight.TryRemove(expectedKey, out _);
}
```

---

### Integration Points

**Caller**: `AuditFleet_HandleNakedPosition` (REAPER.Audit.cs:304)

**BEFORE**:
```csharp
private void AuditFleet_HandleNakedPosition(Account acct, Position pos, int actualQty, string expectedKey, bool shouldLog)
{
    bool hasWorkingStop = AuditFleet_CheckWorkingStop(acct);
    if (!hasWorkingStop)
    {
        if (EnqueueReaperNakedStopCandidate(acct, pos, actualQty, expectedKey, shouldLog))
        {
            try { TriggerCustomEvent(e => ProcessReaperNakedStopQueue(), null); }
            catch (Exception tcEx) { /* cleanup */ }
        }
    }
    else
    {
        _nakedPositionFirstSeen.TryRemove(acct.Name, out _);
    }
}
```

**AFTER**:
```csharp
private void AuditFleet_HandleNakedPosition(Account acct, Position pos, int actualQty, string expectedKey, bool shouldLog)
{
    bool hasWorkingStop = AuditFleet_CheckWorkingStop(acct);
    if (!hasWorkingStop)
    {
        if (DetectNakedPosition(acct, pos, actualQty, expectedKey, shouldLog, pendingStopReplacements, activePositions))
        {
            try { TriggerCustomEvent(e => ProcessReaperNakedStopQueue(), null); }
            catch (Exception tcEx)
            {
                ClearNakedStopInFlight(expectedKey);  // NEW: Accessor method
                Print(string.Format("[REAPER][NAKED_STOP] TriggerCustomEvent failed for {0}: {1} -- in-flight cleared.",
                    acct.Name, tcEx.Message));
            }
        }
    }
    else
    {
        ClearNakedPositionGrace(acct.Name);  // NEW: Accessor method
    }
}
```

---

## MODULE 2: V12_002.REAPER.OrphanSafety.cs

### Extracted Methods

#### 1. `DetectOrphanFSM` (Diagnostic Entry Point)
**Signature**:
```csharp
/// <summary>
/// Detects orphaned FSM positions (broker flat but activePositions entry exists) after 10s grace.
/// Diagnostic only - logs warning but does NOT trigger flatten (non-fatal assertion).
/// </summary>
/// <param name="entryName">FSM entry name (key in activePositions)</param>
/// <param name="accountName">Account name</param>
/// <param name="actualQty">Broker position quantity (should be 0 for orphan detection)</param>
/// <param name="activePositions">Position info dictionary</param>
/// <returns>True if orphan detected and logged, false if grace active or position live</returns>
private bool DetectOrphanFSM(
    string entryName,
    string accountName,
    int actualQty,
    ConcurrentDictionary<string, PositionInfo> activePositions)
```

**Logic**: Extract lines 127-153 from `REAPER.Audit.cs:AuditSingleFleetAccount`

**CYC Target**: ≤ 4

---

#### 2. `ValidateRepairEligibility_OrphanCheck` (Repair Integration)
**Signature**:
```csharp
/// <summary>
/// Validates repair eligibility with orphan self-heal logic.
/// Increments orphan counter on PositionInfo lookup failure.
/// Triggers force-zero self-heal after 3 failed attempts.
/// </summary>
/// <param name="accountName">Account name</param>
/// <param name="activePositions">Position info dictionary</param>
/// <param name="repairPos">Output: PositionInfo if found</param>
/// <param name="repairEntryName">Output: Entry name if found</param>
/// <returns>True if PositionInfo found (repair can proceed), false if orphaned</returns>
private bool ValidateRepairEligibility_OrphanCheck(
    string accountName,
    ConcurrentDictionary<string, PositionInfo> activePositions,
    out PositionInfo repairPos,
    out string repairEntryName)
```

**Logic**: Extract lines 43-75 from `REAPER.Repair.cs:ValidateRepairEligibility`

**CYC Target**: ≤ 5

---

#### 3. `ExecuteOrphanSelfHeal` (Helper)
**Signature**:
```csharp
/// <summary>
/// Executes orphan self-heal: force-zeros expectedPositions and resets orphan counter.
/// SCOPE: Account-level (clears expected position for entire account, not per-FSM).
/// </summary>
/// <param name="accountName">Account name</param>
private void ExecuteOrphanSelfHeal(string accountName)
```

**Logic**: Extract lines 62-68 from `REAPER.Repair.cs:ValidateRepairEligibility`

**Scope Documentation**:
```csharp
/// <remarks>
/// SCOPE BLAST WARNING: SetExpectedPositionLocked(ExpKey(accountName), 0) clears the expected
/// position for the ENTIRE account (all FSMs on this account-instrument pair), not just the
/// orphaned entry. This is INTENTIONAL - the 3-attempt threshold ensures this only fires after
/// sustained failure (3+ audit cycles = 3+ seconds), indicating a systemic issue requiring
/// aggressive cleanup to unblock the repair loop.
/// 
/// If the account has multiple active FSMs (rare but possible during transition), this force-zero
/// will affect all of them, potentially triggering desync detection on healthy FSMs. This is
/// acceptable given the rarity of the trigger condition and the severity of the orphan state.
/// </remarks>
```

**CYC Target**: ≤ 2

---

### State Ownership

**Moved to OrphanSafety.cs**:
```csharp
// Orphan FSM grace tracking (key = FSM entry name)
private readonly ConcurrentDictionary<string, DateTime> _orphanedPositionFirstSeen
    = new ConcurrentDictionary<string, DateTime>();

// Orphan repair attempt counter (key = account name)
private readonly ConcurrentDictionary<string, int> _reaperOrphanRepairCount
    = new ConcurrentDictionary<string, int>();
```

**Accessor Methods** (in base class `V12_002.REAPER.cs`):
```csharp
// Expose for orchestrator cleanup
internal void ClearOrphanFSMGrace(string entryName)
{
    _orphanedPositionFirstSeen.TryRemove(entryName, out _);
}

// Expose for repair success path
internal void ClearOrphanRepairCount(string accountName)
{
    _reaperOrphanRepairCount.TryRemove(accountName, out _);
}
```

---

### Integration Points

#### Caller 1: `AuditSingleFleetAccount` (REAPER.Audit.cs:127)

**BEFORE**:
```csharp
foreach (var fsm in accountFsms)
{
    if (actualQty == 0 && activePositions.ContainsKey(fsm.EntryName))
    {
        DateTime firstSeen = _orphanedPositionFirstSeen.GetOrAdd(fsm.EntryName, DateTime.UtcNow);
        double graceElapsed = (DateTime.UtcNow - firstSeen).TotalSeconds;
        if (graceElapsed > 10.0)
        {
            Print(/* diagnostic warning */);
            _orphanedPositionFirstSeen.TryRemove(fsm.EntryName, out _);
        }
    }
    else
    {
        _orphanedPositionFirstSeen.TryRemove(fsm.EntryName, out _);
    }
}
```

**AFTER**:
```csharp
foreach (var fsm in accountFsms)
{
    if (actualQty == 0 && activePositions.ContainsKey(fsm.EntryName))
    {
        DetectOrphanFSM(fsm.EntryName, acct.Name, actualQty, activePositions);
    }
    else
    {
        ClearOrphanFSMGrace(fsm.EntryName);  // NEW: Accessor method
    }
}
```

---

#### Caller 2: `ValidateRepairEligibility` (REAPER.Repair.cs:43)

**BEFORE**:
```csharp
foreach (var kvp in activePositions.ToArray())
{
    PositionInfo pi = kvp.Value;
    if (pi.IsFollower && pi.ExecutingAccount != null && pi.ExecutingAccount.Name == accountName)
    {
        repairPos = pi;
        repairEntryName = kvp.Key;
        break;
    }
}

if (repairPos == null)
{
    int orphanCount = _reaperOrphanRepairCount.AddOrUpdate(accountName, 1, (k, v) => v + 1);
    Print(/* orphan attempt log */);
    if (orphanCount >= 3)
    {
        Print(/* self-heal log */);
        SetExpectedPositionLocked(ExpKey(accountName), 0);
        _reaperOrphanRepairCount.TryRemove(accountName, out _);
    }
    return false;
}

_reaperOrphanRepairCount.TryRemove(accountName, out _);
return true;
```

**AFTER**:
```csharp
if (!ValidateRepairEligibility_OrphanCheck(accountName, activePositions, out repairPos, out repairEntryName))
{
    return false;  // Orphan detected, self-heal triggered if threshold reached
}

ClearOrphanRepairCount(accountName);  // NEW: Accessor method (success path)
return true;
```

---

## REFACTORING: V12_002.REAPER.NakedStop.cs

### Current State
**File**: `V12_002.REAPER.NakedStop.cs` (84 LOC, CYC 5)  
**Method**: `ProcessReaperNakedStopQueue` (lines 21-80)

### Extraction Plan

**Move** `CalculateEmergencyStopPrice` to `V12_002.REAPER.NakedPosition.cs` (co-locate with detection logic).

**Residual** `ProcessReaperNakedStopQueue` becomes a thin wrapper:
```csharp
private void ProcessReaperNakedStopQueue()
{
    while (_reaperNakedStopQueue.TryDequeue(out var item))
    {
        try
        {
            Account acct = Account.All.FirstOrDefault(a => a.Name == item.AccountName);
            if (acct == null)
            {
                Print(string.Format("[REAPER][NAKED_STOP] Account {0} not found -- skipping.", item.AccountName));
                ClearNakedStopInFlight(ExpKey(item.AccountName));
                continue;
            }

            var (stopPrice, closeAction) = CalculateEmergencyStopPrice(item.Direction, Close[0], item.Qty);

            string signalName = "EMERGENCY_STOP_" + item.AccountName;
            Order emergencyStop = acct.CreateOrder(
                Instrument, closeAction, OrderType.StopMarket,
                TimeInForce.Gtc, item.Qty,
                0, stopPrice, "", signalName, null);

            acct.Submit(new[] { emergencyStop });

            ClearNakedStopInFlight(ExpKey(item.AccountName));
            Print(string.Format(
                "[REAPER][EMERGENCY_STOP] Submitted StopMarket for {0}: {1} {2}ct @ {3:F2} (Dist={4:F2})",
                item.AccountName, closeAction, item.Qty, stopPrice, Math.Abs(Close[0] - stopPrice)));
        }
        catch (Exception ex)
        {
            ClearNakedStopInFlight(ExpKey(item.AccountName));
            Print(string.Format("[REAPER][EMERGENCY_STOP_FAIL] {0}: {1}", item.AccountName, ex.Message));
        }
    }
}
```

**CYC Target**: ≤ 3 (reduced from 5 via helper extraction)

---

## IMPLEMENTATION SEQUENCE

### Ticket 1: Naked Position Module Extraction
**Priority**: P1 (Foundation)  
**Agent**: Bob CLI (v12-engineer)  
**Estimated CYC Reduction**: 8 → 5 (DetectNakedPosition)

**Steps**:
1. Create `V12_002.REAPER.NakedPosition.cs` with state dictionaries
2. Extract `DetectNakedPosition` with TOCTOU fix (`GetOrAdd`)
3. Extract helpers: `CheckPendingStopReplace`, `EvaluateNakedPositionGrace`, `EnqueueEmergencyStop`
4. Extract `CalculateEmergencyStopPrice` from `NakedStop.cs` with ATR floor fix
5. Add accessor methods to `V12_002.REAPER.cs`
6. Update `AuditFleet_HandleNakedPosition` to call new module
7. Update `ProcessReaperNakedStopQueue` to call `CalculateEmergencyStopPrice`

**Verification**:
- [ ] deploy-sync.ps1 PASS
- [ ] grep -r "lock(" src/ → 0 matches
- [ ] F5 NinjaTrader: Naked position detection fires after 5s grace
- [ ] F5 NinjaTrader: Emergency stop submitted with correct price
- [ ] BUILD_TAG: `1111.007-reaper-t1`

---

### Ticket 2: Orphan Safety Module Extraction
**Priority**: P2 (Dependent on T1)  
**Agent**: Bob CLI (v12-engineer)  
**Estimated CYC Reduction**: 6 → 5 (ValidateRepairEligibility_OrphanCheck)

**Steps**:
1. Create `V12_002.REAPER.OrphanSafety.cs` with state dictionaries
2. Extract `DetectOrphanFSM` with 10s grace logic
3. Extract `ValidateRepairEligibility_OrphanCheck` with 3-attempt counter
4. Extract `ExecuteOrphanSelfHeal` with scope documentation
5. Add accessor methods to `V12_002.REAPER.cs`
6. Update `AuditSingleFleetAccount` to call `DetectOrphanFSM`
7. Update `ValidateRepairEligibility` to call `ValidateRepairEligibility_OrphanCheck`

**Verification**:
- [ ] deploy-sync.ps1 PASS
- [ ] grep -r "lock(" src/ → 0 matches
- [ ] F5 NinjaTrader: Orphan FSM diagnostic logs after 10s grace
- [ ] F5 NinjaTrader: Self-heal triggers after 3 failed repair attempts
- [ ] BUILD_TAG: `1111.007-reaper-t2`

---

## RISK MITIGATION

### Pre-Extraction Checklist

1. **State Reference Audit**
   - [ ] Grep for `_nakedPositionFirstSeen` → document all references
   - [ ] Grep for `_reaperNakedStopInFlight` → document all references
   - [ ] Grep for `_reaperNakedStopQueue` → document all references
   - [ ] Grep for `_orphanedPositionFirstSeen` → document all references
   - [ ] Grep for `_reaperOrphanRepairCount` → document all references

2. **Thread Safety Verification**
   - [ ] All `TriggerCustomEvent` calls preserved
   - [ ] All `ConcurrentDictionary` atomic operations preserved
   - [ ] No new `lock()` statements introduced

3. **Behavioral Preservation**
   - [ ] Grace periods unchanged (5s naked, 10s orphan)
   - [ ] Thresholds unchanged (3 orphan attempts)
   - [ ] Log prefixes unchanged (`[REAPER][NAKED_POSITION]`, `[REAPER][DIAGNOSTIC]`)

### Post-Extraction Verification

1. **Functional Tests**
   - [ ] Naked position detection fires after 5s grace (live session)
   - [ ] Emergency stop submitted with ATR-bounded price (live session)
   - [ ] Stop-replace suppression works (live session)
   - [ ] Orphan FSM diagnostic logs after 10s grace (restart scenario)
   - [ ] Self-heal triggers after 3 failed repairs (simulated orphan)

2. **Performance Tests**
   - [ ] Audit cycle time < 100ms per account (10 accounts)
   - [ ] Emergency stop submission latency < 50ms (P99)
   - [ ] Zero new heap allocations (profiler verification)

3. **Safety Tests**
   - [ ] grep -r "lock(" src/ → 0 matches
   - [ ] All `Print` calls ASCII-only (no Unicode)
   - [ ] deploy-sync.ps1 PASS (hard-link integrity)

---

## ACCEPTANCE CRITERIA

### Ticket 1: Naked Position Module

1. **Functional**
   - [ ] Grace period tracking preserved (5s default, configurable)
   - [ ] Stop-replace suppression logic intact
   - [ ] Emergency stop enqueue with in-flight guard functional
   - [ ] ATR-bounded stop price calculation correct (with floor applied)
   - [ ] TOCTOU race FIXED (use `GetOrAdd`)

2. **Non-Functional**
   - [ ] `DetectNakedPosition` CYC ≤ 5
   - [ ] All helpers CYC ≤ 3
   - [ ] Module LOC ≤ 150
   - [ ] Zero `lock()` statements
   - [ ] ASCII-only compliance

### Ticket 2: Orphan Safety Module

1. **Functional**
   - [ ] 10-second grace period preserved
   - [ ] Diagnostic logging after grace expiry functional
   - [ ] 3-attempt self-heal threshold intact
   - [ ] Force-zero logic triggers correctly (account-level scope documented)

2. **Non-Functional**
   - [ ] `ValidateRepairEligibility_OrphanCheck` CYC ≤ 5
   - [ ] All helpers CYC ≤ 3
   - [ ] Module LOC ≤ 100
   - [ ] Zero `lock()` statements
   - [ ] ASCII-only compliance

---

**[PLAN-GATE]**

Approach complete. Key decisions:

1. **Two-ticket extraction**: Naked Position (T1) → Orphan Safety (T2)
2. **TOCTOU fix applied**: Use `GetOrAdd` for atomic grace timestamp initialization
3. **ATR floor added**: Emergency stop distance bounded by `MinimumStop`
4. **Scope documented**: Orphan self-heal affects entire account (intentional)
5. **CYC targets**: All methods ≤ 5, helpers ≤ 3

Ready for Phase 3 (VALIDATE). Proceed?