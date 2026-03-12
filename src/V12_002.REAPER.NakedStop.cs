// V12 REAPER Emergency Stop -- Naked-position hard stop protection (Build 1102R)
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
        #region V12 REAPER Emergency Stop

        /// <summary>
        /// Build 1102R: Processes queued naked-position emergency stop requests on the strategy thread.
        /// Called directly from the actor-enqueued REAPER audit flow.
        /// Submits a StopMarket order at MaximumStop ticks from current close to protect the naked position.
        /// </summary>
        private void ProcessReaperNakedStopQueue()
        {
            while (_reaperNakedStopQueue.TryDequeue(out var item))
            {
                try
                {
                    Account acct = Account.All.FirstOrDefault(a => a.Name == item.AccountName);
                    if (acct == null)
                    {
                        Print(string.Format("[REAPER][NAKED_STOP] Account {0} not found -- skipping.", item.AccountName));
                        _reaperNakedStopInFlight.TryRemove(ExpKey(item.AccountName), out _); // [Build 969.3]
                        continue;
                    }

                    // Compute emergency stop price: MaximumStop ticks from current close.
                    // Close[0] is safe here -- ProcessReaperNakedStopQueue runs on the strategy thread.
                    double emergencyStopDist = MaximumStop;
                    double atrBound = CalculateATRStopDistance(RMAStopATRMultiplier);
                    if (atrBound > 0)
                        emergencyStopDist = Math.Min(emergencyStopDist, atrBound);
                    if (emergencyStopDist <= 0)
                        emergencyStopDist = Math.Max(tickSize, MinimumStop);

                    double stopPrice;
                    OrderAction closeAction;

                    if (item.Direction == MarketPosition.Long)
                    {
                        stopPrice   = Instrument.MasterInstrument.RoundToTickSize(Close[0] - emergencyStopDist);
                        closeAction = OrderAction.Sell;
                    }
                    else
                    {
                        stopPrice   = Instrument.MasterInstrument.RoundToTickSize(Close[0] + emergencyStopDist);
                        closeAction = OrderAction.BuyToCover;
                    }

                    string signalName = "EMERGENCY_STOP_" + item.AccountName;
                    Order emergencyStop = acct.CreateOrder(
                        Instrument, closeAction, OrderType.StopMarket,
                        TimeInForce.Gtc, item.Qty,
                        0, stopPrice, "", signalName, null);

                    acct.Submit(new[] { emergencyStop });

                    // BUG-M2: Clear in-flight guard after successful submission
                    _reaperNakedStopInFlight.TryRemove(ExpKey(item.AccountName), out _); // [Build 969.3] - Clears guard for immediate retry if broker update latches
                    Print(string.Format(
                        "[REAPER][EMERGENCY_STOP] Submitted StopMarket for {0}: {1} {2}ct @ {3:F2} (Dist={4:F2})",
                        item.AccountName, closeAction, item.Qty, stopPrice, emergencyStopDist));
                }
                catch (Exception ex)
                {
                    // BUG-M2: Clear in-flight guard on failure so next cycle can retry
                    _reaperNakedStopInFlight.TryRemove(ExpKey(item.AccountName), out _); // [Build 968]
                    Print(string.Format("[REAPER][EMERGENCY_STOP_FAIL] {0}: {1}", item.AccountName, ex.Message));
                }
            }
        }

        #endregion
    }
}
