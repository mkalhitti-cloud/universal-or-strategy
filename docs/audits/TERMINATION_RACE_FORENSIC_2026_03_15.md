# Termination Race Forensic and Apex Order-Type Compliance

Date: 2026-03-15

## Executive Summary

This report is repo-grounded and diagnosis-only. No strategy code was changed.

Primary conclusion:

- Ghost orders can survive shutdown because termination disables the startup/callback guards before the strategy finishes canceling or reconciling live broker orders.
- The shutdown sweep has two blind spots:
  - it only cancels orders in `Working` or `Accepted`
  - its broker-name scan only matches `Stop_`, `S_`, `T1_`-`T5_`, and `Fleet_`
- `SubmitBracketOrders()` creates a real broker order before the stop is registered in `stopOrders`, creating a live-but-untracked window during teardown.
- Apex does require stop-loss discipline, but Apex does not state that `StopLimit` is required for `MES` or `MNQ`.
- Official Apex guidance permits mental stops unless the trader is on Probation. Official platform and exchange docs show `StopMarket` and `StopLimit` are supported order types, not mandated instrument-specific compliance types.

## Evidence Spine

### 1. Termination ordering

`src/V12_002.Lifecycle.cs:334-390`

- Termination starts at `State.Terminated`.
- `_configureComplete` and `_dataLoadedComplete` are set to `false` first.
- Only after that does the strategy run `CancelAllV12GtcOrders(true)`.
- IPC is stopped after the cancel sweep.
- Reaper is stopped after IPC.
- Fleet handlers are unsubscribed after Reaper.
- Tracking dictionaries are then cleared unconditionally.

Observed order:

1. readiness flags off
2. cancel sweep
3. stop IPC
4. stop Reaper
5. unsubscribe account handlers
6. clear `activePositions`, `entryOrders`, `stopOrders`, and target dictionaries

### 2. Master callback gating

`src/V12_002.cs:235-257`

- `EnsureStartupReady()` returns `false` whenever Configure or DataLoaded readiness is not complete.
- Once termination flips those flags off, guarded callbacks stop processing.

`src/V12_002.Orders.Callbacks.cs:157-175`

- `OnOrderUpdate()` exits immediately if `EnsureStartupReady()` fails.

`src/V12_002.Orders.Callbacks.Execution.cs:37-45`

- `OnPositionUpdate()` exits immediately if `EnsureStartupReady()` fails.

`src/V12_002.Orders.Callbacks.Execution.cs:178-191`

- `OnExecutionUpdate()` exits immediately if `EnsureStartupReady()` fails.

### 3. Master bracket submission path

`src/V12_002.Orders.Callbacks.cs:191-265`

- A filled master entry reaches `HandleEntryOrderFilled()`.
- That method is where `SubmitBracketOrders()` is called for the newly filled position.

### 4. Stop live-before-track gap in `SubmitBracketOrders()`

`src/V12_002.Orders.Management.cs:37-194`

- The stop order is submitted first.
- Only after submit returns does the code enqueue `ctx.stopOrders[entryName] = stopOrder`.
- Targets are tracked directly in their dictionaries.
- `pos.BracketSubmitted = true` is set last.

This ordering matters because the broker can already know about the stop before the strategy's own `stopOrders` dictionary does.

### 5. Actor-queue behavior

`src/V12_002.cs:220-226`

- `Enqueue()` appends work to `_cmdQueue`.

`src/V12_002.cs:272-275`

- `TryDrain()` cannot re-enter while `_drainToken` is already held.

`src/V12_002.cs:337-358`

- `DrainActor()` executes queued commands serially.
- It can break early on budget/yield.
- If commands remain, they are rescheduled for a later drain.

Implication:

- When `SubmitBracketOrders()` enqueues the stop dictionary write from inside an already-running actor callback, that write is deferred until a later dequeue step, not performed inline.

### 6. Cancel sweep coverage

`src/V12_002.SIMA.Lifecycle.cs:339-430`

- `SweepTrackedOrders()` only cancels orders whose state is `Working` or `Accepted`.
- `SweepBrokerOrders()` also only cancels orders whose state is `Working` or `Accepted`.
- Broker scanning only matches names starting with:
  - `Stop_`
  - `S_`
  - `T1_`
  - `T2_`
  - `T3_`
  - `T4_`
  - `T5_`
  - `Fleet_`

### 7. Actual signal-name patterns

`src/V12_002.Entries.OR.cs:155-157`

- OR master entries are named `ORLong_<timestamp>` or `ORShort_<timestamp>`.

`src/V12_002.Entries.Trend.cs:345-347`

- Trend manual entries are named `TrendMnlLong_<timestamp>` or `TrendMnlShort_<timestamp>`.

`src/V12_002.SIMA.Execution.cs:230-240`

- Local RMA master entries are named `RMA_<ticks>`.

`src/V12_002.SIMA.Dispatch.cs:179-180`

- Follower dispatch entries are named `Fleet_<account>_<tradeType>_<i>`.

Validation:

- `Fleet_*` is visible to the broker-name sweep.
- Local master entries such as `ORLong_*`, `TrendMnlLong_*`, and `RMA_*` are not.

### 8. Queued follower/account work is not startup-gated

`src/V12_002.Orders.Callbacks.AccountOrders.cs:37-56`

- `OnAccountOrderUpdate()` enqueues terminal account-order events without `EnsureStartupReady()`.

`src/V12_002.Orders.Callbacks.AccountOrders.cs:101-123`

- `ProcessAccountOrderQueue()` drains queued account-order events and re-enqueues actor work.

`src/V12_002.Orders.Callbacks.AccountOrders.cs:546-585`

- `ProcessQueuedAccountOrderCore()` reads `activePositions` and order dictionaries directly.

`src/V12_002.UI.Compliance.cs:262-269`

- `OnAccountExecutionUpdate()` enqueues account executions without `EnsureStartupReady()`.

`src/V12_002.UI.Compliance.cs:319-345`

- `ProcessAccountExecutionQueue()` drains queued executions and actor-enqueues `ProcessQueuedExecution()`.

`src/V12_002.UI.Compliance.cs:362-388`

- `ProcessQueuedExecution()` relies on `entryOrders` and `activePositions` to find a filled follower entry and call `SymmetryGuardOnFollowerFill()`.

`src/V12_002.Symmetry.Follower.cs:17-54`

- `SymmetryGuardOnFollowerFill()` marks the follower filled and, when conditions allow, submits the deferred bracket.

### 9. IPC is still active until after the cancel sweep

`src/V12_002.UI.IPC.cs:112-144`

- `TryEnqueueIpcCommand()` accepts commands into `ipcCommandQueue`.

`src/V12_002.UI.IPC.Server.cs:317-329`

- The IPC server enqueues a command and immediately schedules `ProcessIpcCommands()`.

`src/V12_002.UI.IPC.cs:221-308`

- `ProcessIpcCommands()` has no `EnsureStartupReady()` or termination gate.
- It actor-enqueues command handlers.
- It re-schedules itself while the queue is non-empty.

`src/V12_002.UI.IPC.Server.cs:423-447`

- `StopIpcServer()` runs after the cancel sweep.
- It stops the listener and clients and resets the queue depth counter.
- It does not drain or clear `ipcCommandQueue`.

## Logical Proof of Failure

## Trace 1: Filled entry during termination can miss bracket submission entirely

Deterministic sequence:

1. Termination begins and immediately sets `_configureComplete = false` and `_dataLoadedComplete = false`.
2. A master entry fill arrives after that point.
3. `OnOrderUpdate()`, `OnExecutionUpdate()`, and `OnPositionUpdate()` all exit early because `EnsureStartupReady()` now returns `false`.
4. `HandleEntryOrderFilled()` never runs.
5. `SubmitBracketOrders()` never runs.
6. The entry can be filled at the broker while the strategy never submits its protective stop/targets.

Why this is a proof:

- The only observed path in this repo that submits a fresh master bracket on fill is `HandleEntryOrderFilled() -> SubmitBracketOrders()`.
- That path is behind `OnOrderUpdate()`.
- `OnOrderUpdate()` is explicitly disabled before the cancel sweep begins.

Result:

- Shutdown can strand a filled position without ever creating the intended bracket.

## Trace 2: A stop can exist at the broker before `stopOrders` knows about it

Deterministic sequence:

1. `HandleEntryOrderFilled()` calls `SubmitBracketOrders()` on the actor thread.
2. `SubmitBracketOrders()` submits the stop to the broker first.
3. Only afterward does it queue the dictionary write `stopOrders[entryName] = stopOrder`.
4. Because this happens inside an already-running actor drain, the queued stop-registration command is deferred.
5. Termination starts during that deferral window.
6. `SweepTrackedOrders()` cannot cancel the stop because it is not yet present in `stopOrders`.
7. `SweepBrokerOrders()` only cancels orders in `Working` or `Accepted`; a newly submitted stop may still be in `Submitted`, `Trigger pending`, or another in-flight state during the sweep.
8. Termination then clears the dictionaries.
9. Any later callback that would have reconciled the stop is either skipped by startup gating or runs against cleared state.

Why this is a proof:

- The repo explicitly submits first and tracks later.
- The actor implementation explicitly defers queued writes when already draining.
- The cancel sweep explicitly ignores non-`Working` and non-`Accepted` states.
- NinjaTrader's official order-state definitions include `Submitted`, `Trigger pending`, and `Change submitted` as real order states between local validation and full exchange working state.

Result:

- A stop can become a broker-side ghost/orphan during shutdown even though the strategy intended to track and cancel it.

## Trace 3: The shutdown sweep has state and name blind spots

Deterministic sequence:

1. Shutdown calls `CancelAllV12GtcOrders(true)`.
2. Tracked sweep only cancels `Working` and `Accepted`.
3. Broker sweep also only cancels `Working` and `Accepted`.
4. Broker sweep only matches names starting with `Stop_`, `S_`, `T1_`-`T5_`, and `Fleet_`.
5. Local master entries are named `ORLong_*`, `ORShort_*`, `TrendMnlLong_*`, `TrendMnlShort_*`, and `RMA_*`.
6. Therefore, any local master entry that is not present in `entryOrders` at sweep time is invisible to the broker-name scan.
7. Any live order in `Submitted`, `Trigger pending`, `Change submitted`, or `Cancel pending` is invisible to both sweep phases regardless of name.

Why this is a proof:

- The name filter is explicit.
- The local signal names are explicit.
- The state filter is explicit.
- Official NinjaTrader state definitions confirm the missing states are valid live states, not hypothetical ones.

Result:

- Shutdown can leave behind both:
  - unattached local entry orders that do not match the broker-name sweep
  - in-flight stop/target orders that are live but not yet `Working` or `Accepted`

External corroboration:

- Apex's evaluation guidance explicitly warns that pending orders not attached to a position must still be canceled manually or they will remain and can liquidate the account.

## Trace 4: Queued IPC and account events can outlive teardown ordering

Deterministic sequence:

1. Broker/account callbacks enqueue account-order and account-execution work without startup gating.
2. IPC clients can still enqueue commands until `StopIpcServer()` runs, which occurs after the cancel sweep.
3. `ProcessIpcCommands()` has no termination gate and can keep scheduling more work while commands remain.
4. Termination clears `activePositions`, `entryOrders`, `stopOrders`, and target dictionaries.
5. Any already-queued `ProcessQueuedExecution()` or `ProcessQueuedAccountOrderCore()` work now runs against cleared or partial state.
6. `ProcessQueuedExecution()` cannot find the follower entry in `entryOrders` / `activePositions`, so deferred follower bracket submission is skipped.
7. `ProcessQueuedAccountOrderCore()` snapshots `activePositions`; after teardown it can no longer match the order to the original position metadata.
8. Reconciliation or late protective work becomes impossible or is downgraded to orphan/ghost cleanup logic without the original context.

Why this is a proof:

- The queues are explicit.
- The lack of startup/termination gating in these queue processors is explicit.
- The dictionary clear is explicit.
- The dependent lookup logic is explicit.

Result:

- Even correct late broker notifications can become unusable because the strategy destroys the state needed to interpret them before those notifications are drained.

## Compliance Verification

### Verified by Apex

- `MES` and `MNQ` are tradable Apex micro futures.
  - Source: Apex support article `Can I Trade Micros?`
  - Source: Apex support article `Futures Trading Times`

- Apex requires stop-loss and risk management discipline.
  - Source: Apex support article `Performance Account (PA) and Compliance`
  - Apex states that trading without a stop loss or defined risk plan is prohibited.
  - Apex also enforces a maximum 5:1 risk-to-reward framework.

- Apex permits mental stops.
  - Source: Apex support article `Performance Account (PA) and Compliance`
  - Mental stops are allowed if they are honored.
  - On Probation, hard stop-loss levels are mandatory.

### Not stated by Apex; supported by broker/exchange docs

- Apex does not state that `StopLimit` is required specifically for `MES` or `MNQ`.
- Official NinjaTrader documentation shows both stop-market and stop-limit orders are supported order methods.
- Official CME Globex documentation for equity futures shows stop-limit and stop-with-protection style orders are supported exchange order types.

Inference:

- Since `MES` and `MNQ` are CME micro equity futures and Apex allows mental stops unless on Probation, there is no official evidence that Apex compliance requires `StopLimit` specifically on those instruments.
- The most defensible reading is that `StopLimit` is supported, not mandated.

### Unverified

- Any claim that Apex has an instrument-specific compliance rule saying `MES` or `MNQ` must use `StopLimit`.
- Any claim that Apex prohibits `StopMarket` for `MES` or `MNQ`.

## Validation Notes

- This diagnosis was validated against actual repo control flow, queue behavior, sweep filters, and signal-name patterns.
- No live NinjaTrader trace logs were provided, so the report is code-rooted rather than log-correlated.
- The strongest externally corroborated operational rule is Apex's warning that pending orders not attached to a position can remain and liquidate the account if the trader does not cancel them.

## Remediation Appendix

These are design recommendations only. They were not implemented in this phase.

1. Introduce an explicit termination state flag and stop accepting new IPC/account work before dropping startup readiness flags.
2. Drain or freeze actor/account/IPC queues before clearing tracking dictionaries.
3. Do not clear tracking dictionaries until terminal confirmations are processed or persisted for post-restart adoption.
4. Expand shutdown sweep coverage to include additional live states such as `Submitted`, `TriggerPending`, `ChangeSubmitted`, and `CancelPending` where appropriate.
5. Replace prefix-based broker scanning with a stronger strategy-owned identity marker, such as a deterministic tag map or persisted order registry.
6. Eliminate the stop live-before-track gap by atomically staging registration intent before submission, or by maintaining a pre-submit pending registry that the shutdown sweep can also inspect.
7. Add an explicit teardown gate to `ProcessIpcCommands()`, `ProcessQueuedExecution()`, and `ProcessQueuedAccountOrderCore()`.
8. Treat follower deferred-bracket submission as teardown-sensitive state and persist enough metadata to recover after late fills.

## Sources

- Apex Trader Funding, `Can I Trade Micros?`
  - https://support.apextraderfunding.com/hc/en-us/articles/4404866659355-Can-I-Trade-Micros
- Apex Trader Funding, `Futures Trading Times`
  - https://support.apextraderfunding.com/hc/en-us/articles/31519771524891-Futures-Trading-Times
- Apex Trader Funding, `Legacy Performance Account (PA) Compliance`
  - https://support.apextraderfunding.com/hc/en-us/articles/31519788944411-Legacy-Performance-Account-PA-Compliance
- Apex Trader Funding, `What Are the Consistency Rules for Evaluation Accounts?`
  - https://support.apextraderfunding.com/hc/en-us/articles/4404866611739-What-Are-the-Consistency-Rules-for-Evaluation-Accounts
- Apex Trader Funding, `Once Evaluation Accounts are Active`
  - https://support.apextraderfunding.com/hc/en-us/articles/35397723680795-Once-Evaluation-Accounts-are-Active
- NinjaTrader, `Order State Definitions`
  - https://ninjatrader.com/support/helpGuides/nt8/order_state_definitions.htm
- NinjaTrader, `Order Types`
  - https://ninjatrader.com/support/helpGuides/nt8/order_types.htm
- CME Group, `CME Globex Reference Guide`
  - https://www.cmegroup.com/tools-information/webhelp/globex-reference-guide/Content/Order-types-and-Qualifiers.html
