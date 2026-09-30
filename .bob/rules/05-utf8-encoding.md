# UTF-8 Encoding Mandate (V12.35)

## Rule

**ALL files in this repository MUST be UTF-8 encoded without BOM.**

UTF-16 (wide-character) encoding is BANNED. It causes every agent tool to mis-read files and produces
the warning: *"file is wide-character encoded (UTF-16)"* — forcing agents to waste tokens working around it.

## Auto-Repair (PERMANENT FIX)

The hook `.bob/hooks/utf8_repair.py` runs **automatically at `run_order: 1`** (after the rules gate,
before any agent work begins). It scans all `.md`, `.yaml`, `.json`, `.cs`, `.ps1` files and silently
converts any UTF-16 or UTF-8-BOM files to UTF-8 no-BOM.

**Agents MUST NOT manually work around encoding issues** — the hook handles it. If you still see a
wide-character warning after the hook runs, it means the hook failed to run. In that case:
```powershell
python .bob/hooks/utf8_repair.py
```

## Enforcement

### Creating new files
- Use `write_file` (Bob IDE native tool) — it always writes UTF-8 no-BOM by default.
- In PowerShell, **never** use `Set-Content` without `-Encoding UTF8` — the default is UTF-16 on PS 5.x.
- Correct PowerShell pattern:
  ```powershell
  # WRONG (PS 5.x default = UTF-16 LE)
  Set-Content path.md "content"

  # CORRECT
  [System.IO.File]::WriteAllText('path.md', $content, (New-Object System.Text.UTF8Encoding $false))
  # or
  $content | Out-File -FilePath path.md -Encoding utf8NoBOM
  ```

### Detecting violations
Run this to find any UTF-16 files before pushing:
```powershell
Get-ChildItem . -Filter '*.md' -Recurse | ForEach-Object {
    $b = [System.IO.File]::ReadAllBytes($_.FullName)
    if ($b.Count -ge 2 -and $b[0] -eq 0xFF -and $b[1] -eq 0xFE) { $_.FullName }
}
```

### Bulk repair
```powershell
$utf8 = New-Object System.Text.UTF8Encoding $false
Get-ChildItem . -Filter '*.md' -Recurse | ForEach-Object {
    $b = [System.IO.File]::ReadAllBytes($_.FullName)
    if ($b.Count -ge 2 -and $b[0] -eq 0xFF -and $b[1] -eq 0xFE) {
        $text = [System.IO.File]::ReadAllText($_.FullName, [System.Text.Encoding]::Unicode)
        [System.IO.File]::WriteAllText($_.FullName, $text, $utf8)
        Write-Host "Fixed: $($_.Name)"
    }
}
```

## Root Cause

PowerShell 5.x `Set-Content` and `Out-File` write UTF-16 LE by default. This was the source encoding
used when the `docs/standards/jane-street/` and `docs/brain/` files were originally generated.

## Applies To

- All `.md`, `.txt`, `.json`, `.yaml`, `.cs`, `.ps1` files
- All agents: Bob, Gemini CLI, Codex, any external contributor

## Effective

2026-07-07 (V12.35) — retroactively fixes 48 files in `docs/`
