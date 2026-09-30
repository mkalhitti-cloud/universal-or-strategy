# PTT-COPIER-B20-LANE-C — Plan Review
# Reviewer: ptt-plan-reviewer
# Epic: PTT-COPIER-B20-LANE-C  Ticket scope: T3
# Plan reviewed: docs/brain/PTT-COPIER-B20-LANE-C/02-architecture-plan.md
# Spec reviewed: specs/002-trade-copier-spec.html (B20 ticket table, lines 2543-2553)
# Rules applied: docs/standards/jane-street/RULES_CATALOG.md
# Date: 2026-07-14 (re-review after V-01 resolution)
# Result: REVIEW_PASS

---

## Prior Review History

**Round 1** (2026-07-14): REVIEW_FAIL — V-01 (Spec Coverage Gap: Window account display
surface not addressed; plan covered Panel only via `FollowerItem.ToString()`).

**Round 2** (this review): V-01 resolved. ptt-architect added §3.3 Part C (Changes H-K)
implementing `AccountDisplayConverter` (`IValueConverter`) + `BuildAccountDisplayTemplate()`
+ `ItemTemplate` wiring in both `BuildRuleRow` and `BuildDynamicRuleRow`.

---

## V-01 Resolution Assessment

**Prior violation**: Plan did not address Window's account ComboBox/ListBox item template
for `!<suffix>` stripping (spec line 2547-2548).

**Resolution check**:

1. **Window account display surface addressed?** YES.
   - §3.3 Change H: `private sealed class AccountDisplayConverter : IValueConverter` added
     inside `TradeCopierWindow`. Implements `Convert` to strip `!<suffix>` via
     `(value as string)?.Split('!')?[0] ?? value?.ToString() ?? ""`. Null-safe chain.
   - §3.3 Change I: `private static DataTemplate BuildAccountDisplayTemplate()` factory
     using `FrameworkElementFactory` + `Binding("Name")` + converter. Code-only WPF,
     consistent with codebase pattern. `BindingMode.OneWay` correct for display-only template.
   - §3.3 Change J: `leaderCb.ItemTemplate` and `followerLb.ItemTemplate` assigned in
     `BuildRuleRow` — both controls covered.
   - §3.3 Change K: Same assignments in `BuildDynamicRuleRow` — both static and dynamic
     rule rows covered.

   DW-B17-ACCOUNT-NAME-01 (Window) is now **fully addressed**.

2. **`AccountDisplayConverter` approach sound for NT8 .NET 4.8?** YES.
   `IValueConverter` is a standard WPF (`System.Windows.Data`) interface available in
   .NET 4.8. NT8 AddOn context supports standard WPF. `FrameworkElementFactory` +
   `DataTemplate` built in code (no XAML) follows the established pattern in
   `TradeCopierWindow.cs`. D-09 decision log entry provides complete rationale. No NT8
   compiler restriction applies to this pattern.

3. **`ConvertBack` correctly `NotImplementedException`?** YES.
   `ConvertBack` throws `NotImplementedException`. The binding uses `BindingMode.OneWay`,
   which means WPF never calls `ConvertBack` at runtime. This is the correct and
   universally accepted WPF implementation for display-only converters. This does NOT
   trigger JS-001 (no throw in hot path) — `ConvertBack` on a OneWay binding is
   definitionally unreachable; it is an interface stub, not a hot path.

4. **`ItemTemplate` in both `BuildRuleRow` and `BuildDynamicRuleRow`?** YES.
   Change J covers `BuildRuleRow` (both `leaderCb` and `followerLb`).
   Change K covers `BuildDynamicRuleRow` (both `leaderCb` and `followerLb`).
   All four account-control surfaces in the Window receive the template.

5. **CYC for new methods <= 8?** YES. See CYC table in §4 of the plan.
   All new/modified methods: CYC 1 or 2. All well within the limit.

6. **`using` requirements for `System.Windows.Data` and `System.Globalization`?**
   Verified against `TradeCopierWindow.cs`: neither `System.Windows.Data` nor
   `System.Globalization` is currently imported (file has 7 `using` directives:
   `System`, `System.Collections.Generic`, `System.Windows`, `System.Windows.Controls`,
   `System.Windows.Media`, `NinjaTrader.Cbi`, `NinjaTrader.NinjaScript`).
   The plan correctly identifies both as required and instructs the engineer to
   "verify present; add if absent." This is accurate and actionable. The plan does NOT
   falsely claim these are already present.
   **Note for engineer**: Two `using` additions are required:
   - `using System.Globalization;` (for `CultureInfo` in `IValueConverter` signatures)
   - `using System.Windows.Data;` (for `IValueConverter`, `Binding`, `BindingMode`)

---

## Violations (Rule-Cited)

None. V-01 is fully resolved. No new violations introduced.

---

## Checklist Results

### JS-021: No `lock()` in proposed changes
**PASS** — No `lock` keyword introduced. `_copyEnabled` is a UI-thread-only `bool`.
`Dispatcher.InvokeAsync` is used for off-thread dispatch. SCAN-01 enforces at build time.

### JS-033: No `async void`
**PASS** — Both `OnCopyEnabledChanged` methods are `private void`. No async state machine.
SCAN-02 enforces at build time.

### NT8-003: No `volatile` on value fields
**PASS** — No `volatile` keyword introduced. SCAN-03 enforces.

### JS-001: No `throw` in hot path
**PASS** — `AccountDisplayConverter.ConvertBack` throws `NotImplementedException` but this
method is unreachable at runtime (WPF never calls `ConvertBack` on a `OneWay` binding).
This is an interface stub, not a hot path. No JS-001 violation.

### JS-002: No `return null`
**PASS** — All methods return `""` or a valid object via null-coalescing. No bare `return null`.

### CYC <= 8 for all new/modified methods
**PASS** — All new/modified methods satisfy CYC <= 8:

| Method | File | CYC |
|--------|------|-----|
| `OnCopyEnabledChanged(bool)` | TradeCopierPanel.cs | 2 |
| `FollowerItem.ToString()` (modified) | TradeCopierPanel.cs | 1 |
| `OnCopyEnabledChanged(bool)` | TradeCopierWindow.cs | 1 |
| `AccountDisplayConverter.Convert` | TradeCopierWindow.cs | 1 |
| `AccountDisplayConverter.ConvertBack` | TradeCopierWindow.cs | 1 |
| `BuildAccountDisplayTemplate()` | TradeCopierWindow.cs | 1 |

Lambda internals correctly excluded from enclosing method CYC.

### `Dispatcher.InvokeAsync` (not `.Invoke`) used
**PASS** — Both `OnCopyEnabledChanged` implementations use `Dispatcher.InvokeAsync`.
SCAN-04 checks for blocking `Dispatcher.Invoke(` — 0 expected matches.

### Subscribe/unsubscribe symmetry
**PASS** — Symmetry verified:

| Surface | Subscribe | Unsubscribe |
|---------|-----------|-------------|
| TradeCopierPanel | Change A: `+= OnCopyEnabledChanged` in `OnLoaded` | Change B: `-= OnCopyEnabledChanged` in `Detach()` |
| TradeCopierWindow | Change E: `+= OnCopyEnabledChanged` in `OnLoaded` | Change F: `-= OnCopyEnabledChanged` in `OnWindowClosed` |

### `FollowerItem.ToString()` null safety
**PASS** — Plan uses correct null-conditional index `?[0]`. Spec-supplied `[0]` (unsafe)
was corrected to `?[0]`. Appendix B provides 5-case exhaustive proof. D-01 documents correction.

### Window account display via `DataTemplate` + `IValueConverter`
**PASS** — Fully addressed in §3.3. Both `BuildRuleRow` and `BuildDynamicRuleRow` wire
`ItemTemplate` for both `leaderCb` and `followerLb`. `AccountDisplayConverter` is
`private sealed class` scoped to `TradeCopierWindow`. NT8 .NET 4.8 compatible approach.

### No new `[Fact]` tests rationale is sound
**PASS** — Spec line 2550 explicitly states no new tests required (UI-only string transform).
§6 provides independent rationale: WPF controls cannot be instantiated in xUnit without
STA + full WPF app context; `FollowerItem` is `private sealed`; `CopyEnabledChanged` event
already tested in B20-LANE-A. Rationale is sound.

---

## Spec Coverage Matrix

| Req ID | Description | Addressed? | Plan Section |
|--------|-------------|------------|--------------|
| DW-B20-LANE-A-DEFER-01 | Wire `CopyEnabledChanged` in Panel and Window | FULL | §3.1 Changes A-C, §3.2 Changes E-G |
| DW-B17-ACCOUNT-NAME-01 — Panel `FollowerItem.ToString()` | Strip `!<suffix>` at display layer in Panel | FULL | §3.1 Change D |
| DW-B17-ACCOUNT-NAME-01 — Window leader ComboBox + follower ListBox | Strip `!<suffix>` in Window via `AccountDisplayConverter` + `DataTemplate` | FULL | §3.3 Changes H-K |

---

## Observations (Non-Blocking)

### OBS-01 — §2 change count underreported for TradeCopierWindow.cs
§2 states "6 changes" for `TradeCopierWindow.cs`. The plan body documents 7 named changes
(E, F, G, H, I, J, K) plus two `using` additions (`System.Globalization`,
`System.Windows.Data`). The `using` additions are explicitly identified in §3.3 but
not counted as numbered changes in §2. This is a documentation precision issue only.
The engineer must add these two `using` directives; both are documented in the plan.
**Not a violation — no action required before ticket generation.**

### OBS-02 — T2 `[Fact]` test responsibility (unchanged from Round 1)
Confirmed correct: `SetEnabled_FiresCopyEnabledChanged` is a B20-LANE-A T2 responsibility,
already closed. Not a T3 concern.

### OBS-03 — SCAN-07 and SCAN-08 scope correctly covers all Window surfaces
SCAN-07 requires `AccountDisplayConverter` referenced at class def + Convert + ConvertBack +
2 in `BuildAccountDisplayTemplate` + 2 each in `BuildRuleRow` and `BuildDynamicRuleRow`.
SCAN-08 requires exactly 4 `ItemTemplate` assignment lines (2 in each method). These scans
will confirm both static and dynamic rule rows are wired. Sound.

---

## Final Verdict

**REVIEW_PASS**

All spec requirements are fully addressed in the revised plan. V-01 (Window account display
surface) is resolved via `AccountDisplayConverter` (`IValueConverter`) + `DataTemplate`
wiring in both `BuildRuleRow` and `BuildDynamicRuleRow`. All JS and NT8 rules: PASS.
CYC: all new methods <= 2. No violations. Engineer may proceed to ticket generation (Phase 3).

**Engineer pre-flight note**: Verify and add the two missing `using` directives in
`TradeCopierWindow.cs` before the compiler step:
- `using System.Globalization;`
- `using System.Windows.Data;`
