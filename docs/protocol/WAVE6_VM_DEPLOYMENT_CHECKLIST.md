# Wave 6 VM Deployment Checklist

**Version**: V12.52
**Date**: 2026-06-17
**Target**: v12-test-golden-v2 (GCP VM)
**Wave**: 6 (79 epics, rollback/repeat)

---

## Critical Updates Required

### 1. Custom Modes (`.bob/custom_modes.yaml`)
**Changes**:
- ❌ Remove Greptile MCP from all phases
- ✅ Add Graphify MCP to all phases (shows relationships across 70+ files)
- ✅ Threshold 15→8 (already done locally)
- ✅ Phase-specific (not wave-specific) (already done locally)

**Rationale**:
- Greptile: Not needed for autonomous execution
- Graphify: Essential for understanding codebase relationships (70+ files)

### 2. API Rotation (`docs/API/api_rotation.json`)
**Changes**:
- ✅ Add davidgreen77 API (160 bobcoins)
- Total: 5 APIs, 800 bobcoins

### 3. Threshold Correction
**Files to Update on VM**:
- `AGENTS.md` - 15→8
- `.codacy.yml` - 15→8
- `.coderabbit.yaml` - 15→8
- `.codeant.yml` - 15→8
- `scripts/pre_push_validation.ps1` - 15→8
- `.bob/custom_modes.yaml` - 15→8

### 4. MCP Configuration (`.bob/mcp.json`)
**Verify**:
- jCodemunch MCP server configured
- Sequential Thinking MCP server configured
- Graphify MCP server configured
- ❌ Greptile MCP server REMOVED

### 5. Hooks Configuration (`.bob/hooks.json`)
**Verify**:
- GitButler integration enabled
- `before_new_task` hook enabled
- `after_task_complete` hook enabled

### 6. Wave 6 Manifests
**Status**: 101 manifests reset to pending (local)
**Action**: Sync to VM after local changes deployed

---

## Deployment Steps

### Step 1: Local Preparation (CURRENT MACHINE)

**1.1 Update Custom Modes**
```bash
# Remove Greptile, add Graphify to all phases
# Edit .bob/custom_modes.yaml
```

**1.2 Verify Local Files**
```bash
# Check threshold corrections
grep -r "15" AGENTS.md .codacy.yml .coderabbit.yaml .codeant.yml scripts/pre_push_validation.ps1 .bob/custom_modes.yaml

# Should return 0 results (all should be 8)
```

**1.3 Commit Local Changes**
```bash
git add .bob/custom_modes.yaml docs/API/api_rotation.json AGENTS.md .codacy.yml .coderabbit.yaml .codeant.yml scripts/pre_push_validation.ps1
git commit -m "V12.52: Wave 6 protocol alignment - threshold 8, Graphify all phases, remove Greptile"
```

### Step 2: VM Sync (7-STEP PROTOCOL)

**2.1 SSH to VM**
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a
```

**2.2 Navigate to Repo**
```bash
cd /home/malhitticrypto/universal-or-strategy
```

**2.3 Check Git Status**
```bash
git status
# If dirty: stash or commit
```

**2.4 Pull Latest**
```bash
git fetch origin
git pull origin main
```

**2.5 Verify Files Updated**
```bash
# Check custom modes
grep -A 5 "MANDATORY MCP TOOLS" .bob/custom_modes.yaml | head -20

# Check API rotation
cat docs/API/api_rotation.json | grep davidgreen77

# Check threshold
grep "threshold" .codacy.yml
```

**2.6 Verify MCP Configuration**
```bash
cat ~/.config/bob/mcp.json
# Should have: jCodemunch, Sequential Thinking, Graphify
# Should NOT have: Greptile
```

**2.7 Test MCP Connectivity**
```bash
# Test jCodemunch
bob --mode v12-phase0-hotspot --test-mcp jcodemunch-mcp

# Test Sequential Thinking
bob --mode v12-phase0-hotspot --test-mcp sequential-thinking

# Test Graphify
bob --mode v12-phase2-architecture --test-mcp graphify
```

### Step 3: Smoke Test (EPIC-CCN-001 Phase 0)

**3.1 Launch Single Phase**
```bash
cd /home/malhitticrypto/universal-or-strategy
bash scripts/wave6/p0_epic_ccn_001.sh 2>&1 | tee logs/wave6_p0_001_pilot.log
```

**3.2 Verify Outputs**
```bash
# Check manifest updated
cat docs/brain/EPIC-CCN-001/manifest.json | grep -A 5 '"0"'

# Check hotspots file created
ls -lh docs/brain/EPIC-CCN-001/00-hotspots.md

# Check Graphify used
grep -i "graphify" logs/wave6_p0_001_pilot.log

# Check Sequential Thinking used
grep -i "sequential" logs/wave6_p0_001_pilot.log

# Check NO Greptile
grep -i "greptile" logs/wave6_p0_001_pilot.log
# Should return 0 results
```

**3.3 Verify Bobcoins**
```bash
# Check bobcoin usage in log
grep -i "bobcoin" logs/wave6_p0_001_pilot.log

# Check API rotation
grep -i "api" logs/wave6_p0_001_pilot.log
```

### Step 4: Full Wave 6 Launch

**4.1 Launch All Phases (79 Epics)**
```bash
# Use master launch script
bash scripts/wave6/launch_wave6_all_phases.sh 2>&1 | tee logs/wave6_master.log
```

**4.2 Monitor Progress**
```bash
# Watch logs
tail -f logs/wave6_master.log

# Check epic status
python scripts/check_wave6_status.py
```

---

## Wave 6 Specifications

### Epic Count
**Total**: 79 epics (NOT 101)
- Wave 4: 80 epics
- Wave 5: 79 epics
- Wave 6: 79 epics (rollback/repeat)

**Source**: `building-blocks/autonomous-refactoring/V12_52_TEMPLATE_USAGE.md` line 256

### Execution Context
**Primary**: GCP VM (v12-test-golden-v2)
**Exception**: Special cases (local execution when VM unavailable)

### Phase Workflow
**Phases**: 10 (0, 1, 1.5, 2, 3, 4, 4.5, 5, 5.V, 6)
**Protocol**: V12.52 Lamport Causal Verification
**Manifest**: `docs/brain/EPIC-{ID}/manifest.json`

---

## MCP Tool Matrix (Updated)

| Phase | jCodemunch | Sequential Thinking | Graphify | Greptile |
|-------|------------|---------------------|----------|----------|
| 0 | ✅ | ✅ | ✅ | ❌ |
| 1 | ✅ | ✅ | ✅ | ❌ |
| 1.5 | ✅ | ✅ | ✅ | ❌ |
| 2 | ✅ | ✅ | ✅ | ❌ |
| 3 | ✅ | ✅ | ✅ | ❌ |
| 4 | ✅ | ✅ | ✅ | ❌ |
| 4.5 | ❌ | ✅ | ✅ | ❌ |
| 5 | ✅ | ✅ | ✅ | ❌ |
| 5.V | ✅ | ✅ | ✅ | ❌ |
| 6 | ✅ | ✅ | ✅ | ❌ |

**Graphify Rationale**: Shows relationships across 70+ files - essential for all phases
**Greptile Removal**: Not needed for autonomous execution

---

## Hooks Integration

### Current Status
**File**: `.bob/hooks.json`
**Hooks Enabled**:
- `before_new_task`: Auto-create GitButler virtual branch
- `after_task_complete`: Auto-commit with V12 message format

### Autonomous-Refactor Command Integration
**Question**: Are hooks automatically invoked by `/autonomous-refactor` command?
**Answer**: NO - hooks are invoked by Bob CLI task lifecycle, not by slash commands

**Clarification**:
- Hooks trigger on Bob CLI events (new task, task complete, edit)
- Slash commands (`/autonomous-refactor`, `/epic-run`) are workflow orchestrators
- Slash commands delegate to custom modes (which run in Bob CLI)
- Custom modes trigger hooks when they complete tasks

**Flow**:
```
/autonomous-refactor (orchestrator)
  ↓
  Calls /epic-run for each epic
    ↓
    /epic-run delegates to phase-specific custom modes
      ↓
      Custom mode executes in Bob CLI
        ↓
        Bob CLI triggers hooks (before_new_task, after_task_complete)
```

---

## Success Criteria

### Smoke Test (EPIC-CCN-001 Phase 0)
- [ ] Manifest updated (Phase 0 status = completed)
- [ ] 00-hotspots.md created
- [ ] Graphify used (log shows graphify calls)
- [ ] Sequential Thinking used (log shows sequentialthinking calls)
- [ ] NO Greptile (log has zero greptile mentions)
- [ ] Bobcoins tracked (log shows usage)
- [ ] API rotation working (davidgreen77 used)

### Full Wave 6
- [ ] 79 epics executed
- [ ] All phases completed (0 through 6)
- [ ] Complexity target achieved (CYC ≤ 8)
- [ ] Bobcoin budget managed (800 total, ~50-80 per epic)
- [ ] No P0 blockers introduced
- [ ] F5 verification passed

---

## Rollback Plan

If Wave 6 fails:
1. Document failure in `docs/brain/WAVE6_FAILURE_ANALYSIS.md`
2. Rollback VM to pre-Wave 6 state
3. Fix issues locally
4. Re-deploy with updated protocol
5. Retry Wave 6

**Reference**: `docs/protocol/WAVE_ROLLBACK_PROTOCOL.md`

---

## Cost Estimate

**Per Epic**: ~50-80 bobcoins
**Total Epics**: 79
**Total Bobcoins**: ~3,950-6,320
**Available Budget**: 800 bobcoins (5 APIs)
**Refills Needed**: ~5-8 refills (160 bobcoins each)

**Total Cost**: ~$200-$320 (at $0.05/bobcoin)

---

## References

- **Wave 6 Spec**: `building-blocks/autonomous-refactoring/V12_52_TEMPLATE_USAGE.md`
- **VM Sync Protocol**: `.bob/skills/gcp-vm-wave-execution/skill.md` lines 80-172
- **Lamport Protocol**: `docs/protocol/V12_52_IMPLEMENTATION_SUMMARY.md`
- **Custom Modes**: `.bob/custom_modes.yaml`
- **Hooks**: `.bob/hooks.json`
- **API Rotation**: `docs/API/api_rotation.json`

---

**Status**: READY FOR LOCAL UPDATES → VM SYNC → SMOKE TEST → FULL LAUNCH