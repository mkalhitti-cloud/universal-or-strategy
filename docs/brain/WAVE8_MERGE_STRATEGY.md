# Wave 8 Merge Strategy: Unified 180-Method Execution

**Date**: 2026-06-19
**Status**: PLANNING - Optimized Approach
**Goal**: Complete all 180 methods efficiently using building-blocks method

## User Insight: Merge Strategy

**Key Observation**: If Wave 7 uses Wave 6 as building blocks, it should be FAST.

**Optimized Approach**:
1. **Wave 7 Catch-Up**: Execute Phase 0-1 for 101 new methods
2. **Wave 8 Merge**: Combine Wave 6 (79) + Wave 7 (101) = 180 methods
3. **Unified Execution**: Run Phase 1.5-6 for ALL 180 methods together

## Why This is Better

### Building-Blocks Efficiency
- ✅ Wave 7 Phase 0-1 scripts copied from Wave 6 (proven templates)
- ✅ No need to wait for Wave 6 completion
- ✅ Parallel preparation (Wave 7 catch-up while Wave 6 frozen)
- ✅ Single unified execution from Phase 1.5 onwards

### Timeline Improvement
**Old Plan** (Sequential):
- Wave 6 complete: 1-2 days
- Wave 7 prep: 1 day
- Wave 7 pilot: 1 day
- Wave 7 full: 2-3 days
- **Total**: 5-7 days

**New Plan** (Merge):
- Wave 7 catch-up (Phase 0-1): 1 day (parallel with Wave 6 freeze fix)
- Wave 8 merge prep: 0.5 days
- Wave 8 pilot (Phase 1.5-6): 1 day
- Wave 8 full (Phase 1.5-6): 2-3 days
- **Total**: 4.5-5.5 days (20% faster)

### Risk Reduction
- ✅ Single execution path (not two separate waves)
- ✅ Unified Lamport clock (no cross-wave coordination)
- ✅ One set of scripts (Phase 1.5-6)
- ✅ Simpler monitoring (one wave status)

## Wave Structure

### Wave 6 (Current State)
- **Epics**: EPIC-CCN-001 through 080 (excluding 024, 027)
- **Methods**: 79
- **Status**: Phase 0-1 complete, Phase 1.5 frozen

### Wave 7 (Catch-Up)
- **Epics**: EPIC-CCN-081 through 181 (101 new epics)
- **Methods**: 101
- **Phases**: 0-1 only (catch up to Wave 6)
- **Timeline**: 1 day

### Wave 8 (Merged)
- **Epics**: EPIC-CCN-001 through 181 (all 180 methods)
- **Methods**: 180 (79 from Wave 6 + 101 from Wave 7)
- **Phases**: 1.5-6 (unified execution)
- **Timeline**: 3-4 days

## Execution Plan

### Phase 1: Wave 7 Catch-Up (1 day)

**Step 1.1: Extract Wave 7 Methods**
```python
# Parse Wave 6 Phase 0 hotspots (79 methods)
wave6_methods = extract_wave6_methods()

# Parse baseline audit (180 methods)
all_methods = parse_complexity_audit()

# Compute Wave 7 methods (set difference)
wave7_methods = all_methods - wave6_methods
assert len(wave7_methods) == 101
```

**Step 1.2: Generate Wave 7 Phase 0 Scripts**
```bash
# Copy Wave 6 Phase 0 template
for epic in 081..181:
    cp building-blocks/autonomous-refactoring/phase0_template_v12_52.sh \
       scripts/wave7/phase0_epic_${epic}.sh
    # Customize with Wave 7 method data
```

**Step 1.3: Execute Wave 7 Phase 0**
```bash
# Run on VM (parallel with Wave 6 freeze fix)
for epic in 081..181:
    ./scripts/wave7/phase0_epic_${epic}.sh
done
```

**Step 1.4: Generate Wave 7 Phase 1 Scripts**
```bash
# Copy Wave 6 Phase 1 template
for epic in 081..181:
    cp building-blocks/autonomous-refactoring/phase1_template_v12_52.sh \
       scripts/wave7/phase1_epic_${epic}.sh
done
```

**Step 1.5: Execute Wave 7 Phase 1**
```bash
# Run on VM
for epic in 081..181:
    ./scripts/wave7/phase1_epic_${epic}.sh
done
```

**Success Criteria**:
- ✅ 101 Phase 0 hotspot files created
- ✅ 101 Phase 1 scope files created
- ✅ Lamport events logged
- ✅ Manifests updated

### Phase 2: Wave 8 Merge Preparation (0.5 days)

**Step 2.1: Validate Wave 6 + Wave 7 Completion**
```python
# Check Phase 0-1 complete for all 180 epics
wave6_complete = check_phase_completion("wave6", phases=[0, 1])
wave7_complete = check_phase_completion("wave7", phases=[0, 1])
assert wave6_complete == 79
assert wave7_complete == 101
```

**Step 2.2: Create Wave 8 Unified Manifest**
```json
{
  "wave_id": "wave8",
  "description": "Merged Wave 6 + Wave 7 for unified Phase 1.5-6 execution",
  "total_epics": 180,
  "total_methods": 180,
  "source_waves": ["wave6", "wave7"],
  "epics": {
    "EPIC-CCN-001": {
      "source_wave": "wave6",
      "method": "GetSubscriberCounts",
      "file": "SignalBroadcaster.cs",
      "cyc": 9,
      "phases": {
        "0": "complete",
        "1": "complete",
        "1.5": "pending"
      }
    },
    "EPIC-CCN-081": {
      "source_wave": "wave7",
      "method": "MethodName",
      "file": "File.cs",
      "cyc": 12,
      "phases": {
        "0": "complete",
        "1": "complete",
        "1.5": "pending"
      }
    }
  }
}
```

**Step 2.3: Generate Wave 8 Phase 1.5-6 Scripts**
```bash
# Copy templates for ALL 180 epics
for phase in 1.5 2 3 4 5 5.V 6:
    for epic in 001..181:
        cp building-blocks/autonomous-refactoring/phase${phase}_template_v12_52.sh \
           scripts/wave8/phase${phase}_epic_${epic}.sh
    done
done
```

**Step 2.4: Fix Phase 1.5 Scripts (Temp File Pattern)**
```bash
# Apply SOP-compliant pattern to all 180 Phase 1.5 scripts
for epic in 001..181:
    sed -i 's/bob --yolo --chat-mode MODE "message"/cat > \/tmp\/phase1_5_msg_$EPIC_ID.txt << '\''EOFMSG'\''\nmessage\nEOFMSG\nbob --yolo --chat-mode MODE "$(cat \/tmp\/phase1_5_msg_$EPIC_ID.txt)"/' \
        scripts/wave8/phase1.5_epic_${epic}.sh
done
```

**Success Criteria**:
- ✅ Wave 8 manifest created (180 epics)
- ✅ All Phase 1.5-6 scripts generated
- ✅ Phase 1.5 scripts use temp file pattern
- ✅ No overlap between Wave 6 and Wave 7 methods

### Phase 3: Wave 8 Pilot (1 day)

**Step 3.1: Select Pilot Epics**
- 1 from Wave 6 (low complexity, e.g., EPIC-CCN-001)
- 1 from Wave 7 (medium complexity)
- 1 from Wave 7 (high complexity)

**Step 3.2: Execute Pilot Phase 1.5-6**
```bash
# Run 3 pilot epics through all phases
for epic in 001 081 120:
    for phase in 1.5 2 3 4 5 5.V 6:
        ./scripts/wave8/phase${phase}_epic_${epic}.sh
    done
done
```

**Step 3.3: Validate Pilot Results**
- ✅ All 3 epics reach Phase 6 completion
- ✅ Build passes after each phase
- ✅ Lamport clock deterministic
- ✅ No protocol violations

**Success Criteria**:
- ✅ 3/3 pilot epics complete
- ✅ All issues identified and fixed
- ✅ Ready for full Wave 8 execution

### Phase 4: Wave 8 Full Execution (2-3 days)

**Step 4.1: Execute Phase 1.5 (All 180 Epics)**
```bash
# Master launch script with 4-minute polling
./scripts/wave8/launch_phase1_5_all.sh
```

**Step 4.2: Execute Phases 2-6 (Sequential)**
```bash
for phase in 2 3 4 5 5.V 6:
    ./scripts/wave8/launch_phase${phase}_all.sh
done
```

**Step 4.3: Monitor Progress**
- 4-minute polling intervals (cost-optimized)
- Recovery loop for failures
- Status dashboard

**Success Criteria**:
- ✅ All 180 epics reach Phase 6 completion
- ✅ Build passes
- ✅ Tests pass
- ✅ All methods CYC ≤8

### Phase 5: Final Validation (1 day)

**Step 5.1: Complexity Audit**
```bash
python scripts/complexity_audit.py
# Verify: 0 methods with CYC > 8
```

**Step 5.2: Build Validation**
```bash
dotnet build
# Verify: 0 errors
```

**Step 5.3: Test Validation**
```bash
dotnet test
# Verify: 100% pass
```

**Step 5.4: Jane Street Compliance**
```bash
# CodeScene complexity score ≤8 for all methods
# Codacy grade improved
# Documentation complete
```

**Success Criteria**:
- ✅ Jane Street CYC ≤8 compliance achieved
- ✅ No circular restarts
- ✅ Complete documentation

## Lamport Clock Strategy (Unified)

### Wave 8 Clock Architecture
```
.lamport/
  ├─ global_event_log.jsonl        # All events (Wave 6 + 7 + 8)
  ├─ wave8_event_log.jsonl         # Wave 8 events only
  └─ lamport_state.json            # Current clock
```

### Clock Ranges
- **Wave 6 Phase 0-1**: 0-7890 (already logged)
- **Wave 7 Phase 0-1**: 7891-15780 (new)
- **Wave 8 Phase 1.5-6**: 15781+ (unified)

### Benefits
- ✅ Single clock for Phase 1.5-6 (no cross-wave coordination)
- ✅ Deterministic ordering
- ✅ Simpler than separate Wave 6/7 clocks

## Special Cases (Carry Forward)

### From Wave 6
- **EPIC-003**: Local execution (due to .dll)
- **EPIC-024**: Excluded (missing Phase 0 script)
- **EPIC-027**: Excluded (user confirmed)

### Wave 7 Considerations
- Check for .dll dependencies (execute locally if needed)
- Pre-validate all Phase 0 scripts exist
- Document any exclusions

### Wave 8 Total
- **In-Scope**: 177 epics (79 from Wave 6 + 98 from Wave 7, excluding 3 special cases)
- **Out-of-Scope**: 3 epics (EPIC-003 local, EPIC-024 missing, EPIC-027 excluded)

## Timeline Comparison

### Old Plan (Sequential Waves)
```
Day 1-2:   Wave 6 Phase 1.5-6 (79 epics)
Day 3:     Wave 7 prep
Day 4:     Wave 7 pilot
Day 5-7:   Wave 7 Phase 0-6 (101 epics)
Day 8:     Final validation
Total: 8 days
```

### New Plan (Merge Strategy)
```
Day 1:     Wave 7 Phase 0-1 (101 epics) || Fix Wave 6 Phase 1.5 scripts
Day 1.5:   Wave 8 merge prep
Day 2:     Wave 8 pilot (3 epics, Phase 1.5-6)
Day 3-5:   Wave 8 full (180 epics, Phase 1.5-6)
Day 6:     Final validation
Total: 6 days (25% faster)
```

## Risk Mitigation

### Risk 1: Wave 7 Phase 0-1 Failures
**Mitigation**: Use proven Wave 6 templates
**Fallback**: Fix issues before Wave 8 merge

### Risk 2: Wave 8 Merge Complexity
**Mitigation**: Unified manifest with clear source tracking
**Fallback**: Separate execution if merge fails

### Risk 3: Phase 1.5 Freeze (Again)
**Mitigation**: Temp file pattern applied to all 180 scripts
**Validation**: Pre-execution test on 3 pilot epics

### Risk 4: Lamport Clock Conflicts
**Mitigation**: Clear clock ranges per wave/phase
**Fallback**: Reset clock if conflicts detected

## Pre-Execution Checklist

### Before Wave 7 Catch-Up
- [ ] Wave 6 methods extracted (79 methods)
- [ ] Baseline audit parsed (180 methods)
- [ ] Wave 7 methods computed (101 methods)
- [ ] No overlap validated
- [ ] Phase 0-1 templates ready

### Before Wave 8 Merge
- [ ] Wave 6 Phase 0-1 complete (79 epics)
- [ ] Wave 7 Phase 0-1 complete (101 epics)
- [ ] Wave 8 manifest created
- [ ] All Phase 1.5-6 scripts generated
- [ ] Phase 1.5 scripts use temp file pattern

### Before Wave 8 Pilot
- [ ] 3 pilot epics selected
- [ ] Lamport clock ranges allocated
- [ ] VM accessible and build passes

### Before Wave 8 Full Execution
- [ ] Pilot completed successfully (3/3 epics)
- [ ] All issues fixed
- [ ] Monitoring scripts deployed

## Success Criteria

### Wave 7 Catch-Up Success
- ✅ 101 epics Phase 0-1 complete
- ✅ Lamport events logged
- ✅ Manifests updated
- ✅ Ready for Wave 8 merge

### Wave 8 Merge Success
- ✅ 180 epics unified manifest
- ✅ All Phase 1.5-6 scripts generated
- ✅ No overlap between Wave 6/7 methods
- ✅ Pilot successful

### Wave 8 Execution Success
- ✅ All 180 epics Phase 1.5-6 complete
- ✅ Build passes
- ✅ Tests pass
- ✅ All methods CYC ≤8

### Jane Street Compliance Success
- ✅ 0 methods with CYC > 8
- ✅ CodeScene score ≤8 for all methods
- ✅ Codacy grade improved
- ✅ No circular restarts
- ✅ Complete documentation

## Next Steps (Immediate)

1. **Create Wave 7 Method Extraction Script** (`extract_wave7_methods.py`)
2. **Generate Wave 7 Phase 0 Scripts** (101 epics)
3. **Execute Wave 7 Phase 0** (parallel with Wave 6 freeze fix)
4. **Generate Wave 7 Phase 1 Scripts** (101 epics)
5. **Execute Wave 7 Phase 1**
6. **Create Wave 8 Unified Manifest**
7. **Generate Wave 8 Phase 1.5-6 Scripts** (180 epics)
8. **Execute Wave 8 Pilot** (3 epics)
9. **Execute Wave 8 Full** (180 epics)

**Awaiting user approval to proceed with Wave 7 catch-up.**