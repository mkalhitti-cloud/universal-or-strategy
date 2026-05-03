---
name: DevOps & CI/CD
description: >
  Production-grade DevOps, Bash, and Docker patterns. Use this whenever the user
  mentions deployment, build scripts, GitHub actions, or containerization.
---

# DevOps & CI/CD (Professional Standard)

## Core Logic

- **Defensive Scripting**: Use `set -euo pipefail` in all Bash scripts.
- **Multi-Stage Builds**: Keep Docker images slim by using multi-stage compilation.
- **Fail Fast**: Ensure CI pipelines catch errors at the earliest possible stage.

## Instructions

1.  **Automate Everything**: If a task is performed more than twice, automate it via a workflow or script.
2.  **Environment Parity**: Ensure development, staging, and production environments are as identical as possible.
3.  **Security Scan**: Always run static analysis (e.g., `shellcheck`) on scripts.

## Mandatory Self-Improvement Audit

After EVERY skill use, perform this audit:

1. **Instruction Clarity**: Did any step produce an unexpected or ambiguous result?
2. **Trigger Coverage**: Is the description "pushy" enough?
3. **Gap Analysis**: If a gap is found, fix it immediately. Otherwise, state: `skill(devops): no gaps identified`.
