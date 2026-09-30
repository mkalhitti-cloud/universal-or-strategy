$utf8 = New-Object System.Text.UTF8Encoding $false
$path = "src\PropTraderTools\Tests\B129Tests.cs"
$content = [System.IO.File]::ReadAllText($path)

# Fix 1: Stop1 does not end with STP -> DW-B137 update
$content = $content -replace 'Assert\.False\(CopyEngine\.IsAtmSTPOrder\(legacy\)\); // "Stop1" does not end with "STP"', 'Assert.True(CopyEngine.IsAtmSTPOrder(legacy)); // DW-B137: Stop1 now returns true (StartsWith("Stop") extended predicate)'

# Fix 2: Stop1 routes to legacy acc.Change() -> DW-B137 update
$content = $content -replace 'Assert\.False\(CopyEngine\.IsAtmSTPOrder\(native\)\); // routes to legacy acc\.Change\(\)', 'Assert.True(CopyEngine.IsAtmSTPOrder(native)); // DW-B137: Stop1 now routes to cancel+resubmit (correct ATM behavior)'

# Fix 3: bare Assert.False on stop1 -> DW-B137 update
$content = $content -replace 'Assert\.False\(CopyEngine\.IsAtmSTPOrder\(stop1\)\);', 'Assert.True(CopyEngine.IsAtmSTPOrder(stop1)); // DW-B137: Stop1 returns true (StartsWith("Stop") extended predicate)'

[System.IO.File]::WriteAllText($path, $content, $utf8)
Write-Host "Done - B129Tests.cs updated for DW-B137"
