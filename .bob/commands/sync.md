# sync

description: Automate VM-to-local backup synchronization. Creates backup archive, verifies contents, and generates ready-to-paste prompt for local integration.

## Usage

```
/sync
```

No parameters needed. The command automatically:
1. Creates timestamped backup archive on VM
2. Verifies archive contents
3. Generates local integration prompt

## Protocol

You are the VM Sync Orchestrator. You automate the 3-step backup workflow.

### Step 1: Create Backup Archive

**Switch to: agent mode**

Hand off:
```
TASK: Create Backup Archive
PROTOCOL:
  1. Verify location: pwd should be /home/malhitticrypto/universal-or-strategy
  2. Run: /usr/bin/python3 package_wave7_for_local.py
  3. Wait for completion
  4. Find archive: ls -t ~/wave7_phase0_complete_*.tar.gz | head -1
  5. Get size: du -h <archive>
  6. Emit: [ARCHIVE-CREATED] Location: <path>, Size: <size>
```

**Gate:** Archive must be created successfully (typically 1-2 MB)

---

### Step 2: Verify Archive Contents

**Switch to: agent mode**

Hand off:
```
TASK: Verify Archive Contents
PROTOCOL:
  1. Extract to temp: mktemp -d
  2. Unpack: tar -xzf <archive> -C <temp>
  3. Count epics: find <temp> -type d -name "EPIC-W7-*" | wc -l
  4. Count templates: find <temp>/building-blocks/wave7 -type f | wc -l
  5. Cleanup: rm -rf <temp>
  6. Emit: [ARCHIVE-VERIFIED] Epics: <count>, Templates: <count>
```

**Gate:** Epic count should match expected (161 for Wave 7)

---

### Step 3: Generate Local Integration Prompt

**Switch to: agent mode**

Hand off:
```
TASK: Generate Local Integration Prompt
PROTOCOL:
  1. Create file: SYNC_TO_LOCAL_PROMPT.md
  2. Include:
     - Archive download command (SCP with actual path)
     - Extraction instructions
     - Copy commands for all file types
     - Verification commands
     - Success criteria
  3. Replace placeholders:
     - ARCHIVE_PATH → actual archive path
     - ARCHIVE_NAME → actual archive filename
     - BACKUP_DIR → extracted directory name
  4. Emit: [PROMPT-READY] File: SYNC_TO_LOCAL_PROMPT.md
```

**Output:** `SYNC_TO_LOCAL_PROMPT.md` in repository root

---

### Step 4: Display Prompt

**Mode:** Orchestrator (you generate this output)

Output:
```
========================================================================
VM Sync Complete!
========================================================================

✅ Backup Archive Created
   Location: <archive_path>
   Size: <size>

✅ Archive Contents Verified
   Epic directories: <count>
   Building-blocks templates: <count>
   Logs and scripts: Included

✅ Local Integration Prompt Ready
   File: SYNC_TO_LOCAL_PROMPT.md

========================================================================
📋 COPY THE PROMPT BELOW AND PASTE INTO YOUR LOCAL BOB IDE:
========================================================================

<display full contents of SYNC_TO_LOCAL_PROMPT.md>

========================================================================
Next Steps:
1. Copy the prompt above
2. Paste into your local Bob IDE
3. Provide VM IP when asked
4. Local agent will handle download and integration
========================================================================
```

---

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

---

## Local Integration Process

The generated prompt guides the local agent through:

1. **Download**: `scp malhitticrypto@VM_IP:<archive> .`
2. **Extract**: `tar -xzf <archive>`
3. **Copy**: Integrate files into local repository
4. **Verify**: Confirm all files present
5. **Report**: Create verification document

---

## Success Criteria

- ✅ Backup archive created (1-2 MB)
- ✅ Archive contents verified
- ✅ Integration prompt generated
- ✅ Prompt displayed for copy/paste
- ✅ Ready for local integration

---

## Use Cases

- **After Phase Completion**: Backup work before proceeding
- **Daily Checkpoints**: Create restore points during long waves
- **Pre-Local Work**: Sync VM work before switching to local machine
- **Disaster Recovery**: Regular backups for rollback capability

---

## Requirements

- Must be run on VM (not local machine)
- Python 3 available
- `package_wave7_for_local.py` present in repository root
- Sufficient disk space for archive (~2 MB)

---

## Tips

- **Run frequently**: After each phase completion
- **Archives are timestamped**: Multiple backups won't conflict
- **Prompt is universal**: Works with Bob IDE, Claude, or any agent
- **Idempotent**: Safe to run multiple times
- **No cleanup needed**: Archives remain in `~/` for reference

---

## Related Commands

- `/wave-status` - Check wave completion status
- `/epic-run` - Execute epic workflows
- GCP VM Wave Execution skill - Full wave orchestration

---

## Example Session

```
User: /sync

Agent:
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

Step 3: Generating local integration prompt...
✅ Local Integration Prompt Created
   File: SYNC_TO_LOCAL_PROMPT.md

========================================================================
📋 COPY THE PROMPT BELOW AND PASTE INTO YOUR LOCAL BOB IDE:
========================================================================
[Full integration instructions...]
```

---

*Command created: 2026-06-23*  
*Version: 1.0*