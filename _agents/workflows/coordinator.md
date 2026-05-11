---
description: Coordinator (Hierarchical task decomposition) pattern -- Antigravity routes tasks to the right agent based on phase and task type
---

Use this workflow when a user request spans multiple phases or agents.
The ORCHESTRATOR (Antigravity) decomposes the task and routes each subtask to the right agent.
Never let one agent handle all phases end-to-end.

---

## Phase 1: Decompose the Task

1. Read the user request.
2. Categorize each subtask:

| Subtask Type             | Route To               | Workflow             |
| ------------------------ | ---------------------- | -------------------- |
| Diagnosis / tracing      | FORENSICS (Codex)      | `/agent_as_tool`     |
| Structural design / plan | ARCHITECT (Claude)     | `/architect_intake`  |
| Code implementation      | ENGINEER (Codex/Jules) | P4 handoff           |
| Peer review / sign-off   | ARCHITECT (Claude)     | `/loop_critic`       |
| Cross-audit / red-team   | All agents             | `/multi_agent_audit` |

3. Write a decomposition plan:
   - List each subtask
   - Assign agent
   - Define expected output
   - Define dependency order (what must complete before the next begins)

---

## Phase 2: Execute Sequentially or in Parallel

- **Sequential**: when subtask B depends on subtask A's output
- **Parallel**: when subtasks are independent (e.g. FORENSICS scans two files simultaneously)
- Always confirm each output before routing to the next agent.

---

## Phase 3: Aggregate & Report

1. Collect all agent outputs.
2. Verify completeness against the decomposition plan.
3. Present the Director with a consolidated summary.
4. If any subtask failed — re-invoke that agent with a corrected prompt (log what was wrong).

---

## Phase 5: Mandatory Self-Improvement Audit (NON-NEGOTIABLE)

After EVERY use of this workflow, the executing agent MUST perform a post-use audit:

1. **Was the decomposition wrong?** Fix the routing table.
2. **Did an agent receive an out-of-scope task?** Tighten the subtask definition.
3. **Was a dependency missed?** Add it to Phase 2 sequencing rules.
4. **Was aggregation incomplete?** Improve the Phase 3 checklist.

**If no gap found, state:** `workflow(coordinator): no gaps identified -- workflow correct as written.`

Skipping the audit is a protocol violation. No Director approval needed for self-improvement edits.

**Commit format:** `workflow(coordinator): [what was fixed and why]`
