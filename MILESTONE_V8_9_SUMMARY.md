# Universal OR Strategy V8.9 Milestone

**Date**: 2026-01-19
**Commit**: d49efa7
**Status**: Unified Stop/Target System Across All Trade Types

---

## Overview

V8.9 standardizes the stop loss multiplier for RMA trades and establishes a unified target system across all six trade entry modes. This ensures consistency in risk management while preserving the unique trailing logic for each strategy type.

---

## Key Changes

### 1. RMA Stop Multiplier Standardization
- **Previous**: 1.0x ATR
- **Updated**: 1.1x ATR
- **Rationale**: Aligns with TREND/RETEST stop multiplier for consistent risk sizing

### 2. Unified Target System
All trades now use the same 4-target framework:
- **T1**: +1.0 point fixed (quick scalp, 20% of contracts)
- **T2**: +0.5x ATR (30% of contracts)
- **T3**: +1.0x ATR (30% of contracts)
- **T4**: Runner with trailing stop (20% of contracts)

**Exception**: OR trades use OR Range instead of ATR for T2/T3 (specific to opening range setup)

---

## Complete Stop Loss Specification

| Trade Type | Entry Method | Initial Stop | Trail Trigger | Trail Logic |
|------------|--------------|--------------|---------------|-------------|
| **OR** | StopMarket at OR±3ticks | 0.5x ATR (1-8pt) | +2pts profit | Fixed point trail: BE→T1→T2→T3 |
| **RMA** | Limit at click price | 1.1x ATR | +2pts profit | Fixed point trail: BE→T1→T2→T3 |
| **TREND E1** | Limit at 9 EMA | 2.0pt fixed | Price crosses EMA9 | EMA9 - 1.1x ATR trail |
| **TREND E2** | Limit at 15 EMA | EMA15 - 1.1x ATR | Immediate | EMA15 - 1.1x ATR trail |
| **RETEST** | Limit at OR High/Low | 1.1x ATR | Price crosses EMA9 | EMA9 - 1.1x ATR trail |
| **MOMO** | StopMarket at click | 0.5pt fixed | +2pts profit | Fixed point trail: BE→T1→T2→T3 |
| **FFMA** | Market at reversal | Candle H/L (min 2t) | +2pts profit | Fixed point trail: BE→T1→T2→T3 |

---

## Standard Trailing Levels (OR, RMA, MOMO, FFMA)

1. **Breakeven (Level 1)**: At +2pts → Entry + 1 tick
2. **Trail 1 (Level 2)**: At +3pts → Extreme - 2pts
3. **Trail 2 (Level 3)**: At +4pts → Extreme - 1.5pts
4. **Trail 3 (Level 4)**: At +5pts → Extreme - 1.0pt

**Frequency Control**:
- Breakeven & Trail 3: Check every tick
- Trail 1 & Trail 2: Check every other tick (reduces order spam)

---

## EMA-Based Trailing (TREND, RETEST)

### TREND E1 (9 EMA Entry)
- **Phase 1**: 2pt fixed stop until price crosses EMA9
- **Phase 2**: EMA9 - 1.1x ATR trail (ratchets favorably only)

### TREND E2 (15 EMA Entry)
- **Continuous**: EMA15 - 1.1x ATR trail from entry (ratchets favorably only)

### RETEST (OR High/Low Entry)
- **Phase 1**: 1.1x ATR fixed stop until price crosses EMA9
- **Phase 2**: EMA9 - 1.1x ATR trail (ratchets favorably only)

---

## Contract Distribution

### Small Positions (1-4 contracts)
| Contracts | T1 | T2 | T3 | T4 |
|-----------|----|----|----|----|
| 1 | 0 | 0 | 0 | 1 |
| 2 | 1 | 0 | 0 | 1 |
| 3 | 1 | 1 | 0 | 1 |
| 4 | 1 | 1 | 1 | 1 |

### Larger Positions (5+ contracts)
- T1: 20% of contracts (minimum 1)
- T2: 30% of contracts (minimum 1)
- T3: 30% of contracts (minimum 1)
- T4: Remainder to runner

---

## Risk Management Parameters

### OR Trade Stops
- **Multiplier**: 0.5x ATR
- **Minimum**: 1.0 point
- **Maximum**: 8.0 points
- **Logic**: If stop > 5pt threshold, use reduced risk ($200 default)

### ATR-Based Stops (RMA, RETEST)
- **Multiplier**: 1.1x ATR
- **ATR Period**: 14 (calculated on 5-min bars)
- **Logic**: If stop > 5pt threshold, use reduced risk ($200 default)

### Fixed Stops (MOMO, TREND E1)
- **MOMO**: 0.5 point (tight stop for momentum trades)
- **TREND E1**: 2.0 point (allows room for EMA cross before trail activates)

---

## UI/Display Features

✅ Fixed dropdowns (v8.9 feature)
✅ Resizable window with proportional scaling
✅ Separate buttons for each entry mode: L/S, RMA, TREND, RETEST, MOMO, FFMA
✅ Real-time position summary and OR status
✅ Manual breakeven button with configurable buffer

---

## Hotkeys

| Key | Action |
|-----|--------|
| **L** | Long entry (OR or selected mode) |
| **S** | Short entry (OR or selected mode) |
| **Shift+Click** | RMA entry at clicked price |
| **F** | Flatten all positions |
| **R** | RMA mode toggle |
| Mode Buttons | TREND, RETEST, MOMO, FFMA activation |

---

## Testing Checklist

- [x] RMA stop multiplier changed to 1.1x ATR
- [x] All trade types calculate T1/T2/T3 targets on entry
- [x] Bracket orders (stop + targets) submit when entry fills
- [x] TREND E1 switches from fixed to EMA9 trail on cross
- [x] TREND E2 continuous EMA15 trail
- [x] RETEST fixed → EMA9 trail on cross
- [x] Standard trailing works for OR/RMA/MOMO/FFMA
- [ ] Paper trade all entry modes
- [ ] Verify stop adjustments on live market
- [ ] Test target fills and runner trailing

---

## Next Steps (V9.0 Candidates)

1. **Add 2pt BE trigger to TREND/RETEST** before EMA trail activation (optional safety net)
2. **Manual target adjustment dropdowns** - modify T1/T2/T3 on existing positions
3. **Position locking** - prevent accidental closes
4. **Partial profit taking** - manual scaling out of positions
5. **Dashboard export** - save trade statistics to Excel

---

## Files Changed

- `UniversalORStrategyV8_9.cs` (+4,854 lines)
  - RMA stop multiplier: 1.0x → 1.1x ATR (line 586)
  - All trade entry logic uses unified target system
  - Trailing logic preserved for each entry type

---

## Known Limitations

- OR trades still use OR Range for T2/T3 (not ATR like others)
- TREND/RETEST skip standard BE levels (go straight to EMA trail)
- Manual breakeven only available during position (can't arm after entry)
- Runner tail doesn't lock profit at specific levels (pure trail only)

---

**Author**: Universal OR Strategy Development
**Status**: Ready for live testing
**Last Updated**: 2026-01-19
