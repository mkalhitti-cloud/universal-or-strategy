# Autonomous 4-Worker Launch: Orchestrator Mode

**Date**: 2026-06-10 10:00 PST
**Mode**: Bob IDE Orchestrator → Spawns independent agents for EVERY slash command
**Execution**: Fully autonomous with adversarial review separation
**Critical**: Reviews/verifications MUST be done by independent non-biased agents (not implementing agent)

---

## Pre-Launch Checklist

### ✅ Infrastructure Ready
- [x] Worker 4 worktree created (`C:/WSGTA/universal-or-epic-cluster-4`)
- [x] Epic validation tool ready (`scripts/validate_epic.py`)
- [x] Jane Street standards validated (perfect alignment)
- [x] All infrastructure committed to git (commit 24106860)

### ⚠️ Pre-Launch Actions Required
- [ ] Clean phantom epics (Workers 2 & 3)
- [ ] Validate epic assignments (all 4 workers)

---

## Step 1: Pre-Launch Cleanup (Copy-Paste to PowerShell)

**Run this BEFORE launching orchestrator**:

```powershell
# Clean Worker 2
cd C:/WSGTA/universal-or-epic-cluster-2
if (Test-Path "docs/brain/EPIC-CCN-13") { Remove-Item -Recurse -Force "docs/brain/EPIC-CCN-13"; Write-Host "✅ Worker 2: Cleaned" -ForegroundColor Green } else { Write-Host "✅ Worker 2: Already clean" -ForegroundColor Green }
git reset --hard HEAD

# Clean Worker 3
cd C:/WSGTA/universal-or-epic-cluster-3
if (Test-Path "docs/brain/EPIC-CCN-13") { Remove-Item -Recurse -Force "docs/brain/EPIC-CCN-13"; Write-Host "✅ Worker 3: Cleaned" -ForegroundColor Green } else { Write-Host "✅ Worker 3: Already clean" -ForegroundColor Green }
git reset --hard HEAD

# Return to main
cd C:/WSGTA/universal-or-strategy
Write-Host "✅ Cleanup complete - Ready for orchestrator launch" -ForegroundColor Green
```

---

## Step 2: Launch Orchestrator (Paste to Bob IDE Orchestrator Mode)

### Orchestrator Prompt (Copy-Paste This)

```markdown
# 4-Worker Parallel Epic Execution: Autonomous Launch

**Mission**: Launch 4 independent worker agents to execute epics autonomously in YOLO mode.

**Context**:
- Infrastructure ready (Worker 4 worktree created, validation tool ready)
- Phantom epics cleaned (Workers 2 & 3)
- Jane Street standards validated (lock-free actors, broker-side persistence, hot potato)
- Epic roadmap: 173 epics total, 10 complete, 163 remaining

**Worker Assignments**:
1. Worker 1: EPIC-CCN-33 (EmergencyFlattenSingleFleetAccount) - IN PROGRESS
2. Worker 2: EPIC-CCN-35 - NEW
3. Worker 3: EPIC-CCN-36 - NEW
4. Worker 4: EPIC-CCN-37 - NEW

**Execution Mode**: BATCH MODE (no PRs until all epics complete)

**Your Task**: Spawn 4 independent subtask agents with these instructions:

---

## Worker Agent Instructions (For Each Subtask)

### Worker 1 (Monitor Only)
```
Workspace: C:/WSGTA/universal-or-epic-cluster-1
Mode: v12-engineer
Task: Monitor EPIC-CCN-33 progress, assign next epic when complete

Instructions:
1. Check manifest: docs/brain/EPIC-CCN-33/manifest.json
2. If status = "completed", assign next epic from roadmap
3. If status = "blocked", report to orchestrator
4. If status = "in_progress", wait and check again in 30 minutes
```

### Worker 2 (New Epic)
```
Workspace: C:/WSGTA/universal-or-epic-cluster-2
Mode: Bob IDE Orchestrator
Task: Execute EPIC-CCN-35 with independent agent spawning

Instructions:
1. Validate epic: python scripts/validate_epic.py --epic EPIC-CCN-35 --worker worker-2
2. Execute: /epic-validate EPIC-CCN-35
   - Orchestrator spawns IMPLEMENTATION agent (v12-engineer mode)
   - Implementation agent writes code
3. Review: /epic-verify-ticket EPIC-CCN-35

---

## Bob IDE Orchestrator Architecture (CRITICAL)

### Why Bob IDE Orchestrator Mode?

**Key Advantage**: Bob IDE Orchestrator spawns **independent agents for EVERY slash command**

### Agent Separation (Adversarial Review)

```
Epic Workflow:
┌─────────────────────────────────────────────────────────────┐
│ Bob IDE Orchestrator (Worker X)                             │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  /epic-validate EPIC-CCN-XX                                  │
│  ├─→ Spawns: Implementation Agent (v12-engineer)            │
│  │   - Writes code                                           │
│  │   - Applies diffs                                         │
│  │   - Runs pre-push validation                              │
│  │   - Updates manifest                                      │
│  │                                                            │
│  /epic-verify-ticket EPIC-CCN-XX                             │
│  ├─→ Spawns: Review Agent (advanced mode) ← INDEPENDENT     │
│      - NON-BIASED adversarial review                         │
│      - CANNOT be the implementation agent                    │
│      - Uses jCodemunch MCP for code analysis                 │
│      - Submits findings via submit_review_findings           │
│      - Verifies Jane Street compliance                       │
│                                                               │
│  /pr-loop (if needed)                                        │
│  ├─→ Spawns: Multiple Independent Agents                    │
│      - Bot forensics agent                                   │
│      - Local repair agent                                    │
│      - Verification agent                                    │
│                                                               │
└─────────────────────────────────────────────────────────────┘
```

### Why This Matters

**Problem**: If implementation agent reviews its own code → Biased, misses bugs
**Solution**: Orchestrator spawns independent review agent → Adversarial, catches bugs

**Example**:
- Implementation agent writes code with subtle race condition
- Implementation agent reviews own code → "Looks good!" (biased)
- Independent review agent reviews code → "Race condition detected!" (adversarial)

### Slash Commands That Spawn Independent Agents

| Command | Spawned Agent | Mode | Purpose |
|---------|---------------|------|---------|
| `/epic-validate` | Implementation | v12-engineer | Write code |
| `/epic-verify-ticket` | Review | advanced | Adversarial review |
| `/pr-loop` | Multiple | various | Bot forensics, repair, verify |
| `/epic-scan` | Audit | advanced | DNA & PR audit |
| `/epic-review-final` | Final Review | advanced | Overall assessment |

**CRITICAL**: Each slash command spawns a **NEW** independent agent. No agent reviews its own work.

   - Orchestrator spawns INDEPENDENT REVIEW agent (advanced mode)
   - Review agent is NON-BIASED, adversarial
   - Review agent CANNOT be the implementation agent
4. On completion: Update manifest, pull next epic from roadmap
5. On blocker: Report to main orchestrator, do NOT proceed

CRITICAL: Every slash command spawns a NEW independent agent
```

### Worker 3 (New Epic)
```
Workspace: C:/WSGTA/universal-or-epic-cluster-3
Mode: Bob IDE Orchestrator
Task: Execute EPIC-CCN-36 with independent agent spawning

Instructions:
1. Validate epic: python scripts/validate_epic.py --epic EPIC-CCN-36 --worker worker-3
2. Execute: /epic-validate EPIC-CCN-36
   - Orchestrator spawns IMPLEMENTATION agent (v12-engineer mode)
   - Implementation agent writes code
3. Review: /epic-verify-ticket EPIC-CCN-36
   - Orchestrator spawns INDEPENDENT REVIEW agent (advanced mode)
   - Review agent is NON-BIASED, adversarial
   - Review agent CANNOT be the implementation agent
4. On completion: Update manifest, pull next epic from roadmap
5. On blocker: Report to main orchestrator, do NOT proceed

CRITICAL: Every slash command spawns a NEW independent agent
```

### Worker 4 (New Epic)
```
Workspace: C:/WSGTA/universal-or-epic-cluster-4
Mode: Bob IDE Orchestrator
Task: Execute EPIC-CCN-37 with independent agent spawning

Instructions:
1. Validate epic: python scripts/validate_epic.py --epic EPIC-CCN-37 --worker worker-4
2. Execute: /epic-validate EPIC-CCN-37
   - Orchestrator spawns IMPLEMENTATION agent (v12-engineer mode)
   - Implementation agent writes code
3. Review: /epic-verify-ticket EPIC-CCN-37
   - Orchestrator spawns INDEPENDENT REVIEW agent (advanced mode)
   - Review agent is NON-BIASED, adversarial
   - Review agent CANNOT be the implementation agent
4. On completion: Update manifest, pull next epic from roadmap
5. On blocker: Report to main orchestrator, do NOT proceed

CRITICAL: Every slash command spawns a NEW independent agent
```

---

## Autonomous Execution Rules

### Auto-Approve Criteria (Workers proceed without Director)
✅ Single method extraction (CYC > 20 → CYC ≤ 8)
✅ LOW or MEDIUM risk
✅ No scope creep detected
✅ Passes pre-push validation (all 13 checks)

### Director Approval Required (Workers STOP and report)
❌ HIGH risk
❌ Multiple file changes
❌ Scope expansion detected
❌ Critical blocker (P0)
❌ Pre-push validation fails

### Epic Completion Protocol
1. Worker completes epic
2. Updates manifest: status = "completed"
3. Runs: `python scripts/validate_epic.py --list-next 1`
4. Claims next epic: `python scripts/validate_epic.py --epic EPIC-CCN-XX --worker worker-X`
5. Starts next epic immediately (no waiting)

### Conflict Resolution
- If 2 workers target same file: Serialize (one waits)
- If epic validation fails: Report to orchestrator
- If worktree corrupted: Reset and retry

---

## Orchestrator Monitoring

**Your Role** (Orchestrator):
1. Monitor worker progress via manifest files
2. Assign next epics when workers complete
3. Resolve conflicts or blockers
4. Track overall progress (163 epics remaining)

**Monitoring Command** (run periodically):
```powershell
# Check all worker statuses
@("33","35","36","37") | ForEach-Object {
    $epic = "EPIC-CCN-$_"
    $cluster = if($_ -eq "33"){"1"}elseif($_ -eq "35"){"2"}elseif($_ -eq "36"){"3"}else{"4"}
    $path = "C:/WSGTA/universal-or-epic-cluster-$cluster/docs/brain/$epic/manifest.json"
    Write-Host "`nWorker $cluster ($epic):" -ForegroundColor Yellow
    if (Test-Path $path) { Get-Content $path | Select-String "status" } else { Write-Host "  Not started" -ForegroundColor Gray }
}
```

---

## Success Criteria

### Per Epic
- ✅ Pre-push validation passes (all 13 checks)
- ✅ CYC ≤ 8 for extracted methods
- ✅ Zero lock statements
- ✅ ASCII-only compliance
- ✅ F5 verification (if needed)

### Overall
- ✅ All 173 epics complete
- ✅ Zero P0 blockers
- ✅ All worktrees merged to main
- ✅ `deploy-sync.ps1` successful
- ✅ Final F5 verification passed

---

## Spawn 4 Workers Now

Use the `new_task` tool to spawn 4 independent subtask agents with the instructions above.

**Expected Behavior**:
- Workers execute autonomously in YOLO mode
- Workers auto-approve standard epics
- Workers pull next epic from roadmap when complete
- Workers report blockers to orchestrator
- Orchestrator monitors progress and resolves conflicts

**Estimated Time**: ~40 hours (10 hours/worker/day × 4 workers)

**Ready to spawn workers!** 🚀
```

---

## Post-Launch Monitoring

### Check Worker Status (Every 30 minutes)
```powershell
# Quick status check
@("33","35","36","37") | ForEach-Object {
    $epic = "EPIC-CCN-$_"
    $cluster = if($_ -eq "33"){"1"}elseif($_ -eq "35"){"2"}elseif($_ -eq "36"){"3"}else{"4"}
    $path = "C:/WSGTA/universal-or-epic-cluster-$cluster/docs/brain/$epic/manifest.json"
    Write-Host "`nWorker $cluster ($epic):" -ForegroundColor Yellow
    if (Test-Path $path) { Get-Content $path | Select-String "status" } else { Write-Host "  Not started" -ForegroundColor Gray }
}
```

### Detailed Progress Report
```powershell
# Get detailed progress
python scripts/epic_manifest.py status --all
```

---

## Troubleshooting

### If Worker Gets Stuck
```powershell
# Check worker status
cd C:/WSGTA/universal-or-epic-cluster-X
cat docs/brain/EPIC-CCN-XX/manifest.json

# Force unlock epic (if needed)
python scripts/validate_epic.py --epic EPIC-CCN-XX --unlock
```

### If Epic Validation Fails
```powershell
# List available epics
python scripts/validate_epic.py --list-available

# Check epic details
python scripts/validate_epic.py --epic EPIC-CCN-XX --info
```

---

## Summary

**Pre-Launch**:
1. ✅ Run cleanup script (Step 1)
2. ✅ Paste orchestrator prompt to Bob IDE (Step 2)

**Orchestrator Will**:
- Spawn 4 independent worker agents
- Each worker executes autonomously in YOLO mode
- Workers auto-approve standard epics
- Workers pull next epic when complete

**You Monitor**:
- Check status every 30 minutes
- Resolve blockers if reported
- Track progress toward 173 epics

**Ready for fully autonomous 4-worker execution!** 🚀