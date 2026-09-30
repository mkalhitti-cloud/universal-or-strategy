# ptt-sync-and-verify.ps1
# Hardened single-command sync: copy production .cs to NT8, MD5-verify every file,
# then print mandatory compile instruction.
#
# Usage:     powershell -File scripts\ptt-sync-and-verify.ps1
# Replaces:  powershell -File .\deploy-sync.ps1  (archived, never worked)
#            powershell -File scripts\sync-ptt-to-nt8.ps1  (no verify step)
#
# MANDATORY: After this script completes with 0 MISMATCH lines, press F5 in
#            NinjaTrader 8 (or Tools -> Edit NinjaScript -> Compile) to recompile.
#            File copy alone does NOT activate new code in NT8.

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$src = Join-Path $PSScriptRoot "..\src\PropTraderTools"
$dst = "$env:USERPROFILE\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools"

$excludeDirs     = @("Tests", "obj", "bin")
$excludePatterns = @("*Tests.cs", "CopyEngineTests.cs", "*.bak")

if (-not (Test-Path $dst)) {
    New-Item -ItemType Directory -Path $dst -Force | Out-Null
}

$copied    = 0
$skipped   = 0
$excluded  = 0
$mismatches = @()

# ── Phase 1: Copy ────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "=== PTT SYNC: src/PropTraderTools -> NT8 AddOns ===" -ForegroundColor Cyan

Get-ChildItem $src -Filter "*.cs" -Recurse | ForEach-Object {
    $file = $_
    $rel  = $file.FullName.Substring((Resolve-Path $src).Path.Length + 1)

    $inExcludedDir = $false
    foreach ($ex in $excludeDirs) {
        if ($rel.StartsWith($ex + "\") -or $rel.StartsWith($ex + "/")) {
            $inExcludedDir = $true; break
        }
    }
    if ($inExcludedDir) { $excluded++; return }

    $isExcluded = $false
    foreach ($pat in $excludePatterns) {
        if ($file.Name -like $pat) { $isExcluded = $true; break }
    }
    if ($isExcluded) { $excluded++; return }

    $target    = Join-Path $dst $rel
    $targetDir = Split-Path $target
    if (-not (Test-Path $targetDir)) {
        New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    }

    $srcHash = (Get-FileHash $file.FullName -Algorithm MD5).Hash
    $dstHash = if (Test-Path $target) { (Get-FileHash $target -Algorithm MD5).Hash } else { "" }

    if ($srcHash -ne $dstHash) {
        Copy-Item $file.FullName $target -Force
        Write-Host "  COPIED:  $rel" -ForegroundColor Yellow
        $copied++
    } else {
        $skipped++
    }
}

Write-Host ""
Write-Host "  Copied:   $copied  |  In-sync: $skipped  |  Excluded: $excluded"

# ── Phase 2: Verify (re-hash every production file after copy) ───────────────
Write-Host ""
Write-Host "=== PTT VERIFY: MD5 check every synced file ===" -ForegroundColor Cyan

Get-ChildItem $src -Filter "*.cs" -Recurse | ForEach-Object {
    $file = $_
    $rel  = $file.FullName.Substring((Resolve-Path $src).Path.Length + 1)

    $inExcludedDir = $false
    foreach ($ex in $excludeDirs) {
        if ($rel.StartsWith($ex + "\") -or $rel.StartsWith($ex + "/")) {
            $inExcludedDir = $true; break
        }
    }
    if ($inExcludedDir) { return }

    $isExcluded = $false
    foreach ($pat in $excludePatterns) {
        if ($file.Name -like $pat) { $isExcluded = $true; break }
    }
    if ($isExcluded) { return }

    $target  = Join-Path $dst $rel
    $srcHash = (Get-FileHash $file.FullName  -Algorithm MD5).Hash
    $dstHash = if (Test-Path $target) { (Get-FileHash $target -Algorithm MD5).Hash } else { "MISSING" }

    if ($srcHash -eq $dstHash) {
        Write-Host "  OK       $rel" -ForegroundColor Green
    } else {
        Write-Host "  MISMATCH $rel  (src=$srcHash  dst=$dstHash)" -ForegroundColor Red
        $mismatches += $rel
    }
}

# ── Summary ──────────────────────────────────────────────────────────────────
Write-Host ""
if ($mismatches.Count -eq 0) {
    Write-Host "=== SYNC + VERIFY: PASS ($($copied + $skipped) files confirmed) ===" -ForegroundColor Green
    Write-Host ""
    Write-Host "NEXT STEP (MANDATORY):" -ForegroundColor Magenta
    Write-Host "  Press F5 in NinjaTrader 8, or go to:" -ForegroundColor Magenta
    Write-Host "  Tools -> Edit NinjaScript -> Compile" -ForegroundColor Magenta
    Write-Host "  File copy alone does NOT activate the new code." -ForegroundColor Magenta
    exit 0
} else {
    Write-Host "=== SYNC + VERIFY: FAIL ($($mismatches.Count) MISMATCH) ===" -ForegroundColor Red
    Write-Host "  Mismatched files:" -ForegroundColor Red
    $mismatches | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
    Write-Host "  Re-run this script. If it persists, check file locks in NT8." -ForegroundColor Red
    exit 1
}
