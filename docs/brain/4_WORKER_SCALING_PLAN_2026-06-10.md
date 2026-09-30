# 4-Worker Parallel Epic Execution: Scaling Plan

**Date**: 2026-06-10 09:44 PST
**Status**: APPROVED - Proceed with scaling
**Orchestrator**: Gemini CLI (Advanced mode)

---

## Executive Summary

**Approved**: Scale from 3 → 4 workers for parallel epic execution
**Target**: 173 epics (CCN-14 through CCN-186)
**Current Progress**: 10/173 complete (5.8%)
**Remaining**: 163 epics
**Estimated Time**: ~40 hours with 4 workers (vs ~54 hours with 3 workers)

---

## Worker Assignments

### Worker 1 (epic-cluster-1)
**Status**: Active
**Current Epic**: EPIC-CCN-33 (EmergencyFlattenSingleFleetAccount)
**Worktree**: `epic-cluster-1/`
**Next Assignment**: EPIC-CCN-34 (after CCN-33 complete)

### Worker 2 (epic-cluster-2)
**Status**: Needs redirect (phantom EPIC-CCN-13)
**Action Required**: Clean phantom epic, assign EPIC-CCN-35
**Worktree**: `epic-cluster-2/`
**Next Assignment**: EPIC-CCN-35

### Worker 3 (epic-cluster-3)
**Status**: Needs redirect (phantom EPIC-CCN-13)
**Action Required**: Clean phantom epic, assign EPIC-CCN-36
**Worktree**: `epic-cluster-3/`
**Next Assignment**: EPIC-CCN-36

### Worker 4 (epic-cluster-4) **NEW**
**Status**: Not created yet
**Action Required**: Create worktree, assign EPIC-CCN-37
**Worktree**: `epic-cluster-4/`
**First Assignment**: EPIC-CCN-37

---

## Scaling Protocol

### Step 1: Create Worker 4 Worktree
```powershell
# Create new worktree for Worker 4
git worktree add epic-cluster-4 gitbutler/workspace

# Verify worktree created
git worktree list
```

### Step 2: Clean Phantom Epics (Workers 2 & 3)
```powershell
# Worker 2: Remove phantom EPIC-CCN-13
cd epic-cluster-2
Remove-Item -Recurse -Force docs/brain/EPIC-CCN-13
git reset --hard HEAD

# Worker 3: Remove phantom EPIC-CCN-13
cd epic-cluster-3
Remove-Item -Recurse -Force docs/brain/EPIC-CCN-13
git reset --hard HEAD

# Return to main workspace
cd c:/WSGTA/universal-or-strategy
```

### Step 3: Validate Epic Assignments
```powershell
# Validate Worker 1's current epic
python scripts/validate_epic.py --epic EPIC-CCN-33 --worker worker-1

# Assign Worker 2
python scripts/validate_epic.py --epic EPIC-CCN-35 --worker worker-2

# Assign Worker 3
python scripts/validate_epic.py --epic EPIC-CCN-36 --worker worker-3

# Assign Worker 4
python scripts/validate_epic.py --epic EPIC-CCN-37 --worker worker-4
```

### Step 4: Launch Worker Orchestrators
```powershell
# Launch 4 worker orchestrators in separate terminals
# Terminal 1: Worker 1 (already running)
# Terminal 2: Worker 2
cd epic-cluster-2
# Start Bob CLI or Gemini CLI for EPIC-CCN-35

# Terminal 3: Worker 3
cd epic-cluster-3
# Start Bob CLI or Gemini CLI for EPIC-CCN-36

# Terminal 4: Worker 4
cd epic-cluster-4
# Start Bob CLI or Gemini CLI for EPIC-CCN-37
```

---

## Epic Distribution Strategy

### Batch 1 (Current - Epics 33-37)
- Worker 1: EPIC-CCN-33 (in progress)
- Worker 2: EPIC-CCN-35 (assign now)
- Worker 3: EPIC-CCN-36 (assign now)
- Worker 4: EPIC-CCN-37 (assign now)

### Batch 2 (Next - Epics 38-41)
- Worker 1: EPIC-CCN-34 (after 33 complete)
- Worker 2: EPIC-CCN-38 (after 35 complete)
- Worker 3: EPIC-CCN-39 (after 36 complete)
- Worker 4: EPIC-CCN-40 (after 37 complete)

### Batch 3 (Epics 41-44)
- Worker 1: EPIC-CCN-41
- Worker 2: EPIC-CCN-42
- Worker 3: EPIC-CCN-43
- Worker 4: EPIC-CCN-44

**Pattern**: Sequential assignment from `epic_roadmap.json`, 4 epics at a time

---

## Coordination Protocol

### Epic Assignment Rules
1. **Validation Required**: All epics MUST be validated via `validate_epic.py` before starting
2. **Locking**: Epic is locked to worker when assigned (prevents conflicts)
3. **Sequential Pull**: Workers pull next epic from roadmap after completing current
4. **No Phantom Epics**: Workers MUST NOT scan `docs/brain/` directories

### File Isolation Strategy
**Goal**: Prevent merge conflicts between workers

**Rule**: Each epic targets a SINGLE method in a SINGLE file
- Worker 1: `src/V12_002.cs` (method A)
- Worker 2: `src/V12_002.cs` (method B)
- Worker 3: `src/V12_003.cs` (method C)
- Worker 4: `src/V12_004.cs` (method D)

**Conflict Prevention**:
- If 2 workers target same file, assign to different methods
- If same method, serialize (one worker waits)
- Use `epic_roadmap.json` to check file/method conflicts

### Communication Protocol
**Orchestrator (me) monitors**:
1. Worker progress (check `docs/brain/EPIC-X/manifest.json`)
2. Epic completion (status = "completed")
3. Conflicts or blockers (status = "blocked")
4. Next epic assignment (when worker idle)

**Workers report**:
- Epic start: Update manifest status = "in_progress"
- Epic complete: Update manifest status = "completed"
- Epic blocked: Update manifest status = "blocked" + reason

---

## Autonomous Approval Protocol

### Standard Epics (Auto-Approve)
**Criteria**:
- Single method extraction (CYC > 20 → CYC ≤ 8)
- LOW or MEDIUM risk
- No scope creep
- Passes pre-push validation

**Action**: Worker proceeds autonomously, no Director approval needed

### High-Risk Epics (Director Approval)
**Criteria**:
- HIGH risk
- Multiple file changes
- Scope expansion detected
- Critical blocker (P0)

**Action**: Worker STOPS, reports to Director, waits for approval

---

## Progress Tracking

### Metrics
- **Epics Complete**: 10/173 (5.8%)
- **Epics Remaining**: 163
- **Workers Active**: 4
- **Estimated Completion**: ~40 hours (10 hours/worker/day × 4 workers)

### Milestones
- **25% Complete** (43 epics): ~10 hours
- **50% Complete** (86 epics): ~20 hours
- **75% Complete** (129 epics): ~30 hours
- **100% Complete** (173 epics): ~40 hours

### Daily Check-ins
**Morning** (09:00 PST):
- Review overnight progress
- Assign next batch of epics
- Resolve any blockers

**Midday** (13:00 PST):
- Check worker status
- Adjust assignments if needed

**Evening** (18:00 PST):
- Review day's progress
- Plan next day's assignments

---

## Rollback Protocol

### If Worker Fails
1. **Stop worker** immediately
2. **Document failure** in `docs/brain/EPIC-X/failure-analysis.md`
3. **Rollback worktree** to last known good state
4. **Reassign epic** to different worker or retry

### If Conflict Detected
1. **Pause both workers** targeting same file
2. **Serialize execution** (one waits for other)
3. **Update epic_roadmap.json** with conflict note
4. **Resume after first worker completes**

---

## Success Criteria

### Per Epic
- ✅ Passes pre-push validation (all 13 checks)
- ✅ CYC ≤ 8 for extracted methods
- ✅ Zero lock statements
- ✅ ASCII-only compliance
- ✅ F5 verification in NinjaTrader (if needed)

### Overall
- ✅ All 173 epics complete
- ✅ Zero P0 blockers
- ✅ All worktrees merged to main
- ✅ `deploy-sync.ps1` executed successfully
- ✅ Final F5 verification passed

---

## Next Actions (Immediate)

### Orchestrator (me)
1. ✅ Create this scaling plan
2. ⬜ Execute Step 1: Create Worker 4 worktree
3. ⬜ Execute Step 2: Clean phantom epics (Workers 2 & 3)
4. ⬜ Execute Step 3: Validate epic assignments
5. ⬜ Execute Step 4: Launch worker orchestrators
6. ⬜ Monitor progress and assign next epics

### Workers
1. ⬜ Worker 1: Continue EPIC-CCN-33
2. ⬜ Worker 2: Start EPIC-CCN-35 (after cleanup)
3. ⬜ Worker 3: Start EPIC-CCN-36 (after cleanup)
4. ⬜ Worker 4: Start EPIC-CCN-37 (after worktree created)

---

## Risk Mitigation

### Risk 1: Worker Coordination Failure
**Mitigation**: `validate_epic.py` prevents phantom epics and conflicts

### Risk 2: Merge Conflicts
**Mitigation**: File isolation strategy + sequential assignment

### Risk 3: Worker Overload
**Mitigation**: Monitor worker progress, redistribute if needed

### Risk 4: Epic Scope Creep
**Mitigation**: V12.23 No Scope Creep Protocol enforced

---

**Status**: Ready to execute scaling plan
**Approval**: GRANTED by Director
**Next Step**: Create Worker 4 worktree and clean phantom epics