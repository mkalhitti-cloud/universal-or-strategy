# PR #23 Fix Queue — wave7/pr4-s4-reaper-defense
# S4 REAPER Defense — 3 files
# Reviewers: Sourcery

---

## [LOGIC-BUG] P0 — Safety.Watchdog.cs: IsWatchdogShouldReset behavior regression

**File**: `src/V12_002.Safety.Watchdog.cs`
**Method**: `IsWatchdogShouldReset` (line ~36)
**Reviewers**: Sourcery (1/4)

**Symptom**: Pre-wave7 behavior when `lastBeat <= 0` (no heartbeat yet received):
method returned `false`, leaving `_watchdogStage` unchanged — escalation state
preserved across the pre-heartbeat window.

Post-wave7 (current): returns `true`, causing the caller at line ~68 to execute
`Interlocked.Exchange(ref _watchdogStage, 0)` — resetting escalation state to 0
before the first heartbeat arrives. Any stage escalation that happened before
first beat is silently cleared.

**Source lines** (current buggy state):
```csharp
private bool IsWatchdogShouldReset()
{
    if (_isTerminating || State != State.Realtime)
        return true;
    long lastBeat = Interlocked.Read(ref _strategyHeartbeatTicks);
    if (lastBeat <= 0)
        return true;    // ← was: return false pre-wave7. Regression.
    long heartbeatAge = DateTime.UtcNow.Ticks - lastBeat;
    if (heartbeatAge <= WatchdogTimeoutTicks)
        return true;
    return !HasWatchdogLeadAccountWorkingOrder();
}
```

**Caller context** (line ~68):
```csharp
if (IsWatchdogShouldReset())
    Interlocked.Exchange(ref _watchdogStage, 0);  // resets stage when true
```

**Pre-wave7 original behavior**: `lastBeat <= 0` returned `false` → stage NOT
reset → correct: watchdog had not yet received its first beat, escalation state
from startup preserved.

**OKF**: lock-free-patterns.md → FSM state transitions must be intentional and
auditable. how-to-build-an-exchange.md → `one_in_flight`: avoid unintended
FSM state resets.

---

## STATUS
- [ ] LOGIC-BUG: IsWatchdogShouldReset lastBeat<=0 returns false not true
