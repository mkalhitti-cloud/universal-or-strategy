// Build 971: Symmetry.Replace -- SymmetryGuardRetargetExistingFollowerBracket, ReplaceExistingFollowerTarget, SkipFollower
// V12 Symmetry Module (Extracted)
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Strategies;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        #region Symmetry Replace

        private void SymmetryGuardRetargetExistingFollowerBracket(string fleetEntryName, PositionInfo pos)
        {
            UpdateStopOrder(fleetEntryName, pos, pos.CurrentStopPrice, pos.CurrentTrailLevel);
            SymmetryGuardReplaceExistingFollowerTarget(fleetEntryName, pos, 1, target1Orders);
            SymmetryGuardReplaceExistingFollowerTarget(fleetEntryName, pos, 2, target2Orders);
            SymmetryGuardReplaceExistingFollowerTarget(fleetEntryName, pos, 3, target3Orders);
            SymmetryGuardReplaceExistingFollowerTarget(fleetEntryName, pos, 4, target4Orders);
            SymmetryGuardReplaceExistingFollowerTarget(fleetEntryName, pos, 5, target5Orders);
        }

        private void SymmetryGuardReplaceExistingFollowerTarget(
            string fleetEntryName,
            PositionInfo pos,
            int targetNumber,
            ConcurrentDictionary<string, Order> dict)
        {
            if (pos.ExecutingAccount == null) return;
            string targetTag = "T" + targetNumber;
            bool isRunner = IsRunnerTarget(targetNumber);
            bool isFilled = IsTargetFilled(pos, targetNumber);
            int qty = GetTargetContracts(pos, targetNumber);

            if (isFilled || isRunner || qty <= 0)
            {
                if (dict.TryGetValue(fleetEntryName, out var staleTarget) && staleTarget != null)
                {
                    if (staleTarget.OrderState == OrderState.Working ||
                        staleTarget.OrderState == OrderState.Accepted ||
                        staleTarget.OrderState == OrderState.Submitted ||
                        staleTarget.OrderState == OrderState.ChangePending)
                    {
                        pos.ExecutingAccount.Cancel(new[] { staleTarget });
                    }
                    dict.TryRemove(fleetEntryName, out _);
                }
                return;
            }

            if (!dict.TryGetValue(fleetEntryName, out var oldTarget) || oldTarget == null)
                return;

            // Build 1004 [DNA-FIX]: Replace raw Cancel+lock(stateLock)+Submit with FollowerTargetReplaceSpec
            // two-phase FSM. Mirror pattern from Trailing.Breakeven.cs Build 957 C1.
            // Phase 1 (here): store spec and cancel only.
            // Phase 2 (automatic): AccountOrders.cs lines 352-382 detects cancel confirm by CancellingOrderId,
            // fires TriggerCustomEvent -> SubmitFollowerTargetReplacement() in Propagation.cs.
            if (oldTarget.OrderState == OrderState.Working ||
                oldTarget.OrderState == OrderState.Accepted ||
                oldTarget.OrderState == OrderState.Submitted ||
                oldTarget.OrderState == OrderState.ChangePending)
            {
                double newPrice = GetTargetPrice(pos, targetNumber);
                if (newPrice <= 0) return;

                OrderAction exitAction = pos.Direction == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;
                string signalName = SymmetryTrim(targetTag + "_" + fleetEntryName, 40);

                var tSpec = new FollowerTargetReplaceSpec
                {
                    EntryName         = fleetEntryName,
                    TargetNum         = targetNumber,
                    NewTargetPrice    = Instrument.MasterInstrument.RoundToTickSize(newPrice),
                    Quantity          = qty,
                    ExitAction        = exitAction,
                    TargetAccount     = pos.ExecutingAccount,
                    CancellingOrderId = oldTarget.OrderId
                };
                _followerTargetReplaceSpecs[signalName] = tSpec;
                // A1-2: Stamp REAPER grace window before cancel to suppress false desync during replace gap.
                StampReaperMoveGrace();
                pos.ExecutingAccount.Cancel(new[] { oldTarget });
            }
        }

        private void SymmetryGuardSkipFollower(
            string fleetEntryName,
            PositionInfo pos,
            double fleetFillPrice,
            double slippageTicks,
            double slippageUsdPerContract,
            string reason)
        {
            Print(string.Format(
                "[SYMMETRY_GUARD] SKIP | {0} | {1} | FleetFill={2:F2} | Slip={3:F1} ticks (${4:F2}/ct)",
                fleetEntryName, reason, fleetFillPrice, slippageTicks, slippageUsdPerContract));

            // Build 1004 [DNA-FIX]: Replace lock(stateLock) with Enqueue actor write (no internal locks).
            // TotalContracts snapshot captured before lambda to prevent closure mutation.
            int _skipContractsSnap = pos.TotalContracts;
            Enqueue(ctx =>
            {
                pos.EntryFilled = true;
                if (pos.RemainingContracts <= 0)
                    pos.RemainingContracts = Math.Max(1, _skipContractsSnap);
            });

            FlattenPositionByName(fleetEntryName);
            CleanupPosition(fleetEntryName);
            SymmetryGuardForgetEntry(fleetEntryName);
        }

        private void SymmetryGuardTryResolveFollowersForDispatch(string dispatchId, DateTime nowUtc)
        {
            if (string.IsNullOrEmpty(dispatchId))
                return;

            var followersToResolve = new List<string>();

            if (symmetryDispatchById.TryGetValue(dispatchId, out var ctx) && ctx != null)
            {
                    // V1101E HOT-PATCH: Build follower worklist from snapshot; never call stateLock paths while snapshotting.
                    foreach (string fleetEntryName in SymmetryReadFollowers(ctx))
                    {
                        if (string.IsNullOrEmpty(fleetEntryName))
                            continue;

                        if (!symmetryFleetEntryToDispatch.TryGetValue(fleetEntryName, out var linkedDispatch))
                            continue;
                        if (!string.Equals(linkedDispatch, dispatchId, StringComparison.Ordinal))
                            continue;
                        if (!symmetryPendingFollowerFills.ContainsKey(fleetEntryName))
                            continue;

                        followersToResolve.Add(fleetEntryName);
                    }
                }

            // V1101E HOT-PATCH: Preserve legacy dispatch-map scan to catch followers missing from ctx.FollowerEntries.
            foreach (var kvp in symmetryPendingFollowerFills.ToArray())
            {
                string fleetEntryName = kvp.Key;
                if (!symmetryFleetEntryToDispatch.TryGetValue(fleetEntryName, out var linkedDispatch))
                    continue;
                if (!string.Equals(linkedDispatch, dispatchId, StringComparison.Ordinal))
                    continue;
                if (followersToResolve.Contains(fleetEntryName))
                    continue;

                followersToResolve.Add(fleetEntryName);
            }

            foreach (string fleetEntryName in followersToResolve)
            {
                if (!symmetryPendingFollowerFills.TryGetValue(fleetEntryName, out var pending))
                    continue;

                // V12.Phase8 [F-04]: Guard activePositions read with stateLock to prevent
                // torn observations concurrent with ExecuteSmartDispatchEntry commits/removals.
                PositionInfo pos = null;
                activePositions.TryGetValue(fleetEntryName, out pos);
                if (pos != null && pos.IsFollower)
                {
                    if (SymmetryGuardTryResolveFollower(fleetEntryName, pos, pending, nowUtc))
                        symmetryPendingFollowerFills.TryRemove(fleetEntryName, out _);
                }
            }
        }

        /// <summary>
        /// Build 929 Fix3 [P1]: PR #2 Image 3 -- Capture follower list before cleanup.
        /// Cancels all follower entry orders linked to this master BEFORE CleanupPosition
        /// destroys the dispatch map. Without this, followers stay alive as zombie Limit orders.
        /// </summary>
        private void SymmetryGuardCascadeFollowerCleanup(string masterEntryName)
        {
            if (!symmetryMasterEntryToDispatch.TryGetValue(masterEntryName, out string dispatchId)) return;
            if (!symmetryDispatchById.TryGetValue(dispatchId, out var ctx)) return;

            string[] followers;
            followers = SymmetryReadFollowers(ctx).ToArray();

            Print(string.Format("[CASCADE] Master {0} cancelled -- terminating {1} linked follower(s).", masterEntryName, followers.Length));

            foreach (string followerName in followers)
            {
                if (!activePositions.TryGetValue(followerName, out var pos)) continue;
                if (!entryOrders.TryGetValue(followerName, out var order)) continue;
                if (order == null) continue;

                if (order.OrderState == OrderState.Working  ||
                    order.OrderState == OrderState.Submitted ||
                    order.OrderState == OrderState.Accepted)
                {
                    Print(string.Format("[CASCADE] Cancelling follower entry: {0} (Acc: {1})", followerName, pos.ExecutingAccount != null ? pos.ExecutingAccount.Name : "Master"));
                    CancelOrderSafe(order, pos);
                    // A2-3: DeltaExpectedPositionLocked deferred to OnAccountOrderUpdate confirmed-cancel
                    // to prevent REAPER desync if the follower was microseconds from filling (Build 960 audit fix).
                }
            }
        }

        private void SymmetryGuardForgetEntry(string entryName)
        {
            if (string.IsNullOrEmpty(entryName)) return;

            symmetryPendingFollowerFills.TryRemove(entryName, out _);
            symmetryMasterEntryToDispatch.TryRemove(entryName, out _);

            if (symmetryFleetEntryToDispatch.TryRemove(entryName, out var dispatchId) &&
                symmetryDispatchById.TryGetValue(dispatchId, out var ctx))
            {
                SymmetryRemoveFollower(ctx, entryName);
            }
        }

        private void SymmetryGuardPruneDispatches()
        {
            DateTime nowUtc = DateTime.UtcNow;

            foreach (var kvp in symmetryDispatchById.ToArray())
            {
                SymmetryDispatchContext ctx = kvp.Value;
                if (ctx == null) continue;

                bool remove = false;

                if (nowUtc - ctx.CreatedUtc > SymmetryDispatchTtl)
                {
                    remove = true;
                }
                else if (ctx.IsResolved)
                {
                    bool hasActiveFollowers = false;
                                        {
                        foreach (string follower in SymmetryReadFollowers(ctx))
                        {
                            // V12.Phase8 [F-04]: activePositions is a ConcurrentDictionary but
                            // ContainsKey here is used alongside ctx.FollowerEntries iteration under
                            // ctx.Sync -- acquire stateLock for the read to prevent torn observations
                            // when ExecuteSmartDispatchEntry commits or removes entries concurrently.
                            bool exists;
                            exists = activePositions.ContainsKey(follower);
                            if (exists)
                            {
                                hasActiveFollowers = true;
                                break;
                            }
                        }
                    }
                    if (!hasActiveFollowers) remove = true;
                }

                if (remove)
                    symmetryDispatchById.TryRemove(kvp.Key, out _);
            }
        }

        private string SymmetryInferTradeType(string entryName, PositionInfo pos)
        {
            if (pos != null)
            {
                if (pos.IsTRENDTrade) return "TREND";
                if (pos.IsRetestTrade) return "RETEST";
                if (pos.IsFFMATrade) return "FFMA";
                if (pos.IsMOMOTrade) return "MOMO";
                if (pos.IsRMATrade) return "RMA";
            }
            return SymmetryNormalizeTradeType(entryName);
        }

        private string SymmetryNormalizeTradeType(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "GENERIC";

            string t = raw.ToUpperInvariant();
            if (t.StartsWith("TREND", StringComparison.Ordinal)) return "TREND";
            if (t.StartsWith("RETEST", StringComparison.Ordinal)) return "RETEST";
            if (t.StartsWith("FFMA", StringComparison.Ordinal)) return "FFMA";
            if (t.StartsWith("MOMO", StringComparison.Ordinal)) return "MOMO";
            if (t.StartsWith("RMA", StringComparison.Ordinal)) return "RMA";
            if (t.StartsWith("OR", StringComparison.Ordinal) || t.Contains("ORLONG") || t.Contains("ORSHORT")) return "OR";
            return "GENERIC";
        }

        private static string SymmetryTrim(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Length <= maxLen ? text : text.Substring(0, maxLen);
        }

        #endregion
    }
}
