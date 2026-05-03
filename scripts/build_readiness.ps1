# scripts/build_readiness.ps1
# WSGTA Readiness: Build System Pillar
# Verifies repository integrity and source compilation suitability.

Write-Host "--- READINESS: Verifying Build Integrity ---" -ForegroundColor Cyan

# 1. Verify Directory Link Readiness
./deploy-sync.ps1

# 1b. Deadlock Window Scan (mandatory gate)
Write-Host "--- Scanning for Deadlock Windows ---" -ForegroundColor Cyan
& "$PSScriptRoot\scan_deadlock_windows.ps1"
if ($LASTEXITCODE -ne 0) {
    Write-Host "BUILD READINESS FAIL: Deadlock windows detected in src/." -ForegroundColor Red
    exit 1
}

# 2. Verify Source Compilation (Evaluation)
dotnet build Linting.csproj /nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host "BUILD READINESS FAIL: Source compilation errors." -ForegroundColor Red
    exit 1
}

Write-Host "BUILD READINESS PASS: Environment and source are synchronized." -ForegroundColor Green
