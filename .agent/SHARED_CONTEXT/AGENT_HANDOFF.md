# AGENT HANDOFF PROTOCOL

## Purpose
This protocol ensures a seamless handoff between different AI agents (Gemini, Claude, etc.) and IDEs (Antigravity, Cursor, Claude Code CLI) by providing a standardized set of context files and rules.

## What to Read First
Before starting any work, ALWAYS read these 4 files in order:
1.  [.agent/SHARED_CONTEXT/CURRENT_SESSION.md](file:///c:/Users/Mohammed%20Khalid/OneDrive/Desktop/WSGTA/Github/universal-or-strategy/.agent/SHARED_CONTEXT/CURRENT_SESSION.md) - Current active state and next steps.
2.  [.agent/TASKS/MASTER_TASKS.json](file:///c:/Users/Mohammed%20Khalid/OneDrive/Desktop/WSGTA/Github/universal-or-strategy/.agent/TASKS/MASTER_TASKS.json) - Full task list, dependencies, and assigned models.
3.  [.agent/SHARED_CONTEXT/V8_STATUS.json](file:///c:/Users/Mohammed%20Khalid/OneDrive/Desktop/WSGTA/Github/universal-or-strategy/.agent/SHARED_CONTEXT/V8_STATUS.json) - Status of the stable production version.
4.  [.agent/SHARED_CONTEXT/V9_STATUS.json](file:///c:/Users/Mohammed%20Khalid/OneDrive/Desktop/WSGTA/Github/universal-or-strategy/.agent/SHARED_CONTEXT/V9_STATUS.json) - Status of the experimental development version.

## Quick Status Summary
*   **V8 Production**: `STABLE` and `PROTECTED`. Do not modify.
*   **V9 Experimental**: `NEEDS TESTING`. Waiting for market open to verify TOS RTD live numbers.

## Next Immediate Tasks
*   **Test V9 TOS RTD**: When market opens (09:30 EST / 06:30 PST).
*   **Verify Production Backups**: V9 files have already been copied to `PRODUCTION/V9_STABLE/`.

## Model Recommendations
*   **Gemini Flash**: Default for all file operations, coordination, planning, and documentation.
*   **Claude Opus / Opus Thinking**: Use for complex debugging, especially if V9 TOS RTD fails testing.

## Important Reminders
*   **NEVER modify files in `PRODUCTION/`**: They are protected backups.
*   **Avoid API 400 Errors**: Use ONE tool call per message unless strictly necessary and safe.
*   **Update Context**: Always update `CURRENT_SESSION.md` before switching agents or finishing your turn.
*   **Follow the Plan**: Refer to `MASTER_TASKS.json` for architectural decisions and task hierarchy.
