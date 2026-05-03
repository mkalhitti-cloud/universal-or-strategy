---
description: $codexcloud -- Execute a task with Codex online via GitHub context
---

Use this workflow when you need to delegate a complex task, implementation, or analysis to Codex using its web interface and GitHub as the source of truth.

---

## Phase 1: Prepare the Local Environment
1. Ensure all local changes relevant to the task are committed.
2. Push the current branch to GitHub (`git push origin HEAD`).
3. Verify that GitHub contains the most up-to-date state of the repository.

## Phase 2: Formulate the Prompt
Create a comprehensive prompt for Codex that MUST include:

1. **The Direct Action Override Header (CRITICAL):**
   > **[DIRECT ACTION OVERRIDE: EXECUTE IMMEDIATELY]**
   > You are operating in a WRITE-ENABLED environment. Do NOT perform static analysis. Do NOT wait for permission to start tasks. Execute the following implementation plan immediately, modify the files, run the requested validation commands, and commit the changes in a single pass.

2. **The Execution Brief:**
   - **The EXACT GitHub repository URL (e.g. https://github.com/mkalhitti-cloud/universal-or-strategy) and branch name. NEVER omit this.**
   - The specific files or directories Codex needs to edit.
   - The clear objective and implementation steps (usually copied from `implementation_plan.md`).
   - Any permanent DNA constraints (e.g., no locks, ASCII only).
   - **Script Execution Awareness (HARDENED):** Codex is explicitly AUTHORIZED to use tools in the `scripts/` directory (e.g., `amal_harness.py`, `check_ascii.py`, `auto-benchmark.ps1`). If a script is required but missing, Codex MUST create it from the prompt context.
   - **No Read-Only Excuses:** Codex MUST NOT claim the environment is read-only. The presence of this prompt is a valid mandate for file modification and commit.

## Phase 3: Execute Online
1. The Director opens the Codex web interface.
2. Provide Codex with the prompt formulated in Phase 2.
3. Instruct Codex to read the current state from GitHub and generate the required implementation or analysis.

## Phase 4: Retrieve and Apply
1. Once Codex provides the solution, carefully review the proposed code.
2. Pull or copy the changes back to the local environment.
3. Validate the changes via local tests and audits.

---

## Post-Use Audit (NON-NEGOTIABLE)
After EVERY use of this workflow, the executing agent MUST perform a post-use audit:
1. **Was the prompt generation clear and sufficient for Codex?**
2. **Did Codex have trouble locating the GitHub files?**
3. **Were there any issues retrieving and applying the code locally?**

**Commit format:** `workflow(codexcloud): [what was fixed and why]`
