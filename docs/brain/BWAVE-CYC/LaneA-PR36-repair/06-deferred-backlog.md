# BWAVE-CYC Lane A PR #36 Repair -- Deferred Backlog

**Epic**: BWAVE-CYC Lane A Repair (PR #36 blockers)
**Branch**: feature/bwave-cyc-lane-a
**Commit**: 8ec10bb3
**Date**: 2026-09-03
**Author**: ptt-plan-reviewer (Phase 5)

This file records all pre-existing debt and out-of-scope issues identified during or adjacent to
the LaneA PR36 repair. None of these items were introduced by tickets A-1 through A-6 and none
blocked FINAL_PASS. Each item requires a future Director-directed ticket to resolve.

---

## DW-LaneA-01

**Item**: SA1507 -- consecutive blank lines in `BwaveCycTaR6HelperTests`
**File**: `src/PropTraderTools/CopyEngineTests.cs`
**Line(s)**: 6843
**Category**: StyleCop / Code Style
**Priority**: P2
**Why deferred**: This violation is inside the `BwaveCycTaR6HelperTests` class, which belongs to
Lane C epic scope. The LaneA repair (A-2) only touched lines 7181-7395 and did not have authority
to modify LaneC test classes. Fixing this during LaneA repair would violate the No Scope Creep
Protocol (V12.23).
**Recommended action**: Include in LaneC SA1507/SA1508 cleanup pass (same session as DW-LaneA-02,
DW-LaneA-03, and DW-LaneA-05).
**Target block**: LaneC or future dedicated StyleCop cleanup ticket.
**Status**: OPEN

---

## DW-LaneA-02

**Item**: SA1507 -- consecutive blank lines in `BwaveCycTaR6HelperTests` closing area
**File**: `src/PropTraderTools/CopyEngineTests.cs`
**Line(s)**: 6920
**Category**: StyleCop / Code Style
**Priority**: P2
**Why deferred**: Same rationale as DW-LaneA-01. In the same `BwaveCycTaR6HelperTests` class,
same lane boundary violation if touched during LaneA scope.
**Recommended action**: Bundle with DW-LaneA-01 fix.
**Target block**: LaneC or future dedicated StyleCop cleanup ticket.
**Status**: OPEN

---

## DW-LaneA-03

**Item**: SA1508 -- closing brace preceded by blank line in `BwaveCycTaR6HelperTests`
**File**: `src/PropTraderTools/CopyEngineTests.cs`
**Line(s)**: 6921
**Category**: StyleCop / Code Style
**Priority**: P2
**Why deferred**: Same rationale as DW-LaneA-01 and DW-LaneA-02. The closing brace of the
`BwaveCycTaR6HelperTests` class is at line 6921 (preceded by a blank line, violating SA1508).
This is a single-line CSharpier fix but requires LaneC scope authority.
**Note**: The verifier Layer 3 discovered these 3 violations (DW-LaneA-01..03) when the engineer
Layer 2 over-claimed "SA1507: 0, SA1508: 0" for the entire file. The discrepancy was correctly
identified and ruled pre-existing, not a regression.
**Recommended action**: Bundle with DW-LaneA-01 and DW-LaneA-02 fix. One `dotnet csharpier format`
call will resolve all three.
**Target block**: LaneC or future dedicated StyleCop cleanup ticket.
**Status**: OPEN

---

## DW-LaneA-04

**Item**: 3039 pre-existing non-ASCII bytes in source code comments
**File(s)**: `src/PropTraderTools/CopyEngineTests.cs` (primary, lines 5787+); also `B46Tests.cs`,
`B47Tests.cs`
**Line(s)**: 5787+ (CopyEngineTests.cs); precise lines in B46/B47 not enumerated by verifier scan
**Category**: ASCII-Only Compliance
**Priority**: P1
**Why deferred**: These bytes are box-drawing characters (`U+2500` -- HORIZONTAL SCAN LINE ─)
used as visual section-header separators in comments, e.g. `// ─────────────────────────`. They
predate this branch and were not introduced by A-1 through A-6. The A-2 removal block was at lines
7181-7395; the non-ASCII bytes are at 5787+ (entirely separate section).

The ASCII-only mandate (AGENTS.md §2) reads: "NEVER use Unicode, emoji, or curly quotes in C# *string
literals*." The mandate's primary enforcement target is string literal content, not comment
decorators. However, a broader reading would include all source bytes.

**Recommended action**:
1. Director to confirm whether the ASCII-only mandate extends to comment bytes or only string literals.
2. If mandate applies: replace box-drawing characters with ASCII dashes `//---` in all affected comment
   headers. A global search-and-replace is safe as these are comments only.
3. If limited to string literals: document the precedent and close this item.
**Target block**: future (requires Director scope decision before acting).
**Status**: OPEN

---

## DW-LaneA-05

**Item**: SA1507 in `BwaveCycLaneCTests.cs` line 566
**File**: `src/PropTraderTools/Tests/BwaveCycLaneCTests.cs`
**Line(s)**: 566
**Category**: StyleCop / Code Style
**Priority**: P2
**Why deferred**: This violation is in a Lane C test file. Lane C ticket ownership is separate from
Lane A. Noted by Director in the original LaneA repair prompt as belonging to Lane C. Touching this
file during LaneA repair would constitute a cross-lane scope violation.
**Recommended action**: Fix as part of LaneC SA1507/SA1508 pass. Can be bundled with DW-LaneA-01..03.
**Target block**: LaneC ticket (blocked by lane ownership -- not a LaneA deliverable).
**Blocked by**: LaneC ticket owner.
**Status**: OPEN

---

## DW-LaneA-06

**Item**: `BuildArrowCluster` residual bug -- unconditional `Background = mainBackground` at line 1233
overrides teal-button background for `_beBtn2`, `_globalBeBtn2`, `_quickBtn`, `_quickAllBtn`
**File**: `src/PropTraderTools/TradeCopierPanel.cs`
**Line(s)**: 1233 (`var btn = new Button { Content = mainContent, Background = mainBackground };`)
**Category**: Logic Bug / UI Regression
**Priority**: P1
**Why deferred**: Ticket A-5 was written as NOOP because the plan was authored against `main/2270c544`
where `BuildArrowCluster` was absent (the method had been replaced by the inline
`BuildBufferedButtonsRow` rewrite during LaneC remediation). On the `feature/bwave-cyc-lane-a` branch,
`BuildArrowCluster` still exists (LaneC R11 extract), and the bug is present at line 1233.

The A-5 ticket scope was explicitly defined as "confirm method absent; no source edit required." The
engineer correctly applied no edit per ticket scope and documented the discrepancy. The verifier
independently confirmed the bug and ruled it non-blocking for VERIFY_PASS.

**Fix options** (requires Director decision):
1. **Conditional background** (minimal fix): Change line 1233 to
   `Background = isTeal ? null : mainBackground` (or omit `Background` property for teal buttons).
   Requires `isTeal` parameter added to `BuildArrowCluster` signature.
2. **Collapse to inline** (architectural): Remove `BuildArrowCluster` and restore inline
   `BuildBufferedButtonsRow` pattern (matches main/2270c544 approach). Higher effort but removes the
   extracted-method divergence between branches.

**Recommended action**: Director to decide fix approach. Assign a dedicated ticket (LaneA-follow-up
or next B-wave). Do NOT merge `feature/bwave-cyc-lane-a` to `main` if `BuildArrowCluster` produces
incorrect teal-button backgrounds visible in the NT8 UI.
**Target block**: LaneA follow-up ticket or B-next.
**Status**: OPEN

---

## Summary Table

| ID | File | Line(s) | Issue | Priority | Owner | Status |
|----|------|---------|-------|----------|-------|--------|
| DW-LaneA-01 | CopyEngineTests.cs | 6843 | SA1507 consecutive blank lines | P2 | LaneC | OPEN |
| DW-LaneA-02 | CopyEngineTests.cs | 6920 | SA1507 consecutive blank lines | P2 | LaneC | OPEN |
| DW-LaneA-03 | CopyEngineTests.cs | 6921 | SA1508 closing brace preceded by blank line | P2 | LaneC | OPEN |
| DW-LaneA-04 | CopyEngineTests.cs, B46/B47 | 5787+ | 3039 non-ASCII bytes in comments | P1 | Director decision | OPEN |
| DW-LaneA-05 | BwaveCycLaneCTests.cs | 566 | SA1507 (LaneC ownership) | P2 | LaneC | OPEN |
| DW-LaneA-06 | TradeCopierPanel.cs | 1233 | BuildArrowCluster unconditional background | P1 | Director decision | OPEN |

---

*Author: ptt-plan-reviewer | BWAVE-CYC LaneA PR36 repair | 2026-09-03*
