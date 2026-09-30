# Firebase Key Update Instructions

## Issue Summary

After the Firebase key exposure incident (2026-06-14), a new service account key was generated. However, there's a filename mismatch:

- **Old key** (revoked): `firebase-credentials.json`
- **New key** (active): `firebase-key.json`
- **Scripts expect**: `firebase-credentials.json`

## Files That Need the Firebase Key

### Python Scripts (Hardcoded Path)

1. **`scripts/query_kb.py`** (Line 9)
   ```python
   CREDENTIALS_PATH = 'firebase-credentials.json'
   ```

2. **`scripts/phase_4_5_ticket_review_mcp.py`** (Line 19)
   ```python
   CREDENTIALS_PATH = 'firebase-credentials.json'
   ```

### Custom Modes (Environment Variable)

The custom modes may reference `GOOGLE_APPLICATION_CREDENTIALS` environment variable, which should point to the credentials file.

## Solution Options

### Option 1: Rename New Key (Recommended)

**Pros**: No code changes needed, maintains consistency with existing scripts
**Cons**: None

**Steps**:
```powershell
# Backup old key (already revoked, but keep for reference)
Rename-Item firebase-credentials.json firebase-credentials.json.revoked

# Rename new key to expected filename
Rename-Item firebase-key.json firebase-credentials.json

# Verify gitignore protection
git status  # Should NOT show firebase-credentials.json
```

### Option 2: Update All Scripts

**Pros**: Uses the new filename convention
**Cons**: Requires updating multiple files, risk of missing references

**Files to Update**:
- `scripts/query_kb.py` (Line 9)
- `scripts/phase_4_5_ticket_review_mcp.py` (Line 19)
- Any custom mode configurations
- Documentation references

## Recommended Action

**Use Option 1** - Rename the new key to `firebase-credentials.json`:

```powershell
# Execute these commands in PowerShell
cd C:\WSGTA\universal-or-strategy

# Backup old revoked key
if (Test-Path firebase-credentials.json) {
    Rename-Item firebase-credentials.json firebase-credentials.json.revoked
}

# Rename new key to expected filename
Rename-Item firebase-key.json firebase-credentials.json

# Verify gitignore protection
git status
```

## Verification

After renaming, test Firebase connectivity:

```powershell
# Test Jane Street KB query
python scripts/query_kb.py "complexity reduction"

# Should return results without errors
```

## Current Status

- ✅ New key generated and saved as `firebase-key.json`
- ✅ Old key revoked by Google
- ✅ `.gitignore` properly configured
- ⏳ **PENDING**: Rename new key to `firebase-credentials.json`

## Security Notes

- Both `firebase-key.json` and `firebase-credentials.json` are in `.gitignore`
- The template file `firebase-key.json.template` is safe to commit (no real credentials)
- Always verify with `git status` before committing

## References

- Incident Report: `docs/security/FIREBASE_KEY_EXPOSURE_INCIDENT.md`
- Template: `firebase-key.json.template`
- Gitignore: `.gitignore` (lines with firebase entries)