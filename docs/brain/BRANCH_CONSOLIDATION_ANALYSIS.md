# Branch Consolidation Analysis - Content-Based Strategy

**Generated**: 2026-06-06
**Purpose**: Categorize remaining branches by content type and merge strategy

## Summary Statistics

- **Total Branches Analyzed**: 50
- **Already Merged**: 38 branches (76%)
- **Remaining Active**: 12 branches (24%)

### Remaining Branches by Strategy

| Strategy | Count | Description |
|----------|-------|-------------|
| **NEEDS_PR** | 3 | Has .cs files, <7 days old, requires PR review |
| **DIRECT_MERGE** | 1 | No .cs files, can merge directly to main |
| **SKIP_OBSOLETE_SRC** | 8 | Has .cs files, >7 days old, skip per user rule |

---

## Category 1: NEEDS_PR (3 branches)

These branches contain `.cs` files and are recent enough to be relevant. They **require PR review** per Three-Tier Branch Model.

### 1. feature/src-epic-ccn-51-reaper-restore
- **Tier**: 5 (2-4 commits)
- **Age**: 0 days (2026-06-06 13:26:14)
- **Ahead/Behind**: 2 commits ahead, 33 behind
- **Files**: 1 total (1 .cs)
  - `src/V12_002.cs`
- **Status**: PR #7 exists with 100/100 PHS
- **Action**: Merge via PR (already has perfect health score)

### 2. epic-ccn-14-src-only
- **Tier**: 6 (1 commit)
- **Age**: 2 days (2026-06-04 14:14:20)
- **Ahead/Behind**: 1 commit ahead, 53 behind
- **Files**: 2 total (2 .cs)
  - `src/V12_002.Orders.Callbacks.Propagation.cs`
  - `src/V12_002.cs`
- **Action**: Check for conflicts, create PR if clean

### 3. epic-ccn-14-propagate-master
- **Tier**: 6 (1 commit)
- **Age**: 2 days (2026-06-04 13:48:32)
- **Ahead/Behind**: 1 commit ahead, 53 behind
- **Files**: 90 total (2 .cs, 14 docs, 3 .bob, 1 script, 30 config, 42 other)
  - **CS**: `src/V12_002.Orders.Callbacks.Propagation.cs`, `src/V12_002.cs`
  - **Docs**: EPIC-CCN-13 scope, autonomous refactor analysis
  - **Bob**: commands, hooks, settings
- **Action**: **MIXED CONTENT** - May need to split into separate PRs (src vs infra)

---

## Category 2: DIRECT_MERGE (1 branch)

This branch has **NO .cs files** and can be merged directly to main without PR.

### 1. infra/epic-posinfo-phase1.5-docs
- **Tier**: 6 (1 commit)
- **Age**: 4 days (2026-06-01 20:34:55)
- **Ahead/Behind**: 1 commit ahead, 92 behind
- **Files**: 8 total (0 .cs, 5 docs, 1 .bob, 3 config)
  - **Docs**: EPIC-POSINFO scope, pattern analysis, phase 1.5 completion
  - **Bob**: custom_modes.yaml
  - **Config**: 3 files
- **Action**: Merge directly to main (no PR needed)
- **Value**: Contains EPIC-POSINFO documentation (important for context)

---

## Category 3: SKIP_OBSOLETE_SRC (8 branches)

These branches have `.cs` files but are **>7 days old**. Per user's rule: "src/ code older than 7 days = probably obsolete, skip if conflicts."

### 1. epic-quality-p0-currentbar-guards
- **Age**: 10 days (2026-05-27 18:35:06)
- **Files**: 2 .cs files
- **Action**: SKIP (obsolete src/)

### 2. pr-8-clean
- **Age**: 10 days (2026-05-26 20:24:35)
- **Files**: 47 .cs files
- **Action**: SKIP (obsolete src/)

### 3. codacy-phase2-errorprone-clean
- **Age**: 9 days (2026-05-28 16:22:45)
- **Files**: 33 .cs files
- **Action**: SKIP (obsolete src/)

### 4. docs/jane-street-deviations-3-4
- **Age**: 8 days (2026-05-29 12:58:49)
- **Files**: 20 total (6 .cs, 12 docs, 1 .bob)
- **Action**: SKIP src/, but **EXTRACT DOCS** (Jane Street deviations are important!)
- **Note**: Contains 12 docs files including Jane Street deviation documentation

---

## Recommended Execution Order

### Phase 1: Direct Merge (No PR)
1. ✅ **infra/epic-posinfo-phase1.5-docs** - Merge directly to main
   - Contains valuable EPIC-POSINFO documentation
   - No .cs files, no conflicts expected

### Phase 2: Extract Docs from Obsolete Branches
2. ✅ **docs/jane-street-deviations-3-4** - Cherry-pick docs only
   - Extract 12 docs files (Jane Street deviations, Bob config, PR categorization)
   - Skip 6 obsolete .cs files

### Phase 3: PR Review (Recent .cs Changes)
3. ✅ **feature/src-epic-ccn-51-reaper-restore** - Already has PR #7 (100/100 PHS)
   - Merge via existing PR
   
4. ⏳ **epic-ccn-14-src-only** - Check conflicts, create PR
   - 2 days old, 2 .cs files
   
5. ⏳ **epic-ccn-14-propagate-master** - MIXED CONTENT
   - **Decision needed**: Split into 2 PRs (src vs infra) or merge as-is?
   - Contains 2 .cs + 88 non-.cs files

### Phase 4: Cleanup
6. ✅ Delete 7 obsolete branches (>7 days old with src/ conflicts)

---

## Key Insights

### Content Distribution
- **Pure src/ branches**: 10 (8 obsolete, 2 recent)
- **Pure infra branches**: 1 (direct merge candidate)
- **Mixed content**: 1 (needs split decision)

### Age Distribution
- **Fresh (<3 days)**: 3 branches (all need PR)
- **Recent (3-7 days)**: 1 branch (direct merge)
- **Obsolete (>7 days)**: 8 branches (skip src/, extract docs if valuable)

### Recovery Potential
- **Immediate value**: 1 branch (infra docs)
- **PR candidates**: 3 branches (recent src/ changes)
- **Doc extraction**: 1 branch (Jane Street deviations)
- **Skip entirely**: 7 branches (obsolete src/ only)

---

## Next Steps

1. **Merge infra/epic-posinfo-phase1.5-docs** directly to main
2. **Extract docs** from docs/jane-street-deviations-3-4 via cherry-pick
3. **Review PR #7** (feature/src-epic-ccn-51-reaper-restore) - already 100/100 PHS
4. **Analyze epic-ccn-14-propagate-master** for split vs merge decision
5. **Create PR** for epic-ccn-14-src-only if no conflicts
6. **Document and delete** 7 obsolete branches

---

## Branch Deletion Candidates (Obsolete)

These branches are >7 days old with src/ conflicts and should be deleted after documenting:

1. epic-quality-p0-currentbar-guards (10 days)
2. pr-8-clean (10 days)
3. codacy-phase2-errorprone-clean (9 days)
4. docs/jane-street-deviations-3-4 (8 days) - **AFTER extracting docs**
5. v12-memory-plane (not found - already deleted?)

**Total to delete**: 4-5 branches after doc extraction