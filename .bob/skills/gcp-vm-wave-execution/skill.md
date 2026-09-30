---
name: gcp-vm-wave-execution
description: >-
  OBSOLETE — DO NOT USE. This skill described the V1 Bob Shell execution model
  using GCP VM screen sessions and _pX_NNN.sh scripts. It has been superseded
  by the Bob IDE V2 native subagent pattern documented in
  docs/workflow/AUTONOMOUS_REFACTOR_INTEGRATION_MATRIX_V2.md.
metadata:
  user-invocable: false
  disable-model-invocation: true
---

# ⛔ OBSOLETE — DO NOT USE

**This skill is RETIRED as of V12.28 (Bob IDE V2 Subagent Model).**

## What replaced it

All wave execution now uses Bob IDE V2 native subagents. The orchestrator
(`autonomous-refactor` mode) spawns subagents directly — no scripts,
no VM screen sessions, no delays needed.

**Correct approach**: See `.bob/skills/autonomous-refactor/SKILL.md` and
`docs/workflow/AUTONOMOUS_REFACTOR_INTEGRATION_MATRIX_V2.md`.

## What was here (NEVER restore)

- `bob --yolo --chat-mode MODE "$(cat /tmp/msg.txt)"` invocations
- GCP VM screen session management
- `_p0_NNN.sh`, `_p1_NNN.sh`, `_p2_NNN.sh` script generation
- 12-second delays between parallel launches
- SSH file persistence verification loops
- `gcloud compute scp` commands
- 4-minute polling intervals

All of the above are **permanently retired**. Do not copy these patterns
into any new script, skill, or command.

## Historical record

This skill was used for Waves 1-6 before Bob IDE V2 became available.
Wave 7 was the first wave to use the native subagent model successfully,
completing Phase 1 (161/161 epics) in a single parallel spawn.
