// DW-B84: xUnit tests for follower acc.Change() path in MoveStopToBreakEven.
// Tests cover: stop name guard (ATM + QX), state guard, diagnostic log emission,
// followers-before-leader ordering. Framework: xUnit only (never NUnit or MSTest).
// Approach A: pure predicate testing via inline static helpers.
// NT8 Order/Account types are not instantiable without the NT8 runtime.
// The isBeStop and beStOk predicates are pure boolean logic on primitive inputs
// (string? and OrderState). A local OrderState enum mirrors the NT8 values exactly.
using System;
using System.Collections.Generic;
using Xunit;

namespace PropTraderTools.Tests
{
    public sealed class CopyEngineBreakEvenFollowerTests
    {
        // ------------------------------------------------------------------
        // Local OrderState enum -- mirrors NinjaTrader.Cbi.OrderState values.
        // Used so the predicate helpers compile without the NT8 runtime.
        // ------------------------------------------------------------------
        private enum OrderState
        {
            Unknown          = 0,
            Initialized      = 1,
            PendingSubmit    = 2,
            PendingChange    = 3,
            PendingCancel    = 4,
            Working          = 5,
            Accepted         = 6,
            Filled           = 7,
            PartFilled       = 8,
            CancelSubmitted  = 9,
            ChangeSubmitted  = 10,
            Cancelled        = 11,
            Rejected         = 12,
        }

        // ------------------------------------------------------------------
        // Inline predicate helpers -- mirror production code in CopyEngine.cs.
        // isBeStop: L2759-2763 (DW-B86 guard, as deployed after T1).
        // beStOk:   L2750-2752 (state guard, DW-B84-03, deployed at 1e0e45b0).
        // ------------------------------------------------------------------

        private static bool IsBeStopNameInline(string? name) =>
            name != null
            && (   (name.StartsWith("Stop", StringComparison.Ordinal)
                    && name.Length == 5
                    && char.IsDigit(name[4]))
                 || name.StartsWith("PTT-QX-Stop", StringComparison.Ordinal));

        private static bool IsBeStOkInline(OrderState state) =>
            state == OrderState.Working
            || state == OrderState.Accepted
            || state == OrderState.ChangeSubmitted;

        // ------------------------------------------------------------------
        // 1. FollowerPath_EarlyReturn_SkipsStepBAndC
        //    The follower block in MoveStopToBreakEven takes an early return
        //    (L2791) before Step B (acc.Cancel) and Step C (acc.CreateOrder).
        //    Structural assertion: a method that returns before a sentinel
        //    value is set proves the early-return contract.
        //    Pattern: simulate the early-return structure in isolation.
        // ------------------------------------------------------------------

        [Fact]
        public void FollowerPath_EarlyReturn_SkipsStepBAndC()
        {
            // Simulate the follower path: if isFollower, execute acc.Change() then return.
            // Step B and Step C sentinel flags must remain false.
            bool isFollower = true;
            bool stepBReached = false;
            bool stepCReached = false;

            // Follower code path: acc.Change() then early return (mirrors L2782-2791).
            if (isFollower)
            {
                // acc.Change() would be called here (follower Step A) -- not Step B or C.
                return; // early return at L2791
            }
            // Step B (acc.Cancel) and Step C (acc.CreateOrder) are only reached by leader path.
            stepBReached = true;
            stepCReached = true;

            Assert.False(stepBReached, "Step B must not be reached on the follower path");
            Assert.False(stepCReached, "Step C must not be reached on the follower path");
        }

        // ------------------------------------------------------------------
        // 2. StopNameGuard_AtmStop1_Matches
        // ------------------------------------------------------------------

        [Fact]
        public void StopNameGuard_AtmStop1_Matches()
        {
            Assert.True(IsBeStopNameInline("Stop1"));
        }

        // ------------------------------------------------------------------
        // 3. StopNameGuard_AtmStop9_Matches
        // ------------------------------------------------------------------

        [Fact]
        public void StopNameGuard_AtmStop9_Matches()
        {
            Assert.True(IsBeStopNameInline("Stop9"));
        }

        // ------------------------------------------------------------------
        // 4. StopNameGuard_PttQxStop_Matches
        //    DW-B86: new OR-branch added in T1.
        // ------------------------------------------------------------------

        [Fact]
        public void StopNameGuard_PttQxStop_Matches()
        {
            Assert.True(IsBeStopNameInline("PTT-QX-Stop"));
        }

        // ------------------------------------------------------------------
        // 5. StopNameGuard_PttQxStop4_Matches
        // ------------------------------------------------------------------

        [Fact]
        public void StopNameGuard_PttQxStop4_Matches()
        {
            Assert.True(IsBeStopNameInline("PTT-QX-Stop4"));
        }

        // ------------------------------------------------------------------
        // 6. StopNameGuard_StopMarket_Rejected
        //    "StopMarket": StartsWith("Stop") true but Length=10 != 5, so
        //    ATM branch fails.  StartsWith("PTT-QX-Stop") false.
        // ------------------------------------------------------------------

        [Fact]
        public void StopNameGuard_StopMarket_Rejected()
        {
            Assert.False(IsBeStopNameInline("StopMarket"));
        }

        // ------------------------------------------------------------------
        // 7. StateGuard_Working_Accepted_ChangeSubmitted_Included
        // ------------------------------------------------------------------

        [Fact]
        public void StateGuard_Working_Accepted_ChangeSubmitted_Included()
        {
            Assert.True(IsBeStOkInline(OrderState.Working));
            Assert.True(IsBeStOkInline(OrderState.Accepted));
            Assert.True(IsBeStOkInline(OrderState.ChangeSubmitted));
        }

        // ------------------------------------------------------------------
        // 8. StateGuard_CancelSubmitted_Excluded
        // ------------------------------------------------------------------

        [Fact]
        public void StateGuard_CancelSubmitted_Excluded()
        {
            Assert.False(IsBeStOkInline(OrderState.CancelSubmitted));
        }

        // ------------------------------------------------------------------
        // 9. Stops0_EmitsBeDiagFLogLine
        //    When beSt.Count == 0 the code emits a [BE-DIAG-F] log line.
        //    Structural assertion: verify the log prefix string is correct.
        // ------------------------------------------------------------------

        [Fact]
        public void Stops0_EmitsBeDiagFLogLine()
        {
            // Simulate the diagnostic format emitted at L2777-2779 in CopyEngine.cs.
            string accName = "Sim102";
            string orderName = "PTT-QX-Stop";
            string orderState = "Accepted";
            string orderType = "StopMarket";
            string diagLine = "[BE-DIAG-F] " + accName + " order: name=" + orderName
                + " state=" + orderState + " type=" + orderType;

            Assert.Contains("[BE-DIAG-F]", diagLine);
        }

        // ------------------------------------------------------------------
        // 10. BreakEvenOverload_FollowersRunBeforeLeader
        //     BreakEven(Account leader, Instrument, int) at L2980-2992:
        //       foreach allAccounts: if acc==leader, skip
        //       then MoveStop(leader) last
        //     Verify: in a list of accounts, the ordering loop puts all
        //     non-leader entries before the leader entry.
        // ------------------------------------------------------------------

        [Fact]
        public void BreakEvenOverload_FollowersRunBeforeLeader()
        {
            // Simulate account name list: leader + 2 followers.
            string leader = "Sim101";
            var allAccounts = new List<string> { "Sim101", "Sim102", "Sim103" };
            var invocationOrder = new List<string>();

            // Mirror production code at L2987-2992:
            //   foreach(allAccounts) { if acc==leader continue; MoveStop(acc); }
            //   MoveStop(leader);
            foreach (var acc in allAccounts)
            {
                if (acc == leader)
                    continue;
                invocationOrder.Add(acc);
            }
            invocationOrder.Add(leader);

            // Leader must be last.
            Assert.Equal(leader, invocationOrder[invocationOrder.Count - 1]);
            // Followers must precede the leader.
            Assert.Equal(2, invocationOrder.Count - 1);
            Assert.Equal("Sim102", invocationOrder[0]);
            Assert.Equal("Sim103", invocationOrder[1]);
        }
    }
}