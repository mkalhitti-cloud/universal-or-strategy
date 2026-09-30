# Branch Investigation Report - 3 NEEDS_PR Candidates

**Date**: 2026-06-06
**Purpose**: Investigate whether these branches represent completed work that wasn't merged or were superseded

---

## Executive Summary

**Status**: ✅ **Jane Street docs extracted** (13 files committed to main)

**Remaining Investigation**: 3 branches with .cs files need deeper analysis to determine if they're:
1. Completed work that should be merged
2. Superseded by other work already in main
3. Abandoned/obsolete work

---

## Branch 1: feature/src-epic-ccn-51-reaper-restore

### Metadata
- **Age**: 0 days (2026-06-06 13:26:14) - **FRESH**
- **Ahead/Behind**: 2 commits ahead, 33 behind
- **Files**: 1 file (`src/V12_002.cs`)
- **PR Status**: PR #7 exists with 100/100 PHS

### Commits
1. `c55323b2` - [STYLE] Fix dense one-liner - convert GetPhotonDispatchRingDepth to block body
2. `e6933ce4` - [SRC] Restore REAPER infrastructure declarations - fix 42 compilation errors

### Analysis Needed
- ❓ Are these commits already in main?
- ❓ Does main currently have `GetPhotonDispatchRingDepth` method?
- ❓ Was this work superseded by a different approach?
- ❓ Is PR #7 still open or was it closed/merged?

### Recommendation
**INVESTIGATE FURTHER** - Check if:
1. PR #7 is open/closed/merged
2. The REAPER infrastructure is already in main
3. These 42 compilation errors were fixed differently

---

## Branch 2: epic-ccn-14-src-only

### Metadata
- **Age**: 2 days (2026-06-04 14:14:20) - **RECENT**
- **Ahead/Behind**: 1 commit ahead, 53 behind
- **Files**: 2 files
  - `src/V12_002.Orders.Callbacks.Propagation.cs`
  - `src/V12_002.cs`

### Commit
`ce7e9421` - refactor(epic-ccn-14): reduce PropagateMaster_IdentifyMove from CYC 18 to 4
- Extracted 2 helper methods:
  - `ScanOrderDictionaryForMaster` (CYC 5)
  - `ScanTargetDictionariesForMaster` (CYC 4)
- Reduced main method from CYC 18 to 4 (77.8% reduction)
- BUILD_TAG: 1111.011-epic-ccn-14

### Analysis Needed
- ❓ Does main currently have `PropagateMaster_IdentifyMove` method?
- ❓ If yes, does it have the helper methods (refactor already applied)?
- ❓ If no, was this method renamed or removed?
- ❓ Is there an EPIC-CCN-14 in docs/brain/?

### Recommendation
**INVESTIGATE FURTHER** - Check if:
1. The refactoring is already in main
2. EPIC-CCN-14 documentation exists
3. This was superseded by epic-ccn-14-propagate-master

---

## Branch 3: epic-ccn-14-propagate-master

### Metadata
- **Age**: 2 days (2026-06-04 13:46:45) - **RECENT**
- **Ahead/Behind**: 1 commit ahead, 53 behind
- **Files**: 90 total (2 .cs, 14 docs, 3 .bob, 1 script, 30 config, 42 other)
  - **CS**: Same 2 files as epic-ccn-14-src-only
  - **Docs**: EPIC-CCN-13, EPIC-CCN-14, autonomous refactor analysis
  - **Bob**: autonomous-refactor.md, pre_task_jane_street_kb.py, settings.json
  - **Deleted**: 42 temporary/diagnostic files (build logs, diffs, etc.)

### Commit
`483d60b7` - Same refactoring as epic-ccn-14-src-only but with additional infrastructure:
- Same CYC reduction (18 → 4)
- Same helper methods
- BUILD_TAG: 1111.011-epic-ccn-14
- **PLUS**: Added autonomous refactor infrastructure + cleaned up temp files

### Analysis Needed
- ❓ Is this the "complete" version of epic-ccn-14-src-only?
- ❓ Are the EPIC-CCN-14 docs already in main?
- ❓ Is the autonomous-refactor.md command already in .bob/?
- ❓ Should we merge this instead of epic-ccn-14-src-only?

### Recommendation
**LIKELY SUPERSEDES epic-ccn-14-src-only** - This appears to be the full epic including:
- Same src/ refactoring
- Documentation
- Infrastructure improvements
- Cleanup of temp files

---

## Key Questions to Answer

### For All 3 Branches:
1. **Commit Status**: Are these commits already in main or gitbutler/workspace?
2. **Content Status**: Is the actual code change already present in main?
3. **Documentation Status**: Do the related docs exist in main?
4. **Supersession**: Was this work completed via a different branch/PR?

### Specific Checks Needed:

**feature/src-epic-ccn-51-reaper-restore**:
```bash
# Check if commits are in main
git branch --contains c55323b2
git branch --contains e6933ce4

# Check if method exists in main
git show main:src/V12_002.cs | grep "GetPhotonDispatchRingDepth"

# Check PR #7 status
gh pr view 7
```

**epic-ccn-14-src-only & epic-ccn-14-propagate-master**:
```bash
# Check if commits are in main
git branch --contains ce7e9421
git branch --contains 483d60b7

# Check if refactoring is in main
git show main:src/V12_002.Orders.Callbacks.Propagation.cs | grep "ScanOrderDictionaryForMaster"

# Check if EPIC-CCN-14 docs exist
ls docs/brain/EPIC-CCN-14/

# Check if autonomous-refactor command exists
ls .bob/commands/autonomous-refactor.md
```

---

## Next Steps

1. **Run the verification commands above**
2. **Compare branch content with main**
3. **Check for related PRs** (gh pr list --search "epic-ccn-14" or "reaper")
4. **Make merge decision** based on findings:
   - If already in main → Delete branch
   - If superseded → Delete branch, keep superseding branch
   - If unique work → Create PR or merge directly

---

## Jane Street Documentation Recovery ✅

**Status**: COMPLETE
- Extracted 13 files from `docs/jane-street-deviations-3-4`
- Committed to main: `86a17596`
- **CRITICAL FILE SAVED**: `docs/standards/JANE_STREET_DEVIATIONS.md`
- Skipped 6 obsolete .cs files (8 days old)
