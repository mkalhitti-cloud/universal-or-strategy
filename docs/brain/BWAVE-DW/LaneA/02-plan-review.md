# BWAVE-DW LaneA — Plan Review

**Reviewer**: ptt-plan-reviewer  
**Phase**: Phase 2 (Plan Review)  
**Plan reviewed**: `docs/brain/BWAVE-DW/LaneA/02-architecture-plan.md`  
**Date**: 2026-09-03  
**Verdict**: **REVIEW_FAIL**

---

## Violations Found: 2

| # | Rule / Policy | Severity | Location in Plan | Description |
|---|---------------|----------|-----------------|-------------|
| V-1 | SPEC COMPLETENESS (P0) | P0 | §CYC Delta — T1 (lines 81–86) | CYC delta table self-contradicts: table row states `Before=N, After=N, Delta=0`; prose immediately below states "Removing them lowers the enclosing method's CYC by 2." The Before column contains the label "N (no change to branch count)" — not a CYC integer. Delta=0 is incorrect; the correct delta is −2. The plan must state concrete Before/After CYC integers so the engineer has an unambiguous target. |
| V-2 | NT8 SYNC STEP (mandatory per AGENTS.md §2, NT8 Sync Integrity V12.B95) | P0 | Entire plan — absent | `ptt-sync-and-verify.ps1` is not referenced anywhere in the plan (not in Acceptance Criteria, not in Key Decisions, not per-ticket). Every `src/PropTraderTools/` modification requires this step followed by F5 in NinjaTrader 8. Omitting it from the plan means the engineer has no instruction to perform the mandatory sync+verify gate. |

---

## Spec Coverage Matrix

| Spec Requirement | Addressed in Plan? | Plan Section |
|------------------|--------------------|--------------|
| A-1 (DW-C38-03): Remove Account.All loop (lines 612–614); preserve line-593 scoped disarm | ✅ YES | §T1 — Fix A-1, Exact Change |
| A-2 (DW-C39-05): Call ApplyFeatureFlags after BuildDynamicRuleRow in OnAddRule | ✅ YES | §T2 — Fix A-2, Exact Change |
| Exact old/new code shown for A-1 | ✅ YES | §T1 — Exact Change |
| Exact old/new code shown for A-2 | ✅ YES | §T2 — Exact Change |
| CYC delta stated for T1 | ❌ FAIL (V-1) | §CYC Delta — T1: table contradicts prose |
| CYC delta stated for T2 | ✅ YES | §CYC Delta — T2: CYC=1 before and after, Delta=0 |
| CYC ≤ 8 for all modified methods | ✅ YES (both methods well below 8) | §CYC Delta — T1/T2 |
| JS-021 (no lock) compliance confirmed | ✅ YES | §JS Rule Compliance Checklist |
| JS-033 (no async void) compliance confirmed | ✅ YES | §JS Rule Compliance Checklist |
| xUnit [Fact] test names listed for T1 | ✅ YES (2 tests) | §xUnit [Fact] Tests — T1 |
| xUnit [Fact] test names listed for T2 | ✅ YES (3 tests) | §xUnit [Fact] Tests — T2 |
| 7-scan checklist present for T1 | ✅ YES | §7-Scan Checklist — T1 |
| 7-scan checklist present for T2 | ✅ YES | §7-Scan Checklist — T2 |
| No scope creep (unrelated fixes bundled) | ✅ YES | Plan contains exactly A-1 and A-2 |
| NT8 sync step (ptt-sync-and-verify.ps1) referenced | ❌ FAIL (V-2) | Absent from entire plan |

---

## Lane-Split Gate Compliance

| Check | Result |
|-------|--------|
| Gate result present (`LANES-APPROVED`) | ✅ YES — stated at plan line 11 |
| Q1 = NO (not same method / not within 50 lines) | ✅ YES — Different files (TradeCopierPanel.cs vs TradeCopierWindow.cs) |
| Q2 = NO (A-2 does not depend on A-1 design) | ✅ YES — Orthogonal data paths confirmed |
| Q3 = YES (each fix has standalone value) | ✅ YES — A-1 and A-2 each independently valuable |
| Q4 = YES (each fix has independent SIM verification path) | ✅ YES — separate SIM test scenarios described |
| Gate logic: Q1=NO, Q2=NO, Q3=YES, Q4=YES → LANES-APPROVED | ✅ COMPLIANT |

---

## Required Fixes Before Re-submission

### Fix V-1 — CYC Delta Table for T1 (§CYC Delta — T1)

Replace the current table:

```
| Method | Before | After | Delta |
|--------|--------|-------|-------|
| Teardown (containing block) | N (no change to branch count) | N | 0 |
```

With a table that states concrete integers, for example:

```
| Method | Before | After | Delta |
|--------|--------|-------|-------|
| Teardown (containing block) | <actual CYC integer from source> | <actual − 2> | −2 |
```

The architect must look up the actual CYC of the enclosing teardown method and fill in real numbers. The prose note ("Removing them lowers the enclosing method's CYC by 2") is correct; the table must match it.

### Fix V-2 — NT8 Sync Step (entire plan)

Add a mandatory post-ticket step to the Acceptance Criteria (or as a separate §Post-Implementation Gate) referencing `ptt-sync-and-verify.ps1`:

```
## Post-Implementation Gate (both tickets)

After engineer commits both tickets:
1. Run `powershell -File scripts\ptt-sync-and-verify.ps1` — must report 0 MISMATCH lines.
2. Press F5 in NinjaTrader 8 — compilation must succeed with 0 errors.

This gate is mandatory per AGENTS.md §2 (NT8 Sync Integrity V12.B95) for every
src/PropTraderTools/ modification.
```

---

## Items That Pass (no action needed)

- Lane-split gate result and logic: ✅ PASS
- A-1 problem statement, exact change, acceptance criteria: ✅ PASS
- A-2 problem statement, exact change, acceptance criteria: ✅ PASS
- CYC delta for T2 (OnAddRule, CYC=1→1, Delta=0): ✅ PASS
- JS-021 compliance table: ✅ PASS
- JS-033 compliance table (event-handler exemption correctly applied): ✅ PASS
- JS-002 / JS-001 compliance: ✅ PASS
- 7-scan checklists for T1 and T2 (all 7 rows present, all PASS): ✅ PASS
- xUnit [Fact] test names with Arrange/Act/Assert outlines: ✅ PASS
- No scope creep: ✅ PASS
- Threading model analysis (Dispatcher thread for T1, UI thread for T2): ✅ PASS
- NT8 API surface for T1 (no new APIs introduced): ✅ PASS
- NT8 API surface for T2 (PTT-internal only): ✅ PASS
- Data flow diagram present: ✅ PASS
- Key decisions section present with rationale: ✅ PASS

---

## Re-submission Instructions

Fix V-1 and V-2 in `02-architecture-plan.md` and resubmit to ptt-plan-reviewer.  
This is Cycle 1. Maximum 2 cycles permitted before escalation.

---

# Cycle 2 Review

**Reviewer**: ptt-plan-reviewer  
**Cycle**: 2 (final allowed)  
**Plan reviewed**: `docs/brain/BWAVE-DW/LaneA/02-architecture-plan.md` (v2)  
**Date**: 2026-09-03  
**Verdict**: **REVIEW_PASS**

---

## Cycle-1 Violation Status

| Violation | Fix Required | Status |
|-----------|-------------|--------|
| V-1 — CYC delta table T1 lacked concrete integers | Table now reads `Before=6, After=4, Delta=-2`; prose enumerates all 6 branches and confirms branches 5 and 6 are deleted | ✅ RESOLVED |
| V-2 — NT8 sync step absent | §Post-Implementation Gate section added (lines 271–284); `ptt-sync-and-verify.ps1` referenced with `18/18 OK, 0 MISMATCH` required result; F5 NinjaTrader 8 recompile gate present | ✅ RESOLVED |

---

## Cycle 2 Full Checklist

| Check | Evidence | Result |
|-------|----------|--------|
| Lane-split gate present: Q1=NO, Q2=NO, Q3=YES, Q4=YES → LANES-APPROVED | Plan lines 11–21 | ✅ PASS |
| A-1 exact old/new code present | Plan lines 59–74 | ✅ PASS |
| A-2 exact old/new code present | Plan lines 125–140 | ✅ PASS |
| T1 CYC concrete integers: Before=6, After=4, Delta=-2 | Plan lines 82–83 table + lines 85–93 branch enumeration | ✅ PASS |
| T2 CYC concrete integers: Before=1, After=1, Delta=0 | Plan lines 151–152 table + line 154 | ✅ PASS |
| All modified methods CYC ≤ 8 | T1 peak=6, T2 peak=1 | ✅ PASS |
| JS-021 (no `lock()`) compliance confirmed for T1 and T2 | Plan line 173 | ✅ PASS |
| JS-033 (no `async void`) compliance confirmed; RoutedEventHandler exemption correctly applied | Plan line 174 | ✅ PASS |
| JS-001 (no throw in hot path) | Neither fix introduces throws | ✅ PASS |
| JS-002 (no null return) | Neither fix has a return value | ✅ PASS |
| xUnit [Fact] test names ≥ 5 total: 2 for T1 + 3 for T2 = 5 | Plan lines 185–213 | ✅ PASS |
| 7-scan checklist present for T1 (all 7 rows, all PASS) | Plan lines 219–229 | ✅ PASS |
| 7-scan checklist present for T2 (all 7 rows, all PASS) | Plan lines 231–241 | ✅ PASS |
| No scope creep (exactly A-1 and A-2, no unrelated fixes) | Plan contains only T1 and T2 | ✅ PASS |
| NT8 sync + F5 gate present | Plan lines 271–284 §Post-Implementation Gate | ✅ PASS |
| No NT8 API violations (no AtmStrategyCreate, no async lifecycle methods, no sealed class changes) | Plan analysis; T1 removes calls, T2 adds PTT-internal call only | ✅ PASS |
| Spec completeness: both A-1 (DW-C38-03) and A-2 (DW-C39-05) fully addressed | Full coverage confirmed | ✅ PASS |

**Violations found this cycle: 0**

---

## Authorization

This plan is cleared for Phase 3 (ticket generation). The engineer may proceed with T1 and T2 as specified.
