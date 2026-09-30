# VM Setup v6 - Status Report

**Date**: 2026-06-12T04:52 UTC
**Session**: GCP VM Setup Continuation

## Root Cause Analysis ✅

### Problem Identified
The v4 startup script attempted to install Bob Shell via npm:
```bash
npm install -g @ibm/bob-shell
```

**Error from logs**:
```
npm error 404 Not Found - GET https://registry.npmjs.org/@ibm%2fbob-shell - Not found
npm error 404  '@ibm/bob-shell@*' is not in this registry.
```

### Key Finding
**`@ibm/bob-shell` does NOT exist on npm registry**. This was an incorrect assumption.

### Correct Installation Method
Per `bobshell_docs.md` line 1789, the official installation method is:
```bash
curl -fsSL https://bob.ibm.com/download/bobshell.sh | bash
```

## Solution Implemented ✅

### Created: `scripts/vm_startup_script_v6.sh`
- **Line 22**: Uses official Bob Shell installation script
- **Method**: `curl -fsSL https://bob.ibm.com/download/bobshell.sh | bash`
- **Execution**: Runs as user `malhitticrypto` (not root)

### VM v6 Launched ✅
- **Name**: v12-golden-image (v6)
- **External IP**: 162.222.180.242 (new IP)
- **Status**: RUNNING
- **Machine**: n2-standard-8 (8 vCPUs, 32 GB RAM, 100 GB SSD)
- **Zone**: us-central1-a
- **Cost**: $0.093/hour (spot pricing)

## Next Steps

### 1. Wait for Setup (8 minutes)
The startup script needs time to:
- Install system dependencies (30s)
- Download and run Bob Shell installer (~2-3 minutes)
- Clone repository (~1 minute)
- Install Python dependencies (~30s)
- Create helper scripts (~10s)

**Timer started**: 04:52 UTC
**Verification time**: 05:00 UTC (8 minutes from now)

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
Execute this sequence:

**A. Stop VM**:
```powershell
gcloud compute instances stop v12-golden-image --project=project-14c86305-3cba-493f-a73 --zone=us-central1-a
```

**B. Create Golden Image**:
```powershell
gcloud compute images create v12-bob-shell-golden-v1 --project=project-14c86305-3cba-493f-a73 --source-disk=v12-golden-image --source-disk-zone=us-central1-a --family=v12-bob-shell --description="Golden image with Bob Shell (official installer), Node.js, Python, and repository"
```

**C. Launch Test VM**:
```powershell
gcloud compute instances create v12-test-vm --project=project-14c86305-3cba-493f-a73 --zone=us-central1-a --machine-type=n2-standard-8 --image=v12-bob-shell-golden-v1 --boot-disk-size=100GB --maintenance-policy=TERMINATE --provisioning-model=SPOT --scopes=cloud-platform
```

**D. Verify Test VM**:
```powershell
gcloud compute ssh v12-test-vm --project=project-14c86305-3cba-493f-a73 --zone=us-central1-a --command="bash -l -c 'bob --version && ls -la ~/universal-or-strategy'"
```

**E. Run 2-Epic Test**:
- Copy `test_config_2_epics.json` to test VM
- Start execution: `~/run_epic_wave.sh test_config_2_epics.json`
- Monitor: `~/check_status.sh`

### 4. If Verification Fails ❌
- Check logs: `gcloud compute ssh v12-golden-image --command="cat /var/log/syslog | grep -A 50 'bobshell.sh'"`
- Manual installation fallback
- Document failure and try alternative approach

## Technical Details

### Installation Method Evolution
| Version | Method | Status |
|---------|--------|--------|
| v1 | Manual setup | ❌ Failed (no sudo) |
| v2 | Inline metadata | ❌ Failed (PowerShell quote parsing) |
| v3 | File-based script | ❌ Failed (wrong Bob URL: bob.build) |
| v4 | npm install | ❌ Failed (@ibm/bob-shell doesn't exist) |
| v5 | Mise-based | ⏸️ Not tested yet |
| **v6** | **Official installer** | ⏳ **Testing now** |

### Why v6 Should Work
1. ✅ Uses official IBM-provided installation script
2. ✅ Runs as user (not root) - matches Bob Shell's expected environment
3. ✅ Documented method in official Bob Shell docs
4. ✅ No dependency on npm registry packages
5. ✅ Includes retry logic and verification

### Cost Tracking
- **Setup attempts**: 4 VMs × 8 minutes × $0.093/hour = $0.050
- **Current v6 VM**: Running, will cost ~$0.012 for 8-minute setup
- **Total spent**: ~$0.062
- **Remaining GCP credit**: $299.94

## Success Criteria

### VM Setup Complete ✅
- [ ] VM boots and SSH works
- [ ] DNS resolves external domains
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

1. **Always verify package existence**: Don't assume npm packages exist without checking
2. **Use official installers**: IBM provides official installation scripts for a reason
3. **Check logs early**: The syslog revealed the 404 error immediately
4. **Document assumptions**: The npm assumption was wrong and cost 3 VM iterations

## References

- **Bob Shell Docs**: `bobshell_docs.md` (line 1789 - official installation method)
- **v6 Script**: `scripts/vm_startup_script_v6.sh` (60 lines)
- **Previous Status**: `docs/workflow/VM_ANTIGRAVITY_CONTINUATION_PROMPT.md`
- **Wave 2 Plan**: `docs/workflow/WAVE_2_CONTINUATION_PROMPT.md`

---

**Status**: ⏳ Waiting for v6 VM setup to complete (8 minutes)
**Next Action**: Run verification command at 05:00 UTC