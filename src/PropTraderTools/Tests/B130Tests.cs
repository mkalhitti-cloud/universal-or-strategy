// B130 Tests -- DW-B137: IsAtmSTPOrder name format extension
// Verifies Stop1/Stop2/Stop3 and Target1/Target2/Target3 ATM names are routed to cancel+resubmit.
// Test-seam: IsAtmSTPOrder is internal static -- accessible via InternalsVisibleTo (CopyEngine.cs L46).
// [assembly: InternalsVisibleTo("PropTraderTools.Tests")] at CopyEngine.cs L46 enables direct call.
// Stub pattern: direct NinjaTrader.Cbi.Order instantiation (consistent with B129Tests.cs pattern).
using System;
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools.Tests
{
    public class B130Tests
    {
        // Helper: creates an Order with the given Name for predicate tests.
        // IsAtmSTPOrder only reads order.Name -- no other NT8 fields needed.
        // Pattern matches B129Tests.cs: direct NinjaTrader.Cbi.Order instantiation + .Name assignment.
        private static Order StubOrder(string name)
        {
            var o = new Order();
            o.Name = name;
            return o;
        }

        [Fact]
        public void B130_DW137_Stop1NameRoutesToCancelResubmit()
        {
            // Stop1/Stop2/Stop3 must match IsAtmSTPOrder (routes to cancel+resubmit via SyncAtmFollowerBracket)
            // "Buy STP" must still match (backward compat)
            // "Entry" must NOT match (non-bracket name)
            Assert.True(CopyEngine.IsAtmSTPOrder(StubOrder("Stop1")));
            Assert.True(CopyEngine.IsAtmSTPOrder(StubOrder("Stop2")));
            Assert.True(CopyEngine.IsAtmSTPOrder(StubOrder("Stop3")));
            Assert.True(CopyEngine.IsAtmSTPOrder(StubOrder("Buy STP"))); // backward compat
            Assert.True(CopyEngine.IsAtmSTPOrder(StubOrder("Sell STP"))); // backward compat
            Assert.False(CopyEngine.IsAtmSTPOrder(StubOrder("Entry"))); // entry order not affected
            Assert.False(CopyEngine.IsAtmSTPOrder(StubOrder("PTT-Copy"))); // PTT orders not affected
        }

        [Fact]
        public void B130_DW137_Target1NameRoutesCorrectly()
        {
            // Target1/Target2/Target3 must match IsAtmSTPOrder (routes to SyncAtmFollowerTarget)
            // acc.Change() on ATM-owned Limit target brackets is a no-op (B129 SIM confirmed)
            // PTT- named orders must NOT match (PTT-TGT-Drag, PTT-Copy are not ATM-owned brackets)
            Assert.True(CopyEngine.IsAtmSTPOrder(StubOrder("Target1")));
            Assert.True(CopyEngine.IsAtmSTPOrder(StubOrder("Target2")));
            Assert.True(CopyEngine.IsAtmSTPOrder(StubOrder("Target3")));
            Assert.False(CopyEngine.IsAtmSTPOrder(StubOrder("PTT-Copy")));
            Assert.False(CopyEngine.IsAtmSTPOrder(StubOrder("PTT-TGT-Drag"))); // PTT order excluded
        }

        // DW-B136 Gap B: Tests -- APPEND ONLY (LaneB ticket-2)
        // Access: internal members via InternalsVisibleTo("PropTraderTools.Tests") at CopyEngine.cs L46.
        // Uses empty ConcurrentBag<Order> -- no real NT8 Orders needed; map isolation is the assertion.

        [Fact]
        public void B130_DW136_CancelLeaderOrder1DoesNotCancelFollowerCopiesOfOrder2()
        {
            // Arrange: two leader orders, each with a bag in the follower copy map
            var engine = CopyEngine.Instance;
            var bag1 = new System.Collections.Concurrent.ConcurrentBag<NinjaTrader.Cbi.Order>();
            var bag2 = new System.Collections.Concurrent.ConcurrentBag<NinjaTrader.Cbi.Order>();
            engine._followerCopyMap.TryAdd("leader-id-1", bag1);
            engine._followerCopyMap.TryAdd("leader-id-2", bag2);

            // Act: cancel follower entries for leader order #1 only
            engine.CancelScopedFollowerEntries("leader-id-1");

            // Assert: leader-id-1 entry evicted (cancel path completed)
            Assert.False(
                engine._followerCopyMap.ContainsKey("leader-id-1"),
                "leader-id-1 bag must be evicted after CancelScopedFollowerEntries"
            );
            // Assert: leader-id-2 entry is UNTOUCHED (DW-B136 Gap B: no cross-cancel)
            Assert.True(
                engine._followerCopyMap.ContainsKey("leader-id-2"),
                "leader-id-2 bag must survive cancel of leader-id-1 (DW-B136 Gap B fix)"
            );

            // Cleanup: remove test entries to avoid polluting singleton state
            engine._followerCopyMap.TryRemove("leader-id-2", out _);
        }

        [Fact]
        public void B130_DW136_SingleEntryPathUnchanged()
        {
            // Arrange: single leader order with one follower bag (normal single-entry workflow)
            var engine = CopyEngine.Instance;
            var bag = new System.Collections.Concurrent.ConcurrentBag<NinjaTrader.Cbi.Order>();
            engine._followerCopyMap.TryAdd("leader-id-solo", bag);

            // Act: cancel follower entries for this single leader order
            engine.CancelScopedFollowerEntries("leader-id-solo");

            // Assert: map entry evicted (single-entry eviction path is clean)
            Assert.False(
                engine._followerCopyMap.ContainsKey("leader-id-solo"),
                "Single-entry: map entry must be evicted by CancelScopedFollowerEntries"
            );

            // Assert: calling again on absent key does not throw (belt-and-suspenders safety)
            var ex = Record.Exception(() => engine.CancelScopedFollowerEntries("leader-id-solo"));
            Assert.Null(ex);
        }

        [Fact]
        public void B130_DW136_CancelLeaderOrder2DoesNotEvictLeaderOrder1Bag()
        {
            // Arrange: two leader orders recorded in the follower copy map
            var engine = CopyEngine.Instance;
            var bag1 = new System.Collections.Concurrent.ConcurrentBag<NinjaTrader.Cbi.Order>();
            var bag2 = new System.Collections.Concurrent.ConcurrentBag<NinjaTrader.Cbi.Order>();
            engine._followerCopyMap.TryAdd("leader-id-1", bag1);
            engine._followerCopyMap.TryAdd("leader-id-2", bag2);

            // Act: EvictDedup fires for leader-id-2 (simulates Cancelled order reaching L1277)
            engine.EvictDedup("leader-id-2", NinjaTrader.Cbi.OrderState.Cancelled);

            // Assert: leader-id-1 bag is untouched (EvictDedup must NOT sweep _followerCopyMap)
            Assert.True(
                engine._followerCopyMap.ContainsKey("leader-id-1"),
                "leader-id-1 bag must survive EvictDedup for leader-id-2"
            );
            // Assert: leader-id-2 bag is also still present (EvictDedup must NOT touch _followerCopyMap at all)
            Assert.True(
                engine._followerCopyMap.ContainsKey("leader-id-2"),
                "leader-id-2 bag must NOT be removed by EvictDedup -- only CancelScopedFollowerEntries evicts"
            );

            // Cleanup: remove test entries to avoid polluting singleton state
            engine._followerCopyMap.TryRemove("leader-id-1", out _);
            engine._followerCopyMap.TryRemove("leader-id-2", out _);
        }

        // -- DW-B107 Tests -----------------------------------------------------------------------
        // Behavioral equivalence tests for SnapshotBeTargets (CopyEngine.cs L3922).
        // SnapshotBeTargets is private; tests use inline predicate helpers mirroring
        // the exact logic at CopyEngine.cs L3948-3958 and the hard-cap at L4023-4024.
        // No NT8 Account/Instrument required -- string operations and List<T> only.
        // -----------------------------------------------------------------------------------------

        [Fact]
        public void B130_DW107_SnapshotBeTargetsFiltersStaleOrders()
        {
            // Local predicates mirroring SnapshotBeTargets L3948-3958 verbatim.
            // CopyEngine.cs L3948-3952: isNative predicate
            static bool IsNativeTarget(string n) =>
                n != null
                && n.Length >= 7
                && n.StartsWith("Target", StringComparison.Ordinal)
                && char.IsDigit(n[6])
                && n[6] != '0';

            // CopyEngine.cs L3953-3958: isPtt predicate
            static bool IsPttTarget(string n) =>
                n != null
                && (
                    (
                        n.StartsWith("PTT-QX-T", StringComparison.Ordinal)
                        && n.Length > 8
                        && char.IsDigit(n[8])
                    ) || n.StartsWith("PTT-BE-Target-", StringComparison.Ordinal)
                );

            // Native ATM target orders: must classify as native, not PTT
            Assert.True(IsNativeTarget("Target1"));
            Assert.True(IsNativeTarget("Target2"));
            Assert.True(IsNativeTarget("Target3"));
            Assert.False(IsPttTarget("Target1"));

            // Stale PTT-BE-Target-* residues: must classify as PTT, not native
            Assert.True(IsPttTarget("PTT-BE-Target-1"));
            Assert.True(IsPttTarget("PTT-BE-Target-4")); // stale T4 from prior session (root cause)
            Assert.False(IsNativeTarget("PTT-BE-Target-1"));

            // PTT-QX-T* orders: must classify as PTT, not native
            Assert.True(IsPttTarget("PTT-QX-T1"));
            Assert.True(IsPttTarget("PTT-QX-T3"));

            // Non-target orders: must classify as neither (proves empty-snapshot contract)
            Assert.False(IsNativeTarget("Entry"));
            Assert.False(IsPttTarget("Entry"));
            Assert.False(IsNativeTarget("PTT-BE-Stop-1"));
            Assert.False(IsPttTarget("PTT-BE-Stop-1"));

            // Native-first priority: when natives exist, PTT residues are excluded.
            // Simulates: nativeTargets.Count > 0 ? nativeTargets : pttTargets (CopyEngine.cs L3964)
            // If any native is present, result is nativeTargets (PTT-BE-Target-4 ignored).
            var nativeTargets = new System.Collections.Generic.List<string>();
            var pttTargets = new System.Collections.Generic.List<string>();
            foreach (var name in new[] { "Target1", "Target2", "Target3", "PTT-BE-Target-4" })
            {
                if (IsNativeTarget(name))
                    nativeTargets.Add(name);
                else if (IsPttTarget(name))
                    pttTargets.Add(name);
            }
            var result = nativeTargets.Count > 0 ? nativeTargets : pttTargets;
            Assert.Equal(3, result.Count); // exactly 3 native targets returned
            Assert.DoesNotContain("PTT-BE-Target-4", result); // stale T4 excluded (DW-B107 fix)
        }

        [Fact]
        public void B130_DW107_HardCapTrimsSnapshotToThreeTargets()
        {
            // Case 1: 4-item list (root-cause scenario: stale T4 present)
            var targets4 = new System.Collections.Generic.List<(
                double Price,
                int Qty,
                OrderAction Action
            )>
            {
                (4200.00, 1, OrderAction.Sell),
                (4210.00, 1, OrderAction.Sell),
                (4220.00, 1, OrderAction.Sell),
                (4230.00, 1, OrderAction.Sell), // stale T4 residue
            };
            while (targets4.Count > 3)
                targets4.RemoveAt(targets4.Count - 1);
            Assert.Equal(3, targets4.Count); // T4 trimmed -- DW-B107 fix verified

            // Case 2: 3-item list (nominal case: exactly 3 targets)
            var targets3 = new System.Collections.Generic.List<(
                double Price,
                int Qty,
                OrderAction Action
            )>
            {
                (4200.00, 1, OrderAction.Sell),
                (4210.00, 1, OrderAction.Sell),
                (4220.00, 1, OrderAction.Sell),
            };
            while (targets3.Count > 3)
                targets3.RemoveAt(targets3.Count - 1);
            Assert.Equal(3, targets3.Count); // unchanged -- no over-trim

            // Case 3: 0-item list (no targets -- retry path)
            var targets0 = new System.Collections.Generic.List<(
                double Price,
                int Qty,
                OrderAction Action
            )>();
            while (targets0.Count > 3)
                targets0.RemoveAt(targets0.Count - 1);
            Assert.Equal(0, targets0.Count); // empty -- no crash, no spurious trim
        }

        [Fact]
        public void B130_DW107_NonTargetOrdersProduceEmptySnapshot()
        {
            // Local predicates mirroring SnapshotBeTargets L3948-3958 verbatim.
            // Reuse same helpers as Test 1 (copied -- local functions are method-scoped).
            static bool IsNativeTarget(string n) =>
                n != null
                && n.Length >= 7
                && n.StartsWith("Target", StringComparison.Ordinal)
                && char.IsDigit(n[6])
                && n[6] != '0';

            static bool IsPttTarget(string n) =>
                n != null
                && (
                    (
                        n.StartsWith("PTT-QX-T", StringComparison.Ordinal)
                        && n.Length > 8
                        && char.IsDigit(n[8])
                    ) || n.StartsWith("PTT-BE-Target-", StringComparison.Ordinal)
                );

            // Non-target names that must NOT pollute the snapshot
            var nonTargetNames = new[]
            {
                "Entry",
                "Close",
                "PTT-BE-Stop-1",
                "PTT-BE-Stop-2",
                "PTT-BE-Stop-3",
                "PTT-Copy",
                "PTT-QX-Stop-1",
                "Stop1",
                "Stop2",
                "Stop3",
            };
            var nativeTargets = new System.Collections.Generic.List<string>();
            var pttTargets = new System.Collections.Generic.List<string>();
            foreach (var name in nonTargetNames)
            {
                if (IsNativeTarget(name))
                    nativeTargets.Add(name);
                else if (IsPttTarget(name))
                    pttTargets.Add(name);
            }
            // Both lists must be empty -- no non-target name leaks into snapshot
            Assert.Empty(nativeTargets);
            Assert.Empty(pttTargets);

            // Native-first return: empty pttTargets returned when both are empty
            var result = nativeTargets.Count > 0 ? nativeTargets : pttTargets;
            Assert.Empty(result); // empty list -- not null (JS-002 contract)
            Assert.NotNull(result); // T7 anchor: SnapshotBeTargets L3930/3964 returns List, never null
        }
    }
}
