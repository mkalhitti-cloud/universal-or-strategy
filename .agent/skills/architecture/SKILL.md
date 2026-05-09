---
name: Architecture
description: >
  Architectural design and ADR (Architectural Decision Record) management for the V12 Universal OR Strategy.
  Focuses on modularity, high-performance patterns, and structural integrity.
---

# Architecture Skill

You are an **Architecture** specialist. Use this skill to design structural changes and maintain the system's "Platinum Standard".

## I. Core Patterns
- **Correctness by Construction**: "Make illegal states unrepresentable." Design types and FSMs so invalid states fail at compile-time rather than relying on runtime `if/else` checks.
- **Lock-Free**: Atomic primitives, SPSC/MPMC queues, zero-lock FSMs.
- **IPC**: TCP-based command routing, multi-client support.
- **RAII**: Scope-based resource management (semaphores, dictionaries).
- **SIMA**: Strategy Instrument Multi-Account orchestration.

## II. ADR Management
Every significant architectural decision MUST be recorded in `docs/specs/adr/`.
- **Status**: [PROPOSED|ACCEPTED|REJECTED|SUPERSEDED]
- **Context**: Problem description and technical constraints.
- **Decision**: The chosen solution.
- **Consequences**: Trade-offs and impact on other modules.

## III. File Topology
- Keep files under 500 lines.
- Use partial classes for feature isolation (e.g., `.Lifecycle.cs`, `.Orders.cs`).
- Maintain a clean separation between UI, Compliance, and Execution logic.

---

## When to use this skill
- Designing new modules (REAPER, SIMA).
- Major refactoring of the 71 strategy files.
- Evaluating structural findings from `graphify` or `get_blast_radius`.

---

## Mandatory Post-Use Self-Improvement Audit (NON-NEGOTIABLE)

After completing any session using this skill, perform an audit:

1. Did any instruction produce an unexpected result or confusion?
2. Was any rule ambiguous enough that you had to make a judgment call?
3. Was a step missing that caused backtracking?
4. Is any reference file out of date?

If yes to any: **update this SKILL.md or references/ file immediately**, then commit:
skill(architecture): [what was fixed]

If no gaps found: state skill(architecture): no gaps identified. in your response.
No Director approval required for skill-only edits.
