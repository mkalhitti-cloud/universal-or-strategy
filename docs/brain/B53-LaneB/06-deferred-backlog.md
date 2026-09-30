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

## B53-LaneB Block Entry (2026-08-10)

### DW-B53C-01: LaneC cancel-propagation code present but pipeline-unverified

**From**: B53-LaneB — Final Review Section G-01 (out-of-scope pre-add finding)
**Status**: OPEN — LaneC pipeline has not been run
**Root cause**: A prior engineer run (before LaneB was formally implemented) pre-added the LaneC
  cancel-propagation methods:
  - `IsLeaderEntryCancelled` at CopyEngine.cs line 1675
  - `FindFollowerWorkingEntry` at CopyEngine.cs line 1691
  - `CancelFollowerEntryOrders` at CopyEngine.cs line 1261
  and their tests `T_B53C_01`, `T_B53C_02` in CopyEngineTests.cs.

  These methods are wired in `DispatchAfterRuleMatch` branch (3) and compile cleanly (build passes,
  0 errors). However, they have NOT been reviewed by a ptt-plan-reviewer, NOT been through a
  ticket-review, NOT been verified by a ptt-verifier, and have no formal plan document
  (`docs/brain/B53-LaneC/02-architecture-plan.md`).
**Resolution needed**: Run the full B53-LaneC pipeline:
  1. ptt-architect: write `docs/brain/B53-LaneC/02-architecture-plan.md`
  2. ptt-plan-reviewer (Phase 2): REVIEW_PASS gate
  3. ptt-architect: write `docs/brain/B53-LaneC/04-tickets.md`
  4. ptt-ticket-reviewer: TICKET_REVIEW_PASS gate
  5. ptt-engineer: verify existing LaneC code matches plan (or correct if needed)
  6. ptt-verifier: run all 7 scans; issue VERIFY_PASS
  7. ptt-plan-reviewer (Phase 5): issue FINAL_PASS for LaneC
**Priority**: P1 — LaneC code is live in the deployed AddOn. It fires on every leader entry
  cancellation. Without pipeline verification, behavioral correctness is unconfirmed.
**Target block**: B53-LaneC pipeline (next immediate task after B53-LaneB FINAL_PASS is cleared)
**Note on test count**: T_B53C_01 and T_B53C_02 contribute to the actual test count but were not
  part of the B53-LaneB ticket scope. These tests will be formally counted and credited in the
  B53-LaneC pipeline.

---

### DW-B53-DRAG-F5-01: F5 gate for limit drag sync

**From**: B53-LaneB — Final Review Section G-01 (forward reference)
**Status**: OPEN — runtime verification not performed
**Root cause**: `SyncFollowerEntryDrag` calls `acc.Change(new Order[] { fo })` where `fo` is a
  "PTT-Copy" follower entry order. This path is logically correct and matches the
  `SyncFollowerBracket` pattern confirmed in B34+. However, the specific sequence of:
    leader drags working limit entry → ChangeSubmitted fires → follower PTT-Copy price updated
  has NOT been run against NT8 simulator.
**Resolution needed**:
  1. Open NinjaTrader with Sim101 (leader) and Sim102 (follower) configured in TradeCopierPanel
  2. Place a working limit entry order on Sim101 (e.g., buy limit at current-5 ticks)
  3. Drag the limit entry to a new price level using NT8 chart or order grid
  4. Observe: NT8 Output tab should show "PTT-Drag: synced Sim102 PTT-Copy to [new price]"
  5. Observe: Sim102 "PTT-Copy" order should show the updated limit price in the Orders tab
  6. Confirm: no CS error, no "PTT-Drag: no PTT-Copy entry found" message
**Acceptance criteria**:
  - NT8 Output: "PTT-Drag: synced Sim102 PTT-Copy to X.XX" appears after drag
  - Sim102 Orders tab shows "PTT-Copy" at updated price within 1 second
  - No exceptions or error messages in NT8 log
  - Sim102 order does not show duplicate entries or ghost orders
**Priority**: P1 — B53-LaneB cannot be considered production-ready without live F5 confirmation.
**Target block**: B53 F5 gate (run alongside or after DW-B54-02 ATM bracket test)
**Notes**:
  - `OrderState.ChangeSubmitted` was confirmed to compile cleanly (CS0117 not triggered by build).
  - acc.Change() on PTT-Copy (AddOn-owned) orders is confirmed safe per NT8-046 analysis.
  - The F5 test is the final behavioral verification step.

---

### DW-B53-BTAG-01: BUILD_TAG does not reflect LaneB feature

**From**: B53-LaneB — Final Review Section A-05 and G-04 (FAIL finding)
**Status**: CLOSED — resolved in Phase 5 RETRY (orchestrator applied fix 2026-08-10)
**Root cause**: `PttBuild.Tag` at CopyEngine.cs line 44 reads:
  `"PTT-COPIER B53 | cancel-propagation | 2026-08-10"`
  This was set by the prior engineer run that pre-added LaneC code. The LaneB targeted fix run
  did not revert or update the tag.
**Impact**: When the Director injects the AddOn and observes the NT8 Output tab, the tag
  reads "cancel-propagation" — a LaneC label — preventing unambiguous confirmation that LaneB
  drag-sync code is live.
**Resolution needed** (one of):
  Option A (immediate): Update CopyEngine.cs line 44 to:
    `"PTT-COPIER B53 | limit-drag-sync | 2026-08-10"`
    Run `verify_links.ps1 -Fix` and re-confirm build clean.
  Option B (deferred to LaneC): Update to combined label during B53-LaneC pipeline:
    `"PTT-COPIER B53 | drag-sync+cancel-prop | 2026-08-10"`
    This bundles both features in one tag update.
**Priority**: P2 — diagnostic observability only; no runtime behavior affected.
**Target block**: B53-LaneC pipeline (Option B) or immediate hotfix (Option A)
**Note for Director**: FINAL_PASS for B53-LaneB is blocked by this finding (Section A-05).
  If the Director authorizes Option B (defer to LaneC tag update), FINAL_PASS may be granted
  with this item carried as OPEN in the B53-LaneC deferred backlog entry.
