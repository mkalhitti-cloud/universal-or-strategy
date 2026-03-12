// V12.17 THREADING FIX: Reaper (Safety Hub) Module
// REAPER Module (Extracted)
// FIX: audit work is actor-enqueued so REAPER state reads run on the strategy thread
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

        // Stamps per-account fill grace. Call from SetExpectedPosition when applying a non-zero delta.
        private void StampAccountFillGrace(string expKey)
        {
            _accountFillGraceTicks[expKey] = DateTime.UtcNow.Ticks;
        }

        private bool IsReaperFillGraceActive(string expKey)
        {
            if (_accountFillGraceTicks.TryGetValue(expKey, out long stampTicks))
                return stampTicks > 0 && (DateTime.UtcNow.Ticks - stampTicks) < ReaperFillGraceTicks;
            // Fallback: check legacy global stamp (covers master account path)
            long globalStamp = Interlocked.Read(ref _lastExpectedPositionSetTicks);
            return globalStamp > 0 && (DateTime.UtcNow.Ticks - globalStamp) < ReaperFillGraceTicks;
        }


        private bool TryGetRepairDistanceLimitPoints(out double limitPoints)
        {
            limitPoints = 0;
            double atrLimit = CalculateATRStopDistance(RMAStopATRMultiplier);
            if (atrLimit <= 0)
                atrLimit = MinimumStop;

            double fenceLimit = (RepairTickFence > 0 && tickSize > 0)
                ? RepairTickFence * tickSize
                : atrLimit;

            limitPoints = Math.Min(Math.Abs(atrLimit), Math.Abs(fenceLimit));
            return limitPoints > 0;
        }

        /// <summary>
        /// V12 SIMA: Start the Reaper audit background thread
        /// </summary>
        private void StartReaperAudit()
        {
            if (isReaperRunning) return;

            isReaperRunning = true;
            reaperThread = new Thread(ReaperLoop)
            {
                IsBackground = true,
                Name = "V12_Reaper_Audit"
            };
            reaperThread.Start();
            Print("[REAPER] Audit thread STARTED - interval: " + ReaperIntervalMs + "ms");
        }

        /// <summary>
        /// V12 SIMA: Stop the Reaper audit background thread
        /// </summary>
        private void StopReaperAudit()
        {
            if (!isReaperRunning) return;

            isReaperRunning = false;
            try
            {
                if (reaperThread != null && reaperThread.IsAlive)
                {
                    reaperThread.Join(2000); // Wait up to 2 seconds
                }
            }
            catch { }
            Print("[REAPER] Audit thread STOPPED");
        }

        /// <summary>
        /// V12 SIMA: Reaper main loop - audits positions every ReaperIntervalMs
        /// </summary>
        private void ReaperLoop()
        {
            Print("[REAPER] Loop started - monitoring account positions...");

            // V12.Phase8 [F-05]: On cold start or reload the isFlattenRunning guard resets to false
            // even if a broker-side flatten is still in flight from the previous strategy instance.
            // Skip the very first audit cycle to allow any in-flight flatten to settle before
            // Reaper evaluates position state. This prevents false CRITICAL DESYNC on reload.
            bool firstCycle = true;

            while (isReaperRunning)
            {
                try
                {
                    Thread.Sleep(ReaperIntervalMs);
                    if (!isReaperRunning) break;

                    // V12.Phase8 [F-05]: Skip first cycle after startup -- grace period for in-flight flattens.
                    if (firstCycle)
                    {
                        firstCycle = false;
                        Print("[REAPER] Startup grace: skipping first audit cycle to allow in-flight flattens to settle.");
                        continue;
                    }

                    // V12.8: Pause auditing while a flatten is actively running to prevent race conditions
                    if (isFlattenRunning) continue;

                    // [BUILD 948] Skip audit until working orders have been re-adopted after restart/reconnect.
                    // Prevents false CRITICAL DESYNC or naked-position alerts during the adoption window.
                    if (!_orderAdoptionComplete) continue;

                    // V12.Hardening: Only audit in live/realtime -- skip historical replay
                    if (State != State.Realtime) continue;

                    Enqueue(ctx => ctx.AuditApexPositions());
                }
                catch (ThreadAbortException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Print("[REAPER] ERROR: " + ex.Message);
                }
            }
        }

        #endregion
    }
}
