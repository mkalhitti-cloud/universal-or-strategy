# Phase MCP Servers vs Custom Modes Analysis

**Date**: 2026-06-18
**Context**: Wave 6 Phase 1 execution showed phase-specific MCP connection errors

## Architecture Overview

### Dual-Layer System (Current)

The V12 workflow uses **BOTH** phase-specific MCP servers AND custom modes:

1. **Phase-Specific MCP Servers** (`.mcp.json.vm`):
   - `phase-0-hotspot` → `scripts/phase_0_hotspot_mcp_fastmcp.py`
   - `phase-1-scope` → `scripts/phase_1_scope_mcp_fastmcp.py`
   - `phase-1-5-boundary` → `scripts/phase_1_5_boundary_mcp.py`
   - `phase-2-architecture` → `scripts/phase_2_architecture_mcp.py`
   - `phase-3-audit` → `scripts/phase_3_audit_mcp.py`
   - `phase-4-tickets` → `scripts/phase_4_tickets_mcp.py`
   - `phase-4-5-review` → `scripts/phase_4_5_ticket_review_mcp.py`
   - `phase-5-execute` → `scripts/phase_5_execute_mcp.py`
   - `phase-5-verify` → `scripts/phase_5_verify_mcp.py`
   - `phase-6-review` → `scripts/phase_6_review_mcp.py`

2. **Custom Modes** (`.bob/custom_modes.yaml`):
   - `v12-phase0-hotspot` - Phase 0 execution mode
   - `v12-phase1-scope` - Phase 1 execution mode
   - `v12-phase1-5-boundary` - Phase 1.5 execution mode
   - `v12-phase2-architecture` - Phase 2 execution mode
   - `v12-phase3-audit` - Phase 3 execution mode
   - `v12-phase4-tickets` - Phase 4 execution mode
   - `v12-phase4-5-review` - Phase 4.5 execution mode
   - `v12-engineer` - Phase 5 execution mode
   - `v12-phase5-v-verify` - Phase 5.V execution mode
   - `v12-phase6-review` - Phase 6 execution mode

## Key Finding: They Are NOT Replacements

**Phase-specific MCP servers** and **custom modes** serve DIFFERENT purposes:

### Phase-Specific MCP Servers (Python Scripts)
**Purpose**: Provide **phase-specific tools** via MCP protocol
**Example**: `phase-1-scope` MCP server provides `execute_phase_1` tool
**Usage**: Called BY Bob CLI when executing a phase
**Status**: ❌ Connection errors in Wave 6 Phase 1 execution

### Custom Modes (Bob CLI Modes)
**Purpose**: Define **agent behavior and constraints** for each phase
**Example**: `v12-phase1-scope` mode defines what the agent should do in Phase 1
**Usage**: Bob CLI runs in this mode (`bob --mode v12-phase1-scope`)
**Status**: ✅ Working correctly in Wave 6 Phase 1 execution

## Connection Errors Explained

### What Failed
```
[ERROR] Error during discovery for server 'phase-1-scope': 
Connection failed for 'phase-1-scope': MCP error -32000: Connection closed
```

### Why It Failed
The phase-specific MCP servers (`phase-0-hotspot`, `phase-1-scope`, etc.) failed to connect because:
1. They are Python FastMCP servers that need to be running
2. They provide `execute_phase_X` tools for orchestration
3. They are NOT needed when Bob CLI is called directly with `--mode`

### Why Phase 1 Still Succeeded
Phase 1 succeeded because:
1. Bob CLI ran in `v12-phase1-scope` **custom mode** ✅
2. Custom mode defines agent behavior (read hotspots, use jCodemunch, write scope)
3. Bob used **jCodemunch MCP** and **Sequential Thinking MCP** directly ✅
4. Phase-specific MCP servers are only needed for **orchestration** (not direct execution)

## MCP Tool Usage by Phase

### Phase 0 (Hotspot Analysis)
**Custom Mode**: `v12-phase0-hotspot`
**MCP Tools Required**:
- ✅ `jcodemunch-mcp`: `search_symbols`, `get_hotspots`, `get_symbol_complexity`, `get_blast_radius`
- ✅ `sequential-thinking`: `sequentialthinking`
**Phase-Specific MCP**: `phase-0-hotspot` (provides `execute_phase_0` tool for orchestration)

### Phase 1 (Scope Definition)
**Custom Mode**: `v12-phase1-scope`
**MCP Tools Required**:
- ✅ `jcodemunch-mcp`: `get_file_outline`, `find_references`, `get_dependency_graph`
- ✅ `sequential-thinking`: `sequentialthinking`
**Phase-Specific MCP**: `phase-1-scope` (provides `execute_phase_1` tool for orchestration)

### Phase 1.5 (Boundary Validation)
**Custom Mode**: `v12-phase1-5-boundary`
**MCP Tools Required**:
- ✅ `jcodemunch-mcp`: `get_symbol_source`, `get_blast_radius`, `find_references`
- ✅ `sequential-thinking`: `sequentialthinking`
**Phase-Specific MCP**: `phase-1-5-boundary` (provides `execute_phase_1_5` tool)

### Phase 2 (Architecture Planning)
**Custom Mode**: `v12-phase2-architecture`
**MCP Tools Required**:
- ✅ `jcodemunch-mcp`: `get_context_bundle`, `get_call_hierarchy`, `get_dependency_graph`
- ✅ `sequential-thinking`: `sequentialthinking`
- ✅ `graphify`: For codebase structure visualization
**Phase-Specific MCP**: `phase-2-architecture` (provides `execute_phase_2` tool)

### Phase 3 (DNA & PR Audit)
**Custom Mode**: `v12-phase3-audit`
**MCP Tools Required**:
- ✅ `jcodemunch-mcp`: `search_ast`, `get_layer_violations`, `get_dependency_cycles`
- ✅ `sequential-thinking`: `sequentialthinking`
- ✅ `greptile`: For PR hygiene checks
**Phase-Specific MCP**: `phase-3-audit` (provides `execute_phase_3` tool)

### Phase 4 (Ticket Generation)
**Custom Mode**: `v12-phase4-tickets`
**MCP Tools Required**:
- ✅ `jcodemunch-mcp`: `get_symbol_complexity`, `get_extraction_candidates`
- ✅ `sequential-thinking`: `sequentialthinking`
**Phase-Specific MCP**: `phase-4-tickets` (provides `execute_phase_4` tool)

### Phase 4.5 (Ticket Review)
**Custom Mode**: `v12-phase4-5-review`
**MCP Tools Required**:
- ✅ `sequential-thinking`: `sequentialthinking`
**Phase-Specific MCP**: `phase-4-5-review` (provides `execute_phase_4_5` tool)

### Phase 5 (Ticket Execution)
**Custom Mode**: `v12-engineer`
**MCP Tools Required**:
- ✅ `jcodemunch-mcp`: `get_symbol_source`, `get_context_bundle`, `plan_refactoring`
- ✅ `sequential-thinking`: `sequentialthinking`
**Phase-Specific MCP**: `phase-5-execute` (provides `execute_phase_5` tool)

### Phase 5.V (Verification)
**Custom Mode**: `v12-phase5-v-verify`
**MCP Tools Required**:
- ✅ `jcodemunch-mcp`: `get_symbol_complexity`, `get_changed_symbols`
- ✅ `sequential-thinking`: `sequentialthinking`
- ✅ `greptile`: For code quality checks
**Phase-Specific MCP**: `phase-5-verify` (provides `execute_phase_5_verify` tool)

### Phase 6 (Final Review)
**Custom Mode**: `v12-phase6-review`
**MCP Tools Required**:
- ✅ `jcodemunch-mcp`: `get_repo_health`, `get_hotspots`
- ✅ `sequential-thinking`: `sequentialthinking`
- ✅ `greptile`: For final PR audit
**Phase-Specific MCP**: `phase-6-review` (provides `execute_phase_6` tool)

## Recommendation: Keep Both Systems

### Phase-Specific MCP Servers
**Status**: ❌ Currently failing to connect
**Purpose**: Orchestration tools (`execute_phase_X`)
**Used By**: Hierarchical orchestrator (if implemented)
**Action**: Can be **deprecated** if not used for orchestration

### Custom Modes
**Status**: ✅ Working correctly
**Purpose**: Define agent behavior for each phase
**Used By**: Bob CLI direct execution (`bob --mode v12-phase1-scope`)
**Action**: **KEEP** - these are essential for wave execution

## Conclusion

**Answer to User's Question**: 
> "has this been replaced by custom modes?"

**NO** - Phase-specific MCP servers have NOT been replaced by custom modes. They serve different purposes:

1. **Phase-Specific MCPs**: Orchestration tools (currently unused, can be deprecated)
2. **Custom Modes**: Agent behavior definitions (actively used, essential)

The connection errors for phase-specific MCPs are **non-blocking** because:
- Wave execution uses Bob CLI with custom modes directly
- Custom modes use jCodemunch + Sequential Thinking MCPs (which work)
- Phase-specific MCPs are only needed for hierarchical orchestration (not implemented)

**Recommendation**: Document that phase-specific MCP connection errors are expected and non-blocking for wave execution.