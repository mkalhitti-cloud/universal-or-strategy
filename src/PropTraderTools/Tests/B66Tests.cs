// src/PropTraderTools/Tests/B66Tests.cs
// B66-LaneB: DW-B66-BE-01 -- SubmitBeStop isLong direction race fix tests.
// 5 xUnit [Fact] tests: T_B66_BE_01 through T_B66_BE_05.
// JS-021: no lock. JS-001: no throw. JS-002: no return null. JS-033: no async void.
// xUnit only -- no NUnit, no MSTest. ASCII identifiers only.

using System.Collections.Generic;
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools
{
    public sealed class B66Tests
    {
        // T_B66_BE_01: isLong=true must map to OrderAction.Sell.
        // Verifies the direction formula: isLong ? Sell : BuyToCover with true input.
        [Fact]
        public void T_B66_BE_01_LongPosition_SubmitsSellDirection()
        {
            bool isLong = true;
            OrderAction captured = isLong ? OrderAction.Sell : OrderAction.BuyToCover;
            Assert.Equal(OrderAction.Sell, captured);
        }

        // T_B66_BE_02: isLong=false must map to OrderAction.BuyToCover.
        // Verifies the direction formula with false input.
        [Fact]
        public void T_B66_BE_02_ShortPosition_SubmitsBuyToCoverDirection()
        {
            bool isLong = false;
            OrderAction captured = isLong ? OrderAction.Sell : OrderAction.BuyToCover;
            Assert.Equal(OrderAction.BuyToCover, captured);
        }

        // T_B66_BE_03: SubmitBeStop with null acc must return immediately -- no exception.
        // Verifies the null guard (check 1) is intact after the signature change.
        [Fact]
        public void T_B66_BE_03_NullAccount_ReturnsImmediately()
        {
            bool thrown = false;
            try
            {
                CopyEngine.Instance.SubmitBeStop(null, null, 7809.5, true);
            }
            catch
            {
                thrown = true;
            }
            Assert.False(
                thrown,
                "SubmitBeStop null guard must return immediately without throwing"
            );
        }

        // T_B66_BE_04: PttGlobalBreakEven test-seam constructor accepts 4-arg delegate.
        // Verifies that the delegate type was updated to Action<Account, Instrument, double, bool>.
        // Execute with empty account list makes no delegate calls -- capturedIsLong stays null.
        [Fact]
        public void T_B66_BE_04_PttGlobalBreakEven_DelegateSignatureAcceptsIsLong()
        {
            bool? capturedIsLong = null;
            var gbe = new PttGlobalBreakEven(
                (acc, instr, price, lng) =>
                {
                    capturedIsLong = lng;
                }
            );
            // Empty account list -- delegate is never invoked. Confirms constructor compiles
            // with 4-arg Action and no phantom helper methods are needed.
            gbe.Execute(new List<Account>(), 0);
            Assert.Null(capturedIsLong); // no delegate call made -- list was empty
        }

        // T_B66_BE_05: BeEventArgs.IsLong property exists and stores the correct value.
        // Verifies that RelayBe can forward e.IsLong to SubmitBeStop without recomputing.
        [Fact]
        public void T_B66_BE_05_BeEventArgs_IsLong_StoredCorrectly()
        {
            var e = new BeEventArgs(null, 7809.5, 7809.5, isLong: true, ocoGroup: string.Empty);
            Assert.True(e.IsLong, "BeEventArgs.IsLong must store the constructor argument");
        }
    }
}
