# B52-LaneA Plan Review
**Block**: B52-LaneA | `test-restore-extraction`
**Reviewer**: ptt-plan-reviewer (Phase 2)
**Date**: 2026-08-08
**Input**: docs/brain/B52-LaneA/02-architecture-plan.md
**Status**: PLAN_REVIEW_PASS

---

## Section 1 — Review Verdict

| Verdict | Violations | Blocking issues |
|---------|-----------|----------------|
| **PLAN_REVIEW_PASS** | 0 | 0 |

All 10 checks passed. Zero rule violations found. Plan is approved for ticket generation (Phase 3).

---

## Section 2 — Checklist Table

| # | Check | Result | Notes |
|---|-------|--------|-------|
| 1 | DW-B50C-01: Restored assertion is specific and behavioral | **PASS** | See below |
| 2 | DW-B51-03: Branch inventory complete — all 11 branches accounted for | **PASS** | See below |
| 3 | DW-B51-03: CYC estimates correct (McCabe and Lizard both documented) | **PASS** | See below |
| 4 | NT8 Compliance — no banned constructs in any new code | **PASS** | See below |
| 5 | JS-002 — no illegal `return null` in production code; test assertion not a violation | **PASS** | See below |
| 6 | No new public API surface — both helpers are `private void` | **PASS** | See below |
| 7 | Extraction preserves all 11 branches across 3 methods (4+4+3=11) | **PASS** | See below |
| 8 | File routing correct — all 3 files are flat root in Wave workspace | **PASS** | See below |
| 9 | Build tag documented — `"PTT-COPIER B52 | test-restore-extraction | 2026-08-08"` at line 41 | **PASS** | See below |
| 10 | Scope compliance (V12.23) — only DW-B50C-01 and DW-B51-03 in scope | **PASS** | See below |

---

## Section 3 — Per-Check Evidence

### Check 1 — DW-B50C-01: Restored assertion is specific

**Source specification**: B50-LaneC/06-deferred-backlog.md §DW-B50C-01:
> "Restore with .NET 4.8-compatible nullable annotation check in a future block."

**Plan's restored test design** (Section 2.6):

1. **Type-level assertion retained** as Assertion 1 (still valid — documents the return type
   contract and is always exercised regardless of NT8 runtime availability). ✅

2. **Behavioral assertion added** as Assertion 2 — creates `new Account { Name = "B52-NULL-PATH" }`
   and invokes via reflection with `"NONEXISTENT_SIGNAL_B52", false`. The foreach in
   `FindFollowerBracketOrder` produces zero matches against a nonexistent signal name, falling
   through to `return null`. ✅

3. **`TargetInvocationException` guard** correctly handles the NT8-absent case:
   - If `Account.Orders` is null/unavailable → NRE wrapped in `TargetInvocationException` → inner
     exception is `NullReferenceException` → method silently returns (type-level assertion already
     confirmed the contract). ✅
   - Any other exception → re-thrown → test fails (correct behavior). ✅

4. **`Assert.Null(result)`** covers the path where `Account.Orders` IS available (empty collection)
   → foreach produces zero iterations → method returns null cleanly → assertion fires. ✅

5. **.NET 4.8 compatibility**: no `NullabilityInfoContext` (`.NET 6+` only), no C# 9+ syntax,
   no `init` accessors. All reflection calls, `BindingFlags`, and `TargetInvocationException` are
   available in `.NET 4.8`. ✅

6. **Test name** changed from `FindFollowerBracketOrder_NullableReturnType` to
   `FindFollowerBracketOrder_ReturnsNullWhenNoMatch` — accurately reflects the behavioral contract
   being tested. ✅

7. **CYC of test method**: try/catch (1 decision) + `if(NullReferenceException)` (1 decision) = 2
   McCabe = 3, Lizard = 2. Both ≤ 8. ✅

---

### Check 2 — DW-B51-03: Branch inventory complete

**Verification against actual code** at `TradeCopierPanel.cs` lines 1969–2021 (read directly):

| # | Plan branch | Actual code | Actual line | Match |
|---|------------|-------------|------------|-------|
| 1 | `if (cb == null) return;` — null guard | `if (cb == null) return;` | 1972 | ✅ |
| 2 | `if (cb.Items.Count > 0) return;` — idempotency | `if (cb.Items.Count > 0) return;` | 1973 | ✅ |
| 3 | `if (!_atmComboRefs.Contains(cb))` | `if (!_atmComboRefs.Contains(cb))` | 1974 | ✅ |
| 4 | `if (GetCopyMode() == CopyMode.Clone)` | `if (CopyEngine.Instance.GetCopyMode() == CopyMode.Clone)` | 1978 | ✅ |
| 5 | `if (Directory.Exists(atmDir))` — dir guard | `if (System.IO.Directory.Exists(atmDir))` | 1991 | ✅ |
| 6 | `foreach (var f in ...)` — loop | `foreach (var f in System.IO.Directory.GetFiles(atmDir, "*.xml"))` | 1993 | ✅ |
| 7 | `if (tName == leaderTemplate)` — leader match | `if (tName == leaderTemplate)` | 1997 | ✅ |
| 8 | `catch` — exception guard | `catch` | 2002 | ✅ |
| 9 | `if (defaultIdx > 0)` — auto-select guard | `if (defaultIdx > 0)` | 2010 | ✅ |
| 10 | `if (!string.IsNullOrEmpty(selName))` | `if (!string.IsNullOrEmpty(selName))` | 2013 | ✅ |
| 11 | `if (item != null)` — FollowerItem null guard | `if (item != null)` | 2017 | ✅ |

All 11 branches exactly match B51 backlog table and actual source. Zero discrepancies.

**Routing is correct**:
- Branches 5–8 (dir-exists, foreach, leader-match, catch) → `PopulateAtmComboItems` ✅
- Branches 9–11 (defaultIdx, selName, item) → `ApplyAtmAutoSelect` ✅
- Branches 1–4 (null guard, idempotency, contains, clone check) → remain in parent ✅

**`cb.SelectedIndex = defaultIdx` placement** (actual line 2006 in source):
- Located between the try/catch block (branches 5–8 source region) and the `if (defaultIdx > 0)`
  block (branches 9–11 source region).
- In post-extraction parent: placed between `PopulateAtmComboItems` call and `ApplyAtmAutoSelect`
  call. This is correct — selection must be set before `ApplyAtmAutoSelect` reads
  `cb.Items[defaultIdx]`. ✅

---

### Check 3 — DW-B51-03: CYC estimates are correct

**`PopulateAtmComboItems`**: absorbs branches 5 (if dir-exists), 6 (foreach), 7 (if tName==leader),
8 (catch).
- 4 decision points → **McCabe = 5**, **Lizard = 4**. Both ≤ 8. ✅
- Plan states exactly this and documents the McCabe/Lizard discrepancy in Section 3.3. ✅

**`ApplyAtmAutoSelect`**: absorbs branches 9 (if defaultIdx>0), 10 (if !IsNullOrEmpty), 11 (if item != null).
- 3 decision points.
- `??` null-coalescing on line 2015–2016 (`(cb.DataContext as FollowerItem) ?? FindAncestorDataContext<FollowerItem>(cb)`):
  correctly NOT counted as a McCabe branch (it is a single expression with dual resolution
  paths but no CFG branch point in standard McCabe/Lizard tooling). ✅
- **McCabe = 4**, **Lizard = 3**. Both ≤ 8. ✅
- Plan states exactly this in Section 3.4. ✅

**Post-extraction parent**: retains branches 1–4 (null guard, idempotency, contains, clone check).
- 4 decision points → **McCabe = 5**, **Lizard = 4**. Both ≤ 8. ✅
- Plan states: parent CYC = 5 (McCabe) / 4 (Lizard). ✅

**Summary table** (plan Section 3.6) is arithmetically correct:

| Method | Before | After (McCabe/Lizard) | ≤ 8? |
|--------|--------|-----------------------|------|
| `OnFollowerAtmTemplateComboLoaded` | 12 / 11 | 5 / 4 | ✅ |
| `PopulateAtmComboItems` | N/A | 5 / 4 | ✅ |
| `ApplyAtmAutoSelect` | N/A | 4 / 3 | ✅ |

---

### Check 4 — NT8 Compliance

Scan of all new code in plan against `docs/standards/NT8_COMPILER_RULES.md`:

| Rule | Description | Status |
|------|-------------|--------|
| NT8-001 | `{ get; init; }` banned | No `init` accessors in any new code ✅ |
| NT8-002 | `abstract record`/`sealed record` banned | No records ✅ |
| NT8-003 | `volatile double` banned | No volatile fields ✅ |
| NT8-004 | `ImmutableDictionary` banned | Not used ✅ |
| NT8-005 | `readonly struct` with `{ get; private set; }` banned | No readonly structs ✅ |
| NT8-007 | `CreateOrder` arg 12 type | No `CreateOrder` calls ✅ |
| NT8-013 | `DateTime.Now` for GTC orders | No `DateTime.Now` ✅ |
| NT8-014 | Signal name starts with `"PTT-"` | No new `CreateOrder` signal names ✅ |
| NT8-015 / NT8-016 | Sealed Indicator/Window banned | No new classes ✅ |
| NT8-018 | `lock()` banned | Zero `lock(` in all new code ✅ |
| NT8-019 | `async void` banned | All new methods are `void` (non-async) ✅ |
| NT8-020 | `SolidColorBrush` must be Freeze()d | No new brushes ✅ |
| NT8-028 | Hex color strings banned | No hex string literals ✅ |
| NT8-031 | `OrderState.PendingSubmit` banned | Not used ✅ |
| NT8-042 | `Dispatcher.InvokeAsync` banned in AddOn | Not used in new code ✅ |
| NT8-043 | Null-conditional compound assignment banned | Not used ✅ |
| NT8-044 | `StringComparison` requires `using System;` | Not used in new code ✅ |
| NT8-045 | `AtmStrategyTemplates` → use filesystem path | Plan reuses the existing filesystem-path pattern verbatim in `PopulateAtmComboItems` ✅ |

**`out int defaultIdx` parameter**: C# `out` parameters are legal in `.NET 4.8` / C# 7.3. The
inline out-variable declaration at the call site (`PopulateAtmComboItems(cb, leaderTemplate, out int defaultIdx)`)
is a C# 7.0 feature, fully supported under `.NET 4.8`. ✅

---

### Check 5 — JS-002: No illegal `return null`

| Location | Code pattern | JS-002 violation? | Reasoning |
|----------|-------------|------------------|-----------|
| Test: `object result = null;` | Local variable init | **NO** | Not a `return null` statement. Local variable. |
| Test: `Assert.Null(result)` | Asserting SUT returned null | **NO** | Test infrastructure checking behavioral contract of SUT. JS-002 bans *production code* from returning null for missing values. Test assertions are explicitly exempt. |
| `PopulateAtmComboItems` | `void` return | **NO** | No `return null` in method; returns via `out` parameter. |
| `ApplyAtmAutoSelect` | `void` return | **NO** | No `return null` in method. |
| `OnFollowerAtmTemplateComboLoaded` (parent) | `return;` early exits | **NO** | `return;` (void) — not `return null`. |

Zero JS-002 violations in any new or modified code. The production method `FindFollowerBracketOrder`
already contains `return null` from prior blocks — this is pre-existing code, the subject of the
test, not new code introduced by B52.

---

### Check 6 — No new public API surface

| Symbol | Access modifier | xUnit compliance | Result |
|--------|----------------|-----------------|--------|
| `PopulateAtmComboItems(ComboBox, string, out int)` | `private` | N/A — not a test | ✅ |
| `ApplyAtmAutoSelect(ComboBox, int)` | `private` | N/A — not a test | ✅ |
| `FindFollowerBracketOrder_ReturnsNullWhenNoMatch()` | `public void [Fact]` | xUnit requires public | ✅ |
| New interfaces | None | N/A | ✅ |
| New public classes | None | N/A | ✅ |

No new public API surface is introduced beyond the single required xUnit test method.

---

### Check 7 — Extraction preserves ALL 11 branches

Post-extraction branch distribution:

| Method | Branches | Total |
|--------|---------|-------|
| `OnFollowerAtmTemplateComboLoaded` (parent) | 1, 2, 3, 4 | 4 |
| `PopulateAtmComboItems` | 5, 6, 7, 8 | 4 |
| `ApplyAtmAutoSelect` | 9, 10, 11 | 3 |
| **Grand total** | | **11** ✅ |

Every branch from the pre-extraction method appears exactly once in the post-extraction design.
No branch is lost. No branch is duplicated. The behavioral contract of the original method is
fully preserved.

---

### Check 8 — File routing correct

| File | Plan path | Workspace | Subdirectory? |
|------|----------|-----------|--------------|
| `CopyEngineTests.cs` | `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs` | Wave (universal-or-strategy) | Flat root ✅ |
| `TradeCopierPanel.cs` | `C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs` | Wave (universal-or-strategy) | Flat root ✅ |
| `CopyEngine.cs` | `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` | Wave (universal-or-strategy) | Flat root ✅ |

Hard-link sync: `powershell -File scripts\verify_links.ps1 -Fix` (PTT workspace — correct). ✅
`deploy-sync.ps1` is NOT referenced (that is the V12 workspace command — correctly absent). ✅

---

### Check 9 — Build tag documented

From plan Section 7:

- **Current tag** (B51): `"PTT-COPIER B51 | ui-fixes | 2026-08-08"` (shown for context)
- **New tag** (B52): `"PTT-COPIER B52 | test-restore-extraction | 2026-08-08"`
- **Location**: `CopyEngine.cs` line 41 — `internal const string Tag` in `internal static class PttBuild`
- **Change scope**: string value only at line 41 — no structural change to `PttBuild` class ✅
- **Emission path**: `TradeCopierAddOn.cs:92` calls `PttBuild.Tag` → NT8 Output tab on inject ✅

---

### Check 10 — Scope compliance (V12.23)

Items in scope: DW-B50C-01 (P1, test restore) and DW-B51-03 (P2, extraction). ✅

Plan Appendix lists DW-B51-01 and DW-B51-02 as "out of scope for this lane" — these were
already CLOSED in B51. Their presence in the appendix is redundant documentation, not a scope
violation. ✅

Files changed: CopyEngineTests.cs (1 method replacement), TradeCopierPanel.cs (1 method
replacement + 2 new private methods), CopyEngine.cs (1 line string change). No additional
changes planned. Zero scope creep. ✅

---

## Section 4 — Spec Coverage Matrix

| Spec requirement | Source | Addressed in plan? | Plan section |
|-----------------|--------|--------------------|-------------|
| Restore DW-B50C-01: strengthen test to verify behavioral null contract | B50-LaneC/06-deferred-backlog.md §DW-B50C-01 | ✅ Yes | Section 2 |
| Restore test must be .NET 4.8 compatible | B50-LaneC implicit (.NET 4.8 codebase constraint) | ✅ Yes | Section 2.6 |
| Restore test must handle NT8-absent runtime (TargetInvocationException guard) | DW-B50C-01 context (no NT8 runtime in xUnit) | ✅ Yes | Section 2.5, 2.6 |
| Extract DW-B51-03: reduce `OnFollowerAtmTemplateComboLoaded` from CYC=12 to ≤8 | B51-LaneA/06-deferred-backlog.md §DW-B51-03 | ✅ Yes | Section 3 |
| Extraction into `PopulateAtmComboItems` (branches 5–8) | B51-LaneA/06-deferred-backlog.md §DW-B51-03 | ✅ Yes | Section 3.3 |
| Extraction into `ApplyAtmAutoSelect` (branches 9–11) | B51-LaneA/06-deferred-backlog.md §DW-B51-03 | ✅ Yes | Section 3.4 |
| Post-extraction parent CYC ≤ 5 | B51-LaneA/06-deferred-backlog.md §DW-B51-03 | ✅ Yes | Section 3.5, 3.6 |
| Both helpers must be private | B51-LaneA/06-deferred-backlog.md §DW-B51-03 | ✅ Yes | Section 3.3, 3.4 |
| No new public API | V12.23 (scope compliance) | ✅ Yes | Section 1, 3.3, 3.4 |
| Build tag update to B52 | PTT pipeline protocol | ✅ Yes | Section 7 |
| Hard-link sync after changes | AGENTS.md §2 | ✅ Yes | Section 6 |
| All 7 scans documented | PTT pipeline protocol | ✅ Yes | Section 8 |

All 12 spec requirements are addressed. Zero gaps.

---

## Section 5 — Verdict

```
PLAN_REVIEW_PASS

Violations: 0
Blocking issues: 0
Checks passed: 10/10
Spec requirements covered: 12/12

Phase 3 (ticket generation) is UNBLOCKED.
```
