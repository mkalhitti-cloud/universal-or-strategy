# V12 Orchestration Consolidation - 2026-06-01

## Executive Summary

**Current Status**: PR #13 hotfix complete, awaiting rebase and merge
**Build**: `1111.010-epic5-perf` (NinjaTrader verified)
**Active Branch**: `src/epic-13-extraction-v2`
**Next Mission**: EPIC-7-QUALITY (Security P0)

---

## 1. Linear CLI Integration

### Installation Status
✅ **COMPLETE** - Linear CLI installed via npm global

### Configuration Required
**Action**: Run authentication setup
```powershell
npx @linear/cli auth
```

**Follow prompts**:
1. Browser will open to Linear OAuth page
2. Authorize the CLI application
3. Token will be saved to `~/.linear/config.json`

### Usage
```powershell
# List issues
npx @linear/cli issues list --team=V12 --status=backlog

# Create issue
npx @linear/cli issue create --title="EPIC-7 TICKET-001" --description="Remove hardcoded secrets"

# Update issue
npx @linear/cli issue update <ISSUE_ID> --status=in-progress
```

### Integration with Roadmap
**Python sync script exists**: `scripts/linear_sync.py`
- Syncs `docs/brain/master_roadmap.md` → Linear
- Requires `.env` file with `LINEAR_API_KEY`
- See `docs/LINEAR_SETUP_GUIDE.md` for full setup

---

## 2. Git Fetch Prompt Answer

**Question from VS Code**: "Would you like IBM Bob to periodically run 'git fetch'?"

**ANSWER**: **YES** ✅

**Rationale**:
- Keeps local refs up-to-date with remote
- Prevents "branch not based on latest main" errors
- Essential for multi-agent workflow (Bob, Codex, Gemini)
- No performance impact (background operation)
- Required for PR hygiene protocol

**How to enable**:
1. Click "Yes" in VS Code prompt
2. Or manually: `git config --global fetch.prune true`

---

## 3. TICKET-002 Clarification

**Full Title**: "Complete Circuit Breaker Rollback Logic (12 instances)"

**What it is**:
- **NOT** a new feature
- **IS** a bug fix for incomplete error handling

**Technical Details**:
- **File**: `src/V12_002.SIMA.Dispatch.cs`
- **Problem**: Circuit breaker rollback cleans up state but forgets to remove dictionary registrations
- **Impact**: Memory leaks on repeated trip/reset cycles (orphaned entries)
- **Instances**: 12 code paths with incomplete cleanup

**Example Pattern** (pseudocode):
```csharp
// CURRENT (INCOMPLETE)
void RollbackCircuitBreaker() {
    _state = CircuitState.Open;
    _retryCount = 0;
    // ❌ MISSING: _dispatchRegistry.Remove(key);
}

// REQUIRED (COMPLETE)
void RollbackCircuitBreaker() {
    _state = CircuitState.Open;
    _retryCount = 0;
    _dispatchRegistry.Remove(key); // ✅ ADD THIS
}
```

**Priority**: P1 HIGH (memory leak risk)
**Effort**: 4-6 hours
**Status**: OPEN (not started)

---

## 4. Branch Cleanup Strategy

### Current State
- **Total Branches**: 57 local + 200+ remote
- **Active**: `src/epic-13-extraction-v2` (current)
- **Stale**: 50+ branches from completed PRs

### Cleanup Protocol (After PR #18 Merge)

#### Phase 1: Safe Deletion (Merged Branches)
```powershell
# Delete local branches already merged to main
git branch --merged main | Where-Object { $_ -notmatch '\*|main' } | ForEach-Object { git branch -d $_.Trim() }

# Delete remote tracking branches for deleted remotes
git fetch --all --prune
```

#### Phase 2: Stale Branch Audit
```powershell
# List branches by last commit date
git for-each-ref --sort=-committerdate refs/heads/ --format='%(committerdate:short) %(refname:short)'

# Delete branches older than 30 days (after manual review)
git branch --format='%(refname:short) %(committerdate:short)' | Where-Object { $_ -match '2026-04|2026-03' } | ForEach-Object { $branch = $_.Split()[0]; git branch -D $branch }
```

#### Phase 3: Remote Cleanup
```powershell
# Delete merged remote branches (GitHub PR branches)
git push origin --delete <branch-name>

# Or use GitHub UI: Settings → Branches → Delete merged branches
```

### Branches to KEEP (Never Delete)
- `main` (production)
- `pre-refactor-baseline` (historical reference)
- `backup/photon-spsc-hardening-clean-pre-rebase` (safety backup)
- Any branch with open PR

### Recommended Cleanup Targets (56 branches)
**Epic 5 variants** (7 branches - keep only `1111.010-epic5-perf`):
- `1111.010-epic5-perf-docs-only`
- `1111.010-epic5-perf-fix-actual`
- `1111.010-epic5-perf-linq-opt`
- `1111.010-epic5-perf-src-only`
- `1111.010-epic5-perf-v2`

**Codacy cleanup branches** (4 branches - all merged):
- `codacy-phase2-errorprone-clean`
- `codacy-phase2-security-errorprone`
- `epic-quality-critical-clean`
- `epic-quality-curly-braces`

**Phase 7 variants** (3 branches - keep only active):
- `feature/phase7-sprint1-sprint2-extraction`
- `feature/phase7-sprint5-extraction`

**Photon hardening variants** (6 branches - keep only `feature/photon-spsc-hardening`):
- `feature/photon-spsc-hardening-clean`
- `feature/photon-spsc-hardening-perfection`
- `feature/photon-spsc-hardening-repair`
- `feature/photon-spsc-hardening-verified`

**Infrastructure experiments** (5 branches):
- `feature/infra-bob-mode-tooling`
- `feature/infra-linear-sync`
- `feature/infra-session-2026-05-31`

**Testing variants** (3 branches):
- `feature/epic6-testing`
- `feature/epic6-testing-clean`
- `feature/epic6-tests-only`

**Total Cleanup**: ~40 branches safe to delete after PR #18 merge

---

## 5. Master Roadmap vs Linear Alignment

### Current Misalignment Issues

#### Issue 1: Build Tag Mismatch
- **Roadmap**: `1111.007-phase7-t1`
- **Actual**: `1111.010-epic5-perf` (NinjaTrader live)
- **Action**: Update roadmap line 11

#### Issue 2: Phase 7 Status
- **Roadmap**: "IN PROGRESS"
- **Actual**: COMPLETE (18/18 tickets done, M-Phase COMPLETE)
- **Action**: Update roadmap lines 97, 389

#### Issue 3: Missing EPIC-7-QUALITY
- **Roadmap**: Not listed in active tasks
- **Actual**: 5 tickets, 29.5-45 hours, P0 security critical
- **Action**: Add to task list after line 178

#### Issue 4: Missing EPIC-13 Hotfix
- **Roadmap**: No mention of IsExternalInit fix
- **Actual**: PR #13 hotfix committed, awaiting merge
- **Action**: Add to health snapshot

### Alignment Actions Required

**1. Update master_roadmap.md**:
```markdown
**Current Build**: 1111.010-epic5-perf
**Status**: PHASE 7 COMPLETE | EPIC-7-QUALITY NEXT
```

**2. Add EPIC-7-QUALITY to task list**:
```markdown
| **9** | EPIC-7-QUALITY: Security & Debt Consolidation | NEXT |
| **9.1** | TICKET-001: Remove 36 hardcoded secrets (P0) | QUEUED |
| **9.2** | TICKET-002: Circuit breaker rollback (P1) | QUEUED |
| **9.3** | TICKET-003: Missing test coverage (P2) | QUEUED |
| **9.4** | TICKET-005: Build artifacts cleanup (P2) | QUEUED |
| **9.5** | TICKET-004: StyleCop violations (P3) | QUEUED |
```

**3. Update Linear**:
- Create Epic: "EPIC-7-QUALITY: Security & Debt Consolidation"
- Create 5 tickets from `docs/brain/EPIC-7-QUALITY/*.md`
- Assign to both accounts (mkalhitti + malhitticrypto)
- Link to GitHub milestone

**4. Sync Protocol**:
```powershell
# After updating roadmap
python scripts/linear_sync.py

# Verify in Linear
npx @linear/cli issues list --team=V12
```

---

## 6. Next Steps (Priority Order)

### Immediate (Next 2 hours)
1. ✅ Answer git fetch prompt: **YES**
2. ⏳ Authenticate Linear CLI: `npx @linear/cli auth`
3. ⏳ Rebase PR #13 hotfix: `git fetch origin main && git rebase origin/main`
4. ⏳ Push rebased branch: `git push origin src/epic-13-extraction-v2 --force-with-lease`

### Short-Term (Next 24 hours)
5. ⏳ Merge PR #13 (after rebase)
6. ⏳ Update `master_roadmap.md` with current state
7. ⏳ Sync roadmap to Linear: `python scripts/linear_sync.py`
8. ⏳ Branch cleanup: Delete 40 stale branches

### Medium-Term (Next Week)
9. ⏳ Start EPIC-7-QUALITY TICKET-001 (P0 Security)
10. ⏳ Complete TICKET-002 (P1 Circuit Breaker)
11. ⏳ Quick win: TICKET-005 (P2 Build Artifacts - 1 hour)

---

## 7. Documentation Updates Required

### Files to Update
1. **`docs/brain/master_roadmap.md`**:
   - Line 11: Build tag → `1111.010-epic5-perf`
   - Line 97: Phase 7 status → COMPLETE
   - Line 389: Add EPIC-7-QUALITY section
   - Line 531: Health snapshot → add PR #13 hotfix

2. **`docs/brain/nexus_a2a.json`**:
   - Update `current_phase` → "EPIC-7-QUALITY"
   - Update `build_tag` → "1111.010-epic5-perf"
   - Add `pr_13_hotfix` entry

3. **`docs/brain/task.md`**:
   - Update mission → "EPIC-7-QUALITY Security Consolidation"
   - Update build → "1111.010-epic5-perf"

4. **`.env`** (create if missing):
   ```bash
   LINEAR_API_KEY=lin_api_<your_key_here>
   LINEAR_TEAM_ID=<team_id>
   LINEAR_ASSIGNEE_IDS=<user_id_1>,<user_id_2>
   ```

---

## 8. Quality Gate Checklist

Before starting EPIC-7-QUALITY:
- [ ] PR #13 merged to main
- [ ] Build verified in NinjaTrader (F5 test)
- [ ] All 57 branches audited (40 deleted)
- [ ] Linear CLI authenticated
- [ ] Roadmap synced to Linear
- [ ] Pre-push validation passes (13/13 checks)
- [ ] No compilation errors
- [ ] No lock() violations
- [ ] ASCII-only compliance

---

## 9. Risk Assessment

### High Risk
- **36 hardcoded secrets** (TICKET-001) - Security compliance violation
- **12 memory leaks** (TICKET-002) - Production stability risk

### Medium Risk
- **24 missing tests** (TICKET-003) - Regression risk
- **Branch sprawl** (57 branches) - Merge conflict risk

### Low Risk
- **5 build artifacts** (TICKET-005) - Repository bloat
- **2 StyleCop violations** (TICKET-004) - Code quality

---

## 10. Success Metrics

### EPIC-7-QUALITY Completion Criteria
- [ ] Gitleaks scan: 0 secrets
- [ ] cubic-dev-ai scan: 0 incomplete rollback warnings
- [ ] Unit test coverage: >80% for affected files
- [ ] Build: 0 warnings
- [ ] Codacy grade: B → A (3,100 → <1,200 issues)
- [ ] Branch count: 57 → <20

### Timeline
- **TICKET-001** (P0): 8-12 hours
- **TICKET-002** (P1): 4-6 hours
- **TICKET-005** (P2): 1 hour
- **TICKET-003** (P2): 16-24 hours
- **TICKET-004** (P3): 1-2 hours
- **Total**: 30-45 hours (1-2 weeks)

---

## Appendix A: Linear CLI Quick Reference

```powershell
# Authentication
npx @linear/cli auth

# List issues
npx @linear/cli issues list --team=V12 --status=backlog

# Create issue
npx @linear/cli issue create \
  --title="TICKET-001: Remove hardcoded secrets" \
  --description="36 secrets found by Gitleaks" \
  --priority=urgent \
  --label=security

# Update issue
npx @linear/cli issue update <ID> --status=in-progress

# Close issue
npx @linear/cli issue update <ID> --status=done
```

---

## Appendix B: Branch Cleanup Script

```powershell
# Save as: scripts/cleanup_branches.ps1

# Phase 1: Delete merged local branches
Write-Host "Phase 1: Deleting merged local branches..."
git branch --merged main | Where-Object { 
    $_ -notmatch '\*|main|pre-refactor-baseline|backup/' 
} | ForEach-Object { 
    $branch = $_.Trim()
    Write-Host "Deleting: $branch"
    git branch -d $branch
}

# Phase 2: Prune remote tracking branches
Write-Host "`nPhase 2: Pruning remote tracking branches..."
git fetch --all --prune

# Phase 3: List stale branches (manual review)
Write-Host "`nPhase 3: Stale branches (>30 days old):"
git for-each-ref --sort=-committerdate refs/heads/ --format='%(committerdate:short) %(refname:short)' | 
    Where-Object { $_ -match '2026-04|2026-03|2026-02' }

Write-Host "`nCleanup complete. Review stale branches above before force-deleting."
```

---

**Document Version**: 1.0
**Last Updated**: 2026-06-01T22:30:00Z
**Next Review**: After PR #13 merge