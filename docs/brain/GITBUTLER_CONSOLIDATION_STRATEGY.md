# GitButler Workspace Consolidation Strategy

## Objective
Consolidate the 2 remaining active branches into `gitbutler/workspace` to establish GitButler as the single source of truth for all ongoing work.

## Current State

### Branch 1: `feature/src-epic-ccn-51-reaper-restore` (PR #7)
**Status**: Active PR with 29/32 checks failing
**Latest commits**:
- `5ef293d3` - [STYLE] Fix dense one-liner (V12 DNA compliance)
- `b7d032f7` - [SRC] Restore REAPER infrastructure (fixes 42 compilation errors)
- `4a5202a7` - [INFRA] Phase 0 + Phase 1: Epic CCN-51 analysis

**Content**: REAPER restoration work - critical src/ changes

### Branch 2: `epic-ccn-14-propagate-master`
**Status**: Active development, 1/2 checks failing
**Latest commits**:
- `483d60b7` - refactor(epic-ccn-14): reduce PropagateMaster_IdentifyMove from CYC 18 to 4
- `5a8826c0` - infra: Add CODEOWNERS + Phase 1-5 documentation
- `c62c46be` - feat: Enforce .cs-only on ALL PRs to main

**Content**: Epic CCN-14 complexity reduction + infrastructure improvements

### Branch 3: `gitbutler/workspace` (Current)
**Status**: Just synced with main (fast-forward merge)
**Latest commit**: `abc28fd2` - [DOCS] GitHub branch cleanup strategy

## Consolidation Plan

### Phase 1: Merge Both Branches into GitButler Workspace

**Step 1**: Merge `epic-ccn-14-propagate-master` first (infrastructure + refactoring)
```bash
git merge origin/epic-ccn-14-propagate-master --no-edit
```

**Step 2**: Merge `feature/src-epic-ccn-51-reaper-restore` (REAPER restoration)
```bash
git merge origin/feature/src-epic-ccn-51-reaper-restore --no-edit
```

**Step 3**: Resolve any conflicts (if they arise)

**Step 4**: Push consolidated workspace
```bash
git push origin gitbutler/workspace --force-with-lease
```

### Phase 2: Update PR #7 to Point to GitButler Workspace

**Option A**: Close PR #7 and create new PR from gitbutler/workspace
- Cleaner history
- Resets bot checks
- Loses PR #7 comment history

**Option B**: Update PR #7 branch to point to gitbutler/workspace
```bash
git checkout feature/src-epic-ccn-51-reaper-restore
git reset --hard gitbutler/workspace
git push origin feature/src-epic-ccn-51-reaper-restore --force
```
- Preserves PR #7
- Keeps comment history
- May confuse bots initially

**Recommendation**: Option B (preserve PR history)

### Phase 3: Delete Consolidated Branches from GitHub

After successful consolidation:
```bash
git push origin --delete epic-ccn-14-propagate-master
# Keep feature/src-epic-ccn-51-reaper-restore as PR #7 branch
```

## Expected Final State

### GitHub Branches (3 total)
1. `main` - Production branch
2. `gitbutler/workspace` - Single source of truth for all work
3. `feature/src-epic-ccn-51-reaper-restore` - PR #7 (points to same commit as gitbutler/workspace)

### GitButler Virtual Branches
All work will be managed as virtual branches within GitButler:
- Epic CCN-14 work (complexity reduction)
- Epic CCN-51 work (REAPER restoration)
- Future epics (all managed via GitButler)

## Benefits

1. **Single Source of Truth**: All work in one place
2. **No More Branch Confusion**: GitButler manages virtual branches
3. **Easier Context Switching**: Switch between epics without git checkout
4. **Atomic Commits**: GitButler allows partial staging per virtual branch
5. **No Lost Work**: GitButler prevents the 86-branch problem

## GitButler Protocol (New Standard)

### For All Future Work:
1. ✅ **Always work in `gitbutler/workspace`**
2. ✅ **Create virtual branches in GitButler UI for each epic/feature**
3. ✅ **Use GitButler's commit interface (not git commit)**
4. ✅ **Push virtual branches to GitHub via GitButler**
5. ✅ **Create PRs from GitButler-managed branches**

### Banned Practices:
- ❌ **No more `git checkout -b` for new features**
- ❌ **No more manual branch management**
- ❌ **No more forgetting to push branches**
- ❌ **No more losing work across 86 branches**

## Rollback Plan

If consolidation fails:
1. Both original branches still exist on GitHub
2. Can recreate from origin refs
3. GitButler workspace can be reset to main

## Next Steps

1. Execute Phase 1 (merge both branches)
2. Verify no conflicts
3. Push consolidated workspace
4. Execute Phase 2 (update PR #7)
5. Execute Phase 3 (cleanup)
6. Document GitButler workflow in AGENTS.md