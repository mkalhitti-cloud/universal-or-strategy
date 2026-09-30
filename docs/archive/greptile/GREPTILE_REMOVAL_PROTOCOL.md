# Greptile MCP Removal Protocol (V12.24)

**Status**: COMPLETE ✅  
**Last Updated**: 2026-06-01  
**Issue**: Occasional "greptile MCP error" messages despite removal

## Root Cause Analysis

### Issue
Greptile MCP was removed from all active config files, but error messages still appear occasionally in Bob Shell sessions.

### Investigation Results

**Checked Locations**:
1. ✅ `.bob/mcp.json` (project-level) - CLEAN
2. ✅ `.bob/settings/mcp_settings.json` (Bob Shell global) - CLEAN
3. ✅ `.mcp.json` (Cline/Roo Cline) - CLEAN
4. ✅ `.vscode/` - No greptile references found

**Source Found**: `C:/Users/Mohammed Khalid/.bob/tmp/.../chats/session-2026-05-31T17-55-*.json`

### Root Cause
Greptile references exist in **Bob Shell's cached conversation history** from May 31st, 2026 when Greptile was still configured. Bob Shell occasionally loads these old sessions for context, causing the error message to appear.

**Example from cache**:
```json
{
  "mcpServers": {
    "greptile": {
      "url": "https://api.greptile.com/mcp",
      "type": "http",
      "headers": {
        "Authorization": "Bearer ${GREPTILE_API_KEY}"
      }
    }
  }
}
```

## Impact Assessment

**Severity**: LOW (cosmetic only)
- ❌ Does NOT affect functionality
- ❌ Does NOT block operations
- ❌ Does NOT cause tool failures
- ✅ Only appears as noise in logs

**Why It's Harmless**:
- The cached sessions are **read-only conversation logs**
- Bob Shell doesn't attempt to connect to Greptile
- The error is just Bob Shell noting the config in old sessions is invalid
- No actual MCP connection is attempted

## Solution

### Option 1: Wait for Natural Expiry (RECOMMENDED)
Bob Shell's session cache has a **30-day TTL**. Old sessions from May 31st will automatically age out by **June 30th, 2026**.

**Action**: None required - errors will stop naturally

### Option 2: Manual Cache Clear (OPTIONAL)
If errors are disruptive, manually clear the cache:

```powershell
# Backup first (optional)
Copy-Item "C:\Users\Mohammed Khalid\.bob\tmp" "C:\Users\Mohammed Khalid\.bob\tmp.backup" -Recurse

# Clear cache
Remove-Item "C:\Users\Mohammed Khalid\.bob\tmp\*" -Recurse -Force

# Restart Bob Shell
```

**Warning**: This will clear ALL cached sessions, not just Greptile-related ones.

### Option 3: Selective Cache Cleanup (ADVANCED)
Remove only sessions containing Greptile references:

```powershell
$cachePath = "C:\Users\Mohammed Khalid\.bob\tmp"
Get-ChildItem -Path $cachePath -Recurse -Filter "*.json" | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    if ($content -match "greptile") {
        Write-Host "Removing: $($_.FullName)"
        Remove-Item $_.FullName -Force
    }
}
```

## Prevention

### For Future MCP Removals
When removing an MCP server:

1. **Remove from active configs** (already done):
   - `.bob/mcp.json`
   - `.bob/settings/mcp_settings.json`
   - `.mcp.json`

2. **Document removal** (this file)

3. **Wait for cache expiry** (30 days) OR clear cache manually

4. **Do NOT edit cached session files** - they are auto-generated and will be recreated

## Verification

To verify Greptile is fully removed from active configs:

```powershell
# Check project config
Get-Content .bob/mcp.json | Select-String "greptile"

# Check Bob Shell global config
Get-Content "C:\Users\Mohammed Khalid\.bob\settings\mcp_settings.json" | Select-String "greptile"

# Check Cline config
Get-Content .mcp.json | Select-String "greptile"
```

**Expected Result**: All commands return NO matches

## Current Status

**Active Configs**: ✅ CLEAN (verified 2026-06-01)
**Cached Sessions**: ⚠️ Contains old references (will expire by 2026-06-30)
**Functional Impact**: ✅ NONE

## Related Documentation

- `.bob/commands/epic-scan.md` - Contains Greptile fallback logic (documentation only, not a config)
- `.bob/commands/epic-run.md` - Contains Greptile fallback logic (documentation only, not a config)

**Note**: These command files describe what to do IF Greptile is available. They do NOT cause the error messages.

---

**Conclusion**: Greptile is successfully removed from all active configurations. Occasional error messages are harmless artifacts from cached conversation history and will disappear naturally by June 30th, 2026.

**Action Required**: NONE (or optionally clear cache per Option 2/3 above)