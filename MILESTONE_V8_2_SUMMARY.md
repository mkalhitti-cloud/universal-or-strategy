# V8.2 Milestone Summary - TREND Strategy

## Release Date: January 18, 2026

## Overview
V8.2 adds the TREND strategy with dual EMA entries, dynamic trailing stops, and full 4-target bracket system.

---

## New Features

### TREND Strategy
- **Dual EMA Entry System**
  - Entry 1 (1/3 position) at 9 EMA limit order
  - Entry 2 (2/3 position) at 15 EMA limit order
  
- **Stop Logic**
  - E1: Fixed 2pt stop → switches to EMA9 trail when price crosses in favor
  - E2: Trails at EMA15 - (1.1 × ATR) from entry

- **Multi-Target System** (same as RMA)
  - T1: 1pt fixed
  - T2: 0.5x ATR
  - T3: 1x ATR
  - T4: Runner (trails)

- **Contract Distribution**
  | Contracts | T1 | T2 | T3 | Runner |
  |-----------|----|----|----|----|
  | 1 | 0 | 0 | 0 | 1 |
  | 2 | 1 | 0 | 0 | 1 |
  | 3 | 1 | 1 | 0 | 1 |
  | 4 | 1 | 1 | 1 | 1 |
  | 5+ | 20% | 30% | 30% | 20% |

---

## Bug Fixes

### BarsInProgress Context Fix
- **Issue:** Button click events fire in BarsInProgress=1 (ATR series), causing EMA reads from wrong series
- **Fix:** Deferred TREND entry to OnBarUpdate when BarsInProgress=0

### EMA Value Accuracy Fix
- **Issue:** EMA[0] included current tick, not matching chart's "On bar close" indicators
- **Fix:** Using EMA[1] (previous bar close) for entry price calculation

### Stop Fill Cleanup Fix
- **Issue:** Trailed stops with DateTime.Ticks suffix weren't matched by object reference
- **Fix:** Added fallback name-based matching to extract entry name from stop order name

---

## Files Changed

| File | Change |
|------|--------|
| `UniversalORStrategyV8_2.cs` | New TREND strategy implementation |

---

## Testing Verified

- ✅ Dual EMA entries at correct prices (matching chart indicators)
- ✅ 4-target brackets (T1/T2/T3/Runner)
- ✅ E1 fixed stop → EMA9 trail transition
- ✅ E2 EMA15 trailing stop
- ✅ Stop fill cleanup for both entries
- ✅ Target cancellation on stop out

---

## Configuration

| Parameter | Default |
|-----------|---------|
| TRENDEnabled | true |
| TRENDEntry1StopPoints | 2 |
| TRENDEntry2ATRMultiplier | 1.1 |

---

## Usage

1. Click **TREND** button (teal) on chart panel
2. Direction auto-detected: EMA below price = LONG, above = SHORT
3. Two limit orders placed at EMA9 and EMA15
4. Each entry gets independent stop and 4-target bracket
