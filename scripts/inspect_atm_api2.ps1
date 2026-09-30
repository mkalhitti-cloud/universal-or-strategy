Add-Type -Path 'C:\Program Files\NinjaTrader 8\bin\NinjaTrader.Core.dll'
$asm = [System.Reflection.Assembly]::LoadFrom('C:\Program Files\NinjaTrader 8\bin\NinjaTrader.Core.dll')
$atmType = $asm.GetTypes() | Where-Object { $_.FullName -eq 'NinjaTrader.NinjaScript.AtmStrategy' } | Select-Object -First 1
Write-Output "=== ALL AtmStrategy PUBLIC INSTANCE METHODS ==="
$atmType.GetMethods([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance) |
    Select-Object Name |
    Sort-Object Name |
    Format-Table -AutoSize

Write-Output "=== ALL AtmStrategy PUBLIC PROPERTIES ==="
$atmType.GetProperties([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance) |
    Select-Object Name, PropertyType |
    Sort-Object Name |
    Format-Table -AutoSize

Write-Output "=== IAtmStrategy interface members ==="
$iAtm = $asm.GetTypes() | Where-Object { $_.FullName -eq 'NinjaTrader.NinjaScript.IAtmStrategy' } | Select-Object -First 1
if ($iAtm) {
    $iAtm.GetMembers() | Select-Object Name, MemberType | Format-Table -AutoSize
}
