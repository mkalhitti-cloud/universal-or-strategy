# GitButler Virtual Branches - Next Steps
**Date**: 2026-06-06
**Status**: Ready for GitButler UI Import
**Current Branch**: `gitbutler/workspace` (synced with main)

## Current State ✅

### Repository Cleanup Complete
- **GitHub Branches**: Reduced from 86 → 4 branches
  - `main` (production)
  - `gitbutler/workspace` (workspace branch, synced with main)
  - `feature/src-epic-ccn-51-reaper-restore` (PR #7, active)
  - `epic-ccn-14-propagate-master` (ready for import)

### Merge Conflict Resolved
- ✅ Aborted problematic merge (15 conflicts)
- ✅ Created conflict resolution strategy document
- ✅ Created GitButler workflow documentation
- ✅ Synced `gitbutler/workspace` with `main`

### Documentation Created
- ✅ `docs/brain/MERGE_CONFLICT_RESOLUTION_STRATEGY.md` - Analysis of 3-way conflict
- ✅ `docs/workflow/GITBUTLER_VIRTUAL_BRANCHES.md` - Complete workflow guide
- ✅ `docs/brain/GITBUTLER_NEXT_STEPS.md` - This file

## Next Steps - GitButler UI Actions

### Step 1: Open GitButler UI
**Action**: Launch GitButler application

**Windows**:
```
Start Menu → GitButler
```

**Mac**:
```
Applications → GitButler
```

**Linux**:
```bash
gitbutler
```

### Step 2: Verify Workspace
**Action**: Confirm GitButler recognizes the repository

**Expected**:
- Repository: `universal-or-strategy`
- Current Branch: `gitbutler/workspace`
- Status: Clean (no uncommitted changes)

### Step 3: Import First Virtual Branch (PR #7)
**Action**: Import `feature/src-epic-ccn-51-reaper-restore` as virtual branch

**Steps**:
1. Click "Import Branch" button
2. Select branch: `feature/src-epic-ccn-51-reaper-restore`
3. Virtual Branch Name: `EPIC-CCN-51-REAPER`
4. Description: `REAPER infrastructure restoration + style fix (PR #7)`
5. Click "Import"

**Expected Result**:
- Virtual branch created
- All commits from PR #7 visible in GitButler
- Files: `src/V12_002.cs` changes visible

**Why This Branch**:
- Currently under review as PR #7
- Contains REAPER restoration work (42 compilation errors fixed)
- Contains style fix (dense one-liner converted to block body)

### Step 4: Import Second Virtual Branch (Epic CCN-14)
**Action**: Import `epic-ccn-14-propagate-master` as virtual branch

**Steps**:
1. Click "Import Branch" button
2. Select branch: `epic-ccn-14-propagate-master`
3. Virtual Branch Name: `EPIC-CCN-14-PROPAGATE`
4. Description: `PropagateMaster complexity reduction (CYC 18→4) + GODMODE complexity audit`
5. Click "Import"

**Expected Result**:
- Virtual branch created
- All commits from epic-ccn-14 visible in GitButler
- Files: `src/V12_002.cs` (PropagateMaster refactoring) + `scripts/complexity_audit.py` (GODMODE version)

**Why This Branch**:
- Contains PropagateMaster_IdentifyMove refactoring (CYC 18→4)
- Contains V12.24 GODMODE complexity audit (Jane Street strict threshold CYC ≤ 8)
- No PR yet (will create after import)

### Step 5: Verify Both Virtual Branches Coexist
**Action**: Confirm both branches are visible simultaneously

**Expected**:
- Virtual Branch 1: `EPIC-CCN-51-REAPER` (visible)
- Virtual Branch 2: `EPIC-CCN-14-PROPAGATE` (visible)
- No conflicts between branches (GitButler handles this)

**Key Insight**:
Both branches modified `src/V12_002.cs` independently, but GitButler tracks which changes belong to which virtual branch. No 3-way merge conflicts!

### Step 6: Work on Epic CCN-14 (Example Workflow)
**Action**: Make changes to EPIC-CCN-14 virtual branch

**Steps**:
1. Select `EPIC-CCN-14-PROPAGATE` virtual branch in GitButler
2. Edit files as needed
3. GitButler automatically tracks changes to this virtual branch
4. Commit within GitButler UI
5. Push virtual branch to GitHub (creates `gitbutler/EPIC-CCN-14-PROPAGATE` branch)

**Note**: This is just an example. You can work on either virtual branch at any time.

## Key Differences: Old vs New Workflow

### OLD WAY (Manual Git - The 86-Branch Problem)
```bash
# Working on PR #7
git checkout feature/src-epic-ccn-51-reaper-restore
# Edit files
git add .
git commit -m "fix"

# Want to work on Epic CCN-14
git stash                                    # Save work
git checkout epic-ccn-14-propagate-master   # Switch branches
# Edit files
git add .
git commit -m "refactor"

# Go back to PR #7
git checkout feature/src-epic-ccn-51-reaper-restore
git stash pop                                # Restore work (may conflict!)

# Result: 86 branches, lost work, confusion
```

### NEW WAY (GitButler Virtual Branches)
```
# Open GitButler UI
# Both virtual branches visible simultaneously:
#   - EPIC-CCN-51-REAPER
#   - EPIC-CCN-14-PROPAGATE

# Edit files for PR #7
# GitButler tracks: "These changes belong to EPIC-CCN-51-REAPER"

# Edit files for Epic CCN-14
# GitButler tracks: "These changes belong to EPIC-CCN-14-PROPAGATE"

# Commit each virtual branch independently
# No branch switching, no stashing, no conflicts

# Result: 4 GitHub branches, no lost work, clear organization
```

## Troubleshooting

### "GitButler doesn't see my branches"
**Solution**: Ensure you're in the repository directory and GitButler is pointed to the correct repo.

### "Import failed"
**Solution**: 
1. Verify branch exists: `git branch -a | grep <branch-name>`
2. Ensure `gitbutler/workspace` is synced with main
3. Try restarting GitButler

### "Changes conflict between virtual branches"
**Solution**: GitButler will show which changes conflict. Review both virtual branches and decide which changes belong where.

### "Want to delete a virtual branch"
**Solution**: In GitButler UI, right-click virtual branch → Delete. This doesn't delete the Git branch, only the virtual branch tracking.

## Success Criteria

### Phase 1: Import Complete ✅
- [ ] GitButler UI opened
- [ ] `EPIC-CCN-51-REAPER` virtual branch imported
- [ ] `EPIC-CCN-14-PROPAGATE` virtual branch imported
- [ ] Both branches visible simultaneously
- [ ] No conflicts reported

### Phase 2: PR #7 Completion
- [ ] PR #7 reviewed and merged to main
- [ ] `feature/src-epic-ccn-51-reaper-restore` deleted from GitHub
- [ ] `EPIC-CCN-51-REAPER` virtual branch deleted from GitButler
- [ ] `gitbutler/workspace` synced with main

### Phase 3: Epic CCN-14 Completion
- [ ] Work on `EPIC-CCN-14-PROPAGATE` virtual branch
- [ ] Commit changes
- [ ] Push virtual branch to GitHub (creates `gitbutler/EPIC-CCN-14-PROPAGATE`)
- [ ] Create PR from `gitbutler/EPIC-CCN-14-PROPAGATE` to main
- [ ] PR reviewed and merged
- [ ] Branch deleted from GitHub
- [ ] Virtual branch deleted from GitButler

### Phase 4: GitButler-First Protocol Established
- [ ] Update AGENTS.md with GitButler-first mandate
- [ ] Create `.bob/rules/gitbutler-first.md` for enforcement
- [ ] All future work uses virtual branches
- [ ] No more manual `git checkout -b` (except for emergency fixes)

## Key Files to Review

### Workflow Documentation
- `docs/workflow/GITBUTLER_VIRTUAL_BRANCHES.md` - Complete workflow guide
- `docs/brain/MERGE_CONFLICT_RESOLUTION_STRATEGY.md` - Why we chose GitButler

### Branch Analysis
- `docs/brain/GITHUB_BRANCH_CLEANUP_STRATEGY.md` - How we got from 86→4 branches
- `docs/brain/BRANCH_CONSOLIDATION_FINAL_SUMMARY.md` - Tier 6 consolidation results

### Protocol Documentation
- `docs/protocol/BRANCH_STRATEGY.md` - Three-Tier Branch Model
- `AGENTS.md` - Agent rules (will be updated with GitButler-first mandate)

## Questions?

### "Why GitButler instead of manual Git?"
**Answer**: Prevents the 86-branch problem. Virtual branches let you work on multiple features simultaneously without branch switching or stashing.

### "What if I need to use manual Git?"
**Answer**: You can still use Git commands. GitButler is a layer on top of Git, not a replacement. But for daily work, GitButler is recommended.

### "Can I mix GitButler and manual Git?"
**Answer**: Yes, but not recommended. Pick one workflow and stick with it. Mixing workflows can cause confusion.

### "What happens to my uncommitted work?"
**Answer**: GitButler tracks uncommitted work per virtual branch. No more lost work from branch switching!

## Next Action Required

**IMMEDIATE**: Open GitButler UI and import the 2 branches following Steps 1-5 above.

**After Import**: Report back with:
- ✅ Both virtual branches imported successfully
- ✅ No conflicts detected
- ✅ Ready to work on either branch

Then we can proceed with completing Epic CCN-14 or waiting for PR #7 to merge.