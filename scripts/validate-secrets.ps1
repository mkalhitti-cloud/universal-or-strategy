# Secrets Validation Script (BMad System)

$root = Get-Location
$violations = 0

Write-Host "--- Scanning for plaintext secrets in tracked files ---" -ForegroundColor Cyan

# Patterns to look for
$patterns = @(
    'sk-[a-zA-Z0-9]{32,}',     # Generic OpenAI/Secret Key pattern
    'ghp_[a-zA-Z0-9]{36,}',    # GitHub PAT
    '"API_KEY":\s*"[^\$]' # Plaintext API Key in JSON (not a variable)
)

$files = Get-ChildItem -Path $root -Recurse -File -Include *.md, *.json, *.txt, *.cs, *.ps1 | Where-Object { 
    $_.FullName -notlike "*\.git\*" -and 
    $_.FullName -notlike "*\bin\*" -and 
    $_.FullName -notlike "*\obj\*" -and
    $_.FullName -notlike "*\settings.json" -and
    $_.FullName -notlike "*\settings.local.json" -and
    $_.Name -ne "validate-secrets.ps1"
}

foreach ($file in $files) {
    foreach ($pattern in $patterns) {
        $matches = Select-String -Path $file.FullName -Pattern $pattern -AllMatches
        if ($matches) {
            foreach ($match in $matches) {
                Write-Host "[VIOLATION] Secret pattern found in $($file.FullName):$($match.LineNumber)" -ForegroundColor Red
                $violations++
            }
        }
    }
}

if ($violations -eq 0) {
    Write-Host "[PASS] No plaintext secrets detected in tracked files." -ForegroundColor Green
    exit 0
} else {
    Write-Host "[FAIL] $violations violations found. Remediation required." -ForegroundColor Red
    exit 1
}
