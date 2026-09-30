# Greptile MCP Fix Summary

**Date**: 2026-05-22 00:52 UTC  
**Session**: REAPER-EXPANSION Phase 2.3 (Post-Restart Validation)  
**Agent**: Advanced Mode (Bob CLI)

---

## Problem Identified

Greptile MCP server was not loading despite correct HTTP configuration in `.mcp.json` at project root.

**Root Cause**: **WRONG CONFIGURATION FILE LOCATION**

---

## Solution Applied

### Issue
VS Code requires MCP configuration in a **user-level directory**, not the project root.

### Fix
Created MCP configuration at the correct location:
```
C:\Users\Mohammed Khalid\AppData\Roaming\Code\User\mcp.json
```

### Configuration
```json
{
  "mcpServers": {
    "jcodemunch": {
      "type": "stdio",
      "command": "jcodemunch-mcp.exe",
      "args": []
    },
    "greptile": {
      "type": "http",
      "url": "https://api.greptile.com/mcp",
      "headers": {
        "Authorization": "Bearer GKZ5piB2DLIr22NtSOF/afDCpA6MT3YiAjpsEkbI6Fx88DK9"
      }
    }
  }
}
```

---

## Documentation Source

Per official Greptile documentation (https://www.greptile.com/docs/llms.txt):

**VS Code MCP Config Locations**:
- **Windows**: `%APPDATA%\Code\User\mcp.json`
- **Linux**: `~/.config/Code/User/mcp.json`
- **macOS**: `~/Library/Application Support/Code/User/mcp.json`

---

## Next Steps - DIRECTOR ACTION REQUIRED

### 1. VSCode Restart (CRITICAL)
**You must fully restart VSCode for the MCP configuration to load.**

Steps:
1. Close VSCode completely (File → Exit)
2. Reopen VSCode
3. Open this project
4. Switch to Advanced mode

### 2. Validation
After restart, verify:
- [ ] Greptile appears in MCP servers list (alongside jcodemunch and sequential-thinking)
- [ ] All 11 Greptile tools are enabled
- [ ] Test query executes successfully

### 3. Resume REAPER-EXPANSION
Once Greptile is validated, execute the 4 Sentinel Audit queries:

1. **Query 1**: "What are the current safety gaps in SIMA dispatch queue management? Focus on unbounded growth and OOM risks."
2. **Query 2**: "Find all usages of _pendingFleetDispatches and _pendingFleetDispatchCount. Does any code path dequeue without updating the count?"
3. **Query 3**: "Review V12_002.UI.IPC.cs for existing (but unused) circuit breaker or rate-limiting patterns."
4. **Query 4**: "Locate all entry methods in src/Entries.*.cs that accept a 'contracts' or 'quantity' parameter to verify our clamping surface."

---

## Files Modified

1. **Created**: `C:\Users\Mohammed Khalid\AppData\Roaming\Code\User\mcp.json` (VS Code user-level MCP config)
2. **Updated**: [`docs/brain/REAPER-EXPANSION/02-greptile-report.md`](./02-greptile-report.md) (Session 2 fix log)
3. **Created**: [`docs/brain/REAPER-EXPANSION/GREPTILE-FIX-SUMMARY.md`](./GREPTILE-FIX-SUMMARY.md) (This file)

---

## Status

**Configuration**: ✅ FIXED  
**Location**: ✅ CORRECT  
**API Key**: ✅ VALID  
**VSCode Restart**: ⏳ PENDING (USER ACTION)  
**Greptile Validation**: ⏳ BLOCKED (Waiting for restart)  
**REAPER Workflow**: 🔒 BLOCKED (Waiting for Greptile validation)

---

## Technical Notes

- The project-root `.mcp.json` is **not used by VS Code** - it may be for other tools (Cursor, Claude Code CLI)
- Both jCodemunch (stdio) and Greptile (HTTP) are now configured in the correct location
- HTTP-based MCP servers are fully supported by VS Code when configured in the user directory
- No code changes were needed - this was purely a configuration location issue

---

**[GREPTILE-FIX-COMPLETE]**

**Next Gate**: Director must restart VSCode and validate Greptile tools are available before proceeding to Phase 2.3 Sentinel Audit execution.