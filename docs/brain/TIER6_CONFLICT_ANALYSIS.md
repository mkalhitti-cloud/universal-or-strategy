# Tier 6 Conflict Analysis - Consolidation Blockers

**Date**: 2026-06-06  
**Status**: 2/20 merged, 2/20 blocked by conflicts

## Successfully Merged (2/20)

1. ✅ `feature/infra-pr18-phs-simple-methodology` - Clean merge (docs only)
2. ✅ `feature/infra-fix-compilation-errors` - Clean merge (src/ fix)

## Blocked by Conflicts (2/20)

### 3. ❌ `feature/epic6-testing-clean` - MAJOR CONFLICTS (7 files)

**Conflicts**:
- `.bob/commands/pr-loop.md` (infrastructure)
- `benchmarks/StandaloneBench.cs` (modify/delete conflict)
- `docs/screenshot.jpg` (binary conflict)
- `docs/screenshot1.jpg` (binary conflict)
- `src/V12_002.Entries.RMA.cs` (source code)
- `src/V12_002.cs` (source code)
- `tests/V12_Performance.Tests/Core/FSMActorTests.cs` (add/add conflict)
- `tests/V12_Performance.Tests/V12_Performance.Tests.csproj` (add/add conflict)

**Analysis**: This is a MIXED branch with significant divergence. The "testing-clean" name suggests it was a cleanup attempt that touched multiple subsystems. The add/add conflicts in tests/ suggest parallel test infrastructure development.

**Recommendation**: DEFER to manual resolution after simpler branches are merged.

### 4. ❌ `feature/infra-session-2026-05-31` - SRC CONFLICT

**Conflicts**:
- `src/V12_002.Orders.Callbacks.cs` (source code)

**Analysis**: Despite "infra" prefix, this branch touched src/. The session date (2026-05-31) is 6 days before GitButler integration (2026-06-06), suggesting it's a stale session branch.

**Recommendation**: DEFER - likely superseded by later work.

## Revised Strategy

### Problem Identified
The "1 commit" metric is misleading - these branches diverged from main weeks ago, so even 1 commit can conflict with 220+ commits of main evolution.

### New Approach: Age-Based Filtering

**Hypothesis**: Branches created CLOSER to GitButler integration date (2026-06-06) will have fewer conflicts.

**Action**: Re-sort Tier 6 by branch creation date (newest first), not just commit count.

### Immediate Next Steps

1. Check branch creation dates for remaining 16 Tier 6 branches
2. Prioritize branches created after 2026-06-01 (last 6 days)
3. Skip branches older than 2026-05-15 (>3 weeks old)
4. Document conflict patterns for manual resolution guide

## Remaining Tier 6 Branches (16/20)

**To be analyzed**:
- feature/src-fix-compilation-errors
- feature/protocol-epic-readiness
- 1111.010-epic5-perf
- docs/epic-posinfo-phase3-validation
- dependabot/nuget/all-88f86858a1
- 1111.010-epic5-perf-fix-actual
- docs/epic-posinfo-phase4-tickets
- epic-quality-p0-currentbar-guards
- epic-ccn-14-src-only
- epic-ccn-14-propagate-master
- infra/epic-posinfo-phase1.5-docs
- v12-memory-plane
- pr-8-clean
- docs/jane-street-deviations-3-4
- codacy-phase2-errorprone-clean
- infra/p5-foundation

## Lessons Learned

1. **Commit count ≠ merge complexity**: A 1-commit branch can have major conflicts if it's old
2. **Branch age matters**: Older branches = more main evolution = more conflicts
3. **File type matters**: src/ conflicts are harder than docs/ conflicts
4. **"Trivial" is relative**: Need both low commit count AND recent creation date

## Next Action

Run branch date analysis to re-prioritize by recency, not just commit count.