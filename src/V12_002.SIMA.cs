// V12.12 FLEET SYMMETRY & SAFETY HARDENING - Single-Instance Multi-Account Copy Trading Engine
// SIMA Module (Extracted)
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Diagnostics;
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
        /// <summary>
        /// V12 SIMA: Helper struct to rank accounts by Daily P/L
        /// </summary>
        private struct AccountRankInfo
        {
            public Account Account;
            public double DailyPL;
            public string Name;
        }

        /// <summary>
        /// V12.Phase8 [F-01/F-02]: Staging struct for target orders -- committed to tracking dicts only after Submit succeeds.
        /// </summary>
        private struct StagedTarget
        {
            public int Num;
            public double Price;
            public Order Order;
        }

        /// <summary>
        /// Build 936 [FIX-1]: Self-contained unit for deferred acct.Submit() via TriggerCustomEvent pump.
        /// Created in ExecuteSmartDispatchEntry setup phase (fast path); consumed by PumpFleetDispatch
        /// on the strategy thread one-at-a-time, breaking the 7-second monolithic blocking window into
        /// N x (next-tick-cycle) slices.
        /// </summary>
        private struct FleetDispatchRequest
        {
            public Account Account;
            public Order[] Orders;
            public string FleetEntryName;
            public string ExpectedKey;
            public int ReservedDelta;
        }


        // V12.1101E [F-06]: REAPER audit is actor-enqueued, so expectedPositions helpers run lock-free on the strategy thread.
        private void AddExpectedPositionDelta(string accountName, int delta)
        {
            // B966: No internal Enqueue. Callers already execute on the strategy thread.
            if (string.IsNullOrEmpty(accountName) || expectedPositions == null) return;
            int oldVal = 0;
            expectedPositions.TryGetValue(accountName, out oldVal);
            int newVal = oldVal + delta;
            expectedPositions[accountName] = newVal;
            // [Phase 8.2 Part 3 - ACCOUNT_SYNC] Trace every mutation for desync audits.
            Print(string.Format("[ACCOUNT_SYNC] {0} expected: {1} -> {2}", accountName, oldVal, newVal));
            if (delta != 0)
            {
                Interlocked.Exchange(ref _lastExpectedPositionSetTicks, DateTime.UtcNow.Ticks);
                if (newVal != 0)
                    StampAccountFillGrace(accountName);
            }
        }

        // V12.1101E [F-06]: Shared AddOrUpdate wrapper. Callers already execute on the strategy thread.
        private void AddOrUpdateExpectedPosition(string accountName, int addValue, Func<int, int> updateExisting)
        {
            // B966: No internal Enqueue. Callers already execute on the strategy thread.
            if (string.IsNullOrEmpty(accountName) || expectedPositions == null || updateExisting == null) return;
            expectedPositions.AddOrUpdate(accountName, addValue, (k, v) => updateExisting(v));
        }

        // V12.1101E [F-06]: Strategy-thread set for expectedPositions.
        private void SetExpectedPosition(string accountName, int value)
        {
            // B966: No internal Enqueue. Callers already execute on the strategy thread.
            if (string.IsNullOrEmpty(accountName) || expectedPositions == null) return;
            expectedPositions[accountName] = value;
            if (value == 0)
                _dispatchSyncPendingExpKeys.TryRemove(accountName, out _); // [B967-FIX-02]
            // REAP-01: Stamp timestamp when a position is reserved so REAPER can apply
            // a grace window and avoid false "Critical Desync" during the broker-confirm lag.
            // Build 935 [REAPER-B935-002]: Also stamp per-account dictionary for scoped grace.
            if (value != 0)
            {
                Interlocked.Exchange(ref _lastExpectedPositionSetTicks, DateTime.UtcNow.Ticks);
                StampAccountFillGrace(accountName);
            }
        }

        // Build 930.1 [P1]: Delta rollback for cascade cancellations.
        // Subtracts or adds the cancelled entry's quantity to the signed total.
        // Preserves expected position for other active entries on the same account.
        private void DeltaExpectedPosition(string accountName, int delta)
        {
            // B966: No internal Enqueue. Callers already execute on the strategy thread.
            if (string.IsNullOrEmpty(accountName) || expectedPositions == null) return;
            int current;
            expectedPositions.TryGetValue(accountName, out current);
            int updated = current + delta;
            expectedPositions[accountName] = updated;
            Print(string.Format("[ACCOUNT_SYNC] {0} expected delta: {1} + ({2}) = {3}", accountName, current, delta, updated));
            if (delta != 0)
                Interlocked.Exchange(ref _lastExpectedPositionSetTicks, DateTime.UtcNow.Ticks);
        }

        private void MarkDispatchSyncPending(string expectedKey)
        {
            if (string.IsNullOrEmpty(expectedKey)) return;
            _dispatchSyncPendingExpKeys.TryAdd(expectedKey, 0); // [B967-FIX-02]
        }

        private void ClearDispatchSyncPending(string expectedKey)
        {
            if (string.IsNullOrEmpty(expectedKey)) return;
            _dispatchSyncPendingExpKeys.TryRemove(expectedKey, out _); // [B967-FIX-02]
        }

        private bool IsDispatchSyncPending(string expectedKey)
        {
            if (string.IsNullOrEmpty(expectedKey)) return false;
            return _dispatchSyncPendingExpKeys.ContainsKey(expectedKey); // [B967-FIX-02]
        }

        /// <summary>
        /// 1102Z-C [RR-2b]: Stamp _lastExpectedPositionSetTicks to open a fresh 5-second REAPER grace window.
        /// Call before any follower entry order mutation (Change or Cancel) during a price-move propagation.
        /// Does NOT mutate expectedPositions ??" position is already reserved; only the price is moving.
        /// Thread-safe: Interlocked.Exchange is lock-free.
        /// </summary>
        private void StampReaperMoveGrace()
        {
            Interlocked.Exchange(ref _lastExpectedPositionSetTicks, DateTime.UtcNow.Ticks);
        }

        // Build 1102U [BUG-1]: Composite key for expectedPositions.
        // Prevents cross-instrument state collision when multiple chart instances trade different
        // instruments (e.g. MES + MCL) on the same account fleet. REAPER was reading MCL's expected
        // quantity on the MES chart, triggering an infinite repair loop of rejected orders.
        // ALL expectedPositions reads and writes MUST use this helper instead of bare acct.Name.
        private string ExpKey(string acctName)
        {
            return acctName + "_" + Instrument.FullName;
        }

        /// <summary>
        /// V12 SIMA: Returns the list of Apex accounts sorted by Daily P/L (Lowest to Highest)
        /// </summary>
        private List<AccountRankInfo> GetSortedAccountFleet()
        {
            List<AccountRankInfo> fleet = new List<AccountRankInfo>();

            foreach (Account acct in Account.All)
            {
                if (IsFleetAccount(acct))
                {
                    double dailyPL = acct.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);
                    fleet.Add(new AccountRankInfo { Account = acct, DailyPL = dailyPL, Name = acct.Name });
                }
            }

            // Sort by P/L ascending (Lowest P/L first)
            return fleet.OrderBy(a => a.DailyPL).ToList();
        }

        private void SetRmaAnchorFromIpc(string anchorStr)
        {
            try
            {
                if (anchorStr == "EMA30") currentRmaAnchor = RmaAnchorType.Ema30;
                else if (anchorStr == "EMA65") currentRmaAnchor = RmaAnchorType.Ema65;
                else if (anchorStr == "EMA200") currentRmaAnchor = RmaAnchorType.Ema200;
                else if (anchorStr == "OR_HIGH") currentRmaAnchor = RmaAnchorType.OrHigh;
                else if (anchorStr == "OR_LOW") currentRmaAnchor = RmaAnchorType.OrLow;
                else if (anchorStr == "MANUAL") currentRmaAnchor = RmaAnchorType.Manual;

                Print("IPC SET ANCHOR: " + anchorStr);
            }
            catch (Exception ex)
            {
                Print("Error SetRmaAnchorFromIpc: " + ex.Message);
            }
        }

    }
}
