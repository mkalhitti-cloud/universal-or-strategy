// B209: xUnit tests for FIX-G-1 (IsBeClearStateTrigger) and FIX-G-2 (IsBeSwapGtcSweepGuard) seams.
// [assembly: InternalsVisibleTo] granted at CopyEngine.cs L42 ("PropTraderTools.Tests").
// JS-021: no lock. [Fact] only. ASCII-only. xUnit only -- no NUnit/MSTest.
using Xunit;
using NinjaTrader.Cbi;
using PropTraderTools;

namespace PropTraderTools.Tests
{
    public class B209Tests
    {
        // ---- FIX-G-1 seam tests: IsBeClearStateTrigger ----

        [Fact]
        public void IsBeClearStateTrigger_Working_ReturnsTrue()
            => Assert.True(CopyEngine.IsBeClearStateTrigger("PTT-BE-Stop-1", OrderState.Working));

        [Fact]
        public void IsBeClearStateTrigger_Cancelled_ReturnsTrue()
            => Assert.True(CopyEngine.IsBeClearStateTrigger("PTT-BE-Stop-1", OrderState.Cancelled));

        [Fact]
        public void IsBeClearStateTrigger_Rejected_ReturnsTrue()
            => Assert.True(CopyEngine.IsBeClearStateTrigger("PTT-BE-Stop-1", OrderState.Rejected));

        // B212-DW04: Accepted no longer clears the flag -- it fires before drain cancel-acks arrive,
        // causing SubmitDrainedEntry to dispatch a spurious PTT-ATM-SEND (qty=9) re-entry.
        // Fallback: PttBeSwapAll.Execute finally clears after 3s if Working never fires.
        [Fact]
        public void IsBeClearStateTrigger_Accepted_ReturnsFalse_B212DW04()
            => Assert.False(CopyEngine.IsBeClearStateTrigger("PTT-BE-Stop-1", OrderState.Accepted));

        [Fact]
        public void IsBeClearStateTrigger_Submitted_ReturnsFalse()
            => Assert.False(CopyEngine.IsBeClearStateTrigger("PTT-BE-Stop-1", OrderState.Submitted));

        [Fact]
        public void IsBeClearStateTrigger_WrongName_ReturnsFalse()
            => Assert.False(CopyEngine.IsBeClearStateTrigger("PTT-BE-Target-1", OrderState.Working));

        // ---- FIX-G-2 seam tests: IsBeSwapGtcSweepGuard ----

        [Fact]
        public void IsBeSwapGtcSweepGuard_PttBeName_SwapActive_ReturnsTrue()
            => Assert.True(CopyEngine.IsBeSwapGtcSweepGuard("PTT-BE-Stop-1", true));

        [Fact]
        public void IsBeSwapGtcSweepGuard_PttBeName_SwapInactive_ReturnsFalse()
            => Assert.False(CopyEngine.IsBeSwapGtcSweepGuard("PTT-BE-Stop-1", false));

        [Fact]
        public void IsBeSwapGtcSweepGuard_NonPttBeName_SwapActive_ReturnsFalse()
            => Assert.False(CopyEngine.IsBeSwapGtcSweepGuard("Stop1", true));

        [Fact]
        public void IsBeSwapGtcSweepGuard_NullName_ReturnsFalse()
            => Assert.False(CopyEngine.IsBeSwapGtcSweepGuard(null, true));
    }
}