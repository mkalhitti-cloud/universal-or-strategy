# PR #20 Triage -- wave7/pr1-s2-execution (Round 2)
# Lane: L1  Cluster: S2-Execution
# Generated: Phase 7 Lane Orchestrator

## Summary
TRIAGE_DONE PR#20  logic=2  mech=2  dna=1  hall=4  noise=3  fixed=2  skip=1

---

## Finding Classifications

### F1 -- ExecuteStopReplacementIfActive: rename _rPos/_rQty/_snap + remove stale comment
- **File**: src/V12_002.Orders.Callbacks.AccountOrders.cs ~L795-812
- **Classification**: VALID-MECHANICAL (partial ALREADY-FIXED)
- **Ground Truth** (read_file L795-812): Locals are already `rPos`, `rQty`, `snap` -- camelCase.
  Variable rename NOT needed. The stale comment "Move guard inside lock" IS present on L797.
- **Action**: Remove stale comment on L797. VALID-MECHANICAL.

### F2 -- SA1503: missing braces on single-line if/else bodies
- **File**: src/V12_002.Orders.Callbacks.AccountOrders.cs ~L140-142
- **Classification**: VALID-MECHANICAL
- **Ground Truth** (read_file L137-143): Lines 139-142 have no braces on if/else if.
  `dotnet csharpier format src/` covers this across all PR diff files.
- **Action**: Run CSharpier format after all fixes.

### F3 -- IsOrderForThisInstrument: reject null instruments
- **File**: src/V12_002.Orders.Callbacks.AccountOrders.cs L82
- **Classification**: VALID-LOGIC-BUG
- **Ground Truth** (read_file L80-83): `return order.Instrument == null || order.Instrument.FullName == Instrument.FullName;`
  Current logic: returns true when instrument is null (passes null-instrument orders through).
  CodeRabbit finding: should reject null instruments, not accept them.
  OKF: production-engineering-billions.md independent_tracking -- ghost orders with null instruments
  should be rejected at the gate, not allowed through to further processing.
- **Action**: Change `== null ||` to `!= null &&`.

### F4 -- IsPendingCancelFsmMatch: add fsm != null guard
- **File**: src/V12_002.Orders.Callbacks.AccountOrders.cs L477-481
- **Classification**: VALID-LOGIC-BUG
- **Ground Truth** (read_file L476-482): `TryGetValue` can produce a null FSM if a null value
  was stored. The chained `.State` dereference would NPE.
  Also: TryHandleReplaceSpecCancellation at L1075-1080 reads fsm.State without null check.
- **Action**: Add `&& fsm != null` guard in IsPendingCancelFsmMatch. Add `if (fsm == null) continue;`
  in TryHandleReplaceSpecCancellation L1075.

### F5 -- IsBrokerOrderLive: include broker-pending states
- **File**: src/V12_002.Orders.Management.Cleanup.cs L615-619
- **Classification**: VALID-LOGIC-BUG
- **Ground Truth** (read_file L614-620): Only `Working || Accepted` -- missing `PendingSubmit`,
  `PendingChange`, `PendingCancel`. Ghost audit misses orders in these states.
  OKF: production-engineering-billions.md staleness_guard.
- **Action**: Add `|| order.OrderState == OrderState.PendingSubmit || order.OrderState == OrderState.PendingChange || order.OrderState == OrderState.PendingCancel`

### F6 -- PropagateMasterTargetMove: route through FSM instead of raw Cancel+Submit
- **File**: src/V12_002.Orders.Callbacks.Propagation.cs L490-547
- **Classification**: SKIP (NEEDS_DIRECTOR)
- **Ground Truth**: `SubmitFollowerTargetReplacement` and `FollowerTargetReplaceSpec` exist.
  However, the PropagateMasterTargetMove/ResubmitTargetOrder path has a comment (L551-555)
  explicitly documenting this as intentional: "Cancel + CreateOrder + Submit is the sole path,
  consistent with PropagateMasterTargetMove and UpdateStopOrder throughout this codebase."
  The FSM path (SymmetryGuardReplaceExistingFollowerTarget) is for symmetry guard use cases
  and requires a CancellingOrderId to be set for Phase 2 detection.
  Migrating ResubmitTargetOrder to FSM requires: (a) building a FollowerTargetReplaceSpec,
  (b) storing CancellingOrderId, (c) canceling only, then (d) Phase 2 fires on cancel confirm.
  This is a non-trivial architectural change that could expose positions if done incorrectly.
- **Action**: SKIP -- mark NEEDS_DIRECTOR. Note in repair-log.

### F7-F14 -- CSharpier braces throughout diff files
- **Classification**: VALID-MECHANICAL (covered by CSharpier run in F2)
- **Action**: Single `dotnet csharpier format src/` covers F2 and F7-F14 together.

---

## Pre-existing fixes (ALREADY-FIXED)
- REPAIR-06: UtcNow consistency (StopSync + Trailing), O(N) Contains removal, ASCII em-dash, oqDepth
- REPAIR-06b: underscore locals to camelCase
- REPAIR-07: remaining underscore locals
- REPAIR-08: null guard for order.Name in IsTrackedOrderPattern

---

## Non-blocking / Noise
- markdown-link-check FAIL: informational, not a merge gate
- opencode/GLM/Qwen review FAIL: missing API keys, non-blocking
- Build & Run Pyramid Suites: pre-existing LogicTests.cs C#14 issue on main

---

## Actionable Findings (this round)
1. F1 (MECH) -- remove stale "Move guard inside lock" comment
2. F2+F7-F14 (MECH) -- CSharpier format
3. F3 (LOGIC) -- IsOrderForThisInstrument null reject
4. F4 (LOGIC) -- IsPendingCancelFsmMatch null guard + TryHandleReplaceSpecCancellation null guard
5. F5 (LOGIC) -- IsBrokerOrderLive pending states
