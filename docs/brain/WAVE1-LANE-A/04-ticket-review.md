# WAVE1-LANE-A Ticket Review

**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Date**: 2026-09-07
**Input tickets**: `docs/brain/WAVE1-LANE-A/04-tickets.md`
**Input plan**: `docs/brain/WAVE1-LANE-A/02-architecture-plan.md`
**Input plan review**: `docs/brain/WAVE1-LANE-A/02-plan-review.md` (REVIEW_PASS — Cycle 2)
**Target file**: `src/PropTraderTools/CopyEngine.cs` (class `TrimSignal`)

---

## Verdict: TICKET_REVIEW_PASS

---

## Ticket 1 Review — RegisterBeRetrySlotIfNeeded

| Check | Result | Notes |
|-------|--------|-------|
| **Atomicity** | PASS | T1 touches exactly `RegisterBeRetrySlotIfNeeded` (A-01) plus its two helpers `IsBeRetrySlotNeeded` and `RegisterPendingBeSlot`. No scope creep beyond A-01. |
| **Traceability — Spec ID** | PASS | `WAVE1-LANE-A-01` present on line 29 of tickets. |
| **Traceability — Plan Section** | PASS | Helper names, CCN values, line ranges, and ordering constraints all match plan Section 4 (A-01 analysis, plan lines 100–151). |
| **CCN Pre-Check (JS-080)** | PASS | `IsBeRetrySlotNeeded` CCN=4 (base+3 `&&` — derivation shown). `RegisterPendingBeSlot` CCN=1 (base only — derivation shown). Parent post-extraction CCN=6. All ≤ 8. |
| **SCAN-01 (lock-free)** | PASS | Present. `IsBeRetrySlotNeeded` is pure static. `RegisterPendingBeSlot` uses ConcurrentDictionary indexer (lock-free). Zero `lock()` added. |
| **SCAN-02 (async void)** | PASS | Present. Both helpers are synchronous. |
| **SCAN-03 (return null)** | PASS | Present. `IsBeRetrySlotNeeded` returns `bool`; `RegisterPendingBeSlot` returns `void`. Neither can return null. |
| **SCAN-04 (CCN ≤ 8)** | PASS | Present. Exact lizard commands specified with expected per-method CCN values. |
| **SCAN-05 (PTT- prefix)** | PASS | Present. Explicitly states no `CreateOrder` calls added or modified in T1 scope. |
| **SCAN-06 (ASCII-only)** | PASS | Present. Specific string literals `"[BE-DIAG] "`, `"registered BE retry slot"`, `"delayMs="` enumerated. |
| **SCAN-07 (public visibility)** | PASS | Present. Both helpers declared `private`/`private static`. |
| **NT8 Constraint — Ordering** | PASS | `RegisterPendingBeSlot` step ordering (1-write, 2-log, 3-timer) documented with CRITICAL heading and race-condition rationale. This directly satisfies JS-096 as flagged in plan review. |
| **Test Specificity — IsBeRetrySlotNeeded** | PASS | 6 named `[Fact]` tests. Each has exact input tuple, exact expected bool, and a one-line explanation of which `&&` short-circuit fires. |
| **Test Specificity — RegisterPendingBeSlot** | PASS | 2 named `[Fact]` tests: `RegisterPendingBeSlot_SlotWrittenWithCorrectKeys_WhenBothDelayVariants` and `RegisterPendingBeSlot_DefaultDelayMs_Is500`. This was the VIOLATION-01 point in Cycle 1 plan review — now resolved in both plan and ticket. |
| **Test Framework** | PASS | xUnit `[Fact]` throughout. Explicit prohibition of NUnit/MSTest stated. |
| **Acceptance Criterion — Build** | PASS | `dotnet build src/PropTraderTools/PropTraderTools.csproj` stated. |
| **Acceptance Criterion — Tests** | PASS | `dotnet test` with count "all 8 new [Fact] tests PASS" stated. |
| **Acceptance Criterion — CCN** | PASS | Lizard commands with per-method CCN targets. |
| **Acceptance Criterion — Signature Stability** | PASS | `grep -n "private void RegisterBeRetrySlotIfNeeded"` stated as stability gate (caller A-04 must not break). |
| **File Routing** | PASS | `src/PropTraderTools/CopyEngine.cs` — Wave workspace (`c:\WSGTA\universal-or-strategy`). Test file `tests/PropTraderTools.Tests/Core/CopyEngineTests.cs` — Wave workspace. |

**Advisory (non-blocking):**
`RegisterPendingBeSlot_DefaultDelayMs_Is500` validates the default parameter via reflection. This tests the contract of the default value (guard against silent 500→200 drift) rather than runtime behavior. Accepted: the intent is legitimate, and the plan review (Cycle 2) explicitly accepted this inline-mirror pattern for NT8-coupled void methods. No FAIL triggered.

**VERDICT: TICKET_REVIEW_PASS**

---

## Ticket 2 Review — FlattenOneAccountLimit + TrimOneAccountLimit

| Check | Result | Notes |
|-------|--------|-------|
| **Atomicity** | PASS | T2 touches A-07 (`FlattenOneAccountLimit`) and A-08 (`TrimOneAccountLimit`) via a single shared helper `SubmitLimitExitOrder`. The plan explicitly models A-07+A-08 as a single ticket due to their shared helper dependency (plan Section 2, sequencing constraint). |
| **Traceability — Spec IDs** | PASS | `WAVE1-LANE-A-07` and `WAVE1-LANE-A-08` both present on lines 279–280 of tickets. |
| **Traceability — Plan Section** | PASS | Helper name, signature, CCN derivation, and what-stays-in-each-parent all match plan Section 4 (A-07 and A-08 analyses). |
| **CCN Pre-Check (JS-080)** | PASS | `SubmitLimitExitOrder` CCN=4 (base+null-check+try+catch — derivation shown). Both parents post-extraction CCN=4. All ≤ 8. |
| **SCAN-01 (lock-free)** | PASS | Present. `acc.CreateOrder` and `acc.Submit` are NT8 API calls — no `lock()`. Zero lock added. |
| **SCAN-02 (async void)** | PASS | Present. All three methods synchronous `void`. |
| **SCAN-03 (return null)** | PASS | Present. All three methods return `void`. |
| **SCAN-04 (CCN ≤ 8)** | PASS | Present. Lizard command with grep filter for all three method names, expected CCN=4 each. |
| **SCAN-05 (PTT- prefix)** | PASS | Present. Explicitly states both call sites pass `"PTT-FlattenLimit"` and `"PTT-TrimLimit"`. Grep verification command specified in acceptance criteria. |
| **SCAN-06 (ASCII-only)** | PASS | Present. String literals `": CreateOrder returned null"` and `" error: "` enumerated. |
| **SCAN-07 (public visibility)** | PASS | Present. `SubmitLimitExitOrder` is `private`. Neither parent changes visibility. |
| **NT8 Constraint — arg12 cast** | PASS | `(NinjaTrader.Cbi.CustomOrder)null` for arg12 documented with CRITICAL NT8-007 label in both body description and extraction code block. Acceptance criterion includes `grep -n "(NinjaTrader.Cbi.CustomOrder)null"` as a verification gate. |
| **Test Specificity** | PASS | 4 named `[Fact]` tests: `SubmitLimitExitOrder_UsesSellAction_WhenPositionIsLong`, `SubmitLimitExitOrder_UsesBuyToCoverAction_WhenPositionIsShort`, `FlattenOneAccountLimit_UsesFullPositionQty_WhenCalled`, `TrimOneAccountLimit_UsesHalfPositionQty_WhenCalled`. Each has exact input values and expected results. Ceiling arithmetic `(int)Math.Ceiling(5 / 2.0) == 3` is explicitly demonstrated. |
| **Test Framework** | PASS | xUnit `[Fact]` throughout. |
| **Acceptance Criterion — Build** | PASS | `dotnet build` stated. |
| **Acceptance Criterion — Tests** | PASS | `dotnet test` with count "all 4 new [Fact] tests PASS" stated. |
| **Acceptance Criterion — CCN** | PASS | Lizard commands with per-method targets. |
| **Acceptance Criterion — NT8-007 guard** | PASS | `grep -n "(NinjaTrader.Cbi.CustomOrder)null"` stated as a post-extraction verification gate. |
| **File Routing** | PASS | `src/PropTraderTools/CopyEngine.cs` — Wave workspace. |

**VERDICT: TICKET_REVIEW_PASS**

---

## Ticket 3 Review — OnOrderUpdate (Advisory)

> T3 is marked ADVISORY in the ticket header. It is executed only on explicit wave-sponsor direction.
> All checks below are evaluated as if T3 were binding (per role mandate: advisory items that ARE executed
> must meet all standards). The advisory tier does not relax any requirement.

| Check | Result | Notes |
|-------|--------|-------|
| **Atomicity** | PASS | T3 touches exactly `OnOrderUpdate` (A-09) and its single helper `TryResolveEnabledRule`. No additional scope. |
| **Traceability — Spec ID** | PASS | `WAVE1-LANE-A-09` present on line 541 of tickets. |
| **Traceability — Plan Section** | PASS | Helper name, signature, gate ordering, CCN derivation, and `.Value` cleanup steps match plan Section 4 (A-09 analysis). |
| **CCN Pre-Check (JS-080)** | PASS | `TryResolveEnabledRule` CCN=4 (base+Gate1+Gate2+Gate3 — derivation shown). `OnOrderUpdate` post-extraction CCN=5. Both ≤ 8. |
| **SCAN-01 (lock-free)** | PASS | Present. `_isCopyEnabled` is a bool field (atomic read on x64, no lock needed). `FindMatchingRule` is an existing method with no lock. |
| **SCAN-02 (async void)** | PASS | Present. `TryResolveEnabledRule` is synchronous `bool`. `OnOrderUpdate` return type unchanged. |
| **SCAN-03 (return null)** | PASS | Present. Returns `bool`. `out CopyRule` is a value-type struct — cannot be null. `return false` ≠ `return null`. |
| **SCAN-04 (CCN ≤ 8)** | PASS | Present. Lizard commands with targets: `OnOrderUpdate` CCN=5, `TryResolveEnabledRule` CCN=4. |
| **SCAN-05 (PTT- prefix)** | PASS | Present. Explicitly states no `CreateOrder` calls added or modified in T3 scope. |
| **SCAN-06 (ASCII-only)** | PASS | Present. States no new string literals in `TryResolveEnabledRule`. Existing comments preserved. |
| **SCAN-07 (public visibility)** | PASS | Present. `TryResolveEnabledRule` is `private`. `OnOrderUpdate` visibility unchanged. |
| **NT8 Constraint — Gate Ordering** | PASS | Body description documents gate ordering as a correctness constraint with CRITICAL label: Gate 1 (enabled) → Gate 2 (null) → Gate 3 (.Value.Enabled). Rationale: calling `.Value.Enabled` on a null `CopyRule?` is undefined behavior. Ordering is a compiler-safety constraint. |
| **Test Specificity** | PASS | 4 named `[Fact]` tests: `TryResolveEnabledRule_ReturnsFalse_WhenCopyDisabled`, `TryResolveEnabledRule_ReturnsFalse_WhenNoMatchingRule`, `TryResolveEnabledRule_ReturnsFalse_WhenRuleIsDisabled`, `TryResolveEnabledRule_ReturnsTrue_WhenAllGatesPass`. Each gate has an independent test. Preconditions, inputs, and expected results are explicit. |
| **Test Framework** | PASS | xUnit `[Fact]` throughout. |
| **Acceptance Criterion — Build** | PASS | `dotnet build` stated. |
| **Acceptance Criterion — Tests** | PASS | `dotnet test` with count "all 4 new [Fact] tests PASS" stated. |
| **Acceptance Criterion — CCN** | PASS | Lizard commands with per-method targets. |
| **Acceptance Criterion — .Value cleanup** | PASS | `grep -n "matchedRule.Value"` stated as a post-extraction zero-hit gate — verifies all five `.Value` call sites were updated. |
| **File Routing** | PASS | `src/PropTraderTools/CopyEngine.cs` — Wave workspace. |

**VERDICT: TICKET_REVIEW_PASS**

---

## Violations

**None.** No TICKET_REVIEW_FAIL conditions were found across any ticket.

---

## Cross-Ticket Checks

| Check | Result | Notes |
|-------|--------|-------|
| **Spec Coverage — aggregate** | PASS | T1 covers A-01. T2 covers A-07+A-08. T3 covers A-09. Methods A-02, A-03, A-04, A-05, A-06, A-10 were classified NO EXTRACTION WARRANTED in the plan (compliant CCN); they correctly appear in no ticket. |
| **No phantom work** | PASS | All ticket items trace directly to plan sections. No work appears in tickets that is absent from the plan. |
| **No missing plan work** | PASS | All AT-LIMIT plan items (A-01, A-07, A-08, A-09) have corresponding tickets. COMPLIANT items (A-02, A-03, A-04, A-05, A-06, A-10) correctly have no tickets. |
| **Duplicate coverage** | PASS | No spec requirement ID appears in more than one ticket. |
| **Out-of-scope CCN violations** | PASS | `IsExitSignalName` (CCN=9) and `HasArmingAtmBrackets` (CCN=9) are correctly excluded from ticket scope and flagged in the Global Acceptance Gate section for a separate epic. This matches plan Section 3 and plan review Out-of-Scope table. |
| **Execution order constraint** | PASS | T1 and T2 declared independent (no merge conflict risk). T3 declared after T1+T2 (OnOrderUpdate is adjacent to active lines — merge risk documented). |
| **Global Acceptance Gate present** | PASS | A global gate section after T3 specifies build, lizard CCN sweep, full test run, P0 scans, NT8 sync, and F5 compile gate. |

---

## Summary

All three tickets pass every check in the Phase 3.5 review mandate:

- **Atomicity**: Each ticket is scoped to exactly its named method(s).
- **Traceability**: Every item maps to a spec requirement ID and a plan section.
- **Spec coverage**: Complete — no uncovered requirements, no phantom work.
- **CCN Pre-Check (JS-080)**: All helpers and post-extraction parents state CCN ≤ 8 with derivation.
- **7-Scan Checklist**: SCAN-01 through SCAN-07 present in every ticket (defense-in-depth contract preserved).
- **NT8 Constraints**: arg12 cast (T2) and gate ordering (T3) documented with CRITICAL labels.
- **Test Coverage**: Every new method has ≥ 1 named `[Fact]` with specific inputs and expected outputs.
  `RegisterPendingBeSlot` (the Cycle 1 VIOLATION-01 method) now has 2 named `[Fact]`s.
- **xUnit Only**: No NUnit or MSTest references anywhere in the tickets.
- **Acceptance Criteria**: Build, test, CCN, and P0 scan commands stated per ticket and in the global gate.
- **File Routing**: All C# paths point to the Wave workspace (`src/PropTraderTools/`, `tests/PropTraderTools.Tests/`).

---

## Final Verdict: TICKET_REVIEW_PASS
