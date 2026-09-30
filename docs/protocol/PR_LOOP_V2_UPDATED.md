# PR-LOOP V2 Protocol (Updated for Three-Tier Branch Model)

**Version**: 2.1  
**Date**: 2026-05-29  
**Status**: ACTIVE

## Overview

The PR-LOOP V2 Protocol is an iterative quality improvement loop that drives Pull Requests to a Perfect Health Score (PHS) of 100/100. This updated version ensures compliance with the Three-Tier Branch Model by preventing forensics artifacts from contaminating src-only PRs.

## Critical Update (V2.1)

**Problem Identified**: The original PR-LOOP V2 Protocol violated the Three-Tier Branch Model by committing forensics files (`docs/brain/*.md`) alongside source code changes in src-only PRs.

**Solution**: Added `-NoCommit` flag to `extract_pr_forensics.ps1` that outputs forensics to `.forensics/` (gitignored) instead of `docs/brain/`.

## Protocol Steps

### Step 0: Branch Type Detection

**BEFORE starting PR-LOOP**, determine the PR type:

```powershell
# Check if PR modifies src/ files
$srcFiles = git diff --name-only origin/main | Where-Object { $_ -match '^src/' }

if ($srcFiles.Count -gt 0) {
    $PrType = "src-only"
    $ForensicsMode = "-NoCommit"
} else {
    $PrType = "non-src"
    $ForensicsMode = ""
}

Write-Host "PR Type: $PrType" -ForegroundColor Cyan
Write-Host "Forensics Mode: $ForensicsMode" -ForegroundColor Yellow
```

### Step 1: Bot Forensics Extraction (HARDENED)

**CRITICAL**: The forensics script generates TWO files:
1. `pr_<N>_forensics.md` - Summary with truncated excerpts (200 chars max)
2. `docs/pr<N>comments.md` - FULL export (may be 1000+ lines)

**You MUST read BOTH files before categorization.**

**For src-only PRs** (use `-NoCommit`):
```powershell
powershell -File .\scripts\extract_pr_forensics.ps1 -PrNumber <PR#> -NoCommit
```

**For non-src PRs** (commit forensics):
```powershell
powershell -File .\scripts\extract_pr_forensics.ps1 -PrNumber <PR#>
```

**Output Locations**:
- **With `-NoCommit`**: `.forensics/pr_<N>_forensics.md` (gitignored)
- **Without `-NoCommit`**: `docs/brain/pr_<N>_forensics.md` (committed)
- **ALWAYS**: `docs/pr<N>comments.md` (full export, must be stashed for src-only PRs)

### Step 1A: Complete Comment Extraction (MANDATORY)

**Large File Strategy** (for files >500 lines):

```
1. Check file size: wc -l docs/pr<N>comments.md
2. If >500 lines, read in 300-line chunks:
   - Lines 1-300 (headers + first bot)
   - Lines 300-600 (second bot)
   - Lines 600-900 (third bot)
   - Continue until EOF
3. Extract ALL inline comments with line numbers
4. Cross-validate against Codacy Issues tab screenshot
```

**Verification Checklist** (ALL must be YES before proceeding):
- [ ] Read full `docs/pr<N>comments.md` file (not just forensics.md)
- [ ] Found all inline comments mentioned in bot summaries
- [ ] Extracted specific line numbers and code snippets
- [ ] Cross-checked Codacy Issues tab screenshot
- [ ] Verified no truncated comments

**If ANY is NO → HALT and complete extraction**

**Confidence Score**:
- **100%**: Read full comments.md + Codacy screenshot + all inline comments extracted
- **75%**: Read forensics.md + partial comments.md
- **50%**: Read forensics.md only
- **<50%**: INCOMPLETE - must restart Step 1

### Step 2: Jane Street Audit + Categorization

Read forensics report and categorize ALL findings:

- **[VALID-FIX]**: Real issues requiring code changes
- **[VALID-SUPPRESS]**: Real issues but architecturally justified (document in `JANE_STREET_DEVIATIONS.md`)
- **[HALLUCINATION]**: False positives from bots
- **[INFRA-NOISE]**: Infrastructure/tooling issues

**MANDATORY READING ORDER**:
1. Read `pr_<N>_forensics.md` (summary)
2. Read `docs/pr<N>comments.md` (FULL details) using large file strategy
3. Screenshot Codacy Issues tab and cross-check
4. Validate each issue against `docs/standards/JANE_STREET_DEVIATIONS.md`
5. Complete verification checklist (Step 1A)
6. Present categorization with confidence score

**For src-only PRs**: Read from `.forensics/pr_<N>_forensics.md` + `docs/pr<N>comments.md`
**For non-src PRs**: Read from `docs/brain/pr_<N>_forensics.md` + `docs/pr<N>comments.md`

### Step 3: Local Repair

**Apply VALID-FIX issues** (priority order: P0 → P1 → P2):

```powershell
# Fix issues in src/ files
# ...

# Run pre-push validation
powershell -File .\scripts\pre_push_validation.ps1
```

**Document VALID-SUPPRESS issues**:
- Add to `docs/standards/JANE_STREET_DEVIATIONS.md`
- Reference specific Jane Street principles
- Provide architectural justification

**CRITICAL for src-only PRs**:
- **ONLY commit `.cs` files** (no `docs/`, no `.md` files)
- **DO NOT commit forensics** (they're in `.forensics/` which is gitignored)
- **DO NOT commit Jane Street Deviations updates** (separate docs-only PR)

### Step 4: Stash Non-.cs Files & Push

**For src-only PRs**, stash any non-.cs changes before pushing:

```powershell
# Check for non-.cs staged changes
$nonCsFiles = git diff --cached --name-only | Where-Object { $_ -notmatch '\.cs$' }

if ($nonCsFiles.Count -gt 0) {
    Write-Host "WARNING: Non-.cs files detected in staging area" -ForegroundColor Yellow
    Write-Host "Stashing: $($nonCsFiles -join ', ')" -ForegroundColor Gray
    
    # Unstage non-.cs files
    foreach ($file in $nonCsFiles) {
        git reset HEAD $file
    }
    
    # Stash them for later (docs-only PR)
    git stash push -m "PR #<N> non-src changes (for docs-only PR)" -- $nonCsFiles
}

# Verify only .cs files are staged
$stagedFiles = git diff --cached --name-only
Write-Host "Staged files: $($stagedFiles -join ', ')" -ForegroundColor Green

# Sync NT8 hard links
powershell -File .\deploy-sync.ps1

# Push changes (ONLY .cs files)
git push origin <branch>

# Wait 5 minutes for bot re-analysis
Start-Sleep -Seconds 300

# Re-extract forensics (same mode as Step 1)
powershell -File .\scripts\extract_pr_forensics.ps1 -PrNumber <PR#> $ForensicsMode
```

### Step 5: Calculate PHS

```powershell
# Calculate Perfect Health Score
powershell -File .\scripts\calculate_fleet_score.ps1 -PrNumber <PR#>
```

**If PHS < 100**: GOTO Step 1 (new iteration)  
**If PHS = 100**: Proceed to Step 6

### Step 6: F5 Verification

Wait for Director's F5 confirmation in NinjaTrader.

### Step 7: Post-Merge Forensics (src-only PRs only)

**AFTER merging src-only PR**, commit forensics to separate docs-only PR:

```powershell
# Create docs-only branch
git checkout main
git pull origin main
git checkout -b docs/pr-<N>-forensics

# Pop stashed non-src changes (if any)
git stash list | Select-String "PR #<N> non-src changes"
# If found: git stash pop stash@{<index>}

# Copy forensics from .forensics/ to docs/brain/
Copy-Item .forensics/pr_<N>_forensics.md docs/brain/
Copy-Item .forensics/pr_<N>_fix_queue.md docs/brain/

# Commit and push
git add docs/
git commit -m "docs: add PR #<N> forensics analysis and Jane Street deviations"
git push origin docs/pr-<N>-forensics

# Create PR (docs-only, no src/ changes)
gh pr create --title "docs: PR #<N> forensics analysis" --body "Post-merge forensics documentation"
```

## Three-Tier Branch Model Compliance

### Branch Types

1. **src/** branches: Source code changes only (`.cs`, `.csproj`)
2. **docs/** branches: Documentation changes only (`.md`, diagrams)
3. **infra/** branches: Infrastructure changes only (`.yml`, `.ps1`, `.json`)

### Enforcement

- **CI Check**: `Verify src/ vs non-src/ Separation` fails if mixed
- **Protocol Guard**: Blocks bot reviews until separation verified
- **Forensics**: `-NoCommit` flag prevents docs/ contamination in src/ PRs
- **Stash**: Non-.cs files automatically stashed before push

## Example: src-only PR Workflow

```powershell
# Step 0: Detect PR type
$PrType = "src-only"

# Step 1: Extract forensics (NoCommit mode)
powershell -File .\scripts\extract_pr_forensics.ps1 -PrNumber 5 -NoCommit
# Output: .forensics/pr_5_forensics.md (gitignored)

# Step 2: Read forensics and categorize
Get-Content .forensics/pr_5_forensics.md
# Categorize: 3 VALID-FIX, 2 VALID-SUPPRESS, 1 HALLUCINATION

# Step 3: Apply fixes (ONLY .cs files)
# Fix issue #1 in src/V12_002.SIMA.Flatten.cs
# Fix issue #2 in src/V12_002.Orders.Management.StopSync.cs
# Fix issue #3 in src/V12_002.Orders.Management.Flatten.cs

# Also updated docs/standards/JANE_STREET_DEVIATIONS.md (will be stashed)

# Pre-push validation
powershell -File .\scripts\pre_push_validation.ps1
# Result: 13/13 PASSED

# Step 4: Stash non-.cs files and push
git add src/*.cs docs/standards/JANE_STREET_DEVIATIONS.md

# Check staged files
$nonCsFiles = git diff --cached --name-only | Where-Object { $_ -notmatch '\.cs$' }
# Found: docs/standards/JANE_STREET_DEVIATIONS.md

# Unstage and stash
git reset HEAD docs/standards/JANE_STREET_DEVIATIONS.md
git stash push -m "PR #5 non-src changes (for docs-only PR)" -- docs/standards/JANE_STREET_DEVIATIONS.md

# Verify only .cs files staged
git diff --cached --name-only
# Output: src/V12_002.SIMA.Flatten.cs, src/V12_002.Orders.Management.StopSync.cs, src/V12_002.Orders.Management.Flatten.cs

# Push
git commit -m "fix: resolve SIMA flatten queue stall and stop sync emergency flatten"
powershell -File .\deploy-sync.ps1
git push origin fix/pr5-clean-cs-only

# Wait 5 minutes
Start-Sleep -Seconds 300

# Re-extract forensics
powershell -File .\scripts\extract_pr_forensics.ps1 -PrNumber 5 -NoCommit

# Step 5: Calculate PHS
powershell -File .\scripts\calculate_fleet_score.ps1 -PrNumber 5
# Result: PHS = 100/100

# Step 6: F5 Verification
# (Wait for Director confirmation)

# Step 7: Post-merge forensics (AFTER PR #5 merges)
git checkout main
git pull origin main
git checkout -b docs/pr-5-forensics

# Pop stashed changes
git stash pop stash@{0}
# Restored: docs/standards/JANE_STREET_DEVIATIONS.md

# Copy forensics
Copy-Item .forensics/pr_5_forensics.md docs/brain/
Copy-Item .forensics/pr_5_fix_queue.md docs/brain/

# Commit all docs changes
git add docs/
git commit -m "docs: add PR #5 forensics analysis and Jane Street deviations"
git push origin docs/pr-5-forensics
gh pr create --title "docs: PR #5 forensics analysis" --body "Post-merge forensics documentation"
```

## Troubleshooting

### Protocol Violation: "Verify src/ vs non-src/ Separation" Failed

**Symptom**: CI check fails with "Mixed src/ and non-src/ changes detected"

**Cause**: Non-.cs files committed alongside `.cs` files

**Fix**:
```powershell
# Check what's staged
git diff --cached --name-only

# Unstage non-.cs files
$nonCsFiles = git diff --cached --name-only | Where-Object { $_ -notmatch '\.cs$' }
foreach ($file in $nonCsFiles) {
    git reset HEAD $file
}

# Stash them
git stash push -m "PR #<N> non-src changes" -- $nonCsFiles

# Amend commit (if already committed)
git reset --soft HEAD~1
git add src/*.cs
git commit -m "fix: <description>"
git push --force origin <branch>
```

### Forensics Not Found

**Symptom**: Cannot find `.forensics/pr_<N>_forensics.md`

**Cause**: Forgot `-NoCommit` flag in Step 1

**Fix**:
```powershell
# Re-run with -NoCommit
powershell -File .\scripts\extract_pr_forensics.ps1 -PrNumber <N> -NoCommit
```

### Stashed Changes Lost

**Symptom**: Cannot find stashed non-src changes

**Fix**:
```powershell
# List all stashes
git stash list

# Find PR-specific stash
git stash list | Select-String "PR #<N>"

# Pop specific stash
git stash pop stash@{<index>}
```

## Version History

- **V2.0** (2026-05-20): Initial PR-LOOP V2 Protocol
- **V2.1** (2026-05-29): Added `-NoCommit` flag and stash workflow for Three-Tier Branch Model compliance

## References

- `docs/protocol/BRANCH_STRATEGY.md` - Three-Tier Branch Model
- `docs/standards/JANE_STREET_DEVIATIONS.md` - Architectural justifications
- `scripts/extract_pr_forensics.ps1` - Forensics extraction tool
- `scripts/pre_push_validation.ps1` - Local quality gates