# scripts/verify_pr_hygiene.ps1
# V12 Mandatory PR Hygiene Gate
# Enforces: 1) Clean Branch (from main), 2) Diff Size < 10,000 chars
# V12.18: Scope-aware rebase check (SRC-ONLY PRs skip rebase validation)

param(
    [int]$PrNumber = 0
)

$MaxDiffSize = 10000
$BaseBranch = "main"

Write-Host "--- V12 PR HYGIENE GATE ---" -ForegroundColor Cyan

# 0. SCOPE DETECTION (if PR number provided)
$scope = "UNKNOWN"
if ($PrNumber -gt 0) {
    try {
        $scopeResult = & "$PSScriptRoot\check_pr_scope.ps1" -PrNumber $PrNumber 2>&1
        if ($LASTEXITCODE -eq 0) {
            $scope = $scopeResult.Trim()
            Write-Host "[0/2] PR Scope: $scope" -ForegroundColor Cyan
        }
    } catch {
        Write-Host "[0/2] PR Scope: Detection failed, assuming MIXED" -ForegroundColor Yellow
        $scope = "MIXED"
    }
}

# 1. CLEAN BRANCH CHECK (scope-aware)
if ($scope -eq "SRC-ONLY") {
    Write-Host "[1/2] Clean Branch: SKIPPED (SRC-ONLY PR - rebase would contaminate)" -ForegroundColor Yellow
    Write-Host "      Rationale: Three-Tier Branch Model prevents non-src commits on src-only branches" -ForegroundColor Gray
} else {
    # Ensure main is fetched
    git fetch origin $BaseBranch --quiet

    $mergeBase = git merge-base HEAD origin/$BaseBranch
    $mainTip = git rev-parse origin/$BaseBranch

    if ($mergeBase -ne $mainTip) {
        # If the merge base isn't the tip of main, check if main is a direct ancestor
        $isAncestor = git merge-base --is-ancestor $BaseBranch HEAD
        if (!$isAncestor) {
            Write-Host "FAIL: Branch is NOT based on the latest main." -ForegroundColor Red
            Write-Host "ACTION: Please rebase onto main using:" -ForegroundColor Yellow
            Write-Host "  git fetch origin main && git rebase origin/main" -ForegroundColor White
            exit 1
        }
    }
    Write-Host "[1/2] Clean Branch: PASS" -ForegroundColor Green
}

# 2. DIFF SIZE CHECK (src/ only)
# Use git diff --shortstat to get the raw numbers
$diffStats = git diff $BaseBranch..HEAD --shortstat -- src/
Write-Host "[2/2] Diff Size Check (src/):" -NoNewline

if ([string]::IsNullOrEmpty($diffStats)) {
    Write-Host " 0 lines (PASS)" -ForegroundColor Green
} else {
    # Extract insertions and deletions from shortstat output
    # Example: " 4 files changed, 10 insertions(+), 4 deletions(-)"
    $matches = [regex]::Matches($diffStats, "(\d+) insertions\(\+\), (\d+) deletions\(-\)")
    if ($matches.Count -eq 1) {
        $insertions = [int]$matches[0].Groups[1].Value
        $deletions = [int]$matches[0].Groups[2].Value
        $totalChanges = $insertions + $deletions
        
        # We estimate chars based on average line length (~40 chars)
        $estimatedChars = $totalChanges * 40
        
        if ($estimatedChars -gt $MaxDiffSize) {
            Write-Host " FAIL (~$estimatedChars chars, Limit: $MaxDiffSize)" -ForegroundColor Red
            Write-Host "ERROR: PR exceeds 10k character limit. Current estimated size: $estimatedChars" -ForegroundColor Red
            Write-Host "Please split the work into smaller commits/PRs." -ForegroundColor Yellow
            exit 1
        }
        Write-Host " PASS (~$estimatedChars chars)" -ForegroundColor Green
    } else {
         # Fallback to direct diff string length if regex fails
         $diff = git diff $BaseBranch..HEAD -- src/
         $diffSize = $diff.Length
         if ($diffSize -gt $MaxDiffSize) {
            Write-Host " FAIL ($diffSize chars, Limit: $MaxDiffSize)" -ForegroundColor Red
            exit 1
         }
         Write-Host " PASS ($diffSize chars)" -ForegroundColor Green
    }
}

Write-Host "`nHYGIENE GATES PASSED. Ready to push." -ForegroundColor Green
exit 0
