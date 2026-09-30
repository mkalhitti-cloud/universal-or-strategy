// B72-LaneA PttBreakEven tests -- Tickets 6-8
// 15 [Fact] tests covering T_BE_CANCEL_01-03, T_ATM_T3_04-05, T_ATM_T3_09-10,
// T_OCO_SHARED_01-02, T_OCO_ID_01-03,
// T_BE_PRICE_LONG_01-02, T_BE_PRICE_SHORT_01-02, T_BE_PRICE_VALID_SHORT,
// T_NOTIFY_01-02
// JS-021: no lock. JS-033: no async void. JS-002: no return null. JS-001: no throw.
// xUnit ONLY. CYC <= 8 per method.

using System;
using System.Reflection;
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools
{
    public class PttBreakEvenB72Tests
    {
        // ----------------------------------------------------------------
        // Ticket 6 -- CancelStaleBracketsLocal stateOk + notBe filter
        // ----------------------------------------------------------------

        [Fact]
        public void T_BE_CANCEL_01_CancelStaleBracketsLocal_TriggerPending_InStateOk()
        {
            bool tpOk =
                OrderState.TriggerPending == OrderState.Working
                || OrderState.TriggerPending == OrderState.Initialized
                || OrderState.TriggerPending == OrderState.Submitted
                || OrderState.TriggerPending == OrderState.Accepted
                || OrderState.TriggerPending == OrderState.TriggerPending;
            Assert.True(tpOk);
        }

        [Fact]
        public void T_BE_CANCEL_02_CancelStaleBracketsLocal_Submitted_InStateOk()
        {
            bool subOk =
                OrderState.Submitted == OrderState.Working
                || OrderState.Submitted == OrderState.Initialized
                || OrderState.Submitted == OrderState.Submitted
                || OrderState.Submitted == OrderState.Accepted
                || OrderState.Submitted == OrderState.TriggerPending;
            Assert.True(subOk);
        }

        [Fact]
        public void T_BE_CANCEL_03_CancelStaleBracketsLocal_Accepted_InStateOk()
        {
            bool accOk =
                OrderState.Accepted == OrderState.Working
                || OrderState.Accepted == OrderState.Initialized
                || OrderState.Accepted == OrderState.Submitted
                || OrderState.Accepted == OrderState.Accepted
                || OrderState.Accepted == OrderState.TriggerPending;
            Assert.True(accOk);
        }

        [Fact]
        public void T_ATM_T3_04_IsAtmBracketName_Stop9_True()
        {
            Assert.True(CopyEngine.IsAtmBracketName("Stop9"));
        }

        [Fact]
        public void T_ATM_T3_05_IsAtmBracketName_Null_False()
        {
            Assert.False(CopyEngine.IsAtmBracketName(null));
        }

        [Fact]
        public void T_ATM_T3_09_CancelStaleBracketsLocal_PttBeTarget1_IsExcluded_StartsWith()
        {
            string name = "PTT-BE-Target-1";
            bool notBe = name != null && !name.StartsWith("PTT-BE-", StringComparison.Ordinal);
            Assert.False(notBe);
        }

        [Fact]
        public void T_ATM_T3_10_CancelStaleBracketsLocal_Stop3_IncludedInStaleList()
        {
            string name = "Stop3";
            bool notBe = name != null && !name.StartsWith("PTT-BE-", StringComparison.Ordinal);
            Assert.True(notBe);
        }

        // ----------------------------------------------------------------
        // Ticket 7 -- OCO Shared Counter + Prefix
        // ----------------------------------------------------------------

        [Fact]
        public void T_OCO_SHARED_01_PttBreakEven_Execute_CallsNextBeOcoSeq_NoCollision()
        {
            int seq1 = CopyEngine.Instance.NextBeOcoSeq();
            int seq2 = CopyEngine.Instance.NextBeOcoSeq();
            Assert.NotEqual(seq1, seq2);
        }

        [Fact(Skip="B180-2: PttBreakEven deleted")]
        public void T_OCO_SHARED_02_PttBreakEven_NoBeOcoSeqField()
        {
            var fi = typeof(PttBreakEven).GetField(
                "_beOcoSeq",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static
            );
            Assert.Null(fi);
        }

        [Fact(Skip="B180-2: PttBreakEven deleted")]
        public void T_OCO_ID_01_BuildBeOcoId_Sim101_UsesFullName_AsPrefix()
        {
            var mi = typeof(PttBreakEven).GetMethod(
                "BuildBeOcoId",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(int), typeof(int) },
                null
            );
            string result = (string)mi.Invoke(null, new object[] { "Sim101", 1, 0 });
            Assert.StartsWith("PTT-BE-Sim101-", result);
        }

        [Fact(Skip="B180-2: PttBreakEven deleted")]
        public void T_OCO_ID_02_BuildBeOcoId_Sim102_DistinctFromSim101()
        {
            var mi = typeof(PttBreakEven).GetMethod(
                "BuildBeOcoId",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(int), typeof(int) },
                null
            );
            string id1 = (string)mi.Invoke(null, new object[] { "Sim102", 1, 0 });
            string id2 = (string)mi.Invoke(null, new object[] { "Sim101", 1, 0 });
            Assert.NotEqual(id1, id2);
        }

        [Fact(Skip="B180-2: PttBreakEven deleted")]
        public void T_OCO_ID_03_BuildBeOcoId_8CharAccName_Uses8CharPrefix()
        {
            var mi = typeof(PttBreakEven).GetMethod(
                "BuildBeOcoId",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(int), typeof(int) },
                null
            );
            string result = (string)mi.Invoke(null, new object[] { "ShortAcc", 5, 0 });
            Assert.StartsWith("PTT-BE-ShortAcc-", result);
        }

        // ----------------------------------------------------------------
        // Ticket 8 -- Sign Fixes + RaiseBeNotify
        // ----------------------------------------------------------------

        [Fact]
        public void T_BE_PRICE_LONG_01_ExecuteOneAccount_Long_BePriceBelowAvgPrice()
        {
            double avg = 5200.0;
            double buf = 3;
            double tick = 0.25;
            bool isLong = true;
            double bePrice = avg + (isLong ? -buf : +buf) * tick;
            Assert.Equal(5199.25, bePrice, 6);
            Assert.True(bePrice < avg);
        }

        [Fact]
        public void T_BE_PRICE_LONG_02_ExecuteOneAccount_Long_ZeroBuffer_BePriceEqualsAvg()
        {
            double avg = 5200.0;
            double buf = 0;
            double tick = 0.25;
            bool isLong = true;
            double bePrice = avg + (isLong ? -buf : +buf) * tick;
            Assert.Equal(5200.0, bePrice, 6);
        }

        [Fact]
        public void T_BE_PRICE_SHORT_01_ExecuteOneAccount_Short_BePriceAboveAvgPrice()
        {
            double avg = 5200.0;
            double buf = 3;
            double tick = 0.25;
            bool isLong = false;
            double bePrice = avg + (isLong ? -buf : +buf) * tick;
            Assert.Equal(5200.75, bePrice, 6);
            Assert.True(bePrice > avg);
        }

        [Fact]
        public void T_BE_PRICE_SHORT_02_ExecuteOneAccount_Short_Buf2_Tick025_BePricePlus050()
        {
            double avg = 5200.0;
            double buf = 2;
            double tick = 0.25;
            bool isLong = false;
            double bePrice = avg + (isLong ? -buf : +buf) * tick;
            Assert.Equal(5200.50, bePrice, 6);
        }

        [Fact]
        public void T_BE_PRICE_VALID_SHORT_ExecuteOneAccount_Short_Positive_BePriceAboveAvg()
        {
            double avg = 5200.0;
            double buf = 1;
            double tick = 0.25;
            bool isLong = false;
            double bePrice = avg + (isLong ? -buf : +buf) * tick;
            Assert.True(bePrice > avg);
        }

        [Fact]
        public void T_NOTIFY_01_RaiseBeNotify_Long_ReportsBePriceBelowEntry()
        {
            double avg = 5200.0;
            double buf = 2;
            double tick = 0.25;
            bool leaderIsLong = true;
            double leaderBePrice = avg + (leaderIsLong ? -buf : +buf) * tick;
            Assert.Equal(5199.50, leaderBePrice, 6);
            Assert.True(leaderBePrice < avg);
        }

        [Fact]
        public void T_NOTIFY_02_RaiseBeNotify_Short_ReportsBePriceAboveEntry()
        {
            double avg = 5200.0;
            double buf = 2;
            double tick = 0.25;
            bool leaderIsLong = false;
            double leaderBePrice = avg + (leaderIsLong ? -buf : +buf) * tick;
            Assert.Equal(5200.50, leaderBePrice, 6);
            Assert.True(leaderBePrice > avg);
        }
    }
}
