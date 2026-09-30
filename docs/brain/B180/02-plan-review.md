# B180 Plan Review
# Block: B180 -- DW-B180-BE-SWAP-ALL
# Produced by: ptt-plan-reviewer (Phase 2)
# Inputs reviewed:
#   docs/brain/B180/02-architecture-plan.md
#   docs/brain/B180-orchestrator-prompt.md
#   docs/standards/RULES_CATALOG.md
# Status: REVIEW_PASS

---

## 1. Executive Summary

**REVIEW_PASS**

All ten mandatory checks (A through J) pass. Zero blocking violations found.
Three minor non-blocking notes are recorded in Section 4.

The plan is approved. Proceed to Ph3 (ticket generation).

---

## 2. Checklist Results

| Check | Gate | Result | Notes |
|-------|------|--------|-------|
| A | Lane-split gate present and complete | **PASS** | Gate result SINGLE-PIPELINE stated; all 4 questions answered. |
| B | B180-1 and B180-2 are separate tickets | **PASS** | Three distinct tickets; staged dependency explicitly enforced. |
| C | CYC <= 8 for all new/changed methods | **PASS** | Worst-case adjusted CYC: PttBeSwapOne.Execute=7, all others lower. |
| D | Jane Street rule compliance | **PASS** | No lock(), no throw, no DateTime.Now, no FontFamily, ASCII-only. |
| E | Deferred-arm logic leak check | **PASS** | No armed-state calls in new OnBeClick or OnGlobalBeClick. |
| F | New class completeness (all required guards) | **PASS** | All five guards present in PttBeSwapOne; all loop guards in PttBeSwapAll. |
| G | SnapshotBeTargetsPublic seam specified | **PASS** | Exact line 6979 insertion point; private SnapshotBeTargets preserved. |
| H | B180-2 removal list completeness | **PASS** | All 13 spec-required symbols present in plan removal tables. |
| I | 5 test cases; correct seam method name | **PASS** | All 5 tests listed; RoundToTickTestable discrepancy documented by architect. |
| J | 7-scan checklist with zero-match patterns | **PASS** | Sections SCAN-01 through SCAN-07 present with explicit expected results. |

---

## 3. Violations

**None.** No blocking violations found.

---

## 4. Minor Notes (non-blocking -- do not cause FAIL)

### MN-01 -- RoundToTick seam name discrepancy (Check I)

**Observation:**
The orchestrator prompt (B180-orchestrator-prompt.md) states:

> `"(Note: RoundToTickPublic test seam already exists at CopyEngine.cs line 4513 -- use it)"`
> `"Call CopyEngine.RoundToTickPublic(4500.125, 0.25)"`

The plan (Section 7, T-B180-T1-05 and Section 11 Decisions) contradicts this:

> `"NOTE: Use CopyEngine.RoundToTickTestable (actual name at line 4514)"`
> `"Orchestrator prompt says 'RoundToPublicPublic' but actual method is RoundToTickTestable. Do NOT add duplicate seam."`

The plan author claims to have read the source and found `RoundToTickTestable` as the actual method name. The Check I reviewer instruction acknowledges both names with "if that's what the source shows," deferring to source evidence. The plan's claim is documented and traceable.

**Risk:** If the actual method name is `RoundToTickPublic`, T-B180-T1-05 will fail to compile. The Ph3 ticket and Ph4b verifier MUST confirm the exact method name before the test is written, and include a `grep -n "RoundToTick" src/PropTraderTools/CopyEngine.cs` scan in the ticket verification checklist.

**Action required (Ph3):** Add explicit grep confirmation of the seam name to the B180-3 ticket verification steps. This is not a plan failure -- the architect documented the conflict. The ticket must lock the name.

---

### MN-02 -- Plan scan list uses custom numbering, not canonical RULES_CATALOG SCAN-01 through SCAN-07

**Observation:**
RULES_CATALOG Section 4 defines a canonical 7-scan protocol with specific commands:
- SCAN-02: Manual McCabe CYC count for all new/modified methods
- SCAN-03: `async`/`await` grep on new/modified executable code
- SCAN-05: `AtmStrategyCreate`/`AtmStrategyChangeStopTarget` grep

The plan's Section 10 scans are operationally correct for B180 but use B180-specific numbering and do not include explicit async/await or AtmStrategyCreate verification commands.

**Assessment:** These canonical scans are implicitly satisfied (no async/await or AtmStrategyCreate are introduced in B180), and the plan covers CYC in Section 8 and ASCII in SCAN-03. No new violation is introduced.

**Action required (Ph4b):** The ptt-verifier MUST run all 7 canonical RULES_CATALOG scans independently (SCAN-01 through SCAN-07 per RULES_CATALOG §4), not only the plan's custom B180 scan list.

---

### MN-03 -- TryEmitBeWarnIfFlatArmed not mentioned in removal list

**Observation:**
The orchestrator prompt's "What is removed" section includes:
> `"TryEmitBeWarnIfFlatArmed (B179 -- no longer needed)"`

The plan's Section 6A (CopyEngine.cs dead symbols) does not list `TryEmitBeWarnIfFlatArmed`. The orchestrator marks this conditional: `"if it did"` -- acknowledging it may not have landed.

**Assessment:** This is a non-blocking omission since the symbol may not exist in the current source. The Ph4b verifier should include:
```
grep -c "TryEmitBeWarnIfFlatArmed" src/PropTraderTools/CopyEngine.cs
```
If the result is > 0, the symbol must be added to the B180-2 removal list before that ticket lands.

---

## 5. Spec Coverage Matrix

| Spec Requirement | Addressed? | Plan Section |
|-----------------|-----------|--------------|
| Per-chart BE button: immediate swap via PttBeSwapOne | YES | Sections 3A, 5A |
| BE ALL button: immediate swap via PttBeSwapAll loop | YES | Sections 3B, 5B |
| SnapshotBeTargetsPublic wrapper in CopyEngine | YES | Section 4A |
| B180-1 additive only -- no removal | YES | Section 2, 5A dead code note |
| B180-2 removal gated on B180-1 VERIFY_PASS | YES | Section 6 preamble |
| Entry price: pos.AveragePrice + direction * buf * tick, rounded | YES | Sections 3A, 3B code |
| Feature flag gate (BreakEven) in both new classes | YES | Sections 3A, 3B code |
| Flat guard (skip qty==0 positions) | YES | PttBeSwapOne (4), PttBeSwapAll.ExecuteOne (4) |
| Follower skip in PttBeSwapAll | YES | Section 3B |
| GlobalBe buffer holder kept as thin class | YES | Section 6C, Decision table |
| PttBreakEven.cs deleted in B180-2 | YES | Section 6D |
| PttGlobalBreakEven.Execute gutted in B180-2 | YES | Section 6C |
| BeState enum + _beState field removed in B180-2 | YES | Section 6B |
| All armed-state FSM methods removed in B180-2 | YES | Sections 6A, 6B |
| 5 test cases: T-B180-T1-01 through T-B180-T1-05 | YES | Section 7 |
| CYC table for all new/changed methods | YES | Section 8 |
| ptt-sync.ps1 after all edits | YES | Appendix A |
| Deferred backlog carry-forward | YES | Section 12 |

---

## 6. CYC Verification Detail

Full McCabe recount (per RULES_CATALOG JS-013 counting standard):

| Method | Base | if/else | foreach | `&&`/`\|\|` | `??`/`?.` | Total CYC | Limit |
|--------|------|---------|---------|-------------|-----------|-----------|-------|
| `PttBeSwapOne.Execute` | 1 | 4 (flag, instr, tickSize, flat) | 0 | 0 | 2 (`?.` + `??`) | 7 | 8 ✅ |
| `PttBeSwapAll.Execute` | 1 | 2 (flag, pos null) | 2 (acc, pos) | 1 (`&&` in follower check) | 0 | 6 | 8 ✅ |
| `PttBeSwapAll.ExecuteOne` | 1 | 2 (instr, tickSize) | 0 | 0 | 2 (`?.` + `??`) | 5 | 8 ✅ |
| `OnBeClick` (new) | 1 | 2 (instr, leader) | 0 | 0 | 1 (`??` on _leaderAccount) | 4 | 8 ✅ |
| `OnGlobalBeClick` (new) | 1 | 0 | 0 | 0 | 0 | 1 | 8 ✅ |
| `SnapshotBeTargetsPublic` | 1 | 0 | 0 | 0 | 0 | 1 | 8 ✅ |

All methods comply with CYC <= 8. Worst case is `PttBeSwapOne.Execute` at CYC=7.

---

## 7. Jane Street Rule Map (New/Changed Methods)

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | Grep new files + changed methods: zero `lock(` | PASS -- no lock statements anywhere |
| JS-001 (no throw) | All failure paths use `return` | PASS -- zero throw keywords |
| JS-002 (no null return) | All new methods return `void`; SnapshotBeTargetsPublic delegates to existing method that returns empty list never null | PASS |
| JS-003 (no magic string state) | No string-based state discriminators | PASS |
| JS-013 (CYC <= 8) | See Section 6 above | PASS -- max CYC=7 |
| ASCII-001 (no non-ASCII) | All string literals inspected: straight ASCII, no curly quotes, no em-dashes | PASS |
| NT8-001 (no async/await) | No async void or async Task in any new/changed method | PASS |
| NT8-006 (no DateTime.Now) | DateTime.Now absent from all new code | PASS |
| NT8-004 (no FontFamily) | Absent | PASS |
| NT8-005 (no #RRGGBB hex) | Absent | PASS |

---

**REVIEW_PASS -- proceed to Ph3 (ticket generation).**
