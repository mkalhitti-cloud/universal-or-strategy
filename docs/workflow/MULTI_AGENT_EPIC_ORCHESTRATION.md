# Multi-Agent Epic Orchestration Architecture

**Version**: 2.0  
**Date**: 2026-06-10  
**Status**: Design Phase  
**Pattern**: Agent-as-Tool Composition (Watsonx Orchestrate)

## Executive Summary

This document defines the **true multi-agent orchestration** architecture where each epic phase is executed by an **independent specialized agent**. The orchestrator agent spawns phase agents using the `new_task` tool, creating a hierarchical agent system with clear separation of concerns.

## Architecture Overview

### Agent Hierarchy

```
Orchestrator Agent (Worker-1, Worker-2, Worker-3, Worker-4)
  ├─ Phase 0 Agent (Hotspot Analysis) - ask mode
  ├─ Phase 1 Agent (Scope Definition) - plan mode
  ├─ Phase 1.5 Agent (Scope Boundary) - plan mode
  ├─ Phase 2 Agent (Architecture Planning) - plan mode
  ├─ Phase 3 Agent (DNA & PR Audit) - advanced mode
  ├─ Phase 4 Agent (Ticket Generation) - plan mode
  ├─ Phase 5.X Agent (Ticket Execution) - v12-engineer mode
  ├─ Phase 5.X.V Agent (Verification) - advanced mode
  └─ Phase 6 Agent (Final Review) - advanced mode
```

### Key Principles

1. **One Phase = One Agent**: Each phase runs as an independent agent task
2. **Manifest-Based Handoff**: Agents read inputs from manifest, write outputs to manifest
3. **Mode Specialization**: Each agent runs in the optimal mode for its task
4. **Autonomous Execution**: Phase agents execute without orchestrator intervention
5. **Parallel Capability**: Independent phases can spawn concurrently

## Agent Definitions

### Orchestrator Agent (Worker)

**Purpose**: Coordinate epic execution by spawning phase agents

**Responsibilities**:
- Claim epic from roadmap (atomic git lock)
- Read manifest to determine next phases
- Spawn phase agents using `new_task` tool
- Monitor phase completion
- Update manifest with results
- Release epic lock on completion

**Tools Used**:
- `new_task` - Spawn phase agents
- Git operations - Atomic locking
- Manifest operations - State management

**YAML**: `sub_orchestrator_worker_X.yaml`

### Phase 0 Agent (Hotspot Analysis)

**Mode**: `ask`  
**Purpose**: Analyze method complexity and identify hotspots

**Inputs** (from manifest):
- Epic ID
- Method name
- File path
- Current cyclomatic complexity

**Outputs** (to manifest):
- `00-hotspots.md` - Hotspot analysis report

**Tasks**:
1. Read method source code
2. Analyze complexity patterns
3. Identify extraction candidates
4. Document findings in `00-hotspots.md`
5. Update manifest with output path

**YAML**: `phase_0_hotspot_agent.yaml`

### Phase 1 Agent (Scope Definition)

**Mode**: `plan`  
**Purpose**: Define extraction scope and boundaries

**Inputs** (from manifest):
- `00-hotspots.md`

**Outputs** (to manifest):
- `00-scope.md` - Scope definition

**Tasks**:
1. Read hotspot analysis
2. Define extraction targets (methods to extract)
3. Identify dependencies
4. Set complexity reduction goals
5. Document scope in `00-scope.md`
6. Update manifest

**YAML**: `phase_1_scope_agent.yaml`

### Phase 1.5 Agent (Scope Boundary)

**Mode**: `plan`  
**Purpose**: Validate scope boundaries (V12.23 No Scope Creep Protocol)

**Inputs** (from manifest):
- `00-scope.md`

**Outputs** (to manifest):
- `01-scope-boundary.md` - Boundary validation

**Tasks**:
1. Read scope definition
2. Check for scope creep indicators
3. Validate ONE EPIC = ONE CONCERN
4. Verify no pre-existing fixes included
5. Document validation in `01-scope-boundary.md`
6. Update manifest

**YAML**: `phase_1_5_boundary_agent.yaml`

### Phase 2 Agent (Architecture Planning)

**Mode**: `plan`  
**Purpose**: Design extraction architecture

**Inputs** (from manifest):
- `01-scope-boundary.md`

**Outputs** (to manifest):
- `02-architecture-plan.md` - Architecture design
- `02-diagrams.mmd` - Mermaid diagrams

**Tasks**:
1. Read validated scope
2. Design helper method signatures
3. Plan extraction sequence
4. Create call flow diagrams
5. Document plan in `02-architecture-plan.md`
6. Generate diagrams in `02-diagrams.mmd`
7. Update manifest

**YAML**: `phase_2_architecture_agent.yaml`

### Phase 3 Agent (DNA & PR Audit)

**Mode**: `advanced`  
**Purpose**: Audit plan against V12 DNA and PR hygiene

**Inputs** (from manifest):
- `02-architecture-plan.md`

**Outputs** (to manifest):
- `03-audit-report.md` - Audit findings

**Tasks**:
1. Read architecture plan
2. Check V12 DNA compliance (lock-free, CYC ≤ 10, ASCII-only)
3. Check PR hygiene (diff size, whitespace)
4. Use jcodemunch MCP for symbol analysis
5. Document findings in `03-audit-report.md`
6. Update manifest

**YAML**: `phase_3_audit_agent.yaml`

### Phase 4 Agent (Ticket Generation)

**Mode**: `plan`  
**Purpose**: Generate implementation tickets

**Inputs** (from manifest):
- `02-architecture-plan.md`
- `03-audit-report.md`

**Outputs** (to manifest):
- `04-tickets.md` - Implementation tickets

**Tasks**:
1. Read architecture plan and audit report
2. Break plan into atomic tickets
3. Define ticket dependencies
4. Set success criteria per ticket
5. Document tickets in `04-tickets.md`
6. Update manifest with ticket count

**YAML**: `phase_4_tickets_agent.yaml`

### Phase 5.X Agent (Ticket Execution)

**Mode**: `v12-engineer`  
**Purpose**: Execute single implementation ticket

**Inputs** (from manifest):
- `04-tickets.md` (specific ticket)
- `02-architecture-plan.md`

**Outputs** (to manifest):
- `ticket-X-completion.md` - Completion report

**Tasks**:
1. Read ticket specification
2. Extract helper method
3. Update caller to use helper
4. Run build verification
5. Document changes in `ticket-X-completion.md`
6. Update manifest

**YAML**: `phase_5_ticket_agent.yaml`

### Phase 5.X.V Agent (Verification)

**Mode**: `advanced`  
**Purpose**: Verify ticket implementation

**Inputs** (from manifest):
- `ticket-X-completion.md`

**Outputs** (to manifest):
- `ticket-X-verification.md` - Verification report

**Tasks**:
1. Read completion report
2. Run build verification
3. Check complexity reduction
4. Verify no regressions
5. Use jcodemunch to verify symbol changes
6. Document verification in `ticket-X-verification.md`
7. Update manifest

**YAML**: `phase_5_verification_agent.yaml`

### Phase 6 Agent (Final Review)

**Mode**: `advanced`  
**Purpose**: Final epic review and completion

**Inputs** (from manifest):
- All `ticket-X-verification.md` files

**Outputs** (to manifest):
- `05-completion-report.md` - Final report

**Tasks**:
1. Read all verification reports
2. Verify all tickets completed
3. Check overall complexity reduction
4. Run full build and test suite
5. Document completion in `05-completion-report.md`
6. Update manifest status to "completed"

**YAML**: `phase_6_review_agent.yaml`

## Orchestrator Workflow

### Epic Execution Flow

```python
# Orchestrator Agent Pseudocode

def execute_epic(epic_id):
    # 1. Claim epic
    claim_result = claim_epic(epic_id, worker_id)
    if not claim_result.success:
        return  # Epic already claimed
    
    # 2. Load manifest
    manifest = load_manifest(epic_id)
    
    # 3. Execute phases sequentially
    phases = [
        ("0", "ask", "Hotspot Analysis"),
        ("1", "plan", "Scope Definition"),
        ("1.5", "plan", "Scope Boundary"),
        ("2", "plan", "Architecture Planning"),
        ("3", "advanced", "DNA & PR Audit"),
        ("4", "plan", "Ticket Generation"),
    ]
    
    for phase_id, mode, description in phases:
        # Check dependencies satisfied
        if not validate_dependencies(epic_id, phase_id):
            log_error(f"Dependencies not satisfied for phase {phase_id}")
            break
        
        # Spawn phase agent
        result = new_task(
            mode=mode,
            message=f"Execute Phase {phase_id}: {description} for {epic_id}",
            todos=[
                f"[ ] Read manifest inputs for phase {phase_id}",
                f"[ ] Execute phase {phase_id} tasks",
                f"[ ] Write outputs to manifest",
                f"[ ] Update manifest status"
            ]
        )
        
        # Wait for completion (blocking)
        if not result.success:
            log_error(f"Phase {phase_id} failed")
            break
    
    # 4. Execute tickets (parallel if independent)
    ticket_count = manifest['metadata']['total_tickets']
    for ticket_num in range(1, ticket_count + 1):
        # Spawn ticket execution agent
        exec_result = new_task(
            mode="v12-engineer",
            message=f"Execute Ticket {ticket_num} for {epic_id}",
            todos=[
                f"[ ] Read ticket {ticket_num} specification",
                f"[ ] Extract helper method",
                f"[ ] Update caller",
                f"[ ] Verify build",
                f"[ ] Document completion"
            ]
        )
        
        # Spawn verification agent
        verify_result = new_task(
            mode="advanced",
            message=f"Verify Ticket {ticket_num} for {epic_id}",
            todos=[
                f"[ ] Read completion report",
                f"[ ] Run build verification",
                f"[ ] Check complexity reduction",
                f"[ ] Document verification"
            ]
        )
    
    # 5. Final review
    review_result = new_task(
        mode="advanced",
        message=f"Final review for {epic_id}",
        todos=[
            "[ ] Read all verification reports",
            "[ ] Verify all tickets completed",
            "[ ] Check overall complexity reduction",
            "[ ] Document completion"
        ]
    )
    
    # 6. Release epic
    release_epic(epic_id, worker_id)
```

## new_task Tool Integration

### Tool Signature

```xml
<new_task>
<mode>mode-slug</mode>
<message>Initial instructions for the new task</message>
<todos>
[ ] First task to complete
[ ] Second task to complete
[ ] Third task to complete
</todos>
</new_task>
```

### Example: Spawning Phase 0 Agent

```xml
<new_task>
<mode>ask</mode>
<message>Execute Phase 0 (Hotspot Analysis) for EPIC-CCN-21.

**Epic**: EPIC-CCN-21
**Method**: GetSubscriberCounts
**File**: src/V12_002.Atm.cs
**Current CYC**: 9

**Your Task**:
1. Read the manifest at docs/brain/EPIC-CCN-21/manifest.json
2. Analyze the method GetSubscriberCounts for complexity hotspots
3. Identify extraction candidates
4. Document findings in docs/brain/EPIC-CCN-21/00-hotspots.md
5. Update manifest with output path and status "completed"

**Success Criteria**:
- 00-hotspots.md exists and contains analysis
- Manifest updated with phase 0 status = "completed"
</message>
<todos>
[ ] Read manifest inputs
[ ] Analyze GetSubscriberCounts method
[ ] Identify extraction candidates
[ ] Write 00-hotspots.md
[ ] Update manifest status
</todos>
</new_task>
```

### Example: Spawning Phase 5.1 Agent (Ticket Execution)

```xml
<new_task>
<mode>v12-engineer</mode>
<message>Execute Ticket 1 for EPIC-CCN-21.

**Epic**: EPIC-CCN-21
**Ticket**: 1 of 2
**Task**: Extract ValidateSubscriberState helper

**Your Task**:
1. Read ticket specification from docs/brain/EPIC-CCN-21/04-tickets.md
2. Read architecture plan from docs/brain/EPIC-CCN-21/02-architecture-plan.md
3. Extract ValidateSubscriberState helper method
4. Update GetSubscriberCounts to call helper
5. Verify build passes
6. Document completion in docs/brain/EPIC-CCN-21/ticket-1-completion.md
7. Update manifest with ticket 1 status

**Success Criteria**:
- Helper method extracted with CYC ≤ 5
- Caller updated correctly
- Build passes
- ticket-1-completion.md exists
- Manifest updated
</message>
<todos>
[ ] Read ticket specification
[ ] Read architecture plan
[ ] Extract ValidateSubscriberState helper
[ ] Update caller
[ ] Verify build
[ ] Document completion
[ ] Update manifest
</todos>
</new_task>
```

## Parallel Execution Strategy

### Independent Phases (Can Run Concurrently)

**Ticket Execution** (Phase 5.1, 5.2, ..., 5.N):
```python
# Spawn all ticket agents concurrently
ticket_agents = []
for ticket_num in range(1, ticket_count + 1):
    agent = new_task(
        mode="v12-engineer",
        message=f"Execute Ticket {ticket_num} for {epic_id}",
        todos=[...]
    )
    ticket_agents.append(agent)

# Wait for all to complete
for agent in ticket_agents:
    agent.wait_for_completion()
```

**Verification** (Phase 5.1.V, 5.2.V, ..., 5.N.V):
```python
# Spawn all verification agents concurrently
verify_agents = []
for ticket_num in range(1, ticket_count + 1):
    agent = new_task(
        mode="advanced",
        message=f"Verify Ticket {ticket_num} for {epic_id}",
        todos=[...]
    )
    verify_agents.append(agent)

# Wait for all to complete
for agent in verify_agents:
    agent.wait_for_completion()
```

## Worker MCP Server Updates

### Updated execute_epic_tool

```python
async def execute_epic_tool(epic_id: str) -> List[TextContent]:
    """Execute epic by spawning phase agents"""
    try:
        # Load manifest
        manifest_path = REPO_PATH / f"docs/brain/{epic_id}/manifest.json"
        with open(manifest_path, 'r') as f:
            manifest = json.load(f)
        
        # Define phases
        phases = [
            ("0", "ask", "Hotspot Analysis"),
            ("1", "plan", "Scope Definition"),
            ("1.5", "plan", "Scope Boundary"),
            ("2", "plan", "Architecture Planning"),
            ("3", "advanced", "DNA & PR Audit"),
            ("4", "plan", "Ticket Generation"),
        ]
        
        results = []
        
        for phase_id, mode, description in phases:
            # Spawn phase agent using new_task
            # NOTE: This requires Bob IDE API integration
            # For now, return instruction to spawn manually
            
            phase_result = {
                "phase": phase_id,
                "mode": mode,
                "description": description,
                "instruction": f"Spawn agent in {mode} mode for {description}",
                "message_template": generate_phase_message(epic_id, phase_id, mode, description)
            }
            
            results.append(phase_result)
        
        return [TextContent(
            type="text",
            text=json.dumps({
                "success": True,
                "epic_id": epic_id,
                "phases": results,
                "note": "Use new_task tool to spawn each phase agent"
            })
        )]
    
    except Exception as e:
        return [TextContent(
            type="text",
            text=json.dumps({
                "success": False,
                "error": str(e)
            })
        )]
```

## Implementation Checklist

### Phase 1: Agent YAML Creation (2 hours)
- [ ] Create `phase_0_hotspot_agent.yaml`
- [ ] Create `phase_1_scope_agent.yaml`
- [ ] Create `phase_1_5_boundary_agent.yaml`
- [ ] Create `phase_2_architecture_agent.yaml`
- [ ] Create `phase_3_audit_agent.yaml`
- [ ] Create `phase_4_tickets_agent.yaml`
- [ ] Create `phase_5_ticket_agent.yaml`
- [ ] Create `phase_5_verification_agent.yaml`
- [ ] Create `phase_6_review_agent.yaml`

### Phase 2: Orchestrator Update (1 hour)
- [ ] Update `sub_orchestrator_worker_X.yaml` to use `new_task`
- [ ] Add phase agent spawning logic
- [ ] Add parallel execution for tickets
- [ ] Add error handling and recovery

### Phase 3: Testing (2 hours)
- [ ] Test Phase 0 agent spawn
- [ ] Test Phase 1 agent spawn
- [ ] Test full phase chain (0 → 1 → 1.5 → 2 → 3 → 4)
- [ ] Test parallel ticket execution
- [ ] Test end-to-end epic (EPIC-CCN-21)

### Phase 4: Documentation (1 hour)
- [ ] Update `WATSONX_MCP_SETUP_GUIDE.md`
- [ ] Create agent spawn examples
- [ ] Document troubleshooting

## Success Criteria

**Per-Phase Agent**:
- ✅ Reads inputs from manifest
- ✅ Executes phase tasks autonomously
- ✅ Writes outputs to standard locations
- ✅ Updates manifest with status
- ✅ No manual intervention required

**Orchestrator**:
- ✅ Spawns phase agents using `new_task`
- ✅ Monitors phase completion
- ✅ Handles phase failures gracefully
- ✅ Supports parallel execution
- ✅ Updates roadmap on completion

**End-to-End**:
- ✅ Complete epic execution with 9 independent agents
- ✅ All phases complete successfully
- ✅ Manifest tracks full workflow
- ✅ Build passes
- ✅ Complexity reduced to target

## References

- **new_task Tool**: System prompt, Tool Use section
- **V12 Epic Workflow**: `docs/workflow/V12_EPIC_WORKFLOW_REFACTORING_DESIGN.md`
- **Manifest Schema**: `docs/workflow/V12_EPIC_WORKFLOW_REFACTORING_DESIGN.md` (Appendix A)
- **Agent-as-Tool Pattern**: `_agents/workflows/agent_as_tool.md`

---

**Document Status**: Design v2.0  
**Next Step**: Create phase agent YAMLs  
**Approval Required**: Director sign-off before implementation