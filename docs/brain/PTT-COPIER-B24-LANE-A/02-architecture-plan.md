# PTT-COPIER-B24-LANE-A — Architecture Plan
# Phase: 1 (Architecture)
# Author: ptt-architect
# Defect: DW-B24-LEADER-CASTNULL-01
# Status: REVIEW_PENDING
# Date: 2026-07-17

---

## 1. Problem Statement — Root Cause

### Symptom

When a NinjaTrader 8 chart is opened cold (user has not manually touched the account dropdown since
NT8 startup), `WireLeaderAccount()` in [`TradeCopierAddOn.cs`](../../../src/PropTraderTools/TradeCopierAddOn.cs)
never calls `panel.SetLeaderAccount()`. The panel status bar shows:

```
No leader -- select account in ChartTrader
```

All panel buttons (BE, Trim, Flatten, Cancel, Tighten) remain dead until the user manually
touches the account dropdown.

### Root Cause

At chart inject time (`DoInject` → `WireLeaderAccount`), the WPF `ComboBox.SelectedItem` for the
ChartTrader account dropdown is a **framework placeholder** — an internal WPF data-binding sentinel
object, not yet a materialised `NinjaTrader.Cbi.Account`. The cast:

```csharp
var current = accountCombo.SelectedItem as NinjaTrader.Cbi.Account;
```

returns `null` silently. The null guard `if (current != null)` is never satisfied.
`SetLeaderAccount` is never called.

The `ComboBox.Text` property, however, already contains the *displayed string* of the selected
item (e.g., `"Sim101"`) even when `SelectedItem` has not materialised. This provides a recovery
path: look up the account by name in `Account.All`.

---

## 2. Fix Approach — Text-Fallback Lookup via Account.All.FirstOrDefault

### Strategy

After the failed cast, before subscribing `SelectionChanged`, add a single text-fallback block
that uses `accountCombo.Text` to locate the `Account` in `Account.All` by
`StringComparison.OrdinalIgnoreCase`.

### Exact Patch (locked architecture — DO NOT deviate)

**Before** (current code, lines ~451-453):

```csharp
var current = accountCombo.SelectedItem as NinjaTrader.Cbi.Account;
if (current != null) panel.SetLeaderAccount(current);
```

**After** (fixed code):

```csharp
var current = accountCombo.SelectedItem as NinjaTrader.Cbi.Account;
if (current == null && accountCombo.Text != null)
    current = Account.All.FirstOrDefault(
        a => string.Equals(a.Name, accountCombo.Text,
                           StringComparison.OrdinalIgnoreCase));
if (current != null) panel.SetLeaderAccount(current);
```

### Invariants

| # | Constraint | Why |
|---|-----------|-----|
| 1 | `StringComparison.OrdinalIgnoreCase` — NOT `==` | Case-sensitive cast fails silently; "Sim101" vs "sim101" would not match |
| 2 | `Account.All` scan runs ONCE at inject time only | Never inside a loop or timer; inject fires once per chart window |
| 3 | `SelectionChanged` subscription stays **unchanged** | It already handles all future account switches correctly |
| 4 | `SetLeaderAccount(null)` is acceptable | `SetLeaderAccount` already handles null; no new null path introduced |

---

## 3. Method Signature + CYC Analysis

### Signature (unchanged — no parameter changes)

```csharp
// TradeCopierAddOn.cs
private static void WireLeaderAccount(ChartTrader chartTrader, TradeCopierPanel panel)
```

**File**: `src/PropTraderTools/TradeCopierAddOn.cs`
**Visibility**: `private static`
**Return type**: `void`
**Parameters**: `ChartTrader chartTrader`, `TradeCopierPanel panel`

### CYC Branch-by-Branch Analysis

| Branch # | Condition | Present Before | Added by Fix |
|----------|-----------|---------------|-------------|
| 1 | `if (accountCombo == null)` — fallback to `FindVisualChildByIndex` | YES | — |
| 2 | `if (accountCombo == null) return` — early exit | YES | — |
| 3 | `if (current == null && accountCombo.Text != null)` | NO | **NEW** |
| 4 | `FirstOrDefault` predicate lambda (1 decision point) | NO | **NEW** |
| 5 | `if (current != null) panel.SetLeaderAccount(current)` | YES | — |
| 6 | `SelectionChanged += lambda` subscription path | YES | — |

**CYC before fix**: 4
**CYC after fix**: 6
**Jane Street ceiling**: 8
**Status**: PASS (6 ≤ 8)

---

## 4. NT8 Rules Check

### NT8-006 — `using System.Linq` Required for LINQ Extension Methods

**Status**: PASS — `using System.Linq` is confirmed at **line 18** of `TradeCopierAddOn.cs`.
`Account.All.FirstOrDefault(...)` requires `System.Linq` — present.

### NT8-021 — `Account.All` Must Not Be Accessed in Constructors or Field Initializers

**Status**: PASS — `WireLeaderAccount` is called from `DoInject`, which runs via
`chart.Dispatcher.InvokeAsync(() => DoInject(chart))` — a chart `Loaded`/`OnWindowCreated`
lifecycle path. NT8 account infrastructure is fully initialised by this point.

### NT8-042 — `Dispatcher.InvokeAsync` Not Available from AddOn Context

**Status**: PASS — The fix introduces **no** new `Dispatcher.InvokeAsync` call inside
`WireLeaderAccount`. The existing `chart.Dispatcher.InvokeAsync` in `InjectIntoChart` is a
WPF Window dispatcher call (not the banned `Globals.GeneralOptions.Dispatcher` or
`Application.Current.Dispatcher` variants) and is untouched.

### NT8-018 — `lock()` Is Banned

**Status**: PASS — No `lock()` anywhere in the fix.

### NT8-043 — Null-Conditional Compound Assignment (`?.` with `-=`) Is Banned (C# 7.3)

**Status**: PASS — The fix does not use `?.` on the left side of any assignment operator.

### Other NT8 P0 Rules

No `{ get; init; }`, no `abstract record`, no `volatile double`, no `ImmutableDictionary`,
no `async void`, no `DateTime.Now` in orders, no `OrderState.PendingSubmit`, no sealed
`Indicator` or `Window`. All PASS.

---

## 5. JS Rules Check

### JS-021 — No `lock()` Usage (P0 CRITICAL)

**Status**: PASS — `Account.All` is a read-only enumerable; no mutation, no lock required.

### JS-002 — Use Option\<T\> Instead of Null (P0 CRITICAL)

**Status**: PASS — `WireLeaderAccount` is `void`; it never returns a value.
The `current` local may be `null` after both paths fail (no SelectedItem cast, no name match).
In that case `SetLeaderAccount(null)` is called, which the existing method contract explicitly
handles. No new `return null` path is introduced.

**Note**: Pre-existing `return null` occurrences at `CopyEngine.cs` lines 653, 1059, 1065, 1118
(DW-B23-LANE-C-02) are outside this block's write-set and are not targeted here.

### JS-001 — No `throw` in Hot Paths (P0 CRITICAL)

**Status**: PASS — No exception throwing in the fix.

### JS-033 — No `async void` (P0 CRITICAL)

**Status**: PASS — No async usage.

### ASCII-Only Compliance

**Status**: PASS — `StringComparison.OrdinalIgnoreCase`, all identifiers, and the
`string.Equals` arguments are ASCII-only.

---

## 6. 7-Scan Checklist (Pre-Populated for Ticket Inheritance)

The ticket engineer MUST run all 7 scans against the final diff before marking the ticket complete.

| Scan | Pattern | Expected Result | Notes |
|------|---------|----------------|-------|
| SCAN-01 | `lock\s*\(` in `TradeCopierAddOn.cs` | 0 matches | JS-021 — lock ban |
| SCAN-02 | `async\s+void\s+\w+\(` in `TradeCopierAddOn.cs` | 0 matches | JS-033 — async void ban |
| SCAN-03 | `return\s+null\s*;` in changed method only | 0 matches (method is void) | JS-002 — confirm void method has no return value |
| SCAN-04 | `DateTime\.Now` in `TradeCopierAddOn.cs` | 0 matches | NT8-013 — DateTime.Now ban |
| SCAN-05 | `"#[0-9A-Fa-f]{6}"` string literals in `TradeCopierAddOn.cs` | 0 matches | NT8-028 — hex color ban |
| SCAN-06 | `\.Dispatcher\.InvokeAsync\|Globals\.Application\|GeneralOptions\.Dispatcher` in new code only | 0 matches | NT8-042 — banned Dispatcher paths |
| SCAN-07 | `StringComparison\.OrdinalIgnoreCase` present in `WireLeaderAccount` body | 1 match | Mandate: OrdinalIgnoreCase required (not `==`) |

---

## 7. Files Touched

| File | Change | Ticket |
|------|--------|--------|
| `src/PropTraderTools/TradeCopierAddOn.cs` | 3 lines added inside `WireLeaderAccount` body | T1 |

**Write-set size**: 1 file.
**No other files changed.**

---

## 8. [Fact] Delta

**Delta: 0** (zero tests added, zero tests removed).

**Rationale**: `WireLeaderAccount` is a UI visual-tree method that depends on:
- A live `ChartTrader` WPF visual tree
- A live `ComboBox` with a real `SelectedItem` / `Text`
- `Account.All` populated by NT8 runtime

None of these are available in the `CopyEngineTests` stub harness (no NT8 runtime, no WPF
message pump, no ComboBox). The method is not testable via xUnit in the current test project.
The [Fact] count of **126 remains unchanged**.

The verification contract is a **manual cold-start gate**:
1. Open MES chart with Sim101 selected in ChartTrader (cold start — do NOT touch the dropdown)
2. Panel status bar must read `"Ready: MES SEP26"` (not `"No leader"`)
3. F5 must be green (0 compiler errors)

---

## Deferred Backlog Status

Items from `docs/brain/PTT-COPIER-B23-LANE-C/06-deferred-backlog.md`:

| ID | Description | Status in B24-LANE-A |
|----|-------------|---------------------|
| DW-B23-LANE-C-01 | Add short-direction `[Fact]` test for `PendingBe_Armed_FiresAtPriceTarget_Short` | NOT targeted — P2, remains OPEN |
| DW-B23-LANE-C-02 | Pre-existing `return null` at `CopyEngine.cs` lines 653, 1059, 1065, 1118 | NOT targeted — P2, remains OPEN |

This lane closes: **DW-B24-LEADER-CASTNULL-01** (new defect, not a prior backlog item).

---

## Component Summary

| Component | Class | File | Role |
|-----------|-------|------|------|
| AddOn entry point | `TradeCopierAddOn` | `TradeCopierAddOn.cs` | Injects panel; wires account |
| Fix site | `WireLeaderAccount` (private static method) | `TradeCopierAddOn.cs` | Text-fallback lookup added |
| Panel target | `TradeCopierPanel.SetLeaderAccount(Account)` | `TradeCopierPanel.cs` | Receives account — unchanged |
| Account source | `NinjaTrader.Cbi.Account.All` | NT8 runtime | Scanned once at inject time |
