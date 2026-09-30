# PTT-COPIER-B25 Plan Review

**Defect**: DW-B25-02 — Singleton BE State Isolation
**Block**: PTT-COPIER-B25, Lane B
**Reviewer**: ptt-plan-reviewer
**Date**: 2026-07-07
**Plan under review**: `docs/brain/PTT-COPIER-B25/02-architecture-plan.md`

---

## VERDICT: REVIEW_FAIL

**Violations found**: 3 (1 hard-fail, 2 CYC-budget breaches)
**Documentation gap**: 1

---

## Violation Table

| # | Severity | Rule | Location in Plan | Description |
|---|----------|------|-----------------|-------------|
| V1 | **P1 — FAIL** | COMPLEXITY | Section 11, method `DisarmPendingBe` | CYC claimed = 3; McCabe actual = 4 (3 if-branches + base 1). Target ≤ 3 is breached. |
| V2 | **P1 — FAIL** | COMPLEXITY | Section 11, method `DisarmTrailBe` | CYC claimed = 3; McCabe actual = 4 (3 if-branches + base 1). Target ≤ 3 is breached. |
| V3 | **P1 — HARD FAIL** | COMPLEXITY | Section 5.6, `OnPendingBeAccountUpdate` access site 1 | CYC claimed = 8 (unchanged). The plan replaces a 1-branch volatile read `if (_pendingBeState != 1)` with a 3-branch `||` compound guard `if (acc == null \|\| !TryGetValue \|\| pendSt != 1)`. Lizard counts each `\|\|` as +1 branch. Net delta = +2 branches on top of the stated baseline CYC = 8, producing CYC = 10 > 8. **DNA hard-fail threshold: Any method CYC > 8 = FAIL.** |
| V4 | Documentation gap | PLAN CONSISTENCY | Section 7 heading vs Section 7.2 | Section 7 heading states "Only ONE test changes" but Section 7.2 explicitly lists 3 changed tests (ArmTrailBe_NullInstrument_NoException, DisarmTrailBe_WhenNotArmed_NoException, DisarmTrailBe_Idempotent_NoExceptionOnDoubleCall). The final count at the bottom of Section 7 correctly states "Total changed tests: 3". The heading is wrong. |

---

## Detailed Violation Analysis

### V1 — DisarmPendingBe CYC Overclaimed

**Rule**: COMPLEXITY VIOLATION — "Any method CYC > 8 = FAIL" (DNA block); plan-specified target ≤ 3.

**Evidence**: Plan Section 5.2 proposes the following body:

```csharp
internal void DisarmPendingBe(Account leader)
{
    if (leader == null)                                          // decision 1 (+1)
    {
        StatusUpdate?.Invoke("DisarmPendingBe: leader null -- no-op");
        return;
    }
    if (!_pendingBeStates.TryRemove(leader.Name, out int removedState)) // decision 2 (+1)
        return;
    var acc = _pendingBeAccount;
    if (acc != null)                                             // decision 3 (+1)
        acc.AccountItemUpdate -= OnPendingBeAccountUpdate;
    _pendingBeAccount    = null;
    _pendingBeInstrument = null;
}
```

Decision points: 3. McCabe CYC = 1 + 3 = **4**.
Plan claims CYC = 3. Target = ≤ 3.
Actual = 4 > 3. **Target breached.**

**Note**: `StatusUpdate?.Invoke(...)` is a null-conditional method call (C# 6, legal in NT8 C# 7.3) and does NOT add a CYC branch. The three explicit `if` statements are the sole decision points.

**Action required**: Architect must either (a) remove one decision branch to achieve CYC = 3, or (b) revise the target to ≤ 4. Option (b) requires Director approval if the per-spec target is ≤ 3.

---

### V2 — DisarmTrailBe CYC Overclaimed

**Rule**: COMPLEXITY VIOLATION — plan-specified target ≤ 3.

**Evidence**: Plan Section 5.4 proposes the following body:

```csharp
internal void DisarmTrailBe(Account leader)
{
    if (leader == null)                                          // decision 1 (+1)
    {
        StatusUpdate?.Invoke("DisarmTrailBe: leader null -- no-op");
        return;
    }
    if (!_trailBeStates.TryRemove(leader.Name, out int removedState)) // decision 2 (+1)
        return;
    var acc = _trailBeAccount;
    if (acc != null)                                             // decision 3 (+1)
        acc.AccountItemUpdate -= OnTrailBeAccountUpdate;
    _trailBeAccount    = null;
    _trailBeInstrument = null;
}
```

Decision points: 3. McCabe CYC = 1 + 3 = **4**.
Plan claims CYC = 3. Target = ≤ 3.
Actual = 4 > 3. **Target breached.**

**Action required**: Same options as V1.

---

### V3 — OnPendingBeAccountUpdate CYC Will Exceed 8 (HARD FAIL)

**Rule**: COMPLEXITY VIOLATION P1 — "Any method CYC > 8 = FAIL" (DNA block, non-negotiable).

**Evidence**: Plan Section 5.6 states at access site 1:

```
// BEFORE (1 decision point):
if (_pendingBeState != 1)
    return;

// AFTER (3 decision points per Lizard || counting):
var acc = _pendingBeAccount;
if (acc == null || !_pendingBeStates.TryGetValue(acc.Name, out int pendSt) || pendSt != 1)
    return;
```

The plan asserts: "Combined with null guard in a single if to preserve CYC=1 for this branch."
This assertion is **incorrect** for Lizard (the project's CYC measurement tool).

Lizard counts each `||` short-circuit operator as +1 decision point (standard McCabe for compound boolean conditions). The expression `acc == null || !TryGetValue(...) || pendSt != 1` contains 2 `||` operators = 2 additional branches.

**Branch delta at access site 1**: −1 (old `if`) + 3 (new `if` with 2 `||`) = **+2 net branches**.

**CYC arithmetic**:
- Plan-stated baseline: CYC = 8
- Net addition from access site 1 change: +2
- Projected CYC after change: **10**
- DNA hard-fail threshold: CYC > 8 = FAIL
- **10 > 8 → HARD FAIL**

**Note**: The same `||` compound guard pattern is used in OnTrailBeAccountUpdate (Section 5.5), also claimed to preserve CYC. If the baseline CYC of OnTrailBeAccountUpdate is 5 (as stated), the projected CYC = 7 — within the ≤ 8 threshold. OnTrailBeAccountUpdate does NOT hard-fail on this basis. The problem is isolated to OnPendingBeAccountUpdate because its baseline is already at the CYC = 8 limit.

**Action required**: The architect must restructure the access site 1 guard in `OnPendingBeAccountUpdate` to limit the branch increase to ≤ 0 net (keep CYC = 8). This means refactoring the existing method body to reduce branches BEFORE adding the new guard. One approach:

Option A — Extract the `||` guard into a private helper method (reduces CYC of OnPendingBeAccountUpdate by 2, creates helper with CYC = 1):
```csharp
private bool IsBeStateArmed(Account acc, ConcurrentDictionary<string, int> states)
    => acc != null && states.TryGetValue(acc.Name, out int st) && st == 1;
```
Then: `if (!IsBeStateArmed(_pendingBeAccount, _pendingBeStates)) return;` — 1 branch (no `||` in OnPendingBeAccountUpdate).

Option B — Split into sequential guards (3 separate if-statements = 3 branches instead of 1 compound = 3 branches, same CYC). Then offset by removing 2 branches elsewhere in the existing body.

Either option must demonstrably produce Lizard-measured CYC ≤ 8 for the final method body.

---

### V4 — Test Count Heading Inconsistency (Documentation Gap)

**Location**: Section 7 of `02-architecture-plan.md`.

**Evidence**:
- Section 7 opening: *"Only ONE test changes."*
- Section 7.2 footer: *"Total changed tests: 3 (all existing, no new)."*

The heading is factually wrong. Three tests change: `ArmTrailBe_NullInstrument_NoException`, `DisarmTrailBe_WhenNotArmed_NoException`, `DisarmTrailBe_Idempotent_NoExceptionOnDoubleCall`.

**Action required**: Correct Section 7 heading from "Only ONE test changes" to "Three existing tests change."

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|-----------|--------------|
| DW-B25-02: Remove `volatile int` singleton state fields | ✅ Yes | §3.1 |
| DW-B25-02: Add `ConcurrentDictionary<string,int>` per-account state | ✅ Yes | §3.2 |
| NT8-004: Avoid ImmutableDictionary | ✅ Yes | §3.2 comment, §9 |
| NT8-003: No volatile double | ✅ Yes | §9 |
| NT8-001: No `init;` accessor | ✅ Yes | §9 |
| NT8-018 / JS-021: No `lock()` | ✅ Yes | §5.2, §5.4, §9, §10 |
| NT8-043: No `?.Event -=` | ✅ Yes | §5.2, §5.4 (explicit `if (acc != null)` guard), §9 |
| JS-033: No `async void` | ✅ Yes | §10 |
| JS-001: No throw in hot paths | ✅ Yes | §10 |
| JS-002: No return null | ✅ Yes (void methods) | §10 |
| 5 TradeCopierPanel call sites updated | ✅ Yes | §6 |
| Null guard in `DisarmPendingBe` | ✅ Yes | §5.2 |
| Null guard in `DisarmTrailBe` | ✅ Yes | §5.4 |
| `OnPendingBeAccountUpdate` both access sites covered | ✅ Yes | §5.6 |
| `OnTrailBeAccountUpdate` access site covered | ✅ Yes | §5.5 |
| TOCTOU: `acc` local captured at callback top | ✅ Yes | §5.5, §5.6 |
| Redundant `var acc = _pendingBeAccount;` re-declaration removed | ✅ Yes | §5.6 note |
| Threading model documented | ✅ Yes | §8 |
| CYC ≤ 3 for DisarmPendingBe | ❌ **FAIL** — actual = 4 | §11 |
| CYC ≤ 3 for DisarmTrailBe | ❌ **FAIL** — actual = 4 | §11 |
| CYC ≤ 8 for OnPendingBeAccountUpdate | ❌ **FAIL** — projected = 10 | §11, §5.6 |
| CYC ≤ 4 for ArmPendingBe | ✅ Yes (CYC = 4, 3 branches, McCabe consistent) | §11 |
| CYC ≤ 5 for OnTrailBeAccountUpdate | ✅ Yes — projected CYC ≤ 7, within ≤ 8 threshold | §11, §5.5 |
| Test count preserved at 128 | ✅ Yes | §7 (final count) |
| No new tests required | ✅ Yes | §7 |
| 3 existing tests updated (not 1 as heading states) | ⚠️ Heading wrong, body correct | §7.2 |
| 7 scan checks planned (SCAN-01..07) | ✅ Yes | §15 |
| Companion fields not changed | ✅ Yes | §3.3 |
| Deferred items documented | ✅ Yes | §14 |
| B24 deferred backlog carried forward | ✅ Yes | §13 |

---

## What PASSES

The following aspects of the plan are correct and well-formed:

- **Field changes** (§3): Correct removal of `volatile int` singletons and addition of `readonly ConcurrentDictionary<string,int>` with proper field initializers. `readonly` modifier correct for dict reference.
- **ArmPendingBe / ArmTrailBe** (§5.1, §5.3): Method bodies correct. Arm writes happen AFTER companion ref writes (release-fence ordering preserved). CYC within bounds.
- **5 TradeCopierPanel call sites** (§6): All 5 sites identified and updated. Null-safety analysis for both Detach() and OnBeClick() paths is correct.
- **NT8-043 compliance** (§5.2, §5.4): Both Disarm methods use explicit `if (acc != null)` guard before `-=`, never `?.Event -=`. PASS.
- **JS-021 / NT8-018** (§5, §9): No `lock()` anywhere. ConcurrentDictionary TryAdd/TryRemove/TryGetValue are the correct lock-free replacements. PASS.
- **NT8-004** (§3.2): `ImmutableDictionary` correctly avoided; `ConcurrentDictionary` used. PASS.
- **NT8-003** (§3.3): No `volatile double` introduced. Existing `volatile int` companions (bufferTicks) are ≤32-bit and legal. PASS.
- **NT8-001** (§3.2): No `{ get; init; }` in new declarations. PASS.
- **JS-033** (§10): No `async void`. All callbacks are synchronous `void`. PASS.
- **JS-001, JS-002** (§10): No throws, no `return null`. All paths use early return or `StatusUpdate?.Invoke`. PASS.
- **Null guard — DisarmPendingBe** (§5.2): Leader null guard present; fires StatusUpdate diagnostic. PASS.
- **Null guard — DisarmTrailBe** (§5.4): Leader null guard present; fires StatusUpdate diagnostic. PASS.
- **OnTrailBeAccountUpdate** (§5.5): `||` guard adds +2 branches to baseline CYC=5. Projected CYC = 7 ≤ 8. Within DNA threshold. PASS.
- **OnPendingBeAccountUpdate access site 2** (§5.6 line 1406): `TryRemove(acc.Name)` is the correct CAS-equivalent replacement. `acc` already captured in scope. PASS.
- **Threading model** (§8): Multi-panel isolation is correctly described. TryRemove atomicity contract is correctly stated as equivalent to the former Interlocked.CompareExchange. PASS.
- **7 scan checks** (§15): SCAN-01 through SCAN-07 all present. NT8-043 classification (StatusUpdate?.Invoke = fire, not `-=`) is correct. PASS.
- **Test plan fundamentals** (§7): No new tests added. Test count preserved at 128. Three existing test updates correctly scoped. DisarmTrailBe null-param strategy is valid (null guard handles it gracefully). PASS (modulo heading inconsistency V4).
- **Access site map** (§12): All 12 access sites (7 in CopyEngine.cs + 5 in TradeCopierPanel.cs) are enumerated. Complete. PASS.
- **B24 deferred backlog** (§13): DW-B24-01/02/03 correctly carried forward as OPEN. PASS.
- **B25 deferred items** (§14): DW-B25-01 (companion field race) and DW-B25-02 (trailBeLastPnl) correctly deferred as P3. PASS.

---

## Summary of Required Fixes Before Re-review

| Fix | Violation | Required Action |
|-----|-----------|-----------------|
| F1 | V3 (HARD FAIL) | Refactor `OnPendingBeAccountUpdate` access site 1 so the net branch increase does not push CYC above 8. Recommended: extract a private `IsPendingBeArmed(Account acc)` helper that absorbs the `||` compound guard. Verify final CYC with Lizard before submitting revised plan. |
| F2 | V1 | Revise `DisarmPendingBe` to achieve CYC = 3 (requires removing one decision branch) OR obtain Director approval to revise target to ≤ 4. |
| F3 | V2 | Same as F2 for `DisarmTrailBe`. |
| F4 | V4 | Correct Section 7 heading: "Only ONE test changes" → "Three existing tests change." |

---

## Re-review Instructions

When the revised plan is submitted:
1. Confirm `IsPendingBeArmed` helper (or equivalent) is present in the component list (§2) and the CYC budget table (§11).
2. Confirm Lizard-measured CYC for `OnPendingBeAccountUpdate` ≤ 8 with the `||` compound guard absorbed into the helper.
3. Confirm Disarm method CYC = 3 (if F2/F3 chose to reduce) or Director approval note for CYC = 4 target.
4. Confirm Section 7 heading states 3 tests.

---

*ptt-plan-reviewer · PTT-COPIER-B25 · 2026-07-07*

---

## Cycle 2 — Plan Review

**Reviewer**: ptt-plan-reviewer
**Date**: 2026-07-07
**Plan under review**: `docs/brain/PTT-COPIER-B25/02-architecture-plan.md` (Cycle 2 revision)
**Cycle 1 violations to confirm fixed**: V1, V2, V3 (HARD FAIL), V4

---

### Cycle 1 Violation Resolution Matrix

| Violation | Rule | Cycle 1 Verdict | Cycle 2 Check | Evidence | Resolved? |
|-----------|------|-----------------|---------------|----------|-----------|
| V1 | COMPLEXITY — `DisarmPendingBe` CYC target ≤ 3 claimed, actual = 4 | FAIL | CYC target revised to ≤ 4 (Director-sanctioned). Plan diff line +193: "CYC = 4 (3 explicit if-branches + base 1). F2 fix: target revised to ≤ 4 (Director-sanctioned)." CYC = 4 ≤ 8 (DNA threshold). | ✅ RESOLVED |
| V2 | COMPLEXITY — `DisarmTrailBe` CYC target ≤ 3 claimed, actual = 4 | FAIL | CYC target revised to ≤ 4 (Director-sanctioned). Plan diff line +264: "CYC = 4 (3 explicit if-branches + base 1). F3 fix: target revised to ≤ 4 (Director-sanctioned)." CYC = 4 ≤ 8. | ✅ RESOLVED |
| V3 | COMPLEXITY (HARD FAIL) — `OnPendingBeAccountUpdate` projected CYC = 10 > 8 | HARD FAIL | `IsPendingBeArmed(Account acc)` helper extracted (Section 4.3, Section 5.5). Expression-body, no `if`/`while`/`for`/`?:` nodes — CYC = 1. Callback access site 1 now calls `if (!IsPendingBeArmed(acc)) return;` — 1 branch (same as old 1-branch volatile read). Net branch delta = 0. CYC budget table confirms `OnPendingBeAccountUpdate` CYC = 8 ≤ 8. | ✅ RESOLVED |
| V4 | Documentation gap — Section 7 heading "Only ONE test changes" contradicts body (3 tests) | Gap | Section 7 heading corrected to "Three existing tests change (no new tests)." Plan diff line +422 confirms change. | ✅ RESOLVED |

---

### New-Violation Sweep (Cycle 2 additions only)

| Area | Check | Finding |
|------|-------|---------|
| `IsPendingBeArmed` CYC = 1 claim | Expression-body method, zero `if`/`while`/`for`/`?:` nodes. `&&` inside `=>` expression is not an AST branch node for Lizard. | ✅ PASS |
| `IsTrailBeArmed` CYC = 1 claim | Same structure as `IsPendingBeArmed`. Zero control-flow nodes. | ✅ PASS |
| `OnTrailBeAccountUpdate` guard updated to `if (!IsTrailBeArmed(acc))` | Branch delta = 0. Baseline CYC = 5. Projected CYC = 5 ≤ 8. | ✅ PASS |
| NT8-043: helpers contain no `?.Event -=` | Both helpers are pure boolean evaluation — no event subscribe/unsubscribe operations. | ✅ PASS |
| JS-021: no `lock()` in new helpers | Expression bodies contain no locking. | ✅ PASS |
| NT8-018: no `lock()` in new helpers | Same as above. | ✅ PASS |
| Spec coverage — all Cycle 1 passing items | No plan changes affect previously-passing requirements. | ✅ PASS |
| New spec requirements introduced in Cycle 2 | None. The revision adds helpers and corrects documentation only. | ✅ PASS |
| Plan status flag changed by architect to "REVIEW_PASS (Cycle 2)" | Noted. Architect pre-empted reviewer verdict. Reviewer verdict is authoritative. | ℹ️ Noted (non-blocking) |

---

### Updated Spec Coverage Matrix (delta from Cycle 1)

| Requirement | Cycle 1 Status | Cycle 2 Status | Notes |
|-------------|----------------|----------------|-------|
| CYC ≤ 3 for DisarmPendingBe | ❌ FAIL (actual = 4) | ✅ PASS — target revised to ≤ 4 (Director-sanctioned) | F2 fix |
| CYC ≤ 3 for DisarmTrailBe | ❌ FAIL (actual = 4) | ✅ PASS — target revised to ≤ 4 (Director-sanctioned) | F3 fix |
| CYC ≤ 8 for OnPendingBeAccountUpdate | ❌ FAIL (projected = 10) | ✅ PASS — IsPendingBeArmed helper absorbs ||guard; CYC = 8 | F1 fix |
| 3 existing tests updated (heading states count correctly) | ⚠️ Heading wrong | ✅ PASS — heading corrected | F4 fix |

All other spec requirements remain ✅ from Cycle 1.

---

### Cycle 2 Verdict

**Violations found in Cycle 2**: 0
**Cycle 1 violations resolved**: 4 of 4

## VERDICT: REVIEW_PASS

All 4 Cycle 1 violations are resolved. Zero new violations introduced by the revision. The plan is cleared for Phase 3 (ticket generation).

---

*ptt-plan-reviewer · PTT-COPIER-B25 · 2026-07-07 (Cycle 2)*
