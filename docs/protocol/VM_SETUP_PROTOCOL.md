# VM Setup Protocol (V12.40)

**Version**: V12.40
**Effective**: 2026-06-17
**Status**: MANDATORY - Read before ANY VM operation

---

## ⚠️ READ THIS FIRST (BLOCKING GATE)

**STOP**: Before reading this protocol, you MUST read:

1. **Primary Skill**: `.bob/skills/gcp-vm-wave-execution/skill.md` (V2.10+)
   - Lines 22-44: "What you need" section
   - Lines 47-120: "Pre-Wave Checklist" section
   - Lines 769-930: "Wave Rollback Procedure" section

2. **Script Generation SOP**: `docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md` (V3.7+)
   - Step -2: "Pre-Wave Validation" section
   - Step 0: "Building-Blocks Method" section

**Verification Checklist** (check ALL before proceeding):
- [ ] I have read `.bob/skills/gcp-vm-wave-execution/skill.md` in full
- [ ] I have read `docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md` Step -2
- [ ] I understand VM does NOT have .NET SDK installed
- [ ] I understand VM ONLY executes Bob CLI (no compilation)
- [ ] I understand Bob CLI location is `/home/malhitticrypto/.npm-global/bin/bob` (npm global, requires login shell)
- [ ] I understand compilation happens locally (Windows machine with .NET 8.0 SDK)

**BLOCKING GATE**: If you have NOT checked ALL boxes above, STOP. Do NOT proceed with ANY VM operation.

---

## Critical VM Facts (AFTER Reading Skill)

### What the VM IS
- **Purpose**: Bob CLI execution environment ONLY
- **Function**: Generates code via Bob Shell API
- **Output**: Markdown files with code suggestions
- **Location**: GCP n2-standard-8 (8 vCPU, 32 GB RAM)

### What the VM IS NOT
- ❌ **NOT a compilation environment** - No .NET SDK installed
- ❌ **NOT a build server** - Cannot run `dotnet build`
- ❌ **NOT a test runner** - Cannot run `dotnet test`
- ❌ **NOT a NinjaTrader instance** - Cannot execute strategies

### Why This Matters
**NEVER run these commands on VM** (they will fail):
```bash
dotnet build          # ❌ FAILS - .NET SDK not installed
dotnet test           # ❌ FAILS - .NET SDK not installed
dotnet run            # ❌ FAILS - .NET SDK not installed
csc                   # ❌ FAILS - C# compiler not installed
msbuild               # ❌ FAILS - MSBuild not installed
```

**Compilation happens HERE** (local machine or NinjaTrader):
- Local: `dotnet build` (Windows machine with .NET 8.0 SDK)
- NinjaTrader: F5 compile (NinjaTrader 8 with hard-linked files)

## VM Configuration

### Instance Details
- **Name**: `v12-test-golden-v2`
- **Zone**: `us-central1-a`
- **Type**: `n2-standard-8` (8 vCPU, 32 GB RAM)
- **Image**: `v12-bob-shell-golden-v2` (golden image)
- **OS**: Ubuntu 22.04 LTS
- **User**: `malhitticrypto`
- **Home**: `/home/malhitticrypto`

### Repository Path
```bash
/home/malhitticrypto/universal-or-strategy
```

### Installed Software

**Bob CLI**:
- **Location**: `/home/malhitticrypto/.npm-global/bin/bob` (npm global installation)
- **Access**: Requires login shell (`bash -l`) - not visible in non-interactive shells
- **Version**: 1.0.4
- **Version Check**: `bob --version`
- **API Keys**: Stored in `docs/API/*.json` (15 keys × 160 bobcoins)

**Git**:
- **Version**: 2.34+
- **Branch**: Always works on `main` (NOT gitbutler/workspace)
- **Remote**: `origin` (GitHub)

**Python**:
- **Version**: 3.10+
- **Purpose**: Helper scripts (query_kb.py, etc.), MCP servers
- **Location**: `/usr/bin/python3`
- **Symlink**: `/usr/bin/python` → `/usr/bin/python3` (REQUIRED for MCP servers)

**Node.js**:
- **Version**: 18+
- **Purpose**: MCP servers (sequential-thinking)
- **Location**: `/usr/bin/node`
- **npm**: `/usr/bin/npm`
- **npx**: `/usr/bin/npx` (REQUIRED for MCP servers)

**Screen**:
- **Version**: 4.9+
- **Purpose**: Background process management
- **Usage**: `screen -dmS <name> bash -l -c '<command>'`

### MCP Server Setup (V12.40)

**Prerequisites** (MANDATORY for Bob CLI MCP tools):

1. **Python Symlink** (REQUIRED):
   ```bash
   sudo ln -sf /usr/bin/python3 /usr/bin/python
   ```
   - **Why**: MCP servers expect `python` command, VM has `python3`
   - **Verify**: `python --version` should show Python 3.10+

2. **Node.js & npm** (REQUIRED):
   - Already installed on golden image v2
   - **Verify**: `node --version && npm --version && npx --version`

**MCP Servers: Local vs VM**:

**OUTDATED - See docs/protocol/VM_MCP_REQUIREMENTS_MATRIX.md for current requirements**

**Local-Only MCP Servers** (run on Windows machine):
- None - all required MCPs can run on Linux VM

**VM-Compatible MCP Servers** (MUST be installed on VM for Wave 7):
- `jcodemunch-mcp` (Linux binary available - see docs/protocol/JCODEMUNCH_VM_INSTALLATION.md)
- `sequential-thinking` (requires Node.js/npx) ✅ INSTALLED
- `graphify` (requires Node.js/npx) ❌ NOT INSTALLED
- `greptile` (requires Node.js/npx + API key) ❌ NOT INSTALLED

**Obsolete Servers** (removed in V12.42):
- `phase-*` servers (replaced by custom modes in .bob/custom_modes.yaml)
- `worker-*` servers (not needed for wave execution)

**Bob CLI Behavior**: When MCP server unavailable, Bob Shell continues with degraded functionality (no MCP tools).

**Verification**:
```bash
# Check Python symlink
python --version && python3 --version
# Both should show Python 3.10+

# Check Node.js tools
node --version && npm --version && npx --version
# All should show version numbers

# Test Bob CLI MCP discovery (should show 0 errors for VM-compatible servers)
bob --mode v12-engineer --api-key-file docs/API/b.json --help 2>&1 | grep -i "error.*discovery"
# Should NOT show errors for phase-* or worker-* servers
# MAY show errors for jcodemunch-mcp.exe (expected - Windows-only)
```

### NOT Installed (Intentionally)

- ❌ .NET SDK (any version)
- ❌ C# compiler (csc)
- ❌ MSBuild
- ❌ Visual Studio
- ❌ NinjaTrader
- ❌ Roslyn analyzers
- ❌ CSharpier
- ❌ jcodemunch-mcp.exe (Windows binary - use local machine)

## Pre-Flight Validation (MANDATORY)

Before ANY wave execution, verify VM setup:

### Step 1: VM Accessibility
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="echo 'VM accessible'"
# Expected: VM accessible
```

### Step 2: Bob CLI Available
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="bob --version"
# Expected: Version output (not "command not found")
```

### Step 3: Repository Exists
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="ls ~/universal-or-strategy/README.md"
# Expected: File path (not "No such file")
```

### Step 4: Git Working
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="cd ~/universal-or-strategy && git status"
# Expected: On branch main (not error)
```

### Step 5: API Keys Present
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="ls ~/universal-or-strategy/docs/API/*.json | wc -l"
# Expected: 15 (number of API key files)
```

**BLOCKING GATE**: If ANY step fails, STOP. Do NOT proceed with wave execution.

## Common Mistakes (AVOID)

### Mistake 1: Trying to Compile on VM
```bash
# ❌ WRONG - This will fail
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="cd ~/universal-or-strategy && dotnet build"
# Error: bash: dotnet: command not found

# ✅ CORRECT - Compile locally
dotnet build
```

### Mistake 2: Wrong Bob CLI Path
```bash
# ❌ WRONG - Bob is not in /usr/local/bin
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="ls /usr/local/bin/bob"
# Error: No such file or directory

# ✅ CORRECT - Bob is aliased in ~/.bashrc
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="bob --version"
```

### Mistake 3: Expecting NinjaTrader on VM
```bash
# ❌ WRONG - NinjaTrader is NOT on VM
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="F5"
# Error: Command not found

# ✅ CORRECT - NinjaTrader is local only
# Press F5 in NinjaTrader on Windows machine
```

### Mistake 4: Using gitbutler/workspace on VM
```bash
# ❌ WRONG - VM always works on main
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="cd ~/universal-or-strategy && git checkout gitbutler/workspace"
# Error: Branch not found (VM doesn't have GitButler)

# ✅ CORRECT - VM uses main branch
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="cd ~/universal-or-strategy && git checkout main"
```

### Mistake 5: Not Reading Skill Documentation (ROOT CAUSE)
```
# ❌ WRONG - Skipping skill reading
"I'll just start the wave execution..."

# ✅ CORRECT - Read skill FIRST
1. Read .bob/skills/gcp-vm-wave-execution/skill.md
2. Read docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md
3. Verify understanding with checklist
4. THEN proceed with wave execution
```

## Workflow: VM vs Local

### VM Responsibilities
1. **Execute Bob CLI** - Generate code via Bob Shell API
2. **Create Files** - Write markdown files with code suggestions
3. **Track Bobcoins** - Monitor API usage
4. **Run Scripts** - Execute phase scripts in screen sessions

### Local Responsibilities
1. **Compile Code** - Run `dotnet build` locally
2. **Run Tests** - Execute `dotnet test` locally
3. **Format Code** - Run `dotnet csharpier` locally
4. **Sync to NinjaTrader** - Run `deploy-sync.ps1` locally
5. **F5 Compile** - Press F5 in NinjaTrader locally

### Handoff Points
1. **VM → Local**: After Phase 5 (Bob generates code)
2. **Local → VM**: After git push (VM pulls latest)
3. **Local → NinjaTrader**: After deploy-sync.ps1 (hard-link sync)

## Troubleshooting

### Issue: "dotnet: command not found"
**Cause**: Trying to compile on VM  
**Solution**: Compile locally, not on VM

### Issue: "bob: command not found"
**Cause**: Bob CLI not in PATH or not installed  
**Solution**: Verify Bob CLI installation: `bob --version`

### Issue: "No such file or directory: /usr/local/bin/bob"
**Cause**: Looking for Bob in wrong location  
**Solution**: Bob is aliased in ~/.bashrc, use `bob` directly

### Issue: "Branch gitbutler/workspace not found"
**Cause**: VM doesn't have GitButler virtual branches  
**Solution**: VM always works on `main` branch

### Issue: "I didn't know VM doesn't have .NET SDK"
**Cause**: Didn't read skill documentation  
**Solution**: Read `.bob/skills/gcp-vm-wave-execution/skill.md` FIRST

## Version History

- **V1.1 (V12.40)** - 2026-06-17: MCP server setup
  - Added Python symlink requirement (`python` → `python3`)
  - Documented Node.js/npm/npx verification
  - Added MCP servers: Local vs VM section
  - Clarified jcodemunch-mcp as Windows-only (local machine)
  - Added MCP server verification commands
  - Updated Python/Node.js descriptions with MCP context

- **V1.0 (V12.39)** - 2026-06-16: Initial VM setup protocol
  - Explicit statement: VM does NOT have .NET SDK
  - Documented Bob CLI location (/home/malhitticrypto/.npm-global/bin/bob, npm global)
  - Added pre-flight validation steps
  - Added common mistakes section
  - Added VM vs Local workflow
  - **Added "READ THIS FIRST" blocking gate**
  - **Added skill reading verification checklist**
  - **Added Mistake 5: Not reading skill documentation**

## References

- **Skill**: `.bob/skills/gcp-vm-wave-execution/skill.md` (V2.10+)
- **Configuration**: `docs/workflow/WAVE_2_CONFIGURATION.md`
- **Git Sync**: `docs/protocol/VM_LOCAL_GIT_SYNC_PROTOCOL.md` (V12.37)
- **Script SOP**: `docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md` (V3.7+)