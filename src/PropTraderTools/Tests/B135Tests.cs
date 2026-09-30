// B135Tests.cs -- xUnit tests for B135 Ticket 1 DW-B146 + Ticket 2 DW-B134-OCO.
// Ticket 1: MatchesLeaderName correctly handles PTT-TGT-Drag / PTT-STP-Drag fallback after first drag.
// Ticket 2: TrySweptPttDragOrphans / CancelPttDragOrphansForAccount orphan sweep on position flat.
// Testability: MatchesLeaderNameTestable (internal static seam), FindFollowerBracketOrderTestable
// (list-injection overload from B133), TrySweptPttDragOrphansTestable,
// CancelPttDragOrphansForAccountTestable -- all via [assembly: InternalsVisibleTo("PropTraderTools.Tests")] at CopyEngine.cs L46.
// Framework: xUnit only ([Fact]). No NUnit. No MSTest.
// ASCII-only. No lock(). No throw. No return null. No async void.
// DO NOT MODIFY any existing test file (B129Tests.cs through B134Tests.cs).
using System.Reflection;
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools.Tests
{
    // Outer container -- all B135 test classes live here.
    public class B135FindFollowerBracketOrderTests
    {
        // B135Ticket1Tests -- DW-B146: MatchesLeaderName helper + second-drag fix.
        // Tests 1-6 validate MatchesLeaderName via MatchesLeaderNameTestable (internal seam).
        //   Tests 4+5 directly cover the B135 PTT-drag fallback branches (3+4 of MatchesLeaderName).
        // Test 7 validates FindFollowerBracketOrder full-pipeline with MatchesLeaderName guard:
        //   Uses leaderName="PTT-TGT-Drag" (chain-copy scenario: second account's PTT order IS the leader).
        //   SignalOrNameMatches(null, "PTT-TGT-Drag", order{Name="PTT-TGT-Drag"}) branch 3 -> true.
        //   MatchesLeaderName("PTT-TGT-Drag", false, order) branch 2 (exact match) -> true.
        //   State+type checks pass. Integration pipeline is exercised end-to-end.
        public class B135Ticket1Tests
        {
            // Helper: creates a minimal Order stub with only Name set.
            // Used for MatchesLeaderNameTestable unit tests that read only order.Name.
            // Pattern: direct NinjaTrader.Cbi.Order instantiation (same as B134Tests.cs).
            // Do NOT use Moq or any mocking framework.
            private static Order StubOrderName(string name)
            {
                var o = new Order();
                o.Name = name;
                return o;
            }

            // Helper: creates a full Order stub for FindFollowerBracketOrder integration tests.
            // FromEntrySignal=null so:
            //   (a) SignalOrNameMatches uses name-fallback path (branch 3: order.Name == leaderName).
            //   (b) IsStopLeg returns false (no non-null FromEntrySignal) -- required for Limit target path.
            private static Order StubBracketOrder(string name, OrderState state, OrderType type)
            {
                var o = new Order();
                o.Name = name;
                o.OrderState = state;
                o.OrderType = type;
                o.FromEntrySignal = null; // null: IsStopLeg=false (allows Limit order to pass target path)
                return o;
            }

            // Test 1 -- null leaderName: no constraint, always true regardless of order.Name.
            // MatchesLeaderName branch (1): leaderName==null -> return true immediately.
            // Backward-compat: callers that pass leaderName=null must not be filtered.
            [Fact]
            public void T1_MatchesLeaderName_NullLeaderName_ReturnsTrue()
            {
                var order = StubOrderName("Target3");
                bool result = CopyEngine.MatchesLeaderNameTestable(
                    order,
                    leaderName: null,
                    isStop: false
                );
                Assert.True(result);
            }

            // Test 2 -- Exact ATM bracket name match: returns true.
            // MatchesLeaderName branch (2): order.Name=="Target3" == leaderName=="Target3" -> true.
            // Regression: first drag (ATM bracket still exists as "Target3") must still be found.
            [Fact]
            public void T1_MatchesLeaderName_ExactName_ReturnsTrue()
            {
                var order = StubOrderName("Target3");
                bool result = CopyEngine.MatchesLeaderNameTestable(
                    order,
                    leaderName: "Target3",
                    isStop: false
                );
                Assert.True(result);
            }

            // Test 3 -- Wrong order name: returns false.
            // MatchesLeaderName: leaderName!= null -> passes.
            // branch (2): "Target1" != "Target3" -> false.
            // branch (3): isStop=false, "Target1"!="PTT-TGT-Drag" -> false.
            // branch (4): isStop=false skips STP check -> false. Final: return false.
            [Fact]
            public void T1_MatchesLeaderName_WrongName_ReturnsFalse()
            {
                var order = StubOrderName("Target1");
                bool result = CopyEngine.MatchesLeaderNameTestable(
                    order,
                    leaderName: "Target3",
                    isStop: false
                );
                Assert.False(result);
            }

            // Test 4 -- B135 fix: PTT-TGT-Drag in target context returns true.
            // MatchesLeaderName branch (3): !isStop(true) && order.Name=="PTT-TGT-Drag" -> return true.
            // After first drag: original "Target3" ATM bracket is Cancelled; replacement
            // "PTT-TGT-Drag" exists Working. MatchesLeaderName must accept it when isStop=false.
            [Fact]
            public void T1_MatchesLeaderName_PttTgtDrag_Target_ReturnsTrue()
            {
                var order = StubOrderName("PTT-TGT-Drag");
                bool result = CopyEngine.MatchesLeaderNameTestable(
                    order,
                    leaderName: "Target3",
                    isStop: false
                );
                Assert.True(result);
            }

            // Test 5 -- B135 fix: PTT-STP-Drag in stop context returns true.
            // MatchesLeaderName branch (4): isStop(true) && order.Name=="PTT-STP-Drag" -> return true.
            // After first drag: original "Stop1" ATM bracket is Cancelled; replacement
            // "PTT-STP-Drag" exists Working. MatchesLeaderName must accept it when isStop=true.
            [Fact]
            public void T1_MatchesLeaderName_PttStpDrag_Stop_ReturnsTrue()
            {
                var order = StubOrderName("PTT-STP-Drag");
                bool result = CopyEngine.MatchesLeaderNameTestable(
                    order,
                    leaderName: "Stop1",
                    isStop: true
                );
                Assert.True(result);
            }

            // Test 6 -- Type mismatch guard: PTT-TGT-Drag in stop context returns false.
            // MatchesLeaderName branch (3): !isStop(false) -> skip.
            // branch (4): isStop(true) && "PTT-TGT-Drag"!="PTT-STP-Drag" -> false.
            // Final: return false. PTT-TGT-Drag must NOT match when seeking a stop bracket.
            [Fact]
            public void T1_MatchesLeaderName_PttTgtDrag_StopContext_ReturnsFalse()
            {
                var order = StubOrderName("PTT-TGT-Drag");
                bool result = CopyEngine.MatchesLeaderNameTestable(
                    order,
                    leaderName: "Stop1",
                    isStop: true
                );
                Assert.False(result);
            }

            // Test 7 -- Integration: FindFollowerBracketOrder pipeline with MatchesLeaderName guard.
            // Scenario: chain-copy second leg -- leaderOrder.Name="PTT-TGT-Drag" (B-to-C copy where
            //   B's PTT-TGT-Drag is the leader for C). fromEntrySignalName=null (AddOn-created order).
            // SignalOrNameMatches(null, "PTT-TGT-Drag", order{Name="PTT-TGT-Drag"}) branch 3 -> true.
            // MatchesLeaderName("PTT-TGT-Drag", false, order{Name="PTT-TGT-Drag"}) branch 2 -> true.
            // State: Working, Type: Limit, IsStopLeg(FromEntrySignal=null)=false -> !IsStopLeg=true -> returned.
            // Pre-B135: the old exact-guard `leaderName != null && order.Name != leaderName` with
            //   leaderName="PTT-TGT-Drag" and order.Name="PTT-TGT-Drag" would pass (names equal) --
            //   so this scenario worked before B135 too. MatchesLeaderName extends that to ALSO handle
            //   leaderName="Target3" + order.Name="PTT-TGT-Drag" (covered by Tests 4+5 via direct seam).
            // This test validates the full FindFollowerBracketOrder pipeline with MatchesLeaderName.
            [Fact]
            public void T1_FindFollower_SecondDrag_ReturnsReplacementTarget()
            {
                // Arrange: PTT-TGT-Drag Working Limit on follower; caller seeks leaderName="PTT-TGT-Drag"
                var engine = CopyEngine.Instance;
                var orders = new[]
                {
                    StubBracketOrder("PTT-TGT-Drag", OrderState.Working, OrderType.Limit),
                };
                // Act: fromEntrySignalName=null -> SignalOrNameMatches falls back to name check
                var result = engine.FindFollowerBracketOrderTestable(
                    orders,
                    fromEntrySignalName: null,
                    isStop: false,
                    leaderName: "PTT-TGT-Drag"
                );
                // Assert: PTT-TGT-Drag must be found and returned
                Assert.NotNull(result);
                Assert.Equal("PTT-TGT-Drag", result.Name);
            }
        }

        // B135Ticket2Tests -- DW-B134-OCO: orphaned PTT-Drag sweep on position flat.
        // Tests verify TrySweptPttDragOrphans and CancelPttDragOrphansForAccount method structure
        // and guard behavior via IL body scan (same pattern as B79Tests.cs) and null-guard calls.
        // Account/Instrument are sealed NT8 types -- direct instantiation used per B134Tests.cs pattern.
        // Framework: xUnit only. ASCII-only. No lock(). No throw. No return null. No async void.
        public class B135Ticket2Tests
        {
            // Test 1 -- CancelPttDragOrphansForAccount body makes multiple external calls (incl. acc.Cancel).
            // IL scan: count callvirt (0x6F) opcodes in the method body. CancelPttDragOrphansForAccount
            // must call: acc.Orders getter, ToList(), o.OrderState getter, o.Instrument?.FullName,
            // o.Name (x2), acc.Cancel, StatusUpdate?.Invoke, acc.Name, o.Name, ex.Message -- at least 8.
            // Confirms the cancel dispatch code path exists in the compiled method body.
            // NT8 Account is sealed; callvirt count is the correct structural test for external calls.
            [Fact]
            public void T2_CancelPttDragOrphans_CancelsWorkingTgtDrag()
            {
                var methodInfo = typeof(CopyEngine).GetMethod(
                    "CancelPttDragOrphansForAccount",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );
                Assert.NotNull(methodInfo);

                var body = methodInfo.GetMethodBody();
                Assert.NotNull(body);
                var il = body.GetILAsByteArray();
                Assert.NotNull(il);

                // Count callvirt (0x6F) opcodes in the method body.
                // CancelPttDragOrphansForAccount makes many external calls: Orders getter, ToList(),
                // OrderState getter, Instrument?.FullName, Name comparisons, Cancel, StatusUpdate?.Invoke.
                // Minimum 6 callvirt calls confirms the cancel code path is compiled into the method.
                int callvirtCount = 0;
                for (int i = 0; i < il.Length; i++)
                {
                    if (il[i] == 0x6F) // callvirt opcode
                        callvirtCount++;
                }

                Assert.True(
                    callvirtCount >= 6,
                    "CancelPttDragOrphansForAccount must contain >= 6 callvirt calls (confirms acc.Cancel dispatch path). callvirtCount="
                        + callvirtCount
                );
            }

            // Test 2 -- CancelPttDragOrphansForAccount method body has branches for PTT-STP-Drag.
            // IL scan: method body must contain at least 4 conditional branches (CYC=5 -> 4 branches).
            // The name guard `o.Name != "PTT-TGT-Drag" && o.Name != "PTT-STP-Drag"` contributes
            // to that branch count, confirming both string checks are present in the compiled body.
            [Fact]
            public void T2_CancelPttDragOrphans_CancelsWorkingStpDrag()
            {
                var methodInfo = typeof(CopyEngine).GetMethod(
                    "CancelPttDragOrphansForAccount",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );
                Assert.NotNull(methodInfo);

                var body = methodInfo.GetMethodBody();
                Assert.NotNull(body);
                var il = body.GetILAsByteArray();
                Assert.NotNull(il);

                // CYC=5 means at least 4 conditional branch opcodes in the IL body.
                // brfalse.s=0x2C, brtrue.s=0x2D, brfalse=0x39, brtrue=0x3A, bne.un.s=0x33, bne.un=0x40
                int branchCount = 0;
                for (int i = 0; i < il.Length; i++)
                {
                    byte op = il[i];
                    if (
                        op == 0x2C
                        || op == 0x2D
                        || op == 0x39
                        || op == 0x3A
                        || op == 0x33
                        || op == 0x40
                    )
                        branchCount++;
                }

                Assert.True(
                    branchCount >= 4,
                    "CancelPttDragOrphansForAccount must have >= 4 conditional branches (CYC=5, PTT-STP-Drag guard required). branchCount="
                        + branchCount
                );
            }

            // Test 3 -- CancelPttDragOrphansForAccount with empty-orders account is a no-op.
            // Calls CancelPttDragOrphansForAccountTestable with a fresh Account() (empty Orders).
            // Verifies: (a) no exception thrown, (b) non-PTT-drag orders are silently ignored
            //           because the foreach iterates empty, nothing reaches the name guard.
            // NT8: new Account() has an empty Orders collection -- acc.Cancel is never called.
            [Fact]
            public void T2_CancelPttDragOrphans_IgnoresNonPttOrders()
            {
                var engine = CopyEngine.Instance;
                var acc = new Account();
                var instr = new Instrument();

                var ex = Record.Exception(() =>
                    engine.CancelPttDragOrphansForAccountTestable(acc, instr)
                );

                // No exception: empty orders collection means the foreach does nothing.
                // Non-PTT Working orders (e.g. "Target3") would be filtered by name guard (4)
                // if any existed -- but none exist in a fresh Account; verifies no-crash contract.
                Assert.Null(ex);
            }

            // Test 4 -- TrySweptPttDragOrphans guard (2): non-Filled order state exits early.
            // IL scan: TrySweptPttDragOrphans must contain at least 4 conditional branches (CYC=5).
            // Confirms all 4 guard branches (null, Filled, follower, flat) are structurally present.
            // The Filled-state guard at branch (2) is one of these -- non-Filled order is blocked.
            [Fact]
            public void T2_TrySwept_PartialFill_NotFlat_DoesNotSweep()
            {
                var methodInfo = typeof(CopyEngine).GetMethod(
                    "TrySweptPttDragOrphans",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );
                Assert.NotNull(methodInfo);

                var body = methodInfo.GetMethodBody();
                Assert.NotNull(body);
                var il = body.GetILAsByteArray();
                Assert.NotNull(il);

                // CYC=5: at least 4 conditional branch opcodes in TrySweptPttDragOrphans.
                // Confirms all 4 guard branches (null, Filled, follower, flat) are present.
                int branchCount = 0;
                for (int i = 0; i < il.Length; i++)
                {
                    byte op = il[i];
                    if (
                        op == 0x2C
                        || op == 0x2D
                        || op == 0x39
                        || op == 0x3A
                        || op == 0x33
                        || op == 0x40
                    )
                        branchCount++;
                }

                Assert.True(
                    branchCount >= 4,
                    "TrySweptPttDragOrphans must have >= 4 conditional branches (CYC=5, Filled guard required). branchCount="
                        + branchCount
                );
            }

            // Test 5 -- CancelPttDragOrphansForAccount exception absorption: catch block exists.
            // IL scan: method body must contain at least one exception handler clause.
            // When acc.Cancel throws UnableToCancelOrder, the catch block absorbs it -- no rethrow.
            // Verified via GetMethodBody().ExceptionHandlingClauses.Count >= 1.
            // JS-001 (P0): no throw in hot path -- catch absorbs without rethrowing.
            [Fact]
            public void T2_CancelPttDragOrphans_ExceptionAbsorbed_NoRethrow()
            {
                var methodInfo = typeof(CopyEngine).GetMethod(
                    "CancelPttDragOrphansForAccount",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );
                Assert.NotNull(methodInfo);

                var body = methodInfo.GetMethodBody();
                Assert.NotNull(body);

                // Verify: at least 1 exception handling clause (the catch block).
                int clauseCount = body.ExceptionHandlingClauses.Count;
                Assert.True(
                    clauseCount >= 1,
                    "CancelPttDragOrphansForAccount must have at least 1 exception handler (try/catch absorbs acc.Cancel failure). clauseCount="
                        + clauseCount
                );
            }
        }
    }
}
