#!/usr/bin/env pwsh
# V12 Universal OR Strategy - Daily Development Startup Script
# Launches: Skybridge, Greptile MCP Bridge, Obsidian Sync, Compound Intelligence

param(
    [switch]$SkipObsidian,
    [switch]$SkipCompound,
    [switch]$Verbose
)

$ErrorActionPreference = "Continue"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "V12 Development Environment Startup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Function to start a service in a new terminal
function Start-Service {
    param(
        [string]$Name,
        [string]$Command,
        [string]$WorkingDir = $PWD,
        [string]$Color = "Green"
    )
    
    Write-Host "[Starting] $Name..." -ForegroundColor $Color
    
    try {
        # Start in new PowerShell window
        Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$WorkingDir'; Write-Host '[$Name] Starting...' -ForegroundColor $Color; $Command" -WindowStyle Normal
        Start-Sleep -Seconds 2
        Write-Host "[OK] $Name launched" -ForegroundColor Green
    }
    catch {
        Write-Host "[ERROR] Failed to start $Name : $_" -ForegroundColor Red
    }
}

# Function to check if a port is in use
function Test-Port {
    param([int]$Port)
    
    try {
        $connection = Test-NetConnection -ComputerName localhost -Port $Port -WarningAction SilentlyContinue -InformationLevel Quiet
        return $connection
    }
    catch {
        return $false
    }
}

# Check if services are already running
Write-Host "[Check] Checking for existing services..." -ForegroundColor Yellow
Write-Host ""

# 1. Start Phoenix Tracing Server (Port 6006)
Write-Host "[Starting] Phoenix Tracing Server..." -ForegroundColor Magenta

try {
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "Write-Host '[Phoenix] Starting trace server...' -ForegroundColor Magenta; python -m phoenix.server.main serve" -WindowStyle Normal
    Start-Sleep -Seconds 2
    Write-Host "[OK] Phoenix launched on http://localhost:6006" -ForegroundColor Green
}
catch {
    Write-Host "[ERROR] Failed to start Phoenix: $_" -ForegroundColor Red
}

Write-Host ""

# 2. Start Greptile MCP Bridge (Port 8080 - SSE-to-HTTP for Bob)
Write-Host "[Starting] Greptile MCP Bridge..." -ForegroundColor Cyan

# Try to load .env file if it exists
$envFile = Join-Path $PSScriptRoot ".." ".env"
if (Test-Path $envFile) {
    Get-Content $envFile | ForEach-Object {
        if ($_ -match '^\s*([^#][^=]+)=(.+)$') {
            $key = $matches[1].Trim()
            $value = $matches[2].Trim()
            [Environment]::SetEnvironmentVariable($key, $value, "Process")
        }
    }
}

if (-not (Test-Path env:GREPTILE_API_KEY)) {
    Write-Host "[SKIP] GREPTILE_API_KEY not set - Greptile MCP Bridge not started" -ForegroundColor Yellow
    Write-Host "[INFO] To enable: Set GREPTILE_API_KEY in .env file or environment" -ForegroundColor Gray
}
else {
    try {
        $greptileCmd = "npx -y greptile-mcp-server --api-key=$env:GREPTILE_API_KEY"
        if (Test-Path env:GITHUB_TOKEN) {
            $greptileCmd += " --github-token=$env:GITHUB_TOKEN"
        }
        
        Start-Process powershell -ArgumentList "-NoExit", "-Command", "Write-Host '[Greptile] Starting MCP bridge on port 8080...' -ForegroundColor Cyan; $greptileCmd" -WindowStyle Normal
        Start-Sleep -Seconds 3
        Write-Host "[OK] Greptile MCP Bridge launched on http://localhost:8080" -ForegroundColor Green
    }
    catch {
        Write-Host "[ERROR] Failed to start Greptile MCP Bridge: $_" -ForegroundColor Red
    }
}

Write-Host ""

# 3. Start Obsidian Sync (if not skipped)
if (-not $SkipObsidian) {
    Write-Host "[Starting] Obsidian Sync..." -ForegroundColor Blue
    
    # Check if Obsidian is installed
    $obsidianPaths = @(
        "$env:LOCALAPPDATA\Obsidian\Obsidian.exe",
        "$env:ProgramFiles\Obsidian\Obsidian.exe",
        "C:\Program Files\Obsidian\Obsidian.exe"
    )
    
    $obsidianPath = $obsidianPaths | Where-Object { Test-Path $_ } | Select-Object -First 1
    
    if ($obsidianPath) {
        try {
            Start-Process $obsidianPath -WindowStyle Normal
            Write-Host "[OK] Obsidian launched" -ForegroundColor Green
        }
        catch {
            Write-Host "[ERROR] Failed to launch Obsidian: $_" -ForegroundColor Red
        }
    }
    else {
        Write-Host "[!] Obsidian not found. Install from https://obsidian.md" -ForegroundColor Yellow
    }
}
else {
    Write-Host "[Skip] Obsidian sync (--SkipObsidian flag)" -ForegroundColor Yellow
}

# 4. Start Compound Intelligence Services (if not skipped)
if (-not $SkipCompound) {
    Write-Host "[Starting] Compound Intelligence Services..." -ForegroundColor Green
    
    # Check for agent bootstrap script
    $bootstrapScript = Join-Path $PSScriptRoot "agent_bootstrap.py"
    
    if (Test-Path $bootstrapScript) {
        # Run bootstrap in background
        Start-Service -Name "Agent Bootstrap" `
                     -Command "python '$bootstrapScript' System architecture" `
                     -WorkingDir $PSScriptRoot `
                     -Color "Green"
    }
    else {
        Write-Host "[!] agent_bootstrap.py not found. Compound intelligence not started." -ForegroundColor Yellow
        Write-Host "    Create it using: docs/protocol/AGENT_BOOTSTRAP_PROTOCOL.md" -ForegroundColor Yellow
    }
    
    # Check for nexus relay
    $nexusScript = Join-Path $PSScriptRoot "nexus_relay.py"
    
    if (Test-Path $nexusScript) {
        Start-Service -Name "Nexus Relay" `
                     -Command "python '$nexusScript'" `
                     -WorkingDir $PSScriptRoot `
                     -Color "Green"
    }
}
else {
    Write-Host "[Skip] Compound intelligence (--SkipCompound flag)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Startup Complete!" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Services launched:" -ForegroundColor White
Write-Host "  - Phoenix Tracing (http://localhost:6006)" -ForegroundColor Magenta
Write-Host "  - Greptile MCP Bridge (http://localhost:8080)" -ForegroundColor Cyan
if (-not $SkipObsidian) {
    Write-Host "  - Obsidian (Knowledge Management)" -ForegroundColor Blue
}
if (-not $SkipCompound) {
    Write-Host "  - Compound Intelligence Stack" -ForegroundColor Green
}
Write-Host ""
Write-Host "Verify services:" -ForegroundColor Yellow
Write-Host "  curl http://localhost:6006" -ForegroundColor Gray
Write-Host "  curl http://localhost:8080" -ForegroundColor Gray
Write-Host ""
Write-Host "To stop services: Close the PowerShell windows" -ForegroundColor Yellow
Write-Host ""

# Optional: Run health checks
if ($Verbose) {
    Write-Host "Running health checks..." -ForegroundColor Yellow
    Start-Sleep -Seconds 5
    
    try {
        $phoenixHealth = Invoke-WebRequest -Uri "http://localhost:6006" -UseBasicParsing -TimeoutSec 5
        Write-Host "[OK] Phoenix: $($phoenixHealth.StatusCode)" -ForegroundColor Green
    }
    catch {
        Write-Host "[ERROR] Phoenix: Not responding" -ForegroundColor Red
    }
    
    try {
        $greptileHealth = Invoke-WebRequest -Uri "http://localhost:8080" -UseBasicParsing -TimeoutSec 5
        Write-Host "[OK] Greptile MCP Bridge: $($greptileHealth.StatusCode)" -ForegroundColor Green
    }
    catch {
        Write-Host "[ERROR] Greptile MCP Bridge: Not responding" -ForegroundColor Red
    }
}

Write-Host "Ready to code!" -ForegroundColor Cyan

# Made with Bob
