// Build 971: SIMA Lifecycle -- ApplySimaState, EnumerateApexAccounts, Hydrate*, CancelAll*, Sweep*
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
        #region V12 SIMA Lifecycle

        private void ProcessApplySimaState(bool enabled)
        {
            // V12.Audit [H-10]: If a previous toggle timed out, attempt retry now.
            // We re-enter with the same `enabled` argument that was pending.
            // If the semaphore is still held this call will time out again, setting the flag once more.
            if (_simaTogglePending)
                Print("[SIMA LIFECYCLE] Retrying previously timed-out toggle (pending retry flag was set).");

            // Measure lifecycle semaphore contention because this wait runs on the actor path
            // and can stall queue drain when SIMA toggles overlap with other work.
            Stopwatch waitTimer = Stopwatch.StartNew();
            // V12.Phase7 [H-10]: Serialize enable/disable transitions to prevent race between
            // concurrent IPC commands and UI toggles leaving SIMA in a partially initialized state.
            if (!_simaToggleSem.Wait(500))
            {
                waitTimer.Stop();
                // V12.Audit [H-10]: Record that this toggle did not complete so the next caller can retry.
                _simaTogglePending = true;
                Print(string.Format("[SIMA_WARN] ApplySimaState timed out waiting for semaphore after {0:F1}ms -- toggle pending, retry.", waitTimer.Elapsed.TotalMilliseconds));
                return;
            }
            try
            {
                waitTimer.Stop();
                if (waitTimer.Elapsed.TotalMilliseconds >= 25.0)
                    Print(string.Format("[LATENCY] [SIMA LIFECYCLE] Toggle semaphore wait: {0:F1}ms", waitTimer.Elapsed.TotalMilliseconds));

                if (enabled)
                    ProcessInitializeSIMA();
                else
                    ProcessShutdownSIMA();

                EnableSIMA = enabled;
                // V12.Audit [H-10]: Toggle completed successfully ?? clear any pending-retry flag.
                _simaTogglePending = false;
            }
            finally
            {
                _simaToggleSem.Release();
            }
        }

        private void ProcessInitializeSIMA()
        {
            EnumerateApexAccounts(); // Unsubs first (idempotent), then re-subscribes + hydrates
            if (ReaperAuditEnabled)
                StartReaperAudit();
            Print("[SIMA LIFECYCLE] SIMA ENABLED -- fleet enumerated, Reaper started");
        }

        private void ProcessShutdownSIMA()
        {
            CancelAllV12GtcOrders(false); // [BUILD 948] GTC sweep before teardown -- skip accounts with open positions
            StopReaperAudit();
            UnsubscribeFromFleetAccounts();
            // A3-1: Drain ghost dispatch queue on SIMA disable (Build 960 audit fix)
            // B957/F2: Rollback ReservedDelta and clear dispatch-sync barrier for each discarded request.
            {
                FleetDispatchRequest ignored;
                while (_pendingFleetDispatches.TryDequeue(out ignored))
                {
                    if (ignored.ReservedDelta != 0)
                        AddExpectedPositionDelta(ignored.ExpectedKey, -ignored.ReservedDelta);
                    ClearDispatchSyncPending(ignored.ExpectedKey);
                }
                Print("[SIMA] Dispatch queue cleared on shutdown with delta rollback.");
            }
            Print("[SIMA LIFECYCLE] SIMA DISABLED -- Reaper stopped, handlers unsubscribed");
        }

        private void EnumerateApexAccounts()
        {
            UnsubscribeFromFleetAccounts(); // V12.1101E [A-4]: Always unsub first ?? idempotent guard against handler accumulation
            simaAccountCount = 0;
            Print("[SIMA] ===================================================");
            Print("[SIMA] V12.12 - Fleet Symmetry & Safety Hardening Initializing");
            Print($"[SIMA] Account Prefix Filter: \"{AccountPrefix}\"");
            Print("[SIMA] ---------------------------------------------------");

            foreach (Account acct in Account.All)
            {
                if (IsFleetAccount(acct))
                {
                    simaAccountCount++;
                    { var _acct966init = ExpKey(acct.Name); SetExpectedPosition(_acct966init, 0); } // Initialize expected position as flat
                    accountDailyProfit[acct.Name] = 0; // Initialize daily profit
                    EnsureAccountComplianceTracking(acct.Name, GetComplianceNow());
                    activeFleetAccounts[acct.Name] = false; // V12.8 SIMA: Default to INACTIVE ?? wait for Fleet Manager / IPC to enable

                    // V12.7: Always subscribe to execution updates for fleet bracket management
                    // (Also used by ComplianceHub for P/L tracking)
                    acct.ExecutionUpdate += OnAccountExecutionUpdate;
                    acct.OrderUpdate += OnAccountOrderUpdate;
                    _subscribedAccountNames.Add(acct.Name); // V12.Phase6 [UNSUB-TRACK]: Track for deterministic unsubscribe
                    if (EnableComplianceHub)
                    {
                        Print($"[SIMA] [OK] {acct.Name} | COMPLIANCE MONITORING ACTIVE");
                    }
                    else
                    {
                        Print($"[SIMA] #{simaAccountCount}: {acct.Name} | Connected: {acct.Connection?.Status == ConnectionStatus.Connected} | Fleet: INACTIVE (awaiting IPC enable)");
                    }
                }
            }

            Print("[SIMA] ---------------------------------------------------");
            Print($"[SIMA] TOTAL ACCOUNTS DETECTED: {simaAccountCount} | ALL INACTIVE by default");
            Print("[SIMA] FLEET INACTIVE - MANUAL ENABLE REQUIRED"); // V12.Phase10 [DEFAULT-FIX]
            Print("[SIMA] ===================================================");

            // V12.Phase6 [HYDRATE]: Seed expectedPositions from live broker state
            HydrateExpectedPositionsFromBroker();

            // [BUILD 948] Adopt any working broker orders into tracking dicts; sets _orderAdoptionComplete = true
            HydrateWorkingOrdersFromBroker();
        }

        /// <summary>
        /// V12.Phase6 [HYDRATE]: Reads actual broker positions for each fleet account and seeds
        /// expectedPositions accordingly. Prevents false Reaper CRITICAL DESYNC alerts when the
        /// strategy restarts while accounts hold open positions.
        /// </summary>
        private void HydrateExpectedPositionsFromBroker()
        {
            int hydratedCount = 0;
            foreach (Account acct in Account.All)
            {
                if (!IsFleetAccount(acct)) continue;

                try
                {
                    // [939-P0]: Snapshot Positions to prevent broker-thread mutation during iteration.
                    foreach (Position pos in acct.Positions.ToArray())
                    {
                        if (pos != null && pos.Instrument != null
                            && pos.Instrument.FullName == Instrument.FullName
                            && pos.MarketPosition != MarketPosition.Flat)
                        {
                            int qty = pos.MarketPosition == MarketPosition.Long ? pos.Quantity : -pos.Quantity;
                            // Build 980 [Nexus]: Route expected position seed through the Actor queue
                            var capturedAcct = acct.Name;
                            var capturedQty = qty;
                            Enqueue(ctx => ctx.AddOrUpdateExpectedPosition(ExpKey(capturedAcct), capturedQty, v => capturedQty));
                            Print($"[SIMA HYDRATE] {acct.Name}: Seeded expected={qty} from broker ({pos.MarketPosition} {pos.Quantity})");
                            hydratedCount++;
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Print($"[SIMA HYDRATE] WARNING: Could not read positions for {acct.Name}: {ex.Message}");
                }
            }
            if (hydratedCount > 0)
                Print($"[SIMA HYDRATE] Hydrated {hydratedCount} account(s) with live broker positions");
        }

        /// <summary>
        /// Build 948 [FIX-B]: Re-adopt working broker orders into tracking dicts after restart or reconnect.
        /// Derives the original entry key by stripping the well-known order-name prefix (e.g. "Stop_" -> stopOrders).
        /// Sets _orderAdoptionComplete = true when done so REAPER can resume auditing.
        /// MUST be called on the strategy thread (via TriggerCustomEvent when initiated from a callback).
        /// Actor-serialized lifecycle and reconnect paths update tracking dicts on the Ordered Actor Thread.
        /// </summary>
        private void HydrateWorkingOrdersFromBroker()
        {
            int adoptedCount = 0;

            foreach (Account acct in Account.All)
            {
                if (!IsFleetAccount(acct)) continue;
                try
                {
                    foreach (Order ord in acct.Orders.ToArray())
                    {
                        if (ord.Instrument?.FullName != Instrument?.FullName) continue;
                        // [Codex P2] Include all live in-flight states -- Submitted/ChangePending/ChangeSubmitted
                        // can be active during an in-flight FSM replace at reconnect time.
                        // Setting _orderAdoptionComplete=true while these are skipped leaves REAPER
                        // auditing against incomplete order tracking and can fire false repair cycles.
                        if (ord.OrderState != OrderState.Working    &&
                            ord.OrderState != OrderState.Accepted   &&
                            ord.OrderState != OrderState.Submitted  &&
                            ord.OrderState != OrderState.ChangePending &&
                            ord.OrderState != OrderState.ChangeSubmitted) continue;

                        string name = ord.Name ?? string.Empty;
                        ConcurrentDictionary<string, Order> targetDict = null;
                        string key     = null;
                        string dictName = null;

                        if (name.StartsWith("Stop_", StringComparison.OrdinalIgnoreCase))
                        { targetDict = stopOrders;   key = name.Substring(5); dictName = "stopOrders"; }
                        else if (name.StartsWith("S_", StringComparison.OrdinalIgnoreCase))
                        { targetDict = stopOrders;   key = name.Substring(2); dictName = "stopOrders"; }
                        else if (name.StartsWith("T1_", StringComparison.OrdinalIgnoreCase))
                        { targetDict = target1Orders; key = name.Substring(3); dictName = "target1Orders"; }
                        else if (name.StartsWith("T2_", StringComparison.OrdinalIgnoreCase))
                        { targetDict = target2Orders; key = name.Substring(3); dictName = "target2Orders"; }
                        else if (name.StartsWith("T3_", StringComparison.OrdinalIgnoreCase))
                        { targetDict = target3Orders; key = name.Substring(3); dictName = "target3Orders"; }
                        else if (name.StartsWith("T4_", StringComparison.OrdinalIgnoreCase))
                        { targetDict = target4Orders; key = name.Substring(3); dictName = "target4Orders"; }
                        else if (name.StartsWith("T5_", StringComparison.OrdinalIgnoreCase))
                        { targetDict = target5Orders; key = name.Substring(3); dictName = "target5Orders"; }
                        // [Codex P1] Adopt Fleet_ prefixed follower entry orders into entryOrders.
                        // Without this, broker-resident follower entries are invisible after reconnect.
                        // ProcessQueuedExecution finds them by object ref in entryOrders, so a missed
                        // adoption means SymmetryGuardOnFollowerFill is bypassed and the new filled
                        // position launches without its protective bracket orders.
                        else if (name.StartsWith("Fleet_", StringComparison.OrdinalIgnoreCase))
                        { targetDict = entryOrders; key = name; dictName = "entryOrders"; }

                        if (targetDict == null || key == null) continue;

                        targetDict[key] = ord;

                        // [Build 980 Nexus] Rebuild activePositions structs so Rehydration does not lead to divergent REAPER audits.
                        if (targetDict == entryOrders && !activePositions.ContainsKey(key))
                        {
                            MarketPosition mp = (ord.OrderAction == OrderAction.Buy || ord.OrderAction == OrderAction.BuyToCover) ? MarketPosition.Long : MarketPosition.Short;
                            double ePrice = ord.LimitPrice != 0 ? ord.LimitPrice : (ord.StopPrice != 0 ? ord.StopPrice : ord.AverageFillPrice);
                            
                            var pos = new PositionInfo
                            {
                                SignalName = key,
                                Direction = mp,
                                TotalContracts = ord.Quantity,
                                RemainingContracts = ord.Quantity,
                                EntryPrice = ePrice,
                                InitialStopPrice = 0,
                                CurrentStopPrice = 0,
                                EntryOrderType = ord.OrderType,
                                EntryFilled = false,
                                IsFollower = key.StartsWith("Fleet_", StringComparison.OrdinalIgnoreCase),
                                ExecutingAccount = acct,
                                BracketSubmitted = false,
                                ExtremePriceSinceEntry = ePrice,
                                CurrentTrailLevel = 0,
                                OcoGroupId = "V12_" + GetStableHash(key)
                            };
                            
                            // Get standard distribution
                            int t1Qty, t2Qty, t3Qty, t4Qty, t5Qty;
                            GetTargetDistribution(ord.Quantity, out t1Qty, out t2Qty, out t3Qty, out t4Qty, out t5Qty);
                            pos.T1Contracts = t1Qty;
                            pos.T2Contracts = t2Qty;
                            pos.T3Contracts = t3Qty;
                            pos.T4Contracts = t4Qty;
                            pos.T5Contracts = t5Qty;
                            
                            // [Build 980 Phase 3]: Reconstruct trade DNA from signal name -- lost across restart.
                            // Fleet entry names follow pattern: Fleet_<AcctName>_<TradeType>_<index>
                            pos.IsMOMOTrade = key.IndexOf("_MOMO_", StringComparison.OrdinalIgnoreCase) >= 0;
                            pos.IsRMATrade = key.IndexOf("_RMA_", StringComparison.OrdinalIgnoreCase) >= 0
                                || key.IndexOf("_TREND_RMA_", StringComparison.OrdinalIgnoreCase) >= 0;
                            pos.IsTRENDTrade = key.IndexOf("_TREND_", StringComparison.OrdinalIgnoreCase) >= 0;
                            pos.IsRetestTrade = key.IndexOf("_RETEST_", StringComparison.OrdinalIgnoreCase) >= 0;
                            if (pos.IsMOMOTrade) pos.IsRMATrade = false; // MOMO overrides generic RMA flag

                            activePositions[key] = pos;
                            Print(string.Format("[SIMA HYDRATE] Rebuilt activePositions struct for {0} | DNA: IsMOMO={1} IsRMA={2} IsTREND={3} IsRetest={4}",
                                key, pos.IsMOMOTrade, pos.IsRMATrade, pos.IsTRENDTrade, pos.IsRetestTrade));
                        }
                        else
                        {
                            // [Build 980 Phase 3]: Force-sync TotalContracts and ExecutingAccount if struct already exists.
                            PositionInfo existingPos;
                            if (activePositions.TryGetValue(key, out existingPos))
                            {
                                existingPos.TotalContracts = ord.Quantity;
                                existingPos.ExecutingAccount = acct;
                                Print(string.Format("[SIMA HYDRATE] Force-synced TotalContracts={0} ExecutingAccount={1} for {2}",
                                    ord.Quantity, acct.Name, key));
                            }
                        }

                        Print(string.Format("[SIMA HYDRATE] Adopted working order {0} into {1}", name, dictName));
                        adoptedCount++;
                    }
                }
                catch (Exception ex)
                {
                    Print(string.Format("[SIMA HYDRATE] WARNING: Could not read orders for {0}: {1}", acct.Name, ex.Message));
                }
            }

            _orderAdoptionComplete = true;
            if (adoptedCount > 0)
                Print(string.Format("[SIMA HYDRATE] Adopted {0} working order(s) from broker -- adoption complete.", adoptedCount));
            else
                Print("[SIMA HYDRATE] No working orders to adopt -- adoption complete.");
        }

        /// <summary>
        /// Build 948 [FIX-A]: Sweep and cancel all V12-managed GTC orders before SIMA disable or strategy terminate.
        /// Phase 1 scans tracked order dicts; Phase 2 scans broker order lists for any V12-prefixed orders.
        /// force=true: cancel regardless of open positions (strategy terminate).
        /// force=false: skip accounts that have an open position for this instrument (SIMA disable -- prevent naked accounts).
        /// </summary>
        private void CancelAllV12GtcOrders(bool force)
        {
            int trackedCancels = SweepTrackedOrders(force);
            int brokerCancels  = SweepBrokerOrders(force);
            Print(string.Format("[BUILD 948] GTC sweep: cancelled {0} tracked + {1} broker-scanned orders",
                trackedCancels, brokerCancels));
        }

        /// <summary>Phase 1: cancel orders held in strategy tracking dictionaries.</summary>
        private int SweepTrackedOrders(bool force)
        {
            int trackedCancels = 0;
            var trackedDicts = new ConcurrentDictionary<string, Order>[]
            {
                entryOrders, stopOrders,
                target1Orders, target2Orders, target3Orders, target4Orders, target5Orders
            };
            foreach (var dict in trackedDicts)
            {
                if (dict == null) continue;
                foreach (var kvp in dict.ToArray())
                {
                    Order ord = kvp.Value;
                    if (ord == null) continue;
                    if (ord.OrderState != OrderState.Working    &&
                        ord.OrderState != OrderState.Accepted   &&
                        ord.OrderState != OrderState.Submitted  &&
                        ord.OrderState != OrderState.PendingSubmit &&
                        ord.OrderState != OrderState.ChangePending &&
                        ord.OrderState != OrderState.ChangeSubmitted) continue;
                    try
                    {
                        bool isFleet = ord.Account != null &&
                            IsFleetAccount(ord.Account) &&
                            !string.Equals(ord.Account.Name, Account.Name, StringComparison.OrdinalIgnoreCase);
                        if (isFleet)
                            ord.Account.Cancel(new[] { ord });
                        else
                            CancelOrder(ord);
                        trackedCancels++;
                    }
                    catch { }
                }
            }
            return trackedCancels;
        }

        /// <summary>
        /// Phase 2: broker-level scan to catch V12 orders not held in tracking dicts.
        /// [P1 LIFECYCLE SAFETY]: skips accounts with open positions when force=false
        /// to avoid leaving them naked after entry-order cancellation.
        /// </summary>
        private int SweepBrokerOrders(bool force)
        {
            int brokerCancels = 0;
            var v12Prefixes = new[] { "Stop_", "S_", "T1_", "T2_", "T3_", "T4_", "T5_", "Fleet_", "RMA", "Trend", "MOMO", "OR", "RETEST", "FFMA" };
            foreach (Account acct in Account.All)
            {
                if (!IsFleetAccount(acct)) continue;
                // [P1 LIFECYCLE SAFETY]: If not a forced teardown, skip accounts with open positions
                // to avoid leaving them naked (no bracket/stop) after their entry orders are cancelled.
                if (!force)
                {
                    bool hasPosition = false;
                    try
                    {
                        foreach (Position pos in acct.Positions)
                        {
                            if (pos.Instrument?.FullName == Instrument?.FullName && pos.Quantity != 0)
                            { hasPosition = true; break; }
                        }
                    }
                    catch { }
                    if (hasPosition)
                    {
                        Print(string.Format("[BUILD 948] GTC sweep: SKIPPING {0} -- open position detected (force=false)", acct.Name));
                        continue;
                    }
                }
                try
                {
                    foreach (Order ord in acct.Orders.ToArray())
                    {
                        if (ord.Instrument?.FullName != Instrument?.FullName) continue;
                        if (ord.OrderState != OrderState.Working    &&
                        ord.OrderState != OrderState.Accepted   &&
                        ord.OrderState != OrderState.Submitted  &&
                        ord.OrderState != OrderState.PendingSubmit &&
                        ord.OrderState != OrderState.ChangePending &&
                        ord.OrderState != OrderState.ChangeSubmitted) continue;
                        string ordName = ord.Name ?? string.Empty;
                        bool isV12 = false;
                        for (int pi = 0; pi < v12Prefixes.Length; pi++)
                        {
                            if (ordName.StartsWith(v12Prefixes[pi], StringComparison.OrdinalIgnoreCase))
                            { isV12 = true; break; }
                        }
                        if (!isV12) continue;
                        try { acct.Cancel(new[] { ord }); brokerCancels++; } catch { }
                    }
                }
                catch { }
            }
            return brokerCancels;
        }


        #endregion
    }
}
