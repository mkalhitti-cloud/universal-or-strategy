// Build 971: SIMA Flatten -- FlattenAllApexAccounts, EmergencyFlattenSingleFleetAccount, ClosePositionsOnlyApexAccounts
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
        #region V12 SIMA Flatten

        private void FlattenAllApexAccounts()
        {
            if (!EnableSIMA)
            {
                Print("[SIMA] DISABLED - Using single-account flatten");
                FlattenAll(); // Call consolidated flatten
                return;
            }

            EnterFlattenScope(); // V12.8: Guard for Reaper + OnAccountExecutionUpdate
            try
            {
                Print("[SIMA] ====== GLOBAL FLATTEN START ======");
                int flattenCount = 0;
                int totalCount = 0;

                // V12.9: Flatten ALL matching accounts regardless of Fleet Manager status.
                // This is a safety mechanism ??" "Flatten All" must always be able to close everything.
                foreach (Account acct in Account.All)
                {
                    if (IsFleetAccount(acct))
                    {
                        totalCount++;
                        try
                        {
                            // [V12.12] Cancel all working orders for this instrument first.
                            // acct.Flatten() is a managed API and silently no-ops in IsUnmanaged=true strategies.
                            List<Order> ordersToCancel = new List<Order>();
                            foreach (Order order in acct.Orders)
                            {
                                if (order.Instrument.FullName == Instrument.FullName &&
                                    (order.OrderState == OrderState.Working || order.OrderState == OrderState.Submitted ||
                                     order.OrderState == OrderState.Accepted || order.OrderState == OrderState.ChangePending ||
                                     order.OrderState == OrderState.ChangeSubmitted))
                                {
                                    ordersToCancel.Add(order);
                                }
                            }
                            if (ordersToCancel.Count > 0)
                            {
                                acct.Cancel(ordersToCancel);
                                Print($"[SIMA] Cancelled {ordersToCancel.Count} working order(s) on {acct.Name}");
                            }

                            // Submit Market close orders for each open position
                            int closedCount = 0;
                            foreach (Position position in acct.Positions)
                            {
                                if (position.MarketPosition == MarketPosition.Flat) continue;
                                int qty = position.Quantity;
                                OrderAction closeAction = position.MarketPosition == MarketPosition.Long
                                    ? OrderAction.Sell
                                    : OrderAction.BuyToCover;
                                string signalName = "Flatten_" + position.MarketPosition.ToString();
                                Order closeOrder = acct.CreateOrder(Instrument, closeAction, OrderType.Market,
                                    TimeInForce.Gtc, qty, 0, 0, "", signalName, null);
                                acct.Submit(new[] { closeOrder });
                                closedCount++;
                            }
                            if (closedCount > 0)
                            {
                                flattenCount++;
                                Print($"[SIMA] [OK] Flattened {closedCount} position(s) on {acct.Name}");
                            }

                            // Reset expected position
                            { var _acct966flat = ExpKey(acct.Name); Enqueue(ctx => ctx.SetExpectedPosition(_acct966flat, 0)); }
                        }
                        catch (Exception ex)
                        {
                            Print($"[SIMA] ??-- FLATTEN FAILED on {acct.Name}: {ex.Message}");
                        }
                    }
                }

                // V12.12: Explicitly flatten the Master account if it was NOT covered by the prefix filter.
                // Bug fix: If Master is "Sim101" and AccountPrefix is "Apex", the loop above skips it entirely.
                bool masterCovered = IsFleetAccount(Account);
                if (!masterCovered)
                {
                    totalCount++;
                    try
                    {
                        // [V12.12] Cancel all working master orders before closing position.
                        List<Order> masterOrdersToCancel = new List<Order>();
                        foreach (Order order in Account.Orders)
                        {
                            if (order.Instrument.FullName == Instrument.FullName &&
                                (order.OrderState == OrderState.Working || order.OrderState == OrderState.Submitted ||
                                 order.OrderState == OrderState.Accepted || order.OrderState == OrderState.ChangePending ||
                                 order.OrderState == OrderState.ChangeSubmitted))
                            {
                                masterOrdersToCancel.Add(order);
                            }
                        }
                        if (masterOrdersToCancel.Count > 0)
                        {
                            Account.Cancel(masterOrdersToCancel);
                            Print($"[SIMA] Cancelled {masterOrdersToCancel.Count} working order(s) on {Account.Name}");
                        }

                        // Submit Market close orders via SubmitOrderUnmanaged for the master account
                        int masterClosedCount = 0;
                        foreach (Position position in Account.Positions)
                        {
                            if (position.MarketPosition == MarketPosition.Flat) continue;
                            int qty = position.Quantity;
                            string signalName = position.MarketPosition == MarketPosition.Long
                                ? "Flatten_MasterLong"
                                : "Flatten_MasterShort";
                            Order masterClose = position.MarketPosition == MarketPosition.Long
                                ? SubmitOrderUnmanaged(0, OrderAction.Sell, OrderType.Market, qty, 0, 0, "", signalName)
                                : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, qty, 0, 0, "", signalName);
                            if (masterClose != null)
                                masterClosedCount++;
                            else
                                Print($"[SIMA] ??-- Master close FAILED (SubmitOrderUnmanaged returned null): {position.MarketPosition} {qty}");
                        }
                        if (masterClosedCount > 0)
                        {
                            flattenCount++;
                            Print($"[SIMA] V12.12 Master flatten: {masterClosedCount} position(s) on {Account.Name} (outside prefix filter)");
                        }

                        { var _acct966mflat = ExpKey(Account.Name); Enqueue(ctx => ctx.SetExpectedPosition(_acct966mflat, 0)); }
                    }
                    catch (Exception ex)
                    {
                        Print($"[SIMA] V12.12 Master FLATTEN FAILED on {Account.Name}: {ex.Message}");
                    }
                }

                Print($"[SIMA] ====== GLOBAL FLATTEN COMPLETE: {flattenCount} flattened across {totalCount} accounts ======");
            }
            finally
            {
                // V12.962 ACTOR: stateLock removed; no monitor to check. Always release guard.
                ExitFlattenScope(); // V12.8: Always release guard, even on exception
            }
        }

        /// <summary>
        /// DEAD-01: Emergency single-account fleet kill. Called when a follower entry fills
        /// AFTER the master order is cancelled (CASCADE-FILLED path). Cancels all working orders
        /// on the instrument for this account, then submits a Market close if a position exists.
        /// Must be called on strategy thread (via TriggerCustomEvent).
        /// </summary>
        private void EmergencyFlattenSingleFleetAccount(Account acct)
        {
            if (acct == null) return;
            Print(string.Format("[DEAD-01] EmergencyFlatten: Initiating kill for {0}", acct.Name));

            try
            {
                // [938-EF-GUARD] Confirm bracket cancellation precedes market close.
                Print(string.Format("[938-EF-GUARD] EF cancelling bracket first: {0}", acct.Name));

                // Step 1: Cancel ALL working orders on this instrument for this account.
                var ordersToCancel = new List<Order>();
                foreach (Order o in acct.Orders)
                {
                    if (o.Instrument.FullName == Instrument.FullName &&
                        (o.OrderState == OrderState.Working    ||
                         o.OrderState == OrderState.Submitted  ||
                         o.OrderState == OrderState.Accepted   ||
                         o.OrderState == OrderState.ChangePending ||
                         o.OrderState == OrderState.ChangeSubmitted))
                    {
                        ordersToCancel.Add(o);
                    }
                }
                if (ordersToCancel.Count > 0)
                {
                    acct.Cancel(ordersToCancel);
                    Print(string.Format("[DEAD-01] EmergencyFlatten: Cancelled {0} working order(s) on {1}.", ordersToCancel.Count, acct.Name));
                }

                // Step 2: Close any live position with a Market order.
                Position pos = acct.Positions.FirstOrDefault(p => p.Instrument.FullName == Instrument.FullName &&
                                                                    p.MarketPosition != MarketPosition.Flat);
                if (pos != null)
                {
                    OrderAction closeAction = pos.MarketPosition == MarketPosition.Long
                        ? OrderAction.Sell          // Close long
                        : OrderAction.BuyToCover;   // Close short

                    Order closeOrder = acct.CreateOrder(
                        Instrument,
                        closeAction,
                        OrderType.Market,
                        TimeInForce.Day,
                        pos.Quantity,
                        0, 0,
                        string.Empty,
                        "Emergency_Flatten_DEAD01",
                        null);
                    acct.Submit(new[] { closeOrder });
                    Print(string.Format("[DEAD-01] EmergencyFlatten: Market {0} {1} submitted on {2}.",
                        closeAction, pos.Quantity, acct.Name));
                }
                else
                {
                    Print(string.Format("[DEAD-01] EmergencyFlatten: {0} already flat -- no close order needed.", acct.Name));
                }

                // Step 3: Clear ghost memory so REAPER does not trigger a second flatten.
                { var _acct966emg = ExpKey(acct.Name); Enqueue(ctx => ctx.SetExpectedPosition(_acct966emg, 0)); }
            }
            catch (Exception ex)
            {
                Print(string.Format("[DEAD-01] EmergencyFlatten ERROR on {0}: {1}", acct.Name, ex.Message));
            }
        }

        private void ClosePositionsOnlyApexAccounts()
        {
            if (!EnableSIMA) return;

            // V12.Phase10 [ZOMBIE-STOP-FIX]: Set isFlattenRunning to suppress REAPER background thread
            // during the naked window between zombie stop cancellation and Market close fill.
            // REAPER.cs L97: `if (isFlattenRunning) continue;` ??" guard already exists; we activate it here.
            // Previously this method did NOT set isFlattenRunning (V12.21 comment). Now it must, because
            // the zombie sweep below creates a transient naked-position window the REAPER would self-heal.
            EnterFlattenScope();
            try
            {
                Print("[SIMA] ====== GLOBAL POSITIONS CLOSE START (System Protection Orders Swept; Limit/Stop Brackets Preserved) ======");
                int closeCount = 0;
                int totalCount = 0;

                foreach (Account acct in Account.All)
                {
                    if (!IsFleetAccount(acct)) continue;

                    totalCount++;
                    try
                    {
                        // -- V12.Phase10 [ZOMBIE-STOP-FIX]: Zombie Sweep ------------------------------
                        // EMERGENCY_STOP_ orders are submitted by REAPER to guard naked positions.
                        // They are NOT OCO-linked and NOT part of any bracket structure, so they survive
                        // FLATTEN_ONLY and become Zombies ??" reversal-fill risk after the position closes.
                        // Stop_*, S_*, T*_ are legitimate bracket stops/targets: intentionally preserved.
                        List<Order> zombieOrders = new List<Order>();
                        foreach (Order order in acct.Orders)
                        {
                            if (order.Instrument.FullName == Instrument.FullName &&
                                (order.OrderState == OrderState.Working       ||
                                 order.OrderState == OrderState.Submitted     ||
                                 order.OrderState == OrderState.Accepted      ||
                                 order.OrderState == OrderState.ChangePending ||
                                 order.OrderState == OrderState.ChangeSubmitted) &&
                                order.Name.StartsWith("EMERGENCY_STOP_", StringComparison.OrdinalIgnoreCase))
                            {
                                zombieOrders.Add(order);
                            }
                        }
                        if (zombieOrders.Count > 0)
                        {
                            acct.Cancel(zombieOrders); // V12.Phase10 [ZOMBIE-STOP-FIX]
                            Print($"[SIMA][ZOMBIE-STOP-FIX] {acct.Name}: swept {zombieOrders.Count} system protection order(s). Deck cleared.");
                        }
                        // -----------------------------------------------------------------------------

                        foreach (Position position in acct.Positions)
                        {
                            if (position.Instrument.FullName != Instrument.FullName) continue;
                            if (position.MarketPosition == MarketPosition.Flat) continue;

                            int qty = position.Quantity;
                            OrderAction closeAction = position.MarketPosition == MarketPosition.Long
                                ? OrderAction.Sell
                                : OrderAction.BuyToCover;
                            string signalName = "GracefulClose_" + position.MarketPosition.ToString();
                            Order closeOrder = acct.CreateOrder(Instrument, closeAction, OrderType.Market,
                                TimeInForce.Gtc, qty, 0, 0, "", signalName, null);
                            acct.Submit(new[] { closeOrder });
                            closeCount++;
                            Print($"[SIMA] [OK] Graceful Close: {qty} {position.MarketPosition} on {acct.Name}");
                        }

                        { var _acct966cpo = ExpKey(acct.Name); Enqueue(ctx => ctx.SetExpectedPosition(_acct966cpo, 0)); }
                    }
                    catch (Exception ex)
                    {
                        Print($"[SIMA] (!) CLOSE FAILED on {acct.Name}: {ex.Message}");
                    }
                }

                // Master account fallback (if not covered by AccountPrefix filter)
                bool masterCovered = IsFleetAccount(Account);
                if (!masterCovered && Account.Positions.Count > 0)
                {
                    // V12.Phase10 [ZOMBIE-STOP-FIX]: Same zombie sweep for master account path.
                    List<Order> masterZombieOrders = new List<Order>();
                    foreach (Order order in Account.Orders)
                    {
                        if (order.Instrument.FullName == Instrument.FullName &&
                            (order.OrderState == OrderState.Working       ||
                             order.OrderState == OrderState.Submitted     ||
                             order.OrderState == OrderState.Accepted      ||
                             order.OrderState == OrderState.ChangePending ||
                             order.OrderState == OrderState.ChangeSubmitted) &&
                            order.Name.StartsWith("EMERGENCY_STOP_", StringComparison.OrdinalIgnoreCase))
                        {
                            masterZombieOrders.Add(order);
                        }
                    }
                    if (masterZombieOrders.Count > 0)
                    {
                        Account.Cancel(masterZombieOrders); // V12.Phase10 [ZOMBIE-STOP-FIX]
                        Print($"[SIMA][ZOMBIE-STOP-FIX] {Account.Name} (master): swept {masterZombieOrders.Count} system protection order(s). Deck cleared.");
                    }

                    foreach (Position position in Account.Positions)
                    {
                        if (position.Instrument.FullName != Instrument.FullName) continue;
                        if (position.MarketPosition == MarketPosition.Flat) continue;

                        int qty = position.Quantity;
                        Order masterClose = position.MarketPosition == MarketPosition.Long
                            ? SubmitOrderUnmanaged(0, OrderAction.Sell,       OrderType.Market, qty, 0, 0, "", "GracefulClose_MasterLong")
                            : SubmitOrderUnmanaged(0, OrderAction.BuyToCover, OrderType.Market, qty, 0, 0, "", "GracefulClose_MasterShort");
                        if (masterClose != null)
                        {
                            closeCount++;
                            Print($"[SIMA] [OK] Graceful Close: Master {qty} {position.MarketPosition}");
                        }
                        else
                        {
                            Print($"[SIMA] ??-- Graceful Close FAILED: Master {qty} {position.MarketPosition} (SubmitOrderUnmanaged returned null)");
                        }
                    }
                    { var _acct966cpm = ExpKey(Account.Name); Enqueue(ctx => ctx.SetExpectedPosition(_acct966cpm, 0)); }
                }

                Print($"[SIMA] ====== GLOBAL POSITIONS CLOSE COMPLETE: {closeCount} positions closed ======");
            }
            finally
            {
                // V12.Phase10 [ZOMBIE-STOP-FIX]: Always release REAPER suppression, even on exception.
                // Mirrors FlattenAllApexAccounts() finally pattern (SIMA.cs L1274).
                ExitFlattenScope();
            }
        }


        #endregion
    }
}
