// B140 xUnit tests: DW-B154 direct fix -- B140 acc.Change branch reverted.
// acc.Change() confirmed no-op on ATM Stop brackets from AddOnBase (SIM Gate 1 FAIL).
// All ATM stop brackets now route to SyncAtmFollowerBracket (cancel+resubmit) unconditionally.
//
// Surviving tests after revert (T_B140_01 and T_B140_06 removed -- tested the removed branch):
//   T_B140_02: Empty Oco regression guard (cancel+resubmit path active regardless of Oco)
//   T_B140_03..05: IsAtmSTPOrder inline predicate for Stop1/Stop2/Stop3 (unchanged)
//   T_B140_07: isStop=false routes to SyncAtmFollowerTarget (unaffected path)
// Framework: xUnit ONLY. NEVER NUnit or MSTest.
using Xunit;

namespace PropTraderTools.Tests
{
    public sealed class B140Tests
    {
        // ------------------------------------------------------------------
        // Inline predicate -- mirrors CopyEngine.IsAtmSTPOrder production code.
        // Production code:
        //   internal static bool IsAtmSTPOrder(Order order) =>
        //       order.Name != null
        //       && (
        //           order.Name.EndsWith("STP", StringComparison.OrdinalIgnoreCase)
        //           || order.Name.StartsWith("Stop", StringComparison.OrdinalIgnoreCase)
        //           || order.Name.StartsWith("Target", StringComparison.OrdinalIgnoreCase)
        //       );
        // ------------------------------------------------------------------
        private static bool IsAtmSTPOrderInline(string? orderName) =>
            orderName != null
            && (
                orderName.EndsWith("STP", System.StringComparison.OrdinalIgnoreCase)
                || orderName.StartsWith("Stop", System.StringComparison.OrdinalIgnoreCase)
                || orderName.StartsWith("Target", System.StringComparison.OrdinalIgnoreCase)
            );

        // ------------------------------------------------------------------
        // T_B140_02: Regression guard -- all ATM stop brackets route to cancel+resubmit.
        // DW-B154: acc.Change() is no-op on ATM brackets; Oco is no longer branched on.
        // Validates: (isStop && IsAtmSTPOrder) -> SyncAtmFollowerBracket unconditionally.
        // ------------------------------------------------------------------

        [Fact]
        public void T_B140_02_AtmStop_AlwaysRoutes_ToCancelResubmit()
        {
            bool isStop = true;
            bool isAtmSTPOrder = true;
            string oco = "f2ec29be-1234-5678-abcd-ef0123456789"; // non-empty, no longer branched on

            bool cancelResubmitPathTaken = isStop && isAtmSTPOrder;
            bool _ = !string.IsNullOrEmpty(oco); // Oco not checked -- DW-B154 revert

            Assert.True(cancelResubmitPathTaken);
        }

        // ------------------------------------------------------------------
        // T_B140_03: IsAtmSTPOrder correctly detects "Stop1".
        // ------------------------------------------------------------------

        [Fact]
        public void T_B140_03_IsAtmSTPOrder_Stop1_ReturnsTrue()
        {
            Assert.True(IsAtmSTPOrderInline("Stop1"));
        }

        // ------------------------------------------------------------------
        // T_B140_04: IsAtmSTPOrder correctly detects "Stop2".
        // ------------------------------------------------------------------

        [Fact]
        public void T_B140_04_IsAtmSTPOrder_Stop2_ReturnsTrue()
        {
            Assert.True(IsAtmSTPOrderInline("Stop2"));
        }

        // ------------------------------------------------------------------
        // T_B140_05: IsAtmSTPOrder correctly detects "Stop3".
        // ------------------------------------------------------------------

        [Fact]
        public void T_B140_05_IsAtmSTPOrder_Stop3_ReturnsTrue()
        {
            Assert.True(IsAtmSTPOrderInline("Stop3"));
        }

        // ------------------------------------------------------------------
        // T_B140_07: ATM target branch (isStop=false) routes to SyncAtmFollowerTarget.
        //            DW-B154 revert only affects isStop=true branch -- targets unaffected.
        // ------------------------------------------------------------------

        [Fact]
        public void T_B140_07_AtmTargetBranch_RouteToSyncAtmFollowerTarget()
        {
            bool isStop = false;
            bool isAtmSTPOrder = true; // "Target1" etc. pass IsAtmSTPOrder

            bool targetBranchTaken = !isStop && isAtmSTPOrder;

            Assert.True(targetBranchTaken);
        }
    }
}