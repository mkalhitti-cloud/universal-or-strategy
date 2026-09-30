# PTT-COPIER-B2 — Phase 2: Plan Review

**Status:** REVIEW_PASS
**Reviewer:** PTT Plan Reviewer (Phase 2)
**Date:** 2026-07-06
**Plan file:** `docs/brain/PTT-COPIER-B2/02-architecture-plan.md`
**Source baseline:** PTT-COPIER-B1 (unmodified; all defects confirmed present in src/)

---

## Source State Confirmed

All three source files were read before this review. The B1 defects listed in the plan
were verified as present in the current source:

| Defect | File | Evidence in src/ |
|--------|------|-----------------|
| D1 | `TradeCopierWindow.cs` | `OnInitialize()` line 24-29: no `_engine.Subscribe()`. `OnDestroyed()` line 31-34: no `_engine.Unsubscribe()`. |
| D2a | `CopyEngine.cs` | Line 21: `private readonly List<CopyRule> _rules = new List<CopyRule>();` |
| D2b | `CopyEngine.cs` | Line 93: `internal void AddRule(CopyRule rule)` — only overload; `CopyRule` is private struct at line 28; unreachable from UI. |
| D2c | `TradeCopierPanel.cs` | Lines 55-68: comboboxes are local variables; populated with `acc.Name` strings via `Items.Add`; no Apply button; no `AddRule` call. |
| D2d | `TradeCopierWindow.cs` | Lines 137-143: `followerCb` has no `ItemsSource`; no Apply button column 7; no `AddRule` call. |
| D3 | `TradeCopierPanel.cs` | Line 89: `IsEnabled = false` (_trimBtn). Line 94: `IsEnabled = false` (_flattenBtn). Line 99: `IsEnabled = false` (_cancelBtn). |
| D4 | `TradeCopierWindow.cs` | Line 241: bare `catch` with no type — `catch` `{` `return null; }`. |
| D5 | `TradeCopierWindow.cs` | Line 63: `"BorderBrush"` (unqualified). Line 87: `"BorderBrush"` (unqualified). |

---

## Checklist Results

### Lifecycle Wiring

| # | Item | Verdict | Evidence |
|---|------|---------|---------|
| 1 | DEFECT-1 fix: `_engine.Subscribe()` added in `OnInitialize()` AFTER StatusUpdate subscription | **PASS** | Plan §T2 item 1 specifies: after `_engine.StatusUpdate += OnStatusUpdate;` add `_engine.Subscribe();`. Order is correct. Thread model diagram (§3) confirms this sequence. |
| 2 | DEFECT-1 fix: `_engine.Unsubscribe()` added in `OnDestroyed()` AFTER StatusUpdate unsubscription | **PASS** | Plan §T2 item 2 specifies: after `_engine.StatusUpdate -= OnStatusUpdate;` add `_engine.Unsubscribe();`. Order is correct. |
| 3 | `Panel.OnInitialize` does NOT call `Subscribe()` — confirmed in plan | **PASS** | Plan §3 thread model: "TradeCopierPanel: MUST NOT call Subscribe() or Unsubscribe()". Plan §T3 lists no Subscribe change. Confirmed in current source (Panel.OnInitialize lines 27-36). |
| 4 | `Panel.OnDestroyed` does NOT call `Unsubscribe()` — confirmed in plan | **PASS** | Same evidence as above. Panel.OnDestroyed lines 38-42 unchanged in plan. |

### Thread Safety

| # | Item | Verdict | Evidence |
|---|------|---------|---------|
| 5 | No new `lock()` introduced in any of the 3 files | **PASS** | Plan §12 concurrency contract: "Zero lock() statements". SCAN-01 guarantee in §8: 0 results. New methods OnApplyRule (§9.2), OnRowApply (§9.3), AddRule overload (§9.1) have no lock(). |
| 6 | `_rules` replaced with `ConcurrentBag<CopyRule>` — confirmed | **PASS** | Plan §T1 item 1: field declaration changed. Plan §4 chose ConcurrentBag over 4 alternatives. Code shown in §4: `private readonly System.Collections.Concurrent.ConcurrentBag<CopyRule> _rules = new System.Collections.Concurrent.ConcurrentBag<CopyRule>();` |
| 7 | `List<CopyRule>` fully removed — confirmed | **PASS** | Plan B2-SCAN-04: `grep "new List<CopyRule>" CopyEngine.cs` → 0 results. Plan §T1 item 1 is a direct replacement of line 21. |
| 8 | `ConcurrentBag.Add()` is thread-safe — confirmed | **PASS** | Plan §4: "Add(item) is thread-safe: uses thread-local storage + steal-other-thread fallback, no visible lock()." JS-021 compliance confirmed. |
| 9 | `ConcurrentBag foreach` is safe during concurrent Add — confirmed | **PASS** | Plan §4: "GetEnumerator() is thread-safe: returns a snapshot of items at time of enumeration. A concurrent Add() during foreach either appears in the snapshot or does not — both are safe outcomes." |
| 10 | `AddRule` string-based overload resolves the CopyRule-is-private problem | **PASS** | Plan §9.1: new overload `internal void AddRule(string instrument, Account master, Account[] followers)` constructs `CopyRule` internally via `CopyRule.Create(...)`. `CopyRule` privacy preserved (JS-003). |

### Rule Wiring

| # | Item | Verdict | Evidence |
|---|------|---------|---------|
| 11 | Panel has at least one path that calls `_engine.AddRule()` | **PASS** | Plan §9.2: `OnApplyRule()` handler calls `_engine.AddRule(_instrument.FullName, leader, new[] { follower })`. Wired to Apply button click. |
| 12 | Window `BuildRuleRow` has at least one path that calls `_engine.AddRule()` per row | **PASS** | Plan §9.3: `OnRowApply()` calls `_engine.AddRule(instrName, leader, new[] { follower })`. Wired via `applyBtn.Click += OnRowApply`. |
| 13 | Both Panel and Window bind Account OBJECTS (not strings) to ComboBoxes | **PASS** | Plan §9.2: `_leaderCombo.ItemsSource = Account.All` (Account objects). Plan §9.3: `leaderCb.ItemsSource = Account.All` (already in B1 for leader). `followerCb.ItemsSource = Account.All` added (D2d fix). Both surfaces use `SelectedItem as Account` cast. |
| 14 | `followerCb` in `BuildRuleRow` now has `ItemsSource` set | **PASS** | Plan §9.3 BuildRuleRow delta: `followerCb.ItemsSource = Account.All;` explicitly listed. B2-SCAN-04 N/A here; confirmed present in plan. |

### Exception Handling

| # | Item | Verdict | Evidence |
|---|------|---------|---------|
| 15 | No new `throw` in hot path (`OnOrderUpdate`, `SendCopy`, `IsDedup`) | **PASS** | Plan §6: "Gate chain in CopyEngine.OnOrderUpdate is UNCHANGED in B2." No throw added in any new method (§9.1 CYC=1, §9.2 OnApplyRule uses early return, §9.3 OnRowApply uses early return). |
| 16 | Bare `catch` replaced with `catch (Exception)` — confirmed in plan | **PASS** | Plan §T2 item 7: `catch {` → `catch (Exception) {`. B2-SCAN-06: `grep "catch {"` TradeCopierWindow.cs → 0 results post-fix. |
| 17 | JS-001: returning null from `FindInstrument` is acceptable (NT API boundary) | **PASS** | Plan §T2 xUnit test: `FindInstrument_ExceptionThrown_ReturnsCatchedException()` — typed catch verified. Plan §T2 SCAN-06: `catch` is an NT API boundary, not a hot-path logic error. |

### UI Fixes

| # | Item | Verdict | Evidence |
|---|------|---------|---------|
| 18 | `_trimBtn IsEnabled = true` — confirmed in plan | **PASS** | Plan §T3 item 5: "Line 89: `IsEnabled = false` → `IsEnabled = true` (trimBtn)". |
| 19 | `_flattenBtn IsEnabled = true` — confirmed in plan | **PASS** | Plan §T3 item 6: "Line 94: `IsEnabled = false` → `IsEnabled = true` (flattenBtn)". |
| 20 | `_cancelBtn IsEnabled = true` — confirmed in plan | **PASS** | Plan §T3 item 7: "Line 99: `IsEnabled = false` → `IsEnabled = true` (cancelBtn)". |

### Resource Keys

| # | Item | Verdict | Evidence |
|---|------|---------|---------|
| 21 | Both sep1 and sep2 in `TradeCopierWindow` use `"NTBrushes.BorderBrush"` — confirmed in plan | **PASS** | Plan §T2 items 5-6: line 63 and line 87 both changed from `"BorderBrush"` to `"NTBrushes.BorderBrush"`. SCAN-04 guarantee: 0 hex colors; B2-SCAN-07: `grep '"BorderBrush"'` → 0 results post-fix. |
| 22 | No plain `"BorderBrush"` key remains in `TradeCopierWindow` | **PASS** | Current source confirms exactly 2 occurrences at lines 63 and 87. Plan fixes both. B2-SCAN-07 verifies. |

### Spec HTML

| # | Item | Verdict | Evidence |
|---|------|---------|---------|
| 23 | Plan confirms all 10 SD items will be addressed in T4 | **PASS** | Plan §T4 table lists exactly 10 rows: `_rules` type, AddRule API, Subscribe lifecycle, Panel ComboBox population, Panel rule apply, Window follower ComboBox, Window row Apply, Panel action buttons, bare catch, border brush resource key. |
| 24 | SD-1..SD-10 each have before/after text documented | **PASS** | Plan §T4: each row has "Before" and "After" columns with specific text content. |

### B1 Deviations

| # | Item | Verdict | Evidence |
|---|------|---------|---------|
| 25 | D1 preserved: `TradeCopierPanel` remains `public sealed class` | **PASS** | Plan §7: "D1: UNCHANGED. B2 does not touch the class declaration. Still `public sealed class TradeCopierPanel : NTWindow`." Confirmed in src/PropTraderTools/TradeCopierPanel.cs line 16. |
| 26 | D2 preserved: `AddRule`/`Subscribe`/`Unsubscribe` naming retained | **PASS** | Plan §7: "D2: UNCHANGED. B2 adds a new AddRule(...) overload but does not rename or remove any existing method." |
| 27 | D3 preserved: `CopyEngine` remains `internal sealed` | **PASS** | Plan §7: "D3: UNCHANGED. Still `internal sealed class CopyEngine`." Confirmed in src/PropTraderTools/CopyEngine.cs line 12. |

### 7-Scan + B2-Scan Coverage

| # | Item | Verdict | Evidence |
|---|------|---------|---------|
| 28 | SCAN-01 (`lock(`): 0 results guaranteed post-fix | **PASS** | Plan §8 SCAN-01: `ConcurrentBag.Add()` and `.GetEnumerator()` use no visible `lock()`. All new handlers have zero lock statements. |
| 29 | SCAN-B2-01: `Subscribe()` appears exactly twice in `TradeCopierWindow` (`OnInitialize` + `OnDestroyed`) | **PASS** | Plan §8 B2-SCAN-03: "Exactly 2 results: one in `OnInitialize()` and one in `OnDestroyed()`." |
| 30 | SCAN-B2-02: `Subscribe()` appears 0 times in `TradeCopierPanel` | **PASS** | Plan §8 B2-SCAN-02: "0 results — Panel must never call these." Confirmed in T3 change list (no Subscribe added). |
| 31 | SCAN-B2-03: `ConcurrentBag` appears in `CopyEngine.cs` | **PASS** | Plan §8 B2-SCAN-05: "Exactly 1 result: the `_rules` field declaration." |
| 32 | SCAN-B2-04: `List<CopyRule>` is gone from `CopyEngine.cs` | **PASS** | Plan §8 B2-SCAN-04: "0 results — must be ConcurrentBag only." T1 item 1 is a direct field replacement. |
| 33 | SCAN-B2-05: `IsEnabled = false` for action buttons is gone from `TradeCopierPanel` | **PASS** | Plan §8 B2-SCAN-08: "0 results in button declarations — D3 fix applied." T3 items 5-7 fix all three. |
| 34 | SCAN-B2-06: `AddRule` call exists in `TradeCopierWindow.cs` | **PASS** | Plan §9.3 `OnRowApply()`: `_engine.AddRule(instrName, leader, new[] { follower });`. Rule wiring diagram §5 confirms. |
| 35 | SCAN-B2-07: `AddRule` call exists in `TradeCopierPanel.cs` | **PASS** | Plan §9.2 `OnApplyRule()`: `_engine.AddRule(_instrument.FullName, leader, new[] { follower });`. Rule wiring diagram §5 confirms. |
| 36 | SCAN-B2-08: `"BorderBrush"` (unqualified) is gone from `TradeCopierWindow.cs` | **PASS** | Plan §8 B2-SCAN-07: "0 results — D5 fix applied." T2 items 5-6 fix both sep1 and sep2. |
| 37 | SCAN-B2-09: bare `catch` is gone from `TradeCopierWindow.cs` | **PASS** | Plan §8 B2-SCAN-06: "0 results — bare catch must be gone." T2 item 7 applies typed catch. |

### Misc

| # | Item | Verdict | Evidence |
|---|------|---------|---------|
| 38 | `TrimSignal` struct still unused — accepted dead code, not a bug | **PASS** | Plan §6 "Gate chain confirmation" lists `TrimSignal` as part of unchanged internals. Plan §1: "Gate chain internals are UNCHANGED." Struct is dead but not a new violation — B1 accepted state. |
| 39 | CYC budget for all new methods ≤ 8 | **PASS** | Plan §11: `AddRule(string, Account, Account[])` CYC=1; `OnApplyRule()` CYC=6; `OnRowApply()` CYC=3. All ≤ 8 (Jane Street strict standard). |
| 40 | No `async`/`await` added to lifecycle methods | **PASS** | Plan §9.2 and §9.3 show synchronous handler signatures. `Dispatcher.InvokeAsync` usage in `OnStatusUpdate` is unchanged from B1 (plan §3: "NO CHANGES in B2"). |

---

## Violations

**None.**

All 40 checklist items: PASS.

---

## REVIEW_PASS
