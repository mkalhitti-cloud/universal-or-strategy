# Changelog

All notable changes to the Universal Opening Range Strategy will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [V10.3] - 2026-01-27
### Added
- **OR Entry Migration**: Ported `ExecuteLong`, `ExecuteShort`, and `EnterORPosition` from V8.31 to V10.
- **Granular Target Management**: Implemented `FlattenSpecificTarget(targetNum)` IPC handler for precision closing of T1, T2, T3, T4, or Runner.
- **Remote App UI Overhaul**:
    - Added `OR LONG` and `OR SHORT` buttons for manual breakout entries.
    - Added target control grid (`T1`, `T2`, `T3`, `T4`, `RUN`) for instant manual target flattening.
    - Removed unused `AUTO` button.
- **Low-Latency Integration**: All new commands leverage the V10.2 Hybrid Dispatcher for near-instant execution.

## [V10.2] - 2026-01-28 - MILESTONE "Hybrid Dispatcher & Multi-Instrument Fix"
### Added
- **Hybrid IPC Dispatcher**: Implemented internal signal broadcasting via `SignalBroadcaster`. 
- **MES Command Gap Fix**: The instance that owns the TCP port now dispatches commands to other instances (e.g., MGC → MES).
- **Case-Insensitive Matching**: Hardened symbol recognition for micro-futures labels.
- **V10 Stable Class Protocol**: New development workflow where filenames increment (V10_2, V10_3) but the internal class remains `UniversalORStrategyV10`.

### Changed
- **Deployment Workflow**: Older `.cs` files in NinjaTrader are now automatically renamed to `.bak` during deployment to prevent class naming conflicts while preserving history.
- **UI Refresh**: Unified version labels to "V10.2 - HYBRID" on charts and logout windows.

---

## [V10.1] - 2026-01-27 - MILESTONE "Hardened Remote Logic"
### 🛠️ What was changed in this session:
- **V10.2 Low Latency (Hybrid Dispatcher)**:
    - Implemented `TriggerCustomEvent` to force instant command processing.
    - Eliminated reliance on `OnMarketData` ticks for button responsiveness.
- **V10.1 Hardening (Trim Logic)**:
    - Replaced `Math.Round` with conservative `Math.Floor` for quantity calculation.
    - Implemented safety locks: Skip trim if contracts < 4 or if trim results in 0 contracts.
- **IPC Execution Bridge**:
    - **Fix**: Replaced `return` with `continue` in the instrument filter loop.
    - **New**: Implemented `ExecuteRMAEntryCustom` for direct Remote App `LONG`/`SHORT` execution.
### Fixed
- **IPC Command Filter Bug**: Replaced `return` with `continue` in `ProcessIpcCommands`. Previous version would stop processing the entire queue if one command was for a different instrument.
- **Trim Math Safety**: 
  - Changed calculation to `Math.Floor` for conservative sizing.
  - Added safety checks to prevent trims on small positions (minimum 4 contracts for 25% trim).
- **Off-Hour Lag**: Added `OnMarketData` tick-hook to process IPC commands immediately. Fixed issue where buttons only worked on bar close.
- **Missing Entry Method**: Implemented `ExecuteRMAEntryCustom` to handle direct market orders from the Remote App.

### Added
- **Build 1505 Stamp**: Added version comment at the top of the file for easier tracking.

---

## [V10.0] - 2026-01-27 - MILESTONE "Global Integration Edition"

## [V9.1.7] - 2026-01-27 - MILESTONE "The Power Cluster"
### Added
- **5-EMA Cluster (V9 Remote)**: Corrected and synchronized the full EMA stack (9, 15, 30, 65, 200).
- **Opening Range Integration**: Added real-time tracking from Thinkorswim (Custom 9 & 11).
- **1H Trend Flag**: High-timeframe trend filter color-coded in UI (Custom 17).

### Changed
- **UI Refresh**: Spaced out EMA values and bumped version to **v9.1.7**.
- **RTD Optimization**: Improved subscription stability for faster value updates.

---

## [8.31] - 2026-01-27 - "Harden & TREND Update"

### Added
- **TREND E1 ATR Stop**: Entry 1 (9 EMA) now uses an ATR-based stop (1.1x from live EMA9) instead of a fixed 2-point stop.
- **Risk Expansion**: `MaximumStop` default increased from 8.0 to 15.0 points to accommodate higher volatility.
- **Stop Duplication Guard**: Added logic in `CreateNewStopOrder` to skip submission if a working stop already exists for that entry.

### Fixed
- **Fixed: TREND E1 Multiplier Bug**: Corrected an inconsistency where TREND E1 was using the E2 multiplier in the trailing stop calculation.
- **Flatten Expansion**: The Flatten button now explicitly cancels T3 and T4 (Runner) orders in addition to T1 and T2.
- **Pending Cleanup**: Flattening now clears the `pendingStopReplacements` queue to prevent stale stops from re-appearing.
- **Improved Flatten Logic**: Uses `Math.Min` of cached vs live quantity to prevent overselling during rapid target fills.

---

## [8.30] - 2026-01-27 - "Thread-Safe Hardened"

### Fixed
- **CRITICAL: Execution Freeze Fix** - Resolved NinjaTrader freezing during high-volatility tick storms
  - Root cause: Race condition between OnBarUpdate (strategy thread) and OnOrderUpdate (callback thread)
  - Both threads were iterating/modifying `activePositions` Dictionary without synchronization

### Changed
- **Thread-Safe Collections**: Replaced all `Dictionary<K,V>` with `ConcurrentDictionary<K,V>`:
  - `activePositions`, `entryOrders`, `stopOrders`
  - `target1Orders`, `target2Orders`, `target3Orders`, `target4Orders`
  - `pendingStopReplacements`, `linkedTRENDEntries`
- **Safe Iteration**: All `foreach` loops over shared collections now use `.ToArray()` snapshots
- **Atomic Operations**: Replaced `ContainsKey` + indexer with `TryGetValue`/`TryRemove` to eliminate TOCTOU races

### Added
- **Circuit Breaker Pattern**: Pauses trailing stop updates when 5+ pending replacements queue up
  - Prevents cascade failure during extreme tick storms
  - Auto-resets after 2 seconds of calm
- **Pending Replacement Timeout**: 5-second timeout for stale `pendingStopReplacements` entries
  - Creates emergency stop if position still needs protection
  - Prevents memory leak from orphaned entries
- **Adaptive Throttling**: `ManageTrailingStops` throttle adjusts based on tick frequency
  - Base: 100ms, increases to 500ms under heavy load
- **DrawORBox Throttling**: 200ms minimum between chart updates to prevent UI saturation

### Technical Details
- Added `System.Collections.Concurrent` and `System.Threading` namespaces
- Added `Interlocked.Increment/Decrement` for atomic counter operations
- New method: `CleanupStalePendingReplacements()` for timeout cleanup
- `PendingStopReplacement` class now includes `CreatedTime` field

### Production Status
- ✅ **APPROVED FOR TESTING** - Requires backtesting with high-tick-frequency data

---

## [V9.10] - 2026-01-26
### Added
- **Gold Standard Integration**: Consolidated features from V9_005 through V9_008.
- **Fuzzy Symbol Matching**: Resolves short symbol names (e.g. "MGC") to full charter names (e.g. "MGC FEB26").
- **Manual Execution Upgrade**: Support for TRIM_25, TRIM_50, and BE_PLUS_1 via TCP/IPC.
- **Diagnostic Logging**: Verbose logging in NinjaScript Output for account and symbol troubleshooting.

### Fixed
- **Thread Safety**: Fixed "Collection modified" errors during order/position iterations by using `.ToArray()`.
- **Command Latency**: Improved command queue processing to handle all pending signals in a single bar update.
- **Port Conflict**: Resolved WPF Hub Server conflict by converting WPF app to Client-only mode.

---

## [8.28] - 2026-01-26 - "Flatten Race Condition & Cross-Instrument Fix"

### Fixed
- **V8.27 Fix**: Cross-Instrument Cancellation Bug. Added a filter in `ReconcileOrphanedOrders` to ensure strategy instances on one instrument (e.g., MGC) don't cancel orders on another instrument (e.g., MES).
- **V8.28 Fix**: Flatten Race Condition. Updated `FlattenAll` to use NinjaTrader's live `Position.Quantity` instead of a stale cached value. This prevents overselling when a target fills exactly as the flatten command is sent.

### Production Status
- ✅ **APPROVED FOR LIVE TRADING**

---

## [8.27] - 2026-01-26 - CRITICAL "Cross-Instrument Cancel Fix"

### Fixed
- **CRITICAL: Cross-Instrument Order Cancellation**: `ReconcileOrphanedOrders` was cancelling orders from OTHER instruments when running multiple strategy instances
  - Root cause: When MGC position closed, the function scanned ALL `Account.Orders` and cancelled MES orders because they weren't in MGC's `activePositions` dictionary
  - Solution: Added instrument filter at line 3807: `if (order.Instrument.FullName != Instrument.FullName) continue;`
  - This now only processes orphaned orders for the CURRENT instrument, preventing cross-contamination

### Test Results
- ✅ MES and MGC trades running concurrently with full brackets (Stop + T1 + T2 + T3)
- ✅ MGC position closed without affecting MES orders
- ✅ Trailing stops working correctly on both instruments
- ✅ No cross-instrument cancellation detected

### Production Status
- ✅ **APPROVED FOR LIVE TRADING**
- Critical bug fixed that was causing random order cancellations

---

## [8.20] - 2026-01-22 - MILESTONE "FINAL CLEAN"

### Added
- **UI Final Clean Branding**: High-visibility `★ V8.20 - FINAL CLEAN ★` header.
- **Absolute Profit Targets**: All targets (T1, T2, T3) and runner actions now calculate offsets from **Entry Price** instead of market price.
- **System.Linq Integration**: Added support for advanced collection handling to prevent crashes.

### Fixed
- **CRITICAL: Collection Modified Crash**: Fixed using `.ToList()` on dictionary keys during stop updates.
- **UI Clutter**: Removed "Show OR Label" property and all background price labels on the chart.
- **Compilation Error**: Fixed static constructor name mismatch and missing Linq import.

### Changed
- **Default Max Stop**: Enforced 8.0 point cap for safety.
- **Deployment Path**: Standardized updates to NT8's `bin/Custom/Strategies` directory.

---

## [Skills-Standardization] - 2026-01-20

### Added
- **Wearable Skill Standard**: Migration to `${PROJECT_ROOT}` universal pathing in all 36 skills.
- **Gemini Flash Integration**: Established `${HANDS}` executor role using `delegation_bridge` for routine file I/O.
- **Opus Critical Protocol**: Formally separated logic (Opus) from execution (Flash) for efficiency.
- **Related Skills Catalog**: Interconnected documentation structure across all utility and trading skills.
- **New Skills**:
  - `delegation-bridge`: Central MCP delegation orchestration.
  - `multi-ide-router`: Optimized model & environment selection.
  - `opus-critical`: Mission-critical vs routine work boundary.
  - `opus-deployment-guide`: Deployment options for Antigravity IDE.

### Changed
- Standardized headers and footers across the entire skill library.
- Refined `skill-creator` to enforce portability and delegation standards.
- Updated `AGENT.md` with new multi-model delegation architecture.

---

## [8.2] - 2026-01-18 - MILESTONE "TREND Strategy"

### Added
- **TREND Strategy**: Dual EMA entry system with scaled positions
  - Entry 1 (1/3 position) at 9 EMA limit order
  - Entry 2 (2/3 position) at 15 EMA limit order
  - Direction auto-detected: EMA below price = LONG, above = SHORT
- **TREND Stop Logic**:
  - E1: Fixed 2pt stop → switches to EMA9 trail when price crosses in favor
  - E2: Trails at EMA15 - (1.1 × ATR) from entry
- **TREND Multi-Target System**: Same as RMA (T1=1pt, T2=0.5xATR, T3=1xATR, T4=Runner)
- **TREND Button**: Teal button in UI panel to trigger TREND entry
- **Deferred Entry Execution**: TREND entries queue for OnBarUpdate to ensure correct EMA reads

### Fixed
- **BarsInProgress Context Bug**: Button clicks fired in BarsInProgress=1 (ATR series), causing wrong EMA values
  - Solution: Deferred entry to OnBarUpdate when BarsInProgress=0
- **EMA Value Accuracy**: Using EMA[1] (previous bar close) to match chart's "On bar close" indicators
- **Stop Fill Cleanup**: Added fallback name-based matching for trailed stops with DateTime.Ticks suffix
  - Extracts entry name from stop order name for reliable cleanup

### Test Results
- ✅ Dual EMA entries at correct prices (matching chart indicators)
- ✅ 4-target brackets (T1/T2/T3/Runner) for both E1 and E2
- ✅ E1 fixed stop → EMA9 trail transition
- ✅ E2 EMA15 trailing stop
- ✅ Phase 2: Remote App UI Cleanup & V8 Logic Port (V10.3) - COMPLETE
- ✅ Phase 3: Multi-Symbol Scalability & Advanced Monitoring

### Files
- `UniversalORStrategyV8_2.cs` - New TREND strategy implementation
- See `MILESTONE_V8_2_SUMMARY.md` for detailed documentation

---

## [8.1] - 2026-01-16 - MILESTONE "Full Order Synchronization"

### Added
- **Entry Price Sync**: Drag master's pending entry → slave updates to match
  - Works for both RMA (Limit) and OR (StopMarket) entries
  - Tracks order type via new `IsRMA` field in SlavePosition
- **Cancellation Sync**: Cancel master's pending entry → slave cancels
  - New `OrderCancelSignal` and `BroadcastOrderCancel()` in SignalBroadcaster
  - Master detects cancellation in `OnOrderUpdate`
- **Entry Update Signal**: New `EntryUpdateSignal` class for price sync
  - `BroadcastEntryUpdate()` method broadcasts price changes
  - Slave's `OnEntryUpdateHandler` cancels/resubmits at new price
- **Order Type Tracking**: `IsRMA` field ensures correct order type on sync

### Fixed
- **Flatten with Pending Orders**: Flatten now cancels pending entries, not just filled positions
  - Checks multiple order states: Working, Accepted, Submitted
- **Order Type Mismatch**: OR entries now correctly resubmit as StopMarket (not Limit)

### Changed
- `GetSubscriberCounts()` now shows Entry and Cancel subscribers
- `ClearAllSubscribers()` clears all new events

### Test Results
- ✅ Entry price sync (dragged 4611→4618.70→4624.30)
- ✅ Entry cancellation sync
- ✅ Flatten with pending OR entry
- ✅ Correct order types for RMA (Limit) and OR (StopMarket)

### Files Modified
- `SignalBroadcaster.cs` - Added EntryUpdateSignal, OrderCancelSignal
- `UniversalORStrategyV8.cs` - Entry change and cancel detection
- `UniversalORSlaveV8.cs` - OnEntryUpdateHandler, OnOrderCancelHandler, IsRMA field

---


## [5.13] - 2026-01-16 - MILESTONE "4-Target System + Frequency-Based Trailing"

### Added
- **4-Target System**: Expanded from 3 to 4 independent profit targets
  - T1: 20% of position (1-point quick profit)
  - T2: 30% of position (0.5x OR range)
  - T3: 30% of position (1.0x OR range)
  - T4 (Runner): 20% of position (trailing stop only)
- **Frequency-Based Trailing Stops**: Smart throttling to reduce order modifications
  - BE level (0-2.99 pts): Check EVERY tick (tight protection)
  - T1 level (3-3.99 pts): Check every OTHER tick (reduced rate)
  - T2 level (4-4.99 pts): Check every OTHER tick (reduced rate)
  - T3 level (5+ pts): Check EVERY tick (lock big profits)
  - New `TicksSinceEntry` counter in PositionInfo class
  - ~50% reduction in order modifications during volatile markets
- **T3 Contract Percentage Property**: New `T3ContractPercent` (default: 30%)
- **T3/T4 Order Tracking**: Added `target3Orders` and `target4Orders` dictionaries
- **Target Movement Validation**: Prevents moving targets to loss side of entry
  - LONG targets must stay ABOVE entry price
  - SHORT targets must stay BELOW entry price

### Fixed
- **CRITICAL: T3/T4 Cleanup Bug**: Orders were submitted but never removed from dictionaries
  - Added `target3Orders.Remove()` in `CleanupPosition()`
  - Added `target4Orders.Remove()` in `CleanupPosition()`
  - Added cleanup in `OnPositionUpdate()` when position goes flat
  - Prevents memory leaks and order conflicts
- **CRITICAL: Target Validation**: `ExecuteTargetAction()` was placing targets on wrong side
  - Added direction-aware validation before moving targets
  - Prevents accidentally creating guaranteed losses

### Changed
- **OR Stop Calculation**: Changed from `0.5x OR range` to `0.5x ATR`
  - More consistent across different market conditions
  - Aligns with RMA stop calculation methodology
- **OR Entry Detection**: Changed from `Close[0]` to `lastKnownPrice`
  - Real-time tick-level price instead of stale bar close
  - Improves breakout timing and reduces late entries
- **Version Banner**: Updated print statements from v5.8 to v5.13

### Test Results
- ✅ 4-target allocation working (T1:20% T2:30% T3:30% T4:20%)
- ✅ T3/T4 cleanup validated (no stranded orders)
- ✅ OR stop using 0.5x ATR (~3.2-3.4 pts for ATR ~6.4)
- ✅ Breakeven arming and auto-trigger working
- ✅ Stop quantity updates after T1 fills
- ✅ Entry blocking for stale breakouts
- ✅ RMA click entries (above=SHORT, below=LONG)

### Known Issues
- UI version string still shows "v5.12" (cosmetic only)
- T3 gets 0 contracts for small positions (3 contracts total) - expected behavior

### Production Status
- ✅ **READY FOR LIVE TRADING**
- All critical bugs fixed
- Cleanup logic validated
- Entry detection improved
- Stop calculation more consistent

### AI Context Transfer: Universal OR Strategy (v10.2 - Hybrid Dispatcher)

## 📡 PROJECT STATE: STABLE - v10.2 RELEASED
### Files
- `UniversalORStrategyV5_v5_13.cs` - Main strategy file
- See `MILESTONE_V5_13_SUMMARY.md` for detailed implementation

---

## [7.0] - 2026-01-13 - MILESTONE "Copy Trading Edition"

### Added
- **V7 Master Strategy**: V5 + copy trading broadcasting
  - All V5.12 features intact (OR, RMA, targets, trailing, etc.)
  - New `EnableCopyTrading` property to toggle broadcasting on/off
  - Broadcasts entry signals (OR and RMA)
  - Broadcasts breakeven commands
  - Broadcasts flatten commands
  - Print messages show copy trading status and slave counts
- **V7 Slave Strategy**: Ultra-lightweight trade copier
  - ~330 lines (vs V5's ~3000 lines)
  - NO UI, NO indicators, NO OR calculations
  - Runs headless from Strategies tab (no chart needed)
  - Calculates own position size based on risk settings
  - Manages own brackets (stop, T1, T2)
  - Event-based signal reception
- **Signal Broadcasting**: Enhanced SignalBroadcaster integration
  - `BroadcastEntrySignal()` helper method in Master
  - Broadcasts after entry order submission
  - Broadcasts at breakeven button click
  - Broadcasts at flatten command
  - Includes full trade details (entry, stop, targets, signal ID)

### Changed
- Master startup prints show "V7 COPY TRADING: ENABLED/Disabled" status
- Master broadcasts only when `EnableCopyTrading = true`
- Slave subscribes to events in State.Realtime
- Slave unsubscribes in State.Terminated

### Technical
- Event-based architecture using SignalBroadcaster events
- Master: `OnTradeSignal`, `OnFlattenAll`, `OnBreakevenRequest` events
- Slave: EventHandler<T> pattern for all signal handlers
- Minimal Master changes (~50 lines added to V5)
- Slave uses simplified position tracking (no complex dictionaries)

### Test Results
- Entry copying WORKING ✅ (RMA Short tested)
- Master → Slave signal transmission VERIFIED ✅
- Identical orders submitted (same price, signal ID) ✅
- Slave position sizing working (risk-based calculation) ✅
- Subscriber counts showing correctly ✅
- Breakeven command: Implemented, not yet tested
- Flatten command: Implemented, not yet tested
- Multiple slaves: Not yet tested

### RAM Comparison
- V5 on 20 charts: ~1-1.6 GB
- V7: 1 Master + 19 slaves: ~400-600 MB
- **Savings: ~60-70% RAM reduction**

### Files
- `UniversalORStrategyV7.cs` - Master (V5 + broadcasting)
- `UniversalORSlaveV7.cs` - Ultra-light slave
- `SignalBroadcaster.cs` - Shared (unchanged from V6)
- See `MILESTONE_V7_0_SUMMARY.md` for complete documentation

### Production Status
- ✅ **INITIAL TESTING SUCCESSFUL**
- Ready for extended testing on simulation accounts
- Continue testing: Breakeven, Flatten, Multiple slaves, Full sessions

---

## [6.0] - 2026-01-13 - FAILED / ARCHIVED

### Status
- ❌ **ARCHITECTURE FAILED - DO NOT USE**
- Archived to `archived-versions/` with `_FAILED` suffix

### Why It Failed
- Master-only design didn't match user workflow
- User wanted to trade V5 normally, just copy to other accounts
- V6 tried to create broadcast-only Master (no local trading)
- Created complexity without solving actual problem
- V7 replaced it with simpler approach: V5 + optional broadcasting

### Lessons Learned
- Don't reinvent working systems (V5 was proven)
- Match architecture to actual user workflow
- "Trade once, copy everywhere" means: trade normally + broadcast
- Not: "broadcast only, no local trading"

### Files Archived
- `UniversalORMasterV6_FAILED.cs`
- `UniversalORSlaveV6_FAILED.cs`
- `V6_CHANGELOG_FAILED.md`
- `V6_SETUP_GUIDE_FAILED.md`

---

## [5.12] - 2026-01-12 - MILESTONE "Target Management Dropdowns"

### Added
- **T1 Target Dropdown Menu**: 6 actions for T1 management
  - Fill at Market NOW - Close T1 portion immediately
  - Move to 1 Point - Adjust T1 to 1 point from current price
  - Move to 2 Points - Adjust T1 to 2 points from current price
  - Move to Market Price - Move T1 limit to current market (instant fill)
  - Move to Breakeven - Move T1 to entry price
  - Cancel T1 Order - Remove T1, let contracts run
- **T2 Target Dropdown Menu**: Same 6 actions as T1
- **Runner Management Dropdown**: 6 actions for runner/stop management
  - Close Runner at Market - Exit runner portion immediately
  - Move Stop to 1 Point - Tighten stop to 1 point from current
  - Move Stop to 2 Points - Tighten stop to 2 points from current
  - Move Stop to Breakeven - Lock in T1+T2 profits
  - Lock 50% of Profit - Move stop halfway to current price
  - Disable Trailing Stop - [x] Port V8 UI and Breakout logic to V10.3
    - [x] Update V9 Remote App (Remove Auto, Add OR/Target buttons)
    - [x] Port `ExecuteLong`/`ExecuteShort` from V8 to V10
    - [x] Implement `FlattenSpecificTarget` IPC handlers
- **Hotkey System**: Combo keys for all actions
  - T1: Hold `1` + letter (M/O/W/K/B/C)
  - T2: Hold `2` + letter (M/O/W/K/B/C)
  - Runner: Hold `3` + letter (M/O/W/B/P/D)
- **Multi-Position Support**: All actions affect all active positions simultaneously

### Changed
- UI grid expanded to include 3 dropdown buttons and 3 dropdown panels
- `OnKeyDown()` enhanced with hotkey combo detection
- Added `ExecuteTargetAction()` method for T1/T2 management
- Added `ExecuteRunnerAction()` method for runner management
- Added `MoveTargetOrder()` helper for target order modification

### Technical
- Dropdown panels collapse/expand with auto-close of other dropdowns
- Menu buttons created dynamically via `CreateDropdownPanel()`
- Action handlers validate position state before executing
- UpdateStopOrder calls include proper parameters (pos, newStopPrice, newTrailLevel)

### Test Results
- Compiled successfully ✅
- UI created and displayed ✅
- Dropdown actions tested in live market ✅
  - T1 → 1 Point: Working (RMAShort test)
  - T2 → 2 Points: Working (RMAShort test)
  - T2 → 1 Point: Working (RMAShort test)
- All v5.11 features intact ✅

### Files
- See `MILESTONE_V5_12_SUMMARY.md` for detailed implementation and use cases

---

## [5.11] - 2026-01-12 - MILESTONE "Breakeven Toggle"

### Added
- **Breakeven Toggle Functionality**: Click to arm/disarm breakeven before trigger
  - Click once → Armed (orange)
  - Click again → Disarmed (gray)
  - Toggle unlimited times before trigger
  - After trigger → Locked (green, cannot toggle)
- **Protection Lock**: Prevents accidentally disarming active breakeven protection
  - Message: "BREAKEVEN: Already triggered - cannot toggle"
  - Lock engages when stop moves to breakeven + buffer

### Changed
- `OnBreakevenButtonClick()` now toggles armed state instead of just setting it
- Added state checking logic to determine arm/disarm action
- Added trigger checking logic to prevent toggle after activation

### Technical
- Checks `ManualBreakevenTriggered` flag to determine if lock should engage
- Checks `ManualBreakevenArmed` flag to determine toggle direction
- Provides clear print messages for each state change

### Test Results
- Toggle tested 5 times successfully (arm/disarm/arm/disarm/arm) ✅
- Auto-trigger working (stop moved at threshold) ✅
- Lock working (cannot toggle after trigger) ✅
- Trailing after breakeven working (T1 → T2 progression) ✅

### Files
- See `MILESTONE_V5_11_SUMMARY.md` for detailed implementation

---


## [5.10] - 2026-01-12 - MILESTONE "ATR Display & OR Label Toggle"

### Added
- **ATR Display in UI Panel**: Current ATR value now shows in OR info section
  - Displays in all states: Waiting, Building, Complete
  - Format: "H: X | L: Y | R: Z | ATR: A.BC"
  - Helps assess market volatility for RMA trade sizing
- **OR Label Toggle**: New `ShowORLabel` property to hide/show chart text
  - Located in "6. Display" settings group
  - Default: ON (shows label)
  - Allows clean chart while maintaining OR box visualization

### Changed
- `UpdateDisplayInternal()` now includes ATR value in OR info text
- `DrawORBox()` conditionally draws OR label based on `ShowORLabel` property

### Technical
- ATR value pulled from existing `currentATR` variable (already calculated for RMA)
- Conditional text formatting prevents display when ATR not yet calculated

### Test Results
- v5.10 compiled and loaded successfully ✅
- ATR displaying correctly in UI panel ✅
- OR label toggle working (hide/show) ✅
- All v5.9 features still working (manual breakeven tested) ✅

### Files
- See `MILESTONE_V5_10_SUMMARY.md` for detailed implementation

---


## [5.9] - 2026-01-12 - MILESTONE "Manual Breakeven Button"

### Added
- **Manual Breakeven Button**: Click to arm breakeven protection that auto-triggers when price reaches threshold
  - Button shows in UI between RMA and Flatten buttons
  - Visual states: Gray (inactive) → Orange (armed) → Green (triggered)
  - "Set and forget" approach - click early, triggers automatically
- **Configurable Buffer**: New `ManualBreakevenBuffer` property (default: 1 tick, range: 1-10)
  - Adjustable per instrument (MES vs MGC can use different buffers)
  - Located in "5. Trailing Stops" property group
- **State Tracking**: Two new fields in PositionInfo class
  - `ManualBreakevenArmed` - Button clicked, monitoring price
  - `ManualBreakevenTriggered` - Stop has been moved to breakeven

### Changed
- `ManageTrailingStops()` now checks manual breakeven BEFORE automatic trailing logic
- Button color updates in `UpdateDisplayInternal()` based on position states

### Technical
- Auto-trigger logic runs on every tick (Calculate.OnPriceChange)
- Works with both OR and RMA positions simultaneously
- Won't move stop until price actually reaches entry + buffer (prevents accidental stop-outs)

### Test Results
- MGC live test: Armed while red, auto-triggered at entry + 1 tick ✅
- Risk reduced from -11 ticks to -3 ticks (73% reduction) ✅
- Button state changes working correctly ✅

### Files
- See `MILESTONE_V5_9_SUMMARY.md` for detailed implementation and usage guide

---

## [5.8] - 2026-01-12 - MILESTONE "Stop Validation & Trailing Stops"

### Added
- **Stop Order Null Validation**: Checks if stop submission returns null, triggers emergency flatten
- **Stop Rejection Recovery**: Detects rejected stops and auto-resubmits
- **Emergency Flatten Function**: `FlattenPositionByName()` closes position at market if stop fails
- **Enhanced Logging**: Clear ⚠️ warnings for critical issues

### Validated
- ✅ Trailing stops working correctly (BE → T1 → T2 → T3)
- ✅ Applies to both OR and RMA trades
- ✅ Entry isolation from v5.7 still working

### Files
- See `MILESTONE_V5_8_SUMMARY.md` for test evidence

---

## [5.7_FINAL_FIX] - 2026-01-12 - MILESTONE "Entry Cancellation Bug Fixed"

### Fixed
- **CRITICAL BUG**: Opposite-side OR entry orders no longer cancelled when position closes
  - Problem: When ORLong closed, ORShort entry was incorrectly cancelled
  - Root cause: `OnPositionUpdate` was cancelling ALL pending entries when position went flat
  - Solution: Removed lines 1283-1296 that cancelled unrelated pending entries
- **String matching bug**: `CleanupPosition` now uses `.StartsWith()` instead of `.Contains()`
  - Prevents "ORLong" from matching "ORShort" during cleanup

### Changed
- **MinimumStop**: 4.0 → 1.0 points (allows tighter stops for small OR ranges)
- **RiskPerTrade**: $400 → $200 (reduced default risk)
- **ReducedRiskPerTrade**: $160 → $200 (simplified - now matches normal risk)

### Validated
- ✅ Opposite-side entries remain active after position closes
- ✅ Only related orders (stop, T1, T2) are cancelled for closed position
- ✅ No "Cancelled orphaned entry" messages for unrelated trades

### Test Results
- MGC test: ORShort filled and stopped out, ORLong entry remained active ✅
- MES test: Both OR entries active, independent position management ✅

### Files
- See `MILESTONE_V5_7_FINAL_FIX_SUMMARY.md` for detailed bug analysis and fix

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
