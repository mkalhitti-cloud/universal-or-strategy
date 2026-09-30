# BWAVE-REFACTOR LaneC -- Deferred Backlog

**Epic**: BWAVE-REFACTOR LaneC
**Branch**: bwave-refactor-lane-c
**Workspace**: `C:\WSGTA\ptt-lane-c\`
**Last Updated**: 2026-09-06 (Block: LaneC-FINAL)
**Maintained by**: ptt-plan-reviewer (Phase 5)

---

## Block: LaneC-FINAL (2026-09-06)

### DW-LC-01: AT-LIMIT CCN=8 Methods -- One-Branch Guard Required

**Priority**: P1
**Target Block**: B6/future
**Status**: OPEN

Three methods reached CCN=8 exactly after extraction:
- `PttQuickExit.Execute` (base 1 + foreach 1 + pos-null-|| 2 + follower-&& 2 + for 1 = 8)
- `PttGlobalQuickExit.Execute()` (base 1 + flag 1 + acc-loop 1 + follower-skip 1 + pos-loop 1 + null/flat 1 + flatten-guard 1 = 7 + ExecuteFollowers call absorbs one)
- `PttBreakEvenSwap.Execute` (base 1 + null-|| 1 + flat-|| 1 + isLong 1 + targets-|| 1 + targets.Count 1 + for 1 = 8)

Any future feature addition (new guard, new branch, new loop) to any of these three methods will exceed CCN=8 and violate the Jane Street complexity mandate.

**Protocol**: Before adding any branch to these three methods, the engineer MUST first extract an existing branch into a helper to create CCN headroom. This check must happen at plan review time (Phase 2) before implementation.

---

### DW-LC-02: ResolveOrderParams Duplication in PttTrim and PttFlatten

**Priority**: P2
**Target Block**: B6/future
**Status**: OPEN

`PttTrim.ResolveOrderParams` and `PttFlatten.ResolveOrderParams` are structurally identical:
- Same signature: `(Position pos, int buffer, double ask, double bid, double tickSize)`
- Same return type: `(OrderType orderType, double limitPrice, double stopPrice)`
- Identical body: `tickSize>0 && (Long?ask:bid)>0` check, useLimitOrder branch

Current design intentionally keeps them separate (each class is self-contained, no cross-file imports to avoid coupling). However, if the order pricing logic changes (e.g., buffer applies differently for limit vs market), the change must be made in two places.

**Proposed fix**: Extract to a shared `PttOrderParams` static class in `Core/PttOrderUtils.cs` (new file), imported by both. Requires a plan-review pass for any new extraction that introduces a shared helper between Features/*.cs files.

---

### DW-LC-03: Pre-existing FindPositionLocal return null (JS-002 spirit)

**Priority**: P2
**Target Block**: B6/future
**Status**: OPEN

Three pre-existing `FindPositionLocal` copies each return `null` when no position is found:
- `PttBreakEven.cs:556,560`
- `PttTrim.cs:198,202`
- `PttFlatten.cs:188,192`

This violates the spirit of JS-002 (use Option<T> instead of null). Not introduced by LaneC (pre-existing). All callers have null guards. Functionally safe.

**Future work**: Introduce `Option<Position>` return type from `FindPositionLocal` (or a static utility variant). This requires changes to all callers (they currently do `if (pos == null || pos.Quantity == 0)` null guards). Scope: medium (3 files, 5 call sites).

---

### DW-LC-04: SubmitQxOcoPair Test -- Overload Disambiguation Gap

**Priority**: P2
**Target Block**: B6/future
**Status**: OPEN

`PttQuickExit_SubmitQxOcoPair_Exists` test uses `Assert.Equal(12, m.GetParameters().Length)` which verifies the correct 12-param `ref` signature currently. However, `GetMethod("SubmitQxOcoPair", ...)` (single-name lookup) returns null if an additional overload is ever added with the same name, causing the test to fail with `NullReferenceException` rather than a clear assertion failure.

**Proposed fix**: Change to `GetMethods(...).FirstOrDefault(m => m.Name == "SubmitQxOcoPair" && m.GetParameters().Length == 12)` for robustness. Low priority as no overload is planned.

---

### DW-LC-05: Doc Comment CCN Drift Post-Extraction

**Priority**: P2
**Target Block**: B6/future
**Status**: OPEN

Several doc comments contain CCN values that pre-date the LaneC extraction:

| File | Method | Doc Comment Says | Actual Post-Extraction CCN |
|------|--------|-----------------|---------------------------|
| `PttQuickExit.cs` | `Execute` | "CYC=7: null/flat guard(1) + follower guard(2) + snapshotStop guard(3) + isLong(4) + for-loop(5) + stop-submit null check(6) + target-submit null check(7)" | CCN=8 (two `||` ops in guards not counted in old doc) |
| `PttGlobalQuickExit.cs` | `SnapshotTargetOrders` | "CYC=5" | CCN=6 (per ticket review table) |
| `PttGlobalQuickExit.cs` | `CancelPttBeOrders` | "CYC=7" | CCN=5 (post-extraction with IsNonTerminalForInstr) |
| `PttGlobalQuickExit.cs` | `WaitForPttBeCancelled` | "CYC=7" | CCN=6 (post-extraction) |
| `PttBreakEven.cs` | `SnapshotTargetsLocal` | "CYC=3" | CCN=5 (per ticket review table) |
| `PttBreakEven.cs` | `CancelStaleBracketsLocal` | "CYC=3" | CCN=6 (per ticket review table) |

These documentation drifts do not affect runtime behavior. However, future reviewers relying on embedded CCN annotations for pre-checks may reach incorrect conclusions. A documentation-only pass is recommended.

---

### DW-LC-06: IsCancellableState and IsNonTerminalPttBeState -- Potential Shared Utility

**Priority**: P2
**Target Block**: future
**Status**: OPEN

Two helpers have overlapping but different semantics:

- `PttBreakEven.IsCancellableState(OrderState s)` -- returns true for {Working|Initialized|Submitted|Accepted|TriggerPending}
- `PttGlobalQuickExit.IsNonTerminalPttBeState(OrderState s)` -- returns true for NOT {Cancelled|Filled|Rejected|PartFilled|Unknown}

Both are ORDER STATE predicates. The set covered by `IsCancellableState` is a proper subset of `IsNonTerminalPttBeState` (non-terminal also covers CancelPending, CancelSubmitted). They are intentionally different.

A future wave may benefit from a `PttOrderStatePredicates` shared utility (similar to the DW-LC-02 `ResolveOrderParams` suggestion) to prevent divergence if the NT8 order state machine changes. Deferred until clear evidence of divergence.

---

## Notes

- All 6 deferred items are P1 or P2. **Zero P0 items are deferred** (P0 items are blocking and must be resolved before FINAL_PASS).
- Items DW-LC-01 through DW-LC-06 were identified during the Phase 5 Final Review on 2026-09-06.
- This file will be appended in future LaneC blocks. Prior entries will be updated to CLOSED when resolved.
