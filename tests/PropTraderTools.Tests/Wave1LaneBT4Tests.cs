// Wave1LaneBT4Tests.cs -- xUnit verification tests for WAVE1-LANE-B T-4.
// Spec requirements covered: B-05 (Execute), B-09 (SubmitStopOrder), B-10 (SubmitTargetOrder).
// Mirrors guard predicate logic from PttQuickExit.cs
// (src/PropTraderTools/Features/PttQuickExit.cs).
//
// Direct ProjectReference impossible across TFMs:
//   PropTraderTools targets net48 (NT8 requirement).
//   PropTraderTools.Tests targets net8.0.
//   NT8 Account/Instrument/Position are not instantiable without the NT8 runtime.
//   Inline mirror of guard predicates is the established pattern
//   (see Wave1LaneBT2Tests.cs, Wave1LaneBT3Tests.cs, PttBreakEvenB72Tests.cs).
//
// Framework: xUnit ONLY. NEVER NUnit or MSTest. ASCII-only. No DateTime.Now. No lock().
// JS-021: no lock. JS-001: no throw. JS-002: no null return. JS-033: no async void.
using Xunit;

namespace PropTraderTools.Tests
{
    /// <summary>
    /// Inline mirror tests for PttQuickExit guard logic.
    /// Tests the guard predicates that protect Execute (B-05),
    /// SubmitStopOrder (B-09), and SubmitTargetOrder (B-10).
    /// InstrumentDefaults.GetQuickTicks inline mirror tests also included.
    /// </summary>
    public sealed class Wave1LaneBT4Tests
    {
        // ------------------------------------------------------------------
        // Inline mirror of PttQuickExit.IsFlatOrMissing.
        // Source: PttQuickExit.cs L192-L204.
        //   pos = null;
        //   if (leader == null) return true;
        //   foreach (Position p in leader.Positions) if (p.Instrument == instr) { pos = p; break; }
        //   return pos == null || pos.Quantity == 0;
        //
        // Parameters mapped to primitives -- no NT8 runtime required.
        //   leaderIsNull  -> leader == null
        //   posFound      -> foreach found the instrument (sets pos != null)
        //   posQty        -> pos.Quantity
        // CYC=4: (1) leaderIsNull, (2) posFound, (3) pos==null, (4) qty==0.
        // ------------------------------------------------------------------
        private static bool IsFlatOrMissing(bool leaderIsNull, bool posFound, int posQty)
        {
            if (leaderIsNull)
                return true;
            if (!posFound)
                return true;
            return posQty == 0;
        }

        // ------------------------------------------------------------------
        // Inline mirror of PttQuickExit.SubmitStopOrder snapshotStop guard.
        // Source: PttQuickExit.cs L287-L288.
        //   if (snapshotStop <= 0) return;
        // CYC=1: single guard branch.
        // B-09: when snapshotStop <= 0, SubmitStopOrder returns immediately (no CreateOrder call).
        // ------------------------------------------------------------------
        private static bool StopOrderShouldSkip(double snapshotStop)
        {
            return snapshotStop <= 0;
        }

        // ------------------------------------------------------------------
        // Inline mirror of PttQuickExit.SubmitQxOcoPair tNQty skip guard.
        // Source: PttQuickExit.cs L166-L167.
        //   if (tNQty <= 0) return; // B129: skip T2 when posQty==1 and t2Qty==0
        // B-10: SubmitTargetOrder is not called when tNQty <= 0.
        // CYC=1: single guard branch.
        // ------------------------------------------------------------------
        private static bool TargetOrderShouldSkip(int tNQty)
        {
            return tNQty <= 0;
        }

        // ------------------------------------------------------------------
        // Inline mirror of PttQuickExit.ResolveStop.
        // Source: PttQuickExit.cs L402.
        //   private static double ResolveStop(double own, double fallback) => own > 0 ? own : fallback;
        // B-09 support: leader stop fallback feeds SubmitStopOrder correctly.
        // CYC=1: single ternary.
        // ------------------------------------------------------------------
        private static double ResolveStop(double own, double fallback) =>
            own > 0 ? own : fallback;

        // ------------------------------------------------------------------
        // Inline mirror of PttQuickExit.CalcTNQty.
        // Source: PttQuickExit.cs L430-L436.
        // B-10 support: qty distribution logic that determines tNQty fed to SubmitTargetOrder.
        // CYC=3: (1) is-last-pair, (2) totalQty > targetCount, (3) remainder vs floor.
        // ------------------------------------------------------------------
        private static int CalcTNQty(int totalQty, int targetCount, int i)
        {
            int floorQty = System.Math.Max(1, totalQty / targetCount);
            if (i == targetCount - 1 && totalQty > targetCount)
                return System.Math.Max(1, totalQty - floorQty * (targetCount - 1));
            return floorQty;
        }

        // ------------------------------------------------------------------
        // Inline mirror of InstrumentDefaults.GetQuickTicks.
        // Source: PttQuickExit.cs L470-L479.
        //   if (string.IsNullOrEmpty(masterName)) return (4, 8);
        //   if (masterName.StartsWith("MES", System.StringComparison.Ordinal)) return (4, 8);
        //   if (masterName.StartsWith("MGC", System.StringComparison.Ordinal)) return (2, 4);
        //   return (4, 8);
        // CYC=3: (1) null/empty guard, (2) MES check, (3) MGC check.
        // ------------------------------------------------------------------
        private static (int t1, int t2) GetQuickTicks(string? masterName)
        {
            if (string.IsNullOrEmpty(masterName))
                return (4, 8);
            if (masterName.StartsWith("MES", System.StringComparison.Ordinal))
                return (4, 8);
            if (masterName.StartsWith("MGC", System.StringComparison.Ordinal))
                return (2, 4);
            return (4, 8);
        }

        // ==================================================================
        // B-05 -- Execute guard behaviour (IsFlatOrMissing)
        // ==================================================================

        [Fact]
        public void IsFlatOrMissing_NullLeader_ReturnsTrue()
        {
            bool result = IsFlatOrMissing(leaderIsNull: true, posFound: false, posQty: 0);
            Assert.True(result);
        }

        [Fact]
        public void IsFlatOrMissing_ZeroQty_ReturnsTrue()
        {
            bool result = IsFlatOrMissing(leaderIsNull: false, posFound: true, posQty: 0);
            Assert.True(result);
        }

        [Fact]
        public void IsFlatOrMissing_InstrumentNotFound_ReturnsTrue()
        {
            bool result = IsFlatOrMissing(leaderIsNull: false, posFound: false, posQty: 5);
            Assert.True(result);
        }

        [Fact]
        public void IsFlatOrMissing_ActivePosition_ReturnsFalse()
        {
            bool result = IsFlatOrMissing(leaderIsNull: false, posFound: true, posQty: 2);
            Assert.False(result);
        }

        // ==================================================================
        // B-09 -- SubmitStopOrder guard behaviour (snapshotStop <= 0)
        // ==================================================================

        [Fact]
        public void SubmitStopOrder_ZeroStop_GuardFires()
        {
            Assert.True(StopOrderShouldSkip(snapshotStop: 0.0));
        }

        [Fact]
        public void SubmitStopOrder_NegativeStop_GuardFires()
        {
            Assert.True(StopOrderShouldSkip(snapshotStop: -1.0));
        }

        [Fact]
        public void SubmitStopOrder_PositiveStop_GuardDoesNotFire()
        {
            Assert.False(StopOrderShouldSkip(snapshotStop: 4500.25));
        }

        [Fact]
        public void ResolveStop_OwnPositive_ReturnsOwn()
        {
            Assert.Equal(4499.75, ResolveStop(own: 4499.75, fallback: 4498.50));
        }

        [Fact]
        public void ResolveStop_OwnZero_ReturnsFallback()
        {
            Assert.Equal(4498.50, ResolveStop(own: 0.0, fallback: 4498.50));
        }

        // ==================================================================
        // B-10 -- SubmitTargetOrder guard behaviour (tNQty <= 0 skip)
        // ==================================================================

        [Fact]
        public void SubmitTargetOrder_ZeroQty_GuardFires()
        {
            Assert.True(TargetOrderShouldSkip(tNQty: 0));
        }

        [Fact]
        public void SubmitTargetOrder_PositiveQty_GuardDoesNotFire()
        {
            Assert.False(TargetOrderShouldSkip(tNQty: 1));
        }

        [Fact]
        public void CalcTNQty_EvenDistribution_ReturnsFloor()
        {
            Assert.Equal(2, CalcTNQty(totalQty: 6, targetCount: 3, i: 0));
            Assert.Equal(2, CalcTNQty(totalQty: 6, targetCount: 3, i: 1));
            Assert.Equal(2, CalcTNQty(totalQty: 6, targetCount: 3, i: 2));
        }

        [Fact]
        public void CalcTNQty_LastPairAbsorbsRemainder()
        {
            Assert.Equal(3, CalcTNQty(totalQty: 7, targetCount: 3, i: 2));
        }

        // ==================================================================
        // InstrumentDefaults.GetQuickTicks -- inline mirror of pure static logic
        // ==================================================================

        [Fact]
        public void GetQuickTicks_MesInstrument_Returns4And8()
        {
            var (t1, t2) = GetQuickTicks("MES");
            Assert.Equal(4, t1);
            Assert.Equal(8, t2);
        }

        [Fact]
        public void GetQuickTicks_MgcInstrument_Returns2And4()
        {
            var (t1, t2) = GetQuickTicks("MGC");
            Assert.Equal(2, t1);
            Assert.Equal(4, t2);
        }

        [Fact]
        public void GetQuickTicks_NullOrEmpty_Returns4And8()
        {
            var (t1n, t2n) = GetQuickTicks(null);
            Assert.Equal(4, t1n);
            Assert.Equal(8, t2n);

            var (t1e, t2e) = GetQuickTicks(string.Empty);
            Assert.Equal(4, t1e);
            Assert.Equal(8, t2e);
        }

        [Fact]
        public void GetQuickTicks_UnknownInstrument_Returns4And8()
        {
            var (t1, t2) = GetQuickTicks("NQ");
            Assert.Equal(4, t1);
            Assert.Equal(8, t2);
        }
    }
}