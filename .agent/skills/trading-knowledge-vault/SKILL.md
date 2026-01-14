---
name: trading-knowledge-vault
description: Automated lessons learned system for NinjaTrader trading. Use when implementing new features, fixing bugs, or reviewing code. Ensures past mistakes are never repeated by making historical lessons mandatory checkpoints.
---

# Trading Knowledge Vault

**Purpose:** Transform lessons learned from passive documentation into active prevention system.

---

## Core Principle

> **Every bug you fix becomes a permanent checkpoint.**  
> **Every lesson learned becomes a mandatory review.**

This skill makes it **impossible** to repeat past mistakes by forcing AI agents to check the vault before making changes.

---

## The Vault Structure

### Critical Bugs (Never Repeat)

#### 1. Close[0] Bug (The $10,000 Lesson)
**What Happened:**
- Trailing stops only updated at bar close
- Lost 50-90% of profit potential
- Discovered in V5, fixed in V6

**Prevention Protocol:**
```markdown
BEFORE writing ANY code that uses price for real-time decisions:
- [ ] Check: Am I using Close[0]?
- [ ] Check: Is this in OnBarUpdate or OnMarketData?
- [ ] Check: Do I need tick-level updates?
- [ ] If YES to tick-level: Use OnMarketData + GetLivePrice()
- [ ] If NO: Explicitly document why Close[0] is acceptable here
```

**Code Pattern:**
```csharp
// ❌ NEVER DO THIS for real-time decisions
if (Close[0] > entryPrice + trailDistance)
    SetStopLoss(newStop);

// ✅ ALWAYS DO THIS
protected override void OnMarketData(MarketDataEventArgs e)
{
    if (e.MarketDataType != MarketDataType.Last) return;
    if (e.Price > entryPrice + trailDistance)
        if (CanModifyOrder()) SetStopLoss(newStop);
}
```

#### 2. Stranded Orders Bug
**What Happened:**
- Stop/target orders remained active after position closed
- Caused unexpected entries
- Fixed in V5.7

**Prevention Protocol:**
```markdown
BEFORE implementing ANY order management:
- [ ] Check: Do I cancel ALL related orders on fill?
- [ ] Check: Do I handle OnOrderUpdate for ALL order states?
- [ ] Check: Do I clean up on strategy disable?
- [ ] Test: Manually close position, verify orders cancelled
```

**Code Pattern:**
```csharp
// ✅ REQUIRED: Cancel all related orders
private void CleanupOrders()
{
    if (stopOrder != null && stopOrder.OrderState == OrderState.Working)
        CancelOrder(stopOrder);
    if (target1Order != null && target1Order.OrderState == OrderState.Working)
        CancelOrder(target1Order);
    if (target2Order != null && target2Order.OrderState == OrderState.Working)
        CancelOrder(target2Order);
}

// Call in OnOrderUpdate when position closes
if (Position.MarketPosition == MarketPosition.Flat)
    CleanupOrders();
```

#### 3. Rate-Limiting Violations
**What Happened:**
- Modified orders too frequently
- Apex account warnings
- Fixed with 1-second delay

**Prevention Protocol:**
```markdown
BEFORE modifying ANY order:
- [ ] Check: Do I have CanModifyOrder() check?
- [ ] Check: Is delay >= 1000ms?
- [ ] Check: Am I tracking lastModTime?
- [ ] Test: Verify no more than 1 mod/second
```

---

### Trading Setups (Proven Patterns)

#### ORB Long Setup
**Entry Criteria:**
- Price breaks above OR high + 1 tick
- Within first 30 minutes of RTH
- ATR > minimum threshold

**Stop Placement:**
- OR low - (ATR * 0.5)
- Immediate submission after entry

**Targets:**
- T1: Entry + (ATR * 1.5) - 50% position
- T2: Entry + (ATR * 3.0) - 50% position

**Trailing Stop:**
- Activates after T1 hit
- Trails at entry + (ATR * 0.5)
- Updates on every tick via OnMarketData

#### RMA Bounce Setup
**Entry Criteria:**
- Price touches 9 EMA during pullback
- 9 EMA > 15 EMA (uptrend confirmed)
- Within RTH session

**Stop Placement:**
- Recent swing low - (ATR * 1.0)

**Targets:**
- T1: Entry + (ATR * 2.0) - 50% position
- T2: Entry + (ATR * 4.0) - 50% position

**Trailing Stop:**
- Activates after T1 hit
- Trails below 9 EMA - (ATR * 0.5)

---

### Performance Lessons

#### Memory Management
**Lesson:** Unbounded collections cause memory leaks

**Prevention:**
```markdown
BEFORE creating ANY collection:
- [ ] Check: Is this collection bounded?
- [ ] Check: Do I clean up old entries?
- [ ] Check: Can I use fixed-size array instead?
- [ ] Test: Monitor memory over 12+ hours
```

**Pattern:**
```csharp
// ❌ NEVER
private List<double> prices = new List<double>();

// ✅ ALWAYS
private double[] recentPrices = new double[100];  // Fixed size
private int priceIndex = 0;
```

#### StringBuilder Pooling
**Lesson:** String concatenation creates garbage

**Prevention:**
```markdown
BEFORE logging or string building:
- [ ] Check: Am I using StringBuilder?
- [ ] Check: Do I reuse the same instance?
- [ ] Check: Do I Clear() before each use?
```

---

### Apex Compliance Lessons

#### Daily Loss Limit
**Lesson:** Must track in real-time, not end-of-day

**Prevention:**
```markdown
BEFORE going live:
- [ ] Check: Do I track daily P&L in real-time?
- [ ] Check: Do I auto-disable at 80% of limit?
- [ ] Check: Do I prevent new entries when close to limit?
- [ ] Test: Simulate hitting limit, verify auto-disable
```

#### Trailing Drawdown
**Lesson:** Resets based on account high-water mark

**Prevention:**
```markdown
BEFORE going live:
- [ ] Check: Do I track account high-water mark?
- [ ] Check: Do I calculate trailing DD correctly?
- [ ] Check: Do I auto-disable if approaching limit?
```

---

## Mandatory Review Checklist

### Before Writing ANY Code

```markdown
1. Check Close[0] Bug vault entry
   - [ ] Am I using Close[0] for real-time decisions?
   - [ ] Should I use OnMarketData instead?

2. Check Stranded Orders vault entry
   - [ ] Do I cancel all orders on position close?
   - [ ] Do I handle all order states?

3. Check Rate-Limiting vault entry
   - [ ] Do I have 1-second delay on modifications?
   - [ ] Do I use CanModifyOrder()?

4. Check Memory Management vault entry
   - [ ] Are my collections bounded?
   - [ ] Do I use StringBuilder for logging?

5. Check Apex Compliance vault entry
   - [ ] Do I track daily loss in real-time?
   - [ ] Do I auto-disable at limits?
```

### Before Deploying to Live

```markdown
1. Review ALL vault entries
2. Verify EVERY prevention protocol is implemented
3. Test EVERY scenario that caused past bugs
4. Document ANY new lessons learned
5. Update vault with new patterns
```

---

## Adding New Lessons

### When to Add to Vault

- Bug discovered and fixed
- New trading pattern proven profitable
- Performance optimization implemented
- Apex compliance issue resolved
- Rithmic feed behavior documented

### How to Add

1. **Document the Lesson:**
   ```markdown
   #### [Lesson Name]
   **What Happened:** [Description of issue/discovery]
   **Prevention Protocol:** [Checklist to prevent recurrence]
   **Code Pattern:** [Example implementation]
   ```

2. **Update Mandatory Checklist:**
   Add new item to "Before Writing ANY Code" section

3. **Test the Prevention:**
   Verify the checklist actually prevents the issue

4. **Commit to Git:**
   ```bash
   git add .agent/skills/trading-knowledge-vault/SKILL.md
   git commit -m "vault: Add [lesson name] to prevent [issue]"
   ```

---

## Vault Categories

### 1. Critical Bugs
- Issues that caused significant losses
- Bugs that violated Apex rules
- Errors that required emergency fixes

### 2. Trading Setups
- Proven entry patterns
- Stop/target configurations
- Trailing stop logic

### 3. Performance Lessons
- Memory optimizations
- Execution speed improvements
- Resource management

### 4. Compliance Lessons
- Apex account rules
- Rithmic feed behavior
- Order management best practices

### 5. Architecture Lessons
- Design patterns that worked
- Patterns that failed
- Scaling considerations

---

## AI Agent Protocol

### When AI Reads This Skill

**Step 1: Load Vault**
- Read ALL critical bugs
- Load relevant trading setups
- Review compliance lessons

**Step 2: Apply Filters**
- Before ANY code change, check vault
- Match current task to vault categories
- Apply relevant prevention protocols

**Step 3: Enforce Checklists**
- MUST complete all checklist items
- CANNOT skip prevention protocols
- MUST document if deviation needed

**Step 4: Update Vault**
- Add new lessons after fixes
- Document new patterns after testing
- Update checklists as needed

---

## Related Skills

- [ninjatrader-strategy-dev](../ninjatrader-strategy-dev/SKILL.md) - Coding patterns
- [live-price-tracking](../live-price-tracking/SKILL.md) - Close[0] bug details
- [apex-rithmic-trading](../apex-rithmic-trading/SKILL.md) - Compliance rules
- [trading-code-review](../trading-code-review/SKILL.md) - Pre-deployment checklist
- [project-lifecycle](../project-lifecycle/SKILL.md) - Version management
