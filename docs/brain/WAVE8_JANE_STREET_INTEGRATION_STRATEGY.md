# Wave 8: Jane Street Violation Integration Strategy

**Version**: 1.0  
**Date**: 2026-06-18  
**Status**: PROPOSAL - Awaiting Director Approval

## Executive Summary

This document proposes a strategy for integrating 299 Jane Street P0 violations into Wave 8 execution alongside 180 complexity reduction epics.

## Current State Analysis

### Complexity Baseline (180 Methods)
- **Source**: `complexity_audit_fresh_2026-06-14.txt`
- **Criteria**: CYC > 8 (Jane Street strict standard)
- **Target**: Reduce all methods to CYC ≤ 8
- **Scope**: Surgical extraction/refactoring

### Jane Street Violations (299 P0)
- **Source**: `jane_street_p0_violations.json`
- **Categories**: Type Safety, Philosophy, Concurrency, Performance
- **Severity**: P0 (Critical - blocks production deployment)
- **Scope**: Architectural fixes, not just refactoring

### File Overlap Analysis
From previous analysis:
- **174 violations (58%)** - In files targeted by Wave 8 complexity epics
- **125 violations (42%)** - In files NOT targeted by Wave 8

## Strategic Options

### Option A: Sequential Execution (RECOMMENDED)
**Order**: Complexity First → Jane Street Second

**Rationale**:
1. **Complexity reduction creates clean foundation** for Jane Street fixes
2. **Simpler code = easier to fix violations** (fewer edge cases)
3. **No scope creep** - Each epic has ONE concern (V12.23 mandate)
4. **Clear success criteria** - CYC ≤ 8 first, then violations
5. **Building-Blocks Method** - Proven Wave 5 templates for complexity

**Execution Plan**:
```
Wave 8A: Complexity Reduction (180 epics)
├─ EPIC-W8A-001 through EPIC-W8A-180
├─ Target: CYC ≤ 8 for all methods
├─ Duration: ~4 weeks (VM parallel execution)
└─ Success: All 180 methods at CYC ≤ 8

Wave 8B: Jane Street Violations (299 epics)
├─ EPIC-W8B-001 through EPIC-W8B-299
├─ Target: Zero P0 violations
├─ Duration: ~6 weeks (architectural changes)
└─ Success: All P0 violations resolved
```

**Advantages**:
- ✅ Clean separation of concerns
- ✅ Can start Wave 8A immediately (proven templates)
- ✅ Wave 8B benefits from simplified code
- ✅ Clear checkpoints for progress tracking
- ✅ No risk of scope creep

**Disadvantages**:
- ⚠️ Longer total timeline (10 weeks vs potential 6-8 weeks)
- ⚠️ May touch same files twice (174 overlapping violations)

---

### Option B: Parallel Execution
**Order**: Complexity + Jane Street Simultaneously

**Rationale**:
1. **Faster completion** - Both concerns addressed in one pass
2. **Fewer file touches** - Fix violations during extraction
3. **Holistic approach** - Consider violations during refactoring

**Execution Plan**:
```
Wave 8: Unified Execution (180 epics)
├─ EPIC-W8-001 through EPIC-W8-180
├─ Primary: CYC ≤ 8 for target method
├─ Secondary: Fix Jane Street violations in same file
├─ Duration: ~6 weeks
└─ Success: CYC ≤ 8 + violations in scope files resolved

Wave 8-Cleanup: Remaining Violations (125 epics)
├─ EPIC-W8C-001 through EPIC-W8C-125
├─ Target: Files NOT in complexity scope
├─ Duration: ~2 weeks
└─ Success: All remaining P0 violations resolved
```

**Advantages**:
- ✅ Faster total timeline (8 weeks vs 10 weeks)
- ✅ Fewer file touches (174 files touched once vs twice)
- ✅ Holistic fixes (consider violations during extraction)

**Disadvantages**:
- ❌ **SCOPE CREEP RISK** - Violates V12.23 "One Epic = One Concern"
- ❌ **Complex success criteria** - Two goals per epic
- ❌ **Harder to track** - Which goal failed if epic fails?
- ❌ **No proven templates** - Would need to create new patterns
- ❌ **Higher cognitive load** - Agent must juggle two concerns

---

### Option C: Hybrid Execution
**Order**: Complexity First, Bundle Violations Where Overlap Exists

**Rationale**:
1. **Start clean** - Wave 8A focuses on complexity only
2. **Bundle opportunistically** - Wave 8B bundles violations by file
3. **Minimize file touches** - Group violations in same file

**Execution Plan**:
```
Wave 8A: Complexity Reduction (180 epics)
├─ EPIC-W8A-001 through EPIC-W8A-180
├─ Target: CYC ≤ 8 ONLY
├─ Duration: ~4 weeks
└─ Success: All 180 methods at CYC ≤ 8

Wave 8B: Jane Street Violations - Bundled by File (87 epics)
├─ EPIC-W8B-001 through EPIC-W8B-087
├─ Each epic = ALL violations in ONE file
├─ 174 violations in 87 files (avg 2 violations/file)
├─ Duration: ~3 weeks
└─ Success: All violations in Wave 8 files resolved

Wave 8C: Jane Street Violations - Remaining Files (125 epics)
├─ EPIC-W8C-001 through EPIC-W8C-125
├─ Files NOT in complexity scope
├─ Duration: ~3 weeks
└─ Success: All remaining P0 violations resolved
```

**Advantages**:
- ✅ Clean complexity phase (proven templates)
- ✅ Reduced file touches (bundle violations per file)
- ✅ Clear success criteria per phase
- ✅ Moderate timeline (10 weeks total)

**Disadvantages**:
- ⚠️ Still touches files twice (complexity then violations)
- ⚠️ Need to create bundling logic for Wave 8B

---

## Recommendation Matrix

| Criterion | Option A (Sequential) | Option B (Parallel) | Option C (Hybrid) |
|-----------|----------------------|---------------------|-------------------|
| **Scope Creep Risk** | ✅ Low | ❌ High | ⚠️ Medium |
| **Timeline** | ⚠️ 10 weeks | ✅ 8 weeks | ⚠️ 10 weeks |
| **File Touch Count** | ❌ 2x for 174 files | ✅ 1x for 174 files | ❌ 2x for 174 files |
| **Success Criteria Clarity** | ✅ Clear | ❌ Complex | ✅ Clear |
| **Building-Blocks Compliance** | ✅ Yes (Wave 5 templates) | ❌ No (new patterns) | ⚠️ Partial |
| **V12.23 Compliance** | ✅ Yes | ❌ No | ✅ Yes |
| **Cognitive Load** | ✅ Low | ❌ High | ⚠️ Medium |
| **Proven Approach** | ✅ Yes | ❌ No | ⚠️ Partial |

## Director Decision Required

**Question**: Which option should we use for Wave 8?

**My Recommendation**: **Option A (Sequential Execution)**

**Rationale**:
1. **V12.23 Mandate**: "One Epic = One Concern" - Option A strictly complies
2. **Building-Blocks Method**: Can use proven Wave 5 templates immediately
3. **Risk Mitigation**: Lower scope creep risk, clearer success criteria
4. **Proven Pattern**: Sequential execution has worked in previous waves
5. **Timeline Acceptable**: 2 extra weeks is worth the reduced risk

**Trade-off Accepted**: Touching 174 files twice is acceptable given:
- First pass simplifies code (CYC ≤ 8)
- Second pass fixes violations in simpler code (easier to reason about)
- Clear checkpoint between phases (can pause/resume)

## Implementation Details (If Option A Approved)

### Wave 8A: Complexity Reduction
1. **Generate 180 epics** from `complexity_audit_fresh_2026-06-14.txt`
2. **Copy Phase 0-6 scripts** from Wave 5 templates
3. **Execute on VM** with 4-minute polling
4. **Success Gate**: All 180 methods at CYC ≤ 8

### Wave 8B: Jane Street Violations
1. **Generate 299 epics** from `jane_street_p0_violations.json`
2. **Create new Phase templates** for violation fixes (no proven templates exist)
3. **Prioritize by severity** within P0 (Type Safety → Concurrency → Performance → Philosophy)
4. **Success Gate**: Zero P0 violations

### Cross-Reference Tracking
- Maintain `wave8_overlap_map.json` showing which violations are in Wave 8A files
- After Wave 8A completes, re-scan violations to see if any were incidentally fixed
- Adjust Wave 8B scope based on remaining violations

## Open Questions

1. **Should Wave 8B use same phase structure as Wave 8A?**
   - Violations may need different phases (no "hotspot analysis" for violations)
   - May need: Phase 0 (Violation Analysis) → Phase 1 (Fix Plan) → Phase 2 (Execute) → Phase 3 (Verify)

2. **How to handle violations fixed incidentally during Wave 8A?**
   - Re-scan after Wave 8A completes
   - Remove from Wave 8B scope
   - Document in completion report

3. **Should we create separate roadmaps for 8A and 8B?**
   - `wave8a_roadmap.json` (180 complexity epics)
   - `wave8b_roadmap.json` (299 violation epics)
   - Or unified `wave8_roadmap.json` with phase markers?

## Next Steps (Pending Approval)

1. ✅ Director approves Option A, B, or C
2. ✅ Generate Wave 8A epic roadmap (180 epics)
3. ✅ Copy Phase 0-6 scripts from Wave 5 (Building-Blocks)
4. ✅ Execute Wave 8A Phase 0 pilot (3 epics)
5. ✅ Scale to full Wave 8A execution
6. ⏸️ After Wave 8A complete, plan Wave 8B

---

**Awaiting Director Decision**: Which option (A, B, or C) should we proceed with?