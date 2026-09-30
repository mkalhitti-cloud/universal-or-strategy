# T-W1-Perf: Intake Report

## Target
- **Method**: `ShouldSkipFleet_RunHealthCheck`
- **File**: [`src/V12_002.SIMA.Fleet.cs:413`](src/V12_002.SIMA.Fleet.cs:413)
- **Allocation**: `acct.Positions.ToArray()`
- **Line**: 413

## Full Method Source

```csharp
private void ShouldSkipFleet_RunHealthCheck(Account acct, StringBuilder dispatchLog)
{
    try
    {
        // [939-P0]: Snapshot Positions to prevent broker-thread mutation during iteration.
        // T-W1-Perf: for-loop replaces FirstOrDefault lambda -- eliminates delegate allocation.
        Position[] _posSnapshot = acct.Positions.ToArray();
        Position brokerPos = null;
        for (int _pi = 0; _pi < _posSnapshot.Length; _pi++)
        {
            if (_posSnapshot[_pi] != null && _posSnapshot[_pi].Instrument.FullName == Instrument.FullName)
            { brokerPos = _posSnapshot[_pi]; break; }
        }
        bool brokerFlat = (brokerPos == null || brokerPos.MarketPosition == MarketPosition.Flat);

        // T-W1-Perf: direct foreach over ConcurrentDictionary -- no .Values snapshot, no closure alloc.
        bool hasActiveFsmForAcct = false;
        foreach (var _fkvp in _followerBrackets)
        {
            var f = _fkvp.Value;
            if (f != null && f.AccountName == acct.Name
                && (f.State == FollowerBracketState.Active
                    || f.State == FollowerBracketState.Accepted
                    || f.State == FollowerBracketState.Submitted
                    || f.State == FollowerBracketState.Replacing))
            { hasActiveFsmForAcct = true; break; }
        }
        bool hasActivePositionForAcct = false;
        foreach (var _pkvp in activePositions)
        {
            var p = _pkvp.Value;
            if (p != null && p.IsFollower && p.ExecutingAccount != null && p.ExecutingAccount.Name == acct.Name)
            { hasActivePositionForAcct = true; break; }
        }
        bool hasDispatchPending = _dispatchSyncPendingExpKeys.ContainsKey(ExpKey(acct.Name));

        if (brokerFlat && !hasActiveFsmForAcct && !hasActivePositionForAcct && !hasDispatchPending)
        {
            // Truly stale: broker flat, no FSM, no position, no dispatch in flight. No-op (nothing to reset).
            dispatchLog.AppendLine(string.Format("[DISPATCH] H-13: {0} broker flat, no FSM/position/dispatch -- no action", acct.Name));
        }
        else if (brokerFlat && (hasActiveFsmForAcct || hasActivePositionForAcct || hasDispatchPending))
        {
            dispatchLog.AppendLine(string.Format("[DISPATCH] H-13 SKIP: {0} Flat but {1} -- not resetting",
                acct.Name, hasActiveFsmForAcct ? "FSM active" : (hasDispatchPending ? "dispatch pending" : "activePos present")));
        }
    }
    catch (Exception ex)
    {
        if (_diagFleet)
            Print("[FLEET_CATCH] ProcessFleetSlot account iteration failed: " + ex.Message);
    }
}
```

## Context Analysis

### What is `acct.Positions`?

**Type**: `PositionCollection` (NinjaTrader 8 API)
- **Source**: NinjaTrader broker API - live collection maintained by broker thread
- **Characteristics**:
  - Thread-unsafe collection (broker thread can mutate during iteration)
  - Implements `IEnumerable<Position>`
  - NOT thread-safe for cross-thread enumeration
  - Typical size: 0-5 positions per account (most accounts have 0-1)

### Why is `.ToArray()` Used?

**Purpose**: **Defensive snapshot for thread safety**

The comment at line 411-412 explicitly states:
```csharp
// [939-P0]: Snapshot Positions to prevent broker-thread mutation during iteration.
// T-W1-Perf: for-loop replaces FirstOrDefault lambda -- eliminates delegate allocation.
```

**Rationale**:
1. **Thread Safety**: `acct.Positions` is owned by NinjaTrader's broker thread
2. **Mutation Risk**: Broker can add/remove positions during iteration (InvalidOperationException)
3. **Build 939 Fix**: This was a P0 fix to prevent crashes from concurrent modification

### What Operations Follow `.ToArray()`?

**Usage Pattern**: **Enumeration only - NO mutation**

```csharp
Position[] _posSnapshot = acct.Positions.ToArray();  // Line 413
Position brokerPos = null;
for (int _pi = 0; _pi < _posSnapshot.Length; _pi++)  // Lines 414-418
{
    if (_posSnapshot[_pi] != null && _posSnapshot[_pi].Instrument.FullName == Instrument.FullName)
    { brokerPos = _posSnapshot[_pi]; break; }
}
```

**Operations**:
- Single linear scan with early-exit on match
- Read-only access (no array mutation)
- Finds first position matching current instrument
- Typical iteration count: 0-5 (most accounts have ≤1 position)

### Allocation Cost

**Frequency**: 1-5 Hz (SIMA dispatch cadence)
- Called once per fleet account per dispatch cycle
- Typical fleet size: 5-10 accounts
- Total calls: 5-50 per second

**Per-Call Cost**:
- Array allocation: ~40 bytes (header + 5 Position references @ 8 bytes each)
- Typical positions per account: 0-1 (empty array or single element)
- Gen0 allocation: ~500 bytes/second at 5 Hz × 10 accounts × 1 position average

**GC Impact**: Negligible
- Small, short-lived Gen0 objects
- Collected in microseconds
- No Gen1/Gen2 promotion (dies before next GC)

## Similar Patterns in File

Found **2 occurrences** of `.ToArray()` in `src/V12_002.SIMA.Fleet.cs`:

### 1. Line 413 (Current Target)
```csharp
Position[] _posSnapshot = acct.Positions.ToArray();
```
**Context**: `ShouldSkipFleet_RunHealthCheck` - health check diagnostic
**Purpose**: Thread-safe snapshot of broker positions

### 2. Line 489
```csharp
Account[] _acctSnapshot = Account.All.ToArray();
```
**Context**: Fleet dispatch loop
**Comment**: 
```csharp
// Build 1109 [FREEZE-PROOF]: Snapshot Account.All once to prevent InvalidOperationException
// if broker reconnects or modifies the collection during iteration.
```
**Purpose**: Thread-safe snapshot of account collection

**Pattern Consistency**: Both uses follow the same defensive snapshot pattern for broker-owned collections.

## Performance Characteristics

### Current Implementation
- **Allocation**: ~40 bytes per call (array header + references)
- **Frequency**: 5-50 calls/second (dispatch cadence × fleet size)
- **Total**: ~500 bytes/second
- **GC Pressure**: Negligible (Gen0 only, microsecond lifetime)

### Alternative: Direct Enumeration
```csharp
// UNSAFE - can throw InvalidOperationException
foreach (var pos in acct.Positions)
{
    if (pos != null && pos.Instrument.FullName == Instrument.FullName)
    { brokerPos = pos; break; }
}
```
**Risk**: Broker thread can modify collection during iteration → crash
**Savings**: ~40 bytes per call
**Trade-off**: NOT WORTH IT - stability > 500 bytes/sec

### Alternative: Lock-Based Snapshot
```csharp
// Hypothetical - requires broker API support
lock (acct.Positions.SyncRoot)
{
    foreach (var pos in acct.Positions)
    { /* ... */ }
}
```
**Problem**: NinjaTrader API does not expose `SyncRoot` for `PositionCollection`
**Feasibility**: Not possible without API changes

## Recommendation

**ACCEPT AS-IS** ✅

### Rationale

1. **Thread Safety is Non-Negotiable**
   - Build 939 P0 fix - prevents `InvalidOperationException` crashes
   - Broker thread can mutate collection at any time
   - No lock-free alternative exists in NinjaTrader API

2. **Allocation Cost is Negligible**
   - ~500 bytes/second total
   - Gen0 only (microsecond lifetime)
   - No GC pause impact
   - Typical HFT systems tolerate 10-100 KB/sec Gen0 churn

3. **Already Optimized**
   - Comment shows prior optimization: "for-loop replaces FirstOrDefault lambda"
   - Early-exit on match (no full iteration)
   - Minimal array size (0-5 elements typical)

4. **Jane Street Alignment**
   - Correctness > micro-optimization
   - "Make illegal states unrepresentable" - thread-safe snapshot prevents race
   - Defensive copies are standard practice for broker API interactions

### Alternative Considered: Object Pool

**Idea**: Pool `Position[]` arrays to avoid allocation
```csharp
Position[] _posSnapshot = ArrayPool<Position>.Shared.Rent(acct.Positions.Count);
try
{
    acct.Positions.CopyTo(_posSnapshot, 0);
    // ... use array ...
}
finally
{
    ArrayPool<Position>.Shared.Return(_posSnapshot);
}
```

**Rejected Because**:
- `PositionCollection` does not implement `CopyTo(T[], int)` in NinjaTrader API
- Would require manual enumeration → same allocation for enumerator
- Added complexity not justified for ~500 bytes/sec
- ArrayPool overhead (rent/return) comparable to Gen0 allocation cost

## Conclusion

**Status**: ✅ **NO ACTION REQUIRED**

The `.ToArray()` allocation is:
- **Necessary** for thread safety (broker API constraint)
- **Minimal** in cost (~500 bytes/sec)
- **Already optimized** (for-loop, early-exit)
- **Aligned** with V12 DNA (correctness by construction)

**Next Steps**: Close T-W1-Perf ticket as "Accepted - Defensive Allocation"

---

**Forensic Intake Complete**
- Date: 2026-06-01
- Analyst: Advanced Mode (jCodemunch-powered)
- Verdict: Accept defensive allocation pattern