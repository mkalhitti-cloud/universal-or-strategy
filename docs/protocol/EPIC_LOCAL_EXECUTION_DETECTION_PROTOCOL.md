# Epic Local Execution Detection Protocol (V12.31)

**Version**: 1.0  
**Effective**: 2026-06-16  
**Status**: MANDATORY for all wave-based autonomous execution  
**Trigger**: EPIC-027 incident (NT8 dependency discovered mid-execution)

## Problem Statement

**Incident**: EPIC-027 Phase 5 executed on Linux VM but required NinjaTrader 8 (Windows-only) for:
- Build verification (NT8 assemblies)
- Test execution (NT8 Strategy Analyzer)
- Complexity audit (NT8 runtime)

**Impact**:
- Phase 5 incomplete (1/3 tickets executed)
- 7 minutes wasted on VM execution
- Manual Windows execution required
- Wave 4 completion delayed

**Root Cause**: No pre-flight check to detect NT8 dependencies before VM launch.

## Detection Criteria

### Indicators of Local Execution Requirement

An epic requires **local (Windows) execution** if ANY of the following are true:

#### 1. NinjaTrader Strategy Code (PRIMARY)
- **File Pattern**: `src/V12_002*.cs` (any file in src/ starting with V12_002)
- **Reason**: Depends on NT8 proprietary assemblies (NinjaTrader.Cbi, NinjaTrader.Gui, etc.)
- **Detection**: Check if epic's `02-architecture-plan.md` references files matching `src/V12_002*.cs`

#### 2. Test Project References NT8 Code
- **File Pattern**: Test files that import/reference V12_002 classes
- **Reason**: Tests require NT8 Strategy Analyzer, not `dotnet test`
- **Detection**: Check if epic's `04-tickets.md` mentions test files in `tests/V12_Performance.Tests/`

#### 3. Complexity Audit Requires NT8 Runtime
- **Indicator**: Epic targets methods in V12_002 strategy
- **Reason**: Complexity tools need NT8 assemblies to parse code
- **Detection**: Check if `00-hotspots.md` lists methods from `src/V12_002*.cs`

#### 4. Build Requires Windows-Specific SDKs
- **Indicator**: Epic modifies `.csproj` files with Windows-only targets
- **Reason**: Linux VM lacks Windows SDKs (.NET Framework, WPF, etc.)
- **Detection**: Check if `02-architecture-plan.md` mentions `.csproj` modifications

#### 5. Manual User Intervention Required
- **Indicator**: Epic requires F5 in NinjaTrader or manual testing
- **Reason**: Cannot be automated on headless Linux VM
- **Detection**: Check if `04-tickets.md` contains "F5 in NinjaTrader" or "manual test"

## Pre-Flight Check Implementation

### Phase -1: Local Execution Detection (NEW)

**When**: BEFORE Phase 0 (Hotspot Analysis)  
**Duration**: 30 seconds  
**Tool**: `scripts/detect_local_execution.py`

**Algorithm**:
```python
def requires_local_execution(epic_id: str) -> tuple[bool, list[str]]:
    """
    Detect if epic requires local Windows execution.
    
    Returns:
        (requires_local, reasons) where reasons is list of detection criteria matched
    """
    reasons = []
    
    # Check 1: V12_002 strategy code
    if epic_targets_v12_002_files(epic_id):
        reasons.append("NT8_STRATEGY_CODE")
    
    # Check 2: Test project references
    if epic_has_nt8_tests(epic_id):
        reasons.append("NT8_TEST_DEPENDENCY")
    
    # Check 3: Complexity audit on NT8 code
    if hotspot_analysis_targets_nt8(epic_id):
        reasons.append("NT8_COMPLEXITY_AUDIT")
    
    # Check 4: Windows-specific build
    if epic_modifies_windows_csproj(epic_id):
        reasons.append("WINDOWS_BUILD_DEPENDENCY")
    
    # Check 5: Manual intervention
    if epic_requires_manual_steps(epic_id):
        reasons.append("MANUAL_INTERVENTION_REQUIRED")
    
    return (len(reasons) > 0, reasons)

def epic_targets_v12_002_files(epic_id: str) -> bool:
    """Check if epic modifies V12_002 strategy files."""
    arch_plan = f"docs/brain/{epic_id}/02-architecture-plan.md"
    if not os.path.exists(arch_plan):
        return False
    
    content = Path(arch_plan).read_text()
    return bool(re.search(r'src/V12_002.*\.cs', content))

def epic_has_nt8_tests(epic_id: str) -> bool:
    """Check if epic creates/modifies tests that reference V12_002."""
    tickets = f"docs/brain/{epic_id}/04-tickets.md"
    if not os.path.exists(tickets):
        return False
    
    content = Path(tickets).read_text()
    return bool(re.search(r'tests/V12_Performance\.Tests/', content))

def hotspot_analysis_targets_nt8(epic_id: str) -> bool:
    """Check if hotspot analysis targets V12_002 methods."""
    hotspots = f"docs/brain/{epic_id}/00-hotspots.md"
    if not os.path.exists(hotspots):
        return False
    
    content = Path(hotspots).read_text()
    return bool(re.search(r'File:.*V12_002.*\.cs', content))

def epic_modifies_windows_csproj(epic_id: str) -> bool:
    """Check if epic modifies .csproj with Windows-specific targets."""
    arch_plan = f"docs/brain/{epic_id}/02-architecture-plan.md"
    if not os.path.exists(arch_plan):
        return False
    
    content = Path(arch_plan).read_text()
    return bool(re.search(r'\.csproj.*net48|\.csproj.*WindowsBase', content))

def epic_requires_manual_steps(epic_id: str) -> bool:
    """Check if epic requires manual user intervention."""
    tickets = f"docs/brain/{epic_id}/04-tickets.md"
    if not os.path.exists(tickets):
        return False
    
    content = Path(tickets).read_text()
    return bool(re.search(r'F5 in NinjaTrader|manual test|user must', content, re.IGNORECASE))
```

### Integration Points

#### 1. Wave Launch Script (`scripts/wave4/launch_wave.sh`)

**BEFORE launching any phase scripts**:
```bash
#!/bin/bash
# Wave 4 Launch Script with Local Execution Detection

echo "[$(date)] Phase -1: Local Execution Detection"

# Run detection for all epics
python scripts/detect_local_execution.py --wave 4 --output wave4_local_epics.json

# Parse results
LOCAL_EPICS=$(jq -r '.local_epics[]' wave4_local_epics.json)
VM_EPICS=$(jq -r '.vm_epics[]' wave4_local_epics.json)

echo "Local execution required: $(echo "$LOCAL_EPICS" | wc -l) epics"
echo "VM execution allowed: $(echo "$VM_EPICS" | wc -l) epics"

# Prompt user
if [ -n "$LOCAL_EPICS" ]; then
    echo ""
    echo "WARNING: The following epics require local Windows execution:"
    echo "$LOCAL_EPICS"
    echo ""
    read -p "Continue with VM execution for remaining epics? (y/n) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo "Wave launch cancelled"
        exit 1
    fi
fi

# Launch only VM-compatible epics
for epic in $VM_EPICS; do
    echo "[$(date)] Launching $epic on VM"
    # ... existing launch logic
done

echo ""
echo "VM execution complete. Local epics require manual execution:"
echo "$LOCAL_EPICS"
```

#### 2. Epic Run Command (`/epic-run`)

**BEFORE Phase 0**:
```python
# In epic_run.py or equivalent

def epic_run(epic_id: str):
    """Execute epic with local execution detection."""
    
    # Phase -1: Detection
    requires_local, reasons = detect_local_execution(epic_id)
    
    if requires_local:
        print(f"⚠️  EPIC-{epic_id} requires LOCAL Windows execution")
        print(f"Reasons: {', '.join(reasons)}")
        print("")
        print("Options:")
        print("1. Execute locally in Windows environment")
        print("2. Skip and mark for manual execution")
        print("3. Cancel")
        
        choice = input("Choose (1/2/3): ")
        
        if choice == "1":
            print("Switching to local execution mode...")
            return execute_epic_locally(epic_id)
        elif choice == "2":
            mark_epic_manual(epic_id, reasons)
            return
        else:
            print("Epic execution cancelled")
            return
    
    # Proceed with normal VM execution
    execute_epic_on_vm(epic_id)
```

#### 3. Roadmap Generation (`scripts/generate_roadmap.py`)

**Add `execution_environment` field**:
```json
{
  "epic_id": "EPIC-CCN-027",
  "method": "CreateBracketOrders",
  "file": "src/V12_002.SIMA.Dispatch.cs",
  "cyc": 37,
  "execution_environment": "local",  // NEW FIELD
  "local_execution_reasons": [       // NEW FIELD
    "NT8_STRATEGY_CODE",
    "NT8_TEST_DEPENDENCY"
  ],
  "status": "pending"
}
```

## Updated Workflows

### Wave Launch Workflow (REVISED)

**OLD** (5 steps):
1. Generate scripts
2. Upload to VM
3. Launch all epics
4. Monitor execution
5. Sync results

**NEW** (7 steps):
1. **Phase -1: Local Execution Detection** ⭐ NEW
2. **Separate epics into VM vs Local** ⭐ NEW
3. Generate scripts (VM epics only)
4. Upload to VM
5. Launch VM epics
6. Monitor execution
7. Sync results + **Report local epics for manual execution** ⭐ NEW

### Epic Run Workflow (REVISED)

**OLD** (6 phases):
- Phase 0: Hotspot
- Phase 1: Scope
- Phase 2: Architecture
- Phase 3: Audit
- Phase 4: Tickets
- Phase 5: Execution

**NEW** (7 phases):
- **Phase -1: Local Execution Detection** ⭐ NEW
- Phase 0: Hotspot
- Phase 1: Scope
- Phase 2: Architecture
- Phase 3: Audit
- Phase 4: Tickets
- Phase 5: Execution (environment-aware)

## SOP Updates Required

### 1. Wave Phase Script Generation SOP V4

**File**: `docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V4.md`

**Changes**:
- Add Phase -1 detection step BEFORE Phase 0
- Update script generation to skip local-execution epics
- Add local epic reporting at end of wave

### 2. GCP VM Wave Execution Skill V3

**File**: `.bob/skills/gcp-vm-wave-execution/skill.md`

**Changes**:
- Add `detect_local_execution.py` to prerequisites
- Update launch procedure to include Phase -1
- Add local epic handling instructions

### 3. Epic Run Slash Command

**File**: `.bob/custom_modes.yaml` (v12-epic-planner mode)

**Changes**:
- Add `/epic-detect-local` command
- Update `/epic-run` to call detection first
- Add `/epic-run-local` for Windows execution

## Implementation Checklist

### Scripts to Create
- [ ] `scripts/detect_local_execution.py` (detection logic)
- [ ] `scripts/wave4/launch_wave_v2.sh` (with Phase -1)
- [ ] `scripts/epic_run_local.py` (Windows execution wrapper)

### Documentation to Update
- [ ] `docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V4.md`
- [ ] `.bob/skills/gcp-vm-wave-execution/skill.md` (V3)
- [ ] `docs/workflow/EPIC_WORKFLOW_WALKTHROUGH.md`
- [ ] `AGENTS.md` (add V12.31 protocol reference)

### Slash Commands to Add
- [ ] `/epic-detect-local <epic_id>` - Run detection only
- [ ] `/epic-run-local <epic_id>` - Execute in Windows environment
- [ ] `/wave-detect-local <wave_num>` - Detect for entire wave

### Roadmap Schema Updates
- [ ] Add `execution_environment` field (vm|local)
- [ ] Add `local_execution_reasons` array
- [ ] Update `generate_roadmap.py` to populate fields

## Testing Strategy

### Unit Tests
```python
def test_detect_v12_002_strategy():
    """Test detection of V12_002 strategy code."""
    epic_id = "EPIC-CCN-027"
    requires_local, reasons = detect_local_execution(epic_id)
    assert requires_local == True
    assert "NT8_STRATEGY_CODE" in reasons

def test_detect_pure_helper_extraction():
    """Test that pure helper extractions don't trigger local execution."""
    epic_id = "EPIC-CCN-012"  # Pure helper extraction, no NT8 deps
    requires_local, reasons = detect_local_execution(epic_id)
    assert requires_local == False
```

### Integration Tests
1. Run detection on Wave 4 epics (80 epics)
2. Verify EPIC-027 flagged as local
3. Verify other epics correctly classified
4. Test wave launch with mixed epics

## Rollout Plan

### Phase 1: Detection Script (1 hour)
- Create `detect_local_execution.py`
- Test on Wave 4 epics
- Validate EPIC-027 detection

### Phase 2: Wave Launch Integration (1 hour)
- Update `launch_wave.sh` with Phase -1
- Test on Wave 4 subset (10 epics)
- Verify local epics skipped

### Phase 3: Epic Run Integration (1 hour)
- Add `/epic-detect-local` command
- Update `/epic-run` to call detection
- Test on EPIC-027

### Phase 4: Documentation (1 hour)
- Update SOP V4
- Update skill V3
- Update AGENTS.md

### Phase 5: Roadmap Schema (30 min)
- Add new fields
- Regenerate Wave 4 roadmap
- Verify local epics marked

**Total Rollout Time**: 4.5 hours

## Success Metrics

### Detection Accuracy
- **Target**: 100% accuracy on known local epics
- **Measure**: Manual review of Wave 4 classifications
- **Baseline**: EPIC-027 must be flagged

### Wave Launch Efficiency
- **Target**: Zero VM launches for local epics
- **Measure**: VM execution logs
- **Baseline**: Wave 4 should skip EPIC-027

### User Experience
- **Target**: Clear guidance on local execution
- **Measure**: User feedback on error messages
- **Baseline**: "Execute locally in Windows" message

## Lessons Learned

### What Went Wrong (EPIC-027)
1. ❌ No pre-flight check for NT8 dependencies
2. ❌ Assumed all epics VM-compatible
3. ❌ Discovered issue mid-execution (Phase 5)
4. ❌ Wasted 7 minutes on VM + manual recovery time

### What Would Have Prevented It
1. ✅ Phase -1 detection before Phase 0
2. ✅ Roadmap marking local epics upfront
3. ✅ Wave launch skipping local epics
4. ✅ Clear user guidance on Windows execution

### Future Prevention
- **MANDATORY**: Run Phase -1 detection before EVERY wave launch
- **MANDATORY**: Update roadmap with execution environment
- **MANDATORY**: Prompt user before launching mixed waves
- **RECOMMENDED**: Create golden image with NT8 for future (if feasible)

## References

- **Incident Report**: `WAVE4_EPIC_027_CRITICAL_FINDINGS.md`
- **Build Blocker Analysis**: `WAVE4_EPIC_027_BUILD_BLOCKER_ANALYSIS.md`
- **Resolution Strategy**: `WAVE4_EPIC_027_BUILD_BLOCKER_RESOLUTION.md`
- **V12.23 Protocol**: No Scope Creep (separate commits for prerequisites)
- **V12.28 Protocol**: 100% Completion Mandate

---

**Status**: Protocol defined | Implementation pending  
**Next Step**: Create `detect_local_execution.py` script  
**Timeline**: 4.5 hours to full rollout  
**Priority**: P0 (blocks future wave launches)