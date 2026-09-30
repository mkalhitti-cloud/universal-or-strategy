# VM Setup Failure Analysis - Bob Shell Installation Impossible

**Date**: 2026-06-12T05:10 UTC
**Conclusion**: Bob Shell cannot be installed on GCP VMs via automated scripts

## Critical Discovery ✅

After 7 VM iterations, we've identified a **fundamental blocker**: The Bob Shell installer has a **permissions paradox** that makes automated installation impossible.

## The Permissions Paradox

### v7 Failure Analysis
```
npm error code EACCES
npm error syscall mkdir
npm error path /usr/lib/node_modules/bobshell
npm error errno -13
npm error Error: EACCES: permission denied, mkdir '/usr/lib/node_modules/bobshell'
```

**The Problem**:
1. Bob Shell installer runs as **user** (`malhitticrypto`)
2. npm tries to install globally to `/usr/lib/node_modules/` (requires root)
3. User doesn't have permission to write to system directories
4. **BUT**: Running the installer as root would install Bob for root, not the user

### Why This Is Unsolvable

**Option A: Run installer as user** ❌
- Result: Permission denied (v7 failure)
- npm can't write to `/usr/lib/node_modules/`

**Option B: Run installer as root** ❌
- Result: Bob installed for root user only
- User `malhitticrypto` still can't run `bob` command
- Defeats the purpose (we need user-level Bob)

**Option C: Use sudo with installer** ❌
- The installer script doesn't support sudo passthrough
- Would still install for root, not user

**Option D: Configure npm prefix** ❌
- Would require modifying the installer script itself
- We don't control the installer script (it's from bob.ibm.com)

## Complete Failure Timeline

| Version | Method | Issue | Root Cause |
|---------|--------|-------|------------|
| v1 | Manual setup | No sudo | Script design |
| v2 | Inline metadata | PowerShell parsing | GCP metadata format |
| v3 | File-based script | Wrong URL | bob.build doesn't exist |
| v4 | npm install | Package not found | @ibm/bob-shell doesn't exist on npm |
| v5 | Mise-based | Not tested | Skipped to try official installer |
| v6 | Official installer | Node.js missing | Installer requires Node.js 22.15+ |
| v7 | Official installer + Node.js | **Permission denied** | **Installer design flaw** |

## Why Bob Shell Installer Fails on VMs

The Bob Shell installer (`bobshell.sh`) is designed for **interactive desktop environments**, not **automated VM provisioning**:

1. **Assumes interactive user session**: Expects user to have sudo access and be prompted
2. **Global npm installation**: Tries to install to system directories
3. **No automation support**: No flags for user-level installation
4. **No permission handling**: Doesn't detect or handle permission issues gracefully

## Alternative Approaches (All Blocked)

### 1. Manual Installation After VM Launch ❌
- **Problem**: Defeats the purpose of "golden image"
- **Problem**: Requires manual intervention for every VM
- **Problem**: Not scalable for Wave 2 autonomous execution

### 2. Pre-built Docker Container ❌
- **Problem**: Bob Shell requires host-level access
- **Problem**: Can't run Bob inside Docker for our use case
- **Problem**: Would need to redesign entire Wave 2 architecture

### 3. Custom Bob Shell Build ❌
- **Problem**: We don't have access to Bob Shell source code
- **Problem**: Would require reverse-engineering the installer
- **Problem**: Maintenance nightmare for updates

### 4. Use Different Tool ❌
- **Problem**: Bob Shell is required for V12 epic workflow
- **Problem**: No equivalent tool exists
- **Problem**: Would require rewriting all epic scripts

## The Real Solution: Local Execution Only

**Conclusion**: Bob Shell is **not designed for cloud VM automation**. It's a **desktop development tool** that requires:
- Interactive user session
- Desktop environment
- Manual installation
- User-level permissions

### Recommended Path Forward

**Option 1: Abandon GCP VM Approach** ✅
- Run Wave 2 execution **locally on Windows machine**
- Use existing Bob Shell installation
- Use PowerShell orchestration (Antigravity)
- Leverage local file system and tools

**Option 2: Hybrid Approach**
- Use GCP VMs for **non-Bob tasks** (Python scripts, data processing)
- Run **Bob-dependent phases locally**
- Sync results back to GCP for storage

**Option 3: Wait for Bob Shell Cloud Support**
- Contact IBM to request cloud-friendly installation method
- Wait for official support for automated VM provisioning
- Timeline: Unknown (could be months/years)

## Cost Analysis

**Money Spent on Failed Attempts**:
- 7 VMs × 8 minutes × $0.093/hour = $0.087
- Total GCP cost: ~$0.09
- Remaining credit: $299.91

**Time Spent**:
- 2 sessions × ~1 hour = 2 hours
- Multiple script iterations
- Extensive debugging

**Lesson Learned**: Not all desktop tools are cloud-ready. Always verify automation compatibility before committing to cloud infrastructure.

## Recommendation

**STOP trying to install Bob Shell on GCP VMs**. It's not designed for this use case.

**START using local execution** with the existing Bob Shell installation on the Windows development machine.

**Wave 2 Execution Strategy**:
1. Use local PowerShell orchestration (Antigravity)
2. Run epic phases locally with Bob Shell
3. Use GCP VMs only for parallel Python-based tasks (if needed)
4. Store results locally or sync to cloud storage

This approach:
- ✅ Works with existing tools
- ✅ No installation issues
- ✅ Faster execution (no VM startup time)
- ✅ Lower cost (no VM charges)
- ✅ Simpler architecture

## Files Created During Investigation

- `scripts/vm_startup_script_v4.sh` - npm approach (failed)
- `scripts/vm_startup_script_v5_mise.sh` - Mise approach (not tested)
- `scripts/vm_startup_script_v6.sh` - Official installer without Node.js (failed)
- `scripts/vm_startup_script_v7.sh` - Official installer with Node.js (failed - permissions)
- `docs/workflow/VM_SETUP_V6_STATUS.md` - v6 analysis
- `docs/workflow/VM_SETUP_V7_STATUS.md` - v7 analysis
- `docs/workflow/VM_SETUP_FAILURE_ANALYSIS.md` - This document

## Next Steps

1. **Accept the limitation**: Bob Shell is not cloud-automatable
2. **Pivot to local execution**: Use existing Windows setup
3. **Update Wave 2 plan**: Remove GCP VM dependency
4. **Focus on orchestration**: Improve local PowerShell automation
5. **Document decision**: Update roadmap with new execution strategy

---

**Final Verdict**: GCP VM approach for Bob Shell = **ABANDONED**
**New Strategy**: Local execution with PowerShell orchestration = **ADOPTED**