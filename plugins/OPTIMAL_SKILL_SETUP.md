# Optimal Skill Setup - V12 Autonomous Refactoring

**Date**: 2026-06-21  
**Purpose**: Complete skill integration plan with explicit references for all phases  
**Context**: Anthropic skills integration + explicit skill references in custom modes

---

## Executive Summary

**Current State**: 13 skills installed (2 Bob native, 11 plugins), mostly implicit references
**Target State**: 12 explicit skill references in all custom modes (corrected from 16 - removed 4 incorrect PR skills)
**Benefits**: Better discoverability, auto-loading, Anthropic spec compliance

**CRITICAL CORRECTION (V12.25)**: Phase 5.V and Phase 6 do NOT use `check-pr` or `pr-loop-auto` skills. The V12.25 manifest-based workflow removed the PR loop from the 10-phase workflow. See `SKILLS_AUDIT_V12_25_CORRECTED.md` for details.

---

## Anthropic Skills to Install

### 1. Skill Creator
**Source**: https://github.com/anthropics/skills/tree/main/skills/skill-creator  
**Install Location**: `.bob/skills/skill-creator/`  
**Purpose**: Create new skills using Anthropic's skill format  
**Use Cases**:
- Generate new skills for discovered patterns
- Convert existing plugins to Anthropic spec
- Rapid skill prototyping

**Installation**:
```bash
# Clone Anthropic skills repo
git clone https://github.com/anthropics/skills.git temp_anthropic_skills

# Copy skill-creator
cp -r temp_anthropic_skills/skills/skill-creator .bob/skills/

# Cleanup
rm -rf temp_anthropic_skills
```

### 2. Wrap Up Skill
**Source**: https://github.com/anthropics/launch-your-agent (one of 2 skills)  
**Install Location**: `.bob/skills/wrap-up/`  
**Purpose**: Session wrap-up and handoff protocol  
**Use Cases**:
- End of epic phase handoff
- Session summary generation
- Context preservation for next session

**Best Fit**: **Phase 6 (Final Review)** and **Phase 5.V (Verification)**

**Installation**:
```bash
# Clone launch-your-agent repo
git clone https://github.com/anthropics/launch-your-agent.git temp_launch_agent

# Copy wrap-up skill
cp -r temp_launch_agent/skills/wrap-up .bob/skills/

# Cleanup
rm -rf temp_launch_agent
```

### 3. Launch Agent Skill
**Source**: https://github.com/anthropics/launch-your-agent (second skill)  
**Install Location**: `.bob/skills/launch-agent/`  
**Purpose**: Agent initialization and context loading  
**Use Cases**:
- Start of epic workflow
- Phase 0 initialization
- Context restoration after interruption

**Best Fit**: **Phase 0 (Hotspot Analysis)** and **Autonomous Refactor orchestrator**

**Installation**:
```bash
# Already cloned above, just copy second skill
cp -r temp_launch_agent/skills/launch-agent .bob/skills/
```

---

## Complete Skills Inventory (After Installation)

### Bob IDE Skills (`.bob/skills/`) - Auto-Loaded

| # | Skill | Status | Purpose | Best Fit Phases |
|---|-------|--------|---------|-----------------|
| 1 | `gcp-vm-wave-execution` | ✅ Installed | VM parallel execution | Phase 0, 5 |
| 2 | `lamport-clock-recovery` | ✅ Installed | Conflict resolution | All phases (implicit) |
| 3 | `skill-creator` | ❌ **TO INSTALL** | Create new skills | Meta (skill development) |
| 4 | `wrap-up` | ❌ **TO INSTALL** | Session handoff | Phase 5.V, 6 |
| 5 | `launch-agent` | ❌ **TO INSTALL** | Agent initialization | Phase 0, Orchestrator |

### Custom Plugins (`plugins/`) - Need Explicit References

| # | Plugin | Status | Purpose | Best Fit Phases |
|---|--------|--------|---------|-----------------|
| 6 | `architecture-validation` | ✅ Installed | Architectural validation | Phase 2 |
| 7 | `scope-boundary-check` | ✅ Installed | Scope validation | Phase 1.5 |
| 8 | `parallel-epic-execution` | ✅ Installed | Local worktrees | Phase 5 (alternative) |
| 9 | `multi-agent-orchestrator` | ⚠️ POC | Sub-agent pattern | Phase 5 (future) |
| 10 | `WAVE2_SHELL_WORKAROUND` | ✅ Installed | SSH file I/O | Phase 0 (VM) |
| 11 | `check-pr` | ✅ Installed | PR polling | Phase 5.V, 6 |
| 12 | `pr-loop-auto` | ✅ Installed | Autonomous PR loop | Phase 5.V, 6 |
| 13 | `bobcoin-account-switch` | ✅ Installed | Account management | Orchestrator |
| 14 | `codebase-architecture` | ✅ Installed | Architecture analysis | Phase 2 |
| 15 | `frontend-design` | ✅ Installed | Frontend patterns | N/A (not used in V12) |
| 16 | `github-migration` | ✅ Installed | GitHub utilities | N/A (not used in V12) |

---

## Optimal Skill References by Phase

### Phase 0: Hotspot Analysis
**Custom Mode**: `v12-phase0-hotspot`  
**Explicit Skills**:
```yaml
skills:
  - "@.bob/skills/launch-agent"           # NEW - Agent initialization
  - "@.bob/skills/gcp-vm-wave-execution"  # VM parallel execution
  - "@plugins/multi-agent-orchestrator/WAVE2_SHELL_WORKAROUND.md"  # SSH workaround
```

**Implicit Skills** (auto-loaded):
- `lamport-clock-recovery` (conflict resolution)

---

### Phase 1: Scope Definition
**Custom Mode**: `v12-phase1-scope`  
**Explicit Skills**: None (pure jCodemunch analysis)

**Implicit Skills**:
- `lamport-clock-recovery`

---

### Phase 1.5: Scope Boundary Validation
**Custom Mode**: `v12-phase1-5-boundary`  
**Explicit Skills**:
```yaml
skills:
  - "@plugins/scope-boundary-check/SKILL.md"  # Scope validation
```

**Implicit Skills**:
- `lamport-clock-recovery`

---

### Phase 2: Architecture Planning
**Custom Mode**: `v12-phase2-architecture`  
**Explicit Skills**:
```yaml
skills:
  - "@plugins/architecture-validation/SKILL.md"  # ALREADY EXPLICIT
  - "@plugins/codebase-architecture/SKILL.md"    # NEW - Architecture analysis
```

**Implicit Skills**:
- `lamport-clock-recovery`

---

### Phase 3: DNA & PR Audit
**Custom Mode**: `v12-phase3-audit`  
**Explicit Skills**: None (pure jCodemunch + sequential-thinking)

**Implicit Skills**:
- `lamport-clock-recovery`

---

### Phase 4: Ticket Generation
**Custom Mode**: `v12-phase4-tickets`  
**Explicit Skills**: None (pure jCodemunch analysis)

**Implicit Skills**:
- `lamport-clock-recovery`

---

### Phase 4.5: Ticket Review
**Custom Mode**: `v12-phase4-5-review`  
**Explicit Skills**: None (Sequential Thinking MCP only)

**Implicit Skills**:
- `lamport-clock-recovery`

---

### Phase 5: Ticket Execution
**Custom Mode**: `v12-engineer`  
**Explicit Skills**:
```yaml
skills:
  - "@.bob/skills/gcp-vm-wave-execution"         # VM execution
  - "@plugins/parallel-epic-execution/SKILL.md"  # Local alternative
```

**Implicit Skills**:
- `lamport-clock-recovery`

---

### Phase 5.V: Verification
**Custom Mode**: `v12-phase5-v-verify`
**Explicit Skills**:
```yaml
skills:
  - "@.bob/skills/wrap-up"              # Session handoff for next ticket
```

**Rationale**: V12.25 manifest-based workflow removed PR loop from 10-phase workflow. Verification is artifact-based (manifest.json), not PR-based.

**Implicit Skills**:
- `lamport-clock-recovery`

---

### Phase 6: Final Review
**Custom Mode**: `v12-phase6-review`
**Explicit Skills**:
```yaml
skills:
  - "@.bob/skills/wrap-up"              # Session handoff for post-epic work
```

**Rationale**: V12.25 manifest-based workflow removed PR loop from 10-phase workflow. Final review is manifest-based (05-completion-report.md), not PR-based. PR submission happens AFTER Phase 6 completion (outside the 10-phase workflow).

**Implicit Skills**:
- `lamport-clock-recovery`

---

### Autonomous Refactor Orchestrator
**Custom Mode**: `autonomous-refactor`  
**Explicit Skills**:
```yaml
skills:
  - "@.bob/skills/launch-agent"                  # NEW - Orchestrator initialization
  - "@.bob/skills/gcp-vm-wave-execution"         # Wave orchestration
  - "@.bob/skills/wrap-up"                       # NEW - Wave completion handoff
  - "@plugins/bobcoin-account-switch/SKILL.md"   # Account management
```

**Implicit Skills**:
- `lamport-clock-recovery`

---

## Implementation Plan

### Step 1: Install Anthropic Skills (30 minutes)

```bash
# 1. Clone repositories
git clone https://github.com/anthropics/skills.git temp_anthropic_skills
git clone https://github.com/anthropics/launch-your-agent.git temp_launch_agent

# 2. Copy skills to .bob/skills/
cp -r temp_anthropic_skills/skills/skill-creator .bob/skills/
cp -r temp_launch_agent/skills/wrap-up .bob/skills/
cp -r temp_launch_agent/skills/launch-agent .bob/skills/

# 3. Verify installation
ls -la .bob/skills/
# Should show: gcp-vm-wave-execution, lamport-clock-recovery, skill-creator, wrap-up, launch-agent

# 4. Cleanup
rm -rf temp_anthropic_skills temp_launch_agent
```

### Step 2: Update Custom Modes (1 hour)

Edit `.bob/custom_modes.yaml` to add explicit skill references:

**Example for Phase 0**:
```yaml
- slug: v12-phase0-hotspot
  name: V12 Phase 0 Hotspot Analyzer
  roleDefinition: |
    You are the V12 Phase 0 Hotspot Analyzer...
  skills:
    - "@.bob/skills/launch-agent"
    - "@.bob/skills/gcp-vm-wave-execution"
    - "@plugins/multi-agent-orchestrator/WAVE2_SHELL_WORKAROUND.md"
  groups:
    - read
    - edit
    - command
    - mcp
```

**Repeat for all 10 phases + autonomous-refactor mode.**

### Step 3: Update Integration Matrix (30 minutes)

Update `docs/workflow/AUTONOMOUS_REFACTOR_INTEGRATION_MATRIX_V2.md`:
- Change "Skills Used" column to show explicit references
- Add new Anthropic skills to skill summary section
- Update skill count from 6 to 9 explicit skills

### Step 4: Test Integration (1 hour)

```bash
# Test Phase 0 with new skills
/epic-intake EPIC-TEST-001 "Test skill integration"

# Verify skills loaded
# Check Bob IDE logs for skill loading messages

# Test wrap-up skill in Phase 6
/epic-review-final EPIC-TEST-001

# Verify session handoff works correctly
```

### Step 5: Document Skill Usage (30 minutes)

Create skill usage examples in each phase's documentation:
- `docs/brain/EPIC-TEST-001/00-hotspots.md` - Show launch-agent usage
- `docs/brain/EPIC-TEST-001/05-completion-report.md` - Show wrap-up usage

---

## Benefits of Explicit Skill References

### 1. Discoverability
- Agents can see which skills are available in current mode
- Easier to understand phase capabilities
- Better documentation for new developers

### 2. Auto-Loading
- Bob IDE loads skills automatically when mode activated
- No need to manually invoke skills
- Consistent skill availability across sessions

### 3. Anthropic Spec Compliance
- Follows https://agentskills.io/specification
- Compatible with Anthropic's skill ecosystem
- Easier to share skills with community

### 4. Version Control
- Explicit references make skill dependencies clear
- Easier to track which skills are used where
- Better change management

### 5. Error Prevention
- Missing skills detected at mode activation
- Clear error messages if skill not found
- Prevents runtime skill invocation failures

---

## Migration Path: Implicit → Explicit

### Current State (Implicit)
```yaml
# Phase 1.5 custom mode (current)
- slug: v12-phase1-5-boundary
  name: V12 Phase 1.5 Boundary Validator
  roleDefinition: |
    You validate scope boundaries using scope-boundary-check skill.
  # No explicit skills field
```

**Problem**: Agent must know to invoke `scope-boundary-check` skill manually.

### Target State (Explicit)
```yaml
# Phase 1.5 custom mode (target)
- slug: v12-phase1-5-boundary
  name: V12 Phase 1.5 Boundary Validator
  roleDefinition: |
    You validate scope boundaries using scope-boundary-check skill.
  skills:
    - "@plugins/scope-boundary-check/SKILL.md"
```

**Benefit**: Bob IDE auto-loads skill, agent has immediate access.

---

## Anthropic Skills Spec Compliance

### Current Format (Custom)
```markdown
name: architecture-validation
description: Systematic architectural validation...

---

# Architecture Validation Skill
...
```

### Anthropic Spec Format
```yaml
---
name: architecture-validation
description: Systematic architectural validation using jCodemunch tools
version: 1.0.0
author: V12 Team
tags: [architecture, validation, jcodemunch]
---

# Architecture Validation Skill
...
```

**Migration**: Use `skill-creator` skill to convert existing plugins to Anthropic spec.

---

## Skills.sh Integration (Future)

### Matt Pocock's Architecture Skill
**URL**: https://www.skills.sh/mattpocock/skills/improve-codebase-architecture  
**Status**: Not installed (similar to our `architecture-validation`)  
**Recommendation**: Evaluate for enhancements, but keep our custom implementation

### Anthropic's Architecture Skill
**URL**: https://www.skills.sh/anthropics/knowledge-work-plugins/architecture  
**Status**: Not installed  
**Recommendation**: Review and potentially merge with our implementation

---

## Post-Installation Validation

### Checklist

- [ ] All 3 Anthropic skills installed in `.bob/skills/`
- [ ] `.bob/custom_modes.yaml` updated with explicit skill references
- [ ] Integration matrix updated (V2.3)
- [ ] Test epic completed successfully with new skills
- [ ] Skill loading verified in Bob IDE logs
- [ ] Wrap-up skill tested in Phase 6
- [ ] Launch-agent skill tested in Phase 0
- [ ] Documentation updated

### Verification Commands

```bash
# 1. Check skill installation
ls -la .bob/skills/
# Expected: 5 directories (gcp-vm-wave-execution, lamport-clock-recovery, skill-creator, wrap-up, launch-agent)

# 2. Verify custom modes have skill references
grep -A 5 "skills:" .bob/custom_modes.yaml | head -20

# 3. Test skill loading
bob --mode v12-phase0-hotspot --dry-run
# Should show: "Loaded skills: launch-agent, gcp-vm-wave-execution, WAVE2_SHELL_WORKAROUND"

# 4. Run test epic
/epic-intake EPIC-TEST-001 "Skill integration test"
```

---

## Related Documentation

- **Skills Inventory**: `plugins/SKILLS_INVENTORY.md`
- **Integration Matrix**: `docs/workflow/AUTONOMOUS_REFACTOR_INTEGRATION_MATRIX_V2.md`
- **Skill Relationships**: `plugins/SKILL_RELATIONSHIPS.md`
- **Anthropic Skills**: https://github.com/anthropics/skills
- **Launch Your Agent**: https://github.com/anthropics/launch-your-agent
- **Agent Skills Spec**: https://agentskills.io/specification

---

**Document Status**: ✅ Complete  
**Next Steps**: Install Anthropic skills, update custom modes, test integration  
**Estimated Time**: 3 hours total  
**Priority**: P1 (enhances autonomous workflow)