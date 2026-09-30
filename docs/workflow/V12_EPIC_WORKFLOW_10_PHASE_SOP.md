# V12 Epic Workflow: 10-Phase Standard Operating Procedure

**Version**: V12.25 (Manifest-Based Independent Subtasks)
**Effective**: 2026-06-13
**Status**: APPROVED
**Authority**: Director Authorization 2026-06-13

## Executive Summary

The V12 Epic Workflow uses a **10-phase manifest-based architecture** where each phase runs as an independent session with clear inputs/outputs tracked in a central `manifest.json`. This eliminates context window exhaustion, enables parallel execution, and provides checkpoint-based recovery.

## Phase Structure Overview

| Phase | Name | Time | Jane Street | OKF KB Hook | Mode | Purpose |
|-------|------|------|-------------|-------------|------|---------|
| **-1** | Pre-flight | 5 min | ❌ | ❌ | `ask` | Branch validation, build check |
| **0** | Hotspot | 10 min | ❌ | ❌ | `ask` | jCodemunch complexity analysis |
| **1** | Scope + Boundary | 20 min | ⚠️ | ⚠️ | `plan` | Define scope + validate boundaries |
| **2** | Architecture | 25 min | ✅ | ✅ | `plan` | Design extraction plan |
| **3** | Audit | 10 min | ⚠️ | ⚠️ | `agent` | DNA & PR hygiene checks |
| **4** | Tickets | 10 min | ⚠️ | ⚠️ | `plan` | Generate surgical tickets |
| **4.5** | Ticket Review | 10 min | ✅ | ✅ | `plan` | Jane Street validation gate |
| **5** | Execution | 10 min/ticket | ✅ | ✅ | `v12-engineer` | Surgical refactoring |
| **5.V** | Verification | 5 min/ticket | ⚠️ | ⚠️ | `agent` | Per-ticket validation |
| **6** | Final Review | 10 min | ❌ | ❌ | `agent` | Epic completion report |

**Total Planning Time**: 100 minutes per epic
**Jane Street Gates**: 3 (Phase 2, 4.5, 5)
**Total Phases**: 10

### Legend
- ✅ **Full Jane Street Integration**: OKF local wiki (`docs/intel/jane-street/`) + `query_kb.py` mandatory
- ⚠️ **Partial Integration**: OKF query optional (recommended)
- ❌ **No Integration**: Analysis-only, no Jane Street validation needed
**Note**: `query_kb.py` uses OKF local wiki as primary source. Firebase is tried first but OKF fallback always resolves.

## Phase Definitions

### Phase -1: Pre-flight Check (5 min)

**Command**: `epic-preflight EPIC-CCN-X`
**Mode**: `ask`
**Purpose**: Validate environment before starting epic

**Inputs**: None
**Outputs**: `preflight-report.md`

**Checks**:
1. ✅ Branch strategy compliance (GitButler virtual branches)
2. ✅ Build passes (`dotnet build`)
3. ✅ No uncommitted changes in `src/`
4. ✅ Manifest directory exists
5. ✅ jCodemunch index current

**Success Criteria**:
- All checks pass
- Manifest initialized with `status: "preflight_complete"`

**Failure Protocol**:
- STOP epic immediately
- Fix blocking issues
- Re-run preflight

---

### Phase 0: Hotspot Analysis (10 min)

**Command**: `epic-intake EPIC-CCN-X "Description"`
**Mode**: `ask`
**Purpose**: Identify complexity hotspot using jCodemunch

**Inputs**: None
**Outputs**: `00-hotspots.md`, `manifest.json`

**Process**:
1. Run `jcodemunch get_hotspots` (top 20, 90-day churn)
2. Select target method (CYC >8, high churn)
3. Run `jcodemunch get_blast_radius` for impact analysis
4. Document hotspot metadata (file, line, CYC, LOC)

**Success Criteria**:
- Hotspot identified with CYC >8
- Blast radius documented
- Manifest created with phase 0 complete

**Jane Street Integration**: ❌ None (analysis only)

---

### Phase 1: Scope + Boundary (20 min)

**Command**: `epic-scope-boundary EPIC-CCN-X --phase 1`
**Mode**: `plan`
**Purpose**: Define extraction scope AND validate single-method boundary

**Inputs**: `00-hotspots.md`
**Outputs**: `00-scope.md`, `01-scope-boundary.md`

**Process**:
1. **Scope Definition** (10 min):
   - Identify extraction candidates within method
   - Define target complexity (CYC ≤8)
   - Document call graph dependencies
   - Create extraction checklist

2. **Boundary Validation** (10 min):
   - Verify extraction stays within single method
   - Check for cross-method dependencies
   - Validate no struct/class modifications
   - Confirm no scope creep (V12.23 Protocol)

**Success Criteria**:
- Scope document complete
- Boundary validation PASS
- No cross-method dependencies
- Manifest updated with phase 1 complete

**Jane Street Integration**: ⚠️ Partial (should query KB for extraction patterns)

**MANDATORY GATE**: Phase 1.5 boundary check prevents scope creep (V12.23)

---

### Phase 2: Architecture Planning (25 min)

**Command**: `epic-plan EPIC-CCN-X`
**Mode**: `plan` (with Firebase hook)
**Purpose**: Design extraction plan with Jane Street validation

**Inputs**: `01-scope-boundary.md`
**Outputs**: `02-architecture-plan.md`, `02-diagrams.mmd`

**Process**:
1. Query Jane Street KB for:
   - Extraction patterns (cognitive simplicity)
   - Parameter passing strategies
   - Error handling patterns
   - Testing requirements

2. Design extraction:
   - Method signatures (explicit, no magic)
   - Call graph (before/after)
   - State management (immutable where possible)
   - Error boundaries

3. Create Mermaid diagrams:
   - Current complexity flow
   - Proposed extraction structure
   - Call hierarchy

**Success Criteria**:
- Architecture plan complete
- Jane Street principles applied
- Diagrams generated
- Complexity reduction path clear
- Manifest updated with phase 2 complete

**Jane Street Integration**: ✅ Full (Firebase RAG + custom mode)

---

### Phase 3: DNA & PR Audit (10 min)

**Command**: `epic-scan EPIC-CCN-X`
**Mode**: `advanced` (requires MCP tools)
**Purpose**: Validate plan against V12 DNA and PR hygiene

**Inputs**: `02-architecture-plan.md`
**Outputs**: `03-audit-report.md`

**Process**:
1. **DNA Compliance**:
   - No new `lock()` statements
   - ASCII-only strings
   - No nullable reference warnings
   - Immutable state where possible

2. **PR Hygiene**:
   - Estimated diff <10k chars
   - Single concern (no scope creep)
   - No whitespace mutation
   - Build will pass

3. **Complexity Validation**:
   - Target CYC ≤8 achievable
   - No hidden dependencies
   - Test coverage plan exists

**Success Criteria**:
- All DNA checks pass
- PR hygiene validated
- No blocking issues
- Manifest updated with phase 3 complete

**Jane Street Integration**: ⚠️ Partial (should validate against Jane Street rules)

---

### Phase 4: Ticket Generation (10 min)

**Command**: `epic-tickets EPIC-CCN-X`
**Mode**: `plan`
**Purpose**: Generate surgical extraction tickets

**Inputs**: `02-architecture-plan.md`
**Outputs**: `04-tickets.md`

**Process**:
1. Analyze method complexity using jCodemunch
2. Generate tickets for each extraction:
   - TICKET-1: Extract method A (CYC X → Y)
   - TICKET-2: Extract method B (CYC X → Y)
   - etc.

3. For each ticket:
   - Method signature
   - Parameter list
   - Return type
   - Call sites to update
   - Test requirements

**Success Criteria**:
- All extractions have tickets
- Each ticket is surgical (single method)
- Complexity targets defined
- Manifest updated with phase 4 complete

**Jane Street Integration**: ⚠️ Partial (should validate ticket granularity)

---

### Phase 4.5: Ticket Review (10 min) **IMPLEMENTED**

**Command**: `use_mcp_tool phase-4-5-review execute_phase_4_5 {"epic_id": "EPIC-CCN-X"}`
**Mode**: `plan` (MCP server with Firebase integration)
**MCP Server**: `phase-4-5-review` (FastMCP)
**Purpose**: Jane Street validation gate before execution

**Inputs**: `04-tickets.md`, `02-architecture-plan.md`, `manifest.json`
**Outputs**: `04.5-ticket-review.md`

**Process**:
1. **Firebase KB Query** (Automated):
   - Extraction patterns
   - Complexity reduction strategies
   - Single responsibility principles
   - Refactoring anti-patterns
   - Method extraction best practices

2. **Scope Boundary Validation** (V12.23 Protocol):
   - ✅ Each ticket targets SINGLE METHOD only
   - ✅ No cross-method refactoring
   - ✅ No "while we're here" improvements
   - ✅ Clear extraction boundaries
   - ✅ Ticket count ≤5 (over-engineering check)

3. **Complexity Target Validation** (Jane Street Alignment):
   - ✅ Target complexity ≤8 (Jane Street threshold)
   - ✅ Realistic reduction claims
   - ✅ No over-engineering
   - ✅ Extraction strategy sound

4. **Jane Street Pattern Compliance**:
   - ✅ "Make illegal states unrepresentable"
   - ✅ Lock-free patterns (no `lock()` blocks)
   - ✅ Actor/FSM pattern usage
   - ✅ ASCII-only compliance

5. **Risk Assessment**:
   - ✅ Blast radius documented
   - ✅ Test coverage requirements clear
   - ✅ Rollback strategy defined
   - ✅ Dependencies validated

**Success Criteria**:
- ✅ All validation checks pass
- ✅ No scope creep detected
- ✅ Complexity targets realistic (≤8)
- ✅ Jane Street KB consulted (5+ documents)
- ✅ Approval/rejection decision documented
- ✅ Manifest updated with phase 4.5 complete

**Jane Street Integration**: ✅ Full (Firebase RAG via `query_kb.py`)

**CRITICAL GATE**: Prevents execution of flawed tickets

**Implementation Details**:
- **Script**: `scripts/phase_4_5_ticket_review_mcp.py`
- **Tool**: `execute_phase_4_5(epic_id: str)`
- **Firebase**: Queries `jane_street_knowledge_base` collection
- **Validation Functions**:
  - `validate_ticket_scope()` - Detects multi-method extraction
  - `validate_complexity_targets()` - Checks CYC ≤8 threshold
  - `query_jane_street_kb()` - RAG pattern retrieval

**Decision Output**:
```markdown
## Decision
**APPROVED** ✅ | **REJECTED** ❌

**Rationale**: [Detailed explanation]

## Recommendations
1. [Specific improvement]
2. [Risk mitigation]
3. [Alternative approach]
```

**Failure Protocol**:
- If REJECTED: Return to Phase 4 for ticket revision
- Document rejection rationale
- Update manifest with `decision: "rejected"`
- Block Phase 5 execution until re-review passes

---

### Phase 5: Ticket Execution (10 min/ticket)

**Command**: `epic-validate EPIC-CCN-X --ticket N`
**Mode**: `v12-engineer` (Bob CLI with Firebase hook)
**Purpose**: Surgical refactoring execution

**Inputs**: `04-tickets.md`, `04.5-ticket-review.md`
**Outputs**: `ticket-N-completion.md`

**Process**:
1. Bob CLI loads ticket context
2. Queries Jane Street KB for:
   - Implementation patterns
   - Error handling
   - Testing strategies

3. Executes extraction:
   - Create new method
   - Update call sites
   - Add tests
   - Verify complexity reduction

4. Runs validation:
   - Build passes
   - Tests pass
   - Complexity target met
   - No new warnings

**Success Criteria**:
- Extraction complete
- Build passes
- Tests pass
- CYC target met
- Manifest updated with ticket N complete

**Jane Street Integration**: ✅ Full (Firebase RAG + custom mode)

**Parallel Execution**: Independent tickets can run concurrently (max 10 agents)

---

### Phase 5.V: Ticket Verification (5 min/ticket)

**Command**: `epic-verify-ticket EPIC-CCN-X --ticket N`
**Mode**: `advanced` (requires MCP tools)
**Purpose**: Independent verification of ticket completion

**Inputs**: `ticket-N-completion.md`
**Outputs**: `ticket-N-verification.md`

**Process**:
1. **Code Review**:
   - Extraction matches plan
   - No scope creep
   - Clean implementation

2. **Complexity Verification**:
   - Run `jcodemunch get_symbol_complexity`
   - Verify CYC ≤8 achieved
   - Check nesting depth

3. **Test Coverage**:
   - New method has tests
   - Tests pass
   - Edge cases covered

4. **Build Validation**:
   - `dotnet build` passes
   - No new warnings
   - Hard links synced

**Success Criteria**:
- All checks pass
- Complexity target met
- Tests pass
- Manifest updated with ticket N verified

**Jane Street Integration**: ⚠️ Partial (should validate against Jane Street patterns)

**Parallel Execution**: Verifications can run concurrently after respective tickets

---

### Phase 6: Final Review (10 min)

**Command**: `epic-review-final EPIC-CCN-X`
**Mode**: `advanced`
**Purpose**: Epic completion report and roadmap update

**Inputs**: All `ticket-N-verification.md` files
**Outputs**: `05-completion-report.md`

**Process**:
1. **Aggregate Results**:
   - Total tickets completed
   - Complexity reduction achieved
   - Test coverage added
   - Build status

2. **Quality Metrics**:
   - CYC reduction (before → after)
   - LOC reduction
   - Test coverage increase
   - Build time impact

3. **Roadmap Update**:
   - Mark epic complete
   - Update `epic_roadmap.json`
   - Document lessons learned

**Success Criteria**:
- All tickets verified
- Completion report generated
- Roadmap updated
- Manifest status = "completed"

**Jane Street Integration**: ❌ None (reporting only)

---

## Manifest-Based State Management

### Central Manifest Structure

**Location**: `docs/brain/EPIC-{ID}/manifest.json`

```json
{
  "epic_id": "EPIC-CCN-X",
  "status": "in_progress",
  "created_at": "2026-06-13T22:00:00Z",
  "updated_at": "2026-06-13T22:30:00Z",
  "phases": {
    "-1": {
      "status": "completed",
      "started_at": "2026-06-13T22:00:00Z",
      "completed_at": "2026-06-13T22:05:00Z",
      "outputs": ["preflight-report.md"]
    },
    "0": {
      "status": "completed",
      "started_at": "2026-06-13T22:05:00Z",
      "completed_at": "2026-06-13T22:15:00Z",
      "outputs": ["00-hotspots.md"]
    },
    "1": {
      "status": "completed",
      "started_at": "2026-06-13T22:15:00Z",
      "completed_at": "2026-06-13T22:35:00Z",
      "outputs": ["00-scope.md", "01-scope-boundary.md"]
    },
    "2": {
      "status": "in_progress",
      "started_at": "2026-06-13T22:35:00Z",
      "outputs": []
    }
  },
  "tickets": [],
  "metadata": {
    "method": "PropagateMaster_IdentifyMove",
    "file": "V12_002.Orders.Callbacks.Propagation.cs",
    "cyc_before": 18,
    "cyc_target": 8
  }
}
```

### Manifest Helper Script

**Location**: `scripts/epic_manifest.py`

**Functions**:
- `load_manifest(epic_id)` - Load and validate manifest
- `update_manifest(epic_id, phase, status, outputs)` - Update phase status
- `validate_dependencies(epic_id, phase)` - Check dependencies satisfied
- `get_next_phases(epic_id)` - Determine executable phases

---

## Parallel Execution Strategy

### Independent Phases

**Can Run Concurrently**:
- Phase 5.1, 5.2, ..., 5.N (if tickets are independent)
- Phase 5.1.V, 5.2.V, ..., 5.N.V (after respective tickets)

**Must Run Sequentially**:
- Phase -1 → 0 → 1 → 2 → 3 → 4 → 4.5
- Phase 5.X → 5.X.V (ticket must complete before verification)
- All Phase 5.X.V → Phase 6 (all verifications before final review)

### VM Capacity Limits

**Maximum Parallel Agents**: 10 (on n2-standard-4: 4 vCPU, 16 GB RAM)

**Resource Allocation**:
- Each agent: ~1.6 GB RAM
- Each agent: ~0.4 vCPU
- Safety margin: 20% reserved

**Orchestration**: Use Bob CLI orchestrator or Watsonx Orchestrate

---

## Jane Street Integration Points

### Current Integration (2 phases)

1. **Phase 2 (Architecture)**: `v12-epic-planner` mode
   - Firebase hook: `scripts/query_kb.py`
   - Queries: Extraction patterns, error handling, testing

2. **Phase 5 (Execution)**: `v12-engineer` mode
   - Firebase hook: `scripts/query_kb.py`
   - Queries: Implementation patterns, testing strategies

### Pending Integration (3 phases)

3. **Phase 1 (Scope)**: Should query for extraction patterns
4. **Phase 3 (Audit)**: Should validate against Jane Street rules
5. **Phase 4.5 (Ticket Review)**: NEW - Full Jane Street validation gate
6. **Phase 5.V (Verification)**: Should validate against Jane Street patterns

### Implementation Plan

**Phase 4.5 Script**: `scripts/phase_4_5_ticket_review_mcp.py`
- Load tickets from Phase 4
- Query Jane Street KB for validation patterns
- Check each ticket against anti-patterns
- Generate review report with pass/fail per ticket

**Firebase Hook Updates**:
- Add to Phase 1 script: Query extraction patterns
- Add to Phase 3 script: Validate against Jane Street rules
- Add to Phase 5.V script: Validate implementation patterns

---

## Command Reference

### Epic Lifecycle Commands

```bash
# Start new epic (Phase 0)
epic-intake EPIC-CCN-X "Description"

# Define scope (Phase 1)
epic-scope-boundary EPIC-CCN-X --phase 1

# Validate scope (Phase 1.5 - merged into Phase 1)
epic-scope-boundary EPIC-CCN-X --phase 1.5

# Plan architecture (Phase 2)
epic-plan EPIC-CCN-X

# Audit plan (Phase 3)
epic-scan EPIC-CCN-X

# Generate tickets (Phase 4)
epic-tickets EPIC-CCN-X

# Review tickets (Phase 4.5) **NEW**
epic-ticket-review EPIC-CCN-X

# Execute ticket (Phase 5.X)
epic-validate EPIC-CCN-X --ticket 1

# Verify ticket (Phase 5.X.V)
epic-verify-ticket EPIC-CCN-X --ticket 1

# Final review (Phase 6)
epic-review-final EPIC-CCN-X
```

### Status Commands

```bash
# Check epic status
python scripts/epic_manifest.py status EPIC-CCN-X

# List all epics
python scripts/epic_manifest.py list

# Get next pending epic
python scripts/epic_manifest.py next
```

---

## Failure Recovery

### Resume from Any Phase

```bash
# Check current status
python scripts/epic_manifest.py status EPIC-CCN-X

# Resume from failed phase
epic-validate EPIC-CCN-X --ticket 2  # Resume Phase 5.2
```

### Rollback Protocol

1. Identify failed phase in manifest
2. Review phase output artifacts
3. Fix issues in separate session
4. Update manifest status to `pending`
5. Re-run phase

---

## Success Criteria

### Per Phase

- ✅ Dependencies satisfied before execution
- ✅ Input artifacts loaded successfully
- ✅ Output artifacts written to standard locations
- ✅ Manifest updated with status and outputs
- ✅ Build passes (for code-changing phases)
- ✅ **CPU usage metrics collected** (see Monitoring Requirements below)

### Epic Completion

- ✅ All phases status = `completed`
- ✅ All tickets verified
- ✅ Final review passed
- ✅ `deploy-sync.ps1` executed successfully
- ✅ F5 in NinjaTrader successful

---

## Monitoring Requirements (MANDATORY)

### CPU Usage Tracking

**Purpose**: Track VM resource utilization to validate capacity planning and detect performance issues.

**When to Collect**: At the END of each phase execution (after all agents complete).

**Collection Command**:
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="echo '=== VM Resource Usage ===' && \
    echo 'CPU Info:' && nproc && echo '' && \
    echo 'Load Average (1/5/15 min):' && uptime | grep -oE 'load average: [0-9., ]+' && echo '' && \
    echo 'Memory Usage:' && free -h | grep Mem && echo '' && \
    echo 'Disk Usage:' && df -h /home/malhitticrypto/universal-or-strategy | tail -1"
```

**Metrics to Record**:

| Metric | Description | Example |
|--------|-------------|---------|
| **CPU Cores** | Total vCPUs available | 8 |
| **Load Average** | 1/5/15 minute load | 0.00, 0.06, 0.10 |
| **CPU Utilization** | Load avg ÷ cores × 100% | 1.25% |
| **Memory Total** | Total RAM available | 31 GB |
| **Memory Used** | RAM in use | 337 MB |
| **Memory %** | Used ÷ Total × 100% | 1.1% |
| **Disk Total** | Total disk space | 97 GB |
| **Disk Used** | Disk space in use | 4.3 GB |
| **Disk %** | Used ÷ Total × 100% | 4.4% |
| **Concurrent Agents** | Peak agents running | ~50 |

**Documentation Template**:

Add to each phase completion report:

```markdown
### VM Resource Usage (Phase X)

| Resource | Capacity | Peak Usage | Utilization | Status |
|----------|----------|------------|-------------|--------|
| **CPU Cores** | X vCPU | Y.YY load avg | Z.Z% | ✅/⚠️/❌ |
| **Memory** | XX GB | YYY MB | Z.Z% | ✅/⚠️/❌ |
| **Disk** | XX GB | Y.Y GB | Z.Z% | ✅/⚠️/❌ |
| **Concurrent Agents** | XX max | ~YY peak | ZZ% | ✅/⚠️/❌ |

**Analysis**: [Brief interpretation of resource usage]

**Recommendation**: [Scaling decisions or optimizations]
```

**Status Thresholds**:
- ✅ **Optimal**: CPU <50%, Memory <50%, Disk <50%
- ⚠️ **Warning**: CPU 50-80%, Memory 50-80%, Disk 50-80%
- ❌ **Critical**: CPU >80%, Memory >80%, Disk >80%

**Enforcement**:
- ALL phase completion reports MUST include CPU metrics
- Reports without CPU metrics are INCOMPLETE
- Use metrics to validate VM capacity for next phase

**Reference**: See `WAVE4_PHASE0_COMPLETION_REPORT.md` for example implementation.

---

## Migration from Old Workflow

### Deprecated Command

**`epic-run`** (monolithic workflow) - DO NOT USE

### New Workflow Commands

Use individual phase commands instead (see Command Reference above)

### Migration Guide

See [`docs/workflow/EPIC_WORKFLOW_MIGRATION_GUIDE.md`](./EPIC_WORKFLOW_MIGRATION_GUIDE.md)

---

## References

- **Complete Walkthrough**: [`docs/workflow/EPIC_WORKFLOW_WALKTHROUGH.md`](./EPIC_WORKFLOW_WALKTHROUGH.md)
- **Watsonx Integration**: [`docs/workflow/WATSONX_ORCHESTRATE_INTEGRATION.md`](./WATSONX_ORCHESTRATE_INTEGRATION.md)
- **V12 DNA Protocol**: [`docs/AGENTS.md`](../AGENTS.md)
- **Jane Street Rules**: [`docs/intel/jane-street/RULES_CATALOG.md`](../intel/jane-street/RULES_CATALOG.md)
- **Complexity Rationale**: [`docs/standards/COMPLEXITY_RATIONALE.md`](../standards/COMPLEXITY_RATIONALE.md)

---

**Document Version**: 1.0
**Last Updated**: 2026-06-13T22:12:00Z
**Next Review**: After Wave 3 completion
**Maintainer**: V12 Architecture Team