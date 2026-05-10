# MISSION BRIEF: Non-Src Repair Execution

**Objective:**
Execute the 13 Non-Src repairs identified in the recent PR audit report. These repairs strictly involve workflows, CI/CD configs, and documentation. You are BANNED from modifying `src/` files.

**Context:**
The full list of repairs is documented under the "Non-Src Repairs" section of `docs/brain/prreport_audit_results.md`. 

**Instructions:**
1. **Read the Audit Report**: Review `docs/brain/prreport_audit_results.md` to get the list of 13 non-src findings.
2. **Execute Repairs**: Use your tools to surgical edit the affected files:
   - Fix prompt/command injections and YAML errors in `.github/workflows/jules-pr-review.yml`.
   - Redact or gitignore the PII in `artifacts/rdp_ocr_utf8.txt`.
   - Fix the `always()`/`success() || failure()` condition in `.github/workflows/sonarcloud.yml`.
   - Re-add `--no-git` to `.github/workflows/gitleaks.yml`.
   - Move `auto_review` to `[github_action_config]` in `.pr_agent.toml`.
   - Correct the split script path in `.github/workflows/gemini-pr-audit.yml`.
   - Pin `pr-agent.yml` and `dependency-review-action` to a commit SHA.
   - Clean up conflicting Phase 5 instructions in `docs/brain/implementation_plan.md`.
   - Fix broken/truncated `grep` verifications in the `Traycerrefactor/*` markdown files.
   - Correct P4/P5 Engineer/Architect role labels in `docs/brain/V12_Workflow_Manifesto.md`.
   - Clean up non-ASCII characters in `.bob/rules-v12-engineer/dna.md` & `AGENTS.md`.
   - Fix the merged `$PLAN_AUDIT` bullet in `CODEX.md`.
3. **Verify Compliance**: Ensure no `src/` code was modified. 
4. **Final Report**: Write a brief completion summary to `docs/brain/non_src_repairs_completed.md`.

**Constraints:**
- Do not make any `src/` code changes. 
- Maintain strict V12 DNA constraints (ASCII compliance only).
- You are executing as the BACKUP ORCHESTRATOR. No Director approval is needed for these non-src edits.
