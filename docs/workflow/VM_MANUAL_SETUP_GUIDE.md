# VM Manual Setup Guide - Pre-Baked Image Creation

**Date**: 2026-06-12  
**Machine Type**: n2-standard-12 (12 vCPUs, 48 GB RAM)  
**Purpose**: Create golden image for autonomous epic execution

## Overview

This guide walks through manually creating a VM with Bob Shell pre-installed, then snapshotting it as a reusable image. This approach bypasses DNS and startup script issues.

## Step 1: Create Base VM (5 minutes)

```bash
gcloud compute instances create v12-golden-image \
  --project=project-14c86305-3cba-493f-a73 \
  --zone=us-central1-a \
  --machine-type=n2-standard-12 \
  --subnet=default \
  --maintenance-policy=TERMINATE \
  --provisioning-model=SPOT \
  --scopes=cloud-platform \
  --boot-disk-size=100GB \
  --boot-disk-type=pd-balanced \
  --image-family=ubuntu-2204-lts \
  --image-project=ubuntu-os-cloud
```

**Wait for VM to start** (~30 seconds)

## Step 2: SSH into VM

```bash
gcloud compute ssh v12-golden-image \
  --project=project-14c86305-3cba-493f-a73 \
  --zone=us-central1-a
```

## Step 3: Fix DNS (if needed)

**Test DNS first**:
```bash
ping -c 1 google.com
```

**If DNS fails**, fix it:
```bash
# Create new resolv.conf
cat > /tmp/resolv.conf << 'EOF'
nameserver 8.8.8.8
nameserver 8.8.4.4
nameserver 1.1.1.1
EOF

# Apply
sudo cp /tmp/resolv.conf /etc/resolv.conf

# Test again
ping -c 1 google.com
```

## Step 4: Install Dependencies (2 minutes)

```bash
# Update package list
sudo apt-get update

# Install required packages
sudo apt-get install -y git curl wget python3 python3-pip tmux jq

# Verify installations
git --version
python3 --version
tmux -V
```

## Step 5: Install Bob Shell (3 minutes)

**Method 1: Direct curl** (try this first):
```bash
curl -fsSL https://bob.build/install.sh | bash
```

**Method 2: Manual download** (if Method 1 fails):
```bash
wget https://bob.build/install.sh -O /tmp/bob_install.sh
bash /tmp/bob_install.sh
```

**Method 3: Alternative endpoint** (if both fail):
```bash
# Try alternative Bob installation endpoint
curl -fsSL https://install.bob.build | bash
```

**Add Bob to PATH**:
```bash
export PATH="$HOME/.bob/bin:$PATH"
echo 'export PATH="$HOME/.bob/bin:$PATH"' >> ~/.bashrc
source ~/.bashrc
```

**Verify Bob installation**:
```bash
bob --version
```

Expected output: `Bob CLI version X.X.X`

## Step 6: Clone Repository (2 minutes)

```bash
# Clone from GitHub (use main branch)
git clone https://github.com/malhitticrypto-debug/universal-or-strategy.git
cd universal-or-strategy

# Verify branch
git branch
# Should show: * main

# Verify repository structure
ls -la
# Should see: src/, scripts/, docs/, etc.
```

## Step 7: Install Python Dependencies (1 minute)

```bash
# Install any required Python packages
pip3 install --user requests

# Verify
python3 -c "import requests; print('OK')"
```

## Step 8: Create Helper Scripts (2 minutes)

**Create execution wrapper**:
```bash
cat > ~/run_epic_wave.sh << 'EOF'
#!/bin/bash
# Epic Wave Execution Wrapper
# Usage: ./run_epic_wave.sh <config_file>

CONFIG_FILE=${1:-/tmp/epic_config.json}

if [ ! -f "$CONFIG_FILE" ]; then
    echo "ERROR: Config file not found: $CONFIG_FILE"
    exit 1
fi

cd ~/universal-or-strategy

# Start execution in tmux
tmux new-session -d -s epic-wave \
    "python3 scripts/wave2_simple_orchestrator.py $CONFIG_FILE 2>&1 | tee /tmp/execution.log"

echo "Execution started in tmux session 'epic-wave'"
echo "Attach with: tmux attach -t epic-wave"
echo "View logs: tail -f /tmp/execution.log"
EOF

chmod +x ~/run_epic_wave.sh
```

**Create status checker**:
```bash
cat > ~/check_status.sh << 'EOF'
#!/bin/bash
# Check execution status

echo "=== Tmux Sessions ==="
tmux ls 2>/dev/null || echo "No active sessions"

echo -e "\n=== Recent Log Entries ==="
if [ -f /tmp/execution.log ]; then
    tail -20 /tmp/execution.log
else
    echo "No execution log found"
fi

echo -e "\n=== Manifest Status ==="
if [ -d ~/universal-or-strategy/docs/brain ]; then
    find ~/universal-or-strategy/docs/brain -name "manifest.json" -exec echo "Found: {}" \;
else
    echo "No manifests found"
fi
EOF

chmod +x ~/check_status.sh
```

## Step 9: Test Bob Shell (1 minute)

```bash
# Quick test
cd ~/universal-or-strategy
bob --version

# Test with a simple command
echo "Test successful!"
```

## Step 10: Clean Up Before Snapshot (1 minute)

```bash
# Clear bash history
history -c

# Clear temporary files
rm -rf /tmp/*

# Clear logs
sudo journalctl --vacuum-time=1s

# Exit SSH
exit
```

## Step 11: Stop VM

```bash
gcloud compute instances stop v12-golden-image \
  --project=project-14c86305-3cba-493f-a73 \
  --zone=us-central1-a
```

**Wait for VM to stop** (~30 seconds)

## Step 12: Create Image Snapshot (5 minutes)

```bash
gcloud compute images create v12-bob-shell-golden-v1 \
  --project=project-14c86305-3cba-493f-a73 \
  --source-disk=v12-golden-image \
  --source-disk-zone=us-central1-a \
  --family=v12-bob-shell \
  --description="Golden image with Bob Shell pre-installed for V12 epic execution"
```

**Wait for image creation** (~5 minutes)

## Step 13: Delete Base VM (optional)

```bash
# Keep the base VM if you want to make changes later
# Or delete it to save costs:
gcloud compute instances delete v12-golden-image \
  --project=project-14c86305-3cba-493f-a73 \
  --zone=us-central1-a \
  --quiet
```

## Step 14: Test Image (5 minutes)

**Create test VM from image**:
```bash
gcloud compute instances create v12-test-from-image \
  --project=project-14c86305-3cba-493f-a73 \
  --zone=us-central1-a \
  --machine-type=n2-standard-12 \
  --subnet=default \
  --maintenance-policy=TERMINATE \
  --provisioning-model=SPOT \
  --scopes=cloud-platform \
  --boot-disk-size=100GB \
  --boot-disk-type=pd-balanced \
  --image=v12-bob-shell-golden-v1
```

**SSH and verify**:
```bash
gcloud compute ssh v12-test-from-image \
  --project=project-14c86305-3cba-493f-a73 \
  --zone=us-central1-a

# Verify Bob
bob --version

# Verify repository
cd ~/universal-or-strategy
git status

# Exit
exit
```

**If test succeeds**, delete test VM:
```bash
gcloud compute instances delete v12-test-from-image \
  --project=project-14c86305-3cba-493f-a73 \
  --zone=us-central1-a \
  --quiet
```

## Step 15: Launch Production VMs from Image

**For Wave 2 (10 epics)**:
```bash
gcloud compute instances create v12-wave2-executor \
  --project=project-14c86305-3cba-493f-a73 \
  --zone=us-central1-a \
  --machine-type=n2-standard-12 \
  --subnet=default \
  --maintenance-policy=TERMINATE \
  --provisioning-model=SPOT \
  --scopes=cloud-platform \
  --boot-disk-size=100GB \
  --boot-disk-type=pd-balanced \
  --image=v12-bob-shell-golden-v1
```

**Copy config and start execution**:
```bash
# Copy config to VM
gcloud compute scp test_config_2_epics.json \
  malhitticrypto@v12-wave2-executor:/tmp/epic_config.json \
  --project=project-14c86305-3cba-493f-a73 \
  --zone=us-central1-a

# Start execution
gcloud compute ssh v12-wave2-executor \
  --project=project-14c86305-3cba-493f-a73 \
  --zone=us-central1-a \
  --command="~/run_epic_wave.sh /tmp/epic_config.json"
```

## Troubleshooting

### DNS Issues
If DNS still fails after Step 3:
```bash
# Check current DNS
cat /etc/resolv.conf

# Check systemd-resolved
sudo systemctl status systemd-resolved

# Restart networking
sudo systemctl restart systemd-networkd
```

### Bob Installation Fails
If all 3 methods fail:
```bash
# Check network connectivity
curl -I https://google.com

# Check if bob.build is reachable
nslookup bob.build

# Try direct IP (if DNS fails)
# Contact Bob support for alternative installation method
```

### Repository Clone Fails
If git clone fails:
```bash
# Check GitHub connectivity
ping github.com

# Try HTTPS instead of SSH
git clone https://github.com/malhitticrypto-debug/universal-or-strategy.git

# Check credentials (if private repo)
git config --global user.name "Your Name"
git config --global user.email "your@email.com"
```

## Success Criteria

✅ VM boots in <30 seconds  
✅ Bob Shell responds to `bob --version`  
✅ Repository exists at `~/universal-or-strategy`  
✅ Repository is on `main` branch  
✅ Helper scripts are executable  
✅ Image creation completes successfully  
✅ Test VM boots from image and passes verification  

## Cost Summary

- **Base VM runtime**: ~20 minutes × $0.14/hour = $0.047
- **Image storage**: $0.10/GB/month × 10 GB = $1.00/month
- **Test VM runtime**: ~5 minutes × $0.14/hour = $0.012
- **Total one-time cost**: $0.059
- **Monthly cost**: $1.00 (image storage)

## Next Steps After Image Creation

1. Update `test_config_2_epics.json` with correct API keys
2. Launch production VM from image
3. Copy config to VM
4. Start 2-epic validation test
5. Monitor execution
6. If successful, scale to full Wave 2 (10 epics)
7. Create additional images for different configurations if needed

## Image Versioning

- **v1**: Initial golden image with Bob Shell + repository
- **v2**: (future) Updated Bob Shell version
- **v3**: (future) Additional dependencies or optimizations

Keep base VM around for quick updates, or recreate from image when needed.