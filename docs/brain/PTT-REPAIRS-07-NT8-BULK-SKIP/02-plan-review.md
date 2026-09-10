# 02-plan-review.md
# Epic: PTT-REPAIRS-07-NT8-BULK-SKIP
# Reviewer: PTT Plan Reviewer (Phase 2) -- SECOND REVIEW
# Plan Revision: REVISION 1 (RULE-01 + RULE-02 corrections applied by ptt-architect)
# Verdict: REVIEW_PASS

---

## Prior Violations Status

| ID | Prior Verdict | Resolution in REVISION 1 |
|----|---------------|--------------------------|
| RULE-01 | FAIL: B79BeAllTargetSnapshotTests and B79BeReplaceAttemptGuardTests silently deviated from spec's OPTION A designation without justification | FIXED: Section 5 OPTION B table now includes explicit `Spec Category` / `Plan Category` columns with full per-class justification text. A dedicated "Spec deviation note (RULE-01 resolution)" block at the end of Section 5 states the corrective rationale verbatim. Deviation is no longer silent. |
| RULE-02 | FAIL: SCAN-7 expected count=1 is inverted (count=1 = NOT hard-linked) | FIXED: SCAN-7 now requires `LinkType = "HardLink" AND HardLinkCount >= 2 after sync` with a rationale block explicitly stating that count=1 indicates the sync FAILED. |

Both prior violations are fully resolved. No new violations found.

---

## Review Requirements Check (all 10)

### REQ 1 — LANE-SPLIT GATE RESULT present
**Result: PASS**
Plan header (line 7): `## LANE-SPLIT GATE RESULT: LANES-APPROVED` — present and unambiguous.

### REQ 2 — Gate Q1..Q4 consistent with LANES-APPROVED
**Result: PASS**
- Q1=NO (changes span thousands of lines across disjoint class ranges — not within 50 lines of same method)
- Q2=NO (independent class ranges, no shared state — Ticket 2 does not depend on Ticket 1's design)
- Q3=YES (each ticket has standalone value if the other is blocked)
- Q4=YES (each ticket has an independent SIM verification path with distinct SCAN-5 numeric targets)
LANES-APPROVED is the correct gate result for Q1=NO, Q2=NO, Q3=YES, Q4=YES. Consistent.

### REQ 3 — B79BeAllTargetSnapshotTests and B79BeReplaceAttemptGuardTests deviation documented with justification (RULE-01 fix)
**Result: PASS**
Section 5 OPTION B table includes explicit `Spec Category = OPTION A` and `Plan Category = OPTION B` columns for both classes, with full justification text per class:
- B79BeAllTargetSnapshotTests: "Spec-deviation: spec listed as OPTION A (all-TypeInit), but live class table shows 8 active tests with only 7 TypeInit failures -- 1 test passes. Applying OPTION A bulk-replace would Skip the 1 passing test, violating the DO-NOT-Skip-passing-tests constraint. OPTION B (individual Skip) is the only safe choice. This deviation is explicit and intentional."
- B79BeReplaceAttemptGuardTests: same pattern documented, 2 passing tests identified.

A dedicated "Spec deviation note (RULE-01 resolution)" paragraph at the end of Section 5 summarises the corrective logic for both classes. The deviation is explicit, justified, and traceable.

### REQ 4 — SCAN-7 checks LinkType="HardLink" AND HardLinkCount >= 2, NOT count=1 (RULE-02 fix)
**Result: PASS**
Section 10, SCAN-7:
```
$f = Get-Item "src\PropTraderTools\CopyEngineTests.cs"; $f.LinkType + " count=" + $f.HardLinkCount
-> Expected: LinkType = "HardLink" AND HardLinkCount >= 2 after sync
-> Rationale: a count of 1 means NOT hard-linked (regular file); a properly synced
   hard link has LinkType="HardLink" and at least 2 directory entries (original + link).
   deploy-sync.ps1 failure would leave count=1; a passing SCAN-7 requires count >= 2.
```
Both required conditions present. The rationale block explicitly identifies count=1 as a failure state. Verification logic is no longer inverted.

### REQ 5 — Per-class strategy (OPTION A vs OPTION B) enumerated correctly
**Result: PASS**

OPTION A (5 classes — all-TypeInit, bulk replace):
| Class | Active | Skip Count | Spec | Plan |
|---|---|---|---|---|
| CopyEngineTests | 188 | 188 | OPTION A | OPTION A |
| CopyEngineB75Tests | 47 | 47 | OPTION A | OPTION A |
| B77QxRaceGuardTests | 8 | 8 | OPTION A | OPTION A |
| B78TargetDispatchTests | 8 | 8 | OPTION A | OPTION A |
| BwaveCycTaR7HelperTests | 34 | 34 | OPTION A | OPTION A |

OPTION B (5 classes — individual Skip):
| Class | TypeInit | Spec | Plan |
|---|---|---|---|
| B79BeAllTargetSnapshotTests | 7 | OPTION A (corrected to B — justified) | OPTION B |
| B79BeReplaceAttemptGuardTests | 1 | OPTION A (corrected to B — justified) | OPTION B |
| B79CancelRaceGuardTests | 5 | OPTION B | OPTION B |
| BwaveCycT1R1BeHelperTests | 2 | OPTION B | OPTION B |
| BwaveCycTaR6HelperTests | 6 | OPTION B | OPTION B |

All 10 in-scope classes accounted for. Classifications are correct.

### REQ 6 — Plan does NOT propose touching production code
**Result: PASS**
Section 9 constraint 1: "Touch ONLY `src/PropTraderTools/CopyEngineTests.cs`. No production code changes."
Section 14 component table: "Production files modified: 0". No production file is named anywhere in the plan.

### REQ 7 — Plan does NOT propose Skip on already-Skipped tests
**Result: PASS**
Section 5 OPTION A mechanism: "Do NOT touch any `[Fact(Skip...)]` lines (whether single-line or multi-line format)."
Section 9 constraint 5 lists each existing-Skip group to preserve:
- CopyEngineTests: 17 existing `[Fact(Skip...)]` preserved
- CopyEngineB75Tests: 14 existing (7 single-line + 7 multi-line) preserved, with specific line numbers
- BwaveCycTaR7HelperTests: 1 at line 8059 — "Engineer must NOT double-skip it."
Double-skip prevention is explicit and class-specific.

### REQ 8 — 7-scan checklist defined per ticket
**Result: PASS**
Section 10 defines all 7 scans (SCAN-1 through SCAN-7). SCAN-5 includes per-ticket numeric targets inline:
- After Ticket 1: Skipped >= 325, Failed <= 157, Passed >= 19
- After Ticket 2: Skipped >= 338, Failed <= 144, Passed >= 19
Both Ticket 1 and Ticket 2 sections in Section 7 cross-reference the SCAN-5 checkpoint with matching numbers. The 7-scan checklist applies to both tickets.

### REQ 9 — Ticket split is sound (no blocking cross-ticket dependency)
**Result: PASS**
Ticket 1 and Ticket 2 operate on disjoint line ranges in the same file. Gate Q2 confirms: "Fix B design depends on Fix A final design? NO (independent class ranges, no shared state)." Ticket 2 (pure OPTION B: BwaveCycTaR6HelperTests, B79CancelRaceGuardTests, BwaveCycT1R1BeHelperTests) can proceed in any order relative to Ticket 1. No shared variables, no ordering constraints, no blocking dependency.

### REQ 10 — Lane A scope boundary respected
**Result: PASS**
Section 4 explicitly lists 6 classes as "NOT in scope (Lane A only -- DO NOT TOUCH)" with line ranges:
- B78QxFollowerStopTests (5129-5306)
- B78CancelFollowerGuardTests (5389-5477)
- B79BeRetryAtmTriggerTests (5692-5765)
- B79BeReplaceFallbackTests (5766-5822)
- BwaveCycTaR2HelperTests (6686-6811)
- BwaveCycTaR3HelperTests (6812-7109)

Each OPTION B class analysis in Section 6 includes an explicit "DO NOT touch the other N tests in this class" statement. B79CancelRaceGuardTests: "DO NOT touch the 59 Lane A tests in this class." Boundary is enforced at both class level and per-test level.

---

## Jane Street DNA Scan

| Rule | Check | Result |
|---|---|---|
| JS-021 lock() anywhere | No lock() introduced in test file changes | PASS |
| JS-021 Monitor/Mutex/SemaphoreSlim for state | None introduced | PASS |
| JS-023 UI update off-thread without Dispatcher.InvokeAsync | No UI changes | PASS |
| JS-001 throw in OnOrderUpdate/SendCopy/gate chain | No logic code added | PASS |
| JS-002 null return where value expected | No logic code added | PASS |
| JS-003 magic string for discriminated state | Skip string is xUnit metadata attribute, not FSM state | PASS |
| JS-009 Dictionary for shared/thread-touched collection | None added | PASS |
| JS-008 Mutable fields on struct | None added | PASS |
| JS-008 SolidColorBrush not Freeze()d | None added | PASS |
| JS-010 Public constructor on singleton or signal struct | None added | PASS |
| NT8: async/await in OnInitialize/OnDestroyed/OnWindowCreated | None added | PASS |
| NT8: Account.All in constructor | None added | PASS |
| NT8: sealed TradeCopierWindow | Not applicable (test file) | N/A |
| NT8: FontFamily override (SCAN-03) | None added | PASS |
| NT8: Hardcoded #RRGGBB hex (SCAN-04) | None added | PASS |
| NT8: CreateOrder without PTT- prefix (SCAN-05) | None added | PASS |
| NT8: DateTime.Now not UtcNow (SCAN-06) | None added | PASS |
| CYC > 8 any method | No methods added | PASS |

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|---|---|---|
| LANE-SPLIT GATE RESULT present | YES | Header, lines 7-14 |
| Gate Q1..Q4 consistent with LANES-APPROVED | YES | Lines 9-13 |
| OPTION A: CopyEngineTests (188) | YES | Section 5 OPTION A |
| OPTION A: CopyEngineB75Tests (47) | YES | Section 5 OPTION A |
| OPTION A: B77QxRaceGuardTests (8) | YES | Section 5 OPTION A |
| OPTION A: B78TargetDispatchTests (8) | YES | Section 5 OPTION A |
| OPTION A: BwaveCycTaR7HelperTests (34) | YES | Section 5 OPTION A |
| B79BeAllTargetSnapshotTests -- deviation from spec OPTION A documented | YES (RULE-01 fixed) | Section 5 OPTION B table + deviation note |
| B79BeReplaceAttemptGuardTests -- deviation from spec OPTION A documented | YES (RULE-01 fixed) | Section 5 OPTION B table + deviation note |
| OPTION B: B79CancelRaceGuardTests (5) | YES | Section 5 OPTION B, Section 6 |
| OPTION B: BwaveCycT1R1BeHelperTests (2) | YES | Section 5 OPTION B, Section 6 |
| OPTION B: BwaveCycTaR6HelperTests (6) | YES | Section 5 OPTION B, Section 6 |
| Exact Skip attribute text (ASCII-only) | YES | Section 2 |
| Touch ONLY CopyEngineTests.cs | YES | Section 9 constraint 1, Section 14 |
| No production code changes | YES | Section 14 (0 production files) |
| DO NOT skip currently passing tests | YES | Section 5 OPTION A mechanism, OPTION B steps |
| DO NOT double-skip already-Skipped tests | YES | Section 5, Section 9 constraint 5 |
| 7-scan checklist defined | YES (RULE-02 fixed) | Section 10 |
| SCAN-7: HardLink AND count >= 2 | YES (RULE-02 fixed) | Section 10 SCAN-7 |
| Ticket split: no cross-ticket blocking dependency | YES | Section 7, Gate Q2 |
| Lane A scope boundary enforced | YES | Section 4 (out-of-scope list), Section 6 (per-class) |
| Baseline numbers match spec | YES | Section 8 |
| Target numbers match spec | YES | Section 8 |
| Hard-link sync step (deploy-sync.ps1) | YES | Section 13, SCAN-6, SCAN-7 |
| NT8 API surface check | YES (no NT8 API used) | Section 11 |
| OPTION B: test-run-first before Skip application | YES | Section 5 OPTION B steps 1-5 |
| ASCII-only constraint in Jane Street audit | YES | Section 3 |
| No lock() | YES | Section 3, Section 9 constraint 3 |

---

## Violations

**None.**

---

## Summary

**Violations found: 0**

Both prior violations (RULE-01 and RULE-02) are fully and correctly resolved in REVISION 1. All 10 review requirements pass. All Jane Street DNA rules pass. Spec coverage is complete with all deviations explicitly documented and justified.

**Verdict: REVIEW_PASS**

Phase 2 gate OPEN. ptt-architect may proceed to Phase 3 (ticket generation: 04-tickets.md).
Max review cycles consumed: 2 of 2.
