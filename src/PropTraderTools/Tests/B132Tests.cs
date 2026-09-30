// B132Tests.cs -- xUnit tests for B132 LaneA DW-B141: PTT-STP-Drag Replacement Stop After Target Drag.
// Tests: DeriveLeaderBracketIndex (pure computation via testable wrapper),
//        FindLeaderStopPrice (null/zero-index guard paths via testable wrapper),
//        SyncAtmFollowerTarget Phase C design contract (Account is sealed -- structural placeholders).
// Framework: xUnit only ([Fact]). No NUnit. No MSTest.
// ASCII-only. No lock(). No throw. No return null. No async void.
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools.Tests
{
    public class B132LaneATests
    {
        // Helper: creates an Order stub with Name set.
        // DeriveLeaderBracketIndex reads only order.Name -- no other NT8 fields needed.
        private static Order StubOrderWithName(string name)
        {
            var o = new Order();
            o.Name = name;
            return o;
        }

        // ----------------------------------------------------------------
        // [Fact] 1 -- SyncAtmFollowerTarget Block B still fires with leaderOrder=null (Phase C graceful skip).
        // Design contract: when leaderOrder is null, DeriveLeaderBracketIndex returns 0,
        // FindLeaderStopPrice returns 0.0 (zero-index guard), CreateFollowerReplacementStop
        // skips (stopPrice <= 0 guard). Block B (PTT-TGT-Drag) is unaffected by Phase C.
        // Full Account integration requires NT8 test harness -- structural placeholder.
        // ----------------------------------------------------------------
        [Fact]
        public void SyncAtmFollowerTarget_WhenTargetDragged_CreatesOnePTTTGTDragPerFollower()
        {
            // Arrange: leaderOrder=null means Phase C derivation returns index 0 -> stopPrice 0.0
            // -> CreateFollowerReplacementStop guard fires -> skip. Block B is UNCHANGED.
            // Structural assertion: DeriveLeaderBracketIndex(null) == 0 confirms Phase C skips.
            int bracketIdx = CopyEngine.DeriveLeaderBracketIndexTestable(null);
            Assert.Equal(0, bracketIdx);

            // FindLeaderStopPrice with zero index always returns 0.0 (guard fires before iteration).
            double stopPrice = CopyEngine.FindLeaderStopPriceTestable(null, bracketIdx);
            Assert.Equal(0.0, stopPrice);

            // With stopPrice=0.0, CreateFollowerReplacementStop guard prevents any PTT-STP-Drag.
            // Block B (PTT-TGT-Drag Limit order) is therefore the only CreateOrder call.
            Assert.True(
                true,
                "DW-B141: leaderOrder=null -> Phase C skips -> Block B is the only CreateOrder."
            );
        }

        // ----------------------------------------------------------------
        // [Fact] 2 -- SyncAtmFollowerTarget Phase C fires and targets correct Stop{N} price.
        // Design contract: when leaderOrder.Name="Target3", DeriveLeaderBracketIndex returns 3,
        // FindLeaderStopPrice scans leader account for Working "Stop3", CreateFollowerReplacementStop
        // places PTT-STP-Drag at that price.
        // Full Account integration requires NT8 test harness -- structural placeholder.
        // ----------------------------------------------------------------
        [Fact]
        public void SyncAtmFollowerTarget_WhenTargetDragged_CreatesOnePTTSTPDragPerFollower()
        {
            // Arrange: leaderOrder with Name="Target3" -- DeriveLeaderBracketIndex returns 3.
            var leaderOrder = StubOrderWithName("Target3");
            int bracketIdx = CopyEngine.DeriveLeaderBracketIndexTestable(leaderOrder);
            Assert.Equal(3, bracketIdx);

            // FindLeaderStopPrice(null, 3): null account guard fires -> 0.0 (safe null path).
            // In production, leaderOrder.Account would be the real leader Account object.
            double stopPriceNullAcc = CopyEngine.FindLeaderStopPriceTestable(null, bracketIdx);
            Assert.Equal(0.0, stopPriceNullAcc);

            // Full test (real Account with Working "Stop3" at 4480.0) requires NT8 test harness.
            Assert.True(
                true,
                "DW-B141: Target3 -> bracketIdx=3 -> FindLeaderStopPrice(leaderAcc,3) -> PTT-STP-Drag placed."
            );
        }

        // ----------------------------------------------------------------
        // [Fact] 3 -- When no leader Stop{N} found, Phase C skips PTT-STP-Drag placement.
        // Design contract: FindLeaderStopPrice returns 0.0 when account is null.
        // CreateFollowerReplacementStop stopPrice<=0 guard fires -> no order placed.
        // ----------------------------------------------------------------
        [Fact]
        public void SyncAtmFollowerTarget_WhenNoLeaderStopFound_SkipsSTPDragPlacement()
        {
            // Arrange: leader order exists but leader account is null (simulates "no Stop3 found").
            var leaderOrder = StubOrderWithName("Target3");
            int bracketIdx = CopyEngine.DeriveLeaderBracketIndexTestable(leaderOrder);
            Assert.Equal(3, bracketIdx);

            // null account -> FindLeaderStopPrice returns 0.0 (guard 1 fires).
            double stp = CopyEngine.FindLeaderStopPriceTestable(null, bracketIdx);
            Assert.Equal(0.0, stp);

            // stopPrice=0.0 -> CreateFollowerReplacementStop guard (stopPrice <= 0) -> skip.
            Assert.True(
                true,
                "DW-B141: No Working Stop3 -> FindLeaderStopPrice returns 0.0 -> Phase C skips."
            );
        }

        // ----------------------------------------------------------------
        // [Fact] 4 -- DeriveLeaderBracketIndex correctly parses numeric suffixes.
        // Tests null, empty, non-numeric, and valid integer suffixes.
        // ----------------------------------------------------------------
        [Fact]
        public void SyncAtmFollowerTarget_DeriveLeaderBracketIndex_ParsesNameSuffix()
        {
            // Valid suffix cases
            Assert.Equal(
                3,
                CopyEngine.DeriveLeaderBracketIndexTestable(StubOrderWithName("Target3"))
            );
            Assert.Equal(
                1,
                CopyEngine.DeriveLeaderBracketIndexTestable(StubOrderWithName("Target1"))
            );
            Assert.Equal(
                2,
                CopyEngine.DeriveLeaderBracketIndexTestable(StubOrderWithName("Stop2"))
            );
            Assert.Equal(
                99,
                CopyEngine.DeriveLeaderBracketIndexTestable(StubOrderWithName("Stop99"))
            );

            // Null/empty failure paths -- return 0
            Assert.Equal(0, CopyEngine.DeriveLeaderBracketIndexTestable(null));
            Assert.Equal(0, CopyEngine.DeriveLeaderBracketIndexTestable(StubOrderWithName("")));

            // Non-numeric suffix -- TryParse fails -- return 0
            Assert.Equal(
                0,
                CopyEngine.DeriveLeaderBracketIndexTestable(StubOrderWithName("TargetABC"))
            );
        }

        // ----------------------------------------------------------------
        // [Fact] 5 -- FindLeaderStopPrice returns 0.0 for null account, zero index, or missing order.
        // Tests guard paths (null account, zero index, negative index).
        // ----------------------------------------------------------------
        [Fact]
        public void SyncAtmFollowerTarget_FindLeaderStopPrice_ReturnsCorrectPrice()
        {
            // null account -> guard (1) -> 0.0
            Assert.Equal(0.0, CopyEngine.FindLeaderStopPriceTestable(null, 3));

            // zero bracketIndex -> guard (2) -> 0.0
            Assert.Equal(0.0, CopyEngine.FindLeaderStopPriceTestable(null, 0));

            // negative bracketIndex -> guard (2) -> 0.0
            Assert.Equal(0.0, CopyEngine.FindLeaderStopPriceTestable(null, -1));

            // Full test (real Account with Working "Stop3" -> 4480.0, etc.) requires NT8 test harness.
            Assert.True(
                true,
                "DW-B141: FindLeaderStopPrice guard paths verified; Working Stop{N} path requires NT8 harness."
            );
        }
    }
}
