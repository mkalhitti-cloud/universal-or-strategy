# Disable Unused MCP Servers

## Context
For autonomous-refactor mode on VM, we use Bob Shell with custom modes directly (v12-phase0-hotspot, v12-phase1-scope, etc.), NOT the worker MCP orchestration.

## MCPs to Disable

### Worker MCPs (NOT NEEDED)
- ❌ worker-1
- ❌ worker-2 (if exists)
- ❌ worker-3
- ❌ worker-4

**Reason**: These were for the old orchestration model where Bob IDE delegated to worker agents via MCP. Now we use Bob Shell custom modes directly on the VM.

### Phase MCPs (KEEP THESE)
- ✅ phase-0-hotspot
- ✅ phase-1-scope
- ✅ phase-1-5-boundary
- ✅ phase-2-architecture
- ✅ phase-3-audit
- ✅ phase-4-tickets
- ✅ phase-5-execute
- ✅ phase-5-verify
- ✅ phase-6-review

**Reason**: These are still useful for local development and testing individual phases.

### Essential MCPs (KEEP THESE)
- ✅ jcodemunch-mcp (code navigation)
- ✅ greptile (GitHub integration)
- ✅ sequential-thinking (reasoning)

## How to Disable

### Option 1: Bob IDE Settings UI
1. Open Bob Settings (gear icon)
2. Go to MCP section
3. Toggle OFF: worker-1, worker-3, worker-4
4. Restart Bob IDE

### Option 2: Edit .mcp.json Directly
1. Open `.mcp.json`
2. Comment out or remove worker MCP entries
3. Restart Bob IDE

## Expected Impact

### Before Disabling
- **Context**: ~60k/200k tokens (30%)
- **Worker MCPs**: ~5-10k tokens (5 tools × 4 servers = 20 tool schemas)

### After Disabling
- **Context**: ~50-55k/200k tokens (25-27%)
- **Savings**: 5-10k tokens (8-17% reduction)
- **Available**: 145-150k tokens for work

## Verification

After disabling, check environment_details for MCP servers list. Should NOT see:
- worker-1
- worker-2
- worker-3
- worker-4

Should still see:
- jcodemunch-mcp
- greptile
- phase-* servers
- sequential-thinking

## Note

This is a **local optimization** for Bob IDE. The VM execution uses Bob Shell custom modes directly, so worker MCPs were never used there anyway.