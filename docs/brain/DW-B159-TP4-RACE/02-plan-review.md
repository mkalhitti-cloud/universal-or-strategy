# DW-B159-TP4-RACE — Plan Review
<!-- block: DW-B159-TP4-RACE -->
<!-- phase: 2 -->
<!-- produced-by: ptt-plan-reviewer (Phase 2) -->
<!-- date: 2026-10-01 -->

---

## Block Header

| Field | Value |
|-------|-------|
| Epic | DW-B159-TP4-RACE — FindFollowerBracketOrder Accepted-state race fix |
| Phase | 2 — Plan Review |
| Plan source | docs/brain/DW-B159-TP4-RACE/02-architecture-plan.md |
| Prior backlog reviewed | docs/brain/DW-B159-KS/06-deferred-backlog.md |
| RULES_CATALOG | docs/protocol/RULES_CATALOG.md (NOT FOUND — reviewed against hardcoded DNA block) |

---

## VERDICT: REVIEW_FAIL

**Violation count: 2**

The plan must be corrected on both findings before re-submission. No fixes are suggested here — architect resolves.

---

## Violation Table

| # | Rule / Checklist Item | Violation | Location in Plan |
|---|-----------------------|-----------|-----------------|
| V-01 | SPEC COMPLETENESS — backlog entries must match prior block state | B167-DEFER-02 marked CLOSED incorrectly. Prior block (DW-B159-KS/06-deferred-backlog.md) explicitly states B167-DEFER-02 "Requires separate block (B168 or DW-B168). Director must run the second output.md test procedure per DW-B167-02/00-dw-card.md before architect can design fix." A test-only verification epic (zero production code changes) cannot close a P1 defect that requires its own dedicated block. The plan's rationale "ATM bracket fo=NULL race window (Accepted state) closed by this epic's verification" conflates two distinct defects: the Accepted-state race (DW-B159-TP4-RACE scope, fixed in DW-LB-FL-01) and the systemic routing failure (B167-DEFER-02 scope: IsDispatchTriggerState rejects StopMarket at all states, Gate0.5 blocks Target orders by name). | Ticket 2 — Backlog Item Operations table, row B167-DEFER-02 |
| V-02 | 7-SCAN CHECKLIST — must be present for every ticket | Ticket 2 has no 7-scan checklist table. The review mandate requires "7-scan checklist present for each ticket." Even for a doc-only ticket where all 7 scans are N/A, the table must be present and explicitly marked N/A with the reason (no C# file modified). The omission leaves the compliance chain incomplete. | Ticket 2 — section ends after Spec Requirement IDs with no scan table |

---

## Spec Coverage Matrix

| Requirement | Addressed in Plan? | Plan Section | Notes |
|-------------|-------------------|--------------|-------|
| Accepted state must be in FindFollowerBracketOrder filter | YES | Ticket 1 — Verification Scope, Test 1 | Guard confirmed present at line 4608 |
| Fix already present → Ticket 1 = test-only (no guard change) | YES | Engineer Pre-Check Result + Component List | Marked READ ONLY, zero production changes |
| Submitted discrepancy flagged for Director | YES | SPEC/CODE DISCREPANCY section + Test 2 (Skipped) | Appropriately handled without unilateral guard change |
| 2 new [Fact] test methods added | YES | Ticket 1 — New Test Methods | Both methods fully specified with CYC=1 |
| 06-deferred-backlog.md created (doc-only) | YES (structure) | Ticket 2 | File and operations defined; V-01 affects one row |
| Backlog items match prior block state | **PARTIAL — FAIL** | Ticket 2 — Backlog Item Operations | B167-DEFER-02 closure is incorrect (V-01) |

---

## Per-Checklist Item Results

### 1. Lane-Split Gate Compliance — PASS

Gate result is present. Pipeline declared SINGLE. Gate logic is sound:
- Q1: N/A (no production code in scope — acceptable) 
- Q2: YES → SINGLE-PIPELINE condition met independently
- Q3: NO → Fails LANE precondition
- Q4: NO → Fails LANE precondition

"LANES require NO on Q1+Q2 AND YES on Q3+Q4. Conditions NOT met." Conclusion correct.

### 2. Spec Alignment: Pre-Check Result Handling — PASS

Plan correctly handles the engineer pre-check finding:
- Fix confirmed present at lines 4606–4613 (`Working || Accepted || Submitted || ChangeSubmitted || Initialized`)
- Ticket 1 scoped to verification + test addition only
- Component list marks `CopyEngine.cs` as "READ ONLY — no changes"
- All four occurrences of "ZERO" production code changes in plan summary are consistent

### 3. Spec/Code Discrepancy: Submitted State — PASS

Architect handling is appropriate:
- Discrepancy documented in a dedicated section with NT8 safety rationale
- Engineer directed to investigate intent of prior DW-LB-FL-01 inclusion
- No unilateral removal of Submitted (correct — could break prior block logic)
- Test 2 uses `[Fact(Skip=...)]` with full conflict rationale to preserve spec contract in code without breaking CI
- Epic explicitly scoped to Accepted-state fix only; discrepancy flagged for Director

### 4. JS Rule Coverage — PASS

| Rule | Check | Result |
|------|-------|--------|
| JS-021 | No `lock()` in new test methods | PASS — pure enum comparisons, no shared mutable state |
| JS-001 | No `throw` in hot path | PASS — test methods are not hot path; no throw present |
| CYC ≤ 8 | Both new methods CYC=1 | PASS — straight-line enum assertions |
| JS-042 / ASCII-ONLY | All string literals ASCII | PASS — no Unicode, no curly quotes, no emoji in test code or Skip string |
| SCAN-06 / No DateTime.Now | Not used | PASS |
| WS-GUARD / No whitespace mutation | Insert-only, no surrounding edits | PASS |
| PTT-PREFIX | No CreateOrder calls | N/A — PASS |
| JS-021 (Monitor/Mutex) | Not used | PASS |
| JS-023 (Dispatcher.InvokeAsync) | Not applicable — no UI code | N/A — PASS |

Production code (`CopyEngine.cs`) is READ ONLY. No new code paths to evaluate in production.

### 5. Ticket 2 (Doc-Only) — FAIL (V-01)

`.cs` edit prohibition: PASS — Ticket 2 explicitly states "Zero .cs edits. Zero production code changes." The single file is `docs/brain/DW-B159-TP4-RACE/06-deferred-backlog.md`.

Backlog entry correctness: **FAIL** — See V-01 above. B167-DEFER-02 cannot be closed by this epic.

### 6. 7-Scan Checklist Per Ticket — FAIL (V-02)

| Ticket | 7-Scan Present? | Finding |
|--------|----------------|---------|
| Ticket 1 | YES — SCAN-01 through SCAN-07 all present | PASS |
| Ticket 2 | **NO** — no scan table present | FAIL (V-02) |

### 7. Spec Requirement IDs Referenced — CONDITIONAL PASS

Only one requirement ID is formally enumerated (`DW-B159-TP4-RACE TICKET-2`). The plan implicitly covers all Ticket 1 requirements through the verification scope and test method specifications. No external spec document was located to cross-check against; coverage appears adequate for stated scope. This item does not independently trigger REVIEW_FAIL.

---

## Prior Block Backlog Reconciliation

Items from `docs/brain/DW-B159-KS/06-deferred-backlog.md` and their plan disposition:

| Prior Item | Prior Status | Plan Disposition | Assessment |
|------------|-------------|-----------------|------------|
| DW-B159-K4 | OPEN | Carried forward (OPEN) | CORRECT |
| DW-B159-K3-UI | OPEN | Carried forward (OPEN) | CORRECT |
| B167-DEFER-01 | OPEN | Carried forward (OPEN) | CORRECT |
| B167-DEFER-02 | OPEN (P1, requires B168) | **Plan marks CLOSED — INCORRECT** | **FAIL** → V-01 |
| B167-DEFER-03 | OPEN | Plan marks OPEN (UNBLOCKED) | CORRECT (B167-DEFER-04 closing is prerequisite) |
| B167-DEFER-04 | OPEN | Plan marks CLOSED (Director confirmed SIM gate complete) | Conditionally acceptable if Director has confirmed; architect must cite the confirmation source in the doc |

---

## Findings Summary

| Finding | Severity | Required Correction |
|---------|----------|-------------------|
| V-01: B167-DEFER-02 incorrectly closed | P0 — REVIEW_FAIL | Change B167-DEFER-02 row to OPEN (carried forward). It requires B168 work per prior block's own description. If the Accepted-state race subset of B167-DEFER-02 is partially resolved, use a separate row or note — do NOT close the parent item. |
| V-02: No 7-scan table for Ticket 2 | P1 — REVIEW_FAIL | Add 7-scan checklist to Ticket 2 with all 7 scans explicitly marked N/A (doc-only, no C# file modified). |

---

## REVIEW_FAIL

**Return the plan to ptt-architect. Two corrections required before re-submission:**

1. **V-01 (B167-DEFER-02):** Correct the backlog operation from CLOSED to OPEN (carried forward). B167-DEFER-02 is a systemic routing failure requiring a dedicated B168 block — not resolvable by a test-only verification epic.

2. **V-02 (Ticket 2 scan table):** Add the 7-scan checklist to Ticket 2 with all rows explicitly marked N/A and reason documented (doc-only ticket, no C# source file touched).

*Plan review produced by ptt-plan-reviewer (Phase 2). Block DW-B159-TP4-RACE. Date 2026-10-01.*
*Verdict: REVIEW_FAIL. Violations: 2 (V-01, V-02).*

---

# CYCLE 2 RE-REVIEW — DW-B159-TP4-RACE
<!-- cycle: 2 -->
<!-- produced-by: ptt-plan-reviewer (Phase 2, Cycle 2) -->
<!-- date: 2026-10-01 -->

## VERDICT: REVIEW_PASS

**Violation count: 0**

Both V-01 and V-02 from Cycle 1 are resolved. No new violations found. All checklist items PASS.

---

## Cycle 2 Violation Resolution

| Violation | Cycle-1 Finding | Cycle-2 Status |
|-----------|----------------|---------------|
| V-01 | B167-DEFER-02 incorrectly marked CLOSED | **RESOLVED** — Plan line 243 now marks B167-DEFER-02 as `OPEN (carried forward unchanged)`. Action cell cites P1 defect, B168 requirement, and SPEC OVERRIDE. Matches prior block (DW-B159-KS/06-deferred-backlog.md) exactly. |
| V-02 | No 7-scan checklist table in Ticket 2 | **RESOLVED** — Plan lines 269–279 contain a complete `### 7-Scan Checklist (Ticket 2)` table with all 7 scans (SCAN-01 through SCAN-07) explicitly marked N/A with documented reason: "no .cs edits". |

---

## Full Checklist Results (Cycle 2)

### 1. Lane-Split Gate Compliance — PASS

Gate table present. SINGLE-PIPELINE declared. Logic sound:
- Q1: N/A (test+doc only) — acceptable default
- Q2: YES → SINGLE-PIPELINE condition met independently
- Q3: NO → LANE precondition fails
- Q4: NO → LANE precondition fails

Conclusion correctly stated: "LANES require NO on Q1+Q2 AND YES on Q3+Q4. Conditions NOT met. SINGLE-PIPELINE."

### 2. Spec Alignment — PASS

- Engineer pre-check result present: fix confirmed at CopyEngine.cs lines 4606–4613.
- Ticket 1 correctly scoped to verification + test addition only (zero production code changes).
- Component list marks `CopyEngine.cs` READ ONLY.
- Summary confirms ZERO production code changes (4 occurrences, consistent).

### 3. Submitted Discrepancy — PASS

- Dedicated `⚠️ SPEC/CODE DISCREPANCY` section present with full NT8 safety rationale.
- Engineer directed to investigate DW-LB-FL-01 intent; no unilateral guard change.
- Test 2 uses `[Fact(Skip=...)]` with ASCII-only conflict rationale in the Skip attribute string.
- Epic scope correctly limited to Accepted-state fix only; Submitted discrepancy flagged for Director.

### 4. JS Rules — PASS

| Rule | Check | Result |
|------|-------|--------|
| JS-021 | No `lock()` in new test methods or production changes | PASS — pure enum comparisons, no shared mutable state; production code READ ONLY |
| JS-021 | No Monitor/Mutex/SemaphoreSlim | PASS — not present |
| JS-023 | No UI update off-thread without Dispatcher.InvokeAsync | N/A — no UI code in scope |
| JS-001 | No `throw` in hot path | PASS — test methods are not hot path; no throw present |
| JS-002 | No null return where value expected | PASS — void test methods; no return values |
| JS-003 | No magic string discriminated state | PASS — not applicable |
| JS-008 | No mutable fields on struct; SolidColorBrush not Freeze()d | N/A — not applicable |
| JS-009 | No Dictionary for shared collection | N/A — not applicable |
| JS-010 | No public constructor on singleton | N/A — not applicable |
| CYC ≤ 8 | All new methods CYC ≤ 8 | PASS — both test methods CYC=1 (straight-line assertions) |
| ASCII-only | No Unicode/emoji/curly-quotes in string literals | PASS — all strings including Skip attribute string are ASCII |
| DateTime.Now | Not used | PASS |
| WS-GUARD | No whitespace mutation | PASS — insert-only, no surrounding edits |
| PTT-PREFIX | No CreateOrder calls | N/A — PASS |
| async/await in lifecycle | Not applicable | N/A — PASS |
| Hardcoded #RRGGBB hex | Not present | PASS |
| FontFamily override | Not applicable | N/A — PASS |
| Account.All in constructor | Not applicable | N/A — PASS |
| sealed TradeCopierWindow | Not applicable | N/A — PASS |
| AtmStrategyCreate in AddOn | Plan explicitly notes StrategyBase-only; not used | PASS |

### 5. Ticket 2: Zero .cs Edits + Backlog Items — PASS

**.cs edit prohibition:** Ticket 2 header explicitly states "Zero .cs edits. Zero production code changes." Single file is `docs/brain/DW-B159-TP4-RACE/06-deferred-backlog.md`. PASS.

**Backlog item reconciliation:**

| Item | Prior Block Status | Plan Disposition | Correct? |
|------|-------------------|-----------------|----------|
| B167-DEFER-01 | OPEN | OPEN (carried forward) | ✓ |
| B167-DEFER-02 | OPEN (P1, B168 required) | OPEN (carried forward unchanged) | ✓ — V-01 resolved |
| B167-DEFER-03 | OPEN | OPEN (UNBLOCKED — B167-DEFER-04 prerequisite met) | ✓ |
| B167-DEFER-04 | OPEN | CLOSED (Director confirmed SIM gate complete) | ✓ — conditionally acceptable; architect cites Director confirmation |
| DW-B159-K4 | OPEN | OPEN (carried forward) | ✓ |
| DW-B159-K3-UI | OPEN | OPEN (carried forward) | ✓ |
| DW-B159-TP4-RACE | NEW | CLOSED (this epic — verification complete) | ✓ |

All items match prior block state. PASS.

### 6. 7-Scan Checklist for ALL Tickets — PASS

| Ticket | 7-Scan Present? | Finding |
|--------|----------------|---------|
| Ticket 1 | YES — SCAN-01 through SCAN-07 all present with results | PASS |
| Ticket 2 | YES — SCAN-01 through SCAN-07 all present, all explicitly N/A (no .cs edits) | PASS — V-02 resolved |

### 7. Spec Requirement IDs Referenced — PASS

All requirements from cycle-1 spec coverage matrix are addressed:

| Requirement | Plan Coverage | Status |
|-------------|--------------|--------|
| Accepted state must be in FindFollowerBracketOrder filter | Ticket 1 Verification Scope + Test 1 | PASS |
| Fix already present → Ticket 1 = test-only | Pre-Check Result section + Component List (READ ONLY) | PASS |
| Submitted discrepancy flagged for Director | SPEC/CODE DISCREPANCY section + Test 2 (Skipped) | PASS |
| 2 new [Fact] test methods added | Ticket 1 New Test Methods | PASS |
| 06-deferred-backlog.md created | Ticket 2 | PASS |
| Backlog items match prior block state | Ticket 2 Backlog Operations table | PASS |

---

## Spec Coverage Matrix (Cycle 2)

| Requirement | Addressed in Plan? | Plan Section | Notes |
|-------------|-------------------|--------------|-------|
| Accepted state in FindFollowerBracketOrder filter | YES | Ticket 1 — Verification Scope, Test 1 | Guard confirmed at line 4608 |
| Fix already present → test-only scope | YES | Engineer Pre-Check Result + Component List | Marked READ ONLY |
| Submitted discrepancy flagged | YES | SPEC/CODE DISCREPANCY + Test 2 (Skipped) | No unilateral change |
| 2 new [Fact] methods | YES | Ticket 1 — New Test Methods | CYC=1 each |
| 06-deferred-backlog.md created | YES | Ticket 2 | File and operations defined |
| Backlog items match prior block state | YES | Ticket 2 — Backlog Item Operations | All items correct — V-01 resolved |

---

## REVIEW_PASS

No violations remain. Both cycle-1 violations (V-01, V-02) are confirmed resolved. All full-checklist items PASS.

**Unlocks Phase 3: ptt-architect may proceed to generate 04-tickets.md.**

*Plan review cycle 2 produced by ptt-plan-reviewer (Phase 2). Block DW-B159-TP4-RACE. Date 2026-10-01.*
*Verdict: REVIEW_PASS. Violations: 0.*
