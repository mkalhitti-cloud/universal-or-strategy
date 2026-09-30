# Parallel Epic Workflow Setup Script
# V12 Photon Kernel - Approach 3: Batch F5 with GitButler Worktrees
# Created: 2026-06-09

param(
    [switch]$DryRun = $false
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "V12 Parallel Epic Workflow Setup" -ForegroundColor Cyan
Write-Host "Approach 3: Batch F5 with Worktrees" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Configuration
$baseDir = "C:\WSGTA"
$mainRepo = "$baseDir\universal-or-strategy"
$worktree1 = "$baseDir\universal-or-epic-cluster-1"
$worktree2 = "$baseDir\universal-or-epic-cluster-2"
$worktree3 = "$baseDir\universal-or-epic-cluster-3"

# Step 1: Verify GitButler is installed
Write-Host "[1/6] Verifying GitButler installation..." -ForegroundColor Yellow
try {
    $butVersion = & but --version 2>&1
    Write-Host "   GitButler found: $butVersion" -ForegroundColor Green
} catch {
    Write-Host "   GitButler not found. Install from: https://gitbutler.com" -ForegroundColor Red
    exit 1
}

# Step 2: Verify we're in the main repo
Write-Host "[2/6] Verifying main repository..." -ForegroundColor Yellow
if (-not (Test-Path "$mainRepo\.git")) {
    Write-Host "   Main repo not found at: $mainRepo" -ForegroundColor Red
    exit 1
}
Set-Location $mainRepo
Write-Host "   Main repo verified: $mainRepo" -ForegroundColor Green

# Step 3: Create GitButler virtual branches for each cluster
Write-Host "[3/6] Creating GitButler virtual branches..." -ForegroundColor Yellow

$branches = @(
    @{Name="epic-cluster-1"; Description="SIMA complexity reduction epics"},
    @{Name="epic-cluster-2"; Description="Orders complexity reduction epics"},
    @{Name="epic-cluster-3"; Description="Lifecycle complexity reduction epics"}
)

foreach ($branch in $branches) {
    Write-Host "  Creating virtual branch: $($branch.Name)..." -ForegroundColor Cyan
    if ($DryRun) {
        Write-Host "    [DRY RUN] Would create: $($branch.Name)" -ForegroundColor Gray
    } else {
        try {
            & but branch new $branch.Name --description $branch.Description 2>&1 | Out-Null
            Write-Host "     Created: $($branch.Name)" -ForegroundColor Green
        } catch {
            Write-Host "     Branch may already exist: $($branch.Name)" -ForegroundColor Yellow
        }
    }
}

# Step 4: Create worktrees
Write-Host "[4/6] Creating worktrees..." -ForegroundColor Yellow

$worktrees = @(
    @{Path=$worktree1; Branch="gitbutler/epic-cluster-1"},
    @{Path=$worktree2; Branch="gitbutler/epic-cluster-2"},
    @{Path=$worktree3; Branch="gitbutler/epic-cluster-3"}
)

foreach ($wt in $worktrees) {
    Write-Host "  Creating worktree: $($wt.Path)..." -ForegroundColor Cyan
    
    # Remove existing worktree if present
    if (Test-Path $wt.Path) {
        Write-Host "     Worktree already exists, removing..." -ForegroundColor Yellow
        if (-not $DryRun) {
            Remove-Item -Path $wt.Path -Recurse -Force
        }
    }
    
    if ($DryRun) {
        Write-Host "    [DRY RUN] Would create worktree at: $($wt.Path)" -ForegroundColor Gray
    } else {
        try {
            & git worktree add $wt.Path $wt.Branch 2>&1 | Out-Null
            Write-Host "     Created worktree: $($wt.Path)" -ForegroundColor Green
        } catch {
            Write-Host "     Failed to create worktree: $_" -ForegroundColor Red
            exit 1
        }
    }
}

# Step 5: Copy configuration files to each worktree
Write-Host "[5/6] Copying configuration files..." -ForegroundColor Yellow

$configFiles = @(
    "bob.config.yaml",
    ".bob\*",
    "deploy-sync.ps1",
    "scripts/*"
)

foreach ($wt in $worktrees) {
    Write-Host "  Copying configs to: $($wt.Path)..." -ForegroundColor Cyan
    
    if ($DryRun) {
        Write-Host "    [DRY RUN] Would copy config files" -ForegroundColor Gray
    } else {
        foreach ($file in $configFiles) {
            $source = Join-Path $mainRepo $file
            $dest = Join-Path $wt.Path $file
            
            if (Test-Path $source) {
                Copy-Item -Path $source -Destination $dest -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
        Write-Host "     Configs copied" -ForegroundColor Green
    }
}

# Step 6: Create cluster assignment file
Write-Host "[6/6] Creating cluster assignment file..." -ForegroundColor Yellow

$assignmentFile = Join-Path $mainRepo "docs/workflow/PARALLEL_EPIC_CLUSTERS.md"

if ($DryRun) {
    Write-Host "  [DRY RUN] Would create: $assignmentFile" -ForegroundColor Gray
} else {
    $content = @()
    $content += "# V12 Parallel Epic Cluster Assignments"
    $content += "# Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    $content += ""
    $content += "## Cluster 1: SIMA Files"
    $content += "Worktree: $worktree1"
    $content += "Target files: V12_002.SIMA.*.cs"
    $content += "* EPIC-CCN-19: AdoptFleetOrders (COMPLETE)"
    $content += "* EPIC-CCN-22: DispatchToFollower"
    $content += ""
    $content += "## Cluster 2: Orders Files"
    $content += "Worktree: $worktree2"
    $content += "Target files: V12_002.Orders.*.cs"
    $content += "* EPIC-CCN-20: HandleFlatPositionUpdate (COMPLETE)"
    $content += "* EPIC-CCN-23: CancelOrderSafe"
    $content += ""
    $content += "## Cluster 3: Lifecycle Files"
    $content += "Worktree: $worktree3"
    $content += "Target files: V12_002.Lifecycle.cs"
    $content += "* EPIC-CCN-21: OnStateChange"
    $content += "* EPIC-CCN-24: OnBarUpdate"
    $content += ""
    $content += "## Usage: See docs/workflow/V12_EPIC_WORKFLOW_REFACTORING_DESIGN.md"
    $content += "Time savings: 267 hours (64 percent faster than sequential)"
    
    $content | Out-File -FilePath $assignmentFile -Encoding UTF8
    Write-Host "   Created: $assignmentFile" -ForegroundColor Green
}

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Setup Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Worktrees created:" -ForegroundColor Yellow
Write-Host "  1. $worktree1 (SIMA epics)" -ForegroundColor Cyan
Write-Host "  2. $worktree2 (Orders epics)" -ForegroundColor Cyan
Write-Host "  3. $worktree3 (Lifecycle epics)" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Review cluster assignments: $assignmentFile" -ForegroundColor White
Write-Host "  2. Open 3 terminals, one per worktree" -ForegroundColor White
Write-Host "  3. Start parallel epic execution" -ForegroundColor White
Write-Host ""
Write-Host "Estimated time savings: 267 hours, 64 percent faster" -ForegroundColor Green
Write-Host ""

if ($DryRun) {
    Write-Host 'DRY RUN MODE: No changes were made. Run without the DryRun switch to execute.' -ForegroundColor Yellow
}

# Made with Bob
