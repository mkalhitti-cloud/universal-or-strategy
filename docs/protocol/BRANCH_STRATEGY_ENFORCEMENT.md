# Branch Strategy Enforcement Protocol (V12.24)

**Status**: ACTIVE
**Effective Date**: 2026-06-09
**Triggered By**: EPIC-CCN-13 Protocol Bypass Incident

## Executive Summary

V12 Universal OR Strategy mandates **GitButler virtual branches ONLY** for all development work. Regular git branches are BANNED to prevent merge conflicts, branch confusion, and protocol violations.

## Branch Types

### ✅ ALLOWED: GitButler Virtual Branches

**What**: Logical branches that exist only in GitButler UI
**Physical Branch**: All work accumulates on `gitbutler/workspace`
**Creation**: `but branch new <name>`
**Benefits**:
- Zero merge conflicts (same physical branch)
- Visual UI management (drag-drop commits)
- Automatic isolation without divergence
- Perfect for parallel epic execution

**Example**:
```bash
# Create virtual branch for EPIC-CCN-14
but branch new epic-ccn-14

# All commits go to gitbutler/workspace
# GitButler UI shows them under "epic-ccn-14" virtual branch
```

### ✅ ALLOWED: Git Worktrees (Alternative)

**What**: Separate working directories with their own branches
**Use Case**: True isolation for testing competing approaches
**Creation**: `git worktree add <path> <branch>`
**Benefits**:
- Complete isolation (separate directories)
- No risk of wrong-branch work
- No GitButler dependency

**Example**:
```bash
# Create worktree for experimental approach
git worktree add ../universal-or-strategy-experiment experiment-branch

# Work in separate directory
cd ../universal-or-strategy-experiment

# When done, merge and remove
cd ../universal-or-strategy
git worktree remove ../universal-or-strategy-experiment
```

### ❌ BANNED: Regular Git Branches

**What**: Traditional `git checkout -b` branches
**Why Banned**:
- Cause merge conflicts (EPIC-CCN-13 vs EPIC-CCN-51 BUILD_TAG conflict)
- Break GitButler single-workspace model
- Prone to wrong-branch execution (epic-ccn-13-pr incident)
- No visual UI management

**Examples of BANNED branches**:
- `epic-ccn-13-pr` (regular branch, caused protocol violation)
- `feature/new-indicator` (regular branch)
- `fix/bug-123` (regular branch)

## Enforcement Mechanism

### 1. Pre-Flight Check (epic-run Phase -1)

**Location**: epic-run command orchestrator
**Timing**: Before Phase 0 (Architectural Exploration)

**Implementation**:
```python
def verify_branch_strategy():
    """
    Enforce GitButler virtual branch strategy.
    
    Checks:
    1. Current branch is gitbutler/workspace
    2. No regular branches exist (except main)
    
    Raises:
        RuntimeError: If regular branches detected or wrong branch
    """
    # Check 1: Verify current branch
    result = subprocess.run(
        ['git', 'branch', '--show-current'],
        capture_output=True,
        text=True
    )
    
    if result.returncode != 0:
        raise RuntimeError("Failed to get current branch")
    
    current_branch = result.stdout.strip()
    
    if current_branch != 'gitbutler/workspace':
        raise RuntimeError(
            f"PROTOCOL VIOLATION: Epic must run on gitbutler/workspace\n"
            f"Current branch: {current_branch}\n"
            f"Fix: git checkout gitbutler/workspace"
        )
    
    # Check 2: Verify no regular branches exist
    result = subprocess.run(
        ['git', 'branch', '--list'],
        capture_output=True,
        text=True
    )
    
    branches = [
        b.strip().lstrip('* ') 
        for b in result.stdout.split('\n') 
        if b.strip()
    ]
    
    # Allowed: main, gitbutler/workspace, gitbutler/* (virtual branches)
    allowed_prefixes = ('main', 'gitbutler/')
    regular_branches = [
        b for b in branches 
        if not any(b.startswith(prefix) for prefix in allowed_prefixes)
    ]
    
    if regular_branches:
        raise RuntimeError(
            f"PROTOCOL VIOLATION: Regular git branches detected\n"
            f"Banned branches: {', '.join(regular_branches)}\n"
            f"V12 mandates GitButler virtual branches only.\n\n"
            f"Migration options:\n"
            f"1. Delete: git branch -D {' '.join(regular_branches)}\n"
            f"2. Convert to worktree: git worktree add ../<repo>-<branch> <branch>\n\n"
            f"Then create virtual branch: but branch new <name>"
        )
    
    print(f"[PREFLIGHT] ✓ Branch strategy verified: {current_branch}")
    print(f"[PREFLIGHT] ✓ No regular branches detected")
```

### 2. Hook Hardening (after_task.py)

**Enhancement**: Add epic context awareness

**Implementation**:
```python
# Modify after_task.py to fail loudly during epics
def verify_branch_for_epic():
    """Fail loudly if wrong branch during epic execution."""
    success, stdout, _ = run_command("git branch --show-current")
    
    if not success:
        return False
    
    current_branch = stdout.strip()
    
    # Check if we're in an epic context
    is_epic = os.environ.get('EPIC_RUN_ACTIVE') == '1'
    
    if current_branch != 'gitbutler/workspace':
        if is_epic:
            # FAIL LOUDLY during epic
            print(f"[after_task] CRITICAL: Epic running on wrong branch!")
            print(f"[after_task] Current: {current_branch}")
            print(f"[after_task] Expected: gitbutler/workspace")
            print(f"[after_task] Epic ID: {os.environ.get('EPIC_ID', 'unknown')}")
            return False  # BLOCK
        else:
            # Silent skip for non-epic tasks
            print(f"[after_task] WARNING: Not on gitbutler/workspace")
            print(f"[after_task] Skipping auto-commit for safety")
            return True  # ALLOW
    
    return True
```

### 3. Git Hook (pre-commit)

**Optional**: Add git-level enforcement

**Location**: `.git/hooks/pre-commit`

**Implementation**:
```bash
#!/bin/bash
# V12 Branch Strategy Enforcement

CURRENT_BRANCH=$(git rev-parse --abbrev-ref HEAD)

# Allow main and gitbutler/* branches
if [[ "$CURRENT_BRANCH" == "main" ]] || [[ "$CURRENT_BRANCH" == gitbutler/* ]]; then
    exit 0
fi

# Block regular branches
echo "ERROR: Regular git branches are banned in V12"
echo "Current branch: $CURRENT_BRANCH"
echo ""
echo "V12 mandates GitButler virtual branches only."
echo "Migration: git checkout gitbutler/workspace && but branch new <name>"
exit 1
```

## Migration Guide

### For Existing Regular Branches

**Step 1**: Identify regular branches
```bash
git branch --list | grep -v "main\|gitbutler"
```

**Step 2**: Choose migration path

**Option A: Move to Virtual Branch** (Recommended)
```bash
# Switch to workspace
git checkout gitbutler/workspace

# Create virtual branch
but branch new <descriptive-name>

# Cherry-pick commits from regular branch
git cherry-pick <commit-hash-1> <commit-hash-2> ...

# Delete regular branch
git branch -D <regular-branch-name>
```

**Option B: Convert to Worktree** (For isolation needs)
```bash
# Create worktree from regular branch
git worktree add ../<repo-name>-<branch-name> <branch-name>

# Work in separate directory
cd ../<repo-name>-<branch-name>

# When done, merge back and remove worktree
cd <original-repo>
git merge <branch-name>
git worktree remove ../<repo-name>-<branch-name>
```

**Step 3**: Verify cleanup
```bash
# Should only show: main, gitbutler/workspace, gitbutler/*
git branch --list
```

### For EPIC-CCN-13 Recovery

**Current State**:
- 5 commits on `epic-ccn-13-pr` (regular branch)
- Need to move to `gitbutler/workspace`

**Recovery Steps**:
```bash
# 1. Switch to workspace
git checkout gitbutler/workspace

# 2. Create virtual branch for EPIC-CCN-13
but branch new epic-ccn-13

# 3. Cherry-pick commits (resolve BUILD_TAG conflict)
git cherry-pick bc20fe02  # ticket-01
git cherry-pick ec951871  # ticket-02
git cherry-pick 50f119e3  # ticket-03 (BUILD_TAG conflict - take EPIC-CCN-13 version)
git cherry-pick fe6f14d1  # ticket-04
git cherry-pick ec713754  # ticket-05

# 4. Delete regular branch
git branch -D epic-ccn-13-pr

# 5. Verify
git log --oneline -5
but branch list
```

## AGENTS.md Integration

**Add to Section 2 (Architectural Mandates)**:

```markdown
- **Branch Strategy Mandate (V12.24)**: 
  * PRIMARY: GitButler virtual branches ONLY (`but branch new <name>`).
  * ALTERNATIVE: Git worktrees for true isolation (`git worktree add`).
  * BANNED: Regular git branches (`git checkout -b`) for development work.
  * ENFORCEMENT: epic-run Phase -1 MUST verify branch strategy compliance.
  * VIOLATION: P0 blocker - epic will not start.
  * REFERENCE: See `docs/protocol/BRANCH_STRATEGY_ENFORCEMENT.md` for details.
```

## Rationale

### Why Virtual Branches?

1. **Zero Merge Conflicts**: All work on same physical branch
2. **Visual Management**: GitButler UI provides drag-drop commit organization
3. **Parallel Execution**: Multiple epics can run simultaneously without conflicts
4. **Protocol Simplicity**: Single workspace model is easier to reason about
5. **Hook Compatibility**: after_task.py auto-commit works seamlessly

### Why Ban Regular Branches?

1. **EPIC-CCN-13 Incident**: Regular branch caused protocol bypass
2. **Merge Conflicts**: BUILD_TAG conflicts between EPIC-CCN-13 and EPIC-CCN-51
3. **Wrong-Branch Risk**: Easy to accidentally work on wrong branch
4. **No UI Management**: Regular branches lack visual organization
5. **Hook Bypass**: Hooks skip auto-commit on non-workspace branches

## Exceptions

**NONE**: This protocol has zero exceptions. All development work MUST use virtual branches or worktrees.

**Rationale**: Consistency is critical for autonomous multi-agent workflows. Exceptions create confusion and protocol violations.

## Monitoring

**Metrics to Track**:
- Number of regular branches detected per week
- Number of epic-run pre-flight failures due to branch violations
- Number of cherry-pick operations (should trend to zero)

**Success Criteria**:
- Zero regular branches exist after 2-week migration period
- Zero epic-run pre-flight failures after migration
- Zero cherry-pick operations after migration

## Sign-Off

**Author**: Advanced Mode (Bob CLI)
**Approved By**: [Pending Director Approval]
**Effective Date**: 2026-06-09
**Review Date**: 2026-07-09 (30-day review)

---

**Related Documents**:
- `docs/brain/EPIC-CCN-13/PROTOCOL_BYPASS_FORENSIC_REPORT.md` (Root cause analysis)
- `AGENTS.md` Section 2 (Architectural Mandates)
- `.bob/hooks/after_task.py` (Hook implementation)