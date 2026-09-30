# cleanup_github_token.ps1
# Removes GITHUB_TOKEN from all possible locations
# Run this during GitHub account migration to prevent authentication conflicts

Write-Host "=== GitHub Token Cleanup ===" -ForegroundColor Cyan
Write-Host ""

$tokenFound = $false

# Check current process environment
$processToken = [System.Environment]::GetEnvironmentVariable("GITHUB_TOKEN", "Process")
if ($processToken) {
    Write-Host "[FOUND] GITHUB_TOKEN in Process environment" -ForegroundColor Yellow
    Write-Host "  Value: $($processToken.Substring(0, [Math]::Min(20, $processToken.Length)))..." -ForegroundColor Gray
    [System.Environment]::SetEnvironmentVariable("GITHUB_TOKEN", $null, "Process")
    Write-Host "  [REMOVED] From Process" -ForegroundColor Green
    $tokenFound = $true
}

# Check user environment variables
$userToken = [System.Environment]::GetEnvironmentVariable("GITHUB_TOKEN", "User")
if ($userToken) {
    Write-Host "[FOUND] GITHUB_TOKEN in User environment variables" -ForegroundColor Yellow
    Write-Host "  Value: $($userToken.Substring(0, [Math]::Min(20, $userToken.Length)))..." -ForegroundColor Gray
    [System.Environment]::SetEnvironmentVariable("GITHUB_TOKEN", $null, "User")
    Write-Host "  [REMOVED] From User environment" -ForegroundColor Green
    $tokenFound = $true
}

# Check system environment variables (requires admin)
$systemToken = [System.Environment]::GetEnvironmentVariable("GITHUB_TOKEN", "Machine")
if ($systemToken) {
    Write-Host "[FOUND] GITHUB_TOKEN in System environment variables" -ForegroundColor Yellow
    Write-Host "  Value: $($systemToken.Substring(0, [Math]::Min(20, $systemToken.Length)))..." -ForegroundColor Gray
    try {
        [System.Environment]::SetEnvironmentVariable("GITHUB_TOKEN", $null, "Machine")
        Write-Host "  [REMOVED] From System environment" -ForegroundColor Green
        $tokenFound = $true
    }
    catch {
        Write-Host "  [ERROR] Failed to remove from System environment (requires admin)" -ForegroundColor Red
        Write-Host "  Run PowerShell as Administrator and retry" -ForegroundColor Yellow
    }
}

# Check .env file
if (Test-Path .env) {
    $envContent = Get-Content .env -Raw
    if ($envContent -match "GITHUB_TOKEN") {
        Write-Host "[FOUND] GITHUB_TOKEN in .env file" -ForegroundColor Yellow
        $newContent = $envContent -replace "(?m)^GITHUB_TOKEN=.*$", "# GITHUB_TOKEN removed during account migration"
        Set-Content .env -Value $newContent -NoNewline
        Write-Host "  [REMOVED] From .env file (commented out)" -ForegroundColor Green
        $tokenFound = $true
    }
}

# Check PowerShell profile
if (Test-Path $PROFILE) {
    $profileContent = Get-Content $PROFILE -Raw
    if ($profileContent -match "GITHUB_TOKEN") {
        Write-Host "[FOUND] GITHUB_TOKEN in PowerShell profile" -ForegroundColor Yellow
        Write-Host "  Location: $PROFILE" -ForegroundColor Gray
        Write-Host "  [ACTION REQUIRED] Manually edit profile and remove GITHUB_TOKEN" -ForegroundColor Yellow
        $tokenFound = $true
    }
}

Write-Host ""
Write-Host "=== Verification ===" -ForegroundColor Cyan
$remainingTokens = Get-ChildItem Env: | Where-Object { $_.Name -like "*GITHUB*" }
if ($remainingTokens) {
    Write-Host "Remaining GitHub environment variables:" -ForegroundColor Yellow
    $remainingTokens | ForEach-Object {
        Write-Host "  $($_.Name) = $($_.Value.Substring(0, [Math]::Min(30, $_.Value.Length)))..." -ForegroundColor Gray
    }
}
else {
    Write-Host "[SUCCESS] No GITHUB_TOKEN found in environment" -ForegroundColor Green
}

Write-Host ""
if ($tokenFound) {
    Write-Host "[CLEANUP COMPLETE] Old token removed" -ForegroundColor Green
    Write-Host "IMPORTANT: Restart your terminal/IDE for changes to take effect" -ForegroundColor Yellow
}
else {
    Write-Host "[CLEAN] No GITHUB_TOKEN found - system is clean" -ForegroundColor Green
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Restart terminal/IDE" -ForegroundColor White
Write-Host "2. Run: gh auth status" -ForegroundColor White
Write-Host "3. Verify active account matches target account" -ForegroundColor White

# Made with Bob
