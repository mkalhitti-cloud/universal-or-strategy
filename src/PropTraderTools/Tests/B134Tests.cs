// B134Tests.cs -- xUnit tests for B134 DW-B144 (Submitted-state gap) + DW-B145 (wrong bracket index).
// Ticket 1 (DW-B144): FindFollowerBracketOrder now accepts OrderState.Submitted (was excluded).
// Testability: FindFollowerBracketOrderTestable(IEnumerable<Order>, ...) exposed via
// [assembly: InternalsVisibleTo("PropTraderTools.Tests")] at CopyEngine.cs L46.
// Framework: xUnit only ([Fact]). No NUnit. No MSTest.
// ASCII-only. No lock(). No throw. No return null. No async void.
// DO NOT MODIFY any existing test file (B129Tests.cs, B130Tests.cs, B131Tests.cs, B132Tests.cs, B133Tests.cs).
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools.Tests
{
    // Outer container -- all B134 test classes live here.
    public class B134FindFollowerBracketOrderTests
    {
        // B134Ticket1Tests -- DW-B144: Submitted-state gap fix.
        // Verifies FindFollowerBracketOrder now accepts OrderState.Submitted.
        // Pattern: order.FromEntrySignal=null, leaderName=order.Name (name-fallback path).
        // This avoids IsStopLeg(order) returning true (which would block Limit target orders).
        // Same pattern as B133LaneBTests.
        public class B134Ticket1Tests
        {
            // Helper: creates an Order stub with the given state, type, and name.
            // FromEntrySignal=null so SignalOrNameMatches uses name-fallback path.
            // IsStopLeg checks: FromEntrySignal=null, Name.StartsWith("Stop") for stops,
            // Name.StartsWith("Target") is NOT a stop -- allows Limit target orders to pass.
            private static Order StubStopOrder(OrderState state, OrderType type, string name)
            {
                var o = new Order();
                o.OrderState = state;
                o.OrderType = type;
                o.Name = name;
                o.FromEntrySignal = null; // null: SignalOrNameMatches falls through to name match
                return o;
            }

            // Helper: creates a Limit order stub (not a stop leg).
            // Name must NOT start with "Stop" or end with "STP" to pass IsStopLeg check.
            private static Order StubTargetOrder(OrderState state, string name)
            {
                var o = new Order();
                o.OrderState = state;
                o.OrderType = OrderType.Limit;
                o.Name = name;
                o.FromEntrySignal = null; // null: IsStopLeg(FromEntrySignal=null) -> false -> !IsStopLeg=true
                return o;
            }

            // Test 1 -- Primary DW-B144 fix: Submitted stop order is now found.
            // Before fix: Submitted rejected by state filter -> returned null -> drag silently lost.
            // After fix: Submitted passes the state filter -> stop order returned.
            [Fact]
            public void T1_SubmittedState_StopOrder_Found_And_Returned()
            {
                // Arrange
                var engine = CopyEngine.Instance;
                var orders = new[]
                {
                    StubStopOrder(OrderState.Submitted, OrderType.StopMarket, "Stop1"),
                };
                // Act
                var result = engine.FindFollowerBracketOrderTestable(
                    orders,
                    fromEntrySignalName: null,
                    isStop: true,
                    leaderName: "Stop1"
                );
                // Assert: Submitted stop order must now be found (DW-B144 fix)
                Assert.NotNull(result);
                Assert.Equal(OrderType.StopMarket, result.OrderType);
            }

            // Test 2 -- Primary DW-B144 fix: Submitted target order is now found.
            // Same fix, target-order path (isStop=false, OrderType.Limit).
            // Name="Target1" so IsStopLeg returns false (not a stop name), Limit target passes.
            [Fact]
            public void T1_SubmittedState_TargetOrder_Found_And_Returned()
            {
                // Arrange
                var engine = CopyEngine.Instance;
                var orders = new[] { StubTargetOrder(OrderState.Submitted, "Target1") };
                // Act
                var result = engine.FindFollowerBracketOrderTestable(
                    orders,
                    fromEntrySignalName: null,
                    isStop: false,
                    leaderName: "Target1"
                );
                // Assert: Submitted target order must now be found (DW-B144 fix)
                Assert.NotNull(result);
                Assert.Equal(OrderType.Limit, result.OrderType);
            }

            // Test 3 -- Regression: Working state must still be found after the B134 fix.
            // Guards against the Submitted addition accidentally breaking the Working branch.
            [Fact]
            public void T1_WorkingState_StillFound_Regression()
            {
                // Arrange
                var engine = CopyEngine.Instance;
                var orders = new[]
                {
                    StubStopOrder(OrderState.Working, OrderType.StopMarket, "Stop1"),
                };
                // Act
                var result = engine.FindFollowerBracketOrderTestable(
                    orders,
                    fromEntrySignalName: null,
                    isStop: true,
                    leaderName: "Stop1"
                );
                // Assert: Working state must still be returned (B143 regression guard)
                Assert.NotNull(result);
            }

            // Test 4 -- Regression: Accepted state must still be found after the B134 fix.
            // Guards against the Submitted addition accidentally breaking the Accepted branch.
            [Fact]
            public void T1_AcceptedState_StillFound_Regression()
            {
                // Arrange
                var engine = CopyEngine.Instance;
                var orders = new[] { StubTargetOrder(OrderState.Accepted, "Target1") };
                // Act
                var result = engine.FindFollowerBracketOrderTestable(
                    orders,
                    fromEntrySignalName: null,
                    isStop: false,
                    leaderName: "Target1"
                );
                // Assert: Accepted state must still be returned (B143 regression guard)
                Assert.NotNull(result);
            }

            // Test 5 -- Non-accepted state is correctly rejected (Initialized not in accepted set).
            // Verifies the state filter still excludes non-live states after the B134 fix.
            [Fact]
            public void T1_NullOrder_NotMatched_Guard()
            {
                // Arrange: Initialized is not in {Working, Accepted, Submitted} -- must be rejected.
                var engine = CopyEngine.Instance;
                var orders = new[]
                {
                    StubStopOrder(OrderState.Initialized, OrderType.StopMarket, "Stop1"),
                };
                // Act
                var result = engine.FindFollowerBracketOrderTestable(
                    orders,
                    fromEntrySignalName: null,
                    isStop: true,
                    leaderName: "Stop1"
                );
                // Assert: non-accepted state (Initialized) still rejected after the B134 fix
                Assert.Null(result);
            }
        }

        // B134Ticket2Tests -- DW-B145: wrong bracket index fix.
        // Verifies the leaderName exact guard in FindFollowerBracketOrder selects the correct bracket.
        // Pattern for T2.1/T2.2: FromEntrySignal=null; SignalOrNameMatches uses name-fallback path.
        //   !IsStopLeg(order) requires FromEntrySignal==null and Name not starting with "Stop".
        //   Three orders in list; leaderName disambiguates to the correct one.
        // Pattern for T2.3: stop order with FromEntrySignal="ATM1" and leaderName=null.
        //   Verifies T2 guard is inactive (short-circuits) when leaderName==null (backward compat).
        // DO NOT MODIFY any existing test file (B129Tests.cs, B130Tests.cs, B131Tests.cs, B132Tests.cs, B133Tests.cs).
        public class B134Ticket2Tests
        {
            // Helper: Limit target order using name-fallback path (FromEntrySignal=null).
            // FromEntrySignal=null: IsStopLeg=false -> !IsStopLeg=true -> Limit target path returns order.
            // SignalOrNameMatches: signalName path skips (null!=signalName fails); name-fallback: order.Name==leaderName.
            private static Order StubTargetOrder(
                string name,
                OrderState state = OrderState.Submitted
            )
            {
                var o = new Order();
                o.OrderState = state;
                o.OrderType = OrderType.Limit;
                o.Name = name;
                o.FromEntrySignal = null;
                return o;
            }

            // Helper: stop order using signal path (FromEntrySignal="ATM1").
            // Used for T2.3 backward-compat test: leaderName=null, signal match returns stop order.
            private static Order StubStopOrderSignal(
                string name,
                OrderState state = OrderState.Submitted
            )
            {
                var o = new Order();
                o.OrderState = state;
                o.OrderType = OrderType.StopMarket;
                o.Name = name;
                o.FromEntrySignal = "ATM1"; // signal path for SignalOrNameMatches
                return o;
            }

            // Build the standard three-target list used across T2.1 and T2.2.
            private static Order[] ThreeTargets() =>
                new[]
                {
                    StubTargetOrder("Target1"),
                    StubTargetOrder("Target2"),
                    StubTargetOrder("Target3"),
                };

            // Test T2.1 -- Primary DW-B145 fix: Target3 is returned when leaderName="Target3".
            // All three orders pass SignalOrNameMatches via name-fallback only for their own name.
            // leaderName exact guard confirms Target3 is the only match.
            // Before fix: first signal-match wins (wrong order). After fix: exact name guard selects correctly.
            [Fact]
            public void T2_Target3_ReturnsTarget3_NotTarget1()
            {
                // Arrange
                var engine = CopyEngine.Instance;
                var orders = ThreeTargets();
                // Act: fromEntrySignalName=null -> name-fallback; leaderName="Target3" -> exact match
                var result = engine.FindFollowerBracketOrderTestable(
                    orders,
                    fromEntrySignalName: null,
                    isStop: false,
                    leaderName: "Target3"
                );
                // Assert: exact-name guard must return Target3
                Assert.NotNull(result);
                Assert.Equal("Target3", result.Name);
            }

            // Test T2.2 -- Backward correctness: Target1 returned when leaderName="Target1".
            // Verifies the exact-name guard works for the first order in the list (not just last).
            [Fact]
            public void T2_Target1_ReturnsTarget1_WhenRequested()
            {
                // Arrange
                var engine = CopyEngine.Instance;
                var orders = ThreeTargets();
                // Act: leaderName="Target1" -> exact match returns first order
                var result = engine.FindFollowerBracketOrderTestable(
                    orders,
                    fromEntrySignalName: null,
                    isStop: false,
                    leaderName: "Target1"
                );
                // Assert: exact-name guard returns Target1 when explicitly requested
                Assert.NotNull(result);
                Assert.Equal("Target1", result.Name);
            }

            // Test T2.3 -- Backward compatibility: leaderName=null does not activate T2 guard.
            // Guard condition: "leaderName != null && ..." -- false when leaderName==null (short-circuit).
            // Uses stop order with signal-match path (non-drag scenario that predates B134).
            // Verifies callers passing leaderName=null are unaffected by the DW-B145 guard.
            [Fact]
            public void T2_NullLeaderName_ReturnsFirstMatch_BackwardCompat()
            {
                // Arrange: single stop order with signal match; leaderName=null
                var engine = CopyEngine.Instance;
                var orders = new[] { StubStopOrderSignal("Stop1") };
                // Act: leaderName=null -> T2 guard condition false -> signal match returns stop order
                var result = engine.FindFollowerBracketOrderTestable(
                    orders,
                    fromEntrySignalName: "ATM1",
                    isStop: true,
                    leaderName: null
                );
                // Assert: backward compat -- null leaderName, signal path, stop order still returned
                Assert.NotNull(result);
                Assert.Equal(OrderType.StopMarket, result.OrderType);
            }
        }
    }
}
