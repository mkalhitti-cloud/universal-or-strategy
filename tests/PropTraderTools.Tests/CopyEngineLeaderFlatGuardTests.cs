// DW-LB-FL-02 / DW-LB-FL-01-V9: xUnit tests for IsDispatchableState, IsNativeExitOnFlatLeader,
// and TryDispatchLeaderFlat guard (3.5) -- Clone mode + BE ALL infinite flatten loop fix.
// Tests 1-3: IsDispatchableState inline mirror.
// Tests 4-6: IsNativeExitOnFlatLeader inline mirror (updated for V9: adds follower-open check).
// Tests 7-11: TryDispatchLeaderFlat inline mirror (full dispatch pipeline).
// Test 11 (V9 NEW): Close on flat leader with open follower MUST flatten the follower.
// Framework: xUnit ONLY. NEVER NUnit or MSTest.
// Approach: inline static predicates mirroring CopyEngine production code.
//   The test project targets net8.0; PropTraderTools targets net48 for NT8.
//   NT8 Order/Account/Instrument types are not instantiable without the NT8 runtime.
//   Inline predicates avoid TFM mismatch and reproduce the exact production logic.
//   TryDispatchLeaderFlat inline mirror: accepts string account/instrument names and
//   a follower list (string[]) -- equivalent dispatch semantics without NT8 types.
// ASCII-only. No lock. No async void. No return null. JS-001/JS-002/JS-021 compliant.
using System;
using System.Collections.Generic;
using Xunit;

namespace PropTraderTools.Tests
{
    public sealed class CopyEngineLeaderFlatGuardTests
    {
        // ------------------------------------------------------------------
        // Local OrderState enum -- mirrors NinjaTrader.Cbi.OrderState values.
        // Used so predicate helpers compile without the NT8 runtime.
        // ------------------------------------------------------------------
        private enum OrderState
        {
            Unknown         = 0,
            Initialized     = 1,
            PendingSubmit   = 2,
            PendingChange   = 3,
            PendingCancel   = 4,
            Working         = 5,
            Accepted        = 6,
            Filled          = 7,
            PartFilled      = 8,
            CancelSubmitted = 9,
            ChangeSubmitted = 10,
            Cancelled       = 11,
            Rejected        = 12,
        }

        // ------------------------------------------------------------------
        // Inline predicate -- mirrors CopyEngine.IsDispatchableState.
        // Production code (DW-LB-FL-02 V-01):
        //   internal static bool IsDispatchableState(OrderState state)
        //       => state == OrderState.Filled || state == OrderState.Cancelled;
        // CYC=2: 1 base + 1 || short-circuit.
        // ------------------------------------------------------------------
        private static bool IsDispatchableStateInline(OrderState state) =>
            state == OrderState.Filled || state == OrderState.Cancelled;

        // ------------------------------------------------------------------
        // Inline predicate -- mirrors CopyEngine.IsNativeExitName.
        // Production code (B65 T1, lines 2358-2371):
        //   internal static bool IsNativeExitName(string name) { ... }
        // Returns true for "Close", "Flatten", "Rev*", "Exit*".
        // ------------------------------------------------------------------
        private static bool IsNativeExitNameInline(string name)
        {
            if (name == null)
                return false;
            if (name == "Close")
                return true;
            if (name == "Flatten")
                return true;
            if (name.StartsWith("Rev", StringComparison.Ordinal))
                return true;
            if (name.StartsWith("Exit", StringComparison.Ordinal))
                return true;
            return false;
        }

        // ------------------------------------------------------------------
        // Inline predicate -- mirrors CopyEngine.IsNativeExitOnFlatLeader.
        // Production code (DW-LB-FL-02 + DW-LB-FL-01-V9):
        //   internal static bool IsNativeExitOnFlatLeader(
        //       string orderName, Account account, Instrument instrument,
        //       IReadOnlyList<Account> followerAccounts,
        //       Func<Account, Instrument, bool> hasOpenPosition)
        //   => IsNativeExitName(orderName)
        //       && !hasOpenPosition(account, instrument)
        //       && !AnyFollowerOpen(followerAccounts, instrument, hasOpenPosition);
        // V9 change: guard now only fires when leader flat AND no follower is open.
        // Uses Func<bool> (leader) and Func<string, bool> (per-follower) to avoid NT8 types.
        // CYC=3: 1 base + 2 && short-circuits.
        // ------------------------------------------------------------------
        private static bool IsNativeExitOnFlatLeaderInline(
            string orderName,
            Func<bool> leaderHasOpenPosition,
            Func<string, bool> followerHasOpenPosition,
            string[] followerAccounts
        )
        {
            if (!IsNativeExitNameInline(orderName))
                return false;
            if (leaderHasOpenPosition())
                return false;
            foreach (var acc in followerAccounts)
            {
                if (acc != null && followerHasOpenPosition(acc))
                    return false;
            }
            return true;
        }

        // ------------------------------------------------------------------
        // Inline predicate -- mirrors CopyEngine.IsNonFlatDispatchName.
        // Production code (lines 2379-2388): PTT- prefix, "Entry", ATM bracket.
        // ------------------------------------------------------------------
        private static bool IsNonFlatDispatchNameInline(string orderName)
        {
            if (orderName != null && orderName.StartsWith("PTT-", StringComparison.Ordinal))
                return true;
            if (orderName == "Entry")
                return true;
            return false;
        }

        // ------------------------------------------------------------------
        // Inline TryDispatchLeaderFlat -- mirrors full production dispatch pipeline.
        // Production code (lines 4703-4717).
        // Uses string-typed account/instrument and string[] follower list to avoid NT8 types.
        // Decision points are identical to production (CYC=8).
        // ------------------------------------------------------------------
        private static bool TryDispatchLeaderFlatInline(
            string account,
            string instrument,
            OrderState state,
            string orderName,
            string[] followerAccounts,
            Func<string, bool> isFollower,
            Func<string, string, bool> hasOpenPosition,
            Action<string, string> flattenOne
        )
        {
            if (!IsDispatchableStateInline(state))
                return false; // (1)
            if (isFollower(account))
                return false; // (2)
            if (IsNonFlatDispatchNameInline(orderName))
                return false; // (2.5+2.6)
            if (IsNativeExitOnFlatLeaderInline(orderName, () => hasOpenPosition(account, instrument), a => hasOpenPosition(a, instrument), followerAccounts))
                return false; // (3.5) DW-LB-FL-02 + DW-LB-FL-01 V9
            if (!IsNativeExitNameInline(orderName) && hasOpenPosition(account, instrument))
                return false; // (3)
            foreach (var acc in followerAccounts)
                flattenOne(acc, instrument); // (4)
            return true;
        }

        // ------------------------------------------------------------------
        // Helper constants used by TryDispatchLeaderFlat tests.
        // ------------------------------------------------------------------
        private const string LeaderName = "SimLeader";
        private const string FollowerName = "SimFollower";
        private const string InstrumentName = "ES";
        private static readonly string[] SingleFollower = new[] { FollowerName };

        // ==================================================================
        // 1. IsDispatchableState_WhenFilled_ReturnsTrue
        // ==================================================================

        [Fact]
        public void IsDispatchableState_WhenFilled_ReturnsTrue()
        {
            Assert.True(IsDispatchableStateInline(OrderState.Filled));
        }

        // ==================================================================
        // 2. IsDispatchableState_WhenCancelled_ReturnsTrue
        // ==================================================================

        [Fact]
        public void IsDispatchableState_WhenCancelled_ReturnsTrue()
        {
            Assert.True(IsDispatchableStateInline(OrderState.Cancelled));
        }

        // ==================================================================
        // 3. IsDispatchableState_WhenWorking_ReturnsFalse
        // ==================================================================

        [Fact]
        public void IsDispatchableState_WhenWorking_ReturnsFalse()
        {
            Assert.False(IsDispatchableStateInline(OrderState.Working));
        }

        // ==================================================================
        // 4. IsNativeExitOnFlatLeader_WhenNativeExitAndLeaderFlat_ReturnsTrue
        //    Defect scenario: "Close" on flat leader -> guard fires.
        // ==================================================================

        // V9 update: guard fires only when leader flat AND no follower open.
        // This test: leader flat, follower flat -> guard fires (true).
        [Fact]
        public void IsNativeExitOnFlatLeader_WhenNativeExitAndLeaderFlatAndFollowerFlat_ReturnsTrue()
        {
            bool result = IsNativeExitOnFlatLeaderInline("Close", () => false, _ => false, SingleFollower);
            Assert.True(result);
        }

        // ==================================================================
        // 5. IsNativeExitOnFlatLeader_WhenNativeExitAndLeaderHasPosition_ReturnsFalse
        //    DW-B65-01 regression: leader has position -> guard must NOT fire.
        // ==================================================================

        [Fact]
        public void IsNativeExitOnFlatLeader_WhenNativeExitAndLeaderHasPosition_ReturnsFalse()
        {
            bool result = IsNativeExitOnFlatLeaderInline("Close", () => true, _ => false, SingleFollower);
            Assert.False(result);
        }

        // ==================================================================
        // 6. IsNativeExitOnFlatLeader_WhenNonNativeExitAndLeaderFlat_ReturnsFalse
        //    Non-native exit ("PTT-BE-Stop-12345") must not trigger guard even when flat.
        // ==================================================================

        [Fact]
        public void IsNativeExitOnFlatLeader_WhenNonNativeExitAndLeaderFlat_ReturnsFalse()
        {
            bool result = IsNativeExitOnFlatLeaderInline("PTT-BE-Stop-12345", () => false, _ => false, SingleFollower);
            Assert.False(result);
        }

        // ==================================================================
        // 6b. IsNativeExitOnFlatLeader_WhenNativeExitAndLeaderFlatButFollowerOpen_ReturnsFalse
        //     DW-LB-FL-01 V9: leader flat + follower open -> guard must NOT fire (dispatch needed).
        // ==================================================================

        [Fact]
        public void IsNativeExitOnFlatLeader_WhenNativeExitAndLeaderFlatButFollowerOpen_ReturnsFalse()
        {
            bool result = IsNativeExitOnFlatLeaderInline("Close", () => false, _ => true, SingleFollower);
            Assert.False(result);
        }

        // ==================================================================
        // 7. TryDispatchLeaderFlat_WhenCloseOnFlatLeader_DoesNotFlattenFollowers
        //    DW-LB-FL-02 root cause: "Close" on already-flat leader must NOT dispatch.
        // ==================================================================

        [Fact]
        public void TryDispatchLeaderFlat_WhenCloseOnFlatLeader_DoesNotFlattenFollowers()
        {
            int flattenCallCount = 0;
            Func<string, bool> isFollower = _ => false;
            Func<string, string, bool> hasPos = (_, __) => false;
            Action<string, string> flattenOne = (_, __) => flattenCallCount++;

            bool result = TryDispatchLeaderFlatInline(
                account: LeaderName,
                instrument: InstrumentName,
                state: OrderState.Filled,
                orderName: "Close",
                followerAccounts: SingleFollower,
                isFollower: isFollower,
                hasOpenPosition: hasPos,
                flattenOne: flattenOne
            );

            Assert.False(result);
            Assert.Equal(0, flattenCallCount);
        }

        // ==================================================================
        // 8. TryDispatchLeaderFlat_WhenCloseOnLeaderWithPosition_FlattensFollowers
        //    DW-B65-01 regression guard: leader has open position -> dispatch MUST occur.
        // ==================================================================

        [Fact]
        public void TryDispatchLeaderFlat_WhenCloseOnLeaderWithPosition_FlattensFollowers()
        {
            int flattenCallCount = 0;
            Func<string, bool> isFollower = _ => false;
            Func<string, string, bool> hasPos = (_, __) => true;
            Action<string, string> flattenOne = (_, __) => flattenCallCount++;

            bool result = TryDispatchLeaderFlatInline(
                account: LeaderName,
                instrument: InstrumentName,
                state: OrderState.Filled,
                orderName: "Close",
                followerAccounts: SingleFollower,
                isFollower: isFollower,
                hasOpenPosition: hasPos,
                flattenOne: flattenOne
            );

            Assert.True(result);
            Assert.Equal(1, flattenCallCount);
        }

        // ==================================================================
        // 9. TryDispatchLeaderFlat_WhenFlattenOnFlatLeader_DoesNotFlattenFollowers
        //    "Flatten" (also a native exit name) on already-flat leader must not dispatch.
        // ==================================================================

        [Fact]
        public void TryDispatchLeaderFlat_WhenFlattenOnFlatLeader_DoesNotFlattenFollowers()
        {
            int flattenCallCount = 0;
            Func<string, bool> isFollower = _ => false;
            Func<string, string, bool> hasPos = (_, __) => false;
            Action<string, string> flattenOne = (_, __) => flattenCallCount++;

            bool result = TryDispatchLeaderFlatInline(
                account: LeaderName,
                instrument: InstrumentName,
                state: OrderState.Filled,
                orderName: "Flatten",
                followerAccounts: SingleFollower,
                isFollower: isFollower,
                hasOpenPosition: hasPos,
                flattenOne: flattenOne
            );

            Assert.False(result);
            Assert.Equal(0, flattenCallCount);
        }

        // ==================================================================
        // 10. TryDispatchLeaderFlat_WhenRevOnFlatLeader_DoesNotFlattenFollowers
        //     "RevToLong" (Rev* native exit) on already-flat leader must not dispatch.
        // ==================================================================

        [Fact]
        public void TryDispatchLeaderFlat_WhenRevOnFlatLeader_DoesNotFlattenFollowers()
        {
            int flattenCallCount = 0;
            Func<string, bool> isFollower = _ => false;
            Func<string, string, bool> hasPos = (_, __) => false;
            Action<string, string> flattenOne = (_, __) => flattenCallCount++;

            bool result = TryDispatchLeaderFlatInline(
                account: LeaderName,
                instrument: InstrumentName,
                state: OrderState.Filled,
                orderName: "RevToLong",
                followerAccounts: SingleFollower,
                isFollower: isFollower,
                hasOpenPosition: hasPos,
                flattenOne: flattenOne
            );

            Assert.False(result);
            Assert.Equal(0, flattenCallCount);
        }
        // ==================================================================
        // 11. TryDispatchLeaderFlat_WhenCloseOnFlatLeaderButFollowerOpen_FlattensFollower
        //     DW-LB-FL-01 V9 regression fix: leader flat after BE closes 6/7 contracts;
        //     follower still holds 1 contract. Close:Filled must propagate flatten to follower.
        // ==================================================================

        [Fact]
        public void TryDispatchLeaderFlat_WhenCloseOnFlatLeaderButFollowerOpen_FlattensFollower()
        {
            int flattenCallCount = 0;
            Func<string, bool> isFollower = a => a == FollowerName;
            // Leader is flat; follower has open position.
            Func<string, string, bool> hasPos = (acct, _) => acct == FollowerName;
            Action<string, string> flattenOne = (_, __) => flattenCallCount++;

            bool result = TryDispatchLeaderFlatInline(
                account: LeaderName,
                instrument: InstrumentName,
                state: OrderState.Filled,
                orderName: "Close",
                followerAccounts: SingleFollower,
                isFollower: isFollower,
                hasOpenPosition: hasPos,
                flattenOne: flattenOne
            );

            Assert.True(result);
            Assert.Equal(1, flattenCallCount);
        }
    }
}