# build-release.ps1 -- PTT release build + ConfuserEx obfuscation
# Usage: powershell -File scripts\build-release.ps1
# Requires: ConfuserEx CLI (crass.exe) in PATH or %CONFUSER_PATH%
# Output: release\PropTraderTools.obfuscated.dll

param(
    [string]$Configuration = "Release",
    [string]$ConfuserCrproj = "confuserex.crproj",
    [string]$OutputDir = "release"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "[build-release] Building $Configuration..."
dotnet build src/PropTraderTools/PropTraderTools.csproj -c $Configuration --nologo

$dllPath = Get-ChildItem "src/PropTraderTools/bin/$Configuration" -Filter "PropTraderTools.dll" -Recurse |
           Select-Object -First 1 -ExpandProperty FullName

if (-not $dllPath) {
    Write-Error "[build-release] PropTraderTools.dll not found after build."
    exit 1
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$confuserExe = if ($env:CONFUSER_PATH) { Join-Path $env:CONFUSER_PATH "crass.exe" } else { "crass.exe" }

if (-not (Get-Command $confuserExe -ErrorAction SilentlyContinue)) {
    Write-Warning "[build-release] ConfuserEx not found at '$confuserExe'. Skipping obfuscation."
    Copy-Item $dllPath (Join-Path $OutputDir "PropTraderTools.dll") -Force
    Write-Host "[build-release] DONE (no obfuscation). Output: $OutputDir\PropTraderTools.dll"
    exit 0
}

Write-Host "[build-release] Running ConfuserEx..."
& $confuserExe -n $ConfuserCrproj
Write-Host "[build-release] DONE. Obfuscated output in $OutputDir"