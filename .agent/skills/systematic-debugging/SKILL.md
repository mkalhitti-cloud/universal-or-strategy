---
name: Systematic Debugging
description: >
  Advanced debugging and troubleshooting strategies. Four phases: investigate,
  analyze, hypothesize, implement. Iron Law: no fixes without root cause.
  Use this whenever the user reports a bug, regression, or "weird" behavior
  that isn't immediately obvious. Proactively invoke when user reports errors,
  stack traces, unexpected behavior, or "it was working yesterday".
  Adapted from gstack/investigate (MIT) + V12 Forensic Protocol.
---

# Systematic Debugging (V12 Forensic Standard)

## Iron Law

**NO FIXES WITHOUT ROOT CAUSE INVESTIGATION FIRST.**

Fixing symptoms creates whack-a-mole debugging. Every fix that doesn't address root cause
makes the next bug harder to find. Find the root cause, then fix it.

This is the P2 Forensic Mandate. Produce a "Logical Proof of Failure" before any P4 edit.

---

## Core Logic (Pre-Phase Checks)

Before starting any phase:
- **Isolate the Fault**: Strip away independent components until only the failing unit remains.
- **Reproduce the Error**: Never skip the repro step. If you can't reproduce it, you can't verify the fix.
- **Binary Search**: Use `git bisect` to find the breaking commit if regression is suspected.
- **Check Locks**: Ensure no legacy `lock(stateLock)` is causing deadlocks (V12 CRITICAL).

---

## Phase 1: Root Cause Investigation

Gather context before forming any hypothesis.

1. **Collect symptoms**: Read error messages, stack traces, and reproduction steps.
   If the user hasn't provided enough context, ask ONE question at a time.

2. **Read the code**: Trace the code path from the symptom back to potential causes.
   For V12 NinjaScript: follow the order tick from Ingress -> Brain -> Execution Gate.

3. **Check recent changes**:
   ```powershell
   git log --oneline -20 -- <affected-files>
   ```
   Was this working before? What changed? A regression means the root cause is in the diff.

4. **Reproduce**: Can you trigger the bug deterministically? If not, gather more evidence.

5. **V12-specific checks**:
   - Use `Print()` or logging to verify state at each FSM transition.
   - Check for `lock(stateLock)` -- this is BANNED in V12 and causes deadlocks.
   - Verify `_simaToggleSem` is released in `finally` blocks.
   - Check `stopOrders` for ghost-order tracking windows during bracket submission.

**Output**: "Root cause hypothesis: ..." -- a specific, testable claim about what is wrong and why.

---

## Scope Lock

After forming your root cause hypothesis, lock edits to the affected module to prevent scope creep.

Identify the narrowest directory containing the affected files. Document it explicitly:
```
DEBUG SCOPE LOCKED: src/<affected-module>/
Edits restricted to this directory. Any changes outside require Director re-authorization.
```

If the bug spans the entire repo or scope is genuinely unclear, note why and skip the lock.

---

## Phase 2: Pattern Analysis

Check if this bug matches a known pattern:

| Pattern | Signature | Where to Look |
|---|---|---|
| Race condition | Intermittent, timing-dependent | Concurrent access to shared state |
| Null propagation | NullReferenceException, TypeError | Missing guards on optional values |
| State corruption | Inconsistent data, partial updates | FSM transitions, callbacks, hooks |
| Integration failure | Timeout, unexpected response | External API calls, service boundaries |
| Configuration drift | Works locally, fails in staging/prod | Env vars, feature flags, DB state |
| Ghost order tracking | Order appears then disappears | V12 bracket submission -- use Direct Write |
| FSM bypass | State transition skipped | Enqueue vs Direct Write selection |
| Lock deadlock | Thread hangs permanently | Banned `lock(stateLock)` present |

Also check:
- `TODOS.md` for related known issues.
- `git log` for prior fixes in the same area -- recurring bugs in the same files are an architectural smell.
- `docs/arena_audit_matrix.md` for any ADR covering this area.

---

## Phase 3: Hypothesis Testing

Before writing ANY fix, verify your hypothesis.

1. **Confirm the hypothesis**: Add a temporary `Print()` statement or assertion at the suspected
   root cause. Run the reproduction. Does the evidence match?

2. **If the hypothesis is wrong**: Before forming the next hypothesis, return to Phase 1.
   Gather more evidence. Do NOT guess.

3. **3-strike rule**: If 3 hypotheses fail, STOP. Escalate to Director:
   ```
   3 hypotheses tested, none confirmed.
   This may be an architectural issue, not a simple bug.

   A) Continue investigating -- new hypothesis: [describe]
   B) Escalate to P3 ARCHITECT -- structural repair required
   C) Add Print() instrumentation and catch it on next occurrence
   ```

**Red flags -- if you see any of these, slow down:**
- "Quick fix for now" -- there is no "for now." Fix it right or escalate.
- Proposing a fix before tracing data flow -- you're guessing.
- Each fix reveals a new problem elsewhere -- wrong layer, not wrong code.

---

## Phase 4: Implementation

Once root cause is confirmed:

1. **Fix the root cause, not the symptom.** The smallest change that eliminates the actual problem.

2. **Minimal diff**: Fewest files touched, fewest lines changed. Resist refactoring adjacent code.

3. **V12 Implementation Rules** (MANDATORY):
   - No `lock(stateLock)` -- use FSM/Actor `Enqueue` model or atomic primitives.
   - Direct writes to `stopOrders` ONLY during bracket submission (Build 981 Protocol).
   - ASCII-only in all C# `Print()` string literals. No emoji, curly quotes, or Unicode arrows.
   - Release `_simaToggleSem` in `finally` blocks.

4. **Write a regression test** that:
   - **Fails** without the fix (proves the test is meaningful).
   - **Passes** with the fix (proves the fix works).

5. **Run the full test suite**: `powershell -File .\scripts\test_stress.ps1`

6. **If fix touches > 5 files**: Flag blast radius to Director before proceeding.

---

## Phase 5: Verification and Report

**Fresh verification**: Reproduce the original bug scenario and confirm it's fixed. Not optional.

Run the test suite. Paste output.

Output a structured debug report:
```
DEBUG REPORT
============================================
Symptom:         [what the user observed]
Root cause:      [what was actually wrong -- Logical Proof of Failure]
Fix:             [what was changed, with file:line references]
Evidence:        [test output, reproduction attempt showing fix works]
Regression test: [file:line of the new test]
Related:         [TODOS.md items, prior bugs in same area, architectural notes]
Status:          DONE | DONE_WITH_CONCERNS | BLOCKED
============================================
```

**Post-edit (V12 MANDATORY)**:
1. Run `powershell -File .\deploy-sync.ps1` to re-establish hard links.
2. Tell Director: "Press F5 in NinjaTrader to compile."
3. Verify BUILD_TAG banner shows updated tag.

---

## Completion Status

- **DONE** -- root cause found, fix applied, regression test written, all tests pass.
- **DONE_WITH_CONCERNS** -- fixed but cannot fully verify (intermittent bug, requires staging).
- **BLOCKED** -- root cause unclear after investigation, escalated to Director.

---

## Important Rules

- **3+ failed fix attempts -> STOP and question the architecture.** Wrong architecture, not failed hypothesis.
- **Never apply a fix you cannot verify.** If you can't reproduce and confirm, don't ship it.
- **Never say "this should fix it."** Verify and prove it. Run the tests.
- **If fix touches > 5 files -> Flag blast radius** to Director before proceeding.
- **This skill produces a Logical Proof of Failure for P3 ARCHITECT consumption.**
  The handoff must contain: symptom, root cause, affected files, and proposed fix scope.

---

## Mandatory Self-Improvement Audit

After EVERY skill use, perform this audit:

1. **Instruction Clarity**: Did any step produce an unexpected or ambiguous result?
2. **Trigger Coverage**: Is the description "pushy" enough to fire on the right scenarios?
3. **V12 Compliance**: Were any V12-specific patterns missed (lock audit, ASCII gate, deploy-sync)?
4. **Gap Analysis**: If a gap is found, fix this SKILL.md immediately.
   Otherwise, state: `skill(systematic-debugging): no gaps identified`.

---

*Source: V12 Forensic Protocol + gstack/investigate (MIT License, garrytan/gstack)*
*Build: 1111.003-v28.0-adr019 | Adapted: 2026-04-20*
