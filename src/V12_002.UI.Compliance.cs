// V12.44 MODULAR: Apex Compliance Hub Module (Split from UI.cs)
// Contains: Compliance tracking, daily summaries, account metrics, performance logging
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
        #region Apex Compliance Hub Logic (V12.1)

        #region Compliance Tracking

        private DateTime GetComplianceNow()
        {
            return ConvertToSelectedTimeZone(DateTime.Now);
        }

        private int GetTradingDayKey(DateTime timeInZone)
        {
            return timeInZone.Year * 10000 + timeInZone.Month * 100 + timeInZone.Day;
        }

        private void EnsureAccountComplianceTracking(string accountName, DateTime nowInZone)
        {
            if (string.IsNullOrEmpty(accountName)) return;
            accountDailyProfit.TryAdd(accountName, 0);
            accountTotalProfit.TryAdd(accountName, 0);
            accountTradeCount.TryAdd(accountName, 0);
            accountDailyTradeCount.TryAdd(accountName, 0);
            accountEquityPeak.TryAdd(accountName, 0);
            accountMaxDrawdown.TryAdd(accountName, 0);
            accountTradingDays.TryAdd(accountName, new ConcurrentDictionary<int, byte>());
            accountLastSummaryDate.TryAdd(accountName, nowInZone.Date);
        }

        private void TrackTradeEntry(Account acct, Execution execution)
        {
            if (acct == null || execution == null || execution.Order == null) return;
            if (execution.Order.OrderState != OrderState.Filled) return;

            OrderAction action = execution.Order.OrderAction;
            if (action != OrderAction.Buy && action != OrderAction.SellShort) return;

            if (EnableSIMA && !IsFleetAccount(acct)) return;

            DateTime nowInZone = GetComplianceNow();
            EnsureAccountComplianceTracking(acct.Name, nowInZone);

            accountTradeCount.AddOrUpdate(acct.Name, 1, (k, v) => v + 1);
            accountDailyTradeCount.AddOrUpdate(acct.Name, 1, (k, v) => v + 1);

            int dayKey = GetTradingDayKey(nowInZone);
            var days = accountTradingDays.GetOrAdd(acct.Name, _ => new ConcurrentDictionary<int, byte>());
            days.TryAdd(dayKey, 1);
        }

        private void UpdateEquityDrawdown(string accountName, double balance)
        {
            double peak = accountEquityPeak.AddOrUpdate(accountName, balance, (k, v) => Math.Max(v, balance));
            double drawdown = Math.Max(0, peak - balance);
            accountMaxDrawdown.AddOrUpdate(accountName, drawdown, (k, v) => Math.Max(v, drawdown));
        }

        private void UpdateAccountMetricsFromAccount(Account acct)
        {
            if (acct == null) return;
            if (EnableSIMA && !IsFleetAccount(acct)) return;

            DateTime nowInZone = GetComplianceNow();
            EnsureAccountComplianceTracking(acct.Name, nowInZone);

            double dailyPL = acct.Get(AccountItem.RealizedProfitLoss, Currency.UsDollar);
            accountDailyProfit[acct.Name] = dailyPL;

            double balance = acct.Get(AccountItem.CashValue, Currency.UsDollar);
            UpdateEquityDrawdown(acct.Name, balance);
        }

        private int GetUniqueTradingDays(string accountName)
        {
            if (accountTradingDays.TryGetValue(accountName, out var days))
                return days.Count;
            return 0;
        }

        #endregion

        #region CSV Reporting

        private void EnsureDailySummaryCsv()
        {
            if (string.IsNullOrEmpty(dailySummaryCsvPath)) return;

            if (!System.IO.File.Exists(dailySummaryCsvPath))
            {
                lock (dailySummaryLock)
                {
                    if (!System.IO.File.Exists(dailySummaryCsvPath))
                    {
                        string header = "Date,Account,DailyPL,DailyTrades,TotalProfit,TotalTrades,MaxDrawdown,UniqueDays";
                        System.IO.File.WriteAllText(dailySummaryCsvPath, header + Environment.NewLine);
                    }
                }
            }
        }

        private void AppendDailySummary(DateTime summaryDate, string accountName, double dailyPL, int dailyTrades,
            double totalProfit, int totalTrades, double maxDrawdown, int uniqueDays)
        {
            if (string.IsNullOrEmpty(dailySummaryCsvPath)) return;

            string safeName = (accountName ?? string.Empty).Replace("\"", "\"\"");
            string line = string.Format(CultureInfo.InvariantCulture,
                "{0},\"{1}\",{2:F2},{3},{4:F2},{5},{6:F2},{7}",
                summaryDate.ToString("yyyy-MM-dd"), safeName, dailyPL, dailyTrades, totalProfit, totalTrades, maxDrawdown, uniqueDays);

            // V12.40 FREEZE FIX: Ensure CSV header exists (fast, no I/O if already created)
            lock (dailySummaryLock)
            {
                EnsureDailySummaryCsv();
            }

            // V12.40 FREEZE FIX: Fire-and-forget async write -- never blocks UI thread
            string pathCopy = dailySummaryCsvPath;
            string lineCopy = line + Environment.NewLine;
            Task.Run(() =>
            {
                try { System.IO.File.AppendAllText(pathCopy, lineCopy); }
                catch { /* swallow -- daily summary is best-effort */ }
            });
        }

        private void FinalizeDailySummaryForAccount(string accountName, DateTime summaryDate)
        {
            if (string.IsNullOrEmpty(accountName)) return;

            double dailyPL = accountDailyProfit.TryGetValue(accountName, out var dp) ? dp : 0;
            int dailyTrades = accountDailyTradeCount.TryGetValue(accountName, out var dt) ? dt : 0;
            int totalTrades = accountTradeCount.TryGetValue(accountName, out var tt) ? tt : 0;
            double maxDrawdown = accountMaxDrawdown.TryGetValue(accountName, out var dd) ? dd : 0;
            int uniqueDays = GetUniqueTradingDays(accountName);

            double totalProfit = accountTotalProfit.AddOrUpdate(accountName, dailyPL, (k, v) => v + dailyPL);
            AppendDailySummary(summaryDate, accountName, dailyPL, dailyTrades, totalProfit, totalTrades, maxDrawdown, uniqueDays);
        }

        private void MaybeFinalizeDailySummaries(DateTime nowInZone, List<Account> accounts)
        {
            if (string.IsNullOrEmpty(dailySummaryCsvPath)) return;

            if ((nowInZone - lastDailySummaryCheck).TotalSeconds < 30) return;
            lastDailySummaryCheck = nowInZone;

            foreach (Account acct in accounts)
            {
                if (acct == null) continue;
                EnsureAccountComplianceTracking(acct.Name, nowInZone);

                DateTime lastDate = accountLastSummaryDate.GetOrAdd(acct.Name, nowInZone.Date);
                if (nowInZone.Date > lastDate.Date)
                {
                    FinalizeDailySummaryForAccount(acct.Name, lastDate);
                    accountDailyProfit[acct.Name] = 0;
                    accountDailyTradeCount[acct.Name] = 0;
                    accountLastSummaryDate[acct.Name] = nowInZone.Date;
                }
            }
        }

        private List<Account> GetComplianceAccounts()
        {
            List<Account> accounts = new List<Account>();

            if (EnableSIMA)
            {
                foreach (Account acct in Account.All)
                {
                    if (IsFleetAccount(acct))
                        accounts.Add(acct);
                }
            }
            else
            {
                if (Account != null)
                    accounts.Add(Account);
            }

            return accounts;
        }

        #endregion

        #region Snapshot & Enforcement
        /// <summary>
        /// V12.Phase7 [C-09]: Compliance enforcement gate.
        /// Returns false if the account has breached any hard compliance limit (severity 2).
        /// Call this at the START of every entry method -- if false, abort and do not submit orders.
        /// Severity levels: 0 = OK, 1 = warning, 2 = hard block (drawdown breached or flat rule).
        /// </summary>
        private bool IsOrderAllowed(string accountName = null)
        {
            if (!EnableComplianceHub) return true;

            string acctName = accountName ?? Account?.Name;
            if (string.IsNullOrEmpty(acctName)) return true;

            // Hard-block: trailing drawdown breached
            if (accountEquityPeak.TryGetValue(acctName, out double peak) && peak > 0 && TrailingDrawdownLimit > 0)
            {
                double balance = 0;
                Account currentAccount = this.Account;
                if (currentAccount != null)
                {
                    try { balance = currentAccount.Get(NinjaTrader.Cbi.AccountItem.CashValue, NinjaTrader.Cbi.Currency.UsDollar); } catch { }
                }
                double buffer = balance - (peak - TrailingDrawdownLimit);
                if (buffer <= 0)
                {
                    Print(string.Format("[COMPLIANCE BLOCKED] Entry suppressed for {0}: Trailing drawdown breached. Buffer=${1:F2}", acctName, buffer));
                    return false;
                }
            }

            // Hard-block: daily profit cap reached (for SIMA fleet accounts)
            if (EnableSIMA && EnableConsistencyLock)
            {
                if (accountDailyProfit.TryGetValue(acctName, out double dp) && MaxDailyProfitCap > 0 && dp >= MaxDailyProfitCap)
                {
                    Print(string.Format("[COMPLIANCE BLOCKED] Entry suppressed for {0}: Daily profit cap hit. DayPL=${1:F2}", acctName, dp));
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Triggered when ANY of the 20 Apex accounts has an execution (entry or exit).
        /// V12.Phase6 [CONCURRENCY-01]: This fires on the BROKER THREAD. We enqueue and marshal
        /// to the strategy thread via TriggerCustomEvent to avoid cross-thread mutation of
        /// strategy state (entryOrders, activePositions, compliance counters).
        /// </summary>
        #endregion

        #region Execution Callbacks

        private void OnAccountExecutionUpdate(object sender, ExecutionEventArgs e)
        {
            if (e == null) return;

            // V12.1101E [TM-02]: Broker-thread callback only enqueues work; state mutation stays on strategy thread.
            Account execAccount = sender as Account;
            _accountExecutionQueue.Enqueue(new QueuedAccountExecution { Account = execAccount, EventArgs = e });
            ScheduleAccountExecutionQueuePump();

        }

        // [BUILD 948] Cap per-invocation drain to prevent strategy-thread starvation during broker replay bursts.
        private const int MaxAccountExecutionsPerDrain = 16;

        private void HoldAccountExecutionQueueUntilFlattenEnds()
        {
            Interlocked.Exchange(ref _accountExecutionPumpDeferredWhileFlatten, 1);
            Interlocked.Exchange(ref _accountExecutionPumpScheduled, 1);
        }

        private void ScheduleAccountExecutionQueuePump()
        {
            if (isFlattenRunning)
            {
                HoldAccountExecutionQueueUntilFlattenEnds();
                return;
            }

            if (Interlocked.CompareExchange(ref _accountExecutionPumpScheduled, 1, 0) != 0)
                return;

            try { TriggerCustomEvent(o => ProcessAccountExecutionQueue(), null); }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref _accountExecutionPumpScheduled, 0);
                Print("[ACCOUNT_EXEC_PUMP] schedule failed: " + ex.Message);
            }
        }

        private void ResumeAccountExecutionQueuePump()
        {
            if (isFlattenRunning)
                return;

            bool deferred = Interlocked.Exchange(ref _accountExecutionPumpDeferredWhileFlatten, 0) != 0;
            if (!deferred && _accountExecutionQueue.IsEmpty)
                return;

            Interlocked.Exchange(ref _accountExecutionPumpScheduled, 0);
            ScheduleAccountExecutionQueuePump();
        }

        /// <summary>
        /// V12.Phase6 [CONCURRENCY-01]: Processes queued account execution events on the STRATEGY THREAD.
        /// [BUILD 948] Drain is capped at MaxAccountExecutionsPerDrain per invocation; remaining items
        /// are rescheduled via TriggerCustomEvent to yield the strategy thread between bursts.
        /// </summary>
        private void ProcessAccountExecutionQueue()
        {
            // V12.1101E [PH5-THREAD-01]: Buffer-and-wait during flatten.
            // Keep queued executions intact and retry when flatten releases.
            if (isFlattenRunning)
            {
                HoldAccountExecutionQueueUntilFlattenEnds();
                return;
            }

            Interlocked.Exchange(ref _accountExecutionPumpScheduled, 0);
            int drained = 0;
            QueuedAccountExecution item;
            while (drained < MaxAccountExecutionsPerDrain && _accountExecutionQueue.TryDequeue(out item))
            {
                drained++;
                if (isFlattenRunning)
                {
                    _accountExecutionQueue.Enqueue(item);
                    HoldAccountExecutionQueueUntilFlattenEnds();
                    return;
                }
                ProcessQueuedExecution(item);
            }

            // [BUILD 948] Reschedule if items remain after hitting the drain cap
            if (!_accountExecutionQueue.IsEmpty)
                ScheduleAccountExecutionQueuePump();

            // Update the compliance log once after draining all queued events
            if (EnableComplianceHub && !isFlattenRunning)
                LogApexPerformance();
        }

        /// <summary>
        /// Processes a single dequeued account execution event on the strategy thread.
        /// Handles compliance tracking, fleet bracket submission (V12.7), and
        /// flat-clear sync [H-15] with Persistence Gate [1102Y-V4].
        /// </summary>
        private void ProcessQueuedExecution(QueuedAccountExecution item)
        {
            if (EnableComplianceHub)
                Print(string.Format("[COMPLIANCE] Execution Update received for account."));

            if (EnableComplianceHub && item.Account != null)
            {
                TrackTradeEntry(item.Account, item.EventArgs.Execution);
                UpdateAccountMetricsFromAccount(item.Account);
            }

            // V12.7: Check if this fill is for a fleet entry with deferred brackets
            try
            {
                Order filledOrder = item.EventArgs.Execution?.Order;
                if (filledOrder != null && filledOrder.OrderState == OrderState.Filled)
                {
                    foreach (var kvp in entryOrders.ToArray())
                    {
                        if (kvp.Value == filledOrder)
                        {
                            string fleetKey = kvp.Key;
                            if (activePositions.TryGetValue(fleetKey, out var pos) && pos.IsFollower && !pos.EntryFilled)
                            {
                                double fleetFillPrice = item.EventArgs.Execution != null ? item.EventArgs.Execution.Price : 0;
                                SymmetryGuardOnFollowerFill(fleetKey, pos, fleetFillPrice);
                            }
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Print(string.Format("[SIMA V12.7] Error in fleet bracket submission: {0}", ex.Message));
            }

            // EMERGENCY FIX [H-15]: After any fleet execution, check if the account is now flat.
            // Syncs expectedPositions when position is closed externally (e.g., manual UI flatten).
            // [1102Y-V4 PERSISTENCE GATE]: Skip flat-clear for entry fills. The broker Positions
            // collection may not yet reflect the new position at this point in the callback,
            // producing a stale-flat read that wipes expectedPositions during fill registration.
            // Only exit fills (Sell / BuyToCover) are safe to use as flat-check triggers.
            try
            {
                Account fleetAcct = item.Account;
                if (fleetAcct != null && expectedPositions != null && expectedPositions.ContainsKey(ExpKey(fleetAcct.Name)))
                {
                    Order execOrder = item.EventArgs?.Execution?.Order;
                    bool isEntryFill = execOrder != null &&
                        (execOrder.OrderAction == OrderAction.Buy || execOrder.OrderAction == OrderAction.SellShort);
                    if (isEntryFill)
                    {
                        Print(string.Format("[ProcessQueuedExecution] [1102Y-V4] Entry fill for {0} -- Persistence Gate active, flat-check skipped.", fleetAcct.Name));
                    }
                    else
                    {
                        var brokerPos = fleetAcct.Positions.FirstOrDefault(p => p.Instrument.FullName == Instrument.FullName);
                        bool nowFlat = (brokerPos == null || brokerPos.MarketPosition == MarketPosition.Flat);
                        if (nowFlat && !IsDispatchSyncPending(ExpKey(fleetAcct.Name)))
                        {
                            SetExpectedPosition(ExpKey(fleetAcct.Name), 0);
                            Print(string.Format("[ProcessQueuedExecution] Fleet {0} is Flat -- expectedPositions cleared for {1}",
                                fleetAcct.Name, Instrument.FullName));
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Writes current account health to a JSON file for the WPF Remote App to read
        /// </summary>
        #endregion

        #region Performance Logging

        private void LogApexPerformance()
        {
            if (!EnableComplianceHub || string.IsNullOrEmpty(complianceLogPath)) return;

            // Throttle logging to once per 5 seconds to prevent disk thrashing during heavy fills
            if ((DateTime.Now - lastComplianceLog).TotalSeconds < 5) return;

            try
            {
                StringBuilder sbCompliance = new StringBuilder();
                sbCompliance.AppendLine("{");
                sbCompliance.AppendLine("  \"Timestamp\": \"" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\",");
                sbCompliance.AppendLine("  \"Instrument\": \"" + Instrument.FullName + "\",");
                sbCompliance.AppendLine("  \"Accounts\": [");

                List<Account> accounts = GetComplianceAccounts();
                DateTime nowInZone = GetComplianceNow();
                MaybeFinalizeDailySummaries(nowInZone, accounts);

                int count = 0;
                foreach (Account acct in accounts)
                {
                    if (acct == null) continue;

                    if (count > 0) sbCompliance.Append(",\n");

                    UpdateAccountMetricsFromAccount(acct);

                    // Basic metrics from NinjaTrader Account object
                    double balance = acct.Get(AccountItem.CashValue, Currency.UsDollar);
                    double dailyPL = accountDailyProfit.TryGetValue(acct.Name, out var dp) ? dp : 0;
                    double totalProfit = accountTotalProfit.GetOrAdd(acct.Name, 0) + dailyPL;
                    int tradeCount = accountTradeCount.TryGetValue(acct.Name, out var tc) ? tc : 0;
                    int uniqueDays = GetUniqueTradingDays(acct.Name);
                    double maxDrawdown = accountMaxDrawdown.TryGetValue(acct.Name, out var dd) ? dd : 0;

                    sbCompliance.AppendLine("    {");
                    sbCompliance.AppendLine("      \"Name\": \"" + acct.Name + "\",");
                    
                    var brokerPos = acct.Positions.FirstOrDefault(p => p.Instrument.FullName == Instrument.FullName);
                    int actualQty = (brokerPos != null && brokerPos.MarketPosition != MarketPosition.Flat)
                        ? (brokerPos.MarketPosition == MarketPosition.Long ? brokerPos.Quantity : -brokerPos.Quantity) : 0;
                    int expectedQty = 0;
                    if (expectedPositions != null) expectedPositions.TryGetValue(ExpKey(acct.Name), out expectedQty);

                    sbCompliance.AppendLine("      \"ActualQty\": " + actualQty + ",");
                    sbCompliance.AppendLine("      \"ExpectedQty\": " + expectedQty + ",");
                    sbCompliance.AppendLine("      \"Balance\": " + balance.ToString("F2") + ",");
                    sbCompliance.AppendLine("      \"DailyPL\": " + dailyPL.ToString("F2") + ",");
                    sbCompliance.AppendLine("      \"TotalProfit\": " + totalProfit.ToString("F2") + ",");
                    sbCompliance.AppendLine("      \"TradeCount\": " + tradeCount + ",");
                    sbCompliance.AppendLine("      \"UniqueDays\": " + uniqueDays + ",");
                    sbCompliance.AppendLine("      \"MaxDrawdown\": " + maxDrawdown.ToString("F2") + ",");
                    bool isConnected = acct.Connection?.Status == ConnectionStatus.Connected;
                    sbCompliance.AppendLine("      \"Connection\": \"" + (isConnected ? "Connected" : "Disconnected") + "\"");
                    sbCompliance.Append("    }");
                    count++;
                }

                sbCompliance.AppendLine("\n  ]");
                sbCompliance.AppendLine("}");

                // V12.40 FREEZE FIX: Fire-and-forget async write -- never blocks UI thread
                string jsonPayload = sbCompliance.ToString();
                string path = complianceLogPath;
                lastComplianceLog = DateTime.Now;
                Task.Run(() =>
                {
                    try { if (path != null) System.IO.File.WriteAllText(path, jsonPayload); }
                    catch { /* swallow -- compliance log is best-effort */ }
                });
            }
            catch (Exception ex)
            {
                Print("[COMPLIANCE] ERROR writing log: " + ex.Message);
            }
        }

        #endregion

        #endregion
    }
}
