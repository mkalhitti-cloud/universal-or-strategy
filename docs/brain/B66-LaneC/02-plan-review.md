# B66-LaneC Plan Review
**Reviewer**: ptt-plan-reviewer (Phase 2)
**Plan**: `docs/brain/B66-LaneC/02-architecture-plan.md`
**Spec**: DW-B64-01 partial (StopLimit drag-sync failures, Defects 1-3)
**Date**: 2026-08-12

---

## Checklist Results

### 1. NT8 Ground Truth Alignment

| Item | Verdict | Evidence |
|------|---------|---------|
| All 3 NT8 facts cited with exact source (file + line)? | PASS | Fact 1: `V12_002.Orders.Callbacks.Propagation.cs` line 209 + `CopyEngine.cs` line 1734. Fact 2: `NT8_FULL_REFERENCE.md` lines 898-899. Fact 3: `CopyEngine.cs` line 805. All three cite file + line. |
| Fact 1: StopLimit.LimitPrice==0 confirmed with V12_002 source citation? | PASS | Section 2 "Fact 1" cites `V12_002.Orders.Callbacks.Propagation.cs` line 209 with exact quoted text, confirmed by `CopyEngine.cs` line 1734 comment. |
| Fact 2: Account.Change() sets StopPrice, cited NT8_FULL_REFERENCE.md lines 898-899? | PASS | Section 2 "Fact 2" cites `docs/standards/NT8_FULL_REFERENCE.md` lines 898-899. Verified against source: line 898-899 reads "StopPriceChanged -- A double value representing the new stop price of an order. Used with Account.Change()". Exact match. |
| Fact 3: Dedup key=0.0 bug identified and explicitly scoped OUT with DW-B66-C-02? | PASS | Section 2 "Fact 3" identifies the dedup collision, states "do NOT fix in this block", and cross-references Section 4 (DW-B66-C-02) and `06-deferred-backlog.md`. |

### 2. Defect Coverage

| Item | Verdict | Evidence |
|------|---------|---------|
| Defect 1 -- Gate C: type guard widened to Limit OR StopLimit AND price read uses GetOrderPrice helper? | PASS | Section 3 "Defect 1" shows fixed Gate C with `(OrderType.Limit \|\| OrderType.StopLimit)` and `double currentPrice = GetOrderPrice(e.Order)`. Both conditions present in the fix code block. |
| Defect 2 -- FindFollowerEntryOrder: state widened to Working OR Accepted AND type widened to Limit OR StopLimit? | PASS | Section 3 "Defect 2" shows fixed `FindFollowerEntryOrder` with `(OrderState.Working \|\| OrderState.Accepted)` and `(OrderType.Limit \|\| OrderType.StopLimit)`. Both guards widened. |
| Defect 3 -- HandleEntryChange: rawPrice uses GetOrderPrice, currentPrice uses GetOrderPrice, SetFollowerPrice sets StopPrice for StopLimit? | PASS | Section 3 "Defect 3" explicitly maps all three fix lines: line 1007 -> `GetOrderPrice(leaderOrder)`, line 1024 -> `GetOrderPrice(fo)`, line 1030 -> `SetFollowerPrice(fo, newPrice)`. SetFollowerPrice implementation shown with `fo.StopPrice = newPrice` for StopLimit. |
| No mention of fixing DispatchCopy Gate 4/5 in this block? | PASS | Section 7 "Not changed" table explicitly lists "DispatchCopy Gate 4 (lines 797-801)" and "DispatchCopy Gate 5 (line 805)" with rationale "Deferred -- DW-B66-C-02". Section 1 summary also states DispatchCopy is deferred. |

### 3. CYC Budget

| Item | Verdict | Evidence |
|------|---------|---------|
| GetOrderPrice helper CYC <= 8 (expect CYC=2)? | PASS | Section 3 "Defect 1" and Section 5 table both state CYC=2. One ternary expression. |
| SetFollowerPrice helper CYC <= 8 (expect CYC=2)? | PASS | Section 3 "Defect 3" and Section 5 table both state CYC=2. One if/else branch. |
| Gate C post-change CYC <= 8 (expect CYC=3)? | PASS | Section 5 table: Gate C post-change CYC=3. Within limit. |
| FindFollowerEntryOrder post-change CYC <= 8 (expect CYC=3)? | PASS | Section 5 table states "3-5 (convention)". Plan documents both counting conventions: compound-predicate convention (mission brief baseline) = 3; McCabe strict (each `\|\|` = +1) = 5. Both values <= 8. The note is more precise than the checklist expectation; both values satisfy the <=8 gate. No violation. |
| HandleEntryChange post-change CYC <= 8 (expect CYC=6)? | PASS | Section 3 "Defect 3" and Section 5 table both state CYC=6 unchanged. Three line replacements are call sites with zero new branch points. |

### 4. Test Coverage

| Item | Verdict | Evidence |
|------|---------|---------|
| Exactly 8 tests specified: T_B66_C_01 through T_B66_C_08? | PASS | Section 6 defines exactly 8 subsections: T_B66_C_01 through T_B66_C_08. Each has a `[Fact]`-decorated method signature. Section 7 "Created" table lists `CopyEngineB66Tests.cs` with "8 xUnit [Fact] tests T_B66_C_01..T_B66_C_08". |
| T_B66_C_07 tests GetOrderPrice returns StopPrice for StopLimit? | PASS | Section 6 T_B66_C_07: "GetOrderPrice returns StopPrice for StopLimit (new)". Setup: `StopPrice = 4500.25`. Assert: `GetOrderPrice(order) == 4500.25`. Correct field verified. |
| T_B66_C_08 tests SetFollowerPrice sets fo.StopPrice for StopLimit? | PASS | Section 6 T_B66_C_08: "SetFollowerPrice sets StopPrice for StopLimit follower (new)". Setup: calls `SetFollowerPrice(fo, 4501.25)`. Asserts `fo.StopPrice == 4501.25` AND `fo.LimitPrice == 0.0` (unchanged). Correct field and non-mutation of LimitPrice both verified. |
| All tests are xUnit [Fact] (no NUnit, no MSTest)? | PASS | Section 6 header: "Framework: xUnit `[Fact]` only -- never NUnit or MSTest". All 8 method signatures decorated with `[Fact]` only. No `[Test]`, `[TestMethod]`, or `[Theory]` present. |
| Test file name and class name specified? | PASS | Section 6 header: File = `src/PropTraderTools/Tests/CopyEngineB66Tests.cs`, Class = `CopyEngineB66CTests`. Both specified. |

### 5. JS-DNA Rules (RULES_CATALOG.md)

| Rule | Item | Verdict | Evidence |
|------|------|---------|---------|
| JS-021 | No lock() in any new/modified code? | PASS | Section 8 compliance table: "All new code is pure conditional expressions and field reads/writes on Order objects. No synchronization primitives introduced. `_dedupCache` is ConcurrentDictionary (existing, unchanged)." No `lock()` in any fix code block. |
| JS-001 | No throw new in hot path? | PASS | Section 8: "No exception throws in any of the three defect fixes or two helper methods." All fix code blocks confirmed throw-free. |
| JS-002 | return null documented with null-guard obligation on callers? | PASS | Section 8: Plan states `FindFollowerEntryOrder` null return is via existing final `return null` at line 991, that the fix adds no new null-return path (it broadens the match predicate, reducing nulls), and that the "Existing XML comment documents the null-return contract (unchanged)." No new null-return obligation is introduced. The existing contract is documented; this is not a new violation. |
| ASCII-only | No Unicode in identifiers or string literals? | PASS | Section 8: "New identifiers: `GetOrderPrice`, `SetFollowerPrice`, `currentPrice`, `dedupPrice`. All ASCII. No string literals changed." |
| No async void | No async void non-event-handler? | PASS | Section 8: "Both new helpers are synchronous static methods. No async introduced." |

### 6. File Changeset Completeness

| Item | Verdict | Evidence |
|------|---------|---------|
| CopyEngine.cs listed with specific lines? | PASS | Section 7 "Modified" table lists 8 rows for `src/PropTraderTools/CopyEngine.cs`, each with precise line numbers (669-670, 673, 986-988, 1007, 1024, 1030, "new after 1039", "new after GetOrderPrice"). |
| CopyEngineB66Tests.cs listed as CREATE? | PASS | Section 7 "Created" table lists `src/PropTraderTools/Tests/CopyEngineB66Tests.cs` with purpose "8 xUnit [Fact] tests T_B66_C_01..T_B66_C_08". |
| DispatchCopy Gate 4/5 explicitly listed as NOT changed (with rationale)? | PASS | Section 7 "Not changed" table: two explicit rows -- "DispatchCopy Gate 4 (lines 797-801)" and "DispatchCopy Gate 5 (line 805)" -- both with rationale "Deferred -- DW-B66-C-02". |

### 7. Deferred Item DW-B66-C-02

| Item | Verdict | Evidence |
|------|---------|---------|
| Priority P1? | PASS | Section 1 summary: "DW-B66-C-02 (P1)". Section 4 header: "**Priority**: P1". Consistent. |
| Target B67+? | PASS | Section 1 summary: "deferred to B67+". Section 4 header: "**Target block**: B67+". Consistent. |
| Root cause documented? | PASS | Section 4: root cause is `order.LimitPrice` as dedup key in `IsDedup`; since StopLimit.LimitPrice == 0 always, all StopLimit entries share dedup key 0.0. First entry dispatches; subsequent entries are wrongly blocked. Precise and traceable to Fact 1. |
| Fix approach documented? | PASS | Section 4 provides the exact replacement code for line 805 (inline `dedupPrice` local or `GetDedupPrice(Order)` helper), with rationale for which to choose based on DispatchCopy CYC. |
| Defer rationale documented? | PASS | Section 4: "Scope creep risk (AGENTS.md Section 11). The `IsDedup` signature and `DispatchCopy` Gate 5 intersect ALL copy paths (Market, Limit, StopLimit, StopMarket). Changing them risks regressions in tested Limit and Market paths." Explicit blast-radius justification provided. |

---

## Violation Summary

No violations found. All 7 sections pass all items.

---

## Final Verdict

**REVIEW_PASS**

The plan is complete, precise, and compliant. All NT8 ground truth is cited from primary sources with exact file + line. The three defect fixes are fully specified with code. CYC budget is within ≤ 8 for all methods under both counting conventions. Eight xUnit [Fact] tests cover regressions and new cases including helpers. JS-DNA rules JS-021, JS-001, JS-002, ASCII-only, and no-async-void are all satisfied. File changeset is complete with specific line numbers and explicit NOT-changed entries for deferred scope. DW-B66-C-02 is defined with priority, target block, root cause, fix approach, and defer rationale.

**Unlocks**: Phase 3 — ticket generation (`04-tickets.md`).
