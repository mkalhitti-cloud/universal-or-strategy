// Build 971: Orders.Callbacks.AccountOrders -- OnAccountOrderUpdate, ProcessAccountOrderQueue, TryFindOrderInPosition, HandleMatchedFollowerOrder, ExecuteFollowerCascadeCleanup, ProcessQueuedAccountOrder
// V12 Orders.Callbacks Module (Extracted)
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
        #region Orders Callbacks Account Orders

        private void OnAccountOrderUpdate(object sender, OrderEventArgs e)
        {
            if (e == null || e.Order == null) return;

            Order order = e.Order;
            Account acct = sender as Account;
            if (acct == null) return;

            // Phase 2: Enqueue into Actor Mailbox for FSM processing (Shadow Mode)
            // Only process if it's a fleet account and matches our instrument
            if (IsFleetAccount(acct) && order.Instrument != null && order.Instrument.FullName == Instrument.FullName)
            {
                _accountMailbox.Enqueue(new AccountEvent
                {
                    AccountAlias = acct.Name,
                    OrderId = order.OrderId,
                    NewState = order.OrderState,
                    FillPrice = order.AverageFillPrice,
                    FilledQty = order.Filled,
                    TimestampTicks = DateTime.UtcNow.Ticks,
                    SignalName = order.Name,
                    ErrorMessage = ""
                });
            }

            if (order.Instrument != null && order.Instrument.FullName != Instrument.FullName) return;

            // Build 1000: Master account managed order tracking
            if (acct == this.Account && order.Instrument != null && order.Instrument.FullName == Instrument.FullName)
                ProcessAccountOrder_UpdateMasterExpected(order);
            // Build 1104.1: Fleet account expectedPositions tracking (symmetric with Master at line 65)
            // Without this, expectedPositions stays stale after fleet stop/target fills,
            // causing REAPER to see Expected != Actual and trigger false flattens.
            else if (IsFleetAccount(acct))
                ProcessAccountOrder_UpdateFleetExpected(order, acct);

            ProcessAccountOrder_EnqueueTerminalUpdate(sender, e, order);
        }

        private void ProcessAccountOrder_UpdateMasterExpected(Order order)
            {
                if (order.OrderState == OrderState.Filled || order.OrderState == OrderState.PartFilled)
                {
                    if (order.Name.StartsWith("Stop_"))
                    {
                        // Clear naked-position grace for master when stop fills/exists
                        _nakedPositionFirstSeen.TryRemove(Account.Name, out _);

                        var mExpKey = ExpKey(Account.Name);
                        Enqueue(ctx => ctx.SetExpectedPositionLocked(mExpKey, 0));
                    }
                    else if (order.Name.StartsWith("T") && order.Name.Contains("_"))
                    {
                        int filledQty = order.Filled;
                        var mExpKey = ExpKey(Account.Name);
                        Enqueue(ctx => 
                        {
                            if (ctx.expectedPositions != null && ctx.expectedPositions.TryGetValue(mExpKey, out int currentExp))
                            {
                                int newExp = 0;
                                if (currentExp > 0)
                                    newExp = Math.Max(0, currentExp - filledQty);
                                else if (currentExp < 0)
                                    newExp = Math.Min(0, currentExp + filledQty);
                                    
                                ctx.SetExpectedPositionLocked(mExpKey, newExp);
                            }
                        });
                    }
                }
            }

        private void ProcessAccountOrder_UpdateFleetExpected(Order order, Account acct)
            {
                if (order.OrderState == OrderState.Filled || order.OrderState == OrderState.PartFilled)
                {
                    if (order.Name.StartsWith("Stop_"))
                    {
                        // Fleet stop filled: position closing. Zero expectedPositions.
                        _nakedPositionFirstSeen.TryRemove(acct.Name, out _);
                        var fExpKey = ExpKey(acct.Name);
                        Enqueue(ctx => ctx.SetExpectedPositionLocked(fExpKey, 0));
                    }
                    else if (order.Name.StartsWith("T") && order.Name.Contains("_"))
                    {
                        // Fleet target filled: delta-decrement expectedPositions.
                        int fFilledQty = order.Filled;
                        var fExpKey = ExpKey(acct.Name);
                        Enqueue(ctx =>
                        {
                            if (ctx.expectedPositions != null && ctx.expectedPositions.TryGetValue(fExpKey, out int fCurrentExp))
                            {
                                int fNewExp;
                                if (fCurrentExp > 0)
                                    fNewExp = Math.Max(0, fCurrentExp - fFilledQty);
                                else if (fCurrentExp < 0)
                                    fNewExp = Math.Min(0, fCurrentExp + fFilledQty);
                                else
                                    fNewExp = 0;
                                ctx.SetExpectedPositionLocked(fExpKey, fNewExp);
                            }
                        });
                    }
                }
            }

        private void ProcessAccountOrder_EnqueueTerminalUpdate(object sender, OrderEventArgs e, Order order)
        {
            if (order.OrderState != OrderState.Cancelled && order.OrderState != OrderState.Rejected &&
                order.OrderState != OrderState.Unknown)
            {
                return;
            }

            // V12.1101E [TM-01]: Marshal broker-thread callback to strategy thread before mutating strategy state.
            _accountOrderQueue.Enqueue(new QueuedAccountOrderUpdate
            {
                Account = sender as Account,
                EventArgs = e
            });
            try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); }
            catch (Exception ex)
            {
                if (_diagFleet)
                    Print("[FLEET_CATCH] OnAccountOrderUpdate trigger failed: " + ex.Message);
            }
        }

        // Build 935 [R-02]: Cap per-drain budget to prevent strategy-thread starvation
        // under high-velocity broker event bursts. Mirrors IpcMaxCommandsPerDrain pattern.
        private const int MaxAccountOrdersPerDrain = 8;

        private void ProcessAccountOrderQueue()
        {
            // Build 1109 [FREEZE-PROOF]: Queue depth warning
            int _oqDepth = _accountOrderQueue.Count;
            if (_oqDepth > 50)
                Print("[ORDER_WARN] Account order queue depth=" + _oqDepth);
            // V12.Phase7 [THREAD-01a]: Buffer-and-wait during flatten (symmetric with ProcessAccountExecutionQueue).
            if (isFlattenRunning)
            {
                try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); }
                catch (Exception ex)
                {
                    if (_diagFleet)
                        Print("[FLEET_CATCH] ProcessAccountOrderQueue flatten gate failed: " + ex.Message);
                }
                return;
            }

            int drainedCount = 0;
            QueuedAccountOrderUpdate item;
            while (drainedCount < MaxAccountOrdersPerDrain && _accountOrderQueue.TryDequeue(out item))
            {
                if (isFlattenRunning)
                {
                    _accountOrderQueue.Enqueue(item);
                    try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); }
                    catch (Exception ex)
                    {
                        if (_diagFleet)
                            Print("[FLEET_CATCH] ProcessAccountOrderQueue drain loop failed: " + ex.Message);
                    }
                    return;
                }
                drainedCount++;
                ProcessQueuedAccountOrder(item);
            }
            // If items remain after budget exhausted, reschedule for next strategy-thread slice.
            if (!_accountOrderQueue.IsEmpty)
                try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); }
                catch (Exception ex)
                {
                    if (_diagFleet)
                        Print("[FLEET_CATCH] ProcessAccountOrderQueue reschedule failed: " + ex.Message);
                }
        }

        // Build 1111.007-phase7-tW2 [T-W2]: Helper for Entry/Stop/T1 predicate (ref-equality short-circuit).
        // Preserves asymmetric pattern: ref-first then OrderId fallback. NO order null guard (H10).
        private bool TryFindOrder_MatchesEntryStopOrT1(ConcurrentDictionary<string, Order> dict, string entryKey, Order order)
        {
            return dict.TryGetValue(entryKey, out var tracked)
                && (tracked == order || (tracked != null && tracked.OrderId == order.OrderId));
        }

        // Build 1111.007-phase7-tW2 [T-W2]: Helper for T2-T5 predicate (OrderId-only equality).
        // Preserves asymmetric pattern: NO ref-equality check (H9). NO order null guard (H10).
        private bool TryFindOrder_MatchesT2ThroughT5(ConcurrentDictionary<string, Order> dict, string entryKey, Order order)
        {
            return dict.TryGetValue(entryKey, out var tracked)
                && tracked != null && tracked.OrderId == order.OrderId;
        }

        // Build 1111.007-phase7-tW2 [T-W2]: Returns true if 'order' belongs to 'entryKey' position.
        // Reduced from CYC=25 to CYC=8 via two-helper extraction. Preserves exact short-circuit order (B7/H11).
        private bool TryFindOrderInPosition(Order order, string entryKey, out string matchedEntry)
        {
            matchedEntry = null;
            // Sequential 7-step check preserving exact short-circuit order (B7/H11):
            // Entry/Stop/T1 use ref-first helper; T2-T5 use id-only helper (H9 asymmetry).
            if (TryFindOrder_MatchesEntryStopOrT1(entryOrders, entryKey, order))
            {
                matchedEntry = entryKey;
                return true;
            }
            if (TryFindOrder_MatchesEntryStopOrT1(stopOrders, entryKey, order))
            {
                matchedEntry = entryKey;
                return true;
            }
            if (TryFindOrder_MatchesEntryStopOrT1(target1Orders, entryKey, order))
            {
                matchedEntry = entryKey;
                return true;
            }
            if (TryFindOrder_MatchesT2ThroughT5(target2Orders, entryKey, order))
            {
                matchedEntry = entryKey;
                return true;
            }
            if (TryFindOrder_MatchesT2ThroughT5(target3Orders, entryKey, order))
            {
                matchedEntry = entryKey;
                return true;
            }
            if (TryFindOrder_MatchesT2ThroughT5(target4Orders, entryKey, order))
            {
                matchedEntry = entryKey;
                return true;
            }
            if (TryFindOrder_MatchesT2ThroughT5(target5Orders, entryKey, order))
            {
                matchedEntry = entryKey;
                return true;
            }
            return false;
        }

        private bool OrdersMatchByRefOrId(Order trackedOrder, Order order)
        {
            return trackedOrder == order
                || (trackedOrder != null && order != null && trackedOrder.OrderId == order.OrderId);
        }

        private bool TryFindMasterEntryForOrder(Order order, KeyValuePair<string, PositionInfo>[] snapshot, out string masterEntryName)
        {
            masterEntryName = null;
            if (order == null || snapshot == null) return false;

            foreach (var kvp in snapshot)
            {
                PositionInfo pos = kvp.Value;
                if (pos == null || pos.IsFollower) continue;

                Order trackedEntry;
                if (entryOrders.TryGetValue(kvp.Key, out trackedEntry) && OrdersMatchByRefOrId(trackedEntry, order))
                {
                    masterEntryName = kvp.Key;
                    return true;
                }
            }

            return false;
        }

        private bool TryGetDispatchFollowerEntries(string masterEntryName, out string[] followerEntries)
        {
            followerEntries = null;
            if (string.IsNullOrEmpty(masterEntryName)) return false;

            string dispatchId;
            SymmetryDispatchContext ctx;
            if (!symmetryMasterEntryToDispatch.TryGetValue(masterEntryName, out dispatchId)
                || !symmetryDispatchById.TryGetValue(dispatchId, out ctx)
                || ctx == null)
            {
                return false;
            }

            followerEntries = ctx.Followers;

            return followerEntries != null && followerEntries.Length > 0;
        }

        private bool IsMasterReplaceCascadeCancellation(Order order, KeyValuePair<string, PositionInfo>[] snapshot,
            out string masterEntryName, out string[] dispatchFollowers)
        {
            masterEntryName = null;
            dispatchFollowers = null;

            if (order == null || order.OrderState != OrderState.Cancelled || order.Account != this.Account)
                return false;

            if (!TryFindMasterEntryForOrder(order, snapshot, out masterEntryName))
                return false;

            if (!TryGetDispatchFollowerEntries(masterEntryName, out dispatchFollowers))
                return false;

            foreach (string followerEntry in dispatchFollowers)
            {
                FollowerReplaceSpec spec;
                if (!_followerReplaceSpecs.TryGetValue(followerEntry, out spec) || spec == null)
                    continue;

                if (spec.State != FollowerReplaceState.PendingCancel
                    && spec.State != FollowerReplaceState.Submitting)
                {
                    continue;
                }

                if (!string.Equals(spec.MasterSignalName, masterEntryName, StringComparison.Ordinal))
                    continue;

                if (!string.Equals(spec.SignalName, followerEntry, StringComparison.Ordinal))
                    continue;

                return true;
            }

            return false;
        }

        // H06: Top-level follower cancellation processor (state-agnostic).
        // Returns true if cancellation was handled (caller should return early).
        // Checks: PendingCancel FSM, Target Replace FSM, Stop Replacement, PendingCleanup purge.
        private bool ProcessFollowerCancellationSafe(string matchedEntry, PositionInfo matchedPos, Order order, string acctName, string reason)
        {
            if (order == null || order.OrderState != OrderState.Cancelled)
            {
                return false;
            }

            // Check 1: PendingCancel entry replacement FSM
            FollowerReplaceSpec fsm;
            if (_followerReplaceSpecs.TryGetValue(matchedEntry, out fsm)
                && fsm.State == FollowerReplaceState.PendingCancel
                && fsm.CancellingOrderId == order.OrderId)
            {
                return HandleMatchedFollower_PendingCancelReplace(matchedEntry, order, acctName);
            }

            // Check 2: Target replacement FSM
            FollowerTargetReplaceSpec tSpec = null;
            string tFsmMatchKey = null;
            foreach (var tKvp in _followerTargetReplaceSpecs.ToArray())
            {
                if (tKvp.Value.CancellingOrderId == order.OrderId)
                {
                    tSpec = tKvp.Value;
                    tFsmMatchKey = tKvp.Key;
                    break;
                }
            }
            if (tSpec != null && tFsmMatchKey != null)
            {
                return HandleMatchedFollower_TargetReplaceCancel(order);
            }

            // Check 3: Stop replacement (follower stops arrive via OnAccountOrderUpdate)
            if (order.Name.StartsWith("Stop_") || order.Name.StartsWith("S_"))
            {
                if (HandleMatchedFollower_StopReplacement(order))
                    return true;

                // Check 4: PendingCleanup purge for terminal stops
                HandleMatchedFollower_PendingCleanupPurge(order);
                Print(string.Format("[SIMA] Follower order terminal: {0} on {1} ({2}) | Id={3}", order.Name, acctName, reason, order.OrderId));
                RemoveGhostOrderRef(order, reason);
                return true;
            }

            return false;
        }

        // Build 935 [R-01]: Handles a follower order positively matched to an active position.
        // Entry-not-filled -> rollback + desync label. Entry-filled or stop/target -> ghost log + cleanup.
        private void HandleMatchedFollowerOrder(string matchedEntry, PositionInfo matchedPos, Order order, string acctName, string reason)
        {
            // H06: Top-level follower cancellation gate (state-agnostic, pre-branch).
            // Processes all cancellation types before entry-order conditional logic.
            if (ProcessFollowerCancellationSafe(matchedEntry, matchedPos, order, acctName, reason))
                return;

            if (entryOrders.TryGetValue(matchedEntry, out var entryOrder) &&
                (entryOrder == order || (entryOrder != null && entryOrder.OrderId == order.OrderId)) &&
                !matchedPos.EntryFilled)
            {
                entryOrders.TryRemove(matchedEntry, out _);
                // Build 1004: Replace expectedPositions guard with FSM Active/Accepted state check.
                bool acctFsmActive = _followerBrackets.Values.Any(f =>
                    f != null && f.AccountName == acctName
                    && (f.State == FollowerBracketState.Active || f.State == FollowerBracketState.Accepted));
                if (!acctFsmActive)
                {
                    // Build 973: FSM-Aware Guard for Meta-Purge Fix
                    FollowerReplaceSpec fsmGuard;
                    if (_followerReplaceSpecs.TryGetValue(matchedEntry, out fsmGuard)
                        && fsmGuard.State == FollowerReplaceState.PendingCancel
                        && fsmGuard.CancellingOrderId == order.OrderId)
                    {
                        Print("[META-PURGE GUARD] Rescuing PendingCancel spec " + matchedEntry + " despite no active FSM. Delegating to resubmit path.");
                        // DO NOT return, DO NOT destroy spec. Fall through.
                    }
                    else
                    {
                        // Build 947: clean up any in-flight FSM spec to avoid orphaned state
                        _followerReplaceSpecs.TryRemove(matchedEntry, out _);
                        return;
                    }
                }

                HandleMatchedFollower_DeltaRollback(matchedEntry);
                Print(string.Format("[SIMA] Follower entry cancelled: {0} on {1}. Reaper monitoring.", matchedEntry, acctName));
                Draw.TextFixed(this, "SIMA_DESYNC_" + acctName, "(!) FOLLOWER DESYNC: " + acctName, TextPosition.TopLeft, Brushes.Red, new SimpleFont("Arial", 11), Brushes.Transparent, Brushes.Transparent, 50);
            }
            else
            {
                // H06: Non-entry orders (stops, targets) already handled by top-level gate
                Print(string.Format("[SIMA] Follower order terminal: {0} on {1} ({2}) | Id={3}", order.Name, acctName, reason, order.OrderId));
                RemoveGhostOrderRef(order, reason);
            }
        }

        private bool HandleMatchedFollower_PendingCancelReplace(string matchedEntry, Order order, string acctName)
        {
                // Build 947 FSM: if this cancel was our PendingCancel, submit replacement instead of DESYNC
                FollowerReplaceSpec fsm;
                if (_followerReplaceSpecs.TryGetValue(matchedEntry, out fsm)
                    && fsm.State == FollowerReplaceState.PendingCancel
                    && fsm.CancellingOrderId == order.OrderId)
                {
                    // Fill-during-gap guard: if master already has a live filled position, let REAPER handle
                    PositionInfo masterPos = null;
                    bool masterFilled = false;

                    // Phase 10 [B960-AUDIT]: synchronization wrapper removed. Both this path
                    // via ProcessQueuedAccountOrder via TriggerCustomEvent and PropagateFollowerEntryReplace
                    // are serialized on the NinjaTrader strategy thread. No concurrent field access is possible.
                    int qty = 0;
                    double price = 0;
                    string acctNameCapture = acctName;
                    string sigName = fsm.SignalName;
                    FollowerReplaceSpec fsmCapture = fsm;

                    masterFilled = !string.IsNullOrEmpty(fsm.MasterSignalName)
                        && activePositions.TryGetValue(fsm.MasterSignalName, out masterPos)
                        && masterPos != null
                        && masterPos.EntryFilled
                        && masterPos.RemainingContracts > 0;

                    if (!masterFilled)
                    {
                        qty             = fsm.PendingQty;
                        price           = fsm.PendingPrice;
                        acctNameCapture = fsm.AccountName;
                        sigName         = fsm.SignalName;
                        fsmCapture      = fsm;
                        fsm.State       = FollowerReplaceState.Submitting;
                    }

                    if (masterFilled)
                    {
                        Print("[FSM] Master filled during cancel wait -- routing "
                            + fsm.SignalName + " to repair instead of replace.");
                        _followerReplaceSpecs.TryRemove(fsm.SignalName, out _);
                        string masterFilledExpKey = ExpKey(acctName);
                        ClearDispatchSyncPending(masterFilledExpKey);
                        _reaperRepairQueue.Enqueue(acctName);
                        ProcessReaperRepairQueue();
                    return true;
                    }

                    bool replacementScheduled = false;
                    try
                    {
                        TriggerCustomEvent(o =>
                        {
                            // [P2 FSM CONSISTENCY]: Re-read price/qty from spec at execution time.
                            // ATR tick absorption may have updated PendingPrice/PendingQty after the
                            // lambda was scheduled -- using stale captures would submit wrong values.
                            SubmitFollowerReplacement(sigName, acctNameCapture, fsmCapture.PendingPrice, fsmCapture.PendingQty, fsmCapture);
                            _followerReplaceSpecs.TryRemove(sigName, out _);
                        }, null);
                        replacementScheduled = true;
                    }
                    catch (Exception ex)
                    {
                        Print("[FSM] TriggerCustomEvent failed for " + sigName + ": " + ex.Message);
                        _followerReplaceSpecs.TryRemove(sigName, out _);
                    }
                    if (replacementScheduled)
                    return true; // FSM-controlled replace cancel -- reservation stays live until resubmit completes.
            }

            return false;
        }

        private bool HandleMatchedFollower_TargetReplaceCancel(Order order)
        {
                // B957/C1: Check for follower TARGET replace FSM spec before doing delta rollback.
                // If this cancel was part of a two-phase target replacement, submit the new order
                // and return -- no delta rollback needed (position remains open, just target moved).
                    FollowerTargetReplaceSpec tSpec = null;
                    string tFsmMatchKey = null;
                    foreach (var tKvp in _followerTargetReplaceSpecs.ToArray())
                    {
                        if (tKvp.Value.CancellingOrderId == order.OrderId)
                        {
                            tSpec = tKvp.Value;
                            tFsmMatchKey = tKvp.Key;
                            break;
                        }
                    }
                    if (tSpec != null && tFsmMatchKey != null)
                    {
                        _followerTargetReplaceSpecs.TryRemove(tFsmMatchKey, out _);
                        FollowerTargetReplaceSpec captured = tSpec;
                        string capturedKey = tFsmMatchKey;
                        try
                        {
                            TriggerCustomEvent(o => SubmitFollowerTargetReplacement(capturedKey, captured), null);
                        }
                        catch (Exception tFsmEx)
                        {
                            Print("[FSM_TGT] TriggerCustomEvent failed for " + capturedKey + ": " + tFsmEx.Message);
                        }
                return true; // FSM-controlled target cancel -- skip delta rollback, not a real desync
                    }

            return false;
                }

        private void HandleMatchedFollower_DeltaRollback(string matchedEntry)
        {
                // A2-3: Direction-aware delta rollback on CONFIRMED cancel -- deferred from SymmetryGuardCascadeFollowerCleanup
                // to prevent REAPER desync on microsecond fill race (Build 960 audit fix).
                PositionInfo cancelledFollowerPos;
                if (activePositions.TryGetValue(matchedEntry, out cancelledFollowerPos) && cancelledFollowerPos != null)
                {
                    if (cancelledFollowerPos.ExecutingAccount == null)
                    {
                        Print("[B983-D2] HandleMatchedFollowerOrder: ExecutingAccount null for " + matchedEntry
                            + " -- skipping ExpKey delta and sync barrier ops to avoid master domain bleed.");
                    }
                    else
                    {
                        string cancelAcctKey = cancelledFollowerPos.ExecutingAccount.Name;
                        int cancelDelta = (cancelledFollowerPos.Direction == MarketPosition.Long)
                            ? -cancelledFollowerPos.TotalContracts : cancelledFollowerPos.TotalContracts;
                        DeltaExpectedPositionLocked(ExpKey(cancelAcctKey), cancelDelta);
                        // B957/D2: Release the SIMA dispatch-sync barrier for this account. Without this, the barrier
                        // remains permanently blocked after a follower cancel, starving future dispatches.
                        _dispatchSyncPendingExpKeys.TryRemove(ExpKey(cancelAcctKey), out _); // [B967-FIX-02]
                    }
                }
            }

        private bool HandleMatchedFollower_StopReplacement(Order order)
            {
                // Build 950: Follower stop replacement -- mirrors HandleOrderCancelled master path.
                // Follower stop cancels arrive via OnAccountOrderUpdate (not OnOrderUpdate), so
                // HandleOrderCancelled never fires for them. Match pendingStopReplacements here.
                // This block is in the else branch because stop orders are not in entryOrders.
                if (order.Name.StartsWith("Stop_") || order.Name.StartsWith("S_"))
                {
                    foreach (var _psr in pendingStopReplacements.ToArray())
                    {
                        if (_psr.Value.OldOrder == order
                            || (_psr.Value.OldOrder != null && _psr.Value.OldOrder.OrderId == order.OrderId))
                        {
                            PositionInfo _rPos;
                            // Build 955: Move guard inside lock -- check and use same atomic snapshot.
                            if (activePositions.TryGetValue(_psr.Key, out _rPos))
                            {
                                int _rQty;
                                _rQty = _rPos.RemainingContracts;
                                if (_rQty > 0)
                                {
                                    CreateNewStopOrder(_psr.Key, _rQty, _psr.Value.StopPrice, _psr.Value.Direction);
                                    if (_psr.Value.BracketRestorationNeeded && _psr.Value.CapturedTargets != null)
                                    {
                                        TargetSnapshot[] _snap = _psr.Value.CapturedTargets;
                                        string _rKey = _psr.Key;
                                        TriggerCustomEvent(o => RestoreCascadedTargets(_rKey, _snap), null);
                                    }
                                } // if (_rQty > 0)
                            } // if (activePositions.TryGetValue)
                            if (pendingStopReplacements.TryRemove(_psr.Key, out _)) Interlocked.Decrement(ref pendingReplacementCount);
                        return true;
                    }
                        }
                    }

            return false;
                }

        private void HandleMatchedFollower_PendingCleanupPurge(Order order)
        {
                // A2-2: Deferred PendingCleanup purge -- follower stop terminal (Build 960 audit fix).
                if (order.Name.StartsWith("Stop_") || order.Name.StartsWith("S_"))
                {
                    foreach (var _sc in stopOrders.ToArray())
                    {
                        if (_sc.Value == order)
                        {
                            PositionInfo _scPos;
                            if (activePositions.TryGetValue(_sc.Key, out _scPos) && _scPos != null
                                && _scPos.PendingCleanup && _scPos.RemainingContracts <= 0)
                            {
                                stopOrders.TryRemove(_sc.Key, out _);
                                activePositions.TryRemove(_sc.Key, out _);
                                SymmetryGuardForgetEntry(_sc.Key);
                                Print("[A2-2] Deferred PendingCleanup purge (follower stop terminal): " + _sc.Key);
                            }
                            break;
                        }
                    }
                }
        }

        // Build 935 [R-01]: SIMA cascade cleanup for unmatched master-cancel events.
        // Receives pre-computed snapshot -- eliminates the second activePositions.ToArray() allocation.
        private void ExecuteFollowerCascadeCleanup(bool enableSima, Order order, string reason, KeyValuePair<string, PositionInfo>[] snapshot)
        {
            // V12.18 SIMA CASCADE: If a master-account order was cancelled,
            // check if any follower positions share the same base signal and tear them down.
            if (enableSima && order.OrderState == OrderState.Cancelled && order.Account == this.Account)
            {
                string masterEntryName;
                string[] dispatchFollowers;
                if (ExecuteFollowerCascade_SuppressMasterReplace(order, reason, snapshot, out masterEntryName, out dispatchFollowers))
                    return;

                string orderSignal = order.Name;
                Dictionary<string, PositionInfo> snapshotByKey = new Dictionary<string, PositionInfo>();
                foreach (var kvp in snapshot)
                    snapshotByKey[kvp.Key] = kvp.Value;

                IEnumerable<string> followerKeys = ExecuteFollowerCascade_ResolveFollowers(orderSignal, masterEntryName, dispatchFollowers, snapshot);

                foreach (string followerKey in followerKeys)
                {
                    PositionInfo cascadePos;
                    if (!snapshotByKey.TryGetValue(followerKey, out cascadePos) || cascadePos == null || !cascadePos.IsFollower)
                        continue;

                    string cascadeAcctName = cascadePos.ExecutingAccount != null ? cascadePos.ExecutingAccount.Name : "NULL";

                    // [BUILD 984] [FIX-A]: Skip cascade teardown if this follower has an in-flight Replace FSM.
                    // A chart-drag cancel on the master reaches this path. Destroying the follower here zeroes
                    // expectedPositions mid-replace; the replacement fill then triggers REAPER Critical Desync
                    // (actualQty != 0, expectedQty == 0) -> Emergency Flatten.
                    FollowerReplaceSpec _b948FsmSpec;
                    if (_followerReplaceSpecs.TryGetValue(followerKey, out _b948FsmSpec))
                    {
                        Print(string.Format("[FSM-GUARD] SKIP cascade teardown for {0} on {1}: in-flight Replace FSM (state={2}). Chart-drag suppressed.",
                            followerKey, cascadeAcctName, _b948FsmSpec.State));
                        continue;
                    }

                    if (!cascadePos.EntryFilled)
                        ExecuteFollowerCascade_CleanupUnfilled(masterEntryName, orderSignal, followerKey, cascadePos);
                    else
                        ExecuteFollowerCascade_EmergencyFlattenFilled(masterEntryName, orderSignal, followerKey, cascadePos);
                }
            }
            RemoveGhostOrderRef(order, reason);
        }

        private bool ExecuteFollowerCascade_SuppressMasterReplace(Order order, string reason, KeyValuePair<string, PositionInfo>[] snapshot, out string masterEntryName, out string[] dispatchFollowers)
        {
            if (IsMasterReplaceCascadeCancellation(order, snapshot, out masterEntryName, out dispatchFollowers))
            {
                Print(string.Format("[FSM] Suppressing cascade teardown for master replace cancel: {0}", masterEntryName));
                RemoveGhostOrderRef(order, reason);
                return true;
            }

            return false;
        }

        private IEnumerable<string> ExecuteFollowerCascade_ResolveFollowers(string orderSignal, string masterEntryName, string[] dispatchFollowers, KeyValuePair<string, PositionInfo>[] snapshot)
                    {
            if (!string.IsNullOrEmpty(masterEntryName) && dispatchFollowers != null && dispatchFollowers.Length > 0)
                return dispatchFollowers;

            // [BUILD 984] [FIX-B]: Delimiter-anchored match replaces bidirectional .Contains().
            // Bidirectional .Contains() caused accidental cascade of unrelated positions:
            // e.g. signal "OR" matched "Fleet_Apex_RETEST_OR_1" incidentally.
            // Anchoring on underscores prevents substring contamination across signal families.
            return snapshot
                .Where(kvp => kvp.Value != null && kvp.Value.IsFollower
                    && (kvp.Key == orderSignal
                        || kvp.Key.Contains("_" + orderSignal + "_")
                        || kvp.Key.EndsWith("_" + orderSignal)))
                .Select(kvp => kvp.Key)
                .ToArray();
        }

        private void ExecuteFollowerCascade_CleanupUnfilled(string masterEntryName, string orderSignal, string followerKey, PositionInfo cascadePos)
        {
            string cascadeAcctName = cascadePos.ExecutingAccount != null ? cascadePos.ExecutingAccount.Name : "NULL";

                        Print(string.Format("[GHOST_FIX] SIMA CASCADE: Master cancel of {0} triggers follower teardown for {1} on {2}",
                            !string.IsNullOrEmpty(masterEntryName) ? masterEntryName : orderSignal, followerKey, cascadeAcctName));
                        CleanupPosition(followerKey);

                        if (cascadePos.ExecutingAccount != null)
                        {
                            int rollbackDelta = (cascadePos.Direction == MarketPosition.Long) ? -cascadePos.TotalContracts : cascadePos.TotalContracts;
                            int currentExp = 0;
                            expectedPositions.TryGetValue(ExpKey(cascadeAcctName), out currentExp);
                            if (currentExp == 0)
                            {
                                Print(string.Format("[GHOST_FIX] SKIP cascade delta for {0}: expectedPositions already 0 (purge-race guard). Delta suppressed.",
                                    cascadeAcctName));
                            }
                            else
                            {
                                DeltaExpectedPositionLocked(ExpKey(cascadeAcctName), rollbackDelta);
                            }
                            ClearDispatchSyncPending(ExpKey(cascadeAcctName));
                            try { RemoveDrawObject("SIMA_DESYNC_" + cascadeAcctName); }
                            catch (Exception ex)
                            {
                                if (_diagFleet)
                                    Print("[FLEET_CATCH] ExecuteFollowerCascade desync cleanup failed: " + ex.Message);
                            }
                        }
                    }

        private void ExecuteFollowerCascade_EmergencyFlattenFilled(string masterEntryName, string orderSignal, string followerKey, PositionInfo cascadePos)
                    {
            string cascadeAcctName = cascadePos.ExecutingAccount != null ? cascadePos.ExecutingAccount.Name : "NULL";

                        Print(string.Format("[DEAD-01] CASCADE-FILLED: Master cancel {0} -- follower {1} on {2} is FILLED. Issuing emergency flatten.",
                            !string.IsNullOrEmpty(masterEntryName) ? masterEntryName : orderSignal, followerKey, cascadeAcctName));
                        if (cascadePos.ExecutingAccount != null)
                        {
                            Account filledFollowerAcct = cascadePos.ExecutingAccount;
                            TriggerCustomEvent(o => EmergencyFlattenSingleFleetAccount(filledFollowerAcct), null);
                        }
                    }

        // H06: State-agnostic cancellation processor for follower orders.
        // Processes cancellations BEFORE matched-entry gate to handle stale-state scenarios.
        // Returns true if cancellation was handled (caller should skip normal flow).
        private bool ProcessFollowerCancellationUnconditional(Order order, string acctName, string reason)
        {
            if (order == null || order.OrderState != OrderState.Cancelled)
                return false;

            // Check 1: PendingCancel entry replacement FSM
            var replaceSpecsSnapshot = _followerReplaceSpecs.ToArray();
            foreach (var kvp in replaceSpecsSnapshot)
            {
                FollowerReplaceSpec fsm = kvp.Value;
                if (fsm.State == FollowerReplaceState.PendingCancel
                    && fsm.CancellingOrderId == order.OrderId)
                {
                    string matchedEntry = kvp.Key;
                    return HandleMatchedFollower_PendingCancelReplace(matchedEntry, order, acctName);
                }
            }

            // Check 2: Target replacement FSM
            var targetReplaceSpecsSnapshot = _followerTargetReplaceSpecs.ToArray();
            foreach (var tKvp in targetReplaceSpecsSnapshot)
            {
                if (tKvp.Value.CancellingOrderId == order.OrderId)
                {
                    return HandleMatchedFollower_TargetReplaceCancel(order);
                }
            }

            // Check 3: Stop replacement (follower stops arrive via OnAccountOrderUpdate)
            // P2-FIX (Iteration 4): Add null guard before order.Name access
            if (order.Name != null && (order.Name.StartsWith("Stop_") || order.Name.StartsWith("S_")))
            {
                if (HandleMatchedFollower_StopReplacement(order))
                    return true;

                // Check 4: PendingCleanup purge for terminal stops
                HandleMatchedFollower_PendingCleanupPurge(order);
                Print(string.Format("[SIMA] Follower order terminal: {0} on {1} ({2}) | Id={3}", order.Name, acctName, reason, order.OrderId));
                RemoveGhostOrderRef(order, reason);
                return true;
            }

            return false;
        }

        private void ProcessQueuedAccountOrder(QueuedAccountOrderUpdate item)
        {
            if (item.EventArgs == null || item.EventArgs.Order == null) return;
            Order order = item.EventArgs.Order;
            if (order.Instrument != null && order.Instrument.FullName != Instrument.FullName) return;

            string reason = order.OrderState.ToString().ToUpper();
            string acctName = item.Account != null ? item.Account.Name : "UNKNOWN";
            Print(string.Format("[GHOST-AUDIT] OnAccountOrderUpdate: {0} | State={1} | Acct={2}", order.Name, reason, acctName));

            // H06: Process cancellations BEFORE matched-entry gate (state-agnostic path)
            if (ProcessFollowerCancellationUnconditional(order, acctName, reason))
                return;

            // Build 935 [R-01]: Single snapshot -- reused by both identity search and cascade cleanup,
            // eliminating the second activePositions.ToArray() allocation in the cascade path.
            var snapshot = activePositions.ToArray();

            string matchedEntry = null;
            PositionInfo matchedPos = null;
            foreach (var kvp in snapshot)
            {
                if (!activePositions.ContainsKey(kvp.Key)) continue;
                PositionInfo pos = kvp.Value;
                if (!pos.IsFollower || pos.ExecutingAccount == null || pos.ExecutingAccount != item.Account) continue;
                if (TryFindOrderInPosition(order, kvp.Key, out matchedEntry))
                {
                    matchedPos = pos;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(matchedEntry) && matchedPos != null && activePositions.ContainsKey(matchedEntry))
                HandleMatchedFollowerOrder(matchedEntry, matchedPos, order, acctName, reason);
            else
                ExecuteFollowerCascadeCleanup(EnableSIMA, order, reason, snapshot);
        }


        #endregion
    }
}
