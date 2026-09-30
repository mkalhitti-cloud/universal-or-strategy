# Setup Linear Environment Variables Permanently
# This script adds LINEAR_API_KEY to your PowerShell profile

$apiKey = "<REDACTED>"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "Linear CLI Environment Setup" -ForegroundColor Cyan
Write-Host "============================================================`n" -ForegroundColor Cyan

# Check if PowerShell profile exists
if (-not (Test-Path $PROFILE)) {
    Write-Host "Creating PowerShell profile at: $PROFILE" -ForegroundColor Yellow
    New-Item -Path $PROFILE -ItemType File -Force | Out-Null
}

# Check if LINEAR_API_KEY already exists in profile
$profileContent = Get-Content $PROFILE -Raw -ErrorAction SilentlyContinue
if ($profileContent -match 'LINEAR_API_KEY') {
    Write-Host "LINEAR_API_KEY already exists in profile. Updating..." -ForegroundColor Yellow
    $profileContent = $profileContent -replace '\$env:LINEAR_API_KEY\s*=\s*"[^"]*"', "`$env:LINEAR_API_KEY = `"$apiKey`""
    Set-Content -Path $PROFILE -Value $profileContent
} else {
    Write-Host "Adding LINEAR_API_KEY to profile..." -ForegroundColor Green
    Add-Content -Path $PROFILE -Value "`n# Linear CLI API Key (V12 Project)"
    Add-Content -Path $PROFILE -Value "`$env:LINEAR_API_KEY = `"$apiKey`""
}

Write-Host "`n✓ LINEAR_API_KEY added to PowerShell profile" -ForegroundColor Green
Write-Host "  Profile location: $PROFILE" -ForegroundColor Gray

# Set for current session
$env:LINEAR_API_KEY = $apiKey
Write-Host "`n✓ LINEAR_API_KEY set for current session" -ForegroundColor Green

Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "Setup Complete!" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

Write-Host "`nNext steps:" -ForegroundColor Yellow
Write-Host "1. Reload your profile: . `$PROFILE" -ForegroundColor White
Write-Host "2. Test the CLI: npx @linear/cli new" -ForegroundColor White
Write-Host "3. Or just close and reopen PowerShell" -ForegroundColor White

Write-Host "`nUsage examples:" -ForegroundColor Yellow
Write-Host "  npx @linear/cli new                    # Create new issue" -ForegroundColor White
Write-Host "  npx @linear/cli checkout               # Checkout branch" -ForegroundColor White

# Made with Bob
