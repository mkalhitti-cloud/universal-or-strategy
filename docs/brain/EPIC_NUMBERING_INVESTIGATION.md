# Epic Numbering Investigation Report

**Date**: 2026-06-09
**Issue**: Window 3 using EPIC-CCN-51 while Windows 1-2 using EPIC-CCN-19/20

## Root Cause Analysis

### The Problem
User noticed that the 3 parallel Bob CLI sessions were working on different epic numbers:
- **Window 1**: EPIC-CCN-19 (GetSubscriberCounts)
- **Window 2**: EPIC-CCN-20 (ProcessSessionReset)  
- **Window 3**: EPIC-CCN-51 (BroadcastSyncTargetState) ⚠️

The user correctly identified this as a potential misalignment.

### Investigation Results

After regenerating the complete epic roadmap from the complexity audit:

**Current Roadmap State**:
- Total epics: **171** (not 168 as originally stated)
- Completed: **4** (CCN-13, 14, 16, 17, 18)
- Pending: **167**

**Epic Number Mapping**:
```
EPIC-CCN-19: GetSubscriberCounts (src/SignalBroadcaster.cs, CYC=9)
EPIC-CCN-20: ProcessSessionReset (src/V12_002.BarUpdate.cs, CYC=11)
EPIC-CCN-51: BroadcastSyncTargetState (src/V12_002.Orders.Callbacks.Execution.cs, CYC=9)
```

### What Happened

**The original task description was INCORRECT**. It stated:
> "Continue with EPIC-CCN-18 through EPIC-CCN-168 (165 epics remaining)"

But the actual `epic_roadmap.json` only had **6 epics** (CCN-13 through CCN-18) before regeneration.

**The orchestrator (Claude) made an assumption error**:
- Assumed there were 168 pre-existing epics in the roadmap
- Assigned Window 3 to EPIC-CCN-51 based on this false assumption
- Windows 1-2 were correctly assigned to CCN-19/20

### Verification

After running `python scripts/generate_epic_roadmap.py`:
- ✅ EPIC-CCN-19 exists and is valid (GetSubscriberCounts, CYC=9)
- ✅ EPIC-CCN-20 exists and is valid (ProcessSessionReset, CYC=11)
- ✅ EPIC-CCN-51 exists and is valid (BroadcastSyncTargetState, CYC=9)

**All 3 epic numbers are legitimate** - they're just not sequential.

### Impact Assessment

**Window 3 Work Status**: ✅ **VALID**
- EPIC-CCN-51 is a real epic in the roadmap
- The method exists: `BroadcastSyncTargetState` in `V12_002.Orders.Callbacks.Execution.cs`
- CYC=9 (needs reduction to ≤8)
- Work performed is legitimate and should be kept

**Windows 1-2 Work Status**: ✅ **VALID**
- Both are working on the correct next sequential epics (19, 20)

### The Real Issue

The problem is **workflow coordination**, not data corruption:

1. **Sequential vs Parallel Execution**:
   - Windows 1-2 are working sequentially (CCN-19, 20)
   - Window 3 jumped ahead to CCN-51
   - This creates a **gap** in the roadmap (CCN-21 through CCN-50 untouched)

2. **Cluster Assignment Logic**:
   - The orchestrator tried to assign "clusters" of related work
   - But the epic roadmap is **flat** (sorted by CYC, not by file/cluster)
   - This caused Window 3 to skip ahead

### Correct Workflow

**For parallel execution, we should**:
1. Assign **consecutive** epics to each window
2. Window 1: EPIC-CCN-19
3. Window 2: EPIC-CCN-20
4. Window 3: EPIC-CCN-21 (not CCN-51)

**OR use cluster-based assignment**:
1. Group epics by file/subsystem
2. Assign entire clusters to windows
3. Requires roadmap reorganization

## Resolution

### Immediate Action
✅ **Keep all work from Windows 1-3** - it's all valid

### Next Steps
1. **Complete current batch** (Windows 1-3 at CCN-19, 20, 51)
2. **After F5 verification**, decide on workflow:
   - **Option A**: Continue sequential (Window 3 moves to CCN-21)
   - **Option B**: Reorganize roadmap by clusters
   - **Option C**: Accept gaps and backfill later

### Recommendation
**Option A (Sequential)** is safest:
- Predictable progress tracking
- No gaps in roadmap
- Easier to resume after failures
- Aligns with Building Block design

## Lessons Learned

1. ✅ **Always verify epic roadmap exists before assigning work**
2. ✅ **Use consecutive epic numbers for parallel execution**
3. ✅ **Don't assume roadmap size from task description**
4. ❌ **Don't use "cluster" logic without explicit roadmap reorganization**

## Action Items

- [ ] User decides: Keep current assignments or reassign Window 3 to CCN-21?
- [ ] Update parallel workflow documentation with sequential assignment rule
- [ ] Add epic roadmap validation to `epic-validate` command
- [ ] Consider adding `--cluster` flag for cluster-based assignment (future)

---

**Status**: Investigation complete, awaiting user decision on Window 3 assignment.