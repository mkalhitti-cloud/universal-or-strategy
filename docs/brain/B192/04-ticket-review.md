# Ticket Review: B192
# GlobalQuickAll MGC/GC Tick Default Bug Fix

**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Date**: 2026-07-15
**Source tickets**: docs/brain/B192/04-tickets.md
**Source plan**: docs/brain/B192/02-architecture-plan.md (REVIEW_PASS, cycle 2)
**Spec**: specs/002-trade-copier-spec.html (B192 amendment at line 41621)

---

## Source Baseline Verification

Source files verified against ticket-stated baselines before review:

| File | Lines | Baseline match |
|------|-------|----------------|
| `src/PropTraderTools/Features/PttQuickExit.cs` | 374-395 | MATCH — 3-branch GetQuickTicks with null/MES/MGC guards confirmed |
| `src/PropTraderTools/TradeCopierPanel.cs` | 499-516 | MATCH — SetInstrument without ResetGlobalQuickAll call confirmed |
| `src/PropTraderTools/CopyEngine.cs` | 437-460 | MATCH — IncrementQuickAll/DecrementQuickAll exist; no ResetGlobalQuickAll confirmed |

---

## Cross-Cutting Observation: Lane B Ticket-Number Inversion vs Plan

> **WARN** (not FAIL — content and dependency ordering are both correct):
>
> The approved architecture plan (02-architecture-plan.md) labels the Lane B tickets as:
> - **T3** = `CopyEngine.ResetGlobalQuickAll` (Lane B Step 1, implement first)
> - **T2** = `TradeCopierPanel.SetInstrument` (Lane B Step 2, depends on T3)
>
> The generated tickets invert these numbers:
> - **T2** = `CopyEngine.ResetGlobalQuickAll` (implement first)
> - **T3** = `TradeCopierPanel.SetInstrument` (implement after T2)
>
> **Why not a FAIL**: The ticket content, spec requirement mapping (SR-B192-03, SR-B192-04),
> implementation designs, and compile-time dependency ordering are all correct and match
> the plan exactly. The inversion is purely cosmetic. The engineer who reads the tickets
> will implement the correct work in the correct order. However, if the engineer also reads
> the plan (section 9 execution order), they will encounter contradictory ticket numbers.
>
> **Recommendation for architect** (no re-issue required): annotate the plan section 9 or
> the ticket header with a cross-reference note mapping plan labels to ticket labels.

---

## T1 — InstrumentDefaults.GetQuickTicks (PttQuickExit.cs)

### Traceability: PASS

| Plan item | Ticket item | Status |
|-----------|-------------|--------|
| Plan §3 T1: add GC→(10,20) guard | Ticket T1 §Target: GC StartsWith guard returning (10,20) | ✅ MATCH |
| Plan §3 T1: explicit ES/NQ/MNQ→(4,8) guards | Ticket T1 §Target: ES, NQ, MNQ StartsWith guards | ✅ MATCH |
| Plan §3 T1 CYC 3→7 | Ticket T1 §CYC Budget: before=3, after=7 | ✅ MATCH |
| SR-B192-01 | Cited in Ticket Header and §Spec Requirements Satisfied | ✅ |
| SR-B192-02 | Cited in Ticket Header and §Spec Requirements Satisfied | ✅ |
| No phantom work | All ticket changes trace to plan §3 T1 and SR-B192-01/02 | ✅ |
| No missing work | All plan T1 items appear in ticket | ✅ |

### JS Pre-Check: PASS

| Rule | Check | Finding |
|------|-------|---------|
| JS-001 | No `throw new XxxException` | No throw in method body. Returns tuple at all branches. ✅ |
| JS-002 | No `return null` | Returns `(int, int)` tuple at every path including default. Never null. ✅ |
| JS-021 | No `lock(` | Static pure method, no shared state, no lock. ✅ |
| JS-033 | No `async void` | Synchronous `internal static` method. ✅ |
| JS-066 | CYC ≤ 8 | CYC=7 (7 if-branch decision points). Ceiling=8. Headroom=1. ✅ |
| JS-081 | ASCII-only literals | All instrument prefix strings ("MES", "ES", "NQ", "MNQ", "MGC", "GC") are ASCII. ✅ |

### CYC Pre-Check: PASS

| | Value |
|---|---|
| CYC before | 3 (null + MES + MGC decision points) |
| CYC after | 7 (null + MES + ES + NQ + MNQ + MGC + GC decision points) |
| JS-066 ceiling | 8 |
| Headroom | 1 ✅ |

Ordering safety: `"MNQ".StartsWith("NQ")` = false (MNQ begins with 'M'); NQ guard before MNQ
guard is safe. `"ES".StartsWith("MES")` = false (ES begins with 'E'); MES guard before ES is
safe. Ticket provides and the plan confirms an explicit ordering safety proof table. ✅

### NT8 Check: PASS

No NT8 API calls in the changed method. Pure static method operating on a `string` parameter. ✅

### Test Coverage: PASS

10 `[Fact]` tests specified with full input/expected/purpose table. All 10 test method names
provided. 3 example test bodies given (matching xUnit [Fact] + Assert.Equal pattern, JS-051,
JS-052, JS-054). Tests cover:

- GC bare name (SR-B192-01) ✅
- GC with expiry date — prefix-match validation (SR-B192-01) ✅
- ES with expiry date (SR-B192-02) ✅
- NQ with expiry date (SR-B192-02) ✅
- MNQ with expiry date (SR-B192-02) ✅
- MGC regression (existing behaviour unchanged) ✅
- MES regression (existing behaviour unchanged) ✅
- Empty string edge case ✅
- Null edge case ✅
- Unknown instrument default path ✅

No public or internal methods without a corresponding `[Fact]`. ✅

### Scan Checklist: PASS

All 7 scans present with patterns and expected-0-matches results:

| Scan | Present |
|------|---------|
| SCAN-01 `lock\s*\(` | ✅ |
| SCAN-02 `throw\s+new\s+\w+Exception\(` | ✅ |
| SCAN-03 `return\s+null\s*;` | ✅ |
| SCAN-04 `DateTime\.Now` | ✅ |
| SCAN-05 `#[0-9A-Fa-f]{6}` / `Color\.FromRgb\(` | ✅ |
| SCAN-06 `FontFamily` | ✅ |
| SCAN-07 Non-ASCII in literals/identifiers | ✅ |

Scope correctly identified as changed lines in `src/PropTraderTools/Features/PttQuickExit.cs`. ✅

### File Routing: PASS

`src/PropTraderTools/Features/PttQuickExit.cs` — Wave workspace path. ✅

### VERDICT: TICKET_REVIEW_PASS

---

## T2 — CopyEngine.ResetGlobalQuickAll (CopyEngine.cs)

> Note: This ticket corresponds to **T3** in the approved architecture plan (plan §3, §8, §9).
> See cross-cutting observation above. Content is correct.

### Traceability: PASS

| Plan item | Ticket item | Status |
|-----------|-------------|--------|
| Plan §3 (T3 in plan): new `ResetGlobalQuickAll(int t1)` method | Ticket T2 §Target: method inserted between DecrementQuickAll and event declaration | ✅ MATCH |
| Plan §3: volatile int write + snapshot pattern + Dispatcher.InvokeAsync | Ticket T2 §Target and §Design Notes: all three elements present | ✅ MATCH |
| Plan §3: CYC=1 | Ticket T2 §CYC Budget: CYC=1 | ✅ MATCH |
| SR-B192-04 | Cited in Ticket Header and §Spec Requirements Satisfied | ✅ |
| Compile dependency noted (T3 depends on T2) | Ticket header callout and §Dependency field | ✅ |
| No phantom work | All ticket additions trace to plan §3 T3 and SR-B192-04 | ✅ |
| No missing work | Plan T3 items fully covered | ✅ |

### JS Pre-Check: PASS

| Rule | Check | Finding |
|------|-------|---------|
| JS-001 | No `throw new XxxException` | No throw. Method is void, no exception path. ✅ |
| JS-002 | No `return null` | void method. ✅ |
| JS-021 | No `lock(` | `_globalQuickAllT1` is `volatile int`; single assignment is atomic on .NET. No lock. ✅ |
| JS-023 | UI update via Dispatcher.InvokeAsync | Event dispatch uses `System.Windows.Application.Current.Dispatcher.InvokeAsync`. Established pattern (HOTFIX-DISPATCH-FIX-01). ✅ |
| JS-033 | No `async void` | Method signature is synchronous `internal void`. The lambda passed to InvokeAsync is not marked async. ✅ |
| JS-066 | CYC ≤ 8 | CYC=1 (straight-line path, no decision points). ✅ |
| JS-081 | ASCII-only literals | Comment and string content are ASCII-only. ✅ |

### CYC Pre-Check: PASS

CYC=1 (single straight-line path; no if, switch, loop, or conditional operator). JS-066 ceiling=8. ✅

### NT8 Check: PASS

`System.Windows.Application.Current.Dispatcher.InvokeAsync` is an established pattern already
in production at lines 445-447 and 455-457 (IncrementQuickAll / DecrementQuickAll). The new
method is an exact structural mirror of those methods. No null-guard on `Application.Current`
is added — consistent with the existing pattern in the same class. No new NT8 API surface
is introduced. ✅

### Test Coverage: PASS

3 `[Fact]` tests specified with setup/action/assert table. All 3 test method names provided
with full example bodies (xUnit [Fact] + Assert.Equal, JS-051, JS-052, JS-054):

- `Fact_ResetGlobalQuickAll_SetsToFour` — basic reset to 4 ✅
- `Fact_ResetGlobalQuickAll_SetsToTen` — reset to 10 (GC/MGC default) ✅
- `Fact_ResetGlobalQuickAll_OverwritesPreviousIncrement` — overwrite after increment ✅

One assert per test (JS-056). ✅

### Scan Checklist: PASS

All 7 scans present with patterns and expected-0-matches results:

| Scan | Present |
|------|---------|
| SCAN-01 `lock\s*\(` | ✅ |
| SCAN-02 `throw\s+new\s+\w+Exception\(` | ✅ |
| SCAN-03 `return\s+null\s*;` | ✅ |
| SCAN-04 `DateTime\.Now` | ✅ |
| SCAN-05 `#[0-9A-Fa-f]{6}` / `Color\.FromRgb\(` | ✅ |
| SCAN-06 `FontFamily` | ✅ |
| SCAN-07 Non-ASCII in literals/identifiers | ✅ |

Scope correctly identified as inserted lines in `src/PropTraderTools/CopyEngine.cs`. ✅

### File Routing: PASS

`src/PropTraderTools/CopyEngine.cs` — Wave workspace path. ✅

### VERDICT: TICKET_REVIEW_PASS

---

## T3 — TradeCopierPanel.SetInstrument (TradeCopierPanel.cs)

> Note: This ticket corresponds to **T2** in the approved architecture plan (plan §3, §8, §9).
> See cross-cutting observation above. Content is correct.

### Traceability: PASS

| Plan item | Ticket item | Status |
|-----------|-------------|--------|
| Plan §3 (T2 in plan): add `CopyEngine.Instance?.ResetGlobalQuickAll(ticks.t1)` as final statement in SetInstrument | Ticket T3 §Target: single line addition at method close | ✅ MATCH |
| Plan §3: null-conditional `?.` (no-op if engine not init) | Ticket T3 §Design Notes note 1 | ✅ |
| Plan §3: CYC unchanged (`?.` is not a cyclomatic branch) | Ticket T3 §CYC Budget and §Design Notes note 4 | ✅ |
| SR-B192-03 | Cited in Ticket Header and §Spec Requirements Satisfied | ✅ |
| B190 amendment acknowledged | Ticket T3 §Design Notes note 5 and §Spec Requirements Satisfied | ✅ |
| Compile dependency on T2 noted | Ticket header callout and §Dependency field | ✅ |
| No phantom work | Single line traces directly to plan §3 T2 and SR-B192-03 | ✅ |
| No missing work | Plan T2 items fully covered | ✅ |

### JS Pre-Check: PASS

| Rule | Check | Finding |
|------|-------|---------|
| JS-001 | No `throw new XxxException` | No throw. ✅ |
| JS-002 | No `return null` | void method, no return. ✅ |
| JS-021 | No `lock(` | No lock in changed line. ✅ |
| JS-033 | No `async void` | Method is synchronous `public void`. ✅ |
| JS-066 | CYC ≤ 8 | CYC unchanged — null-conditional `?.` is not a cyclomatic branch under project convention. ✅ |

### CYC Pre-Check: PASS

`?.` (null-conditional member access) does not add a cyclomatic branch under the project's
established CYC counting convention. Confirmed by Design Notes §4 in ticket and plan §3 (T2
in plan) §CYC Budget. CYC of SetInstrument is unchanged from pre-B192. ✅

### NT8 Check: PASS

The changed line `CopyEngine.Instance?.ResetGlobalQuickAll(ticks.t1)` does not call any
NT8 API directly. `SetInstrument` runs on the UI thread (confirmed by Design Notes §2).
Calling `ResetGlobalQuickAll` from the UI thread is safe; the method internally dispatches
its event to the UI thread via `Dispatcher.InvokeAsync`. No new NT8 API surface. ✅

### Test Coverage: PASS

`SetInstrument` is modified (not a new method) and requires a live NT8 `Instrument` object.
Pure xUnit testing is correctly identified as not possible. SIM verification is the appropriate
substitute. 11 SIM steps (SIM-1 through SIM-11) are specified with action and expected result
columns, covering MGC, GC, ES, NQ, and MNQ instrument switches. All SIM steps gated before
`VERIFY_PASS`. ✅

The new call `CopyEngine.Instance?.ResetGlobalQuickAll(ticks.t1)` is covered at the unit level
by the T2 xUnit tests (`Fact_ResetGlobalQuickAll_*`). The integration path is covered by SIM. ✅

### Scan Checklist: PASS

All 7 scans present with patterns and expected-0-matches results:

| Scan | Present |
|------|---------|
| SCAN-01 `lock\s*\(` | ✅ |
| SCAN-02 `throw\s+new\s+\w+Exception\(` | ✅ |
| SCAN-03 `return\s+null\s*;` | ✅ |
| SCAN-04 `DateTime\.Now` | ✅ |
| SCAN-05 `#[0-9A-Fa-f]{6}` / `Color\.FromRgb\(` | ✅ |
| SCAN-06 `FontFamily` | ✅ |
| SCAN-07 Non-ASCII in literals/identifiers | ✅ |

Scope correctly identified as changed lines in `src/PropTraderTools/TradeCopierPanel.cs`. ✅

### File Routing: PASS

`src/PropTraderTools/TradeCopierPanel.cs` — Wave workspace path. ✅

### VERDICT: TICKET_REVIEW_PASS

---

## Aggregate Spec Coverage Check

| Spec Requirement | Ticket | Coverage |
|-----------------|--------|----------|
| SR-B192-01: GC returns (10, 20) | T1 | ✅ Covered exactly once |
| SR-B192-02: Explicit ES/NQ/MNQ returns (4, 8) | T1 | ✅ Covered exactly once |
| SR-B192-03: SetInstrument syncs GlobalQuickAllT1 | T3 | ✅ Covered exactly once |
| SR-B192-04: CopyEngine.ResetGlobalQuickAll method | T2 | ✅ Covered exactly once |

No duplicate coverage. No uncovered requirements. ✅

---

## Dependency Ordering Check: PASS

Lane B compile dependency is correctly documented at three points:
1. Top-level Execution Order block in tickets file
2. T2 ticket header callout box: "IMPLEMENT THIS TICKET BEFORE T3"
3. T3 ticket header callout box: "IMPLEMENT AFTER T2"

Engineer cannot miss the ordering constraint. ✅

---

## Summary Matrix

| Ticket | Traceability | JS Pre-Check | CYC Pre-Check | NT8 Check | Test Coverage | Scan Checklist | File Routing | VERDICT |
|--------|-------------|--------------|---------------|-----------|---------------|----------------|--------------|---------|
| T1 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **TICKET_REVIEW_PASS** |
| T2 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **TICKET_REVIEW_PASS** |
| T3 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **TICKET_REVIEW_PASS** |

---

## Overall: TICKET_REVIEW_PASS

All three tickets pass all mandatory checks. One cosmetic WARN is noted (Lane B ticket
numbers T2/T3 are inverted vs the approved plan's T3/T2 labels) but this does not affect
implementation correctness, dependency ordering, spec coverage, or engineer contract integrity.
No rewrite required.

**Green light for Phase 4a (ptt-engineer).**

---

*Written by: ptt-ticket-reviewer*
*Epic: B192*
*Phase: 3.5 (Ticket Review)*
*Status: TICKET_REVIEW_PASS*
