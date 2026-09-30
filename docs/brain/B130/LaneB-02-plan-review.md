# B130 LaneB Plan Review
# DW-B136 Gap B: Order-ID Scoped Cancel for Simultaneous Entries

**Reviewer**: ptt-plan-reviewer
**Block**: B130 LaneB
**Plan Reviewed**: `docs/brain/B130/LaneB-02-architecture-plan.md`
**Date**: 2026-09-01
**Overall Verdict**: **REVIEW_FAIL**

---

## Violations Found: 1

| # | Rule | Severity | Description | Location in Plan |
|---|------|----------|-------------|-----------------|
| V-01 | (Functional Correctness) | P0 | Execution-order bug: EvictDedup fires BEFORE TryCancelFollowerEntries in OnOrderUpdate (L1277 vs L1361). Plan adds `_followerCopyMap.TryRemove` unconditionally to EvictDedup final design (Section 7 revised EvictDedup, Section 4d) AND also to `CancelScopedFollowerEntries` (Section 5b belt-and-suspenders). Because EvictDedup removes `"id2"` from the map first, `CancelScopedFollowerEntries("id2")` sees a TryGetValue miss and returns immediately — the follower copy of order2 is **never cancelled**. The plan's own Section 7 "RESOLUTION" claims Option B fixes this by putting TryRemove only inside `CancelScopedFollowerEntries`, but the final EvictDedup code block (page bottom of Section 7 + Section 4d) contradicts this by also adding the unconditional TryRemove to EvictDedup. The two designs are mutually exclusive. The resolution is internally inconsistent and the net effect of the plan's final state is a broken cancel path. | Sections 4d, 5b (revised), 7 |

---

## Per-Criterion Analysis

### Criterion 1 — Problem Coverage: Does the plan fix DW-B136 Gap B?

**FAIL**

The core design (Option B: `_followerCopyMap` keyed by leader orderId) is architecturally correct.
`RecordFollowerCopy` + `CancelScopedFollowerEntries` correctly scopes the cancel to a specific leader order.

However, the plan contains a **critical execution-order defect** that renders the cancel path inoperative:

- `EvictDedup` is called unconditionally at [`OnOrderUpdate` L1277](src/PropTraderTools/CopyEngine.cs:1277).
- `TryCancelFollowerEntries` is called at [`OnOrderUpdate` L1361](src/PropTraderTools/CopyEngine.cs:1361) — 84 lines later, after multiple pre-gate checks.
- The plan's **final revised EvictDedup** (end of Section 7, also Section 4d) adds `_followerCopyMap.TryRemove(orderId, out _)` unconditionally for all terminal states including Cancelled.
- Therefore, when leader cancels order2:
  1. `EvictDedup("id2", Cancelled)` fires — removes `"id2"` from `_followerCopyMap`
  2. `CancelScopedFollowerEntries("id2")` fires — `TryGetValue("id2")` returns false → method returns immediately, no cancels issued
  3. Follower copy of order2 is **never cancelled** — DW-B136 Gap B is NOT fixed.

The plan's Section 7 self-identifies this problem and proposes Option B (TryRemove only inside `CancelScopedFollowerEntries`). But the final code blocks in Section 7 and Section 4d **both** add the TryRemove to EvictDedup, defeating Option B. The two positions are mutually exclusive; the plan must choose one:

- **Correct resolution**: Remove `_followerCopyMap.TryRemove` from EvictDedup entirely. Let `CancelScopedFollowerEntries` do both iterate + `TryRemove` after the loop. Rely on the belt-and-suspenders in `CancelScopedFollowerEntries`. For the non-cancelled terminal paths (Filled/Rejected), the map entry for that leader orderId is harmless (follower copies already in terminal state), and will be evicted by the next EvictDedup cycle for each follower orderId independently. Alternatively: add a separate `_followerCopyMap.TryRemove` unconditionally at the **end of EvictDedup** but then `CancelScopedFollowerEntries` must call `TryGetValue` **before** EvictDedup processes (impossible from current OnOrderUpdate sequence) OR `CancelScopedFollowerEntries` must be called from a different trigger point.

**Engineer fix required**: Remove `_followerCopyMap.TryRemove(orderId, out _)` from `EvictDedup`. The `TryRemove` in `CancelScopedFollowerEntries` (after the loop) is the correct and only eviction point on the cancel path.

---

### Criterion 2 — Option Selection: Is Option B sound?

**PASS** (with caveat)

Option B (`ConcurrentDictionary<string, ConcurrentBag<Order>>`) is the correct choice:
- Zero name-change blast radius
- `ConcurrentDictionary` + `ConcurrentBag` satisfy JS-021 and JS-025
- `signal.OrderId` is confirmed present at [`CopySignal.OrderId` L497](src/PropTraderTools/CopyEngine.cs:497), set at L904 in `DispatchCopy`
- Rejection of Option A (PTT-Copy name embedding) is well-reasoned — avoids 3–4 name-equality predicate sites
- Rejection of Option C (reuse `_dedupCache`/`_orderMap`) is correct — wrong value types and semantics

The option selection logic is sound. The execution-order defect is in the wiring, not in the option choice.

---

### Criterion 3 — No lock(): JS-021 (P0)

**PASS**

- `_followerCopyMap` declared as `ConcurrentDictionary<string, ConcurrentBag<Order>>` — lock-free (JS-025 compliant)
- `RecordFollowerCopy` uses `GetOrAdd` + `ConcurrentBag.Add` — no lock
- `CancelScopedFollowerEntries` uses `TryGetValue` + `TryRemove` — no lock
- `EvictDedup` uses `TryRemove` — no lock
- No `lock(`, `Monitor`, `Mutex`, or `SemaphoreSlim` in any planned code block

JS-021: zero violations. ✅

---

### Criterion 4 — CYC <= 8: All new/modified methods

**PASS**

| Method | Planned CYC | JS Limit | Status |
|--------|-------------|----------|--------|
| `RecordFollowerCopy` | 1 | 8 | ✅ |
| `CancelScopedFollowerEntries` | 5 | 8 | ✅ |
| `TryCancelFollowerEntries` | 4 (was 6) | 8 | ✅ |
| `SendCopy` | 5 (unchanged) | 8 | ✅ |
| `SendCopyWithAtm` | 4 (unchanged) | 8 | ✅ |
| `EvictDedup` | 2 (unchanged) | 8 | ✅ |

McCabe counting is correct for each method. All within JS strict limit.

---

### Criterion 5 — NT8 API Correctness

**PASS**

- `acc.Cancel(Order[])` from AddOn context: confirmed at [`NT8_ADDON_KNOWLEDGE.md` L222](docs/standards/NT8_ADDON_KNOWLEDGE.md:222)
- `fo.Account.Cancel(new Order[] { fo })` pattern matches existing `CancelOneAccount` at [`CopyEngine.cs` L3336](src/PropTraderTools/CopyEngine.cs:3336)
- `ConcurrentBag<Order>` stores live NT8 Order references (updated in-place via `OnOrderUpdate`) — valid approach
- `fo.OrderState` checked at cancel time reflects current state — no stale reference issue
- `Order.OrderId.ToString()` as key: matches existing pattern at [`CopyEngine.cs` L1894](src/PropTraderTools/CopyEngine.cs:1894)
- `signal.OrderId` is the leader's orderId string (`string` type, set at L511 / L904 in `DispatchCopy`) — confirmed present in `CopySignal` at L497

One observation (not a violation, but engineer should verify): In `SendCopyWithAtm`, the follower order is named `"Entry"` (NT8 constraint for `StartAtmStrategy`, L2866) and submission is handled by `StartAtmStrategy`, NOT by `follower.Submit()`. The plan correctly identifies this (Section 4b places `RecordFollowerCopy` after `StartAtmStrategy` but before the method returns). This is valid — the `Order` object returned by `CreateOrder` is still a valid reference.

---

### Criterion 6 — Test Design: Do the two [Fact] tests verify Gap B is fixed?

**PARTIAL FAIL** (consequence of V-01)

The tests verify the **map isolation invariant** (Test 1) and **single-entry eviction path** (Test 2). Both tests exercise `EvictDedup`, which is structurally correct for what they claim to test.

However:
- Test 1 asserts that `EvictDedup("id2")` does not remove `"id1"` — this correctly validates map isolation. ✅
- Test 2 asserts that `EvictDedup("solo")` removes its bag — this validates single-entry cleanup. ✅
- **Neither test validates that `CancelScopedFollowerEntries` actually issues a cancel call.** There is no test asserting that `fo.Account.Cancel(...)` is called when a follower order is in Working state. This is a test coverage gap for the core behavioral fix.
- Due to V-01: when `EvictDedup` runs first (as in production), both tests would pass even with the broken cancel path because neither test calls `CancelScopedFollowerEntries` after `EvictDedup`.

The tests are **necessary but not sufficient** to verify Gap B is fixed. A third test is required: populate the map, call `CancelScopedFollowerEntries` directly (without calling `EvictDedup` first), and assert the cancel method is invoked. This requires a mock or stub for `fo.Account`.

---

### Criterion 7 — 7-Scan Checklist: All 7 scans present

**PASS**

Section 9 lists 7 scans:

| # | Scan | Present |
|---|------|---------|
| SCAN-01 | No lock() | ✅ |
| SCAN-02 | CYC <= 8 | ✅ |
| SCAN-03 | ASCII-only | ✅ |
| SCAN-04 | JS-001 no throw in hot path | ✅ |
| SCAN-05 | PTT- prefix on new orders | ✅ (vacuously satisfied) |
| SCAN-06 | DateTime.UtcNow | ✅ (vacuously satisfied) |
| SCAN-07 | ConcurrentDictionary for new map | ✅ |

All 7 scans present with pass criteria defined. ✅

---

### Criterion 8 — Single-Entry Constraint: Still documented?

**PASS**

Section 1 (Problem Statement) explicitly states:
> "Single-entry constraint (MUST remain documented, NOT removed): The copier design intent is one active entry per instrument per leader account at a time."

Section 4c includes the comment to be added in `TryCancelFollowerEntries`:
> "Single-entry best practice: one leader entry per instrument at a time is the supported workflow. This fix prevents collateral cancel when the constraint is violated..."

The constraint documentation is preserved in both the spec comment and the code. ✅

---

### Criterion 9 — No Scope Creep

**PASS**

Section 13 (Out of Scope) explicitly lists 5 deferred items that are NOT addressed:
- DW-B134-OCO, DW-B129-01, DW-B133, DW-B89-DEFERRED-xx, DW-B107

Changes are limited to `CopyEngine.cs` (4 methods modified/added + 1 field added) and `B130Tests.cs` (new file).
No changes to `TradeCopierWindow.cs`, `TradeCopierPanel.cs`, `PttContracts.cs`, or any other file.

Scope is tightly bounded to DW-B136 Gap B only. ✅

---

### Criterion 10 — InternalsVisibleTo: Already present or must be added?

**PASS**

[`CopyEngine.cs` L46](src/PropTraderTools/CopyEngine.cs:46):
```csharp
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("PropTraderTools.Tests")]
```
Already present in production code. No addition required. ✅

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|------------|-----------|--------------|
| DW-B136 Gap B: scope cancel to specific leader orderId | PARTIAL (design correct but broken by V-01) | Sections 3–5, 7 |
| Single-entry constraint preserved | YES | Sections 1, 4c |
| Fix design: RecordFollowerCopy + CancelScopedFollowerEntries | YES (design) | Sections 5a, 5b |
| Lock-free implementation (JS-021) | YES | Sections 3, 5a, 5b |
| CYC <= 8 for all methods | YES | Section 6 |
| xUnit [Fact] tests | YES (2 tests present) | Section 8 |
| 7-scan checklist | YES | Section 9 |

---

## Summary of Violations

| ID | Rule | Severity | Description |
|----|------|----------|-------------|
| V-01 | Functional Correctness (Spec Coverage P0) | FAIL | Execution-order defect: EvictDedup (L1277) removes map entry before CancelScopedFollowerEntries (L1361) can consume it. Plan's final EvictDedup code adds TryRemove unconditionally, defeating its own Option B resolution. Follower copy of cancelled leader order is never cancelled — DW-B136 Gap B remains unfixed in the plan's final state. |

---

## Required Fix Before REVIEW_PASS

**Engineer action on the plan (not on src — ptt-architect must correct `LaneB-02-architecture-plan.md` first):**

Remove `_followerCopyMap.TryRemove(orderId, out _)` from the `EvictDedup` method entirely.
The final `EvictDedup` body must remain:

```csharp
internal void EvictDedup(string orderId, OrderState state)
{
    if (state != Filled && state != Cancelled && state != Rejected)
        return;
    _dedupCache.TryRemove(orderId, out _);
    if (state == OrderState.Cancelled)
        _entryDispatchedOrders.Clear();
    // _followerCopyMap NOT touched here -- eviction done in CancelScopedFollowerEntries after use
}
```

`CancelScopedFollowerEntries` retains the `TryRemove(leaderOrderId, out _)` call after the loop (as in the "belt-and-suspenders" revision), which is now the **only** eviction point on the cancel path.

This restores the correct execution order:
1. `EvictDedup(Cancelled)` — clears `_dedupCache` and `_entryDispatchedOrders` only
2. `CancelScopedFollowerEntries("id2")` — finds bag in map → cancels follower copies → evicts bag

Sections 4d and 7 (EvictDedup block) must be updated accordingly.

---

## Overall Verdict

**REVIEW_FAIL**

1 violation (V-01, P0): Execution-order defect renders the core cancel path inoperative in the plan's final state. The fix requires removing `_followerCopyMap.TryRemove` from `EvictDedup` and relying solely on `CancelScopedFollowerEntries` for map eviction on the cancel path.

Return to ptt-architect for correction. Re-submit for Phase 2 review after plan is updated.

---

# B130 LaneB Plan Review — Cycle 2

**Reviewer**: ptt-plan-reviewer
**Block**: B130 LaneB
**Plan Reviewed**: `docs/brain/B130/LaneB-02-architecture-plan.md` (V2 — 2026-09-01)
**Cycle**: 2 (re-review after V-01 fix)
**Date**: 2026-09-01
**Overall Verdict**: **REVIEW_PASS**

---

## V-01 Fix Confirmation

**V-01 is FIXED.**

The prior cycle-1 violation was:
> `_followerCopyMap.TryRemove(orderId, out _)` added unconditionally to `EvictDedup`, removing the map entry before `CancelScopedFollowerEntries` could consume it at L1361, rendering the cancel path inoperative.

**V2 resolution verified**:
- Section 4d explicitly states: "`EvictDedup` body is **unchanged** from the current source. No `_followerCopyMap.TryRemove` is added to `EvictDedup`."
- The final `EvictDedup` code block (Section 4d, lines matching current source) contains only `_dedupCache.TryRemove(orderId, out _)` and `_entryDispatchedOrders.Clear()` — no `_followerCopyMap` access. ✅
- `CancelScopedFollowerEntries` (Section 5b) retains the post-loop `TryRemove(leaderOrderId, out _)` as the **sole** eviction point on the cancel path. ✅
- Section 7 execution-order diagram is internally consistent: EvictDedup at L1277 touches only `_dedupCache`; `TryGetValue("id2")` at L1361 returns HIT; follower copy of order2 is cancelled; `"id1"` bag is intact. ✅
- No contradicting code block exists anywhere in V2 (V1's contradiction between Section 7 "RESOLUTION" prose and the final EvictDedup code has been eliminated). ✅

---

## Per-Criterion Analysis (Cycle 2)

### Criterion 1 — Problem Coverage: Does the plan fix DW-B136 Gap B?

**PASS**

Section 7 (Data Flow) provides a complete execution-order trace for the two-simultaneous-entries scenario:

1. `EvictDedup("id2", Cancelled)` at L1277 → touches only `_dedupCache` and `_entryDispatchedOrders`. `_followerCopyMap["id2"]` bag remains present.
2. `CancelScopedFollowerEntries("id2")` at L1361 → `TryGetValue("id2")` = HIT → iterates bag → cancels follower copy of order2 (OrderState == Working) → `TryRemove("id2")`.
3. `_followerCopyMap["id1"]` = `{order1Copy}` — INTACT, untouched.

Result: follower copy of order2 is correctly cancelled; follower copy of order1 is untouched. DW-B136 Gap B is fixed by this plan. ✅

---

### Criterion 2 — Option Selection: Is Option B sound?

**PASS**

Option B (`ConcurrentDictionary<string, ConcurrentBag<Order>>`) rationale (Section 2) is sound:
- Zero blast radius on existing name predicates (`PTT-Copy` name unchanged). ✅
- Lock-free (JS-021, JS-025 compliant). ✅
- Lowest CYC impact: `TryCancelFollowerEntries` 6→4, two new methods at CYC=1 and CYC=5. ✅
- Live Order references: `fo.OrderState` at cancel time reflects current NT8 state. ✅
- Correct eviction order: sole `TryRemove` inside `CancelScopedFollowerEntries` post-loop. ✅
- Option A (name embed) and Option C (reuse `_dedupCache`/`_orderMap`) rejections are well-reasoned. ✅

---

### Criterion 3 — No lock(): JS-021 (P0)

**PASS**

All new/modified code surveyed:
- `_followerCopyMap` field: `ConcurrentDictionary<string, ConcurrentBag<Order>>` — no `lock`. ✅
- `RecordFollowerCopy`: `GetOrAdd` + `ConcurrentBag.Add` — no `lock`. ✅
- `CancelScopedFollowerEntries`: `TryGetValue`, `foreach`, `TryRemove` — no `lock`. ✅
- `EvictDedup` body: unchanged, contains no `lock(`. ✅
- `SendCopy` / `SendCopyWithAtm` additions: single `RecordFollowerCopy` call, no `lock`. ✅
- No `Monitor`, `Mutex`, or `SemaphoreSlim` for state anywhere in the plan. ✅

JS-021: zero violations. ✅

---

### Criterion 4 — CYC ≤ 8: All new/modified methods

**PASS**

| Method | CYC (V2) | Limit | Status |
|--------|----------|-------|--------|
| `RecordFollowerCopy` (new) | 1 | 8 | ✅ |
| `CancelScopedFollowerEntries` (new) | 5 | 8 | ✅ |
| `TryCancelFollowerEntries` (modified) | 4 (was 6) | 8 | ✅ |
| `SendCopy` (modified) | 5 (unchanged) | 8 | ✅ |
| `SendCopyWithAtm` (modified) | 4 (unchanged) | 8 | ✅ |
| `EvictDedup` (unmodified) | 2 | 8 | ✅ |

McCabe counting verified:
- `CancelScopedFollowerEntries`: base 1 + (1) TryGetValue miss + (2) foreach + (3) compound OR OrderState guard + (4) try/catch = 5. Post-loop unconditional `TryRemove` adds no branch. ✅
- `TryCancelFollowerEntries`: base 1 + (1) Cancelled state + (2) IsAtmBracketName + (3) compound prefix OR = 4. ✅

All methods ≤ 8. ✅

---

### Criterion 5 — NT8 API Correctness

**PASS**

- `acc.Cancel(Order[])` from AddOn context: confirmed at `NT8_ADDON_KNOWLEDGE.md` L222. ✅
- `fo.Account.Cancel(new Order[] { fo })` pattern (Section 5b) structurally identical to existing `CancelOneAccount` at L3336. ✅
- `ConcurrentBag<Order>` holds live NT8 Order references updated in-place via `OnOrderUpdate`. ✅
- `fo.OrderState` at cancel time reflects current live state — no stale reference. ✅
- `Order.OrderId.ToString()` as key: matches existing pattern at L1894, L1684, L3516. ✅
- `signal.OrderId` in `SendCopy`/`SendCopyWithAtm`: `CopySignal.OrderId` confirmed at L497. ✅
- No `AtmStrategyCreate` (StrategyBase-only API) used anywhere in this plan. ✅
- No `async/await` in any NT8 lifecycle method. ✅

---

### Criterion 6 — Test Design: [Fact] tests verify Gap B

**PASS**

V2 plan introduces **three** xUnit `[Fact]` tests (Section 8), resolving the cycle-1 partial-fail:

| Test | What it verifies |
|------|-----------------|
| `B130_DW136_CancelLeaderOrder2DoesNotEvictLeaderOrder1Bag` | `EvictDedup("id2")` does NOT touch `"id1"` or `"id2"` bag — map isolation regression guard against V-01 re-introduction. ✅ |
| `B130_DW136_CancelScopedFollowerEntriesEvictsMapEntryAfterLoop` | Post-loop `TryRemove` fires even for empty bag — confirms eviction path is unconditional. ✅ |
| `B130_DW136_CancelScopedFollowerEntriesMissesAfterEvictDedup` | Documents V-01 regression scenario. Confirms no-throw on TryGetValue miss. Confirms correct-order eviction. Confirms `EvictDedup` on absent ID is safe no-op. ✅ |

**Note on cancel-call assertion**: A test asserting that `fo.Account.Cancel(...)` is invoked requires a Working NT8 `Order` object with a valid `Account` reference. NT8's `Account` is a sealed runtime type that cannot be instantiated in a unit test context. Testing of `fo.Account.Cancel(...)` invocation is delegated to the Director SIM gate (standard PTT test-boundary protocol). The three plan tests are complete for the unit-testable surface area. ✅

Framework: xUnit `[Fact]` only — mandated by RULES_CATALOG.md. No NUnit/MSTest. ✅

---

### Criterion 7 — 7-Scan Checklist: All 7 scans present

**PASS**

Section 9 lists all 7 scans with commands and pass criteria:

| # | Scan | Present | Notes |
|---|------|---------|-------|
| SCAN-01 | No lock() | ✅ | grep pattern provided |
| SCAN-02 | CYC ≤ 8 | ✅ | Manual count per Section 6 |
| SCAN-03 | ASCII-only | ✅ | grep pattern provided |
| SCAN-04 | JS-001 no throw in hot path | ✅ | try/catch, no rethrow |
| SCAN-05 | PTT- prefix on new orders | ✅ | Vacuously satisfied (no new orders) |
| SCAN-06 | DateTime.UtcNow | ✅ | Vacuously satisfied (no DateTime) |
| SCAN-07 | ConcurrentDictionary for new map | ✅ | `ConcurrentDictionary<string, ConcurrentBag<Order>>` |

All 7 scans present with pass criteria defined. ✅

---

### Criterion 8 — Single-Entry Constraint: Still documented?

**PASS**

- Section 1: Explicit MUST-NOT-REMOVE annotation: *"Single-entry constraint (MUST remain documented, NOT removed)"*. ✅
- Section 4c: Comment to be preserved in `TryCancelFollowerEntries` body: *"Single-entry best practice: one leader entry per instrument at a time is the supported workflow."* ✅

Constraint documentation preserved in both spec comment and code. ✅

---

### Criterion 9 — No Scope Creep

**PASS**

Section 13 explicitly lists 5 deferred items not addressed:
- DW-B134-OCO, DW-B129-01, DW-B133, DW-B89-DEFERRED-xx, DW-B107

Changes bounded to:
- `CopyEngine.cs`: 1 new field + 2 new methods + 3 modified methods (body changes only, no signature breaks except `TryCancelFollowerEntries` body simplification).
- `Tests/B130Tests.cs`: new file.

No changes to `TradeCopierWindow.cs`, `TradeCopierPanel.cs`, `PttContracts.cs`, or any other file. ✅

---

### Criterion 10 — InternalsVisibleTo: Present in codebase?

**PASS**

Plan references `[assembly: InternalsVisibleTo("PropTraderTools.Tests")]` at `CopyEngine.cs` L46 (confirmed as already present in the production codebase). No addition required. New fields and methods are `internal` visibility — accessible to test assembly via this attribute. ✅

---

## Spec Coverage Matrix (Cycle 2)

| Requirement | Addressed? | Plan Section |
|------------|-----------|--------------|
| DW-B136 Gap B: scope cancel to specific leader orderId | YES | Sections 3–5, 7 |
| Single-entry constraint preserved | YES | Sections 1, 4c |
| Fix design: RecordFollowerCopy + CancelScopedFollowerEntries | YES | Sections 5a, 5b |
| Lock-free implementation (JS-021, JS-025) | YES | Sections 3, 5a, 5b |
| CYC ≤ 8 for all methods | YES | Section 6 |
| xUnit [Fact] tests (≥2) | YES (3 tests) | Section 8 |
| 7-scan checklist | YES | Section 9 |
| EvictDedup NOT modified | YES | Section 4d |

---

## Violations Found (Cycle 2)

**None.** Zero violations.

---

## Overall Verdict (Cycle 2)

**REVIEW_PASS**

V-01 is confirmed fixed. All 10 criteria pass. All P0/P1 hardcoded DNA rules satisfied. Plan V2 is approved to proceed to Phase 3 (ticket generation).
