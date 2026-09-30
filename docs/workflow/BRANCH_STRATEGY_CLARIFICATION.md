# Branch Strategy & GitButler Workflow Clarification

**Date**: 2026-06-07  
**Status**: ACTIVE  
**Version**: V12.18+

---

## Executive Summary

**Key Finding**: Both GitButler hooks (pre-commit and post-commit) are present and functional. The pre-commit hook enforces src-only protection on feature branches while allowing all files on main.

**Recommended Workflow**: Use GitButler virtual branches for ALL work (src, infra, protocol) to maximize efficiency and minimize context switching.

---

## Three-Tier Branch Strategy (CORRECTED)

### Tier 1: C# Code (`feature/src-*`)
- **Scope**: `**/*.cs` files (src, tests, benchmarks - ANY .cs file)
- **Branch Pattern**: `feature/src-epic-ccn-X-*`
- **PR Required**: ✅ YES (full bot review)
- **GitButler**: ✅ RECOMMENDED (virtual branches)
- **Enforcement**: Pre-commit hook blocks non-.cs files
- **Note**: Hook checks file extension only, not directory location

### Tier 2: Infrastructure (`feature/infra-*`)
- **Scope**: `docs/`, `scripts/`, `.github/` (non-.cs files only)
- **Branch Pattern**: `feature/infra-*` OR direct to main
- **PR Required**: ✅ YES (lightweight, no bot review)
- **GitButler**: ✅ OPTIONAL (can use traditional branches OR direct push)
- **Enforcement**: Manual (no hook protection)
- **Note**: `.cs` files in tests/ or benchmarks/ belong to Tier 1, not Tier 2

### Tier 3: Protocol (`feature/protocol-*`)
- **Scope**: `.bob/`, `.codex/`, `.cursor/`, agent rules
- **Branch Pattern**: `feature/protocol-*` OR direct to main
- **PR Required**: ❌ OPTIONAL (95% direct push to main)
- **GitButler**: ✅ OPTIONAL (can use traditional branches OR direct push)
- **Enforcement**: Manual (no hook protection)

---

## GitButler Hook Analysis

### Pre-Commit Hook (`.git/hooks/pre-commit`)

**Purpose**: Enforces src-only protection on feature branches

**Logic**:
1. **Main Branch**: Allows ALL files (no restrictions)
2. **Feature Branches**: Only `.cs` files allowed
3. **Sync Check**: Blocks commit if branch is behind main (prevents divergence)
4. **Bob Integration**: Cleans up stale notes before commit

**Key Code**:
```bash
# Main branch: allow everything
if [ "$branch" = "main" ]; then
    echo "✓ Main branch: all files allowed" >&2
    exit 0
fi

# Other branches: only .cs files
staged_files=$(git diff --cached --name-only)
non_cs_files=$(echo "$staged_files" | grep -v "\.cs$")

if [ -n "$non_cs_files" ]; then
    echo "ERROR: Only .cs files allowed on branch '$branch'" >&2
    exit 1
fi
```

**Implications**:
- ✅ ANY `.cs` file is allowed on feature branches (src/, tests/, benchmarks/)
- ✅ Tier 2/3 changes (non-.cs files) MUST go to main OR use separate branches
- ✅ GitButler virtual branches inherit this protection
- ✅ Hook doesn't check directory location, only file extension

### Post-Commit Hook (`.git/hooks/post-commit`)

**Purpose**: Attaches git notes from Bob Shell and syncs with remote

**Logic**:
1. Reads `.bob/notes/pending-notes.txt`
2. Attaches notes to HEAD commit
3. Pushes notes to remote
4. Clears pending notes file

**Status**: ✅ Verified present and functional (previous session)

---

## Why Use GitButler for ALL Work?

### Benefits

1. **No Context Switching**
   - Work on multiple epics simultaneously
   - Switch between tasks without `git checkout`
   - Virtual branches stack cleanly

2. **Atomic Commits**
   - Each epic gets its own commit history
   - No mixed concerns in commits
   - Clean PR diffs

3. **Simplified Workflow**
   - One workspace branch (`gitbutler/workspace`)
   - No branch management overhead
   - Automatic conflict detection

4. **Tier 2/3 Flexibility**
   - Can commit infra/protocol changes to main directly
   - OR create virtual branches for review
   - Hook protection still applies (only .cs on feature branches)

### Workflow Comparison

| Aspect | Traditional Branches | GitButler Virtual Branches |
|--------|---------------------|---------------------------|
| Context Switching | `git checkout` (slow) | Instant (UI toggle) |
| Multiple Epics | One at a time | Simultaneous |
| Commit History | Linear | Stacked (clean) |
| Tier 2/3 Changes | Separate branches | Same workspace |
| Hook Protection | ✅ Yes | ✅ Yes (inherited) |

---

## Recommended Workflows

### Workflow A: GitButler for Everything (RECOMMENDED)

**Use Case**: Maximum efficiency, working on multiple epics

**Steps**:
1. Open GitButler UI
2. Create virtual branch for Epic CCN-X (Tier 1)
3. Make src/ changes, commit to virtual branch
4. Create PR from virtual branch
5. For Tier 2/3 changes:
   - Option A: Commit directly to main (no PR)
   - Option B: Create separate virtual branch (lightweight PR)

**Advantages**:
- ✅ No branch switching
- ✅ Work on multiple epics simultaneously
- ✅ Clean commit history per epic
- ✅ Automatic conflict detection

**Disadvantages**:
- ⚠️ Requires GitButler UI (not CLI-friendly)
- ⚠️ Learning curve for new tool

### Workflow B: Traditional Branches (FALLBACK)

**Use Case**: CLI-only workflow, single epic focus

**Steps**:
1. Create `feature/src-epic-ccn-X` branch
2. Make src/ changes, commit
3. Create PR
4. For Tier 2/3 changes:
   - Switch to main: `git checkout main`
   - Make changes, commit directly to main
   - Push to GitHub (no PR)

**Advantages**:
- ✅ CLI-friendly
- ✅ Simple mental model
- ✅ No new tools required

**Disadvantages**:
- ❌ Context switching overhead
- ❌ One epic at a time
- ❌ Manual branch management

---

## Answers to Your Questions

### Q1: Will everything be on `gitbutler/workspace` going forward?

**Answer**: ✅ YES, if you use GitButler for all work (recommended)

**Details**:
- GitButler creates a single workspace branch (`gitbutler/workspace`)
- All virtual branches stack on top of this workspace
- When you push a virtual branch, GitButler creates a real Git branch (e.g., `feature/src-epic-ccn-X`)
- The workspace branch itself is local-only (not pushed to GitHub)

**Workflow**:
```
Local:
  gitbutler/workspace (local only)
    ├── virtual-branch-epic-51 (Tier 1)
    ├── virtual-branch-epic-52 (Tier 1)
    └── virtual-branch-docs (Tier 2)

GitHub (after push):
  feature/src-epic-ccn-51 (PR #8)
  feature/src-epic-ccn-52 (PR #9)
  feature/infra-docs-update (PR #10)
```

### Q2: Clarification on Three-Tier Branch Strategy

**Answer**: ✅ CORRECTED - Tier 2/3 branches CAN create PRs (optional)

**Original Misunderstanding**:
- ❌ "Tier 2/3 should NOT create PRs"

**Correct Understanding**:
- ✅ **Tier 1**: PR REQUIRED (full bot review)
- ✅ **Tier 2**: PR REQUIRED (lightweight, no bot review)
- ✅ **Tier 3**: PR OPTIONAL (95% direct push to main)

**Rationale**:
- Tier 2 PRs provide audit trail for infrastructure changes
- Tier 3 PRs are rare (only for major protocol changes)
- Both can be pushed directly to main if changes are trivial

### Q3: Why not include infra/protocol in GitButler too?

**Answer**: ✅ YOU SHOULD include them in GitButler!

**Recommended Approach**:
1. **Use GitButler for ALL work** (Tier 1, 2, 3)
2. **Create virtual branches** for each concern:
   - `epic-51-src` (Tier 1) → PR required
   - `epic-51-docs` (Tier 2) → PR optional (lightweight)
   - `protocol-update` (Tier 3) → PR optional (rare)

**Benefits**:
- ✅ No context switching between src and docs work
- ✅ Atomic commits per concern
- ✅ Can work on multiple epics + docs simultaneously
- ✅ GitButler handles branch management

**Example Workflow**:
```
GitButler Workspace:
  ├── epic-51-src (Tier 1)
  │   └── src/V12_002.Orders.Callbacks.Hydration.cs
  ├── epic-51-docs (Tier 2)
  │   └── docs/brain/EPIC-CCN-51/
  └── protocol-update (Tier 3)
      └── .bob/custom_modes.yaml

Push Strategy:
  - epic-51-src → PR #8 (full review)
  - epic-51-docs → Direct to main (no PR)
  - protocol-update → Direct to main (no PR)
```

### Q4: Did GitButler hooks persist after all changes?

**Answer**: ✅ YES, both hooks are present and functional

**Verification**:
- ✅ Pre-commit hook: 127 lines (src-only protection + Bob integration)
- ✅ Post-commit hook: Verified in previous session (git notes sync)

**Hook Locations**:
- `.git/hooks/pre-commit` (enforces src-only on feature branches)
- `.git/hooks/post-commit` (syncs Bob notes to remote)

**Protection Status**:
- ✅ Tier 1 branches: Automatically protected (only .cs files)
- ✅ Main branch: All files allowed
- ✅ Bob notes: Automatically synced after every commit

---

## Migration Path

### Current State
- ✅ Main branch: Clean, all merges complete
- ✅ GitHub branches: 83 branches (2 merged, 81 remaining)
- ✅ GitButler hooks: Present and functional

### Recommended Next Steps

1. **Start Using GitButler** (Today)
   - Open GitButler UI
   - Create virtual branch for Epic CCN-51
   - Make src/ changes
   - Commit to virtual branch
   - Push to GitHub (creates real branch + PR)

2. **Tier 2/3 Changes** (As Needed)
   - Option A: Create virtual branches in GitButler (recommended)
   - Option B: Commit directly to main (faster for trivial changes)

3. **Cleanup Old Branches** (After Epic CCN-51)
   - Delete merged branches from GitHub
   - Archive obsolete branches
   - Document lessons learned

---

## Summary

**Key Takeaways**:
1. ✅ Use GitButler for ALL work (Tier 1, 2, 3)
2. ✅ Tier 2 PRs are lightweight (no bot review)
3. ✅ Tier 3 PRs are optional (95% direct push)
4. ✅ GitButler hooks are present and functional
5. ✅ No context switching = maximum efficiency

**Recommended Workflow**:
```
GitButler Workspace (local)
  ├── epic-51-src (Tier 1) → PR required
  ├── epic-51-docs (Tier 2) → PR optional
  └── protocol-update (Tier 3) → PR optional

Push Strategy:
  - Tier 1: Always create PR (full review)
  - Tier 2: Create PR OR direct push (your choice)
  - Tier 3: Direct push (95% of time)
```

**Next Action**: Start Epic CCN-51 using GitButler virtual branches!