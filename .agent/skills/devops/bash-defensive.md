---
name: Bash Defensive Patterns
description: >
  Professional, production-ready shell scripting guidelines.
  Ensures all scripts are fail-safe and predictable.
---

# Bash Defensive Patterns (Professional Standard)

## Core Logic

- **Bail on Error**: Always use `set -euo pipefail`.
- **Variable Safety**: Quote ALL variables to prevent globbing and word splitting (`"$VAR"`).
- **Static Analysis**: Use `shellcheck` before every commit.

## Instructions

1.  **Function Isolation**: Wrap logic in main() and use local variables.
2.  **Explicit Paths**: Use absolute paths or define a `ROOT_DIR` at the start.
3.  **Audit Exit Codes**: Use `trap` to catch signals and cleanup temporary files.

## Mandatory Self-Improvement Audit

After EVERY skill use, perform this audit:

1. **Instruction Clarity**: Did any step produce an unexpected or ambiguous result?
2. **Trigger Coverage**: Is the description "pushy" enough?
3. **Gap Analysis**: If a gap is found, fix it immediately. Otherwise, state: `skill(bash-defensive): no gaps identified`.
