# Linear CLI Authentication Fix

## The Problem
The `@linear/cli` package doesn't use environment variables for authentication. It needs to authenticate via its own config file.

## Solution: Use Python Script Instead

The Python script (`scripts/linear_sync.py`) works better because it uses the Linear API directly with your API key from `.env`.

### Step 1: Install Python Dependencies
```powershell
pip install requests python-dotenv
```

### Step 2: Your .env File is Already Set Up
✅ Already done - `.env` contains your API key

### Step 3: Test the Python Script
```powershell
python scripts/linear_sync.py --dry-run
```

## Alternative: Manual Linear Web UI

Since the CLI authentication is complex, the **easiest approach** is to use Linear's web interface:

### Create EPIC-7-QUALITY Issues Manually

1. **Go to Linear**: https://linear.app
2. **Create Epic**: 
   - Title: "EPIC-7-QUALITY: Security & Debt Consolidation"
   - Description: "Consolidate deferred technical debt from bot findings"

3. **Create 5 Tickets** (copy from `docs/brain/EPIC-7-QUALITY/*.md`):

   **TICKET-001** (P0 CRITICAL):
   - Title: "Remove 36 Hardcoded Secrets"
   - Priority: Urgent
   - Labels: security, P0
   - Estimate: 8-12 hours

   **TICKET-002** (P1 HIGH):
   - Title: "Complete Circuit Breaker Rollback Logic (12 instances)"
   - Priority: High
   - Labels: bug, P1, error-prone
   - Estimate: 4-6 hours

   **TICKET-003** (P2 MEDIUM):
   - Title: "Add Missing Test Coverage"
   - Priority: Medium
   - Labels: testing, P2
   - Estimate: 16-24 hours

   **TICKET-005** (P2 MEDIUM):
   - Title: "Clean Up Build Artifacts"
   - Priority: Medium
   - Labels: maintenance, P2
   - Estimate: 1 hour

   **TICKET-004** (P3 LOW):
   - Title: "Fix StyleCop Violations"
   - Priority: Low
   - Labels: style, P3
   - Estimate: 1-2 hours

## Recommended Workflow

**For V12 Project, skip the CLI and use**:
1. **Linear Web UI** for creating/managing issues (fastest)
2. **Python script** for bulk syncing roadmap → Linear (when needed)
3. **Local markdown files** as source of truth (`docs/brain/EPIC-7-QUALITY/*.md`)

## Why This Approach is Better

✅ **No authentication hassles** - Web UI is already logged in
✅ **Visual interface** - Easier to organize and prioritize
✅ **Drag & drop** - Reorder tasks, change status
✅ **Team collaboration** - Assign to both accounts easily
✅ **Markdown files** - Still the source of truth in repo

## Quick Start (Recommended)

1. Open https://linear.app in your browser
2. Create the epic and 5 tickets manually (10 minutes)
3. Keep `docs/brain/EPIC-7-QUALITY/*.md` as detailed specs
4. Update Linear status as you complete tickets

**That's it!** No CLI authentication needed.