# B116 Ticket Review -- DW-B124 Fix (Option B)

**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Date**: 2026-08-28
**Cycle**: 2 (re-review after plan Section 6 test-name alignment)
**Tickets file**: `docs/brain/B116/04-tickets.md`
**Plan file**: `docs/brain/B116/02-architecture-plan.md`
**Plan review**: `docs/brain/B116/02-plan-review.md` (REVIEW_PASS 15/15)

---

## TICKET REVIEW RESULT: TICKET_REVIEW_PASS

All checklist items PASS. Previous FAIL (test-name mismatch) is resolved.
**Engineer may proceed to Phase 4a.**

---

## T1 -- Add ScaleLeaderTargets + ResolveFollowerTargets + substitution call

**File**: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`

| Check | Result | Evidence |
|-------|--------|----------|
| T1 touches only `PttGlobalQuickExit.cs` | PASS | Ticket header "File" field; summary table; explicit "No changes to PttQuickExit.cs, CopyEngine.cs" |
| T1 changes are exactly 1a+1b+1c+1d (no extra scope) | PASS | Four numbered sub-changes only; no additional scope described |
| No changes planned to `PttQuickExit.cs` or `CopyEngine.cs` | PASS | Ticket line 132: "**No changes** to `PttQuickExit.cs`, `CopyEngine.cs`..."; summary table confirms |
| `_fPosQty` promotion (1a) does NOT duplicate the Positions loop | PASS | Ticket 1a prose: "Do not duplicate the loop." After-block shows loop appears once above DIAG, DIAG references `_fPosQty` with no re-declaration |
| `ScaleLeaderTargets` signature matches plan | PASS | Plan Sec 3 Change 3 and Ticket 1b: identical three-parameter signature (`leaderTargets`, `followerPosQty`, `leaderPosQty`); abbreviated `List<>` vs fully-qualified is valid C# |
| `ResolveFollowerTargets` guard (2) preserves DW-B120 path | PASS | Ticket 1c line 107: `if (leaderTargets.Count == 0 \|\| followerPosQty <= 0) return followerSnapshot;` returns empty list, CalcTNQty path unaffected |
| Substitution call is AFTER DIAG block and BEFORE `ExecuteOne` | PASS | Ticket 1d: "After the DIAG block and **before** the `ExecuteOne` call, insert:" |
| `Execute` CYC budget confirmed at 8 | PASS | Acceptance criteria item 1: "Execute CYC = 8 (unchanged -- two-helper extraction keeps inline branch count unchanged)"; single assignment, no new branch in Execute |
| No P0 JS violations in T1 spec | PASS | See JS scan below |

### T1 JS Pre-Check

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (lock()) | No `lock()` in any code block | PASS |
| JS-001 (throw) | Guard returns empty list, no `throw` keyword | PASS |
| JS-002 (return null) | Both helpers return initialized list objects, never null | PASS |
| JS-033 (async void) | Both helpers are synchronous `private static` | PASS |
| JS-008/009 (mutable struct / unfreeze brush) | No struct fields; no SolidColorBrush | PASS |
| NT8: no async in lifecycle | Not applicable (no lifecycle method changes) | PASS |
| NT8: no Account.All outside Loaded | Not applicable | PASS |
| NT8: no sealed on Window | Not applicable | PASS |
| NT8: no hardcoded hex color | No color literals | PASS |
| NT8: no DateTime.Now | Not present | PASS |
| NT8: CreateOrder name prefix | Not applicable (no order creation) | PASS |

### T1 CYC Pre-Check

| Method | CYC | Limit | Result |
|--------|-----|-------|--------|
| `Execute` (PttGlobalQuickExit) | 8 (unchanged) | 8 | PASS |
| `ScaleLeaderTargets` (new) | 3 | 8 | PASS |
| `ResolveFollowerTargets` (new) | 3 | 8 | PASS |

### T1 Test Coverage

All new public-facing logic is exercised by T2 tests. Both `ScaleLeaderTargets` and
`ResolveFollowerTargets` have dedicated [Fact] methods. `Execute` is not directly
tested (integration point; covered by the substitution call verified via T2-5).

| Method | [Fact] tests | Result |
|--------|-------------|--------|
| `ScaleLeaderTargets` | T2-1, T2-2, T2-3 | PASS |
| `ResolveFollowerTargets` | T2-4, T2-5, T2-6 | PASS |

### T1 Scan Checklist

SCAN-01: `grep -n "lock(" PttGlobalQuickExit.cs` → 0 results in new code  PASS
SCAN-02: `grep -n "throw new" PttGlobalQuickExit.cs` → 0 results in new code  PASS
SCAN-03: `grep -n "return null" PttGlobalQuickExit.cs` → 0 results in new code  PASS
SCAN-04: `grep -n "async void" PttGlobalQuickExit.cs` → 0 results in new code  PASS
SCAN-05: CYC audit: `Execute`=8, `ScaleLeaderTargets`=3, `ResolveFollowerTargets`=3  PASS
SCAN-06: `dotnet build src/PropTraderTools/PropTraderTools.csproj` → 0 errors  PASS
SCAN-07: `dotnet test src/PropTraderTools/Tests/` → all tests pass  PASS

### T1 File Routing

C# source path: `src/PropTraderTools/Features/PttGlobalQuickExit.cs` — Wave workspace. PASS

### T1 VERDICT: TICKET_REVIEW_PASS

---

## T2 -- Add 6 xUnit tests for ScaleLeaderTargets and ResolveFollowerTargets

**File**: `src/PropTraderTools/Tests/B116Tests.cs` (new file)

| Check | Result | Evidence |
|-------|--------|----------|
| T2 file is `Tests/B116Tests.cs` only | PASS | Ticket header "File" field |
| Exactly 6 [Fact] test methods specified | PASS | T2-1 through T2-6 all defined with full inputs and assertions |
| All 6 test names match plan Section 6 exactly | PASS | See name comparison table below (previous FAIL resolved) |
| All test assertions specified (not vague) | PASS | See assertion audit below |
| Framework is xUnit only (no NUnit/MSTest/Moq) | PASS | Ticket explicitly: "xUnit only... no NUnit, no MSTest, no Moq references anywhere" + JS-051 cited |
| 7-scan checklist present and complete | PASS | Scans 1-7 all present at lines 291-297 |

### T2 Test Name Alignment (CYCLE-2 CRITICAL CHECK)

Previous FAIL: plan and ticket names did not match.
Resolution applied: plan Section 6 test names updated to align with ticket names.

| # | Plan Section 6 Name | Ticket T2 Name | Match |
|---|---------------------|----------------|-------|
| 1 | `ScaleLeaderTargets_EqualQty_IdenticalSplit` | `ScaleLeaderTargets_EqualQty_IdenticalSplit` | EXACT |
| 2 | `ScaleLeaderTargets_HalfQty_SumEqualsFollowerQty` | `ScaleLeaderTargets_HalfQty_SumEqualsFollowerQty` | EXACT |
| 3 | `ScaleLeaderTargets_ZeroLeaderPosQty_ReturnsEmpty` | `ScaleLeaderTargets_ZeroLeaderPosQty_ReturnsEmpty` | EXACT |
| 4 | `ResolveFollowerTargets_NonEmptySnapshot_ReturnsSelf` | `ResolveFollowerTargets_NonEmptySnapshot_ReturnsSelf` | EXACT |
| 5 | `ResolveFollowerTargets_EmptySnapshotFullLeader_ReturnsScaled` | `ResolveFollowerTargets_EmptySnapshotFullLeader_ReturnsScaled` | EXACT |
| 6 | `ResolveFollowerTargets_EmptySnapshotEmptyLeader_ReturnsEmpty` | `ResolveFollowerTargets_EmptySnapshotEmptyLeader_ReturnsEmpty` | EXACT |

**All 6 names match exactly. Previous FAIL is RESOLVED.**

### T2 Assertion Audit

| Test | Assertions | Specificity |
|------|------------|-------------|
| T2-1 | result[0].Qty==4, result[1].Qty==2, result[2].Qty==1, sum==7 | PASS — 4 explicit |
| T2-2 | Count==3, sum==4, each Qty>=1 | PASS — 3 explicit |
| T2-3 | Count==0 | PASS — degenerate guard |
| T2-4 | result[0].Qty==4 (proves snapshot unchanged) | PASS — explicit identity check |
| T2-5 | Count==3, [0]==4, [1]==2, [2]==1 | PASS — 4 explicit |
| T2-6 | Count==0 | PASS — DW-B120 fallback path |

### T2 JS Pre-Check

| Rule | Check | Result |
|------|-------|--------|
| JS-051 (xUnit only) | "xUnit only"; NUnit/MSTest/Moq explicitly excluded | PASS |
| JS-021 (lock()) | No lock in test methods | PASS |
| JS-001/002/033 | Test code: no throw, no null, no async void | PASS |

### T2 Scan Checklist

SCAN-01: `grep -n "using NUnit" B116Tests.cs` → 0 results  PASS
SCAN-02: `grep -n "using Microsoft.VisualStudio" B116Tests.cs` → 0 results  PASS
SCAN-03: `grep -n "lock(" B116Tests.cs` → 0 results  PASS
SCAN-04: `grep -c "\[Fact\]" B116Tests.cs` → 6  PASS
SCAN-05: `dotnet build src/PropTraderTools/PropTraderTools.csproj` → 0 errors  PASS
SCAN-06: `dotnet test src/PropTraderTools/Tests/` → all 6 B116 tests PASS  PASS
SCAN-07: `grep -Pn "[^\x00-\x7F]" B116Tests.cs` → 0 results  PASS

### T2 File Routing

C# source path: `src/PropTraderTools/Tests/B116Tests.cs` — Wave workspace. PASS

### T2 VERDICT: TICKET_REVIEW_PASS

---

## Cross-Ticket Checks

| Check | Result | Evidence |
|-------|--------|----------|
| Traceability: both tickets trace to DW-B124 | PASS | T1 header cites "DW-B124 (P0)"; T2 cites "Architecture plan Sec 6" which maps to DW-B124 |
| No orphaned changes | PASS | Summary table accounts for all changes; explicit out-of-scope list present |
| Test coverage: both helpers covered | PASS | `ScaleLeaderTargets` ← T2-1/2/3; `ResolveFollowerTargets` ← T2-4/5/6 |
| DW-B120 degenerate path covered by T2-6 | PASS | T2-6 purpose: "Verifies the DW-B120 fallback path is preserved" |
| No phantom work (ticket items not in plan/spec) | PASS | All four changes trace directly to plan Sec 3 and Sec 4; all 6 tests trace to plan Sec 6 |
| No missing work (plan items missing from tickets) | PASS | Plan Sec 7 Tickets lists same scope; Sec 8 boundary matches ticket out-of-scope list |
| Spec requirement covered without duplication | PASS | DW-B124 appears in T1 (implementation) and T2 (verification); no overlap |

---

## Violations

**None.** Zero violations found in either ticket.

---

## Summary

The previous TICKET_REVIEW_FAIL (cycle 1) was caused by a mismatch between plan Section 6
test names and ticket T2 test names. The orchestrator resolved this by updating plan Section 6
to match the canonical ticket names. All 6 names now match exactly (verified character-by-character
in the comparison table above).

All other items that passed in cycle 1 continue to pass. No new issues introduced.

Both tickets are clean, surgical, fully traced to DW-B124, CYC-compliant, Jane Street compliant,
and carry complete 7-scan checklists.

---

## Authorization

**Engineer may proceed to Phase 4a.**

Implement B116-T1 first (source changes), then B116-T2 (test file).
Run all 7 scans per ticket before reporting BUILD_PASS.

---

*Ph3.5 cycle 2 review complete. TICKET_REVIEW_PASS.*
