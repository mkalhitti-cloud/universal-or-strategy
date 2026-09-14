# ptt-sync.ps1
# Hard-links all src/PropTraderTools/**/*.cs files to the NinjaTrader 8 AddOns folder.
# Run after every src/PropTraderTools edit to keep NT8 in sync.
# Usage:  powershell -File .\scripts\ptt-sync.ps1
# Idempotent: removes existing destination file before re-linking so the inode is refreshed.

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path $PSScriptRoot -Parent
$srcRoot  = Join-Path $repoRoot "src\PropTraderTools"
$dstRoot  = "$env:USERPROFILE\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools"

if (!(Test-Path $srcRoot)) {
    Write-Error "Source not found: $srcRoot"
    exit 1
}

if (!(Test-Path $dstRoot)) {
    Write-Host "Creating NT8 AddOns target: $dstRoot"
    New-Item $dstRoot -ItemType Directory -Force | Out-Null
}

$files = Get-ChildItem $srcRoot -Recurse -Filter "*.cs" | Where-Object {
    $_.FullName -notmatch "\\bin\\" -and
    $_.FullName -notmatch "\\obj\\" -and
    $_.FullName -notmatch "\\TestResults\\" -and
    $_.Name     -notmatch "Tests\.cs$"          # xUnit test files -- NT8 cannot resolve Xunit
}

if ($files.Count -eq 0) {
    Write-Error "No .cs files found under $srcRoot (excluding bin/obj/TestResults). Check path."
    exit 1
}

$ok  = 0
$err = 0

foreach ($f in $files) {
    $rel     = $f.FullName.Substring($srcRoot.Length).TrimStart('\')
    $dstFile = Join-Path $dstRoot $rel
    $dstDir  = Split-Path $dstFile -Parent

    if (!(Test-Path $dstDir)) {
        New-Item $dstDir -ItemType Directory -Force | Out-Null
    }

    if (Test-Path $dstFile) {
        Remove-Item $dstFile -Force
    }

    try {
        New-Item -ItemType HardLink -Path $dstFile -Target $f.FullName | Out-Null
        Write-Host "  LINKED  $rel"
        $ok++
    } catch {
        Write-Host "  ERROR   $rel -- $_" -ForegroundColor Red
        $err++
    }
}

Write-Host ""
Write-Host "ptt-sync complete: $ok linked, $err errors" -ForegroundColor $(if ($err -eq 0) { "Green" } else { "Red" })
if ($err -gt 0) { exit 1 }
