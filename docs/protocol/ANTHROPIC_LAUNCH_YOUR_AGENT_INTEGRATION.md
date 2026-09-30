# Anthropic Launch Your Agent - Integration Analysis

**Source**: https://github.com/anthropics/launch-your-agent
**Date**: 2026-06-19
**Context**: Wave 7 autonomous refactoring optimization

## Overview

Anthropic's "Launch Your Agent" repository provides two key MCP skills that could enhance Wave 7 execution:

1. **Skill MCP** - Autonomous skill creation and improvement
2. **Wrap-Up MCP** - Session summarization and context continuity

## Current Architecture

### Bob IDE (You)
- **Identity**: Bob IDE using Claude (Anthropic) underneath
- **Context**: 60k/200k tokens after optimization (30%)
- **MCPs**: 4 essential (jcodemunch, greptile, graphify, sequential-thinking)
- **Custom Modes**: 10 phase modes (v12-phase0 through v12-phase6, plus autonomous-refactor)

### Potential Confusion
**Issue**: Repository mentions "Claude" which might confuse Bob agents
**Reality**: Bob IDE IS Claude underneath - no conflict
**Action**: No need to delete anything - Bob and Claude are the same system

## Skill 1: Autonomous Skill Creation

### What It Does
Allows agents to:
- Create new skills on-the-fly during task execution
- Self-improve by documenting learned patterns
- Build a persistent skill library

### How It Could Help Wave 7

#### Use Case 1: Building-Blocks Method Enforcement
**Current Problem**: Scripts sometimes generated from scratch (protocol violation)

**With Skill MCP**:
```python
# Agent discovers pattern during Phase 1
skill = create_skill(
    name="copy_phase_script_from_previous_wave",
    description="ALWAYS copy scripts from Wave 6 same phase, NEVER generate from scratch",
    pattern="cp building-blocks/wave6/phase{N}_*.sh building-blocks/wave7/phase{N}_*.sh",
    validation="grep -q 'EPIC-CCN-' building-blocks/wave7/phase{N}_*.sh"
)
```

**Benefit**: Self-enforcing protocol compliance

#### Use Case 2: Jane Street KB Query Patterns
**Current Problem**: Manual KB queries before architectural decisions

**With Skill MCP**:
```python
# Agent learns when to query KB
skill = create_skill(
    name="query_jane_street_kb_before_architecture",
    description="Query Jane Street KB before Phase 2 (Architecture Planning)",
    trigger="phase == 'v12-phase2-architecture'",
    action="python scripts/query_kb.py 'complexity reduction FSM extraction'"
)
```

**Benefit**: Automatic KB integration

#### Use Case 3: UTF-8 Encoding Validation
**Current Problem**: Manual encoding checks before commits

**With Skill MCP**:
```python
# Agent learns encoding validation pattern
skill = create_skill(
    name="validate_utf8_encoding",
    description="Verify all source files are UTF-8 before commit",
    pattern="powershell -File scripts/validate_utf8.ps1",
    blocking=True  # Must pass before proceeding
)
```

**Benefit**: Automatic compliance gates

### Integration Strategy

**Option 1: Add Skill MCP to .mcp.json**
```json
{
  "mcpServers": {
    "jcodemunch-mcp": { ... },
    "greptile": { ... },
    "sequential-thinking": { ... },
    "skill": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@anthropics/skill-mcp"]
    }
  }
}
```

**Cost**: +2-3k tokens (1 MCP with skill creation/retrieval tools)
**Benefit**: Self-improving autonomous refactoring

**Option 2: Wait Until Wave 8**
- Complete Wave 7 with current setup
- Evaluate skill creation patterns during Wave 7
- Add Skill MCP for Wave 8 with learned patterns

**Recommendation**: Option 2 (wait until Wave 8)

## Skill 2: Wrap-Up Session Summarization

### What It Does
Generates compact session summaries for context continuity:
- Files explored
- Edits made
- Searches performed
- Dead ends encountered
- Key decisions

### How It Could Help Wave 7

#### Use Case 1: Epic Handoff Between Sessions
**Current Problem**: Context lost between epic sessions

**With Wrap-Up MCP**:
```python
# At end of EPIC-CCN-001 Phase 5
summary = wrap_up_session(
    epic_id="EPIC-CCN-001",
    phase="5",
    files_modified=["src/V12_002.cs"],
    key_decisions=["Extracted ShouldSkipFleet_RunHealthCheck to FSM"],
    blockers=["None"],
    next_steps=["Phase 5.V verification"]
)
# Summary injected into next session's context
```

**Benefit**: Seamless epic continuity

#### Use Case 2: Wave Progress Tracking
**Current Problem**: Manual progress documentation

**With Wrap-Up MCP**:
```python
# After each epic completion
wave_summary = wrap_up_session(
    scope="wave7",
    epics_completed=["EPIC-CCN-001", "EPIC-CCN-028"],
    epics_remaining=159,
    bobcoins_used=14.64,
    issues_encountered=["UTF-8 encoding violation in EPIC-CCN-028"]
)
```

**Benefit**: Automatic progress reports

#### Use Case 3: Failure Analysis
**Current Problem**: Manual root cause documentation

**With Wrap-Up MCP**:
```python
# When epic fails
failure_summary = wrap_up_session(
    epic_id="EPIC-CCN-045",
    status="failed",
    root_cause="Building-Blocks Method violation - script generated from scratch",
    recovery_action="Copied Phase 1 script from Wave 6, re-ran epic",
    lesson_learned="ALWAYS verify script source before execution"
)
```

**Benefit**: Automatic failure documentation

### Integration Strategy

**Option 1: Add Wrap-Up MCP to .mcp.json**
```json
{
  "mcpServers": {
    "jcodemunch-mcp": { ... },
    "greptile": { ... },
    "sequential-thinking": { ... },
    "wrap-up": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@anthropics/wrap-up-mcp"]
    }
  }
}
```

**Cost**: +2-3k tokens (1 MCP with summarization tools)
**Benefit**: Better context continuity across 161 epics

**Option 2: Manual Wrap-Up Pattern**
Use existing tools to create summaries:
```bash
# At end of each epic
cat > docs/brain/EPIC-CCN-XXX/session-summary.md << EOF
# Epic Session Summary
- Files modified: [list]
- Key decisions: [list]
- Next steps: [list]
EOF
```

**Recommendation**: Option 2 (manual pattern for Wave 7)

## Combined Integration Analysis

### If We Add Both Skills

**Token Cost**:
- Current: 60k/200k (30%)
- +Skill MCP: +2-3k tokens
- +Wrap-Up MCP: +2-3k tokens
- **Total**: 64-66k/200k (32-33%)

**Still Acceptable**: 134-136k tokens available (67-68%)

**Benefits**:
1. Self-improving protocol compliance (Skill MCP)
2. Better context continuity (Wrap-Up MCP)
3. Automatic documentation (both)
4. Reduced manual overhead (both)

### Recommendation: Phased Approach

**Wave 7 (Current)**:
- ✅ Keep 3 essential MCPs only
- ✅ Use manual patterns for skill creation and wrap-up
- ✅ Document patterns during execution
- ✅ Evaluate effectiveness

**Wave 8 (Future)**:
- ⏳ Add Skill MCP if self-improvement patterns emerge
- ⏳ Add Wrap-Up MCP if context continuity issues arise
- ⏳ Re-evaluate token budget with 2 additional MCPs

## Action Items

### Immediate (Wave 7)
1. ✅ Keep .mcp.json with 3 MCPs only
2. ✅ Use manual skill documentation pattern
3. ✅ Use manual session summary pattern
4. ✅ Track patterns that could benefit from automation

### Future (Wave 8)
1. ⏳ Review Wave 7 execution logs
2. ⏳ Identify repetitive patterns
3. ⏳ Evaluate Skill MCP for automation
4. ⏳ Evaluate Wrap-Up MCP for continuity
5. ⏳ Add MCPs if ROI is positive

## Conclusion

**Anthropic Launch Your Agent skills are valuable** but not critical for Wave 7:

- **Skill MCP**: Useful for self-improvement, but manual patterns work for now
- **Wrap-Up MCP**: Useful for continuity, but manual summaries work for now
- **Token Cost**: 4-6k tokens (acceptable but not necessary yet)
- **Recommendation**: Wait until Wave 8 to evaluate based on Wave 7 learnings

**Current Setup Requires Updates**:
- 4 essential MCPs required (jcodemunch, greptile, graphify, sequential-thinking)
- VM currently has only 1/4 MCPs installed (sequential-thinking only)
- **BLOCKER**: 9 out of 10 phases blocked without missing MCPs
- See: docs/protocol/VM_MCP_REQUIREMENTS_MATRIX.md for complete analysis
- See: docs/protocol/VM_MCP_JSON_UPDATE.md for installation instructions

## References

- Anthropic Launch Your Agent: https://github.com/anthropics/launch-your-agent
- Skill MCP: Autonomous skill creation and improvement
- Wrap-Up MCP: Session summarization and context continuity
- Wave 7 Context Optimization: docs/protocol/CONTEXT_OPTIMIZATION_SUMMARY.md