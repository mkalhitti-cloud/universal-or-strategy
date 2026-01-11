# NinjaTrader 8 Strategy Development Patterns

## Critical Bug: Close[0] vs. Live Price

### The Problem
Using `Close[0]` for trailing stops only updates at **bar close**, not in real-time.

Example of BROKEN code:
```
if (Close[0] > entryPrice + atrDistance)
{
    // This only executes when bar closes, not tick-by-tick
    SetStopLoss(CalculateLoss());
}
```

### The Solution
Use `OnMarketData()` for tick-level price tracking:

```
private double lastUpdatePrice = 0;

protected override void OnMarketData(MarketDataEventArgs e)
{
    if (e.MarketDataType != MarketDataType.Last) return;

    lastUpdatePrice = e.Price;

    // Now you can check live price
    if (lastUpdatePrice > entryPrice + atrDistance)
    {
        // This executes on EVERY tick, not just bar close
        SetStopLoss(CalculateLoss());
    }
}
```

### GetLivePrice() Helper Pattern
```
private double GetLivePrice()
{
    // Returns last market data price
    if (Bid > 0 && Ask > 0)
        return (Bid + Ask) / 2.0;  // Midpoint if available

    if (Bid > 0) return Bid;
    if (Ask > 0) return Ask;

    return Close[0];  // Fallback only
}
```

## IsUnmanaged=true Architecture

For full control of order management:

```
private IsUnmanaged = true;

protected override void OnStateChange()
{
    if (State == State.SetDefaults)
    {
        IsUnmanaged = true;
        // ... rest of defaults
    }
}
```

Benefits:
- Full control over order routing
- Tick-level order modifications
- Sub-50ms execution possible
- Works with Rithmic feeds

## OnMarketData Hook Pattern

```
protected override void OnMarketData(MarketDataEventArgs e)
{
    // Only process Last (trade) prices
    if (e.MarketDataType != MarketDataType.Last) return;

    // Only process our instrument
    if (e.Instrument != Instrument) return;

    // Now process the tick
    UpdateTrailingStop(e.Price);
    CheckEntrySignals(e.Price);
}
```

## Rate-Limiting Order Modifications

Prevent excessive order changes (Apex rule compliance):

```
private DateTime lastModificationTime = DateTime.MinValue;
private const int ModificationDelayMs = 1000;  // 1 second minimum

private bool CanModifyOrder()
{
    if ((DateTime.Now - lastModificationTime).TotalMilliseconds < ModificationDelayMs)
        return false;

    lastModificationTime = DateTime.Now;
    return true;
}

// Then in your code:
if (CanModifyOrder() && priceHitThreshold)
{
    position.StopLoss = newStopPrice;
}
```

## StringBuilder Pooling (Memory Efficiency)

Reduce garbage collection pressure:

```
private StringBuilder logBuffer = new StringBuilder();

private void LogMessage(string message)
{
    logBuffer.Clear();
    logBuffer.Append(Time[0].ToString("yyyy-MM-dd HH:mm:ss")).Append(" - ");
    logBuffer.Append(message);
    Print(logBuffer.ToString());
}
```

## Common Anti-Patterns (DON'T DO THESE)

### ❌ Using Close[0] for Trailing Stops
Already covered above - use OnMarketData instead.

### ❌ Continuous OrderState Polling in OnBarClose
```
// WRONG - This is inefficient
if (position.OrderState == OrderState.Filled)
{
    // ...
}
```

**CORRECT** - Use order callback events:
```
protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice, int quantity, int filled, double averageFillPrice, OrderState orderState, DateTime time, ErrorCode errorCode, string nserror)
{
    if (order == null) return;
    if (orderState == OrderState.Filled)
    {
        // Handle fill immediately
    }
}
```

### ❌ No Error Handling for Rithmic Disconnects
```
// WRONG - No safety check
private double GetLivePrice()
{
    return Close[0];  // What if data is stale?
}

// CORRECT - Include staleness check
private double GetLivePrice()
{
    if ((DateTime.Now - LastBarTime).TotalSeconds > 5)
    {
        // Warn about stale data
        Print("Warning: Price data is 5+ seconds old");
        return Close[1];  // Use previous close
    }
    return Close[0];
}
```

### ❌ Not Managing Position Lifecycle
```
// WRONG - No tracking of entry state
if (position == null)
{
    position = Enter();
}

// CORRECT - Clear state management
if (position == null && canEnter)
{
    position = Enter();
    entryTime = Time[0];
    entryPrice = Close[0];
}
else if (position != null && position.Quantity == 0)
{
    position = null;  // Clear closed position
}
```

## Best Practices Summary

1. **Always use OnMarketData for live prices**
2. **Rate-limit all order modifications (Apex compliance)**
3. **Implement proper error handling for data feeds**
4. **Use IsUnmanaged=true for full control**
5. **Pool strings/builders to reduce GC**
6. **Test with multiple timeframes (1min, 5min, 15min)**
7. **Verify execution speed (target < 50ms)**
8. **Monitor memory usage (target < 80% on your system)**

## Testing Checklist

- [ ] Code compiles without warnings
- [ ] OnMarketData hook fires on every tick
- [ ] Trailing stops update between bar closes
- [ ] Order modifications are rate-limited
- [ ] Memory usage stable after 1 hour
- [ ] No crashes after 12+ hour session
- [ ] Rithmic disconnect/reconnect handled
- [ ] Apex compliance verified (order count)

## References
- NT8 Documentation: https://ninjatrader.com/support
- Rithmic Data Feed: See apex-rithmic-trading.md
- Order Management: See Order_Management.xlsx
- Critical Bugs: See nt8-common-bugs.md
