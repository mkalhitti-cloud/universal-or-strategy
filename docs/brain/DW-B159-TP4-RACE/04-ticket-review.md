# Ticket Review: DW-B159-TP4-RACE
<!-- block: DW-B159-TP4-RACE -->
<!-- phase: 3.5 -->
<!-- produced-by: ptt-ticket-reviewer (Phase 3.5) -->
<!-- date: 2026-10-01 -->
<!-- tickets-source: docs/brain/DW-B159-TP4-RACE/04-tickets.md -->
<!-- plan-source: docs/brain/DW-B159-TP4-RACE/02-architecture-plan.md -->
<!-- plan-verdict: REVIEW_PASS (Cycle 2 confirmed in 02-plan-review.md) -->

---

## Block Header

| Field | Value |
|-------|-------|
| Epic | DW-B159-TP4-RACE -- FindFollowerBracketOrder Accepted-state race fix |
| Phase | 3.5 -- Ticket Review |
| Tickets reviewed | 2 (T1 + T2) |
| Files read | 04-tickets.md, 02-architecture-plan.md, 02-plan-review.md, DW-B159-KS/06-deferred-backlog.md, CopyEngine.cs (lines 4590-4618), CopyEngineTests.cs (lines 9820-9840) |

---

## Pre-Check Verification: Architect Claim vs. Actual Source

**Claim (04-tickets.md, ENGINEER PRE-CHECK RESULT):** `OrderState.Accepted` is present at
CopyEngine.cs line 4608. Fix confirmed.

**Direct read of CopyEngine.cs lines 4606-4613 (actual source):**

```
4607:     order.OrderState != OrderState.Working        // (5) branches -- DW-LB-FL-01 V7: Initialized added
4608:     && order.OrderState != OrderState.Accepted
4609:     && order.OrderState != OrderState.Submitted
4610:     && order.OrderState != OrderState.ChangeSubmitted
4611:     && order.OrderState != OrderState.Initialized
```

**Verdict:** Architect claim CONFIRMED. `OrderState.Accepted` is present at line 4608.
`OrderState.Submitted` also present at line 4609. Spec/code discrepancy confirmed.

**CopyEngineTests.cs insertion point (line 9829):** Confirmed. The outer `CopyEngineTests`
class closes its final `}` at line 9829. `B159KSuiteTests` class begins at line 9836
(separated by comment block). Insertion point is valid.

---

## T1 -- FindFollowerBracketOrder Accepted-State Verification + Tests

### Traceability

| Ticket Req ID | Maps To | Status |
|---------------|---------|--------|
| DW-B159-TP4-RACE-T1-REQ-01 | Plan: Ticket 1 Verification Scope item 2 (line 4608) | PASS |
| DW-B159-TP4-RACE-T1-REQ-02 | Plan: Engineer Pre-Check Result + Component List (READ ONLY) | PASS |
| DW-B159-TP4-RACE-T1-REQ-03 | Plan: SPEC/CODE DISCREPANCY section + Test 2 (Skipped) | PASS |
| DW-B159-TP4-RACE-T1-REQ-04 | Plan: Ticket 1 New Test Methods (both methods fully specified) | PASS |

All ticket items map to plan sections. No phantom work. No missing plan coverage.

**Traceability: PASS**

---

### JS Pre-Check (T1)

| Rule | Check | Finding |
|------|-------|---------|
| JS-021 | No `lock()` in test methods | PASS -- pure enum comparisons, no shared mutable state |
| JS-021 | No Monitor / Mutex / SemaphoreSlim | PASS -- not present |
| JS-023 | No UI update off-thread without Dispatcher.InvokeAsync | N/A -- no UI code |
| JS-001 | No `throw` in hot path | PASS -- test methods are not hot path; no throw |
| JS-002 | No `return null` for optional value | PASS -- void return methods |
| JS-003 | No empty string / missing-key sentinel for state | PASS -- not applicable |
| JS-008 | No mutable fields on struct | N/A -- not applicable |
| JS-009 | No Dictionary for shared collection | N/A -- not applicable |
| JS-042 | ASCII-only string literals (including Skip attribute string) | PASS -- all strings confirmed ASCII |
| JS-036 | No `DateTime.Now` | PASS -- not used |
| WS-GUARD | No whitespace mutation outside inserted lines | PASS -- insert-only |
| PTT-PREFIX | No `CreateOrder` calls | N/A -- PASS |

**JS Pre-Check: PASS**

---

### CYC Pre-Check (T1)

| Method | Branches | CYC | Verdict |
|--------|----------|-----|---------|
| `FindFollowerBracketOrder_AcceptedState_IsReturned` | 0 (straight-line bool + assert) | 1 | PASS |
| `FindFollowerBracketOrder_SubmittedState_IsSkipped` | 0 (straight-line bool + assert, body never executes) | 1 | PASS |

**CYC Pre-Check: PASS**

---

### NT8 Constraints (T1)

| Constraint | Check | Finding |
|------------|-------|---------|
| No async/await in lifecycle method | N/A -- test methods | PASS |
| No Account.All outside Loaded | N/A -- no Account API | PASS |
| No sealed on TradeCopierWindow | N/A -- not applicable | PASS |
| No FontFamily override | N/A -- no WPF | PASS |
| No hardcoded hex color | N/A -- no color | PASS |
| No CreateOrder without PTT- prefix | N/A -- no CreateOrder | PASS |
| No DateTime.Now | PASS -- not used | PASS |
| No NT8 runtime calls in test methods | PASS -- pure BCL OrderState enum, no NinjaTrader.Cbi cctor required | PASS |

**NT8 Constraints: PASS**

---

### Completeness (T1)

| Item | Check | Finding |
|------|-------|---------|
| Method signatures present | Both signatures with full decorator, access modifier, return type, parameters | PASS |
| Insertion point specified | "immediately before the final closing `}` of that class (which appears at line 9829)" -- confirmed by CopyEngineTests.cs read | PASS |
| Target class specified | outer `CopyEngineTests` class, NOT `B159KSuiteTests` | PASS |
| Full verbatim test source provided | Yes -- full source block in "Full Test Source" section | PASS |
| Zero production code changes explicitly stated | Yes -- stated in Scope Lock and Pre-Check Result | PASS |

**Completeness: PASS**

---

### Test Coverage (T1) -- CRITICAL FINDINGS

**Test 1: `FindFollowerBracketOrder_AcceptedState_IsReturned`**

The test body evaluates:

```csharp
bool acceptedInSet =
    OrderState.Accepted == OrderState.Working          // always false
    || OrderState.Accepted == OrderState.Accepted      // ALWAYS TRUE (compile-time constant)
    || OrderState.Accepted == OrderState.Submitted
    || OrderState.Accepted == OrderState.ChangeSubmitted
    || OrderState.Accepted == OrderState.Initialized;
Assert.True(acceptedInSet, ...);
```

**VIOLATION -- TR-T1-TEST1-TAUTOLOGY:**
`OrderState.Accepted == OrderState.Accepted` is a compile-time constant expression that
evaluates to `true` unconditionally, making `acceptedInSet` always `true` regardless of
any other term in the disjunction. `Assert.True(acceptedInSet)` will therefore pass
REGARDLESS of whether `OrderState.Accepted` is present in the production guard at
CopyEngine.cs line 4608 or not.

**Regression detection capability: ZERO.** If a future engineer removes
`OrderState.Accepted` from the `FindFollowerBracketOrder` guard, this test will still
pass. The test does not test the production guard -- it tests only the trivially-true
inline enum identity comparison that hardcodes the expected value on both sides.

This is a TEST COVERAGE FAIL. The test must be restructured to actually invoke
`FindFollowerBracketOrder` (or `FindFollowerBracketOrderTestable`, the existing test seam
at CopyEngine.cs line 4756 per plan Component List) with an order in `Accepted` state
and assert the returned order is not null. Alternatively, the test must use reflection to
inspect the method body or otherwise anchor to actual production behavior.

The current inline constant-comparison pattern provides no contract enforcement.
A test that cannot catch the regression it was designed to catch is a failed test.

**Test 2: `FindFollowerBracketOrder_SubmittedState_IsSkipped`**

The test body evaluates:

```csharp
bool submittedInSet =
    OrderState.Submitted == OrderState.Working         // always false
    || OrderState.Submitted == OrderState.Accepted     // always false
    || OrderState.Submitted == OrderState.Submitted    // ALWAYS TRUE (compile-time constant)
    || OrderState.Submitted == OrderState.ChangeSubmitted
    || OrderState.Submitted == OrderState.Initialized;
Assert.False(submittedInSet, ...);
```

**SECONDARY VIOLATION -- TR-T1-TEST2-ALWAYS-FAIL:**
`submittedInSet` is always `true` (same tautology as Test 1). `Assert.False(true, ...)` 
always fails. The `[Fact(Skip=...)]` attribute currently hides this permanent failure.
When any future engineer un-Skips this test to "enforce the spec contract," the test
will fail immediately -- not because the guard still includes Submitted, but because
the test body is logically inverted and hardcodes a permanent `true` on the wrong side
of the assertion.

The Skip attribute rationale states: "This test will pass only if Submitted is removed
from the state filter at CopyEngine.cs line 4609." This is incorrect -- the test will
STILL FAIL even if Submitted is removed from the guard, because `submittedInSet` is
still `true` due to the tautological third term.

**Severity:** Secondary. The Skip protects CI from seeing the failure now. However,
delivering a permanently-broken Skipped test misleads all future engineers and
constitutes a false contract. The skip rationale is also factually wrong.

**Summary of Test Coverage violations:**

| Finding | ID | Severity |
|---------|----|----------|
| Test 1 body is a tautology -- acceptedInSet always true -- zero regression detection capability | TR-T1-TEST1-TAUTOLOGY | P0 -- FAIL |
| Test 2 body always evaluates to Assert.False(true) -- permanently fails when un-Skipped -- skip rationale is factually wrong | TR-T1-TEST2-ALWAYS-FAIL | P1 -- FAIL |

**Test Coverage: FAIL**

---

### 7-Scan Checklist Presence (T1)

All 7 scans present (SCAN-01 through SCAN-07) with Command column, Expected column,
and Result column populated. Defense-in-depth contract complete for engineer attestation
and verifier cross-check.

**7-Scan Checklist Presence: PASS**

---

### File Routing (T1)

| File | Path | Correct? |
|------|------|----------|
| Test file | `src/PropTraderTools/CopyEngineTests.cs` | PASS -- Wave workspace |
| Verification target (READ ONLY) | `src/PropTraderTools/CopyEngine.cs` | PASS -- Wave workspace |

No Director workspace `.cs` paths.

**File Routing: PASS**

---

### T1 VERDICT: TICKET_REVIEW_FAIL

**Violations:**
- TR-T1-TEST1-TAUTOLOGY (P0): Test 1 body uses `OrderState.Accepted == OrderState.Accepted`
  (always true), making `Assert.True(acceptedInSet)` a permanent pass regardless of
  production guard content. Zero regression detection capability.
- TR-T1-TEST2-ALWAYS-FAIL (P1): Test 2 body uses `OrderState.Submitted == OrderState.Submitted`
  (always true), making `Assert.False(submittedInSet)` a permanent fail when un-Skipped.
  Skip attribute rationale states the test "will pass only if Submitted is removed from
  the guard" -- factually incorrect. The test will still fail even after removal.

---

## T2 -- DW-BACKLOG-TRIAGE-B167 (Doc Only)

### Traceability

| Ticket Req ID | Maps To | Status |
|---------------|---------|--------|
| DW-B159-TP4-RACE-T2-REQ-01 | Plan: Ticket 2 File (CREATE) | PASS |
| DW-B159-TP4-RACE-T2-REQ-02 | Plan: Backlog row B167-DEFER-02 OPEN (carried forward) | PASS |
| DW-B159-TP4-RACE-T2-REQ-03 | Plan: Backlog row B167-DEFER-04 CLOSED | PASS |
| DW-B159-TP4-RACE-T2-REQ-04 | Plan: Backlog row B167-DEFER-03 OPEN (UNBLOCKED) | PASS |
| DW-B159-TP4-RACE-T2-REQ-05 | Plan: Backlog row DW-B159-TP4-RACE CLOSED | PASS |
| DW-B159-TP4-RACE-T2-REQ-06 | Plan: All other items carried forward OPEN | PASS |

No phantom work. No missing plan coverage.

**Traceability: PASS**

---

### JS Pre-Check (T2)

All JS rules N/A -- no `.cs` edits in this ticket. Explicitly stated in Scope Lock:
"Zero `.cs` edits. Zero production code changes. Zero test file edits."

**JS Pre-Check: PASS (N/A)**

---

### CYC Pre-Check (T2)

N/A -- no `.cs` edits. No methods introduced.

**CYC Pre-Check: PASS (N/A)**

---

### NT8 Constraints (T2)

N/A -- no `.cs` edits. Doc-only ticket.

**NT8 Constraints: PASS (N/A)**

---

### Completeness (T2)

| Item | Check | Finding |
|------|-------|---------|
| Target file path specified | `docs/brain/DW-B159-TP4-RACE/06-deferred-backlog.md` | PASS |
| Action type specified | CREATE (new file) | PASS |
| Zero `.cs` edits explicitly stated | Yes -- Scope Lock | PASS |
| Full document content provided verbatim | Yes -- "Document Content (engineer must create verbatim)" block | PASS |
| Backlog item operations table present | Yes -- all 7 items with prior status, new status, action | PASS |

**Completeness: PASS**

---

### Test Coverage (T2)

No new C# methods introduced. No `[Fact]` tests required. Doc-only scope.

**Test Coverage: PASS (N/A)**

---

### Backlog Item Status Verification (T2)

Cross-checked against `docs/brain/DW-B159-KS/06-deferred-backlog.md` (prior block source,
read directly):

| Item ID | Prior Status (KS backlog) | Ticket Status | Correct? |
|---------|--------------------------|---------------|----------|
| B167-DEFER-01 | OPEN | OPEN (carried forward unchanged) | PASS |
| B167-DEFER-02 | OPEN (P1, B168 required) | OPEN (carried forward unchanged) -- OVERRIDE: must remain OPEN | PASS -- critical constraint honored |
| B167-DEFER-03 | OPEN | OPEN (UNBLOCKED -- B167-DEFER-04 prerequisite met) | PASS |
| B167-DEFER-04 | OPEN | CLOSED (Director confirmed SIM gate complete 2026-09-20) | PASS |
| DW-B159-TP4-RACE | NEW (this epic) | CLOSED (opened and closed in this block) | PASS |
| DW-B159-K4 | OPEN | OPEN (carried forward unchanged) | PASS |
| DW-B159-K3-UI | OPEN | OPEN (carried forward unchanged) | PASS |

**Specific gate checks from task brief:**
- B167-DEFER-02 OPEN: PASS -- marked OPEN with OVERRIDE notation; P1/B168 constraint honored
- B167-DEFER-04 CLOSED: PASS -- marked CLOSED with Director confirmation citation
- B167-DEFER-03 UNBLOCKED: PASS -- marked OPEN (UNBLOCKED) with prerequisite explanation

All backlog item statuses match prior block and plan Cycle 2 reconciliation table.

**Backlog Item Statuses: PASS**

---

### 7-Scan Checklist Presence (T2)

All 7 scans present (SCAN-01 through SCAN-07), all explicitly marked N/A with documented
reason: "no .cs file modified in this ticket." Defense-in-depth contract complete.
This resolves the plan-review V-02 violation that was caught and fixed before Phase 3.

**7-Scan Checklist Presence: PASS**

---

### File Routing (T2)

| File | Path | Correct? |
|------|------|----------|
| Backlog doc | `docs/brain/DW-B159-TP4-RACE/06-deferred-backlog.md` | PASS -- doc file, not .cs |

No `.cs` files touched.

**File Routing: PASS**

---

### T2 VERDICT: TICKET_REVIEW_PASS

All checks pass. B167-DEFER-02 correctly held OPEN. B167-DEFER-04 correctly closed.
B167-DEFER-03 correctly unblocked. 7-scan table present with N/A rationale. Zero `.cs`
edits confirmed throughout.

---

## Violation Summary

| ID | Ticket | Rule | Violation | Severity |
|----|--------|------|-----------|----------|
| TR-T1-TEST1-TAUTOLOGY | T1 | TEST COVERAGE | `FindFollowerBracketOrder_AcceptedState_IsReturned` uses `OrderState.Accepted == OrderState.Accepted` (always true). `acceptedInSet` is always `true`. `Assert.True(acceptedInSet)` never fails regardless of production guard content. Zero regression detection capability. Test does not test `FindFollowerBracketOrder` -- it tests an inline constant expression. | P0 -- FAIL |
| TR-T1-TEST2-ALWAYS-FAIL | T1 | TEST COVERAGE | `FindFollowerBracketOrder_SubmittedState_IsSkipped` uses `OrderState.Submitted == OrderState.Submitted` (always true). `submittedInSet` is always `true`. `Assert.False(submittedInSet)` will always fail when un-Skipped, regardless of the production guard. Skip rationale states "will pass only if Submitted is removed from guard" -- factually incorrect. | P1 -- FAIL |

**Engineer action required (do not spawn engineer until violations are resolved):**
Both violations require the architect to redesign the test bodies to exercise the actual
production guard via `FindFollowerBracketOrderTestable` (test seam at CopyEngine.cs line
4756, cited in plan Component List). Suggested approach (architect resolves -- not a
suggestion from reviewer, listed for context only): construct a test Order object with
the target `OrderState` and invoke the testable seam to verify the returned result.

---

## Overall

### TICKET_REVIEW_FAIL

**Fail reason:** T1 Test Coverage violations TR-T1-TEST1-TAUTOLOGY (P0) and
TR-T1-TEST2-ALWAYS-FAIL (P1). Both new [Fact] test method bodies use compile-time
constant identity comparisons that make the assertions either permanently pass (Test 1)
or permanently fail (Test 2), independent of the actual production guard in
`FindFollowerBracketOrder`. The tests provide zero regression protection.

**T2 status:** TICKET_REVIEW_PASS. No violations in T2. T2 may proceed as-is once T1 is
corrected and re-submitted.

**Action:** Return to ptt-architect (Phase 3) for T1 test body redesign. After
correction, re-submit only 04-tickets.md for Phase 3.5 re-review. This is review
cycle 1 of maximum 2. If cycle 2 also fails, escalate to Director.

---

*Ticket review produced by ptt-ticket-reviewer (Phase 3.5). Block DW-B159-TP4-RACE. Date 2026-10-01.*
*Verdict: TICKET_REVIEW_FAIL. Violations: 2 (TR-T1-TEST1-TAUTOLOGY P0, TR-T1-TEST2-ALWAYS-FAIL P1).*
*T2 independently: TICKET_REVIEW_PASS.*

---

## Cycle 2 Re-Review — DW-B159-TP4-RACE
<!-- cycle: 2 -->
<!-- produced-by: ptt-ticket-reviewer (Phase 3.5, Cycle 2) -->
<!-- date: 2026-10-01 -->
<!-- tickets-source: docs/brain/DW-B159-TP4-RACE/04-tickets.md (Correction Cycle 1) -->

### Source Evidence Read (Cycle 2)

| File | Lines Read | Confirmed |
|------|-----------|-----------|
| `src/PropTraderTools/CopyEngine.cs` | 4595-4618 | `FindFollowerBracketOrder` IEnumerable overload; `&& order.OrderState != OrderState.Accepted` at line 4608; `&& order.OrderState != OrderState.Submitted` at line 4609 |
| `src/PropTraderTools/CopyEngineTests.cs` | 9760-9840 | `DwB16702SlateReadyTests` class starts line 9766; `ResolveCopyEngineSource()` defined at line 9768; class closes `}` at line 9829; `B159KSuiteTests` starts line 9836 |

---

### TR-T1-TEST1-TAUTOLOGY (P0) — Resolution Verification

**Cycle-1 violation:** `OrderState.Accepted == OrderState.Accepted` was a compile-time constant `true`,
making `Assert.True(acceptedInSet)` a permanent pass with zero regression detection capability.

**Cycle-2 corrected body (primary path):**
```csharp
var src = System.IO.File.ReadAllText(srcFile);
Assert.Contains(
    "order.OrderState != OrderState.Accepted",
    src,
    System.StringComparison.Ordinal);
```

**Verification:**
- Target text `"order.OrderState != OrderState.Accepted"` is present at `CopyEngine.cs` line 4608 (confirmed by direct source read above).
- If a future engineer removes `OrderState.Accepted` from the guard, that line disappears from the source file → `Assert.Contains` raises `ContainsException` → test **FAILS** → regression **CAUGHT**.
- The assertion is NOT a tautology. It is anchored to actual production source text.
- Structural fallback (`typeof(CopyEngine).GetMethod("FindFollowerBracketOrderTestable", ...)`) is only reached when `srcFile` does not exist on disk. It verifies the test seam, not the guard text. Weaker but not incorrect; acceptable secondary path.

**TR-T1-TEST1-TAUTOLOGY: RESOLVED ✓**

---

### TR-T1-TEST2-ALWAYS-FAIL (P1) — Resolution Verification

**Cycle-1 violation:** `OrderState.Submitted == OrderState.Submitted` was a compile-time constant `true`,
making `Assert.False(submittedInSet)` permanently fail when un-Skipped. Skip rationale ("will pass only if
Submitted is removed from guard") was factually incorrect — the test would STILL fail after removal due to
the tautological body.

**Cycle-2 corrected body:**
```csharp
Assert.DoesNotContain(
    "order.OrderState != OrderState.Submitted",
    src,
    System.StringComparison.Ordinal);
```

**Verification:**
- Target text `"order.OrderState != OrderState.Submitted"` IS present at `CopyEngine.cs` line 4609 (confirmed).
- While Submitted remains in guard: `DoesNotContain` → **FAILS** (correct — signals spec-code conflict when un-Skipped).
- When Director removes Submitted from guard: that text disappears from source → `DoesNotContain` → **PASSES** (correct enforcement of spec contract).
- Skip is still active → CI is protected. Skip rationale now reads: "When Director removes Submitted from guard and un-Skips this test, Assert.DoesNotContain will pass." This is **factually correct**.
- No vacuous-pass, no tautology, no permanent-fail pattern remains.

**TR-T1-TEST2-ALWAYS-FAIL: RESOLVED ✓**

---

### T1 — Full Cycle-2 Checklist

#### Traceability (T1)

| Ticket Req ID | Maps To | Status |
|---------------|---------|--------|
| DW-B159-TP4-RACE-T1-REQ-01 | Plan: Verification Scope item 2 (line 4608) | PASS |
| DW-B159-TP4-RACE-T1-REQ-02 | Plan: Engineer Pre-Check Result + Component List (READ ONLY) | PASS |
| DW-B159-TP4-RACE-T1-REQ-03 | Plan: SPEC/CODE DISCREPANCY section + Test 2 (Skipped, corrected rationale) | PASS |
| DW-B159-TP4-RACE-T1-REQ-04 | Plan: Ticket 1 New Test Methods (source-text contract) | PASS |

No phantom work. No missing plan coverage.

**Traceability: PASS**

---

#### JS Pre-Check (T1)

| Rule | Check | Finding |
|------|-------|---------|
| JS-021 | No `lock()` in new test methods | PASS — file read + assert + reflection, no shared mutable state |
| JS-021 | No Monitor / Mutex / SemaphoreSlim | PASS — not present |
| JS-023 | No UI update off-thread | N/A — no UI code |
| JS-001 | No `throw` in hot path | PASS — test methods are not hot path; no throw present |
| JS-002 | No `return null` for optional value | PASS — void return |
| JS-003 | No empty-string sentinel | N/A |
| JS-008 | No mutable fields on struct | N/A |
| JS-009 | No Dictionary for shared collection | N/A |
| JS-042 | ASCII-only string literals incl. Skip attribute | PASS — `"order.OrderState != OrderState.Accepted"`, `"order.OrderState != OrderState.Submitted"`, Skip text all confirmed ASCII-only. No Unicode, no curly quotes, no emoji. |
| JS-036 | No `DateTime.Now` | PASS — not used |
| WS-GUARD | No whitespace mutation outside inserted lines | PASS — insert-only diff |
| PTT-PREFIX | No `CreateOrder` without "PTT-" prefix | N/A — no CreateOrder in tests |

**JS Pre-Check: PASS**

---

#### CYC Pre-Check (T1)

| Method | Decision Nodes | CYC | Limit | Verdict |
|--------|---------------|-----|-------|---------|
| `FindFollowerBracketOrder_AcceptedState_IsReturned` | 1 (if/else on File.Exists) | 2 | ≤ 8 | PASS |
| `FindFollowerBracketOrder_SubmittedState_IsSkipped` | 1 (if on File.Exists, no else) | 2 | ≤ 8 | PASS |

*Note: Ticket table states CYC=1 for both methods; actual CYC is 2 due to the `if (File.Exists)` branch. This is a minor imprecision in the ticket's self-annotation but is NOT a violation — both methods remain well within the ≤ 8 limit and the 1-2 target range cited in the test table notes.*

**CYC Pre-Check: PASS**

---

#### NT8 Constraints (T1)

| Constraint | Check | Finding |
|------------|-------|---------|
| No async/await in lifecycle method | N/A — test methods | PASS |
| No Account.All outside Loaded | N/A — no Account API | PASS |
| No sealed on TradeCopierWindow | N/A | PASS |
| No FontFamily override | N/A — no WPF | PASS |
| No hardcoded hex color | N/A | PASS |
| No CreateOrder without PTT- prefix | N/A | PASS |
| No DateTime.Now | PASS — not used | PASS |
| No NT8 runtime calls in test methods | PASS — pure BCL (`System.IO.File.ReadAllText`, `System.Reflection`) only; no NinjaTrader.Cbi cctor required | PASS |
| Source-text approach avoids NT8 runtime dependency | PASS — guard text verified at read time, not at NT8 runtime | PASS |

**NT8 Constraints: PASS**

---

#### Completeness (T1)

| Item | Check | Finding |
|------|-------|---------|
| Method signatures present (both) | Decorator, return type, parameters, access modifier, full Skip attribute | PASS |
| Insertion class specified | `DwB16702SlateReadyTests` (line 9766), confirmed as class containing `ResolveCopyEngineSource()` at line 9768 | PASS |
| Insertion point specified | Before `}` at line 9829, before `B159KSuiteTests` at line 9836 — confirmed by source read | PASS |
| `ResolveCopyEngineSource()` accessible | Yes — tests are in same class (`DwB16702SlateReadyTests`) as the helper | PASS |
| Full verbatim test source provided | Yes — "Full Test Source (engineer must copy verbatim)" block | PASS |
| Zero production code changes | Explicitly stated in Scope Lock and Pre-Check Result | PASS |
| Class name vs. plan discrepancy | Plan says "primary `CopyEngineTests` class"; ticket says `DwB16702SlateReadyTests`. Ticket's refinement is (a) technically required for helper access, (b) physically correct for the insertion point described in the plan, (c) within architect's authority to refine during ticket generation. Not a FAIL. | OBS |

**Completeness: PASS**

---

#### Test Coverage (T1)

| Test Name | Assertion | Non-Tautological? | Fails on Regression? | Notes |
|-----------|-----------|------------------|---------------------|-------|
| `FindFollowerBracketOrder_AcceptedState_IsReturned` | `Assert.Contains("order.OrderState != OrderState.Accepted", src)` | YES | YES — if Accepted removed from guard, source text absent → Contains throws | Cycle-1 tautology eliminated. Source-text contract. |
| `FindFollowerBracketOrder_SubmittedState_IsSkipped` | `Assert.DoesNotContain("order.OrderState != OrderState.Submitted", src)` | YES | YES (when un-Skipped) — if Submitted removed, DoesNotContain passes; while present, fails | Skip active. Skip rationale factually correct. |

**Test Coverage: PASS**

---

#### 7-Scan Checklist Presence (T1)

All 7 scans present (SCAN-01 through SCAN-07) with Command, Expected, and Result columns populated.
Defense-in-depth contract complete for engineer attestation (ticket-1-completion.md) and verifier
cross-check (ticket-1-verification.md).

**7-Scan Checklist Presence: PASS**

---

#### File Routing (T1)

| File | Path | Correct? |
|------|------|----------|
| Test file (edit target) | `src/PropTraderTools/CopyEngineTests.cs` | PASS — Wave workspace |
| Verification read target | `src/PropTraderTools/CopyEngine.cs` | PASS — Wave workspace, READ ONLY |

No Director workspace `.cs` paths.

**File Routing: PASS**

---

### T1 VERDICT (Cycle 2): TICKET_REVIEW_PASS

Both cycle-1 P0/P1 violations resolved. All 7 checklist categories pass. No new violations found.

---

### T2 — Cycle-2 Status

T2 passed cycle 1 with zero violations. No changes were made to T2 in correction cycle 1.
All T2 checks carry forward unchanged.

- Traceability: **PASS** (all 6 req IDs map to plan; B167-DEFER-02 OPEN, B167-DEFER-04 CLOSED, B167-DEFER-03 UNBLOCKED)
- JS Pre-Check: **PASS (N/A)** — no `.cs` edits
- CYC Pre-Check: **PASS (N/A)** — no `.cs` edits
- NT8 Constraints: **PASS (N/A)** — doc-only ticket
- Completeness: **PASS** — target file, action type, zero `.cs` edits, full verbatim doc content, backlog operations table all present
- Test Coverage: **PASS (N/A)** — no new C# methods; doc-only scope
- 7-Scan Checklist Presence: **PASS** — SCAN-01 through SCAN-07 all present, all N/A with documented reason
- File Routing: **PASS** — `docs/brain/DW-B159-TP4-RACE/06-deferred-backlog.md` (doc, not `.cs`)

**T2 VERDICT (Cycle 2): TICKET_REVIEW_PASS**

---

### Cycle-2 Violation Summary

| ID | Ticket | Severity | Status |
|----|--------|----------|--------|
| TR-T1-TEST1-TAUTOLOGY | T1 | P0 | **RESOLVED** in Correction Cycle 1 |
| TR-T1-TEST2-ALWAYS-FAIL | T1 | P1 | **RESOLVED** in Correction Cycle 1 |

No new violations found in cycle 2.

---

## Overall (Cycle 2)

### TICKET_REVIEW_PASS

**Both cycle-1 violations fully resolved:**
- TR-T1-TEST1-TAUTOLOGY (P0): Test 1 now uses `Assert.Contains("order.OrderState != OrderState.Accepted", src)` anchored to actual CopyEngine.cs production source text. Non-tautological. Regression-catching. Confirmed against line 4608.
- TR-T1-TEST2-ALWAYS-FAIL (P1): Test 2 now uses `Assert.DoesNotContain("order.OrderState != OrderState.Submitted", src)`. Skip remains active with factually correct rationale. Will pass when Director removes Submitted from guard and un-Skips.

**T1 and T2 both TICKET_REVIEW_PASS.**

**Pipeline may proceed to Phase 4a (engineer).**

---

*Ticket review cycle 2 produced by ptt-ticket-reviewer (Phase 3.5). Block DW-B159-TP4-RACE. Date 2026-10-01.*
*Verdict: TICKET_REVIEW_PASS. Violations resolved: 2 (TR-T1-TEST1-TAUTOLOGY P0, TR-T1-TEST2-ALWAYS-FAIL P1). No new violations.*
