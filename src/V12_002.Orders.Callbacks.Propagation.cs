// Build 971: Orders.Callbacks.Propagation -- PropagateMasterPriceMove, PropagateMasterStopMove, PropagateMasterTargetMove, PropagateMasterEntryMove, PropagateFollowerEntryReplace, SubmitFollowerReplacement, SubmitFollowerTargetReplacement
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
        #region Orders Callbacks Propagation

        private void PropagateMasterPriceMove(Order masterOrder, double newLimit, double newStop, int newMasterQty = 0)
        {
            if (!EnableSIMA || masterOrder == null || masterOrder.Account != this.Account) return;

            // [BUILD 924 -- Fix C] Raise propagation flag before dispatch; finally block clears it.
            _propagationActive = true;
            try
            {

            // --- Step 1: Identify master position and move type via object identity ---
            string masterEntryName = null;
            bool isEntryMove  = false;
            bool isStopMove   = false;
            bool isTargetMove = false;
            int  masterTargetNum = 0;

            foreach (var kvp in entryOrders)
            {
                if (kvp.Value == masterOrder &&
                    activePositions.TryGetValue(kvp.Key, out var mp) && !mp.IsFollower)
                {
                    masterEntryName = kvp.Key;
                    isEntryMove = true;
                    break;
                }
            }

            if (masterEntryName == null)
            {
                foreach (var kvp in stopOrders)
                {
                    if (kvp.Value == masterOrder &&
                        activePositions.TryGetValue(kvp.Key, out var mp) && !mp.IsFollower)
                    {
                        masterEntryName = kvp.Key;
                        isStopMove = true;
                        break;
                    }
                }
            }

            if (masterEntryName == null)
            {
                for (int t = 1; t <= 5 && masterEntryName == null; t++)
                {
                    var tDict = GetTargetOrdersDictionary(t);
                    if (tDict == null) continue;
                    foreach (var kvp in tDict)
                    {
                        if (kvp.Value == masterOrder &&
                            activePositions.TryGetValue(kvp.Key, out var mp) && !mp.IsFollower)
                        {
                            masterEntryName  = kvp.Key;
                            isTargetMove     = true;
                            masterTargetNum  = t;
                            break;
                        }
                    }
                }
            }

            if (masterEntryName == null) return; // Not a tracked master order

            // --- Step 2: Resolve follower entry names via Symmetry dispatch context ---

            // [BUILD 926 -- Codex P1 Fix]: Derive master TradeType from boolean flags.
            // Master boolean flags ARE accurate (master positions set IsTRENDTrade, IsRMATrade etc. correctly).
            // Only FOLLOWER flags are contaminated (IsRMATrade=true on ALL followers for trailing behavior).
            // Follower type discrimination uses SignalName parsing instead -- see fallback scan below.
            string masterTradeType = null;
            if (activePositions.TryGetValue(masterEntryName, out var masterPosForType))
            {
                // [BUILD 928 -- Codex P2 Fix]: IsRetestTrade MUST be checked before IsRMATrade.
                // RETEST positions set both IsRetestTrade=true AND IsRMATrade=true (uses RMA trailing).
                // Old order checked IsRMATrade first -> RETEST master classified as "RMA" -> fallback
                // propagation targets RMA followers and silently skips RETEST followers.
                if      (masterPosForType.IsTRENDTrade)  masterTradeType = "TREND";
                else if (masterPosForType.IsRetestTrade) masterTradeType = "RETEST"; // <- before RMA
                else if (masterPosForType.IsRMATrade)    masterTradeType = "RMA";
                else if (masterPosForType.IsMOMOTrade)   masterTradeType = "MOMO";
                else if (masterPosForType.IsFFMATrade)   masterTradeType = "FFMA";
                else                                     masterTradeType = "OR";
            }

            IEnumerable<string> followerEntryNames;
            if (symmetryMasterEntryToDispatch.TryGetValue(masterEntryName, out string dispatchId) &&
                symmetryDispatchById.TryGetValue(dispatchId, out var ctx))
            {
                string[] snapshot;
                snapshot = SymmetryReadFollowers(ctx).ToArray();
                followerEntryNames = snapshot;
            }
            else
            {
                // [BUILD 926 -- Codex P1 Fix]: Fallback type match now uses SignalName parsing.
                //
                // ROOT CAUSE: IsRMATrade=true is stamped on ALL fleet followers (ExecuteSmartDispatchEntry
                // line 434) to enforce point-based trailing. Using IsRMATrade as a type discriminator
                // caused OR followers to fail the !IsRMATrade predicate and be excluded from OR
                // propagation, and incorrectly included in RMA propagation.
                //
                // FIX: Fleet entry names are stamped with the trade type at dispatch time:
                //   Format: "Fleet_<AccountName>_<TRADETYPE>_<Index>"
                //   Example: "Fleet_PA-APEX-422136-05_OR_0", "Fleet_APEX-09_RMA_1"
                //
                // [BUILD 927 -- Codex P2 Fix]: Do NOT use Contains("_TYPE_") -- if an account name
                // itself contains a trade-type substring (e.g. _RMA_, _OR_), Contains() misclassifies
                // the follower by matching the account name token instead of the TRADETYPE segment.
                //
                // SAFE APPROACH: Extract TRADETYPE by segment position.
                // TRADETYPE is always the second-to-last underscore-delimited segment:
                //   lastUnderscore      = before the numeric Index
                //   secondLastUnderscore = before the TRADETYPE token
                // Example: "Fleet_SimApexSim_02_OR_0"
                //   lastUs  -> before "0"    -> remaining = "Fleet_SimApexSim_02_OR"
                //   typeUs  -> before "OR"   -> extracted = "OR"  ?
                var fallback = new List<string>();
                foreach (var kvp in activePositions)
                {
                    if (!kvp.Value.IsFollower || kvp.Value.ExecutingAccount == null) continue;
                    if (masterTradeType == null)
                    {
                        fallback.Add(kvp.Key);
                        continue;
                    }

                    // --- Segment-position extraction ---
                    string sig = kvp.Value.SignalName ?? kvp.Key;
                    string followerType = null;
                    int lastUs = sig.LastIndexOf('_');
                    if (lastUs > 0)
                    {
                        int typeUs = sig.LastIndexOf('_', lastUs - 1);
                        if (typeUs >= 0)
                        {
                            string extracted = sig.Substring(typeUs + 1, lastUs - typeUs - 1);
                            // Validate against known set -- rejects garbage from unusual account names
                            if (extracted == "OR"     || extracted == "RMA"  ||
                                extracted == "TREND"  || extracted == "RETEST" ||
                                extracted == "MOMO"   || extracted == "FFMA"  ||
                                // Build 930 Fix P2: Suffix-marker support -- FFMA_MNL, FFMA_MNL_MKT, OR_RETEST etc.
                                extracted.StartsWith("FFMA_") || extracted.StartsWith("MOMO_") ||
                                extracted.StartsWith("OR_")   || extracted.StartsWith("RMA_")  ||
                                extracted.StartsWith("TREND_") || extracted.StartsWith("RETEST_"))
                                followerType = extracted.Split('_')[0]; // normalize to base type
                        }
                    }

                    // Fallback: segment parsing failed -- use boolean flags (RMA/OR ambiguity defaults to RMA)
                    if (followerType == null)
                    {
                        if      (kvp.Value.IsTRENDTrade)  followerType = "TREND";
                        else if (kvp.Value.IsRetestTrade)  followerType = "RETEST";
                        else if (kvp.Value.IsMOMOTrade)    followerType = "MOMO";
                        else if (kvp.Value.IsFFMATrade)    followerType = "FFMA";
                        else                               followerType = "RMA";
                    }

                    if (followerType == masterTradeType)
                        fallback.Add(kvp.Key);
                }
                followerEntryNames = fallback;
            }

            // --- Step 3: Apply move to each linked follower ---
            foreach (string fleetEntryName in followerEntryNames)
            {
                if (!activePositions.TryGetValue(fleetEntryName, out var pos)) continue;
                if (!pos.IsFollower || pos.ExecutingAccount == null) continue;

                if (isEntryMove)
                {
                    // [FIX-PM-02]: For StopMarket/StopLimit entries limitPrice=0 always; price lives in stopPrice.
                    // Passing newLimit=0 to PropagateMasterEntryMove caused the tick guard to silently no-op
                    // on every user-drag, and historically resubmitted Limit followers at price 0.
                    double effectiveEntryPrice = newLimit > 0 ? newLimit : newStop;
                    if (effectiveEntryPrice <= 0) continue; // both zero -- NT8 callback race, skip safely
                    PropagateMasterEntryMove(fleetEntryName, pos, effectiveEntryPrice, newMasterQty);
                }
                else if (isStopMove)
                    PropagateMasterStopMove(fleetEntryName, pos, newStop);
                else if (isTargetMove)
                    PropagateMasterTargetMove(fleetEntryName, pos, masterTargetNum, newLimit);
            }
            } // end try
            finally
            {
                // [BUILD 924 -- Fix C] Always clear propagation flag, even on exception.
                _propagationActive = false;
            }
        }

        /// <summary>
        /// V12.MOVE-SYNC: Propagate master stop price move to follower.
        /// Delegates to UpdateStopOrder which uses cancel/resubmit via follower Account API
        /// (per V12.10 pattern -- ChangeOrder is master-local only).
        /// </summary>
        private void PropagateMasterStopMove(string fleetEntryName, PositionInfo pos, double newStop)
        {
            if (newStop <= 0) return;
            // [FIX-PM-03]: Skip stop propagation for followers whose entry hasn't filled yet.
            // When the master bracket stop first becomes Working (after master fill), this fires for
            // all dispatched followers. Unfilled followers have no live stop order to move, and the
            // log noise ("Stop move: A -> B" at dispatch time) was incorrectly suggesting a problem.
            if (!pos.EntryFilled) return;
            double roundedStop = Instrument.MasterInstrument.RoundToTickSize(newStop);
            if (Math.Abs(pos.CurrentStopPrice - roundedStop) <= tickSize / 2) return;

            Print(string.Format("[MOVE-SYNC] Stop move: {0} on {1}: {2:F2} -> {3:F2}",
                fleetEntryName, pos.ExecutingAccount.Name, pos.CurrentStopPrice, roundedStop));

            UpdateStopOrder(fleetEntryName, pos, roundedStop, pos.CurrentTrailLevel);
        }

        /// <summary>
        /// V12.MOVE-SYNC: Propagate master target price move to follower via cancel+resubmit.
        /// Mirrors SymmetryGuardReplaceExistingFollowerTarget (Symmetry.cs:504) pattern.
        /// </summary>
        private void PropagateMasterTargetMove(string fleetEntryName, PositionInfo pos, int targetNum, double newLimit)
        {
            if (newLimit <= 0) return;
            var targetDict = GetTargetOrdersDictionary(targetNum);
            if (targetDict == null) return;
            if (!targetDict.TryGetValue(fleetEntryName, out var tOrder) || tOrder == null) return;
            if (tOrder.OrderState != OrderState.Working && tOrder.OrderState != OrderState.Accepted) return;

            double roundedLimit = Instrument.MasterInstrument.RoundToTickSize(newLimit);
            if (Math.Abs(tOrder.LimitPrice - roundedLimit) <= tickSize / 2) return;

            Print(string.Format("[MOVE-SYNC] T{0} move: {1} on {2}: {3:F2} -> {4:F2}",
                targetNum, fleetEntryName, pos.ExecutingAccount.Name, tOrder.LimitPrice, roundedLimit));

            try
            {
                pos.ExecutingAccount.Cancel(new[] { tOrder });

                int qty = tOrder.Quantity;
                OrderAction exitAction = pos.Direction == MarketPosition.Long
                    ? OrderAction.Sell : OrderAction.BuyToCover;
                string signalName = SymmetryTrim("T" + targetNum + "_" + fleetEntryName, 40);

                Order replacement = pos.ExecutingAccount.CreateOrder(
                    Instrument, exitAction, OrderType.Limit, TimeInForce.Gtc,
                    qty, roundedLimit, 0,
                    // [923A-P1b-GUID]: 8-char GUID fragment replaces Ticks -- eliminates collision risk at high resubmit frequency
                    "MGT_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                    signalName, null);

                pos.ExecutingAccount.Submit(new[] { replacement });
                targetDict[fleetEntryName] = replacement;

                Print(string.Format("[MOVE-SYNC] T{0} resubmitted: {1} @ {2:F2}",
                    targetNum, fleetEntryName, roundedLimit));
            }
            catch (Exception ex)
            {
                Print(string.Format("[MOVE-SYNC] ERROR PropagateMasterTargetMove T{0} {1}: {2}",
                    targetNum, fleetEntryName, ex.Message));
            }
        }

        /// <summary>
        /// V12.MOVE-SYNC / 1102Z-D: Propagate master entry price move to follower (pre-fill orders).
        /// Account.Change() removed -- it completes silently on Apex/Tradovate but is a broker-side no-op.
        /// Cancel + CreateOrder + Submit is the sole path, consistent with PropagateMasterTargetMove
        /// and UpdateStopOrder throughout this codebase.
        /// StampReaperMoveGrace() is called before Cancel to open a 5-second REAPER suppression window
        /// covering the cancel gap. REAPER's ChangePending guard (AuditApexPositions line 193) provides
        /// a second layer of protection.
        /// </summary>
        private void PropagateMasterEntryMove(string fleetEntryName, PositionInfo pos, double newLimit, int newMasterQty = 0)
        {
            if (!entryOrders.TryGetValue(fleetEntryName, out var fEntry) || fEntry == null) return;
            if (fEntry.OrderState != OrderState.Working && fEntry.OrderState != OrderState.Accepted) return;

            double roundedLimit = Instrument.MasterInstrument.RoundToTickSize(newLimit);
            // [FIX-PM-02b]: For StopMarket/StopLimit orders price lives in StopPrice (LimitPrice is always 0).
            bool isStopTypeEntry = fEntry.OrderType == OrderType.StopMarket || fEntry.OrderType == OrderType.StopLimit;
            double fEffectivePrice = isStopTypeEntry ? fEntry.StopPrice : fEntry.LimitPrice;

            // [QTY-SYNC]: Scale master quantity for this follower.
            // Fallback to fEntry.Quantity if no quantity signal (pure price-change callback, or qty=0 noise).
            // [923A-P2a-OVF]: checked{} forces explicit OverflowException vs silent int truncation on extreme parity ratios
            // (e.g., 1 NQ -> 10 MES with very high master qty). Clamps to maxContracts on overflow.
            int scaledQty;
            try
            {
                scaledQty = (newMasterQty > 0 && FleetParityMultiplier > 0)
                    ? checked((int)Math.Max(1L, (long)newMasterQty * FleetParityMultiplier))  // [922Z-OVF+923A]: long cast + checked int
                    : fEntry.Quantity;
            }
            catch (OverflowException)
            {
                Print(string.Format("[923A-OVF] Parity scalar overflow for {0} -- clamping to maxContracts ({1})", fleetEntryName, maxContracts));
                scaledQty = maxContracts;
            }

            bool priceChanged    = Math.Abs(fEffectivePrice - roundedLimit) > tickSize / 2;
            bool quantityChanged = scaledQty != fEntry.Quantity;
            if (!priceChanged && !quantityChanged) return;

            Print(string.Format("[MOVE-SYNC] Entry move: {0} on {1}: {2:F2} -> {3:F2} x{4}",
                fleetEntryName, pos.ExecutingAccount.Name, fEffectivePrice, roundedLimit, scaledQty));

            // 1102Z-D: Stamp grace BEFORE cancel -- opens 5-second REAPER suppression window.
            StampReaperMoveGrace();

            // Build 947 FSM: derive master signal name for fill-during-gap detection.
            // Uses same key-contains pattern as cascade cleanup to find the master activePositions entry.
            string masterSignalName = string.Empty;
            foreach (var kvp in activePositions)
            {
                if (!kvp.Value.IsFollower &&
                    (fleetEntryName.Contains(kvp.Key) || kvp.Key.Contains(fleetEntryName)))
                {
                    masterSignalName = kvp.Key;
                    break;
                }
            }

            // Build 947 FSM: two-phase replace -- wait for broker cancel confirmation before resubmit.
            // [GHOST-FIX-1 Build 922Z]: identity chain (fleetEntryName = signal name) preserved in FSM.
            // [FIX-PM-02c]: order type + direction threaded through FSM spec for StopMarket and Short support.
            OrderAction entryAction = pos.Direction == MarketPosition.Long
                ? OrderAction.Buy : OrderAction.SellShort;

            PropagateFollowerEntryReplace(
                fleetEntryName, masterSignalName,
                pos.ExecutingAccount.Name, pos.ExecutingAccount,
                roundedLimit, scaledQty,
                entryAction, fEntry.OrderType, isStopTypeEntry);
        }

        // Build 947: PropagateFollowerEntryReplace -- FSM entry point for two-phase cancel+resubmit.
        // Called from PropagateMasterEntryMove instead of the old inline cancel+submit block.
        // If a replace is already in-flight (PendingCancel or Submitting), ATR ticks are absorbed
        // by updating PendingQty/PendingPrice without firing a second cancel.
        private void PropagateFollowerEntryReplace(
            string fleetEntryName, string masterSignalName,
            string accountName, Account acct,
            double newPrice, int newQty,
            OrderAction entryAction, OrderType entryOrderType, bool isStopType)
        {
            Order currentEntry = null;

            FollowerReplaceSpec existing;
            if (_followerReplaceSpecs.TryGetValue(fleetEntryName, out existing))
            {
                // Already in PendingCancel or Submitting -- absorb ATR tick into latest spec.
                existing.PendingQty   = newQty;
                existing.PendingPrice = newPrice;
                Print("[FSM] Replace spec updated (in-flight): "
                    + fleetEntryName + " qty=" + newQty + " price=" + newPrice);
                return;
            }

            if (!entryOrders.TryGetValue(fleetEntryName, out currentEntry) || currentEntry == null)
            {
                Print("[FSM] SKIP replace: no tracked entry for " + fleetEntryName);
                return;
            }

            var spec = new FollowerReplaceSpec
            {
                State             = FollowerReplaceState.PendingCancel,
                CancellingOrderId = currentEntry.OrderId,
                PendingQty        = newQty,
                PendingPrice      = newPrice,
                AccountName       = accountName,
                SignalName        = fleetEntryName,
                MasterSignalName  = masterSignalName,
                EntryAction       = entryAction,
                EntryOrderType    = entryOrderType,
                IsStopType        = isStopType
            };
            _followerReplaceSpecs[fleetEntryName] = spec;
            SetFsmReplacing(fleetEntryName, currentEntry.OrderId);

            // Cancel outside lock -- currentEntry captured inside lock above
            try
            {
                acct.Cancel(new[] { currentEntry });
                Print("[FSM] Cancel sent for " + fleetEntryName
                    + " OrderId=" + currentEntry.OrderId);
            }
            catch (Exception ex)
            {
                Print("[FSM] Cancel failed for " + fleetEntryName + ": " + ex.Message);
                _followerReplaceSpecs.TryRemove(fleetEntryName, out _);
            }
        }

        // Build 947: SubmitFollowerReplacement -- called on strategy thread via TriggerCustomEvent
        // after broker confirms the PendingCancel. Uses spec fields to preserve direction + order type.
        private void SubmitFollowerReplacement(
            string fleetSignalName, string accountName,
            double price, int qty, FollowerReplaceSpec spec)
        {
            Account acct = null;
            FlattenedSubstrateState m = Volatile.Read(ref _membrane);
            if (m != null && m.AccountIndexByName != null)
            {
                int idx;
                if (m.AccountIndexByName.TryGetValue(accountName, out idx)
                    && idx >= 0 && idx < m.AccountByIndex.Length)
                {
                    acct = m.AccountByIndex[idx];
                }
            }
            if (acct == null)
            {
                acct = Account.All.FirstOrDefault(
                    a => string.Equals(a.Name, accountName, StringComparison.OrdinalIgnoreCase));
            }
            if (acct == null)
            {
                Print("[FSM] SUBMIT FAIL: account not found: " + accountName);
                return;
            }

            // Build 948 [FIX-C]: Defensive expectedPositions re-assertion.
            // If ExecuteFollowerCascadeCleanup ran concurrently before Fix A sealed the gap,
            // DeltaExpectedPositionLocked may have zeroed expectedPositions for this account.
            // Without re-asserting, the replacement fill triggers REAPER Critical Desync:
            //   actualQty != 0, expectedQty == 0 -> Emergency Flatten.
            string _b948ExpKey = ExpKey(accountName);
            int _b948CurrentExp = 0;
            expectedPositions.TryGetValue(_b948ExpKey, out _b948CurrentExp);
            bool _b948ZeroStartReasserted = _b948CurrentExp == 0 && qty != 0;
            if (_b948ZeroStartReasserted)
            {
                int _b948Delta = spec.EntryAction == OrderAction.Buy ? qty : -qty;
                AddExpectedPositionDeltaLocked(_b948ExpKey, _b948Delta);
                MarkDispatchSyncPending(_b948ExpKey);
                Print(string.Format("[FSM-GUARD] Re-asserted expectedPositions for {0}: {1} (cascade decrement detected before replacement submit).",
                    accountName, _b948Delta));
            }

            // [FIX-PM-02c]: preserve order type so StopMarket followers remain StopMarket.
            double limitPx = !spec.IsStopType ? price : 0;
            double stopPx  =  spec.IsStopType ? price : 0;
            string expectedKey = ExpKey(accountName);
            int expectedDelta = 0;
            PositionInfo trackedPos;
            if (!_b948ZeroStartReasserted
                && activePositions.TryGetValue(fleetSignalName, out trackedPos) && trackedPos != null)
            {
                int qtyDiff = qty - trackedPos.TotalContracts;
                if (qtyDiff != 0)
                    expectedDelta = trackedPos.Direction == MarketPosition.Long ? qtyDiff : -qtyDiff;
            }

            // [923A-P1-GUID]: 8-char GUID fragment as ocoId; signal name = fleetSignalName (GHOST-FIX-1).
            Order newEntry = acct.CreateOrder(
                Instrument, spec.EntryAction, spec.EntryOrderType, TimeInForce.Gtc,
                qty, limitPx, stopPx,
                "MGE_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                fleetSignalName, null);

            if (!_b948ZeroStartReasserted && expectedDelta != 0)
            {
                AddExpectedPositionDeltaLocked(expectedKey, expectedDelta);
                Print("[FSM] Replacement expected sync: "
                    + fleetSignalName + " delta=" + expectedDelta);
            }

            try
            {
                acct.Submit(new[] { newEntry });
            }
            catch (Exception submitEx)
            {
                if (!_b948ZeroStartReasserted && expectedDelta != 0)
                    AddExpectedPositionDeltaLocked(expectedKey, -expectedDelta);

                Print("[FSM] SUBMIT FAIL: replacement submit threw for " + fleetSignalName + ": " + submitEx.Message);
                return;
            }

            // B966: wrap dict write + pos mutation in Enqueue so it flows through actor pipeline.
            // Order submission stays outside; captures prevent stale closure refs.
            { var _ne966 = newEntry; var _fsn966 = fleetSignalName; var _qty966 = qty;
            Enqueue(ctx => {
                ctx.entryOrders[_fsn966] = _ne966;
                FollowerBracketFSM fsm966;
                if (!ctx._followerBrackets.TryGetValue(_fsn966, out fsm966) || fsm966 == null)
                {
                    fsm966 = new FollowerBracketFSM
                    {
                        AccountName = accountName,
                        EntryName = _fsn966
                    };
                    ctx._followerBrackets[_fsn966] = fsm966;
                }

                if (!string.IsNullOrEmpty(fsm966.ReplacingCancelOrderId))
                    ctx._orderIdToFsmKey.TryRemove(fsm966.ReplacingCancelOrderId, out _);

                fsm966.EntryOrder = _ne966;
                fsm966.State = FollowerBracketState.Submitted;
                fsm966.ReplacingCancelOrderId = null;
                fsm966.LastUpdateUtc = DateTime.UtcNow;
                if (!string.IsNullOrEmpty(_ne966.OrderId))
                    ctx._orderIdToFsmKey[_ne966.OrderId] = _fsn966;

                // [QTY-SYNC]: Sync PositionInfo to new size so SubmitBracketOrders sum-assertion passes.
                PositionInfo pos966;
                if (ctx.activePositions.TryGetValue(_fsn966, out pos966) && pos966 != null)
                {
                    pos966.TotalContracts     = _qty966;
                    pos966.RemainingContracts = _qty966;
                    int ft1, ft2, ft3, ft4, ft5;
                    ctx.GetTargetDistribution(_qty966, out ft1, out ft2, out ft3, out ft4, out ft5);
                    pos966.T1Contracts = ft1;
                    pos966.T2Contracts = ft2;
                    pos966.T3Contracts = ft3;
                    pos966.T4Contracts = ft4;
                    pos966.T5Contracts = ft5;
                }
            }); }

            Print("[FSM] Replacement submitted: " + fleetSignalName
                + " @ " + price + " x" + qty);
        }

        // B957/C1: SubmitFollowerTargetReplacement -- called on strategy thread via TriggerCustomEvent
        // after broker confirms the PendingCancel of a follower target order (two-phase FSM for targets).
        private void SubmitFollowerTargetReplacement(string tFsmKey, FollowerTargetReplaceSpec spec)
        {
            var tDict = GetTargetOrdersDictionary(spec.TargetNum);
            Order newTargetOrder = null;
            try
            {
                newTargetOrder = spec.TargetAccount.CreateOrder(
                    Instrument, spec.ExitAction, OrderType.Limit, TimeInForce.Gtc,
                    spec.Quantity, spec.NewTargetPrice, 0, "",
                    "T" + spec.TargetNum + "_" + spec.EntryName, null);
            }
            catch (Exception createEx)
            {
                Print("[FSM_TGT] CreateOrder threw for " + tFsmKey + ": " + createEx.Message);
                return;
            }
            if (newTargetOrder == null)
            {
                Print("[FSM_TGT] CreateOrder returned null for " + tFsmKey + " -- position may be unprotected.");
                return;
            }
            try { spec.TargetAccount.Submit(new[] { newTargetOrder }); }
            catch (Exception submitEx)
            {
                Print("[FSM_TGT] Submit threw for " + tFsmKey + ": " + submitEx.Message);
                return;
            }
            if (tDict != null) tDict[spec.EntryName] = newTargetOrder;
            Print("[FSM_TGT] Target replacement submitted: T" + spec.TargetNum + " for " + spec.EntryName + " -> " + spec.NewTargetPrice);
        }


        #endregion
    }
}
