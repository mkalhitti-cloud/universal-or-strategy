# Parallel Execution SOP (V12.25)

## Purpose
Standard Operating Procedure for parallel execution in Wave 2+ autonomous refactor workflows.

## Critical Lesson: Wave 2 Phase 6 Sequential Error

**What Happened**: Phase 6 ran sequentially instead of parallel (21 min vs ~7-10 min estimated)

**Root Cause**: Used `wait_for_completion()` pattern which waits for each epic before starting next:
```python
# ❌ WRONG - Sequential execution
for epic in epics:
    launch_epic(epic)
    wait_for_completion(epic)  # Blocks until epic completes
    check_status(epic)
```

**Correct Pattern**: Use ThreadPoolExecutor to launch all epics simultaneously:
```python
# ✅ CORRECT - Parallel execution
from concurrent.futures import ThreadPoolExecutor, as_completed

def run_epic_phase(epic_id):
    """Execute single epic phase (Phase 5 or Phase 6)"""
    screen_name = f"p6_{epic_id}"
    script_path = f"_p6_{epic_id}.sh"
    
    # Launch in screen session
    cmd = f"screen -dmS {screen_name} bash -l {script_path}"
    subprocess.run(cmd, shell=True, check=True)
    
    # Wait for this epic to complete
    while screen_exists(screen_name):
        time.sleep(30)
    
    # Check status
    return check_epic_status(epic_id)

# Launch all epics in parallel with 3 workers
epics = ["EPIC-CCN-107", "EPIC-CCN-108", ..., "EPIC-CCN-114"]
with ThreadPoolExecutor(max_workers=3) as executor:
    futures = {executor.submit(run_epic_phase, epic): epic for epic in epics}
    
    for future in as_completed(futures):
        epic = futures[future]
        try:
            result = future.result()
            print(f"✅ {epic} completed: {result}")
        except Exception as e:
            print(f"❌ {epic} failed: {e}")
```

## Wave 2 Design Rationale

**Why 3 Workers?**
- VM has 4 vCPUs (n2-standard-4)
- Reserve 1 vCPU for system overhead
- 3 parallel workers = optimal CPU utilization
- Expected speedup: 2-3x vs sequential

**Worker Pool Strategy**:
- Phase 5: 3 workers for ticket execution (gated per epic)
- Phase 6: 3 workers for epic reviews (independent)
- Each worker gets dedicated screen session
- Logs isolated per worker

## Implementation Checklist

When creating autonomous execution scripts:

- [ ] Import ThreadPoolExecutor and as_completed
- [ ] Define worker function that handles single epic/ticket
- [ ] Set max_workers=3 (or adjust based on VM specs)
- [ ] Use as_completed() to process results as they finish
- [ ] Add exception handling per worker
- [ ] Log start/completion timestamps per worker
- [ ] Verify screen sessions are isolated (unique names)
- [ ] Test with 2 epics first, then scale to full batch

## Testing Parallel Execution

**Smoke Test** (2 epics):
```bash
# Launch 2 epics in parallel
python test_parallel_execution.py --epics EPIC-CCN-111,EPIC-CCN-113

# Verify both screen sessions running
screen -ls | grep -E "p6_111|p6_113"

# Monitor logs in parallel
tail -f logs/phase6_111.log &
tail -f logs/phase6_113.log &
```

**Full Test** (7 epics):
```bash
# Launch all 7 epics with 3 workers
python launch_phase6_parallel.py

# Verify 3 active workers at any time
watch -n 5 'screen -ls | grep p6_'

# Expected: 3 running, 4 queued initially
# As workers complete, queue drains
```

## Performance Metrics

**Sequential Baseline** (Wave 2 Phase 6 actual):
- 7 epics × 3 min avg = 21 minutes total
- CPU utilization: ~25% (1 core active)

**Parallel Target** (3 workers):
- 7 epics ÷ 3 workers = 3 batches
- Batch 1: 3 epics × 3 min = 9 min
- Batch 2: 3 epics × 3 min = 9 min (overlaps with Batch 1 tail)
- Batch 3: 1 epic × 3 min = 3 min
- **Total: ~7-10 minutes** (2-3x speedup)
- CPU utilization: ~75% (3 cores active)

## Error Handling

**Worker Failure Scenarios**:

1. **Single Worker Fails**:
   - Other workers continue
   - Failed epic logged with exception
   - Retry logic optional (depends on failure type)

2. **Multiple Workers Fail**:
   - Remaining workers complete
   - Generate failure report
   - Manual intervention required

3. **VM Resource Exhaustion**:
   - Reduce max_workers to 2
   - Add memory monitoring
   - Implement backpressure (queue depth limit)

## Monitoring Commands

**Check Active Workers**:
```bash
screen -ls | grep -c "p6_"  # Count active Phase 6 workers
```

**Monitor CPU Usage**:
```bash
top -b -n 1 | grep "Cpu(s)"  # Should show ~75% with 3 workers
```

**Check Worker Progress**:
```bash
for epic in 107 108 109 111 112 113 114; do
    echo "EPIC-CCN-$epic: $(tail -1 logs/phase6_$epic.log)"
done
```

## Integration with Obsidian Kanban

**Git Hooks** (automatic updates):
- `post-commit`: Updates kanban after each commit
- `post-merge`: Updates kanban after merging changes
- Runs in background (non-blocking)

**Manual Update**:
```bash
python scripts/wave2/update_wave2_kanban.py
```

**File Watcher** (near real-time):
```bash
scripts/wave2/start_kanban_watcher.bat
```

## Future Waves

**Wave 3+ Scaling**:
- If VM upgraded to 8 vCPUs: increase to 6-7 workers
- If epic count >20: consider batching (10 epics per wave)
- If ticket count >100: implement priority queue

**Optimization Opportunities**:
- Pre-warm Bob CLI sessions (reduce startup overhead)
- Cache jCodemunch index (reduce API calls)
- Parallel Phase 0-4 (currently sequential by design)

## References

- **Wave 2 Completion Report**: `WAVE2_FINAL_STATUS_CORRECTED.md`
- **Sequential Error Analysis**: Lines 89-103 in completion report
- **Python ThreadPoolExecutor**: https://docs.python.org/3/library/concurrent.futures.html
- **Screen Session Management**: `man screen`

## Enforcement

**MANDATORY**: All future autonomous execution scripts MUST use parallel execution pattern.

**Violation**: Using sequential `wait_for_completion()` loop is a protocol violation.

**Review Gate**: Code review must verify ThreadPoolExecutor usage before merge.

---

**Version**: V12.25  
**Effective**: 2026-06-13  
**Author**: Bob (Orchestrator)  
**Status**: ACTIVE