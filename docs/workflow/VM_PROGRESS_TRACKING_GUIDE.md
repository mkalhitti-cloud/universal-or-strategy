# VM Progress Tracking Guide

**Simple, effective progress tracking for Wave 2 execution - no Docker required!**

## 🎯 What You Get

1. **Live Obsidian Dashboard** - Auto-updates every 5 minutes
2. **Kanban Board** - Visual progress tracking (Pending → In Progress → Complete)
3. **Phase Breakdown** - See exactly which phase each epic is in
4. **Resource Metrics** - BobCoins used, time elapsed, estimated completion
5. **VM Status** - Know if VM is running or stopped

## 📋 Setup (One-Time)

### 1. Open Obsidian Dashboard

In Obsidian, navigate to:
```
docs/brain/WAVE_2_PROGRESS_DASHBOARD.md
```

**Tip**: Pin this file in Obsidian so it's always visible!

### 2. Start Monitoring Script (On Your Laptop)

Open a terminal and run:
```bash
cd c:/WSGTA/universal-or-strategy
python scripts/monitor_vm_progress.py
```

**What it does**:
- Polls the VM every 5 minutes via SSH
- Updates the Obsidian dashboard automatically
- Shows real-time progress in terminal
- Runs until you press Ctrl+C

### 3. Start VM Execution

In another terminal:
```bash
# Start the VM
gcloud compute instances start v12-epic-executor --project=project-14c86305-3cba-493f-a73 --zone=us-central1-a

# SSH into VM
gcloud compute ssh v12-epic-executor --project=project-14c86305-3cba-493f-a73 --zone=us-central1-a

# Start execution (provide your 10 API keys)
cd universal-or-strategy
python3 scripts/autonomous_executor.py \
  --api-keys key1,key2,key3,key4,key5,key6,key7,key8,key9,key10 \
  --workers 8

# Detach from tmux: Ctrl+B, then D
# Exit SSH: exit
```

## 📊 Dashboard Features

### Kanban Board
```
📋 Pending (8)
- EPIC-CCN-164 (CYC 36→8)
- EPIC-CCN-107 (CYC 31→8)
...

🔄 In Progress (2)
- EPIC-CCN-32 (CYC 23→8) - Phase 5
- EPIC-CCN-110 (CYC 19→8) - Phase 5

✅ Complete (0)
```

### Phase Breakdown Table
Shows exactly which phase each epic is in:
- ⏳ = Pending
- 🔄 = In Progress
- ✅ = Complete
- ❌ = Failed

### Resource Metrics
- **BobCoins Used**: 450 / 1,600 (28%)
- **Time Elapsed**: 2h 15m
- **Estimated Completion**: 2026-06-12 03:30
- **VM Status**: RUNNING

## 🔄 Usage Patterns

### Pattern 1: Start Work, Monitor, Sleep
```bash
# Morning: Start VM and execution
gcloud compute instances start v12-epic-executor ...
# (SSH in, start autonomous_executor.py)

# Start monitoring on laptop
python scripts/monitor_vm_progress.py

# Go to sleep - monitoring runs in background
# Obsidian dashboard updates every 5 minutes
```

### Pattern 2: Check Progress Anytime
```bash
# Single update (no continuous monitoring)
python scripts/monitor_vm_progress.py --once

# Then check Obsidian dashboard
```

### Pattern 3: Stop Everything
```bash
# Stop monitoring: Ctrl+C in terminal

# Stop VM (saves money while you sleep)
gcloud compute instances stop v12-epic-executor --project=project-14c86305-3cba-493f-a73 --zone=us-central1-a
```

## 🎨 Obsidian Integration

### Auto-Refresh
Obsidian automatically detects file changes and refreshes the view. You'll see updates every 5 minutes without doing anything!

### Kanban Plugin (Optional)
If you have the Obsidian Kanban plugin installed, the dashboard uses native Kanban syntax. If not, it still displays as readable markdown.

### Graph View
The dashboard is in `docs/brain/` so it appears in your Obsidian graph alongside other project documentation.

## 🚫 What You DON'T Need

- ❌ Docker (Routa requires Docker - we're not using it)
- ❌ Linear API setup (too complex for this use case)
- ❌ Manual SSH checks (monitoring script does it automatically)
- ❌ Separate tracking tool (everything in Obsidian)

## 🔧 Troubleshooting

### Dashboard Not Updating?
1. Check monitoring script is running: `ps aux | grep monitor_vm_progress`
2. Check VM is running: `gcloud compute instances list`
3. Run manual update: `python scripts/monitor_vm_progress.py --once`

### SSH Timeout?
The monitoring script has a 60-second timeout. If SSH is slow:
1. Check your internet connection
2. Verify VM is running
3. Try manual SSH: `gcloud compute ssh v12-epic-executor ...`

### Wrong Data?
The script reads `manifest.json` files from the VM. If data looks wrong:
1. SSH into VM
2. Check: `cat universal-or-strategy/docs/brain/EPIC-CCN-*/manifest.json`
3. Verify autonomous_executor.py is running: `ps aux | grep autonomous`

## 📈 What to Watch

### Good Signs ✅
- Epics moving from Pending → In Progress → Complete
- BobCoins increasing steadily (not too fast)
- Estimated completion time stabilizing
- No failed phases (❌)

### Warning Signs ⚠️
- Epic stuck in same phase for >30 minutes
- BobCoins depleting faster than expected
- VM status shows STOPPED (when it should be running)
- Multiple failed phases

### Action Required 🚨
- BobCoins >90% used → Add more API keys
- Multiple failures → SSH in and check logs
- VM stopped unexpectedly → Check GCP console

## 💡 Pro Tips

1. **Pin the dashboard** in Obsidian for always-visible progress
2. **Run monitoring in tmux** on your laptop so it survives terminal closes
3. **Check dashboard before bed** to see overnight progress estimate
4. **Use `--once` mode** for quick checks without starting continuous monitoring
5. **Keep terminal visible** - monitoring script shows real-time updates

## 🎯 Success Criteria

You'll know Wave 2 is complete when:
- ✅ All 10 epics show "Complete" status
- ✅ All phases show ✅ (green checkmarks)
- ✅ Dashboard shows "10 / 10 (100%)"
- ✅ VM can be stopped (work is done)

---

**Questions?** Check the monitoring script output - it shows detailed status every 5 minutes.