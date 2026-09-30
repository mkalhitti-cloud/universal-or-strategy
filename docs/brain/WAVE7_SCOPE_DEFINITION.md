# Wave 7 Scope Definition: 102 Methods to CYC ≤8

**Date**: 2026-06-18
**Status**: READY FOR EXECUTION
**Wave**: 7 of 7 (Final Wave)
**Goal**: Refactor remaining 102 methods to CYC ≤8 (Jane Street strict standard)

## Executive Summary

Wave 7 represents the **final 56.7% of the 180-method refactoring initiative** to achieve complete Jane Street CYC ≤8 compliance. This wave was computed as the **set difference** between the baseline 180 methods and the 78 methods already addressed in Wave 6.

### Key Metrics
- **Wave 6 Methods**: 78 (43.3% of total)
- **Wave 7 Methods**: 102 (56.7% of total)
- **Total Coverage**: 180 methods (100%)
- **Overlap**: 0 methods (validated)
- **Extraction Date**: 2026-06-18

## Method Distribution

### Wave Comparison

| Wave | Epics | Methods | % of Total | Status |
|------|-------|---------|------------|--------|
| Wave 6 | 78 | 78 | 43.3% | Phase 0-1 Complete |
| Wave 7 | TBD | 102 | 56.7% | Ready for Execution |
| **Total** | **TBD** | **180** | **100%** | **In Progress** |

### Complexity Distribution

**Wave 7 Complexity Breakdown**:
- **Low (CYC 9-14)**: 100 methods (98.0%)
- **Medium (CYC 15-19)**: 2 methods (2.0%)
- **High (CYC 20+)**: 0 methods (0.0%)

**Comparison to Wave 6**:
- Wave 7 has **higher average complexity** (more methods in 9-14 range)
- Wave 7 includes the **highest complexity method** (CYC 21)
- Wave 7 requires **more surgical extraction** due to complexity concentration

## Top 10 Most Complex Wave 7 Methods

| Rank | File | Method | CYC | Priority |
|------|------|--------|-----|----------|
| 1 | V12_002.SIMA.Dispatch.cs | Dispatch_PublishMarketBracketToPhoton | 21 | P0 |
| 2 | V12_002.Entries.RMA.cs | MonitorRmaProximity | 17 | P0 |
| 3 | V12_002.UI.IPC.Commands.Fleet.cs | CancelAll_ProcessMasterAccount | 14 | P1 |
| 4 | V12_002.REAPER.Repair.cs | SubmitRepairOrderWithAuthorization | 14 | P1 |
| 5 | V12_002.Orders.Callbacks.AccountOrders.cs | OnAccountOrderUpdate | 14 | P1 |
| 6 | V12_002.Orders.Management.Cleanup.cs | BuildLiveBrokerOrderIndex | 14 | P1 |
| 7 | V12_002.Orders.Callbacks.AccountOrders.cs | HandleMatchedFollower_StopReplacement | 14 | P1 |
| 8 | V12_002.Orders.Callbacks.AccountOrders.cs | HandleMatchedFollowerOrder | 14 | P1 |
| 9 | V12_002.SIMA.Dispatch.cs | Dispatch_ProcessFleetLoop | 14 | P1 |
| 10 | V12_002.SIMA.Fleet.cs | VerifyPhotonSlotIntegrity | 14 | P1 |

## File Distribution

**Top 10 Files by Method Count**:

| File | Methods | % of Wave 7 |
|------|---------|-------------|
| V12_002.Orders.Callbacks.AccountOrders.cs | 12 | 11.8% |
| V12_002.Orders.Callbacks.Propagation.cs | 6 | 5.9% |
| V12_002.Orders.Management.Cleanup.cs | 6 | 5.9% |
| V12_002.REAPER.Audit.cs | 6 | 5.9% |
| V12_002.UI.Compliance.cs | 5 | 4.9% |
| V12_002.Lifecycle.cs | 4 | 3.9% |
| V12_002.Orders.Callbacks.Execution.cs | 4 | 3.9% |
| V12_002.SIMA.Execution.cs | 4 | 3.9% |
| V12_002.UI.IPC.cs | 4 | 3.9% |
| V12_002.Orders.Callbacks.cs | 4 | 3.9% |

**Total Files**: 39 files (29 additional files beyond top 10)

## Validation Results

### Set Difference Validation

```
✅ PASS: No overlap between Wave 6 and Wave 7
✅ PASS: Total coverage = 180 methods (100%)
✅ PASS: Wave 6 (78) + Wave 7 (102) = 180
✅ PASS: All baseline methods accounted for
```

### Extraction Validation

```
Source: baseline_180_methods.json (180 methods)
Wave 6: docs/brain/EPIC-CCN-*/00-hotspots.md (78 methods)
Wave 7: Set difference (102 methods)

Excluded Epics:
  - EPIC-CCN-024: Missing Phase 0 script
  - EPIC-CCN-027: User confirmed "not required"
```

## Wave 7 Special Considerations

### High-Complexity Methods

**Dispatch_PublishMarketBracketToPhoton (CYC 21)**:
- **File**: V12_002.SIMA.Dispatch.cs
- **Risk**: CRITICAL - Highest complexity in entire codebase
- **Strategy**: Multi-phase extraction with FSM decomposition
- **Pilot Candidate**: YES (high complexity representative)

**MonitorRmaProximity (CYC 17)**:
- **File**: V12_002.Entries.RMA.cs
- **Risk**: HIGH - RMA proximity monitoring logic
- **Strategy**: Extract proximity calculation helpers
- **Pilot Candidate**: YES (medium complexity representative)

### File Concentration Risk

**V12_002.Orders.Callbacks.AccountOrders.cs (12 methods)**:
- **Risk**: MEDIUM - High method concentration in single file
- **Mitigation**: Sequential execution to avoid merge conflicts
- **Strategy**: Execute in dependency order (callers before callees)

### Cross-File Dependencies

**SIMA Subsystem** (V12_002.SIMA.*.cs):
- Multiple files with interdependencies
- Requires careful ordering to avoid breaking changes
- Recommend: Execute SIMA methods in single batch

**Order Callbacks** (V12_002.Orders.Callbacks.*.cs):
- Complex call graph across multiple files
- High risk of cascade failures
- Recommend: Extensive blast radius analysis before execution

## Wave 7 Pilot Strategy

### Pilot Selection Criteria

Select **3 pilot epics** representing complexity distribution:

1. **Low Complexity (CYC 9-10)**: Baseline validation
2. **Medium Complexity (CYC 11-15)**: Standard extraction pattern
3. **High Complexity (CYC 16-21)**: Surgical multi-phase extraction

### Recommended Pilot Epics

**Option A: Complexity-Based Selection**
1. **Low**: Any method with CYC 9-10 from V12_002.Lifecycle.cs
2. **Medium**: MonitorRmaProximity (CYC 17) from V12_002.Entries.RMA.cs
3. **High**: Dispatch_PublishMarketBracketToPhoton (CYC 21) from V12_002.SIMA.Dispatch.cs

**Option B: File-Diversity Selection**
1. V12_002.Lifecycle.cs method (CYC 9)
2. V12_002.Orders.Callbacks.AccountOrders.cs method (CYC 14)
3. V12_002.SIMA.Dispatch.cs method (CYC 21)

### Pilot Success Criteria

- ✅ All 3 pilot epics reach Phase 6 completion
- ✅ Build passes after each pilot epic
- ✅ No protocol violations detected
- ✅ Lamport clock deterministic
- ✅ Complexity reduced to CYC ≤8 for all 3 methods
- ✅ No regressions in existing tests

## Execution Plan

### Phase 0: Preparation (1 day)
1. ✅ Extract Wave 7 methods (COMPLETE)
2. ✅ Validate no overlap (COMPLETE)
3. ✅ Export wave7_methods.json (COMPLETE)
4. ⏳ Select 3 pilot epics
5. ⏳ Generate Phase 0 scripts for pilot
6. ⏳ Initialize Lamport clock for Wave 7

### Phase 1: Pilot Execution (1-2 days)
1. Execute Phase 0-6 for 3 pilot epics
2. Validate Lamport clock determinism
3. Document pilot results
4. Fix any issues before full execution

### Phase 2: Full Execution (3-4 days)
1. Generate Phase 0 for remaining 99 epics
2. Execute Phases 0-6 for all 102 epics
3. Monitor progress (4-minute polling)
4. Handle failures (recovery loop)

### Phase 3: Validation (1 day)
1. Verify all 102 methods refactored
2. Run complexity audit (confirm CYC ≤8)
3. Build passes
4. Tests pass
5. Document Wave 7 completion

**Total Timeline**: 5-7 days

## Risk Assessment

### Critical Risks

**Risk 1: High-Complexity Method Failures**
- **Impact**: P0 - Blocks wave completion
- **Probability**: MEDIUM
- **Mitigation**: Pilot testing + multi-phase extraction
- **Fallback**: Manual intervention by Director

**Risk 2: File Concentration Conflicts**
- **Impact**: P1 - Merge conflicts in AccountOrders.cs
- **Probability**: HIGH
- **Mitigation**: Sequential execution + frequent commits
- **Fallback**: Rebase and retry

**Risk 3: SIMA Subsystem Cascade Failures**
- **Impact**: P0 - Breaks multi-account execution
- **Probability**: MEDIUM
- **Mitigation**: Extensive blast radius analysis
- **Fallback**: Rollback and re-plan

### Medium Risks

**Risk 4: Lamport Clock Conflicts**
- **Impact**: P2 - Event ordering issues
- **Probability**: LOW
- **Mitigation**: Global clock with wave-specific ranges
- **Fallback**: Reset clock if conflicts detected

**Risk 5: Pilot Failures**
- **Impact**: P1 - Delays full execution
- **Probability**: MEDIUM
- **Mitigation**: Fix issues before full execution
- **Fallback**: Extend pilot to 5 epics

## Success Criteria

### Wave 7 Completion
- ✅ All 102 epics reach Phase 6 completion
- ✅ Build passes after Wave 7
- ✅ 102 methods refactored to CYC ≤8
- ✅ No regressions in existing tests
- ✅ Documentation complete

### Jane Street Compliance (Overall)
- ✅ All 180 methods refactored to CYC ≤8
- ✅ No methods with CYC > 8 remain
- ✅ CodeScene complexity score ≤8 for all methods
- ✅ Codacy grade improved
- ✅ Build passes
- ✅ Tests pass

## Artifacts

### Generated Files
- ✅ `wave7_methods.json` - 102 methods for Phase 0 script generation
- ✅ `wave6_methods.json` - 78 methods for reference
- ✅ `extract_wave7_methods.ps1` - Extraction script

### Documentation
- ✅ `WAVE7_SCOPE_DEFINITION.md` - This document
- ⏳ `WAVE7_PILOT_PLAN.md` - Pilot strategy (pending)
- ⏳ `WAVE7_PILOT_REPORT.md` - Pilot results (pending)
- ⏳ `WAVE7_EXECUTION_PLAN.md` - Full execution plan (pending)
- ⏳ `WAVE7_COMPLETION_REPORT.md` - Final status (pending)

## Next Steps

1. **Review wave7_methods.json** - Validate method list
2. **Select 3 Pilot Epics** - Use complexity-based selection
3. **Generate Phase 0 Scripts** - For pilot epics only
4. **Initialize Lamport Clock** - Allocate Wave 7 clock range
5. **Execute Pilot** - Validate workflow before full execution
6. **Document Pilot Results** - Capture lessons learned
7. **Generate Full Phase 0** - For remaining 99 epics
8. **Execute Wave 7** - Full 102-epic execution
9. **Validate Completion** - Confirm Jane Street compliance

## References

- **Baseline Audit**: `complexity_audit_fresh_2026-06-14.txt`
- **Wave 6 Status**: `docs/brain/WAVE6_WAVE7_MASTER_PLAN.md`
- **Extraction Script**: `extract_wave7_methods.ps1`
- **Method Lists**: `wave7_methods.json`, `wave6_methods.json`
- **Building Blocks**: `building-blocks/autonomous-refactoring/`

---

**Generated**: 2026-06-18 by extract_wave7_methods.ps1
**Validated**: ✅ No overlap, 100% coverage, 180 methods total
**Status**: READY FOR PILOT EXECUTION