# VM Configuration Reference

**Version**: 1.0
**Last Updated**: 2026-06-20
**Status**: AUTHORITATIVE SOURCE OF TRUTH

## Current VM Details

### Production VM
- **Name**: `v12-test-golden-v2`
- **Zone**: `us-central1-a`
- **Machine Type**: `n2-standard-8`
- **Preemptible**: Yes (SPOT instance)
- **External IP**: Dynamic (check with `gcloud compute instances list`)
- **User**: `malhitticrypto`
- **Repository Path**: `/home/malhitticrypto/universal-or-strategy`

### Golden Image
- **Name**: `v12-bob-shell-golden-v2`
- **Status**: Production-ready
- **Bob CLI**: Installed at `~/bob` (aliased in `~/.bashrc`)

## Common Commands

### Check VM Status
```bash
gcloud compute instances describe v12-test-golden-v2 --zone=us-central1-a --format="get(status)"
```

### Start VM
```bash
gcloud compute instances start v12-test-golden-v2 --zone=us-central1-a
```

### Stop VM
```bash
gcloud compute instances stop v12-test-golden-v2 --zone=us-central1-a
```

### Get Current IP
```bash
gcloud compute instances describe v12-test-golden-v2 --zone=us-central1-a --format="get(networkInterfaces[0].accessConfigs[0].natIP)"
```

### SSH to VM
```bash
# Get current IP first
VM_IP=$(gcloud compute instances describe v12-test-golden-v2 --zone=us-central1-a --format="get(networkInterfaces[0].accessConfigs[0].natIP)")
ssh malhitticrypto@$VM_IP
```

### Execute Command on VM
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="cd ~/universal-or-strategy && <command>"
```

## Historical VM Names (DEPRECATED)

These names appear in old documentation but are NO LONGER VALID:

- ❌ `universal-or-vm` - Never existed
- ❌ `v12-test-epic-164` - Test VM from Wave 2, deleted
- ❌ `v12-wave2-parallel` - Planned but never created

## Update Protocol

When VM details change:
1. Update this file FIRST
2. Search codebase for old references
3. Update all scripts and documentation
4. Commit with message: "chore: update VM configuration to [new-name]"

## Verification Checklist

Before any wave execution:
- [ ] Verify VM name: `v12-test-golden-v2`
- [ ] Verify zone: `us-central1-a`
- [ ] Verify user: `malhitticrypto`
- [ ] Verify repo path: `/home/malhitticrypto/universal-or-strategy`
- [ ] Get current IP (dynamic)
- [ ] Test SSH connectivity

## Cost Tracking

- **Hourly Rate**: $0.093/hour (SPOT pricing)
- **Typical Wave Duration**: 30-60 minutes
- **Typical Wave Cost**: $0.047 - $0.093

## References

- Golden Image Creation: `docs/workflow/VM_SETUP_V8_SOLUTION.md`
- Wave Execution: `building-blocks/autonomous-refactoring/ARCHITECTURE.md`
- VM Setup Protocol: `docs/protocol/VM_SETUP_PROTOCOL.md`