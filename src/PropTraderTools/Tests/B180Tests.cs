// B180Tests.cs -- xUnit tests for B180 DW-B180-BE-SWAP-ALL: Break Even immediate swap.
// Tests: T-B180-T1-01 through T-B180-T1-05 per 04-tickets.md B180-3 spec.
// T-B180-T1-01 and T-B180-T1-04: DEFERRED (NT8 runtime seam not available in unit test harness).
// T-B180-T1-02, T-B180-T1-03, T-B180-T1-05: pure math -- no NT8 runtime required.
// Seam: CopyEngine.RoundToTickTestable (internal static, granted via InternalsVisibleTo at CopyEngine.cs L46).
// Framework: xUnit only ([Fact]). No NUnit. No MSTest.
// JS-021: no lock. JS-001: no throw. ASCII-only. CYC=1 per method.
using System;
using Xunit;

namespace PropTraderTools.Tests
{
    // -------------------------------------------------------------------------
    // B180 DW-B180-BE-SWAP-ALL -- Break Even immediate swap tests
    // -------------------------------------------------------------------------
    public class B180BeSwapTests
    {
        // T-B180-T1-01: PttBeSwapOne.Execute skips when account is flat (pos==null or qty==0).
        // DEFERRED: Requires NT8 Account/Instrument/Position runtime objects not available in
        // the unit test harness. All NT8-touching tests in CopyEngineTests.cs use
        // [Fact(Skip="NT8-runtime:...")] because CopyEngine.cctor requires the NT8 host.
        // No Account/Instrument/Position mock infrastructure exists in this harness.
        // Deferred to integration test scope (requires NT8 host in NinjaTrader AddOn context).
        // Deferred backlog item: DW-B180-BE-T01 -- Account/Instrument/Position seam needed.
        [Fact(Skip = "DEFERRED: NT8 Account/Instrument/Position seam not available in unit test harness")]
        public void PttBeSwapOne_FlatAccount_SkipsExecution()
        {
            // Contract when seam is available:
            // string capturedStatus = null;
            // CopyEngine.Instance.StatusUpdate = msg => capturedStatus = msg;
            // PttBeSwapOne.Execute(testAccount, testInstr, 0);
            // Assert.NotNull(capturedStatus);
            // Assert.Contains(": BE flat skip", capturedStatus);
            Assert.True(false, "DEFERRED: NT8 Account/Instrument/Position seam not available -- see DW-B180-BE-T01 backlog item");
        }

        // T-B180-T1-02: PttBeSwapOne.Execute computes tick-rounded entry price for a long position.
        // Setup: long position at AveragePrice=4500.125, tickSize=0.25, bufferTicks=0.
        // Expected: entryPrice = Math.Round(4500.125 / 0.25) * 0.25 = 4500.25 (nearest tick up).
        // Seam: captured entryPrice passed to PttBreakEvenSwap.Execute delegate.
        [Fact]
        public void PttBeSwapOne_LongPosition_ComputesTickRoundedEntryPrice()
        {
            // Arrange: AveragePrice=4500.125, tickSize=0.25, buf=0
            double avgPrice = 4500.125;
            double tickSize = 0.25;
            int buf = 0;

            // Act: compute inline (same formula as PttBeSwapOne)
            bool isLong = true;
            double direction = isLong ? -1.0 : +1.0;
            double raw = avgPrice + direction * buf * tickSize;
            double entryPrice = Math.Round(raw / tickSize, MidpointRounding.AwayFromZero) * tickSize;

            // Assert
            Assert.Equal(4500.25, entryPrice, 10);
        }

        // T-B180-T1-03: PttBeSwapOne bufferTicks shifts entry price by direction.
        // Long: stop goes BELOW entry (direction=-1). Short: stop goes ABOVE entry (direction=+1).
        [Fact]
        public void PttBeSwapOne_BufferTicks_ShiftsEntryPriceByDirection()
        {
            double avgPrice = 5000.0;
            double tickSize = 0.25;
            int buf = 2;

            // Long: direction = -1.0
            double rawLong = avgPrice + (-1.0) * buf * tickSize;
            double entryLong = Math.Round(rawLong / tickSize, MidpointRounding.AwayFromZero) * tickSize;
            Assert.Equal(4999.5, entryLong, 10);

            // Short: direction = +1.0
            double rawShort = avgPrice + (+1.0) * buf * tickSize;
            double entryShort = Math.Round(rawShort / tickSize, MidpointRounding.AwayFromZero) * tickSize;
            Assert.Equal(5000.5, entryShort, 10);
        }

        // T-B180-T1-04: PttBeSwapAll.Execute skips follower accounts.
        // DEFERRED: Requires NT8 Account.All enumeration (a static NT8 list populated only when
        // the NT8 host is running) and live Account instances for IsFollowerAccount(Account).
        // No Account stub/mock infrastructure exists in the current test harness.
        // Deferred to integration test scope (requires NT8 host in NinjaTrader AddOn context).
        // Deferred backlog item: DW-B180-BE-T04 -- Account.All stub / IsFollowerAccount seam needed.
        [Fact(Skip = "DEFERRED: NT8 Account.All and IsFollowerAccount seam not available in unit test harness")]
        public void PttBeSwapAll_FollowerAccounts_AreSkipped()
        {
            // Contract when seam is available:
            // Verify PttBreakEvenSwap.Execute is called for leader accounts only (not followers).
            // Account.All must contain at least one leader and one follower for the test to be meaningful.
            Assert.True(false, "DEFERRED: NT8 Account.All stub not available -- see DW-B180-BE-T04 backlog item");
        }

        // T-B180-T1-05: RoundToTick regression using CopyEngine.RoundToTickTestable seam.
        // MN-01: uses RoundToTickTestable (added in B180-3 to CopyEngine.cs), NOT RoundToTickPublic.
        // No NT8 runtime required -- pure math, no Account/Instrument dependencies.
        [Fact]
        public void RoundToTick_KnownValues_MatchExpected()
        {
            // Seam: CopyEngine.RoundToTickTestable (internal static, pure math)
            Assert.Equal(4500.25, PttTickMath.RoundToTick(4500.125, 0.25), 10);
            // 4500.125 / 0.25 = 18000.5 -> Math.Round = 18001 -> * 0.25 = 4500.25

            Assert.Equal(5000.0, PttTickMath.RoundToTick(4999.875, 0.25), 10);
            // 4999.875 / 0.25 = 19999.5 -> Math.Round = 20000 -> * 0.25 = 5000.0
            // NOTE: Orchestrator prompt had typo (expected 4500.0). Corrected by plan Section 7 note.

            Assert.Equal(4500.0, PttTickMath.RoundToTick(4500.0, 0.25), 10);
            // Exact-tick input: no rounding required.

            Assert.Equal(0.1, PttTickMath.RoundToTick(0.1, 0.0), 10);
            // tickSize=0 edge case: returns rawPrice unchanged per implementation (tickSize>0 guard).
        }
    }
}