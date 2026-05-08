// <copyright file="V12_002.REAPER.cs" company="BMad">
// Copyright (c) BMad. All rights reserved.
// </copyright>
// V12.17 THREADING FIX: Reaper (Safety Hub) Module
// REAPER Module (Extracted)
// FIX: acct.Flatten() calls moved from background thread -> strategy thread via TriggerCustomEvent
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        #region V12 REAPER Audit Logic

        // V12.17: Queue for flatten requests marshaled from background thread -> strategy thread
        private ConcurrentQueue<string> _reaperFlattenQueue = new ConcurrentQueue<string>();

        // V12.Phase8.2: Queue for repair requests marshaled from background thread -> strategy thread
        private ConcurrentQueue<string> _reaperRepairQueue = new ConcurrentQueue<string>();
        // V12.Phase8.2: Prevents double-repair for the same account while an order is in-flight
        private readonly ConcurrentDictionary<string, byte> _repairInFlight = new ConcurrentDictionary<string, byte>(); // [Build 968]
        // [Phase 5 Repair] Mirrors _repairInFlight to dedupe flatten enqueues across audit cycles.
        private readonly ConcurrentDictionary<string, byte> _reaperFlattenInFlight = new ConcurrentDictionary<string, byte>();

        // Build 1102R: Queue for naked-position emergency stop requests (background -> strategy thread)
        private ConcurrentQueue<(string AccountName, MarketPosition Direction, int Qty)> _reaperNakedStopQueue
            = new ConcurrentQueue<(string, MarketPosition, int)>();
        // Build 1102R: Prevents duplicate emergency stops while broker confirmation is pending (mirrors _repairInFlight)
        private readonly ConcurrentDictionary<string, byte> _reaperNakedStopInFlight = new ConcurrentDictionary<string, byte>(); // [Build 968]

        // GHOST-FIX-2 [Build 922Z]: Tracks when an account first appeared as "naked" (position with no working stop).
        // REAPER only fires emergency stop after NakedPositionGraceSec have elapsed, preventing race-condition
        // triggers during the normal bracket-confirmation window immediately after a fill.
        private ConcurrentDictionary<string, DateTime> _nakedPositionFirstSeen
            = new ConcurrentDictionary<string, DateTime>();

        // Build 999: Tracks accounts where Phase 5 Position Pass failed (stop in CancelPending during reconnect).
        // REAPER defers critical desync up to 10s to allow the stop-replace cycle to complete.
        // Keyed by account name; value = UTC time of first Position Pass failure detected.
        private ConcurrentDictionary<string, DateTime> _positionPassFailedFirstSeen
            = new ConcurrentDictionary<string, DateTime>();

        // [922Z-THROTTLE]: Prevents "Repair BLOCKED" from printing every second during intentional long-sitting orders.
        // Key = blocking order name; Value = last time the message was printed.
        private ConcurrentDictionary<string, DateTime> _repairBlockedLastLogged
            = new ConcurrentDictionary<string, DateTime>();

        // Build 935 [REAPER-B935-002]: Per-account fill-grace timestamps.
        // Replaces single global _lastExpectedPositionSetTicks which incorrectly blocked ALL account repairs
        // whenever ANY account had a fill. Now each account tracks its own fill-grace window independently.
        private readonly ConcurrentDictionary<string, long> _accountFillGraceTicks
            = new ConcurrentDictionary<string, long>();

        /// <summary>Build 946: Track consecutive failed repair attempts per account where PositionInfo is missing.</summary>
        private readonly ConcurrentDictionary<string, int> _reaperOrphanRepairCount = new ConcurrentDictionary<string, int>();

        // Stamps per-account fill grace. Call from SetExpectedPositionLocked when applying a non-zero delta.
        private void StampAccountFillGrace(string expKey)
        {
            _accountFillGraceTicks[expKey] = DateTime.UtcNow.Ticks;
        }

        private bool IsReaperFillGraceActive(string expKey)
        {
            if (_accountFillGraceTicks.TryGetValue(expKey, out long stampTicks))
            {
                return stampTicks > 0 && (DateTime.UtcNow.Ticks - stampTicks) < ReaperFillGraceTicks;
            }
            // Fallback: check legacy global stamp (covers master account path)
            long globalStamp = Interlocked.Read(ref _lastExpectedPositionSetTicks);
            return globalStamp > 0 && (DateTime.UtcNow.Ticks - globalStamp) < ReaperFillGraceTicks;
        }


        private bool TryGetRepairDistanceLimitPoints(out double limitPoints)
        {
            limitPoints = 0;
            double atrLimit = CalculateATRStopDistance(RMAStopATRMultiplier);
            if (atrLimit <= 0)
            {
                atrLimit = MinimumStop;
            }

            double fenceLimit = (RepairTickFence > 0 && tickSize > 0)
                ? RepairTickFence * tickSize
                : atrLimit;

            limitPoints = Math.Min(Math.Abs(atrLimit), Math.Abs(fenceLimit));
            return limitPoints > 0;
        }

        /// <summary>
        /// V12 SIMA: Start the Reaper audit timer.
        /// Phase 3.5 Migration: Replaces background thread with strategy-thread timer.
        /// </summary>
        private void StartReaperAudit()
        {
            if (_reaperTimer != null) StopReaperAudit();

            _reaperTimer = new System.Timers.Timer(ReaperIntervalMs);
            _reaperTimer.Elapsed += OnReaperTimerElapsed;
            _reaperTimer.AutoReset = true;
            _reaperTimer.Enabled = true;

            Print("[REAPER] Audit timer STARTED - interval: " + ReaperIntervalMs + "ms (Strategy Thread)");
        }

        /// <summary>
        /// V12 SIMA: Stop the Reaper audit timer.
        /// </summary>
        private void StopReaperAudit()
        {
            if (_reaperTimer == null)
            {
                return;
            }

            _reaperTimer.Stop();
            _reaperTimer.Elapsed -= OnReaperTimerElapsed;
            _reaperTimer.Dispose();
            _reaperTimer = null;

            Print("[REAPER] Audit timer STOPPED");
        }

        /// <summary>
        /// Timer event handler. Marshals the audit call to the strategy thread.
        /// </summary>
        private void OnReaperTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            // V12.Phase8 [F-05]: Skip auditing while a flatten is actively running
            if (isFlattenRunning || !_orderAdoptionComplete || State != State.Realtime)
            {
                return;
            }

            try
            {
                // Marshal to strategy thread via TriggerCustomEvent
                TriggerCustomEvent(o => AuditApexPositions(), null);
            }
            catch (Exception ex)
            {
                Print("[REAPER] Timer Marshalling Error: " + ex.Message);
            }
        }

        #endregion
    }
}
