---
description: Loop (Review & Critique) pattern -- ENGINEER generates, ARCHITECT critiques, loop until SIGN-OFF or max iterations
---

Use this workflow after any ENGINEER (P4) implementation to validate correctness before Director handoff.
Max 3 iterations. If unresolved after 3, escalate to Director with open issues documented.

---

## Phase 1: Engineer Generates

1. ENGINEER produces the implementation (code diff, surgical edit, or patch).
2. ENGINEER runs mandatory self-audit BEFORE submitting for critique:
   - `grep` for `lock(stateLock)` — must be zero hits
   - `grep` for non-ASCII characters in C# strings — must be zero hits
   - Verify all FSM guards present (`PendingCancel`, `Submitting` states)
   - Dry-run logic trace against Mission Brief

---

## Phase 2: Architect Critiques

ARCHITECT (Claude) reviews the implementation against:

1. Mission Brief spec — does the code solve the stated problem?
2. Permanent DNA rules — no locks, correct Enqueue/direct-write pattern, Build 981 protocol
3. Side effects — does the change break any adjacent logic?
4. ASCII compliance — confirmed by Engineer audit, spot-checked by Architect

**Critique output format:**

```
LOOP-CRITIC VERDICT — Iteration [N]

STATUS: [APPROVED / REVISION REQUIRED]

Issues (if any):
1. [Issue + exact line reference]
2. [Issue + exact line reference]

Required fixes before re-submit:
- [Specific fix instructions]
```

---

## Phase 3: Loop or Sign Off

- **APPROVED**: ARCHITECT issues P5 Sign-Off. Route to Director for final confirmation.
- **REVISION REQUIRED**: ENGINEER applies fixes. Return to Phase 1. Increment iteration counter.
- **Max iterations (3) reached without APPROVED**: Escalate to Director with full issue log. Do NOT approve.

---

## Phase 5: Mandatory Self-Improvement Audit (NON-NEGOTIABLE)

After EVERY use of this workflow, the executing agent MUST perform a post-use audit:

1. **Did the loop exceed 3 iterations?** Identify why the spec was unclear and fix it.
2. **Was a DNA rule not checked?** Add it to Phase 2 checklist.
3. **Was the critique format ambiguous?** Clarify the verdict template.
4. **Did Engineer self-audit miss something Architect caught?** Add it to Phase 1 audit list.

**If no gap found, state:** `workflow(loop_critic): no gaps identified -- workflow correct as written.`

Skipping the audit is a protocol violation. No Director approval needed for self-improvement edits.

**Commit format:** `workflow(loop_critic): [what was fixed and why]`
