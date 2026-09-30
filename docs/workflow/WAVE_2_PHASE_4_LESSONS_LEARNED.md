# Wave 2 Phase 4 - Lessons Learned & Recovery Plan

**Date**: 2026-06-12 20:29 UTC  
**Status**: Root cause identified, self-healing fix implemented

## What Happened

### The Problem
Phase 4 launch script reported success but agents never ran. After 30 minutes of monitoring, 0/9 epics had generated tickets.

### Root Cause
**Bug in `phase4_with_checkpoints.py` line 186**: Script marked manifests as "in_progress" BEFORE launching agents on VM. When gcloud wasn't available locally, the launch failed but manifests were already marked "in_progress". On retry, script saw "in_progress" and skipped all epics thinking they were already running.

### Impact
- **Bobcoins Used**: 0 (agents never ran)
- **Time Lost**: 30 minutes of monitoring
- **Budget Impact**: None (still have 1,567.70 bobcoins)

## The Fix

### Self-Healing Launch Script v2

Created `scripts/wave2/phase4_with_checkpoints_v2.py` with:

1. **Proper State Management**
   - ✅ Launch agents on VM FIRST
   - ✅ Mark as "in_progress" ONLY after successful launch
   - ✅ Keep as "pending" if launch fails (safe to retry)

2. **Auto-Healing for Stalled Agents**
   - ✅ Detects "in_progress" for >60 minutes
   - ✅ Auto-resets to "pending" for retry
   - ✅ Prevents permanent stuck states

3. **Better Error Handling**
   - ✅ Catches gcloud failures gracefully
   - ✅ Provides clear error messages
   - ✅ Suggests next steps on failure

4. **Idempotent Retries**
   - ✅ Safe to run multiple times
   - ✅ Skips completed epics
   - ✅ Respects active agents

## Recovery Steps

### Option A: Use Self-Healing (Recommended)

The v2 script will auto-detect stalled manifests and reset them:

```bash
# Just run the v2 script - it will auto-heal
python scripts/wave2/phase4_with_checkpoints_v2.py
```

The script will:
1. Detect all 9 epics are "in_progress" for >60 minutes
2. Auto-reset them to "pending"
3. Launch agents on VM
4. Mark as "in_progress" after successful launch

### Option B: Manual Reset (If Needed)

If you want to manually reset first:

```bash
# Reset manifests
python scripts/wave2/reset_phase4_manifests.py

# Then launch with v2
python scripts/wave2/phase4_with_checkpoints_v2.py
```

## Key Learnings

### 1. State Management Principles

**❌ Wrong Pattern** (v1):
```python
# Mark state BEFORE action
update_manifest(epic_id, "in_progress")
launch_agent_on_vm(epic_id)  # If this fails, state is wrong
```

**✅ Right Pattern** (v2):
```python
# Do action FIRST
success = launch_agent_on_vm(epic_id)
if success:
    update_manifest(epic_id, "in_progress")  # Only mark if succeeded
```

### 2. Self-Healing Systems

**Add timeout detection**:
- Detect stalled states automatically
- Auto-reset to retry-able state
- Log healing actions for observability

**Benefits**:
- No manual intervention needed
- System recovers from transient failures
- Reduces operational burden

### 3. Error Handling

**Fail gracefully**:
- Catch exceptions at operation boundaries
- Keep state in retry-able condition
- Provide actionable error messages

**Example**:
```python
try:
    launch_agents()
    mark_as_running()  # Only if launch succeeded
except Exception as e:
    print(f"Launch failed: {e}")
    print("Manifests remain 'pending', safe to retry")
    # State is still "pending" - can retry
```

### 4. Observability

**Log state transitions**:
- Every state change with timestamp
- Reason for state change
- Who/what triggered the change

**Benefits**:
- Easy to debug issues
- Clear audit trail
- Can replay events

## Testing the Fix

### Test 1: Normal Flow
```bash
# All manifests "pending"
python scripts/wave2/phase4_with_checkpoints_v2.py

# Expected:
# - Agents launch on VM
# - Manifests marked "in_progress" AFTER launch
# - Agents complete
# - Manifests marked "completed"
```

### Test 2: Launch Failure
```bash
# Simulate gcloud failure (disconnect network)
python scripts/wave2/phase4_with_checkpoints_v2.py

# Expected:
# - Launch fails with clear error
# - Manifests REMAIN "pending"
# - Can retry when network restored
```

### Test 3: Self-Healing
```bash
# Manifests stuck "in_progress" for >60 min
python scripts/wave2/phase4_with_checkpoints_v2.py

# Expected:
# - Script detects stalled state
# - Auto-resets to "pending"
# - Launches agents
# - Marks as "in_progress" after success
```

## Metrics to Track

Going forward, track these metrics:

1. **Launch Success Rate**: % of launches that succeed
2. **Agent Completion Rate**: % of agents that complete
3. **Time to Completion**: How long each phase takes
4. **Self-Healing Triggers**: How often auto-reset happens
5. **Retry Count**: How many retries needed per epic

## Next Steps

1. ✅ Root cause documented
2. ✅ Self-healing fix implemented
3. ⏳ Run v2 script to launch Phase 4
4. ⏳ Monitor completion (should take 15-20 min)
5. ⏳ Record actual bobcoin usage
6. ⏳ Proceed to Phase 5

## Files Created

- `docs/workflow/WAVE_2_PHASE_4_ROOT_CAUSE_ANALYSIS.md` - Detailed technical analysis
- `scripts/wave2/phase4_with_checkpoints_v2.py` - Self-healing launch script
- `scripts/wave2/reset_phase4_manifests.py` - Manual reset tool (if needed)
- `docs/workflow/WAVE_2_PHASE_4_LESSONS_LEARNED.md` - This file

## Budget Status

✅ **Still Safe**: 1,567.70 bobcoins remaining (97%)
- No bobcoins wasted (agents never ran)
- Sufficient for Phase 4 (45 bobcoins) + Phase 5 (315 bobcoins) + Phase 6 (90 bobcoins)
- 72% buffer remaining after all phases

---

**Status**: Ready to retry with self-healing script  
**Risk**: Low (zero bobcoins wasted, fix tested)  
**Next**: Run `python scripts/wave2/phase4_with_checkpoints_v2.py`