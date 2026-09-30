# Linear Setup Guide - Mohammed's Configuration

## Your Setup
- **Primary Linear Account**: mkalhitti@gmail.com (main development)
- **Secondary Account**: malhitticrypto@gmail.com (GitHub repo owner)
- **Goal**: Track V12 roadmap in Linear, assign tasks to both accounts

---

## Step-by-Step Setup

### Step 1: Get Your Linear API Key

1. **Log in to Linear** with mkalhitti@gmail.com
   - Go to: https://linear.app

2. **Navigate to API Settings**
   - Click your profile (bottom left)
   - Settings → API
   - Or direct link: https://linear.app/settings/api

3. **Create New API Key**
   - Click "Create new key"
   - Name: `V12 Roadmap Sync`
   - Scopes needed:
     - ✅ `read:issues`
     - ✅ `write:issues`
     - ✅ `read:projects`
     - ✅ `write:projects`
   - Click "Create"

4. **Copy the Key**
   - Starts with `lin_api_`
   - **IMPORTANT**: Copy it now - you won't see it again!
   - Example: `lin_api_abc123def456ghi789...`

---

### Step 2: Run Setup Script

Open PowerShell in your project directory:

```powershell
cd C:\WSGTA\universal-or-strategy

# Install dependencies (if not already installed)
pip install requests

# Run setup script
python scripts/linear_setup.py
```

**What will happen**:
```
============================================================
🚀 Linear Setup Helper - V12 Integration
============================================================

📝 Step 1: Get your Linear API Key
   1. Go to: https://linear.app/settings/api
   2. Click 'Create new key'
   3. Give it a name (e.g., 'V12 Sync')
   4. Copy the key (starts with 'lin_api_')

Paste your Linear API key here: _
```

**Paste your API key** and press Enter.

---

### Step 3: Select Your Team

The script will show your teams:
```
🔌 Step 2: Testing connection...
✅ Connected as: Mohammed Khalid (mkalhitti@gmail.com)
   User ID: user-abc123

📋 Step 3: Finding your teams...

📋 Available Teams:
   1. V12 Development (Key: V12)
      Team ID: team-xyz789
   2. Personal Projects (Key: PERS)
      Team ID: team-def456

Select team (1-2): _
```

**Select the team** where you want V12 tasks (probably "V12 Development" or similar).

---

### Step 4: Select Users to Assign

The script will show all users in your workspace:
```
👥 Step 4: Finding users in workspace...

👥 Available Users:
   1. Mohammed Khalid (mkalhitti@gmail.com) - ✓ Active
      User ID: user-abc123
   2. Mohammed Khalid (malhitticrypto@gmail.com) - ✓ Active
      User ID: user-def456

Select users to assign tasks to (comma-separated, e.g., 1,2):
(Press Enter to skip user assignment)
Your choice: _
```

**Type**: `1,2` (to assign to both accounts)

---

### Step 5: Verify .env File

The script creates `.env` file:
```
✅ Created .env file
⚠️  IMPORTANT: .env contains your API key - never commit it to git!
```

**Check the file**:
```powershell
cat .env
```

Should look like:
```bash
# Linear Integration Configuration
LINEAR_API_KEY=your_linear_api_key_here
LINEAR_TEAM_ID=your_team_id_here
LINEAR_ASSIGNEE_IDS=user_id_1,user_id_2
LINEAR_WORKSPACE_NAME="V12 Development"
```

---

### Step 6: Run Initial Sync

Now sync your roadmap to Linear:

```powershell
# Dry run first (preview only)
python scripts/linear_sync.py --dry-run

# Actual sync
python scripts/linear_sync.py
```

**Expected output**:
```
📖 Parsing roadmap: docs/brain/master_roadmap.md
Found 7 phases and 8 tasks
Current build: 1111.007-mphase-mp0

Syncing V12 Roadmap (Build: 1111.007-mphase-mp0) to Linear...
✅ Created epic: V12 Universal OR Strategy - Build 1111.007-mphase-mp0
✅ Created Phase 1: Foundation (Monolith Partition)
✅ Created Phase 2: Command Routing (IPC TCP + FSM)
✅ Created Phase 3: Strategy Patterns (RAII)
✅ Created Phase 4: Event Lifecycle Dispatcher
✅ Created Phase 5: Modularization (StickyState)
✅ Created Phase 6: Hot Path Execution Hardening
✅ Created Phase 7: Concurrency Hardening
✅ Created Task 1: Epic 1 (H05 + H08 Stop Order Sync)
✅ Created Task 2: Epic 1 (H21 + H22 Retest Rollback)
✅ Created Task 3: Epic 1 (REAPER Diagnostic)
✅ Created Task 4: Epic 2 (Visual/Command Pipeline)
✅ Created Task 5: Epic 3 (REAPER & Lifecycle)
✅ Created Task 6: Epic 4 (Signal & State)
✅ Created Task 7: PR Merge (24 repairs)
✅ Created Task 8: Live trading & system testing

🎉 Sync complete! Created 7 phases and 8 tasks
```

---

### Step 7: Check Linear

1. **Open Linear**: https://linear.app
2. **Find your project**: Look for "V12 Universal OR Strategy"
3. **Verify**:
   - ✅ All 7 phases are listed
   - ✅ All 8 tasks are listed
   - ✅ Tasks are assigned to BOTH accounts (mkalhitti + malhitticrypto)
   - ✅ Statuses match roadmap (DONE, IN PROGRESS, QUEUED)

---

## What You'll See in Linear

### Project Structure
```
📦 V12 Universal OR Strategy - Build 1111.007-mphase-mp0
│
├── 📋 Phase 1: Foundation [DONE]
├── 📋 Phase 2: Command Routing [DONE]
├── 📋 Phase 3: Strategy Patterns [DONE]
├── 📋 Phase 4: Event Lifecycle Dispatcher [DONE]
├── 📋 Phase 5: Modularization [DONE]
├── 📋 Phase 6: Hot Path Execution Hardening [DONE]
└── 📋 Phase 7: Concurrency Hardening [IN PROGRESS]
    ├── ✅ T-1: Epic 1 (Stop Order Sync) [DONE]
    ├── ✅ T-2: Epic 1 (Retest Rollback) [DONE]
    ├── ✅ T-3: Epic 1 (REAPER Diagnostic) [DONE]
    ├── 🔄 T-4: Epic 2 (Visual/Command Pipeline) [IN PROGRESS]
    ├── ⏳ T-5: Epic 3 (REAPER & Lifecycle) [QUEUED]
    ├── ⏳ T-6: Epic 4 (Signal & State) [QUEUED]
    ├── ⏳ T-7: PR Merge (24 repairs) [QUEUED]
    └── ⏳ T-8: Live trading & testing [QUEUED]
```

### Task Assignment
Every task will show:
- **Assignees**: 
  - 👤 Mohammed Khalid (mkalhitti@gmail.com)
  - 👤 Mohammed Khalid (malhitticrypto@gmail.com)

This way both accounts can track progress!

---

## Daily Workflow

### After Completing Work
```powershell
# 1. Update roadmap
vim docs/brain/master_roadmap.md

# 2. Sync to Linear
python scripts/linear_sync.py

# 3. Commit changes
git add docs/brain/master_roadmap.md
git commit -m "docs: update roadmap - completed T-4"
git push
```

### Check Progress
- **In Linear**: https://linear.app (visual board)
- **In Repo**: `docs/brain/master_roadmap.md` (source of truth)

---

## Troubleshooting

### "Authentication failed"
**Solution**: Your API key expired or is invalid
```powershell
# Get new key from https://linear.app/settings/api
# Update .env file with new key
python scripts/linear_setup.py  # Re-run setup
```

### "Team not found"
**Solution**: Wrong team ID in .env
```powershell
# Re-run setup to select correct team
python scripts/linear_setup.py
```

### "Duplicate issues created"
**Solution**: Script doesn't check for existing issues yet
- Delete duplicates in Linear UI
- Re-run sync

---

## Security Notes

### ⚠️ NEVER Commit These Files
- `.env` (contains API key)
- Any file with `lin_api_` in it

### ✅ Already Protected
Your `.gitignore` already excludes:
```
.env
*.env
.env.*
```

---

## Next Steps

1. ✅ Run `python scripts/linear_setup.py`
2. ✅ Paste your API key
3. ✅ Select team and users
4. ✅ Run `python scripts/linear_sync.py`
5. ✅ Check Linear to see your V12 project!

**Questions?** Check `docs/LINEAR_INTEGRATION.md` for full documentation.