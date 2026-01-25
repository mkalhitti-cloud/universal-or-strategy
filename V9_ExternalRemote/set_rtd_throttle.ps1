$excel = New-Object -ComObject Excel.Application
$excel.RTD.ThrottleInterval = 0
$excel.Quit()
[System.Runtime.Interopservices.Marshal]::ReleaseComObject($excel) | Out-Null
Write-Host "Excel RTD ThrottleInterval set to 0"
