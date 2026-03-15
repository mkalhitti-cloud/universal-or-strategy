// V12.Harness.Shim -- NinjaTrader.Cbi stub types for headless testing
// Build 980 Proof-of-Work Harness
// ASCII-only. No WPF references. Targets net48.
using System;
using System.Collections.Generic;

namespace NinjaTrader.Cbi
{
    // -- Enumerations -------------------------------------------------

    public enum OrderState
    {
        Unknown,
        Submitted,
        Accepted,
        Working,
        Cancelled,
        Filled,
        Rejected,
        PartFilled,
        PendingCancel,
        PendingChange,
        PendingSubmit
    }

    public enum OrderAction
    {
        Buy,
        Sell,
        BuyToCover,
        SellShort
    }

    public enum OrderType
    {
        Market,
        Limit,
        StopMarket,
        StopLimit
    }

    public enum MarketPosition
    {
        Long,
        Short,
        Flat
    }

    // -- Order --------------------------------------------------------

    public class Order
    {
        public string      Name       { get; set; }
        public string      OrderId    { get; set; }
        public OrderState  OrderState { get; set; }
        public OrderAction Action     { get; set; }
        public OrderType   OrderType  { get; set; }
        public double      LimitPrice { get; set; }
        public double      StopPrice  { get; set; }
        public int         Quantity   { get; set; }
        public Account     Account    { get; set; }
        public Instrument  Instrument { get; set; }

        public static Order Make(string name, OrderState state = OrderState.Working,
            OrderType type = OrderType.Limit, string orderId = null)
        {
            return new Order
            {
                Name       = name,
                OrderId    = orderId ?? Guid.NewGuid().ToString("N"),
                OrderState = state,
                OrderType  = type
            };
        }
    }

    // -- Account -------------------------------------------------------

    public class Account
    {
        public string Name { get; set; }

        // Orders submitted/cancelled through this account stub (for verification)
        public List<Order[]> SubmittedOrderBatches  = new List<Order[]>();
        public List<Order[]> CancelledOrderBatches  = new List<Order[]>();

        public void Cancel(Order[] orders)
        {
            CancelledOrderBatches.Add(orders);
            if (orders != null)
                foreach (var o in orders)
                    if (o != null) o.OrderState = OrderState.Cancelled;
        }

        public void Submit(Order[] orders)
        {
            SubmittedOrderBatches.Add(orders);
        }
    }

    // -- Instrument ---------------------------------------------------

    public class Instrument
    {
        public string FullName { get; set; }

        public static Instrument MES => new Instrument { FullName = "MES 03-26" };
    }

    // -- OrderEventArgs -----------------------------------------------

    public class OrderEventArgs : EventArgs
    {
        public Order Order { get; set; }
    }
}
