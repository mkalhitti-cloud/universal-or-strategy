# Setup YOLO Mode for Bob CLI
# Adds a PowerShell function alias so 'bob' automatically uses '--yolo' flag

$profilePath = $PROFILE

# Create profile if it doesn't exist
if (!(Test-Path $profilePath)) {
    New-Item -Path $profilePath -ItemType File -Force | Out-Null
    Write-Host "Created PowerShell profile at: $profilePath"
}

# Check if bob function already exists
$profileContent = Get-Content $profilePath -Raw -ErrorAction SilentlyContinue
if ($profileContent -match 'function bob') {
    Write-Host "Bob function already exists in profile. Skipping."
    exit 0
}

# Add bob function to profile
$bobFunction = @"

# Bob CLI YOLO Mode (auto-approve all tool calls)
function bob {
    & bob.exe --yolo `$args
}
"@

Add-Content -Path $profilePath -Value $bobFunction
Write-Host "✅ YOLO mode added to PowerShell profile"
Write-Host ""
Write-Host "To activate in current session, run:"
Write-Host "  . `$PROFILE"
Write-Host ""
Write-Host "Or close and reopen PowerShell windows"

# Made with Bob
