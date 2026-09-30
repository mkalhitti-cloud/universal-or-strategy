# scripts/git_src_only.ps1
# V12.19 Src-Only Push Helper
# Automates the src-only staging and commit workflow

param(
    [Parameter(Mandatory=$true)]
    [string]$Message,
    
    [Parameter(Mandatory=$false)]
    [switch]$Push,
    
    [Parameter(Mandatory=$false)]
    [string]$Branch
)

$ErrorActionPreference = "Stop"

Write-Host "`n--- V12.19 SRC-ONLY PUSH HELPER ---" -ForegroundColor Cyan

# 1. Verify we're in the repo root
if (!(Test-Path "src")) {
    Write-Host "ERROR: Must run from repository root (src/ directory not found)" -ForegroundColor Red
    exit 1
}

# 2. Check for staged non-src files
Write-Host "[1/5] Checking for non-src staged files..." -NoNewline
$stagedFiles = git diff --cached --name-only
$nonSrcStaged = $stagedFiles | Where-Object { $_ -notmatch "^src/" }

if ($nonSrcStaged) {
    Write-Host " WARNING" -ForegroundColor Yellow
    Write-Host "`nNon-src files are currently staged:" -ForegroundColor Yellow
    $nonSrcStaged | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
    Write-Host "`nThese will be UNSTAGED. Continue? (Y/N): " -NoNewline -ForegroundColor Yellow
    $response = Read-Host
    if ($response -ne "Y" -and $response -ne "y") {
        Write-Host "Aborted by user." -ForegroundColor Red
        exit 1
    }
    # Unstage non-src files
    $nonSrcStaged | ForEach-Object { git reset HEAD $_ }
    Write-Host "Non-src files unstaged." -ForegroundColor Green
} else {
    Write-Host " PASS" -ForegroundColor Green
}

# 3. Stage src/ files
Write-Host "[2/5] Staging src/ files..." -NoNewline
git add src/
$srcStaged = git diff --cached --name-only | Where-Object { $_ -match "^src/" }
if (!$srcStaged) {
    Write-Host " FAIL" -ForegroundColor Red
    Write-Host "ERROR: No src/ files to commit. Make changes to src/ first." -ForegroundColor Red
    exit 1
}
Write-Host " PASS ($($srcStaged.Count) files)" -ForegroundColor Green

# 4. Run PR hygiene check
Write-Host "[3/5] Running PR hygiene check..." -NoNewline
try {
    & "$PSScriptRoot\verify_pr_hygiene.ps1" | Out-Null
    Write-Host " PASS" -ForegroundColor Green
} catch {
    Write-Host " FAIL" -ForegroundColor Red
    Write-Host "ERROR: PR hygiene check failed. Fix issues and try again." -ForegroundColor Red
    exit 1
}

# 5. Commit
Write-Host "[4/5] Committing src/ changes..." -NoNewline
try {
    git commit -m $Message | Out-Null
    Write-Host " PASS" -ForegroundColor Green
} catch {
    Write-Host " FAIL" -ForegroundColor Red
    Write-Host "ERROR: Commit failed. Check git status." -ForegroundColor Red
    exit 1
}

# 6. Push (optional)
if ($Push) {
    Write-Host "[5/5] Pushing to remote..." -NoNewline
    
    # Determine branch
    if (!$Branch) {
        $Branch = git rev-parse --abbrev-ref HEAD
    }
    
    try {
        git push origin $Branch | Out-Null
        Write-Host " PASS" -ForegroundColor Green
        Write-Host "`nSUCCESS: Src-only changes pushed to origin/$Branch" -ForegroundColor Green
    } catch {
        Write-Host " FAIL" -ForegroundColor Red
        Write-Host "ERROR: Push failed. Check network and branch permissions." -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "[5/5] Push skipped (use -Push flag to push automatically)" -ForegroundColor Gray
    Write-Host "`nSUCCESS: Src-only changes committed locally." -ForegroundColor Green
    Write-Host "To push: git push origin $(git rev-parse --abbrev-ref HEAD)" -ForegroundColor Cyan
}

Write-Host "`nFiles committed:" -ForegroundColor Cyan
$srcStaged | ForEach-Object { Write-Host "  + $_" -ForegroundColor Green }

exit 0

# Made with Bob
