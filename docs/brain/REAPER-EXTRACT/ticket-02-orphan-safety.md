# REAPER-EXTRACT Ticket 02: Orphan Safety Module Extraction
**Epic ID**: REAPER-EXTRACT  
**Ticket ID**: ticket-02-orphan-safety  
**Priority**: P2 (Dependent on T1)  
**Agent**: Bob CLI (v12-engineer)  
**Estimated Duration**: 1 session  
**Dependencies**: ticket-01-naked-position (must be Director-accepted and F5-verified)

---

## OBJECTIVE

Extract orphan FSM detection and self-heal logic from `V12_002.REAPER.Audit.cs` and `V12_002.REAPER.Repair.cs` into a dedicated lock-free module `V12_002.REAPER.OrphanSafety.cs`.

**Complexity Reduction**:
- `ValidateRepairEligibility` (orphan logic): CYC 6 → ≤ 5 (via 2 helper extractions)

**Critical Documentation**:
- ✅ Orphan self-heal scope blast (account-level force-zero is intentional)

---

## SCOPE

### Files to Create
- `src/V12_002.REAPER.OrphanSafety.cs` (~100 LOC)

### Files to Modify
- `src/V12_002.REAPER.cs` (add accessor methods)
- `src/V12_002.REAPER.Audit.cs` (update `AuditSingleFleetAccount`)
- `src/V12_002.REAPER.Repair.cs` (update `ValidateRepairEligibility`)

### State to Move
```csharp
// FROM: V12_002.REAPER.cs lines 68, 61
// TO: V12_002.REAPER.OrphanSafety.cs

private readonly ConcurrentDictionary<string, DateTime> _orphanedPositionFirstSeen
    = new ConcurrentDictionary<string, DateTime>();

private readonly ConcurrentDictionary<string, int> _reaperOrphanRepairCount
    = new ConcurrentDictionary<string, int>();
```

---

## IMPLEMENTATION STEPS

### Step 1: Create Module Skeleton

Create `src/V12_002.REAPER.OrphanSafety.cs`:

```csharp
// <copyright file="V12_002.REAPER.OrphanSafety.cs" company="BMad">
// Copyright (c) BMad. All rights reserved.
// </copyright>
// V12 REAPER Orphan FSM Detection & Self-Heal Module
// Build 1111.007-reaper-t2: Extracted from REAPER.Audit.cs and REAPER.Repair.cs
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
        #region V12 REAPER Orphan FSM Detection & Self-Heal

        // Orphan FSM grace tracking (key = FSM entry name)
        private readonly ConcurrentDictionary<string, DateTime> _orphanedPositionFirstSeen
            = new ConcurrentDictionary<string, DateTime>();

        // Orphan repair attempt counter (key = account name)
        private readonly ConcurrentDictionary<string, int> _reaperOrphanRepairCount
            = new ConcurrentDictionary<string, int>();

        #endregion
    }
}
```

**Verification**: File compiles, no syntax errors.

---

### Step 2: Extract `DetectOrphanFSM` (Diagnostic Entry Point)

Add to `V12_002.REAPER.OrphanSafety.cs`:

```csharp
/// <summary>
/// Detects orphaned FSM positions (broker flat but activePositions entry exists) after 10s grace.
/// Diagnostic only - logs warning but does NOT trigger flatten (non-fatal assertion).
/// Jane Street Alignment: Atomic state transitions via GetOrAdd (TOCTOU-safe).
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
{
    // Only detect orphans when broker is flat AND activePositions entry exists
    if (actualQty != 0 || !activePositions.ContainsKey(entryName))
    {
        // Position is live or activePositions is clean -- clear first-seen timestamp
        _orphanedPositionFirstSeen.TryRemove(entryName, out _);
        return false;
    }

    // Check if grace period has expired (10 seconds)
    DateTime firstSeen = _orphanedPositionFirstSeen.GetOrAdd(entryName, DateTime.UtcNow);
    double graceElapsed = (DateTime.UtcNow - firstSeen).TotalSeconds;

    if (graceElapsed > 10.0)
    {
        // Grace expired -- log diagnostic warning
        Print(
            string.Format(
                "[REAPER][DIAGNOSTIC] Orphaned FSM position detected: {0} entry={1}. "
                    + "Broker flat but activePositions entry exists after {2:F1}s grace. "
                    + "This may indicate a TOCTOU race in entry rollback logic.",
                accountName,
                entryName,
                graceElapsed
            )
        );

        // Clear first-seen timestamp to avoid log spam
        _orphanedPositionFirstSeen.TryRemove(entryName, out _);
        return true;
    }

    return false;  // Grace active
}
```

**CYC Target**: ≤ 4

**Verification**: Method compiles, logic matches original (REAPER.Audit.cs:127-153).

---

### Step 3: Extract `ValidateRepairEligibility_OrphanCheck` (Repair Integration)

Add to `V12_002.REAPER.OrphanSafety.cs`:

```csharp
/// <summary>
/// Validates repair eligibility with orphan self-heal logic.
/// Increments orphan counter on PositionInfo lookup failure.
/// Triggers force-zero self-heal after 3 failed attempts.
/// Jane Street Alignment: Atomic state transitions via AddOrUpdate (CAS operation).
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
{
    repairPos = null;
    repairEntryName = null;

    // 1. Find the stored PositionInfo for this account in activePositions
    foreach (var kvp in activePositions.ToArray())
    {
        PositionInfo pi = kvp.Value;
        if (pi.IsFollower && pi.ExecutingAccount != null
            && pi.ExecutingAccount.Name == accountName)
        {
            repairPos = pi;
            repairEntryName = kvp.Key;
            break;
        }
    }

    if (repairPos == null)
    {
        // Orphan detected - increment counter
        int orphanCount = _reaperOrphanRepairCount.AddOrUpdate(accountName, 1, (k, v) => v + 1);
        Print(string.Format("[REAPER REPAIR] x No PositionInfo found for {0} -- cannot repair. (orphan attempt {1}/3)",
            accountName, orphanCount));

        if (orphanCount >= 3)
        {
            // Threshold reached - trigger self-heal
            ExecuteOrphanSelfHeal(accountName);
        }
        return false;
    }

    // Clear orphan counter on successful PositionInfo resolution
    _reaperOrphanRepairCount.TryRemove(accountName, out _);
    return true;
}
```

**CYC Target**: ≤ 5

**Verification**: Method compiles, logic matches original (REAPER.Repair.cs:43-75).

---

### Step 4: Extract `ExecuteOrphanSelfHeal` (Helper with Scope Documentation)

Add to `V12_002.REAPER.OrphanSafety.cs`:

```csharp
/// <summary>
/// Executes orphan self-heal: force-zeros expectedPositions and resets orphan counter.
/// SCOPE: Account-level (clears expected position for entire account, not per-FSM).
/// Jane Street Alignment: Atomic state transition via SetExpectedPositionLocked.
/// </summary>
/// <param name="accountName">Account name</param>
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
/// 
/// RATIONALE: Orphan state indicates a fundamental breakdown in FSM lifecycle management. After
/// 3 failed repair attempts (3+ seconds of sustained failure), the safest action is to reset
/// the account's expected position to zero, allowing the next audit cycle to detect the true
/// broker state and re-establish synchronization. This is a "nuclear option" that trades
/// potential false-positive desync detection for guaranteed recovery from orphan deadlock.
/// </remarks>
private void ExecuteOrphanSelfHeal(string accountName)
{
    Print(string.Format("[REAPER] SELF-HEAL: {0} has no PositionInfo after 3 attempts. Force-zeroing expectedPositions to unblock repair loop.",
        accountName));

    // SetExpectedPositionLocked(..., 0) already removes from _dispatchSyncPendingExpKeys internally.
    SetExpectedPositionLocked(ExpKey(accountName), 0);

    // Reset orphan counter
    _reaperOrphanRepairCount.TryRemove(accountName, out _);
}
```

**CYC Target**: ≤ 2

**Scope Documentation Verification**:
- ✅ SCOPE BLAST WARNING clearly states account-level impact
- ✅ INTENTIONAL keyword emphasizes design decision
- ✅ RATIONALE explains why this aggressive action is necessary
- ✅ Rarity of trigger condition documented (3+ seconds of sustained failure)

---

### Step 5: Add Accessor Methods to Base Class

Add to `src/V12_002.REAPER.cs` (after existing accessor methods from T1):

```csharp
// Build 1111.007-reaper-t2: Accessor methods for OrphanSafety module

/// <summary>
/// Clears orphan FSM grace timestamp (called when position becomes live or activePositions is clean).
/// </summary>
internal void ClearOrphanFSMGrace(string entryName)
{
    _orphanedPositionFirstSeen.TryRemove(entryName, out _);
}

/// <summary>
/// Clears orphan repair counter (called on successful PositionInfo resolution).
/// </summary>
internal void ClearOrphanRepairCount(string accountName)
{
    _reaperOrphanRepairCount.TryRemove(accountName, out _);
}
```

**Verification**: Methods compile, match accessor pattern from T1.

---

### Step 6: Update `AuditSingleFleetAccount` Caller

Modify `src/V12_002.REAPER.Audit.cs` lines 127-153:

**BEFORE**:
```csharp
// [BUILD 981 DIAGNOSTIC]: Detect orphaned FSM positions after grace period.
foreach (var fsm in accountFsms)
{
    if (actualQty == 0 && activePositions.ContainsKey(fsm.EntryName))
    {
        // Check if grace period has expired (10 seconds)
        DateTime firstSeen = _orphanedPositionFirstSeen.GetOrAdd(fsm.EntryName, DateTime.UtcNow);
        double graceElapsed = (DateTime.UtcNow - firstSeen).TotalSeconds;

        if (graceElapsed > 10.0)
        {
            // Grace expired -- log diagnostic warning
            Print(
                string.Format(
                    "[REAPER][DIAGNOSTIC] Orphaned FSM position detected: {0} entry={1}. "
                        + "Broker flat but activePositions entry exists after {2:F1}s grace. "
                        + "This may indicate a TOCTOU race in entry rollback logic.",
                    acct.Name,
                    fsm.EntryName,
                    graceElapsed
                )
            );

            // Clear first-seen timestamp to avoid log spam
            _orphanedPositionFirstSeen.TryRemove(fsm.EntryName, out _);
        }
    }
    else
    {
        // Position is live or activePositions is clean -- clear first-seen timestamp
        _orphanedPositionFirstSeen.TryRemove(fsm.EntryName, out _);
    }
}
```

**AFTER**:
```csharp
// [BUILD 981 DIAGNOSTIC]: Detect orphaned FSM positions after grace period.
foreach (var fsm in accountFsms)
{
    DetectOrphanFSM(fsm.EntryName, acct.Name, actualQty, activePositions);
}
```

**Verification**: Method compiles, calls new `DetectOrphanFSM`.

---

### Step 7: Update `ValidateRepairEligibility` Caller

Modify `src/V12_002.REAPER.Repair.cs` lines 31-76:

**BEFORE**:
```csharp
private bool ValidateRepairEligibility(string accountName, out PositionInfo repairPos, out string repairEntryName)
{
    repairPos = null;
    repairEntryName = null;

    // A3-2: Abort immediately if a flatten is in progress (Build 960 audit fix)
    if (isFlattenRunning)
    {
        Print("[REAPER REPAIR] Aborted -- flatten in progress.");
        return false;
    }

    // 1. Find the stored PositionInfo for this account in activePositions
    foreach (var kvp in activePositions.ToArray())
    {
        PositionInfo pi = kvp.Value;
        if (pi.IsFollower && pi.ExecutingAccount != null
            && pi.ExecutingAccount.Name == accountName)
        {
            repairPos = pi;
            repairEntryName = kvp.Key;
            break;
        }
    }

    if (repairPos == null)
    {
        int orphanCount = _reaperOrphanRepairCount.AddOrUpdate(accountName, 1, (k, v) => v + 1);
        Print(string.Format("[REAPER REPAIR] x No PositionInfo found for {0} -- cannot repair. (orphan attempt {1}/3)",
            accountName, orphanCount));

        if (orphanCount >= 3)
        {
            Print(string.Format("[REAPER] SELF-HEAL: {0} has no PositionInfo after 3 attempts. Force-zeroing expectedPositions to unblock repair loop.",
                accountName));
            // SetExpectedPositionLocked(..., 0) already removes from _dispatchSyncPendingExpKeys internally.
            SetExpectedPositionLocked(ExpKey(accountName), 0);
            _reaperOrphanRepairCount.TryRemove(accountName, out _);
        }
        return false;
    }

    // Clear orphan counter on successful PositionInfo resolution
    _reaperOrphanRepairCount.TryRemove(accountName, out _);
    return true;
}
```

**AFTER**:
```csharp
private bool ValidateRepairEligibility(string accountName, out PositionInfo repairPos, out string repairEntryName)
{
    repairPos = null;
    repairEntryName = null;

    // A3-2: Abort immediately if a flatten is in progress (Build 960 audit fix)
    if (isFlattenRunning)
    {
        Print("[REAPER REPAIR] Aborted -- flatten in progress.");
        return false;
    }

    // NEW: Delegate orphan check to OrphanSafety module
    if (!ValidateRepairEligibility_OrphanCheck(accountName, activePositions, out repairPos, out repairEntryName))
    {
        return false;  // Orphan detected, self-heal triggered if threshold reached
    }

    ClearOrphanRepairCount(accountName);  // NEW: Accessor method (success path)
    return true;
}
```

**Verification**: Method compiles, calls new `ValidateRepairEligibility_OrphanCheck` and accessor method.

---

### Step 8: Remove Extracted Code from Original Files

**Delete from `src/V12_002.REAPER.cs`**:
- Line 68: `_orphanedPositionFirstSeen` declaration
- Line 61: `_reaperOrphanRepairCount` declaration

**Delete from `src/V12_002.REAPER.Audit.cs`**:
- Lines 127-153: Orphan FSM detection loop (replaced with `DetectOrphanFSM` call)

**Delete from `src/V12_002.REAPER.Repair.cs`**:
- Lines 43-75: Orphan check and self-heal logic (replaced with `ValidateRepairEligibility_OrphanCheck` call)

**Verification**: Files compile after deletion, no orphaned references.

---

### Step 9: Update BUILD_TAG

Modify `src/V12_002.cs` line 47:

**BEFORE**:
```csharp
public const string BUILD_TAG = "1111.007-reaper-t1";
```

**AFTER**:
```csharp
public const string BUILD_TAG = "1111.007-reaper-t2";
```

**Verification**: BUILD_TAG updated, matches ticket ID.

---

## VERIFICATION CHECKLIST

### Pre-Deployment

- [ ] All files compile without errors
- [ ] grep -r "lock(" src/ → 0 matches
- [ ] grep -r "_orphanedPositionFirstSeen" src/ → only in OrphanSafety.cs and accessor methods
- [ ] grep -r "_reaperOrphanRepairCount" src/ → only in OrphanSafety.cs and accessor methods
- [ ] All string literals ASCII-only (no Unicode)
- [ ] Scope blast documentation present in `ExecuteOrphanSelfHeal` remarks

### Deployment

- [ ] `powershell -File .\deploy-sync.ps1` → PASS
- [ ] ASCII gate → PASS
- [ ] DIFF guard → PASS (< 10,000 characters)

### F5 Verification (Live NinjaTrader)

- [ ] Strategy loads without errors
- [ ] Orphan FSM diagnostic logs after 10s grace (simulate by manually terminating FSM while broker is flat)
- [ ] Self-heal triggers after 3 failed repair attempts (simulate by removing PositionInfo entry)
- [ ] Force-zero clears expected position (check via debug log)
- [ ] Log prefixes correct: `[REAPER][DIAGNOSTIC]`, `[REAPER] SELF-HEAL:`

### Restart Scenario Verification

- [ ] Strategy restart with live position → FSM hydrated correctly (no false orphan detection)
- [ ] Strategy restart with flat position → Stale FSM auto-terminated (handled by `AuditFleet_CalculateExpectedActual`, not orphan detection)

---

## ACCEPTANCE CRITERIA

### Functional

1. **Orphan FSM Detection**
   - [ ] 10-second grace period preserved (hardcoded, not configurable)
   - [ ] Diagnostic logging after grace expiry functional
   - [ ] Grace timestamp cleared when position becomes live or activePositions is clean
   - [ ] No false positives during restart (handled by separate logic in `AuditFleet_CalculateExpectedActual`)

2. **Orphan Self-Heal**
   - [ ] 3-attempt self-heal threshold intact
   - [ ] Force-zero logic triggers correctly (account-level scope)
   - [ ] Orphan counter reset after self-heal
   - [ ] Scope blast documented in method remarks (INTENTIONAL, RATIONALE)

### Non-Functional

3. **Complexity Reduction**
   - [ ] `DetectOrphanFSM` CYC ≤ 4 (verify via `python scripts/complexity_audit.py`)
   - [ ] `ValidateRepairEligibility_OrphanCheck` CYC ≤ 5
   - [ ] `ExecuteOrphanSelfHeal` CYC ≤ 2
   - [ ] Module LOC ≤ 100

4. **V12 DNA Compliance**
   - [ ] Zero `lock()` statements in new module
   - [ ] All state mutations atomic (CAS or queue-based)
   - [ ] ASCII-only compliance (all string literals)
   - [ ] Zero new heap allocations on common path

5. **Jane Street Alignment**
   - [ ] Atomic state transitions (via `GetOrAdd`, `AddOrUpdate`, `TryRemove`)
   - [ ] Wait-free progress (no blocking operations)
   - [ ] Memory ordering correct (all operations on strategy thread)
   - [ ] Bounded latency (all operations O(1) or O(N) where N < 10)

---

## ROLLBACK PLAN

If F5 verification fails:

1. Revert `src/V12_002.REAPER.OrphanSafety.cs` (delete file)
2. Revert `src/V12_002.REAPER.cs` (restore state declarations, remove accessor methods)
3. Revert `src/V12_002.REAPER.Audit.cs` (restore orphan FSM detection loop)
4. Revert `src/V12_002.REAPER.Repair.cs` (restore orphan check and self-heal logic)
5. Revert BUILD_TAG to `1111.007-reaper-t1`
6. Run `powershell -File .\deploy-sync.ps1`
7. F5 NinjaTrader to verify rollback successful

---

**[TICKET-GATE]**

Ticket 02 ready for execution. Dependencies: ticket-01-naked-position (must be Director-accepted).

After this ticket is Director-accepted, proceed to EXECUTION_GUIDE generation.