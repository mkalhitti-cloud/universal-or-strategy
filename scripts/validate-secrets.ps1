# Secrets Validation Script (BMad System)

$root = Get-Location
$rootPath = $root.Path
$violations = 0

Write-Host "--- Scanning for plaintext secrets in tracked files ---" -ForegroundColor Cyan

# Patterns to look for
$patterns = @(
    'sk-[a-zA-Z0-9]{32,}',     # Generic OpenAI/Secret Key pattern
    'ghp_[a-zA-Z0-9]{36,}',    # GitHub PAT
    '"API_KEY":\s*"[^\$]',      # Plaintext API Key in JSON (not a variable)
    '(?i)authorization\s*[:=]\s*["'']?\s*bearer\s+(?![\$<{])(?!token\b|your[_-]?token\b|example\b)[-A-Za-z0-9._~+/]{20,}={0,2}' # Bearer tokens
)

$trackedFiles = git -C $rootPath ls-files -- '*.md' '*.json' '*.txt' '*.cs' '*.ps1'
if ($LASTEXITCODE -ne 0) {
    Write-Host "[FAIL] Unable to enumerate tracked files via git ls-files." -ForegroundColor Red
    exit 2
}

$files = $trackedFiles | Where-Object {
    $_ -and
    $_ -notmatch '^(?:\.cursor|\.testsprite)/' -and
    $_ -notmatch '(^|/)(?:bin|obj)/' -and
    $_ -notmatch '(^|/)settings(?:\.local)?\.json$' -and
    $_ -ne 'scripts/validate-secrets.ps1'
} | ForEach-Object {
    Join-Path $rootPath $_
}

foreach ($file in $files) {
    foreach ($pattern in $patterns) {
        $matches = Select-String -Path $file -Pattern $pattern -AllMatches
        if ($matches) {
            foreach ($match in $matches) {
                Write-Host "[VIOLATION] Secret pattern found in ${file}:$($match.LineNumber)" -ForegroundColor Red
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
