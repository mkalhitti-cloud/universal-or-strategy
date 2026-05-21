# Format All C# Files - Automated CSharpier Runner
# Part of PR Perfection Loop - ensures zero IDE warnings

param(
    [switch]$Check = $false,  # Check mode: verify formatting without modifying
    [switch]$Verbose = $false
)

$ErrorActionPreference = "Stop"

Write-Host "=== CSharpier Auto-Formatter ===" -ForegroundColor Cyan
Write-Host ""

# Find all C# files in src/ and tests/
$srcFiles = Get-ChildItem -Path "src" -Filter "*.cs" -File -ErrorAction SilentlyContinue
$testFiles = Get-ChildItem -Path "tests" -Filter "*.cs" -File -ErrorAction SilentlyContinue
$allFiles = @($srcFiles) + @($testFiles)

if ($allFiles.Count -eq 0) {
    Write-Host "[WARN] No C# files found in src/ or tests/" -ForegroundColor Yellow
    exit 0
}

Write-Host "Found $($allFiles.Count) C# files" -ForegroundColor Yellow
Write-Host ""

$formatted = 0
$unchanged = 0
$errors = 0

foreach ($file in $allFiles) {
    $relativePath = $file.FullName.Replace("$PWD\", "")
    
    try {
        if ($Check) {
            # Check mode - verify formatting
            $output = & dotnet csharpier $file.FullName --check 2>&1
            if ($LASTEXITCODE -eq 0) {
                if ($Verbose) {
                    Write-Host "[OK] $relativePath" -ForegroundColor Green
                }
                $unchanged++
            } else {
                Write-Host "[NEEDS FORMAT] $relativePath" -ForegroundColor Yellow
                $formatted++
            }
        } else {
            # Format mode - apply formatting
            $output = & dotnet csharpier $file.FullName 2>&1
            if ($LASTEXITCODE -eq 0) {
                if ($Verbose) {
                    Write-Host "[FORMATTED] $relativePath" -ForegroundColor Green
                }
                $formatted++
            } else {
                if ($Verbose) {
                    Write-Host "[UNCHANGED] $relativePath" -ForegroundColor Gray
                }
                $unchanged++
            }
        }
    } catch {
        Write-Host "[ERROR] $relativePath : $_" -ForegroundColor Red
        $errors++
    }
}

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Cyan
if ($Check) {
    Write-Host "Files needing format: $formatted" -ForegroundColor $(if ($formatted -gt 0) { "Yellow" } else { "Green" })
    Write-Host "Files already formatted: $unchanged" -ForegroundColor Green
} else {
    Write-Host "Files formatted: $formatted" -ForegroundColor Green
    Write-Host "Files unchanged: $unchanged" -ForegroundColor Gray
}
Write-Host "Errors: $errors" -ForegroundColor $(if ($errors -gt 0) { "Red" } else { "Green" })
Write-Host ""

if ($errors -gt 0) {
    Write-Host "[FAIL] Formatting encountered errors" -ForegroundColor Red
    exit 1
}

if ($Check -and $formatted -gt 0) {
    Write-Host "[ACTION REQUIRED] Run without -Check flag to format files" -ForegroundColor Yellow
    exit 1
}

Write-Host "[SUCCESS] All files formatted" -ForegroundColor Green
exit 0

# Made with Bob
