# Update .bobignore to exclude entire docs/brain/ directory
# This will reduce context from 60k to ~20k tokens

$bobignorePath = ".bobignore"
$content = Get-Content $bobignorePath

# Add comprehensive docs/brain/ exclusion
$newLines = @(
    "",
    "# Exclude entire docs/brain/ directory (386 files, 3.55 MB)",
    "# Only essential Wave 7 execution files are needed",
    "docs/brain/*",
    "",
    "# Whitelist: Essential Wave 7 files only",
    "!docs/brain/WAVE7_SETUP_COMPLETE.md",
    "!docs/brain/WAVE7_CONTEXT_VERIFICATION.md",
    "!docs/brain/task.md"
)

# Append to .bobignore
$content += $newLines
$content | Set-Content $bobignorePath

Write-Output "✅ Updated .bobignore to exclude docs/brain/*"
Write-Output "✅ Whitelisted 3 essential Wave 7 files"
Write-Output ""
Write-Output "Expected context reduction: 60k → ~20k tokens (67% reduction)"
Write-Output ""
Write-Output "Next: Restart Bob IDE session to verify fix"

# Made with Bob
