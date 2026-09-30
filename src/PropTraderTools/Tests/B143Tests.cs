// B143Tests.cs -- xUnit tests for B143 DW-B142-MGC-02 instrument-level entry guard.
// 7 [Fact] tests verify: first-call dispatch allowed, second-call duplicate blocked,
// EvictDedup(Cancelled) clears instrument guard, EvictDedup(Filled) preserves guard,
// ClearLiveEntryForInstrument prefix-removes all direction keys, no-op on missing key,
// bracket cancel does not clear entry guard (scoped-removal contract).
// Testability: 5 thin shims in CopyEngine.cs #region B143 test seam (inserted after L3511).
// All shims granted via [assembly: InternalsVisibleTo("PropTraderTools.Tests")] at CopyEngine.cs L46.
// Framework: xUnit only ([Fact]). No NUnit. No MSTest.
// ASCII-only. No lock(). No throw. No return null. No async void.
// DO NOT MODIFY any existing test file.
using NinjaTrader.Cbi;
using Xunit;

namespace PropTraderTools.Tests
{
    public class B143Tests
    {
        // T_B143_01 -- IsLiveEntryBlocked first call returns false (dispatch allowed).
        // Verifies: fresh instrKey + orderId -> gate allows dispatch.
        // CYC=1 (single linear path, no branches).
        [Fact]
        public void T_B143_01_IsLiveEntryBlocked_FirstCall_ReturnsFalse_AllowsDispatch()
        {
            var engine = CopyEngine.Instance;
            bool result = engine.IsLiveEntryBlocked_ForTest(
                "TEST-B143-01|Sell",
                "ORD-B143-01",
                2000.0
            );
            Assert.False(result);
        }

        // T_B143_02 -- Second call same instrKey returns true (duplicate blocked).
        // Verifies: instrument-level guard fires on different orderId, same instrKey.
        // CYC=1 (single linear path, no branches).
        [Fact]
        public void T_B143_02_IsLiveEntryBlocked_SecondCall_SameInstrKey_ReturnsTrue_BlocksDuplicate()
        {
            var engine = CopyEngine.Instance;
            bool firstResult = engine.IsLiveEntryBlocked_ForTest(
                "TEST-B143-02|Sell",
                "ORD-B143-02A",
                2000.0
            );
            bool secondResult = engine.IsLiveEntryBlocked_ForTest(
                "TEST-B143-02|Sell",
                "ORD-B143-02B",
                2000.0
            );
            Assert.False(firstResult);
            Assert.True(secondResult);
        }

        // T_B143_03 -- EvictDedup(Cancelled) clears instrKey; future entry unblocked.
        // Verifies: cancel path removes companion map entry + live entry guard.
        // CYC=1 (single linear path, no branches).
        [Fact]
        public void T_B143_03_EvictDedup_Cancelled_ClearsInstrKey_FutureEntryUnblocked()
        {
            var engine = CopyEngine.Instance;
            engine.IsLiveEntryBlocked_ForTest("TEST-B143-03|Sell", "ORD-B143-03", 2000.0);
            engine.EvictDedup_ForTest("ORD-B143-03", OrderState.Cancelled);
            bool afterCancel = engine.IsLiveEntryBlocked_ForTest(
                "TEST-B143-03|Sell",
                "ORD-B143-03C",
                2000.0
            );
            Assert.False(afterCancel);
            Assert.False(engine.EntryInstrKeyByOrderIdContains_ForTest("ORD-B143-03"));
        }

        // T_B143_04 -- EvictDedup(Filled) does NOT clear instrKey; trade still live.
        // Verifies: fill path cleans companion map only; _liveEntryInstruments preserved.
        // CYC=1 (single linear path, no branches).
        [Fact]
        public void T_B143_04_EvictDedup_Filled_DoesNotClear_TradeStillLive()
        {
            var engine = CopyEngine.Instance;
            engine.IsLiveEntryBlocked_ForTest("TEST-B143-04|Sell", "ORD-B143-04", 2000.0);
            engine.EvictDedup_ForTest("ORD-B143-04", OrderState.Filled);
            bool afterFill = engine.IsLiveEntryBlocked_ForTest(
                "TEST-B143-04|Sell",
                "ORD-B143-04F",
                2000.0
            );
            Assert.True(afterFill);
        }

        // T_B143_05 -- ClearLiveEntryForInstrument removes all keys with prefix.
        // Verifies: both "TEST-B143-05|Sell" and "TEST-B143-05|Buy" removed in one call.
        // CYC=1 (single linear path, no branches).
        [Fact]
        public void T_B143_05_ClearLiveEntryForInstrument_RemovesAllKeysWithPrefix()
        {
            var engine = CopyEngine.Instance;
            engine.IsLiveEntryBlocked_ForTest("TEST-B143-05|Sell", "ORD-B143-05A", 2000.0);
            engine.IsLiveEntryBlocked_ForTest("TEST-B143-05|Buy", "ORD-B143-05B", 2000.0);
            engine.ClearLiveEntryForInstrument_ForTest("TEST-B143-05");
            Assert.False(
                engine.IsLiveEntryBlocked_ForTest("TEST-B143-05|Sell", "ORD-B143-05C", 0.0)
            );
            Assert.False(
                engine.IsLiveEntryBlocked_ForTest("TEST-B143-05|Buy", "ORD-B143-05D", 0.0)
            );
        }

        // T_B143_06 -- ClearLiveEntryForInstrument is no-op when no matching key.
        // Verifies: no exception thrown; unrelated key survives intact.
        // CYC=1 (single linear path, no branches).
        [Fact]
        public void T_B143_06_ClearLiveEntryForInstrument_IsNoOp_WhenNoMatchingKey()
        {
            var engine = CopyEngine.Instance;
            engine.IsLiveEntryBlocked_ForTest("UNRELATED-INSTR|Sell", "ORD-B143-06U", 0.0);
            engine.ClearLiveEntryForInstrument_ForTest("INSTRUMENT_NOT_PRESENT");
            Assert.True(
                engine.IsLiveEntryBlocked_ForTest("UNRELATED-INSTR|Sell", "ORD-B143-06X", 0.0)
            );
        }

        // T_B143_07 -- EvictDedup(bracketOrderId, Cancelled) does NOT clear live entry guard.
        // Verifies: scoped-removal contract -- bracket orderId not in _entryInstrKeyByOrderId,
        // so TryRemove returns false and _liveEntryInstruments key for the entry survives.
        // CYC=1 (single linear path, no branches).
        [Fact]
        public void T_B143_07_EvictDedup_BracketCancelOrderId_DoesNotClearLiveEntryGuard()
        {
            var engine = CopyEngine.Instance;
            engine.IsLiveEntryBlocked_ForTest("TEST-B143-07|Sell", "ORD-B143-07A", 2000.0);
            engine.EvictDedup_ForTest("BRACKET-ORD-B143-07", OrderState.Cancelled);
            Assert.True(engine.LiveEntryInstrumentsContains_ForTest("TEST-B143-07|Sell"));
            Assert.True(
                engine.IsLiveEntryBlocked_ForTest("TEST-B143-07|Sell", "ORD-B143-07B", 2000.0)
            );
        }
    }
}
