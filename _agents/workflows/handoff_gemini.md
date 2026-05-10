---
description: Handoff to Gemini CLI using the pipe prompt method (Traycer style)
---

Use this workflow to delegate a task or handoff execution to the Gemini CLI (BACKUP ORCHESTRATOR), utilizing a piped prompt technique to bypass standard command-line argument limits and ensure context-rich handoffs.

---

## Phase 1: Prepare the Autonomous Mission Brief

1. **Define the Mission**: Outline the high-level objective (e.g., "Check the PR audit status, gather the findings from GitHub Actions, and generate a final report"). You do NOT need to gather the data yourself.
2. **Write the Prompt File**: Save the mission instructions into a dedicated markdown file (e.g., `docs/brain/gemini_mission_brief.md`).
   - Keep it strictly instructional for headless execution.
   - Instruct Gemini to use its own tools to gather necessary context (e.g., "Use the `gh` CLI to check the PR status").
   - Restate specific V12 DNA constraints (e.g., "No lock() statements", "ASCII compliance only").
   - Explicitly instruct Gemini on the expected output artifact (e.g., "Write the results to `docs/brain/gemini_analysis.md`").

---

## Phase 2: Execute the Pipe Prompt

To execute the Gemini CLI, use `Start-Process` to spawn a visible interactive terminal (just like Traycer). This prevents `node-pty` attach crashes in headless environments.

```powershell
# // turbo
Start-Process powershell -ArgumentList "-NoExit -File .\scripts\run_gemini_brief.ps1"
```

*(Note: The `run_gemini_brief.ps1` script handles reading the raw file into the `gemini -p` command.)*

---

## Phase 3: Monitor and Verify

1. Await the completion of the `gemini` subprocess.
2. Verify that Gemini generated the expected artifacts or output.
3. Report the handoff success and findings back to the Director (User).

---

## Phase 4: Mandatory Self-Improvement Audit (NON-NEGOTIABLE)

After EVERY use of this workflow, the executing agent MUST perform a post-use audit:

1. **Did the pipe execution succeed?** Adjust the PowerShell syntax if it failed due to quoting or encoding issues.
2. **Did Gemini CLI fully grasp the context?** If it missed details, refine the handoff brief structure.

**If no gap found, state:** `workflow(handoff_gemini): no gaps identified -- workflow correct as written.`

Skipping the audit is a protocol violation. No Director approval needed for self-improvement edits.

**Commit format:** `workflow(handoff_gemini): [what was fixed and why]`
