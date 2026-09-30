# GitHub Branch Cleanup Strategy

## Current State
- **Total branches on origin**: 84 (including main and HEAD)
- **Target**: Keep only 4 branches
- **Branches to delete**: 80

## Branches to KEEP

1. `origin/main` - Primary branch
2. `origin/gitbutler/workspace` - GitButler integration
3. `origin/feature/src-epic-ccn-51-reaper-restore` - Active PR #7
4. `origin/epic-ccn-14-propagate-master` - Active work

## Branches to DELETE (80 total)

### Category 1: Epic Performance Variants (7 branches)
All variations of epic5-perf work - likely superseded or merged:
- `origin/1111.010-epic5-perf`
- `origin/1111.010-epic5-perf-docs-only`
- `origin/1111.010-epic5-perf-fix-actual`
- `origin/1111.010-epic5-perf-linq-opt`
- `origin/1111.010-epic5-perf-src-only`
- `origin/1111.010-epic5-perf-v2`
- `origin/feature/epic5-perf-optimization`

### Category 2: Backup/Archive Branches (1 branch)
- `origin/backup/photon-spsc-hardening-clean-pre-rebase`

### Category 3: Codacy Quality Work (4 branches)
- `origin/codacy-phase2-errorprone-clean`
- `origin/codacy-phase2-security-errorprone`
- `origin/fix/codacy-phase2-src`
- `origin/fix/codacy-phase2-src-clean`

### Category 4: Consolidation Work (1 branch)
- `origin/consolidation/tier6-complete` - Work already merged to main

### Category 5: Dependabot (1 branch)
- `origin/dependabot/nuget/all-88f86858a1`

### Category 6: Documentation Branches (4 branches)
- `origin/docs/epic-posinfo-phase2-protocol`
- `origin/docs/epic-posinfo-phase3-validation`
- `origin/docs/epic-posinfo-phase4-tickets`
- `origin/docs/phase-10-godmode-reactivation`

### Category 7: Epic Quality Work (5 branches)
- `origin/epic-7-quality/ticket-002-circuit-breaker-rollback`
- `origin/epic-quality-critical-clean`
- `origin/epic-quality-curly-braces`
- `origin/epic-quality-p0-currentbar-guards`
- `origin/epic-quality-p1-struct-false-positives`

### Category 8: Epic CCN Work (4 branches)
- `origin/epic-ccn-13-extract-monitor-rma-proximity`
- `origin/epic-ccn-13-retroactive-pr`
- `origin/epic-ccn-14-src-only` - Superseded by epic-ccn-14-propagate-master
- `origin/src/epic-13-extraction-clean`

### Category 9: Feature Branches - Phase 7 (3 branches)
- `origin/feat/phase7-final-validation`
- `origin/feature/phase7-sprint1-sprint2-extraction`
- `origin/feature/phase7-sprint5-extraction`

### Category 10: Feature Branches - Reaper (2 branches)
- `origin/feat/reaper-expansion-phase2`
- `origin/feature/reaper-expansion`

### Category 11: Feature Branches - Documentation (2 branches)
- `origin/feature/docs-codacy-analysis`
- `origin/feature/docs-pr10-forensics`

### Category 12: Feature Branches - Epic 6 (4 branches)
- `origin/feature/epic6-cicd-docs`
- `origin/feature/epic6-testing`
- `origin/feature/epic6-testing-clean`
- `origin/feature/epic6-tests-only`

### Category 13: Feature Branches - Infrastructure (7 branches)
- `origin/feature/infra-bob-mode-tooling`
- `origin/feature/infra-fix-compilation-errors`
- `origin/feature/infra-github-migration-skill`
- `origin/feature/infra-linear-sync`
- `origin/feature/infra-pr18-phs-simple-methodology`
- `origin/feature/infra-session-2026-05-31`
- `origin/infra-pr-pollution-fix`

### Category 14: Feature Branches - Photon SPSC (5 branches)
- `origin/feature/photon-spsc-hardening`
- `origin/feature/photon-spsc-hardening-clean`
- `origin/feature/photon-spsc-hardening-perfection`
- `origin/feature/photon-spsc-hardening-repair`
- `origin/feature/photon-spsc-hardening-verified`

### Category 15: Feature Branches - Protocol (2 branches)
- `origin/feature/protocol-branch-guard`
- `origin/feature/protocol-epic-readiness`

### Category 16: Feature Branches - Source Code (3 branches)
- `origin/feature/src-epic-ccn-12-shadowpropagatestop`
- `origin/feature/src-fix-compilation-errors`
- `origin/feature/src-phase7-new1-callbacks`

### Category 17: Feature Branches - Misc (2 branches)
- `origin/feature/src-tw1-perf-linq-optimization`
- `origin/feature/telemetry-instrumentation`

### Category 18: Fix Branches (4 branches)
- `origin/fix/opencode-config-v2`
- `origin/fix/pr17-critical-violations`
- `origin/fix/pr3-clean-cs-only`
- `origin/fix/pr5-clean-cs-only`

### Category 19: Fix Branches - Validation (1 branch)
- `origin/fix/signalbroadcaster-struct-validation`

### Category 20: Infrastructure Branches (4 branches)
- `origin/infra/cleanup-and-docs`
- `origin/infra/cleanup-final`
- `origin/infra/p5-foundation`
- `origin/infra/pr14-forensics-and-cleanup`

### Category 21: Phase 5 Branches (2 branches)
- `origin/p5/order-callbacks`
- `origin/p5/sima-core`

### Category 22: Phase Branches (2 branches)
- `origin/phase-5-distributed-pipeline`
- `origin/phase-6-t0-roadmap-registration`

### Category 23: PR Branches (1 branch)
- `origin/pr-8-clean`

### Category 24: Baseline/Archive (1 branch)
- `origin/pre-refactor-baseline`

### Category 25: Protocol Branches (1 branch)
- `origin/protocol/pr-21-round3-documentation`

### Category 26: Source Branches (3 branches)
- `origin/src/epic-13-extraction-v2`
- `origin/src/epic-13-handle-entry-order-filled-extraction`
- `origin/src/epic-posinfo-ticket-01`

### Category 27: Test Branches (2 branches)
- `origin/test-curly-braces-tool`
- `origin/test-workflow-trigger`

### Category 28: Memory Plane (1 branch)
- `origin/v12-memory-plane`

## Deletion Strategy

### Option 1: Batch Delete via PowerShell Script (RECOMMENDED)
Create a script that deletes all 80 branches in one operation:

```powershell
# Save all branches to delete
$branchesToDelete = @(
    "1111.010-epic5-perf",
    "1111.010-epic5-perf-docs-only",
    # ... (all 80 branches)
)

# Delete from GitHub
foreach ($branch in $branchesToDelete) {
    Write-Host "Deleting $branch..."
    git push origin --delete $branch
}
```

### Option 2: GitHub CLI Batch Delete
```bash
# List all branches except the 4 to keep
gh api repos/antigravityos187-sketch/universal-or-strategy/branches --paginate | jq -r '.[].name' | grep -v -E '^(main|gitbutler/workspace|feature/src-epic-ccn-51-reaper-restore|epic-ccn-14-propagate-master)$' | xargs -I {} gh api -X DELETE repos/antigravityos187-sketch/universal-or-strategy/git/refs/heads/{}
```

### Option 3: Manual GitHub UI (NOT RECOMMENDED - Too slow)
Delete each branch individually via GitHub web interface.

## Pre-Deletion Checklist

- [ ] Verify branch protection is disabled (already done)
- [ ] Confirm all 4 branches to keep are correct
- [ ] Backup branch list (this document serves as backup)
- [ ] Verify no active PRs on branches to delete
- [ ] Run deletion script
- [ ] Verify final branch count is 4
- [ ] Re-enable branch protection for main

## Post-Deletion Verification

```powershell
# Should show only 4 branches
git ls-remote --heads origin | Measure-Object -Line

# Should list: main, gitbutler/workspace, feature/src-epic-ccn-51-reaper-restore, epic-ccn-14-propagate-master
git ls-remote --heads origin
```

## Rollback Plan

If deletion was a mistake:
1. Check GitHub's "Recently deleted branches" (available for 30 days)
2. Restore specific branches via GitHub UI
3. Or re-push from local if branch still exists locally

## Notes

- All deleted branches' content has been consolidated to main via previous merge operations
- No active PRs exist on branches being deleted
- Branch protection must be disabled before deletion
- Local tracking branches will be cleaned up with `git fetch --prune`