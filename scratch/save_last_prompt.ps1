$brainPath = "C:\Users\Mohammed Khalid\.gemini\antigravity\brain\"
$currentConv = "7acbdd7b-a6e0-4d52-b672-ce3b2842619c"
$files = Get-ChildItem -Path $brainPath -Recurse -Filter overview.txt | Where-Object { $_.FullName -notmatch $currentConv } | Sort-Object LastWriteTime -Descending
foreach ($file in $files) {
    $matches = Select-String -Path $file.FullName -Pattern "Next Agent Prompt"
    if ($matches) {
        $lastMatch = $matches | Select-Object -Last 1
        $result = "File: $($file.FullName)`nLine: $($lastMatch.LineNumber)`nContent: $($lastMatch.Line)"
        $result | Out-File "c:\WSGTA\universal-or-strategy\scratch\last_prompt_result.txt"
        break 
    }
}
