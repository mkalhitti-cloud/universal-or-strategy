# TRAILING STOP TEST PLAN - Universal OR Strategy V5.3

**Document Purpose**: Step-by-step testing checklist for trailing stop behavior
**User**: Mo (Apex funded trader)
**Environment**: NinjaTrader 8 + Rithmic + MES futures
**Status**: Ready for systematic testing

---

## WHAT YOU'VE ALREADY PROVEN ✅

**Test Date**: [First RMA test]
**Trade**: RMA SHORT 2 contracts @ 6995.50
**Result**: Trailing stop moved ONCE after T1 fill
- Original stop: 6998.76
- After T1 @ 6993.87: Stop moved to 6995.60
- Final exit: Stop filled @ 6995.63

**Conclusion**: Basic trailing works for ONE adjustment after T1 fill.

---

## WHAT STILL NEEDS TESTING ❓

You need to verify 5 critical scenarios:

1. **Continuous favorable movement** - Does it trail multiple times?
2. **Slow drift** - Does it adjust for small incremental moves?
3. **Gap moves** - Can it handle sudden jumps?
4. **Reversals** - Does it lock in profit then hold?
5. **Time duration** - Does it stay active for hours?

---

## TEST SCENARIO 1: Continuous Favorable Movement

### What You're Testing
**Question**: If price keeps moving in my favor after T1 fills, does the trailing stop keep following it down (for SHORT) or up (for LONG)?

### Market Conditions to Wait For
- Enter RMA LONG at a support level during an uptrend
- OR enter RMA SHORT at a resistance level during a downtrend
- Price needs to trend steadily (not choppy)
- Ideal: 8-10 point move over 30-60 minutes

### How to Trigger This Test
1. Wait for clean trend day
2. Enter RMA LONG on pullback OR RMA SHORT on rally
3. Let T1 fill
4. Watch price continue trending
5. DO NOT manually intervene

### What to Watch in Output Window

**GOOD - Expected Log Sequence:**
```
T1 FILLED: 1 contracts @ 6993.87 | Remaining: 1
STOP QTY UPDATED: 1 contracts @ 6995.60
[Price moves +2 more points in your favor]
STOP UPDATED: RMAShort_123456 → 6993.60 (Level: T1)
[Price moves +2 more points]
STOP UPDATED: RMAShort_123456 → 6991.60 (Level: T2)
[Price moves +1 more point]
STOP UPDATED: RMAShort_123456 → 6990.60 (Level: T3)
```

**SIGNS IT'S WORKING:**
- Multiple "STOP UPDATED" messages as price trends
- Stop price gets tighter (closer to current price)
- Level changes: BE → T1 → T2 → T3
- Stop always stays BEHIND current price (never ahead)

### Red Flags That Indicate Problems

❌ **PROBLEM 1**: Only ONE "STOP UPDATED" message
- **What it means**: Trailing stopped after first adjustment
- **Where to debug**: `ManageTrailingStops()` method around line 1436

❌ **PROBLEM 2**: Stop price doesn't change even though price moved 5+ points
- **What it means**: `ExtremePriceSinceEntry` not updating
- **Where to debug**: Lines 1446-1449 (extreme price tracking)

❌ **PROBLEM 3**: Stop moves AWAY from current price (gets looser)
- **What it means**: Math error in trail calculation
- **Where to debug**: Lines 1458-1529 (trail level calculations)

### How to Know It Passed
✅ You see 3+ "STOP UPDATED" messages
✅ Each update has a tighter stop price
✅ Final stop is within 1-2 points of current price
✅ Position exits at favorable price when reversal happens

---

## TEST SCENARIO 2: Slow Drift (Incremental Moves)

### What You're Testing
**Question**: If price moves slowly (0.25-0.50 points at a time), does trailing still adjust?

### Market Conditions to Wait For
- Sideways choppy market with small 1-2 point range
- Slow grind higher or lower (not trending)
- Price moves in 0.25-0.50 point increments
- Ideal: Low volatility session (11am-1pm ET)

### How to Trigger This Test
1. Enter RMA position during lunch hour chop
2. Wait for T1 to fill
3. Watch price slowly drift in your favor
4. Monitor for 30-45 minutes minimum

### What to Watch in Output Window

**GOOD - Expected Behavior:**
```
T1 FILLED: 1 contracts @ 6993.87 | Remaining: 1
[15 minutes pass, price drifts 0.25 down]
[Another 10 minutes, price drifts 0.50 down]
[Price now 1 point better than T1]
STOP UPDATED: RMAShort_123456 → 6994.85 (Level: T1)
[Another 20 minutes, price drifts 0.50 down]
[Price now 2 points better than entry]
STOP UPDATED: RMAShort_123456 → 6994.10 (Level: BE)
```

**SIGNS IT'S WORKING:**
- Trailing triggers even on small moves
- Doesn't require big jumps to adjust
- Each bar close re-evaluates stop level
- Eventually locks in profit even in choppy market

### Red Flags That Indicate Problems

❌ **PROBLEM 1**: Price drifts 3+ points but no stop adjustment
- **What it means**: Trigger thresholds too tight OR bar-by-bar tracking broken
- **Where to debug**: `OnBarUpdate()` line 624 (calls ManageTrailingStops)

❌ **PROBLEM 2**: Stop only updates on big moves (2+ points)
- **What it means**: Logic requires larger profit before adjusting
- **Where to debug**: Trail trigger conditions (lines 1459, 1477, 1495, 1513)

❌ **PROBLEM 3**: Trailing freezes after first adjustment
- **What it means**: `CurrentTrailLevel` not allowing next level
- **Where to debug**: Level checks (`pos.CurrentTrailLevel < X`)

### How to Know It Passed
✅ Stop adjusts even on 0.50-1.00 point moves
✅ Multiple adjustments over 30+ minute period
✅ Final profit locked in despite choppy price action
✅ Position doesn't get stopped out prematurely

---

## TEST SCENARIO 3: Gap Moves (Sudden Jumps)

### What You're Testing
**Question**: If price gaps 5+ points in your favor instantly, does trailing adjust on the NEXT bar?

### Market Conditions to Wait For
- News release (CPI, FOMC, NFP)
- Market open (9:30am ET) with overnight gap
- Fast market conditions
- Sudden breakout from consolidation

### How to Trigger This Test
1. Enter RMA position BEFORE news event
2. Let T1 fill before news
3. News hits → price gaps 5-10 points instantly
4. Watch next bar close for stop adjustment

### What to Watch in Output Window

**GOOD - Expected Behavior:**
```
T1 FILLED: 1 contracts @ 6993.87 | Remaining: 1
STOP QTY UPDATED: 1 contracts @ 6995.60
[NEWS HITS - Price gaps from 6992 to 6985 instantly]
STOP UPDATED: RMAShort_123456 → 6987.00 (Level: T2)
[Next bar closes]
STOP UPDATED: RMAShort_123456 → 6986.00 (Level: T3)
```

**SIGNS IT'S WORKING:**
- Stop adjusts IMMEDIATELY on next bar close after gap
- New stop reflects the gap move (jumps several points)
- Trail level may skip from T1 → T3 (that's OK)
- Stop stays behind current price even after gap

### Red Flags That Indicate Problems

❌ **PROBLEM 1**: Gap happens but stop stays at old price
- **What it means**: `ExtremePriceSinceEntry` didn't capture gap
- **Where to debug**: Lines 1446-1449 (uses Close[0] for extreme)

❌ **PROBLEM 2**: Stop moves but gets you stopped out immediately
- **What it means**: Stop validation moved it TOO CLOSE to market
- **Where to debug**: `ValidateStopPrice()` method line 1403

❌ **PROBLEM 3**: Strategy crashes or stops updating after gap
- **What it means**: Exception thrown during extreme price update
- **Where to debug**: Error handling in `ManageTrailingStops()`

### How to Know It Passed
✅ Stop adjusts within 1-2 bars after gap
✅ New stop price reflects full gap move
✅ Position stays open and continues trailing
✅ Eventually exits at even better price after gap

---

## TEST SCENARIO 4: Reversals (Lock Profit, Then Hold)

### What You're Testing
**Question**: After trailing tightens, if price reverses against you, does stop HOLD at locked-in level (not move worse)?

### Market Conditions to Wait For
- V-shaped reversal pattern
- Price trends your direction, then sharply reverses
- Ideal: After morning trend, lunchtime reversal

### How to Trigger This Test
1. Enter RMA during morning trend
2. Let trailing tighten as trend continues
3. Wait for sharp reversal (price moves 3+ points against you)
4. Verify stop does NOT move worse

### What to Watch in Output Window

**GOOD - Expected Behavior:**
```
STOP UPDATED: RMAShort_123456 → 6988.00 (Level: T3)
[Price at 6987, then reverses UP to 6990]
[NO NEW STOP UPDATED MESSAGE]
[Price continues UP to 6993]
[Still NO STOP UPDATED MESSAGE]
[Price hits 6995]
STOP FILLED: 1 contracts @ 6988.10
```

**SIGNS IT'S WORKING:**
- After trailing tightens, NO "STOP UPDATED" messages on reversals
- Stop price NEVER gets looser (further from entry)
- Eventually stopped out at last known tight stop
- Profit locked in despite reversal

### Red Flags That Indicate Problems

❌ **PROBLEM 1**: Stop moves WORSE during reversal
- **What it means**: CRITICAL BUG - trailing is bi-directional
- **Where to debug**: Lines 1465, 1483, 1501, 1519 (comparison logic)

❌ **PROBLEM 2**: Stop gets hit immediately on small reversal
- **What it means**: Trail distance too tight
- **Where to debug**: `Trail1/2/3DistancePoints` property values

❌ **PROBLEM 3**: Reversal causes stop to disappear
- **What it means**: Stop order cancelled but not replaced
- **Where to debug**: `UpdateStopOrder()` method line 1539

### How to Know It Passed
✅ Stop NEVER moves against you during reversal
✅ Stop holds at tightest level achieved
✅ Eventual stop-out locks in profit
✅ No "STOP UPDATED" messages during adverse moves

---

## TEST SCENARIO 5: Time Duration (Multi-Hour Holds)

### What You're Testing
**Question**: If position stays open 4+ hours, does trailing continue working throughout?

### Market Conditions to Wait For
- Strong trend day (election, Fed day, etc.)
- Low volatility grind (take profit slowly)
- Any session where you hold 4+ hours

### How to Trigger This Test
1. Enter RMA position early (9:30am-10am ET)
2. Let it run through lunch
3. Monitor until 2pm+ ET
4. Watch for continuous stop updates

### What to Watch in Output Window

**GOOD - Expected Behavior:**
```
09:45 AM - T1 FILLED: 1 contracts @ 6993.87
09:58 AM - STOP UPDATED: → 6995.20 (Level: BE)
10:22 AM - STOP UPDATED: → 6993.50 (Level: T1)
11:10 AM - STOP UPDATED: → 6991.00 (Level: T2)
12:45 PM - STOP UPDATED: → 6989.25 (Level: T2)
01:30 PM - STOP UPDATED: → 6987.50 (Level: T3)
02:15 PM - STOP FILLED: 1 contracts @ 6987.60
```

**SIGNS IT'S WORKING:**
- Stop updates span multiple hours
- Updates continue through lunch (low volume)
- Timestamps show continuous monitoring
- Position eventually exits cleanly

### Red Flags That Indicate Problems

❌ **PROBLEM 1**: No stop updates after 11am
- **What it means**: Strategy may have stopped running
- **Where to debug**: Check NinjaTrader Strategies tab - "Running" status?

❌ **PROBLEM 2**: Stop disappears from chart after 2+ hours
- **What it means**: Order expired or cancelled
- **Where to debug**: TimeInForce setting (should be GTC)

❌ **PROBLEM 3**: Huge gap between last update and stop fill
- **What it means**: Trailing stopped adjusting mid-session
- **Where to debug**: `OnBarUpdate()` - is it being called every bar?

### How to Know It Passed
✅ Stop updates logged across 4+ hour span
✅ Updates during low AND high volatility periods
✅ Position held through lunch hour
✅ Final exit after afternoon session

---

## MASTER TESTING CHECKLIST

### Pre-Test Setup
- [ ] v5.3 BUGFIX compiled and loaded
- [ ] Output window visible and capturing all prints
- [ ] Chart set to 5-minute bars (matches strategy)
- [ ] Rithmic data feed connected
- [ ] Sim account OR small real position size
- [ ] Order Management Excel ready to track

### During Each Test
- [ ] Note exact entry time and price
- [ ] Screenshot Output window every 15 minutes
- [ ] Mark every "STOP UPDATED" message with timestamp
- [ ] Monitor Orders tab - verify stop is working
- [ ] Watch for any error messages in Output

### After Each Test
- [ ] Copy full Output log to text file
- [ ] Update Order Management Excel
- [ ] Note what worked and what didn't
- [ ] If failed, note exact failure point
- [ ] Document market conditions during test

---

## EMERGENCY STOP - WHEN TO ABORT TEST

🚨 **STOP TESTING IMMEDIATELY IF:**

1. **Stop order disappears from chart** - Manual flatten required
2. **Position shows but no working stop** - Orphaned position risk
3. **Multiple error messages in Output** - Code malfunction
4. **Stop price moves AWAY from market** - Reverse trailing bug
5. **Strategy shows "Stopped" in Strategies tab** - Not monitoring anymore

**Emergency Action**: Press **F** key to flatten all positions, then investigate.

---

## DEBUGGING REFERENCE - CODE LOCATIONS

If a test fails, here's where to look:

| Problem | Code Location | Line Range |
|---------|---------------|------------|
| Trailing not starting | `ManageTrailingStops()` | 1436-1537 |
| Extreme price not tracking | Extreme price update | 1446-1449 |
| Trail levels not progressing | Trail level conditions | 1459-1529 |
| Stop moving wrong direction | Comparison logic | 1465, 1483, 1501, 1519 |
| Stop validation issues | `ValidateStopPrice()` | 1403-1430 |
| Stop not updating on bar close | `OnBarUpdate()` calls | 623-624 |
| Stop disappearing | `UpdateStopOrder()` | 1539-1566 |
| Orphaned orders | `CleanupPosition()` | 1682-1734 |

---

## SUCCESS CRITERIA - ALL TESTS MUST PASS

To consider trailing stops "production ready":

✅ **Scenario 1**: Continuous moves → 3+ stop adjustments
✅ **Scenario 2**: Slow drift → Adjusts on small 0.5-1pt moves
✅ **Scenario 3**: Gaps → Immediate adjustment on next bar
✅ **Scenario 4**: Reversals → Stop NEVER moves worse
✅ **Scenario 5**: Duration → Works for 4+ hours straight

**If even ONE test fails**: Do NOT trade live until debugged and re-tested.

---

## NOTES SECTION - FILL IN DURING TESTING

### Test 1: Continuous Movement
**Date**: ___________
**Result**: ☐ PASS ☐ FAIL
**Notes**: ___________________________________________

### Test 2: Slow Drift
**Date**: ___________
**Result**: ☐ PASS ☐ FAIL
**Notes**: ___________________________________________

### Test 3: Gap Moves
**Date**: ___________
**Result**: ☐ PASS ☐ FAIL
**Notes**: ___________________________________________

### Test 4: Reversals
**Date**: ___________
**Result**: ☐ PASS ☐ FAIL
**Notes**: ___________________________________________

### Test 5: Time Duration
**Date**: ___________
**Result**: ☐ PASS ☐ FAIL
**Notes**: ___________________________________________

---

## FINAL APPROVAL SIGNATURE

Once ALL 5 tests pass, sign here before live trading:

**Tested By**: ___________
**Date**: ___________
**Account**: Sim ☐ Live ☐
**Approved for Live**: YES ☐ NO ☐

---

**END OF TEST PLAN**

*Print this document and check off items as you test. Your live account depends on it.*
