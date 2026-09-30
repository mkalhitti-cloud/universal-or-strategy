# B54-LaneA Plan Review

**Reviewer**: ptt-plan-reviewer (Phase 2)
**Epic**: PTT-COPIER B54 LaneA
**Plan reviewed**: `docs/brain/B54-LaneA/02-architecture-plan.md`
**Spec**: `specs/002-trade-copier-spec.html` id="section-b54"
**Rules**: `docs/standards/jane-street/RULES_CATALOG.md`
**Date**: 2026-08-09

---

## Verdict: REVIEW_PASS

Zero violations found. All 14 checklist items pass.

---

## Checklist Results

| # | Check | Result | Evidence in Plan |
|---|-------|--------|-----------------|
| 1 | All 3 root causes addressed (A=OnLoaded, B=XML persistence, C=event after LoadRules) | PASS | §1 enumerates Root Cause A, B, C verbatim; §2/§3 fix each individually |
| 2 | `CopyRulesContainer` gets `[XmlElement] CopyEnabled` (not `CopyRuleDto`) | PASS | §3 A2 adds property to `CopyRulesContainer` class only |
| 3 | `IsEnabled` property exposes `_isCopyEnabled` correctly | PASS | §3 A1: `public bool IsEnabled => _isCopyEnabled;` — expression-bodied, no setter |
| 4 | `SaveRules` writes `container.CopyEnabled = _isCopyEnabled` | PASS | §3 A3: one statement before `XmlSerializer.Serialize` |
| 5 | `LoadRules` restores `_isCopyEnabled` and fires `CopyEnabledChanged` at end | PASS | §3 A4: both statements inside `try`, before `_persistenceLoaded = true` |
| 6 | Both Panel and Window get `ApplyCopyState(bool enabled)` methods | PASS | §4 B1 (Panel), §5 C1 (Window) |
| 7 | `OnLoaded` on both surfaces calls `ApplyCopyState(_engine.IsEnabled)` AFTER subscribing | PASS | §4 B3 and §5 C3: subscribe line first, `ApplyCopyState` call second |
| 8 | Toggle handlers delegate to `engine.SetEnabled()` without direct button mutation | PASS | §4 B4 (`OnCopyToggle`), §5 C4 (`OnGlobalToggle`): engine call only, comments confirm no mutation |
| 9 | All new/modified methods CYC <= 8 | PASS | §8 complexity table: max CYC = 2 (Panel `ApplyCopyState` with null guard) |
| 10 | T_B54_01/02/03 unit-testable without WPF host | PASS | §6: tests use `CopyEngine` + temp files + reflection only; no WPF controls |
| 11 | Tests use reflection to reset `_persistenceLoaded` for idempotency | PASS | §6: `ResetPersistenceLoaded` helper uses `GetField("_persistenceLoaded", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue`; all 3 tests call it |
| 12 | JS-021 (no lock()), JS-002 (no return null), JS-033 (no async void) respected | PASS | §9 compliance table: all three rules verified; `ApplyCopyState` is sync `void`, `Dispatcher.InvokeAsync` is an expression inside it, not an `async void` signature |
| 13 | NT8-001 (no init-only setters) respected | PASS | §3 A2: `CopyRulesContainer.CopyEnabled` uses `{ get; set; }`, explicitly noted as "NOT an `init`-only setter" |
| 14 | DW-B54-01 and DW-B54-02 explicitly deferred | PASS | §10 deferred table: DW-B54-01 = "DEFERRED — Director research required", DW-B54-02 = "BLOCKED by DW-B54-01"; §10 final line confirms neither is touched in §3–§6 |

---

## Spec Coverage Matrix

| Spec Requirement (section-b54) | Addressed? | Plan Section |
|-------------------------------|-----------|--------------|
| Engine is authority; `CopyEngine.IsEnabled` is ground truth | YES | §2 invariant, §7 invariant 1 |
| `ApplyCopyState(_engine.IsEnabled)` called unconditionally in `OnLoaded` | YES | §4 B3, §5 C3 |
| `CopyEnabledChanged` fires on every state change and after `LoadRules` | YES | §3 A4 (LoadRules), §2 state machine (SetEnabled unchanged) |
| `[XmlElement] public bool CopyEnabled { get; set; }` on serialization DTO | YES | §3 A2 |
| `SaveRules()` writes `CopyEnabled` | YES | §3 A3 |
| `LoadRules()` reads `CopyEnabled` and fires event | YES | §3 A4 |
| `ApplyCopyState` is single private method per surface; only callers are `OnLoaded` and event handler | YES | §7 invariant 4; §4 B1/B2/B3, §5 C1/C2/C3 |
| No surface calls `ApplyCopyState` from toggle handler | YES | §4 B4, §5 C4 — comments explicitly prohibit it |
| `OnCopyToggle` / `OnGlobalToggle` delegate to `engine.SetEnabled()` | YES | §4 B4, §5 C4 |
| T_B54_01: `LoadRules(true)` → `IsEnabled==true` AND event fires `true` | YES | §6 T_B54_01 |
| T_B54_02: `LoadRules(false)` → `IsEnabled==false` AND event fires `false` | YES | §6 T_B54_02 |
| T_B54_03: `SaveRules/LoadRules` round-trip preserves `CopyEnabled=true` | YES | §6 T_B54_03 |
| DW-B54-01 deferred (Director research item) | YES | §10 |
| DW-B54-02 deferred (blocked by DW-B54-01) | YES | §10 |

---

## Rule Compliance Summary

| Rule ID | Description | Status |
|---------|-------------|--------|
| JS-021 | No `lock()` | PASS — zero lock calls in all new and modified methods |
| JS-002 | No `return null` | PASS — all new methods are `void` or return `bool` (non-nullable) |
| JS-033 | No `async void` | PASS — `ApplyCopyState` is synchronous `private void`; `Dispatcher.InvokeAsync` is an expression statement inside it, not an async method signature; existing toggle handlers are unchanged `void` event handler signatures |
| NT8-001 | No `init`-only setters | PASS — `CopyRulesContainer.CopyEnabled` uses `{ get; set; }` standard setter |

---

## Notes for Engineer

1. **Panel null guard (§4 B1)**: The `if (_copyToggleBtn2 == null) return;` guard inside `ApplyCopyState` is correct for ChartTrader panel WPF template quirks. Window version (§5 C1) omits it correctly — Window lifecycle guarantees the control exists before `OnLoaded`.

2. **`_persistenceLoaded` reset (§6)**: The `?.SetValue` (null-conditional) on the reflection call is safe. If the field name ever changes, the test will produce a misleading silent-pass. Engineer should add a non-null assertion on the `FieldInfo` before calling `SetValue` to fail loudly if the field is renamed.  
   *This is a test-quality suggestion only — not a plan violation.*

3. **`overridePath` parameter on private methods (§3 A4)**: Signature change to `private void SaveRules(string overridePath = null)` and `private void LoadRules(string overridePath = null)` is correct and NT8-001-clean. The null-coalescing `??` path resolution does not introduce CYC branches.

4. **7-scan contract (§11)**: SCAN-03 and SCAN-04 are stated as "0 new instances" (delta check), not "0 total". Engineer must confirm no new `return null` or `throw new` are introduced by these changes specifically. Existing instances in the file are pre-existing and not in scope.

---

**REVIEW_PASS** — Plan is approved for Phase 3 (ticket generation).
