// B129Tests.cs -- xUnit tests for Build2TargetList (B129 2-target fixed bracket split).
// Replaces B128 ComputeInstrSplit tests. Framework: xUnit only ([Fact]). No NUnit. No MSTest.
using System.Collections.Generic;
using Xunit;

namespace PropTraderTools.Tests
{
    public class B128Tests
    {
        [Fact]
        public void T_B129_01_Build2TargetList_Even_T1EqualT2()
        {
            var result = TradeCopierPanel.Build2TargetList(4);
            Assert.Equal(2, result.Count);
            Assert.Equal(2, result[0].Qty);
            Assert.Equal(2, result[1].Qty);
            Assert.Equal(0.0, result[0].Price);
            Assert.Equal(0.0, result[1].Price);
        }

        [Fact]
        public void T_B129_02_Build2TargetList_Odd_T1Heavier()
        {
            var result = TradeCopierPanel.Build2TargetList(5);
            Assert.Equal(2, result.Count);
            Assert.Equal(3, result[0].Qty);
            Assert.Equal(2, result[1].Qty);
            Assert.Equal(0.0, result[0].Price);
            Assert.Equal(0.0, result[1].Price);
        }

        [Fact]
        public void T_B129_03_Build2TargetList_One_T2IsZero()
        {
            var result = TradeCopierPanel.Build2TargetList(1);
            Assert.Equal(2, result.Count);
            Assert.Equal(1, result[0].Qty);
            Assert.Equal(0, result[1].Qty);
            Assert.Equal(0.0, result[0].Price);
            Assert.Equal(0.0, result[1].Price);
        }

        [Fact]
        public void T_B129_04_Build2TargetList_Large_Odd()
        {
            var result = TradeCopierPanel.Build2TargetList(7);
            Assert.Equal(2, result.Count);
            Assert.Equal(4, result[0].Qty);
            Assert.Equal(3, result[1].Qty);
            Assert.Equal(0.0, result[0].Price);
            Assert.Equal(0.0, result[1].Price);
        }

        [Fact]
        public void T_B129_05_Build2TargetList_Six_BothThree()
        {
            // Covers "Quick2t press 6-contract: Output shows T1=3 T2=3" verification criterion
            var result = TradeCopierPanel.Build2TargetList(6);
            Assert.Equal(2, result.Count);
            Assert.Equal(3, result[0].Qty);
            Assert.Equal(3, result[1].Qty);
            Assert.Equal(0.0, result[0].Price);
            Assert.Equal(0.0, result[1].Price);
        }
    }
}
