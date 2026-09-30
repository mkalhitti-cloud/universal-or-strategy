---
name: vm-sync
description: Automate VM-to-local backup synchronization for wave execution
---

# VM Sync

Automate VM-to-local backup synchronization workflow for Wave 7 (and future waves).

## What it does

Creates a backup archive on the VM and generates a ready-to-paste prompt for local integration.

**3-Step Process**:
1. **Create Backup Archive** - Packages all epic directories, templates, logs, and scripts
2. **Verify Contents** - Confirms all required files are included
3. **Generate Local Prompt** - Creates step-by-step integration instructions

## When to use

- After completing a wave phase on VM
- Before starting work on local machine
- To synchronize VM work to local repository
- To create backup checkpoints during long-running waves

## What you need

- Must be run on VM (not local machine)
- Python 3 available
- `package_wave7_for_local.py` script present in repository root

## How to use it

Simply run:
```
/sync
```

No parameters needed. The command automatically:
- Detects current wave context
- Creates timestamped backup archive
- Generates integration prompt
- Outputs prompt ready for copy/paste

## Output

The command provides:

### 1. Backup Archive Status
```
✅ Backup Archive Created
Location: /home/malhitticrypto/wave7_phase0_complete_[timestamp].tar.gz
Size: 1.1 MB
Status: Ready for download
```

### 2. Archive Contents Summary
```
✅ Archive Contents
- 161 EPIC-W7-* directories (00-hotspots.md + manifest.json each)
- Universal launcher with PATH fix
- Phase 0 template for future waves
- Session logs and execution logs
- All Phase 0 scripts and Python tools
```

### 3. Local Integration Prompt
```
✅ Local Integration Instructions Created
File: SYNC_TO_LOCAL_PROMPT.md

📋 COPY THE PROMPT BELOW AND PASTE INTO YOUR LOCAL BOB IDE:
---
[Full step-by-step integration instructions follow...]
```

## Files Created

- **Backup Archive**: `~/wave7_phase0_complete_[timestamp].tar.gz`
- **Integration Prompt**: `SYNC_TO_LOCAL_PROMPT.md` (in repository root)

## What Gets Transferred

### Epic Directories
- All EPIC-W7-* directories from `docs/brain/`
- Each contains: `00-hotspots.md`, `manifest.json`
- Preserves complete phase analysis

### Building-Blocks Templates
- Universal launcher with PATH fix
- Phase templates for future script generation
- Located in `building-blocks/wave7/`

### Logs and Documentation
- Session logs (`logs/wave7_*`)
- Execution logs (`logs/phase0/`)
- Status reports and analysis documents

### Scripts and Tools
- Phase execution scripts (`_p0_*.sh`, etc.)
- Python utilities (cleanup, fix, relaunch)
- Recovery and monitoring tools

## Local Integration Steps

The generated prompt includes:

1. **Download Command**
   ```bash
   scp malhitticrypto@VM_IP:~/wave7_phase0_complete_[timestamp].tar.gz .
   ```

2. **Extract Archive**
   ```bash
   tar -xzf wave7_phase0_complete_[timestamp].tar.gz
   ```

3. **Copy to Repository**
   ```bash
   cp -r wave7_backup_*/docs/brain/EPIC-W7-* /path/to/repo/docs/brain/
   cp -r wave7_backup_*/building-blocks/wave7/* /path/to/repo/building-blocks/wave7/
   # ... (additional copy commands)
   ```

4. **Verify Integration**
   ```bash
   ls -d docs/brain/EPIC-W7-* | wc -l  # Should show 161
   ```

5. **Create Verification Report**
   - Confirms all files present
   - Documents git status
   - Ready for commit

## Success Criteria

- ✅ Backup archive created successfully
- ✅ Archive size reasonable (typically 1-2 MB)
- ✅ Integration prompt generated
- ✅ Prompt contains all required steps
- ✅ SCP download command included
- ✅ Verification commands included

## Tips

- **Run after each phase completion** to maintain local backup
- **Archive is timestamped** - multiple backups won't conflict
- **Prompt is self-contained** - paste into any agent (Bob IDE, Claude, etc.)
- **VM IP required** - local agent will ask for it
- **Idempotent** - safe to run multiple times

## Related Skills

- `/wave-status` - Check wave completion status
- `/epic-run` - Execute epic workflows
- GCP VM Wave Execution - Full wave orchestration

## Example Session

```
User: /sync

Agent: 
✅ Backup Archive Created
Location: /home/malhitticrypto/wave7_phase0_complete_20260623_041615.tar.gz
Size: 1.1 MB

✅ Archive Contents
- 161 EPIC-W7-* directories
- Building-blocks templates
- Logs and execution scripts

✅ Local Integration Prompt Ready
File: SYNC_TO_LOCAL_PROMPT.md

📋 COPY THE PROMPT BELOW AND PASTE INTO YOUR LOCAL BOB IDE:
---
# Wave 7 Phase 0 Backup - Local Integration

[Full integration instructions...]
```

## Post-Use Audit

After using this skill:
- ✅ Verify archive was created
- ✅ Check archive size is reasonable
- ✅ Confirm prompt file was generated
- ✅ Test SCP download command format
- ✅ Document any issues encountered

**Last Audit**: 2026-06-23 - Skill created, awaiting first use