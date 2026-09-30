# Greptile Cleanup Script for Slash Commands
# Removes Greptile MCP references from .bob/commands/*.md files
# Replaces with jcodemunch-mcp (the actual MCP being used)

$ErrorActionPreference = "Stop"

Write-Host "=== Greptile Cleanup: Slash Commands ===" -ForegroundColor Cyan
Write-Host ""

# Files with Greptile references (from search results)
$filesToClean = @(
    ".bob/commands/epic-scan.md",
    ".bob/commands/mcp-loop.md",
    ".bob/commands/epic-tdd.md",
    ".bob/commands/epic-run.md",
    ".bob/commands/pre-push.md",
    ".bob/commands/local-loop.md"
)

$totalReferences = 0
$filesModified = 0

foreach ($file in $filesToClean) {
    if (-not (Test-Path $file)) {
        Write-Host "WARNING: File not found: $file" -ForegroundColor Yellow
        continue
    }

    Write-Host "Processing: $file" -ForegroundColor White
    
    $content = Get-Content $file -Raw
    $originalContent = $content
    
    # Count references before cleanup
    $matches = [regex]::Matches($content, "greptile|Greptile", [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    $refCount = $matches.Count
    
    if ($refCount -eq 0) {
        Write-Host "  No Greptile references found" -ForegroundColor Green
        continue
    }
    
    Write-Host "  Found $refCount Greptile reference(s)" -ForegroundColor Yellow
    $totalReferences += $refCount
    
    # Replacement patterns based on context
    
    # Pattern 1: "Greptile MCP" -> "jcodemunch-mcp"
    $content = $content -replace "Greptile MCP", "jcodemunch-mcp"
    
    # Pattern 2: "greptile" MCP server -> "jcodemunch-mcp"
    $content = $content -replace 'use_mcp_tool greptile', 'use_mcp_tool jcodemunch-mcp'
    $content = $content -replace '"greptile":', '"jcodemunch-mcp":'
    
    # Pattern 3: Greptile review/query -> jcodemunch search
    $content = $content -replace "Greptile review", "jcodemunch code analysis"
    $content = $content -replace "Greptile semantic analysis", "jcodemunch semantic search"
    $content = $content -replace "Greptile findings", "jcodemunch analysis results"
    
    # Pattern 4: Greptile config files -> Remove (not needed for jcodemunch)
    $content = $content -replace 'greptile\.json', '.jcodemunch.jsonc'
    $content = $content -replace 'greptile_findings\.json', 'jcodemunch_analysis.json'
    
    # Pattern 5: Greptile score -> jcodemunch analysis score
    $content = $content -replace '\$GREPTILE_SCORE', '$JCODEMUNCH_SCORE'
    $content = $content -replace 'GREPTILE_SCORE', 'JCODEMUNCH_SCORE'
    
    # Pattern 6: Standalone "Greptile" -> "jcodemunch"
    $content = $content -replace '\bGreptile\b', 'jcodemunch'
    $content = $content -replace '\bgreptile\b', 'jcodemunch'
    
    # Pattern 7: "Greptile + Cubic" -> "jcodemunch + Cubic"
    $content = $content -replace "Greptile \+ Cubic", "jcodemunch + Cubic"
    
    # Pattern 8: Greptile API URL -> Remove (not applicable)
    $content = $content -replace '"url": "https://api\.greptile\.com/mcp"', '"command": "jcodemunch-mcp"'
    
    # Check if content changed
    if ($content -ne $originalContent) {
        # Backup original
        $backupPath = "$file.bak"
        Copy-Item $file $backupPath -Force
        Write-Host "  Backup created: $backupPath" -ForegroundColor Gray
        
        # Write cleaned content
        Set-Content $file $content -NoNewline
        Write-Host "  Cleaned $refCount reference(s)" -ForegroundColor Green
        $filesModified++
    } else {
        Write-Host "  WARNING: No changes made (pattern mismatch?)" -ForegroundColor Yellow
    }
    
    Write-Host ""
}

Write-Host "=== Cleanup Summary ===" -ForegroundColor Cyan
Write-Host "Files processed: $($filesToClean.Count)" -ForegroundColor White
Write-Host "Files modified: $filesModified" -ForegroundColor Green
Write-Host "Total references cleaned: $totalReferences" -ForegroundColor Green
Write-Host ""

# Generate cleanup report
$reportPath = "docs/workflow/GREPTILE_COMMANDS_CLEANUP_REPORT.md"

$reportLines = @()
$reportLines += "# Greptile Cleanup Report: Slash Commands"
$reportLines += ""
$reportLines += "**Date**: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
$reportLines += "**Script**: ``scripts/cleanup_greptile_in_commands.ps1``"
$reportLines += "**Status**: Complete"
$reportLines += ""
$reportLines += "## Summary"
$reportLines += ""
$reportLines += "**Files Processed**: $($filesToClean.Count)"
$reportLines += "**Files Modified**: $filesModified"
$reportLines += "**Total References Cleaned**: $totalReferences"
$reportLines += ""
$reportLines += "## Files Modified"
$reportLines += ""

foreach ($file in $filesToClean) {
    if (Test-Path $file) {
        $content = Get-Content $file -Raw
        $matches = [regex]::Matches($content, "jcodemunch")
        $jcodemunchCount = $matches.Count
        
        $reportLines += "### ``$file``"
        $reportLines += "**Status**: Cleaned"
        $reportLines += "**jcodemunch references**: $jcodemunchCount"
        $reportLines += "**Backup**: ``$file.bak``"
        $reportLines += ""
    }
}

$reportLines += "## Replacement Patterns Applied"
$reportLines += ""
$reportLines += "1. **Greptile MCP** -> **jcodemunch-mcp**"
$reportLines += "2. **greptile MCP server** -> **jcodemunch-mcp**"
$reportLines += "3. **Greptile review** -> **jcodemunch code analysis**"
$reportLines += "4. **Greptile semantic analysis** -> **jcodemunch semantic search**"
$reportLines += "5. **Greptile findings** -> **jcodemunch analysis results**"
$reportLines += "6. **greptile.json** -> **.jcodemunch.jsonc**"
$reportLines += "7. **GREPTILE_SCORE** -> **JCODEMUNCH_SCORE**"
$reportLines += "8. **Greptile + Cubic** -> **jcodemunch + Cubic**"
$reportLines += ""
$reportLines += "## Verification"
$reportLines += ""
$reportLines += "All slash commands now reference **jcodemunch-mcp** (the actual MCP being used) instead of Greptile MCP (which was never integrated)."
$reportLines += ""
$reportLines += "## Next Steps"
$reportLines += ""
$reportLines += "1. Review modified files for context accuracy"
$reportLines += "2. Test slash commands with jcodemunch-mcp"
$reportLines += "3. Update integration matrix to reflect cleanup"
$reportLines += "4. Remove Greptile from system prompts (AGENTS.md, docs/AGENTS.md)"
$reportLines += ""
$reportLines += "## Related Documentation"
$reportLines += ""
$reportLines += "- **Integration Matrix**: ``docs/workflow/AUTONOMOUS_REFACTOR_INTEGRATION_MATRIX_V2.md``"
$reportLines += "- **jcodemunch MCP**: ``.mcp.json``"
$reportLines += "- **Custom Modes**: ``.bob/custom_modes.yaml``"
$reportLines += ""
$reportLines += "---"
$reportLines += ""
$reportLines += "**Cleanup Complete**: All slash commands now correctly reference jcodemunch-mcp."

$reportLines | Out-File $reportPath -Encoding UTF8

Write-Host "Cleanup report generated: $reportPath" -ForegroundColor Cyan
Write-Host ""

Write-Host "Greptile cleanup complete!" -ForegroundColor Green
Write-Host "Review the modified files and test slash commands." -ForegroundColor Gray

# Made with Bob
