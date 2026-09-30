# B42-QX-BE-01 — Plan Review

**Reviewer**: ptt-plan-reviewer
**Phase**: 2 (Plan Gate)
**Input**: `docs/brain/B42-QX-BE-01/02-architecture-plan.md`
**Standards**: `docs/standards/jane-street/RULES_CATALOG.md`
**Date**: 2026-08-05

---

## VERDICT: REVIEW_PASS

Zero violations found. Plan is cleared for Phase 3 (ticket generation).

---

## VIOLATION REGISTER

| # | Rule ID | Description | Location in Plan | Verdict |
|---|---------|-------------|-----------------|---------|
| — | — | No violations found | — | — |

---

## SPEC COVERAGE MATRIX

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| Direction 1 (Quick All → BE All): `SnapshotTargetsLocal` returns 0 targets when QX orders are live | YES | ROOT CAUSE SUMMARY §Direction 1; FIX T1 |
| Direction 2 (BE All → Quick All): `CancelQxBrackets` leaves PTT-BE-* orders alive to compete | YES | ROOT CAUSE SUMMARY §Direction 2; FIX T2 |
| T1: new `IsPttQxTarget` predicate, `private static`, no LINQ, CYC ≤ 2 | YES | FIX T1 — PttBreakEven.cs |
| T1: filter in `SnapshotTargetsLocal` extended, not replaced | YES | FIX T1 §Modified filter |
| T1: `IsAtmTargetName()` is invariant — body unchanged | YES | FIX T1 contract; INVARIANTS table |
| T2: single boolean change `cancelPttBe: false → true` | YES | FIX T2 — CopyEngine.cs |
| T2: `CancelStaleBrackets` body unchanged | YES | FIX T2; INVARIANTS table |
| No new state fields | YES | INVARIANTS table |
| No new order naming conventions | YES | INVARIANTS table |
| Single-button ATM-only path invariant documented | YES | INVARIANTS table |
| Single-button QX-only path no-regression documented | YES | INVARIANTS table |
| Single-button BE-only path no-regression documented | YES | INVARIANTS table |
| JS-001 (no throw in hot paths) | YES | RULES CATALOG GATE; FIX T1 contract |
| JS-002 (no return null) | YES | RULES CATALOG GATE; all new methods return bool |
| JS-021 (no lock) | YES | RULES CATALOG GATE; THREADING MODEL |
| JS-033 (no async void) | YES | RULES CATALOG GATE; no async methods introduced |
| NT8-006 (no LINQ in PttBreakEven.cs) | YES | RULES CATALOG GATE; FIX T1 NT8-006 note |
| 7-scan checklist present for engineer tickets | YES | 7-SCAN CHECKLIST section (SCAN-01 through SCAN-07) |
| xUnit [Fact] tests specified (never NUnit/MSTest) | YES | XUNIT TEST PLAN section |
| CYC ≤ 8 on all touched methods | YES | IsPttQxTarget=2, SnapshotTargetsLocal=3, CancelQxBrackets=1 |
| BUILD TAG UPDATE specified | YES | BUILD TAG UPDATE section |

---

## CRITERION-BY-CRITERION ANALYSIS

### Criterion 1 — Both root causes addressed

**PASS.**

Direction 1 (Quick All fires → BE All cannot snapshot QX targets):
Root cause identified at `SnapshotTargetsLocal` line 266 filter; fix adds `IsPttQxTarget` as a
second accepted predicate alongside `IsAtmTargetName`. Data flow diagram confirms `"PTT-QX-T1"`
passes the new filter and tranche count is > 0 before `SubmitBeTargetsLocal` is called.

Direction 2 (BE All fires → Quick All leaves PTT-BE-* competing):
Root cause identified at `CancelQxBrackets` line 2229; fix flips `cancelPttBe: false` to `true`.
Data flow diagram confirms `CancelStaleBrackets` sweeps both PTT-BE-* and PTT-QX-* orders before
Quick All submits new QX brackets.

### Criterion 2 — T1 preserves NT8-006 (no LINQ in PttBreakEven.cs)

**PASS.**

`IsPttQxTarget` uses exclusively:
- `string.IsNullOrEmpty` (static method, not LINQ)
- `.Length` (property, not LINQ)
- `.Substring` (instance method, not LINQ)
- `char` comparison with `>=` / `<=` (primitive, not LINQ)

No `using System.Linq;` introduced. SCAN-07 in the 7-scan checklist mandates `grep` for
`using System.Linq` and `.Where|.Select|.Any|.ToList` in PttBreakEven.cs.

### Criterion 3 — `IsAtmTargetName()` not modified

**PASS.**

Plan states `IsAtmTargetName() MUST NOT be changed — it is an invariant.` Invariants table
row `IsAtmTargetName() signature and body unchanged | CONFIRMED` present. SCAN-05 mandates diff
shows zero lines changed in that method. Regression tests verify ATM names still match and QX names
do not cross-match: `[Fact] IsAtmTargetName_PttQxT1_ReturnsFalse`.

### Criterion 4 — `IsPttQxTarget()` CYC ≤ 2

**PASS — CYC = 2.**

Standard McCabe counting:
- Base = 1
- `if (string.IsNullOrEmpty(name) || name.Length < 9)` = 1 decision node (the if-statement;
  compound `||` within a single if is one branch point under standard McCabe, not MCDC)
- `return ... && ... && ...` = the `&&` operators in a return expression do not constitute
  control-flow branch nodes; the return statement does not diverge flow.
- **Total CYC = 2.** Claim in plan is correct.

### Criterion 5 — `SnapshotTargetsLocal()` CYC stays ≤ 3

**PASS.**

Orchestrator confirms existing CYC = 3. The modification extends an existing compound `if`
condition from `!IsAtmTargetName(o.Name)` to `(!IsAtmTargetName(o.Name) && !IsPttQxTarget(o.Name))`.
This replaces one sub-expression of an already-counted branch node. No new `if`/`while`/`for`/
`case`/`goto` added. CYC remains 3.

### Criterion 6 — T2 is the single boolean change

**PASS.**

The before/after diff shown in the plan is exactly one token change:
```
- => CancelStaleBrackets(acc, instr, cancelPttBe: false, cancelPttQx: true);
+ => CancelStaleBrackets(acc, instr, cancelPttBe: true,  cancelPttQx: true);
```
`CancelStaleBrackets` signature and body confirmed unchanged. `CancelQxBrackets` method body
remains a single-expression lambda (CYC = 1).

### Criterion 7 — No new state fields

**PASS.**

`IsPttQxTarget` is `private static bool` — pure computation, no instance or static field
introduced. No new fields anywhere in `PttBreakEven` or `CopyEngine`. Invariants table: CONFIRMED.

### Criterion 8 — No new order naming conventions

**PASS.**

`"PTT-QX-T1"`, `"PTT-QX-T2"`, `"PTT-QX-T3"` are existing naming conventions established by the
Quick All feature. The predicate merely recognises them; it does not create or register new names.
Invariants table: CONFIRMED.

### Criterion 9 — JS-001, JS-002, JS-021, JS-033

**PASS on all four.**

| Rule | Evidence in Plan |
|------|-----------------|
| JS-001 (no throw) | `IsPttQxTarget` returns bool on all paths; no `throw` anywhere in plan |
| JS-002 (no return null) | Only boolean return types introduced |
| JS-021 (no lock) | THREADING MODEL: "No `lock()`, no ConcurrentQueue, no Dispatcher.InvokeAsync required" |
| JS-033 (no async void) | Both fixes are synchronous; no async methods introduced |

### Criterion 10 — 7-scan checklist present

**PASS.**

Section "7-SCAN CHECKLIST (Engineer contract for Phase 5 execution)" contains all seven scans:

| Scan | Coverage |
|------|----------|
| SCAN-01 | `lock(` in modified files |
| SCAN-02 | `async void` in modified files |
| SCAN-03 | `throw new` in hot paths |
| SCAN-04 | `return null` from non-nullable returns |
| SCAN-05 | `IsAtmTargetName()` body unchanged |
| SCAN-06 | `CancelStaleBrackets()` body unchanged |
| SCAN-07 | NT8-006 — no LINQ added to PttBreakEven.cs |

### Criterion 11 — Single-button behaviour invariants documented

**PASS — all three single-button paths covered.**

| Path | Invariant | How proven |
|------|-----------|-----------|
| ATM-only | `IsPttQxTarget` never matches ATM names | `"Target1"` has length 7 < 9; fails guard at `name.Length < 9`; returns false |
| QX-only | `cancelPttBe: true` with no PTT-BE-* orders is a no-op | `CancelStaleBrackets` filter produces empty cancel list when no matching orders exist |
| BE-only | `IsPttQxTarget` returns false when no QX orders present | No PTT-QX-T* orders in `acc.Orders`; snapshot includes only ATM targets as before |

---

## RISK NOTES (informational — not blocking)

1. **T2 side-effect on partially-filled BE positions**: The plan confirms this is intended
   behaviour (any residual BE bracket after BE All fires is stale). No orphan-position risk
   identified since `CancelStaleBrackets` only cancels *Working* orders, not filled legs.
   Engineer should confirm this understanding during implementation.

2. **`IsPttQxTarget` length check at 9, not 10**: `"PTT-QX-T1"` is 9 characters; the guard
   `name.Length < 9` would pass a 9-char string to the prefix+digit check. The prefix check
   requires `name.Substring(0, 8)` to equal `"PTT-QX-T"` (8 chars) and `name[8]` to be
   `'1'`..'3'. For a 9-char string this is exactly correct. No off-by-one error.

---

## FINAL GATE RESULT

```
REVIEW_PASS
Violations: 0
Criteria passed: 11/11
Plan cleared for Phase 3 ticket generation.
```
