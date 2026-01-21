param(
    [string[]]$Keywords = @("Rejected", "Error", "Termination", "Exception", "Critical", "MGC", "MES"),
    [int]$MaxLines = 100
)

# Use Get-LatestLog logic internally or expect paths as input? 
# Let's make it smarter: if no files provided, find them.
$LatestLogScript = Join-Path $PSScriptRoot "Get-LatestLog.ps1"
$Files = & $LatestLogScript

if (-not $Files) {
    Write-Warning "No log or trace files found to scan."
    return
}

foreach ($FileObj in $Files) {
    $FilePath = $FileObj.FullName
    Write-Host "`n--- Scanning: $FilePath ---" -ForegroundColor Cyan
    
    foreach ($Keyword in $Keywords) {
        $Matches = Select-String -Path $FilePath -Pattern $Keyword -CaseSensitive:$false
        if ($Matches) {
            Write-Host "Found matches for '$Keyword':" -ForegroundColor Yellow
            $Matches | Select-Object -Last $MaxLines | ForEach-Object {
                Write-Host "Line $($_.LineNumber): $($_.Line.Trim())"
            }
        }
    }
}
