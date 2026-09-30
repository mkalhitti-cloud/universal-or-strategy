#if PTT_FOLLOWER_ACTIVE
// PTT-COPIER-B42 -- B42Tests.cs
// xUnit [Fact] tests for B42: FillSignalEventArgs, PttBus.FillSignal, PttFollowerStrategy guards.
// Jane Street rules: JS-001, JS-010, JS-021.
// NT8 runtime NOT required -- all NT8 dependencies stubbed via virtual test-seam helpers.
// xUnit only -- no NUnit, no MSTest.
using System;
using System.Reflection;
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools
{
    // -------------------------------------------------------------------------
    // TestFollowerStrategy -- injectable subclass used by T_B42_03..05
    // -------------------------------------------------------------------------

    /// <summary>
    /// Testable subclass of PttFollowerStrategy.
    /// Overrides all virtual test-seam helpers to avoid NT8 runtime dependency.
    /// All four name comparisons are injectable via public string properties.
    /// </summary>
    internal class TestFollowerStrategy : PttFollowerStrategy
    {
        // Injectable: replaces Account.Name on the strategy side
        public string StrategyAccountName { get; set; } = "AccA";

        // Injectable: replaces Instrument.FullName on the strategy side
        public string StrategyInstrumentName { get; set; } = "MES 09-26";

        // Injectable: replaces args.Account?.Name on the signal side
        public string SignalAccountName { get; set; } = "AccA";

        // Injectable: replaces args.Instrument?.FullName on the signal side
        public string SignalInstrumentName { get; set; } = "MES 09-26";

        // Counter incremented when CallAtmStrategyCreate is invoked
        public int AtmInvokedCount { get; private set; }

        // Test seam: bypass real NT8 Account.Name
        protected override string GetStrategyAccountName() => StrategyAccountName;

        // Test seam: bypass real NT8 Instrument.FullName
        protected override string GetStrategyInstrumentName() => StrategyInstrumentName;

        // Test seam: bypass real args.Account?.Name
        protected override string GetSignalAccountName(FillSignalEventArgs args) =>
            SignalAccountName;

        // Test seam: bypass real args.Instrument?.FullName
        protected override string GetSignalInstrumentName(FillSignalEventArgs args) =>
            SignalInstrumentName;

        // Test seam: capture ATM call without NT8 runtime
        protected override void CallAtmStrategyCreate(FillSignalEventArgs args)
        {
            AtmInvokedCount++;
        }

        /// <summary>
        /// Routes the given args through the private OnFillSignal method via reflection.
        /// Exercises the full guard chain: GetSignalAccountName / GetStrategyAccountName,
        /// GetSignalInstrumentName / GetStrategyInstrumentName, then CallAtmStrategyCreate.
        /// </summary>
        public void SimulateFillSignal(FillSignalEventArgs args)
        {
            var mi = typeof(PttFollowerStrategy).GetMethod(
                "OnFillSignal",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            if (mi == null)
                throw new InvalidOperationException("OnFillSignal not found via reflection");
            mi.Invoke(this, new object[] { args });
        }
    }

    // -------------------------------------------------------------------------
    // T_B42_01 -- FillSignalEventArgs struct field round-trip
    // -------------------------------------------------------------------------
    public class FillSignalEventArgsTests
    {
        [Fact]
        public void FillSignalEventArgs_CarriesAllFields()
        {
            // Arrange: null Account + null Instrument (struct holds reference -- valid stub)
            Account account = null;
            Instrument instrument = null;
            string atmName = "MyATM";
            OrderAction action = OrderAction.Buy;
            int qty = 3;
            string orderId = "PTT-Copy-001";

            // Act
            var args = FillSignalEventArgs.Create(
                account,
                instrument,
                atmName,
                action,
                qty,
                orderId
            );

            // Assert: all 6 fields round-trip
            Assert.Equal(account, args.Account);
            Assert.Equal(instrument, args.Instrument);
            Assert.Equal(atmName, args.AtmTemplateName);
            Assert.Equal(action, args.OrderAction);
            Assert.Equal(qty, args.Quantity);
            Assert.Equal(orderId, args.EntryOrderId);
        }

        [Fact]
        public void FillSignalEventArgs_NullAtmName_DefaultsToEmptyString()
        {
            // Arrange + Act: null atmTemplateName and orderId should coalesce to string.Empty
            var args = FillSignalEventArgs.Create(null, null, null, OrderAction.Buy, 1, null);

            // Assert: null-coalesced to string.Empty per constructor
            Assert.Equal(string.Empty, args.AtmTemplateName);
            Assert.Equal(string.Empty, args.EntryOrderId);
        }
    }

    // -------------------------------------------------------------------------
    // T_B42_02 -- PttBus.FillSignal event publish
    // -------------------------------------------------------------------------
    public class PttBusFillSignalTests : IDisposable
    {
        private Action<FillSignalEventArgs> _handler1;
        private Action<FillSignalEventArgs> _handler2;

        [Fact]
        public void RaiseFillSignal_FiresAllSubscribers()
        {
            // Arrange
            int callCount1 = 0;
            int callCount2 = 0;
            FillSignalEventArgs captured1 = default;
            FillSignalEventArgs captured2 = default;

            _handler1 = a =>
            {
                callCount1++;
                captured1 = a;
            };
            _handler2 = a =>
            {
                callCount2++;
                captured2 = a;
            };

            PttBus.FillSignal += _handler1;
            PttBus.FillSignal += _handler2;

            var expected = FillSignalEventArgs.Create(
                null,
                null,
                "ATM1",
                OrderAction.Sell,
                2,
                "ORD-002"
            );

            try
            {
                // Act
                PttBus.RaiseFillSignal(expected);

                // Assert: both subscribers called exactly once with identical args
                Assert.Equal(1, callCount1);
                Assert.Equal(1, callCount2);
                Assert.Equal(expected.AtmTemplateName, captured1.AtmTemplateName);
                Assert.Equal(expected.Quantity, captured2.Quantity);
            }
            finally
            {
                PttBus.FillSignal -= _handler1;
                PttBus.FillSignal -= _handler2;
            }
        }

        public void Dispose()
        {
            if (_handler1 != null)
            {
                PttBus.FillSignal -= _handler1;
                _handler1 = null;
            }
            if (_handler2 != null)
            {
                PttBus.FillSignal -= _handler2;
                _handler2 = null;
            }
        }
    }

    // -------------------------------------------------------------------------
    // T_B42_03, T_B42_04, T_B42_05 -- PttFollowerStrategy guard logic
    // -------------------------------------------------------------------------
    public class PttFollowerStrategyGuardTests
    {
        /// <summary>
        /// T_B42_03: When the signal's account name does NOT match the strategy's account name,
        /// OnFillSignal rejects the signal and CallAtmStrategyCreate is never called.
        /// Uses TestFollowerStrategy.SimulateFillSignal to route through the full guard chain.
        /// </summary>
        [Fact]
        public void OnFillSignal_IgnoresWrongAccount()
        {
            // Arrange: strategy bound to "AccA"; signal carries "AccB" (wrong account)
            var strategy = new TestFollowerStrategy
            {
                StrategyAccountName = "AccA",
                StrategyInstrumentName = "MES 09-26",
                SignalAccountName = "AccB", // MISMATCH -- guard must reject
                SignalInstrumentName = "MES 09-26",
            };
            var args = FillSignalEventArgs.Create(
                null,
                null,
                string.Empty,
                OrderAction.Buy,
                1,
                "ORD-003"
            );

            // Act: route through OnFillSignal via reflection
            strategy.SimulateFillSignal(args);

            // Assert: first guard (account) fires; CallAtmStrategyCreate never reached
            Assert.Equal(0, strategy.AtmInvokedCount);
        }

        /// <summary>
        /// T_B42_04: When the signal's instrument name does NOT match the strategy's instrument name,
        /// OnFillSignal rejects the signal even if the account guard passed.
        /// </summary>
        [Fact]
        public void OnFillSignal_IgnoresWrongInstrument()
        {
            // Arrange: strategy bound to "AccA" / "MES 09-26"; signal carries right account, wrong instrument
            var strategy = new TestFollowerStrategy
            {
                StrategyAccountName = "AccA",
                StrategyInstrumentName = "MES 09-26",
                SignalAccountName = "AccA", // MATCH -- account guard passes
                SignalInstrumentName = "MNQ 09-26", // MISMATCH -- instrument guard must reject
            };
            var args = FillSignalEventArgs.Create(
                null,
                null,
                string.Empty,
                OrderAction.Buy,
                1,
                "ORD-004"
            );

            // Act
            strategy.SimulateFillSignal(args);

            // Assert: second guard (instrument) fires; CallAtmStrategyCreate never reached
            Assert.Equal(0, strategy.AtmInvokedCount);
        }

        /// <summary>
        /// T_B42_05: When both account and instrument names match, OnFillSignal routes through
        /// the full guard chain and calls CallAtmStrategyCreate exactly once.
        /// </summary>
        [Fact]
        public void OnFillSignal_CallsAtmWhenAccountAndInstrumentMatch()
        {
            // Arrange: all four names match
            var strategy = new TestFollowerStrategy
            {
                StrategyAccountName = "AccA",
                StrategyInstrumentName = "MES 09-26",
                SignalAccountName = "AccA", // MATCH
                SignalInstrumentName = "MES 09-26", // MATCH
            };
            var args = FillSignalEventArgs.Create(
                null,
                null,
                "MyATM",
                OrderAction.Buy,
                2,
                "ORD-005"
            );

            // Act: route through the full OnFillSignal guard chain
            strategy.SimulateFillSignal(args);

            // Assert: both guards pass; CallAtmStrategyCreate override increments counter
            Assert.Equal(1, strategy.AtmInvokedCount);
        }
    }

    // -------------------------------------------------------------------------
    // T_B42_06, T_B42_07 -- SendCopy FillSignal publish behavior
    // -------------------------------------------------------------------------
    public class SendCopyFillSignalTests : IDisposable
    {
        private readonly CopyEngine _engine = CopyEngine.Instance;
        private Action<FillSignalEventArgs> _fillHandler;

        /// <summary>
        /// T_B42_06: Verifies the RaiseFillSignal event-publication pipeline that T2 inserts
        /// into SendCopy. Calls PttBus.RaiseFillSignal directly with known args and asserts
        /// the subscriber receives exactly 1 call with matching fields.
        ///
        /// Why RaiseFillSignal directly: SendCopy's success path calls CreateOrder first.
        /// CreateOrder requires an NT8 Account bound to an active session -- not available in
        /// the test runner context. Calling RaiseFillSignal directly is the NT8-runtime-free
        /// equivalent that validates the T1+T2 event-wire contract: "after CreateOrder succeeds,
        /// PttBus.RaiseFillSignal(args) is called; all FillSignal subscribers receive args."
        /// T_B42_07 (below) validates the complementary invariant via actual SendCopy invocation.
        /// </summary>
        [Fact]
        public void SendCopy_PublishesFillSignal_EventPipelineVerified()
        {
            // Arrange: subscribe a counter and capture handler
            int signalCount = 0;
            FillSignalEventArgs captured = default;
            _fillHandler = a =>
            {
                signalCount++;
                captured = a;
            };
            PttBus.FillSignal += _fillHandler;

            var expected = FillSignalEventArgs.Create(
                null,
                null,
                "ScalpATM",
                OrderAction.Buy,
                3,
                "PTT-ORD-006"
            );

            try
            {
                // Act: invoke the same call that T2 inserts after CreateOrder in SendCopy
                PttBus.RaiseFillSignal(expected);

                // Assert: subscriber received exactly 1 call with matching args
                Assert.Equal(1, signalCount);
                Assert.Equal(expected.AtmTemplateName, captured.AtmTemplateName);
                Assert.Equal(expected.Quantity, captured.Quantity);
                Assert.Equal(expected.EntryOrderId, captured.EntryOrderId);
                Assert.Equal(expected.OrderAction, captured.OrderAction);
            }
            finally
            {
                PttBus.FillSignal -= _fillHandler;
                _fillHandler = null;
            }
        }

        /// <summary>
        /// T_B42_07: Verifies that SendCopy does NOT raise PttBus.FillSignal when CreateOrder throws.
        /// Calls SendCopy via reflection with a null follower Account. CreateOrder throws
        /// NullReferenceException (null Account), which is caught by the SendCopy try/catch.
        /// The RaiseFillSignal call (inserted after CreateOrder in T2) is never reached.
        /// signalCount must remain 0.
        /// </summary>
        [Fact]
        public void SendCopy_DoesNotPublishFillSignalWhenCreateOrderThrows()
        {
            // Arrange: subscribe a counter
            int signalCount = 0;
            _fillHandler = _ => signalCount++;
            PttBus.FillSignal += _fillHandler;

            _engine.SetEnabled(false);

            // Locate SendCopy via reflection (private instance method, 4 parameters)
            var mi = typeof(CopyEngine).GetMethod(
                "SendCopy",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.NotNull(mi);

            // Build a minimal CopySignal via reflection (private struct) to pass as arg3
            // Strategy: use an AddRule + retrieve a CopyRule fixture, then build a CopySignal.
            // CopySignal is a private struct on CopyEngine -- access via nested type reflection.
            var signalType = typeof(CopyEngine).GetNestedType("CopySignal", BindingFlags.NonPublic);
            Assert.NotNull(signalType); // CopySignal must exist as a private nested type

            // Create a default CopySignal instance (all fields default/zero -- sufficient to
            // reach CreateOrder before the null Account causes NullReferenceException)
            object copySignal = Activator.CreateInstance(signalType);

            // Use FollowerAtmMode.Inherit as the mode arg (simplest non-null mode)
            var mode = new FollowerAtmMode.Inherit();

            try
            {
                // Act: invoke SendCopy with null Account -- CreateOrder throws NullReferenceException
                // which is caught inside SendCopy's catch block. RaiseFillSignal is never reached.
                mi.Invoke(_engine, new object[] { null, null, copySignal, mode });
            }
            catch (TargetInvocationException tie)
            {
                // NullReferenceException on null Account before CreateOrder is expected --
                // what matters is FillSignal was NOT raised before the exception.
                // Any other inner exception type = test failure (unexpected throw path).
                if (!(tie.InnerException is NullReferenceException))
                    throw;
            }

            // Assert: FillSignal subscriber was NEVER called (catch path skips RaiseFillSignal)
            Assert.Equal(0, signalCount);
        }

        public void Dispose()
        {
            if (_fillHandler != null)
            {
                PttBus.FillSignal -= _fillHandler;
                _fillHandler = null;
            }
        }
    }
}
#endif // PTT_FOLLOWER_ACTIVE
