# Wave 2 Phase 4 - Continuation Prompt

**Copy and paste this into your next session to continue Wave 2 execution.**

---

I'm continuing Wave 2 autonomous execution. We just completed setup for Phase 4 (Ticket Generation).

## Current Status

### Wave 2 v4 Complete (Phases 0-3)
- **Completed**: 2026-06-12 19:27 UTC
- **Result**: All 9 epics completed successfully
- **Actual Usage**: 3.23 bobcoins per epic (29.07 total)
- **Remaining**: 1,567.70 bobcoins

### Phase 4 Ready to Launch
- **Estimated Cost**: 45 bobcoins (5 per epic × 9)
- **Available**: 1,567.70 bobcoins
- **After Phase 4**: 1,522.70 bobcoins
- **Status**: ✅ All systems ready

### Tools Created
1. **API Balance Tracker**: `scripts/wave2/api_balance_tracker.py`
   - Automated bobcoin tracking (no manual dashboard checks)
   - Current state: `docs/workflow/api_balance_state.json`
   
2. **Checkpoint System**: 
   - Launch: `scripts/wave2/phase4_with_checkpoints.py`
   - Monitor: `scripts/wave2/monitor_phase4.py`
   - Manifest-based state tracking (can resume from failures)

3. **Documentation**: `docs/workflow/WAVE_2_PHASE_4_READY.md`

### Manifest Fix Applied
All 9 epic manifests now have Phase 4 entries. The launch script handles missing phases gracefully.

## Your Tasks

### 1. Launch Phase 4
```bash
python scripts/wave2/phase4_with_checkpoints.py
```

This will:
- Upload orchestrator script to VM
- Launch 9 screen sessions (one per epic)
- Each agent runs: `bob --chat-mode plan --max-coins 5`
- Generate `04-tickets.md` for each epic

**Expected Timeline**: 5-10 minutes (based on Phase 0-3 being 3.23 bobcoins)

### 2. Monitor Execution
```bash
# Check every 2 minutes
python scripts/wave2/monitor_phase4.py
```

This shows:
- Which agents are still running
- Which have completed (DONE_EXIT=0)
- Updates manifests automatically

### 3. Record Actual Usage
After all agents complete, check IBM Bob dashboard for actual bobcoin usage:

```bash
# Example: If EPIC-CCN-107 used 4.8 bobcoins
python scripts/wave2/api_balance_tracker.py record "bob.json" "EPIC-CCN-107" 4.8 "4"

# Repeat for all 9 epics, then check summary
python scripts/wave2/api_balance_tracker.py summary
```

### 4. Verify Completion
```bash
# Check all manifests updated
python scripts/wave2/monitor_phase4.py

# Verify all have 04-tickets.md
ls docs/brain/EPIC-CCN-*/04-tickets.md
```

## Success Criteria

Phase 4 is complete when:
- ✅ All 9 epics have `04-tickets.md`
- ✅ All manifests show phase 4 status = "completed"
- ✅ All agents returned DONE_EXIT=0
- ✅ Actual usage ~5 bobcoins per epic
- ✅ No API went negative

## Next Phases

### Phase 5: Implementation (Most Expensive)
- **Estimated**: 35 bobcoins per epic (315 total)
- **Available After Phase 4**: ~1,523 bobcoins
- **Status**: ✅ Sufficient budget

### Phase 6: Final Review
- **Estimated**: 10 bobcoins per epic (90 total)
- **Available After Phase 5**: ~1,208 bobcoins
- **Status**: ✅ Sufficient budget

## Quick Reference

### Check API Balances
```bash
python scripts/wave2/api_balance_tracker.py summary
```

### Check Phase Feasibility
```bash
python scripts/wave2/api_balance_tracker.py check 5 9  # Phase 5
python scripts/wave2/api_balance_tracker.py check 6 9  # Phase 6
```

### VM Status
```bash
gcloud compute instances list --filter="name=v12-test-golden-v2"
```

### Screen Sessions
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="screen -ls"
```

## Epic List (Wave 2)

| Epic ID | Method | Complexity | API |
|---------|--------|------------|-----|
| EPIC-CCN-107 | ProcessIpcCommands | 76 | bob.json |
| EPIC-CCN-108 | ProcessOnExecutionUpdate | 67 | bob (1).json |
| EPIC-CCN-109 | HydrateFSMsFromWorkingOrders | 45 | bob (2).json |
| EPIC-CCN-110 | HandleFlatPositionUpdate | 37 | bob (3).json |
| EPIC-CCN-111 | AdoptFleetOrders | 37 | bob (4).json |
| EPIC-CCN-112 | ExtractTargetConfiguration | 31 | bob (5).json |
| EPIC-CCN-113 | SweepBrokerOrders | 28 | bob (6).json |
| EPIC-CCN-114 | FlattenSinglePosition | 27 | b.json |
| EPIC-CCN-115 | ExecuteRetestEntry | 26 | b (2).json |

## Key Files

- **API State**: `docs/workflow/api_balance_state.json`
- **Ready Guide**: `docs/workflow/WAVE_2_PHASE_4_READY.md`
- **Manifests**: `docs/brain/EPIC-CCN-*/manifest.json`
- **Logs**: `logs/phase4/EPIC-CCN-*.log` (on VM)

## Troubleshooting

### If gcloud not found
The launch script requires `gcloud` CLI. Either:
- Run from environment where gcloud is installed
- Or modify script to use full path to gcloud

### If agent fails mid-execution
1. Check log: `logs/phase4/EPIC-CCN-XXX.log` (on VM)
2. Check manifest: `docs/brain/EPIC-CCN-XXX/manifest.json`
3. Fix issue if needed
4. Reset manifest: Change `"failed"` to `"pending"`
5. Re-run launch script (only failed epic will launch)

### If bobcoin usage higher than expected
- Check dashboard for actual usage
- Update estimates in `api_balance_tracker.py`
- Verify safety margin still >10%

---

**Status**: ✅ Ready to Launch Phase 4  
**Next Action**: Run `python scripts/wave2/phase4_with_checkpoints.py`  
**Last Updated**: 2026-06-12 19:58 UTC