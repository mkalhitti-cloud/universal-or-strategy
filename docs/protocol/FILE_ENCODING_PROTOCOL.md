# File Encoding Protocol (V12.33)

**Version**: 1.0  
**Effective**: 2026-06-16  
**Status**: MANDATORY for all agents and workflows  
**Severity**: P0 (Blocking)

## Problem Statement

**Incident**: EPIC-CCN-027 TICKET-2 execution failed due to UTF-16 encoding in `src/V12_002.SIMA.Dispatch.cs`. Bob CLI's `apply_diff` tool achieved 0% similarity match, causing 7+ minutes of wasted execution time and blocking epic progress.

**Root Cause**: Windows files may default to UTF-16 encoding, which is incompatible with:
- Bob CLI's `apply_diff` tool (requires UTF-8)
- Git diff tools (expect UTF-8)
- Most Unix-based tooling (assumes UTF-8)
- Cross-platform collaboration (UTF-8 is universal standard)

## Mandatory Encoding Standard

**ALL source files MUST use UTF-8 encoding without BOM (Byte Order Mark).**

### Scope
- ✅ **All `.cs` files** (C# source code)
- ✅ **All `.md` files** (Markdown documentation)
- ✅ **All `.json` files** (Configuration, manifests)
- ✅ **All `.yaml`/`.yml` files** (Configuration)
- ✅ **All `.sh` files** (Shell scripts)
- ✅ **All `.ps1` files** (PowerShell scripts)
- ✅ **All `.py` files** (Python scripts)
- ✅ **All `.txt` files** (Plain text)

### Exceptions
- ❌ **Binary files** (`.dll`, `.exe`, `.png`, etc.) - Not applicable
- ❌ **Legacy files** requiring specific encoding - Must be documented in `.encodingignore`

## Detection Methods

### Method 1: PowerShell (Windows)
```powershell
# Check single file
$bytes = [System.IO.File]::ReadAllBytes("path/to/file.cs")
if ($bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) {
    Write-Host "UTF-16 LE detected (INVALID)"
} elseif ($bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF) {
    Write-Host "UTF-16 BE detected (INVALID)"
} elseif ($bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
    Write-Host "UTF-8 with BOM (DISCOURAGED)"
} else {
    Write-Host "UTF-8 without BOM (CORRECT)"
}
```

### Method 2: File Command (Linux/Mac)
```bash
file -i path/to/file.cs
# Expected: text/x-c; charset=utf-8
# Invalid: text/x-c; charset=utf-16le
```

### Method 3: Visual Inspection
- Open file in hex editor
- Check first bytes:
  - `FF FE` = UTF-16 LE (INVALID)
  - `FE FF` = UTF-16 BE (INVALID)
  - `EF BB BF` = UTF-8 with BOM (DISCOURAGED)
  - No BOM = UTF-8 without BOM (CORRECT)

### Method 4: Bob CLI Error Pattern
```
Error: No sufficiently similar match found (0% similar, needs 100%)
Similarity Score: 0%
```
If Bob's `apply_diff` shows 0% similarity on valid content, suspect encoding issue.

## Conversion Procedures

### Procedure 1: PowerShell (Single File)
```powershell
# Convert UTF-16 to UTF-8 (no BOM)
Get-Content "path/to/file.cs" -Encoding Unicode | 
    Set-Content -Encoding UTF8 "path/to/file.cs"

# Verify conversion
$bytes = [System.IO.File]::ReadAllBytes("path/to/file.cs")
if ($bytes[0] -ne 0xFF -and $bytes[0] -ne 0xFE -and $bytes[0] -ne 0xEF) {
    Write-Host "✅ Conversion successful"
} else {
    Write-Host "❌ Conversion failed"
}
```

### Procedure 2: PowerShell (Batch Conversion)
```powershell
# Convert all .cs files in src/ to UTF-8
Get-ChildItem -Path "src" -Filter "*.cs" -Recurse | ForEach-Object {
    $content = Get-Content $_.FullName -Encoding Unicode
    Set-Content -Path $_.FullName -Value $content -Encoding UTF8
    Write-Host "Converted: $($_.FullName)"
}
```

### Procedure 3: Git Pre-Commit Hook
```bash
#!/bin/bash
# .git/hooks/pre-commit
# Reject commits with non-UTF-8 files

for file in $(git diff --cached --name-only --diff-filter=ACM | grep -E '\.(cs|md|json|yaml|yml|sh|ps1|py|txt)$'); do
    if file -i "$file" | grep -qv "charset=utf-8"; then
        echo "ERROR: $file is not UTF-8 encoded"
        echo "Run: Get-Content '$file' -Encoding Unicode | Set-Content -Encoding UTF8 '$file'"
        exit 1
    fi
done
```

## Prevention Strategies

### Strategy 1: VSCode Settings
Add to `.vscode/settings.json`:
```json
{
    "files.encoding": "utf8",
    "files.autoGuessEncoding": false,
    "files.eol": "\n"
}
```

### Strategy 2: EditorConfig
Add to `.editorconfig`:
```ini
[*.{cs,md,json,yaml,yml,sh,ps1,py,txt}]
charset = utf-8
end_of_line = lf
insert_final_newline = true
```

### Strategy 3: Git Attributes
Add to `.gitattributes`:
```
*.cs text eol=lf encoding=utf-8
*.md text eol=lf encoding=utf-8
*.json text eol=lf encoding=utf-8
*.yaml text eol=lf encoding=utf-8
*.yml text eol=lf encoding=utf-8
*.sh text eol=lf encoding=utf-8
*.ps1 text eol=lf encoding=utf-8
*.py text eol=lf encoding=utf-8
*.txt text eol=lf encoding=utf-8
```

### Strategy 4: Pre-Flight Check Script
Create `scripts/check_encoding.ps1`:
```powershell
#!/usr/bin/env pwsh
# Check all source files for correct encoding

$invalid = @()
Get-ChildItem -Path "src" -Filter "*.cs" -Recurse | ForEach-Object {
    $bytes = [System.IO.File]::ReadAllBytes($_.FullName)
    if ($bytes.Length -ge 2) {
        if (($bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) -or 
            ($bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF) -or
            ($bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)) {
            $invalid += $_.FullName
        }
    }
}

if ($invalid.Count -gt 0) {
    Write-Host "❌ ENCODING VIOLATIONS DETECTED:" -ForegroundColor Red
    $invalid | ForEach-Object { Write-Host "  - $_" }
    exit 1
} else {
    Write-Host "✅ All files use UTF-8 encoding" -ForegroundColor Green
    exit 0
}
```

## Integration Points

### 1. Pre-Push Validation
Add to `scripts/pre_push_validation.ps1`:
```powershell
# Check 14: File Encoding
Write-Host "`n[14/14] Checking file encoding..." -ForegroundColor Cyan
& "$PSScriptRoot\check_encoding.ps1"
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Encoding check failed" -ForegroundColor Red
    $script:failedChecks += "Encoding"
}
```

### 2. Epic Workflow Pre-Flight
Add to Phase -1 (Pre-flight) checks:
```bash
# Verify file encoding before epic execution
powershell -File ./scripts/check_encoding.ps1
if [ $? -ne 0 ]; then
    echo "ERROR: Encoding violations detected"
    exit 1
fi
```

### 3. Bob CLI Wrapper
Modify `scripts/execute_epic_local.ps1`:
```powershell
# Pre-execution encoding check
Write-Host "Checking file encoding..." -ForegroundColor Cyan
$targetFile = "src/V12_002.SIMA.Dispatch.cs"  # Example
$bytes = [System.IO.File]::ReadAllBytes($targetFile)
if ($bytes[0] -eq 0xFF -or $bytes[0] -eq 0xFE) {
    Write-Host "❌ UTF-16 detected in $targetFile - converting to UTF-8..." -ForegroundColor Yellow
    Get-Content $targetFile -Encoding Unicode | Set-Content -Encoding UTF8 $targetFile
    Write-Host "✅ Converted to UTF-8" -ForegroundColor Green
}
```

### 4. Wave Execution Scripts
Add to all `_pX_*.sh` scripts:
```bash
# Encoding pre-check
if file -i "$TARGET_FILE" | grep -qv "charset=utf-8"; then
    echo "ERROR: $TARGET_FILE is not UTF-8"
    exit 1
fi
```

## Enforcement

### Severity Levels
- **P0 (Blocking)**: UTF-16 encoding in `src/` files
- **P1 (Warning)**: UTF-8 with BOM in `src/` files
- **P2 (Info)**: Non-UTF-8 in `docs/` files

### Violation Response
1. **Detect**: Run `check_encoding.ps1`
2. **Convert**: Use PowerShell conversion procedure
3. **Verify**: Re-run `check_encoding.ps1`
4. **Commit**: Add converted files to git
5. **Document**: Log violation in session notes

### Audit Frequency
- **Pre-commit**: Every commit (via git hook)
- **Pre-push**: Every push (via pre_push_validation.ps1)
- **Pre-epic**: Every epic execution (Phase -1)
- **Weekly**: Automated scan via CI/CD

## Recovery Procedure

If encoding issue detected mid-execution:

1. **Stop execution immediately**
   ```powershell
   Stop-Process -Name "IBM Bob" -Force
   ```

2. **Convert affected files**
   ```powershell
   Get-Content "path/to/file.cs" -Encoding Unicode | 
       Set-Content -Encoding UTF8 "path/to/file.cs"
   ```

3. **Verify conversion**
   ```powershell
   & scripts/check_encoding.ps1
   ```

4. **Restart execution**
   ```powershell
   .\scripts\execute_epic_local.ps1 -EpicId "EPIC-XXX" -TicketId "TICKET-Y"
   ```

5. **Document incident**
   - Add to session notes
   - Update protocol if new edge case discovered

## References

- **Incident Report**: EPIC-CCN-027 TICKET-2 failure (2026-06-16)
- **Root Cause**: UTF-16 encoding in `src/V12_002.SIMA.Dispatch.cs`
- **Impact**: 7+ minutes wasted execution, epic blocked
- **Resolution**: Convert to UTF-8, harden protocol

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-06-16 | Initial protocol (post-EPIC-CCN-027 incident) |

---

**MANDATORY COMPLIANCE**: All agents MUST verify file encoding before ANY file modification operation.