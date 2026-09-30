Add-Type -Path 'C:\Program Files\NinjaTrader 8\bin\NinjaTrader.Core.dll'
$asm = [System.Reflection.Assembly]::LoadFrom('C:\Program Files\NinjaTrader 8\bin\NinjaTrader.Core.dll')

Write-Output "=== UserAtmDictionary members ==="
$uatm = $asm.GetTypes() | Where-Object { $_.FullName -eq 'NinjaTrader.ServerAtm.UserAtmDictionary' } | Select-Object -First 1
if ($uatm) {
    $uatm.GetMembers([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance) |
        ForEach-Object {
            $params = ''
            if ($_ -is [System.Reflection.MethodInfo]) {
                $p = $_.GetParameters() | ForEach-Object { $_.ParameterType.Name + ' ' + $_.Name }
                $params = '(' + ($p -join ', ') + ') : ' + $_.ReturnType.Name
            }
            Write-Output ($_.MemberType.ToString() + ' | ' + $_.Name + $params)
        }
}

Write-Output ""
Write-Output "=== Account ALL public methods/properties ==="
$accType = $asm.GetTypes() | Where-Object { $_.FullName -eq 'NinjaTrader.Cbi.Account' } | Select-Object -First 1
$accType.GetMembers([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance) |
    Where-Object { $_.Name -notmatch '^(add_|remove_|get_|set_|Equals|GetHash|GetType|ToString|MemberwiseClone)' } |
    ForEach-Object {
        $params = ''
        if ($_ -is [System.Reflection.MethodInfo]) {
            $p = $_.GetParameters() | ForEach-Object { $_.ParameterType.Name + ' ' + $_.Name }
            $params = '(' + ($p -join ', ') + ') : ' + $_.ReturnType.Name
        }
        Write-Output ($_.MemberType.ToString().PadRight(12) + ' | ' + $_.Name + $params)
    } | Sort-Object
