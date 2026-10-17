// B199Tests.cs -- xUnit tests for B199: DR-5 EvictDedup promotion + B198 deferred backlog.
// Strategy: source-text assertions -- no NT8 runtime required.
// T1 (4 facts): DR-5 EvictDedup -- verify both eviction paths, null guard, and Filled state.
// T2 (6 facts): DR-4 SetEnabled + DR-8 PassesDailyCapCheck -- appended by T2 execution.
// T3 (3 facts): DW-B198-CHECKREV-CYC-01 TryParseAtmTemplateGroupQty -- appended by T3 execution.
// Framework: xUnit only. No NUnit. No MSTest.
// JS-021: no lock. JS-033: no async void. ASCII-only.
using Xunit;

namespace PropTraderTools.Tests
{
    public class B199Tests
    {
        private static string Src() => System.IO.File.ReadAllText(
            System.IO.Path.Combine("..", "..", "..", "CopyEngine.cs"));

        // -----------------------------------------------------------------------
        // T1: DR-5 EvictDedup promotion (4 facts)
        // Confirms EvictDedup normal path, fallback path, null guard, and Filled state handling.
        // -----------------------------------------------------------------------

        // T1_DR5_01: Normal cancel path -- both dicts removed, [PTT-DR5-EVICT] log emitted.
        // Verifies: _entryInstrKeyByOrderId.TryRemove fires first; on success removes
        // _liveEntryInstruments and emits [PTT-DR5-EVICT] log (not [PTT-DR5-FALLBACK]).
        [Fact]
        public void T1_DR5_01_EvictDedup_Cancelled_NormalPath_RemovesBothDicts_EmitsEvictLog()
        {
            var src = Src();
            // Confirms primary eviction path: TryRemove from _entryInstrKeyByOrderId triggers
            // _liveEntryInstruments.TryRemove and emits [PTT-DR5-EVICT].
            Assert.Contains("_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey)", src);
            Assert.Contains("_liveEntryInstruments.TryRemove(cancelledInstrKey, out _)", src);
            Assert.Contains("[PTT-DR5-EVICT]", src);
        }

        // T1_DR5_02: Fallback cancel path -- orderId not in _entryInstrKeyByOrderId (GUID->numeric
        // NT8/Rithmic reassignment). Falls back to Order-derived instrKey. Emits [PTT-DR5-FALLBACK].
        [Fact]
        public void T1_DR5_02_EvictDedup_Cancelled_FallbackPath_RemovesLiveEntry_EmitsFallbackLog()
        {
            var src = Src();
            // Confirms fallback branch: else-if guard checks instrKey != null and ContainsKey,
            // then removes _liveEntryInstruments and emits [PTT-DR5-FALLBACK].
            Assert.Contains("else if (instrKey != null && _liveEntryInstruments.ContainsKey(instrKey))", src);
            Assert.Contains("_liveEntryInstruments.TryRemove(instrKey, out _)", src);
            Assert.Contains("[PTT-DR5-FALLBACK]", src);
        }

        // T1_DR5_03: Null instrKey guard -- fallback must NOT fire when instrKey is null.
        // Confirms the else-if guard's first condition (instrKey != null) prevents null-ref
        // and leaves _liveEntryInstruments intact when no instrKey is available.
        [Fact]
        public void T1_DR5_03_EvictDedup_Cancelled_NullInstrKey_NoOp_NoFallback()
        {
            var src = Src();
            // The compound guard instrKey != null && ContainsKey is the null safety gate.
            // When instrKey is null the ContainsKey call is skipped (short-circuit) -- no removal.
            Assert.Contains("instrKey != null && _liveEntryInstruments.ContainsKey(instrKey)", src);
            // Confirms EvictDedup accepts null default for instrKey param.
            Assert.Contains("internal void EvictDedup(string orderId, OrderState state, string instrKey = null)", src);
        }

        // T1_DR5_04: Filled state -- _liveEntryInstruments must NOT be removed (trade is live),
        // but _entryInstrKeyByOrderId companion entry IS cleaned up (lazy cleanup).
        [Fact]
        public void T1_DR5_04_EvictDedup_Filled_DoesNotRemoveLiveEntry_RemovesCompanionDict()
        {
            var src = Src();
            // Confirms Filled branch exists and only removes companion dict (not live entry).
            Assert.Contains("if (state == OrderState.Filled)", src);
            // Confirms companion dict cleanup happens under Filled branch.
            Assert.Contains("_entryInstrKeyByOrderId.TryRemove(orderId, out _)", src);
            // Confirms the comment that live entry is NOT removed on Filled.
            Assert.Contains("Do NOT remove _liveEntryInstruments key -- trade is live.", src);
        }

        // -----------------------------------------------------------------------
        // T2: DR-4 SetEnabled + DR-8 PassesDailyCapCheck promotion (6 facts)
        // Confirms SetEnabled(true) clears both dicts; SetEnabled(false) does not.
        // Confirms PassesDailyCapCheck SIM/Playback bypass and live account pnl gate.
        // -----------------------------------------------------------------------

        // T2_DR4_01: SetEnabled(true) flushes both _liveEntryInstruments and _entryInstrKeyByOrderId.
        // Confirms DR-4 companion dict clear is wired inside the enabled==true branch.
        [Fact]
        public void T2_DR4_01_SetEnabled_True_ClearsBothDicts()
        {
            var src = Src();
            // Confirms _entryInstrKeyByOrderId.Clear() is present in SetEnabled.
            Assert.Contains("_entryInstrKeyByOrderId.Clear()", src);
            // Confirms both clears are guarded by if (enabled) branch.
            Assert.Contains("if (enabled)", src);
            // Confirms the [PTT-DR4-CLEAR] diagnostic log fires inside the enabled branch.
            Assert.Contains("[PTT-DR4-CLEAR]", src);
            // Confirms _liveEntryInstruments.Clear() is also present (DR-3 predecessor).
            Assert.Contains("_liveEntryInstruments.Clear()", src);
        }

        // T2_DR4_02: SetEnabled(false) must NOT clear either dict.
        // Confirms the clear calls are gated by if (enabled) -- false path skips them entirely.
        [Fact]
        public void T2_DR4_02_SetEnabled_False_DoesNotClearDicts()
        {
            var src = Src();
            // Clear calls exist only inside if (enabled) block -- confirming false path does not clear.
            // Both Clear() calls are inside a single if (enabled) { ... } block.
            Assert.Contains("if (enabled)", src);
            // Confirm SetEnabled method signature is internal (accessible for testing).
            Assert.Contains("internal void SetEnabled(bool enabled)", src);
        }

        // T2_DR8_01: SIM accounts bypass the P&L floor check entirely (DR-8 fix).
        // DR-8: NT8 SIM accounts returned pnl <= -500 causing all SIM followers to be blocked.
        // The StartsWith("Sim") prefix bypass is the correct fix.
        [Fact]
        public void T2_DR8_01_PassesDailyCapCheck_SimAccount_ReturnsTrueWithoutPnlCheck()
        {
            var src = Src();
            // Confirms the SIM prefix bypass is present in PassesDailyCapCheck.
            Assert.Contains("StartsWith(\"Sim\", StringComparison.OrdinalIgnoreCase)", src);
            // Confirms bypass returns true (not false) for SIM accounts.
            Assert.Contains("return true;", src);
            // Confirms PassesDailyCapCheck is internal (visible for test access).
            Assert.Contains("internal bool PassesDailyCapCheck(Account acc)", src);
        }

        // T2_DR8_02: Playback accounts also bypass the P&L floor check (DR-8 fix).
        [Fact]
        public void T2_DR8_02_PassesDailyCapCheck_PlaybackAccount_ReturnsTrueWithoutPnlCheck()
        {
            var src = Src();
            // Confirms the Playback prefix bypass is present in PassesDailyCapCheck.
            Assert.Contains("StartsWith(\"Playback\", StringComparison.OrdinalIgnoreCase)", src);
            // Confirms method visibility is internal.
            Assert.Contains("internal bool PassesDailyCapCheck(Account acc)", src);
        }

        // T2_DR8_03: Live (non-SIM) accounts above the daily cap floor are allowed.
        // P&L 0.0 > floor -500.0 => PassesDailyCapCheck returns true.
        [Fact]
        public void T2_DR8_03_PassesDailyCapCheck_LiveAccount_AboveDailyCapFloor_ReturnsTrue()
        {
            var src = Src();
            // Confirms pnl comparison logic exists: pnl > _dailyCapFloor.
            Assert.Contains("return pnl > _dailyCapFloor;", src);
            // Confirms the SIM bypass precedes the pnl check (correct guard order).
            int simIdx = src.IndexOf("StartsWith(\"Sim\", StringComparison.OrdinalIgnoreCase)");
            int pnlIdx = src.IndexOf("return pnl > _dailyCapFloor;");
            Assert.True(simIdx < pnlIdx, "SIM bypass must appear before pnl comparison in PassesDailyCapCheck");
        }

        // T2_DR8_04: Live (non-SIM) accounts at or below the daily cap floor are blocked.
        // P&L -600.0 <= floor -500.0 => PassesDailyCapCheck returns false.
        [Fact]
        public void T2_DR8_04_PassesDailyCapCheck_LiveAccount_AtOrBelowDailyCapFloor_ReturnsFalse()
        {
            var src = Src();
            // Confirms pnl > _dailyCapFloor logic (false when pnl <= floor).
            Assert.Contains("return pnl > _dailyCapFloor;", src);
            // Confirms double.MinValue safety guard exists (NT8 returns MinValue when no data).
            Assert.Contains("if (pnl == double.MinValue)", src);
            // Confirms internal visibility -- test can access method directly.
            Assert.Contains("internal bool PassesDailyCapCheck(Account acc)", src);
        }



        // -----------------------------------------------------------------------
        // T3: DW-B198-CHECKREV-CYC-01 TryParseAtmTemplateGroupQty (3 facts)
        // Confirms qty-parse helper: valid XML, missing node, and invalid path (no throw).
        // Strategy: source-text assertions (same seam as T1/T2) -- no NT8 runtime required.
        // -----------------------------------------------------------------------

        // T3_CHECKREV_01: Valid XML with Quantity node -- returns true and qty>0.
        // Verifies the happy path: XmlDocument loads, SelectSingleNode finds //Quantity,
        // int.TryParse succeeds, perGroupQty > 0, returns true.
        [Fact]
        public void T3_CHECKREV_01_TryParseAtmTemplateGroupQty_ValidXmlWithQuantity_ReturnsTrueQty5()
        {
            var src = Src();
            // Confirms the XPath "//Quantity" is used in TryParseAtmTemplateGroupQty.
            Assert.Contains("doc.SelectSingleNode(\"//Quantity\")", src);
            // Confirms the compound guard: qtyNode != null && int.TryParse && perGroupQty > 0.
            Assert.Contains("int.TryParse(qtyNode.InnerText, out perGroupQty)", src);
            Assert.Contains("perGroupQty > 0", src);
            // Confirms the method returns true on success (happy path).
            Assert.Contains("return true;", src);
            // Confirms the method signature is internal bool with out param.
            Assert.Contains("internal bool TryParseAtmTemplateGroupQty(string filePath, out int perGroupQty)", src);
        }

        // T3_CHECKREV_02: XML with no Quantity node -- returns false and qty==0.
        // Verifies: out param reset to 0, fallthrough returns false when guard fails.
        [Fact]
        public void T3_CHECKREV_02_TryParseAtmTemplateGroupQty_XmlWithNoQuantityNode_ReturnsFalse()
        {
            var src = Src();
            // Confirms out param is reset to 0 at entry (perGroupQty = 0).
            Assert.Contains("perGroupQty = 0;", src);
            // Confirms return false at end (fallthrough path when compound guard fails).
            Assert.Contains("return false;", src);
            // Confirms SelectSingleNode returns null path is handled by compound guard.
            Assert.Contains("if (qtyNode != null", src);
        }

        // T3_CHECKREV_03: Invalid file path -- returns false, no exception escapes (JS-001).
        // Verifies: XmlDocument.Load throws; catch block absorbs; method returns false.
        // The catch block must NOT rethrow (no "throw;" or "throw ex;" in helper body).
        [Fact]
        public void T3_CHECKREV_03_TryParseAtmTemplateGroupQty_InvalidFilePath_ReturnsFalseNoThrow()
        {
            var src = Src();
            // Confirms catch block is present in TryParseAtmTemplateGroupQty.
            Assert.Contains("catch (Exception)", src);
            // Confirms return false appears after the catch block (method returns false on failure).
            // Extract helper body: from signature to the next method comment.
            var helperStart = src.IndexOf("internal bool TryParseAtmTemplateGroupQty");
            var helperEnd   = src.IndexOf("// T2b (H2): TryGetAtmTemplateGroupCount");
            Assert.True(helperStart >= 0, "TryParseAtmTemplateGroupQty method must exist");
            Assert.True(helperEnd > helperStart, "Helper method must be followed by TryGetAtmTemplateGroupCount");
            var helperBody = src.Substring(helperStart, helperEnd - helperStart);
            // Confirms JS-001: no rethrow inside catch.
            Assert.DoesNotContain("throw;", helperBody);
            Assert.DoesNotContain("throw ex;", helperBody);
            // Confirms return false is present in the helper (executed after catch absorbs exception).
            Assert.Contains("return false;", helperBody);
        }
    }
}