# Ticket Review: B42-QX-BE-01
## Quick All / BE All any-order interaction repair

**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Date**: 2026-08-05
**Inputs**:
- `docs/brain/B42-QX-BE-01/04-tickets.md`
- `docs/brain/B42-QX-BE-01/02-architecture-plan.md`
- `docs/standards/jane-street/RULES_CATALOG.md`

---

## T1 — PttBreakEven.cs: Add `IsPttQxTarget` + extend `SnapshotTargetsLocal`

### Traceability
**FAIL**

1. **Plan-to-ticket implementation divergence** — The architecture plan (02-architecture-plan.md §FIX T1) prescribes:
   ```csharp
   if (string.IsNullOrEmpty(name) || name.Length < 9) return false;
   return "PTT-QX-T".Equals(name.Substring(0, 8), StringComparison.Ordinal) && name[8] >= '1' && name[8] <= '3';
   ```
   The ticket (04-tickets.md §T1, Change 1 of 2) prescribes a different implementation:
   ```csharp
   if (name == null || name.Length != 9) return false;
   return name[0] == 'P' && name[1] == 'T' && ... name[8] >= '1' && name[8] <= '3';
   ```
   These differ in two ways:
   - Guard condition: plan uses `< 9` (allows strings of length 10+); ticket uses `!= 9` (strictly enforces length 9).
   - Body: plan uses `Substring(0,8).Equals(..., Ordinal)`; ticket uses per-character indexed comparison.
   The ticket instructs the engineer to "copy verbatim" — but verbatim copy of the ticket diverges from the plan's approved design. This is a traceability violation: work described in the ticket is not traceable to the exact implementation in the plan.

   **Citation**: 04-tickets.md §T1 "Change 1 of 2 — Implementation", vs 02-architecture-plan.md §"New method: IsPttQxTarget(string name) — Implementation".

2. **Plan xUnit test list not fully reflected in T3** — The architecture plan (§XUNIT TEST PLAN) lists 12 distinct `[Fact]` stubs for `IsPttQxTarget` and `IsAtmTargetName` (including null, empty, too-short, T1/T2/T3/T4 true/false, wrong-prefix, regression tests). The ticket T3 collapses these into 7 named facts (T_BUG_QX_BE_01 through _07). The 7-fact contract is the specified validation scope per the spec requirement — however the plan also requires a regression test for `IsAtmTargetName` (via reflection in T_BUG_QX_BE_07). The ticket covers this. The compression from plan's 12 stubs to 7 facts is acceptable but constitutes a traceability gap that is flagged here as a WARNING rather than a hard FAIL on this sub-item.

   **Net result**: FAIL on item 1 (implementation divergence). WARNING on item 2 (test compression).

### JS Pre-Check
**PASS**

| Rule | Check | Verdict |
|------|-------|---------|
| JS-021 (lock) | No `lock(` described in T1 changes | PASS |
| JS-002 (return null) | `IsPttQxTarget` returns `bool`; no null return | PASS |
| JS-001 (throw) | No `throw` in guard — early return only | PASS |
| JS-033 (async void) | No async method introduced | PASS |

### CYC Pre-Check
**PASS** (with caveat noted under Traceability)

- `IsPttQxTarget` as described in ticket: CYC = 2 (one if-guard, one compound expression). PASS.
- `SnapshotTargetsLocal` — ticket states CYC stays 3, "no new branch node added." The
  `(!IsAtmTargetName(o.Name) && !IsPttQxTarget(o.Name))` replaces `!IsAtmTargetName(o.Name)` inside
  an existing `||` chain. This replaces one sub-expression with a compound `&&` — the number of
  predicate branches in the outer `if` is unchanged. CYC = 3: PASS.

### NT8 Constraints
**PASS**

| Rule | Check | Verdict |
|------|-------|---------|
| NT8-006 (no LINQ in PttBreakEven.cs) | Ticket explicitly prohibits `using System.Linq`, `.Where`, `.Select`, `.Any`, `.ToList` | PASS |
| NT8-007 (CreateOrder arg11) | No new CreateOrder calls in T1 | PASS |
| NT8-013 (DateTime) | No DateTime usage in T1 | PASS |
| `IsAtmTargetName()` invariant | Ticket explicitly states: "Engineer must verify `git diff` shows zero changed lines inside `IsAtmTargetName`" | PASS |

### Test Coverage
**PASS**

T1 does not introduce any new public or internal methods beyond `IsPttQxTarget` (private static). The
private method is exercised through T3 tests (T_BUG_QX_BE_01, T_BUG_QX_BE_02, T_BUG_QX_BE_03,
T_BUG_QX_BE_04) via inline replication helpers or reflection. All public-facing behaviour change
(SnapshotTargetsLocal filter extension) is exercised indirectly through the combined-predicate tests.
No untested public API surfaces exist.

### Scan Checklist
**PASS** — all 7 scans present

T1 carries SCAN-01 through SCAN-07 as required:

| Scan | Label in Ticket | Present |
|------|-----------------|---------|
| SCAN-01 | JS-021 lock | YES |
| SCAN-02 | JS-002 null | YES |
| SCAN-03 | JS-033 async void | YES |
| SCAN-04 | NT8-006 LINQ | YES |
| SCAN-05 | CYC <= 8 | YES |
| SCAN-06 | IsAtmTargetName unchanged | YES |
| SCAN-07 | no new state fields | YES |

### File Routing
**PASS** — `c:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttBreakEven.cs`
(Wave workspace, not Director workspace)

### Completeness
**PASS** (with Traceability FAIL already cited above)

- Insertion point specified: "immediately after the closing `}` of `IsAtmTargetName()`, approximately line 248" ✓
- Filter replacement location specified: "line ~266, Current line 266" with verbatim before/after text ✓
- `IsAtmTargetName()` invariant documented ✓

### T1 VERDICT: **TICKET_REVIEW_FAIL**
**Violation**: Implementation in T1 is not traceable to the plan. The guard condition (`!= 9` vs `< 9`)
and the body (`char-indexed` vs `Substring+Ordinal`) differ from what the REVIEW_PASS plan prescribes.
The engineer is instructed to "copy verbatim" from the ticket, which produces different behavior from
the plan for strings of length > 9 (plan would accept them past the length guard; ticket would reject).
This is a functional difference that was not resolved by plan review. **Architect must reconcile
04-tickets.md §T1 implementation with 02-architecture-plan.md §FIX T1 before engineer spawning.**

---

## T2 — CopyEngine.cs: Flip `cancelPttBe: false` → `cancelPttBe: true`

### Traceability
**PASS**

- Maps to spec requirement BUG-B42-QX-BE-01 Direction 2 ✓
- Plan §FIX T2 and ticket §T2 both specify exactly: change `cancelPttBe: false` → `cancelPttBe: true` at lines 2229–2230 of CopyEngine.cs ✓
- No phantom work; no plan items missing ✓

### JS Pre-Check
**PASS**

| Rule | Check | Verdict |
|------|-------|---------|
| JS-021 (lock) | No `lock(` introduced in expression-body delegation | PASS |
| JS-002 (return null) | Returns `void`; no null possible | PASS |
| JS-001 (throw) | No throw; argument flip only | PASS |
| JS-033 (async void) | No async changes | PASS |

### CYC Pre-Check
**PASS**

- `CancelQxBrackets` CYC stays 1 (single expression-body delegation, no branching) ✓

### NT8 Constraints
**PASS**

| Rule | Check | Verdict |
|------|-------|---------|
| NT8-007 (CreateOrder arg11) | No new CreateOrder calls | PASS |
| NT8-013 (DateTime) | No DateTime usage | PASS |
| `CancelStaleBrackets()` invariant | Ticket explicitly states "No other lines in CopyEngine.cs are modified" and SCAN-06 verifies `CancelStaleBrackets` body unchanged | PASS |

### Test Coverage
**PASS**

`CancelQxBrackets` change is a one-argument flip on an expression-body delegating method. The semantic
change (PTT-BE-* orders now included in cancel) is exercised by T3 facts T_BUG_QX_BE_05 and
T_BUG_QX_BE_06, which validate the flag predicate logic inline. No new public/internal methods are
introduced without a corresponding `[Fact]`.

### Scan Checklist
**PASS** — all 7 scans present

T2 carries SCAN-01 through SCAN-07 as required:

| Scan | Label in Ticket | Present |
|------|-----------------|---------|
| SCAN-01 | JS-021 lock | YES |
| SCAN-02 | JS-002 null | YES |
| SCAN-03 | JS-033 async void | YES |
| SCAN-04 | NT8-006 LINQ (not our change) | YES |
| SCAN-05 | CYC <= 8 | YES |
| SCAN-06 | CancelStaleBrackets unchanged | YES |
| SCAN-07 | no new state fields | YES |

### File Routing
**PASS** — `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`
(Wave workspace, not Director workspace)

### Completeness
**PASS**

- Exact before/after text specified for lines 2229–2230 ✓
- Build tag update documented with verbatim string ✓
- B41 single-button regression risk: **WARNING** — ticket does not explicitly state that B41 tests
  T_B41_09..11 remain unaffected. The cross-ticket invariants table states "Single-button QX-only
  path: no regression — `cancelPttBe: true` with no PTT-BE-* orders present is a no-op." This covers
  the scenario described by B41 tests but does not reference the B41 test IDs by name. Architect should
  add an explicit statement: "T_B41_09, T_B41_10, T_B41_11 must continue to pass after T2 change"
  to make the regression contract visible to the engineer. This is a WARNING, not a hard FAIL, because
  the functional correctness argument is already present in the invariants table.

### T2 VERDICT: **TICKET_REVIEW_PASS** (with WARNING on B41 test citation)

---

## T3 — CopyEngineTests.cs: 7 xUnit `[Fact]` tests

### Traceability
**PASS**

All 7 `[Fact]` method names map to the spec requirement BUG-B42-QX-BE-01 Validation:

| Fact ID | Assertion | Spec Coverage |
|---------|-----------|---------------|
| T_BUG_QX_BE_01 | `IsPttQxTarget` true for T1, T2 | Direction 1 predicate — positive path |
| T_BUG_QX_BE_02 | `IsPttQxTarget` false for T4, Stop, Target1 | Direction 1 predicate — negative path |
| T_BUG_QX_BE_03 | Combined predicate accepts ATM name | SnapshotTargetsLocal ATM-path invariant |
| T_BUG_QX_BE_04 | Combined predicate accepts QX name | SnapshotTargetsLocal QX-path (bug fix) |
| T_BUG_QX_BE_05 | `cancelPttBe: true` includes PTT-BE-Stop | Direction 2 flag logic |
| T_BUG_QX_BE_06 | `cancelPttBe: true` includes PTT-BE-Target-1 | Direction 2 flag logic |
| T_BUG_QX_BE_07 | `IsAtmTargetName` still false for PTT-QX-T1 | IsAtmTargetName invariant guard |

No phantom tests. No plan test items left uncovered in the 7-fact contract scope.

### JS Pre-Check
**PASS**

| Rule | Check | Verdict |
|------|-------|---------|
| JS-021 (lock) | No `lock(` in any of the 7 test methods | PASS |
| JS-002 (return null) | No `return null` in test methods | PASS |
| JS-001 (throw) | No throw in test logic | PASS |
| JS-033 (async void) | All 7 `[Fact]` methods are synchronous void | PASS |

### CYC Pre-Check
**PASS**

All 7 `[Fact]` methods are linear (CYC = 1). `IsPttQxTargetInline` and `IsAtmTargetNameInline` private
helpers are each CYC = 2 (single if-guard + compound return). No method exceeds CYC = 2.

### NT8 Constraints
**PASS**

Tests use no NT8 runtime objects except `T_BUG_QX_BE_07` which uses `System.Reflection` to invoke
`PttBreakEven.IsAtmTargetName` via `typeof(PttBreakEven).GetMethod(...)`. This is acceptable in a
test file. No LINQ restriction applies to test files (SCAN-04 is marked N/A in T3). No DateTime
usage. No CreateOrder calls.

### Test Coverage
**PASS**

7-fact contract fully covered. Inline helpers (`IsPttQxTargetInline`, `IsAtmTargetNameInline`)
replicate the production logic and serve as self-contained oracle implementations. Using reflection
for T_BUG_QX_BE_07 is the correct approach for asserting a private static method is unchanged and
behaves as specified.

**Note**: `IsPttQxTargetInline` in T3 uses the ticket's char-indexed implementation (`name == null
|| name.Length != 9` guard + char comparisons). This is consistent with the T1 ticket implementation.
However, per the T1 Traceability FAIL, if the plan's Substring approach is restored in T1, the inline
helper in T3 must also be updated to match. The inline helper must mirror the production implementation
exactly or it becomes an unreliable oracle.

### Scan Checklist
**PASS** — all 7 scans present

T3 carries SCAN-01 through SCAN-07 as required:

| Scan | Label in Ticket | Present |
|------|-----------------|---------|
| SCAN-01 | JS-021 lock | YES |
| SCAN-02 | JS-002 null | YES |
| SCAN-03 | JS-033 async void | YES |
| SCAN-04 | NT8-006 LINQ (N/A for tests) | YES |
| SCAN-05 | CYC <= 8 | YES |
| SCAN-06 | xUnit [Fact] only | YES |
| SCAN-07 | no new state fields | YES |

### File Routing
**PASS** — `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs`
(Wave workspace, not Director workspace)

### Completeness
**PASS**

- Append location specified: "before the closing `}` of the last test class (line ~4340, before `}` at line 4341)" ✓
- Required `using` statements listed (System.Reflection, Xunit) ✓
- All 7 `[Fact]` method bodies supplied verbatim with full assertions ✓

### T3 VERDICT: **TICKET_REVIEW_PASS** (conditional on T1 plan reconciliation — if T1 implementation
changes to Substring approach, `IsPttQxTargetInline` in T3 must be updated to match)

---

## Aggregate Checklist Results

| Checklist Item | Result | Notes |
|----------------|--------|-------|
| T1 traces to BUG-B42-QX-BE-01 Direction 1 | FAIL | Implementation in ticket ≠ implementation in plan |
| T2 traces to BUG-B42-QX-BE-01 Direction 2 | PASS | Exact before/after matches plan |
| T3 covers all 7 [Fact] IDs T_BUG_QX_BE_01..07 | PASS | All 7 present with assertions |
| No lock() in T1/T2 | PASS | None described |
| No return null introduced | PASS | All methods return bool or void |
| No async void introduced | PASS | All methods synchronous |
| IsPttQxTarget CYC = 2 | PASS | One guard + one compound return |
| SnapshotTargetsLocal CYC <= 3 | PASS | Filter extended, no new branch node |
| CancelQxBrackets CYC = 1 | PASS | Expression-body delegation unchanged |
| NT8-006: char-indexed primitives in T1 | PASS | No LINQ; char-by-char in ticket impl |
| NT8-007: no new CreateOrder arg11 | PASS | No new order submissions |
| NT8-013: no DateTime changes | PASS | None present |
| IsAtmTargetName() signature/body unchanged | PASS | Explicitly documented as invariant |
| T1 insertion point specified (~line 248) | PASS | "immediately after closing } of IsAtmTargetName" |
| T1 filter replacement line specified (~line 266) | PASS | Verbatim before/after text given |
| T2 exact before/after text specified | PASS | Lines 2229–2230 verbatim |
| T3 append location specified (~line 4340) | PASS | "before closing } at line 4341" |
| T_BUG_QX_BE_01 asserts IsPttQxTarget true for T1, T2 | PASS | Both asserted |
| T_BUG_QX_BE_02 false for out-of-range / wrong-prefix | PASS | T4, Stop, Target1 all asserted false |
| T_BUG_QX_BE_03 combined predicate accepts ATM name | PASS | "Target1" via isAtm path |
| T_BUG_QX_BE_04 combined predicate accepts QX name | PASS | "PTT-QX-T1" via isQx path |
| T_BUG_QX_BE_05 cancelPttBe=true includes PTT-BE-Stop | PASS | Assert.True on passesFilter |
| T_BUG_QX_BE_06 cancelPttBe=true includes PTT-BE-Target-1 | PASS | Assert.True on passesFilter |
| T_BUG_QX_BE_07 IsAtmTargetName invariant (false for PTT-QX-T1) | PASS | Reflection + Assert.False |
| T1 has SCAN-01..07 | PASS | All 7 present |
| T2 has SCAN-01..07 | PASS | All 7 present |
| T3 has SCAN-01..07 | PASS | All 7 present |
| B41 tests T_B41_09..11 not broken by T2 change | WARNING | Invariant logic present, test IDs not cited explicitly |

---

## Violations Summary

| # | Type | Ticket | Violation | Rule / Section |
|---|------|--------|-----------|----------------|
| 1 | FAIL | T1 | **Implementation diverges from approved plan.** T1 instructs engineer to "copy verbatim" a char-indexed guard (`name == null \|\| name.Length != 9`) and per-char body. Plan (REVIEW_PASS) prescribes `string.IsNullOrEmpty(name) \|\| name.Length < 9` guard + `"PTT-QX-T".Equals(name.Substring(0, 8), StringComparison.Ordinal)` body. These differ functionally for strings of length > 9. T3 `IsPttQxTargetInline` mirrors the ticket (not the plan), making the oracle also misaligned with the plan. | Traceability rule: every ticket item must map to a plan item. 04-tickets.md §T1 "Change 1 of 2 — Implementation" vs 02-architecture-plan.md §"New method: IsPttQxTarget(string name) — Implementation". |

---

## Warnings (non-blocking, recommend fix before engineer)

| # | Ticket | Warning |
|---|--------|---------|
| W1 | T2 | B41 regression test IDs (T_B41_09, T_B41_10, T_B41_11) not explicitly cited in T2 as must-remain-passing. Functional argument is present in the invariants table but engineer contract is incomplete. Add: "T_B41_09, T_B41_10, T_B41_11 must continue to pass after this change" to T2. |
| W2 | T3 | If T1 Traceability FAIL is resolved by restoring the plan's Substring approach, the `IsPttQxTargetInline` helper in T3 must be updated to mirror the updated production implementation. Failure to sync will make T_BUG_QX_BE_01 and T_BUG_QX_BE_02 test an oracle that diverges from the real method. |
| W3 | T3 | Plan §XUNIT TEST PLAN lists 12 named `[Fact]` stubs. Ticket T3 delivers 7. The 3 stubs dropped (null-guard, empty-guard, too-short-guard) provide coverage of edge cases in `IsPttQxTarget` that are tested via reflection in T_BUG_QX_BE_07 only for `IsAtmTargetName`. Recommend architect explicitly confirm that null/empty/short edge cases are adequately covered by T_BUG_QX_BE_02 (which tests T4, Stop, Target1 but not null or empty). |

---

## Overall: **TICKET_REVIEW_FAIL**

**Reason**: T1 contains a FAIL on Traceability — the implementation the engineer is instructed to
build verbatim (`name == null || name.Length != 9` + char-indexed body) diverges from the
REVIEW_PASS architecture plan (`string.IsNullOrEmpty || name.Length < 9` + Substring approach).

**Required action before re-submission**: Architect must resolve the implementation discrepancy
by either:
- (A) Updating 04-tickets.md T1 §"Change 1 of 2" to use the plan's Substring implementation, OR
- (B) Updating 02-architecture-plan.md to reflect the char-indexed approach and re-obtaining
  REVIEW_PASS on the updated plan.

T3 §`IsPttQxTargetInline` must then be updated to mirror whichever implementation is chosen.

**T2 and T3 are TICKET_REVIEW_PASS** and require no changes beyond the warnings noted above.
Engineer may NOT be spawned until T1 (and the cascading T3 oracle fix) is resolved.

---

## Re-Gate Review: B42-QX-BE-01 (Phase 3.5 — Iteration 2)

**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Date**: 2026-08-05
**Trigger**: Architect resolved TICKET_REVIEW_FAIL Violation 1 (T1 Traceability — implementation divergence)
**Inputs**:
- `docs/brain/B42-QX-BE-01/04-tickets.md` (updated)
- `docs/brain/B42-QX-BE-01/02-architecture-plan.md` (updated — Status now REVIEW_PASS)
- `docs/standards/jane-street/RULES_CATALOG.md`

---

### Violation 1 Resolution Confirmation

**Previous violation**: T1 instructed engineer to build `name == null || name.Length != 9` + char-indexed body while the REVIEW_PASS plan prescribed `string.IsNullOrEmpty || name.Length < 9` + `Substring`-based body. Functional difference: plan accepted strings of length > 9; ticket rejected them.

**Fix applied**: `02-architecture-plan.md` §FIX T1 now carries the char-indexed implementation (`name == null || name.Length != 9` guard + per-char `name[0]..name[8]` body), identical to `04-tickets.md` §T1 "Change 1 of 2".

**Diff confirmed**: Plan delta shows lines +100..+104 now match ticket lines verbatim. Summary comment updated (`CYC=2: (1) null/exact-length guard, (2) char-by-char prefix+digit check`). NT8-006 risk note updated (`char indexer name[N]`). API surface table updated (`string.Length, char indexer name[N]`).

**Resolution status**: **RESOLVED — T1 Traceability PASS**

Also resolved: Warning W2 from Iteration 1 — `IsPttQxTargetInline` in T3 now mirrors the updated production implementation. Oracle is consistent.

---

### T1 — PttBreakEven.cs: Add `IsPttQxTarget` + extend `SnapshotTargetsLocal`

**Traceability**: PASS
- BUG-B42-QX-BE-01 Direction 1 ✓
- Plan §FIX T1 implementation now identical to ticket §T1 "Change 1 of 2" (guard + body) ✓
- Plan §Modified filter matches ticket §T1 "Change 2 of 2" verbatim ✓
- `IsAtmTargetName()` invariant documented in both plan and ticket ✓
- No phantom work; no plan items missing ✓

**JS Pre-Check**: PASS
| Rule | Verdict |
|------|---------|
| JS-021 (lock) | PASS — no `lock(` in pure static computation |
| JS-002 (return null) | PASS — returns `bool` |
| JS-001 (throw) | PASS — early return only |
| JS-033 (async void) | PASS — all methods synchronous |
| JS-008/009 (immutability) | PASS — no new struct/Dictionary fields |

**CYC Pre-Check**: PASS
- `IsPttQxTarget` CYC = 2 (if-guard + compound `&&` return) ≤ 8 ✓
- `SnapshotTargetsLocal` CYC stays 3 (filter extended, no new branch node) ≤ 8 ✓

**NT8 Check**: PASS
| Rule | Verdict |
|------|---------|
| NT8-006 (no LINQ) | PASS — guard uses `null` check + `.Length`; body uses char indexer only |
| NT8-007 (CreateOrder arg11) | PASS — no new CreateOrder |
| NT8-014 (PTT- prefix) | PASS — no new order name strings |
| DateTime.Now | PASS — none |
| FontFamily / hardcoded hex | PASS — none |
| sealed on TradeCopierWindow | PASS — none |
| async in lifecycle | PASS — none |

**Test Coverage**: PASS
- `IsPttQxTarget` (private static): covered by T3 T_BUG_QX_BE_01, _02, _03, _04 (inline helpers + reflection) ✓
- `SnapshotTargetsLocal` filter change: covered by T3 T_BUG_QX_BE_03 (ATM path) and T_BUG_QX_BE_04 (QX path) ✓
- No new public/internal method without a corresponding `[Fact]` ✓

**Scan Checklist**: PASS — all 7 present
| Scan | Label | Present |
|------|-------|---------|
| SCAN-01 | JS-021 lock — no `lock(` in PttBreakEven.cs | YES |
| SCAN-02 | JS-002 null — returns bool, no null returns | YES |
| SCAN-03 | JS-033 async void — both methods synchronous | YES |
| SCAN-04 | NT8-006 LINQ — grep commands specified | YES |
| SCAN-05 | CYC ≤ 8 — IsPttQxTarget=2, SnapshotTargetsLocal=3 | YES |
| SCAN-06 | IsAtmTargetName unchanged — git diff verification | YES |
| SCAN-07 | No new state fields | YES |

**File Routing**: PASS — `c:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttBreakEven.cs` (Wave workspace)

**VERDICT: TICKET_REVIEW_PASS**

---

### T2 — CopyEngine.cs: Flip `cancelPttBe: false` → `cancelPttBe: true`

**Traceability**: PASS
- BUG-B42-QX-BE-01 Direction 2 ✓
- Plan §FIX T2 and ticket §T2 identical: `cancelPttBe: false` → `cancelPttBe: true` at lines 2229–2230 ✓
- Build tag update in both plan and ticket ✓
- No phantom work; no plan items missing ✓

**JS Pre-Check**: PASS
| Rule | Verdict |
|------|---------|
| JS-021 (lock) | PASS — expression-body delegation, no lock |
| JS-002 (return null) | PASS — returns void |
| JS-001 (throw) | PASS — argument flip only |
| JS-033 (async void) | PASS — no async changes |

**CYC Pre-Check**: PASS
- `CancelQxBrackets` CYC stays 1 (single expression-body, no new branch) ✓

**NT8 Check**: PASS
- No CreateOrder calls ✓; no DateTime.Now ✓; no FontFamily ✓; no hardcoded hex ✓
- `CancelStaleBrackets` body unchanged — enforced by SCAN-06 ✓

**Test Coverage**: PASS
- `CancelQxBrackets` semantic change (PTT-BE-* now included): exercised by T3 T_BUG_QX_BE_05 and T_BUG_QX_BE_06 ✓
- No new public/internal method without `[Fact]` ✓

**Scan Checklist**: PASS — all 7 present
| Scan | Label | Present |
|------|-------|---------|
| SCAN-01 | JS-021 lock | YES |
| SCAN-02 | JS-002 null | YES |
| SCAN-03 | JS-033 async void | YES |
| SCAN-04 | NT8-006 LINQ (not our change) | YES |
| SCAN-05 | CYC ≤ 8 — CancelQxBrackets=1 | YES |
| SCAN-06 | CancelStaleBrackets body unchanged | YES |
| SCAN-07 | No new state fields | YES |

**File Routing**: PASS — `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` (Wave workspace)

**VERDICT: TICKET_REVIEW_PASS**

---

### T3 — CopyEngineTests.cs: 7 xUnit `[Fact]` tests

**Traceability**: PASS
- BUG-B42-QX-BE-01 Validation ✓
- All 7 `[Fact]` IDs map to plan §XUNIT TEST PLAN validation requirements ✓
- `IsPttQxTargetInline` in T3 now mirrors updated plan implementation (char-indexed) — W2 resolved ✓
- No phantom tests; no plan test items uncovered ✓

**JS Pre-Check**: PASS
| Rule | Verdict |
|------|---------|
| JS-021 (lock) | PASS — no `lock(` in any test method |
| JS-002 (return null) | PASS — no `return null` in tests |
| JS-001 (throw) | PASS — no throw in test logic |
| JS-033 (async void) | PASS — all 7 `[Fact]` methods synchronous `void` |

**CYC Pre-Check**: PASS
- All 7 `[Fact]` methods: CYC = 1 (linear) ✓
- `IsPttQxTargetInline`: CYC = 2 ✓
- `IsAtmTargetNameInline`: CYC = 2 ✓
- No method approaches CYC = 8 ✓

**NT8 Check**: PASS
- `System.Reflection` in T_BUG_QX_BE_07: acceptable in test files ✓
- xUnit `[Fact]` only — no `[Theory]`, no NUnit, no MSTest ✓
- No DateTime.Now, no CreateOrder ✓

**Test Coverage**: PASS
- 7 `[Fact]` methods cover all spec validation assertions ✓
- Inline helpers consistent with updated production implementation ✓
- T_BUG_QX_BE_07 reflection test enforces `IsAtmTargetName` invariant ✓

**Scan Checklist**: PASS — all 7 present
| Scan | Label | Present |
|------|-------|---------|
| SCAN-01 | JS-021 lock — no lock in test methods | YES |
| SCAN-02 | JS-002 null — no return null | YES |
| SCAN-03 | JS-033 async void — all synchronous | YES |
| SCAN-04 | NT8-006 LINQ — N/A for test files | YES |
| SCAN-05 | CYC ≤ 8 — each [Fact] CYC=1 | YES |
| SCAN-06 | xUnit [Fact] only | YES |
| SCAN-07 | No new state fields | YES |

**File Routing**: PASS — `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs` (Wave workspace)

**VERDICT: TICKET_REVIEW_PASS**

---

### Re-Gate Aggregate Results

| Check | T1 | T2 | T3 |
|-------|----|----|-----|
| Traceability (plan + spec) | PASS | PASS | PASS |
| JS Pre-Check (JS-001/002/021/033) | PASS | PASS | PASS |
| CYC Pre-Check (≤ 8) | PASS | PASS | PASS |
| NT8 Constraints | PASS | PASS | PASS |
| Test Coverage (all methods have [Fact]) | PASS | PASS | PASS |
| Scan Checklist (SCAN-01 through SCAN-07) | PASS | PASS | PASS |
| File Routing (Wave workspace .cs paths) | PASS | PASS | PASS |

### Resolved Violations

| # | From Iteration 1 | Resolution |
|---|-----------------|------------|
| V1 | T1 Traceability FAIL — implementation divergence (plan used Substring; ticket used char-indexed) | RESOLVED — plan updated to char-indexed; plan and ticket now identical |

### Resolved Warnings

| # | From Iteration 1 | Resolution |
|---|-----------------|------------|
| W2 | T3 `IsPttQxTargetInline` oracle would diverge if T1 was restored to Substring | RESOLVED — T1 kept char-indexed; T3 inline helper matches |

### Remaining Warnings (non-blocking)

| # | Ticket | Warning |
|---|--------|---------|
| W1 | T2 | B41 regression test IDs (T_B41_09, T_B41_10, T_B41_11) not explicitly cited in T2 as must-remain-passing. Functional argument is present in the cross-ticket invariants table. Recommend architect add a one-line citation in T2 for engineer clarity. Non-blocking. |
| W3 | T3 | Plan §XUNIT TEST PLAN listed 12 named stubs; ticket delivers 7. Null/empty/short edge-case stubs from the plan are not individually named in T3. These cases are implicitly covered: `IsPttQxTarget(null)` and length != 9 are rejected by the first guard, and T_BUG_QX_BE_02 rejects the short/wrong-suffix cases. Non-blocking; edge cases are functionally covered. |

---

## Overall (Iteration 2): **TICKET_REVIEW_PASS**

All 3 tickets pass all 7 checks. Zero violations remain. Warnings W1 and W3 are non-blocking.

**Engineer may be spawned.** Execution order: T1 → T2 → T3 → `dotnet build` → `dotnet test`.
