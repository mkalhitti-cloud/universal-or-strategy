# Wave Phase Script Generation SOP V3

**Version**: 3.9
**Date**: 2026-06-18
**Status**: SUPERSEDED by Bob IDE V2 Subagent Model (2026-06-24)
**Supersedes**: V3.8
**Critical Update**: Bob CLI invocation pattern (V3.9) - fixes Phase 1.5 freeze issue

> ## 🚫 SUPERSEDED NOTICE (Bob IDE V2 — 2026-06-24)
>
> **This SOP is OBSOLETE for wave phase execution.**
>
> Bob IDE V2 introduces native subagent spawning. Shell scripts (`_pX_NNN.sh`),
> Bob CLI invocation patterns, GCP VM screen sessions, and the Building-Blocks
> Method for script generation are **no longer needed**.
>
> **For current execution model**, see:
> `docs/workflow/AUTONOMOUS_REFACTOR_INTEGRATION_MATRIX_V2.md` (V2.3)
> → Section: "Bob IDE V2 Subagent Execution Model"
>
> **This document is RETAINED** as historical reference for the V1 shell execution model.
> Do NOT follow these instructions for new wave execution.

---

## Critical Update (V3)

**New Rule**: ALWAYS copy the SAME phase from the PREVIOUS wave, NOT adjacent phases from the current wave.

**Violation Discovered**: Wave 3 Phase 3 copied Wave 3 Phase 2 (wrong) instead of Wave 2 Phase 3 (correct).

**Impact**: Wrong execution mode → Wrong output format → 2 failed attempts → 34 minutes debugging → ~5.2 bobcoins wasted.

---

## The Golden Rule

### ALWAYS Copy Same Phase from Previous Wave

```
✅ CORRECT:
Wave 3 Phase 3 → Copy Wave 2 Phase 3
Wave 3 Phase 4 → Copy Wave 2 Phase 4
Wave 3 Phase 5 → Copy Wave 2 Phase 5

❌ WRONG:
Wave 3 Phase 3 → Copy Wave 3 Phase 2
Wave 3 Phase 4 → Copy Wave 3 Phase 3
Wave 3 Phase 5 → Copy Wave 3 Phase 4
```

### Why This Matters

**Each phase has unique requirements**:
- Different execution modes (ask/plan/advanced/v12-engineer)
- Different command patterns
- Different output formats
- Different validation requirements

**Adjacent phases are NOT interchangeable**.

---

## CRITICAL: Lamport Clock Event Sources (V3.8)

### Event Log vs Manifest Events

**CRITICAL**: The Lamport clock verification system checks TWO sources for phase completion events:

1. **Global Event Log** (`.lamport/event_log.jsonl`)
   - Immutable append-only log
   - Contains ALL events across ALL epics
   - Primary source for verification

2. **Manifest Events** (`docs/brain/EPIC-X/manifest.json` → `lamport_events` array)
   - Per-epic event history
   - Fallback source for migrated epics
   - May exist when global log is missing events

### Why This Matters

**Wave 6 Phase 1 Incident**:
- 4 epics blocked with "Phase 0 not complete" errors
- Manifests showed Phase 0 complete (status, events, output files all present)
- Global event log missing Phase 0 completion events for these 4 epics
- **Root Cause**: Manifest migration script updated manifests but didn't sync to global log

### Permanent Fix (V3.8)

**Updated `scripts/lamport_clock.py`**:
```python
def check_dependencies(self, epic_id: str, phase: str) -> Tuple[bool, str]:
    # Check global event log first
    events = self.get_event_log(epic_id)
    
    # Also load manifest events as fallback (for migrated epics)
    manifest_events = self._load_manifest_events(epic_id)
    
    for req_phase in required_phases:
        # Check global event log first
        completions = [e for e in events if ...]
        
        # Fallback: check manifest events
        if not completions and manifest_events:
            completions = [e for e in manifest_events if ...]
```

### Verification Protocol

**Before ANY wave execution**:
1. Verify global event log exists: `.lamport/event_log.jsonl`
2. Verify manifest events exist: `manifest.json` → `lamport_events` array
3. If discrepancy detected, `check_dependencies()` will use manifest as fallback

**DO NOT** manually inject events into global log - the fallback mechanism handles this automatically.

---

## CRITICAL: VM vs Local Building Blocks (V3.3)

### Two Different Script Versions

**IMPORTANT**: There are TWO versions of building-block scripts:

1. **Local Building Blocks** (`scripts/wave{N}/_p{X}_*.sh` in local repo)
   - May have simplified prerequisite checks
   - Example: `if [ ! -f "docs/brain/EPIC-CCN-001/05-completion.md" ]`
   - Used for reference and initial generation

2. **VM Building Blocks** (scripts already on VM from previous waves)
   - Have **robust prerequisite checks** with OR logic
   - Example: `if ! find docs/brain/EPIC-CCN-001 -maxdepth 1 \( -name "05-*.md" -o -name "ticket-*-completion.md" \) -print -quit | grep -q .`
   - **ALWAYS use VM version as source of truth**

### Why This Matters

**Wave 4 Phase 6 Discovery**:
- Local `scripts/wave4/_p6_001.sh` had simple check: `if [ ! -f "docs/brain/EPIC-CCN-001/05-completion.md" ]`
- VM `scripts/wave4/_p6_001.sh` had robust check with OR logic for multiple file patterns
- **Root Cause**: VM scripts evolved during execution, local copies were stale

### Correct Workflow

**Step 1: Check VM for existing scripts**:
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="cat ~/universal-or-strategy/scripts/wave{N-1}/_p{X}_001.sh"
```

**Step 2: Copy VM version, NOT local version**:
```bash
# Download VM script as template
gcloud compute scp v12-test-golden-v2:~/universal-or-strategy/scripts/wave{N-1}/_p{X}_001.sh \
  ./scripts/wave{N}/template_p{X}.sh --zone=us-central1-a

# Use this as your building block
```

**Step 3: Generate new scripts from VM template**:
- Use the downloaded VM script as your source
- Modify only epic numbers and API keys
- Preserve ALL logic, especially prerequisite checks

### Common Differences Between VM and Local

| Aspect | Local Version | VM Version (Correct) |
|--------|---------------|---------------------|
| **Prerequisite Check** | Simple file existence | Robust OR logic with find |
| **Error Messages** | Basic | Detailed with expected patterns |
| **Line Endings** | May have CRLF (Windows) | Always LF (Unix) |
| **Permissions** | May not be executable | Always executable |

### Line Ending Fix (MANDATORY)

**After uploading to VM, ALWAYS fix line endings**:
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="cd ~/universal-or-strategy/scripts/wave{N} && sed -i 's/\r$//' _p{X}_*.sh"
```

**Why**: Windows generates CRLF line endings, VM needs LF. Symptom: `/bin/bash^M: bad interpreter`

---

## Standard Operating Procedure
### Step -3: Skill Reading Verification (V12.39 - NEW - BLOCKING GATE)

**MANDATORY**: Before ANY wave execution, verify you have read the skill documentation.

**Purpose**: Prevent incorrect assumptions about VM capabilities that lead to failed wave executions.

#### Skill Reading Checklist

Execute these checks in order. If ANY box unchecked, STOP immediately.

**1. Read Primary Skill**:
```markdown
File: .bob/skills/gcp-vm-wave-execution/skill.md (V2.10+)
- [ ] Read "READ THIS FIRST" section (top of file)
- [ ] Read "What you need" section (lines 22-44)
- [ ] Read "VM Setup" section (after "What you need")
- [ ] Read "Pre-Wave Checklist" section (lines 47-120)
```

**2. Read VM Setup Protocol**:
```markdown
File: docs/protocol/VM_SETUP_PROTOCOL.md (V12.39)
- [ ] Read "READ THIS FIRST" section (top of file)
- [ ] Read "Critical VM Facts" section
- [ ] Read "Pre-Flight Validation" section
```

**3. Verify Understanding**:
```markdown
Critical Facts (check ALL):
- [ ] VM does NOT have .NET SDK installed
- [ ] VM does NOT compile code (dotnet build will fail)
- [ ] VM ONLY executes Bob CLI for code generation
- [ ] Bob CLI location: ~/bob (aliased in ~/.bashrc)
- [ ] Compilation happens locally (Windows machine with .NET 8.0 SDK)
```

#### BLOCKING GATE

**If ANY box unchecked**:
1. STOP immediately
2. Read the skill documentation in full
3. Check ALL boxes in verification checklist
4. ONLY THEN proceed to Step -2

**Why This Matters**:
- Agents who skip reading the skill make incorrect assumptions about VM capabilities
- Common mistake: trying to run `dotnet build` on VM (VM has no .NET SDK)
- Common mistake: looking for Bob CLI in wrong location (it's at ~/bob, not /usr/local/bin)
- Common mistake: expecting compilation on VM (compilation happens locally only)

**DO NOT PROCEED** until ALL boxes checked.

**Reference**: 
- `.bob/skills/gcp-vm-wave-execution/skill.md` (V2.10+)
- `docs/protocol/VM_SETUP_PROTOCOL.md` (V12.39)


### Step -2: Pre-Wave Validation (V12.39 - UPDATED)

**Prerequisites**: Step -3 (Skill Reading Verification) MUST be complete.

**CRITICAL**: Before generating ANY scripts, validate wave readiness.

**Purpose**: Prevent cascade failures by catching protocol gaps before execution.

#### Validation Checklist

Execute these checks in order. If ANY check fails, STOP immediately.

**0. Skill Reading Complete** (Step -3 - BLOCKING GATE):
```markdown
- [ ] Step -3 verification checklist complete
- [ ] If not complete, STOP and complete Step -3 first
```

**1. VM Setup Verified** (V12.39):
```markdown
- [ ] Read docs/protocol/VM_SETUP_PROTOCOL.md
- [ ] Verify VM accessible, Bob CLI available, repository exists
- [ ] REMEMBER: VM does NOT have .NET SDK (compilation is local only)
```

**2. Encoding Pre-Check**:
```powershell
.\scripts\check_encoding.ps1
# Expected: Exit code 0, "All files use UTF-8 encoding"
```

**3. 7-Step Git Sync Verified**:
```bash
# Follow V12.37 protocol (see Step 0.5 below)
# Expected: VM and local on same commit AND working tree clean
```

**4. ~~VM Build Passes~~ (SKIP - V12.39)**:
```bash
# SKIP THIS CHECK - VM does NOT have .NET SDK installed
# Compilation happens locally only, NOT on VM
```
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="cd ~/universal-or-strategy && dotnet build src/V12_002.csproj"
# Expected: Build succeeded, 0 errors
```

**5. Local Build Passes**:
```powershell
dotnet build src/V12_002.csproj
# Expected: Build succeeded, 0 errors
```

**6. Pre-Flight Validation Tested**:
```bash
# Test skip/local/normal epic classification
python scripts/epic_preflight_validation.py --wave N
# Expected: Correct counts for skip/local/normal epics
```

**7. Pilot Test Plan Created**:
```markdown
# Create docs/brain/WAVE-N/pilot-test-plan.md
- Epic: EPIC-CCN-001 (or first epic in wave)
- Expected output: Phase X files
- Success criteria: Files created, build passes, 0 P0/P1 issues
```

**8. All Protocol Files Exist**:
```bash
# Verify required protocols exist
ls docs/protocol/WAVE_ROLLBACK_PROTOCOL.md
ls docs/protocol/VM_LOCAL_GIT_SYNC_PROTOCOL.md
ls docs/protocol/FILE_ENCODING_PROTOCOL.md
# Expected: All files exist
```

#### BLOCKING GATE

**If ANY check fails**:
1. STOP immediately
2. Document failure in wave monitoring file
3. Fix issue before proceeding
4. Re-run validation checklist
5. Only proceed after ALL checks pass

**DO NOT PROCEED** with script generation until validation passes.

### Step 0: MANDATORY Encoding Pre-Check (V12.33)

**CRITICAL**: Before generating ANY phase scripts, verify UTF-8 encoding compliance.

**Command**:
```powershell
.\scripts\check_encoding.ps1
```

**Expected Output**:
```
=== File Encoding Validation ===
Scanning: src

=== Results ===
✓ All files use UTF-8 encoding (no BOM)
```

**If Violations Found**:
```powershell
# Automatically convert to UTF-8 without BOM
.\scripts\check_encoding.ps1 -Fix

# Verify fix succeeded
.\scripts\check_encoding.ps1
```

**Why This Matters**:
- UTF-16 encoding causes Bob CLI apply_diff to fail with 0% similarity
- EPIC-CCN-027 TICKET-2: 7+ minutes wasted, epic blocked
- Bob's tools expect UTF-8, cannot handle UTF-16 LE/BE
- Silent failure: No error message, just 0% match

**DO NOT PROCEED** until encoding check passes (exit code 0).

**Reference**: `docs/protocol/FILE_ENCODING_PROTOCOL.md` (V12.33)

### Step 0.5: MANDATORY VM-Local Git Sync (V12.37)

**CRITICAL**: Before generating ANY wave scripts, verify VM and local are on the SAME git commit AND working tree is clean.

**Why This Matters**:
- Wave 5 Pilot Test #2 Incident: Commits matched (`810cfb2f`) but working tree had Wave 4 files
- Impact: Bob saw already-extracted code (CYC=10) instead of baseline code needing extraction
- Result: Wasted execution time, confusion about work status
- Fix: V12.37 added working tree verification (7-step sync)

**7-Step Sync Checklist**:

1. **Check Local Git State**:
   ```bash
   git log -1 --oneline
   git status
   # Expected: Clean working tree, no uncommitted src/ changes
   ```

2. **Push Local Commits to Origin**:
   ```bash
   git push origin gitbutler/workspace --force --no-verify
   # Use --no-verify if pre-push validation blocks non-critical issues
   ```

3. **Check VM Git State (Before Sync)**:
   ```bash
   gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
     --command="cd ~/universal-or-strategy && git log -1 --oneline && git status"
   ```

4. **Sync VM to Match Local (Hard Reset + Clean)**:
   ```bash
   gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
     --command="cd ~/universal-or-strategy && git fetch origin && git reset --hard origin/gitbutler/workspace && git clean -fd"
   ```

5. **Verify Commits Match (MANDATORY)**:
   ```bash
   LOCAL_COMMIT=$(git log -1 --format="%H")
   VM_COMMIT=$(gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
     --command="cd ~/universal-or-strategy && git log -1 --format='%H'")
   
   echo "Local: $LOCAL_COMMIT"
   echo "VM:    $VM_COMMIT"
   
   if [ "$LOCAL_COMMIT" = "$VM_COMMIT" ]; then
     echo "✅ COMMITS MATCH"
   else
     echo "❌ COMMITS MISMATCH"
     exit 1
   fi
   ```

6. **Verify Working Tree Clean (V12.37 - NEW)**:
   ```bash
   VM_STATUS=$(gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
     --command="cd ~/universal-or-strategy && git status --porcelain")
   
   if [ -z "$VM_STATUS" ]; then
     echo "✅ WORKING TREE CLEAN"
   else
     echo "❌ WORKING TREE DIRTY"
     echo "$VM_STATUS"
     exit 1
   fi
   ```

7. **Verify Baseline Files (V12.37 - NEW)**:
   ```bash
   # For pilot epic, verify no Phase 5 files exist
   PILOT_EPIC="EPIC-CCN-001"  # Adjust for your wave
   PHASE5_FILES=$(gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
     --command="ls ~/universal-or-strategy/docs/brain/$PILOT_EPIC/ticket-*-completion.md 2>/dev/null | wc -l")
   
   if [ "$PHASE5_FILES" = "0" ]; then
     echo "✅ BASELINE VERIFIED (no Phase 5 files)"
   else
     echo "❌ BASELINE CONTAMINATED ($PHASE5_FILES Phase 5 files found)"
     exit 1
   fi
   ```

**BLOCKER**: If ANY step fails, STOP immediately. Do NOT proceed with wave execution.

**Document in Wave Monitoring File**:
```markdown
## Git Sync Verification (V12.36)

**Pre-Wave Sync**:
- Local commit: `<hash>` (`<message>`)
- VM commit (before sync): `<hash>` (`<message>`)
- VM commit (after sync): `<hash>` (`<message>`)
- Sync status: ✅ VERIFIED / ❌ FAILED
```

**Complete Protocol**: `docs/protocol/VM_LOCAL_GIT_SYNC_PROTOCOL.md` (V12.36)

### Step 1: Copy Previous Wave's Same Phase

**Command**:
```bash
cp scripts/wave{N-1}/generate_phase{X}_scripts.py scripts/wave{N}/generate_wave{N}_phase{X}_scripts.py
```

**Example** (Wave 3 Phase 4):
```bash
cp scripts/wave2/generate_phase4_scripts.py scripts/wave3/generate_wave3_phase4_scripts.py
```

**DO NOT**:
- Copy adjacent phase from current wave
- Generate from scratch
- Assume patterns are similar

### Step 1.5: MANDATORY Integration Matrix V2 Validation (V3.11 - NEW - BLOCKING GATE)

**CRITICAL**: Before generating ANY phase script, you MUST verify the correct custom mode from Integration Matrix V2.

#### Integration Matrix Validation Checklist

**BEFORE script generation:**

1. **Open Integration Matrix V2**:
   ```bash
   cat docs/workflow/AUTONOMOUS_REFACTOR_INTEGRATION_MATRIX_V2.md
   ```

2. **Verify Custom Mode for Target Phase**:
   - Phase 0: `v12-phase0-hotspot`
   - Phase 1: `v12-phase1-scope`
   - Phase 1.5: `v12-phase1-5-boundary`
   - Phase 2: `v12-phase2-architecture`
   - Phase 3: `v12-phase3-audit`
   - Phase 4: `v12-phase4-tickets`
   - Phase 4.5: `v12-phase4-5-review`
   - Phase 5: `v12-engineer`
   - Phase 5.V: `v12-phase5-v-verify`
   - Phase 6: `v12-phase6-review`

3. **Check Required MCPs**:
   - Verify which MCPs are MANDATORY for this phase
   - Common MCPs: jcodemunch, sequential-thinking, graphify
   - Phase-specific: Check Integration Matrix for exact requirements

4. **Verify Building-Blocks Source**:
   - Building-blocks provide MECHANICS (--yolo, temp files, nohup)
   - Integration Matrix provides WORKFLOW (custom mode, MCPs)
   - NEVER copy custom mode from building-blocks
   - ALWAYS use custom mode from Integration Matrix V2

#### BLOCKING GATE

**Script generation is BLOCKED if:**
- ❌ Integration Matrix V2 not consulted
- ❌ Wrong custom mode selected (e.g., `plan` instead of `v12-phase1-scope`)
- ❌ Custom mode copied from building-blocks instead of Integration Matrix
- ❌ Required MCPs not verified against Integration Matrix

**Example of CORRECT workflow:**
```bash
# 1. Check Integration Matrix for Phase 1
grep "Phase 1:" docs/workflow/AUTONOMOUS_REFACTOR_INTEGRATION_MATRIX_V2.md
# Output: Custom Mode: v12-phase1-scope, MCPs: jcodemunch + sequential-thinking

# 2. Copy Phase 1 script mechanics from building-blocks
cp building-blocks/wave4/phase1/_p1_001.sh _p1_001.sh

# 3. Update ONLY the custom mode to match Integration Matrix
# Change: bob --yolo --chat-mode plan
# To: bob --yolo --chat-mode v12-phase1-scope
```

**Example of WRONG workflow (VIOLATION):**
```bash
# ❌ WRONG: Copying custom mode from building-blocks
cp building-blocks/wave4/phase1/_p1_001.sh _p1_001.sh
# Script has: bob --yolo --chat-mode plan
# This is WRONG - Integration Matrix says v12-phase1-scope
```

#### Post-Generation Validation

After generating scripts, run validation:
```bash
# Validate custom mode in generated script
grep "chat-mode" _p1_001.sh
# Should show: --chat-mode v12-phase1-scope (NOT plan)
```

### Step 2: Update Epic Numbers ONLY

**Change ONLY these lines**:
```python
# Epic to API key mapping (Wave 3: CCN-116 through CCN-125)
API_ALLOCATION = {
    "116": "b (2).json",  # Was "107"
    "117": "b.json",      # Was "108"
    "118": "bob (1).json", # Was "109"
    # ... etc
}
```

**DO NOT change**:
- Mode (ask/plan/advanced/v12-engineer)
- Command pattern
- Output format
- Validation requirements

### Step 3: Verify Against SOP

**Check these 4 things**:

1. **Mode matches SOP**:
   ```bash
   grep "chat-mode" scripts/wave{N}/generate_wave{N}_phase{X}_scripts.py
   ```

2. **Command pattern matches SOP**:
   ```bash
   grep "bob --" scripts/wave{N}/generate_wave{N}_phase{X}_scripts.py
   ```

3. **Output format matches SOP**:
   ```bash
   grep "0{X}-" scripts/wave{N}/generate_wave{N}_phase{X}_scripts.py
   ```

4. **Validation requirements match SOP**:
   - Check prompt includes required checks
   - Check manifest update logic
   - Check bobcoin reporting

### Step 4: Upload Scripts to VM

**Upload all scripts**:
```bash
gcloud compute scp scripts/wave{N}/_p{X}_*.sh v12-test-golden-v2:~/universal-or-strategy/scripts/wave{N}/ --zone=us-central1-a
```

**Set permissions**:
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="chmod +x ~/universal-or-strategy/scripts/wave{N}/_p{X}_*.sh"
```

### Step 5: MANDATORY Upload Verification

**CRITICAL**: Always verify ALL scripts uploaded successfully before proceeding.

**Count expected scripts**:
```bash
# Count local scripts
ls scripts/wave{N}/_p{X}_*.sh | wc -l
```

**Verify on VM**:
```bash
# Count VM scripts
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="ls ~/universal-or-strategy/scripts/wave{N}/_p{X}_*.sh | wc -l"
```

**Compare counts**:
```bash
# If counts don't match, STOP and investigate
LOCAL_COUNT=$(ls scripts/wave{N}/_p{X}_*.sh | wc -l)
VM_COUNT=$(gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="ls ~/universal-or-strategy/scripts/wave{N}/_p{X}_*.sh | wc -l")

if [ "$LOCAL_COUNT" != "$VM_COUNT" ]; then
    echo "ERROR: Upload incomplete. Local: $LOCAL_COUNT, VM: $VM_COUNT"
    exit 1
fi
```

**Why This Matters**:
- Wave 4 Phase 5: 7 scripts never uploaded → 7 epics failed
- Silent failure: No error message, scripts just missing
- Cost: 1-2 hours recovery time + debugging effort

**DO NOT PROCEED** until counts match exactly.

### Step 6: Test with 2 Epics (Pilot Test)

**Run pilot test**:
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="cd universal-or-strategy && ./scripts/wave{N}/_p{X}_116.sh"
```

**Verify output format**:
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="ls -lh docs/brain/EPIC-CCN-116/0{X}-*.md"
```

**Deploy all only after pilot success**.

### Step 7: Document Any Deviations

**If pattern must change**:
1. Create `WAVE{N}_PHASE{X}_DEVIATION.md`
2. Document why pattern changed
3. Update this SOP with new pattern
4. Verify with Director before proceeding

---

## CRITICAL: Bob CLI Invocation Pattern (V3.9 - MANDATORY)

**Effective**: 2026-06-18 (Post-Wave 6 Phase 1.5 Freeze Incident)

### The Two-Step Pattern (MANDATORY for ALL Phases)

**ALL phase scripts MUST use this exact pattern**:

```bash
# Step 1: Create message file
cat > /tmp/phaseX_msg_$EPIC_ID.txt << 'EOFMSG'
[Full message content here]
EOFMSG

# Step 2: Invoke Bob with command substitution
bob --yolo --chat-mode MODE "$(cat /tmp/phaseX_msg_$EPIC_ID.txt)" 2>&1 | tee "logs/waveN/phaseX/$EPIC_ID.log"
```

### Why This Pattern is Required

**Root Cause (Wave 6 Phase 1.5 Freeze)**:
- ❌ Inline message strings cause Bob CLI to freeze waiting for stdin
- ❌ Shell variable expansion interferes with message parsing
- ❌ Piped output (`| tee`) blocks stdin when message is incomplete

**Solution (Wave 4 Proven Pattern)**:
- ✅ Temp file ensures message is complete before Bob reads it
- ✅ Command substitution (`$(cat ...)`) delivers full content in one chunk
- ✅ No stdin ambiguity - Bob receives complete message immediately

### VM vs Local Invocation (SAME PATTERN)

**Both VM and local execution use identical pattern**:

#### VM Execution (Automated Waves)
```bash
# On VM: /home/malhitticrypto/universal-or-strategy
cat > /tmp/phase1_msg_001.txt << 'EOFMSG'
[message]
EOFMSG

bob --yolo --chat-mode plan "$(cat /tmp/phase1_msg_001.txt)"
```

#### Local Execution (Manual Testing)
```bash
# On Windows: c:\WSGTA\universal-or-strategy
cat > /tmp/phase1_msg_001.txt << 'EOFMSG'
[message]
EOFMSG

bob --yolo --chat-mode plan "$(cat /tmp/phase1_msg_001.txt)"
```

**Key Point**: The pattern is IDENTICAL. The only difference is the working directory.

### What NOT to Do

❌ **NEVER use inline message strings**:
```bash
# THIS CAUSES FREEZE - DO NOT USE
bob --yolo --chat-mode plan "Define scope for $EPIC_ID..."
```

❌ **NEVER use --message flag without temp file**:
```bash
# THIS ALSO CAUSES ISSUES - DO NOT USE
bob --yolo --chat-mode plan --message "Define scope..."
```

✅ **ALWAYS use temp file + command substitution**:
```bash
# THIS WORKS - ALWAYS USE THIS PATTERN
cat > /tmp/msg.txt << 'EOFMSG'
[message]
EOFMSG
bob --yolo --chat-mode plan "$(cat /tmp/msg.txt)"
```

### Validation Checklist

Before generating ANY phase scripts, verify:
- [ ] Script uses `cat > /tmp/phaseX_msg_$EPIC_ID.txt << 'EOFMSG'`
- [ ] Script uses `bob --yolo --chat-mode MODE "$(cat /tmp/...)"`
- [ ] NO inline message strings in bob command
- [ ] NO --message flag without temp file
- [ ] Pattern matches Wave 4 exactly

---
## CRITICAL: Screen Session Script Protocol (V3.10 - MANDATORY)

**Reference**: `docs/protocol/SCREEN_SESSION_SCRIPT_PROTOCOL.md`

### No Heredocs in Screen Sessions

**HEREDOCS ARE BANNED in all scripts launched via screen sessions.**

Wave 7 incident (2026-06-22): 72/161 epics failed due to nested heredoc syntax errors.

#### The Rule

```bash
# ❌ WRONG - Heredoc in screen script
cat > /tmp/message.txt << 'EOF'
Message content
EOF

# ✅ CORRECT - Python file writing
# In generator:
with open(f'/tmp/message_{epic_num}.txt', 'w') as f:
    f.write(message_content)

# In script:
command "$(cat /tmp/message_{epic_num}.txt)"
```

### Pre-Launch Syntax Validation (MANDATORY)

**ALL generated scripts MUST pass `bash -n` validation before launch.**

Add to every script generator:

```python
def validate_script_syntax(script_path: str) -> bool:
    """Validate bash script syntax"""
    result = subprocess.run(
        ['bash', '-n', script_path],
        capture_output=True,
        text=True
    )
    if result.returncode != 0:
        print(f"[ERROR] Syntax error in {script_path}:")
        print(result.stderr)
        return False
    return True

# After generating all scripts
print("\n[*] Validating script syntax...")
failed = []
for epic_num in epic_numbers:
    script_path = f"scripts/wave{wave_num}/_p{phase}_{epic_num:03d}.sh"
    if not validate_script_syntax(script_path):
        failed.append(epic_num)

if failed:
    print(f"\n[ERROR] {len(failed)} scripts failed syntax validation")
    sys.exit(1)

print(f"[OK] All scripts passed syntax validation")
```

### Incremental Rollout (MANDATORY)

**NEVER launch all epics at once without validation.**

1. **Pilot** (3 epics): Low/medium/high complexity
2. **First Batch** (10 epics): Verify all complete
3. **Full Wave** (remaining): Only if pilot + batch succeeded

### Validation Checklist

- [ ] No heredocs in generated scripts
- [ ] Syntax validation added to generator
- [ ] All scripts pass `bash -n` check
- [ ] Pilot test (3 epics) completed successfully
- [ ] First batch (10 epics) completed successfully
- [ ] Screen Session Script Protocol reviewed

---


## Phase-Specific Requirements

### Phase 0 (Hotspot Analysis)
- **Mode**: `ask`
- **Command**: `bob --yolo --chat-mode ask "$(cat /tmp/phase0_msg_$EPIC_ID.txt)"`
- **Output**: `00-hotspots.md`, `manifest.json`
- **Validation**: jCodemunch hotspot data

### Phase 1 (Scope Definition)
- **Mode**: `plan`
- **Command**: `bob --yolo --chat-mode plan "$(cat /tmp/phase1_msg_$EPIC_ID.txt)"`
- **Output**: `00-scope.md`, `01-scope-boundary.md` (combined Phase 1 + 1.5)
- **Validation**: Single-method boundary

### Phase 1.5 (Scope Boundary Validation)
- **Mode**: `v12-phase1-5-boundary`
- **Command**: `bob --yolo --chat-mode v12-phase1-5-boundary "$(cat /tmp/phase1_5_msg_$EPIC_ID.txt)"`
- **Output**: `01-scope-boundary.md`
- **Validation**: Single-method boundary, no scope creep
- **Note**: Wave 4 combined this with Phase 1; Wave 6+ separates it

### Phase 2 (Architecture Planning)
- **Mode**: `plan`
- **Command**: `bob --yolo --chat-mode plan "$(cat /tmp/phase2_msg_$EPIC_ID.txt)"`
- **Output**: `02-architecture-plan.md`, `02-diagrams.mmd`
- **Validation**: Jane Street alignment

### Phase 3 (DNA & PR Audit)
- **Mode**: `advanced`
- **Command**: `bob --yolo --chat-mode advanced "$(cat /tmp/phase3_msg_$EPIC_ID.txt)"`
- **Output**: `03-audit-report.md`
- **Validation**: DNA compliance, PR hygiene

### Phase 4 (Ticket Generation)
- **Mode**: `plan`
- **Command**: `bob --yolo --chat-mode plan "$(cat /tmp/phase4_msg_$EPIC_ID.txt)"`
- **Output**: `04-tickets.md`
- **Validation**: Ticket breakdown, execution order

### Phase 5 (Ticket Execution)
- **Mode**: `v12-engineer`
- **Command**: `bob --yolo --chat-mode v12-engineer "$(cat /tmp/phase5_msg_$EPIC_ID.txt)"`
- **Output**: `ticket-X-completion.md`
- **Validation**: Build passes, tests pass
- **Test Framework**: xUnit ONLY (project uses xUnit 2.9.0+)
  - ✅ Use: `[Fact]`, `Assert.Equal()`, `Assert.NotNull()`, `Assert.True()`
  - ❌ NEVER use: NUnit (`[Test]`, `[TestFixture]`, `Assert.AreEqual()`, `Assert.IsNotNull()`)
  - ❌ NEVER use: MSTest (`[TestMethod]`, `[TestClass]`)
  - **Rationale**: EPIC-027 TICKET-1 generated NUnit tests → 29 compilation errors → manual conversion required

### Phase 6 (Final Review)
- **Mode**: `advanced`
- **Command**: `bob --yolo --chat-mode advanced "$(cat /tmp/phase6_msg_$EPIC_ID.txt)"`
- **Output**: `05-completion-report.md`
- **Validation**: All tickets verified, roadmap updated

---

## Common Mistakes

### Mistake 1: Copying Adjacent Phase

**Wrong**:
```bash
# Copying Wave 3 Phase 2 for Wave 3 Phase 3
cp scripts/wave3/generate_wave3_phase2_scripts.py scripts/wave3/generate_wave3_phase3_scripts.py
```

**Right**:
```bash
# Copying Wave 2 Phase 3 for Wave 3 Phase 3
cp scripts/wave2/generate_phase3_scripts.py scripts/wave3/generate_wave3_phase3_scripts.py
```

### Mistake 2: Changing Mode

**Wrong**:
```python
# Changing mode from 'advanced' to 'plan'
bob --yolo --chat-mode plan "$(cat /tmp/phase3_msg_X.txt)"
```

**Right**:
```python
# Keeping mode as 'advanced' (from Wave 2 Phase 3)
bob --yolo --chat-mode advanced "$(cat /tmp/phase3_msg_X.txt)"
```

### Mistake 3: Skipping Test

**Wrong**:
```bash
# Deploying all 10 scripts without testing
gcloud compute scp _p3_*.sh v12-test-golden-v2:~/universal-or-strategy/
```

**Right**:
```bash
# Testing 2 scripts first
gcloud compute scp _p3_116.sh _p3_117.sh v12-test-golden-v2:~/universal-or-strategy/
# Verify output format
# Deploy all only after success
```

---

## Verification Checklist

Before deploying any phase scripts, verify:

- [ ] **SKILL READING COMPLETE** (Step -3 - V12.39 BLOCKING GATE)
- [ ] **PRE-WAVE VALIDATION PASSED** (Step -2 - V12.39 MANDATORY)
- [ ] **ENCODING PRE-CHECK PASSED** (Step 0 - V12.33 MANDATORY)
- [ ] **VM-LOCAL GIT SYNC VERIFIED** (Step 0.5 - V12.37 MANDATORY)
- [ ] Copied from previous wave's SAME phase (not adjacent phase)
- [ ] Updated epic numbers only (107-115 → 116-125)
- [ ] Mode matches SOP (ask/plan/advanced/v12-engineer)
- [ ] Command pattern matches SOP
- [ ] Output format matches SOP (0X-*.md)
- [ ] Validation requirements match SOP
- [ ] **UPLOADED ALL SCRIPTS TO VM** (Step 4)
- [ ] **VERIFIED SCRIPT COUNT MATCHES** (Step 5 - MANDATORY)
- [ ] Tested with 2 epics first (pilot test)
- [ ] Output format verified
- [ ] Ready to deploy all

---

## Recovery Procedure

If wrong output format detected:

1. **STOP immediately** - Do not deploy remaining scripts
2. **Identify root cause** - Check which phase was copied
3. **Create corrected generator** - Copy correct phase from previous wave
4. **Test with 2 epics** - Verify output format
5. **Deploy all** - Only after success
6. **Document failure** - Update lessons learned

---

## Success Metrics

### Per Phase
- ✅ All scripts generated without errors
- ✅ All scripts use correct mode
- ✅ All scripts produce correct output format
- ✅ All scripts complete within budget
- ✅ All APIs remain positive

### Per Wave
- ✅ All phases follow SOP
- ✅ No architecture bugs
- ✅ No wrong output formats
- ✅ Budget maintained (>80% remaining)

---

## Step 11: Post-Wave Rollback (V12.38 - NEW)

**Purpose**: Standardize rollback procedures for failed waves.

**When to Execute**: After wave completion if quality gates fail.

### Rollback Decision

**Automatic Triggers** (no approval needed):
- P0 compilation blocker in ANY PR
- >20% epic failure rate
- >5 P0 issues in Greptile audit
- Scope creep in >10% of epics

**Manual Triggers** (Director approval required):
- 10-20% failure rate with 0 P0 issues
- Cost-benefit analysis favors rollback
- Systemic protocol gap detected

### Rollback Decision Matrix

Use this matrix to determine keep/skip/local/retry scope:

| Scenario | Keep | Skip | Local | Retry | Rationale |
|----------|------|------|-------|-------|-----------|
| All PRs clean | All | 0 | 0 | 0 | No rollback needed |
| 1-2 PRs buggy | Clean | Invalid | Encoding | Buggy | Surgical fix |
| >50% PRs buggy | 0 | Invalid | Encoding | All | Full rollback |
| P0 in ANY PR | 0 | Invalid | Encoding | All | Safety first |

### Rollback Execution

**Follow 4-step procedure**:

1. **Close All PRs**: Use `gh pr close` with rollback reason
2. **Revert Merged PRs**: If any PRs merged before rollback
3. **Delete Phase 5-6 Files**: For retry epics only
4. **Update Roadmap**: Mark invalid/local/retry status

**Complete Protocol**: See `docs/protocol/WAVE_ROLLBACK_PROTOCOL.md`

**Quick Reference**: See `docs/workflow/WAVE_ROLLBACK_CHECKLIST.md`

### Post-Rollback Actions

**Immediate** (Day 0):
1. Execute 4-step rollback
2. Document root cause
3. Identify protocol gaps
4. Create hardening plan

**Short-Term** (Day 1-3):
1. Update protocols (fix gaps)
2. Update SOPs (add missing steps)
3. Update skills (add missing checks)
4. Update custom modes (add missing mandates)

**Validation** (Day 4-7):
1. Run pilot test with hardened protocols
2. Verify 0 P0/P1 issues in pilot
3. Document pilot results
4. Obtain Director approval for retry

**Retry** (Day 8+):
1. Launch retry wave with hardened protocols
2. Monitor closely (first 10 epics)
3. Apply recovery loop if any failures
4. Document improvements

### Rollback Cost Calculation

**Formula**:
```
Lost Cost = (Retry Epics × Phase 5-6 Cost per Epic)
Retry Cost = (Retry Epics × Phase 5-6 Cost per Epic)
Total Impact = Lost Cost + Retry Cost
```

**Example (Wave 4)**:
- Retry Epics: 78
- Phase 5-6 Cost: $0.05/epic
- Lost Cost: $3.90
- Retry Cost: $3.90
- Total Impact: $7.80

### Success Criteria for Retry

**Before Retry Wave**:
- [ ] All protocol gaps fixed
- [ ] Pilot test passed (0 P0/P1 issues)
- [ ] Director approval obtained
- [ ] Cost estimate updated

**During Retry Wave**:
- [ ] First 10 epics monitored closely
- [ ] Recovery loop applied if failures
- [ ] Quality gates enforced

**After Retry Wave**:
- [ ] All epics completed successfully
- [ ] 0 P0 issues in Greptile audit
- [ ] Lessons learned documented

---

## Version History

### V3.8 (2026-06-18)
- **Added**: Lamport Clock Event Log vs Manifest Events section
- **Fixed**: `check_dependencies()` now checks BOTH global event log AND manifest `lamport_events` array
- **Updated**: `scripts/lamport_clock.py` with `_load_manifest_events()` fallback method
- **Reason**: Wave 6 Phase 1 blocked 4 epics - manifests showed Phase 0 complete but events missing from global log
- **Impact**: Permanent fix prevents future manifest migration issues
- **Reference**: scripts/lamport_clock.py (lines 229-318)

### V3.7 (2026-06-16)
- **Added**: Skill Reading Verification section (Step -3) with BLOCKING GATE
- **Updated**: Pre-Wave Validation (Step -2) to reference Step -3 and VM setup verification
- **Updated**: Verification checklist with skill reading requirement
- **Updated**: VM Build check marked as SKIP (VM does NOT have .NET SDK)
- **Reason**: 3 recurring issues - agents not reading skill documentation (ROOT CAUSE), trying to run dotnet build on VM, looking for Bob CLI in wrong location
- **Reference**: docs/protocol/VM_SETUP_PROTOCOL.md (V12.39), .bob/skills/gcp-vm-wave-execution/skill.md (V2.10)

### V3.6 (2026-06-16)
- **Added**: Pre-Wave Validation section (Step -2) with 7-check gate
- **Added**: Post-Wave Rollback section (Step 11) with 4-step procedure
- **Updated**: VM-Local Git Sync to V12.37 (7-step sync with working tree verification)
- **Updated**: Verification checklist with pre-wave validation requirement
- **Reason**: Wave 4 rollback experience + Wave 5 Pilot Test #2 working tree gap
- **Reference**: docs/protocol/WAVE_ROLLBACK_PROTOCOL.md (V12.38)

### V3.5 (2026-06-16)

### V3.5 (2026-06-16)
- **Added**: MANDATORY VM-Local Git Sync (Step 0.5) before script generation
- **Added**: Git sync verification checklist (5 steps)
- **Updated**: Verification checklist with git sync requirement
- **Reason**: Wave 5 pilot test incident (VM on old commit, Bob saw extracted code)
- **Reference**: docs/protocol/VM_LOCAL_GIT_SYNC_PROTOCOL.md (V12.36)

### V3.4 (2026-06-16)
- **Added**: Local execution alternative section with PowerShell adaptations
- **Added**: File I/O protocol for SSH/non-interactive mode
- **Added**: PowerShell command equivalents for grep, cat, ls
- **Updated**: References to include LOCAL_EXECUTION_PATTERN.md
- **Reason**: EPIC-CCN-016 local completion after VM Phase 5 failure
- **Reference**: building-blocks/autonomous-refactoring/LOCAL_EXECUTION_PATTERN.md

### V3.3 (2026-06-16)
- **Added**: MANDATORY encoding pre-check (Step 0) before script generation
- **Added**: UTF-8 compliance verification using check_encoding.ps1
- **Updated**: Verification checklist with encoding pre-check requirement
- **Reason**: EPIC-CCN-027 TICKET-2 UTF-16 encoding failure (Bob CLI 0% similarity)
- **Reference**: docs/protocol/FILE_ENCODING_PROTOCOL.md (V12.33)

### V3.2 (2026-06-16)
- **Added**: xUnit framework requirement for Phase 5 (Ticket Execution)
- **Added**: Test framework validation (xUnit ONLY, no NUnit/MSTest)
- **Updated**: Phase 5 requirements with explicit framework constraints
- **Reason**: EPIC-027 TICKET-1 NUnit mismatch (29 errors, manual conversion)

### V3.1 (2026-06-15)
- **Added**: MANDATORY upload verification step (Step 5)
- **Added**: Script count comparison protocol
- **Updated**: Verification checklist with upload verification
- **Reason**: Wave 4 Phase 5 failures (7 scripts never uploaded to VM)

### V3.0 (2026-06-13)
- **Added**: Golden Rule (always copy same phase from previous wave)
- **Added**: Common mistakes section
- **Added**: Recovery procedure
- **Reason**: Wave 3 Phase 3 architecture bug (copied adjacent phase)

### V2.0 (2026-06-12)
- **Added**: Test with 2 epics before full deployment
- **Added**: Verification checklist
- **Reason**: Wave 3 Phase 1 failures (3 attempts)

### V1.0 (2026-06-11)
- **Initial**: Basic script generation procedure
- **Reason**: Wave 2 Phase 0 success

---

## Local Execution Alternative (V3.4)

**When to Use Local Execution**:
- VM execution failed for specific epics
- Need to debug phase execution interactively
- Want to verify phase output before VM deployment
- Recovering from VM failures (e.g., EPIC-CCN-016)

**Pattern**: Execute phases locally using Bob CLI, one at a time, mirroring VM script execution.

### Local Execution Steps

**1. Extract Phase Script from VM**:
```powershell
# Read the VM script to get API key and instructions
Get-Content scripts/wave4/_p1_016.sh
```

**2. Set API Key**:
```powershell
# Extract API key from line 10 of VM script
$env:BOBSHELL_API_KEY='bob_prod_bob-admin_...'
```

**3. Execute Phase with Bob CLI**:
```powershell
# Use same mode and instructions as VM script
bob --yolo --chat-mode plan @"
[Copy instructions from VM script's heredoc]
"@
```

**4. Verify Output**:
```powershell
# Check files created
Get-Item docs/brain/EPIC-CCN-XXX/0X-*.md | Select-Object Name, Length
```

**5. Repeat for Next Phase**:
```powershell
# Move to next phase script (_p2_016.sh)
# Extract new API key
# Execute with Bob CLI
```

### PowerShell Adaptations

**File I/O Protocol** (CRITICAL):
- ❌ NEVER use Bob's `write_to_file`, `read_file`, `run_shell_command` in SSH mode
- ✅ ALWAYS use `execute_command` with PowerShell heredoc syntax
- ✅ Set `cwd` parameter explicitly for directory-specific commands

**File Creation**:
```powershell
# WRONG (Bob's write_to_file fails in SSH mode)
write_to_file("path/file.md", "content")

# CORRECT (PowerShell heredoc)
@'
content here
'@ | Out-File -FilePath path/file.md -Encoding UTF8
```

**File Reading**:
```powershell
# WRONG (Bob's read_file fails in SSH mode)
read_file("path/file.md")

# CORRECT (PowerShell)
Get-Content path/file.md -Raw
```

**Method Extraction**:
```powershell
# WRONG (grep doesn't exist on Windows)
grep -A 50 "TryHandleFleet_CancelAll" src/file.cs

# CORRECT (PowerShell regex)
$content = Get-Content src/file.cs -Raw
if ($content -match '(?s)TryHandleFleet_CancelAll.*?^\s*\}') {
    $matches[0]
}
```

### Success Criteria (Same as VM)

**Per Phase**:
- ✅ Output files created in `docs/brain/EPIC-CCN-XXX/`
- ✅ File sizes >1K (not empty)
- ✅ Build passes (for code-changing phases)
- ✅ Bobcoin usage reported

**Complete Walkthrough**: See `building-blocks/autonomous-refactoring/LOCAL_EXECUTION_PATTERN.md`

---

## References

- **Wave 3 Phase 3 Bug**: `WAVE3_PHASE3_ARCHITECTURE_BUG_ANALYSIS.md`
- **Lessons Learned**: `building-blocks/autonomous-refactoring/WAVE3_PHASE3_LESSONS_LEARNED.md`
- **Complete Handoff**: `WAVE3_PHASE3_COMPLETE_HANDOFF.md`
- **Local Execution Pattern**: `building-blocks/autonomous-refactoring/LOCAL_EXECUTION_PATTERN.md` (V1.0)

---

**MANDATORY COMPLIANCE**: All agents MUST follow this SOP for all phase script generation.

**Violation Consequences**: Wrong output format, wasted bobcoins, debugging time, architecture rewrites.

**Next Update**: After Wave 4 completion.