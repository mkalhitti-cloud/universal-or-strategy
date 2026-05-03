# 1. Download Git
$gitUrl = "https://github.com/git-for-windows/git/releases/download/v2.48.1.windows.1/Git-2.48.1-64-bit.exe"
$outPath = "$env:TEMP\git-install.exe"
Invoke-WebRequest -Uri $gitUrl -OutFile $outPath

# 2. Install Git Silently
Start-Process -FilePath $outPath -ArgumentList "/VERYSILENT", "/NORESTART" -Wait

# 3. Create NinjaTrader Strategy Directory
$strategyPath = "C:\Users\admin\Documents\NinjaTrader 8\bin\Custom\Strategies"
New-Item -ItemType Directory -Path $strategyPath -Force

# 4. Clone Repository
Set-Location -Path $strategyPath
# Git will be on path after silent install
& "C:\Program Files\Git\cmd\git.exe" clone https://github.com/mkalhitti-cloud/universal-or-strategy.git
