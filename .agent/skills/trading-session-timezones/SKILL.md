---
name: trading-session-timezones
description: Trading session timing for MES/MGC futures with timezone handling. Use when implementing session-based entry/exit logic, ORB time windows, RTH detection, or handling global market overlaps.
---
# Trading Session Timezones - MES/MGC Futures

**Context:** Session timing for micro futures (MES/MGC) trading
**Critical For:** Opening Range Breakout (ORB), session-based entry/exit timing
**Platform:** NinjaTrader 8 with Rithmic data feed

---

## Session Times (Eastern Time)

### RTH (Regular Trading Hours) - Primary Trading Window
```
Open:  09:30 ET
Close: 16:00 ET
```
**Best for:** ORB (9:30-10:00 setup), MOMO, all high-volume strategies
**Volume:** Highest - 50-200 ticks/min during active periods
**Spread:** Tightest (1-2 ticks typical)

### Globex (Extended Hours)
```
Open:  18:00 ET (previous day)
Close: 17:00 ET (current day)
```
**Best for:** Trend continuation, overnight gap setups
**Volume:** Moderate - 5-50 ticks/min
**Spread:** Wider (2-4 ticks typical)

### Pre-Market
```
Open:  04:00 ET
Close: 09:30 ET
```
**Best for:** ORB preparation, pre-market analysis
**Volume:** Low - 5-20 ticks/min
**Spread:** Wide (3-5 ticks)

---

## Code Implementation

### Detecting Session State
```csharp
private bool IsRTH()
{
    TimeSpan now = Time[0].TimeOfDay;
    TimeSpan rthOpen = new TimeSpan(9, 30, 0);
    TimeSpan rthClose = new TimeSpan(16, 0, 0);

    return now >= rthOpen && now < rthClose;
}

private bool IsORWindow()
{
    TimeSpan now = Time[0].TimeOfDay;
    TimeSpan orStart = new TimeSpan(9, 30, 0);
    TimeSpan orEnd = new TimeSpan(10, 0, 0);

    return now >= orStart && now < orEnd;
}
```

### Timezone Conversion for Multi-Market Trading
```csharp
// Convert ET to other timezones for global session awareness
private DateTime ConvertETToLocal(DateTime etTime)
{
    TimeZoneInfo et = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
    TimeZoneInfo local = TimeZoneInfo.Local;

    return TimeZoneInfo.ConvertTime(etTime, et, local);
}
```

---

## Optimal Trading Windows (Priority Tiers)

### Tier 1: Best Execution Quality (Target 100% of trades here)
```
09:30 - 12:00 ET  Morning session (ORB, MOMO, high volatility)
13:00 - 15:00 ET  Afternoon trends (RMA, TREND strategies)
```
**Characteristics:**
- Tightest spreads (1-2 ticks)
- Highest volume (100+ ticks/min)
- Best fills (slippage ≤ 2 ticks)
- Fastest execution (< 50ms)

### Tier 2: Acceptable Quality (Use selectively)
```
08:00 - 09:30 ET  Pre-market (ORB setup, low risk entries)
15:00 - 16:00 ET  Close (end-of-day moves)
```
**Characteristics:**
- Moderate spreads (2-3 ticks)
- Moderate volume (20-50 ticks/min)
- Acceptable fills (slippage ≤ 3 ticks)

### Tier 3: Avoid (Only for specific strategies)
```
19:00 - 22:00 ET  Evening Globex
04:00 - 08:00 ET  Early morning
```
**Characteristics:**
- Wide spreads (3-5 ticks)
- Low volume (< 20 ticks/min)
- Poor fills (slippage > 3 ticks)

### Dead Zones (Never Trade)
```
16:00 - 18:00 ET  Session transition (Rithmic may disconnect)
22:00 - 04:00 ET  Overnight (ultra-low volume)
```

---

## ORB Strategy Timing Requirements

### Setup Phase (9:30-10:00 ET)
```csharp
protected override void OnBarUpdate()
{
    if (!IsORWindow() || !IsFirstTickOfBar)
        return;

    // Capture high/low during OR window
    if (High[0] > sessionHigh)
        sessionHigh = High[0];
    if (Low[0] < sessionLow)
        sessionLow = Low[0];
}
```

### Trade Phase (10:00-12:00 ET)
```csharp
private bool IsORTradeWindow()
{
    TimeSpan now = Time[0].TimeOfDay;
    TimeSpan tradeStart = new TimeSpan(10, 0, 0);
    TimeSpan tradeEnd = new TimeSpan(12, 0, 0);

    return now >= tradeStart && now < tradeEnd;
}

protected override void OnBarUpdate()
{
    if (!IsORTradeWindow() || !orComplete)
        return;

    // Execute breakout trades
    if (Close[0] > sessionHigh + TickSize)
        ExecuteLong();
    else if (Close[0] < sessionLow - TickSize)
        ExecuteShort();
}
```

### Forced Exit (12:00 ET)
```csharp
protected override void OnBarUpdate()
{
    TimeSpan exitTime = new TimeSpan(12, 0, 0);

    if (Time[0].TimeOfDay >= exitTime && Position.MarketPosition != MarketPosition.Flat)
    {
        FlattenAll("ORB time exit");
    }
}
```

---

## Global Session Overlaps (For Multi-Market Awareness)

### Key Times in Eastern (ET)
```
20:00 ET (prev day)  China market open (Shanghai 9:00 AM)
01:00 ET             Tokyo market open (Tokyo 3:00 PM)
03:00 ET             London pre-market
08:00 ET             London RTH open
09:30 ET             New York RTH open ← PRIMARY FOCUS
```

### Overlap Impact on MES/MGC
- **London + NY (8:00-16:00 ET):** Highest volume period
- **Asia hours (20:00-03:00 ET):** Lower volume, wider spreads
- **NY solo (15:00-16:00 ET):** Moderate volume, end-of-day positioning

---

## Calendar-Based Filters

### High-Impact News Days (Avoid or Trade Cautiously)
```csharp
// Example: FOMC days - avoid 2 hours before/after announcement
private bool IsFOMCDay()
{
    // Hardcode FOMC dates or check economic calendar API
    DateTime[] fomcDates = { new DateTime(2025, 1, 29), new DateTime(2025, 3, 19) };
    return fomcDates.Contains(Time[0].Date);
}

protected override void OnBarUpdate()
{
    if (IsFOMCDay() && Time[0].TimeOfDay >= new TimeSpan(12, 0, 0))
    {
        // Pause trading or reduce position size
        return;
    }
}
```

### Weekly Patterns
```csharp
private bool IsFriday()
{
    return Time[0].DayOfWeek == DayOfWeek.Friday;
}

protected override void OnBarUpdate()
{
    // Reduce position size on Friday afternoons
    if (IsFriday() && Time[0].TimeOfDay >= new TimeSpan(14, 0, 0))
    {
        positionSizeMultiplier = 0.5;  // Half size
    }
}
```

---

## Rithmic Data Feed Session Behavior

### Expected Tick Frequency by Session
```csharp
private void MonitorTickHealth()
{
    int ticksPerMinute = tickCount / minutesSinceOpen;

    if (IsRTH() && ticksPerMinute < 20)
        Print("WARNING: RTH tick frequency low - Rithmic may be degraded");
    else if (!IsRTH() && ticksPerMinute < 5)
        Print("INFO: Normal overnight low volume");
}
```

### Session Transition Handling
```csharp
protected override void OnBarUpdate()
{
    // Close positions before session transitions to avoid gaps
    TimeSpan sessionTransition = new TimeSpan(15, 55, 0);

    if (Time[0].TimeOfDay >= sessionTransition && Position.MarketPosition != MarketPosition.Flat)
    {
        FlattenAll("Pre-session close");
    }
}
```

---

## Performance Optimization

### Cache Session Checks (Don't Recalculate Every Tick)
```csharp
private bool cachedIsRTH = false;
private DateTime lastSessionCheck = DateTime.MinValue;

private bool IsRTHCached()
{
    if (Time[0] != lastSessionCheck)
    {
        cachedIsRTH = IsRTH();
        lastSessionCheck = Time[0];
    }
    return cachedIsRTH;
}
```

### Execution Speed Target
- Session state checks: < 0.1ms
- OR window detection: < 0.1ms
- Forced exit logic: < 1ms total

---

## Quick Reference Table

| Session | ET Time | Ticks/Min | Spread | ORB | RMA | MOMO | TREND |
|---------|---------|-----------|--------|-----|-----|------|-------|
| RTH Morning | 9:30-12:00 | 100+ | 1-2 | ✅ Best | ✅ | ✅ Best | ✅ |
| RTH Afternoon | 12:00-16:00 | 50-100 | 1-2 | ❌ | ✅ | ⚠️ Lower | ✅ |
| Pre-Market | 4:00-9:30 | 5-20 | 3-5 | ⚠️ Setup | ⚠️ | ❌ | ❌ |
| Evening | 19:00-22:00 | 10-30 | 2-4 | ❌ | ⚠️ | ❌ | ⚠️ |
| Overnight | 22:00-4:00 | < 10 | 4-6 | ❌ | ❌ | ❌ | ❌ |

---

## Testing Checklist

Before deploying session-based logic:
- [ ] Verify timezone conversion (ET vs. local)
- [ ] Test OR window detection (9:30-10:00 exact)
- [ ] Test forced exit (12:00 ET exact)
- [ ] Verify session transition handling
- [ ] Test on historical data with session gaps
- [ ] Verify tick frequency monitoring works
- [ ] Test calendar filters (Friday, FOMC, etc.)

---

## Related Skills
- [wsgta-trading-system.md](wsgta-trading-system.md) - Strategy-specific session rules
- [universal-strategy-v6-context.md](../project-specific/universal-strategy-v6-context.md) - Current implementation
- [apex-rithmic-trading.md](apex-rithmic-trading.md) - Data feed session behavior

