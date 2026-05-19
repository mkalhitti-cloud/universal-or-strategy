# Jules Tools CLI Reference Guide

This document captures the official terminal command surface and utility specifications for **Jules Tools (CLI)** operating within the **V12 Universal OR Strategy** repository.

The CLI allows you to trigger remote coding sessions, list repositories, inspect live session diffs, and pull code changes directly from your terminal.

---

## 💾 Installation & Setup

To install or update the Jules Tools CLI globally:

```bash
# Global installation via npm
npm install -g @google/jules
```

Once installed, the `jules` binary is available globally.

### 🔑 Authentication

You must authenticate with your Google account before executing session commands.

```bash
# Login (opens authentication browser flow)
jules login

# Logout (clears local credentials)
jules logout
```

---

## 🛠️ CLI Syntax & Commands

All commands are structured under the base `jules` executable. Use `-h` or `--help` on any command for detailed flag structures.

### 🚩 Global Flags
- `-h`, `--help`: View help guidelines for a command.
- `--theme <dark|light>`: Set the TUI theme color palette (default: `dark`).

### 📦 Command Reference Table

| Command | Action | Sub-Flags |
| :--- | :--- | :--- |
| `jules version` | Display the installed CLI binary version. | None |
| `jules remote list` | List active sessions or connected codebases. | `--repo`, `--session` |
| `jules remote new` | Launch a new cloud coding agent session. | `--repo <name>`, `--session "<prompt>"`, `--parallel <num>` |
| `jules remote pull` | Retrieve code changes from a completed session. | `--session <id>` |
| `jules completion` | Generate shell tab-completion scripts. | `bash`, `zsh` |

---

## ⚡ Examples & Usage Patterns

### 1. Repository & Session Audits
```bash
# List all codebases authorized under the Jules GitHub App
jules remote list --repo

# List all past and active agent sessions
jules remote list --session
```

### 2. Spawning Autonomous Sessions
Jules automatically detects the repository if executed from within the project directory (e.g. `c:\WSGTA\universal-or-strategy\`).
```bash
# Start a session to audit expected position structures in this repository
jules remote new --session "Audit expectedPositions and activePositions dict key alignments"

# Start a parallel session (e.g., to run multiple Red-Team analysis sweeps)
jules remote new --repo mkalhitti-cloud/universal-or-strategy --session "Audit CIT_Repair for allocations" --parallel 3
```

### 3. Pulling Completed Code Changes
```bash
# Pull changes from completed session ID 123456
jules remote pull --session 123456
```

---

## 🖥️ Terminal User Interface (TUI) Dashboard

For an interactive console experience that mirrors the web dashboard (including real-time logs, side-by-side diff viewers, and prompt setup wizards), run the base command with no arguments:

```bash
jules
```

- **Exit TUI**: Press `Ctrl+C` or follow console menu options.
- **Warp Integration**: Runs natively inside Warp and other modern, GPU-accelerated terminal environments.
