# Environment Variables Setup Guide

**Last Updated:** 2026-05-24T04:28:00Z  
**Related:** EPIC-7-QUALITY-001 (Remove Hardcoded Secrets)

## Overview

This guide explains how to set up environment variables for the V12 Universal OR Strategy project. All API keys and secrets must be stored in a `.env` file (gitignored) and never committed to version control.

## Quick Start

1. **Copy the template:**
   ```powershell
   Copy-Item .env.example .env
   ```

2. **Edit `.env` and add your API keys** (see sections below for where to obtain them)

3. **Verify `.env` is gitignored:**
   ```powershell
   git check-ignore .env
   # Should output: .env
   ```

4. **Never commit `.env`** - It contains your actual secrets

## Required Environment Variables

### MCP Server Configuration

#### GREPTILE_API_KEY
**Purpose:** Authentication for Greptile MCP server (code search and analysis)  
**Where to get it:** https://greptile.com/  
**Format:** `Bearer token` (e.g., `vob20OZM949...`)  
**Used by:** `.bob/mcp.json`

**Setup:**
1. Log in to Greptile dashboard
2. Navigate to API Keys section
3. Generate new API key
4. Add to `.env`:
   ```bash
   GREPTILE_API_KEY=your_token_here
   ```

### Observability & Tracing

#### LANGSMITH_API_KEY
**Purpose:** LangSmith tracing for multi-agent reasoning chains  
**Where to get it:** https://smith.langchain.com/  
**Format:** `lsv2_pt_...`  
**Used by:** Multi-agent workflows, LangSmith bridge

**Setup:**
1. Log in to LangSmith
2. Go to Settings > API Keys
3. Create new API key
4. Add to `.env`:
   ```bash
   LANGSMITH_API_KEY=lsv2_pt_your_key_here
   ```

#### CONTEXT7_API_KEY
**Purpose:** Context7 API for context management  
**Where to get it:** https://context7.com/  
**Format:** `ctx7sk-...`  
**Used by:** Context management utilities

**Setup:**
1. Sign up at Context7
2. Generate API key from dashboard
3. Add to `.env`:
   ```bash
   CONTEXT7_API_KEY=ctx7sk-your_key_here
   ```

#### PINECONE_API_KEY
**Purpose:** Pinecone vector database for embeddings  
**Where to get it:** https://www.pinecone.io/  
**Format:** `pcsk_...`  
**Used by:** Vector search and embeddings

**Setup:**
1. Create Pinecone account
2. Create new project
3. Copy API key from project settings
4. Add to `.env`:
   ```bash
   PINECONE_API_KEY=pcsk_your_key_here
   ```

#### BRAINTRUST_API_KEY
**Purpose:** Braintrust evaluation platform  
**Where to get it:** https://www.braintrustdata.com/  
**Format:** `sk-...`  
**Used by:** Model evaluation and testing

**Setup:**
1. Sign up at Braintrust
2. Navigate to API Keys
3. Generate new key
4. Add to `.env`:
   ```bash
   BRAINTRUST_API_KEY=sk-your_key_here
   ```

## Optional Environment Variables

### LANGSMITH_TRACING
**Purpose:** Enable/disable LangSmith tracing  
**Default:** `false`  
**Values:** `true` or `false`

```bash
LANGSMITH_TRACING=true
```

### LANGSMITH_PROJECT
**Purpose:** LangSmith project name  
**Default:** `V12-Universal-OR-Strategy`

```bash
LANGSMITH_PROJECT=V12-Universal-OR-Strategy
```

### DROID_BIN_PATH
**Purpose:** Override path to droid CLI binary  
**Default:** `%USERPROFILE%\bin\droid`

```bash
DROID_BIN_PATH=C:\custom\path\to\droid
```

### NT_DOCUMENTS_PATH
**Purpose:** Override NinjaTrader documents path  
**Default:** `%USERPROFILE%\Documents\NinjaTrader 8`

```bash
NT_DOCUMENTS_PATH=C:\custom\path\to\NinjaTrader 8
```

### V12_IPC_PORT
**Purpose:** IPC server port for Control Surface app  
**Default:** `5001`

```bash
V12_IPC_PORT=5001
```

## Verification

### Check Environment Variables are Loaded

**PowerShell:**
```powershell
# Check if .env exists
Test-Path .env

# Verify a variable is set (without revealing the value)
if ($env:GREPTILE_API_KEY) { "✓ GREPTILE_API_KEY is set" } else { "✗ GREPTILE_API_KEY is missing" }
```

**Bash/WSL:**
```bash
# Check if .env exists
[ -f .env ] && echo "✓ .env exists" || echo "✗ .env missing"

# Verify a variable is set
[ -n "$GREPTILE_API_KEY" ] && echo "✓ GREPTILE_API_KEY is set" || echo "✗ GREPTILE_API_KEY is missing"
```

### Run Gitleaks Scan

Verify no secrets are committed:
```powershell
gitleaks detect --source . --verbose --no-git
# Expected output: "no leaks found"
```

## Security Best Practices

1. **Never commit `.env`** - It's in `.gitignore` for a reason
2. **Rotate tokens regularly** - Especially after team member changes
3. **Use different tokens per environment** - Dev, staging, production
4. **Limit token permissions** - Only grant necessary scopes
5. **Monitor token usage** - Check for unauthorized access
6. **Revoke compromised tokens immediately** - See Token Rotation Guide

## Troubleshooting

### "Environment variable not found" errors

**Symptom:** Application fails with missing environment variable error

**Solution:**
1. Verify `.env` file exists in project root
2. Check variable name matches exactly (case-sensitive)
3. Restart your terminal/IDE to reload environment
4. Verify no extra spaces around `=` in `.env`

### MCP Server Authentication Failures

**Symptom:** Greptile MCP server fails to connect

**Solution:**
1. Verify `GREPTILE_API_KEY` is set in `.env`
2. Check token hasn't expired
3. Verify token has correct permissions
4. Test token directly via Greptile API

### LangSmith Tracing Not Working

**Symptom:** No traces appear in LangSmith dashboard

**Solution:**
1. Verify `LANGSMITH_TRACING=true` in `.env`
2. Check `LANGSMITH_API_KEY` is valid
3. Verify `LANGSMITH_PROJECT` matches your project name
4. Run test: `python scripts/langsmith_bridge.py --test`

## Related Documentation

- [Token Rotation Instructions](./TOKEN_ROTATION_INSTRUCTIONS.md)
- [Secrets Audit Report](../brain/EPIC-7-QUALITY/SECRETS_AUDIT_REPORT.md)
- [MCP Configuration](./MCP_CONFIGURATION.md)
- [Security Policy](../SECURITY.md)

## Support

For issues with:
- **Environment setup:** Check this guide and `.env.example`
- **Token rotation:** See [TOKEN_ROTATION_INSTRUCTIONS.md](./TOKEN_ROTATION_INSTRUCTIONS.md)
- **Security concerns:** Contact security team immediately
- **API key issues:** Contact respective service provider

---

**Remember:** Your `.env` file contains sensitive credentials. Treat it like a password file.