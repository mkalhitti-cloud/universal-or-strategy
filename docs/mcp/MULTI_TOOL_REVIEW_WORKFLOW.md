# Multi-Tool Code Review Workflow

Complete guide to the integrated code review workflow using CodeAnt, Cubic, and Greptile.

## Overview

This project uses a **5-stage review pipeline** that catches issues at every phase of development:

```
Real-Time → Pre-Commit → Pre-Push → Cloud PR → Quality Analysis
(CodeAnt)   (CodeAnt)    (Cubic)     (Cubic)    (Greptile)
```

## Tool Setup Status

### ✅ CodeAnt CLI + MCP
- **Status**: Fully configured and tested
- **Version**: 0.5.1
- **Authentication**: Logged in
- **MCP Server**: Added to `.bob/mcp.json`
- **Extension**: Installed in VS Code

### ⏳ Cubic CLI
- **Status**: Installing (background)
- **Purpose**: Local pre-push review + Cloud PR scoring
- **Authentication**: Pending (will auto-prompt on first run)

### ✅ Greptile MCP
- **Status**: Active
- **Authentication**: Bearer token
- **Purpose**: Code quality scoring

### ❌ Cubic MCP
- **Status**: Disabled (OAuth broken in Bob IDE)
- **Reason**: Bob IDE doesn't support OAuth for `streamable-http` servers
- **Alternative**: Use Cubic CLI instead

## The 5-Stage Workflow

### Stage 1: Real-Time Feedback (While Coding)
**Tool**: CodeAnt Extension

**What it does**:
- Instant feedback as you type
- Static analysis in VS Code
- Highlights issues inline

**When to use**: Always active while coding

---

### Stage 2: Pre-Commit Review
**Tool**: CodeAnt CLI or MCP

**Commands**:
```bash
# Review uncommitted C# changes only
codeant review

# Via Bob IDE MCP
Ask Bob: "Run CodeAnt review on my C# changes"
```

**What it catches** (C# files only):
- Code quality issues
- Security vulnerabilities
- Style violations
- Dead code
- Complexity issues

**Note**: Configured via `.codeantignore` to scan only `*.cs` files (saves tokens)

**Output**: Priority-based issues (MINOR, MAJOR, CRITICAL)

**When to use**: Before every commit

---

### Stage 3: Pre-Push Review (Local PR Simulation)
**Tool**: Cubic CLI

**Commands**:
```bash
# Auto-detect base branch
cubic review --base

# Explicit base branch
cubic review --base main

# Custom focus
cubic review --prompt "check for security issues"
```

**What it catches**:
- Issues missed by CodeAnt
- Cross-file dependencies
- Architecture violations
- Team review patterns (synced from cloud)

**Output**: Priority-based issues (P0, P1, P2)

**When to use**: Before pushing to remote

---

### Stage 4: Cloud PR Review (Official Score)
**Tool**: Cubic GitHub Integration

**How it works**:
- Automatic on PR creation/update
- More thorough than local CLI
- Uses full cloud context

**Output**: 
- Merge confidence score (0-5)
- Detailed issue list
- Team review learnings

**Score meanings**:
- 5/5 = Safe to merge (high confidence)
- 4/5 = Likely safe (minor concerns)
- 3/5 = Review needed (moderate risk)
- 2/5 = Risky (significant issues)
- 1/5 = Dangerous (critical problems)
- 0/5 = Do not merge (blocking issues)

**When to use**: After pushing, before merging

---

### Stage 5: Code Quality Analysis
**Tool**: Greptile MCP

**Commands**:
```
Ask Bob: "Check Greptile score"
Ask Bob: "Show Greptile review issues"
```

**What it provides**:
- Code quality metrics
- Technical debt analysis
- Maintainability score

**When to use**: Final check before merge

---

## Recommended Daily Workflow

### Morning: Start Work
```bash
# Pull latest
git pull origin main

# Check for any pending reviews
Ask Bob: "Check Greptile score on main"
```

### During Development: Iterate
```
1. Code in VS Code (CodeAnt Extension active)
2. See inline feedback
3. Fix issues as you go
```

### Before Commit: Local Review
```bash
# Review C# changes only
codeant review

# Or via Bob
Ask Bob: "Run CodeAnt review on my C# changes"

# Fix issues, then commit
git add .
git commit -m "feat: implement feature X"
```

### Before Push: Pre-Flight Check
```bash
# Run local PR simulation
cubic review --base

# Fix any P0/P1 issues
# Iterate until clean or only disputed issues remain

# Push
git push origin feature-branch
```

### After Push: PR Review
```
1. Create PR on GitHub
2. Wait for Cubic cloud review (automatic)
3. Check merge confidence score
4. Fix any issues Cubic found
5. Push fixes
6. Repeat until score ≥ 4/5
```

### Before Merge: Final Check
```
Ask Bob: "Check Greptile score on PR #123"
Ask Bob: "Show Cubic review issues on PR #123"

# Merge when:
# - Cubic score ≥ 4/5
# - Greptile score acceptable
# - All P0 issues resolved
```

---

## Tool Comparison Matrix

| Feature | CodeAnt Ext | CodeAnt CLI | Cubic CLI | Cubic Cloud | Greptile |
|---------|-------------|-------------|-----------|-------------|----------|
| **Real-time feedback** | ✅ | ❌ | ❌ | ❌ | ❌ |
| **Pre-commit review** | ✅ | ✅ | ❌ | ❌ | ❌ |
| **Pre-push review** | ❌ | ❌ | ✅ | ❌ | ❌ |
| **Merge confidence (0-5)** | ❌ | ❌ | ❌ | ✅ | ❌ |
| **Code quality score** | ❌ | ❌ | ❌ | ❌ | ✅ |
| **MCP integration** | ❌ | ✅ | ❌ | ❌ | ✅ |
| **C# focused** | ✅ | ✅ | ❌ | ❌ | ❌ |
| **AI-driven fixes** | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Team patterns** | ❌ | ❌ | ✅ | ✅ | ❌ |
| **Speed** | Instant | Fast | Fast | Slow | Fast |
| **Thoroughness** | Medium | Medium | Medium | High | High |

---

## Configuration Files

### `.bob/mcp.json`
```json
{
  "mcpServers": {
    "greptile": {
      "url": "https://api.greptile.com/mcp",
      "type": "streamable-http",
      "headers": {
        "Authorization": "Bearer YOUR_TOKEN"
      },
      "disabled": false
    },
    "cubic": {
      "type": "streamable-http",
      "url": "https://www.cubic.dev/api/mcp",
      "oauth": {...},
      "disabled": true  // OAuth broken, use CLI instead
    },
    "codeant": {
      "command": "npx",
      "args": ["-y", "any-cli-mcp-server", "codeant"],
      "disabled": false
    }
  }
}
```

### CodeAnt Authentication
```bash
# Login (already done)
codeant login

# Set GitHub token for PR features
codeant set-token github YOUR_GITHUB_TOKEN

# Check status
codeant --version
```

### Cubic Authentication
```bash
# First run will auto-prompt
cubic

# Or manual auth
cubic auth login
```

---

## Bob IDE Integration

### Available MCP Commands

**CodeAnt**:
- "Run CodeAnt review on my changes"
- "Check for secrets in my code"
- "Run CodeAnt on staged files"

**Greptile**:
- "Check Greptile score"
- "Show Greptile review issues on PR #123"
- "List Greptile findings"

**Cubic** (when CLI completes):
- Use CLI directly in terminal (MCP disabled)

---

## Troubleshooting

### CodeAnt Issues

**Issue**: `codeant: command not found`
**Fix**: 
```bash
npm install -g codeant-cli
# Restart terminal
```

**Issue**: "Not authenticated"
**Fix**:
```bash
codeant logout
codeant login
```

### Cubic Issues

**Issue**: OAuth not working in Bob IDE
**Status**: Known issue - Bob IDE doesn't support OAuth for streamable-http
**Workaround**: Use Cubic CLI instead of MCP

**Issue**: Cubic CLI not installed
**Status**: Installing in background (Terminal 1)
**Action**: Wait for installation to complete

### Greptile Issues

**Issue**: "Unauthorized" error
**Fix**: Check Bearer token in `.bob/mcp.json`

---

## Performance Tips

1. **Use CodeAnt for quick iterations** - Fastest feedback loop
2. **Run Cubic before push** - Catches issues before they hit CI
3. **Don't skip local reviews** - Cloud review is slower and costs API credits
4. **Fix P0 issues immediately** - They'll block your PR anyway
5. **Use `--prompt` for focused reviews** - Faster than full review

---

## Next Steps

1. ✅ CodeAnt is ready to use
2. ⏳ Wait for Cubic CLI installation to complete
3. 🔄 Reload VS Code to activate CodeAnt MCP
4. 🧪 Test the full workflow on your next feature
5. 📊 Track your review scores over time

---

## Support

- **CodeAnt**: https://codeant.ai/docs
- **Cubic**: https://cubic.dev/docs
- **Greptile**: https://greptile.com/docs
- **Bob IDE MCP**: Settings → MCP

---

**Last Updated**: 2026-06-05
**Status**: CodeAnt ✅ | Cubic ⏳ | Greptile ✅