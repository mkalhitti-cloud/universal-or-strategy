# B133 LaneA Phase 2 — Plan Review
**Reviewer**: ptt-plan-reviewer
**Date**: 2026-08-21
**Plan file**: `docs/brain/B133/LaneA-02-architecture-plan.md`
**Spec**: DW-B142 P0 (SignalOrNameMatches null==null false-positive)

---

## VERDICT: REVIEW_FAIL

**3 checks failed. Plan must be revised before Phase 3 unlocks.**

---

## Per-Check Results

| Check | Result | Evidence |
|-------|--------|----------|
| R-01 | PASS | Section 1 present; root cause paragraph fully explains `null==null` false-positive path through `FindFollowerBracketOrder` → `SyncFollowerBracket` → `acc.Cancel(Target1)` OCO cascade |
| R-02 | PASS | Section 1 + Section 3 describe exactly one-line null-guard addition; before/after diff shown; no refactor scope |
| R-03 | PASS | Section 2 "Files Touched" lists only `CopyEngine.cs` + `B133Tests.cs`; "Any other .cs file" explicitly listed as NOT touched |
| R-04 | PASS | Section 2 "Files NOT Touched" explicitly names `FindFollowerBracketOrder (L2524-2570)` and `SyncFollowerBracket (L2187)` with reasons |
| R-05 | PASS | Spec does not enumerate method names; plan provides 5 named [Fact] methods in Section 4 table covering the full defect scenario (null/null false-positive, ATM fallback match, ATM fallback no-match, strategy regression, double-null no-match) |
| R-06 | PASS | Section 4 "Mock/Stub Strategy" addresses NT8 `Order` sealed-class concern: confirms `Order` is NOT sealed in test assembly, documents `StubOrder()` direct-instantiation pattern, explains that only `Name` and `FromEntrySignal` are accessed |
| R-07 | PASS | Section 3 states "CYC of `SignalOrNameMatches` remains 3 after the fix"; Section 6 repeats "CYC stays at 3 (unchanged)"; reasoning given (short-circuit is not a new CFG branch node) |
| R-08 | PASS | Section 4 "Regression Strategy" table covers all four suites with exact counts: B132(5), B131(7), B130(8), B129(13) |
| R-09 | PASS | Section 5 table contains all 7 scans with exact commands: SCAN-01 through SCAN-07 |
| **R-10** | **FAIL** | Plan does not contain an explicit architectural statement that the fix introduces no new `lock()`, `throw new`, `return null`, or `async void`. The 7-scan section is a post-implementation verification checklist, not a plan-level declaration. Required: add an explicit sentence in Section 3 or Section 6 such as: "This fix introduces no new `lock()`, `throw new`, `return null`, or `async void` constructs." |
| **R-11** | **FAIL** | Plan does not explicitly state that all new identifiers introduced (`signalName` guard, `B133LaneATests`, `StubOrder`, test method names) are ASCII-only. SCAN-06 is a post-implementation check; the plan must carry an affirmative architectural statement. Required: add explicit ASCII-only confirmation for new identifiers in Section 3 or Section 6. |
| **R-12** | **FAIL** | Plan does not acknowledge that `CreateOrder` is N/A for this fix. The spec mandates "no new CreateOrder (N/A for this fix)" as an explicit plan-level statement (SCAN-05 per spec, JS constraint SCAN-05). Required: add a line in Section 6 or the fix design confirming no new `CreateOrder` call is introduced (acknowledged as N/A). |
| R-13 | PASS | Section 6 explicitly states "DW- items: None." NT8 `Order` constructor and property assignment are confirmed empirically validated; no speculative DW- items created |

---

## Violations Detail

### R-10 FAIL
**Location**: Plan Section 6 (RISKS / DEFERRED WORK) and Section 3 (FIX DESIGN)
**Issue**: No explicit plan-level declaration that the one-line fix introduces zero new instances of `lock()`, `throw new`, `return null`, or `async void`.
**Required addition** (exact wording not mandated; substance is):
> "This fix introduces no new `lock()`, `throw new`, `return null`, or `async void` constructs. The change is a pure boolean short-circuit guard on an existing conditional expression."

---

### R-11 FAIL
**Location**: Plan Section 3 (FIX DESIGN) or Section 6
**Issue**: No explicit plan-level statement that new code identifiers are ASCII-only.
**Required addition** (exact wording not mandated; substance is):
> "All new identifiers and string literals introduced by this fix (`signalName != null`, `B133LaneATests`, `StubOrder`, test method names) are ASCII-only. No Unicode, emoji, or curly-quote characters are introduced."

---

### R-12 FAIL
**Location**: Plan Section 6 (RISKS / DEFERRED WORK) or Section 2 (SCOPE)
**Issue**: Plan is silent on `CreateOrder`. The spec explicitly requires the plan to state that no new `CreateOrder` call is introduced (N/A acknowledgement).
**Required addition** (exact wording not mandated; substance is):
> "No new `CreateOrder` call is introduced. This fix does not interact with order submission. (N/A per spec.)"

---

## Required Revisions for ptt-architect

Add the following to `LaneA-02-architecture-plan.md` before resubmitting:

1. **In Section 3 or Section 6** — Explicit declaration: no new `lock()`, `throw new`, `return null`, `async void` introduced.
2. **In Section 3 or Section 6** — Explicit declaration: all new identifiers are ASCII-only.
3. **In Section 6** — Explicit N/A acknowledgement: no new `CreateOrder` call introduced.

These are additive sentences only. No structural changes to the plan are required.
The core architecture (one-line fix, 5 tests, scope, callers, CYC, regression) is sound and correct.

---

## Passing Architecture Summary (for record)

- Root cause analysis: correct and complete
- Fix design: minimal, surgical, one-line
- Scope: correctly bounded to 2 files
- Caller isolation: correctly justified
- CYC claim: 3 (no new branch, short-circuit only) — correct
- Test strategy: 5 [Fact], class `B133LaneATests`, stub pattern established by B131
- Regression coverage: B132(5)+B131(7)+B130(8)+B129(13) = 33 prior tests
- 7-scan commands: all present with exact syntax
- DW- items: none (correct — no NT8 API uncertainty)

---

*Review written by ptt-plan-reviewer. Return to ptt-architect for Cycle 1 revision.*

---

## REVIEW CYCLE 2 — 2026-08-21

**Reviewer**: ptt-plan-reviewer
**Cycle**: 2 of 2 (final)
**Plan revision**: Architect added three additive statements to resolve R-10, R-11, R-12.

### Per-Check Results — Cycle 2

| Check | Result | Evidence |
|-------|--------|----------|
| R-01 | PASS | Unchanged from Cycle 1. Root cause analysis complete. |
| R-02 | PASS | Unchanged from Cycle 1. One-line fix scope confirmed. |
| R-03 | PASS | Unchanged from Cycle 1. Files Touched list correct. |
| R-04 | PASS | Unchanged from Cycle 1. Files NOT Touched justified. |
| R-05 | PASS | Unchanged from Cycle 1. 5 named [Fact] methods specified. |
| R-06 | PASS | Unchanged from Cycle 1. Stub pattern for NT8 Order documented. |
| R-07 | PASS | Unchanged from Cycle 1. CYC=3, no new CFG branch node. |
| R-08 | PASS | Unchanged from Cycle 1. Regression table covers all 4 prior suites. |
| R-09 | PASS | Unchanged from Cycle 1. All 7 scans present with exact commands. |
| R-10 | PASS | **RESOLVED.** Section 3 "Jane Street DNA Compliance" (L95) now explicitly states: "This fix introduces no new `lock()`, `throw new`, `return null`, or `async void` constructs." |
| R-11 | PASS | **RESOLVED.** Section 4 "ASCII Compliance" (L184-185) now explicitly states: "All new identifiers in `B133Tests.cs` (class name `B133LaneATests`, all method names, all variable names) are ASCII-only characters. No Unicode, emoji, or curly quotes are used." |
| R-12 | PASS | **RESOLVED.** Section 3 "Jane Street DNA Compliance" (L98-99) now explicitly states: "CreateOrder: N/A — this fix does not introduce any new `CreateOrder` calls. The PTT- prefix mandate is not applicable to this change." |
| R-13 | PASS | Unchanged from Cycle 1. DW- items: None. No NT8 API uncertainty. |

### Violations Detail — Cycle 2

None. All 13 checks pass.

### Architecture Integrity Confirmation

The three additions are purely additive sentences. No structural element of the plan was altered:

- Fix design (one-line null-guard) — unchanged and correct
- Scope (2 files only) — unchanged and correct
- CYC claim (3, no new branch) — unchanged and correct
- Test strategy (5 [Fact], B133LaneATests, StubOrder pattern) — unchanged and correct
- Regression coverage (B132+B131+B130+B129 = 33 prior tests) — unchanged and correct
- 7-scan commands (SCAN-01 through SCAN-07) — unchanged and correct
- DW- items (none) — unchanged and correct

---

## VERDICT: REVIEW_PASS

**All 13 checks pass. Phase 3 (ticket generation) is unlocked.**

*Review Cycle 2 written by ptt-plan-reviewer. No further review cycles required.*
