# Backup Bob IDE Settings and Conversations
# Purpose: Preserve Bob IDE configuration before version downgrade
# Usage: powershell -File .\scripts\backup_bob_ide_settings.ps1

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupDir = ".\backups\bob_ide_$timestamp"

Write-Host "Backing up Bob IDE settings and conversations..." -ForegroundColor Cyan
Write-Host ""

# Create backup directory
New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
Write-Host "Created backup directory: $backupDir" -ForegroundColor Green

# Bob IDE settings locations
$bobLocations = @{
    "AppData" = "$env:APPDATA\IBM Bob"
    "LocalAppData" = "$env:LOCALAPPDATA\IBM Bob"
    "UserProfile" = "$env:USERPROFILE\.bob"
}

# Backup each location
foreach ($location in $bobLocations.GetEnumerator()) {
    $sourcePath = $location.Value
    $destPath = Join-Path $backupDir $location.Key
    
    if (Test-Path $sourcePath) {
        Write-Host "Backing up $($location.Key): $sourcePath" -ForegroundColor Yellow
        Copy-Item -Path $sourcePath -Destination $destPath -Recurse -Force
        Write-Host "  Backed up successfully" -ForegroundColor Green
    } else {
        Write-Host "  Skipped (not found)" -ForegroundColor Gray
    }
}

# Backup workspace-specific Bob settings
$workspaceSettings = @(
    ".\.bob",
    ".\bob.config.yaml",
    ".\.vscode\settings.json"
)

Write-Host ""
Write-Host "Backing up workspace settings..." -ForegroundColor Yellow
foreach ($setting in $workspaceSettings) {
    if (Test-Path $setting) {
        $destPath = Join-Path $backupDir "workspace"
        New-Item -ItemType Directory -Path $destPath -Force | Out-Null
        Copy-Item -Path $setting -Destination $destPath -Recurse -Force
        Write-Host "  Backed up: $setting" -ForegroundColor Green
    }
}

# Create restore script
$restoreScript = @"
# Restore Bob IDE Settings
# Usage: powershell -File restore_bob_ide_settings.ps1

Write-Host "Restoring Bob IDE settings from backup..." -ForegroundColor Cyan
Write-Host ""

# Restore AppData
if (Test-Path ".\AppData") {
    Copy-Item -Path ".\AppData\*" -Destination "`$env:APPDATA\IBM Bob" -Recurse -Force
    Write-Host "Restored AppData settings" -ForegroundColor Green
}

# Restore LocalAppData
if (Test-Path ".\LocalAppData") {
    Copy-Item -Path ".\LocalAppData\*" -Destination "`$env:LOCALAPPDATA\IBM Bob" -Recurse -Force
    Write-Host "Restored LocalAppData settings" -ForegroundColor Green
}

# Restore UserProfile
if (Test-Path ".\UserProfile") {
    Copy-Item -Path ".\UserProfile\*" -Destination "`$env:USERPROFILE\.bob" -Recurse -Force
    Write-Host "Restored UserProfile settings" -ForegroundColor Green
}

# Restore workspace settings
if (Test-Path ".\workspace") {
    Copy-Item -Path ".\workspace\*" -Destination "..\.." -Recurse -Force
    Write-Host "Restored workspace settings" -ForegroundColor Green
}

Write-Host ""
Write-Host "Restore complete!" -ForegroundColor Green
"@

$restoreScriptPath = Join-Path $backupDir "restore_bob_ide_settings.ps1"
$restoreScript | Out-File -FilePath $restoreScriptPath -Encoding UTF8

Write-Host ""
Write-Host "Backup complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Backup location: $backupDir" -ForegroundColor Cyan
Write-Host "Restore script: $restoreScriptPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "To restore later, run:" -ForegroundColor Yellow
Write-Host "  cd $backupDir" -ForegroundColor White
Write-Host "  .\restore_bob_ide_settings.ps1" -ForegroundColor White

# Made with Bob
