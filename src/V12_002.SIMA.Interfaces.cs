using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        private interface IDispatchGate
        {
            bool CanDispatch(ref DispatchContext ctx, IGateContext gateContext);
        }

        private interface IGateContext
        {
            FlattenedSubstrateState Membrane { get; }
        }

        private interface IFleetResolver
        {
            bool TryResolve(ref DispatchContext ctx, IFleetContext fleetContext);
        }

        private interface IFleetContext
        {
            FlattenedSubstrateState Membrane { get; }
            Account[] Accounts { get; }
        }

        private interface IDispatchRouter
        {
            bool TryRoute(ref DispatchContext ctx, FlattenedSubstrateState membrane, out int accountIndex);
        }

        private interface IPhotonEnqueuer
        {
            bool TryEnqueue(ref DispatchContext ctx, int accountIndex, int poolRecordId);
        }

        private interface IDispatchOrchestrator
        {
            bool TryDispatch(ref DispatchContext ctx, IGateContext gateContext, IFleetContext fleetContext);
        }

        private ref struct DispatchContext
        {
            public ulong NameHash;
            public string FleetEntryName;
            public Account Account;
            public int Quantity;
            public int TargetCount;
            public double EntryPrice;
            public double StopPrice;
            public OrderAction Action;
        }

        private bool TryGetAccountIndexByNameHash(FlattenedSubstrateState membrane, ulong nameHash, out int accountIndex)
        {
            accountIndex = -1;
            if (membrane == null || membrane.AccountIndexByNameHash == null)
                return false;
            return membrane.AccountIndexByNameHash.TryGetValue(nameHash, out accountIndex);
        }
    }
}
