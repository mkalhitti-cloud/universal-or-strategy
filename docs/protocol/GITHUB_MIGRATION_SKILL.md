# GitHub Migration Skill

**Purpose**: Automate GitHub account migrations with upfront decision-making  
**Version**: 1.0  
**Last Updated**: 2026-05-25

## Pre-Migration Decision (MANDATORY)

**BEFORE starting any migration, the agent MUST ask:**

```
GitHub Account Migration Detected

You have two options:

1. **PAT Approach** (RECOMMENDED for frequent migrations)
   - ✅ Fastest: 2 minutes setup, works forever
   - ✅ Keep fork structure (easier to sync upstream)
   - ✅ One-time token creation, reuse for all future migrations
   - ⚠️ Requires storing token securely

2. **Standalone Repo** (for permanent migrations)
   - ✅ Cleanest: No fork limitations
   - ✅ Full control over repository
   - ❌ Slower: Must recreate repo each time
   - ❌ Loses fork relationship (harder to sync upstream)

**Your migration frequency**: Every few days

**Recommendation**: Use PAT approach (Option 1)
- Create token once, store in `.env` or secure vault
- All future migrations work automatically
- No repo recreation needed

Which option do you prefer?
```

## Option 1: PAT Approach (For Frequent Migrations)

### Initial Setup (One-Time)

1. **Create PAT on new account**:
   ```
   https://github.com/settings/tokens/new?scopes=repo&description=Bob%20CLI%20Automation
   ```

2. **Store token securely**:
   ```powershell
   # Add to .env (gitignored)
   echo "GITHUB_PAT_NEW_ACCOUNT=ghp_xxxxxxxxxxxxx" >> .env
   
   # Or use Windows Credential Manager
   cmdkey /generic:github_pat_new_account /user:malhitticrypto-debug /pass:ghp_xxxxxxxxxxxxx
   ```

3. **Configure git credential helper**:
   ```powershell
   git config --global credential.helper "!gh auth git-credential"
   ```

### Every Migration (Automated)

```powershell
# Load PAT from .env
$env:GH_TOKEN = (Get-Content .env | Select-String "GITHUB_PAT_NEW_ACCOUNT").ToString().Split("=")[1]

# Push branch
git push <new-remote> <branch>

# Create PR (works automatically)
gh pr create --repo <new-account>/<repo> --head <branch> --base main --title "..." --body "..."
```

### Advantages for Frequent Migrations
- ✅ **Zero setup time** after initial token creation
- ✅ **Fork structure preserved** (easier to sync with upstream)
- ✅ **Automation works immediately** (no repo recreation)
- ✅ **GitHub Apps auto-work** (account-level, not repo-level)

## Option 2: Standalone Repo (For Permanent Migrations)

### Every Migration (Manual Steps)

1. **Delete fork**:
   ```powershell
   gh repo delete <new-account>/<repo> --yes
   ```

2. **Create fresh repo**:
   ```powershell
   gh repo create <new-account>/<repo> --private --description "..."
   ```

3. **Push all branches**:
   ```powershell
   git remote add new https://github.com/<new-account>/<repo>.git
   git push new --all
   git push new --tags
   ```

4. **Add secrets** (every time):
   - ANTHROPIC_API_KEY
   - OPENAI_API_KEY
   - GREPTILE_API_KEY
   - GOOGLE_API_KEY

### Disadvantages for Frequent Migrations
- ❌ **5-10 minutes per migration** (repo recreation + secrets)
- ❌ **Loses fork relationship** (can't sync upstream easily)
- ❌ **Manual secret management** (must re-add every time)

## Recommendation for Your Use Case

**Use PAT Approach (Option 1)** because:

1. **Frequency**: Migrating every few days means setup time compounds
   - PAT: 2 min initial + 0 min per migration = 2 min total
   - Standalone: 10 min per migration × 10 migrations = 100 min total

2. **Fork Benefits**: Keeping fork structure allows:
   - Easy upstream sync: `git fetch upstream && git merge upstream/main`
   - Contribution workflow: Can submit PRs back to original repo
   - Clear provenance: Shows relationship to original project

3. **Automation**: PAT enables full automation
   - No manual steps per migration
   - PR Loop V2 works out of the box
   - Bot checks trigger automatically

## Security Best Practices

### PAT Storage Options (Ranked by Security)

1. **Windows Credential Manager** (BEST):
   ```powershell
   cmdkey /generic:github_pat_new_account /user:malhitticrypto-debug /pass:ghp_xxxxxxxxxxxxx
   
   # Retrieve in scripts:
   $cred = cmdkey /list:github_pat_new_account
   ```

2. **Environment Variable** (GOOD):
   ```powershell
   # Add to .env (ensure .env is in .gitignore)
   GITHUB_PAT_NEW_ACCOUNT=ghp_xxxxxxxxxxxxx
   ```

3. **Azure Key Vault / 1Password** (ENTERPRISE):
   - Use for production environments
   - Requires additional setup

### PAT Rotation Schedule

- **Every 90 days**: Rotate PAT for security
- **Immediately**: If token is exposed or compromised
- **After migration complete**: If using temporary account

## Agent Automation Script

```powershell
# GitHub Migration Automation
# Usage: .\migrate-github-account.ps1 -NewAccount "malhitticrypto-debug" -Branch "feature-branch"

param(
    [Parameter(Mandatory=$true)]
    [string]$NewAccount,
    
    [Parameter(Mandatory=$true)]
    [string]$Branch,
    
    [string]$Repo = "universal-or-strategy"
)

# Check if PAT exists
$patEnvVar = "GITHUB_PAT_NEW_ACCOUNT"
if (-not (Test-Path env:$patEnvVar)) {
    Write-Host "PAT not found. Loading from .env..."
    $env:GH_TOKEN = (Get-Content .env | Select-String $patEnvVar).ToString().Split("=")[1]
}

# Verify authentication
$authStatus = gh auth status 2>&1
if ($authStatus -notmatch $NewAccount) {
    Write-Error "Not authenticated as $NewAccount. Run: gh auth login"
    exit 1
}

# Push branch
Write-Host "Pushing branch $Branch to $NewAccount/$Repo..."
git push $NewAccount $Branch

# Create PR
Write-Host "Creating PR..."
gh pr create --repo "$NewAccount/$Repo" --head $Branch --base main --title "[AUTO] $Branch" --body "Automated migration from PR Loop V2"

Write-Host "✅ Migration complete! PR created at: https://github.com/$NewAccount/$Repo/pulls"
```

## Merge Conflict Resolution

When merging PRs after account migration, conflicts may occur if PRs were created on the old account and main has advanced.

### Standard Resolution Process

1. **Set GitHub token** (if not already set):
   ```powershell
   $env:GITHUB_TOKEN = "github_pat_xxxxxxxxxxxxx"
   ```

2. **Verify authentication**:
   ```powershell
   gh auth status
   ```

3. **Attempt merge**:
   ```powershell
   gh pr merge <PR_NUMBER> --squash --delete-branch
   ```

4. **If merge conflicts occur**:
   ```powershell
   # Checkout the PR branch
   gh pr checkout <PR_NUMBER>
   
   # Fetch latest main
   git fetch origin main
   
   # Merge main into PR branch
   git merge origin/main
   ```

5. **Resolve conflicts** (choose appropriate strategy):
   
   **Option A: Accept incoming (main) changes** (when main has newer fixes):
   ```powershell
   git checkout --theirs <conflicted-file>
   git add <conflicted-file>
   ```
   
   **Option B: Keep current (PR) changes** (when PR has critical updates):
   ```powershell
   git checkout --ours <conflicted-file>
   git add <conflicted-file>
   ```
   
   **Option C: Manual resolution** (when both sides have important changes):
   - Open file in editor
   - Resolve conflict markers manually
   - `git add <conflicted-file>`

6. **Complete merge**:
   ```powershell
   git commit -m "Merge main into PR branch - resolve conflicts"
   git push
   ```

7. **Retry PR merge**:
   ```powershell
   gh pr merge <PR_NUMBER> --squash --delete-branch
   ```

### Conflict Resolution Decision Tree

```
Is the conflicted file in src/?
├─ YES → Was it modified in a previous merged PR?
│  ├─ YES → Use --theirs (accept main's version)
│  └─ NO → Manual review required
└─ NO → Is it a docs/brain/ file?
   ├─ YES → Use --ours (keep PR's version)
   └─ NO → Manual review required
```

### Common Conflict Scenarios

| Scenario | Strategy | Command |
|----------|----------|---------|
| PR #9 merged, PR #10 conflicts on same files | Accept main | `git checkout --theirs <file>` |
| Documentation updates conflict | Keep PR version | `git checkout --ours <file>` |
| Both PRs modify different parts of same file | Manual merge | Edit file manually |
| Protocol/config file conflicts | Manual review | Check both versions |

### Automation Script

```powershell
# Resolve PR conflicts automatically
# Usage: .\resolve-pr-conflicts.ps1 -PRNumber 10 -Strategy "theirs"

param(
    [Parameter(Mandatory=$true)]
    [int]$PRNumber,
    
    [ValidateSet("ours", "theirs", "manual")]
    [string]$Strategy = "manual"
)

# Checkout PR
gh pr checkout $PRNumber

# Fetch and merge main
git fetch origin main
$mergeResult = git merge origin/main 2>&1

if ($mergeResult -match "CONFLICT") {
    Write-Host "Conflicts detected. Applying strategy: $Strategy"
    
    # Get conflicted files
    $conflictedFiles = git diff --name-only --diff-filter=U
    
    foreach ($file in $conflictedFiles) {
        if ($Strategy -eq "theirs") {
            git checkout --theirs $file
            git add $file
            Write-Host "✅ Resolved $file (accepted main version)"
        }
        elseif ($Strategy -eq "ours") {
            git checkout --ours $file
            git add $file
            Write-Host "✅ Resolved $file (kept PR version)"
        }
        else {
            Write-Host "⚠️ Manual resolution required for: $file"
        }
    }
    
    if ($Strategy -ne "manual") {
        git commit -m "Merge main - resolve conflicts using $Strategy strategy"
        git push
        Write-Host "✅ Conflicts resolved and pushed"
    }
}
else {
    Write-Host "✅ No conflicts - merge successful"
}
```

## Troubleshooting

### PAT not working
**Symptom**: "must be a collaborator" error  
**Fix**: Verify PAT has `repo` scope: https://github.com/settings/tokens

### Token expired
**Symptom**: "Bad credentials" error  
**Fix**: Generate new PAT and update `.env`

### Wrong account authenticated
**Symptom**: PR created on old account  
**Fix**: 
```powershell
gh auth logout
gh auth login  # Login with new account
```

## Version History

- **1.0** (2026-05-25): Initial skill based on PR #11 migration experience
  - Added upfront decision prompt
  - Documented PAT approach for frequent migrations
  - Created automation script template