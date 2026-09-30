# Phase Mode & MCP Tool Protocol (V12.53)

**Version**: V12.53  
**Effective**: 2026-06-17  
**Status**: MANDATORY - BLOCKING GATE

## Core Principle: Deterministic Phase Execution

**ALL phases use custom modes** to achieve **deterministic execution** through:

1. **Instruction Layer**: Each custom mode is a layer of phase-specific instructions
2. **Context Focus**: Custom mode enforces what the agent should focus on for that phase
3. **Repeatability**: Same inputs → same outputs (no agent drift)
4. **Enforcement**: MCP tools, agent tracking, output format all mandated in custom mode

**Rationale**: Base modes (ask/plan/advanced) are too generic. Custom modes provide **phase-specific guardrails** that ensure consistent behavior across 80+ parallel executions.

## Custom Mode Mapping (ALL 10 Phases)

| Phase | Custom Mode | Context Focus | MCP Tools Required |
|-------|-------------|---------------|-------------------|
| **0** | `v12-phase0-hotspot` | Complexity + churn analysis | jCodemunch, Sequential Thinking |
| **1** | `v12-phase1-scope` | Single-method boundary definition | jCodemunch, Sequential Thinking |
| **1.5** | `v12-phase1-5-boundary` | Scope creep prevention gate | jCodemunch, Sequential Thinking |
| **2** | `v12-phase2-architecture` | Extraction plan + method signatures | jCodemunch, Sequential Thinking, Graphify |
| **3** | `v12-phase3-audit` | V12 DNA + PR hygiene compliance | jCodemunch, Sequential Thinking, Greptile |
| **4** | `v12-phase4-tickets` | Surgical ticket breakdown | jCodemunch, Sequential Thinking |
| **4.5** | `v12-phase4-5-review` | Jane Street validation gate | Sequential Thinking |
| **5** | `v12-engineer` | Surgical refactoring execution | jCodemunch, Sequential Thinking |
| **5.V** | `v12-phase5-v-verify` | Complexity + scope + test verification | jCodemunch, Sequential Thinking, Greptile |
| **6** | `v12-phase6-review` | Epic completion + PR readiness | jCodemunch, Sequential Thinking, Greptile |

## Why Custom Modes for Every Phase?

### Problem with Base Modes

**Base modes are too generic**:
- `ask` mode: No enforcement of output format, MCP tool usage, or agent tracking
- `plan` mode: No phase-specific validation rules
- `advanced` mode: Too many capabilities, no focus

**Result**: Agent drift across 80+ parallel executions → inconsistent outputs, missing data, validation failures

### Solution: Custom Mode as Instruction Layer

**Each custom mode is a deterministic instruction layer**:

```yaml
- slug: v12-phase0-hotspot
  roleDefinition: >
    You are the V12 Hotspot Analyzer for Phase 0.
    
    CONTEXT FOCUS: Complexity + churn analysis ONLY
    
    YOUR ONLY JOB:
    1. Use jCodemunch to get hotspots (get_hotspots tool)
    2. Use Sequential Thinking to analyze top 20 methods
    3. Write 00-hotspots.md with EXACT format (see template)
    4. Write manifest.json with phase metadata
    5. Include Agent Tracking section (MANDATORY)
    
    DO NOT:
    - Analyze scope (that's Phase 1)
    - Design architecture (that's Phase 2)
    - Generate tickets (that's Phase 4)
    
    MANDATORY MCP TOOLS:
    - jCodemunch: get_hotspots, get_symbol_complexity
    - Sequential Thinking: sequentialthinking
```

**Benefits**:
- **Deterministic**: Same hotspot data → same analysis → same output format
- **Focused**: Agent can't drift into Phase 1/2/4 concerns
- **Enforceable**: Custom rules block execution if MCP tools missing
- **Trackable**: Agent tracking mandatory in custom mode definition

## MCP Tool Requirements

### Sequential Thinking (MANDATORY - 10/10 phases)

**Purpose**: Break down complex reasoning into explicit, verifiable steps

**Why mandatory**: Ensures agent shows its work → reviewable decision process

**Example**:
```xml
<use_mcp_tool>
<server_name>sequential-thinking</server_name>
<tool_name>sequentialthinking</tool_name>
<arguments>
{
  "thought": "Step 1: Analyzing scope boundary - is this single method or multiple?",
  "nextThoughtNeeded": true,
  "thoughtNumber": 1,
  "totalThoughts": 5
}
</arguments>
</use_mcp_tool>
```

### jCodemunch (MANDATORY - 9/10 phases, except 4.5)

**Purpose**: Ground all analysis in live code reality (no assumptions)

**Why mandatory**: Prevents hallucination → agent must verify code structure before making decisions

**Key Tools by Phase**:
- **Phase 0**: `get_hotspots`, `get_symbol_complexity`
- **Phase 1**: `get_file_outline`, `find_references`
- **Phase 1.5**: `get_symbol_source`, `get_blast_radius`
- **Phase 2**: `get_context_bundle`, `get_call_hierarchy`
- **Phase 3**: `search_ast`, `get_layer_violations`
- **Phase 4**: `get_symbol_complexity`, `get_extraction_candidates`
- **Phase 5**: `get_symbol_source`, `plan_refactoring`
- **Phase 5.V**: `get_symbol_complexity`, `get_changed_symbols`
- **Phase 6**: `get_repo_health`, `get_hotspots`

### Graphify (MANDATORY - Phase 2 only)

**Purpose**: Visualize codebase structure for architecture planning

**Why Phase 2 only**: Architecture phase needs module relationship understanding

### Greptile (MANDATORY - Phases 3, 5.V, 6)

**Purpose**: PR hygiene checks, code quality audit

**Why these phases**: Audit (3), verification (5.V), and final review (6) need PR compliance checks

## Agent Tracking (MANDATORY)

**Every phase output MUST include**:

```markdown
## Agent Tracking

- **Agent Name**: v12-phase0-hotspot
- **Bobcoins Used**: 2.5 (Balance: 157.5)
- **API Key**: bob.json
- **Execution Time**: 3.2 minutes
```

**Why mandatory**: Enables cost tracking, performance analysis, and agent accountability

## Enforcement in epic_manifest.py

**Updated CUSTOM_MODES** (lines 84-98):
```python
CUSTOM_MODES = {
    "v12-phase0-hotspot",      # Phase 0: Hotspot Analysis
    "v12-phase1-scope",        # Phase 1: Scope Definition
    "v12-phase1-5-boundary",   # Phase 1.5: Scope Boundary Validation
    "v12-phase2-architecture", # Phase 2: Architecture Planning
    "v12-phase3-audit",        # Phase 3: DNA & PR Audit
    "v12-phase4-tickets",      # Phase 4: Ticket Generation
    "v12-phase4-5-review",     # Phase 4.5: Ticket Review (Jane Street)
    "v12-engineer",            # Phase 5: Ticket Execution
    "v12-phase5-v-verify",     # Phase 5.V: Verification
    "v12-phase6-review",       # Phase 6: Final Review
    "v12-epic-planner",        # Interactive epic planning (not wave execution)
    "v12-phase7-lead",         # Concurrency engineering (not wave execution)
    "autonomous-refactor"      # Wave orchestration (not phase execution)
}
```

**Validation** (load_manifest function):
- Checks `mode` field exists for every phase
- Validates mode is in `ALL_VALID_MODES`
- Checks `mcp_tools` field exists for every phase
- **BLOCKER**: Missing/invalid mode or mcp_tools → `ValidationError` → execution blocked

## Manifest Schema (V12.53)

**Per-Phase Fields**:
```json
{
  "0": {
    "status": "pending",
    "mode": "v12-phase0-hotspot",
    "mcp_tools": ["jcodemunch-mcp", "sequential-thinking"],
    "dependencies": [],
    "outputs": [],
    "created_at": "2026-06-17T00:00:00Z"
  }
}
```

## Custom Mode Structure

**Location**: `.bob/custom_modes.yaml`

**Template**:
```yaml
- slug: v12-phase{N}-{name}
  name: V12 Phase {N} {Title}
  roleDefinition: >
    CONTEXT FOCUS: {What this phase focuses on}
    
    YOUR ONLY JOB:
    1. {Step 1}
    2. {Step 2}
    3. {Step 3}
    
    DO NOT:
    - {Out of scope concern 1}
    - {Out of scope concern 2}
    
    MANDATORY MCP TOOLS:
    - {Tool 1}: {Purpose}
    - {Tool 2}: {Purpose}
    
    Agent Tracking (MANDATORY): Include in output
  whenToUse: Phase {N} ({Name}) of V12 epic workflows
  groups:
    - read
    - edit (restricted to .md, .json, .yaml, .yml, .txt)
    - command
    - mcp
  customRules:
    - mcpMandatory: |
        BLOCKER: {MCP tools} are MANDATORY.
        If unavailable, STOP and report error.
    - agentTracking: |
        MANDATORY: Include Agent Tracking section.
```

## Wave Execution Scripts

**Bob CLI Command Pattern**:
```bash
bob --yolo --chat-mode v12-phase0-hotspot "$(cat /tmp/phase0_msg_001.txt)"
```

**Key Points**:
- `--chat-mode` specifies custom mode slug
- Custom mode enforces phase-specific context focus
- MCP tools enforced by custom mode rules
- Agent tracking enforced by custom mode rules

## Migration from Base Modes (DEPRECATED)

**Old Pattern** (Wave 4 - DEPRECATED):
```bash
bob --yolo --chat-mode ask "..."      # Too generic, no enforcement
bob --yolo --chat-mode plan "..."     # Too generic, no enforcement
bob --yolo --chat-mode advanced "..." # Too generic, no enforcement
```

**New Pattern** (Wave 6 - CURRENT):
```bash
bob --yolo --chat-mode v12-phase0-hotspot "..."      # Deterministic, enforced
bob --yolo --chat-mode v12-phase1-scope "..."        # Deterministic, enforced
bob --yolo --chat-mode v12-phase3-audit "..."        # Deterministic, enforced
```

**Why the change**:
- **Determinism**: Custom modes = instruction layers → consistent behavior
- **Focus**: Each phase has clear context boundaries
- **Enforcement**: MCP tools + agent tracking mandatory
- **Repeatability**: Same inputs → same outputs (no drift)

## Verification Checklist

Before launching any wave:

- [ ] All 10 custom modes exist in `.bob/custom_modes.yaml` (local + VM)
- [ ] `epic_manifest.py` CUSTOM_MODES includes all 10 phase modes
- [ ] All manifests have `mode` field for every phase
- [ ] All manifests have `mcp_tools` field for every phase
- [ ] MCP servers running: jCodemunch, Sequential Thinking, Graphify, Greptile
- [ ] Pilot test verifies custom mode + MCP tools work
- [ ] Agent tracking present in pilot test output

## Troubleshooting

### Error: "Invalid mode 'ask' for phase 0"

**Cause**: Manifest still using base mode instead of custom mode

**Fix**:
```bash
python scripts/wave6/fix_all_manifest_modes.py
```

### Error: "MCP tool 'jcodemunch-mcp' not available"

**Cause**: MCP server not running or not configured

**Fix**:
1. Check `.mcp.json` or `.mcp.json.vm` for server config
2. Verify server binary exists and is executable
3. Test server: `bob --list-mcp-tools`

### Error: "Agent tracking missing in output"

**Cause**: Custom mode didn't enforce agent tracking requirement

**Fix**:
1. Update custom mode `customRules` in `.bob/custom_modes.yaml`
2. Add `agentTracking` rule with MANDATORY enforcement
3. Regenerate phase scripts with updated template

## References

- **Custom Modes**: `.bob/custom_modes.yaml`
- **Validation**: `scripts/epic_manifest.py` (lines 79-87, 253-305)
- **Manifest Schema**: `docs/workflow/V12_EPIC_WORKFLOW_10_PHASE_SOP.md`
- **MCP Servers**: `.mcp.json` (local), `.mcp.json.vm` (VM)
- **Wave Execution**: `.bob/skills/gcp-vm-wave-execution/skill.md`

## Version History

- **V12.53** (2026-06-17): ALL 10 phases now use custom modes for deterministic execution
- **V12.52** (2026-06-16): Lamport Causal Verification added
- **V12.40** (2026-06-15): VM environment hardened
- **V12.39** (2026-06-14): Skill Reading Mandate