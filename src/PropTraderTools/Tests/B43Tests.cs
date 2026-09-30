// PTT-COPIER-B43 -- B43Tests.cs
// xUnit [Fact] tests for B43: Per-Follower ATM Template ComboBox.
// Defect fixed: DW-B43-NAMED-TB-01 (Named TextBox keyboard-bubble to NT8 instrument search).
// Fix: Replaced TextBox+mode-ComboBox pair with single ATM template ComboBox.
// Tests: ParseAtmTemplateSelection (Window), GetLeaderAtmTemplateName (Panel), CopyEngine round-trip.
// Jane Street rules: JS-002 (no return null), JS-021 (no lock), JS-033 (no async void).
// xUnit only -- no NUnit, no MSTest.
// CYC: all [Fact] methods = CYC 1 (straight-line assertion bodies).
using System;
using Xunit;

namespace PropTraderTools
{
    /// <summary>
    /// B43: ATM template ComboBox -- ParseAtmTemplateSelection, GetLeaderAtmTemplateName,
    /// and CopyEngine backward-compatibility round-trip.
    /// </summary>
    public class B43Tests
    {
        // -------------------------------------------------------------------------
        // T_B43_01 -- Template name selection produces Named mode
        // -------------------------------------------------------------------------

        /// <summary>
        /// When a real template name is selected, ParseAtmTemplateSelection returns
        /// FollowerAtmMode.Named with the correct TemplateName.
        /// </summary>
        [Fact(Skip="B180-2: TradeCopierWindow.ParseAtmTemplateSelection removed")]`n        public void T_B43_01_OnRowApply_TemplateSelected_ProducesNamedMode()
        {
            // Arrange
            const string sel = "MES $200";

            // Act
            FollowerAtmMode result = TradeCopierWindow.ParseAtmTemplateSelection(sel);

            // Assert
            var named = Assert.IsType<FollowerAtmMode.Named>(result);
            Assert.Equal("MES $200", named.TemplateName);
        }

        // -------------------------------------------------------------------------
        // T_B43_02 -- "(none)" selection produces Inherit mode
        // -------------------------------------------------------------------------

        /// <summary>
        /// When the sentinel "(none)" is selected, ParseAtmTemplateSelection returns
        /// FollowerAtmMode.Inherit (no ATM brackets spawned on follower).
        /// </summary>
        [Fact(Skip="B180-2: TradeCopierWindow.ParseAtmTemplateSelection removed")]`n        public void T_B43_02_OnRowApply_NoneSelected_ProducesInheritMode()
        {
            // Arrange
            const string sel = "(none)";

            // Act
            FollowerAtmMode result = TradeCopierWindow.ParseAtmTemplateSelection(sel);

            // Assert
            Assert.IsType<FollowerAtmMode.Inherit>(result);
        }

        // -------------------------------------------------------------------------
        // T_B43_03 -- null selection produces Inherit mode
        // -------------------------------------------------------------------------

        /// <summary>
        /// When null is passed (e.g. SelectedItem cast fails), ParseAtmTemplateSelection
        /// returns FollowerAtmMode.Inherit -- never throws, never returns null.
        /// </summary>
        [Fact(Skip="B180-2: TradeCopierWindow.ParseAtmTemplateSelection removed")]`n        public void T_B43_03_OnRowApply_NullSelected_ProducesInheritMode()
        {
            // Act
            FollowerAtmMode result = TradeCopierWindow.ParseAtmTemplateSelection(null);

            // Assert
            Assert.IsType<FollowerAtmMode.Inherit>(result);
        }

        // -------------------------------------------------------------------------
        // T_B43_04 -- GetLeaderAtmTemplateName with null chart returns empty string
        // -------------------------------------------------------------------------

        /// <summary>
        /// When _currentChart is null (panel not yet attached to a chart),
        /// GetLeaderAtmTemplateName returns string.Empty and does not throw.
        /// </summary>
        [Fact]
        public void T_B43_04_GetLeaderAtmTemplateName_NullChart_ReturnsEmptyString()
        {
            // Act: pass null directly -- no WPF chart instantiation needed
            string result = TradeCopierPanel.GetLeaderAtmTemplateName(null);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        // -------------------------------------------------------------------------
        // T_B43_05 -- CopyEngine ParseAtmModeName backward-compat round-trip
        // -------------------------------------------------------------------------

        /// <summary>
        /// Rules saved before B43 (with serialization "Named:MES $200") still load
        /// correctly after B43. CopyEngine.ParseAtmModeName and AtmModeToString are
        /// untouched by B43 -- this test confirms the format contract is preserved.
        /// </summary>
        [Fact]
        public void T_B43_05_ParseAtmModeName_RoundTrip_BackwardCompat()
        {
            // Deserialize saved rule: "Named:MES $200" -> Named("MES $200")
            FollowerAtmMode mode = CopyEngine.ParseAtmModeName("Named:MES $200");
            var named = Assert.IsType<FollowerAtmMode.Named>(mode);
            Assert.Equal("MES $200", named.TemplateName);

            // Round-trip: Named("MES $200") -> "Named:MES $200"
            string serialized = CopyEngine.AtmModeToString(new FollowerAtmMode.Named("MES $200"));
            Assert.Equal("Named:MES $200", serialized);
        }
    }
}
