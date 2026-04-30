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
        /// V14.2 [ADR-012]: Shared fleet dispatch processing logic.
        /// Called by both Photon ring consumer and legacy ConcurrentQueue consumer.
        /// Guarantees identical invariants on both paths.
        /// </summary>
        /// <param name="poolRecordId">Index into PhotonOrderPool. -1 for legacy path (no pool release).</param>
        private void ProcessFleetSlot(Account acct, Order[] orders, int orderCount,
            string fleetEntryName, string expectedKey, int reservedDelta, long signalTicks,
            int poolRecordId)
        {
            bool syncCleared = false;
            FlattenedSubstrateState membrane = Volatile.Read(ref _membrane);
            int membraneIndex = -1;
            if (membrane != null && acct != null && membrane.AccountIndexByName != null)
                membrane.AccountIndexByName.TryGetValue(acct.Name, out membraneIndex);
            try
            {
                // Phase 6 [MG-T1]: Reject stale queued dispatch (enqueued > 5s ago)
                if (signalTicks > 0 && !MetadataGuardTimestamp(signalTicks, "Pump:" + fleetEntryName))
                {
                    ClearDispatchSyncPending(expectedKey);
                    if (membrane != null && membraneIndex >= 0 && membraneIndex < membrane.DispatchSyncPendingByIndex.Length)
                        Volatile.Write(ref membrane.DispatchSyncPendingByIndex[membraneIndex], 0);
                    syncCleared = true;
                    if (reservedDelta != 0)
                    {
                        AddExpectedPositionDeltaLocked(expectedKey, -reservedDelta);
                        if (membrane != null && membraneIndex >= 0 && membraneIndex < membrane.ExpectedPositionByIndex.Length)
                            Interlocked.Add(ref membrane.ExpectedPositionByIndex[membraneIndex], -reservedDelta);
                    }
                    activePositions.TryRemove(fleetEntryName, out _);
                    entryOrders.TryRemove(fleetEntryName, out _);
                    stopOrders.TryRemove(fleetEntryName, out _);
                    for (int tNum = 1; tNum <= 5; tNum++)
                    {
                        var td = GetTargetOrdersDictionary(tNum);
                        if (td != null) td.TryRemove(fleetEntryName, out _);
                    }
                    _followerBrackets.TryRemove(fleetEntryName, out _);
                    Print(string.Format("[PUMP] STALE dispatch rejected for {0} -- rolled back", fleetEntryName));
                    return;
                }

                // Phase 2 [D1]: Initialize FollowerBracketFSM for Shadow Mode
                if (!_followerBrackets.ContainsKey(fleetEntryName))
                {
                    var newFsm = new FollowerBracketFSM
                    {
                        AccountName = acct.Name,
                        EntryName = fleetEntryName,
                        State = FollowerBracketState.Submitted,
                        RemainingContracts = Math.Abs(reservedDelta),
                        LastUpdateUtc = DateTime.UtcNow
                    };

                    // FIX-D2: Use bounded for-loop (pool arrays are MaxOrdersPerSlot=7, may have fewer)
                    for (int i = 0; i < orderCount; i++)
                    {
                        var ord = orders[i];
                        if (ord == null || string.IsNullOrEmpty(ord.Name)) continue;

                        if (ord.Name == fleetEntryName)
                        {
                            newFsm.EntryOrder = ord;
                            newFsm.ExpectedEntryPrice = ord.LimitPrice > 0 ? ord.LimitPrice : 0;
                        }
                        else if (ord.Name.StartsWith("Stop_") || ord.Name.StartsWith("S_"))
                        {
                            newFsm.StopOrder = ord;
                            newFsm.ExpectedStopPrice = ord.StopPrice;
                            newFsm.OcoGroupId = ord.Oco;
                        }
                        else if (ord.Name.StartsWith("T"))
                        {
                            for (int tIdx = 1; tIdx <= 5; tIdx++)
                            {
                                if (ord.Name.StartsWith("T" + tIdx + "_"))
                                {
                                    newFsm.Targets[tIdx - 1] = ord;
                                    newFsm.ExpectedTargetPrices[tIdx - 1] = ord.LimitPrice;
                                    newFsm.OcoGroupId = ord.Oco;
                                    break;
                                }
                            }
                        }
                    }
                    _followerBrackets.TryAdd(fleetEntryName, newFsm);
                }

                acct.Submit(orders);
                ClearDispatchSyncPending(expectedKey);
                if (membrane != null && membraneIndex >= 0 && membraneIndex < membrane.DispatchSyncPendingByIndex.Length)
                    Volatile.Write(ref membrane.DispatchSyncPendingByIndex[membraneIndex], 0);
                syncCleared = true;

                // Phase 6 [FSM-P2]: Promote from PendingSubmit to Submitted
                FollowerBracketFSM pFsm;
                if (_followerBrackets.TryGetValue(fleetEntryName, out pFsm)
                    && pFsm != null
                    && pFsm.State == FollowerBracketState.PendingSubmit)
                {
                    pFsm.State = FollowerBracketState.Submitted;
                    pFsm.LastUpdateUtc = DateTime.UtcNow;
                }

                // Phase 3 [Step 3]: Register all order IDs for O(1) FSM lookup
                FollowerBracketFSM fsm;
                if (_followerBrackets.TryGetValue(fleetEntryName, out fsm))
                {
                    for (int i = 0; i < orderCount; i++)
                    {
                        var ord = orders[i];
                        if (ord != null && !string.IsNullOrEmpty(ord.OrderId))
                            _orderIdToFsmKey[ord.OrderId] = fleetEntryName;
                    }
                }

                Print(string.Format("[PUMP] Submitted {0} orders for {1} | {2}",
                    orderCount, fleetEntryName, acct.Name));
            }
            catch (Exception ex)
            {
                Print(string.Format("[PUMP] Submit FAILED for {0} ({1}): {2}",
                    fleetEntryName, acct.Name, ex.Message));
                if (!syncCleared)
                {
                    ClearDispatchSyncPending(expectedKey);
                    if (membrane != null && membraneIndex >= 0 && membraneIndex < membrane.DispatchSyncPendingByIndex.Length)
                        Volatile.Write(ref membrane.DispatchSyncPendingByIndex[membraneIndex], 0);
                }
                if (reservedDelta != 0)
                {
                    AddExpectedPositionDeltaLocked(expectedKey, -reservedDelta);
                    if (membrane != null && membraneIndex >= 0 && membraneIndex < membrane.ExpectedPositionByIndex.Length)
                        Interlocked.Add(ref membrane.ExpectedPositionByIndex[membraneIndex], -reservedDelta);
                }
                activePositions.TryRemove(fleetEntryName, out _);
                entryOrders.TryRemove(fleetEntryName, out _);
                stopOrders.TryRemove(fleetEntryName, out _);
                for (int tNum = 1; tNum <= 5; tNum++)
                {
                    var targetDict = GetTargetOrdersDictionary(tNum);
                    if (targetDict != null)
                        targetDict.TryRemove(fleetEntryName, out _);
                }
                _followerBrackets.TryRemove(fleetEntryName, out _);
            }
            finally
            {
                if (poolRecordId >= 0)
                    _photonPool.ReleaseByIndex(poolRecordId);
                Interlocked.Decrement(ref _pendingFleetDispatchCount);
                if ((_photonDispatchRing != null && !_photonDispatchRing.IsEmpty)
                    || !_pendingFleetDispatches.IsEmpty)
                    try { TriggerCustomEvent(o => PumpFleetDispatch(), null); } catch { }
            }
        }

        private void PumpFleetDispatch()
        {
            // A3-1: Abort and drain if SIMA disabled or flatten running.
            if (isFlattenRunning || !EnableSIMA)
            {
                FlattenedSubstrateState mDrain = Volatile.Read(ref _membrane);
                FleetDispatchSlot abortSlot;
                while (_photonDispatchRing != null && _photonDispatchRing.TryDequeue(out abortSlot))
                {
                    int accountIndex = abortSlot.AccountIndex;
                    Account accountRef = (mDrain != null && accountIndex >= 0 && accountIndex < mDrain.AccountByIndex.Length)
                        ? mDrain.AccountByIndex[accountIndex]
                        : null;
                    string expectedKey = accountRef != null ? ExpKey(accountRef.Name) : null;

                    if (abortSlot.ReservedDelta != 0 && expectedKey != null)
                    {
                        AddExpectedPositionDeltaLocked(expectedKey, -abortSlot.ReservedDelta);
                        if (mDrain != null && accountIndex >= 0 && accountIndex < mDrain.ExpectedPositionByIndex.Length)
                            Interlocked.Add(ref mDrain.ExpectedPositionByIndex[accountIndex], -abortSlot.ReservedDelta);
                    }
                    if (expectedKey != null)
                    {
                        ClearDispatchSyncPending(expectedKey);
                        if (mDrain != null && accountIndex >= 0 && accountIndex < mDrain.DispatchSyncPendingByIndex.Length)
                            Volatile.Write(ref mDrain.DispatchSyncPendingByIndex[accountIndex], 0);
                    }

                    if (abortSlot.PoolRecordId >= 0)
                        _photonPool.ReleaseByIndex(abortSlot.PoolRecordId);

                    Interlocked.Decrement(ref _pendingFleetDispatchCount);
                }
                FleetDispatchRequest stale;
                while (_pendingFleetDispatches.TryDequeue(out stale))
                {
                    if (stale.ReservedDelta != 0)
                        AddExpectedPositionDeltaLocked(stale.ExpectedKey, -stale.ReservedDelta);
                    ClearDispatchSyncPending(stale.ExpectedKey);
                    Interlocked.Decrement(ref _pendingFleetDispatchCount);
                }
                Print("[PUMP] Abort: SIMA inactive or flatten running. Ring+Queue drained with delta rollback.");
                return;
            }

            FleetDispatchSlot ringSlot;
            if (_photonDispatchRing != null && _photonDispatchRing.TryDequeue(out ringSlot))
            {
                FlattenedSubstrateState m = Volatile.Read(ref _membrane);
                int accountIndex = ringSlot.AccountIndex;
                int poolRecordId = ringSlot.PoolRecordId;
                Account accountRef = (m != null && accountIndex >= 0 && accountIndex < m.AccountByIndex.Length)
                    ? m.AccountByIndex[accountIndex]
                    : null;
                string expectedKey = accountRef != null ? ExpKey(accountRef.Name) : null;

                ulong stored = ringSlot.Shadow;
                ringSlot.Shadow = 0UL;
                ulong recomputed = ComputeFleetDispatchShadow(ref ringSlot, _photonShadowSalt);
                ringSlot.Shadow = stored;
                if (recomputed != stored)
                {
                    Interlocked.Increment(ref _photonCrcFailures);
                    Print(string.Format(
                        "[PHOTON_SHADOW] INTEGRITY FAILURE: expected=0x{0:X16} got=0x{1:X16} acctIdx={2} signalHash=0x{3:X16} -- SKIPPING",
                        stored, recomputed, accountIndex, ringSlot.SignalHash));
                    if (ringSlot.ReservedDelta != 0 && expectedKey != null)
                    {
                        AddExpectedPositionDeltaLocked(expectedKey, -ringSlot.ReservedDelta);
                        if (m != null && accountIndex >= 0 && accountIndex < m.ExpectedPositionByIndex.Length)
                            Interlocked.Add(ref m.ExpectedPositionByIndex[accountIndex], -ringSlot.ReservedDelta);
                    }
                    if (expectedKey != null)
                    {
                        ClearDispatchSyncPending(expectedKey);
                        if (m != null && accountIndex >= 0 && accountIndex < m.DispatchSyncPendingByIndex.Length)
                            Volatile.Write(ref m.DispatchSyncPendingByIndex[accountIndex], 0);
                    }
                    if (poolRecordId >= 0)
                        _photonPool.ReleaseByIndex(poolRecordId);
                    Interlocked.Decrement(ref _pendingFleetDispatchCount);
                    if (!_photonDispatchRing.IsEmpty || !_pendingFleetDispatches.IsEmpty)
                        try { TriggerCustomEvent(o => PumpFleetDispatch(), null); } catch { }
                    return;
                }

                if (accountRef == null)
                {
                    Interlocked.Increment(ref _photonCrcFailures);
                    Print(string.Format("[PUMP] Stale AccountIndex {0} (membrane drift) -- skipping", accountIndex));
                    if (poolRecordId >= 0)
                        _photonPool.ReleaseByIndex(poolRecordId);
                    Interlocked.Decrement(ref _pendingFleetDispatchCount);
                    return;
                }

                Order[] ringOrders = _photonPool.GetByIndex(poolRecordId);
                int orderCount = 0;
                if (ringOrders != null)
                {
                    for (int i = 0; i < ringOrders.Length; i++)
                    {
                        if (ringOrders[i] == null)
                            break;
                        orderCount++;
                    }
                }
                if (ringOrders == null || orderCount == 0)
                {
                    Print(string.Format("[PUMP] Pool payload missing for acctIdx={0} poolRecordId={1} -- skipping", accountIndex, poolRecordId));
                    if (expectedKey != null)
                    {
                        ClearDispatchSyncPending(expectedKey);
                        if (m != null && accountIndex >= 0 && accountIndex < m.DispatchSyncPendingByIndex.Length)
                            Volatile.Write(ref m.DispatchSyncPendingByIndex[accountIndex], 0);
                    }
                    if (ringSlot.ReservedDelta != 0 && expectedKey != null)
                    {
                        AddExpectedPositionDeltaLocked(expectedKey, -ringSlot.ReservedDelta);
                        if (m != null && accountIndex >= 0 && accountIndex < m.ExpectedPositionByIndex.Length)
                            Interlocked.Add(ref m.ExpectedPositionByIndex[accountIndex], -ringSlot.ReservedDelta);
                    }
                    if (poolRecordId >= 0)
                        _photonPool.ReleaseByIndex(poolRecordId);
                    Interlocked.Decrement(ref _pendingFleetDispatchCount);
                    return;
                }

                string fleetEntryName = !string.IsNullOrEmpty(ringOrders[0].Name)
                    ? ringOrders[0].Name
                    : "Fleet_" + accountRef.Name + "_idx" + accountIndex + "_h" + ringSlot.SignalHash.ToString("X8");
                ProcessFleetSlot(accountRef, ringOrders, orderCount,
                    fleetEntryName, expectedKey, ringSlot.ReservedDelta,
                    ringSlot.SignalTicks, poolRecordId);
                return;
            }

            FleetDispatchRequest req;
            if (!_pendingFleetDispatches.TryDequeue(out req))
                return;
            ProcessFleetSlot(req.Account, req.Orders, req.Orders.Length,
                req.FleetEntryName, req.ExpectedKey, req.ReservedDelta,
                req.SignalTicks, -1);
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

            // Step 2: H-13 stale state reconciliation (Build 1004: FSM-primary, no expectedPositions read).
            try
            {
                // [939-P0]: Snapshot Positions to prevent broker-thread mutation during iteration.
                var brokerPos = acct.Positions.ToArray().FirstOrDefault(p => p.Instrument.FullName == Instrument.FullName);
                bool brokerFlat = (brokerPos == null || brokerPos.MarketPosition == MarketPosition.Flat);

                bool hasActiveFsmForAcct = _followerBrackets.Values.Any(f =>
                    f != null
                    && f.AccountName == acct.Name
                    && (f.State == FollowerBracketState.Active
                        || f.State == FollowerBracketState.Accepted
                        || f.State == FollowerBracketState.Submitted
                        || f.State == FollowerBracketState.Replacing));
                bool hasActivePositionForAcct = activePositions.Values.Any(p =>
                    p.IsFollower && p.ExecutingAccount != null && p.ExecutingAccount.Name == acct.Name);
                bool hasDispatchPending = _dispatchSyncPendingExpKeys.ContainsKey(ExpKey(acct.Name));

                if (brokerFlat && !hasActiveFsmForAcct && !hasActivePositionForAcct && !hasDispatchPending)
                {
                    // Truly stale: broker flat, no FSM, no position, no dispatch in flight. No-op (nothing to reset).
                    dispatchLog.AppendLine(string.Format("[DISPATCH] H-13: {0} broker flat, no FSM/position/dispatch -- no action", acct.Name));
                }
                else if (brokerFlat && (hasActiveFsmForAcct || hasActivePositionForAcct || hasDispatchPending))
                {
                    dispatchLog.AppendLine(string.Format("[DISPATCH] H-13 SKIP: {0} Flat but {1} -- not resetting",
                        acct.Name, hasActiveFsmForAcct ? "FSM active" : (hasDispatchPending ? "dispatch pending" : "activePos present")));
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
        /// V12.1101E [A-4]: Idempotent unsubscribe -- removes all SIMA event handlers before
        /// re-subscribing. Prevents handler accumulation on repeated SIMA toggle cycles.
        /// V12.Phase6 [UNSUB-TRACK]: Deterministic unsubscribe -- uses tracked set of subscribed accounts
        /// instead of re-scanning Account.All, which may have changed since subscribe time.
        /// </summary>
        private void UnsubscribeFromFleetAccounts()
        {
            // Build 1109 [FREEZE-PROOF]: Snapshot Account.All once to prevent InvalidOperationException
            // if broker reconnects or modifies the collection during iteration.
            Account[] _acctSnapshot = Account.All.ToArray();

            // First: unsubscribe from tracked set (deterministic -- guaranteed to match subscribe)
            foreach (string acctName in _subscribedAccountNames)
            {
                foreach (Account acct in _acctSnapshot)
                {
                    if (acct.Name == acctName)
                    {
                        acct.ExecutionUpdate -= OnAccountExecutionUpdate;
                        acct.OrderUpdate     -= OnAccountOrderUpdate;
                        break;
                    }
                }
            }
            // Fallback: also sweep snapshot for any handlers from untracked subscribe paths
            foreach (Account acct in _acctSnapshot)
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
