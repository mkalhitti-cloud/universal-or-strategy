# Hotfix: MCP Server Execution Recovery (Windows)

## 1. Forensic Diagnosis
The `sequential-thinking` and `chrome-devtools-mcp` servers are configured to use `npx` as the base command. On Windows, `npx` is a script (`.cmd` or `.ps1`), not a binary executable. The Antigravity process-spawning logic fails to resolve `npx` to `npx.cmd` when attempting to start the server, resulting in the error:
`The system cannot execute the specified program. calling 'initialize': EOF`.

## 2. Proposed Changes
Update `C:\Users\Mohammed Khalid\.gemini\antigravity\mcp_config.json` to use `npx.cmd` for all servers currently using `npx`.

### File: `C:\Users\Mohammed Khalid\.gemini\antigravity\mcp_config.json`

```diff
-      "command": "npx",
+      "command": "npx.cmd",
```
(Apply to both `sequential-thinking` and `chrome-devtools-mcp`)

## 3. Verification Plan
1. Apply the edit to `mcp_config.json`.
2. Verify that the `MCP Error` warning in the Antigravity UI disappears (User observation).
3. Successfully invoke the `sequential-thinking` tool from the Orchestrator (Antigravity).

## 4. Rollback Plan
Revert `npx.cmd` to `npx` if unexpected side effects occur.
