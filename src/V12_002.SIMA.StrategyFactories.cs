// Phase 3: Strategy factories and dispatch decoupling contracts.
using System;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        private interface IFleetAccountFilter
        {
            bool ShouldSkip(Account account, AccountRankInfo rankInfo, System.Collections.Generic.IReadOnlyCollection<string> activeAccountSnapshot);
        }

        private interface IFollowerPricingEngine
        {
            FollowerPriceEnvelope ComputeFollowerPrices(
                OrderAction action,
                double entryPrice);
        }

        private readonly struct FollowerPriceEnvelope
        {
            public readonly double StopPrice;
            public readonly double T1;
            public readonly double T2;
            public readonly double T3;
            public readonly double T4;
            public readonly double T5;

            public FollowerPriceEnvelope(double stopPrice, double t1, double t2, double t3, double t4, double t5)
            {
                StopPrice = stopPrice;
                T1 = t1;
                T2 = t2;
                T3 = t3;
                T4 = t4;
                T5 = t5;
            }
        }

        private interface IFollowerFSMBuilder
        {
            FollowerBracketFSM BuildPendingSubmit(
                Account account,
                string fleetEntryName,
                int followerQty,
                Order entry,
                Order stop,
                string ocoId,
                in StagedTargetBuffer stagedTargets);
        }

        private readonly struct DispatchOrderEnvelope
        {
            public readonly FleetDispatchSlot Slot;
            public readonly int PoolSlotIndex;

            public DispatchOrderEnvelope(int poolSlotIndex, in FleetDispatchSlot slot)
            {
                Slot = slot;
                PoolSlotIndex = poolSlotIndex;
            }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct StagedTargetBuffer
        {
            public int Count;
            private StagedTarget t1;
            private StagedTarget t2;
            private StagedTarget t3;
            private StagedTarget t4;
            private StagedTarget t5;

            public bool Add(in StagedTarget target)
            {
                if (Count >= 5) return false;

                if (Count == 0) t1 = target;
                else if (Count == 1) t2 = target;
                else if (Count == 2) t3 = target;
                else if (Count == 3) t4 = target;
                else t5 = target;

                Count++;
                return true;
            }

            public StagedTarget At(int index)
            {
                if (index < 0 || index >= Count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                if (index == 0) return t1;
                if (index == 1) return t2;
                if (index == 2) return t3;
                if (index == 3) return t4;
                return t5;
            }
        }

        private readonly struct MarketOrderCommandFactory
        {
            public DispatchOrderEnvelope Create(
                OrderAction action,
                int followerQty,
                double entryPrice,
                double stopPrice,
                int dispatchTargetCount,
                int reservedDelta,
                int poolSlotIndex,
                long signalTicks,
                ulong shadowSalt)
            {
                FleetDispatchSlot slot = new FleetDispatchSlot
                {
                    EntryPrice = entryPrice,
                    StopPrice = stopPrice,
                    SignalTicks = signalTicks,
                    PoolSlotIndex = poolSlotIndex,
                    OrderCount = 2 + dispatchTargetCount,
                    Quantity = followerQty,
                    TargetCount = dispatchTargetCount,
                    Action = (int)action,
                    ReservedDelta = reservedDelta
                };
                slot.Shadow = ComputeFleetDispatchShadow(ref slot, shadowSalt);
                return new DispatchOrderEnvelope(poolSlotIndex: poolSlotIndex, slot: in slot);
            }
        }

        private readonly struct LimitOrderCommandFactory
        {
            public DispatchOrderEnvelope Create(
                OrderAction action,
                int followerQty,
                double limitPrice,
                int reservedDelta,
                int poolSlotIndex,
                long signalTicks,
                ulong shadowSalt)
            {
                FleetDispatchSlot slot = new FleetDispatchSlot
                {
                    EntryPrice = limitPrice,
                    StopPrice = 0d,
                    SignalTicks = signalTicks,
                    PoolSlotIndex = poolSlotIndex,
                    OrderCount = 1,
                    Quantity = followerQty,
                    TargetCount = 0,
                    Action = (int)action,
                    ReservedDelta = reservedDelta
                };
                slot.Shadow = ComputeFleetDispatchShadow(ref slot, shadowSalt);
                return new DispatchOrderEnvelope(poolSlotIndex: poolSlotIndex, slot: in slot);
            }
        }
    }
}
