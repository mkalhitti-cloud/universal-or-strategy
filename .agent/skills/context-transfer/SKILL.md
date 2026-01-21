name: context-transfer
description: Standardized protocol for generating session handoff prompts using Gemini Flash delegation. Ensures zero context loss when switching IDEs, terminal agents, or hitting usage limits.

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
Primary Executor: Gemini 3 Flash (via delegation_bridge MCP)
Protocol: Wearable Project Standard
Tools: call_gemini_flash (read/analyze), grep_search
Type: Context Continuity Specialist
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
- **Project Location**: `${PROJECT_ROOT}/`
- **NinjaTrader Location**: `C:/Users/${USERNAME}/Documents/NinjaTrader 8/bin/Custom/Strategies/`
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

## Related Skills
- [delegation-bridge](../delegation-bridge/SKILL.md) - State persistence and retrieval
- [wearable-project](../antigravity-core/wearable-project.md) - Continuity standards
- [version-safety](../version-safety/SKILL.md) - File naming in handoffs

---
*Optimized for Gemini 3 Flash delegation via bridge (2026-01-21)*
