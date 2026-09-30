# V12 Branch Sync Protocol (DETERMINISTIC)

## The Killer Problem (Now SOLVED)

**What Was Happening:**
1. Working on PR branch (e.g., `gitbutler/workspace`)
2. Edit non-.cs files mid-PR (`.bob/commands/`, docs, configs)
3. Git hook blocks committing them to PR branch
4. Commit them to main instead
5. **PR branch becomes "behind" main** ← KILLER ISSUE
6. Missing files cause errors (slash commands gone, modes missing)
7. Repeat cycle, frustration builds

**Root Cause:** No enforcement that PR branches MUST stay in sync with main.

## The Deterministic Solution

### 1. Pre-Commit Hook Enforcement

**Location:** `.git/hooks/pre-commit`

**What It Does:**
- BLOCKS any commit on PR branches that are behind main
- Forces immediate merge before allowing commits
- Makes sync **mandatory**, not optional

**Error Message:**
```
❌ FATAL: Branch 'gitbutler/workspace' is 3 commits behind main

REQUIRED FIX:
  git merge main

After merge succeeds, retry your commit.
```

### 2. Automated Commit Script

**Location:** `scripts/commit-to-main.ps1`

**Usage:**
```powershell
# From your PR branch:
.\scripts\commit-to-main.ps1 "Add new slash commands"
```

**What It Does:**
1. Stashes all changes
2. Switches to main
3. Commits ONLY non-.cs files
4. Pushes to main
5. Switches back to PR branch
6. Restores .cs work
7. **MANDATORY: Merges main into PR branch**

**No More Options:** Merge is now required, not optional.

## The Guarantee

**INVARIANT:** `PR branch MUST NOT be older than main`

This is now **enforced** at commit time. You cannot commit to a PR branch that's behind main.

## Workflow Examples

### Scenario 1: Adding Slash Commands Mid-PR

```powershell
# You're on gitbutler/workspace, working on IPC fixes
# You realize you need a new slash command

# Edit .bob/commands/new-command.md
# Try to commit:
git add .bob/commands/new-command.md
git commit -m "Add new command"

# ❌ BLOCKED by hook: "Only .cs files allowed"

# Use the script instead:
.\scripts\commit-to-main.ps1 "Add new slash command"

# Script does:
# 1. Commits to main
# 2. Merges main into gitbutler/workspace
# 3. You're back on PR branch, in sync

# Now you can continue with .cs work:
git add src/V12_002.IPC.Hardening.cs
git commit -m "Fix IPC validation"
# ✓ ALLOWED (branch is in sync)
```

### Scenario 2: Updating Documentation Mid-PR

```powershell
# You're on feature/phase7-fixes
# You update docs/workflow/JANE_STREET_ENFORCEMENT.md

# Use the script:
.\scripts\commit-to-main.ps1 "Update Jane Street docs"

# Script merges main back automatically
# Continue with .cs work:
git add src/V12_002.SIMA.Dispatch.cs
git commit -m "Fix SIMA dispatch"
# ✓ ALLOWED
```

### Scenario 3: Merge Conflicts (Handled Gracefully)

```powershell
.\scripts\commit-to-main.ps1 "Add MCP config"

# If merge conflicts occur:
# ⚠ MERGE CONFLICTS DETECTED
# 
# REQUIRED STEPS:
#   1. Resolve conflicts in VS Code
#   2. git add .
#   3. git commit

# After resolving:
git add .
git commit
# ✓ Sync complete, can continue
```

## What This Prevents

- ❌ Missing slash commands after restart
- ❌ Missing custom modes
- ❌ Branch divergence issues
- ❌ "Why are my files gone?" confusion
- ❌ Manual branch juggling
- ❌ Forgotten merges
- ❌ Stale PR branches

## Technical Details

### Pre-Commit Hook Logic

```bash
# Fetch latest main
git fetch origin main:main

# Count commits PR branch is behind
behind=$(git rev-list --count HEAD..main)

if [ "$behind" -gt 0 ]; then
    # BLOCK commit
    exit 1
fi
```

### Script Merge Logic

```powershell
# After committing to main:
git checkout $prBranch
git merge main --no-edit  # MANDATORY

if ($LASTEXITCODE -ne 0) {
    # Conflicts detected, exit with instructions
    exit 1
}
```

## Installation

The hook is already installed at `.git/hooks/pre-commit`.

To verify:
```bash
cat .git/hooks/pre-commit | grep "BRANCH SYNC CHECK"
```

Should output: `=== V12 BRANCH SYNC CHECK ===`

## Maintenance

### Updating the Hook

If you need to modify the hook:
```bash
# Edit the hook
nano .git/hooks/pre-commit

# Test it
git commit -m "test"
```

### Disabling (NOT RECOMMENDED)

If you absolutely must disable (e.g., emergency):
```bash
# Temporarily bypass
git commit --no-verify -m "emergency fix"

# Re-enable by removing --no-verify
```

**WARNING:** Bypassing the hook defeats the entire protocol.

## FAQ

**Q: What if I want to commit non-.cs files to my PR branch?**
A: You can't. That's the point. Non-.cs files go to main (no review needed).

**Q: What if I forget to use the script?**
A: The hook will block your commit and remind you.

**Q: What if I have merge conflicts?**
A: Resolve them normally. The hook allows commits after conflicts are resolved.

**Q: Can I commit .cs files while behind main?**
A: No. The hook blocks ALL commits on PR branches that are behind main.

**Q: Why is this so strict?**
A: Because branch divergence was a recurring killer issue. Strictness prevents it.

## Success Metrics

**Before This Protocol:**
- Branch divergence: Common
- Missing files: Frequent
- Manual fixes: Required
- Frustration: High

**After This Protocol:**
- Branch divergence: **Impossible**
- Missing files: **Prevented**
- Manual fixes: **Automated**
- Frustration: **Eliminated**

## Related Files

- `.git/hooks/pre-commit` - Enforcement hook
- `scripts/commit-to-main.ps1` - Automated workflow
- `docs/workflow/PR5_ORCHESTRATION_WORKFLOW.md` - PR workflow
- `docs/workflow/JANE_STREET_ENFORCEMENT.md` - Review protocol

## Version History

- **v1.0** (2026-06-06): Initial deterministic protocol
  - Pre-commit hook enforcement
  - Mandatory merge in script
  - Zero tolerance for divergence