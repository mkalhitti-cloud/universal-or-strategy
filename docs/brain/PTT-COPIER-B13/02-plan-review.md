# PTT-COPIER-B13 Plan Review

**Reviewer**: ptt-plan-reviewer
**Cycle**: 2 (R2 revised plan)
**Date**: 2026-07-12
**Plan**: docs/brain/PTT-COPIER-B13/02-architecture-plan.md (revision R2)
**Spec**: specs/002-trade-copier-spec.html
**Prior backlog**: docs/brain/PTT-COPIER-B12/06-deferred-backlog.md
**Cycle 1 violations fixed**: V-01 (T3 spec traceability), V-02 (T2 Assert absent)

---

## Verdict: REVIEW_PASS

---

## Cycle 1 Violation Resolution

| Violation | Status | Evidence |
|-----------|--------|----------|
| V-01 SPEC-TRACEABILITY: T3 was ATR enable/disable CheckBox (not spec-traceable) | **RESOLVED** | R2 T3 is now DW-B12-DEFER-03 (Math.Clamp comment + NT8-031 rule). Plan §5.1 quotes the exact B12 backlog entry verbatim. B12 backlog row DW-B12-DEFER-03 text matches word-for-word. No CopyEngine change, no CheckBox, no UI. |
| V-02 TEST-ASSERTION: T2 [Fact] had no Assert.* call | **RESOLVED** | R2 test `UpdateAtrFraction_ForwardsToEngine_WhenEngineSet` at plan §4.4 contains `Assert.Equal(5, qty)`. The assertion observes `GetSuggestedQty(null)` return value, distinguishing the enabled-engine path (qty=5) from the disabled/null fallback (qty=1). |

---

## Checklist Results

### Spec Traceability

| Item | Result | Notes |
|------|--------|-------|
| T1 maps to DW-B12-DEFER-01 (wire GetRefPrice) | PASS | Spec line 7424 lists DW-B12-DEFER-01 as B13 target. Plan §3 implements `_instrument.MarketData.Last.Price` with triple null guard. B12 backlog row DW-B12-DEFER-01 confirmed. |
| T2 maps to DW-B12-DEFER-02 (startup sync NotifyRiskChanged+NotifyAtrFractionChanged) | PASS | Spec line 7424 lists DW-B12-DEFER-02 as B13 target. Plan §4 appends two calls to `OnLoaded()`. B12 backlog row DW-B12-DEFER-02 ("startup sync -- push panel initial values to AtrSizingEngine at OnLoaded") confirmed. |
| T3 maps to DW-B12-DEFER-03 (Math.Clamp comment fix + NT8-031 rule entry) -- NOT an ATR UI toggle | PASS | Plan §5.1 quotes B12 backlog DW-B12-DEFER-03 exactly: "Correct Math.Clamp ban comment misattribution... Add NT8-031." No CheckBox, no CopyEngine.SetAtrEnabled, no UI change. V-01 resolved. |
| No tickets invented beyond spec/backlog scope | PASS | T1+T2 are spec line 7424 targets. T3 is a P3 docs fix from OPEN B12 backlog row. No invented features. |
| DW-B9-01 (ATR canvas) shelved to B14 | PASS | Plan §1 Shelved table; §11 Forward Roadmap. |
| DW-B9-03 (Click-trader offset) shelved to B14 | PASS | Plan §1 Shelved table; §11 Forward Roadmap. |

### Architecture Soundness

| Item | Result | Notes |
|------|--------|-------|
| T1: GetRefPrice() wired to `_instrument.MarketData.Last.Price` | PASS | Triple null guard (instrument / MarketData / Last). Returns `0.0` on any null. `.Last.Price` is `double`. NT8-032 applied correctly per plan §3.3. |
| T1: CYC=4 | PASS | 3 if-return null guards + 1 normal return = CYC 4. Correct. |
| T2: `NotifyRiskChanged()` + `NotifyAtrFractionChanged()` appended to `OnLoaded()` | PASS | Plan §4.2: two straight-line calls after `LoadAtmTemplates()`. No branching, CYC unchanged. |
| T2: `_atrEngine` null safety at OnLoaded time | PASS | Plan §4.2 explains: `StartAtrEngine` fires before `Loaded` event. Existing null guards in `UpdateAtrFraction`/`UpdateMaxRisk` cover any edge case. Safe. |
| T3: Comment-only edit in AtrSizingEngine.cs + docs edit in NT8_COMPILER_RULES.md | PASS | Plan §5.2 (comment) and §5.3 (rule row). No new methods, no new fields, no UI, no logic change. |
| CopyEngine READ ONLY for this block | PASS | Plan §2 component map explicitly marks CopyEngine as "READ ONLY -- no changes." |

### JS Rules (P0 Hard Blockers)

| Rule | Result | Notes |
|------|--------|-------|
| JS-021: no `lock()` | PASS | No `lock(` in any code snippet in the plan. |
| JS-033: no `async void` | PASS | No `async` keyword anywhere. All handlers are `private void`. |
| JS-001: no exception throws on hot paths | PASS | No `throw` anywhere. Null guards return `0.0`. |
| JS-002: no `return null` for missing values | PASS | `GetRefPrice()` returns `double` (0.0). No reference-type null return. |

### NT8 Compiler Rules

| Rule | Result | Notes |
|------|--------|-------|
| NT8-003: no `volatile double` | PASS | No `volatile double` introduced. Plan §6.2 notes only `NT8-003` applies -- and no new volatile double fields exist. |
| NT8-001: no `{ get; init; }` properties | PASS | No init-only setters in any snippet. |

### Method Signatures & CYC

| Item | Result | Notes |
|------|--------|-------|
| `private double GetRefPrice()` -- CYC=4 | PASS | 3 null guards + 1 normal return. CYC=4. ≤8. |
| `private void OnLoaded(...)` -- CYC unchanged | PASS | Two straight-line appended calls. No new decision points. |
| T3 (comment fix) -- CYC unchanged | PASS | No logic modified. CYC not applicable. |
| All CYC ≤ 8 | PASS | Highest is T1 at CYC=4. |

### Test Coverage

| Item | Result | Notes |
|------|--------|-------|
| Plan requires xUnit [Fact] for T1+T2 | PASS | T1 explicitly test-exempt (NT8 runtime dependency; Sim101 gate DW-B13-SIM-T1-01 documented per plan §3.6). T2 has [Fact] with Assert. T3 is docs-only (no test required). |
| T2 [Fact] has a real Assert.* call (V-02 fix) | PASS | `Assert.Equal(5, qty)` at plan §4.4. Distinguishes enabled-engine path (qty=5) from fallback path (qty=1). Concrete, non-vacuous. |
| No NUnit / MSTest | PASS | Only `[Fact]` (xUnit) used. |
| xUnit only | PASS | |

### 7-Scan Checklist

| Item | Result | Notes |
|------|--------|-------|
| SCAN-01 lock() grep | PASS | Plan §10: no `lock(` in any new/modified code. |
| SCAN-02 async void grep | PASS | Plan §10: no `async void`. |
| SCAN-03 return null grep | PASS | Plan §10: `GetRefPrice` returns `double` (0.0), not null. |
| SCAN-04 DateTime.Now | PASS | Plan §10: no date/time usage. |
| SCAN-05 CreateOrder PTT- prefix | PASS | Plan §10: no new CreateOrder calls. |
| SCAN-06 hex color literals | PASS | Plan §10: no new color assignments or UI controls. |
| SCAN-07 cross-thread volatile | PASS | Plan §10: no new cross-thread fields; existing `_atrEnabled` already volatile. |
| All 7 scans present in plan | PASS | Plan §10 covers all 7 explicitly. |

---

## Violations

None.

| # | Rule ID | Description | Location | Severity |
|---|---------|-------------|----------|----------|
| (none) | — | — | — | — |

---

## Spec Coverage Matrix

| Spec B13 Target (line 7424) | Addressed in Plan? | Plan Section |
|----------------------------|--------------------|--------------|
| DW-B9-01 ATR box on canvas | Shelved to B14 (explicit) | §1 "Shelved" |
| DW-B9-03 Click-trader Bid+1/Ask-1 | Shelved to B14 (explicit) | §1 "Shelved" |
| DW-B12-DEFER-01 GetRefPrice via MarketData.Last.Price | Addressed | §3 Ticket T1 |
| DW-B12-DEFER-02 ATR fraction spinner startup sync | Addressed | §4 Ticket T2 |

| Plan Ticket | Spec/Backlog Basis | Status |
|-------------|-------------------|--------|
| T1 | Spec line 7424 (DW-B12-DEFER-01) | In scope |
| T2 | Spec line 7424 (DW-B12-DEFER-02) | In scope |
| T3 | B12 backlog DW-B12-DEFER-03 (P3 docs fix, OPEN entering B13) | In scope (P3 docs only) |

---

## Gate Decision

Cycle 1 found 2 violations (V-01, V-02). Both are resolved in R2. No new violations found.
Plan is cleared to proceed to Phase 3 (ticket generation).

REVIEW_PASS
