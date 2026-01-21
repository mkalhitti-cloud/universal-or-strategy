---
name: universal-or-strategy
description: Universal OR Strategy project context for NinjaTrader 8 automated trading system. Use when working on the Universal OR Strategy codebase, understanding project architecture, reviewing implementation status, or making code changes to ORB, RMA, or other WSGTA strategies.
---
# Universal OR Strategy - Current Project Context

**Version:** V8.2 (4-target system with frequency-based trailing)
**Platform:** NinjaTrader 8, Apex funded accounts, Rithmic data feed
**Developer:** Mo (non-coder trader, WSGTA methodology expert)
**Status:** Production-ready for ORB, RMA, and TREND strategies

---

## Project Mission

Automated micro futures trading system (MES/MGC) implementing all 6 WSGTA strategies in a single unified framework with independent position tracking and strict risk management.

---

## Current Implementation Status

### ✅ Completed & Live (V5.3.1)

#### 1. Opening Range Breakout (ORB)
```csharp
// Setup: 9:30-10:00 ET range capture
// Trade: 10:00-12:00 ET breakout execution
// Exit: Forced at 12:00 ET

- Session high/low tracking
- Breakout detection (> high + 1 tick, < low - 1 tick)
- ATR-based position sizing
- Dual profit targets (TP1 @ 2×ATR, TP2 @ 4×ATR)
- 50/50 exit rule enforced
```

#### 2. RMA Click-Entry System
```csharp
// Interactive: Shift+Click on chart to place limit orders
// Auto-detection: Direction based on click position vs. EMAs

- EMA(9) and EMA(15) trend detection
- Price-to-chart coordinate conversion (calibrated for MES/MGC)
- Automatic direction assignment (long/short)
- ATR-based stops and targets
- 50/50 profit taking
```

#### 3. Live Price Tracking (V5.3 Critical Fix)
```csharp
// Replaced Close[0] with OnMarketData tick-level tracking

- OnMarketData hook for tick-by-tick updates
- GetLivePrice() helper with bid/ask fallbacks
- Trailing stops update between bar closes
- Rate-limited order modifications (1/second max)
- Rithmic disconnect detection
```

### 🔄 In Development

#### 4. Fibonacci Confluence Tool
```
NOT a strategy - alerting tool only

- Identifies session high/low
- Calculates Fib levels (23.6%, 38.2%, 50%, 61.8%, 78.6%)
- Alerts when price near Fib + EMA confluence
- Does NOT modify entry/exit rules
- Visual indicators on chart
```

#### 5. Additional WSGTA Strategies (Planned)
```
- FFMA (Far From Moving Average) - Mean reversion
- MOMO (Momentum) - RSI + volume breakouts
- DBDT (Double Bottom/Top) - Pattern detection
- TREND (9/15 EMA) - Pullback entries with trailing
```

---

## Critical Lessons Learned

### 1. Close[0] Bug (V5.3 Discovery)
**Problem:** Trailing stops only updated at bar close, losing intra-bar price movements
**Solution:** OnMarketData hook with tick-level price tracking
**Impact:** 50-90% improvement in trailing stop execution

### 2. Multi-AI Code Review Value
**Process:** 4-AI consensus (Claude, Gemini, DeepSeek, Grok)
**Benefit:** Caught Close[0] bug, memory leaks, Apex compliance issues
**Lesson:** Always run multi-AI review before live deployment

### 3. Memory Efficiency Critical
**Constraint:** Laptop at 80%+ RAM with 20+ charts
**Solution:** StringBuilder pooling, circular buffers, fixed-size collections
**Lesson:** Every byte counts in high-frequency trading

### 4. Rithmic Feed Specifics
**Characteristics:** Faster than Continuum but can disconnect briefly
**Solution:** Disconnect/reconnect handling, tick frequency monitoring
**Lesson:** Never assume data feed stability

---

## Architecture

### Order Management
```csharp
IsUnmanaged = true  // Full manual control

- Unmanaged orders for tick-level precision
- Rate-limited modifications (Apex compliance)
- Order rejection handling with graceful recovery
- Position tracking with external flatten detection
```

### Position Sizing (ATR-Based)
```csharp
contracts = riskDollars / (stopDistance * tickValue)
stopDistance = ATR(14) × multiplier

- Dynamic sizing based on volatility
- Max position limits from Order_Management.xlsx
- Single position per strategy
- Total risk capped across all strategies
```

### Profit Targets (50/50 Rule)
```csharp
TP1: Exit 50% @ entry ± (ATR × 2)
TP2: Exit 50% @ entry ± (ATR × 4)

- No exceptions to 50/50 split
- TP distances always ATR-based
- Partial exits tracked independently
```

### Stop Loss (NO EXCEPTIONS)
```csharp
Every entry IMMEDIATELY gets stop loss
stopLoss = entry ± (ATR × 2)

- Validation before submission (not at/past market)
- 4-tick minimum buffer from current price
- Trailing stops update tick-by-tick (OnMarketData)
- Rate-limited to 1 modification/second
```

---

## Trading Rules (WSGTA Compliance)

### Session Times
```
ORB Setup:    9:30-10:00 ET (range capture)
ORB Trade:   10:00-12:00 ET (breakout window)
ORB Exit:    12:00 ET (forced flatten)

RMA:          9:30-16:00 ET (all day)
MOMO:         9:30-12:00 ET (morning volatility)
```

### Risk Management
```csharp
Daily loss limit:  From Order_Management.xlsx
Max drawdown:      Account-specific (Apex rules)
Position limit:    Per strategy + total aggregate
Consecutive loss limit: Strategy-specific
```

### Order Rules
```
Entry types:     Market (ORB, MOMO) or Limit (RMA, FFMA)
Profit targets:  50% @ TP1, 50% @ TP2
Stop loss:       ATR × 2, immediate placement
Modifications:   Max 1 per second (Apex compliance)
```

---

## File Structure

```
NinjaTrader 8\bin\Custom\Strategies\
├── UniversalORStrategyV5_v5_2_MILESTONE.cs  ← CURRENT VERSION
├── [Previous versions in archive/]

Repository:
├── README.md              User documentation
├── CHANGELOG.md           Version history
├── PLAN.md               Development roadmap
├── CLAUDE.md             AI assistant context (this project)
├── Order_Management.xlsx  SINGLE SOURCE OF TRUTH for parameters
└── .agent/               IDE-agnostic agent configuration & state
    ├── PROJECT_STATE.md  Current development context
    ├── UNANSWERED_QUESTIONS.md Blockers and Q&A
    ├── state/            Session and version tracking
    └── skills/           Project-specific capabilities
```

---

## Order_Management.xlsx (Single Source of Truth)

### Sections
```
1. Strategy Parameters
   - ORB times, ATR periods, breakout thresholds

2. Position Sizing
   - Risk per trade ($)
   - Max positions per strategy
   - Total position limit

3. Profit Targets
   - TP1 distance (ATR multiplier)
   - TP2 distance (ATR multiplier)
   - Exit percentages (50/50)

4. Risk Management
   - Daily loss limit ($)
   - Max drawdown (%)
   - Trailing stop parameters

5. Session Settings
   - Timezone (ET default)
   - Session start/end times
   - Trading windows per strategy
```

**Rule:** ANY parameter change updates Excel FIRST, then code reads from it.

---

## Performance Targets

### Execution Speed (Priority #1)
```
Order submission:  < 50ms from signal
Hotkey response:   < 10ms (L/S/F keys)
Fill reporting:    < 200ms
OnMarketData:      < 1ms per tick
Position sizing:   < 0.5ms
```

### Memory (Priority #2)
```
Strategy footprint:  < 50 MB per instance
20+ charts:          < 1.5 GB total
Memory leak test:    Stable after 12+ hours
GC pauses:           < 10ms average
```

### Reliability (Priority #3)
```
Uptime:              24/5 without restart
Rithmic disconnect:  Graceful recovery
Order rejection:     Logged and handled
Position tracking:   100% accuracy
```

---

## Development Workflow

### Making Code Changes
```
1. Identify issue or improvement
2. Check relevant skill files first
3. Request complete, compilable code (no snippets)
4. Show before/after comparison
5. Specify exact file location (region, method)
6. Verify Apex compliance
7. Run multi-AI code review if critical
8. Test: Backtest → Paper → Sim → Live (1 contract)
```

### Code Review Checklist (Before ANY Change)
```
Critical Bugs:
- [ ] No Close[0] in real-time decisions
- [ ] OnMarketData implemented correctly
- [ ] GetLivePrice() fallback chain exists

Apex Compliance:
- [ ] Rate-limiting on order modifications
- [ ] Order error handling complete
- [ ] IsUnmanaged=true set correctly

Performance:
- [ ] StringBuilder pooling for logging
- [ ] Collections have fixed size
- [ ] Execution < 50ms target

WSGTA Rules:
- [ ] ATR-based position sizing
- [ ] 50/50 profit targets
- [ ] Stop loss always set immediately
```

---

## Current Development Phase

### Phase 2 (Current - 70% Complete)
```
✅ ORB system complete and tested
✅ RMA click-entry complete and calibrated
✅ Live price tracking fix implemented (V5.3)
🔄 Fibonacci confluence tool (non-strategy)
🔄 FFMA, MOMO, DBDT, TREND strategies
```

### Phase 3 (Planned)
```
- Multi-chart, single-account coordination
- Independent strategy position tracking
- Shared risk management across strategies
- Advanced alerts and notifications
```

### Phase 4 (Future)
```
- Multi-account support (scale to 20 accounts)
- Account routing optimization
- Aggregate risk management
- Portfolio-level position management
```

---

## Success Metrics

### Code Quality
```
✅ Compiles without errors/warnings
✅ Passes multi-AI code review
✅ Zero memory leaks (12+ hour test)
✅ Sub-50ms order execution
```

### Trading Performance
```
✅ ORB entry accuracy ± 1-2 ticks
✅ RMA click-to-price calibration accurate
✅ Trailing stops update between bars
✅ Apex compliance maintained
✅ Daily loss limits enforced
```

### Reliability
```
✅ No crashes in 12+ hour sessions
✅ Rithmic disconnect/reconnect handled
✅ Order rejections logged and recovered
✅ Position tracking 100% accurate
```

---

## Key Implementation Details

### Session High/Low Tracking
```csharp
// Resets daily, tracks during 9:30-10:00 ET window
private double sessionHigh = double.MinValue;
private double sessionLow = double.MaxValue;
private bool orComplete = false;

// Updated on every bar during OR window
// Locked at 10:00 ET, used for breakout detection
```

### Trailing Stop Management
```csharp
// Updates tick-by-tick via OnMarketData
private double highestPrice = 0;  // For long positions
private double lowestPrice = double.MaxValue;  // For shorts

// Rate-limited to 1 modification per second
// Only moves in favorable direction (never against)
```

### Position Tracking
```csharp
// Dictionary tracks multiple independent positions
private Dictionary<string, PositionInfo> activePositions;

// Handles external flattens (Control Center)
protected override void OnPositionUpdate(...)

// Cleans up on position close
```

---

## Common Pitfalls (Avoid These)

### ❌ Don't Do
```
1. Use Close[0] for real-time decisions
2. Modify orders more than 1/second
3. Skip stop loss on any entry
4. Create unbounded collections
5. Call indicators in OnMarketData
6. Ignore order rejection errors
7. Use hard-coded position sizes
8. Trade outside defined session windows
```

### ✅ Do This Instead
```
1. Use OnMarketData for tick-level tracking
2. Implement rate-limiting (CanModifyOrder)
3. ALWAYS set stop immediately after entry
4. Use circular buffers or fixed arrays
5. Cache indicator values in OnBarUpdate
6. Log and handle all order errors
7. Calculate position size from ATR
8. Enforce session time filters
```

---

## Testing Protocol

### Before Live Deployment
```
1. Compile without errors/warnings
2. Backtest 2-4 weeks of data
3. Paper trade 2-5 sessions
4. Sim account 1+ hour
5. Monitor memory for 1+ hour
6. Verify hotkeys work (L/S/F)
7. Test flatten functionality
8. Verify stops submit correctly
9. Verify targets execute at TP1/TP2
10. Test trailing stop updates
```

---

## Integration Points

### Order_Management.xlsx
```
Read parameters on strategy initialization
Never write to Excel from code
Human updates Excel, code reads
```

### Rithmic Data Feed
```
Tick-level data via OnMarketData
Expected rate: 50-200 ticks/min (RTH)
Disconnect detection: > 5 seconds without ticks
Recovery: Close positions, resume when reconnected
```

### Apex Account
```
Daily loss limit enforcement
Trailing drawdown monitoring
Order modification rate-limiting
Position size limits respected
```

---

## Related Skills

- [ninjatrader-strategy-dev.md](../core/ninjatrader-strategy-dev.md) - Code patterns
- [live-price-tracking.md](../references/live-price-tracking.md) - CRITICAL bug fix
- [wsgta-trading-system.md](../../trading/wsgta-trading-system.md) - Trading methodology
- [apex-rithmic-trading.md](../../trading/apex-rithmic-trading.md) - Account compliance
- [trading-code-review.md](../../trading/trading-code-review.md) - Pre-live checklist
- [trading-session-timezones.md](../../trading/trading-session-timezones.md) - Session timing

