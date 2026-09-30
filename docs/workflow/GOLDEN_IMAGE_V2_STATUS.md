# Golden Image v2 Creation Status

**Date**: 2026-06-12  
**VM**: v12-golden-image-v2  
**IP**: 136.111.14.177  
**Status**: 🔄 Running startup script

## Changes from v1

### Fix 1: Global Git Identity
```bash
git config --global user.email "malhitticrypto@gmail.com"
git config --global user.name "malhitticrypto"
```

**Why**: Bob Shell's checkpointing requires global git config, not just repo-level

### Fix 2: Bob Shell Pre-Authentication
```bash
bob auth --apikey <BOB_API_KEY>
```

**Why**: Enables headless operation without browser-based IBM SSO

**API Key Used**: `bob_prod_bob-admin_4UXUt9vwr3DKi2jrP1dEiXvaFmhdsqerpRo1bkVFZYLtod9BWoa82vRKNW2JvLNFiMCXiKWhAyhdHYjgsxCNoMDF_HzsjucwNDH4LGfvXN21q8jECWiaErhvvr9z9h474jEp5`

## Startup Script Timeline

| Time | Step | Duration |
|------|------|----------|
| 0:00 | VM boot | 1 min |
| 0:01 | Install Node.js 22.x | 2 min |
| 0:03 | Configure npm prefix | 10 sec |
| 0:03 | Install Bob Shell | 3 min |
| 0:06 | Configure git identity | 5 sec |
| 0:06 | Authenticate Bob Shell | 10 sec |
| 0:06 | Install additional tools | 1 min |
| 0:07 | Verification checks | 30 sec |
| **0:08** | **Setup complete** | **Total** |

## Verification Checklist

After 8 minutes, verify:
- [ ] Bob Shell v1.0.4 installed
- [ ] Global git config set
- [ ] Bob authentication configured
- [ ] `/tmp/setup_complete.txt` exists

## Next Steps

1. **Wait 8 minutes** for startup script to complete
2. **Verify installation**:
   ```powershell
   gcloud compute ssh v12-golden-image-v2 --zone=us-central1-a --command="bash -l -c 'bob --version && git config --global user.email && cat ~/.bob/settings.json | grep -q ibm_secrets && echo Bob authenticated'"
   ```
3. **Stop VM**:
   ```powershell
   gcloud compute instances stop v12-golden-image-v2 --zone=us-central1-a
   ```
4. **Create golden image v2**:
   ```powershell
   gcloud compute images create v12-bob-shell-golden-v2 --source-disk=v12-golden-image-v2 --source-disk-zone=us-central1-a --family=v12-bob-shell --description="V12 Golden Image v2 with Bob Shell 1.0.4, pre-authenticated, global git config"
   ```
5. **Launch test VM from v2**
6. **Run single epic test**
7. **If test passes → Launch Wave 2**

## Current Time

- **VM Started**: 06:13 UTC
- **Expected Complete**: 06:21 UTC (8 minutes)
- **Current**: Waiting...

## Cost Tracking

- Golden image v1: $0.04
- Test VM v1: $0.08
- Golden image v2: $0.04 (in progress)
- **Total so far**: $0.16