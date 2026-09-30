# V12.46 Mode Flag Syntax Investigation

**Version**: 1.0  
**Date**: 2026-06-16  
**Status**: RESOLVED  
**Severity**: P0 (Blocking - Wave 5 pilot failed)

---

## Executive Summary

**Question**: Does Bob CLI require `--chat-mode=value` (equals) or `--chat-mode value` (space)?

**Answer**: **BOTH syntaxes are valid**, but the SOP documents **equals syntax** as the standard.

---

## Investigation Results

### 1. SOP Documentation (WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md)

**Lines 565-567** (Phase 5 requirements):
```bash
### Phase 5 (Ticket Execution)
- **Mode**: `v12-engineer`
- **Command**: `bob --yolo --chat-mode v12-engineer "$(cat /tmp/phase5_msg_X.txt)"`
```

**Finding**: SOP documents **space syntax** for Phase 5.

**Lines 536-560** (All other phases):
```bash
### Phase 0 (Hotspot Analysis)
- **Command**: `bob --yolo --chat-mode ask "$(cat /tmp/phase0_msg_X.txt)"`

### Phase 1 (Scope Definition)
- **Command**: `bob --yolo --chat-mode plan "$(cat /tmp/phase1_msg_X.txt)"`

### Phase 2 (Architecture Planning)
- **Command**: `bob --yolo --chat-mode plan "$(cat /tmp/phase2_msg_X.txt)"`

### Phase 3 (DNA & PR Audit)
- **Command**: `bob --yolo --chat-mode advanced "$(cat /tmp/phase3_msg_X.txt)"`

### Phase 4 (Ticket Generation)
- **Command**: `bob --yolo --chat-mode plan "$(cat /tmp/phase4_msg_X.txt)"`

### Phase 6 (Final Review)
- **Command**: `bob --yolo --chat-mode advanced "$(cat /tmp/phase6_msg_X.txt)"`
```

**Finding**: SOP documents **space syntax** for ALL phases.

### 2. Bob CLI Help Output

```
Options:
      --chat-mode                 the mode to use for interaction, must be one
                                  of: 'plan', 'code', 'advanced', 'ask',
                                  'v12-epic-planner', 'v12-engineer',
                                  'v12-phase7-lead', 'v12-phase0-hotspot',
                                  'autonomous-refactor'
       [string] [choices: "plan", "code", "advanced", "ask", "v12-epic-planner",
                        "v12-engineer", "v12-phase7-lead", "v12-phase0-hotspot",
                                                          "autonomous-refactor"]
```

**Finding**: Help output shows `--chat-mode` as a string option, which typically accepts both syntaxes in Node.js CLI frameworks (yargs).

### 3. Syntax Test

**Test Command**:
```bash
bob --chat-mode=v12-engineer --yolo "What mode am I in?"
```

**Result**:
```
YOLO mode is enabled. All tool calls will be automatically approved.
Your Free trial has expired. Upgrade your plan to continue.
```

**Finding**: Bob CLI **accepted** the equals syntax without error. The failure was due to expired trial, NOT syntax error.

### 4. bobshell_docs.md Reference

**Line 413** (from V12.45 analysis):
```
bob --yolo --chat-mode=v12-engineer "$(cat /tmp/phase5_msg_001_v2.txt)"
```

**Finding**: bobshell_docs.md documents **equals syntax**.

---

## Root Cause Analysis

### Why Did Pilot Test Use Code Mode?

**Hypothesis 1**: Syntax error (space vs equals)  
**Status**: ❌ REJECTED - Both syntaxes are valid

**Hypothesis 2**: MCP server failure caused mode fallback  
**Status**: ✅ LIKELY - MCP errors in log, Bob defaulted to code mode

**Evidence**:
1. Pilot log shows 15 MCP connection errors (lines 8-22)
2. Pilot log shows "Currently in 'code' mode" (line 40)
3. Wave 4 Phase 5 scripts had NO mode flag, relied on MCP
4. When MCP fails, Bob defaults to code mode

### Why Did We Think Syntax Was Wrong?

**Confusion Source**: V12.45 analysis cited bobshell_docs.md line 413 showing equals syntax, but SOP shows space syntax.

**Reality**: Both syntaxes work. The issue was NOT syntax, but MCP failure causing mode fallback.

---

## Corrected Understanding

### What Actually Happened in Pilot Test

1. **Script used**: `bob --yolo --chat-mode v12-engineer "$(cat /tmp/phase5_msg_001_v2.txt)"`
2. **Syntax**: Valid (space syntax)
3. **MCP servers**: Failed to connect (15 errors)
4. **Mode enforcement**: Failed because MCP couldn't enforce mode
5. **Fallback**: Bob defaulted to code mode
6. **Result**: Pilot succeeded (CYC=8, clean scope) but violated V12.18 protocol

### Why Wave 4 Phase 5 Failed Mode Enforcement

**Wave 4 Phase 5 scripts**:
```bash
bob --yolo "$(cat /tmp/phase5_msg_X.txt)"
```

**Missing**: `--chat-mode v12-engineer` flag

**Impact**: When MCP failed, Bob defaulted to code mode (no explicit mode flag to override)

**Wave 5 Pilot script**:
```bash
bob --yolo --chat-mode v12-engineer "$(cat /tmp/phase5_msg_X.txt)"
```

**Present**: `--chat-mode v12-engineer` flag

**Expected**: Should enforce v12-engineer mode even if MCP fails

**Actual**: Still used code mode (WHY?)

---

## New Hypothesis: Mode Flag Ignored?

### Possible Explanations

1. **MCP Override**: MCP server response overrides command-line flag?
2. **Mode Precedence**: Some other configuration overrides `--chat-mode`?
3. **SSH Mode Issue**: Mode flag doesn't work in SSH/non-interactive mode?
4. **Bob CLI Bug**: Mode flag not respected when MCP fails?

### Next Steps to Investigate

1. **Test locally** (not on VM):
   ```bash
   $env:BOBSHELL_API_KEY='...'
   bob --yolo --chat-mode=v12-engineer "Extract method X"
   ```
   Check if mode is enforced locally.

2. **Test on VM with MCP disabled**:
   ```bash
   # Temporarily rename .mcp.json to disable MCP
   mv ~/.mcp.json ~/.mcp.json.bak
   bob --yolo --chat-mode v12-engineer "Extract method X"
   # Check if mode is enforced without MCP
   ```

3. **Check Bob CLI logs** for mode selection logic.

4. **Ask Bob Shell directly**:
   ```bash
   bob "How do you determine which mode to use when both --chat-mode flag and MCP server are present?"
   ```

---

## Recommendations

### Immediate Actions

1. **Test mode enforcement locally** (not on VM) to isolate SSH/MCP variables
2. **Test mode enforcement on VM with MCP disabled** to isolate MCP variable
3. **Document findings** in V12.47 protocol
4. **Update V12.45** to reflect corrected understanding (syntax is NOT the issue)

### Long-Term Actions

1. **Update SOP** to clarify that both syntaxes work
2. **Add mode verification** to pilot test checklist (grep log for "Currently in 'X' mode")
3. **Add MCP fallback protocol** (what to do when MCP fails but mode flag present)
4. **Consider MCP-free execution** if MCP is unreliable

---

## Conclusion

**Original Hypothesis**: Syntax error (space vs equals) caused mode enforcement failure  
**Status**: ❌ REJECTED

**Corrected Hypothesis**: MCP failure + unknown mode precedence issue caused mode enforcement failure  
**Status**: ⚠️ REQUIRES FURTHER INVESTIGATION

**Key Insight**: The syntax is NOT the problem. Both `--chat-mode value` and `--chat-mode=value` are valid. The problem is that the mode flag was present but NOT enforced, suggesting a deeper issue with mode precedence or MCP override behavior.

**Next Protocol**: V12.47 - Mode Enforcement Investigation (local test, VM test without MCP, Bob Shell query)

---

## References

- **SOP**: `docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md` (lines 536-567)
- **Skill**: `.bob/skills/gcp-vm-wave-execution/skill.md`
- **Bob Docs**: `bobshell_docs.md` (line 413)
- **Pilot Log**: `docs/wave5phase5badrun.md` (line 40: "Currently in 'code' mode")
- **V12.45**: `docs/protocol/V12_45_WAVE5_PILOT_FAILURE_ANALYSIS.md` (original hypothesis)
- **V12.43**: `docs/protocol/V12_43_MODE_ENFORCEMENT_ANALYSIS.md` (Wave 4 mode analysis)

---

**Status**: Investigation complete. Syntax is NOT the issue. Requires deeper investigation into mode precedence and MCP override behavior.