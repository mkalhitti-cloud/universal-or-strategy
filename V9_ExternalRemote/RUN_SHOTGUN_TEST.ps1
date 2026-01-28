<#
.SYNOPSIS
  Launches the V9_ExternalRemote.exe application after closing any existing instances.

.DESCRIPTION
  This script closes any running instances of V9_ExternalRemote.exe, then launches the newly built executable.
  Finally, it displays a message box with instructions to the user.
#>

# Path to the V9_Milestone_FINAL.exe executable
$ExecutablePath = "c:\Users\Mohammed Khalid\OneDrive\Desktop\WSGTA\Github\universal-or-strategy\V9_ExternalRemote\bin\Release\net6.0-windows\V9_Milestone_FINAL.exe"
$ProcessName = "V9_Milestone_FINAL"

Write-Host "Closing any existing instances of '$ProcessName.exe'..."

# Attempt to gracefully close the process
try {
    $processes = Get-Process -Name $ProcessName -ErrorAction Stop
    foreach ($process in $processes) {
        Write-Host "  Attempting to gracefully close process ID $($process.Id)..."
        $process.CloseMainWindow() | Out-Null
        $process.WaitForExit(5000)
        if (-not $process.HasExited) {
            Write-Warning "  Force-killing process ID $($process.Id)..."
            $process.Kill()
            $process.WaitForExit()
        }
        Write-Host "  Process ID $($process.Id) closed."
    }
}
catch {
    if ($_.Exception.GetType().FullName -eq "System.Management.Automation.ItemNotFoundException") {
        Write-Host "  No running '$ProcessName.exe' processes found."
    }
    else {
        Write-Error "  Error closing processes: $($_.Exception.Message)"
    }
}

Write-Host "Launching V9.0.12 from '$ExecutablePath'..."

try {
    $fileInfo = Get-Item $ExecutablePath
    Write-Host "Executable Found: $($fileInfo.FullName)"
    Write-Host "Created: $($fileInfo.CreationTime)"
    Write-Host "Last Modified: $($fileInfo.LastWriteTime)"
    
    Start-Process -FilePath $ExecutablePath -WorkingDirectory (Split-Path -Path $ExecutablePath -Parent)
    Write-Host "Application launched successfully!"
}
catch {
    Write-Error "Failed to launch application: $($_.Exception.Message)"
    exit 1
}

# Display instructions
Add-Type -AssemblyName PresentationFramework
$message = "V9.1.6 MGC EMA Alignment Ready!" + [Environment]::NewLine + [Environment]::NewLine +
"Next Steps:" + [Environment]::NewLine +
"1. Type 'MGC_TEST' in the symbol input box" + [Environment]::NewLine +
"2. Press Enter or click 'Set Symbol'" + [Environment]::NewLine +
"3. Wait 30-40 seconds for test to complete" + [Environment]::NewLine +
"4. Check results in v9_shotgun_results.txt"

[System.Windows.MessageBox]::Show($message, "Shotgun Test Ready", [System.Windows.MessageBoxButton]::OK, [System.Windows.MessageBoxImage]::Information) | Out-Null

Write-Host "`nScript completed. The application is running."
