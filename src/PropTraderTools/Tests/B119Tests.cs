// B119Tests.cs -- xUnit tests for DW-B128 Direction-Change Guard in DispatchCopy
// Framework: xUnit (NEVER NUnit or MSTest)
// Jane Street rules: JS-021 (no lock), JS-001 (no throw), CYC <= 8, ASCII-only.
// Part A: Pure unit tests for IsReversalToFlatFollower -- no NT8 mocks, direct static call.
// Part B: ConcurrentDictionary invariant tests -- no CopyEngine instance needed.
// Part C: BuyToCover / SellShort direction-change variants.
// All 11 [Fact] methods follow the exact names from B119-T1 ticket Section 4 Step 4.
using System.Collections.Concurrent;
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools
{
    /// <summary>
    /// B119 -- DW-B128 Direction-Change Guard.
    /// Tests: IsReversalToFlatFollower static predicate + _lastLeaderDirection dict invariants.
    /// Zero NT8 API calls: OrderAction is an enum (value type), no runtime NT8 dependency.
    /// </summary>
    public class B119Tests
    {
        // =====================================================================
        // Part A -- Pure unit tests for IsReversalToFlatFollower
        // =====================================================================

        // A1: Buy -> Buy, follower flat: same direction -- guard must NOT fire.
        [Fact]
        public void T_IsReversalToFlatFollower_SameDirection_Buy_NotFired()
        {
            Assert.False(
                CopyEngine.IsReversalToFlatFollower(
                    OrderAction.Buy,
                    OrderAction.Buy,
                    followerIsFlat: true
                )
            );
        }

        // A2: Sell -> Sell, follower flat: same direction -- guard must NOT fire.
        [Fact]
        public void T_IsReversalToFlatFollower_SameDirection_Sell_NotFired()
        {
            Assert.False(
                CopyEngine.IsReversalToFlatFollower(
                    OrderAction.Sell,
                    OrderAction.Sell,
                    followerIsFlat: true
                )
            );
        }

        // A3: Sell dispatched after Buy, follower flat: reversal + flat -- guard FIRES (returns true).
        [Fact]
        public void T_IsReversalToFlatFollower_Reversal_BuyToSell_FlatFollower_Fires()
        {
            Assert.True(
                CopyEngine.IsReversalToFlatFollower(
                    OrderAction.Sell,
                    OrderAction.Buy,
                    followerIsFlat: true
                )
            );
        }

        // A4: Buy dispatched after Sell, follower flat: reversal + flat -- guard FIRES (returns true).
        [Fact]
        public void T_IsReversalToFlatFollower_Reversal_SellToBuy_FlatFollower_Fires()
        {
            Assert.True(
                CopyEngine.IsReversalToFlatFollower(
                    OrderAction.Buy,
                    OrderAction.Sell,
                    followerIsFlat: true
                )
            );
        }

        // A5: Sell dispatched after Buy, follower NOT flat: reversal but has position -- guard must NOT fire.
        [Fact]
        public void T_IsReversalToFlatFollower_Reversal_DirectionChange_NotFlat_NotFired()
        {
            Assert.False(
                CopyEngine.IsReversalToFlatFollower(
                    OrderAction.Sell,
                    OrderAction.Buy,
                    followerIsFlat: false
                )
            );
        }

        // A6: First-entry invariant -- when currentAction == lastAction (safe placeholder for no-prior-direction),
        // IsReversalToFlatFollower must return false regardless of flatness.
        // (In DispatchCopy, hasLastDirection guards the call; this verifies the helper is safe with equal inputs.)
        [Fact]
        public void T_IsReversalToFlatFollower_NoLastDirection_NotFired()
        {
            Assert.False(
                CopyEngine.IsReversalToFlatFollower(
                    OrderAction.Buy,
                    OrderAction.Buy,
                    followerIsFlat: true
                )
            );
        }

        // =====================================================================
        // Part B -- ConcurrentDictionary invariant tests (no CopyEngine instance)
        // =====================================================================

        // B1: Absent key -- TryGetValue returns false on a new dictionary.
        [Fact]
        public void T_DirDict_AbsentKey_TryGetValue_ReturnsFalse()
        {
            var dict = new ConcurrentDictionary<string, OrderAction>();
            bool found = dict.TryGetValue("NQ 03-26 CME", out _);
            Assert.False(found);
        }

        // B2: After one write -- TryGetValue returns true with the written value.
        [Fact]
        public void T_DirDict_AfterWrite_KeyPresent_ReturnsBuy()
        {
            var dict = new ConcurrentDictionary<string, OrderAction>();
            dict["NQ 03-26 CME"] = OrderAction.Buy;
            bool found = dict.TryGetValue("NQ 03-26 CME", out OrderAction val);
            Assert.True(found);
            Assert.Equal(OrderAction.Buy, val);
        }

        // B3: Overwrite -- second write for the same key returns the updated value.
        [Fact]
        public void T_DirDict_OverwriteUpdatesValue()
        {
            var dict = new ConcurrentDictionary<string, OrderAction>();
            dict["NQ 03-26 CME"] = OrderAction.Buy;
            dict["NQ 03-26 CME"] = OrderAction.Sell;
            bool found = dict.TryGetValue("NQ 03-26 CME", out OrderAction val);
            Assert.True(found);
            Assert.Equal(OrderAction.Sell, val);
        }

        // =====================================================================
        // Part C -- BuyToCover / SellShort direction-change variants
        // =====================================================================

        // C1: SellShort dispatched after BuyToCover, follower flat -- guard FIRES (returns true).
        [Fact]
        public void T_IsReversalToFlatFollower_BuyToCoverToSellShort_Flat_ReturnsTrue()
        {
            Assert.True(
                CopyEngine.IsReversalToFlatFollower(
                    OrderAction.SellShort,
                    OrderAction.BuyToCover,
                    followerIsFlat: true
                )
            );
        }

        // C2: BuyToCover dispatched after SellShort, follower flat -- guard FIRES (returns true).
        [Fact]
        public void T_IsReversalToFlatFollower_SellShortToBuyToCover_Flat_ReturnsTrue()
        {
            Assert.True(
                CopyEngine.IsReversalToFlatFollower(
                    OrderAction.BuyToCover,
                    OrderAction.SellShort,
                    followerIsFlat: true
                )
            );
        }
    }
}
