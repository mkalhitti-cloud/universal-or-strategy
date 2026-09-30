# VM Firebase Setup Required

## Date: 2026-06-14

## Issue Discovered

During Firebase key rotation testing, discovered that the **VM does not have Firebase dependencies installed**.

## Test Results

### Local Machine ✅
```bash
$ python scripts/query_kb.py "test"
[*] Querying Jane Street Knowledge Base...
[*] Available documents in collection:
  - 10 Jane Street documents successfully listed
```

### VM ❌
```bash
$ gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="python3 scripts/query_kb.py test"
Traceback (most recent call last):
  File "/home/malhitticrypto/universal-or-strategy/scripts/query_kb.py", line 4, in <module>
    import firebase_admin
ModuleNotFoundError: No module named 'firebase_admin'
```

## Root Cause

The VM golden image (`v12-test-golden-v2`) does not have the Python `firebase-admin` package installed.

## Impact

**Phase 4.5 (Ticket Review)** and **Jane Street KB queries** will fail on the VM until Firebase is set up.

## Solution Options

### Option 1: Install Firebase on VM (Recommended)

**Steps**:
```bash
# SSH to VM
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a

# Install firebase-admin
pip3 install firebase-admin

# Copy new Firebase key
exit
gcloud compute scp firebase-credentials.json v12-test-golden-v2:/home/malhitticrypto/universal-or-strategy/ --zone=us-central1-a

# Test
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="cd ~/universal-or-strategy && python3 scripts/query_kb.py test"
```

### Option 2: Skip Firebase on VM

**If Phase 4.5 is not needed for current wave**:
- Phase 0-4 do not require Firebase
- Phase 5 (Ticket Execution) does not require Firebase
- Phase 6 (Final Review) does not require Firebase

**Only Phase 4.5 (Ticket Review)** requires Firebase for Jane Street KB integration.

## Current Status

- ✅ **Local Firebase**: Working (key rotated, dependencies installed)
- ❌ **VM Firebase**: Not working (missing `firebase-admin` package)
- ⏳ **VM Setup**: Pending (install dependencies + copy key)

## Recommendation

**For Wave 3 (Current)**:
- If using 10-phase workflow (includes Phase 4.5): Install Firebase on VM
- If using 9-phase workflow (skips Phase 4.5): No action needed

**For Future Waves**:
- Update golden image to include `firebase-admin` in `requirements.txt`
- Include Firebase key setup in VM bootstrap script

## Related Files

- Firebase key (local): `firebase-credentials.json` (gitignored)
- Python script: `scripts/query_kb.py`
- Phase 4.5 script: `scripts/phase_4_5_ticket_review_mcp.py`
- Requirements: `requirements.txt` (should include `firebase-admin`)

## Next Steps

1. **Decide**: Is Phase 4.5 needed for current wave?
   - **YES**: Install Firebase on VM (Option 1)
   - **NO**: Skip Firebase setup, proceed with 9-phase workflow

2. **If installing Firebase**:
   ```bash
   # Quick install script
   gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="pip3 install firebase-admin"
   gcloud compute scp firebase-credentials.json v12-test-golden-v2:/home/malhitticrypto/universal-or-strategy/ --zone=us-central1-a
   gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a --command="cd ~/universal-or-strategy && python3 scripts/query_kb.py test"
   ```

3. **Update golden image** (for future waves):
   - Add `firebase-admin` to `requirements.txt`
   - Include in VM setup documentation

## References

- Incident Report: `docs/security/FIREBASE_KEY_EXPOSURE_INCIDENT.md`
- Fix Instructions: `docs/security/FIREBASE_KEY_UPDATE_INSTRUCTIONS.md`
- Completion Report: `docs/security/FIREBASE_FIX_COMPLETE.md`