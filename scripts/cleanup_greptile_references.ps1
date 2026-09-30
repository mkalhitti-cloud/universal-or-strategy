# Greptile Reference Cleanup Script
# Purpose: Remove all Greptile MCP references from system prompts and documentation
# Date: 2026-06-21
# Context: Greptile MCP not used in any of the 10 V12 phases

param(
    [switch]$DryRun = $false,
    [switch]$ArchiveOnly = $false
)

Write-Host "=== Greptile Reference Cleanup ===" -ForegroundColor Cyan
Write-Host "Mode: $(if ($DryRun) { 'DRY RUN (no changes)' } else { 'LIVE (will modify files)' })" -ForegroundColor Yellow
Write-Host ""

# Files requiring manual review and cleanup
$criticalFiles = @(
    @{
        Path = "AGENTS.md"
        Lines = @(30)
        Issue = "Remove '02-greptile-report.md' reference"
        Priority = "P0"
    },
    @{
        Path = "docs/AGENTS.md"
        Lines = @(30)
        Issue = "Remove '02-greptile-report.md' reference"
        Priority = "P0"
    },
    @{
        Path = "docs/workflow/LOOP_ORCHESTRATION.md"
        Lines = @(92, 96, 100, 103, 106, 123, 124, 128, 150, 266, 270, 274, 280, 310, 334, 343, 344, 357)
        Issue = "Remove Greptile MCP and CLI references"
        Priority = "P1"
    },
    @{
        Path = "docs/workflow/HANDOFF_PROMPT_EPIC_LOOP.md"
        Lines = @(117, 234)
        Issue = "Remove Greptile MCP references"
        Priority = "P1"
    },
    @{
        Path = "docs/wave6/PHASE_MCP_VS_CUSTOM_MODES_ANALYSIS.md"
        Lines = @(109, 137, 145)
        Issue = "Remove Greptile MCP references from phase analysis"
        Priority = "P1"
    }
)

# Files to archive (obsolete Greptile documentation)
$archiveFiles = @(
    "docs/mcp/GREPTILE_MCP_TROUBLESHOOTING.md",
    "docs/protocol/GREPTILE_REMOVAL_PROTOCOL.md"
)

# Historical files (no action needed - for reference only)
$historicalFiles = @(
    "docs/pr22comments.md",
    "docs/pr20comments.md",
    "docs/pr14comments.md",
    "docs/pr13comments4.md",
    "WAVE5_PILOT_TEST_CONTINUATION_PROMPT.md",
    "WAVE4_PROTOCOL_HARDENING_PLAN.md",
    "docs/setup/TOKEN_ROTATION_INSTRUCTIONS.md",
    "docs/setup/ENVIRONMENT_VARIABLES_SETUP.md",
    "docs/protocol/WAVE6_VM_DEPLOYMENT_CHECKLIST.md",
    "docs/workflow/VM_UPDATE_PROCEDURE_PR_REMOVAL.md",
    "docs/protocol/VM_UPDATE_COMPLETE_2026-06-20.md",
    "docs/protocol/VM_SETUP_PROTOCOL.md",
    "docs/protocol/VM_MCP_REQUIREMENTS_MATRIX.md",
    "docs/protocol/VM_MCP_JSON_UPDATE.md",
    "docs/brain/WAVE7_CONTEXT_VERIFICATION.md",
    "docs/workflow/PR5_ORCHESTRATION_WORKFLOW.md",
    "docs/protocol/V12_41_MCP_FIX.md",
    "docs/workflow/PHASE2_COMMAND_UPDATES_SUMMARY.md",
    "docs/protocol/PR_LOOP_V2_HARDENING.md",
    "docs/workflow/MCP_UPDATE_PROTOCOL.md",
    "docs/protocol/ANTHROPIC_LAUNCH_YOUR_AGENT_INTEGRATION.md",
    "docs/protocol/BOB_IDE_CUSTOM_MODES_VS_MCP.md",
    "docs/protocol/CONTEXT_OPTIMIZATION_SUMMARY.md",
    "docs/workflow/BATCH_COMMIT_STRATEGY.md",
    "docs/workflow/GOLDEN_IMAGE_V2_TEST_RESULTS.md",
    "docs/wave6/MCP_CLEANUP_RECOMMENDATION.md",
    "docs/wave5phase5badrun.md"
)

# Step 1: Archive obsolete Greptile documentation
if (-not $ArchiveOnly) {
    Write-Host "Step 1: Archiving obsolete Greptile documentation..." -ForegroundColor Green
    $archiveDir = "docs/archive/greptile"
    
    if (-not $DryRun) {
        New-Item -ItemType Directory -Force -Path $archiveDir | Out-Null
    }
    
    foreach ($file in $archiveFiles) {
        if (Test-Path $file) {
            Write-Host "  [ARCHIVE] $file → $archiveDir/" -ForegroundColor Yellow
            if (-not $DryRun) {
                Move-Item $file $archiveDir -Force
            }
        } else {
            Write-Host "  [SKIP] $file (not found)" -ForegroundColor Gray
        }
    }
    Write-Host ""
}

# Step 2: Report critical files requiring manual review
Write-Host "Step 2: Critical files requiring manual review..." -ForegroundColor Green
foreach ($fileInfo in $criticalFiles) {
    $path = $fileInfo.Path
    $priority = $fileInfo.Priority
    $issue = $fileInfo.Issue
    $lines = $fileInfo.Lines -join ", "
    
    if (Test-Path $path) {
        Write-Host "  [$priority] $path" -ForegroundColor $(if ($priority -eq "P0") { "Red" } else { "Yellow" })
        Write-Host "      Lines: $lines" -ForegroundColor Gray
        Write-Host "      Issue: $issue" -ForegroundColor Gray
        Write-Host ""
    } else {
        Write-Host "  [SKIP] $path (not found)" -ForegroundColor Gray
    }
}

# Step 3: Report historical files (no action needed)
Write-Host "Step 3: Historical files (no action needed)..." -ForegroundColor Green
Write-Host "  Found $($historicalFiles.Count) historical files with Greptile references" -ForegroundColor Gray
Write-Host "  These are archived documentation and require no cleanup" -ForegroundColor Gray
Write-Host ""

# Step 4: Generate cleanup report
Write-Host "Step 4: Generating cleanup report..." -ForegroundColor Green
$reportPath = "docs/workflow/GREPTILE_CLEANUP_REPORT.md"

$reportContent = @"
# Greptile Reference Cleanup Report

**Date**: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
**Status**: $(if ($DryRun) { "DRY RUN" } else { "EXECUTED" })

## Summary

- **Critical Files**: $($criticalFiles.Count) files requiring manual review
- **Archived Files**: $($archiveFiles.Count) obsolete documentation files
- **Historical Files**: $($historicalFiles.Count) files (no action needed)

## Critical Files (Manual Review Required)

"@

foreach ($fileInfo in $criticalFiles) {
    $reportContent += @"

### [$($fileInfo.Priority)] $($fileInfo.Path)
**Lines**: $($fileInfo.Lines -join ", ")
**Issue**: $($fileInfo.Issue)
**Action**: Manual review and cleanup required

"@
}

$reportContent += @"

## Archived Files

"@

foreach ($file in $archiveFiles) {
    $status = if (Test-Path $file) { "✅ Archived" } else { "⚠️ Not found" }
    $reportContent += "- $status`: $file`n"
}

$reportContent += @"

## Historical Files (No Action Needed)

These files contain Greptile references but are historical documentation:

"@

foreach ($file in $historicalFiles) {
    $reportContent += "- $file`n"
}

$reportContent += @"

## Next Steps

1. **Manual Review**: Open each critical file and remove Greptile references
2. **Verify**: Run ``grep -r "greptile" docs/ AGENTS.md`` to confirm cleanup
3. **Test**: Run pilot epic to verify no Greptile MCP errors
4. **Update**: Mark this cleanup as complete in Wave 7 checklist

## Verification Command

``````powershell
# Check for remaining Greptile references in critical files
`$criticalPaths = @(
    "AGENTS.md",
    "docs/AGENTS.md",
    "docs/workflow/LOOP_ORCHESTRATION.md",
    "docs/workflow/HANDOFF_PROMPT_EPIC_LOOP.md",
    "docs/wave6/PHASE_MCP_VS_CUSTOM_MODES_ANALYSIS.md"
)

foreach (`$path in `$criticalPaths) {
    if (Test-Path `$path) {
        `$matches = Select-String -Path `$path -Pattern "greptile" -CaseSensitive:$false
        if (`$matches) {
            Write-Host "[FOUND] `$path has `$(`$matches.Count) Greptile references" -ForegroundColor Red
        } else {
            Write-Host "[CLEAN] `$path" -ForegroundColor Green
        }
    }
}
``````

---

**Generated by**: ``scripts/cleanup_greptile_references.ps1``
"@

if (-not $DryRun) {
    $reportContent | Out-File -FilePath $reportPath -Encoding UTF8
    Write-Host "  Report saved: $reportPath" -ForegroundColor Green
} else {
    Write-Host "  [DRY RUN] Would save report to: $reportPath" -ForegroundColor Yellow
}
Write-Host ""

# Step 5: Summary
Write-Host "=== Cleanup Summary ===" -ForegroundColor Cyan
Write-Host "Mode: $(if ($DryRun) { 'DRY RUN' } else { 'LIVE' })" -ForegroundColor Yellow
Write-Host "Archived: $($archiveFiles.Count) files" -ForegroundColor Green
Write-Host "Manual Review: $($criticalFiles.Count) files" -ForegroundColor Yellow
Write-Host "Historical: $($historicalFiles.Count) files (no action)" -ForegroundColor Gray
Write-Host ""

if ($DryRun) {
    Write-Host "Run without -DryRun to execute cleanup" -ForegroundColor Yellow
} else {
    Write-Host "✅ Cleanup complete!" -ForegroundColor Green
    Write-Host "Next: Manually review critical files listed above" -ForegroundColor Yellow
}

# Made with Bob
