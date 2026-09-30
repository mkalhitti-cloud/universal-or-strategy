# VM-Local Git Sync Protocol (V12.38)

**Version**: 1.3
**Effective**: 2026-07-13
**Status**: 🔴 MANDATORY - BLOCKING GATE
**Severity**: P0 (Wave execution blocker)

## Two-Track Local Development Model (V12.38)

This is the foundational model for all local development. Two worktrees, one
repo, one IDE window. No switching. No stashing. No pollution.

### Setup

```
C:\WSGTA\
  universal-or-strategy\           <- TRACK 1: wave work
    branch: main / wave7/pr-X
    open in: Arena.code-workspace (root 1)
    who writes here: VM agent + you for F5 verification

  universal-or-strategy-director\  <- TRACK 2: director work
    branch: director (or 001-agent-arena-platform)
    open in: Arena.code-workspace (root 2)
    who writes here: YOU ONLY -- docs, specs, protocols, rules
```

Open `Arena.code-workspace` -- both roots appear in one Bob IDE window:

```
Bob IDE
  STRATEGY -- WAVE WORK (main)
    src/
    docs/brain/
    ...

  DIRECTOR -- DOCS/SPEC/PROTOCOL (director)
    docs/protocol/
    docs/workflow/
    specs/
    AGENTS.md
    .bob/
    ...
```

### The Rules

| Rule | Detail |
|------|--------|
| Track 1 is wave-only | Only VM agent and F5 verification touches this folder |
| Track 2 is director-only | Only you touch this -- no agent wave work ever |
| No switching branches | Each folder has its own permanent branch -- never checkout in Track 1 while wave runs |
| No stashing | Changes in Track 2 are always safe -- completely separate working tree |
| Non-.cs files: direct push | docs/protocol/AGENTS.md/.bob/ -- commit and push to main directly, no PR |
| .cs files: PR + F5 gate | All src/ changes go through wave branch PR and F5 verification |
| VM never sees director | director branch is local only -- never push to VM |
| Arena spec lives in Track 2 | specs/ folder edited in director worktree only |

### Day to Day

```
VM running wave work?
  -> You open Track 2 (director worktree) in same IDE window
  -> Edit docs/protocol/AGENTS.md/specs/ freely
  -> git add . && git commit && git push origin main  (direct, no PR)
  -> Zero conflict with VM -- different files, different track

Wave PR ready for F5?
  -> Switch to Track 1 root in same IDE window
  -> git fetch origin && git checkout wave7/pr-A
  -> F5 in NinjaTrader
  -> GREEN -> merge on GitHub
  -> git checkout main && git pull origin main
  -> Back to Track 2 for director work -- no stash needed

Want to work on Arena spec?
  -> Track 2 is already on 001-agent-arena-platform branch
  -> Edit specs/ freely
  -> git commit -- stays local, never touches VM
```

### Worktree Commands (Reference)

```powershell
# See all active worktrees
git worktree list

# Track 1 is the main repo folder (always exists)
# Track 2 was created with:
# git worktree add ..\universal-or-strategy-director director

# Remove worktree if ever needed
# git worktree remove ..\universal-or-strategy-director
```

---

## Problem Statement

**Wave 5 Pilot Test Incident #1** (2026-06-15): VM was on commit `0d28fb4` (Wave 4 work) while local was on `dad30745` (post-rollback). This caused Bob to see already-extracted code (CYC=10) instead of the baseline code that needed extraction.

**Wave 5 Pilot Test Incident #2** (2026-06-16): Commits matched (`810cfb2f`) but working tree had stale Wave 4 files (`05-completion.md` from 2026-06-15). Bob found EPIC-CCN-001 already complete (CYC=10) instead of baseline needing extraction.

**Root Cause**: V12.36 protocol verified commits match but didn't verify working tree clean or baseline files. `git reset --hard` updates HEAD but doesn't remove untracked files.

## The Rule

**BEFORE EVERY WAVE EXECUTION**: VM and local MUST be on the SAME git commit.

**AFTER EVERY WAVE EXECUTION**: Sync VM changes back to local for PR creation.

## Pre-Wave Git Sync Checklist (Local → VM) - 7 Steps

**Purpose**: Ensure VM starts with correct baseline code before wave execution.

**Version**: V12.37 (7-step sync with working tree and baseline verification)

### Step 1: Verify Local Git State

```bash
# Check current commit
git log -1 --oneline
# Expected: Shows latest commit hash and message

# Check branch
git branch --show-current
# Expected: gitbutler/workspace (or your working branch)

# Check for uncommitted changes
git status
# Expected: Clean working tree (no uncommitted src/ changes)
```

**BLOCKER**: If uncommitted src/ changes exist, commit or stash them before proceeding.

### Step 2: Push Local Commits to Origin

```bash
# Push to origin (creates/updates remote branch)
git push origin gitbutler/workspace --force

# Verify push succeeded
git log origin/gitbutler/workspace -1 --oneline
# Expected: Same commit as local HEAD
```

**Note**: Use `--force` if rebasing or amending commits. Use `--no-verify` only if pre-push validation blocks non-critical issues (e.g., test DLL missing).

### Step 3: Verify VM Git State (BEFORE Sync)

```bash
# Check VM current commit
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="cd ~/universal-or-strategy && git log -1 --oneline"

# Check VM branch
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="cd ~/universal-or-strategy && git branch --show-current"

# Check for uncommitted changes on VM
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="cd ~/universal-or-strategy && git status --short"
```

**Document**: Record VM commit hash and compare to local. If different, proceed to Step 4.

### Step 4: Sync VM to Match Local

```bash
# Fetch latest from origin
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="cd ~/universal-or-strategy && git fetch origin"

# Reset VM to match local commit (HARD RESET)
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="cd ~/universal-or-strategy && git reset --hard origin/gitbutler/workspace"

# Clean untracked files (Wave 4 artifacts, etc.)
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="cd ~/universal-or-strategy && git clean -fd"
```

**CRITICAL**: `git reset --hard` will DESTROY any uncommitted work on VM. Ensure VM has no valuable uncommitted changes before running.

### Step 5: Verify Sync Succeeded (MANDATORY)

```bash
# Check VM commit matches local
LOCAL_COMMIT=$(git log -1 --format="%H")
VM_COMMIT=$(gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="cd ~/universal-or-strategy && git log -1 --format='%H'")

echo "Local: $LOCAL_COMMIT"
echo "VM:    $VM_COMMIT"

# Compare (must match exactly)
if [ "$LOCAL_COMMIT" = "$VM_COMMIT" ]; then
  echo "✅ SYNC VERIFIED: VM and local on same commit"
else
  echo "❌ SYNC FAILED: Commits do not match"
  exit 1
fi
```

**BLOCKER**: If commits don't match, STOP and investigate. Do NOT proceed with wave execution.

### Step 6: Verify Working Tree Clean (V12.37 - NEW)

```bash
# Check for uncommitted changes or untracked files
VM_STATUS=$(gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="cd ~/universal-or-strategy && git status --porcelain")

if [ -z "$VM_STATUS" ]; then
  echo "✅ WORKING TREE CLEAN: No uncommitted changes"
else
  echo "❌ WORKING TREE DIRTY: Uncommitted changes detected"
  echo "$VM_STATUS"
  exit 1
fi
```

**Why This Matters**: `git reset --hard` updates HEAD but doesn't remove untracked files. Explicit verification prevents stale data from previous waves.

**Example Failure**: Wave 5 Pilot Test #2 - commits matched but `05-completion.md` existed as untracked file from Wave 4.

**BLOCKER**: If working tree is not clean, investigate and clean before proceeding.

### Step 7: Verify Baseline Files (V12.37 - NEW)

```bash
# For pilot tests: verify Phase 5 files DON'T exist for pilot epic
VM_P5_FILES=$(gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="ls ~/universal-or-strategy/docs/brain/EPIC-CCN-001/05-*.md 2>/dev/null | wc -l")

if [ "$VM_P5_FILES" = "0" ]; then
  echo "✅ BASELINE VERIFIED: No Phase 5 files in EPIC-CCN-001"
else
  echo "❌ BASELINE CORRUPTED: Found $VM_P5_FILES Phase 5 files (expected 0)"
  exit 1
fi
```

**Why This Matters**: Prevents Bob from seeing already-extracted code (CYC=10) instead of baseline code needing extraction.

**Use Case**: Before pilot tests, verify brain files match expected baseline (no Phase 5/6 completion files).

**BLOCKER**: If baseline is corrupted, run nuclear clean (see below) before proceeding.

### Step 8: Verify Source Files Match (Optional but Recommended)

```bash
# Check specific file hash on VM vs local
LOCAL_HASH=$(md5sum src/V12_002.cs | awk '{print $1}')
VM_HASH=$(gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="cd ~/universal-or-strategy && md5sum src/V12_002.cs | awk '{print \$1}'")

echo "Local V12_002.cs: $LOCAL_HASH"
echo "VM V12_002.cs:    $VM_HASH"

if [ "$LOCAL_HASH" = "$VM_HASH" ]; then
  echo "✅ FILE VERIFIED: V12_002.cs matches"
else
  echo "❌ FILE MISMATCH: V12_002.cs differs"
  exit 1
fi
```

**Use Case**: Verify critical files (e.g., target file for EPIC-CCN-001) match exactly.

## Nuclear Clean Option (V12.37)

**Purpose**: Complete VM state reset when working tree or baseline is corrupted.

**When to Use**:
- Before pilot tests (ensure clean baseline)
- After failed waves (remove partial artifacts)
- When Step 6 or Step 7 fails (working tree dirty or baseline corrupted)

**Commands**:

```bash
# Step 1: Remove ALL Wave 4+ brain files
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="cd ~/universal-or-strategy && rm -rf docs/brain/EPIC-CCN-*/05-*.md docs/brain/EPIC-CCN-*/ticket-*.md docs/brain/EPIC-CCN-*/06-*.md"

# Step 2: Hard reset + clean untracked files
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="cd ~/universal-or-strategy && git fetch origin && git reset --hard origin/gitbutler/workspace && git clean -fdx"

# Step 3: Verify baseline (example for EPIC-CCN-001)
VM_P5_COUNT=$(gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="ls ~/universal-or-strategy/docs/brain/EPIC-CCN-001/05-*.md 2>/dev/null | wc -l")

if [ "$VM_P5_COUNT" = "0" ]; then
  echo "✅ Nuclear clean successful"
else
  echo "❌ Nuclear clean failed: $VM_P5_COUNT files remain"
  exit 1
fi
```

**CRITICAL**: `git clean -fdx` removes ALL untracked files including `.env`, logs, and local configs. Use with caution.

## Post-Wave Git Sync Checklist (VM → Local)

**Purpose**: Sync VM changes back to local for PR creation and verification.

**When to Use**: After wave execution completes, before creating PRs.

### Step 1: Verify VM Git State

```bash
# Check VM current commit
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="cd ~/universal-or-strategy && git log -1 --oneline"

# Check for uncommitted changes on VM
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="cd ~/universal-or-strategy && git status --short"
```

**Expected**: VM should have uncommitted changes (modified src/ files, new brain files).

### Step 2: Download Changed Files from VM

```bash
# Download brain files (Phase 5-6 outputs)
gcloud compute scp -r \
  v12-test-golden-v2:~/universal-or-strategy/docs/brain/EPIC-CCN-* \
  ./docs/brain/ \
  --zone=us-central1-a

# Download modified source files
gcloud compute scp \
  v12-test-golden-v2:~/universal-or-strategy/src/V12_002.*.cs \
  ./src/ \
  --zone=us-central1-a

# Download logs (for bobcoin tracking)
gcloud compute scp -r \
  v12-test-golden-v2:~/universal-or-strategy/logs/wave5/ \
  ./logs/ \
  --zone=us-central1-a
```

### Step 3: Verify Files Downloaded

```bash
# Check brain files
ls docs/brain/EPIC-CCN-*/ticket-*-completion.md | wc -l
# Expected: Number of completed tickets

# Check source files modified
git status --short src/
# Expected: Modified files listed

# Check logs downloaded
ls logs/wave5/*.log | wc -l
# Expected: Number of epics executed
```

### Step 4: Run Local Verification (5 Mandatory Checks)

**Check 1: Compilation**
```powershell
dotnet build
# Expected: Exit code 0, zero errors
```

**Check 2: Complexity Reduction**
```bash
python scripts/complexity_audit.py
# Expected: Target methods CYC ≤8
```

**Check 3: Scope Compliance**
```bash
git diff HEAD --stat
# Expected: ONLY target files modified, no adjacent changes
```

**Check 4: Test Coverage**
```powershell
dotnet test
# Expected: xUnit tests exist and pass
```

**Check 5: Encoding Compliance**
```powershell
powershell -File .\scripts\check_encoding.ps1
# Expected: Exit code 0, all files UTF-8 without BOM
```

### Step 5: Commit and Push to Local Branch

```bash
# Stage changes
git add docs/brain/ src/ logs/

# Commit with wave summary
git commit -m "feat(wave5): Phase 5-6 execution complete

- Epics: EPIC-CCN-001 through EPIC-CCN-078
- Tickets: [count] tickets executed
- CYC: [before] → [after] (target ≤8)
- Tests: [count] xUnit tests passing
- Encoding: UTF-8 verified

Wave 5 execution with hardened protocols (V12.34-V12.36)"

# Push to origin
git push origin gitbutler/workspace
```

### Step 6: Verify Local and VM Still Synced (Optional)

```bash
# If VM committed changes, verify local matches
LOCAL_COMMIT=$(git log -1 --format="%H")
VM_COMMIT=$(gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="cd ~/universal-or-strategy && git log -1 --format='%H'")

echo "Local: $LOCAL_COMMIT"
echo "VM:    $VM_COMMIT"

# Note: VM may have uncommitted changes, that's OK
# What matters is local has all VM changes downloaded
```

## Integration Points

### 1. Wave Phase Script Generation SOP (V3.1 Update)

**Add to Section 2.1 (Pre-Generation Checklist)**:

```markdown
### 2.1.1 VM-Local Git Sync (MANDATORY)

Before generating ANY wave scripts:

1. ✅ Run VM-Local Git Sync Protocol (V12.36)
2. ✅ Verify local and VM on same commit
3. ✅ Document commit hash in wave monitoring file

**Reference**: `docs/protocol/VM_LOCAL_GIT_SYNC_PROTOCOL.md`
```

### 2. GCP VM Wave Execution Skill (V2.6 Update)

**Add to "Pre-Wave Checklist" section**:

```markdown
## Pre-Wave Checklist (MANDATORY)

### 0. VM-Local Git Sync (NEW - V12.36)
- [ ] Local git state clean (no uncommitted src/ changes)
- [ ] Local commits pushed to origin
- [ ] VM fetched latest from origin
- [ ] VM reset to match local commit (git reset --hard)
- [ ] Commit hashes verified identical (local vs VM)
- [ ] Document commit hash in wave monitoring file

**BLOCKER**: If sync fails, STOP and investigate before proceeding.

**Reference**: `docs/protocol/VM_LOCAL_GIT_SYNC_PROTOCOL.md`
```

### 3. Autonomous Refactor Mode Custom Instructions

**Add to "MANDATORY PROTOCOLS" section**:

```markdown
0. **VM-LOCAL GIT SYNC (V12.36 - BLOCKING GATE)**: Before EVERY wave execution,
   verify VM and local are on the SAME git commit. Run VM-Local Git Sync Protocol
   checklist. If commits don't match, STOP and sync before proceeding. Wave 5 pilot
   incident: VM on old commit (0d28fb4) caused Bob to see already-extracted code.
   Reference: docs/protocol/VM_LOCAL_GIT_SYNC_PROTOCOL.md
```

### 4. Wave Monitoring Template

**Add to every wave monitoring file**:

```markdown
## Git Sync Verification (V12.36)

**Pre-Wave Sync**:
- Local commit: `<hash>` (`<message>`)
- VM commit (before sync): `<hash>` (`<message>`)
- VM commit (after sync): `<hash>` (`<message>`)
- Sync status: ✅ VERIFIED / ❌ FAILED

**Verification Command**:
```bash
LOCAL_COMMIT=$(git log -1 --format="%H")
VM_COMMIT=$(gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="cd ~/universal-or-strategy && git log -1 --format='%H'")
echo "Local: $LOCAL_COMMIT"
echo "VM:    $VM_COMMIT"
```
```

## Common Issues

### Issue: Commits Match But Working Tree Has Stale Data (V12.37)

**Symptom**: Step 5 passes (commits match) but Bob finds already-completed work or stale files.

**Root Cause**: `git reset --hard` doesn't remove untracked files. Working tree can have artifacts from previous waves even when commits match.

**Solution**: Add Step 6 (working tree verification) and Step 7 (baseline file verification) to catch this.

**Example**: Wave 5 Pilot Test #2 (2026-06-16)
- Commits matched: `810cfb2f`
- Working tree had Wave 4 files: `05-completion.md` from 2026-06-15
- Bob found EPIC-CCN-001 already complete (CYC=10)
- Fixed with nuclear clean + 7-step sync

**Prevention**: Always run 7-step sync (V12.37) before pilot tests.

## Failure Scenarios

### Scenario 1: VM Behind Local

**Symptom**: VM commit is older than local commit.

**Cause**: Local work not pushed to origin, or VM not fetched.

**Fix**:
1. Push local commits: `git push origin gitbutler/workspace --force`
2. Fetch on VM: `git fetch origin`
3. Reset VM: `git reset --hard origin/gitbutler/workspace`
4. Verify sync

### Scenario 2: VM Ahead of Local

**Symptom**: VM commit is newer than local commit.

**Cause**: Work done directly on VM (protocol violation), or local not pulled.

**Fix**:
1. Pull VM changes to local: `git pull origin main`
2. Rebase if needed: `git rebase origin/main`
3. Push to origin: `git push origin gitbutler/workspace --force`
4. Verify sync

**CRITICAL**: If VM has uncommitted work, investigate why. VM should NEVER have uncommitted changes between waves.

### Scenario 3: Diverged Histories

**Symptom**: VM and local have different commit histories (not just ahead/behind).

**Cause**: Force pushes, rebases, or parallel work on both environments.

**Fix**:
1. Decide which history is correct (usually local)
2. Force reset VM to match local: `git reset --hard origin/gitbutler/workspace`
3. Clean VM: `git clean -fd`
4. Verify sync

**CRITICAL**: Diverged histories indicate a serious protocol violation. Document root cause.

### Scenario 4: Uncommitted Changes on VM (Expected Post-Wave)

**Symptom**: `git status` on VM shows modified files after wave execution.

**Cause**: Wave execution completed, changes not yet synced to local.

**Fix** (This is NORMAL post-wave):
1. Download changes to local (see Post-Wave Sync Checklist)
2. Run 5 mandatory checks locally
3. Commit to local branch
4. Push to origin
5. Create PR

**Note**: Uncommitted changes on VM BEFORE wave execution are a problem. Uncommitted changes AFTER wave execution are expected and should be synced to local.

### Scenario 5: VM Ahead After Wave Execution

**Symptom**: VM has commits that local doesn't have.

**Cause**: Wave scripts committed changes on VM (unusual but possible).

**Fix**:
1. Pull VM commits to local: `git pull origin main`
2. Rebase if needed: `git rebase origin/main`
3. Verify 5 mandatory checks
4. Push to origin: `git push origin gitbutler/workspace`

## Automation Script (Optional)

Create `scripts/sync_vm_git.sh`:

```bash
#!/bin/bash
set -e

echo "=== VM-Local Git Sync Protocol (V12.36) ==="

# Step 1: Check local state
echo "[1/5] Checking local git state..."
LOCAL_COMMIT=$(git log -1 --format="%H")
LOCAL_MSG=$(git log -1 --format="%s")
echo "  Local: $LOCAL_COMMIT ($LOCAL_MSG)"

# Step 2: Push to origin
echo "[2/5] Pushing to origin..."
git push origin gitbutler/workspace --force --no-verify
echo "  ✅ Pushed to origin/gitbutler/workspace"

# Step 3: Check VM state (before sync)
echo "[3/5] Checking VM git state (before sync)..."
VM_COMMIT_BEFORE=$(gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="cd ~/universal-or-strategy && git log -1 --format='%H'")
echo "  VM (before): $VM_COMMIT_BEFORE"

# Step 4: Sync VM
echo "[4/5] Syncing VM to match local..."
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="cd ~/universal-or-strategy && git fetch origin && git reset --hard origin/gitbutler/workspace && git clean -fd"
echo "  ✅ VM synced"

# Step 5: Verify sync
echo "[5/5] Verifying sync..."
VM_COMMIT_AFTER=$(gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="cd ~/universal-or-strategy && git log -1 --format='%H'")
echo "  VM (after): $VM_COMMIT_AFTER"

if [ "$LOCAL_COMMIT" = "$VM_COMMIT_AFTER" ]; then
  echo "✅ SYNC VERIFIED: VM and local on same commit"
  echo "   Commit: $LOCAL_COMMIT"
  echo "   Message: $LOCAL_MSG"
  exit 0
else
  echo "❌ SYNC FAILED: Commits do not match"
  echo "   Local: $LOCAL_COMMIT"
  echo "   VM:    $VM_COMMIT_AFTER"
  exit 1
fi
```

**Usage**:
```bash
chmod +x scripts/sync_vm_git.sh
./scripts/sync_vm_git.sh
```

## Success Criteria

- ✅ Local and VM on identical commit (hash matches exactly)
- ✅ VM working tree clean (no uncommitted changes)
- ✅ Commit hash documented in wave monitoring file
- ✅ Source files verified matching (optional but recommended)

## Enforcement

**BLOCKING GATE**: This protocol is MANDATORY before every wave execution. Skipping this check is a P0 protocol violation.

**Audit**: Every wave monitoring file MUST include git sync verification section with commit hashes.

**Violation**: If wave execution proceeds without git sync verification, the wave is INVALID and must be rolled back.

## References

- **Wave 5 Pilot Test Incident**: VM on commit 0d28fb4 (Wave 4 work) instead of dad30745 (post-rollback)
- **Root Cause**: No protocol to verify git sync before wave execution
- **Impact**: Bob saw already-extracted code (CYC=10), wasted execution time
- **Fix**: This protocol (V12.36)

## Version History

- **V1.2 (V12.37)** (2026-06-16): Added Step 6 (working tree verification), Step 7 (baseline verification), nuclear clean option, and Wave 5 Pilot Test #2 learnings
- **V1.1** (2026-06-16): Added bidirectional sync (VM → Local post-wave) and 5-check verification
- **V1.0 (V12.36)** (2026-06-16): Initial 5-step protocol created after Wave 5 Pilot Test #1 incident

---

## PR-Gate + F5 Compilation Flow (V12.38)

This section codifies the mandatory end-to-end flow from wave execution on the VM
to a merged PR on main. Every agent and every Director session must follow this.

### The Flow

```
VM
  1. Executes wave work on a named wave branch (e.g. wave7/pr-A)
  2. Pushes branch to GitHub
  3. Opens PR: wave7/pr-A -> main

GitHub PR Bots (automated)
  4. Codacy runs static analysis
  5. CodeRabbit runs AI review
  6. Other bots run (Semgrep, pre-push checks)
  7. PR must reach green / no new blockers before F5 step

LOCAL (you -- mandatory human gate)
  8. git fetch origin
  9. git checkout wave7/pr-A   (the EXACT PR branch -- NOT main)
  10. Open NinjaTrader
  11. Press F5 -- strategy must compile with zero errors
  12. If green --> go to GitHub --> Merge PR into main
  13. If red  --> do NOT merge --> report failure back to VM for fix

VM (next wave)
  14. git pull origin main
  15. Starts next wave on clean, verified main
```

### Rules

| Rule | Detail |
|------|--------|
| Always checkout the PR branch | Never F5 on main -- verify BEFORE merge, not after |
| F5 is a blocking gate | Green F5 required before any merge to main |
| main stays compilable | Only verified wave branches land on main |
| One PR at a time | Do not queue multiple PRs for F5 -- verify each independently |
| VM never touches main directly | VM pushes to wave branches only, never force-pushes main |

### Branch Naming Convention

```
wave7/pr-A    wave7/pr-B    wave7/pr-C ...
     |              |              |
     v              v              v
  PR -> main    PR -> main    PR -> main
  (F5 gate)     (F5 gate)     (F5 gate)
```

### Local Commands (Quick Reference)

```powershell
# Step 1: Fetch all remote branches
git fetch origin

# Step 2: Checkout the PR branch (replace pr-A with actual branch)
git checkout wave7/pr-A

# Step 3: Verify you are on the right branch
git branch --show-current
# Expected: wave7/pr-A

# Step 4: Open NinjaTrader and press F5
# If compile succeeds --> merge on GitHub
# If compile fails   --> do NOT merge, report to VM

# Step 5: After merge, sync main locally
git checkout main
git pull origin main
```

### Arena Spec Branch Isolation

The `001-agent-arena-platform` branch is LOCAL ONLY.
- Never push to VM
- Never include in wave PRs
- Switch to it only when doing spec work, not during wave PR verification

```powershell
# Switching from wave verification back to spec work
git checkout 001-agent-arena-platform

# Switching from spec work to wave verification
git stash   # if any uncommitted spec edits
git checkout wave7/pr-A
```

---

## Version History

- **V1.3 (V12.38)**: Added PR-Gate + F5 Compilation Flow section with full
  end-to-end wave branch lifecycle, F5 blocking gate rules, and Arena spec
  branch isolation guidance
- **V1.2 (V12.37)** (2026-06-16): Added Step 6 (working tree verification), Step 7 (baseline verification), nuclear clean option, and Wave 5 Pilot Test #2 learnings
- **V1.1** (2026-06-16): Added bidirectional sync (VM -> Local post-wave) and 5-check verification
- **V1.0 (V12.36)** (2026-06-16): Initial 5-step protocol created after Wave 5 Pilot Test #1 incident

---

**Protocol Owner**: Wave Execution Lead
**Last Updated**: 2026-07-13
**Next Review**: After Wave 7 completion