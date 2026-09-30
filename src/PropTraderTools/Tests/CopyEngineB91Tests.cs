// DW-B91 CopyEngine tests -- Ticket-1 (DW-B91-A: Entry order dispatch dedup)
// 3 [Fact] tests: IsEntryDispatched_FirstCall_ReturnsFalseAndMarksDispatched,
//                 IsEntryDispatched_AfterEvictDedup_SecondCallReturnsFalse,
//                 IsEntryDispatched_DifferentOrderIds_IndependentTracking
// JS-021: no lock. JS-033: no async void. JS-002: no return null. JS-001: no throw.
// xUnit ONLY. CYC <= 8 per method.

using System;
using System.Reflection;
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools
{
    public class CopyEngineB91Tests
    {
        // ----------------------------------------------------------------
        // Ticket-1 -- DW-B91-A: IsEntryDispatched dedup guard
        // ----------------------------------------------------------------

        // T_B91A_01: first call returns false (allowed), second call returns true (blocked).
        // Verifies TryAdd side-effect: orderId is marked as dispatched on first call.
        [Fact]
        public void IsEntryDispatched_FirstCall_ReturnsFalseAndMarksDispatched()
        {
            var engine = CopyEngine.Instance;
            var mi = typeof(CopyEngine).GetMethod(
                "IsEntryDispatched",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.NotNull(mi);

            string orderId = "B91A-test-01-" + Guid.NewGuid();

            bool first = (bool)mi.Invoke(engine, new object[] { orderId });
            bool second = (bool)mi.Invoke(engine, new object[] { orderId });

            Assert.False(first, "First call must return false (dispatch allowed)");
            Assert.True(
                second,
                "Second call must return true (dispatch blocked -- already dispatched)"
            );
        }

        // T_B91A_02: after EvictDedup(Filled), IsEntryDispatched returns false again (slot evicted).
        // Verifies that EvictDedup clears the _entryDispatchedOrders slot for orderId.
        [Fact]
        public void IsEntryDispatched_AfterEvictDedup_SecondCallReturnsFalse()
        {
            var engine = CopyEngine.Instance;
            var miDisp = typeof(CopyEngine).GetMethod(
                "IsEntryDispatched",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.NotNull(miDisp);

            string orderId = "B91A-test-02-" + Guid.NewGuid();

            // Mark as dispatched
            bool before = (bool)miDisp.Invoke(engine, new object[] { orderId });
            Assert.False(before, "Pre-condition: first call must return false");

            // Evict via EvictDedup with Filled state
            engine.EvictDedup(orderId, OrderState.Filled);

            // After eviction the slot must be open again
            bool after = (bool)miDisp.Invoke(engine, new object[] { orderId });
            Assert.False(
                after,
                "After EvictDedup, IsEntryDispatched must return false (slot evicted)"
            );
        }

        // T_B91A_03: two distinct orderIds track independently.
        // Verifies each orderId has its own slot; marking one does not affect the other.
        [Fact]
        public void IsEntryDispatched_DifferentOrderIds_IndependentTracking()
        {
            var engine = CopyEngine.Instance;
            var mi = typeof(CopyEngine).GetMethod(
                "IsEntryDispatched",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            Assert.NotNull(mi);

            string orderA = "B91A-test-03A-" + Guid.NewGuid();
            string orderB = "B91A-test-03B-" + Guid.NewGuid();

            bool a1 = (bool)mi.Invoke(engine, new object[] { orderA }); // first A
            bool b1 = (bool)mi.Invoke(engine, new object[] { orderB }); // first B
            bool a2 = (bool)mi.Invoke(engine, new object[] { orderA }); // second A
            bool b2 = (bool)mi.Invoke(engine, new object[] { orderB }); // second B

            Assert.False(a1, "First call orderA must return false");
            Assert.False(b1, "First call orderB must return false");
            Assert.True(a2, "Second call orderA must return true (blocked)");
            Assert.True(b2, "Second call orderB must return true (blocked)");
        }

        // ----------------------------------------------------------------
        // Ticket-2 -- DW-B91-B: FlattenFollower open-position guard
        // ----------------------------------------------------------------

        // T_B91B_01: acc=null guard -- flattenOne is never called.
        // Verifies null guard (a) in FlattenFollower: if acc==null, return immediately.
        [Fact]
        public void FlattenFollower_NullAccount_DoesNotCallFlattenOne()
        {
            var mi = typeof(CopyEngine).GetMethod(
                "FlattenFollower",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Assert.NotNull(mi);

            int flattenCallCount = 0;
            Action<NinjaTrader.Cbi.Account, NinjaTrader.Instrument.Instrument> flattenOne = (
                a,
                i
            ) => flattenCallCount++;
            Func<NinjaTrader.Cbi.Account, NinjaTrader.Instrument.Instrument, bool> hasOpen = (
                a,
                i
            ) => true;

            mi.Invoke(null, new object[] { null, null, hasOpen, flattenOne });

            Assert.Equal(0, flattenCallCount);
        }

        // T_B91B_02: hasOpenPosition returns false -- flattenOne is never called.
        // Verifies re-entry protection guard (b) in FlattenFollower: already-flat follower is skipped.
        [Fact]
        public void FlattenFollower_NoOpenPosition_DoesNotCallFlattenOne()
        {
            var mi = typeof(CopyEngine).GetMethod(
                "FlattenFollower",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Assert.NotNull(mi);

            int flattenCallCount = 0;
            // Sentinel non-null account and instrument objects (content irrelevant for this guard test)
            var acc = new NinjaTrader.Cbi.Account();
            var instr = new NinjaTrader.Instrument.Instrument();
            Action<NinjaTrader.Cbi.Account, NinjaTrader.Instrument.Instrument> flattenOne = (
                a,
                i
            ) => flattenCallCount++;
            Func<NinjaTrader.Cbi.Account, NinjaTrader.Instrument.Instrument, bool> hasOpen = (
                a,
                i
            ) => false; // already flat

            mi.Invoke(null, new object[] { acc, instr, hasOpen, flattenOne });

            Assert.Equal(0, flattenCallCount);
        }

        // T_B91B_03: hasOpenPosition returns true -- flattenOne is called exactly once.
        // Verifies the happy path: open-position follower is flattened.
        [Fact]
        public void FlattenFollower_HasOpenPosition_CallsFlattenOne()
        {
            var mi = typeof(CopyEngine).GetMethod(
                "FlattenFollower",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            Assert.NotNull(mi);

            int flattenCallCount = 0;
            var acc = new NinjaTrader.Cbi.Account();
            var instr = new NinjaTrader.Instrument.Instrument();
            Action<NinjaTrader.Cbi.Account, NinjaTrader.Instrument.Instrument> flattenOne = (
                a,
                i
            ) => flattenCallCount++;
            Func<NinjaTrader.Cbi.Account, NinjaTrader.Instrument.Instrument, bool> hasOpen = (
                a,
                i
            ) => true; // has open position

            mi.Invoke(null, new object[] { acc, instr, hasOpen, flattenOne });

            Assert.Equal(1, flattenCallCount);
        }
    }
}
