// C:\WSGTA\universal-or-strategy\src\PropTraderTools\Features\PttFlatten.cs
// B34 -- PttFlatten module: full close on leader account.
// IPttModule implementation. ModuleId = "FLAT".
// Dependencies: Core/PttContracts.cs + NinjaTrader.Cbi ONLY. NO CopyEngine import.
// JS-021: no lock. JS-033: synchronous void. NT8-006: no LINQ.
// DW-B33-02/04: buffer>0 uses Limit order path (NT8-049: arg6=limitPrice, arg7=0).

using System;
using System.Collections.Generic;
using NinjaTrader.Cbi;

namespace PropTraderTools
{
    /// <summary>
    /// Flatten module. Submits a market close order for the full leader position.
    /// Fires PttBus.FlatFired after execution so PttCopier can fan-out to followers.
    /// </summary>
    public class PttFlatten : IPttModule
    {
        public string ModuleId { get; private set; }
        public bool IsEnabled { get; private set; }

        public PttFlatten()
        {
            ModuleId = "FLAT";
            IsEnabled = true;
        }

        /// <summary>Set enabled state (wired by TradeCopierPanel license bool). CYC=1.</summary>
        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
        }

        /// <summary>No PttBus subscriptions. CYC=1.</summary>
        public void Initialize(IPttHostContext ctx) { }

        /// <summary>No subscriptions to unsubscribe. CYC=1.</summary>
        public void Teardown() { }

        /// <summary>
        /// Execute full close on leader account.
        /// CYC=3: (1) IsEnabled guard, (2) position guard, (3) FlattenPositionLocal + RaiseFlatted.
        /// JS-021: no lock. JS-033: synchronous void.
        /// DW-B33-02/04: passes buffer/ask/bid/tickSize for Limit order path.
        /// </summary>
        public void Execute(IPttHostContext ctx)
        {
            if (!IsEnabled)
                return; // (1)

            Position pos = FindPositionLocal(ctx.LeaderAccount, ctx.Instrument);
            if (pos == null || pos.Quantity == 0)
                return; // (2)

            int buf = ctx.FlatBuffer; // DW-B33-02
            double ask = ctx.Ask;
            double bid = ctx.Bid;
            double tickSize = ctx.Instrument.MasterInstrument.TickSize;
            FlattenPositionLocal(
                ctx.LeaderAccount,
                ctx.Instrument,
                pos, // (3a)
                buf,
                ask,
                bid,
                tickSize
            );

            PttBus.RaiseFlatted(this, new FlatEventArgs(ctx.Instrument)); // (3b)
        }

        // ---------------------------------------------------------------------

        /// <summary>
        /// Submit close order for full position on the given account.
        /// DW-B33-02/04: buffer>0 => Limit order at ask+buf*tick (long) or bid-buf*tick (short).
        /// NT8-049: Limit order -- arg6=limitPrice, arg7=0 -- NEVER SWAP.
        /// NT8-007: arg11 = (NinjaTrader.Cbi.CustomOrder)null.
        /// NT8-013: DateTime.MaxValue.
        /// NT8-014: signal "PTT-Flatten".
        /// CYC=6: (1)(||) acc null, (2)(||) instr null, (3)(||) pos null, (4) direction ternary,
        ///         (5) try/catch, (6) order null check.
        /// </summary>
        private static void FlattenPositionLocal(
            Account acc,
            Instrument instr,
            Position pos,
            int buffer,
            double ask,
            double bid,
            double tickSize
        )
        {
            if (acc == null || instr == null || pos == null)
                return; // (1)(2)(3)

            OrderAction direction =
                pos.MarketPosition == MarketPosition.Long
                    ? OrderAction.Sell
                    : OrderAction.BuyToCover; // (4)

            var (orderType, limitPrice, stopPrice) = ResolveOrderParams(
                pos,
                buffer,
                ask,
                bid,
                tickSize
            );

            try // (5)
            {
                var order = acc.CreateOrder(
                    instr,
                    direction,
                    orderType,
                    OrderEntry.Manual,
                    TimeInForce.Gtc,
                    pos.Quantity, // full qty
                    limitPrice, // arg6: limitPrice (NT8-049)
                    stopPrice, // arg7: stopPrice  (NT8-049)
                    string.Empty, // arg8: oco group
                    "PTT-Flatten", // arg9: signal name (NT8-014)
                    DateTime.MaxValue, // arg10: gtd (NT8-013)
                    (NinjaTrader.Cbi.CustomOrder)null
                ); // arg11 (NT8-007)
                if (order != null) // (6)
                {
                    acc.Submit(new[] { order });
                    NinjaTrader.Code.Output.Process(
                        "[FLAT] FlattenPositionLocal "
                            + orderType
                            + " "
                            + direction
                            + " "
                            + pos.Quantity
                            + " @ "
                            + (orderType == OrderType.Limit ? limitPrice.ToString("F2") : "mkt")
                            + " on "
                            + acc.Name,
                        NinjaTrader.NinjaScript.PrintTo.OutputTab1
                    );
                }
            }
            catch (Exception ex)
            {
                NinjaTrader.Code.Output.Process(
                    "[FLAT] FlattenPositionLocal EXCEPTION on "
                        + (acc != null ? acc.Name : "null")
                        + ": "
                        + ex.Message,
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
            }
        }

        /// <summary>
        /// Compute order type, limit price, and stop price for flatten close order.
        /// Structurally identical to PttTrim.ResolveOrderParams -- same extraction pattern.
        /// CYC=5: (1) tickSize>0, (2) && (Long?ask:bid)>0, (3) isLong ternary in useLimitOrder,
        ///         (4) if(useLimitOrder) branch, (5) MarketPosition ternary for limitPrice.
        /// JS-002: returns value tuple (never null). JS-001: no throw. JS-021: no lock. ASCII-only.
        /// NT8-049: Limit orderType uses limitPrice in arg6, stopPrice=0 in arg7 (preserved in caller).
        /// </summary>
        private static (
            OrderType orderType,
            double limitPrice,
            double stopPrice
        ) ResolveOrderParams(Position pos, int buffer, double ask, double bid, double tickSize)
        {
            bool useLimitOrder =
                tickSize > 0.0 // (1)
                && (pos.MarketPosition == MarketPosition.Long ? ask > 0.0 : bid > 0.0); // (2)(3)

            if (useLimitOrder) // (4)
            {
                // Long sell limit: ask - buffer*tick (aggressive taker). Short buy-to-cover: bid + buffer*tick.
                // NT8-049: arg6=limitPrice, arg7=0 for Limit orders.
                double lp =
                    pos.MarketPosition == MarketPosition.Long // (5)
                        ? ask - buffer * tickSize
                        : bid + buffer * tickSize;
                return (OrderType.Limit, lp, 0);
            }
            return (OrderType.Market, 0, 0);
        }

        /// <summary>NT8-050: foreach-based position lookup, never acc.Positions[instr]. CYC=2.</summary>
        private static Position FindPositionLocal(Account acc, Instrument instr)
        {
            if (acc == null || instr == null)
                return null;
            foreach (Position p in acc.Positions)
                if (p.Instrument == instr)
                    return p;
            return null;
        }
    }
}
