---
description: Handoff to Gemini CLI using the pipe prompt method (Traycer style)
---

Use this workflow to delegate a task or handoff execution to the Gemini CLI (BACKUP ORCHESTRATOR), utilizing a piped prompt technique to bypass standard command-line argument limits and ensure context-rich handoffs.

---

## Phase 1: Prepare the Handoff Brief

1. **Synthesize Context**: Gather all relevant forensic findings, logical proofs, architectural constraints, and task requirements.
2. **Write the Prompt File**: Save the complete prompt into a dedicated markdown file (e.g., `docs/brain/gemini_handoff_brief.md`).
   - Keep it strictly instructional for headless execution.
   - Restate specific V12 DNA constraints (e.g., "No lock() statements", "ASCII compliance only").
   - Explicitly instruct Gemini on the expected output artifact (e.g., "Write the results to `docs/brain/gemini_analysis.md`").

---

## Phase 2: Execute the Pipe Prompt

To execute the Gemini CLI using the Traycer-style pipe prompt method in PowerShell, read the prompt file and pipe it into the `gemini` command.

```powershell
# // turbo
$promptContent = Get-Content docs/brain/gemini_handoff_brief.md -Raw
gemini -p $promptContent
```

*(Note: The global protocol requires `gemini -p "<prompt>"`. By reading it into a variable and passing it as an argument, we avoid shell escaping issues and length limits that occur with raw inline strings.)*

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

**If no gap found, state:** `workflow(gemini_pipe): no gaps identified -- workflow correct as written.`

Skipping the audit is a protocol violation. No Director approval needed for self-improvement edits.

**Commit format:** `workflow(gemini_pipe): [what was fixed and why]`
