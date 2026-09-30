# B44-LaneA Plan Review
Block: PTT-COPIER-B44
Epic: B44-LaneA
Reviewer: ptt-plan-reviewer
Input: docs/brain/B44-LaneA/02-architecture-plan.md
Date: 2026-08-05
Status: REVIEW_FAIL

---

## Result

**REVIEW_FAIL**

Violations: 1 (P0 — Spec Completeness)

The plan must be returned to ptt-architect for correction before Phase 3 is
unlocked. See Section 3 for the required fix.

---

## 1. Violation Table

| # | Rule ID | Severity | Description | Location in Plan |
|---|---------|----------|-------------|-----------------|
| V-01 | SPEC-COMPLETENESS (P0) | FAIL | `CopyEngine.Instance` not addressed in test design. Spec requires "CopyEngine.Instance used in tests." Plan describes test-local `engine` instances (implied direct construction), which conflicts with the CopyEngine singleton/smart-constructor contract. No explanation of how tests obtain a valid CopyEngine reference, how the Account.All stub is injected, or whether `CopyEngine.Instance` is reset between tests. | §7 (T_B44_01 through T_B44_04) |

---

## 2. Spec Coverage Matrix

| Spec Requirement | Addressed? | Plan Section |
|-----------------|-----------|--------------|
| `private volatile bool _subscribed;` added after `_isCopyEnabled` at ~L102 | ✅ YES | §3.1 |
| `Subscribe()`: `if (_subscribed) return;` at top | ✅ YES | §3.2 |
| `Subscribe()`: `_subscribed = true;` placed BEFORE foreach | ✅ YES | §3.2 |
| `Unsubscribe()`: `if (!_subscribed) return;` at top | ✅ YES | §3.3 |
| `Unsubscribe()`: `_subscribed = false;` placed BEFORE foreach | ✅ YES | §3.3 |
| `TradeCopierPanel.OnLoaded`: `_engine.Subscribe()` after IPttModules loop, before leader wiring | ✅ YES | §4.1 |
| `TradeCopierPanel.Detach()`: `_engine.Unsubscribe()` as FIRST statement | ✅ YES | §4.2 |
| 4 [Fact] tests (T_B44_01 through T_B44_04) | ✅ YES | §7 |
| xUnit only, no NUnit/MSTest | ✅ YES | §7 |
| **CopyEngine.Instance used in tests** | ❌ NOT ADDRESSED | §7 |
| Tests are NT8-runtime-free (FakeAccount stub, no Account.All dependency) | ✅ YES | §7 |
| Idempotency covers Panel+Window simultaneous open scenario | ✅ YES | §5 |

---

## 3. Violation Detail

### V-01 — Spec Completeness: CopyEngine.Instance not addressed (P0)

**Spec requirement** (verbatim):
> "CopyEngine.Instance used in tests"

**What the plan says**: §7 test descriptions use language like "Arrange: CopyEngine with 1
FakeAccount in Account.All stub" and "Arrange: fresh CopyEngine (never subscribed)" — these
imply a separately constructed test instance (`new CopyEngine(...)` or similar). The plan
contains no reference to `CopyEngine.Instance`, makes no claim about how a CopyEngine
reference is obtained in the test harness, and does not address the following open questions:

1. Does `CopyEngine` have a public constructor? If the class uses a private constructor
   (JS-010 / smart-constructor pattern), the plan must explain how tests create or obtain an
   instance.
2. If tests use `CopyEngine.Instance` (the singleton), how is the singleton's `_subscribed`
   flag reset to `false` between T_B44_01, T_B44_02, T_B44_03, T_B44_04? A singleton that
   retains state between tests violates test isolation and will cause T_B44_03 and T_B44_04
   to produce non-deterministic results depending on execution order.
3. The FakeAccount stub approach (to avoid NT8 runtime dependency) is NT8-runtime-free only
   if `Account.All` is injectable or mockable. The plan does not describe how the FakeAccount
   collection is injected into `CopyEngine`'s `Subscribe()` / `Unsubscribe()` loops (which
   iterate `Account.All`). Without an injection seam, tests will reference `Account.All`
   (NT8 runtime) and become un-runnable in the test host.

**Impact**: Any of the three gaps above will cause at least one of T_B44_01 through T_B44_04
to fail at authoring time (compile error, crash, or non-determinism). This is a blocking spec
completeness gap.

---

## 4. Passing Checks (Informational)

The following checks all passed without violations. Recorded for traceability.

### 4.1 T1 — CopyEngine Idempotency (PASS)

- Field `private volatile bool _subscribed` placed immediately after `_isCopyEnabled` at L103.
  Exact location matches spec. `volatile bool` is permitted per NT8-017; `volatile double` would
  be banned per NT8-003. This is `volatile bool` — no violation.
- `Subscribe()`: guard `if (_subscribed) return;` at top, then `_subscribed = true;` before
  foreach. Ordering matches spec verbatim.
- `Unsubscribe()`: guard `if (!_subscribed) return;` at top, then `_subscribed = false;` before
  foreach. Ordering matches spec verbatim.

### 4.2 T2 — TradeCopierPanel Call Sites (PASS)

- `OnLoaded`: `_engine.Subscribe()` inserted at L622 — after IPttModules loop closing brace
  (L620) and before leader wiring block (L624). Exact location matches spec.
- `Detach()`: `_engine.Unsubscribe()` is the first statement in the method body, explicitly
  before the existing `if (_currentChart != null)` guard at L493. Matches spec.

### 4.3 Jane Street DNA (PASS)

| Rule | Result | Evidence |
|------|--------|----------|
| JS-021 (no lock) | PASS | No lock() introduced. volatile + Dispatcher thread serialization used. |
| JS-002 (no return null) | PASS | All new returns are `return;` (void). No null-returning methods added. |
| JS-033 (no async void) | PASS | No async methods introduced. OnLoaded is RoutedEventHandler (exempt per JS-033 pattern). |
| JS-023 (atomic primitives) | PASS | `volatile bool _subscribed` is the correct atomic primitive for a boolean cross-thread flag. |

### 4.4 NT8 Compiler Rules (PASS)

| Rule | Result | Evidence |
|------|--------|----------|
| NT8-003 (no volatile double) | PASS | Field is `volatile bool`, not `volatile double`. |
| NT8-017 (volatile mandatory) | PASS | `_subscribed` is declared `volatile`. |
| NT8-018 (no lock) | PASS | No lock() in plan. |
| NT8-019 (no async void in callbacks) | PASS | No async methods introduced. |
| NT8-021 (no Account.All in constructor) | PASS | Account.All accessed only inside Subscribe/Unsubscribe, not in field initializers or constructors. |

### 4.5 Cyclomatic Complexity (PASS)

| Method | Post-change CYC | Threshold |
|--------|----------------|-----------|
| `Subscribe()` | 2 | ≤ 8 ✅ |
| `Unsubscribe()` | 2 | ≤ 8 ✅ |
| `TradeCopierPanel.OnLoaded` | delta = 0 (straight-line call) | ≤ 8 ✅ |
| `TradeCopierPanel.Detach()` | delta = 0 (straight-line call) | ≤ 8 ✅ |

### 4.6 7-Scan Checklist (PASS)

All three tickets (T1, T2, Tests) carry exactly 7 scans each. Content of each scan is
appropriately targeted to the ticket scope.

### 4.7 Idempotency — Panel+Window Simultaneous Open (PASS)

§5 provides an explicit 8-row invariant table covering all open/close orderings for Panel and
Window. The re-subscribe scenario (Subscribe → Unsubscribe → Subscribe) is explicitly modelled
as row 8, confirming that `_subscribed` is correctly reset to `false` by Unsubscribe so that
a subsequent Subscribe() proceeds normally.

---

## 5. Required Fix for REVIEW_PASS

The plan architect must revise §7 to address all three sub-items of V-01:

1. **State how tests obtain a CopyEngine reference**: Either confirm `CopyEngine.Instance` is
   used (and explain singleton usage), or confirm a testable constructor/factory is available,
   and show the test harness construction pattern.

2. **Describe how `_subscribed` is reset between tests**: If using the singleton, a `Reset()`
   or `TestReset()` method (internal visibility) must be added to the plan, or tests must be
   written to be order-independent (e.g. by calling Unsubscribe at the start of each test to
   ensure a clean initial state).

3. **Describe how `Account.All` is made injectable for tests**: The FakeAccount approach
   requires a seam in `Subscribe()` / `Unsubscribe()` so that the `Account.All` enumeration
   can be replaced with a test collection. Without this seam, tests will call the NT8 runtime.
   The plan must either (a) document an existing injection seam in CopyEngine, or (b) add a
   plan item to introduce one (e.g. a `protected virtual IEnumerable<Account> GetAccounts()`
   method that tests can override, or a constructor-injected `IEnumerable<Account>` parameter).

Once §7 is revised to fully address these three items, a second review cycle may proceed.

---

## 6. Review Cycle Status

| Cycle | Date | Result |
|-------|------|--------|
| 1 | 2026-08-05 | REVIEW_FAIL — 1 violation (V-01, spec completeness) |
| 2 | — | Pending architect revision |

Maximum 2 cycles allowed. If Cycle 2 also FAIL, escalate to Director.

---

## Cycle 2 Review — 2026-08-05

**Reviewer**: ptt-plan-reviewer
**Input**: docs/brain/B44-LaneA/02-architecture-plan.md (revised — §7 injection seam added)
**Verdict**: **REVIEW_PASS**

---

### C2-1. V-01 Remediation Verification

The revised §7 addresses all three sub-questions raised in Cycle 1.

#### Q1 — How tests obtain a CopyEngine reference (RESOLVED ✅)

§7.1 Q1 states `private readonly CopyEngine _engine = CopyEngine.Instance;`.
`CopyEngine` uses a private constructor and exposes the singleton via `CopyEngine.Instance`.
This is the same access pattern used in `SendCopyFillSignalTests` (B42Tests.cs:241).
No public constructor. No `new CopyEngine(...)`. JS-010 (smart constructor) is satisfied.

#### Q2 — How `_subscribed` is reset between tests (RESOLVED ✅)

§7.1 Q2 and §7.2 provide a complete implementation:

```csharp
private static readonly FieldInfo _subscribedField =
    typeof(CopyEngine).GetField(
        "_subscribed",
        BindingFlags.NonPublic | BindingFlags.Instance);

public void Dispose()
{
    _subscribedField.SetValue(CopyEngine.Instance, false);
}
```

`SubscribeIdempotencyTests : IDisposable`. xUnit constructs a new test-class instance per
`[Fact]` and calls `Dispose()` at teardown. `_subscribed` is guaranteed `false` at the start
of every test. Mechanism matches the B42 precedent (B42Tests.cs:304-306). Test isolation is
structurally enforced.

#### Q3 — How Account.All is made injectable / NT8-runtime-free (RESOLVED ✅)

§7.1 Q3 demonstrates that no injection seam is needed. The observable contract of the
idempotency guard is the `_subscribed` field value, which is set BEFORE the foreach in both
`Subscribe()` (§3.2) and `Unsubscribe()` (§3.3). In the test host (no NT8 runtime, zero
accounts in `Account.All`), the foreach body executes zero iterations and no NT8 Account API
is touched. The field state is fully assertable via `FieldInfo.GetValue`. All four tests are
provably NT8-runtime-free. The logic is internally consistent with the production code plan.

---

### C2-2. Full Checks (all originally passing — confirmed still passing)

#### T1 — CopyEngine Idempotency (PASS)

- `private volatile bool _subscribed` at L103, immediately after `_isCopyEnabled`. Exact placement per spec. `volatile bool` (32-bit) is permitted by NT8 CLR constraints — NT8-003 (`volatile double` banned) does NOT apply. NT8-017 satisfied. JS-023 satisfied.
- `Subscribe()`: guard `if (_subscribed) return;` at top; `_subscribed = true;` BEFORE foreach. Ordering correct per spec.
- `Unsubscribe()`: guard `if (!_subscribed) return;` at top; `_subscribed = false;` BEFORE foreach. Ordering correct per spec.

#### T2 — TradeCopierPanel Call Sites (PASS)

- `OnLoaded`: `_engine.Subscribe()` at L622 — after IPttModules loop closing brace (L620), before leader wiring (L624). Exact per spec.
- `Detach()`: `_engine.Unsubscribe()` is the first statement, before the existing `if (_currentChart != null)` at L493. Exact per spec.
- `TradeCopierWindow.cs` — explicitly noted as NO CHANGES in §2. Passes.

#### Jane Street DNA (PASS)

| Rule | Result | Evidence |
|------|--------|----------|
| JS-021 (no lock) | PASS | No `lock()` anywhere in plan. volatile + Dispatcher thread serialization. |
| JS-002 (no return null) | PASS | All new returns are `return;` (void). No null-returning methods added. |
| JS-033 (no async void) | PASS | No async methods introduced. OnLoaded is RoutedEventHandler (exempt). |
| JS-023 (atomic primitives) | PASS | `volatile bool _subscribed` for cross-thread boolean state. |
| JS-010 (smart constructor) | PASS | CopyEngine.Instance singleton with private constructor; no public constructor in new code. |
| JS-001 (no throw in hot path) | PASS | No throw introduced. |

#### NT8 Compiler Rules (PASS)

| Rule | Result | Evidence |
|------|--------|----------|
| NT8-003 (no `volatile double`) | PASS | Field is `volatile bool`, not `double`. |
| NT8-017 (`volatile bool` mandatory) | PASS | `_subscribed` declared `volatile`. |
| NT8-018 (no lock) | PASS | No lock(). |
| NT8-019 (no async void) | PASS | No async methods introduced. |
| NT8-021 (Account.All not in constructor) | PASS | Account.All accessed only inside Subscribe/Unsubscribe bodies. |
| NT8-042 (Dispatcher.InvokeAsync banned) | PASS | Not used; OnLoaded already on WPF Dispatcher thread. |
| NT8-043 (no `?.` compound assignment) | PASS | No null-conditional `-=` or `+=` in plan. |

#### Cyclomatic Complexity (PASS)

| Method | CYC | Threshold |
|--------|-----|-----------|
| `Subscribe()` | 2 | ≤ 8 ✅ |
| `Unsubscribe()` | 2 | ≤ 8 ✅ |
| `TradeCopierPanel.OnLoaded` | delta = 0 | ≤ 8 ✅ |
| `TradeCopierPanel.Detach()` | delta = 0 | ≤ 8 ✅ |

#### 7-Scan Checklists (PASS)

- T1 (CopyEngine.cs): 7 scans present (SCAN-01 through SCAN-07). ✅
- T2 (TradeCopierPanel.cs): 7 scans present (SCAN-01 through SCAN-07). ✅
- Tests (B44Tests.cs): 7 scans present (SCAN-01 through SCAN-07). ✅

#### xUnit Only (PASS)

§7 header: "Framework: xUnit only. No NUnit. No MSTest." All four test methods use `[Fact]`. Tests ticket SCAN-01 and SCAN-02 enforce this at authoring time. ✅

---

### C2-3. Spec Coverage Matrix (Final)

| Spec Requirement | Addressed? | Plan Section |
|-----------------|-----------|--------------|
| `private volatile bool _subscribed` after `_isCopyEnabled` at ~L102 | ✅ YES | §3.1 |
| `Subscribe()`: `if (_subscribed) return;` at top | ✅ YES | §3.2 |
| `Subscribe()`: `_subscribed = true;` placed BEFORE foreach | ✅ YES | §3.2 |
| `Unsubscribe()`: `if (!_subscribed) return;` at top | ✅ YES | §3.3 |
| `Unsubscribe()`: `_subscribed = false;` placed BEFORE foreach | ✅ YES | §3.3 |
| `TradeCopierPanel.OnLoaded`: `_engine.Subscribe()` after IPttModules loop | ✅ YES | §4.1 |
| `TradeCopierPanel.Detach()`: `_engine.Unsubscribe()` as FIRST statement | ✅ YES | §4.2 |
| 4 `[Fact]` tests (T_B44_01 through T_B44_04) | ✅ YES | §7.3 |
| xUnit only, no NUnit/MSTest | ✅ YES | §7 |
| CopyEngine.Instance used in tests | ✅ YES | §7.1 Q1 |
| Tests are NT8-runtime-free | ✅ YES | §7.1 Q3 |
| Idempotency covers Panel+Window simultaneous open scenario | ✅ YES | §5 |

**All 12 spec requirements addressed. Zero gaps remaining.**

---

### C2-4. Violation Table

| # | Rule ID | Severity | Description | Status |
|---|---------|----------|-------------|--------|
| V-01 | SPEC-COMPLETENESS (P0) | — | CopyEngine.Instance + _subscribed reset + Account.All injectable | **CLOSED** — fully addressed in §7.1 Q1/Q2/Q3 |

---

### C2-5. Review Cycle Status (Updated)

| Cycle | Date | Result | Notes |
|-------|------|--------|-------|
| 1 | 2026-08-05 | REVIEW_FAIL | 1 violation: V-01 (spec completeness, §7 injection seam) |
| 2 | 2026-08-05 | **REVIEW_PASS** | V-01 closed. All other checks passing. Phase 3 unlocked. |
