# Autonomous Refactor Integration Matrix

**Date**: 2026-06-21  
**Purpose**: Map skills, MCPs, modes, and slash commands across all 10 phases  
**Context**: Wave 7 preparation - validate complete integration

## Executive Summary

This document validates that the `/autonomous-refactor` master orchestrator properly integrates all 10 phases of the V12 epic workflow, showing which MCPs, skills, and modes each phase uses.

### Key Findings

✅ **All 10 phases mapped to slash commands**  
✅ **MCP usage validated for advanced mode phases**  
✅ **Skill references documented (1 explicit, 5 implicit)**  
⚠️ **Gap identified**: Greptile MCP not in `.mcp.json` but referenced in system prompt  
⚠️ **Gap identified**: Several phases lack explicit skill references

---

## Phase-by-Phase Integration Matrix

| Phase | Slash Command | Mode | MCPs Used | Skills Used | Status |
|-------|---------------|------|-----------|-------------|--------|
| **0** | `/epic-intake` | `ask` | None | VM execution (implicit) | ✅ Mapped |
| **1** | `/epic-scope-boundary` | `plan` | None | None | ✅ Mapped |
| **1.5** | `/epic-scope-boundary --phase 1.5` | `plan` | None | Scope boundary check (implicit) | ✅ Mapped |
| **2** | `/epic-plan` | `plan` | None | Architecture validation (explicit) | ✅ Mapped |
| **3** | `/epic-scan` | `advanced` | jcodemunch, sequential-thinking | None | ✅ Mapped |
| **4** | `/epic-tickets` | `plan` | None | None | ✅ Mapped |
| **4.5** | (Manual review) | N/A | N/A | None | ⚠️ No command |
| **5** | `/epic-validate` | `v12-engineer` | Bob CLI (external) | VM/parallel execution (implicit) | ✅ Mapped |
| **5.V** | `/epic-verify-ticket` | `advanced` | jcodemunch, sequential-thinking | None | ✅ Mapped |
| **6** | `/epic-review-final` | `advanced` | jcodemunch, sequential-thinking | None | ✅ Mapped |

---

## Detailed Phase Analysis

### Phase 0: Hotspot Analysis
**Command**: `/epic-intake`  
**Mode**: `ask`  
**MCPs**: None (ask mode doesn't support MCPs)  
**Skills**:
- `.bob/skills/gcp-vm-wave-execution/` (implicit) - For VM parallel execution
- `plugins/multi-agent-orchestrator/WAVE2_SHELL_WORKAROUND.md` (implicit) - SSH file I/O

**Validation**: ✅ Correctly mapped in `/autonomous-refactor` Phase 1

---

### Phase 1: Scope Definition
**Command**: `/epic-scope-boundary`  
**Mode**: `plan`  
**MCPs**: None (plan mode doesn't support MCPs)  
**Skills**: None

**Validation**: ✅ Correctly mapped in `/autonomous-refactor` Phase 2 (Epic Execution Loop)

---

### Phase 1.5: Scope Boundary Validation
**Command**: `/epic-scope-boundary --phase 1.5`  
**Mode**: `plan`  
**MCPs**: None (plan mode doesn't support MCPs)  
**Skills**:
- `plugins/scope-boundary-check/SKILL.md` (implicit) - Validation logic

**Gap**: ⚠️ Should add explicit skill reference in command file

**Validation**: ✅ Correctly mapped in `/autonomous-refactor` Phase 2 (Epic Execution Loop)

---

### Phase 2: Architecture Planning
**Command**: `/epic-plan`  
**Mode**: `plan`  
**MCPs**: None (plan mode doesn't support MCPs)  
**Skills**:
- ✅ `@plugins/architecture-validation/SKILL.md` (explicit, line 205)

**Validation**: ✅ Correctly mapped in `/autonomous-refactor` Phase 2 (Epic Execution Loop)

---

### Phase 3: DNA & PR Audit
**Command**: `/epic-scan`  
**Mode**: `advanced`  
**MCPs**:
- ✅ `jcodemunch-mcp` - Code analysis, symbol search, file navigation
- ✅ `sequential-thinking` - Complex reasoning for audit analysis
- ⚠️ `greptile` - Referenced in system prompt but NOT in `.mcp.json`

**Skills**: None explicitly referenced

**Gap**: ⚠️ Greptile MCP missing from `.mcp.json` (only in system prompt)

**Validation**: ✅ Correctly mapped in `/autonomous-refactor` Phase 2 (Epic Execution Loop)

---

### Phase 4: Ticket Generation
**Command**: `/epic-tickets`  
**Mode**: `plan`  
**MCPs**: None (plan mode doesn't support MCPs)  
**Skills**: None

**Validation**: ✅ Correctly mapped in `/autonomous-refactor` Phase 2 (Epic Execution Loop)

---

### Phase 4.5: Ticket Review
**Command**: (No dedicated command - manual review)  
**Mode**: N/A  
**MCPs**: N/A  
**Skills**: None

**Gap**: ⚠️ No automated command for this phase

**Validation**: ⚠️ NOT mapped in `/autonomous-refactor` (manual gate)

---

### Phase 5: Ticket Execution
**Command**: `/epic-validate`  
**Mode**: `v12-engineer` (Bob CLI)  
**MCPs**: Bob CLI uses its own MCP configuration (external to `.mcp.json`)  
**Skills**:
- `plugins/parallel-epic-execution/SKILL.md` (implicit) - Local parallel execution
- `.bob/skills/gcp-vm-wave-execution/` (implicit) - VM execution

**Gap**: ⚠️ Should add explicit skill references in command file

**Validation**: ✅ Correctly mapped in `/autonomous-refactor` Phase 2 (Epic Execution Loop)

---

### Phase 5.V: Verification
**Command**: `/epic-verify-ticket`  
**Mode**: `advanced`  
**MCPs**:
- ✅ `jcodemunch-mcp` - Code verification, complexity checks
- ✅ `sequential-thinking` - Verification reasoning

**Skills**: None explicitly referenced

**Validation**: ✅ Correctly mapped in `/autonomous-refactor` Phase 2 (Epic Execution Loop)

---

### Phase 6: Final Review
**Command**: `/epic-review-final`  
**Mode**: `advanced`  
**MCPs**:
- ✅ `jcodemunch-mcp` - Final code health checks
- ✅ `sequential-thinking` - Review reasoning

**Skills**: None explicitly referenced

**Validation**: ✅ Correctly mapped in `/autonomous-refactor` Phase 3 (Final Verification)

---

## MCP Usage Summary

### Available MCPs (from `.mcp.json`)

1. **jcodemunch-mcp** (stdio)
   - **Used in**: Phase 3, 5.V, 6 (all `advanced` mode phases)
   - **Purpose**: Code analysis, symbol search, complexity checks, file navigation
   - **Tools**: 70+ tools for code exploration and analysis

2. **sequential-thinking** (stdio)
   - **Used in**: Phase 3, 5.V, 6 (all `advanced` mode phases)
   - **Purpose**: Complex reasoning, step-by-step analysis
   - **Tools**: `sequentialthinking` tool for dynamic problem-solving

### Missing MCPs

3. **greptile** (referenced in system prompt but NOT in `.mcp.json`)
   - **Should be used in**: Phase 3 (DNA & PR Audit)
   - **Purpose**: Custom context, merge requests, code reviews
   - **Status**: ⚠️ Configuration gap - need to add to `.mcp.json`

---

## Skill Usage Summary

### Explicitly Referenced Skills

1. **`plugins/architecture-validation/SKILL.md`**
   - **Used in**: Phase 2 (Architecture Planning)
   - **Status**: ✅ Properly referenced in command file

### Implicitly Used Skills

2. **`.bob/skills/gcp-vm-wave-execution/`**
   - **Used in**: Phase 0, 5 (VM parallel execution)
   - **Status**: ⚠️ Should add explicit references

3. **`.bob/skills/lamport-clock-recovery/`**
   - **Used in**: All phases (Lamport conflict resolution)
   - **Status**: ⚠️ Should add explicit references

4. **`plugins/scope-boundary-check/SKILL.md`**
   - **Used in**: Phase 1.5 (Scope validation)
   - **Status**: ⚠️ Should add explicit reference

5. **`plugins/parallel-epic-execution/SKILL.md`**
   - **Used in**: Phase 5 (Local parallel execution)
   - **Status**: ⚠️ Should add explicit reference

6. **`plugins/multi-agent-orchestrator/WAVE2_SHELL_WORKAROUND.md`**
   - **Used in**: Phase 0 (SSH file I/O)
   - **Status**: ⚠️ Should add explicit reference

---

## Mode Distribution

| Mode | Phases | MCP Support | Skill Support |
|------|--------|-------------|---------------|
| `ask` | Phase 0 | ❌ No | ✅ Yes |
| `plan` | Phase 1, 1.5, 2, 4 | ❌ No | ✅ Yes |
| `advanced` | Phase 3, 5.V, 6 | ✅ Yes | ✅ Yes |
| `v12-engineer` | Phase 5 | ✅ Yes (Bob CLI) | ✅ Yes |

**Key Insight**: Only `advanced` and `v12-engineer` modes support MCPs. This is why Phases 3, 5, 5.V, and 6 use `advanced` mode.

---

## Autonomous Refactor Command Validation

### Does `/autonomous-refactor` properly orchestrate all 10 phases?

**Analysis of `/autonomous-refactor` command structure**:

```
Phase 1: Initialize Session (advanced mode)
  ↓
Phase 2: Epic Execution Loop (orchestrator mode)
  ├─ Step A: Epic Status Report (orchestrator - no mode switch)
  ├─ Step B: Run Epic via /epic-run (orchestrator mode)
  │   └─ /epic-run internally calls:
  │       ├─ Phase 0: /epic-intake (ask mode)
  │       ├─ Phase 1: /epic-scope-boundary (plan mode)
  │       ├─ Phase 1.5: /epic-scope-boundary --phase 1.5 (plan mode)
  │       ├─ Phase 2: /epic-plan (plan mode)
  │       ├─ Phase 3: /epic-scan (advanced mode)
  │       ├─ Phase 4: /epic-tickets (plan mode)
  │       ├─ Phase 5: /epic-validate (v12-engineer mode)
  │       ├─ Phase 5.V: /epic-verify-ticket (advanced mode)
  │       └─ Phase 6: /epic-review-final (advanced mode) [partial]
  ├─ Step C: Verify Epic Completion (orchestrator - no mode switch)
  ├─ Step D: Update Progress Log (advanced mode)
  └─ Step E: Check Completion Criteria (advanced mode)
  ↓
Phase 3: Final Verification (advanced mode)
  ↓
Phase 4: Completion Handshake (orchestrator - no mode switch)
```

### Validation Results

✅ **Phase 0**: Mapped via `/epic-run` → `/epic-intake`  
✅ **Phase 1**: Mapped via `/epic-run` → `/epic-scope-boundary`  
✅ **Phase 1.5**: Mapped via `/epic-run` → `/epic-scope-boundary --phase 1.5`  
✅ **Phase 2**: Mapped via `/epic-run` → `/epic-plan`  
✅ **Phase 3**: Mapped via `/epic-run` → `/epic-scan`  
✅ **Phase 4**: Mapped via `/epic-run` → `/epic-tickets`  
⚠️ **Phase 4.5**: NOT mapped (manual review gate)  
✅ **Phase 5**: Mapped via `/epic-run` → `/epic-validate`  
✅ **Phase 5.V**: Mapped via `/epic-run` → `/epic-verify-ticket`  
✅ **Phase 6**: Mapped via `/epic-run` → `/epic-review-final` (partial) + Phase 3 (Final Verification)

**Conclusion**: 9 out of 10 phases are properly mapped. Phase 4.5 (Ticket Review) is intentionally manual.

---

## Integration Gaps & Recommendations

### Critical Gaps

1. **Greptile MCP Missing**
   - **Impact**: Phase 3 (DNA & PR Audit) cannot use Greptile tools
   - **Fix**: Add Greptile to `.mcp.json`
   - **Priority**: P1 (blocks PR audit features)

2. **Phase 4.5 Not Automated**
   - **Impact**: Manual review gate breaks autonomous flow
   - **Fix**: Create `/epic-review-tickets` command
   - **Priority**: P2 (workaround exists - Director review)

### Skill Reference Gaps

3. **Missing Explicit Skill References**
   - **Phases affected**: 0, 1.5, 5
   - **Impact**: Skills used but not documented in command files
   - **Fix**: Add explicit `@skill` references
   - **Priority**: P3 (documentation improvement)

### Recommendations

#### 1. Add Greptile MCP to `.mcp.json`

```json
{
  "mcpServers": {
    "jcodemunch-mcp": {
      "type": "stdio",
      "command": "jcodemunch-mcp.exe",
      "args": []
    },
    "greptile": {
      "type": "stdio",
      "command": "greptile-mcp",
      "args": []
    },
    "sequential-thinking": {
      "type": "stdio",
      "command": "npx.cmd",
      "args": ["-y", "@modelcontextprotocol/server-sequential-thinking"]
    }
  }
}
```

#### 2. Add Explicit Skill References

**Phase 0** (`.bob/commands/epic-intake.md`):
```markdown
**Skills Used**:
- @.bob/skills/gcp-vm-wave-execution/ - VM parallel execution
- @plugins/multi-agent-orchestrator/WAVE2_SHELL_WORKAROUND.md - SSH file I/O
```

**Phase 1.5** (`.bob/commands/epic-scope-boundary.md`):
```markdown
**Skills Used**:
- @plugins/scope-boundary-check/SKILL.md - Scope validation logic
```

**Phase 5** (`.bob/commands/epic-validate.md`):
```markdown
**Skills Used**:
- @plugins/parallel-epic-execution/SKILL.md - Local parallel execution
- @.bob/skills/gcp-vm-wave-execution/ - VM execution
```

#### 3. Create `/epic-review-tickets` Command

**Purpose**: Automate Phase 4.5 (Ticket Review)  
**Mode**: `advanced`  
**MCPs**: jcodemunch-mcp, sequential-thinking  
**Logic**:
- Load tickets from `04-tickets.md`
- Validate each ticket against Jane Street KB
- Check for scope creep
- Verify ticket independence
- Generate approval report

---

## Anthropic Feature Integration

### Extended Thinking (Claude 3.7 Sonnet)

**Recommended Phases**:
- ✅ **Phase 2** (Architecture Planning) - Complex design decisions
- ✅ **Phase 3** (DNA & PR Audit) - Multi-signal analysis
- ✅ **Phase 5** (Ticket Execution) - Refactoring strategy

**Implementation**:
```python
# Enable extended thinking for complex reasoning
response = client.messages.create(
    model="claude-3-7-sonnet-20250219",
    thinking={
        "type": "enabled",
        "budget_tokens": 10000
    },
    messages=[...]
)
```

### Prompt Caching (70-90% Cost Reduction)

**Recommended Caching**:
- ✅ **System Prompts**: V12 DNA, Jane Street KB, AGENTS.md
- ✅ **Per-Phase**: Phase-specific templates and examples
- ✅ **Knowledge Base**: Jane Street rules (299 rules, ~50k tokens)

**Implementation**:
```python
# Cache Jane Street KB for all phases
system_prompt = [
    {
        "type": "text",
        "text": jane_street_kb_content,
        "cache_control": {"type": "ephemeral"}
    }
]
```

**Cost Impact**: Estimated 70-90% reduction in API costs for Wave 7 (180 epics)

---

## Next Steps

### Immediate (Pre-Wave 7)

1. ✅ **Matrix Complete**: This document validates integration
2. ⏳ **Add Greptile MCP**: Update `.mcp.json` (P1)
3. ⏳ **Add Skill References**: Update command files (P3)
4. ⏳ **Test Integration**: Run pilot epic with full matrix

### Short-Term (Wave 7 Execution)

5. ⏳ **Enable Extended Thinking**: Phases 2, 3, 5
6. ⏳ **Enable Prompt Caching**: System prompts + Jane Street KB
7. ⏳ **Monitor MCP Usage**: Track jcodemunch vs greptile usage
8. ⏳ **Measure Cost Savings**: Compare with/without caching

### Long-Term (Post-Wave 7)

9. ⏳ **Create `/epic-review-tickets`**: Automate Phase 4.5
10. ⏳ **Consolidate Skills**: Merge VM + local parallel execution
11. ⏳ **Optimize MCP Calls**: Reduce redundant tool invocations
12. ⏳ **Document Patterns**: Best practices for MCP + skill integration

---

## Related Documentation

- **Skill Audit**: `docs/workflow/SKILL_AUDIT_10_PHASES.md`
- **Epic Workflow**: `docs/workflow/V12_EPIC_WORKFLOW_REFACTORING_DESIGN.md`
- **Autonomous Refactor**: `.bob/commands/autonomous-refactor.md`
- **MCP Configuration**: `.mcp.json`
- **GCP VM Skill**: `.bob/skills/gcp-vm-wave-execution/skill.md`
- **Lamport Recovery**: `.bob/skills/lamport-clock-recovery/skill.md`

---

**Document Status**: ✅ Complete  
**Validation**: 9/10 phases mapped, 2 MCPs active, 6 skills identified  
**Next Review**: After Wave 7 pilot epic