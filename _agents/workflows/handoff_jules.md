---
description: Handoff to Jules using the pipe prompt method (Traycer style)
---

Use this workflow to delegate surgical code edits or refactoring tasks to the Jules agent, utilizing a piped prompt technique to bypass standard command-line argument limits.

---

## Phase 1: Prepare the Autonomous Mission Brief

1. **Define the Mission**: Outline the specific code implementation or refactoring objective.
2. **Write the Prompt File**: Save the mission instructions into a dedicated markdown file (e.g., `docs/brain/jules_mission_brief.md`).
   - Keep it strictly instructional for headless execution.
   - Instruct Jules exactly what files to edit and what the constraints are.
   - Include any relevant forensic or architectural context required for the task.

---

## Phase 2: Execute the Pipe Prompt

To execute the Jules CLI using the Traycer-style pipe prompt method in PowerShell, read the prompt file and pass it to the `jules` command.

```powershell
# // turbo
$promptContent = Get-Content docs/brain/jules_mission_brief.md -Raw
jules "$promptContent"
```

*(Note: Verify the exact argument flag required for Jules in your environment, similar to Codex or Gemini.)*

---

## Phase 3: Monitor and Verify

1. Await the completion of the `jules` subprocess.
2. Verify that Jules successfully executed the edits in `src/`.
3. Report the handoff success back to the Director (User).

---

## Phase 4: Mandatory Self-Improvement Audit (NON-NEGOTIABLE)

After EVERY use of this workflow, the executing agent MUST perform a post-use audit:

**If no gap found, state:** `workflow(handoff_jules): no gaps identified -- workflow correct as written.`
**Commit format:** `workflow(handoff_jules): [what was fixed and why]`
