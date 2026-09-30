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

## B54-LaneA Block Entry (2026-08-09)

### DW-B54-01 (Carried): AtmStrategyCreate API for AddOn context (NT8-055)

**Carry-forward from**: B53-LaneA block entry above.
**Status**: OPEN — unchanged. No progress in B54-LaneA (this lane addressed DW-B54-03 UI sync only).
**Priority**: P0.
**Target block**: B55+ (ATM lane).

---

### DW-B54-02 (Carried): F5-GATE-02 — Live ATM bracket test

**Carry-forward from**: B53-LaneA block entry above.
**Status**: OPEN — blocked by DW-B54-01. No progress in B54-LaneA.
**Priority**: P0.
**Target block**: B55+ (ATM lane, after DW-B54-01 resolved).

---

### DW-B54-03-DIAG (Carried): Diagnostic log for #if NT8_ADDON_ATM inactive state

**Carry-forward from**: B53-LaneA DW-B54-03 entry above (renamed to -DIAG to distinguish from the
  B54-LaneA work item DW-B54-03 which is now CLOSED).
**Note on naming**: DW-B54-03 as originally proposed in B53 referred to the diagnostic log P2 item.
  In B54-LaneA planning, DW-B54-03 was repurposed as the primary lane work item (UI state desync P0).
  That work item is now **CLOSED**. The diagnostic log item carries forward as DW-B54-03-DIAG.
**Status**: OPEN (P2 observability aid).
**Priority**: P2.
**Target block**: B55 or bundle with DW-B54-01 fix.

---

### DW-BACKLOG-01 (Carried): PttContracts.cs FillSignal dead-code cleanup

**Carry-forward from**: B53-LaneA block entry above.
**Status**: OPEN — unchanged. No progress in B54-LaneA.
**Priority**: P2.
**Target block**: Future (independent of ATM work).

---

### DW-B54-04 (New): dotnet test runner isolation for XmlSerializer / private-type constraint

**From**: B54-LaneA — verifier observation (ticket-1-verification.md SCAN-07 analysis)
**Status**: OPEN (P2 — non-blocking; behavioral gate is F5 in NT8 process)
**Root cause**: `CopyEngine` is a sealed singleton with a `private sealed class CopyRulesContainer`
  nested type. The standalone `dotnet test` runner cannot generate a `XmlSerializer` serialization
  assembly for private nested types outside NT8's full-trust in-process environment. This causes
  24 pre-existing tests to fail in CI/standalone mode. The 3 new B54 tests (T_B54_01/02/03) are
  correctly written and test real behaviour, but fail for the same infrastructure reason — not a
  B54 code defect. F5 compilation + runtime in NinjaTrader's process is the confirmed behavioral
  gate for all persistence-path tests.
**Resolution needed**: One of:
  1. Move `CopyRulesContainer` from `private nested class` to `internal class` in the same file.
     `InternalsVisibleTo` in the test project assembly attribute then allows XmlSerializer access.
  2. Restructure tests to use a public test-seam DTO that wraps CopyRulesContainer data, avoiding
     direct private-type serialization in the test runner.
  3. Accept the constraint as permanent and document F5 as the sole persistence test gate.
**Priority**: P2 — non-blocking. F5 behavioral verification is the gate.
**Target block**: Future (independent of B55+ ATM work).
