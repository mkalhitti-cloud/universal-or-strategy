# Wave 2 Launch Plan - Sequential Execution

**Status**: Ready to Launch  
**Date**: 2026-06-12  
**Approval**: ✅ User approved

## Executive Summary

Wave 2 will execute **10 epics** using **sequential execution** due to GCP vCPU quota limitations. Each VM will launch, execute its epic, and stop before the next VM launches.

## GCP Quota Analysis

### Current Quota
- **Global vCPU limit**: 12 vCPUs
- **n2-standard-8**: 8 vCPUs per VM
- **Maximum concurrent VMs**: 1 VM (12 ÷ 8 = 1.5)

### Parallel Execution Requirements
To run 10 VMs in parallel:
- **Required vCPUs**: 80 (10 VMs × 8 vCPUs)
- **Current quota**: 12 vCPUs
- **Shortfall**: 68 vCPUs
- **Quota increase time**: 24-48 hours

### Decision: Sequential Execution
✅ **Chosen approach**: Launch VMs one at a time
- No quota increase needed
- Immediate execution
- Zero risk of quota errors
- Slightly longer total time (~5 hours vs ~30 minutes)

## Wave 2 Epic List

| # | Epic ID | Target Method | Current CYC | Target CYC |
|---|---------|---------------|-------------|------------|
| 1 | EPIC-CCN-164 | IsCommandForThisInstrument | 36 | 8 |
| 2 | EPIC-CCN-107 | OnBarUpdate | 28 | 8 |
| 3 | EPIC-CCN-108 | OnOrderUpdate | 26 | 8 |
| 4 | EPIC-CCN-109 | OnExecutionUpdate | 24 | 8 |
| 5 | EPIC-CCN-110 | OnPositionUpdate | 22 | 8 |
| 6 | EPIC-CCN-111 | OnAccountItemUpdate | 21 | 8 |
| 7 | EPIC-CCN-112 | ProcessMarketData | 20 | 8 |
| 8 | EPIC-CCN-113 | ValidateOrderParameters | 20 | 8 |
| 9 | EPIC-CCN-114 | CalculatePositionSize | 19 | 8 |
| 10 | EPIC-CCN-115 | UpdateRiskMetrics | 19 | 8 |

**Total complexity reduction**: 235 → 80 (66% reduction)

## Execution Workflow

### Per-Epic Workflow
```
1. Launch VM from golden image v2 (30 seconds)
2. Wait for VM boot (30 seconds)
3. Execute epic-intake via SSH (5-10 minutes)
4. Stop VM (10 seconds)
5. Repeat for next epic
```

### Timeline Estimate
- **Per epic**: ~10 minutes (including boot/shutdown)
- **Total time**: ~100 minutes (1 hour 40 minutes)
- **Parallel equivalent**: ~30 minutes (if quota allowed)
- **Time overhead**: +70 minutes (acceptable for zero-risk execution)

## Cost Analysis

### Per-VM Cost
- **Machine type**: n2-standard-8 SPOT
- **Rate**: $0.093/hour
- **Duration**: 10 minutes = 0.167 hours
- **Cost per VM**: $0.016

### Wave 2 Total Cost
- **VMs**: 10
- **Total runtime**: 100 minutes = 1.67 hours
- **Total cost**: $0.16 (10 VMs × $0.016)

### Cost Comparison
- **Sequential**: $0.16
- **Parallel (if quota allowed)**: $0.47 (10 VMs × 30 min × $0.093/hour)
- **Savings**: $0.31 (66% cheaper due to no idle time)

## Launch Script

**Script**: `scripts/launch_wave2_sequential.sh`

**Features**:
- ✅ Automatic VM creation from golden image v2
- ✅ SSH command execution with Bob Shell
- ✅ Automatic VM shutdown after completion
- ✅ Progress logging with timestamps
- ✅ Error handling and status reporting

**Usage**:
```bash
cd ~/universal-or-strategy
bash scripts/launch_wave2_sequential.sh
```

## Success Criteria

### Per-Epic Success
- ✅ VM launches successfully
- ✅ Bob Shell executes epic-intake
- ✅ Artifacts created in `docs/brain/EPIC-*/`
- ✅ VM stops cleanly

### Wave 2 Success
- ✅ All 10 epics complete Phase 0 (intake)
- ✅ All artifacts validated
- ✅ No VM quota errors
- ✅ Total cost < $0.20

## Risk Mitigation

### Identified Risks
1. **VM launch failure**: Golden image v2 tested and validated ✅
2. **Bob Shell authentication**: API key method tested ✅
3. **License acceptance**: `--accept-license` flag tested ✅
4. **SSH timeout**: 10-minute timeout sufficient for Phase 0 ✅
5. **Quota exhaustion**: Sequential execution eliminates risk ✅

### Rollback Plan
If any epic fails:
1. Review VM logs via `gcloud compute instances get-serial-port-output`
2. SSH into stopped VM for debugging
3. Fix issue and re-run single epic
4. Continue with remaining epics

## Post-Wave 2 Actions

### Immediate (after completion)
1. Review all 10 epic artifacts in `docs/brain/EPIC-*/`
2. Validate Phase 0 outputs (hotspot analysis)
3. Delete stopped VMs to avoid storage costs
4. Update roadmap with Wave 2 completion status

### Next Steps
1. **Wave 3 planning**: Select next 10 epics
2. **Quota increase request**: Submit for 80 vCPUs (optional)
3. **Phase 1 execution**: Run scope definition for Wave 2 epics
4. **Parallel optimization**: If quota increased, switch to parallel execution

## Approval Status

- ✅ **User approval**: Received 2026-06-12
- ✅ **Golden image validated**: v2 tested successfully
- ✅ **Script ready**: `launch_wave2_sequential.sh` created
- ✅ **Cost approved**: $0.16 total
- ✅ **Timeline approved**: ~100 minutes

**Status**: READY TO LAUNCH

## Launch Command

```bash
# From local machine (not VM)
cd ~/universal-or-strategy
bash scripts/launch_wave2_sequential.sh
```

**Expected output**: 10 VMs created, 10 epics processed, 10 VMs stopped, total time ~100 minutes.