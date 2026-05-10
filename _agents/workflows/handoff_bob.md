---
description: Handoff to Bob CLI using the pipe prompt method (Traycer style)
---

Use this workflow to delegate infrastructure, refactoring, or CLI scaffolding tasks to the Bob CLI worker, utilizing a piped prompt technique to bypass standard command-line argument limits.

---

## Phase 1: Prepare the Autonomous Mission Brief

1. **Define the Mission**: Outline the specific infrastructure or scaffolding objective.
2. **Write the Prompt File**: Save the mission instructions into a dedicated markdown file (e.g., `docs/brain/bob_mission_brief.md`).
   - Keep it strictly instructional for headless execution.
   - Instruct Bob to use its own tools to perform the file scaffolding or configuration edits.
   - Explicitly instruct Bob on any required verification steps post-execution.

---

## Phase 2: Execute the Pipe Prompt

To execute the Bob CLI using the Traycer-style pipe prompt method in PowerShell, read the prompt file and pass it to the `bob` command.

```powershell
# // turbo
$promptContent = Get-Content docs/brain/bob_mission_brief.md -Raw
bob "$promptContent"
```

*(Note: Verify the exact argument flag required for Bob in your environment, similar to Codex or Gemini.)*

---

## Phase 3: Monitor and Verify

1. Await the completion of the `bob` subprocess.
2. Verify that Bob successfully executed the infrastructure edits.
3. Report the handoff success back to the Director (User).

---

## Phase 4: Mandatory Self-Improvement Audit (NON-NEGOTIABLE)

After EVERY use of this workflow, the executing agent MUST perform a post-use audit:

**If no gap found, state:** `workflow(handoff_bob): no gaps identified -- workflow correct as written.`
**Commit format:** `workflow(handoff_bob): [what was fixed and why]`
