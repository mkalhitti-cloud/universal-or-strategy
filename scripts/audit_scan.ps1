# Director's Audit Scan (V1.0)
# Purpose: Automate code safety checks for NinjaTrader strategies to find "Silent Killers".

param (
    [string]$SearchPath = "C:\WSGTA\universal-or-strategy"
)

Write-Host "Starting Universal OR Strategy Audit Scan..." -ForegroundColor Cyan
$failureCount = 0

# 1. DIV_ZERO CHECK: Find divisions without guards
Write-Host "Checking for Division-by-Zero risks..." -ForegroundColor Yellow
$divFiles = Get-ChildItem -Path $SearchPath -Include "*.cs" -Recurse | Select-String -Pattern "\s*\/[^/]" -AllMatches
foreach ($match in $divFiles) {
    if ($match.Line -match "CalculatePositionSize|CalculateATRStopDistance|GetTargetDistribution") {
        continue
    }
    
    if ($match.Line -match "\/\s*0") {
        Write-Host "CRITICAL: Hardcoded division by zero at $($match.Path):$($match.LineNumber)" -ForegroundColor Red
        $failureCount++
    }
}

# 2. STALE_PRICE CHECK
Write-Host "Checking for Stale Price usage..." -ForegroundColor Yellow
$staleFiles = Get-ChildItem -Path $SearchPath -Include "*.cs" -Recurse | Select-String -Pattern "Close\[0\]"
foreach ($match in $staleFiles) {
    $content = Get-Content $match.Path
    $prevLine = $content[$match.LineNumber - 2]
    if ($prevLine -notmatch "lastKnownPrice" -and $match.Line -notmatch "currentPrice = lastKnownPrice") {
        Write-Host "WARNING: Potential stale price (Close [0]) without lastKnownPrice guard at $($match.Path):$($match.LineNumber)" -ForegroundColor Magenta
    }
}

# 3. MODE_EXCLUSION CHECK
Write-Host "Checking for Mode Mutual Exclusion..." -ForegroundColor Yellow
$uiFile = Join-Path $SearchPath "UniversalORStrategyV12_002_Dev.UI.cs"
if (Test-Path $uiFile) {
    if ((Get-Content $uiFile | Select-String -Pattern "isRMAModeActive").Count -gt 1) {
        $setModeMatch = Get-Content $uiFile | Select-String -Pattern "void SetActiveMode"
        if (-not $setModeMatch) {
            Write-Host "WARNING: Strategy lacks a centralized SetActiveMode method. Mode collisions possible." -ForegroundColor Magenta
        }
    }
}

# 4. NAMING_SYNC CHECK (Timestamp Resolution)
Write-Host "Checking Timestamp Resolution in Signal Names..." -ForegroundColor Yellow
$signalFiles = Get-ChildItem -Path $SearchPath -Include "*.cs" -Recurse | Select-String -Pattern 'ToString\("HHmmss"\)'
foreach ($match in $signalFiles) {
    if ($match.Line -notmatch "ffff") {
        Write-Host "WARNING: Signal name timestamp resolution too low (needs ffff) at $($match.Path):$($match.LineNumber)" -ForegroundColor Magenta
    }
}

Write-Host "`nAudit Scan Complete." -ForegroundColor Cyan
if ($failureCount -eq 0) {
    Write-Host "SUCCESS: NO CRITICAL ERRORS FOUND." -ForegroundColor Green
}
else {
    Write-Host "FAILURE: FOUND $failureCount CRITICAL ERRORS." -ForegroundColor Red
}
