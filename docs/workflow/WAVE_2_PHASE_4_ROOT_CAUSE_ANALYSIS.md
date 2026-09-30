# Wave 2 Phase 4 - Root Cause Analysis & Fix

**Date**: 2026-06-12 20:28 UTC  
**Issue**: Phase 4 agents never launched despite script reporting success

## Root Cause

### The Bug

In `scripts/wave2/phase4_with_checkpoints.py` line 186:

```python
if status == "pending":
    epics_to_run.append(epic)
    # Mark as in_progress
    update_manifest(epic_id, PHASE, "in_progress")  # ← BUG: Marks BEFORE launching
```

**Problem**: The script marks epics as "in_progress" BEFORE actually launching them on the VM.

### What Happened

1. **First run** (19:55 UTC): Script marked all 9 epics as "in_progress" locally
2. **Upload/Execute**: Script tried to upload to VM but gcloud wasn't available locally
3. **Result**: Manifests show "in_progress" but agents never launched
4. **Second run** (any retry): Script sees "in_progress" and skips with warning:
   ```
   [WARNING] EPIC-CCN-107 Phase 4 already in progress
   [STATUS] Epics to run: 0
   [COMPLETE] All epics already completed or in progress!
   ```

### Why API Tokens Didn't Work

**The agents never ran.** The script exited early because it thought agents were already running.

## The Fix

### Self-Healing Launch Script

The script needs to:
1. ✅ Check if epic needs to run
2. ✅ Launch agent on VM
3. ✅ **ONLY THEN** mark as "in_progress"
4. ✅ If launch fails, keep status as "pending" for retry
5. ✅ Add timeout detection: if "in_progress" for >60 minutes, auto-reset to "pending"

### Implementation

```python
def check_phase_status_with_timeout(epic_id: str, phase: str, timeout_minutes: int = 60) -> str:
    """Check phase status with auto-reset for stalled agents"""
    manifest = load_manifest(epic_id)
    
    if phase not in manifest["phases"]:
        # Add missing phase
        manifest["phases"][phase] = {"status": "pending", "output": f"0{phase}-tickets.md"}
        save_manifest(epic_id, manifest)
        return "pending"
    
    status = manifest["phases"][phase]["status"]
    
    # Self-healing: Reset stalled "in_progress" to "pending"
    if status == "in_progress":
        last_updated = datetime.fromisoformat(manifest.get("last_updated", "2000-01-01T00:00:00"))
        elapsed = (datetime.utcnow() - last_updated).total_seconds() / 60
        
        if elapsed > timeout_minutes:
            print(f"[HEAL] EPIC-CCN-{epic_id} stalled for {elapsed:.0f} min, resetting to pending")
            manifest["phases"][phase]["status"] = "pending"
            save_manifest(epic_id, manifest)
            return "pending"
    
    return status

def launch_agents_then_mark(epics_to_run: list):
    """Launch agents on VM, THEN mark as in_progress"""
    # Build script
    script_content = build_phase4_script(epics_to_run)
    script_path = Path("/tmp/wave2_phase4.sh")
    script_path.write_text(script_content, newline="\n")
    
    try:
        # Upload to VM
        subprocess.run([
            "gcloud", "compute", "scp",
            str(script_path), f"{VM_NAME}:/tmp/wave2_phase4.sh",
            f"--zone={ZONE}"
        ], check=True)
        
        # Execute on VM
        subprocess.run([
            "gcloud", "compute", "ssh", VM_NAME,
            f"--zone={ZONE}",
            "--command=bash /tmp/wave2_phase4.sh"
        ], check=True)
        
        # SUCCESS: Now mark as in_progress
        for epic in epics_to_run:
            update_manifest(epic["id"], PHASE, "in_progress")
            print(f"[LAUNCHED] EPIC-CCN-{epic['id']} agent running on VM")
        
        return True
        
    except subprocess.CalledProcessError as e:
        print(f"[ERROR] Failed to launch agents: {e}")
        print("[KEEP] Manifests remain 'pending' for retry")
        return False
```

## Lessons Learned

### 1. State Management
- ❌ **Don't**: Mark state changes before confirming action succeeded
- ✅ **Do**: Mark state changes AFTER confirming action succeeded

### 2. Self-Healing
- ✅ Add timeout detection for stalled states
- ✅ Auto-reset to "pending" if stuck too long
- ✅ Make retries idempotent

### 3. Observability
- ✅ Log every state transition with timestamp
- ✅ Distinguish between "never started" and "stalled"
- ✅ Report actual vs expected state

### 4. Error Handling
- ✅ Catch gcloud failures gracefully
- ✅ Keep manifests in retry-able state on failure
- ✅ Provide clear next steps in error messages

## Implementation Plan

### Phase 1: Fix Launch Script (Immediate)
1. Move `update_manifest()` call to AFTER successful VM launch
2. Add try/catch around gcloud commands
3. Keep manifests as "pending" if launch fails

### Phase 2: Add Self-Healing (Next)
1. Add timeout detection in `check_phase_status()`
2. Auto-reset stalled "in_progress" to "pending"
3. Add elapsed time to status checks

### Phase 3: Improve Observability (Future)
1. Add structured logging with timestamps
2. Track state transitions in separate log
3. Add health check endpoint

## Testing Strategy

### Test Case 1: Normal Flow
1. All manifests "pending"
2. Launch script runs
3. Agents launch on VM
4. Manifests marked "in_progress"
5. Agents complete
6. Manifests marked "completed"

### Test Case 2: Launch Failure
1. Manifests "pending"
2. Launch script runs
3. gcloud fails (VM down, no auth, etc.)
4. Manifests REMAIN "pending" (not "in_progress")
5. Retry succeeds

### Test Case 3: Stalled Agent
1. Agent marked "in_progress" at T=0
2. At T=60min, agent still "in_progress"
3. Next launch detects timeout
4. Auto-resets to "pending"
5. Re-launches agent

### Test Case 4: Duplicate Launch Prevention
1. Agent running on VM
2. Manifest shows "in_progress" (recent timestamp)
3. Second launch attempt
4. Detects recent "in_progress", skips
5. No duplicate agents

## Metrics to Track

- **Launch Success Rate**: % of launches that succeed
- **Agent Completion Rate**: % of agents that complete vs stall
- **Time to Completion**: P50, P95, P99 for each phase
- **Retry Count**: How many retries needed per epic
- **Stall Detection**: How many auto-resets triggered

## Next Steps

1. ✅ Document root cause (this file)
2. ⏳ Fix launch script with proper state management
3. ⏳ Add self-healing timeout detection
4. ⏳ Test with single epic first
5. ⏳ Roll out to all 9 epics

---

**Status**: Root cause identified, fix designed  
**Impact**: Zero bobcoins wasted (agents never ran)  
**Recovery**: Simple - reset manifests and re-launch with fixed script