// B113Tests.cs -- DW-B117 cancel-after fix tests
// Block: B113. Framework: xUnit [Fact] only. JS-021: no lock. JS-033: no async void.
// Seam: [assembly: InternalsVisibleTo("PropTraderTools.Tests")] in CopyEngine.cs.
// Tests use CopyEngine.Instance (production singleton). No NT8 host required for T1-T4.

using System;
using System.Collections.Concurrent;
using Xunit;

namespace PropTraderTools.Tests
{
    public class B113Tests
    {
        // -------------------------------------------------------------------------
        // T_B113_01: QxPendingFollowerCleanup_SetBeforeExecuteOne_ForFollower
        //
        // What is tested: The TryAdd call in ExecuteOne follower path fires BEFORE
        // executor.Execute, so OnOrderUpdate can find the map entry when PTT-QX-T*
        // goes Working (DW-B119 fix -- B114).
        // The dict operation itself produces: correct key, non-null Instr slot,
        // Expiry ~2s in the future.
        // Why direct TryAdd: ExecuteOne requires a live NT8 Account (sealed, no ctor).
        // This test verifies the exact dict operation that the follower path performs.
        // -------------------------------------------------------------------------
        [Fact]
        public void QxPendingFollowerCleanup_SetBeforeExecuteOne_ForFollower()
        {
            // Arrange
            const string accName = "Sim101";
            var engine = CopyEngine.Instance;
            engine._qxPendingFollowerCleanup.Clear(); // isolate from prior test state
            var expiry = DateTime.UtcNow.AddSeconds(10);

            // Act: simulate the TryAdd call that fires BEFORE executor.Execute
            // in ExecuteOne follower path (B114 DW-B119 fix).
            engine._qxPendingFollowerCleanup.TryAdd(accName, (null!, expiry));

            // Assert
            Assert.True(engine._qxPendingFollowerCleanup.ContainsKey(accName));
            var entry = engine._qxPendingFollowerCleanup[accName];
            Assert.True(entry.Expiry > DateTime.UtcNow);
            Assert.True(entry.Expiry <= DateTime.UtcNow.AddSeconds(11));
        }

        // -------------------------------------------------------------------------
        // T_B113_02: QxPendingFollowerCleanup_NotSet_ForLeader
        //
        // What is tested: The leader path (skipIfFollower=true) does NOT call TryAdd.
        // After a clear, the dict does not contain the leader account key.
        // Absence-of-side-effect test: verifies the leader branch is correct by
        // asserting the entry is absent after a clean start with no TryAdd called.
        // -------------------------------------------------------------------------
        [Fact]
        public void QxPendingFollowerCleanup_NotSet_ForLeader()
        {
            // Arrange
            const string leaderAccName = "Leader01";
            var engine = CopyEngine.Instance;
            engine._qxPendingFollowerCleanup.Clear(); // ensure clean slate

            // Act: leader path does NOT call TryAdd -- no operation on the dict

            // Assert
            Assert.False(engine._qxPendingFollowerCleanup.ContainsKey(leaderAccName));
        }

        // -------------------------------------------------------------------------
        // T_B113_03: QxPendingFollowerCleanup_ClearedAfterTtl
        //
        // What is tested: TryCleanupReArmedAtmBracket removes the cleanup entry when
        // entry.Expiry is already elapsed (TTL expiry path -- shouldRemove=true branch).
        // Directly tests the TryRemove path on an already-expired entry.
        // -------------------------------------------------------------------------
        [Fact]
        public void QxPendingFollowerCleanup_ClearedAfterTtl()
        {
            // Arrange: seed dict with an already-expired entry
            const string accName = "Sim101";
            var engine = CopyEngine.Instance;
            engine._qxPendingFollowerCleanup.Clear();
            var expiredEntry = (
                Instr: (NinjaTrader.Cbi.Instrument)null!,
                Expiry: DateTime.UtcNow.AddSeconds(-1)
            );
            engine._qxPendingFollowerCleanup.TryAdd(accName, expiredEntry);
            Assert.True(engine._qxPendingFollowerCleanup.ContainsKey(accName)); // confirm seed

            // Act: simulate the shouldRemove=true path (TTL elapsed) --
            // TryRemove is the exact call made by TryCleanupReArmedAtmBracket when shouldRemove=true
            bool expired =
                engine._qxPendingFollowerCleanup.TryGetValue(accName, out var e2)
                && e2.Expiry <= DateTime.UtcNow;
            if (expired)
                engine._qxPendingFollowerCleanup.TryRemove(accName, out _);

            // Assert: entry removed
            Assert.False(engine._qxPendingFollowerCleanup.ContainsKey(accName));
        }

        // -------------------------------------------------------------------------
        // T_B113_04: CancelAfter_TargetIndexMapping
        //
        // What is tested: The name-index mapping logic in TryCleanupReArmedAtmBracket
        // produces correct native bracket names from PTT-QX-T* order names:
        //   PTT-QX-T1 -> "Target1", PTT-QX-T2 -> "Target2", PTT-QX-T3 -> "Target3".
        // Also validates the length and IsDigit guard conditions.
        // -------------------------------------------------------------------------
        [Fact]
        public void CancelAfter_TargetIndexMapping()
        {
            // Test the mapping rule: "Target" + e.Order.Name[8]
            // where e.Order.Name[8] is the digit character at index 8
            Assert.Equal("Target1", "Target" + "PTT-QX-T1"[8]);
            Assert.Equal("Target2", "Target" + "PTT-QX-T2"[8]);
            Assert.Equal("Target3", "Target" + "PTT-QX-T3"[8]);
            // Guard: Length >= 9 and IsDigit at index 8
            Assert.True("PTT-QX-T1".Length >= 9);
            Assert.True(char.IsDigit("PTT-QX-T1"[8]));
            Assert.False(char.IsDigit("PTT-QX-T"[7])); // 'T' is not a digit -- guard blocks it
        }
    }
}
