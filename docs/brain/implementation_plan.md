# Phase 4: Event Lifecycle Refactoring -- Implementation Plan
# docs/brain/implementation_plan.md
#
# ADR-020 | Branch: feature/phase-4-event-lifecycle
# Status: APPROVED FOR ENGINEER EXECUTION
# Author: P3 Architect (Claude Sonnet 4.6)
# Prerequisites: PR #70 merged (Phase 3 -- Strategy Factory Interfaces complete)
#                ADR-019 complete (zero lock(stateLock) across codebase)

---

## 1. EXECUTIVE SUMMARY

Phase 4 surgically decomposes the four monolithic NinjaTrader 8 lifecycle override
methods (OnStateChange, OnBarUpdate, OnOrderUpdate, OnExecutionUpdate) that currently
reside as God Functions in the root V12_002.cs partial class. The target architecture
transforms each override into a one-liner dispatcher that routes into dedicated, focused
partial-class handler files -- mirroring the decomposition already established in
Phase 1 (entry modules) and Phase 2 (order management / REAPER).

The result: V12_002.cs becomes a pure thin shell. Every unit of lifecycle business logic
has a single, auditable home. The FSM Enqueue contract is enforced at every dispatch
boundary. Zero new abstractions are introduced beyond what the mission requires.

---

## 2. CURRENT STATE AUDIT

### 2.1 Lifecycle Method Surface (Pre-Phase 4)

Based on the repository file manifest and established partial-class conventions, the
following lifecycle boundary is the target of this refactor:

| NT8 Override           | Current Home              | Known Responsibilities (estimated)    |
|------------------------|---------------------------|---------------------------------------|
| OnStateChange()        | V12_002.cs (root)         | SetDefaults, Configure, DataLoaded,   |
|                        |                           | Realtime init, IPC setup, Teardown    |
| OnBarUpdate()          | V12_002.cs (root)         | Data guard, indicator eval, entry     |
|                        |                           | dispatch, REAPER tick, trailing eval  |
| OnOrderUpdate()        | V12_002.Orders.Callbacks  | FSM routing, bracket tracking,        |
|                        |                           | ghost-order guards, follower FSM      |
| OnExecutionUpdate()    | V12_002.Orders.Callbacks  | Fill accounting, REAPER trigger,      |
|                        |                           | SIMA reconciliation, PnL logging      |

### 2.2 Phase 3 Artifacts in Place (Do Not Touch)

- IStrategyFactory<TContext> and all factory implementations (src/)
- StrategyFactoryRegistry and resolver wiring
- All partial files from Phases 1-3 (Entries.*, REAPER.cs, SIMA.cs, etc.)

### 2.3 Known Violations to Eradicate

- Any remaining inline state-phase switch/if-else chains inside OnStateChange
- Any OnBarUpdate body longer than ~15 lines (it should be a pure dispatch list)
- Any direct mutation of mutable state fields outside of Enqueue -- confirmed via
  ADR-019 scan but the OnBarUpdate body may still contain direct writes that predate
  the Enqueue mandate; Phase 4 hardens this boundary

---

## 3. TARGET ARCHITECTURE

### 3.1 New File Map (src/ additions only)

```
src/
  V12_002.cs                          <-- MODIFIED: override bodies -> one-liners
  V12_002.Lifecycle.State.cs          <-- NEW: OnStateChange phase handlers
  V12_002.Lifecycle.BarUpdate.cs      <-- NEW: OnBarUpdate sub-dispatchers
  V12_002.Orders.Callbacks.cs         <-- MODIFIED: thin dispatcher headers added
  (all other files)                   <-- UNTOUCHED
```

### 3.2 Dispatcher Contract (root V12_002.cs)

After Phase 4, the four lifecycle overrides in V12_002.cs MUST look exactly like this
and nothing else. All business logic lives in the handler files.

```csharp
// V12_002.cs -- root partial class (MODIFIED SECTION ONLY)
// Phase 4 target: each override is a one-liner dispatcher.
// ARCHITECT NOTE: NT8 requires override to live in the concrete class.
// The partial class system routes to handler methods in sibling files.

protected override void OnStateChange()
{
    DispatchOnStateChange();
}

protected override void OnBarUpdate()
{
    DispatchOnBarUpdate();
}

// OnOrderUpdate and OnExecutionUpdate already delegate to
// V12_002.Orders.Callbacks.cs -- Phase 4 makes this explicit and adds
// the guard header described in Section 3.4.
protected override void OnOrderUpdate(
    Cbi.Order order,
    double limitPrice,
    double stopPrice,
    int quantity,
    int filled,
    double averageFillPrice,
    Cbi.OrderState orderState,
    DateTime time,
    Cbi.ErrorCode error,
    string nativeError)
{
    DispatchOnOrderUpdate(order, limitPrice, stopPrice, quantity,
                          filled, averageFillPrice, orderState,
                          time, error, nativeError);
}

protected override void OnExecutionUpdate(
    Cbi.Execution execution,
    string executionId,
    double price,
    int quantity,
    Cbi.MarketPosition marketPosition,
    string orderId,
    DateTime time)
{
    DispatchOnExecutionUpdate(execution, executionId, price,
                              quantity, marketPosition, orderId, time);
}
```

### 3.3 V12_002.Lifecycle.State.cs (NEW FILE -- FULL SCAFFOLD)

This file implements `DispatchOnStateChange()` and all private phase handlers.
The NT8 state machine maps directly to a switch on `State`; each case calls a
focused private method. No inline logic is permitted in the dispatch switch.

```csharp
// V12_002.Lifecycle.State.cs
// Phase 4 -- OnStateChange Dispatcher
// ADR-020 | ASCII-only strings | Zero lock(stateLock)
// All FSM mutations via Enqueue(ctx => ...)

using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using System;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002
    {
        // ---------------------------------------------------------------
        // PRIMARY DISPATCHER -- called by OnStateChange() in V12_002.cs
        // ---------------------------------------------------------------
        private void DispatchOnStateChange()
        {
            switch (State)
            {
                case State.SetDefaults:
                    HandleState_SetDefaults();
                    break;

                case State.Configure:
                    HandleState_Configure();
                    break;

                case State.DataLoaded:
                    HandleState_DataLoaded();
                    break;

                case State.Historical:
                    HandleState_Historical();
                    break;

                case State.Transition:
                    HandleState_Transition();
                    break;

                case State.Realtime:
                    HandleState_Realtime();
                    break;

                case State.Terminated:
                    HandleState_Terminated();
                    break;

                default:
                    Print("[V12] [WARN] OnStateChange -- unhandled state: "
                          + State.ToString());
                    break;
            }
        }

        // ---------------------------------------------------------------
        // PHASE HANDLERS
        // Each method is responsible for exactly one NT8 lifecycle phase.
        // No cross-phase logic is permitted inside a handler.
        // ---------------------------------------------------------------

        private void HandleState_SetDefaults()
        {
            // Populate all [Parameter] defaults.
            // No subsystem init permitted here -- NT8 context not ready.
            Description = "V12 Universal OR Strategy -- Phase 4 Modular Build";
            Name        = "V12_002";

            // Strategy engine defaults
            Calculate                       = Calculate.OnBarClose;
            IsExitOnSessionCloseStrategy    = true;
            ExitOnSessionCloseSeconds       = 30;
            IsFillLimitOnTouch              = false;
            MaximumBarsLookBack             = MaximumBarsLookBack.TwoHundredFiftySix;
            OrderFillResolution             = OrderFillResolution.Standard;
            Slippage                        = 0;
            StartBehavior                   = StartBehavior.WaitUntilFlat;
            TimeInForce                     = TimeInForce.Gtc;
            TraceOrders                     = false;
            RealtimeErrorHandling           = RealtimeErrorHandling.StopCancelClose;
            StopTargetHandling              = StopTargetHandling.PerEntryExecution;
            BarsRequiredToTrade             = 20;

            // Delegate remaining parameter defaults to Properties partial
            InitializeParameterDefaults();
        }

        private void HandleState_Configure()
        {
            // Add data series, register indicators, wire IPC listener.
            // All subsystem bootstrapping that requires AddDataSeries().
            ConfigureDataSeries();
            ConfigureIndicators();
            ConfigureIpcListener();
        }

        private void HandleState_DataLoaded()
        {
            // Post-load validation. ATR baseline. Strategy factory init.
            ValidateInstrumentConfig();
            InitializeStrategyFactory();
            InitializeReaperBounds();
        }

        private void HandleState_Historical()
        {
            // Warm-up pass housekeeping.
            // NOTE: Most logic fires in OnBarUpdate during this state.
            // This handler initializes any historical-pass-only counters.
            ResetHistoricalPassCounters();
        }

        private void HandleState_Transition()
        {
            // NT8 calls this once between Historical and Realtime.
            // Flush any historical-only state that must not carry forward.
            FlushHistoricalOnlyState();
            Print("[V12] Transition -> Realtime gate passed");
        }

        private void HandleState_Realtime()
        {
            // Realtime subsystem activation sequence.
            // Order matters -- dependencies must be ready before consumers.
            ActivateSimaMonitor();
            ActivateIpcListener();
            ActivateAccountUpdateListener();
            Print("[V12] Realtime activation complete -- all subsystems live");
        }

        private void HandleState_Terminated()
        {
            // Ordered teardown -- reverse of activation sequence.
            // Each method is individually guarded; failures are logged, not thrown.
            DeactivateIpcListener();
            DeactivateSimaMonitor();
            DeactivateAccountUpdateListener();
            FlushPendingEnqueuedWork();
            Print("[V12] Terminated -- teardown complete");
        }

        // ---------------------------------------------------------------
        // CONFIGURATION HELPERS (called from phase handlers above)
        // These are stubs -- actual bodies migrate from current V12_002.cs
        // OnStateChange inline logic during Engineer execution.
        // ---------------------------------------------------------------

        private void InitializeParameterDefaults()  { /* MIGRATE from current SetDefaults block  */ }
        private void ConfigureDataSeries()          { /* MIGRATE from current Configure block     */ }
        private void ConfigureIndicators()          { /* MIGRATE from current Configure block     */ }
        private void ConfigureIpcListener()         { /* MIGRATE IPC init from Configure block    */ }
        private void ValidateInstrumentConfig()     { /* MIGRATE from DataLoaded block            */ }
        private void InitializeStrategyFactory()    { /* HOOK: calls factory registry (Phase 3)   */ }
        private void InitializeReaperBounds()       { /* MIGRATE ATR fence calc from DataLoaded   */ }
        private void ResetHistoricalPassCounters()  { /* MIGRATE historical counter resets        */ }
        private void FlushHistoricalOnlyState()     { /* MIGRATE Transition cleanup               */ }
        private void ActivateSimaMonitor()          { /* DELEGATE to V12_002.SIMA.cs              */ }
        private void ActivateIpcListener()          { /* DELEGATE to V12_002.UI.IPC.cs            */ }
        private void ActivateAccountUpdateListener(){ /* MIGRATE account listener wire-up         */ }
        private void DeactivateIpcListener()        { /* DELEGATE to V12_002.UI.IPC.cs            */ }
        private void DeactivateSimaMonitor()        { /* DELEGATE to V12_002.SIMA.cs              */ }
        private void DeactivateAccountUpdateListener(){ /* MIGRATE listener teardown              */ }
        private void FlushPendingEnqueuedWork()     { /* Drain queue before Terminated returns    */ }
    }
}
```

### 3.4 V12_002.Lifecycle.BarUpdate.cs (NEW FILE -- FULL SCAFFOLD)

OnBarUpdate is the hottest path -- called on every bar. The dispatcher executes
strictly ordered sub-dispatchers. Each guard method returns bool; a false result
short-circuits the remainder of the bar processing pipeline. This replaces an
inline if/else/return chain with a documented, named pipeline.

```csharp
// V12_002.Lifecycle.BarUpdate.cs
// Phase 4 -- OnBarUpdate Dispatcher
// ADR-020 | ASCII-only strings | Zero lock(stateLock)
// PERF: No allocation on hot path. All methods are non-virtual.

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002
    {
        // ---------------------------------------------------------------
        // PRIMARY DISPATCHER -- called by OnBarUpdate() in V12_002.cs
        // Pipeline: each stage returns false to abort the remainder.
        // ---------------------------------------------------------------
        private void DispatchOnBarUpdate()
        {
            if (!BarUpdate_GuardDataValidity())   return;
            if (!BarUpdate_GuardTradingSession()) return;
            if (!BarUpdate_GuardFlatReset())      return;

            BarUpdate_ComputeIndicators();
            BarUpdate_EvaluateOpeningRange();
            BarUpdate_DispatchEntries();
            BarUpdate_EvaluateTrailing();
            BarUpdate_TickReaper();
            BarUpdate_RefreshUI();
        }

        // ---------------------------------------------------------------
        // GUARD STAGES -- return false to short-circuit pipeline
        // ---------------------------------------------------------------

        // Returns false if bar index or BarsInProgress state is invalid.
        // This is the primary data-safety gate; must be first in pipeline.
        private bool BarUpdate_GuardDataValidity()
        {
            if (CurrentBar < BarsRequiredToTrade) return false;
            if (BarsInProgress != 0)              return false;
            return true;
        }

        // Returns false if we are outside the configured trading session window.
        // Reads: SessionOpen, SessionClose params (from V12_002.Properties.cs)
        private bool BarUpdate_GuardTradingSession()
        {
            // MIGRATE: current session-time guard logic from OnBarUpdate body
            return IsWithinTradingSession();
        }

        // Returns false if an end-of-session flat-reset is in progress.
        // Prevents new entries from firing during graceful session close.
        private bool BarUpdate_GuardFlatReset()
        {
            // MIGRATE: IsExitOnSessionClose / IsSessionResetPending check
            return !IsSessionResetPending();
        }

        // ---------------------------------------------------------------
        // PROCESSING STAGES -- fire sequentially after all guards pass
        // ---------------------------------------------------------------

        // Recompute all indicator series used by this bar.
        // DELEGATE: actual indicator update logic is passive (NT8 calculates
        // indicator series automatically). This method is the hook for any
        // derived/computed indicator state that V12 maintains manually.
        private void BarUpdate_ComputeIndicators()
        {
            // MIGRATE: any manual indicator state updates from OnBarUpdate body
        }

        // Evaluate Opening Range boundaries. Runs on every bar during the
        // OR formation window; no-ops after OR is confirmed locked.
        // DELEGATE: V12_002.Entries.OR.cs handles the OR state machine.
        private void BarUpdate_EvaluateOpeningRange()
        {
            EvaluateOrBoundaries(); // defined in V12_002.Entries.OR.cs
        }

        // Route to the active entry strategy via the factory interface.
        // DELEGATE: V12_002.Entries.cs -> factory resolver (Phase 3).
        private void BarUpdate_DispatchEntries()
        {
            if (!IsEntryPermitted()) return;
            _activeEntryStrategy?.OnBarUpdate(); // Phase 3 factory interface
        }

        // Evaluate trailing stop adjustments for any open position.
        // DELEGATE: V12_002.Trailing.cs
        private void BarUpdate_EvaluateTrailing()
        {
            EvaluateTrailingStop(); // defined in V12_002.Trailing.cs
        }

        // Tick the REAPER repair subsystem. Runs every bar to evaluate
        // whether an orphaned bracket needs surgical correction.
        // DELEGATE: V12_002.REAPER.cs
        private void BarUpdate_TickReaper()
        {
            TickReaper(); // defined in V12_002.REAPER.cs
        }

        // Refresh any bar-driven UI state (chart drawings, panel data).
        // DELEGATE: V12_002.UI.Callbacks.cs
        private void BarUpdate_RefreshUI()
        {
            OnBarUpdate_UI(); // defined in V12_002.UI.Callbacks.cs
        }

        // ---------------------------------------------------------------
        // HELPER STUBS (bodies migrate from current OnBarUpdate inline)
        // ---------------------------------------------------------------
        private bool IsWithinTradingSession() { /* MIGRATE */ return true; }
        private bool IsSessionResetPending()  { /* MIGRATE */ return false; }
        private bool IsEntryPermitted()       { /* MIGRATE: risk/position check */ return true; }
    }
}
```

### 3.5 V12_002.Orders.Callbacks.cs (MODIFIED -- DISPATCHER HEADERS)

This file already holds the bulk of OnOrderUpdate / OnExecutionUpdate logic from
prior phases. Phase 4 adds the two thin dispatcher method signatures that are now
called from V12_002.cs, making the delegation explicit and auditable.

Add the following two methods to the TOP of the existing partial class in
V12_002.Orders.Callbacks.cs (do NOT modify any existing method bodies):

```csharp
// V12_002.Orders.Callbacks.cs -- Phase 4 additions (TOP OF FILE)
// Add dispatcher entry points; existing handler methods below are UNTOUCHED.

private void DispatchOnOrderUpdate(
    Cbi.Order order,
    double limitPrice,
    double stopPrice,
    int quantity,
    int filled,
    double averageFillPrice,
    Cbi.OrderState orderState,
    DateTime time,
    Cbi.ErrorCode error,
    string nativeError)
{
    // Guard: ignore orders not belonging to this strategy's instruments
    if (order == null) return;

    // Route through existing FSM handler -- no new logic here
    HandleOrderUpdate(order, limitPrice, stopPrice, quantity,
                      filled, averageFillPrice, orderState,
                      time, error, nativeError);
}

private void DispatchOnExecutionUpdate(
    Cbi.Execution execution,
    string executionId,
    double price,
    int quantity,
    Cbi.MarketPosition marketPosition,
    string orderId,
    DateTime time)
{
    if (execution == null) return;

    // Route through existing handler -- no new logic here
    HandleExecutionUpdate(execution, executionId, price,
                          quantity, marketPosition, orderId, time);
}
```

**CONSTRAINT FOR ENGINEER:** The existing `HandleOrderUpdate` and `HandleExecutionUpdate`
method names must be verified against the current file before applying. If they differ,
match the existing names exactly. Do NOT rename any existing method.

---

## 4. MIGRATION STRATEGY FOR EXISTING OnStateChange / OnBarUpdate BODIES

The Engineer must execute the following surgical migration for each phase handler stub:

### 4.1 Migration Protocol (per stub method)

```
Step 1: Read the current OnStateChange() or OnBarUpdate() body in V12_002.cs
        -> verify: identify the exact code block belonging to this state/stage

Step 2: Copy that block verbatim into the stub method body in the new Lifecycle file
        -> verify: block is now present in Lifecycle file

Step 3: Replace the original block in V12_002.cs with a call to the dispatcher
        -> verify: V12_002.cs override body now contains ONLY the dispatcher call

Step 4: Run ASCII scan (check_ascii.py) on the new file
        -> verify: zero non-ASCII characters

Step 5: Confirm zero occurrences of "lock(stateLock)" in the new file
        -> verify: grep returns empty

Step 6: Confirm all state mutations inside migrated block use Enqueue(ctx => ...)
        -> verify: no direct field mutations outside Enqueue lambda
```

### 4.2 Enqueue Compliance Audit (Phase 4 Specific)

Any direct field write inside the migrated code that is NOT inside an Enqueue lambda
and is NOT a Build 981 exemption (stopOrders direct-write during bracket submission)
MUST be wrapped before migration:

```csharp
// BANNED PATTERN (pre-Phase 4 direct write):
_someStateField = newValue;

// REQUIRED PATTERN (post-Phase 4 Enqueue):
Enqueue(ctx =>
{
    _someStateField = newValue;
});
```

**EXCEPTION:** Read-only operations (logging, indicator reads, guard checks) do NOT
require Enqueue wrapping. Only mutable state field writes are subject to this rule.

---

## 5. V12_002.cs ROOT FILE -- FINAL TARGET STATE

After Phase 4 is complete, the only lifecycle-related content remaining in V12_002.cs
should be:

1. The four one-liner override methods (Section 3.2 above)
2. Class-level field declarations (unchanged)
3. Constructor (if any, unchanged)
4. Any NT8-required metadata attributes (unchanged)

The following content must be ABSENT from V12_002.cs after migration:
- Any switch(State) block
- Any if(BarsInProgress != 0) guard
- Any session time comparison
- Any direct call to indicator methods
- Any IPC socket initialization
- Any Print() calls that are not in a dedicated handler file

---

## 6. CONSTRAINT COMPLIANCE CHECKLIST

Every file touched in this phase must pass ALL of the following before PR submission:

| Check                                    | Tool / Method                            |
|------------------------------------------|------------------------------------------|
| Zero lock(stateLock) occurrences         | grep -r "lock(stateLock)" src/           |
| Zero non-ASCII characters in strings     | python check_ascii.py                    |
| All state mutations inside Enqueue       | Manual audit per migrated block          |
| OnStateChange body is one-liner only     | Visual inspection of V12_002.cs          |
| OnBarUpdate body is one-liner only       | Visual inspection of V12_002.cs          |
| No new abstractions beyond plan spec     | Karpathy: simplicity-first review        |
| BUILD_TAG incremented                    | V12_002.Properties.cs build tag field    |
| deploy-sync.ps1 executed after edits     | Post-edit deployment protocol (CLAUDE.md)|

---

## 7. NEW FILE CREATION ORDER (Engineer Execution Sequence)

Execute in this exact order to minimize merge conflicts:

```
1. Create src/V12_002.Lifecycle.State.cs    (new file, scaffold from Section 3.3)
2. Create src/V12_002.Lifecycle.BarUpdate.cs (new file, scaffold from Section 3.4)
3. Migrate OnStateChange body -> phase handlers in Lifecycle.State.cs
4. Migrate OnBarUpdate body  -> pipeline stages in Lifecycle.BarUpdate.cs
5. Modify src/V12_002.Orders.Callbacks.cs   (add dispatcher headers, Section 3.5)
6. Modify src/V12_002.cs                    (replace override bodies with dispatchers)
7. Run ASCII scan across all 4 modified/created files
8. Run lock(stateLock) grep scan
9. Run Enqueue compliance audit
10. Increment BUILD_TAG in V12_002.Properties.cs
11. Run deploy-sync.ps1
12. Instruct Director: press F5 in NinjaTrader to compile
13. Verify banner shows new BUILD_TAG
```

---

## 8. ADR ENTRY

**ADR-020: Phase 4 -- Event Lifecycle Decoupling**

- **Status:** Approved
- **Context:** OnStateChange and OnBarUpdate in V12_002.cs contain monolithic
  logic blocks spanning hundreds of lines, violating single-responsibility and
  making surgical audit of Enqueue compliance difficult.
- **Decision:** Introduce two new partial-class files (Lifecycle.State.cs,
  Lifecycle.BarUpdate.cs). Transform the four NT8 override methods into
  one-liner dispatchers. Explicitly surface the existing Orders.Callbacks.cs
  delegation via typed dispatcher methods.
- **Consequences:** V12_002.cs becomes a pure thin shell. All lifecycle phases
  are individually auditable. Enqueue compliance boundary is enforceable per
  migrated block. The factory interface (Phase 3) is now a first-class consumer
  in the BarUpdate pipeline via `_activeEntryStrategy?.OnBarUpdate()`.
- **Constraints:** Zero lock(stateLock). ASCII-only. No new abstractions beyond
  this plan. Build 981 stopOrders direct-write exemption preserved.

---

## 9. OPEN QUESTIONS FOR DIRECTOR REVIEW

The following items require Director decision before or during Engineer execution:

1. **Enqueue boundary for HandleState_Terminated():** The teardown sequence
   (DeactivateIpcListener, etc.) may need to fire on the NT8 thread directly
   rather than through Enqueue, since NT8 may not process queued work after
   Terminated is called. **Recommendation:** Teardown handlers execute directly;
   document as a named exception in ADR-020.

2. **BarUpdate_TickReaper() on every bar:** Confirm REAPER should tick on every
   OnBarUpdate call vs. only on OnBarClose bars. If Calculate = OnBarClose, this
   is a non-issue. If Calculate is ever changed to OnEachTick, REAPER needs its
   own bar-close guard. **Recommendation:** Add `if (IsFirstTickOfBar)` guard
   inside TickReaper() in V12_002.REAPER.cs as a defensive measure.

3. **_activeEntryStrategy nullability:** The Phase 3 factory resolver may return
   null during the historical pass before the session opens. The null-conditional
   `?.OnBarUpdate()` in the scaffold handles this silently. Confirm this is
   acceptable vs. a logged warning. **Recommendation:** Silent null is correct --
   pre-session historical bars should not log per-bar warnings.

---

## 10. BUILD TAG

Increment `BUILD_TAG` in `V12_002.Properties.cs` to the next sequential value
(e.g., if current is Build 981, set to Build 982) after all migration steps
are complete and before running deploy-sync.ps1.

---

*Plan complete. Director's Handoff Block follows.*

---

## DIRECTOR'S HANDOFF BLOCK
## TO: Codex Engineer (P4)
## FROM: P3 Architect
## MISSION: Phase 4 -- Event Lifecycle Refactoring
## BRANCH: feature/phase-4-event-lifecycle
## PLAN FILE: docs/brain/implementation_plan.md (this file)

---

### YOUR MISSION

Execute the Phase 4 Event Lifecycle refactoring exactly as specified in this plan.
You are the P4 ENGINEER. You have execution permission on src/ for this mission only.

### EXECUTION ORDER (do not deviate)

```
1.  git checkout feature/phase-4-event-lifecycle
2.  Verify PR #70 is merged and branch is up-to-date with main
3.  Create src/V12_002.Lifecycle.State.cs   (scaffold: Section 3.3)
4.  Create src/V12_002.Lifecycle.BarUpdate.cs (scaffold: Section 3.4)
5.  READ src/V12_002.cs OnStateChange() body in full before touching it
6.  Migrate each state phase block -> corresponding HandleState_*() method
7.  Replace V12_002.cs OnStateChange() body with: DispatchOnStateChange();
8.  READ src/V12_002.cs OnBarUpdate() body in full before touching it
9.  Migrate each guard/stage block -> corresponding BarUpdate_*() method
10. Replace V12_002.cs OnBarUpdate() body with: DispatchOnBarUpdate();
11. READ src/V12_002.Orders.Callbacks.cs -- confirm existing handler method names
12. Add DispatchOnOrderUpdate() and DispatchOnExecutionUpdate() to Callbacks.cs
    using EXACT existing handler method names (do NOT assume HandleOrderUpdate)
13. Replace V12_002.cs OnOrderUpdate() and OnExecutionUpdate() bodies with
    one-liner calls to the new Dispatch* methods
14. Run: python check_ascii.py  -- must report ZERO violations
15. Run: grep -rn "lock(stateLock)" src/ -- must return ZERO results
16. Manual audit: every state mutation in migrated blocks uses Enqueue(ctx=>...)
    EXCEPTION: teardown handlers in HandleState_Terminated() may write directly
    EXCEPTION: stopOrders direct-write (Build 981 exemption)
17. Increment BUILD_TAG in src/V12_002.Properties.cs
18. Run: powershell -File .\deploy-sync.ps1  -- ASCII Gate must PASS
19. Instruct Director: press F5 in NinjaTrader to compile
20. Confirm banner displays new BUILD_TAG
21. Commit with message:
    feat(phase-4): decompose lifecycle overrides into dispatcher pattern [ADR-020]
22. Push branch and open PR against main
```

### MANDATORY SELF-AUDIT BEFORE PR (P4 Engineer Checklist)

Invoke your internal `architect` subagent (/loop-critic) to verify:

- [ ] V12_002.cs OnStateChange body = exactly one line: DispatchOnStateChange();
- [ ] V12_002.cs OnBarUpdate body   = exactly one line: DispatchOnBarUpdate();
- [ ] V12_002.cs OnOrderUpdate body = exactly one line: dispatcher call
- [ ] V12_002.cs OnExecutionUpdate body = exactly one line: dispatcher call
- [ ] Zero lock(stateLock) across all 4 modified/created files
- [ ] Zero non-ASCII characters across all 4 modified/created files
- [ ] All Enqueue constraints verified per Section 4.2
- [ ] BUILD_TAG incremented
- [ ] deploy-sync.ps1 completed with ASCII Gate PASS

### OPEN QUESTIONS -- ANSWER BEFORE CODING

Before migrating, resolve Section 9 items:
- Q1 (Terminated teardown -- direct vs Enqueue): Implement direct-call teardown;
  document in code comment as "Terminated-exempt per ADR-020 Section 9.1"
- Q2 (REAPER tick guard): Add IsFirstTickOfBar guard inside TickReaper() in
  V12_002.REAPER.cs as a defensive measure (single-line change, surgical)
- Q3 (null factory): Confirm null-conditional is acceptable; no logging change needed

### WHAT YOU MUST NOT DO

- Do NOT modify any file in src/ other than the 4 listed in Section 3.1
- Do NOT rename any existing method
- Do NOT introduce any new interface, class, or abstraction not in this plan
- Do NOT add any emoji, curly quotes, em-dashes, or Unicode to string literals
- Do NOT use lock(stateLock) under any circumstances
- Do NOT submit directly -- submit via GitHub PR for Director review

### SIGN-OFF REQUIREMENT

After completing the checklist, post your P4 audit report to the PR description
in this format:

```
P4 ENGINEER SELF-AUDIT -- Phase 4
ASCII Gate:          PASS / n violations
lock(stateLock) scan: PASS / n hits
Enqueue audit:       PASS / n violations
BUILD_TAG:           [new tag value]
deploy-sync:         PASS
Loop-critic verdict: APPROVED / REVISION REQUIRED
```

The Director (P1) will review the PR and provide final merge authorization.

---
END OF HANDOFF BLOCK
P3 Architect sign-off: Claude Sonnet 4.6 | 2026-05-01
ADR-020 | Phase 4 | feature/phase-4-event-lifecycle
