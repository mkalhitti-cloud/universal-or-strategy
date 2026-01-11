# UniversalORStrategy V5.3.1 Project Context

**Current Status:** V5.3.1 - Live price tracking with RMA improvements
**Last Updated:** January 2025
**Developer:** Mo (non-technical trader, WSGTA methodology expert)

## Project Overview

Automated micro futures trading system for MES and MGC using NinjaTrader 8 on Apex funded accounts with Rithmic data feeds.

**Architecture:** Multi-strategy framework (ORB, RMA, FFMA, MOMO, DBDT, TREND)
**Goal:** Implement all 6 WSGTA strategies in single system with independent position tracking

## Current Implementation (V5.3.1)

### Completed Features

#### ✅ Opening Range Breakout (ORB)
- 9:30-10:00 ET range capture
- Automatic breakout detection
- Entry at high/low breach + 1 tick
- ATR-based position sizing
- Dual profit targets (TP1 50%, TP2 100%)

#### ✅ RMA Click-Entry System
- Shift+Click to place limit orders
- Automatic direction detection (up/down)
- ATR-based risk management
- Click-to-price conversion calibrated for MES/MGC
- Dual profit targets

#### ✅ Live Price Tracking (V5.3 Fix)
- OnMarketData hook for tick-level updates
- GetLivePrice() helper with fallbacks
- Trailing stops update between bar closes
- Rate-limited order modifications (1/second max)

### In Development

#### 🔄 Fibonacci Retracement Confluence
- Measuring from session high/low
- Recent swing identification
- Confluence alerts when trades align with Fib levels
- Visual indicators on chart
- Does NOT change entry rules or position sizing

#### 🔄 FFMA, MOMO, DBDT, TREND Strategies
- Implementing remaining 4 WSGTA strategies
- Independent position tracking
- Shared risk management infrastructure

## Key Learnings

### Critical Bug: Close[0] vs. Live Price
**Impact:** Trailing stops only updated at bar close, losing sub-bar movements
**Solution:** OnMarketData hook with tick-level price tracking
**Lesson:** Always audit price update triggers, especially for stop management

### Multi-AI Code Review Value
Conducting consensus reviews (Claude, Gemini, DeepSeek, Grok) caught:
- Close[0] bug that single review missed
- Memory leak patterns
- Apex compliance issues
- Performance optimization opportunities

### Memory Efficiency Critical
System runs on laptop with limited RAM (80%+ usage reported).
- StringBuilder pooling reduces GC pressure
- OnBarClose event batching
- Efficient order tracking

### Rithmic Feed Specifics
- Faster than Continuum but can disconnect briefly
- Requires disconnect/reconnect handling
- Works well with IsUnmanaged=true
- Tick data available immediately (no delay)

## Trading Rules (WSGTA Compliance)

### Position Sizing
All positions follow ATR-based risk calculation:
- Risk per trade: Defined in Order_Management.xlsx
- Position size: Risk ÷ (Stop Loss - Entry) × Contract Multiplier
- Maximum positions: Account rules compliance

### Order Management
- Entry: Limit or market (strategy dependent)
- Profit targets: 50% @ TP1, 50% @ TP2
- Stops: ATR-based, updated tick-by-tick
- Order rate limit: Max 1 modification per second

### Session Times
**NY Session (EST):**
- ORB: 9:30-10:00 (range build), 10:00-12:00 (breakout)
- RMA: 9:30-16:00 (regular market hours)

### Risk Management
- Daily loss limit: Set in Order_Management.xlsx
- Max drawdown: Account rule limit

## File Structure

```
..\NinjaTrader 8\bin\Cbi\Strategies\
├── UniversalORStrategyV5_v5_2_MILESTONE.cs ← CURRENT VERSION
├── UniversalORStrategyV*.cs (archive versions)
├── Order_Management.xlsx ← SINGLE SOURCE OF TRUTH
├── CHANGELOG.md
├── README.md
├── .gemini/skills/ ← You are using this now
```

## Order_Management.xlsx Structure

**Single source of truth for all trading parameters**

Sections:
- **Strategy Parameters:** ORB times, ATR periods, thresholds
- **Position Sizing:** Risk per trade, max positions
- **Profit Targets:** TP1 and TP2 percentages
- **Risk Management:** Daily loss limit, max drawdown
- **Session Settings:** Timezone-specific parameters
- **Rithmic Settings:** Data feed configuration

## Performance Targets

### Execution (Priority #1)
- Order submission: < 50ms from signal
- Target: Sub-50ms round-trip to exchange

### Memory (Priority #2)
- Strategy footprint: < 50 MB per instance
- 20+ simultaneous charts: < 1.5 GB total
- No memory leaks: Stable after 12+ hours

### Reliability (Priority #3)
- Uptime: 24/5 without restart
- Rithmic disconnect: Graceful handling

## Development Workflow

### Making Changes
1. Identify issue or improvement
2. Check relevant skill files (especially live-price-tracking.md)
3. Request complete, compilable code
4. Show before/after comparison
5. Verify Apex compliance

### Code Review Checklist
- [ ] Uses OnMarketData for live prices
- [ ] No Close[0] for real-time decisions
- [ ] Rate-limiting on order modifications
- [ ] GetLivePrice() helper used
- [ ] Apex compliance verified
- [ ] Memory impact < 5 MB
- [ ] Execution latency < 100ms
- [ ] Proper error handling
- [ ] Logging for debugging
- [ ] WSGTA rules enforced

## Related Skills
- ninjatrader-strategy-dev.md - Development patterns
- nt8-common-bugs.md - Known issues and fixes
- live-price-tracking.md - CRITICAL fix documentation
- wsgta-trading-system.md - Trading methodology
- apex-rithmic-trading.md - Account and data feed specifics
- micro-futures-specifications.md - MES/MGC contract details

## For Gemini
When assisting, reference this session context and mention which skill file is relevant.
