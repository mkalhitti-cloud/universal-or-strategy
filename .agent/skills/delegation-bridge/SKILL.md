---
name: delegation-bridge
description: Universal MCP-based delegation to Gemini Flash 3.0 for cost optimization. Works with ANY AI model (Claude, Gemini, Grok) via MCP server to route file I/O and routine tasks to the most cost-effective execution layer.
---

# Delegation Bridge Skill

## Purpose
Route file I/O and routine tasks to Gemini Flash 3.0 (200x cheaper than Opus, 40x cheaper than Haiku) via MCP server, while preserving AI-agnostic compatibility across all development environments.

## Core Principle: Universal Cost Optimization

**Every AI benefits from delegation:**
- Claude Opus/Sonnet/Haiku → Delegate I/O to Gemini Flash
- Gemini Pro → Delegate I/O to Gemini Flash
- Any other AI → Delegate I/O to cheapest available option

**Result**: Consistent workflow, minimal cost, zero vendor lock-in.

---

## MCP Server Configuration

**Server**: `delegation_bridge`
**Tool**: `call_gemini_flash`

**Parameters**:
```json
{
  "context": "Task description (e.g., 'Deploy V8_9.cs to both locations')",
  "code": "Optional code content to deploy",
  "action": "Type of action: deploy|update_docs|read_file|analyze"
}
```

**Connection**: Configured in `.agent/config/mcp_servers.json` (if file exists)

---

## When to Delegate (MANDATORY for ALL AIs)

### ✅ Always Delegate to Gemini Flash:

#### 1. File Operations
- Creating/saving files
- Dual-deployment to NinjaTrader (project repo + bin folder)
- Reading non-critical files for context
- CRLF line ending fixes (Windows compatibility)
- File listing and directory traversal

#### 2. Documentation Updates
- CHANGELOG.md updates
- README updates
- Version tracking in `.agent/state/current_version.txt`
- Session state updates in `.agent/state/session_state.json`

#### 3. Routine Analysis
- File listing (versions, backups, etc.)
- Directory structure exploration
- Git status parsing
- Cost tracking updates

#### 4. Deployment Tasks
- Saving to project repository
- Copying to NinjaTrader bin/Custom/Strategies/
- Verifying file integrity
- Updating deployment logs

#### 5. Context & Usage Tracking
- Saving conversational context to `.agent/PROJECT_STATE.md`
- Updating `.agent/UNANSWERED_QUESTIONS.md`
- Recording usage metrics in `.agent/state/cost_tracking.json`
- Maintaining session continuity in `.agent/state/session_state.json`

---

### ❌ Never Delegate (Keep in Current AI):

#### 1. Code Logic & Implementation
- Writing trading algorithms
- Implementing NinjaTrader strategy features
- Bug fixes requiring deep reasoning
- Architecture design decisions

#### 2. User Interaction
- Asking clarifying questions
- Explaining technical concepts
- Decision framework application
- Cost/quality trade-off discussions

#### 3. Critical Analysis
- Code review for trading logic
- Security vulnerability assessment
- Performance optimization strategies
- Complex debugging

---

## Delegation Priority Chain

**Configured in**: `.agent/config/ai_capabilities.json`

```
Priority 1: Gemini Flash 3.0 (via MCP)
├─ Cost: $0.075/M input, $0.30/M output
├─ Speed: Fastest
└─ Best for: All file I/O, deployment, docs

Priority 2: Claude Haiku 3.5 (if MCP unavailable)
├─ Cost: $0.25/M input, $1.25/M output
├─ Speed: Fast
└─ Best for: Fallback file operations

Priority 3: Current AI (last resort)
├─ Cost: Varies (expensive if Opus/Sonnet)
├─ Speed: Depends on model
└─ Best for: When delegation unavailable
```

---

## Usage Examples

### Example 1: Claude Opus (Antigravity IDE) - Full File Implementation

```
[User pastes prompt requesting V8_9_MOMO.cs implementation]

Opus Thinking:
<thinking>
MOMO setup requires:
- Properties for MOMO-specific settings
- Class variables for order tracking
- UI button integration in CreateUI()
- Entry logic in OnBarUpdate()
- Trailing stops in ManageTrailingStops()
- Position tracking in activePositions dictionary

This touches 6 code locations, requires full file approach.
</thinking>

[Opus generates complete V8_9_MOMO.cs file - 4100 lines]

Opus: "Implementation complete. Code cost: $0.12

Now delegating deployment to Gemini Flash for optimal cost..."

call_gemini_flash(
  context: "Deploy V8_9_MOMO.cs to both project repo and NinjaTrader bin folder. Update CHANGELOG.md with 'V8.9 MOMO - Added momentum trade setup with full lifecycle management' entry. Update .agent/state/current_version.txt to 'V8_9_MOMO'. Verify CRLF line endings.",
  code: [V8_9_MOMO.cs full content],
  action: "deploy"
)

[Gemini Flash executes via MCP]

Gemini Flash Response:
✓ Saved to ${PROJECT_ROOT}/UniversalORStrategyV8_9_MOMO.cs
✓ Deployed to NinjaTrader bin/Custom/Strategies/
✓ CHANGELOG.md updated
✓ .agent/state/current_version.txt → V8_9_MOMO
✓ CRLF verified
Deployment cost: $0.0001

Opus: "Deployment complete. Total cost: $0.1201
Ready to test in NinjaTrader."
```

**Cost breakdown**:
- Opus (code): $0.12
- Gemini Flash (deploy): $0.0001
- **Total**: $0.1201

**vs Opus doing everything**: $0.40 (70% savings!)

---

### Example 2: Claude Sonnet (Claude Code CLI) - Planning & Coordination

```
User: "List all available strategy versions"

Sonnet: "Delegating file listing to Gemini Flash..."

call_gemini_flash(
  context: "List all UniversalORStrategy*.cs files in ${PROJECT_ROOT}/ with version numbers extracted from filenames. Sort by version number.",
  action: "read_file"
)

[Gemini Flash returns version list]

Sonnet: "Here are your available versions:

- V8_2.cs (baseline - button rows)
- V8_9.cs (current - 4-target system + frequency trailing)
- V8_9_MOMO.cs (latest - momentum setup)

Cost: $0.0001 (Gemini Flash)"
```

---

### Example 3: Gemini Flash (Direct Use in IDE)

```
[User asks Gemini Flash directly in Cursor/Windsurf]

User: "Deploy the V8_9_MOMO.cs file to both locations"

Gemini Flash: "I'm already the optimal choice for this task. Executing deployment directly..."

[Deploys without delegation]

✓ Saved to project repo
✓ Deployed to NinjaTrader bin folder
✓ CHANGELOG updated
✓ State files updated

Cost: $0.0001
```

**Note**: When Gemini Flash is the active AI, it skips delegation and executes directly.

---

### Example 4: Gemini Pro (Another IDE) - Code + Delegation

```
[User asks Gemini Pro to add a checkbox feature]

Gemini Pro Thinking:
"Simple UI change - checkbox for auto-breakeven.
Touches 1 method (CreateUI), no shared state.
Snippet approach is safe."

[Gemini Pro generates CreateUI() method snippet]

Gemini Pro: "Code implementation complete. Delegating integration and deployment to Gemini Flash..."

call_gemini_flash(
  context: "Integrate this CreateUI() method snippet into V8_9.cs, save as V8_9_AUTO_BE_CHECKBOX.cs, deploy to both locations, update docs.",
  code: [CreateUI() method snippet],
  action: "deploy"
)

[Gemini Flash integrates snippet into full file and deploys]

Gemini Pro: "Feature complete. Cost: $0.02 (Pro logic) + $0.0001 (Flash deployment)"
```

---

### Example 5: Cross-IDE Session Continuity

```
[Claude Code CLI session hits usage limit mid-feature]

Sonnet: "Updating session state before limit exhaustion..."

call_gemini_flash(
  context: "Update .agent/state/session_state.json with current progress: MOMO feature at 'code_complete' status, next step 'deployment_pending'",
  code: null,
  action: "update_docs"
)

[User switches to Antigravity IDE with fresh credits]

Opus: "Reading session state..."

call_gemini_flash(
  context: "Read .agent/state/session_state.json to understand current project status",
  action: "read_file"
)

[Gemini Flash returns state]

Opus: "I see MOMO feature code is complete. Deployment pending. Shall I proceed with deployment?"

[Zero context loss, seamless handoff]
```

---

## Cost Comparison Table

| Task | Opus Only | Opus + Haiku | Opus + Gemini Flash | Savings |
|------|-----------|--------------|---------------------|---------|
| **Plan MOMO feature** | $0.02 | $0.02 | $0.02 | 0% |
| **Implement code (full file)** | $0.12 | $0.12 | $0.12 | 0% |
| **Deploy to 2 locations** | $0.10 | $0.004 | $0.0001 | 99.9% |
| **Update CHANGELOG** | $0.06 | $0.003 | $0.00005 | 99.9% |
| **Update state files** | $0.10 | $0.004 | $0.0001 | 99.9% |
| **TOTAL** | **$0.40** | **$0.153** | **$0.1402** | **65%** |

**Key insight**: Code costs stay the same (Opus quality needed), but ALL I/O costs drop to near-zero with Gemini Flash.

---

## Integration with Other Skills

### This Skill is Used By:

1. **multi-ide-router**
   - Calls delegation bridge when routing tasks
   - Checks `.agent/config/ai_capabilities.json` for priority

2. **file-manager**
   - Delegates all file operations to Gemini Flash
   - Falls back to Haiku if MCP unavailable

3. **version-safety**
   - Delegates deployment to Gemini Flash
   - Delegates version tracking updates

4. **context-transfer**
   - Uses delegation bridge for session state updates
   - Enables cross-IDE continuity

### This Skill Calls:

- **MCP server**: `delegation_bridge`
- **Tool**: `call_gemini_flash`
- **Config**: `.agent/config/ai_capabilities.json`
- **State**: `.agent/state/*.json` files

---

## State Tracking & Portability

### Before Each Delegation:

**Current AI should**:
1. Update `.agent/state/session_state.json` with current progress
2. Ensure paths use `${PROJECT_ROOT}/` not absolute paths
3. Record delegation in `.agent/state/cost_tracking.json`

### After Each Delegation:

**Gemini Flash should**:
1. Update `.agent/state/last_deployment.json`:
   ```json
   {
     "timestamp": "2026-01-19T15:30:00Z",
     "file_deployed": "UniversalORStrategyV8_9_MOMO.cs",
     "targets": [
       "${PROJECT_ROOT}/UniversalORStrategyV8_9_MOMO.cs",
       "C:/Users/${USERNAME}/Documents/NinjaTrader 8/bin/Custom/Strategies/UniversalORStrategyV8_9_MOMO.cs"
     ],
     "verification": {
       "crlf_checked": true,
       "file_sizes_match": true,
       "compilation_ready": true
     }
   }
   ```

2. Append to `.agent/state/cost_tracking.json`
3. Update `.agent/state/current_version.txt` if version changed

**Result**: ANY AI on ANY machine can read state and continue work.

---

## Fallback Strategy

### If MCP Server Unavailable:

```
Detecting delegation bridge...
❌ MCP server 'delegation_bridge' not available

Fallback chain:
1. Try Claude Haiku (if in Claude Code CLI) → Cost: $0.004
2. Try current AI (expensive fallback) → Cost: $0.10
3. Manual file operations (user intervention)

Warning: Using fallback increases cost by 40-1000x
Recommendation: Restart MCP server or switch to IDE with MCP support
```

### If Gemini Flash Fails:

```
Delegating to Gemini Flash...
❌ Gemini Flash returned error: [error message]

Fallback action:
1. Log error to .agent/state/errors.log
2. Retry once with exponential backoff
3. If retry fails, fall back to Claude Haiku
4. Continue workflow (don't block user)

Error logged:
{
  "timestamp": "2026-01-19T15:45:00Z",
  "error": "Gemini Flash timeout",
  "task": "deploy V8_9_MOMO.cs",
  "fallback_used": "claude_haiku",
  "extra_cost": "$0.004"
}
```

---

## Environment Variable Resolution

### Supported Variables:

- `${PROJECT_ROOT}` → Absolute path to git repository root
- `${USERNAME}` → Current OS username
- `${HOME}` → User home directory
- `${PLATFORM}` → windows|mac|linux

### Example Resolution:

**Config file**:
```json
{
  "ninjatrader_bin": "C:/Users/${USERNAME}/Documents/NinjaTrader 8/bin/Custom/Strategies/"
}
```

**Runtime resolution** (on Mohammed's machine):
```
C:/Users/${USERNAME}/Documents/NinjaTrader 8/bin/Custom/Strategies/
(resolves to: C:/Users/Mohammed Khalid/Documents/NinjaTrader 8/bin/Custom/Strategies/)
```

**On different machine** (user: john):
```
C:/Users/${USERNAME}/Documents/NinjaTrader 8/bin/Custom/Strategies/
(resolves to: C:/Users/john/Documents/NinjaTrader 8/bin/Custom/Strategies/)
```

**Result**: Same config works on any machine.

---

## Quick Reference: When to Delegate

| Task | Delegate? | To |  Cost |
|------|-----------|-----|-------|
| Write trading algorithm | ❌ NO | Current AI (Opus preferred) | $0.12 |
| Deploy file to NinjaTrader | ✅ YES | Gemini Flash | $0.0001 |
| Update CHANGELOG.md | ✅ YES | Gemini Flash | $0.00005 |
| Fix critical bug | ❌ NO | Current AI (Opus preferred) | $0.20 |
| List version files | ✅ YES | Gemini Flash | $0.0001 |
| Ask user clarifying questions | ❌ NO | Current AI | $0.01 |
| Update .agent/state/ files | ✅ YES | Gemini Flash | $0.00005 |
| Explain code concept | ❌ NO | Current AI | $0.02 |
| Read file for analysis | ✅ YES | Gemini Flash | $0.0001 |
| Generate Antigravity prompt | ❌ NO | Current AI (Sonnet) | $0.02 |

**Rule**: If it's file I/O, docs, or routine analysis → **DELEGATE**. If it's logic, reasoning, or user interaction → **CURRENT AI**.

---

## Testing the Delegation Bridge

### Test 1: Simple File List

```
call_gemini_flash(
  context: "List all .cs files in ${PROJECT_ROOT}/ that start with 'Universal'",
  action: "read_file"
)

Expected: List of UniversalORStrategy*.cs files
Cost: ~$0.0001
```

### Test 2: Deploy Test File

```
call_gemini_flash(
  context: "Create test file ${PROJECT_ROOT}/test_delegation.txt with content 'Delegation bridge working!' and deploy to NinjaTrader bin folder",
  code: "Delegation bridge working!",
  action: "deploy"
)

Expected: File in both locations
Cost: ~$0.0001
```

### Test 3: State Update

```
call_gemini_flash(
  context: "Update .agent/state/session_state.json: set in_progress.status to 'delegation_bridge_tested'",
  action: "update_docs"
)

Expected: session_state.json updated
Cost: ~$0.00005
```

### Test 4: Cross-AI Handoff

```
[In Claude Code CLI]
Sonnet: call_gemini_flash(context: "Update state: switching to Antigravity", action: "update_docs")

[Switch to Antigravity]
Opus: call_gemini_flash(context: "Read session state", action: "read_file")

Expected: Opus sees Sonnet's final state
Cost: ~$0.0002 total
```

---

## Summary

**Delegation Bridge = Universal Cost Optimization Layer**

- ✅ Works with ANY AI (Claude, Gemini, Grok, etc.)
- ✅ Routes file I/O to cheapest option (Gemini Flash)
- ✅ Preserves code quality (Opus still handles logic)
- ✅ Enables cross-IDE portability (state in .agent/)
- ✅ Saves 65-99% on non-code tasks
- ✅ Zero vendor lock-in

**Cost equation**:
```
Old way: ALL_TASKS_IN_OPUS = $0.40/feature
New way: OPUS_CODE + GEMINI_FLASH_IO = $0.14/feature
Savings: 65% per feature
```

**Over 10 features**: Save $2.60 = enough for 6 more features free!

---

## Next Steps After Reading This Skill

1. **Verify MCP server** `delegation_bridge` is configured
2. **Test delegation** with simple file list task
3. **Update other skills** to use delegation bridge
4. **Enforce Continuity**: Always update `.agent/PROJECT_STATE.md` after significant logic changes.
5. **Celebrate** 65% cost savings on every feature! 🎉
