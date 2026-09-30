# B108 Plan Review — ptt-plan-reviewer
**Phase**: 2 (Plan Review)
**Epic**: B108-T1 (DW-B107 fix)
**Plan under review**: `docs/brain/B108/02-architecture-plan.md`
**Reviewer**: ptt-plan-reviewer
**Date**: 2026-08-11

---

## Result: REVIEW_PASS

All 12 criteria satisfied. No violations found. Zero JS rule breaches. Zero spec gaps.
Plan is cleared for Phase 3 (ticket generation).

---

## Per-Criterion Verdicts

### RC-01: Spec Compliance — PASS

- **DW-B107 closed**: Plan Section 1 (Problem Statement) and Section 6 (Spec Requirement
  Traceability) explicitly close DW-B107 (P2-MEDIUM — stale PTT-BE-Target-* residues in BE
  path). All three CHANGE A/B/C rows in the traceability table cite DW-B107.
- **Out-of-scope files untouched**: Section 5 "Explicitly Out of Scope" lists
  `PttGlobalQuickExit.cs`, `PttQuickExit.cs`, and `PttBreakEvenSwap.cs` with per-file
  rationale. The cap is applied upstream (before `Execute`), so `PttBreakEvenSwap.cs` need
  not change.
- **Single file**: Section 5 "Total files: 1 / New files created: 0 / Interface files
  changed: 0 / Other PropTraderTools files changed: 0."

### RC-02: CYC Correctness — PASS

- **MoveStopToBreakEven CYC=8→7**: Section 3 CYC Analysis shows 3 old branches
  (`snapshot-foreach`, `stateOk`, `instrOk`) collapse into zero (method call, not decision
  point); 1 new `while`-cap branch added; pre-existing `partial-retry branch` counted
  explicitly — net delta is -1 (8→7). Summary table: "CYC Before: 8 | CYC After: 7 |
  Status: PASS."
- **SnapshotBeTargets CYC=7**: Section 3 enumerates all 7 counted branches with matching
  annotation in the method code comment: null guard (1), foreach (2), o==null continue (3),
  stateOk (4), instrOk+type (5), if(isNative) (6), else if(isPtt) (7). Summary table:
  "CYC After: 7 | Limit: 8 | Status: PASS."
- **No method exceeds 8**: "No existing method exceeds CYC=8 after B108. No other methods
  are touched." (Section 3 closing statement).

### RC-03: JS Rule Coverage — PASS

Section 4 JS Compliance Analysis contains a six-row table; each row names the rule and
explains the mechanism in the "New Code Behaviour" column:

| Rule | How plan satisfies |
|------|--------------------|
| JS-001 | "All new paths use early `return` or value return; no exception thrown anywhere" |
| JS-002 | "`SnapshotBeTargets` returns empty `nativeTargets` list on null input (never null)" |
| JS-021 | "local list operations only; `while + RemoveAt` is single-threaded; no shared state mutation; no `lock()`" |
| JS-033 | "All new code is synchronous; no `async` keyword anywhere" |
| ASCII-only | "method name, string literals, all comments — pure 7-bit ASCII" |
| No LINQ (NT8-006/JS-006) | "Cap uses `while + RemoveAt`; no `.Take()`, `.GetRange()`, `.Where()`" |

### RC-04: Return Type Correctness — PASS

- **Correct return type**: Section 2 CHANGE A signature:
  `private List<(double Price, int Qty, OrderAction Action)> SnapshotBeTargets(Account acc, Instrument instrument)`
- **Difference explicitly called out**: Section 2 contains a comparison table:
  "Return element type | `(double Price, int Qty)` [QX] | `(double Price, int Qty, OrderAction Action)` [BE]"
  with note: "needed by `PttBreakEvenSwap.Execute`". The QX version (`SnapshotTargetOrders`)
  at `PttGlobalQuickExit.cs` L182-185 confirms the 2-tuple — no `OrderAction` element.
  Verified in source: `List<(double Price, int Qty)>` at L182-184.

### RC-05: stateOk Width Preservation — PASS

- **All 7 states present**: Plan CHANGE A body lists
  `Working | Accepted | Submitted | Initialized | TriggerPending | ChangeSubmitted | CancelSubmitted`.
- **Not narrowed to 2**: QX path uses only `Working | Accepted` (confirmed: `PttGlobalQuickExit.cs`
  L195-197). Plan explicitly warns against narrowing.
- **Authority cited**: DW-B79-01 and REPAIR-09 DW-B79-05 are cited in three places:
  (1) the code comment in CHANGE A ("stateOk is wider than SnapshotTargetOrders (7 states vs 2)
  per DW-B79-01 + REPAIR-09 DW-B79-05"), (2) the narrative below the table
  ("The 7-state stateOk is intentional…introduced by DW-B79-01 and REPAIR-09 DW-B79-05"),
  (3) Section 6 Prior Fixes Preserved row "7-state stateOk widening | DW-B79-01 +
  REPAIR-09 DW-B79-05 | Carried verbatim into SnapshotBeTargets; not narrowed."

### RC-06: isNative `[6] != '0'` Guard — PASS

- **Guard present in plan**: Plan CHANGE A code line 94 shows:
  `&& o.Name[6] != '0';`
- **QX version confirmed to omit it**: `PttGlobalQuickExit.cs` L203-206 shows
  `o.Name.StartsWith("Target",...) && o.Name.Length > 6 && char.IsDigit(o.Name[6])` —
  no `!= '0'` check.
- **Difference called out**: Section 2 difference table: "isNative digit check |
  `[6] != '0'` omitted [QX] | `[6] != '0'` PRESENT (required — 'Target0' is not a valid
  ATM target) [BE]."
- **Source cross-check**: `CopyEngine.cs` L3408 in the existing Step A shows
  `&& o.Name[6] != '0'` — plan carries this guard faithfully.

### RC-07: Null Safety — PASS

- **Returns empty list, never null**: `SnapshotBeTargets` returns `nativeTargets` (an empty
  `List<T>`) when `acc == null || instrument == null`.
- **Position in method**: Plan code shows declarations first (`var nativeTargets = new
  List<...>()` then `var pttTargets = new List<...>()`), then immediately the null guard
  `if (acc == null || instrument == null) return nativeTargets;` — satisfying the RC-07
  requirement that the guard appears AFTER the two List declarations and before anything else.
- **T2 verifier criterion** in Section 7 corroborates: "Returns `nativeTargets` (empty list),
  NOT `null` / No `return null` anywhere in the method."

### RC-08: Cap Position — PASS

- **Ordering explicitly stated**: Section 2 CHANGE C: "Insertion point: Immediately after
  `var targets = SnapshotBeTargets(acc, instrument);` (the CHANGE B call site), BEFORE
  `PttBreakEvenSwap.Execute(...)`."
- **T10 verifier criterion** (Section 7) corroborates the same ordering requirement:
  "present immediately after `var targets = SnapshotBeTargets(...)` and BEFORE
  `PttBreakEvenSwap.Execute(acc, instrument, newStop, targets)`."
- Actual source confirms `PttBreakEvenSwap.Execute(...)` at L3427 — cap will slot
  between L3422 (post-foreach) and L3427.

### RC-09: No LINQ — PASS

- **Correct pattern specified**: Plan CHANGE C code:
  ```csharp
  while (targets.Count > 3)
      targets.RemoveAt(targets.Count - 1);
  ```
- **LINQ explicitly rejected**: Section 2 CHANGE C narrative: "Why `while` not `.Take(3)` or
  `.GetRange`: LINQ is banned by JS-006 / NT8-006. `List.Take` is a LINQ extension method.
  `while + RemoveAt` is allocation-free on the existing `List<T>`."
- **T11 verifier criterion** lists the exact banned patterns: `.Take(3)`, `.GetRange(0,3)`,
  `.Where(...)`, `.Select(...)`.

### RC-10: Acceptance Criteria Coverage — PASS

Section 7 "Test Scope: Verifier Inspection Criteria" contains all T1-T15 explicitly:

| Criterion | Present | Plan Section |
|-----------|---------|--------------|
| T1 — SnapshotBeTargets exists, correct sig | YES | Section 7 T1 |
| T2 — null guard returns empty list not null | YES | Section 7 T2 |
| T3 — two-pass structure (nativeTargets+pttTargets, native-first) | YES | Section 7 T3 |
| T4 — stateOk 7-state | YES | Section 7 T4 |
| T5 — isNative `[6] != '0'` | YES | Section 7 T5 |
| T6 — isPtt covers PTT-QX-T* and PTT-BE-Target-* | YES | Section 7 T6 |
| T7 — CYC annotation present on SnapshotBeTargets (CYC=7) | YES | Section 7 T7 |
| T8 — Step A loop replaced by single call | YES | Section 7 T8 |
| T9 — Step A comment updated | YES | Section 7 T9 |
| T10 — while cap present with correct position | YES | Section 7 T10 |
| T11 — No LINQ at cap site | YES | Section 7 T11 |
| T12 — MoveStopToBreakEven CYC annotation updated to 7 | YES | Section 7 T12 |
| T13 — No lock() in new code | YES | Section 7 T13 |
| T14 — No return null in new code | YES | Section 7 T14 |
| T15 — PttGlobalQuickExit.cs, PttQuickExit.cs, PttBreakEvenSwap.cs untouched | YES | Section 7 T15 |

### RC-11: Line References — PASS

Plan claims Step A loop spans **L3373-3422**. Cross-checked against actual source:

- `CopyEngine.cs` L3373: `// -- Step A: snapshot ATM target orders BEFORE cancelling anything ----------` ✓
- `CopyEngine.cs` L3379: `var targets = new List<(double Price, int Qty, OrderAction Action)>();` ✓
- `CopyEngine.cs` L3380: `foreach (Order o in acc.Orders)` ✓
- `CopyEngine.cs` L3422: `}` (closing brace of the `foreach`) ✓

Plan description matches: "the Step A comment block + `var targets = new List<...>()` +
the entire `foreach` loop body." The range is accurate and consistent with the actual source.

### RC-12: Prior Fix Preservation — PASS

Section 6 "Prior Fixes Preserved" table lists all three required entries with explicit
preservation statements:

| Fix | Plan Citation | Preservation |
|-----|---------------|--------------|
| DW-B79-01 | "7-state `stateOk` widening | DW-B79-01 + REPAIR-09 DW-B79-05 | Carried verbatim into `SnapshotBeTargets`; not narrowed" | YES |
| REPAIR-09 DW-B79-05 | Same row as DW-B79-01 | YES |
| HOTFIX-MSTBE-QX-TARGETS-01 | "`PTT-QX-T*` and `PTT-BE-Target-*` fallback | HOTFIX-MSTBE-QX-TARGETS-01 | Carried into `pttTargets` bucket of `SnapshotBeTargets`" | YES |

Additional preserved fixes also listed: `isRetry` guard (DW-B79-04) and `diagTotal`
logging block (DW-B79-02 DIAG) — both explicitly stated as untouched.

---

## Violations Found

None.

---

## Summary

| RC | Criterion | Verdict |
|----|-----------|---------|
| RC-01 | Spec Compliance — closes DW-B107, one file, three out-of-scope files excluded | PASS |
| RC-02 | CYC Correctness — MoveStopToBreakEven 8→7, SnapshotBeTargets 7, no method >8 | PASS |
| RC-03 | JS Rule Coverage — JS-001, JS-002, JS-021, JS-033, ASCII-only, no-LINQ each explained | PASS |
| RC-04 | Return Type Correctness — 3-tuple with OrderAction, difference from QX called out | PASS |
| RC-05 | stateOk Width Preservation — 7 states, not narrowed, DW-B79-01 + REPAIR-09 DW-B79-05 cited | PASS |
| RC-06 | isNative `[6] != '0'` Guard — present in BE, omission in QX called out, source confirmed | PASS |
| RC-07 | Null Safety — empty list returned, guard position after two List declarations | PASS |
| RC-08 | Cap Position — after SnapshotBeTargets call, before PttBreakEvenSwap.Execute, explicit | PASS |
| RC-09 | No LINQ — while+RemoveAt used, .Take/.GetRange explicitly rejected | PASS |
| RC-10 | Acceptance Criteria — T1-T15 all present in Section 7 | PASS |
| RC-11 | Line References — L3373-3422 verified against actual CopyEngine.cs source | PASS |
| RC-12 | Prior Fix Preservation — DW-B79-01, REPAIR-09 DW-B79-05, HOTFIX-MSTBE-QX-TARGETS-01 listed | PASS |

**REVIEW_PASS** — Phase 3 (ticket generation) is unblocked.
