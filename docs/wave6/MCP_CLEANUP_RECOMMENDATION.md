# MCP Cleanup Recommendation - Phase-Specific Servers

**Date**: 2026-06-18
**Context**: Wave 6 Phase 1 showed connection errors for phase-specific MCP servers

## Finding: Phase-Specific MCPs Are Obsolete

### Original Architecture (Coordinator Pattern)
Phase-specific MCP servers (`phase-0-hotspot`, `phase-1-scope`, etc.) were designed as **coordinators**:
- Provide `execute_phase_X` tool
- Return **instructions** for Bob IDE to follow
- Bob reads instructions and executes them

### Current Architecture (Custom Modes)
Custom modes (`v12-phase0-hotspot`, `v12-phase1-scope`, etc.) replaced this pattern:
- Define agent **behavior** directly in `.bob/custom_modes.yaml`
- Bob executes work itself (not following external instructions)
- Uses jCodemunch + Sequential Thinking MCPs directly

## Evidence: Phase 1 Execution

### What Happened
```bash
bob --yolo --mode "v12-phase1-scope" "Define extraction scope for EPIC-CCN-024..."
```

**Custom Mode Used**: ✅ `v12-phase1-scope`
**MCP Tools Used**: ✅ `jcodemunch-mcp`, `sequential-thinking`
**Phase-Specific MCP**: ❌ `phase-1-scope` (connection error, not needed)

### Why It Worked Without Phase-Specific MCP
Bob's custom mode (`v12-phase1-scope`) defines the behavior directly:
```yaml
roleDefinition: |
  You are the V12 Scope Analyzer for Phase 1. Your job is to:
  1. Read Phase 0 hotspot analysis (00-hotspots.md)
  2. Use jCodemunch MCP to verify code structure
  3. Use Sequential Thinking MCP for scope boundary decisions
  4. Define extraction scope with clear boundaries
  5. Write 00-scope.md with scope definition
```

Bob doesn't need `phase-1-scope` MCP to tell it what to do - the custom mode already defines it.

## Obsolete MCP Servers

The following MCP servers in `.mcp.json.vm` are **obsolete**:

1. ❌ `phase-0-hotspot` - Replaced by `v12-phase0-hotspot` custom mode
2. ❌ `phase-1-scope` - Replaced by `v12-phase1-scope` custom mode
3. ❌ `phase-1-5-boundary` - Replaced by `v12-phase1-5-boundary` custom mode
4. ❌ `phase-2-architecture` - Replaced by `v12-phase2-architecture` custom mode
5. ❌ `phase-3-audit` - Replaced by `v12-phase3-audit` custom mode
6. ❌ `phase-4-tickets` - Replaced by `v12-phase4-tickets` custom mode
7. ❌ `phase-4-5-review` - Replaced by `v12-phase4-5-review` custom mode
8. ❌ `phase-5-execute` - Replaced by `v12-engineer` custom mode
9. ❌ `phase-5-verify` - Replaced by `v12-phase5-v-verify` custom mode
10. ❌ `phase-6-review` - Replaced by `v12-phase6-review` custom mode

## Essential MCP Servers (Keep)

The following MCP servers are **essential** and should be kept:

1. ✅ `jcodemunch-mcp` - Code analysis (used by all custom modes)
2. ✅ `sequential-thinking` - Structured reasoning (used by all custom modes)
3. ✅ `greptile` - PR hygiene checks (used by Phases 3, 5.V, 6)
4. ✅ `graphify` - Codebase visualization (used by Phase 2)

## Recommended Cleanup

### Step 1: Update `.mcp.json.vm`

**Remove** phase-specific MCP servers:
```json
{
  "mcpServers": {
    "sequential-thinking": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-sequential-thinking"]
    }
  },
  "_comment": "VM-specific MCP configuration (V12.42 - Cleanup). Phase-specific MCPs removed (replaced by custom modes). Essential MCPs: sequential-thinking (all phases). jcodemunch-mcp excluded (Windows-only). greptile excluded (auth required)."
}
```

### Step 2: Archive Phase-Specific MCP Scripts

Move to archive directory:
```bash
mkdir -p scripts/archive/phase-mcp-servers
mv scripts/phase_*_mcp*.py scripts/archive/phase-mcp-servers/
```

Add README explaining why they were archived:
```markdown
# Archived: Phase-Specific MCP Servers

These MCP servers implemented a coordinator pattern where they returned
instructions for Bob IDE to follow. They were replaced by custom modes
in `.bob/custom_modes.yaml` which define agent behavior directly.

Archived: 2026-06-18
Reason: Obsolete - replaced by custom modes
Wave: Wave 6 Phase 1 cleanup
```

### Step 3: Update Documentation

Update `AGENTS.md` to remove references to phase-specific MCP servers.

## Impact Analysis

### Zero Impact on Wave Execution
- Wave 6 Phase 1 already succeeded without phase-specific MCPs
- All 79 epics completed using custom modes only
- Connection errors were non-blocking

### Simplified Architecture
- Fewer MCP servers to maintain
- Clearer separation: custom modes define behavior, MCPs provide tools
- Reduced confusion about which system is used

### Preserved Functionality
- All phase execution capabilities preserved in custom modes
- jCodemunch + Sequential Thinking MCPs still available
- No loss of features

## Verification

After cleanup, verify Wave 6 Phase 1.5 execution:
```bash
# Should work without phase-specific MCPs
bob --yolo --mode "v12-phase1-5-boundary" "Validate scope for EPIC-CCN-024..."
```

Expected:
- ✅ Custom mode `v12-phase1-5-boundary` executes
- ✅ Uses `jcodemunch-mcp` and `sequential-thinking`
- ✅ No connection errors for removed phase-specific MCPs

## Conclusion

**Answer**: YES - Phase-specific MCP servers (`phase-0-hotspot`, `phase-1-scope`, etc.) were replaced by custom modes (`v12-phase0-hotspot`, `v12-phase1-scope`, etc.) and should be removed from `.mcp.json.vm`.

**Action**: Remove obsolete phase-specific MCP servers to simplify architecture and eliminate connection errors.

**Timeline**: Can be done immediately - Wave 6 execution already proves they're not needed.