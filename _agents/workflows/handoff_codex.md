---
description: Handoff to Codex CLI using the pipe prompt method (Traycer style)
---

Use this workflow to delegate a surgical implementation or forensic audit task to the Codex CLI (ENGINEER/FORENSICS), utilizing a piped prompt technique to bypass standard command-line argument limits.

---

## Phase 1: Prepare the Autonomous Mission Brief

1. **Define the Mission**: Outline the specific surgical implementation or adversarial audit objective.
2. **Write the Prompt File**: Save the mission instructions into a dedicated markdown file (e.g., `docs/brain/codex_mission_brief.md`).
   - Keep it strictly instructional for headless execution.
   - Instruct Codex to use its own tools to perform the file edits or forensic grep searches.
   - Include the specific V12 DNA constraints (e.g., "No lock() statements", "ASCII compliance only", "Must run deploy-sync.ps1 post-edit").

---

## Phase 2: Execute the Pipe Prompt

To execute the Codex CLI using the Traycer-style pipe prompt method in PowerShell, read the prompt file and pass it to the `codex` command.

```powershell
# // turbo
$promptContent = Get-Content docs/brain/codex_mission_brief.md -Raw
codex "$promptContent"
```

*(Note: Verify the exact argument flag required for Codex in your environment, typically it accepts the prompt as the first positional argument or via an environment variable like `$env:TRAYCER_PROMPT`.)*

---

## Phase 3: Monitor and Verify

1. Await the completion of the `codex` subprocess.
2. Verify that Codex successfully executed the surgical edits or forensic audits.
3. Report the handoff success back to the Director (User).

---

## Phase 4: Mandatory Self-Improvement Audit (NON-NEGOTIABLE)

After EVERY use of this workflow, the executing agent MUST perform a post-use audit:

**If no gap found, state:** `workflow(handoff_codex): no gaps identified -- workflow correct as written.`
**Commit format:** `workflow(handoff_codex): [what was fixed and why]`
