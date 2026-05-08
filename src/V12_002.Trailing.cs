// <copyright file="V12_002.Trailing.cs" company="BMad">
// Copyright (c) BMad. All rights reserved.
// </copyright>
// V12.46 MODULAR: Trailing Stop Module (Extracted from Orders.cs)
// Contains: ManageTrailingStops, CleanupStalePendingReplacements, UpdateStopOrder,
//           CalculateStopForLevel, MoveStopsToBreakevenWithOffset, MoveSpecificTarget
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
            bool _shouldExit;
            ManageTrail_AdaptiveThrottleTick(out _shouldExit);
            if (_shouldExit) return;

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

            // Build 1105: Shadow Mode auto-propagation (runs after fleet sync)
            ShadowEngineCheck();
        }

        private void ManageTrail_AdaptiveThrottleTick(out bool shouldExit)
        {
            shouldExit = false;
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
            if ((now - lastStopManagementTime).TotalMilliseconds < adaptiveThrottleMs) { shouldExit = true; return; }

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
                    shouldExit = true; return; // Skip trailing stop updates while circuit breaker is active
                }
            }
        }

        // V8.30: Clean up stale pending replacements that are older than 5 seconds
        // Prevents memory leak and ensures positions remain protected
        #endregion
    }
}
