# Agent Parity Consensus — V12 Project

## 🎯 Executive Summary

This project now uses a unified **Multi-Agent Parity Architecture**. All agents (Antigravity, Cursor, Claude, Codex) share the same standards, workflows, and memory patterns.

## 🏛️ Architecture Pillars

1. **Single Source of Truth**: `.agent/` is the canonical home for all project law.
2. **Agent Home Folders**: Each agent has a dedicated `.agent/agents/{name}/` folder for IDENTITY and MEMORY.
3. **FSM Order Management**: All agents follow the `MOVE-SYNC` / `FollowerReplaceState` FSM for NinjaTrader orders.
4. **ASCII Safety**: Hard rule against non-ASCII characters in C# strings (prevents Build 936 compiler crashes).

## 🛡️ Risk Mitigation

| Risk            | Mitigation                                                            | Status       |
| :-------------- | :-------------------------------------------------------------------- | :----------- |
| **Secret Leak** | `.gemini/settings.json` ignored; `validate-secrets.ps1` script added. | ✅ COMPLETED |
| **Agent drift** | Shared `.agent/skills/` and mirroring scripts.                        | ✅ COMPLETED |
| **Logic Races** | Mandatory Codex forensic triage protocol.                             | ✅ COMPLETED |

## 🔗 Context7 & MCP

- **Active in**: Antigravity, Claude, Cursor.
- **Provider**: Upstash Context7 (Live C# / NinjaTrader docs lookup).
- **Parity**: All agents now have access to live documentation via MCP server `context7`.
