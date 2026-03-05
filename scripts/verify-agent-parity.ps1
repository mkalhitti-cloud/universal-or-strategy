# Verify Agent Parity script

$root = Get-Location
$parity = $true

Write-Host "--- Checking Standards and Manifesto Parity ---" -ForegroundColor Cyan
if (Test-Path "$root\.agent\standards_manifesto.md") {
    Write-Host "[PASS] standards_manifesto.md exists in .agent/ (Single Source)" -ForegroundColor Green
} else {
    Write-Host "[FAIL] standards_manifesto.md missing from .agent/" -ForegroundColor Red
    $parity = $false
}

Write-Host "--- Checking Agent Home Folders ---" -ForegroundColor Cyan
$agents = @("antigravity", "claude", "codex", "cursor")
foreach ($agent in $agents) {
    if (Test-Path "$root\.agent\agents\$agent\IDENTITY.md") {
        Write-Host "[PASS] $agent identity folder is wired" -ForegroundColor Green
    } else {
        Write-Host "[FAIL] $agent identity folder missing" -ForegroundColor Red
        $parity = $false
    }
}

Write-Host "--- Checking MCP Settings Drift ---" -ForegroundColor Cyan
if (Test-Path "$root\.gemini\settings.json") {
    $geminiSettings = Get-Content "$root\.gemini\settings.json" | ConvertFrom-Json
    if ($geminiSettings.mcpServers.context7) {
        Write-Host "[PASS] Context7 is wired in Antigravity" -ForegroundColor Green
    } else {
        Write-Host "[WARN] Context7 missing from Antigravity" -ForegroundColor Yellow
    }
}

Write-Host "--- FINAL STATUS ---" -ForegroundColor Cyan
if ($parity) {
    Write-Host "### PARITY ACHIEVED ###" -ForegroundColor Black -BackgroundColor Green
} else {
    Write-Host "### PARITY FAILED - CHECK OUTPUT ###" -ForegroundColor White -BackgroundColor Red
    exit 1
}
