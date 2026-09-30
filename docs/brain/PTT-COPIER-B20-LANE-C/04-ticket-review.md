# PTT-COPIER-B20-LANE-C — Ticket Review
# Reviewer: ptt-ticket-reviewer
# Tickets reviewed: docs/brain/PTT-COPIER-B20-LANE-C/04-tickets.md
# Plan source: docs/brain/PTT-COPIER-B20-LANE-C/02-architecture-plan.md (REVIEW_PASS)
# Plan review: docs/brain/PTT-COPIER-B20-LANE-C/02-plan-review.md (REVIEW_PASS 2026-07-14)
# Rules applied: docs/standards/jane-street/RULES_CATALOG.md
# Date: 2026-07-14

---

## Ticket Review: PTT-COPIER-B20-LANE-C

---

### T3 — Account Display Fix + Cross-Surface Toggle Sync

---

#### Traceability

| Req ID | Ticket Coverage | Verdict |
|--------|----------------|---------|
| DW-B20-LANE-A-DEFER-01 — Panel subscribe/unsubscribe + method | Change A (`OnLoaded +=`), Change B (`Detach() -=`), Change C (new `OnCopyEnabledChanged(bool)`) | COVERED |
| DW-B20-LANE-A-DEFER-01 — Window subscribe/unsubscribe + method | Change E (`OnLoaded +=`), Change F (`OnWindowClosed -=`), Change G (new `OnCopyEnabledChanged(bool)`) | COVERED |
| DW-B17-ACCOUNT-NAME-01 — Panel `FollowerItem.ToString()` | Change D (`FollowerItem.ToString()` with `?[0]` null-conditional index) | COVERED |
| DW-B17-ACCOUNT-NAME-01 — Window `AccountDisplayConverter` + `BuildAccountDisplayTemplate` + `ItemTemplate` wiring | Change H (`AccountDisplayConverter` class), Change I (`BuildAccountDisplayTemplate` method + static field), Change J (`BuildRuleRow` both controls), Change K (`BuildDynamicRuleRow` both controls) | COVERED |

**Phantom work** (in ticket, not in plan/spec): None found.  
**Missing work** (in plan, not in ticket): None found. All 11 named changes from the plan (A–K plus pre-flight using directives) are represented in the ticket with exact insertion context.

**Traceability: PASS**

---

#### JS Pre-Check

| Rule | Description | Ticket Evidence | Verdict |
|------|-------------|----------------|---------|
| **JS-021** | No `lock()` anywhere | Ticket explicitly documents no `lock` keyword. `_copyEnabled` is a `bool` accessed on the UI thread; `Dispatcher.InvokeAsync` used for dispatch. JS Rule Constraints table: "No `lock` keyword introduced." | PASS |
| **JS-023** | `Dispatcher.InvokeAsync` (not blocking `.Invoke`) | Both `OnCopyEnabledChanged` methods use `Dispatcher.InvokeAsync`. Ticket notes Appendix C SCAN-04 checks for `Dispatcher.Invoke(` (blocking form). | PASS |
| **JS-033** | No `async void` (non-event-handler) | Both `OnCopyEnabledChanged` methods declared `private void`. No async state machine. JS Rule Constraints table: "Both `OnCopyEnabledChanged` methods are `private void`." | PASS |
| **JS-001** | No `throw` in hot path | `AccountDisplayConverter.ConvertBack` throws `NotImplementedException`. Ticket correctly documents this is a one-way binding interface stub: "WPF never calls `ConvertBack` on a `OneWay` binding." Definitionally unreachable at runtime — not a hot path. JS Rule Constraints table explicitly addresses this. | PASS |
| **JS-002** | No `return null` | All methods return `""` or valid objects via null-coalescing (`?? ""`). No bare `return null`. JS Rule Constraints table: "All methods return `""` or a valid object via null-coalescing." | PASS |
| **JS-008** | No mutable fields on struct | No struct fields introduced. | N/A — PASS |
| **JS-009** | No `Dictionary<K,V>` on shared state | No Dictionary fields introduced in ticket scope. | N/A — PASS |

**JS Pre-Check: PASS**

---

#### CYC Pre-Check

| Method | File | CYC | At Risk (>8)? |
|--------|------|-----|---------------|
| `OnCopyEnabledChanged(bool)` | `TradeCopierPanel.cs` | 2 | No |
| `FollowerItem.ToString()` (modified) | `TradeCopierPanel.cs` | 1 | No |
| `OnCopyEnabledChanged(bool)` | `TradeCopierWindow.cs` | 1 | No |
| `AccountDisplayConverter.Convert` | `TradeCopierWindow.cs` | 1 | No |
| `AccountDisplayConverter.ConvertBack` | `TradeCopierWindow.cs` | 1 | No |
| `BuildAccountDisplayTemplate()` | `TradeCopierWindow.cs` | 1 | No |

All new/modified methods satisfy CYC <= 8. Counting convention consistent with codebase standard (lambdas excluded from enclosing method CYC; null-conditional `?.` operators do not add CYC; ternaries inside `Dispatcher.InvokeAsync` lambda excluded from enclosing method count).

Panel `OnCopyEnabledChanged` CYC=2 rationale is sound: one `if (_copyToggleBtn2 == null) return;` guard (+1) plus base path (+1). Window `OnCopyEnabledChanged` CYC=1 rationale is sound: constructor guarantee documented in Decision D-02 prevents null dereference without a guard.

**CYC Pre-Check: PASS**

---

#### NT8 Check

| Constraint | Description | Ticket Evidence | Verdict |
|------------|-------------|----------------|---------|
| `using System.Windows.Data` | Required for `IValueConverter`, `Binding`, `BindingMode` | Pre-flight step explicitly documented: "Add `using System.Globalization;` and `using System.Windows.Data;`" with exact verification instruction ("neither … is currently imported"). | PASS |
| `using System.Globalization` | Required for `CultureInfo` in `IValueConverter` signatures | Same pre-flight step. Both are listed as missing and both are documented for addition. | PASS |
| `ConvertBack` throws `NotImplementedException` | Correct for one-way binding — WPF never calls `ConvertBack` on a `OneWay` binding | Change H body: `throw new NotImplementedException("AccountDisplayConverter is one-way only");` | PASS |
| No XAML required | Code-based `DataTemplate` via `FrameworkElementFactory` — matches existing codebase pattern | Change I uses `FrameworkElementFactory` + `DataTemplate` in pure C#. No `.xaml` file. | PASS |
| NT8-003: No `volatile` | No `volatile double` or `volatile int` introduced | JS Rule Constraints table: "No `volatile` fields introduced. `_copyEnabled` is a plain `bool`." | PASS |
| JS-021: No `lock()` | See JS Pre-Check | Documented — no lock keyword. | PASS |
| No `sealed` on `TradeCopierWindow` | Ticket must not describe sealing the window class | Ticket scope is restricted to Panel and Window body changes. No class-level modifier changes. `TradeCopierWindow` is not sealed. | PASS |
| No `FontFamily` on WPF element | No FontFamily assignments | None found in ticket changes. | PASS |
| No hardcoded hex color | No `#RRGGBB` literals | None found in ticket changes. Color references use named brushes (`BrushActive`, `BrushInactive`, `WBrushActive`, `WBrushInactive`). | PASS |
| No `DateTime.Now` | No `DateTime.Now` usage | None found in ticket changes. | PASS |
| `Account.All` only from `Loaded` handler | `ItemsSource = Account.All` already in `BuildRuleRow` / `BuildDynamicRuleRow`; this ticket only adds `ItemTemplate` assignments after the existing `ItemsSource` calls | Ticket explicitly states only `ItemTemplate` assignments are added — no new `Account.All` access introduced. | PASS |
| No `async/await` in lifecycle methods | No async lifecycle methods described | `OnLoaded` and `Detach` / `OnWindowClosed` receive only synchronous `+=` / `-=` line insertions. No `async` keyword added to any lifecycle method. | PASS |

**NT8 Check: PASS**

---

#### Test Coverage

New methods introduced by T3:

| Method | Visibility | Reason No [Fact] Required |
|--------|-----------|--------------------------|
| `OnCopyEnabledChanged(bool)` — Panel | `private void` | WPF `Button.Content` / `Background` assignment behind `Dispatcher.InvokeAsync`; xUnit cannot instantiate WPF controls without STA + full WPF app context. |
| `FollowerItem.ToString()` (modified) — Panel | `private` (inner `private sealed class`) | `FollowerItem` is `private sealed` inside `TradeCopierPanel`; inaccessible from xUnit without reflection (violates "no test contortion" principle). |
| `OnCopyEnabledChanged(bool)` — Window | `private void` | WPF `Button.Content` / `Background` assignment; same WPF instantiation constraint. |
| `AccountDisplayConverter.Convert` | `public` (but class is `private sealed` nested) | Outer class `TradeCopierWindow` is not accessible for test instantiation via this path; converter is stateless display logic wrapping `.NET Split`; not our business logic. |
| `AccountDisplayConverter.ConvertBack` | `public` (interface stub) | OneWay binding interface stub; unreachable at runtime; testing a `throw` stub has no value. |
| `BuildAccountDisplayTemplate()` | `private static` | Indirectly exercised via WPF template wiring; no externally observable non-UI return value to assert. |

Spec explicitly waives `[Fact]` requirement for this ticket (UI-only string transform + event wiring). Plan review §No-New-Tests confirmed rationale as sound (REVIEW_PASS). `CopyEnabledChanged` event logic was tested in B20-LANE-A. Expected `[Fact]` count remains **120** (unchanged).

**Test Coverage: PASS**

---

#### Scan Checklist Presence

The ticket's 7-Scan Checklist section must contain SCAN-01 through SCAN-07 with exact `ctx_shell` commands.

| Scan | Command Present | Expected Result Stated | Verdict |
|------|----------------|----------------------|---------|
| SCAN-01: JS-021 No `lock()` | `ctx_shell("grep -rn \"lock(\" src/PropTraderTools/")` | Expected: 0 results | PRESENT |
| SCAN-02: JS-033 No `async void` | `ctx_shell("grep -rn \"async void \" src/PropTraderTools/ --include=\"*.cs\"")` | Expected: 0 results | PRESENT |
| SCAN-03: JS-002 No new `return null` violations | `ctx_shell("grep -rn \"return null\" src/PropTraderTools/ --include=\"*.cs\"")` | Action note + pre-existing hit guidance | PRESENT |
| SCAN-04: NT8-003 No new `volatile` fields | `ctx_shell("grep -rn \"volatile\" src/PropTraderTools/ --include=\"*.cs\"")` | Expected: 0 new volatile fields | PRESENT |
| SCAN-05: Build — Zero errors | `ctx_shell("dotnet build")` | Expected: 0 errors, 0 new warnings | PRESENT |
| SCAN-06: Tests — All pass | `ctx_shell("dotnet test")` | Expected: 120 [Fact] pass, 0 fail | PRESENT |
| SCAN-07: CYC — No new CYC > 8 | `ctx_shell("python scripts/complexity_audit.py")` | Expected: 0 new methods CYC > 8; reference CYC table included | PRESENT |

All 7 scans are present with exact `ctx_shell` commands and stated expected results. CYC reference table attached to SCAN-07. Engineer contract is complete.

**Scan Checklist: PASS**

---

#### File Routing

| File | Path in Ticket | Workspace | Verdict |
|------|---------------|-----------|---------|
| `TradeCopierPanel.cs` | `src/PropTraderTools/TradeCopierPanel.cs` | Wave workspace (`c:\WSGTA\universal-or-strategy\src\PropTraderTools\`) | PASS |
| `TradeCopierWindow.cs` | `src/PropTraderTools/TradeCopierWindow.cs` | Wave workspace | PASS |

Files NOT modified (`CopyEngine.cs`, `CopyEngineTests.cs`, `TradeCopierAddOn.cs`, `AtrSizingEngine.cs`) are explicitly listed. No .cs path points to the Director workspace.

**File Routing: PASS**

---

#### Additional Safety Observations (Non-Blocking)

**OBS-T3-01 — Static field `_accountDisplayConverter` (ticket refinement over plan):**  
The plan's §3.3 Change I instantiates `new AccountDisplayConverter()` per call. The ticket's Change I introduces a `private static readonly AccountDisplayConverter _accountDisplayConverter = new AccountDisplayConverter();` field and reuses it. This is a beneficial refinement (stateless converter, avoids repeated allocation). It does not affect correctness, CYC, or rule compliance. Not a violation.

**OBS-T3-02 — Redundant self-callback is idempotent:**  
The cross-surface data flow diagram correctly documents that a surface may receive its own toggle event back (e.g., Panel fires `SetEnabled`, which fires `CopyEnabledChanged`, which calls Panel's `OnCopyEnabledChanged`). The ticket correctly characterizes this as "redundant, idempotent" — bool assignment and the queued `Dispatcher.InvokeAsync` UI update both operate on values already set. No action required.

**OBS-T3-03 — Subscribe/unsubscribe symmetry confirmed:**  
Panel: `OnLoaded +=` / `Detach() -=`. Window: `OnLoaded +=` (inside second `try`) / `OnWindowClosed -=`. Both surfaces correctly tear down on the same lifecycle event that tears down `PositionStateChanged`. No leak path.

---

#### VERDICT: TICKET_REVIEW_PASS

| Check | Result |
|-------|--------|
| Traceability | PASS |
| JS Pre-Check | PASS |
| CYC Pre-Check | PASS |
| NT8 Check | PASS |
| Test Coverage | PASS |
| Scan Checklist (SCAN-01 through SCAN-07) | PASS |
| File Routing | PASS |

---

## Overall: TICKET_REVIEW_PASS

T3 is the only ticket in this lane. All checks pass. No violations found. The engineer may proceed to implementation.

**Pre-flight reminder for engineer** (documented in ticket, not a review finding):  
Add two missing `using` directives in `TradeCopierWindow.cs` before any compiler step:
- `using System.Globalization;`
- `using System.Windows.Data;`
