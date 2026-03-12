// V12 REAPER Repair Engine -- Re-issues missed entry orders for desynced follower accounts
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
        #region V12 REAPER Repair Engine

        /// <summary>
        /// V12.Phase8.2: Processes queued repair requests on the strategy thread.
        /// Re-issues the original entry order for a desynced follower account.
        /// Build 935: Per-repair logic extracted to ExecuteReaperRepair (CS-R1140 compliance).
        /// </summary>
        private void ProcessReaperRepairQueue()
        {
            string accountName;
            while (_reaperRepairQueue.TryDequeue(out accountName))
                ExecuteReaperRepair(accountName);
        }

        // Build 935 [REAPER-B935-005]: Single-repair body extracted from ProcessReaperRepairQueue.
        // Threading: runs on the strategy thread from the actor-enqueued REAPER audit flow.
        private void ExecuteReaperRepair(string accountName)
        {
            string repairKey = accountName + "_" + Instrument.FullName;
            try
            {
                // A3-2: Abort immediately if a flatten is in progress (Build 960 audit fix)
                if (isFlattenRunning)
                {
                    Print("[REAPER REPAIR] Aborted -- flatten in progress.");
                    return;
                }

                // 1. Find the stored PositionInfo for this account in activePositions
                PositionInfo repairPos = null;
                string repairEntryName = null;
                foreach (var kvp in activePositions.ToArray())
                {
                    PositionInfo pi = kvp.Value;
                    if (pi.IsFollower && pi.ExecutingAccount != null
                        && pi.ExecutingAccount.Name == accountName)
                    {
                        repairPos = pi;
                        repairEntryName = kvp.Key;
                        break;
                    }
                }

                if (repairPos == null)
                {
                    int orphanCount = _reaperOrphanRepairCount.AddOrUpdate(accountName, 1, (k, v) => v + 1);
                    Print(string.Format("[REAPER REPAIR] x No PositionInfo found for {0} -- cannot repair. (orphan attempt {1}/3)",
                        accountName, orphanCount));

                    if (orphanCount >= 3)
                    {
                        Print(string.Format("[REAPER] SELF-HEAL: {0} has no PositionInfo after 3 attempts. Force-zeroing expectedPositions to unblock repair loop.",
                            accountName));
                        // SetExpectedPosition(..., 0) already removes from _dispatchSyncPendingExpKeys internally.
                        SetExpectedPosition(ExpKey(accountName), 0);
                        _reaperOrphanRepairCount.TryRemove(accountName, out _);
                    }
                    return;
                }

                // Clear orphan counter on successful PositionInfo resolution
                _reaperOrphanRepairCount.TryRemove(accountName, out _);

                OrderType repairOrderType = repairPos.EntryOrderType;
                double repairEntryPrice = Instrument.MasterInstrument.RoundToTickSize(repairPos.EntryPrice);
                double repairLimitPrice = 0;
                double repairStopPrice = 0;

                if (repairOrderType == OrderType.Limit)
                {
                    repairLimitPrice = repairEntryPrice;
                }
                else if (repairOrderType == OrderType.StopMarket)
                {
                    repairStopPrice = repairEntryPrice;
                }
                else if (repairOrderType == OrderType.StopLimit)
                {
                    repairLimitPrice = repairEntryPrice;
                    repairStopPrice = repairEntryPrice;
                }

                // Build 935: hard risk gate for ALL repair order types.
                // Repairs must remain inside the tighter of ATR-derived distance and tick fence distance.
                double currentPrice = lastKnownPrice > 0 ? lastKnownPrice : Close[0];
                if (currentPrice <= 0)
                {
                    Print($"[REAPER] REPAIR BLOCKED: invalid currentPrice={currentPrice:F4} for {accountName}.");
                    return;
                }

                if (!TryGetRepairDistanceLimitPoints(out double repairLimitPoints))
                {
                    Print($"[REAPER] REPAIR BLOCKED: unable to derive repair distance bound for {accountName}.");
                    return;
                }

                double hardBoundDiff = Math.Abs(currentPrice - repairEntryPrice);
                if (hardBoundDiff > repairLimitPoints)
                {
                    Print($"[REAPER] REPAIR BLOCKED: {accountName} {repairOrderType} exceeds hard bound. " +
                          $"Current={currentPrice:F2}, Entry={repairEntryPrice:F2}, Diff={hardBoundDiff:F4} > Limit={repairLimitPoints:F4}.");
                    return;
                }

                // 2. Safety Fence: enforce only when repair submits a Market order.
                if (repairOrderType == OrderType.Market)
                {
                    // Legacy market-fence check retained as a secondary guard.
                    double priceDiff = Math.Abs(currentPrice - repairEntryPrice);
                    double fenceDistance = RepairTickFence * tickSize;

                    if (priceDiff > fenceDistance)
                    {
                        Print($"[REAPER] REPAIR BLOCKED: Price fence exceeded for {accountName}. " +
                              $"Current={currentPrice:F2}, Entry={repairEntryPrice:F2}, " +
                              $"Diff={priceDiff:F4} > Fence={fenceDistance:F4} ({RepairTickFence} ticks). " +
                              $"Adjust RepairTickFence if you want to force entry.");
                        return;
                    }
                }

                // 3. Resolve account object
                Account targetAcct = repairPos.ExecutingAccount;
                if (targetAcct == null)
                {
                    Print($"[REAPER REPAIR] [FAIL] ExecutingAccount is null for {accountName}"); // [Build 969]
                    return;
                }

                // 4. In-flight was already set on the background thread before TriggerCustomEvent (A3-2)
                // 5. Re-issue entry order using the SIMA acct.CreateOrder + acct.Submit pattern
                OrderAction action = repairPos.Direction == MarketPosition.Long
                    ? OrderAction.Buy : OrderAction.SellShort;
                int quantity = repairPos.TotalContracts;
                string repairSignal = repairEntryName;

                Order repairEntry = targetAcct.CreateOrder(
                    Instrument,
                    action,
                    repairOrderType,
                    TimeInForce.Gtc,
                    quantity,
                    repairLimitPrice,
                    repairStopPrice,
                    "",
                    repairSignal,
                    null);

                if (repairEntry == null)
                {
                    Print($"[REAPER REPAIR] [FAIL] CreateOrder returned null for {accountName}"); // [Build 969]
                    return;
                }

                // V12.Phase8.2 [RACE-GUARD]: Re-verify expectedPositions immediately before order submission.
                int currentExpected = 0;
                expectedPositions.TryGetValue(ExpKey(accountName), out currentExpected);
                if (currentExpected == 0)
                {
                    Print($"[REAPER REPAIR] (!) RACE GUARD ABORT for {accountName}: " +
                          $"expectedPositions cleared to 0 while repair was in queue. Discarding repair order.");
                    return;
                }

                repairPos.BracketSubmitted = false;
                // B966: reaperThread -- Enqueue not applicable (would drain on wrong thread).
                // ConcurrentDictionary single-write is inherently thread-safe.
                entryOrders[repairEntryName] = repairEntry;

                targetAcct.Submit(new[] { repairEntry });

                Print($"[REAPER REPAIR] [OK] Repair order submitted for {accountName} under key={repairEntryName}: " + // [Build 969]
                      $"{action} {quantity} {repairOrderType} " +
                      $"{(repairOrderType == OrderType.Market ? "@ Market" : "@ " + repairEntryPrice.ToString("F2"))} " +
                      $"(original entry={repairEntryPrice:F2})");

                // 6. Clear DESYNC chart label
                try
                {
                    string desyncTag = "SIMA_DESYNC_" + accountName;
                    RemoveDrawObject(desyncTag);
                }
                catch { }
            }
            catch (Exception ex)
            {
                Print($"[REAPER REPAIR] [FAIL] FAILED for {accountName}: {ex.Message}"); // [Build 969]
            }
            finally
            {
                // [Build 969.3] - Top-level finally guarantees _repairInFlight cleanup on ALL exit paths.
                _repairInFlight.TryRemove(repairKey, out _);
            }
        }

        #endregion
    }
}
