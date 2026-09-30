---
name: lamport-clock-recovery
description: Diagnose and repair Lamport clock non-determinism errors in V12.52 epic workflows with automatic conflict detection and surgical fixes
---

# Lamport Clock Recovery

**Version**: 1.0
**Created**: 2026-06-17
**Last Updated**: 2026-06-17

## What it does

Diagnoses and repairs Lamport clock non-determinism errors in V12.52 epic workflows. The Lamport clock system tracks execution history to ensure deterministic workflow execution. When state hash mismatches occur, this skill identifies the root cause and applies surgical fixes.

## When to use

- Epic execution blocked with "NON-DETERMINISTIC: State hash mismatch" error
- Phase 0 shows "completed" in manifest but hotspot file missing
- Multiple execution attempts created conflicting state hashes
- Need to clear Lamport history for specific epics

## What you need

- Access to `.lamport/event_log.jsonl` (VM or local)
- Epic manifest files in `docs/brain/EPIC-*/manifest.json`
- SSH access to GCP VM (if running on VM)

## How to use it

### 1. Diagnose the Issue

**Check for Lamport conflicts:**
```bash
# On VM
cd ~/universal-or-strategy
grep "NON-DETERMINISTIC" logs/wave*/phase*/*.log | head -10

# Check specific epic's event log
grep "EPIC-CCN-001" .lamport/event_log.jsonl
```

**Common patterns:**
- Multiple `phase_complete` events with different `state_hash` values
- Manifest shows `status: "completed"` but output file missing
- Different agents (`wave6-p0-001` vs `pilot-test-001`) completed same phase

### 2. Identify Root Cause

**Type A: Manifest-File Mismatch**
- Manifest: `phases.0.status = "completed"`
- Reality: `docs/brain/EPIC-*/00-hotspots.md` doesn't exist
- **Fix**: Reset manifest phase status to `pending`

**Type B: Conflicting State Hashes**
- Event log shows 2+ `phase_complete` events for same epic+phase
- Each has different `state_hash`
- **Fix**: Remove all events for that epic from event log

**Type C: Pilot Test Pollution**
- Pilot test created event log entries
- Production run detects conflict with pilot's state hash
- **Fix**: Remove pilot test events from event log

### 3. Apply Fix

**For Type A (Manifest-File Mismatch):**
```bash
# Use fix_phase0_status.py
python3 scripts/fix_phase0_status.py

# Manually (if script unavailable):
# Edit manifest.json, change phases.0.status from "completed" to "pending"
# Remove phases.0.outputs and phases.0.created_at fields
```

**For Type B/C (Conflicting State Hashes):**
```bash
# Use clean_lamport_event_log.py
python3 scripts/clean_lamport_event_log.py

# Manually (if script unavailable):
# 1. Backup event log
cp .lamport/event_log.jsonl .lamport/event_log.jsonl.backup

# 2. Filter out conflicting epic's events
grep -v "EPIC-CCN-001" .lamport/event_log.jsonl > .lamport/event_log.jsonl.tmp
mv .lamport/event_log.jsonl.tmp .lamport/event_log.jsonl
```

### 4. Relaunch Epic

**Kill existing screen session:**
```bash
screen -ls | grep "wave6_p0_001" | cut -d. -f1 | awk '{print $1}' | xargs -I {} screen -S {} -X quit
```

**Relaunch:**
```bash
bash scripts/wave6/_p0_epic_ccn_001.sh
```

**Monitor:**
```bash
tail -f logs/wave6/phase0/EPIC-CCN-001_recovery.log
```

## Self-Healing Features

### 1. Automatic Conflict Detection

**Symptom**: Epic fails with "State hash mismatch"

**Auto-Recovery**:
1. Script detects Lamport error in log
2. Identifies conflicting events in `.lamport/event_log.jsonl`
3. Backs up event log
4. Removes conflicting entries
5. Resets manifest phase status
6. Relaunches epic automatically

**Verification**:
```bash
# Check if auto-recovery succeeded
grep "✅.*Reset Phase 0 to pending" logs/wave6/phase0/EPIC-*_recovery.log
grep "✅.*Event log cleaned" logs/wave6/phase0/EPIC-*_recovery.log
```

### 2. Manifest Consistency Check

**Symptom**: Manifest says "completed" but file missing

**Auto-Recovery**:
1. Script checks for output file existence
2. If missing, resets phase status to `pending`
3. Removes stale `outputs` and `created_at` fields
4. Logs inconsistency for audit

**Verification**:
```bash
# Check manifest status
cat docs/brain/EPIC-CCN-001/manifest.json | jq '.phases."0".status'

# Check file existence
ls docs/brain/EPIC-CCN-001/00-hotspots.md
```

### 3. Event Log Backup

**Symptom**: Event log corruption or accidental deletion

**Auto-Recovery**:
1. Before any modification, creates timestamped backup
2. Backup location: `.lamport/event_log.jsonl.backup.TIMESTAMP`
3. Keeps last 5 backups, auto-deletes older ones

**Verification**:
```bash
ls -lt .lamport/event_log.jsonl.backup.* | head -5
```

## Common Issues & Auto-Recovery

### Issue: "State hash mismatch: 2 different states"

**Cause**: Multiple execution attempts with different outcomes

**Auto-Fix**:
```bash
# Automatic via recovery script
python3 scripts/clean_lamport_event_log.py

# Manual verification
grep "EPIC-CCN-001" .lamport/event_log.jsonl | wc -l  # Should be 0 after cleaning
```

**Prevention**: Always kill screen sessions before relaunching

### Issue: Manifest shows completed but file missing

**Cause**: File creation failed but manifest updated

**Auto-Fix**:
```bash
# Automatic via status fix script
python3 scripts/fix_phase0_status.py

# Manual verification
cat docs/brain/EPIC-CCN-001/manifest.json | jq '.phases."0".status'  # Should be "pending"
```

**Prevention**: Use file persistence verification in phase scripts

### Issue: Pilot test polluted event log

**Cause**: Pilot test ran before production wave

**Auto-Fix**:
```bash
# Remove pilot test events
grep -v "pilot-test" .lamport/event_log.jsonl > .lamport/event_log.jsonl.tmp
mv .lamport/event_log.jsonl.tmp .lamport/event_log.jsonl
```

**Prevention**: Use separate event logs for pilot vs production

## Scripts Reference

### `scripts/fix_phase0_status.py`
- **Purpose**: Reset Phase 0 status when manifest-file mismatch detected
- **Usage**: `python3 scripts/fix_phase0_status.py`
- **Output**: Reports which epics were fixed

### `scripts/clean_lamport_event_log.py`
- **Purpose**: Remove conflicting events from Lamport event log
- **Usage**: `python3 scripts/clean_lamport_event_log.py`
- **Output**: Shows events removed, total before/after

### `scripts/clear_lamport_conflicts.py`
- **Purpose**: Clear lamport_clock field from manifests (deprecated - field not used)
- **Usage**: `python3 scripts/clear_lamport_conflicts.py`
- **Note**: Superseded by event log cleaning

## Post-Use Audit (MANDATORY)

After using this skill, verify:

1. **Conflict Resolution**:
   ```bash
   # No more "State hash mismatch" errors
   grep "NON-DETERMINISTIC" logs/wave6/phase0/*.log | wc -l  # Should be 0
   ```

2. **Manifest Consistency**:
   ```bash
   # All "completed" phases have output files
   for epic in docs/brain/EPIC-CCN-*/; do
     status=$(cat $epic/manifest.json | jq -r '.phases."0".status')
     file="$epic/00-hotspots.md"
     if [ "$status" = "completed" ] && [ ! -f "$file" ]; then
       echo "❌ Inconsistency: $epic"
     fi
   done
   ```

3. **Event Log Integrity**:
   ```bash
   # Event log is valid JSON
   cat .lamport/event_log.jsonl | jq -s '.' > /dev/null && echo "✅ Valid" || echo "❌ Corrupt"
   ```

4. **Skill Gaps**: State `skill(lamport-clock-recovery): no gaps identified` if no issues found during use.

## Related Skills

- [`gcp-vm-wave-execution`](.bob/skills/gcp-vm-wave-execution/skill.md) - Parent skill for wave orchestration
- [`building-blocks-method`](building-blocks/autonomous-refactoring/ARCHITECTURE.md) - Manifest-based architecture

## Version History

- **V1.0** (2026-06-17): Initial creation
  - Type A/B/C conflict patterns identified
  - Auto-recovery scripts created
  - Self-healing features documented