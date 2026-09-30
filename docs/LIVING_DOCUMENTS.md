# Living Documents Tracker

This file tracks all "living documents" in the repository - documents that are actively maintained and serve as authoritative sources of truth for workflows, protocols, and standards.

## Workflow Documents

### Integration Matrix V2 (AUTHORITATIVE)
- **File**: `docs/workflow/AUTONOMOUS_REFACTOR_INTEGRATION_MATRIX_V2.md`
- **Purpose**: Authoritative source for custom mode selection in autonomous refactoring workflows
- **Status**: Active (V2.0)
- **Last Updated**: 2026-06-24
- **Update Frequency**: As workflow evolves
- **Owner**: Autonomous Refactor Agent
- **Critical For**: Wave execution, phase script generation, custom mode selection

**What It Defines**:
- Custom mode for each phase (0, 1, 1.5, 2, 3, 4, 4.5, 5, 5.V, 6)
- Required MCPs per phase (jcodemunch, sequential-thinking, graphify)
- Required skills per phase
- Output files per phase
- Agent tracking requirements

**When to Consult**:
- BEFORE generating any phase script
- BEFORE executing any phase
- AFTER phase completion (validation)

**Related Documents**:
- `docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md` (script generation mechanics)
- `.bob/custom_modes.yaml` (custom mode definitions)
- `scripts/validate_phase_compliance.py` (validation tool)

### Wave Phase Script Generation SOP V3
- **File**: `docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md`
- **Purpose**: Standard operating procedure for generating phase scripts using Building-Blocks Method
- **Status**: Active (V3.11)
- **Last Updated**: 2026-06-24
- **Update Frequency**: As lessons learned from wave execution
- **Owner**: Autonomous Refactor Agent

**What It Defines**:
- Building-Blocks Method (copy from previous wave)
- Bob CLI invocation pattern (temp file + command substitution)
- VM vs Local script differences
- Lamport clock event logging
- Pre-generation validation checklist (NEW in V3.11)

### Cost-Optimized Polling Protocol
- **File**: `docs/protocol/COST_OPTIMIZED_POLLING_PROTOCOL.md`
- **Purpose**: 4-minute polling intervals for 88% cost reduction
- **Status**: Active (V1.0)
- **Last Updated**: 2026-06-14
- **Update Frequency**: As cost optimization strategies evolve
- **Owner**: Autonomous Refactor Agent

## Protocol Documents

### Recovery Loop Protocol
- **File**: `docs/protocol/RECOVERY_LOOP_PROTOCOL.md`
- **Purpose**: Failed epic recovery and retry procedures
- **Status**: Active (V1.1)
- **Last Updated**: 2026-06-01
- **Update Frequency**: As recovery patterns emerge
- **Owner**: Autonomous Refactor Agent

### Branch Strategy Enforcement
- **File**: `docs/protocol/BRANCH_STRATEGY_ENFORCEMENT.md`
- **Purpose**: GitButler virtual branches mandate
- **Status**: Active (V1.0)
- **Last Updated**: 2026-05-25
- **Update Frequency**: As branch strategy evolves
- **Owner**: All Agents

### Test Framework Protocol
- **File**: `docs/protocol/TEST_FRAMEWORK_PROTOCOL.md`
- **Purpose**: xUnit-only mandate (NEVER NUnit/MSTest)
- **Status**: Active (V1.0)
- **Last Updated**: 2026-06-14
- **Update Frequency**: As test framework requirements evolve
- **Owner**: All Agents

## Configuration Files

### Custom Modes Configuration
- **File**: `.bob/custom_modes.yaml`
- **Purpose**: Defines all 11 custom modes for Bob CLI
- **Status**: Active
- **Last Updated**: 2026-06-24
- **Update Frequency**: When new phases or modes added
- **Owner**: Autonomous Refactor Agent

**Recent Updates**:
- Added Integration Matrix V2 reference to all 11 modes (2026-06-24)
- Ensures agents consult Integration Matrix before ANY action

### GCP VM Wave Execution Skill
- **File**: `.bob/skills/gcp-vm-wave-execution/skill.md`
- **Purpose**: VM-based wave execution procedures
- **Status**: Active (V2.6)
- **Last Updated**: 2026-06-24
- **Update Frequency**: As VM execution patterns evolve
- **Owner**: Autonomous Refactor Agent

**Recent Updates**:
- Added Post-Phase Validation section (V2.6, 2026-06-24)
- Mandates validation after EVERY phase completion

## Validation Tools

### Phase Compliance Validator
- **File**: `scripts/validate_phase_compliance.py`
- **Purpose**: Validates phase execution against Integration Matrix V2
- **Status**: Active (V1.0)
- **Last Updated**: 2026-06-24
- **Update Frequency**: As validation requirements evolve
- **Owner**: Autonomous Refactor Agent

**What It Validates**:
- Correct custom mode used
- Required output files exist
- Manifest updated correctly
- Lamport events logged
- MCP usage evidence (heuristic)

## Update Protocol

When updating a living document:

1. **Update the document** with new content
2. **Update this tracker** with:
   - New "Last Updated" date
   - Version increment (if applicable)
   - Brief description of changes
3. **Notify dependent systems**:
   - Update custom modes if workflow changes
   - Update validation scripts if requirements change
   - Update skills if procedures change
4. **Test changes**:
   - Run validation scripts
   - Test with pilot epic
   - Document any issues

## Version History

### 2026-06-24: Protocol Updates to Prevent Workflow Violations
- Added Integration Matrix V2 as living document
- Updated all 11 custom modes with Integration Matrix reference
- Created `validate_phase_compliance.py` validation tool
- Updated SOP V3 with Step 1.5 (Integration Matrix validation)
- Updated GCP VM skill with Post-Phase Validation (V2.6)
- **Root Cause**: 7 waves of false starts due to copying workflow from building-blocks instead of Integration Matrix
- **Solution**: Make Integration Matrix V2 the authoritative source, referenced in all relevant files

### 2026-06-14: Wave 7 Fresh Start
- Reset Lamport clock
- Updated complexity audit baseline (180 methods)
- Established CodeScene ≤8 target (Jane Street strict standard)

### 2026-06-01: 100% Completion Mandate
- Updated Recovery Loop Protocol (V1.1)
- Established N/N (100%) completion requirement
- No epic dismissals without Director approval