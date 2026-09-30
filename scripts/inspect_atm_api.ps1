
Add-Type -Path 'C:\Program Files\NinjaTrader 8\bin\NinjaTrader.Core.dll'
$asm = [System.Reflection.Assembly]::LoadFrom('C:\Program Files\NinjaTrader 8\bin\NinjaTrader.Core.dll')
$atmTypes = $asm.GetTypes() | Where-Object { $_.Name -match 'Atm' }
Write-Output "=== ATM Types ==="
$atmTypes | Select-Object FullName | Format-Table -AutoSize

Write-Output "=== AtmStrategy methods (if found) ==="
$atmType = $atmTypes | Where-Object { $_.Name -eq 'AtmStrategy' } | Select-Object -First 1
if ($atmType) {
    $atmType.GetMethods([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance) |
        Select-Object Name, ReturnType |
        Sort-Object Name |
        Format-Table -AutoSize
} else {
    Write-Output "AtmStrategy not found in NinjaTrader.Core.dll"
}

Write-Output "=== Account methods containing 'Atm' or 'Change' ==="
$accType = $asm.GetTypes() | Where-Object { $_.Name -eq 'Account' } | Select-Object -First 1
if ($accType) {
    $accType.GetMethods([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance) |
        Where-Object { $_.Name -match 'Atm|Change|Break|Stop' } |
        Select-Object Name |
        Format-Table -AutoSize
} else {
    Write-Output "Account type not found"
}
