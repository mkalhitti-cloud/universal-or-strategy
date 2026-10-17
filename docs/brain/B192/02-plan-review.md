# B192 Plan Review
# GlobalQuickAll MGC/GC Tick Default Bug Fix

**Epic**: B192
**Reviewer**: ptt-plan-reviewer
**Date**: 2026-07-15
**Cycle**: 2 (re-review after Director spec amendment)
**Plan source**: docs/brain/B192/02-architecture-plan.md (cycle 2, PLAN_COMPLETE)
**Result**: **REVIEW_PASS**

---

## A. Spec Amendment Verification

### A.1 Plan Section 0 Acknowledgment

The plan's Section 0 (cycle-2 addition) correctly:

| Item | Status |
|------|--------|
| Cites the cycle-1 REVIEW_FAIL violation verbatim (T2/T3 contradicted B190 spec line 41621) | CONFIRMED ✅ |
| Quotes the Director's spec amendment text in full at Section 0.2 | CONFIRMED ✅ |
| Maps each prior violation to its resolution in the amendment table (Section 0.3) | CONFIRMED ✅ |
| Documents the accepted multi-panel stomp trade-off in Section 0.4 | CONFIRMED ✅ |
| States no architectural changes to T1/T2/T3 are required | CONFIRMED ✅ |

### A.2 Spec File — Line 41621 (Primary Amendment Location)

Verified text at `specs/002-trade-copier-spec.html` line 41621:

> **B192 SPEC AMENDMENT (supersedes B190 note):** `GlobalQuickAllT1` IS now reset in
> `SetInstrument()` via `CopyEngine.Instance?.ResetGlobalQuickAll(ticks.t1)`. Director
> decision 2026-07-15: Quick ALL and QAll2t must fire with instrument-correct tick counts
> (10 for MGC/GC, 4 for MES/ES/NQ/MNQ). The B190 multi-panel stomp risk is accepted —
> the standard usage is one instrument panel per session. Both per-panel fields
> `_quickT1`/`_quickT2` AND the global singleton are now reset. See B192 tickets T2+T3.

**CONFIRMED** — Amendment is live in the spec file. Text matches the plan's Section 0.2 quote exactly. ✅

### A.3 Spec File — Lines 41958-41959 (Second Amendment Location)

Verified text at `specs/002-trade-copier-spec.html` lines 41958-41959:

> B192 AMENDMENT: add `CopyEngine.Instance?.ResetGlobalQuickAll(ticks.t1);` after label refresh.
> B190 "NO change to GlobalQuickAllT1" note superseded by Director decision 2026-07-15.

**CONFIRMED** — Second amendment location is live. B190 prohibition explicitly superseded. ✅

---

## B. LANE-SPLIT GATE Review

**Gate result carried from cycle 1: LANES-APPROVED**
No changes to the lane analysis in cycle 2. Q1–Q4 verified again:

| Question | Answer | Status |
|----------|--------|--------|
| Q1: Same method or within 50 lines? | No. T1=PttQuickExit.cs, T2=TradeCopierPanel.cs, T3=CopyEngine.cs. Three separate files. | PASS ✅ |
| Q2: Fix B design depends on Fix A final design? | Partial. T2 compile-depends on T3. T1 is fully independent. | PASS ✅ |
| Q3: Each fix has standalone value if the other is blocked? | T1: YES. T3 alone: zero user-visible value (method added, not yet called). T2 alone: compile error. Correct conclusion: T1=Lane A independent, T2+T3=Lane B coupled. | PASS ✅ |
| Q4: Each fix has an independent SIM verification path? | T1: GC panel per-panel Quick exit buttons. T2+T3: Quick ALL button with MGC panel. Independent. | PASS ✅ |

**LANE-SPLIT GATE: LANES-APPROVED** ✅

Lane A: T1 — independent implementation and merge.
Lane B: T3 → T2 (compile-time ordering enforced).

---

## C. Source Code Baseline Verification

All three failure points confirmed against live source files:

| Failure Point | File | Lines | Baseline Confirmed |
|---------------|------|-------|--------------------|
| A: GetQuickTicks missing GC entry; GC falls through to (4,8) | `PttQuickExit.cs` | 385-394 | ✅ Current code has only null+MES+MGC guards; GC falls to default `return (4, 8)` |
| B: SetInstrument does not call ResetGlobalQuickAll | `TradeCopierPanel.cs` | 501-516 | ✅ Current code ends at `_instr2tBtn.Content = ...`; no singleton sync |
| C: CopyEngine has no ResetGlobalQuickAll method | `CopyEngine.cs` | 437-460 | ✅ Only IncrementQuickAll and DecrementQuickAll exist; no Reset method |

Plan's baseline description is accurate in all respects. ✅

---

## D. JS Rule Compliance Check — Proposed Code

### D.1 T1 — `InstrumentDefaults.GetQuickTicks` (PttQuickExit.cs)

| Rule | Check | Result |
|------|-------|--------|
| JS-001 | No `throw new XxxException` | No throw anywhere in method ✅ |
| JS-002 | No `return null` | Returns `(int, int)` tuple exclusively ✅ |
| JS-021 | No `lock()` | Static method, no shared state, no lock ✅ |
| JS-033 | No `async void` | Synchronous static method ✅ |
| JS-066 | CYC ≤ 8 | 7 decision points by project convention (null/empty + MES + ES + NQ + MNQ + MGC + GC). CYC=7 ≤ 8 ✅ |
| JS-081/082 | ASCII-only | All string literals are ASCII instrument name prefixes ✅ |

**T1: NO VIOLATIONS** ✅

**CYC ordering correctness:**
- `"MNQ".StartsWith("NQ")` = false (MNQ starts with 'M') → NQ check safe before MNQ ✅
- `"MGC".StartsWith("GC")` = false (MGC starts with 'M') → GC check safe after MGC ✅
- All prefix guards are mutually exclusive in practice ✅

### D.2 T3 — `CopyEngine.ResetGlobalQuickAll` (CopyEngine.cs)

| Rule | Check | Result |
|------|-------|--------|
| JS-001 | No `throw new XxxException` | No throw in method ✅ |
| JS-002 | void return, no null | Void method ✅ |
| JS-021 | No `lock()` | Uses `volatile int` write (atomic on .NET). No lock required or present ✅ |
| JS-023 | UI update via Dispatcher.InvokeAsync | `GlobalQuickAllBufferChanged?.Invoke(v)` dispatched via `Dispatcher.InvokeAsync` — matching pattern of HOTFIX-DISPATCH-FIX-01 in `IncrementQuickAll` and `DecrementQuickAll` ✅ |
| JS-033 | No `async void` | Method signature is `internal void`, synchronous. Lambda inside `InvokeAsync` is the pre-existing pattern — not a new `async void` ✅ |
| JS-066 | CYC ≤ 8 | CYC=1 (single straight-line path, no decision points) ✅ |
| JS-081/082 | ASCII-only | Comment and identifier ASCII-only ✅ |

**Snapshot pattern (`int v = _globalQuickAllT1`):** Correct. Captures the value post-assignment before the lambda executes asynchronously. This prevents a read-race where another thread modifies the field before the Dispatcher callback runs. This pattern is identical to lines 444/454 in the existing code. ✅

**T3: NO VIOLATIONS** ✅

### D.3 T2 — `TradeCopierPanel.SetInstrument` (TradeCopierPanel.cs)

| Rule | Check | Result |
|------|-------|--------|
| JS-001 | No `throw new XxxException` | No throw in method ✅ |
| JS-002 | No `return null` | void method ✅ |
| JS-021 | No `lock()` | No lock ✅ |
| JS-033 | No `async void` | Synchronous void ✅ |
| JS-066 | CYC unchanged | New line adds zero decision points (`?.` is null-conditional operator, not a branch node under project CYC convention). CYC unchanged from pre-B192 ✅ |
| JS-081/082 | ASCII-only | Comment ASCII-only ✅ |

**Null-conditional guard (`?.`):** Correct defensive pattern. If `CopyEngine.Instance` is null, the call is a no-op. Matches existing defensive null-conditional patterns in `PttGlobalQuickExit.ResolveQuickTicks`. ✅

**Thread safety of calling site:** `SetInstrument` runs on the UI thread (directly assigns `_statusText.Text`). Calling `ResetGlobalQuickAll` from the UI thread is safe. `ResetGlobalQuickAll` then marshals its event via `Dispatcher.InvokeAsync` to avoid re-entrant UI updates. ✅

**T2: NO VIOLATIONS** ✅

---

## E. NT8 Constraint Check

| NT8 Rule | Check | Result |
|----------|-------|--------|
| No `async`/`await` in `OnInitialize`/`OnDestroyed`/`OnWindowCreated` | Not used in any proposed change | ✅ |
| No `Account.All` in constructor | Not applicable | ✅ |
| `sealed TradeCopierWindow` not removed | Not touched | ✅ |
| No `FontFamily` override | Not present in any proposed code (SCAN-06 covers) | ✅ |
| No hardcoded `#RRGGBB` hex | Not present in any proposed code (SCAN-05 covers) | ✅ |
| No `CreateOrder` without PTT- prefix | Not applicable | ✅ |
| No `DateTime.Now` | Not present (SCAN-04 covers) | ✅ |
| No `AtmStrategyCreate` in AddOn context | Not used | ✅ |

**NT8: NO VIOLATIONS** ✅

---

## F. Spec Coverage Matrix

| Spec Requirement | Addressed? | Plan Section |
|-----------------|------------|--------------|
| SR-B192-01: GC must return (10, 20) in GetQuickTicks | YES | T1, Section 3 |
| SR-B192-02: Explicit ES, NQ, MNQ entries → (4, 8) | YES | T1, Section 3 |
| SR-B192-03: SetInstrument syncs `GlobalQuickAllT1` via `ResetGlobalQuickAll` | YES | T2, Section 3 |
| SR-B192-04: `CopyEngine.ResetGlobalQuickAll(int t1)` method created | YES | T3, Section 3 |
| B192 SPEC AMENDMENT: Both per-panel fields AND global singleton reset | YES | T2+T3 together |
| B192 SPEC AMENDMENT: 10 ticks for MGC/GC, 4 for MES/ES/NQ/MNQ | YES | T1 (GetQuickTicks) + T2+T3 (singleton) |
| Deferred backlog (DW-B183-03 unaffected) | Documented | Section 7 |

**SPEC COVERAGE: COMPLETE** ✅

---

## G. Test Strategy Review

| Check | Status |
|-------|--------|
| xUnit `[Fact]` tests for T1 (GetQuickTicks) — no NT8 objects required | 10 tests specified ✅ (JS-051, JS-052) |
| Regression tests for pre-existing behavior (MGC, MES) | Present ✅ |
| GC new test case | Present ✅ |
| xUnit `[Fact]` tests for T3 (ResetGlobalQuickAll) | 3 tests specified ✅ |
| SIM verification for T2 (live NT8 object required, cannot unit test) | Correctly stated as SIM-only ✅ |
| Descriptive test names | Yes (Fact_GetQuickTicks_GC_ReturnsTen etc.) ✅ (JS-055) |
| Test isolation — no external dependencies for T1/T3 | Pure static/volatile int tests ✅ (JS-061) |

**TEST STRATEGY: ADEQUATE** ✅

---

## H. 7-Scan Checklist Review

Plan Section 6 specifies all 7 required scans:

| Scan | Pattern | Verdict |
|------|---------|---------|
| SCAN-01 | `lock\s*\(` | Pattern correct. Applicable to all 3 files ✅ |
| SCAN-02 | `throw\s+new\s+\w+Exception\(` | Pattern correct ✅ |
| SCAN-03 | `return\s+null\s*;` | Pattern correct ✅ |
| SCAN-04 | `DateTime\.Now` | Pattern correct ✅ |
| SCAN-05 | `#[0-9A-Fa-f]{6}` or `Color\.FromRgb\(` | Pattern correct ✅ |
| SCAN-06 | `FontFamily` | Pattern correct ✅ |
| SCAN-07 | Non-ASCII in string literals or identifiers | Pattern correct ✅ |

Expected result specified: 0 matches on all scans. ✅

**7-SCAN CHECKLIST: COMPLETE** ✅

---

## I. Dependency and Execution Order

| Requirement | Plan Section | Status |
|-------------|-------------|--------|
| T3 (ResetGlobalQuickAll) must exist before T2 compiles | Section 9, Lane B ordering | SPECIFIED ✅ |
| T1 may be implemented and merged independently | Section 9, Lane A | SPECIFIED ✅ |
| T3 and T2 may be committed together or sequentially (T3 first) | Section 9 | SPECIFIED ✅ |
| Commit message format specified for both lanes | Section 9 | SPECIFIED ✅ |

**EXECUTION ORDER: CORRECT** ✅

---

## J. New Violations Introduced by Cycle-2 Section 0?

Section 0 is documentation-only (acknowledgment of spec amendment, violation resolution table, trade-off analysis). No new code design elements are introduced. No JS rules or NT8 rules apply to documentation text.

**NO NEW VIOLATIONS FROM CYCLE-2 SECTION 0** ✅

---

## K. Violation Summary

| Violation ID | Description | Location | Status |
|-------------|-------------|----------|--------|
| (none) | — | — | — |

**Zero violations found.** ✅

---

## L. Cycle-1 Violation Closure

| Cycle-1 Violation | Resolution |
|-------------------|------------|
| T2: `CopyEngine.Instance?.ResetGlobalQuickAll(ticks.t1)` called from `SetInstrument()` prohibited by B190 spec line 41621 | **CLOSED** — B192 spec amendment at line 41621 explicitly authorises this call with Director sign-off 2026-07-15 |
| T3: `CopyEngine.ResetGlobalQuickAll(int t1)` prohibited by B190 spec line 41621 | **CLOSED** — same amendment; method is the required implementation vehicle |

Both cycle-1 violations are formally closed. ✅

---

## RESULT

**REVIEW_PASS**

All checks passed. No violations. Cycle-1 violations are closed by Director spec amendment (confirmed live in spec file). The plan is approved for Phase 3 ticket generation.

---

*Reviewer: ptt-plan-reviewer*
*Epic: B192*
*Phase: 2 (Plan Review — cycle 2)*
*Cycle: 2*
*Verdict: REVIEW_PASS*
