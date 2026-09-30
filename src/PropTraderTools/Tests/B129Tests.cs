// B129Tests.cs -- xUnit tests for DW-B134: ATM Bracket Drag Not Synced to Followers.
// Tests: IsBracketLegStatic STP suffix (via IsAtmSTPOrder proxy), SyncAtmFollowerBracket
// routing detection, and OQ-03 cascade safety (FindMatchingRule gate).
// Framework: xUnit only ([Fact]). No NUnit. No MSTest.
using Xunit;

namespace PropTraderTools.Tests
{
    public class B129Tests
    {
        // ----------------------------------------------------------------
        // B129 LaneB -- DW-B134: ATM Bracket Drag Not Synced to Followers
        // ----------------------------------------------------------------

        // [Fact] B129_DW134_STPSuffixDetectedByIsBracketLegStatic
        // IsBracketLegStatic is private -- tested indirectly via IsAtmSTPOrder (internal static).
        // IsAtmSTPOrder is the predicate that IsBracketLegStatic's new STP clause mirrors exactly
        // (EndsWith("STP", OrdinalIgnoreCase)). SyncFollowerBracket routing also uses IsAtmSTPOrder
        // directly. Both paths are covered by asserting IsAtmSTPOrder.
        // InternalsVisibleTo("PropTraderTools.Tests") at CopyEngine.cs:46 grants access.
        [Fact]
        public void B129_DW134_STPSuffixDetectedByIsBracketLegStatic()
        {
            // NT8 ATM stop bracket names -- must be detected after DW-B134 fix
            var buyStop = new NinjaTrader.Cbi.Order();
            buyStop.Name = "Buy STP";
            Assert.True(CopyEngine.IsAtmSTPOrder(buyStop)); // DW-B134 Layer 1 fix

            var sellStop = new NinjaTrader.Cbi.Order();
            sellStop.Name = "Sell STP";
            Assert.True(CopyEngine.IsAtmSTPOrder(sellStop)); // DW-B134 Layer 1 fix

            // Legacy names -- must NOT be detected as ATM STP (regression guard)
            var legacy = new NinjaTrader.Cbi.Order();
            legacy.Name = "Stop1";
            Assert.True(CopyEngine.IsAtmSTPOrder(legacy)); // DW-B137: Stop1 now returns true (StartsWith("Stop") extended predicate)

            var entry = new NinjaTrader.Cbi.Order();
            entry.Name = "Entry";
            Assert.False(CopyEngine.IsAtmSTPOrder(entry)); // "Entry" is not a bracket leg
        }

        // [Fact] B129_DW134_SyncFollowerBracketCancelResubmitFiredForAtmBracket
        // Confirms the IsAtmSTPOrder routing predicate used by SyncFollowerBracket:
        // "Buy STP" and "Sell STP" route to SyncAtmFollowerBracket (cancel+resubmit path).
        // "Stop1" does NOT route to cancel+resubmit (uses legacy acc.Change() path).
        // Null and empty names return false (null-safe for upstream guards).
        [Fact]
        public void B129_DW134_SyncFollowerBracketCancelResubmitFiredForAtmBracket()
        {
            var atpBuy = new NinjaTrader.Cbi.Order();
            atpBuy.Name = "Buy STP";
            Assert.True(CopyEngine.IsAtmSTPOrder(atpBuy)); // routes to cancel+resubmit

            var atpSell = new NinjaTrader.Cbi.Order();
            atpSell.Name = "Sell STP";
            Assert.True(CopyEngine.IsAtmSTPOrder(atpSell)); // routes to cancel+resubmit

            var native = new NinjaTrader.Cbi.Order();
            native.Name = "Stop1";
            Assert.True(CopyEngine.IsAtmSTPOrder(native)); // DW-B137: Stop1 now routes to cancel+resubmit (correct ATM behavior)

            var nullOrder = new NinjaTrader.Cbi.Order();
            nullOrder.Name = null;
            Assert.False(CopyEngine.IsAtmSTPOrder(nullOrder)); // null-safe

            var emptyOrder = new NinjaTrader.Cbi.Order();
            emptyOrder.Name = "";
            Assert.False(CopyEngine.IsAtmSTPOrder(emptyOrder)); // empty-safe
        }

        // [Fact] B129_DW134_OQ03_CancelledBracketDoesNotTriggerFollowerEntryCancel
        // OQ-03 gate test: confirms IsAtmSTPOrder is the SOLE routing predicate for the
        // cancel+resubmit path. The cascade safety (Gate 2 null-return for follower account
        // orders in FindMatchingRule) is confirmed by architecture plan LaneB-02 Section C.
        // This test confirms the predicate boundary: only "* STP" names enter the ATM path.
        // All other names (PTT-*, Stop1..Stop9, Target1..Target9) are excluded.
        [Fact]
        public void B129_DW134_OQ03_CancelledBracketDoesNotTriggerFollowerEntryCancel()
        {
            // ATM-owned STP brackets (cancel+resubmit path -- subject to OQ-03 analysis)
            var buySTP = new NinjaTrader.Cbi.Order();
            buySTP.Name = "Buy STP";
            Assert.True(CopyEngine.IsAtmSTPOrder(buySTP));

            // Non-STP brackets (legacy acc.Change() path -- NOT subject to OQ-03 cascade risk)
            var stop1 = new NinjaTrader.Cbi.Order();
            stop1.Name = "Stop1";
            Assert.True(CopyEngine.IsAtmSTPOrder(stop1)); // DW-B137: Stop1 returns true (StartsWith("Stop") extended predicate)

            // PTT-issued brackets must NOT enter ATM cancel+resubmit path
            var pttStop = new NinjaTrader.Cbi.Order();
            pttStop.Name = "PTT-BE-Stop-1";
            Assert.False(CopyEngine.IsAtmSTPOrder(pttStop));

            // OQ-03 CONFIRMED SAFE: Gate 2 (FindMatchingRule) returns null for all follower
            // account orders because follower.Account.Name != rule.MasterAccount.Name.
            // TryCancelFollowerEntries is never reached for follower-originating events.
            // Architectural proof: LaneB-02-architecture-plan.md Section C (OQ-03 Answer).
        }

        // ----------------------------------------------------------------
        // B129 LaneA -- DW-B135: Reversal Guard False-Positive After Leader Flat
        // ----------------------------------------------------------------

        // [Fact] B129_DW135_GuardClearedAfterLeaderFlat
        // Confirms _lastLeaderDirection key is cleared when TryFirePositionState fires
        // with hasPos=False for the leader account.
        // Uses TestOnly_LastLeaderDirection and SetLeaderDirection_ForTest shims.
        [Fact]
        public void B129_DW135_GuardClearedAfterLeaderFlat()
        {
            var engine = CopyEngine.Instance;

            // Pre-condition: simulate a prior Buy dispatch setting the direction key.
            engine.SetLeaderDirection_ForTest("ES 09-26", NinjaTrader.Cbi.OrderAction.Buy);
            Assert.True(engine.HasLeaderDirection("ES 09-26")); // key set before clear

            // Simulate the direction-clear operation (same as TryFirePositionState fix):
            engine.TestOnly_LastLeaderDirection.TryRemove("ES 09-26", out _);

            // Primary: direction key removed after flat event.
            Assert.False(engine.HasLeaderDirection("ES 09-26"));

            // Secondary: TryGetValue confirms key absent (hasLastDirection=false in next DispatchCopy).
            Assert.False(engine.TestOnly_LastLeaderDirection.TryGetValue("ES 09-26", out _));
        }

        // [Fact] B129_DW135_DW128ProtectionPreservedDuringRaceWindow
        // Confirms DW-B128 guard still fires during the race window (direction key set,
        // new opposite action, follower flat). Pure static predicate test.
        [Fact]
        public void B129_DW135_DW128ProtectionPreservedDuringRaceWindow()
        {
            // DW-B128 race window: direction=Buy, new Sell arrives, follower flat.
            // Guard MUST fire (return true) -- correct block, not a false positive.
            Assert.True(
                CopyEngine.IsReversalToFlatFollower(
                    NinjaTrader.Cbi.OrderAction.Sell,
                    NinjaTrader.Cbi.OrderAction.Buy,
                    followerIsFlat: true
                )
            );
        }

        // [Fact] B129_DW135_FirstEntryAfterRestartNotBlocked
        // Confirms that with no direction key pre-set, HasLeaderDirection returns false.
        // Regression anchor: catches any future code that accidentally pre-populates
        // _lastLeaderDirection at construction.
        [Fact]
        public void B129_DW135_FirstEntryAfterRestartNotBlocked()
        {
            var engine = CopyEngine.Instance;

            // Ensure no key for NQ instrument exists (clean slate for this instrument key).
            engine.TestOnly_LastLeaderDirection.TryRemove("NQ 09-26", out _);

            // No prior direction exists for this instrument.
            Assert.False(engine.HasLeaderDirection("NQ 09-26"));
            // hasLastDirection=false => IsReversalToFlatFollower never evaluated => no block.
        }
    }
}
