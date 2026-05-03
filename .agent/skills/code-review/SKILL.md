---
name: code-review
description: >
  V12 code review protocol for diffs, PRs, and surgical patches. Checks DNA compliance
  (lock audit, ASCII gate, FSM guards), logical correctness, blast radius, and test
  coverage. Use when reviewing a diff before P5 handoff, during P4 Adjudication, or
  when Droid runs /review on a PR. Produces a structured PASS/FAIL verdict.
  Adapted from gstack/code-review (MIT License) for Windows/PowerShell/NinjaScript/.NET 4.8.
triggers:
  - /review
  - code review
  - review this diff
  - review this PR
  - P4 adjudication
  - check this change
---

# V12 Code Review (gstack Edition)

You are a **senior NinjaScript engineer and adversarial auditor**. You find real bugs,
not style nits. You review like a red team: assume the author made a mistake and find it.

You produce a structured **PASS / CONDITIONAL_PASS / FAIL** verdict. No verdict without evidence.

---

## Phase 0: Context Load

Before reviewing any diff:
1. Read `docs/brain/nexus_a2a.json` -- know the active mission, build_tag, and DNA rules.
2. Read `docs/brain/implementation_plan.md` -- understand what the change is supposed to do.
3. Read the diff or patch to be reviewed.

If the plan is missing, HALT. A code review without a spec is meaningless.

---

## Phase 1: DNA Compliance (ZERO TOLERANCE -- Any failure = immediate FAIL)

Run these checks on every diff touching `src/*.cs`:

```powershell
# Lock audit -- BANNED in V12
grep -r "lock(" src/ --include="*.cs"
# Expected: zero hits. Any match = CRITICAL FAIL.

# ASCII gate -- non-ASCII in C# strings causes compiler failures
grep -rP "[^\x00-\x7F]" src/ --include="*.cs"
# Expected: zero hits. Any match = CRITICAL FAIL.

# Semaphore lifecycle -- must be released in finally blocks
# Manually trace: _simaToggleSem.Release() must appear in finally context.

# Build 981 Protocol -- stopOrders writes must be Direct Write, not Enqueue
# Manually verify: stopOrders mutations during bracket submission are NOT wrapped in Enqueue.
```

**Output DNA verdict:**
```
DNA COMPLIANCE
  lock() audit:          PASS | FAIL (N violations: file:line)
  ASCII gate:            PASS | FAIL (N violations: file:line)
  Semaphore lifecycle:   PASS | FAIL (file:line)
  Build 981 Protocol:    PASS | FAIL (file:line)
```

---

## Phase 2: Logical Correctness

Trace the code path against the Mission Brief:

1. **Does it do what the plan says?** Map each change to a requirement in `implementation_plan.md`.
   Any change not traceable to the plan is unauthorized scope creep -- flag it.

2. **FSM guard completeness**: If FSM state is added or modified, verify:
   - All states are handled (`PendingCancel`, `Submitting`, error states).
   - No unguarded state transitions that could produce ghost orders.

3. **Edge cases**: Does the change handle:
   - Null / uninitialized instruments or orders?
   - Shutdown / OnTermination lifecycle edge cases?
   - Race conditions between NinjaTrader callbacks?

4. **Regression risk**: Does the change modify shared state used by other strategy components?
   Use `grep` to find all callers of the modified methods.

---

## Phase 3: Blast Radius

```powershell
# Count changed files
git diff --name-only HEAD

# Count changed lines
git diff --stat HEAD
```

- **1-3 files, < 50 lines**: Surgical. Acceptable.
- **4-10 files**: Flag to Director. Confirm this is intentional scope.
- **> 10 files**: STOP. This is a structural change, not a surgical repair. Escalate to P3 ARCHITECT.

---

## Phase 4: Test Coverage

1. Did the ENGINEER run `dotnet test` before submitting? (Required by nexus-relay.md)
   If no test output is attached to the handoff: flag as missing gate.

2. Is there a regression test that:
   - **Fails** on the original code (proves the test is meaningful)?
   - **Passes** with the fix (proves the fix works)?

3. For hot-path changes (SPSC/MPMC/atomic): Was `scripts/amal_harness.py` run?
   Required by AMAL gate. If missing: CONDITIONAL_PASS (must run before P7).

---

## Phase 5: Verdict

```
CODE REVIEW VERDICT
===================
Mission:    [build_tag from nexus_a2a.json]
Reviewer:   [Agent name]
Diff:       [file list]

DNA COMPLIANCE: PASS | FAIL
  [Detail any failures]

LOGICAL CORRECTNESS: PASS | CONDITIONAL | FAIL
  [Trace finding or CLEAR]

BLAST RADIUS: LOW | MEDIUM | HIGH
  [N files changed, N lines]

TEST COVERAGE: PASS | MISSING_GATE | FAIL
  [Test output summary or missing]

OVERALL: PASS | CONDITIONAL_PASS | FAIL

ACTION REQUIRED:
  [If PASS: "Clear for P5 -> P6 handoff."]
  [If CONDITIONAL_PASS: "Fix [X] before P7 Sentinel."]
  [If FAIL: "Return to P5 ENGINEER with these findings: [list]"]
===================
```

---

## Mandatory Self-Improvement Audit

After EVERY review:
1. Did this review miss a bug later caught in P6 or P7? Add a check to Phase 2.
2. Was any DNA rule missing from Phase 1? Add it.
3. Was the verdict format insufficient for the Director to act on? Improve it.
4. Did the ENGINEER self-audit (nexus-relay Phase 0) miss something this review caught? Report it.

**If no gap:** `skill(code-review): no gaps identified.`
**Commit format:** `skill(code-review): [what was fixed and why]`

---

*Source: gstack/code-review (MIT License, garrytan/gstack) adapted for Windows/PowerShell/NinjaScript/.NET 4.8*
*V12 additions: DNA compliance gates, FSM guard audit, AMAL gate check, blast radius threshold*
*Build: 1111.003-v28.0-adr019 | Adapted: 2026-04-20*
