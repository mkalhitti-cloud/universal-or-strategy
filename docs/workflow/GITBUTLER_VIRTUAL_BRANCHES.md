# GitButler Virtual Branches Workflow
**Date**: 2026-06-06
**Status**: ACTIVE PROTOCOL
**Replaces**: Manual `git checkout -b` workflow

## Executive Summary

GitButler virtual branches solve the **86-branch problem** by allowing multiple independent feature branches to coexist in a single workspace without creating separate Git branches for each one.

## The Problem We're Solving

### Before GitButler (The 86-Branch Problem)
```
Repository State: 86 branches on GitHub
├── feature/src-epic-ccn-51-reaper-restore (PR #7)
├── epic-ccn-14-propagate-master (no PR)
├── feature/infra-fix-compilation-errors (merged)
├── feature/src-fix-compilation-errors (merged)
├── ... (82 more branches, mostly abandoned)
└── Result: Branch confusion, lost work, merge conflicts
```

**Root Causes**:
1. Creating a new Git branch for every small task
2. Forgetting to delete merged branches
3. Switching between branches loses uncommitted work
4. Multiple branches modifying the same files = 3-way conflicts

### After GitButler (The Solution)
```
Repository State: 4 branches on GitHub
├── main (production)
├── gitbutler/workspace (single workspace branch)
├── feature/src-epic-ccn-51-reaper-restore (PR #7, will be deleted after merge)
└── epic-ccn-14-propagate-master (will be imported as virtual branch)

GitButler Workspace: Multiple virtual branches
├── Virtual Branch 1: "EPIC-CCN-51-REAPER" (REAPER restoration)
├── Virtual Branch 2: "EPIC-CCN-14-PROPAGATE" (PropagateMaster refactoring)
├── Virtual Branch 3: "DOCS-CLEANUP" (documentation updates)
└── All coexist in one workspace, no branch switching needed
```

## How Virtual Branches Work

### Concept
Virtual branches are **logical separations** of work within a single Git branch (`gitbutler/workspace`). GitButler tracks which changes belong to which virtual branch using metadata.

### Key Benefits
1. **No Branch Switching**: All virtual branches visible simultaneously
2. **No Lost Work**: Uncommitted changes stay with their virtual branch
3. **Selective Commits**: Commit only the virtual branches you want
4. **Selective Push**: Push only specific virtual branches to GitHub
5. **No Merge Conflicts**: GitButler handles consolidation intelligently

## Setup Instructions

### 1. Install GitButler
Already installed (detected in repository).

### 2. Initialize Workspace
```bash
# Ensure you're on gitbutler/workspace
git checkout gitbutler/workspace

# Sync with main
git fetch origin main
git merge origin/main --ff-only

# Push to establish remote tracking
git push -u origin gitbutler/workspace
```

### 3. Open GitButler UI
- **Windows**: Launch GitButler from Start Menu
- **Mac**: Launch GitButler from Applications
- **Linux**: Run `gitbutler` from terminal

### 4. Import Existing Branches as Virtual Branches

#### Import PR #7 Branch
1. In GitButler UI, click "Import Branch"
2. Select `feature/src-epic-ccn-51-reaper-restore`
3. Name: "EPIC-CCN-51-REAPER"
4. Description: "REAPER infrastructure restoration + style fix"
5. Click "Import"

#### Import Epic CCN-14 Branch
1. In GitButler UI, click "Import Branch"
2. Select `epic-ccn-14-propagate-master`
3. Name: "EPIC-CCN-14-PROPAGATE"
4. Description: "PropagateMaster complexity reduction (CYC 18→4)"
5. Click "Import"

## Daily Workflow

### Creating a New Feature
**OLD WAY** (Manual Git):
```bash
git checkout -b feature/new-feature
# Work on feature
git add .
git commit -m "feat: new feature"
git push -u origin feature/new-feature
```

**NEW WAY** (GitButler):
1. Open GitButler UI
2. Click "New Virtual Branch"
3. Name: "NEW-FEATURE"
4. Work on feature (files automatically tracked)
5. Commit within GitButler UI
6. Push virtual branch to GitHub (creates real branch automatically)

### Working on Multiple Features Simultaneously
**OLD WAY** (Manual Git):
```bash
git stash                    # Save work
git checkout other-branch    # Switch branches
# Work on other feature
git stash pop               # Restore work (may conflict!)
```

**NEW WAY** (GitButler):
1. All virtual branches visible simultaneously
2. Edit files for Feature A
3. Edit files for Feature B
4. GitButler tracks which changes belong to which branch
5. Commit each virtual branch independently

### Committing Changes
**In GitButler UI**:
1. Select virtual branch
2. Review changes in that branch
3. Write commit message
4. Click "Commit"
5. Changes committed only to that virtual branch

### Pushing to GitHub
**In GitButler UI**:
1. Select virtual branch
2. Click "Push"
3. GitButler creates a real Git branch on GitHub
4. Branch name: `gitbutler/<virtual-branch-name>`

### Creating a Pull Request
**After pushing virtual branch**:
```bash
# GitButler creates: gitbutler/EPIC-CCN-51-REAPER
gh pr create --title "[SRC] EPIC-CCN-51: REAPER restoration" \
             --body "Automated PR from GitButler virtual branch" \
             --base main \
             --head gitbutler/EPIC-CCN-51-REAPER
```

## Migration Plan

### Phase 1: Import Existing Branches ✅ CURRENT
- [x] Abort merge conflict (completed)
- [ ] Import `feature/src-epic-ccn-51-reaper-restore` as virtual branch
- [ ] Import `epic-ccn-14-propagate-master` as virtual branch
- [ ] Verify both branches work independently in GitButler

### Phase 2: Update PR #7
- [ ] Wait for PR #7 to merge to main
- [ ] Delete `feature/src-epic-ccn-51-reaper-restore` from GitHub
- [ ] Sync `gitbutler/workspace` with main

### Phase 3: Complete Epic CCN-14
- [ ] Work on EPIC-CCN-14 virtual branch in GitButler
- [ ] Commit changes
- [ ] Push virtual branch to GitHub
- [ ] Create PR from `gitbutler/EPIC-CCN-14-PROPAGATE` to main
- [ ] Merge and delete branch

### Phase 4: Establish GitButler-First Protocol
- [ ] Update AGENTS.md with GitButler-first mandate
- [ ] Document in `.bob/rules/` for enforcement
- [ ] Train all agents on virtual branch workflow

## Troubleshooting

### "Virtual branch conflicts with another virtual branch"
**Solution**: GitButler detected overlapping changes. Review both branches and decide which changes belong where.

### "Cannot push virtual branch"
**Solution**: Ensure `gitbutler/workspace` is synced with `origin/main` first.

### "Lost uncommitted work"
**Solution**: Check GitButler UI - uncommitted work is tracked per virtual branch. Switch to the correct virtual branch.

### "Want to go back to manual Git"
**Solution**: 
```bash
# Export virtual branch to real Git branch
git checkout -b feature/my-feature
git cherry-pick <commits-from-virtual-branch>
```

## Comparison: Manual Git vs GitButler

| Feature | Manual Git | GitButler Virtual Branches |
|---------|-----------|---------------------------|
| **Branch Creation** | `git checkout -b` | Click "New Virtual Branch" |
| **Branch Switching** | `git checkout` (loses uncommitted work) | No switching needed (all visible) |
| **Uncommitted Work** | Lost on switch (unless stashed) | Tracked per virtual branch |
| **Merge Conflicts** | Frequent (3-way conflicts) | Rare (GitButler handles consolidation) |
| **Branch Cleanup** | Manual deletion (often forgotten) | Automatic after merge |
| **Simultaneous Work** | Impossible (one branch at a time) | Easy (all branches visible) |
| **GitHub Branches** | One per feature (86 branches!) | One workspace + virtual branches (4 branches) |

## Integration with V12 Protocols

### Three-Tier Branch Model
GitButler virtual branches work with the three-tier model:

**Tier 1 (Source)**: `gitbutler/src-*` virtual branches
- Full PR review required
- Pushed to GitHub as `gitbutler/src-*` branches

**Tier 2 (Infrastructure)**: `gitbutler/infra-*` virtual branches
- Direct merge to main (no PR)
- Pushed to GitHub as `gitbutler/infra-*` branches

**Tier 3 (Protocol)**: `gitbutler/protocol-*` virtual branches
- Direct push or optional PR
- Pushed to GitHub as `gitbutler/protocol-*` branches

### Bob CLI Integration
Bob CLI works seamlessly with GitButler:
```bash
# Bob automatically detects GitButler workspace
bob /epic-run EPIC-CCN-15

# Bob commits to current virtual branch
# No manual branch management needed
```

## Success Metrics

### Before GitButler (Baseline)
- 86 branches on GitHub
- 3-way merge conflicts
- Lost work from branch switching
- Confusion about which branch to use

### After GitButler (Target)
- 4 branches on GitHub (main + workspace + 2 active PRs)
- Zero 3-way conflicts
- Zero lost work
- Clear virtual branch organization

## References

- **GitButler Docs**: https://docs.gitbutler.com/features/virtual-branches
- **Conflict Resolution Strategy**: `docs/brain/MERGE_CONFLICT_RESOLUTION_STRATEGY.md`
- **Branch Cleanup Strategy**: `docs/brain/GITHUB_BRANCH_CLEANUP_STRATEGY.md`
- **Three-Tier Branch Model**: `docs/protocol/BRANCH_STRATEGY.md`

## Next Steps

1. **Immediate**: Import 2 existing branches as virtual branches
2. **Short-term**: Complete Epic CCN-14 using virtual branches
3. **Medium-term**: Update AGENTS.md with GitButler-first protocol
4. **Long-term**: Never create manual Git branches again (except for PRs)