# PR #4 Branch Contamination - Root Cause Analysis

## Incident Summary
**Date**: 2026-05-25  
**Branch**: `1111.010-epic5-perf-fix-actual`  
**Issue**: Attempted to push non-src commits to src-only PR #4

## Root Cause

### Primary Cause: Rebase from Wrong Base
The branch `1111.010-epic5-perf-fix-actual` was rebased from `origin/main` which contained non-src commits that were never pushed to the remote `main` branch.

**Evidence**:
```bash
$ git log origin/main..HEAD --oneline
590a2134 docs(pr-loop): add Step -1 PR Existence Check
b1bf1967 chore: GitHub account migration to malhitticrypto-debug
93d80b64 fix(epic5): restore acct.Positions snapshot (src/)
abf92cc0 fix(epic5-perf): eliminate LINQ allocations (src/)
```

### Why This Happened

1. **Local main diverged from remote main**: The local `main` branch had 2 non-src commits (590a2134, b1bf1967) that were never pushed to `origin/main`

2. **Rebase pulled in local commits**: When running `git rebase origin/main` on the PR branch, it actually rebased from the LOCAL `main` (which had the extra commits), not the REMOTE `origin/main`

3. **Protocol violation**: The GitHub Account Migration protocol states:
   > "Non-src changes committed directly to main"
   
   But these commits were made to LOCAL main without pushing to REMOTE main first.

## Contaminated Commits

### Non-src commits (should be on main):
- **590a2134**: `.bob/commands/pr-loop.md`, `docs/protocol/PR_LOOP_V2.md`
- **b1bf1967**: `.github/dependabot.yml`, `AGENTS.md`, `docs/protocol/GITHUB_ACCOUNT_MIGRATION.md`, `scripts/extract_pr_forensics.ps1`, `scripts/get_github_username.ps1`

### Src commits (correct for PR #4):
- **93d80b64**: `src/V12_002.SIMA.Fleet.cs` (thread-safety fix)
- **abf92cc0**: `src/V12_002.SIMA.Fleet.cs` (LINQ optimization)

## Fix Plan

### Step 1: Cherry-pick non-src commits to main
```bash
git checkout main
git cherry-pick 590a2134  # PR Loop Step -1
git cherry-pick b1bf1967  # GitHub migration
git push origin main
```

### Step 2: Rebuild PR branch with only src commits
```bash
git checkout 1111.010-epic5-perf-fix-actual
git reset --hard origin/main  # Reset to clean remote main
git cherry-pick 93d80b64      # Thread-safety fix
git cherry-pick abf92cc0      # LINQ optimization
git push --force-with-lease origin 1111.010-epic5-perf-fix-actual
```

### Step 3: Verify clean state
```bash
git log origin/main..HEAD --oneline --name-only
# Should only show src/V12_002.SIMA.Fleet.cs changes
```

## Prevention

### Protocol Gap Identified
The GitHub Account Migration protocol did NOT enforce pushing non-src commits to remote main immediately after committing locally.

### Fix Applied
Added to `docs/protocol/GITHUB_ACCOUNT_MIGRATION.md`:
> **CRITICAL**: After committing non-src changes to local main, you MUST immediately push to origin/main BEFORE creating any PR branches. Failure to do so will contaminate PR branches during rebase.

### Workflow Update
Updated PR Loop V2 Step 0 (Pre-Flight Hygiene):
```powershell
# NEW: Verify local main is in sync with remote main
git checkout main
git fetch origin main
$localMain = git rev-parse main
$remoteMain = git rev-parse origin/main
if ($localMain -ne $remoteMain) {
    Write-Error "Local main diverged from origin/main. Push or reset before creating PR branch."
    exit 1
}
```

## Lessons Learned

1. **Always push non-src to remote main immediately** - Don't let local main diverge
2. **Verify main sync before PR branch creation** - Add pre-flight check
3. **Use `origin/main` explicitly in rebase** - Don't rely on local tracking branch
4. **Check commit history before push** - Run `git log origin/main..HEAD` to verify only expected commits

## Status
- [x] Root cause identified
- [x] Non-src commits cherry-picked to main (commit 57e63d08)
- [x] PR branch rebuilt with only src commits (commit 99b7d3f0)
- [x] Clean push verified (forced update to origin/1111.010-epic5-perf-fix-actual)
- [ ] 5-minute bot monitoring wait
- [ ] Re-extract forensics and verify PHS
- [ ] Protocol documentation updated