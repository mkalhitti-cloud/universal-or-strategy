# Wave 7 Session Handoff - 100% Context

**Date**: 2026-06-21  
**Session Cost**: $192.91  
**Status**: Ready for Task 7 (Master Launch Script)

---

## PASTE THIS IN NEW SESSION

```
# Wave 7 Autonomous Refactoring - Session Handoff

## Context Summary

Wave 7 preparation is complete. All 161 epics are ready for execution on VM. Phase 1.5 freeze issue has been resolved. Special cases analysis confirms no DLL dependencies or local-only requirements.

## Current Status

**Completed Tasks** (1-6):
1. ✅ /continue command implementation
2. ✅ Lamport clock infrastructure
3. ✅ Epic naming convention standardization
4. ✅ Building-Blocks Method documentation
5. ✅ Cost-optimized polling protocol
6. ✅ Wave 7 template fixes (Phase 1.5 freeze resolved)
7. ✅ Special cases analysis (all 161 epics VM-safe)
8. ✅ Comprehensive execution plan

**Next Task** (7): Create master launch script with 1-minute → 4-minute polling

## Critical Information

### Wave 7 Scope
- **Total Epics**: 161 (EPIC-W7-001 through EPIC-W7-161)
- **Goal**: Reduce all methods from CYC > 8 to CYC ≤ 8 (Jane Street strict standard)
- **Source**: complexity_audit_fresh_2026-06-14.txt
- **Execution**: ALL 161 epics VM-safe (no DLL dependencies)

### Phase 1.5 Freeze Issue (RESOLVED)
- **Problem**: Sessions freezing when starting/stopping Phase 1.5 scripts
- **Root Cause**: Missing `~/.npm-global/bin/` prefix on bob executable
- **Solution**: Fixed all 7 templates using Bob CLI
- **Verification**: scripts/verify_wave7_templates.py passes 7/7
- **Templates Fixed**: Phase 1, 1.5, 2, 4, 5, 5.V, 6

### Polling Strategy (Per User Requirements)
**Phase 1: Launch Verification (First 10 Epics)**
- Poll every **1 minute**
- Verify successful launch
- Check for errors in Lamport events
- Confirm no freeze conditions

**Phase 2: Full Wave Execution (Remaining 151 Epics)**
- Poll every **4 minutes** (cost-optimized, 88% reduction)
- Monitor progress via Lamport events
- Track bobcoin usage per API
- Apply recovery loop for failures

### Critical Requirements (P0 Blockers)
1. **UTF-8 Encoding**: ALL source files MUST be UTF-8 (no BOM, no ASCII violations)
2. **xUnit Test Framework**: ALWAYS xUnit ([Fact], Assert.Equal()), NEVER NUnit/MSTest
3. **Building-Blocks Method**: ALWAYS copy scripts from previous wave, NEVER generate from scratch
4. **Bob CLI Pattern**: Two-step (temp file + command substitution), full path `~/.npm-global/bin/bob`

### Special Cases (2 Epics)
- EPIC-W7-134: ProcessOnConnectionStatusUpdate (network operations)
- EPIC-W7-156: ProcessClientStream (stream operations)
- **Impact**: None - can execute on VM with standard monitoring

### Infrastructure
- **VM IP**: 34.60.155.195
- **VM User**: malhitticrypto
- **Branch**: gitbutler/workspace (GitButler virtual branches)
- **Build Status**: Passing
- **Git Status**: Clean

### Lamport Clock
- **Event Log**: .lamport/wave7/event_log.jsonl
- **Event Types**: wave_start, phase_start, phase_complete, phase_fail, wave_complete
- **Phases**: 0, 1, 1.5, 2, 3, 4, 4.5, 5.X, 5.X.V, 6
- **Helper Scripts**: filter_wave7_events.py, generate_wave7_stats.py, verify_wave7_determinism.py

### Templates Location
- **Directory**: building-blocks/wave7/
- **Templates**: 9 total (7 using Bob CLI)
- **Status**: All verified passing
- **Verification**: scripts/verify_wave7_templates.py

## Key Documents (Read These First)

**MANDATORY READING**:
1. `docs/workflow/WAVE7_EXECUTION_PLAN.md` (600 lines)
   - Comprehensive execution guide
   - Polling strategy details
   - Critical requirements
   - Recovery protocol
   - Pre-execution checklist

2. `docs/workflow/WAVE7_SPECIAL_CASES_ANALYSIS.md`
   - All 161 epics analyzed
   - VM-safe confirmation
   - External dependencies identified

3. `docs/workflow/WAVE7_PREPARATION_COMPLETE.md` (400 lines)
   - Complete task summary
   - Deliverables list
   - Verification checklist

4. `docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md` (1130 lines)
   - Building-Blocks Method (MANDATORY)
   - Bob CLI invocation pattern (Section 589-678)
   - Validation checklist

5. `docs/protocol/COST_OPTIMIZED_POLLING_PROTOCOL.md`
   - 4-minute polling intervals
   - 88% cost reduction strategy
   - Master launch script pattern

6. `.lamport/wave7/README.md` (424 lines)
   - Event schema documentation
   - Integration examples
   - Python API and Bash patterns

## Bob CLI Pattern (CRITICAL)

**Correct Pattern** (Two-Step):
```bash
# Step 1: Create message file
cat > /tmp/phaseX_msg_$EPIC_ID.txt << 'EOFMSG'
[message content]
EOFMSG

# Step 2: Invoke Bob with command substitution
~/.npm-global/bin/bob --yolo --chat-mode MODE "$(cat /tmp/phaseX_msg_$EPIC_ID.txt)"
```

**BANNED Pattern** (Causes Freeze):
```bash
# ❌ NEVER use inline strings
bob --yolo --chat-mode MODE "inline message here"
```

**Critical Notes**:
- Missing `~/.npm-global/bin/` prefix causes indefinite hang
- Inline strings cause session freeze
- Temp file + command substitution is MANDATORY

## Task 7 Requirements

**Deliverable**: Create `scripts/launch_wave7.sh`

**Features Required**:
1. Launch all 161 epics sequentially
2. Implement 1-minute polling for first 10 epics
3. Implement 4-minute polling for remaining 151 epics
4. Monitor progress via Lamport events
5. Status reporting (X/161 complete)
6. Recovery loop for failures
7. Cost tracking per API (Anthropic, OpenAI, Google, Groq)
8. Error detection and logging
9. Graceful shutdown on Ctrl+C

**Reference Implementation**: See `docs/protocol/COST_OPTIMIZED_POLLING_PROTOCOL.md`

**Success Criteria**:
- Script launches all 161 epics
- Polling intervals correct (1-min → 4-min transition after epic 10)
- Lamport events logged for all phases
- Recovery loop functional
- Cost tracking enabled
- Progress reporting clear

## Recovery Loop Protocol

**Failure Detection**:
- Phase fails (status = `failed` in Lamport event)
- Build fails after sync
- UTF-8 encoding violation
- xUnit test framework violation
- Timeout (phase exceeds 30 minutes)

**Recovery Steps**:
1. STOP wave immediately
2. Analyze failure (review Lamport events)
3. Document in `docs/brain/EPIC-W7-XXX/failure-analysis.md`
4. Fix issue (separate session)
5. Update manifest (set phase status to `pending`)
6. Re-run failed phase only
7. Verify success
8. Resume wave

## Verification Commands

**Check Templates**:
```bash
python scripts/verify_wave7_templates.py
```

**Check Special Cases**:
```bash
python scripts/analyze_wave7_special_cases.py
```

**Generate Stats**:
```bash
python scripts/generate_wave7_stats.py
```

**Filter Events**:
```bash
python scripts/filter_wave7_events.py --epic EPIC-W7-042
```

## File Locations

**Templates**: `building-blocks/wave7/`
- phase0_template_wave7.sh
- phase1_template_wave7.sh
- phase1_5_template_wave7.sh
- phase2_template_wave7.sh
- phase3_template_wave7.sh
- phase4_template_wave7.sh
- phase4_5_template_wave7.sh
- phase5_template_wave7.sh
- phase5_v_template_wave7.sh
- phase6_template_wave7.sh

**Scripts**: `scripts/`
- analyze_wave7_special_cases.py
- verify_wave7_templates.py
- filter_wave7_events.py
- generate_wave7_stats.py
- verify_wave7_determinism.py
- test_wave7_lamport.py

**Documentation**: `docs/workflow/`
- WAVE7_EXECUTION_PLAN.md
- WAVE7_SPECIAL_CASES_ANALYSIS.md
- WAVE7_PREPARATION_COMPLETE.md
- WAVE7_TEMPLATE_STATUS_REPORT.md
- WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md

**Lamport**: `.lamport/wave7/`
- README.md
- event_log.jsonl (will be created during execution)

## Jane Street KB Integration

**Query Before**:
- Phase 2 (Architecture Planning): Query for extraction patterns
- Phase 5 (Ticket Execution): Query for implementation patterns
- Phase 5.V (Verification): Query for testing patterns

**Command**:
```bash
python scripts/query_kb.py "complexity reduction"
python scripts/query_kb.py "FSM extraction"
python scripts/query_kb.py "lock-free patterns"
```

**KB Coverage**: 100+ rules with P0/P1/P2 severity, HFT patterns, FSM/Actor patterns, testing strategies

## Pre-Execution Checklist

**Infrastructure**:
- [ ] VM accessible (ssh malhitticrypto@34.60.155.195)
- [ ] Build passes on VM (dotnet build)
- [ ] Git status clean (no uncommitted src/ changes)
- [ ] Branch strategy: GitButler virtual branches active
- [ ] jCodemunch index current
- [ ] Lamport clock infrastructure ready

**Templates**:
- [ ] All 9 templates in building-blocks/wave7/
- [ ] Bob CLI path fixed (~/.npm-global/bin/bob)
- [ ] Temp file pattern verified
- [ ] Verification script passes (scripts/verify_wave7_templates.py)

**Documentation**:
- [ ] SOP V3 reviewed
- [ ] Cost protocol reviewed
- [ ] Jane Street KB accessible
- [ ] Special cases analysis reviewed

## Success Metrics

**Wave Completion**:
- 161/161 epics complete (100%)
- All phases pass for all epics
- Build passes on VM and local
- F5 in NinjaTrader succeeds
- All methods CYC ≤8
- Zero UTF-8 violations
- Zero xUnit framework violations
- Lamport events logged for all phases
- Cost within budget

## Important Notes

1. **Building-Blocks Method is MANDATORY**: Always copy scripts from previous wave, never generate from scratch
2. **Bob CLI Pattern is CRITICAL**: Missing full path or using inline strings causes freeze
3. **Polling Strategy is USER-SPECIFIED**: 1-minute for first 10, then 4-minute for remaining 151
4. **All 161 Epics are VM-Safe**: No DLL dependencies, no local-only requirements
5. **UTF-8 and xUnit are P0 Blockers**: Violations stop execution immediately
6. **Recovery Loop is Mandatory**: Failed epics MUST be fixed before wave completion

## Next Steps

1. Read WAVE7_EXECUTION_PLAN.md (600 lines)
2. Read COST_OPTIMIZED_POLLING_PROTOCOL.md
3. Review building-blocks/wave7/ templates
4. Create scripts/launch_wave7.sh with:
   - 1-minute polling (first 10 epics)
   - 4-minute polling (remaining 151 epics)
   - Lamport event monitoring
   - Cost tracking
   - Recovery loop
   - Progress reporting
5. Test with pilot epics (3 epics: low/medium/high complexity)
6. Launch full wave (161 epics)

## Questions to Ask User

Before starting Task 7, confirm:
1. Should pilot test run first (3 epics) or go straight to full wave?
2. Any specific epics to prioritize or avoid?
3. Should script auto-recover failures or pause for manual intervention?
4. What's the bobcoin budget for this wave?

---

**END OF HANDOFF CONTEXT**

Ready to proceed with Task 7: Master Launch Script
```

---

## Instructions for User

1. Copy the entire block above (between the triple backticks)
2. Open a new Bob IDE session
3. Paste the context
4. Give the agent your task (before Task 7)
5. After that task completes, proceed to Task 7

The agent will have 100% context and can proceed immediately.