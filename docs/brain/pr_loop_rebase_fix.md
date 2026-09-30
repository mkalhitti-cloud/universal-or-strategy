# PR Loop Rebase Contamination Fix

**Date**: 2026-05-27  
**Issue**: PR #8 Step 0 attempted rebase and staged non-src files  
**Severity**: P1 - Protocol Violation  

## Problem Statement

During PR Loop execution for PR #8 (src-only changes), Step 0 (Pre-Flight Hygiene) performed a blind `git rebase origin/main` without checking PR scope. This caused non-src files (`plugins/check-pr/SKILL.md`) to be staged, violating the src-only protocol for that PR.

### Root Cause

The original Step 0 protocol did not differentiate between:
- **SRC-ONLY PRs**: Changes confined to `src/` directory
- **MIXED PRs**: Changes spanning multiple directories

Rebasing a src-only PR against `main` risks contamination when `main` contains recent non-src commits that aren't relevant to the PR's scope.

## Solution

### 1. New Script: `check_pr_scope.ps1`

Created a PowerShell script that analyzes PR file scope:

```powershell
powershell -File .\scripts\check_pr_scope.ps1 -PrNumber <N>
```

**Outputs**:
- `SRC-ONLY`: All changed files are in `src/`
- `MIXED`: Files span multiple directories
- `EMPTY`: No files changed

### 2. Scope-Aware Rebase Protocol

**For SRC-ONLY PRs**:
- Skip rebase entirely
- Use `git reset --hard origin/<branch>` instead
- Rationale: Prevents staging non-src commits from main

**For MIXED PRs**:
- Proceed with normal `git rebase origin/main`
- Resolve conflicts as needed
- Conflicts in src/ files should be rare (likely merge issues)

**For EMPTY PRs**:
- Skip hygiene checks entirely

### 3. Updated Documentation

**Files Modified**:
1. `docs/protocol/PR_LOOP_V2.md` - Added CRITICAL SAFETY CHECK section to Step 0
2. `.bob/commands/pr-loop.md` - Updated Step 0 handoff protocol with scope detection
3. `scripts/check_pr_scope.ps1` - New utility script (42 lines)

## Prevention Mechanism

**Mandatory Pre-Rebase Check**:
```powershell
# Step 0 now ALWAYS runs this first
$scope = powershell -File .\scripts\check_pr_scope.ps1 -PrNumber $1

if ($scope -eq "SRC-ONLY") {
    # Hard reset to branch HEAD - no rebase
    git fetch origin <branch>
    git reset --hard origin/<branch>
} elseif ($scope -eq "MIXED") {
    # Normal rebase with conflict resolution
    git fetch origin main
    git rebase origin/main
}
```

## Testing

**Test Case 1: SRC-ONLY PR**
```powershell
# PR #8 (src-only changes)
.\scripts\check_pr_scope.ps1 -PrNumber 8
# Expected: SRC-ONLY
# Action: git reset --hard origin/feature/pr-8
```

**Test Case 2: MIXED PR**
```powershell
# PR with docs/ and src/ changes
.\scripts\check_pr_scope.ps1 -PrNumber 9
# Expected: MIXED
# Action: git rebase origin/main
```

**Test Case 3: EMPTY PR**
```powershell
# PR with no file changes
.\scripts\check_pr_scope.ps1 -PrNumber 10
# Expected: EMPTY
# Action: Skip hygiene checks
```

## Impact

**Before Fix**:
- ❌ Blind rebase contaminated src-only PRs
- ❌ Non-src files staged unintentionally
- ❌ PR scope violations went undetected

**After Fix**:
- ✅ Scope-aware rebase strategy
- ✅ Src-only PRs protected from contamination
- ✅ Mixed PRs handled with proper conflict resolution
- ✅ Protocol violation prevented at source

## Related Issues

- **PR #8**: Triggered this fix (src-only PR contaminated by rebase)
- **AGENTS.md**: Updated with PR Hygiene Mandate
- **PR_LOOP_V2.md**: Now includes scope detection as mandatory step

## Future Enhancements

1. **Auto-Detection**: Integrate scope check into `verify_pr_hygiene.ps1`
2. **Metrics**: Track SRC-ONLY vs MIXED PR ratios
3. **Validation**: Add unit tests for `check_pr_scope.ps1`
4. **CI Integration**: Run scope check in GitHub Actions

## Verification Checklist

- [x] `check_pr_scope.ps1` created and tested
- [x] `PR_LOOP_V2.md` updated with safety check
- [x] `.bob/commands/pr-loop.md` updated with new protocol
- [x] Documentation created (`pr_loop_rebase_fix.md`)
- [ ] Test on next src-only PR (PR #9+)
- [ ] Monitor for false positives (files misclassified)

## Conclusion

This fix hardens the PR Loop protocol against rebase contamination by introducing mandatory scope detection before any rebase operation. Src-only PRs now use hard reset instead of rebase, eliminating the risk of staging unrelated non-src commits from main.

**Status**: ✅ PROTOCOL-HARDENED