# WAVE1-LANE-A Plan Review -- Cycle 2

**Reviewer**: PTT Plan Reviewer (Phase 2, Cycle 2 -- Final)
**Date**: 2026-09-07
**Plan file**: `docs/brain/WAVE1-LANE-A/02-architecture-plan.md`
**Epic**: CopyEngine.cs God-Method Extraction (A-01 through A-10)

---

## Verdict: REVIEW_PASS

---

## Cycle 1 Violation Resolution

| Violation | Rule | Description | Cycle 1 Status | Cycle 2 Status |
|-----------|------|-------------|----------------|----------------|
| VIOLATION-01 | JS-080 (test completeness) | `RegisterPendingBeSlot` had ZERO named [Fact] tests | REVIEW_FAIL | **RESOLVED** |

**Evidence of resolution (lines 498-501 of revised plan):**

Section 8 now contains a dedicated `RegisterPendingBeSlot` test subsection with two named [Fact]s:
- `RegisterPendingBeSlot_SlotWrittenWithCorrectKeys_WhenBothDelayVariants`
- `RegisterPendingBeSlot_DefaultDelayMs_Is500`

Both use the inline logic-mirror pattern with explicit rationale (NT8 `Account`/`Instrument`/`DispatcherTimer` are not constructible outside the NT8 runtime). This is the correct approach for a `void` NT8-coupled helper. VIOLATION-01 is **RESOLVED**.

---

## Lane-Split Gate Compliance

| Check | Required | Plan Value | Result |
|-------|----------|-----------|--------|
| Gate result phrase present | "LANE-SPLIT GATE RESULT: SINGLE-PIPELINE" | Line 40: `**GATE RESULT: SINGLE-PIPELINE**` | PASS |
| Q1 answered (same file / proximity) | Yes | Lines 42-44: all in `CopyEngine.cs`, span 5000 lines, no proximity requirement | PASS |
| Q2 answered (dependency between methods) | Yes | Lines 46-50: 3 dependency chains identified, signatures constrained | PASS |
| Q3 answered (standalone value) | Yes | Lines 52-53: each extraction is self-contained | PASS |
| Q4 answered (independent verification) | Yes | Lines 55-56: `lizard -T cyclomatic_complexity=8` per method | PASS |

**Gate Note:** The plan uses `**GATE RESULT: SINGLE-PIPELINE**` (under the section header `## 2. Lane-Split Gate Result`). The Q1-Q4 answers are present and complete. Accepted as compliant.

---

## JS Rule Compliance Table

| Rule | Check | Plan Evidence | Result |
|------|-------|---------------|--------|
| JS-080 (CYC <= 8 per helper) | All proposed helpers CCN <= 8 | `IsBeRetrySlotNeeded`=4, `RegisterPendingBeSlot`=1, `SubmitLimitExitOrder`=4, `TryResolveEnabledRule`=4 (lines 123, 131, 275, 333) | PASS |
| JS-021 (no lock()) | Zero lock() in any proposed helper | Line 19: scan shows zero actual lock blocks. Line 551: threading table confirms no lock in helpers. | PASS |
| JS-096 (ordering: slot-before-timer) | `RegisterPendingBeSlot` preserves write-before-timer ordering | Lines 136-139 and 385-389: explicit 3-step ordering constraint documented: (1) dict write, (2) log, (3) QueueBeRetryFallback | PASS |
| JS-002 (no null return) | No helper returns null | Line 30: all helpers return `void`, `bool`, or value types. `TryResolveEnabledRule` uses `out CopyRule` (struct -- never null). | PASS |
| JS-001 (no throw in hot path) | No helper throws | Line 29: all helpers use `void`/`bool` returns with guard clauses, no `throw` in helper bodies. `SubmitLimitExitOrder` uses try/catch but does not rethrow -- error is logged via `StatusUpdate`. | PASS |

---

## Per-Helper CCN Verification

| Helper | Plan CCN | Branch Derivation Shown? | CCN <= 8? |
|--------|----------|--------------------------|-----------|
| `IsBeRetrySlotNeeded` | 4 | Yes: base(1) + 3 `&&` boolean ops (lines 130-131) | PASS |
| `RegisterPendingBeSlot` | 1 | Yes: base(1), no branches -- pure assignment + log + delegate call (line 123) | PASS |
| `SubmitLimitExitOrder` | 4 | Yes: base(1) + order null check(1) + try(1) + catch(1) (line 275) | PASS |
| `TryResolveEnabledRule` | 4 | Yes: base(1) + isCopyEnabled(1) + matchedRule null(1) + matchedRule.Enabled(1) (line 333) | PASS |

All four helpers satisfy JS-080 (CCN <= 8). No helper exceeds the limit.

---

## No-Rename-Only Check

| Helper | Parent Before | Parent After | CCN Reduces? | Evidence |
|--------|--------------|-------------|--------------|----------|
| `IsBeRetrySlotNeeded` + `RegisterPendingBeSlot` | A-01: CCN=8 | CCN=6 (delta -2) | YES | Line 134: base(1)+5 guards=6; Section 9 table confirms delta -2 |
| `SubmitLimitExitOrder` | A-07: CCN=8 | CCN=4 (delta -4) | YES | Line 277: base(1)+pos null(2)+isLong ternary(1)=4; Section 9 confirms |
| `SubmitLimitExitOrder` | A-08: CCN=8 | CCN=4 (delta -4) | YES | Line 304: same analysis; Section 9 confirms |
| `TryResolveEnabledRule` | A-09: CCN=8 | CCN=5 (delta -3) | YES | Line 335-336: base(1)+4 remaining calls=5; Section 9 confirms |

No rename-only extractions. Every helper produces a measurable CCN reduction in its parent.

---

## Helper Visibility Check

| Helper | Declared Visibility | Location |
|--------|--------------------|---------  |
| `RegisterPendingBeSlot` | `private void` | Lines 121, 436 |
| `IsBeRetrySlotNeeded` | `private static bool` | Lines 128, 444 |
| `SubmitLimitExitOrder` | `private void` | Lines 272, 453 |
| `TryResolveEnabledRule` | `private bool` | Lines 330, 464 |

Line 431: "All helpers are `private` (never widened). All in class `TrimSignal` in `CopyEngine.cs`."

All helpers are `private`. PASS.

---

## Test Coverage Check

| Helper | [Fact] Count | Fact Names Specified? | xUnit? | NT8 Runtime Limitation Noted? |
|--------|-------------|----------------------|--------|-------------------------------|
| `IsBeRetrySlotNeeded` | 6 | YES (lines 143-150, table lines 483-488) | YES (line 477) | N/A (pure static, no NT8 deps) |
| `RegisterPendingBeSlot` | 2 | YES (lines 500-501) | YES (line 477) | YES (lines 493-496: Account/Instrument/DispatcherTimer not constructible outside NT8) |
| `SubmitLimitExitOrder` | 4 | YES (lines 285-290, table lines 505-510) | YES (line 477) | YES (inline mirror pattern per plan) |
| `TryResolveEnabledRule` | 4 | YES (lines 345-349, table lines 514-519) | YES (line 477) | N/A (advisory tier) |

- All [Fact] names follow `MethodName_Condition_ExpectedBehavior` convention (BDD-style).
- Framework: `**Framework:** xUnit ONLY. NEVER NUnit or MSTest.` (line 477) -- PASS.
- No NUnit or MSTest references anywhere in the plan.
- `RegisterPendingBeSlot` now has 2 named [Fact]s (up from 0 in Cycle 1) -- VIOLATION-01 RESOLVED.

---

## Out-of-Scope CCN Violations

| Method | CCN | Plan Disposition | Correct? |
|--------|-----|-----------------|---------|
| `IsExitSignalName` | 9 | Flagged for separate epic (lines 89-93, 538-543, 597-599) | PASS |
| `HasArmingAtmBrackets` | 9 | Flagged for separate epic (lines 89-94, 538-543, 597-599) | PASS |

Both violating methods are correctly excluded from this epic's scope. Extraction plans are documented (Section 6) for the follow-up epic. This is the correct handling.

---

## Advisory Items (Non-Blocking)

1. **A-04 dead code removal** (advisory): 55 commented-out DW-B88 legacy lines. Plan recommends removal. Non-blocking -- purely NLOC reduction.
2. **A-05 dead variable removal** (advisory): `atmTemplate` local variable is unused and has a latent null assignment. Removal has zero CCN impact.
3. **A-09 advisory tier** (advisory): `TryResolveEnabledRule` is marked ADVISORY. At CCN=8, A-09 is compliant. Extraction is optional for this wave and does not block REVIEW_PASS.
4. **A-10 log extraction** (advisory): `LogBeStopSubmission` extraction is purely NLOC reduction, zero CCN impact.

None of these are violations. All are properly classified as advisory.

---

## Final Verdict: REVIEW_PASS

All Phase 2 checklist items pass:

- [x] VIOLATION-01 from Cycle 1 (RegisterPendingBeSlot has zero [Fact] tests) -- **RESOLVED**
- [x] LANE-SPLIT GATE: SINGLE-PIPELINE present, Q1-Q4 answered
- [x] JS-080: All helpers CCN <= 8 (verified with branch derivation)
- [x] JS-021: No lock() in any helper
- [x] JS-096: Slot-before-timer ordering explicitly documented and constrained
- [x] JS-002: No null return from any helper
- [x] JS-001: No throw in any helper
- [x] No rename-only extractions (all reduce parent CCN)
- [x] All helpers are `private`
- [x] `IsBeRetrySlotNeeded`: 6 named [Fact]s present
- [x] `RegisterPendingBeSlot`: 2 named [Fact]s present (was 0 in Cycle 1)
- [x] `SubmitLimitExitOrder`: 4 named [Fact]s present
- [x] `TryResolveEnabledRule`: 4 named [Fact]s present (advisory tier)
- [x] All tests use xUnit [Fact] (not NUnit/MSTest)
- [x] `IsExitSignalName` (CCN=9) and `HasArmingAtmBrackets` (CCN=9) flagged for separate epic

**REVIEW_PASS** -- Pipeline may advance to Phase 3 (ticket generation).
