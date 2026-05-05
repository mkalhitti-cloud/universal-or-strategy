# Universal OR Strategy: Project Command Center

Welcome, Director. This repository contains the hardened **Universal OR Strategy V12 (Modular)** codebase and its supporting infrastructure.

## 📁 Directory Structure

| Folder     | Purpose                                                             |
| ---------- | ------------------------------------------------------------------- |
| `src/`     | **The Alpha**: Core C# strategy and panel logic.                    |
| `bin/`     | Executables and binary tools (Auditors, CLI).                       |
| `docs/`    | Architecture maps, risk reports, audit logs, and Handoff Protocols. |
| `scripts/` | Automation tools for deployment, testing, and audits.               |

## 🧠 Shared AI Memory (The "Joint Brain")

To prevent AI "blindspots" between platforms (Claude Code, Cursor, Codex), always refer to the project-local memory in `docs/brain/`:

- **Roadmap**: [task.md](file:///C:/WSGTA/universal-or-strategy/docs/brain/task.md)
- **Current Plan**: [implementation_plan.md](file:///C:/WSGTA/universal-or-strategy/docs/brain/implementation_plan.md)
- **Audit Walkthrough**: [walkthrough.md](file:///C:/WSGTA/universal-or-strategy/docs/brain/walkthrough.md)

## 📜 Project Governance

- **Workflow DNA**: [Institutional Workflow DNA](file:///C:/WSGTA/universal-or-strategy/docs/protocol/INSTITUTIONAL_WORKFLOW_DNA.md) — The "Zero-Trust" psychology for all operations.
- **Local Terminal First**: Prioritize running agents (Codex, Claude) in the local terminal over background tasks. See [IDE_GUIDE.md](file:///C:/WSGTA/universal-or-strategy/IDE_GUIDE.md#🚀-5-Manual-Agent-Launch-Local-Terminal-First) for commands.
- **Executive Audit**: [Zero-Trust Executive Audit Protocol](file:///.agent/workflows/executive_audit.md) — Standardized protocol for high-level risk assessment.
- **Handoff Protocol**: [MASTER_HANDOFF_PROTOCOL.md](file:///C:/WSGTA/universal-or-strategy/docs/protocol/MASTER_HANDOFF_PROTOCOL.md) — Follow this for all agent transitions.

## 🚀 Key Commands

### Deployment

Synchronize the repository with your NinjaTrader 8 environment:

```powershell
./deploy-sync.ps1
```

### Auditing

Run the executive audit scan to discover logic risks:

```powershell
./scripts/audit_scan.ps1
```

### Verification

Verify the project structure and path integrity:

```powershell
./scripts/verify_reorg.ps1
```

## 🛠 Active Workflows

- **Multi-Agent Audit**: Refer to [multi_agent_audit.md](file:///.agent/workflows/multi_agent_audit.md) for coordinating the Reaper Scan.

---

_Status: Build 932 (Stabilized Final)_

