Add-Type -Path 'C:\Program Files\NinjaTrader 8\bin\NinjaTrader.Core.dll'
$asm = [System.Reflection.Assembly]::LoadFrom('C:\Program Files\NinjaTrader 8\bin\NinjaTrader.Core.dll')

# Check Account.Strategies type
$accType = $asm.GetTypes() | Where-Object { $_.FullName -eq 'NinjaTrader.Cbi.Account' } | Select-Object -First 1
$strategiesProp = $accType.GetProperty('Strategies')
$serverStrategiesProp = $accType.GetProperty('ServerStrategies')
Write-Output "Account.Strategies type: $($strategiesProp.PropertyType.FullName)"
Write-Output "Account.ServerStrategies type: $($serverStrategiesProp.PropertyType.FullName)"

# Get the element type of Strategies collection
$stratCollType = $strategiesProp.PropertyType
Write-Output "Strategies element generic args:"
$stratCollType.GetGenericArguments() | ForEach-Object { Write-Output "  $_" }

# Inspect StrategyBase
Write-Output ""
Write-Output "=== StrategyBase methods matching Stop|Target|Change|Atm|Break|Move ==="
$sbType = $asm.GetTypes() | Where-Object { $_.FullName -eq 'NinjaTrader.NinjaScript.StrategyBase' } | Select-Object -First 1
if ($sbType) {
    $sbType.GetMethods([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance) |
        Where-Object { $_.Name -match 'Stop|Target|Change|Atm|Break|Move|Order' } |
        ForEach-Object {
            $p = $_.GetParameters() | ForEach-Object { $_.ParameterType.Name + ' ' + $_.Name }
            Write-Output ($_.ReturnType.Name + ' ' + $_.Name + '(' + ($p -join ', ') + ')')
        }
}

# Inspect ServerAtmStrategy more carefully
Write-Output ""
Write-Output "=== ServerAtmStrategy ALL public members ==="
$saType = $asm.GetTypes() | Where-Object { $_.FullName -eq 'NinjaTrader.ServerAtm.ServerAtmStrategy' } | Select-Object -First 1
if ($saType) {
    $saType.GetMembers([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance) |
        ForEach-Object {
            $params = ''
            if ($_ -is [System.Reflection.MethodInfo]) {
                $p = $_.GetParameters() | ForEach-Object { $_.ParameterType.Name + ' ' + $_.Name }
                $params = '(' + ($p -join ', ') + ') : ' + $_.ReturnType.Name
            }
            Write-Output ($_.MemberType.ToString().PadRight(12) + ' | ' + $_.Name + $params)
        }
}
