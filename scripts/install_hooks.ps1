# install_hooks.ps1 -- Activates V12 pre-commit safety hooks in the local git repo.
# Run once after cloning or whenever the hook policy changes.
# Protocol: Path Hardening -- all paths quoted (BMad V12 Permanent DNA).

$ErrorActionPreference = "Stop"

$repoRoot   = Split-Path -Parent $PSScriptRoot
$hooksDir   = (& git -C "$repoRoot" rev-parse --git-path hooks).Trim()
$hookTarget = Join-Path $hooksDir "pre-commit"

Write-Host "--- V12 Hook Installer ---"
Write-Host "Repo root : $repoRoot"
Write-Host "Hooks dir : $hooksDir"

if (-not $hooksDir -or -not (Test-Path -LiteralPath $hooksDir)) {
    Write-Error "ERROR: Git hooks path not found. Is this a git repository?"
    exit 1
}

# Build hook lines as an array to avoid PowerShell heredoc conflicts with sh $() syntax.
# Each element is one line of the resulting sh script.
$lines = @(
    "#!/bin/sh",
    "# V12 Pre-Commit Safety Hook -- installed by scripts/install_hooks.ps1",
    "# Gates: (1) No lock() in src/, (2) ASCII-only in staged .cs files",
    "",
    'echo "--- V12 Pre-Commit Gate ---"',
    "",
    "# Gate 1: Lock-free audit",
    'REPO_ROOT=$(git rev-parse --show-toplevel)',
    'LOCK_HITS=$(grep -rnE "(^|[[:space:]])lock[[:space:]]*\(" "$REPO_ROOT/src/" 2>/dev/null | grep -vE "(//|stateLock|\*)" | wc -l)',
    'if [ "$LOCK_HITS" -gt "0" ]; then',
    '    echo "PRE-COMMIT FAIL: lock() found in src/ -- BANNED by Platinum Standard."',
    '    grep -rnE "(^|[[:space:]])lock[[:space:]]*\(" "$REPO_ROOT/src/" 2>/dev/null | grep -vE "(//|stateLock|\*)"',
    '    exit 1',
    "fi",
    "",
    "# Gate 2: ASCII purity on staged .cs files",
    'STAGED_CS=$(git diff --cached --name-only --diff-filter=ACM | grep "\.cs$")',
    'if [ -n "$STAGED_CS" ]; then',
    '    python "$REPO_ROOT/check_ascii.py" $STAGED_CS',
    '    if [ $? -ne 0 ]; then',
    '        echo "PRE-COMMIT FAIL: Non-ASCII detected in staged C# files."',
    '        exit 1',
    '    fi',
    "fi",
    "",
    "# Gate 3: Gitleaks (staged-files scan; best-effort if binary missing)",
    'if command -v gitleaks >/dev/null 2>&1; then',
    '    gitleaks protect --staged --config "$REPO_ROOT/.gitleaks.toml" --redact',
    '    if [ $? -ne 0 ]; then',
    '        echo "PRE-COMMIT FAIL: Gitleaks detected a potential secret in staged files."',
    '        exit 1',
    '    fi',
    'else',
    '    echo "[WARN] gitleaks not on PATH -- skipping secret scan. CI will catch it."',
    'fi',
    "",
    "# Gate 4: Large file detection (Max 20MB)",
    "MAX_SIZE=20971520",
    'OVERSIZED=$(git diff --cached --name-only --diff-filter=ACM | xargs -r du -b | awk -v size="$MAX_SIZE" ''$1 > size {print $2}'')',
    'if [ -n "$OVERSIZED" ]; then',
    '    echo "PRE-COMMIT FAIL: Files over 20MB detected in staged files."',
    '    echo "$OVERSIZED"',
    '    exit 1',
    "fi",
    "",
    'echo "--- V12 Pre-Commit Gate: PASS ---"',
    "exit 0"
)

$hookContent = $lines -join "`n"
[System.IO.File]::WriteAllText($hookTarget, $hookContent + "`n", (New-Object System.Text.UTF8Encoding $false))

Write-Host ""
Write-Host "HOOK INSTALLED : $hookTarget"
Write-Host "Active gates   : [1] lock() ban  [2] ASCII purity  [3] gitleaks (if installed)  [4] 20MB size limit"
Write-Host "To bypass (rare): git commit --no-verify"
