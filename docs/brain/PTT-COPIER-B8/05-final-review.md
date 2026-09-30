# PTT-COPIER-B8 Final Review
**Status**: FINAL_PASS
**Reviewer**: PTT Plan Reviewer (Phase 6)
**Date**: 2026-07-08
**Baseline**: B7 FINAL_PASS -- 5 files, 27 [Fact] tests, all 7 scans green
**B8 Target**: DW-B7-01 (multiplier) + DW-B7-03 (ATM mode wiring), 13 new tests, all 7 scans green

---

## Section A: System Coherence

**Verdict: PASS**

The four source files form a complete, coherent system with no missing wiring:

| Component | Role | Wiring Status |
|-----------|------|---------------|
| `CopyEngine.cs` | Pure logic singleton | Engine exposes `AddRule` (3-arg + 5-arg), `SetFollowerMultiplier`, `SetAtmMode`, `SendCopy` (with mode dispatch), `GetMultiplier`, `GetAtmMode`, `ParseAtmModeName`, `AtmModeToString`, `RuleToDto`, `DtoToRule` |
| `TradeCopierPanel.cs` | ChartTrader surface | Calls `_engine.AddRule(instrument, leader, followers, multipliers, atmMap)` via 5-arg overload in `OnApplyRule` (line 517). Per-follower `Multiplier` TextBox + ATM `ComboBox` collect per-row state. `ParseAtmModeNameLocal` is a self-contained mirror helper. |
| `TradeCopierWindow.cs` | Standalone window | Calls `_engine.AddRule(name, leader, followers.ToArray(), multipliers, atmMap)` via 5-arg overload in `OnRowApply` (line 534). ATM ComboBox present in both `BuildRuleRow` (static, line 302) and `BuildDynamicRuleRow` (dynamic, line 410). `tag[3] = atmCb` confirmed at line 312 (static) and line 415 (dynamic). Guard `tag.Length > 3` at line 523 now fires for both rows. |
| `CopyEngineTests.cs` | xUnit test suite | 40 [Fact] tests: 27 existing + 13 new. No missing coverage for any new B8 public/internal API. |

**Wiring completeness check:**

| Wiring Point | Present? | Source File:Line |
|---|---|---|
| Panel calls 5-arg `AddRule` | YES | `TradeCopierPanel.cs:517` |
| Window calls 5-arg `AddRule` | YES | `TradeCopierWindow.cs:534` |
| Window static row `atmCb` in tag | YES | `TradeCopierWindow.cs:312` (DEFECT-T2-001 RESOLVED in Cycle 2) |
| Window dynamic row `atmCbDyn` in tag | YES | `TradeCopierWindow.cs:415` |
| `OnRowApply` reads `tag[3]` | YES | `TradeCopierWindow.cs:523` |
| `DispatchCopy` applies `GetMultiplier` per follower | YES | `CopyEngine.cs:333` |
| `DispatchCopy` calls `GetAtmMode` per follower | YES | `CopyEngine.cs:340` |
| `SendCopy` dispatches on `FollowerAtmMode` | YES | `CopyEngine.cs:518-527` |
| `RuleToDto` serializes `FollowerMultipliers` + `FollowerAtmModeNames` | YES | `CopyEngine.cs:895-916` |
| `DtoToRule` deserializes both null-safely | YES | `CopyEngine.cs:945-962` |
| 3-arg `AddRule` overload PRESERVED | YES | `CopyEngine.cs:190-193` |
| `CopyRule.Create` backward compat (optional params) | YES | `CopyEngine.cs:103-111` |

No missing wiring found.

---

## Section B: Deferred Item Status

| ID | Item | B8 Decision | Status |
|----|------|-------------|--------|
| DW-B7-01 | Per-account qty multiplier | IN SCOPE | **CLOSED (B8)** -- `CopyRule.FollowerMultipliers`, `GetMultiplier`, `DispatchCopy` loop, Panel TextBox, `RuleToDto`/`DtoToRule`, 5 new tests (T-B8-01..04, T-B8-12) all confirmed present in source files. |
| DW-B7-02 | ATR dynamic sizing engine | DEFERRED B9 | **OPEN** -- `MarketData.Subscribe`/`AddOnBase` incompatibility documented in plan §1. Not touched in B8. Target: B9. |
| DW-B7-03 | FollowerAtmMode behavioral wiring | IN SCOPE | **CLOSED (B8)** -- `FollowerAtmMode` sealed hierarchy, `SendCopy` mode dispatch, `GetAtmMode`, Panel ATM ComboBox, Window ATM ComboBox (both static and dynamic rows), `ParseAtmModeName`, `AtmModeToString`, persistence, 5 new tests (T-B8-05..07, T-B8-11, T-B8-13) all confirmed present. |
| SPEC-B8-04 | Click trader (chart-click entry) | DEFERRED B9 | **OPEN** -- ChartControl.MouseDown + armed state requires NT8 chart overlay. Correctly excluded from B8. Target: B9. |
| SPEC-B8-05 | ATR box visualization | DEFERRED B9 | **OPEN** -- Depends on DW-B7-02 ATR engine. Target: B9. |
| SPEC-B8-06 | Full mirror mode / Mode 2 | DEFERRED B9 | **OPEN** -- `OnOrderUpdate` relay for modifications requires significant engine extension. Target: B9. |

**B7 backlog items DW-B7-01 and DW-B7-03 are fully closed. DW-B7-02 carries forward.**

Additionally, two new B8 advisory deferred items (from B7/06-deferred-backlog.md line 96-98):

| ID | Item | Status |
|----|------|--------|
| DW-B8-01 | JS-002 `return null` cleanup in CopyEngine.cs query helpers | OPEN -- B9 |
| DW-B8-02 | Gate hook path fix for PropTraderTools repo detection | OPEN -- B9 |

---

## Section C: Cross-File JS Violations

**Independent scans run against `c:/WSGTA/universal-or-strategy/src/PropTraderTools/*.cs`.**

| Scan | Pattern | Command Result | Verdict |
|------|---------|----------------|---------|
| SCAN-01 | `lock\s*\(` in executable code | 2 matches -- `CopyEngine.cs:208` and `:589` -- BOTH in **comments only** (`// ConcurrentBag rebuild pattern -- no lock (JS-021)`) | **PASS -- ZERO violations** |
| SCAN-02 | `throw new` in `SendCopy`/`DispatchCopy` dispatch path | ZERO matches in all `.cs` files | **PASS -- ZERO** |
| SCAN-03 | `return null` in new B8 methods | Zero new `return null` in B8 code paths. Pre-existing: `FindFollowerBracketOrder` (Order? nullable, pre-B8), `FindPosition` (Position, pre-B8), `FindInstrument` in Window (platform boundary, JS-015 exempt). No new B8 code returns null. | **PASS** |
| SCAN-04 | `new Dictionary<` mutable | ZERO matches. Only `ConcurrentDictionary` (pre-existing) and `ImmutableDictionary` (B8 new) used. | **PASS -- ZERO** |
| SCAN-05 | `DateTime.Now` (non-UTC) | ZERO matches. `DateTime.MaxValue` used in `CreateOrder` (order expiry argument, pre-existing); `DateTime.UtcNow` used in log lines. | **PASS -- ZERO** |
| SCAN-06 | `async void` | ZERO matches. All B8 event handlers are synchronous `void`. | **PASS -- ZERO** |
| SCAN-07 | `#[0-9A-Fa-f]{6}` hex color literals | 8 matches -- ALL in **comments only** (color annotation comments on `MakeBrush`/`MakeWinBrush` lines, e.g. `// green #22c55e`). No hex in executable string literals. | **PASS -- ZERO executable violations** |

**Additional B8 specific checks:**

| Check | Result |
|-------|--------|
| `CreateOrder` signal name always "PTT-Copy" | PASS -- `CopyEngine.cs:516` hardcodes `string signalName = "PTT-Copy";`; never overwritten for Inherit, Market, or Named modes. Named ATM template passed as 12th `atmTemplate` parameter at line 543. |
| `FollowerAtmMode` sealed hierarchy with private base constructor | PASS -- `CopyEngine.cs:36`: `private FollowerAtmMode() { }` (JS-010). Three sealed record subtypes: `Inherit`, `Market`, `Named`. |
| `ImmutableDictionary` used for ATM template map (not `Dictionary<`) | PASS -- `CopyRule.FollowerAtmTemplates` is `ImmutableDictionary<string, FollowerAtmMode>` (JS-009). |
| `SolidColorBrush.Freeze()` called | PASS -- Both `MakeBrush(r,g,b)` in Panel and `MakeWinBrush(r,g,b)` in Window call `brush.Freeze()` before returning (JS-008). |
| Non-ASCII in executable code | PASS -- One `§` character found at `CopyEngine.cs:866` in a **comment** only, not a string literal. AGENTS.md §2 prohibits Unicode in C# string literals -- this is compliant. |
| `TradeCopierWindow` not sealed | PASS -- `TradeCopierWindow.cs:20`: `public class TradeCopierWindow : Window` (no `sealed` modifier). |
| `FontFamily` override | PASS -- ZERO matches. |

**No JS rule violations found in B8 code.**

---

## Section D: Spec Requirements

**DW-B7-01 (Per-account qty multiplier) -- End-to-End Check:**

| Requirement | Status | Evidence |
|-------------|--------|---------|
| Data model: `CopyRule.FollowerMultipliers` parallel array to `FollowerAccounts` | PASS | `CopyEngine.cs:79` -- `internal readonly int[] FollowerMultipliers` |
| Factory: `CopyRule.Create` accepts optional `multipliers` | PASS | `CopyEngine.cs:103-111` -- optional param, default null |
| Engine mutation: `SetFollowerMultiplier` clamps to `[1,10]` | PASS | `CopyEngine.cs:210-227` -- ConcurrentBag rebuild, `Math.Max(1, Math.Min(10, multiplier))` |
| Hot path: `GetMultiplier` applied per follower in `DispatchCopy` | PASS | `CopyEngine.cs:333` -- `int mult = GetMultiplier(rule, idx)` |
| Hot path: `CopySignal.Quantity * mult` applied before `SendCopy` | PASS | `CopyEngine.cs:336` |
| UI TextBox in Panel per-follower row | PASS | `TradeCopierPanel.cs:330-340` -- Width=30, wired to `OnFollowerMultiplierChanged` |
| Panel `OnApplyRule` collects and passes multipliers | PASS | `TradeCopierPanel.cs:496-517` |
| Persistence: `RuleToDto` serializes `FollowerMultipliers` | PASS | `CopyEngine.cs:895-898` |
| Persistence: `DtoToRule` deserializes null-safely (B6/B7 backward compat) | PASS | `CopyEngine.cs:946-948` |
| Test coverage | PASS | T-B8-01..04 + T-B8-08 + T-B8-12 (6 tests) |

**DW-B7-03 (FollowerAtmMode behavioral wiring) -- End-to-End Check:**

| Requirement | Status | Evidence |
|-------------|--------|---------|
| Sealed hierarchy: `Inherit / Market / Named(string)` | PASS | `CopyEngine.cs:37-39` |
| Private base constructor (JS-010) | PASS | `CopyEngine.cs:36` |
| `SendCopy` dispatch: Inherit=pass-through, Market=force market, Named=atmTemplate param | PASS | `CopyEngine.cs:518-543` |
| `signalName` always "PTT-Copy" (no PTT- prefix violation) | PASS | `CopyEngine.cs:516` -- never overwritten |
| ATM template for Named mode via `atmTemplate` param (12th arg of `CreateOrder`) | PASS | `CopyEngine.cs:543` |
| `GetAtmMode` returns `Inherit` as default (never null) | PASS | `CopyEngine.cs:561` |
| `ParseAtmModeName` handles null/empty/"Market"/"Named:XXX" | PASS | `CopyEngine.cs:566-575` |
| `AtmModeToString` handles all three variants | PASS | `CopyEngine.cs:579-586` |
| `SetAtmMode` ConcurrentBag rebuild, no lock | PASS | `CopyEngine.cs:591-606` |
| Per-follower ATM ComboBox in Panel | PASS | `TradeCopierPanel.cs:358-364` |
| Panel `OnApplyRule` builds `ImmutableDictionary` and calls 5-arg `AddRule` | PASS | `TradeCopierPanel.cs:510-517` |
| `ParseAtmModeNameLocal` self-contained helper on Panel | PASS | `TradeCopierPanel.cs:524-533` |
| Per-rule ATM ComboBox in Window (static row) | PASS | `TradeCopierWindow.cs:302-307` |
| Per-rule ATM ComboBox in Window (dynamic row) | PASS | `TradeCopierWindow.cs:410-436` |
| Static row `atmCb` included in `applyBtn.Tag` as `tag[3]` | PASS | `TradeCopierWindow.cs:312` (DEFECT-T2-001 RESOLVED) |
| `OnRowApply` reads `tag[3]`, builds `atmMap`, calls 5-arg `AddRule` | PASS | `TradeCopierWindow.cs:523-534` |
| Persistence: `RuleToDto` serializes `FollowerAtmModeNames` via `AtmModeToString` | PASS | `CopyEngine.cs:901-905` |
| Persistence: `DtoToRule` parses `FollowerAtmModeNames` via `ParseAtmModeName` null-safely | PASS | `CopyEngine.cs:952-960` |
| Backward compat: B6/B7 XML with no `FollowerAtmModeNames` defaults to Inherit | PASS | `CopyEngine.cs:952` -- null check before loop |
| Test coverage | PASS | T-B8-05..07 + T-B8-09 + T-B8-11 + T-B8-13 (6 tests) |

All in-scope spec requirements are satisfied end-to-end.

---

## Section E: Test Coverage

**[Fact] count: 40 (target >= 40)**

| Source | Count | Verified By |
|--------|-------|-------------|
| B7 baseline (existing) | 27 | T3-verification.md Check 3: all 27 at original lines, unchanged |
| B8 new tests (T-B8-01..T-B8-13) | 13 | T3-verification.md Check 1: independent scan returns exactly 40 |
| **Total** | **40** | Independent grep: 40 `[Fact]` attributes confirmed |

**New test coverage by method:**

| New B8 Method | [Fact] Tests | Count |
|---|---|---|
| `AddRule` 5-arg overload | T-B8-01 | 1 |
| `GetMultiplier` (bounds, happy path, null) | T-B8-02, T-B8-03, T-B8-04 | 3 |
| `SetFollowerMultiplier` | T-B8-12 | 1 |
| `FollowerAtmMode` constructors | T-B8-05 | 1 |
| `GetAtmMode` (default, named) | T-B8-06, T-B8-07 | 2 |
| `ParseAtmModeName` | T-B8-11 | 1 |
| `SetAtmMode` | T-B8-13 | 1 |
| Persistence round-trips | T-B8-08, T-B8-09, T-B8-10 | 3 |

Every new B8 internal/public API has at least one `[Fact]` test. `SendCopy` and WPF event handlers are NT8/WPF-bound and not unit-testable without a live NT8 context -- accepted per ticket review.

---

## Section F: 7-Scan Summary

All scans performed independently against `c:/WSGTA/universal-or-strategy/src/PropTraderTools/*.cs`.

| Scan ID | Pattern | Scope | Result |
|---------|---------|-------|--------|
| SCAN-01 | `lock\s*\(` | `src/PropTraderTools` | **ZERO** (2 comment-only hits) |
| SCAN-02 | `throw new` in hot path dispatch | `DispatchCopy`, `SendCopy` | **ZERO** |
| SCAN-03 | `return null` in new B8 methods | New B8 code only | **ZERO** (pre-existing nullable returns are B6/B7 boundary patterns) |
| SCAN-04 | `new Dictionary<` mutable | `src/PropTraderTools` | **ZERO** |
| SCAN-05 | `DateTime\.Now[^U]` | `src/PropTraderTools` | **ZERO** |
| SCAN-06 | `async void` | `src/PropTraderTools` | **ZERO** |
| SCAN-07 | `#[0-9A-Fa-f]{6}` in string literals | `src/PropTraderTools` | **ZERO** (8 comment-only hits) |

**All 7 scans: ZERO violations.**

---

## Section G: NT8 Constraint Summary

| Constraint | Status | Evidence |
|-----------|--------|---------|
| No `async/await` in `OnInitialize`/`OnDestroyed`/`OnWindowCreated` | PASS | No new async methods introduced in B8 |
| `Account.All` only in `Loaded` handlers / NT main thread | PASS | `DtoToRule` called from `LoadRules()` from `OnLoaded` (pre-existing B6 pattern); `BuildDynamicRuleRow` binds `Account.All` from `OnAddRule` on WPF UI thread |
| `TradeCopierWindow` NOT `sealed` | PASS | `TradeCopierWindow.cs:20` -- `public class TradeCopierWindow : Window` |
| Off-thread UI updates via `Dispatcher.InvokeAsync` | PASS | `TradeCopierPanel.cs:537` and `TradeCopierWindow.cs:539` -- both `OnStatusUpdate` handlers use `Dispatcher.InvokeAsync` |
| `CreateOrder` signal name starts "PTT-" | PASS | `CopyEngine.cs:516` -- `string signalName = "PTT-Copy";` hardcoded for ALL modes |
| `FontFamily` override | PASS | ZERO matches |
| No `MarketData.Subscribe` | PASS | DW-B7-02 (ATR engine) deferred to B9; no `MarketData` access in B8 |
| `SolidColorBrush.Freeze()` | PASS | All brushes created via `MakeBrush(r,g,b)` / `MakeWinBrush(r,g,b)` which call `Freeze()` before returning |

---

## Section H: Known Non-Blocking Issues (Advisory)

| ID | Severity | Description | Location |
|----|----------|-------------|----------|
| DEFECT-T2-002 | ADVISORY | `ParseAtmModeNameWindow` private helper absent in `TradeCopierWindow`; the file calls `CopyEngine.ParseAtmModeName` directly (line 525). Functionally equivalent; breaks the file-isolation principle stated in T2 ticket. No runtime impact. | `TradeCopierWindow.cs:525` |
| DEFECT-T2-003 | MINOR | `§` (U+00A7, section sign) in comment at `CopyEngine.cs:866`. Not in executable code; not a C# string literal. No DNA violation. | `CopyEngine.cs:866` |
| Comment drift | MINOR | `OnRowApply` comment at line 521 reads "static rows use 3-element tag" -- stale after Cycle 2 fix changed static row tag to 4-element. No functional impact. | `TradeCopierWindow.cs:521` |
| DW-B8-01 | P2 | Pre-existing `return null` in `FindRule`, `FindPosition`, `FindLimitEntry` (B6/B7 code paths). Not new violations; deferred to B9 for `Option<T>` cleanup. | `CopyEngine.cs:806, 671, 677` |
| DW-B8-02 | P2 | Rules-gate hook (`pre_task_rules_gate.py`) scans V12 repo `src/` which contains 93 pre-existing violations unrelated to trade copier work. Hook needs path configuration for PropTraderTools-only scans. | `.bob/hooks/pre_task_rules_gate.py` |

---

## Section K: Deferred Work / Block Backlog

**REQUIRED FORMAT** -- All open items from B7 updated; new B8 items added.

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B7-01 | Per-account qty multiplier (1x/2x/3x) -- CopyRule DTO + serialization + UI TextBox | P2 | B8 | **CLOSED (B8)** |
| DW-B7-02 | ATR dynamic sizing engine (AtrSizingEngine.cs, MarketData subscription, rolling ATR) | P1 | B9 | **OPEN** |
| DW-B7-03 | FollowerAtmMode behavioral wiring -- SendCopy dispatch + Window/Panel dropdowns | P2 | B8 | **CLOSED (B8)** |
| DW-B8-01 | JS-002 cleanup: replace `return null` with `Option<T>` in CopyEngine.cs query helpers (FindRule:671/677, FindPosition:802, FindFollowerBracketOrder:437) | P2 | B9 | **OPEN** |
| DW-B8-02 | Gate hook path: `pre_task_rules_gate.py` scans universal-or-strategy-director/src/ -- update to detect PropTraderTools repo when running in trade copier context | P2 | B9 | **OPEN** |
| DW-B8-03 | ATR dynamic sizing engine (carry from DW-B7-02) -- design AtrSizingEngine.cs as detached Indicator managed by AddOn | P1 | B9 | **OPEN** |
| DW-B8-04 | Click trader / chart-click entry -- ChartControl.MouseDown + armed state overlay (SPEC-B8-04) | P1 | B9 | **OPEN** |
| DW-B8-05 | ATR box visualization on chart -- depends on DW-B7-02 AtrSizingEngine (SPEC-B8-05) | P2 | B9 | **OPEN** |
| DW-B8-06 | Full mirror mode / Mode 2 -- OnOrderUpdate relay for modifications (SPEC-B8-06) | P2 | B9 | **OPEN** |

---

## Overall Verdict

| Section | Result |
|---------|--------|
| A. System Coherence | PASS |
| B. Deferred Item Status | PASS (DW-B7-01 CLOSED, DW-B7-03 CLOSED, DW-B7-02 OPEN->B9) |
| C. Cross-File JS Violations | PASS (0 violations) |
| D. Spec Requirements | PASS (all in-scope requirements satisfied end-to-end) |
| E. Test Coverage | PASS (40 [Fact] tests confirmed) |
| F. 7-Scan Summary | PASS (all 7 scans zero) |
| G. NT8 Constraints | PASS |
| H. Non-Blocking Issues | 5 advisory items (none blocking) |
| K. Deferred Backlog | PRESENT (required for FINAL_PASS) |

**FINAL_PASS**
