# scan_deadlock_windows.ps1 -- Deadlock Window Detector
# Flags resource acquisition NOT immediately followed by a try block.
# ZERO violations required before any P5 Engineer handoff.
$files = Get-ChildItem -Path "src" -Recurse -Filter "*.cs"
$violations = @()

foreach ($file in $files) {
    $lines = Get-Content $file.FullName
    for ($i = 0; $i -lt $lines.Length; $i++) {
        $line = $lines[$i]
        if ($line -match '\.Wait\(0\)|InFlight\s*=\s*true|_.*Sem\.Wait|TryAdd\(') {
            $foundTry = $false
            $foundReturn = $false
            for ($j = $i + 1; $j -lt [Math]::Min($i + 6, $lines.Length); $j++) {
                if ($lines[$j] -match '^\s*try\s*\{') { $foundTry = $true; break }
                if ($lines[$j] -match '_isTerminating|return;|return false') { $foundReturn = $true }
            }
            if ($foundReturn -and -not $foundTry) {
                $violations += "$($file.FullName):$($i + 1) -- DEADLOCK WINDOW"
            }
        }
    }
}

if ($violations.Count -gt 0) {
    Write-Error "SCAN FAILED: $($violations.Count) Deadlock Window(s) found:"
    $violations | ForEach-Object { Write-Error $_ }
    exit 1
} else {
    Write-Host "SCAN PASSED: Zero Deadlock Windows found." -ForegroundColor Green
}
