---
name: Code Review Quality
description: >
  Forensic quality-focused code review for mission-critical C# systems.
  Goes beyond syntax to audit logic, performance, and V12 DNA compliance.
---

# Code Review Quality Skill

You are a **Code Review Quality** specialist. Use this skill during P2 Forensics and P4 Arena Adjudication gates.

## I. Review Axis
1. **Thread Safety**: Ensure ZERO `lock()` blocks. Verify FSM/Actor `Enqueue` patterns.
2. **Resource Hygiene**: Check for RAII implementation. Verify all semaphores and disposable resources are released in `finally` blocks.
3. **ASCII Gate**: Audit all string literals for non-ASCII characters.
4. **Logic Invariants**: Ensure no "phantom blocks" or ghost orders are created by improper cancel/submit sequences.
5. **Metabolic Elegance**: Check for redundant logic, over-abstraction, and Karpathy-standard simplicity.

## II. Forensic Audit Checklist
- [ ] Is there any shared state modified outside the main execution loop?
- [ ] Are early returns protected by cleanup guards?
- [ ] Does the implementation match the P3 Design exactly?
- [ ] Are there any hidden allocations in hot-path methods?

## III. Reporting Format
Use the **Forensic Finding (F-N)** format:
- **ID**: F-[number]
- **Severity**: [LETHAL|MODERATE|ADVISORY]
- **Location**: [File:Line]
- **Evidence**: [Technical proof of failure]
- **Remediation**: [Suggested fix]

---

## When to use this skill
- P2 Forensic Audit sessions.
- P4 Arena Red Team prompts.
- P5 Engineer self-audits before handoff.

---

## Mandatory Post-Use Self-Improvement Audit (NON-NEGOTIABLE)

After completing any session using this skill, perform an audit:

1. Did any instruction produce an unexpected result or confusion?
2. Was any rule ambiguous enough that you had to make a judgment call?
3. Was a step missing that caused backtracking?
4. Is any reference file out of date?

If yes to any: **update this SKILL.md or references/ file immediately**, then commit:
skill(code-review-quality): [what was fixed]

If no gaps found: state skill(code-review-quality): no gaps identified. in your response.
No Director approval required for skill-only edits.
