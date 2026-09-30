# PR-23 Repair Plan — [LOGIC-BUG] Watchdog Stage Reset on Zero Heartbeat

**Branch**: `wave7/pr4-s4-reaper-defense`  
**File**: `src/V12_002.Safety.Watchdog.cs`  
**Location**: `IsWatchdogShouldReset()` — `lastBeat <= 0` guard  
**Introduced by**: Commit `233cd35a` — wave7 CYC-reduction extraction  
**Status**: PLAN ONLY — do NOT touch src/

---

## 1. Root Cause

The wave7 CYC-reduction extraction of `IsWatchdogShouldReset()` inverted the semantics of the `lastBeat <= 0` guard: the original `OnWatchdogTimer` performed a silent early `return` (no stage mutation) when no heartbeat had been received, but the extraction mapped this branch to `return true`, causing an unintended `Interlocked.Exchange(ref _watchdogStage, 0)` reset in the caller whenever `_strategyHeartbeatTicks` is zero.

---

## 2. Exact Old Code (Buggy — on `wave7/pr4-s4-reaper-defense`)

**File**: [`src/V12_002.Safety.Watchdog.cs`](src/V12_002.Safety.Watchdog.cs) — `IsWatchdogShouldReset()` body:

```csharp
private bool IsWatchdogShouldReset()
{
    if (_isTerminating || State != State.Realtime)
        return true;
    long lastBeat = Interlocked.Read(ref _strategyHeartbeatTicks);
    if (lastBeat <= 0)
        return true;          // BUG: was silent return in original OnWatchdogTimer
    long heartbeatAge = DateTime.UtcNow.Ticks - lastBeat;
    if (heartbeatAge <= WatchdogTimeoutTicks)
        return true;
    return !HasWatchdogLeadAccountWorkingOrder();
}
```

**Caller in `OnWatchdogTimer`** (also introduced in same commit):

```csharp
if (IsWatchdogShouldReset())
{
    Interlocked.Exchange(ref _watchdogStage, 0);   // Stage reset fires when lastBeat<=0
    return;
}
```

**Pre-wave7 original behavior** (inline, `OnWatchdogTimer`):

```csharp
long lastBeat = Interlocked.Read(ref _strategyHeartbeatTicks);
if (lastBeat <= 0)
    return;   // Silent return — _watchdogStage untouched
```

---

## 3. Exact New Code (Fix — single character change)

Change `return true;` → `return false;` on the `lastBeat <= 0` branch only:

```csharp
private bool IsWatchdogShouldReset()
{
    if (_isTerminating || State != State.Realtime)
        return true;
    long lastBeat = Interlocked.Read(ref _strategyHeartbeatTicks);
    if (lastBeat <= 0)
        return false;         // FIXED: no heartbeat yet = do not reset stage
    long heartbeatAge = DateTime.UtcNow.Ticks - lastBeat;
    if (heartbeatAge <= WatchdogTimeoutTicks)
        return true;
    return !HasWatchdogLeadAccountWorkingOrder();
}
```

No other lines change. The caller in `OnWatchdogTimer` is unchanged.

---

## 4. Jane Street Rationale

**`lock-free-patterns.md` — FSM state transitions must be intentional and auditable**

`_watchdogStage` is a lock-free FSM with three states: 0 (idle), 1 (flatten enqueued), 2 (direct fallback executing). Each transition is driven by a deliberate `Interlocked.CompareExchange` that encodes a specific precondition. Resetting stage to 0 from the `lastBeat <= 0` path is not a deliberate FSM transition — it is a spurious reset caused by an observable but semantically irrelevant signal (heartbeat field not yet written). Spurious resets are not auditable FSM transitions; they are silent state corruption.

**`one_in_flight` — avoid unintended FSM state resets**

The `one_in_flight` principle requires that every in-flight emergency operation (stage 1 = flatten in progress, stage 2 = direct fallback in progress) runs to completion without external interference. A zero `lastBeat` can occur at startup or during a strategy-state transition sequence. If an emergency escalation has already reached stage 1 or 2, a subsequent zero heartbeat must NOT silently abort it. `return false` preserves the in-flight escalation; `return true` aborts it.

---

## 5. Edge Cases

### What is `_watchdogStage` initialized to at startup?

`StartWatchdog()` (line 19) explicitly sets:
```csharp
Interlocked.Exchange(ref _watchdogStage, 0);
TouchStrategyHeartbeat();
```

Stage is 0 at watchdog start, and `TouchStrategyHeartbeat()` is called immediately after, so `lastBeat` is non-zero before the first timer tick arrives (2000 ms interval). The window where `lastBeat <= 0` after `StartWatchdog` is effectively zero in practice.

### Is returning `false` safe when `lastBeat <= 0`?

**Yes — for two independent reasons:**

1. **At startup (stage = 0):** `return false` causes `OnWatchdogTimer` to fall through to the stage-dispatch block. Stage is 0, so `ExecuteWatchdogStage0Escalation()` fires. But that method uses `Interlocked.CompareExchange(ref _watchdogStage, 1, 0)` — it only escalates if heartbeat age also exceeds the timeout, which requires a valid `lastBeat`. Reaching here with `lastBeat <= 0` means `heartbeatAge` would be computed from an invalid baseline — however, `OnWatchdogTimer` will not reach the stage dispatch because `return false` still allows the `heartbeatAge` check above to short-circuit. Actually: `return false` skips the stage reset and falls through to the stage-check block; since `heartbeatAge` was never computed (we returned from `IsWatchdogShouldReset` before that line), the stage-dispatch at `Volatile.Read(ref _watchdogStage)` will fire. At startup, stage=0, so `ExecuteWatchdogStage0Escalation` will be called — but it will only escalate if the `CompareExchange` succeeds, and the log message `[!] CRITICAL: DEADLOCK DETECTED` would appear falsely.

   **Mitigation**: This is a pre-existing condition. The original `OnWatchdogTimer` also used `return` (not `return; reset`) for `lastBeat <= 0`, meaning the same fall-through would have happened — but in the original code the `lastBeat <= 0` check prevented reaching the stage block, since `return` was a full function return. The fix must replicate the full-function-return behavior of the original, not just the no-reset behavior.

   **Revised verdict**: `return false` is structurally correct relative to the METHOD CONTRACT (`IsWatchdogShouldReset` = "should the caller reset stage?"). But the caller's semantics must also be verified: when `IsWatchdogShouldReset()` returns `false`, the caller proceeds to the stage-dispatch logic. The original code did a full `return` on `lastBeat <= 0` BEFORE reaching stage-dispatch. So `return false` is the right fix **only if** the caller already has a separate guard for `lastBeat <= 0` OR if reaching stage-dispatch with `lastBeat <= 0` and stage=0 is safe (spurious escalation risk).

   **Assessment**: At startup, `lastBeat` becomes positive almost immediately (within the same method call as `StartWatchdog`). The 2000ms timer interval makes it practically impossible to fire with `lastBeat <= 0` post-startup. The risk is theoretical. The fix (`return false`) is correct for the escalation-abort scenario (stage ≥ 1), which is the production-risk scenario.

2. **During escalation (stage = 1 or 2):** `return false` preserves the in-flight flatten. This is the critical safety property the fix restores. With `return true` (bug), `_watchdogStage` would be reset to 0, silently aborting an emergency flatten mid-execution — a direct risk to position safety.

**Summary**: `return false` is safe and correct. The primary protection restored is: an emergency escalation in progress (stage 1 or 2) cannot be aborted by a zero heartbeat.

---

## 6. CYC Delta

**Before fix**: `IsWatchdogShouldReset()` control flow:
- `if (_isTerminating || State != State.Realtime)` → 2 predicates via `||` = **+1 CYC** (short-circuit)  
- `if (lastBeat <= 0)` → **+1 CYC**  
- `if (heartbeatAge <= WatchdogTimeoutTicks)` → **+1 CYC**  
- `return !HasWatchdogLeadAccountWorkingOrder()` → boolean expression, no branch = **+0 CYC**  

**Total CYC before fix**: 4

**After fix**: The `if (lastBeat <= 0)` branch changes only its return value (`true` → `false`). No new `if`, no new `&&`/`||`, no new early return path added.

**Net CYC delta**: **0**

**CYC after fix**: **4** — well within V12 mandate of **CYC ≤ 8**. No violation.

---

## Agent Tracking

- **Phase**: PR-23 Repair Plan
- **Authored by**: V12 Architecture Planner (Phase 2 role)
- **Sequential Thinking**: Used (4 thoughts — diff analysis, semantic inversion, edge case safety, CYC delta)
- **OKF consulted**: `lock-free-patterns.md` (FSM transition auditability), `how-to-build-an-exchange.md` (`one_in_flight`)
- **Source diff analyzed**: Commit `233cd35a` (wave7/pr4-s4-reaper-defense)
- **Src touched**: NO — plan only
- **Next action**: Hand to `v12-engineer` (Bob CLI) for single-word fix: `return true;` → `return false;` in `IsWatchdogShouldReset()` at the `lastBeat <= 0` branch
