# PR #5 Orchestration Workflow

**Version**: 1.0  
**Date**: 2026-06-05  
**Purpose**: Safe workflow for preserving documentation work and orchestrating PR #5 with new unified review system

---

## Current Situation

### Branch Status
- **Current Branch**: `gitbutler/workspace`
- **PR #5 Branch**: `fix/pr5-clean-cs-only` (exists locally and on origin)
- **Untracked Files**: 200+ new documentation and configuration files
- **Stashed Work**: 26 stashes (including PR #5 work in stash@{10} and stash@{11})

### New Assets to Preserve
All files created during unified scoring implementation:
- `.codeant.yml`, `.snyk`, `sonar-project.properties`, `.gitguardian.yaml`
- `.bob/mcp.json` (Greptile MCP configuration)
- `docs/mcp/*.md` (8 new MCP documentation files)
- `docs/workflow/*.md` (3 new workflow files)
- `docs/protocol/GITHUB_MIGRATION_SKILL.md` (updated)
- All GitHub Actions workflows in `.github/workflows/`

---

## Strategy: GitButler Virtual Branch Workflow

### Why GitButler?
✅ **Selective Staging**: Stage only `.cs` files for PR #5  
✅ **Preserve Context**: Keep all documentation in workspace  
✅ **No Data Loss**: Documentation stays uncommitted but tracked  
✅ **Clean PRs**: PR #5 contains ONLY `.cs` changes  

### Alternative Considered: Separate Branches
❌ **Rejected** because:
- Requires committing docs to a separate branch
- Risk of merge conflicts when both branches merge to main
- More complex to manage multiple PRs simultaneously
- GitButler already installed and configured

---

## Phase 1: Preserve Current Work (CRITICAL)

### Step 1.1: Commit Documentation to Workspace Branch

```powershell
# Add all new documentation and configuration files
git add .codeant.yml .snyk sonar-project.properties .gitguardian.yaml
git add .bob/mcp.json
git add docs/mcp/
git add docs/workflow/
git add docs/protocol/GITHUB_MIGRATION_SKILL.md
git add .github/workflows/

# Commit with clear message
git commit -m "docs: unified scoring system + MCP integration + workflow orchestration

- Full-pass review configurations (.codeant.yml, .snyk, sonar-project.properties, .gitguardian.yaml)
- MCP server configurations and troubleshooting guides
- Workflow orchestration documentation (PR Loop, Epic Loop, Problems/Comments integration)
- GitHub migration guide with SaaS platform migration steps
- Multi-layer secrets scanning strategy
- Real-time layered defense documentation

Part of V12.23 Protocol - Unified 5/5 Scoring Initiative"

# Push to preserve remotely
git push origin gitbutler/workspace
```

**Verification**:
```powershell
git log -1 --stat  # Verify commit includes all files
git status         # Should show clean working directory
```

---

## Phase 2: Prepare PR #5 Branch

### Step 2.1: Fetch Latest Remote State

```powershell
# Ensure we have latest remote refs
git fetch origin

# Check PR #5 branch status
git log origin/fix/pr5-clean-cs-only --oneline -5
```

### Step 2.2: Inspect Stashed PR #5 Work

```powershell
# View stash@{10} contents (PR #5 forensics)
git stash show -p stash@{10} | Select-String -Pattern "\.cs$" -Context 2

# View stash@{11} contents (PR #5 docs)
git stash show -p stash@{11} | Select-String -Pattern "\.cs$" -Context 2
```

### Step 2.3: Decision Point - Recreate or Reuse?

**Option A: Recreate PR #5 Branch (RECOMMENDED)**
- ✅ Clean slate - no contamination risk
- ✅ Apply only validated `.cs` fixes
- ✅ Use new workflow from start
- ⚠️ Must re-extract fixes from forensics

**Option B: Reuse Existing Branch**
- ✅ Faster - work already done
- ❌ May contain non-`.cs` contamination
- ❌ Doesn't test new workflow end-to-end

**DECISION**: Use Option A (recreate) to pilot new workflow properly.

---

## Phase 3: Recreate PR #5 with New Workflow

### Step 3.1: Create Fresh Branch from Main

```powershell
# Ensure main is up to date
git checkout main
git pull origin main

# Create new PR #5 branch
git checkout -b fix/pr5-unified-review-v2

# Verify clean state
git status  # Should show "nothing to commit, working tree clean"
```

### Step 3.2: Run Autonomous Orchestration

**Invoke New Workflow**:
```powershell
# This will be executed by Bob autonomously
# User just provides the trigger command
```

**Bob's Autonomous Steps** (from `docs/workflow/LOOP_ORCHESTRATION.md`):

1. **Extract GitHub Findings**:
   ```powershell
   gh pr view 5 --json reviews,comments | ConvertFrom-Json | ConvertTo-Json -Depth 10 > pr_5_github_findings.json
   ```

2. **Extract Greptile Findings** (via MCP):
   ```
   <use_mcp_tool>
   <server_name>greptile</server_name>
   <tool_name>list_merge_request_comments</tool_name>
   <arguments>
   {
     "name": "universal-or-strategy",
     "remote": "github",
     "defaultBranch": "main",
     "prNumber": 5,
     "greptileGenerated": true
   }
   </arguments>
   </use_mcp_tool>
   ```

3. **Extract VS Code Problems**:
   ```powershell
   .\scripts\extract_vscode_problems.ps1 -OutputPath "pr_5_vscode_problems.json"
   ```

4. **Categorize All Findings**:
   ```powershell
   python scripts/categorize_problems.py `
     --github pr_5_github_findings.json `
     --greptile pr_5_greptile_findings.json `
     --vscode pr_5_vscode_problems.json `
     --output pr_5_categorized.json
   ```

5. **Deduplicate**:
   ```powershell
   python scripts/deduplicate_findings.py `
     --input pr_5_categorized.json `
     --output pr_5_actionable.json
   ```

6. **Generate Fix Queue**:
   ```powershell
   python scripts/generate_fix_queue.py `
     --input pr_5_actionable.json `
     --output docs/brain/pr_5_fix_queue_v2.md
   ```

7. **Apply Fixes** (one at a time, verify each):
   - Read fix from queue
   - Apply using `apply_diff` or `write_to_file`
   - Verify with `execute_command` (build/test)
   - Mark complete in queue
   - Repeat until queue empty

8. **Verify Build**:
   ```powershell
   dotnet build --no-incremental
   ```

9. **Run Tests**:
   ```powershell
   dotnet test --no-build
   ```

10. **Commit** (only `.cs` files):
    ```powershell
    git add src/**/*.cs tests/**/*.cs
    git commit -m "fix: PR #5 unified review fixes - [category]
    
    Addresses findings from:
    - GitHub reviews (X issues)
    - Greptile MCP (Y issues)
    - VS Code Problems panel (Z issues)
    
    Total actionable issues: N
    Deduplication: M → N (X% reduction)
    
    All fixes validated against Jane Street principles."
    ```

11. **Push**:
    ```powershell
    git push origin fix/pr5-unified-review-v2
    ```

12. **Create PR**:
    ```powershell
    gh pr create --title "fix: PR #5 unified review fixes" --body "$(cat pr_5_description.md)"
    ```

13. **Run PR Loop**:
    ```powershell
    # Bob will invoke this autonomously
    # /pr-loop 5
    ```

---

## Phase 4: Autonomous Execution Protocol

### Bob's Responsibilities

**Before Starting**:
- ✅ Verify all scripts exist (`extract_vscode_problems.ps1`, `categorize_problems.py`, etc.)
- ✅ Verify Greptile MCP is authenticated (or use CLI fallback)
- ✅ Verify current branch is `fix/pr5-unified-review-v2`
- ✅ Verify working directory is clean

**During Execution**:
- ✅ Execute each step sequentially
- ✅ Verify success before proceeding to next step
- ✅ If step fails, diagnose and fix issue before continuing
- ✅ Log all actions to `pr_5_orchestration_log.md`
- ✅ Update progress in session context

**Error Handling**:
- If Greptile MCP fails → Fall back to Greptile CLI
- If script missing → Create script on-the-fly
- If build fails → Stop, report error, wait for user guidance
- If test fails → Stop, report error, wait for user guidance

**Reporting**:
After each major phase, report:
```
Phase X Complete: [Phase Name]
- Files processed: N
- Issues found: M
- Issues fixed: K
- Build status: ✅/❌
- Test status: ✅/❌
- Next phase: [Phase Name]
```

### User's Responsibilities

**Monitoring**:
- Watch Bob's progress reports
- Intervene only if Bob requests guidance
- Approve final PR creation

**Feedback Loop**:
- If workflow has issues, note them for improvement
- After PR #5 complete, update workflow documentation
- Iterate on autonomous capabilities

---

## Phase 5: Post-Orchestration

### Step 5.1: Verify PR #5 Quality

```powershell
# Check PR status
gh pr view 5 --json reviews,checks,comments

# Verify unified scoring
# Expected output:
# - SonarCloud: 5/5 ⭐⭐⭐⭐⭐
# - Snyk: 5/5 ⭐⭐⭐⭐⭐
# - Sourcery: 5/5 ⭐⭐⭐⭐⭐
# - CodeAnt: 5/5 ⭐⭐⭐⭐⭐
# - Cubic: 5/5 ⭐⭐⭐⭐⭐
# - GitGuardian: 5/5 ⭐⭐⭐⭐⭐
# - Greptile: 5/5 ⭐⭐⭐⭐⭐
```

### Step 5.2: Merge Documentation Branch

```powershell
# Switch back to workspace branch
git checkout gitbutler/workspace

# Create PR for documentation
gh pr create --title "docs: unified scoring system + workflow orchestration" --body "See commit message for details"

# Merge after approval
gh pr merge <PR_NUMBER> --squash --delete-branch
```

### Step 5.3: Update Workflow Documentation

Based on pilot experience, update:
- `docs/workflow/LOOP_ORCHESTRATION.md` - Add lessons learned
- `docs/workflow/PROBLEMS_COMMENTS_INTEGRATION.md` - Refine categorization rules
- `docs/protocol/PR_LOOP_V2.md` - Update with autonomous execution notes

---

## Safety Checklist

Before starting orchestration, verify:

- [ ] All documentation committed to `gitbutler/workspace`
- [ ] `gitbutler/workspace` pushed to origin
- [ ] Main branch is up to date
- [ ] New branch created from main: `fix/pr5-unified-review-v2`
- [ ] Working directory is clean
- [ ] All scripts exist in `scripts/` directory
- [ ] Greptile MCP authenticated (or CLI available)
- [ ] VS Code Problems panel accessible
- [ ] GitHub CLI authenticated

---

## Rollback Plan

If orchestration fails catastrophically:

```powershell
# Abort current work
git reset --hard origin/main

# Restore documentation
git checkout gitbutler/workspace

# Restore stashed PR #5 work (if needed)
git stash apply stash@{10}  # PR #5 forensics
git stash apply stash@{11}  # PR #5 docs

# Manual fallback: Use old workflow
# Follow docs/brain/pr_5_orchestration_plan.md
```

---

## Success Criteria

PR #5 orchestration is successful when:

1. ✅ All `.cs` fixes applied and committed
2. ✅ Build passes: `dotnet build --no-incremental`
3. ✅ Tests pass: `dotnet test --no-build`
4. ✅ PR created with unified scoring
5. ✅ All 7 bots report 5/5 ⭐⭐⭐⭐⭐
6. ✅ No non-`.cs` files in PR diff
7. ✅ Documentation preserved in separate branch
8. ✅ Workflow improvements documented

---

## Next Steps After PR #5

1. **Merge Documentation PR** - Get unified scoring configs into main
2. **Update Migration Guide** - Add PR #5 pilot results
3. **Refine Autonomous Workflow** - Based on lessons learned
4. **Apply to Next PR** - Use improved workflow on PR #6+
5. **Measure Efficiency** - Compare old vs new workflow (time, accuracy, autonomy)

---

## Appendix: Script Inventory

Scripts that must exist before orchestration:

| Script | Purpose | Status |
|--------|---------|--------|
| `scripts/extract_vscode_problems.ps1` | Extract Problems panel to JSON | ⚠️ Create |
| `scripts/categorize_problems.py` | Categorize findings using Jane Street KB | ⚠️ Create |
| `scripts/deduplicate_findings.py` | Merge and deduplicate findings | ⚠️ Create |
| `scripts/generate_fix_queue.py` | Generate prioritized fix queue | ⚠️ Create |
| `scripts/verify_pr_hygiene.ps1` | Pre-push validation | ✅ Exists |
| `scripts/git_src_only.ps1` | Stage only src/ files | ✅ Exists |

**Action Required**: Create missing scripts before starting orchestration.

---

## Version History

- **1.0** (2026-06-05): Initial workflow for PR #5 pilot
  - GitButler virtual branch strategy
  - Autonomous orchestration protocol
  - Safety checklist and rollback plan
  - Script inventory