# Skills Inventory - Universal OR Strategy

**Date**: 2026-06-21  
**Purpose**: Complete inventory of installed skills and integration status  
**Context**: Skills vs Plugins distinction, Anthropic Skills integration assessment

---

## Skills vs Plugins: The Distinction

### `.bob/skills/` (Bob IDE Native Skills)
**Location**: `.bob/skills/`  
**Format**: Bob IDE skill format (YAML frontmatter + markdown)  
**Integration**: Loaded automatically by Bob IDE  
**Current Count**: 2 skills

### `plugins/` (Custom V12 Skills)
**Location**: `plugins/`  
**Format**: Custom markdown format (no YAML frontmatter)  
**Integration**: Referenced in custom modes, not auto-loaded  
**Current Count**: 11 plugins

**Key Difference**: Bob skills are auto-discovered and loaded by Bob IDE. Plugins are manually referenced in custom mode definitions or workflow documentation.

---

## Installed Skills Inventory

### Bob IDE Skills (`.bob/skills/`)

#### 1. GCP VM Wave Execution
**File**: `.bob/skills/gcp-vm-wave-execution/skill.md`  
**Purpose**: Execute wave-based autonomous refactoring on GCP VM  
**Status**: ✅ Active (used in Phases 0, 5)  
**Integration**: Referenced in `v12-phase0-hotspot` and `v12-engineer` custom modes  
**Key Features**:
- VM parallel execution (9 epics simultaneously)
- Lamport clock event tracking
- Bobcoin usage monitoring
- SSH file persistence workaround
- Sequential Thinking MCP integration

#### 2. Lamport Clock Recovery
**File**: `.bob/skills/lamport-clock-recovery/skill.md`  
**Purpose**: Diagnose and repair Lamport clock non-determinism errors  
**Status**: ✅ Active (used across all phases)  
**Integration**: Implicit (used when conflicts detected)  
**Key Features**:
- Automatic conflict detection
- Manifest consistency checks
- Event log backup and cleanup
- State hash mismatch resolution

---

### Custom Plugins (`plugins/`)

#### 3. Architecture Validation
**File**: `plugins/architecture-validation/SKILL.md`  
**Purpose**: Systematic architectural validation using jCodemunch tools  
**Status**: ✅ Active (Phase 2)  
**Integration**: Explicitly referenced in `v12-phase2-architecture` custom mode  
**Source**: Matt Pocock's "Architectural Improvement Methodology"  
**Key Features**:
- Dependency cycle detection
- Coupling metrics (Ca/Ce/Instability)
- Layer violation checks
- Blast radius analysis
- Interface contract validation

**Note**: This is the architecture skill you asked about - it's from Matt Pocock, not the Anthropic or skills.sh versions.

#### 4. Scope Boundary Check
**File**: `plugins/scope-boundary-check/SKILL.md`  
**Purpose**: Validate single-method scope, prevent scope creep  
**Status**: ✅ Active (Phase 1.5)  
**Integration**: Implicit (used in `v12-phase1-5-boundary` mode)  
**Key Features**:
- Single-method validation
- Scope creep detection
- BLOCKER enforcement

#### 5. Parallel Epic Execution
**File**: `plugins/parallel-epic-execution/SKILL.md`  
**Purpose**: Execute 3 epics simultaneously using Git worktrees  
**Status**: ✅ Active (Phase 5 alternative)  
**Integration**: Implicit (local execution alternative to VM)  
**Key Features**:
- 3 Git worktrees (epic-cluster-1/2/3)
- File-based clustering
- Batch F5 verification
- 64% time savings

#### 6. Multi-Agent Orchestrator
**File**: `plugins/multi-agent-orchestrator/SKILL.md`  
**Purpose**: Single Bob session spawns specialized sub-agents  
**Status**: ⚠️ POC testing (sub-agent support unknown)  
**Integration**: Not yet integrated  
**Key Features**:
- Sub-agent pattern
- Phase specialization
- Artifact-based communication

#### 7. WAVE2 Shell Workaround
**File**: `plugins/multi-agent-orchestrator/WAVE2_SHELL_WORKAROUND.md`  
**Purpose**: SSH file I/O workaround for `read_file` bug  
**Status**: ✅ Active (Phase 0 VM execution)  
**Integration**: Implicit (used in `gcp-vm-wave-execution` skill)  
**Key Features**:
- Shell command fallback (`cat`, `ls`, `wc -l`)
- Non-interactive mode compatibility

#### 8. Bobcoin Account Switch
**File**: `plugins/bobcoin-account-switch/`  
**Purpose**: Switch between bobcoin accounts  
**Status**: ⚠️ Unknown (not documented in integration matrix)

#### 9. Check PR
**File**: `plugins/check-pr/`  
**Purpose**: PR validation checks  
**Status**: ⚠️ Unknown (not documented in integration matrix)

#### 10. Codebase Architecture
**File**: `plugins/codebase-architecture/`  
**Purpose**: Codebase architecture analysis  
**Status**: ⚠️ Unknown (not documented in integration matrix)

#### 11. Frontend Design
**File**: `plugins/frontend-design/`  
**Purpose**: Frontend design patterns  
**Status**: ⚠️ Unknown (not documented in integration matrix)

#### 12. GitHub Migration
**File**: `plugins/github-migration/`  
**Purpose**: GitHub migration utilities  
**Status**: ⚠️ Unknown (not documented in integration matrix)

#### 13. PR Loop Auto
**File**: `plugins/pr-loop-auto/`  
**Purpose**: Automated PR loop execution  
**Status**: ⚠️ Unknown (not documented in integration matrix)

---

## Anthropic Skills Integration Assessment

### Requested Skills

#### 1. Skill Creator
**Source**: https://github.com/anthropics/skills/tree/main/skills/skill-creator  
**Status**: ❌ Not installed  
**Purpose**: Create new skills using Anthropic's skill format  
**Recommendation**: Install to `.bob/skills/skill-creator/`

#### 2. Wrap Up Skill
**Source**: https://github.com/anthropics/launch-your-agent  
**Status**: ❌ Not installed  
**Purpose**: Session wrap-up and handoff  
**Recommendation**: Install to `.bob/skills/wrap-up/`

#### 3. Agent Skills Specification
**Source**: https://agentskills.io/specification  
**Status**: ⚠️ Partial compliance  
**Current Format**: Custom markdown (not Anthropic spec)  
**Recommendation**: Migrate existing skills to Anthropic spec format

---

## Skills.sh Architecture Skills

### Option 1: Matt Pocock's Improve Codebase Architecture
**URL**: https://www.skills.sh/mattpocock/skills/improve-codebase-architecture  
**Status**: ❌ Not installed (but similar to our architecture-validation)  
**Overlap**: High overlap with `plugins/architecture-validation/SKILL.md`

### Option 2: Anthropic's Architecture Skill
**URL**: https://www.skills.sh/anthropics/knowledge-work-plugins/architecture  
**Status**: ❌ Not installed  
**Overlap**: Unknown (need to review)

### Current Architecture Skill
**File**: `plugins/architecture-validation/SKILL.md`  
**Source**: Matt Pocock's methodology (custom implementation)  
**Status**: ✅ Active and working well  
**Recommendation**: Keep current implementation, evaluate skills.sh versions for enhancements

---

## Integration Status Summary

| Skill | Location | Status | Used In | Format |
|-------|----------|--------|---------|--------|
| **gcp-vm-wave-execution** | `.bob/skills/` | ✅ Active | Phase 0, 5 | Bob IDE |
| **lamport-clock-recovery** | `.bob/skills/` | ✅ Active | All phases | Bob IDE |
| **architecture-validation** | `plugins/` | ✅ Active | Phase 2 | Custom |
| **scope-boundary-check** | `plugins/` | ✅ Active | Phase 1.5 | Custom |
| **parallel-epic-execution** | `plugins/` | ✅ Active | Phase 5 | Custom |
| **multi-agent-orchestrator** | `plugins/` | ⚠️ POC | None | Custom |
| **WAVE2_SHELL_WORKAROUND** | `plugins/` | ✅ Active | Phase 0 | Custom |
| **bobcoin-account-switch** | `plugins/` | ⚠️ Unknown | Unknown | Custom |
| **check-pr** | `plugins/` | ⚠️ Unknown | Unknown | Custom |
| **codebase-architecture** | `plugins/` | ⚠️ Unknown | Unknown | Custom |
| **frontend-design** | `plugins/` | ⚠️ Unknown | Unknown | Custom |
| **github-migration** | `plugins/` | ⚠️ Unknown | Unknown | Custom |
| **pr-loop-auto** | `plugins/` | ⚠️ Unknown | Unknown | Custom |

**Active Skills**: 7/13 (54%)  
**Unknown Status**: 6/13 (46%)

---

## Recommendations

### Immediate Actions

1. **Install Anthropic Skills**:
   ```bash
   # Create skill directories
   mkdir -p .bob/skills/skill-creator
   mkdir -p .bob/skills/wrap-up
   
   # Download from GitHub
   # https://github.com/anthropics/skills/tree/main/skills/skill-creator
   # https://github.com/anthropics/launch-your-agent
   ```

2. **Audit Unknown Plugins**:
   - Review `plugins/bobcoin-account-switch/`
   - Review `plugins/check-pr/`
   - Review `plugins/codebase-architecture/`
   - Review `plugins/frontend-design/`
   - Review `plugins/github-migration/`
   - Review `plugins/pr-loop-auto/`
   - Document status and integration points

3. **Migrate to Anthropic Spec** (Optional):
   - Convert custom plugins to Anthropic skill format
   - Add YAML frontmatter
   - Follow https://agentskills.io/specification

### Short-Term Actions

4. **Evaluate Skills.sh Architecture Skills**:
   - Compare Matt Pocock's skills.sh version with our implementation
   - Evaluate Anthropic's architecture skill
   - Identify enhancements to adopt

5. **Complete Multi-Agent Orchestrator POC**:
   - Test sub-agent support in Bob IDE
   - Document findings
   - Integrate if successful

### Long-Term Actions

6. **Skill Consolidation**:
   - Merge overlapping skills (e.g., VM + local parallel execution)
   - Standardize on Anthropic spec format
   - Create skill dependency graph

7. **Skill Discovery**:
   - Implement auto-discovery for `plugins/` directory
   - Add skill registry
   - Enable skill versioning

---

## Related Documentation

- **Integration Matrix**: `docs/workflow/AUTONOMOUS_REFACTOR_INTEGRATION_MATRIX_V2.md`
- **Skill Relationships**: `plugins/SKILL_RELATIONSHIPS.md`
- **Skill Audit**: `docs/workflow/SKILL_AUDIT_10_PHASES.md`
- **Anthropic Skills**: https://github.com/anthropics/skills
- **Agent Skills Spec**: https://agentskills.io/specification
- **Skills.sh**: https://www.skills.sh/

---

**Document Status**: ✅ Complete  
**Next Review**: After Anthropic skills installation  
**Maintainer**: Autonomous Refactor Mode