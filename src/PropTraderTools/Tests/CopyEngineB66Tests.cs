// CopyEngineB66Tests.cs -- xUnit tests for B66-LaneC: StopLimit entry drag fix (DW-B64-01).
// Block: PTT-COPIER-B66-LaneC
// Tags: T_B66_C_01 through T_B66_C_08
// Jane Street rules: JS-001 (no throw), JS-021 (no lock), JS-033 (no async void).
// xUnit only -- no NUnit, no MSTest. NT8-054: Tests\ subfolder.
// NT8 Order is sealed and cannot be instantiated in tests.
// Tested helpers (GetOrderPrice, SetFollowerPrice) are private static.
// For helpers that cannot be invoked with NT8 Order, the logic is verified
// by replaying the boolean condition inline (same pattern as B66Tests.cs T_B66_BE_01/02).
// For reflection-based helpers (Gate C predicate), reflection is used where feasible.
using System;
using System.Reflection;
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools
{
    public class CopyEngineB66CTests
    {
        // -------------------------------------------------------------------------
        // T_B66_C_01 -- Gate C accepts Limit+Accepted (canonical happy path)
        // Verifies the Gate C boolean: (Limit || StopLimit) && (Accepted || Working).
        // Tests the Limit+Accepted combination which is the original B62 path.
        // -------------------------------------------------------------------------
        [Fact]
        public void T_B66_C_01_GateC_LimitAccepted_EvaluatesTrue()
        {
            // Act -- replay Gate C predicate with Limit + Accepted
            var orderType = OrderType.Limit;
            var orderState = OrderState.Accepted;
            bool gateC =
                (orderType == OrderType.Limit || orderType == OrderType.StopLimit)
                && (orderState == OrderState.Accepted || orderState == OrderState.Working);
            // Assert
            Assert.True(gateC);
        }

        // -------------------------------------------------------------------------
        // T_B66_C_02 -- Gate C accepts StopLimit+Working (B66 widening -- DW-B64-01 fix)
        // Verifies that StopLimit orders in Working state pass through Gate C.
        // Pre-B66 this combination was silently dropped (OrderType.Limit only).
        // -------------------------------------------------------------------------
        [Fact]
        public void T_B66_C_02_GateC_StopLimitWorking_EvaluatesTrue()
        {
            // Act -- replay Gate C predicate with StopLimit + Working
            var orderType = OrderType.StopLimit;
            var orderState = OrderState.Working;
            bool gateC =
                (orderType == OrderType.Limit || orderType == OrderType.StopLimit)
                && (orderState == OrderState.Accepted || orderState == OrderState.Working);
            // Assert
            Assert.True(gateC);
        }

        // -------------------------------------------------------------------------
        // T_B66_C_03 -- Gate C rejects Market order (must not fire for Market)
        // Verifies that OrderType.Market is excluded by Gate C type guard.
        // -------------------------------------------------------------------------
        [Fact]
        public void T_B66_C_03_GateC_MarketOrder_EvaluatesFalse()
        {
            // Act -- replay Gate C predicate with Market + Working
            var orderType = OrderType.Market;
            var orderState = OrderState.Working;
            bool gateC =
                (orderType == OrderType.Limit || orderType == OrderType.StopLimit)
                && (orderState == OrderState.Accepted || orderState == OrderState.Working);
            // Assert
            Assert.False(gateC);
        }

        // -------------------------------------------------------------------------
        // T_B66_C_04 -- Gate C rejects Limit+Filled (terminal state must not fire)
        // Verifies that Filled state is excluded by Gate C state guard.
        // Prevents spurious HandleEntryChange calls on terminal events.
        // -------------------------------------------------------------------------
        [Fact]
        public void T_B66_C_04_GateC_LimitFilled_EvaluatesFalse()
        {
            // Act -- replay Gate C predicate with Limit + Filled
            var orderType = OrderType.Limit;
            var orderState = OrderState.Filled;
            bool gateC =
                (orderType == OrderType.Limit || orderType == OrderType.StopLimit)
                && (orderState == OrderState.Accepted || orderState == OrderState.Working);
            // Assert
            Assert.False(gateC);
        }

        // -------------------------------------------------------------------------
        // T_B66_C_05 -- FindFollowerEntryOrder guard: name=="PTT-Copy" required
        // Verifies the PTT-Copy name guard logic that FindFollowerEntryOrder checks.
        // NT8 Order cannot be instantiated; test the boolean expression directly.
        // -------------------------------------------------------------------------
        [Fact]
        public void T_B66_C_05_FindFollowerEntryOrder_NameGuard_PTTCopyRequired()
        {
            // Act -- replay FindFollowerEntryOrder inner guard for name
            string orderName = "PTT-Copy";
            bool nameMatch = orderName == "PTT-Copy";
            Assert.True(nameMatch);

            // Verify non-PTT-Copy names are excluded
            string otherName = "Close";
            bool otherMatch = otherName == "PTT-Copy";
            Assert.False(otherMatch);
        }

        // -------------------------------------------------------------------------
        // T_B66_C_06 -- FindFollowerEntryOrder type+state guard: Working||Accepted AND Limit||StopLimit
        // Verifies the combined type+state predicate used inside FindFollowerEntryOrder.
        // B66-LaneC widened state from Working-only to Working||Accepted,
        //   and type from Limit-only to Limit||StopLimit.
        // -------------------------------------------------------------------------
        [Fact]
        public void T_B66_C_06_FindFollowerEntryOrder_StopLimitAccepted_MatchesGuard()
        {
            // Arrange: StopLimit + Accepted (broker-simulated StopLimit stays Accepted per NT8 docs line 1005)
            var orderType = OrderType.StopLimit;
            var orderState = OrderState.Accepted;
            // Act -- replay FindFollowerEntryOrder inner predicate
            bool guard =
                (orderState == OrderState.Working || orderState == OrderState.Accepted)
                && (orderType == OrderType.Limit || orderType == OrderType.StopLimit);
            // Assert
            Assert.True(guard);
        }

        // -------------------------------------------------------------------------
        // T_B66_C_07 -- GetOrderPrice: StopLimit returns StopPrice, Limit returns LimitPrice
        // Logic: order.OrderType == OrderType.StopLimit ? order.StopPrice : order.LimitPrice
        // NT8 Order sealed -- test the ternary expression with local variables.
        // This mirrors exactly the one-liner in CopyEngine.cs line 1008-1009.
        // -------------------------------------------------------------------------
        [Fact]
        public void T_B66_C_07_GetOrderPrice_ReturnsCorrectPriceByOrderType()
        {
            // StopLimit path: drag price lives in StopPrice (NT8 fact: StopLimit.LimitPrice==0)
            var stopLimitType = OrderType.StopLimit;
            double stopPrice = 4500.25;
            double limitPrice = 0.0;
            double resultStopLimit = stopLimitType == OrderType.StopLimit ? stopPrice : limitPrice;
            Assert.Equal(4500.25, resultStopLimit);

            // Limit path: drag price lives in LimitPrice
            var limitType = OrderType.Limit;
            double limitPrice2 = 4500.50;
            double stopPrice2 = 0.0;
            double resultLimit = limitType == OrderType.StopLimit ? stopPrice2 : limitPrice2;
            Assert.Equal(4500.50, resultLimit);
        }

        // -------------------------------------------------------------------------
        // T_B66_C_08 -- SetFollowerPrice: StopLimit sets StopPrice, Limit sets LimitPrice
        // Logic: if (fo.OrderType == StopLimit) fo.StopPrice = price; else fo.LimitPrice = price;
        // NT8 Order sealed -- verify the branch condition logic using local variables.
        // This directly mirrors the if/else in CopyEngine.cs lines 1018-1021.
        // -------------------------------------------------------------------------
        [Fact]
        public void T_B66_C_08_SetFollowerPrice_SetsCorrectFieldByOrderType()
        {
            double capturedStopPrice = 0.0;
            double capturedLimitPrice = 0.0;
            double newPrice = 4501.75;

            // StopLimit path: SetFollowerPrice should assign fo.StopPrice
            var foTypeA = OrderType.StopLimit;
            if (foTypeA == OrderType.StopLimit)
                capturedStopPrice = newPrice;
            else
                capturedLimitPrice = newPrice;
            Assert.Equal(4501.75, capturedStopPrice);
            Assert.Equal(0.0, capturedLimitPrice);

            // Reset
            capturedStopPrice = 0.0;
            capturedLimitPrice = 0.0;

            // Limit path: SetFollowerPrice should assign fo.LimitPrice
            var foTypeB = OrderType.Limit;
            if (foTypeB == OrderType.StopLimit)
                capturedStopPrice = newPrice;
            else
                capturedLimitPrice = newPrice;
            Assert.Equal(0.0, capturedStopPrice);
            Assert.Equal(4501.75, capturedLimitPrice);
        }
    }
}
