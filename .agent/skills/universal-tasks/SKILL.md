---
name: universal-tasks
description: Orchestrates complex, long-running projects across multiple sessions or sub-agents using the "Task" primitive. Replaces simple Todos with persistent, dependency-aware Tasks stored in the file system. Use when coordinating architectural changes, multi-step migrations, or any work requiring multi-agent collaboration and state persistence.
---

# Universal Tasks Skill

## Purpose
"Unhobble" agents by providing a persistent, dependency-aware Task system. This skill allows agents to track progress across context windows, sessions, and different models (Opus, Sonnet, Flash) while respecting project hierarchy and technical blockers.

## Core Primitive: The Task
A **Task** is more than a Todo. It contains:
- **ID**: Unique identifier (e.g., `V9_001`).
- **Status**: `pending`, `in_progress`, `completed`, `blocked`.
- **Dependencies**: IDs of tasks that MUST be completed before this one starts.
- **Blockers**: External conditions (e.g., `market_closed`).
- **Assigned Model**: The best AI for the job (Opus for logic, Flash for I/O).

## Workflow: Multi-Session Collaboration

### 1. Initialization
When starting a project or a new session, read the **Task List**:
- Default location: `.agent/TASKS/MASTER_TASKS.json`
- Env Var: `CLAUDE_CODE_TASK_LIST_ID` (if set, use that specific list).

### 2. Validation (MANDATORY)
Before starting ANY task, validate its dependencies and ownership using the bundled script:
```bash
python .agent/skills/universal-tasks/scripts/validate_task.py <TASK_ID> [AGENT_ID]
```

### 3. Discovery & Claiming
Agents must follow the [Discovery Protocol](references/discovery_protocol.md) to claim tasks atomically. This prevents duplicate work.
```bash
python .agent/skills/universal-tasks/scripts/sync_tasks.py <TASK_ID> in_progress <AGENT_ID>
```

### 4. Synchronization
When status changes, sync the update to ensure other agents/sessions see it. The system uses `.lock` files to prevent race conditions during concurrent writes.
```bash
python .agent/skills/universal-tasks/scripts/sync_tasks.py <TASK_ID> <STATUS> [AGENT_ID]
```

## Guidelines for Agents
- **Don't skip dependencies**: The task system is designed to prevent "race conditions" where an agent implements code before the architecture is ready.
- **Escalate appropriately**: If a `pending` task is assigned to `opus_4.5`, don't attempt it with `gemini_flash` if it involves complex reasoning.
- **Update on completion**: Always sync status to `completed` immediately upon verification.

## Implementation Details
- **Schema**: See [TASKS_SCHEMA.md](references/tasks_schema.md) for JSON structure.
- **Storage**: Tasks are stored in the project's `.agent/TASKS` directory (locally) or `~/.claude/tasks` (globally).

---

## Related Skills
- [delegation-bridge](../delegation-bridge/SKILL.md) - For cost-efficient file ops and deployment.
- [antigravity-core](../antigravity-core/SKILL.md) - For collaboration protocols.
