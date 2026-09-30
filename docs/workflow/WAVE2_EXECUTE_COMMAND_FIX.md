# Wave 2 Phase 0: execute_command Fix

**Date**: 2026-06-13  
**Issue**: File persistence bug in SSH/screen mode  
**Root Cause**: `run_shell_command` tool fails silently in background execution  
**Solution**: Replace with `execute_command` tool + explicit `cwd` parameter

## Problem Summary

### Initial Symptoms
- All 9 Phase 0 agents completed successfully (DONE_EXIT=0)
- Logs showed "files created and verified"
- But all directories were empty (`total 0`)

### Root Cause Analysis

**Bob Shell Tool Hierarchy**:
1. `write_to_file` - FAILS in SSH mode (path resolution bug)
2. `read_file` - FAILS in SSH mode ("File not found" even when exists)
3. `run_shell_command` - FAILS in SSH/screen mode (persistence bug)
4. `execute_command` - WORKS (bypasses tool layer entirely)

**Why `run_shell_command` Failed**:
- Executes in Bob's internal context, not the SSH session
- Files created in Bob's working directory, not VM filesystem
- Reports success but doesn't persist to disk
- Same bug as `write_to_file`, just at a different layer

## Solution Applied

### Changes Made

**1. Updated Instructions** (Lines 15-22 in all scripts):
```bash
**MANDATORY RULES (Violation = Task Failure)**:
1. ❌ NEVER use write_to_file tool - it has path resolution bugs in SSH mode
2. ❌ NEVER use read_file tool - it fails with "File not found" even when files exist
3. ❌ NEVER use run_shell_command tool - it also has persistence bugs in SSH mode
4. ✅ ALWAYS use execute_command tool with `cat > file << 'EOF'` to create files
5. ✅ ALWAYS use execute_command tool with `ls -lh` and `wc -l` to verify files
6. ✅ ALWAYS set cwd parameter to /home/malhitticrypto/universal-or-strategy
7. ✅ ALWAYS follow the EXACT tool usage patterns shown below (copy/paste them)
```

**2. Updated Code Examples** (All Step 2, 3, 4 sections):
```xml
<execute_command>
<command>
cat > docs/brain/EPIC-CCN-XXX/00-hotspots.md << 'EOF'
[content]
EOF</command>
<cwd>/home/malhitticrypto/universal-or-strategy</cwd>
</execute_command>
```

**3. Updated Rationale** (Lines 24-27):
```
**WHY THIS MATTERS**:
- execute_command bypasses Bob's tool layer and works reliably in SSH mode
- run_shell_command, write_to_file, and read_file all fail in SSH/screen sessions
- The working directory must be explicitly set with cwd parameter
```

### Files Modified

All 9 Phase 0 scripts updated:
- `_p0_107.sh` (manual fix via apply_diff)
- `_p0_108.sh` through `_p0_115.sh` (automated via bash script)

### Scripts Created

1. **`scripts/wave2/apply_execute_command_fix.sh`**
   - Automated sed-based replacement
   - Replaced `run_shell_command` with `execute_command`
   - Updated rule numbering and rationale

2. **`scripts/wave2/fix_xml_tags.sh`** (created but not needed)
   - Would have fixed code block formatting
   - Not required since manual fix already had correct XML

## Verification Steps

### Before Deployment
```bash
# Check one script for correct format
grep -A5 "execute_command" _p0_107.sh

# Verify all 9 scripts updated
for i in 107 108 109 110 111 112 113 114 115; do
  grep -c "execute_command" _p0_$i.sh
done
```

### After Deployment
```bash
# Deploy to VM
gcloud compute scp _p0_*.sh v12-test-golden-v2:~/universal-or-strategy/

# Launch Phase 0 (all 9 epics)
bash scripts/wave2/launch_phase0_all_screen.sh

# Monitor progress
watch -n 5 'screen -ls | grep phase0'

# Verify files created
for i in 107 108 109 110 111 112 113 114 115; do
  ls -lh docs/brain/EPIC-CCN-$i/
done
```

## Expected Outcome

**Success Criteria**:
- ✅ All 9 agents complete (DONE_EXIT=0)
- ✅ 18 files created (9 × 00-hotspots.md + 9 × manifest.json)
- ✅ Files verified with `ls -lh` showing non-zero sizes
- ✅ Ready to proceed to Phase 1 (Scope Definition)

## Lessons Learned

### Tool Selection Hierarchy (SSH Mode)
1. **First Choice**: `execute_command` with explicit `cwd`
2. **Never Use**: `write_to_file`, `read_file`, `run_shell_command`

### Why This Matters
- Bob Shell tools work great in interactive mode
- But fail silently in SSH/screen background execution
- `execute_command` bypasses the tool layer entirely
- Must always set `cwd` parameter explicitly

### Prevention
- Document this in `.bob/skills/gcp-vm-wave-execution/skill.md`
- Add to Wave 2 troubleshooting guide
- Include in all future VM-based agent scripts

## Related Documentation

- **Original Issue**: `scripts/wave2/READ_FILE_TOOL_ISSUE.md`
- **Wave 2 Skill**: `.bob/skills/gcp-vm-wave-execution/skill.md`
- **Custom Mode**: `.bob/custom_modes.yaml` (v12-phase0-hotspot)
- **Launcher**: `scripts/wave2/launch_phase0_all_screen.sh`

## Next Steps

1. Deploy fixed scripts to VM
2. Launch Phase 0 for all 9 epics
3. Verify file persistence
4. Proceed to Phase 1 (Scope Definition)
5. Update skill documentation with this fix