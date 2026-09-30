# Antigravity: Check Bob Shell Configuration on VM

## Purpose

Compare Bob Shell configuration between local (working) and VM (write_to_file fails) to find differences.

## Commands to Run

### 1. Check Bob Shell Installation Location
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="bash -l -c 'which bob'"
```
Expected: `/home/malhitticrypto/.npm-global/bin/bob`

### 2. Check Bob Shell Version
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="bash -l -c 'bob --version'"
```
Expected: `1.0.4` (or similar)

### 3. List Bob Shell Configuration Directory
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="ls -la /home/malhitticrypto/.bob/"
```
Expected: Should show `api-keys/`, `custom_modes.yaml`, `settings.json`, etc.

### 4. Check Bob Shell Settings
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="cat /home/malhitticrypto/.bob/settings.json"
```
Look for: Any settings related to file operations, working directory, or tool configuration

### 5. Check Custom Modes Configuration
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="cat /home/malhitticrypto/.bob/custom_modes.yaml"
```
Look for: `v12-phase0-hotspot` mode definition, tool groups (especially `edit` group with write_to_file)

### 6. Check if Bob Shell Has Tool Restrictions
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="bash -l -c 'bob --help | grep -i tool'"
```
Look for: Any mentions of tool restrictions or configuration

### 7. Check Working Directory When Bob Runs
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="bash -l -c 'cd /home/malhitticrypto/universal-or-strategy && pwd && bob --chat-mode v12-phase0-hotspot \"What is my current working directory? Use run_shell_command with pwd to check.\"'"
```
Expected: Should show `/home/malhitticrypto/universal-or-strategy`

### 8. Check File System Permissions
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="ls -ld /home/malhitticrypto/universal-or-strategy/docs/brain/EPIC-CCN-107/"
```
Expected: `drwxrwxr-x` (writable by user)

### 9. Test Simple File Creation (Not via Bob)
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="cd /home/malhitticrypto/universal-or-strategy && echo 'test' > test_direct.txt && cat test_direct.txt && rm test_direct.txt"
```
Expected: Should show "test" (confirms filesystem works)

### 10. Compare with Local Bob Shell Config
On your local machine, run:
```bash
cat ~/.bob/settings.json
cat ~/.bob/custom_modes.yaml
```
Compare with VM output from steps 4 and 5.

## What to Look For

### Red Flags:
- ❌ Different Bob Shell versions (local vs VM)
- ❌ Missing `edit` tool group in custom mode
- ❌ Settings that restrict file operations
- ❌ Wrong working directory when Bob runs
- ❌ File permission issues in target directories

### Green Flags:
- ✅ Same Bob Shell version
- ✅ `edit` group includes write_to_file
- ✅ No tool restrictions in settings
- ✅ Correct working directory
- ✅ Writable target directories

## What to Paste Back

For each command (1-9), paste the output. For command 10, paste your local config files.

```
Command 1 (which bob):
[paste output]

Command 2 (bob --version):
[paste output]

Command 3 (ls .bob/):
[paste output]

Command 4 (settings.json):
[paste output]

Command 5 (custom_modes.yaml):
[paste output]

Command 6 (bob --help):
[paste output]

Command 7 (working directory test):
[paste output]

Command 8 (permissions):
[paste output]

Command 9 (direct file creation):
[paste output]

Command 10 (local config):
[paste local settings.json and custom_modes.yaml]
```

---

**This will identify any configuration differences that might prevent write_to_file from working on the VM.**