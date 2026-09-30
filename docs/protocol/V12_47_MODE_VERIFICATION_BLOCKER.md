# V12.47 Mode Verification Blocker Protocol

**Version**: 1.0  
**Date**: 2026-06-16  
**Status**: MANDATORY  
**Severity**: P0 (Blocking - Prevents 1000+ wasted runs)

---

## Executive Summary

**Problem**: Wave 4 (79 epics) and Wave 5 pilot (1 epic) executed in **code mode** despite explicit `--chat-mode v12-engineer` flags, violating V12.18 protocol. Total waste: ~80 epics × $0.05 = **$4.00** + debugging time.

**Root Cause**: No verification that Bob actually used the requested mode. Agents report mode in logs, but we never checked.

**Solution**: MANDATORY mode verification gate before ANY phase execution proceeds.

---

## Critical Findings

### 1. --yolo Flag Was Present ✅

**Pilot Script Line 55**:
```bash
bob --yolo --chat-mode v12-engineer "$(cat /tmp/phase5_msg_001_v2.txt)"
```

**Finding**: `--yolo` flag was present and correct. This was NOT the issue.

### 2. Agents DO Report Mode ✅

**Wave 5 Pilot Log Line 40**:
```
**Mode Check**: Currently in 'code' mode - this is acceptable for Phase 5 execution as it involves surgical code changes.
```

**Finding**: Bob DOES report mode in logs. We just never verified it matched the requested mode.

### 3. Wave 4 Logs Have NO Mode Reporting ❌

**Command**:
```powershell
Get-ChildItem logs/phase5/*.log | Select-Object -First 1 | ForEach-Object { 
    Get-Content $_.FullName | Select-String "Currently in.*mode|Using model|Mode Check" 
}
```

**Result**: Empty output (no mode reporting found)

**Finding**: Wave 4 logs (if they exist) do NOT contain mode verification. This suggests:
- Either logs were not saved
- Or Bob did not report mode in Wave 4
- Or mode reporting is inconsistent

---

## The Blocker: Mode Verification Gate

### MANDATORY Pre-Execution Check

**Before ANY phase execution proceeds**, verify mode in pilot test log:

```bash
# 1. Execute pilot test
./scripts/wave{N}/_p{X}_001.sh > pilot_test.log 2>&1

# 2. Extract mode from log
MODE=$(grep -oP "Currently in '\K[^']+(?=' mode)" pilot_test.log)

# 3. Verify mode matches expected
EXPECTED_MODE="v12-engineer"  # For Phase 5
if [ "$MODE" != "$EXPECTED_MODE" ]; then
    echo "ERROR: Mode mismatch!"
    echo "Expected: $EXPECTED_MODE"
    echo "Actual: $MODE"
    exit 1
fi

# 4. Only proceed if mode matches
echo "✅ Mode verified: $MODE"
```

### MANDATORY Post-Execution Check

**After pilot test completes**, verify mode in execution log:

```bash
# 1. Check if mode was reported
if ! grep -q "Currently in.*mode" pilot_test.log; then
    echo "ERROR: No mode reporting found in log"
    echo "Bob may not be reporting mode - investigate"
    exit 1
fi

# 2. Extract and verify mode
MODE=$(grep -oP "Currently in '\K[^']+(?=' mode)" pilot_test.log)
echo "Mode used: $MODE"

# 3. Verify against expected
if [ "$MODE" != "v12-engineer" ]; then
    echo "ERROR: Wrong mode used"
    echo "Expected: v12-engineer"
    echo "Actual: $MODE"
    exit 1
fi
```

---

## Building-Blocks Gap Analysis

### Why Was This Missed?

**Question**: "This should have been in the building blocks, why was it missed?"

**Answer**: Building-blocks method copies scripts from previous wave, but Wave 4 scripts had NO mode verification. The gap propagated forward.

**Wave 4 Phase 5 Scripts**:
```bash
# Line 55 (Wave 4)
bob --yolo "$(cat /tmp/phase5_msg_X.txt)"
# NO --chat-mode flag
# NO mode verification
```

**Wave 5 Phase 5 Scripts**:
```bash
# Line 55 (Wave 5)
bob --yolo --chat-mode v12-engineer "$(cat /tmp/phase5_msg_X.txt)"
# ADDED --chat-mode flag (good!)
# STILL NO mode verification (gap!)
```

**Root Cause**: Building-blocks method is fast but requires **verification templates**. We added the mode flag but forgot to add mode verification.

---

## Protocol Updates Required

### 1. Update SOP (WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md)

**Add to Step 6 (Pilot Test)**:

```markdown
### Step 6: Test with 2 Epics (Pilot Test) - UPDATED V12.47

**Run pilot test**:
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="cd universal-or-strategy && ./scripts/wave{N}/_p{X}_116.sh" \
  | tee pilot_test.log
```

**MANDATORY: Verify mode in log**:
```bash
# Extract mode from log
MODE=$(grep -oP "Currently in '\K[^']+(?=' mode)" pilot_test.log)

# Verify mode matches expected
EXPECTED_MODE="v12-engineer"  # Adjust per phase
if [ "$MODE" != "$EXPECTED_MODE" ]; then
    echo "❌ BLOCKER: Mode mismatch!"
    echo "Expected: $EXPECTED_MODE"
    echo "Actual: $MODE"
    echo "DO NOT PROCEED with full wave"
    exit 1
fi

echo "✅ Mode verified: $MODE"
```

**Verify output format**:
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="ls -lh docs/brain/EPIC-CCN-116/0{X}-*.md"
```

**Deploy all only after pilot success AND mode verification**.
```

### 2. Update Skill (gcp-vm-wave-execution/skill.md)

**Add to Pre-Wave Checklist**:

```markdown
### Pre-Wave Checklist (MANDATORY - V12.47)

**8. Mode Verification Template** (NEW - V12.47):
```bash
# Add to pilot test script
MODE=$(grep -oP "Currently in '\K[^']+(?=' mode)" pilot_test.log)
EXPECTED_MODE="v12-engineer"  # Adjust per phase
if [ "$MODE" != "$EXPECTED_MODE" ]; then
    echo "❌ BLOCKER: Mode mismatch"
    exit 1
fi
```
```

### 3. Update Building-Blocks Templates

**Add to all phase scripts** (after Bob execution):

```bash
# Execute with Bob Shell
bob --yolo --chat-mode v12-engineer "$(cat /tmp/phase5_msg_X.txt)" | tee /tmp/phase5_log_X.txt

# MANDATORY: Verify mode (V12.47)
MODE=$(grep -oP "Currently in '\K[^']+(?=' mode)" /tmp/phase5_log_X.txt)
if [ "$MODE" != "v12-engineer" ]; then
    echo "ERROR: Mode mismatch! Expected v12-engineer, got $MODE"
    exit 1
fi
echo "✅ Mode verified: $MODE"
```

---

## Rollback Impact Analysis

### Wave 4 + Wave 5 Rollback Cost

**Epics Affected**:
- Wave 4: 79 epics (all used code mode)
- Wave 5: 1 epic (pilot used code mode)
- Total: 80 epics

**Cost Calculation**:
```
Lost Cost = 80 epics × $0.05 = $4.00
Retry Cost = 80 epics × $0.05 = $4.00
Total Impact = $8.00
```

**Time Impact**:
- Wave 4 execution: ~28 hours
- Wave 5 pilot: ~1 hour
- Debugging: ~4 hours
- Total: ~33 hours wasted

**Root Cause**: No mode verification gate allowed 1000+ runs to proceed in wrong mode.

---

## Immediate Actions

### 1. Create Mode Verification Script

**File**: `scripts/verify_mode.sh`

```bash
#!/bin/bash
# Mode Verification Script (V12.47)
# Usage: ./verify_mode.sh <log_file> <expected_mode>

LOG_FILE="$1"
EXPECTED_MODE="$2"

if [ ! -f "$LOG_FILE" ]; then
    echo "ERROR: Log file not found: $LOG_FILE"
    exit 1
fi

# Extract mode from log
MODE=$(grep -oP "Currently in '\K[^']+(?=' mode)" "$LOG_FILE")

if [ -z "$MODE" ]; then
    echo "ERROR: No mode reporting found in log"
    echo "Bob may not be reporting mode - investigate"
    exit 1
fi

echo "Mode found in log: $MODE"

if [ "$MODE" != "$EXPECTED_MODE" ]; then
    echo "❌ BLOCKER: Mode mismatch!"
    echo "Expected: $EXPECTED_MODE"
    echo "Actual: $MODE"
    exit 1
fi

echo "✅ Mode verified: $MODE"
exit 0
```

### 2. Update All Phase Scripts

**Add mode verification to all building-blocks templates**:
- `building-blocks/autonomous-refactoring/phase0_template.sh`
- `building-blocks/autonomous-refactoring/phase1_template.sh`
- `building-blocks/autonomous-refactoring/phase2_template.sh`
- `building-blocks/autonomous-refactoring/phase3_template.sh`
- `building-blocks/autonomous-refactoring/phase4_template.sh`
- `building-blocks/autonomous-refactoring/phase5_template.sh`
- `building-blocks/autonomous-refactoring/phase6_template.sh`

### 3. Update Pilot Test Checklist

**Add to pilot test verification**:
- [ ] Mode reported in log
- [ ] Mode matches expected (v12-engineer for Phase 5)
- [ ] No mode mismatch errors

---

## Success Criteria

### Per Pilot Test
- ✅ Mode reported in log
- ✅ Mode matches expected
- ✅ No mode mismatch errors
- ✅ Output files created
- ✅ Build passes

### Per Wave
- ✅ Pilot test mode verified
- ✅ All epics use correct mode
- ✅ No protocol violations
- ✅ 100% completion rate

---

## References

- **V12.46**: Mode flag syntax investigation (both syntaxes valid)
- **V12.45**: Pilot failure analysis (original hypothesis: syntax error)
- **V12.43**: Mode enforcement analysis (Wave 4 used code mode)
- **V12.18**: Code mode ban (MANDATORY protocol)
- **SOP**: `docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md`
- **Skill**: `.bob/skills/gcp-vm-wave-execution/skill.md`

---

**Status**: Protocol created. Awaiting implementation in building-blocks templates and SOP updates.

**Next Steps**:
1. Create `scripts/verify_mode.sh`
2. Update all building-blocks templates
3. Update SOP with mode verification step
4. Update skill with mode verification checklist
5. Test mode verification on VM (with and without MCP)
6. Document findings in V12.48