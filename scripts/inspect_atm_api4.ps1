Add-Type -Path 'C:\Program Files\NinjaTrader 8\bin\NinjaTrader.Core.dll'
$asm = [System.Reflection.Assembly]::LoadFrom('C:\Program Files\NinjaTrader 8\bin\NinjaTrader.Core.dll')

Write-Output "=== Account properties/methods with Atm, Strategy, Position ==="
$accType = $asm.GetTypes() | Where-Object { $_.FullName -eq 'NinjaTrader.Cbi.Account' } | Select-Object -First 1
$accType.GetMembers([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance) |
    Where-Object { $_.Name -match 'Atm|Strategy|strategy' } |
    ForEach-Object {
        $params = ''
        if ($_ -is [System.Reflection.MethodInfo]) {
            $p = $_.GetParameters() | ForEach-Object { $_.ParameterType.Name + ' ' + $_.Name }
            $params = '(' + ($p -join ', ') + ') : ' + $_.ReturnType.Name
        }
        Write-Output ($_.MemberType.ToString() + ' | ' + $_.Name + $params)
    }

Write-Output ""
Write-Output "=== ServerAtmStrategy members ==="
$saType = $asm.GetTypes() | Where-Object { $_.FullName -eq 'NinjaTrader.ServerAtm.ServerAtmStrategy' } | Select-Object -First 1
if ($saType) {
    $saType.GetMembers([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance) |
        Where-Object { $_.Name -match 'Stop|Target|Change|Id|Break|Move|Order|Unique' } |
        ForEach-Object {
            $params = ''
            if ($_ -is [System.Reflection.MethodInfo]) {
                $p = $_.GetParameters() | ForEach-Object { $_.ParameterType.Name + ' ' + $_.Name }
                $params = '(' + ($p -join ', ') + ') : ' + $_.ReturnType.Name
            }
            Write-Output ($_.MemberType.ToString() + ' | ' + $_.Name + $params)
        }
}
