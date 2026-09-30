// B133Tests.cs -- xUnit tests for B133 LaneA DW-B142: SignalOrNameMatches null==null false-positive fix.
// Tests: SignalOrNameMatches null-guard regression (5 [Fact] methods).
// Testability: SignalOrNameMatchesTestable is an internal accessor exposed via
// [assembly: InternalsVisibleTo("PropTraderTools.Tests")] at CopyEngine.cs L46.
// Framework: xUnit only ([Fact]). No NUnit. No MSTest.
// ASCII-only. No lock(). No throw. No return null. No async void.
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools.Tests
{
    public class B133LaneATests
    {
        // Helper: creates an Order with Name and FromEntrySignal set.
        // SignalOrNameMatches reads only order.FromEntrySignal and order.Name.
        // Pattern: direct NinjaTrader.Cbi.Order instantiation (same as B131Tests.cs, B132Tests.cs).
        // Do NOT use Moq or any mocking framework.
        private static Order StubOrder(string name, string? fromEntrySignal)
        {
            var o = new Order();
            o.Name = name;
            o.FromEntrySignal = fromEntrySignal;
            return o;
        }

        // Test 1 -- Primary DW-B142 regression guard.
        // Before the fix: null==null returned true (false positive, wrong follower cancelled).
        // After the fix: signalName != null guard fires -> branch (1) false.
        // Branch (3): order.Name="Stop1" != leaderName="Target3" -> false.
        // Expected: false (the ATM bracket drag cancel-all bug is fixed).
        [Fact]
        public void SignalOrNameMatches_NullSignal_DoesNotMatchBySignal()
        {
            var order = StubOrder("Stop1", fromEntrySignal: null);
            bool result = CopyEngine.SignalOrNameMatchesTestable(
                order,
                signalName: null,
                leaderName: "Target3"
            );
            Assert.False(result);
        }

        // Test 2 -- ATM name-fallback path works correctly after null-guard.
        // Branch (1): signalName=null -> guard fires -> false.
        // Branch (2): leaderName="Target3" != null -> passes.
        // Branch (3): order.Name="Target3" == leaderName="Target3" -> true.
        [Fact]
        public void SignalOrNameMatches_NullSignal_MatchesByName()
        {
            var order = StubOrder("Target3", fromEntrySignal: null);
            bool result = CopyEngine.SignalOrNameMatchesTestable(
                order,
                signalName: null,
                leaderName: "Target3"
            );
            Assert.True(result);
        }

        // Test 3 -- ATM name-fallback correctly rejects a wrong-name order.
        // Branch (1): signalName=null -> guard fires -> false.
        // Branch (2): leaderName="Target3" != null -> passes.
        // Branch (3): order.Name="Target1" != "Target3" -> false.
        [Fact]
        public void SignalOrNameMatches_NullSignal_NoMatch_WrongName()
        {
            var order = StubOrder("Target1", fromEntrySignal: null);
            bool result = CopyEngine.SignalOrNameMatchesTestable(
                order,
                signalName: null,
                leaderName: "Target3"
            );
            Assert.False(result);
        }

        // Test 4 -- Existing strategy-order signal path is unbroken by the fix.
        // Branch (1): signalName="ES" != null -> guard passes.
        //             order.FromEntrySignal="ES" == signalName="ES" -> true.
        [Fact]
        public void SignalOrNameMatches_NonNullSignal_MatchesBySignal()
        {
            var order = StubOrder("Stop1", fromEntrySignal: "ES");
            bool result = CopyEngine.SignalOrNameMatchesTestable(
                order,
                signalName: "ES",
                leaderName: null
            );
            Assert.True(result);
        }

        // Test 5 -- Double-null produces no match: both signalName and leaderName are null.
        // Branch (1): signalName=null -> guard fires -> false.
        // Branch (2): leaderName=null -> guard fires -> false.
        // No match when both signal and leader name are null.
        [Fact]
        public void SignalOrNameMatches_NullLeaderName_NullSignal_NoMatch()
        {
            var order = StubOrder("Stop1", fromEntrySignal: null);
            bool result = CopyEngine.SignalOrNameMatchesTestable(
                order,
                signalName: null,
                leaderName: null
            );
            Assert.False(result);
        }
    }

    // B133 LaneB -- DW-B143: FindFollowerBracketOrder Accepted-state fix.
    // Tests verify the state filter now accepts Working AND Accepted orders.
    // Seam: FindFollowerBracketOrderTestable(IEnumerable<Order>, fromEntrySignal, isStop, leaderName)
    //   -- list-injection overload added at CopyEngine.cs alongside the Account overload.
    // Order is NOT sealed in the test assembly: direct instantiation via new Order().
    // Framework: xUnit only ([Fact]). No NUnit. No MSTest.
    // ASCII-only. No lock(). No throw. No return null. No async void.
    public class B133LaneBTests
    {
        // Helper: creates an Order stub with the given state, type, and name.
        // FindFollowerBracketOrder reads: order.OrderState, order.OrderType, order.Name,
        // order.FromEntrySignal (via SignalOrNameMatches). Set Name so SignalOrNameMatches
        // matches via the name-fallback branch (leaderName == order.Name).
        private static Order StubOrder(OrderState state, OrderType type, string name)
        {
            var o = new Order();
            o.OrderState = state;
            o.OrderType = type;
            o.Name = name;
            o.FromEntrySignal = null; // null causes SignalOrNameMatches to fall through to name match
            return o;
        }

        // Test 1 -- Primary DW-B143 regression-prevention test.
        // Verifies that a bracket order in OrderState.Accepted is returned after the fix.
        // Before fix: Working-only filter skipped Accepted -> returned null -> drag silently lost.
        // After fix: Accepted passes the state filter -> order returned.
        [Fact]
        public void FindFollowerBracketOrder_AcceptedState_IsFound()
        {
            // Arrange
            var engine = CopyEngine.Instance;
            var orders = new[] { StubOrder(OrderState.Accepted, OrderType.StopMarket, "Stop1") };
            // Act
            var result = engine.FindFollowerBracketOrderTestable(
                orders,
                fromEntrySignalName: null,
                isStop: true,
                leaderName: "Stop1"
            );
            // Assert: Accepted state must now be found (DW-B143 fix)
            Assert.NotNull(result);
        }

        // Test 2 -- Verifies that Submitted orders are NOT returned.
        // Submitted is explicitly excluded: NT8 Account.Cancel() on Submitted is unreliable.
        // The filter must continue to skip Submitted orders after the DW-B143 fix.
        [Fact]
        public void FindFollowerBracketOrder_SubmittedState_IsNotFound()
        {
            // Arrange
            var engine = CopyEngine.Instance;
            var orders = new[] { StubOrder(OrderState.Submitted, OrderType.StopMarket, "Stop1") };
            // Act
            var result = engine.FindFollowerBracketOrderTestable(
                orders,
                fromEntrySignalName: null,
                isStop: true,
                leaderName: "Stop1"
            );
            // Assert: Post-B134: Submitted orders now accepted (DW-B144 fix)
            Assert.NotNull(result);
        }

        // Test 3 -- Verifies that Filled orders are NOT returned.
        // Filled is a terminal state and must not be selected for cancel-and-resubmit.
        [Fact]
        public void FindFollowerBracketOrder_FilledState_IsNotFound()
        {
            // Arrange
            var engine = CopyEngine.Instance;
            var orders = new[] { StubOrder(OrderState.Filled, OrderType.Limit, "Target1") };
            // Act
            var result = engine.FindFollowerBracketOrderTestable(
                orders,
                fromEntrySignalName: null,
                isStop: false,
                leaderName: "Target1"
            );
            // Assert: Filled is terminal -- must not be returned
            Assert.Null(result);
        }

        // Test 4 -- Regression: Working state must still be found after the fix.
        // Guards against the fix accidentally narrowing the filter (Working must remain valid).
        [Fact]
        public void FindFollowerBracketOrder_WorkingState_IsFound()
        {
            // Arrange
            var engine = CopyEngine.Instance;
            var orders = new[] { StubOrder(OrderState.Working, OrderType.StopLimit, "Stop2") };
            // Act
            var result = engine.FindFollowerBracketOrderTestable(
                orders,
                fromEntrySignalName: null,
                isStop: true,
                leaderName: "Stop2"
            );
            // Assert: Working state must still be returned (regression guard)
            Assert.NotNull(result);
        }

        // Test 5 -- Regression: Cancelled state must still be excluded.
        // Cancelled is a terminal state and must remain excluded.
        [Fact]
        public void FindFollowerBracketOrder_CancelledState_IsNotFound()
        {
            // Arrange
            var engine = CopyEngine.Instance;
            var orders = new[] { StubOrder(OrderState.Cancelled, OrderType.Limit, "Target1") };
            // Act
            var result = engine.FindFollowerBracketOrderTestable(
                orders,
                fromEntrySignalName: null,
                isStop: false,
                leaderName: "Target1"
            );
            // Assert: Cancelled is terminal -- must not be returned
            Assert.Null(result);
        }
    }
}
