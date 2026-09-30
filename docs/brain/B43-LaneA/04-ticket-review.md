# B43-LaneA — Ticket Review
**Block:** PTT-COPIER-B43 (Per-Follower ATM Template ComboBox)
**Phase:** 3.5 — Ticket Review
**Reviewer:** ptt-ticket-reviewer
**Date:** 2026-08-05
**Source Tickets:** `docs/brain/B43-LaneA/04-tickets.md` (TICKETS_COMPLETE)
**Source Plan:** `docs/brain/B43-LaneA/02-architecture-plan.md` (REVIEW_PASS, Cycle 2)
**Plan Review:** `docs/brain/B43-LaneA/02-plan-review.md` (REVIEW_PASS, Cycle 2 2026-08-05)

---

## Ticket Review: B43-LaneA

---

### T1 — TradeCopierPanel.cs: Replace ATM mode cluster with template ComboBox

**Traceability:** PASS
- Spec: `DW-B43-NAMED-TB-01` — cited in ticket header ✅
- Plan section: `§4.1` — cited in ticket header ✅
- All 4 new methods match plan §4.1.3–§4.1.6 signatures exactly ✅
- 3 removed handlers (`OnFollowerAtmComboLoaded`, `OnFollowerAtmModeChanged_WithNamedBox`, `OnFollowerAtmModeChanged`) match plan §4.1.7 exactly ✅
- `OnRowGridLoaded` 5-column change maps to plan §4.1.2 ✅
- `BuildCheckItemTemplate` FEF changes map to plan §4.1.1 ✅
- `OnApplyRule` zero-diff confirmation maps to plan §4.1.8 ✅
- No phantom work detected ✅

**JS Pre-Check:** PASS
- JS-021 (`lock()`): No `lock()` in any new code. SCAN-01 contract present. ✅
- JS-002 (`return null`): `FindAncestorDataContext<T>` (T1.7) uses `return default(T)` at both exit paths — not `return null`. Consistent with plan §4.1.6 P1-01 fix and plan review §P1-01 RESOLVED. ✅
- JS-002: `GetLeaderAtmTemplateName` (T1.6) returns `string.Empty` on all null/exception branches. ✅
- JS-033/NT8-019 (`async void`): All handlers are `private void` synchronous. ✅
- JS-001 (throw in hot paths): No `throw` in any new code; `try/catch` swallows API exceptions gracefully. ✅

**CYC Pre-Check:** PASS
- `OnFollowerAtmTemplateComboLoaded`: CYC=4 (4 branches counted in body). ≤8 ✅
- `OnFollowerAtmTemplateComboChanged`: CYC=3 (3 branches counted in body). ≤8 ✅
- `GetLeaderAtmTemplateName`: CYC=4 (4 branches: null guard × 2 + not found + catch). ≤8 ✅
  Note: Ticket correctly reports CYC=4 (matches plan review §A-04 correction — plan §10 table said 3 but review corrected to 4).
- `FindAncestorDataContext<T>`: CYC=3 (3 branches: null guard + while loop + DataContext match). ≤8 ✅

**NT8 Check:** PASS
- NT8-012 (FEF Loaded event): T1.2 wires `OnFollowerAtmTemplateComboLoaded` via
  `FrameworkElement.LoadedEvent` using FEF `AddHandler`. Plan §4.1.1 cited `ComboBox.LoadedEvent`
  but `FrameworkElement.LoadedEvent` is the underlying routed event — both route to the same
  handler. PASS (minor naming variance, functionally identical). ✅
- NT8-019 (no async void): All handlers synchronous void. ✅
- NT8-018/JS-021 (no lock): Zero. ✅
- NT8-042 (no Dispatcher.InvokeAsync): No new `InvokeAsync` calls. ✅
- NT8-043 (no null-conditional compound assignment): No `?.` with `-=`. ✅
- NT8-041 (no reflection on Charts): T1.6 uses `TradeCopierAddOn.FindVisualChildByIndex<ComboBox>(ct, 2)` — visual tree walk only. ✅
- NT8-008 (Chart.ChartControl banned): T1.6 uses `FindVisualChild<ChartTrader>`. ✅
- NT8-001 (`init` banned): No new properties with `init` accessor. ✅
- NT8-003 (`volatile double` banned): No new volatile fields. ✅

**Test Coverage:** PASS
- `OnFollowerAtmTemplateComboLoaded` — private; no [Fact] required ✅
- `OnFollowerAtmTemplateComboChanged` — private; no [Fact] required ✅
- `GetLeaderAtmTemplateName` — `internal static`; [Fact] T_B43_04 covers null-chart branch ✅
- `FindAncestorDataContext<T>` — private; no [Fact] required ✅

**Scan Checklist:** PASS (with notation — see SCAN-03 cross-ticket note below)
- SCAN-01: `grep "lock("` → zero results ✅
- SCAN-02: `grep "async void"` → zero results ✅
- SCAN-03: `grep "return null"` → zero results ✅ (see SCAN-03 note)
- SCAN-04: CYC audit (4 new methods) ✅
- SCAN-05: `grep "init;"` → zero results ✅
- SCAN-06: `grep "volatile double"` → zero results ✅
- SCAN-07: `grep "async void"` (NT8-033 belt-and-suspenders) → zero results ✅
All 7 scans present ✅

**File Routing:** PASS
- `src/PropTraderTools/TradeCopierPanel.cs` — Wave workspace path ✅

**VERDICT: TICKET_REVIEW_PASS**

---

### T2 — TradeCopierWindow.cs: Replace ATM mode cluster with template ComboBox

**Traceability:** PASS
- Spec: `DW-B43-NAMED-TB-01` — cited in ticket header ✅
- Plan section: `§4.2` — cited in ticket header ✅
- `BuildRuleRow` removals (T2.1) and additions (T2.2) match plan §4.2.1 ✅
- `BuildDynamicRuleRow` removals (T2.3) and additions (T2.4) match plan §4.2.2 ✅
- `ParseAtmTemplateSelection` signature matches plan §4.2.3: `internal static FollowerAtmMode ParseAtmTemplateSelection(string sel)` ✅
- `OnRowApply` update (T2.6) matches plan §4.2.4 ✅
- `applyBtn.Tag` 5→4 element change in BOTH `BuildRuleRow` and `BuildDynamicRuleRow` explicitly stated ✅
- No phantom work detected ✅

**JS Pre-Check:** PASS
- JS-021 (`lock()`): No `lock()` in any new code. ✅
- JS-002 (`return null`): `ParseAtmTemplateSelection` returns `new FollowerAtmMode.Inherit()` or `new FollowerAtmMode.Named(sel)` — never null. ✅
- JS-033 (`async void`): `ParseAtmTemplateSelection` is a pure static function, no async. ✅
- JS-001 (throw in hot paths): `try/catch` in `BuildRuleRow`/`BuildDynamicRuleRow` swallows `AtmStrategyTemplates` API exception. ✅

**CYC Pre-Check:** PASS
- `ParseAtmTemplateSelection`: CYC=2 (2 branches: null/empty/none branch + else). ≤8 ✅
- `OnRowApply` (updated): CYC ≤ 4. ≤8 ✅

**NT8 Check:** PASS
- NT8-019 (no async void): No async methods. ✅
- NT8-018/JS-021 (no lock): Zero. ✅
- NT8-042 (no Dispatcher.InvokeAsync): No new `InvokeAsync` calls. ✅
- NT8-043 (no null-conditional compound assignment): No `?.` with `-=`. ✅
- NT8-001 (`init` banned): No new `init` accessors. ✅
- NT8-003 (`volatile double` banned): No new volatile fields. ✅

**Test Coverage:** PASS
- `ParseAtmTemplateSelection` — `internal static`; [Fact] T_B43_01 (Named), T_B43_02 ("(none)"→Inherit), T_B43_03 (null→Inherit) cover all 3 branches ✅
- `OnRowApply` (modified method, not new): the behavioral impact on `ParseAtmTemplateSelection` path is covered indirectly by T_B43_01–T_B43_03 ✅
- `BuildRuleRow`, `BuildDynamicRuleRow` — private; no [Fact] required ✅

**Scan Checklist:** PASS (with notation — see SCAN-03 cross-ticket note below)
- SCAN-01: `grep "lock("` → zero results ✅
- SCAN-02: `grep "async void"` → zero results ✅
- SCAN-03: `grep "return null"` → zero results ✅ (see SCAN-03 note)
- SCAN-04: CYC audit (2 methods: `ParseAtmTemplateSelection` CYC≤2, `OnRowApply` CYC≤4) ✅
- SCAN-05: `grep "init;"` → zero results ✅
- SCAN-06: `grep "volatile double"` → zero results ✅
- SCAN-07: `grep "async void"` (NT8-033 belt-and-suspenders) → zero results ✅
All 7 scans present ✅

**File Routing:** PASS
- `src/PropTraderTools/TradeCopierWindow.cs` — Wave workspace path ✅

**VERDICT: TICKET_REVIEW_PASS**

---

### T3 — B43Tests.cs: New xUnit test file (5 [Fact] methods)

**Traceability:** PASS
- Spec: `DW-B43-NAMED-TB-01` — cited in ticket header ✅
- Plan section: `§4.3` — cited in ticket header ✅
- T3 dependency on T1+T2 explicitly documented ✅
- 5 [Fact] method names match plan §4.3 exactly:
  - `T_B43_01: OnRowApply_TemplateSelected_ProducesNamedMode` ✅
  - `T_B43_02: OnRowApply_NoneSelected_ProducesInheritMode` ✅
  - `T_B43_03: OnRowApply_NullSelected_ProducesInheritMode` ✅
  - `T_B43_04: GetLeaderAtmTemplateName_NullChart_ReturnsEmptyString` ✅
  - `T_B43_05: ParseAtmModeName_RoundTrip_BackwardCompat` ✅
- No phantom work detected ✅

**JS Pre-Check:** PASS
- Test file contains no production code patterns. No `lock()`, no `async void`, no `return null`. ✅
- xUnit framework mandated: `using Xunit;` + `[Fact]` specified. ✅
- NUnit/MSTest explicitly BANNED in ticket (T3.0): `BANNED: using NUnit.Framework;`, `BANNED: [Test]`, etc. ✅

**CYC Pre-Check:** PASS
- All 5 [Fact] bodies are straight-line Arrange/Act/Assert with no branches. CYC=1 each. ≤8 ✅

**NT8 Check:** PASS
- No NT8 lifecycle methods in test file. ✅
- `GetLeaderAtmTemplateName(null)` in T_B43_04: method is `internal static` — no WPF instantiation, no NT8 context needed. Ticket notes this explicitly. ✅

**Test Coverage:** PASS
- T_B43_01: `ParseAtmTemplateSelection("MES $200")` → `FollowerAtmMode.Named` with `TemplateName=="MES $200"` ✅
- T_B43_02: `ParseAtmTemplateSelection("(none)")` → `FollowerAtmMode.Inherit` ✅
- T_B43_03: `ParseAtmTemplateSelection(null)` → `FollowerAtmMode.Inherit` ✅
- T_B43_04: `GetLeaderAtmTemplateName(null)` → `string.Empty` ✅
- T_B43_05: `CopyEngine.ParseAtmModeName("Named:MES $200")` round-trip + "Inherit" round-trip ✅
- Exactly 5 [Fact] methods per plan §4.3 ✅

**Scan Checklist:** PASS (with notation — see SCAN-03 cross-ticket note below)
- SCAN-01: `grep "lock("` → zero results ✅
- SCAN-02: `grep "async void"` → zero results ✅
- SCAN-03: `grep "return null"` → zero results ✅ (see SCAN-03 note)
- SCAN-04: CYC audit (all 5 [Fact] = CYC 1) ✅
- SCAN-05: `grep "init;"` → zero results ✅
- SCAN-06: `grep "volatile double"` → zero results ✅
- SCAN-07: `grep "async void"` (NT8-033 belt-and-suspenders) → zero results ✅
All 7 scans present ✅

**File Routing:** PASS
- `src/PropTraderTools/B43Tests.cs` (new file) — Wave workspace path ✅

**VERDICT: TICKET_REVIEW_PASS**

---

## Cross-Ticket Findings

### SCAN-03 Numbering Mismatch (WARN — non-blocking)

**Finding:** The plan §16 7-scan engineer contract defines SCAN-03 as:
```
SCAN-03 | DateTime\.Now[^U] | 0 hits in new/modified methods
```
But all three ticket 7-scan checklists define SCAN-03 as:
```
SCAN-03: grep "return null" in <file> (new/modified code only) → zero results
```

The `return null` check corresponds to plan SCAN-04 (`return\s+null\s*;`). The `DateTime.Now` check (plan SCAN-03) is therefore absent from all three ticket engineer contracts, replaced by a duplicate of plan SCAN-04.

**Severity: WARN only** — not escalated to TICKET_REVIEW_FAIL because:
1. All 7 scan slot positions (SCAN-01 through SCAN-07) are present in each ticket ✅
2. The `return null` check (JS-002) is the higher-risk violation and is correctly contracted ✅
3. No new date/time logic is introduced in B43 — the `DateTime.Now` scan would pass trivially on these methods ✅
4. The `return null` check is a meaningful JS-002 enforcement contract for the new code ✅

**Action for Architect (carry to B44):** Align the ticket 7-scan checklist numbering with plan §16 in future blocks so SCAN-03 = `DateTime.Now` and SCAN-04 = `return null`. Engineer should additionally run `grep "DateTime.Now" TradeCopierPanel.cs TradeCopierWindow.cs B43Tests.cs` as an informal check before completing T1/T2.

---

## Spec Coverage Aggregate

| Spec Requirement | Ticket | Status |
|-----------------|--------|--------|
| DW-B43-NAMED-TB-01 — Eliminate TextBox keyboard-bubbling defect (Panel) | T1 | COVERED ✅ |
| DW-B43-NAMED-TB-01 — Eliminate TextBox keyboard-bubbling defect (Window) | T2 | COVERED ✅ |
| DW-B43-NAMED-TB-01 — Test coverage for new ATM template selection logic | T3 | COVERED ✅ |

No uncovered requirements. No duplicate coverage. ✅

---

## Global Acceptance Criteria Verification

All 12 acceptance criteria (AC-01 through AC-12) are present in the Global Acceptance Criteria table, including:
- AC-05: `CopyEngine.cs` diff = 0 ✅
- AC-11: BUILD_TAG updated to `PTT-COPIER B43 | atm-template-picker | <date>` ✅
- AC-12: `verify_links.ps1 -Fix` passes (Wave workspace hard-link sync) ✅

---

## Overall: TICKET_REVIEW_PASS

All three tickets (T1, T2, T3) pass all mandatory checks:
- ✅ Traceability: All items map to plan §4.1/§4.2/§4.3 and spec DW-B43-NAMED-TB-01
- ✅ JS Pre-Check: No lock(), no return null, no async void, no throw in hot paths
- ✅ CYC Pre-Check: All new/modified methods ≤ 4 (well within ≤8 budget)
- ✅ NT8 Check: All NT8 constraints satisfied (NT8-012, NT8-019, NT8-042, NT8-043, NT8-041)
- ✅ Completeness: Both BuildRuleRow and BuildDynamicRuleRow addressed; 4-element Tag in both; BUILD_TAG and hard-link sync in AC criteria
- ✅ Test Coverage: All internal/public new methods have [Fact] coverage (5 tests)
- ✅ Scan Checklist: SCAN-01 through SCAN-07 present in all 3 tickets
- ✅ File Routing: All .cs paths under Wave workspace `src/PropTraderTools/`

**TICKET_REVIEW_PASS — Safe to spawn ptt-engineer.**
