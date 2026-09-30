# Antigravity: VM Automation Challenge - Quote Escaping Solution Needed

## Context

We're trying to launch Wave 2 autonomous execution: 1 GCP VM running 10 parallel Bob Shell agents. We have a working golden image (`v12-bob-shell-golden-v2`) but cannot execute the orchestrator script due to quote escaping issues.

## The Problem

**Quote escaping fails across all approaches** when trying to pass complex commands through multiple layers:

### Failed Approach 1: Windows PowerShell → gcloud → SSH
```powershell
gcloud compute ssh VM_NAME --command="cd ~/repo && screen -dmS EPIC bash -c 'bob --auth-method api-key -p \"Run epic\" > log.txt 2>&1'"
```
**Result**: Quote mangling, command breaks

### Failed Approach 2: WSL → gcloud → SSH
```bash
wsl gcloud compute ssh VM_NAME --command="..."
```
**Result**: Same issue - gcloud CLI is Windows binary, same quote problems

### Failed Approach 3: Inline orchestrator via SSH
```powershell
$orchestrator = @'
#!/bin/bash
screen -dmS EPIC bash -c "bob ..."
'@
gcloud compute ssh VM_NAME --command="$orchestrator"
```
**Result**: PowerShell quote escaping corrupts the script

### Failed Approach 4: File upload then execute
```powershell
gcloud compute scp orchestrator.sh VM_NAME:~/
gcloud compute ssh VM_NAME --command="bash ~/orchestrator.sh"
```
**Result**: SSH key cache mismatch blocks file upload

## What Works (Manual)

✅ **Google Cloud Console Browser SSH**: Direct terminal, no quote issues
- But requires manual copy/paste each wave (not automated)

## The Challenge

**How do we automate Wave 2 launch (and future waves) from a Windows laptop without manual intervention?**

## Requirements

1. ✅ **Fully automated** - No manual copy/paste
2. ✅ **Reliable** - Works every time
3. ✅ **Repeatable** - Same command for Wave 3, 4, 5...
4. ✅ **Windows-compatible** - Must work from Windows laptop
5. ✅ **Cost-effective** - No expensive infrastructure

## Available Resources

- **GCP Project**: `project-14c86305-3cba-493f-a73`
- **Golden Image**: `v12-bob-shell-golden-v2` (has Bob Shell, screen, git, Python)
- **Windows Laptop**: Has gcloud CLI, WSL (Ubuntu), PowerShell
- **MCP Servers**: You have access to Google Compute Engine MCP and other tools

## The Orchestrator Script (What Needs to Run on VM)

```bash
#!/bin/bash
cd ~/universal-or-strategy
mkdir -p logs

# Launch 10 parallel Bob Shell agents in screen sessions
screen -dmS EPIC-CCN-164 bash -c 'bob --accept-license --auth-method api-key -p "Run epic-intake for EPIC-CCN-164" --max-coins 30 > logs/EPIC-CCN-164.log 2>&1'
screen -dmS EPIC-CCN-107 bash -c 'bob --accept-license --auth-method api-key -p "Run epic-intake for EPIC-CCN-107" --max-coins 30 > logs/EPIC-CCN-107.log 2>&1'
# ... 8 more similar lines

sleep 2
screen -ls
```

## Potential Solutions to Explore

### Option A: Cloud Run Jobs
- Package orchestrator as Docker container
- Trigger via API
- **Question**: Can this work? How complex is setup?

### Option B: Cloud Functions
- HTTP-triggered function that SSHs into VM
- **Question**: Can Cloud Functions SSH into Compute Engine VMs?

### Option C: Startup Script with Metadata
- Pass epic list via VM metadata
- Startup script reads metadata and launches agents
- **Question**: Can we trigger startup script on existing VM?

### Option D: GCP Compute Engine MCP
- Use your MCP tools to orchestrate
- **Question**: Can MCP tools execute commands on VMs without quote escaping issues?

### Option E: Cloud Scheduler + Cloud Build
- Scheduled trigger runs Cloud Build
- Cloud Build executes gcloud commands
- **Question**: Does Cloud Build have same quote escaping issues?

### Option F: Terraform/Pulumi
- Infrastructure-as-code approach
- **Question**: Can IaC tools execute post-creation commands reliably?

## Your Mission

**Analyze this problem and recommend the BEST long-term solution** that:
1. Works from Windows laptop
2. Fully automated (no manual steps)
3. Reliable and repeatable
4. Simple to maintain

**Provide**:
1. **Recommended approach** (with rationale)
2. **Step-by-step implementation** (commands/code)
3. **Proof it solves quote escaping** (how it avoids the issue)
4. **Cost analysis** (if any additional GCP services needed)
5. **Maintenance burden** (how complex is it to maintain?)

## Success Criteria

The solution should allow us to run a single command like:
```bash
launch-wave-2
```

And have it:
1. Launch VM from golden image
2. Execute orchestrator script (10 parallel agents)
3. Monitor progress
4. Retrieve results
5. Clean up VM

**All without manual intervention or quote escaping issues.**

## Current Status

- ✅ Golden image v2 created and tested (works perfectly)
- ✅ Single epic test succeeded (70 seconds, all phases passed)
- ❌ Parallel launch blocked by quote escaping
- 💰 Current session cost: $59.67 (troubleshooting investment)
- 🎯 Target: Launch Wave 2 (10 epics, 30 min, $0.047)

## Question for You

**What is the best way to solve this automation challenge for long-term use?**

Use your Google Compute Engine MCP or any other tools at your disposal. Think creatively - there must be a way to automate this that we haven't tried yet.