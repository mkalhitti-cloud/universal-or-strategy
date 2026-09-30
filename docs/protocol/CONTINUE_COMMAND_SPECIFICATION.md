# /continue Command Specification

**Version**: 1.0
**Status**: DRAFT
**Created**: 2026-06-20
**Purpose**: Continuous session workflow without automatic subtask handoff

## Executive Summary

The `/continue` command enables a continuous workflow where tasks are completed sequentially in separate sessions, maintaining context continuity without automatic parent handoff. This differs from `$continue` (which spawns subtasks) by giving the user explicit control over task progression.

## Problem Statement

**Current Pain Points**:
1. **Context Pollution**: Long sessions accumulate irrelevant context from previous tasks
2. **Automatic Handoff**: `$continue` spawns subtasks that automatically return to parent
3. **Mixed Concerns**: Single session handles multiple unrelated tasks (MCPs, skills, Lamport clock, epic naming)
4. **Wave Confusion**: Cannot distinguish which epic belongs to which wave (EPIC-1 in Wave 6 vs Wave 7)

**User Need**:
> "I want to /continue after every task as we run through the tasks... but I don't want the subtask handing off to the parent automatically. I want to /continue task after task for us to have a continuous session separated and compartmentalized to avoid pollution of context but still have continuity."

## Design Goals

1. **Compartmentalization**: Each task runs in a fresh session with minimal context
2. **Continuity**: Session state persists across `/continue` invocations
3. **User Control**: User explicitly triggers next task (no automatic handoff)
4. **Context Preservation**: Essential state carries forward, noise is dropped
5. **Task Isolation**: Each task is self-contained and verifiable

## Command Syntax

```
/continue [task-description]
```

**Examples**:
```
/continue Fix MCP configuration issues
/continue Update workflow skills documentation
/continue Implement Lamport clock for Wave 7
/continue Add wave prefix to epic naming (EPIC-W7-001)
```

## Workflow Model

### Traditional Subtask Model (Current)
```
Parent Session
  ├─ Subtask 1 (auto-spawned)
  │  └─ Returns to parent automatically
  ├─ Subtask 2 (auto-spawned)
  │  └─ Returns to parent automatically
  └─ Context accumulates in parent
```

**Problems**:
- Parent session accumulates all context
- Automatic handoff removes user control
- Cannot skip or reorder tasks easily

### /continue Model (Implemented)
```
Session 1: Fix MCPs (Window 1)
  └─ Completes, writes state to .continue/state.json
  
User types: /continue

Session 2: Update skills (NEW WINDOW 2)
  ├─ Agent loads context via python scripts/continue_session.py context
  ├─ Agent uses new_task tool to spawn new session
  ├─ New session starts with minimal context (~500 tokens)
  └─ Completes, updates state
  
User types: /continue

Session 3: Implement Lamport clock (NEW WINDOW 3)
  ├─ Agent loads context via python scripts/continue_session.py context
  ├─ Agent uses new_task tool to spawn new session
  ├─ New session starts with minimal context (~500 tokens)
  └─ Completes, updates state
```

**Benefits**:
- Each session starts fresh in NEW WINDOW (like subtasks)
- No automatic handoff to parent (unlike subtasks)
- User controls progression (explicit `/continue`)
- Can skip, reorder, or branch tasks
- State is explicit and inspectable
- Context stays minimal (~500 tokens vs 86k+)

## State Management

### State File: `.continue/state.json`

**Location**: `.continue/state.json` (gitignored)

**Schema**:
```json
{
  "session_id": "continue-2026-06-20-001",
  "created_at": "2026-06-20T18:30:00Z",
  "updated_at": "2026-06-20T18:45:00Z",
  "current_task": {
    "id": 3,
    "description": "Implement Lamport clock for Wave 7",
    "status": "in_progress",
    "started_at": "2026-06-20T18:40:00Z"
  },
  "completed_tasks": [
    {
      "id": 1,
      "description": "Fix MCP configuration issues",
      "status": "completed",
      "started_at": "2026-06-20T18:30:00Z",
      "completed_at": "2026-06-20T18:35:00Z",
      "artifacts": [
        ".mcp.json",
        ".mcp.json.vm",
        "docs/protocol/GREPTILE_AND_PR_REMOVAL_COMPLETE.md"
      ],
      "summary": "Removed Greptile MCP from local and VM configurations"
    },
    {
      "id": 2,
      "description": "Update workflow skills documentation",
      "status": "completed",
      "started_at": "2026-06-20T18:35:00Z",
      "completed_at": "2026-06-20T18:40:00Z",
      "artifacts": [
        "docs/workflow/AUTONOMOUS_REFACTOR_MODE_INSTRUCTIONS.md",
        ".bob/custom_modes.yaml"
      ],
      "summary": "Removed PR task references from Phase 3, 5.V, 6"
    }
  ],
  "context": {
    "wave": 7,
    "branch": "gitbutler/workspace",
    "vm_status": "running",
    "vm_ip": "34.121.187.241",
    "last_commit": "932f1448"
  },
  "next_tasks": [
    {
      "id": 4,
      "description": "Add wave prefix to epic naming (EPIC-W7-001)",
      "priority": "high",
      "blocked_by": []
    },
    {
      "id": 5,
      "description": "Execute Wave 7 pilot (3 epics)",
      "priority": "high",
      "blocked_by": [3, 4]
    }
  ]
}
```

### State Transitions

**On `/continue <task>`**:
1. Read `.continue/state.json`
2. Load minimal context (current_task, completed_tasks summary, context)
3. Start new session with task description
4. Execute task
5. Update state.json with completion status and artifacts
6. Present summary to user
7. Wait for next `/continue` command

**On Session Start** (no `/continue`):
1. Check if `.continue/state.json` exists
2. If exists, offer to resume: "Continue from last session? (Y/n)"
3. If yes, load state and present summary
4. If no, start fresh session

## Context Preservation Strategy

### What Carries Forward (Minimal Context)

**Essential State** (~500 tokens):
- Current wave number
- Current branch
- VM status and IP
- Last commit hash
- Completed tasks summary (1 line each)
- Current task description

**Example Context Block**:
```markdown
## Session Context (from /continue)

**Wave**: 7 (180 epics, CYC > 8 → CYC ≤ 8)
**Branch**: gitbutler/workspace
**VM**: Running (34.121.187.241)
**Last Commit**: 932f1448

**Completed Tasks**:
1. ✅ Fix MCP configuration issues (removed Greptile)
2. ✅ Update workflow skills documentation (removed PR tasks)

**Current Task**: Implement Lamport clock for Wave 7
```

### What Gets Dropped (Noise)

**Excluded from Context**:
- Full file contents from previous tasks
- Detailed implementation discussions
- Error messages and debugging logs
- Tool use history
- Intermediate artifacts

**Rationale**: Each task should be self-contained. If Task 3 needs information from Task 1, it should re-read the relevant files, not rely on stale context.

## Implementation Plan

### Phase 1: State Management (Core)

**Files to Create**:
- `.continue/state.json` - Session state
- `scripts/continue_session.py` - State management utilities
- `docs/protocol/CONTINUE_COMMAND_SPECIFICATION.md` - This document

**Functions**:
```python
# scripts/continue_session.py

def init_session(task_description: str) -> dict:
    """Initialize new /continue session."""
    pass

def load_state() -> dict:
    """Load state from .continue/state.json."""
    pass

def save_state(state: dict) -> None:
    """Save state to .continue/state.json."""
    pass

def complete_task(task_id: int, artifacts: list, summary: str) -> None:
    """Mark task as completed and update state."""
    pass

def get_minimal_context() -> str:
    """Generate minimal context block for next session."""
    pass
```

### Phase 2: Bob IDE Integration (IMPLEMENTED)

**File**: `.bob/commands/continue.md`

**Behavior**:
1. User types `/continue`
2. Agent loads context: `python scripts/continue_session.py context`
3. Agent uses `new_task` tool to spawn new session in new window
4. Agent passes loaded context as initial message
5. New session starts with minimal context (~500 tokens)
6. Agent executes current task
7. On completion, agent calls `complete_task()`

**Key Implementation Detail**: The `/continue` command instructs the agent to use the `new_task` tool, which spawns a new session in a new window (similar to subtasks) but WITHOUT automatic parent handoff.

**XML Example**:
```xml
<new_task>
<mode>autonomous-refactor</mode>
<message>
[Context from python scripts/continue_session.py context]

**Current Task**: [Task description]

**Instructions**: Execute the current task using the context above.
</message>
<todos>
[ ] Load and verify context
[ ] Execute current task
[ ] Mark task complete when done
</todos>
</new_task>
```

### Phase 3: User Experience

**Command Flow**:
```bash
# User initializes first task
python scripts/continue_session.py init "Fix MCP configuration issues"

# Session 1 completes, shows summary
✅ Task 1 Complete: Fix MCP configuration issues
   Artifacts: .mcp.json, .mcp.json.vm
   Summary: Removed Greptile MCP from local and VM

# User types: /continue
# → Bob IDE spawns NEW SESSION in NEW WINDOW

# Session 2 (NEW WINDOW) starts with minimal context
📋 Session Context:
   Wave 7 | gitbutler/workspace | VM: 34.121.187.241
   Completed: 1 task (Fix MCP configuration issues)
   
🎯 Current Task: Update workflow skills documentation

# Session 2 completes
✅ Task 2 Complete: Update workflow skills documentation
   Artifacts: docs/workflow/AUTONOMOUS_REFACTOR_MODE_INSTRUCTIONS.md
   Summary: Removed PR task references from Phase 3, 5.V, 6

# User types: /continue
# → Bob IDE spawns ANOTHER NEW SESSION in NEW WINDOW

# Session 3 (NEW WINDOW) starts with minimal context
📋 Session Context:
   Wave 7 | gitbutler/workspace | VM: 34.121.187.241
   Completed: 2 tasks
   
🎯 Current Task: Implement Lamport clock for Wave 7
```

## Real-World Task List (Example)

**Pre-Wave 7 Tasks** (from user feedback):

1. ✅ **Fix MCP configuration issues**
   - Remove Greptile MCP (local + VM)
   - Verify jCodemunch + Sequential Thinking only
   - Status: COMPLETED

2. ✅ **Update workflow skills documentation**
   - Remove PR task references from Phase 3, 5.V, 6
   - Update AUTONOMOUS_REFACTOR_MODE_INSTRUCTIONS.md
   - Status: COMPLETED

3. ⏳ **Implement Lamport clock for Wave 7**
   - Create `.lamport/wave7/` directory structure
   - Implement event logging in phase scripts
   - Add clock synchronization logic
   - Status: PENDING

4. ⏳ **Add wave prefix to epic naming**
   - Change EPIC-001 → EPIC-W7-001
   - Update epic_roadmap.json schema
   - Update all phase scripts to use new naming
   - Rationale: Distinguish Wave 6 vs Wave 7 epics
   - Status: PENDING

5. ⏳ **Execute Wave 7 pilot (3 epics)**
   - Run Phases 0-6 for 3 test epics
   - Verify all changes work correctly
   - Document any issues
   - Status: BLOCKED (waiting for tasks 3-4)

## Benefits Over Subtask Model

| Feature | Subtask Model | /continue Model |
|---------|---------------|-----------------|
| **Context Size** | Accumulates (86k → 114k) | Stays minimal (~500 tokens) |
| **User Control** | Automatic handoff | Explicit progression |
| **Task Isolation** | Shared parent context | Fresh session per task |
| **State Visibility** | Opaque (in parent) | Explicit (state.json) |
| **Reordering** | Difficult | Easy (just /continue different task) |
| **Skipping** | Difficult | Easy (don't /continue that task) |
| **Branching** | Not supported | Easy (fork state.json) |

## Edge Cases

### 1. Task Fails Mid-Execution

**Scenario**: Task 3 fails halfway through

**Behavior**:
- State remains at "in_progress"
- User can retry: `/continue Implement Lamport clock for Wave 7`
- Or skip: `/continue Add wave prefix to epic naming`
- Or debug: Inspect `.continue/state.json` and artifacts

### 2. User Wants to Branch

**Scenario**: User wants to try two approaches to Task 4

**Behavior**:
```bash
# Save current state
cp .continue/state.json .continue/state-backup.json

# Try approach A
/continue Add wave prefix to epic naming (approach A)

# If fails, restore and try approach B
cp .continue/state-backup.json .continue/state.json
/continue Add wave prefix to epic naming (approach B)
```

### 3. User Wants to Resume After Break

**Scenario**: User closes IDE and returns next day

**Behavior**:
- On next Bob CLI start, check for `.continue/state.json`
- Prompt: "Resume from last session? Last task: Implement Lamport clock (in_progress)"
- If yes, load state and present summary
- If no, archive state and start fresh

## Migration Path

### For Existing Workflows

**Option 1: Gradual Adoption**
- Keep using subtasks for simple tasks
- Use `/continue` for complex multi-task workflows
- Both models coexist

**Option 2: Full Migration**
- Deprecate subtask model for multi-task workflows
- Mandate `/continue` for all task sequences
- Update AGENTS.md with new protocol

**Recommendation**: Option 1 (gradual adoption)

## Success Metrics

**Quantitative**:
- Context size stays <10k tokens per session (vs 86k+ with subtasks)
- Task completion time reduces (less context to process)
- User satisfaction with control increases

**Qualitative**:
- Users report less confusion about task state
- Easier to debug failed tasks (explicit state)
- Easier to reorder or skip tasks

## Open Questions

1. **Should state.json be committed to git?**
   - Pro: Reproducible workflows
   - Con: Clutters git history
   - **Recommendation**: Gitignore by default, commit manually if needed

2. **Should /continue support task dependencies?**
   - Example: `/continue Task4 --depends-on Task3`
   - **Recommendation**: Start simple (no dependencies), add later if needed

3. **Should /continue support parallel tasks?**
   - Example: `/continue Task3 & /continue Task4` (run in parallel)
   - **Recommendation**: Not in V1 (adds complexity)

4. **How to handle VM sync in /continue workflow?**
   - Should each task auto-sync to VM?
   - Or manual sync between tasks?
   - **Recommendation**: Manual sync (user controls when to push)

## Next Steps

1. **Create state management utilities** (`scripts/continue_session.py`)
2. **Test with real task sequence** (Tasks 3-5 from user feedback)
3. **Document usage in AGENTS.md**
4. **Get user feedback on UX**
5. **Iterate based on feedback**

---

**Author**: Autonomous Refactor Mode
**Reviewers**: [Pending]
**Status**: DRAFT - Awaiting user feedback