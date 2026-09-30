---
description: Run N parallel subagents on the same task and pick the best result.
argument-hint: <task description>
---
# BEST-OF-N EXECUTION PATTERN

## What it is
Spawn N parallel `general` subagents against the same task, collect all N results,
then pick the best one. Useful for code generation, planning, and any task where
multiple independent attempts improve output quality.

---

## How to invoke

Fire all N subagents in a **single turn** so they run in parallel:

```
spawn_subagent(type="general", description="<task> -- attempt 1")
spawn_subagent(type="general", description="<task> -- attempt 2")
spawn_subagent(type="general", description="<task> -- attempt 3")
spawn_subagent(type="general", description="<task> -- attempt 4")
spawn_subagent(type="general", description="<task> -- attempt 5")
```

All 5 calls in the same turn = true parallel execution.
Each `general` subagent runs on the **default model** (same capability as the parent agent).
Each returns a summary back to the parent, which then selects the best result.

---

## spawn_subagent vs start_subtask

| | `spawn_subagent(type="general")` | `start_subtask` |
|---|---|---|
| Parallelism | Yes -- all in one turn | No -- sequential only (one per turn) |
| Model | Default model (full capability) | Whatever model the assigned mode runs on |
| Context | Isolated (no parent history unless fork_context=true) | Full subtask session with its own mode |
| Use case | Best-of-N, parallel research, parallel generation | Pipeline stages (engineer -> verifier -> reviewer) |

`start_subtask` spins up a full subtask session with its own mode, which runs at
whatever model that mode is configured for -- effectively giving you a full-capability
agent per lane. But it is sequential: you cannot fire 5 `start_subtask` calls in one
turn and have them run in parallel.

---

## Model selection -- the truth (confirmed via IBM Bob docs)

**You cannot choose a specific model in Bob.** This is documented in the official FAQ:

> "No. Bob automatically selects the most appropriate language model for each task
> based on complexity, required capabilities, and cost-efficiency."

Bob's routing factors:
- Task complexity -- simpler tasks get faster/cheaper models
- Required capabilities -- specialized tasks route to models with specific strengths
- Context size -- large codebases may require extended-context models
- Cost optimization -- balances performance with resource usage

**What the `explore` / `general` distinction actually means:**
- `explore` subagent = lighter model (read-only codebase exploration)
- `general` subagent = default model (full tool access, same as parent)

**The jCodemunch `model=` parameter** (e.g. `plan_turn(model="claude-opus-4-7")`) is
NOT a model selector. It is a tier hint that narrows the jCodemunch tool list exposed
to you. It has zero effect on which LLM Bob uses to generate responses.

---

## Recommended N

- N=3 for fast iteration (planning, short code)
- N=5 for high-stakes generation (complex logic, architecture decisions)
- N=7+ rarely justified given token cost vs marginal quality gain
