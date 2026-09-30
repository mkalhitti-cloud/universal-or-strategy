# PTT-COPIER-B5 — Plan Review
**Reviewer**: PTT Plan Reviewer (Phase 2)
**Date**: 2026-07-06
**Plan file reviewed**: `docs/brain/PTT-COPIER-B5/02-architecture-plan.md`
**Rules catalog**: `docs/standards/jane-street/RULES_CATALOG.md`
**Source files read (Wave workspace, read-only)**:
- `src/PropTraderTools/CopyEngine.cs` (B4 state)
- `src/PropTraderTools/TradeCopierPanel.cs` (B4 state)
- `src/PropTraderTools/TradeCopierWindow.cs` (B4 state)
- `src/PropTraderTools/CopyEngineTests.cs` (B3 state)
- `docs/brain/PTT-COPIER-B4/06-deferred-backlog.md`

---

## Review Verdict: REVIEW_FAIL

---

## Checklist Results

| # | Item | Result |
|---|------|--------|
| R1 | DW-B5-01 addressed — ListBox multi-select, ScrollViewer wrap, `Account[]` wired to engine on both Panel and Window surfaces | **PASS** |
| R2 | DW-B5-02 addressed — Shift+B KeyBinding, `OnWindowBreakEven` handler, `SetActiveRule`, `_activeRuleInstrument` field, MouseEnter tracking on fixed and dynamic rows | **PASS** |
| R3 | All method signatures use correct, resolvable C# types | **FAIL** — see Violations |
| R4 | All additions purely additive; no B1-B4 logic rewritten; `CopyEngine.cs` untouched | **PASS** |
| R5 | No `lock()` usage in any planned code block | **PASS** |
| R6 | No `DateTime.Now` usage; all new code uses `DateTime.UtcNow` where needed or no DateTime at all | **PASS** |
| R7 | No hardcoded hex color literals, no non-ASCII characters, no `FontFamily` references | **PASS** |
| R8 | All new method CYC counts ≤ 8 (max observed = 5 in `OnRowApply`, plan claims max = 2 for new methods; all well under threshold) | **PASS** |
| R9 | Two `[Fact]` xUnit tests for `BreakEven()` listed (DW-B3-03); teardown fix for DW-B2-01 shown; no CopyEngine changes so no additional engine tests required | **PASS** |
| R10 | Section H — Risk/Regression section present with 8 items covering ListBox height, SelectedItems, null `_activeRuleInstrument`, dynamic row empty instrument, tag slot-2 type change, test isolation, log coverage, and single-follower regression | **PASS** |

---

## Violations

### V-01 — R3 FAIL: Missing `using System.Windows.Input;` in TradeCopierWindow.cs additions

**Severity**: Compilation-blocking (all new Window code will fail to build without this directive)

**Location in plan**: Section E — "TradeCopierWindow.cs additions"
- Sub-section: "BuildUI() modification — add Shift+B after existing setup" (plan lines 216-222)
- Sub-section: "New nested class (identical to Panel's RelayCommand)" (plan lines 357-376)

**Evidence**:
- `TradeCopierWindow.cs` (B4, lines 1-12) currently carries these usings only:
  ```
  using System;
  using System.Windows;
  using System.Windows.Controls;
  using NinjaTrader.Cbi;
  using NinjaTrader.Gui;
  using NinjaTrader.Gui.Tools;
  using NinjaTrader.NinjaScript;
  ```
- `System.Windows.Input` is **absent**.
- Plan Section E introduces the following symbols that live exclusively in `System.Windows.Input`:
  - `Key` (used in `new KeyBinding(beWinCmd, Key.B, ModifierKeys.Shift)`)
  - `ModifierKeys` (same line)
  - `KeyBinding` (same line, also used via `InputBindings.Add(...)`)
  - `ICommand` (used in `private sealed class RelayCommand : ICommand`)
- `TradeCopierPanel.cs` (line 7) has `using System.Windows.Input;` and compiles — but the Window file does not.
- The plan never lists `using System.Windows.Input;` as a required addition to `TradeCopierWindow.cs` anywhere in Section B (change list), Section E (additions), or Section G (compliance checklist).

**Rule citation**: Not a RULES_CATALOG violation per se; this is a **plan completeness failure** — the plan describes code that cannot compile as written. The plan's own compliance checklist (Section G) claims "No new DateTime usage" and "ASCII-only" pass, but omits verification that referenced types are resolvable. Under the reviewer mandate, a plan that produces uncompilable code is REVIEW_FAIL.

**Fix required by engineer (do not suggest fixes — report only)**:
Engineer must add `using System.Windows.Input;` to the plan's stated additions for `TradeCopierWindow.cs` before this plan can be approved.

---

## Notes (informational, non-blocking)

1. **Spec HTML not found** (`specs/002-trade-copier-spec.html`): The plan lists this file as an input read, but no `specs/` directory exists in the Director workspace and no HTML spec was located. The plan's content is internally consistent and cross-checked against the B4 backlog ledger, so this did not generate a violation — but the spec reference cannot be confirmed.

2. **`OnTestStatusUpdate` teardown reference** (Section F): The plan's `IDisposable.Dispose` teardown references a named method `OnTestStatusUpdate`, but the existing tests subscribe via inline lambdas. This is a minor inconsistency in the test design description that the engineer should resolve during implementation — not a rules violation.

3. **`TradeCopierWindow.cs` class is `public class`** (not `sealed`): Pre-existing B4 issue; not introduced by B5 plan. Not a B5 violation.

---

## Approval Statement

**NOT APPROVED.**

This plan contains one compilation-blocking omission (V-01). The plan is otherwise well-structured, fully additive, free of lock()/DateTime.Now/hex/non-ASCII violations, and correctly targets both DW-B5-01 and DW-B5-02 deferred items. Once the engineer adds `using System.Windows.Input;` to the TradeCopierWindow.cs additions in Section E (and updates Section B's change list accordingly), this plan is expected to pass re-review with no further issues.

---

## Cycle 2 Review

**Reviewer**: PTT Plan Reviewer (Phase 2)
**Date**: 2026-07-06
**Trigger**: Re-review after Architect fixed V-01 (missing `using System.Windows.Input;`)
**Plan revision**: `02-architecture-plan.md` — re-issued with `REVIEW_PASS (re-issued after V-01 fix)` header

---

### Checklist Results

| # | Item | Result |
|---|------|--------|
| R1 | DW-B5-01 addressed — `ListBox SelectionMode.Extended`, `ScrollViewer MaxHeight=80`, `SelectedItems` extraction, `followers.ToArray()` wired to `_engine.AddRule` on both Panel and Window surfaces | **PASS** |
| R2 | DW-B5-02 addressed — Shift+B `KeyBinding`, `OnWindowBreakEven` handler, `SetActiveRule`, `_activeRuleInstrument` field, `MouseEnter` tracking on fixed and dynamic rows, `RelayCommand` nested class | **PASS** |
| R3 | V-01 fix confirmed: Section E now opens with an explicit **"Add using directive (file header)"** sub-section stating `using System.Windows.Input;` as additive change #1 for `TradeCopierWindow.cs`, enumerating all four resolved symbols (`Key`, `ModifierKeys`, `KeyBinding`, `ICommand`) and showing the full before-state of the using block | **PASS** |
| R4 | All additions purely additive; `CopyEngine.cs` untouched; Panel and Window changes are surgical replacements within existing methods; no B1–B4 logic removed or rewritten | **PASS** |
| R5 | No `lock()` usage in any planned code block; `RelayCommand.CanExecuteChanged` uses empty `add { } remove { }` accessors (no lock); JS-021 PASS | **PASS** |
| R6 | No `DateTime.Now` usage; no new DateTime at all in B5 code; JS-021/DateTime rule PASS | **PASS** |
| R7 | No hardcoded hex color literals, no non-ASCII characters, no `FontFamily` references; all new string literals are ASCII; Section G ASCII-only / No hex colors / No FontFamily all marked PASS | **PASS** |
| R8 | All new method CYC ≤ 8: `OnWindowBreakEven` = 3, `SetActiveRule` = 1, `RelayCommand.Execute` = 1, `RelayCommand.CanExecute` = 1; modified methods (`OnApplyRule`, `OnRowApply`) stay at CYC ≤ 4; well under threshold | **PASS** |
| R9 | Two `[Fact]` xUnit tests for `BreakEven()` listed in Section F (`BreakEven_FlatAccount_SkipsAndLogs`, `BreakEven_LongPosition_LogsBeMove`); teardown fix for DW-B2-01 shown | **PASS** |
| R10 | Section H — Risk/Regression section present with 8 items: ListBox height, `SelectedItems` observable, null `_activeRuleInstrument`, dynamic row empty instrument, tag slot-2 type change (MEDIUM), test isolation, log coverage, single-follower regression | **PASS** |

---

### Violations

**NONE.** V-01 from Cycle 1 is fully resolved. No new violations found.

---

### Approval Statement

**APPROVED.**

All 10 checklist items pass. The single Cycle 1 violation (V-01 — missing `using System.Windows.Input;`) is confirmed fixed by the addition of the explicit "Add using directive (file header)" sub-section in Section E. The plan is complete, additive-only, free of lock()/DateTime.Now/hex/non-ASCII violations, correctly targets both deferred items (DW-B5-01 and DW-B5-02), and includes adequate risk coverage and xUnit test stubs.

**Final Verdict: REVIEW_PASS**
