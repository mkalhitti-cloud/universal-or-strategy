# Greptile MCP Authentication Troubleshooting

**Issue**: Greptile MCP server showing "Unauthorized: valid MCP authentication required" error in Bob IDE.

**Error Details**:
```json
{
  "jsonrpc": "2.0",
  "error": {
    "code": -32000,
    "message": "Unauthorized: valid MCP authentication required."
  },
  "id": null
}
```

## Current Configuration

**File**: `.bob/mcp.json`

```json
{
  "mcpServers": {
    "greptile": {
      "url": "https://api.greptile.com/mcp",
      "type": "streamable-http",
      "headers": {
        "Authorization": "Bearer pS4L5AeiV35cwkTbNyKVunYcJWQhbD+CI1M8hk6Hf4aMiLKR"
      },
      "disabled": false,
      "alwaysAllow": []
    }
  }
}
```

## Root Cause Analysis

The error indicates one of three issues:

1. **Expired Token**: The API token may have expired
2. **Invalid Token**: The token format or value is incorrect
3. **Revoked Token**: The token was revoked on Greptile's side

## Resolution Steps

### Option 1: Regenerate Greptile API Token (Recommended)

1. **Go to Greptile Dashboard**:
   - URL: https://app.greptile.com/settings/api-keys
   - Login with your account

2. **Revoke Old Token**:
   - Find token ending in `...LKR`
   - Click **Revoke**

3. **Generate New Token**:
   - Click **Create New API Key**
   - Name: "Bob IDE MCP Server"
   - Permissions: Read + Write
   - Copy the new token

4. **Update `.bob/mcp.json`**:
   ```json
   {
     "mcpServers": {
       "greptile": {
         "url": "https://api.greptile.com/mcp",
         "type": "streamable-http",
         "headers": {
           "Authorization": "Bearer <NEW_TOKEN_HERE>"
         },
         "disabled": false,
         "alwaysAllow": []
       }
     }
   }
   ```

5. **Restart Bob IDE**:
   - Close all Bob windows
   - Reopen Bob
   - Verify Greptile MCP shows no errors in Settings

### Option 2: Disable Greptile MCP (Temporary)

If you don't need Greptile MCP immediately:

```json
{
  "mcpServers": {
    "greptile": {
      "url": "https://api.greptile.com/mcp",
      "type": "streamable-http",
      "headers": {
        "Authorization": "Bearer pS4L5AeiV35cwkTbNyKVunYcJWQhbD+CI1M8hk6Hf4aMiLKR"
      },
      "disabled": true,  // Changed to true
      "alwaysAllow": []
    }
  }
}
```

### Option 3: Use Greptile CLI Instead

Greptile CLI doesn't require MCP authentication:

```bash
# Install Greptile CLI
npm install -g @greptile/cli

# Authenticate
greptile auth login

# Use in workflows
greptile search "your query"
greptile review --pr 6
```

## Impact Assessment

**Current Impact**: LOW
- Greptile MCP is one of 3 MCP servers (Greptile, Cubic, CodeAnt)
- CodeAnt MCP is working correctly
- Cubic CLI is installed (v1.6.6) and can be used via CLI
- jCodemunch MCP is working correctly

**Workaround**: Use Greptile CLI instead of MCP server for now.

## Verification

After fixing, verify Greptile MCP is working:

1. **Check Bob Settings**:
   - Open Bob IDE
   - Go to Settings → MCP
   - Greptile should show "Connected" status

2. **Test MCP Tool**:
   ```bash
   # In Bob chat
   Can you use the Greptile MCP to search for "ProcessBracketEvent"?
   ```

3. **Expected Response**:
   - Bob should successfully call Greptile MCP
   - Results should be returned without authentication errors

## Related Issues

- **Cubic MCP**: Currently disabled (OAuth flow not completed)
- **CodeAnt MCP**: Working correctly via `npx any-cli-mcp-server codeant`
- **jCodemunch MCP**: Working correctly

## Next Steps

1. ✅ **Immediate**: Disable Greptile MCP to clear error (or regenerate token)
2. ✅ **Short-term**: Use Greptile CLI for reviews
3. ⬜ **Long-term**: Re-enable Greptile MCP after token refresh

## References

- **Greptile Dashboard**: https://app.greptile.com
- **Greptile API Docs**: https://docs.greptile.com/api
- **Bob MCP Config**: `.bob/mcp.json`
- **MCP Troubleshooting**: `docs/mcp/CUBIC_MCP_TROUBLESHOOTING.md`