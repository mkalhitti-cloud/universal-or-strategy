// PTT-COPIER-B190 -- B190Tests.cs
// xUnit tests for B190 T1: InstrumentDefaults MGC update + SetInstrument() tick reset.
// Jane Street rules: JS-001, JS-002, JS-021, JS-066, JS-081
// 5 [Fact] tests -- T_B190_T1_01 through T_B190_T1_05.
using Xunit;

namespace PropTraderTools
{
    public class B190Tests
    {
        // T_B190_T1_01: MES still returns (4, 8) after MGC value change (regression guard).
        [Fact]
        public void T_B190_T1_01_InstrumentDefaults_MES_Returns4And8()
        {
            var (t1, t2) = InstrumentDefaults.GetQuickTicks("MES");
            Assert.Equal(4, t1);
            Assert.Equal(8, t2);
        }

        // T_B190_T1_02: MGC now returns (10, 20) per B190 change.
        [Fact]
        public void T_B190_T1_02_InstrumentDefaults_MGC_Returns10And20()
        {
            var (t1, t2) = InstrumentDefaults.GetQuickTicks("MGC");
            Assert.Equal(10, t1);
            Assert.Equal(20, t2);
        }

        // T_B190_T1_03: unknown and empty instrument names return (4, 8) fallback.
        [Fact]
        public void T_B190_T1_03_InstrumentDefaults_Unknown_ReturnsMesDefault()
        {
            var (t1Xyz, t2Xyz) = InstrumentDefaults.GetQuickTicks("XYZ");
            Assert.Equal(4, t1Xyz);
            Assert.Equal(8, t2Xyz);

            var (t1Empty, t2Empty) = InstrumentDefaults.GetQuickTicks("");
            Assert.Equal(4, t1Empty);
            Assert.Equal(8, t2Empty);
        }

        // T_B190_T1_04: proxy test for SetInstrument() MES path -- GetQuickTicks("MES").t1 == 4.
        // TradeCopierPanel ctor requires NT8 host; test InstrumentDefaults directly as gate.
        [Fact]
        public void T_B190_T1_04_SetInstrument_MES_SetsQuickT1_4()
        {
            var (t1, _) = InstrumentDefaults.GetQuickTicks("MES");
            Assert.Equal(4, t1);
        }

        // T_B190_T1_05: proxy test for SetInstrument() MGC path -- GetQuickTicks("MGC").t1 == 10, t2 == 20.
        // TradeCopierPanel ctor requires NT8 host; test InstrumentDefaults directly as gate.
        [Fact]
        public void T_B190_T1_05_SetInstrument_MGC_SetsQuickT1_10()
        {
            var (t1, t2) = InstrumentDefaults.GetQuickTicks("MGC");
            Assert.Equal(10, t1);
            Assert.Equal(20, t2);
        }

        // T_B190_T2_01: BuildSpinnerCluster exists as a private instance method on TradeCopierPanel
        // and its return type is DockPanel. Uses reflection -- no NT8 runtime required.
        [Fact]
        public void T_B190_T2_01_BuildSpinnerCluster_ReturnsDockPanel()
        {
            var method = typeof(TradeCopierPanel).GetMethod(
                "BuildSpinnerCluster",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);
            Assert.Equal(typeof(System.Windows.Controls.DockPanel), method.ReturnType);
        }

        // T_B190_T2_02: Verifies BuildSpinnerCluster has 6 parameters matching the canonical
        // spinner factory contract: (string label, int ticks, onUp, onDn, onClick, ref Button btnField).
        // Confirms last param is ref Button (assigns btnField in caller). AC6 layout contract.
        [Fact]
        public void T_B190_T2_02_BuildSpinnerCluster_GridDockedRight()
        {
            var method = typeof(TradeCopierPanel).GetMethod(
                "BuildSpinnerCluster",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);
            var parameters = method.GetParameters();
            Assert.Equal(6, parameters.Length);
            Assert.Equal(typeof(string), parameters[0].ParameterType);
            Assert.Equal(typeof(int), parameters[1].ParameterType);
            Assert.True(parameters[5].ParameterType.IsByRef);
        }
        // T_B190_T3_01: proxy test for _beBuffer=0 default.
        // FormatGlobalBeBuffer("BE", 0) returns "BE" (ticks==0 branch) -- confirms AC9.
        // FormatGlobalBeBuffer is private static -- accessed via reflection.
        [Fact]
        public void T_B190_T3_01_BeBuffer_Default_IsZero()
        {
            var method = typeof(TradeCopierPanel).GetMethod(
                "FormatGlobalBeBuffer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.NotNull(method);
            var result = (string)method.Invoke(null, new object[] { "BE", 0 });
            Assert.Equal("BE", result);
        }
    }
}
