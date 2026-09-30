# B51-LaneA Plan Review

**Block**: PTT-COPIER-B51
**Lane**: A
**Reviewer**: ptt-plan-reviewer
**Date**: 2026-08-08
**Plan reviewed**: docs/brain/B51-LaneA/02-architecture-plan.md
**Spec source**: inline (DW-B51-01, DW-B51-02 + build tag)

---

## VERDICT: REVIEW_PASS

Zero violations found. All 9 checklist items PASS.

---

## Checklist Results

| # | Item | Result |
|---|------|--------|
| 1 | DW-B51-01 addressed correctly (Visibility.Collapsed on multFactory, handler preserved) | PASS |
| 2 | DW-B51-02 addressed correctly (GetCopyMode() check inside OnFollowerAtmTemplateComboLoaded) | PASS |
| 3 | "HIDE NOT DELETE" respected (TextBox not deleted, OnFollowerMultiplierChanged not deleted) | PASS |
| 4 | CYC <= 8 for OnFollowerAtmTemplateComboLoaded after fix (4 → 5) | PASS |
| 5 | No P0 JS violations (JS-021 lock, JS-001 throw, JS-002 return null, JS-033 async void) | PASS |
| 6 | No NT8 P0 violations | PASS |
| 7 | Plan touches only files in scope (TradeCopierPanel.cs + CopyEngine.cs build tag) | PASS |
| 8 | Build tag correct ("PTT-COPIER B51 \| ui-fixes \| 2026-08-08") | PASS |
| 9 | No new tests rationale present and valid | PASS |

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| DW-B51-01: multFactory TextBox Visibility = Collapsed | YES | Section 2 (DW-B51-01) |
| DW-B51-01: Do NOT delete TextBox | YES | Section 2 — AddHandler line preserved in Before/After snippet |
| DW-B51-01: Do NOT remove OnFollowerMultiplierChanged | YES | Section 2 — AddHandler call kept intact; Section 5 JS compliance notes zero handler removal |
| DW-B51-02: OnFollowerAtmTemplateComboLoaded gets GetCopyMode() check | YES | Section 2 (DW-B51-02) |
| DW-B51-02: Fix placed inside !_atmComboRefs.Contains(cb) block | YES | Section 2 code snippet |
| DW-B51-02: CYC <= 8 after +1 branch | YES | Section 6 — Before 4, After 5 |
| Build tag: CopyEngine.cs L41 → "PTT-COPIER B51 \| ui-fixes \| 2026-08-08" | YES | Section 2 (Build Tag Bump) |
| Files in scope: TradeCopierPanel.cs + CopyEngine.cs only | YES | Section 3, Section 10 |
| No new tests required (WPF Visibility) | YES | Section 9 — rationale valid |

---

## Rule Compliance Detail

### Jane Street P0 Rules

| Rule | Check | Result |
|------|-------|--------|
| JS-001 | No `throw new XxxException` in modified code paths | PASS — no throw statements in B51 changes |
| JS-002 | No `return null` in modified methods | PASS — neither modified method has a return statement (void handlers / void factory call) |
| JS-021 | No `lock()` anywhere in modified regions | PASS — all changes are on the WPF UI thread; no synchronisation primitives introduced |
| JS-033 | No `async void` (non-event-handler) | PASS — both modified methods are synchronous void RoutedEventHandler / factory builder |

No P1 JS violations introduced by the plan's proposed code.

### NT8 P0 Rules

| Rule | Check | Result |
|------|-------|--------|
| NT8-001 | No `{ get; init; }` | PASS — no properties declared |
| NT8-002 | No `abstract record` / `sealed record` | PASS — no types declared |
| NT8-003 | No `volatile double` | PASS — no field declarations |
| NT8-007 | No `CreateOrder` with wrong arg 12 type | PASS — no CreateOrder call in B51 |
| NT8-013 | No `DateTime.Now` in CreateOrder | PASS — no CreateOrder call in B51 |
| NT8-015 | No `sealed class ... : Indicator` | PASS — no class declarations changed |
| NT8-016 | No `sealed class ... : Window` | PASS — no class declarations changed |
| NT8-019 | No `async void` in NT8 callback methods | PASS — no async methods introduced |
| NT8-030 | `OnWindowCreated` idempotency guard | PASS — OnWindowCreated not touched |
| NT8-031 | No `OrderState.PendingSubmit` | PASS — no OrderState access in B51 |
| NT8-042 | No `Dispatcher.InvokeAsync` | PASS — plan Section 7 correctly identifies both changes as already on the UI thread; no dispatcher required |
| NT8-043 | No null-conditional compound assignment (`?.` with `-=`/`+=`) | PASS — no event subscriptions modified |
| NT8-044 | `StringComparison` requires `using System` | PASS — StringComparison not used in B51 |

---

## Notes

1. **Threading reasoning is sound** (plan Section 7): `BuildCheckItemTemplate` is called during
   panel initialization on the UI thread. `FrameworkElementFactory.SetValue` sets a template
   default before any element is instantiated — no live UI mutation, no dispatcher needed.
   `OnFollowerAtmTemplateComboLoaded` is a `RoutedEventHandler` (always UI thread). The
   `GetCopyMode()` call reads a PTT-internal enum — confirmed safe from UI thread.

2. **CYC delta is minimal**: DW-B51-01 adds 0 branches (`SetValue` is a simple assignment);
   DW-B51-02 adds exactly 1 branch (one `if` inside an existing block). Both remain well
   inside the CYC <= 8 threshold.

3. **HIDE NOT DELETE convention** is explicitly observed. Both the `FrameworkElementFactory`
   instance (`multFactory`) and the `TextChangedEventHandler` registration are preserved in
   the plan's After snippet. The spec constraint is met precisely.

4. **No scope creep**: plan Section 3 and Section 10 enumerate exactly the two files permitted
   by the spec. No additional files, classes, or tests are introduced.

---

## Decision

**REVIEW_PASS** — Plan is approved for Phase 3 (ticket generation).
