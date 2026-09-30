# Manual Steps: Kill Frozen VM Processes

**Date**: 2026-06-18  
**Purpose**: Clear frozen Bob processes from VM to unblock Wave 6  
**Estimated Time**: 5 minutes

## Prerequisites

- ✅ VM is running (136.114.155.172)
- ✅ You have SSH access configured
- ✅ You're on Windows with PowerShell

## Step-by-Step Instructions

### Step 1: Open PowerShell Terminal

**Option A: From VSCode (Recommended)**
1. In VSCode, press `` Ctrl+` `` (backtick key, usually below Escape)
2. This opens the integrated terminal at the bottom
3. Make sure it says "powershell" in the dropdown (not "cmd" or "bash")

**Option B: From Windows Start Menu**
1. Press `Windows Key`
2. Type "PowerShell"
3. Click "Windows PowerShell" (NOT "PowerShell ISE")

### Step 2: Connect to VM

In the PowerShell terminal, type:

```powershell
ssh malhitticrypto@136.114.155.172
```

**What to expect**:
- If this is your first time: You'll see a message about authenticity, type `yes` and press Enter
- You'll be prompted for a password (if using password auth)
- OR it will connect automatically (if using SSH keys)
- **If it hangs**: Wait 30 seconds, then press `Ctrl+C` and try again

**Success looks like**:
```
malhitticrypto@v12-test-golden-v2:~$
```

### Step 3: Check for Frozen Processes

Once connected to the VM, type:

```bash
ps aux | grep -E '(bob|phase|epic)' | grep -v grep
```

**What to expect**:
- You'll see a list of running processes
- Look for lines containing "bob" or "phase" or "epic"
- If you see many lines, processes are frozen

**Example output**:
```
malhittic 12345  0.0  0.1 123456 7890 ?  S  10:30  0:00 bob --yolo --chat-mode v12-phase1-5-boundary
malhittic 12346  0.0  0.1 123456 7890 ?  S  10:31  0:00 /bin/bash _p1_5_epic_001.sh
```

### Step 4: Kill All Frozen Processes

Type these commands **one at a time**, pressing Enter after each:

```bash
pkill -9 -f bob
```

Press Enter, wait 2 seconds, then:

```bash
pkill -9 -f phase
```

Press Enter, wait 2 seconds, then:

```bash
pkill -9 -f epic
```

**What to expect**:
- No output is normal (silence = success)
- Processes are killed immediately

### Step 5: Verify Clean State

Type:

```bash
ps aux | grep -E '(bob|phase|epic)' | grep -v grep
```

**What to expect**:
- **Success**: No output (empty, no lines)
- **Still frozen**: You see process lines - repeat Step 4

### Step 6: Check Wave Logs

Now that processes are killed, check which wave was running:

```bash
ls -lh ~/wave*/phase*.log 2>/dev/null | head -10
```

**What to expect**:
- List of log files with dates
- Look for "wave6" or "wave7" in the paths
- Note the most recent dates

**Example output**:
```
-rw-r--r-- 1 malhittic malhittic 1.2M Jun 18 10:30 /home/malhittic/wave6_phase0.log
-rw-r--r-- 1 malhittic malhittic 856K Jun 18 10:45 /home/malhittic/wave6_phase1.log
```

### Step 7: Exit VM

Type:

```bash
exit
```

You're back in your local PowerShell.

## What to Report Back

After completing these steps, tell me:

1. **Did Step 5 show clean state?** (Yes/No)
2. **What wave logs did you see?** (wave6, wave7, or other)
3. **What were the most recent log dates?** (e.g., "Jun 18 10:30")

## If Something Goes Wrong

### SSH Connection Hangs (Step 2)
- Wait 30 seconds
- Press `Ctrl+C` to cancel
- Try again
- If still hangs: VM might be overloaded, wait 5 minutes and retry

### "Permission Denied" Error
- You need SSH access configured
- Check with: `ssh -v malhitticrypto@136.114.155.172`
- Look for authentication errors

### "Command Not Found" Error
- You're in the wrong shell
- Make sure you're connected to the VM (Step 2 succeeded)
- The prompt should show `malhitticrypto@v12-test-golden-v2:~$`

### Processes Won't Die (Step 5 Still Shows Processes)
- Try one more time: `pkill -9 -f bob; pkill -9 -f phase; pkill -9 -f epic`
- If still there, they might be zombie processes (harmless)
- Report back and we'll handle differently

## Next Steps After Completion

Once you report back the wave logs info, I'll:
1. Determine if Wave 6 can be recovered
2. OR recommend starting Wave 8 fresh
3. Generate the appropriate scripts

---

**Ready?** Start with Step 1 and work through each step carefully.