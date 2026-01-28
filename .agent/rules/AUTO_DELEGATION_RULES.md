# Automatic MCP Delegation Rules
## For Claude Code CLI with Gemini Flash MCP Bridge

**Effective Date**: 2026-01-28
**Status**: ACTIVE
**Auto-Delegation Enabled**: YES

---

## Quick Rules

1. **ALWAYS delegate these tasks to Gemini Flash via MCP**:
   - ✅ Saving files (Write, Edit operations)
   - ✅ Deploying code (especially dual-deployment to NinjaTrader)
   - ✅ Updating documentation (CHANGELOG.md, README.md, etc.)
   - ✅ Creating/updating state files (.agent/state/*.json)
   - ✅ Routine file operations (copying, listing, verification)

2. **NEVER delegate these tasks** (keep in current model):
   - ❌ Code logic & implementation (requires reasoning)
   - ❌ Debugging complex issues (requires deep analysis)
   - ❌ User interaction & clarifications
   - ❌ Strategic decisions & planning
   - ❌ Complex file reading/analysis

3. **Cost Comparison** (Why delegate):
   ```
   File save operation:
   - Using Haiku (current): $0.0025 (250 tokens input)
   - Using Gemini Flash (delegated): $0.000019 (250 tokens input)
   - Savings per operation: 99%

   For 100 file operations:
   - Current: $0.25
   - Delegated: $0.0019
   - Total savings: $0.25 (enough for 5+ feature implementations!)
   ```

---

## How to Auto-Delegate

### Method 1: Use call_gemini_flash Tool (Recommended)

When you need to save a file, UPDATE CONTEXT to include state, or deploy code:

```python
call_gemini_flash(
    context: "Your task description here",
    code: "Optional file content",
    action: "deploy|update_docs|read_file|analyze"
)
```

**Example - Saving a file**:
```python
call_gemini_flash(
    context: "Save this C# code as UniversalORStrategyV9_NEW.cs to project root and deploy to NinjaTrader",
    code: """// C# strategy code here...
public class MyStrategy { ... }""",
    action: "deploy"
)
```

**Example - Updating documentation**:
```python
call_gemini_flash(
    context: "Update .agent/PROJECT_STATE.md with current session progress",
    code: """# Current Session State
Status: Feature implementation in progress
Next step: Testing
""",
    action: "update_docs"
)
```

### Method 2: Native Write/Edit Tools (Fallback Only)

If `call_gemini_flash` is unavailable, use native tools:

```python
Write(
    file_path="/absolute/path/to/file.cs",
    content="file content"
)
```

---

## Task Type Decision Matrix

| Task | Delegate? | Tool | Cost | Notes |
|------|-----------|------|------|-------|
| Save .cs strategy file | ✅ YES | `call_gemini_flash` | $0.0001 | Use action="deploy" |
| Update CHANGELOG.md | ✅ YES | `call_gemini_flash` | $0.00005 | Use action="update_docs" |
| Deploy to NinjaTrader | ✅ YES | `call_gemini_flash` | $0.0001 | Dual-location deployment |
| Read strategy file | ❌ NO | Use `Read()` | $0 | Free operation |
| Parse JSON config | ❌ NO | Use native Python | $0 | Keep context in model |
| Fix bug in code | ❌ NO | Current model | $0.15 | Complex logic needed |
| Update session state | ✅ YES | `call_gemini_flash` | $0.00005 | Use action="update_docs" |
| Create test file | ✅ YES | `call_gemini_flash` | $0.0001 | Use action="deploy" |
| Ask user question | ❌ NO | Current model | $0.01 | User interaction only |

---

## Auto-Delegation Workflow

### Every Conversation in Claude Code CLI:

1. **Read this file** (you're reading it now!) ✓
2. **Check if file operation needed** - If yes, proceed to step 3
3. **Use `call_gemini_flash`** with appropriate action:
   - `action="deploy"` → for saving/deploying code
   - `action="update_docs"` → for documentation/state updates
   - `action="read_file"` → for reading many files
   - `action="analyze"` → for analyzing content
4. **Receive response** from Gemini Flash with completion confirmation
5. **Continue with reasoning/coding** in current model (Haiku)

### Cost Tracking

Each delegation logs to `.agent/state/cost_tracking.json`:
```json
{
  "total_cost": 0.00019,
  "delegations": [
    {
      "action": "deploy",
      "context": "Save strategy file",
      "cost_estimate": 0.0001,
      "timestamp": "2026-01-28T19:45:00Z"
    }
  ]
}
```

---

## MCP Bridge Status

**Server**: `delegation_bridge` (Python + Gemini Flash 3.0)
**Location**: `.agent/mcp-servers/delegation_bridge.py`
**Status**: ✅ ACTIVE & CONFIGURED
**Config**: `claude.json` in project root
**State**: `.agent/state/` directory

### Check MCP Status

If delegation isn't working:

1. Verify `.env` has `GEMINI_API_KEY` set
2. Check `.agent/state/errors.log` for recent errors
3. Review `.agent/state/session_state.json` for `mcp_status`
4. Fall back to native Read/Write tools if needed

---

## Examples from Real Tasks

### Example 1: Deploy Strategy Update

**Scenario**: User wants to save a new strategy version

```markdown
User: "Save V9_NEW.cs to the project"

You (Haiku in Claude Code CLI):
"I'll delegate the file save to Gemini Flash for cost optimization..."

call_gemini_flash(
    context: "Save UniversalORStrategyV9_NEW.cs to project root ${PROJECT_ROOT}/. Also deploy to NinjaTrader bin folder at C:/Users/${USERNAME}/Documents/NinjaTrader 8/bin/Custom/Strategies/. Update CHANGELOG.md with version note.",
    code: "[full C# code content]",
    action: "deploy"
)

[Gemini Flash responds with confirmation]

Gemini Flash: "✓ File saved to project\n✓ Deployed to NinjaTrader\n✓ CHANGELOG updated\nCost: $0.0001"

You: "Done! Code is saved and ready for testing. Cost: $0.0001 (99% cheaper than if I did it)"
```

### Example 2: Update Session State

**Scenario**: Need to save progress between conversations

```markdown
You: "Updating session state before context exhaustion..."

call_gemini_flash(
    context: "Update .agent/state/session_state.json with in_progress status",
    code: """
{
  "in_progress": {
    "task": "V9_RTD_Bridge - TOS connection fix",
    "status": "debugging_in_progress",
    "last_checkpoint": "Analyzed RTD subscription protocol",
    "next_step": "Test with live market data"
  }
}
    """,
    action: "update_docs"
)

Gemini Flash: "✓ session_state.json updated\nCost: $0.00005"

[User closes Claude Code CLI]
[User opens new conversation]

New Agent: "Reading previous session state..."
call_gemini_flash(
    context: "Read .agent/state/session_state.json to understand previous progress",
    action: "read_file"
)

Gemini Flash: "[Returns full session state from previous conversation]"

New Agent: "I can see where we left off. Let's continue debugging the RTD connection..."
```

---

## Environment Variable Resolution

The MCP bridge automatically resolves these variables in file paths:

- `${PROJECT_ROOT}` → `/home/user/universal-or-strategy`
- `${USERNAME}` → Your OS username
- `${HOME}` → Your home directory
- `${PLATFORM}` → linux/windows/darwin

Example:
```python
context: "Save to ${PROJECT_ROOT}/UniversalORStrategyV9.cs"
# Resolves to: /home/user/universal-or-strategy/UniversalORStrategyV9.cs
```

---

## Important Notes

1. **Automatic in Every Conversation**: These rules apply automatically. No manual setup needed per conversation.

2. **Fallback-Ready**: If MCP is unavailable, you automatically fall back to native tools.

3. **State Persistence**: Session state is saved, so opening a new conversation won't lose context.

4. **Cost Tracking**: Every delegation is logged for transparency.

5. **Always Reversible**: You can switch between delegation and native tools anytime.

---

## FAQ

**Q: What if call_gemini_flash fails?**
A: The bridge automatically falls back to using native Read/Write tools and logs the error to `.agent/state/errors.log`. You'll get a fallback notification.

**Q: Does this work in every conversation?**
A: Yes! The `claude.json` configuration ensures the MCP bridge is available in every new Claude Code CLI session.

**Q: Can I turn off delegation?**
A: Yes, just use native tools (Read, Write, Edit) instead of `call_gemini_flash` anytime.

**Q: How much will I save?**
A: Approximately $0.0001-$0.0002 per file operation vs $0.002-$0.005 with native Haiku. For a feature with 50 file operations: **$0.25 saved = enough for 5 more features free!**

---

## Summary

✅ **Every conversation**: Auto-delegation enabled
✅ **MCP bridge**: Configured and ready
✅ **Cost savings**: 99% on file I/O
✅ **Zero setup needed**: Just use `call_gemini_flash` for file ops
✅ **Fallback ready**: Native tools available if needed

**Next Steps**: When you need to save a file, use `call_gemini_flash`. It's that simple!
