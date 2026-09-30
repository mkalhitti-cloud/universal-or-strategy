// B116Tests.cs -- DW-B124 fix tests for ScaleLeaderTargets and ResolveFollowerTargets.
// Block: B116. Framework: xUnit Fact-only. JS-021: no lock. JS-033: no async void.
// Seam: ScaleLeaderTargets and ResolveFollowerTargets are internal static on PttGlobalQuickExit.
// No NT8 host required -- methods use only List<(double,int)> and int arguments.

using System.Collections.Generic;
using Xunit;

namespace PropTraderTools.Tests
{
    public class B116Tests
    {
        // -------------------------------------------------------------------------
        // T2-1: ScaleLeaderTargets_EqualQty_IdenticalSplit
        //
        // What is tested: when followerPosQty == leaderPosQty, output is identical to input.
        // Inputs: leaderTargets=[(0.0,4),(0.0,2),(0.0,1)], leaderPosQty=7, followerPosQty=7.
        // -------------------------------------------------------------------------
        [Fact]
        public void ScaleLeaderTargets_EqualQty_IdenticalSplit()
        {
            var leaderTargets = new List<(double Price, int Qty)> { (0.0, 4), (0.0, 2), (0.0, 1) };

            var result = PttGlobalQuickExit.ScaleLeaderTargets(leaderTargets, 7, 7);

            Assert.Equal(4, result[0].Qty);
            Assert.Equal(2, result[1].Qty);
            Assert.Equal(1, result[2].Qty);
            int sum = 0;
            foreach (var r in result)
                sum += r.Qty;
            Assert.Equal(7, sum);
        }

        // -------------------------------------------------------------------------
        // T2-2: ScaleLeaderTargets_HalfQty_SumEqualsFollowerQty
        //
        // What is tested: when followerPosQty < leaderPosQty, sum == followerPosQty, each >= 1.
        // Inputs: leaderTargets=[(0.0,4),(0.0,2),(0.0,1)], leaderPosQty=7, followerPosQty=4.
        // -------------------------------------------------------------------------
        [Fact]
        public void ScaleLeaderTargets_HalfQty_SumEqualsFollowerQty()
        {
            var leaderTargets = new List<(double Price, int Qty)> { (0.0, 4), (0.0, 2), (0.0, 1) };

            var result = PttGlobalQuickExit.ScaleLeaderTargets(leaderTargets, 4, 7);

            Assert.Equal(3, result.Count);
            int sum = 0;
            foreach (var r in result)
            {
                Assert.True(r.Qty >= 1);
                sum += r.Qty;
            }
            Assert.Equal(4, sum);
        }

        // -------------------------------------------------------------------------
        // T2-3: ScaleLeaderTargets_ZeroLeaderPosQty_ReturnsEmpty
        //
        // What is tested: leaderPosQty=0 guard returns empty list (no divide-by-zero).
        // Inputs: leaderTargets=[(0.0,4),(0.0,2),(0.0,1)], leaderPosQty=0, followerPosQty=7.
        // -------------------------------------------------------------------------
        [Fact]
        public void ScaleLeaderTargets_ZeroLeaderPosQty_ReturnsEmpty()
        {
            var leaderTargets = new List<(double Price, int Qty)> { (0.0, 4), (0.0, 2), (0.0, 1) };

            var result = PttGlobalQuickExit.ScaleLeaderTargets(leaderTargets, 7, 0);

            Assert.Equal(0, result.Count);
        }

        // -------------------------------------------------------------------------
        // T2-4: ResolveFollowerTargets_NonEmptySnapshot_ReturnsSelf
        //
        // What is tested: non-empty follower snapshot is returned unchanged.
        // Inputs: followerSnapshot=[(0.0,4),(0.0,2),(0.0,1)], leaderTargets=[(0.0,3),(0.0,2),(0.0,2)],
        //         followerPosQty=7, leaderPosQty=7.
        // -------------------------------------------------------------------------
        [Fact]
        public void ResolveFollowerTargets_NonEmptySnapshot_ReturnsSelf()
        {
            var followerSnapshot = new List<(double Price, int Qty)>
            {
                (0.0, 4),
                (0.0, 2),
                (0.0, 1),
            };
            var leaderTargets = new List<(double Price, int Qty)> { (0.0, 3), (0.0, 2), (0.0, 2) };

            var result = PttGlobalQuickExit.ResolveFollowerTargets(
                followerSnapshot,
                leaderTargets,
                7,
                7
            );

            Assert.Equal(4, result[0].Qty);
        }

        // -------------------------------------------------------------------------
        // T2-5: ResolveFollowerTargets_EmptySnapshotFullLeader_ReturnsScaled
        //
        // What is tested: empty snapshot + valid leader data returns scaled leader targets (DW-B124 fix path).
        // Inputs: followerSnapshot=[], leaderTargets=[(0.0,4),(0.0,2),(0.0,1)],
        //         leaderPosQty=7, followerPosQty=7.
        // -------------------------------------------------------------------------
        [Fact]
        public void ResolveFollowerTargets_EmptySnapshotFullLeader_ReturnsScaled()
        {
            var followerSnapshot = new List<(double Price, int Qty)>();
            var leaderTargets = new List<(double Price, int Qty)> { (0.0, 4), (0.0, 2), (0.0, 1) };

            var result = PttGlobalQuickExit.ResolveFollowerTargets(
                followerSnapshot,
                leaderTargets,
                7,
                7
            );

            Assert.Equal(3, result.Count);
            Assert.Equal(4, result[0].Qty);
            Assert.Equal(2, result[1].Qty);
            Assert.Equal(1, result[2].Qty);
        }

        // -------------------------------------------------------------------------
        // T2-6: ResolveFollowerTargets_EmptySnapshotEmptyLeader_ReturnsEmpty
        //
        // What is tested: empty snapshot + empty leader returns empty (DW-B120 CalcTNQty fallback preserved).
        // Inputs: followerSnapshot=[], leaderTargets=[], followerPosQty=7, leaderPosQty=7.
        // -------------------------------------------------------------------------
        [Fact]
        public void ResolveFollowerTargets_EmptySnapshotEmptyLeader_ReturnsEmpty()
        {
            var followerSnapshot = new List<(double Price, int Qty)>();
            var leaderTargets = new List<(double Price, int Qty)>();

            var result = PttGlobalQuickExit.ResolveFollowerTargets(
                followerSnapshot,
                leaderTargets,
                7,
                7
            );

            Assert.Equal(0, result.Count);
        }
    }
}
