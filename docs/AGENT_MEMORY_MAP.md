# Agent Memory Map

## 🧠 Persistent Storage Locations

| Agent           | Home Folder (.agent/agents/) | Root Memory File |
| :-------------- | :--------------------------- | :--------------- |
| **Antigravity** | `antigravity/`               | `GEMINI.md`      |
| **Claude**      | `claude/`                    | `CLAUDE.md`      |
| **Codex**       | `codex/`                     | `CODEX.md`       |
| **Cursor**      | `cursor/`                    | `.cursorrules`   |

## 🔄 Handoff Protocol

- **Start Session**: Agent reads their `IDENTITY.md` and `MEMORY.md`.
- **DURING Session**: Agent records critical discoveries or build IDs.
- **END Session**: Agent appends a summary to their `MEMORY.md` under a dated header.

## 🏗️ Fresh Clone Bootstrap

1. Run `scripts/verify-agent-parity.ps1`.
2. Copy `.gemini/settings.template.json` to `settings.json` (ignored).
3. Set `TESTSPRITE_API_KEY` env var.
4. User pastes `ONBOARDING.md` prompt into their preferred agent.
