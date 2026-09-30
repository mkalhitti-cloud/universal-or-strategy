# Cubic MCP Troubleshooting Guide

## Error: "Unauthorized: valid MCP authentication required"

**Symptom**: Red dot on Cubic in Bob IDE MCP settings, error code -32000

**Root Cause**: OAuth authentication not completed

**Solution**:

### Step 1: Verify Configuration
Check `.bob/mcp.json` contains:
```json
{
  "mcpServers": {
    "cubic": {
      "type": "streamable-http",
      "url": "https://www.cubic.dev/api/mcp",
      "oauth": {
        "provider": "cubic",
        "authUrl": "https://www.cubic.dev/oauth/authorize",
        "tokenUrl": "https://www.cubic.dev/oauth/token",
        "scopes": ["read", "write"]
      },
      "disabled": false,
      "alwaysAllow": []
    }
  }
}
```

### Step 2: Restart Bob IDE
1. Close Bob IDE completely (File → Exit)
2. Reopen Bob IDE
3. Navigate to Settings → MCP

### Step 3: Complete OAuth Flow

**Option A: Automatic Prompt**
- Bob IDE may automatically open browser to https://www.cubic.dev/oauth/authorize
- Login with your Cubic account
- Click "Authorize" to grant access
- Browser redirects back to Bob IDE
- Cubic dot turns green

**Option B: Manual Authorization**
1. In Settings → MCP, click on "cubic" entry
2. Look for "Connect" or "Authorize" button
3. Click button to initiate OAuth flow
4. Follow browser prompts
5. Return to Bob IDE

**Option C: Manual Token (Fallback)**
If OAuth UI doesn't appear:
1. Visit https://www.cubic.dev/settings/api
2. Generate an API token
3. Update `.bob/mcp.json`:
```json
"cubic": {
  "type": "streamable-http",
  "url": "https://www.cubic.dev/api/mcp",
  "headers": {
    "Authorization": "Bearer YOUR_TOKEN_HERE"
  }
}
```
4. Restart Bob IDE

### Step 4: Verify Connection
After OAuth completes:
- Cubic dot should turn green in Settings → MCP
- Test with: "Show cubic review issues on PR #5"
- Should return merge confidence score (X/5)

## Common Issues

### Issue: OAuth popup blocked
**Solution**: Check browser popup blocker, allow popups from Bob IDE

### Issue: OAuth redirect fails
**Solution**: 
1. Check firewall/antivirus isn't blocking localhost redirects
2. Try manual token method (Option C above)

### Issue: Token expires
**Symptom**: Green dot turns red after working previously
**Solution**: Re-run OAuth flow (Step 3)

## Cubic MCP Capabilities

Once connected, Cubic provides 11 tools:
1. `list_pull_requests` - List PRs in repo
2. `get_pull_request` - Get PR details
3. `create_pull_request` - Create new PR
4. `update_pull_request` - Update PR metadata
5. `merge_pull_request` - Merge PR
6. `get_merge_confidence` - Get merge confidence score (X/5)
7. `list_reviews` - List PR reviews
8. `create_review` - Submit review
9. `get_diff` - Get PR diff
10. `list_commits` - List PR commits
11. `get_commit` - Get commit details

**Key Tool**: `get_merge_confidence` returns score 0-5:
- 5/5 = Safe to merge (high confidence)
- 4/5 = Likely safe (minor concerns)
- 3/5 = Review needed (moderate risk)
- 2/5 = Risky (significant issues)
- 1/5 = Dangerous (critical problems)
- 0/5 = Do not merge (blocking issues)

## Integration with /mcp-loop

Once Cubic is working, it integrates into the nested loop architecture:

```
/local-loop (8 min)
  ↓ PASS
/mcp-loop (3 min)
  ├─ Greptile: Code quality score (X/5)
  └─ Cubic: Merge confidence (Y/5)
  ↓ Both ≥ 4/5
/pr-loop (GitHub bots)
```

Target: Both Greptile and Cubic scores ≥ 4/5 before pushing to GitHub.

## Support

If issues persist:
1. Check Bob IDE logs: Help → Toggle Developer Tools → Console
2. Check Cubic status: https://status.cubic.dev
3. Report to Bob IDE: Help → Report Issue
4. Document in: `docs/mcp/cubic_oauth_issue_[date].md`