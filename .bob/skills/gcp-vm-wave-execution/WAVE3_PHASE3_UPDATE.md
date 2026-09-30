# GCP VM Wave Execution Skill - Wave 3 Phase 3 Update

**Date**: 2026-06-13
**Update Type**: Critical Bug Fix
**Status**: MANDATORY

---

## Critical Discovery

### Architecture Bug in Phase 3

**Issue**: Wave 3 Phase 3 generator copied Wave 3 Phase 2 pattern (Bob Shell) instead of Wave 2 Phase 3 pattern (Claude advanced mode).

**Impact**: 
- Wrong execution mode → Wrong output format
- 2 failed attempts
- 34 minutes debugging
- ~5.2 bobcoins wasted

**Root Cause**: Building-Blocks Methodology Violation

---

## The Golden Rule (NEW)

### ALWAYS Copy Same Phase from Previous Wave

```
✅ CORRECT:
Wave 3 Phase 3 → Copy Wave 2 Phase 3
Wave 3 Phase 4 → Copy Wave 2 Phase 4
Wave 3 Phase 5 → Copy Wave 2 Phase 5

❌ WRONG:
Wave 3 Phase 3 → Copy Wave 3 Phase 2
Wave 3 Phase 4 → Copy Wave 3 Phase 3
Wave 3 Phase 5 → Copy Wave 3 Phase 4
```

**Rationale**: Each phase has unique requirements (mode, command pattern, output format, validation). Adjacent phases are NOT interchangeable.

---

## Updated Skill Instructions

### Phase Script Generation (MANDATORY)

**Step 1: Copy Previous Wave's Same Phase**
```bash
cp scripts/wave{N-1}/generate_phase{X}_scripts.py scripts/wave{N}/generate_wave{N}_phase{X}_scripts.py
```

**Step 2: Update Epic Numbers ONLY**
- Change epic numbers (107-115 → 116-125)
- Update API allocation
- DO NOT change mode, command pattern, or output format

**Step 3: Verify Against SOP**
- Check mode matches SOP
- Check command pattern matches SOP
- Check output format matches SOP

**Step 4: Test with 2 Epics**
- Generate scripts
- Deploy 2 scripts only
- Verify output format
- Deploy all only after success

---

## Phase 3 Specific Requirements

### Correct Pattern (from Wave 2 Phase 3)

**Mode**: `advanced` (Claude with MCP tools)
**Command**: `bob --yolo --chat-mode advanced "$(cat /tmp/phase3_msg_X.txt)"`
**Output**: `03-audit-report.md`
**Validation**: DNA compliance, PR hygiene, risk assessment

### Wrong Pattern (Wave 3 Phase 2)

**Mode**: Bob Shell (status summary)
**Command**: `bob --yolo /epic-scan EPIC-CCN-X`
**Output**: Scan reports (checklist)
**Validation**: None

### Why Phase 3 Needs Claude Advanced Mode

**Phase 3 is a GATE**: Must validate before proceeding to Phase 4.

**Required Checks**:
1. DNA compliance (lock-free, ASCII-only, Jane Street)
2. PR hygiene (diff size, whitespace, scope creep)
3. Risk assessment
4. Go/No-Go recommendation

**Bob Shell cannot perform these checks** - it only reports status.

---

## Prevention Measures

### Before Generating Any Phase Script

1. **Check Previous Wave**:
   ```bash
   cat scripts/wave{N-1}/generate_phase{X}_scripts.py | grep "bob --"
   ```

2. **Verify Mode**:
   ```bash
   grep "chat-mode" scripts/wave{N}/generate_wave{N}_phase{X}_scripts.py
   ```

3. **Test First**:
   - Generate 2 test scripts
   - Run on VM
   - Verify output format
   - Deploy all only after success

---

## Recovery Procedure

If wrong output format detected:

1. **STOP immediately** - Do not deploy remaining scripts
2. **Identify root cause** - Check which phase was copied
3. **Create corrected generator** - Copy correct phase from previous wave
4. **Test with 2 epics** - Verify output format
5. **Deploy all** - Only after success
6. **Document failure** - Update lessons learned

---

## Cost Impact

### Wave 3 Phase 3 Total

| Attempt | Status | Bobcoins | Reason |
|---------|--------|----------|--------|
| First | Failed | 0 | HTTP 401 (dummy keys) |
| Second | Wrong | ~5.2 | Wrong output format |
| Third | Success | ~10.93 | Correct architecture |
| **Total** | **Complete** | **~16.13** | **All 10 epics** |

**Lesson**: 5 minutes testing saves 30 minutes debugging and ~5 bobcoins.

---

## Updated Skill Post-Use Audit

After every use of this skill:
1. ✅ Check if any instruction was ambiguous
2. ✅ Update skill if gaps found
3. ✅ Document new failure modes
4. ✅ Add recovery procedures
5. ✅ State "skill(gcp-vm-wave-execution): no gaps identified" if no gaps found

**Last Audit**: 2026-06-13 21:45 UTC - **CRITICAL UPDATE**: Added Golden Rule (always copy same phase from previous wave). Updated Phase 3 requirements to use Claude advanced mode. Added prevention measures and recovery procedure. This update prevents the architecture bug discovered in Wave 3 Phase 3.

---

## References

- **Complete Handoff**: `WAVE3_PHASE3_COMPLETE_HANDOFF.md`
- **Lessons Learned**: `building-blocks/autonomous-refactoring/WAVE3_PHASE3_LESSONS_LEARNED.md`
- **Updated SOP**: `docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md`
- **Architecture Bug**: `WAVE3_PHASE3_ARCHITECTURE_BUG_ANALYSIS.md`

---

**MANDATORY**: All future phase script generation MUST follow the Golden Rule.