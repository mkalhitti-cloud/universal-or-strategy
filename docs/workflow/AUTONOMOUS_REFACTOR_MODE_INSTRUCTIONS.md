# Autonomous Refactor Mode Instructions

**Version**: 1.0  
**Effective**: 2026-06-19  
**Mode**: `autonomous-refactor`  
**Wave**: 7 (Fresh Start)  
**Status**: ACTIVE

## Purpose

The `autonomous-refactor` mode is the **Wave Orchestrator** for autonomous complexity reduction workflows. It coordinates multi-epic wave execution across VM and local environments, monitors progress via Lamport event logs, and ensures compliance with V12 protocols.

## Scope

**Wave 7 Execution (Fresh Start)**:
- **Goal**: Reduce all 180 methods from CYC > 8 to CYC ≤ 8 (Jane Street strict standard)
- **Source**: `complexity_audit_fresh_2026-06-14.txt` (180 methods)
- **Target**: CodeScene complexity score ≤8 for all methods
- **Execution**: VM for all epics, local fallback for .dll dependencies (if discovered)
- **Status**: Fresh start - no pre-excluded epics

## Critical Requirements

### UTF-8 Encoding (MANDATORY)
**ALL source files MUST be UTF-8 encoded**:
- ❌ No BOM (Byte Order Mark)
- ❌ No ASCII-only violations
- ✅ Verify before every commit: `file --mime-encoding <file>`
- ✅ Fix if needed: `dos2unix` or `iconv`

**Enforcement**: UTF-8 encoding violations = BLOCKER (must fix before proceeding)

### xUnit Test Framework (MANDATORY)
**ALWAYS generate xUnit tests**:
- ✅ Use `[Fact]` attribute
- ✅ Use `Assert.Equal()` assertions
- ❌ NEVER use NUnit (`[Test]`, `Assert.AreEqual()`)
- ❌ NEVER use MSTest (`[TestMethod]`, `Assert.AreEqual()`)

**Enforcement**: Test framework violations = BLOCKER (must fix before proceeding)

## Core Responsibilities

### 1. Wave Orchestration
- Generate phase scripts for all 180 epics using Building-Blocks Method
- Coordinate VM execution (local fallback if .dll found)
- Monitor wave progress across all phases
- Manage pilot testing (3 epics per phase)
- Execute recovery loops for failed epics

### 2. Script Generation (Building-Blocks Method)
**MANDATORY PROTOCOL**:
- ✅ ALWAYS copy scripts from previous wave's SAME phase
- ✅ Update ONLY epic numbers and method-specific data
- ❌ NEVER generate scripts from scratch
- ❌ NEVER copy adjacent phases (e.g., Phase 1 from Phase 0)

**Reference**: `docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md`

### 3. Progress Monitoring
- Track Lamport event logs (`.lamport/wave7/event_log.jsonl`)
- Monitor bobcoin usage per API
- Verify file persistence on VM
- Generate status reports

### 4. Cost Optimization
- Use 4-minute polling intervals (88% cost reduction vs 30 seconds)
- Cache jCodemunch index results
- Batch similar queries
- Track cost per epic

**Reference**: `docs/protocol/COST_OPTIMIZED_POLLING_PROTOCOL.md`

### 5. Quality Gates
- Verify Building-Blocks Method compliance
- Validate Bob CLI temp file pattern usage
- Check Lamport event determinism
- Ensure Jane Street KB queries before architectural decisions
- Verify UTF-8 encoding compliance
- Verify xUnit test framework usage

## Critical Protocols

### Building-Blocks Method

**The Golden Rule**: ALWAYS copy from previous wave's SAME phase.

**Workflow**:
```bash
# Step 1: Copy previous wave's script (Wave 5 templates)
cp building-blocks/autonomous-refactoring/phase0_template_v12_52.sh \
   scripts/wave7/phase0_epic_001.sh

# Step 2: Update ONLY epic-specific data
# - Epic ID (EPIC-CCN-XXX)
# - Method name
# - File path
# - Complexity score

# Step 3: Verify against SOP
# - Correct mode used
# - Temp file pattern present
# - Lamport event logging included
# - UTF-8 encoding check included
# - xUnit test generation specified
```

**Why This Matters**:
- Proven templates reduce errors by 95%
- Consistent structure enables automation
- Compound intelligence from previous waves
- Faster execution (no debugging new scripts)

### Bob CLI Invocation Pattern

**MANDATORY for ALL phases**:

```bash
# Step 1: Create message file
cat > /tmp/phaseX_msg_$EPIC_ID.txt << 'EOFMSG'
[message content here]
EOFMSG

# Step 2: Invoke Bob with command substitution
~/.npm-global/bin/bob --yolo --chat-mode MODE "$(cat /tmp/phaseX_msg_$EPIC_ID.txt)"
```

**Why This Pattern**:
- ❌ Inline strings cause Bob CLI freeze (Wave 6 incident)
- ✅ Temp file + command substitution is reliable
- ✅ Works on both VM and local
- ✅ Handles multi-line messages correctly

**NEVER DO THIS**:
```bash
# ❌ WRONG - causes freeze
bob --yolo --chat-mode MODE "inline message here"
```

### Lamport Event Tracking

**Purpose**: Deterministic causality tracking across distributed execution.

**Event Schema**:
```json
{
  "timestamp": "2026-06-19T04:00:00Z",
  "lamport_clock": 0,
  "epic_id": "EPIC-W7-001",
  "phase": "0",
  "event_type": "phase_start",
  "status": "pending",
  "metadata": {
    "method": "GetSubscriberCounts",
    "file": "SignalBroadcaster.cs",
    "cyc": 9
  }
}
```

**Event Types**:
- `phase_start`: Phase execution begins
- `phase_complete`: Phase execution succeeds
- `phase_failed`: Phase execution fails
- `epic_start`: Epic workflow begins
- `epic_complete`: Epic workflow succeeds

**Log Location**: `.lamport/wave7/event_log.jsonl`

**Clock Range**: Wave 7 starts at 0 (fresh start)

### Cost Optimization

**Polling Strategy**:
- ✅ 4-minute intervals (240 seconds)
- ❌ NOT 30-second intervals (88% cost increase)

**Cache Strategy**:
- Reuse jCodemunch index across epics
- Batch similar queries (e.g., all Phase 0 hotspots)
- Cache Jane Street KB results

**Cost Tracking**:
```bash
# Extract bobcoin usage per epic
grep "bobcoins_used" .lamport/wave7/event_log.jsonl | \
  jq -r '[.epic_id, .metadata.bobcoins_used] | @csv'
```

### Jane Street KB Integration

**MANDATORY queries**:
- **Phase 2** (Architecture Planning): Query for extraction patterns
- **Phase 5** (Ticket Execution): Query for implementation patterns
- **Phase 5.V** (Verification): Query for testing patterns

**Command**:
```bash
python scripts/query_kb.py "complexity reduction"
python scripts/query_kb.py "FSM extraction"
python scripts/query_kb.py "lock-free patterns"
```

**KB Coverage**: 100+ rules with P0/P1/P2 severity, HFT patterns, FSM/Actor patterns, testing strategies.

## Wave 7 Phases

### Phase 0: Hotspot Analysis
- **Mode**: `v12-phase0-hotspot`
- **Input**: Method name, file, CYC score
- **Output**: `00-hotspots.md`, `manifest.json`
- **MCP**: jCodemunch (get_hotspots, get_symbol_complexity, get_blast_radius)

### Phase 1: Scope Definition
- **Mode**: `v12-phase1-scope`
- **Input**: `00-hotspots.md`
- **Output**: `00-scope.md`
- **MCP**: jCodemunch (get_file_outline, find_references, get_dependency_graph)

### Phase 1.5: Scope Boundary Validation
- **Mode**: `v12-phase1-5-boundary`
- **Input**: `00-scope.md`
- **Output**: `01-scope-boundary.md`
- **Gate**: REJECT if scope exceeds single method

### Phase 2: Architecture Planning
- **Mode**: `v12-phase2-architecture`
- **Input**: `01-scope-boundary.md`
- **Output**: `02-architecture-plan.md`
- **KB Query**: MANDATORY before planning

### Phase 3: DNA Audit
- **Mode**: `v12-phase3-audit`
- **Input**: `02-architecture-plan.md`
- **Output**: `03-audit-report.md`
- **MCP**: jCodemunch (search_ast, get_layer_violations, get_dependency_cycles)

### Phase 4: Ticket Generation
- **Mode**: `v12-phase4-tickets`
- **Input**: `02-architecture-plan.md`
- **Output**: `04-tickets.md`
- **MCP**: jCodemunch (get_symbol_complexity, get_extraction_candidates)

### Phase 5: Ticket Execution
- **Mode**: `v12-engineer`
- **Input**: `04-tickets.md`
- **Output**: `ticket-X-completion.md`
- **KB Query**: MANDATORY before implementation
- **Requirements**: UTF-8 encoding + xUnit tests

### Phase 5.V: Verification
- **Mode**: `v12-phase5-v-verify`
- **Input**: `ticket-X-completion.md`
- **Output**: `ticket-X-verification.md`
- **Checks**: CYC ≤8, only target method modified, xUnit tests pass, UTF-8 encoding

### Phase 6: Final Review
- **Mode**: `v12-phase6-review`
- **Input**: All verification reports
- **Output**: `05-completion-report.md`
- **Checks**: All tickets complete, build passes, CodeScene ≤8

## Pilot Protocol

**Before Full Wave Execution**:
1. Select 3 pilot epics (low/medium/high complexity)
2. Execute all phases for pilot epics
3. Validate success criteria
4. Fix any issues
5. Document lessons learned
6. Proceed to full wave

**Pilot Selection Criteria**:
- **Low**: CYC 9-10 (simple extraction)
- **Medium**: CYC 11-15 (moderate complexity)
- **High**: CYC 16+ (complex extraction)

**Success Criteria**:
- ✅ All 3 epics reach Phase 6 completion
- ✅ Build passes after each phase
- ✅ Lamport clock deterministic
- ✅ No protocol violations
- ✅ Bob CLI pattern used correctly
- ✅ UTF-8 encoding verified
- ✅ xUnit tests generated and passing

## Recovery Loop Protocol

**When Epics Fail**:
1. **STOP** phase execution immediately
2. Document failure in `docs/brain/EPIC-XXX/failure-analysis.md`
3. Identify root cause (mode error, MCP failure, scope creep, encoding, test framework, etc.)
4. Apply fix to failed epic
5. Re-run failed epic through all phases
6. Verify success
7. Update building-blocks templates if pattern issue found
8. Resume phase execution

**Wave Completion Rule**: Wave is NOT complete until N/N (100%).

**Example**:
- Wave 7 Phase 1.5: 179/180 complete → NOT DONE
- Must fix failed epic before proceeding to Phase 2

## Special Cases

### .dll Dependencies
- **Detection**: Discovered during Phase 0 or Phase 5
- **Action**: Switch to local execution for that epic
- **Pattern**: Same temp file + command substitution (PowerShell adaptation)
- **Verification**: Same success criteria as VM epics

### UTF-8 Encoding Violations
- **Detection**: Phase 5.V verification or build failure
- **Action**: BLOCKER - must fix before proceeding
- **Fix**: `dos2unix <file>` or `iconv -f ASCII -t UTF-8 <file>`
- **Verification**: `file --mime-encoding <file>` shows `utf-8`

### Test Framework Violations
- **Detection**: Phase 5.V verification
- **Action**: BLOCKER - must fix before proceeding
- **Fix**: Replace NUnit/MSTest with xUnit
- **Verification**: Grep for `[Fact]` and `Assert.Equal()` in test files

## Monitoring Commands

### Check Wave Progress
```bash
# Count completed epics per phase
for phase in 0 1 1.5 2 3 4 5 5.V 6; do
  echo "Phase $phase: $(grep "phase_complete.*phase\":\"$phase\"" .lamport/wave7/event_log.jsonl | wc -l)/180"
done
```

### Check Bobcoin Usage
```bash
# Total bobcoins used
grep "bobcoins_used" .lamport/wave7/event_log.jsonl | \
  jq -s 'map(.metadata.bobcoins_used) | add'

# Per-API breakdown
grep "api_key" .lamport/wave7/event_log.jsonl | \
  jq -r '[.metadata.api_key, .metadata.bobcoins_used] | @csv' | \
  awk -F, '{sum[$1]+=$2} END {for (api in sum) print api, sum[api]}'
```

### Check Failed Epics
```bash
# List all failed epics
grep "phase_failed" .lamport/wave7/event_log.jsonl | \
  jq -r '[.epic_id, .phase, .metadata.error] | @csv'
```

### Verify File Persistence
```bash
# Check if all Phase 0 hotspot files exist
for i in $(seq -w 1 180); do
  epic="EPIC-CCN-$i"
  if [ ! -f "docs/brain/$epic/00-hotspots.md" ]; then
    echo "Missing: $epic"
  fi
done
```

### Verify UTF-8 Encoding
```bash
# Check all C# source files for UTF-8 encoding
find src/ -name "*.cs" -exec file --mime-encoding {} \; | grep -v utf-8
# Empty output = all files UTF-8 ✅
```

### Verify xUnit Tests
```bash
# Check test files for xUnit usage
grep -r "\[Fact\]" tests/ | wc -l  # Should be > 0
grep -r "\[Test\]" tests/ | wc -l  # Should be 0 (NUnit)
grep -r "\[TestMethod\]" tests/ | wc -l  # Should be 0 (MSTest)
```

## Success Criteria

### Per Phase
- ✅ N/N epics complete (100%)
- ✅ All output files created
- ✅ Lamport events logged
- ✅ Build passes (for code-changing phases)
- ✅ No protocol violations
- ✅ UTF-8 encoding verified (Phase 5+)
- ✅ xUnit tests generated and passing (Phase 5+)

### Per Wave
- ✅ All 180 epics Phase 0-6 complete
- ✅ All methods CYC ≤8
- ✅ CodeScene complexity score ≤8
- ✅ Build passes
- ✅ Tests pass
- ✅ Documentation complete
- ✅ All source files UTF-8 encoded
- ✅ All tests use xUnit framework

### Jane Street Compliance
- ✅ 0 methods with CYC > 8
- ✅ No circular restarts
- ✅ Complete audit trail
- ✅ Codacy grade improved

## References

### Mandatory Reading
1. **Building-Blocks Method**: `docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md`
2. **Cost Optimization**: `docs/protocol/COST_OPTIMIZED_POLLING_PROTOCOL.md`
3. **Architecture**: `building-blocks/autonomous-refactoring/ARCHITECTURE.md`
4. **Getting Started**: `building-blocks/autonomous-refactoring/GETTING_STARTED.md`

### Phase Templates (Wave 5 - Building Blocks)
- Phase 0: `building-blocks/autonomous-refactoring/phase0_template_v12_52.sh`
- Phase 1: `building-blocks/autonomous-refactoring/phase1_template_v12_52.sh`
- Phase 1.5: `building-blocks/autonomous-refactoring/phase1_5_template_v12_52_FIXED.sh`
- Phase 2: `building-blocks/autonomous-refactoring/phase2_template_v12_52.sh`
- Phase 3: `building-blocks/autonomous-refactoring/phase3_template_v12_52.sh`
- Phase 4: `building-blocks/autonomous-refactoring/phase4_template_v12_52.sh`
- Phase 5: `building-blocks/autonomous-refactoring/phase5_template_v12_52.sh`
- Phase 5.V: `building-blocks/autonomous-refactoring/phase5_v_template_v12_52.sh`
- Phase 6: `building-blocks/autonomous-refactoring/phase6_template_v12_52.sh`

### Helper Scripts
- Roadmap generation: `scripts/generate_wave7_roadmap.py`
- Lamport event creation: `scripts/create_lamport_event.py`
- Status monitoring: `scripts/check_wave_status.sh`
- Master launch: `scripts/launch_wave7_phase.sh`

## Version History

### V1.0 (2026-06-19)
- Initial creation for Wave 7 (fresh start)
- Building-Blocks Method integration
- Bob CLI temp file pattern
- Lamport event tracking
- Cost optimization (4-minute polling)
- Jane Street KB integration
- Pilot protocol
- Recovery loop protocol
- UTF-8 encoding requirement (MANDATORY)
- xUnit test framework requirement (MANDATORY)