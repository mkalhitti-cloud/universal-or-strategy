$brainPath = "C:\Users\Mohammed Khalid\.gemini\antigravity\brain\"
$currentConv = "7acbdd7b-a6e0-4d52-b672-ce3b2842619c"
$files = Get-ChildItem -Path $brainPath -Recurse -Filter overview.txt | Where-Object { $_.FullName -notmatch $currentConv } | Sort-Object LastWriteTime -Descending
foreach ($file in $files) {
    $matches = Select-String -Path $file.FullName -Pattern "Next Agent Prompt"
    if ($matches) {
        Write-Output "File: $($file.FullName)"
        foreach ($match in $matches | Select-Object -Last 1) {
            Write-Output "Line: $($match.LineNumber)"
            Write-Output "Content: $($match.Line)"
            Write-Output "---"
        }
        break 
    }
}
