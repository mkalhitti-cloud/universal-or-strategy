---
name: subagent-driven-development
description: >
  Development workflow where an orchestrator decomposes complex tasks and delegates each part
  to specialized subagents running in parallel or sequence. Use when a task is too large for 
  a single agent pass, requires specialized expertise per phase, or benefits from parallel 
  execution (e.g., the P1->P3->P4 Director's Gate hierarchy).
  Keywords: subagent, parallel agents, task decomposition, orchestrator, delegation, multi-agent.
---

# Subagent-Driven Development Skill

## Overview

Subagent-Driven Development (SDD) is a pattern where an **Orchestrator** agent decomposes a complex task into discrete subtasks and routes each to a **specialized subagent**. The orchestrator manages context, sequencing, and integration — subagents execute only within their domain.

This maps directly to the **BMad V12 Director's Gate Hierarchy**:

- **ORCHESTRATOR (Antigravity)**: Task decomposition, context management, routing.
- **FORENSICS (Codex)**: Diagnosis, audit, proof-of-failure.
- **ARCHITECT (Claude)**: Design, implementation_plan.md, no src/ writes.
- **ENGINEER (Codex/Jules)**: Implementation, deploy-sync.

## Core Pattern

```
1. Orchestrator receives complex task
2. Decompose → subtask list (ordered by dependency)
3. For each subtask:
   a. Select specialist subagent
   b. Write precise, self-contained prompt (no context assumed)
   c. Pass artifacts/file handles as GitHub links (not raw code)
   d. Await result + validate
4. Integrate results
5. Deliver to Director
```

## Decomposition Rules

| Rule                         | Detail                                                               |
| ---------------------------- | -------------------------------------------------------------------- |
| **Single Responsibility**    | Each subtask has exactly one output artifact                         |
| **Self-Contained Prompts**   | Subagent prompt must include all context — assume zero shared memory |
| **No Circular Dependencies** | Map dependency graph before dispatching                              |
| **Parallelism First**        | Identify tasks with no dependency — dispatch together                |
| **GitHub-First References**  | Use branch/file links, not inline code blocks, for large references  |

## Subtask Prompt Template

```markdown
## Subtask: [NAME]

**Agent**: [FORENSICS | ARCHITECT | ENGINEER]
**Input Artifacts**: [paths or GitHub links]
**Objective**: [precise, single output description]
**Success Criteria**: [how orchestrator validates the output]
**Output Artifact**: [expected file path or format]
**Constraints**: [any restrictions — no src/ writes, ASCII-only, etc.]
```

## Parallelism Strategy

Use the `/dispatching-parallel-agents` skill for fan-out:

- Identify **independent** subtasks (no shared write targets)
- Dispatch simultaneously
- Merge results with a **reduction subagent** if outputs must be synthesized

Avoid parallel dispatch for tasks with **shared state** (same file write target) — serialize those.

## V12 Compliance Requirements

- **Plan Approval Gate**: All implementation subtasks MUST produce `implementation_plan.md` reviewed by Director before ENGINEER executes.
- **Engineer Self-Audit**: After every implementation subtask, ENGINEER must run grep audits and ASCII scan before handing back.
- **No Raw Code in Prompts for >50 lines**: Use Python extractor + GitHub link (Section 7 of GEMINI.md).
- **Post-Edit Deployment**: ENGINEER must run `deploy-sync.ps1` after every src/ edit.

## Anti-Patterns (BANNED)

| Anti-Pattern                         | Why Banned                                                     |
| ------------------------------------ | -------------------------------------------------------------- |
| Orchestrator writes code directly    | BANNED per Director's Gate — Orchestrator is coordination only |
| Subagent assumes previous context    | Leads to silent failures; every prompt must be self-contained  |
| Simulating sub-agent output          | Zero-Trust Identity — never hallucinate sub-agent results      |
| Raw code blocks for large references | Use GitHub links instead                                       |

## Post-Use Audit

After every use:

1. Were all subtask prompts self-contained (no assumed context)?
2. Was parallelism correctly applied (no write conflicts)?
3. Did the plan approval gate execute before any ENGINEER subtask?

State: `skill(subagent-driven-development): no gaps identified.` or document fix.
