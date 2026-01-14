# WSGTA Trading System - Complete Implementation Guide

**Context:** Wall Street Global Trading Academy methodology for MES/MGC futures
**Platform:** NinjaTrader 8, Apex funded accounts, Rithmic data feed
**Strategies:** ORB, RMA, FFMA, MOMO, DBDT, TREND

---

## Core Principles (Code Implementation Required)

### 1. ATR-Based Position Sizing (REQUIRED)
```csharp
private int CalculatePositionSize(double riskDollars, double atrMultiplier)
{
    double atr = ATR(14)[0];
    double stopDistance = atr * atrMultiplier;
    double tickValue = Instrument.MasterInstrument.PointValue;

    int contracts = (int)Math.Floor(riskDollars / (stopDistance * tickValue));

    // Cap at max from Order_Management.xlsx
    return Math.Min(contracts, MaxPositionSize);
}
```

### 2. Dual Profit Targets (50/50 Rule)
```csharp
private void SetProfitTargets(double entryPrice, double atr, bool isLong)
{
    double tp1Distance = atr * 2.0;  // TP1 = 2× ATR
    double tp2Distance = atr * 4.0;  // TP2 = 4× ATR

    if (isLong)
    {
        tp1Price = entryPrice + tp1Distance;
        tp2Price = entryPrice + tp2Distance;
    }
    else
    {
        tp1Price = entryPrice - tp1Distance;
        tp2Price = entryPrice - tp2Distance;
    }
}

protected override void OnBarUpdate()
{
    // Exit 50% at TP1, 50% at TP2
    if (position.Quantity > 0)
    {
        if (!tp1Hit && GetLivePrice() >= tp1Price)
        {
            int halfQty = position.Quantity / 2;
            position.Close(halfQty, "TP1");
            tp1Hit = true;
        }

        if (tp1Hit && GetLivePrice() >= tp2Price)
        {
            position.Close("TP2");
            tp2Hit = true;
        }
    }
}
```

### 3. Stop Loss Enforcement (NO EXCEPTIONS)
```csharp
private void ExecuteLongWithStop(double entryPrice, double atr)
{
    double stopPrice = entryPrice - (atr * 2.0);

    // NEVER enter without stop
    if (stopPrice <= 0 || stopPrice >= entryPrice)
    {
        Print("ERROR: Invalid stop price - order rejected");
        return;
    }

    // Enter with stop immediately
    EnterLong(CalculatePositionSize(riskPerTrade, 2.0), "Long");
    SetStopLoss(stopPrice);
}
```

---

## Strategy 1: Opening Range Breakout (ORB)

### Setup Phase (9:30-10:00 ET)
```csharp
private double sessionHigh = double.MinValue;
private double sessionLow = double.MaxValue;
private bool orComplete = false;

protected override void OnBarUpdate()
{
    TimeSpan now = Time[0].TimeOfDay;
    TimeSpan orStart = new TimeSpan(9, 30, 0);
    TimeSpan orEnd = new TimeSpan(10, 0, 0);

    // Capture range
    if (now >= orStart && now < orEnd)
    {
        if (High[0] > sessionHigh)
            sessionHigh = High[0];
        if (Low[0] < sessionLow)
            sessionLow = Low[0];

        orComplete = false;
    }
    else if (now >= orEnd && !orComplete)
    {
        orComplete = true;
        Print($"OR complete: H={sessionHigh}, L={sessionLow}");
    }
}
```

### Breakout Detection (10:00-12:00 ET)
```csharp
protected override void OnBarUpdate()
{
    if (!orComplete || Position.MarketPosition != MarketPosition.Flat)
        return;

    TimeSpan now = Time[0].TimeOfDay;
    if (now < new TimeSpan(10, 0, 0) || now >= new TimeSpan(12, 0, 0))
        return;

    double livePrice = GetLivePrice();

    // Long breakout
    if (livePrice > sessionHigh + TickSize)
    {
        double atr = ATR(14)[0];
        int qty = CalculatePositionSize(riskPerTrade, 2.0);

        EnterLong(qty, "ORB_Long");
        SetStopLoss(livePrice - (atr * 2.0));
        SetProfitTargets(livePrice, atr, true);
    }
    // Short breakout
    else if (livePrice < sessionLow - TickSize)
    {
        double atr = ATR(14)[0];
        int qty = CalculatePositionSize(riskPerTrade, 2.0);

        EnterShort(qty, "ORB_Short");
        SetStopLoss(livePrice + (atr * 2.0));
        SetProfitTargets(livePrice, atr, false);
    }
}
```

### Forced Exit (12:00 ET)
```csharp
protected override void OnBarUpdate()
{
    if (Position.MarketPosition == MarketPosition.Flat)
        return;

    TimeSpan exitTime = new TimeSpan(12, 0, 0);

    if (Time[0].TimeOfDay >= exitTime)
    {
        FlattenAll("ORB time exit");
    }
}
```

---

## Strategy 2: Regular Moving Average (RMA)

### Click-to-Entry Implementation
```csharp
protected override void OnMouseDown(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
{
    // Only on Shift+Click
    if (!IsShiftPressed)
        return;

    double clickedPrice = chartScale.GetValueByY(dataPoint.Y);
    double ema9 = EMA(9)[0];
    double ema15 = EMA(15)[0];

    // Validate setup (price must align with EMA trend)
    bool isLongSetup = ema9 > ema15 && clickedPrice > ema15;
    bool isShortSetup = ema9 < ema15 && clickedPrice < ema15;

    if (isLongSetup)
    {
        double atr = ATR(14)[0];
        int qty = CalculatePositionSize(riskPerTrade, 2.0);

        EnterLongLimit(qty, clickedPrice, "RMA_Long");
        SetStopLoss(clickedPrice - (atr * 2.0));
        SetProfitTargets(clickedPrice, atr, true);
    }
    else if (isShortSetup)
    {
        double atr = ATR(14)[0];
        int qty = CalculatePositionSize(riskPerTrade, 2.0);

        EnterShortLimit(qty, clickedPrice, "RMA_Short");
        SetStopLoss(clickedPrice + (atr * 2.0));
        SetProfitTargets(clickedPrice, atr, false);
    }
}
```

### Automated EMA Touch Entry
```csharp
protected override void OnBarUpdate()
{
    if (Position.MarketPosition != MarketPosition.Flat)
        return;

    double ema9 = EMA(9)[0];
    double ema15 = EMA(15)[0];
    double livePrice = GetLivePrice();

    // Long setup: Price touches EMA9 from above in uptrend
    if (ema9 > ema15 && livePrice <= ema9 + TickSize && livePrice >= ema9 - TickSize)
    {
        if (Close[1] > ema9)  // Was above, now touching
        {
            double atr = ATR(14)[0];
            int qty = CalculatePositionSize(riskPerTrade, 2.0);

            EnterLong(qty, "RMA_Auto_Long");
            SetStopLoss(livePrice - (atr * 2.0));
            SetProfitTargets(livePrice, atr, true);
        }
    }
}
```

---

## Strategy 3: Far From Moving Average (FFMA)

### Mean Reversion Setup
```csharp
protected override void OnBarUpdate()
{
    if (Position.MarketPosition != MarketPosition.Flat)
        return;

    double ema9 = EMA(9)[0];
    double atr = ATR(14)[0];
    double livePrice = GetLivePrice();

    double distanceFromEMA = Math.Abs(livePrice - ema9);

    // Entry when price is 1-2 ATRs away from EMA
    if (distanceFromEMA >= atr && distanceFromEMA <= atr * 2.0)
    {
        // Long: Price far below EMA, expecting reversion up
        if (livePrice < ema9)
        {
            int qty = CalculatePositionSize(riskPerTrade * 0.75, 2.0);  // Smaller size for mean reversion

            EnterLong(qty, "FFMA_Long");
            SetStopLoss(livePrice - (atr * 2.0));

            // Targets: EMA9 (TP1), EMA15 (TP2)
            tp1Price = ema9;
            tp2Price = EMA(15)[0];
        }
        // Short: Price far above EMA
        else if (livePrice > ema9)
        {
            int qty = CalculatePositionSize(riskPerTrade * 0.75, 2.0);

            EnterShort(qty, "FFMA_Short");
            SetStopLoss(livePrice + (atr * 2.0));

            tp1Price = ema9;
            tp2Price = EMA(15)[0];
        }
    }
}
```

---

## Strategy 4: Momentum (MOMO)

### Volume + RSI Breakout
```csharp
protected override void OnBarUpdate()
{
    if (Position.MarketPosition != MarketPosition.Flat)
        return;

    // Only trade morning session
    TimeSpan now = Time[0].TimeOfDay;
    if (now < new TimeSpan(9, 30, 0) || now >= new TimeSpan(12, 0, 0))
        return;

    double rsi = RSI(14, 3)[0];
    double avgVolume = SMA(Volume, 20)[0];
    double currentVolume = Volume[0];

    // Long: RSI > 70 + volume surge
    if (rsi > 70 && currentVolume > avgVolume * 1.5)
    {
        if (Close[0] > Close[1])  // Momentum up
        {
            double atr = ATR(14)[0];
            int qty = CalculatePositionSize(riskPerTrade * 0.5, 1.5);  // Smaller size, tighter stop

            EnterLong(qty, "MOMO_Long");
            SetStopLoss(Close[0] - (atr * 1.5));

            // Quick targets
            tp1Price = Close[0] + (atr * 0.5);
            tp2Price = Close[0] + (atr * 1.0);
        }
    }
    // Short: RSI < 30 + volume surge
    else if (rsi < 30 && currentVolume > avgVolume * 1.5)
    {
        if (Close[0] < Close[1])
        {
            double atr = ATR(14)[0];
            int qty = CalculatePositionSize(riskPerTrade * 0.5, 1.5);

            EnterShort(qty, "MOMO_Short");
            SetStopLoss(Close[0] + (atr * 1.5));

            tp1Price = Close[0] - (atr * 0.5);
            tp2Price = Close[0] - (atr * 1.0);
        }
    }
}
```

---

## Strategy 5: Double Bottom/Top (DBDT)

### Pattern Detection
```csharp
private double firstBottom = 0;
private double secondBottom = 0;
private double neckline = 0;
private int barsSinceFirstBottom = 0;

protected override void OnBarUpdate()
{
    // Detect first bottom (local low)
    if (Low[0] < Low[1] && Low[0] < Low[2])
    {
        if (firstBottom == 0)
        {
            firstBottom = Low[0];
            barsSinceFirstBottom = 0;
        }
    }

    barsSinceFirstBottom++;

    // Detect second bottom (within 5-20 bars)
    if (barsSinceFirstBottom >= 5 && barsSinceFirstBottom <= 20)
    {
        if (Low[0] < Low[1] && Low[0] < Low[2])
        {
            // Must be within 1 ATR of first bottom
            double atr = ATR(14)[0];
            if (Math.Abs(Low[0] - firstBottom) <= atr * 0.5)
            {
                secondBottom = Low[0];
                neckline = Math.Max(High[0], High[1]);  // Resistance between bottoms
            }
        }
    }

    // Breakout above neckline
    if (secondBottom > 0 && Close[0] > neckline)
    {
        double atr = ATR(14)[0];
        int qty = CalculatePositionSize(riskPerTrade, 2.0);

        EnterLong(qty, "DBDT_Long");
        SetStopLoss(secondBottom - TickSize);

        // Target: Distance from neckline to bottom
        double patternHeight = neckline - secondBottom;
        tp1Price = neckline + patternHeight;
        tp2Price = neckline + (patternHeight * 2.0);

        // Reset pattern
        firstBottom = 0;
        secondBottom = 0;
    }
}
```

---

## Strategy 6: Trend (9/15 EMA)

### Trend Confirmation + Pullback Entry
```csharp
protected override void OnBarUpdate()
{
    double ema9 = EMA(9)[0];
    double ema15 = EMA(15)[0];
    double livePrice = GetLivePrice();

    // Uptrend: EMA9 > EMA15
    if (ema9 > ema15)
    {
        // Pullback to EMA9
        if (livePrice <= ema9 + TickSize && livePrice >= ema9 - TickSize)
        {
            if (Close[1] > ema9)  // Was above, now touching
            {
                double atr = ATR(14)[0];
                int qty = CalculatePositionSize(riskPerTrade, 3.0);  // Wider stop for trend

                EnterLong(qty, "TREND_Long");
                SetStopLoss(livePrice - (atr * 3.0));
                SetProfitTargets(livePrice, atr, true);

                // Enable trailing stop
                EnableTrailingStop(atr);
            }
        }
    }
    // Downtrend: EMA15 > EMA9
    else if (ema15 > ema9)
    {
        if (livePrice <= ema9 + TickSize && livePrice >= ema9 - TickSize)
        {
            if (Close[1] < ema9)
            {
                double atr = ATR(14)[0];
                int qty = CalculatePositionSize(riskPerTrade, 3.0);

                EnterShort(qty, "TREND_Short");
                SetStopLoss(livePrice + (atr * 3.0));
                SetProfitTargets(livePrice, atr, false);

                EnableTrailingStop(atr);
            }
        }
    }
}
```

---

## Trailing Stop Management (Live Price Tracking)

```csharp
private double highestPrice = 0;
private double lowestPrice = double.MaxValue;
private DateTime lastStopUpdate = DateTime.MinValue;

protected override void OnMarketData(MarketDataEventArgs e)
{
    if (e.MarketDataType != MarketDataType.Last)
        return;

    double livePrice = e.Price;

    // Long position trailing
    if (Position.MarketPosition == MarketPosition.Long)
    {
        if (livePrice > highestPrice)
        {
            highestPrice = livePrice;

            double atr = ATR(14)[0];
            double newStop = highestPrice - (atr * trailingMultiplier);

            // Only update if 1+ second passed (rate limiting)
            if ((DateTime.Now - lastStopUpdate).TotalMilliseconds >= 1000)
            {
                if (newStop > currentStopPrice)  // Only move stop up
                {
                    SetStopLoss(newStop);
                    lastStopUpdate = DateTime.Now;
                }
            }
        }
    }
    // Short position trailing
    else if (Position.MarketPosition == MarketPosition.Short)
    {
        if (livePrice < lowestPrice)
        {
            lowestPrice = livePrice;

            double atr = ATR(14)[0];
            double newStop = lowestPrice + (atr * trailingMultiplier);

            if ((DateTime.Now - lastStopUpdate).TotalMilliseconds >= 1000)
            {
                if (newStop < currentStopPrice)  // Only move stop down
                {
                    SetStopLoss(newStop);
                    lastStopUpdate = DateTime.Now;
                }
            }
        }
    }
}
```

---

## Risk Management (Hard Limits)

### Daily Loss Limit
```csharp
private double dailyPnL = 0;
private double dailyLossLimit = -500;  // From Order_Management.xlsx

protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
{
    dailyPnL += execution.Quantity * (execution.Price - entryPrice) * Instrument.MasterInstrument.PointValue;

    if (dailyPnL <= dailyLossLimit)
    {
        Print("DAILY LOSS LIMIT HIT - STOPPING TRADING");
        FlattenAll("Daily loss limit");
        allowTrading = false;
    }
}
```

### Position Limits
```csharp
private int maxConcurrentPositions = 3;  // From Order_Management.xlsx
private Dictionary<string, Position> activePositions = new Dictionary<string, Position>();

private bool CanEnterNewPosition()
{
    if (activePositions.Count >= maxConcurrentPositions)
    {
        Print("Max concurrent positions reached");
        return false;
    }

    if (!allowTrading)
    {
        Print("Trading disabled (daily loss limit or manual)");
        return false;
    }

    return true;
}
```

---

## Performance Benchmarks

### Execution Speed Targets
- Position sizing calculation: < 0.5ms
- Entry order submission: < 50ms from signal
- Stop/target placement: < 10ms after entry
- Trailing stop update: < 1ms per tick

### Memory Targets
- Per-strategy footprint: < 10 MB
- Total with 6 strategies: < 60 MB
- No memory growth after 12+ hours

---

## Testing Checklist

Before deploying any strategy:
- [ ] Compiles without errors/warnings
- [ ] ATR-based position sizing verified
- [ ] Stop loss ALWAYS set before entry
- [ ] TP1/TP2 levels correct (50/50 split)
- [ ] Trailing stops update between bar closes
- [ ] Rate-limiting on order modifications
- [ ] Daily loss limit enforcement works
- [ ] Position limits respected
- [ ] Session time filters work
- [ ] Backtested 2+ weeks successfully
- [ ] Paper traded 2-5 sessions
- [ ] Live tested with 1 contract

---

## Related Skills
- [ninjatrader-strategy-dev.md](../core/ninjatrader-strategy-dev.md) - Code patterns
- [live-price-tracking.md](../references/live-price-tracking.md) - Critical bug fix
- [trading-code-review.md](trading-code-review.md) - Quality checklist
- [apex-rithmic-trading.md](apex-rithmic-trading.md) - Account compliance
- [trading-session-timezones.md](trading-session-timezones.md) - Session timing
