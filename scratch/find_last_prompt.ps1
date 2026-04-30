$brainPath = "C:\Users\Mohammed Khalid\.gemini\antigravity\brain\"
$files = Get-ChildItem -Path $brainPath -Recurse -Filter overview.txt | Sort-Object LastWriteTime -Descending
foreach ($file in $files) {
    $matches = Select-String -Path $file.FullName -Pattern "Next Agent Prompt"
    if ($matches) {
        Write-Output "File: $($file.FullName)"
        foreach ($match in $matches | Select-Object -Last 1) {
            Write-Output "Line: $($match.LineNumber)"
            Write-Output "Content: $($match.Line)"
            Write-Output "---"
        }
        break # Only show the most recent file with a match
    }
}
