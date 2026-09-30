# Implementation Plan: Hardening PR Hygiene & Epic Workflow Automation

## Objective
Fix the "Dirty Branch" violation that stops the PR Perfection Loop and ensure the Orchestrator (Bob) automatically initiates `/pr-loop` at the end of every Epic.

## Key Files & Context
- `scripts/verify_pr_hygiene.ps1`: The gatekeeper script for PR cleanliness.
- `.bob/commands/pr-loop.md`: The definition of the PR Perfection Loop.
- `.bob/commands/epic-run.md`: The high-level orchestration for Epics.
- `.bob/rules/00-pr-hygiene.md`: New general rules for PR hygiene.

## Proposed Changes

### 1. Automation & Script Hardening
- **`scripts/verify_pr_hygiene.ps1`**: Improve error messaging for "Dirty Branch" to provide actionable `git` commands.
- **`.bob/commands/pr-loop.md`**: Update "Step 0: Pre-Flight Hygiene" to automatically perform `git fetch origin main` and `git rebase origin/main` before verification.

### 2. Workflow Orchestration
- **`.bob/commands/epic-run.md`**:
    - Insert a mandatory **Phase 6: PR Submission & Perfection** before the final completion report.
    - Automate PR creation using the GitHub CLI (`gh pr create`).
    - Mandate the execution of `/pr-loop <PR_NUMBER>` until 100/100 PHS is achieved.
    - Move the `[EPIC-COMPLETE]` summary block to only trigger after the final PHS 100/100 is confirmed.

### 3. Behavioral Rules
- **`.bob/rules/00-pr-hygiene.md`** (New File): Establish a project-wide mandate for rebased branches and continuous hygiene.

## Verification & Testing
1. **Script Validation**: Run `powershell -File .\scripts\verify_pr_hygiene.ps1` on a branch that is behind `main` and verify the new error message is clear.
2. **Workflow Dry Run**: Review the updated `.bob/commands/` files to ensure logical flow from Epic Completion to PR Perfection.
3. **PHS Check**: Verify that `/pr-loop` logic correctly handles the rebase step without human intervention.
