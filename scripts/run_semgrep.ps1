# Run Semgrep Scan for V12 DNA Compliance
# Part of Pre-Push Validation Workflow
#
# Usage:
#   .\scripts\run_semgrep.ps1                    # Full scan
#   .\scripts\run_semgrep.ps1 -DryRun            # Show what would be scanned
#   .\scripts\run_semgrep.ps1 -OutputJson        # JSON output for CI
#   .\scripts\run_semgrep.ps1 -Severity ERROR    # Only show ERROR findings

param(
    [switch]$DryRun,
    [switch]$OutputJson,
    [ValidateSet("ERROR", "WARNING", "INFO")]
    [string]$Severity = "WARNING",
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Semgrep V12 DNA Compliance Scan" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if Semgrep is installed
$semgrepInstalled = $null -ne (Get-Command semgrep -ErrorAction SilentlyContinue)

if (-not $semgrepInstalled) {
    Write-Host "[ERROR] Semgrep is not installed." -ForegroundColor Red
    Write-Host ""
    Write-Host "Install via pip:" -ForegroundColor Yellow
    Write-Host "  pip install semgrep" -ForegroundColor White
    Write-Host ""
    Write-Host "Or via Homebrew (macOS):" -ForegroundColor Yellow
    Write-Host "  brew install semgrep" -ForegroundColor White
    Write-Host ""
    Write-Host "Or download from: https://semgrep.dev/docs/getting-started/" -ForegroundColor Yellow
    exit 1
}

# Verify .semgrep.yml exists
if (-not (Test-Path ".semgrep.yml")) {
    Write-Host "[ERROR] .semgrep.yml configuration not found." -ForegroundColor Red
    Write-Host "Expected location: .semgrep.yml" -ForegroundColor Yellow
    exit 1
}

Write-Host "[INFO] Semgrep version:" -ForegroundColor Cyan
semgrep --version
Write-Host ""

# Build scan command
$scanArgs = @(
    "scan",
    "--config", ".semgrep.yml",
    "--metrics", "off"  # Disable telemetry
)

# Add severity filter
if ($Severity -eq "ERROR") {
    $scanArgs += "--severity", "ERROR"
} elseif ($Severity -eq "WARNING") {
    $scanArgs += "--severity", "WARNING"
    $scanArgs += "--severity", "ERROR"
}

# Add output format
if ($OutputJson) {
    $scanArgs += "--json"
    $scanArgs += "--output", "semgrep-results.json"
} else {
    if ($Verbose) {
        $scanArgs += "--verbose"
    }
}

# Add target paths (scan src/ only, exclude tests/benchmarks for speed)
$scanArgs += "src/"

if ($DryRun) {
    Write-Host "[DRY-RUN] Would execute:" -ForegroundColor Yellow
    Write-Host "  semgrep $($scanArgs -join ' ')" -ForegroundColor White
    Write-Host ""
    Write-Host "[DRY-RUN] Scanning paths:" -ForegroundColor Yellow
    Get-ChildItem -Path "src/" -Filter "*.cs" -Recurse | Select-Object -First 10 | ForEach-Object {
        Write-Host "  - $($_.FullName)" -ForegroundColor White
    }
    Write-Host "  ... (and more)" -ForegroundColor White
    exit 0
}

Write-Host "[INFO] Scanning src/ for V12 DNA violations..." -ForegroundColor Cyan
Write-Host ""

# Run Semgrep
$startTime = Get-Date
try {
    & semgrep @scanArgs
    $exitCode = $LASTEXITCODE
} catch {
    Write-Host "[ERROR] Semgrep scan failed: $_" -ForegroundColor Red
    exit 1
}
$endTime = Get-Date
$duration = ($endTime - $startTime).TotalSeconds

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Scan completed in $([math]::Round($duration, 2))s" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Interpret exit codes
# Semgrep exit codes:
#   0 = No findings
#   1 = Findings detected
#   2 = Fatal error
if ($exitCode -eq 0) {
    Write-Host "[PASS] No V12 DNA violations detected!" -ForegroundColor Green
    Write-Host ""
    Write-Host "All checks passed:" -ForegroundColor Green
    Write-Host "  [x] No lock() statements" -ForegroundColor Green
    Write-Host "  [x] ASCII-only strings" -ForegroundColor Green
    Write-Host "  [x] Atomic operations on shared state" -ForegroundColor Green
    Write-Host "  [x] No blocking async calls" -ForegroundColor Green
    Write-Host "  [x] No LINQ in hot paths" -ForegroundColor Green
    exit 0
} elseif ($exitCode -eq 1) {
    Write-Host "[FAIL] V12 DNA violations detected!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Review findings above and fix before pushing." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Common fixes:" -ForegroundColor Cyan
    Write-Host "  - lock() -> Interlocked.CompareExchange or Actor model" -ForegroundColor White
    Write-Host "  - Unicode -> ASCII equivalents (!, --, ->, straight quotes)" -ForegroundColor White
    Write-Host "  - Task.Wait() -> await Task.WhenAll()" -ForegroundColor White
    Write-Host "  - LINQ in OnBarUpdate -> for loops" -ForegroundColor White
    Write-Host ""
    Write-Host "For detailed guidance, see: docs/architecture.md" -ForegroundColor Cyan
    exit 1
} else {
    Write-Host "[ERROR] Semgrep scan failed with exit code $exitCode" -ForegroundColor Red
    exit $exitCode
}

# Made with Bob
