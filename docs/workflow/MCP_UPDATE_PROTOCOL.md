# MCP Update Protocol

**Version**: 2.0
**Date**: 2026-07-02
**Purpose**: Define when to update jcodemunch index and graphify knowledge graph

## Critical MCPs

### 1. jcodemunch-mcp
**Purpose**: Code navigation, symbol search, dependency analysis
**Type**: Stdio (local executable)
**Status**: ✅ Restored to `.bob/mcp.json`

### 2. greptile
**Purpose**: Semantic code search, custom context, PR analysis
**Type**: Streamable HTTP (cloud API)
**Status**: ✅ Preserved in `.bob/mcp.json`

### 3. worker-1 through worker-4
**Purpose**: Parallel epic execution via MCP tools
**Type**: Stdio (Python scripts)
**Status**: ✅ Added to `.bob/mcp.json`

## Graphify vs jcodemunch

### Graphify
**Does NOT need MCP** - It's a CLI tool that generates static files:
- `graphify-out/graph.json` - Knowledge graph
- `graphify-out/GRAPH_REPORT.md` - Analysis report
- `graphify-out/wiki/` - Documentation

**How it works**:
1. Run `graphify update .` (CLI command)
2. Reads source files directly
3. Generates static output files
4. Bob reads the output files (no MCP needed)

### jcodemunch-mcp
**Requires MCP** - It's a server that indexes code:
- Runs as background process
- Maintains in-memory index
- Provides real-time queries via MCP tools
- Much faster than reading files directly

## Update Frequency

### jcodemunch Index

**Update Trigger**: After editing source files

**Frequency Options**:

| Trigger | Command | When to Use |
|---------|---------|-------------|
| **Every Edit** | `register_edit(repo, file_paths)` | ✅ **RECOMMENDED** - Keeps index fresh |
| **Every Commit** | `index_file(path)` | ⚠️ Too infrequent - stale between edits |
| **Every Task** | `index_folder(path, incremental=true)` | ❌ Too slow - full re-index |

**Best Practice**: Call `register_edit()` after EVERY file modification
```python
# After editing src/V12_002.cs
use_mcp_tool(
    server_name="jcodemunch-mcp",
    tool_name="register_edit",
    arguments={
        "repo": "universal-or-strategy",
        "file_paths": ["src/V12_002.cs"],
        "reindex": true
    }
)
```

**Why Every Edit?**
- jcodemunch caches search results
- Editing a file invalidates cached results
- `register_edit()` clears cache for that file
- Next search will use fresh data

### Graphify Knowledge Graph

**Update Trigger**: After structural changes

**Frequency Options**:

| Trigger | Command | When to Use |
|---------|---------|-------------|
| **Every Task START** | `graphify update . --no-cluster --no-description` | ✅ **MANDATORY** — ~19s, AST-only |
| **Every Task END** | `graphify update . --no-cluster --no-description` | ✅ **MANDATORY** — keeps graph fresh for next agent |
| **Every Commit** | `graphify update .` | ⚠️ Full rebuild — use for wave-level review only |
| **Every PR** | `graphify update .` | ✅ Good for final wave validation |

**Best Practice (V2.0)**: Every task, fast mode
```bash
# START of every task (FIRST action)
graphify update . --no-cluster --no-description

# END of every task (LAST action, after any file edits)
graphify update . --no-cluster --no-description
```

**Why Every Task?**
- `--no-cluster --no-description` is only ~19 seconds
- All agents benefit from a fresh graph view
- Reduces 71x token cost of raw file reading
- God nodes and community structure guide every extraction decision
- Next agent inherits an up-to-date graph without needing to rebuild

## Comparison Table

| Feature | jcodemunch | graphify |
|---------|-----------|----------|
| **Speed** | Fast (real-time) | Slow (30-60s) |
| **Scope** | Symbol-level | File/module-level |
| **Use Case** | Code navigation | Architecture overview |
| **Update** | Every edit | Every epic |
| **MCP Required** | ✅ Yes | ❌ No (CLI tool) |
| **Cache** | In-memory | Static files |

## Workflow Integration

### During Epic Execution (Option 1)

```
1. Worker claims epic
2. Worker executes phases 1-6
3. Phase 6 (epic-validate) modifies source files
4. ✅ Call register_edit() for each modified file
5. Worker releases epic
6. ✅ Call graphify update . after epic completes
```

### During Parallel Execution (Option 3)

```
Worker 1:
  - Edits src/V12_002.SIMA.cs
  - Calls register_edit(["src/V12_002.SIMA.cs"])
  
Worker 2:
  - Edits src/V12_002.Orders.cs
  - Calls register_edit(["src/V12_002.Orders.cs"])
  
After all 4 workers complete:
  - Run graphify update . once
  - Captures all structural changes
```

## Critical Thinking MCP

**Status**: Not found in current configuration
**Question**: Was this a custom MCP or a third-party tool?

**If custom**: Need to locate the server script
**If third-party**: Need installation instructions

**Action Required**: User to clarify what "critical thinking" MCP was

## Summary

**jcodemunch**:
- ✅ Needs MCP
- ✅ Update every edit via `register_edit()`
- ✅ Fast, real-time queries

**graphify**:
- ❌ No MCP needed (CLI tool)
- ✅ Update every epic via `graphify update .`
- ⚠️ Slow, static output

**greptile**:
- ✅ Preserved in config
- ✅ Cloud-based (no local updates needed)

**worker agents**:
- ✅ Added to config
- ✅ Ready for Option 1 execution