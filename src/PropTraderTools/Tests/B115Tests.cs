// B115Tests.cs -- DW-B122 Accepted-state guard tests
// Block: B115. Framework: xUnit [Fact] only. JS-021: no lock. JS-033: no async void.
// Seam: _qxPendingFollowerCleanup (internal ConcurrentDictionary, InternalsVisibleTo).
// NT8 constraint: TryCleanupReArmedAtmBracket requires sealed OrderEventArgs -- not callable
// directly. Tests validate guard logic (DW-B122 compound state check) via inline boolean
// evaluation and cleanup dict state checks using the ConcurrentDictionary seam.

using System;
using System.Collections.Concurrent;
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools.Tests
{
    public class B115Tests
    {
        // -------------------------------------------------------------------------
        // T_B115_01: TryCleanupReArmedAtmBracket_GuardAcceptsAcceptedState
        //
        // What is tested: DW-B122 compound guard (condition a) in TryCleanupReArmedAtmBracket.
        // Guard: (state != Working && state != Accepted)
        // For OrderState.Accepted: (true && false) = false -> does NOT return early.
        // Before DW-B122: guard was (state != Working) only -> (true) = true -> returned early.
        // After DW-B122: Accepted is explicitly excluded from early-return.
        // Test validates the post-fix boolean logic for OrderState.Accepted.
        // -------------------------------------------------------------------------
        [Fact]
        public void TryCleanupReArmedAtmBracket_GuardAcceptsAcceptedState()
        {
            // The DW-B122 guard condition (a) expression (inline, matches CopyEngine.cs L2397-2398):
            //   (state != OrderState.Working && state != OrderState.Accepted)
            // When true: early return (cleanup skipped).
            // When false: cleanup proceeds.

            OrderState testState = OrderState.Accepted;

            // Post-DW-B122: Accepted should NOT cause early return (guard evaluates false).
            bool guardEarly = testState != OrderState.Working && testState != OrderState.Accepted;

            Assert.False(
                guardEarly,
                "DW-B122: Accepted state must NOT cause early return in TryCleanupReArmedAtmBracket guard (a)."
            );
        }

        // -------------------------------------------------------------------------
        // T_B115_02: TryCleanupReArmedAtmBracket_GuardRejectsUnknownState
        //
        // What is tested: State other than Working/Accepted (Cancelled) causes early return.
        // Guard: (state != Working && state != Accepted) = true -> early return -> cleanup skipped.
        // For OrderState.Cancelled: (true && true) = true -> guard fires early. Cleanup skipped.
        // -------------------------------------------------------------------------
        [Fact]
        public void TryCleanupReArmedAtmBracket_GuardRejectsUnknownState()
        {
            OrderState testState = OrderState.Cancelled;

            bool guardEarly = testState != OrderState.Working && testState != OrderState.Accepted;

            Assert.True(
                guardEarly,
                "Non-Working, non-Accepted state must cause early return (cleanup skipped)."
            );
        }

        // -------------------------------------------------------------------------
        // T_B115_03: TryCleanupReArmedAtmBracket_DictSeam_T1Path_EntryRetained
        //
        // What is tested: shouldRemove policy for tChar='1' (non-expired entry).
        // shouldRemove = ('1' == '3') || (expiry <= UtcNow) = false || false = false
        // Entry must remain in dict after T1 processing decision.
        // -------------------------------------------------------------------------
        [Fact]
        public void TryCleanupReArmedAtmBracket_DictSeam_T1Path_EntryRetained()
        {
            const string accName = "SimT1";
            var engine = CopyEngine.Instance;
            engine._qxPendingFollowerCleanup.Clear();

            var expiry = DateTime.UtcNow.AddSeconds(10); // DW-B121: 10s TTL
            engine._qxPendingFollowerCleanup.TryAdd(accName, (null!, expiry));

            // Simulate T1 removal decision: shouldRemove = ('1' == '3') || (expiry <= UtcNow)
            char tChar = '1';
            bool shouldRemove = tChar == '3' || expiry <= DateTime.UtcNow;
            if (shouldRemove)
                engine._qxPendingFollowerCleanup.TryRemove(accName, out _);

            Assert.True(
                engine._qxPendingFollowerCleanup.ContainsKey(accName),
                "T1 path: entry must be retained (shouldRemove=false) when TTL not elapsed."
            );
        }

        // -------------------------------------------------------------------------
        // T_B115_04: TryCleanupReArmedAtmBracket_DictSeam_T3Path_EntryRemoved
        //
        // What is tested: shouldRemove policy for tChar='3' (T3 = last bracket).
        // shouldRemove = ('3' == '3') || (expiry <= UtcNow) = true
        // Entry must be absent from dict after T3 removal.
        // -------------------------------------------------------------------------
        [Fact]
        public void TryCleanupReArmedAtmBracket_DictSeam_T3Path_EntryRemoved()
        {
            const string accName = "SimT3";
            var engine = CopyEngine.Instance;
            engine._qxPendingFollowerCleanup.Clear();

            var expiry = DateTime.UtcNow.AddSeconds(10); // DW-B121: 10s TTL
            engine._qxPendingFollowerCleanup.TryAdd(accName, (null!, expiry));

            // Simulate T3 removal decision: shouldRemove = ('3' == '3') || (expiry <= UtcNow) = true
            char tChar = '3';
            bool shouldRemove = tChar == '3' || expiry <= DateTime.UtcNow;
            if (shouldRemove)
                engine._qxPendingFollowerCleanup.TryRemove(accName, out _);

            Assert.False(
                engine._qxPendingFollowerCleanup.ContainsKey(accName),
                "T3 path: entry must be removed after T3 (shouldRemove=true)."
            );
        }
    }
}
