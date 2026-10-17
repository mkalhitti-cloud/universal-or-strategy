using System;
using System.Reflection;
using Xunit;
using PropTraderTools;

// B193 formalization tests.
// Verifies per-panel _quickAllT1 field, InstrumentDefaults.GetQuickTicks,
// FormatBuffer / FormatQuickAllBuffer label strings, and ResolveQuickTicks proxy.
// No NT8 host required -- pure reflection + formula tests.
// JS-021: no lock. JS-001: no throw. JS-002: no return null. JS-033: no async void.
// ASCII-only strings. CYC<=1 per test method.
public class B193Tests
{
    // Reflection helper: invokes private static FormatBuffer(string name, int ticks)
    // Returns: name + " +" + ticks  (e.g. "3x +10")
    // Mirrors B189T4SpinnerTests.InvokeFormatBuffer pattern (CopyEngineTests.cs:10566)
    // with internal cast to string instead of returning object.
    private static string InvokeFormatBuffer(string name, int ticks)
    {
        var m = typeof(TradeCopierPanel).GetMethod(
            "FormatBuffer",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        return (string)m.Invoke(null, new object[] { name, ticks });
    }

    // Reflection helper: invokes private static FormatQuickAllBuffer(string name, int ticks)
    // Returns: name + " +" + ticks + "t"  (e.g. "3x All +10t")
    // QUICK-LABEL-UNIT-01: "t" unit suffix is appended by FormatQuickAllBuffer only.
    private static string InvokeFormatQuickAllBuffer(string name, int ticks)
    {
        var m = typeof(TradeCopierPanel).GetMethod(
            "FormatQuickAllBuffer",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        return (string)m.Invoke(null, new object[] { name, ticks });
    }

    [Fact]
    public void T_B193_01_GetQuickTicks_MGC_Returns10_20()
    {
        Assert.Equal((10, 20), InstrumentDefaults.GetQuickTicks("MGC"));
    }

    [Fact]
    public void T_B193_02_GetQuickTicks_GC_Returns10_20()
    {
        Assert.Equal((10, 20), InstrumentDefaults.GetQuickTicks("GC"));
    }

    [Fact]
    public void T_B193_03_GetQuickTicks_MES_Returns4_8()
    {
        Assert.Equal((4, 8), InstrumentDefaults.GetQuickTicks("MES"));
    }

    [Fact]
    public void T_B193_04_GetQuickTicks_ES_Returns4_8()
    {
        Assert.Equal((4, 8), InstrumentDefaults.GetQuickTicks("ES"));
    }

    [Fact]
    public void T_B193_05_FormatBuffer_3x_10_Returns3xPlus10()
    {
        Assert.Equal("3x +10", InvokeFormatBuffer("3x", 10));
    }

    [Fact]
    public void T_B193_06_FormatBuffer_2x_10_Returns2xPlus10()
    {
        Assert.Equal("2x +10", InvokeFormatBuffer("2x", 10));
    }

    [Fact]
    public void T_B193_07_FormatBuffer_2xAll_10_Returns2xAllPlus10()
    {
        Assert.Equal("2x All +10", InvokeFormatBuffer("2x All", 10));
    }

    [Fact]
    public void T_B193_08_FormatQuickAllBuffer_3xAll_10_Returns3xAllPlus10t()
    {
        Assert.Equal("3x All +10t", InvokeFormatQuickAllBuffer("3x All", 10));
    }

    [Fact]
    public void T_B193_09_TradeCopierPanel_HasField_quickAllT1()
    {
        var field = typeof(TradeCopierPanel).GetField(
            "_quickAllT1",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.NotNull(field);
    }

    [Fact]
    public void T_B193_10_ResolveQuickTicks_Proxy_MGC_t1_Equals10()
    {
        Assert.Equal(10, InstrumentDefaults.GetQuickTicks("MGC").t1);
    }
}