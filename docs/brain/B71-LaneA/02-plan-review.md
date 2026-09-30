# B71-LaneA Plan Review

**Block**: B71-LaneA
**Epic**: Quick ALL Follower Bracket Dispatch + QX Guard
**Reviewer**: ptt-plan-reviewer
**Date**: 2026-08-13
**Phase**: 2 (Plan Review)
**Plan File**: docs/brain/B71-LaneA/02-architecture-plan.md

---

## Verdict: REVIEW_PASS

Zero P0 violations. Zero P1 violations. All 12 checklist items PASS. All spec requirements addressed.

---

## Section A: Rules Catalog Scope Note

The active RULES_CATALOG.md (`docs/standards/jane-street/RULES_CATALOG.md`) contains 41 rules
across three categories: Type Safety (JS-001..020), Concurrency (JS-021..035), and Performance
(JS-036..041). The catalog is truncated — categories Testing (JS-051..065), Code Review
(JS-066..080), Serialization (JS-081..095), and Philosophy (JS-096..110) referenced in AGENTS.md
are not present in the file as of 2026-08-13. Review is applied against all 41 present rules.

The CYC ≤ 8 constraint cited as "JS-041" in the plan's own inline comments is a project DNA
rule (AGENTS.md Section 3.5, COMPLEXITY_REDUCTION_PROTOCOL.md). In this catalog, JS-041 is
`StructLayout for Cache-Friendly Data` (Performance/P1). The CYC ≤ 8 rule is enforced here as a
project mandate, not via a catalog rule ID. No rule-ID citation error created a compliance gap
in the plan's substance; the enforcement constraint is correct.

---

## Section B: Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| FIX 1 (DW-B71-01): Add `OrderState.Submitted` to `CancelQxBrackets` stateOk gate | YES | §3.1 |
| FIX 2 (DW-B71-02): Add `bool skipIfFollower = true` parameter to `PttQuickExit.Execute` | YES | §3.2 |
| FIX 2: Follower guard block inserted after flat guard (before Step 2) | YES | §3.2 |
| FIX 3 (DW-B71-04): Remove `CancelQxBracketsForFollowers` call from `PttGlobalQuickExit.Execute` | YES | §3.3.B(a) |
| FIX 3: Add follower dispatch loop to `PttGlobalQuickExit.Execute` | YES | §3.3.B(b) |
| FIX 3: Update `ExecuteOne` signature to accept `skipIfFollower` and forward it | YES | §3.3.B(c) |
| CRITICAL: Change `FindRule` from `private` to `internal` in `CopyEngine.cs` | YES | §3.3.A |
| 10 xUnit tests T_B71_01..T_B71_10, all planned | YES | §4 |
| CancelQxBracketsForFollowers removal rationale documented | YES | §3.3.B(a) + §2 Fact 6 |
| ExecuteOne signature change documented with `skipIfFollower` forwarding | YES | §3.3.B(c) |
| All prior DW carry-forward items documented | YES | §1 (carry-forward table) |

All 11 spec requirements are covered. No gaps found.

---

## Section C: 12-Point Checklist

### S1: ASCII-Only Compliance

**Status**: PASS

All new string literals planned in B71-LaneA scope are ASCII-only:
- `"PTT-QX: follower guard -- skip "` — ASCII (plan §3.2, SCAN-01)
- `"NULL"` — ASCII
- All comment text — ASCII
- `"B71 DW-B71-02: ..."` (comment) — ASCII
- `"B71 DW-B71-04: ..."` (comment) — ASCII

Plan correctly notes pre-existing non-ASCII at CopyEngine.cs lines 398, 499, ~1449-1450 are
out-of-scope and must not be touched (tracked as PRE-EXISTING-01/02 in deferred backlog).
The plan's SCAN-01 engineer action (grep for non-ASCII in modified lines) is correctly scoped.

No JS-001-adjacently-named ASCII rule exists in the present catalog (JS-001 is about
Result\<T,E\>); the ASCII mandate comes from AGENTS.md §2. No violation.

---

### S2: NT8 API Validity

**Status**: PASS

All NT8 API claims verified against `docs/standards/NT8_FULL_REFERENCE.md`:

| Claim in Plan | NT8_FULL_REFERENCE Evidence | Verified? |
|---------------|---------------------------|-----------|
| `OrderState.Submitted` is a valid enum value | Line 936: "OrderState.Submitted — Order is submitted to the broker" | YES |
| `Account.Cancel()` exists | Line 318: "Cancel() — Cancels specified order(s) on the account" | YES |
| `Account.Cancel()` accepts pre-execution-state orders | NT8 docs place no OrderState restriction on Cancel(); plan notes try/catch pattern is correct for broker-level rejection | YES (by absence of restriction) |
| `IsFollowerAccount` is internal on CopyEngine | Plan §2 Fact 5: verified in source at line 409 | GROUNDED IN SOURCE |
| `CopyRule.FollowerAccounts` is `Account[]` internal readonly | Plan §2 Fact 4: verified in source at line 181 | GROUNDED IN SOURCE |
| `FindRule` is `private CopyRule?` at line 1750 | Plan §2 Fact 3: verified in source | GROUNDED IN SOURCE |

No phantom API usage. No `Account.All` used in a constructor (NT8 constraint — it is used in
`Execute()`, an event-handler path called after initialization). No `AtmStrategyCreate` (that
is StrategyBase-only; not called here). PASS.

---

### S3: All 10 xUnit Tests Present

**Status**: PASS

All 10 tests T_B71_01..T_B71_10 are present in plan §4 with assertion strategies.

| Test | Method Under Test | Assertion Strategy | Present? |
|------|------------------|-------------------|----------|
| T_B71_01 | `CancelQxBrackets` | `OrderState.Submitted` order is included in `acc.Cancel()` | YES |
| T_B71_02 | `CancelQxBrackets` | `OrderState.Working` still cancelled (regression) | YES |
| T_B71_03 | `CancelQxBrackets` | `OrderState.Accepted` still cancelled (regression) | YES |
| T_B71_04 | `CancelQxBrackets` | `OrderState.Filled` order is NOT included, cancel not called | YES |
| T_B71_05 | `PttQuickExit.Execute` | `skipIfFollower=true` + follower → early return, zero orders created | YES |
| T_B71_06 | `PttQuickExit.Execute` | `skipIfFollower=false` → guard skipped, CancelQxBrackets called | YES |
| T_B71_07 | `PttQuickExit.Execute` | Log message contains "PTT-QX: follower guard -- skip Sim102" | YES |
| T_B71_08 | `PttGlobalQuickExit.Execute` | ExecuteOne called for leader (skipIfFollower=true default) | YES |
| T_B71_09 | `PttGlobalQuickExit.Execute` | ExecuteOne called for 2 followers with skipIfFollower=false | YES |
| T_B71_10 | `PttGlobalQuickExit.Execute` + `PttQuickExit.Execute` | Flat follower: ExecuteOne called but zero orders created | YES |

Framework: xUnit `[Fact]` (mandatory per TEST_FRAMEWORK_PROTOCOL.md). No `[Theory]` used
(appropriate — inputs are fixed per test). PASS.

---

### S4: lock() Ban (JS-021)

**Status**: PASS

No `lock()` appears in any new or modified code planned for B71-LaneA. All new code operates
synchronously on the UI thread via:
- Read-only iteration over `FollowerAccounts` (immutable array set at construction time)
- Instance method calls on CopyEngine singleton
- No shared mutable state mutations

Plan §SCAN-04 provides correct grep commands to confirm at implementation time. No JS-021
violation.

---

### S5: throw / return null ban (JS-001, JS-002)

**Status**: PASS

No `throw new XxxException()` in any hot path code in the plan. New code introduces only:
- `return;` (early exit)
- `NinjaTrader.Code.Output.Process(...)` (logging)
- `continue;` (loop skip)
- `executor.Execute(...)` (delegation)

The existing `try { acc.Cancel(...); } catch { }` is a pre-existing fire-and-forget pattern
(intentional: NT8 broker cancel may throw on state transition). B71 does not modify this block
and introduces no new throw or catch blocks.

`CopyRule? FindRule(...)` returns a nullable — plan correctly uses `if (rule != null)` guard
rather than returning null where a value is expected. The null-safe pattern `engine?.FindRule()`
with null-propagation is consistent with the existing codebase pattern. No JS-002 violation
(nullable return is appropriate here; the caller guards).

---

### S6: CYC ≤ 8 Compliance

**Status**: PASS

| Method | File | CYC Before | CYC After | At Limit? | Status |
|--------|------|-----------|-----------|-----------|--------|
| `CancelQxBrackets` | CopyEngine.cs | ~6 | ~6 | No | PASS |
| `PttQuickExit.Execute` | PttQuickExit.cs | 6 | 7 | No | PASS |
| `PttGlobalQuickExit.Execute` | PttGlobalQuickExit.cs | 6 | 8 | YES (limit) | PASS |
| `ExecuteOne` | PttGlobalQuickExit.cs | 1 | 1 | No | PASS |
| `FindRule` | CopyEngine.cs | 3 | 3 | No | PASS (body unchanged) |

**CYC accounting for `PttGlobalQuickExit.Execute` (CYC = 8)** — plan's node-count is verified:
1. `foreach (Account acc in Account.All)` — loop head
2. `if (engine != null && engine.IsFollowerAccount(acc)) continue` — branch
3. `foreach (Position pos in acc.Positions)` — inner loop head
4. `if (pos == null || pos.Quantity == 0) continue` — branch
5. `var rule = engine?.FindRule(...)` — null-propagation (counts as branch in Roslyn CFG)
6. `if (rule != null)` — explicit null check
7. `foreach (var follower in rule.Value.FollowerAccounts)` — inner-inner loop
8. `if (follower == null) continue` — branch

Net CYC = 8. Exactly at project DNA limit. PASS per spec ("CYC 6->8, at limit, PASS").

Plan provides a contingency: if complexity_audit.py reports 9+, the follower loop must be
extracted to a helper. This is the correct fallback. PASS.

---

### S7: NT8 Account.Cancel accepts Submitted-state orders

**Status**: PASS

Documented in plan §2 Fact 2 and SCAN-07 table:
- NT8_FULL_REFERENCE.md lines 318-319 confirms `Cancel()` method exists with no documented
  OrderState restriction.
- Plan correctly notes the existing `try { ... } catch { }` wrapper handles any broker-level
  rejection for orders already transitioning.

---

### FindRule private→internal change documented

**Status**: PASS

Fully documented in plan §3.3.A with:
- File and line citation (`CopyEngine.cs` line 1750)
- Before/after code snippet
- Rationale: `PttGlobalQuickExit` is in the same assembly (`PropTraderTools`) but a different
  class; `private` blocks cross-class access; `internal` is the minimal promotion.
- Confirmation that existing callers (lines 510, 1731, 1934) are all inside `CopyEngine` and
  continue to work unchanged.

---

### CancelQxBracketsForFollowers removal rationale documented

**Status**: PASS

Documented in plan §3.3.B(a) and §2 Fact 6:
- Removal is justified because the follower dispatch loop (Fix 3b) now calls
  `ExecuteOne(follower, ...)` which internally calls `PttQuickExit.Execute` → Step 3 →
  `CancelQxBrackets(follower, instr)` directly.
- The original explicit `CancelQxBracketsForFollowers` call becomes redundant.
- A second cancel pass (DW-B71-03) is acknowledged as functionally harmless (NT8 no-ops on
  already-cancelled orders) and tracked as P2 deferred work.

---

### All prior DW carry-forward items documented

**Status**: PASS

Plan §1 carry-forward table lists all 10 open items from prior blocks:

| ID | Source | Status |
|----|--------|--------|
| DW-B66-C-02 | B66-LaneC | OPEN (carry-forward) |
| DW-B66-BE-01 | B66-LaneC | OPEN (carry-forward) |
| DW-B63-01 | B63 | OPEN (carry-forward) |
| DW-B54-01 | B54 | OPEN BLOCKED (carry-forward) |
| DW-B58-01 | B58 | OPEN (carry-forward) |
| DW-B58-02 | B58 | OPEN (carry-forward) |
| DW-B58-03 | B58 | OPEN (carry-forward) |
| PRE-EXISTING-01 | CopyEngine.cs | OPEN (carry-forward) |
| PRE-EXISTING-02 | CopyEngine.cs | OPEN (carry-forward) |
| PRE-EXISTING-03 | deploy-sync | OPEN (carry-forward) |

Plan §1 explicitly states zero B66-LaneC deferred items are closed by B71-LaneA. PASS.

---

### ExecuteOne signature change documented

**Status**: PASS

Documented in plan §3.3.B(c) with:
- Before/after method signature
- Default value `bool skipIfFollower = true` so existing `ExecuteOne(acc, ...)` call sites
  compile without change
- Forward of `skipIfFollower` parameter to `executor.Execute(acc, instr, t1Ticks, t2Ticks, skipIfFollower)`
- CYC impact note: `ExecuteOne` stays at CYC 1 (no branches added)

---

### No scope creep beyond the 3 fixes

**Status**: PASS

B71-LaneA touches exactly:
- `CopyEngine.cs`: line 460-463 (Submitted state) + line 1750 (FindRule visibility)
- `PttQuickExit.cs`: line 33 (signature) + new guard block after line 46
- `PttGlobalQuickExit.cs`: lines 28-65 (remove call, add loop, update ExecuteOne)
- `tests/PropTraderTools.Tests/CopyEngineB71Tests.cs`: new test file (10 tests)

No other files are modified. The new DW-B71-03 item (double-cancel awareness) is correctly
deferred to B72+, not silently patched in this block. PASS.

---

## Section D: Additional Observations (non-blocking)

**Observation 1 — DW-B71-03 deferred double-cancel is correctly handled**

Plan §6 acknowledges that `PttQuickExit.Execute` line 54 calling
`CancelQxBracketsForFollowers(instr)` when `leader` is actually a follower account results in a
redundant cancel pass. This is functionally safe (NT8 no-ops on already-cancelled orders) and
is correctly flagged as P2 (not blocking). The plan defers to B72+ and documents the fix
approach. No action required here.

**Observation 2 — CYC counting uses correct Roslyn CFG methodology**

The plan's CYC accounting for `PttGlobalQuickExit.Execute` (CYC = 8) correctly counts the
null-propagation operator `?.` as a branch. This is consistent with how Roslyn's cyclomatic
complexity analyzer counts `?.` chains. The contingency fallback (extract if audit reports 9+)
is the correct defensive measure.

**Observation 3 — `Account.All` usage in `Execute()` is safe**

The plan's comment `/// NT8-021: Account.All safe -- called from UI thread after Loaded` is
correct and matches NT8 AddOn lifecycle constraints. `Account.All` is only accessed in the
`Execute()` method body, not in a constructor or during initialization. PASS.

---

## Section E: Verdict Summary

| Checklist Item | Result | Notes |
|---------------|--------|-------|
| S1: ASCII-only | PASS | All new strings confirmed ASCII |
| S2: NT8 API validity | PASS | All claims verified against NT8_FULL_REFERENCE.md |
| S3: All 10 tests present | PASS | T_B71_01..T_B71_10 with assertion strategies |
| S4: lock() ban (JS-021) | PASS | No lock() in any planned code |
| S5: throw/return-null ban (JS-001, JS-002) | PASS | No throw in hot paths; nullable handled correctly |
| S6: CYC ≤ 8 | PASS | Max CYC = 8 (Execute, at limit) |
| S7: Account.Cancel/Submitted documented | PASS | NT8_FULL_REFERENCE.md lines 318-319, 936-937 |
| FindRule private→internal documented | PASS | §3.3.A with full rationale |
| CancelQxBracketsForFollowers removal documented | PASS | §3.3.B(a) + §2 Fact 6 |
| Prior DW carry-forward documented | PASS | §1 table, 10 items |
| ExecuteOne signature change documented | PASS | §3.3.B(c) with default value |
| No scope creep | PASS | Exactly 3 source files + 1 new test file |

**P0 violations**: 0
**P1 violations**: 0
**P2 observations**: 2 (non-blocking, documented above)

---

## REVIEW_PASS

Plan is approved for Phase 3 (ticket generation). Architect may proceed to `04-tickets.md`.
