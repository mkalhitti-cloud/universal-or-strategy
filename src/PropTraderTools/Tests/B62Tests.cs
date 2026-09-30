// B62Tests.cs -- xUnit tests for B62 entry drag sync + price-keyed dedup fix
// Block: PTT-COPIER-B62-LaneA
// Tags: T_B62_01 through T_B62_05
// Jane Street rules: JS-001, JS-021, JS-025
// xUnit only -- no NUnit, no MSTest. NT8-054: Tests\ subfolder.
// IsDedup is private -- accessed via reflection.
// EvictDedup is internal -- directly accessible (same assembly, same project).
using System.Reflection;
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools
{
    public class B62Tests
    {
        // Helper: invoke private IsDedup(string, double) via reflection
        private static bool InvokeIsDedup(CopyEngine engine, string orderId, double limitPrice)
        {
            var mi = typeof(CopyEngine).GetMethod(
                "IsDedup",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string), typeof(double) },
                null
            );
            return (bool)mi.Invoke(engine, new object[] { orderId, limitPrice });
        }

        // -------------------------------------------------------------------------
        // T_B62_01 -- IsDedup_FirstCall_ReturnsFalse
        // Verifies TryAdd on a fresh orderId succeeds; not a dup -> dispatch.
        // -------------------------------------------------------------------------
        [Fact]
        public void IsDedup_FirstCall_ReturnsFalse() // T_B62_01
        {
            var engine = CopyEngine.Instance;
            bool result = InvokeIsDedup(engine, "ord-b62-001", 7751.0);
            Assert.False(result);
        }

        // -------------------------------------------------------------------------
        // T_B62_02 -- IsDedup_SecondCallSamePrice_ReturnsTrue
        // Verifies TryAdd on existing orderId fails; dup -> skip.
        // -------------------------------------------------------------------------
        [Fact]
        public void IsDedup_SecondCallSamePrice_ReturnsTrue() // T_B62_02
        {
            var engine = CopyEngine.Instance;
            InvokeIsDedup(engine, "ord-b62-002", 7751.0); // seed
            bool result = InvokeIsDedup(engine, "ord-b62-002", 7751.0);
            Assert.True(result);
        }

        // -------------------------------------------------------------------------
        // T_B62_03 -- EvictDedup_FilledState_RemovesEntry
        // Verifies Filled terminal state triggers TryRemove; orderId unlocked for re-use.
        // -------------------------------------------------------------------------
        [Fact]
        public void EvictDedup_FilledState_RemovesEntry() // T_B62_03
        {
            var engine = CopyEngine.Instance;
            InvokeIsDedup(engine, "ord-b62-003", 7751.0); // seed
            engine.EvictDedup("ord-b62-003", OrderState.Filled);
            bool result = InvokeIsDedup(engine, "ord-b62-003", 7751.0);
            Assert.False(result);
        }

        // -------------------------------------------------------------------------
        // T_B62_04 -- EvictDedup_WorkingState_DoesNotRemove
        // Verifies Working (non-terminal) state is a no-op; entry still present.
        // -------------------------------------------------------------------------
        [Fact]
        public void EvictDedup_WorkingState_DoesNotRemove() // T_B62_04
        {
            var engine = CopyEngine.Instance;
            InvokeIsDedup(engine, "ord-b62-004", 7751.0); // seed
            engine.EvictDedup("ord-b62-004", OrderState.Working);
            bool result = InvokeIsDedup(engine, "ord-b62-004", 7751.0);
            Assert.True(result);
        }

        // -------------------------------------------------------------------------
        // T_B62_05 -- EvictDedup_CancelledState_RemovesEntry
        // Verifies Cancelled terminal state triggers TryRemove; mirrors T_B62_03 for Cancelled.
        // -------------------------------------------------------------------------
        [Fact]
        public void EvictDedup_CancelledState_RemovesEntry() // T_B62_05
        {
            var engine = CopyEngine.Instance;
            InvokeIsDedup(engine, "ord-b62-005", 7751.0); // seed
            engine.EvictDedup("ord-b62-005", OrderState.Cancelled);
            bool result = InvokeIsDedup(engine, "ord-b62-005", 7751.0);
            Assert.False(result);
        }
    }
}
