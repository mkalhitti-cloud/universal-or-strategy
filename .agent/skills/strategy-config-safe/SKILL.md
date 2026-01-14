---
name: strategy-config-safe
description: Safe parameter update protocol for NinjaTrader strategies. Use when modifying stops, targets, ATR multipliers, or any State.SetDefaults values. Ensures changes don't break execution logic or introduce bugs.
---

# Strategy Configuration Safety Protocol

**Purpose:** Prevent bugs when updating strategy parameters (stops, targets, ATR multipliers).

---

## When to Use This Skill

- Updating stop loss or profit target multipliers
- Changing ATR period or multipliers
- Modifying position sizing parameters
- Adjusting session time windows
- Any change to `State.SetDefaults` section

---

## The Golden Rule

> **NEVER modify execution logic when updating parameters.**
> 
> Parameters live in `State.SetDefaults`.  
> Execution logic lives in `OnBarUpdate`, `OnMarketData`, etc.  
> **These two zones must NEVER be mixed in the same edit.**

---

## Safe Parameter Update Workflow

### Step 1: Identify the Parameter Zone

All user-configurable parameters MUST be in `State.SetDefaults`:

```csharp
protected override void OnStateChange()
{
    if (State == State.SetDefaults)
    {
        // ✅ SAFE ZONE - All parameters here
        StopLossATRMultiplier = 2.0;
        Target1ATRMultiplier = 1.5;
        Target2ATRMultiplier = 3.0;
        ATRPeriod = 14;
        ORWindowMinutes = 30;
    }
}
```

### Step 2: Pre-Update Validation

Before changing ANY parameter, verify:

```markdown
- [ ] Parameter exists in State.SetDefaults
- [ ] Parameter is declared as a [NinjaScriptProperty]
- [ ] Parameter has min/max validation
- [ ] Parameter is used correctly in execution logic
- [ ] No hardcoded values in execution logic
```

### Step 3: Make the Change

**Example: Updating Stop Loss Multiplier**

```csharp
// ❌ WRONG - Hardcoded in execution logic
double stopPrice = entryPrice - (atr * 2.0);  // Magic number!

// ✅ CORRECT - Uses parameter
double stopPrice = entryPrice - (atr * StopLossATRMultiplier);
```

**Before changing the parameter:**
```csharp
[Range(0.5, 5.0), NinjaScriptProperty]
[Display(Name = "Stop Loss ATR Multiplier", Order = 1, GroupName = "Risk Management")]
public double StopLossATRMultiplier { get; set; }

// In SetDefaults
StopLossATRMultiplier = 2.0;  // Current value
```

**After changing the parameter:**
```csharp
// In SetDefaults
StopLossATRMultiplier = 2.5;  // New value - more conservative
```

### Step 4: Validation Checks

After updating, verify:

```markdown
- [ ] Value is within [Range] bounds
- [ ] Change makes logical sense (e.g., stop > 0, target > stop)
- [ ] No execution logic was accidentally modified
- [ ] Compilation successful with no warnings
- [ ] Test in Strategy Analyzer with new value
```

---

## Parameter Categories & Safety Rules

### 1. Risk Management Parameters

**Stop Loss:**
```csharp
[Range(0.5, 5.0), NinjaScriptProperty]
public double StopLossATRMultiplier { get; set; }

// Safety rule: Must be > 0, typically 1.5-3.0
// Validation: stopPrice must be < entryPrice for longs
```

**Profit Targets:**
```csharp
[Range(0.5, 10.0), NinjaScriptProperty]
public double Target1ATRMultiplier { get; set; }

[Range(1.0, 15.0), NinjaScriptProperty]
public double Target2ATRMultiplier { get; set; }

// Safety rule: Target2 > Target1 > StopLoss
// Validation: Enforce in OnStateChange
if (Target2ATRMultiplier <= Target1ATRMultiplier)
{
    Print("ERROR: Target2 must be > Target1");
    Target2ATRMultiplier = Target1ATRMultiplier + 1.0;
}
```

### 2. ATR Parameters

**ATR Period:**
```csharp
[Range(5, 50), NinjaScriptProperty]
public int ATRPeriod { get; set; }

// Safety rule: Must have sufficient bars
// Validation: Check CurrentBar >= ATRPeriod in OnBarUpdate
if (CurrentBar < ATRPeriod) return;
```

**ATR Multipliers:**
```csharp
// All ATR multipliers should use same base ATR
private double cachedATR = 0;

protected override void OnBarUpdate()
{
    cachedATR = ATR(ATRPeriod)[0];  // Calculate once
    
    // Use cached value everywhere
    double stopDistance = cachedATR * StopLossATRMultiplier;
    double target1Distance = cachedATR * Target1ATRMultiplier;
}
```

### 3. Session Timing Parameters

**OR Window:**
```csharp
[Range(15, 120), NinjaScriptProperty]
public int ORWindowMinutes { get; set; }

// Safety rule: Must fit within RTH session
// Validation: ORWindowMinutes <= 390 (6.5 hours)
```

**Session Times:**
```csharp
[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
public TimeSpan SessionStartTime { get; set; }

[PropertyEditor("NinjaTrader.Gui.Tools.TimeEditorKey")]
public TimeSpan SessionEndTime { get; set; }

// Safety rule: EndTime > StartTime
// Validation in OnStateChange
if (SessionEndTime <= SessionStartTime)
{
    Print("ERROR: Session end must be after start");
    SessionEndTime = SessionStartTime.Add(TimeSpan.FromHours(6.5));
}
```

### 4. Position Sizing Parameters

**Contracts Per Account:**
```csharp
[Range(1, 10), NinjaScriptProperty]
public int ContractsPerAccount { get; set; }

// Safety rule: Must respect account size
// Validation: Check against Apex daily loss limit
double maxLoss = ContractsPerAccount * (cachedATR * StopLossATRMultiplier) * TickValue;
if (maxLoss > DailyLossLimit * 0.5)
{
    Print("WARNING: Position size too large for daily loss limit");
}
```

---

## Common Parameter Update Scenarios

### Scenario 1: Making Stops Tighter

**Current:** `StopLossATRMultiplier = 2.0`  
**New:** `StopLossATRMultiplier = 1.5`

**Validation:**
```markdown
- [ ] New value > 0.5 (minimum safe distance)
- [ ] Test on historical data to verify not stopped out too frequently
- [ ] Check win rate doesn't drop below 40%
- [ ] Verify Apex daily loss limit still safe
```

### Scenario 2: Extending Targets

**Current:** `Target2ATRMultiplier = 3.0`  
**New:** `Target2ATRMultiplier = 4.0`

**Validation:**
```markdown
- [ ] Target2 > Target1 (maintain hierarchy)
- [ ] Historical data shows target is reachable
- [ ] Doesn't violate 50/50 profit target rule
- [ ] Trailing stop logic still works correctly
```

### Scenario 3: Changing ATR Period

**Current:** `ATRPeriod = 14`  
**New:** `ATRPeriod = 20`

**Validation:**
```markdown
- [ ] BarsRequiredToPlot updated if necessary
- [ ] Cached ATR calculation uses new period
- [ ] All ATR-based distances recalculated
- [ ] Test shows improved volatility measurement
```

---

## Isolation Protocol

### What You CAN Change Safely

✅ Parameter default values in `State.SetDefaults`  
✅ Parameter [Range] bounds  
✅ Parameter display names/descriptions  
✅ Parameter grouping/ordering  

### What You CANNOT Change

❌ Execution logic in `OnBarUpdate`  
❌ Order submission code  
❌ Trailing stop calculations  
❌ Entry/exit conditions  
❌ Position management logic  

**If you need to change execution logic, that's a NEW FEATURE, not a parameter update.**

---

## Testing Protocol

### After ANY Parameter Change

1. **Compile Test:**
   ```markdown
   - [ ] No compilation errors
   - [ ] No warnings
   ```

2. **Strategy Analyzer Test:**
   ```markdown
   - [ ] Run on 3 months historical data
   - [ ] Verify win rate > 40%
   - [ ] Check max drawdown < 10%
   - [ ] Validate profit factor > 1.2
   ```

3. **Simulation Test:**
   ```markdown
   - [ ] Deploy to sim account
   - [ ] Monitor for 1 week
   - [ ] Verify no order rejections
   - [ ] Check execution speed < 50ms
   ```

4. **Live Deployment:**
   ```markdown
   - [ ] Only after sim test passes
   - [ ] Monitor first 3 days closely
   - [ ] Keep previous version ready for rollback
   ```

---

## Emergency Rollback

If parameter change causes issues in live trading:

```powershell
# Immediate rollback steps
1. Disable strategy
2. Revert parameter to previous value
3. Recompile
4. Test on sim before re-enabling
5. Document what went wrong
```

---

## Parameter Change Log

Keep a log of all parameter changes:

```markdown
## Parameter Change History

### 2026-01-14
- **StopLossATRMultiplier:** 2.0 → 1.8
- **Reason:** Reduce risk per trade
- **Result:** Win rate increased 5%, profit factor stable
- **Status:** Live on all accounts

### 2026-01-10
- **Target2ATRMultiplier:** 3.0 → 3.5
- **Reason:** Capture larger moves
- **Result:** Target hit rate dropped 10%, reverted
- **Status:** Rolled back after 3 days
```

---

## Related Skills

- [ninjatrader-strategy-dev](../ninjatrader-strategy-dev/SKILL.md) - Coding patterns
- [trading-code-review](../trading-code-review/SKILL.md) - Pre-deployment checklist
- [apex-rithmic-trading](../apex-rithmic-trading/SKILL.md) - Risk management rules
- [project-lifecycle](../project-lifecycle/SKILL.md) - Version management
