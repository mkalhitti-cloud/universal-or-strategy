# B198 Plan Review
**Author**: ptt-plan-reviewer (Phase 2)
**Date**: 2026-10-13
**Epic**: B198 -- SimStop N-bracket generalisation + QTY instrument + fo=NULL drain + ATM dir fix
**Plan under review**: docs/brain/B198/02-architecture-plan.md
**Spec source**: docs/brain/B198-orchestrator-prompt.md
**Diagnosis source**: docs/brain/B198-diagnosis.md
**Rules source**: docs/standards/jane-street/RULES_CATALOG.md
**Prior backlog**: docs/brain/B197/06-deferred-backlog.md (READ ONLY)

---

## VERDICT: REVIEW_FAIL

**Violation count: 2**

Both violations are P0 SPEC COMPLETENESS failures per the reviewer role DNA:
> "Any spec requirement not addressed in the plan = FAIL"

The violations are targeted and correctable. The plan is otherwise architecturally sound. REVIEW_FAIL does not invalidate the overall T1/T2/T4 design — only T3 and the test table require revision.

---

## Violations

### VIO-1 | SPEC COMPLETENESS (P0) | T3 design does not satisfy the fo=NULL fix requirement | **Required fix: T3 must wire the follower order reference into SyncFollowerBracket**

**Rule citation**: Role DNA — "Any spec requirement not addressed in the plan = FAIL"

**Spec requirement** (docs/brain/B198-orchestrator-prompt.md, TICKET 3):

> In `PendingDispatchDrain` struct, add a nullable `Order FollowerOrder` field.
> In `SubmitDrainedEntry`, after `SendCopyWithAtm` returns, store the created order in the drain payload.
> **When `TrySetSlateReadyOnDrainFill` resolves the drain, pass the stored `FollowerOrder` to `SyncFollowerBracket` instead of null.**

**What the plan designs (§5)**:

The plan replaces the struct-field approach with a class-level `ConcurrentDictionary<string, Order> _drainFollowerEntryOrders`. The dict is populated in `SubmitDrainedEntry` and cleaned up in `TrySetSlateReadyOnDrainFill` / `AbortDrainOnFill` / `TryDrainWatchdog`.

**However**, the plan explicitly states at §5 end:

> "The primary fo=NULL fix for the SimStop SIM test case is T1 (predicate fixes). T3 provides the order reference infrastructure for robustness and future use."

And the plan's SIM-3 pass condition (§9) states:

> "All SyncFollowerBracket calls have `fo=<order>` (not fo=NULL)."

**The plan's T3 design does not achieve its own SIM-3 PASS condition.** The `_drainFollowerEntryOrders` dict stores the order reference but nothing in the plan's T3 design reads it back and passes it to `SyncFollowerBracket`. The plan's SIM-3 pass condition can only be satisfied if: (a) T1 predicates fix all instances of fo=NULL (which is the case when SimStop prefix recognition is the only cause), OR (b) the order reference is actually threaded into `SyncFollowerBracket`.

If (a) is sufficient, then T3 is purely infrastructure and its SIM-3 claim is misleading. If (b) is required by the spec (and it is — the spec explicitly says "pass the stored FollowerOrder to SyncFollowerBracket instead of null"), then the T3 design is incomplete.

**The spec is unambiguous**: the fo=NULL fix requires the stored order to be passed to `SyncFollowerBracket`. The plan defers this wiring. This is a spec requirement not addressed.

**Also noted**: The spec says add a field to `PendingDispatchDrain` **struct**. The plan uses a class-level dict instead. The class-level dict is architecturally defensible (avoids mutable struct field which would be a JS-008 violation), but the plan must explicitly acknowledge this design divergence and confirm that the wiring into `SyncFollowerBracket` is complete — not deferred.

**Required fix**: T3 design must include the wiring step: after `TrySetSlateReadyOnDrainFill` confirms the drain fill, retrieve the stored `Order` from `_drainFollowerEntryOrders` and pass it as the `fo` argument to `SyncFollowerBracket`. This is the spec-required "pass the stored FollowerOrder to SyncFollowerBracket instead of null" step. The §5 design must include:

```
Change 3 (revised): TrySetSlateReadyOnDrainFill — retrieve and pass fo
After TrySetSlateReadyOnDrainFill confirms the drain fill (existing slate-ready logic),
retrieve the order reference:
    _drainFollowerEntryOrders.TryGetValue(e.Order.Account.Name, out Order fo);
    _drainFollowerEntryOrders.TryRemove(e.Order.Account.Name, out _);
Then call SyncFollowerBracket(..., fo) with the retrieved order (which may be null if
SendCopyWithAtm failed, in which case fo=NULL log is still correct).
```

The CYC impact of adding a `TryGetValue` + pass-through to an existing call is 0 (TryGetValue is not a new branch; the existing SyncFollowerBracket call already happens). Plan §7 must be updated with this change.

**The use of a class-level ConcurrentDictionary instead of a struct field is acceptable** provided it avoids the JS-008 mutable-struct violation. The plan should document this design decision explicitly.

---

### VIO-2 | SPEC COMPLETENESS (P0) | T1 test plan missing required IsBracketLeg / IsBracketLegStatic / IsAtmSTPOrder / IsStopLeg test cases | **Required fix: add 4 test cases to T1 test table**

**Rule citation**: Role DNA — "Any spec requirement not addressed in the plan = FAIL"

**Spec requirement** (docs/brain/B198-orchestrator-prompt.md, TICKET 1, Tests section):

> Minimum test cases:
> ...
> 12. `IsBracketLeg/IsBracketLegStatic/IsAtmSTPOrder/IsStopLeg — same pattern from B197Tests.cs.`

**What the plan designs (§3 T1 Test Cases)**:

The plan includes T1-01 through T1-12. These cover:
- T1-01..T1-03: IsAtmBracketName ✅
- T1-04..T1-05: IsNativeExitName ✅
- T1-06..T1-12: TryParseStopSuffix and MaxBracketLegs ✅

**Missing**: No test cases for `IsBracketLeg`, `IsBracketLegStatic`, `IsAtmSTPOrder`, or `IsStopLeg` with SimStop input. The spec mandates these as minimum test cases #12 (the last required item, "same pattern from B197Tests.cs").

**Required fix**: Add 4 test cases to the T1 test table (or note them as a single grouped entry):

| # | Test name | Assertion |
|---|-----------|-----------|
| T1-13 | `IsBracketLeg_SimStop1_ReturnsTrue` | source-text Assert.Contains `StartsWith("SimStop", StringComparison.Ordinal)` in IsBracketLeg body |
| T1-14 | `IsBracketLegStatic_SimStop1_ReturnsTrue` | source-text Assert.Contains `StartsWith("SimStop", StringComparison.Ordinal)` in IsBracketLegStatic body |
| T1-15 | `IsAtmSTPOrder_SimStop1_ReturnsTrue` | source-text Assert.Contains `StartsWith("SimStop", StringComparison.Ordinal)` in IsAtmSTPOrder body |
| T1-16 | `IsStopLeg_SimStop1_ReturnsTrue` | source-text Assert.Contains `StartsWith("SimStop", StringComparison.Ordinal)` in IsStopLeg body |

These use the same source-text assertion pattern established in B197Tests.cs (TypeInitializationException constraint). They add negligible complexity to the engineer's work — the source-text assertions are one-liner file reads.

---

## Full Checklist Evaluation

### LANE-SPLIT GATE COMPLIANCE

| Check | Result |
|-------|--------|
| "LANE-SPLIT GATE RESULT:" line present | ✅ PASS |
| SINGLE-PIPELINE rationale stated | ✅ PASS (Q1: all 4 tickets touch CopyEngine.cs = single pipeline) |
| Q2-Q4 not answered (Q1 terminates gate) | NOTE: Q1 unambiguously terminates the decision; absence of Q2-Q4 is acceptable |

**Gate result: PASS**

---

### SPEC COMPLIANCE

| Requirement | Addressed? | Plan Section | Notes |
|-------------|-----------|--------------|-------|
| T1: IsNativeExitName predicate fix | ✅ | §3 Part A | Correct. |
| T1: IsAtmBracketName predicate fix | ✅ | §3 Part A | Correct. char.IsDigit(name[7]) guard explained. |
| T1: IsBracketLeg predicate fix | ✅ | §3 Part A | Correct. |
| T1: IsBracketLegStatic predicate fix | ✅ | §3 Part A | Correct. |
| T1: IsAtmSTPOrder predicate fix | ✅ | §3 Part A | Correct. |
| T1: IsStopLeg predicate fix | ✅ | §3 Part A | Correct (null guard included). |
| T1 Part B: TryParseStopSuffix (B1) | ✅ | §3 B1 | Replacement provided. n>3 cap removed. CYC 3→5. |
| T1 Part B: CaptureOtherLegTargetPrices + ResubmitCollateralLegs (B2) | ✅ | §3 B2 | MaxBracketLegs=9 const. Both sites fixed together. StartsWith("Stop") guard fix included. |
| T1 Part B: FindLeaderCollateralOrder (B3) | ✅ | §3 B3 | SimStop lookup added. CYC 3→3. |
| T1 Part B: CaptureLinkedTargetPrice (B4) | ✅ | §3 B1 | Auto-covered by B1 fix confirmed. |
| T1 B5 (NEWLY DISCOVERED): FindLeaderStopPrice | ✅ | §3 B5 | Correctly identified and fixed. |
| Director requirement: ANY N brackets | ✅ | §3 B1+B2 | MaxBracketLegs=9, n>3 cap removed. |
| T2 split (T2a instrument + T2b fix) | ✅ | §4 | Justification sound. H1/H2/H3 paths covered. |
| T3 fo=NULL drain path: struct field | ⚠️ | §5 | Plan uses class dict instead of struct field. Design divergence acknowledged but wiring step missing. **VIO-1** |
| T3 fo=NULL drain path: threading order ref through SubmitDrainedEntry | ✅ | §5 | Dict population in SubmitDrainedEntry. |
| T3 fo=NULL drain path: SyncFollowerBracket wiring | ❌ | §5 | Order not passed to SyncFollowerBracket. **VIO-1** |
| T4 ATM dir path corrected | ✅ | §6 | "AtmStrategy" subpath. |
| T4 warning logs | ✅ | §6 | [PTT-STARTUP-WARN] on dir-not-found. Reverse=true already covered. |
| Pre-confirmed facts embedded (no re-investigation) | ✅ | §2 | Diagnosis summary correct. |
| DW-B194-04 NOT reopened | ✅ | §2 + §10 | Correctly noted as already implemented. |

---

### JANE STREET COMPLIANCE

| Rule | Check | Result |
|------|-------|--------|
| JS-021: No lock() | No new lock() in any field or method design. ConcurrentDictionary used. | ✅ PASS |
| JS-033: No async void | No new async methods introduced. | ✅ PASS |
| JS-001: No throw in hot path | No throws in any T1-T4 method designs. | ✅ PASS |
| JS-002: No return null for Option-typed returns | SendCopyWithAtm changes to `Order?` return type. Returns null on failure. Project uses nullable Order returns throughout (NT8 established pattern). This is the project's documented null-as-absence signal. Not a new anti-pattern. | ✅ PASS (existing contract extension) |
| JS-066: All new/modified method CYC ≤ 8 | See §7 table. Max after: TryParseStopSuffix CYC=5, CheckAtmTemplatesForReverse(string) T2b H2 CYC=7, SubmitDrainedEntry CYC=6. | ✅ PASS |
| JS-008: Mutable struct fields | Plan avoids modifying PendingDispatchDrain struct (uses class dict instead). JS-008 not triggered. | ✅ PASS |
| JS-009: Dictionary for shared collection | New fields use ConcurrentDictionary, not Dictionary. | ✅ PASS |
| JS-051/JS-052: xUnit [Fact] only | Tests use [Fact] from xUnit. No NUnit/MSTest. | ✅ PASS |
| ASCII-only string literals | All log strings and identifiers in plan use ASCII only. No Unicode, emoji, curly quotes. | ✅ PASS |

---

### PLAN STRUCTURE

| Section | Present? | Notes |
|---------|----------|-------|
| §1 Executive Summary (ticket count = 5) | ✅ | 5 tickets stated: T1, T2a, T2b, T3, T4. |
| §2 Pre-pipeline diagnosis summary | ✅ | All 5 key diagnosis points embedded. |
| §3 T1 design (Part A table + Part B B1-B5) | ✅ | 6 predicates + 5 Part B items + B5 newly discovered. |
| §4 T2a/T2b design (log instrumentation + fix hypothesis) | ✅ | H1/H2/H3 paths. ConcurrentDictionary for T2b H2. |
| §5 T3 drain path design | ⚠️ | Present but spec wiring incomplete. VIO-1. |
| §6 T4 ATM dir design | ✅ | Path fix + warning log + Reverse=true coverage. |
| §7 CYC Impact Summary table | ✅ | 20 rows. All before/after. JS-066 compliance column. |
| §8 7-scan checklist | ✅ | All 7 scans with PowerShell commands. |
| §9 SIM gate plan (4 scenarios) | ✅ | SIM-1 through SIM-4 match orchestrator spec. |
| §10 Deferred Work Register | ✅ | 15 B197 items carried forward. New B198 DW items. DW-B194-04 not reopened. |

---

### NEWLY DISCOVERED SITES

| Site | Plan coverage | Result |
|------|--------------|--------|
| B5: FindLeaderStopPrice — "Stop"+bracketIndex | §3 B5. Fix designed. CYC 6→6. | ✅ PASS |
| B2-extra: CaptureOtherLegTargetPrices StartsWith("Stop") guard | §3 B2. Guard change included with MaxBracketLegs expansion. | ✅ PASS |
| No additional grep hits beyond known list + B5 | Appendix confirms: all 7 grep patterns run, no new sites found beyond B5. | ✅ PASS |

---

### DW CARD INTEGRITY

| Check | Result |
|-------|--------|
| DW-B194-04 NOT reopened | ✅ PASS — §2 explicitly states "already implemented" |
| DW-B197-SIM-01 carried forward unchanged | ✅ PASS — present in §10 P1 table |
| All 15 B197 carry-forward items present in §10 | ✅ PASS — all 15 items confirmed: DW-B183-03, DW-B167-DEFER-03, DW-B167-DEFER-04, DW-B170-SIM, DW-B197-SIM-01, DW-B192-SINST-01, DW-B159-K2, DW-B159-K3, DW-B159-K4, DW-B167-DEFER-01, DW-B197-03, DW-B193-01, DW-B197-01, DW-B197-02, DW-B197-04 |

---

## Summary: Required Fixes Before REVIEW_PASS

The architect must revise `docs/brain/B198/02-architecture-plan.md` to address:

### Fix 1 (VIO-1): T3 §5 — add SyncFollowerBracket wiring step

Add the following as **Change 3 (revised)** in §5:

```
After TrySetSlateReadyOnDrainFill confirms the drain fill and marks slate-ready:
  (a) Retrieve the stored order: _drainFollowerEntryOrders.TryGetValue(acctKey, out Order drainFo)
  (b) Remove from dict: _drainFollowerEntryOrders.TryRemove(acctKey, out _)
  (c) Pass drainFo (may be null if SendCopyWithAtm failed) to the existing SyncFollowerBracket call
      as the `fo` argument, replacing the current null.
CYC impact: TryGetValue + TryRemove = 0 new branches. CYC: 5 → 5 (unchanged).
```

Also update §5 "Why this solves fo=NULL" to accurately state:
- T1 fixes SimStop prefix recognition (the gate-level cause).
- T3 additionally passes the stored order to SyncFollowerBracket, satisfying the timing-race case where SyncFollowerBracket is called before NT8 places the ATM bracket orders.

Also update §9 SIM-3 to confirm the PASS condition is achievable from T3 design (not only from T1).

Also update §7 CYC Impact Summary to confirm TrySetSlateReadyOnDrainFill CYC stays at 5 with TryGetValue (no new branch).

Optionally: explicitly document why the class-level ConcurrentDictionary is used instead of a `PendingDispatchDrain` struct field (to avoid JS-008 mutable struct violation). This is recommended for reviewer traceability.

### Fix 2 (VIO-2): T1 §3 — add 4 missing test cases to test table

Add T1-13 through T1-16 (IsBracketLeg, IsBracketLegStatic, IsAtmSTPOrder, IsStopLeg with SimStop1 input, using source-text assertion pattern from B197Tests.cs).

---

## Non-Violations Noted (Informational)

- **LANE-SPLIT GATE Q2-Q4**: The plan answers only Q1 (same file). Q1 terminates the gate unambiguously. Non-blocking — noted only for completeness.
- **T3 struct vs dict**: The plan's choice of ConcurrentDictionary over struct field is an architectural improvement (avoids JS-008 mutable struct). The divergence from the spec's "struct field" phrasing is acceptable and defensible. The wiring step is the only mandatory addition.
- **SIM-3 pass condition vs T1 sufficiency**: The plan's claim that T1 is the "primary fo=NULL fix" may be correct for the current SIM scenario. The SIM-3 condition should be written to reflect both T1 and T3 contributions rather than implying T3 alone achieves it.

---

## Maximum Revision Cycles

Per pipeline protocol: REVIEW_FAIL sends plan back to ptt-architect (max 2 cycles). This is cycle 1.

---

*Written by: ptt-plan-reviewer*
*Epic: B198*
*Phase: 2 (Plan Review)*
*Returns: REVIEW_FAIL*

---

## Cycle 2 Review

**Date**: 2026-10-13
**Reviewer**: ptt-plan-reviewer (Phase 2 — Cycle 2, final cycle)
**Plan revision**: Revision 1 (post REVIEW_FAIL cycle 1)
**Plan footer confirms**: "Fixes: VIO-1 (T3 SyncFollowerBracket wiring), VIO-2 (T1-13..T1-16 test cases)"

---

### VIO-1 RESOLUTION CHECK

**Required fix**: `TrySetSlateReadyOnDrainFill` must include TryGetValue + TryRemove from `_drainFollowerEntryOrders` and pass `drainFo` to `SyncFollowerBracket` instead of null.

**Plan §5 Change 3 (lines 365-381)**:

| Sub-check | Expected | Found | Result |
|-----------|----------|-------|--------|
| `_drainFollowerEntryOrders.TryGetValue(acctKey, out Order drainFo)` present | Yes | Yes — verbatim at line 374 of plan | ✅ |
| `_drainFollowerEntryOrders.TryRemove(acctKey, out _)` present | Yes | Yes — verbatim at line 375 of plan | ✅ |
| `SyncFollowerBracket(acc, drainFo, ...)` call passes drainFo | Yes | Yes — "pass drainFo instead of null" at line 377 of plan | ✅ |
| CYC delta = 0 for TryGetValue + TryRemove | Yes | Yes — plan explicitly states "TryGetValue + TryRemove are not branching constructs... Zero new McCabe branches" | ✅ |
| §7 CYC table `TrySetSlateReadyOnDrainFill` row: Before=5, After=5 | Yes | Yes — row present at plan §7 | ✅ |
| Design note documents ConcurrentDictionary vs struct field (JS-008 rationale) | Recommended | Yes — §5 "Design note" paragraph explicitly states JS-008 rationale | ✅ |
| §9 SIM-3 updated to reference T1 + T3 contributions | Yes | Yes — SIM-3 step 4 says "T1 enables this by ensuring... T3 enables this for the timing-race window..." | ✅ |
| T3-04 test case validates the wiring | Yes | Yes — `TrySetSlateReadyOnDrainFill_RetrievesAndPassesDrainFo` asserts `_drainFollowerEntryOrders.TryGetValue` in source text | ✅ |

**VIO-1 STATUS: RESOLVED** ✅

---

### VIO-2 RESOLUTION CHECK

**Required fix**: Add T1-13 through T1-16 (IsBracketLeg, IsBracketLegStatic, IsAtmSTPOrder, IsStopLeg with SimStop input, source-text assertion pattern from B197Tests.cs).

**Plan §3 T1 Test Cases table (rows T1-13..T1-16)**:

| # | Test name in plan | Pattern | Result |
|---|-------------------|---------|--------|
| T1-13 | `IsBracketLeg_SimStop1_ReturnsTrue` | source-text Assert.Contains `StartsWith("SimStop", StringComparison.Ordinal)` in `IsBracketLeg` body | ✅ |
| T1-14 | `IsBracketLegStatic_SimStop1_ReturnsTrue` | source-text Assert.Contains `StartsWith("SimStop", StringComparison.Ordinal)` in `IsBracketLegStatic` body | ✅ |
| T1-15 | `IsAtmSTPOrder_SimStop1_ReturnsTrue` | source-text Assert.Contains `StartsWith("SimStop", StringComparison.Ordinal)` in `IsAtmSTPOrder` body | ✅ |
| T1-16 | `IsStopLeg_SimStop1_ReturnsTrue` | source-text Assert.Contains `StartsWith("SimStop", StringComparison.Ordinal)` in `IsStopLeg` body | ✅ |

Testability note (plan lines 254) correctly explains why source-text assertions are used for private/instance methods (TypeInitializationException constraint) — consistent with B197Tests.cs pattern cited by the spec.

**VIO-2 STATUS: RESOLVED** ✅

---

### FULL RE-SCAN (Prior PASS items re-confirmed)

| Check | Prior | Cycle 2 | Notes |
|-------|-------|---------|-------|
| LANE-SPLIT GATE present and valid | PASS | ✅ PASS | "LANE-SPLIT GATE RESULT: SINGLE-PIPELINE" — §20-31 of plan |
| All 6 Part A predicates in T1 scope | PASS | ✅ PASS | §3 Part A table: IsNativeExitName, IsAtmBracketName, IsBracketLeg, IsBracketLegStatic, IsAtmSTPOrder, IsStopLeg — all 6 present with correct OR clauses |
| Part B items B1-B5 designed | PASS | ✅ PASS | B1 (TryParseStopSuffix replacement), B2 (MaxBracketLegs+SimStop guard), B3 (SimStop+suffix lookup), B4 (auto-covered by B1 confirmed), B5 (FindLeaderStopPrice newly discovered and fixed) |
| T2a/T2b split justified | PASS | ✅ PASS | §4: rationale stated (H2 fix requires XML parse or new UI field — crosses one-ticket-one-concern boundary) |
| T3 fo=NULL full fix: ConcurrentDictionary (no struct field — JS-008 compliant) | PASS | ✅ PASS | §5 new field `_drainFollowerEntryOrders` declared as `private readonly ConcurrentDictionary<string, Order>` |
| T3 fo=NULL: SendCopyWithAtm return type Order? | PASS | ✅ PASS | §5 Change 1: return type changed from bool to Order?. All 6 call sites enumerated. |
| T3 fo=NULL: SubmitDrainedEntry stores Order (CYC 5→6) | PASS | ✅ PASS | §5 Change 2: `_drainFollowerEntryOrders.TryAdd(acctKey, newOrder)` after null check |
| T3 fo=NULL: TrySetSlateReadyOnDrainFill retrieves + passes drainFo | ❌ VIO-1 → ✅ FIXED | ✅ PASS | §5 Change 3: TryGetValue + TryRemove + SyncFollowerBracket(drainFo) — all present |
| T3 fo=NULL: AbortDrainOnFill cleanup | PASS | ✅ PASS | §5 Change 4: TryRemove |
| T3 fo=NULL: TryDrainWatchdog cleanup | PASS | ✅ PASS | §5 Change 5: TryRemove in watchdog expiry block |
| T4 ATM dir corrected ("AtmStrategy" subpath) | PASS | ✅ PASS | §6: `Path.Combine(..., "templates", "AtmStrategy")` |
| T4 warning logs: dir-not-found + Reverse=true | PASS | ✅ PASS | §6: [PTT-STARTUP-WARN] with path. Existing Reverse=true warning preserved. |
| §7 CYC table complete (all touched methods) | PASS | ✅ PASS | 20 rows covering all B198 modified methods. Max CYC after = 7 (T2b H2 path). All ≤8. |
| §8 7-scan checklist present | PASS | ✅ PASS | SCAN-01 through SCAN-07 with PowerShell commands |
| §9 SIM gate 4 scenarios | PASS | ✅ PASS | SIM-1 through SIM-4. SIM-3 now correctly attributes PASS to T1+T3 both. |
| §10 Deferred Work Register: 15 B197 carry-forwards | PASS | ✅ PASS | All 15 items confirmed: DW-B183-03, DW-B167-DEFER-03, DW-B167-DEFER-04, DW-B170-SIM, DW-B197-SIM-01, DW-B192-SINST-01, DW-B159-K2, DW-B159-K3, DW-B159-K4, DW-B167-DEFER-01, DW-B197-03, DW-B193-01, DW-B197-01, DW-B197-02, DW-B197-04 |
| DW-B194-04 NOT reopened | PASS | ✅ PASS | §2 states "already done"; §10 does not list it |
| No lock() | PASS | ✅ PASS | All new collections are ConcurrentDictionary. No lock() anywhere in plan. |
| No async void | PASS | ✅ PASS | No new async methods introduced. |
| No return null (hot-path) | PASS | ✅ PASS | SendCopyWithAtm returns null on failure — established project null-as-absence contract, exempt per prior cycle reasoning |
| ASCII-only string literals | PASS | ✅ PASS | All log strings and identifiers in plan are ASCII-only |
| Pre-confirmed facts embedded (no re-investigation) | PASS | ✅ PASS | §2 diagnosis summary: all 5 key facts stated without re-investigation |

**Total re-scan checks: 23 of 23 PASS. Zero new violations.**

---

### Jane Street DNA Final Verification

| Rule | Check | Result |
|------|-------|--------|
| JS-021: No lock() | No lock() in any field or method design. ConcurrentDictionary for all shared state. | ✅ PASS |
| JS-033: No async void | No new async methods introduced. | ✅ PASS |
| JS-001: No throw in hot path | No throws in any T1-T4 method designs. | ✅ PASS |
| JS-002: No return null | SendCopyWithAtm null-on-failure is the project's established contract. | ✅ PASS |
| JS-066: All CYC ≤ 8 | Max after = 7 (T2b H2 CheckAtmTemplatesForReverse). All 20 rows ≤ 8. | ✅ PASS |
| JS-008: Mutable struct fields | Struct field approach explicitly avoided. ConcurrentDictionary at class level. | ✅ PASS |
| JS-009: Dictionary for shared collection | All new shared fields use ConcurrentDictionary. | ✅ PASS |
| JS-051/JS-052: xUnit [Fact] only | Testability note confirms xUnit [Fact]. Source-text pattern per B197Tests.cs. | ✅ PASS |
| ASCII-only | All C# string literals and log messages in plan are ASCII-only. | ✅ PASS |

---

### Cycle 2 Verdict

**VIOLATION COUNT: 0**

Both prior violations are fully resolved:
- VIO-1 (T3 SyncFollowerBracket wiring): ✅ RESOLVED — Change 3 in §5 now contains the mandatory TryGetValue + TryRemove + drainFo pass-through. CYC stays at 5. §7 updated. §9 SIM-3 updated.
- VIO-2 (T1-13..T1-16 missing tests): ✅ RESOLVED — All 4 test cases present in §3 T1 test table with correct source-text assertion pattern.

All 23 prior PASS items re-confirmed. Zero new violations introduced.

## VERDICT: REVIEW_PASS

*Written by: ptt-plan-reviewer*
*Epic: B198*
*Phase: 2 (Plan Review — Cycle 2)*
*Returns: REVIEW_PASS*
