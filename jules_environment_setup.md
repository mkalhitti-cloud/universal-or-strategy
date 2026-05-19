# Jules Environment Setup Guide

This document captures the official configuration and environment capability standards for **Jules CLI** (Backup Engineer #2) operating within the **V12 Universal OR Strategy** repository. 

Jules runs each task inside a secure, short-lived virtual machine (VM) powered by Ubuntu Linux.

---

## 🛠️ Preinstalled Tools & Runtimes

The Jules VM includes the following developer tools preinstalled:

### 🐍 Python (System default: 3.12.11)
- **python3**: `Python 3.12.11`
- **pip**: `pip 25.1.1`
- **poetry**: `Poetry 2.1.3`
- **uv**: `uv 0.7.13`
- **pytest**: `pytest 8.4.0`
- **ruff**: `ruff 0.12.0`
- **pyenv**: `3.10.18`, `3.12.11`

### 🟢 Node.js (System default: v22.16.0)
- **node**: `v22.16.0` (with `v18.20.8` and `v20.19.2` available via nvm)
- **npm**: `11.4.2`
- **pnpm**: `10.12.1`
- **yarn**: `1.22.22`
- **prettier**: `3.5.3`

### ☕ Java
- **java**: `openjdk 21.0.7`
- **maven**: `3.9.10`
- **gradle**: `8.8`

### 🐹 Go
- **go**: `go 1.24.3 linux/amd64`

### 🦀 Rust
- **rustc / cargo**: `1.87.0 (2025-05-09)`

### 🐳 Docker & Compilers
- **docker**: `28.2.2`
- **docker-compose**: `v2.36.2`
- **gcc / clang**: `gcc 13.3.0` / `clang 18.1.3`
- **cmake / ninja**: `cmake 3.28.3` / `ninja 1.11.1`

### 🔍 Other Utilities
- `ripgrep (rg)`: `14.1.0`
- `jq`: `jq-1.7`
- `tmux`: `3.4`
- `git`: `2.49.0`
- `curl`, `make`, `grep`, `sed`, `awk`, `tar`, `yq`

---

## ⚙️ Initial Setup Script (Jules VM Configuration)

To help Jules configure dependencies and run the validation scripts for the repository:

1. Click on the codebase in the left sidebar under **codebases**.
2. Select **Configuration** at the top.
3. In the **Initial Setup** input, enter the following commands to install Node.js and Python test dependencies:
   ```bash
   npm install
   # (Optional) Verify system versions during validation
   node -v
   python3 -c "import sys; print(sys.version)"
   ```
4. Click **Run and Snapshot** to validate the script and create a persistent environment snapshot for all future Jules tasks.

---

## 🎯 Verification & Best Practices
- **Validation**: Check installed tool versions by running:
  ```bash
  set +x; . /opt/environment_summary.sh
  ```
- **Lightweight Setup**: Keep the initial setup script minimal and fast to ensure Jules VMs spin up rapidly without wasting execution tokens or caching windows.
- **DNA Integrity**: All custom testing and validation scripts run in the Jules VM must maintain full V12 DNA compliance (atomic, lock-free, and strict ASCII string limits).
