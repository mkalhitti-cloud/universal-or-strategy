// BWAVE-DW LaneB tests -- verifies B-4 BuildFollowerMultipliers refactor.
// Uses reflection to confirm: method present, 1 param (Account[]), value-tuple return, instance method.
// xUnit only -- JS-051. No lock() -- JS-021. No async void -- JS-033.
using System;
using System.Reflection;
using Xunit;

namespace PropTraderTools.Tests
{
    public class BwaveDwLaneBTests
    {
        [Fact]
        public void BuildFollowerMultipliers_SignatureUnchanged_AfterContainsRefactor()
        {
            var m = typeof(TradeCopierPanel).GetMethod(
                "BuildFollowerMultipliers",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.NotNull(m);
            var parms = m.GetParameters();
            Assert.Equal(1, parms.Length);
            Assert.Equal(typeof(NinjaTrader.Cbi.Account[]), parms[0].ParameterType);
            Assert.True(m.ReturnType.IsValueType);
            Assert.False(m.IsStatic);
        }
    }
}
