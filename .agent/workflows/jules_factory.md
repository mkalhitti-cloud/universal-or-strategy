# $Julesfactory: The In-Repo Software Factory Protocol

This workflow allows for one-shot, TDD-driven project builds within the existing repository using the Orchestrator-Worker-Validator (OWV) pattern.

## 1. Activation
Invoke this workflow when the Director (User) wants to build a new standalone component or micro-project without manual environment setup.

## 2. Protocol: OWV (Orchestrator-Worker-Validator)
Every behavior implemented under this workflow MUST follow the strict sequence:

1. **ORCHESTRATOR**: Decompose the milestone into a numbered list of testable behaviors.
2. **VALIDATOR**: 
   - Write a failing unit test (xUnit/NUnit) in the `tests/` directory.
   - The test must reference code that does not yet exist.
   - Execute the test and document the failure (e.g., compile error) in a comment.
3. **WORKER**: 
   - Implement the MINIMUM code in `src/` to satisfy the failing test.
   - Run the test and confirm GREEN.
4. **REPEAT**: Move to the next behavior.

## 3. Build Isolation
- All factory projects must reside in a dedicated sub-directory (e.g., `src/<ProjectName>_Factory/`).
- All `dotnet` or build commands MUST be scoped to the project-specific solution file to avoid `net48` / `net8.0` crosstalk.

## 4. Compliance Gates
- **Zero Lock**: No `lock()` statements permitted. Use atomic primitives or the FSM/Actor model.
- **ASCII Only**: No Unicode or emoji in string literals.
- **Auto-Audit**: The Gemini PR Auditor must pass with a 10/10 verdict before the task is considered COMPLETE.

## 5. RE-USE TEMPLATE
The latest one-shot prompt is stored in:
`docs/brain/jules_factory_template.md` (Self-updating via P1 Orchestrator)
