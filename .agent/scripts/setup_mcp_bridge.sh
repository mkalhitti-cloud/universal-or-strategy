#!/bin/bash
# Setup script for Gemini Flash MCP Delegation Bridge
# This script initializes all required components for automatic cost-optimized delegation

set -e

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
AGENT_DIR="$PROJECT_ROOT/.agent"

echo "🚀 Setting up Gemini Flash MCP Delegation Bridge"
echo "=================================================="
echo ""

# Check directories exist
echo "📁 Checking directory structure..."
mkdir -p "$AGENT_DIR/config"
mkdir -p "$AGENT_DIR/state"
mkdir -p "$AGENT_DIR/mcp-servers"
mkdir -p "$AGENT_DIR/rules"
echo "✓ Directories ready"

# Check for .env file
echo ""
echo "🔑 Checking API credentials..."
if [ -f "$PROJECT_ROOT/.env" ]; then
    if grep -q "GEMINI_API_KEY\|GOOGLE_API_KEY" "$PROJECT_ROOT/.env"; then
        echo "✓ API key found in .env"
    else
        echo "⚠️  .env exists but no API key found"
        echo "   Run: echo 'GEMINI_API_KEY=your_key_here' >> $PROJECT_ROOT/.env"
    fi
else
    echo "⚠️  .env file not found"
    echo "   Setup instructions:"
    echo "   1. Get API key from https://aistudio.google.com/app/apikey"
    echo "   2. cp $PROJECT_ROOT/.env.template $PROJECT_ROOT/.env"
    echo "   3. Edit .env and add your GEMINI_API_KEY"
fi

# Check Python dependencies
echo ""
echo "📦 Checking Python dependencies..."
if python3 -c "import google.genai" 2>/dev/null; then
    echo "✓ google-genai package installed"
else
    echo "⚠️  google-genai not installed"
    echo "   Run: pip install google-genai"
fi

# Check config files exist
echo ""
echo "⚙️  Checking configuration files..."
if [ -f "$AGENT_DIR/config/ai_capabilities.json" ]; then
    echo "✓ ai_capabilities.json found"
else
    echo "✗ ai_capabilities.json missing"
fi

if [ -f "$PROJECT_ROOT/claude.json" ]; then
    echo "✓ claude.json found"
else
    echo "✗ claude.json missing"
fi

if [ -f "$AGENT_DIR/rules/AUTO_DELEGATION_RULES.md" ]; then
    echo "✓ AUTO_DELEGATION_RULES.md found"
else
    echo "✗ AUTO_DELEGATION_RULES.md missing"
fi

# Check delegation_bridge.py
echo ""
echo "🌉 Checking delegation bridge..."
if [ -f "$AGENT_DIR/mcp-servers/delegation_bridge.py" ]; then
    if python3 -m py_compile "$AGENT_DIR/mcp-servers/delegation_bridge.py" 2>/dev/null; then
        echo "✓ delegation_bridge.py syntax valid"
    else
        echo "✗ delegation_bridge.py has syntax errors"
    fi
else
    echo "✗ delegation_bridge.py not found"
fi

# Summary
echo ""
echo "=================================================="
echo "📋 Setup Summary"
echo "=================================================="
echo ""
echo "Configuration Location: $PROJECT_ROOT"
echo "Auto-Delegation Status: $([ -f "$PROJECT_ROOT/claude.json" ] && echo "✅ ENABLED" || echo "⚠️  DISABLED")"
echo ""
echo "Next Steps:"
echo "1. If not done: Get API key from https://aistudio.google.com/app/apikey"
echo "2. Create .env file with: echo 'GEMINI_API_KEY=your_key' > $PROJECT_ROOT/.env"
echo "3. Install google-genai: pip install google-genai"
echo "4. Read $AGENT_DIR/rules/AUTO_DELEGATION_RULES.md for usage instructions"
echo "5. Open Claude Code CLI and start using delegation!"
echo ""
echo "🎯 After setup: Use 'call_gemini_flash()' to delegate file operations"
echo "💰 Expected savings: 99% on file I/O operations"
echo ""
