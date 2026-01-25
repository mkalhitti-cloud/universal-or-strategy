$date = Get-Date -Format "yyyy-MM-dd_HHmm"
$sourcePath = "C:\Users\Mohammed Khalid\Documents\NinjaTrader 8"
$backupBase = "C:\Users\Mohammed Khalid\OneDrive\Desktop\WSGTA\NinjaTrader_Local_Backups"
$backupPath = Join-Path $backupBase ("Backup_" + $date)

if (-not (Test-Path $sourcePath)) {
    Write-Error "Source path not found: $sourcePath"
    exit 1
}

Write-Host "Starting backup to: $backupPath"
New-Item -ItemType Directory -Path $backupPath -Force

# Copy critical folders
robocopy $sourcePath $backupPath /E /XJ /R:3 /W:5 /NP /MT:16 /NFL /NDL /XF "*.tmp"

Write-Host "Backup completed successfully."
