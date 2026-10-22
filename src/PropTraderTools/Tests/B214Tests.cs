using System;
using System.Reflection;
using Xunit;

namespace PropTraderTools.Tests
{
    // B214 Lane B -- T1 tests for FormatBuffer and FormatGlobalBeBuffer.
    public class B214T1Tests
    {
        private static MethodInfo GetFormatBuffer() =>
            typeof(TradeCopierPanel)
                .GetMethod("FormatBuffer", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("FormatBuffer method not found");

        private static MethodInfo GetFormatGlobalBeBuffer() =>
            typeof(TradeCopierPanel)
                .GetMethod("FormatGlobalBeBuffer", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("FormatGlobalBeBuffer method not found");

        // UI-2: zero ticks suppresses the "+0" artifact -- returns label name only.
        [Fact]
        public void T_B214_T1_01_FormatBuffer_ZeroTicks_ReturnsNameOnly()
        {
            var result = (string)GetFormatBuffer().Invoke(null, new object[] { "Trim", 0 });
            Assert.Equal("Trim", result);
        }

        // UI-3: positive ticks appends "t" suffix.
        [Fact]
        public void T_B214_T1_02_FormatBuffer_PositiveTicks_AppendsTSuffix()
        {
            var result = (string)GetFormatBuffer().Invoke(null, new object[] { "Trim", 5 });
            Assert.Equal("Trim +5t", result);
        }

        // UI-3: negative ticks appends "t" suffix (int.ToString auto-includes "-").
        [Fact]
        public void T_B214_T1_03_FormatBuffer_NegativeTicks_AppendsTSuffix()
        {
            var result = (string)GetFormatBuffer().Invoke(null, new object[] { "Ask", -3 });
            Assert.Equal("Ask -3t", result);
        }

        // UI-3: FormatGlobalBeBuffer positive ticks appends "t" suffix.
        [Fact]
        public void T_B214_T1_04_FormatGlobalBeBuffer_PositiveTicks_AppendsTSuffix()
        {
            var result = (string)GetFormatGlobalBeBuffer().Invoke(null, new object[] { "BE All", 3 });
            Assert.Equal("BE All +3t", result);
        }

        // UI-3: FormatGlobalBeBuffer negative ticks appends "t" suffix.
        [Fact]
        public void T_B214_T1_05_FormatGlobalBeBuffer_NegativeTicks_AppendsTSuffix()
        {
            var result = (string)GetFormatGlobalBeBuffer().Invoke(null, new object[] { "BE All", -3 });
            Assert.Equal("BE All -3t", result);
        }

        // UI-2: FormatGlobalBeBuffer zero ticks returns name only (no "0t" artifact).
        [Fact]
        public void T_B214_T1_06_FormatGlobalBeBuffer_ZeroTicks_ReturnsNameOnly()
        {
            var result = (string)GetFormatGlobalBeBuffer().Invoke(null, new object[] { "BE All", 0 });
            Assert.Equal("BE All", result);
        }
    }
}