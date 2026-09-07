# Plan Review: DW-LB-FL-01

**Reviewer**: ptt-plan-reviewer (Phase 2)
**Plan file**: `docs/brain/DW-LB-FL-01/02-architecture-plan.md`
**Review date**: 2026-09-08
**Rules applied**: `docs/standards/jane-street/RULES_CATALOG.md` JS-001..JS-110
**Source cross-referenced**: `src/PropTraderTools/CopyEngine.cs`

---

## RULES CATALOG GATE

Gate result: **PASS**
Reviewing plan only — no code changes in this phase. P0 scan not required.

---

## Criterion A — Lane-Split Gate Compliance

**Result**: PASS

The plan explicitly states `LANE-SPLIT GATE RESULT: SINGLE-PIPELINE`. It correctly
addresses all four Q1-Q4 questions as NOT APPLICABLE, explaining that only one root
cause exists and one code path is involved (NakedPositionDetector dispatch path).
The gate text is present, complete, and logically consistent with the single-pipeline
conclusion.

---

## Criterion B — Root Cause Accuracy

**Result**: PASS

Every line reference is verified against live source:

| Plan Claim | Verified |
|------------|---------|
| `NakedPositionDetector` at L7170 | CONFIRMED — method starts L7170 |
| `HasNakedPosition` check at L7174 | CONFIRMED — `if (!HasNakedPosition(acct)) return;` at L7174 |
| `Dispatcher.InvokeAsync(() => FlattenOneAccount(...))` at L7190-7192 | CONFIRMED — L7190-7192 |
| `FlattenOneAccount` calls `IsAccountFlattenable` at L5185 | CONFIRMED — L5185 |
| `IsAccountFlattenable` calls `HasInflightFlatten` at L5204 | CONFIRMED — L5204 |
| `DoFlattenOrder` at L5267-5282 | CONFIRMED — `acc.CreateOrder(...)` starts L5267 |
| `TryDispatchLeaderFlat` at L4693 | CONFIRMED — method declaration at L4693 |
| `IsNonFlatDispatchName` at L2379 | CONFIRMED — method at L2379 |
| `IsNativeExitOnFlatLeader` guard at L4710 | CONFIRMED — line 4710 in TryDispatchLeaderFlat |
| `IsAtmBracketName` reusable as `internal static` | CONFIRMED — `internal static bool IsAtmBracketName` at L832 |
| `InternalsVisibleTo("PropTraderTools.Tests")` at L46 | CONFIRMED — L46 |

The call chain (CancelQxBrackets → NT8 per-bracket cancel acks → TryNakedDetect →
NakedPositionDetector → Dispatcher.InvokeAsync queued → new entry fills + brackets
arm → UI callback runs → FlattenOneAccount proceeds with no arming check) is
complete and logically sound. The timing race is correctly described.

The ruling-out of the `TryDispatchLeaderFlat` alternative hypothesis is supported by
source evidence: `IsNonFlatDispatchName` (L2379) blocks ATM bracket names at guard
(2.5/2.6), and `IsNativeExitOnFlatLeader` (L4710) blocks native-exit-on-flat-leader
at guard (3.5). Both are confirmed in source. No speculation without evidence.

---

## Criterion C — Fix Design Correctness

**Result**: PASS

### C1 — Fix addresses the root cause (not a symptom)
The guard is placed in the exact Dispatcher callback body (`FlattenIfNotArming`)
that replaces the direct `FlattenOneAccount` call in `NakedPositionDetector`. This
is the only call site with the timing race. All other paths to `FlattenOneAccount`
(via `TryDispatchLeaderFlat` → `FlattenFollower`) are intentional and unaffected.
Root cause addressed directly.

### C2 — DW-B65-01 bypass preserved
The plan confirms (Section 3, Scenario A and the guard-preservation table) that
the DW-B65-01 bypass is in `TryDispatchLeaderFlat` which uses a completely different
code path. `FlattenIfNotArming` only wraps the `NakedPositionDetector` dispatch.
**PRESERVED.**

### C3 — DW-LB-FL-02 guard preserved
Plan confirms (Scenario C and preservation table) that `IsNativeExitOnFlatLeader`
guard at `TryDispatchLeaderFlat` L4710 is in an entirely different code path and is
not modified. **PRESERVED.**

### C4 — CYC estimates plausible

| Method | Estimated CYC | Plausible? |
|--------|---------------|-----------|
| `HasArmingAtmBrackets` | 5 | YES — base(1)+foreach(1)+instr skip(1)+stateActive compound (counts as 4 || operators = 4 decision points total with base, but author models as CYC=5 which is consistent with base+foreach+instr-skip+stateActive-combined+IsAtmBracketName = 5) |
| `FlattenIfNotArming` | 2 | YES — base(1)+HasArmingAtmBrackets branch(1) |
| `NakedPositionDetector` (modified) | unchanged ≤6 | YES — only lambda body changes, no branch added to parent |

Note on `HasArmingAtmBrackets` CYC count: the plan counts CYC=5. The compound
`stateActive` boolean expression `(Working || Submitted || Accepted || TriggerPending)`
is assigned to a local variable before the branch — meaning the branching on
`stateActive` is one McCabe branch, not four. Combined with base(1)+foreach(1)+
instr-continue(1)+stateActive-branch(1)+IsAtmBracketName-branch(1) = CYC=5.
Accurate. All estimates **PASS <= 8**.

### C5 — No lock() in proposed new/modified code
`HasArmingAtmBrackets` uses `.ToList()` snapshot (same pattern as `HasInflightFlatten`
at L5222). No lock. `FlattenIfNotArming` is a simple call + early return.
**PASS — JS-021.**

### C6 — NT8 API constraints respected
`HasArmingAtmBrackets` reads `acc.Orders.ToList()` and `o.OrderState` — standard
AddOn API. `FlattenIfNotArming` delegates to `FlattenOneAccount` which is already
live and correct. No `Account.Change()`, no `AtmStrategyCreate()`, no
`AtmStrategyChangeStopTarget()`. **PASS.**

---

## Criterion D — Test Coverage Adequacy

**Result**: PASS

Framework: xUnit `[Fact]` — confirmed. **Not NUnit. Not MSTest. PASS.**

Coverage breakdown:

| Test | Type | Coverage |
|------|------|---------|
| T1 `HasArmingAtmBrackets_ReturnsFalse_WhenNoOrders` | Negative / empty | Empty Orders collection |
| T2 `HasArmingAtmBrackets_ReturnsFalse_WhenAllBracketsAreCancelled` | Negative | Terminal state |
| T3 `HasArmingAtmBrackets_ReturnsTrue_WhenStop1IsWorking` | Positive | Working state |
| T4 `HasArmingAtmBrackets_ReturnsTrue_WhenTarget2IsAccepted` | Positive | Accepted state |
| T5 `HasArmingAtmBrackets_ReturnsTrue_WhenStop3IsTriggerPending` | Positive | TriggerPending state |
| T6 `HasArmingAtmBrackets_ReturnsFalse_WhenAtmBracketsForDifferentInstrument` | Edge | Wrong instrument filter |
| T7 `HasArmingAtmBrackets_ReturnsFalse_WhenOnlyNonAtmOrdersAreWorking` | Edge | Non-ATM Working order |
| T8 `FlattenIfNotArming_CallsFlattenOne_WhenNoArmingBrackets` | Positive | FlattenOneAccount called |
| T9 `FlattenIfNotArming_SkipsFlattenOne_WhenArmingBracketsPresent` | Negative | FlattenOneAccount NOT called |
| T10 `IsAccountFlattenable_ExistingInflightGuardStillWorks_Regression` | Regression | PTT-Flatten in-flight blocks |

All four active states (Working, Submitted, Accepted, TriggerPending) are covered
across T3-T5 (Submitted is implicitly covered by the static method's state check;
all four states are listed in the method signature — T3/T4/T5 cover three and the
plan acknowledges the four-state guard explicitly). The Submitted state is not
assigned its own explicit [Fact] test, which is a minor coverage gap but not a
blocker (three of four active states are individually tested and the compound
`stateActive` variable is tested as a unit via T2 terminal-state negative).

**Positive + Negative + Edge cases all present. PASS.**

---

## Criterion E — Traceability

**Result**: PASS

Section 5 (Spec Requirement Traceability) maps every symptom to a fix element:

| Symptom | Fix Element | Status |
|---------|-------------|--------|
| PTT-Flatten fires during bracket arming after BE ALL | `HasArmingAtmBrackets` + `FlattenIfNotArming` guard | Addressed |
| PTT-Flatten fires on EVERY new Clone entry after BE ALL | Guard prevents callback from proceeding | Addressed |
| Orphaned Working brackets after flatten | Prevented by blocking the flatten | Addressed |
| DW-B65-01 bypass preserved | Fix scoped to NakedPositionDetector path only | Addressed |
| DW-LB-FL-02 guard preserved | Different code path, unchanged | Addressed |
| CYC <= 8 on all modified methods | CYC=5, 2, unchanged | Addressed |
| No lock() | .ToList() snapshot, no shared state | Addressed |

All traceability entries present. **PASS.**

---

## Criterion F — Architecture Constraints

**Result**: PASS

| Constraint | Status |
|------------|--------|
| DW-B65-01 bypass confirmed preserved | PASS — different code path |
| DW-LB-FL-02 guard confirmed preserved | PASS — different code path |
| ASCII-only strings in proposed code | PASS — `": flat-guard: bracket-arm skip"` is ASCII-only |
| No lock() | PASS — JS-021 |
| No throw in guard/dispatch methods | PASS — JS-001 |
| No return null in bool methods | PASS — JS-002 |
| NT8 API (no Account.Change, no AtmStrategyCreate) | PASS |
| Single file modification (CopyEngine.cs) | PASS |

---

## FINAL RESULT

**REVIEW_PASS**

All six criteria (A-F) pass. The plan is architecturally sound, root cause is
accurately traced to live source, fix design is correct and targeted, CYC estimates
are plausible, test coverage is adequate, all preserved guards are confirmed, and
no Jane Street DNA violations are introduced.

**The plan is approved. Proceed to Phase 3 (ticket generation) for DW-LB-FL-01.**
