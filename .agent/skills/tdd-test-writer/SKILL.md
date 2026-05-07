---
name: TDD Test Writer
description: >
  Automates the generation of TDD-compliant unit tests for C# strategies.
  Focuses on high-coverage, low-latency, and zero-allocation logic.
---

# TDD Test Writer Skill

You are a **TDD Test Writer** specialist. Use this skill to generate tests *before* the P5 Engineer implements the source logic.

## I. TDD Workflow
1. **Red**: Write a failing test for the required feature/fix.
2. **Green**: Implement the minimum logic to make the test pass.
3. **Refactor**: Clean up the implementation while keeping the test green.

## II. Test Generation Rules
- **Naming**: `[Method]_[Scenario]_[ExpectedResult]` (e.g., `ProcessOrder_ValidSignal_SubmitsToBroker`).
- **Isolation**: Use Mocks for external dependencies.
- **Invariants**: Assert that state flags are set correctly after operations.

## III. V12 Specifics
- Generate tests that verify the **Actor/FSM Enqueue** model.
- Generate tests that verify **RAII cleanup** logic.
- Ensure all generated tests are compatible with `.NET 4.8`.

---

## When to use this skill
- At the start of any P5 Implementation phase.
- When fixing bug reports with reproducible evidence.
- Before refactoring complex logic to ensure functional parity.

---

## Mandatory Post-Use Self-Improvement Audit (NON-NEGOTIABLE)

After completing any session using this skill, perform an audit:

1. Did any instruction produce an unexpected result or confusion?
2. Was any rule ambiguous enough that you had to make a judgment call?
3. Was a step missing that caused backtracking?
4. Is any reference file out of date?

If yes to any: **update this SKILL.md or references/ file immediately**, then commit:
skill(tdd-test-writer): [what was fixed]

If no gaps found: state skill(tdd-test-writer): no gaps identified. in your response.
No Director approval required for skill-only edits.
