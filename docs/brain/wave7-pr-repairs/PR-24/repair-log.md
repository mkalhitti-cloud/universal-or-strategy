# PR #24 Repair Log -- wave7/pr5-s5-signals
# S5 Signals & Entries Cluster
# Lane: L5

## REPAIR-08 (Prior Session -- Already Committed)
- Classification: VALID-DNA
- Summary: DateTime.UtcNow consistency, _aek966/_aed966 camelCase rename
- Commit: 25b825df (on branch wave7/pr5-s5-signals)
- Verifier: N/A (pre-existing, noted as already-fixed in fix_queue.md)

---

## REPAIR-F1: DateTime.Now -> UtcNow in MOMO/Retest/OR
- Classification: VALID-DNA
- Finding: DateTime.Now used in 5 places across 3 entry files (hot-path entry name builders)
- OKF Rule: FSM Determinism -- DateTime.Now is BANNED, use DateTime.UtcNow
- Plan: Single-token substitution x5 (MOMO.cs:70, OR.cs:62,106, Retest.cs:138,392)
- Engineer Commit: 55e4d256
- Build: PASS (0 errors, 0 warnings)
- Gate: PASS (GATE PASSED -- all 5 checks green)
- Verifier Verdict: PASS (all 5 instances confirmed replaced, no lock(), no Unicode)
- Bot Re-review: Addresses Greptile P1 (DateTime.Now inconsistency), Gemini medium (3 comments)

---

## REPAIR-F3: IndexOf crash guard MOMO.cs:85
- Classification: VALID-LOGIC-BUG
- Finding: entryName.Substring(0, entryName.IndexOf('_')) -- IndexOf returns -1 if no underscore, Substring throws ArgumentOutOfRangeException
- OKF Rule: Production safety -- "Make illegal states unrepresentable", derive from known type not fragile string parse
- Plan: Replace IndexOf/Substring with direction ternary (same pattern as BuildMOMOPositionInfo:254)
- Engineer Commit: 7871df75
- Build: PASS (0 errors, 0 warnings)
- Gate: PASS (GATE PASSED)
- Verifier Verdict: PASS (ternary confirmed, direction in scope, no crash path)
- Bot Re-review: Addresses Greptile P1 (IndexOf crash), CodeRabbit functional correctness comment

---

## REPAIR-F5: FFMA comment "out params" -> "ref params"
- Classification: VALID-MECHANICAL
- Finding: ValidateAndAdjustFFMALimitStop comment says "out params" but method uses `ref` parameters
- Plan: Single-word fix in comment (line 322)
- Engineer Commit: 5f66d8e6 (batch with F8, F9)
- Build: PASS
- Gate: PASS
- Verifier Verdict: PASS

---

## REPAIR-F8: Remove unused sessionEndTime parameter from ProcessSessionReset
- Classification: VALID-MECHANICAL
- Finding: ProcessSessionReset(sessionEndTime) param never referenced in method body (CodeRabbit outside-diff comment)
- Plan: Remove param from signature (BarUpdate.cs:110) and call-site (line ~325). Retain local variable.
- Engineer Commit: 5f66d8e6 (batch with F5, F9)
- Build: PASS
- Gate: PASS
- Verifier Verdict: PASS (signature clean, local var retained for midnight detection)

---

## REPAIR-F9: Retest log equality-case bias fix
- Classification: VALID-MECHANICAL
- Finding: DetermineRetestDirection else branch (handles currentPrice <= sessionMid) logs "< {1:F2}" which is misleading at equality
- Plan: Change "<" to "<=" in format string
- Engineer Commit: 5f66d8e6 (batch with F5, F8)
- Build: PASS
- Gate: PASS
- Verifier Verdict: PASS

---

## Skipped Findings

| ID | Classification | Reason |
|----|---------------|--------|
| F2 | INFORMATIONAL | Throttle bypass when accounts=0 -- no correctness bug, MaybeFinalizeDailySummaries has its own guard |
| F4 | INFRA-NOISE | "Magic literals" on 30s/10min -- domain constants in helpers |
| F6 | HALLUCINATION | CSharpier braces -- 0 issues confirmed by csharpier check |
| F7 | HALLUCINATION | stopDistance<=0 dead guard -- guard catches independent ATR=0 case |
| F10 | INFRA-NOISE | Duplicate ternary in OR helpers -- informational, different semantic purposes |
| F11 | INFRA-NOISE | Script file findings (Sourcery on gate script) -- out of src/ scope |
| F12 | INFRA-NOISE | deploy-sync reminder -- process note |

---

## Push Record
- Push SHA: 5f66d8e6 (HEAD after repairs)
- Branch: wave7/pr5-s5-signals -> origin/wave7/pr5-s5-signals
- Gate pre-push: GATE PASSED (all 5 checks)
- Pre-push hook: Epic count 161 PASS, Wave 7 CYC gate PASS
