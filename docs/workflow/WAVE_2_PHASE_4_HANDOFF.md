# Wave 2 Phase 4 - Handoff for VM Execution

**Date**: 2026-06-12 20:31 UTC  
**Status**: Ready for execution by user with gcloud CLI access

## Current State

✅ **All Systems Ready**:
- All 9 epic manifests reset to "pending"
- Self-healing launch script created and tested
- Launch script built and ready at `/tmp/wave2_phase4_v2.sh`
- Budget allocated: 45 bobcoins (1,567.70 remaining)

## What Needs to Happen

Someone with `gcloud` CLI access needs to run:

```bash
python scripts/wave2/phase4_with_checkpoints_v2.py
```

This will:
1. Upload launch script to VM
2. Execute script to start 9 agents in screen sessions
3. Mark manifests as "in_progress" ONLY after successful launch
4. Agents will generate tickets in 15-20 minutes

## Why This Is Safe

The v2 script has self-healing built in:

1. **Proper State Management**: Marks "in_progress" ONLY after successful launch
2. **Graceful Failure**: If launch fails, manifests stay "pending" (safe to retry)
3. **Auto-Healing**: If agents stall >60 min, auto-resets to "pending"
4. **Idempotent**: Safe to run multiple times

## Monitoring After Launch

### Check Status
```bash
python scripts/wave2/check_phase4_local.py
```

### Check VM Agents
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="screen -ls"
```

### Check Logs
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="tail -f logs/phase4/EPIC-CCN-107.log"
```

## Expected Timeline

- **Launch**: <1 minute
- **Execution**: 15-20 minutes per epic
- **Total**: ~20 minutes for all 9 epics to complete

## Success Criteria

Phase 4 complete when:
- ✅ All 9 manifests show `"status": "completed"`
- ✅ All 9 epics have `04-tickets.md` files
- ✅ All agents returned DONE_EXIT=0 in logs
- ✅ Actual usage ~5 bobcoins per epic (45 total)

## After Completion

1. **Verify Tickets**:
   ```bash
   ls docs/brain/EPIC-CCN-*/04-tickets.md
   ```

2. **Record Usage**:
   Check IBM Bob dashboard for actual bobcoin usage per API:
   ```bash
   python scripts/wave2/api_balance_tracker.py record "bob.json" "EPIC-CCN-107" <actual> "4"
   # Repeat for all 9 epics
   ```

3. **Check Summary**:
   ```bash
   python scripts/wave2/api_balance_tracker.py summary
   ```

4. **Proceed to Phase 5**:
   Phase 5 (Implementation) is the most expensive phase:
   - Estimated: 35 bobcoins per epic (315 total)
   - Available after Phase 4: ~1,523 bobcoins
   - Status: ✅ Sufficient budget

## What We Fixed

### Original Bug
Script marked manifests as "in_progress" BEFORE launching agents. When gcloud wasn't available, launch failed but manifests were stuck "in_progress".

### The Fix
v2 script marks manifests as "in_progress" ONLY AFTER successful launch. If launch fails, manifests stay "pending" for safe retry.

### Self-Healing
If manifests are "in_progress" for >60 minutes, v2 script auto-resets them to "pending" and retries.

## Files Created

1. `scripts/wave2/phase4_with_checkpoints_v2.py` - Self-healing launch script
2. `scripts/wave2/reset_phase4_manifests.py` - Manual reset tool
3. `scripts/wave2/check_phase4_local.py` - Local status checker
4. `docs/workflow/WAVE_2_PHASE_4_ROOT_CAUSE_ANALYSIS.md` - Technical analysis
5. `docs/workflow/WAVE_2_PHASE_4_LESSONS_LEARNED.md` - Learnings & recovery
6. `docs/workflow/WAVE_2_PHASE_4_HANDOFF.md` - This file

## Budget Status

✅ **Safe**: 1,567.70 bobcoins remaining (97%)
- Phase 4: 45 bobcoins
- Phase 5: 315 bobcoins
- Phase 6: 90 bobcoins
- **Total Remaining**: 450 bobcoins needed
- **Buffer**: 1,117.70 bobcoins (70% safety margin)

## Contact

If issues arise:
1. Check `docs/workflow/WAVE_2_PHASE_4_LESSONS_LEARNED.md` for troubleshooting
2. Check `docs/workflow/WAVE_2_PHASE_4_ROOT_CAUSE_ANALYSIS.md` for technical details
3. Manifests are in safe "pending" state - can retry anytime

---

**Status**: ✅ Ready for execution  
**Next**: Run `python scripts/wave2/phase4_with_checkpoints_v2.py` from environment with gcloud CLI  
**Risk**: Low (self-healing, safe retries, sufficient budget)