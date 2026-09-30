# B120 Plan Review — DW-B129 Leader Fallback Flatten

**Reviewer**: ptt-plan-reviewer  
**Block**: B120  
**Defect**: DW-B129 (P0)  
**Plan**: `docs/brain/B120/02-architecture-plan.md`  
**Source verified**: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`  
**Rules verified**: `docs/standards/jane-street/RULES_CATALOG.md` (JS-001, JS-002, JS-021, JS-033) + role DNA (JS-066 CYC ≤ 8)  
**Date**: 2026-08-28  

---

## Violations Found

None.

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| DW-B129: leader left open after B118 PTT-BE-* cancel | YES | Sections A, B |
| Root cause: empty-book after B118 wait → ExecuteOne no-op | YES | Section B (steps 1-5) |
| Fix: NeedsLeaderFallbackFlatten helper (internal static bool) | YES | Section C1 |
| Fix: fallback block with acc.Flatten + continue after SnapshotTargetOrders | YES | Section C2 |
| Fix does not affect follower path | YES | Sections D, H |
| CYC budget maintained ≤ 8 for all methods | YES | Section C3 |
| Execute() extraction of follower block into ExecuteFollowers() | YES | Section C3 |
| NT8 API Account.Flatten(Instrument) confirmed | YES | Section E |
| Test coverage: 3 xUnit [Fact] tests | YES | Section F |
| ASCII-only in all new string literals | YES | Sections C1, C2, G |
| No lock() in any new code | YES | Section G SCAN-01 |
| No async void in any new code | YES | Section G SCAN-02 |

---

## Per-Check Results

### R1 — Defect accurately described (log evidence match)

**PASS**

Plan Section A and Section B describe the failure chain precisely:
- BE-fire replaces ATM bracket with PTT-BE-* orders.
- B118 `CancelPttBeOrders` cancels 6 PTT-BE-* orders; `WaitForPttBeCancelled` awaits terminal state.
- `SnapshotTargetOrders` returns `count=0` — clean order book is not a protected state.
- `ExecuteOne` called with `targets=[]`, `leaderStop=0` — resolves stop=0, submits no PTT-QX order.
- Leader left with 7 open contracts and zero bracket protection.

Log evidence in plan (`count=6`, `count=0`, `stop resolved: 0`, `snapshot: 0 cancellable orders`, `NO QX ORDER`) maps exactly to source lines 49-54 and 90 of `PttGlobalQuickExit.cs`.

---

### R2 — Fix architecture minimal and correct

**PASS**

Plan Section C2 proposes:
1. `NeedsLeaderFallbackFlatten(int, int, int): bool` — pure static boolean helper. No side effects.
2. A single `if` block inserted after line 52 (`SnapshotTargetOrders`) and before line 90 (`ExecuteOne`): log + `acc.Flatten(pos.Instrument)` + `continue`.
3. Extraction of lines 92-167 (follower block) into `private void ExecuteFollowers(...)` for CYC budget.

Changes are limited to `PttGlobalQuickExit.cs` only. No other files modified. The `continue` correctly skips `ExecuteOne` for the current `foreach pos` iteration. The fallback fires only when all three predicates hold (B118 active + empty book + open position). The normal path (no BE cancellation, or snapshot has targets) is unaffected.

---

### R3 — CYC analysis correct

**PASS**

Independent recount of `Execute()` post-fix (after follower block extraction + new `if` added):

| # | Decision point | Source |
|---|---------------|--------|
| 1 | `foreach (Account acc in Account.All)` | line 40 |
| 2 | `if (engine != null && engine.IsFollowerAccount(acc))` | line 42 |
| 3 | `foreach (Position pos in acc.Positions)` | line 44 |
| 4 | `if (pos == null \|\| pos.Quantity == 0)` | line 46 |
| 5 | `for (int _i = 0; ...)` [DW-B115-DIAG leader block] | line 78 |
| 6 | `if (NeedsLeaderFallbackFlatten(...))` [new B120] | new |

CYC = 6 + 1 = **7** — matches plan table. ≤ 8. ✅

`ExecuteFollowers()` post-extraction:

| # | Decision point |
|---|---------------|
| 1 | `if (rule != null)` |
| 2 | `foreach (var follower ...)` |
| 3 | `if (follower == null)` |
| 4 | `foreach (NinjaTrader.Cbi.Position _p ...)` [DIAG] |
| 5 | `if (_p != null && _p.Instrument != null && ...)` [DIAG] |
| 6 | `for (int _i = 0; ...)` [DIAG follower targets] |

CYC = 6 + 1 = **7** — matches plan. ≤ 8. ✅

`NeedsLeaderFallbackFlatten`: single compound `&&` return expression. CYC = 2 (plan) or 3 (strict short-circuit counting). Both ≤ 8. ✅

---

### R4 — Follower path isolation

**PASS**

Plan Section D confirms:
- Leader uses `_beCancelCount` (source line 49, local variable in leader loop).
- Followers use `_fBeCancelCount` (source line 99, per-follower local variable).
- `NeedsLeaderFallbackFlatten` is called only with `_beCancelCount` on the leader path.
- Follower accounts processed exclusively inside `ExecuteFollowers()`; `ResolveFollowerTargets` handles the follower empty-snapshot case independently.
- No `acc.Flatten` is called on any follower account.

This is verified against source: lines 92-167 (the entire follower dispatch block) are the extraction boundary. No cross-contamination.

---

### R5 — NT8 API correctness

**PASS**

Plan Section E confirms `Account.Flatten(Instrument instrument)` from `NT8_FULL_REFERENCE.md`:
- Closes all open positions for the specified instrument on the account.
- No `CreateOrder`/`Submit()` pattern required — `Flatten` is a direct NT8 method.
- Scoped to `pos.Instrument` (not all instruments on the account).
- Called from UI thread, consistent with all other `acc.*` calls in `Execute()`.

`Account.Flatten` is an AddOn-valid API (not `StrategyBase`-only like `AtmStrategyCreate`). The NT8 reference confirms this. ✅

---

### R6 — JS-021 compliance (no lock())

**PASS**

New code in plan:
- `NeedsLeaderFallbackFlatten`: no `lock`.
- Fallback `if` block: no `lock`.
- `ExecuteFollowers`: extraction of existing code only — current source has no `lock` in lines 92-167.

Current source has no `lock(` anywhere. New code introduces none. Rule JS-021 satisfied.

---

### R7 — JS-033 compliance (no async void)

**PASS**

All proposed new and modified methods are synchronous:
- `NeedsLeaderFallbackFlatten`: `internal static bool` — no `async`.
- `ExecuteFollowers`: `private void` — no `async`.
- Fallback `if` block: inline synchronous code.

No `async` keyword in any proposed changes. Rule JS-033 satisfied.

---

### R8 — JS-066 compliance (CYC ≤ 8 all methods)

**PASS**

| Method | CYC (plan) | CYC (independent recount) | ≤ 8? |
|--------|-----------|--------------------------|------|
| `Execute()` after fix | 7 | 7 | ✅ |
| `ExecuteFollowers()` | 7 | 7 | ✅ |
| `NeedsLeaderFallbackFlatten()` | 2 | 2–3 | ✅ |

All existing methods (`SnapshotTargetOrders` CYC=5, `ScaleLeaderTargets` CYC=3, `ResolveFollowerTargets` CYC=4, `CancelPttBeOrders` CYC=7, `WaitForPttBeCancelled` CYC=7, `ResolveQuickTicks` CYC=2, `ExecuteOne` CYC=2, `IsPttBeOrder` CYC=1, `IsNonTerminalPttBeState` CYC=1) are unchanged and all ≤ 8.

---

### R9 — Test coverage adequate

**PASS**

Three xUnit `[Fact]` tests in `src/PropTraderTools/Tests/B120Tests.cs`:

| Test | Inputs | Expected | Short-circuit point |
|------|--------|----------|---------------------|
| F1 | beCancelCount=1, snapshotCount=0, posQty=7 | true | all three predicates pass |
| F2 | beCancelCount=0, snapshotCount=0, posQty=7 | false | first predicate fails (no BE activity) |
| F3 | beCancelCount=1, snapshotCount=3, posQty=7 | false | second predicate fails (targets present) |

True path covered. Both specified false paths covered. Each test targets a distinct predicate. Note: a fourth test (`posQty=0`) would cover the third predicate's false path; this is absent but was not required by the spec. Required coverage (1 true + 2 false) is satisfied.

---

### R10 — ASCII-only

**PASS**

Proposed new string literals (Section C2):
- `"[PTT-QX-FLATTEN] leader fallback flatten: "` — all ASCII printable characters.
- `" qty="` — ASCII.
- `NeedsLeaderFallbackFlatten` identifier — ASCII-only.
- `ExecuteFollowers` identifier — ASCII-only.

No Unicode characters, no emoji, no curly quotes in any proposed new code.

---

## Summary

All 10 checks pass. No violations found against:
- JS-001 (no throw in new code) ✅
- JS-002 (no null return; new helper returns bool) ✅
- JS-021 (no lock) ✅
- JS-033 (no async void) ✅
- JS-066 / CYC ≤ 8 (Execute=7, ExecuteFollowers=7, NeedsLeaderFallbackFlatten=2) ✅
- NT8 API (Account.Flatten confirmed AddOn-valid) ✅
- ASCII-only (all new string literals are ASCII) ✅
- Spec completeness (all DW-B129 requirements addressed) ✅
- Follower path isolation (no cross-contamination) ✅
- Minimal fix (one guard, one flatten, one continue; one extraction for budget) ✅

---

## Gate Result

**REVIEW_PASS**

Phase 3 (ptt-architect → `docs/brain/B120/04-tickets.md`) is unblocked.
