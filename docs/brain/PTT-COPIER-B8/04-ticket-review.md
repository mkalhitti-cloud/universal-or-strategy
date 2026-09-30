# Ticket Review: PTT-COPIER-B8
**Status**: TICKET_REVIEW_FAIL  
**Reviewer**: PTT Ticket Reviewer (Phase 4.5)  
**Date**: 2026-07-08  
**Plan**: `docs/brain/PTT-COPIER-B8/02-architecture-plan.md` — REVIEW_PASS (Cycle 2)  
**Tickets**: `docs/brain/PTT-COPIER-B8/04-tickets.md`  
**Rules Catalog**: `docs/standards/jane-street/RULES_CATALOG.md`  
**Baseline**: B7 FINAL_PASS — 27 passing [Fact] tests confirmed (lines 23–463 of `CopyEngineTests.cs`)

---

## T1 — Per-Account Qty Multiplier (DW-B7-01)

### Traceability: PASS
All items in T1 map to plan items and spec requirements:
- `DW-B7-01` → `docs/brain/PTT-COPIER-B7/06-deferred-backlog.md` ✅
- `SPEC line 2319` → `specs/002-trade-copier-spec.html` ✅
- All method signatures (§C) map to plan §3.1 (CopyEngine) and §3.2 (TradeCopierPanel) ✅
- Deferred items (DW-B7-02, DW-B7-03, SPEC-B8-04/05/06) correctly excluded ✅

No phantom work. No missing plan coverage in T1 scope.

### JS Pre-Check: PASS
| Check | Method | Result |
|-------|--------|--------|
| JS-021 No lock() | `SetFollowerMultiplier`, `AddRule` (new overload) | PASS — ConcurrentBag rebuild; no `lock(` |
| JS-001 No throw in hot path | `GetMultiplier`, `DispatchCopy` | PASS — `GetMultiplier` returns int for all paths; no throw |
| JS-002 No return null | `GetMultiplier` | PASS — returns `int` (value type; null impossible) |
| JS-008 No mutable struct fields | `CopyRule.FollowerMultipliers` | PASS — `readonly int[]` on `private readonly struct` |
| JS-009 No mutable Dictionary | `CopyRuleDto.FollowerMultipliers` | PASS — `int[]`, not `Dictionary<>` |
| DateTime.Now | No T1 method | PASS — not introduced |
| Hardcoded hex | No T1 method | PASS — not introduced |
| FontFamily override | No T1 method | PASS — not applicable |
| CreateOrder PTT- prefix | Not touched in T1 | PASS — `SendCopy` unmodified in T1 |

### CYC Pre-Check: PASS
| Method | Estimated CYC | Status |
|--------|--------------|--------|
| `GetMultiplier` | 3 (null guard + bounds + clamp) | PASS |
| `DispatchCopy` (modified) | 8 (stated at limit in ticket §C #10 and plan §3.1) | PASS — at limit, not over |
| `OnFollowerMultiplierChanged` | ≤3 (parse + clamp + set) | PASS |
| `RuleToDto` | Low (loop + ternary) | PASS |
| `DtoToRule` | Low (null guard + loop) | PASS |

### NT8 Check: PASS
| Constraint | T1 Status |
|-----------|-----------|
| No async/await in lifecycle | PASS — all T1 methods synchronous |
| Off-thread UI → Dispatcher.InvokeAsync | PASS — `OnFollowerMultiplierChanged` fires on WPF UI thread |
| Account.All outside Loaded | PASS — not accessed in any T1 method |
| TradeCopierWindow sealed | PASS — not touched in T1 |
| CreateOrder name "PTT-" prefix | PASS — SendCopy not modified in T1 |

### Test Coverage: **FAIL**
| New Method (T1 §C) | [Fact] Test | Status |
|--------------------|------------|--------|
| `AddRule` (new 5-arg overload) | T-B8-01 | PASS |
| `GetMultiplier` (out-of-range) | T-B8-02 | PASS |
| `GetMultiplier` (valid index) | T-B8-03 | PASS |
| `GetMultiplier` (null array) | T-B8-04 | PASS |
| **`SetFollowerMultiplier`** | **NONE** | **FAIL — no [Fact] specified in T1, T2, or T3** |
| `DispatchCopy` (modified) | Not unit-testable (requires live NT8 Account) | Acceptable |

**Violation**: `SetFollowerMultiplier` is a new internal API described in T1 §C (item #5) with no [Fact] test anywhere in T1 §F, T2 §F, or T3 §C/F.

### Scan Checklist: PASS
All 7 scans present in T1 §G with command, T1 impact, and ZERO expectation. ✅

### VERDICT: **TICKET_REVIEW_FAIL**
> Reason: Missing [Fact] for `SetFollowerMultiplier` (new internal API, T1 §C item #5).

---

## T2 — FollowerAtmMode Behavioral Wiring (DW-B7-03)

### Traceability: PASS
All items in T2 map to plan items and spec requirements:
- `DW-B7-03` → `docs/brain/PTT-COPIER-B7/06-deferred-backlog.md` ✅
- `SPEC lines 2331–2340` and `SPEC line 2335` ✅
- All method signatures (§C) map to plan §3.1 (CopyEngine), §3.2 (TradeCopierPanel), §3.3 (TradeCopierWindow) ✅
- Deferred items (DW-B8-02, DW-B8-03) correctly excluded ✅

### JS Pre-Check: PASS
| Check | Method | Result |
|-------|--------|--------|
| JS-021 No lock() | `SetAtmMode` | PASS — ConcurrentBag rebuild; no `lock(` |
| JS-001 No throw in hot path | `SendCopy` (dispatch), `GetAtmMode`, `ParseAtmModeName` | PASS — catch logs and returns false; no re-throw; helpers return Inherit default |
| JS-002 No return null | `GetAtmMode`, `ParseAtmModeName`, `AtmModeToString` | PASS — all return concrete types; `GetAtmMode` returns `new FollowerAtmMode.Inherit()` |
| JS-003 Sealed record hierarchy | `FollowerAtmMode` | PASS — `abstract record` with private constructor; three `sealed record` subtypes |
| JS-009 ImmutableDictionary | `CopyRule.FollowerAtmTemplates`, `OnApplyRule`, `OnRowApply` | PASS — `ImmutableDictionary<string, FollowerAtmMode>` used throughout |
| DateTime.Now | No T2 method | PASS — `DateTime.MaxValue` (order expiry, pre-existing) |
| Hardcoded hex | No T2 method | PASS — not introduced |
| FontFamily override | No T2 method | PASS — not applicable |

### CYC Pre-Check: PASS
| Method | Estimated CYC | Status |
|--------|--------------|--------|
| `GetAtmMode` | 2 (TryGetValue + fallback) | PASS |
| `ParseAtmModeName` | 3 (null/empty → Inherit; "Market"; "Named:") | PASS |
| `AtmModeToString` | 3 (is Inherit; is Market; is Named) | PASS |
| `SendCopy` (modified) | ≈5 (if + else if + try/catch) | PASS |
| `DispatchCopy` (modified) | 8 (at limit, per plan §3.1 and ticket) | PASS |
| `ParseAtmModeNameLocal` / `ParseAtmModeNameWindow` | 3 each | PASS |

### NT8 Check: **FAIL**
| Constraint | T2 Status |
|-----------|-----------|
| No async/await in lifecycle | PASS — all T2 methods synchronous |
| Off-thread UI → Dispatcher.InvokeAsync | PASS — WPF event handlers fire on UI thread |
| Account.All outside Loaded | PASS — not accessed in any T2 method |
| TradeCopierWindow sealed | PASS — ticket explicitly confirms "No sealed modifier added" (T2 §E) |
| **CreateOrder name must start "PTT-"** | **FAIL** |

**NT8 Violation**: T2 §C (SendCopy pseudocode, lines 240–269) explicitly describes `Account.CreateOrder` being called with `signalName = named.TemplateName` when `mode is FollowerAtmMode.Named`. The user-supplied ATM template name does **not** start with `"PTT-"`. The ticket review rule states: *"Ticket describes CreateOrder with name not starting 'PTT-' = FAIL"*. T2 §E acknowledges this: *"SendCopy uses 'PTT-Copy' for Inherit and Market modes; uses user-supplied template name for Named mode"*. The justification (NT8 ATM auto-attach) does not override the hard constraint.

**Citation**: NT8 Constraint Violations (hard) — "CreateOrder with name not starting 'PTT-' = FAIL"  
**Location in ticket**: T2 §C SendCopy pseudocode (line 243: `string signalName = "PTT-Copy";` then line 252: `signalName = named.TemplateName;`) and T2 §E NT8 Constraints row "CreateOrder signal name".

### Test Coverage: **FAIL**
| New Method (T2 §C) | [Fact] Test | Status |
|--------------------|------------|--------|
| `SetAtmMode` | **NONE** | **FAIL — no [Fact] specified in T1, T2, or T3** |
| `GetAtmMode` (no entry) | T-B8-06 | PASS |
| `GetAtmMode` (Named entry) | T-B8-07 | PASS |
| `ParseAtmModeName` | T-B8-11 | PASS |
| `AtmModeToString` | Indirectly via T-B8-09 (persistence round-trip) | Acceptable |
| `FollowerAtmMode` constructors | T-B8-05 | PASS |
| `SendCopy` (modified) | Not unit-testable (requires live NT8 Account) | Acceptable |
| `OnFollowerAtmComboLoaded` / `OnFollowerAtmModeChanged` / `OnRowApply` | WPF methods; not unit-testable | Acceptable |

**Violation**: `SetAtmMode` is a new internal API described in T2 §C (item #2) with no [Fact] test anywhere.

### Scan Checklist: PASS
All 7 scans present in T2 §G with command, T2 impact, and ZERO expectation. ✅

### VERDICT: **TICKET_REVIEW_FAIL**
> Reason 1 (NT8): `CreateOrder` called with `named.TemplateName` (no "PTT-" prefix) when Named mode active — NT8 hard constraint violated.  
> Reason 2 (Test Coverage): Missing [Fact] for `SetAtmMode` (new internal API, T2 §C item #2).

---

## T3 — Tests for B8 Features

### Traceability: PASS
All 11 [Fact] tests map to DW-B7-01, DW-B7-03, or persistence/backward-compat coverage:
- T-B8-01..04 → DW-B7-01 ✅
- T-B8-05..07, T-B8-11 → DW-B7-03 ✅
- T-B8-08..10 → Persistence + backward compat ✅
- T-B8-11 → Recommended gap closure from plan review ✅ (not phantom — fills a documented gap)

### JS Pre-Check: PASS
| Check | Result |
|-------|--------|
| xUnit only; no NUnit/MSTest | PASS — all `[Fact]`, `Xunit` namespace |
| No lock() in test code | PASS |
| No DateTime.Now | PASS — `DateTime.UtcNow.Ticks` only in pre-existing dedup tests (unchanged) |
| ImmutableDictionary used | PASS — `ImmutableDictionary<string, FollowerAtmMode>.Empty` throughout |
| `throw ex` at line 769 | PASS — test re-throw of caught `TargetInvocationException`; not a hot-path production method; `throw ex` is not `throw new XxxException(...)` |

### CYC Pre-Check: PASS
All 11 test methods are linear or have ≤3 branches. Highest: `DtoToRule_NullMultipliers_DoesNotThrow` (≈CYC 3). All within limit. ✅

### NT8 Check: PASS
- No NT8 lifecycle methods ✅
- All tests use `null` / `new Account[0]` — no live NT8 context required ✅
- Persistence tests use `Path.GetTempPath() + Guid.NewGuid()` — safe ✅

### Completeness: **FAIL**
Plan §3.4 lists 10 tests (T-B8-01..T-B8-10). T3 delivers 11 (T-B8-01..T-B8-11). The additional T-B8-11 closes a documented plan-review gap — **not** phantom work.

However, T3 carries forward two missing tests that were required by T1 and T2:
- **`SetFollowerMultiplier`** — no [Fact] in T3 (first identified in T1; not remedied here)
- **`SetAtmMode`** — no [Fact] in T3 (first identified in T2; not remedied here)

Both are new internal APIs with no test coverage in any ticket.

### Test Coverage: **FAIL**
Test bodies for all 11 declared [Fact] methods are present and complete in T3 §F. ✅ for declared tests.

Missing [Fact] (from T1 and T2 carry-forward):
- `SetFollowerMultiplier` — new internal method (T1 §C #5), zero tests
- `SetAtmMode` — new internal method (T2 §C #2), zero tests

### Scan Checklist: PASS
All 7 scans present in T3 §G. SCAN-02 is qualified as "ZERO (hot path methods)" with a note that `throw ex` in test code does not match `grep -r "throw new"`. This is technically accurate — `throw ex` is not matched by the `throw new` pattern. ✅

### VERDICT: **TICKET_REVIEW_FAIL**
> Reason (Test Coverage / Completeness): No [Fact] tests specified for `SetFollowerMultiplier` (carried from T1) or `SetAtmMode` (carried from T2). Both are new internal APIs described in the tickets with no test anywhere.

---

## Overall: **TICKET_REVIEW_FAIL**

### Violation Summary

| Ticket | Check | Rule | Violation |
|--------|-------|------|-----------|
| T1 | Test Coverage | Reviewer Rule | `SetFollowerMultiplier` (T1 §C #5) has no [Fact] test in T1 §F, T2 §F, or T3 §C/F |
| T2 | NT8 Check | NT8 Hard Constraint | `CreateOrder` called with `named.TemplateName` (no "PTT-" prefix) when `FollowerAtmMode.Named` active — T2 §C pseudocode line 252, T2 §E NT8 row "CreateOrder signal name" |
| T2 | Test Coverage | Reviewer Rule | `SetAtmMode` (T2 §C #2) has no [Fact] test in T1 §F, T2 §F, or T3 §C/F |
| T3 | Test Coverage / Completeness | Reviewer Rule | Inherits both missing tests from T1 and T2 — not remedied in T3 |

### Required Fixes Before Re-Review

**Fix 1 (T1 + T3)**: Add `[Fact] SetFollowerMultiplier_UpdatesMultiplier_RebuildsRules` to T3 §C and §F.  
The test should call `AddRule` with an initial multiplier, then call `SetFollowerMultiplier` to change it, then verify the rule in `_rules` bag has the updated value.

**Fix 2 (T2 + T3)**: Add `[Fact] SetAtmMode_UpdatesAtmTemplate_RebuildsRules` to T3 §C and §F.  
The test should call `AddRule` with an initial ATM map, then call `SetAtmMode` to override it, then verify the rule in `_rules` bag has the updated `FollowerAtmTemplates` entry.

**Fix 3 (T2)**: Resolve the `CreateOrder` PTT- prefix violation for `FollowerAtmMode.Named` mode.  
Options: (a) Architect must document an explicit exemption in the ticket citing the specific NT8 ATM auto-attach incompatibility with the "PTT-" prefix requirement and obtain a Director waiver; or (b) Signal name for Named mode uses `"PTT-" + named.TemplateName` if NT8 ATM auto-attach supports prefix-matching (to be validated against NT8 behavior).

---

## Cycle 2 Review — 2026-07-08

**Reviewer**: PTT Ticket Reviewer (Phase 4.5 — Cycle 2)  
**Input**: `docs/brain/PTT-COPIER-B8/04-tickets.md` Revision 1 (Status: TICKETS_COMPLETE)  
**Fixes Verified**: Fix 1 (T-B8-12), Fix 2 (PTT- prefix), Fix 3 (T-B8-13)

---

### Cycle 2 — Violation Closure Verification

| Cycle 1 Violation | Location | Fix Applied | Cycle 2 Result |
|-------------------|----------|-------------|----------------|
| Missing [Fact] for `SetFollowerMultiplier` | T1 §F / T3 §C/F | T-B8-12 added to T1 §F; `[Fact] SetFollowerMultiplier_UpdatesMultiplier_RebuildsRules` body in T3 §F (lines 823–854) | **CLOSED** ✅ |
| `CreateOrder` called with `named.TemplateName` (no PTT- prefix) when Named mode active | T2 §C / T2 §E | `signalName` is now `"PTT-Copy"` for **all** modes (Inherit, Market, Named); ATM template name routed via separate `atmTemplate` parameter in last position of `Account.CreateOrder`; T2 §C pseudocode line 245 hardcodes `string signalName = "PTT-Copy";`; T2 §E confirms "PTT- prefix is never violated" | **CLOSED** ✅ |
| Missing [Fact] for `SetAtmMode` | T2 §F / T3 §C/F | T-B8-13 added to T2 §F; `[Fact] SetAtmMode_UpdatesAtmTemplate_RebuildsRules` body in T3 §F (lines 861–895) | **CLOSED** ✅ |

---

### T1 — Per-Account Qty Multiplier (Cycle 2 Delta Review)

#### Traceability: PASS
No changes to traceability since Cycle 1. All items map to DW-B7-01 and SPEC line 2319. T-B8-12 addition maps to T1 §C #5 (`SetFollowerMultiplier`) — not phantom work.

#### JS Pre-Check: PASS
No new JS-rule–relevant patterns introduced in Revision 1. T-B8-12 test code uses ConcurrentBag reflection, no `lock()`, no `throw new`, no `return null`, no `Dictionary<`. PASS carried forward.

#### CYC Pre-Check: PASS
No method estimates changed. `SetFollowerMultiplier` is described as ConcurrentBag rebuild pattern (CYC ≈ 3–4). PASS carried forward.

#### NT8 Check: PASS
No NT8 constraint changes in T1. PASS carried forward.

#### Test Coverage: PASS
| New Method (T1 §C) | [Fact] Test | Status |
|--------------------|------------|--------|
| `AddRule` (5-arg overload) | T-B8-01 | PASS |
| `GetMultiplier` (out-of-range) | T-B8-02 | PASS |
| `GetMultiplier` (valid index) | T-B8-03 | PASS |
| `GetMultiplier` (null array) | T-B8-04 | PASS |
| `SetFollowerMultiplier` | **T-B8-12** | **PASS — present in T1 §F and T3 §C/§F with complete body** |
| `DispatchCopy` (modified) | Not unit-testable (live NT8 Account) | Acceptable |

#### Scan Checklist: PASS
7 scans present in T1 §G (SCAN-01 through SCAN-07). ✅

#### VERDICT: **TICKET_REVIEW_PASS**

---

### T2 — FollowerAtmMode Behavioral Wiring (Cycle 2 Delta Review)

#### Traceability: PASS
T-B8-13 addition maps to T2 §C #2 (`SetAtmMode`) — not phantom work.

#### JS Pre-Check: PASS
No new JS-rule violations in Revision 1. T-B8-13 test code: no `lock()`, no `Dictionary<`, `ImmutableDictionary.Empty` used, `[Fact]` xUnit only. PASS carried forward.

#### CYC Pre-Check: PASS
No method estimates changed. PASS carried forward.

#### NT8 Check: PASS
| Constraint | T2 Status |
|-----------|-----------|
| No async/await in lifecycle | PASS — all T2 methods synchronous |
| Off-thread UI → Dispatcher.InvokeAsync | PASS — WPF event handlers on UI thread |
| Account.All outside Loaded | PASS — not accessed in any T2 method |
| TradeCopierWindow sealed | PASS — "No sealed modifier added" confirmed |
| `CreateOrder` signal name must start "PTT-" | **PASS** — Revision 1 fix: `signalName = "PTT-Copy"` hardcoded for all modes; Named ATM template routed via `atmTemplate` parameter (position 12 of `Account.CreateOrder`); T2 §C pseudocode line 245 and T2 §E NT8 Constraints row both confirm invariant holds |

#### Test Coverage: PASS
| New Method (T2 §C) | [Fact] Test | Status |
|--------------------|------------|--------|
| `SetAtmMode` | **T-B8-13** | **PASS — present in T2 §F and T3 §C/§F with complete body** |
| `GetAtmMode` (no entry) | T-B8-06 | PASS |
| `GetAtmMode` (Named entry) | T-B8-07 | PASS |
| `ParseAtmModeName` | T-B8-11 | PASS |
| `AtmModeToString` | Indirectly via T-B8-09 (persistence round-trip) | Acceptable |
| `FollowerAtmMode` constructors | T-B8-05 | PASS |
| `SendCopy` (modified) | Not unit-testable (live NT8 Account) | Acceptable |
| `OnFollowerAtmComboLoaded` / `OnFollowerAtmModeChanged` / `OnRowApply` | WPF methods, not unit-testable | Acceptable |

#### Scan Checklist: PASS
7 scans present in T2 §G (SCAN-01 through SCAN-07). ✅

#### VERDICT: **TICKET_REVIEW_PASS**

---

### T3 — Tests for B8 Features (Cycle 2 Delta Review)

#### Traceability: PASS
T-B8-12 → DW-B7-01 / T1 §C #5. T-B8-13 → DW-B7-03 / T2 §C #2. Both additions close documented missing-test gaps from Cycle 1. No phantom work.

#### JS Pre-Check: PASS
All 13 tests: `[Fact]` attribute, `Xunit` namespace, no NUnit/MSTest. No `lock()`. No `DateTime.Now`. `ImmutableDictionary<string, FollowerAtmMode>.Empty` used throughout. `throw ex` at line 779 of `DtoToRule_NullMultipliers_DoesNotThrow` is a test re-throw of a caught `TargetInvocationException` — not matched by `throw new XxxException(...)` pattern; not a hot-path production method. PASS.

#### CYC Pre-Check: PASS
All 13 test methods ≤ CYC 3. PASS carried forward.

#### NT8 Check: PASS
No NT8 lifecycle methods. Null accounts. Temp paths. PASS carried forward.

#### Test Coverage: PASS
All 13 declared `[Fact]` methods have complete bodies in T3 §F:
- T-B8-01 through T-B8-04 (multiplier storage, bounds, retrieval, null) ✅
- T-B8-05 through T-B8-07, T-B8-11 (ATM mode variants, GetAtmMode, ParseAtmModeName) ✅
- T-B8-08 through T-B8-10 (persistence round-trips, backward compat) ✅
- T-B8-12 (`SetFollowerMultiplier_UpdatesMultiplier_RebuildsRules`) ✅
- T-B8-13 (`SetAtmMode_UpdatesAtmTemplate_RebuildsRules`) ✅

**Editorial note (WARN — not FAIL)**: T3 §B states "append 11 new [Fact] tests" but §C declares 13 and §F provides 13 complete bodies. The Test Count Verification table at end of tickets correctly states 13. The §B count of "11" is an uncorrected remnant from the pre-Revision-1 draft. The authoritative sources (§C declarations, §F bodies, overview table) agree at 13. No test is missing; no body is absent. This is a documentation typo, not a coverage gap.

#### Scan Checklist: PASS
7 scans present in T3 §G (SCAN-01 through SCAN-07). SCAN-02 qualification ("ZERO (hot path methods)") is technically correct — `throw ex` does not match `grep -r "throw new"`. ✅

#### VERDICT: **TICKET_REVIEW_PASS**

---

### Overall Cycle 2 Summary

| Ticket | Traceability | JS Pre-Check | CYC Pre-Check | NT8 Check | Test Coverage | Scan Checklist | VERDICT |
|--------|-------------|-------------|--------------|-----------|--------------|----------------|---------|
| T1 | PASS | PASS | PASS | PASS | PASS | PASS | **TICKET_REVIEW_PASS** |
| T2 | PASS | PASS | PASS | **PASS** | **PASS** | PASS | **TICKET_REVIEW_PASS** |
| T3 | PASS | PASS | PASS | PASS | **PASS** | PASS | **TICKET_REVIEW_PASS** |

**Warnings (non-blocking)**:
- T3 §B count reads "11" instead of "13" — documentation typo only; all 13 bodies present.

### Overall: **TICKET_REVIEW_PASS**

All three Cycle 1 violations are closed. No new violations introduced. Tickets are cleared for Phase 5 (ticket execution).
