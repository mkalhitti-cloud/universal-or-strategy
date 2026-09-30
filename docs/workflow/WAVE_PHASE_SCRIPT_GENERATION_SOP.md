# Wave Phase Script Generation SOP

**Version**: 1.0  
**Date**: 2026-06-13  
**Status**: MANDATORY - Violation = P0 Blocker

---

## Critical Rule: Copy Working Phase, Don't Start From Scratch

**NEVER generate phase scripts from scratch. ALWAYS copy the previous working phase and modify only what's necessary.**

### Why This Matters

Wave 2 Phase 1 failed 3 times due to script generation bugs:
1. Used `jq` extraction instead of hardcoded API keys
2. Used wrong JSON field `.key` instead of `.apikey`
3. Used wrong launcher pattern `bash -c` instead of `bash -l`

**Root Cause**: Generator script created Phase 1 from scratch instead of copying Phase 0's proven pattern.

**Cost**: 3 hours debugging, 3 failed launches, wasted bobcoins.

---

## The SOP (Standard Operating Procedure)

### Step 1: Copy Previous Working Phase

```bash
# Example: Creating Phase 2 scripts from Phase 1
cp _p1_107.sh _p2_107.sh
cp _p1_108.sh _p2_108.sh
# ... copy all 9 scripts

cp launch_phase1_all_screen.sh launch_phase2_all_screen.sh
```

### Step 2: Identify What Needs to Change

**Only these elements change between phases:**

| Element | Phase 0 | Phase 1 | Phase 2 | Phase 3 |
|---------|---------|---------|---------|---------|
| **Script name** | `_p0_*.sh` | `_p1_*.sh` | `_p2_*.sh` | `_p3_*.sh` |
| **Log directory** | `logs/phase0/` | `logs/phase1/` | `logs/phase2/` | `logs/phase3/` |
| **Message file** | `/tmp/phase0_msg_*.txt` | `/tmp/phase1_msg_*.txt` | `/tmp/phase2_msg_*.txt` | `/tmp/phase3_msg_*.txt` |
| **Output file** | `00-hotspots.md` | `00-scope.md` | `02-architecture-plan.md` | `03-audit-report.md` |
| **Manifest phase** | `"0"` | `"1"` | `"2"` | `"3"` |
| **Task description** | Hotspot Analysis | Scope Definition | Architecture Planning | DNA & PR Audit |
| **Chat mode** | `v12-phase0-hotspot` | `plan` | `plan` | `advanced` |

**Everything else stays IDENTICAL:**
- ✅ API key loading (hardcoded)
- ✅ Directory structure
- ✅ Bob Shell invocation pattern
- ✅ Logging pattern
- ✅ Error handling

### Step 3: Use Find-and-Replace (Not Rewrite)

```bash
# Example: Converting Phase 1 to Phase 2
sed -i 's/phase1/phase2/g' _p2_*.sh
sed -i 's/Phase 1/Phase 2/g' _p2_*.sh
sed -i 's/Scope Definition/Architecture Planning/g' _p2_*.sh
sed -i 's/00-scope.md/02-architecture-plan.md/g' _p2_*.sh
sed -i 's/"1"/"2"/g' _p2_*.sh
```

### Step 4: Update Only Task-Specific Content

The message file content (between `cat > /tmp/phase*_msg_*.txt << 'EOFMSG'` and `EOFMSG`) is the ONLY place where task-specific instructions go.

**DO NOT CHANGE**:
- Shebang line
- `set -e`
- `cd` command
- `export BOBSHELL_API_KEY=` line
- `mkdir -p` commands
- Bob Shell invocation pattern
- Logging pattern
- `echo "DONE_EXIT=$?"` line

### Step 5: Verify Against Working Phase

Before deploying, compare new phase against working phase:

```bash
# Check structure matches
diff -u <(grep -v 'phase[0-9]' _p1_107.sh) <(grep -v 'phase[0-9]' _p2_107.sh)

# Should show ONLY phase number differences
```

---

## The Proven Pattern (Phase 0 Baseline)

This is the GOLD STANDARD that all phases must match:

```bash
#!/bin/bash
set -e
cd /home/malhitticrypto/universal-or-strategy
export BOBSHELL_API_KEY='bob_prod_bob-admin_...'  # HARDCODED
mkdir -p docs/brain/EPIC-CCN-107
mkdir -p logs/phase0

cat > /tmp/phase0_msg_107.txt << 'EOFMSG'
[TASK-SPECIFIC INSTRUCTIONS HERE]

**MANDATORY REPORTING**:
After completing all tasks, you MUST report:
1. Bobcoins used this session: [X.XX]
2. Remaining balance in API key: [Y.YY]

Format: "Cost: X.XX | Balance: Y.YY"
EOFMSG

bob --yolo --chat-mode v12-phase0-hotspot "$(cat /tmp/phase0_msg_107.txt)" 2>&1 | tee logs/phase0/EPIC-CCN-107.log
echo "DONE_EXIT=$?"

# Made with Bob
```

**Launcher Pattern**:
```bash
screen -dmS "$session_name" bash -l "$script_name"
```

---

## What NOT to Do

### ❌ DON'T: Generate from scratch
```python
# BAD: Creating new template
SCRIPT_TEMPLATE = """#!/bin/bash
# New script structure...
"""
```

### ❌ DON'T: Use jq extraction
```bash
# BAD: Dynamic API key loading
export BOBSHELL_API_KEY=$(jq -r '.apikey' "$HOME/.bob/api-keys/b.json")
```

### ❌ DON'T: Add unnecessary wrappers
```bash
# BAD: Extra bash wrapper
bash -l -c "bob --yolo --chat-mode plan \"$(cat /tmp/msg.txt)\""
```

### ❌ DON'T: Change launcher pattern
```bash
# BAD: Non-login shell
screen -dmS "$session_name" bash -c "bash $script_name; exec bash"
```

---

## What TO Do

### ✅ DO: Copy working phase
```bash
cp _p1_107.sh _p2_107.sh
```

### ✅ DO: Use hardcoded API keys
```bash
export BOBSHELL_API_KEY='bob_prod_bob-admin_V7HJU1JX...'
```

### ✅ DO: Use direct invocation
```bash
bob --yolo --chat-mode plan "$(cat /tmp/phase2_msg_107.txt)" 2>&1 | tee logs/phase2/EPIC-CCN-107.log
```

### ✅ DO: Use login shell in launcher
```bash
screen -dmS "$session_name" bash -l "$script_name"
```

---

## Generator Script Pattern

If you MUST use a generator script (not recommended), it should:

1. **Load API keys from JSON files** (don't use jq in generated scripts)
2. **Use correct field name** (`.apikey` not `.key`)
3. **Hardcode keys into scripts** (no dynamic extraction)
4. **Match Phase 0 pattern exactly** (copy structure, change only phase-specific content)

**Example**:
```python
def load_api_key(api_file):
    """Load API key from JSON file."""
    json_path = os.path.join("docs", "API", api_file)
    with open(json_path, 'r') as f:
        data = json.load(f)
        return data['apikey']  # Correct field name

# Then hardcode into script
script_content = TEMPLATE.format(api_key=load_api_key(api_file))
```

---

## Validation Checklist

Before deploying ANY new phase scripts:

- [ ] Copied from previous working phase (not generated from scratch)
- [ ] API keys are hardcoded (no jq extraction)
- [ ] Using correct field `.apikey` (not `.key`)
- [ ] Bob Shell invocation matches Phase 0 pattern
- [ ] Launcher uses `bash -l` (not `bash -c`)
- [ ] Only phase-specific content changed (task description, file names, phase number)
- [ ] **Bobcoin reporting included in message** (Cost + Balance)
- [ ] **Scripts made executable after upload** (chmod +x)
- [ ] **Log directory verified after launch** (ls logs/phase*/)
- [ ] **Bobcoin tracking verified in logs** (grep 'Cost:.*Balance:')
- [ ] Tested one script locally before deploying all 9
- [ ] Compared against working phase with diff

---

## Emergency Recovery

If scripts fail after deployment:

1. **Check logs for error pattern**:
   ```bash
   gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="tail -30 /home/malhitticrypto/universal-or-strategy/logs/phase*/EPIC-CCN-107.log"
   ```

2. **Compare against working phase**:
   ```bash
   diff _p0_107.sh _p1_107.sh
   ```

3. **Fix locally, redeploy**:
   ```bash
   # Fix the issue
   gcloud compute scp _p1_*.sh v12-test-golden-v2:/home/malhitticrypto/universal-or-strategy/ --zone=us-central1-a
   
   # Kill and relaunch
   gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="killall screen; sleep 2; cd /home/malhitticrypto/universal-or-strategy && bash launch_phase1_all_screen.sh"
   ```

---

## SSH Terminal Setup for Live Monitoring

**Purpose**: Connect VSCode directly to VM for real-time monitoring and debugging

### When to Use
- Debugging phase failures
- Watching phase execution in real-time
- Editing scripts directly on VM
- Monitoring screen sessions interactively

### Setup Steps

**1. Get VM External IP**:
```bash
gcloud compute instances describe v12-test-golden-v2 \
  --zone=us-central1-a \
  --format="get(networkInterfaces[0].accessConfigs[0].natIP)"
```

**2. Create SSH Config**:

Create/edit `C:\Users\Mohammed Khalid\.ssh\config`:
```
Host v12-vm
    HostName <EXTERNAL_IP_FROM_STEP_1>
    User malhitticrypto
    IdentityFile ~/.ssh/google_compute_engine
    StrictHostKeyChecking no
    UserKnownHostsFile /dev/null
```

**3. Test Connection**:
```bash
ssh v12-vm
```

**4. Connect VSCode**:
- Open VSCode
- Press `Ctrl+Shift+P`
- Type "Remote-SSH: Connect to Host"
- Select "v12-vm"
- Wait for connection (first time: 30-60 seconds)

### Using Remote Terminal

**Once connected**:
```bash
# Navigate to project
cd ~/universal-or-strategy

# List screen sessions
screen -ls

# Attach to specific session
screen -r p3-107

# Detach from session (keep it running)
# Press: Ctrl+A, then D

# View logs in real-time
tail -f logs/phase3/EPIC-CCN-107.log

# Check file creation
ls -lh docs/brain/EPIC-CCN-107/

# Monitor all phases at once
watch -n 5 'screen -ls; echo "---"; ls docs/brain/EPIC-CCN-*/0*.md 2>/dev/null | wc -l'
```

### Common Issues

**Issue**: "Could not establish connection"
- **Cause**: Wrong hostname or IP changed
- **Fix**: Re-run Step 1, update SSH config with new IP

**Issue**: "Permission denied (publickey)"
- **Cause**: SSH keys not configured
- **Fix**: Run `gcloud compute config-ssh` to regenerate keys

**Issue**: "screen: command not found" (locally)
- **Cause**: Running screen on local machine instead of VM
- **Fix**: SSH to VM first, then run screen commands

### Tips

- **Live Monitoring**: Keep VSCode connected during phase execution
- **Multiple Terminals**: Open multiple terminals to monitor different logs
- **File Editing**: Edit scripts directly on VM (no scp needed)
- **Screen Sessions**: Use `screen -ls` to see all running phases
- **Detach Safely**: Always use `Ctrl+A, D` (don't close terminal)

---

## Success Criteria

A phase script generation is successful when:

1. ✅ All 9 scripts execute without "command not found" errors
2. ✅ All 9 scripts authenticate successfully (no HTTP 401)
3. ✅ All 9 scripts create expected output files
4. ✅ Logs show Bob Shell executing tasks (not just errors)
5. ✅ **Logs contain bobcoin usage AND balance** (Cost: X.XX | Balance: Y.YY)
6. ✅ Pattern matches previous working phase (verified with diff)

---

## References

- **Phase 0 Success**: `WAVE2_PHASE0_COMPLETION_REPORT.md` (8/9 epics, 89% success)
- **Phase 1 Failures**: 3 failed launches due to script generation bugs
- **Phase 1 Success**: After copying Phase 0 pattern exactly
- **Skill Documentation**: `.bob/skills/gcp-vm-wave-execution/skill.md`

---

## Version History

- **V1.0** (2026-06-13): Initial SOP after Phase 1 debugging session
  - Documented "copy working phase" mandate
  - Captured Phase 0 proven pattern as baseline
  - Added validation checklist and emergency recovery

---

**REMEMBER**: The working pattern is sacred. Copy it, don't recreate it.