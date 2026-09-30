# jCodemunch VM Installation Guide

**Version**: 1.0
**Date**: 2026-06-20
**Status**: REQUIRED for Wave 7 execution

## Why This Is Needed

**8 out of 10 phases require jCodemunch MCP**:
- Phase 0: Hotspot analysis (`get_hotspots`, `get_symbol_complexity`)
- Phase 1: Scope definition (`get_file_outline`, `find_references`)
- Phase 1.5: Boundary validation (`get_symbol_source`, `get_blast_radius`)
- Phase 2: Architecture planning (`get_context_bundle`, `get_call_hierarchy`)
- Phase 3: DNA audit (`search_ast`, `get_layer_violations`)
- Phase 4: Ticket generation (`get_symbol_complexity`, `get_extraction_candidates`)
- Phase 5: Ticket execution (`get_symbol_source`, `plan_refactoring`)
- Phase 5.V: Verification (`get_symbol_complexity`, `get_changed_symbols`)
- Phase 6: Final review (`get_repo_health`, `get_hotspots`)

**Without jCodemunch**: VM can only run Phase 4.5 (Jane Street validation)

## Installation Steps

### 1. Check Current Status

```bash
# SSH into VM
ssh malhitticrypto@34.60.155.195

# Check if jCodemunch is installed
which jcodemunch-mcp
# Expected: /usr/local/bin/jcodemunch-mcp or similar

# Check if it's in PATH
jcodemunch-mcp --version
# Expected: version number or error if not installed
```

### 2. Install jCodemunch (Linux Binary)

**Option A: Download Pre-built Binary** (RECOMMENDED)

```bash
# Download latest Linux release
curl -L https://github.com/jcodemunch/jcodemunch-mcp/releases/latest/download/jcodemunch-mcp-linux-x64 -o jcodemunch-mcp

# Make executable
chmod +x jcodemunch-mcp

# Move to PATH
sudo mv jcodemunch-mcp /usr/local/bin/

# Verify installation
jcodemunch-mcp --version
```

**Option B: Build from Source** (if pre-built not available)

```bash
# Install Rust (if not already installed)
curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh
source $HOME/.cargo/env

# Clone repository
git clone https://github.com/jcodemunch/jcodemunch-mcp.git
cd jcodemunch-mcp

# Build release binary
cargo build --release

# Install binary
sudo cp target/release/jcodemunch-mcp /usr/local/bin/

# Verify installation
jcodemunch-mcp --version
```

**Option C: Install via npm** (if available)

```bash
# Install globally via npm
npm install -g @jcodemunch/mcp-server

# Verify installation
jcodemunch-mcp --version
```

### 3. Update VM MCP Configuration

```bash
# Edit .mcp.json.vm
nano ~/universal-or-strategy/.mcp.json.vm
```

**Add jCodemunch to configuration**:

```json
{
  "mcpServers": {
    "jcodemunch-mcp": {
      "type": "stdio",
      "command": "jcodemunch-mcp",
      "args": []
    },
    "sequential-thinking": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-sequential-thinking"]
    }
  }
}
```

### 4. Index the Repository

```bash
# Navigate to repo
cd ~/universal-or-strategy

# Index the repository (one-time setup)
jcodemunch-mcp index-folder --path . --use-ai-summaries false

# Verify index created
ls -la ~/.jcodemunch/
# Expected: index files for universal-or-strategy
```

### 5. Test jCodemunch MCP

```bash
# Test basic query
echo '{"method": "list_repos"}' | jcodemunch-mcp

# Expected output: JSON with repository list including universal-or-strategy
```

### 6. Verify Bob Shell Integration

```bash
# Test Bob Shell can access jCodemunch
bob --yolo --chat-mode ask "Use jCodemunch to list repos"

# Expected: Bob should successfully call jCodemunch MCP and return repo list
```

## Configuration Files to Update

### 1. `.mcp.json.vm` (VM-specific)

**Before**:
```json
{
  "mcpServers": {
    "sequential-thinking": {...}
  }
}
```

**After**:
```json
{
  "mcpServers": {
    "jcodemunch-mcp": {
      "type": "stdio",
      "command": "jcodemunch-mcp",
      "args": []
    },
    "sequential-thinking": {...}
  }
}
```

### 2. Update Documentation

**Files to update**:
- `docs/protocol/ANTHROPIC_LAUNCH_YOUR_AGENT_INTEGRATION.md` (line 19, 269)
- `docs/protocol/CONTEXT_OPTIMIZATION_SUMMARY.md` (if mentions VM MCPs)
- `docs/protocol/WAVE7_CONTEXT_OPTIMIZATION_FINAL_REPORT.md` (if mentions VM MCPs)

**Change**:
- FROM: "VM: sequential-thinking only"
- TO: "VM: jcodemunch-mcp + sequential-thinking"

## Verification Checklist

After installation, verify:

- [ ] `jcodemunch-mcp --version` returns version number
- [ ] `which jcodemunch-mcp` returns path
- [ ] `.mcp.json.vm` includes jcodemunch-mcp configuration
- [ ] Repository indexed: `~/.jcodemunch/` contains index files
- [ ] Bob Shell can access jCodemunch: `bob --yolo --chat-mode ask "list repos"`
- [ ] Test Phase 0 script runs successfully (uses `get_hotspots`)

## Troubleshooting

### Issue: "jcodemunch-mcp: command not found"

**Solution**: Binary not in PATH
```bash
# Find binary location
find ~ -name jcodemunch-mcp 2>/dev/null

# Add to PATH in ~/.bashrc
echo 'export PATH="$PATH:/path/to/jcodemunch"' >> ~/.bashrc
source ~/.bashrc
```

### Issue: "Index not found"

**Solution**: Repository not indexed
```bash
cd ~/universal-or-strategy
jcodemunch-mcp index-folder --path . --use-ai-summaries false
```

### Issue: "Permission denied"

**Solution**: Binary not executable
```bash
chmod +x /usr/local/bin/jcodemunch-mcp
```

### Issue: Bob Shell can't access jCodemunch

**Solution**: MCP configuration not loaded
```bash
# Verify .mcp.json.vm is in repo root
ls -la ~/universal-or-strategy/.mcp.json.vm

# Restart Bob Shell session
# (exit and reconnect SSH)
```

## Cost Impact

**Before** (sequential-thinking only):
- 1 MCP loaded
- ~2-3k tokens overhead
- Only Phase 4.5 can run on VM

**After** (jcodemunch + sequential-thinking):
- 2 MCPs loaded
- ~5-6k tokens overhead
- All 10 phases can run on VM
- **Net savings**: ~$1,200 across Wave 7 (161 epics)
  - Reason: Avoid local execution overhead, enable full VM automation

## Success Criteria

✅ **Installation Complete When**:
1. `jcodemunch-mcp --version` works
2. Repository indexed successfully
3. `.mcp.json.vm` updated with jcodemunch-mcp
4. Bob Shell can query jCodemunch
5. Phase 0 test script completes successfully

## Next Steps After Installation

1. Update `.mcp.json.vm` with jcodemunch-mcp
2. Commit and push changes to git
3. Sync to VM: `git pull origin main`
4. Run Phase 0 pilot test (3 epics)
5. Verify all phases can execute on VM
6. Proceed with Wave 7 full execution

## References

- jCodemunch GitHub: https://github.com/jcodemunch/jcodemunch-mcp
- MCP Protocol: https://modelcontextprotocol.io/
- Wave 7 Setup: `docs/brain/WAVE7_SETUP_COMPLETE.md`
- Custom Modes: `.bob/custom_modes.yaml` (shows jCodemunch requirements per phase)