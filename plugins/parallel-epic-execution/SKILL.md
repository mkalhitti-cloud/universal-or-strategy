---
name: parallel-epic-execution
description: Execute multiple V12 complexity reduction epics in parallel using Git worktrees for 64% time savings
version: 1.0.0
created: 2026-06-09
status: active
tags: [workflow, automation, parallel, epic, v12]
---

# Parallel Epic Execution Skill

**Version**: 1.0.0
**Created**: 2026-06-09
**Status**: Active (Pilot Phase)

## Overview

Execute multiple V12 complexity reduction epics in parallel using Git worktrees, achieving 64% time savings (148 hours vs 415 hours sequential).

## Architecture

### Three-Cluster Model

**Cluster 1: SIMA Files** (`epic-cluster-1`)
- Target: `src/V12_002.SIMA.*.cs`
- Epics: EPIC-CCN-19, 22, 25, 28, 31, 34, 37, 40, 43, 46...
- Worktree: `C:\WSGTA\universal-or-epic-cluster-1`

**Cluster 2: Orders Files** (`epic-cluster-2`)
- Target: `src/V12_002.Orders.*.cs`
- Epics: EPIC-CCN-20, 23, 26, 29, 32, 35, 38, 41, 44, 47...
- Worktree: `C:\WSGTA\universal-or-epic-cluster-2`

**Cluster 3: Lifecycle Files** (`epic-cluster-3`)
- Target: `src/V12_002.Lifecycle.cs`, `src/V12_002.*.cs` (other)
- Epics: EPIC-CCN-21, 24, 27, 30, 33, 36, 39, 42, 45, 48...
- Worktree: `C:\WSGTA\universal-or-epic-cluster-3`

### Isolation Strategy

Each worktree operates on:
- **Separate Git branch**: `epic-cluster-1/2/3`
- **Isolated working directory**: No file conflicts
- **Independent Bob CLI session**: Auto-approval enabled
- **Dedicated mode**: `v12-engineer` for surgical refactoring

## Setup Protocol

### Prerequisites

1. ✅ Main repo clean (no uncommitted changes)
2. ✅ On `gitbutler/workspace` branch
3. ✅ All quality gates passing (GODMODE)
4. ✅ Bob CLI installed and configured

### Automated Setup

```bash
# Run setup script (creates branches, worktrees, configs)
powershell -File .\scripts\setup_parallel_epic_workflow.ps1
```

**What It Does**:
1. Creates 3 Git branches from current state
2. Creates 3 worktrees at `C:\WSGTA\universal-or-epic-cluster-*`
3. Copies `bob.config.yaml` to each worktree
4. Creates `.bob/settings.json` with auto-approval enabled
5. Generates cluster assignment guide

### Manual Setup (If Script Fails)

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

# Enable auto-approval (create .bob/settings.json in each worktree)
# See: docs/workflow/PARALLEL_EPIC_AUTO_APPROVAL.md
```

## Execution Protocol

### Phase 1: Launch Bob CLI Sessions

Open 3 Bob Shell windows and navigate to worktrees:

**Window 1** (SIMA):
```bash
cd C:\WSGTA\universal-or-epic-cluster-1
bob
# Verify mode: v12-engineer
```

**Window 2** (Orders):
```bash
cd C:\WSGTA\universal-or-epic-cluster-2
bob
# Verify mode: v12-engineer
```

**Window 3** (Lifecycle):
```bash
cd C:\WSGTA\universal-or-epic-cluster-3
bob
# Verify mode: v12-engineer
```

### Phase 2: Start Parallel Execution

Paste commands simultaneously in all 3 windows:

**Window 1**: `Execute EPIC-CCN-19 Ticket 1`  
**Window 2**: `Execute EPIC-CCN-20 Ticket 1`  
**Window 3**: `Execute EPIC-CCN-21 Ticket 1`

**Auto-Approval**: Bob will execute autonomously without permission prompts (YOLO mode enabled via `.bob/settings.json`).

### Phase 3: Batch F5 Verification

**When**: After completing 3 tickets (one per cluster)

**Process**:
1. Switch to main repo: `cd C:\WSGTA\universal-or-strategy`
2. Merge all 3 branches:
   ```bash
   git merge epic-cluster-1 --no-ff -m "feat: EPIC-CCN-19 complete"
   git merge epic-cluster-2 --no-ff -m "feat: EPIC-CCN-20 complete"
   git merge epic-cluster-3 --no-ff -m "feat: EPIC-CCN-21 complete"
   ```
3. Sync hard links: `powershell -File .\deploy-sync.ps1`
4. Press F5 in NinjaTrader
5. Verify compilation + runtime behavior
6. If pass: Commit merged changes
7. If fail: Revert, fix in respective worktree, retry

### Phase 4: Continue Loop

After successful F5:
- Return to each Bob window
- Start next ticket in sequence
- Repeat until all epics complete

## Daily Workflow

**Morning Session** (2-3 hours):
- Start 3 tickets in parallel (no F5)
- Bob executes autonomously
- Monitor progress via Bob CLI output

**Lunch Break** (15 minutes):
- Batch F5 test all 3 worktrees
- Verify compilation + runtime
- Commit if passing

**Afternoon Session** (2-3 hours):
- Start next 3 tickets in parallel (no F5)
- Bob executes autonomously

**Evening** (15 minutes):
- Batch F5 test all 3 worktrees
- Commit if passing
- Plan next day's epics

## Time Savings Analysis

### Sequential Execution (Baseline)
- **Per Epic**: 2.5 hours (6 phases + F5)
- **Total (165 epics)**: 412.5 hours
- **Calendar Time**: 51.6 days (8 hours/day)

### Parallel Execution (3 Worktrees)
- **Per Batch**: 2.5 hours (3 epics in parallel) + 0.25 hours (batch F5)
- **Total (165 epics)**: 148.1 hours
- **Calendar Time**: 18.5 days (8 hours/day)

**Savings**: 264.4 hours (64% faster)

## Conflict Resolution

### Merge Conflicts

**Rare** (clusters target different files), but if they occur:

1. Identify conflicting file
2. Determine which cluster owns the file
3. Accept changes from owning cluster
4. Reject changes from other clusters
5. Re-run F5 verification

### Build Errors After Merge

1. Check `deploy-sync.ps1` output (hard link sync)
2. Verify no duplicate method definitions
3. Check for missing using statements
4. Re-run pre-push validation

## Monitoring

### Progress Tracking

```bash
# Check epic status across all clusters
git log --oneline --graph --all --decorate

# View current epic in each worktree
cat C:\WSGTA\universal-or-epic-cluster-1\docs\brain\task.md
cat C:\WSGTA\universal-or-epic-cluster-2\docs\brain\task.md
cat C:\WSGTA\universal-or-epic-cluster-3\docs\brain\task.md
```

### Health Metrics

Monitor in each Bob window:
- **Token Usage**: Should stay under 200k per epic
- **Complexity Reduction**: Target CYC ≤8
- **Build Time**: Should remain <30s
- **Test Coverage**: Should increase incrementally

## Cleanup Protocol

### After All Epics Complete

```bash
# Merge final changes to main
cd C:\WSGTA\universal-or-strategy
git merge epic-cluster-1 --no-ff -m "feat: SIMA epics complete"
git merge epic-cluster-2 --no-ff -m "feat: Orders epics complete"
git merge epic-cluster-3 --no-ff -m "feat: Lifecycle epics complete"

# Remove worktrees
git worktree remove C:\WSGTA\universal-or-epic-cluster-1
git worktree remove C:\WSGTA\universal-or-epic-cluster-2
git worktree remove C:\WSGTA\universal-or-epic-cluster-3

# Delete branches
git branch -d epic-cluster-1
git branch -d epic-cluster-2
git branch -d epic-cluster-3

# Final verification
powershell -File .\scripts\pre_push_validation.ps1
powershell -File .\deploy-sync.ps1
```

## Troubleshooting

### Bob CLI Not Auto-Approving

**Symptom**: Bob asks for permission despite `.bob/settings.json`

**Fix**:
1. Verify file exists: `cat .bob/settings.json`
2. Restart Bob CLI to reload settings
3. Or manually select "Yes, allow always" when prompted

### Worktree Creation Fails

**Symptom**: `fatal: 'gitbutler/workspace' is already used by worktree`

**Fix**: Use separate branches (not the same branch for all worktrees)

### F5 Compilation Errors After Merge

**Symptom**: NinjaTrader shows compilation errors

**Fix**:
1. Run `deploy-sync.ps1` to sync hard links
2. Check for duplicate method definitions
3. Verify all using statements present
4. Re-run pre-push validation

## Success Criteria

- ✅ 3 worktrees created successfully
- ✅ Auto-approval enabled in all worktrees
- ✅ Bob CLI running in `v12-engineer` mode
- ✅ Epics executing in parallel without conflicts
- ✅ Batch F5 verification passing
- ✅ Time savings achieved (64% faster)

## References

- **Workflow Design**: `docs/workflow/V12_EPIC_WORKFLOW_REFACTORING_DESIGN.md`
- **Setup Script**: `scripts/setup_parallel_epic_workflow.ps1`
- **Auto-Approval Config**: `docs/workflow/PARALLEL_EPIC_AUTO_APPROVAL.md`
- **Epic Roadmap**: `epic_roadmap.json`
- **Cluster Assignments**: `docs/workflow/PARALLEL_EPIC_CLUSTERS.md`

## Lessons Learned (Pilot Phase)

### What Worked
- ✅ Git worktrees provide perfect isolation
- ✅ Auto-approval eliminates interruptions
- ✅ Batch F5 testing is efficient
- ✅ File-based clustering prevents conflicts

### What Needs Improvement
- ⚠️ Setup script had Unicode character issues (fixed)
- ⚠️ GitButler virtual branches don't work with worktrees (use regular branches)
- ⚠️ Need better progress tracking across worktrees

### Future Enhancements
- 🔮 Automated cluster assignment based on file analysis
- 🔮 Real-time progress dashboard (3-panel view)
- 🔮 Automatic conflict detection before merge
- 🔮 Parallel F5 testing (3 NinjaTrader instances)