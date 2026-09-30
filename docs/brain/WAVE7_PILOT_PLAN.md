# Wave 7 Pilot Test Plan

**Date**: 2026-06-19  
**Status**: Ready for Execution  
**Mode**: autonomous-refactor

## Pilot Epics Selected

### 1. HIGH COMPLEXITY: EPIC-CCN-001
- **Method**: `ShouldSkipFleet_RunHealthCheck`
- **File**: `V12_002.SIMA.Fleet.cs`
- **CYC Before**: 31
- **CYC Target**: ≤8
- **Priority**: High
- **Rationale**: Most complex method in codebase - stress test for extraction logic

### 2. MEDIUM COMPLEXITY: EPIC-CCN-028
- **Method**: `ProcessQueuedAccountOrder`
- **File**: `V12_002.Orders.Callbacks.AccountOrders.cs`
- **CYC Before**: 15
- **CYC Target**: ≤8
- **Priority**: Medium
- **Rationale**: Representative of typical refactoring workload

### 3. LOW COMPLEXITY: EPIC-CCN-115
- **Method**: `OnBarUpdate`
- **File**: `V12_002.BarUpdate.cs`
- **CYC Before**: 10
- **CYC Target**: ≤8
- **Priority**: Low
- **Rationale**: Minimal extraction - validates baseline workflow

## Execution Plan

### Phase 0: Hotspot Analysis
**For each epic**:
1. Copy Phase 0 template from Wave 5 (`building-blocks/autonomous-refactoring/phase0_template_v12_52.sh`)
2. Update epic ID and method-specific data
3. Execute using Bob CLI temp file pattern
4. Verify output: `docs/brain/EPIC-CCN-XXX/00-hotspots.md`
5. Log Lamport event

### Phase 1: Scope Definition
**For each epic**:
1. Copy Phase 1 template from Wave 5
2. Update epic ID
3. Execute using Bob CLI temp file pattern
4. Verify output: `docs/brain/EPIC-CCN-XXX/00-scope.md`
5. Log Lamport event

### Phase 1.5: Scope Boundary Validation
**For each epic**:
1. Copy Phase 1.5 template from Wave 5 (FIXED version)
2. Update epic ID
3. **CRITICAL**: Use temp file pattern (NEVER inline strings)
4. Verify output: `docs/brain/EPIC-CCN-XXX/01-scope-boundary.md`
5. Log Lamport event

## Success Criteria

### Per Epic
- ✅ All 3 phases (0, 1, 1.5) complete successfully
- ✅ All output files created in correct locations
- ✅ Lamport events logged for each phase
- ✅ No protocol violations (Building-Blocks Method, temp file pattern)
- ✅ Cost tracking accurate

### Overall Pilot
- ✅ Total cost ≈ $21.96 (3 epics × $7.32/epic)
- ✅ No context exhaustion (stay under 200k tokens)
- ✅ All templates verified working
- ✅ Ready to scale to full 161 epics

## Cost Estimate

- **Per Epic**: $7.32 (based on Wave 6 data)
- **Pilot Total**: 3 × $7.32 = $21.96
- **Full Wave**: 161 × $7.32 = $1,178.52

## Building-Blocks Method Compliance

**MANDATORY**: All scripts MUST be copied from Wave 5 templates:
- ❌ NEVER generate scripts from scratch
- ✅ ALWAYS copy from `building-blocks/autonomous-refactoring/`
- ✅ ONLY modify epic ID and method-specific data
- ✅ VERIFY against SOP V3 before execution

## Bob CLI Pattern Compliance

**MANDATORY**: All Bob CLI invocations MUST use temp file pattern:

```bash
# Step 1: Create message file
cat > /tmp/phaseX_msg_$EPIC_ID.txt << 'EOFMSG'
[message content]
EOFMSG

# Step 2: Invoke Bob with command substitution
~/.npm-global/bin/bob --yolo --chat-mode MODE "$(cat /tmp/phaseX_msg_$EPIC_ID.txt)"
```

❌ NEVER: `bob --yolo --chat-mode MODE "inline message"` (causes freeze)

## Lamport Event Tracking

All phase transitions logged to `.lamport/wave7/event_log.jsonl`:

```json
{
  "timestamp": "2026-06-19T06:30:00Z",
  "lamport_clock": 1,
  "epic_id": "EPIC-CCN-001",
  "phase": "0",
  "event_type": "phase_start",
  "status": "pending"
}
```

## Next Steps

1. **Execute Phase 0** for all 3 pilot epics
2. **Verify outputs** and Lamport events
3. **Execute Phase 1** for all 3 pilot epics
4. **Execute Phase 1.5** for all 3 pilot epics
5. **Validate costs** match $21.96 estimate
6. **Document results** in `WAVE7_PILOT_RESULTS.md`
7. **Proceed to full wave** if pilot successful

## References

- Roadmap: `epic_roadmap_wave7.json`
- SOP V3: `docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md`
