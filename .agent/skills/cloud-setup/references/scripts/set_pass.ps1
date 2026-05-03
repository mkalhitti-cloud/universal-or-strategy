# 3. Install R|Trader Pro (Required for Rithmic Plug-in Mode)
$rtraderUrl = "https://downloads.rithmic.com/rtraderpro.msi"
$rtraderPath = "$env:TEMP\rtraderpro.msi"
if (-not (Test-Path "C:\Program Files (x86)\Rithmic\RTraderPro\RTraderPro.exe")) {
    Write-Output "Downloading R|Trader Pro..."
    Invoke-WebRequest -Uri $rtraderUrl -OutFile $rtraderPath -UseBasicParsing
    Write-Output "Installing R|Trader Pro..."
    Start-Process msiexec.exe -ArgumentList "/i `"$rtraderPath`" /quiet /qn /norestart" -Wait
}
# 4. Enable Windows Audio (Required for NinjaTrader Alerts)
# Note: Windows Server 2022 may need this to be explicitly started
Get-Service -Name "Audiosrv" -ErrorAction SilentlyContinue | Set-Service -StartupType Automatic
Get-Service -Name "AudioEndpointBuilder" -ErrorAction SilentlyContinue | Set-Service -StartupType Automatic
Start-Service -Name "Audiosrv" -ErrorAction SilentlyContinue
Start-Service -Name "AudioEndpointBuilder" -ErrorAction SilentlyContinue

# Force set admin password
net user admin Sacramento2025!
