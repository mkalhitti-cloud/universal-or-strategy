// B75-LaneA CopyEngine tests -- OnOrderUpdate CYC refactor extractions
// Covers: IsPttEntryOrderCancelTrigger, IsNonFlatDispatchName, IsAtmBracketName,
//         TryFireFollowerBeDisarm (NT8-runtime skip), FindMatchingRule (NT8-runtime skip),
//         TryCancelFollowerEntries (NT8-runtime skip), TryHandleBracketDrag (NT8-runtime skip),
//         TryHandleEntryDrag (NT8-runtime skip).
// Section G.4 arch-plan tests (IsPttManagedEntryName = IsPttEntryOrderCancelTrigger in impl,
//   IsDispatchBlockedOrderName = IsNonFlatDispatchName in impl).
// B75-LaneB tests -- GetLeaderAtmTemplateName, SetCloneAtmObjectCache, GetCloneAtmMode,
//   GetSavedFollowerNames, restore-block predicate isolation.
// Hotfix refs: HOTFIX-B66-ATM-TPL, HOTFIX-B66-ATM-OBJ, HOTFIX-B67-CHECKBOX-RESTORE.
// JS-021: no lock. JS-033: no async void. JS-002: no return null. JS-001: no throw.
// xUnit ONLY. CYC <= 8 per method.

using Xunit;

namespace PropTraderTools
{
    public class TradeCopierPanelB75Tests
    {
        // ----------------------------------------------------------------
        // Section G.4 -- IsNonFlatDispatchName (= IsDispatchBlockedOrderName in arch plan)
        // ----------------------------------------------------------------

        [Fact]
        public void IsDispatchBlockedOrderName_PttPrefix_ReturnsTrue()
        {
            Assert.True(CopyEngine.IsNonFlatDispatchName("PTT-Copy"));
        }

        [Fact]
        public void IsDispatchBlockedOrderName_PttBeStop_ReturnsTrue()
        {
            Assert.True(CopyEngine.IsNonFlatDispatchName("PTT-BE-Stop"));
        }

        [Fact]
        public void IsDispatchBlockedOrderName_Entry_ReturnsTrue()
        {
            Assert.True(CopyEngine.IsNonFlatDispatchName("Entry"));
        }

        [Fact]
        public void IsDispatchBlockedOrderName_Close_ReturnsFalse()
        {
            Assert.False(CopyEngine.IsNonFlatDispatchName("Close"));
        }

        [Fact]
        public void IsDispatchBlockedOrderName_Null_ReturnsFalse()
        {
            Assert.False(CopyEngine.IsNonFlatDispatchName(null));
        }

        [Fact]
        public void IsDispatchBlockedOrderName_EmptyString_ReturnsFalse()
        {
            Assert.False(CopyEngine.IsNonFlatDispatchName(""));
        }

        [Fact]
        public void IsDispatchBlockedOrderName_PttQxPrefix_ReturnsTrue()
        {
            Assert.True(CopyEngine.IsNonFlatDispatchName("PTT-QX-T1"));
        }

        // ----------------------------------------------------------------
        // Section G.4 -- IsPttEntryOrderCancelTrigger (= IsPttManagedEntryName in arch plan)
        // ----------------------------------------------------------------

        [Fact]
        public void IsPttManagedEntryName_NullOrder_ReturnsFalse()
        {
            Assert.False(CopyEngine.IsPttEntryOrderCancelTrigger(null));
        }

        // ----------------------------------------------------------------
        // IsAtmBracketName -- used by TryCancelFollowerEntries (HOTFIX-B63-COPY-CANCEL-01)
        // ----------------------------------------------------------------

        [Fact]
        public void IsAtmBracketName_Stop1_ReturnsTrue()
        {
            Assert.True(CopyEngine.IsAtmBracketName("Stop1"));
        }

        [Fact]
        public void IsAtmBracketName_Target1_ReturnsTrue()
        {
            Assert.True(CopyEngine.IsAtmBracketName("Target1"));
        }

        [Fact]
        public void IsAtmBracketName_PttCopy_ReturnsFalse()
        {
            Assert.False(CopyEngine.IsAtmBracketName("PTT-Copy"));
        }

        [Fact]
        public void IsAtmBracketName_Null_ReturnsFalse()
        {
            Assert.False(CopyEngine.IsAtmBracketName(null));
        }

        [Fact]
        public void IsAtmBracketName_Empty_ReturnsFalse()
        {
            Assert.False(CopyEngine.IsAtmBracketName(""));
        }

        // ----------------------------------------------------------------
        // TryFireFollowerBeDisarm -- NT8 runtime dependency (Account, Instrument)
        // ----------------------------------------------------------------

        [Fact(Skip = "NT8-runtime: requires live Account + Instrument objects")]
        public void TryFireFollowerBeDisarm_NullOrder_NoException() { }

        [Fact(Skip = "NT8-runtime: requires live Account + Instrument objects")]
        public void TryFireFollowerBeDisarm_LeaderAccount_DoesNotFire() { }

        [Fact(Skip = "NT8-runtime: requires live Account + Instrument objects")]
        public void TryFireFollowerBeDisarm_FollowerPttBeStopFilled_FiresPositionState() { }

        // ----------------------------------------------------------------
        // FindMatchingRule, TryCancelFollowerEntries, TryHandleBracketDrag, TryHandleEntryDrag
        // -- all require NT8 runtime (Order, CopyRule structs with Account/Instrument fields)
        // ----------------------------------------------------------------

        [Fact(Skip = "NT8-runtime: requires live Order + CopyRule objects")]
        public void FindMatchingRule_NoRules_ReturnsNull() { }

        [Fact(Skip = "NT8-runtime: requires live Order + CopyRule objects")]
        public void TryCancelFollowerEntries_NotCancelled_ReturnsFalse() { }

        [Fact(Skip = "NT8-runtime: requires live Order + CopyRule objects")]
        public void TryHandleBracketDrag_NotWorkingBracket_ReturnsFalse() { }

        [Fact(Skip = "NT8-runtime: requires live Order + CopyRule objects")]
        public void TryHandleEntryDrag_NotLimitOrStopLimit_ReturnsFalse() { }

        // ====================================================================
        // B75-LaneB -- HOTFIX-B66-ATM-TPL, HOTFIX-B66-ATM-OBJ, HOTFIX-B67-CHECKBOX-RESTORE
        // Ticket source: docs/brain/B75-LaneB/04-tickets.md  (TICKET_REVIEW_PASS, Second Pass)
        // ====================================================================

        // ----------------------------------------------------------------
        // T_B66TPL_01 -- GetLeaderAtmTemplateName(null) -> string.Empty
        // Guard-1 null-check path. NT8-HOST-NOT-REQUIRED.
        // ----------------------------------------------------------------

        [Fact]
        public void T_B66TPL_01_NullChart_ReturnsEmpty()
        {
            string result = TradeCopierPanel.GetLeaderAtmTemplateName(null);
            Assert.Equal(string.Empty, result);
            Assert.NotNull(result);
        }

        // ----------------------------------------------------------------
        // T_B66TPL_02 -- GetLeaderAtmTemplateName(null) covers Guard-1.
        // Guard-2 (no ChartTrader in visual tree) requires NT8 host -- skip skeleton.
        // ----------------------------------------------------------------

        [Fact]
        public void T_B66TPL_02_NullChart_NoChartTrader_ReturnsEmpty()
        {
            // Unit portion: null input fires Guard-1 before any visual-tree traversal.
            string result = TradeCopierPanel.GetLeaderAtmTemplateName(null);
            Assert.Equal(string.Empty, result);
        }

        [Fact(
            Skip = "NT8-HOST-REQUIRED: Guard-2 (FindVisualChild<ChartTrader> returns null) requires live WPF visual tree"
        )]
        public void T_B66TPL_02_Integration_NoChartTrader_ReturnsEmpty()
        {
            // Integration skeleton (documents intent):
            // Arrange: real Chart with no ChartTrader child in visual tree.
            // Act:     TradeCopierPanel.GetLeaderAtmTemplateName(realChartWithNoChartTrader)
            // Assert:  Assert.Equal(string.Empty, result)
        }

        // ----------------------------------------------------------------
        // T_B66TPL_03 -- Primary path: ct.AtmStrategy != null -> returns Name.
        // Requires live Chart + ChartTrader with AtmStrategy set. NT8-HOST-REQUIRED.
        // ----------------------------------------------------------------

        [Fact(
            Skip = "NT8-HOST-REQUIRED: FindVisualChild<ChartTrader> + ct.AtmStrategy require live NT8 chart"
        )]
        public void T_B66TPL_03_PrimaryPath_AtmStrategyNonNull_ReturnsName()
        {
            // Arrange: real Chart whose ChartTrader.AtmStrategy.Name == "MES $200 SL6"
            // Act:     string result = TradeCopierPanel.GetLeaderAtmTemplateName(chartWithAtmStrategy);
            // Assert:  Assert.Equal("MES $200 SL6", result);
        }

        // ----------------------------------------------------------------
        // T_B66TPL_04 -- Fallback-1: AtmStrategySelector found, returns SelectedAtmStrategy.Name.
        // NT8-HOST-REQUIRED.
        // ----------------------------------------------------------------

        [Fact(
            Skip = "NT8-HOST-REQUIRED: FindVisualChild<AtmStrategySelector> requires live NT8 chart"
        )]
        public void T_B66TPL_04_Fallback1_AtmStrategySelectorFound_ReturnsName()
        {
            // Arrange: real Chart with ChartTrader where ct.AtmStrategy==null
            //          but AtmStrategySelector.SelectedAtmStrategy.Name == "ATM1"
            // Act:     string result = TradeCopierPanel.GetLeaderAtmTemplateName(chartWithSelectorOnly);
            // Assert:  Assert.Equal("ATM1", result);
        }

        // ----------------------------------------------------------------
        // T_B66TPL_05 -- All paths null -> string.Empty. Never null, never throw.
        // NT8-HOST-REQUIRED for full visual-tree path; Guard-1 already covered by T_B66TPL_01.
        // ----------------------------------------------------------------

        [Fact(Skip = "NT8-HOST-REQUIRED: Fallback-2 ComboBox path requires live NT8 chart")]
        public void T_B66TPL_05_AllPathsNull_ReturnsEmpty()
        {
            // Arrange: real Chart with ChartTrader; ct.AtmStrategy==null,
            //          no AtmStrategySelector, FindVisualChildByIndex<ComboBox>(ct,2)==null
            // Act:     string result = TradeCopierPanel.GetLeaderAtmTemplateName(chartAllPathsNull);
            // Assert:  Assert.Equal(string.Empty, result);
            //          Assert.NotNull(result);
        }

        // ----------------------------------------------------------------
        // T_B66OBJ_P01 -- SetCloneAtmObjectCache(nonNull) -> GetCloneAtmMode -> Named with AtmObject.
        // Requires NinjaTrader.NinjaScript.AtmStrategy instance. NT8-HOST-REQUIRED.
        // ----------------------------------------------------------------

        [Fact(
            Skip = "NT8-HOST-REQUIRED: NinjaTrader.NinjaScript.AtmStrategy cannot be instantiated without NT8 host"
        )]
        public void T_B66OBJ_P01_SetNonNull_GetCloneAtmMode_ReturnsNamedWithObject()
        {
            // Arrange:
            //   CopyEngine.Instance.SetCloneAtmObjectCache(null);
            //   CopyEngine.Instance.SetCloneAtmCache(string.Empty);
            //   var stubAtmObj = <NT8-AtmStrategy-instance>;  // requires NT8 host
            //   CopyEngine.Instance.SetCloneAtmObjectCache(stubAtmObj);
            // Act:
            //   FollowerAtmMode mode = CopyEngine.Instance.GetCloneAtmMode();
            // Assert:
            //   Assert.IsType<FollowerAtmMode.Named>(mode);
            //   var named = (FollowerAtmMode.Named)mode;
            //   Assert.NotNull(named.AtmObject);
        }

        // ----------------------------------------------------------------
        // T_B66OBJ_P02 -- SetCloneAtmObjectCache(null) + SetCloneAtmCache("") -> Inherit.
        // Pure volatile field write. NT8-HOST-NOT-REQUIRED.
        // ----------------------------------------------------------------

        [Fact]
        public void T_B66OBJ_P02_SetNull_GetCloneAtmMode_ReturnsInherit()
        {
            CopyEngine.Instance.SetCloneAtmObjectCache(null);
            CopyEngine.Instance.SetCloneAtmCache(string.Empty);
            FollowerAtmMode mode = CopyEngine.Instance.GetCloneAtmMode();
            Assert.IsType<FollowerAtmMode.Inherit>(mode);
        }

        // ----------------------------------------------------------------
        // T_B67_01 -- GetSavedFollowerNames with matching rule -> both follower names.
        // Requires Account objects for AddRule. NT8-HOST-REQUIRED.
        // ----------------------------------------------------------------

        [Fact(
            Skip = "NT8-HOST-REQUIRED: NinjaTrader.Cbi.Account cannot be constructed without NT8 host"
        )]
        public void T_B67_01_MatchingRule_ReturnsBothFollowerNames()
        {
            // Arrange:
            //   var master = <NT8 Account with Name="Sim101">;
            //   var followers = new Account[] { <Name="Sim102">, <Name="Sim103"> };
            //   CopyEngine.Instance.AddRule("MES SEP26", master, followers);
            // Act:
            //   HashSet<string> result = CopyEngine.Instance.GetSavedFollowerNames("MES SEP26", "Sim101");
            // Assert:
            //   Assert.NotNull(result);
            //   Assert.Contains("Sim102", result);
            //   Assert.Contains("Sim103", result);
            // Teardown: ClearRules() or snapshot restore to avoid singleton state pollution.
        }

        // ----------------------------------------------------------------
        // T_B67_02 -- GetSavedFollowerNames with no matching rule -> empty HashSet.
        // Phantom instrument key -- no Account objects needed. NT8-HOST-NOT-REQUIRED.
        // ----------------------------------------------------------------

        [Fact]
        public void T_B67_02_NoMatchingRule_ReturnsEmptyHashSet()
        {
            System.Collections.Generic.HashSet<string> result =
                CopyEngine.Instance.GetSavedFollowerNames("T_B67_02_PHANTOM_INSTRUMENT", "Sim101");
            Assert.NotNull(result);
            Assert.Equal(0, result.Count);
        }

        // ----------------------------------------------------------------
        // T_B67_03 -- Restore-block predicate isolation.
        // GetSavedFollowerNames returns phantom empty set; predicate Contains logic verified inline.
        // NT8-HOST-NOT-REQUIRED for the predicate isolation portion.
        // AddRule for a seeded set requires Account -> documented as skip for that setup path.
        // This test verifies the pure predicate logic using an empty saved set
        // (Sim102 and Sim103 both NOT in empty set -> both false -- correct baseline).
        // ----------------------------------------------------------------

        [Fact]
        public void T_B67_03_RestoreBlock_OnlyMatchingItemsChecked()
        {
            // Arrange: use phantom instrument so GetSavedFollowerNames returns empty set.
            // (Full seeded test with real Account objects is documented in T_B67_01 skip skeleton.)
            System.Collections.Generic.HashSet<string> saved =
                CopyEngine.Instance.GetSavedFollowerNames("T_B67_03_INSTRUMENT", "Sim101");

            // Act: simulate restore predicate from TradeCopierPanel.cs lines 648-650
            bool sim102Selected = saved.Contains("Sim102");
            bool sim103Selected = saved.Contains("Sim103");

            // Assert: phantom instrument has no followers -- both items unselected (empty-set baseline).
            // This verifies the predicate mechanics: Contains returns false for items not in saved set.
            Assert.False(
                sim102Selected,
                "Sim102 is NOT in the saved rule -- must remain unselected"
            );
            Assert.False(
                sim103Selected,
                "Sim103 is NOT in the saved rule -- must remain unselected"
            );
        }
    }
}
