# BWAVE-CYC Deferred Backlog

This file accumulates all deferred items across BWAVE-CYC lanes. Each block records items
identified during the Phase 5 final review for that lane's PR. Items are NOT introduced by
the respective lane's tickets; they represent pre-existing debt, test quality gaps, or
out-of-scope issues that require a future Director-directed ticket to resolve.

---

# Block 1: BWAVE-CYC Lane A -- PR #36 Repair

**Epic**: BWAVE-CYC Lane A Repair (PR #36 blockers)
**Branch**: `feature/bwave-cyc-lane-a`
**Commit**: 8ec10bb3
**Date**: 2026-09-03
**Author**: ptt-plan-reviewer (Phase 5)

These items were identified during or adjacent to the LaneA PR36 repair. None were introduced
by tickets A-1 through A-6. None blocked FINAL_PASS.

| ID | Severity | File | Line | Description | Action |
|----|----------|------|------|-------------|--------|
| DW-LaneA-01 | P2 | CopyEngineTests.cs | 6843 | SA1507 -- consecutive blank lines in `BwaveCycTaR6HelperTests` | Bundle with DW-LaneA-02/03; fix in LaneC or future StyleCop cleanup ticket |
| DW-LaneA-02 | P2 | CopyEngineTests.cs | 6920 | SA1507 -- consecutive blank lines in `BwaveCycTaR6HelperTests` closing area | Bundle with DW-LaneA-01/03 |
| DW-LaneA-03 | P2 | CopyEngineTests.cs | 6921 | SA1508 -- closing brace preceded by blank line in `BwaveCycTaR6HelperTests` | Bundle with DW-LaneA-01/02; one `dotnet csharpier format` call resolves all three |
| DW-LaneA-04 | P1 | CopyEngineTests.cs (primary), B46Tests.cs, B47Tests.cs | 5787+ | 3039 non-ASCII bytes in source code comments (box-drawing U+2500 section headers) | Director to confirm scope of ASCII mandate vs. comment decorators. If mandate applies: global search-and-replace `//---` |
| DW-LaneA-05 | P2 | BwaveCycLaneCTests.cs | 566 | SA1507 -- belongs to LaneC ticket ownership | Fix in LaneC SA1507/SA1508 pass; blocked by lane ownership |
| DW-LaneA-06 | P1 | TradeCopierPanel.cs | 1233 | `BuildArrowCluster` unconditional `Background = mainBackground` overwrites teal-button background. Ticket A-5 was NOOP per plan scope (method absent on main/2270c544). Fix requires Director decision: conditional background or collapse to inline `BuildBufferedButtonsRow` | Director decides fix approach; assign dedicated ticket (LaneA-follow-up or B-next) |

**Status of all DW-LaneA items**: OPEN

---

# Block 2: BWAVE-CYC Lane B -- PR #37

**Epic**: BWAVE-CYC Lane B -- CopyEngine CCN reduction TB-T1..T7
**Branch**: `feature/bwave-cyc-lane-b2`
**Merge commit**: d6017eab
**Date**: 2026-09-03
**Author**: ptt-plan-reviewer (Phase 5)

These items were identified by Cubic during bot review of PR #37. All 8 findings are in
`BwaveCycLaneBTests.cs` (companion test file) only. Zero production code violations found.
None were introduced by tickets TB-T1 through TB-T7. None blocked FINAL_PASS.

| ID | Severity | File | Line | Description | Action |
|----|----------|------|------|-------------|--------|
| DW-B37-01 | P2 | BwaveCycLaneBTests.cs | 142 | `TryRecordBeTargetFill` not exercised with `Order` object; production regression path not caught by test | Implement with real `Order` mock or expand test to cover Order-based execution path in B-next test-hardening ticket |
| DW-B37-02 | P3 | BwaveCycLaneBTests.cs | 433 | Test name `IsBeRetryEligible_ReturnsFalse_WhenPositionIsFlat` contradicts `Assert.True`; correct name: `IsPttBeRetryTriggerOrder_ReturnsTrue_WhenNameIsPttQxT` | Rename test method; update test name to match assertion |
| DW-B37-03 | P2 | BwaveCycLaneBTests.cs | 446 | Test only calls predicate, never exercises retry execution path | Rename or implement the retry execution branch; verify TryFireFollowerBeRetry orchestration |
| DW-B37-04 | P3 | BwaveCycLaneBTests.cs | 546 | Test name `IsNativeExitName_ReturnsTrue_WhenNameIsTarget` contradicts `Assert.False`; correct name: `IsNativeExitName_ReturnsFalse_WhenNameIsTarget` | Rename test method |
| DW-B37-05 | P2 | BwaveCycLaneBTests.cs | 697 | Test does not invoke `CopyRule.Create`; normalization path (follower name round-trip) not verified | Rename test or expand to call `CopyRule.Create` and verify normalized follower names |
| DW-B37-06 | P3 | BwaveCycLaneBTests.cs | 707 | Test name `ResolveMultipliers_ReturnsAllOnes_WhenMultipliersNull` contradicts `Assert.Null`; correct name: `ResolveMultipliers_ReturnsNull_WhenMultipliersNull` | Rename test method |
| DW-B37-07 | P3 | BwaveCycLaneBTests.cs | 723 | Test name says Bid but `Assert.Equal(101.0)` asserts the Ask value; correct name: `SelectRefPriceByDirection_ReturnsAsk_WhenLong` | Rename test method |
| DW-B37-08 | P3 | BwaveCycLaneBTests.cs | 752 | Test name says Ask but `Assert.Equal(100.0)` asserts the Bid value; correct name: `SelectRefPriceByDirection_ReturnsBid_WhenShort` | Rename test method |

**Recommended batching**:
- **Single rename pass** (one session): DW-B37-02, DW-B37-04, DW-B37-06, DW-B37-07, DW-B37-08 -- all P3 test name inversions; no logic change required.
- **Test hardening pass** (requires analysis): DW-B37-01, DW-B37-03, DW-B37-05 -- P2 items requiring actual test implementation work.

**Status of all DW-B37 items**: OPEN

---

## Cumulative Status Table

| ID | Priority | File | Line | Status | Target Block |
|----|----------|------|------|--------|--------------|
| DW-LaneA-01 | P2 | CopyEngineTests.cs | 6843 | OPEN | LaneC or StyleCop cleanup |
| DW-LaneA-02 | P2 | CopyEngineTests.cs | 6920 | OPEN | LaneC or StyleCop cleanup |
| DW-LaneA-03 | P2 | CopyEngineTests.cs | 6921 | OPEN | LaneC or StyleCop cleanup |
| DW-LaneA-04 | P1 | CopyEngineTests.cs, B46Tests.cs, B47Tests.cs | 5787+ | OPEN | Director decision required |
| DW-LaneA-05 | P2 | BwaveCycLaneCTests.cs | 566 | OPEN | LaneC ticket (ownership block) |
| DW-LaneA-06 | P1 | TradeCopierPanel.cs | 1233 | OPEN | Director decision required |
| DW-B37-01 | P2 | BwaveCycLaneBTests.cs | 142 | OPEN | B-next test hardening |
| DW-B37-02 | P3 | BwaveCycLaneBTests.cs | 433 | OPEN | B-next test cleanup (rename pass) |
| DW-B37-03 | P2 | BwaveCycLaneBTests.cs | 446 | OPEN | B-next test hardening |
| DW-B37-04 | P3 | BwaveCycLaneBTests.cs | 546 | OPEN | B-next test cleanup (rename pass) |
| DW-B37-05 | P2 | BwaveCycLaneBTests.cs | 697 | OPEN | B-next test hardening |
| DW-B37-06 | P3 | BwaveCycLaneBTests.cs | 707 | OPEN | B-next test cleanup (rename pass) |
| DW-B37-07 | P3 | BwaveCycLaneBTests.cs | 723 | OPEN | B-next test cleanup (rename pass) |
| DW-B37-08 | P3 | BwaveCycLaneBTests.cs | 752 | OPEN | B-next test cleanup (rename pass) |

---

*Last updated: 2026-09-03 | ptt-plan-reviewer | BWAVE-CYC LaneB PR #37 Phase 5*
