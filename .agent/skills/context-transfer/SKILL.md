---
name: context-transfer
description: Lightweight Haiku sub-agent for generating context handoff prompts when starting new conversations. Creates comprehensive summaries of current work, pending tasks, and important context to paste into fresh sessions. Auto-triggers on context transfer requests.
---

# Context Transfer Sub-Agent

## Purpose
Fast generation of context handoff prompts using Haiku model to help you start fresh conversations without losing context.

## When to Auto-Trigger
Automatically spawn a Haiku sub-agent when user requests:
- "Generate handoff prompt"
- "Create context for new conversation"
- "I need to start a new session"
- "Prepare context transfer"
- "What should I paste in a new chat?"

## Sub-Agent Configuration
```
Model: haiku
Tools: Read, Grep, Bash (for git status)
Type: general-purpose
```

## What the Handoff Prompt Should Include

### 1. Current Project State
- Active version (e.g., "Working on V8_2_UI_HORIZONTAL")
- Last completed work
- Files modified recently
- Git status summary

### 2. Pending Tasks
- What's in progress
- What's planned next
- Any blockers or issues

### 3. Important Context
- Recent decisions made
- Testing status
- Known issues
- Configuration details

### 4. File Locations
- Which files are where (project vs NinjaTrader)
- What's been deployed
- What needs deployment

### 5. Next Steps
- Immediate next action
- Priority order
- Testing needed

## Handoff Prompt Template

```markdown
# Project Handoff - [DATE]

## Current Status
- **Active Version**: [version name]
- **Last Work**: [what was just completed]
- **Testing Status**: [tested/untested/in progress]

## Recent Changes
[Bullet list of recent modifications]

## File State
- **Project Location**: `[path]`
- **NinjaTrader Location**: `[path]`
- **Deployed Version**: [version in NinjaTrader]
- **Git Status**: [modified/staged/committed files]

## Pending Work
1. [Next immediate task]
2. [Following task]
3. [After that]

## Important Context
- [Key decisions made]
- [Constraints to remember]
- [Patterns to follow]

## Known Issues
- [Issue 1]
- [Issue 2]

## Next Action
[Specific first step to take in new conversation]

---

**Paste this into new conversation and say**: "Continue from here"
```

## Operations

### 1. Generate Full Handoff
**Process**:
1. Check git status for recent changes
2. Read recent work from plan files (if exist)
3. Scan for modified files
4. Check CHANGELOG.md for latest version
5. Compile all info into handoff template
6. Output formatted prompt

### 2. Generate Quick Handoff
**Process**:
1. Current version only
2. Last 1-2 changes
3. Next immediate step
4. Minimal but actionable

### 3. Generate Task-Specific Handoff
**Process**:
1. Focus on specific task context
2. Include only relevant files
3. Include only relevant history
4. Tailored to continuation of specific work

## Example Outputs

### Full Handoff Example
```markdown
# Project Handoff - 2026-01-18

## Current Status
- **Active Version**: UniversalORStrategyV8_2_UI_HORIZONTAL
- **Last Work**: Created horizontal button layout with resizable panel
- **Testing Status**: Not yet compiled or tested

## Recent Changes
- Redesigned UI from vertical (18 rows) to horizontal (3 button rows)
- Added resize grip for panel width adjustment (280-600px)
- Split entry buttons: Row 1 = Long/Short, Row 2 = RMA/TREND
- Created 5-button target row: T1 | T2 | T3 | Runner | BE

## File State
- **Project Location**: `c:\Users\Mohammed Khalid\OneDrive\Desktop\WSGTA\Github\universal-or-strategy\`
- **NinjaTrader Location**: `C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\Strategies\`
- **Deployed Version**: V8_2_UI_HORIZONTAL in both locations
- **Git Status**:
  - Modified: UniversalORStrategyV8_2_UI_HORIZONTAL.cs
  - Untracked: New skills (version-manager, file-manager, docs-manager, context-transfer)

## Pending Work
1. Compile V8_2_UI_HORIZONTAL in NinjaTrader (F5)
2. Test button layout on chart
3. Verify resize functionality works
4. Test all buttons still trigger correctly
5. Update changelog with UI redesign entry

## Important Context
- **Version Safety Protocol**: Always save under new name, never overwrite V8_2
- **Deployment**: Must copy to BOTH project and NinjaTrader
- **UI Title Bar**: Shows version (e.g., "═══ OR Strategy V8.2 UI HORIZONTAL ═══")
- **Trading Logic**: UNCHANGED - only UI modified

## Known Issues
- None currently - fresh version not yet tested

## Next Action
Open NinjaTrader, compile V8_2_UI_HORIZONTAL (F5), load on chart to verify layout.

---

**Paste this into new conversation and say**: "Continue from here"
```

### Quick Handoff Example
```markdown
# Quick Handoff

Working on: UniversalORStrategyV8_2_UI_HORIZONTAL
Last change: Horizontal button layout (3 rows instead of vertical stack)
Next step: Compile in NinjaTrader and test

Continue from here.
```

### Task-Specific Handoff Example
```markdown
# UI Redesign Handoff

Task: Horizontal button layout for V8.2
Status: Code written, not yet tested

Files:
- UniversalORStrategyV8_2_UI_HORIZONTAL.cs (both locations)

Changes:
- 3 horizontal button rows (Long/Short, RMA/TREND, Targets)
- Resize grip added
- Proportional button scaling

Next: Compile and verify layout on chart

Continue testing the UI redesign.
```

## Integration with Other Skills

### Works With:
- **version-safety**: Includes version naming in handoff
- **file-manager**: Notes deployment status
- **docs-manager**: Can reference recent changelog entries
- **version-manager**: Lists which version is active

### Use Cases:
1. **Session Timeout**: Generate handoff before session ends
2. **Context Limit**: Generate handoff before hitting token limit
3. **Switching Tasks**: Generate handoff to return to later
4. **Sharing Work**: Generate handoff to pass to another person/session

## Auto-Detection

Agent can detect when handoff might be needed:
- Conversation getting long (50+ messages)
- Token count approaching limit
- User says "I need to stop" or "continue this later"

## Output Format

Always output as:
1. **Markdown formatted** - easy to copy/paste
2. **Self-contained** - all context in one block
3. **Actionable** - clear next steps
4. **Timestamped** - know when it was generated

## Safety Rules
- Include ONLY necessary context (don't dump everything)
- Prioritize actionable info over history
- Keep file paths accurate
- Note any uncommitted changes
- Highlight any critical state

## Example Interaction

**User**: "Generate handoff prompt, I need to start a new conversation"

**Haiku Agent** (context-transfer):
1. Runs `git status`
2. Reads recent files
3. Checks plan file (if exists)
4. Scans for version info
5. Generates formatted handoff

**Output**: [Complete handoff prompt as shown above]

**User**: [Copies output, starts new conversation, pastes, says "Continue from here"]

**New Conversation Agent**: Reads context, understands state, continues work seamlessly
