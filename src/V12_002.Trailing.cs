// V12.46 MODULAR: Trailing Stop Module (Extracted from Orders.cs)
// Contains: ManageTrailingStops, CleanupStalePendingReplacements, UpdateStopOrder,
//           CalculateStopForLevel, OnBreakevenButtonClick, MoveStopsToBreakevenWithOffset, MoveSpecificTarget
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.Core.FloatingPoint;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        #region Trailing Stops

        private void ManageTrailingStops()
        {
            DateTime now = DateTime.Now;

            // V8.30: Adaptive throttle calculation - adjusts based on tick frequency
            tickCountInLastSecond++;
            if ((now - lastTickCountReset).TotalSeconds >= 1)
            {
                // Adjust throttle based on tick frequency
                if (tickCountInLastSecond > 50)
                    adaptiveThrottleMs = Math.Min(500, adaptiveThrottleMs + 50); // Increase throttle under load
                else if (tickCountInLastSecond < 20)
                    adaptiveThrottleMs = Math.Max(100, adaptiveThrottleMs - 25); // Decrease throttle when calm

                tickCountInLastSecond = 0;
                lastTickCountReset = now;
            }

            // V8.30: Use adaptive throttle instead of fixed 100ms
            if ((now - lastStopManagementTime).TotalMilliseconds < adaptiveThrottleMs)
                return;

            lastStopManagementTime = now;

            // V8.30: Clean up stale pending replacements (5-second timeout)
            CleanupStalePendingReplacements();

            // V8.30: Circuit breaker check - pause trailing when too many pending replacements
            if (circuitBreakerActive)
            {
                if ((now - circuitBreakerActivatedTime).TotalSeconds > 2)
                {
                    circuitBreakerActive = false;
                    Print("V8.30: Circuit breaker RESET - trailing stops resumed");
                }
                else
                {
                    return; // Skip trailing stop updates while circuit breaker is active
                }
            }

            // V8.30: Thread-safe snapshot iteration - prevents "Collection was modified" exception
            var positionSnapshot = activePositions.ToArray();
            foreach (var kvp in positionSnapshot)
            {
                string entryName = kvp.Key;
                PositionInfo pos = kvp.Value;

                // V8.30: Verify position still exists (may have been removed by callback thread)
                if (!activePositions.ContainsKey(entryName)) continue;

                if (!pos.EntryFilled || !pos.BracketSubmitted) continue;
                if (pos.IsFollower && SymmetryGuardIsAnchorPending(entryName)) continue;

                // Increment tick counter on every call
                pos.TicksSinceEntry++;

                // Update extreme price
                if (pos.Direction == MarketPosition.Long)
                    pos.ExtremePriceSinceEntry = Math.Max(pos.ExtremePriceSinceEntry, Close[0]);
                else
                    pos.ExtremePriceSinceEntry = Math.Min(pos.ExtremePriceSinceEntry, Close[0]);

                // V8.2: TREND Entry 1 - starts with fixed 2pt stop, switches to EMA9 trail when price crosses EMA
                if (pos.IsTRENDTrade && pos.IsTRENDEntry1 && !pos.IsRMATrade)
                {
                    // V8.2: Use stored ema9 instance
                    double tickPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];
                    double ema9Live = ema9 != null ? ema9[0] : Close[0];
                    double currentPrice = tickPrice;
                    
                    // Check if price has crossed EMA9 in our favor
                    bool priceInFavor = pos.Direction == MarketPosition.Long
                        ? currentPrice > ema9Live  // LONG: price above EMA9
                        : currentPrice < ema9Live; // SHORT: price below EMA9

                    // If not yet trailing and price crossed EMA in our favor, activate trailing
                    if (!pos.Entry1TrailActivated && priceInFavor)
                    {
                        pos.Entry1TrailActivated = true;
                        Print(string.Format("TREND E1: Switching to EMA9 trail (Price={0:F2} crossed EMA9={1:F2})",
                            currentPrice, ema9Live));
                    }

                    // If trailing is activated, manage the EMA9 trail
                    if (pos.Entry1TrailActivated)
                    {
                        double trendStop = pos.Direction == MarketPosition.Long
                            ? ema9Live - (currentATR * TRENDEntry1ATRMultiplier)  // V8.31: Uses E1 specific multiplier
                            : ema9Live + (currentATR * TRENDEntry1ATRMultiplier);

                        bool shouldUpdate = pos.Direction == MarketPosition.Long
                            ? trendStop > pos.CurrentStopPrice
                            : trendStop < pos.CurrentStopPrice;

                        if (shouldUpdate)
                        {
                            UpdateStopOrder(entryName, pos, trendStop, pos.CurrentTrailLevel);
                            // Print(string.Format("TREND E1 TRAIL: Stop moved to {0:F2} (EMA9={1:F2} - {2}xATR)",
                            //    trendStop, ema9Live, TRENDEntry2ATRMultiplier));
                        }
                    }
                    continue; // Skip normal trailing logic for TREND E1
                }

                // V8.2: TREND Entry 2 uses EMA15 trailing stop (1.1x ATR from live EMA15)
                if (pos.IsTRENDTrade && pos.IsTRENDEntry2 && !pos.IsRMATrade)
                {
                    // V8.2: Use stored ema15 instance
                    double ema15Live = ema15 != null ? ema15[0] : Close[0];
                    
                    double trendStop = pos.Direction == MarketPosition.Long
                        ? ema15Live - (currentATR * TRENDEntry2ATRMultiplier)
                        : ema15Live + (currentATR * TRENDEntry2ATRMultiplier);

                    bool shouldUpdate = pos.Direction == MarketPosition.Long
                        ? trendStop > pos.CurrentStopPrice
                        : trendStop < pos.CurrentStopPrice;

                    if (shouldUpdate)
                    {
                        UpdateStopOrder(entryName, pos, trendStop, pos.CurrentTrailLevel);
                        Print(string.Format("TREND E2 TRAIL: Stop moved to {0:F2} (EMA15={1:F2} - {2}xATR)", 
                            trendStop, ema15Live, TRENDEntry2ATRMultiplier));
                    }
                    continue; // Skip normal trailing logic for TREND E2
                }

                // V8.4: RETEST trade - Phase 1: Wait for price to cross 9 EMA, Phase 2: Trail at 9 EMA
                if (pos.IsRetestTrade && !pos.IsRMATrade)
                {
                    double tickPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];
                    double ema9Live = ema9 != null ? ema9[0] : Close[0];
                    double currentPrice = tickPrice;

                    // Phase 1: Wait for price to cross EMA9 in our favor
                    if (!pos.RetestTrailActivated)
                    {
                        bool priceInFavor = pos.Direction == MarketPosition.Long
                            ? currentPrice > ema9Live  // LONG: price above EMA9
                            : currentPrice < ema9Live; // SHORT: price below EMA9

                        if (priceInFavor)
                        {
                            pos.RetestTrailActivated = true;
                            Print(string.Format("RETEST: Switching to EMA9 trail (Price={0:F2} crossed EMA9={1:F2})",
                                currentPrice, ema9Live));
                        }
                        // Stay at fixed stop until price crosses EMA
                        continue;
                    }

                    // Phase 2: Trail at 9 EMA - 1.1x ATR (locked in, only moves favorably)
                    double retestStop = pos.Direction == MarketPosition.Long
                        ? ema9Live - (currentATR * RetestATRMultiplier)
                        : ema9Live + (currentATR * RetestATRMultiplier);

                    // Only update if better than current stop
                    bool shouldUpdate = pos.Direction == MarketPosition.Long
                        ? retestStop > pos.CurrentStopPrice
                        : retestStop < pos.CurrentStopPrice;

                    if (shouldUpdate)
                    {
                        UpdateStopOrder(entryName, pos, retestStop, pos.CurrentTrailLevel);
                        Print(string.Format("RETEST TRAIL: Stop moved to {0:F2} (EMA9={1:F2} - {2}xATR)",
                            retestStop, ema9Live, RetestATRMultiplier));
                    }
                    continue; // Skip normal trailing logic for RETEST
                }

                double profitPoints = pos.Direction == MarketPosition.Long
                    ? pos.ExtremePriceSinceEntry - pos.EntryPrice
                    : pos.EntryPrice - pos.ExtremePriceSinceEntry;

                double newStopPrice = pos.CurrentStopPrice;
                int newTrailLevel = pos.CurrentTrailLevel;

                // Standard TREND/RETEST are EMA-only; point-based BE/T1/T2/T3 is RMA-only for these trade types.
                bool isTrendOrRetestTrade = pos.IsTRENDTrade || pos.IsRetestTrade;
                bool allowPointBasedTrailing = !isTrendOrRetestTrade || pos.IsRMATrade;
                if (!allowPointBasedTrailing)
                    continue;

                // MANUAL BREAKEVEN - Check FIRST before automatic trailing
                // This allows user to "arm" breakeven early and it auto-triggers when price reaches threshold
                if (pos.ManualBreakevenArmed && !pos.ManualBreakevenTriggered)
                {
                    double beThreshold = pos.EntryPrice + (BreakEvenOffsetTicks * tickSize);
                    bool thresholdReached = false;

                    if (pos.Direction == MarketPosition.Long)
                    {
                        thresholdReached = Close[0] >= beThreshold;
                    }
                    else // Short
                    {
                        beThreshold = pos.EntryPrice - (BreakEvenOffsetTicks * tickSize);
                        thresholdReached = Close[0] <= beThreshold;
                    }

                    if (thresholdReached)
                    {
                        // Move stop to breakeven + buffer
                        double manualBEStop = pos.Direction == MarketPosition.Long
                            ? pos.EntryPrice + (BreakEvenOffsetTicks * tickSize)
                            : pos.EntryPrice - (BreakEvenOffsetTicks * tickSize);

                        // Only move if it's better than current stop
                        bool shouldMove = pos.Direction == MarketPosition.Long
                            ? manualBEStop > pos.CurrentStopPrice
                            : manualBEStop < pos.CurrentStopPrice;

                        if (shouldMove)
                        {
                            newStopPrice = manualBEStop;
                            newTrailLevel = 1; // Same as automatic breakeven
                            pos.ManualBreakevenTriggered = true;
                            Print(string.Format("? MANUAL BREAKEVEN TRIGGERED: {0} -> Stop moved to {1:F2} (Entry + {2} tick)", 
                                entryName, manualBEStop, BreakEvenOffsetTicks));
                        }
                    }
                }

                // v5.13 FREQUENCY CONTROL: Determine if we should check trailing based on current level
                // BE (level 0-1) and T3 (level 4) = every tick
                // T1 (level 2) and T2 (level 3) = every OTHER tick
                
                bool shouldCheckTrailing = true; // Default: check every tick
                
                // Determine current active level based on profit
                if (profitPoints >= Trail3TriggerPoints && pos.T1Filled && pos.T2Filled)
                {
                    // At T3 level (5+ points) - Check EVERY tick
                    shouldCheckTrailing = true;
                }
                else if (profitPoints >= Trail2TriggerPoints && pos.T1Filled)
                {
                    // At T2 level (4-4.99 points) - Check every OTHER tick
                    shouldCheckTrailing = (pos.TicksSinceEntry % 2 == 0);
                }
                else if (profitPoints >= Trail1TriggerPoints)
                {
                    // At T1 level (3-3.99 points) - Check every OTHER tick
                    shouldCheckTrailing = (pos.TicksSinceEntry % 2 == 0);
                }
                else
                {
                    // At BE level or below (0-2.99 points) - Check EVERY tick
                    shouldCheckTrailing = true;
                }

                // Only proceed with trailing logic if frequency check passes
                if (!shouldCheckTrailing)
                    continue;

                // Trail 3 (highest priority) - At 5 points, trail by 1 point
                // V8.22: Strictly profit based (no target dependencies)
                if (profitPoints >= Trail3TriggerPoints)
                {
                    double trail3Stop = pos.Direction == MarketPosition.Long
                        ? pos.ExtremePriceSinceEntry - Trail3DistancePoints
                        : pos.ExtremePriceSinceEntry + Trail3DistancePoints;

                    if (pos.Direction == MarketPosition.Long && trail3Stop > pos.CurrentStopPrice)
                    {
                        newStopPrice = trail3Stop;
                        newTrailLevel = 4; // Level 4 = Trail 3
                    }
                    else if (pos.Direction == MarketPosition.Short && trail3Stop < pos.CurrentStopPrice)
                    {
                        newStopPrice = trail3Stop;
                        newTrailLevel = 4;
                    }
                }
                // Trail 2 - At 4 points, trail by 1.5 points
                else if (profitPoints >= Trail2TriggerPoints && pos.CurrentTrailLevel < 3)
                {
                    double trail2Stop = pos.Direction == MarketPosition.Long
                        ? pos.ExtremePriceSinceEntry - Trail2DistancePoints
                        : pos.ExtremePriceSinceEntry + Trail2DistancePoints;

                    if (pos.Direction == MarketPosition.Long && trail2Stop > pos.CurrentStopPrice)
                    {
                        newStopPrice = trail2Stop;
                        newTrailLevel = 3; // Level 3 = Trail 2
                    }
                    else if (pos.Direction == MarketPosition.Short && trail2Stop < pos.CurrentStopPrice)
                    {
                        newStopPrice = trail2Stop;
                        newTrailLevel = 3;
                    }
                }
                // Trail 1 - At 3 points, trail by 2 points
                else if (profitPoints >= Trail1TriggerPoints && pos.CurrentTrailLevel < 2)
                {
                    double trail1Stop = pos.Direction == MarketPosition.Long
                        ? pos.ExtremePriceSinceEntry - Trail1DistancePoints
                        : pos.ExtremePriceSinceEntry + Trail1DistancePoints;

                    if (pos.Direction == MarketPosition.Long && trail1Stop > pos.CurrentStopPrice)
                    {
                        newStopPrice = trail1Stop;
                        newTrailLevel = 2; // Level 2 = Trail 1
                    }
                    else if (pos.Direction == MarketPosition.Short && trail1Stop < pos.CurrentStopPrice)
                    {
                        newStopPrice = trail1Stop;
                        newTrailLevel = 2;
                    }
                }
                // Break-even - At 2 points, move to BE +1 tick
                else if (profitPoints >= BreakEvenTriggerPoints && pos.CurrentTrailLevel < 1)
                {
                    double beStop = pos.Direction == MarketPosition.Long
                        ? pos.EntryPrice + (BreakEvenOffsetTicks * tickSize)
                        : pos.EntryPrice - (BreakEvenOffsetTicks * tickSize);

                    if (pos.Direction == MarketPosition.Long && beStop > pos.CurrentStopPrice)
                    {
                        newStopPrice = beStop;
                        newTrailLevel = 1;
                        // [Build 1102J] Prevent the ManualBreakevenArmed path from re-firing redundantly.
                        pos.ManualBreakevenTriggered = true;
                    }
                    else if (pos.Direction == MarketPosition.Short && beStop < pos.CurrentStopPrice)
                    {
                        newStopPrice = beStop;
                        newTrailLevel = 1;
                        // [Build 1102J] Prevent the ManualBreakevenArmed path from re-firing redundantly.
                        pos.ManualBreakevenTriggered = true;
                    }
                }

                // V8.21: Check if stop price actually changed by more than 1 tick before updating
                // This prevents redundant "micro-updates" that saturate the order system
                if (Math.Abs(newStopPrice - pos.CurrentStopPrice) < tickSize * 0.9)
                    continue;

                // Update stop if needed
                if (newStopPrice != pos.CurrentStopPrice)
                {
                    UpdateStopOrder(entryName, pos, newStopPrice, newTrailLevel);
                }
            }

            // V12.10: FLEET SYMMETRY SYNC PASS
            // When SIMA is enabled, force followers to match the Leader's trail level.
            // Followers calculate stops relative to their OWN entry prices but are triggered
            // by the Leader's profit progress. This prevents slippage-induced desync.
            if (EnableSIMA)
            {
                // Phase 1: Find the highest trail level among leader positions, by direction
                int leaderLongMaxLevel = 0;
                int leaderShortMaxLevel = 0;

                foreach (var kvp in positionSnapshot)
                {
                    PositionInfo ldr = kvp.Value;
                    if (ldr.IsFollower || !ldr.EntryFilled || !ldr.BracketSubmitted) continue;

                    if (ldr.Direction == MarketPosition.Long)
                        leaderLongMaxLevel = Math.Max(leaderLongMaxLevel, ldr.CurrentTrailLevel);
                    else if (ldr.Direction == MarketPosition.Short)
                        leaderShortMaxLevel = Math.Max(leaderShortMaxLevel, ldr.CurrentTrailLevel);
                }

                // V12.12: Diagnostic -- log leader trail levels for fleet sync visibility
                if (leaderLongMaxLevel > 0 || leaderShortMaxLevel > 0)
                    Print($"[SIMA] Fleet Sync: Leader trail levels -- Long={leaderLongMaxLevel}, Short={leaderShortMaxLevel}");

                // Phase 2: Sync lagging followers UP to the leader's level
                if (leaderLongMaxLevel > 0 || leaderShortMaxLevel > 0)
                {
                    foreach (var kvp in positionSnapshot)
                    {
                        string entryName2 = kvp.Key;
                        PositionInfo fol = kvp.Value;

                        if (!fol.IsFollower) continue;
                        if (!fol.EntryFilled || !fol.BracketSubmitted) continue;
                        if (!activePositions.ContainsKey(entryName2)) continue;

                        int targetLevel = (fol.Direction == MarketPosition.Long)
                            ? leaderLongMaxLevel
                            : leaderShortMaxLevel;

                        // V12.12: Guard -- skip if no leader exists for this direction (targetLevel==0)
                        if (targetLevel == 0) continue;

                        // Only sync UP -- never regress a follower already at a higher level
                        if (fol.CurrentTrailLevel >= targetLevel) continue;

                        double syncStopPrice = CalculateStopForLevel(fol, targetLevel);

                        // Only move if it's a more protective stop
                        bool isBetter = (fol.Direction == MarketPosition.Long)
                            ? syncStopPrice > fol.CurrentStopPrice
                            : syncStopPrice < fol.CurrentStopPrice;

                        if (isBetter)
                        {
                            UpdateStopOrder(entryName2, fol, syncStopPrice, targetLevel);
                            Print(string.Format("FLEET SYNC: {0} synced to Level {1} -> Stop {2:F2} (Leader advanced)",
                                entryName2, targetLevel, syncStopPrice));
                        }
                    }
                }
            }
        }

        // V8.30: Clean up stale pending replacements that are older than 5 seconds
        // Prevents memory leak and ensures positions remain protected
        private void CleanupStalePendingReplacements()
        {
            DateTime now = DateTime.Now;

            // V8.30: Safe iteration with snapshot
            foreach (var kvp in pendingStopReplacements.ToArray())
            {
                if ((now - kvp.Value.CreatedTime).TotalSeconds > 5)
                {
                    if (pendingStopReplacements.TryRemove(kvp.Key, out var pending))
                    {
                        Interlocked.Decrement(ref pendingReplacementCount);
                        Print(string.Format("V8.30: Stale pending replacement REMOVED for {0} (>5sec old)", kvp.Key));

                        // If position still exists and needs protection, create emergency stop
                        if (activePositions.TryGetValue(kvp.Key, out var pos) && pos.EntryFilled && pos.RemainingContracts > 0)
                        {
                            Print(string.Format("V8.30: Creating EMERGENCY replacement stop for {0}", kvp.Key));
                            // V12.1101E [F-02]: Use live RemainingContracts under stateLock instead of stale pending.Quantity
                            int replacementQty;
                            lock (stateLock)
                            {
                                replacementQty = pos.RemainingContracts;
                            }
                            CreateNewStopOrder(kvp.Key, replacementQty, pending.StopPrice, pending.Direction);
                            // Build 950: Also restore bracket targets after V8.30 emergency stop.
                            if (pending.BracketRestorationNeeded && pending.CapturedTargets != null)
                            {
                                TargetSnapshot[] _tSnap = pending.CapturedTargets;
                                string _tKey = kvp.Key;
                                TriggerCustomEvent(o => RestoreCascadedTargets(_tKey, _tSnap), null);
                            }
                        }
                    }
                }
            }
        }

        // V12.44: ChangeStop() removed -- dead code, only caller was MoveStopsToBreakevenPlusOne (also removed)

        private void UpdateStopOrder(string entryName, PositionInfo pos, double newStopPrice, int newTrailLevel)
        {
            // V8.30: Thread-safe check using TryGetValue
            if (!stopOrders.TryGetValue(entryName, out var currentStop)) return;

            Order newStop = null;

            try
            {
                double validatedStopPrice = ValidateStopPrice(pos.Direction, newStopPrice, newTrailLevel, pos.EntryPrice);

                // V8.30: Thread-safe update using TryGetValue to avoid TOCTOU race
                if (pendingStopReplacements.TryGetValue(entryName, out var existingPending))
                {
                    // Update the pending replacement atomically (pending is a reference type)
                    existingPending.StopPrice = validatedStopPrice;
                    existingPending.Quantity = pos.RemainingContracts;
                    pos.CurrentStopPrice = validatedStopPrice;
                    pos.CurrentTrailLevel = newTrailLevel;
                    return;
                }

                // V8.11 FIX: Store pending replacement BEFORE cancelling
                // V8.12 FIX: Also handle CancelPending and PendingSubmit states to prevent race condition
                // V8.30: Added CreatedTime for timeout support and circuit breaker tracking
                if (currentStop != null && (currentStop.OrderState == OrderState.CancelPending || currentStop.OrderState == OrderState.Submitted))
                {
                    // Order is already being cancelled or submitted - queue the new stop price
                    var newPending = new PendingStopReplacement
                    {
                        EntryName = entryName,
                        Quantity = pos.RemainingContracts,
                        StopPrice = validatedStopPrice,
                        Direction = pos.Direction,
                        OldOrder = currentStop,
                        CreatedTime = DateTime.Now  // V8.30: Timeout support
                    };

                    // V8.30: Thread-safe add or update
                    PendingStopReplacement _addedRecord = null;
                    if (pendingStopReplacements.TryAdd(entryName, newPending))
                    {
                        // V8.30: Track count for circuit breaker
                        int currentCount = Interlocked.Increment(ref pendingReplacementCount);
                        if (currentCount >= CIRCUIT_BREAKER_THRESHOLD && !circuitBreakerActive)
                        {
                            circuitBreakerActive = true;
                            circuitBreakerActivatedTime = DateTime.Now;
                            Print(string.Format("V8.30: CIRCUIT BREAKER ACTIVATED - {0} pending replacements (threshold: {1})",
                                currentCount, CIRCUIT_BREAKER_THRESHOLD));
                        }
                        _addedRecord = newPending;
                    }
                    else if (pendingStopReplacements.TryGetValue(entryName, out var pending))
                    {
                        // Just update the pending price and size
                        pending.StopPrice = validatedStopPrice;
                        pending.Quantity = pos.RemainingContracts; // Build 952.4: keep qty current mid-cancel
                        // Build 950: Refresh CapturedTargets on the live pending record if not yet populated.
                        if (!pending.BracketRestorationNeeded)
                        {
                            var _b950Refresh = new System.Collections.Generic.List<TargetSnapshot>();
                            for (int _t2 = 1; _t2 <= 5; _t2++)
                            {
                                var _tD2 = GetTargetOrdersDictionary(_t2);
                                Order _tO2;
                                if (_tD2 != null && _tD2.TryGetValue(entryName, out _tO2) && _tO2 != null
                                    && (_tO2.OrderState == OrderState.Working || _tO2.OrderState == OrderState.Accepted))
                                    _b950Refresh.Add(new TargetSnapshot { TargetNum = _t2, Price = _tO2.LimitPrice, Qty = _tO2.Quantity, CapturedOrder = _tO2 });
                            }
                            pending.CapturedTargets = _b950Refresh.Count > 0 ? _b950Refresh.ToArray() : null;
                            pending.BracketRestorationNeeded = _b950Refresh.Count > 0;
                        }
                        _addedRecord = pending;
                    }

                    // Build 950: Snapshot Working/Accepted targets before cancel for OCO cascade restoration.
                    // Build 952.4: Write to _addedRecord (live record) so TryAdd-failed path is not lost.
                    if (_addedRecord != null)
                    {
                        var _b950Targets = new System.Collections.Generic.List<TargetSnapshot>();
                        for (int _t = 1; _t <= 5; _t++)
                        {
                            var _tD = GetTargetOrdersDictionary(_t);
                            Order _tO;
                            if (_tD != null && _tD.TryGetValue(entryName, out _tO) && _tO != null
                                && (_tO.OrderState == OrderState.Working || _tO.OrderState == OrderState.Accepted))
                                _b950Targets.Add(new TargetSnapshot { TargetNum = _t, Price = _tO.LimitPrice, Qty = _tO.Quantity, CapturedOrder = _tO });
                        }
                        _addedRecord.CapturedTargets = _b950Targets.Count > 0 ? _b950Targets.ToArray() : null;
                        _addedRecord.BracketRestorationNeeded = _b950Targets.Count > 0;
                    }

                    pos.CurrentStopPrice = validatedStopPrice;
                    pos.CurrentTrailLevel = newTrailLevel;
                    Print(string.Format("V8.12: Stop update queued for {0} (current state: {1})", entryName, currentStop.OrderState));
                    return;
                }

                if (currentStop != null && (currentStop.OrderState == OrderState.Working || currentStop.OrderState == OrderState.Accepted))
                {
                    var newPending = new PendingStopReplacement
                    {
                        EntryName = entryName,
                        Quantity = pos.RemainingContracts,
                        StopPrice = validatedStopPrice,
                        Direction = pos.Direction,
                        OldOrder = currentStop,
                        CreatedTime = DateTime.Now  // V8.30: Timeout support
                    };

                    // V8.30: Thread-safe add
                    if (pendingStopReplacements.TryAdd(entryName, newPending))
                    {
                        int currentCount = Interlocked.Increment(ref pendingReplacementCount);
                        if (currentCount >= CIRCUIT_BREAKER_THRESHOLD && !circuitBreakerActive)
                        {
                            circuitBreakerActive = true;
                            circuitBreakerActivatedTime = DateTime.Now;
                            Print(string.Format("V8.30: CIRCUIT BREAKER ACTIVATED - {0} pending replacements", currentCount));
                        }
                    }

                    // Build 950: Snapshot Working/Accepted targets before cancel for OCO cascade restoration.
                    {
                        var _b950Targets = new System.Collections.Generic.List<TargetSnapshot>();
                        for (int _t = 1; _t <= 5; _t++)
                        {
                            var _tD = GetTargetOrdersDictionary(_t);
                            Order _tO;
                            if (_tD != null && _tD.TryGetValue(entryName, out _tO) && _tO != null
                                && (_tO.OrderState == OrderState.Working || _tO.OrderState == OrderState.Accepted))
                                _b950Targets.Add(new TargetSnapshot { TargetNum = _t, Price = _tO.LimitPrice, Qty = _tO.Quantity, CapturedOrder = _tO });
                        }
                        newPending.CapturedTargets = _b950Targets.Count > 0 ? _b950Targets.ToArray() : null;
                        newPending.BracketRestorationNeeded = _b950Targets.Count > 0;
                    }

                    if (pos.ExecutingAccount != null)
                    {
                        pos.ExecutingAccount.Cancel(new[] { currentStop });
                    }
                    else
                    {
                        CancelOrder(currentStop);
                    }
                    pos.CurrentStopPrice = validatedStopPrice;
                    pos.CurrentTrailLevel = newTrailLevel;

                    string levelName = newTrailLevel <= 0 ? "Initial" : (newTrailLevel == 1 ? "BE" : "T" + (newTrailLevel - 1));
                    Print(string.Format("STOP UPDATED: {0} -> {1:F2} (Level: {2})", entryName, validatedStopPrice, levelName));
                    return;
                }

                // No existing stop or not in a cancellable state - create directly
                if (pos.ExecutingAccount != null)
                {
                    newStop = pos.ExecutingAccount.CreateOrder(Instrument, pos.Direction == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover, 
                        OrderType.StopMarket, TimeInForce.Gtc, pos.RemainingContracts, 0, validatedStopPrice, "Stop_" + entryName, "Stop_" + entryName, null);
                    pos.ExecutingAccount.Submit(new[] { newStop });
                    stopOrders[entryName] = newStop;
                }
                else
                {
                    // V12.3: Truncate signal name to stay under 50-char NinjaTrader limit
                    string suffix = (DateTime.Now.Ticks % 100000000).ToString();
                    string stopSigName = "S_" + entryName + "_" + suffix;
                    if (stopSigName.Length > 50) stopSigName = stopSigName.Substring(0, 50);
                    OrderAction stopExitAction = pos.Direction == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;
                    newStop = SubmitOrderUnmanaged(0, stopExitAction, OrderType.StopMarket, pos.RemainingContracts, 0, validatedStopPrice, "", stopSigName);

                    if (newStop != null) stopOrders[entryName] = newStop;
                }

                if (newStop == null)
                {
                    Print(string.Format("(!) CRITICAL ERROR: Stop order submission returned NULL for {0}!", entryName));
                    Print(string.Format("(!) POSITION UNPROTECTED: {0} {1} contracts @ {2:F2}",
                        pos.Direction == MarketPosition.Long ? "LONG" : "SHORT",
                        pos.RemainingContracts,
                        pos.EntryPrice));
                    Print(string.Format("(!) Attempted stop price: {0:F2} | Current price: {1:F2}", validatedStopPrice, Close[0]));

                    Print(string.Format("(!) Attempting emergency flatten for {0}...", entryName));
                    FlattenPositionByName(entryName);
                    return;
                }

                stopOrders[entryName] = newStop;
                pos.CurrentStopPrice = validatedStopPrice;
                pos.CurrentTrailLevel = newTrailLevel;

                string levelName2 = newTrailLevel == 1 ? "BE" : "T" + (newTrailLevel - 1);
                Print(string.Format("STOP UPDATED: {0} -> {1:F2} (Level: {2})", entryName, validatedStopPrice, levelName2));

            }
            catch (Exception ex)
            {
                Print(string.Format("(!) ERROR UpdateStopOrder for {0}: {1}", entryName, ex.Message));
                Print(string.Format("(!) POSITION MAY BE UNPROTECTED: {0} contracts", pos.RemainingContracts));
                
                // Attempt emergency flatten
                try
                {
                    Print(string.Format("(!) Attempting emergency flatten for {0}...", entryName));
                    FlattenPositionByName(entryName);
                }
                catch (Exception flattenEx)
                {
                    Print(string.Format("(!)(!) EMERGENCY FLATTEN FAILED: {0}", flattenEx.Message));
                }
            }
        }

        // V12.10: Fleet Symmetry -- calculates the correct stop price for a given trail level
        // using the position's own entry/extreme prices. Pure calculation, no side effects.
        private double CalculateStopForLevel(PositionInfo pos, int level)
        {
            bool isLong = (pos.Direction == MarketPosition.Long);
            switch (level)
            {
                case 1: // Breakeven
                    return isLong
                        ? pos.EntryPrice + (BreakEvenOffsetTicks * tickSize)
                        : pos.EntryPrice - (BreakEvenOffsetTicks * tickSize);
                case 2: // Trail 1
                    return isLong
                        ? pos.ExtremePriceSinceEntry - Trail1DistancePoints
                        : pos.ExtremePriceSinceEntry + Trail1DistancePoints;
                case 3: // Trail 2
                    return isLong
                        ? pos.ExtremePriceSinceEntry - Trail2DistancePoints
                        : pos.ExtremePriceSinceEntry + Trail2DistancePoints;
                case 4: // Trail 3
                    return isLong
                        ? pos.ExtremePriceSinceEntry - Trail3DistancePoints
                        : pos.ExtremePriceSinceEntry + Trail3DistancePoints;
                default:
                    return pos.CurrentStopPrice; // No change
            }
        }

        private void OnBreakevenButtonClick()
        {
            try
            {
                if (activePositions.Count == 0)
                {
                    Print("BREAKEVEN: No active positions");
                    return;
                }

                // V8.30: Thread-safe snapshot iteration for UI button handler
                var posSnapshot = activePositions.ToArray();

                // Check if any positions are already triggered (can't toggle after trigger)
                bool anyTriggered = false;
                foreach (var kvp in posSnapshot)
                {
                    if (kvp.Value.ManualBreakevenTriggered)
                    {
                        anyTriggered = true;
                        break;
                    }
                }

                if (anyTriggered)
                {
                    Print("BREAKEVEN: Already triggered - cannot toggle");
                    return;
                }

                // Check current state - if any armed, disarm all; if none armed, arm all
                bool anyArmed = false;
                foreach (var kvp in posSnapshot)
                {
                    if (kvp.Value.ManualBreakevenArmed)
                    {
                        anyArmed = true;
                        break;
                    }
                }

                // Toggle: if armed, disarm; if disarmed, arm
                foreach (var kvp in posSnapshot)
                {
                    if (!activePositions.ContainsKey(kvp.Key)) continue;
                    PositionInfo pos = kvp.Value;
                    if (pos.EntryFilled && !pos.ManualBreakevenTriggered)
                    {
                        if (anyArmed)
                        {
                            // Disarm
                            pos.ManualBreakevenArmed = false;
                            Print(string.Format("BREAKEVEN DISARMED: {0}", kvp.Key));
                        }
                        else
                        {
                            // Arm
                            pos.ManualBreakevenArmed = true;
                            Print(string.Format("BREAKEVEN ARMED: {0} - Will trigger at Entry + {1} tick(s)",
                                kvp.Key, BreakEvenOffsetTicks));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Print("ERROR OnBreakevenButtonClick: " + ex.Message);
            }
        }

        #endregion

        #region Stop Management Helpers (V11)

        /// <summary>
        /// Moves all active position stops to Breakeven + Offset Points.
        /// If offset is 0, it is pure breakeven.
        /// </summary>
        private void MoveStopsToBreakevenWithOffset(double offsetPoints)
        {
            try
            {
                if (activePositions.Count == 0)
                {
                    Print("[BE_INFO] No active trades in memory to move.");
                    return;
                }

                foreach (var kvp in activePositions.ToArray())
                {
                    PositionInfo pos = kvp.Value;
                    string entryName = kvp.Key;

                    if (!pos.EntryFilled || pos.RemainingContracts <= 0) continue;

                    double newStopPrice;
                    if (pos.Direction == MarketPosition.Long)
                        newStopPrice = pos.EntryPrice + offsetPoints;
                    else
                        newStopPrice = pos.EntryPrice - offsetPoints;

                    // Round to tick size
                    newStopPrice = Instrument.MasterInstrument.RoundToTickSize(newStopPrice);

                    // [V12.12] ARM GUARD: If price hasn't cleared the BE threshold yet, arm instead of executing.
                    // ManageTrailingStops() will call UpdateStopOrder when price crosses the threshold.
                    if (lastKnownPrice <= 0)
                    {
                        Print(string.Format("[BE_ABORT] {0}: Price data stale (0). Waiting for next tick.", entryName));
                        continue;
                    }
                    double referencePrice = lastKnownPrice;
                    bool priceCleared = pos.Direction == MarketPosition.Long
                        ? referencePrice >= newStopPrice
                        : referencePrice <= newStopPrice;

                    if (!priceCleared)
                    {
                        pos.ManualBreakevenArmed = true;
                        pos.ManualBreakevenTriggered = false;
                        Print(string.Format("[V12] BE Armed: {0} Price has not reached threshold. Shielding entry once cleared.", entryName));
                        continue;
                    }

                    // Only move stop if it's a better price (profit-protecting direction)
                    bool isBetter = (pos.Direction == MarketPosition.Long && newStopPrice > pos.CurrentStopPrice)
                                 || (pos.Direction == MarketPosition.Short && newStopPrice < pos.CurrentStopPrice);

                    if (!isBetter)
                    {
                        Print(string.Format("BE+{0}: Stop already better for {1}. Current={2:F2}, Request={3:F2}",
                            offsetPoints, entryName, pos.CurrentStopPrice, newStopPrice));
                        continue;
                    }

                    // V12.10: Use UpdateStopOrder for proper Master/Follower routing
                    // (ChangeOrder only works for Master -- followers were silently skipped)
                    UpdateStopOrder(entryName, pos, newStopPrice, 1);
                    pos.ManualBreakevenTriggered = true;
                    Print(string.Format("BE+{0} MOVED: {1} Stop -> {2:F2}", offsetPoints, entryName, newStopPrice));
                }
            }
            catch (Exception ex)
            {
                Print("ERROR MoveStopsToBreakevenWithOffset: " + ex.Message);
            }
        }

        /// <summary>
        /// V14: Moves a specific target to a new profit level (Entry + X points)
        /// </summary>
        /// <param name="targetNum">Target number (1-5)</param>
        /// <param name="profitPoints">Points of profit from entry (1.0 or 2.0)</param>
        private void MoveSpecificTarget(int targetNum, double profitPoints)
        {
            if (targetNum < 1 || targetNum > 5)
            {
                Print($"[V14] MoveSpecificTarget: Invalid target number {targetNum}");
                return;
            }
            
            if (activePositions == null || activePositions.Count == 0)
            {
                Print($"[V14] MoveSpecificTarget: No active positions to move target T{targetNum}");
                return;
            }
            
            int movedCount = 0;
            
            // Iterate through all active positions
            foreach (var kvp in activePositions.ToArray())
            {
                if (!activePositions.ContainsKey(kvp.Key)) continue;
                
                PositionInfo pos = kvp.Value;
                string entryName = kvp.Key;
                
                if (!pos.EntryFilled)
                {
                    Print($"[V14] MoveSpecificTarget T{targetNum}: Skipping {entryName} - entry not filled");
                    continue;
                }
                
                // Find the target order for this position
                // [1102Z-F]: Search the correct account -- follower orders live on their own account,
                // not on the Master account from which Account.Orders is sourced.
                string targetOrderName = $"T{targetNum}_{entryName}";
                Order targetOrder = null;
                var searchAcct = (pos.IsFollower && pos.ExecutingAccount != null)
                    ? pos.ExecutingAccount
                    : Account;
                
                foreach (Order order in searchAcct.Orders)
                {
                    if (order != null && 
                        order.Name == targetOrderName && 
                        order.Instrument.FullName == Instrument.FullName &&
                        (order.OrderState == OrderState.Working || 
                         order.OrderState == OrderState.Accepted))
                    {
                        targetOrder = order;
                        break;
                    }
                }
                
                if (targetOrder == null)
                {
                    Print($"[V14] MoveSpecificTarget T{targetNum}: No working order found for {entryName} (may already be filled)");
                    continue;
                }
                
                // Calculate new target price: Entry Price + Profit Points
                double entryPrice = pos.EntryPrice;
                double newTargetPrice;
                
                if (pos.Direction == MarketPosition.Long)
                {
                    newTargetPrice = entryPrice + profitPoints;
                }
                else // Short
                {
                    newTargetPrice = entryPrice - profitPoints;
                }
                
                // Round to tick size
                newTargetPrice = Instrument.MasterInstrument.RoundToTickSize(newTargetPrice);
                
                // Validate: Don't move target past current market (would execute immediately)
                double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];
                bool isValidMove = true;
                
                if (pos.Direction == MarketPosition.Long)
                {
                    // Long: Target should be above entry, but below or at market is OK (just fills immediately)
                    if (newTargetPrice < entryPrice)
                    {
                        Print($"[V14] MoveSpecificTarget T{targetNum}: REJECTED - Long target {newTargetPrice:F2} below entry {entryPrice:F2}");
                        isValidMove = false;
                    }
                }
                else // Short
                {
                    // Short: Target should be below entry
                    if (newTargetPrice > entryPrice)
                    {
                        Print($"[V14] MoveSpecificTarget T{targetNum}: REJECTED - Short target {newTargetPrice:F2} above entry {entryPrice:F2}");
                        isValidMove = false;
                    }
                }
                
                if (!isValidMove) continue;
                
                // Move the order: Master uses ChangeOrder; followers use cancel+resubmit via account API.
                // ChangeOrder only works for orders submitted through the NinjaScript managed order system.
                // Fleet follower orders are submitted via acct.Submit(), so they require the broker-level API.
                try
                {
                    if (pos.IsFollower && pos.ExecutingAccount != null)
                    {
                        // [1102Z-F]: Fleet follower path -- cancel old limit, resubmit at new price
                        pos.ExecutingAccount.Cancel(new[] { targetOrder });

                        OrderAction exitAct = pos.Direction == MarketPosition.Long
                            ? OrderAction.Sell : OrderAction.BuyToCover;
                        Order newFollowerOrder = pos.ExecutingAccount.CreateOrder(
                            Instrument, exitAct, OrderType.Limit, TimeInForce.Gtc,
                            targetOrder.Quantity, newTargetPrice, 0, "", targetOrderName, null);
                        pos.ExecutingAccount.Submit(new[] { newFollowerOrder });

                        // Update dictionary reference so the strategy tracks the new order
                        var tDictF = GetTargetOrdersDictionary(targetNum);
                        if (tDictF != null) tDictF[entryName] = newFollowerOrder;

                        movedCount++;
                        double profitFromEntryF = Math.Abs(newTargetPrice - entryPrice);
                        Print($"[SIMA] MoveSpecificTarget T{targetNum}: Follower {entryName} on {pos.ExecutingAccount.Name} -> {newTargetPrice:F2} (+{profitFromEntryF:F2})");
                    }
                    else
                    {
                        // Master path -- ChangeOrder is fine for NinjaScript-managed orders
                        ChangeOrder(targetOrder, targetOrder.Quantity, newTargetPrice, 0);
                        movedCount++;

                        double profitFromEntry = Math.Abs(newTargetPrice - entryPrice);
                        Print($"[V14] MoveSpecificTarget T{targetNum}: {entryName} -> {newTargetPrice:F2} (+{profitFromEntry:F2} from entry {entryPrice:F2})");
                    }
                }
                catch (Exception ex)
                {
                    Print($"[V14] MoveSpecificTarget T{targetNum}: Move FAILED for {entryName} - {ex.Message}");
                }
            }
            
            if (movedCount > 0)
            {
                Print($"[V14] MoveSpecificTarget T{targetNum}: Moved {movedCount} target(s) to +{profitPoints}pt profit");
            }
            else
            {
                Print($"[V14] MoveSpecificTarget T{targetNum}: No targets were moved (no active working orders found)");
            }
        }

        #endregion
    }
}
