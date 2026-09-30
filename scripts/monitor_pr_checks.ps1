#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Monitor GitHub PR checks and wait for bot responses.

.DESCRIPTION
    Implements the PR Loop V2 polling protocol:
    - Sleep 5 minutes initially after push
    - Poll every 3 minutes until all checks complete
    - Extract bot comments and calculate PHS
    - Exit when checks complete or timeout reached

.PARAMETER PrNumber
    GitHub Pull Request number to monitor

.PARAMETER InitialWait
    Initial sleep duration in seconds (default: 240 = 4 minutes)

.PARAMETER PollInterval
    Polling interval in seconds (default: 180 = 3 minutes)

.PARAMETER Timeout
    Maximum wait time in minutes (default: 30)

.EXAMPLE
    .\monitor_pr_checks.ps1 -PrNumber 7
    Monitor PR #7 with default timings (5 min initial, 3 min poll)

.EXAMPLE
    .\monitor_pr_checks.ps1 -PrNumber 7 -InitialWait 60 -PollInterval 60
    Monitor PR #7 with custom timings (1 min initial, 1 min poll)

.NOTES
    Part of V12 PR Loop V2 autonomous workflow.
    Requires GitHub CLI (gh) to be installed and authenticated.
#>

param(
    [Parameter(Mandatory=$true)]
    [int]$PrNumber,

    [Parameter(Mandatory=$false)]
    [int]$InitialWait = 240,  # 4 minutes

    [Parameter(Mandatory=$false)]
    [int]$PollInterval = 180,  # 3 minutes

    [Parameter(Mandatory=$false)]
    [int]$Timeout = 30  # 30 minutes
)

$ErrorActionPreference = "Stop"
$startTime = Get-Date

function Write-Status {
    param([string]$Message)
    $elapsed = [math]::Round(((Get-Date) - $startTime).TotalMinutes, 1)
    Write-Host "[${elapsed}m] $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "[OK] $Message" -ForegroundColor Green
}

function Write-Warning-Custom {
    param([string]$Message)
    Write-Host "[WARN] $Message" -ForegroundColor Yellow
}

function Write-Error-Custom {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

function Get-PRChecks {
    param([int]$PrNumber)
    
    try {
        $json = gh pr checks $PrNumber --json state,name,bucket,completedAt 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Error-Custom "Failed to fetch PR checks: $json"
            return $null
        }
        return $json | ConvertFrom-Json
    }
    catch {
        Write-Error-Custom "Exception fetching PR checks: $_"
        return $null
    }
}

function Get-PRComments {
    param([int]$PrNumber)
    
    try {
        $json = gh pr view $PrNumber --json comments,reviews 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Error-Custom "Failed to fetch PR comments: $json"
            return $null
        }
        return $json | ConvertFrom-Json
    }
    catch {
        Write-Error-Custom "Exception fetching PR comments: $_"
        return $null
    }
}

function Test-AllChecksComplete {
    param($Checks)
    
    if ($null -eq $Checks -or $Checks.Count -eq 0) {
        return $false
    }
    
    foreach ($check in $Checks) {
        if ($check.state -eq "PENDING" -or $check.state -eq "IN_PROGRESS") {
            return $false
        }
    }
    return $true
}

function Get-ChecksSummary {
    param($Checks)
    
    $summary = @{
        total = $Checks.Count
        success = 0
        failure = 0
        pending = 0
        skipped = 0
    }
    
    foreach ($check in $Checks) {
        switch ($check.bucket) {
            "pass" { $summary.success++ }
            "fail" { $summary.failure++ }
            "skipping" { $summary.skipped++ }
            "pending" { $summary.pending++ }
            "cancel" { $summary.skipped++ }
            default {
                if ($check.state -eq "PENDING" -or $check.state -eq "IN_PROGRESS" -or $check.state -eq "QUEUED") {
                    $summary.pending++
                }
            }
        }
    }
    
    return $summary
}

# Main execution
Write-Status "Starting PR #$PrNumber monitor"
Write-Status "Initial wait: ${InitialWait}s, Poll interval: ${PollInterval}s, Timeout: ${Timeout}m"

# Initial wait
Write-Status "Sleeping ${InitialWait}s (initial wait)..."
Start-Sleep -Seconds $InitialWait

$pollCount = 0
$maxPolls = [math]::Ceiling(($Timeout * 60 - $InitialWait) / $PollInterval)

while ($true) {
    $pollCount++
    $elapsed = [math]::Round(((Get-Date) - $startTime).TotalMinutes, 1)
    
    # Check timeout
    if ($elapsed -ge $Timeout) {
        Write-Warning-Custom "Timeout reached (${Timeout}m). Exiting with partial results."
        break
    }
    
    Write-Status "Poll #${pollCount}: Fetching PR checks..."
    $checks = Get-PRChecks -PrNumber $PrNumber
    
    if ($null -eq $checks) {
        Write-Warning-Custom "Failed to fetch checks. Retrying in ${PollInterval}s..."
        Start-Sleep -Seconds $PollInterval
        continue
    }
    
    $summary = Get-ChecksSummary -Checks $checks
    Write-Status "Checks: $($summary.total) total, $($summary.success) success, $($summary.failure) failure, $($summary.pending) pending"
    
    # Check if all complete
    if (Test-AllChecksComplete -Checks $checks) {
        Write-Success "All checks complete!"
        
        # Fetch comments/reviews
        Write-Status "Fetching bot comments and reviews..."
        $prData = Get-PRComments -PrNumber $PrNumber
        
        if ($null -ne $prData) {
            $commentCount = $prData.comments.Count
            $reviewCount = $prData.reviews.Count
            Write-Status "Found $commentCount comments, $reviewCount reviews"
        }
        
        # Output summary
        $output = @{
            pr_number = $PrNumber
            checks_complete = $true
            elapsed_time = "${elapsed}m"
            checks_summary = $summary
            comment_count = $commentCount
            review_count = $reviewCount
            next_action = "extract_forensics"
        }
        
        Write-Success "Monitoring complete. Ready for forensics extraction."
        $output | ConvertTo-Json -Depth 10
        exit 0
    }
    
    # Not complete yet - sleep and retry
    if ($pollCount -ge $maxPolls) {
        Write-Warning-Custom "Max polls reached. Exiting with partial results."
        break
    }
    
    Write-Status "Checks still pending. Sleeping ${PollInterval}s..."
    Start-Sleep -Seconds $PollInterval
}

# Timeout or max polls reached
$output = @{
    pr_number = $PrNumber
    checks_complete = $false
    elapsed_time = "${elapsed}m"
    checks_summary = $summary
    next_action = "manual_review"
    warning = "Timeout or max polls reached. Manual review recommended."
}

Write-Warning-Custom "Exiting with incomplete checks."
$output | ConvertTo-Json -Depth 10
exit 1

# Made with Bob
