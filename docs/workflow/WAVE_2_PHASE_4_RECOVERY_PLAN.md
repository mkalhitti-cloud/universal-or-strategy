# Wave 2 Phase 4 - Recovery Plan

**Date**: 2026-06-12 20:48 UTC
**Status**: Agents running with duplicate key - Recovery plan ready

## Current Situation

**Agents Running**: 9/9 on VM (launched 20:36 UTC, T+12 minutes)
**Issue**: EPIC-108 and EPIC-114 sharing `b.json` (duplicate key)
**Impact**: Likely stalled due to quota contention

## Root Cause

The v2 script used an improvised API allocation instead of copying the proven allocation from Wave 2 v4:

**Buggy Allocation (v2)**:
```
114 → b.json  ❌ (DUPLICATE with 108)
```

**Proven Allocation (Wave 2 v4)**:
```
114 → bob (6).json  ✅ (UNIQUE)
```

## Prevention Measures Implemented

### 1. Protocol Hardened in AGENTS.md
Added **Multi-Agent API Key Allocation Protocol (V12.25)**:
- Check previous success before ANY multi-agent launch
- Copy proven allocation exactly
- Validate for duplicates before launch
- Never improvise allocations

### 2. Fixed Script Created
`scripts/wave2/phase4_with_checkpoints_v3_fixed.py`:
- Uses proven allocation from Wave 2 v4
- Validates for duplicates before launch (MANDATORY)
- Documents source script in comments
- Immutable allocation with clear warnings

### 3. Validation Function
```python
def validate_api_allocation():
    """Validate API allocation for duplicates - MANDATORY before launch"""
    api_values = list(API_ALLOCATION.values())
    if len(api_values) != len(set(api_values)):
        duplicates = [x for x in api_values if api_values.count(x) > 1]
        raise ValueError(f"DUPLICATE API KEYS DETECTED: {duplicates}")
    print(f"[VALIDATE] ✓ {len(api_values)} unique API keys validated")
```

## Recovery Options

### Option 1: Wait and See (Current)
- Let agents run for full 15-20 minutes
- Check at 20:51 UTC if any completed
- If all stalled, proceed to Option 2

### Option 2: Kill and Relaunch (If Stalled)
```bash
# 1. Kill all agents on VM
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="pkill -f 'bob.*phase4'"

# 2. Reset manifests
python scripts/wave2/reset_phase4_manifests.py

# 3. Relaunch with fixed script
python scripts/wave2/phase4_with_checkpoints_v3_fixed.py
```

## Proven API Allocation (IMMUTABLE)

From `scripts/wave2/_wave2_v4_launch_generated.sh` (lines 57-65):
```
107 → b (2).json
108 → b.json
109 → bob (1).json
110 → bob (2).json
111 → bob (3).json
112 → bob (4).json
113 → bob (5).json
114 → bob (6).json  ← CRITICAL: NOT b.json
115 → bob.json
```

## Lessons Learned

1. **Never Improvise**: Always copy proven allocations from successful runs
2. **Validate Before Launch**: Check for duplicates before ANY multi-agent launch
3. **Document Source**: Reference the source script in comments
4. **Build on Success**: Each successful deployment is the template for the next
5. **Context Matters**: Don't lose working patterns between sessions

## Timeline

- **20:36 UTC**: Launched with buggy allocation (v2)
- **20:40 UTC**: Detected duplicate key issue
- **20:45 UTC**: Hardened protocol in AGENTS.md
- **20:48 UTC**: Created fixed script (v3)
- **20:51 UTC**: Decision point - wait or relaunch

## Next Steps

1. **Wait until 20:51 UTC** (T+15 minutes)
2. **Check progress**: `python scripts/wave2/check_phase4_local.py`
3. **If stalled**: Execute Option 2 (kill and relaunch)
4. **If working**: Let finish, use v3 for Phase 5

## Success Criteria

- All 9 epics have `04-tickets.md` files
- All manifests show Phase 4 = "completed"
- API balances show bobcoin consumption
- No quota contention errors in logs

## References

- Issue Analysis: `docs/workflow/WAVE_2_PHASE_4_API_KEY_ISSUE.md`
- Launch Success: `docs/workflow/WAVE_2_PHASE_4_LAUNCH_SUCCESS.md`
- Fixed Script: `scripts/wave2/phase4_with_checkpoints_v3_fixed.py`
- Protocol: `AGENTS.md` (Multi-Agent API Key Allocation Protocol)