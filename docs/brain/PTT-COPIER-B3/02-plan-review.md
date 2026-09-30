# PTT-COPIER-B3 Plan Review

**Reviewer:** PTT Plan Reviewer
**Date:** 2026-06-21
**Input:** 02-architecture-plan.md

## Checklist Results

### A — Completeness

| ID | Check | Result | Note |
|----|-------|--------|------|
| A1 | §1 Summary present with ticket table | PASS | T1/T2/T3 table with file and concern present |
| A2 | §2 Scope lists in-scope, out-of-scope, and files-touched table | PASS | All three subsections present with correct entries |
| A3 | §3 T1 documents all 8 changes with Before/After code and rationale | PASS | Changes 3.1–3.8 each have code and rationale; new-addition changes include insert snippets |
| A4 | §4 T2 documents all 8 changes with code and rationale | PASS | Changes 4.1–4.8 each have code and rationale |
| A5 | §5 T3 lists all 17 [Fact] method signatures with implementation notes | PASS | All 17 methods listed in table; per-group implementation notes present |
| A6 | §6 Jane Street compliance table covers JS-001, JS-010, JS-021, JS-023, JS-025 | PASS | All five rules addressed with B3-specific compliance notes |
| A7 | §7 SCAN assertions cover SCAN-01 through SCAN-12 | PASS | All 12 SCAN entries present with pattern, scope, and expected result |
| A8 | §8 Accepted Deviations present (at least D1–D4) | PASS | D1 through D4 present with justifications |
| A9 | §9 Block 4 Backlog present | PASS | Five backlog items with priority, file, and notes |

### B — T1 Correctness (CopyEngine.cs changes)

| ID | Check | Result | Note |
|----|-------|--------|------|
| B1 | `readonly` removal from `_rules` correctly specified | PASS | Change 3.1 Before/After matches source line 21 exactly; removal of `readonly` is the sole diff |
| B2 | `CopyRule.Enabled` added as `internal readonly bool Enabled` with constructor and factory update | PASS | Change 3.2 adds field, updates private constructor to 4-arg form, factory gets `bool enabled = true` default |
| B3 | Gate 2.5 inserted AFTER `if (matchedRule == null) return;` and BEFORE Gate 3 | PASS | Change 3.3 location comment confirms exact insertion point matching source structure |
| B4 | `_dailyCapFloor` default is exactly `-500.0` | PASS | Change 3.4 code shows `private double _dailyCapFloor = -500.0;` |
| B5 | `SetDailyCapFloor` placed immediately after `SetEnabled` | PASS | Change 3.5 location note states "immediately after `SetEnabled`" |
| B6 | `PassesDailyCapCheck` uses correct NT8 API, `double.MinValue` guard, `pnl > _dailyCapFloor` return | PASS | Change 3.6 After code matches all three requirements exactly |
| B7 | `SetRuleEnabled` uses snapshot-rebuild into new `ConcurrentBag`, assigns `_rules = newBag`, zero `lock()` | PASS | Change 3.7 code: List snapshot → new ConcurrentBag → foreach rebuild → `_rules = newBag`; no lock present |
| B8 | `FindRule` null guard is `if (instrument == null) return null;` as first statement | PASS | Change 3.8 specifies "Insert as first statement" with exact guard expression |

### C — T2 Correctness (TradeCopierWindow.cs changes)

| ID | Check | Result | Note |
|----|-------|--------|------|
| C1 | `_rulesPanel` promoted to class field (not local var) | PASS | Change 4.1 adds `private StackPanel _rulesPanel;` and replaces local `var rulesPanel` throughout `BuildUI` |
| C2 | `OnRuleToggle` calls `_engine.SetRuleEnabled(instrName, newState)` | PASS | Change 4.2 replacement body includes `_engine.SetRuleEnabled(instrName, newState)` |
| C3 | `OnRuleToggle` uses `is`-pattern for TextBox tag | PASS | Change 4.2 shows `btn.Tag is TextBox tb ? tb.Text : btn.Tag as string` exactly |
| C4 | `addRuleBtn.IsEnabled = true` and `addRuleBtn.Click += OnAddRule` both present | PASS | Change 4.3 specifies both; source currently has `IsEnabled = false` with no Click handler |
| C5 | `OnAddRule` appends `BuildDynamicRuleRow()` to `_rulesPanel` | PASS | Change 4.4 code is `_rulesPanel.Children.Add(BuildDynamicRuleRow())` |
| C6 | `BuildDynamicRuleRow` column 0 is `TextBox`; all action buttons use `instrTextBox` as Tag | PASS | Change 4.5 code shows `var instrTextBox = new TextBox` in col 0; all 4 action buttons carry `Tag = instrTextBox` |
| C7 | `OnRowApply` uses `is`-pattern for `tag[0]`; guards `string.IsNullOrEmpty(instrName)` | PASS | Change 4.6 shows both the `is`-pattern cast and the `string.IsNullOrEmpty` guard return |
| C8 | `OnRuleTrim`, `OnRuleFlatten`, `OnRuleCancel` all updated with `is`-pattern for TextBox Tag | PASS | Change 4.7 explicitly covers all three handlers with identical `is`-pattern replacement |

### D — T3 Correctness (CopyEngineTests.cs spec)

| ID | Check | Result | Note |
|----|-------|--------|------|
| D1 | Framework specified as xUnit only — NUnit and MSTest explicitly excluded | PASS | §5 states "xUnit ONLY. NEVER NUnit. NEVER MSTest." |
| D2 | Namespace specified as `PropTraderTools` | PASS | §5 states "Namespace: `PropTraderTools`" |
| D3 | Singleton access via `CopyEngine.Instance` only — `new CopyEngine()` explicitly forbidden | PASS | §5 states "never `new CopyEngine()`" |
| D4 | `Subscribe()` explicitly forbidden in tests | PASS | §5 states "`Subscribe()` is NOT called in any test" |
| D5 | All 17 [Fact] method names listed exactly as specified | PASS | Table rows 1–17 each have exact method name, group, and key assertion |
| D6 | `IsDedup` access via `MethodInfo` + `BindingFlags.NonPublic \| BindingFlags.Instance` specified | PASS | Tests 16–17 implementation notes show exact `GetMethod` call with both flags |
| D7 | `_dailyCapFloor` and `_rules` access via `FieldInfo.GetValue` specified | PASS | Tests 3–4 and 5–7 implementation notes both show `FieldInfo.GetValue(_engine)` pattern |
| D8 | Reset pattern `SetEnabled(false)` at start of each test specified | PASS | §5 states "each test begins with `_engine.SetEnabled(false)`" |

### E — Jane Street Compliance

| ID | Check | Result | Note |
|----|-------|--------|------|
| E1 | ZERO `lock()` in all planned changes — confirmed | PASS | `SetRuleEnabled` uses snapshot-rebuild; `SetDailyCapFloor` is single field write; no lock in any planned code |
| E2 | `ConcurrentBag` maintained in `_rules` field type — confirmed (JS-025) | PASS | Change 3.7 rebuilds into `new ConcurrentBag<CopyRule>()`; SCAN-11/12 assertions in §7 verify |
| E3 | No `throw` added to hot path (`OnOrderUpdate`) — confirmed (JS-001) | PASS | Gate 2.5 is plain `return`; `PassesDailyCapCheck` and `FindRule` guard use `return`/`return null` only |
| E4 | `private CopyEngine()` constructor unchanged — confirmed (JS-010) | PASS | §6 states "untouched"; source line 83 confirms `private CopyEngine() { }` |
| E5 | `volatile bool _isCopyEnabled` field unchanged — confirmed (JS-023) | PASS | §6 states "Field declaration unchanged"; source line 19 confirmed |

### F — Scan Coverage

| ID | Check | Result | Note |
|----|-------|--------|------|
| F1 | SCAN-01 (`lock(`) → 0 results confirmed | PASS | §7 SCAN-01 specifies 0 results across all 4 files; no lock in any planned change |
| F2 | SCAN-11 (`readonly ConcurrentBag`) → 0 results confirmed | PASS | Change 3.1 removes `readonly` from `_rules`; §7 SCAN-11 confirms 0 results in `CopyEngine.cs` |
| F3 | SCAN-12 (`_rules = new ConcurrentBag`) → 2 results confirmed | PASS | §3.1 rationale and §7 SCAN-12 both state "2 results (field init + SetRuleEnabled reassign)" |
| F4 | SCAN-08/09/10 (NUnit, MSTest, Subscribe) → 0 results confirmed | PASS | §7 rows SCAN-08/09/10 all specify 0 results; §5 explicitly prohibits all three |

---

## Summary

- Total checks: 34
- Passed: 34
- Failed: 0
- Violations: none

## Decision

REVIEW_PASS
