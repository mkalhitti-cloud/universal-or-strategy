# Agent Discovery & Claiming Protocol

This protocol ensures that multiple agents (or the same agent across different sessions) can collaborate on the same task list without overlapping work or creating race conditions.

## 1. Discovery
Agents find work by scanning the `MASTER_TASKS.json` file.
- **Criteria**:
    - `status == "pending"`
    - `claimed_by == null`
    - All `dependencies` have `status == "completed"`.
    - No active `blockers`.

## 2. Claiming (Atomic Operation)
Once a suitable task is found, the agent MUST claim it before starting work. This is done via the `sync_tasks.py` script, which use a `.lock` file to ensure atomicity.

**Command**:
```bash
python .agent/skills/universal-tasks/scripts/sync_tasks.py <TASK_ID> in_progress <AGENT_ID>
```

**Success**: The agent is now the owner of the task. `claimed_by` is set to `<AGENT_ID>` and `claimed_at` is set to the current ISO timestamp.
**Failure**: If the task was claimed by another agent just milliseconds before, the script returns an error. The agent should then go back to the Discovery phase.

## 3. Execution & Updates
During execution, the agent should periodically update the `sub_tasks` progress within the main task object using `sync_tasks.py`.

## 4. Release (Completion)
When the task is done and verified, the agent marks it as completed.
**Command**:
```bash
python .agent/skills/universal-tasks/scripts/sync_tasks.py <TASK_ID> completed <AGENT_ID>
```
The script will automatically clear the `claimed_by` field, making the task "completed" and ready for dependent tasks to start.

## 5. Stale Claim Recovery (Automatic)
To prevent deadlocks from crashed agents, the system implements **Automatic Stale Claim Recovery**:
- When `validate_task.py` or `sync_tasks.py` is called, it checks the `claimed_at` field.
- If a task is `in_progress` and the `claimed_at` timestamp is **older than 1 hour**, the claim is considered stale.
- The system will **automatically release** the claim (setting `claimed_by` and `claimed_at` to `null`), allowing other agents to pick up the task.
- Verification: The recovery event is logged to the project's task list metadata.
