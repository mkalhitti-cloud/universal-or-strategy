# Filtered Branch Consolidation Plan

**Date**: 2026-06-06  
**Critical Filter**: Only branches with commits AHEAD of `gitbutler/workspace`  
**Total Branches**: 64 (down from 86 total branches)

---

## Key Insight: Commit-Ahead Filtering

**Your brilliant observation**: We only care about branches that have **unique commits** not already in `gitbutler/workspace`.

**Result**: 22 branches filtered out (already fully merged or behind workspace)

---

## Priority Tiers by Commit Count

### Tier 1: MASSIVE (100+ commits ahead)

**Total**: 1 branch  
**Risk**: EXTREME - Likely ancient divergence, may skip

1. **backup/photon-spsc-hardening-clean-pre-rebase** - 345 commits ahead
   - Status: BACKUP branch (name indicates pre-rebase snapshot)
   - Action: **SKIP** - This is a backup, not active work

### Tier 2: LARGE (20-99 commits ahead)

**Total**: 8 branches  
**Risk**: HIGH - Significant divergence, careful review needed

1. **fix/opencode-config-v2** - 62 commits ahead
2. **phase-5-distributed-pipeline** - 41 commits ahead
3. **infra/cleanup-final** - 39 commits ahead
4. **infra/cleanup-and-docs** - 39 commits ahead
5. **feature/photon-spsc-hardening** - 25 commits ahead
6. **feature/epic6-cicd-docs** - 25 commits ahead
7. **feat/reaper-expansion-phase2** - 24 commits ahead
8. **feature/photon-spsc-hardening-verified** - 21 commits ahead

### Tier 3: MEDIUM (10-19 commits ahead)

**Total**: 4 branches  
**Risk**: MEDIUM - Moderate divergence

1. **codacy-phase2-security-errorprone** - 15 commits ahead
2. **feature/src-epic-ccn-12-shadowpropagatestop** - 14 commits ahead
3. **feature/phase7-sprint5-extraction** - 12 commits ahead
4. **feature/photon-spsc-hardening-clean** - 10 commits ahead

### Tier 4: SMALL (5-9 commits ahead)

**Total**: 11 branches  
**Risk**: LOW-MEDIUM - Manageable divergence

1. **protocol/pr-21-round3-documentation** - 9 commits ahead
2. **fix/codacy-phase2-src** - 8 commits ahead
3. **feat/phase7-final-validation** - 7 commits ahead
4. **feature/epic5-perf-optimization** - 7 commits ahead
5. **infra/pr14-forensics-and-cleanup** - 7 commits ahead
6. **feature/phase7-sprint1-sprint2-extraction** - 7 commits ahead
7. **fix/pr17-critical-violations** - 6 commits ahead
8. **epic-7-quality/ticket-002-circuit-breaker-rollback** - 6 commits ahead
9. **feature/src-tw1-perf-linq-optimization** - 6 commits ahead
10. **src/epic-13-extraction-v2** - 5 commits ahead
11. **feature/photon-spsc-hardening-perfection** - 5 commits ahead

### Tier 5: MINIMAL (2-4 commits ahead)

**Total**: 20 branches  
**Risk**: LOW - Small divergence, likely quick merges

1. **1111.010-epic5-perf-v2** - 5 commits ahead
2. **feature/docs-codacy-analysis** - 5 commits ahead
3. **feature/photon-spsc-hardening-repair** - 5 commits ahead
4. **phase-6-t0-roadmap-registration** - 4 commits ahead
5. **epic-ccn-13-retroactive-pr** - 4 commits ahead
6. **src/epic-13-extraction-clean** - 4 commits ahead
7. **feature/telemetry-instrumentation** - 3 commits ahead
8. **epic-quality-p1-struct-false-positives** - 3 commits ahead
9. **epic-quality-critical-clean** - 3 commits ahead
10. **src/epic-13-handle-entry-order-filled-extraction** - 3 commits ahead
11. **fix/signalbroadcaster-struct-validation** - 3 commits ahead
12. **feature/src-epic-ccn-51-reaper-restore** - 2 commits ahead ⭐ (100/100 PHS)
13. **fix/pr3-clean-cs-only** - 2 commits ahead
14. **1111.010-epic5-perf-linq-opt** - 2 commits ahead
15. **epic-ccn-13-extract-monitor-rma-proximity** - 2 commits ahead
16. **feature/infra-linear-sync** - 2 commits ahead
17. **feature/infra-github-migration-skill** - 2 commits ahead
18. **p5/sima-core** - 2 commits ahead
19. **p5/order-callbacks** - 2 commits ahead
20. **src/epic-posinfo-ticket-01** - 2 commits ahead

### Tier 6: TRIVIAL (1 commit ahead)

**Total**: 20 branches  
**Risk**: MINIMAL - Single commit, very quick merges

1. **infra/p5-foundation** - 1 commit ahead
2. **codacy-phase2-errorprone-clean** - 1 commit ahead
3. **docs/jane-street-deviations-3-4** - 1 commit ahead
4. **pr-8-clean** - 1 commit ahead
5. **v12-memory-plane** - 1 commit ahead
6. **infra/epic-posinfo-phase1.5-docs** - 1 commit ahead
7. **epic-ccn-14-propagate-master** - 1 commit ahead
8. **epic-ccn-14-src-only** - 1 commit ahead
9. **epic-quality-p0-currentbar-guards** - 1 commit ahead
10. **docs/epic-posinfo-phase4-tickets** - 1 commit ahead
11. **1111.010-epic5-perf-fix-actual** - 1 commit ahead
12. **dependabot/nuget/all-88f86858a1** - 1 commit ahead
13. **docs/epic-posinfo-phase3-validation** - 1 commit ahead
14. **1111.010-epic5-perf** - 1 commit ahead
15. **feature/protocol-epic-readiness** - 1 commit ahead
16. **feature/src-fix-compilation-errors** - 1 commit ahead
17. **feature/infra-session-2026-05-31** - 1 commit ahead
18. **feature/epic6-testing-clean** - 1 commit ahead
19. **feature/infra-fix-compilation-errors** - 1 commit ahead
20. **feature/infra-pr18-phs-simple-methodology** - 1 commit ahead

---

## Recommended Execution Strategy

### Phase 1: Start with Tier 6 (TRIVIAL - 20 branches)

**Why first**: 
- Single commits = minimal conflict risk
- Fast wins = build momentum
- Easy to verify = quick validation

**Batch merge approach**:
```bash
# Merge all 20 trivial branches in sequence
git merge feature/infra-pr18-phs-simple-methodology --no-edit
git merge feature/infra-fix-compilation-errors --no-edit
# ... continue through all Tier 6
```

### Phase 2: Tier 5 (MINIMAL - 20 branches)

**Priority**: Start with `feature/src-epic-ccn-51-reaper-restore` (100/100 PHS)

**Why second**:
- 2-4 commits = still low risk
- Contains important work (PR #7 is perfect)
- Builds on Tier 6 foundation

### Phase 3: Tier 4 (SMALL - 11 branches)

**Why third**:
- 5-9 commits = manageable
- May contain important fixes
- Review each before merging

### Phase 4: Tier 3 (MEDIUM - 4 branches)

**Why fourth**:
- 10-19 commits = careful review needed
- Check for supersession by Tiers 1-3

### Phase 5: Tier 2 (LARGE - 8 branches)

**Why fifth**:
- 20-99 commits = high conflict risk
- Many likely superseded by earlier tiers
- Selective merge only

### Phase 6: Tier 1 (MASSIVE - 1 branch)

**Action**: **SKIP** - `backup/photon-spsc-hardening-clean-pre-rebase` is a backup

---

## Filtered Out (22 branches with 0 commits ahead)

These branches are **already fully merged** or **behind workspace**:

1. gitbutler/target
2. pre-refactor-baseline
3. feature/reaper-expansion
4. feature/epic6-testing
5. 1111.010-epic5-perf-src-only
6. 1111.010-epic5-perf-docs-only
7. epic-quality-curly-braces
8. test-curly-braces-tool
9. feature/protocol-branch-guard
10. fix/pr5-clean-cs-only
11. feature/infra-bob-mode-tooling
12. feature/docs-pr10-forensics
13. feature/src-phase7-new1-callbacks
14. epic-ccn-13-cs-only
15. epic-ccn-13-clean
16. docs/epic-posinfo-phase2-protocol
17. docs/phase-10-godmode-reactivation
18. feature/infra-epic-analysis-phase0
19. protocol/gitbutler-bob-integration (already merged)
20. build/1105-monolith (already merged)
21. feature/epic6-tests-only
22. epic-quality-p0-currentbar-guards (duplicate? shows 1 commit in other list)

---

## Success Metrics

- ✅ 64 branches with unique commits identified
- ✅ 22 already-merged branches filtered out
- ✅ Prioritized by commit count (risk proxy)
- ✅ Clear execution order (trivial → massive)
- ⏳ Start with Tier 6 (20 quick wins)

---

## Next Action

**START**: Tier 6, Branch 1  
**Command**: `git merge feature/infra-pr18-phs-simple-methodology --no-edit`

**Rationale**: Single commit = minimal risk, fast validation, builds momentum

---

## References

- **Chronological Strategy**: `docs/brain/CHRONOLOGICAL_CONSOLIDATION_STRATEGY.md`
- **Workspace Status**: `docs/brain/WORKSPACE_CONSOLIDATION_STATUS.md`
- **Branch Categorization**: `docs/brain/BRANCH_CATEGORIZATION_ANALYSIS.md`