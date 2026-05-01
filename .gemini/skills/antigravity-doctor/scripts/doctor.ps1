<#
.SYNOPSIS
    Diagnoses and repairs common Antigravity IDE startup and generation issues.
#>

$ProjectRoot = Get-Location
$GeminiDir = Join-Path $env:USERPROFILE ".gemini"
$AntigravityProfile = Join-Path $GeminiDir "antigravity"

Write-Host "--- Antigravity Doctor: Starting Recovery ---" -ForegroundColor Cyan

# 1. Clear Stale Locks
Write-Host "[1/5] Clearing stale lock files..." -ForegroundColor Yellow
Get-ChildItem -Path $GeminiDir -Filter "*.lock" -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force
Get-ChildItem -Path $GeminiDir -Filter "LOCK" -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force
Get-ChildItem -Path $ProjectRoot -Filter "*.lock" -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force

# 2. Fix Git Config
Write-Host "[2/5] Validating Git configuration..." -ForegroundColor Yellow
if (Test-Path ".git/config") {
    git config core.repositoryformatversion 1
    git config --unset extensions.worktreeConfig 2>$null
    Write-Host "      Git config optimized for Antigravity." -ForegroundColor Green
}

# 3. Optimize Scanner
Write-Host "[3/5] Checking scanner optimization (.claudeignore)..." -ForegroundColor Yellow
$IgnorePath = Join-Path $ProjectRoot ".claudeignore"
if (-not (Test-Path $IgnorePath)) {
    $DefaultIgnore = @"
# Antigravity Scanner Optimization
ARCHIVE_V12/
vendor/
node_modules/
mandelbulb-explorer*/
morpheus-website/
morpheus-glitch-matrix/
TestResults/
bin/
obj/
.dotnet-home/
graphify-out_backup/
"@
    Set-Content -Path $IgnorePath -Value $DefaultIgnore
    Write-Host "      Created .claudeignore to prevent scanner overload." -ForegroundColor Green
}

# 4. Process Cleanup
Write-Host "[4/5] Terminating hanging processes..." -ForegroundColor Yellow
$Processes = @("language_server_windows_x64", "Antigravity", "jcodemunch-mcp")
foreach ($P in $Processes) {
    Stop-Process -Name $P -Force -ErrorAction SilentlyContinue
}

# 5. Heavy Directory Detection
Write-Host "[5/5] Checking for directory bloat (>50k files)..." -ForegroundColor Yellow
$MaxFiles = 50000
$Dirs = Get-ChildItem -Path $ProjectRoot -Directory -Exclude ".git", "node_modules", "Laboratory"
foreach ($Dir in $Dirs) {
    $Count = (Get-ChildItem -Path $Dir.FullName -Recurse -File -ErrorAction SilentlyContinue).Count
    if ($Count -gt $MaxFiles) {
        Write-Host "      CRITICAL: Directory '$($Dir.Name)' contains $Count files. This WILL crash the Antigravity scanner." -ForegroundColor Red
        Write-Host "      ACTION: Move '$($Dir.Name)' out of the project root or add it to .claudeignore immediately." -ForegroundColor Yellow
    }
}

Write-Host "--- Recovery Complete ---" -ForegroundColor Cyan
Write-Host "Please restart Antigravity now." -ForegroundColor White
