$path = "C:\Users\Mohammed Khalid\.gemini\antigravity\brain\733c7fbb-ef32-4a6b-b2a4-4c9bd313e6d5\.system_generated\logs\overview.txt"
$lines = Get-Content $path
foreach ($line in $lines) {
    if ($line -match "Silicon-Safe Trojan Horse") {
        Write-Output $line
    }
}
