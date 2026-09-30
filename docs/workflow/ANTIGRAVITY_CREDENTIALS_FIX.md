# Fixing Antigravity Google Compute Engine MCP Credentials

## Problem

The Google Compute Engine MCP is showing this error:
```
Error: Failed to get Google credentials: google: error getting credentials using GOOGLE_APPLICATION_CREDENTIALS environment variable: open C:\WSGTA\universal-or-strategy\firebase-key.json: The system cannot find the file specified.
```

## Root Cause

The MCP server is looking for a service account key file (`firebase-key.json`) but:
1. The file doesn't exist
2. You're already authenticated via `gcloud auth` (user credentials)
3. The MCP server needs to use your existing gcloud credentials instead

## Solution Options

### Option 1: Use Existing gcloud Credentials (Recommended)

The MCP server should use your existing gcloud user credentials instead of looking for a service account key.

**In Antigravity IDE**:
1. Click "Configure" on the Google Compute Engine MCP
2. Remove or comment out the `GOOGLE_APPLICATION_CREDENTIALS` environment variable
3. The MCP will fall back to using your gcloud user credentials automatically

### Option 2: Create Application Default Credentials

Run this command to create application default credentials from your gcloud login:

```powershell
gcloud auth application-default login
```

This will:
- Open a browser for authentication
- Store credentials in `%APPDATA%\gcloud\application_default_credentials.json`
- Allow the MCP to use your user credentials

### Option 3: Skip MCP - Use Direct gcloud Commands

Since you already have working gcloud credentials, Antigravity can just use `gcloud` commands directly without the MCP server:

**Disable the Google Compute Engine MCP** and use these commands instead:

```powershell
# Verify VM environment
gcloud compute ssh v12-test-epic-164 --zone=us-central1-a --command="bash -l -c 'bob --version && git --version && python3 --version'"

# Clone repo
gcloud compute ssh v12-test-epic-164 --zone=us-central1-a --command="cd ~ && git clone https://github.com/malhitticrypto-debug/universal-or-strategy.git"

# Run epic test
gcloud compute ssh v12-test-epic-164 --zone=us-central1-a --command="cd ~/universal-or-strategy && bob /epic-intake EPIC-CCN-164"
```

## Recommended Approach

**Use Option 3** (Skip MCP, use direct gcloud commands):
- You already have working gcloud credentials
- No additional setup needed
- Antigravity can run the commands directly
- The MCP server adds complexity without benefit in this case

## Updated Antigravity Prompt

Use this simplified prompt instead:

```
I'm continuing the GCP VM setup for Wave 2 autonomous execution. Bob has created a golden image with Bob Shell pre-installed and launched a test VM.

Execute these tasks using direct gcloud commands (you have working gcloud credentials):

**Task 1: Verify VM Environment**
```powershell
gcloud compute ssh v12-test-epic-164 --zone=us-central1-a --command="bash -l -c 'bob --version && git --version && python3 --version'"
```
(Accept SSH host key when prompted - type 'y')

Expected output: Bob Shell 1.0.4, git 2.x, Python 3.12.x

**Task 2: Clone Repository**
```powershell
gcloud compute ssh v12-test-epic-164 --zone=us-central1-a --command="cd ~ && git clone https://github.com/malhitticrypto-debug/universal-or-strategy.git && cd universal-or-strategy && git config user.email 'malhitticrypto@gmail.com' && git config user.name 'malhitticrypto'"
```

**Task 3: Run Epic Test** (15-20 minutes)
```powershell
gcloud compute ssh v12-test-epic-164 --zone=us-central1-a --command="cd ~/universal-or-strategy && bob /epic-intake EPIC-CCN-164"
```

**Task 4: Check Completion**
```powershell
gcloud compute ssh v12-test-epic-164 --zone=us-central1-a --command="cd ~/universal-or-strategy && cat docs/brain/EPIC-CCN-164/05-completion-report.md"
```

Report back with results when complete.
```

## Why This Works

- Your gcloud CLI is already authenticated (`malhitticrypto@gmail.com`)
- The `gcloud compute ssh` commands will use your existing credentials
- No MCP server configuration needed
- Simpler and more reliable