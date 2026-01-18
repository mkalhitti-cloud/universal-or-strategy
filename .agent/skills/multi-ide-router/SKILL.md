---
name: multi-ide-router
description: Optimizes model and IDE selection across Claude Code CLI and Antigravity IDE to maximize credit efficiency. Routes tasks to the best environment based on complexity and thinking requirements.
---

# Multi-IDE Router Skill

## Purpose
Intelligently route tasks between Claude Code CLI and Antigravity IDE to optimize credit usage and leverage each environment's strengths.

## Your Two Environments (OPTIMIZED WORKFLOW)

### PRIMARY: Antigravity IDE
**Role:** Code and project decision expert

**Primary Model:** **Claude Opus 4.5 Thinking**
- All code work happens here first
- Shows reasoning for decisions
- Separate credit pool (maximize usage)

**Backup Model:** Claude Sonnet 4.5 Thinking (if needed)

**When to use:**
- ✅ ANY code work (features, bugs, logic, UI)
- ✅ Architecture design
- ✅ Trading logic changes
- ✅ Debugging
- ✅ Refactoring

**Workflow:**
1. You talk to Sonnet (Claude Code) to gather context
2. Sonnet generates detailed prompt for Antigravity
3. You paste prompt into Antigravity (Opus Thinking)
4. Opus works and returns results
5. You implement/test

---

### SECONDARY: Claude Code CLI
**Role:** Context gathering, planning, instruction creation, routine tasks

**Models:**
- **Sonnet (me)**: Coordinator, context gathering, prompt generation
- **Haiku**: Routine file/doc operations (auto-spawned)
- **Opus**: Fallback when Antigravity credits depleted (auto-spawned)

**When to use:**
- ✅ Talking to me to plan work
- ✅ Generating prompts for Antigravity
- ✅ File operations (Haiku automatic)
- ✅ Changelog updates (Haiku automatic)
- ✅ **FALLBACK**: When Antigravity credits exhausted

**Workflow:**
1. You tell me (Sonnet) what you want to build
2. I ask clarifying questions
3. I generate detailed prompt with context
4. You paste into Antigravity Opus Thinking
5. I handle routine tasks (files, docs) via Haiku sub-agents

---

## Routing Decision Tree (OPTIMIZED WORKFLOW)

```
Task Request
    │
    ├─ ANY CODE/PROJECT WORK?
    │   └─ PRIMARY: Antigravity IDE (Opus 4.5 Thinking)
    │       └─ If credits exhausted → Fallback: Claude Code CLI (Opus sub-agent)
    │
    ├─ Routine file/doc operations?
    │   └─ USE: Claude Code CLI (Haiku sub-agent)
    │
    ├─ Context gathering / planning / instruction creation?
    │   └─ USE: Claude Code CLI (Sonnet = me)
    │       → Generate detailed prompts for Antigravity Opus
    │
    └─ Antigravity credits depleted?
        └─ FALLBACK: Claude Code CLI (Opus/Haiku sub-agents)
            Wait for credit refresh, then resume in Antigravity
```

**Key Principle**: Antigravity IDE (Opus Thinking) = Primary code expert until credits run out

---

## Task Routing Matrix (OPTIMIZED)

| Task Type | Primary IDE | Model | Thinking? | Reason |
|-----------|-------------|-------|-----------|--------|
| **ANY code work** | **Antigravity** | **Opus 4.5 Thinking** | ✅ Yes | Primary code expert |
| **UI features** | **Antigravity** | **Opus 4.5 Thinking** | ✅ Yes | Shows design reasoning |
| **Bug fixes** | **Antigravity** | **Opus 4.5 Thinking** | ✅ Yes | Thorough analysis |
| **Trading logic** | **Antigravity** | **Opus 4.5 Thinking** | ✅ Yes | Critical + reasoning |
| **Architecture design** | **Antigravity** | **Opus 4.5 Thinking** | ✅ Yes | Design exploration |
| **Refactoring** | **Antigravity** | **Opus 4.5 Thinking** | ✅ Yes | Shows reasoning |
| **Live emergencies** | **Antigravity** | **Opus 4.5 Thinking** | ✅ Yes | Deep analysis |
| **Complex debugging** | **Antigravity** | **Opus 4.5 Thinking** | ✅ Yes | Reasoning trace |
| ─── | ─── | ─── | ─── | ─── |
| **File operations** | Claude Code | Haiku | No | Routine, automatic |
| **Changelog updates** | Claude Code | Haiku | No | Simple, automatic |
| **Context gathering** | Claude Code | Sonnet | No | Planning/instructions |
| **Fallback (no credits)** | Claude Code | Opus sub-agent | No | When AG depleted |

**AG = Antigravity IDE**

---

## Credit Optimization Strategy

### Week 1 (Heavy Development)
**Claude Code CLI:**
- 10 file operations (Haiku) = $0.01
- 5 code features (Opus) = $1.50
- Coordination (Sonnet) = $0.15
- **Total**: $1.66

**Antigravity IDE:**
- 1 architecture session (Sonnet Thinking) = $0.30
- **Total**: $0.30

**Weekly Total**: $1.96 (vs $3.64 all-Opus)
**Savings**: 46%

---

### Week 2 (Maintenance + Emergency)
**Claude Code CLI:**
- 8 routine tasks (Haiku) = $0.01
- 2 bug fixes (Opus) = $0.60
- Coordination (Sonnet) = $0.10
- **Total**: $0.71

**Antigravity IDE:**
- 1 live trading bug (Opus Thinking) = $2.00
- **Total**: $2.00

**Weekly Total**: $2.71
**Without optimization**: $4.50
**Savings**: 40%

---

## When to Use Antigravity IDE

### Use Case 1: Architecture Planning
**Trigger**: Designing new subsystems or major refactors

**Why Antigravity:**
- Sonnet Thinking shows design reasoning
- Can explore multiple approaches
- Different credit pool

**Prompt Template:**
```markdown
# Architecture Planning Session

Context: [Paste context from Claude Code session]

Task: Design the architecture for [feature]

Requirements:
- [Requirement 1]
- [Requirement 2]

Show your reasoning for:
- Architecture decisions
- Trade-offs considered
- Why chosen approach is best

Return: Detailed plan to implement in Claude Code CLI
```

**Model**: Claude Sonnet 4.5 Thinking

---

### Use Case 2: Live Trading Emergency
**Trigger**: Money being lost, orders failing, production incident

**Why Antigravity:**
- Opus Thinking for deep analysis
- Shows reasoning trace
- Thorough edge case consideration

**Prompt Template:**
```markdown
# Live Trading Emergency

Context: [Paste error logs, strategy version, what happened]

Issue: [Describe problem]

Impact:
- Account: [Funded/Sim]
- Loss: [Dollar amount]
- Trades affected: [Number]

Analyze:
1. Root cause (show reasoning)
2. Why it happened
3. Edge cases to consider
4. Tested fix that won't make it worse

CRITICAL: This is live money. Be thorough.
```

**Model**: Claude Opus 4.5 Thinking

---

### Use Case 3: Complex Debugging (Non-Emergency)
**Trigger**: Bug is hard to reproduce, multi-system issue

**Strategy**: Start in Claude Code CLI (Opus)
- If Opus solves it → Done, saved credits
- If stuck after 2 attempts → Switch to Antigravity (Opus Thinking)

**Prompt Template:**
```markdown
# Complex Debugging Session

Context: [Paste from Claude Code session]

Problem: [What's broken]

What we've tried:
- [Attempt 1]
- [Attempt 2]

Show reasoning for:
- Why previous attempts failed
- Root cause analysis
- Comprehensive solution

Return: Fix to implement in Claude Code CLI
```

**Model**: Claude Opus 4.5 Thinking

---

## Context Transfer Between IDEs

### Claude Code → Antigravity

**Use the context-transfer skill:**
1. In Claude Code CLI: "Generate handoff prompt"
2. Haiku generates context summary
3. Copy/paste into Antigravity
4. Add: "Continue in [Sonnet/Opus] Thinking mode"

**Format:**
```markdown
# Context from Claude Code CLI

[Generated handoff from context-transfer skill]

---

Continue this work in [Sonnet/Opus] Thinking mode.

Task: [Specific task for Antigravity]
```

### Antigravity → Claude Code

**After Antigravity session:**
1. Copy the plan/solution from Antigravity
2. Paste into Claude Code CLI
3. Say: "Implement this plan from Antigravity session"

**Format:**
```markdown
I designed this architecture in Antigravity IDE (Sonnet Thinking).

[Paste architecture plan]

Implement this plan using Opus sub-agent.
```

---

## Sonnet's New Role (OPTIMIZED WORKFLOW)

### I Am Your Context Gatherer & Prompt Generator

**When you ask for ANY code work:**

```
User: "Add a horizontal button layout to the UI"

Me (Sonnet):
"Got it. Let me gather context and create a prompt for Antigravity Opus.

Questions:
- Which version should I base this on? (V8_2?)
- How many button rows? (3 rows: Long/Short, RMA/TREND, Targets?)
- Should buttons resize proportionally?

Once you answer, I'll generate a detailed prompt with:
- Current file state
- Specific changes needed
- Expected output format
- Testing checklist

You'll paste this into Antigravity IDE (Opus 4.5 Thinking) to implement."
```

**Then I generate:**
```markdown
# Prompt for Antigravity IDE (Opus 4.5 Thinking)

## Context
Current version: UniversalORStrategyV8_2.cs
Current UI: Vertical button layout (18 rows × 1 column)
Goal: Horizontal button layout with proportional sizing

## Task
Redesign the UI grid structure from vertical to horizontal:
- Row 1: Long | Short (2 buttons, equal width)
- Row 2: RMA | TREND (2 buttons, equal width)
- Row 3: T1 | T2 | T3 | Runner | BE (5 buttons, equal width)

## Requirements
[Detailed requirements]

## Expected Output
**IMPORTANT: Return the COMPLETE modified file (UniversalORStrategyV8_2_UI_HORIZONTAL.cs).**

Include:
- Grid structure (8 rows)
- Nested grids for button rows
- Star sizing for proportional columns
- Resize grip functionality

**Output format:** Full C# file, 4000+ lines, ready to compile.

Haiku will deploy this file to both locations.

## Testing Checklist
[How to verify it works]

Show your reasoning for design decisions.
```

---

## Snippet vs Full File Decision Framework

### When Deciding Output Format

**CRITICAL QUESTION**: Should Opus return code snippets or the full file?

**Answer depends on**: Complexity and integration risk of the change.

---

### ✅ Snippet Approach is SAFE for:

**Single-method changes:**
- "Add a dropdown to CreateUI()"
- "Fix bug in ManageTrailingStops()"
- "Add validation to SubmitValidatedStop()"

**Isolated additions:**
- New property with no dependencies
- New helper method called from one place
- UI-only changes (button text, colors)

**Characteristics:**
- Touches 1-2 methods
- No shared state changes
- Clear boundaries
- Easy for Haiku to integrate

**Prompt format:**
```markdown
## Expected Output
**IMPORTANT: Return ONLY the modified CreateUI() method.**
**DO NOT return the full file.**

Output format:
```csharp
private void CreateUI()
{
    // Your implementation
}
```

Haiku will integrate this into the full V8_2.cs file.
```

**Cost**: ~$0.10 (Opus returns 500 lines)

---

### ⚠️ Full File Approach is SAFER for:

**Multi-method features** (like MOMO setup):
- New trade setup with entry/exit/tracking
- New target system affecting multiple methods
- Changes to shared state (activePositions, class variables)

**Cross-cutting changes:**
- Refactoring that affects 5+ methods
- New variable used in 3+ places
- UI changes + logic changes together

**Critical trading logic:**
- Order management changes
- Stop loss modifications
- Position tracking updates

**Characteristics:**
- Touches 3+ methods
- Affects shared state
- Integration complexity high
- Live trading risk = zero bugs allowed

**Prompt format:**
```markdown
## Expected Output
**IMPORTANT: Return the COMPLETE modified file (UniversalORStrategyV8_3_MOMO.cs).**

Include ALL code (4000+ lines) ready to compile.

Haiku will deploy this file to both locations.
```

**Cost**: ~$0.40 (Opus reads/writes full 4000-line file)

---

### 📊 Decision Matrix

| Change Type | Methods Affected | Shared State? | Approach | Cost |
|-------------|------------------|---------------|----------|------|
| Fix typo in UI | 1 | No | Snippet | $0.05 |
| Add dropdown menu | 1 | No | Snippet | $0.10 |
| New helper method | 1-2 | No | Snippet | $0.10 |
| Bug fix in trailing stops | 1-2 | Maybe | **Full File** | $0.40 |
| New breakeven button | 2-3 | Yes | **Full File** | $0.40 |
| MOMO trade setup | 6+ | Yes | **Full File** | $0.40 |
| Refactor position tracking | 5+ | Yes | **Full File** | $0.40 |

**Rule of Thumb**: If you're unsure, use **Full File** for live trading code. $0.30 extra cost = cheap insurance.

---

### 🔒 MANDATORY: User Confirmation Before Generating Prompt

**CRITICAL**: Sonnet must ALWAYS ask the user to confirm the approach BEFORE generating the Antigravity prompt.

**Process:**
1. User requests code work
2. Sonnet analyzes the request using the decision matrix
3. **Sonnet recommends** snippet or full file approach with reasoning
4. **Sonnet asks**: "Do you agree with this approach?"
5. User confirms or changes decision
6. Sonnet generates the prompt

**Example:**
```
User: "Add MOMO trade setup with button and all stops/targets"

Sonnet: "Analyzing MOMO feature request...

This touches 6 code locations:
- Properties (MOMO settings)
- Class variables (order tracking)
- CreateUI() (button)
- OnBarUpdate() (entry logic)
- ManageTrailingStops() (trailing)
- OnOrderUpdate() (state handling)

Per the decision matrix: 6+ methods + shared state = FULL FILE approach

Cost comparison:
- Snippet approach: $0.10 (risky - integration complexity)
- Full file approach: $0.40 (safer - Opus sees all dependencies)

Recommendation: FULL FILE ($0.40)

Do you agree with full file approach, or would you prefer snippet approach?"

User: "yes full file"

Sonnet: "Confirmed. Generating full file prompt for Antigravity Opus...

[Generates detailed prompt requesting complete V8_3_MOMO.cs]

Ready to paste into Antigravity IDE."
```

**Never skip this confirmation step.** User has final say on cost vs risk trade-off.

---

### Example: MOMO Trade Setup

**Why Full File is safer for MOMO:**

MOMO touches 6 different locations:
1. **Properties region** - New settings
2. **Class variables** - Order objects, state tracking
3. **CreateUI()** - MOMO button
4. **OnBarUpdate()** - Entry logic
5. **ManageTrailingStops()** - Trailing logic
6. **OnOrderUpdate()** - Order state handling

**Snippet approach risk** ($0.10):
- Haiku might place MOMO button in wrong grid row
- Variable initialization might be out of sequence
- activePositions integration might miss cleanup
- ManageTrailingStops() might get duplicate code
- OnOrderUpdate() might not handle MOMO states

**Full file approach** ($0.40):
- Opus sees ALL dependencies
- Opus controls entire class structure
- No integration errors possible
- You review ONE complete file
- Live trading = zero bugs allowed

**Verdict**: Extra $0.30 is worth avoiding integration bugs in funded trading.

---

### Prompt Template Examples

#### Example 1: Snippet Approach (Simple UI Change)
```markdown
# Prompt for Antigravity IDE (Opus 4.5 Thinking)

## Context
Current version: UniversalORStrategyV8_2.cs
Change needed: Add "Auto BE" checkbox to UI

## Task
Add a checkbox below the Breakeven button that enables/disables automatic breakeven.

## Expected Output
**IMPORTANT: Return ONLY the modified CreateUI() method snippet.**
**DO NOT return the full file.**

Output format:
```csharp
private void CreateUI()
{
    // Your complete CreateUI() implementation with checkbox added
}
```

Haiku will integrate this method into V8_2.cs.

## Testing
- Checkbox appears in UI
- Clicking toggles automatic breakeven
- Setting persists across restarts
```

#### Example 2: Full File Approach (Complex Feature)
```markdown
# Prompt for Antigravity IDE (Opus 4.5 Thinking)

## Context
Current version: UniversalORStrategyV8_2.cs (~4000 lines)
New feature: MOMO trade setup with full lifecycle management

## Task
Add MOMO (momentum) trade setup with:
- Properties for MOMO settings
- Class variables for order tracking
- UI button for MOMO entry
- Entry logic in OnBarUpdate()
- Trailing stops in ManageTrailingStops()
- Position tracking in activePositions dictionary

## Expected Output
**IMPORTANT: Return the COMPLETE file (UniversalORStrategyV8_3_MOMO.cs).**

Include:
- ALL existing code (~4000 lines)
- ALL MOMO additions integrated correctly
- Updated class name: `public class UniversalORStrategyV8_3_MOMO`
- Updated UI title: "OR Strategy V8.3 MOMO"

**Output format:** Full C# file ready to compile in NinjaTrader.

Haiku will deploy V8_3_MOMO.cs to both locations.

## Testing
- MOMO button appears in UI
- MOMO entries submit correctly
- Stops and targets manage independently
- No conflicts with existing OR/RMA/TREND logic
```

**Pattern for ALL code work:**
1. You tell me what you want
2. I ask clarifying questions
3. I generate detailed prompt
4. You paste into Antigravity Opus Thinking
5. Opus implements with reasoning shown

**Pattern for routine tasks:**
```
User: "Update the changelog with V8_3 entry"

Me (Sonnet):
"Routine doc update. Spawning Haiku sub-agent..."
[Haiku handles it automatically]
"Done. Changelog updated."
```

**Pattern when Antigravity credits depleted:**
```
User: "Add a new feature"

Me (Sonnet):
"Normally I'd recommend Antigravity Opus, but if credits are low,
I can spawn an Opus sub-agent here in Claude Code CLI as fallback.

Should I:
1. Generate prompt for Antigravity (use separate credits)
2. Use Claude Code Opus sub-agent (fallback)
3. Wait for Antigravity credit refresh"
```

---

## Credit Usage Dashboard (Mental Model)

### Claude Code CLI Credits
**Best for:**
- High-volume routine tasks (Haiku savings)
- Standard development (Opus sub-agents)
- Day-to-day coding

**Burn Rate**: ~$2.50/week (optimized)

### Antigravity IDE Credits
**Best for:**
- Thinking-intensive tasks
- Architecture planning (1-2x/month)
- Live emergencies (rare, critical)

**Burn Rate**: ~$0.50/week (occasional use)

**Combined**: ~$3/week vs $4.50/week unoptimized = **33% savings**

---

## Example Session Workflow

### Scenario: Planning + Implementing New Feature

#### Step 1: Architecture (Antigravity - Sonnet Thinking)
```
You: "Design a new 4-level breakeven trailing system"

Antigravity Sonnet Thinking:
<thinking>
Let me explore different approaches...
- Approach A: Time-based progression
- Approach B: Profit-based progression
- Approach C: Hybrid
</thinking>

Recommendation: Hybrid approach because [reasoning]...

Implementation plan:
1. Add BreakevenLevel enum
2. Track profit thresholds
3. Update ManageTrailingStops()
...
```

**Cost**: $0.30 (Sonnet Thinking)

#### Step 2: Implementation (Claude Code - Opus Sub-Agent)
```
You: [Paste plan from Antigravity]

"Implement this breakeven system"

Sonnet: "Received architecture from Antigravity. Spawning Opus to implement..."

Opus: [Implements the plan]

Sonnet: "Done. Files created:
- UniversalORStrategyV8_2_BREAKEVEN_SYSTEM.cs
Ready to test."
```

**Cost**: $1.20 (Opus sub-agent)

**Total**: $1.50 for full feature (vs $2.50 all-Opus-Thinking)
**Savings**: 40%

---

## Summary: OPTIMIZED WORKFLOW

### Primary Workflow (90% of time)

**Step 1: Talk to Sonnet (Claude Code CLI)**
```
You: "I want to add a new breakeven trailing stop feature"

Me: "Let me gather context. Questions:
     - Should this be a new button?
     - What profit levels trigger it?
     - Any specific NinjaTrader requirements?"
```

**Step 2: I Generate Prompt for Antigravity**
```
Me: "Here's the detailed prompt for Antigravity Opus Thinking:

[Generates comprehensive prompt with:
 - Full context about current code
 - Specific requirements
 - Expected output format
 - Testing checklist]

Paste this into Antigravity IDE and switch to Opus 4.5 Thinking."
```

**Step 3: You Use Antigravity (Opus Thinking)**
```
[You paste prompt into Antigravity]
[Opus Thinking shows reasoning process]
[Opus provides complete implementation]
```

**Step 4: Return to Claude Code for Deployment**
```
You: "Opus implemented the feature. Here's the code."

Me: "Great! Spawning Haiku to:
    - Save as V8_2_BREAKEVEN_TRAILING
    - Deploy to both locations
    - Update changelog

    Done. Ready to test in NinjaTrader."
```

---

### Fallback Workflow (When Antigravity Credits Low)

**Option A: Wait for Credit Refresh**
```
Me: "Antigravity credits are low.
     Credits refresh on [date].
     Should we wait or use Claude Code Opus as fallback?"
```

**Option B: Use Claude Code Opus Sub-Agent**
```
Me: "Using Claude Code Opus sub-agent as fallback...
     [Spawns Opus sub-agent, no Thinking mode]
     Done. Less detailed reasoning but feature complete."
```

---

### Routine Tasks (Automatic)

**File Operations, Docs, Simple Changes:**
```
You: "List available versions"
Me: [Spawns Haiku automatically] "Here are the versions..."

You: "Update changelog"
Me: [Spawns Haiku automatically] "Changelog updated."

You: "Change default risk from $200 to $150"
Me: [Spawns Haiku automatically] "Default updated."
```

**No manual switching needed.**

---

## Integration with Existing Skills

**This skill works with:**
- **context-transfer**: Generates handoff prompts between IDEs
- **opus-critical**: Defines when Opus is needed
- **version-safety**: Works in both IDEs
- **file-manager**: Works in both IDEs (with adaptation)

**Sonnet uses this skill to:**
1. Detect task type
2. Recommend IDE switch if needed
3. Generate context for transfer
4. Coordinate implementation after planning

---

## Quick Reference (OPTIMIZED)

| Task | Where | Model | Process |
|------|-------|-------|---------|
| **ANY code work** | **Antigravity** | **Opus Thinking** | Talk to Sonnet → Get prompt → Paste to AG |
| **Context/planning** | Claude Code | Sonnet | Talk to me directly |
| **Files/docs** | Claude Code | Haiku | Automatic (I spawn Haiku) |
| **Fallback (no AG credits)** | Claude Code | Opus sub-agent | I spawn Opus |

**Primary:** Antigravity Opus Thinking (until credits depleted)
**Secondary:** Claude Code (context + routine tasks + fallback)

---

## Credit Maximization Strategy

**Antigravity Credits:**
- Use for ALL code work (Opus Thinking = best quality)
- Separate credit pool from Claude Code
- When depleted → Fallback to Claude Code Opus

**Claude Code Credits:**
- Sonnet: Context gathering, prompt generation (cheap)
- Haiku: Routine tasks (very cheap)
- Opus: Fallback only when Antigravity depleted

**Result:** Maximize both credit pools efficiently
