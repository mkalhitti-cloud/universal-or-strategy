# Wave 7 Template Status Report

**Date**: 2026-06-21
**Status**: ⚠️ CRITICAL BLOCKER IDENTIFIED

## Summary

All 9 phase templates have been successfully copied from Wave 5 to Wave 7 directory (`building-blocks/wave7/`), but a **CRITICAL BLOCKER** has been identified: **None of the templates use the mandatory temp file + command substitution pattern**.

## Template Inventory

✅ **All 9 templates copied successfully:**

1. `phase0_template_wave7.sh` - Phase 0 (Hotspot Analysis)
2. `phase1_template_wave7.sh` - Phase 1 (Scope Definition)
3. `phase1_5_template_wave7.sh` - Phase 1.5 (Boundary Validation)
4. `phase2_template_wave7.sh` - Phase 2 (Architecture Planning)
5. `phase3_template_wave7.sh` - Phase 3 (DNA Audit)
6. `phase4_template_wave7.sh` - Phase 4 (Ticket Generation)
7. `phase5_template_wave7.sh` - Phase 5 (Ticket Execution)
8. `phase5_v_template_wave7.sh` - Phase 5.V (Verification)
9. `phase6_template_wave7.sh` - Phase 6 (Final Review)

## Verification Results

### ✅ EPIC Naming Convention
- **Status**: PASSED (9/9)
- **Finding**: No EPIC-CCN-XXX references found
- **Action**: None required

### ❌ Temp File Pattern (CRITICAL BLOCKER)
- **Status**: FAILED (0/9)
- **Finding**: ALL templates missing mandatory temp file + command substitution pattern
- **Impact**: Bob CLI will FREEZE if templates are used as-is
- **Root Cause**: Wave 5 templates were created before temp file pattern mandate

## Critical Issue Details

### The Problem

According to `docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md`, ALL Bob CLI invocations MUST use:

```bash
# Step 1: Create message file
cat > /tmp/phaseX_msg_$EPIC_ID.txt << 'EOFMSG'
[message content]
EOFMSG

# Step 2: Invoke Bob with command substitution
~/.npm-global/bin/bob --yolo --chat-mode MODE "$(cat /tmp/phaseX_msg_$EPIC_ID.txt)"
```

**BANNED Pattern** (causes freeze):
```bash
bob --yolo --chat-mode MODE "inline message"
```

### Current State

All 9 templates currently use an unknown pattern (blocked by .bobignore from inspection). Based on verification script results, they do NOT use the temp file pattern.

## Remediation Plan

### Option 1: Manual Template Update (RECOMMENDED)
1. User updates .bobignore to allow template inspection
2. Agent reads each template to identify current pattern
3. Agent updates each template to use temp file pattern
4. Agent verifies with `python scripts/verify_wave7_templates.py`
5. Agent commits updated templates

**Estimated Time**: 30-45 minutes
**Risk**: Low (templates are isolated, no production impact)

### Option 2: Reference Correct Templates
1. Locate templates that already use temp file pattern (if any exist)
2. Copy those templates instead of Wave 5 templates
3. Update EPIC naming to EPIC-W7-XXX format

**Estimated Time**: 15-20 minutes
**Risk**: Medium (depends on finding correct templates)

### Option 3: Generate from Scratch (NOT RECOMMENDED)
1. Generate new templates following SOP V3
2. Violates Building-Blocks Method (copy, don't generate)

**Estimated Time**: 60+ minutes
**Risk**: High (violates protocol, may introduce errors)

## Recommended Next Steps

1. **IMMEDIATE**: User must decide on remediation approach
2. **BEFORE WAVE 7 EXECUTION**: Templates MUST be fixed
3. **VERIFICATION**: Run `python scripts/verify_wave7_templates.py` until 9/9 pass
4. **DOCUMENTATION**: Update this report with remediation results

## Verification Script

A comprehensive verification script has been created:

```bash
# Check templates (read-only)
python scripts/verify_wave7_templates.py

# Auto-fix EPIC naming (if needed)
python scripts/verify_wave7_templates.py --fix
```

**Script Features**:
- ✅ Checks temp file pattern compliance
- ✅ Checks EPIC naming convention
- ✅ Detects BANNED inline bob pattern
- ✅ Auto-fixes EPIC naming (with --fix flag)
- ✅ Color-coded output
- ✅ Windows console compatible

## .bobignore Issue

**BLOCKER**: The `building-blocks/` directory is excluded in `.bobignore`, preventing agent access to templates for inspection and modification.

**Required Action**: User must either:
1. Temporarily remove `building-blocks/` from `.bobignore`, OR
2. Manually update templates following SOP V3, OR
3. Provide agent with alternative access method

## References

- **SOP**: `docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md`
- **Architecture**: `building-blocks/autonomous-refactoring/ARCHITECTURE.md`
- **Verification Script**: `scripts/verify_wave7_templates.py`
- **Template Directory**: `building-blocks/wave7/`

## Status History

| Date | Status | Notes |
|------|--------|-------|
| 2026-06-21 | ⚠️ BLOCKER | Templates copied but missing temp file pattern; .bobignore blocks access |

---

**CRITICAL**: Wave 7 execution is BLOCKED until:
1. Templates are updated to use temp file pattern, AND
2. Verification script confirms 9/9 templates pass