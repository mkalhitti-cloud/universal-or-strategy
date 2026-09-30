# Chronological Branch Consolidation Strategy

**Date**: 2026-06-06  
**GitButler Integration Start**: 2026-06-06 16:08:56  
**Strategy**: Merge newer branches first (after GitButler), then older branches

---

## Rationale

Merging branches in chronological order (newest first) has several advantages:
1. **Newer branches** are closer to current main state → fewer conflicts
2. **Newer branches** likely contain fixes for issues in older branches → reduces redundant work
3. **Older branches** may be superseded by newer work → can skip if redundant
4. **Progressive validation** → each merge brings us closer to current state

---

## Phase 1: POST-GITBUTLER Branches (After 2026-06-06 16:08:56)

**Total**: 3 branches  
**Risk**: LOW - These are the most recent, closest to current state

### Merge Order (Newest to Oldest)

1. **build/1105-monolith** (2026-06-06 17:00:10)
   - Status: ✅ ALREADY MERGED (commit 3542b1ba)
   - Contains: Main infrastructure merge

2. **protocol/gitbutler-bob-integration** (2026-06-06 16:29:31)
   - Status: ✅ ALREADY MERGED (commits 928a5652, 43a34d1e, 0246c93b)
   - Contains: GitButler integration files

3. **feature/src-epic-ccn-51-reaper-restore** (2026-06-06 13:26:14)
   - Status: ⏳ READY TO MERGE
   - Type: SRC-ONLY
   - PHS: 100/100 (perfect)
   - Priority: HIGHEST

---

## Phase 2: PRE-GITBUTLER Branches (Before 2026-06-06 16:08:56)

**Total**: 83 branches  
**Risk**: MEDIUM-HIGH - Older branches, more likely to have conflicts or be superseded

### Group A: Recent Pre-GitButler (2026-06-01 to 2026-06-06)

**Total**: 23 branches  
**Risk**: MEDIUM - Recent enough to be relevant

#### Merge Order (Newest to Oldest)

1. **feature/src-fix-compilation-errors** (2026-06-05 22:37:08) - SRC-ONLY
2. **feature/infra-fix-compilation-errors** (2026-06-05 22:24:00) - INFRA
3. **feature/infra-epic-analysis-phase0** (2026-06-05 20:03:27) - INFRA
4. **docs/phase-10-godmode-reactivation** (2026-06-04 17:56:25) - DOCS
5. **epic-ccn-13-retroactive-pr** (2026-06-04 17:20:18) - MIXED
6. **epic-ccn-14-src-only** (2026-06-04 14:14:20) - SRC-ONLY
7. **epic-ccn-14-propagate-master** (2026-06-04 13:48:32) - MIXED
8. **epic-ccn-13-clean** (2026-06-04 10:12:00) - MIXED
9. **epic-ccn-13-cs-only** (2026-06-03 12:25:33) - SRC-ONLY
10. **epic-ccn-13-extract-monitor-rma-proximity** (2026-06-02 18:29:49) - MIXED
11. **feature/src-epic-ccn-12-shadowpropagatestop** (2026-06-02 16:11:50) - SRC-ONLY
12. **protocol/pr-21-round3-documentation** (2026-06-02 11:17:17) - PROTOCOL
13. **src/epic-posinfo-ticket-01** (2026-06-01 22:19:54) - SRC-ONLY
14. **docs/epic-posinfo-phase4-tickets** (2026-06-01 21:08:37) - DOCS
15. **docs/epic-posinfo-phase3-validation** (2026-06-01 20:59:28) - DOCS
16. **docs/epic-posinfo-phase2-protocol** (2026-06-01 20:50:31) - DOCS
17. **infra/epic-posinfo-phase1.5-docs** (2026-06-01 20:34:55) - INFRA
18. **infra/pr14-forensics-and-cleanup** (2026-06-01 19:40:03) - INFRA
19. **src/epic-13-extraction-v2** (2026-06-01 19:07:40) - SRC-ONLY
20. **src/epic-13-extraction-clean** (2026-06-01 07:56:52) - SRC-ONLY
21. **src/epic-13-handle-entry-order-filled-extraction** (2026-06-01 01:34:08) - SRC-ONLY

### Group B: Mid-May Pre-GitButler (2026-05-25 to 2026-05-31)

**Total**: 21 branches  
**Risk**: MEDIUM-HIGH - May be superseded by newer work

#### Merge Order (Newest to Oldest)

1. **feature/infra-github-migration-skill** (2026-05-31 21:12:38) - INFRA
2. **feature/docs-pr10-forensics** (2026-05-31 18:54:09) - DOCS
3. **feature/infra-bob-mode-tooling** (2026-05-31 13:03:36) - INFRA
4. **feature/src-phase7-new1-callbacks** (2026-05-31 11:25:06) - SRC-ONLY
5. **feature/infra-session-2026-05-31** (2026-05-31 10:23:23) - INFRA
6. **feature/src-tw1-perf-linq-optimization** (2026-05-30 20:20:02) - SRC-ONLY
7. **feature/docs-codacy-analysis** (2026-05-30 20:14:28) - DOCS
8. **feature/telemetry-instrumentation** (2026-05-30 20:09:39) - MIXED
9. **feature/infra-linear-sync** (2026-05-30 20:09:20) - INFRA
10. **feature/protocol-epic-readiness** (2026-05-30 20:08:56) - PROTOCOL
11. **fix/pr5-clean-cs-only** (2026-05-30 12:30:05) - SRC-ONLY
12. **dependabot/nuget/all-88f86858a1** (2026-05-29 16:07:39) - INFRA
13. **docs/jane-street-deviations-3-4** (2026-05-29 12:58:49) - DOCS
14. **fix/pr3-clean-cs-only** (2026-05-29 12:58:49) - SRC-ONLY
15. **fix/codacy-phase2-src** (2026-05-29 11:50:23) - SRC-ONLY
16. **feature/infra-pr18-phs-simple-methodology** (2026-05-29 07:42:38) - INFRA
17. **fix/codacy-phase2-src-clean** (2026-05-28 21:54:19) - SRC-ONLY
18. **fix/pr17-critical-violations** (2026-05-28 19:40:04) - MIXED
19. **codacy-phase2-errorprone-clean** (2026-05-28 16:22:45) - MIXED
20. **codacy-phase2-security-errorprone** (2026-05-28 16:13:53) - MIXED
21. **feature/protocol-branch-guard** (2026-05-28 08:09:28) - PROTOCOL

### Group C: Early May Pre-GitButler (2026-05-20 to 2026-05-27)

**Total**: 18 branches  
**Risk**: HIGH - Likely superseded, may skip many

#### Merge Order (Newest to Oldest)

1. **fix/signalbroadcaster-struct-validation** (2026-05-28 07:17:04) - SRC-ONLY
2. **epic-quality-p1-struct-false-positives** (2026-05-27 19:05:20) - MIXED
3. **epic-quality-p0-currentbar-guards** (2026-05-27 18:35:06) - MIXED
4. **test-curly-braces-tool** (2026-05-27 17:45:55) - INFRA
5. **epic-quality-curly-braces** (2026-05-27 16:05:50) - MIXED
6. **epic-quality-critical-clean** (2026-05-27 10:56:47) - MIXED
7. **pr-8-clean** (2026-05-26 20:24:35) - MIXED
8. **1111.010-epic5-perf-v2** (2026-05-25 17:41:46) - MIXED
9. **1111.010-epic5-perf-fix-actual** (2026-05-25 15:29:22) - MIXED
10. **1111.010-epic5-perf-linq-opt** (2026-05-25 14:14:04) - SRC-ONLY
11. **1111.010-epic5-perf-docs-only** (2026-05-25 11:17:56) - DOCS
12. **1111.010-epic5-perf-src-only** (2026-05-25 11:12:56) - SRC-ONLY
13. **1111.010-epic5-perf** (2026-05-25 08:39:48) - MIXED
14. **epic-7-quality/ticket-002-circuit-breaker-rollback** (2026-05-24 15:41:30) - MIXED
15. **feature/epic6-cicd-docs** (2026-05-23 20:37:20) - DOCS
16. **feature/epic6-tests-only** (2026-05-23 11:54:07) - MIXED
17. **feature/epic6-testing-clean** (2026-05-22 23:36:02) - MIXED
18. **feature/epic5-perf-optimization** (2026-05-22 23:18:03) - MIXED

### Group D: Mid-May Pre-GitButler (2026-05-17 to 2026-05-22)

**Total**: 7 branches  
**Risk**: HIGH - Very old, likely superseded

#### Merge Order (Newest to Oldest)

1. **feature/epic6-testing** (2026-05-22 17:13:30) - MIXED
2. **feat/reaper-expansion-phase2** (2026-05-22 13:15:54) - MIXED
3. **feature/reaper-expansion** (2026-05-21 15:40:06) - MIXED
4. **feature/photon-spsc-hardening-verified** (2026-05-20 18:34:37) - MIXED
5. **feature/photon-spsc-hardening-perfection** (2026-05-20 12:47:02) - MIXED
6. **feature/photon-spsc-hardening-repair** (2026-05-20 12:47:02) - MIXED
7. **feature/photon-spsc-hardening-clean** (2026-05-20 10:08:35) - MIXED

### Group E: Early May Pre-GitButler (2026-05-06 to 2026-05-18)

**Total**: 14 branches  
**Risk**: VERY HIGH - Ancient, almost certainly superseded

#### Merge Order (Newest to Oldest)

1. **backup/photon-spsc-hardening-clean-pre-rebase** (2026-05-20 08:37:49) - BACKUP
2. **feature/photon-spsc-hardening** (2026-05-18 20:41:09) - MIXED
3. **v12-memory-plane** (2026-05-18 16:19:50) - MIXED
4. **feat/phase7-final-validation** (2026-05-17 15:22:28) - MIXED
5. **feature/phase7-sprint5-extraction** (2026-05-15 12:10:03) - MIXED
6. **feature/phase7-sprint1-sprint2-extraction** (2026-05-11 19:13:09) - MIXED
7. **phase-6-t0-roadmap-registration** (2026-05-08 15:07:51) - MIXED
8. **fix/opencode-config-v2** (2026-05-07 14:35:11) - MIXED
9. **infra/p5-foundation** (2026-05-06 15:07:45) - INFRA
10. **p5/order-callbacks** (2026-05-06 15:05:15) - MIXED
11. **p5/sima-core** (2026-05-06 15:04:46) - MIXED
12. **infra/cleanup-and-docs** (2026-05-06 15:03:47) - INFRA
13. **infra/cleanup-final** (2026-05-06 15:03:47) - INFRA
14. **pre-refactor-baseline** (2026-05-06 15:03:00) - BASELINE

### Group F: Ancient Pre-GitButler (Before 2026-05-06)

**Total**: 2 branches  
**Risk**: EXTREME - Historical artifacts, likely skip

1. **phase-5-distributed-pipeline** (2026-05-06 14:27:22) - MIXED
2. **gitbutler/target** (2026-03-15 19:49:49) - GITBUTLER

---

## Execution Strategy

### Phase 1: POST-GITBUTLER (3 branches)
- ✅ **COMPLETE** - All 3 branches already merged

### Phase 2A: Recent Pre-GitButler (Group A - 23 branches)
**Start here** - These are most likely to be relevant

For each branch:
1. Check if superseded by newer work (compare file changes)
2. If unique work: `git merge <branch> --no-edit`
3. If superseded: Document and skip
4. Manual conflict review for safety

### Phase 2B: Mid-May Pre-GitButler (Group B - 21 branches)
**Selective merge** - Review each for relevance before merging

### Phase 2C: Early May Pre-GitButler (Groups C, D, E - 39 branches)
**Likely skip most** - Only merge if contains unique critical work

### Phase 2D: Ancient Pre-GitButler (Group F - 2 branches)
**Skip** - Historical artifacts, no longer relevant

---

## Success Criteria

- ✅ All POST-GITBUTLER branches merged (3/3 complete)
- ⏳ All relevant PRE-GITBUTLER branches merged or documented as superseded
- ✅ No compilation errors
- ✅ All V12 DNA checks passing
- ✅ GitButler virtual branches organized
- ✅ Old branches backed up

---

## Next Action

**START**: Phase 2A, Branch 1  
**Command**: `git merge feature/src-fix-compilation-errors --no-edit`

---

## References

- **Full Branch Analysis**: `docs/brain/BRANCH_CATEGORIZATION_ANALYSIS.md`
- **Workspace Status**: `docs/brain/WORKSPACE_CONSOLIDATION_STATUS.md`
- **GitButler Integration**: `docs/brain/GITBUTLER_INTEGRATION_COMPLETE.md`