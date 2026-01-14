# CLAUDE.md - AI Assistant Context File

This file provides context for AI assistants (Claude, etc.) working on this codebase.

## Project Overview

**Universal Opening Range Strategy** - A NinjaTrader 8 trading strategy for opening range and other trade setups.

**Purpose**: Live funded trading on Apex accounts using Rithmic data feed.

**User Profile**: Mo - Non-coder trader who needs complete, compilable code with clear instructions.

---

## Critical Context

### Trading Environment
- **Platform**: NinjaTrader 8
- **Data Feed**: Rithmic (NOT Continuum)
- **Broker**: Apex Funded Account
- **Instruments**: MES (Micro E-mini S&P), MGC (Micro Gold)
- **User Location**: California (Pacific Time, but trades on Eastern Time)

### Architecture Constraints
- **IsUnmanaged = true**: Strategy uses unmanaged orders for full control
- **RAM Sensitivity**: Must run on laptop with 20+ charts, memory at 80%+
- **Reliability Critical**: Live funded trading - no bugs allowed
- **Future Scale**: Architecture must support 20 accounts eventually

---

## Code Standards

### For Mo (Non-Coder)
1. **ALWAYS** provide complete, compilable code blocks
2. **ALWAYS** specify exact file location (region, method name)
3. **ALWAYS** show before/after when modifying existing code
4. **ALWAYS** include backup instructions before changes
5. **NEVER** provide partial code snippets
6. **NEVER** use coding jargon without explanation

### NinjaTrader Specifics
- Use `Print()` for debugging (shows in Output window)
- UI changes must use `ChartControl.Dispatcher.InvokeAsync()`
- Order names must be unique (use timestamp suffix)
- Stop orders need validation (can't be at/past market price)

### Naming Conventions
- Entry orders: `Long_[timestamp]` or `Short_[timestamp]`
- Stop orders: `Stop_[entryName]`
- Target orders: `T1_[entryName]`, `T2_[entryName]`
- Flatten orders: `Flatten_[entryName]`

---

## Current Version: V4

### Key Changes from V3
1. **Box instead of Rays** - Single `Draw.Rectangle()` replaces multiple rays
2. **Session-based timing** - `SessionStart`, `SessionEnd`, `ORTimeframe`
3. **Timeframe dropdown** - User selects 1, 5, 10, or 15 minute OR
4. **RAM optimizations** - Reduced string allocations, pooled objects

### V4 Properties Structure
```
1. Session Settings
   - SessionStart (time)
   - SessionEnd (time)
   - ORTimeframe (dropdown: 1, 5, 10, 15)
   - SelectedTimeZone (dropdown)

2. Risk Management (unchanged from V3)
3. Stop Loss (unchanged from V3)
4. Profit Targets (unchanged from V3)
5. Trailing Stops (unchanged from V3)
6. Display
   - ShowMidLine
   - BoxOpacity
```

---

## Common Issues & Solutions

### "Stop at market" Error
**Cause**: Stop price too close to current price
**Solution**: SubmitValidatedStop() method ensures 4-tick buffer

### Position Tracking Mismatch
**Cause**: External flatten (Control Center) doesn't notify strategy
**Solution**: OnPositionUpdate() cleans up internal tracking

### Memory Growth
**Cause**: Rays accumulate, strings allocated repeatedly
**Solution**: V4 uses single box, StringBuilder pooling

### Order Rejection
**Cause**: Various (stop at market, duplicate name, insufficient margin)
**Solution**: Unique order names, validated stop prices, risk checks

---

## Testing Checklist

Before any production deployment:
- [ ] Compiles without errors or warnings
- [ ] Test on Market Replay (not just backtest)
- [ ] Sim account for at least 1 hour
- [ ] Verify OR box draws correctly
- [ ] Verify hotkeys work (L/S/F)
- [ ] Verify stops submit correctly
- [ ] Verify targets submit correctly
- [ ] Verify trailing stop updates
- [ ] Test flatten functionality
- [ ] Monitor memory for 1+ hour

---

## File Structure

```
UniversalORStrategy/
â”œâ”€â”€ README.md           # User documentation
â”œâ”€â”€ CHANGELOG.md        # Version history
â”œâ”€â”€ PLAN.md            # Development roadmap
â”œâ”€â”€ CLAUDE.md          # This file (AI context)
â”œâ”€â”€ archive/
â”‚   â”œâ”€â”€ v2/            # UniversalORStrategy_CD_v2_BACKUP.cs
â”‚   â””â”€â”€ v3/            # UniversalORStrategyV3.cs
â”œâ”€â”€ src/
â”‚   â””â”€â”€ v4/            # UniversalORStrategyV4.cs (current)
â””â”€â”€ docs/              # Additional documentation
```

---

## Communication Protocol

### When Starting a Session
Ask Mo for:
1. Current status of last changes
2. Results of last testing
3. Immediate goals for this session

### When Ending a Session
Provide Mo with:
1. Summary of changes made
2. What was tested and results
3. Next steps recommended
4. Any risks or concerns

### Code Changes
1. Always show the complete method/region being changed
2. Explain in trading terms (not coding terms)
3. Provide step-by-step instructions for implementation
4. Include backup reminder

---

## Quick Reference

### Key Methods
- `OnBarUpdate()` - Main logic loop, called on each bar
- `OnOrderUpdate()` - Handles all order state changes
- `OnPositionUpdate()` - Detects external position changes
- `ExecuteLong()` / `ExecuteShort()` - Entry submission
- `ManageTrailingStops()` - Updates stops based on profit
- `SubmitValidatedStop()` - Safe stop submission with validation
- `FlattenAll()` - Emergency exit all positions

### Key Variables
- `sessionHigh`, `sessionLow`, `sessionRange` - OR levels
- `orComplete` - True when OR window ends
- `activePositions` - Dictionary of tracked positions
- `tickSize`, `pointValue` - Instrument specifics

### Hotkeys
- **L** - Execute Long
- **S** - Execute Short
- **F** - Flatten All

---

## Version History Summary

| Version | Key Feature | Status |
|---------|-------------|--------|
| V4 | Box visualization, RAM optimized | Current |
| V3 | Multi-target, trailing stops | Archive |
| V2 | Unmanaged orders, basic bracket | Archive |
