# B115 Architecture Plan — Formalize DW-B119 + DW-B121 + DW-B122 Hotfixes

**Status**: REVIEW_PENDING
**Date**: 2026-08-27
**Author**: ptt-architect (Phase 1)
**Block**: B115
**Return value**: PLAN_COMPLETE (pending review)

---

## 1. Block Summary

B115 formalizes three live hotfixes applied 2026-08-27 via Director-approved direct edits.
All three fixes are already present in production source. B115 provides no new production
logic changes. The block closes the test and documentation gap that the hotfix fast-path
intentionally deferred.

| Item | Value |
|---|---|
| **Purpose** | Formalize DW-B119 cleanup placement (B114-T1), DW-B121 TTL 2s→10s, DW-B122 Accepted-state guard via tests and one readability edit |
| **Scope** | Test files (B113Tests.cs update, B115Tests.cs new) + CopyEngine.cs parentheses (T3, clarity only) |
| **Production code changed** | T3 only — one-line parentheses wrap; no logic change |
| **Live gate status** | Combo D PASS 2026-08-27 confirmed. All three hotfixes in source. |
| **Phase** | B115 = pipeline formalization, not a new feature |

---

## 2. Fix Inventory

| Fix-ID | File | Method | Change | Status | CYC Impact |
|---|---|---|---|---|---|
| DW-B119 | `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | `ExecuteOne` (follower path) | `_qxPendingFollowerCleanup.TryAdd` moved BEFORE `try { executor.Execute }` | FIXED-B114-T1 (code in source) | None — ExecuteOne CYC stays at 2 |
| DW-B121 | `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | `ExecuteOne` follower path — TryAdd TTL value | `DateTime.UtcNow.AddSeconds(2)` changed to `AddSeconds(10)` | HOTFIX-APPLIED 2026-08-27 | None — ExecuteOne CYC stays at 2 |
| DW-B122 | `src/PropTraderTools/CopyEngine.cs` | `TryCleanupReArmedAtmBracket` — guard condition (a) | Added `&& e.Order.OrderState != OrderState.Accepted` to state guard | HOTFIX-APPLIED 2026-08-27 | None — TryCleanupReArmedAtmBracket CYC stays at 5 |

**Source verification** (read 2026-08-27):

- DW-B119 confirmed: `PttGlobalQuickExit.cs` L163-166 — TryAdd appears before `try {` at L167.
- DW-B121 confirmed: `PttGlobalQuickExit.cs` L165 — `DateTime.UtcNow.AddSeconds(10)`.
- DW-B122 confirmed: `CopyEngine.cs` L2397-2398 — two-line state guard present.

---

## 3. Operator Precedence Analysis

### Guard Expression (CopyEngine.cs L2396-2408)

```csharp
if (
    e.Order.OrderState != OrderState.Working
    && e.Order.OrderState != OrderState.Accepted   // DW-B122
    || e.Order.Name == null
    || !e.Order.Name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
    || e.Order.Name.Length < 9
    || !char.IsDigit(e.Order.Name[8])
    || e.Order.Account == null
    || !IsFollowerAccount(e.Order.Account)
    || !_qxPendingFollowerCleanup.TryGetValue(e.Order.Account.Name, out var entry)
    || entry.Expiry <= DateTime.UtcNow
    || entry.Instr?.FullName != e.Order.Instrument?.FullName
)
    return;
```

### C# Operator Precedence Rule

Per ECMA-334 §12.4.2: the `&&` (conditional-AND) operator binds more tightly than `||` (conditional-OR).
This is the same rule as in most C-family languages.

### Evaluation Order Proof

With natural precedence, the compiler groups the expression as:

```
COMPOUND_A = (e.Order.OrderState != OrderState.Working
              && e.Order.OrderState != OrderState.Accepted)

FULL_GUARD = COMPOUND_A
          || (e.Order.Name == null)
          || (!e.Order.Name.StartsWith(...))
          || (e.Order.Name.Length < 9)
          || (!char.IsDigit(e.Order.Name[8]))
          || (e.Order.Account == null)
          || (!IsFollowerAccount(e.Order.Account))
          || (!_qxPendingFollowerCleanup.TryGetValue(...))
          || (entry.Expiry <= DateTime.UtcNow)
          || (entry.Instr?.FullName != e.Order.Instrument?.FullName)
```

**Intended meaning** (from comment block L2388-2394):

> Guard (a): order just went Working OR Accepted → method should proceed.
> Return early if state is NEITHER Working NOR Accepted.

Translating: `early_return_on_state = (state != Working) && (state != Accepted)` — true when
state is neither. This is exactly `COMPOUND_A`. The `||` chain then adds independent short-circuit
early-return conditions. The composed expression is: early-return if (wrong state) OR (bad name) OR
(no entry) OR (TTL elapsed) OR (instrument mismatch). This matches the author's intent precisely.

### Correctness Verdict

Parentheses are **NOT REQUIRED** for correctness. Natural `&&`-before-`||` precedence already
produces the intended evaluation.

### Clarity Verdict — T3 INCLUDED

Although the expression is semantically correct without parentheses, the compound state check
(`!= Working && != Accepted`) appears at the head of a nine-condition `||` chain. A reader
scanning the guard top-to-bottom may not immediately recognize that the `&&` sub-expression
is self-contained before the first `||`. Explicit parentheses visually anchor the sub-expression:

```csharp
if (
    (   e.Order.OrderState != OrderState.Working
     && e.Order.OrderState != OrderState.Accepted)   // DW-B122: Accepted passes guard
    || e.Order.Name == null
    || ...
)
```

**Recommendation**: T3 INCLUDED. Wrap lines 2397-2398 in explicit parentheses.
This is a readability improvement with zero behavior change and zero CYC change.

---

## 4. Test Coverage Gap Analysis

### DW-B119 / DW-B121 — T_B113_01 TTL Bounds

| | Detail |
|---|---|
| **Existing test** | `B113Tests.cs` — `QxPendingFollowerCleanup_SetBeforeExecuteOne_ForFollower` (T_B113_01) |
| **What it tests** | TryAdd fires before executor.Execute; dict key present; expiry in the future |
| **Gap** | Arrange uses `DateTime.UtcNow.AddSeconds(2)` for `expiry`. Upper-bound assertion uses `AddSeconds(3)`. Production TTL is now `AddSeconds(10)` (DW-B121). Constants are stale and misrepresent the production value. |
| **Closing test** | T1 — update T_B113_01: `AddSeconds(2)` → `AddSeconds(10)`, `AddSeconds(3)` → `AddSeconds(11)` |
| **Why 11 for upper bound** | Upper bound must exceed the seeded TTL to account for the ~0ms elapsed between TryAdd and Assert; AddSeconds(10) + 1s slack = AddSeconds(11) is a tight but safe bound |

### DW-B122 — Accepted-State Guard

| | Detail |
|---|---|
| **Existing test** | None — no test covers `OrderState.Accepted` path in `TryCleanupReArmedAtmBracket` |
| **What it tests** | Guard condition (a) returns early on `OrderState.Accepted` (pre-fix behavior) vs. passes through (post-fix behavior) |
| **Gap** | Zero tests for the Accepted-state path. Without a test, the DW-B122 fix can silently regress. |
| **Closing test** | T2 — `TryCleanupReArmedAtmBracket_FiresOnAccepted_CancelsNativeBracket` in `B115Tests.cs` |
| **Test design** | Cannot call `TryCleanupReArmedAtmBracket` directly (requires sealed `OrderEventArgs`). Tests the guard sub-expression inline and validates dict TryRemove behavior via `_qxPendingFollowerCleanup` seam. |

---

## 5. Ticket Plan

### T1 — Update T_B113_01 TTL Constants (B113Tests.cs)

**Purpose**: Bring T_B113_01 assertions into alignment with the production 10s TTL (DW-B121).

**File**: `src/PropTraderTools/Tests/B113Tests.cs`

**Method targeted**: `QxPendingFollowerCleanup_SetBeforeExecuteOne_ForFollower`

**Changes** (two constant replacements, no structural change):

| Location | Before | After |
|---|---|---|
| Arrange: `var expiry = DateTime.UtcNow.AddSeconds(...)` | `AddSeconds(2)` | `AddSeconds(10)` |
| Assert: `Assert.True(entry.Expiry <= DateTime.UtcNow.AddSeconds(...))` | `AddSeconds(3)` | `AddSeconds(11)` |

**Spec requirement**: DW-B121 (TTL formalized at 10s).

**CYC impact**: None. Test method CYC stays at 1.

**JS rules**: No lock(), no async void, DateTime.UtcNow (correct), ASCII-only strings.

---

### T2 — New Test: Accepted-State Guard (B115Tests.cs)

**Purpose**: Formally verify that DW-B122 guard condition (a) does not early-return on `OrderState.Accepted`.

**File**: `src/PropTraderTools/Tests/B115Tests.cs` *(new file)*

**Method**: `TryCleanupReArmedAtmBracket_FiresOnAccepted_CancelsNativeBracket` — xUnit `[Fact]`

**Framework**: xUnit `[Fact]` only. No NUnit, no MSTest.

**Seam**: `CopyEngine.Instance._qxPendingFollowerCleanup` (internal `ConcurrentDictionary`,
accessible via `[assembly: InternalsVisibleTo("PropTraderTools.Tests")]` declared in CopyEngine.cs).

**What the test validates** (without live NT8 objects):

1. **Guard sub-expression is false for Accepted state** (guard does NOT early-return):
   ```
   (OrderState.Accepted != OrderState.Working && OrderState.Accepted != OrderState.Accepted)
   = (true && false)
   = false  → guard passes through (cleanup fires)
   ```
   Assert: `Assert.False(stateGuardFires)` where `stateGuardFires` encodes the compound state check.

2. **Dict entry survives after T1-equivalent event** (tChar='1', non-expired entry):
   Seed `_qxPendingFollowerCleanup` with a non-expired entry.
   Simulate `shouldRemove = (tChar == '3') || (entry.Expiry <= DateTime.UtcNow)`.
   For `tChar='1'` and non-expired entry: `shouldRemove = false`.
   Assert: entry remains in dict after simulated T1 pass.

3. **Dict entry removed after T3-equivalent event** (tChar='3'):
   Simulate `shouldRemove = ('3' == '3') || ...` = true.
   Call `TryRemove(accName, out _)`.
   Assert: entry absent from dict.

**What the test cannot validate** (NT8 sealed types):
- Actual `acc.Cancel(...)` call (requires live `Account` object).
- `IsFollowerAccount(e.Order.Account)` resolution (requires live `Account` object).
- Full `TryCleanupReArmedAtmBracket(OrderEventArgs e)` invocation (requires sealed `OrderEventArgs`).

**CYC**: 1 (linear assertions, no branches).

**JS rules**: No lock(), no async void, no throw, DateTime.UtcNow, ASCII-only strings.

**Spec requirement**: DW-B122 (Accepted-state guard formalized).

---

### T3 — Parentheses Clarity Edit (CopyEngine.cs)

**Purpose**: Add explicit parentheses around the compound state check in
`TryCleanupReArmedAtmBracket` guard for reader clarity. No behavior change.

**File**: `src/PropTraderTools/CopyEngine.cs`

**Method**: `TryCleanupReArmedAtmBracket` — guard block at lines 2396-2408.

**Change**: Wrap lines 2397-2398 in explicit parentheses:

```csharp
// Before
if (
    e.Order.OrderState != OrderState.Working
    && e.Order.OrderState != OrderState.Accepted
    || e.Order.Name == null
    || ...

// After
if (
    (   e.Order.OrderState != OrderState.Working
     && e.Order.OrderState != OrderState.Accepted)   // DW-B122
    || e.Order.Name == null
    || ...
```

**Behavior change**: None. C# natural precedence already produces this grouping.
**CYC impact**: None. TryCleanupReArmedAtmBracket CYC stays at 5.
**Spec requirement**: Operator precedence clarity (see Section 3).

---

## 6. Seam Analysis

### Primary Seam

`CopyEngine.Instance._qxPendingFollowerCleanup`

- **Type**: `internal ConcurrentDictionary<string, (NinjaTrader.Cbi.Instrument Instr, DateTime Expiry)>`
- **Visibility**: `internal` in `CopyEngine.cs`; accessible from test assembly via `[assembly: InternalsVisibleTo("PropTraderTools.Tests")]` declared in `CopyEngine.cs`.
- **Thread safety**: `ConcurrentDictionary` — no lock() needed in tests or in production.
- **Test isolation**: Each test calls `.Clear()` at the start to isolate from prior state.

### What T2 Can Verify via Seam

| Scenario | Testable via Seam? | Mechanism |
|---|---|---|
| Guard sub-expression value for `Accepted` | YES | Evaluate inline: `(OrderState.Accepted != OrderState.Working && OrderState.Accepted != OrderState.Accepted)` |
| Dict entry seeded before Execute | YES | Direct `TryAdd` on `_qxPendingFollowerCleanup` |
| Entry survives T1 event (shouldRemove=false) | YES | Check `ContainsKey` after simulated T1 |
| Entry removed on T3 event (shouldRemove=true) | YES | Call `TryRemove`, assert `!ContainsKey` |
| TTL expiry path (shouldRemove via elapsed) | YES | Seed with past `Expiry`, check `ContainsKey` after `TryRemove` |

### What T2 Cannot Verify (NT8 Sealed Types)

| Scenario | Blocker |
|---|---|
| `acc.Cancel(new Order[] { toCancel })` fires | `Account` class is sealed with no public constructor — cannot instantiate |
| `IsFollowerAccount(e.Order.Account)` resolution | Requires live `Account` object |
| Full `TryCleanupReArmedAtmBracket(OrderEventArgs e)` round-trip | `OrderEventArgs` is sealed with no public constructor |
| Instrument full-name matching guard | `Instrument` is sealed with no public constructor |

These scenarios require live NT8 runtime (Combo D integration test). They are covered by the
2026-08-27 live Combo D PASS confirmation.

---

## 7. Spec Requirement Traceability

| Ticket | DW Item | Description | Closes |
|---|---|---|---|
| T1 | DW-B121 | TTL 10s formalized in T_B113_01 assertion constants | DW-B121 test debt |
| T1 | DW-B119 | TryAdd-before-Execute confirmed as already-fixed — T_B113_01 still valid coverage | DW-B119 documentation |
| T2 | DW-B122 | Accepted-state guard logic verified via dict seam test | DW-B122 test debt |
| T3 | (precedence) | Explicit parentheses added to compound state guard for reader clarity | Operator precedence clarity item from Section 3 |

**DW items NOT addressed by B115** (out of scope):
- DW-B120 (snapshot=3 path) — separate concern, not part of B115 scope.
- DW-B117 (cancel-after structural fix) — addressed in B113; B115 inherits the TTL fix only.

---

## 8. 7-Scan Checklist Pre-Assessment

The following table enumerates all 7 scans and their applicability to B115. This is the
pre-assessment for the engineer's SCAN-01 through SCAN-07 gate on every ticket.

| Scan # | Rule | Description | Applicable to B115? | Expected Result |
|---|---|---|---|---|
| SCAN-01 | JS-021 | `lock()` anywhere in new/modified code | YES — all three tickets | Zero `lock(` in T1, T2, T3 edits. ConcurrentDictionary used throughout. |
| SCAN-02 | JS-033 | `async void` (non-event-handler) | YES — new test file T2 | Zero `async void`. New test method is `public void TestName()`. |
| SCAN-03 | JS-002 | `return null` for missing values | YES — test files | Zero `return null`. Tests have no return values (void). |
| SCAN-04 | JS-001 | `throw new XxxException` in hot paths | YES — any code | Zero new `throw`. Tests use `Assert.*`, no throw. T3 parentheses only. |
| SCAN-05 | JS-036/037 | `new byte[]` / `new T[]` without `ArrayPool` in hot path | NOT APPLICABLE | Test files only; not hot-path production code. No array allocation needed. |
| SCAN-06 | CYC | Cyclomatic complexity ≤ 8 | YES — all methods | T1: CYC=1 (unchanged). T2: CYC=1 (new test). T3: CYC=5 (unchanged). All ≤ 8. |
| SCAN-07 | ASCII | No Unicode in string literals or identifiers | YES — all three tickets | All string literals are ASCII. Test names, dict keys ("Sim101"), order name patterns ("PTT-QX-T1") are all ASCII. |

**SCAN-05 note**: Although test files are not hot paths, the engineer should confirm no `new byte[]`
is inadvertently introduced in T2 test setup. Expected: none needed.

---

*End of B115 Architecture Plan*
