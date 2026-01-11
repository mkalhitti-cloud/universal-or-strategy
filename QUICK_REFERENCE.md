# UniversalORStrategy V4.0.1 - Quick Reference Card

## ðŸŽ¯ DAILY TRADING CHECKLIST

### Before Session Opens
- [ ] Verify strategy loaded on chart
- [ ] Check output window shows "v4.0 DYNAMIC SCALING"
- [ ] Confirm hotkeys active: "REALTIME - Hotkeys: L=Long, S=Short, F=Flatten"
- [ ] Verify session times correct for today's trading
- [ ] Check margin/buying power sufficient

### When Session Opens
- [ ] OR box starts building (status shows "BUILDING...")
- [ ] Wait for OR to complete (~5 minutes)
- [ ] Status changes to "OR COMPLETE" (green)
- [ ] Box appears at full session width
- [ ] Entry prices displayed on buttons

### Taking Trades
- [ ] Click **Long (L)** for bullish breakout OR
- [ ] Click **Short (S)** for bearish breakout OR  
- [ ] Click **both** for bracket (both sides)
- [ ] Press **F** anytime to flatten filled positions

### Monitor Position
- [ ] Panel shows: Direction, Contracts, Entry, Stop, Stop Level
- [ ] Watch for target fills: T1 â†’ T2 â†’ T3
- [ ] Stop automatically trails: Init â†’ BE â†’ T1 â†’ T2 â†’ T3
- [ ] Output window logs all activity

---

## ðŸ”¥ HOTKEYS (INSTANT EXECUTION)

| Key | Action | Description |
|-----|--------|-------------|
| **L** | Long | Submit long entry at OR high + 3 ticks |
| **S** | Short | Submit short entry at OR low - 3 ticks |
| **F** | Flatten | Close all filled positions, keep pending orders |

---

## ðŸ“Š DYNAMIC POSITION SIZING

Strategy automatically adapts based on calculated contract size:

| Size | Allocation | Strategy |
|------|------------|----------|
| **1 contract** | T1:0 T2:0 T3:1 | Trail-only (pure runner) |
| **2 contracts** | T1:1 T2:0 T3:1 | Quick profit + runner |
| **3+ contracts** | T1:1+ T2:1+ T3:remainder | Full scaling system |

**You'll see in output:**
```
POSITION SIZE: 1 contract â†’ Trail-only mode
POSITION SIZE: 2 contracts â†’ T1:1 Trail:1
POSITION SIZE ADJUSTED: 3 contracts â†’ T1:1 T2:1 T3:1
ENTRY ORDER: Long 6 @ 6986.25 | ... | T1: 2@... T2: 2@... T3: 2@trail
```

---

## ðŸŽª PANEL STATUS INDICATORS

### OR Status
- **"Waiting"** (white) = Before session start
- **"BUILDING..."** (yellow) = Collecting OR data
- **"OR COMPLETE"** (green) = Ready to trade

### Button States
- **Disabled (gray)** = OR not ready
- **Enabled (green/red)** = Ready to click
- Shows entry price when enabled

### Position Display
- **"No positions"** = Flat
- **"L:10 PENDING"** = Long entry submitted, not filled
- **"S:10 @4486.70 S:4490.70(Init)"** = Short filled, initial stop
- **"S:8 @4486.70 S:4487.00(BE)"** = 2 contracts exited at targets, stop at BE

---

## âš ï¸ CRITICAL RULES

### âœ… DO
- Wait for "OR COMPLETE" before entering
- Monitor output window for order confirmations
- Use Flatten (F) to exit quickly if needed
- Verify stop levels make sense
- Keep pending orders if one side fills badly

### âŒ DON'T
- Don't click buttons before OR completes
- Don't use Control Center "Flatten Position" (disables strategy)
- Don't manually cancel strategy orders (breaks tracking)
- Don't change strategy properties while running
- Don't panic - strategy manages risk automatically

---

## ðŸ” TROUBLESHOOTING

### Buttons Don't Work
- Check: OR status = "OR COMPLETE"?
- Check: Output shows "Insufficient contracts"? â†’ Increase min contracts
- Check: Hotkeys attached? â†’ Look for "REALTIME - Hotkeys..." message

### Orders Rejected
- Check: Sufficient margin?
- Check: Stop price valid? (not at/through market)
- Check: Instrument tradeable? (session hours)

### Wrong Entry Price
- Check: OR high/low values correct?
- Check: Tick size matches instrument?
- Restart: Remove and re-add strategy

### OR Starts Late
- Check: PC timezone vs strategy timezone setting
- Check: Session start time is correct
- Verify: "Session Reset" message shows correct time

### Box Position Wrong
- Restart strategy (remove and re-add)
- Verify timezone settings
- Check session start/end times

---

## ðŸ“ˆ EXPECTED LOG OUTPUT (NORMAL TRADE)

```
Session Reset: 1/7/2026 at 21:30:00 Eastern
OR WINDOW START: 01/07/2026 21:35:00 (Bar time in Eastern)
OR Start tracked - Bar 724
OR COMPLETE at 21:35:00: H=6988.50 L=6987.50 M=6988.00 R=1.00

[User clicks Long button]

ENTRY ORDER: Long 20 @ 6989.25 | Stop: 6985.25 (-4.00) | T1: 6@6991.25 T2: 6@6989.75 T3: 8@trail
ENTRY FILLED: LONG 20 @ 6989.18
BRACKET SUBMITTED: Stop@6985.25 | T1:6@6991.25 | T2:6@6989.75 | T3:8@trail
T1 FILLED: 6 contracts @ 6991.25 | Remaining: 14
STOP UPDATED: Long_123456 â†’ 6989.43 (Level: BE)
T2 FILLED: 6 contracts @ 6989.82 | Remaining: 8
STOP UPDATED: Long_123456 â†’ 6991.18 (Level: T1)
STOP UPDATED: Long_123456 â†’ 6992.18 (Level: T2)
STOP FILLED: 8 contracts @ 6992.25
```

---

## ðŸ’¡ TRADING TIPS

### Tip 1: Wait for Confirmation
Don't enter immediately at 9:35 PM. Wait to see if price is respecting OR levels. Enter when you see momentum.

### Tip 2: Use Flatten Strategically
If one side fills and immediately reverses, press F. The opposite entry stays active for the real move.

### Tip 3: Both Sides = Patient
Submit both Long and Short if you're unsure direction. Let market choose. Flatten the loser, ride the winner.

### Tip 4: Watch T1
First target hit = validation. If T1 hits quickly, trail aggressively. If T1 struggles, consider flattening early.

### Tip 5: Trust the Trail
Once at BE, you can't lose. Let the trailing stop do its job. Don't exit manually unless you have strong reason.

---

## ðŸŽ“ COMMON SCENARIOS

### Scenario 1: Clean Breakout
```
9:35 PM: OR completes, range = 4 points
9:40 PM: Market breaks OR high with volume
Action: Click Long (L)
Result: Entry fills, T1 hits quickly, position trails to profit
```

### Scenario 2: False Breakout
```
9:35 PM: OR completes
9:42 PM: Market breaks OR high
9:43 PM: Immediately reverses back into range
Action: Press Flatten (F) when back in range
Result: Exit long for small loss, short entry still active at OR low
```

### Scenario 3: Choppy Open
```
9:35 PM: OR completes, tight range
Price chops around OR levels for 20 minutes
Action: Don't enter yet - wait for clean break with momentum
Result: Avoid whipsaw by being patient
```

### Scenario 4: Gap Open
```
9:30 PM: Session opens with big gap
9:35 PM: OR completes very tight (1-2 points)
Action: Click both Long and Short (bracket)
Result: High probability one side fills profitably when gap fills/extends
```

---

## ðŸ† SUCCESS METRICS

Track these over 20+ trades:

- **Win Rate**: % of trades that hit at least T1 (target 60%+)
- **Avg Winner**: $ per winning trade (target 2-3x avg loser)
- **Avg Loser**: $ per losing trade (should equal stop distance Ã— contract size)
- **Profit Factor**: Total wins / Total losses (target 1.5+)
- **Max Drawdown**: Largest peak-to-valley loss (keep under 20% of account)

**Good Performance**:
- Win rate: 60%
- Avg winner: $120 (6 pts @ $20/contract)
- Avg loser: $80 (4 pts @ $20/contract)
- Profit Factor: (120 Ã— 0.6) / (80 Ã— 0.4) = 2.25 âœ“

---

## ðŸ“ž NEED HELP?

**Output Window**: Most issues show error messages here  
**NinjaTrader Logs**: Documents\NinjaTrader 8\Log  
**Strategy Disable**: Usually means order rejection - check margin  

**Remember**: Strategy auto-manages once position is entered. Your job is entry timing and direction selection. Everything else is automated!

---

**Trade smart, manage risk, let runners run! ðŸš€**
