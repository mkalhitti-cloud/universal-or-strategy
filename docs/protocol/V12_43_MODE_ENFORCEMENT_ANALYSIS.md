# V12.43 Mode Enforcement Analysis

**Date**: 2026-06-17
**Status**: CRITICAL DISCOVERY
**Impact**: Wave 4 and Wave 5 execution strategy

## Executive Summary

Wave 4 used **explicit mode flags** in ALL phases (Phase 0-4), but Phase 5 scripts did NOT specify mode. This caused Bob CLI to default to **code mode** when MCP failed, violating V12.18 protocol.

## Evidence from Wave 4 Scripts

### Phases 0-4: Explicit Mode Enforcement ✅

**Phase 0** (Hotspot Analysis):
```bash
bob --yolo --chat-mode v12-phase0-hotspot "$(cat /tmp/phase0_msg_001.txt)"
```

**Phase 1** (Scope Definition):
```bash
bob --yolo --chat-mode plan "$(cat /tmp/phase1_msg_001.txt)"
```

**Phase 2** (Architecture Planning):
```bash
bob --yolo --chat-mode plan "$(cat /tmp/phase2_msg_001.txt)"
```

**Phase 3-4**: Similar pattern with explicit `--chat-mode` flags

### Phase 5: NO Mode Enforcement ❌

**Phase 5** (Ticket Execution):
```bash
bob --yolo "$(cat /tmp/phase5_msg_001.txt)"
```

**Missing**: `--chat-mode v12-engineer` flag

## Root Cause

1. **Phase 5 scripts rely on MCP** to enforce mode via instructions
2. **When MCP fails** (15 connection errors), Bob doesn't receive mode instructions
3. **Bob defaults to code mode** (BANNED per V12.18)
4. **Work completes anyway** because code mode has sufficient capabilities for simple extractions

## Why Wave 4 Succeeded Despite Violation

**Code mode capabilities**:
- ✅ Can read files
- ✅ Can write files
- ✅ Can apply diffs
- ✅ Can execute commands
- ❌ No MCP tools (jcodemunch, graphify)
- ❌ No browser tools
- ❌ Inferior context management

**Wave 4 extractions were simple enough** that code mode's limited capabilities were sufficient. However, this violates protocol and could fail on complex refactorings.

## Wave 5 Solution

**Add explicit mode flag to Phase 5 scripts**:

```bash
# OLD (Wave 4 - protocol violation)
bob --yolo "$(cat /tmp/phase5_msg_001.txt)"

# NEW (Wave 5 - protocol compliant)
bob --yolo --chat-mode v12-engineer "$(cat /tmp/phase5_msg_001.txt)"
```

## Impact Assessment

### Wave 4 (Completed)
- **79/80 epics succeeded** (98.75% success rate)
- **Mode violation**: All 80 epics executed in code mode
- **Quality issues**: 28 Greptile findings (scope creep, pre-existing fixes)
- **Rollback reason**: Quality issues, NOT mode violation

### Wave 5 (Pending)
- **Must add mode flag** to all 78 Phase 5 scripts
- **Expected behavior**: Bob uses v12-engineer mode regardless of MCP status
- **Protocol compliance**: V12.18 satisfied
- **Risk reduction**: Better context management for complex extractions

## Building-Blocks Method Update

**MANDATORY**: When copying Phase 5 scripts from Wave 4, ADD mode flag:

```bash
# Find-and-replace pattern
OLD: bob --yolo "$(cat /tmp/phase5_msg_
NEW: bob --yolo --chat-mode v12-engineer "$(cat /tmp/phase5_msg_
```

## Verification Checklist

Before launching Wave 5:
- [ ] All 78 Phase 5 scripts have `--chat-mode v12-engineer` flag
- [ ] Pilot test (EPIC-CCN-001) verifies mode enforcement
- [ ] Bob CLI log shows "v12-engineer mode" (not "code mode")
- [ ] MCP errors still occur (expected, non-blocking)
- [ ] Work completes successfully with correct mode

## Protocol Update

**V12.43**: Phase 5 scripts MUST include explicit `--chat-mode v12-engineer` flag. Do NOT rely on MCP to enforce mode.

**Enforcement**: Pre-flight validation script must verify mode flag presence in all Phase 5 scripts.

## References

- V12.18: Code Mode Ban
- V12.42: MCP Errors Non-Blocking
- Wave 4 scripts: `scripts/wave4/_p0_*.sh` through `_p4_*.sh` (all have mode flags)
- Wave 4 scripts: `scripts/wave4/_p5_*.sh` (missing mode flags)
- Bad run log: `docs/wave5phase5badrun.md` line 40 (mode check shows code mode)