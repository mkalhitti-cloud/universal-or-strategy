---
name: github-repo-migration
description: Comprehensive workflow for migrating a GitHub repository to a new account/owner. Handles remote repointing, secret porting, and CI/CD bot verification. Use when a user needs to move their project to a new GitHub entity (e.g., to access new trials or consolidate accounts).
---

# GitHub Repo Migration Protocol

This skill governs the migration of a "Golden Master" repository state from an old GitHub account to a new one.

## 1. PRE-FLIGHT (Local IDE)

1. **Identify the "Golden Master"**: Ensure the local repository is on the `main` branch and fully merged.
2. **Rename Old Remote**: `git remote rename origin legacy`
3. **Verify Auth**:
   - **MANDATORY**: Run `gh auth logout` first to clear old tokens.
   - Run `gh auth login` to authenticate as the NEW owner.
   - Update Git identity: `git config --global user.name "<new-username>"`

## 2. REPOINTING WORKFLOW

Run the following commands to establish the new link:

1. `git remote add origin <NEW_REPO_URL>`
2. `git fetch origin`
3. `git push origin main --force` (Push the Golden Master state)
4. `git push --all origin` (Push all other branches)
5. **Hook Installation**: Run `powershell -File .\scripts\install_hooks.ps1` to re-activate V12 pre-commit and pre-push gates.

## 3. ASSET PORTING (Manual Checklist for Director)

Instruct the Director to manually migrate non-code assets:

1. **Secrets**: Copy `JULES_API_KEY`, `CODECOV_TOKEN`, `CODACY_PROJECT_TOKEN`, etc., from Old Repo -> New Repo Settings.
2. **GitHub Apps**:
   - Authorize **Jules**, **Greptile**, **CodeRabbit**, **Codacy**, and **DeepSource** on the new repo.
   - Trigger a fresh **Greptile Indexing** via its dashboard.

### Codacy Coverage Integration

After migrating to the new repository, ensure coverage tracking is configured:

1. **Add Secret**: In new repo Settings -> Secrets and variables -> Actions, add `CODACY_PROJECT_TOKEN`
   - Get token from: https://app.codacy.com/gh/<new-owner>/<repo-name>/settings/coverage
   
2. **Verify Workflow**: Ensure `.github/workflows/codacy-coverage.yml` exists in the repository
   - Workflow triggers on push to main/develop and pull requests
   - Generates coverage using Coverlet.Console
   - Uploads to Codacy using Coverage Reporter

3. **Validate Coverage Upload**:
   - Push a commit to trigger the workflow
   - Check Actions tab: https://github.com/<new-owner>/<repo-name>/actions
   - Verify "Codacy Coverage" workflow completes successfully
   - Confirm coverage appears on Codacy dashboard: https://app.codacy.com/gh/<new-owner>/<repo-name>/coverage

4. **Troubleshooting**:
   - If upload fails: Verify `CODACY_PROJECT_TOKEN` is correctly set
   - If no coverage data: Check test execution logs in workflow
   - If bash script fails: Ensure Git Bash is available (included with Git for Windows)

## 4. VERIFICATION (A2A Jules)

Delegate to **Jules** or use `gh pr checks` to verify:

1. CI/CD pipeline triggers on the new repo.
2. Status checks (SonarCloud, DeepSource) are receiving payloads.
3. PR Hygiene gate passes.

## MANDATORY POST-USE AUDIT

After every migration, perform a post-use audit:

1. Check if any remote name was left in an ambiguous state.
2. Verify if file-locks prevented the push.
3. Update this skill if a new SaaS platform (e.g. Greptile) requires specific migration steps.
4. State: `skill(github-repo-migration): no gaps identified` or `skill(github-repo-migration): [fix applied]`.
