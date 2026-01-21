---
name: opus-deployment-guide
description: Deployment options for Opus-generated code in Antigravity IDE. Includes automatic MCP deployment, Haiku fallback, manual copy/paste, and instructions for each IDE setup.
---

# Opus Deployment Guide

## Problem: Code Implementation ≠ Automatic Deployment

When Opus in Antigravity IDE generates code, it cannot automatically deploy without one of these options.

**Universal Path:** `${PROJECT_ROOT}`
**Executors:** ${BRAIN} (Reasoning), ${HANDS} (Gemini Flash via delegation_bridge)

---

## Option 1: Use MCP Bridge (RECOMMENDED - Fully Automated)

### What It Does
Opus calls `call_gemini_flash()` via MCP server → Gemini Flash deploys automatically

### When to Use
- You want fully automated deployment
- You have 10 minutes to set up MCP
- You want 99.9% cost savings on deployment

### Setup Instructions

**Step 1: Verify MCP Server Available**
- Antigravity IDE → Settings → Check MCP servers
- Look for: `delegation_bridge` server

**Step 2: Configure MCP (if not found)**
- Install: MCP server bundle from `.agent/mcp-servers/`
- Add to Antigravity settings:
  ```json
  {
    "mcp_servers": {
      "delegation_bridge": {
        "command": "node path-to-mcp-bridge/index.js",
        "env": {
          "GEMINI_API_KEY": "your-google-ai-key",
          "MODEL": "gemini-2.0-flash"
        }
      }
    }
  }
  ```

**Step 3: Test the Connection**
- In Antigravity, ask Opus:
  ```
  "Test MCP deployment: call_gemini_flash(context: 'test', action: 'test')"
  ```
- Verify call succeeded

**Step 4: Use in Prompts**
Add to Opus prompt:
```markdown
## Deployment
After implementation is complete, call:
call_gemini_flash(
  context: "Deploy V8_9_MOMO.cs to both project repo and NinjaTrader bin. Update CHANGELOG.md.",
  code: [full file content],
  action: "deploy"
)
```

### Cost
- $0.0001 per deployment (99.9% cheaper than Opus doing it)
- Typical: $0.0001 - $0.0005 per deployment

### Pros
✅ Fully automated (no manual steps)
✅ No copy/paste required
✅ Lowest cost per deployment
✅ Updates docs automatically
✅ Integrates with CI/CD pipelines
✅ Deployment history logged

### Cons
⚠️ Requires MCP setup (one-time, 10 minutes)
⚠️ Requires GEMINI_API_KEY in environment
⚠️ Gemini Flash must be available
⚠️ Network dependency

---

## Option 2: Manual Handoff to Haiku (Fast Setup - Partially Automated)

### What It Does
Opus returns code → You copy/paste to Claude Code CLI → Haiku deploys

### When to Use
- You want quick setup (no MCP config)
- You're okay with 2-minute manual steps
- You prefer Claude Code CLI deployment
- MCP is unavailable

### Workflow

**Step 1: Get Code from Opus**
```
Opus: [Returns V8_9_MOMO.cs full file content]
```

**Step 2: Copy File Content**
- Select all Opus output (Ctrl+A)
- Copy to clipboard (Ctrl+C)

**Step 3: Paste into Claude Code CLI**
```
[In Claude Code CLI terminal]
You: "Deploy this code to my NinjaTrader project:

[Paste V8_9_MOMO.cs content here]

Deploy to:
- ${PROJECT_ROOT}/UniversalORStrategyV8_9_MOMO.cs
- NinjaTrader bin folder: C:/Users/[USERNAME]/Documents/NinjaTrader 8/bin/Custom/Strategies/
- Update CHANGELOG.md with: 'V8.9 MOMO - Added momentum trade setup'
"

Sonnet: [Spawns Haiku sub-agent]
Haiku: [Handles deployment]
```

**Step 4: Verify Deployment**
- Check Claude Code CLI output for success message
- Verify files exist in both locations
- Check CHANGELOG.md was updated

### Cost
- $0.004 per deployment (40x cheaper than Opus deploying)
- Typical: $0.003 - $0.006 per deployment

### Pros
✅ Works immediately (no setup required)
✅ No environment variables needed
✅ Uses cheap Claude Code CLI (Sonnet/Haiku)
✅ Familiar workflow (copy/paste)
✅ Good fallback for MCP issues
✅ Works from any terminal

### Cons
⚠️ Manual copy/paste required (2 minutes)
⚠️ More steps than Option 1
⚠️ Still more expensive than MCP option
⚠️ Requires Claude Code CLI running
⚠️ Requires Anthropic API credits

---

## Option 3: Manual File Operations (No Setup - Fully Manual)

### What It Does
Opus returns code → You save files manually → You update NinjaTrader manually

### When to Use
- MCP unavailable and Claude Code CLI down
- Emergency situations only
- Last resort / backup plan
- Network connectivity issues

### Workflow

**Step 1: Copy Code from Opus**
- Select all Opus output (Ctrl+A)
- Copy to clipboard (Ctrl+C)

**Step 2: Save to Project Repo**
```
File → Save As
Location: ${PROJECT_ROOT}/UniversalORStrategyV8_9_MOMO.cs
Format: UTF-8 (no BOM)
```

**Step 3: Save to NinjaTrader Bin**
```
File → Save As
Location: C:/Users/${USERNAME}/Documents/NinjaTrader 8/bin/Custom/Strategies/UniversalORStrategyV8_9_MOMO.cs
Format: UTF-8 (no BOM)
```

**Step 4: Compile in NinjaTrader**
- Open NinjaTrader Cbi Editor
- Load the saved strategy file
- Verify compilation succeeds
- Fix any compilation errors

**Step 5: Update CHANGELOG**
- Open CHANGELOG.md in text editor
- Add entry under [Unreleased]:
  ```markdown
  - V8.9 MOMO - Added momentum trade setup
  - Fixed trailing stop logic
  - Improved entry conditions
  ```
- Save file

**Step 6: Verify Deployment**
- Check files exist in both locations
- Verify file sizes match
- Test strategy in NinjaTrader sandbox mode

### Cost
- $0.12 (Opus code only, no deployment automation)
- Plus: Your time (estimated 3-5 minutes)
- Total cost per deployment: $0.12 + 3-5 min of labor

### Pros
✅ Works with absolutely no setup
✅ Full manual control
✅ No dependencies on other tools
✅ Works in emergency situations
✅ All code in your hands (air-gapped safe)
✅ Good for learning/debugging

### Cons
❌ Very manual (3-5 minutes per deployment)
❌ Error-prone (easy to save wrong location)
❌ No automation or verification
❌ Most time-consuming option
❌ Easy to forget docs update
❌ No deployment history

---

## Recommended Strategy by IDE/Setup

| Scenario | Recommended Option | Setup Time | Deployment Time | Cost | Reliability |
|----------|-------------------|-----------|-----------------|------|-------------|
| **Antigravity + MCP configured** | Option 1: MCP Bridge | 0 min | 0 min | $0.0001 | 99.9% |
| **Antigravity + no MCP** | Option 2: Haiku Handoff | 0 min | 2 min | $0.004 | 99% |
| **Cursor with Gemini Flash** | Switch to Gemini | 1 min | 0 min | $0.0001 | 99% |
| **Emergency / offline** | Option 3: Manual | 0 min | 5 min | $0.12+time | 85% |
| **Development/Learning** | Option 3: Manual | 0 min | 5 min | $0.12+time | 85% |

---

## Quick Deployment Decision Tree

```
START: Opus has generated code

1. Do you have MCP bridge configured?
   ├─ YES → Use Option 1 (MCP Bridge)
   │  └─ Cost: $0.0001, Time: 0 min, Effort: None
   │
   └─ NO, is Claude Code CLI available?
      ├─ YES → Use Option 2 (Haiku Handoff)
      │  └─ Cost: $0.004, Time: 2 min, Effort: Copy/paste
      │
      └─ NO → Use Option 3 (Manual)
         └─ Cost: $0.12+time, Time: 5 min, Effort: Full manual

```

---

## Automation Levels

### Level 3 (IDEAL - MCP Bridge)
```
Opus generates code
  ↓
Opus calls MCP (call_gemini_flash)
  ↓
Gemini Flash receives deployment request
  ↓
Gemini deploys to all locations automatically
  ↓
Deployment complete - Email/Slack notification sent

Cost: $0.0001
Time: 0 minutes (fully automated)
Effort: Zero (set and forget)
```

### Level 2 (GOOD - Haiku Handoff)
```
Opus generates code
  ↓
You copy/paste to Claude Code CLI
  ↓
Sonnet spawns Haiku sub-agent
  ↓
Haiku handles multi-file deployment
  ↓
Deployment complete - Claude Code CLI confirms

Cost: $0.004
Time: 2 minutes (mostly manual)
Effort: Copy/paste (low)
```

### Level 1 (OKAY - Manual)
```
Opus generates code
  ↓
You save file to project repo
  ↓
You save file to NinjaTrader bin
  ↓
You compile in NinjaTrader
  ↓
You manually update CHANGELOG
  ↓
Deployment complete - Manual verification

Cost: $0.12 + your time
Time: 5 minutes (fully manual)
Effort: High (all manual steps)
```

---

## Cost Comparison

### Scenario: Deploy 10 Strategies in a Week

| Option | Deployment Cost | Time Investment | Total Cost (time = $50/hr) | Final Cost |
|--------|-----------------|-----------------|---------------------------|-----------|
| **Option 1: MCP** | 10 × $0.0001 = $0.001 | 0 hours | $0 | **$0.001** |
| **Option 2: Haiku** | 10 × $0.004 = $0.04 | 20 minutes = 0.33 hrs | $16.50 | **$16.54** |
| **Option 3: Manual** | 10 × $0.12 = $1.20 | 50 minutes = 0.83 hrs | $41.50 | **$42.70** |

**Savings by using Option 1:** $42.69 per 10 deployments

---

## Troubleshooting

### MCP Not Working?

**Symptom:** `call_gemini_flash() failed: MCP server unavailable`

**Solution:**
1. Check MCP server status in Antigravity IDE
   ```
   Settings → MCP Servers → Check `delegation_bridge` status
   ```
2. Verify GEMINI_API_KEY environment variable is set
   ```
   echo $GEMINI_API_KEY  (Linux/Mac)
   echo %GEMINI_API_KEY%  (Windows)
   ```
3. Try restarting Antigravity IDE
4. Check network connectivity to Google AI API
5. Fall back to Option 2 (Haiku Handoff)

**Prevention:**
- Keep MCP server running before using Option 1
- Test MCP connection weekly
- Have API key backed up elsewhere

---

### Haiku Not Responding?

**Symptom:** `Claude Code CLI timeout waiting for Haiku response`

**Solution:**
1. Verify Claude Code CLI is running
   ```
   ps aux | grep claude-code  (Linux/Mac)
   tasklist | findstr claude  (Windows)
   ```
2. Check Anthropic API credits available
   ```
   claude-code status
   ```
3. Check network connectivity
4. Try Option 3 (Manual deployment) while investigating
5. Restart Claude Code CLI

**Prevention:**
- Keep Claude Code CLI running in background
- Check credits regularly
- Monitor Claude Code logs for errors

---

### Manual Deployment Failed?

**Symptom:** Files saved but compilation fails in NinjaTrader

**Solution:**
1. Verify file paths are correct
   ```
   Project repo: c:\[PROJECT_ROOT]\UniversalORStrategyV8_9_MOMO.cs
   NinjaTrader: c:\Users\[USERNAME]\Documents\NinjaTrader 8\bin\Custom\Strategies\UniversalORStrategyV8_9_MOMO.cs
   ```
2. Check file was saved (not just open)
   - Look at file modification time
   - Verify file size > 0 bytes
3. Verify NinjaTrader bin folder exists
   - If not: Create `Documents\NinjaTrader 8\bin\Custom\Strategies\`
4. Check for encoding issues (should be UTF-8)
5. Run NinjaTrader compiler on the file:
   - Tools → Compile → Select your strategy file
6. Check CHANGELOG.md was updated
   - Open file and verify entry exists

**Prevention:**
- Use Option 1 or 2 to avoid manual errors
- Test file saves with `ls -la filename`
- Always verify CHANGELOG before closing IDE

---

## Integration with IDE Workflows

### In Antigravity IDE (Option 1)

**Prompt Template:**
```markdown
## Task: Implement [feature] in NinjaTrader Strategy

### Requirements
- Language: C#
- Target: NinjaTrader 8
- Files: ${PROJECT_ROOT}/UniversalORStrategyV[version].cs
- Dependencies: [list any]

### Implementation
[Your detailed requirements]

### Deployment
After implementation is complete, please deploy using:

call_gemini_flash(
  context: "Deploy UniversalORStrategyV[version].cs to:
    1. ${PROJECT_ROOT}/UniversalORStrategyV[version].cs
    2. C:/Users/[USERNAME]/Documents/NinjaTrader 8/bin/Custom/Strategies/
    3. Update CHANGELOG.md with: '[feature description]'",
  code: [full file content],
  action: "deploy"
)
```

### In Claude Code CLI (Option 2)

**Prompt Template:**
```markdown
Deploy this NinjaTrader strategy code to my project.

[Code here]

## Deployment Details
- File name: UniversalORStrategyV[version].cs
- Destination 1: ${PROJECT_ROOT}/UniversalORStrategyV[version].cs
- Destination 2: C:/Users/[USERNAME]/Documents/NinjaTrader 8/bin/Custom/Strategies/
- Update CHANGELOG.md with: "[deployment notes]"
- Also check compilation in NinjaTrader if possible
```

### Manual Workflow (Option 3)

**Checklist:**
```markdown
[ ] Code copied from Opus
[ ] File saved to project repo
[ ] File saved to NinjaTrader bin
[ ] File compiled successfully in NinjaTrader
[ ] CHANGELOG.md updated
[ ] File sizes verified in both locations
[ ] Strategy tested in sandbox mode
```

---

## Adding This to Project Prompts

When asking Opus to implement code, include:

```markdown
## Deployment Instructions

After completing implementation, you have three options:

### Option 1 (Recommended): MCP Bridge
If MCP is configured, call:
call_gemini_flash(
  context: "Deploy ${FILENAME} to project and NinjaTrader bin",
  code: [full file content],
  action: "deploy"
)

### Option 2 (Fast Setup): Manual Handoff to Haiku
If MCP unavailable:
1. Return complete file
2. User copies to Claude Code CLI
3. Haiku handles deployment

### Option 3 (Emergency): Manual
If both unavailable:
1. Return complete file
2. User saves files manually
3. User updates CHANGELOG
4. User verifies in NinjaTrader

File should be: UniversalORStrategyV[version].cs (~4000 lines)
Ready for: NinjaTrader 8 compilation
Destinations:
  - ${PROJECT_ROOT}/UniversalORStrategyV[version].cs
  - C:/Users/[USERNAME]/Documents/NinjaTrader 8/bin/Custom/Strategies/
```

---

## When to Use Each Option

### Use Option 1 (MCP Bridge) When:
- MCP is already configured and tested
- You deploy frequently (3+ times per week)
- You want fully automated workflow
- You have Gemini API access
- Cost is a priority
- You have good internet connectivity

### Use Option 2 (Haiku Handoff) When:
- MCP is down or unavailable
- You deploy occasionally (1-2 times per week)
- You want minimal setup
- Claude Code CLI is running
- You have Anthropic API credits
- You're comfortable with copy/paste

### Use Option 3 (Manual) When:
- Both Option 1 and 2 are unavailable
- It's an emergency situation
- You're learning/debugging
- You want full control and transparency
- Network is unstable
- You need to verify each step manually

---

## Best Practices

### For Reliable Deployments

**Do:**
- ✅ Test each option once to understand the workflow
- ✅ Use Option 1 for production deployments
- ✅ Keep MCP server monitoring (health checks)
- ✅ Document deployment history
- ✅ Verify files in both locations after deploy
- ✅ Test code in sandbox before live trading

**Don't:**
- ❌ Mix deployment options in same day
- ❌ Skip CHANGELOG updates
- ❌ Deploy directly to NinjaTrader without saving to repo first
- ❌ Use Option 3 for routine deployments
- ❌ Forget to verify compilation succeeds
- ❌ Deploy untested code to live accounts

### For Cost Optimization

| Scenario | Recommended | Reason |
|----------|-------------|--------|
| Frequent deployer (10+/week) | Option 1 | Saves $400+/month |
| Moderate deployer (3-5/week) | Option 1 | Setup pays for itself in 2 weeks |
| Occasional deployer (1-2/week) | Option 2 | Option 1 setup not worth it |
| Emergency/Backup | Option 3 | No ongoing costs |

---

## Monitoring & Health Checks

### Weekly Deployment System Check

```markdown
[ ] MCP server running? (if using Option 1)
    - Check: Antigravity IDE → Settings → MCP Servers
    - Status: Running

[ ] Claude Code CLI running? (if using Option 2)
    - Check: claude-code status
    - Status: Ready

[ ] API keys configured?
    - GEMINI_API_KEY set? (Option 1)
    - Anthropic API key valid? (Option 2)

[ ] Network connectivity?
    - Can ping Google AI API
    - Can ping Anthropic API

[ ] Test deployment?
    - Option 1: Test with dummy call
    - Option 2: Test with dummy file
    - Option 3: Save test file, verify
```

---

## Related Skills

- [ninjatrader-strategy-dev](../ninjatrader-strategy-dev/SKILL.md) - NinjaTrader strategy development
- [wearable-project](../antigravity-core/wearable-project.md) - Portability standards

---

## FAQ

### Q: Which option should I start with?
**A:** Start with Option 2 (Haiku Handoff). No setup required, minimal cost, familiar workflow. If you deploy frequently, invest 10 minutes in Option 1 later.

### Q: Can I switch between options?
**A:** Yes, absolutely. Each option is independent. You can use Option 1 today and Option 2 tomorrow.

### Q: What if Option 1 fails mid-deployment?
**A:** Gemini Flash will report the error. Fall back to Option 2 or 3. Check MCP server logs for details.

### Q: How do I know which option is active?
**A:** Check your IDE settings:
- Antigravity: Settings → MCP Servers
- Claude Code CLI: `claude-code status`
- Manual: No setup, always available

### Q: Can I automate Option 2 further?
**A:** Yes! Create a shell script/batch file that pipes Opus output directly to Claude Code CLI. This makes Option 2 nearly as fast as Option 1.

### Q: What's the maximum file size for deployment?
**A:** All options support files up to:
- Option 1 (MCP): ~100KB (typical strategy ~10-50KB)
- Option 2 (Claude Code): ~5MB per deployment
- Option 3 (Manual): Unlimited (file system dependent)

### Q: Do I need to update version numbers when deploying?
**A:** Recommended but not required. If you do:
- Update filename: `V8_9_MOMO.cs`
- Update CHANGELOG.md
- Update version constant in code: `VERSION = "8.9"`

---

## Quick Reference Card

```
OPUS DEPLOYMENT OPTIONS

┌─────────────────────────────────────────────────────────┐
│ Option 1: MCP Bridge (BEST)                             │
├─────────────────────────────────────────────────────────┤
│ Setup: 10 min (one-time)                                │
│ Deployment: 0 min (automatic)                           │
│ Cost: $0.0001 per deploy                                │
│ Trigger: call_gemini_flash()                            │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│ Option 2: Haiku Handoff (RECOMMENDED)                   │
├─────────────────────────────────────────────────────────┤
│ Setup: 0 min (no setup)                                 │
│ Deployment: 2 min (copy/paste)                          │
│ Cost: $0.004 per deploy                                 │
│ Trigger: Claude Code CLI paste                          │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│ Option 3: Manual (FALLBACK)                             │
├─────────────────────────────────────────────────────────┤
│ Setup: 0 min (no setup)                                 │
│ Deployment: 5 min (manual)                              │
│ Cost: $0.12 + your time                                 │
│ Trigger: Manual file operations                         │
└─────────────────────────────────────────────────────────┘
```

