# Wave Rollback Protocol (V12.38)

**Version**: 1.0  
**Effective**: 2026-06-16  
**Status**: MANDATORY for all wave-based autonomous refactoring

## Purpose

This protocol standardizes rollback procedures for failed waves, ensuring consistent decision-making, execution, and recovery across all autonomous refactoring operations.

## When to Rollback

### Automatic Rollback Triggers

The following conditions trigger IMMEDIATE rollback without Director approval:

1. **P0 Compilation Blockers**: ANY P0 issue in ANY PR
2. **High Failure Rate**: >20% epic failure rate (e.g., 16/80 failed)
3. **Quality Threshold**: >5 P0 issues across wave (Greptile audit)
4. **Scope Creep**: Detected in >10% of epics

### Manual Rollback Decision

Requires Director approval and analysis:

1. **Cost-Benefit Analysis**: Rollback cost vs fix-in-place cost
2. **Timeline Impact**: Delay assessment for retry wave
3. **Protocol Gap Severity**: Systemic vs isolated issues
4. **Learning Value**: ROI of rollback for protocol hardening

## Rollback Scope Decision Matrix

Use this matrix to determine which epics to keep, skip, retry, or execute locally:

| Scenario | Keep | Skip | Local | Retry | Rationale |
|----------|------|------|-------|-------|-----------|
| All PRs clean | All | 0 | 0 | 0 | No rollback needed |
| 1-2 PRs buggy | Clean PRs | Invalid | Encoding | Buggy | Surgical fix |
| 3-5 PRs buggy | Clean PRs | Invalid | Encoding | Buggy | Partial rollback |
| >50% PRs buggy | 0 | Invalid | Encoding | All | Full rollback |
| P0 in ANY PR | 0 | Invalid | Encoding | All | Safety first |
| Scope creep detected | 0 | Creep | Encoding | Clean | Prevent cascade |

**Legend**:
- **Keep**: PRs are clean, merge them
- **Skip**: Epics are invalid (out of scope, already done)
- **Local**: Encoding-sensitive epics (UTF-16 issues, execute locally)
- **Retry**: Re-execute in next wave with hardened protocols

## 4-Step Rollback Procedure

### Step 1: Close All PRs

**Purpose**: Prevent accidental merges during rollback

**Commands**:
```bash
# List all open PRs
gh pr list --state open

# Close each PR with rollback reason
gh pr close <PR_NUMBER> --comment "Closing: Wave N rollback due to <reason>. Will retry in Wave N+1 with hardened protocols. Issues found: <count> (P0: X, P1: Y, P2: Z)"

# Verify all closed
gh pr list --state open
# Expected: 0 open PRs
```

**Checklist**:
- [ ] All open PRs listed
- [ ] Each PR closed with reason
- [ ] Issue count documented in comment
- [ ] 0 open PRs remain

### Step 2: Revert Merged PRs (if any)

**Purpose**: Undo any PRs that were merged before rollback decision

**Commands**:
```bash
# Switch to main
git checkout main
git pull origin main

# Find merged PR commits
git log --oneline --grep="EPIC-CCN" -10

# Revert merge commit (if any merged)
git revert <commit-hash> --no-edit

# Push revert
git push origin main

# Verify revert successful
git log --oneline -5
```

**Checklist**:
- [ ] Checked for merged PRs
- [ ] Reverted all merged PRs (if any)
- [ ] Pushed reverts to main
- [ ] Verified reverts successful

### Step 3: Delete Phase 5-6 Files

**Purpose**: Clean up execution artifacts for retry epics

**Script** (PowerShell):
```powershell
# Define retry epics (example: Wave 4 had 78 retry epics)
$retryEpics = 1..26 + 28..80 | ForEach-Object { "EPIC-CCN-{0:D3}" -f $_ }

# Delete Phase 5-6 files
foreach ($epic in $retryEpics) {
    $brainDir = "docs/brain/$epic"
    if (Test-Path $brainDir) {
        # Delete ticket completion files
        Remove-Item "$brainDir/ticket-*-completion.md" -ErrorAction SilentlyContinue
        
        # Delete verification report
        Remove-Item "$brainDir/06-verification-report.md" -ErrorAction SilentlyContinue
        
        Write-Host "Deleted Phase 5-6 files for $epic"
    }
}

# Verify deletion
Write-Host "`nVerification:"
$retryEpics | ForEach-Object {
    $brainDir = "docs/brain/$_"
    if (Test-Path $brainDir) {
        $phase5Files = Get-ChildItem "$brainDir/ticket-*-completion.md" -ErrorAction SilentlyContinue
        $phase6Files = Get-ChildItem "$brainDir/06-verification-report.md" -ErrorAction SilentlyContinue
        if ($phase5Files -or $phase6Files) {
            Write-Host "WARNING: $_ still has Phase 5-6 files"
        }
    }
}
```

**Checklist**:
- [ ] Retry epic list created
- [ ] Deletion script executed
- [ ] File counts verified (before/after)
- [ ] No Phase 5-6 files remain for retry epics

### Step 4: Update Roadmap

**Purpose**: Mark epic status for next wave

**Actions**:
1. Edit `epic_roadmap.json`
2. Mark invalid epics: `"status": "INVALID"`
3. Mark encoding-sensitive epics: `"execution": "local"`
4. Mark retry epics: `"status": "pending"`
5. Commit changes

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

Issues breakdown:
- P0: X (compilation blockers)
- P1: Y (logic errors)
- P2: Z (style violations)

Hardening actions:
1. <action 1>
2. <action 2>
3. <action 3>
```

**Checklist**:
- [ ] Invalid epics marked
- [ ] Encoding-sensitive epics marked for local
- [ ] Retry epics marked as pending
- [ ] Roadmap committed with detailed message

## Pre-Rollback Checklist

Before executing rollback, verify:

- [ ] **Greptile audit complete**: All PRs audited
- [ ] **Issue count documented**: P0/P1/P2 breakdown
- [ ] **Failure rate calculated**: Failed epics / total epics
- [ ] **Root cause identified**: What went wrong
- [ ] **Rollback scope decided**: Keep/skip/local/retry counts
- [ ] **Cost analysis complete**: Lost vs retry vs fix-in-place
- [ ] **Director approval obtained**: If manual trigger

## Post-Rollback Checklist

After executing rollback, verify:

- [ ] **All PRs closed or reverted**: 0 open PRs
- [ ] **Phase 5-6 files deleted**: For retry epics only
- [ ] **Roadmap updated**: Final status for all epics
- [ ] **Rollback committed**: To gitbutler/workspace
- [ ] **Protocol gaps identified**: What failed
- [ ] **Hardening plan created**: How to fix
- [ ] **Lessons learned documented**: For future waves

## Rollback Cost Calculation

### Formula

```
Lost Cost = (Retry Epics × Phase 5-6 Cost per Epic)
Retry Cost = (Retry Epics × Phase 5-6 Cost per Epic)
Total Impact = Lost Cost + Retry Cost
```

### Example: Wave 4 Rollback

**Inputs**:
- Retry Epics: 78
- Phase 5-6 Cost: $0.05/epic
- Phase 5 Cost: $0.03/epic
- Phase 6 Cost: $0.02/epic

**Calculation**:
```
Lost Cost = 78 × $0.05 = $3.90
Retry Cost = 78 × $0.05 = $3.90
Total Impact = $3.90 + $3.90 = $7.80
```

**Comparison to Fix-in-Place**:
```
Fix Cost = 28 issues × $0.15/issue = $4.20
Retest Cost = 7 PRs × $0.50/PR = $3.50
Total Fix = $4.20 + $3.50 = $7.70

Rollback Savings = $7.70 - $7.80 = -$0.10 (slight loss)
```

**Decision**: Rollback chosen for protocol hardening value (prevents future cascades)

### Cost-Benefit Decision Tree

```
Is Total Impact < Fix Cost?
├─ YES → Rollback (cheaper)
└─ NO → Is protocol gap systemic?
   ├─ YES → Rollback (prevent cascade)
   └─ NO → Fix in place (isolated issue)
```

## Recovery Protocol

### Immediate Actions (Day 0)

1. **Execute 4-step rollback**: Close PRs, revert, delete, update
2. **Document root cause**: What went wrong
3. **Identify protocol gaps**: What failed
4. **Create hardening plan**: How to fix

### Short-Term Actions (Day 1-3)

1. **Update protocols**: Fix identified gaps
2. **Update SOPs**: Add missing steps
3. **Update skills**: Add missing checks
4. **Update custom modes**: Add missing mandates

### Validation Actions (Day 4-7)

1. **Run pilot test**: Validate hardened protocols
2. **Verify 0 P0/P1 issues**: In pilot epic
3. **Document pilot results**: Success criteria met
4. **Obtain Director approval**: For retry wave

### Retry Actions (Day 8+)

1. **Launch retry wave**: With hardened protocols
2. **Monitor closely**: First 10 epics
3. **Apply recovery loop**: If any failures
4. **Document improvements**: Lessons learned

## Case Study: Wave 4 Rollback (2026-06-15)

### Context

- **Wave**: 4
- **Epics**: 80 total
- **Completed**: 79/80 (98.75%)
- **PRs Created**: 7
- **Issues Found**: 28 (Greptile audit)

### Trigger

**Automatic**: P0 compilation blockers detected

### Analysis

**Issue Breakdown**:
- P0: 9 (compilation blockers)
- P1: 12 (logic errors)
- P2: 6 (style violations)

**Failure Rate**: 87.5% (7/8 PRs had issues)

**Root Cause**: Bob CLI over-optimization
- No SURGICAL ONLY mandate
- No explicit verification protocol
- No scope boundary enforcement

### Decision

**Rollback Scope**:
- Keep: 0 PRs
- Skip: 1 epic (EPIC-CCN-027, invalid)
- Local: 1 epic (EPIC-CCN-045, encoding-sensitive)
- Retry: 78 epics

**Rationale**: P0 blockers = safety risk, full rollback required

### Execution

**Step 1**: Closed 7 PRs with rollback reason
**Step 2**: No merged PRs to revert
**Step 3**: Deleted Phase 5-6 files for 78 retry epics
**Step 4**: Updated roadmap with final status

### Cost

```
Lost Cost: 78 × $0.05 = $3.90
Retry Cost: 78 × $0.05 = $3.90
Total Impact: $7.80
```

### Hardening

**Protocol Updates**:
1. **V12.34**: Added SURGICAL ONLY mandate
2. **V12.34**: Added 5-check verification protocol
3. **V12.23**: Added scope boundary validation
4. **V12.36**: Added VM-Local git sync protocol

**SOP Updates**:
1. Updated WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md
2. Updated autonomous-refactor custom mode
3. Updated gcp-vm-wave-execution skill

### Outcome

**Wave 5 Pilot Test**: Caught additional protocol gap (V12.37)
- Issue: Stale working tree despite commit match
- Fix: 7-step sync with working tree verification
- ROI: $0.06 spent, $4.62 saved (77x)

**Lesson**: Rollback investment paid off by catching cascade before 77-epic execution

## Version History

- **V1.0 (V12.38)** - 2026-06-16: Initial rollback protocol based on Wave 4 experience
  - 4-step rollback procedure
  - Decision matrix for scope determination
  - Cost calculation formula
  - Wave 4 case study
  - Recovery protocol