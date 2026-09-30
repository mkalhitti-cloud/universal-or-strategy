# GitHub Account Migration Protocol

**Version**: 1.0  
**Date**: 2026-06-08  
**Status**: Active

---

## Overview

This protocol documents the complete process for migrating a repository between GitHub accounts, including all authentication, token management, and cleanup procedures discovered during the `malhitticrypto-debug` → `antigravityos187-sketch` migration.

---

## Pre-Migration Checklist

### 1. Token Cleanup (MANDATORY - Run First)

**⚠️ CRITICAL**: Remove old `GITHUB_TOKEN` from previous account BEFORE migration.

**Why This Step is Mandatory**:
- Old tokens from previous accounts persist in PowerShell profile and environment variables
- They override `gh` CLI authentication silently
- Cause permission errors that are hard to diagnose (e.g., "Resource not accessible by personal access token")
- **This was the root cause of PR #9 closure failure**

**Automated Cleanup Script**:
```powershell
powershell -File .\scripts\cleanup_github_token.ps1
```

**What It Does**:
1. Removes `GITHUB_TOKEN` from Process environment
2. Removes `GITHUB_TOKEN` from User environment variables
3. Removes `GITHUB_TOKEN` from System environment variables (if admin)
4. Comments out `GITHUB_TOKEN` in `.env` file
5. **Detects `GITHUB_TOKEN` in PowerShell profile** (manual removal required)

**Manual Step Required**:
If script reports "FOUND in PowerShell profile":
```powershell
# Open profile in editor
notepad $PROFILE

# Remove or comment out lines like:
# $env:GITHUB_TOKEN = "github_pat_..."

# Save and close
```

**Verification**:
```powershell
Get-ChildItem Env: | Where-Object { $_.Name -like "*GITHUB*" }
# Should show NO GITHUB_TOKEN
```

**IMPORTANT**: Restart terminal/IDE after cleanup for changes to take effect.

---

### 2. Account Verification

**Identify Primary Account**:
```powershell
gh auth status
```

**Expected Output**:
```
github.com
  ✓ Logged in to github.com account <PRIMARY_ACCOUNT>
  - Active account: true
  - Token scopes: 'repo', 'workflow', 'admin:org', ...
```

**Action**: Confirm the active account matches your target account.

---

### 2. Token Conflict Detection

**Problem**: `GITHUB_TOKEN` environment variable can override `gh` CLI authentication.

**Check for Conflicts**:
```powershell
Get-ChildItem Env: | Where-Object { $_.Name -like "*GITHUB*" }
```

**If `GITHUB_TOKEN` is set**:
- It will override keyring-stored credentials
- GitHub CLI will use the token's associated account, not the active account
- This causes permission errors when the token belongs to a different account

**Solution**: Remove `GITHUB_TOKEN` from environment variables (see Section 4).

---

## Migration Steps

### Step 1: Transfer Repository Ownership

**GitHub Web UI**:
1. Navigate to: `https://github.com/<OLD_ACCOUNT>/<REPO>/settings`
2. Scroll to "Danger Zone"
3. Click "Transfer ownership"
4. Enter new account name: `<NEW_ACCOUNT>`
5. Confirm transfer

**Result**: Repository URL changes from:
- `https://github.com/<OLD_ACCOUNT>/<REPO>`
- → `https://github.com/<NEW_ACCOUNT>/<REPO>`

---

### Step 2: Update Local Git Remote

**Check Current Remote**:
```powershell
git remote -v
```

**Update Remote URL**:
```powershell
git remote set-url origin https://github.com/<NEW_ACCOUNT>/<REPO>.git
```

**Verify**:
```powershell
git remote -v
# Should show new account in URLs
```

---

### Step 3: Clean Up Old PRs

**Problem**: PRs from old account remain open after transfer.

**List All PRs**:
```powershell
# Temporarily unset GITHUB_TOKEN if it exists
$env:GITHUB_TOKEN = $null
gh pr list --state all
```

**Close Old PRs**:
```powershell
# For each old PR number:
$env:GITHUB_TOKEN = $null
gh pr close <PR_NUMBER>
```

**Why Unset GITHUB_TOKEN**:
- If `GITHUB_TOKEN` belongs to old account, `gh pr close` will fail with:
  ```
  GraphQL: Resource not accessible by personal access token (closePullRequest)
  ```
- Unsetting forces `gh` to use keyring-stored credentials for new account

---

### Step 4: Fix GITHUB_TOKEN Conflict (Permanent)

**Root Cause**: `GITHUB_TOKEN` environment variable set in `.env` or system environment.

**Option A: Remove from .env File**:
```powershell
# Edit .env and remove or comment out:
# GITHUB_TOKEN=github_pat_...
```

**Option B: Remove from System Environment** (Windows):
1. Open "Environment Variables" (System Properties)
2. Check both "User variables" and "System variables"
3. Delete `GITHUB_TOKEN` if present
4. Restart terminal/IDE

**Option C: Unset in Current Session** (Temporary):
```powershell
$env:GITHUB_TOKEN = $null
```

**Verification**:
```powershell
Get-ChildItem Env: | Where-Object { $_.Name -like "*GITHUB*" }
# Should NOT show GITHUB_TOKEN
```

---

### Step 5: Verify GitHub CLI Authentication

**Switch to New Account** (if needed):
```powershell
gh auth switch --user <NEW_ACCOUNT>
```

**If Error: "GITHUB_TOKEN environment variable is being used"**:
- You must remove `GITHUB_TOKEN` from environment first (Step 4)
- Then retry `gh auth switch`

**Verify Active Account**:
```powershell
gh auth status
```

**Expected**:
```
github.com
  ✓ Logged in to github.com account <NEW_ACCOUNT>
  - Active account: true
```

---

### Step 6: Test Operations

**Test PR Operations**:
```powershell
# List PRs (should work without unsetting GITHUB_TOKEN)
gh pr list

# Create test PR (if needed)
gh pr create --title "Test PR" --body "Testing authentication"

# Close test PR
gh pr close <PR_NUMBER>
```

**If Any Command Fails**:
- Check `gh auth status` again
- Verify `GITHUB_TOKEN` is not set: `$env:GITHUB_TOKEN`
- Re-authenticate: `gh auth login`

---

## Post-Migration Verification

### Checklist

- [ ] Repository accessible at new URL: `https://github.com/<NEW_ACCOUNT>/<REPO>`
- [ ] Local remote updated: `git remote -v` shows new account
- [ ] All old PRs closed: `gh pr list --state open` shows 0 results
- [ ] `GITHUB_TOKEN` removed from environment
- [ ] `gh auth status` shows new account as active
- [ ] Can create/close PRs without permission errors
- [ ] Can push to repository: `git push origin main`

---

## Common Issues and Solutions

### Issue 1: "Resource not accessible by personal access token"

**Symptom**:
```
GraphQL: Resource not accessible by personal access token (closePullRequest)
```

**Cause**: `GITHUB_TOKEN` environment variable belongs to old account.

**Solution**:
```powershell
$env:GITHUB_TOKEN = $null
gh pr close <PR_NUMBER>
```

**Permanent Fix**: Remove `GITHUB_TOKEN` from `.env` or system environment (Step 4).

---

### Issue 2: PRs Still Show Old Account

**Symptom**: PRs created before migration still reference old account.

**Explanation**: This is expected. GitHub preserves PR history including original author.

**Solution**: Close old PRs and create new ones from new account.

---

### Issue 3: Cannot Switch Active Account

**Symptom**:
```
The value of the GITHUB_TOKEN environment variable is being used for authentication.
To have GitHub CLI manage credentials instead, first clear the value from the environment.
```

**Solution**: Remove `GITHUB_TOKEN` from environment (Step 4), then retry `gh auth switch`.

---

### Issue 4: Git Push Fails After Migration

**Symptom**:
```
remote: Repository not found.
fatal: repository 'https://github.com/<OLD_ACCOUNT>/<REPO>.git/' not found
```

**Cause**: Local remote still points to old account.

**Solution**: Update remote URL (Step 2).

---

## GitButler Integration

**Important**: GitButler virtual branches are workspace-local and unaffected by account migration.

**After Migration**:
1. Verify workspace: `git branch` should show `gitbutler/workspace`
2. Sync with new remote: `git fetch origin main`
3. Rebase workspace: `git rebase origin/main`
4. Continue using GitButler normally

**No Action Required**: Virtual branches remain intact.

---

## Automation Script

**Future Enhancement**: Create `scripts/migrate_github_account.ps1` to automate:
1. Remote URL update
2. Old PR cleanup
3. Token conflict detection
4. Authentication verification

**Not Implemented Yet**: Manual process documented above is current standard.

---

## References

- **GitHub CLI Docs**: https://cli.github.com/manual/
- **GitHub Transfer Ownership**: https://docs.github.com/en/repositories/creating-and-managing-repositories/transferring-a-repository
- **V12 Protocol**: `docs/protocol/BRANCH_STRATEGY.md`
- **GitButler Workflow**: `docs/workflow/AUTONOMOUS_GITBUTLER_WORKFLOW.md`

---

## Changelog

| Date | Version | Changes |
|------|---------|---------|
| 2026-06-08 | 1.0 | Initial protocol based on malhitticrypto-debug → antigravityos187-sketch migration |

---

**Protocol Status**: ✅ VALIDATED (PR #9 closure successful after token fix)