---
# TICKET REAPER-EXPANSION-01: SIMA Safety Module
# Epic: REAPER-EXPANSION
# Sequence: 1 of 3
# Depends on: NONE
---

## Objective
Implement SIMA fleet dispatch queue safety layer with queue depth monitoring, stale dispatch detection, symmetry auditing, and toggle gate leak detection.

## Scope
IN scope:
- Create new file: `src/V12_002.REAPER.SIMA.cs`
- Modify: `src/V12_002.REAPER.Audit.cs` (integration point)
- Modify: `src/V12_002.SIMA.Fleet.cs` (stale dispatch check)
- Modify: `src/V12_002.SIMA.Dispatch.cs` (toggle gate tracking)
- Add state fields: `_simaConsecutiveRejections`, `_staleDispatchThresholdSeconds`

OUT of scope:
- IPC safety module (Ticket 02)
- Entry validation module (Ticket 03)
- Automated tests (deferred to Phase 4)
- Performance optimization (deferred to Phase 5)

## Context References
- Analysis: docs/brain/REAPER-EXPANSION/01-analysis.md -- Section "SIMA Safety Gaps"
- Approach: docs/brain/REAPER-EXPANSION/02-approach-sima.md -- Complete module specification

## Implementation Instructions

### Step 1: Create V12_002.REAPER.SIMA.cs

Extract 4 methods from the approach blueprint:

| New Method | Responsibility | Min LOC | CYC Target |
|------------|---------------|---------|------------|
| AuditFleetDispatchQueue | Primary entry point - orchestrates all SIMA audits | 40 | ≤ 5 |
| CheckStaleDispatch | Validates dispatch age against 3s threshold | 20 | ≤ 3 |
| AuditSymmetryContext | Cross-account position sum audit | 50 | ≤ 4 |
| AuditSimaToggleGate | Force-reset after 5 consecutive rejections | 25 | ≤ 3 |

**State Fields** (add to V12_002.REAPER.SIMA.cs):
```csharp
private int _simaConsecutiveRejections = 0;
private int _staleDispatchThresholdSeconds = 3;
```

**Accessor Methods** (add to V12_002.REAPER.cs):
```csharp
internal void IncrementSimaRejectionCount()
{
    Interlocked.Increment(ref _simaConsecutiveRejections);
}

internal void ResetSimaRejectionCount()
{
    Interlocked.Exchange(ref _simaConsecutiveRejections, 0);
}
```

### Step 2: Integration Point 1 - REAPER Audit Cycle

**File**: `src/V12_002.REAPER.Audit.cs`
**Method**: `AuditApexPositions`

Add SIMA audit call after existing fleet/master audits:
```csharp
// NEW: SIMA safety audit
if (EnableSIMA)
{
    AuditFleetDispatchQueue(shouldLog);
}
```

### Step 3: Integration Point 2 - Stale Dispatch Check

**File**: `src/V12_002.SIMA.Fleet.cs`
**Method**: `PumpFleetDispatch` (line ~232)

Insert stale check AFTER dequeue, BEFORE ProcessFleetSlot:
```csharp
FleetDispatchRequest req;
if (!_pendingFleetDispatches.TryDequeue(out req))
    return;

// NEW: Stale dispatch check
if (!CheckStaleDispatch(req.SignalTicks, req.Account.Name, req.FleetEntryName))
{
    // Rollback delta and clear in-flight guard
    if (req.ReservedDelta != 0)
        AddExpectedPositionDeltaLocked(req.ExpectedKey, -req.ReservedDelta);
    ClearDispatchSyncPending(req.ExpectedKey);
    return;
}

ProcessFleetSlot(...);
```

### Step 4: Integration Point 3 - Toggle Gate Rejection Tracking

**File**: `src/V12_002.SIMA.Dispatch.cs`
**Method**: `ExecuteSmartDispatchEntry` (line ~49)

Add rejection tracking on CAS failure:
```csharp
if (Interlocked.CompareExchange(ref _simaToggleState, 1, 0) != 0)
{
    IncrementSimaRejectionCount();  // NEW
    Print("[DISPATCH] Semaphore contended -- deferring dispatch (non-blocking)");
    // ... deferred retry ...
    return;
}

ResetSimaRejectionCount();  // NEW: Reset on successful acquisition
```

## V12 DNA Guardrails
- [ ] Zero new lock() statements
- [ ] Zero non-ASCII characters in string literals
- [ ] All sub-methods >= 15 LOC (extraction floor)
- [ ] AuditFleetDispatchQueue CYC ≤ 5
- [ ] All helpers CYC ≤ 4
- [ ] No logic drift -- pure structural movement only
- [ ] Jane Street Compliance: 100% (Atomic, Wait-Free, Bounded, Deterministic)

## Post-Edit Verification (Mandatory)
```powershell
# 1. Re-establish hard links (MANDATORY after every src/ edit)
powershell -File .\deploy-sync.ps1

# 2. Complexity verification
python scripts/complexity_audit.py

# 3. Lock regression (must return ZERO)
grep -r "lock(" src/

# 4. ASCII gate (must return ZERO)
grep -Prn "[^\x00-\x7F]" src/
```

## Acceptance Criteria
- [ ] V12_002.REAPER.SIMA.cs created with 4 methods (~180 LOC)
- [ ] Queue depth monitoring (warn @100, critical @200 with emergency flatten)
- [ ] Stale dispatch detection (3s threshold, configurable)
- [ ] Symmetry context audit (cross-account consistency, delta >2 triggers repair)
- [ ] Toggle gate leak detection (force-reset after 5 rejections)
- [ ] All integration points implemented (3 files modified)
- [ ] deploy-sync.ps1 ASCII gate: PASS
- [ ] complexity_audit.py shows all methods ≤ target CYC
- [ ] lock() audit: ZERO matches
- [ ] Director presses F5 in NinjaTrader -- BUILD_TAG banner visible
- [ ] BUILD_TAG: `1111.008-reaper-expansion-t1`