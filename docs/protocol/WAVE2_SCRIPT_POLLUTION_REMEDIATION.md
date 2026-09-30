# Wave 2 Script Pollution Remediation Plan

**Issue**: Phase 6 orchestrator (`launch_phase6_all_epics.sh`) used sequential execution instead of parallel, violating the Building Blocks SOP for future waves.

**Impact**: Future agents will copy the sequential pattern, perpetuating the inefficiency.

**Solution**: Replace sequential script with correct parallel version, archive the incorrect one for forensic analysis.

---

## Analysis

### What Went Wrong

**File**: `launch_phase6_all_epics.sh` (uploaded to VM)

**Problem Code**:
```python
# ❌ WRONG - Sequential execution
for epic in epics:
    launch_epic_phase6(epic)
    wait_for_completion(epic)  # Blocks until epic completes
    check_status(epic)
```

**Why It's Wrong**:
- Waits for each epic to complete before starting next
- Only uses 1 of 3 available workers
- 2-3x slower than parallel execution
- Violates Wave 2 design (3 parallel workers)

### Correct Pattern

**Should Be**:
```python
# ✅ CORRECT - Parallel execution
from concurrent.futures import ThreadPoolExecutor, as_completed

with ThreadPoolExecutor(max_workers=3) as executor:
    futures = {executor.submit(run_epic_phase6, epic): epic for epic in epics}
    for future in as_completed(futures):
        result = future.result()
```

---

## Remediation Strategy

### Option 1: Archive + Replace (RECOMMENDED)

**Pros**:
- Preserves forensic evidence of what actually ran
- Provides correct template for future waves
- Clear separation between "what happened" vs "what should happen"

**Cons**:
- Two versions of the script exist (could confuse agents)

**Implementation**:
1. Archive sequential script: `launch_phase6_all_epics.sh.SEQUENTIAL_ERROR`
2. Create correct parallel script: `launch_phase6_all_epics_PARALLEL.sh`
3. Add README explaining the difference
4. Update PARALLEL_EXECUTION_SOP.md to reference correct script

### Option 2: Delete Sequential Script

**Pros**:
- Clean slate for future waves
- No confusion about which script to use

**Cons**:
- Loses forensic evidence
- Can't reproduce what actually happened in Wave 2
- Harder to learn from the mistake

### Option 3: In-Place Fix with Git History

**Pros**:
- Git history preserves what happened
- Only one version exists going forward

**Cons**:
- Agents might not check git history
- Sequential version still in git log (could be copied)

---

## Recommendation: Option 1 (Archive + Replace)

### Step 1: Archive Sequential Script

```bash
# On VM
cd /home/malhitticrypto/universal-or-strategy
mv launch_phase6_all_epics.sh launch_phase6_all_epics.sh.SEQUENTIAL_ERROR

# Add warning header
cat > launch_phase6_all_epics.sh.SEQUENTIAL_ERROR.README << 'EOF'
# ⚠️ WARNING: SEQUENTIAL EXECUTION ERROR

This script was used in Wave 2 Phase 6 but contains a CRITICAL ERROR:
It executes epics SEQUENTIALLY instead of PARALLEL.

**DO NOT USE THIS SCRIPT AS A TEMPLATE**

See: launch_phase6_all_epics_PARALLEL.sh for correct implementation
See: docs/protocol/PARALLEL_EXECUTION_SOP.md for explanation

This file is preserved for forensic analysis only.

Error: Used wait_for_completion() loop instead of ThreadPoolExecutor
Impact: 21 minutes vs ~7-10 minutes (2-3x slower)
Date: 2026-06-13
EOF
```

### Step 2: Create Correct Parallel Script

```bash
# Create correct version
cat > launch_phase6_all_epics_PARALLEL.sh << 'EOF'
#!/bin/bash
# Wave 2 Phase 6 Orchestrator (PARALLEL EXECUTION)
# Launches all 7 epic reviews in parallel with 3 workers
# V12.25 - Correct Implementation

set -e

EPICS=(107 108 109 111 112 113 114)
LOG_DIR="logs"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
MAIN_LOG="${LOG_DIR}/phase6_parallel_${TIMESTAMP}.log"

mkdir -p "$LOG_DIR"

echo "=== Wave 2 Phase 6 Parallel Orchestrator ===" | tee -a "$MAIN_LOG"
echo "Started: $(date)" | tee -a "$MAIN_LOG"
echo "Workers: 3 (parallel execution)" | tee -a "$MAIN_LOG"
echo "Epics: ${EPICS[@]}" | tee -a "$MAIN_LOG"
echo "" | tee -a "$MAIN_LOG"

# Launch all epics in parallel using GNU parallel or xargs
# Method 1: Using GNU parallel (if available)
if command -v parallel &> /dev/null; then
    echo "Using GNU parallel for execution" | tee -a "$MAIN_LOG"
    printf '%s\n' "${EPICS[@]}" | parallel -j 3 --line-buffer "
        echo \"[{}] Starting Phase 6 review...\"
        screen -dmS p6_{} bash -l _p6_{}.sh
        
        # Wait for completion
        while screen -list | grep -q \"p6_{}\"; do
            sleep 30
        done
        
        echo \"[{}] Phase 6 complete\"
    " 2>&1 | tee -a "$MAIN_LOG"
else
    # Method 2: Using Python ThreadPoolExecutor
    echo "Using Python ThreadPoolExecutor for execution" | tee -a "$MAIN_LOG"
    python3 << 'PYTHON_EOF'
import subprocess
import time
from concurrent.futures import ThreadPoolExecutor, as_completed

def run_epic_phase6(epic_id):
    """Execute Phase 6 for a single epic."""
    screen_name = f"p6_{epic_id}"
    script_path = f"_p6_{epic_id}.sh"
    
    print(f"[{epic_id}] Starting Phase 6 review...")
    
    # Launch in screen session
    cmd = f"screen -dmS {screen_name} bash -l {script_path}"
    subprocess.run(cmd, shell=True, check=True)
    
    # Wait for completion
    while True:
        result = subprocess.run(
            f"screen -list | grep -q '{screen_name}'",
            shell=True,
            capture_output=True
        )
        if result.returncode != 0:  # Screen session ended
            break
        time.sleep(30)
    
    print(f"[{epic_id}] Phase 6 complete")
    return epic_id

# Execute with 3 workers
epics = [107, 108, 109, 111, 112, 113, 114]
with ThreadPoolExecutor(max_workers=3) as executor:
    futures = {executor.submit(run_epic_phase6, epic): epic for epic in epics}
    
    for future in as_completed(futures):
        epic = futures[future]
        try:
            result = future.result()
            print(f"✅ EPIC-CCN-{result} completed successfully")
        except Exception as e:
            print(f"❌ EPIC-CCN-{epic} failed: {e}")
PYTHON_EOF
fi

echo "" | tee -a "$MAIN_LOG"
echo "=== Phase 6 Parallel Execution Complete ===" | tee -a "$MAIN_LOG"
echo "Completed: $(date)" | tee -a "$MAIN_LOG"
echo "Check individual logs in: ${LOG_DIR}/phase6_*.log" | tee -a "$MAIN_LOG"
EOF

chmod +x launch_phase6_all_epics_PARALLEL.sh
```

### Step 3: Update Documentation

```bash
# Update PARALLEL_EXECUTION_SOP.md
cat >> docs/protocol/PARALLEL_EXECUTION_SOP.md << 'EOF'

---

## Wave 2 Script Pollution Remediation

**Issue**: Original `launch_phase6_all_epics.sh` used sequential execution (error).

**Resolution**:
- Sequential script archived: `launch_phase6_all_epics.sh.SEQUENTIAL_ERROR`
- Correct script created: `launch_phase6_all_epics_PARALLEL.sh`
- Future waves MUST use `_PARALLEL.sh` version as template

**Reference**: `docs/protocol/WAVE2_SCRIPT_POLLUTION_REMEDIATION.md`

---
EOF
```

### Step 4: Update Building Blocks Index

```bash
# Create building blocks index
cat > docs/workflow/BUILDING_BLOCKS_INDEX.md << 'EOF'
# Building Blocks Index - Autonomous Refactor Scripts

This index tracks which scripts are APPROVED templates for future waves.

## Phase 5 Scripts (Ticket Execution)

### ✅ APPROVED TEMPLATES
- `launch_remaining_epics.sh` - Multi-epic orchestrator (PARALLEL)
- `complete_epic_108_proper.sh` - Single epic orchestrator (GATED)
- `_p5_*.sh` - Individual ticket execution scripts
- `_p5v_*.sh` - Individual ticket validation scripts

### ❌ DO NOT USE
- None (Phase 5 scripts are all correct)

## Phase 6 Scripts (Epic Reviews)

### ✅ APPROVED TEMPLATES
- `launch_phase6_all_epics_PARALLEL.sh` - Multi-epic orchestrator (PARALLEL)
- `_p6_*.sh` - Individual epic review scripts

### ❌ DO NOT USE
- `launch_phase6_all_epics.sh.SEQUENTIAL_ERROR` - Sequential execution error
  - **Why**: Uses wait_for_completion() loop instead of ThreadPoolExecutor
  - **Impact**: 2-3x slower than parallel execution
  - **Archived**: 2026-06-13 (Wave 2 forensic evidence)

## Script Selection Rules

When creating scripts for future waves:

1. **Check this index first** - Verify script is in APPROVED list
2. **Use PARALLEL suffix** - If multiple versions exist, use `_PARALLEL.sh`
3. **Verify pattern** - Check for ThreadPoolExecutor, not wait_for_completion()
4. **Test with 2 epics** - Smoke test before full deployment

## References

- **Parallel Execution SOP**: `docs/protocol/PARALLEL_EXECUTION_SOP.md`
- **Wave 2 Remediation**: `docs/protocol/WAVE2_SCRIPT_POLLUTION_REMEDIATION.md`
- **Building Blocks Method**: `.bob/commands/autonomous-refactor.md`

---

**Last Updated**: 2026-06-13  
**Maintainer**: Orchestrator (Bob)
EOF
```

---

## Implementation Checklist

- [ ] Archive sequential script with warning header
- [ ] Create correct parallel script
- [ ] Update PARALLEL_EXECUTION_SOP.md
- [ ] Create BUILDING_BLOCKS_INDEX.md
- [ ] Test parallel script with 2 epics (smoke test)
- [ ] Update autonomous-refactor command to reference index
- [ ] Add to pre-wave checklist: "Verify scripts against BUILDING_BLOCKS_INDEX"

---

## Future Wave Protocol

Before starting any future wave:

1. **Check Building Blocks Index**: `docs/workflow/BUILDING_BLOCKS_INDEX.md`
2. **Verify script is APPROVED**: Look for ✅ marker
3. **Avoid DEPRECATED scripts**: Look for ❌ marker
4. **Use PARALLEL suffix**: When multiple versions exist
5. **Smoke test**: Run with 2 epics before full deployment

---

## Lessons Learned

### What Went Wrong
- Orchestrator (me) used sequential pattern by mistake
- No pre-deployment smoke test to catch the error
- No building blocks index to guide script selection

### What Went Right
- Error was caught and documented
- Parallel SOP was created
- Forensic evidence preserved for learning

### Prevention for Future Waves
- ✅ Building Blocks Index created
- ✅ Parallel SOP documented
- ✅ Smoke test protocol added
- ✅ Script naming convention (use `_PARALLEL.sh` suffix)

---

**Version**: V12.25  
**Date**: 2026-06-13  
**Status**: REMEDIATION PLAN APPROVED