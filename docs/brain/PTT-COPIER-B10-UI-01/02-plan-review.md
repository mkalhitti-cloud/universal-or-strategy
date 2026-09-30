# PTT-COPIER-B10-UI-01 — Plan Review

**Status**: REVIEW_PASS
**Epic**: PTT-COPIER-B10-UI-01
**Plan reviewed**: `docs/brain/PTT-COPIER-B10-UI-01/02-architecture-plan.md`
**Spec reviewed**: `specs/002-trade-copier-spec.html`
**Rules reviewed**: `docs/standards/jane-street/RULES_CATALOG.md`
**Date**: 2026-07-07
**Reviewer**: PTT Plan Reviewer

---

## Result

> **REVIEW_PASS**

Zero violations. All 9 checklist items pass. All 7 scans pass. No JS-XXX rule breaches found.

---

## Violations Found

*None.*

---

## Checklist Results

| # | Check | Result | Evidence |
|---|-------|--------|----------|
| 1 | Scope: DW-B10-UI-01 ONLY | **PASS** | Plan §1 states "This block addresses DW-B10-UI-01 only." §8 confirms single-file change to TradeCopierPanel.cs. All B9 deferred items listed in §12 as carry-forward notice only, zero plan items. |
| 2 | Technical: WPF Grid + FrameworkElementFactory + Loaded event | **PASS** | §3.1 correctly describes `FrameworkElementFactory(typeof(Grid))` with `RoutedEventHandler` on `FrameworkElement.LoadedEvent`. §3.2 explains why Loaded is required (ColumnDefinitions only accessible post-instantiation) and cites existing `OnRowApply` handlers as in-codebase precedent. |
| 3 | Column spec: Col0=`*`+MinWidth80+ellipsis, Col1=62 Right, Col2=30, Col3=80, Col4=80 Collapsed, Col5=20 Center | **PASS** | §5 table and code sample match exactly: Col0 `*`/MinWidth=80/CharacterEllipsis, Col1 62px/Right, Col2 30px, Col3 80px, Col4 80px/Collapsed, Col5 20px/Center. |
| 4 | OnRowGridLoaded CYC=2 (null guard + already-configured guard) | **PASS** | §4.2 states CYC=2. §5 code shows exactly two branches: `if (sender is not Grid grid) return;` and `if (grid.Tag is bool) return;`. No other branches exist. |
| 5 | BuildCheckItemTemplate CYC stays at 1 | **PASS** | §4.1 states "CYC after change: 1 (no branches — pure factory construction)." Body is a sequential factory build with zero conditional branches. |
| 6 | 7-scan checklist present and all PASS | **PASS** | §10 contains all 7 scans (SCAN-01 through SCAN-07), each with rule ID, check description, and PASS result. See detailed scan validation below. |
| 7 | No scope creep into B10 main tickets | **PASS** | §8 File Scope confirms CopyEngine.cs, TradeCopierWindow.cs, AtrSizingEngine.cs, and tests are all "No change." No trailing-stop, BE-watcher, ATR-box, or tighten-stop logic anywhere in the plan. |
| 8 | Deferred B9 OPEN items noted as carry-forward | **PASS** | §1 and §12 list all 9 deferred items (DW-B9-01 through DW-B10-GAP-002b) with IDs, priorities, and status=OPEN. Explicit statement: "Zero plan items for those tickets in this block." |
| 9 | Method signatures well-defined | **PASS** | `BuildCheckItemTemplate()` — private, returns `DataTemplate`, no parameters (§4.1). `OnRowGridLoaded(object sender, RoutedEventArgs e)` — private, returns `void`, standard RoutedEventHandler (§4.2). Both include access modifier, return type, and parameter types. |

---

## 7-Scan Detail Validation

| Scan | Rule ID | Claim in Plan | Reviewer Verdict |
|------|---------|---------------|-----------------|
| SCAN-01 | JS-021 — No `lock()` | "No lock() in new or modified code" | **PASS** — No lock() in any plan code sample. Tag guard is a DependencyProperty UI-thread operation, not a lock. |
| SCAN-02 | JS-033 — No `async void` (non-EventHandler) | "`OnRowGridLoaded` is `void` (sync), not `async void`" | **PASS** — Handler is synchronous `void`. JS-033 bans `async void`; plain `void` for WPF event handlers is the correct NT8 pattern. |
| SCAN-03 | JS-001 — No `throw` in business logic | "Null guard uses `return`, not `throw`" | **PASS** — Plan code shows `return;` for both guards. No `throw` expression anywhere. |
| SCAN-04 | JS-002 — No `return null` | "`BuildCheckItemTemplate` returns non-null `DataTemplate`; `OnRowGridLoaded` is `void`" | **PASS** — Factory method returns `new DataTemplate { VisualTree = gridFactory }`. No null path exists. |
| SCAN-05 | JS-036/JS-037 — No `byte[]` heap alloc in hot path | "No buffers — pure WPF layout" | **PASS** — Plan introduces only WPF layout objects (Grid, ColumnDefinition). No buffer allocation. |
| SCAN-06 | ASCII-only; no FontFamily; no #RRGGBB hex; no `CreateOrder` without PTT- prefix; no `DateTime.Now` | "All identifiers and strings are ASCII; no FontFamily; no hex color literals" | **PASS** — No #RRGGBB literals, no FontFamily assignment, no CreateOrder call, no DateTime.Now. §9 NT8 API table confirms FontFamily BANNED and not used. |
| SCAN-07 | CYC ≤ 8 for all methods | `BuildCheckItemTemplate` CYC=1; `OnRowGridLoaded` CYC=2 | **PASS** — Maximum CYC across both methods is 2. Well within the CYC ≤ 8 hard limit. |

---

## DNA Block Checks (Hardcoded Rules)

| Rule | Check | Result |
|------|-------|--------|
| JS-021 — No `lock()` | No lock() in plan code | PASS |
| JS-023 — UI update from off-thread without Dispatcher.InvokeAsync | `OnRowGridLoaded` fires on WPF UI thread by invariant; Dispatcher not needed (§7) | PASS |
| JS-001 — `throw` in OnOrderUpdate / SendCopy / gate chain | Not applicable; plan touches view layer only | PASS |
| JS-002 — `return null` where value expected | `BuildCheckItemTemplate` returns concrete `DataTemplate` | PASS |
| JS-003 — Magic string for discriminated state | No string-discriminated state in this plan | PASS |
| JS-008 — Mutable fields on struct / SolidColorBrush not Freeze()d | No structs, no Brushes introduced | PASS |
| JS-009 — `Dictionary<K,V>` for shared/thread-touched collection | No new collections | PASS |
| JS-010 — Public constructor on singleton or signal struct | No new classes or structs introduced | PASS |
| NT8: `async`/`await` in `OnInitialize`/`OnDestroyed`/`OnWindowCreated` | Not present | PASS |
| NT8: `Account.All` in constructor | Not present | PASS |
| NT8: `sealed TradeCopierWindow` | Not modified | PASS |
| NT8: `FontFamily` override (SCAN-03) | §9 confirms FontFamily BANNED and not used | PASS |
| NT8: Hardcoded `#RRGGBB` hex (SCAN-04) | Not present | PASS |
| NT8: `CreateOrder` without PTT- prefix (SCAN-05) | Not present | PASS |
| NT8: `DateTime.Now` (SCAN-06) | Not present | PASS |
| CYC > 8 on any method | Max CYC in plan = 2 | PASS |

---

## Spec Coverage Matrix

| Spec Requirement | Addressed? | Plan Section |
|------------------|-----------|--------------|
| Follower dropdown row shows account name | Yes — Col0 TextBlock, AccountName binding preserved | §4.1, §5, §6 |
| Follower dropdown row shows daily P&L | Yes — Col1 TextBlock, DailyPnl binding preserved | §4.1, §5, §6 |
| Follower dropdown row shows per-follower multiplier TextBox (B8) | Yes — Col2 TextBox, Multiplier TwoWay binding preserved | §4.1, §5, §6 |
| Follower dropdown row shows per-follower ATM ComboBox (B8) | Yes — Col3 ComboBox, AtmName TwoWay binding preserved | §4.1, §5, §6 |
| Follower dropdown row shows Named TextBox (B8) | Yes — Col4 TextBox, Visibility=Collapsed preserved | §4.1, §5, §6 |
| Follower dropdown row shows CheckBox (B7) | Yes — Col5 CheckBox, IsChecked binding preserved | §4.1, §5, §6 |
| All existing bindings unchanged | Yes — plan explicitly states "Bindings are unchanged" | §4.1 |
| ViewModel, engine, gate chain untouched | Yes — §6 Data Flow, §8 File Scope confirm zero changes above DataTemplate layer | §6, §8 |
| NT-native appearance (100% NT WPF theme) | Yes — no custom colors, no FontFamily, no hex literals. All controls already use NT styles | §9, §10 SCAN-06 |
| No B10 main tickets in scope | Yes — trailing stop, BE watcher, ATR box, tighten stop, click trader all excluded | §1, §8, §12 |

All spec requirements for DW-B10-UI-01 are addressed. No requirement gap found.

---

## Reviewer Notes

1. The spec at line 1302 and 1551 still describes a "horizontal StackPanel ItemTemplate" — this is the *pre-fix* B7 state. The plan correctly identifies this as the problem and replaces it with a Grid. The spec does not mandate StackPanel; it describes the desired *content* (name, P&L, checkmark extended by B8's mult + ATM). The Grid layout is the fix for vertical misalignment and is within scope.

2. The `Tag = true` guard pattern (`grid.Tag is bool`) prevents re-entry on re-layout. This is idiomatic for NT8's WPF environment where templates can be applied more than once. No concurrency concern: `OnRowGridLoaded` is UI-thread-affined by WPF invariant.

3. The plan correctly notes that `FrameworkElementFactory` cannot add `ColumnDefinitions` before instantiation — this is a WPF platform constraint. The Loaded-event workaround is the canonical solution and is already used in `TradeCopierWindow.cs`.

4. No new `using` directives are needed (§11) — the required WPF types are already imported in `TradeCopierPanel.cs`.
