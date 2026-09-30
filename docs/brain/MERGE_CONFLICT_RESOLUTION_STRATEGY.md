# Merge Conflict Resolution Strategy
**Date**: 2026-06-06
**Branch**: `gitbutler/workspace` ← `epic-ccn-14-propagate-master`
**Conflicts**: 15 files (13 docs, 1 script, 1 source)

## Executive Summary

We're attempting to consolidate `epic-ccn-14-propagate-master` into `gitbutler/workspace` but hit 15 merge conflicts. The conflicts reveal a critical issue: **both branches contain important but divergent work**.

## Critical Finding: src/V12_002.cs Conflict

### The Problem
`src/V12_002.cs` is conflicted AND is currently under review in **PR #7** (`feature/src-epic-ccn-51-reaper-restore`).

### Git History Analysis
```
* c55323b2 (feature/src-epic-ccn-51-reaper-restore) [STYLE] Fix dense one-liner
* e6933ce4 [SRC] Restore REAPER infrastructure - fix 42 compilation errors
  |
  | * 483d60b7 (epic-ccn-14-propagate-master) refactor: reduce PropagateMaster_IdentifyMove CYC 18→4
  |
* face9224 (origin/gitbutler/workspace) [SRC] Fix CS0102 duplicate field
* a48dc8c3 fix: resolve 3 VALID-FIX issues
```

**Key Insight**: 
- PR #7 branch (`feature/src-epic-ccn-51-reaper-restore`) contains REAPER restoration work
- `epic-ccn-14-propagate-master` contains PropagateMaster complexity reduction
- `gitbutler/workspace` contains CS0102 duplicate field fix
- **All three branches modified the same file independently**

## Conflict Breakdown

### 1. scripts/complexity_audit.py ✅ RESOLVED
**Winner**: `epic-ccn-14-propagate-master` version

**Rationale**:
- V12.24 GODMODE with Jane Street strict threshold (CYC ≤ 8)
- Aligns with AGENTS.md Jane Street mandate
- Better than V12.22 threshold of 20
- Includes GODMODE branding and messaging

**Action**: Accept incoming version (epic-ccn-14-propagate-master)

### 2. src/V12_002.cs ⚠️ CRITICAL - REQUIRES ANALYSIS
**Status**: BLOCKED - Cannot resolve without understanding PR #7 changes

**The Dilemma**:
- **HEAD (gitbutler/workspace)**: Contains CS0102 duplicate field fix + prior work
- **Incoming (epic-ccn-14-propagate-master)**: Contains PropagateMaster_IdentifyMove refactoring (CYC 18→4)
- **PR #7 (feature/src-epic-ccn-51-reaper-restore)**: Contains REAPER restoration + style fix

**Risk**: 
- If we accept either version blindly, we may:
  - Lose the REAPER restoration work (42 compilation errors return)
  - Lose the PropagateMaster complexity reduction
  - Lose the CS0102 duplicate field fix
  - Break PR #7 which is currently under review

**Required Analysis**:
1. Check if PR #7 already includes the PropagateMaster refactoring
2. Check if epic-ccn-14 includes the REAPER restoration
3. Determine which version is "newer" or "more complete"

### 3. Documentation Conflicts (13 files) 📄 LOW PRIORITY
**Files**:
- `docs/brain/AUTONOMOUS_REFACTOR_GAP_ANALYSIS.md`
- `docs/brain/AUTONOMOUS_REFACTOR_MODES_ANALYSIS.md`
- `docs/brain/EPIC-CCN-13/00-scope.md`
- `docs/brain/EPIC-CCN-14/00-scope.md`
- `docs/brain/EPIC-CCN-14/01-implementation-plan.md`
- `docs/brain/EPIC-CCN-14/02-tickets.md`
- `docs/brain/JANE_STREET_KB_TOKEN_ANALYSIS.md`
- `docs/brain/autonomous_refactor_baseline_corrected.md`
- `docs/brain/autonomous_refactor_progress.md`
- `docs/brain/session_snapshot_2026-06-04-autonomous-refactor.json`
- `docs/brain/session_snapshot_2026-06-04-epic-ccn-13-corrected.json`
- `docs/brain/session_snapshot_2026-06-04-epic-ccn-13.json`
- `docs/protocol/BOT_INSTRUCTIONS_UNIVERSAL.md`

**Strategy**: Accept incoming (epic-ccn-14) versions - these are newer planning documents

## Recommended Resolution Strategy

### Option A: Abort Merge and Use GitButler Virtual Branches ✅ RECOMMENDED
**Rationale**: 
- Avoids complex 3-way merge conflicts
- Preserves all work independently
- Lets GitButler handle consolidation intelligently
- Prevents breaking PR #7

**Steps**:
1. Abort current merge: `git merge --abort`
2. Open GitButler UI
3. Import both branches as virtual branches:
   - Virtual Branch 1: "EPIC-CCN-51-REAPER" (from feature/src-epic-ccn-51-reaper-restore)
   - Virtual Branch 2: "EPIC-CCN-14-PROPAGATE" (from epic-ccn-14-propagate-master)
4. Let GitButler manage the consolidation
5. Work on each epic independently in GitButler

**Advantages**:
- ✅ No manual conflict resolution needed
- ✅ Preserves all work
- ✅ Doesn't break PR #7
- ✅ Aligns with GitButler-first workflow

### Option B: Manual 3-Way Merge (NOT RECOMMENDED)
**Why Not**:
- ❌ Requires deep understanding of all 3 branches
- ❌ High risk of losing work
- ❌ May break PR #7
- ❌ Time-consuming (15 conflicts)
- ❌ Doesn't solve the root problem (too many branches)

### Option C: Sequential Merging (ALTERNATIVE)
**Strategy**:
1. Abort current merge
2. Wait for PR #7 to merge to main
3. Sync gitbutler/workspace with main
4. Then merge epic-ccn-14-propagate-master
5. Conflicts will be simpler (only 2-way, not 3-way)

**Advantages**:
- ✅ Simpler conflict resolution
- ✅ Doesn't risk breaking PR #7
- ✅ Cleaner git history

**Disadvantages**:
- ⏱️ Requires waiting for PR #7 to complete
- ⏱️ Delays consolidation

## Decision Matrix

| Criterion | Option A (GitButler) | Option B (Manual) | Option C (Sequential) |
|-----------|---------------------|-------------------|----------------------|
| **Risk** | Low | High | Medium |
| **Time** | Fast | Slow | Medium |
| **Complexity** | Low | High | Medium |
| **PR #7 Safety** | ✅ Safe | ❌ Risky | ✅ Safe |
| **Alignment with Protocol** | ✅ Yes | ❌ No | ⚠️ Partial |

## Recommendation

**ABORT MERGE and use Option A (GitButler Virtual Branches)**

This is the cleanest path forward because:
1. It's what GitButler was designed for
2. It prevents the 86-branch problem from recurring
3. It doesn't risk breaking PR #7
4. It aligns with the new GitButler-first workflow

## Next Steps

1. **Immediate**: Abort merge
   ```bash
   git merge --abort
   ```

2. **Short-term**: Document GitButler virtual branch workflow
   - Create `docs/workflow/GITBUTLER_VIRTUAL_BRANCHES.md`
   - Update AGENTS.md with GitButler-first protocol

3. **Medium-term**: Import branches into GitButler
   - Open GitButler UI
   - Import `feature/src-epic-ccn-51-reaper-restore` as virtual branch
   - Import `epic-ccn-14-propagate-master` as virtual branch

4. **Long-term**: Establish GitButler as primary workflow
   - No more manual `git checkout -b`
   - All feature work in virtual branches
   - Single workspace branch on GitHub

## Lessons Learned

1. **Don't merge branches with overlapping src/ changes** - This creates 3-way conflicts
2. **GitButler virtual branches prevent this problem** - Each epic stays isolated
3. **PR #7 should have been created from gitbutler/workspace** - Not a separate branch
4. **The 86-branch problem was a symptom** - Root cause: not using GitButler properly

## References

- PR #7: https://github.com/antigravityos187-sketch/universal-or-strategy/pull/7
- GitButler Docs: https://docs.gitbutler.com/features/virtual-branches
- Three-Tier Branch Model: `docs/protocol/BRANCH_STRATEGY.md`