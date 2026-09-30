# Plan Review — B110
# DW-B110: Remove CancelQxBracketsForFollowers from Leader Path

**Reviewer**: ptt-plan-reviewer  
**Date**: 2026-08-26  
**Input**: `docs/brain/B110/02-architecture-plan.md`  
**Phase**: 2 (Plan Review)

---

## Source Verification

| File | Lines Read | Match? |
|------|-----------|--------|
| `src/PropTraderTools/Features/PttQuickExit.cs` | L88–108 | CONFIRMED — L100–L107 exactly as described in plan |
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | L127–174 | CONFIRMED — DW-B79-03 block at L145–163 with `_qxCancelInProgress` guard |
| `src/PropTraderTools/CopyEngine.cs` | L922–942 | CONFIRMED — `CancelQxBracketsForFollowers` definition at L929; no external callers in `CancelQxBrackets` loop |
| `grep CancelQxBracketsForFollowers src/` | 26 matches | CONFIRMED — production call sites: `CopyEngine.cs:929` (def) + `PttQuickExit.cs:107` (call to delete) only |

---

## Checklist R1–R10

### R1 — Spec Alignment
**PASS**

- Plan correctly targets deletion of L100–L107 inclusive (8 lines: 6-line comment block + `if (skipIfFollower)` + call).
- Source confirmed: L100 = `// B70 DW-B70-02:`, L107 = `CopyEngine.Instance?.CancelQxBracketsForFollowers(instr);` — exact match.
- `skipIfFollower` parameter at L70–77 explicitly preserved (plan Section 3 "What Does NOT Change").

### R2 — Redundancy Proof
**PASS**

- DW-B79-03 block confirmed at `PttGlobalQuickExit.cs` L145–163: sets `_qxCancelInProgress` flag for the account before cancel, calls `CancelQxBrackets(acc, instr)`, removes flag in `finally`.
- Grep confirms only one production call site for `CancelQxBracketsForFollowers`: `PttQuickExit.cs:107`. All other matches are test files or comments.
- Redundancy proof in plan Section 2 is factually correct.

### R3 — Race Elimination
**PASS**

- Plan Section 1, collision chain steps 1–7, correctly describes the race: leader's `CancelQxBracketsForFollowers` fires follower bracket cancellations before the follower's own `ExecuteOne` run has set `_qxCancelInProgress`, causing guard(3b) to return FALSE and triggering a spurious BE-RETRY.
- Mechanism of fix is accurate: removal of the unguarded call; DW-B79-03 continues to handle per-follower cancel with the guard correctly set.

### R4 — CYC Accuracy
**PASS**

- CYC=8 → CYC=7 transition correctly enumerated in plan Section 4 (8 branches before, 7 after, branch 3 `cancelFollowers guard` deleted, branches 4–8 renumbered 3–7).
- Docstring update in plan Section 3 is verbatim and correct.
- B78 DW-B78-02 sentence at L35–36 correctly identified for deletion.
- T_B110_02 `Assert.Equal(6, branchCount)` is consistent (CYC = branch_count + 1 = 7).

### R5 — Files In Scope
**PASS**

- MODIFY: `PttQuickExit.cs` only (L100–L107 delete + docstring update).
- ADD: `B110Tests.cs` (new test file).
- NO CHANGES: `CopyEngine.cs`, `PttGlobalQuickExit.cs`, `B68Tests.cs`, `B78Tests.cs`, `B79Tests.cs` — all explicitly enumerated in plan Section 5.

### R6 — Test Design
**PASS**

- T_B110_01 (IL token scan, `CancelQxBracketsForFollowers` absent from `Execute` IL) — pattern matches `B68Tests.cs T_B68_03`; plan explicitly references this pattern.
- T_B110_02 (IL branch count = 6, CYC = 7) — reflection-based branch instruction enumeration; assertion value consistent with stated CYC.
- Both tests use `[Fact]` (xUnit) — compliant with JS testing mandate (NUnit/MSTest banned).

### R7 — Combo Regression Map
**PASS**

- Combo C (BE-ALL → QX-ALL): PASS — primary fix. Mechanism correct.
- Combo D (QX-ALL → BE-ALL): PASS — non-regression. DW-B79-03 path unaffected.
- Combo E (QX-ALL direct): PASS — non-regression. No BE brackets in play.
- Combo F (QX-ALL in green): PASS — non-regression. B108 green-position path unaffected.

All four combos present and correctly characterized.

### R8 — Verify Criteria
**PASS**

- T1–T10 present in plan Section 7.
- T9 mandates `powershell -File scripts\ptt-sync-and-verify.ps1` with pass condition "0 MISMATCH lines". ✅
- T10 mandates agent writes result to `ticket-1-verification.md` with "PASS logged" condition. ✅

### R9 — JS Rules
**PASS**

- JS-021 (no `lock()`): PASS — deletion removes code; no new lock introduced.
- JS-001 (no `throw` in hot path): PASS — no exception-throwing code added.
- JS-002 (no `return null`): PASS — no new return paths.
- JS-033 (no `async void`): PASS — method remains synchronous `void`.
- JS-066 (diff < 10k chars): PASS — ~8 lines deleted + docstring update ≈ 600 chars diff.
- JS-080 (CYC ≤ 8): PASS — CYC decreases from 8 → 7.

All cited in plan Section 9 with rationale.

### R10 — No Scope Creep
**PASS**

- Changes restricted to `PttQuickExit.cs` (surgical deletion + docstring) and new `B110Tests.cs`.
- `CopyEngine.cs` and `PttGlobalQuickExit.cs` explicitly NO CHANGES.
- No unrelated modifications planned.

---

## Observations (Non-Blocking)

| # | Observation | Severity | Action Required |
|---|-------------|----------|-----------------|
| OBS-1 | `CopyEngine.cs:923` comment reads *"Called by PttGlobalQuickExit.Execute before placing new PTT-QX-* orders on the leader."* — this is inaccurate post-fix (the method is called by nobody in production after B110). Plan does not update this comment because `CopyEngine.cs` is NO CHANGES. | Informational | None required for B110. Engineer may log as DW item for future cleanup. |

---

## Violations

**None.** All 10 checks pass. No JS rule violations. No spec gaps.

---

## Verdict

```
REVIEW_PASS
```

Plan is approved for Phase 3 (ticket generation). The ptt-architect may proceed to write `docs/brain/B110/04-tickets.md`.
