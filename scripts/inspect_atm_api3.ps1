Add-Type -Path 'C:\Program Files\NinjaTrader 8\bin\NinjaTrader.Core.dll'
$asm = [System.Reflection.Assembly]::LoadFrom('C:\Program Files\NinjaTrader 8\bin\NinjaTrader.Core.dll')
$atmType = $asm.GetTypes() | Where-Object { $_.FullName -eq 'NinjaTrader.NinjaScript.AtmStrategy' } | Select-Object -First 1

Write-Output "=== AtmStrategyChangeStopTarget signature ==="
$atmType.GetMethods() | Where-Object { $_.Name -eq 'AtmStrategyChangeStopTarget' } | ForEach-Object {
    $params = $_.GetParameters() | ForEach-Object { $_.ParameterType.Name + ' ' + $_.Name }
    Write-Output ($_.ReturnType.Name + ' ' + $_.Name + '(' + ($params -join ', ') + ')')
}

Write-Output "=== ALL Atm* methods with signatures ==="
$atmType.GetMethods() | Where-Object { $_.Name -match '^AtmStrategy' } | ForEach-Object {
    $params = $_.GetParameters() | ForEach-Object { $_.ParameterType.Name + ' ' + $_.Name }
    Write-Output ($_.ReturnType.Name + ' ' + $_.Name + '(' + ($params -join ', ') + ')')
}
