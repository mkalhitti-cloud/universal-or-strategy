# GitHub Personal Access Token Storage
**Last Updated**: 2026-06-05 (Phase 11 Token Persistence Fix)

**CRITICAL**: This file is gitignored. Never commit tokens to git.

## Current PAT (antigravityos187-sketch)

**Created**: 2026-06-04
**Expires**: [Set expiration when creating]
**Scopes Required**:
- ✅ `repo` (full control of private repositories)
- ✅ `workflow` (update GitHub Actions workflows)
- ✅ `admin:org` (organization management - optional)
- ✅ `delete_repo` (delete repositories - optional)

**Token**: `ghp_XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX` (Updated 2026-06-05)
**Previous Token**: `ghp_XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX` (Deprecated)

## Usage

### CRITICAL: User-Level Persistence Required

**Problem**: Setting `$env:GITHUB_TOKEN` only persists for the current session. When the terminal closes, the token is lost and reverts to the old account.

**Solution**: Set at User level for permanent persistence:

```powershell
# CORRECT: Set at User level (persists across terminal restarts)
[System.Environment]::SetEnvironmentVariable('GITHUB_TOKEN', 'ghp_YOUR_TOKEN_HERE', 'User')

# Also set for current session
$env:GITHUB_TOKEN = "ghp_YOUR_TOKEN_HERE"

# Verify authentication
gh auth status
# Should show: Logged in to github.com account antigravityos187-sketch

# Verify User-level persistence
[System.Environment]::GetEnvironmentVariable('GITHUB_TOKEN', 'User')
# Should return: ghp_YOUR_TOKEN_HERE
```

### Troubleshooting Token Persistence

**Symptom**: `gh auth status` shows old account (backtothefutures83-oss) after terminal restart

**Diagnosis**:
```powershell
# Check which level token is set at
[System.Environment]::GetEnvironmentVariable('GITHUB_TOKEN', 'Process')  # Current session only
[System.Environment]::GetEnvironmentVariable('GITHUB_TOKEN', 'User')     # Persistent (REQUIRED)
```

**Fix**:
```powershell
# 1. Set at User level
[System.Environment]::SetEnvironmentVariable('GITHUB_TOKEN', 'ghp_YOUR_TOKEN_HERE', 'User')

# 2. Update current session
$env:GITHUB_TOKEN = "ghp_YOUR_TOKEN_HERE"

# 3. Verify correct account
gh auth status
```

## Token Rotation

When rotating tokens:
1. Generate new token at: https://github.com/settings/tokens/new
2. Update this file with new token
3. Re-authenticate GitHub CLI
4. Test PR creation permissions
5. Revoke old token

## Security Notes

- This file is in `.gitignore` (verified)
- Never share tokens in chat logs or screenshots
- Rotate tokens every 90 days
- Use fine-grained tokens when possible

## Lessons Learned (Phase 11 - 2026-06-05)

**Issue**: GitHub CLI authenticated to wrong account (backtothefutures83-oss instead of antigravityos187-sketch) despite previous token updates.

**Root Cause**: Token was set at Process level (`$env:GITHUB_TOKEN`) instead of User level, causing it to revert to old token after terminal restart.

**Resolution**:
1. Set token at User level using `[System.Environment]::SetEnvironmentVariable('GITHUB_TOKEN', '<token>', 'User')`
2. Verified persistence with `[System.Environment]::GetEnvironmentVariable('GITHUB_TOKEN', 'User')`
3. Confirmed correct account with `gh auth status`

**Prevention**: Always use User-level environment variables for tokens that must persist across sessions.