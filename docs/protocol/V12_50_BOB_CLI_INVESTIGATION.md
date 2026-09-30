# V12.50: Bob CLI Investigation - VM Setup Gap

**Version**: 1.0  
**Date**: 2026-06-17  
**Status**: CRITICAL BLOCKER - Bob CLI not installed on VM

## Executive Summary

Per Director's request to "figure out the mode and mcp issue", I investigated Bob CLI installation on the VM. **Bob CLI does NOT exist on the VM**, despite documentation claiming it's at `~/bob`.

## Investigation Steps

### 1. Documentation Claims
**Skill Document** (`.bob/skills/gcp-vm-wave-execution/skill.md` line 16, 25):
- "Bob CLI location is `~/bob` (aliased)"
- "I understand Bob CLI location is `~/bob` (aliased)"

**VM Setup Protocol** (`docs/protocol/VM_SETUP_PROTOCOL.md` line 81-82):
- "Location: `~/bob` (user home directory, NOT /usr/local/bin)"
- "Alias: `bob` (configured in ~/.bashrc)"

### 2. Actual VM State
```bash
# Test 1: Check if bob command exists
$ which bob
# command not found

# Test 2: Check if ~/bob file exists
$ ls -la /home/malhitticrypto/bob
# ls: cannot access '/home/malhitticrypto/bob': No such file or directory

# Test 3: Check for bob-shell or bobshell
$ which bob-shell bobshell
# No bob commands found

# Test 4: Check ~/.bashrc for bob alias
$ cat ~/.bashrc | grep -i bob
# export BOBSHELL_API_KEY=bob_prod_bob-admin_...
# (Only API key, no alias)

# Test 5: Find any file named 'bob'
$ find ~ -name 'bob' -type f 2>/dev/null
# /home/malhitticrypto/universal-or-strategy/.git/refs/notes/bob
# /home/malhitticrypto/universal-or-strategy/.git/logs/refs/notes/bob
# (Only git refs, not the binary)
```

**Conclusion**: Bob CLI is NOT installed on the VM.

### 3. Wave 4 Script Analysis
All Wave 4 scripts call `bob` command:
- **Phase 0**: `bob --yolo --chat-mode v12-phase0-hotspot "$(cat /tmp/phase0_msg_001.txt)"`
- **Phase 1**: `bob --yolo --chat-mode plan "$(cat /tmp/phase1_msg_001.txt)"`
- **Phase 2**: `bob --yolo --chat-mode plan "$(cat /tmp/phase2_msg_001.txt)"`
- **Phase 3**: `bob --yolo --chat-mode advanced "$(cat /tmp/phase3_msg_001.txt)"`
- **Phase 4**: `bob --yolo --chat-mode plan "$(cat /tmp/phase4_msg_001.txt)"`
- **Phase 5**: `bob --yolo "$(cat /tmp/phase5_msg_001.txt)"` (NO mode flag)

**Impact**: If Bob CLI doesn't exist, ALL these scripts would fail with "command not found".

### 4. Wave 4 Logs
- **VM logs**: Only Wave 5 logs exist (`logs/wave5/`)
- **Wave 4 logs**: Never saved to VM disk
- **Cannot verify**: Whether Wave 4 scripts actually executed or failed

## Critical Questions

### Q1: Was Bob CLI ever installed on VM?
**Unknown**. Documentation says it should be at `~/bob`, but it's not there now.

**Possibilities**:
1. Bob CLI was never installed (documentation is aspirational)
2. Bob CLI was installed but removed (when? why?)
3. Bob CLI is installed elsewhere (where?)

### Q2: How did Wave 4 scripts execute?
**Unknown**. No Wave 4 logs exist to verify.

**Possibilities**:
1. Wave 4 scripts never executed (failed silently)
2. Wave 4 executed locally, not on VM
3. Bob CLI was temporarily installed for Wave 4, then removed

### Q3: What is the golden image `v12-bob-shell-golden-v2`?
**Unknown**. Documentation references this image, but unclear if:
1. Image includes Bob CLI pre-installed
2. Image requires Bob CLI to be installed manually
3. Image is outdated and doesn't match current VM state

### Q4: Where should Bob CLI come from?
**Unknown**. Documentation doesn't specify:
1. Download URL for Bob CLI binary
2. Installation instructions
3. Version requirements

## Mode and MCP Investigation (Director's Request)

### Cannot Test Without Bob CLI
The Director asked to "figure out the mode and mcp issue" and "validate that we can actually use custom modes and mcp custom modes on bob cli on the vm".

**BLOCKER**: Cannot test modes or MCP without Bob CLI installed.

**What I Cannot Test**:
- ❌ Custom modes (e.g., `--chat-mode v12-engineer`)
- ❌ MCP functionality (e.g., phase-5-execute MCP server)
- ❌ Mode enforcement (whether Bob respects mode flags)
- ❌ Mode reporting (whether Bob reports which mode it's using)

### What I CAN Verify
✅ **MCP Server Dependencies** (V12.40):
- Python 3: Installed (`/usr/bin/python3`)
- Python symlink: EXISTS (`/usr/bin/python` → `/usr/bin/python3`)
- Node.js: Installed (v22.22.3)
- npm/npx: Installed

✅ **MCP Configuration Files**:
- `.bob/mcp.json`: Exists locally (needs upload to VM)
- `.bob/custom_modes.yaml`: Exists locally (needs upload to VM)

## Wave 4 Script Mode Flags

### Phases 0-4: Had Mode Flags
```bash
# Phase 0
bob --yolo --chat-mode v12-phase0-hotspot "$(cat /tmp/phase0_msg_001.txt)"

# Phase 1
bob --yolo --chat-mode plan "$(cat /tmp/phase1_msg_001.txt)"

# Phase 2
bob --yolo --chat-mode plan "$(cat /tmp/phase2_msg_001.txt)"

# Phase 3
bob --yolo --chat-mode advanced "$(cat /tmp/phase3_msg_001.txt)"

# Phase 4
bob --yolo --chat-mode plan "$(cat /tmp/phase4_msg_001.txt)"
```

### Phase 5: NO Mode Flag
```bash
# Phase 5 (MISSING MODE FLAG)
bob --yolo "$(cat /tmp/phase5_msg_001.txt)"
```

**Finding**: Wave 4 Phase 5 scripts did NOT specify mode, but this is irrelevant if Bob CLI doesn't exist.

## My False Claims (V12.43)

In V12.43, I claimed:
> "Bob CLI to default to **code mode** when MCP failed"

**This was speculation without evidence**. I had:
- ❌ No Wave 4 logs to verify
- ❌ No Bob CLI to test
- ❌ No actual mode reporting from Bob

**Correction**: I cannot determine what mode Bob used (or if Bob even executed) without logs or a working Bob CLI installation.

## Recommendations

### Immediate Actions Required

1. **Install Bob CLI on VM**:
   - Obtain Bob CLI binary (download URL?)
   - Install to `/home/malhitticrypto/bob`
   - Add alias to `~/.bashrc`: `alias bob='/home/malhitticrypto/bob'`
   - Verify: `bob --version`

2. **Upload MCP Configuration**:
   ```bash
   gcloud compute scp .bob/mcp.json v12-test-golden-v2:/home/malhitticrypto/universal-or-strategy/.bob/ --zone=us-central1-a
   gcloud compute scp .bob/custom_modes.yaml v12-test-golden-v2:/home/malhitticrypto/universal-or-strategy/.bob/ --zone=us-central1-a
   ```

3. **Test Bob CLI**:
   ```bash
   # Test basic invocation
   gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="cd ~/universal-or-strategy && bob --version"
   
   # Test custom mode
   gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="cd ~/universal-or-strategy && bob --chat-mode v12-engineer --help"
   
   # Test MCP
   gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="cd ~/universal-or-strategy && bob mcp list-servers"
   ```

4. **Verify Mode Reporting**:
   - Create test script that invokes Bob with mode flag
   - Check if Bob reports "Currently in 'X' mode" in output
   - Document actual behavior

### Questions for Director

1. **Where is Bob CLI binary?**
   - Download URL?
   - Installation instructions?
   - Version to use?

2. **Is golden image outdated?**
   - Should `v12-bob-shell-golden-v2` include Bob CLI?
   - Does current VM match golden image?
   - Should we rebuild golden image?

3. **Did Wave 4 actually execute?**
   - On VM or locally?
   - With or without Bob CLI?
   - Where are the logs?

4. **How to proceed?**
   - Install Bob CLI and retry Wave 5?
   - Execute locally instead of on VM?
   - Use different approach entirely?

## Status

**BLOCKED**: Cannot proceed with Wave 5 or any VM-based execution until Bob CLI is installed and verified.

**Next Step**: Director must provide Bob CLI installation instructions or clarify VM setup expectations.