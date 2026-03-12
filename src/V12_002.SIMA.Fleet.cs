// Build 971: SIMA Fleet -- PumpFleetDispatch, ShouldSkipFleetAccount, UnsubscribeFromFleetAccounts
// V12 SIMA Module (Extracted)
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
        #region V12 SIMA Fleet

        /// <summary>
        /// Build 936 [FIX-1]: Processes ONE pending fleet dispatch request per invocation.
        /// Called via TriggerCustomEvent -- always runs on the strategy thread (NT8 thread-safe).
        /// Separates acct.Submit() calls across strategy-thread cycles, eliminating the synchronous
        /// 7-second freeze caused by submitting the full fleet in one tight loop.
        /// Error handling mirrors ExecuteSmartDispatchEntry catch block: dict cleanup + delta rollback.
        /// </summary>
        private void PumpFleetDispatch()
        {
            // A3-1: Abort and drain queue if SIMA is disabled or flatten is running (Build 960 audit fix)
            if (isFlattenRunning || !EnableSIMA)
            {
                // B957/F1: Rollback ReservedDelta and clear dispatch-sync barrier for each discarded request.
                FleetDispatchRequest stale;
                while (_pendingFleetDispatches.TryDequeue(out stale))
                {
                    if (stale.ReservedDelta != 0)
                        AddExpectedPositionDelta(stale.ExpectedKey, -stale.ReservedDelta);
                    ClearDispatchSyncPending(stale.ExpectedKey);
                }
                Print("[PUMP] Abort: SIMA inactive or flatten running. Queue drained with delta rollback.");
                return;
            }

            if (!_pendingFleetDispatches.TryDequeue(out var req))
                return;

            bool syncCleared = false;
            try
            {
                req.Account.Submit(req.Orders);
                ClearDispatchSyncPending(req.ExpectedKey);
                syncCleared = true;
                Print(string.Format("[PUMP] Submitted {0} orders for {1} | {2}",
                    req.Orders.Length, req.FleetEntryName, req.Account.Name));
            }
            catch (Exception ex)
            {
                Print(string.Format("[PUMP] Submit FAILED for {0} ({1}): {2}",
                    req.FleetEntryName, req.Account.Name, ex.Message));
                if (!syncCleared)
                    ClearDispatchSyncPending(req.ExpectedKey);
                if (req.ReservedDelta != 0)
                    AddExpectedPositionDelta(req.ExpectedKey, -req.ReservedDelta);
                // Full tracking-dict cleanup -- mirrors ExecuteSmartDispatchEntry [F-01] catch block.
                activePositions.TryRemove(req.FleetEntryName, out _);
                entryOrders.TryRemove(req.FleetEntryName, out _);
                stopOrders.TryRemove(req.FleetEntryName, out _);
                for (int tNum = 1; tNum <= 5; tNum++)
                {
                    var targetDict = GetTargetOrdersDictionary(tNum);
                    if (targetDict != null)
                        targetDict.TryRemove(req.FleetEntryName, out _);
                }
            }
            finally
            {
                Interlocked.Decrement(ref _pendingFleetDispatchCount);
                // Chain next pump cycle if more requests remain in the queue.
                if (!_pendingFleetDispatches.IsEmpty)
                    try { TriggerCustomEvent(o => PumpFleetDispatch(), null); } catch { }
            }
        }

        // Build 935 [SIMA-B935-001]: Skip-logic extracted from ExecuteSmartDispatchEntry fleet loop.
        // Returns true if the account should be skipped for this dispatch cycle.
        // Threading: strategy thread only. stateLock usage identical to original inline code.
        private bool ShouldSkipFleetAccount(Account acct, AccountRankInfo rankInfo,
            System.Collections.Generic.HashSet<string> activeAccountSnapshot, System.Text.StringBuilder dispatchLog)
        {
            // Step 1: Inactive check -- prevents UI toggle race.
            if (!activeAccountSnapshot.Contains(acct.Name))
            {
                dispatchLog.AppendLine(string.Format("[SIMA] {0} SKIPPED (Inactive)", acct.Name));
                return true;
            }

            // Step 2: H-13 stale expectedPositions reconciliation.
            try
            {
                // [939-P0]: Snapshot Positions to prevent broker-thread mutation during iteration.
                var brokerPos = acct.Positions.ToArray().FirstOrDefault(p => p.Instrument.FullName == Instrument.FullName);
                bool brokerFlat = (brokerPos == null || brokerPos.MarketPosition == MarketPosition.Flat);
                int expected;
                expectedPositions.TryGetValue(ExpKey(acct.Name), out expected);

                if (brokerFlat && Math.Abs(expected) > 0)
                {
                    bool hasPendingRepairOrder = false;
                    foreach (var kvp in entryOrders.ToArray())
                    {
                        var ord = kvp.Value;
                        if (ord != null && !IsOrderTerminal(ord.OrderState)
                            && activePositions.TryGetValue(kvp.Key, out var pos)
                            && pos.IsFollower && pos.ExecutingAccount != null
                            && pos.ExecutingAccount.Name == acct.Name)
                        { hasPendingRepairOrder = true; break; }
                    }

                    bool hasActivePositionForAcct = activePositions.Values.Any(
                        p => p.IsFollower && p.ExecutingAccount != null && p.ExecutingAccount.Name == acct.Name);

                    bool isMasterWaiting = false;
                    foreach (var kvp in entryOrders.ToArray())
                    {
                        if (activePositions.TryGetValue(kvp.Key, out var pi) && !pi.IsFollower && pi.ExecutingAccount == this.Account
                            && kvp.Value != null && (kvp.Value.OrderState == OrderState.Working
                                || kvp.Value.OrderState == OrderState.Submitted || kvp.Value.OrderState == OrderState.Accepted))
                        { isMasterWaiting = true; break; }
                    }

                    if (hasPendingRepairOrder || hasActivePositionForAcct || isMasterWaiting)
                        dispatchLog.AppendLine(string.Format("[DISPATCH] H-13 SKIP: {0} Flat but {1} -- not resetting",
                            acct.Name, isMasterWaiting ? "Master working" : (hasPendingRepairOrder ? "repair in-flight" : "activePos present")));
                    else
                    {
                        { var _acct966h13 = ExpKey(acct.Name); Enqueue(ctx => ctx.SetExpectedPosition(_acct966h13, 0)); }
                        dispatchLog.AppendLine(string.Format("[DISPATCH] H-13: Stale expectedPos cleared for {0} (broker Flat)", acct.Name));
                    }
                }
            }
            catch { }

            // Step 3: Consistency Lock -- skip if daily P&L cap hit.
            if (EnableConsistencyLock && rankInfo.DailyPL >= MaxDailyProfitCap)
            {
                dispatchLog.AppendLine(string.Format("[DISPATCH] {0} SKIPPED - Consistency Lock ({1:C})", acct.Name, rankInfo.DailyPL));
                return true;
            }

            return false;
        }


        /// <summary>
        /// V12.1101E [A-4]: Idempotent unsubscribe ??" removes all SIMA event handlers before
        /// re-subscribing. Prevents handler accumulation on repeated SIMA toggle cycles.
        /// V12.Phase6 [UNSUB-TRACK]: Deterministic unsubscribe ??" uses tracked set of subscribed accounts
        /// instead of re-scanning Account.All, which may have changed since subscribe time.
        /// </summary>
        private void UnsubscribeFromFleetAccounts()
        {
            // First: unsubscribe from tracked set (deterministic ??" guaranteed to match subscribe)
            foreach (string acctName in _subscribedAccountNames)
            {
                foreach (Account acct in Account.All)
                {
                    if (acct.Name == acctName)
                    {
                        acct.ExecutionUpdate -= OnAccountExecutionUpdate;
                        acct.OrderUpdate     -= OnAccountOrderUpdate;
                        break;
                    }
                }
            }
            // Fallback: also sweep Account.All for any handlers from untracked subscribe paths
            foreach (Account acct in Account.All)
            {
                if (IsFleetAccount(acct))
                {
                    acct.ExecutionUpdate -= OnAccountExecutionUpdate;
                    acct.OrderUpdate     -= OnAccountOrderUpdate;
                }
            }
            _subscribedAccountNames.Clear();
        }


        #endregion
    }
}
