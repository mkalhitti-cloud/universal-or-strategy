<#
.SYNOPSIS
  Automates the deployment of NinjaTrader 8 strategy files from the project repository to the NinjaTrader bin folder.

.DESCRIPTION
  This script performs the following actions:
  1. Verifies the source file exists in the project repository.
  2. Ensures the class name in the .cs file matches the filename.
  3. Copies the file to the NinjaTrader Custom Strategies folder.
  4. Optionally updates the version string displayed in the UI.

.PARAMETER SourceFileName
  The name of the .cs file in the project root to deploy.

.EXAMPLE
  .\ninja_deploy.ps1 -SourceFileName "UniversalORStrategyV8_2.cs"
#>

param (
    [Parameter(Mandatory = $true)]
    [string]$SourceFileName
)

$ErrorActionPreference = "Stop"

# Configuration
$ProjectRoot = "c:\Users\Mohammed Khalid\OneDrive\Desktop\WSGTA\Github\universal-or-strategy"
$NinjaTraderPath = "C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\Strategies"

# 1. Path Verification
$SourcePath = Join-Path $ProjectRoot $SourceFileName
if (-not (Test-Path $SourcePath)) {
    Write-Error "Source file not found at: $SourcePath"
}

$ClassName = [System.IO.Path]::GetFileNameWithoutExtension($SourceFileName)
$DestinationPath = Join-Path $NinjaTraderPath $SourceFileName

Write-Host "--- Starting Deployment of $SourceFileName ---" -ForegroundColor Cyan

# 2. Class Name Synchronization
Write-Host "Verifying class name synchronization..." -ForegroundColor Gray
$Content = Get-Content $SourcePath -Raw
# Update: public class [Something] : Strategy
$NewContent = $Content -replace "public class (\w+) : Strategy", "public class $ClassName : Strategy"

if ($Content -ne $NewContent) {
    Write-Host "Updating class name to match filename: $ClassName" -ForegroundColor Yellow
    $NewContent | Set-Content $SourcePath
}
else {
    Write-Host "Class name already matches filename." -ForegroundColor Green
}

# 3. Deployment
Write-Host "Deploying to NinjaTrader strategies folder..." -ForegroundColor Gray
Copy-Item $SourcePath $DestinationPath -Force

if (Test-Path $DestinationPath) {
    Write-Host "SUCCESS: File deployed to $DestinationPath" -ForegroundColor Green
}
else {
    Write-Error "FAILED: Deployment failed to $DestinationPath"
}

Write-Host "--- Deployment Complete ---" -ForegroundColor Cyan
Write-Host "Please switch to NinjaTrader and press F5 to compile." -ForegroundColor Magenta
