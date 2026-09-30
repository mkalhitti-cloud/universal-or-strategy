# Bob Shell Logout Script
# Purpose: Clean logout from Bob Shell to switch IBM accounts
# Usage: .\scripts\bob_logout.ps1

Write-Host "🔓 Bob Shell Logout Utility" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Check if Bob sessions are running
$bobProcesses = Get-Process -Name "bob" -ErrorAction SilentlyContinue
if ($bobProcesses) {
    Write-Host "⚠️  Warning: Bob processes are still running" -ForegroundColor Yellow
    Write-Host "   Please close all Bob sessions first (type /exit in each window)" -ForegroundColor Yellow
    Write-Host ""
    $continue = Read-Host "Continue anyway? (y/n)"
    if ($continue -ne "y") {
        Write-Host "❌ Logout cancelled" -ForegroundColor Red
        exit 1
    }
}

# Step 2: Remove authentication files
Write-Host "🗑️  Removing Bob authentication files..." -ForegroundColor Yellow

$authFile = "$env:USERPROFILE\.bob\auth.json"
$sessionFile = "$env:USERPROFILE\.bob\session.json"

$removed = 0

if (Test-Path $authFile) {
    Remove-Item -Path $authFile -Force
    Write-Host "   ✅ Removed: auth.json" -ForegroundColor Green
    $removed++
} else {
    Write-Host "   ℹ️  Not found: auth.json" -ForegroundColor Gray
}

if (Test-Path $sessionFile) {
    Remove-Item -Path $sessionFile -Force
    Write-Host "   ✅ Removed: session.json" -ForegroundColor Green
    $removed++
} else {
    Write-Host "   ℹ️  Not found: session.json" -ForegroundColor Gray
}

# Step 3: Verify cleanup
Write-Host ""
if ($removed -gt 0) {
    Write-Host "✅ Bob Shell logout complete!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "1. Switch IBM account in browser: https://myibm.ibm.com/dashboard" -ForegroundColor White
    Write-Host "2. Sign out of current account" -ForegroundColor White
    Write-Host "3. Sign in with new account (with 160 Bobcoins)" -ForegroundColor White
    Write-Host "4. Run: .\scripts\bob_resume.ps1" -ForegroundColor White
} else {
    Write-Host "ℹ️  No authentication files found (already logged out)" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "You can now:" -ForegroundColor Cyan
    Write-Host "1. Switch IBM account in browser" -ForegroundColor White
    Write-Host "2. Run: .\scripts\bob_resume.ps1" -ForegroundColor White
}

Write-Host ""
Write-Host "================================" -ForegroundColor Cyan

# Made with Bob
