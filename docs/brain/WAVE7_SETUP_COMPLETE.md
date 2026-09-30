# Wave 7 Fresh Start - Setup Complete

**Date**: 2026-06-19  
**Status**: Ready for Execution  
**Wave**: 7 (Complexity Reduction)

## Summary

Wave 7 is a **fresh start** for autonomous complexity reduction. All infrastructure is now in place for executing 161 complexity epics.

## Completed Setup Tasks

### ✅ Task 1: Mode Configuration
- **File**: `.bob/custom_modes.yaml`
- **Mode**: `autonomous-refactor`
- **Features**:
  - Wave orchestration and monitoring
  - Building-Blocks Method enforcement
  - Bob CLI temp file pattern (MANDATORY)
  - Lamport event tracking
  - Cost optimization (4-minute polling)
  - Jane Street KB integration
  - UTF-8 encoding requirement (MANDATORY)
  - xUnit test framework requirement (MANDATORY)

### ✅ Task 2: Instructions Document
- **File**: `docs/workflow/AUTONOMOUS_REFACTOR_MODE_INSTRUCTIONS.md`
- **Content**:
  - Complete mode documentation
  - Building-Blocks Method protocol
  - Bob CLI invocation pattern
  - Lamport event tracking
  - Cost optimization strategy
  - Jane Street KB integration
  - Pilot protocol
  - Recovery loop protocol
  - UTF-8 encoding requirements
  - xUnit test framework requirements
  - Monitoring commands
  - Success criteria

### ✅ Task 3: Wave 7 Roadmap
- **File**: `epic_roadmap_wave7.json`
- **Script**: `scripts/generate_wave7_roadmap.py`
- **Epics**: 161 (found from complexity audit)
- **Source**: `complexity_audit_fresh_2026-06-14.txt`
- **Priority Breakdown**:
  - High (CYC ≥16): 27 epics
  - Medium (CYC 11-15): 87 epics
  - Low (CYC 9-10): 47 epics

## Wave 7 Scope

### Goal
Reduce all 161 methods from CYC > 8 to CYC ≤ 8 (Jane Street strict standard)

### Target
CodeScene complexity score ≤8 for all methods

### Execution Model
- **VM**: All 161 epics (automated wave execution)
- **Local**: Fallback for .dll dependencies (if discovered)

### Critical Requirements
1. **UTF-8 Encoding**: ALL source files MUST be UTF-8 (no BOM)
2. **xUnit Tests**: ALWAYS generate xUnit tests - NEVER NUnit/MSTest

## Top 10 Most Complex Methods

| Epic | Method | CYC | File |
|------|--------|-----|------|
| EPIC-W7-001 | ShouldSkipFleet_RunHealthCheck | 31 | V12_002.SIMA.Fleet.cs |
| EPIC-W7-002 | HandleSecondaryOrderFilled | 21 | V12_002.Orders.Callbacks.cs |
| EPIC-W7-003 | ShadowPropagateStopMoves | 20 | V12_002.SIMA.Shadow.cs |
| EPIC-W7-004 | ShowModeSpecificControls | 20 | V12_002.UI.Panel.Handlers.cs |
| EPIC-W7-005 | FindChartTraderViaChartTab | 20 | V12_002.UI.Panel.Helpers.cs |
| EPIC-W7-006 | ProcessOnOrderUpdate | 19 | V12_002.Orders.Callbacks.cs |
| EPIC-W7-007 | ValidateOrphanedMasterOrders | 19 | V12_002.Orders.Management.Cleanup.cs |
| EPIC-W7-008 | ManageCIT | 19 | V12_002.Orders.Management.Flatten.cs |
| EPIC-W7-009 | TryHandleFleetCommand | 19 | V12_002.UI.IPC.Commands.Fleet.cs |
| EPIC-W7-010 | TryHandleFleet_CancelAll | 19 | V12_002.UI.IPC.Commands.Fleet.cs |

## Remaining Setup Tasks

### 🔄 Task 4: Copy Phase Templates (In Progress)
- Copy Phase 0-6 templates from `building-blocks/autonomous-refactoring/`
- Verify all templates use temp file pattern for Bob CLI
- Update templates for Wave 7 epic IDs

### ⏳ Task 5: Lamport Event System
- Create `.lamport/wave7/` directory structure
- Document event schema for all phases
- Create helper scripts for event creation/validation

### ⏳ Task 6: Master Launch Script
- Create master launch script with 4-minute polling
- Implement cost-optimized polling strategy
- Add progress monitoring and status reporting

### ⏳ Task 7: Phase 0 Pilot
- Select 3 pilot epics (low/medium/high complexity)
- Execute Phase 0 for pilot epics
- Validate success criteria
- Fix any issues before full wave

### ⏳ Task 8: Full Wave Execution
- Execute all 161 epics through Phase 0-6
- Monitor progress via Lamport events
- Apply recovery loop for failures
- Achieve 161/161 (100%) completion

## Wave Naming Convention

- **Wave 7**: Complexity reduction (CYC > 8 → CYC ≤ 8) - THIS WAVE
- **Wave 8**: Jane Street violations (separate wave, future)

## References

### Documentation
- Mode Instructions: `docs/workflow/AUTONOMOUS_REFACTOR_MODE_INSTRUCTIONS.md`
- SOP V3: `docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md`
- Cost Protocol: `docs/protocol/COST_OPTIMIZED_POLLING_PROTOCOL.md`
- Architecture: `building-blocks/autonomous-refactoring/ARCHITECTURE.md`

### Scripts
- Roadmap Generator: `scripts/generate_wave7_roadmap.py`
- Roadmap File: `epic_roadmap_wave7.json`

### Templates (Wave 5 - Building Blocks)
- Phase 0: `building-blocks/autonomous-refactoring/phase0_template_v12_52.sh`
- Phase 1: `building-blocks/autonomous-refactoring/phase1_template_v12_52.sh`
- Phase 1.5: `building-blocks/autonomous-refactoring/phase1_5_template_v12_52_FIXED.sh`
- Phase 2: `building-blocks/autonomous-refactoring/phase2_template_v12_52.sh`
- Phase 3: `building-blocks/autonomous-refactoring/phase3_template_v12_52.sh`
- Phase 4: `building-blocks/autonomous-refactoring/phase4_template_v12_52.sh`
- Phase 5: `building-blocks/autonomous-refactoring/phase5_template_v12_52.sh`
- Phase 5.V: `building-blocks/autonomous-refactoring/phase5_v_template_v12_52.sh`
- Phase 6: `building-blocks/autonomous-refactoring/phase6_template_v12_52.sh`

## Next Steps

1. **Immediate**: Copy and verify Phase 0-6 templates (Task 4)
2. **Next**: Set up Lamport event system (Task 5)
3. **Then**: Create master launch script (Task 6)
4. **Pilot**: Execute Phase 0 pilot with 3 epics (Task 7)
5. **Full Wave**: Execute all 161 epics (Task 8)

## Success Criteria

### Per Phase
- ✅ N/N epics complete (100%)
- ✅ All output files created
- ✅ Lamport events logged
- ✅ Build passes (for code-changing phases)
- ✅ No protocol violations
- ✅ UTF-8 encoding verified (Phase 5+)
- ✅ xUnit tests generated and passing (Phase 5+)

### Per Wave
- ✅ All 161 epics Phase 0-6 complete
- ✅ All methods CYC ≤8
- ✅ CodeScene complexity score ≤8
- ✅ Build passes
- ✅ Tests pass
- ✅ Documentation complete
- ✅ All source files UTF-8 encoded
- ✅ All tests use xUnit framework

### Jane Street Compliance
- ✅ 0 methods with CYC > 8
- ✅ No circular restarts
- ✅ Complete audit trail
- ✅ Codacy grade improved

## Notes

- **161 vs 180 Methods**: The complexity audit found 161 methods with CYC > 8 (marked as "REFACTOR"). This is the accurate count from the source data.
- **Fresh Start**: No pre-excluded epics. All 161 methods are in scope.
- **UTF-8 Encoding**: Critical requirement added based on previous wave issues.
- **xUnit Framework**: Critical requirement added based on previous wave issues.
- **Building-Blocks Method**: MANDATORY for all script generation (copy from Wave 5 templates).
- **Bob CLI Pattern**: MANDATORY temp file + command substitution (NEVER inline strings).