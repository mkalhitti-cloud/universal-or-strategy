# PTT-BUGS-LIVE-0911 Lane C — Plan Review
# ptt-plan-reviewer | Cycle 2 | Phase 2 Output

---

## Review Summary

**VERDICT: REVIEW_PASS**

**Violation count: 0**

All four Cycle 1 violations are resolved. The plan is grounded in direct source reads,
architecturally correct, spec-complete, and NT8/Jane Street compliant. No banned APIs,
no lock(), no async/await, no throw in dispatch path, no CYC overage in any proposed method.
Two minor factual inaccuracies are noted below (Recommendations) but neither triggers a
REVIEW_FAIL condition under the stated gate rules.

---

## Cycle 1 Violations Status

| ID | Description | Status | Evidence |
|----|-------------|--------|----------|
| V-01 | `SendCopyWithAtm` must be confirmed present and accurately described | **RESOLVED** | Plan §1.10 reproduces the exact signature, body, and CYC=4 from `src/PropTraderTools/CopyEngine.cs` line 5123. Source-verified: lines 5123–5167 confirmed. |
| V-02 | `_cloneAtmObjectByInstr` must be confirmed present and accurately described | **RESOLVED** | Plan §1.2 cites the field at line 151 as `ConcurrentDictionary<string, AtmStrategy>`. Source-verified: lines 151–152 confirmed. Writer (`SetCloneAtmObjectCache` line 740) and reader (`GetCloneAtmMode` line 751) both confirmed in plan §1.3–§1.4. |
| V-03 | `StartAtmStrategy` must use STATIC form: `NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(...)` | **RESOLVED** | Plan §1.10 cites lines 5149 and 5151 as STATIC calls. Source-verified: both lines confirmed as `NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(namedMode.AtmObject, order)` and `NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(namedMode.TemplateName, order)`. |
| V-04 | Named mode routing exclusively through `SendCopyWithAtm`; `SendCopy` must NOT be proposed for Named-mode ATM logic | **RESOLVED** | Plan §1.8 confirms: `if (mode is FollowerAtmMode.Named namedAtm) SendCopyWithAtm(...)` (line 2795–2796), `else SendCopy(...)` (line 2798). Named mode never reaches `SendCopy`. Source-verified at `DispatchToFollower` lines 2795–2798. |

---

## Lane-Split Gate Compliance

**Gate result stated:** YES — Section 2 of the plan contains an explicit `LANE-SPLIT GATE RESULT: SINGLE-PIPELINE`.

**Q1–Q4 derivation:**

| Question | Plan Answer | Reviewer Assessment |
|----------|-------------|---------------------|
| Q1: Same method or within 50 lines? | YES | **Imprecise derivation** (see Recommendations), but answer direction (YES) is defensible: the two components of the work (`OnOrderUpdate` and `DispatchCopy`) are in the same dispatch chain in the same file. The plan's stated distance of "88 lines" is arithmetically incorrect (1642 vs 2561 = 919 lines); however Q2=YES independently drives single-pipeline, so the Q1 arithmetic error does not affect the gate outcome. |
| Q2: Fix B depends on Fix A final design? | YES | CORRECT. Lane D wiring cannot be placed without first confirming OnOrderUpdate is the correct single entry. Sequential dependency confirmed. |
| Q3: Each fix has standalone value if other is blocked? | YES (Fix A only) | PARTIALLY CORRECT. Fix A (documentation finding) has standalone value. Fix B alone has none. The plan acknowledges this dependency in the Q3 note. The answer "YES" overstates B's standalone value but the conclusion (single-pipeline) is unaffected. |
| Q4: Independent SIM verification paths? | YES | CORRECT. Fix A: code review; Fix B: diagnostic log confirmation per master entry in SIM. |

**Conclusion derivation:** Q1=YES and Q2=YES → SINGLE-PIPELINE. Correct.

**Gate REVIEW_FAIL conditions:**
- Plan uses lanes but Q1/Q2 = YES → NOT triggered (plan is single-pipeline)
- Plan uses lanes but Q3/Q4 = NO → NOT triggered (plan is single-pipeline)
- Plan is single-pipeline but gate result not stated → NOT triggered (gate result IS stated)

**Gate compliance: PASS.**

---

## Source Investigation Quality

| Claim in Plan | Source Line(s) | Verified? | Notes |
|---------------|----------------|-----------|-------|
| `FollowerAtmMode` discriminated union (Inherit/Market/Named) | Lines 80–113 | YES | Exact — private base ctor line 81, Inherit line 83, Market line 88, Named lines 93–113 confirmed. |
| `_cloneAtmObjectByInstr` is `ConcurrentDictionary<string, AtmStrategy>` at line 151 | Lines 151–152 | YES | Exact. |
| `SetCloneAtmObjectCache` at line 740, CYC=2 | Lines 740–746 | YES | Exact body confirmed. |
| `GetCloneAtmMode` at line 751, CYC=4, never null | Lines 751–763 | YES | Exact — 3 return paths all non-null confirmed. |
| `ResolveAtmMode` at line 5183, CYC=2 | Lines 5183–5188 | YES | Exact. |
| `OnOrderUpdate` at line 1642, subscribed via `acc.OrderUpdate += OnOrderUpdate` line 1601 | Lines 1597–1601, 1642 | YES | Exact. |
| Pre-gate block sequence (EvictDedup → ... → TryDrainWatchdog) | Lines 1644–1692 | YES | All 14 pre-gate calls confirmed in correct order. |
| `DispatchCopy(e.Order, matchedRule)` at line 1729, ONLY call site | Line 1729 | YES | One live call site confirmed. NOTE: plan states "two total references" — this is imprecise (5 grep matches including comments). The substantive claim of ONE call site is accurate. |
| `DispatchCopy` definition at line 2561, CYC=8 | Lines 2561–2661 | YES | 4 gate guards + foreach + ShouldSkipFollower if + dispatched>0 if = CYC=8 confirmed. |
| Lane D wiring point between lines 2629–2631 | Lines 2629–2633 | YES | `ResolveBaseQty` at line 2629, B119 snapshot comment at line 2631 confirmed. Gap is real. |
| `DispatchToFollower` at line 2760, Named→`SendCopyWithAtm`, else→`SendCopy` | Lines 2760–2799 | YES | Exact — lines 2795–2798 confirmed. |
| `SendCopyWithAtm` at line 5123, STATIC calls at lines 5149/5151, CYC=4 | Lines 5123–5167 | YES | Exact. |
| `ReplaceFollowerCopyOnAtmCancel` at line 4502 | Lines 4502–4524 | YES | Named→`SendCopyWithAtm` at line 4522, else→`SendCopy` at line 4524 confirmed. |
| `SendCopy` uses `"PTT-Copy"` signal name | Line 5065 | YES | `string signalName = "PTT-Copy"` confirmed. |
| `AtmStrategyCreate` — zero occurrences | grep | YES | Zero occurrences confirmed in `CopyEngine.cs`. |
| `AtmStrategyChangeStopTarget` — zero occurrences | grep | YES | Zero occurrences confirmed in `CopyEngine.cs`. |

**Source investigation quality: HIGH.** All material claims are directly traceable to verified source lines. The stale jcodemunch index caveat (§7.5) is appropriately acknowledged.

---

## Design Correctness

### Finding: OnOrderUpdate is the correct single entry point — CORRECT

The reasoning in plan §3 is architecturally sound:

1. **No pre-OnOrderUpdate hook with valid Order object exists in NT8 AddOnBase.** The Order object and its final properties (OrderId, OrderState = Submitted/Accepted) only exist after NT8 processes the submission. A Push path before this event is impossible in the AddOn model.

2. **Initialized state rejection is intentional.** `IsDispatchTriggerState` rejects `Initialized` to avoid premature dispatch. The plan correctly identifies why this gate exists.

3. **Single call site.** `DispatchCopy` is called exactly once in the entire file (line 1729). No Push API exists. The plan's grep assertion ("zero matches" for Push/PushEntry/etc.) supports this.

4. **Gate chain is complete and deterministic.** All 5 gates (0.5, 3, 4, 5 + dedup commit) are correctly described and source-verified.

### Finding: Lane D wiring point — CORRECT

- Location (after `ResolveBaseQty` line 2629, before B119 snapshot line 2631) is accurate and source-verified.
- CYC analysis is correct: unconditional void call adds 0 branches to `DispatchCopy` (currently at CYC limit of 8).
- Threading observation is correct: NT8 account background thread, no Dispatcher needed for sync hook.
- "Fires once per leader order, not N times per follower" — correct, wiring point is before the follower loop.

### Stub hook design — CORRECT and MINIMAL

The plan proposes `TryLaneDHook(Order order, CopyRule rule, CopySignal baseSignal, int baseQty)` with empty body (CYC=1). This is architecturally minimal: zero behavior change, zero CYC impact on caller, establishes the wiring contract without prejudging what Lane D will do.

**Special check — "too small" criterion:** The plan correctly concludes no behavior-change code is needed in Lane C. The only deliverable is the wiring stub + documentation. This IS a valid minimal change satisfying the spec goal (identify wiring point, confirm OnOrderUpdate is correct, establish Lane D hook). It does NOT require REVIEW_FAIL for being "too small."

---

## NT8 / Jane Street Compliance

| Rule | Plan Claim | Source-Verified | Result |
|------|------------|-----------------|--------|
| **JS-021 (P0)**: No lock/Monitor/Mutex/SemaphoreSlim | All shared state uses `ConcurrentDictionary` | YES — `_cloneAtmObjectByInstr` line 151, `_cloneAtmCacheByInstr` line 145 confirmed as `ConcurrentDictionary` | PASS |
| **JS-001 (P0)**: No throw in dispatch/gate/event-handler | `SendCopyWithAtm`, `SendCopy`, `ReplaceFollowerCopyOnAtmCancel`, `DispatchCopy` all try/catch, return false | YES — catch blocks at lines 5162–5166 and 5108–5112 confirmed | PASS |
| **JS-002 (P0)**: No unexpected null return | `GetCloneAtmMode` returns `Inherit` as fallback; `ResolveAtmMode` never null | YES — line 762 (`return new FollowerAtmMode.Inherit()`) confirmed | PASS |
| **CYC ≤ 8 (P1)**: All proposed methods | `DispatchCopy` CYC=8 (no change); `TryLaneDHook` CYC=1; `SendCopyWithAtm` CYC=4 | YES — branch count confirmed | PASS |
| **ASCII-only**: No Unicode in strings/comments | Asserted for all reviewed methods | No non-ASCII observed in reviewed lines | PASS |
| **No async/await in NT8 lifecycle/event handlers** | `OnOrderUpdate` and all dispatch-chain methods synchronous | YES — no async keyword in any reviewed method | PASS |
| **NT8: `StartAtmStrategy` STATIC** | Lines 5149, 5151: `NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(...)` | YES — source-verified exact form | PASS |
| **NT8: `AtmStrategyCreate` banned** | Zero occurrences | YES — grep confirmed | PASS |
| **NT8: `AtmStrategyChangeStopTarget` banned** | Zero occurrences | YES — grep confirmed | PASS |
| **NT8: Order name `"Entry"` for ATM arming** | Line 5142 hardcodes `"Entry"` | YES — source-verified | PASS |
| **NT8: `"PTT-Copy"` prefix on `SendCopy` orders** | Line 5065: `signalName = "PTT-Copy"` | YES — source-verified | PASS |
| **No `DateTime.Now`** | `DateTime.MaxValue` used in `CreateOrder` args | YES — lines 5143 and 5098 confirmed as `DateTime.MaxValue` | PASS |

**All NT8 / Jane Street checks: PASS.**

---

## Ticket Coverage

| Spec Goal | Addressed in Plan | Ticket |
|-----------|-------------------|--------|
| 1. Identify where `DispatchCopy` / `SendCopy` / `SendCopyWithAtm` is called for entry fills | YES — §1.6–§1.8 map the full dispatch chain | T1 (documentation) |
| 2. Determine whether a Push path exists ahead of `OnOrderUpdate` | YES — §3 reasoning + grep evidence | T1 (documentation) |
| 3. Design minimal change for Push entry copy — OR document `OnOrderUpdate` is correct and propose Lane D wiring | YES — §3 concludes OnOrderUpdate is correct; §4 specifies wiring point | T1 (code: stub hook) |
| 4. Clearly identify Lane D wiring point (exact method, file, line) | YES — §4 table: `DispatchCopy`, `CopyEngine.cs`, between lines 2629–2631 | T1 (code: stub hook) |

Single ticket (T1) is appropriate for this scope. No missing spec requirements.

**7-scan checklist in ticket:** Present in plan §6. All 7 scans addressed with explicit pass criteria.

**Ticket coverage: PASS.**

---

## Violations

**None.**

No JS-XXX violations identified. No NT8 banned API violations. No CYC overages in proposed methods. No lock(), no throw in dispatch chain, no async/await, no null return issues.

---

## Recommendations (Non-Blocking)

The following items are factual inaccuracies or precision issues in the plan. They do not trigger REVIEW_FAIL but should be corrected by the engineer before the plan is finalized to prevent confusion during implementation.

**REC-01 — Q1 arithmetic error in Lane-Split Gate section:**
Plan §2 states the two components are "within 88 lines of each other." The actual line distance is `2561 - 1642 = 919 lines`. The "88" figure appears to be fabricated or transposed. The Q1=YES conclusion is defensible on other grounds (same dispatch chain, same file, same ticket scope), but the stated arithmetic is wrong. Correct the Q1 rationale to remove the "88 lines" claim. This does not affect the gate outcome since Q2=YES independently drives SINGLE-PIPELINE.

**REC-02 — "Two total references" claim for `DispatchCopy` is imprecise:**
Plan §1.7 states "`DispatchCopy` has exactly two total references in the file." A grep shows 5 matches (lines 1729, 2425, 2561, 2677, 5116) — four are in comments, one is the definition, one is the sole call site. The substantive claim (ONE live call site at line 1729) is accurate; the "two total references" phrasing is wrong. Rephrase to: "one call site (line 1729) and one definition (line 2561); all other occurrences are in comments."

**REC-03 — Stale comment at `SendCopy` line 5052:**
Source code line 5052 contains a stale comment: "For Named mode the ATM template name is passed as the final 'atm' parameter of CreateOrder." This is incorrect — Named mode never reaches `SendCopy` in the current design (`DispatchToFollower` routes Named to `SendCopyWithAtm`). This is a pre-existing code comment issue, not introduced by the plan, and is out of scope for Lane C. Flag to the engineer as a minor doc-debt item to clean up when the file is next modified.

---

*Reviewer: ptt-plan-reviewer | Epic: PTT-BUGS-LIVE-0911 | Lane: C | Cycle: 2*
*Return: REVIEW_PASS*
