# Mode Routing Protocol (V12.18+)

## Built-In Modes (Bob IDE 2.0)

| Mode | Tool Access | Use For |
|------|------------|---------|
| **agent** | read, edit, execute, mcp, browser, subagents | All code changes, verification, shell tasks, MCP queries |
| **plan** | read, edit (markdown only), browser, mcp | Architecture planning, specs — no code edits |
| **ask** | read, browser, mcp | Analysis, Q&A — no file edits |

## Mode Routing Decision Tree

```
Is task modifying code?
├─ YES → Is task in src/?
│  ├─ YES → Use Bob CLI (v12-engineer custom mode)
│  └─ NO  → Use agent mode
└─ NO  → Use ask mode or plan mode
```

## V12 Custom Modes (Phase-Specific)

For wave execution, always use the appropriate phase mode from `.bob/custom_modes.yaml`.
Use `agent` mode for any task not covered by a custom mode.

## Enforcement Date

**Effective**: 2026-05-25 (V12.18) — Bob IDE 2.0
**Mandatory Compliance**: All agents, all sessions
