# .mcp.json.vm Update Instructions

**Version**: 1.0
**Date**: 2026-06-20
**Status**: MANUAL UPDATE REQUIRED

## Problem

The current `.mcp.json.vm` file only has Sequential Thinking MCP installed. Wave 7 requires 4 MCPs total, and 9 out of 10 phases are currently BLOCKED.

## Current Configuration

**File**: `.mcp.json.vm`

```json
{
  "mcpServers": {
    "sequential-thinking": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-sequential-thinking"]
    }
  },
  "_comment": "VM-specific MCP configuration (V12.42 - Cleanup 2026-06-18). Phase-specific MCPs removed (replaced by custom modes in .bob/custom_modes.yaml). Essential MCPs: sequential-thinking (used by all phases). Excluded: jcodemunch-mcp.exe (Windows-only), greptile (auth required), worker-* (not needed for wave execution)."
}
```

## Required Configuration

**REPLACE** the entire `.mcp.json.vm` file with this configuration:

```json
{
  "mcpServers": {
    "sequential-thinking": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-sequential-thinking"]
    },
    "jcodemunch-mcp": {
      "type": "stdio",
      "command": "/usr/local/bin/jcodemunch-mcp",
      "args": [],
      "env": {
        "JCODEMUNCH_USE_AI_SUMMARIES": "false"
      }
    },
    "graphify": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-graphify"]
    },
    "greptile": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@greptile/mcp-server"],
      "env": {
        "GREPTILE_API_KEY": "${GREPTILE_API_KEY}"
      }
    }
  },
  "_comment": "VM-specific MCP configuration for Wave 7 autonomous execution (V12.43 - 2026-06-20). All 4 MCPs are MANDATORY for 9/10 phases. Sequential Thinking: all phases (100%). jCodemunch: 8/10 phases (80%). Graphify: Phase 2 only (10%). Greptile: Phases 3, 5.V, 6 (30%). See docs/protocol/VM_MCP_REQUIREMENTS_MATRIX.md for complete phase × MCP matrix."
}
```

## Installation Prerequisites

Before updating `.mcp.json.vm`, you MUST install the missing MCPs:

### 1. jCodemunch MCP (CRITICAL - Priority 1)

**Status**: ❌ NOT INSTALLED
**Impact**: Blocks 8 out of 10 phases

**Installation**:
```bash
# Follow the complete guide:
# docs/protocol/JCODEMUNCH_VM_INSTALLATION.md

# Quick steps:
# 1. Download Linux binary from GitHub releases
# 2. Install to /usr/local/bin/jcodemunch-mcp
# 3. Make executable: chmod +x /usr/local/bin/jcodemunch-mcp
# 4. Test: jcodemunch-mcp --version
```

**Configuration Notes**:
- Path in `.mcp.json.vm` must match installation location
- If installed to `~/.local/bin/jcodemunch-mcp`, update the `command` field accordingly
- `JCODEMUNCH_USE_AI_SUMMARIES=false` disables AI summaries (reduces cost)

### 2. Graphify MCP (HIGH - Priority 2)

**Status**: ❌ NOT INSTALLED
**Impact**: Blocks Phase 2 (Architecture Planning)

**Installation**:
```bash
# Option 1: npm (recommended)
npm install -g @modelcontextprotocol/server-graphify

# Option 2: npx (no installation, slower)
# Already configured in .mcp.json.vm to use npx

# Test:
npx -y @modelcontextprotocol/server-graphify --version
```

**Configuration Notes**:
- Using `npx -y` means no installation required
- First run will be slower (downloads package)
- Subsequent runs use npm cache

### 3. Greptile MCP (MEDIUM - Priority 3)

**Status**: ❌ NOT INSTALLED
**Impact**: Blocks Phases 3, 5.V, 6 (DNA Audit, Verification, Final Review)

**Installation**:
```bash
# 1. Obtain Greptile API key from https://app.greptile.com

# 2. Set environment variable (add to ~/.bashrc or ~/.profile)
export GREPTILE_API_KEY="your-api-key-here"

# 3. Install via npm
npm install -g @greptile/mcp-server

# 4. Test
npx -y @greptile/mcp-server --version
```

**Configuration Notes**:
- Requires valid API key
- API key must be set in environment before Bob CLI starts
- Using `npx -y` means no installation required (but API key still needed)

## Update Procedure

### Step 1: Install Missing MCPs
```bash
# SSH to VM
ssh user@vm-hostname

# Install jCodemunch (follow full guide)
# See: docs/protocol/JCODEMUNCH_VM_INSTALLATION.md

# Verify Graphify works (no installation needed with npx)
npx -y @modelcontextprotocol/server-graphify --version

# Set Greptile API key
export GREPTILE_API_KEY="your-key"
echo 'export GREPTILE_API_KEY="your-key"' >> ~/.bashrc

# Verify Greptile works
npx -y @greptile/mcp-server --version
```

### Step 2: Update .mcp.json.vm
```bash
# Backup current file
cp .mcp.json.vm .mcp.json.vm.backup

# Edit file (use nano, vim, or your preferred editor)
nano .mcp.json.vm

# Paste the "Required Configuration" from above
# Save and exit

# Verify JSON syntax
cat .mcp.json.vm | jq .
```

### Step 3: Verify Configuration
```bash
# Test Bob CLI with new MCP configuration
bob --version

# Test Phase 4.5 (only needs Sequential Thinking)
# This should work even before installing other MCPs
bob --yolo --chat-mode v12-phase4-5-review "Test message"

# After installing jCodemunch, test Phase 0
bob --yolo --chat-mode v12-phase0-hotspot "Test message"

# After installing Graphify, test Phase 2
bob --yolo --chat-mode v12-phase2-architecture "Test message"

# After installing Greptile, test Phase 3
bob --yolo --chat-mode v12-phase3-audit "Test message"
```

## Verification Checklist

- [ ] jCodemunch MCP installed and tested
- [ ] Graphify MCP tested (npx works)
- [ ] Greptile API key set in environment
- [ ] Greptile MCP tested (npx works)
- [ ] `.mcp.json.vm` updated with all 4 MCPs
- [ ] JSON syntax validated with `jq`
- [ ] Bob CLI starts without errors
- [ ] Phase 4.5 test successful (Sequential Thinking only)
- [ ] Phase 0 test successful (jCodemunch required)
- [ ] Phase 2 test successful (Graphify required)
- [ ] Phase 3 test successful (Greptile required)

## Troubleshooting

### jCodemunch Not Found
```bash
# Check installation
which jcodemunch-mcp
ls -la /usr/local/bin/jcodemunch-mcp

# If not found, reinstall
# Follow: docs/protocol/JCODEMUNCH_VM_INSTALLATION.md

# Update .mcp.json.vm with correct path
```

### Graphify Fails
```bash
# Test npx directly
npx -y @modelcontextprotocol/server-graphify --version

# Check npm cache
npm cache clean --force

# Retry
```

### Greptile Authentication Error
```bash
# Verify API key is set
echo $GREPTILE_API_KEY

# If empty, set it
export GREPTILE_API_KEY="your-key"

# Add to shell profile for persistence
echo 'export GREPTILE_API_KEY="your-key"' >> ~/.bashrc
source ~/.bashrc
```

### Bob CLI Fails to Start
```bash
# Check MCP configuration syntax
cat .mcp.json.vm | jq .

# If syntax error, restore backup
cp .mcp.json.vm.backup .mcp.json.vm

# Fix syntax and retry
```

## Impact of Update

### Before Update
- ✅ 1 phase ready (Phase 4.5 only)
- ❌ 9 phases blocked (missing MCPs)
- ❌ Wave 7 execution COMPLETELY BLOCKED

### After Update
- ✅ 10 phases ready (all phases)
- ✅ Wave 7 execution UNBLOCKED
- ✅ Full autonomous refactoring capability restored

## References

- MCP Requirements Matrix: [`docs/protocol/VM_MCP_REQUIREMENTS_MATRIX.md`](VM_MCP_REQUIREMENTS_MATRIX.md)
- jCodemunch Installation: [`docs/protocol/JCODEMUNCH_VM_INSTALLATION.md`](JCODEMUNCH_VM_INSTALLATION.md)
- Custom Modes: [`.bob/custom_modes.yaml`](../../.bob/custom_modes.yaml)
- Wave 7 Setup: [`docs/brain/WAVE7_SETUP_COMPLETE.md`](../brain/WAVE7_SETUP_COMPLETE.md)

## Version History

- **1.0** (2026-06-20): Initial update instructions created