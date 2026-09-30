# Skill Relationships - Parallel Execution Strategies

## Three Complementary Skills

### 1. Parallel Epic Execution (Local Worktrees)
**File**: `plugins/parallel-epic-execution/SKILL.md`

**Purpose**: Execute 3 epics simultaneously on local Windows machine using Git worktrees

**Architecture**:
- 3 Git worktrees (epic-cluster-1/2/3)
- 3 Bob CLI sessions (one per worktree)
- File-based clustering (SIMA, Orders, Lifecycle)
- Batch F5 verification

**Use Case**: Local development, full epic workflow (Phase 0-6)

**Time Savings**: 64% (148 hours vs 415 hours sequential)

---

### 2. Multi-Agent Orchestrator (Sub-Agent Pattern)
**File**: `plugins/multi-agent-orchestrator/SKILL.md`

**Purpose**: Single Bob session spawns specialized sub-agents for each epic phase

**Architecture**:
- 1 Bob orchestrator session
- Sub-agents for each phase (Analysis, Planning, Architecture, etc.)
- Artifact-based communication (manifest.json)
- F5 gates between tickets

**Use Case**: Single epic execution with phase specialization

**Status**: POC testing (sub-agent support unknown)

---

### 3. Wave 2 Phase 0 (Remote VM Parallel)
**Files**: 
- `plugins/multi-agent-orchestrator/WAVE2_SHELL_WORKAROUND.md`
- `scripts/wave2/FINAL_SOLUTION_SUMMARY.md`

**Purpose**: Execute 9 Phase 0 hotspot analyses in parallel on remote VM

**Architecture**:
- 9 Python-launched Bob agents (SSH to VM)
- Custom mode: `v12-phase0-hotspot`
- MCP tools: jCodemunch for analysis
- Shell commands for file I/O (workaround for `read_file` bug)

**Use Case**: Batch hotspot analysis before epic planning

**Time Savings**: 9x parallelization (9 epics analyzed simultaneously)

---

## Comparison Matrix

| Feature | Parallel Epic | Multi-Agent Orchestrator | Wave 2 Phase 0 |
|---------|---------------|-------------------------|----------------|
| **Execution** | Local Windows | Local Windows | Remote VM (SSH) |
| **Parallelism** | 3 worktrees | 1 session (sub-agents) | 9 Python processes |
| **Scope** | Full epic (Phase 0-6) | Full epic (Phase 0-6) | Phase 0 only |
| **Isolation** | Git worktrees | Sub-agent contexts | Separate processes |
| **Coordination** | Manual merge | Artifact passing | Manifest files |
| **F5 Testing** | Batch (3 at once) | Per ticket | Not applicable |
| **Status** | Active | POC testing | Active (with workaround) |

---

## When to Use Each

### Use Parallel Epic Execution When:
- ✅ Working on local Windows machine
- ✅ Need to execute full epics (Phase 0-6)
- ✅ Epics target different files (no conflicts)
- ✅ Want maximum time savings (64%)

### Use Multi-Agent Orchestrator When:
- ✅ Working on single epic
- ✅ Want phase specialization (different modes per phase)
- ✅ Need clear audit trail (artifact handoffs)
- ⚠️ After POC confirms sub-agent support works

### Use Wave 2 Phase 0 When:
- ✅ Need batch hotspot analysis
- ✅ Have remote VM with jCodemunch indexed
- ✅ Want to parallelize Phase 0 across many epics
- ✅ Planning to execute Phase 1-6 separately

---

## Integration Scenarios

### Scenario 1: Full Parallel Workflow
1. **Wave 2 Phase 0**: Analyze 9 epics on VM (parallel)
2. **Parallel Epic Execution**: Execute 3 epics locally (worktrees)
3. **Repeat**: Next batch of 3 epics

**Result**: Maximum parallelization at both analysis and execution stages

### Scenario 2: Orchestrator + Worktrees
1. **Multi-Agent Orchestrator**: Execute epic in main repo
2. **Parallel Epic Execution**: Execute 2 more epics in worktrees
3. **Batch F5**: Test all 3 together

**Result**: Combines phase specialization with parallel execution

### Scenario 3: Wave 2 + Sequential
1. **Wave 2 Phase 0**: Analyze 9 epics on VM (parallel)
2. **Sequential Execution**: Execute epics one at a time locally
3. **Prioritize**: Use hotspot analysis to order epics

**Result**: Informed prioritization without parallel execution complexity

---

## Known Issues

### Wave 2 Phase 0: read_file Tool Bug
**Problem**: `read_file` tool fails in SSH/non-interactive mode

**Workaround**: Use shell commands (`cat`, `ls`, `wc -l`)

**Status**: Working solution deployed

**Documentation**: `plugins/multi-agent-orchestrator/WAVE2_SHELL_WORKAROUND.md`

### Multi-Agent Orchestrator: Sub-Agent Support Unknown
**Problem**: Bob Shell sub-agent capabilities not yet tested

**Status**: POC testing required

**Fallback**: Use Parallel Epic Execution instead

---

## References

- **Parallel Epic**: `plugins/parallel-epic-execution/SKILL.md`
- **Orchestrator**: `plugins/multi-agent-orchestrator/SKILL.md`
- **Wave 2**: `plugins/multi-agent-orchestrator/WAVE2_SHELL_WORKAROUND.md`
- **Workflow Design**: `docs/workflow/V12_EPIC_WORKFLOW_REFACTORING_DESIGN.md`