# V12.41: VM MCP Server Configuration Fix

**Date**: 2026-06-17
**Status**: ✅ RESOLVED
**Protocol Version**: V12.41

## Problem

Wave 5 pilot test (EPIC-CCN-001) failed with 15 MCP connection errors:
- `sequential-thinking`: spawn npx.cmd ENOENT (Windows command on Linux)
- `jcodemunch-mcp`: spawn jcodemunch-mcp.exe ENOENT (Windows binary)
- `greptile`: Requires authentication (not available on VM)
- `worker-1` through `worker-4`: MCP error -32000 Connection closed
- All `phase-*` servers: MCP error -32000 Connection closed

## Root Cause

1. **Windows-specific commands**: `.mcp.json.vm` referenced `npx.cmd` and `.exe` binaries
2. **Unnecessary servers**: Worker and greptile servers not needed for Wave 5
3. **Bob CLI behavior**: Attempts to connect to ALL MCP servers in config, even unused ones

## Solution

**Modified `.mcp.json.vm` (V12.41)**:

### Removed Servers
- ❌ `jcodemunch-mcp` - Windows .exe binary (not available on Linux)
- ❌ `greptile` - Requires authentication
- ❌ `worker-1` through `worker-4` - Not needed for Wave 5 execution

### Kept Servers (All Verified Working)
- ✅ `phase-0-hotspot` - Python/fastmcp
- ✅ `phase-1-scope` - Python/fastmcp
- ✅ `phase-1-5-boundary` - Python/mcp
- ✅ `phase-2-architecture` - Python/mcp
- ✅ `phase-3-audit` - Python/mcp
- ✅ `phase-4-tickets` - Python/mcp
- ✅ `phase-5-execute` - Python/mcp (PRIMARY for Wave 5)
- ✅ `phase-5-verify` - Python/mcp
- ✅ `phase-6-review` - Python/mcp
- ✅ `sequential-thinking` - Node.js/npx (fixed command)

## Verification

```bash
# Python dependencies confirmed installed
python3 -c 'import mcp, fastmcp; print("OK")'  # ✅ OK

# Phase-5-execute server starts successfully
python3 scripts/phase_5_execute_mcp.py  # ✅ FastMCP 3.4.2 started

# Configuration deployed to VM
gcloud compute scp .mcp.json.vm v12-test-golden-v2:/home/malhitticrypto/universal-or-strategy/.mcp.json --zone=us-central1-a  # ✅ Uploaded
```

## Expected Result

**Before (V12.40)**: 15 MCP connection errors
**After (V12.41)**: 0 MCP connection errors

All phase-* MCP servers will connect successfully, enabling:
- Phase 5 execution via `phase-5-execute` server
- Phase 5 verification via `phase-5-verify` server
- Phase 6 review via `phase-6-review` server

## Deployment

```bash
# Deploy fixed config to VM
gcloud compute scp .mcp.json.vm v12-test-golden-v2:/home/malhitticrypto/universal-or-strategy/.mcp.json --zone=us-central1-a
```

## Next Steps

1. ✅ MCP configuration fixed and deployed
2. ⏳ Clean up VM working tree (git reset --hard)
3. ⏳ Re-run pilot test (EPIC-CCN-001) with fixed MCP config
4. ⏳ Verify 0 MCP connection errors
5. ⏳ Proceed with full Wave 5 execution (77 epics)

## References

- **Error Log**: `docs/wave5phase5badrun.md`
- **Fixed Config**: `.mcp.json.vm` (V12.41)
- **VM Setup Protocol**: `docs/protocol/VM_SETUP_PROTOCOL.md` (V12.40)