#!/usr/bin/env pwsh
# File Encoding Validation Script (V12.33)
# Checks all source files for UTF-8 encoding compliance
# Part of FILE_ENCODING_PROTOCOL.md

param(
    [string]$Path = "src",
    [switch]$Fix,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

Write-Host "`n=== File Encoding Validation ===" -ForegroundColor Cyan
Write-Host "Scanning: $Path" -ForegroundColor Gray

$extensions = @("*.cs", "*.md", "*.json", "*.yaml", "*.yml", "*.sh", "*.ps1", "*.py", "*.txt")
$invalid = @()
$fixed = @()

foreach ($ext in $extensions) {
    $files = Get-ChildItem -Path $Path -Filter $ext -Recurse -ErrorAction SilentlyContinue
    
    foreach ($file in $files) {
        if ($Verbose) {
            Write-Host "Checking: $($file.FullName)" -ForegroundColor Gray
        }
        
        $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
        
        if ($bytes.Length -lt 2) {
            continue  # Empty or single-byte file
        }
        
        $isInvalid = $false
        $encoding = "UTF-8"
        
        # Check for UTF-16 LE (FF FE)
        if ($bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) {
            $isInvalid = $true
            $encoding = "UTF-16 LE"
        }
        # Check for UTF-16 BE (FE FF)
        elseif ($bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF) {
            $isInvalid = $true
            $encoding = "UTF-16 BE"
        }
        # Check for UTF-8 with BOM (EF BB BF)
        elseif ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
            $isInvalid = $true
            $encoding = "UTF-8 with BOM"
        }
        
        if ($isInvalid) {
            $invalid += [PSCustomObject]@{
                File = $file.FullName
                Encoding = $encoding
            }
            
            if ($Fix) {
                try {
                    Write-Host "  Converting: $($file.FullName)" -ForegroundColor Yellow
                    
                    # Read with appropriate encoding
                    if ($encoding -like "UTF-16*") {
                        $content = Get-Content $file.FullName -Encoding Unicode
                    } else {
                        $content = Get-Content $file.FullName -Encoding UTF8
                    }
                    
                    # Write as UTF-8 without BOM
                    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
                    [System.IO.File]::WriteAllLines($file.FullName, $content, $utf8NoBom)
                    
                    $fixed += $file.FullName
                    Write-Host "  ✅ Converted to UTF-8" -ForegroundColor Green
                }
                catch {
                    Write-Host "  ❌ Conversion failed: $_" -ForegroundColor Red
                }
            }
        }
    }
}

# Report results
Write-Host "`n=== Results ===" -ForegroundColor Cyan

if ($invalid.Count -eq 0) {
    Write-Host "✅ All files use UTF-8 encoding (no BOM)" -ForegroundColor Green
    exit 0
}
else {
    Write-Host "❌ Found $($invalid.Count) file(s) with invalid encoding:" -ForegroundColor Red
    $invalid | Format-Table -AutoSize
    
    if ($Fix) {
        Write-Host "`n✅ Fixed $($fixed.Count) file(s)" -ForegroundColor Green
        
        if ($fixed.Count -lt $invalid.Count) {
            Write-Host "⚠️  $($invalid.Count - $fixed.Count) file(s) could not be fixed" -ForegroundColor Yellow
            exit 1
        }
        else {
            Write-Host "✅ All files converted successfully" -ForegroundColor Green
            exit 0
        }
    }
    else {
        Write-Host "`nTo fix automatically, run:" -ForegroundColor Yellow
        Write-Host "  .\scripts\check_encoding.ps1 -Fix" -ForegroundColor White
        exit 1
    }
}

# Made with Bob
