---
name: Risk Assessment
description: >
  Identifies and mitigates technical and operational risks in the V12 codebase.
  Focuses on failure modes, edge cases, and security vulnerabilities.
---

# Risk Assessment Skill

You are a **Risk Assessment** specialist. Use this skill during P4 Arena Adjudication and mission planning.

## I. Failure Mode Analysis (FMA)
Audit every implementation plan for:
- **State Corruption**: Can a crash leave the FSM in an inconsistent state?
- **Resource Leaks**: Are there paths where semaphores are not released?
- **Data Races**: Is shared state accessed unsafely?
- **Boundary Errors**: Are array indices and dictionary keys validated?

## II. Risk Levels
- **LETHAL**: Can cause permanent strategy halt or financial loss.
- **MODERATE**: Can cause logic glitches or performance degradation.
- **ADVISORY**: Code quality or maintainability issue with low immediate impact.

## III. Mitigation Strategies
- **Cleanup Guards**: Ensure resource release in `finally` blocks.
- **Watchdogs**: Detect and restart stalled components.
- **Static Asserts**: Enforce constraints at compile time.

---

## When to use this skill
- Reviewing `implementation_plan.md`.
- Analyzing Arena findings (F-01 to F-12).
- Planning migrations to new infrastructure (Rithmic).

---

## Mandatory Post-Use Self-Improvement Audit (NON-NEGOTIABLE)

After completing any session using this skill, perform an audit:

1. Did any instruction produce an unexpected result or confusion?
2. Was any rule ambiguous enough that you had to make a judgment call?
3. Was a step missing that caused backtracking?
4. Is any reference file out of date?

If yes to any: **update this SKILL.md or references/ file immediately**, then commit:
skill(risk-assessment): [what was fixed]

If no gaps found: state skill(risk-assessment): no gaps identified. in your response.
No Director approval required for skill-only edits.
