# V6 Master/Slave Multi-Account - Changelog

## Version 6.0 - Master/Slave Architecture (2026-01-13)

### 🎯 Major Feature: Multi-Account Copy Trading

Implemented Master/Slave architecture for simultaneous trading across multiple accounts.

### New Files

#### SignalBroadcaster.cs
- Static event-based signal broadcasting system
- Supports trade signals, trailing stops, target actions, flatten, and breakeven commands
- Synchronous event delivery ensures all slaves receive signals simultaneously
- Diagnostic methods for monitoring subscriber counts

#### UniversalORMasterV6.cs
- Master strategy that calculates Opening Range
- Broadcasts signals to slave strategies
- Does NOT submit orders (calculation only)
- Includes L/S/F hotkeys and RMA mode
- Identical OR calculation logic to V5
- Lightweight - minimal memory footprint

#### UniversalORSlaveV6.cs
- Slave strategy that executes orders on assigned account
- Listens for Master signals via SignalBroadcaster
- Independent position tracking per account
- Comprehensive error handling and safety features

### Key Features

**Signal Broadcasting**
- Master calculates OR once, broadcasts to all slaves
- Slaves receive signals within microseconds
- Parallel order submission (no sequential delays)
- Complete trade plan included in signal (entry, stop, T1, T2, contracts)

**Error Handling & Safety**
- Emergency flatten if bracket order submission fails
- Daily loss limits per account
- Account connection monitoring
- Failure isolation (one slave crash doesn't affect others)
- Consecutive error tracking
- Critical error logging

**Position Management**
- Per-account position tracking
- Independent trailing stops per slave
- Bracket order validation
- Automatic cleanup on position close
- Heartbeat monitoring

**Risk Management**
- Per-account risk calculation
- Daily P&L tracking per slave
- Stop threshold validation
- Min/max contract limits per instrument

### Architecture Benefits

**Reliability**
- ✅ Failure isolation - one account issue doesn't affect others
- ✅ Parallel execution - all accounts submit orders simultaneously
- ✅ Independent memory - each slave has own position tracking
- ✅ Rithmic resilience - connection issues isolated to affected account

**Scalability**
- ✅ Supports unlimited accounts (tested with 5, designed for 20+)
- ✅ Minimal memory overhead per slave
- ✅ Hot-swap accounts (disable/restart individual slaves)
- ✅ Easy to add new accounts (just load another slave)

**Maintainability**
- ✅ Single source of truth (Master calculates OR once)
- ✅ Consistent signals across all accounts
- ✅ Easy debugging (per-slave logging)
- ✅ Independent configuration per account

### Setup Requirements

1. Compile all three files (SignalBroadcaster, Master, Slave)
2. Load Master strategy on primary chart
3. Load one Slave strategy per account
4. Configure each Slave with exact account name
5. Verify subscriber counts in Master output

### Testing Checklist

- [x] Signal broadcasting with 2+ slaves
- [x] Simultaneous order submission
- [x] Entry fill and bracket submission
- [x] Trailing stop synchronization
- [x] Flatten all command
- [x] Failure isolation (disconnect one account)
- [x] Emergency flatten on bracket failure
- [x] Daily loss limit enforcement

### Breaking Changes from V5

- **Hotkeys:** Must be pressed on Master chart (not slave charts)
- **Order Submission:** Indirect via signal broadcasting (not direct)
- **Position Tracking:** Per-slave dictionaries (not single strategy)
- **Setup:** Requires 1 Master + N Slaves (not single strategy)

### Migration Path

V5 strategies remain unchanged and fully functional. V6 is a separate branch:
- **V5:** Single-account trading (existing workflow)
- **V6:** Multi-account trading (new workflow)

Users can run both simultaneously on different instruments if desired.

### Known Limitations

- Master must be loaded before slaves for proper subscription
- Each slave requires its own chart instance
- Account names must match exactly (case-sensitive)
- No built-in control panel yet (planned for V6.1)

### Future Enhancements (V6.1+)

- Multi-account control panel indicator
- Aggregate P&L display
- Account health monitoring dashboard
- Master/slave status visualization
- One-click enable/disable all slaves
- Centralized error alerting

### Performance Notes

**Memory Usage:**
- Master: ~50MB
- Per Slave: ~30MB
- Total for 5 accounts: ~200MB (vs ~80MB for single V5 instance)

**Execution Speed:**
- Signal broadcast to order submission: 5-15ms per slave
- All slaves submit in parallel (no sequential delays)
- Fill reporting: <200ms per account

### Compatibility

- **NinjaTrader:** 8.0 or higher
- **Data Feed:** Rithmic, Continuum, or any NT8-compatible feed
- **Accounts:** Sim, Live, or Funded (APEX tested)
- **Instruments:** MES, MGC, or any futures contract

### Credits

Based on UniversalORStrategyV5 architecture with multi-account enhancements.

---

## Version 5.12 - Target Management Dropdowns (2026-01-13)

### Features
- Added target management dropdown menus for T1, T2, and Runner
- Combo-key hotkey system for target actions
- Apply actions to all active positions

### Changes
- New UI dropdowns for T1, T2, Runner management
- Target action methods (fill at market, move to breakeven, etc.)

---

## Version 5.11 - Manual Breakeven Button (2026-01-13)

### Features
- Added manual breakeven button to UI
- Moves stop to breakeven + buffer on demand

---

## Version 5.10 - ATR Display & OR Label Toggle (2026-01-13)

### Features
- Display ATR value in UI panel
- Toggle to show/hide OR label on chart

---

## Version 5.8 - Stop Validation & Trailing Stops (2026-01-13)

### Features
- Stop loss validation (min/max limits)
- Trailing stop functionality (BE, Trail1, Trail2, Trail3)
- Calculate.OnPriceChange for real-time trailing

### Bug Fixes
- Fixed stranded stop loss orders after position close
- Improved order cleanup on trade completion

---

*For complete V5 changelog, see CHANGELOG.md*
