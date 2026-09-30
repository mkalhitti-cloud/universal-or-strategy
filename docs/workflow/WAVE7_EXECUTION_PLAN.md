# Wave 7 Execution Plan

**Version**: 1.0  
**Created**: 2026-06-21  
**Status**: Ready for Execution  
**Total Epics**: 161 (ALL VM-safe)

---

## Executive Summary

Wave 7 targets **161 methods** with cyclomatic complexity > 8, reducing them to ≤8 (Jane Street strict standard). Analysis confirms **ALL epics can execute on VM** - no DLL dependencies or local-only requirements detected.

### Key Findings

✅ **VM Execution**: All 161 epics VM-safe  
✅ **No DLL Dependencies**: Zero NinjaTrader-specific blockers  
✅ **No UTF-8 Violations**: Clean baseline (will verify during execution)  
✅ **Templates Fixed**: Phase 1.5 freeze issue resolved (bob CLI path)  
✅ **Lamport Clock**: Event tracking infrastructure ready  

### Special Cases Identified

- **2 epics** with external dependencies (network/stream operations)
  - EPIC-W7-134: ProcessOnConnectionStatusUpdate
  - EPIC-W7-156: ProcessClientStream
- **No blockers** - can execute on VM with standard monitoring

---

## Polling Strategy (Per User Requirements)

### Phase 1: Launch Verification (First 10 Epics)

**Polling Interval**: **1 minute**

**Purpose**: Verify wave launch succeeds before scaling up

**Monitoring**:
- Check Lamport events for phase_start events
- Verify no freeze/hang conditions
- Confirm Bob CLI invocations succeed
- Check for UTF-8 encoding errors
- Verify xUnit test generation (not NUnit/MSTest)

**Success Criteria**:
- 10/10 epics launch successfully
- No frozen sessions
- All Lamport events logged
- Build passes on VM

**Failure Protocol**:
- STOP wave immediately
- Analyze failure in Lamport events
- Fix issue before proceeding
- Re-launch first 10 epics

### Phase 2: Full Wave Execution (Remaining 151 Epics)

**Polling Interval**: **4 minutes** (cost-optimized)

**Purpose**: Monitor progress across all 161 epics

**Monitoring**:
- Track completion via Lamport events
- Monitor bobcoin usage per API
- Check for errors in event logs
- Verify file persistence
- Track phase progression (0 → 1 → 1.5 → 2 → 3 → 4 → 5 → 5.V → 6)

**Success Criteria**:
- 161/161 epics complete (100%)
- All phases pass for all epics
- Build passes after sync
- F5 in NinjaTrader succeeds

**Recovery Loop**:
- Failed epics MUST be fixed before wave completion
- Document failures in `docs/brain/EPIC-W7-XXX/failure-analysis.md`
- Apply fixes and re-run failed epics
- Wave is NOT complete until 161/161

---

## Critical Requirements (ALL Epics)

### 1. UTF-8 Encoding (P0 Blocker)

**Requirement**: ALL source files MUST be UTF-8 encoded

**Enforcement**:
- No BOM (Byte Order Mark)
- No ASCII-only violations
- Verify before every commit
- Pre-push validation check #1

**Detection**:
```powershell
# Check for non-UTF-8 files
Get-ChildItem src/ -Recurse -Include *.cs | ForEach-Object {
    $content = [System.IO.File]::ReadAllBytes($_.FullName)
    if ($content[0] -eq 0xEF -and $content[1] -eq 0xBB -and $content[2] -eq 0xBF) {
        Write-Host "BOM detected: $($_.FullName)"
    }
}
```

**Violation = BLOCKER**: Epic cannot proceed until fixed

### 2. xUnit Test Framework (P0 Blocker)

**Requirement**: ALWAYS generate xUnit tests ONLY

**Correct Pattern**:
```csharp
using Xunit;

public class MyTests
{
    [Fact]
    public void TestMethod()
    {
        Assert.Equal(expected, actual);
    }
}
```

**BANNED Patterns**:
```csharp
// ❌ NEVER use NUnit
[Test]
public void TestMethod() { }

// ❌ NEVER use MSTest
[TestMethod]
public void TestMethod() { }
```

**Enforcement**:
- Pre-push validation check #3
- Bob CLI mode `v12-engineer` enforces xUnit
- Violation = P0 blocker

**Reference**: `docs/protocol/TEST_FRAMEWORK_PROTOCOL.md`

### 3. Building-Blocks Method (Mandatory)

**Requirement**: ALWAYS copy scripts from previous wave, NEVER generate from scratch

**Correct Workflow**:
1. Identify phase (0, 1, 1.5, 2, 3, 4, 5, 5.V, 6)
2. Copy template from `building-blocks/wave7/phaseX_template_wave7.sh`
3. Update ONLY epic-specific parameters:
   - `EPIC_ID` (e.g., EPIC-W7-042)
   - Method name
   - File path
   - Complexity value
4. Verify against SOP V3 checklist
5. Execute

**BANNED**:
- ❌ Generating scripts from scratch
- ❌ Copying from adjacent phases (e.g., Phase 1 from Phase 0)
- ❌ Inline bob invocations (causes freeze)

**Reference**: `docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md`

### 4. Bob CLI Invocation Pattern (Mandatory)

**Correct Pattern** (Two-Step):
```bash
# Step 1: Create message file
cat > /tmp/phaseX_msg_$EPIC_ID.txt << 'EOFMSG'
[message content]
EOFMSG

# Step 2: Invoke Bob with command substitution
~/.npm-global/bin/bob --yolo --chat-mode MODE "$(cat /tmp/phaseX_msg_$EPIC_ID.txt)"
```

**BANNED Pattern** (Causes Freeze):
```bash
# ❌ NEVER use inline strings
bob --yolo --chat-mode MODE "inline message here"
```

**Critical**: Missing `~/.npm-global/bin/` prefix causes indefinite hang

**Reference**: SOP V3 Section 589-678

---

## Execution Phases

### Phase 0: Hotspot Analysis
- **Mode**: `ask`
- **Purpose**: Identify complexity hotspots
- **Output**: `00-hotspots.md`
- **Duration**: ~2 minutes per epic

### Phase 1: Scope Definition
- **Mode**: `plan`
- **Purpose**: Define refactoring scope
- **Output**: `00-scope.md`
- **Duration**: ~3 minutes per epic

### Phase 1.5: Scope Boundary Validation (MANDATORY GATE)
- **Mode**: `plan`
- **Purpose**: Prevent scope creep
- **Output**: `01-scope-boundary.md`
- **Duration**: ~2 minutes per epic
- **Critical**: Must validate scope before architecture planning

### Phase 2: Architecture Planning
- **Mode**: `plan`
- **Purpose**: Design extraction strategy
- **Output**: `02-architecture-plan.md`, `02-diagrams.mmd`
- **Duration**: ~5 minutes per epic
- **Jane Street KB**: Query before planning

### Phase 3: DNA & PR Audit
- **Mode**: `advanced`
- **Purpose**: Audit plan against V12 DNA
- **Output**: `03-audit-report.md`
- **Duration**: ~3 minutes per epic

### Phase 4: Ticket Generation
- **Mode**: `plan`
- **Purpose**: Generate implementation tickets
- **Output**: `04-tickets.md`
- **Duration**: ~2 minutes per epic

### Phase 4.5: Ticket Review (Jane Street Gate)
- **Mode**: `advanced`
- **Purpose**: Validate tickets against Jane Street standards
- **Output**: `04-ticket-review.md`
- **Duration**: ~2 minutes per epic

### Phase 5: Ticket Execution
- **Mode**: `v12-engineer` (Bob CLI)
- **Purpose**: Surgical refactoring
- **Output**: `ticket-X-completion.md`
- **Duration**: ~10 minutes per ticket (varies by complexity)
- **Jane Street KB**: Query before implementation

### Phase 5.V: Per-Ticket Verification
- **Mode**: `advanced`
- **Purpose**: Verify ticket completion
- **Output**: `ticket-X-verification.md`
- **Duration**: ~3 minutes per ticket
- **Jane Street KB**: Query for testing patterns

### Phase 6: Final Review
- **Mode**: `advanced`
- **Purpose**: Epic completion report
- **Output**: `05-completion-report.md`
- **Duration**: ~5 minutes per epic

---

## Monitoring & Observability

### Lamport Event Tracking

**Event Log**: `.lamport/wave7/event_log.jsonl`

**Event Types**:
- `wave_start`: Wave 7 execution begins
- `phase_start`: Epic phase begins
- `phase_complete`: Epic phase completes successfully
- `phase_fail`: Epic phase fails (triggers recovery)
- `wave_complete`: All 161 epics complete

**Event Schema**:
```json
{
  "timestamp": "2026-06-21T15:00:00.000Z",
  "lamport_clock": 42,
  "epic_id": "EPIC-W7-042",
  "phase": "1.5",
  "event_type": "phase_complete",
  "status": "success",
  "metadata": {
    "duration_seconds": 120,
    "output_file": "docs/brain/EPIC-W7-042/01-scope-boundary.md"
  }
}
```

**Query Tools**:
- `scripts/filter_wave7_events.py` - Extract Wave 7 events
- `scripts/generate_wave7_stats.py` - Generate statistics
- `scripts/verify_wave7_determinism.py` - Verify determinism

### Progress Dashboard

**Real-Time Metrics**:
- Epics completed: X/161
- Current phase distribution
- Average time per phase
- Failure rate
- Bobcoin usage (per API)

**Query**:
```bash
python scripts/generate_wave7_stats.py
```

### Cost Tracking

**Bobcoin APIs**:
- Anthropic (Claude)
- OpenAI (GPT-4)
- Google (Gemini)
- Groq (Llama)

**Monitoring**:
- Track usage per API
- Identify cost spikes
- Optimize polling intervals
- Reference: `docs/protocol/COST_OPTIMIZED_POLLING_PROTOCOL.md`

---

## Recovery Loop Protocol

### Failure Detection

**Triggers**:
- Phase fails (status = `failed` in Lamport event)
- Build fails after sync
- UTF-8 encoding violation
- xUnit test framework violation
- Timeout (phase exceeds 30 minutes)

### Recovery Steps

1. **STOP Wave**: Pause execution immediately
2. **Analyze Failure**: Review Lamport events and logs
3. **Document**: Create `docs/brain/EPIC-W7-XXX/failure-analysis.md`
4. **Fix Issue**: Apply targeted fix (separate session)
5. **Update Manifest**: Set phase status to `pending`
6. **Re-Run Phase**: Execute failed phase only
7. **Verify**: Confirm success before proceeding
8. **Resume Wave**: Continue with next epic

### Failure Categories

**Category 1: Transient (Retry)**
- Network timeout
- API rate limit
- VM resource contention

**Category 2: Configuration (Fix & Retry)**
- Missing environment variable
- Incorrect file path
- Permission issue

**Category 3: Code Issue (Manual Fix)**
- Compilation error
- Test failure
- Logic bug

**Category 4: Protocol Violation (Blocker)**
- UTF-8 encoding violation
- xUnit framework violation
- Scope creep detected

---

## Pre-Execution Checklist

### Infrastructure

- [ ] VM accessible (ssh malhitticrypto@34.60.155.195)
- [ ] Build passes on VM (`dotnet build`)
- [ ] Git status clean (no uncommitted `src/` changes)
- [ ] Branch strategy: GitButler virtual branches active
- [ ] jCodemunch index current
- [ ] Lamport clock infrastructure ready

### Templates

- [ ] All 9 templates copied to `building-blocks/wave7/`
- [ ] Bob CLI path fixed (`~/.npm-global/bin/bob`)
- [ ] Temp file pattern verified
- [ ] Verification script passes (`scripts/verify_wave7_templates.py`)

### Documentation

- [ ] SOP V3 reviewed (`docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md`)
- [ ] Cost protocol reviewed (`docs/protocol/COST_OPTIMIZED_POLLING_PROTOCOL.md`)
- [ ] Jane Street KB accessible (`scripts/query_kb.py`)
- [ ] Special cases analysis complete (`docs/workflow/WAVE7_SPECIAL_CASES_ANALYSIS.md`)

### Monitoring

- [ ] Lamport event log initialized (`.lamport/wave7/event_log.jsonl`)
- [ ] Progress dashboard ready (`scripts/generate_wave7_stats.py`)
- [ ] Cost tracking enabled
- [ ] Recovery loop protocol understood

---

## Pilot Test (3 Epics)

**Purpose**: Validate full workflow before scaling to 161 epics

**Selection Criteria**:
- 1 low complexity (CYC 9-12)
- 1 medium complexity (CYC 13-16)
- 1 high complexity (CYC 17-20)

**Pilot Epics** (TBD):
- EPIC-W7-XXX (low)
- EPIC-W7-YYY (medium)
- EPIC-W7-ZZZ (high)

**Success Criteria**:
- All 3 epics complete Phases 0-6
- Build passes after sync
- F5 in NinjaTrader succeeds
- Lamport events logged correctly
- No UTF-8 or xUnit violations

**Failure Protocol**:
- Fix issues before full wave
- Update templates if needed
- Document lessons learned

---

## Post-Wave Validation

### Build Verification

```powershell
# Sync to local
powershell -File .\deploy-sync.ps1

# Run pre-push validation (all 13 checks)
powershell -File .\scripts\pre_push_validation.ps1

# Verify NinjaTrader
# 1. Open NinjaTrader
# 2. Press F5 (compile)
# 3. Verify zero errors
```

### Complexity Verification

```bash
# Re-run complexity audit
python scripts/complexity_audit.py

# Verify all 161 methods now ≤8
# Compare against baseline: complexity_audit_fresh_2026-06-14.txt
```

### CodeScene Verification

**Target**: Code Health Score ≤8 for all refactored files

**Process**:
1. Open refactored files in VS Code
2. Check CodeScene status bar
3. Verify green/yellow (not red)
4. Document improvements

---

## Success Criteria

### Wave Completion

- ✅ 161/161 epics complete (100%)
- ✅ All phases pass for all epics
- ✅ Build passes on VM and local
- ✅ F5 in NinjaTrader succeeds
- ✅ All methods CYC ≤8
- ✅ Zero UTF-8 violations
- ✅ Zero xUnit framework violations
- ✅ Lamport events logged for all phases
- ✅ Cost within budget

### Quality Gates

- ✅ Pre-push validation passes (13/13 checks)
- ✅ CodeScene score ≤8 for all files
- ✅ Codacy grade maintained or improved
- ✅ Zero compilation errors
- ✅ Zero test failures
- ✅ Zero scope creep violations

---

## Launch Scripts (Task 7 - COMPLETE)

**Status**: ✅ Master launch scripts created

### Scripts Created

1. **`scripts/launch_wave7.sh`** - Master launch script
   - Launches all 161 epics sequentially
   - Two-phase polling (1-min → 4-min)
   - Lamport event monitoring
   - Progress reporting
   - Recovery loop for failures
   - Cost tracking
   - Graceful shutdown

2. **`scripts/check_wave7_status.sh`** - Status monitoring
   - Overall progress dashboard
   - VM status (sessions, disk)
   - Phase distribution
   - Error analysis
   - Performance metrics
   - Recent activity
   - Recommendations

3. **`scripts/WAVE7_LAUNCH_README.md`** - Complete documentation
   - Usage instructions
   - Execution workflow
   - Monitoring & observability
   - Cost tracking
   - Recovery protocol
   - Troubleshooting guide

4. **`scripts/WAVE7_QUICK_REFERENCE.md`** - Quick command reference
   - Launch commands
   - Monitoring commands
   - Debugging commands
   - Recovery commands
   - Emergency procedures

### Quick Start

```bash
# Launch Wave 7 (all 161 epics)
bash scripts/launch_wave7.sh

# Monitor progress
bash scripts/check_wave7_status.sh

# Watch continuously (every 4 minutes)
watch -n 240 bash scripts/check_wave7_status.sh
```

---

## Next Steps

1. **User**: Review launch scripts and documentation
2. **User**: Run pre-flight checks (VM connectivity, roadmap, templates)
3. **User**: Launch Wave 7: `bash scripts/launch_wave7.sh`
4. **User**: Monitor progress: `bash scripts/check_wave7_status.sh`
5. **User**: Handle failures (if any) via recovery loop
6. **User**: Verify 161/161 completion
7. **User**: Sync to local: `powershell -File .\deploy-sync.ps1`
8. **User**: Run pre-push validation: `powershell -File .\scripts\pre_push_validation.ps1`
9. **User**: Test in NinjaTrader (F5)
10. **User**: Create PR for Wave 7

---

## References

- **Launch Scripts**: `scripts/launch_wave7.sh`, `scripts/check_wave7_status.sh`
- **Documentation**: `scripts/WAVE7_LAUNCH_README.md`, `scripts/WAVE7_QUICK_REFERENCE.md`
- **SOP V3**: `docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md`
- **Cost Protocol**: `docs/protocol/COST_OPTIMIZED_POLLING_PROTOCOL.md`
- **Lamport Clock**: `.lamport/wave7/README.md`
- **Special Cases**: `docs/workflow/WAVE7_SPECIAL_CASES_ANALYSIS.md`
- **Template Status**: `docs/workflow/WAVE7_TEMPLATE_STATUS_REPORT.md`
- **Architecture**: `building-blocks/autonomous-refactoring/ARCHITECTURE.md`
- **Getting Started**: `building-blocks/autonomous-refactoring/GETTING_STARTED.md`
- **Jane Street KB**: `docs/intel/jane-street/` (100+ rules)
- **Test Framework**: `docs/protocol/TEST_FRAMEWORK_PROTOCOL.md`

---

**END OF EXECUTION PLAN**