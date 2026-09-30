# Create Auto-Approval Settings for Worktrees
# V12 Photon Kernel - Parallel Epic Execution
# Created: 2026-06-09

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Worktree Auto-Approval Setup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Configuration
$worktrees = @(
    "C:\WSGTA\universal-or-epic-cluster-1",
    "C:\WSGTA\universal-or-epic-cluster-2",
    "C:\WSGTA\universal-or-epic-cluster-3"
)

$settingsContent = @{
    autoApprove = $true
    autoApproveTools = @(
        "execute_command",
        "write_to_file",
        "apply_diff",
        "insert_content",
        "read_file",
        "list_files",
        "search_files"
    )
}

foreach ($worktree in $worktrees) {
    Write-Host "Processing: $worktree" -ForegroundColor Yellow
    
    # Verify worktree exists
    if (-not (Test-Path $worktree)) {
        Write-Host "  Worktree not found, skipping..." -ForegroundColor Red
        continue
    }
    
    # Create .bob directory if it doesn't exist
    $bobDir = Join-Path $worktree ".bob"
    if (-not (Test-Path $bobDir)) {
        New-Item -ItemType Directory -Path $bobDir -Force | Out-Null
        Write-Host "  Created .bob directory" -ForegroundColor Green
    }
    
    # Create settings.json
    $settingsPath = Join-Path $bobDir "settings.json"
    $settingsContent | ConvertTo-Json -Depth 10 | Set-Content $settingsPath -Encoding UTF8
    Write-Host "  Created settings.json with auto-approval enabled" -ForegroundColor Green
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Setup Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Auto-approval enabled in all worktrees." -ForegroundColor Green
Write-Host "Restart Bob CLI sessions to load new settings." -ForegroundColor Yellow
Write-Host ""

# Made with Bob
