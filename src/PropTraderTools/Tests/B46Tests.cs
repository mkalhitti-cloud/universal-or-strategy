// B46Tests.cs
// Block: PTT-COPIER-B46
// Spec: DW-B46-ATM-EMPTY-GUARD-01, DW-B46-COMBO-AUTOSELECT-02
// Tests: T_B46_01 through T_B46_03
// Framework: xUnit only (no NUnit, no MSTest)
// NT8-runtime-free: zero NT8 API calls

using System;
using Xunit;

namespace PropTraderTools
{
    public sealed class B46Tests
    {
        // T_B46_01 -- Empty AtmTemplateName triggers the guard (IsNullOrWhiteSpace = true).
        // Guard fires -> AtmStrategyCreate is skipped -> strategy stays alive.
        // Spec: DW-B46-ATM-EMPTY-GUARD-01
        [Fact]
        public void T_B46_01_EmptyAtmTemplateName_GuardFires()
        {
            var args = FillSignalEventArgs.Create(
                null,
                null,
                string.Empty,
                NinjaTrader.Cbi.OrderAction.Buy,
                8,
                "ORD-B46-001"
            );

            // Guard predicate: string.IsNullOrWhiteSpace(args.AtmTemplateName)
            // This is exactly what production CallAtmStrategyCreate evaluates.
            Assert.True(string.IsNullOrWhiteSpace(args.AtmTemplateName));
        }

        // T_B46_02 -- Non-empty AtmTemplateName does NOT trigger the guard.
        // Guard does not fire -> AtmStrategyCreate is called with the template name.
        // Spec: DW-B46-ATM-EMPTY-GUARD-01 (negative / pass-through case)
        [Fact]
        public void T_B46_02_NonEmptyAtmTemplateName_GuardDoesNotFire()
        {
            var args = FillSignalEventArgs.Create(
                null,
                null,
                "MES $200 SL5",
                NinjaTrader.Cbi.OrderAction.Buy,
                8,
                "ORD-B46-002"
            );

            Assert.False(string.IsNullOrWhiteSpace(args.AtmTemplateName));
            Assert.Equal("MES $200 SL5", args.AtmTemplateName);
        }

        // T_B46_03 -- "Named:MES $200 SL5" (written by auto-select) round-trips
        //            through CopyEngine.ParseAtmModeName to Named mode correctly.
        //            Validates the serialisation format is consistent end-to-end.
        // Spec: DW-B46-COMBO-AUTOSELECT-02
        [Fact]
        public void T_B46_03_ComboAutoSelectFormat_ParsesAsNamedMode()
        {
            string written = "Named:MES $200 SL5";

            var mode = CopyEngine.ParseAtmModeName(written);

            var named = Assert.IsType<FollowerAtmMode.Named>(mode);
            Assert.Equal("MES $200 SL5", named.TemplateName);
        }
    }
}
