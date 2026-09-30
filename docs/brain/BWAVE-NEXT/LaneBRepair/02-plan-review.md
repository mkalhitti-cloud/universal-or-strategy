# BWAVE-NEXT LaneBRepair -- Plan Review

**Epic**: BWAVE-NEXT LaneBRepair  
**Phase**: 2 Review  
**Reviewer**: ptt-plan-reviewer  
**Input**: `docs/brain/BWAVE-NEXT/LaneBRepair/02-architecture-plan.md`  
**Spec**: `docs/brain/BWAVE-NEXT/LaneB-repair-mission-brief.md`  
**Catalog**: `docs/standards/jane-street/RULES_CATALOG.md`  
**Date**: 2026-09-05  

---

## RULES CATALOG GATE

Catalog read: `docs/standards/jane-street/RULES_CATALOG.md` — UTF-8 clean, JS-001 through
JS-035+ fully loaded.  
P0 rules confirmed applicable to engineer's scope: JS-021 (lock ban), JS-033 (async void ban),
JS-001 (throw ban), JS-002 (return null ban).  
Zero P0 violations in the plan document itself.

**GATE RESULT: PASS**

---

## Section 1 — Lane-Split Gate Compliance

| Check | Result | Evidence |
|-------|--------|----------|
| Plan states `LANE-SPLIT GATE RESULT: SINGLE-PIPELINE` | PASS | Plan line 44 (also echoed at line 393) |
| Q1 answer YES (same method cluster) — valid reason for single-pipeline | PASS | Plan lines 46-48: all fixes in `OnOrderUpdate` (lines 1412-1416), `DrainThenDispatch` (lines 6507-6546), `TryReplaceOnAtmCancel` (lines 863-868) |
| No lane split attempted without gate passage | PASS | Single-ticket T1 design; no parallel lanes in plan |

**Section verdict: PASS**

---

## Section 2 — Spec Coverage

### F1 — Filled → TryRemove + abort; Cancelled/Rejected → OnDrainCancelAck

| Check | Result | Evidence |
|-------|--------|----------|
| Filled routed to `TryRemove` + abort (not drain-ack) | PASS | Plan Section C F1 shows `else if (e.Order.OrderState == OrderState.Filled) { _pendingDispatchDrains.TryRemove(e.Order.Account.Name, out _); }` |
| Cancelled/Rejected routed to `OnDrainCancelAck` | PASS | Plan Section C F1 shows outer `if (Cancelled \|\| Rejected) { if (ContainsKey) OnDrainCancelAck(...); }` |
| Code verbatim matches spec fix block | PASS | Spec F1 fix block and plan C F1 revised design are identical |

**F1 verdict: PASS**

### F2 — CONFIRMED name prefix and type from source read

| Check | Result | Evidence |
|-------|--------|----------|
| Name prefix sourced from actual read (not assumed) | PASS | Plan Section B F2 reads `FindFollowerEntryOrder` lines 3684-3702 and `SubmitEntryDirect` line 6575, confirming `"PTT-Copy"` |
| `o.Name.StartsWith("PTT-Copy", StringComparison.Ordinal)` used | PASS | Plan Section C F2 (line 246) |
| `o.OrderType == OrderType.Limit \|\| OrderType.StopLimit` predicate included | PASS | Plan Section C F2 (lines 245-246) |
| "Entry" (Clone mode) correctly excluded with rationale | PASS | Plan line 108: "Note: `"Entry"` is Clone mode — NOT a drain concern. Brief explicitly restricts to PTT-Copy." |

**F2 verdict: PASS**

### F3 — All five design elements present

| Check | Result | Evidence |
|-------|--------|----------|
| Field declaration `ConcurrentDictionary<long, byte> _drainOwnedOrderIds` | PASS | Plan Section C F3 lines 256-258 |
| `DrainThenDispatch` recording: `TryAdd(e.OrderId, 0)` in cancel foreach | PASS | Plan Section C F3 lines 275-279 |
| `TryReplaceOnAtmCancel` guard: `ContainsKey(order.OrderId) → return` BEFORE line 865 | PASS | Plan Section C F3 lines 283-287; explicitly states "insert BEFORE line 865" |
| Cleanup in `SubmitDrainedEntry` after `TryRemove` | PASS | Plan Section C F3 lines 290-293 |
| Cleanup in `TryDrainWatchdog` inside timeout block | PASS | Plan Section C F3 lines 295-299 |
| `DrainedOrderIds` property added to `PendingDispatchDrain` | PASS | Plan Section C F3 lines 263-265 and Section H ctor signature |

**F3 verdict: PASS**

### F4 — Option A confirmed, PendingDispatchDrain ctor signature verified

| Check | Result | Evidence |
|-------|--------|----------|
| Constructor signature read from source (lines 6662-6671), all 9 params listed | PASS | Plan Section B F4 lines 133-144 |
| Option A selected and confirmed viable | PASS | Plan Section B F4 line 146: "Option A is viable" |
| `pendingCancelCount: entryCandidates.Count` at construction (before payload visible) | PASS | Plan Section C F4 shows `_pendingDispatchDrains[acctKey] = payload` AFTER ctor call |
| `Interlocked.Exchange` removed | PASS | Plan Section C F4 comment "No Interlocked.Exchange -- count was set correctly at construction" |
| `cancelCount` variable removed | PASS | Plan Section C F4: "Remove: `int cancelCount = 0;` variable, `cancelCount++` increment, and Interlocked.Exchange" |

**F4 verdict: PASS**

### F5 — Dead branch removed + CYC comment updated

| Check | Result | Evidence |
|-------|--------|----------|
| Lines 6541-6546 deleted | PASS | Plan Section C F5 lines 337-344 |
| CYC comment updated from `CYC=4` to `CYC=3` | PASS | Plan Section C F5 lines 346-354 |
| Updated comment includes rationale (`F5-repair: dead (4) cancelCount==0 branch removed`) | PASS | Plan line 353 |

**F5 verdict: PASS**

### Test Renames — 5 renames, correct files

| Check | Current Name | New Name | File | Result |
|-------|-------------|---------|------|--------|
| Rename 1 | `ActiveOrders_ThreadSafetyVerification` | `ActiveOrders_FilterBehavior_AfterToListAddition` | `BwaveDwLaneATests.cs` | PASS |
| Rename 2 | `NakedDetector_DebounceField_UsesLongArithmetic` | `NakedDetector_DebounceState_FieldTypeIsLong` | `BwaveDwLaneATests.cs` | PASS |
| Rename 3 | `DrainThenDispatch_CancelsExistingEntryBeforeSubmit` | `DrainThenDispatch_MethodExists_WithExpectedSignature` | `BwaveNextLaneBTests.cs` | PASS |
| Rename 4 | `OnDrainCancelAck_SubmitsDrainedEntry_WhenPendingCountReachesZero` | `OnDrainCancelAck_MethodExists_WithExpectedSignature` | `BwaveNextLaneBTests.cs` | PASS |
| Rename 5 | `DrainWatchdog_ClearsStuckDrain_AfterTimeout` | `DrainWatchdog_MethodExists_WithExpectedSignature` | `BwaveNextLaneBTests.cs` | PASS |

All 5 renames match spec exactly. Bodies and `[Fact]` attributes explicitly preserved per plan Section E "Rename-only rule".

**Test renames verdict: PASS**

**Section 2 overall verdict: PASS**

---

## Section 3 — CYC Analysis

| Method | Pre-fix CYC | Post-fix CYC | Delta | Budget | Result |
|--------|-------------|--------------|-------|--------|--------|
| `OnOrderUpdate` | 7 | 8 | +1 (F1) | ≤8 | PASS |
| `DrainThenDispatch` | 4 | 3 | -1 (F5 dead branch removed) | ≤8 | PASS |
| `TryReplaceOnAtmCancel` | 2 | 3 | +1 (F3 guard) | ≤8 | PASS |
| `SubmitDrainedEntry` | 2-3 | 3-4 | +1 (F3 cleanup foreach) | ≤8 | PASS |
| `TryDrainWatchdog` | 3 | 4 | +1 (F3 cleanup inner foreach) | ≤8 | PASS |
| `OnDrainCancelAck` | 3 | 3 | 0 | ≤8 | PASS |
| `PendingDispatchDrain` ctor | 0 | 0 | 0 (data class) | ≤8 | PASS |

**CYC accounting note for `OnOrderUpdate`**: The plan's Section C F1 transparently shows initial
arithmetic confusion (computing +2) then revision to +1 per the brief's authoritative accounting.
The brief explicitly states "was 7, now 8." The plan's final decision (Section D table) follows
the brief and is consistent with the codebase's existing CYC convention (the `"CYC +1 (branch 7)"`
comment at plan line 84). The inner `if (ContainsKey)` guard inside an if-arm is a guard, not a
top-level OnOrderUpdate branch decision — consistent with the cited `FindFollowerEntryOrder`
null-guard example. This is not a rule violation; the reasoning is documented and internally
consistent.

**Section 3 verdict: PASS**

---

## Section 4 — Jane Street Compliance

| Rule | Check | Result |
|------|-------|--------|
| JS-021: No `lock(` | All new code uses `ConcurrentDictionary.TryAdd`, `TryRemove`, `ContainsKey` — zero `lock(` in any proposed code | PASS |
| JS-033: No `async void` | All new/modified methods (`TryReplaceOnAtmCancel`, `DrainThenDispatch`, `SubmitDrainedEntry`, `TryDrainWatchdog`) are synchronous `void` with no async keyword | PASS |
| JS-002: No `return null` | No new nullable return paths; no `return null` in any proposed code block | PASS |
| JS-001: No `throw new` | No `throw new` in any proposed new code | PASS |
| CYC ≤ 8 | All post-fix methods ≤ 8 (worst case: OnOrderUpdate = 8, exact budget) | PASS |
| ASCII-only | All identifiers and string literals in proposed code are ASCII-only (`"PTT-Copy"`, `_drainOwnedOrderIds`, etc.) | PASS |
| xUnit-only | Test renames preserve `[Fact]` attribute; no `[Test]`, NUnit, or MSTest introduced | PASS |
| NT8 banned APIs | No `Account.Change()`, `AtmStrategyCreate()`, `AtmStrategyChangeStopTarget()` in proposed code | PASS |
| DateTime.UtcNow (no DateTime.Now) | No `DateTime.Now` introduced; `Environment.TickCount` pattern unchanged | PASS |
| No FontFamily override | Not applicable; no UI changes | PASS |

**Section 4 verdict: PASS**

---

## Section 5 — Out-of-Scope Exclusions

| Out-of-Scope Item | Spec Rationale | Plan Excludes? | Result |
|-------------------|---------------|----------------|--------|
| `TickCount64` change | .NET 4.8 target; `TickCount64` is .NET 5+ | Not mentioned in plan | PASS |
| Remove `.ToList()` from `ActiveOrders` | Thread-safety fix, DW-NEXT-A-07; out of scope | Not in plan | PASS |
| Drain key extension to `acct+instrument` (DW-NEXT-B-01) | Future backlog P2 | Not implemented; explicitly listed as out-of-scope in spec | PASS |
| GTC/Day TIF preservation (DW-NEXT-B-02) | Future backlog P2 | Not implemented | PASS |

**Section 5 verdict: PASS**

---

## Section 6 — Ticket Structure

| Check | Result | Evidence |
|-------|--------|----------|
| Single ticket T1 covers F1-F5 + test renames | PASS | Plan Section H defines one ticket T1 covering F1-F5 + F7/8/9 |
| Justification for bundling stated | PASS | Plan Section H: "same method cluster", "F3's cleanup design references F4's payload change (DrainedOrderIds)", "F5 removes a dead branch in DrainThenDispatch, the same method that F2, F3, and F4 also modify — separating them would require partial-edit coordination" |
| 7-scan checklist included in ticket | PASS | Plan Section H lines 470-478 lists SCAN-01 through SCAN-07 |
| Acceptance criteria complete | PASS | Plan Section H lines 482-491 match spec acceptance criteria |

**Section 6 verdict: PASS**

---

## Violation Summary

| Violation ID | Rule | Description | Location in Plan | Status |
|-------------|------|-------------|-----------------|--------|
| — | — | No violations found | — | — |

**Zero violations.**

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| F1: Filled → TryRemove abort; Cancelled/Rejected → OnDrainCancelAck | YES | C (F1), D, H |
| F2: CONFIRMED name prefix + OrderType filter | YES | B (F2), C (F2) |
| F3: field + recording + guard + cleanup-ack + cleanup-watchdog | YES | B (F3), C (F3), H |
| F4: Option A (count before ctor), ctor signature confirmed | YES | B (F4), C (F4), H |
| F5: dead branch removed + CYC comment updated | YES | B (F5), C (F5), D |
| Test renames: 5 renames, correct files | YES | E, H |
| Lane-split gate answered | YES | A (LANE-SPLIT), F |
| CYC ≤ 8 all post-fix methods | YES | D |
| JS-021/033/002/001 compliance | YES | G, H (SCAN-01–04) |
| 7-scan checklist | YES | H (SCAN-01–07) |
| Out-of-scope items excluded | YES | Spec out-of-scope table; none implemented |
| Single-ticket bundling justified | YES | H (Justification) |

**All spec requirements addressed.**

---

## Final Verdict

**REVIEW_PASS**

*Reviewer: ptt-plan-reviewer | 2026-09-05 | Phase 2 | BWAVE-NEXT LaneBRepair*  
*Zero violations found. All checklist items pass. Plan is cleared for Phase 3 (ticket generation).*
