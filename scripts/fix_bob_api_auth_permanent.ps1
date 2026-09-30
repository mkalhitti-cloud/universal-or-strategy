# Fix Bob Shell API Authentication - PERMANENT
# Removes environment variable at USER level and updates settings file

Write-Host "`n=== Bob Shell API Authentication - PERMANENT Fix ===" -ForegroundColor Cyan

# Step 1: Fix settings.json field name (if not already done)
Write-Host "`n[1/4] Fixing .bob/settings.json field name..." -ForegroundColor Yellow

$settingsPath = ".bob/settings.json"
$settingsContent = Get-Content $settingsPath -Raw

if ($settingsContent -match '"apikey":') {
    $settingsContent = $settingsContent -replace '"apikey":', '"api_key":'
    Set-Content -Path $settingsPath -Value $settingsContent -NoNewline
    Write-Host "  [OK] Changed 'apikey' to 'api_key'" -ForegroundColor Green
} else {
    Write-Host "  [OK] Already using 'api_key'" -ForegroundColor Green
}

# Step 2: Remove environment variable at USER level (permanent)
Write-Host "`n[2/4] Removing BOBSHELL_API_KEY from USER environment..." -ForegroundColor Yellow

$userEnvPath = 'HKCU:\Environment'
$envVarName = 'BOBSHELL_API_KEY'

try {
    $currentValue = [System.Environment]::GetEnvironmentVariable($envVarName, 'User')
    
    if ($currentValue) {
        $oldKey = $currentValue.Substring(0, 30) + "..."
        [System.Environment]::SetEnvironmentVariable($envVarName, $null, 'User')
        Write-Host "  [OK] Removed USER-level environment variable (was: $oldKey)" -ForegroundColor Green
    } else {
        Write-Host "  [INFO] USER-level environment variable not set" -ForegroundColor Gray
    }
} catch {
    Write-Host "  [WARNING] Could not access USER environment: $_" -ForegroundColor Yellow
}

# Step 3: Remove from current session
Write-Host "`n[3/4] Removing from current PowerShell session..." -ForegroundColor Yellow

if ($env:BOBSHELL_API_KEY) {
    Remove-Item Env:\BOBSHELL_API_KEY -ErrorAction SilentlyContinue
    Write-Host "  [OK] Removed from current session" -ForegroundColor Green
} else {
    Write-Host "  [INFO] Not set in current session" -ForegroundColor Gray
}

# Step 4: Verify configuration
Write-Host "`n[4/4] Verifying configuration..." -ForegroundColor Yellow

# Check settings file
$settings = Get-Content $settingsPath | ConvertFrom-Json
if ($settings.api_key) {
    $newKey = $settings.api_key.Substring(0, 30) + "..."
    Write-Host "  [OK] Settings file has api_key: $newKey" -ForegroundColor Green
} else {
    Write-Host "  [ERROR] Settings file missing api_key field!" -ForegroundColor Red
    exit 1
}

# Check USER-level environment variable
$userEnvCheck = [System.Environment]::GetEnvironmentVariable($envVarName, 'User')
if (-not $userEnvCheck) {
    Write-Host "  [OK] USER-level environment variable removed" -ForegroundColor Green
} else {
    Write-Host "  [WARNING] USER-level environment variable still exists!" -ForegroundColor Yellow
}

# Check current session
if (-not $env:BOBSHELL_API_KEY) {
    Write-Host "  [OK] Current session environment variable removed" -ForegroundColor Green
} else {
    Write-Host "  [WARNING] Current session still has environment variable!" -ForegroundColor Yellow
}

Write-Host "`n=== Summary ===" -ForegroundColor Cyan
Write-Host "Bob Shell will now use the API key from .bob/settings.json" -ForegroundColor Green
Write-Host "New API key: $newKey" -ForegroundColor Gray
Write-Host "`nIMPORTANT Next Steps:" -ForegroundColor Cyan
Write-Host "  1. CLOSE THIS TERMINAL WINDOW completely" -ForegroundColor Yellow
Write-Host "  2. Open a NEW terminal window" -ForegroundColor Yellow
Write-Host "  3. Verify with: echo `$env:BOBSHELL_API_KEY (should be empty)" -ForegroundColor Gray
Write-Host "  4. Start Bob Shell and check balance" -ForegroundColor Gray
Write-Host ""

# Made with Bob
