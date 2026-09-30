using System.Collections.Generic;
using Xunit;

namespace PropTraderTools.Tests
{
    public class B117Tests
    {
        // T1: partial snapshot count=2, leader count=3 -> ScaleLeaderTargets fires
        [Fact]
        public void ResolveFollowerTargets_PartialSnapshot_count2of3_ReturnsScaled()
        {
            var follower = new List<(double, int)> { (100.0, 2), (99.0, 1) };
            var leader = new List<(double, int)> { (100.0, 4), (99.0, 2), (98.0, 1) };
            var result = PttGlobalQuickExit.ResolveFollowerTargets(follower, leader, 7, 7);
            Assert.Equal(3, result.Count);
            Assert.Equal(4, result[0].Item2);
        }

        // T2: partial snapshot count=1, leader count=3 -> ScaleLeaderTargets fires
        [Fact]
        public void ResolveFollowerTargets_PartialSnapshot_count1of3_ReturnsScaled()
        {
            var follower = new List<(double, int)> { (100.0, 4) };
            var leader = new List<(double, int)> { (100.0, 4), (99.0, 2), (98.0, 1) };
            var result = PttGlobalQuickExit.ResolveFollowerTargets(follower, leader, 7, 7);
            Assert.Equal(3, result.Count);
            Assert.Equal(4, result[0].Item2);
        }
    }
}
