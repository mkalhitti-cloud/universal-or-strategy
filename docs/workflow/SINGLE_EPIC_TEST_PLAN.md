# Single Epic Test Plan - Golden Image Validation

**Date**: 2026-06-12  
**Status**: Ready to Execute  
**Golden Image**: `v12-bob-shell-golden-v1`  
**Test Epic**: EPIC-CCN-164 (Complexity: 21, Low risk)

## Objective

Validate the golden image (`v12-bob-shell-golden-v1`) can successfully execute a complete V12 epic workflow before launching full Wave 2 parallel execution.

## Golden Image Details

**Image Name**: `v12-bob-shell-golden-v1`  
**Family**: `v12-bob-shell`  
**Status**: READY  
**Created**: 2026-06-12 05:35 UTC

**Pre-installed Software**:
- ✅ Bob Shell v1.0.4
- ✅ Node.js 22.x
- ✅ Git 2.x
- ✅ Python 3.12
- ✅ npm configured for user-level global installs (`~/.npm-global/`)

**Installation Method**: v8 startup script with npm prefix fix

## Test Epic Selection

**EPIC-CCN-164**: `ExtractMethod_V12_002_Entries_RMA_MonitorRmaProximity_Lines_1_to_21`

**Rationale**:
- **Low Complexity**: CYC 21 (manageable scope)
- **Low Risk**: Single method extraction
- **Representative**: Tests full Phase 0-6 workflow
- **Fast**: Should complete in ~15-20 minutes

**Alternative**: EPIC-CCN-107 (CYC 20) if EPIC-CCN-164 unavailable

## Test VM Configuration

**VM Name**: `v12-test-epic-164`  
**Machine Type**: `n2-standard-8` (8 vCPUs, 32GB RAM)  
**Boot Disk**: 50GB  
**Provisioning**: SPOT instance (cost-effective)  
**Zone**: `us-central1-a`  
**Image**: `v12-bob-shell-golden-v1`

## Test Execution Steps

### 1. Launch Test VM
```powershell
gcloud compute instances create v12-test-epic-164 \
  --zone=us-central1-a \
  --machine-type=n2-standard-8 \
  --image=v12-bob-shell-golden-v1 \
  --boot-disk-size=50GB \
  --provisioning-model=SPOT \
  --instance-termination-action=DELETE \
  --metadata=startup-script='#!/bin/bash
# Clone repo
cd /home/malhitticrypto
git clone https://github.com/malhitticrypto-debug/universal-or-strategy.git
cd universal-or-strategy

# Configure Git
git config --global user.email "malhitticrypto@gmail.com"
git config --global user.name "malhitticrypto"

# Mark setup complete
echo "Test VM ready!" > /tmp/test_vm_ready.txt
'
```

### 2. Wait for Startup (2 minutes)
Allow time for:
- VM boot
- Startup script execution
- Git clone completion

### 3. Verify Environment
```powershell
gcloud compute ssh v12-test-epic-164 --zone=us-central1-a --command="bash -l -c 'bob --version && git --version && python3 --version'"
```

**Expected Output**:
```
1.0.4
git version 2.x.x
Python 3.12.x
```

### 4. Execute Epic Workflow
```powershell
gcloud compute ssh v12-test-epic-164 --zone=us-central1-a --command="bash -l -c 'cd universal-or-strategy && bob /epic-intake EPIC-CCN-164'"
```

### 5. Monitor Progress
Check epic status every 5 minutes:
```powershell
gcloud compute ssh v12-test-epic-164 --zone=us-central1-a --command="bash -l -c 'cd universal-or-strategy && cat docs/brain/EPIC-CCN-164/manifest.json'"
```

### 6. Verify Completion
After ~15-20 minutes, check final status:
```powershell
gcloud compute ssh v12-test-epic-164 --zone=us-central1-a --command="bash -l -c 'cd universal-or-strategy && cat docs/brain/EPIC-CCN-164/05-completion-report.md'"
```

## Success Criteria

### ✅ Environment Validation
- [ ] Bob Shell v1.0.4 accessible
- [ ] Git operations succeed
- [ ] Python 3.12 available
- [ ] Repo cloned successfully

### ✅ Epic Execution
- [ ] Phase 0 (Hotspot Analysis) completes
- [ ] Phase 1 (Scope Definition) completes
- [ ] Phase 1.5 (Scope Boundary) passes
- [ ] Phase 2 (Architecture Planning) completes
- [ ] Phase 3 (DNA & PR Audit) passes
- [ ] Phase 4 (Ticket Generation) completes
- [ ] Phase 5 (Ticket Execution) succeeds
- [ ] Phase 5.V (Verification) passes
- [ ] Phase 6 (Final Review) completes

### ✅ Quality Gates
- [ ] No compilation errors
- [ ] Complexity reduced (target: CYC ≤ 8)
- [ ] All tests pass
- [ ] PR hygiene validated
- [ ] `manifest.json` shows all phases `completed`

### ✅ Artifact Validation
- [ ] `00-hotspots.md` exists
- [ ] `00-scope.md` exists
- [ ] `01-scope-boundary.md` exists
- [ ] `02-architecture-plan.md` exists
- [ ] `03-audit-report.md` exists
- [ ] `04-tickets.md` exists
- [ ] Ticket completion reports exist
- [ ] `05-completion-report.md` exists

## Failure Scenarios

### Scenario 1: Bob Shell Not Found
**Symptom**: `bob: command not found`  
**Diagnosis**: PATH not loaded correctly  
**Fix**: Verify `~/.npm-global/bin` in PATH

### Scenario 2: Git Clone Fails
**Symptom**: Authentication error  
**Diagnosis**: GitHub credentials not configured  
**Fix**: Add SSH key or use HTTPS with token

### Scenario 3: Epic Execution Hangs
**Symptom**: Phase stuck for >10 minutes  
**Diagnosis**: Bob Shell waiting for input or API timeout  
**Fix**: Check logs, restart phase

### Scenario 4: Compilation Errors
**Symptom**: Phase 5 fails with build errors  
**Diagnosis**: Pre-existing codebase issues  
**Fix**: Document and skip to next epic

## Rollback Plan

If test fails:
1. Stop test VM
2. Analyze failure logs
3. Fix golden image if needed (create v2)
4. Re-run test
5. Do NOT proceed to Wave 2 until test passes

## Wave 2 Launch Criteria

**ONLY proceed with full Wave 2 if**:
- ✅ Single epic test completes successfully
- ✅ All quality gates pass
- ✅ No environment issues detected
- ✅ Execution time within expected range (15-20 min)

**Wave 2 Configuration** (if test passes):
- Launch 10 parallel VMs (one per epic)
- Use same golden image (`v12-bob-shell-golden-v1`)
- Monitor via `scripts/monitor_vm_progress.py`
- Expected completion: 2-3 hours for all 10 epics

## Cost Analysis

**Test VM Cost**:
- n2-standard-8 SPOT: ~$0.08/hour
- Expected runtime: 0.5 hours
- **Total**: ~$0.04

**Wave 2 Cost** (if test passes):
- 10 VMs × $0.08/hour × 3 hours = $2.40
- **Total Wave 2**: ~$2.40

**Risk Mitigation**: $0.04 test cost prevents wasting $2.40 on broken setup

## Timeline

| Step | Duration | Cumulative |
|------|----------|------------|
| Launch test VM | 2 min | 2 min |
| Verify environment | 1 min | 3 min |
| Execute epic | 15-20 min | 18-23 min |
| Verify results | 2 min | 20-25 min |
| **Total** | **20-25 min** | **20-25 min** |

## Next Steps

1. **Now**: Launch test VM
2. **+2 min**: Verify environment
3. **+3 min**: Start epic execution
4. **+20 min**: Check completion
5. **+25 min**: If success → Launch Wave 2
6. **+25 min**: If failure → Debug and retry

## References

- Golden Image Creation: `scripts/vm_startup_script_v8.sh`
- Wave 2 Orchestrator: `scripts/wave2_simple_orchestrator.py`
- Epic Manifest: `scripts/epic_manifest.py`
- VM Monitoring: `scripts/monitor_vm_progress.py`