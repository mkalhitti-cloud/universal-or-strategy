// Wave1LaneBT3Tests.cs -- xUnit verification tests for WAVE1-LANE-B T-3.
// Spec requirements covered: B-03 (ExecuteFollowers), B-04 (Execute overloads), B-07 (ExecuteOne).
// Mirrors guard predicate logic from PttGlobalQuickExit.cs
// (src/PropTraderTools/Features/PttGlobalQuickExit.cs).
//
// Direct ProjectReference impossible across TFMs:
//   PropTraderTools targets net48 (NT8 requirement).
//   PropTraderTools.Tests targets net8.0.
//   NT8 Account/Instrument/Position are not instantiable without the NT8 runtime.
//   Inline mirror of guard predicates is the established pattern
//   (see Wave1LaneBT2Tests.cs, PttBreakEvenB72Tests.cs).
//
// Framework: xUnit ONLY. NEVER NUnit or MSTest. ASCII-only. No DateTime.Now. No lock().
// JS-021: no lock. JS-001: no throw. JS-002: no null return. JS-033: no async void.
using System.Collections.Generic;
using Xunit;

namespace PropTraderTools.Tests
{
    /// <summary>
    /// Inline mirror tests for PttGlobalQuickExit guard logic.
    /// Tests the guard predicates that protect ExecuteFollowers (B-03),
    /// Execute overloads (B-04), and ExecuteOne (B-07).
    /// </summary>
    public sealed class Wave1LaneBT3Tests
    {
        // ------------------------------------------------------------------
        // Inline mirror of PttGlobalQuickExit.NeedsLeaderFallbackFlatten.
        // Source: PttGlobalQuickExit.cs L300.
        //   return beCancelCount > 0 && snapshotCount == 0 && posQty > 0;
        // CYC=3: one && chain with three operands.
        // B-04 guard: when this returns true, Execute calls acc.Flatten and skips ExecuteOne.
        // ------------------------------------------------------------------
        private static bool NeedsLeaderFallbackFlatten(
            int beCancelCount,
            int snapshotCount,
            int posQty
        )
        {
            return beCancelCount > 0 && snapshotCount == 0 && posQty > 0;
        }

        // ------------------------------------------------------------------
        // Inline mirror of PttGlobalQuickExit.IsInvalidForcedTargets.
        // Source: PttGlobalQuickExit.cs L491.
        //   return targets == null || targets.Count < 2;
        // CYC=2: (1) null, (2) count < 2.
        // B-04 guard: Execute(forcedTargets) early-returns when this is true.
        // ------------------------------------------------------------------
        private static bool IsInvalidForcedTargets(int? count)
        {
            // null represented by count==null, non-null by count value
            return count == null || count.Value < 2;
        }

        // ------------------------------------------------------------------
        // Inline mirror of PttGlobalQuickExit.ResolveFollowerTargets full logic.
        // Source: PttGlobalQuickExit.cs L645-L652.
        //   if (followerSnapshot.Count > 0
        //       && (leaderTargets.Count == 0 || followerSnapshot.Count == leaderTargets.Count))
        //       return followerSnapshot;
        //   if (leaderTargets.Count == 0 || followerPosQty <= 0)
        //       return followerSnapshot;
        //   return ScaleLeaderTargets(leaderTargets, followerPosQty, leaderPosQty);
        // CYC=4. B-03 guard: drives which target list is passed to follower ExecuteOne.
        // ------------------------------------------------------------------
        private static bool ResolveFollowerTargets_UsesFollowerSnapshot(
            int followerSnapshotCount,
            int leaderTargetsCount,
            int followerPosQty
        )
        {
            if (
                followerSnapshotCount > 0
                && (leaderTargetsCount == 0 || followerSnapshotCount == leaderTargetsCount)
            )
                return true; // uses follower snapshot path
            if (leaderTargetsCount == 0 || followerPosQty <= 0)
                return true; // also returns follower snapshot (fallback)
            return false; // scales from leader
        }

        // ------------------------------------------------------------------
        // Inline mirror of PttGlobalQuickExit.ScaleLeaderTargets proportional logic.
        // Source: PttGlobalQuickExit.cs L607-L622.
        // B-03 guard: when follower snapshot is empty, targets are derived from leader.
        // Only tests the arithmetic core -- no NT8 types required.
        // ------------------------------------------------------------------
        private static List<(double Price, int Qty)> ScaleLeaderTargets(
            List<(double Price, int Qty)> leaderTargets,
            int followerPosQty,
            int leaderPosQty
        )
        {
            var result = new List<(double Price, int Qty)>(leaderTargets.Count);
            if (leaderPosQty <= 0)
                return result;
            int allocated = 0;
            for (int i = 0; i < leaderTargets.Count; i++)
            {
                int qty;
                if (i == leaderTargets.Count - 1)
                    qty = System.Math.Max(1, followerPosQty - allocated);
                else
                    qty = System.Math.Max(
                        1,
                        (int)
                            System.Math.Round(
                                (double)leaderTargets[i].Qty * followerPosQty / leaderPosQty
                            )
                    );
                allocated += qty;
                result.Add((leaderTargets[i].Price, qty));
            }
            return result;
        }

        // ------------------------------------------------------------------
        // Inline mirror of ExecuteOne follower-guard logic (skipIfFollower flag).
        // Source: PttGlobalQuickExit.cs L350.
        //   if (!skipIfFollower) { ... CopyEngine._qxCancelInProgress.TryAdd ... }
        // CYC=2: follower guard (1) + leader path (2).
        // B-07 guard: when skipIfFollower=false, intent-guard is armed before submit.
        // ------------------------------------------------------------------
        private static bool ExecuteOne_ArmsQxGuard(bool skipIfFollower)
        {
            // Returns true when the follower path arms _qxCancelInProgress guard
            return !skipIfFollower;
        }

        // ------------------------------------------------------------------
        // Inline mirror of IsNativeTargetOrder predicate.
        // Source: PttGlobalQuickExit.cs L458-L461.
        //   return !IsNullOrEmpty(name) && name.StartsWith("Target") && name.Length > 6 && IsDigit(name[6]);
        // CYC=4. B-04: used inside SnapshotTargetOrders which is called by Execute.
        // ------------------------------------------------------------------
        private static bool IsNativeTargetOrder(string name)
        {
            return !string.IsNullOrEmpty(name)
                && name.StartsWith("Target", System.StringComparison.Ordinal)
                && name.Length > 6
                && char.IsDigit(name[6]);
        }

        // ------------------------------------------------------------------
        // Inline mirror of IsPttTargetOrder predicate.
        // Source: PttGlobalQuickExit.cs L474-L479.
        // CYC=5. B-04: used inside SnapshotTargetOrders.
        // ------------------------------------------------------------------
        private static bool IsPttTargetOrder(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            return (
                    name.StartsWith("PTT-QX-T", System.StringComparison.Ordinal)
                    && name.Length > 8
                    && char.IsDigit(name[8])
                ) || name.StartsWith("PTT-BE-Target-", System.StringComparison.Ordinal);
        }

        // ==================================================================
        // B-03: ExecuteFollowers guard behaviour
        // ==================================================================

        /// <summary>
        /// B-03: When follower snapshot count matches leader count (full match),
        /// ResolveFollowerTargets uses the follower snapshot directly.
        /// ExecuteFollowers passes follower's own target list to ExecuteOne.
        /// </summary>
        [Fact]
        public void ExecuteFollowers_WhenFollowerSnapshotMatchesLeader_UsesFollowerSnapshot()
        {
            // followerSnapshot.Count==2 == leaderTargets.Count==2 => use follower snapshot
            bool usesFollower = ResolveFollowerTargets_UsesFollowerSnapshot(
                followerSnapshotCount: 2,
                leaderTargetsCount: 2,
                followerPosQty: 2
            );
            Assert.True(usesFollower);
        }

        /// <summary>
        /// B-03: When follower snapshot is empty but leader has 2 targets and follower has a
        /// position, ResolveFollowerTargets scales leader targets to follower position size.
        /// ExecuteFollowers passes scaled targets (not empty list) to ExecuteOne.
        /// </summary>
        [Fact]
        public void ExecuteFollowers_WhenFollowerSnapshotEmpty_ScalesFromLeader()
        {
            // followerSnapshot.Count==0, leader has targets, follower has a position => scale
            bool usesFollower = ResolveFollowerTargets_UsesFollowerSnapshot(
                followerSnapshotCount: 0,
                leaderTargetsCount: 2,
                followerPosQty: 3
            );
            Assert.False(usesFollower); // false = scales from leader
        }

        /// <summary>
        /// B-03: ScaleLeaderTargets allocates follower quantities proportionally.
        /// With 1 leader contract and 2 follower contracts, single tranche gets qty=2.
        /// </summary>
        [Fact]
        public void ExecuteFollowers_ScaleLeaderTargets_ProportionalAllocation()
        {
            // Leader: 1 contract at price 5000. Follower: 2 contracts.
            var leaderTargets = new List<(double Price, int Qty)> { (5000.0, 1) };
            var scaled = ScaleLeaderTargets(leaderTargets, followerPosQty: 2, leaderPosQty: 1);
            Assert.Single(scaled);
            Assert.Equal(5000.0, scaled[0].Price);
            Assert.Equal(2, scaled[0].Qty);
        }

        /// <summary>
        /// B-03: When follower snapshot is partial (count less than leaderCount),
        /// ResolveFollowerTargets falls through to ScaleLeaderTargets (not partial snapshot).
        /// DW-B125: partial snapshots are rejected.
        /// </summary>
        [Fact]
        public void ExecuteFollowers_WhenFollowerSnapshotPartial_ScalesFromLeader()
        {
            // followerSnapshot.Count==1, leaderCount==2 => partial, scale from leader
            bool usesFollower = ResolveFollowerTargets_UsesFollowerSnapshot(
                followerSnapshotCount: 1,
                leaderTargetsCount: 2,
                followerPosQty: 2
            );
            Assert.False(usesFollower); // partial rejected, scale from leader
        }

        // ==================================================================
        // B-04: Execute overload guard behaviour
        // ==================================================================

        /// <summary>
        /// B-04: NeedsLeaderFallbackFlatten returns true when B118 cancelled BE orders
        /// (beCancelCount > 0) AND snapshot is empty AND leader still has a position.
        /// Execute calls acc.Flatten and skips ExecuteOne in this case.
        /// </summary>
        [Fact]
        public void Execute_WhenBeOrdersCancelledAndSnapshotEmpty_FlattenGuardFires()
        {
            bool flatten = NeedsLeaderFallbackFlatten(
                beCancelCount: 1,
                snapshotCount: 0,
                posQty: 1
            );
            Assert.True(flatten);
        }

        /// <summary>
        /// B-04: NeedsLeaderFallbackFlatten returns false when snapshot is non-empty.
        /// Execute proceeds to ExecuteOne and ExecuteFollowers (normal path).
        /// </summary>
        [Fact]
        public void Execute_WhenSnapshotNonEmpty_FlattenGuardDoesNotFire()
        {
            bool flatten = NeedsLeaderFallbackFlatten(
                beCancelCount: 1,
                snapshotCount: 2,
                posQty: 1
            );
            Assert.False(flatten);
        }

        /// <summary>
        /// B-04: NeedsLeaderFallbackFlatten returns false when beCancelCount is 0,
        /// even if snapshot is empty. B118 did not cancel any BE orders -- skip flatten.
        /// </summary>
        [Fact]
        public void Execute_WhenNoBeCancelled_FlattenGuardDoesNotFire()
        {
            bool flatten = NeedsLeaderFallbackFlatten(
                beCancelCount: 0,
                snapshotCount: 0,
                posQty: 1
            );
            Assert.False(flatten);
        }

        /// <summary>
        /// B-04: Execute(forcedTargets) guard -- IsInvalidForcedTargets returns true when
        /// the list is null. Execute returns early without any order submission.
        /// </summary>
        [Fact]
        public void Execute_ForcedTargets_WhenNull_EarlyReturn()
        {
            bool invalid = IsInvalidForcedTargets(count: null);
            Assert.True(invalid);
        }

        /// <summary>
        /// B-04: Execute(forcedTargets) guard -- IsInvalidForcedTargets returns true when
        /// the list has fewer than 2 entries (DW-B133: QAll2t requires at least 2 targets).
        /// </summary>
        [Fact]
        public void Execute_ForcedTargets_WhenLessThan2Entries_EarlyReturn()
        {
            bool invalid = IsInvalidForcedTargets(count: 1);
            Assert.True(invalid);
        }

        /// <summary>
        /// B-04: Execute(forcedTargets) guard -- IsInvalidForcedTargets returns false when
        /// the list has 2 or more entries. Execute proceeds to the account loop.
        /// </summary>
        [Fact]
        public void Execute_ForcedTargets_WhenTwoOrMoreEntries_Proceeds()
        {
            bool invalid = IsInvalidForcedTargets(count: 2);
            Assert.False(invalid);
        }

        /// <summary>
        /// B-04: SnapshotTargetOrders -- IsNativeTargetOrder returns true for "Target1"
        /// (canonical ATM bracket name used by NT8 ATM templates).
        /// </summary>
        [Fact]
        public void Execute_SnapshotTargetOrders_NativeTarget1_Detected()
        {
            Assert.True(IsNativeTargetOrder("Target1"));
        }

        /// <summary>
        /// B-04: SnapshotTargetOrders -- IsPttTargetOrder returns true for "PTT-QX-T1"
        /// (PTT quick-exit target bracket name pattern).
        /// </summary>
        [Fact]
        public void Execute_SnapshotTargetOrders_PttQxT1_Detected()
        {
            Assert.True(IsPttTargetOrder("PTT-QX-T1"));
        }

        /// <summary>
        /// B-04: SnapshotTargetOrders -- IsPttTargetOrder returns true for "PTT-BE-Target-Stop"
        /// (PTT break-even re-arm target bracket name pattern).
        /// </summary>
        [Fact]
        public void Execute_SnapshotTargetOrders_PttBeTarget_Detected()
        {
            Assert.True(IsPttTargetOrder("PTT-BE-Target-Stop"));
        }

        // ==================================================================
        // B-07: ExecuteOne guard behaviour
        // ==================================================================

        /// <summary>
        /// B-07: When skipIfFollower=false (follower path), ExecuteOne arms the
        /// _qxCancelInProgress intent-guard before calling executor.Execute.
        /// Guard prevents TryReplacePttBeBrackets from racing during PTT-QX submit window.
        /// </summary>
        [Fact]
        public void ExecuteOne_WhenSkipIfFollowerFalse_ArmsQxCancelGuard()
        {
            bool armsGuard = ExecuteOne_ArmsQxGuard(skipIfFollower: false);
            Assert.True(armsGuard);
        }

        /// <summary>
        /// B-07: When skipIfFollower=true (leader path), ExecuteOne does NOT arm the
        /// intent-guard. Leader brackets are handled by PttQuickExit.Execute's own cancel logic.
        /// </summary>
        [Fact]
        public void ExecuteOne_WhenSkipIfFollowerTrue_DoesNotArmGuard()
        {
            bool armsGuard = ExecuteOne_ArmsQxGuard(skipIfFollower: true);
            Assert.False(armsGuard);
        }
    }
}