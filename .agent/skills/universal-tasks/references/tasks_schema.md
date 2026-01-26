# Master Tasks Schema (v1.0)

This document defines the JSON structure used by the `universal-tasks` skill to coordinate across sessions.

## Root Object

| Field | Type | Description |
|-------|------|-------------|
| `task_list_id` | string | Unique name for the task list. |
| `version` | string | Schema version (current: 1.0). |
| `last_updated` | string | ISO timestamp of the last modification. |
| `tasks` | array | List of Task objects. |
| `sub_agents` | object | Definition of specialized agent roles. |
| `workflow_rules`| object | Default behaviors for models and escalation. |

## Task Object

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Unique identifier (e.g., `TRD_001`). |
| `title` | string | Short summary of the work. |
| `status` | string | `pending`, `in_progress`, `completed`, `blocked`. |
| `priority` | string | `critical`, `high`, `medium`, `low`. |
| `assigned_model`| string | Model suggested for the task. |
| `claimed_by` | string | unique Agent/Session ID currently working on it (e.g., `gemini-3.0-flash-123`). |
| `claimed_at` | string | ISO timestamp when the task was most recently claimed. |
| `dependencies` | array | List of Task IDs that must be `completed`. |
| `blockers` | array | External factors (e.g., `market_closed`). |

## Example Task: Signal Processing Implementation

```json
{
  "id": "V9_SIGNAL_001",
  "title": "Implement EMA Crossover Logic",
  "status": "pending",
  "priority": "high",
  "assigned_model": "opus_4.5",
  "claimed_by": "Agent_X",
  "claimed_at": "2026-01-25T15:00:00Z",
  "dependencies": ["V9_ARCH_001"],
  "blockers": [],
  "sub_tasks": [
    {"task": "Extract EMA values from TOS RTD", "status": "pending"},
    {"task": "Implement crossover trigger switch", "status": "pending"}
  ],
  "notes": "Ensure 100ms polling rate is respected."
}
```

## Sub-Agent Object

```json
{
  "name": "Agent Role Name",
  "responsibility": "Description of duties",
  "assigned_tasks": ["TASK_ID"],
  "primary_model": "The logic model",
  "file_ops_model": "The I/O model (Gemini Flash)",
  "context_file": "Path to status JSON"
}
```
