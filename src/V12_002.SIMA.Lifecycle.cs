// <copyright file="V12_002.SIMA.Lifecycle.cs" company="BMad">
// Copyright (c) BMad. All rights reserved.
// </copyright>
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
            // Build 1109 [FREEZE-PROOF]: Non-blocking semaphore. Wait(0) returns instantly.
            // If contended, defer to next strategy-thread cycle via TriggerCustomEvent.
            if (!_simaToggleSem.Wait(0))
            {
                waitTimer.Stop();
                _simaTogglePending = true;
                bool _defEnabled = enabled;
                Print("[SIMA_WARN] Toggle semaphore contended -- scheduling non-blocking retry");
                try { TriggerCustomEvent(o => ProcessApplySimaState(_defEnabled), null); } catch { }
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
                // V12.Audit [H-10]: Toggle completed successfully -- clear any pending-retry flag.
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
            CancelAllV12GtcOrders(false); // [BUILD 984] GTC sweep before teardown -- skip accounts with open positions
            StopReaperAudit();
            UnsubscribeFromFleetAccounts();
            // v28.0 shutdown drain: sideband-aware, XorShadow-free (we do not verify on shutdown;
            // we just need to release pool + roll back delta). Sideband entries are zeroed after.
            {
                FleetDispatchSlot ringSlot;
                while (_photonDispatchRing != null && _photonDispatchRing.TryDequeue(out ringSlot))
                {
                    int _sbIdx = ringSlot.PoolSlotIndex;
                    string _expectedKey = (_sbIdx >= 0 && _sbIdx < _photonSideband.Length)
                        ? _photonSideband[_sbIdx].ExpectedKey
                        : null;
                    if (ringSlot.ReservedDelta != 0 && _expectedKey != null)
                        AddExpectedPositionDelta(_expectedKey, -ringSlot.ReservedDelta);
                    if (_expectedKey != null)
                        ClearDispatchSyncPending(_expectedKey);
                    if (_sbIdx >= 0)
                    {
                        _photonPool.ReleaseByIndex(_sbIdx);
                        if (_sbIdx < _photonSideband.Length)
                            _photonSideband[_sbIdx] = default(FleetDispatchSideband);
                    }
                }
                Print("[SIMA] Photon ring cleared on shutdown with delta rollback.");
            }
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
            UnsubscribeFromFleetAccounts(); // V12.1101E [A-4]: Always unsub first -- idempotent guard against handler accumulation
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
                    // Build 1105: Only init expectedPositions for master during enumeration.
                    // Follower REAPER audit truth is owned by FSM.
                    if (acct.Name == Account.Name)
                    { var _acct966init = ExpKey(acct.Name); SetExpectedPosition(_acct966init, 0); }
                    accountDailyProfit[acct.Name] = 0; // Initialize daily profit
                    EnsureAccountComplianceTracking(acct.Name, GetComplianceNow());
                    activeFleetAccounts[acct.Name] = false; // V12.8 SIMA: Default to INACTIVE -- wait for Fleet Manager / IPC to enable

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

            // Build 1103: Apply persisted fleet toggles from sticky state file.
            // Must run AFTER enumeration (dict populated) but BEFORE hydration (expected positions).
            ApplyPendingStickyFleetToggles();

            // V12.Phase6 [HYDRATE]: Seed expectedPositions from live broker state
            HydrateExpectedPositionsFromBroker();

            // [BUILD 984] Adopt any working broker orders into tracking dicts; sets _orderAdoptionComplete = true
            HydrateWorkingOrdersFromBroker();

            // Build 1103: Enrich reconstructed positions with persisted trail state.
            // Must run AFTER Phase 3 (activePositions populated) so position keys exist.
            EnrichTrailStateFromSticky();
        }

        /// <summary>
        /// V12.Phase6 [HYDRATE]: Reads actual broker positions for each fleet account and seeds
        /// expectedPositions accordingly. Prevents false Reaper CRITICAL DESYNC alerts when the
        /// strategy restarts while accounts hold open positions.
        /// </summary>
        private void HydrateExpectedPositionsFromBroker()
        {
            int hydratedCount = 0;
            HydrateExpected_FleetPositions(ref hydratedCount);
            if (hydratedCount > 0)
                Print($"[SIMA HYDRATE] Hydrated {hydratedCount} account(s) with live broker positions");
            HydrateExpected_MasterPosition(ref hydratedCount);
        }

        private void HydrateExpected_FleetPositions(ref int hydratedCount)
        {
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
        }

        private void HydrateExpected_MasterPosition(ref int hydratedCount)
        {
            // Build 993: Hydrate master account (mirrors AuditMasterAccountIfNeeded pattern).
            // IsFleetAccount excludes master -- must be handled separately, same as REAPER audit.
            bool masterIsFleet993 = IsFleetAccount(Account);
            if (!masterIsFleet993)
            {
                try
                {
                    foreach (Position pos in Account.Positions.ToArray())
                    {
                        if (pos != null && pos.Instrument?.FullName == Instrument.FullName
                            && pos.MarketPosition != MarketPosition.Flat)
                        {
                            int qty = pos.MarketPosition == MarketPosition.Long ? pos.Quantity : -pos.Quantity;
                            var capturedQty993 = qty;
                            Enqueue(ctx => ctx.AddOrUpdateExpectedPosition(ExpKey(Account.Name), capturedQty993, v => capturedQty993));
                            Print(string.Format("[SIMA HYDRATE] {0} (Master): Seeded expected={1} from broker ({2} {3})",
                                Account.Name, qty, pos.MarketPosition, pos.Quantity));
                            hydratedCount++;
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Print(string.Format("[SIMA HYDRATE] WARNING: Could not read positions for {0} (Master): {1}",
                        Account.Name, ex.Message));
                }
            }
        }

        /// <summary>
        /// Build 984 [FIX-B]: Re-adopt working broker orders into tracking dicts after restart or reconnect.
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
                        adoptedCount += HydrateWorking_AdoptFleetOrder(acct, ord);
                }
                catch (Exception ex)
                {
                    Print(string.Format("[SIMA HYDRATE] WARNING: Could not read orders for {0}: {1}", acct.Name, ex.Message));
                }
            }

            // Build 993: Adopt master account bracket orders (mirrors fleet loop; no FSM creation for master).
            // IsFleetAccount excludes master -- must be handled separately.
            bool masterIsFleetForOrders993 = IsFleetAccount(Account);
            if (!masterIsFleetForOrders993)
            {
                try
                {
                    Account masterBroker996h = Account;
                    foreach (Order ord in masterBroker996h.Orders.ToArray())
                        adoptedCount += HydrateWorking_AdoptMasterOrder(ord);
                }
                catch (Exception ex)
                {
                    Print(string.Format("[SIMA HYDRATE] WARNING: Could not adopt orders for {0} (Master): {1}",
                        Account.Name, ex.Message));
                }
            }

            // Build 1108.003 [D2-A]: Reconstruct master activePositions from adopted bracket orders + broker.
            // Filled master positions have bracket orders but no working entry order to hydrate from.
            if (!masterIsFleetForOrders993)
            {
                try
                {
                    HydrateWorking_ReconstructMasterPosition();
                }
                catch (Exception ex)
                {
                    Print(string.Format("[SIMA HYDRATE] WARNING: Master position reconstruction failed: {0}", ex.Message));
                }
            }

            HydrateWorking_GateComplete(adoptedCount);
        }

        private int HydrateWorking_AdoptFleetOrder(Account acct, Order ord)
        {
            if (ord.Instrument?.FullName != Instrument?.FullName) return 0;
            // [Codex P2] Include all live in-flight states -- Submitted/ChangePending/ChangeSubmitted
            // can be active during an in-flight FSM replace at reconnect time.
            // Setting _orderAdoptionComplete=true while these are skipped leaves REAPER
            // auditing against incomplete order tracking and can fire false repair cycles.
            if (ord.OrderState != OrderState.Working    &&
                ord.OrderState != OrderState.Accepted   &&
                ord.OrderState != OrderState.Submitted  &&
                ord.OrderState != OrderState.ChangePending &&
                ord.OrderState != OrderState.ChangeSubmitted) return 0;

            string name = ord.Name ?? string.Empty;
            ConcurrentDictionary<string, Order> targetDict = null;
            string key = null;
            string dictName = null;

            if (name.StartsWith("Stop_", StringComparison.OrdinalIgnoreCase))
            { targetDict = stopOrders; key = name.Substring(5); dictName = "stopOrders"; }
            else if (name.StartsWith("S_", StringComparison.OrdinalIgnoreCase))
            { targetDict = stopOrders; key = name.Substring(2); dictName = "stopOrders"; }
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

            if (targetDict == null || key == null) return 0;

            targetDict[key] = ord;
            if (targetDict == entryOrders)
                HydrateWorking_RebuildFollowerPosition(acct, ord, key);

            Print(string.Format("[SIMA HYDRATE] Adopted working order {0} into {1}", name, dictName));
            return 1;
        }

        private void HydrateWorking_RebuildFollowerPosition(Account acct, Order ord, string key)
        {
            // [Build 980 Nexus] Rebuild activePositions structs so Rehydration does not lead to divergent REAPER audits.
            if (!activePositions.ContainsKey(key))
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
        }

        private int HydrateWorking_AdoptMasterOrder(Order ord)
        {
            if (ord.Instrument?.FullName != Instrument?.FullName) return 0;
            // Build 994: Also accept Unknown -- NT8 Sim marks previous-session orders as Unknown.
            if (ord.OrderState != OrderState.Working    &&
                ord.OrderState != OrderState.Accepted   &&
                ord.OrderState != OrderState.Submitted  &&
                ord.OrderState != OrderState.ChangePending &&
                ord.OrderState != OrderState.ChangeSubmitted &&
                ord.OrderState != OrderState.Unknown) return 0;

            string name = ord.Name ?? string.Empty;
            ConcurrentDictionary<string, Order> targetDict = null;
            string key = null;
            string dictName = null;

            if (name.StartsWith("Stop_", StringComparison.OrdinalIgnoreCase))
            { targetDict = stopOrders; key = name.Substring(5); dictName = "stopOrders"; }
            else if (name.StartsWith("S_", StringComparison.OrdinalIgnoreCase))
            { targetDict = stopOrders; key = name.Substring(2); dictName = "stopOrders"; }
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

            if (targetDict == null || key == null) return 0;

            targetDict[key] = ord;
            Print(string.Format("[SIMA HYDRATE] {0} (Master): Adopted {1} -> {2}[{3}]",
                Account.Name, name, dictName, key));
            return 1;
        }

        private void HydrateWorking_ReconstructMasterPosition()
        {
            MarketPosition masterMP = MarketPosition.Flat;
            int masterQty = 0;
            double masterAvgPrice = 0;
            foreach (Position brokerPos in Account.Positions.ToArray())
            {
                if (brokerPos != null && brokerPos.Instrument != null
                    && brokerPos.Instrument.FullName == Instrument.FullName
                    && brokerPos.MarketPosition != MarketPosition.Flat)
                {
                    masterMP = brokerPos.MarketPosition;
                    masterQty = brokerPos.Quantity;
                    masterAvgPrice = brokerPos.AveragePrice;
                    break;
                }
            }

            if (masterMP != MarketPosition.Flat && masterQty > 0)
            {
                foreach (var stopKvp in stopOrders.ToArray())
                {
                    string key = stopKvp.Key;
                    if (key.StartsWith("Fleet_", StringComparison.OrdinalIgnoreCase)) continue;
                    if (activePositions.ContainsKey(key)) continue;

                    Order adoptedStop = stopKvp.Value;
                    double stopPrice = adoptedStop != null ? adoptedStop.StopPrice : 0;

                    int t1Qty, t2Qty, t3Qty, t4Qty, t5Qty;
                    GetTargetDistribution(masterQty, out t1Qty, out t2Qty, out t3Qty, out t4Qty, out t5Qty);

                    bool trendMnlMatch = key.StartsWith("TrendMnl", StringComparison.OrdinalIgnoreCase);
                    Print(string.Format("[SIMA HYDRATE] Master stop key audit for {0}: TrendMnlStartsWith={1}",
                        key, trendMnlMatch));

                    var pos = new PositionInfo
                    {
                        SignalName = key,
                        Direction = masterMP,
                        TotalContracts = masterQty,
                        RemainingContracts = masterQty,
                        EntryPrice = masterAvgPrice,
                        InitialStopPrice = stopPrice,
                        CurrentStopPrice = stopPrice,
                        EntryOrderType = OrderType.Market,
                        EntryFilled = true,
                        IsFollower = false,
                        ExecutingAccount = null,
                        BracketSubmitted = true,
                        ExtremePriceSinceEntry = masterAvgPrice,
                        CurrentTrailLevel = 0,
                        OcoGroupId = "V12_" + GetStableHash(key),
                        T1Contracts = t1Qty,
                        T2Contracts = t2Qty,
                        T3Contracts = t3Qty,
                        T4Contracts = t4Qty,
                        T5Contracts = t5Qty
                    };

                    pos.IsMOMOTrade = key.StartsWith("MOMO", StringComparison.OrdinalIgnoreCase);
                    pos.IsTRENDTrade = trendMnlMatch
                        || key.StartsWith("TRMA_", StringComparison.OrdinalIgnoreCase);
                    pos.IsRetestTrade = key.StartsWith("Retest", StringComparison.OrdinalIgnoreCase);
                    pos.IsRMATrade = key.StartsWith("TRMA_", StringComparison.OrdinalIgnoreCase)
                        || pos.IsRetestTrade;
                    pos.IsFFMATrade = key.StartsWith("FFMA", StringComparison.OrdinalIgnoreCase);
                    if (pos.IsMOMOTrade) pos.IsRMATrade = false;

                    activePositions[key] = pos;
                    Print(string.Format("[SIMA HYDRATE] Reconstructed master position for {0} | Dir={1} Qty={2} AvgPx={3} StopPx={4}",
                        key, masterMP, masterQty, masterAvgPrice, stopPrice));
                }
            }
        }

        private void HydrateWorking_GateComplete(int adoptedCount)
        {
            // Phase 5: Rebuild FSMs from adopted orders before enabling REAPER
            HydrateFSMsFromWorkingOrders();

            _orderAdoptionComplete = true;
            if (adoptedCount > 0)
                Print(string.Format("[SIMA HYDRATE] Adopted {0} working order(s) from broker -- adoption complete.", adoptedCount));
            else
                Print("[SIMA HYDRATE] No working orders to adopt -- adoption complete.");
        }

        /// <summary>
        /// Phase 5: Rebuilds _followerBrackets and _orderIdToFsmKey from already-adopted
        /// working orders. Called from HydrateWorkingOrdersFromBroker() before the
        /// adoption-complete gate is set. Idempotent -- safe to call on every reconnect.
        /// </summary>
        private void HydrateFSMsFromWorkingOrders()
        {
            int fsmCreated = 0;
            int ordersIndexed = 0;

            foreach (var kvp in entryOrders.ToArray())
                HydrateFsm_FromEntryOrder(kvp, ref fsmCreated, ref ordersIndexed);

            int positionFsmCreated = HydrateFsm_PositionPass(ref fsmCreated, ref ordersIndexed);
            HydrateFsm_LogSummary(positionFsmCreated, fsmCreated, ordersIndexed);
        }

        private void HydrateFsm_FromEntryOrder(KeyValuePair<string, Order> kvp, ref int fsmCreated, ref int ordersIndexed)
        {
            string entryKey = kvp.Key;
            Order entryOrder = kvp.Value;
            if (entryOrder == null) return;

            // Skip master account entries
            PositionInfo pi;
            if (!activePositions.TryGetValue(entryKey, out pi) || !pi.IsFollower) return;
            if (pi.ExecutingAccount == null) return;

            // Idempotent: skip if FSM already exists (safe on repeated reconnects)
            if (_followerBrackets.ContainsKey(entryKey)) return;

            // Map broker order state to FSM state
            FollowerBracketState hydrationState;
            OrderState entryState = entryOrder.OrderState;
            if (entryState == OrderState.Filled || entryState == OrderState.PartFilled)
                hydrationState = FollowerBracketState.Active;
            else if (entryState == OrderState.Accepted)
                hydrationState = FollowerBracketState.Accepted;
            else if (entryState == OrderState.Working
                  || entryState == OrderState.Submitted
                  || entryState == OrderState.Initialized
                  || entryState == OrderState.ChangePending
                  || entryState == OrderState.ChangeSubmitted)
                hydrationState = FollowerBracketState.Submitted;
            else
                return; // Terminal state -- FSM not needed

            int hydratedRemainingContracts = Math.Max(0, entryOrder.Quantity);
            if (hydrationState == FollowerBracketState.Active)
            {
                Position livePosition = pi.ExecutingAccount.Positions.ToArray().FirstOrDefault(p =>
                    p != null
                    && p.Instrument != null
                    && p.Instrument.FullName == Instrument.FullName
                    && p.MarketPosition != MarketPosition.Flat);
                if (livePosition != null)
                    hydratedRemainingContracts = Math.Abs(livePosition.Quantity);
            }

            var fsm = new FollowerBracketFSM
            {
                AccountName = pi.ExecutingAccount.Name,
                EntryName = entryKey,
                State = hydrationState,
                RemainingContracts = hydratedRemainingContracts,
                LastUpdateUtc = DateTime.UtcNow,
                EntryOrder = entryOrder
            };

            HydrateFsm_LinkStopAndTargets(entryKey, fsm, ref ordersIndexed);
            _followerBrackets.TryAdd(entryKey, fsm);

            if (!string.IsNullOrEmpty(entryOrder.OrderId))
            { _orderIdToFsmKey[entryOrder.OrderId] = entryKey; ordersIndexed++; }

            fsmCreated++;
        }

        private void HydrateFsm_LinkStopAndTargets(string entryKey, FollowerBracketFSM fsm, ref int ordersIndexed)
        {
            // Link stop order
            Order stopOrd;
            if (stopOrders.TryGetValue(entryKey, out stopOrd) && stopOrd != null)
            {
                fsm.StopOrder = stopOrd;
                if (!string.IsNullOrEmpty(stopOrd.OrderId))
                { _orderIdToFsmKey[stopOrd.OrderId] = entryKey; ordersIndexed++; }
            }

            // Link target orders (match exact property names on FollowerBracketFSM)
            Order targetOrd;
            if (target1Orders.TryGetValue(entryKey, out targetOrd) && targetOrd != null)
            {
                fsm.Targets[0] = targetOrd;
                if (!string.IsNullOrEmpty(targetOrd.OrderId))
                { _orderIdToFsmKey[targetOrd.OrderId] = entryKey; ordersIndexed++; }
            }
            if (target2Orders.TryGetValue(entryKey, out targetOrd) && targetOrd != null)
            {
                fsm.Targets[1] = targetOrd;
                if (!string.IsNullOrEmpty(targetOrd.OrderId))
                { _orderIdToFsmKey[targetOrd.OrderId] = entryKey; ordersIndexed++; }
            }
            if (target3Orders.TryGetValue(entryKey, out targetOrd) && targetOrd != null)
            {
                fsm.Targets[2] = targetOrd;
                if (!string.IsNullOrEmpty(targetOrd.OrderId))
                { _orderIdToFsmKey[targetOrd.OrderId] = entryKey; ordersIndexed++; }
            }
            if (target4Orders.TryGetValue(entryKey, out targetOrd) && targetOrd != null)
            {
                fsm.Targets[3] = targetOrd;
                if (!string.IsNullOrEmpty(targetOrd.OrderId))
                { _orderIdToFsmKey[targetOrd.OrderId] = entryKey; ordersIndexed++; }
            }
            if (target5Orders.TryGetValue(entryKey, out targetOrd) && targetOrd != null)
            {
                fsm.Targets[4] = targetOrd;
                if (!string.IsNullOrEmpty(targetOrd.OrderId))
                { _orderIdToFsmKey[targetOrd.OrderId] = entryKey; ordersIndexed++; }
            }
        }

        private int HydrateFsm_PositionPass(ref int fsmCreated, ref int ordersIndexed)
        {
            // Position Pass: handle accounts with open positions but terminal entry orders
            int positionFsmCreated = 0;
            foreach (Account acct in Account.All)
            {
                if (!IsFleetAccount(acct)) continue;

                // Do we already have an FSM for this account?
                if (_followerBrackets.Values.Any(f => string.Equals(f.AccountName, acct.Name, StringComparison.OrdinalIgnoreCase))) continue;

                // Is there an open position for this instrument in this account?
                Position acctPos = acct.Positions.FirstOrDefault(p => p.Instrument.FullName == Instrument.FullName && p.MarketPosition != MarketPosition.Flat);
                if (acctPos == null) continue;

                // Scan stopOrders for any entryKey belonging to this account
                string recoveredKey = null;
                Order recoveredStop = null;
                foreach (var stopKvp in stopOrders.ToArray())
                {
                    Order stopCand = stopKvp.Value;
                    if (stopCand == null) continue;
                    if (stopCand.Account == null) continue;

                    // If the stop order's original account matches our current iteration account
                    if (string.Equals(stopCand.Account.Name, acct.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        recoveredKey = stopKvp.Key;
                        recoveredStop = stopCand;
                        break;
                    }
                }

                if (recoveredKey == null)
                {
                    Print(string.Format("[SIMA] Phase 5 Position Pass: WARNING -- open position on {0} but no stopOrders key found. FSM not created. REAPER grace window started.", acct.Name));
                    // Build 999: Mark account for REAPER grace window -- defer critical desync up to 10s.
                    // CancelPending stop (stop-replace mid-flight at disable) causes this warning.
                    // The replace cycle resolves within seconds; grace prevents premature flatten cascade.
                    _positionPassFailedFirstSeen[acct.Name] = DateTime.UtcNow;
                    continue;
                }

                // Idempotent guard
                if (_followerBrackets.ContainsKey(recoveredKey)) continue;

                var fsm = new FollowerBracketFSM
                {
                    AccountName = acct.Name,
                    EntryName = recoveredKey,
                    State = FollowerBracketState.Active,
                    RemainingContracts = Math.Abs(acctPos.Quantity),
                    LastUpdateUtc = DateTime.UtcNow,
                    EntryOrder = null // Terminal entry order
                };

                if (recoveredStop != null)
                    fsm.StopOrder = recoveredStop;

                HydrateFsm_LinkStopAndTargets(recoveredKey, fsm, ref ordersIndexed);

                if (_followerBrackets.TryAdd(recoveredKey, fsm))
                {
                    positionFsmCreated++;
                    fsmCreated++;
                    Print(string.Format("[SIMA] Phase 5 Position Pass: Active FSM hydrated for {0} on {1}.",
                        recoveredKey, acct.Name));
                }
            }

            return positionFsmCreated;
        }

        private void HydrateFsm_LogSummary(int positionFsmCreated, int fsmCreated, int ordersIndexed)
        {
            Print(string.Format("[SIMA] Phase 5 FSM Hydration (Position Pass): {0} Active FSMs created from open positions.",
                positionFsmCreated));

            Print(string.Format("[SIMA] Phase 5 FSM Hydration: {0} FSMs created, {1} order IDs indexed.",
                fsmCreated, ordersIndexed));
        }

        /// <summary>
        /// Build 984 [FIX-A]: Sweep and cancel all V12-managed GTC orders before SIMA disable or strategy terminate.
        /// Phase 1 scans tracked order dicts; Phase 2 scans broker order lists for any V12-prefixed orders.
        /// force=true: cancel regardless of open positions (strategy terminate).
        /// force=false: skip accounts that have an open position for this instrument (SIMA disable -- prevent naked accounts).
        /// </summary>
        private void CancelAllV12GtcOrders(bool force)
        {
            int trackedCancels = SweepTrackedOrders(force);
            int brokerCancels  = SweepBrokerOrders(force);
            Print(string.Format("[BUILD 984] GTC sweep: cancelled {0} tracked + {1} broker-scanned orders",
                trackedCancels, brokerCancels));
        }

        /// <summary>Phase 1: cancel orders held in strategy tracking dictionaries.</summary>
        private int SweepTrackedOrders(bool force)
        {
            // Build 990: Semantic separation -- force=false (SIMA disable) cancels only entry orders.
            // Bracket orders (stop/target) are GTC with the broker and must remain to protect live positions.
            // force=true (strategy terminate) cancels all tracked orders.
            var trackedDicts = force
                ? new ConcurrentDictionary<string, Order>[]
                    { entryOrders, stopOrders, target1Orders, target2Orders, target3Orders, target4Orders, target5Orders }
                : new ConcurrentDictionary<string, Order>[]
                    { entryOrders };

            int trackedCancels = 0;
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
                        ord.OrderState != OrderState.ChangePending &&
                        ord.OrderState != OrderState.ChangeSubmitted) continue;
                    try
                    {
                        CancelOrderOnAccount(ord, ord.Account);
                        trackedCancels++;
                    }
                    catch { }
                }
            }
            return trackedCancels;
        }

        /// <summary>
        /// Phase 2: broker-level scan to catch V12 orders not held in tracking dicts.
        /// Build 990: Semantic separation -- force=false only targets entry-signal prefixes.
        /// Bracket prefixes (Stop_, S_, T1_-T5_) are excluded on soft disable to protect live positions.
        /// </summary>
        private int SweepBrokerOrders(bool force)
        {
            int brokerCancels = 0;
            // Build 990: Semantic separation -- force=false (SIMA disable) only targets entry-signal prefixes.
            // Bracket prefixes (Stop_, S_, T1_-T5_) are excluded on soft disable to protect live positions.
            var v12Prefixes = force
                ? new[] { "Stop_", "S_", "T1_", "T2_", "T3_", "T4_", "T5_", "Fleet_", "RMA", "Trend", "MOMO", "OR", "RETEST", "FFMA" }
                : new[] { "Fleet_", "RMA", "Trend", "MOMO", "OR", "RETEST", "FFMA" };

            foreach (Account acct in Account.All)
            {
                if (!IsFleetAccount(acct)) continue;
                try
                {
                    foreach (Order ord in acct.Orders.ToArray())
                        brokerCancels += SweepBroker_CancelEligibleOrder(force, acct, ord, v12Prefixes);
                }
                catch { }
            }
            return brokerCancels;
        }

        private bool SweepBroker_IsV12Order(string ordName, string[] v12Prefixes)
        {
            for (int pi = 0; pi < v12Prefixes.Length; pi++)
            {
                if (ordName.StartsWith(v12Prefixes[pi], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private bool SweepBroker_IsProtectedBracket(string ordName)
        {
            return ordName.StartsWith("Stop_", StringComparison.OrdinalIgnoreCase) ||
                ordName.StartsWith("S_", StringComparison.OrdinalIgnoreCase) ||
                ordName.StartsWith("T1_", StringComparison.OrdinalIgnoreCase) ||
                ordName.StartsWith("T2_", StringComparison.OrdinalIgnoreCase) ||
                ordName.StartsWith("T3_", StringComparison.OrdinalIgnoreCase) ||
                ordName.StartsWith("T4_", StringComparison.OrdinalIgnoreCase) ||
                ordName.StartsWith("T5_", StringComparison.OrdinalIgnoreCase) ||
                ordName.StartsWith("Target_", StringComparison.OrdinalIgnoreCase);
        }

        private int SweepBroker_CancelEligibleOrder(bool force, Account acct, Order ord, string[] v12Prefixes)
        {
            if (ord.Instrument?.FullName != Instrument?.FullName) return 0;
            if (ord.OrderState != OrderState.Working    &&
                ord.OrderState != OrderState.Accepted   &&
                ord.OrderState != OrderState.Submitted  &&
                ord.OrderState != OrderState.ChangePending &&
                ord.OrderState != OrderState.ChangeSubmitted) return 0;

            string ordName = ord.Name ?? string.Empty;
            if (!SweepBroker_IsV12Order(ordName, v12Prefixes)) return 0;

            // [FIX-FF]: Explicit bracket exclusion on soft disable.
            // Bracket orders protect live positions -- never cancel them during
            // SIMA disable or soft terminate. Defensive guard against naming drift.
            if (!force && SweepBroker_IsProtectedBracket(ordName))
            {
                Print(string.Format("[FIX-FF] Protected bracket order from sweep: {0} on {1}",
                    ordName, acct.Name));
                return 0;
            }

            try { acct.Cancel(new[] { ord }); return 1; } catch { return 0; }
        }


        #endregion
    }
}
