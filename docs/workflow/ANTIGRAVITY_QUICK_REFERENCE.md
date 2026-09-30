# Antigravity Quick Reference - VM Verification

## Current VM Status

**VM Name**: v12-golden-image (v4)
**Status**: Running with 8-minute setup timer
**External IP**: 136.111.14.177
**Zone**: us-central1-a
**Machine**: n2-standard-8 (8 vCPUs, 32 GB RAM)
**Project**: project-14c86305-3cba-493f-a73

## Verification Command (Run After 8 Minutes)

```powershell
gcloud compute ssh v12-golden-image --project=project-14c86305-3cba-493f-a73 --zone=us-central1-a --command="bash -l -c 'cat /tmp/setup_complete.txt && bob --version'"
```

## Expected Output (Success)

```
Setup complete!
Bob Shell v1.x.x
```

## If Success - Next Commands

### 1. Stop VM
```powershell
gcloud compute instances stop v12-golden-image --project=project-14c86305-3cba-493f-a73 --zone=us-central1-a
```

### 2. Create Golden Image
```powershell
gcloud compute images create v12-bob-shell-golden-v1 --project=project-14c86305-3cba-493f-a73 --source-disk=v12-golden-image --source-disk-zone=us-central1-a --family=v12-bob-shell --description="Golden image with Bob Shell, Node.js, Python, and repository"
```

### 3. Launch Test VM
```powershell
gcloud compute instances create v12-test-vm --project=project-14c86305-3cba-493f-a73 --zone=us-central1-a --machine-type=n2-standard-8 --image=v12-bob-shell-golden-v1 --boot-disk-size=100GB --maintenance-policy=TERMINATE --provisioning-model=SPOT --scopes=cloud-platform
```

### 4. Verify Test VM
```powershell
gcloud compute ssh v12-test-vm --project=project-14c86305-3cba-493f-a73 --zone=us-central1-a --command="bash -l -c 'bob --version && ls -la ~/universal-or-strategy'"
```

## If Failure - Troubleshooting

### Check Setup Logs
```powershell
gcloud compute ssh v12-golden-image --project=project-14c86305-3cba-493f-a73 --zone=us-central1-a --command="cat /var/log/syslog | grep startup-script"
```

### Check Bob Installation
```powershell
gcloud compute ssh v12-golden-image --project=project-14c86305-3cba-493f-a73 --zone=us-central1-a --command="which bob && npm list -g @ibm/bob-shell"
```

### Manual Fix (If Needed)
```powershell
# SSH into VM
gcloud compute ssh v12-golden-image --project=project-14c86305-3cba-493f-a73 --zone=us-central1-a

# Inside VM:
npm install -g @ibm/bob-shell
bob --version
exit

# Then stop and create image
```

## Alternative: Try v5 Mise-based Script

### Delete v4 VM
```powershell
gcloud compute instances delete v12-golden-image --project=project-14c86305-3cba-493f-a73 --zone=us-central1-a --quiet
```

### Create v5 VM
```powershell
gcloud compute instances create v12-golden-image --project=project-14c86305-3cba-493f-a73 --zone=us-central1-a --machine-type=n2-standard-8 --subnet=default --maintenance-policy=TERMINATE --provisioning-model=SPOT --scopes=cloud-platform --boot-disk-size=100GB --boot-disk-type=pd-balanced --image-family=ubuntu-2204-lts --image-project=ubuntu-os-cloud --metadata-from-file=startup-script=scripts/vm_startup_script_v5_mise.sh
```

### Wait 8 Minutes and Verify
```powershell
gcloud compute ssh v12-golden-image --project=project-14c86305-3cba-493f-a73 --zone=us-central1-a --command="bash -l -c 'cat /tmp/setup_complete.txt && bob --version && mise --version'"
```

## Cost Tracking

- **Setup time**: ~8 minutes × $0.093/hour = $0.012
- **Verification time**: ~2 minutes × $0.093/hour = $0.003
- **Total one-time cost**: ~$0.015
- **Image storage**: $1.00/month
- **Per-VM launch**: 30 seconds × $0.093/hour = $0.0016

## Timer Started

**Start time**: When Antigravity started 8-minute timer
**Expected completion**: 8 minutes from start
**Action**: Run verification command above

---

**Made with Bob** 🤖