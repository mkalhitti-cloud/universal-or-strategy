# Ticket Review: PTT-COPIER-B7
# Phase 4.5 output. Written by ptt-ticket-reviewer.
# Source tickets: docs/brain/PTT-COPIER-B7/04-tickets.md
# Plan reviewed against: docs/brain/PTT-COPIER-B7/02-architecture-plan.md
# Spec reviewed against: specs/002-trade-copier-spec.html
# Rules applied: docs/standards/jane-street/RULES_CATALOG.md
# Status: REVIEW_COMPLETE

---

## Ticket Review: PTT-COPIER-B7

---

### T1 — CopyEngine + Tests (P0)

#### Traceability: PASS

All ticket items map to plan or spec. No phantom work detected. No missing plan items.

| Ticket Item | Plan / Spec Citation | Status |
|-------------|----------------------|--------|
| B7-F0 bracket mirroring | spec line 2162; plan Section 1 | ✅ |
| `_orderMap: ConcurrentDictionary<string, ConcurrentBag<FollowerBinding>>` | spec line 2175-2176 (ConcurrentBag is plan-approved lock-free upgrade over spec's `List<>`, V01); plan Section 1 new field | ✅ |
| `FollowerBinding` readonly struct | spec line 2195 (pill); plan Section 1.5 | ✅ |
| `FromEntrySignal` name matching in `FindFollowerBracketOrder` | spec lines 2181, 2188, 1846-1847; plan V01 | ✅ |
| Stop leg: StopPrice sync; Target leg: LimitPrice sync | spec lines 2183-2184; plan Section 1 method 3 | ✅ |
| Price delta >= 1 tick guard | spec line 2189; plan V02 | ✅ |
| `PositionState` readonly struct | spec lines 1045, 1052; plan V05, Section 1.5 | ✅ |
| `PositionStateChanged` event | spec lines 716-717; plan V05, Section 1.5 | ✅ |
| `FollowerAtmMode` sealed record hierarchy | spec lines 1045, 2335; plan V06, Section 1.5 | ✅ |
| `ImmutableDictionary<string, FollowerAtmMode>` on `CopyRule` | spec lines 1059, 2340; plan V07, Section 1.5 | ✅ |
| 5 new xUnit [Fact] tests | spec line 2196 (min 2; 5 exceeds minimum); plan Section 1 Test Plan | ✅ |
| `DispatchCopy` extraction from `OnOrderUpdate` | plan Section 1 method 1 | ✅ |
| `IsWorkingBracket` predicate | plan Section 1 method 2 | ✅ |
| `HandleBracketChange` with V02 price-delta guard | plan Section 1 method 3 | ✅ |
| `FindFollowerBracketOrder` nullable return (V03) | plan Section 1 method 4 | ✅ |
| `PopulateOrderMap` with mandatory dedup guard | plan Section 1 method 5 + Engineer Note #1 | ✅ |
| `TryFirePositionState` pre-gate call | plan Section 1 method 6 | ✅ |
| `HasOpenPosition` helper | plan Section 1 helper methods | ✅ |
| `HasWorkingEntries` helper | plan Section 1 helper methods | ✅ |
| `OnOrderUpdate` restructured (CYC=7) | plan Section 1 Modified Method | ✅ |
| `using System.Collections.Immutable` directive | plan Section 1 New Using Directive | ✅ |
| `CopyRule.Create` factory update (V07 Engineer Note #5) | plan Section 1.5 CopyRule.FollowerAtmTemplates field | ✅ |
| `TradeCopierAddOn.cs` UNCHANGED | plan Section 6 File Change Summary | ✅ |

No plan items missing from ticket. No ticket items absent from plan/spec.

---

#### JS Pre-Check: PASS

| Check | Ticket Description | Rule | Status |
|-------|--------------------|------|--------|
| `lock()` usage | Not described. `_orderMap` uses `ConcurrentDictionary.GetOrAdd` (atomic); inner value uses `ConcurrentBag.Add` (lock-free). | JS-021 (P0) | ✅ |
| `throw` in dispatch path (`OnOrderUpdate`, `HandleBracketChange`, `DispatchCopy`) | `HandleBracketChange` wraps `acc.Change()` in try/catch; catches and fires `StatusUpdate` — no throw propagated. `DispatchCopy` uses early-return gates, no throw. | JS-001 (P0) | ✅ |
| `null` return from non-nullable method | `FindFollowerBracketOrder` return type is `Order?` — nullable annotation makes null contract explicit and compile-time verifiable. All callers use `if (fo == null) continue`. This is the approved NT8-compatible form of JS-002 per plan V03. | JS-002 (P0) | ✅ |
| `Dictionary<K,V>` for shared state | `_orderMap` uses `ConcurrentDictionary<string, ConcurrentBag<FollowerBinding>>` (JS-025). `FollowerAtmTemplates` uses `ImmutableDictionary` (JS-009). No plain `Dictionary<K,V>` on any shared or struct field. | JS-009 (P2), JS-025 (P1) | ✅ |
| Mutable struct | `FollowerBinding` declared `internal readonly struct` with `{ get; init; }` properties. `PositionState` declared `public readonly struct` with `{ get; init; }` properties. Both are immutable. | JS-008 (P1) | ✅ |
| `SolidColorBrush` without `Freeze()` | No brush creation in T1 (CopyEngine). N/A. | JS-008 (P1) | ✅ N/A |
| `DateTime.Now` | Not described anywhere in T1. | NT8 hard constraint | ✅ |
| Hardcoded hex color `#RRGGBB` | No hex strings in CopyEngine. SCAN-04 expected count = 0. | SCAN-04 | ✅ |
| `FontFamily` override | Not described. SCAN-03 expected count = 0. | SCAN-03 / NT8 | ✅ |
| `CreateOrder` without `PTT-` prefix | No new `CreateOrder` calls. Existing `"PTT-Copy"` call site is unchanged. SCAN-05 expected count = 0. | SCAN-05 | ✅ |
| `FollowerAtmMode` sealed record private base constructor | `private FollowerAtmMode() { }` in abstract record body. Nested records (`Inherit`, `Market`, `Named`) inside the abstract record body (JS-010; Engineer Note #4 mandatory). | JS-010 (P0) | ✅ |

---

#### CYC Pre-Check: PASS

| Method | Ticket CYC | Plan CYC | Expected | Status |
|--------|-----------|---------|---------|--------|
| `OnOrderUpdate` (restructured) | 7 | 7 | ≤ 8 | ✅ |
| `DispatchCopy` | 6 | 6 | ≤ 8 | ✅ |
| `IsWorkingBracket` | 1 | 1 | ≤ 8 | ✅ |
| `HandleBracketChange` | 8 | 8 | ≤ 8 (at limit) | ✅ |
| `FindFollowerBracketOrder` | 4 | 4 | ≤ 8 | ✅ |
| `PopulateOrderMap` | 2 | 1 (+1 for dedup guard per Engineer Note #1, plan-approved) | ≤ 8 | ✅ |
| `TryFirePositionState` | 2 | 2 | ≤ 8 | ✅ |
| `HasOpenPosition` | 2 | 2 | ≤ 8 | ✅ |
| `HasWorkingEntries` | 3 | 3 | ≤ 8 | ✅ |

`PopulateOrderMap` CYC discrepancy (plan=1 vs ticket=2): plan body shows CYC=1 but Engineer Note #1 documents a mandatory dedup guard adding 1 branch. The ticket correctly reflects CYC=2 post-note. This is an approved plan addendum, not a violation. CYC=2 ≤ 8. ✅

Branch count verification for `HandleBracketChange` (8 branches, at limit):
(1) isStop ternary, (1) instrument null guard, (1) tickSize null-coalescing conditional, (1) rawPrice ternary, (1) foreach loop, (1) acc null continue, (1) fo null continue, (1) price-delta guard. Total = 8. ✅ try/catch does not add to CYC (plan confirmed). No at-risk method for CYC > 8.

---

#### NT8 Constraint Check: PASS

| Constraint | Ticket Description | Status |
|------------|--------------------|--------|
| No `async/await` in lifecycle methods | `OnOrderUpdate`, `HandleBracketChange`, and all new methods are synchronous. No async/await described. | ✅ |
| Off-thread UI update without `Dispatcher.InvokeAsync` | `HandleBracketChange` fires `StatusUpdate` (a string event, not a WPF UI update). `CopyEngine` is not a UI class — no `Dispatcher.InvokeAsync` needed in engine context. Plan NT8 Constraints table confirms this. `PositionStateChanged` fires an event; UI handlers own the dispatcher wrap. | ✅ |
| `Account.All` outside Loaded handler | Not accessed in T1. | ✅ |
| `TradeCopierWindow` modified from sealed | CopyEngine.cs contains no Window declaration. SCAN-07 N/A for T1. | ✅ |
| `acc.Change(new Order[] { fo })` pattern | Matches established NT8 pattern at CopyEngine.cs:443 (`MoveStopToBreakEven`). No new API pattern. | ✅ |

---

#### Completeness: PASS

| Item | Status |
|------|--------|
| All 2 T1 in-scope files addressed (`CopyEngine.cs`, `CopyEngineTests.cs`) | ✅ |
| `TradeCopierAddOn.cs` explicitly marked UNCHANGED | ✅ (plan summary table; ticket summary table) |
| T2 dependency on T1 explicitly stated | ✅ (Engineer Note #2; T2 Dependency section) |

---

#### Test Coverage: PASS

Rule scope: "Missing [Fact] for any **public or internal** method = FAIL." All 9 new methods in T1 are `private` (or `private static`). The 5 specified [Fact] tests provide reflection-based coverage for the most critical new methods and exceed the spec minimum of 2.

| Test ID | Method Name | Verified Behavior | Rule Scope | Status |
|---------|------------|-------------------|-----------|--------|
| T-B7-01 | `DispatchCopy_MethodExists` | private method exists, 2 params | private — bonus | ✅ |
| T-B7-02 | `IsWorkingBracket_MethodExists` | private static method exists, 1 param | private — bonus | ✅ |
| T-B7-03 | `HandleBracketChange_NullGuards_DoNotThrow` | null-adjacent inputs, no unhandled exception | private — bonus | ✅ |
| T-B7-04 | `FindFollowerBracketOrder_NullableReturnType` | return type is `Order?` nullable | private — bonus | ✅ |
| T-B7-05 | `OnOrderUpdate_WithWorkingBracket_DoesNotDispatchCopy` | Gate B diverts bracket to HandleBracketChange path | private — bonus | ✅ |

Test names match plan test table (Section 1 Test Plan) exactly. xUnit `[Fact]` only confirmed. No `[Theory]`, no NUnit, no MSTest.

---

#### Scan Checklist: PASS

All 7 scans present in ticket with expected count = 0.

| Scan | Pattern | Expected Count | Present | Status |
|------|---------|---------------|---------|--------|
| SCAN-01 | `lock(` | 0 | ✅ | ✅ |
| SCAN-02 | Non-ASCII characters | 0 | ✅ | ✅ |
| SCAN-03 | `FontFamily` | 0 | ✅ | ✅ |
| SCAN-04 | `#[0-9A-Fa-f]{6}` | 0 | ✅ | ✅ |
| SCAN-05 | `CreateOrder` without `PTT-` prefix | 0 | ✅ | ✅ |
| SCAN-06 | `DateTime.Now` | 0 | ✅ | ✅ |
| SCAN-07 | `sealed class TradeCopierWindow` | 0 (N/A for CopyEngine.cs) | ✅ | ✅ |

---

### VERDICT T1: TICKET_REVIEW_PASS

---

---

### T2 — UI: Button Color Coding + ScrollViewer (P2)

#### Traceability: PASS

All ticket items map to plan or spec. No phantom work detected. No missing plan items.

| Ticket Item | Plan / Spec Citation | Status |
|-------------|----------------------|--------|
| Copy ON = green, Copy OFF = grey (Layer 2) | PTT_DESIGN_PILLAR Layer 2; spec line 715 | ✅ |
| Flatten/Cancel = red only when position/entries live | spec lines 716-717; PTT_DESIGN_PILLAR Layer 3 | ✅ |
| Trim = amber only when position live | spec lines 716-717; PTT_DESIGN_PILLAR Layer 3 | ✅ |
| BE = green only when position live | spec line 716; PTT_DESIGN_PILLAR Layer 3 | ✅ |
| Grey when no target state exists (all action buttons) | spec line 716 ("A grey button is information") | ✅ |
| `PositionStateChanged` event drives live transitions | spec line 717; plan V04, Section 2 | ✅ |
| Both Panel + Window subscribe/unsubscribe | spec line 717 ("All surfaces subscribe"); plan V04 | ✅ |
| Canonical RGB values per PTT_DESIGN_PILLAR | PTT_DESIGN_PILLAR lines 192-198; plan V08, Section 2 | ✅ |
| BrushDanger corrected from (185,28,28) to (239,68,68) | plan V08; spec CSS variable `--red: #ef4444` | ✅ |
| BrushCaution corrected from (217,119,6) to (245,158,11) | plan V08; spec CSS variable `--amber: #f59e0b` | ✅ |
| BrushInactive corrected from (75,85,99) to (55,65,81) | plan V08; spec CSS variable `--dim: #4b5563` | ✅ |
| `ScrollViewer` wrapping `_rulesPanel` (MaxHeight=400) | spec line 1409 (Window rule rows); plan Section 3 (UNCHANGED from REVIEW_PASS) | ✅ |
| `DockPanel.SetDock` on ScrollViewer wrapper | plan Section 3 architectural constraint | ✅ |
| `MakeWinBrush` static helper in `TradeCopierWindow.cs` | plan Section 2 TradeCopierWindow.cs | ✅ |
| Per-rule button tracking lists (`_flattenBtns`, etc.) | plan Section 2 + Engineer Note #3 | ✅ |
| `BuildRuleRow()` + `BuildDynamicRuleRow()` button list appends | plan Section 2 Engineer Note #3 | ✅ |
| `UpdateButtonColors(false, false)` at end of `BuildUI()` | plan V04, Section 2; spec line 716 (Layer 3 initial state) | ✅ |
| `OnToggle()` / `OnGlobalToggle()` / `OnRuleToggle()` brush update | plan Section 2 toggle modifications | ✅ |
| `OnWindowClosed` unsubscribe pattern | plan Section 2 Subscribe/Unsubscribe wiring | ✅ |
| No NTButtonStyle on color-coded buttons | plan Section 2 NTButtonStyle note | ✅ |
| T1 compile dependency explicitly stated | ticket T2 Dependency section; plan Section 5 T2 | ✅ |
| `TradeCopierAddOn.cs` UNCHANGED | plan Section 6 File Change Summary; ticket Summary table | ✅ |

No plan items missing from ticket. No ticket items absent from plan/spec.

---

#### JS Pre-Check: PASS

| Check | Ticket Description | Rule | Status |
|-------|--------------------|------|--------|
| `lock()` usage | Not described. All button updates on UI thread only. `List<Button>` tracking lists accessed exclusively on UI thread (JS-021 note in ticket confirmed). | JS-021 (P0) | ✅ |
| `throw` in any method | No exception throwing described in any T2 method. | JS-001 (P0) | ✅ |
| `null` return | No method returns null. `OnPositionStateChanged` uses early return. `UpdateButtonColors` is void. | JS-002 (P0) | ✅ |
| `Dictionary<K,V>` for shared state | `_flattenBtns`, `_cancelBtns`, `_trimBtns`, `_beBtns` are `List<Button>` — UI-thread-only data, not shared state. No plain `Dictionary<K,V>` on any cross-thread field. | JS-009 (P2) | ✅ |
| `SolidColorBrush` without `Freeze()` | All brushes via `MakeBrush(r,g,b)` (Panel, existing helper that calls `Freeze()`) and `MakeWinBrush(byte r, byte g, byte b)` (Window, new helper that calls `brush.Freeze()`). `static readonly` fields = single allocation. | JS-008 (P1) | ✅ |
| Off-thread UI update without `Dispatcher.InvokeAsync` | Both `OnPositionStateChanged` implementations on Panel and Window call `Dispatcher.InvokeAsync(() => UpdateButtonColors(...))`. Event fires from NT8 background order-update thread. | JS-023 (P1) | ✅ |
| `DateTime.Now` | Not described. SCAN-06 expected count = 0. | NT8 hard constraint | ✅ |
| Hardcoded hex color `#RRGGBB` | All brush values use integer RGB via `MakeBrush(r,g,b)` / `MakeWinBrush(r,g,b)`. Hex annotations in comments (e.g., `// green #22c55e`) are documentation only — not string literals. SCAN-04 expected count = 0 on all code paths. | SCAN-04 | ✅ |
| `FontFamily` override | Not described. SCAN-03 expected count = 0. | SCAN-03 / NT8 | ✅ |
| `CreateOrder` without `PTT-` prefix | No `CreateOrder` calls in UI files. SCAN-05 expected count = 0. | SCAN-05 | ✅ |

---

#### CYC Pre-Check: PASS

| Method | File | Ticket CYC | Plan CYC | Expected | Status |
|--------|------|-----------|---------|---------|--------|
| `UpdateButtonColors` | TradeCopierPanel.cs | 5 | 5 | ≤ 8 | ✅ |
| `OnPositionStateChanged` | TradeCopierPanel.cs | 1 | 1 | ≤ 8 | ✅ |
| `MakeWinBrush` | TradeCopierWindow.cs | 1 | 1 | ≤ 8 | ✅ |
| `UpdateButtonColors` | TradeCopierWindow.cs | 5 | 5 | ≤ 8 | ✅ |
| `OnPositionStateChanged` | TradeCopierWindow.cs | 1 | 1 | ≤ 8 | ✅ |
| `OnWindowClosed` | TradeCopierWindow.cs | 1 | 1 | ≤ 8 | ✅ |

All T2 methods ≤ 8. No at-risk methods. ✅

---

#### NT8 Constraint Check: PASS

| Constraint | Ticket Description | Status |
|------------|--------------------|--------|
| No `async/await` in lifecycle methods | `OnLoaded`, `OnWindowClosed` are synchronous. No async/await described in any T2 method. | ✅ |
| Off-thread UI update without `Dispatcher.InvokeAsync` | `OnPositionStateChanged` on both Panel and Window marshals via `Dispatcher.InvokeAsync`. `UpdateButtonColors` always called inside this marshal. Never called directly from event thread. | ✅ |
| `Account.All` outside Loaded handler | Not accessed in T2. | ✅ |
| `TradeCopierWindow` changed from sealed | Explicitly preserved. SCAN-07 verifies `sealed class TradeCopierWindow` unchanged. Ticket manual checklist: "Window title bar / base class unchanged (sealed class TradeCopierWindow still sealed)". | ✅ |
| Brush thread safety | All brushes `Freeze()`d via `MakeBrush`/`MakeWinBrush`. Safe to capture in `Dispatcher.InvokeAsync` lambda closures. | ✅ |

---

#### Completeness: PASS

| Item | Status |
|------|--------|
| All 2 T2 in-scope files addressed (`TradeCopierPanel.cs`, `TradeCopierWindow.cs`) | ✅ |
| `TradeCopierAddOn.cs` explicitly marked UNCHANGED | ✅ (plan summary table; ticket Summary table) |
| T2 compile dependency on T1 explicitly stated | ✅ (T2 Dependency section; Engineer Note #2) |

---

#### Test Coverage: PASS

Rule scope: "Missing [Fact] for any **public or internal** method = FAIL." All 6 new T2 methods are `private`:
- `UpdateButtonColors(bool, bool)` — private (Panel)
- `OnPositionStateChanged(string, PositionState)` — private (Panel)
- `MakeWinBrush(byte, byte, byte)` — private static (Window)
- `UpdateButtonColors(bool, bool)` — private (Window)
- `OnPositionStateChanged(string, PositionState)` — private (Window)
- `OnWindowClosed(object, EventArgs)` — private (Window)

No public or internal methods added in T2. Manual NT8 F5 is the correct acceptance gate for pure WPF UI methods per plan Section 2 Test Plan ("no engine logic changes — no xUnit tests required for B7-F1"). The 7-item manual verification checklist is present and complete.

---

#### Scan Checklist: PASS

All 7 scans present in T2 ticket with expected count = 0 (SCAN-07: "0 changes" — class declaration verified unchanged).

| Scan | Pattern | Expected Count | Present | Status |
|------|---------|---------------|---------|--------|
| SCAN-01 | `lock(` | 0 | ✅ | ✅ |
| SCAN-02 | Non-ASCII characters | 0 | ✅ | ✅ |
| SCAN-03 | `FontFamily` | 0 | ✅ | ✅ |
| SCAN-04 | `#[0-9A-Fa-f]{6}` | 0 | ✅ | ✅ |
| SCAN-05 | `CreateOrder` without `PTT-` prefix | 0 | ✅ | ✅ |
| SCAN-06 | `DateTime.Now` | 0 | ✅ | ✅ |
| SCAN-07 | `sealed class TradeCopierWindow` | 0 changes (present, verify not removed) | ✅ | ✅ |

---

### VERDICT T2: TICKET_REVIEW_PASS

---

---

## Summary

| Ticket | Traceability | JS Pre-Check | CYC Pre-Check | NT8 Check | Completeness | Test Coverage | Scan Checklist | VERDICT |
|--------|-------------|-------------|--------------|----------|-------------|--------------|---------------|---------|
| T1 — CopyEngine + Tests | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **TICKET_REVIEW_PASS** |
| T2 — UI Panel + Window | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **TICKET_REVIEW_PASS** |

### Notable Observations (non-blocking)

1. **`PopulateOrderMap` CYC delta** (T1): Plan body shows CYC=1; ticket correctly uses CYC=2 reflecting the mandatory dedup guard added via Engineer Note #1. Both are plan-consistent. No action required.

2. **`FindFollowerBracketOrder` returns `Order?`** (T1): JS-002 strictly mandates `Option<T>` over null, but the plan reviewer already accepted `Order?` (C# 8+ nullable reference type) as the NT8-compatible equivalent. The plan explicitly documents this as V03 compliance. Null contract is explicit and compile-time verifiable. No action required.

3. **`List<Button>` tracking fields in `TradeCopierWindow`** (T2): These are `List<Button>` rather than concurrent collections. This is correct — they are exclusively accessed on the UI thread (construction in `BuildRuleRow`, read in `UpdateButtonColors` via `Dispatcher.InvokeAsync`). No cross-thread access described. No action required.

4. **Hex strings in comments** (T2): Brush fields include comments like `// green #22c55e`. These are code comments, not string literals. SCAN-04 pattern targets string literals, not comments. No action required.

---

## Overall: TICKET_REVIEW_PASS

Both tickets pass all mandatory checks. No violations detected against RULES_CATALOG.md, the architecture plan, spec requirements, or NT8 constraints. Engineer may proceed with T1 execution.
