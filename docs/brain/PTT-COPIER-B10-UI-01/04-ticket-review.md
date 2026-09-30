# Ticket Review: PTT-COPIER-B10-UI-01

**Reviewer**: PTT Ticket Reviewer (v12-phase4-5-review)
**Date**: 2026-07-07
**Tickets in scope**: 1 (T1 only)
**Architecture Plan**: 02-architecture-plan.md (REVIEW_PASS)
**Spec**: specs/002-trade-copier-spec.html

---

## T1 — DW-B10-UI-01: Follower dropdown Grid column alignment

### CHECK 1 — TRACEABILITY

All ticket requirement references map to verified spec content:

| Ticket Ref | Spec Location | Verified Content |
|---|---|---|
| B7-UI-PANEL-FOLLOWER | Line 1302 | `ComboBox` with horizontal `StackPanel ItemTemplate`. Row: `[account name] [daily P&L] [checkmark]` — confirmed. |
| B7-UI-PANEL-FOLLOWER-DETAIL | Line 1303 | `FollowerItem` INPC, `DailyPnlText`, `DailyPnlColor`, `GetSelectedFollowers()` pattern — confirmed. |
| B7-EVOLUTION-TABLE | Lines 1544–1562 | B7 row layout: "horizontal `StackPanel` — left to right: account name | daily P&L | checkmark" — confirmed. |
| B8-PANEL-ROW | Line 1975 | "Panel: per-follower mult TextBox + ATM ComboBox in each row" — confirmed. |

No phantom work (ticket items not in plan/spec). No missing work (plan Section 1 and Section 8 account for all B10-UI-01 scope; all other B9/B10 open items explicitly deferred).

**Traceability: PASS**

---

### CHECK 2 — SCOPE

- Single ticket T1 only in file — no scope creep.
- Plan Section 1 explicitly lists all deferred B9/B10 items (DW-B9-01, DW-B9-02, DW-B9-03, DW-B9-GAP-001a/b/c/d, DW-B10-GAP-002a/b) with "Zero plan items for those tickets in this block."
- Plan Section 8 confirms single-file scope: only `src/PropTraderTools/TradeCopierPanel.cs` changes; CopyEngine.cs, TradeCopierWindow.cs, AtrSizingEngine.cs, and tests/CopyEngineTests.cs — **No change** on all four.

**Scope: PASS**

---

### CHECK 3 — JS PRE-CHECK (7-scan checklist present)

The ticket body contains a full inline 7-scan table (SCAN-01 through SCAN-07):

| Scan | Rule | Ticket Annotation | Result |
|---|---|---|---|
| SCAN-01 | JS-021 — No `lock()` | `grep -n "lock(" src/PropTraderTools/TradeCopierPanel.cs` → 0 new hits. Tag guard is UI-thread-affined DependencyProperty. | PASS |
| SCAN-02 | JS-033 — No `async void` (non-EventHandler) | `OnRowGridLoaded` is synchronous `void`, NOT `async void`. No async operation. | PASS |
| SCAN-03 | JS-001 — No `throw` in business logic | Null/type guard uses `return`, not `throw`. | PASS |
| SCAN-04 | JS-002 — No `return null` | `BuildCheckItemTemplate()` returns `new DataTemplate { ... }` — never null. `OnRowGridLoaded` is `void`. | PASS |
| SCAN-05 | JS-036/037 — No `byte[]`/`T[]` heap alloc in hot path | No byte buffers. `ColumnDefinition` objects are WPF layout objects, not hot-path allocations. | PASS |
| SCAN-06 | ASCII-only | All new identifiers ASCII; no `FontFamily`; no hex color literals. | PASS |
| SCAN-07 | CYC ≤ 8 | `BuildCheckItemTemplate` CYC=1; `OnRowGridLoaded` CYC=2. Both ≤ 8. | PASS |

All 7 scans present with inline verification commands and PASS annotations.

**JS Pre-Check: PASS**

---

### CHECK 4 — CYC PRE-CHECK

| Method | CYC | At-risk? |
|---|---|---|
| `BuildCheckItemTemplate()` | 1 (no branches — pure factory construction) | No |
| `OnRowGridLoaded(object sender, RoutedEventArgs e)` | 2 (two `if` guards: type guard + already-configured guard) | No |

Both methods well within CYC ≤ 8. No at-risk methods in the diff.

**CYC Pre-Check: PASS**

---

### CHECK 5 — NT8 CONSTRAINTS

All NT8 hard constraints verified against ticket and architecture plan:

| Constraint | Status |
|---|---|
| No `async/await` in lifecycle method | PASS — `OnRowGridLoaded` is synchronous `void` |
| No `Account.All` outside Loaded handler | PASS — no `Account.All` call anywhere in ticket scope |
| No `sealed` on `TradeCopierWindow` | PASS — `TradeCopierWindow` is not touched |
| No `FontFamily` on WPF element | PASS — SCAN-06 explicitly confirms no `FontFamily` |
| No hardcoded hex color | PASS — SCAN-06 explicitly confirms no hex color literals |
| No `CreateOrder` with name not starting "PTT-" | PASS — no order creation in this ticket |
| No `DateTime.Now` | PASS — no date/time usage in this ticket |
| `FrameworkElementFactory` + `Loaded` event valid for NT8 WPF | PASS — Plan Section 3.2 explains canonical WPF workaround; pattern already used in `TradeCopierWindow.cs` (`OnRowApply` handlers) |
| UI update from non-UI thread | PASS — `OnRowGridLoaded` fires on WPF UI thread (WPF `Loaded` event invariant); no `Dispatcher.InvokeAsync` needed and none used incorrectly |

**NT8 Check: PASS**

---

### CHECK 6 — COMPLETENESS (column spec)

All 6 columns present with exact widths and constraints:

| Col | Content | Width | Alignment/Constraint | Ticket Match |
|---|---|---|---|---|
| 0 | Account name | `*` Star, MinWidth=80 | TextTrimming=CharacterEllipsis | PASS |
| 1 | Daily P&L | 62 px (fixed) | TextAlignment=Right | PASS |
| 2 | Multiplier TB | 30 px (fixed) | TwoWay binding preserved | PASS |
| 3 | ATM ComboBox | 80 px (fixed) | TwoWay binding preserved | PASS |
| 4 | Named TB | 80 px (fixed) | Visibility=Collapsed | PASS |
| 5 | CheckBox | 20 px (fixed) | HorizontalAlignment=Center | PASS |

Column spec is complete: all 6 columns, exact pixel widths, alignments, and binding preservation notes present.

**Completeness: PASS**

---

### CHECK 7 — TEST COVERAGE

Ticket explicitly states: *"None required. This is a pure view-layer layout change. No business logic, no engine behavior, no data model mutations. The tests/CopyEngineTests.cs file is unchanged."*

Rationale is sound: `BuildCheckItemTemplate()` and `OnRowGridLoaded()` are WPF factory methods with no observable output beyond UI layout. No new public or internal API is introduced. Architecture plan Section 8 confirms `tests/CopyEngineTests.cs` — No change.

**Test Coverage: PASS** (explicit justification present for zero tests; consistent with spec pattern for pure UI layout changes)

---

### CHECK 8 — IMPLEMENTATION STEPS

10 numbered steps verified for precision:

- Step 1: Open specific file (no guessing) ✅
- Step 2: Locate specific method `BuildCheckItemTemplate()`, identifies current root factory type ✅
- Step 3: Replace root factory — inline `csharp` code snippet ✅
- Step 4: Register Loaded handler — inline `csharp` code snippet ✅
- Step 5: Update each child factory with `Grid.ColumnProperty` — inline `csharp` code snippet with all 6 column assignments ✅
- Step 6: Preserve all existing `SetBinding`/`SetValue` calls verbatim ✅
- Step 7: Append all children in order — inline `csharp` code snippet ✅
- Step 8: Return template — inline `csharp` code snippet ✅
- Step 9: Add `OnRowGridLoaded` as new private method immediately after `BuildCheckItemTemplate` — full method body inline ✅
- Step 10: Verify no new `using` directives needed — explicitly lists all types already imported ✅

No step requires an engineer to guess intent, look up column widths, or infer ordering.

**Implementation Steps: PASS**

---

### CHECK 9 — BUILD GATE

Exact build command present:

```powershell
dotnet build Linting.csproj /p:Configuration=Release /clp:ErrorsOnly
```

Success criteria: "0 errors, 0 warnings for modified file."
F5 NinjaTrader gate also appears as a named acceptance criterion ("F5 in NinjaTrader compiles green").

**Build Gate: PASS**

---

### CHECK 10 — ACCEPTANCE CRITERIA

11 measurable, unambiguous criteria:

1. All six columns align vertically across every row regardless of account name length ✅ (observable)
2. Account names trimmed with ellipsis — no overflow, no horizontal scrollbar ✅ (observable)
3. Column widths match spec: name=`*`(min 80), P&L=62, Mult=30, ATM=80, Named=80, Checkbox=20 ✅ (exact numerics)
4. All existing bindings (AccountName, DailyPnlText, DailyPnlColor, Multiplier, AtmName, IsChecked) remain functional ✅
5. `FollowerItem.INotifyPropertyChanged` live P&L updates still render green/red/dim ✅
6. "N selected" header count remains accurate after check/uncheck ✅
7. `OnRowGridLoaded` does not re-apply `ColumnDefinitions` on re-layout (Tag guard fires on second pass) ✅
8. `GetSelectedFollowers()` behavior unchanged ✅
9. Build gate passes: `dotnet build Linting.csproj /p:Configuration=Release /clp:ErrorsOnly` → 0 errors ✅
10. All 7 scans PASS (SCAN-01 through SCAN-07) ✅
11. F5 in NinjaTrader compiles green ✅

**Acceptance Criteria: PASS**

---

## Summary

| Check | Result |
|---|---|
| CHECK 1 — Traceability | **PASS** |
| CHECK 2 — Scope | **PASS** |
| CHECK 3 — JS Pre-Check (7-scan present) | **PASS** |
| CHECK 4 — CYC Pre-Check | **PASS** |
| CHECK 5 — NT8 Constraints | **PASS** |
| CHECK 6 — Completeness (column spec) | **PASS** |
| CHECK 7 — Test Coverage | **PASS** |
| CHECK 8 — Implementation Steps | **PASS** |
| CHECK 9 — Build Gate | **PASS** |
| CHECK 10 — Acceptance Criteria | **PASS** |

---

## Overall: TICKET_REVIEW_PASS
