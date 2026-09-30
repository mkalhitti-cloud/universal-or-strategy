# Codacy Remediation: Epic & PR Strategy

**Generated**: 2026-05-27  
**Total Issues**: 1,957  
**Recommended Approach**: 3 Epics with 7 PRs

---

## Epic Structure Recommendation

### ✅ RECOMMENDED: 3 Epics with Incremental PRs

**Rationale**:
- Smaller, focused PRs are easier to review
- Faster feedback loops
- Lower risk of merge conflicts
- Can pause/pivot between epics if priorities change
- Maintains continuous integration (no long-lived branches)

---

## Epic 1: Critical Security & Style (IMMEDIATE)

**Epic Name**: EPIC-QUALITY-CRITICAL  
**Branch**: `epic-quality-critical`  
**Timeline**: Week 1 (3-4 days)  
**Target**: 1,218 issues (62% reduction)

### Scope
- Security: 16 issues
- Error-Prone: 115 issues
- Curly Braces: 909 issues (automated)
- CLS Compliance: 98 issues (automated)
- Redundant Modifiers: 80 issues (automated)

### PR Strategy: 3 PRs

#### PR #1: Security + Error-Prone
**Branch**: `epic-quality-critical` (base)  
**Scope**: 131 issues  
**Est. Diff**: ~500 lines  
**Timeline**: Day 1-2

**Commit Sequence**:
1. Fix all 16 Security issues
2. Fix all 115 Error-Prone issues
3. Run full validation suite
4. F5 verification

**PR Title**: `fix(security): Resolve 131 critical security and error-prone issues`

**PR Description Template**:
```markdown
## Summary
Fixes all P0 security vulnerabilities and error-prone patterns identified by Codacy.

## Issues Resolved
- Security: 16 → 0
- Error-Prone: 115 → 0

## Changes
- [List specific fixes by category]

## Verification
- ✅ All tests pass
- ✅ F5 verification successful
- ✅ Zero new Codacy issues introduced

## Codacy Impact
Before: 1,957 issues  
After: 1,826 issues  
Reduction: 131 issues (7%)
```

**Merge Strategy**: Squash merge to main after approval

---

#### PR #2: Automated Style Fixes (Curly Braces)
**Branch**: `epic-quality-critical` (after PR #1 merged)  
**Scope**: 909 issues  
**Est. Diff**: ~3,000 lines  
**Timeline**: Day 2-3

**Commit Sequence**:
1. Run automated curly brace fixer
2. Review diff for any logic changes (should be zero)
3. Run full validation suite
4. F5 verification

**PR Title**: `style: Auto-fix 909 curly brace violations (Codacy compliance)`

**PR Description Template**:
```markdown
## Summary
Automated fix for all curly brace violations using Roslyn analyzer.

## Issues Resolved
- Enforce Curly Braces: 909 → 0

## Changes
- Applied IDE0011 (Add braces) to all control statements
- Zero logic changes (style-only)

## Verification
- ✅ All tests pass
- ✅ F5 verification successful
- ✅ Diff reviewed for logic changes (none found)

## Codacy Impact
Before: 1,826 issues  
After: 917 issues  
Reduction: 909 issues (50%)
```

**Merge Strategy**: Squash merge to main after approval

---

#### PR #3: Automated Style Fixes (CLS + Redundant)
**Branch**: `epic-quality-critical` (after PR #2 merged)  
**Scope**: 178 issues  
**Est. Diff**: ~200 lines  
**Timeline**: Day 3-4

**Commit Sequence**:
1. Add CLS compliance attributes (98 issues)
2. Remove redundant modifiers (80 issues)
3. Run full validation suite
4. F5 verification

**PR Title**: `style: Auto-fix 178 CLS compliance and redundant modifier violations`

**PR Description Template**:
```markdown
## Summary
Automated fixes for CLS compliance and redundant modifiers.

## Issues Resolved
- Mark Assemblies as CLS Compliant: 98 → 0
- Avoid Redundant Modifiers: 80 → 0

## Changes
- Added [assembly: CLSCompliant(true)] to AssemblyInfo.cs
- Removed redundant access modifiers using IDE0040/IDE0044

## Verification
- ✅ All tests pass
- ✅ F5 verification successful
- ✅ Zero logic changes

## Codacy Impact
Before: 917 issues  
After: 739 issues  
Reduction: 178 issues (19%)
```

**Merge Strategy**: Squash merge to main after approval

---

### Epic 1 Success Criteria
- ✅ 3 PRs merged to main
- ✅ 1,218 issues resolved (62% reduction)
- ✅ Remaining: 739 issues
- ✅ All PRs under 10k diff limit
- ✅ Zero test failures
- ✅ F5 verification passed for each PR

---

## Epic 2: High-Severity Complexity (CRITICAL PATH)

**Epic Name**: EPIC-QUALITY-COMPLEXITY  
**Branch**: `epic-quality-complexity`  
**Timeline**: Week 2-3 (6-8 days)  
**Target**: 140 HIGH severity issues

### Scope
- HIGH severity complexity violations
- File cluster approach (group by file)
- Surgical method extractions

### PR Strategy: 4 PRs (File Clusters)

**Clustering Strategy**:
1. Export all HIGH severity issues
2. Group by file
3. Identify files with 3+ HIGH issues (hot files)
4. Create 4 clusters of ~35 issues each

#### PR #4: Complexity Cluster 1 (UI Hot Files)
**Branch**: `epic-quality-complexity-ui`  
**Scope**: ~35 issues  
**Est. Diff**: ~1,000 lines  
**Timeline**: Day 1-2

**Target Files**:
- `V12_002.UI.Callbacks.cs` (OnKeyDown: 49 CYC)
- `V12_002.UI.Panel.Handlers.cs` (AttachPanelHandlers: 39 CYC)
- `V12_002.UI.Panel.Handlers.cs` (OnSyncAllClick: 37 CYC)

**Approach**: Command Pattern extraction for UI callbacks

**PR Title**: `refactor(ui): Extract UI callback complexity (35 HIGH issues resolved)`

---

#### PR #5: Complexity Cluster 2 (IPC & Commands)
**Branch**: `epic-quality-complexity-ipc`  
**Scope**: ~35 issues  
**Est. Diff**: ~1,000 lines  
**Timeline**: Day 3-4

**Target Files**:
- `V12_002.UI.IPC.cs` (ProcessIpc_MatchSymbol: 49 CYC)
- `V12_002.UI.IPC.Commands.Fleet.cs`
- `V12_002.UI.IPC.Commands.Config.cs`

**Approach**: FSM message router extraction

**PR Title**: `refactor(ipc): Extract IPC command complexity (35 HIGH issues resolved)`

---

#### PR #6: Complexity Cluster 3 (Orders & Trailing)
**Branch**: `epic-quality-complexity-orders`  
**Scope**: ~35 issues  
**Est. Diff**: ~1,000 lines  
**Timeline**: Day 5-6

**Target Files**:
- `V12_002.Trailing.cs` (ManageTrail_RunPerTradeBranches: 36 CYC)
- `V12_002.Orders.Management.StopSync.cs` (ValidateStopPrice: 33 CYC)
- `V12_002.Orders.Management.Flatten.cs`

**Approach**: Per-strategy handler extraction

**PR Title**: `refactor(orders): Extract order management complexity (35 HIGH issues resolved)`

---

#### PR #7: Complexity Cluster 4 (SIMA & Lifecycle)
**Branch**: `epic-quality-complexity-sima`  
**Scope**: ~35 issues  
**Est. Diff**: ~1,000 lines  
**Timeline**: Day 7-8

**Target Files**:
- `V12_002.SIMA.Dispatch.cs` (ExecuteSmartDispatchEntry: 33 CYC)
- `V12_002.SIMA.Lifecycle.cs`
- `V12_002.Lifecycle.cs` (OnStateChangeDataLoaded: 30 CYC)

**Approach**: Initialization pipeline extraction

**PR Title**: `refactor(sima): Extract SIMA lifecycle complexity (35 HIGH issues resolved)`

---

### Epic 2 Success Criteria
- ✅ 4 PRs merged to main
- ✅ 140 HIGH severity issues resolved
- ✅ Remaining: ~599 issues
- ✅ All extracted methods have CYC ≤ 15
- ✅ Zero logic drift
- ✅ F5 verification passed for each PR

---

## Epic 3: Incremental Polish (ONGOING)

**Epic Name**: EPIC-QUALITY-POLISH  
**Branch**: N/A (distributed work)  
**Timeline**: Ongoing (3-4 months)  
**Target**: ~599 remaining issues

### Scope
- MEDIUM/MINOR severity issues
- Best practice violations
- Style inconsistencies
- Unused code cleanup

### PR Strategy: Boy Scout Rule (No Dedicated PRs)

**Approach**:
- Fix issues in files you touch during EPIC-2, EPIC-3, EPIC-4 feature work
- No dedicated epic branch
- Include fixes in feature PRs
- Track progress in weekly health reports

**Example**:
```markdown
## PR for EPIC-2 Feature Work
...
## Codacy Cleanup (Boy Scout Rule)
- Fixed 12 MEDIUM issues in touched files
- Removed 3 unused methods
- Updated 5 XML doc comments

Before: 599 issues  
After: 584 issues  
Reduction: 15 issues
```

### Epic 3 Success Criteria
- ✅ Issue count decreases by 10% per sprint
- ✅ No new issues introduced in PRs
- ✅ Grade improves from B → A over 3 months
- ✅ Final state: 0 issues

---

## PR Workflow Summary

### Timeline Overview

| Week | Epic | PRs | Issues Resolved | Remaining |
|------|------|-----|-----------------|-----------|
| 1 | EPIC-QUALITY-CRITICAL | PR #1-3 | 1,218 (62%) | 739 |
| 2 | EPIC-QUALITY-COMPLEXITY | PR #4-5 | 70 (9%) | 669 |
| 3 | EPIC-QUALITY-COMPLEXITY | PR #6-7 | 70 (9%) | 599 |
| 4+ | EPIC-QUALITY-POLISH | Distributed | ~599 (31%) | 0 |

**Total PRs**: 7 dedicated + distributed cleanup  
**Total Timeline**: 3 weeks (focused) + 3-4 months (polish)

---

## PR Hygiene Protocol (ALL PRs)

### Pre-Push Checklist
- [ ] Run `pre_push_validation.ps1` (13/13 checks pass)
- [ ] Verify diff < 10k lines (split if needed)
- [ ] F5 verification screenshot included
- [ ] Codacy before/after metrics documented
- [ ] Link to remediation plan section

### PR Template (Standard)
```markdown
## Summary
[Brief description of changes]

## Epic
[Link to epic tracking issue]

## Issues Resolved
[Category]: [Before] → [After]

## Changes
- [Bullet list of changes]

## Verification
- [ ] All tests pass
- [ ] F5 verification successful
- [ ] Zero new Codacy issues introduced
- [ ] Diff < 10k lines

## Codacy Impact
Before: [X] issues  
After: [Y] issues  
Reduction: [Z] issues ([%]%)

## Screenshots
[F5 verification screenshot]
```

---

## Alternative Strategy: Single Mega-PR (NOT RECOMMENDED)

### Why NOT Recommended

**Cons**:
- ❌ Massive diff (10k+ lines) violates PR hygiene
- ❌ Difficult to review (cognitive overload)
- ❌ High risk of merge conflicts
- ❌ Long-lived branch (integration hell)
- ❌ All-or-nothing merge (no incremental progress)
- ❌ Harder to rollback if issues found

**Pros**:
- ✅ Single PR to track
- ✅ All changes in one place

**Verdict**: Cons heavily outweigh pros. Use incremental PR strategy.

---

## Recommended Execution Order

### Phase 1: Epic 1 (Week 1)
1. Create branch: `epic-quality-critical`
2. Fix Security + Error-Prone → PR #1
3. Wait for PR #1 merge
4. Auto-fix curly braces → PR #2
5. Wait for PR #2 merge
6. Auto-fix CLS + redundant → PR #3
7. Wait for PR #3 merge
8. **Checkpoint**: 1,218 issues resolved (62%)

### Phase 2: Epic 2 (Week 2-3)
1. Create branch: `epic-quality-complexity`
2. Extract UI complexity → PR #4
3. Wait for PR #4 merge
4. Extract IPC complexity → PR #5
5. Wait for PR #5 merge
6. Extract Orders complexity → PR #6
7. Wait for PR #6 merge
8. Extract SIMA complexity → PR #7
9. Wait for PR #7 merge
10. **Checkpoint**: 140 issues resolved (7%)

### Phase 3: Epic 3 (Ongoing)
1. No dedicated branch
2. Fix issues during feature work
3. Track progress weekly
4. **Target**: 599 → 0 over 3-4 months

---

## Success Metrics

### Epic 1 (Week 1)
- **Issues**: 1,957 → 739 (62% reduction)
- **PRs**: 3/3 merged
- **Grade**: B → B+ (expected)

### Epic 2 (Week 2-3)
- **Issues**: 739 → 599 (19% reduction)
- **PRs**: 4/4 merged
- **Grade**: B+ → A- (expected)

### Epic 3 (Ongoing)
- **Issues**: 599 → 0 (31% reduction)
- **PRs**: Distributed
- **Grade**: A- → A (target)

### Final State
- **Total Issues**: 1,957 → 0 (100%)
- **Total PRs**: 7 dedicated + distributed
- **Grade**: B → A
- **Timeline**: 3 weeks (focused) + 3-4 months (polish)

---

## Risk Mitigation

### Risk: PR #2 (Curly Braces) Exceeds 10k Diff
**Mitigation**: Split into 2 PRs if needed (e.g., by file count)

### Risk: Merge Conflicts Between PRs
**Mitigation**: Always rebase before creating new PR, use short-lived branches

### Risk: F5 Verification Fails Mid-Epic
**Mitigation**: Rollback to last known good commit, fix issue, resume

### Risk: Codacy API Rate Limit
**Mitigation**: Cache exports locally, batch operations

---

## Next Steps

1. **Review this strategy** with Director
2. **Create Epic 1 branch**: `epic-quality-critical`
3. **Begin PR #1**: Security + Error-Prone fixes
4. **Execute incrementally**: Wait for each PR merge before starting next

**Recommended Start**: Immediately (Week 1 execution)