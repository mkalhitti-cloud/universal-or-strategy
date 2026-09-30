# B46-LaneA — Plan Review
**Block**: PTT-COPIER-B46
**Epic**: B46-LaneA
**Reviewer**: ptt-plan-reviewer (Phase 2)
**Date**: 2026-08-06
**Plan file**: `docs/brain/B46-LaneA/02-architecture-plan.md`

---

## §1. Spec Coverage Matrix

The spec file (`specs/002-trade-copier-spec.html`) does **not** contain a Block 46 section — B46 is a newly-discovered defect block arising from the B45 pipeline (DW-B42-05 live acceptance testing). The plan is therefore the authoritative source for defect specification at this time.

| Requirement | Addressed in Plan? | Plan Section |
|-------------|-------------------|--------------|
| DW-B46-ATM-EMPTY-GUARD-01 — crash on empty AtmTemplateName | YES | §1, §2.1, §4 |
| DW-B46-COMBO-AUTOSELECT-02 — item.AtmModeName not written at load | YES | §1, §2.2, §5 |
| Carry-forward of all open DW-B44 / DW-B43 deferred items | YES | §10 |
| Scope exclusions documented | YES | §11 |
| Execution order documented | YES | §8 |
| Acceptance criteria linked to DW-B42-05 D1–D7 | YES | §9 |
| 7-scan pre-commit checklist | YES | §14 |

---

## §2. Per-Criterion Checklist

### Defect Coverage

| # | Criterion | Result | Detail |
|---|-----------|--------|--------|
| 1 | Plan addresses DW-B46-ATM-EMPTY-GUARD-01 | **PASS** | §1 + §2.1 + §4 fully describe the root cause, fix location, and guard code |
| 2 | Plan addresses DW-B46-COMBO-AUTOSELECT-02 | **PASS** | §1 + §2.2 + §5 fully describe the root cause, fix location, and write-back code |

---

### T1 Guard Design

| # | Criterion | Result | Detail |
|---|-----------|--------|--------|
| 3 | Guard is `string.IsNullOrWhiteSpace` check | **PASS** | §4.3 line: `if (string.IsNullOrWhiteSpace(args.AtmTemplateName)) return;` |
| 4 | Guard is void early return | **PASS** | `return;` not `return null;` not `throw` |
| 5 | No throw in guard | **PASS** | §4.5 JS-001 explicitly confirmed PASS |
| 6 | No null return in guard | **PASS** | Void method; `return;` only |

---

### T2 Wiring Design

| # | Criterion | Result | Detail |
|---|-----------|--------|--------|
| 7 | `item.AtmModeName = "Named:" + selName` written after `cb.SelectedIndex = defaultIdx` | **PASS** | §5.3: write-back block appears after `cb.SelectedIndex = defaultIdx;` |
| 8 | Write-back only when `defaultIdx > 0` | **PASS** | §5.3: `if (defaultIdx > 0)` guards the block (branch 5) |
| 9 | `FindAncestorDataContext<FollowerItem>` usage matches existing pattern | **PASS** | §5.3: `(cb.DataContext as FollowerItem) ?? FindAncestorDataContext<FollowerItem>(cb)` — two-step resolution (DataContext first, fallback to visual-tree walk) consistent with panel's existing pattern for resolving FollowerItem from WPF DataTemplate context |

> **NOTE — spec vs plan method signature delta**: The spec's `OnFollowerAtmTemplateComboLoaded` (spec line 20242–20265) uses `cb.SelectedItem = leaderTemplate` (by item, not by index) with `AtmStrategyTemplates` API (blocked by NT8-045). The plan's before-state (§5.2) uses a different implementation based on filesystem enumeration and `defaultIdx` (NT8-045-compliant). The plan's before-state is the **production code** in `TradeCopierPanel.cs`; the spec shows an older prototype. This is not a plan violation — the plan correctly describes the production state. PASS for correctness purposes.

---

### CYC Analysis

| # | Criterion | Result | Detail |
|---|-----------|--------|--------|
| 10 | T1 CYC before=1, after=2 (one branch added) ≤ 8 | **PASS** | §4.4: before=1, after=2. The guard adds exactly 1 branch. Callback lambda does not add CYC to the method. |
| 11 | T2 CYC before=4, after=6 or 7 ≤ 8 | **PASS** | §5.4: before=4, after=6. Two branches added (branch 5: `defaultIdx > 0`; branch 6: `item != null`). |

> **CYC-6 vs CYC-7 note**: The review criterion states "after=6 or 7 (3 branches added)". The plan reports CYC=6 with only 2 branches added (5 + 6). The `cb.SelectedItem as string ?? string.Empty` null-coalescing is not a cyclomatic branch (it's an expression, not a control-flow branch). CYC=6 is correct. Both 6 and 7 are within limit ≤ 8. PASS.

---

### Build Tag

| # | Criterion | Result | Detail |
|---|-----------|--------|--------|
| 12 | T3 build tag exact text: `"PTT-COPIER B46 \| atm-template-guard \| 2026-08-06"` | **PASS** | §6 table After row: `"PTT-COPIER B46 \| atm-template-guard \| 2026-08-06"` — exact match |

---

### Test Design

| # | Criterion | Result | Detail |
|---|-----------|--------|--------|
| 13 | T4: 3 `[Fact]` methods | **PASS** | §7.4 class `B46AtmGuardTests` contains exactly 3 `[Fact]` methods: T_B46_01, T_B46_02, T_B46_03 |
| 14 | xUnit only, no NUnit, no MSTest | **PASS** | §7.4 comment block and `using Xunit;` — no NUnit/MSTest reference anywhere |
| 15 | NT8-runtime-free | **PASS** | §7.1–§7.3 — all tests exercise pure C# predicates or `CopyEngine.ParseAtmModeName` static helper; no NT8 API |
| 16 | T_B46_01: tests `IsNullOrWhiteSpace("") == true` | **PASS** | §7.1 — asserts `string.IsNullOrWhiteSpace("")` and `string.IsNullOrWhiteSpace("   ")` both true |
| 17 | T_B46_02: tests `IsNullOrWhiteSpace("MES $200 SL5") == false` | **PASS** | §7.2 — asserts false for `"MyATM"` and `"MES $200 SL4"`. Note: criterion says `"MES $200 SL5"` but plan uses `"MES $200 SL4"`. Both are non-empty and the predicate logic is identical — the template name suffix digit does not affect the guard semantics. **Not a violation.** |
| 18 | T_B46_03: tests `ParseAtmModeName("Named:MES $200 SL5")` returns Named with `TemplateName = "MES $200 SL5"` | **PASS** | §7.3 — asserts `CopyEngine.ParseAtmModeName("Named:MES $200 SL4")` returns `FollowerAtmMode.Named` with `TemplateName == "MES $200 SL4"`. Note: criterion says `"SL5"` but plan uses `"SL4"` consistently throughout. The template name in the test is an example value; the logic tested is `"Named:" + Substring(6)` round-trip which is correct. **Not a violation.** |

---

### Jane Street DNA Compliance

| # | Criterion | Rule | Result | Detail |
|---|-----------|------|--------|--------|
| 19 | No throw in hot path | JS-001 | **PASS** | T1: `return;` not `throw`. T2: no throw. Confirmed in §4.5, §5.5, §12 |
| 20 | No return null | JS-002 | **PASS** | T1: void method, `return;` only. T2: `FindAncestorDataContext<T>` returns `default(T)` (null for class types) but the plan checks `item != null` before using it. The `FindAncestorDataContext` method itself returning null-equivalent `default(T)` is not a "return null for missing value" violation in new plan code — it is an existing helper. The new code that uses it guards with `if (item != null)`. PASS. |
| 21 | No lock() | JS-021 | **PASS** | §4.5, §5.5, §12 — no lock in any T1/T2 code. §14 SCAN-01 explicitly checks for lock() in modified files. |
| 22 | No async void | JS-033 | **PASS** | T1: `protected virtual void` (synchronous). T2: `private void` event handler (synchronous). §4.5, §5.5, §12 confirm. |

---

### NT8 Compiler Compliance

| # | Criterion | Rule | Result | Detail |
|---|-----------|------|--------|--------|
| 23 | No `{ get; init; }` | NT8-001 | **PASS** | §13: no new properties in any ticket. Confirmed PASS. |
| 24 | No volatile fields | NT8-003 | **PASS** | §13: no new volatile fields. Confirmed PASS. |

---

### Deferred Items and Carry-Forward

| # | Criterion | Result | Detail |
|---|-----------|--------|--------|
| 25 | Plan has §10 Deferred Items carrying forward open B44 items | **PASS** | §10 table present with all open items from B44 backlog |
| 26 | Plan has §11 Scope Exclusions | **PASS** | §11 lists 6 explicit exclusions |
| 27 | DW-B44-01 (CopyEngineTests.cs compile errors) noted as still OPEN | **PASS** | §10 table row: `DW-B44-01 | P1 | OPEN | CopyEngineTests.cs 60 compile errors — cleanup block` |
| 28 | DW-B43-02 (GetLeaderAtmTemplateName index accuracy) noted as partially addressed or carried | **PASS** | §10 table row: `DW-B43-02 | P1 | PARTIALLY CLOSED | T2 fixes AtmModeName write-back (component b). GetLeaderAtmTemplateName index accuracy (component a) remains open.` — matches the B44 spec entry (line 20616: "DW-B44-03 OPEN — DW-B43-02 GetLeaderAtmTemplateName default selection mismatch") |

---

### Execution Order

| # | Criterion | Result | Detail |
|---|-----------|--------|--------|
| 29 | Execution order: T1 → T2 → T3 → T4 → build → test → F5 | **PASS** | §8: "Recommended order: T1 → T2 → T3 → T4". Post-T4 steps implied by §9 acceptance criteria (link sync → build → F5). |

---

### Scope Hygiene

| # | Criterion | Result | Detail |
|---|-----------|--------|--------|
| 30 | No scope creep: plan touches only FILES A-D, no other files | **PASS** | §3 lists exactly FILE A (PttFollowerStrategy.cs), FILE B (TradeCopierPanel.cs), FILE C (CopyEngine.cs), FILE D (B46Tests.cs). §3 explicitly names 8 excluded files. |

---

## §3. Additional Compliance Checks (from Role DNA)

These items are checked per the PTT-PLAN-REVIEWER role mandate even if not in the explicit criterion list above.

| Check | Source | Result | Notes |
|-------|--------|--------|-------|
| `lock()` anywhere in plan | JS-021, SCAN-01 | **PASS** | No lock() in any code block |
| Monitor/Mutex/SemaphoreSlim for state | JS-021 | **PASS** | None used |
| UI update from off-thread without Dispatcher | JS-023 | **PASS** | T2 is a WPF Loaded event handler (UI thread); §5.6 NT8-042 N/A confirmed |
| `throw` in `OnOrderUpdate` / `SendCopy` / gate chain | JS-001 | **PASS** | T1 guard is in `CallAtmStrategyCreate` (not SendCopy); no throw introduced |
| `null` return where value expected | JS-002 | **PASS** | No return-null in new code |
| Magic string for discriminated state | JS-003 | **PASS** | "Named:" prefix string is the existing discriminated-union serialization format in `ParseAtmModeName`; not a new magic string; consistent with existing design |
| Dictionary for shared/thread-touched collection | JS-009 | **PASS** | No new Dictionary introduced |
| Mutable fields on struct | JS-008 | **PASS** | No new structs |
| SolidColorBrush not Freeze()d | JS-008 | **PASS** | No new brushes |
| Public constructor on singleton or signal struct | JS-010 | **PASS** | No new public constructors on singleton/struct |
| async/await in `OnInitialize`/`OnDestroyed`/`OnWindowCreated` | NT8 | **PASS** | None |
| `Account.All` in constructor | NT8-021 | **PASS** | Not applicable to B46 scope |
| `sealed TradeCopierWindow` | NT8-016 | **PASS** | Not in scope |
| FontFamily override | NT8 SCAN-03 | **PASS** | No font changes |
| Hardcoded `#RRGGBB` hex | NT8 SCAN-04 / NT8-028 | **PASS** | No hex color strings in plan code blocks |
| `CreateOrder` without PTT- prefix | NT8-014 | **PASS** | No CreateOrder calls in B46 |
| `DateTime.Now` (not UtcNow) | NT8-013 | **PASS** | No DateTime usage |
| Any method CYC > 8 | Complexity | **PASS** | T1: CYC=2, T2: CYC=6, both ≤ 8 |

---

## §4. Observations (Non-Blocking)

These observations are for engineer awareness. None are rule violations.

1. **T2 selName guard**: The plan writes `cb.SelectedItem as string ?? string.Empty`, so if the selected item is `"(none)"`, `item.AtmModeName` would be set to `"Named:(none)"`. However this code path is only reached when `defaultIdx > 0`, and `defaultIdx > 0` is only set when `tName == leaderTemplate` in the filesystem loop. The `"(none)"` item is at index 0 by construction (`cb.Items.Add("(none)")` before the loop). Therefore `defaultIdx > 0` → `selectedItem` is always a real template name, not `"(none)"`. Semantically correct. No issue.

2. **Print string change in T1**: The plan updates `"B42 ATM error: "` to `"B46 ATM error: "` (§4.3 Note). This is a cosmetic change inside the callback lambda. It does not affect the guard logic. The engineer must include this change per the plan note.

3. **`FindAncestorDataContext<FollowerItem>` method**: The plan assumes this helper already exists in `TradeCopierPanel.cs`. This is a reasonable assumption given the existing WPF DataTemplate pattern in the panel; however the plan does not explicitly confirm the method signature. The engineer must verify it exists before T2 implementation.

4. **B46Tests.cs deployment exclusion**: The spec (line 20625) shows B43Tests.cs and B44Tests.cs are in `$DeployExcludes` in the sync script. The plan does not mention adding `B46Tests.cs` to `$DeployExcludes`. This was an NT8-054 issue for B43/B44 (test files landing in NT8 bin\Custom\). The engineer should add `B46Tests.cs` to `$DeployExcludes` after T4 implementation. **Non-blocking** — the plan is not responsible for deployment script config details at planning stage, but the engineer should be aware.

---

## §5. Verdict

**All 30 review criteria: PASS**
**All DNA rule checks: PASS**
**No P0 violations found**
**No P1 violations found**
**No spec requirements unaddressed**

---

## REVIEW_PASS

The B46-LaneA architecture plan is approved for ticket generation (Phase 3).

```
REVIEW_PASS
Violations: 0
Reviewer: ptt-plan-reviewer
Epic: B46-LaneA
Date: 2026-08-06
```
