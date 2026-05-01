// Phase 3: Strategy factories and dispatch decoupling contracts.
using System;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class V12_002 : Strategy
    {
        private interface IFleetAccountFilter
        {
            bool ShouldSkip(Account account, AccountRankInfo rankInfo, System.Collections.Generic.HashSet<string> activeAccountSnapshot);
        }

        private interface IFollowerPricingEngine
        {
            void ComputeFollowerPrices(
                OrderAction action,
                double entryPrice,
                out double stopPrice,
                out double t1,
                out double t2,
                out double t3,
                out double t4,
                out double t5);
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

        private struct StagedTargetBuffer
        {
            public int Count;
            public StagedTarget T1;
            public StagedTarget T2;
            public StagedTarget T3;
            public StagedTarget T4;
            public StagedTarget T5;

            public void Add(in StagedTarget target)
            {
                if (Count == 0) T1 = target;
                else if (Count == 1) T2 = target;
                else if (Count == 2) T3 = target;
                else if (Count == 3) T4 = target;
                else if (Count == 4) T5 = target;
                Count = Math.Min(5, Count + 1);
            }

            public StagedTarget At(int index)
            {
                if (index == 0) return T1;
                if (index == 1) return T2;
                if (index == 2) return T3;
                if (index == 3) return T4;
                return T5;
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
