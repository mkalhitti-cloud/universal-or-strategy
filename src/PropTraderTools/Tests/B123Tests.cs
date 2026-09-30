// B123Tests.cs -- xUnit tests for B123 DW-B133: forced 2-target Execute overload.
// Confirms Build2TargetList arithmetic and reflection presence of both Execute overloads.
// JS-051: xUnit only. JS-053: Assert.Equal / Assert.True / Assert.NotNull. ASCII-only.
using System.Collections.Generic;
using Xunit;

namespace PropTraderTools.Tests
{
    public class B123Tests
    {
        [Fact]
        public void T_B123_01_Build2TargetList_7qty_T1IsHeavy()
        {
            var result = TradeCopierPanel.Build2TargetList(7);
            Assert.Equal(2, result.Count);
            Assert.Equal(4, result[0].Qty); // T1 heavy
            Assert.Equal(3, result[1].Qty); // T2 residual
        }

        [Fact]
        public void T_B123_02_Build2TargetList_6qty_T1EqualsT2()
        {
            var result = TradeCopierPanel.Build2TargetList(6);
            Assert.Equal(2, result.Count);
            Assert.Equal(3, result[0].Qty);
            Assert.Equal(3, result[1].Qty);
        }

        [Fact]
        public void T_B123_03_Build2TargetList_AlwaysReturnsCount2()
        {
            for (int qty = 1; qty <= 9; qty++)
            {
                var result = TradeCopierPanel.Build2TargetList(qty);
                Assert.Equal(2, result.Count);
            }
        }

        [Fact]
        public void T_B123_04_ForcedOverload_Exists()
        {
            var t = typeof(PttGlobalQuickExit);
            var m = t.GetMethod(
                "Execute",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null,
                new[] { typeof(List<(double Price, int Qty)>) },
                null
            );
            Assert.NotNull(m);
        }

        [Fact]
        public void T_B123_05_NoArgOverload_StillExists()
        {
            var t = typeof(PttGlobalQuickExit);
            var m = t.GetMethod(
                "Execute",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null,
                System.Type.EmptyTypes,
                null
            );
            Assert.NotNull(m);
        }
    }
}
