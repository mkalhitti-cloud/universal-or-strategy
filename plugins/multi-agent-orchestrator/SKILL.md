# Multi-Agent Epic Orchestrator Skill

**Version**: 1.0.0  
**Created**: 2026-06-09  
**Status**: POC Testing  
**Author**: Roo Cline (VS Code Extension)

## Overview

The Multi-Agent Epic Orchestrator is a Bob Shell command that coordinates specialized sub-agents through the V12 epic workflow. Each phase is executed by a dedicated sub-agent with the appropriate mode and capabilities.

## Architecture

### Orchestrator Pattern

```
Bob Orchestrator (Single Session)
├─ Phase 0: Analysis Agent (ask mode)
├─ Phase 1: Planning Agent (plan mode)
├─ Phase 2: Architecture Agent (plan mode)
├─ Phase 3: Review Agent (advanced mode)
├─ Phase 4: Ticket Agent (plan mode)
├─ Phase 5: Implementation Agents (v12-engineer mode, one per ticket)
└─ Phase 6: Completion Agent (advanced mode)
```

### Sub-Agent Specialization

| Agent | Mode | Capabilities | Output |
|-------|------|--------------|--------|
| **Analysis** | `ask` | Read-only analysis | Complexity assessment |
| **Planning** | `plan` | Strategic thinking | Markdown documents |
| **Architecture** | `plan` | Design patterns | Architecture plan |
| **Review** | `advanced` | MCP tools, audits | Audit reports |
| **Ticket** | `plan` | TDD specs | Ticket documents |
| **Implementation** | `v12-engineer` | Code modification | Code changes |
| **Completion** | `advanced` | Verification | Completion report |

### Communication Protocol

**Artifact Passing**:
- Each sub-agent reads previous phase output file
- Writes its own output file
- Updates manifest.json with status

**State Management**:
- Central manifest: `docs/brain/EPIC-{ID}/manifest.json`
- Tracks phase status, artifacts, timestamps
- Enables resume after failure

**F5 Gates**:
- Orchestrator stops after each ticket
- User presses F5 in NinjaTrader
- User confirms BUILD_TAG and zero errors
- Orchestrator proceeds to next ticket

## Usage

### Basic Usage

**In Bob Shell**:
```bash
cd c:/WSGTA/universal-or-strategy
bob --yolo

# In Bob:
/epic-orchestrate EPIC-CCN-21
```

**Orchestrator will**:
1. Spawn Analysis Agent (Phase 0)
2. Spawn Planning Agent (Phase 1)
3. Spawn Architecture Agent (Phase 2)
4. Spawn Review Agent (Phase 3)
5. Spawn Ticket Agent (Phase 4)
6. Spawn Implementation Agent per ticket (Phase 5)
7. Stop at F5 gates
8. Spawn Completion Agent (Phase 6)

### Parallel Orchestrators

**Deploy 3 orchestrators simultaneously**:

**Window 1**:
```bash
cd c:/WSGTA/universal-or-epic-cluster-1
bob --yolo
/epic-orchestrate EPIC-CCN-21
```

**Window 2**:
```bash
cd c:/WSGTA/universal-or-epic-cluster-2
bob --yolo
/epic-orchestrate EPIC-CCN-23
```

**Window 3**:
```bash
cd c:/WSGTA/universal-or-epic-cluster-3
bob --yolo
/epic-orchestrate EPIC-CCN-24
```

Each orchestrator spawns its own sub-agents independently.

## Testing Protocol

### POC Test (First Run)

**Objective**: Verify sub-agent spawning and artifact passing

**Test Epic**: EPIC-CCN-21 (ExecuteFFMAManualMarketEntry, CYC 12)

**Steps**:
1. Launch Bob Shell in main repo
2. Run `/epic-orchestrate EPIC-CCN-21`
3. Observe sub-agent spawning
4. Verify artifact files created
5. Check F5 gate behavior
6. Document findings

**Success Criteria**:
- ✅ Sub-agents spawn successfully
- ✅ Artifacts passed between agents
- ✅ F5 gate stops orchestrator
- ✅ Orchestrator resumes after F5
- ✅ Epic completes successfully

**Failure Scenarios**:
- ❌ Sub-agents don't spawn → Bob Shell doesn't support sub-agents
- ❌ Artifacts not passed → File system communication broken
- ❌ F5 gate doesn't stop → Orchestrator logic error
- ❌ Can't resume → State management broken

### Full Test (After POC Success)

**Objective**: Validate 3 parallel orchestrators

**Test Epics**: EPIC-CCN-21, EPIC-CCN-23, EPIC-CCN-24

**Steps**:
1. Deploy 3 orchestrators in separate worktrees
2. Monitor all 3 simultaneously
3. Coordinate F5 verification across all 3
4. Measure time and Bobcoin usage
5. Compare to manual 3-session baseline

**Success Criteria**:
- ✅ All 3 orchestrators complete successfully
- ✅ F5 coordination works smoothly
- ✅ Time savings ≥10% vs manual
- ✅ Bobcoin savings ≥10% vs manual
- ✅ Zero context compression issues

## Benefits

### Compared to Manual 3-Session Setup

**Advantages**:
- ✅ **Specialization**: Each sub-agent optimized for its phase
- ✅ **Context efficiency**: Fresh context per phase (no compression)
- ✅ **Error isolation**: Failed phase doesn't corrupt other work
- ✅ **Audit trail**: Clear handoffs via file artifacts
- ✅ **Reusability**: Same orchestrator for all 165 epics

**Preserved**:
- ✅ Human F5 verification
- ✅ Interactive questions (sub-agents can ask)
- ✅ YOLO mode auto-approval

### Efficiency Projections

**Manual 3-session baseline** (EPIC-CCN-19, 20, 22):
- Time: 3 hours
- Bobcoins: 209k (35% of budget)
- Epics: 3 complete

**Projected with orchestrators**:
- Time: 2.5 hours (17% faster)
- Bobcoins: ~180k (14% savings)
- Epics: 3 complete
- **Bonus**: Better error handling, clearer audit trail

## Troubleshooting

### Sub-Agents Don't Spawn

**Symptom**: Orchestrator runs as single agent, no sub-agents created

**Cause**: Bob Shell doesn't support sub-agent spawning (feature not available)

**Solution**: Fall back to manual 3-session setup, document limitation

### Artifacts Not Passed

**Symptom**: Sub-agents can't read previous phase output

**Cause**: File paths incorrect or files not created

**Solution**: 
1. Check file paths in orchestrator command
2. Verify files created in correct location
3. Add explicit file existence checks

### F5 Gate Doesn't Stop

**Symptom**: Orchestrator continues without waiting for F5

**Cause**: Stop logic not implemented correctly

**Solution**:
1. Add explicit user prompt after ticket completion
2. Wait for user confirmation before proceeding
3. Test with `/clear` command to reset context

### Context Compression

**Symptom**: Orchestrator compresses context mid-epic

**Cause**: Long conversation history accumulates

**Solution**:
1. Use `/clear` command after each phase
2. Split epic into multiple orchestrator sessions
3. Use checkpoint files for state preservation

## Known Limitations

### Current Limitations (POC Phase)

1. **Sub-agent support unknown**: Bob Shell sub-agent capabilities not yet tested
2. **Error recovery**: No automated recovery sub-agents yet
3. **Parallel tickets**: Tickets executed sequentially (not parallel)
4. **F5 automation**: Still requires manual F5 press

### Future Enhancements

1. **Error recovery sub-agents**: Spawn recovery agent on failure
2. **Parallel ticket execution**: Execute independent tickets simultaneously
3. **Automated F5 coordination**: Batch verification across orchestrators
4. **Cross-orchestrator communication**: Share state between orchestrators

## Files

### Command File
- **Path**: `.bob/commands/epic-orchestrate.md`
- **Type**: Bob Shell slash command
- **Size**: 329 lines
- **Format**: Markdown with YAML frontmatter

### Skill Documentation
- **Path**: `plugins/multi-agent-orchestrator/SKILL.md`
- **Type**: Skill documentation
- **Purpose**: Usage guide and troubleshooting

### Test Results
- **Path**: `plugins/multi-agent-orchestrator/TEST_RESULTS.md`
- **Type**: Test documentation
- **Purpose**: POC and full test results

## Success Metrics

### POC Success
- ✅ Orchestrator spawns sub-agents
- ✅ Artifacts passed between agents
- ✅ F5 gates work correctly
- ✅ Epic completes successfully

### Production Success
- ✅ 3 parallel orchestrators running
- ✅ Time savings ≥10% vs manual
- ✅ Bobcoin savings ≥10% vs manual
- ✅ Zero context compression issues
- ✅ Error handling robust

## Next Steps

### Immediate (POC Test)
1. Run `/epic-orchestrate EPIC-CCN-21` in Bob Shell
2. Observe sub-agent behavior
3. Document findings in TEST_RESULTS.md
4. Refine orchestrator based on results

### After POC Success
1. Deploy 3 parallel orchestrators
2. Monitor execution
3. Measure efficiency gains
4. Update skill documentation

### Future Work
1. Implement error recovery sub-agents
2. Add parallel ticket execution
3. Automate F5 coordination
4. Integrate with Watsonx Orchestrate

## References

- **V12 Epic Workflow**: `docs/workflow/V12_EPIC_WORKFLOW_REFACTORING_DESIGN.md`
- **Parallel Execution**: `docs/workflow/PARALLEL_EPIC_EXECUTION.md`
- **Bob Shell Docs**: `bobshell_docs.md`
- **Epic Roadmap**: `epic_roadmap.json`

## Version History

- **1.0.0** (2026-06-09): Initial POC implementation
  - Created orchestrator command
  - Defined sub-agent architecture
  - Documented testing protocol