# Wave Rollback Checklist (V12.38)

**Version**: 1.0  
**Effective**: 2026-06-16  
**Purpose**: Quick reference checklist for wave rollback execution

## Pre-Rollback

### Analysis Phase

- [ ] **Greptile audit complete**: All PRs audited for issues
- [ ] **Issue count documented**: P0/P1/P2 breakdown recorded
- [ ] **Failure rate calculated**: (Failed epics / Total epics) × 100%
- [ ] **Root cause identified**: What went wrong documented
- [ ] **Protocol gaps listed**: What failed in current protocols

### Decision Phase

- [ ] **Rollback scope decided**: Keep/skip/local/retry counts determined
- [ ] **Cost analysis complete**: Lost vs retry vs fix-in-place calculated
- [ ] **Decision matrix applied**: Used WAVE_ROLLBACK_PROTOCOL.md matrix
- [ ] **Director approval obtained**: If manual trigger (not automatic)

### Communication Phase

- [ ] **Team notified**: Rollback decision communicated
- [ ] **Timeline updated**: Retry wave schedule adjusted
- [ ] **Stakeholders informed**: Impact assessment shared

## Rollback Execution

### Step 1: Close PRs

- [ ] **List all open PRs**: `gh pr list --state open`
- [ ] **Close each PR with reason**: Include issue count and rollback rationale
- [ ] **Document PR numbers**: Record closed PR list
- [ ] **Verify 0 open PRs**: `gh pr list --state open` returns empty

**Commands**:
```bash
gh pr list --state open
gh pr close <PR_NUMBER> --comment "Closing: Wave N rollback due to <reason>. Issues: P0=X, P1=Y, P2=Z. Retry in Wave N+1."
```

### Step 2: Revert Merged PRs

- [ ] **Check for merged PRs**: `git log --oneline --grep="EPIC-CCN" -10`
- [ ] **If merged: revert commits**: `git revert <commit-hash> --no-edit`
- [ ] **Push reverts to main**: `git push origin main`
- [ ] **Verify reverts successful**: Check git log

**Commands**:
```bash
git checkout main
git pull origin main
git log --oneline --grep="EPIC-CCN" -10
# If merged PRs found:
git revert <commit-hash> --no-edit
git push origin main
```

### Step 3: Delete Phase 5-6 Files

- [ ] **Create deletion script**: For retry epics only
- [ ] **Execute script**: Run PowerShell deletion script
- [ ] **Verify file counts**: Before/after comparison
- [ ] **Commit deletion**: To gitbutler/workspace

**Script Template**:
```powershell
$retryEpics = 1..26 + 28..80 | ForEach-Object { "EPIC-CCN-{0:D3}" -f $_ }
foreach ($epic in $retryEpics) {
    $brainDir = "docs/brain/$epic"
    if (Test-Path $brainDir) {
        Remove-Item "$brainDir/ticket-*-completion.md" -ErrorAction SilentlyContinue
        Remove-Item "$brainDir/06-verification-report.md" -ErrorAction SilentlyContinue
        Write-Host "Deleted Phase 5-6 files for $epic"
    }
}
```

### Step 4: Update Roadmap

- [ ] **Mark invalid epics**: `"status": "INVALID"`
- [ ] **Mark encoding-sensitive**: `"execution": "local"`
- [ ] **Mark retry epics**: `"status": "pending"`
- [ ] **Commit roadmap changes**: With detailed message

**Commit Message Template**:
```
rollback: Wave N Phase 5-6 (X epics)

- Closed PRs #X-Y (Z issues found)
- Deleted Phase 5-6 files for X retry epics
- Marked Y epics as INVALID
- Marked Z epics for local execution

Root cause: <description>
Solution: <hardening plan>
Cost: $X lost, $Y retry = $Z total impact
```

## Post-Rollback

### Verification Phase

- [ ] **All PRs closed or reverted**: 0 open PRs remain
- [ ] **Phase 5-6 files deleted**: For retry epics only
- [ ] **Roadmap updated**: Final status for all epics
- [ ] **Rollback committed**: To gitbutler/workspace
- [ ] **Build passes**: Local build successful

### Documentation Phase

- [ ] **Root cause analysis**: Documented in `docs/brain/WAVE-N/rollback-analysis.md`
- [ ] **Protocol gaps identified**: Listed with severity
- [ ] **Hardening plan created**: Specific actions to fix gaps
- [ ] **Lessons learned documented**: For future waves

### Communication Phase

- [ ] **Team updated**: Rollback complete notification
- [ ] **Timeline confirmed**: Retry wave schedule
- [ ] **Success criteria defined**: For retry wave

## Retry Preparation

### Protocol Hardening Phase

- [ ] **All protocol gaps fixed**: Updates committed
- [ ] **SOPs updated**: Missing steps added
- [ ] **Skills updated**: Missing checks added
- [ ] **Custom modes updated**: Missing mandates added

### Validation Phase

- [ ] **Pilot test plan created**: Single epic test
- [ ] **Pilot test executed**: Successfully completed
- [ ] **0 P0/P1 issues in pilot**: Quality gate passed
- [ ] **Pilot results documented**: Success criteria met

### Approval Phase

- [ ] **Director approval obtained**: For retry wave
- [ ] **Cost estimate updated**: Based on hardened protocols
- [ ] **Timeline confirmed**: Retry wave schedule
- [ ] **Team ready**: All agents briefed on changes

## Quick Reference: Rollback Triggers

### Automatic (No Approval Needed)

- ✅ P0 compilation blocker in ANY PR
- ✅ >20% epic failure rate
- ✅ >5 P0 issues across wave
- ✅ Scope creep in >10% of epics

### Manual (Director Approval Required)

- ⚠️ 10-20% failure rate with 0 P0 issues
- ⚠️ Cost-benefit analysis favors rollback
- ⚠️ Systemic protocol gap detected
- ⚠️ Timeline pressure requires clean slate

## Quick Reference: Rollback Scope Matrix

| Scenario | Keep | Skip | Local | Retry |
|----------|------|------|-------|-------|
| All PRs clean | All | 0 | 0 | 0 |
| 1-2 PRs buggy | Clean | Invalid | Encoding | Buggy |
| >50% PRs buggy | 0 | Invalid | Encoding | All |
| P0 in ANY PR | 0 | Invalid | Encoding | All |

## Sign-Off

### Rollback Execution

- **Executed By**: _______________
- **Date**: _______________
- **Wave Number**: _______________
- **Epics Affected**: _______________

### Root Cause

- **Primary Cause**: _______________
- **Contributing Factors**: _______________
- **Protocol Gaps**: _______________

### Retry Plan

- **Retry Wave**: _______________
- **Hardening Actions**: _______________
- **Estimated Retry Cost**: $_______________
- **Expected Completion**: _______________

### Approval

- **Director Approval**: _______________
- **Date**: _______________
- **Notes**: _______________

## Version History

- **V1.0 (V12.38)** - 2026-06-16: Initial checklist based on Wave 4 rollback experience