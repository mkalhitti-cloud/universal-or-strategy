# Skill: Reachability Analysis (NinjaScript V12)

Perform a deep forensic trace to determine if a suspected vulnerability or logic flaw is physically reachable during live trading.

## When to Use This Skill

- **During P5 Adverse Audits** - Validate if a finding is "Logic Drift" or a real risk.
- **Before P3 Architect Intake** - Provide a "Logical Proof of Failure" (PoF).
- **Post-Failure Forensics** - Map how an error propagated through the state machine.

## Prerequisites

- `docs/brain/implementation_plan.md` (the target code)
- `src/` (core kernel files: REAPER, SIMA, CIT)

## Instructions

### Step 1: Identify the Sink

Identify the exact line of code where the "failure" occurs (e.g., a direct write to `stopOrders` without a guard, or an un-enqueued state change).

### Step 2: Entry Point Selection

Identify all possible entry points that can trigger this code:

- `OnBarUpdate()` (Market data)
- `OnOrderUpdate()` / `OnAccountOrderUpdate()` (Broker events)
- `OnStateChange()` (Lifecycle events)
- `TriggerCustomEvent` (Cross-thread/Asynchronous tasks)

### Step 3: Call-Chain Mapping

Trace the data flow from the entry point back to the sink:

1. **Direct Calls**: List the sequence of methods.
2. **State Dependencies**: Document any boolean flags (e.g., `isTerminating`) or FSM states that must be active to allow the path.
3. **Queue Context**: Note if the path crosses the Actor Model boundary (e.g. from `OnAccountOrderUpdate` to a Lambada inside `Enqueue`).

### Step 4: Logic Drift Detection

Compare the trace against system invariants:

- If a path requires `_isTerminating == true` but the sink is _already_ protected by a guard, the finding is **ILLOGICAL** (Logic Drift).
- If a path bypasses the `Enqueue` queue, it is a **CRITICAL** finding.

## Success Criteria

- A clear "Proof of Failure" (PoF) diagram or pseudo-code trace.
- A Reachability Rating: `EXTERNAL_MARKET`, `EXTERNAL_BROKER`, or `INTERNAL_RACE`.

## Example PoF (NinjaScript)

```csharp
// Entry: OnAccountOrderUpdate (Thread: BrokerPipe)
// 1. Order state changes to 'Cancelled'
// 2. Logic hits SubmitFollowerReplacement()
// 3. FSM is in 'PendingCancel' state
// -> Vulnerability: Direct write to stopOrders occurs before main thread Enqueue completes.
```
