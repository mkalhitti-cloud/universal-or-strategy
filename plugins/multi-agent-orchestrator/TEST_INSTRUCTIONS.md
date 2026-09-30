# Multi-Agent Orchestrator POC Test Instructions

**Test Date**: 2026-06-09  
**Test Epic**: EPIC-CCN-21 (ExecuteFFMAManualMarketEntry, CYC 12)  
**Tester**: User  
**Observer**: Roo Cline

## Pre-Test Setup

### 1. Verify Command Installation

**Check command exists**:
```bash
# In main repo
ls .bob/commands/epic-orchestrate.md
```

**Expected**: File exists (329 lines)

### 2. Verify Test Epic Available

**Check epic roadmap**:
```bash
# In main repo
python -c "import json; data=json.load(open('epic_roadmap.json')); print([e for e in data if e['epic_number']=='EPIC-CCN-21'])"
```

**Expected**: EPIC-CCN-21 found with CYC 12

### 3. Check Bobcoin Budget

**Current budget**: ~390k Bobcoins remaining (after 3-epic batch)

**POC cost estimate**: 20-30k Bobcoins

**Safe to proceed**: Yes (leaves 360k for next batch)

## Test Procedure

### Step 1: Launch Bob Shell

**Terminal**:
```bash
cd c:/WSGTA/universal-or-strategy
bob --yolo
```

**Expected**:
- Bob Shell starts
- YOLO mode active (red "Auto-approve: Full" text)
- Mode: V12 Photon Engineer

### Step 2: Run Orchestrator Command

**In Bob Shell**:
```
/epic-orchestrate EPIC-CCN-21
```

**Expected behavior**:
- Orchestrator acknowledges command
- Begins Phase 0 (Hotspot Analysis)

### Step 3: Observe Phase 0 (Analysis Agent)

**Watch for**:
- ✅ Sub-agent spawned (mode: ask)
- ✅ Complexity assessment generated
- ✅ Ticket count estimated
- ❌ Error: Sub-agent not spawned

**If sub-agent spawns**:
- Note: "Sub-agent spawning WORKS"
- Continue to Phase 1

**If sub-agent doesn't spawn**:
- Note: "Sub-agent spawning NOT SUPPORTED"
- Document Bob Shell limitation
- STOP TEST (orchestrator not viable)

### Step 4: Observe Phase 1 (Planning Agent)

**Watch for**:
- ✅ Sub-agent spawned (mode: plan)
- ✅ File created: `docs/brain/EPIC-CCN-21/01-scope-boundary.md`
- ✅ Scope boundaries defined
- ❌ Error: File not created

**If file created**:
- Note: "Artifact passing WORKS"
- Continue to Phase 2

**If file not created**:
- Note: "Artifact passing BROKEN"
- Check file system permissions
- STOP TEST (fix required)

### Step 5: Observe Phase 2 (Architecture Agent)

**Watch for**:
- ✅ Sub-agent spawned (mode: plan)
- ✅ File created: `docs/brain/EPIC-CCN-21/02-architecture-plan.md`
- ✅ Helper methods designed (CYC ≤8)
- ❌ Error: Design incomplete

**Verify file contents**:
```bash
cat docs/brain/EPIC-CCN-21/02-architecture-plan.md
```

**Expected**: Architecture plan with helper method signatures

### Step 6: Observe Phase 3 (Review Agent)

**Watch for**:
- ✅ Sub-agent spawned (mode: advanced)
- ✅ DNA audit executed
- ✅ File created: `docs/brain/EPIC-CCN-21/03-audit-report.md`
- ❌ Error: Audit failed

**Check audit results**:
```bash
cat docs/brain/EPIC-CCN-21/03-audit-report.md
```

**Expected**: Zero P0-P3 violations

### Step 7: Observe Phase 4 (Ticket Agent)

**Watch for**:
- ✅ Sub-agent spawned (mode: plan)
- ✅ File created: `docs/brain/EPIC-CCN-21/04-tickets.md`
- ✅ TDD specs included
- ❌ Error: Tickets incomplete

**Verify ticket count**:
```bash
grep -c "^## Ticket" docs/brain/EPIC-CCN-21/04-tickets.md
```

**Expected**: 2-3 tickets (based on CYC 12)

### Step 8: Observe Phase 5 (Implementation Agent - Ticket 1)

**Watch for**:
- ✅ Sub-agent spawned (mode: v12-engineer)
- ✅ Tests written first (TDD)
- ✅ Helper method extracted
- ✅ Build passes
- ✅ deploy-sync.ps1 executed
- ✅ BUILD_TAG updated
- ✅ File created: `docs/brain/EPIC-CCN-21/ticket-1-completion.md`
- 🛑 **STOP: Orchestrator waits for F5**

**Orchestrator should display**:
```
[ORCHESTRATOR] EPIC-CCN-21 Ticket 1: {TICKET_NAME}
  Status: ✅ COMPLETE
  BUILD_TAG: 1111.XXX-epic-ccn-21-t1
  🛑 STOP: Press F5 to verify
  
  Waiting for user confirmation...
```

### Step 9: F5 Verification (Critical Test)

**Action**: Press F5 in NinjaTrader

**Watch for**:
- ✅ BUILD_TAG appears in output (1111.XXX-epic-ccn-21-t1)
- ✅ Zero compilation errors
- ✅ Strategy loads successfully

**In Bob Shell**: Type confirmation (e.g., "F5 passed")

**Expected**: Orchestrator proceeds to Ticket 2

**If orchestrator doesn't wait**:
- Note: "F5 gate BROKEN"
- Orchestrator needs stop logic fix
- Continue test but document issue

### Step 10: Observe Remaining Tickets

**Repeat Step 8-9** for each remaining ticket

**Watch for**:
- ✅ Each ticket completes successfully
- ✅ F5 gate stops after each ticket
- ✅ Orchestrator resumes after confirmation

### Step 11: Observe Phase 6 (Completion Agent)

**Watch for**:
- ✅ Sub-agent spawned (mode: advanced)
- ✅ All tickets verified
- ✅ CYC ≤8 achieved
- ✅ Manifest updated (status: "completed")
- ✅ Roadmap updated
- ✅ File created: `docs/brain/EPIC-CCN-21/05-completion-report.md`

**Orchestrator should display**:
```
[ORCHESTRATOR] EPIC-CCN-21 COMPLETE
  Duration: {TOTAL_TIME}
  Tickets: {COUNT}/{COUNT} completed
  CYC Reduction: 12 → {AFTER}
  Status: ✅ READY FOR PR SUBMISSION
```

## Test Results Documentation

### Success Criteria Checklist

**Sub-Agent Spawning**:
- [ ] Phase 0: Analysis Agent spawned
- [ ] Phase 1: Planning Agent spawned
- [ ] Phase 2: Architecture Agent spawned
- [ ] Phase 3: Review Agent spawned
- [ ] Phase 4: Ticket Agent spawned
- [ ] Phase 5: Implementation Agents spawned (one per ticket)
- [ ] Phase 6: Completion Agent spawned

**Artifact Passing**:
- [ ] 01-scope-boundary.md created
- [ ] 02-architecture-plan.md created
- [ ] 03-audit-report.md created
- [ ] 04-tickets.md created
- [ ] ticket-N-completion.md created (for each ticket)
- [ ] 05-completion-report.md created

**F5 Gates**:
- [ ] Orchestrator stops after Ticket 1
- [ ] Orchestrator waits for user confirmation
- [ ] Orchestrator resumes after confirmation
- [ ] Orchestrator stops after each subsequent ticket

**Epic Completion**:
- [ ] All tickets completed
- [ ] CYC ≤8 achieved
- [ ] Manifest updated
- [ ] Roadmap updated
- [ ] Zero compilation errors

### Performance Metrics

**Time**:
- Start time: ___________
- End time: ___________
- Total duration: ___________
- Comparison to manual: ___________

**Bobcoins**:
- Starting budget: ~390k
- Ending budget: ___________
- Bobcoins used: ___________
- Comparison to manual: ___________

**Context**:
- Context compressions: ___________
- Context resets needed: ___________

### Issues Encountered

**Issue 1**: ___________
- Severity: [ ] Critical [ ] High [ ] Medium [ ] Low
- Impact: ___________
- Workaround: ___________

**Issue 2**: ___________
- Severity: [ ] Critical [ ] High [ ] Medium [ ] Low
- Impact: ___________
- Workaround: ___________

## Post-Test Actions

### If POC Succeeds

1. **Document success** in TEST_RESULTS.md
2. **Deploy 3 parallel orchestrators** for full test
3. **Measure efficiency gains** vs manual baseline
4. **Update skill documentation** with findings

### If POC Fails

1. **Document failure** in TEST_RESULTS.md
2. **Identify root cause** (sub-agent support, artifact passing, F5 gates)
3. **Determine if fixable** or fundamental limitation
4. **Fall back to manual 3-session setup** if not fixable

### If POC Partially Succeeds

1. **Document partial success** in TEST_RESULTS.md
2. **Identify what works** and what doesn't
3. **Design workarounds** for broken features
4. **Test workarounds** before full deployment

## Next Steps

### After POC Test

**If successful**:
- Deploy 3 parallel orchestrators
- Test with EPIC-CCN-21, 23, 24
- Measure efficiency gains
- Update documentation

**If failed**:
- Document Bob Shell limitations
- Keep manual 3-session setup
- Explore alternative architectures
- Consider Watsonx Orchestrate

## Test Execution

**Ready to test**: Yes  
**Test epic**: EPIC-CCN-21  
**Estimated time**: 30-45 minutes  
**Bobcoin budget**: 20-30k (safe)

**To begin test**:
1. Open terminal in main repo
2. Run `bob --yolo`
3. Type `/epic-orchestrate EPIC-CCN-21`
4. Follow steps above
5. Document results

**Good luck!** 🚀