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
    @{ src = "V12_002.Orders.CancelGateway.cs"; dst = Join-Path $NtStrategyDir "V12_002.Orders.CancelGateway.cs" },
    @{ src = "V12_002.Photon.MmioMirror.cs"; dst = Join-Path $NtStrategyDir "V12_002.Photon.MmioMirror.cs" },
    @{ src = "V12_002.Photon.Pool.cs"; dst = Join-Path $NtStrategyDir "V12_002.Photon.Pool.cs" },
    @{ src = "V12_002.Photon.Ring.cs"; dst = Join-Path $NtStrategyDir "V12_002.Photon.Ring.cs" },
    @{ src = "V12_002.Photon.Substrate.cs"; dst = Join-Path $NtStrategyDir "V12_002.Photon.Substrate.cs" },
    @{ src = "V12_002.Safety.Watchdog.cs"; dst = Join-Path $NtStrategyDir "V12_002.Safety.Watchdog.cs" },
    @{ src = "V12_002.SIMA.Shadow.cs"; dst = Join-Path $NtStrategyDir "V12_002.SIMA.Shadow.cs" },
    @{ src = "V12_002.StickyState.cs"; dst = Join-Path $NtStrategyDir "V12_002.StickyState.cs" },
    @{ src = "V12_002.StructuredLog.cs"; dst = Join-Path $NtStrategyDir "V12_002.StructuredLog.cs" },
    @{ src = "V12_002.Telemetry.cs"; dst = Join-Path $NtStrategyDir "V12_002.Telemetry.cs" },
    @{ src = "V12_002.UI.Panel.Brushes.cs"; dst = Join-Path $NtStrategyDir "V12_002.UI.Panel.Brushes.cs" },
    @{ src = "V12_002.UI.Panel.Construction.cs"; dst = Join-Path $NtStrategyDir "V12_002.UI.Panel.Construction.cs" },
    @{ src = "V12_002.UI.Panel.Handlers.cs"; dst = Join-Path $NtStrategyDir "V12_002.UI.Panel.Handlers.cs" },
    @{ src = "V12_002.UI.Panel.Helpers.cs"; dst = Join-Path $NtStrategyDir "V12_002.UI.Panel.Helpers.cs" },
    @{ src = "V12_002.UI.Panel.Lifecycle.cs"; dst = Join-Path $NtStrategyDir "V12_002.UI.Panel.Lifecycle.cs" },
    @{ src = "V12_002.UI.Panel.StateSync.cs"; dst = Join-Path $NtStrategyDir "V12_002.UI.Panel.StateSync.cs" },
    @{ src = "V12_002.UI.Snapshot.cs"; dst = Join-Path $NtStrategyDir "V12_002.UI.Snapshot.cs" }
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
    @{ src = "V12_002.MetadataGuard.cs"; dst = Join-Path $NtStrategyDir "V12_002.MetadataGuard.cs" },
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

Write-Host "--- WSGTA DEPLOY SYNC: Hardening Environment ---" -ForegroundColor Cyan

foreach ($map in $Mappings) {
    if ($map.src -match "Indicator") {
        $srcPath = Join-Path (Join-Path $RepoRoot "src") $map.src
    }
    else {
        $srcPath = Join-Path (Join-Path $RepoRoot "src") $map.src
    }
    
    $dstPath = $map.dst
    
    if (!(Test-Path $srcPath)) {
        Write-Host "SKIP: Source missing -> src/$($map.src)" -ForegroundColor Gray
        continue
    }

    # Ensure Target Directory Exists
    $targetDir = Split-Path $dstPath
    if (!(Test-Path $targetDir)) { New-Item -ItemType Directory -Path $targetDir }

    # Sync Logic
    if (Test-Path $dstPath) {
        $item = Get-Item $dstPath
        # If it's a file but NOT a link, backup it
        # Check if it's already a link to the current source
        $isLink = $item.Attributes -match "ReparsePoint"
        
        if ($isLink) {
            # Verify it points to the right place or just recreate it
            Remove-Item $dstPath -Force
        }
        else {
            # Backup if it's a real file (to avoid losing work)
            $backup = $dstPath + ".bak_" + (Get-Date -Format "yyyyMMdd_HHmm")
            Write-Host "BACKUP: Archiving existing NT file -> $(Split-Path $backup -Leaf)" -ForegroundColor Yellow
            Move-Item $dstPath $backup
        }
    }

    # Create the Link
    Write-Host "LINKING: $($map.src) -> NT8" -ForegroundColor Green
    New-Item -ItemType HardLink -Path $dstPath -Value $srcPath | Out-Null
}

Write-Host "`n--- SYNC COMPLETE: One Source of Truth Established ---" -ForegroundColor Cyan
Write-Host "Tip: Edit files in $RepoRoot. NT8 will update instantly." -ForegroundColor Gray
