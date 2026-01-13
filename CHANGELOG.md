# Changelog

All notable changes to the Universal Opening Range Strategy will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [5.4_PERFORMANCE] - 2026-01-12 - MILESTONE "Trailing Stops Validated"

### Validated
- **Trailing stop progression**: BE → T1 → T2 levels working perfectly in live trading
- **Order cleanup**: 100% success rate across all trade exits
- **Multi-level trailing**: Confirmed stops advance through all profit levels
- **No system freezes**: Performance optimizations resolved previous NT8 freeze issues

### Test Results
- 4 live RMA trades analyzed (MES/MGC)
- All trades showed proper stop movement and cleanup
- Trade example: MGC RMALong advanced through BE → T1 → T2 trail levels
- Zero stranded orders after exits
- Clean bracket management with 2-18 contract positions

### Production Status
- ✅ **APPROVED FOR LIVE FUNDED TRADING**
- No critical issues identified
- Reliable performance across multiple sessions and instruments

### Files
- See `MILESTONE_V5_4_PERFORMANCE_SUMMARY.md` for detailed trade analysis

---

## [5.2.0] - 2026-01-09 - MILESTONE "Native Click Conversion"

### Added
- **Percentage-based click conversion**: RMA clicks now work at ANY window size
- **Overnight session detection**: OR box correctly extends to next day for sessions crossing midnight

### Changed
- Click conversion formula: `effectiveHeight = panelHeight Ã— 0.667`
- OR box end time calculation adds 1 day for overnight sessions

### Fixed
- RMA click accuracy broken when resizing windows (was using fixed pixel offsets)
- OR box drawing backwards (to the left) for overnight sessions like 21:00-16:00

### Technical
- Removed `RMAChartYOffset` and `RMABottomAxisHeight` properties (no longer needed)
- Click conversion now fully automatic - no calibration required

---

## [5.1.0] - 2026-01-08

### Added
- **Fixed pixel offset tuning**: Instrument-specific offset values for click conversion
- **Auto-detection**: MES/ES vs MGC/GC toolbar configurations

### Fixed
- Initial RMA click-to-price conversion issues

### Known Issues
- Fixed pixel offsets only work at specific window sizes (addressed in V5.2)

---

## [5.0.0] - 2026-01-07

### Added
- **RMA Subsystem**: Range Market Analysis click-anywhere limit entries
- **Shift+Click entries**: Click on chart to place limit orders at exact price
- **Direction detection**: Clicks above price = SHORT, clicks below = LONG
- **ATR-based targets**: RMA uses ATR for stops and targets (configurable multipliers)
- **RMA button**: Toggle RMA mode from UI panel
- **Multi-strategy framework**: Architecture to support OR + RMA in same strategy

### Changed
- Strategy now supports two entry methods: OR breakouts and RMA limit entries
- Position tracking expanded to handle both OR and RMA positions independently

### Technical
- Mouse click handling via `PreviewMouseLeftButtonDown` event
- Coordinate conversion from screen pixels to price levels
- Separate position info tracking for OR vs RMA entries

---

## [4.0.1] - 2026-01-06 - MILESTONE

### Added
- **OR Target visualization**: Optional display of T1/T2 target lines on chart
- **Enhanced logging**: More detailed position and order tracking

### Fixed
- External close detection edge cases
- Orphaned order cleanup reliability

---

## [4.0.0] - 2025-01-05

### Added
- **Box Visualization**: Replaced ray drawing with single Rectangle box for OR range (major RAM reduction)
- **Session-Based Time Properties**: New session start/end times instead of just OR window
- **Timeframe Dropdown**: Select OR duration (1, 5, 10, 15 minutes) from dropdown menu
- **Memory Optimizations**: Reduced object allocations, pooled string builders
- **GitHub Repository Structure**: Organized codebase with archive, docs, and version folders

### Changed
- Drawing system from 3+ rays to single box (reduces draw objects by ~70%)
- Time property structure to be more intuitive for multi-session trading
- Default timeframe handling to support multiple OR durations

### Removed
- Individual ray lines for OR high/low/mid (replaced with single box)
- Redundant text labels (integrated into box or panel)

### Technical
- Reduced memory footprint by ~40% compared to V3
- Optimized string concatenation with StringBuilder pooling
- Simplified draw object management

---

## [3.0.0] - 2025-01-04

### Added
- **Multi-Target System**: Three independent profit targets (T1, T2, T3)
- **Advanced Trailing Stops**: Stepped trailing system (BE â†’ T1 â†’ T2 â†’ T3)
- **Stop Validation**: Prevents "stop at market" errors with buffer logic
- **Position Sync**: Detects and handles external position changes
- **Flatten Button**: UI button and hotkey (F) to flatten all positions
- **Draggable Panel**: Move the control panel anywhere on the chart
- **Reduced Risk Mode**: Lower position size when volatility is high
- **OnPositionUpdate Handler**: Cleans up if position closed externally

### Changed
- Contract allocation now splits into 3 targets (33/33/34%)
- Stop management uses validated submission with market-distance checks
- UI panel now draggable with title bar handle

### Fixed
- "Stop above/below market" rejection errors
- Orphaned orders when position flattened externally
- Memory leaks from uncleaned position tracking

---

## [2.0.0] - 2025-01-02

### Added
- **Unmanaged Order Mode**: Full control over bracket orders
- **Basic Stop/Target**: Single stop and single target per entry
- **Hotkey Support**: L for Long, S for Short
- **UI Panel**: Basic control panel with entry buttons
- **Time Zone Support**: Eastern, Central, Mountain, Pacific, UTC

### Changed
- Migrated from managed to unmanaged order handling
- Entry orders placed as stop orders at OR high/low + 3 ticks

### Known Issues
- Ray lines can cause memory buildup on long sessions
- No handling for external position changes

---

## [1.0.0] - Initial Development

### Added
- Basic Opening Range detection
- Simple breakout entry logic
- Ray line visualization for OR levels
- Basic position management

---

## Migration Guide

### V4 â†’ V5

1. **New RMA Feature**:
   - Enable with `RMAEnabled = true`
   - Shift+Click on chart to place limit entries
   - Configure ATR period and multipliers for RMA stops/targets

2. **No Breaking Changes**:
   - OR functionality unchanged
   - All V4 settings still work
   - RMA is additive, not replacement

### V3 â†’ V4

1. **Time Settings Changed**:
   - Old: `ORStartTime`, `OREndTime`
   - New: `SessionStart`, `SessionEnd`, `ORTimeframe`
   - V4 calculates OR window from session start + timeframe

2. **Visual Changes**:
   - Box replaces individual lines
   - Mid-line now inside box (optional)

3. **No Functional Changes**:
   - Entry logic unchanged
   - Risk management unchanged
   - Trailing stops unchanged

### V2 â†’ V3

1. **Single target â†’ Multi-target**:
   - Configure T1, T2, T3 percentages
   - T3 has no limit, exits only via trailing stop

2. **New Parameters**:
   - `ReducedRiskPerTrade`
   - `StopThresholdPoints`
   - `T1ContractPercent`, `T2ContractPercent`, `T3ContractPercent`
