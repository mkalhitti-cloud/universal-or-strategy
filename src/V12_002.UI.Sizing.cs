// V12.44 MODULAR: ATR Auto-Sizing Engine Module (Split from UI.cs)
// Contains: Position sizing, ATR stop calculations, target distribution, pending order sync
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
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.Strategies;
using System.Net;
using System.Net.Sockets;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        #region V12.30 ATR Auto-Sizing Engine

        // IS-01: Iron Shield Target Distribution [V12.BEYOND-BUG]
        // Replaces percentage-based engine with count-based integer division.
        // Source of truth: activeTargetCount (mirrors dashboard selection exactly).
        // Ghost targets eliminated -- T4/T5 are always 0 when count < 4/5.
        // FIX-B [Build 1102Z]: Added targetCountOverride optional parameter.
        // When a caller passes a pre-snapshotted count (e.g., dispatchTargetCount from SIMA),
        // it is used instead of the live activeTargetCount global read. This prevents the
        // IPC SET_TARGET_COUNT command from changing the distribution mid-dispatch.
        // All existing call sites omit the parameter and continue using the live global (no breaking change).
        private void GetTargetDistribution(int contracts, out int t1, out int t2, out int t3, out int t4, out int t5,
            int targetCountOverride = -1)
        {
            t1 = 0; t2 = 0; t3 = 0; t4 = 0; t5 = 0;
            if (contracts <= 0) return;

            // IS-01: Clamp active target count (Source of Truth -- mirrors dashboard selection).
            // Use caller snapshot when provided; fall through to live activeTargetCount for all other call sites.
            int count = (targetCountOverride >= 1 && targetCountOverride <= 5)
                ? targetCountOverride
                : Math.Max(1, Math.Min(5, activeTargetCount));

            // IS-02: Integer-Division Distribution (remainder to T1 first -- scalp anchor)
            int[] buckets = new int[5];
            int baseQty   = contracts / count;
            int remainder = contracts % count;
            for (int i = 0; i < count; i++)
                buckets[i] = baseQty + (i < remainder ? 1 : 0);

            // IS-03: Teleporting Backstop (Final Reserve Anchor)
            int captureIndex = count - 1;

            // V12.Audit [D-008]: Final Sum Gate
            int distSum = buckets[0] + buckets[1] + buckets[2] + buckets[3] + buckets[4];
            if (distSum != contracts)
            {
                Print($"[SIZING_FATAL] Sum={distSum} Expected={contracts}. Forcing to T{count}.");
                buckets[captureIndex] += contracts - distSum;
            }

            t1 = buckets[0]; t2 = buckets[1]; t3 = buckets[2]; t4 = buckets[3]; t5 = buckets[4];
        }

        /// <summary>
        /// V12.30: ATR Auto-Sizing Engine -- Core Sizing Method
        /// 1. stopDistanceRaw -> Ceiling to whole point
        /// 2. Quantity -> Floor(MaxRisk / (ceilingStop * pointValue))
        /// 3. Clamp to [minContracts, max]
        /// </summary>
        private int CalculatePositionSize(double stopDistanceRaw)
        {
            if (double.IsNaN(stopDistanceRaw)      || double.IsInfinity(stopDistanceRaw)      ||
                double.IsNaN(MaxRiskAmount)         || double.IsInfinity(MaxRiskAmount)         ||
                double.IsNaN(SlippageCushionPoints) || double.IsInfinity(SlippageCushionPoints) ||
                pointValue <= 0)
            {
                Print("[SIZING] (!) Invalid sizing inputs -- returning min contracts");
                return Math.Max(1, minContracts);
            }
            if (stopDistanceRaw <= 0) return Math.Max(1, minContracts);

            // STEP 1: CEILING to whole POINT (e.g. 2.3 -> 3.0, 4.0 -> 4.0)
            double stopPoints = Math.Ceiling(stopDistanceRaw);

            double riskToUse = MaxRiskAmount;
            double stopDollars = stopPoints * pointValue;
            if (stopDollars <= 0) return Math.Max(1, minContracts);

            // SLIP-01: Subtract slippage cushion from risk budget before sizing.
            // Followers may fill at worse prices than master (entry slippage); their stop
            // is at the same absolute level -> actual dollar risk = (fillPrice - stop) x qty x pv.
            // Cushion ensures worst-case follower risk stays <= MaxRiskAmount.
            double slippageCushionDollars = SlippageCushionPoints * pointValue;
            double effectiveRisk = riskToUse - slippageCushionDollars;

            // STEP 2: FLOOR the quantity (never exceed $MaxRisk after slippage reserve)
            // [923A-P2b-OVF]: checked{} guards against astronomically low stopDollars (near-zero ATR)
            // producing a double->int overflow. Clamps to maxContracts on overflow rather than silent wrap.
            int contracts;
            try   { contracts = checked((int)Math.Floor(effectiveRisk / stopDollars)); }
            catch (OverflowException)
            {
                Print($"[923A-OVF] Sizing overflow -- stop={stopDollars:F4} effectiveRisk={effectiveRisk:F0} -- clamping to maxContracts ({maxContracts})");
                contracts = maxContracts;
            }

            // V12.Phase8.3: Diagnostic warning when ATR/Risk math produces 0 -- makes risk-floor fallbacks visible
            if (contracts == 0)
                Print($"[SIZING] Risk/Stop math resulted in 0 -- falling back to minContracts floor ({minContracts}). Risk=${riskToUse:F0}, StopDollars=${stopDollars:F0}");

            // V12.1101E [B-9]: Clamp to [minContracts, maxContracts] -- prevents runaway sizing on
            // tiny ATR values (e.g., flat market) from hitting broker limits or compliance thresholds.
            contracts = Math.Max(minContracts, Math.Min(contracts, maxContracts));

            Print($"[V12.30 SIZING] RawStop={stopDistanceRaw:F2} -> Ceiling={stopPoints:F0}pt | Risk=${riskToUse:F0} | Cushion=${slippageCushionDollars:F0} | EffRisk=${effectiveRisk:F0} | StopDollars=${stopDollars:F0} | Qty={contracts} | Clamp=[{minContracts},{maxContracts}]");
            return contracts;
        }

        /// <summary>
        /// V12.30: ATR Auto-Sizing Engine -- Centralized Stop Distance Calculator
        /// Returns ATR-based stop rounded UP to nearest whole point.
        /// Replaces all inline "currentATR * multiplier" patterns.
        /// </summary>
        private double CalculateATRStopDistance(double atrMultiplier)
        {
            if (currentATR <= 0) return MinimumStop;

            double rawStop = currentATR * atrMultiplier;
            double ceilingStop = Math.Ceiling(rawStop);  // Round UP to whole point
            return Math.Max(MinimumStop, Math.Min(ceilingStop, MaximumStop));
        }

        /// <summary>
        /// V12.45: Live Sync Engine -- Updates unfilled entry orders when ATR
        /// causes ceiling-stop or floor-qty to change.
        /// FLICKER PROTECTION HARDENING:
        /// 1. Order State Guard: Only sync orders in Accepted/Working state (blocks ChangePending)
        /// 2. Tick-Aware Threshold: Uses tickSize instead of hardcoded 0.01
        /// 3. Retry Cooldown: 500ms pause after ChangeOrder failure to prevent broker hammering
        /// </summary>
        private DateTime _lastSyncFailureTime = DateTime.MinValue;  // V12.45: Retry cooldown tracker

        private void SyncPendingOrders()
        {
            if (currentATR <= 0) return;

            // V12.45 RETRY COOLDOWN: If a ChangeOrder failed recently, back off for 500ms
            // This prevents rapid-fire rejections that can cascade into broker throttling
            if ((DateTime.Now - _lastSyncFailureTime).TotalMilliseconds < 500) return;

            foreach (var kvp in activePositions.ToArray())
            {
                PositionInfo pos = kvp.Value;
                string entryName = kvp.Key;

                // Only sync UNFILLED entries
                if (pos.EntryFilled) continue;

                // V1102Q [SOVEREIGN-DRIFT]: Followers skip active ATR-sync.
                // They purely follow the master-dispatched quantity.
                if (pos.IsFollower) continue;

                // Get the entry order
                Order entryOrder;
                if (!entryOrders.TryGetValue(entryName, out entryOrder)) continue;
                if (entryOrder == null) continue;

                // V12.45 ORDER STATE GUARD: Only modify orders in stable states
                // Accepted = broker acknowledged, waiting for fill
                // Working  = actively in the order book
                // ChangePending = a ChangeOrder is already in-flight -- DO NOT send another
                OrderState currentState = entryOrder.OrderState;
                if (currentState != OrderState.Accepted && currentState != OrderState.Working)
                {
                    if (currentState == OrderState.ChangePending)
                        Print($"[V12.45 SYNC] SKIP {entryName}: ChangeOrder already in-flight (ChangePending)");
                    continue;
                }

                // [RACE-05]: Compute sizing math + flicker check + stop-price update atomically under stateLock.
                // Prevents volatility drift where currentATR changes between math and state mutation.
                // ChangeOrder broker call is staged outside the lock (broker API must not hold our lock).
                int    newQty;
                bool   needsQtyChange;
                string syncLog;
                // [M8.2 SIZING-SYNC]: Capture expected-position delta for Live Sync quantity changes.
                int    expectedDelta = 0;
                string acctName     = null;

                lock (stateLock)
                {
                    double atrMult    = GetATRMultiplierForPosition(pos);
                    double newStopDist = CalculateATRStopDistance(atrMult);
                    newQty            = CalculatePositionSize(newStopDist);

                    // V12.45 TICK-AWARE FLICKER CHECK: use tickSize for meaningful comparison
                    double oldCeilingStop = Math.Ceiling(Math.Abs(pos.EntryPrice - pos.CurrentStopPrice));
                    double stopDelta      = Math.Abs(newStopDist - oldCeilingStop);
                    if (stopDelta < tickSize && newQty == pos.TotalContracts)
                        continue;  // No material change -- skip (releases lock before continuing)

                    double newStopPrice = pos.Direction == MarketPosition.Long
                        ? pos.EntryPrice - newStopDist
                        : pos.EntryPrice + newStopDist;

                    // Stop prices update immediately -- they reflect intent and are safe before broker confirmation.
                    pos.CurrentStopPrice = newStopPrice;
                    pos.InitialStopPrice = newStopPrice;

                    // [VOLATILITY-01]: TotalContracts / distribution are NOT updated here.
                    // They are committed in OnOrderUpdate when broker confirms the ChangeOrder (Accepted state).
                    // This prevents Desync-01 if the broker rejects the size change.
                    needsQtyChange = newQty != entryOrder.Quantity;
                    if (needsQtyChange)
                    {
                        // [M8.2 SIZING-SYNC]: Mirror the quantity change into expectedPositions so Reaper
                        // sees the updated target size before the fill arrives.
                        int qtyDelta    = newQty - entryOrder.Quantity;
                        expectedDelta   = pos.Direction == MarketPosition.Long ? qtyDelta : -qtyDelta;
                        acctName        = (pos.IsFollower && pos.ExecutingAccount != null)
                                            ? pos.ExecutingAccount.Name : Account.Name;
                    }
                    syncLog = $"[V12.45 SYNC] {entryName}: Stop {oldCeilingStop:F0}->{newStopDist:F0}pt | Qty {entryOrder.Quantity}->{newQty} | ATR={currentATR:F2}";
                }

                // ChangeOrder must be called outside stateLock -- broker API call.
                try
                {
                    if (needsQtyChange)
                    {
                        ChangeOrder(entryOrder, newQty, entryOrder.LimitPrice, entryOrder.StopPrice);
                        // [M8.2 SIZING-SYNC]: Update expectedPositions only after ChangeOrder succeeds.
                        // A failed ChangeOrder (caught below) will not leave a stale expectedPositions delta.
                        AddExpectedPositionDeltaLocked(ExpKey(acctName), expectedDelta);
                        // V12.Phantom-Fix [FIX-3]: Log only when a ChangeOrder is actually sent.
                        // Unconditional Print on every bar created hundreds of no-op log lines
                        // while a Limit order sat pending fill on tick/renko charts.
                        Print(syncLog);
                    }
                }
                catch (Exception ex)
                {
                    // V12.45 RETRY COOLDOWN: Record failure time to prevent hammering
                    _lastSyncFailureTime = DateTime.Now;
                    Print($"[V12.45 SYNC] ERROR syncing {entryName}: {ex.Message} -- cooldown 500ms");
                }
            }
        }

        /// <summary>
        /// V12.30: Returns the ATR multiplier for a given position type.
        /// Used by SyncPendingOrders to determine which multiplier to recalculate with.
        /// </summary>
        private double GetATRMultiplierForPosition(PositionInfo pos)
        {
            if (pos.IsRMATrade) return RMAStopATRMultiplier;
            if (pos.IsTRENDTrade)
            {
                if (pos.IsTRENDEntry1)
                    return isTrendRmaMode ? RMAStopATRMultiplier : TRENDEntry1ATRMultiplier;
                return isTrendRmaMode ? RMAStopATRMultiplier : TRENDEntry2ATRMultiplier;
            }
            if (pos.IsRetestTrade)
                return isRetestRmaMode ? RMAStopATRMultiplier : RetestATRMultiplier; // V12.Hardening: was isTrendRmaMode (typo)
            return StopMultiplier; // ORB default
        }

        #endregion
    }
}
