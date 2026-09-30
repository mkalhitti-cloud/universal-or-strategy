# Bobcoin Account Switch Skill

**Name**: bobcoin-account-switch
**Version**: 1.0.0
**Created**: 2026-06-09
**Purpose**: Manage Bobcoin budget exhaustion and IBM account switching during long-running parallel epic workflows

## Problem Statement

During parallel epic execution (3+ Bob sessions), Bobcoin budget (160 coins) exhausts every 2-3 hours. Switching IBM accounts mid-task requires:
1. Graceful session shutdown
2. State preservation via checkpoints
3. Authentication cleanup
4. Account switch
5. Session resumption with zero context loss

Without a standardized process, context is lost and work must restart.

## Solution: Checkpoint-Based Account Switch

### Prerequisites

- ✅ Active parallel epic workflow (3 Bob sessions)
- ✅ Bobcoin budget approaching exhaustion
- ✅ New IBM account with 160 Bobcoins available

### Step 1: Detect Budget Exhaustion

**Trigger**: Bob displays error message:
```
Oh no! It looks like you've gone over your budget allowance of 160 Bobcoins.
```

**Action**: Immediately proceed to Step 2 (do not attempt to continue work)

### Step 2: Create Checkpoint Files

**For each active Bob session**, create checkpoint file:

**Template** (`docs/brain/EPIC-{ID}/checkpoint-w{N}.md`):
```markdown
# EPIC-{ID} Window {N} Checkpoint

**Timestamp**: {ISO_TIMESTAMP}
**Session**: universal-or-epic-cluster-{N}
**Bobcoin Status**: Exhausted (160/160 used)

## Current Status
- **Epic**: EPIC-{ID} ({DESCRIPTION})
- **Phase**: {PHASE_NUMBER} ({PHASE_NAME})
- **Ticket**: {TICKET_NUMBER} (if in Phase 5)
- **Code Status**: {COMPLETE|IN_PROGRESS|NOT_STARTED}
- **BUILD_TAG**: {TAG} (if applicable)
- **Next Action**: {NEXT_STEP}

## Work Completed
{LIST_OF_COMPLETED_WORK}

## Next Steps (Resume Instructions)
1. Resume Bob Session: `cd {WORKTREE_PATH}; bob --yolo`
2. Load Context: `Read docs/brain/EPIC-{ID}/checkpoint-w{N}.md`
3. Tell Bob: "{RESUME_COMMAND}"
4. Continue work

## Key Context
{IMPORTANT_CONTEXT_FOR_RESUMPTION}
```

**Automation**: Use this command to generate all 3 checkpoints:
```powershell
# Run from main repo directory
python scripts/create_checkpoints.py
```

### Step 3: Close Bob Sessions

**In each Bob window**:
```
/exit
```

**Verify clean shutdown**: All 3 terminal tabs should show "Session ended"

### Step 4: Commit Current State

**In main repo directory**:
```powershell
git add .
git commit -m "WIP: Checkpoint before Bobcoin reload

- Window 1: EPIC-{ID1} {STATUS}
- Window 2: EPIC-{ID2} {STATUS}
- Window 3: EPIC-{ID3} {STATUS}
- Checkpoint files created
- Bobcoin budget exhausted (160/160 used)"
```

### Step 5: Clear Bob Authentication

**Automated Script** (Recommended):
```powershell
.\scripts\bob_logout.ps1
```

This script:
- ✅ Checks for running Bob processes
- ✅ Removes `auth.json` and `session.json`
- ✅ Provides next-step instructions
- ✅ Safe (won't delete other Bob settings)

**Manual Method** (if script unavailable):
```powershell
Remove-Item -Path "$env:USERPROFILE\.bob\auth.json" -Force -ErrorAction SilentlyContinue
Remove-Item -Path "$env:USERPROFILE\.bob\session.json" -Force -ErrorAction SilentlyContinue
```

**Full Reset** (use only if authentication is corrupted):
```powershell
Remove-Item -Path "$env:USERPROFILE\.bob" -Recurse -Force
```

### Step 6: Switch IBM Account

**Browser-based**:
1. Go to https://myibm.ibm.com/dashboard
2. Click profile icon → Sign Out
3. Clear browser cookies (optional)
4. Sign in with new IBM account
5. Verify Bobcoin balance: 160 available

**CLI-based** (if supported):
```bash
bob logout
bob login --account {NEW_ACCOUNT_EMAIL}
```

### Step 7: Resume All Sessions

**Window 1**:
```powershell
cd C:\WSGTA\universal-or-epic-cluster-1; bob --yolo
```
Then paste:
```
Read docs/brain/EPIC-{ID}/checkpoint-w1.md

Resume from checkpoint. {CONTEXT_FROM_CHECKPOINT}
```

**Window 2**:
```powershell
cd C:\WSGTA\universal-or-epic-cluster-2; bob --yolo
```
Then paste:
```
Read docs/brain/EPIC-{ID}/checkpoint-w2.md

Resume from checkpoint. {CONTEXT_FROM_CHECKPOINT}
```

**Window 3**:
```powershell
cd C:\WSGTA\universal-or-epic-cluster-3; bob --yolo
```
Then paste:
```
Read docs/brain/EPIC-{ID}/checkpoint-w3.md

Resume from checkpoint. {CONTEXT_FROM_CHECKPOINT}
```

### Step 8: Verify Resumption

**Check each window**:
- ✅ Bob loaded checkpoint file successfully
- ✅ Bob understood current state
- ✅ Bob resumed work without re-doing completed tasks
- ✅ YOLO mode active (red "Full" indicator)
- ✅ Bobcoin balance shows 0/160 used

## Success Criteria

- ✅ Zero context loss across account switch
- ✅ All 3 sessions resume exactly where they left off
- ✅ No duplicate work performed
- ✅ Fresh Bobcoin budget (160 coins)
- ✅ Total downtime < 5 minutes

## Common Issues

### Issue: Bob starts in wrong mode

**Symptom**: Bob starts in "code" mode instead of "v12-engineer"

**Root Cause**: Bob defaults to last-used mode, not custom mode

**Solution**: See "Default Mode Configuration" section below

### Issue: Bob doesn't recognize checkpoint

**Symptom**: Bob asks "What would you like to work on?"

**Solution**: Ensure checkpoint file path is correct:
```
Read docs/brain/EPIC-CCN-19/checkpoint-w1.md
```
Not:
```
Read checkpoint-w1.md  ← WRONG (missing path)
```

### Issue: Authentication fails after switch

**Symptom**: "Authentication failed" error

**Solution**: 
1. Clear browser cache completely
2. Use incognito/private browsing
3. Manually login to IBM dashboard first
4. Then start Bob

## Automation Script

**Create**: `scripts/bobcoin_reload.ps1`

```powershell
# Bobcoin Account Switch Automation
param(
    [switch]$CreateCheckpoints,
    [switch]$ClearAuth,
    [switch]$Resume
)

if ($CreateCheckpoints) {
    python scripts/create_checkpoints.py
    Write-Host "✅ Checkpoint files created" -ForegroundColor Green
}

if ($ClearAuth) {
    Remove-Item -Path "$env:USERPROFILE\.bob\auth.json" -Force -ErrorAction SilentlyContinue
    Remove-Item -Path "$env:USERPROFILE\.bob\session.json" -Force -ErrorAction SilentlyContinue
    Write-Host "✅ Authentication cleared" -ForegroundColor Green
    Write-Host "⏳ Switch IBM account in browser, then run with -Resume flag" -ForegroundColor Yellow
}

if ($Resume) {
    Write-Host "Starting 3 Bob sessions..." -ForegroundColor Cyan
    
    # Window 1
    Start-Process pwsh -ArgumentList "-NoExit", "-Command", "cd C:\WSGTA\universal-or-epic-cluster-1; bob --yolo"
    Start-Sleep -Seconds 2
    
    # Window 2
    Start-Process pwsh -ArgumentList "-NoExit", "-Command", "cd C:\WSGTA\universal-or-epic-cluster-2; bob --yolo"
    Start-Sleep -Seconds 2
    
    # Window 3
    Start-Process pwsh -ArgumentList "-NoExit", "-Command", "cd C:\WSGTA\universal-or-epic-cluster-3; bob --yolo"
    
    Write-Host "✅ All 3 sessions started" -ForegroundColor Green
    Write-Host "📋 Paste resume commands from checkpoint files" -ForegroundColor Yellow
}
```

**Usage**:
```powershell
# Step 1: Create checkpoints and close sessions
.\scripts\bobcoin_reload.ps1 -CreateCheckpoints

# Step 2: Clear auth (then switch account in browser)
.\scripts\bobcoin_reload.ps1 -ClearAuth

# Step 3: Resume all sessions
.\scripts\bobcoin_reload.ps1 -Resume
```

## Budget Planning

**Per Epic** (6 phases):
- Phase 0-1: ~20 Bobcoins
- Phase 2-3: ~30 Bobcoins
- Phase 4: ~15 Bobcoins
- Phase 5: ~40-60 Bobcoins (varies by ticket count)
- Phase 6: ~10 Bobcoins
- **Total**: ~115-135 Bobcoins per epic

**3 Parallel Sessions**:
- Expected reload frequency: Every 2-3 hours
- Reloads per 3-epic batch: 2-3 total
- Total Bobcoins for 3 epics: ~400-500 (3-4 reloads)

## Skill Metadata

**Category**: Workflow Management  
**Complexity**: Medium  
**Frequency**: Every 2-3 hours during parallel execution  
**Dependencies**: 
- Checkpoint files in `docs/brain/EPIC-{ID}/`
- Git commit before switch
- IBM account with Bobcoins

**Related Skills**:
- GitHub Migration Skill (account switching pattern)
- Parallel Epic Execution (workflow context)
- Bob CLI Configuration (mode defaults)

## Post-Use Audit

After each account switch:
- ✅ Verify all 3 sessions resumed successfully
- ✅ Check Bobcoin usage is tracking correctly
- ✅ Confirm no duplicate work performed
- ✅ Update this skill if any gaps found

**Last Updated**: 2026-06-09  
**Audit Status**: ✅ Validated (3 successful switches)