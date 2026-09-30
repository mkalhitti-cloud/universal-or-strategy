# GitHub Migration Skill

## Purpose
Automate GitHub repository migration between accounts, including token setup, repository transfer, and CI/CD reconfiguration.

## When to Use
- Migrating repositories between GitHub accounts
- Setting up new GitHub account with existing repositories
- Reconfiguring CI/CD after account changes

## Prerequisites
- Source and target GitHub accounts
- Admin access to source repository
- GitHub CLI (`gh`) installed

## Execution Steps

### Phase 0: Token Cleanup (MANDATORY - Run First)

**⚠️ CRITICAL**: Remove old `GITHUB_TOKEN` from previous account BEFORE starting migration.

**Why This Step is Mandatory**:
- Old tokens from previous accounts persist in PowerShell profile and environment variables
- They override `gh` CLI authentication silently
- Cause permission errors that are hard to diagnose (e.g., "Resource not accessible by personal access token")
- **Root cause of PR closure failures during malhitticrypto-debug → antigravityos187-sketch migration**

**Automated Cleanup**:
```powershell
# Run cleanup script
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

### Phase 1: Pre-Migration Checklist
1. **Backup current state**
   - Export repository settings
   - Document current CI/CD configuration
   - Save all GitHub Actions secrets
   - Export branch protection rules

2. **Verify access**
   - Confirm admin access to source repository
   - Verify target account exists
   - Check repository name availability in target account

### Phase 2: Generate GitHub PAT Token
**CRITICAL**: This step must be completed BEFORE any migration work.

1. **Navigate to token generation page**
   - Go to: https://github.com/settings/tokens/new
   - Or: Settings > Developer settings > Personal access tokens > Tokens (classic)

2. **Configure token**
   - **Note**: Use descriptive name (e.g., "Bob new new", "Migration Token")
   - **Expiration**: Set to "No expiration" (or appropriate duration)
   - **Required Scopes** (check ALL of these):
     - ✅ `repo` (full control of private repositories)
       - Includes: repo:status, repo_deployment, public_repo, repo:invite, security_events
     - ✅ `workflow` (update GitHub Action workflows)
     - ✅ `admin:org` (full control of orgs and teams)
       - Minimum: `read:org` if full admin not needed
     - ✅ `delete_repo` (optional, for cleanup)

3. **Generate and save token**
   - Click "Generate token"
   - **IMMEDIATELY copy the token** (shown only once)
   - Token format: `ghp_` followed by 36 characters
   - Example: `ghp_XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX` (36 characters)

4. **Store token securely**
   - Save to `docs/brain/GITHUB_MIGRATION_TOKENS.md`
   - Add to password manager (1Password, LastPass, etc.)
   - **NEVER commit token to git**

### Phase 3: Configure GitHub CLI Authentication

1. **Set token persistently (User level)**
   ```powershell
   # Windows PowerShell
   [System.Environment]::SetEnvironmentVariable('GITHUB_TOKEN', 'ghp_YOUR_TOKEN_HERE', 'User')
   
   # Also set for current session
   $env:GITHUB_TOKEN = "ghp_YOUR_TOKEN_HERE"
   ```

2. **Verify authentication**
   ```powershell
   gh auth status --show-token
   ```
   
   Expected output:
   ```
   ✓ Logged in to github.com account YOUR_ACCOUNT (GITHUB_TOKEN)
   - Active account: true
   - Token scopes: 'repo', 'workflow', 'admin:org', ...
   ```

3. **Test token permissions**
   ```powershell
   # Test repository access
   gh api /repos/OWNER/REPO --jq '.full_name'
   
   # Should return: OWNER/REPO
   ```

### Phase 4: Repository Transfer

1. **Transfer repository**
   ```bash
   # Via GitHub CLI
   gh repo transfer SOURCE_OWNER/REPO_NAME TARGET_OWNER
   
   # Or via web interface:
   # Settings > General > Danger Zone > Transfer ownership
   ```

2. **Verify transfer**
   ```bash
   gh repo view TARGET_OWNER/REPO_NAME
   ```

### Phase 5: Update Local Git Configuration

1. **Update remote URL**
   ```bash
   git remote set-url origin https://github.com/TARGET_OWNER/REPO_NAME.git
   ```

2. **Verify remote**
   ```bash
   git remote -v
   ```

3. **Test push access**
   ```bash
   git fetch origin
   ```

### Phase 6: Reconfigure CI/CD

1. **Update GitHub Actions secrets**
   - Navigate to: Settings > Secrets and variables > Actions
   - Add/update required secrets:
     - `CODACY_API_TOKEN`
     - `CS_ACCESS_TOKEN` (CodeScene)
     - `SONAR_TOKEN`
     - Any other service tokens

2. **Update workflow files** (if needed)
   - Check for hardcoded account names
   - Update repository references
   - Verify branch names

3. **Update external services**
   - Codacy: Update repository connection
   - CodeScene: Update repository connection
   - SonarCloud: Update repository connection
   - Any other integrated services

### Phase 7: Update Documentation

1. **Update GITHUB_MIGRATION_TOKENS.md**
   - Add new PAT token
   - Document token scopes
   - Add usage instructions

2. **Update repository documentation**
   - Update README.md with new repository URL
   - Update CONTRIBUTING.md if it references old account
   - Update any hardcoded URLs in docs/

3. **Update CI/CD documentation**
   - Document new secrets configuration
   - Update deployment instructions

### Phase 8: Verification

1. **Test GitHub CLI operations**
   ```bash
   # Test PR creation (dry run)
   gh pr list
   
   # Test issue access
   gh issue list
   ```

2. **Test CI/CD pipeline**
   - Trigger a test workflow run
   - Verify all secrets are accessible
   - Check external service integrations

3. **Verify branch protection rules**
   - Check Settings > Branches
   - Verify required status checks
   - Test push restrictions

## Common Issues

### Issue: "Resource not accessible by personal access token"
**Cause**: Token missing required scopes (usually `workflow` or `repo`)
**Solution**: 
1. Regenerate token with all required scopes
2. Update `GITHUB_TOKEN` environment variable
3. Verify with `gh auth status --show-token`

### Issue: Token reverts to old account
**Cause**: Environment variable not set persistently
**Solution**:
```powershell
# Set at User level (persists across sessions)
[System.Environment]::SetEnvironmentVariable('GITHUB_TOKEN', 'ghp_NEW_TOKEN', 'User')

# Restart terminal or reload environment
```

### Issue: Repository protection rules block push
**Cause**: Pre-push validation gates or branch protection rules
**Solution**: This is expected behavior - fix validation issues before pushing

## Post-Migration Checklist

- [ ] GitHub CLI authenticated with new account
- [ ] Token has all required scopes (repo, workflow, admin:org)
- [ ] Token set persistently at User level
- [ ] Repository transferred to new account
- [ ] Local git remote updated
- [ ] GitHub Actions secrets configured
- [ ] External services (Codacy, CodeScene, SonarCloud) updated
- [ ] Documentation updated with new URLs
- [ ] CI/CD pipeline tested and working
- [ ] Branch protection rules verified
- [ ] Old account token documented (kept for old account access)

## Security Notes

- **NEVER commit tokens to git**
- Store tokens in password manager
- Rotate tokens every 90 days
- Revoke old tokens after migration (for old account)
- Keep old account token separate (don't delete - needed for old account access)

## References

- GitHub PAT Documentation: https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/creating-a-personal-access-token
- GitHub CLI Documentation: https://cli.github.com/manual/
- Repository Transfer: https://docs.github.com/en/repositories/creating-and-managing-repositories/transferring-a-repository

## Post-Use Audit (MANDATORY - Anthropic Skill-Creator Protocol)

**All agents MUST perform this audit after EVERY use of this skill:**

### Audit Checklist

1. **Ambiguity Check**: Were any instructions unclear or produce unexpected results?
   - Did Phase 0 (Token Cleanup) execute successfully? (Verify old tokens removed)
   - Was the PAT token generated with ALL required scopes? (repo, workflow, admin:org)
   - Did `gh auth status --show-token` confirm authentication? (Verify token active)
   - Was the token set persistently at User level? (Check environment variables)
   - Did repository transfer complete? (Verify new owner in `gh repo view`)
   - Were all CI/CD secrets reconfigured? (Codacy, CodeScene, SonarCloud)
   - Did local git remote update? (Verify `git remote -v` shows new URL)
   - Were all documentation URLs updated? (README, CONTRIBUTING, docs/)

2. **Gap Detection**: If ANY instruction was ambiguous or produced unexpected results:
   - Document the gap in this SKILL.md immediately
   - Add the quirk to the relevant section (Common Issues, Phase instructions, etc.)
   - Update version history with the fix

3. **Audit Statement**: If no gaps found, state:
   ```
   skill(github-migration): no gaps identified
   ```

4. **Protocol Violation**: Skipping this audit is a V12 protocol violation.

### Known Quirks (Updated During Audits)

- **Token Cleanup (2026-06-08)**: Old `GITHUB_TOKEN` from previous accounts persists in PowerShell profile - MUST be manually removed
- **Token Scopes (2026-06-08)**: Missing `workflow` scope causes "Resource not accessible" errors - regenerate with all scopes
- **OAuth Issues (2026-06-08)**: Some MCP servers (Cubic) don't support OAuth in Bob IDE - use CLI fallback
- **PowerShell Profile (2026-06-08)**: `cleanup_github_token.ps1` detects but cannot auto-remove tokens from `$PROFILE` - manual edit required

## V12 DNA Alignment

- **Correctness by Construction**: Phase 0 cleanup prevents silent authentication failures
- **ASCII-Only**: All commit messages and documentation must be ASCII-safe
- **Jane Street Alignment**: Explicit token scope checklist prevents permission errors
- **Karpathy Protocol**: Explicit success criteria at each phase (token verified, transfer complete, etc.)

---

**Last Updated**: 2026-06-08
**Maintainer**: Gemini CLI (Advanced Mode)
**Status**: ✅ Active - Converted to self-improving format with Phase 0 token cleanup