// PTT-COPIER-B70 -- B70Tests.cs
// Tests for DW-B70-01 (OCO ID reuse) and DW-B70-02 (PTT-Copy cancel).
// Ticket 1: T_B70_01, T_B70_02, T_B70_03 -- NextQxOcoId seed validation.
// Ticket 2: T_B70_04..T_B70_08 -- IsQxCancelCandidate PTT-Copy branch.

using System;
using System.Reflection;
using System.Runtime.Serialization;
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools
{
    public sealed class CopyEngineB70Tests
    {
        // -------------------------------------------------------------------------
        // T_B70_01: Two sequential calls return distinct IDs.
        // Resets _qxOcoSeq to a known value via reflection for test isolation.
        // Interlocked.Increment guarantees monotonic -- two calls must differ.
        // -------------------------------------------------------------------------
        [Fact]
        public void T_B70_01_NextQxOcoId_TwoCalls_ReturnDistinctIds()
        {
            // Arrange: reset _qxOcoSeq to known value for isolation
            var fi = typeof(CopyEngine).GetField(
                "_qxOcoSeq",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            fi.SetValue(CopyEngine.Instance, 1000);

            // Act
            string id1 = CopyEngine.Instance.NextQxOcoId();
            string id2 = CopyEngine.Instance.NextQxOcoId();

            // Assert: Interlocked.Increment guarantees monotonic -- id1 != id2
            Assert.NotEqual(id1, id2);
        }

        // -------------------------------------------------------------------------
        // T_B70_02: All IDs have "PTT-QX-" prefix.
        // Verifies prefix invariant is preserved after the seed change.
        // -------------------------------------------------------------------------
        [Fact]
        public void T_B70_02_NextQxOcoId_AllIds_StartWithPttQxPrefix()
        {
            // Arrange
            var fi = typeof(CopyEngine).GetField(
                "_qxOcoSeq",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            fi.SetValue(CopyEngine.Instance, 2000);

            // Act
            string id = CopyEngine.Instance.NextQxOcoId();

            // Assert
            Assert.StartsWith("PTT-QX-", id, StringComparison.Ordinal);
        }

        // -------------------------------------------------------------------------
        // T_B70_03: 100 sequential calls return 100 distinct values.
        // HashSet.Count < 100 would expose any counter aliasing or collision.
        // -------------------------------------------------------------------------
        [Fact]
        public void T_B70_03_NextQxOcoId_100Calls_AllDistinct()
        {
            // Arrange: seed to a stable starting value well below 99999
            var fi = typeof(CopyEngine).GetField(
                "_qxOcoSeq",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            fi.SetValue(CopyEngine.Instance, 3000);

            // Act
            var ids = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < 100; i++)
                ids.Add(CopyEngine.Instance.NextQxOcoId());

            // Assert: all 100 IDs are distinct (no counter aliasing)
            Assert.Equal(100, ids.Count);
        }

        // -------------------------------------------------------------------------
        // MakeOrder: create an NT8 Order via FormatterServices (sealed class bypass).
        // Matches pattern in CopyEngineTests.cs lines 3133-3189.
        // Uses FormatterServices.GetUninitializedObject -- no constructor invoked.
        // -------------------------------------------------------------------------
        private static Order MakeOrder(OrderState state, string name)
        {
            // NT8 Order is sealed -- use FormatterServices to bypass constructor.
            var order = (Order)FormatterServices.GetUninitializedObject(typeof(Order));

            // Set OrderState: try property first, then backing field.
            var stateProp = typeof(Order).GetProperty(
                "OrderState",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            );
            if (stateProp != null && stateProp.CanWrite)
            {
                stateProp.SetValue(order, state);
            }
            else
            {
                var stateField =
                    typeof(Order).GetField(
                        "orderState",
                        BindingFlags.NonPublic | BindingFlags.Instance
                    )
                    ?? typeof(Order).GetField(
                        "_orderState",
                        BindingFlags.NonPublic | BindingFlags.Instance
                    )
                    ?? typeof(Order).GetField(
                        "OrderState",
                        BindingFlags.NonPublic | BindingFlags.Instance
                    );
                stateField?.SetValue(order, state);
            }

            // Set Name: try property first, then backing field.
            var nameProp = typeof(Order).GetProperty(
                "Name",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            );
            if (nameProp != null && nameProp.CanWrite)
            {
                nameProp.SetValue(order, name);
            }
            else
            {
                var nameField =
                    typeof(Order).GetField("name", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? typeof(Order).GetField(
                        "_name",
                        BindingFlags.NonPublic | BindingFlags.Instance
                    )
                    ?? typeof(Order).GetField(
                        "Name",
                        BindingFlags.NonPublic | BindingFlags.Instance
                    );
                nameField?.SetValue(order, name);
            }

            return order;
        }

        // -------------------------------------------------------------------------
        // T_B70_04: IsQxCancelCandidate returns true for exact "PTT-Copy" (new branch 5).
        // -------------------------------------------------------------------------
        [Fact]
        public void T_B70_04_IsQxCancelCandidate_PttCopyExact_ReturnsTrue()
        {
            // Arrange: order with Name = "PTT-Copy" (exact base signal name used by DispatchCopy)
            var order = MakeOrder(OrderState.Working, "PTT-Copy");

            // Act + Assert: new branch (5) must fire for the exact signal name
            Assert.True(
                CopyEngine.IsQxCancelCandidate(order),
                "IsQxCancelCandidate: 'PTT-Copy' must return true (PTT-Copy prefix branch (5))"
            );
        }

        // -------------------------------------------------------------------------
        // T_B70_05: IsQxCancelCandidate returns true for "PTT-Copy-Variant" (StartsWith coverage).
        // -------------------------------------------------------------------------
        [Fact]
        public void T_B70_05_IsQxCancelCandidate_PttCopyVariant_ReturnsTrue()
        {
            // Arrange: order name with PTT-Copy prefix plus suffix
            var order = MakeOrder(OrderState.Working, "PTT-Copy-Variant");

            // Act + Assert: StartsWith("PTT-Copy") must match all variants
            Assert.True(
                CopyEngine.IsQxCancelCandidate(order),
                "IsQxCancelCandidate: 'PTT-Copy-Variant' must return true (StartsWith PTT-Copy)"
            );
        }

        // -------------------------------------------------------------------------
        // T_B70_06: IsQxCancelCandidate returns true for "PTT-QX-Stop" (regression guard branch 3).
        // -------------------------------------------------------------------------
        [Fact]
        public void T_B70_06_IsQxCancelCandidate_PttQxStop_ReturnsTrue_Regression()
        {
            // Arrange: PTT-QX- prefix order (pre-existing branch (3))
            var order = MakeOrder(OrderState.Working, "PTT-QX-Stop");

            // Act + Assert: branch (3) must not be broken by the new branch (5)
            Assert.True(
                CopyEngine.IsQxCancelCandidate(order),
                "IsQxCancelCandidate: 'PTT-QX-Stop' must return true -- branch (3) regression guard"
            );
        }

        // -------------------------------------------------------------------------
        // T_B70_07: IsQxCancelCandidate returns true for "Stop1" (regression guard branch 2 ATM).
        // -------------------------------------------------------------------------
        [Fact]
        public void T_B70_07_IsQxCancelCandidate_Stop1_ReturnsTrue_Regression()
        {
            // Arrange: ATM bracket name (pre-existing branch (2) via IsAtmBracketName)
            var order = MakeOrder(OrderState.Working, "Stop1");

            // Act + Assert: branch (2) must not be broken by the new branch (5)
            Assert.True(
                CopyEngine.IsQxCancelCandidate(order),
                "IsQxCancelCandidate: 'Stop1' must return true -- branch (2) ATM regression guard"
            );
        }

        // -------------------------------------------------------------------------
        // T_B70_08: IsQxCancelCandidate returns false for "Entry" (none of 5 branches fire).
        // -------------------------------------------------------------------------
        [Fact]
        public void T_B70_08_IsQxCancelCandidate_EntryName_ReturnsFalse()
        {
            // Arrange: a non-bracket, non-PTT order name
            var order = MakeOrder(OrderState.Working, "Entry");

            // Act + Assert: none of the 5 branches fires -- must return false
            Assert.False(
                CopyEngine.IsQxCancelCandidate(order),
                "IsQxCancelCandidate: 'Entry' must return false (not a bracket or PTT-prefixed order)"
            );
        }
    }
}
