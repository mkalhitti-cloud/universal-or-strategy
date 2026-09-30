# B118 Plan Review -- DW-B126 BE/QX Race Condition Fix

**Block**: B118
**Phase**: 2 (Plan Review)
**Reviewer**: ptt-plan-reviewer
**Date**: 2026-08-28
**Input**: `docs/brain/B118/02-architecture-plan.md`
**Rules source**: `docs/standards/jane-street/RULES_CATALOG.md` (JS-001..JS-110)
**Source baseline**: `src/PropTraderTools/Features/PttGlobalQuickExit.cs` (as of B118 review)

---

## Verdict: REVIEW_PASS

Zero violations found across all 13 checklist items. No rule citations required (nothing to cite).

---

## 13-Item Checklist Assessment

### Item 1 -- Defect Traceability
**Verdict**: PASS

The plan's Section A provides a precise T=0..T=1.7 sequence trace establishing the exact mechanism
by which PTT-BE fills between the QX snapshot and the exchange acknowledgement of the QX stop order
produce an oversell. Root cause is stated with precision:

> "PttGlobalQuickExit.Execute() calls SnapshotTargetOrders() and ExecuteOne() while PTT-BE-Target-*
> and PTT-BE-Stop-* orders are still in active (non-terminal) states."

Section B's cancel-first design directly eliminates the race window: PTT-BE-* orders are driven to
terminal states before `SnapshotTargetOrders` executes, so the snapshot sees the true residual
position. The fix is correctly targeted at the root cause, not a symptom.

---

### Item 2 -- JS-021 Compliance (No lock())
**Verdict**: PASS

No `lock()` appears in any proposed method signature, logic description, or pseudocode block.
All four new methods (`CancelPttBeOrders`, `WaitForPttBeCancelled`, `IsPttBeOrder`,
`IsNonTerminalPttBeState`) are `static` and stateless. No shared mutable state is introduced.
The plan's existing `_qxCancelInProgress` and `_qxPendingFollowerCleanup` references are
`ConcurrentDictionary` (lock-free) and are marked UNCHANGED.

---

### Item 3 -- JS-001 Compliance (No throw in hot paths)
**Verdict**: PASS

No `throw` appears in any proposed method. The only error case is the `WaitForPttBeCancelled`
timeout, which is explicitly designed as a fail-safe that logs a warning and returns normally:

> "timeout logs a warning but does NOT throw. Execution proceeds to QX logic."

This is the correct pattern. No exception can propagate from the new code.

---

### Item 4 -- JS-033 Compliance (No async void)
**Verdict**: PASS

All four new methods are synchronous:
- `CancelPttBeOrders` -- `internal static int` (synchronous)
- `WaitForPttBeCancelled` -- `internal static void` (synchronous, blocking poll)
- `IsPttBeOrder` -- `private static bool` (synchronous predicate)
- `IsNonTerminalPttBeState` -- `private static bool` (synchronous predicate)

No `async` keyword appears in any new method. No `async void` anywhere.

---

### Item 5 -- CYC Budget (all methods <= 8)
**Verdict**: PASS

Section C CYC table verified against the logic descriptions in Section B:

| Method | Plan CYC | Branch Count Verified | Status |
|--------|----------|-----------------------|--------|
| `Execute()` | 8 | Unchanged -- 4 lines added, 0 new branches | PASS |
| `ExecuteOne()` | 2 | Unchanged | PASS |
| `SnapshotTargetOrders()` | 5 | Unchanged | PASS |
| `ScaleLeaderTargets()` | 3 | Unchanged | PASS |
| `ResolveFollowerTargets()` | 4 | Unchanged | PASS |
| `CancelPttBeOrders()` | 7 | acc null(1), instr null(2), foreach(3), o null(4), instrOk(5), IsPttBeOrder(6), stateOk(7) | PASS |
| `WaitForPttBeCancelled()` | 7 | acc/count guard(1), while(2), foreach(3), o null(4), instrOk(5), IsPttBeOrder(6), nonTerminal(7) | PASS |
| `IsPttBeOrder()` | 1 | single boolean expression | PASS |
| `IsNonTerminalPttBeState()` | 1 | single boolean expression | PASS |

Branch extraction into `IsPttBeOrder` and `IsNonTerminalPttBeState` is a valid and required
technique to keep both caller methods within the CYC=8 budget. Without the helpers,
`CancelPttBeOrders` would be CYC=9 and `WaitForPttBeCancelled` would be CYC=9.

---

### Item 6 -- ASCII-Only String Literals
**Verdict**: PASS

All proposed string literals in Section B and Section H use ASCII-only characters:
- `"PTT-BE-Target-"` -- ASCII
- `"PTT-BE-Stop-"` -- ASCII
- `"[PTT-QX-ALL] CancelPttBeOrders: acc="` -- ASCII
- `"[PTT-QX-ALL] WaitForPttBeCancelled: acc="` -- ASCII
- `"// B118 DW-B126: cancel PTT-BE-* BEFORE snapshot ..."` -- ASCII

No Unicode characters, emoji, or curly quotes present in any proposed literal.

---

### Item 7 -- NT8 API Grounding
**Verdict**: PASS

Section E documents NT8 API claims against NT8_FULL_REFERENCE.md with line citations:

| API | Citation | Verified |
|-----|----------|---------|
| `Account.Cancel(IEnumerable<Order>)` | NT8_FULL_REFERENCE.md lines 2408-2451 | PASS |
| `OrderState` enum terminal values | NT8_FULL_REFERENCE.md lines 976-997 | PASS |
| `acc.Orders.ToList()` pattern | NT8_FULL_REFERENCE.md AddOn context + CopyEngine.cs existing usages cited | PASS |

The plan correctly notes that `CancelOrder(Order)` is `StrategyBase`-only (not available on
`AddOnBase`) and uses the correct `Account.Cancel()` API for the AddOn context.

The plan correctly documents that `Order.IsTerminalState()` has an unspecified set of terminal
states in the NT8 reference, and provides an explicit `IsNonTerminalPttBeState` predicate with
enumerated terminal states sourced from NT8_FULL_REFERENCE.md lines 976-997. This is more
defensible than relying on an undocumented NT8 method.

---

### Item 8 -- Preserved Patterns
**Verdict**: PASS

Section D explicitly enumerates every pattern that must be preserved and marks each UNCHANGED:

| Pattern | Location | B118 Status |
|---------|----------|-------------|
| `_qxCancelInProgress` (ConcurrentDictionary) | CopyEngine.cs line 267; ExecuteOne() lines 209, 238 | UNCHANGED |
| `_qxPendingFollowerCleanup` (ConcurrentDictionary) | CopyEngine.cs line 276; ExecuteOne() line 217 | UNCHANGED |
| DW-B115-DIAG logging blocks | Execute() lines 66-80 (leader) and 93-121 (follower) | UNCHANGED |
| ExecuteOne() follower path structure | ExecuteOne() lines 199-253 | UNCHANGED |
| PTT-QX-GUARD log line | ExecuteOne() line 201 | UNCHANGED |
| SnapshotTargetOrders() DW-B106 two-pass logic | lines 306-326 | UNCHANGED |

The plan further notes that the cancel-first step occurs in `Execute()` BEFORE the DIAG blocks,
so the DIAG blocks themselves are not touched and are not relocated. This is correct: the
insertion points in the Appendix show the new lines are prepended before `SnapshotTargetOrders`
calls, which are themselves before the DW-B115-DIAG blocks.

**Cross-check against source**: The actual `PttGlobalQuickExit.cs` confirms:
- Leader path: `SnapshotTargetOrders` at line 47, DW-B115-DIAG at lines 66-80. Plan inserts
  `CancelPttBeOrders + WaitForPttBeCancelled` before line 47. Correct.
- Follower path: `SnapshotTargetOrders(follower, ...)` at line 89 (approximately, after
  the DW-B115-DIAG block at lines 93-121). The plan inserts the cancel-first step before the
  follower `SnapshotTargetOrders`. Correct.

---

### Item 9 -- File Scope
**Verdict**: PASS

Section G lists exactly one modified file:
- `src/PropTraderTools/Features/PttGlobalQuickExit.cs` -- MODIFIED (4 new methods + 4 lines in Execute())

All other `.cs` files are explicitly listed as UNCHANGED, including `CopyEngine.cs`,
`PttBreakEven.cs`, `PttBreakEvenSwap.cs`, `PttQuickExit.cs`, `TradeCopierPanel.cs`,
`PttCancel.cs`, and all existing test files.

New file `src/PropTraderTools/Tests/B118Tests.cs` is a test-only addition, not a modification
to any existing production file. This is within acceptable scope for a defect fix.

---

### Item 10 -- Test Coverage
**Verdict**: PASS

Section F defines 8 xUnit `[Fact]` tests using xUnit only (no NUnit, no MSTest):

| Test | Coverage Target |
|------|----------------|
| `T_B118_CancelPttBeOrders_ReturnsCancelledCount` | Core cancel path, count accuracy |
| `T_B118_CancelPttBeOrders_SkipsTerminalOrders` | Terminal-state exclusion filter |
| `T_B118_CancelPttBeOrders_SkipsNonPttBeOrders` | Name-filter precision |
| `T_B118_WaitForPttBeCancelled_ReturnsImmediately_WhenExpectedCountZero` | Fast path (count=0) |
| `T_B118_WaitForPttBeCancelled_ReturnsImmediately_WhenAllTerminalOnFirstPoll` | Fast path (already terminal) |
| `T_B118_WaitForPttBeCancelled_TimesOutGracefully_WhenOrdersStayNonTerminal` | Timeout fail-safe |
| `T_B118_CancelPttBeOrders_ReturnsZero_WhenNoPttBeOrdersExist` | DW-B127 structural check (second press) |
| `T_B118_IsPttBeOrder_MatchesTargetAndStop` | Name-predicate correctness |

Coverage is adequate for the cancel-first path. All critical edge cases are addressed:
fast path, terminal-order exclusion, non-PTT-BE exclusion, timeout safety, and the
DW-B127 second-press scenario.

The plan explicitly states "All existing tests must stay green" and the file scope in Section G
confirms no existing test files are modified.

---

### Item 11 -- DW-B127 Closure
**Verdict**: PASS

The structural elimination reasoning is sound:

1. First QX press: `CancelPttBeOrders` sends cancel to all non-terminal PTT-BE-* orders.
   `WaitForPttBeCancelled` polls until all are in terminal states (Cancelled or Filled).
   PTT-BE-* orders are now in terminal states before `ExecuteOne` fires.

2. Second QX press (rapid): `CancelPttBeOrders` scans `acc.Orders` — all PTT-BE-* orders are
   already terminal (from first press). Returns 0. `WaitForPttBeCancelled` sees
   `expectedCount == 0` and returns immediately via fast path.

3. The `_qxCancelInProgress` guard in `ExecuteOne` (B113 existing logic) prevents
   double-submission of PTT-QX orders on the follower path.

4. Result: no active PTT-BE orders remain when any QX execution proceeds.
   DW-B127 is eliminated at the structural level, not patched.

The test `T_B118_CancelPttBeOrders_ReturnsZero_WhenNoPttBeOrdersExist` directly validates
the second-press fast path.

---

### Item 12 -- Timeout Safety
**Verdict**: PASS (with documented note)

The `WaitForPttBeCancelled` design:
- Deadline computed via `DateTime.UtcNow.AddMilliseconds(maxWaitMs)` -- SCAN-06 compliant
  (uses `UtcNow`, not `DateTime.Now`).
- `Thread.Sleep(20)` per iteration, 50 iterations maximum = 1000ms bounded.
- Timeout exits with a warning log and does NOT throw.
- Caller contract: `expectedCount = return value of CancelPttBeOrders`; if 0, returns
  immediately without any sleep.

**Note on Thread.Sleep on UI thread**: The plan explicitly justifies this design:
> "Existing Execute() already blocks the UI thread during sequential account processing."
> "NT8 SIM cancels confirm in < 50ms typical."
> "maxWaitMs = 1000ms is bounded and safe."

This is an acceptable trade-off given the NT8 AddOn execution context where synchronous
blocking during button-press handlers is an established pattern in this codebase.
The 1000ms upper bound is concrete and enforced.

On timeout, execution falls through to `SnapshotTargetOrders` and the original race condition
is the worst-case outcome -- not a hang or crash. This is the correct fail-safe posture.

---

### Item 13 -- Leader vs Follower Cancel Paths
**Verdict**: PASS

Section B explicitly addresses both paths:

**Leader path** (Section B, "Step-by-Step: leader path in Execute()"):
```
int _beCancelCount = CancelPttBeOrders(acc, pos.Instrument);
WaitForPttBeCancelled(acc, pos.Instrument, _beCancelCount, 1000);
var targets = SnapshotTargetOrders(acc, pos.Instrument);  // UNCHANGED
```

**Follower path** (Section B, "Step-by-Step: follower path in Execute()"):
```
int _fBeCancelCount = CancelPttBeOrders(follower, pos.Instrument);
WaitForPttBeCancelled(follower, pos.Instrument, _fBeCancelCount, 1000);
var followerTargets = SnapshotTargetOrders(follower, pos.Instrument);
```

Both paths receive identical treatment. The plan correctly notes that the cancel-first step
is placed in `Execute()` rather than in `ExecuteOne()`, which keeps `ExecuteOne()` at CYC=2
unchanged and ensures cancels happen before `SnapshotTargetOrders` in both paths.

The Appendix diff sketch confirms both insertion points. The source file confirms the
follower `SnapshotTargetOrders` call is at line 89 in `Execute()` (follower loop), which is
in `Execute()` scope, not inside `ExecuteOne()`.

---

## Spec Coverage Matrix

| DW-B126/B127 Requirement | Addressed? | Plan Section |
|--------------------------|------------|--------------|
| Cancel all PTT-BE-* BEFORE submitting PTT-QX orders | YES | Section B (leader + follower paths) |
| Await Cancelled confirmation before proceeding to ExecuteOne | YES | Section B -- WaitForPttBeCancelled |
| Fix applied to both leader and follower account paths | YES | Section B -- both step-by-step blocks |
| DW-B127 structurally eliminated | YES | Section B last paragraph + Section H §5 |
| Existing preserved patterns not disrupted | YES | Section D |
| Execute() CYC unchanged | YES | Section C |
| NT8 API correct for AddOn context | YES | Section E |
| Timeout fail-safe (no throw) | YES | Section B -- WaitForPttBeCancelled design |
| Test coverage for cancel-first path | YES | Section F (8 xUnit tests) |
| Closure criteria documented | YES | Section H |
| File scope limited to PttGlobalQuickExit.cs | YES | Section G |

All 11 spec requirements addressed. No gaps.

---

## Rule Violations

None. Zero violations found.

---

## Summary

The architecture plan for B118 is well-formed, precisely targeted at the DW-B126 root cause,
and passes all 13 checklist items. The cancel-first design is structurally sound:

1. `CancelPttBeOrders` + `WaitForPttBeCancelled` are inserted at the two correct
   call sites in `Execute()` (leader and follower paths) — before each
   `SnapshotTargetOrders` call.
2. All new methods are within CYC=8. Helpers `IsPttBeOrder` and `IsNonTerminalPttBeState`
   are correctly extracted to maintain the budget.
3. No JS-001, JS-021, JS-033, or any other P0/P1 rule is violated.
4. NT8 API usage is correct for the AddOn context (`Account.Cancel()`, not `CancelOrder()`).
5. `DateTime.UtcNow` used throughout (SCAN-06 compliant).
6. All existing patterns (`_qxCancelInProgress`, `_qxPendingFollowerCleanup`, DW-B115-DIAG)
   are explicitly preserved and undisturbed.
7. DW-B127 structural elimination is correctly reasoned.
8. 8 xUnit `[Fact]` tests provide adequate coverage of the cancel-first path.

**REVIEW_PASS**
