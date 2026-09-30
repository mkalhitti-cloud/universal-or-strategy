// B143 xUnit tests: DW-B142-MGC-01 + DW-B142-MGC-02 instrument-level entry guard (commit 3f709a91).
// Tests use inline predicates mirroring production CopyEngine logic.
// PropTraderTools.Tests targets net8.0; PropTraderTools targets net48 (NT8 requirement).
// Direct ProjectReference is impossible across TFMs -- inline mirrors established pattern
// (see CopyEngineB137Tests.cs, B140Tests.cs). NT8 OrderState replaced by inline constants.
// Framework: xUnit ONLY. NEVER NUnit or MSTest.
using System.Collections.Concurrent;
using Xunit;

namespace PropTraderTools.Tests
{
    public sealed class B143Tests
    {
        // ------------------------------------------------------------------
        // Inline state fields -- mirror private fields of CopyEngine.
        // Each [Fact] creates a fresh B143Tests instance, so state is isolated.
        // JS-025: ConcurrentDictionary for shared state. JS-021: no lock().
        // ------------------------------------------------------------------
        private readonly ConcurrentDictionary<string, byte> _liveEntryInstruments = new();
        private readonly ConcurrentDictionary<string, string> _entryInstrKeyByOrderId = new();
        private readonly ConcurrentDictionary<string, byte> _entryDispatchedOrders = new();
        private readonly ConcurrentDictionary<string, double> _dedupCache = new();

        // ------------------------------------------------------------------
        // Inline method mirrors -- match production CopyEngine logic exactly.
        // Source confirmed at commit 3f709a91: CopyEngine.cs ~L4585-L4673.
        // ------------------------------------------------------------------

        // Mirrors IsDedup (private in CopyEngine)
        private bool IsDedup(string orderId, double limitPrice)
        {
            if (!_dedupCache.TryAdd(orderId, limitPrice))
                return true;
            return false;
        }

        // Mirrors IsEntryDispatched (private in CopyEngine)
        private bool IsEntryDispatched(string orderId)
        {
            if (_entryDispatchedOrders.ContainsKey(orderId))
                return true;
            _entryDispatchedOrders.TryAdd(orderId, 0);
            return false;
        }

        // Mirrors IsLiveEntryBlocked (private in CopyEngine) -- production CYC=4
        private bool IsLiveEntryBlocked(string instrKey, string orderId, double limitPrice)
        {
            if (_liveEntryInstruments.ContainsKey(instrKey))
                return true;
            if (IsDedup(orderId, limitPrice))
                return true;
            if (IsEntryDispatched(orderId))
                return true;
            _liveEntryInstruments.TryAdd(instrKey, 0);
            _entryInstrKeyByOrderId.TryAdd(orderId, instrKey);
            return false;
        }

        // Mirrors ClearLiveEntryForInstrument (private in CopyEngine) -- production CYC=2/3
        private void ClearLiveEntryForInstrument(string instrFullName)
        {
            foreach (var key in _liveEntryInstruments.Keys)
            {
                if (key.StartsWith(instrFullName + "|", System.StringComparison.Ordinal))
                    _liveEntryInstruments.TryRemove(key, out _);
            }
        }

        // Inline enum constants replacing NT8 OrderState (not available in net8.0 test project)
        private static class OrderStateInline
        {
            internal const int Filled = 0;
            internal const int Cancelled = 1;
            internal const int Rejected = 2;
        }

        // Mirrors EvictDedup (internal in CopyEngine) -- production CYC=5
        // Uses int instead of NT8 OrderState enum (NT8 runtime not available in net8.0 tests)
        private void EvictDedup(string orderId, int state)
        {
            if (
                state != OrderStateInline.Filled
                && state != OrderStateInline.Cancelled
                && state != OrderStateInline.Rejected
            )
                return;

            _dedupCache.TryRemove(orderId, out _);

            if (state == OrderStateInline.Cancelled)
            {
                _entryDispatchedOrders.TryRemove(orderId, out _);
                if (_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey))
                    _liveEntryInstruments.TryRemove(cancelledInstrKey, out _);
            }

            if (state == OrderStateInline.Filled)
            {
                _entryInstrKeyByOrderId.TryRemove(orderId, out _);
                // NOTE: _liveEntryInstruments intentionally NOT cleared on Filled -- mirrors production
            }
        }

        // ------------------------------------------------------------------
        // Test 1: DW-B142-MGC-01 -- first dispatch allowed, instrument key recorded.
        // CYC=1 (linear arrange/act/assert, no branching).
        // ------------------------------------------------------------------
        [Fact]
        public void IsLiveEntryBlocked_FirstDispatch_Allowed()
        {
            // Arrange: fresh state (new instance per [Fact])
            // instrKey = "MGC DEC26|Sell", orderId = "1001", limitPrice = 2250.0

            // Act
            var result = IsLiveEntryBlocked("MGC DEC26|Sell", "1001", 2250.0);

            // Assert
            Assert.False(result);                                              // dispatch allowed
            Assert.True(_liveEntryInstruments.ContainsKey("MGC DEC26|Sell")); // instrument key recorded
            Assert.Equal("MGC DEC26|Sell", _entryInstrKeyByOrderId["1001"]);  // companion map written
        }

        // ------------------------------------------------------------------
        // Test 2: DW-B142-MGC-02 -- cancel+resubmit dup blocked by instrument guard.
        // CYC=1 (linear arrange/act/assert, no branching).
        // ------------------------------------------------------------------
        [Fact]
        public void IsLiveEntryBlocked_SameInstrNewOrderId_Blocked()
        {
            // Arrange: seed instrument guard via first dispatch
            IsLiveEntryBlocked("MGC DEC26|Sell", "1001", 2250.0);
            // new orderId=1002, different price bypasses price-keyed dedup

            // Act
            var result = IsLiveEntryBlocked("MGC DEC26|Sell", "1002", 2251.0);

            // Assert
            Assert.True(result);                                               // blocked by instrument guard
            Assert.True(_liveEntryInstruments.ContainsKey("MGC DEC26|Sell")); // guard still present
        }

        // ------------------------------------------------------------------
        // Test 3: DW-B142-MGC-02 -- cancel eviction releases instrument guard.
        // CYC=1 (linear arrange/act/assert, no branching).
        // ------------------------------------------------------------------
        [Fact]
        public void EvictDedup_Cancelled_RemovesInstrKey()
        {
            // Arrange: seed full state via first dispatch
            IsLiveEntryBlocked("MGC DEC26|Sell", "1001", 2250.0);

            // Act
            EvictDedup("1001", OrderStateInline.Cancelled);

            // Assert
            Assert.False(_liveEntryInstruments.ContainsKey("MGC DEC26|Sell")); // instrument guard released
            Assert.False(_entryInstrKeyByOrderId.ContainsKey("1001"));          // companion map cleaned
            Assert.False(_entryDispatchedOrders.ContainsKey("1001"));           // scoped removal (not Clear)
            Assert.False(_dedupCache.ContainsKey("1001"));                      // dedup cache cleaned
        }

        // ------------------------------------------------------------------
        // Test 4: DW-B142-MGC-02 -- after cancel evict, next orderId allowed through once.
        // CYC=1 (linear arrange/act/assert, no branching).
        // ------------------------------------------------------------------
        [Fact]
        public void EvictDedup_Cancelled_AllowsSubsequentDispatch()
        {
            // Arrange: first dispatch, then cancel eviction
            IsLiveEntryBlocked("MGC DEC26|Sell", "1001", 2250.0);
            EvictDedup("1001", OrderStateInline.Cancelled);

            // Act: new orderId after resubmit
            var result = IsLiveEntryBlocked("MGC DEC26|Sell", "1002", 2251.0);

            // Assert
            Assert.False(result);                                              // new orderId allowed through
            Assert.True(_liveEntryInstruments.ContainsKey("MGC DEC26|Sell")); // guard re-armed for 1002
            Assert.Equal("MGC DEC26|Sell", _entryInstrKeyByOrderId["1002"]);  // companion map updated
        }

        // ------------------------------------------------------------------
        // Test 5: DW-B142-MGC-01 -- flat path, ClearLiveEntryForInstrument scopes removal correctly.
        // CYC=1 (linear arrange/act/assert, no branching).
        // ------------------------------------------------------------------
        [Fact]
        public void ClearLiveEntryForInstrument_RemovesAllKeys()
        {
            // Arrange: seed three keys directly
            _liveEntryInstruments.TryAdd("MGC DEC26|Buy", 0);
            _liveEntryInstruments.TryAdd("MGC DEC26|Sell", 0);
            _liveEntryInstruments.TryAdd("NQ SEP26|Buy", 0);  // different instrument -- must NOT be removed

            // Act
            ClearLiveEntryForInstrument("MGC DEC26");

            // Assert
            Assert.False(_liveEntryInstruments.ContainsKey("MGC DEC26|Buy"));  // removed
            Assert.False(_liveEntryInstruments.ContainsKey("MGC DEC26|Sell")); // removed
            Assert.True(_liveEntryInstruments.ContainsKey("NQ SEP26|Buy"));    // untouched
        }

        // ------------------------------------------------------------------
        // Test 6: DW-B142-MGC-01 -- filled path, companion map cleaned, instrument guard preserved.
        // CYC=1 (linear arrange/act/assert, no branching).
        // ------------------------------------------------------------------
        [Fact]
        public void EvictDedup_Filled_PreservesInstrKey()
        {
            // Arrange: first dispatch
            IsLiveEntryBlocked("MGC DEC26|Sell", "1001", 2250.0);

            // Act
            EvictDedup("1001", OrderStateInline.Filled);

            // Assert
            Assert.False(_entryInstrKeyByOrderId.ContainsKey("1001"));         // companion map cleaned (lazy)
            Assert.True(_liveEntryInstruments.ContainsKey("MGC DEC26|Sell"));  // instrument guard PRESERVED
        }

        // ------------------------------------------------------------------
        // Test 7: DW-B142-MGC-02 -- non-entry cancel does not corrupt instrument guards.
        // CYC=1 (linear arrange/act/assert, no branching).
        // ------------------------------------------------------------------
        [Fact]
        public void EvictDedup_NonEntryCancel_DoesNotClearOtherGuards()
        {
            // Arrange: instrument guard set for a different orderId; 9999 is a bracket order
            _liveEntryInstruments.TryAdd("MGC DEC26|Sell", 0);  // belongs to a different entry orderId
            _dedupCache.TryAdd("9999", 2250.0);                  // 9999 in dedup cache (bracket order)
            // _entryInstrKeyByOrderId does NOT contain "9999" -- it is not an entry dispatch

            // Act
            EvictDedup("9999", OrderStateInline.Cancelled);

            // Assert -- TryRemove on missing key is a silent no-op
            Assert.True(_liveEntryInstruments.ContainsKey("MGC DEC26|Sell"));  // untouched (9999 is not entry)
            Assert.False(_dedupCache.ContainsKey("9999"));                      // dedup cleaned for 9999
            Assert.False(_entryDispatchedOrders.ContainsKey("9999"));           // scoped removal only
        }
    }
}