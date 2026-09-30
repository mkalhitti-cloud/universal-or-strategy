# REAPER-EXTRACT Ticket 01: Naked Position Module Extraction
**Epic ID**: REAPER-EXTRACT  
**Ticket ID**: ticket-01-naked-position  
**Priority**: P1 (Foundation)  
**Agent**: Bob CLI (v12-engineer)  
**Estimated Duration**: 1 session  
**Dependencies**: None

---

## OBJECTIVE

Extract naked-position monitoring and emergency stop logic from `V12_002.REAPER.Audit.cs` and `V12_002.REAPER.NakedStop.cs` into a dedicated lock-free module `V12_002.REAPER.NakedPosition.cs`.

**Complexity Reduction**:
- `EnqueueReaperNakedStopCandidate`: CYC 8 → ≤ 5 (via 3 helper extractions)
- `ProcessReaperNakedStopQueue`: CYC 5 → ≤ 3 (via 1 helper extraction)

**Critical Fixes**:
- ✅ TOCTOU race in grace check (use `GetOrAdd`)
- ✅ ATR floor for emergency stop distance

---

## SCOPE

### Files to Create
- `src/V12_002.REAPER.NakedPosition.cs` (~150 LOC)

### Files to Modify
- `src/V12_002.REAPER.cs` (add accessor methods)
- `src/V12_002.REAPER.Audit.cs` (update `AuditFleet_HandleNakedPosition`)
- `src/V12_002.REAPER.NakedStop.cs` (update `ProcessReaperNakedStopQueue`)

### State to Move
```csharp
// FROM: V12_002.REAPER.cs lines 40-41, 32-33
// TO: V12_002.REAPER.NakedPosition.cs

private ConcurrentDictionary<string, DateTime> _nakedPositionFirstSeen
    = new ConcurrentDictionary<string, DateTime>();

private readonly ConcurrentDictionary<string, byte> _reaperNakedStopInFlight
    = new ConcurrentDictionary<string, byte>();

private ConcurrentQueue<(string AccountName, MarketPosition Direction, int Qty)> _reaperNakedStopQueue
    = new ConcurrentQueue<(string, MarketPosition, int)>();
```

---

## IMPLEMENTATION STEPS

### Step 1: Create Module Skeleton

Create `src/V12_002.REAPER.NakedPosition.cs`:

```csharp
// <copyright file="V12_002.REAPER.NakedPosition.cs" company="BMad">
// Copyright (c) BMad. All rights reserved.
// </copyright>
// V12 REAPER Naked Position Detection Module
// Build 1111.007-reaper-t1: Extracted from REAPER.Audit.cs and REAPER.NakedStop.cs
// Jane Street Alignment: Atomic state transitions, wait-free progress, bounded latency
using System;
using System.Collections.Concurrent;
using System.Linq;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        #region V12 REAPER Naked Position Detection

        // Grace period tracking (key = account name)
        private ConcurrentDictionary<string, DateTime> _nakedPositionFirstSeen
            = new ConcurrentDictionary<string, DateTime>();

        // In-flight guard (key = expectedKey = accountName_instrumentName)
        private readonly ConcurrentDictionary<string, byte> _reaperNakedStopInFlight
            = new ConcurrentDictionary<string, byte>();

        // Emergency stop queue (marshalled to strategy thread)
        private ConcurrentQueue<(string AccountName, MarketPosition Direction, int Qty)> _reaperNakedStopQueue
            = new ConcurrentQueue<(string, MarketPosition, int)>();

        #endregion
    }
}
```

**Verification**: File compiles, no syntax errors.

---

### Step 2: Extract `DetectNakedPosition` (Primary Entry Point)

Add to `V12_002.REAPER.NakedPosition.cs`:

```csharp
/// <summary>
/// Detects naked positions (position without working stop) and enqueues emergency stop after grace period.
/// Thread-safe: Called from audit thread via TriggerCustomEvent marshalling.
/// Jane Street Alignment: Atomic state transitions via GetOrAdd (TOCTOU-safe).
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
{
    // H17-GUARD: Prevent new enqueues after shutdown initiated
    if (_isTerminating)
        return false;

    // Check for pending stop-replace (suppression logic)
    if (CheckPendingStopReplace(acct, pendingStopReplacements, activePositions))
    {
        _nakedPositionFirstSeen.TryRemove(acct.Name, out _);
        if (shouldLog)
            Print(string.Format("[REAPER] {0}: Stop replace in flight -- suppressing naked audit.", acct.Name));
        return false;
    }

    // Evaluate grace period
    int graceSeconds = (NakedPositionGraceSec >= 5) ? NakedPositionGraceSec : 5;
    if (!EvaluateNakedPositionGrace(acct.Name, actualQty, graceSeconds, shouldLog))
    {
        return false;  // Grace active or just started
    }

    // Grace expired - enqueue emergency stop
    DateTime firstSeen = _nakedPositionFirstSeen[acct.Name];  // Safe: GetOrAdd already called in EvaluateNakedPositionGrace
    double graceElapsed = (DateTime.UtcNow - firstSeen).TotalSeconds;
    return EnqueueEmergencyStop(acct.Name, pos, actualQty, expectedKey, graceElapsed);
}
```

**CYC Target**: ≤ 5 (orchestrator only, delegates to 3 helpers)

**Verification**: Method compiles, calls 3 helpers (to be extracted next).

---

### Step 3: Extract `CheckPendingStopReplace` (Helper 1)

Add to `V12_002.REAPER.NakedPosition.cs`:

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
{
    foreach (var psr in pendingStopReplacements.Values)
    {
        PositionInfo psrPos;
        if (
            activePositions.TryGetValue(psr.EntryName, out psrPos)
            && psrPos != null
            && psrPos.ExecutingAccount != null
            && psrPos.ExecutingAccount.Name == acct.Name
        )
        {
            return true;
        }
    }
    return false;
}
```

**CYC Target**: ≤ 3

**Verification**: Method compiles, logic matches original (REAPER.Audit.cs:499-513).

---

### Step 4: Extract `EvaluateNakedPositionGrace` (Helper 2 with TOCTOU Fix)

Add to `V12_002.REAPER.NakedPosition.cs`:

```csharp
/// <summary>
/// Evaluates naked position grace period. Records first detection or checks elapsed time.
/// TOCTOU-safe: Uses GetOrAdd for atomic timestamp initialization.
/// Jane Street Alignment: Atomic state transition via CAS operation.
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
{
    // TOCTOU FIX: GetOrAdd is atomic (CAS operation)
    DateTime firstSeen = _nakedPositionFirstSeen.GetOrAdd(accountName, DateTime.UtcNow);
    double graceElapsed = (DateTime.UtcNow - firstSeen).TotalSeconds;

    if (graceElapsed < graceSeconds)
    {
        // Grace active - log on first detection only (within first 500ms)
        if (graceElapsed < 0.5)
        {
            Print(
                string.Format(
                    "[REAPER][NAKED_POSITION] {0}: {1}ct naked -- starting {2}s grace window.",
                    accountName,
                    actualQty,
                    graceSeconds
                )
            );
        }
        return false;  // Grace active
    }

    return true;  // Grace expired
}
```

**CYC Target**: ≤ 3

**TOCTOU Fix Verification**:
- ✅ `GetOrAdd` is atomic (CAS operation)
- ✅ No race where two threads both insert
- ✅ No intermediate state where key exists but value is null

---

### Step 5: Extract `EnqueueEmergencyStop` (Helper 3)

Add to `V12_002.REAPER.NakedPosition.cs`:

```csharp
/// <summary>
/// Enqueues emergency stop request with in-flight guard (prevents duplicates).
/// Jane Street Alignment: Wait-free progress via TryAdd (fail-fast on duplicate).
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
{
    // H16-FIX: Atomic TryAdd check prevents duplicate naked stop submissions.
    if (!_reaperNakedStopInFlight.TryAdd(expectedKey, 0))
    {
        // Already in flight - skip
        return false;
    }

    Print(
        string.Format(
            "[REAPER][NAKED_POSITION] {0}: {1}ct CONFIRMED naked after {2:F1}s grace. Queuing emergency hard stop.",
            accountName,
            actualQty,
            graceElapsed
        )
    );

    _reaperNakedStopQueue.Enqueue((accountName, pos.MarketPosition, Math.Abs(actualQty)));
    return true;
}
```

**CYC Target**: ≤ 2

**Verification**: Method compiles, logic matches original (REAPER.Audit.cs:539-554).

---

### Step 6: Extract `CalculateEmergencyStopPrice` (Helper 4 with ATR Floor Fix)

Add to `V12_002.REAPER.NakedPosition.cs`:

```csharp
/// <summary>
/// Calculates ATR-bounded emergency stop price with defensive floor.
/// Jane Street Alignment: Bounded latency via deterministic calculation (no I/O, no blocking).
/// </summary>
/// <param name="direction">Position direction (Long/Short)</param>
/// <param name="currentClose">Current bar close price</param>
/// <param name="qty">Position quantity (unsigned)</param>
/// <returns>Tuple of (stopPrice, closeAction)</returns>
private (double stopPrice, OrderAction closeAction) CalculateEmergencyStopPrice(
    MarketPosition direction,
    double currentClose,
    int qty)
{
    // Compute emergency stop distance: MaximumStop vs ATR-bound (use tighter)
    double emergencyStopDist = MaximumStop;
    double atrBound = CalculateATRStopDistance(RMAStopATRMultiplier);

    if (atrBound > 0)
    {
        // ATR FLOOR FIX: Prevent stops tighter than MinimumStop
        atrBound = Math.Max(atrBound, MinimumStop);
        emergencyStopDist = Math.Min(emergencyStopDist, atrBound);
    }

    // Fallback: If result is still invalid, use MinimumStop
    if (emergencyStopDist <= 0)
        emergencyStopDist = Math.Max(tickSize, MinimumStop);

    // Calculate stop price and close action based on direction
    double stopPrice;
    OrderAction closeAction;

    if (direction == MarketPosition.Long)
    {
        stopPrice = Instrument.MasterInstrument.RoundToTickSize(currentClose - emergencyStopDist);
        closeAction = OrderAction.Sell;
    }
    else
    {
        stopPrice = Instrument.MasterInstrument.RoundToTickSize(currentClose + emergencyStopDist);
        closeAction = OrderAction.BuyToCover;
    }

    return (stopPrice, closeAction);
}
```

**CYC Target**: ≤ 3

**ATR Floor Fix Verification**:
- ✅ `atrBound = Math.Max(atrBound, MinimumStop)` prevents stops tighter than user-configured minimum
- ✅ Fallback to `Math.Max(tickSize, MinimumStop)` still present (defensive)

---

### Step 7: Add Accessor Methods to Base Class

Add to `src/V12_002.REAPER.cs` (after existing helper methods):

```csharp
// Build 1111.007-reaper-t1: Accessor methods for NakedPosition module

/// <summary>
/// Clears naked position grace timestamp (called when working stop is detected).
/// </summary>
internal void ClearNakedPositionGrace(string accountName)
{
    _nakedPositionFirstSeen.TryRemove(accountName, out _);
}

/// <summary>
/// Clears naked stop in-flight guard (called after emergency stop submission or on error).
/// </summary>
internal void ClearNakedStopInFlight(string expectedKey)
{
    _reaperNakedStopInFlight.TryRemove(expectedKey, out _);
}
```

**Verification**: Methods compile, match accessor pattern from existing helpers.

---

### Step 8: Update `AuditFleet_HandleNakedPosition` Caller

Modify `src/V12_002.REAPER.Audit.cs` lines 304-339:

**BEFORE**:
```csharp
private void AuditFleet_HandleNakedPosition(
    Account acct,
    Position pos,
    int actualQty,
    string expectedKey,
    bool shouldLog
)
{
    bool hasWorkingStop = AuditFleet_CheckWorkingStop(acct);

    if (!hasWorkingStop)
    {
        if (EnqueueReaperNakedStopCandidate(acct, pos, actualQty, expectedKey, shouldLog))
        {
            try
            {
                TriggerCustomEvent(e => ProcessReaperNakedStopQueue(), null);
            }
            catch (Exception tcEx)
            {
                _reaperNakedStopInFlight.TryRemove(expectedKey, out _);
                Print(
                    string.Format(
                        "[REAPER][NAKED_STOP] TriggerCustomEvent failed for {0}: {1} -- in-flight cleared.",
                        acct.Name,
                        tcEx.Message
                    )
                );
            }
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
private void AuditFleet_HandleNakedPosition(
    Account acct,
    Position pos,
    int actualQty,
    string expectedKey,
    bool shouldLog
)
{
    bool hasWorkingStop = AuditFleet_CheckWorkingStop(acct);

    if (!hasWorkingStop)
    {
        if (DetectNakedPosition(acct, pos, actualQty, expectedKey, shouldLog, pendingStopReplacements, activePositions))
        {
            try
            {
                TriggerCustomEvent(e => ProcessReaperNakedStopQueue(), null);
            }
            catch (Exception tcEx)
            {
                ClearNakedStopInFlight(expectedKey);  // NEW: Accessor method
                Print(
                    string.Format(
                        "[REAPER][NAKED_STOP] TriggerCustomEvent failed for {0}: {1} -- in-flight cleared.",
                        acct.Name,
                        tcEx.Message
                    )
                );
            }
        }
    }
    else
    {
        ClearNakedPositionGrace(acct.Name);  // NEW: Accessor method
    }
}
```

**Verification**: Method compiles, calls new `DetectNakedPosition` and accessor methods.

---

### Step 9: Update `ProcessReaperNakedStopQueue` Processor

Modify `src/V12_002.REAPER.NakedStop.cs` lines 21-80:

**BEFORE**:
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
                _reaperNakedStopInFlight.TryRemove(ExpKey(item.AccountName), out _);
                continue;
            }

            // Compute emergency stop price: MaximumStop ticks from current close.
            double emergencyStopDist = MaximumStop;
            double atrBound = CalculateATRStopDistance(RMAStopATRMultiplier);
            if (atrBound > 0)
                emergencyStopDist = Math.Min(emergencyStopDist, atrBound);
            if (emergencyStopDist <= 0)
                emergencyStopDist = Math.Max(tickSize, MinimumStop);

            double stopPrice;
            OrderAction closeAction;

            if (item.Direction == MarketPosition.Long)
            {
                stopPrice   = Instrument.MasterInstrument.RoundToTickSize(Close[0] - emergencyStopDist);
                closeAction = OrderAction.Sell;
            }
            else
            {
                stopPrice   = Instrument.MasterInstrument.RoundToTickSize(Close[0] + emergencyStopDist);
                closeAction = OrderAction.BuyToCover;
            }

            string signalName = "EMERGENCY_STOP_" + item.AccountName;
            Order emergencyStop = acct.CreateOrder(
                Instrument, closeAction, OrderType.StopMarket,
                TimeInForce.Gtc, item.Qty,
                0, stopPrice, "", signalName, null);

            acct.Submit(new[] { emergencyStop });

            _reaperNakedStopInFlight.TryRemove(ExpKey(item.AccountName), out _);
            Print(string.Format(
                "[REAPER][EMERGENCY_STOP] Submitted StopMarket for {0}: {1} {2}ct @ {3:F2} (Dist={4:F2})",
                item.AccountName, closeAction, item.Qty, stopPrice, emergencyStopDist));
        }
        catch (Exception ex)
        {
            _reaperNakedStopInFlight.TryRemove(ExpKey(item.AccountName), out _);
            Print(string.Format("[REAPER][EMERGENCY_STOP_FAIL] {0}: {1}", item.AccountName, ex.Message));
        }
    }
}
```

**AFTER**:
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
                ClearNakedStopInFlight(ExpKey(item.AccountName));  // NEW: Accessor method
                continue;
            }

            // NEW: Call extracted helper with ATR floor fix
            var (stopPrice, closeAction) = CalculateEmergencyStopPrice(item.Direction, Close[0], item.Qty);

            string signalName = "EMERGENCY_STOP_" + item.AccountName;
            Order emergencyStop = acct.CreateOrder(
                Instrument, closeAction, OrderType.StopMarket,
                TimeInForce.Gtc, item.Qty,
                0, stopPrice, "", signalName, null);

            acct.Submit(new[] { emergencyStop });

            ClearNakedStopInFlight(ExpKey(item.AccountName));  // NEW: Accessor method
            Print(string.Format(
                "[REAPER][EMERGENCY_STOP] Submitted StopMarket for {0}: {1} {2}ct @ {3:F2} (Dist={4:F2})",
                item.AccountName, closeAction, item.Qty, stopPrice, Math.Abs(Close[0] - stopPrice)));
        }
        catch (Exception ex)
        {
            ClearNakedStopInFlight(ExpKey(item.AccountName));  // NEW: Accessor method
            Print(string.Format("[REAPER][EMERGENCY_STOP_FAIL] {0}: {1}", item.AccountName, ex.Message));
        }
    }
}
```

**Verification**: Method compiles, calls `CalculateEmergencyStopPrice` and accessor methods.

---

### Step 10: Remove Extracted Code from Original Files

**Delete from `src/V12_002.REAPER.cs`**:
- Lines 40-41: `_nakedPositionFirstSeen` declaration
- Lines 32-33: `_reaperNakedStopQueue` declaration
- Lines 34-35: `_reaperNakedStopInFlight` declaration

**Delete from `src/V12_002.REAPER.Audit.cs`**:
- Lines 488-559: `EnqueueReaperNakedStopCandidate` method (entire method)

**Verification**: Files compile after deletion, no orphaned references.

---

### Step 11: Update BUILD_TAG

Modify `src/V12_002.cs` line 47:

**BEFORE**:
```csharp
public const string BUILD_TAG = "1111.007-mphase-mp0";
```

**AFTER**:
```csharp
public const string BUILD_TAG = "1111.007-reaper-t1";
```

**Verification**: BUILD_TAG updated, matches ticket ID.

---

## VERIFICATION CHECKLIST

### Pre-Deployment

- [ ] All files compile without errors
- [ ] grep -r "lock(" src/ → 0 matches
- [ ] grep -r "_nakedPositionFirstSeen" src/ → only in NakedPosition.cs and accessor methods
- [ ] grep -r "_reaperNakedStopInFlight" src/ → only in NakedPosition.cs and accessor methods
- [ ] grep -r "_reaperNakedStopQueue" src/ → only in NakedPosition.cs
- [ ] All string literals ASCII-only (no Unicode)

### Deployment

- [ ] `powershell -File .\deploy-sync.ps1` → PASS
- [ ] ASCII gate → PASS
- [ ] DIFF guard → PASS (< 10,000 characters)

### F5 Verification (Live NinjaTrader)

- [ ] Strategy loads without errors
- [ ] Naked position detection fires after 5s grace (simulate by removing stop manually)
- [ ] Emergency stop submitted with correct price (check order log)
- [ ] Stop-replace suppression works (naked audit suppressed during stop-replace)
- [ ] Log prefixes correct: `[REAPER][NAKED_POSITION]`, `[REAPER][EMERGENCY_STOP]`

### Performance Verification

- [ ] Audit cycle time < 100ms per account (10 accounts)
- [ ] Emergency stop submission latency < 50ms (P99)
- [ ] Zero new heap allocations on common path (profiler)

---

## ACCEPTANCE CRITERIA

### Functional

1. **Naked Position Detection**
   - [ ] Grace period tracking preserved (5s default, configurable via `NakedPositionGraceSec`)
   - [ ] Stop-replace suppression logic intact (checks `pendingStopReplacements`)
   - [ ] Emergency stop enqueue with in-flight guard functional (`TryAdd` prevents duplicates)
   - [ ] ATR-bounded stop price calculation correct (with floor applied: `Math.Max(atrBound, MinimumStop)`)
   - [ ] TOCTOU race FIXED (use `GetOrAdd` instead of `TryGetValue` + indexer write)

2. **Emergency Stop Submission**
   - [ ] Queue processor runs on strategy thread (via `TriggerCustomEvent`)
   - [ ] Stop price calculated correctly (ATR-bounded with floor)
   - [ ] Order submission successful (via `acct.CreateOrder` + `acct.Submit`)
   - [ ] In-flight guard cleared after submission or on error

### Non-Functional

3. **Complexity Reduction**
   - [ ] `DetectNakedPosition` CYC ≤ 5 (verify via `python scripts/complexity_audit.py`)
   - [ ] `CheckPendingStopReplace` CYC ≤ 3
   - [ ] `EvaluateNakedPositionGrace` CYC ≤ 3
   - [ ] `EnqueueEmergencyStop` CYC ≤ 2
   - [ ] `CalculateEmergencyStopPrice` CYC ≤ 3
   - [ ] Module LOC ≤ 150

4. **V12 DNA Compliance**
   - [ ] Zero `lock()` statements in new module
   - [ ] All state mutations atomic (CAS or queue-based)
   - [ ] ASCII-only compliance (all string literals)
   - [ ] Zero new heap allocations on common path

5. **Jane Street Alignment**
   - [ ] Atomic state transitions (via `GetOrAdd`, `TryAdd`)
   - [ ] Wait-free progress (no blocking operations)
   - [ ] Memory ordering correct (via `TriggerCustomEvent`)
   - [ ] Bounded latency (all operations O(1) or O(N) where N < 10)

---

## ROLLBACK PLAN

If F5 verification fails:

1. Revert `src/V12_002.REAPER.NakedPosition.cs` (delete file)
2. Revert `src/V12_002.REAPER.cs` (restore state declarations, remove accessor methods)
3. Revert `src/V12_002.REAPER.Audit.cs` (restore `EnqueueReaperNakedStopCandidate`)
4. Revert `src/V12_002.REAPER.NakedStop.cs` (restore inline stop price calculation)
5. Revert BUILD_TAG to `1111.007-mphase-mp0`
6. Run `powershell -File .\deploy-sync.ps1`
7. F5 NinjaTrader to verify rollback successful

---

**[TICKET-GATE]**

Ticket 01 ready for execution. Dependencies: None.

Proceed to Ticket 02 (Orphan Safety) after this ticket is Director-accepted.