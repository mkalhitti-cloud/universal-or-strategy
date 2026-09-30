# sync-ptt-to-nt8.ps1
# Sync ONLY production .cs files from src/PropTraderTools/ to NT8 Custom AddOns folder.
# Excludes: Tests/, obj/, bin/, CopyEngineTests.cs, *.bak, *Tests.cs
# Usage: powershell -File scripts\sync-ptt-to-nt8.ps1

$src = Join-Path $PSScriptRoot "..\src\PropTraderTools"
$dst = "$env:USERPROFILE\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools"

# Subdirectories to EXCLUDE from sync (never go into NT8 Custom)
$excludeDirs = @("Tests", "obj", "bin")

# File patterns to EXCLUDE from sync
$excludePatterns = @("*Tests.cs", "CopyEngineTests.cs", "*.bak")

if (-not (Test-Path $dst)) {
    New-Item -ItemType Directory -Path $dst -Force | Out-Null
}

$copied  = 0
$skipped = 0
$excluded = 0

Get-ChildItem $src -Filter "*.cs" -Recurse | ForEach-Object {
    $file = $_
    $rel  = $file.FullName.Substring((Resolve-Path $src).Path.Length + 1)

    # Check if this file lives inside an excluded directory
    $inExcludedDir = $false
    foreach ($ex in $excludeDirs) {
        if ($rel.StartsWith($ex + "\") -or $rel.StartsWith($ex + "/")) {
            $inExcludedDir = $true
            break
        }
    }
    if ($inExcludedDir) { $excluded++; return }

    # Check if filename matches an excluded pattern
    $isExcluded = $false
    foreach ($pat in $excludePatterns) {
        if ($file.Name -like $pat) { $isExcluded = $true; break }
    }
    if ($isExcluded) { $excluded++; return }

    # Sync the file
    $target    = Join-Path $dst $rel
    $targetDir = Split-Path $target
    if (-not (Test-Path $targetDir)) {
        New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    }

    $srcHash = (Get-FileHash $file.FullName -Algorithm MD5).Hash
    $dstHash = if (Test-Path $target) { (Get-FileHash $target -Algorithm MD5).Hash } else { "" }

    if ($srcHash -ne $dstHash) {
        Copy-Item $file.FullName $target -Force
        Write-Host "COPIED:   $rel"
        $copied++
    } else {
        $skipped++
    }
}

Write-Host ""
Write-Host "Done. Copied: $copied  Skipped (in sync): $skipped  Excluded (tests/obj/bin): $excluded"
