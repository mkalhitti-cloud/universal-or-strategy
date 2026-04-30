$brainPath = "C:\Users\Mohammed Khalid\.gemini\antigravity\brain\"
$files = Get-ChildItem -Path $brainPath -Recurse -Filter overview.txt
foreach ($file in $files) {
    $matches = Select-String -Path $file.FullName -Pattern "Next Agent Prompt"
    if ($matches) {
        foreach ($match in $matches) {
            Write-Output "File: $($file.FullName)"
            Write-Output "Line: $($match.LineNumber)"
            Write-Output "Content: $($match.Line)"
            Write-Output "---"
        }
    }
}
