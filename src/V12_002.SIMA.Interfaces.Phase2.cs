using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace NinjaTrader.NinjaScript.Strategies
{
    internal interface IOrderCommandFactory
    {
        bool Build(
            in AccountDispatchContext context,
            out OrderBuildResult result,
            out string errorMessage);
    }

    internal interface IDispatchRouter
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IOrderCommandFactory Select(OrderType entryOrderType);
    }

    internal readonly struct AccountDispatchContext
    {
        public readonly Account Account;
        public readonly int AccountIndex;
        public readonly string FleetEntryName;
        public readonly string OcoId;
        public readonly string ExpectedKey;

        public readonly double EntryPrice;
        public readonly double StopPrice;
        public readonly double T1Price;
        public readonly double T2Price;
        public readonly double T3Price;
        public readonly double T4Price;
        public readonly double T5Price;

        public readonly int FollowerQty;
        public readonly int FT1;
        public readonly int FT2;
        public readonly int FT3;
        public readonly int FT4;
        public readonly int FT5;

        public readonly OrderAction Action;
        public readonly OrderType EntryOrderType;
        public readonly string TradeType;
        public readonly int DispatchTargetCount;
        public readonly bool IsMarketEntry;

        public readonly string SymmetryDispatchId;

        public AccountDispatchContext(
            Account account,
            int accountIndex,
            string fleetEntryName,
            string ocoId,
            string expectedKey,
            double entryPrice,
            double stopPrice,
            double t1Price, double t2Price, double t3Price, double t4Price, double t5Price,
            int followerQty,
            int ft1, int ft2, int ft3, int ft4, int ft5,
            OrderAction action,
            OrderType entryOrderType,
            string tradeType,
            int dispatchTargetCount,
            bool isMarketEntry,
            string symmetryDispatchId)
        {
            Account = account;
            AccountIndex = accountIndex;
            FleetEntryName = fleetEntryName;
            OcoId = ocoId;
            ExpectedKey = expectedKey;
            EntryPrice = entryPrice;
            StopPrice = stopPrice;
            T1Price = t1Price;
            T2Price = t2Price;
            T3Price = t3Price;
            T4Price = t4Price;
            T5Price = t5Price;
            FollowerQty = followerQty;
            FT1 = ft1;
            FT2 = ft2;
            FT3 = ft3;
            FT4 = ft4;
            FT5 = ft5;
            Action = action;
            EntryOrderType = entryOrderType;
            TradeType = tradeType;
            DispatchTargetCount = dispatchTargetCount;
            IsMarketEntry = isMarketEntry;
            SymmetryDispatchId = symmetryDispatchId;
        }
    }

    internal struct OrderBuildResult
    {
        public Order EntryOrder;
        public Order StopOrder;
        public List<StagedTarget> StagedTargets;
        public PositionInfo PositionInfo;
        public int NonRunnerLimitQty;
        public int RunnerQty;
        public double LimitPx;
        public double StopPx;

        public int TotalOrderCount =>
            1 + (StopOrder != null ? 1 : 0) + (StagedTargets != null ? StagedTargets.Count : 0);
    }

    internal interface IPhotonDispatchChannel
    {
        bool Enqueue(
            in AccountDispatchContext context,
            in OrderBuildResult result,
            ref int reservedDelta);

        void PumpPrime();
    }
}
