// V12.Tests -- T-PL-01: StopLimit Pricing Preservation Regression Suite
// Build 980 Proof-of-Work
// Proves: FSM cancel+resubmit cycle preserves explicit StopLimit pricing (B1 repair).
// ATR tick absorption updates spec in-place; lambda reads at execution time not capture time.
// ASCII-only log assertions.
using System;
using System.Linq;
using NinjaTrader.Cbi;
using Xunit;

namespace V12.Tests
{
    public class T_PL_01_StopLimitPricing
    {
        // -----------------------------------------------------------
        // PL01-A: StopLimit prices preserved through FSM transition
        // -----------------------------------------------------------
        [Fact]
        public void PL01_A_StopLimit_PricesPreserved_ThroughFsmTransition()
        {
            var h = FsmHarness.Create();
            const string signal = "Fleet_Apex1_RETEST_1";
            const string acct   = "Apex1";

            var spec = new FollowerReplaceSpec
            {
                State             = FollowerReplaceState.PendingCancel,
                CancellingOrderId = "order-001",
                PendingQty        = 1,
                PendingLimitPrice = 4800.25,  // B1: explicit pricing set at spec creation
                PendingStopPrice  = 4799.75,
                AccountName       = acct,
                SignalName        = signal,
                MasterSignalName  = "RETEST_1",
                EntryOrderType    = OrderType.StopLimit,
                IsStopType        = true
            };
            h._followerReplaceSpecs[signal] = spec;
            spec.State = FollowerReplaceState.Submitting;

            // Lambda fires synchronously -- no suppression
            h.SimulateFsmReplacementLambda(signal, acct, spec);

            Assert.True(h.SubmitCalled, "SubmitFollowerReplacement must be called.");
            Assert.NotNull(h.LastSubmitSpec);
            // xUnit 2.x: Assert.Equal(expected, actual, precision) for double
            Assert.Equal(4800.25, h.LastSubmitSpec.PendingLimitPrice, 2);
            Assert.Equal(4799.75, h.LastSubmitSpec.PendingStopPrice, 2);
            Assert.Equal(OrderType.StopLimit, h.LastSubmitSpec.EntryOrderType);
        }

        // -----------------------------------------------------------
        // PL01-B: ATR absorption updates spec; lambda reads updated prices
        // -----------------------------------------------------------
        [Fact]
        public void PL01_B_AtrAbsorption_UpdatesSpec_LambdaReadsNewValues()
        {
            var h = FsmHarness.Create();
            const string signal = "Fleet_Apex1_RMA_1";
            const string acct   = "Apex1";

            var spec = new FollowerReplaceSpec
            {
                State             = FollowerReplaceState.PendingCancel,
                CancellingOrderId = "order-002",
                PendingQty        = 2,
                PendingLimitPrice = 4800.25,  // original price at spec creation
                PendingStopPrice  = 4799.75,
                AccountName       = acct,
                SignalName        = signal,
                EntryOrderType    = OrderType.StopLimit,
                IsStopType        = true
            };
            h._followerReplaceSpecs[signal] = spec;

            // Simulate ATR tick update arriving while in PendingCancel:
            // Real code: "while in PendingCancel, additional sizing changes update PendingLimitPrice/PendingStopPrice"
            spec.PendingLimitPrice = 4801.50;  // ATR-adjusted price
            spec.PendingStopPrice  = 4800.75;

            // Confirm the spec still references the SAME object (no stale capture)
            Assert.Same(h._followerReplaceSpecs[signal], spec);

            // Lambda fires -- must read from spec object (not stale capture)
            h.SimulateFsmReplacementLambda(signal, acct, spec);

            Assert.True(h.SubmitCalled);
            Assert.Equal(4801.50, h.LastSubmitSpec.PendingLimitPrice, 2);
            Assert.Equal(4800.75, h.LastSubmitSpec.PendingStopPrice, 2);
        }

        // -----------------------------------------------------------
        // PL01-C: StopMarket -- LimitPrice must remain 0 (no injection)
        // -----------------------------------------------------------
        [Fact]
        public void PL01_C_StopMarket_NoLimitPriceInjection()
        {
            var h = FsmHarness.Create();
            const string signal = "Fleet_Apex1_OR_2";
            const string acct   = "Apex1";

            var spec = new FollowerReplaceSpec
            {
                State             = FollowerReplaceState.PendingCancel,
                CancellingOrderId = "order-003",
                PendingQty        = 1,
                PendingLimitPrice = 0.0,  // StopMarket has no limit price
                PendingStopPrice  = 4799.00,
                AccountName       = acct,
                SignalName        = signal,
                EntryOrderType    = OrderType.StopMarket,
                IsStopType        = true
            };
            h._followerReplaceSpecs[signal] = spec;

            h.SimulateFsmReplacementLambda(signal, acct, spec);

            Assert.True(h.SubmitCalled);
            Assert.Equal(0.0, h.LastSubmitSpec.PendingLimitPrice, 4);
            Assert.Equal(4799.00, h.LastSubmitSpec.PendingStopPrice, 2);
            Assert.Equal(OrderType.StopMarket, h.LastSubmitSpec.EntryOrderType);
        }

        // -----------------------------------------------------------
        // PL01-D: Pure Limit order -- StopPrice must remain 0
        // -----------------------------------------------------------
        [Fact]
        public void PL01_D_LimitOrder_NoStopPriceInjection()
        {
            var h = FsmHarness.Create();
            const string signal = "Fleet_Apex1_OR_3";
            const string acct   = "Apex1";

            var spec = new FollowerReplaceSpec
            {
                State             = FollowerReplaceState.PendingCancel,
                CancellingOrderId = "order-004",
                PendingQty        = 1,
                PendingLimitPrice = 4800.00,
                PendingStopPrice  = 0.0,   // Limit has no stop price
                AccountName       = acct,
                SignalName        = signal,
                EntryOrderType    = OrderType.Limit,
                IsStopType        = false
            };
            h._followerReplaceSpecs[signal] = spec;

            h.SimulateFsmReplacementLambda(signal, acct, spec);

            Assert.True(h.SubmitCalled);
            Assert.Equal(4800.00, h.LastSubmitSpec.PendingLimitPrice, 2);
            Assert.Equal(0.0, h.LastSubmitSpec.PendingStopPrice, 4);
            Assert.Equal(OrderType.Limit, h.LastSubmitSpec.EntryOrderType);
        }

        // -----------------------------------------------------------
        // PL01-E: Spec removed from map after successful submission
        // -----------------------------------------------------------
        [Fact]
        public void PL01_E_SpecRemovedAfterSuccessfulSubmit()
        {
            var h = FsmHarness.Create();
            const string signal = "Fleet_Apex1_FFMA_1";
            const string acct   = "Apex1";

            var spec = new FollowerReplaceSpec
            {
                State             = FollowerReplaceState.PendingCancel,
                PendingQty        = 1,
                PendingLimitPrice = 4802.00,
                AccountName       = acct,
                SignalName        = signal,
                EntryOrderType    = OrderType.Limit
            };
            h._followerReplaceSpecs[signal] = spec;

            h.SimulateFsmReplacementLambda(signal, acct, spec);

            Assert.True(h.SubmitCalled);
            Assert.False(h._followerReplaceSpecs.ContainsKey(signal));
        }
    }
}
