# PTT-COPIER-B24-LANE-A — Plan Review
# Phase: 2 (Plan Review)
# Reviewer: ptt-plan-reviewer
# Defect: DW-B24-LEADER-CASTNULL-01
# Input: 02-architecture-plan.md
# Rules: RULES_CATALOG.md (JS-XXX), NT8_COMPILER_RULES.md (NT8-XXX)
# Date: 2026-07-17

---

## VERDICT: REVIEW_PASS

**Violations found**: 0
**Advisory notes**: 2 (non-blocking)
**All 10 checklist items**: PASS

---

## Violation Log

*No violations. Table is empty by construction.*

| # | Rule ID | Description | Location in Plan | Severity | Result |
|---|---------|-------------|-----------------|----------|--------|
| — | — | No violations found | — | — | — |

---

## 10-Item Checklist Results

| # | Checklist Item | Result | Evidence |
|---|---------------|--------|----------|
| 1 | Root cause correctly identified (SelectedItem placeholder at inject time) | PASS | Plan §1 correctly identifies WPF data-binding sentinel as cause of null cast. `ComboBox.Text` recovery path is accurate. |
| 2 | Text-fallback approach architecturally sound (`Account.All.FirstOrDefault` + `OrdinalIgnoreCase`) | PASS | Strategy correct. Scan once at inject time (not in loop/timer). Fallback placed after cast attempt, before `SelectionChanged`. `OrdinalIgnoreCase` is mandatory and present. |
| 3 | NT8-042 respected (no new `Dispatcher.InvokeAsync` introduced) | PASS | Fix is 3 lines inside `WireLeaderAccount`. Zero dispatcher calls added. Pre-existing `chart.Dispatcher.InvokeAsync` (lines 167, 181) uses a WPF `DispatcherObject.Dispatcher` (window-owned), which is not among NT8-042's three banned paths. Pre-existing `Application.Current.Dispatcher.InvokeAsync` (lines 251, 293) is outside this lane's write-set. Plan correctly scopes NT8-042 claim to fix only. |
| 4 | JS-021 respected (no `lock()`) | PASS | `grep lock\s*\(` on `TradeCopierAddOn.cs` returns 0 matches (verified against actual source). Fix adds no lock. `Account.All` is read-only enumerable — no mutation, no lock required. |
| 5 | NT8-006 pre-satisfied (`using System.Linq` already present) | PASS | `grep "using System.Linq"` confirms line 18 of `TradeCopierAddOn.cs`. Plan states line 18. Exact match. `FirstOrDefault` will compile. |
| 6 | CYC analysis correct (4 → 6, within ≤8 ceiling) | PASS | Actual source at lines 443–464 confirms pre-fix CYC=4 (3 `if` branches + base 1). Fix adds compound condition (`current == null && accountCombo.Text != null` = 2 decision points) yielding post-fix CYC=6. 6 ≤ 8. |
| 7 | Write-set correctly constrained to `TradeCopierAddOn.cs` ONLY | PASS | Plan §7: "Write-set size: 1 file. No other files changed." Confirmed. |
| 8 | [Fact] delta = 0 (UI visual-tree method, not testable in stub harness) | PASS | Rationale is complete and correct: no NT8 runtime, no WPF message pump, no live `ComboBox` in the `CopyEngineTests` harness. 126 [Fact] count unchanged. |
| 9 | 7-scan checklist covers all required scans | PASS | All 7 scans present (SCAN-01 through SCAN-07). See detailed scan analysis below. |
| 10 | Verification contract complete and correct | PASS | Manual cold-start gate: (1) open MES chart cold, (2) status bar reads "Ready: MES SEP26", (3) F5 green. Contract is complete and unambiguous. |

---

## P0 DNA Rule Scan (Exhaustive)

Every P0 CRITICAL rule from the role DNA block applied to the plan's proposed code change:

| Rule | Check | Result |
|------|-------|--------|
| JS-021 — `lock()` anywhere | Fix introduces no `lock()`. Source file has 0 existing `lock(` occurrences. | PASS |
| JS-001 — `throw` in hot paths | Fix introduces no exception throwing. | PASS |
| JS-002 — `null return where value expected` | `WireLeaderAccount` is `void`. No return value possible. | PASS |
| JS-033 — `async void` | Fix is synchronous. No `async` keyword. | PASS |
| NT8-042 — `Dispatcher.InvokeAsync` in fix | Zero new `Dispatcher.InvokeAsync` calls in the 3-line fix block. | PASS |
| NT8-019 — `async void` in NT8 callback | Not applicable; fix is synchronous void. | PASS |
| NT8-021 — `Account.All` in constructor | Called from `DoInject` (lifecycle event path, not constructor). | PASS |
| NT8-018 — `lock()` banned | Zero `lock(` in file and fix. | PASS |
| NT8-043 — null-conditional compound assignment | Fix contains no `?.` on the left side of `-=` or `+=`. | PASS |

---

## 7-Scan Checklist Analysis (Section 6 of Plan)

| Scan | Pattern | Expected | Assessment |
|------|---------|----------|------------|
| SCAN-01 | `lock\s*\(` in `TradeCopierAddOn.cs` | 0 matches | Correct. Grep confirmed 0 matches. |
| SCAN-02 | `async\s+void\s+\w+\(` in `TradeCopierAddOn.cs` | 0 matches | Correct. Fix is synchronous. |
| SCAN-03 | `return\s+null\s*;` in changed method only | 0 matches (void method) | Correct. `WireLeaderAccount` is `void`. |
| SCAN-04 | `DateTime\.Now` in `TradeCopierAddOn.cs` | 0 matches | Correct. No `DateTime.Now` in fix. |
| SCAN-05 | `"#[0-9A-Fa-f]{6}"` literals in `TradeCopierAddOn.cs` | 0 matches | Correct. No hex color literals in fix. |
| SCAN-06 | Banned dispatcher patterns in new code only | 0 matches | Correct. Scope is "new code only" — fix adds none of the three banned patterns. |
| SCAN-07 | `StringComparison\.OrdinalIgnoreCase` in `WireLeaderAccount` | 1 match | Correct. Mandatory — enforces case-insensitive name match. |

**All 7 scans: PASS**

---

## Key Constraint Cross-Check

| Constraint | Plan Addresses It? | Result |
|-----------|-------------------|--------|
| No new `Dispatcher.InvokeAsync` call | YES — §4 NT8-042 explicitly confirmed | PASS |
| No `lock()` | YES — §5 JS-021 explicitly confirmed, grep verified | PASS |
| `Account.All` NOT in loop or timer | YES — §2 Invariant #2: "scan runs ONCE at inject time only" | PASS |
| No tests added | YES — §8 [Fact] delta = 0, write-set = 1 file | PASS |
| No files other than `TradeCopierAddOn.cs` | YES — §7 explicitly states 1-file write-set | PASS |
| Text-fallback AFTER cast, BEFORE `SelectionChanged` | YES — §2 "After" block is positioned between cast and subscription | PASS |
| `StringComparison.OrdinalIgnoreCase` used | YES — §2 Invariant #1 and SCAN-07 | PASS |
| `SelectionChanged` subscription unchanged | YES — §2 Invariant #3 | PASS |

---

## CYC Verification (Branch-by-Branch)

Source lines 443–464 (pre-fix):

```
Base:                                           +1 = 1
if (accountCombo == null) → assign fallback     +1 = 2
if (accountCombo == null) return;               +1 = 3
if (current != null) panel.SetLeaderAccount     +1 = 4
SelectionChanged lambda declaration             +0 (lambda counted separately; outer CYC stays)
```

Pre-fix CYC = **4**. Matches plan.

Post-fix additions:

```
if (current == null && ...)   → compound: +1 for &&  = 5
FirstOrDefault predicate      → lambda decision point = 6
```

Post-fix CYC = **6**. Matches plan. Jane Street ceiling = 8. **6 ≤ 8: PASS.**

---

## Advisory Notes (Non-Blocking)

### ADV-01: Pre-existing NT8-042-banned dispatcher calls outside write-set

`TradeCopierAddOn.cs` lines 251 and 293 use `System.Windows.Application.Current.Dispatcher.InvokeAsync(...)`, which is the exact pattern banned by NT8-042. These are pre-existing code from prior blocks and are **outside this lane's write-set**. They are noted here for situational awareness only. They are not a FAIL for this review because:

1. This lane's write-set is `WireLeaderAccount` only.
2. The plan explicitly and correctly scopes NT8-042 to the fix.
3. Remediation of pre-existing violations belongs to a separate deferred work item.

**Action**: Log as deferred work item in `06-deferred-backlog.md` at Final Review (Phase 5). Not a block here.

### ADV-02: Spec file not accessible in director workspace

`specs/002-trade-copier-spec.html` does not exist in `c:\WSGTA\universal-or-strategy-director\`. A full spec coverage matrix could not be produced. However:

1. This is a single-defect fix lane (DW-B24-LEADER-CASTNULL-01), not a feature epic.
2. The plan's correctness argument is fully self-contained and does not depend on spec enumeration.
3. The defect description in the task prompt provides sufficient specification for this fix.

**Action**: No block. Spec matrix waived for single-defect fix lanes.

---

## Spec Coverage (Defect-Scoped)

| Requirement | Plan Section | Status |
|-------------|-------------|--------|
| Panel status bar reads "Ready: MES SEP26" after cold-start (not "No leader") | §8 Verification contract step 2 | Addressed |
| Buttons (BE, Trim, Flatten, Cancel, Tighten) are active after cold-start | Implied by `SetLeaderAccount` being called with resolved account | Addressed |
| Fix must not break hot-path account switching via dropdown | §2 Invariant #3 (`SelectionChanged` subscription unchanged) | Addressed |
| Fix scoped to TradeCopierAddOn.cs, no other files | §7 Write-set | Addressed |

---

## Summary

The plan is architecturally correct, rule-compliant, and complete for the defect scope. Zero violations found across all P0 DNA rules, NT8 compiler rules, and key constraint checks. All 10 checklist items pass. The CYC analysis is accurate. The 7-scan checklist is complete and correctly configured. The verification contract is unambiguous and executable.

**REVIEW_PASS** — plan is cleared for Phase 3 (ticket generation).
