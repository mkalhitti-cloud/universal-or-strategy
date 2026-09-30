# Wave 6 + Wave 7 Master Plan: Complete Jane Street CYC ≤8 Compliance

**Date**: 2026-06-18
**Status**: PLANNING - Pre-Execution
**Goal**: Refactor ALL 180 methods to CYC ≤8 (Jane Street strict standard)

## Executive Summary

**User Decision**: Option B with modifications
- Complete Wave 6 as pilot (79 methods)
- Create Wave 7 for remaining 101 methods
- **CRITICAL**: Wave 7 also needs pilot testing
- **CRITICAL**: Map Wave 6 methods to avoid duplication in Wave 7
- **CRITICAL**: Establish deterministic Lamport clock for all 180 methods
- **CRITICAL**: Document everything to avoid circular restarts

## The Complete Scope: 180 Methods

### Baseline Audit
- **Source**: `complexity_audit_fresh_2026-06-14.txt`
- **Total Methods**: 363 analyzed
- **Methods CYC > 8**: **180 methods** (requires refactoring)
- **Methods CYC ≤ 8**: 183 methods (compliant)

### Wave Distribution
- **Wave 6**: 79 methods (44% of total)
- **Wave 7**: 101 methods (56% of total)
- **Total**: 180 methods (100% coverage)

## Wave 6 Status (Current)

### Scope
- **Epics**: 78 (EPIC-CCN-001 through 080, excluding 024 and 027)
- **Methods**: 79 (77 single-method + 1 multi-method with 2 methods)
- **Special Cases**:
  - EPIC-003: Local execution (due to .dll dependency)
  - EPIC-024: Missing Phase 0 script (excluded)
  - EPIC-027: User confirmed "not required" (excluded)

### Phase Completion
- Phase 0 (Hotspot Analysis): ✅ 78/78 complete
- Phase 1 (Scope Definition): ✅ 79/79 complete
- Phase 1.5 (Boundary Validation): ⚠️ FROZEN (unknown completion)
- Phases 2-6: ⏳ Pending

### Issues Identified
1. **Phase 1.5 Freeze**: Inline Bob CLI messages (SOP violation)
2. **Scope Incomplete**: Only 44% of required work
3. **No Pilot for Wave 7**: Need to test before full execution

## Wave 7 Planning (New)

### Scope Determination Strategy

**Step 1: Extract Wave 6 Methods**
```python
# Parse Wave 6 Phase 0 hotspot files
wave6_methods = []
for epic in range(1, 81):
    if epic in [24, 27]:
        continue
    hotspot_file = f"docs/brain/EPIC-CCN-{epic:03d}/00-hotspots.md"
    # Extract method names and files
```

**Step 2: Extract All 180 Methods from Baseline**
```python
# Parse complexity_audit_fresh_2026-06-14.txt
all_methods = []
# Format: "  - File.cs::MethodName (CYC=15, LOC=32)"
```

**Step 3: Compute Wave 7 Methods**
```python
wave7_methods = all_methods - wave6_methods
# Should yield 101 methods
```

**Step 4: Validate No Overlap**
```python
assert len(wave6_methods) == 79
assert len(wave7_methods) == 101
assert len(wave6_methods & wave7_methods) == 0  # No duplicates
assert len(wave6_methods | wave7_methods) == 180  # Complete coverage
```

### Wave 7 Special Cases

**Carry Forward from Wave 6**:
- **EPIC-003 Pattern**: If Wave 7 has .dll dependencies, execute locally
- **Missing Scripts**: Pre-validate all Phase 0 scripts exist
- **Exclusions**: Document any user-confirmed exclusions

**New Considerations**:
- Multi-method epics (if any methods in same file)
- Cross-file dependencies
- Shared utility methods

## Deterministic Lamport Clock Strategy

### Problem
Current Lamport clock is per-wave, not global. This causes:
- Wave 6 starts at event 0
- Wave 7 would also start at event 0
- No global ordering across waves

### Solution: Global Lamport Clock

**Architecture**:
```
.lamport/
  ├─ global_event_log.jsonl        # Global event stream (all waves)
  ├─ wave6_event_log.jsonl         # Wave 6 events (legacy)
  ├─ wave7_event_log.jsonl         # Wave 7 events (new)
  └─ lamport_state.json            # Current clock value
```

**lamport_state.json**:
```json
{
  "current_clock": 15420,
  "last_wave": "wave6",
  "last_epic": "EPIC-CCN-080",
  "last_phase": "phase1",
  "waves": {
    "wave6": {
      "start_clock": 0,
      "end_clock": 7890,
      "epics": 78,
      "methods": 79
    },
    "wave7": {
      "start_clock": 7891,
      "end_clock": null,
      "epics": null,
      "methods": 101
    }
  }
}
```

**Implementation**:
1. Wave 6 continues from current clock (Phase 1.5 onwards)
2. Wave 7 starts at `wave6.end_clock + 1`
3. All events written to both global and wave-specific logs
4. Manifest references global clock values

### Lamport Clock Initialization

**For Wave 6** (already started):
- Current clock: ~7890 (estimated from Phase 0-1 completion)
- Continue from current value
- Backfill global_event_log with Wave 6 events

**For Wave 7** (new):
- Start clock: Wave 6 end_clock + 1
- Pre-allocate clock ranges per phase
- Deterministic event ordering

## Pilot Strategy

### Wave 6 Pilot (Already Executed)
- **Scope**: 3 epics (EPIC-CCN-001, 002, 003)
- **Phases**: 0, 1, 1.5 (partial)
- **Status**: Phase 1.5 frozen (inline Bob CLI issue)
- **Lessons Learned**:
  - ✅ Phase 0-1 workflow validated
  - ❌ Phase 1.5 needs temp file pattern
  - ✅ Lamport clock verification works
  - ✅ Manifest-based state management works

### Wave 7 Pilot (Required)
- **Scope**: 3 epics from Wave 7 method list
- **Selection Criteria**:
  - 1 low complexity (CYC 9-10)
  - 1 medium complexity (CYC 11-15)
  - 1 high complexity (CYC 16-20)
  - Mix of files (not all from same file)
- **Phases**: 0 through 6 (complete workflow)
- **Success Criteria**:
  - All 3 epics reach Phase 6 completion
  - No protocol violations
  - Lamport clock deterministic
  - Build passes after each phase

## Documentation Requirements

### Wave 6 Documentation (Update)
- [x] `WAVE6_SCOPE_CRISIS_ANALYSIS.md` - Scope incomplete analysis
- [x] `PHASE1_5_FREEZE_ROOT_CAUSE_ANALYSIS.md` - Freeze diagnosis
- [ ] `WAVE6_METHOD_MANIFEST.md` - List of 79 methods
- [ ] `WAVE6_COMPLETION_REPORT.md` - Final status (after Phase 6)

### Wave 7 Documentation (Create)
- [ ] `WAVE7_SCOPE_DEFINITION.md` - 101 methods, no overlap with Wave 6
- [ ] `WAVE7_PILOT_PLAN.md` - 3-epic pilot strategy
- [ ] `WAVE7_PILOT_REPORT.md` - Pilot results
- [ ] `WAVE7_EXECUTION_PLAN.md` - Full 101-epic execution
- [ ] `WAVE7_COMPLETION_REPORT.md` - Final status

### Cross-Wave Documentation (Create)
- [ ] `GLOBAL_LAMPORT_CLOCK_DESIGN.md` - Clock architecture
- [ ] `WAVE6_WAVE7_METHOD_MAPPING.md` - Method distribution
- [ ] `180_METHOD_MASTER_MANIFEST.md` - Complete scope
- [ ] `JANE_STREET_COMPLIANCE_TRACKER.md` - Progress to CYC ≤8

## Execution Plan

### Phase 1: Wave 6 Completion (1-2 days)
1. **Fix Phase 1.5 Scripts** (temp file pattern)
2. **Complete Phase 1.5** (78 epics)
3. **Execute Phases 2-6** (sequential)
4. **Document Wave 6 Completion**

### Phase 2: Wave 7 Preparation (1 day)
1. **Extract Wave 6 Methods** (from Phase 0 hotspots)
2. **Extract All 180 Methods** (from baseline audit)
3. **Compute Wave 7 Methods** (set difference)
4. **Validate No Overlap** (assertions)
5. **Initialize Global Lamport Clock** (backfill Wave 6)
6. **Select Wave 7 Pilot Epics** (3 methods)
7. **Document Wave 7 Scope**

### Phase 3: Wave 7 Pilot (1 day)
1. **Generate Phase 0 for 3 Pilot Epics**
2. **Execute Phases 0-6** (complete workflow)
3. **Validate Lamport Clock** (deterministic)
4. **Document Pilot Results**
5. **Fix Any Issues** (before full execution)

### Phase 4: Wave 7 Full Execution (2-3 days)
1. **Generate Phase 0 for Remaining 98 Epics**
2. **Execute Phases 0-6** (all 101 epics)
3. **Monitor Progress** (4-minute polling)
4. **Handle Failures** (recovery loop)
5. **Document Wave 7 Completion**

### Phase 5: Final Validation (1 day)
1. **Verify All 180 Methods Refactored**
2. **Run Complexity Audit** (confirm CYC ≤8)
3. **Build Passes** (no compilation errors)
4. **Tests Pass** (no regressions)
5. **Document Jane Street Compliance**

**Total Timeline**: 6-8 days

## Risk Mitigation

### Risk 1: Wave 7 Method Extraction Errors
**Mitigation**: Automated validation script with assertions
**Fallback**: Manual review of method lists

### Risk 2: Lamport Clock Conflicts
**Mitigation**: Global clock with wave-specific ranges
**Fallback**: Reset clock if conflicts detected

### Risk 3: Wave 7 Pilot Failures
**Mitigation**: Fix issues before full execution
**Fallback**: Extend pilot to 5 epics if needed

### Risk 4: Circular Restarts (User Concern)
**Mitigation**: Complete documentation before execution
**Validation**: Pre-execution checklist (see below)

## Pre-Execution Checklist

### Before Wave 6 Completion
- [ ] Phase 1.5 scripts fixed (temp file pattern)
- [ ] VM restarted and accessible
- [ ] Build passes on VM
- [ ] Git status clean

### Before Wave 7 Preparation
- [ ] Wave 6 method list extracted (79 methods)
- [ ] Baseline audit parsed (180 methods)
- [ ] Wave 7 method list computed (101 methods)
- [ ] No overlap validated (assertions pass)
- [ ] Global Lamport clock initialized

### Before Wave 7 Pilot
- [ ] 3 pilot epics selected
- [ ] Phase 0 scripts generated
- [ ] Lamport clock ranges allocated
- [ ] Documentation complete

### Before Wave 7 Full Execution
- [ ] Pilot completed successfully (3/3 epics)
- [ ] All issues from pilot fixed
- [ ] Remaining 98 epics ready
- [ ] Monitoring scripts deployed

## Success Criteria

### Wave 6 Success
- ✅ All 78 epics reach Phase 6 completion
- ✅ Build passes after Wave 6
- ✅ 79 methods refactored to CYC ≤8
- ✅ Documentation complete

### Wave 7 Success
- ✅ All 101 epics reach Phase 6 completion
- ✅ Build passes after Wave 7
- ✅ 101 methods refactored to CYC ≤8
- ✅ Documentation complete

### Overall Success (Jane Street Compliance)
- ✅ All 180 methods refactored to CYC ≤8
- ✅ No methods with CYC > 8 remain
- ✅ Build passes
- ✅ Tests pass
- ✅ CodeScene complexity score ≤8 for all methods
- ✅ Codacy grade improved
- ✅ No circular restarts

## Next Steps (Immediate)

1. **Create Method Extraction Script** (`extract_wave6_methods.py`)
2. **Create Wave 7 Scope Script** (`compute_wave7_scope.py`)
3. **Create Global Lamport Clock** (`initialize_global_lamport.py`)
4. **Document Wave 6 Methods** (`WAVE6_METHOD_MANIFEST.md`)
5. **Document Wave 7 Scope** (`WAVE7_SCOPE_DEFINITION.md`)

**Awaiting user approval to proceed with method extraction.**