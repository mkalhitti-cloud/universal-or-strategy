# VM MCP Requirements Matrix (Wave 7)

**Version**: 1.0
**Date**: 2026-06-20
**Status**: CRITICAL - VM Missing Required MCPs

## Executive Summary

**BLOCKER**: VM currently has only 1 of 4 required MCPs installed. Wave 7 execution will FAIL without installing missing MCPs.

**Current VM State**:
- ✅ Sequential Thinking MCP (installed)
- ❌ jCodemunch MCP (MISSING - required by 8/10 phases)
- ❌ Graphify MCP (MISSING - required by 1/10 phases)
- ❌ Greptile MCP (MISSING - required by 3/10 phases)

## Phase × MCP Requirements Matrix

| Phase | Mode Slug | Sequential Thinking | jCodemunch | Graphify | Greptile | Status |
|-------|-----------|---------------------|------------|----------|----------|--------|
| **0** | v12-phase0-hotspot | ✅ MANDATORY | ✅ MANDATORY | ❌ | ❌ | ❌ BLOCKED |
| **1** | v12-phase1-scope | ✅ MANDATORY | ✅ MANDATORY | ❌ | ❌ | ❌ BLOCKED |
| **1.5** | v12-phase1-5-boundary | ✅ MANDATORY | ✅ MANDATORY | ❌ | ❌ | ❌ BLOCKED |
| **2** | v12-phase2-architecture | ✅ MANDATORY | ✅ MANDATORY | ✅ MANDATORY | ❌ | ❌ BLOCKED |
| **3** | v12-phase3-audit | ✅ MANDATORY | ✅ MANDATORY | ❌ | ✅ MANDATORY | ❌ BLOCKED |
| **4** | v12-phase4-tickets | ✅ MANDATORY | ✅ MANDATORY | ❌ | ❌ | ❌ BLOCKED |
| **4.5** | v12-phase4-5-review | ✅ MANDATORY | ❌ | ❌ | ❌ | ✅ READY |
| **5** | v12-engineer | ✅ MANDATORY | ✅ MANDATORY | ❌ | ❌ | ❌ BLOCKED |
| **5.V** | v12-phase5-v-verify | ✅ MANDATORY | ✅ MANDATORY | ❌ | ✅ MANDATORY | ❌ BLOCKED |
| **6** | v12-phase6-review | ✅ MANDATORY | ✅ MANDATORY | ❌ | ✅ MANDATORY | ❌ BLOCKED |

**Summary**:
- ✅ **1 phase ready** (Phase 4.5 only)
- ❌ **9 phases blocked** (missing jCodemunch, Graphify, or Greptile)

## MCP Usage Statistics

### jCodemunch MCP
**Required by**: 8 out of 10 phases (80%)
**Status**: ❌ NOT INSTALLED ON VM

**Phases**:
- Phase 0: Hotspot analysis (search_symbols, get_hotspots, get_symbol_complexity, get_blast_radius)
- Phase 1: Scope definition (get_file_outline, find_references, get_dependency_graph)
- Phase 1.5: Boundary validation (get_symbol_source, get_blast_radius, find_references)
- Phase 2: Architecture planning (get_context_bundle, get_call_hierarchy, get_dependency_graph)
- Phase 3: DNA audit (search_ast, get_layer_violations, get_dependency_cycles)
- Phase 4: Ticket generation (get_symbol_complexity, get_extraction_candidates)
- Phase 5: Ticket execution (get_symbol_source, get_context_bundle, plan_refactoring)
- Phase 5.V: Verification (get_symbol_complexity, get_changed_symbols)
- Phase 6: Final review (get_repo_health, get_hotspots)

**Critical**: jCodemunch is the MOST IMPORTANT MCP for Wave 7 execution.

### Sequential Thinking MCP
**Required by**: 10 out of 10 phases (100%)
**Status**: ✅ INSTALLED ON VM

**Purpose**: Complex reasoning and validation for all phases.

### Graphify MCP
**Required by**: 1 out of 10 phases (10%)
**Status**: ❌ NOT INSTALLED ON VM

**Phases**:
- Phase 2: Architecture planning (codebase structure visualization)

**Note**: Lower priority than jCodemunch, but still required for Phase 2.

### Greptile MCP
**Required by**: 3 out of 10 phases (30%)
**Status**: ❌ NOT INSTALLED ON VM

**Phases**:
- Phase 3: DNA audit (PR hygiene checks)
- Phase 5.V: Verification (code quality checks)
- Phase 6: Final review (final PR audit)

**Note**: Requires authentication. May need special setup on VM.

## Installation Priority

### Priority 1: CRITICAL (Wave Blocker)
**jCodemunch MCP** - Required by 8/10 phases
- Installation guide: [`docs/protocol/JCODEMUNCH_VM_INSTALLATION.md`](JCODEMUNCH_VM_INSTALLATION.md)
- Binary: `jcodemunch-mcp.exe` (Windows-only)
- **BLOCKER**: Without this, only Phase 4.5 can execute

### Priority 2: HIGH (Phase 2 Blocker)
**Graphify MCP** - Required by Phase 2
- Installation: TBD (need to research Linux compatibility)
- May be available via npm or pip

### Priority 3: MEDIUM (Phases 3, 5.V, 6 Blocker)
**Greptile MCP** - Required by 3 phases
- Installation: TBD (requires authentication)
- May need API key configuration

## Current VM Configuration

**File**: `.mcp.json.vm`

```json
{
  "mcpServers": {
    "sequential-thinking": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-sequential-thinking"]
    }
  },
  "_comment": "VM-specific MCP configuration (V12.42 - Cleanup 2026-06-18). Phase-specific MCPs removed (replaced by custom modes in .bob/custom_modes.yaml). Essential MCPs: sequential-thinking (used by all phases). Excluded: jcodemunch-mcp.exe (Windows-only), greptile (auth required), worker-* (not needed for wave execution)."
}
```

**Issues**:
1. Comment incorrectly states jCodemunch is "Windows-only" and excluded
2. Comment incorrectly states Greptile requires auth and is excluded
3. Comment incorrectly states these MCPs are "not needed for wave execution"
4. **Reality**: 9 out of 10 phases REQUIRE these MCPs

## Required VM Configuration

**Updated `.mcp.json.vm`** (after installation):

```json
{
  "mcpServers": {
    "sequential-thinking": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-sequential-thinking"]
    },
    "jcodemunch-mcp": {
      "type": "stdio",
      "command": "/path/to/jcodemunch-mcp",
      "args": [],
      "env": {
        "JCODEMUNCH_USE_AI_SUMMARIES": "false"
      }
    },
    "graphify": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-graphify"]
    },
    "greptile": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@greptile/mcp-server"],
      "env": {
        "GREPTILE_API_KEY": "${GREPTILE_API_KEY}"
      }
    }
  },
  "_comment": "VM-specific MCP configuration for Wave 7 autonomous execution. All 4 MCPs are MANDATORY for 9/10 phases. Sequential Thinking: all phases. jCodemunch: 8/10 phases. Graphify: Phase 2. Greptile: Phases 3, 5.V, 6."
}
```

## Installation Checklist

### Step 1: jCodemunch MCP (CRITICAL)
- [ ] Follow [`docs/protocol/JCODEMUNCH_VM_INSTALLATION.md`](JCODEMUNCH_VM_INSTALLATION.md)
- [ ] Download Linux binary from GitHub releases
- [ ] Install to `/usr/local/bin/jcodemunch-mcp` or `~/.local/bin/jcodemunch-mcp`
- [ ] Make executable: `chmod +x /path/to/jcodemunch-mcp`
- [ ] Test: `jcodemunch-mcp --version`
- [ ] Update `.mcp.json.vm` with correct path

### Step 2: Graphify MCP (HIGH)
- [ ] Research Linux installation method (npm or pip)
- [ ] Install via package manager
- [ ] Test: `npx -y @modelcontextprotocol/server-graphify --version` (if npm)
- [ ] Update `.mcp.json.vm` with correct command

### Step 3: Greptile MCP (MEDIUM)
- [ ] Obtain Greptile API key
- [ ] Set environment variable: `export GREPTILE_API_KEY=<key>`
- [ ] Install via npm: `npm install -g @greptile/mcp-server`
- [ ] Test: `npx -y @greptile/mcp-server --version`
- [ ] Update `.mcp.json.vm` with API key configuration

### Step 4: Verification
- [ ] Test each MCP individually
- [ ] Run Phase 4.5 (only phase that doesn't need jCodemunch) to verify Sequential Thinking works
- [ ] Run Phase 0 test to verify jCodemunch works
- [ ] Run Phase 2 test to verify Graphify works
- [ ] Run Phase 3 test to verify Greptile works

## Impact Analysis

### Without jCodemunch MCP
**Blocked Phases**: 0, 1, 1.5, 2, 3, 4, 5, 5.V, 6 (9 out of 10)
**Impact**: Wave 7 execution is COMPLETELY BLOCKED
**Workaround**: NONE - jCodemunch is mandatory for code analysis

### Without Graphify MCP
**Blocked Phases**: 2 (Architecture Planning)
**Impact**: Cannot visualize codebase structure
**Workaround**: Manual architecture planning (not recommended)

### Without Greptile MCP
**Blocked Phases**: 3 (DNA Audit), 5.V (Verification), 6 (Final Review)
**Impact**: Cannot perform PR hygiene checks and code quality audits
**Workaround**: Manual code review (not recommended)

## Next Steps

1. **IMMEDIATE**: Install jCodemunch MCP on VM (follow installation guide)
2. **HIGH**: Research and install Graphify MCP
3. **MEDIUM**: Configure Greptile MCP with API key
4. **FINAL**: Update `.mcp.json.vm` with all 4 MCPs
5. **VERIFY**: Test each phase mode to confirm MCPs work

## References

- Installation Guide: [`docs/protocol/JCODEMUNCH_VM_INSTALLATION.md`](JCODEMUNCH_VM_INSTALLATION.md)
- Custom Modes: [`.bob/custom_modes.yaml`](../../.bob/custom_modes.yaml)
- Wave 7 Setup: [`docs/brain/WAVE7_SETUP_COMPLETE.md`](../brain/WAVE7_SETUP_COMPLETE.md)
- Building-Blocks Architecture: [`building-blocks/autonomous-refactoring/ARCHITECTURE.md`](../../building-blocks/autonomous-refactoring/ARCHITECTURE.md)

## Version History

- **1.0** (2026-06-20): Initial matrix created after discovering VM MCP gap