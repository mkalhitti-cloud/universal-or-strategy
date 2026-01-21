param(
    [string]$BaseDir = "C:\Users\Mohammed Khalid\Documents\NinjaTrader 8"
)

$LogDir = Join-Path $BaseDir "log"
$TraceDir = Join-Path $BaseDir "trace"

Write-Host "Searching for latest logs in $LogDir..."
$LatestLogs = Get-ChildItem -Path $LogDir -Filter "log.*.txt" | Sort-Object LastWriteTime -Descending | Select-Object -First 2

Write-Host "`nSearching for latest trace in $TraceDir..."
$LatestTrace = Get-ChildItem -Path $TraceDir | Sort-Object LastWriteTime -Descending | Select-Object -First 1

$Results = @()
if ($LatestLogs) { $Results += $LatestLogs }
if ($LatestTrace) { $Results += $LatestTrace }

return $Results | Select-Object FullName, LastWriteTime, Length
