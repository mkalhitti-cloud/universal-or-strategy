# verify_critical_symbols.ps1
# Guardian: all critical B183 symbols must be present in CopyEngine.cs + TradeCopierPanel.cs
# Run: powershell -File .\scripts\verify_critical_symbols.ps1
# Exit 0 = PASS. Exit 1 = FAIL (lists missing symbols).

$engineFile = "src/PropTraderTools/CopyEngine.cs"
$panelFile  = "src/PropTraderTools/TradeCopierPanel.cs"
foreach ($f in @($engineFile, $panelFile)) {
    if (-not (Test-Path $f)) { Write-Error "$f not found"; exit 1 }
}

$engine = Get-Content $engineFile -Raw
$panel  = Get-Content $panelFile  -Raw

# B183-specific symbols (re-introduced this pipeline)
$requiredEngine_B183 = @(
    "_followerSlateReady",
    "TryMarkFollowerSlateNotReady",
    "TrySetFollowerSlateReady",
    "IsAccountFlat",
    "HasAnyActiveOrders",
    "OnFollowerPositionUpdate",
    "FlatGuardLoop",
    "TryLaneDHook",
    "_laneDDispatched",
    "TryLaneDDispatchFollower",
    "TryLaneEHook",
    "_laneEDispatched",
    "TryLaneEDispatchFollower",
    "BuildLaneEKey",
    "IsLaneEEligible",
    "PositionUpdate"
)

# Permanent guardian symbols -- must ALWAYS be present regardless of any future pipeline
# Any pipeline that removes one of these requires Director explicit approval + DW-card
$requiredEngine_permanent = @(
    "FindFollowerBracketOrder",
    "EvictDedup",
    "TryCancelFollowerEntries",
    "CancelScopedFollowerEntries",
    "IsLiveEntryBlocked",
    "IsReversalToFlatFollower",
    "ShouldSkipForReversalGuard",
    "TryDispatchLeaderFlat",
    "DispatchCopy",
    "OnOrderUpdate",
    "Subscribe",
    "Unsubscribe"
)

$requiredEngine = $requiredEngine_B183 + $requiredEngine_permanent

$requiredPanel = @(
    "OnPendingBeArmedDispatch",
    "OnGlobalBeAllDisarmed",
    "UpdateBeAllVisuals",
    "DisarmBeAllOnFlat",
    "PendingBeArmed",
    "GlobalBeAllDisarmed"
)

$missing = @()
foreach ($sym in $requiredEngine) {
    if ($engine -notmatch [regex]::Escape($sym)) { $missing += "CopyEngine: $sym" }
}
foreach ($sym in $requiredPanel) {
    if ($panel -notmatch [regex]::Escape($sym)) { $missing += "TradeCopierPanel: $sym" }
}

# BUG3 visual check: Quick2t must have BrushTeal background (not just border)
if ($panel -match "Quick2t" -and $panel -notmatch "instr2tBtn\.Background\s*=\s*BrushTeal") {
    $missing += "TradeCopierPanel: _instr2tBtn.Background = BrushTeal (Quick2t visual fix)"
}

# Single-hunk ban on CopyEngine.cs
$hunkCount = (git diff HEAD~1 HEAD -- $engineFile 2>$null | Select-String "^@@").Count
if ($hunkCount -eq 1) {
    Write-Error "SINGLE-HUNK BAN: CopyEngine.cs diff is a full-file replace. B183-class regression risk."
    $missing += "[SINGLE-HUNK-REPLACE-DETECTED: CopyEngine.cs]"
}

# Deletion audit: catch unauthorised symbol removals
$deletedSymbols = git diff HEAD~1 HEAD -- $engineFile 2>$null |
    Select-String "^-\s+(private|internal|public)" |
    ForEach-Object { $_.Line.Trim() }
if ($deletedSymbols.Count -gt 0) {
    Write-Warning "DELETION AUDIT: $($deletedSymbols.Count) deleted symbol(s) detected in CopyEngine.cs:"
    $deletedSymbols | ForEach-Object { Write-Warning "  $_" }
    $missing += "[DELETION-AUDIT: $($deletedSymbols.Count) symbols deleted -- each requires DW-card]"
}

# Commit size gate: >300 deleted or added lines requires Director approval
$deletedLines = (git diff HEAD~1 HEAD -- $engineFile 2>$null | Select-String "^-" | Measure-Object).Count
$addedLines   = (git diff HEAD~1 HEAD -- $engineFile 2>$null | Select-String "^\+" | Measure-Object).Count
if ($deletedLines -gt 300 -or $addedLines -gt 300) {
    Write-Warning "COMMIT SIZE GATE: deleted=$deletedLines added=$addedLines on CopyEngine.cs."
    Write-Warning "  >300 lines requires Director explicit approval documented in the ticket."
    $missing += "[COMMIT-SIZE-GATE: del=$deletedLines add=$addedLines -- Director approval required if >300]"
}

if ($missing.Count -gt 0) {
    Write-Error "GUARDIAN FAIL -- missing symbols and/or gate violations:"
    $missing | ForEach-Object { Write-Error "  - $_" }
    exit 1
}

Write-Host "GUARDIAN PASS -- all symbols present. Hunk=$hunkCount Del=$deletedLines Add=$addedLines"
exit 0
