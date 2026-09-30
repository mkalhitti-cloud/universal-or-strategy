Add-Type -Path 'C:\Program Files\NinjaTrader 8\bin\NinjaTrader.Core.dll'
$asm = [System.Reflection.Assembly]::LoadFrom('C:\Program Files\NinjaTrader 8\bin\NinjaTrader.Core.dll')

Write-Output "=== ServerBracket PROPERTIES ==="
$sbType = $asm.GetTypes() | Where-Object { $_.FullName -eq 'NinjaTrader.ServerAtm.ServerBracket' } | Select-Object -First 1
$sbType.GetProperties([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance) |
    ForEach-Object {
        $rw = ''
        if ($_.CanRead)  { $rw += 'get' }
        if ($_.CanWrite) { $rw += ',set' }
        Write-Output ($_.Name.PadRight(24) + ' : ' + $_.PropertyType.Name + ' [' + $rw + ']')
    } | Sort-Object

Write-Output ""
Write-Output "=== ServerBracket METHODS ==="
$sbType.GetMethods([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance) |
    Where-Object { $_.Name -notmatch '^(get_|set_|Equals|GetHash|GetType|ToString|MemberwiseClone)' } |
    ForEach-Object {
        $p = $_.GetParameters() | ForEach-Object { $_.ParameterType.Name + ' ' + $_.Name }
        Write-Output ($_.ReturnType.Name.PadRight(10) + ' ' + $_.Name + '(' + ($p -join ', ') + ')')
    }

Write-Output ""
Write-Output "=== ServerAtmStrategy METHODS (non-trivial) ==="
$saType = $asm.GetTypes() | Where-Object { $_.FullName -eq 'NinjaTrader.ServerAtm.ServerAtmStrategy' } | Select-Object -First 1
$saType.GetMethods([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance) |
    Where-Object { $_.Name -notmatch '^(get_|set_|Equals|GetHash|GetType|ToString|MemberwiseClone|add_|remove_)' } |
    ForEach-Object {
        $p = $_.GetParameters() | ForEach-Object { $_.ParameterType.Name + ' ' + $_.Name }
        Write-Output ($_.ReturnType.Name.PadRight(10) + ' ' + $_.Name + '(' + ($p -join ', ') + ')')
    }
