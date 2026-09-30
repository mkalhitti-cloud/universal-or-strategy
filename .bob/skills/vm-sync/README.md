# VM Sync Skill

## Quick Start

Simply run:
```
/sync
```

This will:
1. Create a backup archive on the VM
2. Verify the archive contents
3. Generate a ready-to-paste prompt for local integration

## What It Does

The `/sync` command automates the 3-step VM-to-local backup workflow:

### Step 1: Create Backup Archive
- Runs `package_wave7_for_local.py`
- Creates timestamped tar.gz archive in `~/`
- Includes all epic directories, templates, logs, and scripts
- Reports archive location and size

### Step 2: Verify Archive Contents
- Extracts archive to temp location
- Counts epic directories
- Verifies building-blocks templates
- Confirms logs and scripts included
- Cleans up temp files

### Step 3: Generate Local Integration Prompt
- Creates `SYNC_TO_LOCAL_PROMPT.md` in repository root
- Includes step-by-step download instructions
- Provides SCP command with actual archive path
- Contains extraction and verification commands
- Ready to copy/paste into local Bob IDE or any agent

## Output Example

```
========================================================================
VM Sync - Automated Backup Workflow
========================================================================

Step 1: Creating backup archive...

✅ Backup Archive Created
   Location: /home/malhitticrypto/wave7_phase0_complete_20260623_041615.tar.gz
   Size: 1.1 MB

Step 2: Verifying archive contents...

✅ Archive Contents Verified
   Epic directories: 161
   Building-blocks templates: 9
   Logs and scripts: Included

Step 3: Generating local integration prompt...

✅ Local Integration Prompt Created
   File: SYNC_TO_LOCAL_PROMPT.md

========================================================================
📋 COPY THE PROMPT BELOW AND PASTE INTO YOUR LOCAL BOB IDE:
========================================================================

[Full integration instructions follow...]

========================================================================
✅ VM Sync Complete!
========================================================================

Next Steps:
1. Copy the prompt above
2. Paste into your local Bob IDE
3. Provide VM IP when asked
4. Local agent will handle download and integration
```

## Files Created

1. **Backup Archive**: `~/wave7_phase0_complete_[timestamp].tar.gz`
   - Timestamped to avoid conflicts
   - Typically 1-2 MB compressed
   - Contains all wave work

2. **Integration Prompt**: `SYNC_TO_LOCAL_PROMPT.md`
   - Located in repository root
   - Self-contained instructions
   - Ready for copy/paste

## Local Integration Process

The generated prompt guides the local agent through:

1. **Download**: SCP command with actual archive path
2. **Extract**: Unpack to temporary directory
3. **Copy**: Integrate files into local repository
4. **Verify**: Confirm all files present
5. **Report**: Create verification document

## Use Cases

- **After Phase Completion**: Backup work before proceeding
- **Daily Checkpoints**: Create restore points during long waves
- **Pre-Local Work**: Sync VM work before switching to local machine
- **Disaster Recovery**: Regular backups for rollback capability

## Requirements

- Must be run on VM (not local machine)
- Python 3 available
- `package_wave7_for_local.py` present in repository root
- Sufficient disk space for archive (~2 MB)

## Tips

- **Run frequently**: After each phase completion
- **Archives are timestamped**: Multiple backups won't conflict
- **Prompt is universal**: Works with Bob IDE, Claude, or any agent
- **Idempotent**: Safe to run multiple times
- **No cleanup needed**: Archives remain in `~/` for reference

## Troubleshooting

### Error: package_wave7_for_local.py not found
- Ensure you're in the repository root
- Check the script exists: `ls -la package_wave7_for_local.py`

### Error: Backup archive not created
- Check Python 3 is available: `python3 --version`
- Check disk space: `df -h ~`
- Review script output for errors

### Archive size seems wrong
- Expected: 1-2 MB for 161 epics
- Too small: May be missing files
- Too large: May include unnecessary files

## Related Commands

- `/wave-status` - Check wave completion status
- `/epic-run` - Execute epic workflows
- GCP VM Wave Execution skill - Full wave orchestration

## Version History

- **v1.0** (2026-06-23): Initial implementation
  - 3-step automated workflow
  - Timestamped archives
  - Self-contained integration prompts
  - Universal agent compatibility