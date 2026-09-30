---
description: Spawn new session in new window with minimal context from previous session.
---
# CONTINUE SESSION WORKFLOW

You are spawning a NEW SESSION in a NEW WINDOW to continue work from a previous session.

## CRITICAL: USE new_task TOOL

**MANDATORY**: You MUST use the `new_task` tool to spawn a new session window.

### Step 1: Load Context

```bash
python scripts/continue_session.py context
```

### Step 2: Spawn New Session

Use the `new_task` tool with the loaded context:

```xml
<new_task>
<mode>autonomous-refactor</mode>
<message>
[Paste the context output from Step 1 here]

**Current Task**: [Task description from context]

**Instructions**: Execute the current task using the context above.
</message>
<todos>
[ ] Load and verify context
[ ] Execute current task
[ ] Mark task complete when done
</todos>
</new_task>
```

## WORKFLOW (In New Session)

1. **Verify Context**: Confirm you have the minimal context (~500 tokens)
2. **Execute Task**: Work on the current task described in context
3. **Complete Task**: When done, mark task complete:
   ```bash
   python scripts/continue_session.py complete "Task description" artifact1.md artifact2.py
   ```
4. **User Decides**: User will either:
   - Type `/continue` again for next task (spawns another new session)
   - Continue in current session if related work remains

## CRITICAL RULES

- **NO SUBTASK HANDOFF**: Never use subtask model - each `/continue` is a fresh session
- **MINIMAL CONTEXT**: Work only with the ~500 token context provided
- **TASK ISOLATION**: Complete one task, mark it done, stop
- **NO ASSUMPTIONS**: If context is unclear, ask user before proceeding
- **BUILDING-BLOCKS**: Always copy scripts from previous wave, never generate from scratch

## STATE MANAGEMENT

Session state is persisted in `.continue/state.json` (gitignored). The state includes:
- Session ID and timestamps
- Current task status
- Completed tasks history
- Wave/branch/VM context
- Last commit SHA

## EXAMPLE USAGE

**Session 1** (Initialize):
```bash
python scripts/continue_session.py init "Implement Lamport clock for Wave 7"
# Work on task...
python scripts/continue_session.py complete "Added Lamport clock" .lamport/wave7/event_log.jsonl
```

**Session 2** (Continue):
```bash
# User types: /continue
# You run: python scripts/continue_session.py context
# Context shows: "Next: Update epic_manifest.py to use Lamport clock"
# Work on task...
python scripts/continue_session.py complete "Updated manifest" scripts/epic_manifest.py
```

**Session 3** (Continue):
```bash
# User types: /continue
# You run: python scripts/continue_session.py context
# Context shows: "Next: Test Lamport clock with pilot epic"
# Work on task...
python scripts/continue_session.py complete "Pilot test passed" docs/brain/EPIC-W7-001/manifest.json
```

## BENEFITS

- **No Context Pollution**: Each session starts fresh with minimal context
- **User Control**: User decides when to continue vs. start new workflow
- **Task Isolation**: Clear boundaries between tasks
- **Continuity**: State persists across sessions without bloat

## REFERENCE

- Specification: `docs/protocol/CONTINUE_COMMAND_SPECIFICATION.md`
- State Management: `scripts/continue_session.py`
- Building-Blocks Method: `docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md`
---

## GRAPHIFY PROTOCOL (MANDATORY — Every Task)

**STARTUP** (run this as your FIRST action before any exploration):
```bash
graphify update . --no-cluster --no-description
```
Then read `.graphify/GRAPH_REPORT.md` for god nodes and community structure.

**SHUTDOWN** (run this as your LAST action after any file edits):
```bash
graphify update . --no-cluster --no-description
```
