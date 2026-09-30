# Wave 7 Task 7 - Master Launch Script (COMPLETE)

**Status**: ✅ COMPLETE  
**Date**: 2026-06-21  
**Task**: Create master launch script for 161 epic autonomous execution

---

## Summary

Task 7 successfully created a comprehensive master launch system for Wave 7 autonomous execution. All scripts, documentation, and monitoring tools are ready for production use.

---

## Deliverables

### 1. Master Launch Script
**File**: `scripts/launch_wave7.sh` (545 lines)

**Features**:
- ✅ Sequential epic launch (161 epics)
- ✅ Two-phase polling strategy
  - Phase 1: First 10 epics @ 1-minute intervals (launch verification)
  - Phase 2: Remaining 151 epics @ 4-minute intervals (cost-optimized)
- ✅ Lamport event monitoring (`.lamport/wave7/event_log.jsonl`)
- ✅ Progress reporting (X/161 complete)
- ✅ Recovery loop for failed epics
- ✅ Cost tracking (per API: Anthropic, OpenAI, Google, Groq)
- ✅ Error detection (UTF-8, xUnit, build failures)
- ✅ Graceful shutdown (Ctrl+C handling)

**Key Functions**:
- `launch_epic_vm()` - Launch single epic on VM
- `monitor_progress()` - Display progress dashboard
- `recovery_loop()` - Handle failed epics
- `record_event()` - Log Lamport events
- `check_vm_connectivity()` - Verify VM access

### 2. Status Check Script
**File**: `scripts/check_wave7_status.sh` (310 lines)

**Features**:
- ✅ Overall progress (completed/running/failed/pending)
- ✅ Progress bar visualization
- ✅ VM status (active sessions, disk usage)
- ✅ Phase distribution (epics per phase)
- ✅ Error analysis (Lamport log failures)
- ✅ Performance metrics (average phase durations)
- ✅ Recent activity (last 10 Lamport events)
- ✅ Recommendations (recovery actions)

**Output Sections**:
1. Overall Progress (with progress bar)
2. VM Status (sessions, disk)
3. Phase Distribution (epics per phase)
4. Error Analysis (failures from Lamport log)
5. Performance Metrics (average durations)
6. Recent Activity (last 10 events)
7. Recommendations (next actions)

### 3. Complete Documentation
**File**: `scripts/WAVE7_LAUNCH_README.md` (502 lines)

**Sections**:
- Overview & Scripts
- Prerequisites (local & VM)
- Execution Workflow (5 steps)
- Monitoring & Observability
- Cost Tracking (per API, expected ~$990)
- Recovery Protocol (automatic & manual)
- Success Criteria (wave & quality gates)
- Troubleshooting (5 common issues)
- References (8 key documents)

### 4. Quick Reference Card
**File**: `scripts/WAVE7_QUICK_REFERENCE.md` (139 lines)

**Sections**:
- Launch Commands
- Monitoring Commands
- Debugging Commands
- Recovery Commands
- Post-Wave Commands
- Progress Tracking
- Success Criteria Checklist
- Emergency Commands

---

## Technical Implementation

### Polling Strategy (Per COST_OPTIMIZED_POLLING_PROTOCOL.md)

**Phase 1: Launch Verification**
- First 10 epics
- 1-minute polling intervals
- Purpose: Catch immediate failures
- Duration: ~10 minutes
- Success: All 10 epics progress past Phase 0

**Phase 2: Full Wave Execution**
- Remaining 151 epics
- 4-minute polling intervals
- Purpose: Monitor progress with cache optimization
- Duration: ~8-12 hours (varies by complexity)
- Success: 161/161 epics complete

**Cost Optimization**:
- 4-minute intervals = 88% cost reduction vs 30-second polling
- Stays within 5-minute cache window (Vertex AI, Anthropic, OpenAI)
- Cache hits = 90% of polls after first
- Parallel execution = zero polling overhead

### Lamport Event Integration

**Event Types Logged**:
- `wave_start` - Wave 7 execution begins
- `epic_start` - Epic begins (Phase 0 start)
- `phase_start` - Phase execution begins
- `phase_complete` - Phase execution succeeds
- `phase_fail` - Phase execution fails
- `epic_complete` - Epic completes (Phase 6 complete)
- `wave_complete` - All 161 epics complete

**Event Schema**:
```json
{
  "clock": 123,
  "event_type": "phase_start",
  "epic_id": "EPIC-W7-001",
  "phase": "0",
  "agent_id": "wave7-phase0-001",
  "status": "running",
  "state_hash": "abc123...",
  "data": {},
  "timestamp": "2026-06-21T20:00:00Z"
}
```

### Recovery Loop Protocol

**Automatic Detection**:
1. Poll detects failed epic via manifest status
2. Check for `failure-analysis.md`
3. Create template if missing
4. Log warning and pause wave
5. Wait for manual intervention

**Manual Recovery**:
1. Identify failed epic
2. Analyze failure (UTF-8, xUnit, build, scope)
3. Fix issue
4. Re-run epic
5. Verify completion

**Recovery Loop Triggers**:
- Failed epic count > 0
- No progress for >1 hour
- Error count spike in Lamport log

---

## Critical Requirements Enforced

### 1. UTF-8 Encoding (P0 Blocker)
- ALL source files MUST be UTF-8 (no BOM)
- Verified before every commit
- Pre-push validation check #1

### 2. xUnit Test Framework (P0 Blocker)
- ALWAYS generate xUnit tests
- NEVER use NUnit or MSTest
- Pre-push validation check #3

### 3. Building-Blocks Method (Mandatory)
- ALWAYS copy scripts from previous wave's SAME phase
- NEVER generate scripts from scratch
- Update ONLY epic numbers and method-specific data

### 4. Bob CLI Pattern (Mandatory)
- Two-step invocation (temp file + command substitution)
- NEVER inline strings (causes freeze)
- Full path to Bob CLI binary

### 5. Branch Strategy (Critical Update)
- Wave 7 executes on `main` branch ONLY
- NO `gitbutler/workspace` for Wave 7
- Local: `main`, VM: `main`, GitHub: `origin/main`

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
- ✅ Cost within budget (~$990)

### Quality Gates
- ✅ Pre-push validation passes (13/13 checks)
- ✅ CodeScene score ≤8 for all files
- ✅ Codacy grade maintained or improved
- ✅ Zero compilation errors
- ✅ Zero test failures
- ✅ Zero scope creep violations

---

## Usage Instructions

### Quick Start

```bash
# 1. Launch Wave 7 (all 161 epics)
bash scripts/launch_wave7.sh

# 2. Monitor progress
bash scripts/check_wave7_status.sh

# 3. Watch continuously (every 4 minutes)
watch -n 240 bash scripts/check_wave7_status.sh
```

### Pre-Flight Checks

```bash
# Verify VM connectivity
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="echo 'VM accessible'"

# Verify roadmap exists
ls -la epic_roadmap_wave7.json

# Verify templates exist
ls -la building-blocks/wave7/phase*_template_wave7.sh

# Verify Lamport infrastructure
ls -la .lamport/wave7/
```

### Post-Wave Validation

```bash
# Sync from VM to local
powershell -File .\deploy-sync.ps1

# Run pre-push validation (all 13 checks)
powershell -File .\scripts\pre_push_validation.ps1

# Verify complexity reduction
python scripts/complexity_audit.py

# Test in NinjaTrader
# 1. Open NinjaTrader
# 2. Press F5 (compile)
# 3. Verify zero errors
```

---

## Expected Timeline

### Phase 1: Launch Verification (First 10 Epics)
- Launch: 2 minutes (staggered)
- Execution: 8 minutes (Phase 0-1)
- Polling: 10 minutes (1-min intervals)
- **Total**: ~20 minutes

### Phase 2: Full Wave Execution (Remaining 151 Epics)
- Launch: 5 minutes (staggered)
- Execution: 8-12 hours (varies by complexity)
- Polling: Parallel (4-min intervals)
- **Total**: ~8-12 hours

### Post-Wave Validation
- Sync: 10 minutes
- Pre-push validation: 15 minutes
- Complexity audit: 5 minutes
- NinjaTrader test: 5 minutes
- **Total**: ~35 minutes

**Grand Total**: ~9-13 hours (end-to-end)

---

## Cost Estimate

### Per Phase (161 epics)

| Phase | Duration | Polls | Cache Hits | Cost |
|-------|----------|-------|------------|------|
| 0 | 30 min | 8 | 7 | ~$50 |
| 1 | 45 min | 12 | 11 | ~$75 |
| 1.5 | 30 min | 8 | 7 | ~$40 |
| 2 | 75 min | 19 | 18 | ~$125 |
| 3 | 30 min | 8 | 7 | ~$60 |
| 4 | 30 min | 8 | 7 | ~$40 |
| 5 | 180 min | 45 | 44 | ~$400 |
| 5.V | 90 min | 23 | 22 | ~$120 |
| 6 | 30 min | 8 | 7 | ~$80 |

**Total**: ~$990 (estimated)

**Cost Optimization**: 88% reduction vs 30-second polling

---

## Next Steps

1. ✅ **Task 7 Complete** - Master launch scripts created
2. ⏭️ **User Review** - Review scripts and documentation
3. ⏭️ **Pre-Flight Checks** - Verify VM, roadmap, templates
4. ⏭️ **Launch Wave 7** - Execute `bash scripts/launch_wave7.sh`
5. ⏭️ **Monitor Progress** - Use `check_wave7_status.sh`
6. ⏭️ **Handle Failures** - Apply recovery loop if needed
7. ⏭️ **Verify Completion** - Confirm 161/161 complete
8. ⏭️ **Post-Wave Validation** - Sync, validate, test
9. ⏭️ **Create PR** - Submit Wave 7 for review

---

## References

- **Launch Scripts**: `scripts/launch_wave7.sh`, `scripts/check_wave7_status.sh`
- **Documentation**: `scripts/WAVE7_LAUNCH_README.md`, `scripts/WAVE7_QUICK_REFERENCE.md`
- **Execution Plan**: `docs/workflow/WAVE7_EXECUTION_PLAN.md`
- **Cost Protocol**: `docs/protocol/COST_OPTIMIZED_POLLING_PROTOCOL.md`
- **Lamport Events**: `.lamport/wave7/README.md`
- **Epic Roadmap**: `epic_roadmap_wave7.json`
- **Templates**: `building-blocks/wave7/`

---

**Task 7 Status**: ✅ COMPLETE  
