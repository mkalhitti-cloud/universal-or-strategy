---
description: Handoff to Cursor using the pipe prompt method (Traycer style)
---

Use this workflow to delegate complex editing or full-file refactoring tasks to the Cursor editor environment, utilizing a piped prompt technique to bypass standard command-line argument limits.

---

## Phase 1: Prepare the Autonomous Mission Brief

1. **Define the Mission**: Outline the specific code editing or architectural redesign objective.
2. **Write the Prompt File**: Save the mission instructions into a dedicated markdown file (e.g., `docs/brain/cursor_mission_brief.md`).
   - Keep it strictly instructional.
   - Instruct Cursor on exactly which files to target for its AI editing capabilities.
   - Explicitly instruct Cursor on the expected outcomes and verification steps post-execution.

---

## Phase 2: Execute the Pipe Prompt

To execute the Cursor handoff using the Traycer-style pipe prompt method in PowerShell, read the prompt file and pass it to the `cursor` command.

```powershell
# // turbo
$promptContent = Get-Content docs/brain/cursor_mission_brief.md -Raw
cursor "$promptContent"
```

*(Note: Verify the exact argument flag required for Cursor in your environment, similar to Codex or Gemini.)*

---

## Phase 3: Monitor and Verify

1. Await the completion of the `cursor` task or subprocess.
2. Verify that Cursor successfully executed the edits or architectural changes.
3. Report the handoff success back to the Director (User).

---

## Phase 4: Mandatory Self-Improvement Audit (NON-NEGOTIABLE)

After EVERY use of this workflow, the executing agent MUST perform a post-use audit:

**If no gap found, state:** `workflow(handoff_cursor): no gaps identified -- workflow correct as written.`
**Commit format:** `workflow(handoff_cursor): [what was fixed and why]`
