using PropTraderTools;
using Xunit;

namespace PropTraderTools.Tests
{
    public class B120Tests
    {
        // B120 DW-B129: NeedsLeaderFallbackFlatten -- true path
        // beCancelCount=1 (B118 ran), snapshotCount=0 (empty book), posQty=7 (open position)
        [Fact]
        public void Test_NeedsLeaderFallbackFlatten_True_WhenBECancelledAndSnapshotEmpty()
        {
            Assert.True(PttGlobalQuickExit.NeedsLeaderFallbackFlatten(1, 0, 7));
        }

        // B120 DW-B129: NeedsLeaderFallbackFlatten -- false when beCancelCount=0 (normal QX path)
        [Fact]
        public void Test_NeedsLeaderFallbackFlatten_False_WhenBECancelCountZero()
        {
            Assert.False(PttGlobalQuickExit.NeedsLeaderFallbackFlatten(0, 0, 7));
        }

        // B120 DW-B129: NeedsLeaderFallbackFlatten -- false when snapshot has targets (normal QX runs)
        [Fact]
        public void Test_NeedsLeaderFallbackFlatten_False_WhenSnapshotHasTargets()
        {
            Assert.False(PttGlobalQuickExit.NeedsLeaderFallbackFlatten(1, 3, 7));
        }
    }
}
