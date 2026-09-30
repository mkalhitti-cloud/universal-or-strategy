# PTT-COPIER Deferred Backlog

<!-- This file is append-only. Do not overwrite prior block entries. -->
<!-- Format: one H2 block per epic block, newest block appended at bottom. -->

---

## B53-LaneA Block Entry (2026-08-10)

### DW-B54-01 (Proposed): AtmStrategyCreate API for AddOn context

**From**: B53-LaneA — `TryAttachAtmToFollower` in `CopyEngine.cs`
**Status**: Gated `#if NT8_ADDON_ATM` — code logic complete, API call not confirmed
**Root cause**: NT8-055 — `NinjaTrader.NinjaScript.AtmStrategy.AtmStrategyCreate` resolves to an
  instance method on `StrategyBase` in the Linting DLL. AddOn code (not extending `StrategyBase`)
  cannot call it as a static. The 2-arg and 3-arg static signatures used in Strategy-side code do
  not exist in the Linting DLL reference surface.
**Resolution needed**: Director to confirm the correct AddOn ATM API. Candidates:
  1. `Account.AtmStrategyCreate(...)` — if this method exists on the `Account` object, it would be
     accessible from AddOn context without requiring StrategyBase.
  2. `NinjaTrader.NinjaScript.AtmStrategy.AtmStrategyCreate` in a **different** DLL namespace —
     the runtime DLL surface may differ from the Linting DLL backup used by the Roslyn linter.
     The F5 runtime may expose a static overload. Must be confirmed by compiling a minimal NT8
     AddOn project (not a Strategy) that calls the method.
  3. `AddOn uses Strategy-as-bridge pattern` — a lightweight Strategy wrapper (entirely different
     from the PttFollowerStrategy class that was gated in B53) that acts on behalf of the AddOn to
     call `this.AtmStrategyCreate(...)` using the StrategyBase instance method.
  4. Manual bracket placement via `Account.CreateOrder` for stop/target legs — bypasses ATM
     entirely; constructs bracket legs explicitly from `CopyRule` ATM template parameters.
**Priority**: P0 — ATM brackets will not attach to follower fills until this is resolved.
**Acceptance criteria**:
  - `NT8_ADDON_ATM` defined in project DefineConstants
  - F5 compilation green with `AtmStrategyCreate` firing from `TryAttachAtmToFollower`
  - Follower fill → ATM brackets appear on Sim102 (F5-GATE-02)
  - NT8-055-RESOLVED appended to `NT8_COMPILER_RULES.md` with the confirmed call pattern
**Predecessor to**: DW-B54-02

---

### DW-B54-02 (Proposed): F5-GATE-02 — Live ATM bracket test on Sim101

**From**: B53-LaneA — verifier open item (ticket-5-verification.md)
**Status**: OPEN — blocked by DW-B54-01
**Root cause**: Cannot verify end-to-end follower fill → ATM bracket path until `AtmStrategyCreate`
  is callable from AddOn context (NT8-055 resolved).
**Resolution needed**: After DW-B54-01 is resolved:
  1. Run NinjaTrader in simulation mode with Sim102 as follower account
  2. Trigger a master trade on Sim101
  3. Confirm `PTT-Copy` order fills on Sim102
  4. Confirm ATM brackets (stop + target) appear on Sim102 after the fill
  5. Confirm no "Cancel pending" stuck orders
**Priority**: P0 — behavioral correctness of the B53 ATM path is unverified without live F5 test.
**Acceptance criteria**:
  - Sim102 shows filled entry order + bracket legs
  - NinjaTrader log shows no CS errors related to ATM
  - No order stuck at `Initialized` or `Cancel pending` state

---

### DW-B54-03 (Proposed): Add diagnostic log for #if NT8_ADDON_ATM inactive state

**From**: B53-LaneA — Phase 5 reviewer observation (Section G-04)
**Status**: OPEN (P2 observability improvement)
**Context**: When `NT8_ADDON_ATM` is not defined (default build), `TryAttachAtmToFollower` silently
  returns without logging that the gate is inactive. A diagnostic message would help the Director
  confirm when the gate is active vs. inactive without reading the source.
**Proposed fix**: After the `templateName` guard, before `#if NT8_ADDON_ATM`:
  ```csharp
  #if !NT8_ADDON_ATM
  StatusUpdate?.Invoke("PTT-ATM: gate inactive -- define NT8_ADDON_ATM to enable ATM attach.");
  return;
  #endif
  ```
**Priority**: P2 — non-blocking; observability aid for Director during NT8-055 resolution.
**Target block**: B54 (can bundle with DW-B54-01 fix).

---

### DW-BACKLOG-01 (Standing): PttContracts.cs FillSignal dead-code cleanup

**From**: B53-LaneA architecture plan §3 (explicit deferral)
**Status**: OPEN (deliberately deferred — explicit No Scope Creep Protocol §11 decision)
**Context**: `PttContracts.cs` still contains the `FillSignal` event and `FillSignalEventArgs`
  class. These are now dead code — `PttBus.RaiseFillSignal` was removed from `SendCopy` in B53-T2,
  and no subscribers remain at runtime. The plan explicitly preserves them ("zero subscribers at
  runtime is harmless dead code; removing them is a separate cleanup epic").
**Resolution needed**: A dedicated cleanup epic to remove `FillSignal`, `FillSignalEventArgs`, and
  `PttBus.RaiseFillSignal` from `PttContracts.cs`. Zero behavior change; pure cleanup.
**Priority**: P2 — harmless dead code; no correctness risk.
**Target block**: Future (independent of B54 ATM work).

---
