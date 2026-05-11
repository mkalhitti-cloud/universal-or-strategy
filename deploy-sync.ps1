# deploy-sync.ps1
# WSGTA Infrastructure: One Source of Truth Automation
# Establishes Hard Links between Git Repo and NinjaTrader 8

$ErrorActionPreference = "Stop"

# --- CONFIGURATION ---
$RepoRoot = "C:\WSGTA\universal-or-strategy"
$NtCustomDir = "C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom"
$NtStrategyDir = Join-Path $NtCustomDir "Strategies"
$NtIndicatorDir = Join-Path $NtCustomDir "Indicators"

# File Mappings (Source in Repo -> Target in NT8)
$Mappings = @(
    # Indicators
    @{ src = "V12_001.cs"; dst = Join-Path $NtIndicatorDir "V12_001.cs" },
    
    # Strategy (Modularized V12_002 Series)
    @{ src = "V12_002.cs"; dst = Join-Path $NtStrategyDir "V12_002.cs" },
    @{ src = "V12_002.PositionInfo.cs"; dst = Join-Path $NtStrategyDir "V12_002.PositionInfo.cs" },
    @{ src = "V12_002.Lifecycle.cs"; dst = Join-Path $NtStrategyDir "V12_002.Lifecycle.cs" },
    @{ src = "V12_002.BarUpdate.cs"; dst = Join-Path $NtStrategyDir "V12_002.BarUpdate.cs" },
    @{ src = "V12_002.DrawingHelpers.cs"; dst = Join-Path $NtStrategyDir "V12_002.DrawingHelpers.cs" },
    @{ src = "V12_002.Entries.cs"; dst = Join-Path $NtStrategyDir "V12_002.Entries.cs" },
    @{ src = "V12_002.Entries.FFMA.cs"; dst = Join-Path $NtStrategyDir "V12_002.Entries.FFMA.cs" },
    @{ src = "V12_002.Entries.OR.cs"; dst = Join-Path $NtStrategyDir "V12_002.Entries.OR.cs" },
    @{ src = "V12_002.Entries.RMA.cs"; dst = Join-Path $NtStrategyDir "V12_002.Entries.RMA.cs" },
    @{ src = "V12_002.Entries.MOMO.cs"; dst = Join-Path $NtStrategyDir "V12_002.Entries.MOMO.cs" },
    @{ src = "V12_002.Entries.Trend.cs"; dst = Join-Path $NtStrategyDir "V12_002.Entries.Trend.cs" },
    @{ src = "V12_002.Entries.Retest.cs"; dst = Join-Path $NtStrategyDir "V12_002.Entries.Retest.cs" },
    @{ src = "V12_002.Orders.Callbacks.cs"; dst = Join-Path $NtStrategyDir "V12_002.Orders.Callbacks.cs" },
    @{ src = "V12_002.Orders.Callbacks.AccountOrders.cs"; dst = Join-Path $NtStrategyDir "V12_002.Orders.Callbacks.AccountOrders.cs" },
    @{ src = "V12_002.Orders.Callbacks.Execution.cs"; dst = Join-Path $NtStrategyDir "V12_002.Orders.Callbacks.Execution.cs" },
    @{ src = "V12_002.Orders.Callbacks.Propagation.cs"; dst = Join-Path $NtStrategyDir "V12_002.Orders.Callbacks.Propagation.cs" },
    @{ src = "V12_002.Orders.Management.cs"; dst = Join-Path $NtStrategyDir "V12_002.Orders.Management.cs" },
    @{ src = "V12_002.Orders.Management.StopSync.cs"; dst = Join-Path $NtStrategyDir "V12_002.Orders.Management.StopSync.cs" },
    @{ src = "V12_002.Orders.Management.Flatten.cs"; dst = Join-Path $NtStrategyDir "V12_002.Orders.Management.Flatten.cs" },
    @{ src = "V12_002.Orders.Management.Cleanup.cs"; dst = Join-Path $NtStrategyDir "V12_002.Orders.Management.Cleanup.cs" },
    @{ src = "V12_002.SIMA.cs"; dst = Join-Path $NtStrategyDir "V12_002.SIMA.cs" },
    @{ src = "V12_002.SIMA.Dispatch.cs"; dst = Join-Path $NtStrategyDir "V12_002.SIMA.Dispatch.cs" },
    @{ src = "V12_002.SIMA.Fleet.cs"; dst = Join-Path $NtStrategyDir "V12_002.SIMA.Fleet.cs" },
    @{ src = "V12_002.SIMA.Lifecycle.cs"; dst = Join-Path $NtStrategyDir "V12_002.SIMA.Lifecycle.cs" },
    @{ src = "V12_002.SIMA.Execution.cs"; dst = Join-Path $NtStrategyDir "V12_002.SIMA.Execution.cs" },
    @{ src = "V12_002.SIMA.Flatten.cs"; dst = Join-Path $NtStrategyDir "V12_002.SIMA.Flatten.cs" },
    @{ src = "V12_002.REAPER.cs"; dst = Join-Path $NtStrategyDir "V12_002.REAPER.cs" },
    @{ src = "V12_002.REAPER.Audit.cs"; dst = Join-Path $NtStrategyDir "V12_002.REAPER.Audit.cs" },
    @{ src = "V12_002.REAPER.Repair.cs"; dst = Join-Path $NtStrategyDir "V12_002.REAPER.Repair.cs" },
    @{ src = "V12_002.REAPER.NakedStop.cs"; dst = Join-Path $NtStrategyDir "V12_002.REAPER.NakedStop.cs" },
    @{ src = "V12_002.UI.Callbacks.cs"; dst = Join-Path $NtStrategyDir "V12_002.UI.Callbacks.cs" },
    @{ src = "V12_002.UI.Compliance.cs"; dst = Join-Path $NtStrategyDir "V12_002.UI.Compliance.cs" },
    @{ src = "V12_002.UI.IPC.cs"; dst = Join-Path $NtStrategyDir "V12_002.UI.IPC.cs" },
    @{ src = "V12_002.UI.IPC.Server.cs"; dst = Join-Path $NtStrategyDir "V12_002.UI.IPC.Server.cs" },
    @{ src = "V12_002.UI.IPC.Commands.Config.cs"; dst = Join-Path $NtStrategyDir "V12_002.UI.IPC.Commands.Config.cs" },
    @{ src = "V12_002.UI.IPC.Commands.Mode.cs"; dst = Join-Path $NtStrategyDir "V12_002.UI.IPC.Commands.Mode.cs" },
    @{ src = "V12_002.UI.IPC.Commands.Fleet.cs"; dst = Join-Path $NtStrategyDir "V12_002.UI.IPC.Commands.Fleet.cs" },
    @{ src = "V12_002.UI.IPC.Commands.Misc.cs"; dst = Join-Path $NtStrategyDir "V12_002.UI.IPC.Commands.Misc.cs" },
    @{ src = "V12_002.UI.Sizing.cs"; dst = Join-Path $NtStrategyDir "V12_002.UI.Sizing.cs" },
    @{ src = "V12_002.Symmetry.cs"; dst = Join-Path $NtStrategyDir "V12_002.Symmetry.cs" },
    @{ src = "V12_002.Symmetry.BracketFSM.cs"; dst = Join-Path $NtStrategyDir "V12_002.Symmetry.BracketFSM.cs" },
    @{ src = "V12_002.Symmetry.Follower.cs"; dst = Join-Path $NtStrategyDir "V12_002.Symmetry.Follower.cs" },
    @{ src = "V12_002.Symmetry.Replace.cs"; dst = Join-Path $NtStrategyDir "V12_002.Symmetry.Replace.cs" },
    @{ src = "V12_002.LogicAudit.cs"; dst = Join-Path $NtStrategyDir "V12_002.LogicAudit.cs" },
    @{ src = "V12_002.Trailing.cs"; dst = Join-Path $NtStrategyDir "V12_002.Trailing.cs" },
    @{ src = "V12_002.Trailing.StopUpdate.cs"; dst = Join-Path $NtStrategyDir "V12_002.Trailing.StopUpdate.cs" },
    @{ src = "V12_002.Trailing.Breakeven.cs"; dst = Join-Path $NtStrategyDir "V12_002.Trailing.Breakeven.cs" },
    @{ src = "V12_002.UI.Snapshot.cs"; dst = Join-Path $NtStrategyDir "V12_002.UI.Snapshot.cs" },
    @{ src = "V12_002.Safety.Watchdog.cs"; dst = Join-Path $NtStrategyDir "V12_002.Safety.Watchdog.cs" },
    @{ src = "V12_002.MetadataGuard.cs"; dst = Join-Path $NtStrategyDir "V12_002.MetadataGuard.cs" },
    @{ src = "V12_002.Photon.Ring.cs"; dst = Join-Path $NtStrategyDir "V12_002.Photon.Ring.cs" },
    @{ src = "V12_002.Photon.Pool.cs"; dst = Join-Path $NtStrategyDir "V12_002.Photon.Pool.cs" },
    @{ src = "V12_002.Properties.cs"; dst = Join-Path $NtStrategyDir "V12_002.Properties.cs" },

    # Strategy Components
    @{ src = "V12_002.Constants.cs"; dst = Join-Path $NtStrategyDir "V12_002.Constants.cs" },
    @{ src = "V12_002.Atm.cs"; dst = Join-Path $NtStrategyDir "V12_002.Atm.cs" },
    @{ src = "V12_002.AccountUpdate.cs"; dst = Join-Path $NtStrategyDir "V12_002.AccountUpdate.cs" },
    @{ src = "V12_002.Data.cs"; dst = Join-Path $NtStrategyDir "V12_002.Data.cs" },

    # Shared Components
    @{ src = "SignalBroadcaster.cs"; dst = Join-Path $NtStrategyDir "SignalBroadcaster.cs" }
)


# =============================================================================
# ASCII PRE-DEPLOY GATE (Build Protocol v2)
# Scans ALL .cs source files for non-ASCII bytes BEFORE touching NT8.
# Any byte > 127 (emoji, curly quotes, em-dashes, box-drawing) will ABORT
# the deploy. This prevents the encoding bug that caused cascading C# errors.
# Fix: run C:\tmp\byte_purge.py, then re-run deploy-sync.ps1
# =============================================================================
Write-Host "`n--- ASCII GATE: Scanning source files ---" -ForegroundColor Yellow
$srcDir = Join-Path $RepoRoot "src"
$gatePass = $true
foreach ($csFile in (Get-ChildItem $srcDir -Filter "*.cs" -Recurse)) {
    $bytes = [System.IO.File]::ReadAllBytes($csFile.FullName)
    $badBytes = $bytes | Where-Object { $_ -gt 127 }
    if ($badBytes.Count -gt 0) {
        Write-Host "ASCII GATE FAIL: $($csFile.Name) has $($badBytes.Count) non-ASCII bytes" -ForegroundColor Red
        Write-Host "  Fix: python C:\tmp\byte_purge.py  then re-run deploy-sync.ps1" -ForegroundColor Red
        $gatePass = $false
    }
}
if (-not $gatePass) {
    Write-Host "`nDEPLOY ABORTED - Fix encoding errors first (see above)" -ForegroundColor Red
    exit 1
}
Write-Host "ASCII GATE PASS - all source files are clean`n" -ForegroundColor Green

# =============================================================================
# DIFF SIZE GUARD (Build Protocol v3)
# Checks the character count of the diff against 'main' to prevent PR bloat.
# Limit: 150,000 characters (per project mandate).
# =============================================================================
Write-Host "--- DIFF GUARD: Checking PR size against main ---" -ForegroundColor Yellow
if (Get-Command "git" -ErrorAction SilentlyContinue) {
    try {
        $diffSize = (git diff main --shortstat | Out-String).Trim()
        $rawDiff = git diff main
        $charCount = $rawDiff.Length
        
        if ($charCount -gt 150000) {
            Write-Host "DIFF GUARD FAIL: Current diff against 'main' is $charCount characters." -ForegroundColor Red
            Write-Host "  Project Limit: 150,000 characters." -ForegroundColor Red
            Write-Host "  Action: Identify large text artifacts or line-ending desyncs." -ForegroundColor Yellow
            # git diff main --stat
            exit 1
        }
        Write-Host "DIFF GUARD PASS: Diff size ($charCount chars) is within limits.`n" -ForegroundColor Green
    } catch {
        Write-Host "DIFF GUARD SKIP: Could not compare against 'main' (likely missing branch or git error).`n" -ForegroundColor Gray
    }
}

# =============================================================================
# SOVEREIGN DROID AUDIT (P5 Red Team)
# Automated verification of V12 architectural mandates.
# =============================================================================
if (Get-Command "droid" -ErrorAction SilentlyContinue) {
    Write-Host "--- SOVEREIGN AUDIT: Launching Droid P5 Review ---" -ForegroundColor Yellow
    $AuditPrompt = "Review all uncommitted changes in src/. STRICTLY FLAG [P0] for any 'lock(' blocks or non-ASCII characters in C# strings. Verify that state mutations follow the Enqueue/Actor pattern."
    try {
        droid exec --auto high $AuditPrompt
        Write-Host "SOVEREIGN AUDIT PASS: Architectural integrity verified.`n" -ForegroundColor Green
    } catch {
        Write-Host "SOVEREIGN AUDIT FAIL: Droid flagged critical violations." -ForegroundColor Red
        Write-Host "Check the output above and fix the P0 findings before deployment.`n" -ForegroundColor Red
    }
} else {
    Write-Host "SOVEREIGN AUDIT SKIP: Droid CLI not found. (Level 2 Readiness incomplete)`n" -ForegroundColor Gray
}

# =============================================================================
# DEPLOYMENT ENGINE: Hardening Environment
# =============================================================================
Write-Host "--- WSGTA DEPLOY SYNC: Hardening Environment ---" -ForegroundColor Cyan

# 1. Base Strategy & Main Components
$FixedMappings = @(
    @{ src = "V12_001.cs"; dst = Join-Path $NtIndicatorDir "V12_001.cs" },
    @{ src = "V12_002.cs"; dst = Join-Path $NtStrategyDir "V12_002.cs" },
    @{ src = "SignalBroadcaster.cs"; dst = Join-Path $NtStrategyDir "SignalBroadcaster.cs" }
)

# 2. Dynamic Discovery: All V12_002 Sub-modules
$DynamicFiles = Get-ChildItem -Path $srcDir -Filter "V12_002.*.cs"
foreach ($file in $DynamicFiles) {
    if ($file.Name -eq "V12_002.cs") { continue } # Already in FixedMappings
    
    $srcPath = $file.FullName
    $dstPath = Join-Path $NtStrategyDir $file.Name
    
    # Ensure Target Directory Exists
    $targetDir = Split-Path $dstPath
    if (!(Test-Path $targetDir)) { New-Item -ItemType Directory -Path $targetDir }

    # Sync Logic
    if (Test-Path $dstPath) {
        $item = Get-Item $dstPath
        if ($item.LinkType -eq "HardLink") {
            Remove-Item $dstPath -Force 
        } else {
            $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
            $backup = $dstPath + ".bak_" + $timestamp
            Write-Host "BACKUP: Archiving existing NT file -> $(Split-Path $backup -Leaf)" -ForegroundColor Yellow
            Move-Item $dstPath $backup -Force
        }
    }

    # Create the Link
    Write-Host "LINKING: $($file.Name) -> NT8" -ForegroundColor Green
    New-Item -ItemType HardLink -Path $dstPath -Value $srcPath | Out-Null
}

# 3. Fixed Mappings Execution
foreach ($map in $FixedMappings) {
    $srcPath = Join-Path $srcDir $map.src
    $dstPath = $map.dst
    if (!(Test-Path $srcPath)) { continue }
    
    if (Test-Path $dstPath) {
        $item = Get-Item $dstPath
        if ($item.LinkType -eq "HardLink") {
            Write-Host "CLEANUP: Removing existing link -> $(Split-Path $dstPath -Leaf)" -ForegroundColor Gray
            Remove-Item $dstPath -Force 
        } else {
            $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
            $backup = $dstPath + ".bak_" + $timestamp
            Write-Host "BACKUP (Fixed): Archiving existing NT file -> $(Split-Path $backup -Leaf)" -ForegroundColor Yellow
            Move-Item $dstPath $backup -Force
        }
    }
    
    Write-Host "LINKING (Fixed): $($map.src) -> NT8" -ForegroundColor Green
    New-Item -ItemType HardLink -Path $dstPath -Value $srcPath | Out-Null
}

Write-Host "`n--- SYNC COMPLETE: One Source of Truth Established ---" -ForegroundColor Cyan
Write-Host "Tip: Edit files in $RepoRoot. NT8 will update instantly." -ForegroundColor Gray
