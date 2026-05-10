---
description: Handoff to Rovo Dev CLI using the pipe prompt method (Traycer style)
---

Use this workflow to delegate tasks to the Rovo Dev CLI, utilizing a piped prompt technique to bypass standard command-line argument limits. Rovo Dev acts as a backup engineer with access to Anthropic and OpenAI models.

---

## Phase 1: Prepare the Autonomous Mission Brief

1. **Define the Mission**: Outline the specific coding, refactoring, or documentation objective.
2. **Write the Prompt File**: Save the mission instructions into a dedicated markdown file (e.g., `docs/brain/rovo_mission_brief.md`).
   - Keep it strictly instructional for headless execution.
   - Instruct Rovo Dev on the exact parameters, files to edit, and success criteria.

---

## Phase 2: Execute the Pipe Prompt

To execute the Rovo Dev CLI using the Traycer-style pipe prompt method in PowerShell, read the prompt file and pass it to the `rovodev` (or your local Rovo Dev binary name) command.

```powershell
# // turbo
$promptContent = Get-Content docs/brain/rovo_mission_brief.md -Raw
acli rovodev "$promptContent"
```

*(Note: Verify the exact argument flag required for Rovo Dev in your environment, similar to Codex or Gemini.)*

---

## Phase 3: Monitor and Verify

1. Await the completion of the Rovo Dev subprocess.
2. Verify that Rovo Dev successfully executed the requested edits.
3. Report the handoff success back to the Director (User).

---

## Phase 4: Mandatory Self-Improvement Audit (NON-NEGOTIABLE)

After EVERY use of this workflow, the executing agent MUST perform a post-use audit:

**If no gap found, state:** `workflow(handoff_rovo): no gaps identified -- workflow correct as written.`
**Commit format:** `workflow(handoff_rovo): [what was fixed and why]`
