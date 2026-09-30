Add-Type -Path 'C:\Program Files\NinjaTrader 8\bin\NinjaTrader.Core.dll'
$asm = [System.Reflection.Assembly]::LoadFrom('C:\Program Files\NinjaTrader 8\bin\NinjaTrader.Core.dll')

# ServerBracket
$sbType = $asm.GetTypes() | Where-Object { $_.FullName -match 'ServerBracket' } | Select-Object -First 1
Write-Output "=== ServerBracket type: $($sbType.FullName) ==="
if ($sbType) {
    $sbType.GetMembers([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance) |
        ForEach-Object {
            $params = ''
            if ($_ -is [System.Reflection.MethodInfo]) {
                $p = $_.GetParameters() | ForEach-Object { $_.ParameterType.Name + ' ' + $_.Name }
                $params = '(' + ($p -join ', ') + ') : ' + $_.ReturnType.Name
            }
            Write-Output ($_.MemberType.ToString().PadRight(12) + ' | ' + $_.Name + $params)
        }
}

# All ServerAtm types
Write-Output ""
Write-Output "=== All NinjaTrader.ServerAtm types ==="
$asm.GetTypes() | Where-Object { $_.Namespace -match 'ServerAtm' } | Select-Object FullName | Format-Table -AutoSize

# ServerAtmStrategy full list
Write-Output ""
Write-Output "=== ServerAtmStrategy ALL members ==="
$saType = $asm.GetTypes() | Where-Object { $_.FullName -eq 'NinjaTrader.ServerAtm.ServerAtmStrategy' } | Select-Object -First 1
$saType.GetMembers([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance) |
    ForEach-Object {
        $params = ''
        if ($_ -is [System.Reflection.MethodInfo]) {
            $p = $_.GetParameters() | ForEach-Object { $_.ParameterType.Name + ' ' + $_.Name }
            $params = '(' + ($p -join ', ') + ') : ' + $_.ReturnType.Name
        }
        Write-Output ($_.MemberType.ToString().PadRight(12) + ' | ' + $_.Name + $params)
    }
