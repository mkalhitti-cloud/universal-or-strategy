# MISSION BRIEF: $prreport - Comprehensive Audit & Arena Triage

**Objective:**
Generate a non-prescriptive, objective report aggregating all audit results from GitHub Actions, PR audit bots, GitHub apps, and the 6 local Arena AI zip folders.

**Instructions:**
1. **Locate & Parse Zip Files**: Find the 6 downloaded Arena AI zip files in the workspace (each contains a battle between two models). You must read their contents *without* unzipping them permanently to the filesystem (to avoid repo bloat). Extract and list all audit findings from the models.
2. **Gather GitHub Audits**: Use the `gh` CLI or your internal tools to pull all audit results, comments, and CI/CD findings from the active GitHub PR, Actions, and integrated bots.
3. **Validate Authenticity (Zero Hallucination)**: 
   - Verify that all bots and Arena models audited the *correct* code.
   - Confirm they performed the *intended* audit.
   - Explicitly cross-reference findings against the actual codebase and discard any hallucinated results.
4. **Triage & Categorize**: Create a strict distinction in your report:
   - **Src-Code Repairs**: Findings that require modifying `src/` files. (These will be routed to the ENGINEER and ARCHITECT).
   - **Non-Src Repairs**: Findings that do *not* involve `src/` code (e.g., docs, workflows, CI/CD config). These will be handled via `/handoff_gemini`.
5. **Subagent Verification**: Once your draft report is complete, you MUST spawn a subagent to rigorously review your work for accuracy, hallucination checks, and protocol compliance. If spawning a subagent is not possible in your environment, you must perform a distinct, documented self-review pass.
6. **Output**: Write the final, verified report to `docs/brain/prreport_audit_results.md`.

**Constraints:**
- Do not make any `src/` code changes. This is a read-only audit synthesis.
- Maintain strict V12 DNA constraints (no `lock()` statements, ASCII compliance only).
- Keep the workspace clean (do not leave extracted zip contents behind).
