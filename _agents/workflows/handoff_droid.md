---
description: Handoff to Droid CLI using the pipe prompt method (Traycer style)
---

Use this workflow to delegate sovereign audits or readiness report generation to the Droid CLI worker, utilizing a piped prompt technique to bypass standard command-line argument limits.

---

## Phase 1: Prepare the Autonomous Mission Brief

1. **Define the Mission**: Outline the specific auditing or reporting objective (e.g., Sovereign Audit or Readiness Check).
2. **Write the Prompt File**: Save the mission instructions into a dedicated markdown file (e.g., `docs/brain/droid_mission_brief.md`).
   - Keep it strictly instructional for headless execution.
   - Instruct Droid on the exact parameters to evaluate (e.g., P0-P3 severity findings).

---

## Phase 2: Execute the Pipe Prompt

To execute the Droid CLI using the Traycer-style pipe prompt method in PowerShell, read the prompt file and pass it to the `droid` command.

```powershell
# // turbo
$promptContent = Get-Content docs/brain/droid_mission_brief.md -Raw
droid "$promptContent"
```

*(Note: Verify the exact argument flag required for Droid in your environment, similar to Codex or Gemini.)*

---

## Phase 3: Monitor and Verify

1. Await the completion of the `droid` subprocess.
2. Verify that Droid successfully executed the audit or reporting task.
3. Report the handoff success back to the Director (User).

---

## Phase 4: Mandatory Self-Improvement Audit (NON-NEGOTIABLE)

After EVERY use of this workflow, the executing agent MUST perform a post-use audit:

**If no gap found, state:** `workflow(handoff_droid): no gaps identified -- workflow correct as written.`
**Commit format:** `workflow(handoff_droid): [what was fixed and why]`
