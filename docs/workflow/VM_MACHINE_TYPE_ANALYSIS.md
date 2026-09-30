# VM Machine Type Analysis for Bob Shell Execution

**Date**: 2026-06-12  
**Purpose**: Determine optimal machine type for pre-baked VM image

## Machine Type Comparison

### n2-standard-8 (Current)
- **vCPUs**: 8
- **RAM**: 32 GB
- **Cost**: $0.093/hour (spot) | $0.40/hour (on-demand)
- **Use Case**: 8 parallel epics (1 epic per vCPU)

### n2-standard-12 (Proposed)
- **vCPUs**: 12
- **RAM**: 48 GB
- **Cost**: $0.14/hour (spot) | $0.60/hour (on-demand)
- **Use Case**: 12 parallel epics (1 epic per vCPU)
- **Cost Delta**: +$0.047/hour spot (+50% more expensive)

### n2-standard-16 (Maximum Parallel)
- **vCPUs**: 16
- **RAM**: 64 GB
- **Cost**: $0.186/hour (spot) | $0.80/hour (on-demand)
- **Use Case**: 16 parallel epics (1 epic per vCPU)
- **Cost Delta**: +$0.093/hour spot (+100% more expensive)

## Bob Shell Performance Considerations

### CPU Requirements
**Bob Shell is single-threaded per task**:
- Each epic execution = 1 Bob Shell process
- Bob Shell uses ~1 vCPU per active task
- More vCPUs = more parallel epics, NOT faster per-epic execution

### Memory Requirements
**Per Epic Estimation**:
- Bob Shell process: ~500 MB
- Python orchestrator: ~200 MB
- Git operations: ~100 MB
- **Total per epic**: ~800 MB

**Memory Capacity**:
- 8 vCPUs (32 GB): 32,000 MB ÷ 8 = 4,000 MB per epic ✅ (5x headroom)
- 12 vCPUs (48 GB): 48,000 MB ÷ 12 = 4,000 MB per epic ✅ (5x headroom)
- 16 vCPUs (64 GB): 64,000 MB ÷ 16 = 4,000 MB per epic ✅ (5x headroom)

**Verdict**: Memory is NOT a constraint for any option.

### Disk I/O
**Bob Shell Disk Usage**:
- Repository clone: ~500 MB
- Build artifacts: ~200 MB per epic
- Logs: ~50 MB per epic
- **Total**: ~1 GB per epic

**Disk Capacity**:
- 100 GB SSD = enough for 100 epics
- **Verdict**: Disk is NOT a constraint.

## Wave 2 Workload Analysis

### Current Wave 2 Scope
- **Total epics**: 10
- **Optimal machine**: n2-standard-8 (8 vCPUs)
- **Execution strategy**: 8 epics in parallel, then 2 epics
- **Total time**: ~35 minutes (30 min first batch + 5 min second batch)

### If Using n2-standard-12
- **Total epics**: 10
- **Execution strategy**: All 10 epics in parallel
- **Total time**: ~30 minutes (single batch)
- **Time saved**: 5 minutes
- **Cost increase**: $0.047/hour × 0.5 hours = $0.024 extra
- **ROI**: Save 5 minutes for $0.024 = **NOT WORTH IT**

### If Using n2-standard-16
- **Total epics**: 10
- **Execution strategy**: All 10 epics in parallel (6 vCPUs idle)
- **Total time**: ~30 minutes (same as 12 vCPUs)
- **Time saved**: 5 minutes
- **Cost increase**: $0.093/hour × 0.5 hours = $0.047 extra
- **ROI**: Save 5 minutes for $0.047 = **DEFINITELY NOT WORTH IT**

## Future Wave Analysis

### Complete Roadmap Scope
- **Total epics**: 165 (from roadmap)
- **Waves**: 165 ÷ 8 = 21 waves (n2-standard-8)
- **Waves**: 165 ÷ 12 = 14 waves (n2-standard-12)
- **Waves**: 165 ÷ 16 = 11 waves (n2-standard-16)

### Cost Comparison (Full Roadmap)

**n2-standard-8**:
- Waves: 21
- Time per wave: 30 minutes
- Total time: 21 × 0.5 = 10.5 hours
- Total cost: 10.5 × $0.093 = $0.98

**n2-standard-12**:
- Waves: 14
- Time per wave: 30 minutes
- Total time: 14 × 0.5 = 7 hours
- Total cost: 7 × $0.14 = $0.98
- **Time saved**: 3.5 hours
- **Cost delta**: $0 (same cost!)

**n2-standard-16**:
- Waves: 11
- Time per wave: 30 minutes
- Total time: 11 × 0.5 = 5.5 hours
- Total cost: 5.5 × $0.186 = $1.02
- **Time saved**: 5 hours
- **Cost delta**: +$0.04 (4% more expensive)

## Recommendation

### For Wave 2 Only: n2-standard-8 ✅
**Rationale**:
- 10 epics fit well in 2 batches (8 + 2)
- Cheapest option ($0.093/hour)
- Only 5 minutes slower than larger machines
- Not worth paying 50% more for 5 minutes

### For Full Roadmap (165 epics): n2-standard-12 ✅✅✅
**Rationale**:
- **Same total cost** as n2-standard-8 ($0.98)
- **3.5 hours faster** (7 hours vs 10.5 hours)
- Better resource utilization (fewer waves)
- More headroom for future scaling

### For Maximum Speed: n2-standard-16
**Rationale**:
- Only 4% more expensive than n2-standard-8
- 5 hours faster than n2-standard-8
- Best for time-critical scenarios
- Overkill for current needs

## Final Decision Matrix

| Scenario | Machine Type | Rationale |
|----------|--------------|-----------|
| **Wave 2 only** | n2-standard-8 | Cheapest, good enough |
| **Full roadmap** | n2-standard-12 | Same cost, 3.5 hours faster |
| **Time-critical** | n2-standard-16 | 5 hours faster, only 4% more |
| **Budget-critical** | n2-standard-8 | Absolute cheapest |

## Recommendation for Pre-Baked Image

**Use n2-standard-12** for the following reasons:

1. **Future-proof**: Handles full roadmap efficiently
2. **Cost-neutral**: Same total cost as n2-standard-8 for full roadmap
3. **Time savings**: 3.5 hours faster for full roadmap
4. **Flexibility**: Can run 12 epics in parallel or 8 epics with 4 vCPUs spare
5. **Headroom**: Extra vCPUs useful for monitoring, logging, or other tasks

## Implementation Plan

1. Delete v3 (n2-standard-8)
2. Create new VM with n2-standard-12
3. Manually install Bob Shell
4. Clone repository on main branch
5. Test with 2-epic validation
6. Create image snapshot
7. Use image for all future waves

## Cost Impact

**Wave 2** (10 epics):
- n2-standard-8: $0.047 (0.5 hours)
- n2-standard-12: $0.070 (0.5 hours)
- **Delta**: +$0.023 (49% more)

**Full Roadmap** (165 epics):
- n2-standard-8: $0.98 (10.5 hours)
- n2-standard-12: $0.98 (7 hours)
- **Delta**: $0 (same cost, 3.5 hours faster)

**Verdict**: Pay $0.023 more for Wave 2, save 3.5 hours on full roadmap at no extra cost.