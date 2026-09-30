# Cost-Optimized Polling Protocol V2

**Version**: 2.0
**Effective**: 2026-06-15
**Supersedes**: COST_OPTIMIZED_POLLING_PROTOCOL.md (V1.0 - 3 min intervals)

---

## Protocol Change

**V1.0 (Old)**: 1 min after first launch, then every 3 min
**V2.0 (New)**: 1 min after first launch, then every 4 min

**Rationale**: Further cost optimization while maintaining adequate monitoring coverage.

---

## Polling Schedule

### Initial Check
**Timing**: 1 minute after FIRST script launch
**Purpose**: Verify launch started successfully
**Actions**:
- Check screen sessions exist
- Verify first few files being created
- Confirm no immediate errors

### Subsequent Checks
**Timing**: Every 4 minutes after initial check
**Purpose**: Monitor progress without excessive API calls
**Actions**:
- Count screen sessions (active agents)
- Count files created (completion progress)
- Extract bobcoin usage (budget tracking)
- Check for errors (failure detection)

---

## Example Timeline

**Scenario**: 80 epics, 25 min execution time per epic

| Time | Event | Action |
|------|-------|--------|
| T+0:00 | First script launches | Start timer |
| T+1:00 | **Check #1** | Initial verification |
| T+5:00 | **Check #2** | Progress update |
| T+9:00 | **Check #3** | Progress update |
| T+13:00 | **Check #4** | Progress update |
| T+17:00 | **Check #5** | Progress update |
| T+21:00 | **Check #6** | Progress update |
| T+25:00 | **Check #7** | Final verification |

**Total Checks**: 7 (vs 9 with 3-min intervals)
**Cost Reduction**: 22% fewer checks vs V1.0
**Coverage**: Still adequate for 25-min execution window

---

## Cost Analysis

### V1.0 (3-min intervals)
- Initial: 1 min
- Subsequent: Every 3 min
- Total checks (25 min): 1 + 8 = 9 checks
- API calls per check: 3-4
- Total API calls: ~30

### V2.0 (4-min intervals)
- Initial: 1 min
- Subsequent: Every 4 min
- Total checks (25 min): 1 + 6 = 7 checks
- API calls per check: 3-4
- Total API calls: ~24

**Savings**: 20% fewer API calls (6 saved)
**Cost Impact**: Minimal (monitoring is cheap)
**Risk**: None (4-min intervals still provide adequate coverage)

---

## Monitoring Commands

### Standard Check (All-in-One)

```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="echo '=== LAUNCH ===' && tail -3 launch_phase2.log && \
             echo '=== SESSIONS ===' && screen -ls | grep -c 'p2-' && \
             echo '=== FILES ===' && ls docs/brain/EPIC-CCN-*/02-architecture-plan.md 2>/dev/null | wc -l"
```

### Detailed Check (When Issues Detected)

```bash
# Check for errors
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="grep -i 'error\|failed' logs/phase2/*.log | head -20"

# Extract bobcoin usage
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="grep -E 'Cost:|Balance:' logs/phase2/*.log | head -20"

# Check file sizes
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="ls -lh docs/brain/EPIC-CCN-*/02-architecture-plan.md | head -10"
```

---

## When to Deviate

### Increase Frequency (2-min intervals)
**Trigger**: >10% failure rate detected
**Reason**: Need tighter monitoring to catch cascading failures
**Duration**: Until failure rate drops below 5%

### Decrease Frequency (5-min intervals)
**Trigger**: 100% success rate after 3 consecutive checks
**Reason**: System stable, can reduce monitoring overhead
**Duration**: Remainder of wave

### Stop Monitoring
**Trigger**: All epics complete (file count = total epics)
**Reason**: No further monitoring needed
**Action**: Create completion report

---

## Integration with Wave Execution

### Phase Launch
1. Launch master script (e.g., `launch_phase2_all.sh`)
2. Record launch start time
3. **Wait 1 minute**
4. Run initial check
5. **Wait 4 minutes**
6. Run subsequent check
7. Repeat step 5-6 until complete

### Automation
```bash
# Example monitoring loop
LAUNCH_TIME=$(date +%s)
sleep 60  # Initial 1-min wait

while true; do
    # Run check
    FILES=$(gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
            --command="ls docs/brain/EPIC-CCN-*/02-architecture-plan.md 2>/dev/null | wc -l")
    
    echo "[$(date)] Files created: $FILES/80"
    
    # Exit if complete
    if [ "$FILES" -eq 80 ]; then
        echo "All epics complete!"
        break
    fi
    
    # Wait 4 minutes
    sleep 240
done
```

---

## Success Criteria

### Per Check
- ✅ Command executes successfully (exit code 0)
- ✅ Data returned (not empty)
- ✅ Progress increasing (file count going up)
- ✅ No blocking errors detected

### Overall
- ✅ All epics complete within expected time
- ✅ Success rate ≥90%
- ✅ Bobcoin usage within budget
- ✅ No P0 blockers

---

## Comparison: V1.0 vs V2.0

| Metric | V1.0 (3 min) | V2.0 (4 min) | Change |
|--------|--------------|--------------|--------|
| **Initial Check** | 1 min | 1 min | Same |
| **Interval** | 3 min | 4 min | +33% |
| **Total Checks (25 min)** | 9 | 7 | -22% |
| **API Calls** | ~30 | ~24 | -20% |
| **Cost** | Baseline | -20% | Savings |
| **Coverage** | Excellent | Good | Acceptable |
| **Risk** | Very Low | Low | Acceptable |

---

## Migration Guide

### For Existing Waves
1. Update custom mode polling interval: 3 → 4 minutes
2. Update monitoring scripts: `sleep 180` → `sleep 240`
3. Update documentation references
4. Test with next wave launch

### For New Waves
1. Use V2.0 protocol from start
2. Reference this document in wave handoff
3. Update wave execution guides

---

## References

- **V1.0 Protocol**: `docs/protocol/COST_OPTIMIZED_POLLING_PROTOCOL.md` (deprecated)
- **Custom Mode**: `.bob/custom_modes.yaml` (autonomous-refactor)
- **Wave Execution Skill**: `.bob/skills/gcp-vm-wave-execution/skill.md`
- **10-Phase SOP**: `docs/workflow/V12_EPIC_WORKFLOW_10_PHASE_SOP.md`

---

**Document Status**: ACTIVE
**Version**: 2.0
**Last Updated**: 2026-06-15
**Maintainer**: V12 Orchestration Team
**Change Log**:
- 2026-06-15: V2.0 - Changed interval from 3 min to 4 min (20% cost reduction)
- 2026-06-13: V1.0 - Initial protocol (3 min intervals, 88% cost reduction vs 30s)