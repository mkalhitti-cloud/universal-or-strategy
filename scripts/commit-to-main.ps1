# Commit Non-.cs Files to Main (While Preserving PR Work)
# Usage: .\scripts\commit-to-main.ps1 "commit message"
#
# Problem: You're mid-PR and edited non-.cs files (.bob/, docs/, configs/)
# Solution: This script safely commits them to main without losing PR work

param(
    [Parameter(Mandatory=$true)]
    [string]$CommitMessage
)

$ErrorActionPreference = "Stop"

Write-Host "=== COMMIT NON-.CS FILES TO MAIN ===" -ForegroundColor Cyan
Write-Host ""

# 1. Check current branch
$currentBranch = git branch --show-current
Write-Host "Current branch: $currentBranch" -ForegroundColor Yellow

if ($currentBranch -eq "main") {
    Write-Host "ERROR: Already on main. Just commit normally." -ForegroundColor Red
    exit 1
}

# 2. Check for non-.cs changes
$allChanges = git status --short
$nonCsChanges = $allChanges | Where-Object { $_ -notmatch '\.cs$' }

if (-not $nonCsChanges) {
    Write-Host "No non-.cs files to commit. Nothing to do." -ForegroundColor Green
    exit 0
}

Write-Host "Non-.cs files to commit:" -ForegroundColor Green
$nonCsChanges | ForEach-Object { Write-Host "  $_" }
Write-Host ""

# 3. Stash ALL changes (including .cs files)
Write-Host "Stashing all changes..." -ForegroundColor Yellow
git stash push -u -m "temp-stash-for-main-commit"

# 4. Switch to main
Write-Host "Switching to main..." -ForegroundColor Yellow
git checkout main

# 5. Pop stash
Write-Host "Restoring changes..." -ForegroundColor Yellow
git stash pop

# 6. Stage ONLY non-.cs files
Write-Host "Staging non-.cs files..." -ForegroundColor Yellow
git add .
git reset HEAD -- '*.cs'

# 7. Show what will be committed
Write-Host ""
Write-Host "Files to commit to main:" -ForegroundColor Green
git diff --cached --name-only

# 8. Commit
Write-Host ""
Write-Host "Committing to main..." -ForegroundColor Yellow
git commit -m "$CommitMessage"

# 9. Push to main
Write-Host "Pushing to main..." -ForegroundColor Yellow
git push origin main

# 10. Switch back to PR branch
Write-Host "Switching back to $currentBranch..." -ForegroundColor Yellow
git checkout $currentBranch

# 11. Restore .cs files from stash (if any were stashed)
$csFiles = git diff stash@{0} --name-only | Where-Object { $_ -match '\.cs$' }
if ($csFiles) {
    Write-Host "Restoring .cs files to PR branch..." -ForegroundColor Yellow
    git checkout stash@{0} -- $csFiles
    git stash drop
}

# 11.5. CRITICAL: Restore .bob/ infrastructure from main
Write-Host "Restoring .bob/ infrastructure from main..." -ForegroundColor Yellow
git checkout main -- .bob/

# 12. MANDATORY: Merge main into PR branch (prevents divergence)
Write-Host ""
Write-Host "CRITICAL: Merging main into $currentBranch..." -ForegroundColor Yellow
Write-Host "This is REQUIRED to prevent branch divergence." -ForegroundColor Yellow
Write-Host ""

git merge main --no-edit

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Merge successful!" -ForegroundColor Green
    Write-Host ""
    Write-Host "✓ COMPLETE SUCCESS!" -ForegroundColor Green
    Write-Host "  - Non-.cs files committed to main" -ForegroundColor Green
    Write-Host "  - PR branch synced with main" -ForegroundColor Green
    Write-Host "  - .cs changes intact" -ForegroundColor Green
    Write-Host "  - No branch divergence" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "⚠ MERGE CONFLICTS DETECTED" -ForegroundColor Red
    Write-Host ""
    Write-Host "This is expected if main and PR both modified the same files." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "REQUIRED STEPS:" -ForegroundColor Yellow
    Write-Host "  1. Resolve conflicts in VS Code" -ForegroundColor White
    Write-Host "  2. git add ." -ForegroundColor White
    Write-Host "  3. git commit" -ForegroundColor White
    Write-Host ""
    Write-Host "After resolving, the pre-commit hook will allow commits." -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

# Made with Bob
