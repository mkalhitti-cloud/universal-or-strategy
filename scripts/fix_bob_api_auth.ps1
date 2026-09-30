# Fix Bob Shell API Authentication
# Switches from environment variable to settings file

Write-Host "`n=== Bob Shell API Authentication Fix ===" -ForegroundColor Cyan

# Step 1: Fix settings.json field name
Write-Host "`n[1/3] Fixing .bob/settings.json field name..." -ForegroundColor Yellow

$settingsPath = ".bob/settings.json"
$settingsContent = Get-Content $settingsPath -Raw

# Replace "apikey" with "api_key"
$settingsContent = $settingsContent -replace '"apikey":', '"api_key":'

# Write back to file
Set-Content -Path $settingsPath -Value $settingsContent -NoNewline

Write-Host "  [OK] Changed 'apikey' to 'api_key'" -ForegroundColor Green

# Step 2: Remove environment variable
Write-Host "`n[2/3] Removing BOBSHELL_API_KEY environment variable..." -ForegroundColor Yellow

if ($env:BOBSHELL_API_KEY) {
    $oldKey = $env:BOBSHELL_API_KEY.Substring(0, 30) + "..."
    Remove-Item Env:\BOBSHELL_API_KEY
    Write-Host "  [OK] Removed environment variable (was: $oldKey)" -ForegroundColor Green
} else {
    Write-Host "  [INFO] Environment variable not set" -ForegroundColor Gray
}

# Step 3: Verify configuration
Write-Host "`n[3/3] Verifying configuration..." -ForegroundColor Yellow

# Check settings file
$settings = Get-Content $settingsPath | ConvertFrom-Json
if ($settings.api_key) {
    $newKey = $settings.api_key.Substring(0, 30) + "..."
    Write-Host "  [OK] Settings file has api_key: $newKey" -ForegroundColor Green
} else {
    Write-Host "  [ERROR] Settings file missing api_key field!" -ForegroundColor Red
    exit 1
}

# Check environment variable is gone
if (-not $env:BOBSHELL_API_KEY) {
    Write-Host "  [OK] Environment variable removed" -ForegroundColor Green
} else {
    Write-Host "  [WARNING] Environment variable still set!" -ForegroundColor Yellow
}

Write-Host "`n=== Summary ===" -ForegroundColor Cyan
Write-Host "Bob Shell will now use the API key from .bob/settings.json" -ForegroundColor Green
Write-Host "New API key: $newKey" -ForegroundColor Gray
Write-Host "`nNext steps:" -ForegroundColor Cyan
Write-Host "  1. Restart any Bob Shell sessions" -ForegroundColor Gray
Write-Host "  2. Verify balance with: bob --help" -ForegroundColor Gray
Write-Host ""

# Made with Bob
