# Greptile Integration Manual (V12.16)

## 1. Overview
Greptile is an AI-powered codebase search and review engine that indexes entire repositories to provide deep context. In our V12/Morpheus architecture, Greptile acts as the **Primary Tester** and **Global Context Engine**, bridging the gap between local surgical edits (Codex/Bob) and system-wide integrity.

## 2. Pillar 1: MCP Server Configuration
The Model Context Protocol (MCP) server allows our agents (Gemini CLI, Cursor) to interact with Greptile directly.

### A. Cursor IDE / VS Code
1. Open **Settings > General > MCP**.
2. Click **+ Add New MCP Server**.
3. **Name**: `greptile`
4. **Type**: `command`
5. **Command**:
   ```bash
   npx -y greptile-mcp-server --api-key=%GREPTILE_API_KEY% --github-token=%GITHUB_TOKEN%
   ```

### B. Claude Desktop / Orchestrator Config
Add to `claude_desktop_config.json`:
```json
{
  "mcpServers": {
    "greptile": {
      "command": "npx",
      "args": ["-y", "greptile-mcp-server"],
      "env": {
        "GREPTILE_API_KEY": "your-api-key",
        "GITHUB_TOKEN": "your-github-token"
      }
    }
  }
}
```

## 3. Pillar 2: Tool Reference
| Tool | Description | Use Case |
| :--- | :--- | :--- |
| `index_repository` | Starts/updates indexing of a repo. | Run after major refactors. |
| `query_repository` | Natural language Q&A with context. | "How does the FSM handle order rejection?" |
| `search_repository` | Semantic and regex search. | Finding all usages of a specific atomic pattern. |
| `get_repository_info` | Checks indexing status/metadata. | Verify if the latest commit is indexed. |

## 4. Pillar 3: /pr-loop Workflow
The autonomous PR loop (`greploop`) ensures high-confidence delivery.

### The Loop Logic:
1. **Trigger**: Agent pushes code and runs `/greploop <PR_NUMBER>`.
2. **Scan**: Greptile performs a full-repository review.
3. **Analyze**: Agent parses **Confidence Score (0-5)** and inline comments.
4. **Iterate**: 
   - If Score < 5: Agent fixes code surgical based on feedback.
   - If Score == 5: PR is promoted to Human Review.
5. **Exit**: Loop breaks when 5/5 score is reached with zero critical findings.

## 5. Pillar 4: Context Patterns (Team Standards)
We feed Greptile our V12 standards to ensure it reviews code "our way."

### Configuration Hierarchy
1. **`.greptile/config.json`**: Directory-level overrides (Cascading).
2. **`greptile.json`**: Repository-level global instructions.
3. **Dashboard**: Organization-wide rules and "Learned Rules" from PR feedback.

### Sample `greptile.json`
```json
{
  "instructions": "Enforce NinjaScript V12 Project Standards. BANNED: lock(stateLock), Unicode in strings, non-FSM order submission. MANDATORY: ASCII Gate, Enqueue model for all state mutations.",
  "rules": [
    {
      "id": "v12-fsm-only",
      "rule": "Follower order replacements must use the two-phase Replace FSM.",
      "severity": "critical"
    }
  ]
}
```

## 6. Greptile as "Primary Tester" (The 29th Bot)
Greptile is promoted to the 29th bot in our fleet, specializing in **Semantic Regression Testing**.

### Strategic Responsibilities:
- **Cross-Module Sentinel**: Unlike unit tests, Greptile identifies when a change in `Module A` subtly breaks assumptions in `Module B`.
- **Architectural Guardrail**: Enforces the "BANNED from src/ edits" rule for Antigravity and ensures only Bob/Codex modify logic.
- **Pre-Human Vetting**: Reduces "review fatigue" by mopping up style nits and obvious logic flaws autonomously before a human developer is pinged.
- **Allocation Auditor**: In conjunction with AMAL, Greptile flags potential GC pressure in hot-paths by analyzing allocation patterns across the call stack.

## 7. Operational Command Summary
- `greptile query "..."`: Quick architectural lookup.
- `/greploop`: Start the autonomous PR fix cycle.
- `graphify update . && greptile index`: Sync local graph with global AI context.
