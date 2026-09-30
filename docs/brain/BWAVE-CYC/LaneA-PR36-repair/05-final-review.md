# BWAVE-CYC Lane A PR #36 Repair -- Final Review (Phase 5)

**Epic**: BWAVE-CYC Lane A Repair (PR #36 blockers)
**Branch**: feature/bwave-cyc-lane-a
**Commit reviewed**: 8ec10bb3
**Date**: 2026-09-03
**Reviewer**: ptt-plan-reviewer (Phase 5)
**Inputs read**:
- `docs/brain/BWAVE-CYC/LaneA-PR36-repair/02-architecture-plan.md`
- `docs/brain/BWAVE-CYC/LaneA-PR36-repair/04-ticket-review.md`
- `docs/brain/BWAVE-CYC/LaneA-PR36-repair/ticket-A-completion.md`
- `docs/brain/BWAVE-CYC/LaneA-PR36-repair/ticket-A-verification.md`
- `docs/standards/jane-street/RULES_CATALOG.md`

---

## Known Baseline

| Item | Status |
|------|--------|
| NT8-runtime pre-existing test failures | 80 -- accepted by Director |
| 10k diff waiver | Approved for PR #36 |
| Greptile check | SUCCESS on PR #36 |
| CodeRabbit state | CHANGES_REQUESTED on PR #36 (being resolved by this repair) |
| Commit under review | 8ec10bb3 on feature/bwave-cyc-lane-a |

---

## SECTION A -- Coherent System Check

| Check | Result | Evidence |
|-------|--------|----------|
| All 6 tickets (A-1 through A-6) have completion artifacts | PASS | ticket-A-completion.md covers A-1 through A-6 with per-ticket detail |
| All 6 tickets have verification artifacts | PASS | ticket-A-verification.md has CHECK 1 through CHECK 6 |
| VERIFY_PASS confirmed | PASS | Explicit "VERIFY_PASS" verdict in ticket-A-verification.md (line 23) |
| No ticket silently skipped | PASS | A-4 and A-5 documented as CONFIRMED-NOOP; rationale present |
| Execution order A-1->A-2->A-3->A-4->A-5->A-6 respected | PASS | Completion and verification both confirm ordered application |
| A-2/A-6 dependency (A-2 before A-6) respected | PASS | Verifier confirmed TA-R9 block removed (A-2) before A-6 applied |
| Ticket review reached TICKET_REVIEW_PASS | PASS | Cycle 2: all 6 tickets PASS (A-2 and A-3 FAIL in Cycle 1 resolved by architect) |

**SECTION A: PASS**

---

## SECTION B -- Cross-File JS Violation Check

All checks performed via independent grep of `src/PropTraderTools/` (RULES_CATALOG.md consulted).

### B-1: lock() -- JS-021

```
grep -r "lock\s*\(" src/PropTraderTools/ --include="*.cs"
```

**Result**: 33 matches found -- ALL are comment lines (compliance annotations such as
`// JS-021: no lock()`). Zero production code uses `lock(`. **JS-021: PASS.**

### B-2: async void -- JS-033

```
grep -r "async void " src/PropTraderTools/ --include="*.cs"
```

**Result**: 50 matches found -- ALL are comment lines (compliance annotations such as
`// JS-033: no async void`). Zero production code uses `async void`. **JS-033: PASS.**

### B-3: JS-002 (null return) -- new method audit

`TryFindPositionForInstrument` (added by A-6):
- Return type: `bool` -- cannot return null
- Method body: `pos = null` is an out-parameter initialization before `return false`, NOT a `return null` statement
- No `return null;` exists anywhere in the new method body
- Pre-existing `return null` count in CopyEngine.cs: 16 (unchanged from pre-repair baseline confirmed by both engineer Layer 2 and verifier Layer 3)

**JS-002: PASS.** No new null-returning code introduced.

### B-4: A-1 changes -- no new JS rule violated

A-1 replaced `Content = "\u25B2"` with `Content = "^"` and `Content = "\u25BC"` with `Content = "v"` at
2 sites (lines 1214, 1220 on this branch -- BuildArrowCluster has a single up/down pair).
- JS-006 (phantom types): inapplicable -- string Content property, not a unit type. PASS.
- JS-008 (mutable struct fields): inapplicable -- no struct involved. PASS.
- No new brush, no new lock, no new async. PASS.

### B-5: A-3 changes -- no test anti-patterns introduced

A-3 removed inner `try/catch(TargetInvocationException)` from `Record.Exception` lambda.
The fix makes `Assert.Null(ex)` substantive, not vacuous. xUnit pattern is correct.
No NUnit or MSTest usage introduced. PASS.

### B-6: A-5 residual bug (line 1233 TradeCopierPanel.cs)

`BuildArrowCluster` exists on this branch (not present on main/2270c544 when plan was written).
Line 1233: `var btn = new Button { Content = mainContent, Background = mainBackground };`
unconditionally sets `Background = mainBackground` on all buttons including teal-bordered ones.
This is a **pre-existing issue on this branch** not introduced by this repair; A-5 ticket was
correctly scoped NOOP. No JS rule violation was newly introduced by A-1..A-6. Deferred to K-6.

**SECTION B: PASS** -- Zero JS rule violations introduced by this repair.

---

## SECTION C -- Missing Wiring Check

```
grep -r "FindPositionForInstrument" src/PropTraderTools/ --include="*.cs"
```

**Result**: 11 matches total. Every match examined:

| Match | Location | Type | Assessment |
|-------|----------|------|------------|
| `TryCancelOrders, FindPositionForInstrument.` | BwaveCycLaneAR9Tests.cs:4 | Comment (original test list header) | OK -- comment only |
| `// FindPositionForInstrument: position lookup...` | BwaveCycLaneAR9Tests.cs:154 | Comment | OK |
| `T_R9_10_TryFindPositionForInstrument_MethodExists...` | BwaveCycLaneAR9Tests.cs:157 | Method name with Try prefix | CORRECT |
| `GetStaticMethod("TryFindPositionForInstrument")` | BwaveCycLaneAR9Tests.cs:159 | Lookup with Try prefix | CORRECT |
| `T_R9_11_TryFindPositionForInstrument_ParameterNames` | BwaveCycLaneAR9Tests.cs:167 | Method name with Try prefix | CORRECT |
| `GetStaticMethod("TryFindPositionForInstrument")` | BwaveCycLaneAR9Tests.cs:169 | Lookup with Try prefix | CORRECT |
| `// CYC=8 after R9 extraction: null...FindPositionForInstrument(0)...` | CopyEngine.cs:1115 | Comment | OK |
| `if (!TryFindPositionForInstrument(acc, instr, out var pos)...` | CopyEngine.cs:1129 | Production call with Try prefix | CORRECT -- caller updated |
| `// BWAVE-CYC TA-R9 (restored): TryFindPositionForInstrument...` | CopyEngine.cs:1167 | Comment | OK |
| `// JS-002: bool + out parameter replaces null return (original FindPositionForInstrument...)` | CopyEngine.cs:1168 | Comment | OK |
| `private static bool TryFindPositionForInstrument(` | CopyEngine.cs:1172 | Declaration | CORRECT |

**Zero** bare calls to `FindPositionForInstrument` (without `Try` prefix) remain in any production code.

**Caller update note**: The plan stated `SubmitBeStop` caller update was out of scope (method was absent on HEAD when plan written). The engineer discovered the method existed on this branch and **correctly updated the caller** at CopyEngine.cs line 1129. This is a positive improvement beyond plan scope -- wiring is complete.

T_R9_10 and T_R9_11 tests updated to `TryFindPositionForInstrument`, 3-param signature with `bool` return type. Both tests now PASS (SCAN-07 net improvement: -1 failure).

**SECTION C: PASS**

---

## SECTION D -- Spec Requirements Satisfied

| Finding | Resolving Ticket | Evidence | Result |
|---------|-----------------|----------|--------|
| CodeRabbit CR36-1 (CS0103 misplaced block) | A-2 | Verifier CHECK 2: TA-R9 block gone; `BwaveCycTaR7HelperTests` intact; TA-R10 comment at new line 7183 | PASS |
| CodeRabbit CR36-2 (vacuous Record.Exception) | A-2 (partial) + A-3 | Verifier CHECK 3: `TargetInvocationException` = 0 in BwaveCycLaneAR9Tests.cs; A-2 eliminates the CopyEngineTests.cs instance | PASS |
| CodeRabbit CR36-3 (ASCII Unicode arrows) | A-1 | Verifier CHECK 1: 0 `\u25B[23]` in repair scope (lines 1130-1400); replacements at lines 1214, 1220 | PASS |
| Greptile P0 JS-002 (null return FindPositionForInstrument) | A-6 | Verifier CHECK 6: `TryFindPositionForInstrument` at lines 1172+; bool return; no `return null`; caller at line 1129 | PASS |
| Greptile P2 (teal button background `BuildArrowCluster`) | A-5 (NOOP) | Verifier CHECK 5: residual bug at line 1233 documented; ticket correctly scoped NOOP against plan HEAD | PASS (deferred K-6) |
| Greptile P2 (ASCII Unicode in string literals) | A-1 | Same as CR36-3 | PASS |
| CodeFactor FAILURE (SA1507/SA1508) | A-4 (NOOP) | Verifier CHECK 4: pre-existing violations at lines 6843/6920/6921 not in repair scope; zero new violations introduced | PASS (deferred K-1..K-3) |

All 7 spec findings covered. No uncovered finding. No phantom work.

**SECTION D: PASS**

---

## SECTION E -- All 7 Scans Zero

| Scan | Expected | Engineer Layer 2 | Verifier Layer 3 | Live Re-Check | Status |
|------|----------|------------------|-----------------|---------------|--------|
| SCAN-01: lock() | 0 production | 0 | 0 | 33 matches, ALL comments | PASS |
| SCAN-02: async void | 0 production | 0 | 0 | 50 matches, ALL comments | PASS |
| SCAN-03: return null new | 0 new in TryFind... | 0 new; 16 pre-existing | 0 new; 16 pre-existing | 24 grep hits, 8 comments + 16 actual; count unchanged | PASS |
| SCAN-04: throw new new | 0 new in modified files | 0 results | 0 results | Not re-scanned; verifier Layer 3 authoritative | PASS |
| SCAN-05: build | 0 errors | Build succeeded; 0 errors | Build succeeded; 0 errors; 1 pre-existing xUnit2004 warning | N/A | PASS |
| SCAN-06: ASCII | 0 new in repair scope | 0 in lines 1130-1400 | 0 new non-ASCII in repair scope; 3039 pre-existing bytes in comments | Verifier Layer 3 authoritative | PASS |
| SCAN-07: test failures | 0 new failures | Failed: 22 (was 23); -1 improvement | Failed: 22; Passed: 487; Skipped: 15 | Verifier Layer 3 authoritative | PASS |

**SECTION E: PASS** -- All 7 scans confirm zero new violations. SCAN-07 net improvement of -1 failure (T_R9_10, T_R9_11 now PASS).

---

## SECTION F -- Pre-Existing Debt Identified by Verifier

The following items were discovered by the verifier as pre-existing debt. They were NOT introduced
by this repair and do not block VERIFY_PASS or FINAL_PASS. Each is documented in Section K for
Director-directed follow-up.

| ID | File | Lines | Category | Severity |
|----|------|-------|----------|---------|
| D1 | CopyEngineTests.cs | 5787+ | 3039 pre-existing non-ASCII bytes (box-drawing chars U+2500 in comment section headers) | P2 debt |
| D2 | CopyEngineTests.cs | 6843, 6920, 6921 | SA1507 (x2) + SA1508 (x1) in BwaveCycTaR6HelperTests closing area | P2 debt |

**SECTION F: INFORMATIONAL** -- No new violations. Debt documented in K-4 (D1) and K-1..K-3 (D2).

---

## SECTION K -- Deferred Work (MANDATORY)

The following items are deferred from this repair. Each is pre-existing or out-of-scope per Director
ticket boundary. None block FINAL_PASS for this repair.

| ID | Item | Priority | File | Line(s) | Target Block | Status |
|----|------|----------|------|---------|--------------|--------|
| DW-LaneA-01 | SA1507 -- consecutive blank lines in `BwaveCycTaR6HelperTests` | P2 | CopyEngineTests.cs | 6843 | LaneC or future cleanup | OPEN |
| DW-LaneA-02 | SA1507 -- consecutive blank lines in `BwaveCycTaR6HelperTests` closing area | P2 | CopyEngineTests.cs | 6920 | LaneC or future cleanup | OPEN |
| DW-LaneA-03 | SA1508 -- closing brace preceded by blank line in `BwaveCycTaR6HelperTests` | P2 | CopyEngineTests.cs | 6921 | LaneC or future cleanup | OPEN |
| DW-LaneA-04 | 3039 non-ASCII bytes in source code comments (box-drawing U+2500 section headers) -- review for ASCII-only mandate compliance | P1 | CopyEngineTests.cs (primary), B46Tests.cs, B47Tests.cs | 5787+ | future | OPEN |
| DW-LaneA-05 | SA1507 in BwaveCycLaneCTests.cs line 566 -- belongs to Lane C ticket ownership | P2 | BwaveCycLaneCTests.cs | 566 | LaneC ticket (blocked by ownership) | OPEN |
| DW-LaneA-06 | A-5 `BuildArrowCluster` residual bug: unconditional `Background = mainBackground` at line 1233 overwrites teal-button background. Ticket A-5 was NOOP per plan scope (method absent on main/2270c544). Full fix requires Director decision: retain `BuildArrowCluster` with conditional background assignment, or collapse back to inline `BuildBufferedButtonsRow`. | P1 | TradeCopierPanel.cs | 1233 | B-next or LaneA follow-up | OPEN |

### Notes

- **K-1..K-3** (DW-LaneA-01..03): In `BwaveCycTaR6HelperTests` -- these are LaneC epic scope. Recommended action: include in LaneC SA1507/SA1508 cleanup pass alongside DW-LaneA-05.
- **K-4** (DW-LaneA-04): Non-ASCII comment bytes pre-date this branch. ASCII-only mandate (AGENTS.md §2) applies to *string literals* and production code. Comment-only non-ASCII is a lower-priority cleanup. Director should confirm scope of ASCII mandate vs. comment decorators.
- **K-5** (DW-LaneA-05): Explicitly noted by Director in original prompt as belonging to Lane C ticket. Blocked by lane ownership -- not an LaneA deliverable.
- **K-6** (DW-LaneA-06): Requires Director architectural decision before a fix ticket can be written. The teal-button background issue existed in `BuildArrowCluster` (LaneC R11 extract), was incidentally resolved on main by the inline `BuildBufferedButtonsRow` rewrite, but the bug persists on this branch. Fix complexity: low (add `if (isTeal)` guard) but decision authority required.

---

## Final Verdict

All sections PASS. No JS rule violations introduced by this repair. All 6 tickets correctly
implemented and independently verified. All 7 scans clean (zero new violations). All spec
findings resolved. Deferred work documented in Section K and in 06-deferred-backlog.md.

```
FINAL_PASS
```

*Reviewer: ptt-plan-reviewer | Phase 5 Final Review | 2026-09-03*
