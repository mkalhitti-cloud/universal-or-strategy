# Token Rotation Instructions - EPIC-7-QUALITY-001

**Priority:** P0 CRITICAL  
**Date:** 2026-05-24T04:28:00Z  
**Status:** ACTION REQUIRED

## ⚠️ CRITICAL: Immediate Action Required

The following API tokens were **exposed in git history** (commit 75b4e911ef2f4cfd40d99b6b9d5bbe5bf3e490bf) and **MUST be rotated immediately**:

1. LangSmith API Key
2. Context7 API Key
3. Pinecone API Key
4. Braintrust API Key
5. Greptile Bearer Token (2 instances)

**These tokens are permanently compromised and must be revoked.**

---

## Token Rotation Checklist

### 1. LangSmith API Key

**Exposed Token:** `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`

**Steps to Rotate:**

1. **Log in to LangSmith:**
   - URL: https://smith.langchain.com/
   - Navigate to Settings > API Keys

2. **Revoke the compromised key:**
   - Find key ending in `...fba99ee3cc`
   - Click "Revoke" or "Delete"
   - Confirm revocation

3. **Generate new key:**
   - Click "Create API Key"
   - Name: `V12-Universal-OR-Strategy-2026-05`
   - Copy the new key immediately (shown only once)

4. **Update `.env`:**
   ```bash
   LANGSMITH_API_KEY=lsv2_pt_YOUR_NEW_KEY_HERE
   ```

5. **Verify:**
   ```powershell
   python scripts/langsmith_bridge.py --test
   # Expected: "[+] Trace emitted successfully."
   ```

**Status:** ⬜ Not Started

---

### 2. Context7 API Key

**Exposed Token:** `ctx7sk-faca8e7b-48b4-4f7c-b7ca-ccc9424bef17`

**Steps to Rotate:**

1. **Log in to Context7:**
   - URL: https://context7.com/
   - Navigate to API Keys section

2. **Revoke the compromised key:**
   - Find key `ctx7sk-faca8e7b-48b4-4f7c-b7ca-ccc9424bef17`
   - Click "Revoke" or "Delete"

3. **Generate new key:**
   - Click "Create New API Key"
   - Copy the new key

4. **Update `.env`:**
   ```bash
   CONTEXT7_API_KEY=ctx7sk-YOUR_NEW_KEY_HERE
   ```

5. **Verify:**
   - Test Context7 integration
   - Confirm API calls succeed

**Status:** ⬜ Not Started

---

### 3. Pinecone API Key

**Exposed Token:** `pcsk_rciXF_PkmYQ3exL4pgKwJtthYR1gHyyLgJjx8RefAm53H6sgtWDjF4hoBXSH8E44PTFED`

**Steps to Rotate:**

1. **Log in to Pinecone:**
   - URL: https://www.pinecone.io/
   - Navigate to your project

2. **Revoke the compromised key:**
   - Go to API Keys section
   - Find key starting with `pcsk_rciXF_...`
   - Click "Delete" or "Revoke"

3. **Generate new key:**
   - Click "Create API Key"
   - Name: `V12-Universal-OR-2026-05`
   - Copy the new key

4. **Update `.env`:**
   ```bash
   PINECONE_API_KEY=pcsk_YOUR_NEW_KEY_HERE
   ```

5. **Verify:**
   - Test vector database connection
   - Confirm queries succeed

**Status:** ⬜ Not Started

---

### 4. Braintrust API Key

**Exposed Token:** `sk-XucAZsl9Qu6ru3eQiURKaghIXGj1QEKs7z5cBlkUyvGlolXG`

**Steps to Rotate:**

1. **Log in to Braintrust:**
   - URL: https://www.braintrustdata.com/
   - Navigate to Settings > API Keys

2. **Revoke the compromised key:**
   - Find key starting with `sk-XucAZsl9...`
   - Click "Revoke" or "Delete"

3. **Generate new key:**
   - Click "Create API Key"
   - Name: `V12-Universal-OR-2026-05`
   - Copy the new key

4. **Update `.env`:**
   ```bash
   BRAINTRUST_API_KEY=sk-YOUR_NEW_KEY_HERE
   ```

5. **Verify:**
   - Test Braintrust integration
   - Confirm evaluation runs succeed

**Status:** ⬜ Not Started

---

### 5. Greptile Bearer Token

**Exposed Tokens:**
- `vob20OZM949/QgQ/IPxtzrU7lJDMGEFuFvwQ8D0UxO3lJ2CG` (in `.bob/mcp.json`)
- `GKZ5piB2DLIr22NtSOF/afDCpA6MT3YiAjpsEkbI6Fx88DK9` (in documentation)

**Steps to Rotate:**

1. **Log in to Greptile:**
   - URL: https://greptile.com/
   - Navigate to API Keys or Settings

2. **Revoke BOTH compromised tokens:**
   - Find token ending in `...88DK9`
   - Find token ending in `...J2CG`
   - Revoke both tokens

3. **Generate new token:**
   - Click "Create API Key" or "Generate Token"
   - Name: `V12-Universal-OR-2026-05`
   - Copy the new token

4. **Update `.env`:**
   ```bash
   GREPTILE_API_KEY=YOUR_NEW_TOKEN_HERE
   ```

5. **Verify MCP server connection:**
   - Restart VS Code or Bob CLI
   - Test Greptile MCP server:
     ```powershell
     # In Bob CLI or VS Code, try a Greptile query
     # Should connect without authentication errors
     ```

**Status:** ⬜ Not Started

---

## Post-Rotation Verification

After rotating all tokens, complete these verification steps:

### 1. Environment Variables Check

```powershell
# Verify .env file exists and contains new tokens
Test-Path .env
# Should return: True

# Verify no old tokens remain
Select-String -Path .env -Pattern "lsv2_pt_4a43d21002c7418bbba1f56725080a33"
# Should return: Nothing (no matches)
```

### 2. Gitleaks Scan

```powershell
# Verify no secrets in working directory
gitleaks detect --source . --verbose --no-git
# Expected: "no leaks found"
```

### 3. Build Verification

```powershell
# Verify build still works
powershell -File .\scripts\build_readiness.ps1
# Expected: Build succeeds
```

### 4. MCP Server Test

```powershell
# Test Greptile MCP connection
# Open Bob CLI or VS Code
# Try a code search query
# Should succeed without authentication errors
```

### 5. LangSmith Tracing Test

```powershell
# Test LangSmith connection
python scripts/langsmith_bridge.py --test
# Expected: "[+] Trace emitted successfully."
```

---

## Security Recommendations

### Immediate Actions (Complete Today)

- [ ] Rotate all 5 exposed tokens
- [ ] Update `.env` with new tokens
- [ ] Verify all services work with new tokens
- [ ] Run Gitleaks scan to confirm no leaks
- [ ] Test build and MCP servers

### Short-term Actions (This Week)

- [ ] Review access logs for compromised tokens
- [ ] Check for unauthorized API usage
- [ ] Update team documentation with new setup process
- [ ] Schedule token rotation reminder (quarterly)

### Long-term Actions (This Month)

- [ ] Consider git history cleanup (BFG Repo-Cleaner or git filter-repo)
- [ ] Implement pre-commit hooks for secret detection
- [ ] Set up secrets manager (AWS Secrets Manager, Azure Key Vault)
- [ ] Document incident in security log

---

## Git History Cleanup (Optional but Recommended)

The exposed tokens are **permanently in git history**. Even after rotation, they remain visible in commit 75b4e911ef2f4cfd40d99b6b9d5bbe5bf3e490bf.

### Option 1: BFG Repo-Cleaner (Recommended)

```powershell
# Install BFG
# Download from: https://rtyley.github.io/bfg-repo-cleaner/

# Clone a fresh copy
git clone --mirror https://github.com/mdasdispatch-hash/universal-or-strategy.git

# Remove .env from history
java -jar bfg.jar --delete-files .env universal-or-strategy.git

# Clean up
cd universal-or-strategy.git
git reflog expire --expire=now --all
git gc --prune=now --aggressive

# Force push (requires coordination with team)
git push --force
```

### Option 2: git filter-repo

```powershell
# Install git-filter-repo
pip install git-filter-repo

# Remove .env from history
git filter-repo --path .env --invert-paths

# Force push (requires coordination with team)
git push --force --all
```

**⚠️ Warning:** Force-pushing rewrites history and requires all team members to re-clone the repository.

---

## Incident Timeline

| Date | Event | Action |
|------|-------|--------|
| 2026-05-18 23:19:50Z | `.env` committed to git (commit 75b4e911) | Tokens exposed |
| 2026-05-24 04:23:00Z | Gitleaks scan detected exposure | Audit initiated |
| 2026-05-24 04:28:00Z | Migration to environment variables completed | This document created |
| TBD | Token rotation completed | User action required |
| TBD | Git history cleanup (optional) | User decision |

---

## Support & Questions

**For token rotation issues:**
- LangSmith: https://docs.smith.langchain.com/
- Context7: Contact support@context7.com
- Pinecone: https://docs.pinecone.io/
- Braintrust: https://docs.braintrustdata.com/
- Greptile: https://docs.greptile.com/

**For security concerns:**
- Review: [SECURITY.md](../SECURITY.md)
- Contact: Security team immediately

**For setup help:**
- See: [ENVIRONMENT_VARIABLES_SETUP.md](./ENVIRONMENT_VARIABLES_SETUP.md)

---

## Completion Checklist

Mark each item as you complete it:

- [ ] LangSmith token rotated and verified
- [ ] Context7 token rotated and verified
- [ ] Pinecone token rotated and verified
- [ ] Braintrust token rotated and verified
- [ ] Greptile token rotated and verified
- [ ] `.env` file updated with all new tokens
- [ ] Gitleaks scan passes (no leaks found)
- [ ] Build verification passes
- [ ] MCP servers connect successfully
- [ ] LangSmith tracing works
- [ ] Git history cleanup decision made
- [ ] Team notified of token rotation
- [ ] Security incident documented

---

**Last Updated:** 2026-05-24T04:28:00Z  
**Next Review:** After all tokens rotated  
**Related:** [SECRETS_AUDIT_REPORT.md](../brain/EPIC-7-QUALITY/SECRETS_AUDIT_REPORT.md)