# scripts/build_readiness.ps1
# WSGTA Readiness: Build System Pillar
# Verifies repository integrity and source compilation suitability.

Write-Host "--- READINESS: Verifying Build Integrity ---" -ForegroundColor Cyan

# 1. Verify Directory Link Readiness
./deploy-sync.ps1

# 2. Verify Code Formatting (CSharpier)
Write-Host "`nChecking code formatting..." -ForegroundColor Yellow
$formatCheck = dotnet csharpier check src/ 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "BUILD READINESS FAIL: Code formatting issues detected." -ForegroundColor Red
    Write-Host "Run: dotnet csharpier format src/" -ForegroundColor Yellow
    exit 1
}
Write-Host "Formatting check passed." -ForegroundColor Green

# 3. Verify Source Compilation (Evaluation)
Write-Host "`nBuilding project..." -ForegroundColor Yellow
dotnet build Linting.csproj /nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host "BUILD READINESS FAIL: Source compilation errors." -ForegroundColor Red
    exit 1
}

Write-Host "`nBUILD READINESS PASS: Environment and source are synchronized." -ForegroundColor Green
