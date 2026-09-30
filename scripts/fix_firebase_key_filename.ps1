# Fix Firebase Key Filename Mismatch
# 
# Issue: New Firebase key saved as firebase-key.json but scripts expect firebase-credentials.json
# Solution: Rename new key to expected filename
#
# Date: 2026-06-14
# Reference: docs/security/FIREBASE_KEY_UPDATE_INSTRUCTIONS.md

$ErrorActionPreference = "Stop"

Write-Host "=== Firebase Key Filename Fix ===" -ForegroundColor Cyan
Write-Host ""

# Navigate to project root
$projectRoot = "C:\WSGTA\universal-or-strategy"
Set-Location $projectRoot

Write-Host "Current directory: $projectRoot" -ForegroundColor Gray
Write-Host ""

# Check if old key exists (revoked)
if (Test-Path "firebase-credentials.json") {
    Write-Host "[1/4] Backing up old revoked key..." -ForegroundColor Yellow
    Rename-Item "firebase-credentials.json" "firebase-credentials.json.revoked" -Force
    Write-Host "      ✓ Renamed to firebase-credentials.json.revoked" -ForegroundColor Green
} else {
    Write-Host "[1/4] No old key found - already removed" -ForegroundColor Gray
}

Write-Host ""

# Check if new key exists
if (-not (Test-Path "firebase-key.json")) {
    Write-Host "[ERROR] firebase-key.json not found!" -ForegroundColor Red
    Write-Host "        Expected location: $projectRoot\firebase-key.json" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please ensure the new Firebase key is saved as firebase-key.json" -ForegroundColor Yellow
    exit 1
}

Write-Host "[2/4] Renaming new key to expected filename..." -ForegroundColor Yellow
Rename-Item "firebase-key.json" "firebase-credentials.json" -Force
Write-Host "      ✓ Renamed firebase-key.json → firebase-credentials.json" -ForegroundColor Green

Write-Host ""

# Verify gitignore protection
Write-Host "[3/4] Verifying gitignore protection..." -ForegroundColor Yellow
$gitStatus = git status --porcelain 2>&1 | Select-String "firebase-credentials.json"

if ($gitStatus) {
    Write-Host "      ✗ WARNING: firebase-credentials.json appears in git status!" -ForegroundColor Red
    Write-Host "      This should be gitignored. Check .gitignore configuration." -ForegroundColor Red
    Write-Host ""
    Write-Host "Git status output:" -ForegroundColor Gray
    git status --porcelain | Select-String "firebase"
} else {
    Write-Host "      ✓ File properly gitignored (not tracked)" -ForegroundColor Green
}

Write-Host ""

# Test Firebase connectivity
Write-Host "[4/4] Testing Firebase connectivity..." -ForegroundColor Yellow

try {
    $testResult = python scripts/query_kb.py "test" 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "      ✓ Firebase connection successful!" -ForegroundColor Green
    } else {
        Write-Host "      ⚠ Firebase test returned non-zero exit code" -ForegroundColor Yellow
        Write-Host "      This may be normal if no results found" -ForegroundColor Gray
    }
} catch {
    Write-Host "      ✗ Firebase connection failed" -ForegroundColor Red
    Write-Host "      Error: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Fix Complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Summary:" -ForegroundColor White
Write-Host "  • Old key backed up as: firebase-credentials.json.revoked" -ForegroundColor Gray
Write-Host "  • New key renamed to: firebase-credentials.json" -ForegroundColor Gray
Write-Host "  • Scripts will now use the new (active) key" -ForegroundColor Gray
Write-Host ""
Write-Host "Next steps:" -ForegroundColor White
Write-Host "  1. Test Jane Street KB: python scripts/query_kb.py 'complexity reduction'" -ForegroundColor Gray
Write-Host "  2. Test Phase 4.5 script: python scripts/phase_4_5_ticket_review_mcp.py" -ForegroundColor Gray
Write-Host ""

# Made with Bob
