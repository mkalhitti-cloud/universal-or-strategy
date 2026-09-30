# Recovery Loop Protocol (V12.26 + V12.28)

**Version**: 1.1
**Effective**: 2026-06-15
**Status**: MANDATORY for all autonomous wave execution
**Scope**: All phases (Phase 0 through Phase 6)

---

## Core Principles

### 1. 100% Completion Mandate (V12.28 - ABSOLUTE)

**ALL EPICS IN SCOPE MUST REACH 100% COMPLETION**

- NEVER dismiss any epic as "not our concern" or "out of scope" without explicit Director approval
- If an epic exists in the roadmap or has a brain directory, it IS in scope and MUST be completed
- Naming mismatches (EPIC-CCN-27 vs EPIC-CCN-027) do NOT exempt an epic from completion
- Missing Phase 5 files do NOT exempt an epic from Phase 6 - execute Phase 5 first, then Phase 6
- The goal is ALWAYS N/N (100%), never N-1/N or "close enough"
- Every incomplete epic is a blocker to wave completion

**Example Violation** (Wave 4):
- EPIC-CCN-027 and 045 dismissed as "not our concern" due to naming mismatch
- Result: Wave reported 79/79 when actually 77/80 (96.25%)
- Root cause: Assumed naming mismatch meant "out of scope"
- Correct action: Investigate ALL epics, execute missing phases, achieve true 80/80

**Reference**: `WAVE4_EPIC_027_045_STATUS.md`

### 2. Recovery Loop Rule (V12.26)

**NEVER PROCEED TO NEXT PHASE WITH INCOMPLETE EPICS**

Every phase MUST achieve 100% completion before the wave advances. Failed epics MUST be recovered in a loop until they catch up with their cohort.

**Rationale**: Compound intelligence, not errors. Unresolved failures cascade into subsequent phases, creating friction and requiring expensive manual intervention.

---

## The Recovery Loop Rule

### Mandatory Loop Structure

```
FOR EACH PHASE (0, 1, 1.5, 2, 3, 4, 4.5, 5, 5.V, 6):
  1. Launch wave for all pending epics
  2. Monitor until completion
  3. IF success_rate < 100%:
       a. Identify failed epics
       b. Analyze root causes
       c. Generate recovery scripts
       d. Execute recovery loop
       e. GOTO step 2 (monitor)
  4. ELSE (success_rate == 100%):
       a. Verify all files exist
       b. Update epic roadmap
       c. Create completion report
       d. PROCEED to next phase
```

### Loop Termination Conditions

**Success**: 100% of epics complete for current phase
**Failure**: After 3 recovery attempts, escalate to manual intervention

---

## Implementation by Phase

### Phase 0: Hotspot Analysis

**Success Criteria**: 80/80 `00-hotspots.md` files created

**Recovery Loop**:
```bash
# 1. Identify failed epics
failed_epics=$(comm -23 <(seq -f "EPIC-CCN-%03g" 1 80 | sort) <(ls docs/brain/EPIC-CCN-*/00-hotspots.md | grep -oP 'EPIC-CCN-\d+' | sort))

# 2. Generate recovery script
python scripts/generate_phase0_recovery.py --epics "$failed_epics"

# 3. Upload to VM
gcloud compute scp scripts/wave4/launch_phase0_recovery.sh v12-test-golden-v2:~/universal-or-strategy/scripts/wave4/ --zone=us-central1-a

# 4. Execute recovery
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="cd universal-or-strategy && ./scripts/wave4/launch_phase0_recovery.sh"

# 5. Monitor (4-min intervals)
# ... wait for completion ...

# 6. Verify 100%
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="cd universal-or-strategy && ls docs/brain/EPIC-CCN-*/00-hotspots.md | wc -l"
# Expected: 80
```

### Phase 1: Scope Definition

**Success Criteria**: 80/80 `01-scope.md` files created

**Recovery Loop**: Same pattern as Phase 0, replace `phase0` with `phase1`

### Phase 1.5: Scope Boundary Validation

**Success Criteria**: 80/80 `01-scope-boundary.md` files created

**Recovery Loop**: Same pattern, replace with `phase1_5`

### Phase 2: Architecture Planning

**Success Criteria**: 80/80 `02-architecture-plan.md` files created

**Recovery Loop**: Same pattern, replace with `phase2`

**Special Case**: If epic missing Phase 1.5, must recover Phase 1.5 first

### Phase 3: DNA & PR Audit

**Success Criteria**: 80/80 `03-audit-report.md` files created

**Recovery Loop**: Same pattern, replace with `phase3`

**Special Case**: If epic missing Phase 2, must recover Phase 2 → Phase 3

### Phase 4: Ticket Generation

**Success Criteria**: 80/80 `04-tickets.md` files created

**Recovery Loop**: Same pattern, replace with `phase4`

**Special Case**: If epic missing Phase 2 or 3, must recover in order:
- Missing Phase 2 → Recover Phase 2 → Phase 3 → Phase 4
- Missing Phase 3 → Recover Phase 3 → Phase 4

### Phase 5: Ticket Execution

**Success Criteria**: All tickets executed for all 80 epics

**Recovery Loop**: Per-ticket recovery (more granular)

### Phase 5.V: Verification

**Success Criteria**: 80/80 verification reports

**Recovery Loop**: Same pattern, replace with `phase5v`

### Phase 6: Final Review

**Success Criteria**: 80/80 completion reports

**Recovery Loop**: Same pattern, replace with `phase6`

---

## Root Cause Analysis (Mandatory)

After EVERY recovery loop, document:

1. **What failed**: Epic IDs, phase, error messages
2. **Why it failed**: Root cause (missing files, MCP error, timeout, etc.)
3. **How to prevent**: Protocol update, script fix, validation check
4. **Lessons learned**: Update SOPs, skills, mode rules

**Output**: `WAVE{N}_PHASE{X}_RECOVERY_ANALYSIS.md`

---

## Building-Blocks Method for Recovery

**CRITICAL**: Recovery scripts MUST use building-blocks method

### Recovery Script Generation

```python
# scripts/generate_phase{X}_recovery.py

def generate_recovery_script(failed_epics, phase):
    """
    Generate recovery script using building-blocks method.
    
    Args:
        failed_epics: List of EPIC-CCN-XXX IDs
        phase: Phase number (0, 1, 1.5, 2, 3, 4, 5, 5.V, 6)
    
    Returns:
        Path to generated recovery script
    """
    # 1. Copy successful epic script from same phase
    template = f"scripts/wave4/_p{phase}_001.sh"
    
    # 2. For each failed epic:
    for epic_id in failed_epics:
        epic_num = extract_number(epic_id)  # e.g., "044"
        
        # 3. Copy template
        recovery_script = f"scripts/wave4/_p{phase}_{epic_num}_recovery.sh"
        shutil.copy(template, recovery_script)
        
        # 4. Find-and-replace epic ID only
        replace_in_file(recovery_script, "EPIC-CCN-001", epic_id)
        
        # 5. Verify script syntax
        verify_bash_syntax(recovery_script)
    
    # 6. Generate launcher
    create_recovery_launcher(failed_epics, phase)
```

**Key Rule**: NEVER generate recovery scripts from scratch. Always copy from working epic.

---

## Monitoring During Recovery

### Cost-Optimized Polling (V2.0)

- **Initial check**: 1 minute after first script launch
- **Subsequent checks**: Every 4 minutes
- **Stop condition**: All screen sessions complete + file count == target

### Recovery-Specific Checks

```bash
# Check recovery progress
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="
  cd universal-or-strategy && 
  echo '=== RECOVERY SESSIONS ===' && 
  screen -ls | grep -c 'p{X}-recovery' && 
  echo '=== RECOVERED FILES ===' && 
  ls docs/brain/EPIC-CCN-{044,065,074}/0{X}-*.md 2>/dev/null | wc -l
"
```

---

## Escalation Protocol

### After 3 Failed Recovery Attempts

1. **Stop automated recovery**
2. **Create escalation report**: `WAVE{N}_PHASE{X}_ESCALATION.md`
3. **Manual investigation**: Director reviews logs, root causes
4. **Decision**:
   - Fix infrastructure issue (MCP server, VM, API)
   - Update protocol (add validation, change approach)
   - Manual execution (Claude session, not VM)
5. **Document fix**: Update SOPs, skills, mode rules
6. **Resume wave**: After fix validated

---

## Integration Points

### 1. Autonomous-Refactor Mode

**File**: `.bob/custom_modes.yaml`

**Update**: Add recovery loop requirement to mode description

```yaml
autonomous-refactor:
  description: |
    MANDATORY RECOVERY LOOP PROTOCOL (V12.26):
    - NEVER proceed to next phase with <100% completion
    - Loop failed epics until they catch up with cohort
    - Use building-blocks method for recovery scripts
    - Document root causes after every recovery
    - Reference: docs/protocol/RECOVERY_LOOP_PROTOCOL.md
```

### 2. GCP VM Wave Execution Skill

**File**: `.bob/skills/gcp-vm-wave-execution/skill.md`

**Update**: Add recovery loop section

```markdown
## Recovery Loop Protocol (MANDATORY)

After every phase execution:
1. Check success rate
2. IF <100%: Execute recovery loop
3. Document root causes
4. Update roadmap
5. ONLY THEN proceed to next phase

Reference: docs/protocol/RECOVERY_LOOP_PROTOCOL.md
```

### 3. Wave Phase Script Generation SOP

**File**: `docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md`

**Update**: Add recovery script generation section

```markdown
## Recovery Script Generation (V12.26)

When epics fail during wave execution:
1. Identify failed epic IDs
2. Copy working script from SAME phase
3. Find-and-replace epic ID only
4. Generate recovery launcher
5. Upload to VM
6. Execute recovery loop
7. Monitor until 100%

NEVER generate recovery scripts from scratch.
```

### 4. Epic Roadmap Updates

**File**: `epic_roadmap_wave4_fresh.json`

**Update**: Add recovery tracking fields

```json
{
  "epic_id": "EPIC-CCN-044",
  "recovery_attempts": 1,
  "recovery_history": [
    {
      "phase": 4,
      "attempt": 1,
      "date": "2026-06-15",
      "root_cause": "Missing Phase 2/3 prerequisites",
      "resolution": "Executed Phase 2 → 3 → 4 sequentially"
    }
  ]
}
```

---

## Success Metrics

### Per Phase
- ✅ 100% completion rate (80/80 epics)
- ✅ Zero unresolved failures
- ✅ All files verified on disk
- ✅ Roadmap updated

### Per Wave
- ✅ All phases 100% complete
- ✅ Recovery loops documented
- ✅ Root causes analyzed
- ✅ Protocols updated

### Overall
- ✅ Compound intelligence (not errors)
- ✅ Smooth autonomous execution
- ✅ Minimal manual intervention
- ✅ Building-blocks validated

---

## Examples

### Example 1: Phase 4 Recovery (Wave 4)

**Scenario**: 77/80 epics complete, 3 failed (EPIC-CCN-044, 065, 074)

**Recovery Loop**:
```bash
# 1. Identify failed epics
failed_epics="EPIC-CCN-044 EPIC-CCN-065 EPIC-CCN-074"

# 2. Analyze root causes
# - EPIC-CCN-044: Missing Phase 2/3 (clear path)
# - EPIC-CCN-065: Critical error after MCP success (investigate)
# - EPIC-CCN-074: MCP connection error (server issue)

# 3. Generate recovery scripts
python scripts/generate_phase4_recovery.py --epics "$failed_epics"

# 4. Upload to VM
gcloud compute scp scripts/wave4/_p4_*_recovery.sh v12-test-golden-v2:~/universal-or-strategy/scripts/wave4/ --zone=us-central1-a
gcloud compute scp scripts/wave4/launch_phase4_recovery.sh v12-test-golden-v2:~/universal-or-strategy/scripts/wave4/ --zone=us-central1-a

# 5. Execute recovery
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="cd universal-or-strategy && ./scripts/wave4/launch_phase4_recovery.sh"

# 6. Monitor (4-min intervals)
# ... wait for completion ...

# 7. Verify 100%
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="cd universal-or-strategy && ls docs/brain/EPIC-CCN-*/04-tickets.md | wc -l"
# Expected: 80

# 8. Document root causes
# Create: WAVE4_PHASE4_RECOVERY_ANALYSIS.md

# 9. Update roadmap
# Mark all 80 epics as phase4_complete

# 10. Proceed to Phase 5
```

### Example 2: Phase 2 Recovery with Prerequisites

**Scenario**: EPIC-CCN-044 missing Phase 2, but also missing Phase 1.5

**Recovery Loop**:
```bash
# 1. Check prerequisites
if [ ! -f "docs/brain/EPIC-CCN-044/01-scope-boundary.md" ]; then
  echo "ERROR: Missing Phase 1.5 prerequisite"
  
  # 2. Recover Phase 1.5 first
  python scripts/generate_phase1_5_recovery.py --epics "EPIC-CCN-044"
  # ... execute Phase 1.5 recovery ...
  
  # 3. Verify Phase 1.5 complete
  # ... check file exists ...
fi

# 4. Now recover Phase 2
python scripts/generate_phase2_recovery.py --epics "EPIC-CCN-044"
# ... execute Phase 2 recovery ...

# 5. Verify Phase 2 complete
# ... check file exists ...
```

---

## Validation Checklist

Before proceeding to next phase, verify:

- [ ] Success rate == 100% (80/80 epics)
- [ ] All output files exist on VM
- [ ] All output files >1K in size
- [ ] No errors in logs
- [ ] Bobcoin usage within budget
- [ ] Epic roadmap updated
- [ ] Recovery analysis documented (if recovery occurred)
- [ ] Protocols updated (if gaps identified)

---

## Enforcement

**Violation**: Proceeding to next phase with <100% completion

**Penalty**: 
1. Immediate halt of wave execution
2. Manual rollback to last 100% phase
3. Root cause analysis required
4. Protocol update required
5. Re-execution from failed phase

**Responsibility**: Wave Execution Lead (autonomous-refactor mode)

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-06-15 | Initial protocol created after Wave 4 Phase 4 recovery |
| 1.1 | 2026-06-15 | Added 100% Completion Mandate (V12.28) after EPIC-027/045 incident |

---

**Protocol Status**: ✅ ACTIVE
**Enforcement**: MANDATORY
**Scope**: All autonomous wave execution
**Maintainer**: Wave Execution Lead
**Last Updated**: 2026-06-15T23:33:00Z