# B53-LaneC Plan Review — Cancel Propagation
# Reviewer: ptt-plan-reviewer
# Date: 2026-08-10
# Result: REVIEW_PASS

---

## Overall Verdict: REVIEW_PASS

Zero P0/P1 rule violations found. Zero NT8 compiler violations. CYC constraints satisfied.
One annotation discrepancy noted (informational, not a blocker).

---

## 1. CYC Constraint Review

### `OnOrderUpdate` (CRITICAL — was CYC=8 before LaneC)

The plan extracts `DispatchAfterRuleMatch` to absorb mirror relay + cancel check + Gate B + DispatchCopy.

Post-extraction `OnOrderUpdate` decision points:
1. Gate 1: `!_isCopyEnabled` → (1)
2. Follower-fill guard compound → (2)
3. Gate 2: `foreach` rule match → (3)
4. `matchedRule == null` → (4)
5. Gate 2.5: `!rule.Enabled` → (5)
6. `DispatchAfterRuleMatch` call → straight call (no branch)

**Plan claims CYC=5. CONFIRMED CYC=5. PASS.**

---

### `DispatchAfterRuleMatch` — CYC annotation discrepancy (informational)

Plan claims CYC=3. Actual count:
1. `if ((CopyMode)_copyModeValue == CopyMode.Mirror)` → (1)
2. `if (IsLeaderEntryCancelled(order, rule))` → (2)
3. `if (IsWorkingBracket(order))` → (3)
4. `if (order.FromEntrySignal != null)` inside Gate B → (4)

**Actual CYC=4, not CYC=3 as annotated.** CYC=4 is still <= 8. **Not a violation.**
Informational only — engineer should update the comment from `CYC=3` to `CYC=4`.

---

### `IsLeaderEntryCancelled` — CYC=3

Decision points: (1) `OrderState.Cancelled` check, (2) `IsBracketLegStatic` check, (3) compound `name != "PTT-Copy" && account match`.
**CYC=3. PASS.**

---

### `FindFollowerWorkingEntry` — CYC=3

Decision points: (1) `foreach` loop, (2) name+state filter (two conditions joined, counts as 1 branch per iteration path), (3) instrument match.
**CYC=3. PASS.**

---

### `CancelFollowerEntryOrders` — CYC=4

Decision points: (1) `foreach` loop, (2) `acc == null` guard, (3) `found == null` guard, (4) `try/catch` block.
**CYC=4. PASS.**

---

## 2. IsBracketLegStatic vs IsBracketLeg (KEY REVIEW QUESTION)

Plan Section 3.2 code: `if (IsBracketLegStatic(order))` ✅
Plan AD-1: explicitly documents the static/instance distinction. ✅
Plan SCAN-07: engineer scan explicitly verifies no `IsBracketLeg` call inside a static method. ✅

**No violation. PASS.**

---

## 3. JS Rule Compliance

| Rule | Check | Finding | Result |
|------|-------|---------|--------|
| JS-001 | No `throw` in hot path | `CancelFollowerEntryOrders` uses `try/catch` + `StatusUpdate` log. No `throw` in any new method. | PASS |
| JS-002 | No propagated null | `FindFollowerWorkingEntry` returns `null`; null checked at call site in `CancelFollowerEntryOrders` via `if (found == null) continue`. Null does NOT propagate past `CancelFollowerEntryOrders`. | PASS |
| JS-021 | No `lock()` | No `lock` in any new method. `acc.Orders.ToList()` snapshot pattern used (existing NT8 pattern). | PASS |
| JS-023 | No off-thread UI update without Dispatcher | No UI updates in new methods. `StatusUpdate` delegate dispatches via existing infrastructure. | PASS |
| JS-033 | No `async void` | All new methods are synchronous. | PASS |

---

## 4. NT8 Compiler Rule Compliance

| Rule | Check | Finding | Result |
|------|-------|---------|--------|
| NT8-001 | No `{ get; init; }` | No new properties with `init`. | PASS |
| NT8-002 | No `abstract record` / `sealed record` | No records. | PASS |
| NT8-003 | No `volatile double` | No new fields. | PASS |
| NT8-005 | No `readonly struct` with `private set` | No new structs. | PASS |
| NT8-007 | `acc.Cancel` takes `Order[]` not `string` | Plan Section 3.4: `acc.Cancel(new Order[] { found })` — correct array form. | PASS |
| NT8-013 | No `DateTime.Now` | Not used in new code. | PASS |
| NT8-014 | Signal name must start with `"PTT-"` | No new `CreateOrder` calls in LaneC. | PASS |
| NT8-018 | No `lock()` | Not present. | PASS |
| NT8-019 | No `async void` | Not present. | PASS |
| NT8-031 | No `OrderState.PendingSubmit` | Plan uses `Working` and `Accepted` only. | PASS |

---

## 5. Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| Cancel propagation fires when leader entry reaches `OrderState.Cancelled` | YES | §3.2 `IsLeaderEntryCancelled`, §4 insertion point |
| NOT a bracket leg (use `IsBracketLegStatic`) | YES | §3.2, AD-1, SCAN-07 |
| `order.Name != "PTT-Copy"` guard | YES | §3.2 line 3 of predicate |
| `order.Account.Name == rule.MasterAccount.Name` guard (spec alias: LeaderAccount) | YES | §3.2; field name `MasterAccount` confirmed correct in CopyEngine.cs |
| `FindFollowerWorkingEntry` searches `Name == "PTT-Copy"`, Working or Accepted | YES | §3.3 |
| `FindFollowerWorkingEntry` returns null if not found | YES | §3.3, JS-002 entry |
| `acc.Orders.ToList()` snapshot for thread safety | YES | §3.3, AD-6 |
| `CancelFollowerEntryOrders` null-checks return of `FindFollowerWorkingEntry` at call site | YES | §3.4 `if (found == null) continue` |
| try/catch around `acc.Cancel` | YES | §3.4 |
| `StatusUpdate` log on cancel + on error | YES | §3.4 |
| `OnOrderUpdate` CYC <= 8 after adding cancel branch | YES | `DispatchAfterRuleMatch` extraction → CYC=5, §3.5, §3.6, AD-3 |
| Cancel fires before Gate B | YES | §4, §3.5 branch order |
| Cancel bypasses `IsDedup` | YES | §3.5 early `return`, AD-7 |
| Cancel fires in Mirror mode (after mirror relay) | YES | §3.5 branch (1) mirror first, AD-4 |
| `PttBuild.Tag` updated | YES | §3.1 |
| T_B53C_01: `IsLeaderEntryCancelled` returns true for cancelled non-bracket leader order | YES | §8 |
| T_B53C_02: `IsLeaderEntryCancelled` returns false for bracket order | YES | §8 |
| Tests use `[Fact]` (xUnit) | YES | §8 — `Assert.True` / `Assert.False` pattern |
| `acc.Cancel(new Order[] { found })` NT8-007 form | YES | §3.4, NT8-007 table entry |

**All spec requirements addressed. PASS.**

---

## 6. Findings Summary

### Violations: 0

No P0, P1, or P2 rule violations found.

### Informational Notes (non-blocking):

| # | Item | Location | Severity |
|---|------|----------|----------|
| INFO-1 | `DispatchAfterRuleMatch` CYC annotation says `CYC=3`; actual count is `CYC=4` (inner `if (order.FromEntrySignal != null)` is the 4th branch). CYC=4 is compliant. Engineer should update inline comment from `// CYC=3` to `// CYC=4`. | Plan §3.5, code comment | Informational |
| INFO-2 | Spec uses `rule.LeaderAccount.Name` (informal alias). Plan correctly uses `rule.MasterAccount.Name` which matches the actual `CopyRule` field confirmed in `CopyEngine.cs` line 184. No discrepancy in the plan — spec used an informal name. | Plan §3.2 vs spec ground truth | Informational |

---

## 7. Reviewer Sign-off

```
Reviewer:   ptt-plan-reviewer
Epic:       B53-LaneC (DW-B53-03)
Plan file:  docs/brain/B53-LaneC/02-architecture-plan.md
Violations: 0
Result:     REVIEW_PASS
```

Phase 3 (ticket generation) is UNLOCKED.
