// src/PropTraderTools/Tests/B81Tests.cs
// DW-B81-01: TryEvictFollowerBeSlot Rejected guard -- PTT-BE-Stop slot eviction on rejection.
// 3 xUnit [Fact] tests: T_DW_B81_01_01, T_DW_B81_01_02, T_DW_B81_01_03.
// Root cause: _pendingFollowerBeSlots slot not evicted on OrderState.Rejected for PTT-BE-Stop.
// Fix: TryEvictFollowerBeSlot now evicts on Rejected (name=="PTT-BE-Stop") bypassing flat-guard.
// JS-021: no lock. JS-001: no throw. JS-002: no return null. JS-033: no async void.
// xUnit only. ASCII identifiers. NT8 sealed types not instantiated -- IL token scan pattern.
using System;
using System.Reflection;
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools
{
    public sealed class B81Tests
    {
        // -----------------------------------------------------------------------
        // T_DW_B81_01_01: TryEvictFollowerBeSlot IL loads OrderState.Rejected field.
        // Verifies the DW-B81-01 guard was compiled in -- if OrderState.Rejected is
        // NOT referenced in the IL, the fix was not applied.
        // Mechanism: scan IL for ldsfld (0x7E) opcodes that resolve to OrderState fields.
        // Assert: OrderState.Rejected field token appears in TryEvictFollowerBeSlot IL.
        // -----------------------------------------------------------------------
        [Fact]
        public void T_DW_B81_01_01_TryEvictFollowerBeSlot_ILContains_OrderStateRejected()
        {
            // Arrange: locate private TryEvictFollowerBeSlot method via reflection
            var method = typeof(CopyEngine).GetMethod(
                "TryEvictFollowerBeSlot",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.NotNull(method);

            var body = method.GetMethodBody();
            Assert.NotNull(body);
            var il = body.GetILAsByteArray();
            Assert.NotNull(il);

            // Act: scan IL for ldsfld (0x7E) loading an OrderState field named "Rejected"
            var module = typeof(CopyEngine).Module;
            bool foundRejected = false;
            for (int i = 0; i < il.Length - 4; i++)
            {
                if (il[i] != 0x7E)
                    continue; // ldsfld opcode
                int token = System.BitConverter.ToInt32(il, i + 1);
                try
                {
                    var fi = module.ResolveField(token) as FieldInfo;
                    if (
                        fi != null
                        && fi.DeclaringType == typeof(OrderState)
                        && fi.Name == "Rejected"
                    )
                    {
                        foundRejected = true;
                        break;
                    }
                }
                catch
                { /* skip unresolvable tokens */
                }
            }

            // Assert: Rejected must be present (DW-B81-01 guard compiled in)
            Assert.True(
                foundRejected,
                "OrderState.Rejected must appear in TryEvictFollowerBeSlot IL (DW-B81-01 guard missing)."
            );
        }

        // -----------------------------------------------------------------------
        // T_DW_B81_01_02: TryEvictFollowerBeSlot IL contains a string load for "PTT-BE-Stop".
        // Verifies the name-guard string literal was compiled in -- prevents over-eviction
        // (any rejected order would otherwise evict the slot, not just PTT-BE-Stop).
        // Mechanism: scan IL for ldstr (0x72) opcodes, resolve string token via module.
        // Assert: string "PTT-BE-Stop" appears in TryEvictFollowerBeSlot IL.
        // -----------------------------------------------------------------------
        [Fact]
        public void T_DW_B81_01_02_TryEvictFollowerBeSlot_ILContains_PttBeStopNameGuard()
        {
            // Arrange
            var method = typeof(CopyEngine).GetMethod(
                "TryEvictFollowerBeSlot",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.NotNull(method);

            var body = method.GetMethodBody();
            Assert.NotNull(body);
            var il = body.GetILAsByteArray();
            Assert.NotNull(il);

            // Act: scan IL for ldstr (0x72) resolving to "PTT-BE-Stop"
            var module = typeof(CopyEngine).Module;
            bool foundNameGuard = false;
            for (int i = 0; i < il.Length - 4; i++)
            {
                if (il[i] != 0x72)
                    continue; // ldstr opcode
                int token = System.BitConverter.ToInt32(il, i + 1);
                try
                {
                    string s = module.ResolveString(token);
                    if (s == "PTT-BE-Stop")
                    {
                        foundNameGuard = true;
                        break;
                    }
                }
                catch
                { /* skip unresolvable tokens */
                }
            }

            // Assert: name guard string must be present
            Assert.True(
                foundNameGuard,
                "String literal 'PTT-BE-Stop' must appear in TryEvictFollowerBeSlot IL (name guard missing)."
            );
        }

        // -----------------------------------------------------------------------
        // T_DW_B81_01_03: TryEvictFollowerBeSlot IL also loads OrderState.Filled.
        // Regression guard -- Filled eviction path must still be present after DW-B81-01.
        // If this fails, the original flat-position eviction path was accidentally removed.
        // -----------------------------------------------------------------------
        [Fact]
        public void T_DW_B81_01_03_TryEvictFollowerBeSlot_ILStillContains_OrderStateFilled()
        {
            // Arrange
            var method = typeof(CopyEngine).GetMethod(
                "TryEvictFollowerBeSlot",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.NotNull(method);

            var body = method.GetMethodBody();
            Assert.NotNull(body);
            var il = body.GetILAsByteArray();
            Assert.NotNull(il);

            // Act: scan IL for ldsfld loading OrderState.Filled
            var module = typeof(CopyEngine).Module;
            bool foundFilled = false;
            for (int i = 0; i < il.Length - 4; i++)
            {
                if (il[i] != 0x7E)
                    continue; // ldsfld opcode
                int token = System.BitConverter.ToInt32(il, i + 1);
                try
                {
                    var fi = module.ResolveField(token) as FieldInfo;
                    if (fi != null && fi.DeclaringType == typeof(OrderState) && fi.Name == "Filled")
                    {
                        foundFilled = true;
                        break;
                    }
                }
                catch
                { /* skip unresolvable tokens */
                }
            }

            // Assert: Filled must still be present (regression -- original Filled path preserved)
            Assert.True(
                foundFilled,
                "OrderState.Filled must remain in TryEvictFollowerBeSlot IL (regression: original Filled path removed)."
            );
        }
    }
}
