# B183 Deferred Backlog
## ptt-plan-reviewer Phase 5

**Date**: 2026-07-07
**Author**: ptt-plan-reviewer (Phase 5)
**Epic**: B183 -- Re-introduce 16 Symbols Dropped by 878a5b33 Megacommit
**Status**: OPEN items awaiting Director triage

---

## Purpose

This document records all items identified during B183 as out-of-scope deferred work.
Items are sourced from verifier reports (T1-T7), the Phase 2 plan review, and Phase 5
cross-file coherence review. Each item is assigned a priority and target block.

**P0 = Compile-blocking or P0 DNA violation. Must be resolved before shipping.**
**P1 = Architecture risk. Must be resolved before next major feature wave.**
**P2 = Quality / testing gap. Track and address in near-term pipeline.**
**P3 = Low-risk cleanup / informational. Address when convenient.**

---

## Deferred Work Register

### DW-B183-06: PendingBeArmed event absent from CopyEngine.cs

| Field | Value |
|-------|-------|
| **Description** | `PendingBeArmed` event declaration was dropped from `CopyEngine.cs` by megacommit 878a5b33. `TradeCopierPanel.cs` T7 correctly subscribes to `_engine.PendingBeArmed += OnPendingBeArmedDispatch` (line 790) and unsubscribes at line 569. These lines reference a non-existent event. The solution will NOT compile until this event is restored. |
| **Source** | T7 verifier (ticket-7-verification.md Section "Ph5 Deferred Backlog Items") |
| **Risk Level** | **P0 -- Compile-blocking** |
| **Impact** | NT8 build fails. Panel cannot initialize. BUG3 BE arm/disarm infrastructure non-functional. |
| **Suggested Ticket** | B184-T1 (CopyEngine.cs BE event infrastructure restoration) |
| **Fix Source** | `git show f01b5155:src/PropTraderTools/CopyEngine.cs` -- grep for `PendingBeArmed` event declaration |
| **Target Block** | B184 |
| **Status** | OPEN |

---

### DW-B183-07: GlobalBeAllDisarmed event absent from CopyEngine.cs

| Field | Value |
|-------|-------|
| **Description** | `GlobalBeAllDisarmed` event declaration was dropped from `CopyEngine.cs` by megacommit 878a5b33. `TradeCopierPanel.cs` T7 subscribes at line 791 and unsubscribes at line 570. These reference a non-existent event. |
| **Source** | T7 verifier (ticket-7-verification.md Section "Ph5 Deferred Backlog Items") |
| **Risk Level** | **P0 -- Compile-blocking** |
| **Impact** | NT8 build fails. BE ALL disarm visual state cannot update. |
| **Suggested Ticket** | B184-T1 (CopyEngine.cs BE event infrastructure restoration -- same ticket as DW-B183-06) |
| **Fix Source** | `git show f01b5155:src/PropTraderTools/CopyEngine.cs` -- grep for `GlobalBeAllDisarmed` event declaration |
| **Target Block** | B184 |
| **Status** | OPEN |

---

### DW-B183-08: DisarmPendingBe method absent from CopyEngine.cs

| Field | Value |
|-------|-------|
| **Description** | `DisarmPendingBe` method was dropped from `CopyEngine.cs` by megacommit 878a5b33. Called from `TradeCopierPanel.cs DisarmBeAllOnFlat` at line 3070: `CopyEngine.Instance.DisarmPendingBe(_leaderAccount)`. Compile-blocking reference. |
| **Source** | T7 verifier (ticket-7-verification.md Section "Ph5 Deferred Backlog Items") + T7 completion (Notes section line 215) |
| **Risk Level** | **P0 -- Compile-blocking** |
| **Impact** | NT8 build fails. DisarmBeAllOnFlat is dead code until this is restored. |
| **Suggested Ticket** | B184-T1 (CopyEngine.cs BE event infrastructure restoration) |
| **Fix Source** | `git show f01b5155:src/PropTraderTools/CopyEngine.cs` -- grep for `DisarmPendingBe` |
| **Target Block** | B184 |
| **Status** | OPEN |

---

### DW-B183-09: IsPendingSlotsEmpty + RaiseBeAllDisarmed methods absent from CopyEngine.cs

| Field | Value |
|-------|-------|
| **Description** | `IsPendingSlotsEmpty` and `RaiseBeAllDisarmed` methods were dropped from `CopyEngine.cs` by megacommit 878a5b33. Both are called from `TradeCopierPanel.cs DisarmBeAllOnFlat` at lines 3067 and 3071 respectively. Compile-blocking references. |
| **Source** | T7 verifier (ticket-7-verification.md Section "Ph5 Deferred Backlog Items") + T7 completion (Notes section lines 215-216) |
| **Risk Level** | **P0 -- Compile-blocking** |
| **Impact** | NT8 build fails. DisarmBeAllOnFlat guard logic and disarm propagation non-functional. |
| **Suggested Ticket** | B184-T1 (CopyEngine.cs BE event infrastructure restoration -- bundle with DW-B183-06, -07, -08) |
| **Fix Source** | `git show f01b5155:src/PropTraderTools/CopyEngine.cs` -- grep for `IsPendingSlotsEmpty`, `RaiseBeAllDisarmed` |
| **Target Block** | B184 |
| **Status** | OPEN |

---

### DW-B183-03: IsLaneEEligible CYC=8 AT BUDGET -- Frozen

| Field | Value |
|-------|-------|
| **Description** | `IsLaneEEligible` method in CopyEngine.cs has CYC=8, exactly at the JS-066 budget. This method is FROZEN. Any future branch addition (if/else, ||, &&) to this method is a P1 JS-066 violation before a helper extraction is done. Future engineers touching Lane E logic must be aware. |
| **Source** | Ph2 Plan Review (02-plan-review.md Section J OBS-2) + T4 completion + T4 verification |
| **Risk Level** | P1 -- Architecture constraint |
| **Impact** | Any unapproved branch addition to IsLaneEEligible = instant VERIFY_FAIL |
| **Suggested Action** | Extract a helper method BEFORE adding any new branch. Candidate: extract null-guard compound as `IsLaneEEligibleNullSafe(pos)`. |
| **Target Block** | future (when Lane E is modified) |
| **Status** | OPEN |

---

### DW-B183-10: DispatchCopy CYC=8 AT BUDGET -- Frozen

| Field | Value |
|-------|-------|
| **Description** | `DispatchCopy` in CopyEngine.cs reached CYC=8 after T3 added the `if (dispatched > 0)` branch. This is at the JS-066 budget. FROZEN. |
| **Source** | T3 completion (ticket-3-completion.md DispatchCopy CYC FREEZE NOTE) + T3 verification |
| **Risk Level** | P1 -- Architecture constraint |
| **Impact** | Any branch addition to DispatchCopy = P1 JS-066 violation before helper extraction |
| **Suggested Action** | Extract a helper before adding any branch to DispatchCopy. |
| **Target Block** | future (when DispatchCopy is modified) |
| **Status** | OPEN |

---

### DW-B183-01: xUnit tests for Slate Gate methods

| Field | Value |
|-------|-------|
| **Description** | `OnFollowerPositionUpdate`, `TrySetFollowerSlateReady`, `IsAccountFlat`, `HasAnyActiveOrders` have zero xUnit test coverage. These methods were designed for testability (static helpers, clear single-responsibility). No test project exists for the CopyEngine at time of B183. |
| **Source** | Ph2 Plan Review (02-plan-review.md Section K DW-B183-01) |
| **Risk Level** | P2 -- Testing gap |
| **Impact** | Regressions in slate gate logic will not be caught by automated tests |
| **Suggested Ticket** | B184 test ticket or standalone test coverage epic |
| **Target Block** | B184 |
| **Status** | OPEN |

---

### DW-B183-02: xUnit test for FlatGuardLoop

| Field | Value |
|-------|-------|
| **Description** | `FlatGuardLoop<TAccount>` is a static generic method with delegate injection -- ideal for unit testing without NT8 stubs. No test exists. |
| **Source** | Ph2 Plan Review (02-plan-review.md Section K DW-B183-02) |
| **Risk Level** | P2 -- Testing gap |
| **Impact** | FlatGuardLoop logic unvalidated. Flatten-on-flat regression undetectable. |
| **Suggested Ticket** | B184 test ticket |
| **Target Block** | B184 |
| **Status** | OPEN |

---

### DW-B183-04: _laneDDispatched unbounded growth

| Field | Value |
|-------|-------|
| **Description** | `_laneDDispatched` (ConcurrentDictionary<string, byte>) grows by one entry per successful Lane D ATM dispatch (`orderId + "#" + accName` key). It is never cleared. In typical sessions (~hundreds of orders) this is negligible (~60 bytes/entry). In very active sessions it could reach MB-scale. |
| **Source** | Ph2 Plan Review (02-plan-review.md Section K DW-B183-04) |
| **Risk Level** | P2 -- Memory growth |
| **Impact** | Negligible in practice, but unbounded. May cause memory pressure in long-running sessions. |
| **Suggested Action** | Add cleanup in `Unsubscribe()` or add a session-boundary clear. |
| **Target Block** | B185 |
| **Status** | OPEN |

---

### DW-B183-05: RULES_CATALOG.md truncated at JS-041

| Field | Value |
|-------|-------|
| **Description** | `docs/standards/jane-street/RULES_CATALOG.md` on disk contains only rules JS-001 through JS-041. Rules JS-042 through JS-110 (including JS-066 CYC and JS-081 ASCII) are absent. The reviewer's hardcoded DNA block in the role definition covers these rules, but the catalog file should be complete for use by other agents and tools. |
| **Source** | Ph2 Plan Review (02-plan-review.md Section J OBS-1) |
| **Risk Level** | P1 -- Ops/maintenance |
| **Impact** | Agents reading RULES_CATALOG.md directly will miss JS-042 through JS-110. Enforcement gap for automated tools. |
| **Suggested Action** | Restore full catalog content from backup or source. Verify encoding is UTF-8 no-BOM (workspace rule 05-utf8-encoding.md). |
| **Target Block** | ops (non-blocking for B184 -- fix in parallel) |
| **Status** | OPEN |

---

## Recommended B184-T1 Scope

The four P0 compile-blocking items (DW-B183-06, -07, -08, -09) should be bundled into a single
B184-T1 ticket:

**B184-T1: Restore CopyEngine.cs BE event infrastructure dropped by 878a5b33**

Symbols to restore (all from `git show f01b5155:src/PropTraderTools/CopyEngine.cs`):
1. `PendingBeArmed` event declaration + raise mechanism
2. `GlobalBeAllDisarmed` event declaration + raise mechanism
3. `DisarmPendingBe(Account leader)` method
4. `IsPendingSlotsEmpty()` method
5. `RaiseBeAllDisarmed()` method

**Constraint**: B180-2 approved removals (`ResetBeStateOnFlat`, `ExecuteBeIdle`, `ExecuteBeArmed`,
`UpdateBeVisuals`, `OnBeConnected`) remain excluded. This ticket restores ONLY the items listed above.

**Verification**: After B184-T1, run `dotnet build` or NT8 compile to confirm the solution compiles.
The guardian script will not need modification (it does not check for these symbols).

---

## Summary by Priority

| Priority | Count | Items |
|----------|-------|-------|
| P0 (Compile-blocking) | 4 | DW-B183-06, -07, -08, -09 |
| P1 (Architecture / Ops risk) | 3 | DW-B183-03, -05, -10 |
| P2 (Testing / Quality) | 3 | DW-B183-01, -02, -04 |

**Total deferred items**: 10

---

*Written by: ptt-plan-reviewer (Phase 5) -- 2026-07-07*
*This document is required for FINAL_PASS (role definition mandate).*
