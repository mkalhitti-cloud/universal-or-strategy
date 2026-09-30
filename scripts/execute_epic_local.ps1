# Local Epic Execution Script (Mimics VM Workflow)
# Usage: .\scripts\execute_epic_local.ps1 -EpicId "EPIC-CCN-027" -TicketId "TICKET-2"

param(
    [Parameter(Mandatory=$true)]
    [string]$EpicId,
    
    [Parameter(Mandatory=$false)]
    [string]$TicketId = "",  # Empty = execute all tickets
    
    [Parameter(Mandatory=$false)]
    [string]$ApiKeyFile = "docs/API/bob.json"
)

# Check if Bob CLI is available
$bobAvailable = Get-Command bob -ErrorAction SilentlyContinue
if (-not $bobAvailable) {
    Write-Host "ERROR: Bob CLI not found in PATH" -ForegroundColor Red
    Write-Host "Install Bob CLI or add to PATH before running this script" -ForegroundColor Yellow
    exit 1
}

# Load API key
if (-not (Test-Path $ApiKeyFile)) {
    Write-Host "ERROR: API key file not found: $ApiKeyFile" -ForegroundColor Red
    exit 1
}

$apiData = Get-Content $ApiKeyFile | ConvertFrom-Json
$apiKey = $apiData.apikey

if (-not $apiKey) {
    Write-Host "ERROR: No apikey found in $ApiKeyFile" -ForegroundColor Red
    exit 1
}

# Set environment variable for Bob Shell
$env:BOBSHELL_API_KEY = $apiKey

Write-Host "=== Local Epic Execution ===" -ForegroundColor Cyan
Write-Host "Epic: $EpicId" -ForegroundColor White
Write-Host "Ticket: $(if ($TicketId) { $TicketId } else { 'ALL' })" -ForegroundColor White
Write-Host "API Key: $(if ($apiKey) { $apiKey.Substring(0, 20) + '...' } else { 'NOT SET' })" -ForegroundColor White
Write-Host ""

# Create message file (mimics VM workflow)
$msgFile = "temp_phase5_msg_$EpicId.txt"

$message = @"
Use the phase-5-execute MCP server to execute Phase 5 for $EpicId.

Call the execute_phase_5 tool with epic_id="$EpicId"$(if ($TicketId) { ", ticket_id=`"$TicketId`"" } else { "" }).

The tool will provide instructions for ticket execution. Follow the instructions to:
1. Read ticket details from docs/brain/$EpicId/04-tickets.md
2. Extract methods as specified in the tickets
3. Run tests to verify extraction
4. Check complexity (CYC ≤8 or as specified)
5. Verify build passes
6. Document completion in ticket-*-completion.md

**Verification**: Confirm completion file exists on disk before reporting success.

**Output**: Create docs/brain/$EpicId/ticket-$(if ($TicketId) { $TicketId } else { 'X' })-completion.md
"@

Set-Content -Path $msgFile -Value $message

Write-Host "Executing with Bob CLI..." -ForegroundColor Yellow
Write-Host ""

# Execute with Bob Shell (mimics VM workflow)
try {
    bob --yolo (Get-Content $msgFile -Raw)
    $exitCode = $LASTEXITCODE
    
    if ($exitCode -eq 0) {
        Write-Host ""
        Write-Host "=== Execution Complete ===" -ForegroundColor Green
        
        # Verify output files
        $completionPattern = "docs/brain/$EpicId/ticket-*-completion.md"
        $completionFiles = Get-ChildItem $completionPattern -ErrorAction SilentlyContinue
        
        if ($completionFiles) {
            Write-Host "Completion files created:" -ForegroundColor Green
            foreach ($file in $completionFiles) {
                Write-Host "  - $($file.Name)" -ForegroundColor White
            }
        } else {
            Write-Host "WARNING: No completion files found" -ForegroundColor Yellow
        }
    } else {
        Write-Host ""
        Write-Host "=== Execution Failed ===" -ForegroundColor Red
        Write-Host "Exit code: $exitCode" -ForegroundColor Red
    }
} finally {
    # Cleanup
    if (Test-Path $msgFile) {
        Remove-Item $msgFile
    }
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Review completion files in docs/brain/$EpicId/" -ForegroundColor White
Write-Host "2. Run Phase 5.V (Verification): .\scripts\execute_phase_verify_local.ps1 -EpicId $EpicId" -ForegroundColor White
Write-Host "3. Run Phase 6 (Final Review): .\scripts\execute_phase_review_local.ps1 -EpicId $EpicId" -ForegroundColor White

# Made with Bob
