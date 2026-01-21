---
name: trading-code-review
description: Pre-live deployment checklist for NinjaTrader trading strategies. Use before deploying any code changes to live trading, when reviewing strategy code for critical bugs, or verifying Apex compliance and WSGTA trading rules.
---
# NinjaTrader Trading Code Review - Pre-Live Checklist

**Context:** Code quality verification before live trading
**Platform:** NinjaTrader 8, Rithmic data feed, Apex funded accounts
**Critical:** Run this checklist on EVERY code change before deployment
**Universal Path:** `${PROJECT_ROOT}`
**Executors:** ${BRAIN} (Reasoning), ${HANDS} (Gemini Flash via delegation_bridge)

---

## Critical Bug Checks (MUST PASS - No Exceptions)

### 1. Close[0] Bug Hunt - PRIORITY #1
**Search Pattern:** `Close[0]` used in real-time price decisions

```csharp
// ❌ CRITICAL BUG
if (Close[0] > entryPrice + atrDistance)
    SetStopLoss(newStop);  // Only updates at bar close!

// ✅ CORRECT
if (lastLivePrice > entryPrice + atrDistance)  // OnMarketData price
    SetStopLoss(newStop);
```

**Verification Steps:**
1. Search entire file for `Close[0]`
2. Check context: Is it used for trailing stops, live exits, or tick-level decisions?
3. If YES → **FAIL - Fix required**
4. All real-time decisions must use OnMarketData or GetLivePrice()

**Impact:** Trailing stops delayed until bar close, losing 50-90% of profit potential

---

### 2. OnMarketData Implementation Check
**Required Pattern:**

```csharp
protected override void OnMarketData(MarketDataEventArgs e)
{
    if (e.MarketDataType != MarketDataType.Last) return;
    if (e.Instrument != Instrument) return;

    double livePrice = e.Price;
    lastTickTime = DateTime.Now;

    // Update trailing stops or live price tracking here
}
```

**Verification:**
- [ ] Hook exists and is implemented
- [ ] Filters for `MarketDataType.Last` only
- [ ] Checks correct `Instrument`
- [ ] Updates trailing stops tick-by-tick
- [ ] No recursive calls or infinite loops
- [ ] Execution time < 1ms per tick

**Impact:** Without this, no tick-level price tracking possible

---

### 3. GetLivePrice() Helper
**Required Pattern:**

```csharp
private double GetLivePrice()
{
    if (Ask > 0 && Bid > 0)
        return (Bid + Ask) / 2.0;

    if (Ask > 0) return Ask;
    if (Bid > 0) return Bid;

    return Close[0];  // Last resort only
}
```

**Verification:**
- [ ] Function exists with fallback chain
- [ ] Bid/Ask midpoint is primary source
- [ ] Close[0] is last resort, not primary
- [ ] Used throughout code for live price decisions

**Impact:** Missing fallbacks cause crashes during data gaps

---

## Apex Compliance Checks (Account Safety)

### 4. Rate-Limiting Order Modifications
**Required Pattern:**

```csharp
private DateTime lastModTime = DateTime.MinValue;
private const int MOD_DELAY_MS = 1000;

private bool CanModifyOrder()
{
    if ((DateTime.Now - lastModTime).TotalMilliseconds < MOD_DELAY_MS)
        return false;

    lastModTime = DateTime.Now;
    return true;
}
```

**Verification:**
- [ ] Rate-limiting function exists
- [ ] Minimum 1000ms between modifications
- [ ] Applied to ALL stop/limit changes
- [ ] No code paths bypass this check

**Impact:** Excessive modifications trigger Apex account penalties

---

### 5. Order Error Handling
**Required Pattern:**

```csharp
protected override void OnOrderUpdate(Order order, ...)
{
    if (order == null) return;

    if (errorCode != ErrorCode.NoError)
    {
        Print($"Order Error: {nserror}");
        // Handle gracefully - don't crash
        return;
    }

    if (orderState == OrderState.Filled)
        HandleFill(order);
    else if (orderState == OrderState.Rejected)
        HandleRejection(order);
}
```

**Verification:**
- [ ] OnOrderUpdate or OnExecutionUpdate implemented
- [ ] Null check for orders
- [ ] ErrorCode validation
- [ ] All order states handled (Filled, Cancelled, Rejected, etc.)
- [ ] Errors logged, not ignored

**Impact:** Silent failures cause position tracking errors

---

### 6. IsUnmanaged=true Verification
**Required:**

```csharp
protected override void OnStateChange()
{
    if (State == State.SetDefaults)
    {
        IsUnmanaged = true;
        // ...
    }
}
```

**Verification:**
- [ ] `IsUnmanaged = true` set in OnStateChange
- [ ] No mixed managed/unmanaged calls
- [ ] All orders placed manually (not auto-managed)

**Impact:** Wrong setting loses full order control

---

## Performance & Memory Checks

### 7. StringBuilder Pooling (Memory Efficiency)
**Required Pattern:**

```csharp
private StringBuilder logBuffer = new StringBuilder();

private void LogMessage(string msg)
{
    logBuffer.Clear();
    logBuffer.Append(Time[0].ToString("HH:mm:ss")).Append(" - ");
    logBuffer.Append(msg);
    Print(logBuffer.ToString());
}
```

**Verification:**
- [ ] No string concatenation in loops (`str + str`)
- [ ] StringBuilder reused (not `new StringBuilder()` every call)
- [ ] Logging won't cause GC pauses

**Impact:** String allocation causes memory leaks, GC pauses

---

### 8. Collection Management
**Anti-Pattern:**

```csharp
// ❌ BAD - Grows unbounded
private List<double> prices = new List<double>();
void OnBarUpdate() { prices.Add(Close[0]); }  // Grows forever

// ✅ GOOD - Fixed size
private double[] prices = new double[100];
private int index = 0;
void OnBarUpdate() { prices[index++ % 100] = Close[0]; }  // Circular buffer
```

**Verification:**
- [ ] Lists have max size or cleared regularly
- [ ] No unbounded collections
- [ ] Circular buffers for limited history
- [ ] Memory usage constant over time

**Impact:** Unbounded growth causes crashes after hours

---

### 9. Execution Speed (Latency Check)
**Targets:**
- OnBarUpdate: < 5ms total
- OnMarketData: < 1ms per tick
- Order submission: < 50ms from signal

**Verification:**
- [ ] No heavy calculations in OnBarUpdate
- [ ] OnMarketData is lightweight
- [ ] No file I/O in trading logic
- [ ] No external API calls during execution
- [ ] Stop/target calculations pre-computed when possible

**Testing:**
```csharp
DateTime start = DateTime.Now;
// ... code to test ...
double elapsedMs = (DateTime.Now - start).TotalMilliseconds;
Print($"Execution: {elapsedMs}ms");
```

**Impact:** Slow execution causes slippage, poor fills

---

## Rithmic Data Feed Checks

### 10. Disconnect Handling
**Required Pattern:**

```csharp
private DateTime lastTickTime = DateTime.Now;

protected override void OnMarketData(MarketDataEventArgs e)
{
    if (e.MarketDataType == MarketDataType.Last)
        lastTickTime = DateTime.Now;
}

protected override void OnBarUpdate()
{
    if ((DateTime.Now - lastTickTime).TotalSeconds > 5)
    {
        Print("WARNING: Rithmic disconnect detected");
        // Pause trading or close positions
    }
}
```

**Verification:**
- [ ] Tracks last tick time
- [ ] Detects staleness (> 5 seconds without data)
- [ ] Pauses or stops trading during disconnects
- [ ] Logs disconnect events

**Impact:** Trading on stale data causes bad fills

---

### 11. Tick Frequency Monitoring
**Expected Rates:**
- RTH (9:30-16:00 ET): 50-200 ticks/min
- Overnight: 5-50 ticks/min

**Verification:**

```csharp
private int tickCount = 0;

protected override void OnMarketData(MarketDataEventArgs e)
{
    if (e.MarketDataType == MarketDataType.Last)
    {
        tickCount++;

        if (tickCount % 100 == 0)
            Print($"Ticks: {tickCount}");
    }
}
```

**Check:** During RTH, should see 50+ ticks/min minimum

---

## WSGTA Trading Rules Compliance

### 12. ATR-Based Position Sizing
**Required Pattern:**

```csharp
private int CalculatePositionSize(double riskDollars, double atrMultiplier)
{
    double atr = ATR(14)[0];
    double stopDistance = atr * atrMultiplier;
    double tickValue = Instrument.MasterInstrument.PointValue;

    int contracts = (int)Math.Floor(riskDollars / (stopDistance * tickValue));

    return Math.Min(contracts, MAX_POSITION_SIZE);
}
```

**Verification:**
- [ ] Uses ATR(14) for stop distance
- [ ] Calculates contracts from risk amount
- [ ] Respects max position size
- [ ] No hard-coded position sizes

**Impact:** Wrong sizing violates risk management

---

### 13. Dual Profit Targets (50/50 Rule)
**Required Pattern:**

```csharp
// At TP1: Exit 50% of position
if (livePrice >= tp1Price && !tp1Hit)
{
    int halfQty = position.Quantity / 2;
    ExitLong(halfQty, "TP1");
    tp1Hit = true;
}

// At TP2: Exit remaining 50%
if (livePrice >= tp2Price && tp1Hit)
{
    ExitLong("TP2");
}
```

**Verification:**
- [ ] TP1 exits exactly 50% of position
- [ ] TP2 exits remaining 50%
- [ ] No other partial exit levels
- [ ] TP distances based on ATR (typically 2× and 4×)

---

### 14. Stop Loss Enforcement
**Required Pattern:**

```csharp
private void EnterWithStop(double entryPrice, double atr, bool isLong)
{
    double stopPrice = isLong ? entryPrice - (atr * 2.0) : entryPrice + (atr * 2.0);

    if (stopPrice <= 0 || (isLong && stopPrice >= entryPrice) || (!isLong && stopPrice <= entryPrice))
    {
        Print("ERROR: Invalid stop - order rejected");
        return;
    }

    EnterLong(...);
    SetStopLoss(stopPrice);
}
```

**Verification:**
- [ ] Every entry IMMEDIATELY gets stop loss
- [ ] Stop loss never 0 or invalid
- [ ] No orders placed without stops
- [ ] Code refuses to trade if stop not set

**Impact:** No stop = runaway losses

---

## Strategy-Specific Checks

### Opening Range Breakout (ORB)
- [ ] Range captured only 9:30-10:00 ET
- [ ] High/Low correctly tracked (not swapped)
- [ ] Breakout trigger correct (> High + 1 tick, < Low - 1 tick)
- [ ] Forced exit at 12:00 ET
- [ ] Only ONE position per direction

### Regular Moving Average (RMA)
- [ ] EMA(9) and EMA(15) calculated
- [ ] Shift+Click detection working
- [ ] Limit order placed at clicked price
- [ ] Direction correctly detected
- [ ] ATR-based stops applied

### Momentum (MOMO)
- [ ] Volume surge detected
- [ ] RSI calculated correctly
- [ ] Only trades 9:30-12:00 ET
- [ ] Quick exits (TP1 at 0.5× ATR)
- [ ] Smaller position size

---

## Final Pre-Live Checklist

**Code Quality:**
- [ ] Compiles without errors
- [ ] Compiles without warnings
- [ ] All critical bugs fixed (Close[0], OnMarketData, etc.)
- [ ] Memory stable (tested 1+ hour)
- [ ] Execution < 50ms

**Safety:**
- [ ] All stops set properly
- [ ] Rate-limiting implemented
- [ ] Error handling complete
- [ ] Data feed validated
- [ ] Apex compliance verified

**Trading Rules:**
- [ ] Position sizing correct (ATR-based)
- [ ] Entry/exit rules followed
- [ ] TP1/TP2 at right levels (50/50)
- [ ] Daily loss limit set
- [ ] Session times respected

**Data Validation:**
- [ ] Rithmic feed live (50+ ticks/min during RTH)
- [ ] No stale data detected
- [ ] Price data accurate (compare with chart)
- [ ] Order fills tracking correctly
- [ ] Account balance correct

**Testing:**
- [ ] Backtested 2+ weeks successfully
- [ ] Paper traded 2-5 sessions
- [ ] Forward tested on 1 contract
- [ ] Know emergency stop procedure
- [ ] Ready for live trading

---

## Quick Bug Search Commands

Search for these patterns in your code:

```
1. Search: "Close\[0\]"
   → Check if used in real-time decisions

2. Search: "OnMarketData"
   → Verify implementation exists

3. Search: "SetStopLoss"
   → Verify called immediately after entry

4. Search: "DateTime"
   → Check rate-limiting implementation

5. Search: "List<"
   → Check for unbounded collections
```

---

## Related Skills
- [ninjatrader-strategy-dev](../ninjatrader-strategy-dev/SKILL.md) - Development patterns
- [live-price-tracking](../live-price-tracking/SKILL.md) - Critical Close[0] bug
- [wsgta-trading-system](../wsgta-trading-system/SKILL.md) - Trading rules
- [apex-rithmic-trading](../apex-rithmic-trading/SKILL.md) - Compliance rules
- [delegation-bridge](../delegation-bridge/SKILL.md) - Safe deployment execution
- [wearable-project](../antigravity-core/wearable-project.md) - Portability standards

