# WSGTA V10 Smart Launcher
# Automatically finds the latest production version and launches it with correct working directory.

$BaseDir = "c:\Users\Mohammed Khalid\OneDrive\Desktop\WSGTA\Github\universal-or-strategy"
$SearchPattern = "V10_PROD_*"

Write-Host "Searching for latest WSGTA Remote in $BaseDir..." -ForegroundColor Cyan

# Find latest folder by name/timestamp (excluding V10_PROD_REMOTE if versioned folders are available)
$LatestFolder = Get-ChildItem -Path $BaseDir -Directory -Filter $SearchPattern | 
Sort-Object LastWriteTime -Descending | 
Select-Object -First 1

if ($null -eq $LatestFolder) {
    Write-Error "Could not find any V10_PROD folders in $BaseDir"
    Pause
    exit
}

$RemotePath = Join-Path $LatestFolder.FullName "V9_ExternalRemote.exe"

if (Test-Path $RemotePath) {
    Write-Host "Launching Latest WSGTA Remote: $($LatestFolder.Name)" -ForegroundColor Green
    # Launching with WorkingDirectory ensures DLLs and config files are found correctly
    Start-Process -FilePath $RemotePath -WorkingDirectory $LatestFolder.FullName
}
else {
    Write-Error "Found folder $($LatestFolder.Name) but could not find V9_ExternalRemote.exe inside."
    Write-Host "Full path searched: $RemotePath" -ForegroundColor Yellow
    Pause
}
