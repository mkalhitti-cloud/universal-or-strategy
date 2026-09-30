# VM Update Procedure - PR Reference Removal

**Date**: 2026-06-20  
**Purpose**: Update VM configuration to remove PR references from autonomous workflow  
**Related**: `docs/workflow/PR_REFERENCE_REMOVAL_REPORT.md`

## Overview

This document provides step-by-step instructions for updating the VM to match the local PR reference removal changes.

## Prerequisites

- [x] Local changes committed to `gitbutler/workspace` branch
- [x] SSH access to VM
- [x] VM repository accessible
- [x] Backup of VM configuration (recommended)

## Files to Update

### 1. Agent Configuration
**File**: `agents/wave-2-orchestrator-agent.yaml`  
**Changes**: 2 locations

```yaml
# Line 43: Change phase description
- ### Phase 3: DNA & PR Audit
+ ### Phase 3: DNA Audit

# Line 88: Change tool description
- description: Execute Phase 3 (DNA & PR Audit) for an epic
+ description: Execute Phase 3 (DNA Audit) for an epic
```

### 2. Template Verification
**File**: `scripts/wave7/TEMPLATE_VERIFICATION.md`  
**Changes**: 1 location

```markdown
# Line 17: Change phase notes
- | 3 | `phase3_template_v12_52.sh` | ✅ Verified | DNA & PR Audit |
+ | 3 | `phase3_template_v12_52.sh` | ✅ Verified | DNA Audit |
```

## Update Steps

### Option A: Git Pull (Recommended)

```bash
# 1. SSH to VM
ssh user@vm-host

# 2. Navigate to repository
cd /path/to/universal-or-strategy

# 3. Check current branch
git branch --show-current
# Should show: gitbutler/workspace (or your VM branch)

# 4. Stash any local changes (if needed)
git stash

# 5. Pull latest changes
git pull origin gitbutler/workspace

# 6. Verify changes applied
git log -1 --oneline
# Should show: "Remove PR references from autonomous workflow"

# 7. Verify agent config
grep "DNA Audit" agents/wave-2-orchestrator-agent.yaml
# Should return 2 matches

# 8. Verify template verification
grep "DNA Audit" scripts/wave7/TEMPLATE_VERIFICATION.md
# Should return 1 match

# 9. Verify no PR references remain
grep -r "PR Audit" agents/ scripts/wave7/ .lamport/wave7/
# Should return no results
```

### Option B: Manual Update (If Git Pull Fails)

```bash
# 1. SSH to VM
ssh user@vm-host

# 2. Navigate to repository
cd /path/to/universal-or-strategy

# 3. Backup current files
cp agents/wave-2-orchestrator-agent.yaml agents/wave-2-orchestrator-agent.yaml.bak
cp scripts/wave7/TEMPLATE_VERIFICATION.md scripts/wave7/TEMPLATE_VERIFICATION.md.bak

# 4. Edit agent configuration
nano agents/wave-2-orchestrator-agent.yaml
# Change "DNA & PR Audit" to "DNA Audit" (2 locations: lines 43, 88)

# 5. Edit template verification
nano scripts/wave7/TEMPLATE_VERIFICATION.md
# Change "DNA & PR Audit" to "DNA Audit" (1 location: line 17)

# 6. Verify changes
grep "DNA Audit" agents/wave-2-orchestrator-agent.yaml
# Should return 2 matches

grep "DNA Audit" scripts/wave7/TEMPLATE_VERIFICATION.md
# Should return 1 match

# 7. Verify no PR references remain
grep -r "PR Audit" agents/ scripts/wave7/ .lamport/wave7/
# Should return no results
```

## Verification Checklist

After updating, verify the following:

- [ ] `agents/wave-2-orchestrator-agent.yaml` contains "DNA Audit" (not "DNA & PR Audit")
- [ ] `scripts/wave7/TEMPLATE_VERIFICATION.md` contains "DNA Audit" (not "DNA & PR Audit")
- [ ] No "PR Audit" references in `agents/` directory
- [ ] No "PR Audit" references in `scripts/wave7/` directory
- [ ] No "PR Audit" references in `.lamport/wave7/` directory
- [ ] `.mcp.json.vm` still has Greptile removed (from previous update)
- [ ] VM can still execute Phase 0 test (smoke test)

## Smoke Test

Run a quick Phase 0 test to verify VM is still functional:

```bash
# 1. Navigate to scripts directory
cd scripts/wave7

# 2. Run Phase 0 for a test epic (if available)
bash phase0_epic_001.sh

# 3. Check for errors
echo $?
# Should return 0 (success)

# 4. Verify output file created
ls -la ../../docs/brain/EPIC-CCN-001/00-hotspots.md
# Should exist

# 5. Check Lamport event log
tail -1 ../../.lamport/wave7/event_log.jsonl
# Should show phase_complete event
```

## Rollback Procedure (If Needed)

If the update causes issues:

```bash
# 1. Restore backup files
cp agents/wave-2-orchestrator-agent.yaml.bak agents/wave-2-orchestrator-agent.yaml
cp scripts/wave7/TEMPLATE_VERIFICATION.md.bak scripts/wave7/TEMPLATE_VERIFICATION.md

# 2. Or revert git commit
git revert HEAD

# 3. Verify rollback
grep "DNA & PR Audit" agents/wave-2-orchestrator-agent.yaml
# Should return 2 matches (old version restored)
```

## MCP Configuration

### Already Updated (No Action Needed)
The `.mcp.json.vm` file was already updated in a previous session to remove Greptile MCP. No further MCP changes are needed for this update.

**Verification**:
```bash
# Check MCP config
cat .mcp.json.vm | grep -i greptile
# Should return no results (Greptile already removed)
```

## Common Issues

### Issue 1: Git Pull Conflicts
**Symptom**: `git pull` fails with merge conflicts  
**Solution**: Use Option B (Manual Update) instead

### Issue 2: Permission Denied
**Symptom**: Cannot edit files  
**Solution**: Use `sudo` or check file permissions
```bash
sudo nano agents/wave-2-orchestrator-agent.yaml
```

### Issue 3: Files Not Found
**Symptom**: Files don't exist at expected paths  
**Solution**: Verify you're in the correct repository directory
```bash
pwd
# Should show: /path/to/universal-or-strategy
```

### Issue 4: Changes Not Taking Effect
**Symptom**: Old "PR Audit" text still appears  
**Solution**: Clear any caches and restart Bob CLI
```bash
# Clear Bob cache (if applicable)
rm -rf ~/.bob/cache

# Restart any running Bob processes
pkill -f bob
```

## Post-Update Actions

After successful update:

1. **Document Update**:
   ```bash
   echo "$(date): PR references removed from VM" >> VM_UPDATE_LOG.txt
   ```

2. **Notify Team**:
   - Update team that VM is ready for Wave 7
   - Confirm Phase 3 now performs "DNA Audit" only

3. **Update Roadmap**:
   - Mark VM update as complete in Wave 7 roadmap
   - Update any VM-specific documentation

4. **Test Wave 7 Pilot**:
   - Execute 3 pilot epics to verify changes
   - Monitor for any PR-related errors

## Success Criteria

Update is successful when:

- [x] All files updated (2 files, 3 changes)
- [x] No "PR Audit" references in autonomous workflow files
- [x] Smoke test passes (Phase 0 executes successfully)
- [x] No errors in Lamport event log
- [x] VM ready for Wave 7 execution

## Timeline

**Estimated Time**: 10-15 minutes
- Git pull: 2 minutes
- Verification: 5 minutes
- Smoke test: 5 minutes
- Documentation: 3 minutes

## Support

If issues arise during update:

1. **Check Logs**: Review Lamport event log for errors
2. **Rollback**: Use rollback procedure if needed
3. **Contact**: Report issues to Wave 7 orchestrator
4. **Document**: Log any issues in VM_UPDATE_LOG.txt

## Related Documentation

- `docs/workflow/PR_REFERENCE_REMOVAL_REPORT.md` - Comprehensive removal report
- `docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md` - Script generation SOP
- `building-blocks/autonomous-refactoring/ARCHITECTURE.md` - Architecture overview
- `.mcp.json.vm` - VM MCP configuration (already updated)

---

**Document Version**: 1.0  
**Last Updated**: 2026-06-20  
**Status**: Ready for VM deployment ✅