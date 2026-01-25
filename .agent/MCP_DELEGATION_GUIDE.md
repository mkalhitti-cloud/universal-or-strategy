# MCP DELEGATION GUIDE FOR AGENTS

**Purpose**: Help all agents understand when and how to use the MCP delegation bridge for cost-efficient operations.

---

## Quick Decision Tree

```
Need to do file operations?
    ├─ YES: Can you use Gemini Flash?
    │   ├─ Running in Cursor/Antigravity? → Use native Gemini Flash
    │   ├─ Running in Claude Code CLI? → Use MCP delegation_bridge
    │   └─ Running in Opus/Sonnet? → Delegate to Flash via MCP
    │
    └─ NO: Just use your native tools
```

---

## MCP Delegation Bridge

### What It Does
- Delegates file operations to **Gemini Flash** (99% cheaper than Opus)
- Runs as a separate service accessible via Model Context Protocol
- Handles: file saving, documentation updates, data formatting, code generation
- Reduces cost from ~$0.02 per operation (Opus) to ~$0.0002 (Flash)

### When to Use
| Scenario | Use MCP? | Why |
|----------|----------|-----|
| Writing file from Opus | YES | Save 99% cost |
| Saving JSON status file | YES | Simple operation, Flash handles easily |
| Creating documentation | YES | Flash is good at this |
| Complex debugging | NO | Stay with Opus for reasoning |
| Strategic planning | NO | Stay with Opus/Sonnet |
| Reading files | NO | Use native tools (Read, Glob, Grep) |

### When NOT to Use
- You're already in Gemini Flash (use native tools)
- You're in Sonnet/Opus doing complex reasoning (don't break context)
- You need bidirectional conversation with the model
- The file operation is critical (use native tools for safety)

---

## IDE-Specific Instructions

### Cursor (Gemini Flash native)
**You have Gemini Flash natively - don't use MCP delegation**

```markdown
Since you ARE Gemini Flash, use your native tools:
- Read/Write files directly
- Edit existing files
- Create new files
- No need for MCP bridge
```

### Antigravity (Opus 4.5 primary)
**Recommended: Use MCP delegation to Gemini Flash**

```markdown
You're in Opus, but can delegate file work:

1. Identify file operation: "I need to save this code to file X"
2. Ask for MCP access if needed
3. Use MCP tool: call_gemini_flash("Save this code as UniversalOR_V9.cs", context)
4. Flash handles it, you stay focused on reasoning
```

### Claude Code CLI (Haiku 4.5 native)
**Available but limited - use for simple delegation only**

```markdown
You can access MCP delegation_bridge:

Check if MCP is available:
- Try accessing call_gemini_flash tool
- If available: delegate file ops to Flash
- If not available: use native Read/Write/Edit tools

Known issue: API 400 errors with concurrent tool calls
- Use ONE tool per message only
- Don't batch tool calls
```

---

## How to Use the Delegation Bridge

### Step 1: Check If Available
```
Try to use the tool in your current environment.
If you see "call_gemini_flash" available, proceed.
If not, fall back to native tools.
```

### Step 2: Prepare Your Request
```
Decide what you need:
- File operation: save, update, create
- Format: JSON, Markdown, C#, etc.
- Content: include the actual content to save
```

### Step 3: Call the Bridge
```
call_gemini_flash(
    prompt: "Save this code to file path X",
    context: "actual code content here..."
)
```

### Step 4: Handle the Response
```
Check response:
- Success: File was saved (or operation completed)
- Error: Try with native tools instead
- API Error: Might need to retry or use native approach
```

---

## Example Scenarios

### Scenario 1: Opus Agent Saving JSON Status
**Agent**: V9_001 running in Antigravity (Opus 4.5)

**Task**: Create `.agent/SHARED_CONTEXT/V9_TOS_RTD_STATUS.json` with test results

**Option A: Use MCP Delegation (Cost-Efficient)**
```python
# In Opus (Antigravity)
json_content = """{
  "task_id": "V9_001",
  "status": "COMPLETED",
  "test_result": "PASS",
  ...
}"""

# Delegate to Flash
response = call_gemini_flash(
    prompt="Save this JSON as .agent/SHARED_CONTEXT/V9_TOS_RTD_STATUS.json",
    context=json_content
)
```

**Option B: Use Native Tool (Simpler)**
```python
# Just use Write tool directly
Write(
    file_path=".agent/SHARED_CONTEXT/V9_TOS_RTD_STATUS.json",
    content=json_content
)
```

**Recommendation**: Use native Write tool - simpler and clearer. Only use MCP if you're doing many file operations and want to save costs.

---

### Scenario 2: Cursor Agent Creating Documentation
**Agent**: V9_004 running in Cursor (Gemini Flash)

**Task**: Create comprehensive test report

**What to Do**:
```
You ARE Gemini Flash - don't use MCP delegation.
Use your native Write tool directly:

Write(
    file_path="DEVELOPMENT/V9_WIP/COPY_TRADING/TEST_RESULTS.md",
    content="your documentation..."
)
```

**Why**: You're already Flash. No need to delegate to yourself via MCP.

---

### Scenario 3: Claude Code CLI Agent Saving Multiple Files
**Agent**: Running in Claude Code CLI (Haiku 4.5)

**Task**: Save test results + update session + create report

**What to Do**:
```
Option A: Use native tools (recommended)
- One message: Create JSON file
- Next message: Update session file
- Next message: Create report
Avoids concurrent tool call issues

Option B: Try MCP delegation
- If call_gemini_flash available, use it
- Delegate all file ops to Flash
- Flash handles concurrency better
```

**Recommendation**: Use native tools, one per message. Simpler and avoids API errors.

---

## Cost Comparison

### Operation: Save JSON status file (1 KB)

**Using Opus (native Write tool)**
```
Input: ~100 tokens
Output: ~50 tokens
Cost: ~$0.015 per operation
```

**Using Gemini Flash (via MCP delegation)**
```
Opus delegates to Flash
Flash handles file save
Cost: ~$0.0001 per operation
Savings: 99%+
```

**When it makes sense**:
- Many file operations (>10): Use MCP → Save ~$0.14
- Few operations (1-3): Use native → Simpler, not worth delegating
- Complex reasoning + file saves: Use MCP → Keep Opus focused

---

## Environment Setup

### If MCP Not Working

1. **Check .env file exists**
   ```
   Location: universal-or-strategy/.env
   Should contain: GEMINI_API_KEY or GOOGLE_API_KEY
   ```

2. **Check delegation_bridge.py is accessible**
   ```
   Location: .agent/mcp-servers/delegation_bridge.py
   Should be runnable: python .agent/mcp-servers/delegation_bridge.py
   ```

3. **Check IDE MCP configuration**
   ```
   For Cursor: Settings → MCP settings → add delegation_bridge
   For Antigravity: MCP configuration in settings
   For Claude Code CLI: May not support MCP without config
   ```

4. **Fallback to native tools**
   ```
   If MCP not available, use:
   - Read() for reading files
   - Write() for creating files
   - Edit() for modifying files
   - Glob() for searching
   - Grep() for content search
   ```

---

## Rules for All Agents

1. **Prefer Native Tools When Available**
   - Simpler, clearer code
   - No dependency on MCP setup
   - Easier to debug issues

2. **Use MCP for Cost Optimization** (if already in expensive model)
   - You're in Opus/Sonnet doing heavy reasoning
   - Have many file operations to perform
   - Want to delegate to cheaper Flash model
   - Already have MCP configured

3. **Document What You Did**
   - Note in commit message if you used MCP delegation
   - Update CURRENT_SESSION.md with model usage
   - Help future agents understand your approach

4. **If MCP Fails, Fall Back Gracefully**
   - Don't assume MCP is available
   - Always have a fallback plan
   - Use native tools if MCP isn't working

---

## For Sub-Agents Running in Opus

When V9_001 or V9_002 or V9_003 escalates and needs complex debugging with an Opus agent:

**Recommended Approach**:
```
1. Perform analysis/debugging in Opus (your strength)
2. When you need to save results:
   Option A: Use native Write/Edit tools (simplest)
   Option B: Delegate to Flash for file ops if doing many (saves $)

Example:
- Analyze code issue (Opus) → 5 min complex reasoning
- Save findings to JSON (delegate to Flash) → 30 sec, costs $0.0001
- Update status (delegate to Flash) → 30 sec, costs $0.0001
- Total cost: mostly Opus reasoning, minimal file op cost
```

---

## Summary

| Using | MCP Delegation? | Why |
|-------|-----------------|-----|
| Gemini Flash (Cursor) | NO | Already Flash, use native |
| Opus (Antigravity) | OPTIONAL | Can delegate to save costs |
| Sonnet (Cursor/Antigravity) | OPTIONAL | Can delegate to save costs |
| Claude Code CLI | NO | Simpler to use native tools |
| Haiku (Claude Code CLI) | NO | Simple model, use native |

**Default**: Use native tools (Read, Write, Edit, Glob, Grep) - they're built-in and always available.

**Advanced**: If in Opus/Sonnet and doing heavy file I/O, consider MCP delegation to Flash for cost savings.

---

*All agents should read this before their first file operation.*
