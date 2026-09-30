# Ticket Review: BWAVE-DW LaneB
**Phase**: 3.5 (Ticket Review — Cycle 2, re-review after V-001 fix)
**Reviewer**: ptt-ticket-reviewer
**Date**: 2026-08-26
**Input**: `docs/brain/BWAVE-DW/LaneB/04-tickets.md` (V-001 fix applied)
**Plan**: `docs/brain/BWAVE-DW/LaneB/02-architecture-plan.md`

---

## V-001 Fix Verification (cycle 2 focus)

**Violation from cycle 1**: TICKET B-4 "After" body was missing the first-match-wins guard. The
original code used `break` inside the inner `foreach` to stop on the first match per follower.
The initial refactor used only `if (idx < 0) continue;` — which allowed a second `_followerItems`
entry for the same account to overwrite the first assignment.

**Fix applied**: The combined guard `if (idx < 0 || multipliers[idx] != 0) continue;` is now present
in the "After" block at line 372 of `04-tickets.md`. This skips any item whose index is already
filled (`multipliers[idx] != 0`), exactly preserving original `break`-on-first-match semantics.

**CYC impact of fix**: The `||` operator adds one branch. Ticket now correctly states CYC = 5
(base(1) + foreach(+1) + if-null(+1) + if-combined(+1 for the `||`) = 5). ≤ 8. PASS.

**Behavioral equivalence**: For any input where each `Account` appears at most once in
`_followerItems`, results are identical. For duplicate entries, first-match wins in both
original and refactored code. PASS.

---

## T1 — TICKET B-1: Delete BwaveCycR2ArrowClusterTests Class

**Type**: ACTIVE (test deletion)
**Spec Req IDs**: DW-C39-06, DW-LaneA-06
**Plan Section**: §5 TICKET B-1 / §4 row B-1

### Check Results

| Check | Result | Notes |
|-------|--------|-------|
| Traceability | PASS | DW-C39-06 / DW-LaneA-06 map to plan §4 B-1 exactly. No phantom work. |
| Spec Coverage | PASS | Covered exactly once. No duplicate. |
| JS Pre-Check | PASS | Deletion only. No new code paths. No JS-XXX rule violations. |
| CYC Pre-Check | PASS | N/A — no method bodies changed. |
| NT8 Check | PASS | No NT8 API surface introduced. |
| Test Coverage | PASS | Deletion ticket — no new methods; no [Fact] required. |
| Scan Checklist | PASS | SCAN-01 through SCAN-07 all present with commands and expected results. |
| File Routing | PASS | `src/PropTraderTools/Tests/BwaveCycLaneCTests.cs` — Wave workspace. |

**VERDICT: TICKET_REVIEW_PASS**

---

## T2 — TICKET B-2: Verify BrushInactive at Button Construction

**Type**: VERIFY-ONLY
**Spec Req IDs**: DW-C39-09
**Plan Section**: §5 TICKET B-2 / §4 row B-2

### Check Results

| Check | Result | Notes |
|-------|--------|-------|
| Traceability | PASS | DW-C39-09 maps to plan §5 B-2 exactly. Evidence cites lines 1152-1168 and line ~1233. |
| Spec Coverage | PASS | Covered exactly once. No duplicate. |
| JS Pre-Check | PASS | No code changes. No violations possible. |
| CYC Pre-Check | PASS | No code changes. |
| NT8 Check | PASS | No NT8 API surface. |
| Test Coverage | PASS | Verify-only; no new methods; no [Fact] required. |
| Scan Checklist | PASS | SCAN-01 through SCAN-07 all present. SCAN-05 = N/A is acceptable for no-change ticket. |
| File Routing | PASS | No files modified. |

**VERDICT: TICKET_REVIEW_PASS**

---

## T3 — TICKET B-3: Verify WPF Cluster Helpers Extraction

**Type**: VERIFY-ONLY
**Spec Req IDs**: DW-C38-02
**Plan Section**: §5 TICKET B-3 / §4 row B-3

### Check Results

| Check | Result | Notes |
|-------|--------|-------|
| Traceability | PASS | DW-C38-02 maps to plan §5 B-3. All 6 helpers cited with exact line numbers and CYC values. |
| Spec Coverage | PASS | Covered exactly once. No duplicate. |
| JS Pre-Check | PASS | No code changes. No violations possible. |
| CYC Pre-Check | PASS | No code changes. All 6 helpers CYC ≤ 8 per plan evidence table. |
| NT8 Check | PASS | No NT8 API surface. |
| Test Coverage | PASS | Verify-only; no new methods; no [Fact] required. |
| Scan Checklist | PASS | SCAN-01 through SCAN-07 all present. SCAN-05 = N/A acceptable. |
| File Routing | PASS | No files modified. |

**VERDICT: TICKET_REVIEW_PASS**

---

## T4 — TICKET B-4: Refactor BuildFollowerMultipliers — Inverted Loop

**Type**: ACTIVE (1 method replaced, 1 new test file)
**Spec Req IDs**: DW-C39-07
**Plan Section**: §5 TICKET B-4 / §4 row B-4

### V-001 Fix Status

The cycle-1 violation is **RESOLVED**. The combined guard
`if (idx < 0 || multipliers[idx] != 0) continue;` is present in the "After" block.
First-match-wins semantics are correctly preserved. CYC updated to 5 in both the
comment (`CCN=5`) and the CYC analysis table. Behavioral-equivalence narrative is
accurate and complete.

### Check Results

| Check | Result | Notes |
|-------|--------|-------|
| Traceability | PASS | DW-C39-07 maps to plan §4 B-4 / §5 TICKET B-4. No phantom work. Corrected target method (BuildFollowerMultipliers, not BuildAtmMap) documented per plan §3. |
| Spec Coverage | PASS | Covered exactly once. No duplicate. |
| JS Pre-Check | PASS | No `lock()` (JS-021). No `return null` (JS-002): method returns value tuple. No `async void` (JS-033). No `throw new XxxException` (JS-001). No mutable struct fields (JS-008). No `DateTime.Now`. No hardcoded hex. No `sealed` on TradeCopierWindow. No `FontFamily` on WPF. No `Account.All` outside Loaded. No `CreateOrder` without "PTT-" prefix. |
| CYC Pre-Check | PASS | After CYC = 5 (base(1) + foreach(+1) + if-null(+1) + if-combined-with-\|\|(+1) + ternary(+1) = 5). ≤ 8. Ticket CYC table states 5 explicitly. Conservative count — safe direction. |
| NT8 Check | PASS | `System.Array.IndexOf` is standard .NET, not an NT8 API. `Account` reference equality is documented safe in plan §8. No `AtmStrategyCreate`, no `AtmStrategyChangeStopTarget`, no `Account.Change()`, no `Account.All`. |
| Test Coverage | PASS | New method body introduced. `[Fact]` `BuildFollowerMultipliers_SignatureUnchanged_AfterContainsRefactor` present in `BwaveDwLaneBTests.cs`. Method is private — reflection-based test is the established project pattern. Asserts: NotNull, not static, 1 parameter of type `Account[]`, return type IsValueType (value tuple). |
| Scan Checklist | PASS | SCAN-01 through SCAN-07 all present. SCAN-04 explicitly states "BuildFollowerMultipliers CYC = 5, <= 8 PASS". SCAN-07 confirms old nested outer for-loop removed. |
| File Routing | PASS | `src/PropTraderTools/TradeCopierPanel.cs` and `src/PropTraderTools/Tests/BwaveDwLaneBTests.cs` — both in Wave workspace `c:\WSGTA\universal-or-strategy\src\PropTraderTools\`. |

**VERDICT: TICKET_REVIEW_PASS**

---

## T5 — TICKET B-5: Verify Tab Order in BuildRuleRow

**Type**: VERIFY-ONLY
**Spec Req IDs**: DW-C38-04
**Plan Section**: §5 TICKET B-5 / §4 row B-5

### Check Results

| Check | Result | Notes |
|-------|--------|-------|
| Traceability | PASS | DW-C38-04 maps to plan §5 B-5 exactly. DOM add-order table with 12 entries and line numbers matches plan §5 table. |
| Spec Coverage | PASS | Covered exactly once. No duplicate. |
| JS Pre-Check | PASS | No code changes. No violations possible. |
| CYC Pre-Check | PASS | No code changes. |
| NT8 Check | PASS | No NT8 API surface. |
| Test Coverage | PASS | Verify-only; no new methods; no [Fact] required. |
| Scan Checklist | PASS | SCAN-01 through SCAN-07 all present. SCAN-05 = N/A acceptable. |
| File Routing | PASS | No files modified. |

**VERDICT: TICKET_REVIEW_PASS**

---

## Aggregate Spec Coverage

| Plan §4 Row | Spec Req ID | Ticket | Status |
|-------------|-------------|--------|--------|
| B-1 (ACTIVE: delete 3 tests) | DW-C39-06, DW-LaneA-06 | B-1 | Covered exactly once |
| B-2 (VERIFY: BrushInactive) | DW-C39-09 | B-2 | Covered exactly once |
| B-3 (VERIFY: helpers extraction) | DW-C38-02 | B-3 | Covered exactly once |
| B-4 (ACTIVE: BuildFollowerMultipliers refactor) | DW-C39-07 | B-4 | Covered exactly once |
| B-5 (VERIFY: tab order) | DW-C38-04 | B-5 | Covered exactly once |

No missing spec coverage. No duplicate spec coverage. **PASS**

---

## Summary

| Ticket | Traceability | JS Pre-Check | CYC Pre-Check | NT8 Check | Test Coverage | Scan Checklist | File Routing | VERDICT |
|--------|-------------|-------------|---------------|-----------|---------------|----------------|-------------|---------|
| B-1 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| B-2 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| B-3 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |
| B-4 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** (V-001 RESOLVED) |
| B-5 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS |

---

## Overall: TICKET_REVIEW_PASS

All 5 tickets pass all 9 checks.
V-001 (missing first-match-wins guard in B-4) is confirmed resolved.
Safe to spawn ptt-engineer.
