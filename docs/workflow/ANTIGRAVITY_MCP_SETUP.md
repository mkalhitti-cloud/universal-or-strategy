# Antigravity MCP Server Configuration for VM Setup

## Required MCP Servers

Enable these MCP servers in Antigravity IDE before starting the VM setup:

### ✅ CRITICAL: Google Compute Engine
**Why**: Antigravity needs this to manage the VM (SSH, status checks, start/stop)
**Required for**: All `gcloud compute` commands in the handoff tasks

### ✅ HELPFUL: Google Cloud Resource Manager
**Why**: Helps Antigravity search and manage GCP projects
**Useful for**: Project-level operations and resource discovery

### ✅ HELPFUL: Google Cloud Logging
**Why**: Antigravity can check VM logs if something goes wrong
**Useful for**: Debugging and troubleshooting

## Not Needed for This Task

- ❌ Stitch (UI design tool)
- ❌ Chrome DevTools MCP (browser automation)
- ❌ Google Developer Knowledge (documentation search)
- ❌ ClickHouse (database tool)
- ❌ Android Management API (mobile device management)
- ❌ Vertex AI Search (AI search)
- ❌ Google Cloud Firestore (database)
- ❌ Google Managed Service for Apache Kafka (streaming)

## Antigravity Prompt

After enabling the MCP servers above, paste this into Antigravity:

```
I'm continuing the GCP VM setup for Wave 2 autonomous execution. Bob has created a golden image with Bob Shell pre-installed and launched a test VM, but needs terminal access to complete the setup.

Read docs/workflow/ANTIGRAVITY_VM_SETUP_HANDOFF.md and execute Tasks 1-5 to validate the test VM. Report back with results when complete.

Key info:
- VM: v12-test-epic-164 (zone: us-central1-a)
- Test Epic: EPIC-CCN-164
- Expected time: ~25 minutes
- First command will ask to accept SSH host key - type 'y'

Start with Task 1 (Verify VM Environment).
```

## Why Google Compute Engine MCP Matters

With the Google Compute Engine MCP enabled, Antigravity can:
- Use native GCP APIs instead of shell commands
- Get structured VM status (not just text output)
- Handle SSH operations more reliably
- Monitor VM state in real-time

This makes Antigravity much more effective at managing the test VM!