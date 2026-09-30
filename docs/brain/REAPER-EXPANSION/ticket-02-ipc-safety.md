---
# TICKET REAPER-EXPANSION-02: IPC Safety Module
# Epic: REAPER-EXPANSION
# Sequence: 2 of 3
# Depends on: ticket-01 (shares REAPER audit cycle integration pattern)
---

## Objective
Implement IPC command queue safety layer with backpressure monitoring, stale command detection, malformed payload circuit breaker, and allowlist bypass detection.

## Scope
IN scope:
- Create new file: `src/V12_002.REAPER.IPC.cs`
- Modify: `src/V12_002.REAPER.Audit.cs` (integration point)
- Modify: `src/V12_002.UI.IPC.cs` (stale command check)
- Add state fields: `_lastIpcInvalidUtf8Count`, `_lastIpcAllowlistRejectCount`, `_staleCommandThresholdSeconds`

OUT of scope:
- SIMA safety module (Ticket 01)
- Entry validation module (Ticket 03)
- Client disconnect infrastructure (TODO markers for Phase 4)
- Automated tests (deferred to Phase 4)

## Context References
- Analysis: docs/brain/REAPER-EXPANSION/01-analysis.md -- Section "IPC Safety Gaps"
- Approach: docs/brain/REAPER-EXPANSION/02-approach-ipc.md -- Complete module specification

## Implementation Instructions

### Step 1: Create V12_002.REAPER.IPC.cs

Extract 4 methods from the approach blueprint:

| New Method | Responsibility | Min LOC | CYC Target |
|------------|---------------|---------|------------|
| AuditIpcCommandQueue | Primary entry point - orchestrates all IPC audits | 35 | ≤ 5 |
| CheckStaleIpcCommand | Validates command age against 10s threshold | 20 | ≤ 3 |
| AuditMalformedPayloadRate | Circuit breaker @10/sec malformed payloads | 30 | ≤ 4 |
| AuditAllowlistBypassRate | Security anomaly @20/min bypass attempts | 30 | ≤ 4 |

**State Fields** (add to V12_002.REAPER.IPC.cs):
```csharp
private int _lastIpcInvalidUtf8Count = 0;
private int _lastIpcAllowlistRejectCount = 0;
private int _staleCommandThresholdSeconds = 10;
```

**Note**: Client disconnect infrastructure is marked with TODO comments for Phase 4 implementation.

### Step 2: Integration Point 1 - REAPER Audit Cycle

**File**: `src/V12_002.REAPER.Audit.cs`
**Method**: `AuditApexPositions`

Add IPC audit call after SIMA audit (from Ticket 01):
```csharp
// SIMA safety audit (from Ticket 01)
if (EnableSIMA)
{
    AuditFleetDispatchQueue(shouldLog);
}

// NEW: IPC safety audit
AuditIpcCommandQueue(shouldLog);
```

### Step 3: Integration Point 2 - Stale Command Check

**File**: `src/V12_002.UI.IPC.cs`
**Method**: `ProcessIpcCommandCore` (line ~383)

Insert stale check AFTER timestamp guard, BEFORE command execution:
```csharp
private void ProcessIpcCommandCore(string action, string[] parts, long senderTicks)
{
    if (!MetadataGuardCommandTimestamp(senderTicks, action))
        continue;
    
    // NEW: Stale command check
    if (!CheckStaleIpcCommand(senderTicks, action))
    {
        Print(string.Format("[IPC][STALE] Skipped stale command: {0}", action));
        return;
    }
    
    // ... execute command ...
}
```

### Step 4: Rate Calculation Logic

Both rate audit methods use the same pattern:
1. Read current counter via `Volatile.Read`
2. Calculate delta from last snapshot
3. Compute rate (delta / 1.0 second, assuming 1s audit interval)
4. Compare against threshold
5. Log violation and trigger action (TODO: client disconnect)
6. Update last snapshot

**Malformed Payload Threshold**: 10/sec
**Allowlist Bypass Threshold**: 0.33/sec (20/min)

## V12 DNA Guardrails
- [ ] Zero new lock() statements
- [ ] Zero non-ASCII characters in string literals
- [ ] All sub-methods >= 15 LOC (extraction floor)
- [ ] AuditIpcCommandQueue CYC ≤ 5
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
- [ ] V12_002.REAPER.IPC.cs created with 4 methods (~150 LOC)
- [ ] Queue depth monitoring (backpressure at 1600/2000)
- [ ] Stale command detection (10s threshold, configurable)
- [ ] Malformed payload circuit breaker (10/sec threshold, TODO: disconnect)
- [ ] Allowlist bypass detection (20/min threshold, TODO: disconnect)
- [ ] All integration points implemented (2 files modified)
- [ ] deploy-sync.ps1 ASCII gate: PASS
- [ ] complexity_audit.py shows all methods ≤ target CYC
- [ ] lock() audit: ZERO matches
- [ ] Director presses F5 in NinjaTrader -- BUILD_TAG banner visible
- [ ] BUILD_TAG: `1111.008-reaper-expansion-t2`