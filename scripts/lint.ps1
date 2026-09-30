# scripts/lint.ps1
# WSGTA Readiness: Style & Validation Pillar
# Runs StyleCop analysis via the evaluation-only Linting project.

Write-Host "--- READINESS: Running StyleCop Audit ---" -ForegroundColor Yellow
dotnet build Linting.csproj /p:SkipCompilerExecution=false /nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host "LINT FAIL: Style violations detected." -ForegroundColor Red
    exit 1
}
Write-Host "LINT PASS: Clean code standards verified." -ForegroundColor Green
