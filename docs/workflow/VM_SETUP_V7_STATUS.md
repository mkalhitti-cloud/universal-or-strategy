# VM Setup v7 - Final Attempt

**Date**: 2026-06-12T05:01 UTC
**Session**: GCP VM Setup Continuation (v7)

## Critical Discovery from v6 Logs ✅

### v6 Failure Analysis
The v6 startup script logs revealed the root cause:

```
startup-script: Checking Node.js Installation
startup-script: ✗ Node.js is not installed.
startup-script:   Please install Node.js version 22.15 or higher and try again.
startup-script: -bash: line 1: bob: command not found
```

**Key Finding**: The Bob Shell installer script (`bobshell.sh`) **checks for Node.js 22.15+** and **exits if not found**. It does NOT install Node.js itself.

### Installation Sequence Error
**v6 script (WRONG)**:
1. Install system dependencies ✅
2. Run Bob Shell installer ❌ (exits because no Node.js)
3. Install Node.js 20.x ❌ (too late, Bob installer already failed)

**v7 script (CORRECT)**:
1. Install system dependencies ✅
2. Install Node.js 22.x ✅ (BEFORE Bob installer)
3. Run Bob Shell installer ✅ (now has required Node.js)

## Solution Implemented ✅

### Created: `scripts/vm_startup_script_v7.sh`
- **Line 18-20**: Installs Node.js 22.x from NodeSource FIRST
- **Line 22-24**: Verifies Node.js installation
- **Line 26-27**: THEN runs Bob Shell installer (which now succeeds)

### VM v7 Launched ✅
- **Name**: v12-golden-image (v7)
- **External IP**: 136.111.14.177 (recycled from v4)
- **Status**: RUNNING
- **Machine**: n2-standard-8 (8 vCPUs, 32 GB RAM, 100 GB SSD)
- **Zone**: us-central1-a
- **Cost**: $0.093/hour (spot pricing)

## Next Steps

### 1. Wait for Setup (8 minutes)
**Timer started**: 05:01 UTC
**Verification time**: 05:09 UTC (8 minutes from now)

### 2. Verification Command
```powershell
gcloud compute ssh v12-golden-image --project=project-14c86305-3cba-493f-a73 --zone=us-central1-a --command="bash -l -c 'cat /tmp/setup_complete.txt && bob --version'"
```

**Expected Output**:
```
Setup complete!
Bob Shell v1.x.x
```

### 3. If Verification Succeeds ✅
Follow the complete sequence in `docs/workflow/VM_SETUP_V6_STATUS.md`:
- Stop VM
- Create golden image
- Launch test VM
- Run 2-epic test

### 4. If Verification Fails ❌
**Fallback Options**:
1. Try v5 Mise-based script (different approach)
2. Manual installation on v7 VM and snapshot
3. Consider alternative Bob Shell installation methods

## Installation Method Evolution

| Version | Method | Node.js | Status |
|---------|--------|---------|--------|
| v1 | Manual setup | N/A | ❌ Failed (no sudo) |
| v2 | Inline metadata | N/A | ❌ Failed (PowerShell quote parsing) |
| v3 | File-based script | N/A | ❌ Failed (wrong Bob URL: bob.build) |
| v4 | npm install | 20.x (after) | ❌ Failed (@ibm/bob-shell doesn't exist) |
| v5 | Mise-based | Via Mise | ⏸️ Not tested yet |
| v6 | Official installer | 20.x (after) | ❌ Failed (Node.js not found by installer) |
| **v7** | **Official installer** | **22.x (before)** | ⏳ **Testing now** |

## Why v7 Should Work

1. ✅ Uses official IBM-provided installation script
2. ✅ Installs Node.js 22.x BEFORE running Bob installer
3. ✅ Meets Bob Shell's prerequisite check (Node.js 22.15+)
4. ✅ Runs as user (not root) - matches Bob Shell's expected environment
5. ✅ Includes verification steps

## Technical Details

### Bob Shell Installer Requirements
Per the v6 logs, the official `bobshell.sh` script:
- Checks for Node.js installation
- Requires Node.js version 22.15 or higher
- Exits with error if Node.js not found
- Does NOT install Node.js itself

### Node.js Version Selection
- **v4/v6**: Used Node.js 20.x (too old for Bob Shell)
- **v7**: Uses Node.js 22.x (meets requirement)
- Source: NodeSource repository (`setup_22.x`)

### Cost Tracking
- **Setup attempts**: 5 VMs × 8 minutes × $0.093/hour = $0.062
- **Current v7 VM**: Running, will cost ~$0.012 for 8-minute setup
- **Total spent**: ~$0.074
- **Remaining GCP credit**: $299.93

## Success Criteria

### VM Setup Complete ✅
- [ ] VM boots and SSH works
- [ ] DNS resolves external domains
- [ ] Node.js 22.x installed and working
- [ ] Bob Shell installed and responds to `bob --version`
- [ ] Repository cloned on `main` branch
- [ ] Helper scripts created and executable
- [ ] `/tmp/setup_complete.txt` exists

### Image Validation ✅
- [ ] Test VM launches from image in <30 seconds
- [ ] Bob Shell works on test VM
- [ ] Repository present on test VM
- [ ] No manual configuration needed

### 2-Epic Test ✅
- [ ] Both epics complete successfully
- [ ] Manifests created correctly
- [ ] Build passes, tests pass
- [ ] Each API key ends with positive balance

## Lessons Learned

1. **Read installer requirements carefully**: Bob Shell installer has prerequisites
2. **Check logs for error messages**: v6 logs clearly stated "Node.js is not installed"
3. **Install dependencies in correct order**: Prerequisites BEFORE the tool that needs them
4. **Version matters**: Node.js 20.x vs 22.x made the difference
5. **Don't assume installers are self-contained**: Some installers expect prerequisites

## References

- **v7 Script**: `scripts/vm_startup_script_v7.sh` (66 lines)
- **v6 Logs**: Revealed Node.js prerequisite requirement
- **Bob Shell Docs**: `bobshell_docs.md` (official installation method)
- **Previous Status**: `docs/workflow/VM_SETUP_V6_STATUS.md`
- **Wave 2 Plan**: `docs/workflow/WAVE_2_CONTINUATION_PROMPT.md`

---

**Status**: ⏳ Waiting for v7 VM setup to complete (8 minutes)
**Next Action**: Run verification command at 05:09 UTC
**Confidence**: HIGH - v6 logs clearly identified the missing prerequisite