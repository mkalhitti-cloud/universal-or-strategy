# start.ps1  —  Start the WSGTA Signal Monitor in LIVE mode
# Run from the project root:   .\tools\signal-monitor\start.ps1
# Or from inside signal-monitor:   .\start.ps1

$root = Split-Path $MyInvocation.MyCommand.Path -Parent
Set-Location $root

Write-Host ""
Write-Host "=========================================="
Write-Host "  WSGTA Signal Monitor — Live Startup"
Write-Host "=========================================="
Write-Host ""

# Step 1: Refresh Schwab token
Write-Host "Step 1: Refreshing Schwab token..."
python refresh_token.py
Write-Host ""

# Step 2: Kill any existing server on port 5000
Write-Host "Step 2: Clearing port 5000..."
$listener = netstat -ano | Select-String ":5000.*LISTENING"
if ($listener) {
    $oldPid = ($listener -split '\s+')[-1].Trim()
    Write-Host "  Killing old server PID $oldPid"
    Stop-Process -Id $oldPid -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 800
    Write-Host "  Port cleared."
} else {
    Write-Host "  Port already free."
}
Write-Host ""

# Step 3: Start server
Write-Host "Step 3: Starting server..."
Start-Process -FilePath "python" -ArgumentList "server.py" -WorkingDirectory $root -WindowStyle Normal
Start-Sleep -Seconds 20

# Step 4: Verify
Write-Host "Step 4: Verifying..."
try {
    $status = Invoke-RestMethod "http://localhost:5000/api/status" -TimeoutSec 8
    Write-Host ""
    Write-Host "  Mode        : $($status.mode)"
    Write-Host "  Last update : $($status.last_refresh_fmt)"
    Write-Host ""
    if ($status.mode -eq "live") {
        Write-Host "  LIVE mode — prices are real-time."
    } else {
        Write-Host "  DEMO mode — check .env and .token_cache.json"
    }
} catch {
    Write-Host "  Server not responding yet. Wait a few seconds and refresh browser."
}

Write-Host ""
Write-Host "  Open: http://localhost:5000"
Write-Host "=========================================="
Write-Host ""
