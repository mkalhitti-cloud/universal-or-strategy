---
name: opus-critical
description: Defines when to use Opus vs Opus Thinking vs Sonnet for NinjaTrader strategy development. Auto-spawns Opus for critical trading logic and standard code work. Reserves Opus Thinking for mission-critical live trading issues.
---

# Opus Critical Sub-Agent

## Purpose
Ensure critical trading code gets Opus-level analysis while using cost-effective models for routine work.

## Model Selection Rules

### 🟢 Haiku (Cheapest, Fastest)
**Auto-spawn Haiku for:**
- File operations (copy, move, list)
- Version loading/switching
- Changelog updates
- Documentation formatting
- Simple property default changes (literals only)

**Examples:**
```csharp
// Haiku can change these:
public double RiskPerTrade { get; set; } = 200;  // → 150
public int ORTimeframe { get; set; } = 15;  // → 10
public bool ShowORLabel { get; set; } = true;  // → false
```

**Cost**: ~$0.25/M input tokens

---

### 🔵 Sonnet (Medium Cost, General Purpose)
**Sonnet handles (when Opus is not spawned):**
- Coordination and task delegation
- Deciding which sub-agent to use
- Communicating results to user
- Planning multi-step workflows

**Cost**: ~$3/M input tokens

---

### 🟡 Opus (Expensive, Smart)
**Auto-spawn Opus for:**

#### 1. Standard Code Work
- Adding new features
- Refactoring existing code
- Bug fixes (non-critical)
- UI changes (buttons, layouts, colors)
- Adding validation checks
- Performance optimizations
- Code cleanup and organization

#### 2. Trading Logic (Critical)
- Order submission (ExecuteLong, ExecuteShort, SubmitOrder)
- Stop validation (SubmitValidatedStop, ValidateStopPrice)
- Position sizing calculations
- Risk management formulas
- Trailing stop logic (ManageTrailingStops)
- Target management (T1, T2, T3, Runner)
- Breakeven calculations
- Copy trading signal broadcasting

#### 3. Architecture & Design
- Designing new subsystems
- Multi-account scaling architecture
- State management refactoring
- Performance optimization strategies
- Integration patterns

**Cost**: ~$15/M input tokens

---

### 🔴 Opus Thinking (Most Expensive, Deepest Analysis)
**IMPORTANT: Thinking mode CANNOT be auto-spawned. User must manually switch.**

**Manual activation only:**
```bash
claude --model opus --thinking
```

**Use Opus Thinking ONLY for:**

#### Mission-Critical Issues
1. **Live Trading Bugs**
   - Orders rejected in production
   - Stops failing on Rithmic
   - Position tracking errors with real money
   - Copy trading signal failures
   - Any bug that causes financial loss

2. **Multi-Step Debugging**
   - Complex state corruption
   - Race conditions in order management
   - Memory leaks affecting live sessions
   - Intermittent failures that are hard to reproduce

3. **Critical Architecture Decisions**
   - Changing order management paradigm
   - Redesigning position tracking architecture
   - Major refactoring of risk calculations
   - Decisions with 6+ month impact

4. **Production Incidents**
   - Emergency fixes for live funded accounts
   - Stop-at-market rejections during trading
   - Unexpected flattening or position mismatches
   - Data feed issues affecting order placement

**When to Use Thinking Mode:**
- **Lives Traded**: Issue occurred in live funded account
- **Money at Risk**: Bug could cause financial loss
- **Complex Root Cause**: Multiple systems interacting
- **High Stakes**: Wrong fix makes it worse
- **Need Reasoning**: Want to see Opus's thought process

**Cost**: ~$15/M input + extra reasoning tokens

**How Sonnet Handles This:**
When Sonnet detects a mission-critical issue, it will **recommend** you switch to Thinking mode but **cannot activate it automatically**. You maintain control over when to use this expensive mode.

---

## Decision Tree

```
User Request
    │
    ├─ File operation / version loading?
    │   └─ YES → Haiku
    │
    ├─ Simple property default change (literal)?
    │   └─ YES → Haiku
    │
    ├─ Standard code work (feature, UI, refactor)?
    │   └─ YES → Opus
    │
    ├─ Trading logic change?
    │   └─ YES → Opus
    │
    ├─ Live trading bug / emergency?
    │   └─ YES → Opus Thinking (manual switch)
    │
    └─ Complex multi-step debugging?
        └─ YES → Opus Thinking (manual switch)
```

---

## Automatic vs Manual Invocation

### Automatic Opus Spawning (Regular Opus 4.5)
**Sonnet auto-spawns regular Opus 4.5 when task involves:**
- Methods: `ExecuteLong`, `ExecuteShort`, `SubmitOrder`, `SubmitStopOrder`
- Methods: `ManageTrailingStops`, `UpdateStopOrder`, `CalculatePositionSize`
- Methods: `OnOrderUpdate`, `OnPositionUpdate`, `OnExecutionUpdate`
- Properties: Risk calculations, stop distances, position sizing
- Features: New buttons, dropdowns, UI layouts, validation

**Sub-agent configuration:**
```javascript
Task(
  subagent_type: "general-purpose",
  model: "opus",  // Regular Opus 4.5 (NOT Thinking)
  prompt: "[Task description]",
  description: "[Short summary]"
)
```

**Example:**
```
User: "Add a new breakeven button to the UI"

Sonnet: "This is standard code work. Spawning Opus sub-agent..."
[Spawns regular Opus 4.5 sub-agent]
Opus: [Returns complete implementation]
Sonnet: "Opus implemented the breakeven button. Ready to test."
```

### Manual Opus Thinking (User Switches)
**User manually switches to Opus Thinking for:**
- Live trading emergencies
- Complex debugging sessions
- Critical architecture decisions

**IMPORTANT:** Thinking mode cannot be activated via sub-agents. User must switch manually.

**How to activate:**
```bash
# User runs in terminal:
claude --model opus --thinking
```

**Example:**
```
User: "Stops are getting rejected on Rithmic in live trading.
       Orders failing at market open. Lost $200 this morning."

Sonnet: "⚠️ CRITICAL: Live trading emergency detected.

         I recommend switching to Opus Thinking mode for deep analysis:

         Run: claude --model opus --thinking

         This will show Opus's reasoning process and ensure thorough
         analysis. Should I proceed with regular Opus or do you want
         to switch to Thinking mode?"

→ User switches to Opus Thinking manually
→ Opus analyzes with extended reasoning
→ Shows thought process in <thinking> blocks
→ Provides thorough root cause analysis
→ Suggests tested fix with edge cases covered
```

---

## Critical Code Areas (Opus Territory)

### Order Management
```csharp
// Any changes to these methods = Opus
ExecuteLong()
ExecuteShort()
SubmitOrder()
SubmitStopOrder()
SubmitValidatedStop()
CancelOrder()
FlattenAll()
FlattenPositionByName()
```

### Stop Management
```csharp
// Any changes to these = Opus
ManageTrailingStops()
UpdateStopOrder()
ValidateStopPrice()
CalculateStopDistance()
```

### Position Tracking
```csharp
// Any changes to these = Opus
OnOrderUpdate()
OnPositionUpdate()
OnExecutionUpdate()
CleanupPosition()
TrackPosition()
```

### Risk Calculations
```csharp
// Any changes to these = Opus
CalculatePositionSize()
CalculateDollarRisk()
CalculateStopDistance()
ValidatePositionSize()
```

### Copy Trading
```csharp
// Any changes to these = Opus
BroadcastEntrySignal()
BroadcastFlattenSignal()
BroadcastBreakevenSignal()
OnTradeSignalReceived()
```

---

## Standard Code Work (Opus Territory)

### UI Changes
```csharp
// Opus handles these:
- Adding new buttons
- Changing layouts (vertical → horizontal)
- Creating dropdown menus
- Resizing panels
- Adding tooltips
- Changing colors/fonts
```

### Feature Implementation
```csharp
// Opus handles these:
- New entry methods (RMA, TREND)
- New target management (T4, T5)
- Hotkey additions
- Display enhancements
- Property additions
```

### Refactoring
```csharp
// Opus handles these:
- Code organization
- Method extraction
- Removing duplicated code
- Improving readability
```

---

## Safe for Haiku (Simple Changes)

### Property Defaults
```csharp
// Haiku can change these literals:
public double RiskPerTrade { get; set; } = 200;  // Change value
public int ORTimeframe { get; set; } = 15;  // Change value
public bool EnableCopyTrading { get; set; } = false;  // Toggle
public string SignalPrefix { get; set; } = "OR";  // Change string

// Haiku can change display properties:
[Display(Name = "Risk Per Trade", Order = 1)]
// Change Name or Order values
```

### NOT Safe for Haiku
```csharp
// These require Opus (involve logic):
public double StopDistance { get; set; } = CalculateStopDistance();
private double CalculatePositionSize() { return RiskPerTrade / StopDistance; }
```

---

## Communication Protocol

### When Sonnet Spawns Opus
**Sonnet says:**
```
"This involves [trading logic / standard code work]. Spawning Opus sub-agent to ensure quality..."

[Opus works]

"Opus completed the [feature/fix]. Here's what changed:
- [List changes]
Ready to test in NinjaTrader."
```

### When User Should Switch to Opus Thinking
**Sonnet says:**
```
"⚠️ CRITICAL ISSUE: This is a live trading bug affecting your funded account.

I recommend switching to Opus Thinking mode for deep analysis:

Run: claude --model opus --thinking

This will:
- Show extended reasoning process
- Analyze all edge cases
- Provide thorough root cause analysis
- Ensure the fix won't introduce new issues

Should I proceed with regular Opus, or do you want to switch to Thinking mode?"
```

---

## Cost Comparison

### Typical Session (1 hour coding)

| Scenario | Cost |
|----------|------|
| All Haiku (routine tasks only) | ~$0.10 |
| Sonnet + Haiku sub-agents | ~$0.35 |
| Sonnet + Opus sub-agent (1 feature) | ~$1.20 |
| Sonnet + Opus sub-agents (3 features) | ~$2.50 |
| All Opus | ~$5.00 |
| Opus Thinking (emergency debug) | ~$8.00 |

**Optimal Balance:**
- Haiku: Routine file/doc operations
- Sonnet: Coordination and simple tasks
- Opus: Standard code work + critical trading logic
- Opus Thinking: Live trading emergencies only

**Savings**: ~50-70% vs using Opus for everything

---

## Example Workflows

### Workflow 1: Add New UI Button (Standard Code Work)
```
User: "Add a 'Close T1 Now' button"

Sonnet: "This is standard code work. Spawning Opus..."
Opus: [Designs button, adds to UI, implements handler]
Sonnet: "Done. Button added to target row. Test in NinjaTrader."

Cost: ~$1.20 (Opus sub-agent)
```

### Workflow 2: Change Default Risk (Simple)
```
User: "Change default risk from $200 to $150"

Sonnet: "Simple property change. Spawning Haiku..."
Haiku: [Changes literal value, saves file]
Sonnet: "Done. RiskPerTrade default now $150."

Cost: ~$0.03 (Haiku sub-agent)
```

### Workflow 3: Fix Stop Rejection (Critical Trading Logic)
```
User: "Stops getting rejected on Rithmic"

Sonnet: "This is critical trading logic. Spawning Opus..."
Opus: [Analyzes SubmitValidatedStop, finds buffer issue, fixes]
Sonnet: "Opus found the issue - 4-tick buffer wasn't accounting for
        Rithmic's specific rules. Fixed and ready to test."

Cost: ~$1.50 (Opus sub-agent)
```

### Workflow 4: Emergency Live Bug (Use Thinking)
```
User: "Live account - orders failing, down $200 this morning"

Sonnet: "⚠️ CRITICAL: This is a live trading emergency.

        Recommendation: Switch to Opus Thinking mode for deep analysis.

        Run: claude --model opus --thinking

        Should I proceed with regular Opus or do you want Thinking mode?"

User: [Switches to Opus Thinking]

Opus Thinking: <thinking>
Let me analyze the order submission flow...
[Extended reasoning about state management, timing, Rithmic quirks]
</thinking>

The issue is a race condition between order submission and position
tracking. Here's the fix with full edge case analysis...

Cost: ~$8.00 (Opus Thinking for thorough analysis)
```

---

## Integration with Other Skills

### Works With:
- **version-safety**: Opus creates new versioned files
- **file-manager**: Haiku deploys Opus-created code
- **docs-manager**: Haiku updates changelog after Opus changes
- **code-formatter**: Haiku cleans up Opus-created code

### Workflow:
1. **Sonnet** receives request
2. **Sonnet** decides: Haiku, Opus, or escalate to Opus Thinking
3. **Sub-agent** does the work
4. **Sonnet** reports results to user
5. **User** tests in NinjaTrader

---

## Summary: When to Use What

| Task | Model | Why |
|------|-------|-----|
| File operations | Haiku | Simple, fast, cheap |
| Property defaults (literals) | Haiku | Safe, no logic |
| Standard code work | Opus | Quality matters, affordable |
| Trading logic | Opus | Can't mess this up |
| Live trading bug | Opus Thinking | Money on the line |
| Emergency debugging | Opus Thinking | Need deep analysis |

**Key Principle**: Use the cheapest model that ensures quality and safety.
