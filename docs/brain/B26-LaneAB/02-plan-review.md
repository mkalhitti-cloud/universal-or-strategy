# B26 Lane A+B — Plan Review

**Epic**: B26-LaneAB  
**Reviewer**: ptt-plan-reviewer  
**Phase**: 2 (Plan Review)  
**Plan Input**: `docs/brain/B26-LaneAB/02-architecture-plan.md`  
**Spec Reference**: `specs/002-trade-copier-spec.html` § `#block-b26`  
**Rules Reference**: `docs/standards/jane-street/RULES_CATALOG.md`  
**Date**: 2026-07-16  
**Result**: **REVIEW_PASS**

---

## Section 1 — Violation Register

**Zero violations found.**

| # | Rule ID | Severity | Description | Plan Location | Finding |
|---|---------|----------|-------------|---------------|---------|
| — | JS-021 | P0 | lock() anywhere | All changes | No `lock(` introduced. PASS |
| — | JS-001 | P0 | throw in hot path | All changes | No `throw` introduced. PASS |
| — | JS-002 | P0 | return null where value expected | All changes | No `return null` introduced. PASS |
| — | JS-033 | P0 | async void (non-event-handler) | All changes | No `async void` introduced. PASS |
| — | JS-023 | P1 | UI update from off-thread without Dispatcher.InvokeAsync | Section A Change 4 | `Dispatcher.InvokeAsync` used correctly. PASS |
| — | JS-036/037 | P0 | New heap allocation in hot path | All changes | No `new byte[]` / `new T[]` introduced. PASS |
| — | JS-008 | P1 | Mutable struct / SolidColorBrush not Frozen | All changes | No SolidColorBrush introduced. PASS |
| — | NT8 | — | async/await in lifecycle hooks | All changes | Not present. PASS |
| — | NT8 | — | FontFamily override | All changes | Not referenced. PASS |
| — | NT8 | — | Hardcoded #RRGGBB hex | All changes | Not referenced. PASS |
| — | NT8 | — | DateTime.Now | All changes | Not referenced. PASS |
| — | NT8 | — | ASCII-only identifiers | All new identifiers | `accountName`, `acc`, `instr` — all ASCII. PASS |
| — | CYC | P1 | Any method CYC > 8 | Section B | Worst-case new CYC = 5. PASS |

---

## Section 2 — Spec Coverage Matrix

| Requirement ID | Spec Location | Plan Section | Addressed? | Notes |
|----------------|---------------|--------------|-----------|-------|
| DW-B26-01 (P0) — wrong BreakEven overload in trail callback | spec:10143–10161 | Section A Change 2 | **YES** | L1422: `BreakEven(instr, newBuffer)` → `BreakEven(acc, instr, newBuffer)`. Exact fix specified. |
| DW-B26-02 (P0) — PendingBeFired broadcast carries no account identity | spec:10163–10188 | Section A Changes 1, 3, 4, 5 | **YES** | 4-line fix across 2 files: event signature, invoke site, dispatcher, OnBeConnected guard. |
| B26 [Fact] target: baseline 131 → 133 (+2 tests) | spec:10250 | Section D | **YES** | T_B26_01 + T_B26_02 fully specified with scenarios, assertions, and coverage rationale. |
| 2-arg BreakEven(Instrument, int) must NOT be deleted | spec:10245 | Section A Change 2, Section F | **YES** | Plan explicitly states "2-arg overload NOT deleted — still live via TradeCopierWindow.cs L691". |
| DW-B26-03 (P1) — Armed visual fix | spec:10191–10209 | Out of scope (Lane C) | **OUT OF SCOPE** | Plan header states Lane C is explicitly excluded. Spec:10249 confirms "All lanes independent". Valid exclusion. |
| DEAD-B26 (P1) — dead field/method removal | spec:10213–10235 | Out of scope (Lane C) | **OUT OF SCOPE** | Same lane-independence exclusion. Valid. |

**In-scope coverage: 100% (3/3 in-scope requirements addressed).**

---

## Section 3 — Key Review Point Adjudications

### R1 — [Fact] Count: 1 test (orchestrator brief) vs 2 tests (plan)

**Finding: ACCEPT 2 TESTS. Plan is correct.**

The orchestrator brief specified a target of 132 (+1 test). The plan specifies 133 (+2 tests).

**Governing authority**: Spec `#block-b26` Architecture Decisions (spec:10250) — explicitly LOCKED by best-of-N synthesis:
> *"B26 requires 2 new tests: T_B26_01_TrailBe_WithNoRule_StillMovesStop … T_B26_02_PendingBeFired_CarriesAccountName. Baseline 131 → target 133."*

The spec supersedes the orchestrator brief. The two defects (DW-B26-01 and DW-B26-02) are completely independent code paths with no shared machinery — 2 tests is architecturally correct and fully spec-mandated. Target **133** is the authoritative figure.

---

### R2 — Change 2 Null-Safety: `acc` nullable at L1422

**Finding: PASS — null handled inside the callee.**

`acc` is `_trailBeAccount` captured at L1405 inside `OnTrailBeAccountUpdate`. It could theoretically be null if `DisarmTrailBe` races between capture and use. The plan correctly identifies this risk (Section A Change 2) and resolves it by delegation: the 3-arg `BreakEven(Account, Instrument, int)` overload has a null guard at its top (~L1203). A null `acc` causes an early return inside the callee — no NullReferenceException, no state mutation. The plan does not introduce or suppress this risk; it correctly relies on existing callee protection.

**Verdict**: No JS-002 violation. No new null exposure. PASS.

---

### R3 — Change 5 Guard Thread-Safety: `_leaderAccount` read in `OnBeConnected`

**Finding: PASS — runs on UI thread.**

The call chain is:
1. `CopyEngine` fires `PendingBeFired?.Invoke(...)` on an account-event thread.
2. `TradeCopierPanel.OnPendingBeFiredDispatch(string instr, string accountName)` receives the call.
3. Immediately delegates to `Dispatcher.InvokeAsync(() => OnBeConnected(instr, accountName))`.
4. `OnBeConnected` runs on the WPF/NT8 UI thread.

`_leaderAccount` is a UI-thread field (set from the panel constructor and UI-thread callbacks). Reading it in `OnBeConnected` — which runs exclusively on the UI thread via `Dispatcher.InvokeAsync` — is unconditionally safe. No cross-thread access to `_leaderAccount` is introduced by this change.

The `accountName` string argument is captured by the lambda closure at step 3 and is immutable (`string` is immutable in C#), so cross-thread string passing is safe.

**Verdict**: No JS-023 violation. Thread-safety maintained. PASS.

---

### R4 — Section C: Subscriber Impact Analysis

**Finding: PASS — analysis is complete and correct.**

The plan identifies the complete subscriber set:

| Location | Role | Text Edit Required? |
|----------|------|-------------------|
| TradeCopierPanel.cs L435 `+= OnPendingBeFiredDispatch` | Subscribe | None — method group reference resolves from new delegate type |
| TradeCopierPanel.cs L398 `-= OnPendingBeFiredDispatch` | Unsubscribe | None — same |
| TradeCopierWindow.cs | Zero subscriptions (orchestrator grep confirmed) | None |

The claim that method group references auto-resolve from the event declaration is correct C# semantics: when the event's delegate type changes and the method signature is updated (Change 4), the existing `+=` and `-=` lines compile without modification. No additional subscriber files need editing.

**Verdict**: Section C analysis is accurate and complete. PASS.

---

### R5 — Section F: Dead Code Analysis

**Finding: PASS — 2-arg overload correctly identified as NOT dead.**

After the DW-B26-01 fix:
- L1422 (in `OnTrailBeAccountUpdate`) no longer calls `BreakEven(Instrument, int)` — the only *incorrect* use is fixed.
- TradeCopierWindow.cs L691 (`OnRuleBreakEven`) still calls `BreakEven(Instrument, int)` — confirmed by spec:10245 ("Agent-A confirmed it is live on the copy-fan-out path").

The 2-arg overload serves the legitimate multi-follower fan-out path (copy mode) and must not be deleted. The plan's instruction "Do not delete" is correct.

**Verdict**: No dead-code misidentification. PASS.

---

### R6 — CYC Analysis

**Finding: PASS (minor CYC baseline discrepancy noted — does not affect outcome).**

| Method | Plan Baseline CYC | Spec CYC Delta | New CYC (Plan) | Limit | Status |
|--------|------------------|----------------|----------------|-------|--------|
| `OnTrailBeAccountUpdate` | 5 | unchanged | 5 | 8 | PASS |
| `OnPendingBeFiredDispatch` | 1 | unchanged | 1 | 8 | PASS |
| `OnBeConnected` | 3 | +1–2 | 4–5 (worst-case) | 8 | PASS |

**Discrepancy noted**: Spec:10246 states "CYC delta: +1 (OnBeConnected 1→2)" implying a baseline of 1. The plan states baseline CYC=3 for `OnBeConnected` (citing 3 existing decision points). This is a baseline discrepancy between spec and plan.

**Ruling**: The discrepancy does not cause a violation — both the spec's figure (1+1=2) and the plan's more conservative figure (3+2=5) are well below the limit of 8. The plan's figure is the safer bound. The discrepancy is noted for the engineer's awareness but does not block the review.

---

### R7 — NT8 Compiler Checklist

**Finding: PASS — all applicable rules checked, no violations.**

The plan's Section E checks 11 NT8 rules. Reviewer confirms:
- No `{ get; init; }` (NT8-001): only `string` parameters and event delegate type changes.
- No records (NT8-002): not introduced.
- No `volatile double` (NT8-003): not introduced.
- No `ImmutableDictionary` (NT8-004): not introduced.
- No `CreateOrder` arg-12 string (NT8-007): not touched.
- No `async void` non-event-handler: not introduced.
- No `lock()`: not introduced.
- No `DateTime.Now`: not referenced.
- ASCII-only: `accountName`, `acc`, `instr` are all ASCII.
- No `FontFamily`: not referenced.
- No hardcoded hex: not referenced.

**Verdict**: All NT8 rules satisfied. PASS.

---

### R8 — Rules Catalog Gate

**Finding: PASS — all P0 rules clear for new code.**

Plan Section "Rules Catalog Gate" lists 8 rules. Reviewer independently verified:

| Rule | P-Level | Check | Result |
|------|---------|-------|--------|
| JS-021 `lock(` | P0 | Grep new lines: no `lock(` | PASS |
| JS-001 `throw new XxxException(` | P0 | No exceptions introduced | PASS |
| JS-002 `return null;` | P0 | No new `return null` introduced | PASS |
| JS-033 `async void` | P0 | No `async void` introduced | PASS |
| JS-036 `new byte[` | P0 | No new heap array allocations | PASS |
| JS-037 `new T[N]` without ArrayPool | P0 | No new array allocations | PASS |
| JS-023 UI from off-thread | P1 | `Dispatcher.InvokeAsync` used correctly (Change 4) | PASS |
| ASCII-only | — | All identifiers ASCII | PASS |

No P0 or P1 violations exist in the 5 changed lines.

---

## Section 4 — Plan Quality Assessment

| Dimension | Assessment |
|-----------|------------|
| Spec alignment | Perfect. All in-scope defects (DW-B26-01, DW-B26-02) addressed with exact line references matching spec. |
| Change set minimality | Excellent. 5 lines changed across 2 files. Zero scope creep. Lane C explicitly excluded per spec lane independence. |
| Test coverage | Strong. 2 tests with full scenario descriptions, pre-fix failure conditions, and post-fix pass conditions. |
| CYC analysis | Complete. All 3 affected methods analyzed. Worst-case CYC=5 within limit. |
| Subscriber impact | Complete. Full subscriber scan performed. Compiler semantics for method group resolution correctly applied. |
| Dead code analysis | Correct. 2-arg overload correctly retained. Spec corroborates finding. |
| Thread-safety reasoning | Explicit and correct. Dispatcher.InvokeAsync chain fully traced. |
| NT8 compliance | All 11 rules checked. No violations. |
| Rules Catalog gate | All P0 rules clear. Independent reviewer verification confirms. |

---

## Section 5 — Pre-Ticket Gate Checklist

- [x] Plan scope matches spec §block-b26 Lane A + Lane B
- [x] DW-B26-01 fix is at call site only (L1422); 2-arg overload preserved
- [x] DW-B26-02 fix is 4 lines across 2 files; CYC within limit
- [x] [Fact] count 2 / target 133 confirmed against locked spec Architecture Decisions
- [x] Zero P0 violations (JS-021, JS-001, JS-002, JS-033, JS-036, JS-037)
- [x] Zero P1 violations (JS-023 UI thread — correct Dispatcher.InvokeAsync usage)
- [x] NT8 checklist all PASS
- [x] CYC worst-case 5 < limit 8
- [x] Thread-safety verified for `_leaderAccount` read in `OnBeConnected`
- [x] Null-safety verified for `acc` at L1422 via callee guard
- [x] Subscriber impact analysis complete (TradeCopierWindow.cs: zero subscribers confirmed)
- [x] Dead code analysis complete (2-arg overload: NOT dead)

---

## Result

**REVIEW_PASS**

Zero violations. Zero spec requirements unaddressed (within declared scope). Plan is cleared for Phase 3 ticket generation.

*Signed: ptt-plan-reviewer | B26-LaneAB | Phase 2*
