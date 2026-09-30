# GitButler Consolidated Workflow (V12.24)

**Date**: 2026-06-07  
**Status**: ACTIVE - Replaces all previous branch strategy docs  
**Version**: V12.24 (Post-EPIC-CCN-51 Consolidation)

---

## Executive Summary

**ONE WORKSPACE, ALL WORK**: Use `gitbutler/workspace` for ALL development (src, infra, protocol). Create virtual branches for logical separation. Batch commits into PRs at epic completion.

**Key Changes from V12.18**:
- ❌ **DEPRECATED**: Traditional three-tier branches (`feature/src-*`, `feature/infra-*`, `feature/protocol-*`)
- ✅ **NEW**: GitButler virtual branches for ALL work
- ✅ **NEW**: Batch PR strategy (complete all epics → create PRs → merge)

---

## Core Principles

### 1. Permanent Workspace
- **Branch**: `gitbutler/workspace` (never leave this branch)
- **Purpose**: Single source of truth for all local work
- **Persistence**: Survives context switches, session restarts, branch confusion

### 2. Virtual Branches
- **Tool**: GitButler UI (desktop app)
- **Purpose**: Logical separation of concerns within workspace
- **Types**:
  - **Tier 1 (src)**: C# code changes (`.cs` files)
  - **Tier 2 (infra)**: Scripts, docs, configs (non-`.cs` files)
  - **Tier 3 (protocol)**: Agent rules, workflows (`.bob/`, `AGENTS.md`, etc.)

### 3. Batch PR Strategy
- **When**: After completing ALL epics in a batch (e.g., EPIC-CCN-15 through EPIC-CCN-45)
- **How**: Cherry-pick commits from workspace into feature branches
- **Why**: Minimize PR overhead, maximize development velocity

---

## Workflow: Epic Execution

### Phase 1: Setup (Once per Epic Batch)
```powershell
# Ensure you're on gitbutler/workspace
git checkout gitbutler/workspace

# Sync with main
git fetch origin main
git merge origin/main

# Open GitButler UI
gitbutler
```

### Phase 2: Epic Execution (Repeat for Each Epic)
```
1. Open GitButler UI
2. Create virtual branch: "epic-ccn-X-src" (Tier 1)
3. Execute epic tickets (Bob CLI or manual)
4. Commit to virtual branch after each ticket
5. F5 verification after each commit
6. Continue to next epic
```

### Phase 3: Jane Street Fixes (Opportunistic)
```
For EVERY file touched during epic:
1. Fix ALL Jane Street violations (not just P0)
2. Magic numbers → named constants
3. Null returns → Option<T>
4. Exceptions → Result<T,E>
5. Lock usage → Actor pattern
6. Commit fixes to same virtual branch
```

### Phase 4: Batch PR Creation (After All Epics Complete)
```powershell
# Example: After completing EPIC-CCN-15 through EPIC-CCN-45

# 1. Create feature branch from main
git checkout main
git checkout -b feature/src-epic-ccn-15-45-batch

# 2. Cherry-pick commits from workspace
git cherry-pick <commit-hash-epic-15>
git cherry-pick <commit-hash-epic-16>
# ... (all epic commits)

# 3. Push and create PR
git push origin feature/src-epic-ccn-15-45-batch
gh pr create --title "[BATCH] EPIC-CCN-15 through 45: CYC reduction + Jane Street fixes" \
             --body "See docs/brain/EPIC-LOOP-EXECUTION-PLAN.md"

# 4. Run /pr-loop to achieve 100/100 PHS
# (Orchestrator mode handles this)

# 5. Merge PR
gh pr merge --squash

# 6. Sync workspace with main
git checkout gitbutler/workspace
git merge origin/main
```

---

## Virtual Branch Organization

### Naming Convention
- **Tier 1 (src)**: `epic-ccn-X-src` (e.g., `epic-ccn-15-src`)
- **Tier 2 (infra)**: `epic-ccn-X-docs` (e.g., `epic-ccn-15-docs`)
- **Tier 3 (protocol)**: `protocol-X` (e.g., `protocol-gitbutler-update`)

### Commit Strategy
- **One commit per ticket** (Tier 1)
- **One commit per logical change** (Tier 2/3)
- **Commit message format**: `feat(epic-ccn-X-tY): <description> - CYC A→B`

### Example Workspace Structure
```
gitbutler/workspace (local only)
├── epic-ccn-15-src (Tier 1)
│   ├── Commit 1: Ticket 1 extraction
│   ├── Commit 2: Ticket 2 extraction
│   └── Commit 3: Jane Street fixes
├── epic-ccn-16-src (Tier 1)
│   ├── Commit 1: Ticket 1 extraction
│   └── Commit 2: Ticket 2 extraction
└── epic-docs (Tier 2)
    └── Commit 1: Update planning docs
```

---

## Three-Tier Model (Adapted for GitButler)

### Tier 1: C# Code (`.cs` files)
- **Scope**: `src/`, `tests/`, `benchmarks/` (ANY `.cs` file)
- **Virtual Branch**: `epic-ccn-X-src`
- **PR Required**: ✅ YES (after batch completion)
- **Review**: Full bot suite (Arena AI, Codacy, CodeRabbit)
- **Complexity Audit**: Required (CYC ≤8 target)

### Tier 2: Infrastructure (non-`.cs` files)
- **Scope**: `docs/`, `scripts/`, `.github/`
- **Virtual Branch**: `epic-ccn-X-docs` OR direct commit to workspace
- **PR Required**: ⚠️ OPTIONAL (batch with Tier 1 OR separate lightweight PR)
- **Review**: Markdown linting only

### Tier 3: Protocol (agent rules)
- **Scope**: `.bob/`, `.codex/`, `.cursor/`, `AGENTS.md`, `BOB.md`, etc.
- **Virtual Branch**: `protocol-X` OR direct commit to workspace
- **PR Required**: ❌ NO (direct push to main after workspace sync)
- **Review**: None (Director approval only)

---

## Batch PR Strategy: Detailed Workflow

### Scenario: Complete 31 Epics (EPIC-CCN-15 through EPIC-CCN-45)

**Goal**: Reduce all methods CYC 15-20 → ≤8, fix Jane Street violations

**Timeline**: 7.75 - 12.9 hours (15-25 min per epic)

**Workflow**:

#### Week 1: Execute Epics 15-25 (11 epics)
```
Day 1-2: EPIC-CCN-15 through EPIC-CCN-20 (6 epics, ~3 hours)
Day 3-4: EPIC-CCN-21 through EPIC-CCN-25 (5 epics, ~2.5 hours)

All commits stay on gitbutler/workspace
No PRs created yet
F5 verification after each epic
```

#### Week 2: Execute Epics 26-35 (10 epics)
```
Day 5-6: EPIC-CCN-26 through EPIC-CCN-30 (5 epics, ~2.5 hours)
Day 7-8: EPIC-CCN-31 through EPIC-CCN-35 (5 epics, ~2.5 hours)

All commits stay on gitbutler/workspace
No PRs created yet
F5 verification after each epic
```

#### Week 3: Execute Epics 36-45 (10 epics) + Batch PR
```
Day 9-10: EPIC-CCN-36 through EPIC-CCN-40 (5 epics, ~2.5 hours)
Day 11-12: EPIC-CCN-41 through EPIC-CCN-45 (5 epics, ~2.5 hours)

Day 13: Create Batch PR
  1. Cherry-pick all 31 epic commits
  2. Create feature/src-epic-ccn-15-45-batch branch
  3. Push to GitHub
  4. Run /pr-loop to 100/100 PHS
  5. Merge PR

Day 14: Sync workspace with main
  git checkout gitbutler/workspace
  git merge origin/main
```

### Benefits of Batch PR Strategy

**Token Efficiency**:
- Traditional: 31 PRs × 5,000 tokens = 155,000 tokens
- Batch: 1 PR × 5,000 tokens = 5,000 tokens
- **Savings**: 150,000 tokens (97% reduction)

**Development Velocity**:
- Traditional: 31 PRs × 2-3 days review = 62-93 days
- Batch: 1 PR × 2-3 days review = 2-3 days
- **Speedup**: 30x faster

**Context Preservation**:
- No branch switching between epics
- No merge conflicts between epics
- No lost work due to branch confusion

**Quality**:
- F5 verification after each epic (immediate feedback)
- Jane Street fixes applied incrementally
- Final PR review catches any missed issues

---

## Hook Integration

### Bob Hooks (`.bob/hooks/`)
- ✅ `after_task.py`: Auto-commit to gitbutler/workspace after task completion
- ✅ `after_task_complete.py`: Task completion handler
- ✅ `before_new_task.py`: Pre-task setup
- ✅ `pre_session.py`: Session initialization
- ✅ `pre_task_jane_street_kb.py`: Jane Street KB loader

### GitButler Hooks (`.git/hooks/`)
- ✅ `pre-commit`: Enforces src-only protection on feature branches (NOT workspace)
- ✅ `post-commit`: Syncs Bob notes to remote

### Hook Behavior on Workspace
- **Workspace commits**: No src-only restriction (all files allowed)
- **Feature branch commits**: Src-only restriction enforced
- **Rationale**: Workspace is local-only, feature branches are for PRs

---

## Migration from Traditional Branches

### Step 1: Identify Active Work
```powershell
# List all unmerged branches
git branch --no-merged main
```

### Step 2: Merge Active Work into Workspace
```powershell
# For each active branch:
git checkout gitbutler/workspace
git merge <branch-name> --no-ff
git branch -d <branch-name>
```

### Step 3: Delete Merged Branches
```powershell
# Run cleanup script
powershell -File .\scripts\cleanup_merged_branches.ps1
```

### Step 4: Update Documentation
```powershell
# This file replaces:
# - docs/protocol/BRANCH_STRATEGY.md (V12.18)
# - docs/workflow/BRANCH_STRATEGY_CLARIFICATION.md
# - docs/workflow/AUTONOMOUS_GITBUTLER_WORKFLOW.md
```

---

## FAQ

### Q: What if I need to create a PR mid-epic?
**A**: Avoid if possible. If urgent, cherry-pick specific commits to a feature branch, create PR, merge, then sync workspace.

### Q: What if I accidentally commit to a feature branch?
**A**: Cherry-pick the commit to gitbutler/workspace, delete the feature branch.

### Q: What if GitButler UI is unavailable?
**A**: Work directly on gitbutler/workspace branch. Commits are still batched, just without virtual branch UI.

### Q: What if I need to switch machines mid-epic?
**A**: Push gitbutler/workspace to GitHub (temporary), pull on new machine, continue work.

### Q: What about emergency hotfixes?
**A**: Create feature branch from main, fix, PR, merge. Then sync workspace with main.

---

## Enforcement

### Branch Guard (Deprecated)
- ❌ `.bob/rules-v12-engineer/branch-guard.md` (no longer enforced)
- ✅ Workspace allows all files (no restrictions)

### Protocol Guard (Deprecated)
- ❌ `.github/workflows/protocol-guard.yml` (no longer needed)
- ✅ Protocol changes committed directly to workspace

### New Enforcement
- ✅ **Social Contract**: All agents use gitbutler/workspace
- ✅ **Documentation**: This file is the single source of truth
- ✅ **Cleanup Script**: `scripts/cleanup_merged_branches.ps1` removes stale branches

---

## Status

**Effective Date**: 2026-06-07 (V12.24)  
**Replaces**: All previous branch strategy docs  
**Next Review**: After EPIC-CCN-15 through EPIC-CCN-45 completion

---

## Summary

**ONE WORKSPACE, ALL WORK**:
- ✅ Use `gitbutler/workspace` for everything
- ✅ Create virtual branches for logical separation
- ✅ Fix Jane Street violations opportunistically
- ✅ Batch commits into PRs at epic completion
- ✅ Maximize velocity, minimize overhead

**Next Action**: Execute EPIC-CCN-15 through EPIC-CCN-45 using this workflow.