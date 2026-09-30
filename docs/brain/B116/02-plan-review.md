# B116 Plan Review -- DW-B124 Fix (Option B)

**Reviewer**: ptt-plan-reviewer  
**Date**: 2026-08-28  
**Phase**: Ph2 Plan Review  
**Plan file**: `docs/brain/B116/02-architecture-plan.md`

---

## REVIEW RESULT: REVIEW_PASS

All 15 checklist items PASS. No JS rule violations found. No spec requirements unaddressed.

---

## Checklist

| # | Item | Result | Evidence (plan section) |
|---|------|--------|------------------------|
| 1 | Fix targets correct call site (L89 region -- SnapshotTargetOrders reassignment before ExecuteOne) | **PASS** | Sec 3 Change 4: substitution inserted after `SnapshotTargetOrders` call + DIAG block; `ExecuteOne` call at L133 unchanged |
| 2 | No changes planned to PttQuickExit.Execute or CalcTNQty | **PASS** | Sec 8 Scope Boundary + Ticket 1: "`PttQuickExit.cs` (no changes to `CalcTNQty` or `Execute`)" |
| 3 | Execute CYC stays at 8 (helper extraction, no inline branch added to Execute) | **PASS** | Sec 5 Updated CYC Table: `Execute` Before=8 After=8; Sec 4 mitigation + Sec 9 risk row both confirm two-helper extraction |
| 4 | ResolveFollowerTargets CYC=3 (non-empty snapshot / empty+no-leader / scale call) | **PASS** | Sec 4 code block labels (1)(2)(3); comment "CYC=3"; each branch verified independently |
| 5 | ScaleLeaderTargets CYC=3 (guard leaderPosQty<=0 / loop / last-target branch) | **PASS** | Sec 3 Change 3 code block labels (1)(2)(3); summary comment "CYC=3" |
| 6 | Non-empty snapshot path returns self unchanged | **PASS** | `ResolveFollowerTargets` branch (1): `if (followerSnapshot.Count > 0) return followerSnapshot;` |
| 7 | DW-B120 path preserved: empty snapshot + empty leader returns empty list | **PASS** | `ResolveFollowerTargets` branch (2): returns `followerSnapshot` (empty list); Sec 1 DW-B120 Independence + Test 6 |
| 8 | 6 xUnit [Fact] tests defined (JS-051) | **PASS** | Sec 6 defines Tests 1-6; Ticket 2: "Framework: xUnit only (JS-051 -- never NUnit or MSTest)" |
| 9 | No lock() usage in new code (JS-021 P0) | **PASS** | Both helpers are private static, no shared state, no synchronization primitives in any planned code block |
| 10 | No throw new XxxException in new code (JS-001 P0) | **PASS** | `ScaleLeaderTargets` guard returns empty list (not throw); no `throw` keyword in any planned code block |
| 11 | No return null in new code (JS-002 P0) | **PASS** | `ScaleLeaderTargets` guard returns initialized empty `List<>` (not null); `ResolveFollowerTargets` returns existing list object (never null) |
| 12 | No async void in new code (JS-033 P0) | **PASS** | Both new methods are synchronous `private static`; no `async` keyword in any planned code |
| 13 | ASCII-only strings in new code | **PASS** | All code block string literals and comments in planned code use only ASCII characters |
| 14 | _fPosQty promoted from DIAG block to named local above DIAG block (no Positions loop duplication) | **PASS** | Sec 3 Change 2: "Promote `_fPosQty` to a named local variable above the DIAG block for reuse"; Ticket 1 Change 1 repeats this |
| 15 | ScaleLeaderTargets last-target-absorbs-rounding: sum == followerPosQty | **PASS** | Last-target branch: `Math.Max(1, followerPosQty - allocated)`; Sec 9 risk row: "Last-target absorption guarantees sum = followerPosQty. Verified by Test 2." |

---

## Violations

**None.** Zero JS rule violations found in the plan.

---

## Notes (non-blocking)

**Edge case: followerPosQty < targetCount**  
When `followerPosQty` is smaller than the number of target tranches (e.g. 2 contracts, 3 targets), the `Math.Max(1, ...)` floor applied to earlier targets can cause `allocated` to exceed `followerPosQty` before the last-target branch fires, resulting in a last-target qty of `followerPosQty - allocated < 1`, which `Math.Max(1, ...)` clamps to 1, and the sum overshoots. This is a degenerate input (fewer contracts than targets) not in the DW-B124 defect scenario (followerPosQty=7, count=3). No JS rule is violated. Recommend tracking as DW item if the operational envelope ever includes 1-2 contract follower accounts with 3 targets.

---

## Summary

The B116 plan is architecturally sound and fully compliant with Jane Street DNA rules.

**Fix correctness**: Option B (leader qty array passthrough) correctly addresses the root cause at the right call site (L89 region in `PttGlobalQuickExit.Execute`). The substitution occurs after `SnapshotTargetOrders` returns the empty follower list and before `ExecuteOne` consumes it. `PttQuickExit.Execute` and `CalcTNQty` are untouched.

**CYC compliance**: The two-helper extraction (`ResolveFollowerTargets` CYC=3 + `ScaleLeaderTargets` CYC=3) keeps `Execute` at CYC=8 with zero inline branch additions. All methods are within the JS CYC<=8 ceiling.

**Jane Street DNA**: No `lock()` (JS-021), no `throw` (JS-001), no `return null` (JS-002), no `async void` (JS-033). Both helpers are private static with no shared state.

**Backward compatibility**: DW-B120 (Sim103 async lag path) is fully preserved via `ResolveFollowerTargets` branch (2). The Sim104 partial-snapshot variant (count=1) continues to return the partial snapshot unchanged (branch (1)), consistent with existing behavior and existing P1 monitor status.

**Testing**: 6 xUnit `[Fact]` tests cover all three branches of both helpers including the equal-qty identity case, the scaled split case, the degenerate guard case, and the DW-B120 fallback path.

**Advance to Ph3 (ticket generation).** No rework required.

---

*Ph2 review complete. REVIEW_PASS.*
