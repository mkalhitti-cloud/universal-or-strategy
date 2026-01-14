---
name: apex-rithmic-trading
description: Apex Trader Funding account compliance and Rithmic data feed optimization for NinjaTrader 8. Use when implementing daily loss limits, trailing drawdown monitoring, order rate-limiting, Rithmic disconnect detection, or ensuring Apex account compliance.
---
# Apex Trader Funding + Rithmic Data Feed - Implementation Guide

**Context:** Funded trading account rules and data feed optimization
**Platform:** NinjaTrader 8 with Rithmic real-time data
**Critical:** Account compliance and execution quality

---

## Apex Account Rules (Hard Limits)

### Daily Loss Limit
```csharp
private double dailyPnL = 0;
private double dailyLossLimit = -500;  // From Order_Management.xlsx
private DateTime lastPnLCheck = DateTime.MinValue;

protected override void OnExecutionUpdate(Execution execution, ...)
{
    // Calculate P&L from execution
    double fillPnL = execution.Quantity * (execution.Price - avgEntry) * Instrument.MasterInstrument.PointValue;
    dailyPnL += fillPnL;

    if (dailyPnL <= dailyLossLimit)
    {
        Print($"DAILY LOSS LIMIT HIT: ${dailyPnL:F2}");
        FlattenAll("Daily loss limit");
        allowTrading = false;

        // Disable strategy for rest of day
        Enabled = false;
    }
}
```

**Rules:**
- Limit resets at 00:00 ET (calendar day)
- If hit, account locks until next day
- Track in real-time, not just at close
- Stop trading at 80% of limit (safety buffer)

### Trailing Drawdown Limit
```csharp
private double accountPeak = startingCapital;
private double maxDrawdownAllowed = 2000;  // From account rules

protected override void OnExecutionUpdate(Execution execution, ...)
{
    double currentEquity = Account.Get(AccountItem.CashValue, Currency.UsDollar);

    // Update peak
    if (currentEquity > accountPeak)
        accountPeak = currentEquity;

    // Check drawdown
    double currentDrawdown = accountPeak - currentEquity;

    if (currentDrawdown >= maxDrawdownAllowed)
    {
        Print($"TRAILING DRAWDOWN LIMIT HIT: ${currentDrawdown:F2}");
        FlattenAll("Drawdown limit");
        allowTrading = false;
        Enabled = false;
    }
}
```

**Rules:**
- Tracks maximum account equity reached
- Drawdown calculated from peak, not starting capital
- No recovery possible if hit - account closes
- Monitor continuously during trading

---

## Order Management Compliance

### Rate-Limiting (1 Modification Per Second)
```csharp
private DateTime lastModTime = DateTime.MinValue;
private const int MOD_DELAY_MS = 1000;

private bool CanModifyOrder()
{
    TimeSpan elapsed = DateTime.Now - lastModTime;

    if (elapsed.TotalMilliseconds < MOD_DELAY_MS)
    {
        Print($"Modification blocked - {MOD_DELAY_MS - elapsed.TotalMilliseconds:F0}ms remaining");
        return false;
    }

    lastModTime = DateTime.Now;
    return true;
}

// Usage example
protected override void OnMarketData(MarketDataEventArgs e)
{
    if (e.MarketDataType != MarketDataType.Last) return;

    if (needsStopUpdate && CanModifyOrder())
    {
        SetStopLoss(newStopPrice);
    }
}
```

**Rules:**
- Absolute minimum 1000ms between modifications
- Applies to stops, limits, all order changes
- Violation triggers account review/warning
- Track per-order, not globally

### Order Rejection Handling
```csharp
protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice,
    int quantity, int filled, double averageFillPrice, OrderState orderState,
    DateTime time, ErrorCode errorCode, string nserror)
{
    if (order == null) return;

    // Handle rejections
    if (orderState == OrderState.Rejected)
    {
        Print($"Order REJECTED: {order.Name} - {nserror}");

        // Common rejection reasons
        if (nserror.Contains("Insufficient"))
        {
            Print("Insufficient buying power - reduce position size");
        }
        else if (nserror.Contains("Invalid price"))
        {
            Print("Invalid price - stop too close to market");
        }
        else if (nserror.Contains("Duplicate"))
        {
            Print("Duplicate order - check order tracking");
        }

        // Clean up tracking
        RemoveOrderFromTracking(order);
    }
}
```

**Common Rejection Causes:**
1. Stop price at or past market price
2. Insufficient buying power
3. Duplicate order names
4. Invalid symbol/instrument
5. Order size exceeds position limit

---

## Rithmic Data Feed Optimization

### Connection Verification
```csharp
private DateTime lastTickTime = DateTime.Now;
private int tickCount = 0;
private bool rithmicConnected = false;

protected override void OnMarketData(MarketDataEventArgs e)
{
    if (e.MarketDataType != MarketDataType.Last) return;
    if (e.Instrument != Instrument) return;

    lastTickTime = DateTime.Now;
    tickCount++;
    rithmicConnected = true;
}

protected override void OnBarUpdate()
{
    // Check connection health
    TimeSpan timeSinceLastTick = DateTime.Now - lastTickTime;

    if (timeSinceLastTick.TotalSeconds > 5 && IsRTH())
    {
        rithmicConnected = false;
        Print("WARNING: Rithmic disconnect - no ticks for 5+ seconds");

        // Pause trading
        if (Position.MarketPosition != MarketPosition.Flat)
        {
            Print("Closing positions due to disconnect");
            FlattenAll("Data feed disconnect");
        }

        allowTrading = false;
    }
    else if (!rithmicConnected && timeSinceLastTick.TotalSeconds < 2)
    {
        rithmicConnected = true;
        Print("Rithmic reconnected");
        allowTrading = true;
    }
}
```

### Tick Frequency Monitoring
```csharp
private int ticksThisMinute = 0;
private DateTime lastMinuteCheck = DateTime.Now;

protected override void OnMarketData(MarketDataEventArgs e)
{
    if (e.MarketDataType != MarketDataType.Last) return;

    ticksThisMinute++;

    // Check frequency every minute
    if ((DateTime.Now - lastMinuteCheck).TotalMinutes >= 1.0)
    {
        Print($"Tick rate: {ticksThisMinute}/min");

        // Warn if degraded during RTH
        if (IsRTH() && ticksThisMinute < 20)
        {
            Print($"WARNING: Low tick rate during RTH - {ticksThisMinute}/min (expect 50+)");
        }

        // Reset counter
        ticksThisMinute = 0;
        lastMinuteCheck = DateTime.Now;
    }
}
```

**Expected Tick Rates:**
- RTH (9:30-16:00 ET): 50-200 ticks/min
- Pre-market (4:00-9:30 ET): 5-20 ticks/min
- Overnight (18:00-4:00 ET): 5-50 ticks/min

---

## Execution Quality Metrics

### Fill Latency Tracking
```csharp
private Dictionary<string, DateTime> orderSubmitTimes = new Dictionary<string, DateTime>();

private void SubmitOrderWithTracking(Order order)
{
    orderSubmitTimes[order.Id] = DateTime.Now;
    SubmitOrder(order);
}

protected override void OnOrderUpdate(Order order, ...)
{
    if (order == null || orderState != OrderState.Filled) return;

    if (orderSubmitTimes.ContainsKey(order.Id))
    {
        TimeSpan latency = DateTime.Now - orderSubmitTimes[order.Id];
        Print($"Fill latency: {latency.TotalMilliseconds:F0}ms");

        if (latency.TotalMilliseconds > 500)
            Print("WARNING: High fill latency");

        orderSubmitTimes.Remove(order.Id);
    }
}
```

**Target Latencies:**
- Order submission to acknowledgment: < 100ms
- Acknowledgment to fill: < 500ms (variable)
- Total round-trip: < 600ms typical

### Slippage Monitoring
```csharp
protected override void OnExecutionUpdate(Execution execution, ...)
{
    if (execution.Order.OrderAction == OrderAction.Buy || execution.Order.OrderAction == OrderAction.SellShort)
    {
        double expectedPrice = execution.Order.LimitPrice > 0 ? execution.Order.LimitPrice : lastQuotePrice;
        double slippageTicks = Math.Abs(execution.Price - expectedPrice) / TickSize;

        Print($"Slippage: {slippageTicks:F1} ticks");

        if (slippageTicks > 3)
            Print($"WARNING: High slippage - {slippageTicks:F1} ticks");
    }
}
```

**Acceptable Slippage:**
- Market orders: 1-2 ticks typical
- Limit orders: 0 ticks (fill at limit or better)
- During volatility: 3-5 ticks acceptable
- > 5 ticks: Poor execution, investigate

---

## Order Types & Behavior

### Market Orders (Immediate Execution)
```csharp
private void EnterMarketLong(int quantity)
{
    // Best for momentum entries where fill is critical
    Order entry = SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Market, quantity, 0, 0, "", "MKT_Long");

    // Expect fill within 100-500ms
    // Slippage: 1-2 ticks typical on MES/MGC
}
```

**Use Cases:**
- ORB breakouts (must fill at breakout)
- MOMO entries (speed critical)
- Emergency exits

### Limit Orders (Price Control)
```csharp
private void EnterLimitLong(int quantity, double limitPrice)
{
    // Best for RMA click entries, mean reversion
    Order entry = SubmitOrderUnmanaged(0, OrderAction.Buy, OrderType.Limit, quantity, limitPrice, 0, "", "LMT_Long");

    // May not fill if price moves away
    // Slippage: 0 (fill at limit or better)
}
```

**Use Cases:**
- RMA click-to-entry
- FFMA mean reversion
- Better fills when not time-sensitive

### Stop Orders (Auto-Exit)
```csharp
private void SetStopLoss(double stopPrice)
{
    // Becomes market order when triggered
    Order stop = SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Stop, Position.Quantity, 0, stopPrice, "", "Stop_Loss");

    // Fills immediately after trigger
    // Slippage: 1-3 ticks possible
}
```

**Use Cases:**
- Stop losses (all strategies)
- Risk management
- Auto-exit if price moves against you

---

## Session Transition Handling

### Pre-Session Close (Avoid Gaps)
```csharp
protected override void OnBarUpdate()
{
    TimeSpan now = Time[0].TimeOfDay;
    TimeSpan sessionClose = new TimeSpan(15, 55, 0);  // 5 min before RTH close

    // Close all positions before session ends
    if (now >= sessionClose && Position.MarketPosition != MarketPosition.Flat)
    {
        Print("Closing positions before session end");
        FlattenAll("Pre-session close");
    }
}
```

**Rationale:**
- Avoid overnight gap risk
- Rithmic may disconnect during transition (16:00-18:00 ET)
- Lower liquidity during transition

---

## Disconnection Recovery Protocol

### Auto-Recovery Flow
```csharp
private bool inRecoveryMode = false;

private void HandleDisconnect()
{
    if (!rithmicConnected && !inRecoveryMode)
    {
        inRecoveryMode = true;
        Print("DISCONNECT DETECTED - Entering recovery mode");

        // Step 1: Close risky positions
        if (Position.MarketPosition != MarketPosition.Flat)
        {
            Print("Closing positions during disconnect");
            FlattenAll("Disconnect recovery");
        }

        // Step 2: Disable new entries
        allowTrading = false;

        // Step 3: Wait for reconnection
        Print("Waiting for Rithmic reconnection...");
    }
}

private void HandleReconnect()
{
    if (rithmicConnected && inRecoveryMode)
    {
        Print("Rithmic RECONNECTED");

        // Verify tick flow resumed
        if (ticksThisMinute >= 10)
        {
            inRecoveryMode = false;
            allowTrading = true;
            Print("Trading resumed");
        }
    }
}
```

---

## Performance Benchmarks

### Execution Speed Targets
```csharp
private void BenchmarkExecution()
{
    DateTime start = DateTime.Now;

    // Position sizing calculation
    int qty = CalculatePositionSize(riskPerTrade, 2.0);
    double sizing_ms = (DateTime.Now - start).TotalMilliseconds;

    // Order submission
    start = DateTime.Now;
    EnterLong(qty, "Benchmark");
    double submission_ms = (DateTime.Now - start).TotalMilliseconds;

    Print($"Position sizing: {sizing_ms:F2}ms");
    Print($"Order submission: {submission_ms:F2}ms");

    // Targets:
    // Sizing: < 0.5ms
    // Submission: < 50ms
}
```

**Performance Targets:**
| Metric | Target | Acceptable | Poor |
|--------|--------|------------|------|
| Position sizing | < 0.5ms | < 1ms | > 2ms |
| Order submission | < 50ms | < 100ms | > 200ms |
| Stop update | < 10ms | < 50ms | > 100ms |
| OnMarketData | < 1ms | < 2ms | > 5ms |

---

## Account Scaling (Multiple Accounts)

### When Ready to Scale
```csharp
// Per-account tracking structure
public class ApexAccount
{
    public string AccountId;
    public double DailyPnL;
    public double DailyLossLimit;
    public double AccountPeak;
    public double MaxDrawdown;
    public bool AllowTrading;
}

private List<ApexAccount> accounts = new List<ApexAccount>();

// Monitor all accounts
private void CheckAllAccounts()
{
    foreach (var account in accounts)
    {
        if (account.DailyPnL <= account.DailyLossLimit)
        {
            Print($"Account {account.AccountId} hit daily loss limit");
            account.AllowTrading = false;
        }
    }

    // Total risk check
    double totalRisk = accounts.Sum(a => Math.Abs(a.DailyPnL));
    double totalLossLimit = accounts.Sum(a => Math.Abs(a.DailyLossLimit));

    Print($"Total risk: ${totalRisk:F2} / ${totalLossLimit:F2}");
}
```

**Scaling Rules:**
- Start with 1 account, prove profitable 2-4 weeks
- Add accounts one at a time
- Each account has independent loss limits
- Total risk = sum of all account risks
- Use same strategy across accounts

---

## Troubleshooting Guide

### Order Rejected - "Stop at Market"
```csharp
private bool ValidateStopPrice(double stopPrice, bool isLong)
{
    double currentPrice = GetLivePrice();
    double buffer = TickSize * 4;  // 4-tick minimum buffer

    if (isLong && stopPrice >= currentPrice - buffer)
    {
        Print($"Stop too close: {stopPrice} vs market {currentPrice}");
        return false;
    }
    else if (!isLong && stopPrice <= currentPrice + buffer)
    {
        Print($"Stop too close: {stopPrice} vs market {currentPrice}");
        return false;
    }

    return true;
}
```

### Fill Not Confirmed (Delayed Notification)
```csharp
// Wait for fill confirmation before assuming rejection
private Dictionary<string, DateTime> pendingOrders = new Dictionary<string, DateTime>();

private void SubmitWithTimeout(Order order)
{
    pendingOrders[order.Id] = DateTime.Now;
    SubmitOrder(order);

    // Check for timeout after 2 seconds
}

protected override void OnOrderUpdate(Order order, ...)
{
    if (pendingOrders.ContainsKey(order.Id))
    {
        TimeSpan waitTime = DateTime.Now - pendingOrders[order.Id];

        if (waitTime.TotalMilliseconds > 2000 && orderState == OrderState.Working)
        {
            Print($"Fill delayed - {waitTime.TotalMilliseconds:F0}ms waiting");
        }

        if (orderState == OrderState.Filled || orderState == OrderState.Cancelled || orderState == OrderState.Rejected)
        {
            pendingOrders.Remove(order.Id);
        }
    }
}
```

---

## Testing Checklist

Before going live with Apex + Rithmic:
- [ ] Daily loss limit enforcement verified
- [ ] Trailing drawdown tracking works
- [ ] Rate-limiting prevents excessive mods
- [ ] Order rejection handling graceful
- [ ] Rithmic disconnect detection works
- [ ] Tick frequency monitoring active
- [ ] Fill latency tracking enabled
- [ ] Slippage monitoring enabled
- [ ] Session transition handling works
- [ ] Recovery protocol tested
- [ ] Account balance tracking accurate

---

## Related Skills
- [ninjatrader-strategy-dev.md](../core/ninjatrader-strategy-dev.md) - Code patterns
- [wsgta-trading-system.md](wsgta-trading-system.md) - Trading rules
- [trading-code-review.md](trading-code-review.md) - Quality checklist
- [trading-session-timezones.md](trading-session-timezones.md) - Session timing

