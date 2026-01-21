---
name: ninjatrader-strategy-dev
description: NinjaTrader 8 strategy development patterns for high-performance trading. Use when developing or debugging NinjaScript strategies, implementing order management, optimizing execution speed, managing memory efficiency, or fixing common bugs like Close[0] usage in real-time decisions.
---

# NinjaTrader 8 Strategy Development - Code Patterns & Best Practices

**Context:** NinjaScript development patterns for high-performance trading strategies
**Platform:** NinjaTrader 8, C# 7.0, .NET Framework 4.8
**Focus:** Execution speed, memory efficiency, reliability

---

## CRITICAL: Close[0] vs. Live Price Tracking

### The Bug (Most Common NinjaTrader Mistake)
```csharp
// ❌ CRITICAL BUG - Only updates at bar close
protected override void OnBarUpdate()
{
    if (Close[0] > entryPrice + atrDistance)
    {
        SetStopLoss(newStop);  // Delayed until bar closes!
    }
}
```

### The Fix (Tick-Level Updates)
```csharp
// ✅ CORRECT - Updates on every tick
private double lastLivePrice = 0;

protected override void OnMarketData(MarketDataEventArgs e)
{
    if (e.MarketDataType != MarketDataType.Last) return;
    if (e.Instrument != Instrument) return;

    lastLivePrice = e.Price;

    // Update trailing stops in real-time
    if (lastLivePrice > entryPrice + atrDistance)
    {
        if (CanModifyOrder())
            SetStopLoss(newStop);
    }
}
```

**Impact:** Using Close[0] for real-time decisions delays updates until bar close, losing 50-90% of profit potential on trailing stops.

---

## OnMarketData Hook Pattern (Required for Live Trading)

### Basic Implementation
```csharp
protected override void OnMarketData(MarketDataEventArgs e)
{
    // Filter: Only process actual trades (Last), not bid/ask/volume
    if (e.MarketDataType != MarketDataType.Last)
        return;

    // Filter: Only process this instrument's data
    if (e.Instrument != Instrument)
        return;

    // Store live price
    double livePrice = e.Price;
    lastTickTime = DateTime.Now;

    // Update trailing stops, check exits, etc.
    ManageTrailingStops(livePrice);
}
```

### Performance Considerations
```csharp
// Execution target: < 1ms per tick
protected override void OnMarketData(MarketDataEventArgs e)
{
    if (e.MarketDataType != MarketDataType.Last) return;

    // AVOID: Heavy calculations here
    // double atr = ATR(14)[0];  // ❌ Slow indicator call

    // PREFER: Pre-calculated values
    double livePrice = e.Price;
    double newStop = highestPrice - cachedATR;  // ✅ Fast

    if (newStop > currentStop)
        SetStopLoss(newStop);
}
```

---

## GetLivePrice() Helper (Fallback Chain)

### Required Pattern
```csharp
private double GetLivePrice()
{
    // Priority 1: Bid/Ask midpoint (most accurate for live price)
    if (Ask > 0 && Bid > 0)
        return (Bid + Ask) / 2.0;

    // Priority 2: Ask (if bid not available)
    if (Ask > 0) return Ask;

    // Priority 3: Bid (if ask not available)
    if (Bid > 0) return Bid;

    // Last resort: Last bar close (stale data)
    return Close[0];
}
```

### When to Use
```csharp
// Use for stop/target placement when OnMarketData not firing
protected override void OnBarUpdate()
{
    double currentPrice = GetLivePrice();  // Not Close[0]!

    if (currentPrice > sessionHigh + TickSize)
    {
        EnterLong(qty, "ORB_Long");
        SetStopLoss(currentPrice - (atr * 2.0));
    }
}
```

---

## IsUnmanaged=true Architecture (Full Order Control)

### Setup
```csharp
protected override void OnStateChange()
{
    if (State == State.SetDefaults)
    {
        IsUnmanaged = true;  // Required for manual order management
        IsExitOnSessionCloseStrategy = false;
        IsFillLimitOnTouch = false;

        Name = "UniversalORStrategy";
        Calculate = Calculate.OnBarClose;
    }
}
```

### Order Submission Pattern
```csharp
private Order entryOrder = null;
private Order stopOrder = null;

private void ExecuteLong(int quantity)
{
    // Submit unmanaged entry order
    entryOrder = SubmitOrderUnmanaged(
        0,  // BarsInProgressIndex
        OrderAction.Buy,
        OrderType.Market,
        quantity,
        0,  // LimitPrice
        0,  // StopPrice
        "",  // OCO ID
        "Long_" + DateTime.Now.Ticks  // Unique name
    );
}

private void SetStopLoss(double stopPrice)
{
    // Submit unmanaged stop order
    stopOrder = SubmitOrderUnmanaged(
        0,
        OrderAction.Sell,
        OrderType.Stop,
        Position.Quantity,
        0,
        stopPrice,
        "",
        "Stop_" + entryOrder.Name
    );
}
```

---

## Rate-Limiting Order Modifications (Apex Compliance)

### Required Pattern
```csharp
private DateTime lastModTime = DateTime.MinValue;
private const int MOD_DELAY_MS = 1000;  // 1 second minimum

private bool CanModifyOrder()
{
    TimeSpan elapsed = DateTime.Now - lastModTime;

    if (elapsed.TotalMilliseconds < MOD_DELAY_MS)
        return false;

    lastModTime = DateTime.Now;
    return true;
}
```

### Usage in OnMarketData
```csharp
protected override void OnMarketData(MarketDataEventArgs e)
{
    if (e.MarketDataType != MarketDataType.Last) return;

    if (Position.MarketPosition == MarketPosition.Long)
    {
        double newStop = e.Price - (cachedATR * 2.0);

        // Only modify if rate-limit allows
        if (newStop > currentStop && CanModifyOrder())
        {
            ChangeOrder(stopOrder, stopOrder.Quantity, 0, newStop);
            currentStop = newStop;
        }
    }
}
```

---

## StringBuilder Pooling (Memory Efficiency)

### Anti-Pattern (Memory Leak)
```csharp
// ❌ BAD - Creates garbage on every call
protected override void OnBarUpdate()
{
    string msg = "Time: " + Time[0] + ", Price: " + Close[0];  // String allocation!
    Print(msg);
}
```

### Correct Pattern (Pooled StringBuilder)
```csharp
// ✅ GOOD - Reuses same StringBuilder
private StringBuilder logBuffer = new StringBuilder(256);

private void LogMessage(string prefix, double value)
{
    logBuffer.Clear();
    logBuffer.Append(prefix).Append(value.ToString("F2"));
    Print(logBuffer.ToString());
}

protected override void OnBarUpdate()
{
    LogMessage("Price: ", Close[0]);  // No string allocation
}
```

---

## Collection Management (Fixed Size)

### Anti-Pattern (Unbounded Growth)
```csharp
// ❌ BAD - Grows forever
private List<double> prices = new List<double>();

protected override void OnBarUpdate()
{
    prices.Add(Close[0]);  // Memory leak after hours of trading
}
```

### Correct Pattern (Circular Buffer)
```csharp
// ✅ GOOD - Fixed size, no growth
private double[] recentPrices = new double[100];
private int priceIndex = 0;

protected override void OnBarUpdate()
{
    recentPrices[priceIndex % 100] = Close[0];  // Circular buffer
    priceIndex++;
}
```

### Dictionary Cleanup
```csharp
// Prevent dictionary growth
private Dictionary<string, Order> activeOrders = new Dictionary<string, Order>();

protected override void OnOrderUpdate(Order order, ...)
{
    if (orderState == OrderState.Filled || orderState == OrderState.Cancelled)
    {
        // Remove completed orders
        if (activeOrders.ContainsKey(order.Name))
            activeOrders.Remove(order.Name);
    }
}
```

---

## Order Update Hook Pattern

### OnOrderUpdate Implementation
```csharp
protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice,
    int quantity, int filled, double averageFillPrice, OrderState orderState,
    DateTime time, ErrorCode errorCode, string nserror)
{
    if (order == null) return;

    // Handle errors first
    if (errorCode != ErrorCode.NoError)
    {
        Print($"Order error: {order.Name} - {nserror}");
        return;
    }

    // Track order states
    if (orderState == OrderState.Filled)
    {
        Print($"Order FILLED: {order.Name} @ {averageFillPrice}");
        HandleFill(order);
    }
    else if (orderState == OrderState.Rejected)
    {
        Print($"Order REJECTED: {order.Name} - {nserror}");
        HandleRejection(order);
    }
    else if (orderState == OrderState.Cancelled)
    {
        Print($"Order CANCELLED: {order.Name}");
        HandleCancellation(order);
    }
}
```

---

## Rithmic Disconnect Detection

### Connection Monitoring
```csharp
private DateTime lastTickTime = DateTime.Now;
private bool dataFeedConnected = true;

protected override void OnMarketData(MarketDataEventArgs e)
{
    if (e.MarketDataType == MarketDataType.Last)
    {
        lastTickTime = DateTime.Now;
        dataFeedConnected = true;
    }
}

protected override void OnBarUpdate()
{
    // Check for stale data
    TimeSpan timeSinceLastTick = DateTime.Now - lastTickTime;

    if (timeSinceLastTick.TotalSeconds > 5)
    {
        if (dataFeedConnected)
        {
            Print("WARNING: Data feed disconnect detected");
            dataFeedConnected = false;

            // Close positions if necessary
            if (Position.MarketPosition != MarketPosition.Flat)
                FlattenAll("Data disconnect");
        }
    }
}
```

---

## Performance Optimization Patterns

### Cache Expensive Calculations
```csharp
// ❌ BAD - Recalculates every tick
protected override void OnMarketData(MarketDataEventArgs e)
{
    double atr = ATR(14)[0];  // Expensive!
    double newStop = e.Price - atr;
}

// ✅ GOOD - Cache ATR value
private double cachedATR = 0;
private int lastATRBar = -1;

protected override void OnBarUpdate()
{
    // Update ATR once per bar
    if (CurrentBar != lastATRBar)
    {
        cachedATR = ATR(14)[0];
        lastATRBar = CurrentBar;
    }
}

protected override void OnMarketData(MarketDataEventArgs e)
{
    double newStop = e.Price - cachedATR;  // Fast!
}
```

### Avoid Indicator Calls in Hot Paths
```csharp
// Calculate indicators in OnBarUpdate, use in OnMarketData
private double ema9 = 0;
private double ema15 = 0;

protected override void OnBarUpdate()
{
    ema9 = EMA(9)[0];
    ema15 = EMA(15)[0];
}

protected override void OnMarketData(MarketDataEventArgs e)
{
    // Fast lookups, no indicator recalculation
    if (e.Price > ema9 && ema9 > ema15)
    {
        // Uptrend detected
    }
}
```

---

## Error Handling Patterns

### Null Checks
```csharp
protected override void OnBarUpdate()
{
    // Always check for sufficient bars
    if (CurrentBar < BarsRequiredToPlot)
        return;

    // Check for null objects
    if (Position == null)
        return;

    // Proceed with logic
}
```

### Try-Catch for External Calls
```csharp
protected override void OnBarUpdate()
{
    try
    {
        // External operation that might fail
        double accountValue = Account.Get(AccountItem.CashValue, Currency.UsDollar);
    }
    catch (Exception ex)
    {
        Print($"Error accessing account: {ex.Message}");
        return;
    }
}
```

---

## Session State Detection

### Time-Based Session Checks
```csharp
private bool IsRTH()
{
    TimeSpan now = Time[0].TimeOfDay;
    return now >= new TimeSpan(9, 30, 0) && now < new TimeSpan(16, 0, 0);
}

private bool IsORWindow()
{
    TimeSpan now = Time[0].TimeOfDay;
    return now >= new TimeSpan(9, 30, 0) && now < new TimeSpan(10, 0, 0);
}
```

### Cached Session Checks (Performance)
```csharp
private bool cachedIsRTH = false;
private DateTime lastSessionCheck = DateTime.MinValue;

private bool IsRTHCached()
{
    // Only recalculate once per bar
    if (Time[0] != lastSessionCheck)
    {
        cachedIsRTH = IsRTH();
        lastSessionCheck = Time[0];
    }
    return cachedIsRTH;
}
```

---

## Common Anti-Patterns (Avoid These)

### 1. Using Close[0] for Real-Time Decisions
Already covered - use OnMarketData instead.

### 2. Not Checking BarsRequiredToPlot
```csharp
// ❌ BAD - Crashes on early bars
double ema = EMA(20)[0];

// ✅ GOOD - Check sufficient bars first
if (CurrentBar < 20) return;
double ema = EMA(20)[0];
```

### 3. Not Filtering MarketDataType
```csharp
// ❌ BAD - Processes bid/ask/volume updates too
protected override void OnMarketData(MarketDataEventArgs e)
{
    UpdateLogic(e.Price);  // Fires too often!
}

// ✅ GOOD - Only process actual trades
protected override void OnMarketData(MarketDataEventArgs e)
{
    if (e.MarketDataType != MarketDataType.Last) return;
    UpdateLogic(e.Price);
}
```

### 4. Not Validating Stop Prices
```csharp
// ❌ BAD - May submit invalid stop
SetStopLoss(entryPrice - atr);

// ✅ GOOD - Validate before submission
double stopPrice = entryPrice - (atr * 2.0);
if (stopPrice > 0 && stopPrice < GetLivePrice() - (TickSize * 4))
    SetStopLoss(stopPrice);
```

---

## Testing Checklist

Before deploying any strategy:
- [ ] Compiles without errors or warnings
- [ ] OnMarketData filters for Last only
- [ ] No Close[0] in real-time decision paths
- [ ] Rate-limiting on order modifications
- [ ] StringBuilder pooling for logging
- [ ] Collections have fixed size or cleanup
- [ ] Error handling for order updates
- [ ] Disconnect detection implemented
- [ ] Null checks before object access
- [ ] BarsRequiredToPlot check in OnBarUpdate
- [ ] Memory stable after 1+ hour test
- [ ] Execution speed < 50ms for entries
- [ ] Delegated Deployment: Verified `call_gemini_flash` usage for save/deploy
- [ ] Continuity Verified: Updated `.agent/PROJECT_STATE.md`

---

## Performance Benchmarks

### Execution Speed Targets
- OnBarUpdate: < 5ms total
- OnMarketData: < 1ms per tick
- Order submission: < 50ms from signal
- Position sizing: < 0.5ms

### Memory Targets
- Strategy footprint: < 50 MB
- No growth after 12+ hours
- GC pauses: < 10ms

---

## Related Skills
- [live-price-tracking](../live-price-tracking/SKILL.md) - Critical Close[0] bug details
- [apex-rithmic-trading](../apex-rithmic-trading/SKILL.md) - Account compliance
- [trading-code-review](../trading-code-review/SKILL.md) - Quality checklist
- [wsgta-trading-system](../wsgta-trading-system/SKILL.md) - Trading rules
- [delegation-bridge](../delegation-bridge/SKILL.md) - Cost-optimized execution & context saving
- [wearable-project](../antigravity-core/wearable-project.md) - Portability standards
