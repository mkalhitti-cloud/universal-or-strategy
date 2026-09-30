#!/bin/bash
# Update VM .mcp.json.vm to remove Greptile MCP
# Version: 1.0
# Date: 2026-06-20

set -e

echo "=== Updating VM MCP Configuration ==="
echo "Removing Greptile MCP from .mcp.json.vm"

# Create new .mcp.json.vm without Greptile
cat > .mcp.json.vm << 'EOF'
{
  "mcpServers": {
    "jcodemunch-mcp": {
      "type": "stdio",
      "command": "jcodemunch-mcp",
      "args": []
    },
    "graphify": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-graphify"]
    },
    "sequential-thinking": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-sequential-thinking"]
    }
  }
}
EOF

echo "✅ .mcp.json.vm updated successfully"
echo ""
echo "Configuration now includes:"
echo "  - jcodemunch-mcp (code navigation)"
echo "  - graphify (codebase structure)"
echo "  - sequential-thinking (complex reasoning)"
echo ""
echo "Greptile MCP removed (no PR exists during wave execution)"

# Made with Bob
