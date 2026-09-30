# API Budget Tracking System

**Critical**: Each API has 160 bobcoins. Going negative requires emailing IBM support for each API.

## Current API Inventory (Updated 2026-06-12)

| API # | File | Initial Balance | Used | Current Balance | Status |
|-------|------|-----------------|------|-----------------|--------|
| 1 | bob.json | 160 | 0 | 160 | ✅ Available |
| 2 | bob (1).json | 160 | 0 | 160 | ✅ Available |
| 3 | bob (2).json | 160 | 0 | 160 | ✅ Available |
| 4 | bob (3).json | 160 | 0 | 160 | ✅ Available |
| 5 | bob (4).json | 160 | 0 | 160 | ✅ Available |
| 6 | bob (5).json | 160 | 0 | 160 | ✅ Available |
| 7 | bob (6).json | 160 | 0 | 160 | ✅ Available |
| 8 | b.json | 160 | 0 | 160 | ✅ Available |
| 9 | **b (2).json** | 160 | 0 | 160 | ✅ **FRESH (Replaced b (1).json)** |
| 10 | sean.carter.jr@atomicmail.io.json | 160 | 0 | 160 | ✅ Available |

**Total Budget**: 1,600 bobcoins available (10 fresh APIs)
**Replacement Note**: Old `b (1).json` (used 78.80 bobcoins) was deleted and replaced with fresh `b (2).json` on 2026-06-12

## Wave 2 Budget Analysis

### Original Plan (FAILED - Would Go Negative)
- **9 epics** × 200 bobcoins each = 1,800 bobcoins needed
- **Problem**: Exceeds total budget by 200 bobcoins
- **Result**: 2+ APIs would go negative → IBM support tickets required

### Revised Strategy (UPDATED - All APIs Fresh)

#### Selected Approach: Safe Budget with Reserve
- **Budget**: 150 bobcoins per epic
- **Total**: 9 epics × 150 = 1,350 bobcoins
- **Available**: 1,600 bobcoins (10 APIs × 160 each)
- **Safety Margin**: 250 bobcoins (15.6%)
- **Reserve**: API #10 (sean.carter.jr@atomicmail.io.json) kept for emergencies/Wave 3

## Wave 2 v4 Configuration

### Configuration
```python
MAX_COINS = "150"  # Safe per-epic budget
SAFETY_BUFFER = 10  # Keep 10 bobcoins minimum per API
```

### API Allocation for Wave 2 v4

| Epic ID | API # | File | Start Balance | Allocated | Reserve |
|---------|-------|------|---------------|-----------|---------|
| EPIC-CCN-107 | 1 | bob.json | 160 | 150 | 10 |
| EPIC-CCN-108 | 2 | bob (1).json | 160 | 150 | 10 |
| EPIC-CCN-109 | 3 | bob (2).json | 160 | 150 | 10 |
| EPIC-CCN-110 | 4 | bob (3).json | 160 | 150 | 10 |
| EPIC-CCN-111 | 5 | bob (4).json | 160 | 150 | 10 |
| EPIC-CCN-112 | 6 | bob (5).json | 160 | 150 | 10 |
| EPIC-CCN-113 | 7 | bob (6).json | 160 | 150 | 10 |
| EPIC-CCN-114 | 8 | b.json | 160 | 150 | 10 |
| EPIC-CCN-115 | 9 | b (2).json | 160 | 150 | 10 |
| **RESERVE** | 10 | sean.carter.jr@atomicmail.io.json | 160 | 0 | 160 |

**Total Allocated**: 1,350 bobcoins
**Total Reserve**: 250 bobcoins (90 from per-epic reserves + 160 from API #10)

### Execution Plan
1. **Pre-Flight Check**: ✅ All 10 APIs verified at 160 bobcoins
2. **Launch**: 9 agents with 150 bobcoins each (1 API per agent)
3. **Monitor**: Track bobcoin consumption per API during execution
4. **Post-Flight**: Document actual usage and remaining balance per API

### Budget Tracking Template

```markdown
## Wave 2 v4 Execution

**Start Time**: [timestamp]
**End Time**: [timestamp]

| Epic ID | API # | Start Balance | Used | End Balance | Status |
|---------|-------|---------------|------|-------------|--------|
| EPIC-CCN-107 | 1 | 160 | TBD | TBD | Running |
| EPIC-CCN-108 | 2 | 160 | TBD | TBD | Running |
| ... | ... | ... | ... | ... | ... |

**Total Used**: TBD / 1,350 bobcoins
**Reserve Remaining**: TBD / 250 bobcoins
```

## Protocol: Preventing Negative Balances

### Before Launch (MANDATORY CHECKLIST)
1. ✅ Verify each API has ≥160 bobcoins (check docs/API/*.json files)
2. ✅ Set MAX_COINS to safe value (150 recommended, never exceed 160)
3. ✅ Document initial balances in this file
4. ✅ Keep 1 API in reserve (API #10)
5. ✅ Ensure 1 API per agent (no sharing)
6. ✅ Calculate total budget vs available (must have 10%+ safety margin)

### During Execution
1. Monitor logs for bobcoin consumption warnings
2. If any agent approaches limit, stop immediately
3. Document actual usage per epic

### After Completion
1. Check final balance for each API
2. Document actual bobcoin consumption
3. Update this file with real usage data
4. Plan next wave based on actual consumption

## Emergency Stop Procedure

If any API approaches negative:
```bash
# Stop all agents immediately
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="bash /tmp/stop_wave2.sh"

# Check logs for bobcoin usage
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="grep -r 'bobcoins' /home/malhitticrypto/universal-or-strategy/logs/"
```

## Actual Usage Data (To Be Updated)

### Wave 2 v1 (Analysis Only)
- **Duration**: 3 minutes
- **Bobcoins Used**: ~50-100 total (shared API)
- **Per-Epic Average**: ~5-10 bobcoins

### Wave 2 v2 (Stopped - Wrong Architecture)
- **Duration**: <1 minute (stopped immediately)
- **Bobcoins Used**: Minimal (~0-5)

### Wave 2 v3 (Stopped - Budget Exceeded)
- **Duration**: <1 minute (stopped immediately)
- **Bobcoins Used**: Minimal (~0-5)

### Wave 2 v4 (Ready to Launch)
- **Budget**: 150 bobcoins per epic × 9 epics = 1,350 bobcoins
- **Available**: 1,600 bobcoins (10 fresh APIs)
- **Safety Margin**: 250 bobcoins (15.6%)
- **Expected Usage**: TBD (will update after execution)
- **API Strategy**: 1 unique API per agent (no sharing)

## Budget Management Workflow

### Step 1: Pre-Launch Verification
```bash
# Verify all API files exist and have correct structure
ls -lh docs/API/*.json

# Count available APIs
ls docs/API/*.json | wc -l  # Should be 10

# Check for any previously used APIs (manual review)
cat docs/workflow/API_BUDGET_TRACKING.md
```

### Step 2: Calculate Safe Budget
```python
# Formula: MAX_COINS = min(api_balance) - SAFETY_BUFFER
# Example: 160 - 10 = 150 bobcoins per epic

num_epics = 9
max_coins_per_epic = 150
total_budget = num_epics * max_coins_per_epic  # 1,350
total_available = 10 * 160  # 1,600
safety_margin = total_available - total_budget  # 250 (15.6%)

# Safety margin should be ≥10%
assert safety_margin / total_available >= 0.10
```

### Step 3: Launch with Budget Tracking
```python
# In launch script (launch_wave_v4_safe_budget.py):
MAX_COINS = "150"  # Per-epic budget
SAFETY_BUFFER = 10  # Minimum reserve per API

# Each agent gets unique API key
for i, (epic_id, api_file, api_key) in enumerate(zip(epics, api_files, api_keys)):
    export BOBSHELL_API_KEY='{api_key}'
    bob --max-coins {MAX_COINS} ...
```

### Step 4: Monitor During Execution
```bash
# SSH to VM and check logs for bobcoin warnings
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="grep -i 'bobcoin\|balance\|quota' /home/malhitticrypto/universal-or-strategy/logs/*.log"

# If any agent shows low balance warning, STOP IMMEDIATELY
python scripts/wave2/stop_wave2.sh
```

### Step 5: Post-Execution Tracking
```bash
# After wave completes, update this file with actual usage:
# 1. Check IBM Bob Shell dashboard for final balances
# 2. Calculate actual bobcoins used per epic
# 3. Document any APIs that went below 20 bobcoins
# 4. Plan next wave based on remaining balances
```

---

**Last Updated**: 2026-06-12 19:08 UTC
**Status**: ✅ Ready for Wave 2 v4 - All 10 APIs fresh with 160 bobcoins each
**Next Action**: Launch Wave 2 v4 with safe budget (150 bobcoins/epic)