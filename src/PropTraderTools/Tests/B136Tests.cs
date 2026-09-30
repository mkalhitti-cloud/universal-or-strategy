// B136 DW-B148: xUnit tests for OrderPassesBracketGate fused guard.
// Seams used: OrderPassesBracketGateTestable (new), FindFollowerBracketOrderTestable list overload (existing B133).
// All identifiers and string literals ASCII-only.
// No lock(), no async void, no return null (all methods return bool or Order?).
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools.Tests
{
    public class B136Tests
    {
        public class B136Ticket1Tests
        {
            // Helper: creates an Order stub for OrderPassesBracketGateTestable unit tests.
            // Reads: order.FromEntrySignal (signal path), order.Name + order.OrderState + order.OrderType (ATM path).
            // Pattern: direct NinjaTrader.Cbi.Order instantiation (same as B133Tests.cs, B134Tests.cs, B135Tests.cs).
            // Do NOT use Moq or any mocking framework.
            private static Order StubBracketOrder(
                string name,
                string? fromEntrySignal,
                OrderState state,
                OrderType type
            )
            {
                var o = new Order();
                o.Name = name;
                o.FromEntrySignal = fromEntrySignal;
                o.OrderState = state;
                o.OrderType = type;
                return o;
            }

            // ----------------------------------------------------------------
            // Signal path (signalName != null) -- strict exclusivity preserved
            // ----------------------------------------------------------------

            [Fact]
            public void T1_OrderPassesBracketGate_SignalPath_Match_ReturnsTrue()
            {
                // signalName != null -> signal path: exact FromEntrySignal match -> true.
                var order = StubBracketOrder(
                    name: "Target3",
                    fromEntrySignal: "S1",
                    state: OrderState.Working,
                    type: OrderType.Limit
                );
                bool result = CopyEngine.OrderPassesBracketGateTestable(
                    order,
                    "S1",
                    "Target3",
                    isStop: false
                );
                Assert.True(result);
            }

            [Fact]
            public void T1_OrderPassesBracketGate_SignalPath_Mismatch_ReturnsFalse()
            {
                // signalName != null -> signal path: FromEntrySignal="S2" != signalName="S1" -> false.
                var order = StubBracketOrder(
                    name: "Target3",
                    fromEntrySignal: "S2",
                    state: OrderState.Working,
                    type: OrderType.Limit
                );
                bool result = CopyEngine.OrderPassesBracketGateTestable(
                    order,
                    "S1",
                    "Target3",
                    isStop: false
                );
                Assert.False(result);
            }

            // ----------------------------------------------------------------
            // ATM path (signalName == null) -- routes to MatchesLeaderName
            // ----------------------------------------------------------------

            [Fact]
            public void T1_OrderPassesBracketGate_AtmPath_ExactName_ReturnsTrue()
            {
                // ATM path: order.Name=="Target3" == leaderName=="Target3" -> true (first-drag scenario).
                var order = StubBracketOrder(
                    name: "Target3",
                    fromEntrySignal: null,
                    state: OrderState.Working,
                    type: OrderType.Limit
                );
                bool result = CopyEngine.OrderPassesBracketGateTestable(
                    order,
                    null,
                    "Target3",
                    isStop: false
                );
                Assert.True(result);
            }

            [Fact]
            public void T1_OrderPassesBracketGate_AtmPath_PttTgtDrag_ReturnsTrue()
            {
                // THE FIX (DW-B148): second drag -- follower bracket is now named "PTT-TGT-Drag".
                // Before B136 this returned false (SignalOrNameMatches branch 3 rejected it).
                // ATM path routes to MatchesLeaderName -> branch (3): !isStop && order.Name=="PTT-TGT-Drag" -> true.
                var order = StubBracketOrder(
                    name: "PTT-TGT-Drag",
                    fromEntrySignal: null,
                    state: OrderState.Working,
                    type: OrderType.Limit
                );
                bool result = CopyEngine.OrderPassesBracketGateTestable(
                    order,
                    null,
                    "Target3",
                    isStop: false
                );
                Assert.True(result);
            }

            [Fact]
            public void T1_OrderPassesBracketGate_AtmPath_PttStpDrag_ReturnsTrue()
            {
                // Stop fix: second drag -- follower stop bracket is "PTT-STP-Drag".
                // ATM path -> MatchesLeaderName branch (4): isStop && order.Name=="PTT-STP-Drag" -> true.
                var order = StubBracketOrder(
                    name: "PTT-STP-Drag",
                    fromEntrySignal: null,
                    state: OrderState.Working,
                    type: OrderType.StopMarket
                );
                bool result = CopyEngine.OrderPassesBracketGateTestable(
                    order,
                    null,
                    "Stop1",
                    isStop: true
                );
                Assert.True(result);
            }

            [Fact]
            public void T1_OrderPassesBracketGate_AtmPath_PttTgtDrag_StopContext_ReturnsFalse()
            {
                // Type guard: PTT-TGT-Drag must not match in a stop context (isStop=true).
                // ATM path -> MatchesLeaderName: leaderName!= null, name!="Stop1", !isStop=false -> skip (3), isStop=true && name!="PTT-STP-Drag" -> skip (4) -> false.
                var order = StubBracketOrder(
                    name: "PTT-TGT-Drag",
                    fromEntrySignal: null,
                    state: OrderState.Working,
                    type: OrderType.Limit
                );
                bool result = CopyEngine.OrderPassesBracketGateTestable(
                    order,
                    null,
                    "Stop1",
                    isStop: true
                );
                Assert.False(result);
            }

            [Fact]
            public void T1_OrderPassesBracketGate_AtmPath_NullLeaderName_ReturnsTrue()
            {
                // Null leaderName: MatchesLeaderName branch (1) passes through unconditionally -> true.
                var order = StubBracketOrder(
                    name: "AnyOrder",
                    fromEntrySignal: null,
                    state: OrderState.Working,
                    type: OrderType.Limit
                );
                bool result = CopyEngine.OrderPassesBracketGateTestable(
                    order,
                    null,
                    null,
                    isStop: false
                );
                Assert.True(result);
            }

            // ----------------------------------------------------------------
            // Integration: FindFollowerBracketOrder via list-injection seam
            // ----------------------------------------------------------------

            [Fact]
            public void T1_FindFollower_SecondTargetDrag_PttTgtDrag_ReturnsOrder()
            {
                // End-to-end: list contains only PTT-TGT-Drag Working Limit.
                // signalName=null, leaderName="Target3", isStop=false.
                // Before B136 returned null; after B136 must return the PTT-TGT-Drag order.
                var engine = CopyEngine.Instance;
                var orders = new[]
                {
                    StubBracketOrder("PTT-TGT-Drag", null, OrderState.Working, OrderType.Limit),
                };
                var result = engine.FindFollowerBracketOrderTestable(
                    orders,
                    fromEntrySignalName: null,
                    isStop: false,
                    leaderName: "Target3"
                );
                Assert.NotNull(result);
                Assert.Equal("PTT-TGT-Drag", result!.Name);
            }

            [Fact]
            public void T1_FindFollower_SecondStopDrag_PttStpDrag_ReturnsOrder()
            {
                // End-to-end: list contains only PTT-STP-Drag Working StopMarket.
                // signalName=null, leaderName="Stop1", isStop=true.
                // Before B136 returned null; after B136 must return the PTT-STP-Drag order.
                var engine = CopyEngine.Instance;
                var orders = new[]
                {
                    StubBracketOrder(
                        "PTT-STP-Drag",
                        null,
                        OrderState.Working,
                        OrderType.StopMarket
                    ),
                };
                var result = engine.FindFollowerBracketOrderTestable(
                    orders,
                    fromEntrySignalName: null,
                    isStop: true,
                    leaderName: "Stop1"
                );
                Assert.NotNull(result);
                Assert.Equal("PTT-STP-Drag", result!.Name);
            }
        }
    }
}
