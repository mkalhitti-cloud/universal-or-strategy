// B191Tests.cs -- xUnit tests for B191 T2: Build2TargetList arithmetic coverage.
// Tests: ceiling-split oracles (qty 5, qty 20), boundary case (qty 1), sum invariant,
//        and structural reflection check that Execute(List, Instrument) overload exists.
// Framework: xUnit only. No NUnit. No MSTest.
// JS-021: no lock. JS-033: no async void. JS-081: ASCII-only. CYC=1 per test method.
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace PropTraderTools.Tests
{
    public class B191Tests
    {
        // T2-1: Build2TargetList(5) -- ceiling-split: t1=(5+1)/2=3, t2=5-3=2.
        [Fact]
        public void Build2TargetList_Qty5_ReturnsT1_3_T2_2()
        {
            var result = TradeCopierPanel.Build2TargetList(5);
            Assert.Equal(2, result.Count);
            Assert.Equal(3, result[0].Qty);
            Assert.Equal(2, result[1].Qty);
        }

        // T2-2: Build2TargetList(20) -- even split: t1=(20+1)/2=10, t2=20-10=10.
        [Fact]
        public void Build2TargetList_Qty20_ReturnsT1_10_T2_10()
        {
            var result = TradeCopierPanel.Build2TargetList(20);
            Assert.Equal(2, result.Count);
            Assert.Equal(10, result[0].Qty);
            Assert.Equal(10, result[1].Qty);
        }

        // T2-3: Sum invariant -- no qty lost across ceiling-split. t1+t2 must equal totalQty.
        [Fact]
        public void Build2TargetList_Qty5_SumEqualsInput()
        {
            var result = TradeCopierPanel.Build2TargetList(5);
            Assert.Equal(5, result.Sum(t => t.Qty));
        }

        // T2-4: Structural reflection check -- Execute(List, Instrument) internal overload exists.
        // Fallback form: name + 2-param count. No NT8 type references.
        [Fact]
        public void Execute_ListInstrument_Overload_ExistsAsInternal()
        {
            var methods = typeof(PttGlobalQuickExit)
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.Name == "Execute" && m.GetParameters().Length == 2)
                .ToList();
            Assert.NotEmpty(methods);
        }

        // T2-5: Build2TargetList(1) -- boundary case: t1=(1+1)/2=1, t2=1-1=0.
        [Fact]
        public void Build2TargetList_Qty1_ReturnsT1_1_T2_0()
        {
            var result = TradeCopierPanel.Build2TargetList(1);
            Assert.Equal(2, result.Count);
            Assert.Equal(1, result[0].Qty);
            Assert.Equal(0, result[1].Qty);
        }
    }
}