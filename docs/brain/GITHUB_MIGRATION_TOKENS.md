# GitHub Migration - API Tokens Reference
# CONFIDENTIAL - DO NOT COMMIT TO GIT
# Last Updated: 2026-06-03

## [SECURE] API Tokens for antigravityos187-sketch Account

### Codacy
- **Token**: `REDACTED`
- **Account**: antigravityos187-sketch
- **Purpose**: Static code analysis
- **Scope**: Repository access
- **Get From**: https://app.codacy.com/account/apiTokens

### CodeScene
- **Token**: `REDACTED`
- **Account**: antigravityos187-sketch
- **Purpose**: Hotspot analysis and code health
- **Scope**: Repository access
- **Get From**: https://codescene.io/users/me/pat

### SonarCloud
- **Token**: `REDACTED`
- **Account**: antigravityos187-sketch
- **Purpose**: Code quality and security analysis
- **Scope**: Repository access
- **Get From**: https://sonarcloud.io/account/security
### GitHub Personal Access Token (PAT)
- **Token**: `ghp_YOUR_TOKEN_HERE`
- **Account**: antigravityos187-sketch
- **Note**: "Bob new new"
- **Purpose**: GitHub CLI authentication for PR creation and repository management
- **Scope**: Full access (repo, workflow, admin:org, and all other scopes)
- **Get From**: https://github.com/settings/tokens
- **Created**: 2026-06-04
- **Expires**: Never
- **Status**: [OK] Active

**Usage**:
```powershell
# RECOMMENDED: Set persistently at User level (survives terminal restarts)
[System.Environment]::SetEnvironmentVariable('GITHUB_TOKEN', 'ghp_YOUR_TOKEN_HERE', 'User')

# NOT RECOMMENDED: Set for current session only (lost when terminal closes)
$env:GITHUB_TOKEN = "ghp_YOUR_TOKEN_HERE"

# Verify authentication
gh auth status --show-token

# Verify token persistence (should return the PAT, not null)
[System.Environment]::GetEnvironmentVariable('GITHUB_TOKEN', 'User')
```

**Troubleshooting**:

**Problem**: `gh auth status` shows wrong account (e.g., backtothefutures83-oss instead of antigravityos187-sketch)

**Cause**: Token was set at Process level only, not User level. Process-level tokens are lost when terminal closes.

**Fix**:
```powershell
# 1. Set token at User level
[System.Environment]::SetEnvironmentVariable('GITHUB_TOKEN', 'ghp_YOUR_TOKEN_HERE', 'User')

# 2. Close and reopen terminal (required for User-level changes to take effect)

# 3. Verify correct account
gh auth status
# Should show: Logged in to github.com account antigravityos187-sketch

# 4. Verify token is at User level
[System.Environment]::GetEnvironmentVariable('GITHUB_TOKEN', 'User')
# Should return: ghp_YOUR_TOKEN_HERE
```

**Problem**: Token not persisting across terminal restarts

**Cause**: Token set at Process level instead of User level

**Diagnosis**:
```powershell
# Check which level token is set at
[System.Environment]::GetEnvironmentVariable('GITHUB_TOKEN', 'Process')  # Current session only
[System.Environment]::GetEnvironmentVariable('GITHUB_TOKEN', 'User')     # Persistent
[System.Environment]::GetEnvironmentVariable('GITHUB_TOKEN', 'Machine')  # All users (not recommended)
```

**Fix**: Set at User level as shown above.

**Problem**: `gh auth login` fails after setting token

**Cause**: Token might be cached at Process level from previous session

**Fix**:
```powershell
# 1. Clear Process-level token
Remove-Item Env:\GITHUB_TOKEN

# 2. Set at User level
[System.Environment]::SetEnvironmentVariable('GITHUB_TOKEN', 'ghp_YOUR_TOKEN_HERE', 'User')

# 3. Close and reopen terminal

# 4. Login
gh auth login --with-token
```


---

## [LIST] GitHub Actions Secrets Setup

**Repository**: https://github.com/antigravityos187-sketch/universal-or-strategy

**Set at**: Settings > Secrets and variables > Actions > New repository secret

| Secret Name | Value | Service |
|-------------|-------|---------|
| `CODACY_API_TOKEN` | `REDACTED` | Codacy |
| `CS_ACCESS_TOKEN` | `REDACTED` | CodeScene |
| `SONAR_TOKEN` | `REDACTED` | SonarCloud |

---

## [SYNC] Migration Reuse Notes

**For future migrations:**
1. These tokens are account-specific (antigravityos187-sketch)
2. They work across multiple repositories under the same account
3. No need to regenerate unless:
   - Token expires
   - Account changes
   - Security breach

**Reuse Strategy:**
- [OK] Same account, new repo -> Reuse all tokens
- [!] New account, new repo -> Generate new tokens
- [X] Security incident -> Revoke and regenerate immediately

---

## [SHIELD] Security Notes

- **NEVER commit this file to git** (already in .gitignore as `docs/brain/GITHUB_MIGRATION_TOKENS.md`)
- Store backup copy in password manager (1Password, LastPass, etc.)
- Rotate tokens every 90 days for security best practices
- Revoke old tokens after successful migration

---

## [DATE] Token Lifecycle

| Token | Created | Expires | Status |
|-------|---------|---------|--------|
| CODACY_API_TOKEN | 2026-06-03 | Never | [OK] Active |
| CS_ACCESS_TOKEN | 2026-06-03 | Never | [OK] Active |
| SONAR_TOKEN | 2026-06-03 | Never | [OK] Active |

---

## [LINK] Quick Access Links

- **Codacy Dashboard**: https://app.codacy.com/gh/antigravityos187-sketch/universal-or-strategy
- **CodeScene Dashboard**: https://codescene.io/projects/antigravityos187-sketch/universal-or-strategy
- **SonarCloud Dashboard**: https://sonarcloud.io/project/overview?id=antigravityos187-sketch_universal-or-strategy
- **GitHub Secrets**: https://github.com/antigravityos187-sketch/universal-or-strategy/settings/secrets/actions

---

*Generated during GitHub migration from malhitticrypto-debug to antigravityos187-sketch*
*Orchestrator: Gemini CLI (Advanced Mode)*
*Date: 2026-06-03T16:52:00Z*