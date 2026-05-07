---
name: Spec Workflow
description: >
  Enforces a structured, four-phase product development lifecycle: Requirements, Design, Task Planning, and Execution.
  Specifically optimized for V12 Universal OR Strategy refactoring missions to prevent logic drift and ensure "Metabolic Elegance".
  Requires user confirmation/sign-off between each phase.
---

# Spec Workflow Skill

You are a **Spec Workflow** specialist. This skill mandates a strict progression through four distinct phases for any feature, refactor, or hardening task.

## Phase 1: Requirements (EARS Syntax)
Define exactly what the system should do using **EARS (Easy Approach to Requirements Syntax)**. Requirements must be measurable and testable.
- **Ubiquitous**: "The [System] shall [Response]"
- **Event-driven**: "When [Trigger], the [System] shall [Response]"
- **Unwanted Behavior**: "If [Trigger], the [System] shall [Response]"
- **State-driven**: "While [In State], the [System] shall [Response]"
- **Optional**: "Where [Feature is included], the [System] shall [Response]"

**Deliverable**: `docs/specs/requirements.md`
**Gate**: User must approve requirements before moving to Design.

## Phase 2: Design
Outline the technical architecture, data models, and API definitions.
- **Patterns**: No `lock` blocks, Actor/FSM model, RAII resource management.
- **Components**: Identify which files (e.g., `.Lifecycle.cs`, `.Dispatch.cs`) are affected.
- **Constraints**: ASCII-only strings, .NET 4.8 compatibility.

**Deliverable**: `docs/specs/design.md`
**Gate**: User must approve design before moving to Task Planning.

## Phase 3: Task Planning (Superpower Granularity)
Break down the design into discrete, reviewable tasks using the **Implementation Plan Standards** (§VII in Architect skill).
- **Granularity**: Each task should be implementable in a single 2-5 minute turn.
- **Verification**: Each task MUST have a clear `verify:` step (e.g., `dotnet build`, `grep audit`).
- **Traceability**: Link every task back to a Requirement ID from Phase 1.
- **No Placeholders**: Never allow "TBD" or "write tests" without the actual code.

**Deliverable**: `docs/specs/tasks.md`
**Gate**: User must approve tasks before moving to Execution.

## Phase 4: Execution
Perform the implementation and verification.
- **Surgical Edits**: Only touch what is necessary.
- **P6 Validation**: Run post-surgery tests for every task.
- **Nexus Sync**: Update `nexus_a2a.json` after every significant change.

**Deliverable**: Implementation and validation reports.
**Gate**: P7 Sentinel merge to main.

---

## When to use this skill
- Any task involving more than 3 files.
- Refactoring high-complexity methods (>50 complexity).
- Implementing new core infrastructure (IPC, SIMA, REAPER).
- Hardening against Arena findings (Build-984).

---

## Mandatory Post-Use Self-Improvement Audit (NON-NEGOTIABLE)

After completing any session using this skill, perform an audit:

1. Did any instruction produce an unexpected result or confusion?
2. Was any rule ambiguous enough that you had to make a judgment call?
3. Was a step missing that caused backtracking?
4. Is any reference file out of date?

If yes to any: **update this SKILL.md or references/ file immediately**, then commit:
skill(spec-workflow): [what was fixed]

If no gaps found: state skill(spec-workflow): no gaps identified. in your response.
No Director approval required for skill-only edits.
