# $prreport - Comprehensive Audit & Arena Triage Protocol

Use this workflow to trigger the `$prreport` high-stakes protocol. This delegates a comprehensive PR audit report to the Gemini CLI (BACKUP ORCHESTRATOR) using the pipe prompt technique. The objective is to non-prescriptively aggregate findings from GitHub PR audit bots, Actions, Apps, and Arena AI zip files, validate their authenticity (zero hallucination), and strategically triage the repairs.

---

## Phase 1: Prepare the Autonomous Mission Brief

1. **Write the Prompt File**: Save the mission instructions into `docs/brain/prreport_mission_brief.md`.
   Ensure the prompt includes the following explicit mandates for the Gemini CLI:
   - **Objective**: Generate a non-prescriptive, objective report aggregating all audit results from GitHub Actions, PR audit bots, GitHub apps, and local Arena AI zip folders.
   - **Zip File Processing**: Instruct the agent to locate the 6 Arena AI zip files (each containing a battle between two models). The agent must read their contents *without* saving unzipped contents permanently to avoid bloat, and parse the audit findings.
   - **Validation Gate**: Validate that all bots audited the correct code, performed the intended audit, and did *not* hallucinate any findings.
   - **Triage Matrix**: Clearly distinguish between repairs that require `src/` code changes (requiring the ENGINEER and ARCHITECT) and repairs that do NOT involve `src/` code (which can be handled directly via `/handoff_gemini`).
   - **Subagent Review**: Instruct the Gemini CLI to spawn a subagent to rigorously review its own generated report. If spawning a subagent fails, it must perform a distinct, rigorous self-review pass.
   - **Output Location**: Write the final comprehensive report to `docs/brain/prreport_audit_results.md`.

---

## Phase 2: Execute the Pipe Prompt

To execute the `$prreport` workflow using the Traycer-style pipe prompt method, spawn a visible terminal:

```powershell
# // turbo
Start-Process powershell -ArgumentList "-NoExit -File .\scripts\run_gemini_brief.ps1"
```

*(Note: Ensure the Arena zip files are present and their location is referenced in the prompt before execution).*

---

## Phase 3: Monitor, Review, and Route

1. Await the completion of the `gemini` subprocess.
2. Review the resulting `docs/brain/prreport_audit_results.md`.
3. For non-`src/` tasks identified in the report, trigger `/handoff_gemini` directly to execute the fixes.
4. For `src/` tasks, hand off the forensic findings to the ARCHITECT via `/architect_intake` or `$claudecloud` for structural design.

---

## Phase 4: Mandatory Self-Improvement Audit (NON-NEGOTIABLE)

After EVERY use of this workflow, the executing agent MUST perform a post-use audit:

1. **Did the agent correctly read the zip files without causing repository bloat?**
2. **Did the verification subagent successfully catch and eliminate hallucinations?**
3. **Was the triage between `src/` and non-`src/` accurate?**

**If no gap found, state:** `workflow($prreport): no gaps identified -- workflow correct as written.`

Skipping the audit is a protocol violation. No Director approval needed for self-improvement edits.

**Commit format:** `workflow($prreport): [what was fixed and why]`
