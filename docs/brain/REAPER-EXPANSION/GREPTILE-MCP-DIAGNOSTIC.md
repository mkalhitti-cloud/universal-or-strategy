# Greptile MCP Connection Diagnostic

## Current Status
- ❌ Greptile server NOT appearing in Bob IDE MCP settings UI
- ✅ jcodemunch working
- ✅ sequential-thinking working
- ✅ Configuration exists in both global and project files
- ✅ JSON is valid (no BOM, no comments)

## Configuration Tested

### Global: `~/.bob/settings/mcp_settings.json`
```json
"greptile": {
  "url": "https://api.greptile.com/mcp",
  "type": "http",
  "headers": {
    "Authorization": "Bearer vob20OZM949/QgQ/IPxtzrU7lJDMGEFuFvwQ8D0UxO3lJ2CG"
  },
  "alwaysAllow": ["query", "search", "search_codebase", "query_codebase", "semantic_search"]
}
```

### Project: `.bob/mcp.json`
```json
{
  "mcpServers": {
    "greptile": {
      "url": "https://api.greptile.com/mcp",
      "type": "http",
      "headers": {
        "Authorization": "Bearer vob20OZM949/QgQ/IPxtzrU7lJDMGEFuFvwQ8D0UxO3lJ2CG"
      },
      "alwaysAllow": ["query", "search", "search_codebase", "query_codebase", "semantic_search"]
    }
  }
}
```

## Issues Encountered

1. **HTTP 405 Error** (before restart): SSE transport was being attempted
2. **Silent Rejection** (after restart): Server doesn't appear in UI despite valid config

## Hypothesis

Bob IDE may not support HTTP/SSE transport for MCP servers, or requires additional configuration. The documentation shows conflicting information:
- Bob IDE docs say to omit `"type"` field for SSE
- Greptile docs show `"type": "http"` is required

## Possible Root Causes

1. **Bob IDE doesn't support HTTP transport**: Only STDIO transport may be supported
2. **Greptile endpoint incompatibility**: The `/mcp` endpoint may not implement MCP protocol correctly
3. **Missing configuration field**: May need additional parameters
4. **Version mismatch**: Bob IDE version may not support this transport type

## Recommended Next Steps

1. Check Bob IDE version and MCP transport support
2. Contact Greptile support for Bob IDE-specific configuration
3. Check if Greptile has a STDIO-based MCP server instead of HTTP
4. Look for Bob IDE error logs that explain why the server is rejected
5. Try alternative MCP configuration formats

## Alternative Approach

If Greptile doesn't work via MCP, consider:
- Using Greptile's REST API directly via `execute_command` with curl
- Creating a custom STDIO MCP server wrapper for Greptile's API
- Using a different code search tool that has better MCP support