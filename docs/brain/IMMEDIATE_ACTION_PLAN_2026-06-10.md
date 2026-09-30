# Immediate Action Plan - Pre-4-Worker Launch
**Date**: 2026-06-10  
**Priority**: 🔴 CRITICAL  
**Goal**: Fix coordination issues before scaling to 4 workers

---

## Current Worker Status

### Worker 1 (epic-cluster-1)
- **Epic**: EPIC-CCN-33 (EmergencyFlattenSingleFleetAccount)
- **Status**: ⚠️ Budget exceeded, needs retry
- **Action**: Retry Phase 5 from checkpoint

### Worker 2 (epic-cluster-2)  
- **Epic**: 🔴 EPIC-CCN-13 (PHANTOM - does not exist in roadmap)
- **Status**: ❌ CRITICAL ERROR - wasted 4 phases on non-existent epic
- **Action**: DELETE EPIC-CCN-13 folder, START EPIC-CCN-29

### Worker 3 (epic-cluster-3)
- **Epic**: EPIC-CCN-23 (OnBarUpdate) 
- **Status**: ✅ Complete, needs F5 verification
- **Action**: F5 verify, then START EPIC-CCN-30

---

## Critical Issue: Phantom Epic Execution

**Problem**: Both Worker 2 & 3 tried to execute EPIC-CCN-13, which **does not exist** in `epic_roadmap.json`

**Root Cause**:
1. Stale EPIC-CCN-13 folders in `docs/brain/` from old numbering scheme
2. Workers scan directories instead of pulling from roadmap
3. No epic validation before starting work

**Impact**: Wasted work, confusion, coordination failure

---

## Immediate Fixes (Before 4th Worker)

### Fix 1: Clean Up Phantom Epics (NOW)

```powershell
# Worker 2
cd C:/WSGTA/universal-or-epic-cluster-2
Remove-Item -Recurse -Force docs/brain/EPIC-CCN-13

# Worker 3  
cd C:/WSGTA/universal-or-epic-cluster-3
Remove-Item -Recurse -Force docs/brain/EPIC-CCN-13
```

### Fix 2: Implement Epic Validation (TODAY)

Create `scripts/validate_epic.py`:

```python
import json
from pathlib import Path

def load_roadmap():
    """Load epic_roadmap.json"""
    with open('epic_roadmap.json', 'r') as f:
        return json.load(f)

def validate_epic_exists(epic_id: str) -> bool:
    """Verify epic exists in roadmap"""
    roadmap = load_roadmap()
    return any(e['epic_number'] == epic_id for e in roadmap)

def get_next_epic() -> dict:
    """Get next pending epic from roadmap"""
    roadmap = load_roadmap()
    for epic in roadmap:
        if epic.get('status') != 'complete' and not epic.get('assigned_to'):
            return epic
    return None

def claim_epic(epic_id: str, worker_id: str) -> dict:
    """Claim epic for worker (add locking)"""
    roadmap = load_roadmap()
    for epic in roadmap:
        if epic['epic_number'] == epic_id:
            if epic.get('assigned_to'):
                raise ValueError(f"Epic {epic_id} already assigned to {epic['assigned_to']}")
            epic['assigned_to'] = worker_id
            epic['lock_timestamp'] = datetime.utcnow().isoformat()
            # Save roadmap
            with open('epic_roadmap.json', 'w') as f:
                json.dump(roadmap, f, indent=2)
            return epic
    raise ValueError(f"Epic {epic_id} not found in roadmap")

if __name__ == '__main__':
    import sys
    if len(sys.argv) < 2:
        print("Usage: python validate_epic.py <epic_id>")
        sys.exit(1)
    
    epic_id = sys.argv[1]
    if validate_epic_exists(epic_id):
        print(f"✅ {epic_id} exists in roadmap")
    else:
        print(f"❌ {epic_id} NOT FOUND in roadmap")
        sys.exit(1)
```

### Fix 3: Update Worker Instructions (TODAY)

**New Protocol**:
1. Worker MUST call `python scripts/validate_epic.py EPIC-CCN-XX` before starting
2. If validation fails, worker MUST call `python scripts/validate_epic.py --next` to get next epic
3. Worker MUST NOT scan `docs/brain/` directories for work

---

## Today's Execution Plan

### Step 1: Clean Up (15 minutes)
- [ ] Delete EPIC-CCN-13 from Worker 2 worktree
- [ ] Delete EPIC-CCN-13 from Worker 3 worktree
- [ ] Verify no other phantom epics exist

### Step 2: Implement Validation (30 minutes)
- [ ] Create `scripts/validate_epic.py`
- [ ] Test validation with existing epics
- [ ] Test with non-existent epic (should fail)

### Step 3: Finish Current Work (2-4 hours)
- [ ] Worker 1: Retry EPIC-CCN-33 Phase 5
- [ ] Worker 2: START EPIC-CCN-29 Phase 0 (with validation)
- [ ] Worker 3: F5 verify EPIC-CCN-23, START EPIC-CCN-30 (with validation)

### Step 4: Test 3-Worker Coordination (1 hour)
- [ ] Verify all 3 workers using validation
- [ ] Verify no conflicts
- [ ] Verify progress tracking works

### Step 5: Add 4th Worker (Tomorrow)
- [ ] Create epic-cluster-4 worktree
- [ ] Assign EPIC-CCN-31 (with validation)
- [ ] Monitor 4-worker coordination

---

## Jane Street Knowledge Base Audit (Deferred)

**Status**: DEFERRED until coordination issues fixed

**Plan**: Index all 406 Jane Street repos focusing on:
- Core libraries (base, core, async)
- Reactive programming (incremental, bonsai)
- Concurrency patterns (hardcaml, async_kernel)
- Performance optimization (magic-trace, tracing)

**Action**: Create separate task after 4-worker coordination proven

---

## Infrastructure Audit (Deferred)

**Status**: DEFERRED until coordination issues fixed

**Components to Review**:
- RAG/CAG setup with Firebase
- Building Blocks integration
- Epic manifest system
- Worktree management

**Action**: Create separate task after 4-worker coordination proven

---

## Success Criteria

### Today
- ✅ All phantom epics cleaned up
- ✅ Epic validation implemented and tested
- ✅ All 3 workers using validation protocol
- ✅ No coordination conflicts

### Tomorrow
- ✅ 4th worker added successfully
- ✅ All 4 workers coordinating without conflicts
- ✅ Progress tracking accurate
- ✅ No phantom epic issues

### This Week
- ✅ 10+ epics completed with 4-worker coordination
- ✅ Jane Street KB audit complete
- ✅ Infrastructure audit complete
- ✅ Workflow automation gaps identified and fixed

---

## Risk Mitigation

### Risk 1: Workers ignore validation
**Mitigation**: Make validation mandatory in epic-intake command

### Risk 2: Validation script has bugs
**Mitigation**: Test thoroughly before deploying to all workers

### Risk 3: 4th worker causes new conflicts
**Mitigation**: Add 4th worker only after 3-worker coordination proven stable

---

**Next Action**: Clean up phantom epics NOW, then implement validation