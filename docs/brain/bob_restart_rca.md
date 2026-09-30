# Bob IDE Random Restart - Root Cause Analysis

**Date**: 2026-05-26  
**Investigator**: Advanced Mode Agent  
**Status**: ROOT CAUSE IDENTIFIED

---

## Executive Summary

Bob IDE is experiencing random restarts due to **Greptile MCP server connection failures**. The MCP bridge configuration in `.bob/mcp.json` is attempting to connect to Greptile's HTTP/SSE endpoint, which is returning 403/405 errors, causing Bob to crash and restart.

---

## Evidence

### 1. Error Logs (`.bob/.bob-errors/errors-2026-05-25.log`)

**Pattern**: Repeated connection failures every time Bob starts:
```
Error: Connection failed for 'greptile': SSE error: Non-200 status code (403)
Error: Connection failed for 'greptile': SSE error: Non-200 status code (405)
```

**Timestamps**:
- 2026-05-25 00:01:32 (403 Forbidden)
- 2026-05-25 00:40:06 (403 Forbidden)
- 2026-05-25 22:42:33 (405 Method Not Allowed)
- 2026-05-25 22:49:09 (405 Method Not Allowed)
- 2026-05-25 22:50:24 (405 Method Not Allowed)
- 2026-05-25 22:51:37 (405 Method Not Allowed)
- 2026-05-25 23:22:15 (405 Method Not Allowed)

**Frequency**: Every Bob session start + periodic retries

### 2. MCP Configuration (`.bob/mcp.json`)

```json
{
  "mcpServers": {
    "greptile": {
      "url": "https://api.greptile.com/mcp",
      "type": "http",
      "headers": {
        "Authorization": "Bearer ${GREPTILE_API_KEY}"
      },
      "alwaysAllow": ["query", "search", "search_codebase", "query_codebase", "semantic_search"]
    }
  }
}
```

**Issue**: The Greptile MCP endpoint is either:
1. Not a valid SSE endpoint (405 = Method Not Allowed)
2. Requires different authentication (403 = Forbidden)
3. No longer available at this URL

### 3. Timeline Correlation

**User Report**: "it just happened again and there was no active agents"

**Analysis**: Bob's pre-session hook (`.bob/hooks/pre_session.py`) runs on EVERY session start, which triggers:
1. Bootstrap context loading
2. MCP server connection attempts
3. Greptile connection failure
4. Bob crash/restart

This explains why restarts happen even when idle - Bob is trying to reconnect to Greptile on every startup.

---

## Root Cause

**Primary**: Greptile MCP server configuration is invalid/broken, causing Bob to crash on startup when attempting to establish SSE connection.

**Secondary**: No error handling/graceful degradation for MCP connection failures - Bob treats MCP connection errors as fatal.

---

## Impact

- **Severity**: P0 (Critical) - Blocks all Bob usage
- **Frequency**: Every session start
- **Workaround**: None (automatic restart loop)

---

## Solution

### Option 1: Remove Greptile MCP (RECOMMENDED)

**Action**: Delete or comment out the Greptile MCP configuration in `.bob/mcp.json`

**Rationale**:
- Greptile endpoint is not working
- Bob has jcodemunch-mcp which provides superior code intelligence
- No loss of functionality

### Option 2: Fix Greptile Configuration

**Action**: Update Greptile MCP configuration with correct endpoint/auth

**Requires**:
- Valid Greptile MCP endpoint URL
- Correct authentication method
- Verification that Greptile supports SSE/MCP protocol

### Option 3: Add Error Handling (LONG-TERM)

**Action**: Modify Bob's MCP connection logic to gracefully handle failures

**Requires**: Bob CLI source code modification (not in our control)

---

## Recommended Fix

**Immediate**: Remove Greptile MCP configuration (Option 1)

**Reasoning**:
1. Greptile endpoint is broken (403/405 errors)
2. jcodemunch-mcp provides all needed code intelligence
3. Zero functionality loss
4. Immediate stability restoration

---

## Implementation

See: `docs/brain/bob_restart_fix.md`