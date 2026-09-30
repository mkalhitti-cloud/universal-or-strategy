# Parallel Epic Execution Workflow

**Version**: 1.0.0  
**Status**: Active (Pilot Phase)  
**Created**: 2026-06-09

## Executive Summary

Execute 165 V12 complexity reduction epics in **148 hours** instead of 415 hours (64% time savings) using Git worktrees and parallel execution.

## Architecture

### Three-Cluster Model

```
Main Repo (gitbutler/workspace)
├── Cluster 1: epic-cluster-1 → C:\WSGTA\universal-or-epic-cluster-1
│   └── SIMA files (V12_002.SIMA.*.cs)
├── Cluster 2: epic-cluster-2 → C:\WSGTA\universal-or-epic-cluster-2
│   └── Orders files (V12_002.Orders.*.cs)
└── Cluster 3: epic-cluster-3 → C:\WSGTA\universal-or-epic-cluster-3
    └── Lifecycle files (V12_002.Lifecycle.cs + others)
```

### Isolation Guarantees

- ✅ **File-Level**: Each cluster targets different source files (zero conflicts)
- ✅ **Branch-Level**: Each worktree on separate Git branch
- ✅ **Process-Level**: Independent Bob CLI sessions with auto-approval
- ✅ **Build-Level**: Batch F5 testing after merging all 3 branches

## Setup (One-Time)

### Step 1: Verify Prerequisites

```powershell
# 1. Clean working tree
git status  # Should show "nothing to commit, working tree clean"

# 2. On correct branch
git branch  # Should show "* gitbutler/workspace"

# 3. Quality gates passing
powershell -File .\scripts\pre_push_validation.ps1

# 4. Hard links synchronized
powershell -File .\deploy-sync.ps1
```

### Step 2: Create Worktrees

```bash
# Create branches
git branch epic-cluster-1 gitbutler/workspace
git branch epic-cluster-2 gitbutler/workspace
git branch epic-cluster-3 gitbutler/workspace

# Create worktrees
git worktree add C:\WSGTA\universal-or-epic-cluster-1 epic-cluster-1
git worktree add C:\WSGTA\universal-or-epic-cluster-2 epic-cluster-2
git worktree add C:\WSGTA\universal-or-epic-cluster-3 epic-cluster-3

# Copy configs
Copy-Item bob.config.yaml C:\WSGTA\universal-or-epic-cluster-1\
Copy-Item bob.config.yaml C:\WSGTA\universal-or-epic-cluster-2\
Copy-Item bob.config.yaml C:\WSGTA\universal-or-epic-cluster-3\
```

### Step 3: Enable Auto-Approval

Create `.bob/settings.json` in each worktree:

```json
{
  "autoApprove": true,
  "autoApproveTools": [
    "execute_command",
    "write_to_file",
    "apply_diff",
    "insert_content",
    "read_file",
    "list_files",
    "search_files"
  ]
}
```

**Automated**:
```powershell
# Run from main repo
powershell -File .\scripts\create_worktree_auto_approval.ps1
```

### Step 4: Launch Bob CLI Sessions

Open 3 terminal windows:

**Window 1** (SIMA):
```bash
cd C:\WSGTA\universal-or-epic-cluster-1
bob
# Verify: "Switched to V12 Photon Engineer mode"
```

**Window 2** (Orders):
```bash
cd C:\WSGTA\universal-or-epic-cluster-2
bob
# Verify: "Switched to V12 Photon Engineer mode"
```

**Window 3** (Lifecycle):
```bash
cd C:\WSGTA\universal-or-epic-cluster-3
bob
# Verify: "Switched to V12 Photon Engineer mode"
```

## Daily Execution Loop

### Morning Session (2-3 hours)

**Start 3 tickets in parallel** (paste simultaneously in all 3 Bob windows):

**Window 1**: `Execute EPIC-CCN-19 Ticket 1`  
**Window 2**: `Execute EPIC-CCN-20 Ticket 1`  
**Window 3**: `Execute EPIC-CCN-21 Ticket 1`

**What Happens**:
- Bob executes autonomously (no permission prompts)
- TDD tests written before extraction
- Methods extracted with zero logic drift
- BUILD_TAG updated automatically
- Commits created in respective branches

**Monitor**: Watch Bob CLI output for completion

### Lunch Break (15 minutes) - Batch F5 Gate

**Step 1: Merge All 3 Branches**

```bash
cd C:\WSGTA\universal-or-strategy

# Merge cluster 1
git merge epic-cluster-1 --no-ff -m "feat(EPIC-CCN-19): complexity reduction complete"

# Merge cluster 2
git merge epic-cluster-2 --no-ff -m "feat(EPIC-CCN-20): complexity reduction complete"

# Merge cluster 3
git merge epic-cluster-3 --no-ff -m "feat(EPIC-CCN-21): complexity reduction complete"
```

**Step 2: Sync Hard Links**

```powershell
powershell -File .\deploy-sync.ps1
```

**Step 3: F5 Verification**

1. Open NinjaTrader 8
2. Press F5 (compile + load)
3. Verify:
   - ✅ Zero compilation errors
   - ✅ Strategy loads successfully
   - ✅ No runtime exceptions
   - ✅ BUILD_TAG displays correctly

**Step 4: Commit or Revert**

**If F5 PASS**:
```bash
git add .
git commit -m "feat: EPIC-CCN-19/20/21 verified (batch F5 pass)"
git push origin gitbutler/workspace
```

**If F5 FAIL**:
```bash
# Revert merge
git reset --hard HEAD~3

# Identify failing cluster
# Fix in respective worktree
# Retry merge one cluster at a time
```

### Afternoon Session (2-3 hours)

**Start next 3 tickets** (same process as morning):

**Window 1**: `Execute EPIC-CCN-22 Ticket 1`  
**Window 2**: `Execute EPIC-CCN-23 Ticket 1`  
**Window 3**: `Execute EPIC-CCN-24 Ticket 1`

### Evening (15 minutes) - Batch F5 Gate

Repeat lunch break process for afternoon tickets.

## Weekly Cadence

**Monday-Thursday**: Execute 6 tickets/day (2 batches)  
**Friday**: Execute 3 tickets + final verification + documentation

**Weekly Target**: 27 epics (9 per cluster)  
**Total Duration**: ~6 weeks for all 165 epics

## Conflict Resolution

### Merge Conflicts (Rare)

**Symptom**: Git reports conflicts during merge

**Resolution**:
1. Identify conflicting file
2. Determine owning cluster (SIMA/Orders/Lifecycle)
3. Accept changes from owning cluster
4. Reject changes from other clusters
5. Re-run F5 verification

### Build Errors After Merge

**Symptom**: NinjaTrader compilation errors after merge

**Diagnosis**:
```powershell
# Check hard link sync
powershell -File .\deploy-sync.ps1

# Check for duplicate methods
grep -r "public.*MethodName" src/

# Verify using statements
grep -r "using " src/ | sort | uniq
```

**Fix**:
1. Run `deploy-sync.ps1` to fix hard links
2. Remove duplicate method definitions
3. Add missing using statements
4. Re-run F5

### Bob CLI Hangs

**Symptom**: Bob stops responding mid-epic

**Recovery**:
1. Check last checkpoint: `.bob/notes/pending-notes.txt`
2. Verify last commit: `git log -1`
3. Restart Bob CLI in same worktree
4. Use `/restore` to resume from checkpoint

## Monitoring & Metrics

### Progress Tracking

```bash
# View all branches
git log --oneline --graph --all --decorate

# Check epic status per cluster
cat C:\WSGTA\universal-or-epic-cluster-1\docs\brain\task.md
cat C:\WSGTA\universal-or-epic-cluster-2\docs\brain\task.md
cat C:\WSGTA\universal-or-epic-cluster-3\docs\brain\task.md

# Count completed epics
git log --oneline | grep "feat(EPIC-CCN" | wc -l
```

### Health Metrics

Monitor these in each Bob window:

| Metric | Target | Alert Threshold |
|--------|--------|-----------------|
| Token Usage | <200k per epic | >250k |
| Complexity Reduction | CYC ≤8 | CYC >10 |
| Build Time | <30s | >60s |
| Test Coverage | Increasing | Decreasing |
| F5 Pass Rate | 100% | <90% |

### Time Tracking

```bash
# Per-epic duration
git log --format="%ar" --date=relative | head -3

# Total time invested
# Start: 2026-06-09
# Target: 148 hours (18.5 days @ 8 hours/day)
```

## Cleanup (After All Epics Complete)

### Step 1: Final Merge

```bash
cd C:\WSGTA\universal-or-strategy

# Merge all final changes
git merge epic-cluster-1 --no-ff -m "feat: SIMA complexity reduction complete (all epics)"
git merge epic-cluster-2 --no-ff -m "feat: Orders complexity reduction complete (all epics)"
git merge epic-cluster-3 --no-ff -m "feat: Lifecycle complexity reduction complete (all epics)"

# Final verification
powershell -File .\scripts\pre_push_validation.ps1
powershell -File .\deploy-sync.ps1
```

### Step 2: Remove Worktrees

```bash
# Remove worktrees
git worktree remove C:\WSGTA\universal-or-epic-cluster-1
git worktree remove C:\WSGTA\universal-or-epic-cluster-2
git worktree remove C:\WSGTA\universal-or-epic-cluster-3

# Delete branches
git branch -d epic-cluster-1
git branch -d epic-cluster-2
git branch -d epic-cluster-3
```

### Step 3: Documentation

Create completion report:

```bash
# Generate final metrics
python scripts/complexity_audit.py > docs/brain/PARALLEL_EPIC_COMPLETION_REPORT.md

# Document lessons learned
# Edit: docs/brain/PARALLEL_EPIC_RETROSPECTIVE.md
```

## Success Criteria

- ✅ 165 epics completed (55 per cluster)
- ✅ All methods CYC ≤8 (Jane Street ultra-alignment)
- ✅ Zero new Jane Street violations (347 P0 baseline maintained)
- ✅ All quality gates passing (13/13 checks)
- ✅ Hard links synchronized (81/81 files)
- ✅ Time savings achieved (64% faster = 267 hours saved)

## References

- **Skill Documentation**: `plugins/parallel-epic-execution/SKILL.md`
- **Setup Script**: `scripts/setup_parallel_epic_workflow.ps1`
- **Auto-Approval Script**: `scripts/create_worktree_auto_approval.ps1`
- **Epic Roadmap**: `epic_roadmap.json`
- **Cluster Assignments**: `docs/workflow/PARALLEL_EPIC_CLUSTERS.md`
- **V12 Workflow Design**: `docs/workflow/V12_EPIC_WORKFLOW_REFACTORING_DESIGN.md`