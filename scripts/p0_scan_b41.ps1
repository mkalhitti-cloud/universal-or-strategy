$src = "c:\WSGTA\universal-or-strategy\src\PropTraderTools"
$files = Get-ChildItem $src -Filter "*.cs" -Recurse

# SCAN-12a: lock(
$lock = $files | Select-String -Pattern "lock\s*\(" | Where-Object { $_.Line -notmatch "//" }
Write-Host "SCAN-12a lock count: $($lock.Count)"
if ($lock.Count -gt 0) { $lock | ForEach-Object { Write-Host "  $($_.FileName):$($_.LineNumber): $($_.Line.Trim())" } }

# SCAN-12b: async void
$av = $files | Select-String -Pattern "async void " | Where-Object { $_.Line -notmatch "//" }
Write-Host "SCAN-12b async void count: $($av.Count)"
if ($av.Count -gt 0) { $av | ForEach-Object { Write-Host "  $($_.FileName):$($_.LineNumber): $($_.Line.Trim())" } }

# SCAN-12c: return null in PttQuickExit.cs
$rn1 = Select-String -Path "$src\Features\PttQuickExit.cs" -Pattern "return null;"
Write-Host "SCAN-12c return null PttQuickExit: $($rn1.Count)"
if ($rn1.Count -gt 0) { $rn1 | ForEach-Object { Write-Host "  line $($_.LineNumber): $($_.Line.Trim())" } }

# SCAN-12d: return null in PttGlobalQuickExit.cs
$rn2 = Select-String -Path "$src\Features\PttGlobalQuickExit.cs" -Pattern "return null;"
Write-Host "SCAN-12d return null PttGlobalQuickExit: $($rn2.Count)"
if ($rn2.Count -gt 0) { $rn2 | ForEach-Object { Write-Host "  line $($_.LineNumber): $($_.Line.Trim())" } }

# SCAN-12e: volatile double in PttQuickExit.cs
$vd1 = Select-String -Path "$src\Features\PttQuickExit.cs" -Pattern "volatile double"
Write-Host "SCAN-12e volatile double PttQuickExit: $($vd1.Count)"
if ($vd1.Count -gt 0) { $vd1 | ForEach-Object { Write-Host "  line $($_.LineNumber): $($_.Line.Trim())" } }

# SCAN-12f: volatile double in PttGlobalQuickExit.cs
$vd2 = Select-String -Path "$src\Features\PttGlobalQuickExit.cs" -Pattern "volatile double"
Write-Host "SCAN-12f volatile double PttGlobalQuickExit: $($vd2.Count)"
if ($vd2.Count -gt 0) { $vd2 | ForEach-Object { Write-Host "  line $($_.LineNumber): $($_.Line.Trim())" } }

Write-Host "--- P0 SCAN COMPLETE ---"
